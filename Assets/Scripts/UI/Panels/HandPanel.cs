// =============================================================================
// HandPanel.cs
// 손패 영역 관리
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
    /// 손패 패널
    /// 손패의 카드들을 표시하고 관리
    /// 
    /// [역할]
    /// - DeckSystem 이벤트 구독하여 손패 변화 감지
    /// - CardView 프리팹으로 카드 UI 생성/삭제
    /// - 카드 정렬 및 레이아웃 관리
    /// - 덱/버림더미 장수 표시
    /// </summary>
    public class HandPanel : MonoBehaviour
    {
        // =====================================================================
        // Inspector 연결
        // =====================================================================

        [Header("카드 영역")]
        [SerializeField] private Transform cardContainer;   // 카드들이 생성될 부모
        [SerializeField] private GameObject cardPrefab;     // CardView 프리팹

        [Header("덱/버림더미 표시")]
        [SerializeField] private TMP_Text deckCountText;    // "23"
        [SerializeField] private TMP_Text discardCountText; // "5"

        [Header("손패 카운터")]
        [SerializeField] private TMP_Text handCountText;    // "손패 7/10"

        [Header("색상 설정")]
        [SerializeField] private Color normalHandColor = Color.white;
        [SerializeField] private Color warningHandColor = new Color(1f, 0.6f, 0f);  // 주황 (8~9장)
        [SerializeField] private Color dangerHandColor = Color.red;                  // 빨강 (10장+)

        [Header("레이아웃 설정")]
        [SerializeField] private float cardSpacing = 10f;   // 카드 간 간격
        [SerializeField] private float maxCardWidth = 100f; // 카드 최대 너비 (겹침 방지)

        // =====================================================================
        // 내부 상태
        // =====================================================================

        /// <summary>현재 표시 중인 CardView 목록</summary>
        private List<CardView> cardViews = new List<CardView>();

        /// <summary>인스턴스ID → CardView 매핑 (빠른 검색용)</summary>
        private Dictionary<string, CardView> cardViewMap = new Dictionary<string, CardView>();

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

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
            // 초기 상태 표시
            RefreshAll();

            // 카드 클릭 이벤트 구독
            CardView.OnCardClicked += OnCardClicked;
        }

        private void OnDestroy()
        {
            CardView.OnCardClicked -= OnCardClicked;
        }

        // =====================================================================
        // 이벤트 구독
        // =====================================================================

        private void SubscribeToEvents()
        {
            // 손패 변경
            DeckSystem.OnHandChanged += OnHandChanged;

            // 덱 변경
            DeckSystem.OnDeckChanged += OnDeckChanged;

            // 버림더미 변경
            DeckSystem.OnDiscardChanged += OnDiscardChanged;

            // 카드 플레이됨
            DeckSystem.OnCardPlayed += OnCardPlayed;

            // 액션 변경 (플레이 가능 상태 갱신용)
            TurnManager.OnActionsChanged += OnActionsChanged;

            // 페이즈 변경 (플레이 가능 상태 갱신용)
            TurnManager.OnPhaseChanged += OnPhaseChanged;

            // 게임 시작
            GameManager.OnGameStarted += OnGameStarted;
        }

        private void UnsubscribeFromEvents()
        {
            DeckSystem.OnHandChanged -= OnHandChanged;
            DeckSystem.OnDeckChanged -= OnDeckChanged;
            DeckSystem.OnDiscardChanged -= OnDiscardChanged;
            DeckSystem.OnCardPlayed -= OnCardPlayed;
            TurnManager.OnActionsChanged -= OnActionsChanged;
            TurnManager.OnPhaseChanged -= OnPhaseChanged;
            GameManager.OnGameStarted -= OnGameStarted;
        }

        // =====================================================================
        // 이벤트 핸들러
        // =====================================================================

        private void OnHandChanged()
        {
            RefreshHand();
        }

        private void OnDeckChanged(int count)
        {
            UpdateDeckCount(count);
        }

        private void OnDiscardChanged(int count)
        {
            UpdateDiscardCount(count);
        }

        private void OnCardPlayed(CardInstance card)
        {
            // 손패에서 해당 카드 제거는 OnHandChanged에서 처리됨
            // 여기서는 추가 효과(애니메이션 등) 가능
        }

        private void OnActionsChanged(int oldValue, int newValue)
        {
            // 액션 변경 시 모든 카드 플레이 가능 상태 갱신
            UpdateAllPlayableStates();
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            // 페이즈 변경 시 모든 카드 플레이 가능 상태 갱신
            UpdateAllPlayableStates();
        }

        private void OnGameStarted()
        {
            RefreshAll();
        }

        // =====================================================================
        // 카드 클릭 처리
        // =====================================================================

        /// <summary>
        /// 카드 클릭 시 처리
        /// </summary>
        private void OnCardClicked(CardView cardView)
        {
            if (cardView == null || cardView.BoundCard == null) return;

            var card = cardView.BoundCard;
            var cardData = cardView.BoundCardData;

            // 오염 카드: 플레이 불가 피드백
            if (cardData?.cardType == CardType.Pollution)
            {
                Debug.Log("[HandPanel] 오염 카드는 플레이할 수 없습니다.");
                // TODO: UI 피드백 (흔들림 효과, 툴팁 등)
                return;
            }

            // 재화 카드: 자동 정산 안내
            if (cardData?.cardType == CardType.Treasure)
            {
                Debug.Log("[HandPanel] 재화 카드는 플레이 종료 시 자동 정산됩니다.");
                // TODO: UI 피드백
                return;
            }

            // 액션 카드: 플레이 시도
            if (cardData?.cardType == CardType.Action)
            {
                if (DeckSystem.Instance.CanPlayCard(card))
                {
                    bool success = DeckSystem.Instance.PlayCard(card);
                    if (success)
                    {
                        Debug.Log($"[HandPanel] 카드 플레이: {cardData.cardName}");
                    }
                }
                else
                {
                    Debug.Log("[HandPanel] 액션이 부족합니다.");
                    // TODO: UI 피드백
                }
            }
        }

        // =====================================================================
        // UI 갱신
        // =====================================================================

        /// <summary>
        /// 전체 갱신
        /// </summary>
        public void RefreshAll()
        {
            RefreshHand();
            
            if (DeckSystem.Instance != null)
            {
                UpdateDeckCount(DeckSystem.Instance.DeckCount);
                UpdateDiscardCount(DeckSystem.Instance.DiscardCount);
            }
        }

        /// <summary>
        /// 손패 갱신 (카드 재생성)
        /// </summary>
        public void RefreshHand()
        {
            var hand = DeckSystem.Instance?.Hand;
            if (hand == null) return;

            // 기존 카드뷰 정리
            ClearCardViews();

            // 새 카드뷰 생성
            foreach (var cardInstance in hand)
            {
                CreateCardView(cardInstance);
            }

            // 손패 카운터 갱신
            UpdateHandCount(hand.Count);

            // 레이아웃 재정렬
            ArrangeCards();
        }

        /// <summary>
        /// 카드뷰 생성
        /// </summary>
        private CardView CreateCardView(CardInstance cardInstance)
        {
            if (cardPrefab == null || cardContainer == null)
            {
                Debug.LogWarning("[HandPanel] cardPrefab 또는 cardContainer가 없습니다.");
                return null;
            }

            // 프리팹 인스턴스화
            GameObject cardObj = Instantiate(cardPrefab, cardContainer);
            CardView cardView = cardObj.GetComponent<CardView>();

            if (cardView == null)
            {
                Debug.LogError("[HandPanel] cardPrefab에 CardView 컴포넌트가 없습니다.");
                Destroy(cardObj);
                return null;
            }

            // 데이터 바인딩
            cardView.Bind(cardInstance);

            // 목록에 추가
            cardViews.Add(cardView);
            cardViewMap[cardInstance.instanceId] = cardView;

            return cardView;
        }

        /// <summary>
        /// 모든 카드뷰 제거
        /// </summary>
        private void ClearCardViews()
        {
            foreach (var cardView in cardViews)
            {
                if (cardView != null)
                {
                    Destroy(cardView.gameObject);
                }
            }

            cardViews.Clear();
            cardViewMap.Clear();
        }

        /// <summary>
        /// 카드 정렬 (Horizontal Layout Group 사용 시 자동)
        /// </summary>
        private void ArrangeCards()
        {
            // Horizontal Layout Group 사용 시 자동 정렬
            // 수동 정렬이 필요하면 여기서 처리
            
            // 카드가 많아서 겹칠 때 간격 조정 (선택적)
            var layoutGroup = cardContainer?.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null && cardViews.Count > 0)
            {
                // 컨테이너 너비 계산
                RectTransform containerRect = cardContainer as RectTransform;
                if (containerRect != null)
                {
                    float containerWidth = containerRect.rect.width;
                    float totalCardWidth = cardViews.Count * maxCardWidth;
                    
                    if (totalCardWidth > containerWidth)
                    {
                        // 카드가 많으면 간격 줄이기
                        float availableSpace = containerWidth - (cardViews.Count * 80f); // 최소 카드 너비 80
                        float newSpacing = Mathf.Max(-30f, availableSpace / (cardViews.Count - 1));
                        layoutGroup.spacing = newSpacing;
                    }
                    else
                    {
                        layoutGroup.spacing = cardSpacing;
                    }
                }
            }
        }

        /// <summary>
        /// 모든 카드 플레이 가능 상태 갱신
        /// </summary>
        private void UpdateAllPlayableStates()
        {
            foreach (var cardView in cardViews)
            {
                cardView?.UpdatePlayableState();
            }
        }

        // =====================================================================
        // 카운터 갱신
        // =====================================================================

        /// <summary>
        /// 덱 장수 갱신
        /// </summary>
        private void UpdateDeckCount(int count)
        {
            if (deckCountText != null)
            {
                deckCountText.text = count.ToString();
            }
        }

        /// <summary>
        /// 버림더미 장수 갱신
        /// </summary>
        private void UpdateDiscardCount(int count)
        {
            if (discardCountText != null)
            {
                discardCountText.text = count.ToString();
            }
        }

        /// <summary>
        /// 손패 카운터 갱신
        /// </summary>
        private void UpdateHandCount(int count)
        {
            if (handCountText == null) return;

            handCountText.text = $"손패 {count}/{GameConfig.MaxHandSize}";

            // 색상 변경 (8장 이상 경고)
            if (count >= GameConfig.MaxHandSize)
            {
                handCountText.color = dangerHandColor;
            }
            else if (count >= GameConfig.MaxHandSize - 2)
            {
                handCountText.color = warningHandColor;
            }
            else
            {
                handCountText.color = normalHandColor;
            }
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        /// <summary>
        /// 특정 카드 인스턴스의 CardView 찾기
        /// </summary>
        public CardView GetCardView(string instanceId)
        {
            cardViewMap.TryGetValue(instanceId, out CardView cardView);
            return cardView;
        }

        /// <summary>
        /// 현재 손패 카드 수
        /// </summary>
        public int CardCount => cardViews.Count;

        // =====================================================================
        // 디버그
        // =====================================================================

        [ContextMenu("Refresh Hand")]
        private void DebugRefreshHand()
        {
            RefreshHand();
            Debug.Log($"[HandPanel] 손패 갱신 완료 ({cardViews.Count}장)");
        }

        [ContextMenu("Log Hand Info")]
        private void DebugLogHandInfo()
        {
            Debug.Log($"[HandPanel] 손패 {cardViews.Count}장:");
            foreach (var cardView in cardViews)
            {
                var data = cardView.BoundCardData;
                Debug.Log($"  - {data?.cardName} ({data?.cardType})");
            }
        }
    }
}
