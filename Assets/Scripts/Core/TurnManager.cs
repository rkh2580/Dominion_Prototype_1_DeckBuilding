// =============================================================================
// TurnManager.cs
// 턴 진행 및 페이즈 전환 관리
// =============================================================================
// [R2 리팩토링] 2026-01-03
// - 자동 출생 로직 주석 처리 (교배 시스템으로 대체 예정)
// - 강제 사망 로직 삭제 (자연사만 사용)
// - 약탈/최종전투 스텁 추가
// =============================================================================

using System;
using System.Linq;
using UnityEngine;
using DeckBuildingEconomy.Data;
using UnityEngine.EventSystems;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 턴 매니저 (싱글톤)
    /// - 턴 시작/종료
    /// - 페이즈 전환
    /// - 액션 관리
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static TurnManager Instance { get; private set; }

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>턴 시작됨 (턴 번호)</summary>
        public static event Action<int> OnTurnStarted;

        /// <summary>턴 종료됨 (턴 번호)</summary>
        public static event Action<int> OnTurnEnded;

        /// <summary>페이즈 변경됨</summary>
        public static event Action<GamePhase> OnPhaseChanged;

        /// <summary>액션 수 변경됨 (이전, 현재)</summary>
        public static event Action<int, int> OnActionsChanged;

        /// <summary>예정 이벤트 발생 (이벤트 종류, 데이터)</summary>
        public static event Action<ScheduledEventType, object> OnScheduledEvent;

        // =====================================================================
        // 상태 접근 (편의용)
        // =====================================================================

        private GameState State => GameManager.Instance.State;

        public int CurrentTurn => State?.currentTurn ?? 0;
        public GamePhase CurrentPhase => State?.currentPhase ?? GamePhase.TurnStart;
        public int RemainingActions => State?.remainingActions ?? 0;

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

            Debug.Log("[TurnManager] 초기화 완료");
        }

        // =====================================================================
        // 게임 시작
        // =====================================================================

        /// <summary>
        /// 게임 시작 (GameManager에서 호출)
        /// </summary>
        public void StartGame()
        {
            Debug.Log("[TurnManager] 게임 시작");
            StartTurn();
        }

        // =====================================================================
        // 턴 흐름
        // =====================================================================

        /// <summary>
        /// 턴 시작
        /// </summary>
        public void StartTurn()
        {
            if (!GameManager.Instance.IsGameRunning) return;

            // 1. 턴 카운터 증가
            State.currentTurn++;
            Debug.Log($"[TurnManager] ===== 턴 {State.currentTurn} 시작 =====");

            // 2. 턴 상태 초기화
            State.ResetTurnState();

            // 3. 페이즈: 턴 시작 처리
            SetPhase(GamePhase.TurnStart);

            // [R6] 4. 가출 처리 (충성도 0 이하 유닛)
            LoyaltySystem.Instance?.ProcessDesertions();

            // 5. 유닛 성장 체크
            ProcessUnitGrowth();

            // 6. [R4 추가] 교배/출산 처리
            BreedingSystem.Instance?.ProcessBreeding();

            // 7. UI 갱신 이벤트 (모든 상태 변경 후)
            OnTurnStarted?.Invoke(State.currentTurn);  // ← 이동!

            // 8. 예정 이벤트 처리
            ProcessScheduledEvents();

            // 9. 랜덤 이벤트 처리 (1턴 제외)
            if (State.currentTurn > 1)
            {
                ProcessRandomEvents();
            }

            // 10. 덱 페이즈 시작
            StartDeckPhase();
        }

        /// <summary>
        /// 턴 종료
        /// </summary>
        public void EndTurn()
        {
            if (!GameManager.Instance.IsGameRunning) return;
            if (State.currentPhase != GamePhase.Purchase)
            {
                Debug.LogWarning("[TurnManager] 구매 페이즈가 아닌 상태에서 턴 종료 시도");
                return;
            }

            Debug.Log($"[TurnManager] 턴 {State.currentTurn} 종료 처리");
            SetPhase(GamePhase.TurnEnd);

            // 1. 손패 초과 처리 (10장 초과 시)
            if (State.hand.Count > GameConfig.MaxHandSize)
            {
                int discardCount = State.hand.Count - GameConfig.MaxHandSize;
                Debug.Log($"[TurnManager] 손패 초과 - {discardCount}장 버려야 함");
                AutoDiscardExcessCards(discardCount);
            }

            ContinueTurnEnd();
        }

        /// <summary>
        /// 턴 종료 계속 (손패 초과 처리 후)
        /// </summary>
        public void ContinueTurnEnd()
        {
            // 2. 유지비 차감
            GoldSystem.Instance?.SubtractGold(State.maintenanceCost);
            Debug.Log($"[TurnManager] 유지비 {State.maintenanceCost} 차감");

            // 3. 오염 카드 효과 처리
            ProcessPollutionEffects();

            // 4. 카드 정리 (손패 + 플레이 영역 → 버림더미)
            DeckSystem.Instance?.CleanupCards();

            // 5. 지속 효과 처리
            ProcessPersistentEffects();

            // 6. 노년 유닛 사망 확률 체크
            ProcessOldAgeDeath();

            // [R6] 7. 충성도 처리 (적자 시 감소, 흑자 시 회복)
            LoyaltySystem.Instance?.ProcessLoyalty();

            // 8. 승패 체크
            GameManager.Instance.CheckWinLoseCondition();

            // 9. 턴 종료 이벤트
            OnTurnEnded?.Invoke(State.currentTurn);

            // 10. 다음 턴 시작
            if (GameManager.Instance.IsGameRunning)
            {
                StartTurn();
            }
        }

        // =====================================================================
        // 페이즈 관리
        // =====================================================================

        /// <summary>
        /// 덱 페이즈 시작
        /// </summary>
        public void StartDeckPhase()
        {
            SetPhase(GamePhase.Deck);

            // 액션 초기화
            SetActions(GameConfig.StartingActions);

            // 5장 드로우
            DeckSystem.Instance?.DrawCards(GameConfig.HandSize);

            Debug.Log("[TurnManager] 덱 페이즈 시작");
        }

        /// <summary>
        /// 덱 페이즈 종료 → 구매 페이즈 시작
        /// </summary>
        public void EndDeckPhase()
        {
            if (State.currentPhase != GamePhase.Deck)
            {
                Debug.LogWarning("[TurnManager] 덱 페이즈가 아닌 상태에서 종료 시도");
                return;
            }

            // 재화 카드 정산
            int treasureGold = DeckSystem.Instance?.CalculateTreasureGold() ?? 0;
            GoldSystem.Instance?.AddGold(treasureGold);
            Debug.Log($"[TurnManager] 재화 정산: +{treasureGold} 골드");

            // 구매 페이즈 시작
            SetPhase(GamePhase.Purchase);
            Debug.Log("[TurnManager] 구매 페이즈 시작");
        }

        /// <summary>
        /// 페이즈 설정
        /// </summary>
        public void SetPhase(GamePhase phase)
        {
            if (State.currentPhase == phase) return;

            State.currentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
            Debug.Log($"[TurnManager] 페이즈 변경: {phase}");
        }

        // =====================================================================
        // 액션 관리
        // =====================================================================

        /// <summary>
        /// 액션 설정
        /// </summary>
        public void SetActions(int count)
        {
            int prev = State.remainingActions;
            State.remainingActions = Mathf.Max(0, count);

            if (prev != State.remainingActions)
            {
                OnActionsChanged?.Invoke(prev, State.remainingActions);
            }
        }

        /// <summary>
        /// 액션 추가
        /// </summary>
        public void AddActions(int amount)
        {
            SetActions(State.remainingActions + amount);
        }

        /// <summary>
        /// 액션 소모 (1개)
        /// </summary>
        public bool UseAction()
        {
            if (State.remainingActions <= 0)
            {
                Debug.LogWarning("[TurnManager] 액션이 부족합니다");
                return false;
            }

            SetActions(State.remainingActions - 1);
            return true;
        }

        /// <summary>
        /// 액션 소모 (UseAction 별칭 - 하위 호환성)
        /// </summary>
        public bool ConsumeAction()
        {
            return UseAction();
        }

        /// <summary>
        /// 액션 있는지 확인
        /// </summary>
        public bool HasActions()
        {
            return State.remainingActions > 0;
        }

        // =====================================================================
        // 유닛 성장
        // =====================================================================

        /// <summary>
        /// 유닛 성장 처리
        /// </summary>
        private void ProcessUnitGrowth()
        {
            foreach (var unit in State.units.ToArray())
            {
                // 노년은 oldAgeTurns 증가
                if (unit.stage == GrowthStage.Old)
                {
                    unit.oldAgeTurns++;
                    Debug.Log($"[TurnManager] {unit.unitName} 노년 {unit.oldAgeTurns}턴째");
                    continue;
                }

                // 단계 잔여 턴 감소
                unit.stageRemainingTurns--;

                // 다음 단계로 진행
                if (unit.stageRemainingTurns <= 0)
                {
                    AdvanceUnitStage(unit);
                }
            }
        }

        /// <summary>
        /// 유닛 성장 단계 진행
        /// </summary>
        private void AdvanceUnitStage(UnitInstance unit)
        {
            GrowthStage oldStage = unit.stage;

            switch (unit.stage)
            {
                case GrowthStage.Child:
                    unit.stage = GrowthStage.Young;
                    unit.stageRemainingTurns = UnitInstance.GetStageDuration(GrowthStage.Young);
                    // [R2 변경] 충성도 업데이트
                    unit.loyalty = GameConfig.AdultLoyalty;

                    // [R3 추가] 유년 슬롯 → 어른 슬롯 이동
                    MoveChildToAdultSlot(unit);

                    Debug.Log($"[TurnManager] {unit.unitName} 성장: 유년 → 청년 (직업 선택 필요)");
                    // 직업 선택 UI 표시를 위한 이벤트 발생
                    UnitSystem.RaiseNeedsJobSelection(unit);
                    break;

                case GrowthStage.Young:
                    unit.stage = GrowthStage.Middle;
                    unit.stageRemainingTurns = UnitInstance.GetStageDuration(GrowthStage.Middle);
                    Debug.Log($"[TurnManager] {unit.unitName} 성장: 청년 → 중년");
                    break;

                case GrowthStage.Middle:
                    unit.stage = GrowthStage.Old;
                    unit.stageRemainingTurns = UnitInstance.GetStageDuration(GrowthStage.Old);
                    unit.oldAgeTurns = 0;
                    Debug.Log($"[TurnManager] {unit.unitName} 성장: 중년 → 노년");
                    break;

                case GrowthStage.Old:
                    // 노년은 시간 경과로 전환되지 않음 (사망 확률로 처리)
                    unit.stageRemainingTurns = 1;
                    break;
            }
        }

        // =====================================================================
        // 예정 이벤트 처리
        // =====================================================================

        /// <summary>
        /// 예정 이벤트 처리
        /// [R2 변경] 자동 출생/강제 사망 삭제, 약탈/최종전투 추가
        /// </summary>
        private void ProcessScheduledEvents()
        {
            int turn = State.currentTurn;

            // ================================================================
            // [R2 삭제] 자동 유닛 출생 → 교배 시스템으로 대체 (R4에서 구현)
            // ================================================================
            // 기존 코드 삭제됨

            // ================================================================
            // 검증 턴 (10, 20, 30, 40, 50턴)
            // ================================================================
            if (Array.IndexOf(GameConfig.ValidationTurns, turn) >= 0)
            {
                Debug.Log($"[TurnManager] 예정 이벤트: 검증 턴 (턴 {turn})");
                OnScheduledEvent?.Invoke(ScheduledEventType.Validation, turn);
                GameManager.Instance.CheckValidation(turn);
            }

            // ================================================================
            // [R2 삭제] 강제 사망 → 자연사만 사용
            // ================================================================
            // 기존 코드 삭제됨

            // ================================================================
            // [R2 추가] 약탈 (8, 16, 24, 32, 40, 48, 56턴) - R7에서 구현
            // ================================================================
            if (Array.IndexOf(GameConfig.RaidTurns, turn) >= 0)
            {
                Debug.Log($"[TurnManager] 예정 이벤트: 약탈 (턴 {turn})");
                OnScheduledEvent?.Invoke(ScheduledEventType.Raid, turn);
                // [R7] RaidSystem으로 약탈 처리
                RaidSystem.Instance?.ProcessRaid(turn);
            }

            // ================================================================
            // [R2 추가] 최종 전투 (60턴) - R7에서 구현
            // ================================================================
            if (turn == GameConfig.FinalBattleTurn)
            {
                Debug.Log($"[TurnManager] 예정 이벤트: 최종 전투 (턴 {turn})");
                OnScheduledEvent?.Invoke(ScheduledEventType.FinalBattle, turn);
                // [R7] RaidSystem으로 최종 전투 처리
                bool victory = RaidSystem.Instance?.ProcessFinalBattle() ?? false;
                if (victory)
                {
                    GameManager.Instance.EndGame(GameEndState.Victory);
                }
                else
                {
                    GameManager.Instance.EndGame(GameEndState.DefeatBattle);
                }
            }
        }

        // =====================================================================
        // [R7 완료] 스텁 메서드 제거됨 - RaidSystem으로 대체
        // =====================================================================

        // =====================================================================
        // 랜덤 이벤트
        // =====================================================================

        /// <summary>
        /// 랜덤 이벤트 처리
        /// </summary>
        private void ProcessRandomEvents()
        {
            EventSystem.Instance?.ProcessRandomEvent(State.currentTurn);
        }

        // =====================================================================
        // 오염/지속 효과
        // =====================================================================

        /// <summary>
        /// 오염 카드 효과 처리
        /// </summary>
        private void ProcessPollutionEffects()
        {
            if (State.pollutionIgnored)
            {
                Debug.Log("[TurnManager] 오염 효과 무시됨 (방벽 등)");
                return;
            }

            foreach (var card in State.hand)
            {
                var cardData = DataLoader.Instance?.GetCard(card.cardDataId);
                if (cardData?.cardType == CardType.Pollution)
                {
                    if (cardData.pollutionType == PollutionType.Curse)
                    {
                        GoldSystem.Instance?.SubtractGold(2);
                        Debug.Log("[TurnManager] 저주 효과: -2 골드");
                    }
                }
            }
        }

        /// <summary>
        /// 지속 효과 처리
        /// </summary>
        private void ProcessPersistentEffects()
        {
            for (int i = State.activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = State.activeEffects[i];

                // 효과 적용
                switch (effect.type)
                {
                    case PersistentEffectType.DelayedGold:
                        // N턴 후 발동 - remainingTurns가 1일 때만 발동
                        if (effect.remainingTurns == 1)
                        {
                            GoldSystem.Instance?.AddGold(effect.value);
                            Debug.Log($"[TurnManager] 지연 효과 발동: +{effect.value} 골드");
                        }
                        else
                        {
                            Debug.Log($"[TurnManager] 지연 효과 대기 중: {effect.remainingTurns - 1}턴 후 +{effect.value} 골드");
                        }
                        break;

                    case PersistentEffectType.GoldPerTurn:
                        // 매 턴 발동
                        GoldSystem.Instance?.AddGold(effect.value);
                        Debug.Log($"[TurnManager] 지속 효과: +{effect.value} 골드 ({effect.remainingTurns - 1}턴 남음)");
                        break;

                    case PersistentEffectType.MaintenanceIncrease:
                        int extraMaintenance = State.maintenanceCost * effect.value / 100;
                        GoldSystem.Instance?.SubtractGold(extraMaintenance);
                        Debug.Log($"[TurnManager] 지속 효과 (흉년): 유지비 {State.maintenanceCost}의 {effect.value}% = -{extraMaintenance} 추가 차감");
                        break;
                }

                // 턴 감소
                effect.remainingTurns--;

                // 만료 시 제거
                if (effect.remainingTurns <= 0)
                {
                    State.activeEffects.RemoveAt(i);
                    Debug.Log($"[TurnManager] 지속 효과 만료");
                }
            }
        }

        /// <summary>
        /// 노년 유닛 사망 확률 체크
        /// </summary>
        private void ProcessOldAgeDeath()
        {
            foreach (var unit in State.units.ToArray())
            {
                if (unit.stage != GrowthStage.Old) continue;

                int deathChance = unit.GetDeathChance();
                if (deathChance <= 0) continue;

                int roll = UnityEngine.Random.Range(0, 100);
                if (roll < deathChance)
                {
                    Debug.Log($"[TurnManager] {unit.unitName} 자연사 (확률 {deathChance}%, 굴림 {roll})");
                    UnitSystem.Instance?.KillUnit(unit);
                }
                else
                {
                    Debug.Log($"[TurnManager] {unit.unitName} 생존 (확률 {deathChance}%, 굴림 {roll})");
                }
            }
        }

        // =====================================================================
        // 기타
        // =====================================================================

        /// <summary>
        /// 손패 초과 시 자동 버림 (프로토타입용)
        /// </summary>
        private void AutoDiscardExcessCards(int count)
        {
            for (int i = 0; i < count && State.hand.Count > 0; i++)
            {
                var card = State.hand[0];
                State.hand.RemoveAt(0);
                State.discardPile.Add(card);
                Debug.Log($"[TurnManager] 자동 버림: {card.cardDataId}");
            }
        }

        /// <summary>
        /// [R3 추가] 유년→청년 성장 시 슬롯 이동
        /// 유년 슬롯에서 빈 어른 슬롯으로 자동 이동
        /// </summary>
        private void MoveChildToAdultSlot(UnitInstance unit)
        {
            if (HouseSystem.Instance == null) return;
            if (string.IsNullOrEmpty(unit.houseId)) return;

            var house = HouseSystem.Instance.GetHouseById(unit.houseId);
            if (house == null) return;

            // 현재 유년 슬롯에 있는지 확인
            if (house.childSlot != unit.unitId)
            {
                Debug.LogWarning($"[TurnManager] {unit.unitName}이 유년 슬롯에 없음");
                return;
            }

            // 빈 어른 슬롯 찾기
            HouseSlotType? targetSlot = null;
            if (string.IsNullOrEmpty(house.adultSlotA))
            {
                targetSlot = HouseSlotType.AdultA;
            }
            else if (string.IsNullOrEmpty(house.adultSlotB))
            {
                targetSlot = HouseSlotType.AdultB;
            }

            if (targetSlot == null)
            {
                // 같은 집에 빈 어른 슬롯이 없으면 다른 집 찾기
                var availableHouses = HouseSystem.Instance.GetHousesWithEmptyAdultSlot();
                foreach (var otherHouse in availableHouses)
                {
                    if (string.IsNullOrEmpty(otherHouse.adultSlotA))
                    {
                        // 유년 슬롯 비우기
                        house.childSlot = null;
                        // 새 집으로 이동
                        HouseSystem.Instance.PlaceUnit(unit, otherHouse, HouseSlotType.AdultA);
                        Debug.Log($"[TurnManager] {unit.unitName} 다른 집으로 이동: {otherHouse.houseName}");
                        return;
                    }
                    else if (string.IsNullOrEmpty(otherHouse.adultSlotB))
                    {
                        house.childSlot = null;
                        HouseSystem.Instance.PlaceUnit(unit, otherHouse, HouseSlotType.AdultB);
                        Debug.Log($"[TurnManager] {unit.unitName} 다른 집으로 이동: {otherHouse.houseName}");
                        return;
                    }
                }

                // 빈 슬롯이 전혀 없으면 가출 (유닛 소멸)
                Debug.Log($"[TurnManager] ⚠️ {unit.unitName} 가출! (빈 어른 슬롯 없음)");
                house.childSlot = null;
                unit.houseId = null;
                UnitSystem.Instance.KillUnit(unit);
                return;
            }

            // 같은 집 내에서 이동
            house.childSlot = null;
            HouseSystem.Instance.PlaceUnit(unit, house, targetSlot.Value);
            Debug.Log($"[TurnManager] {unit.unitName} 같은 집 내 이동: 유년→{targetSlot}");
        }
    }
}