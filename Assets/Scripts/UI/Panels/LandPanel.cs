// =============================================================================
// LandPanel.cs
// 사유지(Land) 시스템 시각화 패널
// =============================================================================
// [R8-6 신규] 2026-01-03
// - HousePanel과 일관된 구조
// - 가로 스크롤 지원 (땅 개수 제한 없음)
// [R9 수정] 2026-01-03
// - OnPhaseChanged 이벤트 구독 추가 (페이즈 변경 시 버튼 상태 갱신)
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 사유지 시스템 패널
    /// LandCard들을 가로로 배치, 스크롤로 모든 땅 접근 가능
    /// 
    /// [Unity 설정 - 스크롤 구조]
    /// LandPanel (이 스크립트)
    /// └── ScrollRect 컴포넌트
    ///     ├── Viewport (Mask)
    ///     │   └── Container (Content, Horizontal Layout Group)
    ///     │       └── LandCard들...
    ///     └── Scrollbar (선택)
    /// </summary>
    public class LandPanel : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("설정")]
        [SerializeField] private Transform container;           // Content (Horizontal Layout Group)
        [SerializeField] private GameObject landCardPrefab;     // LandCard 프리팹
        [SerializeField] private ScrollRect scrollRect;         // 스크롤 (선택, 없으면 스크롤 없이 동작)

        [Header("카드 설정")]
        [SerializeField] private float cardWidth = 180f;        // 카드 너비
        [SerializeField] private float cardSpacing = 15f;       // 카드 간격

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private List<LandCard> landCards = new List<LandCard>();

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void OnEnable()
        {
            // LandSystem 이벤트 구독
            LandSystem.OnLandAcquired += OnLandAcquired;
            LandSystem.OnLandDeveloped += OnLandDeveloped;

            // 게임/턴 이벤트 구독
            GameManager.OnGameStarted += OnGameStarted;
            TurnManager.OnTurnStarted += OnTurnStarted;

            // [R9 추가] 페이즈 변경 이벤트 구독
            TurnManager.OnPhaseChanged += OnPhaseChanged;

            // 골드 변경 시 개발 버튼 상태 갱신
            GoldSystem.OnGoldChanged += OnGoldChanged;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            LandSystem.OnLandAcquired -= OnLandAcquired;
            LandSystem.OnLandDeveloped -= OnLandDeveloped;

            GameManager.OnGameStarted -= OnGameStarted;
            TurnManager.OnTurnStarted -= OnTurnStarted;

            // [R9 추가] 페이즈 변경 이벤트 구독 해제
            TurnManager.OnPhaseChanged -= OnPhaseChanged;

            GoldSystem.OnGoldChanged -= OnGoldChanged;
        }

        private void Start()
        {
            RefreshAll();
        }

        // =====================================================================
        // 이벤트 핸들러
        // =====================================================================

        private void OnLandAcquired(LandInstance land)
        {
            RefreshAll();
        }

        private void OnLandDeveloped(LandInstance land, LandType newType, int newLevel)
        {
            RefreshLand(land.landId);
        }

        private void OnGameStarted()
        {
            RefreshAll();
        }

        private void OnTurnStarted(int turn)
        {
            RefreshAll();
        }

        /// <summary>
        /// [R9 추가] 페이즈 변경 시 버튼 상태 갱신
        /// 덱 페이즈로 변경되면 개발 버튼 비활성화
        /// 구매 페이즈로 변경되면 개발 버튼 활성화 (골드 충분 시)
        /// </summary>
        private void OnPhaseChanged(GamePhase phase)
        {
            // 개발 버튼 상태만 갱신 (전체 RefreshAll보다 가벼움)
            foreach (var card in landCards)
            {
                card?.RefreshDevelopButton();
            }

            Debug.Log($"[LandPanel] 페이즈 변경: {phase} - 개발 버튼 갱신");
        }

        private void OnGoldChanged(int oldGold, int newGold, int delta)
        {
            // 개발 버튼 활성화 상태 갱신
            foreach (var card in landCards)
            {
                card?.RefreshDevelopButton();
            }
        }

        // =====================================================================
        // UI 갱신
        // =====================================================================

        /// <summary>
        /// 전체 사유지 목록 갱신
        /// </summary>
        public void RefreshAll()
        {
            if (container == null || landCardPrefab == null) return;

            var state = GameManager.Instance?.State;
            if (state == null) return;

            // 기존 카드 제거
            foreach (var card in landCards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            landCards.Clear();

            // 모든 사유지 생성 (제한 없음)
            foreach (var land in state.lands)
            {
                var obj = Instantiate(landCardPrefab, container);
                var card = obj.GetComponent<LandCard>();
                if (card != null)
                {
                    card.Bind(land);
                    landCards.Add(card);
                }
            }

            // Content 크기 조정 (스크롤용)
            UpdateContentSize();

            Debug.Log($"[LandPanel] 사유지 {landCards.Count}개 표시");
        }

        /// <summary>
        /// 개별 사유지 갱신
        /// </summary>
        public void RefreshLand(string landId)
        {
            var card = landCards.Find(c => c.BoundLand?.landId == landId);
            card?.Refresh();
        }

        // =====================================================================
        // Content 크기 조정 (스크롤용)
        // =====================================================================

        /// <summary>
        /// Container(Content)의 크기를 카드 개수에 맞게 조정
        /// </summary>
        private void UpdateContentSize()
        {
            if (container == null) return;

            var rectTransform = container.GetComponent<RectTransform>();
            if (rectTransform == null) return;

            int count = landCards.Count;
            if (count == 0) return;

            // 총 너비 = (카드 너비 × 개수) + (간격 × (개수-1)) + 양쪽 패딩
            float totalWidth = (cardWidth * count) + (cardSpacing * (count - 1)) + 20f;

            // Content 크기 설정
            rectTransform.sizeDelta = new Vector2(totalWidth, rectTransform.sizeDelta.y);

            // 새 땅 추가 시 스크롤을 맨 오른쪽으로 (최신 땅 보이게)
            if (scrollRect != null)
            {
                StartCoroutine(ScrollToEndNextFrame());
            }
        }

        private System.Collections.IEnumerator ScrollToEndNextFrame()
        {
            yield return null;
            if (scrollRect != null)
            {
                scrollRect.horizontalNormalizedPosition = 1f; // 맨 오른쪽
            }
        }

        // =====================================================================
        // 외부 접근
        // =====================================================================

        /// <summary>
        /// 특정 사유지 카드 찾기
        /// </summary>
        public LandCard GetLandCard(string landId)
        {
            return landCards.Find(c => c.BoundLand?.landId == landId);
        }

        /// <summary>
        /// 모든 사유지 카드
        /// </summary>
        public IReadOnlyList<LandCard> AllLandCards => landCards.AsReadOnly();
    }
}