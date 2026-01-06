// =============================================================================
// TargetSelectionPanel.cs
// 카드 효과 대상 선택 UI 패널
// =============================================================================
//
// [역할]
// - 플레이어가 카드 효과의 대상(손패의 카드)을 선택하는 UI
// - 정산, 소각, 정화 등 대상 선택이 필요한 효과에서 사용
//
// [화면 구성]
// ┌─────────────────────────────────────────────────────────────┐
// │                     [안내 텍스트]                           │
// │               "정산할 재화 카드를 선택하세요"                │
// │                  (최대 1장 선택 가능)                       │
// │                                                             │
// │  ┌───────┐  ┌───────┐  ┌───────┐  ┌───────┐              │
// │  │ 카드1 │  │ 카드2 │  │ 카드3 │  │ 카드4 │  ...         │
// │  │  ✓   │  │       │  │       │  │       │              │
// │  └───────┘  └───────┘  └───────┘  └───────┘              │
// │                                                             │
// │           [선택 완료]          [취소]                       │
// └─────────────────────────────────────────────────────────────┘
//
// [사용 흐름]
// 1. EffectSystem이 OnTargetSelectionRequired 이벤트 발생
// 2. 이 패널이 이벤트 수신 → Show() 호출
// 3. 플레이어가 카드 선택
// 4. "선택 완료" 클릭 → 콜백으로 선택된 카드 전달
//
// [수정 이력]
// - 2025-12-30: 옵션 B 적용
//   - panelRoot 대신 자기 자신(gameObject) On/Off
//   - 이벤트 구독을 Awake()/OnDestroy()에서 관리
//   - TMP_Text → TextMeshProUGUI 버그 수정
//
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 대상 선택 패널
    /// </summary>
    public class TargetSelectionPanel : MonoBehaviour
    {
        // =====================================================================
        // Inspector 연결
        // =====================================================================

        [Header("패널 요소")]
        [SerializeField] private Image dimBackground;           // 배경 딤 처리
        [SerializeField] private TMP_Text instructionText;      // 안내 텍스트
        [SerializeField] private TMP_Text selectionCountText;   // 선택 수 표시

        [Header("카드 표시 영역")]
        [SerializeField] private Transform cardContainer;       // 카드들의 부모
        [SerializeField] private GameObject cardPrefab;         // 카드 프리팹 (CardView)

        [Header("버튼")]
        [SerializeField] private Button confirmButton;          // 선택 완료 버튼
        [SerializeField] private Button cancelButton;           // 취소 버튼
        [SerializeField] private TMP_Text confirmButtonText;    // 버튼 텍스트

        [Header("선택 표시 색상")]
        [SerializeField] private Color selectedBorderColor = new Color(0f, 1f, 0.5f);  // 선택됨: 녹색
        [SerializeField] private Color normalBorderColor = new Color(0.5f, 0.5f, 0.5f); // 기본: 회색

        // =====================================================================
        // 상태
        // =====================================================================

        private List<CardInstance> selectableCards;         // 선택 가능한 카드 목록
        private List<CardInstance> selectedCards;           // 현재 선택된 카드들
        private int maxSelections;                          // 최대 선택 수
        private Action<List<CardInstance>> onCompleteCallback;  // 완료 콜백

        private List<SelectableCardView> cardViews;         // 생성된 카드 뷰들

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            selectedCards = new List<CardInstance>();
            cardViews = new List<SelectableCardView>();

            // 버튼 이벤트 연결
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            // ★ 이벤트 구독 (Awake에서 처리 - GameObject 활성 상태와 무관)
            EffectSystem.OnTargetSelectionRequired += OnTargetSelectionRequired;
            Debug.Log("[TargetSelectionPanel] Awake - 이벤트 구독 완료");

            // ★ 초기 상태: 자기 자신 비활성화
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            // ★ 이벤트 구독 해제 (파괴 시)
            EffectSystem.OnTargetSelectionRequired -= OnTargetSelectionRequired;
            Debug.Log("[TargetSelectionPanel] OnDestroy - 이벤트 구독 해제");
        }

        // =====================================================================
        // 이벤트 핸들러
        // =====================================================================

        /// <summary>
        /// EffectSystem에서 대상 선택 요청 시 호출
        /// </summary>
        private void OnTargetSelectionRequired(
            TargetType targetType,
            int maxTargets,
            List<CardInstance> cards,
            Action<List<CardInstance>> callback)
        {
            Debug.Log($"[TargetSelectionPanel] 이벤트 수신: {targetType}, {cards?.Count}장");
            Show(targetType, maxTargets, cards, callback);
        }

        // =====================================================================
        // 표시/숨김
        // =====================================================================

        /// <summary>
        /// 패널 표시
        /// </summary>
        public void Show(
            TargetType targetType,
            int maxTargets,
            List<CardInstance> cards,
            Action<List<CardInstance>> callback)
        {
            if (cards == null || cards.Count == 0)
            {
                Debug.LogWarning("[TargetSelectionPanel] 선택 가능한 카드 없음");
                callback?.Invoke(new List<CardInstance>());
                return;
            }

            selectableCards = cards;
            maxSelections = maxTargets > 0 ? maxTargets : cards.Count;
            onCompleteCallback = callback;
            selectedCards.Clear();

            // ★ 자기 자신 활성화 (DimBackground 포함 모든 자식 활성화)
            gameObject.SetActive(true);

            // 안내 텍스트 설정
            SetInstructionText(targetType, maxSelections);

            // 카드 생성
            CreateCardViews();

            // 버튼 상태 갱신
            UpdateUI();

            Debug.Log($"[TargetSelectionPanel] 표시: {targetType}, 최대 {maxSelections}장 선택");
        }

        /// <summary>
        /// 패널 숨김
        /// </summary>
        public void Hide()
        {
            // 카드 뷰 정리
            ClearCardViews();

            // 상태 초기화
            selectableCards = null;
            selectedCards.Clear();
            onCompleteCallback = null;

            // ★ 자기 자신 비활성화
            gameObject.SetActive(false);

            Debug.Log("[TargetSelectionPanel] 숨김");
        }

        // =====================================================================
        // 안내 텍스트
        // =====================================================================

        /// <summary>
        /// 대상 타입에 따른 안내 텍스트 설정
        /// </summary>
        private void SetInstructionText(TargetType targetType, int maxTargets)
        {
            if (instructionText == null) return;

            string targetName = GetTargetTypeName(targetType);
            string countText = maxTargets == 1 ? "1장" : $"최대 {maxTargets}장";

            instructionText.text = $"{targetName}을(를) 선택하세요\n({countText} 선택 가능)";
        }

        /// <summary>
        /// 대상 타입 한글명
        /// </summary>
        private string GetTargetTypeName(TargetType targetType)
        {
            switch (targetType)
            {
                case TargetType.HandCard:
                    return "카드";
                case TargetType.HandTreasure:
                    return "재화 카드";
                case TargetType.HandPollution:
                    return "오염 카드";
                case TargetType.HandAction:
                    return "액션 카드";
                default:
                    return "대상";
            }
        }

        // =====================================================================
        // 카드 뷰 생성/정리
        // =====================================================================

        /// <summary>
        /// 선택 가능한 카드들의 뷰 생성
        /// </summary>
        private void CreateCardViews()
        {
            ClearCardViews();

            if (cardPrefab == null || cardContainer == null)
            {
                Debug.LogError("[TargetSelectionPanel] cardPrefab 또는 cardContainer가 null");
                return;
            }

            foreach (var card in selectableCards)
            {
                // 카드 프리팹 인스턴스화
                var cardObj = Instantiate(cardPrefab, cardContainer);

                // CardView 컴포넌트로 데이터 바인딩
                var cardView = cardObj.GetComponent<CardView>();
                if (cardView != null)
                {
                    cardView.Bind(card);
                }

                // SelectableCardView 컴포넌트 추가
                var selectableView = cardObj.AddComponent<SelectableCardView>();
                selectableView.Initialize(card, selectedBorderColor, normalBorderColor);
                selectableView.OnSelectionChanged += OnCardSelectionChanged;

                cardViews.Add(selectableView);
            }

            Debug.Log($"[TargetSelectionPanel] 카드 뷰 {cardViews.Count}개 생성");
        }

        /// <summary>
        /// 카드 뷰 정리
        /// </summary>
        private void ClearCardViews()
        {
            foreach (var view in cardViews)
            {
                if (view != null)
                {
                    view.OnSelectionChanged -= OnCardSelectionChanged;
                    Destroy(view.gameObject);
                }
            }
            cardViews.Clear();
        }

        // =====================================================================
        // 선택 처리
        // =====================================================================

        /// <summary>
        /// 카드 선택 상태 변경 시
        /// </summary>
        private void OnCardSelectionChanged(SelectableCardView cardView, bool isSelected)
        {
            if (isSelected)
            {
                // 선택 추가
                if (!selectedCards.Contains(cardView.BoundCard))
                {
                    // 최대 선택 수 체크
                    if (selectedCards.Count >= maxSelections)
                    {
                        // 가장 먼저 선택한 것 해제
                        if (selectedCards.Count > 0)
                        {
                            var firstSelected = selectedCards[0];
                            selectedCards.RemoveAt(0);

                            // 해당 카드 뷰 선택 해제
                            var firstView = cardViews.Find(v => v.BoundCard == firstSelected);
                            firstView?.SetSelected(false, silent: true);
                        }
                    }

                    selectedCards.Add(cardView.BoundCard);
                }
            }
            else
            {
                // 선택 해제
                selectedCards.Remove(cardView.BoundCard);
            }

            UpdateUI();
        }

        /// <summary>
        /// UI 갱신 (선택 수, 버튼 상태)
        /// </summary>
        private void UpdateUI()
        {
            // 선택 수 표시
            if (selectionCountText != null)
            {
                selectionCountText.text = $"{selectedCards.Count} / {maxSelections}";
            }

            // 확인 버튼 상태
            if (confirmButton != null)
            {
                // 최소 1장 이상 선택해야 활성화
                confirmButton.interactable = selectedCards.Count > 0;
            }

            // 버튼 텍스트
            if (confirmButtonText != null)
            {
                confirmButtonText.text = selectedCards.Count > 0
                    ? $"선택 완료 ({selectedCards.Count}장)"
                    : "선택 완료";
            }
        }

        // =====================================================================
        // 버튼 이벤트
        // =====================================================================

        /// <summary>
        /// 선택 완료 클릭
        /// </summary>
        private void OnConfirmClicked()
        {
            if (selectedCards.Count == 0)
            {
                Debug.LogWarning("[TargetSelectionPanel] 선택된 카드 없음");
                return;
            }

            Debug.Log($"[TargetSelectionPanel] 선택 완료: {selectedCards.Count}장");

            // 콜백 호출
            var callback = onCompleteCallback;
            var selected = new List<CardInstance>(selectedCards);

            // 패널 숨김
            Hide();

            // 콜백 실행
            callback?.Invoke(selected);
        }

        /// <summary>
        /// 취소 클릭
        /// </summary>
        private void OnCancelClicked()
        {
            Debug.Log("[TargetSelectionPanel] 취소");

            var callback = onCompleteCallback;

            // 패널 숨김
            Hide();

            // 빈 목록으로 콜백 (효과 스킵)
            callback?.Invoke(new List<CardInstance>());
        }
    }

    // =========================================================================
    // SelectableCardView - 선택 가능한 카드 뷰 컴포넌트
    // =========================================================================

    /// <summary>
    /// 선택 가능한 카드 뷰
    /// 카드 프리팹에 동적으로 추가되어 선택 기능 제공
    /// </summary>
    public class SelectableCardView : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        // 데이터
        public CardInstance BoundCard { get; private set; }
        public bool IsSelected { get; private set; }

        // 색상
        private Color selectedColor;
        private Color normalColor;

        // 테두리 이미지 (선택 표시용)
        private Image borderImage;
        private GameObject selectionIndicator;

        // 이벤트
        public event Action<SelectableCardView, bool> OnSelectionChanged;

        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize(CardInstance card, Color selectedBorder, Color normalBorder)
        {
            BoundCard = card;
            selectedColor = selectedBorder;
            normalColor = normalBorder;
            IsSelected = false;

            // 테두리 이미지 찾기 (CardView 구조 참조)
            borderImage = transform.Find("Border")?.GetComponent<Image>();

            // 선택 표시 인디케이터 생성
            CreateSelectionIndicator();

            // 초기 상태
            UpdateVisual();
        }

        /// <summary>
        /// 선택 표시용 인디케이터 생성
        /// </summary>
        private void CreateSelectionIndicator()
        {
            // 기존 인디케이터 확인
            var existing = transform.Find("SelectionIndicator");
            if (existing != null)
            {
                selectionIndicator = existing.gameObject;
                return;
            }

            // 새로 생성 - 체크마크 또는 테두리 강조
            selectionIndicator = new GameObject("SelectionIndicator");
            selectionIndicator.transform.SetParent(transform, false);

            // RectTransform 설정 (카드 전체 덮음)
            var rect = selectionIndicator.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 테두리 이미지
            var image = selectionIndicator.AddComponent<Image>();
            image.color = new Color(selectedColor.r, selectedColor.g, selectedColor.b, 0.3f);
            image.raycastTarget = false;

            // 체크마크 텍스트 추가
            var checkObj = new GameObject("CheckMark");
            checkObj.transform.SetParent(selectionIndicator.transform, false);

            var checkRect = checkObj.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(50, 50);

            // ★ 수정: TMP_Text → TextMeshProUGUI (TMP_Text는 추상 클래스)
            var checkText = checkObj.AddComponent<TextMeshProUGUI>();
            checkText.text = "✓";
            checkText.fontSize = 36;
            checkText.alignment = TextAlignmentOptions.Center;
            checkText.color = Color.white;

            selectionIndicator.SetActive(false);
        }

        /// <summary>
        /// 선택 상태 설정
        /// </summary>
        /// <param name="selected">선택 여부</param>
        /// <param name="silent">이벤트 발생 안 함</param>
        public void SetSelected(bool selected, bool silent = false)
        {
            if (IsSelected == selected) return;

            IsSelected = selected;
            UpdateVisual();

            if (!silent)
            {
                OnSelectionChanged?.Invoke(this, IsSelected);
            }
        }

        /// <summary>
        /// 시각 상태 갱신
        /// </summary>
        private void UpdateVisual()
        {
            // 테두리 색상
            if (borderImage != null)
            {
                borderImage.color = IsSelected ? selectedColor : normalColor;
            }

            // 선택 인디케이터
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(IsSelected);
            }
        }

        /// <summary>
        /// 클릭 처리
        /// </summary>
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
            {
                // 토글
                SetSelected(!IsSelected);
            }
        }
    }
}