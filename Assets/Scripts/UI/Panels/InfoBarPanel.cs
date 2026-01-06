// =============================================================================
// InfoBarPanel.cs
// 상단 정보 바 UI - 턴, 골드, 유지비, 액션, 페이즈, 전투력, 충성도 표시
// =============================================================================
// [R8 수정] 2026-01-03
// - 전투력 표시 추가
// - 평균 충성도 표시 추가
// - 이모지 → 텍스트로 변경 (폰트 호환성)
// =============================================================================
//
// [Unity 레이아웃 설정 가이드]
// 
// InfoBarPanel이 자동으로 자식 요소들을 배치하려면:
// 
// 1. InfoBarPanel 오브젝트에 다음 컴포넌트 추가:
//    - Horizontal Layout Group
//      - Child Alignment: Middle Left (또는 Middle Center)
//      - Child Force Expand Width: OFF (체크 해제)
//      - Child Force Expand Height: ON
//      - Spacing: 15~20
//    - Content Size Fitter (선택)
//      - Horizontal Fit: Preferred Size
//
// 2. 각 자식 TMP_Text에:
//    - Layout Element 컴포넌트 추가
//      - Preferred Width: 80~120 (텍스트 길이에 따라)
//    - 또는 Content Size Fitter
//      - Horizontal Fit: Preferred Size
//
// 3. 구분자(|)를 넣고 싶다면:
//    - 별도의 TMP_Text 오브젝트로 "|" 추가
//    - 또는 각 텍스트에 " | " 포함
//
// =============================================================================

using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

