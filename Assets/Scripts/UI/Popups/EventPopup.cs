// =============================================================================
// EventPopup.cs
// 랜덤 이벤트 표시 팝업 (E8 수정 - 새 시그니처 적용)
// =============================================================================
//
// [역할]
// - 랜덤 이벤트 발생 시 자동 표시
// - 이벤트 제목, 설명, 결과 표시
// - 긍정/부정 이벤트: 확인 버튼
// - 선택 이벤트: N개 선택지 동적 버튼 생성
//
// [E8 변경사항]
// - OnChoiceRequired 시그니처 변경: List<EventChoiceInfo>, Action<int>
// - GetEventInfo() 반환값 변경: 2개 (name, description)
//
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckBuildingEconomy.Core;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 선택지 정보 (내부용)
    /// </summary>
    [Serializable]
    public class ChoiceOption
    {
        public string text;         // 버튼에 표시될 텍스트
        public bool interactable;   // 선택 가능 여부

        public ChoiceOption(string text, bool interactable = true)
        {
            this.text = text;
            this.interactable = interactable;
        }
    }

    /// <summary>
    /// 이벤트 표시 팝업
    /// </summary>
    public class EventPopup : MonoBehaviour
    {
        // =====================================================================
        // Inspector 연결
        // =====================================================================

        [Header("팝업 요소")]
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private Image dimBackground;

        [Header("이벤트 정보")]
        [SerializeField] private TMP_Text titleText;          // 이벤트 제목
        [SerializeField] private TMP_Text descriptionText;    // 이벤트 설명
        [SerializeField] private TMP_Text resultText;         // 효과 결과 메시지

        [Header("카테고리 표시")]
        [SerializeField] private Image categoryIcon;          // 카테고리 아이콘 (선택)
        [SerializeField] private TMP_Text categoryText;       // "긍정", "부정", "선택"

        [Header("버튼 - 일반 이벤트")]
        [SerializeField] private GameObject confirmButtonGroup;
        [SerializeField] private Button confirmButton;

        [Header("버튼 - 선택 이벤트 (동적 생성)")]
        [SerializeField] private GameObject choiceButtonGroup;
        [SerializeField] private Transform choiceButtonContainer;  // 버튼들이 생성될 부모
        [SerializeField] private GameObject choiceButtonPrefab;    // 버튼 프리팹

        [Header("색상 설정")]
        [SerializeField] private Color positiveColor = new Color(0.3f, 0.7f, 0.3f);   // 초록
        [SerializeField] private Color negativeColor = new Color(0.8f, 0.3f, 0.3f);   // 빨강
        [SerializeField] private Color choiceColor = new Color(0.3f, 0.5f, 0.8f);     // 파랑

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private string currentEventId;
        private RandomEventCategory currentCategory;
        private Action<int> choiceCallback;  // 선택된 인덱스를 전달
        private bool isWaitingForChoice = false;

        // 동적 생성된 버튼들
        private List<GameObject> dynamicButtons = new List<GameObject>();

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            // 확인 버튼 이벤트 연결
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            // 초기 상태: 숨김
            Hide();
        }

        private void OnEnable()
        {
            // EventSystem 이벤트 구독 (E8 새 시그니처)
            EventSystem.OnRandomEventTriggered += OnEventTriggered;
            EventSystem.OnChoiceRequired += OnChoiceRequired;
            EventSystem.OnEventCompleted += OnEventCompleted;
        }

        private void OnDisable()
        {
            EventSystem.OnRandomEventTriggered -= OnEventTriggered;
            EventSystem.OnChoiceRequired -= OnChoiceRequired;
            EventSystem.OnEventCompleted -= OnEventCompleted;
        }

        private void OnDestroy()
        {
            // 버튼 이벤트 해제
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
            }

            // 동적 버튼 정리
            ClearDynamicButtons();
        }

        // =====================================================================
        // EventSystem 이벤트 핸들러
        // =====================================================================

        /// <summary>
        /// 랜덤 이벤트 발생 시
        /// </summary>
        private void OnEventTriggered(string eventId, RandomEventCategory category)
        {
            currentEventId = eventId;
            currentCategory = category;

            // 선택 이벤트는 OnChoiceRequired에서 처리
            if (category == RandomEventCategory.Choice)
            {
                return;
            }

            // 긍정/부정 이벤트: 바로 표시
            ShowEvent(eventId, category);
        }

        /// <summary>
        /// 선택 이벤트 - 선택 필요 시 (E8 새 시그니처)
        /// </summary>
        private void OnChoiceRequired(string eventId, string description, List<EventChoiceInfo> choices, Action<int> callback)
        {
            currentEventId = eventId;
            currentCategory = RandomEventCategory.Choice;
            isWaitingForChoice = true;
            choiceCallback = callback;

            // EventChoiceInfo를 ChoiceOption으로 변환
            var choiceOptions = new List<ChoiceOption>();
            foreach (var choice in choices)
            {
                choiceOptions.Add(new ChoiceOption(choice.choiceText, choice.canSelect));
            }

            ShowChoiceEvent(eventId, description, choiceOptions);
        }

        /// <summary>
        /// 이벤트 효과 적용 완료 시
        /// </summary>
        private void OnEventCompleted(string eventId, string resultMessage)
        {
            // 결과 메시지 표시
            if (resultText != null)
            {
                resultText.text = resultMessage;
                resultText.gameObject.SetActive(true);
            }

            // 선택 이벤트 완료 후 버튼 전환
            if (isWaitingForChoice)
            {
                isWaitingForChoice = false;
                ClearDynamicButtons();
                ShowConfirmButtonOnly();
            }
        }

        // =====================================================================
        // 표시/숨김
        // =====================================================================

        /// <summary>
        /// 긍정/부정 이벤트 표시
        /// </summary>
        private void ShowEvent(string eventId, RandomEventCategory category)
        {
            // E8: GetEventInfo는 이제 2개만 반환
            var (title, description) = EventSystem.Instance.GetEventInfo(eventId);

            // UI 갱신
            UpdateTitle(title);
            UpdateDescription(description);
            UpdateCategory(category);

            // 결과는 OnEventCompleted에서 표시
            if (resultText != null)
            {
                resultText.gameObject.SetActive(false);
            }

            // 버튼 설정: 확인 버튼만
            ShowConfirmButtonOnly();

            // 팝업 표시
            ShowPopup();

            Debug.Log($"[EventPopup] 표시: {title} ({category})");
        }

        /// <summary>
        /// 선택 이벤트 표시 (동적 버튼 생성)
        /// </summary>
        private void ShowChoiceEvent(string eventId, string description, List<ChoiceOption> choices)
        {
            // E8: GetEventInfo는 이제 2개만 반환
            var (title, _) = EventSystem.Instance.GetEventInfo(eventId);

            // UI 갱신
            UpdateTitle(title);
            UpdateDescription(description);
            UpdateCategory(RandomEventCategory.Choice);

            // 결과는 선택 후 표시
            if (resultText != null)
            {
                resultText.gameObject.SetActive(false);
            }

            // 동적 버튼 생성
            CreateChoiceButtons(choices);

            // 팝업 표시
            ShowPopup();

            Debug.Log($"[EventPopup] 선택 이벤트 표시: {title} (선택지 {choices.Count}개)");
        }

        /// <summary>
        /// 팝업 표시
        /// </summary>
        private void ShowPopup()
        {
            if (popupRoot != null)
            {
                popupRoot.SetActive(true);
            }
            if (dimBackground != null)
            {
                dimBackground.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 팝업 숨김
        /// </summary>
        private void Hide()
        {
            if (popupRoot != null)
            {
                popupRoot.SetActive(false);
            }
            if (dimBackground != null)
            {
                dimBackground.gameObject.SetActive(false);
            }

            ClearDynamicButtons();
            isWaitingForChoice = false;
            choiceCallback = null;
        }

        // =====================================================================
        // UI 갱신 헬퍼
        // =====================================================================

        /// <summary>
        /// 제목 갱신
        /// </summary>
        private void UpdateTitle(string title)
        {
            if (titleText != null)
            {
                titleText.text = title;
            }
        }

        /// <summary>
        /// 설명 갱신
        /// </summary>
        private void UpdateDescription(string description)
        {
            if (descriptionText != null)
            {
                descriptionText.text = description;
            }
        }

        /// <summary>
        /// 카테고리 표시 갱신
        /// </summary>
        private void UpdateCategory(RandomEventCategory category)
        {
            if (categoryText != null)
            {
                categoryText.text = category switch
                {
                    RandomEventCategory.Positive => "긍정 이벤트",
                    RandomEventCategory.Negative => "부정 이벤트",
                    RandomEventCategory.Choice => "선택 이벤트",
                    _ => ""
                };

                categoryText.color = category switch
                {
                    RandomEventCategory.Positive => positiveColor,
                    RandomEventCategory.Negative => negativeColor,
                    RandomEventCategory.Choice => choiceColor,
                    _ => Color.white
                };
            }

            // 카테고리 아이콘 색상도 변경 (있으면)
            if (categoryIcon != null)
            {
                categoryIcon.color = category switch
                {
                    RandomEventCategory.Positive => positiveColor,
                    RandomEventCategory.Negative => negativeColor,
                    RandomEventCategory.Choice => choiceColor,
                    _ => Color.white
                };
            }
        }

        /// <summary>
        /// 확인 버튼만 표시
        /// </summary>
        private void ShowConfirmButtonOnly()
        {
            if (confirmButtonGroup != null)
            {
                confirmButtonGroup.SetActive(true);
            }
            if (choiceButtonGroup != null)
            {
                choiceButtonGroup.SetActive(false);
            }
        }

        // =====================================================================
        // 동적 버튼 생성
        // =====================================================================

        /// <summary>
        /// 선택지 버튼 동적 생성
        /// </summary>
        private void CreateChoiceButtons(List<ChoiceOption> choices)
        {
            // 기존 버튼 정리
            ClearDynamicButtons();

            // 버튼 그룹 활성화
            if (confirmButtonGroup != null)
            {
                confirmButtonGroup.SetActive(false);
            }
            if (choiceButtonGroup != null)
            {
                choiceButtonGroup.SetActive(true);
            }

            // 프리팹과 컨테이너 체크
            if (choiceButtonPrefab == null || choiceButtonContainer == null)
            {
                Debug.LogError("[EventPopup] choiceButtonPrefab 또는 choiceButtonContainer가 설정되지 않았습니다.");
                return;
            }

            // 선택지 개수만큼 버튼 생성
            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];

                // 프리팹 복제
                GameObject buttonObj = Instantiate(choiceButtonPrefab, choiceButtonContainer);
                buttonObj.SetActive(true);

                // 버튼 컴포넌트 찾기
                Button button = buttonObj.GetComponent<Button>();
                TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();

                // 텍스트 설정
                if (buttonText != null)
                {
                    buttonText.text = choice.text;
                }

                // 활성화 상태 설정
                if (button != null)
                {
                    button.interactable = choice.interactable;

                    // 클릭 이벤트 연결 (클로저 주의: 지역 변수로 복사)
                    int choiceIndex = i;
                    button.onClick.AddListener(() => OnChoiceClicked(choiceIndex));
                }

                // 리스트에 추가 (나중에 정리용)
                dynamicButtons.Add(buttonObj);
            }

            Debug.Log($"[EventPopup] 선택 버튼 {choices.Count}개 생성됨");
        }

        /// <summary>
        /// 동적 생성된 버튼들 제거
        /// </summary>
        private void ClearDynamicButtons()
        {
            foreach (var buttonObj in dynamicButtons)
            {
                if (buttonObj != null)
                {
                    // 이벤트 해제
                    var button = buttonObj.GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.RemoveAllListeners();
                    }

                    Destroy(buttonObj);
                }
            }
            dynamicButtons.Clear();
        }

        // =====================================================================
        // 버튼 클릭 핸들러
        // =====================================================================

        /// <summary>
        /// 확인 버튼 클릭
        /// </summary>
        private void OnConfirmClicked()
        {
            Debug.Log("[EventPopup] 확인 버튼 클릭");
            Hide();
        }

        /// <summary>
        /// 선택지 버튼 클릭 (인덱스 기반)
        /// </summary>
        private void OnChoiceClicked(int choiceIndex)
        {
            Debug.Log($"[EventPopup] 선택지 {choiceIndex} 클릭");

            if (choiceCallback != null)
            {
                choiceCallback.Invoke(choiceIndex);
                choiceCallback = null;
            }
        }

        // =====================================================================
        // 외부 접근용
        // =====================================================================

        /// <summary>
        /// 팝업이 열려있는지 확인
        /// </summary>
        public bool IsVisible => popupRoot != null && popupRoot.activeSelf;

        // =====================================================================
        // 확장용: 직접 호출
        // =====================================================================

        /// <summary>
        /// 외부에서 다중 선택지 이벤트 직접 표시
        /// </summary>
        public void ShowMultipleChoiceEvent(string title, string description, List<ChoiceOption> choices, Action<int> callback)
        {
            currentEventId = "custom";
            currentCategory = RandomEventCategory.Choice;
            choiceCallback = callback;
            isWaitingForChoice = true;

            // UI 갱신
            UpdateTitle(title);
            UpdateDescription(description);
            UpdateCategory(RandomEventCategory.Choice);

            // 결과 숨김
            if (resultText != null)
            {
                resultText.gameObject.SetActive(false);
            }

            // 동적 버튼 생성
            CreateChoiceButtons(choices);

            // 팝업 표시
            ShowPopup();

            Debug.Log($"[EventPopup] 커스텀 선택 이벤트 표시: {title} (선택지 {choices.Count}개)");
        }
    }
}