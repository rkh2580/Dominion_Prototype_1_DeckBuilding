// =============================================================================
// EmptySlot.cs
// 빈 슬롯 표시 (드래그/드롭 타겟)
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 빈 슬롯 - 유닛이 없는 자리 표시
    /// </summary>
    public class EmptySlot : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text labelText;

        [Header("색상")]
        [SerializeField] private Color normalColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private Color highlightColor = new Color(0.5f, 0.8f, 0.5f, 0.7f);
        [SerializeField] private Color invalidColor = new Color(0.8f, 0.3f, 0.3f, 0.7f);

        public HouseSlotType SlotType { get; private set; }
        public string HouseId { get; private set; }

        /// <summary>
        /// 슬롯 초기화
        /// </summary>
        public void Setup(HouseSlotType slotType, string houseId)
        {
            SlotType = slotType;
            HouseId = houseId;

            if (labelText != null)
            {
                labelText.text = slotType == HouseSlotType.Child ? "유년" : "성인";
            }

            SetNormal();
        }

        // =====================================================================
        // 상태 변경 (드래그/드롭 피드백)
        // =====================================================================

        public void SetNormal()
        {
            if (background != null)
                background.color = normalColor;
        }

        public void SetHighlight()
        {
            if (background != null)
                background.color = highlightColor;
        }

        public void SetInvalid()
        {
            if (background != null)
                background.color = invalidColor;
        }

        /// <summary>
        /// 유닛이 이 슬롯에 배치 가능한지
        /// </summary>
        public bool CanAccept(UnitInstance unit)
        {
            if (unit == null) return false;

            bool isChildUnit = unit.stage == GrowthStage.Child;
            bool isChildSlot = SlotType == HouseSlotType.Child;

            // 유년 유닛 → 유년 슬롯만
            // 성인 유닛 → 성인 슬롯만
            return isChildUnit == isChildSlot;
        }
    }
}