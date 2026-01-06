// =============================================================================
// HouseCard.cs
// 개별 집 카드
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 개별 집 카드
    /// 
    /// 구조:
    /// HouseCard
    /// ├── HouseNameText
    /// ├── AdultRow (Horizontal Layout Group)
    /// │   ├── UnitCard 또는 EmptySlot
    /// │   └── UnitCard 또는 EmptySlot
    /// ├── ChildRow
    /// │   └── UnitCard 또는 EmptySlot
    /// └── StatusText
    /// </summary>
    public class HouseCard : MonoBehaviour
    {
        [Header("레이아웃")]
        [SerializeField] private Transform adultRow;
        [SerializeField] private Transform childRow;

        [Header("프리팹")]
        [SerializeField] private GameObject unitCardPrefab;     // 100x140 크기
        [SerializeField] private GameObject emptySlotPrefab;    // 100x140 크기

        [Header("UI 요소")]
        [SerializeField] private TMP_Text houseNameText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image glowImage;

        [Header("색상")]
        [SerializeField] private Color pregnantColor = new Color(0.5f, 0.8f, 1f, 0.5f);
        [SerializeField] private Color breedableColor = new Color(1f, 0.5f, 0.7f, 0.5f);

        private HouseInstance boundHouse;

        private GameObject adultAObject;
        private GameObject adultBObject;
        private GameObject childObject;

        private UnitCard adultACard;
        private UnitCard adultBCard;
        private UnitCard childCard;

        public UnitCard AdultACard => adultACard;
        public UnitCard AdultBCard => adultBCard;
        public UnitCard ChildCard => childCard;
        public HouseInstance BoundHouse => boundHouse;

        // =====================================================================
        // 바인딩
        // =====================================================================

        public void Bind(HouseInstance house)
        {
            boundHouse = house;
            Refresh();
        }

        public void Refresh()
        {
            if (boundHouse == null) return;

            UpdateName();
            UpdateSlots();
            UpdateStatus();
        }

        // =====================================================================
        // UI 갱신
        // =====================================================================

        private void UpdateName()
        {
            if (houseNameText != null)
                houseNameText.text = boundHouse.houseName;
        }

        private void UpdateSlots()
        {
            // 기존 제거
            ClearSlot(ref adultAObject);
            ClearSlot(ref adultBObject);
            ClearSlot(ref childObject);
            adultACard = null;
            adultBCard = null;
            childCard = null;

            // 성인 슬롯 A, B
            adultAObject = CreateSlotContent(adultRow, boundHouse.adultSlotA, HouseSlotType.AdultA, out adultACard);
            adultBObject = CreateSlotContent(adultRow, boundHouse.adultSlotB, HouseSlotType.AdultB, out adultBCard);

            // 유년 슬롯
            childObject = CreateSlotContent(childRow, boundHouse.childSlot, HouseSlotType.Child, out childCard);
        }

        private void ClearSlot(ref GameObject obj)
        {
            if (obj != null)
            {
                Destroy(obj);
                obj = null;
            }
        }

        private GameObject CreateSlotContent(Transform parent, string unitId, HouseSlotType slotType, out UnitCard card)
        {
            card = null;
            if (parent == null) return null;

            GameObject obj = null;

            if (!string.IsNullOrEmpty(unitId))
            {
                var unit = UnitSystem.Instance?.GetUnitById(unitId);
                if (unit != null && unitCardPrefab != null)
                {
                    obj = Instantiate(unitCardPrefab, parent);
                    card = obj.GetComponent<UnitCard>();
                    card?.Bind(unit);
                }
            }
            else if (emptySlotPrefab != null)
            {
                obj = Instantiate(emptySlotPrefab, parent);
                var empty = obj.GetComponent<EmptySlot>();
                empty?.Setup(slotType, boundHouse.houseId);
            }

            return obj;
        }

        private void UpdateStatus()
        {
            bool isPregnant = boundHouse.isPregnant;
            bool canBreed = BreedingSystem.Instance?.CanBreed(boundHouse) ?? false;

            if (statusText != null)
            {
                if (isPregnant)
                {
                    int remaining = GameConfig.PregnancyDuration - boundHouse.pregnancyTurns;
                    statusText.text = $"임신중 ({remaining}턴)";
                    statusText.gameObject.SetActive(true);
                }
                else if (canBreed)
                {
                    int idx = Mathf.Min(boundHouse.fertilityCounter - 1, GameConfig.FertilityChance.Length - 1);
                    if (idx < 0) idx = 0;  // 카운터가 0일 때 대비
                    int chance = GameConfig.FertilityChance[idx];
                    statusText.text = $"교배 {chance}%";
                    statusText.gameObject.SetActive(true);
                }
                else
                {
                    statusText.gameObject.SetActive(false);
                }
            }

            if (glowImage != null)
            {
                if (isPregnant)
                {
                    glowImage.gameObject.SetActive(true);
                    glowImage.color = pregnantColor;
                }
                else if (canBreed)
                {
                    glowImage.gameObject.SetActive(true);
                    glowImage.color = breedableColor;
                }
                else
                {
                    glowImage.gameObject.SetActive(false);
                }
            }
        }

        // =====================================================================
        // 헬퍼
        // =====================================================================

        public bool IsSlotEmpty(HouseSlotType slot)
        {
            return slot switch
            {
                HouseSlotType.AdultA => string.IsNullOrEmpty(boundHouse.adultSlotA),
                HouseSlotType.AdultB => string.IsNullOrEmpty(boundHouse.adultSlotB),
                HouseSlotType.Child => string.IsNullOrEmpty(boundHouse.childSlot),
                _ => false
            };
        }

        public string GetHouseId() => boundHouse?.houseId;
    }
}