// LayoutRebuilder는 UnityEngine.UI 네임스페이스에 포함됨

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 상단 정보 바 패널
    /// 게임의 핵심 수치를 실시간으로 표시
    /// 
    /// [표시 항목]
    /// - 턴 카운터: 현재 턴 / 최대 턴 (60)
    /// - 골드: 보유 골드 (음수 시 빨간색)
    /// - 유지비: 턴 종료 시 차감될 골드
    /// - 액션: 남은 액션 수
    /// - 페이즈: 현재 게임 페이즈 (덱/구매)
    /// - 전투력: 총 전투력
    /// - 충성도: 평균 충성도
    /// </summary>
    public class InfoBarPanel : MonoBehaviour
    {
        // =====================================================================
        // Inspector 연결 (Unity 에디터에서 할당)
        // =====================================================================

        [Header("텍스트 요소 - 기존")]
        [SerializeField] private TMP_Text turnText;        // "턴: 15/60"
        [SerializeField] private TMP_Text goldText;        // "골드: 127"
        [SerializeField] private TMP_Text maintenanceText; // "유지비: 8"
        [SerializeField] private TMP_Text actionText;      // "액션: 2"
        [SerializeField] private TMP_Text phaseText;       // "덱" 또는 "구매"

        [Header("텍스트 요소 - 신규 (R8)")]
        [SerializeField] private TMP_Text combatPowerText; // "전투력: 245"
        [SerializeField] private TMP_Text loyaltyText;     // "충성도: 85"

        [Header("색상 설정 - 기존")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color dangerColor = Color.red;
        [SerializeField] private Color deckPhaseColor = new Color(0.3f, 0.5f, 1f);    // 파란색
        [SerializeField] private Color purchasePhaseColor = new Color(0.3f, 0.8f, 0.3f); // 초록색

        [Header("색상 설정 - 충성도")]
        [SerializeField] private Color loyaltyHighColor = new Color(0.3f, 0.9f, 0.3f);  // 80+ 녹색
        [SerializeField] private Color loyaltyMidColor = new Color(0.9f, 0.9f, 0.3f);   // 50~79 노란색
        [SerializeField] private Color loyaltyLowColor = new Color(0.9f, 0.3f, 0.3f);   // 50 미만 빨간색

        [Header("검증 턴 강조")]
        [SerializeField] private Color validationTurnColor = Color.yellow;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void OnEnable()
        {
            // 이벤트 구독
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제 (메모리 누수 방지)
            UnsubscribeFromEvents();
        }

        private void Start()
        {
            // 초기 상태 표시
            RefreshAll();

            // 레이아웃 강제 갱신 (Content Size Fitter 타이밍 이슈 해결)
            ForceLayoutRebuild();
        }

        /// <summary>
        /// 레이아웃 강제 재계산
        /// Content Size Fitter와 Horizontal Layout Group 조합 시 
        /// 초기화 타이밍 이슈로 인한 불일치 해결
        /// </summary>
        private void ForceLayoutRebuild()
        {
            Canvas.ForceUpdateCanvases();

            var rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }
        }

        // =====================================================================
        // 이벤트 구독 관리
        // =====================================================================

        /// <summary>
        /// 모든 필요한 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            // 턴 시작 → 턴 카운터 갱신
            TurnManager.OnTurnStarted += OnTurnStarted;

            // 페이즈 변경 → 페이즈 표시 갱신
            TurnManager.OnPhaseChanged += OnPhaseChanged;

            // 액션 변경 → 액션 표시 갱신
            TurnManager.OnActionsChanged += OnActionsChanged;

            // 골드 변경 → 골드 표시 갱신
            GoldSystem.OnGoldChanged += OnGoldChanged;

            // 게임 상태 변경 → 유지비 등 갱신
            GameManager.OnGameStateChanged += OnGameStateChanged;

            // 게임 시작 → 전체 갱신
            GameManager.OnGameStarted += OnGameStarted;

            // [R8 신규] 충성도 변경 → 충성도 표시 갱신
            LoyaltySystem.OnLoyaltyChanged += OnLoyaltyChanged;

            // [R8 신규] 유닛 생성/사망 → 전투력/충성도 갱신
            UnitSystem.OnUnitCreated += OnUnitChanged;
            UnitSystem.OnUnitDied += OnUnitChanged;

            // [R8 신규] 전직 완료 → 전투력 갱신
            UnitSystem.OnUnitLeveledUp += OnUnitLeveledUp;

            // [R8 신규] 약탈 완료 → 전투력 갱신 (유닛 납치 시)
            RaidSystem.OnRaidCompleted += OnRaidCompleted;
        }

        /// <summary>
        /// 모든 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            TurnManager.OnTurnStarted -= OnTurnStarted;
            TurnManager.OnPhaseChanged -= OnPhaseChanged;
            TurnManager.OnActionsChanged -= OnActionsChanged;
            GoldSystem.OnGoldChanged -= OnGoldChanged;
            GameManager.OnGameStateChanged -= OnGameStateChanged;
            GameManager.OnGameStarted -= OnGameStarted;

            // [R8 신규]
            LoyaltySystem.OnLoyaltyChanged -= OnLoyaltyChanged;
            UnitSystem.OnUnitCreated -= OnUnitChanged;
            UnitSystem.OnUnitDied -= OnUnitChanged;
            UnitSystem.OnUnitLeveledUp -= OnUnitLeveledUp;
            RaidSystem.OnRaidCompleted -= OnRaidCompleted;
        }

        // =====================================================================
        // 이벤트 핸들러 - 기존
        // =====================================================================

        /// <summary>
        /// 턴 시작 시 호출
        /// </summary>
        private void OnTurnStarted(int turn)
        {
            UpdateTurnDisplay(turn);
        }

        /// <summary>
        /// 페이즈 변경 시 호출
        /// </summary>
        private void OnPhaseChanged(GamePhase phase)
        {
            UpdatePhaseDisplay(phase);
        }

        /// <summary>
        /// 액션 변경 시 호출
        /// </summary>
        private void OnActionsChanged(int oldValue, int newValue)
        {
            UpdateActionDisplay(newValue);
        }

        /// <summary>
        /// 골드 변경 시 호출
        /// </summary>
        private void OnGoldChanged(int oldValue, int newValue, int delta)
        {
            UpdateGoldDisplay(newValue);
        }

        /// <summary>
        /// 게임 상태 변경 시 호출 (유지비 갱신용)
        /// </summary>
        private void OnGameStateChanged(GameState state)
        {
            UpdateMaintenanceDisplay(state.maintenanceCost);
        }

        /// <summary>
        /// 게임 시작 시 호출
        /// </summary>
        private void OnGameStarted()
        {
            RefreshAll();
        }

        // =====================================================================
        // 이벤트 핸들러 - 신규 (R8)
        // =====================================================================

        /// <summary>
        /// 충성도 변경 시 호출
        /// </summary>
        private void OnLoyaltyChanged(UnitInstance unit, int delta, int newLoyalty)
        {
            UpdateLoyaltyDisplay();
        }

        /// <summary>
        /// 유닛 생성/사망 시 호출
        /// </summary>
        private void OnUnitChanged(UnitInstance unit)
        {
            UpdateCombatPowerDisplay();
            UpdateLoyaltyDisplay();
        }

        /// <summary>
        /// 유닛 전직 완료 시 호출
        /// </summary>
        private void OnUnitLeveledUp(UnitInstance unit, int newLevel, CardInstance card)
        {
            UpdateCombatPowerDisplay();
        }

        /// <summary>
        /// 약탈 완료 시 호출
        /// </summary>
        private void OnRaidCompleted(bool success, int enemyPower, int allyPower)
        {
            // 유닛 납치 등으로 전투력 변경 가능
            UpdateCombatPowerDisplay();
            UpdateLoyaltyDisplay();
        }

        // =====================================================================
        // UI 갱신 메서드
        // =====================================================================

        /// <summary>
        /// 모든 표시 항목 갱신
        /// </summary>
        public void RefreshAll()
        {
            var state = GameManager.Instance?.State;
            if (state == null) return;

            UpdateTurnDisplay(state.currentTurn);
            UpdateGoldDisplay(state.gold);
            UpdateMaintenanceDisplay(state.maintenanceCost);
            UpdateActionDisplay(state.remainingActions);
            UpdatePhaseDisplay(state.currentPhase);

            // [R8 신규]
            UpdateCombatPowerDisplay();
            UpdateLoyaltyDisplay();
        }

        /// <summary>
        /// 턴 카운터 표시 갱신
        /// </summary>
        private void UpdateTurnDisplay(int turn)
        {
            if (turnText == null) return;

            turnText.text = $"턴: {turn}/{GameConfig.MaxTurns}";

            // 검증 턴 강조 (10, 20, 30, 40, 50턴)
            bool isValidationTurn = System.Array.IndexOf(GameConfig.ValidationTurns, turn) >= 0;
            turnText.color = isValidationTurn ? validationTurnColor : normalColor;
        }

        /// <summary>
        /// 골드 표시 갱신
        /// </summary>
        private void UpdateGoldDisplay(int gold)
        {
            if (goldText == null) return;

            goldText.text = $"골드: {gold}";

            // 음수면 빨간색
            if (gold < 0)
            {
                goldText.color = dangerColor;
            }
            // 유지비보다 적으면 주황색 경고
            else if (GameManager.Instance?.State != null &&
                     gold < GameManager.Instance.State.maintenanceCost)
            {
                goldText.color = warningColor;
            }
            else
            {
                goldText.color = normalColor;
            }
        }

        /// <summary>
        /// 유지비 표시 갱신
        /// </summary>
        private void UpdateMaintenanceDisplay(int maintenance)
        {
            if (maintenanceText == null) return;

            maintenanceText.text = $"유지비: {maintenance}";
        }

        /// <summary>
        /// 액션 표시 갱신
        /// </summary>
        private void UpdateActionDisplay(int actions)
        {
            if (actionText == null) return;

            actionText.text = $"액션: {actions}";

            // 0이면 회색 처리
            actionText.color = actions <= 0 ? Color.gray : normalColor;
        }

        /// <summary>
        /// 페이즈 표시 갱신
        /// </summary>
        private void UpdatePhaseDisplay(GamePhase phase)
        {
            if (phaseText == null) return;

            switch (phase)
            {
                case GamePhase.Deck:
                    phaseText.text = "덱";
                    phaseText.color = deckPhaseColor;
                    break;

                case GamePhase.Purchase:
                    phaseText.text = "구매";
                    phaseText.color = purchasePhaseColor;
                    break;

                case GamePhase.TurnStart:
                case GamePhase.Event:
                    phaseText.text = "이벤트";
                    phaseText.color = normalColor;
                    break;

                case GamePhase.TurnEnd:
                    phaseText.text = "정산";
                    phaseText.color = normalColor;
                    break;

                case GamePhase.GameOver:
                    phaseText.text = "종료";
                    phaseText.color = dangerColor;
                    break;

                default:
                    phaseText.text = "-";
                    phaseText.color = normalColor;
                    break;
            }
        }

        // =====================================================================
        // UI 갱신 메서드 - 신규 (R8)
        // =====================================================================

        /// <summary>
        /// 전투력 표시 갱신
        /// </summary>
        private void UpdateCombatPowerDisplay()
        {
            if (combatPowerText == null) return;

            var state = GameManager.Instance?.State;
            if (state == null)
            {
                combatPowerText.text = "전투력: -";
                return;
            }

            // 총 전투력 계산
            int totalPower = CalculateTotalCombatPower(state);
            combatPowerText.text = $"전투력: {totalPower}";
        }

        /// <summary>
        /// 충성도 표시 갱신 (평균)
        /// </summary>
        private void UpdateLoyaltyDisplay()
        {
            if (loyaltyText == null) return;

            var state = GameManager.Instance?.State;
            if (state == null || state.units == null || state.units.Count == 0)
            {
                loyaltyText.text = "충성도: -";
                loyaltyText.color = normalColor;
                return;
            }

            // 성인 유닛만 평균 계산 (유년 제외)
            int sum = 0;
            int count = 0;

            foreach (var unit in state.units)
            {
                if (unit.stage != GrowthStage.Child)
                {
                    sum += unit.loyalty;
                    count++;
                }
            }

            if (count == 0)
            {
                loyaltyText.text = "충성도: -";
                loyaltyText.color = normalColor;
                return;
            }

            int average = sum / count;
            loyaltyText.text = $"충성도: {average}";

            // 충성도 색상
            loyaltyText.color = average switch
            {
                >= 80 => loyaltyHighColor,
                >= 50 => loyaltyMidColor,
                _ => loyaltyLowColor
            };
        }

        /// <summary>
        /// 총 전투력 계산
        /// </summary>
        private int CalculateTotalCombatPower(GameState state)
        {
            if (state.units == null || state.units.Count == 0) return 0;

            int total = 0;
            foreach (var unit in state.units)
            {
                // 유년은 전투력 0
                if (unit.stage == GrowthStage.Child) continue;

                // combatPower 필드 사용 (R1에서 추가됨)
                total += unit.combatPower;
            }

            return total;
        }

        // =====================================================================
        // 디버그
        // =====================================================================

        /// <summary>
        /// Inspector에서 테스트용
        /// </summary>
        [ContextMenu("Refresh All")]
        private void DebugRefreshAll()
        {
            RefreshAll();
            Debug.Log("[InfoBarPanel] 수동 갱신 완료");
        }
    }
}