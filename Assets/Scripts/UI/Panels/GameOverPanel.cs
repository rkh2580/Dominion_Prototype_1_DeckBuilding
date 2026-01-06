// =============================================================================
// GameOverPanel.cs
// 게임 종료 화면 (승리/패배 결과 표시)
// =============================================================================
//
// [역할]
// - 게임 종료 시 결과 표시
// - 승리/패배에 따른 다른 연출
// - 재시작 버튼
//
// [트리거]
// - GameManager.OnGameEnded 이벤트
//
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 게임 종료 화면
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        // =====================================================================
        // Inspector 연결
        // =====================================================================

        [Header("패널 요소")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image dimBackground;

        [Header("결과 표시")]
        [SerializeField] private TMP_Text resultTitleText;     // "승리!" or "패배..."
        [SerializeField] private TMP_Text resultMessageText;   // 상세 메시지
        [SerializeField] private TMP_Text statsText;           // 통계 (턴 수, 유닛 수 등)

        [Header("결과 아이콘/이미지 (선택)")]
        [SerializeField] private Image resultIcon;
        [SerializeField] private Sprite victorySprite;
        [SerializeField] private Sprite defeatSprite;

        [Header("버튼")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;        // 프로토타입에서는 선택적

        [Header("색상 설정")]
        [SerializeField] private Color victoryColor = new Color(1f, 0.84f, 0f);      // 금색
        [SerializeField] private Color defeatColor = new Color(0.6f, 0.2f, 0.2f);    // 어두운 빨강
        [SerializeField] private Color victoryBgColor = new Color(0.2f, 0.3f, 0.2f); // 어두운 초록
        [SerializeField] private Color defeatBgColor = new Color(0.3f, 0.15f, 0.15f); // 어두운 빨강

        [Header("배경 패널 (색상 변경용)")]
        [SerializeField] private Image panelBackground;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            // 버튼 이벤트 연결
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            }

            // 초기 상태: 숨김
            Hide();
        }

        private void OnEnable()
        {
            // GameManager 이벤트 구독
            GameManager.OnGameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameManager.OnGameEnded -= OnGameEnded;
        }

        private void OnDestroy()
        {
            // 버튼 이벤트 해제
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            }
        }

        // =====================================================================
        // 이벤트 핸들러
        // =====================================================================

        /// <summary>
        /// 게임 종료 시 호출
        /// </summary>
        private void OnGameEnded(GameEndState endState)
        {
            Show(endState);
        }

        // =====================================================================
        // 표시/숨김
        // =====================================================================

        /// <summary>
        /// 패널 표시
        /// </summary>
        public void Show(GameEndState endState)
        {
            // 결과에 따른 UI 갱신
            UpdateDisplay(endState);

            // 패널 활성화
            if (dimBackground != null)
            {
                dimBackground.gameObject.SetActive(true);
            }
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            Debug.Log($"[GameOverPanel] 표시: {endState}");
        }

        /// <summary>
        /// 패널 숨김
        /// </summary>
        public void Hide()
        {
            if (dimBackground != null)
            {
                dimBackground.gameObject.SetActive(false);
            }
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            Debug.Log("[GameOverPanel] 숨김");
        }

        // =====================================================================
        // UI 갱신
        // =====================================================================

        /// <summary>
        /// 결과에 따른 UI 갱신
        /// </summary>
        private void UpdateDisplay(GameEndState endState)
        {
            bool isVictory = endState == GameEndState.Victory;

            // 제목
            if (resultTitleText != null)
            {
                resultTitleText.text = isVictory ? "승리!" : "패배...";
                resultTitleText.color = isVictory ? victoryColor : defeatColor;
            }

            // 메시지
            if (resultMessageText != null)
            {
                resultMessageText.text = GetResultMessage(endState);
            }

            // 통계
            if (statsText != null)
            {
                statsText.text = GetStatsText();
            }

            // 아이콘
            if (resultIcon != null)
            {
                if (isVictory && victorySprite != null)
                {
                    resultIcon.sprite = victorySprite;
                    resultIcon.color = victoryColor;
                }
                else if (!isVictory && defeatSprite != null)
                {
                    resultIcon.sprite = defeatSprite;
                    resultIcon.color = defeatColor;
                }
            }

            // 배경 색상
            if (panelBackground != null)
            {
                panelBackground.color = isVictory ? victoryBgColor : defeatBgColor;
            }
        }

        /// <summary>
        /// 결과 메시지 생성
        /// </summary>
        private string GetResultMessage(GameEndState endState)
        {
            return endState switch
            {
                GameEndState.Victory => 
                    "축하합니다!\n45턴을 성공적으로 생존했습니다.\n당신의 마을은 번영을 이루었습니다.",
                
                GameEndState.DefeatBankrupt => 
                    "파산...\n마을의 금고가 바닥났습니다.\n주민들이 흩어졌습니다.",
                
                GameEndState.DefeatValidation => 
                    "검증 실패...\n요구된 골드를 모으지 못했습니다.\n왕국에서 마을을 폐쇄했습니다.",
                
                _ => "게임 종료"
            };
        }

        /// <summary>
        /// 통계 텍스트 생성
        /// </summary>
        private string GetStatsText()
        {
            var state = GameManager.Instance?.State;
            if (state == null) return "";

            int finalTurn = state.currentTurn;
            int unitCount = state.units?.Count ?? 0;
            int deckSize = (state.deck?.Count ?? 0) + (state.hand?.Count ?? 0) + (state.discardPile?.Count ?? 0);
            int finalGold = state.gold;

            return $"최종 턴: {finalTurn}\n" +
                   $"남은 유닛: {unitCount}명\n" +
                   $"덱 크기: {deckSize}장\n" +
                   $"최종 골드: {finalGold}";
        }

        // =====================================================================
        // 버튼 핸들러
        // =====================================================================

        /// <summary>
        /// 재시작 버튼 클릭
        /// </summary>
        private void OnRestartClicked()
        {
            Debug.Log("[GameOverPanel] 재시작 버튼 클릭");

            // 패널 숨기기
            Hide();

            // 씬 재로드 또는 GameManager.StartNewGame() 호출
            // 방법 1: 씬 재로드 (깔끔하지만 느림)
            // SceneManager.LoadScene(SceneManager.GetActiveScene().name);

            // 방법 2: GameManager에서 재시작 (빠름, 싱글톤 주의)
            RestartGame();
        }

        /// <summary>
        /// 메인 메뉴 버튼 클릭 (프로토타입에서는 미사용)
        /// </summary>
        private void OnMainMenuClicked()
        {
            Debug.Log("[GameOverPanel] 메인 메뉴 버튼 클릭");

            // 메인 메뉴 씬으로 이동 (구현 시)
            // SceneManager.LoadScene("MainMenu");
        }

        /// <summary>
        /// 게임 재시작
        /// </summary>
        private void RestartGame()
        {
            // 현재 씬 재로드 (가장 안전한 방법)
            string currentScene = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(currentScene);

            Debug.Log("[GameOverPanel] 게임 재시작");
        }

        // =====================================================================
        // 외부 접근
        // =====================================================================

        /// <summary>
        /// 패널이 열려있는지 확인
        /// </summary>
        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;
    }
}
