// =============================================================================
// PromotionPopup.cs
// 전직 선택 팝업 - 유년 → 청년 전환 시 직업 선택
// =============================================================================
//
// [역할]
// - 유년에서 청년으로 성장할 때 자동 표시
// - 3개 직업 중 1개 선택 (폰 고정 + 랜덤 2개)
// - 퀸은 랜덤 슬롯에서만 등장 가능
//
// [흐름]
// 1. TurnManager에서 유년→청년 전환 감지
// 2. UnitSystem.OnUnitNeedsPromotion 이벤트 발생
// 3. PromotionPopup.Show(unit) 호출
// 4. UnitSystem.GetJobChoices()로 3개 직업 획득
// 5. 플레이어가 직업 선택
// 6. UnitSystem.PromoteUnit(unit, job) 호출
//
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 전직 선택 팝업
    /// </summary>
    public class PromotionPopup : MonoBehaviour
    {
        // =====================================================================
        // Inspector 연결
        // =====================================================================

        [Header("팝업 요소")]
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private Image dimBackground;

        [Header("헤더")]
        [SerializeField] private TMP_Text titleText;        // "주민 A의 직업 선택"
        [SerializeField] private TMP_Text subtitleText;     // "청년이 되었습니다. 직업을 선택하세요."

        [Header("직업 버튼")]
        [SerializeField] private Transform jobButtonContainer;
        [SerializeField] private GameObject jobButtonPrefab;

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private UnitInstance currentUnit;
        private List<JobButton> jobButtons = new List<JobButton>();
        private List<Job> currentChoices = new List<Job>();

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            // DimBackground는 클릭해도 닫히지 않음 (필수 선택)

            // 초기 상태: 숨김
            if (dimBackground != null)
            {
                dimBackground.gameObject.SetActive(false);
            }
            if (popupRoot != null)
            {
                popupRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            // 전직 필요 이벤트 구독
            UnitSystem.OnUnitNeedsJobSelection += Show;
        }

        private void OnDisable()
        {
            UnitSystem.OnUnitNeedsJobSelection -= Show;
        }

        // =====================================================================
        // 표시/숨김
        // =====================================================================

        /// <summary>
        /// 팝업 표시
        /// </summary>
        public void Show(UnitInstance unit)
        {
            if (unit == null) return;

            currentUnit = unit;

            // UnitSystem에서 3개 선택지 가져오기 (폰 고정 + 랜덤 2개)
            currentChoices = UnitSystem.Instance?.GetJobChoices() ?? new List<Job> { Job.Pawn };

            // UI 갱신
            UpdateDisplay();

            // 직업 버튼 생성
            CreateJobButtons();

            // 팝업 활성화
            if (dimBackground != null)
            {
                dimBackground.gameObject.SetActive(true);
            }
            if (popupRoot != null)
            {
                popupRoot.SetActive(true);
            }

            Debug.Log($"[PromotionPopup] 표시: {unit.unitName}, 선택지: {string.Join(", ", currentChoices)}");
        }

        /// <summary>
        /// 팝업 숨김
        /// </summary>
        public void Hide()
        {
            if (dimBackground != null)
            {
                dimBackground.gameObject.SetActive(false);
            }
            if (popupRoot != null)
            {
                popupRoot.SetActive(false);
            }

            currentUnit = null;
            currentChoices.Clear();
            ClearJobButtons();

            Debug.Log("[PromotionPopup] 숨김");
        }

        // =====================================================================
        // UI 갱신
        // =====================================================================

        /// <summary>
        /// 표시 갱신
        /// </summary>
        private void UpdateDisplay()
        {
            if (titleText != null)
            {
                titleText.text = $"{currentUnit.unitName}의 직업 선택";
            }

            if (subtitleText != null)
            {
                subtitleText.text = "청년이 되었습니다. 직업을 선택하세요.";
            }
        }

        /// <summary>
        /// 직업 버튼 생성 (UnitSystem에서 받은 3개 선택지)
        /// </summary>
        private void CreateJobButtons()
        {
            ClearJobButtons();

            if (jobButtonContainer == null || jobButtonPrefab == null) return;

            foreach (var job in currentChoices)
            {
                var buttonObj = Instantiate(jobButtonPrefab, jobButtonContainer);
                var jobButton = buttonObj.GetComponent<JobButton>();

                if (jobButton != null)
                {
                    jobButton.Setup(job);
                    jobButton.OnJobSelected += OnJobSelected;
                    jobButtons.Add(jobButton);
                }
            }
        }

        /// <summary>
        /// 직업 버튼 제거
        /// </summary>
        private void ClearJobButtons()
        {
            foreach (var button in jobButtons)
            {
                if (button != null)
                {
                    button.OnJobSelected -= OnJobSelected;
                    Destroy(button.gameObject);
                }
            }
            jobButtons.Clear();
        }

        // =====================================================================
        // 직업 선택 처리
        // =====================================================================

        /// <summary>
        /// 직업 선택됨
        /// </summary>
        private void OnJobSelected(Job selectedJob)
        {
            if (currentUnit == null) return;

            Debug.Log($"[PromotionPopup] 직업 선택: {currentUnit.unitName} → {selectedJob}");

            // 전직 실행
            bool success = UnitSystem.Instance.SelectJob(currentUnit, selectedJob);

            if (success)
            {
                Debug.Log($"[PromotionPopup] 전직 성공!");
            }
            else
            {
                Debug.LogError($"[PromotionPopup] 전직 실패!");
            }

            // 팝업 닫기
            Hide();
        }
    }
}