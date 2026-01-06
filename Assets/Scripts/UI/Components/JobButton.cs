// =============================================================================
// JobButton.cs
// 직업 선택 버튼 컴포넌트
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 직업 선택 버튼
    /// </summary>
    public class JobButton : MonoBehaviour
    {
        [Header("UI 요소")]
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text jobNameText;
        [SerializeField] private TMP_Text jobDescText;
        [SerializeField] private Image jobIcon;
        [SerializeField] private Button selectButton;

        [Header("직업별 색상")]
        [SerializeField] private Color pawnColor = new Color(0.8f, 0.6f, 0.2f);      // 갈색 (경제)
        [SerializeField] private Color knightColor = new Color(0.2f, 0.5f, 0.8f);    // 파랑 (드로우)
        [SerializeField] private Color bishopColor = new Color(0.6f, 0.3f, 0.7f);    // 보라 (정화)
        [SerializeField] private Color rookColor = new Color(0.5f, 0.5f, 0.5f);      // 회색 (방어)

        private Job boundJob;

        /// <summary>
        /// 직업 선택 이벤트
        /// </summary>
        public event System.Action<Job> OnJobSelected;

        private void Awake()
        {
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnSelectClicked);
            }
        }

        /// <summary>
        /// 버튼 설정
        /// </summary>
        public void Setup(Job job)
        {
            boundJob = job;

            // 직업 이름
            if (jobNameText != null)
            {
                jobNameText.text = GetJobDisplayName(job);
            }

            // 직업 설명
            if (jobDescText != null)
            {
                jobDescText.text = GetJobDescription(job);
            }

            // 배경색
            if (background != null)
            {
                background.color = job switch
                {
                    Job.Pawn => pawnColor,
                    Job.Knight => knightColor,
                    Job.Bishop => bishopColor,
                    Job.Rook => rookColor,
                    _ => Color.gray
                };
            }
        }

        /// <summary>
        /// 선택 버튼 클릭
        /// </summary>
        private void OnSelectClicked()
        {
            OnJobSelected?.Invoke(boundJob);
        }

        /// <summary>
        /// 직업 표시 이름
        /// </summary>
        private string GetJobDisplayName(Job job)
        {
            return job switch
            {
                Job.Pawn => "♟ 폰",
                Job.Knight => "♞ 나이트",
                Job.Bishop => "♝ 비숍",
                Job.Rook => "♜ 룩",
                Job.Queen => "♛ 퀸",
                _ => "???"
            };
        }

        /// <summary>
        /// 직업 설명
        /// </summary>
        private string GetJobDescription(Job job)
        {
            return job switch
            {
                Job.Pawn => "경제/생산 특화\n골드 획득, 재화 강화",
                Job.Knight => "드로우/도박 특화\n카드 드로우, 확률 보상",
                Job.Bishop => "덱 관리/정화 특화\n카드 소멸, 오염 제거",
                Job.Rook => "방어/안정 특화\n지속 수입, 유지비 감소",
                Job.Queen => "만능/복합\n모든 카드풀 접근",
                _ => ""
            };
        }
    }
}
