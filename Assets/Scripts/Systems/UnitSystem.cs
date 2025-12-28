// =============================================================================
// UnitSystem.cs
// 유닛 관리 시스템
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
    /// - 강화
    /// - 전직
    /// </summary>
    public class UnitSystem : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static UnitSystem Instance { get; private set; }

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>유닛 생성됨</summary>
        public static event Action<UnitInstance> OnUnitCreated;

        /// <summary>유닛 사망함</summary>
        public static event Action<UnitInstance> OnUnitDied;

        /// <summary>유닛 강화됨 (유닛, 새 강화 레벨, 획득한 카드)</summary>
        public static event Action<UnitInstance, int, CardInstance> OnUnitUpgraded;

        /// <summary>유닛 전직함 (유닛, 새 직업)</summary>
        public static event Action<UnitInstance, Job> OnUnitPromoted;

        /// <summary>유닛 성장함 (유닛, 이전 단계, 새 단계)</summary>
        public static event Action<UnitInstance, GrowthStage, GrowthStage> OnUnitGrown;

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
        /// 유닛 생성
        /// </summary>
        public UnitInstance CreateUnit(string name, Job job, GrowthStage stage)
        {
            if (State == null) return null;

            var unit = UnitInstance.Create(name, job, stage);
            State.units.Add(unit);

            Debug.Log($"[UnitSystem] 유닛 생성: {name} ({job}, {stage})");
            OnUnitCreated?.Invoke(unit);

            return unit;
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
            if (!State.units.Contains(unit))
            {
                Debug.LogWarning($"[UnitSystem] 사망할 유닛을 찾지 못함: {unit.unitName}");
                return;
            }

            Debug.Log($"[UnitSystem] 유닛 사망: {unit.unitName}");

            // 종속 카드 모두 소멸
            RemoveUnitCards(unit);

            // 유닛 제거
            State.units.Remove(unit);

            // 유지비 재계산
            GameManager.Instance?.RecalculateMaintenanceCost();

            OnUnitDied?.Invoke(unit);
        }

        /// <summary>
        /// 유닛의 종속 카드 모두 제거
        /// </summary>
        private void RemoveUnitCards(UnitInstance unit)
        {
            // 모든 영역에서 해당 유닛 종속 카드 찾아서 제거
            RemoveCardsFromList(State.deck, unit.unitId);
            RemoveCardsFromList(State.hand, unit.unitId);
            RemoveCardsFromList(State.discardPile, unit.unitId);
            RemoveCardsFromList(State.playArea, unit.unitId);

            Debug.Log($"[UnitSystem] {unit.unitName}의 종속 카드 {unit.ownedCardIds.Count}장 소멸");
            unit.ownedCardIds.Clear();
        }

        /// <summary>
        /// 리스트에서 특정 유닛 소속 카드 제거
        /// </summary>
        private void RemoveCardsFromList(List<CardInstance> list, string unitId)
        {
            list.RemoveAll(card => card.ownerUnitId == unitId);
        }

        // =====================================================================
        // 강화
        // =====================================================================

        /// <summary>
        /// 강화 가능 여부 확인
        /// </summary>
        public bool CanUpgradeUnit(UnitInstance unit)
        {
            if (State == null || unit == null) return false;
            if (!unit.CanUpgrade()) return false;

            // 골드 체크
            int cost = unit.GetNextUpgradeCost();
            if (State.gold < cost) return false;

            return true;
        }

        /// <summary>
        /// 강화 비용 계산 (할인 적용)
        /// </summary>
        public int GetUpgradeCost(UnitInstance unit)
        {
            if (unit == null) return int.MaxValue;

            int baseCost = unit.GetNextUpgradeCost();
            
            // 할인 적용 (행상인 방문 등)
            if (State.upgradeDiscount > 0)
            {
                baseCost = Mathf.CeilToInt(baseCost * (100 - State.upgradeDiscount) / 100f);
            }

            return baseCost;
        }

        /// <summary>
        /// 유닛 강화 (3지선다 후 선택한 카드로)
        /// </summary>
        /// <param name="unit">강화할 유닛</param>
        /// <param name="selectedCardId">선택한 카드 ID</param>
        /// <returns>성공 여부</returns>
        public bool UpgradeUnit(UnitInstance unit, string selectedCardId)
        {
            if (!CanUpgradeUnit(unit))
            {
                Debug.LogWarning($"[UnitSystem] 강화 불가: {unit?.unitName}");
                return false;
            }

            // 비용 지불
            int cost = GetUpgradeCost(unit);
            if (!GoldSystem.Instance.TrySpendGold(cost))
            {
                return false;
            }

            // 강화 레벨 증가
            unit.upgradeLevel++;
            unit.upgradedThisTurn = true;

            // 카드 추가
            var newCard = DeckSystem.Instance.AddCardToDeck(selectedCardId, unit.unitId);

            Debug.Log($"[UnitSystem] 유닛 강화: {unit.unitName} → Lv.{unit.upgradeLevel}, 카드 획득: {selectedCardId}");
            OnUnitUpgraded?.Invoke(unit, unit.upgradeLevel, newCard);

            return true;
        }

        /// <summary>
        /// 강화 3지선다 카드 생성
        /// </summary>
        public List<string> GetUpgradeCardChoices(UnitInstance unit)
        {
            if (unit == null) return new List<string>();

            var jobDef = DataLoader.Instance?.GetJob(unit.job);
            if (jobDef == null) return new List<string>();

            var choices = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                string cardId = RollCardFromPool(jobDef);
                if (!string.IsNullOrEmpty(cardId))
                {
                    choices.Add(cardId);
                }
            }

            Debug.Log($"[UnitSystem] 강화 선택지: {string.Join(", ", choices)}");
            return choices;
        }

        /// <summary>
        /// 카드풀에서 확률에 따라 카드 선택
        /// </summary>
        private string RollCardFromPool(JobDefinition jobDef)
        {
            // 등급 확률: 기본 60%, 고급 35%, 희귀 5%
            int roll = UnityEngine.Random.Range(0, 100);
            List<string> pool;

            if (roll < GameConfig.RareCardChance) // 5%
            {
                pool = jobDef.cardPoolRare;
            }
            else if (roll < GameConfig.RareCardChance + GameConfig.AdvancedCardChance) // 40%
            {
                pool = jobDef.cardPoolAdvanced;
            }
            else // 60%
            {
                pool = jobDef.cardPoolBasic;
            }

            // 해당 풀이 비어있으면 하위 등급으로 폴백
            if (pool == null || pool.Count == 0)
            {
                pool = jobDef.cardPoolAdvanced;
            }
            if (pool == null || pool.Count == 0)
            {
                pool = jobDef.cardPoolBasic;
            }
            if (pool == null || pool.Count == 0)
            {
                Debug.LogWarning($"[UnitSystem] {jobDef.job} 카드풀이 비어있음");
                return null;
            }

            int index = UnityEngine.Random.Range(0, pool.Count);
            return pool[index];
        }

        // =====================================================================
        // 전직
        // =====================================================================

        /// <summary>
        /// 전직 선택지 생성 (유년→청년)
        /// </summary>
        public List<Job> GetPromotionChoices()
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

            Debug.Log($"[UnitSystem] 전직 선택지: {string.Join(", ", choices)}");
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
        /// 전직 실행
        /// </summary>
        public bool PromoteUnit(UnitInstance unit, Job newJob)
        {
            if (unit == null || unit.stage != GrowthStage.Child)
            {
                Debug.LogWarning("[UnitSystem] 전직 불가: 유년 유닛이 아님");
                return false;
            }

            Job oldJob = unit.job;
            unit.job = newJob;
            unit.stage = GrowthStage.Young;
            unit.stageRemainingTurns = UnitInstance.GetStageDuration(GrowthStage.Young);

            // 전직 시 기본 카드 1장 획득
            var jobDef = DataLoader.Instance?.GetJob(newJob);
            if (jobDef?.cardPoolBasic?.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, jobDef.cardPoolBasic.Count);
                string cardId = jobDef.cardPoolBasic[index];
                DeckSystem.Instance?.AddCardToDeck(cardId, unit.unitId);
                Debug.Log($"[UnitSystem] 전직 카드 획득: {cardId}");
            }

            Debug.Log($"[UnitSystem] 전직 완료: {unit.unitName} → {newJob}");
            OnUnitPromoted?.Invoke(unit, newJob);

            return true;
        }

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
        /// 강화 가능한 유닛 목록
        /// </summary>
        public List<UnitInstance> GetUpgradeableUnits()
        {
            if (State == null) return new List<UnitInstance>();

            return State.units.FindAll(u => CanUpgradeUnit(u));
        }

        /// <summary>
        /// 특정 단계 유닛 목록
        /// </summary>
        public List<UnitInstance> GetUnitsByStage(GrowthStage stage)
        {
            if (State == null) return new List<UnitInstance>();

            return State.units.FindAll(u => u.stage == stage);
        }
    }
}
