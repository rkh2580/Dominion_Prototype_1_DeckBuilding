// =============================================================================
// ActionButtonPanel.cs
// 행동 버튼 관리 (플레이 종료, 턴 종료)
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 행동 버튼 패널
    /// 덱/구매 페이즈에 따라 버튼 표시 및 기능 전환
    /// 
    /// [버튼 동작]
    /// - 덱 페이즈: "플레이 종료" → 재화 정산 → 구매 페이즈
    /// - 구매 페이즈: "턴 종료" → 턴 종료 처리 → 다음 턴
    /// </summary>
    public class ActionButtonPanel : MonoBehaviour
    {
        // =====================================================================
        // Inspector 연결
        // =====================================================================

        [Header("버튼")]
        [SerializeField] private Button mainActionButton;       // 메인 버튼 (플레이 종료 / 턴 종료)
        [SerializeField] private TMP_Text mainButtonText;       // 버튼 텍스트

        [Header("버튼 텍스트 설정")]
        [SerializeField] private string endPlayText = "플레이 종료";
        [SerializeField] private string endTurnText = "턴 종료";

        [Header("색상 설정")]
        [SerializeField] private Color deckPhaseColor = new Color(0.3f, 0.5f, 1f);      // 파란색
        [SerializeField] private Color purchasePhaseColor = new Color(0.3f, 0.8f, 0.3f); // 초록색
        [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f);     // 회색

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private Image buttonImage;
        private GamePhase currentPhase;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            // 버튼 이미지 캐싱
            if (mainActionButton != null)
            {
                buttonImage = mainActionButton.GetComponent<Image>();
            }
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Start()
        {
            // 버튼 클릭 리스너 등록
            if (mainActionButton != null)
            {
                mainActionButton.onClick.AddListener(OnMainButtonClicked);
            }

            // 초기 상태 설정
            RefreshButtonState();
        }

        // =====================================================================
        // 이벤트 구독
        // =====================================================================

        private void SubscribeToEvents()
        {
            TurnManager.OnPhaseChanged += OnPhaseChanged;
            GameManager.OnGameStarted += OnGameStarted;
            GameManager.OnGameEnded += OnGameEnded;
        }

        private void UnsubscribeFromEvents()
        {
            TurnManager.OnPhaseChanged -= OnPhaseChanged;
            GameManager.OnGameStarted -= OnGameStarted;
            GameManager.OnGameEnded -= OnGameEnded;
        }

        // =====================================================================
        // 이벤트 핸들러
        // =====================================================================

        private void OnPhaseChanged(GamePhase phase)
        {
            currentPhase = phase;
            RefreshButtonState();
        }

        private void OnGameStarted()
        {
            RefreshButtonState();
        }

        private void OnGameEnded(GameEndState endState)
        {
            // 게임 종료 시 버튼 비활성화
            SetButtonEnabled(false);
            if (mainButtonText != null)
            {
                mainButtonText.text = "게임 종료";
            }
        }

        // =====================================================================
        // 버튼 클릭 처리
        // =====================================================================

        /// <summary>
        /// 메인 버튼 클릭 시 호출
        /// </summary>
        private void OnMainButtonClicked()
        {
            if (TurnManager.Instance == null) return;

            switch (currentPhase)
            {
                case GamePhase.Deck:
                    // 덱 페이즈 → 구매 페이즈
                    OnEndPlayClicked();
                    break;

                case GamePhase.Purchase:
                    // 구매 페이즈 → 턴 종료
                    OnEndTurnClicked();
                    break;

                default:
                    Debug.Log($"[ActionButtonPanel] 현재 페이즈({currentPhase})에서는 버튼 동작 없음");
                    break;
            }
        }

        /// <summary>
        /// 플레이 종료 클릭
        /// </summary>
        private void OnEndPlayClicked()
        {
            Debug.Log("[ActionButtonPanel] 플레이 종료 클릭");

            // TurnManager에서 덱 페이즈 종료 처리
            // → 재화 정산 → 구매 페이즈 전환
            TurnManager.Instance.EndDeckPhase();
        }

        /// <summary>
        /// 턴 종료 클릭
        /// </summary>
        private void OnEndTurnClicked()
        {
            Debug.Log("[ActionButtonPanel] 턴 종료 클릭");

            // TurnManager에서 턴 종료 처리
            // → 유지비 차감 → 카드 정리 → 다음 턴
            TurnManager.Instance.EndTurn();
        }

        // =====================================================================
        // UI 갱신
        // =====================================================================

        /// <summary>
        /// 버튼 상태 갱신
        /// </summary>
        public void RefreshButtonState()
        {
            var state = GameManager.Instance?.State;
            if (state == null)
            {
                SetButtonEnabled(false);
                return;
            }

            currentPhase = state.currentPhase;

            switch (currentPhase)
            {
                case GamePhase.Deck:
                    // 덱 페이즈: "플레이 종료" 버튼
                    SetButtonEnabled(true);
                    SetButtonText(endPlayText);
                    SetButtonColor(deckPhaseColor);
                    break;

                case GamePhase.Purchase:
                    // 구매 페이즈: "턴 종료" 버튼
                    SetButtonEnabled(true);
                    SetButtonText(endTurnText);
                    SetButtonColor(purchasePhaseColor);
                    break;

                case GamePhase.TurnStart:
                case GamePhase.Event:
                case GamePhase.TurnEnd:
                    // 진행 중: 버튼 비활성화
                    SetButtonEnabled(false);
                    SetButtonText("처리 중...");
                    break;

                case GamePhase.GameOver:
                    // 게임 종료
                    SetButtonEnabled(false);
                    SetButtonText("게임 종료");
                    break;
            }
        }

        /// <summary>
        /// 버튼 활성화/비활성화
        /// </summary>
        private void SetButtonEnabled(bool enabled)
        {
            if (mainActionButton != null)
            {
                mainActionButton.interactable = enabled;
            }

            if (buttonImage != null && !enabled)
            {
                buttonImage.color = disabledColor;
            }
        }

        /// <summary>
        /// 버튼 텍스트 설정
        /// </summary>
        private void SetButtonText(string text)
        {
            if (mainButtonText != null)
            {
                mainButtonText.text = text;
            }
        }

        /// <summary>
        /// 버튼 색상 설정
        /// </summary>
        private void SetButtonColor(Color color)
        {
            if (buttonImage != null && mainActionButton.interactable)
            {
                buttonImage.color = color;
            }
        }

        // =====================================================================
        // 디버그
        // =====================================================================

        [ContextMenu("Refresh Button State")]
        private void DebugRefreshState()
        {
            RefreshButtonState();
            Debug.Log($"[ActionButtonPanel] 버튼 상태 갱신 (페이즈: {currentPhase})");
        }
    }
}
