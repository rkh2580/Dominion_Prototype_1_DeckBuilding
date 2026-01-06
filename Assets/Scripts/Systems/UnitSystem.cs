// =============================================================================
// UnitSystem.cs
// 유닛 관리 시스템
// =============================================================================
// [R5 리팩토링] 2026-01-03
// - upgrade → promotion 이름 변경
// - 전직 레벨별 카드풀/비용 구현
// - 전투력 보너스 계산
// - 기존 강화 로직 → 전직 로직으로 통합
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 유닛 시스템 (싱글톤)
    /// - 유닛 생성/사망
    /// - 전직 (Lv.0→1→2→3)
    /// - 직업 선택 (유년→청년)
    /// </summary>
    public class UnitSystem : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static UnitSystem Instance { get; private set; }

        // =====================================================================
        // 무료 전직 상태
        // =====================================================================

        /// <summary>무료 전직 대기 중인 유닛 (null이면 일반 전직)</summary>
        private UnitInstance pendingFreePromotionUnit;

        /// <summary>무료 전직 이벤트 (UpgradePopup에서 구독)</summary>
        public static event Action<UnitInstance> OnFreePromotionRequested;

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>유닛 생성됨</summary>
        public static event Action<UnitInstance> OnUnitCreated;

        /// <summary>유닛 사망함</summary>
        public static event Action<UnitInstance> OnUnitDied;

        /// <summary>유닛 레벨업 전직함 (유닛, 새 전직 레벨, 획득한 카드)</summary>
        public static event Action<UnitInstance, int, CardInstance> OnUnitLeveledUp;

        /// <summary>유닛 직업 변경함 (유년→청년 시 직업 결정) - 기존 OnUnitPromoted와 동일</summary>
        public static event Action<UnitInstance, Job> OnUnitPromoted;

        /// <summary>유닛 직업 선택함 (유년→청년 시 직업 결정) - OnUnitPromoted의 새 이름</summary>
        public static event Action<UnitInstance, Job> OnUnitJobSelected;

        /// <summary>유닛 성장함 (유닛, 이전 단계, 새 단계)</summary>
        public static event Action<UnitInstance, GrowthStage, GrowthStage> OnUnitGrown;

        /// <summary>유닛 직업 선택 필요 (유년→청년 전환 시)</summary>
        public static event Action<UnitInstance> OnUnitNeedsJobSelection;

        // [Obsolete] 하위 호환성 - 추후 삭제
        [Obsolete("Use OnUnitPromoted instead")]
        public static event Action<UnitInstance, int, CardInstance> OnUnitUpgraded;

        [Obsolete("Use OnUnitNeedsJobSelection instead")]
        public static event Action<UnitInstance> OnUnitNeedsPromotion;

        /// <summary>
        /// 직업 선택 필요 이벤트 발생 (TurnManager에서 호출)
        /// </summary>
        public static void RaiseNeedsJobSelection(UnitInstance unit)
        {
            OnUnitNeedsJobSelection?.Invoke(unit);
            // 하위 호환성
#pragma warning disable CS0618
            OnUnitNeedsPromotion?.Invoke(unit);
#pragma warning restore CS0618
        }

        // [Obsolete] 하위 호환성
        [Obsolete("Use RaiseNeedsJobSelection instead")]
        public static void RaiseNeedsPromotion(UnitInstance unit)
        {
            RaiseNeedsJobSelection(unit);
        }

        /// <summary>
        /// 무료 전직 요청 (이벤트 효과용)
        /// 비용 없이 전직 팝업을 띄움
        /// </summary>
        public void RequestFreePromotion(UnitInstance unit)
        {
            if (unit == null || !unit.CanPromote())
            {
                Debug.LogWarning("[UnitSystem] 무료 전직 불가: 조건 미충족");
                return;
            }

            pendingFreePromotionUnit = unit;
            Debug.Log($"[UnitSystem] 무료 전직 요청: {unit.unitName}");

            // 전직 팝업 이벤트 발생
            OnFreePromotionRequested?.Invoke(unit);
        }

        /// <summary>
        /// 무료 전직 대상인지 확인
        /// </summary>
        public bool IsFreePromotion(UnitInstance unit)
        {
            return pendingFreePromotionUnit != null && pendingFreePromotionUnit == unit;
        }

        // =====================================================================
        // 상태 접근
        // =====================================================================

        private GameState State => GameManager.Instance.State;

        public List<UnitInstance> Units => State?.units;
        public int UnitCount => State?.units.Count ?? 0;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Debug.Log("[UnitSystem] 초기화 완료");
        }

        // =====================================================================
        // 유닛 생성
        // =====================================================================

        /// <summary>
        /// 유닛 생성 (직업 기본 카드 자동 부여)
        /// [E3] Default.json 의존 제거 - JobSO.startingCards에서 직접 가져옴
        /// </summary>
        /// <param name="name">유닛 이름</param>
        /// <param name="job">직업</param>
        /// <param name="stage">성장 단계</param>
        /// <param name="grantStartingCards">시작 카드 부여 여부 (기본 true)</param>
        public UnitInstance CreateUnit(string name, Job job, GrowthStage stage, bool grantStartingCards = true)
        {
            if (State == null) return null;

            var unit = UnitInstance.Create(name, job, stage);
            State.units.Add(unit);

            // [E3] 직업 기본 시작 카드 부여
            if (grantStartingCards)
            {
                GrantStartingCards(unit, job);
            }

            // 전투력 재계산
            State.RecalculateCombatPower();

            Debug.Log($"[UnitSystem] 유닛 생성: {name} ({job}, {stage}), 시작카드: {grantStartingCards}");
            OnUnitCreated?.Invoke(unit);

            return unit;
        }

        /// <summary>
        /// [E3] 직업 기본 시작 카드 부여
        /// JobSO.startingCards에서 가져와서 덱에 추가
        /// DeckSystem.AddCardToDeck이 ownedCardIds 추가 + 유지비 재계산 처리
        /// </summary>
        private void GrantStartingCards(UnitInstance unit, Job job)
        {
            if (unit == null) return;

            // JobSO에서 시작 카드 가져오기
            var jobSO = DataLoader.Instance?.JobDatabaseSO?.GetJob(job);
            if (jobSO == null || !jobSO.HasStartingCards)
            {
                Debug.Log($"[UnitSystem] 직업 {job}의 시작 카드 없음 (JobSO 미설정 또는 빈 배열)");
                return;
            }

            // 각 시작 카드를 덱에 추가
            foreach (string cardId in jobSO.startingCards)
            {
                if (string.IsNullOrEmpty(cardId)) continue;

                // AddCardToDeck이 자동으로:
                // 1. 카드 생성 및 덱 추가
                // 2. unit.ownedCardIds에 추가
                // 3. 유지비 재계산 (RecalculateMaintenanceCost)
                var card = DeckSystem.Instance?.AddCardToDeck(cardId, unit.unitId);

                if (card != null)
                {
                    Debug.Log($"[UnitSystem] 시작 카드 부여: {unit.unitName} ← {cardId}");
                }
                else
                {
                    Debug.LogWarning($"[UnitSystem] 시작 카드 부여 실패: {cardId} (카드 데이터 없음?)");
                }
            }
        }

        // =====================================================================
        // 유닛 사망
        // =====================================================================

        /// <summary>
        /// 유닛 사망 처리
        /// </summary>
        public void KillUnit(UnitInstance unit)
        {
            if (State == null || unit == null) return;

            Debug.Log($"[UnitSystem] 유닛 사망: {unit.unitName}");

            // 집에서 유닛 제거 (UI 갱신 트리거)
            if (!string.IsNullOrEmpty(unit.houseId))
            {
                HouseSystem.Instance?.RemoveUnit(unit);
            }

            // 종속 카드 삭제
            RemoveUnitCards(unit.unitId);

            // 유닛 제거
            State.units.Remove(unit);

            // 전투력 재계산
            State.RecalculateCombatPower();

            OnUnitDied?.Invoke(unit);

            // 유지비 재계산
            GameManager.Instance?.RecalculateMaintenanceCost();
        }

        /// <summary>
        /// 유닛의 종속 카드 모두 제거
        /// </summary>
        private void RemoveUnitCards(string unitId)
        {
            RemoveCardsFromList(State.deck, unitId);
            RemoveCardsFromList(State.hand, unitId);
            RemoveCardsFromList(State.discardPile, unitId);
            RemoveCardsFromList(State.playArea, unitId);
        }

        /// <summary>
        /// 리스트에서 특정 유닛 소속 카드 제거
        /// </summary>
        private void RemoveCardsFromList(List<CardInstance> list, string unitId)
        {
            list.RemoveAll(card => card.ownerUnitId == unitId);
        }

        // =====================================================================
        // 전직 (Promotion) - Lv.0 → 1 → 2 → 3
        // =====================================================================

        /// <summary>
        /// 전직 가능 여부 확인
        /// </summary>
        public bool CanPromoteUnit(UnitInstance unit)
        {
            if (State == null || unit == null) return false;
            if (!unit.CanPromote()) return false;

            // 무료 전직이면 골드 체크 스킵
            if (IsFreePromotion(unit)) return true;

            // 골드 체크
            int cost = GetPromotionCost(unit);
            if (State.gold < cost) return false;

            return true;
        }

        /// <summary>
        /// 전직 비용 계산 (할인 적용)
        /// </summary>
        public int GetPromotionCost(UnitInstance unit)
        {
            if (unit == null) return int.MaxValue;

            int baseCost = unit.GetNextPromotionCost();

            // 할인 적용 (행상인 방문 등)
            if (State.upgradeDiscount > 0)
            {
                baseCost = Mathf.CeilToInt(baseCost * (100 - State.upgradeDiscount) / 100f);
            }

            return baseCost;
        }

        /// <summary>
        /// 유닛 전직 (3지선다 후 선택한 카드로)
        /// </summary>
        /// <param name="unit">전직할 유닛</param>
        /// <param name="selectedCardId">선택한 카드 ID</param>
        /// <returns>성공 여부</returns>
        public bool PromoteUnit(UnitInstance unit, string selectedCardId)
        {
            if (!CanPromoteUnit(unit))
            {
                Debug.LogWarning($"[UnitSystem] 전직 불가: {unit?.unitName}");
                return false;
            }

            // 무료 전직 여부 확인 및 플래그 리셋
            bool isFree = IsFreePromotion(unit);
            if (isFree)
            {
                pendingFreePromotionUnit = null;
                Debug.Log($"[UnitSystem] 무료 전직 적용: {unit.unitName}");
            }

            // 비용 지불 (무료 전직이면 스킵)
            if (!isFree)
            {
                int cost = GetPromotionCost(unit);
                if (!GoldSystem.Instance.TrySpendGold(cost))
                {
                    return false;
                }
            }

            // 전직 레벨 증가
            int oldLevel = unit.promotionLevel;
            unit.promotionLevel++;
            unit.promotedThisTurn = true;

            // 카드 추가
            var newCard = DeckSystem.Instance.AddCardToDeck(selectedCardId, unit.unitId);

            // 전투력 재계산
            State.RecalculateCombatPower();

            Debug.Log($"[UnitSystem] 유닛 전직: {unit.unitName} Lv.{oldLevel} → Lv.{unit.promotionLevel}, 카드 획득: {selectedCardId}");

            OnUnitLeveledUp?.Invoke(unit, unit.promotionLevel, newCard);

            // 하위 호환성
#pragma warning disable CS0618
            OnUnitUpgraded?.Invoke(unit, unit.promotionLevel, newCard);
#pragma warning restore CS0618

            return true;
        }

        /// <summary>
        /// 전직 3지선다 카드 생성
        /// </summary>
        public List<string> GetPromotionCardChoices(UnitInstance unit)
        {
            if (unit == null) return new List<string>();

            var jobDef = DataLoader.Instance?.GetJob(unit.job);
            if (jobDef == null) return new List<string>();

            var choices = new List<string>();
            int nextLevel = unit.promotionLevel + 1;

            for (int i = 0; i < 3; i++)
            {
                string cardId = RollCardFromPool(jobDef, nextLevel);
                if (!string.IsNullOrEmpty(cardId))
                {
                    choices.Add(cardId);
                }
            }

            Debug.Log($"[UnitSystem] 전직 선택지 (Lv.{nextLevel}): {string.Join(", ", choices)}");
            return choices;
        }

        /// <summary>
        /// 카드풀에서 확률에 따라 카드 선택
        /// 전직 레벨에 따라 등급 필터링
        /// </summary>
        private string RollCardFromPool(JobDefinition jobDef, int promotionLevel)
        {
            // 전직 레벨에 따른 등급 필터링
            // Lv.1: 기본 + 고급
            // Lv.2: 고급 + 희귀
            // Lv.3: 희귀만
            List<string> availablePools = new List<string>();

            switch (promotionLevel)
            {
                case 1:
                    // 기본 60%, 고급 35%, 희귀 5%
                    availablePools.AddRange(GetWeightedPool(jobDef, includeBasic: true, includeAdvanced: true, includeRare: false));
                    break;
                case 2:
                    // 고급 + 희귀
                    availablePools.AddRange(GetWeightedPool(jobDef, includeBasic: false, includeAdvanced: true, includeRare: true));
                    break;
                case 3:
                    // 희귀만
                    if (jobDef.cardPoolRare != null && jobDef.cardPoolRare.Count > 0)
                    {
                        availablePools.AddRange(jobDef.cardPoolRare);
                    }
                    else if (jobDef.cardPoolAdvanced != null && jobDef.cardPoolAdvanced.Count > 0)
                    {
                        // 희귀가 없으면 고급에서
                        availablePools.AddRange(jobDef.cardPoolAdvanced);
                    }
                    break;
                default:
                    // 기본 풀
                    if (jobDef.cardPoolBasic != null)
                        availablePools.AddRange(jobDef.cardPoolBasic);
                    break;
            }

            if (availablePools.Count == 0)
            {
                Debug.LogWarning($"[UnitSystem] {jobDef.job} Lv.{promotionLevel} 카드풀이 비어있음");
                return null;
            }

            int index = UnityEngine.Random.Range(0, availablePools.Count);
            return availablePools[index];
        }

        /// <summary>
        /// 등급별 가중치 적용한 카드풀 반환
        /// </summary>
        private List<string> GetWeightedPool(JobDefinition jobDef, bool includeBasic, bool includeAdvanced, bool includeRare)
        {
            var result = new List<string>();

            // 등급 확률: 기본 60%, 고급 35%, 희귀 5%
            int roll = UnityEngine.Random.Range(0, 100);

            if (includeRare && roll < GameConfig.RareCardChance &&
                jobDef.cardPoolRare != null && jobDef.cardPoolRare.Count > 0)
            {
                return jobDef.cardPoolRare;
            }
            else if (includeAdvanced && roll < GameConfig.RareCardChance + GameConfig.AdvancedCardChance &&
                     jobDef.cardPoolAdvanced != null && jobDef.cardPoolAdvanced.Count > 0)
            {
                return jobDef.cardPoolAdvanced;
            }
            else if (includeBasic && jobDef.cardPoolBasic != null && jobDef.cardPoolBasic.Count > 0)
            {
                return jobDef.cardPoolBasic;
            }

            // 폴백
            if (includeAdvanced && jobDef.cardPoolAdvanced != null && jobDef.cardPoolAdvanced.Count > 0)
                return jobDef.cardPoolAdvanced;
            if (includeBasic && jobDef.cardPoolBasic != null && jobDef.cardPoolBasic.Count > 0)
                return jobDef.cardPoolBasic;
            if (includeRare && jobDef.cardPoolRare != null && jobDef.cardPoolRare.Count > 0)
                return jobDef.cardPoolRare;

            return result;
        }

        // =====================================================================
        // 직업 선택 (유년 → 청년)
        // =====================================================================

        /// <summary>
        /// 직업 선택지 생성 (유년→청년)
        /// </summary>
        public List<Job> GetJobChoices()
        {
            var choices = new List<Job>();

            // 슬롯 1: 폰 고정
            choices.Add(Job.Pawn);

            // 슬롯 2: 랜덤
            Job slot2 = RollRandomJob();
            choices.Add(slot2);

            // 슬롯 3: 랜덤 (슬롯 2와 중복 불가)
            Job slot3 = RollRandomJobExcluding(slot2);
            choices.Add(slot3);

            Debug.Log($"[UnitSystem] 직업 선택지: {string.Join(", ", choices)}");
            return choices;
        }

        /// <summary>
        /// 랜덤 직업 선택 (확률 기반)
        /// </summary>
        private Job RollRandomJob()
        {
            int roll = UnityEngine.Random.Range(0, 100);

            if (roll < GameConfig.QueenChance) // 10%
                return Job.Queen;
            if (roll < GameConfig.QueenChance + GameConfig.RookChance) // 40%
                return Job.Rook;
            if (roll < GameConfig.QueenChance + GameConfig.RookChance + GameConfig.BishopChance) // 70%
                return Job.Bishop;
            return Job.Knight; // 30%
        }

        /// <summary>
        /// 특정 직업 제외하고 랜덤 선택
        /// </summary>
        private Job RollRandomJobExcluding(Job exclude)
        {
            Job result;
            int attempts = 0;
            do
            {
                result = RollRandomJob();
                attempts++;
            }
            while (result == exclude && attempts < 10);

            return result;
        }

        /// <summary>
        /// 직업 선택 실행 (유년→청년)
        /// </summary>
        public bool SelectJob(UnitInstance unit, Job newJob)
        {
            if (unit == null)
            {
                Debug.LogWarning("[UnitSystem] 직업 선택 불가: 유닛이 null");
                return false;
            }

            // 청년이고 아직 폰(기본 직업)인 경우 선택 가능
            // 또는 유년인 경우도 가능
            bool canSelect = (unit.stage == GrowthStage.Young && unit.job == Job.Pawn) ||
                             (unit.stage == GrowthStage.Child);

            if (!canSelect)
            {
                Debug.LogWarning("[UnitSystem] 직업 선택 불가: 조건 미충족");
                return false;
            }

            Job oldJob = unit.job;
            unit.job = newJob;

            // 아직 유년이면 청년으로 전환
            if (unit.stage == GrowthStage.Child)
            {
                unit.stage = GrowthStage.Young;
                unit.stageRemainingTurns = UnitInstance.GetStageDuration(GrowthStage.Young);
            }

            // [버그수정] 직업 선택 시 해당 직업의 기본 카드(startingCards) 부여
            GrantStartingCards(unit, newJob);

            // 전투력 재계산
            State.RecalculateCombatPower();

            Debug.Log($"[UnitSystem] 직업 선택 완료: {unit.unitName} → {newJob}");
            OnUnitJobSelected?.Invoke(unit, newJob);
            OnUnitPromoted?.Invoke(unit, newJob);  // 하위 호환성

            return true;
        }

        // =====================================================================
        // 하위 호환성 (Obsolete)
        // =====================================================================

        /// <summary>
        /// [Obsolete] 강화 가능 여부 - CanPromoteUnit 사용 권장
        /// </summary>
        [Obsolete("Use CanPromoteUnit instead")]
        public bool CanUpgradeUnit(UnitInstance unit) => CanPromoteUnit(unit);

        /// <summary>
        /// [Obsolete] 강화 비용 - GetPromotionCost 사용 권장
        /// </summary>
        [Obsolete("Use GetPromotionCost instead")]
        public int GetUpgradeCost(UnitInstance unit) => GetPromotionCost(unit);

        /// <summary>
        /// [Obsolete] 유닛 강화 - PromoteUnit 사용 권장
        /// </summary>
        [Obsolete("Use PromoteUnit instead")]
        public bool UpgradeUnit(UnitInstance unit, string selectedCardId) => PromoteUnit(unit, selectedCardId);

        /// <summary>
        /// [Obsolete] 강화 카드 선택지 - GetPromotionCardChoices 사용 권장
        /// </summary>
        [Obsolete("Use GetPromotionCardChoices instead")]
        public List<string> GetUpgradeCardChoices(UnitInstance unit) => GetPromotionCardChoices(unit);

        /// <summary>
        /// [Obsolete] 전직 선택지 - GetJobChoices 사용 권장
        /// </summary>
        [Obsolete("Use GetJobChoices instead")]
        public List<Job> GetPromotionChoices() => GetJobChoices();

        /// <summary>
        /// [Obsolete] 전직 실행 - SelectJob 사용 권장
        /// </summary>
        [Obsolete("Use SelectJob instead")]
        public bool PromoteUnit(UnitInstance unit, Job newJob) => SelectJob(unit, newJob);

        // =====================================================================
        // 유틸리티
        // =====================================================================

        /// <summary>
        /// ID로 유닛 찾기
        /// </summary>
        public UnitInstance GetUnitById(string unitId)
        {
            return State?.units.Find(u => u.unitId == unitId);
        }

        /// <summary>
        /// 전직 가능한 유닛 목록
        /// </summary>
        public List<UnitInstance> GetPromotableUnits()
        {
            if (State == null) return new List<UnitInstance>();

            return State.units.FindAll(u => CanPromoteUnit(u));
        }

        /// <summary>
        /// [Obsolete] 강화 가능한 유닛 목록 - GetPromotableUnits 사용 권장
        /// </summary>
        [Obsolete("Use GetPromotableUnits instead")]
        public List<UnitInstance> GetUpgradeableUnits() => GetPromotableUnits();

        /// <summary>
        /// 특정 단계 유닛 목록
        /// </summary>
        public List<UnitInstance> GetUnitsByStage(GrowthStage stage)
        {
            if (State == null) return new List<UnitInstance>();

            return State.units.FindAll(u => u.stage == stage);
        }

        /// <summary>
        /// 특정 직업 유닛 목록
        /// </summary>
        public List<UnitInstance> GetUnitsByJob(Job job)
        {
            if (State == null) return new List<UnitInstance>();

            return State.units.FindAll(u => u.job == job);
        }

        /// <summary>
        /// 총 전투력 계산 (모든 유닛)
        /// </summary>
        public int GetTotalCombatPower()
        {
            if (State == null) return 0;

            int total = 0;
            foreach (var unit in State.units)
            {
                total += unit.combatPower;
            }
            return total;
        }

        // =====================================================================
        // 디버그
        // =====================================================================

        /// <summary>
        /// 모든 유닛 상태 로그
        /// </summary>
        [ContextMenu("Log All Units")]
        public void LogAllUnits()
        {
            if (State == null || State.units.Count == 0)
            {
                Debug.Log("[UnitSystem] 유닛 없음");
                return;
            }

            Debug.Log($"=== 유닛 현황 ({State.units.Count}명) ===");

            foreach (var unit in State.units)
            {
                string promotionInfo = $"Lv.{unit.promotionLevel}";
                if (unit.CanPromote())
                {
                    int cost = GetPromotionCost(unit);
                    promotionInfo += $" (다음 전직: {cost}G)";
                }

                Debug.Log($"  {unit.unitName}: {unit.job} {unit.stage} {promotionInfo} | 전투력: {unit.combatPower} | 충성도: {unit.loyalty}");
            }

            Debug.Log($"  총 전투력: {GetTotalCombatPower()}");
        }
    }
}