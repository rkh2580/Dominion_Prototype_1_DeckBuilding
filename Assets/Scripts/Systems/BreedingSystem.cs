// =============================================================================
// BreedingSystem.cs
// 교배/출산 시스템
// =============================================================================
// [R4 신규] 2026-01-03
// - 매 턴 교배 조건 체크
// - 임신 확률 계산 (fertilityCounter 기반)
// - 출산 처리 (3턴 후)
// - 유전 시스템 (부모 카드 복사)
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 교배/출산 시스템 (싱글톤)
    /// - 매 턴 교배 조건 체크
    /// - 임신 확률 계산
    /// - 출산 및 유전
    /// </summary>
    public class BreedingSystem : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static BreedingSystem Instance { get; private set; }

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>교배 조건 충족 (집) - UI 표시용</summary>
        public static event Action<HouseInstance> OnBreedingConditionMet;

        /// <summary>임신 시도 (집, 성공 여부, 확률)</summary>
        public static event Action<HouseInstance, bool, int> OnBreedingAttempted;

        /// <summary>출산 완료 (집, 부모A, 부모B, 자식)</summary>
        public static event Action<HouseInstance, UnitInstance, UnitInstance, UnitInstance> OnChildBorn;

        /// <summary>유전 카드 획득 (자식, 카드)</summary>
        public static event Action<UnitInstance, CardInstance> OnInheritedCard;

        // =====================================================================
        // 상태 접근
        // =====================================================================

        private GameState State => GameManager.Instance.State;

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

            Debug.Log("[BreedingSystem] 초기화 완료");
        }

        // =====================================================================
        // 메인 처리 (TurnManager에서 호출)
        // =====================================================================

        /// <summary>
        /// 매 턴 시작 시 교배/임신 처리
        /// </summary>
        public void ProcessBreeding()
        {
            if (State == null || State.houses == null) return;

            Debug.Log("[BreedingSystem] === 교배/임신 처리 시작 ===");

            foreach (var house in State.houses)
            {
                // 1. 임신 중인 집: 임신 턴 진행
                if (house.isPregnant)
                {
                    ProcessPregnancy(house);
                }
                // 2. 임신 중 아닌 집: 교배 조건 체크
                else
                {
                    ProcessBreedingCheck(house);
                }
            }

            Debug.Log("[BreedingSystem] === 교배/임신 처리 완료 ===");
        }

        /// <summary>
        /// 임신 중인 집의 임신 턴 진행
        /// [D3] 부모 노년 체크 추가 - 임신 강제 종료
        /// </summary>
        private void ProcessPregnancy(HouseInstance house)
        {
            // [D3] 부모 노년 체크 - 임신 강제 종료
            var parentA = UnitSystem.Instance?.GetUnitById(house.adultSlotA);
            var parentB = UnitSystem.Instance?.GetUnitById(house.adultSlotB);

            if (parentA?.stage == GrowthStage.Old || parentB?.stage == GrowthStage.Old)
            {
                Debug.Log($"[BreedingSystem] {house.houseName} 임신 취소 - 부모 노년화");
                HouseSystem.Instance?.CancelPregnancy(house);
                return;
            }

            house.pregnancyTurns++;
            Debug.Log($"[BreedingSystem] {house.houseName} 임신 {house.pregnancyTurns}/{GameConfig.PregnancyDuration}턴");

            // 출산 조건 체크
            if (house.pregnancyTurns >= GameConfig.PregnancyDuration)
            {
                ProcessBirth(house);
            }
        }

        /// <summary>
        /// 출산 처리
        /// </summary>
        private void ProcessBirth(HouseInstance house)
        {
            // 부모 확인
            var parentA = UnitSystem.Instance?.GetUnitById(house.adultSlotA);
            var parentB = UnitSystem.Instance?.GetUnitById(house.adultSlotB);

            if (parentA == null || parentB == null)
            {
                Debug.LogWarning($"[BreedingSystem] {house.houseName} 출산 실패 - 부모 없음");
                HouseSystem.Instance?.CancelPregnancy(house);
                return;
            }

            // 유년 슬롯 확인
            if (!house.HasEmptyChildSlot())
            {
                Debug.LogWarning($"[BreedingSystem] {house.houseName} 출산 실패 - 유년 슬롯 사용 중");
                // 임신 유지 (다음 턴에 재시도)
                return;
            }

            // HouseSystem을 통해 출산
            var child = HouseSystem.Instance?.ProcessBirth(house);
            if (child == null)
            {
                Debug.LogError($"[BreedingSystem] {house.houseName} 출산 실패 - 유닛 생성 오류");
                return;
            }

            Debug.Log($"[BreedingSystem] 출산 완료: {house.houseName} → {child.unitName}");

            // 유전 처리
            ProcessInheritance(child, parentA, parentB);

            // 이벤트 발생
            OnChildBorn?.Invoke(house, parentA, parentB, child);
        }

        // =====================================================================
        // 교배 조건 체크
        // =====================================================================

        /// <summary>
        /// 교배 조건 체크 및 임신 시도
        /// </summary>
        private void ProcessBreedingCheck(HouseInstance house)
        {
            // 교배 조건 확인
            if (!CanBreed(house))
            {
                // 조건 미충족 시 카운터 리셋
                if (house.fertilityCounter > 0)
                {
                    Debug.Log($"[BreedingSystem] {house.houseName} 교배 조건 미충족 - 카운터 리셋");
                    house.fertilityCounter = 0;
                }
                return;
            }

            // 교배 조건 충족
            OnBreedingConditionMet?.Invoke(house);

            // 교배 카운터 증가
            house.fertilityCounter++;

            // 임신 확률 계산
            int chanceIndex = Mathf.Min(house.fertilityCounter - 1, GameConfig.FertilityChance.Length - 1);
            int chance = GameConfig.FertilityChance[chanceIndex];

            Debug.Log($"[BreedingSystem] {house.houseName} 교배 시도 - 카운터: {house.fertilityCounter}, 확률: {chance}%");

            // 임신 시도
            int roll = UnityEngine.Random.Range(0, 100);
            bool success = roll < chance;

            Debug.Log($"[BreedingSystem] {house.houseName} 주사위: {roll} < {chance} → {(success ? "임신 성공!" : "임신 실패")}");

            OnBreedingAttempted?.Invoke(house, success, chance);

            if (success)
            {
                // 임신 시작
                HouseSystem.Instance?.StartPregnancy(house);
            }
        }

        /// <summary>
        /// 교배 가능 조건 확인
        /// </summary>
        public bool CanBreed(HouseInstance house)
        {
            if (house == null) return false;

            // 1. 어른 슬롯 2개 모두 채워져야 함
            if (!house.HasBothAdults())
            {
                return false;
            }

            // 2. 유년 슬롯 비어있어야 함
            if (!house.HasEmptyChildSlot())
            {
                return false;
            }

            // 3. 두 어른 모두 교배 가능해야 함
            var adultA = UnitSystem.Instance?.GetUnitById(house.adultSlotA);
            var adultB = UnitSystem.Instance?.GetUnitById(house.adultSlotB);

            if (adultA == null || adultB == null)
            {
                return false;
            }

            if (!adultA.CanBreed() || !adultB.CanBreed())
            {
                return false;
            }

            return true;
        }

        // =====================================================================
        // 유전 시스템
        // =====================================================================

        /// <summary>
        /// 부모 카드를 자식에게 복사
        /// </summary>
        private void ProcessInheritance(UnitInstance child, UnitInstance parentA, UnitInstance parentB)
        {
            Debug.Log($"[BreedingSystem] 유전 처리 시작: {child.unitName}");

            // 부모 A의 종속 카드 복사
            CopyCardsFromParent(child, parentA);

            // 부모 B의 종속 카드 복사
            CopyCardsFromParent(child, parentB);

            // 유지비 재계산
            GameManager.Instance?.RecalculateMaintenanceCost();

            Debug.Log($"[BreedingSystem] 유전 완료: {child.unitName} ({child.ownedCardIds.Count}장 획득)");
        }

        /// <summary>
        /// 한 부모의 종속 카드를 자식에게 복사
        /// </summary>
        private void CopyCardsFromParent(UnitInstance child, UnitInstance parent)
        {
            if (parent.ownedCardIds == null || parent.ownedCardIds.Count == 0)
            {
                Debug.Log($"[BreedingSystem] {parent.unitName}의 종속 카드 없음");
                return;
            }

            foreach (string cardInstanceId in parent.ownedCardIds)
            {
                // 카드 인스턴스 찾기
                var originalCard = FindCardInstance(cardInstanceId);
                if (originalCard == null)
                {
                    Debug.LogWarning($"[BreedingSystem] 카드 인스턴스를 찾을 수 없음: {cardInstanceId}");
                    continue;
                }

                // 새 카드 복사본 생성
                var newCard = DeckSystem.Instance?.AddCardToDeck(originalCard.cardDataId, child.unitId);
                if (newCard != null)
                {
                    Debug.Log($"[BreedingSystem] 유전 카드: {originalCard.cardDataId} ({parent.unitName} → {child.unitName})");
                    OnInheritedCard?.Invoke(child, newCard);
                }
            }
        }

        /// <summary>
        /// 모든 영역에서 카드 인스턴스 찾기
        /// </summary>
        private CardInstance FindCardInstance(string cardInstanceId)
        {
            // 덱에서 찾기
            var card = State.deck.Find(c => c.instanceId == cardInstanceId);
            if (card != null) return card;

            // 손패에서 찾기
            card = State.hand.Find(c => c.instanceId == cardInstanceId);
            if (card != null) return card;

            // 버림더미에서 찾기
            card = State.discardPile.Find(c => c.instanceId == cardInstanceId);
            if (card != null) return card;

            // 플레이 영역에서 찾기
            card = State.playArea.Find(c => c.instanceId == cardInstanceId);
            if (card != null) return card;

            return null;
        }

        // =====================================================================
        // 조회 유틸리티
        // =====================================================================

        /// <summary>
        /// 임신 중인 집 목록
        /// </summary>
        public List<HouseInstance> GetPregnantHouses()
        {
            if (State == null) return new List<HouseInstance>();
            return State.houses.FindAll(h => h.isPregnant);
        }

        /// <summary>
        /// 교배 가능한 집 목록
        /// </summary>
        public List<HouseInstance> GetBreedableHouses()
        {
            if (State == null) return new List<HouseInstance>();
            return State.houses.FindAll(h => CanBreed(h));
        }

        /// <summary>
        /// 다음 출산까지 남은 턴 (집 지정)
        /// </summary>
        public int GetRemainingPregnancyTurns(HouseInstance house)
        {
            if (house == null || !house.isPregnant) return -1;
            return GameConfig.PregnancyDuration - house.pregnancyTurns;
        }

        // =====================================================================
        // 디버그
        // =====================================================================

        /// <summary>
        /// 교배/임신 상태 로그
        /// </summary>
        [ContextMenu("Log Breeding Status")]
        public void LogBreedingStatus()
        {
            if (State == null || State.houses.Count == 0)
            {
                Debug.Log("[BreedingSystem] 집 없음");
                return;
            }

            Debug.Log("=== 교배/임신 상태 ===");

            foreach (var house in State.houses)
            {
                string status;
                if (house.isPregnant)
                {
                    int remaining = GetRemainingPregnancyTurns(house);
                    status = $"임신 중 ({remaining}턴 후 출산)";
                }
                else if (CanBreed(house))
                {
                    int chanceIndex = Mathf.Min(house.fertilityCounter, GameConfig.FertilityChance.Length - 1);
                    int nextChance = GameConfig.FertilityChance[chanceIndex];
                    status = $"교배 가능 (카운터: {house.fertilityCounter}, 다음 확률: {nextChance}%)";
                }
                else
                {
                    status = "교배 불가";
                }

                Debug.Log($"  {house.houseName}: {status}");
            }
        }

        /// <summary>
        /// 디버그: 강제 임신
        /// </summary>
        [ContextMenu("Debug: Force Pregnancy (First Breedable)")]
        public void DebugForcePregnancy()
        {
            var breedable = GetBreedableHouses();
            if (breedable.Count == 0)
            {
                Debug.Log("[BreedingSystem] 교배 가능한 집 없음");
                return;
            }

            var house = breedable[0];
            HouseSystem.Instance?.StartPregnancy(house);
            Debug.Log($"[BreedingSystem] 강제 임신: {house.houseName}");
        }

        /// <summary>
        /// 디버그: 강제 출산
        /// </summary>
        [ContextMenu("Debug: Force Birth (First Pregnant)")]
        public void DebugForceBirth()
        {
            var pregnant = GetPregnantHouses();
            if (pregnant.Count == 0)
            {
                Debug.Log("[BreedingSystem] 임신 중인 집 없음");
                return;
            }

            var house = pregnant[0];
            house.pregnancyTurns = GameConfig.PregnancyDuration; // 즉시 출산 조건 충족
            ProcessBirth(house);
        }
    }
}