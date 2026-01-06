// =============================================================================
// HousePanel.cs
// 집(House) 시스템 시각화 패널 (UnitPanel 대체)
// =============================================================================
// [R8-7 수정] 2026-01-03
// - RelocatePopup.OnRelocateCompleted 이벤트 구독 추가
// [R9 수정] 2026-01-04
// - ScrollView 방식으로 변경 (스케일 조정 제거)
// - 가로 스크롤로 모든 집 접근 가능
// =============================================================================
//
// [Unity 설정 가이드 - ScrollView 구조]
// 
// HousePanel (이 스크립트)
// └── ScrollRect 컴포넌트
//     ├── Viewport (Mask 컴포넌트)
//     │   └── Container (Content, Horizontal Layout Group)
//     │       └── HouseCard들...
//     └── Scrollbar Horizontal (선택)
//
// 1. HousePanel에 ScrollRect 컴포넌트 추가:
//    - Horizontal: ON
//    - Vertical: OFF
//    - Movement Type: Elastic
//    - Viewport: Viewport 오브젝트
//    - Content: Container 오브젝트
//
// 2. Viewport 설정:
//    - Mask 컴포넌트 추가
//    - RectTransform: 원하는 표시 영역 크기
//
// 3. Container에 Horizontal Layout Group 추가:
//    - Child Alignment: Middle Left
//    - Spacing: 15
//    - Child Force Expand: Width OFF, Height ON
//
// 4. Container에 Content Size Fitter 추가:
//    - Horizontal Fit: Preferred Size
//    - Vertical Fit: Unconstrained (또는 Preferred Size)
//
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 집 시스템 패널
    /// ScrollView로 가로 스크롤 지원
    /// </summary>
    public class HousePanel : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private Transform container;           // Content (Horizontal Layout Group)
        [SerializeField] private GameObject houseCardPrefab;    // HouseCard 프리팹
        [SerializeField] private ScrollRect scrollRect;         // 스크롤뷰

        [Header("카드 설정")]
        [SerializeField] private float cardWidth = 220f;        // 카드 너비
        [SerializeField] private float cardSpacing = 15f;       // 카드 간격

        private List<HouseCard> houseCards = new List<HouseCard>();

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void OnEnable()
        {
            // HouseSystem 이벤트
            HouseSystem.OnHouseCreated += OnHouseChanged;
            HouseSystem.OnHouseRemoved += OnHouseChanged;
            HouseSystem.OnUnitPlaced += OnUnitChanged;
            HouseSystem.OnUnitRemoved += OnUnitRemoved;
            HouseSystem.OnPregnancyStarted += OnHouseStateChanged;
            HouseSystem.OnPregnancyCancelled += OnHouseStateChanged;
            HouseSystem.OnBirth += OnBirth;

            // BreedingSystem 이벤트
            BreedingSystem.OnBreedingConditionMet += OnHouseStateChanged;
            BreedingSystem.OnChildBorn += OnChildBorn;

            // 기타 이벤트
            TurnManager.OnTurnStarted += OnTurnStarted;
            GameManager.OnGameStarted += OnGameStarted;
            UnitSystem.OnUnitLeveledUp += OnUnitLeveledUp;

            // [R8-7] RelocatePopup 이벤트 구독
            RelocatePopup.OnRelocateCompleted += OnRelocateCompleted;
        }

        private void OnDisable()
        {
            HouseSystem.OnHouseCreated -= OnHouseChanged;
            HouseSystem.OnHouseRemoved -= OnHouseChanged;
            HouseSystem.OnUnitPlaced -= OnUnitChanged;
            HouseSystem.OnUnitRemoved -= OnUnitRemoved;
            HouseSystem.OnPregnancyStarted -= OnHouseStateChanged;
            HouseSystem.OnPregnancyCancelled -= OnHouseStateChanged;
            HouseSystem.OnBirth -= OnBirth;

            BreedingSystem.OnBreedingConditionMet -= OnHouseStateChanged;
            BreedingSystem.OnChildBorn -= OnChildBorn;

            TurnManager.OnTurnStarted -= OnTurnStarted;
            GameManager.OnGameStarted -= OnGameStarted;
            UnitSystem.OnUnitLeveledUp -= OnUnitLeveledUp;

            // [R8-7] RelocatePopup 이벤트 구독 해제
            RelocatePopup.OnRelocateCompleted -= OnRelocateCompleted;
        }

        private void Start()
        {
            RefreshAll();
        }

        // =====================================================================
        // 이벤트 핸들러
        // =====================================================================

        private void OnHouseChanged(HouseInstance house) => RefreshAll();
        private void OnUnitChanged(HouseInstance house, UnitInstance unit, HouseSlotType slot) => RefreshHouse(house.houseId);
        private void OnUnitRemoved(HouseInstance house, UnitInstance unit) => RefreshHouse(house.houseId);
        private void OnHouseStateChanged(HouseInstance house) => RefreshHouse(house.houseId);
        private void OnBirth(HouseInstance house, UnitInstance child) => RefreshHouse(house.houseId);
        private void OnChildBorn(HouseInstance house, UnitInstance a, UnitInstance b, UnitInstance child) => RefreshHouse(house.houseId);
        private void OnTurnStarted(int turn) => RefreshAll();
        private void OnGameStarted() => RefreshAll();
        private void OnUnitLeveledUp(UnitInstance unit, int level, CardInstance card)
        {
            if (!string.IsNullOrEmpty(unit.houseId))
                RefreshHouse(unit.houseId);
        }

        /// <summary>
        /// [R8-7] 재배치 완료 시 전체 갱신
        /// </summary>
        private void OnRelocateCompleted(UnitInstance unit, HouseInstance house, HouseSlotType slot)
        {
            Debug.Log($"[HousePanel] 재배치 완료 감지: {unit.unitName} → {house.houseName}");
            RefreshAll();
        }

        // =====================================================================
        // UI 갱신
        // =====================================================================

        /// <summary>
        /// 전체 집 목록 갱신
        /// </summary>
        public void RefreshAll()
        {
            if (container == null || houseCardPrefab == null) return;

            var state = GameManager.Instance?.State;
            if (state == null) return;

            // 기존 카드 제거
            foreach (var card in houseCards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            houseCards.Clear();

            // 모든 집 생성 (최대 12개)
            int count = 0;
            foreach (var house in state.houses)
            {
                if (count >= GameConfig.MaxHouses) break;

                var obj = Instantiate(houseCardPrefab, container);
                var card = obj.GetComponent<HouseCard>();
                if (card != null)
                {
                    card.Bind(house);
                    houseCards.Add(card);
                }
                count++;
            }

            // Content 크기 조정 (스크롤용)
            UpdateContentSize();

            Debug.Log($"[HousePanel] 집 {houseCards.Count}개 표시");
        }

        /// <summary>
        /// 개별 집 갱신
        /// </summary>
        public void RefreshHouse(string houseId)
        {
            var card = houseCards.Find(c => c.BoundHouse?.houseId == houseId);
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

            int count = houseCards.Count;
            if (count == 0) return;

            // 총 너비 = (카드 너비 × 개수) + (간격 × (개수-1)) + 양쪽 패딩
            float totalWidth = (cardWidth * count) + (cardSpacing * (count - 1)) + 20f;

            // Content 크기 설정
            rectTransform.sizeDelta = new Vector2(totalWidth, rectTransform.sizeDelta.y);
        }

        // =====================================================================
        // 스크롤 제어
        // =====================================================================

        /// <summary>
        /// 스크롤을 맨 왼쪽으로 이동
        /// </summary>
        public void ScrollToStart()
        {
            if (scrollRect != null)
            {
                scrollRect.horizontalNormalizedPosition = 0f;
            }
        }

        /// <summary>
        /// 스크롤을 맨 오른쪽으로 이동
        /// </summary>
        public void ScrollToEnd()
        {
            if (scrollRect != null)
            {
                scrollRect.horizontalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// 특정 집으로 스크롤
        /// </summary>
        public void ScrollToHouse(string houseId)
        {
            if (scrollRect == null) return;

            int index = houseCards.FindIndex(c => c.BoundHouse?.houseId == houseId);
            if (index < 0) return;

            int count = houseCards.Count;
            if (count <= 1) return;

            float normalizedPos = (float)index / (count - 1);
            scrollRect.horizontalNormalizedPosition = normalizedPos;
        }

        // =====================================================================
        // 외부 접근
        // =====================================================================

        /// <summary>
        /// 특정 집 카드 찾기
        /// </summary>
        public HouseCard GetHouseCard(string houseId)
        {
            return houseCards.Find(c => c.BoundHouse?.houseId == houseId);
        }

        /// <summary>
        /// 모든 집 카드
        /// </summary>
        public IReadOnlyList<HouseCard> AllHouseCards => houseCards.AsReadOnly();
    }
}