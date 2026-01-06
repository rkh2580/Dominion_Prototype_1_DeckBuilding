// =============================================================================
// UnitPanel.cs
// 유닛 패널 - 커스텀 그리드 레이아웃으로 유닛 카드 배치
// =============================================================================
//
// [역할]
// - 유닛 카드들을 4열 그리드로 배치
// - 유닛 수에 따라 줄 수 및 카드 크기 자동 조정
// - 마지막 줄 중앙 정렬
// - UnitSystem 이벤트 구독하여 자동 갱신
//
// [레이아웃 규칙]
// - 1~4명: 1줄, 100% 크기
// - 5~8명: 2줄, 85% 크기
// - 9~12명: 3줄, 70% 크기
// - 13명+: 4줄 이상, 60% 크기 (최소)
//
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 유닛 패널 - 유닛 카드 그리드 레이아웃 관리
    /// </summary>
    public class UnitPanel : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("프리팹")]
        [SerializeField] private GameObject unitCardPrefab;  // UnitCard 프리팹

        [Header("레이아웃 설정")]
        [SerializeField] private int columns = 4;            // 열 개수
        [SerializeField] private float baseCardWidth = 150f; // 기본 카드 너비
        [SerializeField] private float baseCardHeight = 200f;// 기본 카드 높이
        [SerializeField] private float horizontalSpacing = 20f; // 가로 간격
        [SerializeField] private float verticalSpacing = 15f;   // 세로 간격

        [Header("스케일 설정")]
        [SerializeField] private float scale1Row = 1.0f;     // 1줄일 때 스케일
        [SerializeField] private float scale2Row = 0.85f;    // 2줄일 때 스케일
        [SerializeField] private float scale3Row = 0.7f;     // 3줄일 때 스케일
        [SerializeField] private float scaleMin = 0.6f;      // 최소 스케일

        [Header("정렬")]
        [SerializeField] private bool centerLastRow = true;  // 마지막 줄 중앙 정렬

        // =====================================================================
        // 내부 상태
        // =====================================================================

        // 유닛 ID → UnitCard 매핑
        private Dictionary<string, UnitCard> unitCards = new Dictionary<string, UnitCard>();

        // 정렬된 카드 리스트 (배치 순서용)
        private List<UnitCard> sortedCards = new List<UnitCard>();

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            if (unitCardPrefab == null)
            {
                Debug.LogError("[UnitPanel] unitCardPrefab이 할당되지 않았습니다!");
            }
        }

        private void OnEnable()
        {
            // UnitSystem 이벤트 구독
            UnitSystem.OnUnitCreated += OnUnitCreated;
            UnitSystem.OnUnitDied += OnUnitDied;
            UnitSystem.OnUnitLeveledUp += OnUnitLeveledUp;
            UnitSystem.OnUnitPromoted += OnUnitPromoted;
            UnitSystem.OnUnitGrown += OnUnitGrown;

            // 게임 시작 이벤트 구독
            GameManager.OnGameStarted += OnGameStarted;

            // 턴 시작 시 전체 갱신 (수명 등)
            TurnManager.OnTurnStarted += OnTurnStarted;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            UnitSystem.OnUnitCreated -= OnUnitCreated;
            UnitSystem.OnUnitDied -= OnUnitDied;
            UnitSystem.OnUnitLeveledUp -= OnUnitLeveledUp;
            UnitSystem.OnUnitPromoted -= OnUnitPromoted;
            UnitSystem.OnUnitGrown -= OnUnitGrown;

            GameManager.OnGameStarted -= OnGameStarted;
            TurnManager.OnTurnStarted -= OnTurnStarted;
        }

        // =====================================================================
        // 이벤트 핸들러
        // =====================================================================

        /// <summary>
        /// 게임 시작 시 초기 유닛들 표시
        /// </summary>
        private void OnGameStarted()
        {
            ClearAllCards();

            if (GameManager.Instance?.State?.units == null) return;

            foreach (var unit in GameManager.Instance.State.units)
            {
                CreateUnitCard(unit);
            }

            ArrangeCards();
        }

        /// <summary>
        /// 유닛 생성됨
        /// </summary>
        private void OnUnitCreated(UnitInstance unit)
        {
            CreateUnitCard(unit);
            ArrangeCards();
        }

        /// <summary>
        /// 유닛 사망함
        /// </summary>
        private void OnUnitDied(UnitInstance unit)
        {
            RemoveUnitCard(unit.unitId);
            ArrangeCards();
        }

        /// <summary>
        /// 유닛 강화됨
        /// </summary>
        private void OnUnitLeveledUp(UnitInstance unit, int newLevel, CardInstance card)
        {
            RefreshUnitCard(unit);
        }

        /// <summary>
        /// 유닛 전직함
        /// </summary>
        private void OnUnitPromoted(UnitInstance unit, Job newJob)
        {
            RefreshUnitCard(unit);
        }

        /// <summary>
        /// 턴 시작됨 - 모든 유닛 카드 갱신 (수명 등)
        /// </summary>
        private void OnTurnStarted(int turnNumber)
        {
            RefreshAll();
        }

        /// <summary>
        /// 유닛 성장함 (단계 변경)
        /// </summary>
        private void OnUnitGrown(UnitInstance unit, GrowthStage oldStage, GrowthStage newStage)
        {
            RefreshUnitCard(unit);
        }

        // =====================================================================
        // 카드 관리
        // =====================================================================

        /// <summary>
        /// 유닛 카드 생성
        /// </summary>
        private void CreateUnitCard(UnitInstance unit)
        {
            if (unitCards.ContainsKey(unit.unitId))
            {
                Debug.LogWarning($"[UnitPanel] 이미 존재하는 유닛 카드: {unit.unitName}");
                return;
            }

            if (unitCardPrefab == null)
            {
                Debug.LogError("[UnitPanel] unitCardPrefab이 null입니다!");
                return;
            }

            // 카드 생성
            var cardObj = Instantiate(unitCardPrefab, transform);
            var card = cardObj.GetComponent<UnitCard>();

            if (card == null)
            {
                Debug.LogError("[UnitPanel] UnitCard 컴포넌트를 찾을 수 없습니다!");
                Destroy(cardObj);
                return;
            }

            // 바인딩
            card.Bind(unit);

            // 딕셔너리에 추가
            unitCards[unit.unitId] = card;
            sortedCards.Add(card);

            Debug.Log($"[UnitPanel] 유닛 카드 생성: {unit.unitName}");
        }

        /// <summary>
        /// 유닛 카드 제거
        /// </summary>
        private void RemoveUnitCard(string unitId)
        {
            if (!unitCards.TryGetValue(unitId, out var card))
            {
                return;
            }

            sortedCards.Remove(card);
            unitCards.Remove(unitId);

            if (card != null && card.gameObject != null)
            {
                Destroy(card.gameObject);
            }

            Debug.Log($"[UnitPanel] 유닛 카드 제거: {unitId}");
        }

        /// <summary>
        /// 유닛 카드 갱신
        /// </summary>
        private void RefreshUnitCard(UnitInstance unit)
        {
            if (unitCards.TryGetValue(unit.unitId, out var card))
            {
                card.Refresh();
            }
        }

        /// <summary>
        /// 모든 카드 제거
        /// </summary>
        private void ClearAllCards()
        {
            foreach (var card in unitCards.Values)
            {
                if (card != null && card.gameObject != null)
                {
                    Destroy(card.gameObject);
                }
            }

            unitCards.Clear();
            sortedCards.Clear();
        }

        // =====================================================================
        // 레이아웃 계산
        // =====================================================================

        /// <summary>
        /// 카드들 재배치
        /// </summary>
        public void ArrangeCards()
        {
            int count = sortedCards.Count;
            if (count == 0) return;

            // 줄 수 계산
            int rows = Mathf.CeilToInt(count / (float)columns);

            // 스케일 결정
            float scale = GetScaleForRows(rows);

            // 실제 카드 크기 (스케일 적용)
            float cardWidth = baseCardWidth * scale;
            float cardHeight = baseCardHeight * scale;

            // 간격도 스케일 적용
            float hSpacing = horizontalSpacing * scale;
            float vSpacing = verticalSpacing * scale;

            // 전체 그리드 크기 계산 (중앙 정렬용)
            float totalWidth = columns * cardWidth + (columns - 1) * hSpacing;
            float totalHeight = rows * cardHeight + (rows - 1) * vSpacing;

            // 시작 위치 (패널 중앙 기준)
            float startX = -totalWidth / 2f + cardWidth / 2f;
            float startY = totalHeight / 2f - cardHeight / 2f;

            // 각 카드 배치
            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int col = i % columns;

                // 이 줄의 아이템 수
                int itemsInThisRow = (row == rows - 1)
                    ? count - (rows - 1) * columns  // 마지막 줄
                    : columns;                       // 다른 줄

                // 마지막 줄 중앙 정렬 오프셋
                float rowOffsetX = 0f;
                if (centerLastRow && row == rows - 1 && itemsInThisRow < columns)
                {
                    float emptySpace = (columns - itemsInThisRow) * (cardWidth + hSpacing);
                    rowOffsetX = emptySpace / 2f;
                }

                // 위치 계산
                float x = startX + col * (cardWidth + hSpacing) + rowOffsetX;
                float y = startY - row * (cardHeight + vSpacing);

                // 적용
                var card = sortedCards[i];
                card.transform.localPosition = new Vector3(x, y, 0);
                card.transform.localScale = Vector3.one * scale;
            }

            Debug.Log($"[UnitPanel] 카드 배치 완료: {count}개, {rows}줄, 스케일 {scale}");
        }

        /// <summary>
        /// 줄 수에 따른 스케일 반환
        /// </summary>
        private float GetScaleForRows(int rows)
        {
            return rows switch
            {
                1 => scale1Row,
                2 => scale2Row,
                3 => scale3Row,
                _ => scaleMin
            };
        }

        // =====================================================================
        // 외부 접근
        // =====================================================================

        /// <summary>
        /// 전체 갱신 (수동 호출용)
        /// </summary>
        public void RefreshAll()
        {
            foreach (var card in unitCards.Values)
            {
                card.Refresh();
            }
        }

        /// <summary>
        /// 특정 유닛 카드 가져오기
        /// </summary>
        public UnitCard GetUnitCard(string unitId)
        {
            unitCards.TryGetValue(unitId, out var card);
            return card;
        }
    }
}