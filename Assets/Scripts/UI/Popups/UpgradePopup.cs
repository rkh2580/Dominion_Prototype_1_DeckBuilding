// =============================================================================
// UpgradePopup.cs (권장: PromotionCardPopup.cs로 리네임)
// 전직 3지선다 카드 선택 팝업
// =============================================================================
//
// [역할]
// - 전직 시 3장의 카드 중 1장 선택
// - 선택된 카드를 유닛에게 부여
//
// [흐름]
// 1. UnitDetailPopup에서 OnPromotionRequested 이벤트 발생
// 2. UpgradePopup이 이벤트 수신 → Show(unit)
// 3. 직업풀에서 랜덤 3장 제시
// 4. 플레이어가 카드 선택
// 5. UnitSystem.PromoteUnit() 호출
// 6. 팝업 닫기
//
// [전직 단계별 카드 등급]
// - Lv.0 → Lv.1: 기본 + 고급
// - Lv.1 → Lv.2: 고급 + 희귀
// - Lv.2 → Lv.3: 희귀 + 초희귀
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
    /// 전직 3지선다 팝업
    /// </summary>
    public class UpgradePopup : MonoBehaviour
    {
        // =====================================================================
        // Inspector 연결
        // =====================================================================

        [Header("팝업 요소")]
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private Image dimBackground;

        [Header("헤더")]
        [SerializeField] private TMP_Text titleText;        // "주민 A 전직"
        [SerializeField] private TMP_Text subtitleText;     // "카드 1장을 선택하세요"

        [Header("카드 슬롯")]
        [SerializeField] private Transform cardContainer;   // 카드 3장 컨테이너
        [SerializeField] private GameObject upgradeCardPrefab; // UpgradeCardSlot 프리팹

        [Header("비용 표시")]
        [SerializeField] private TMP_Text costText;         // "비용: 25 골드"

        [Header("버튼")]
        [SerializeField] private Button cancelButton;

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private UnitInstance currentUnit;
        private List<CardData> offeredCards = new List<CardData>();
        private List<UpgradeCardSlot> cardSlots = new List<UpgradeCardSlot>();
        private int promotionCost;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            // 취소 버튼
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(Hide);
            }

            // DimBackground 클릭 시 닫기
            if (dimBackground != null)
            {
                var dimButton = dimBackground.GetComponent<Button>();
                if (dimButton == null)
                {
                    dimButton = dimBackground.gameObject.AddComponent<Button>();
                    dimButton.transition = Selectable.Transition.None;
                }
                dimButton.onClick.AddListener(Hide);
            }

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
            // 전직 요청 이벤트 구독
            UnitDetailPopup.OnPromotionRequested += OnPromotionRequested;

            // 무료 전직 이벤트 구독 (이벤트 효과)
            UnitSystem.OnFreePromotionRequested += OnFreePromotionRequested;
        }

        private void OnDisable()
        {
            UnitDetailPopup.OnPromotionRequested -= OnPromotionRequested;
            UnitSystem.OnFreePromotionRequested -= OnFreePromotionRequested;
        }

        // =====================================================================
        // 이벤트 핸들러
        // =====================================================================

        /// <summary>
        /// 전직 요청 수신
        /// </summary>
        private void OnPromotionRequested(UnitInstance unit)
        {
            Show(unit);
        }

        /// <summary>
        /// 무료 전직 요청 수신 (이벤트 효과)
        /// </summary>
        private void OnFreePromotionRequested(UnitInstance unit)
        {
            Show(unit);
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

            // 전직 가능 여부 재확인
            if (!UnitSystem.Instance.CanPromoteUnit(unit))
            {
                Debug.LogWarning($"[UpgradePopup] 전직 불가: {unit.unitName}");
                return;
            }

            currentUnit = unit;
            promotionCost = UnitSystem.Instance.GetPromotionCost(unit);

            // 카드 3장 생성
            GenerateCardOffers();

            // UI 갱신
            UpdateDisplay();

            // 팝업 활성화
            if (dimBackground != null)
            {
                dimBackground.gameObject.SetActive(true);
            }
            if (popupRoot != null)
            {
                popupRoot.SetActive(true);
            }

            Debug.Log($"[UpgradePopup] 표시: {unit.unitName}, 비용: {promotionCost}G");
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
            offeredCards.Clear();

            Debug.Log("[UpgradePopup] 숨김");
        }

        // =====================================================================
        // 카드 생성
        // =====================================================================

        /// <summary>
        /// 3장의 카드 제안 생성
        /// </summary>
        private void GenerateCardOffers()
        {
            offeredCards.Clear();

            // 유닛의 직업풀에서 카드 가져오기
            var availableCards = GetAvailableCards(currentUnit.job, currentUnit.promotionLevel);

            // 3장 랜덤 선택 (중복 없이)
            var shuffled = new List<CardData>(availableCards);
            ShuffleList(shuffled);

            int count = Mathf.Min(3, shuffled.Count);
            for (int i = 0; i < count; i++)
            {
                offeredCards.Add(shuffled[i]);
            }

            // 카드가 3장 미만이면 경고
            if (offeredCards.Count < 3)
            {
                Debug.LogWarning($"[UpgradePopup] 제공 가능한 카드가 {offeredCards.Count}장뿐입니다.");
            }
        }

        /// <summary>
        /// 직업풀에서 사용 가능한 카드 목록 가져오기
        /// </summary>
        private List<CardData> GetAvailableCards(Job job, int currentLevel)
        {
            var result = new List<CardData>();

            // DataLoader에서 모든 카드 가져오기
            var allCards = DataLoader.Instance?.Cards;
            if (allCards == null) return result;

            foreach (var card in allCards.Values)
            {
                // 액션 카드만
                if (card.cardType != CardType.Action) continue;

                // 직업풀 체크
                if (card.jobPools == null || card.jobPools.Length == 0) continue;

                bool matchesJob = false;
                foreach (var cardJob in card.jobPools)
                {
                    if (cardJob == job)
                    {
                        matchesJob = true;
                        break;
                    }
                }
                if (!matchesJob) continue;

                // 등급 체크 (전직 단계에 따라)
                // Lv.0 → Lv.1: 기본 + 고급
                // Lv.1 → Lv.2: 고급 + 희귀
                // Lv.2 → Lv.3: 희귀 + 초희귀
                bool gradeOk = false;
                switch (currentLevel)
                {
                    case 0:
                        gradeOk = card.rarity == CardRarity.Basic || card.rarity == CardRarity.Advanced;
                        break;
                    case 1:
                        gradeOk = card.rarity == CardRarity.Advanced || card.rarity == CardRarity.Rare;
                        break;
                    case 2:
                        gradeOk = card.rarity == CardRarity.Rare || card.rarity == CardRarity.SuperRare;
                        break;
                    default:
                        gradeOk = false;  // Lv.3 이상은 전직 불가
                        break;
                }
                if (!gradeOk) continue;

                result.Add(card);
            }

            return result;
        }

        /// <summary>
        /// 리스트 셔플 (Fisher-Yates)
        /// </summary>
        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        // =====================================================================
        // UI 갱신
        // =====================================================================

        /// <summary>
        /// 전체 UI 갱신
        /// </summary>
        private void UpdateDisplay()
        {
            // 제목
            if (titleText != null)
            {
                titleText.text = $"{currentUnit.unitName} 전직";
            }

            // 부제목
            if (subtitleText != null)
            {
                int nextLevel = currentUnit.promotionLevel + 1;
                subtitleText.text = $"Lv.{nextLevel} 카드 1장을 선택하세요";
            }

            // 비용 (무료 전직이면 "무료!" 표시)
            if (costText != null)
            {
                if (UnitSystem.Instance.IsFreePromotion(currentUnit))
                {
                    costText.text = "무료!";
                }
                else
                {
                    costText.text = $"비용: {promotionCost} 골드";
                }
            }

            // 카드 슬롯 갱신
            UpdateCardSlots();
        }

        /// <summary>
        /// 카드 슬롯 갱신
        /// </summary>
        private void UpdateCardSlots()
        {
            // 기존 슬롯 제거
            ClearCardSlots();

            if (cardContainer == null || upgradeCardPrefab == null) return;

            // 새 슬롯 생성
            for (int i = 0; i < offeredCards.Count; i++)
            {
                var cardData = offeredCards[i];

                var slotObj = Instantiate(upgradeCardPrefab, cardContainer);
                var slot = slotObj.GetComponent<UpgradeCardSlot>();

                if (slot != null)
                {
                    slot.Setup(cardData, i);
                    slot.OnCardSelected += OnCardSelected;
                    cardSlots.Add(slot);
                }
            }
        }

        /// <summary>
        /// 카드 슬롯 제거
        /// </summary>
        private void ClearCardSlots()
        {
            foreach (var slot in cardSlots)
            {
                if (slot != null)
                {
                    slot.OnCardSelected -= OnCardSelected;
                    Destroy(slot.gameObject);
                }
            }
            cardSlots.Clear();
        }

        // =====================================================================
        // 카드 선택 처리
        // =====================================================================

        /// <summary>
        /// 전직 완료됨 (UnitDetailPopup이 구독하여 닫힘)
        /// </summary>
        public static event System.Action OnPromotionCompleted;

        /// <summary>
        /// 카드 선택됨
        /// </summary>
        private void OnCardSelected(int index)
        {
            if (index < 0 || index >= offeredCards.Count) return;

            var selectedCard = offeredCards[index];

            Debug.Log($"[UpgradePopup] 카드 선택: {selectedCard.cardName}");

            // 전직 실행
            bool success = UnitSystem.Instance.PromoteUnit(currentUnit, selectedCard.id);

            if (success)
            {
                Debug.Log($"[UpgradePopup] 전직 성공! {currentUnit.unitName} → {selectedCard.cardName}");

                // 전직 완료 이벤트 발생
                OnPromotionCompleted?.Invoke();
            }
            else
            {
                Debug.LogError($"[UpgradePopup] 전직 실패!");
            }

            // 팝업 닫기
            Hide();
        }
    }
}