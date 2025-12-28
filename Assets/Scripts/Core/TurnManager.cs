// =============================================================================
// TurnManager.cs
// 턴 진행 및 페이즈 전환 관리
// =============================================================================

using System;
using System.Linq;
using UnityEngine;
using DeckBuildingEconomy.Data;

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

            // 4. 유닛 성장 체크
            ProcessUnitGrowth();

            // 5. 예정 이벤트 처리
            ProcessScheduledEvents();

            // 6. 랜덤 이벤트 처리 (1턴 제외)
            if (State.currentTurn > 1)
            {
                ProcessRandomEvents();
            }

            // 7. 덱 페이즈 시작
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
            // → UI에서 선택 후 ContinueTurnEnd() 호출
            if (State.hand.Count > GameConfig.MaxHandSize)
            {
                int discardCount = State.hand.Count - GameConfig.MaxHandSize;
                Debug.Log($"[TurnManager] 손패 초과 - {discardCount}장 버려야 함");
                // TODO: UI에서 선택 대기, 선택 완료 후 ContinueTurnEnd() 호출
                // 프로토타입에서는 자동 처리 (맨 앞부터 버림)
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

            // 7. 승패 체크
            GameManager.Instance.CheckWinLoseCondition();

            // 8. 턴 종료 이벤트
            OnTurnEnded?.Invoke(State.currentTurn);

            // 9. 다음 턴 시작
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
            StartPurchasePhase();
        }

        /// <summary>
        /// 구매 페이즈 시작
        /// </summary>
        public void StartPurchasePhase()
        {
            SetPhase(GamePhase.Purchase);
            Debug.Log("[TurnManager] 구매 페이즈 시작");
        }

        /// <summary>
        /// 페이즈 설정
        /// </summary>
        private void SetPhase(GamePhase phase)
        {
            State.currentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        // =====================================================================
        // 액션 관리
        // =====================================================================

        /// <summary>
        /// 액션 설정
        /// </summary>
        public void SetActions(int amount)
        {
            int old = State.remainingActions;
            State.remainingActions = amount;
            OnActionsChanged?.Invoke(old, amount);
        }

        /// <summary>
        /// 액션 추가
        /// </summary>
        public void AddActions(int amount)
        {
            int old = State.remainingActions;
            State.remainingActions += amount;
            OnActionsChanged?.Invoke(old, State.remainingActions);
            Debug.Log($"[TurnManager] 액션 +{amount} (현재: {State.remainingActions})");
        }

        /// <summary>
        /// 액션 소모 (카드 플레이 시)
        /// </summary>
        public bool ConsumeAction()
        {
            if (State.remainingActions <= 0)
            {
                Debug.LogWarning("[TurnManager] 액션 부족");
                return false;
            }

            int old = State.remainingActions;
            State.remainingActions--;
            OnActionsChanged?.Invoke(old, State.remainingActions);
            Debug.Log($"[TurnManager] 액션 소모 (남은 액션: {State.remainingActions})");

            // 참고: 액션 0이어도 자동 종료하지 않음
            // 플레이어가 "플레이 종료" 버튼으로 명시적으로 EndDeckPhase() 호출해야 함
            // (재화 카드만 남은 상태에서 정산 타이밍을 플레이어가 선택)

            return true;
        }

        // =====================================================================
        // 이벤트 처리
        // =====================================================================

        /// <summary>
        /// 유닛 성장 처리
        /// </summary>
        private void ProcessUnitGrowth()
        {
            foreach (var unit in State.units.ToArray()) // ToArray: 순회 중 변경 대비
            {
                unit.stageRemainingTurns--;

                // 단계 전환 체크
                if (unit.stageRemainingTurns <= 0)
                {
                    AdvanceUnitStage(unit);
                }

                // 노년 경과 턴 증가
                if (unit.stage == GrowthStage.Old)
                {
                    unit.oldAgeTurns++;
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
                    Debug.Log($"[TurnManager] {unit.unitName} 성장: 유년 → 청년 (전직 필요)");
                    // TODO: 전직 UI 표시
                    OnScheduledEvent?.Invoke(ScheduledEventType.Birth, unit); // 전직 이벤트로 활용
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
                    unit.stageRemainingTurns = 1; // 계속 1로 유지
                    break;
            }
        }

        /// <summary>
        /// 예정 이벤트 처리
        /// </summary>
        private void ProcessScheduledEvents()
        {
            int turn = State.currentTurn;

            // 유닛 출생 (6, 12, 18, 24, 30, 36, 42턴)
            if (Array.IndexOf(GameConfig.BirthTurns, turn) >= 0)
            {
                Debug.Log($"[TurnManager] 예정 이벤트: 유닛 출생 (턴 {turn})");
                var newUnit = UnitInstance.Create($"주민 {(char)('A' + State.units.Count)}", Job.Pawn, GrowthStage.Child);
                State.units.Add(newUnit);
                OnScheduledEvent?.Invoke(ScheduledEventType.Birth, newUnit);
            }

            // 검증 턴 (11, 23, 35턴)
            if (Array.IndexOf(GameConfig.ValidationTurns, turn) >= 0)
            {
                Debug.Log($"[TurnManager] 예정 이벤트: 검증 턴 (턴 {turn})");
                OnScheduledEvent?.Invoke(ScheduledEventType.Validation, turn);
                GameManager.Instance.CheckValidation(turn);
            }

            // 강제 사망 (12, 24, 36턴)
            if (Array.IndexOf(GameConfig.ForcedDeathTurns, turn) >= 0)
            {
                Debug.Log($"[TurnManager] 예정 이벤트: 강제 사망 (턴 {turn})");
                ProcessForcedDeath();
            }
        }

        /// <summary>
        /// 강제 사망 처리 (유년 제외 랜덤 1명)
        /// </summary>
        private void ProcessForcedDeath()
        {
            var candidates = State.units.FindAll(u => u.stage != GrowthStage.Child);
            
            if (candidates.Count == 0)
            {
                Debug.Log("[TurnManager] 강제 사망 대상 없음 (유년만 있음)");
                return;
            }

            int index = UnityEngine.Random.Range(0, candidates.Count);
            var victim = candidates[index];
            
            Debug.Log($"[TurnManager] 강제 사망: {victim.unitName}");
            UnitSystem.Instance?.KillUnit(victim);
            OnScheduledEvent?.Invoke(ScheduledEventType.ForcedDeath, victim);
        }

        /// <summary>
        /// 랜덤 이벤트 처리
        /// </summary>
        private void ProcessRandomEvents()
        {
            // TODO: EventSystem에서 처리 (M5에서 구현)
            // 80% 확률로 이벤트 발생
            // 긍정 50%, 부정 20%, 선택 30%
            Debug.Log("[TurnManager] 랜덤 이벤트 처리 (미구현)");
        }

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
                    case PersistentEffectType.GoldPerTurn:
                        GoldSystem.Instance?.AddGold(effect.value);
                        Debug.Log($"[TurnManager] 지속 효과: +{effect.value} 골드");
                        break;

                    case PersistentEffectType.MaintenanceIncrease:
                        GoldSystem.Instance?.SubtractGold(effect.value);
                        Debug.Log($"[TurnManager] 지속 효과 (흉년): -{effect.value} 추가 유지비");
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
    }
}
