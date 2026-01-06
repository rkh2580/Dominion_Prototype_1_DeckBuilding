// =============================================================================
// UpgradeCardSlot.cs
// 강화 선택지 카드 슬롯
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 강화 선택지 카드 슬롯
    /// </summary>
    public class UpgradeCardSlot : MonoBehaviour
    {
        [Header("UI 요소")]
        [SerializeField] private Image cardBackground;
        [SerializeField] private TMP_Text cardNameText;
        [SerializeField] private TMP_Text cardDescText;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private Button selectButton;

        [Header("등급별 색상")]
        [SerializeField] private Color basicColor = new Color(0.7f, 0.7f, 0.7f);
        [SerializeField] private Color advancedColor = new Color(0.3f, 0.5f, 0.8f);
        [SerializeField] private Color rareColor = new Color(0.8f, 0.6f, 0.2f);

        private int slotIndex;
        private CardData boundCard;

        /// <summary>
        /// 카드 선택 이벤트
        /// </summary>
        public event System.Action<int> OnCardSelected;

        private void Awake()
        {
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnSelectClicked);
            }
        }

        /// <summary>
        /// 슬롯 설정
        /// </summary>
        public void Setup(CardData card, int index)
        {
            boundCard = card;
            slotIndex = index;

            // 카드 이름
            if (cardNameText != null)
            {
                cardNameText.text = card.cardName;
            }

            // 카드 설명 (효과 요약)
            if (cardDescText != null)
            {
                cardDescText.text = GetEffectSummary(card);
            }

            // 등급
            if (rarityText != null)
            {
                rarityText.text = GetRarityName(card.rarity);
            }

            // 배경색
            if (cardBackground != null)
            {
                cardBackground.color = card.rarity switch
                {
                    CardRarity.Basic => basicColor,
                    CardRarity.Advanced => advancedColor,
                    CardRarity.Rare => rareColor,
                    _ => basicColor
                };
            }
        }

        /// <summary>
        /// 선택 버튼 클릭
        /// </summary>
        private void OnSelectClicked()
        {
            OnCardSelected?.Invoke(slotIndex);
        }

        /// <summary>
        /// 효과 요약 텍스트 생성
        /// </summary>
        private string GetEffectSummary(CardData card)
        {
            // description이 있으면 우선 사용 (기획자 의도 반영)
            if (!string.IsNullOrEmpty(card.description))
            {
                return card.description;
            }

            // description이 없을 때만 동적 생성 (폴백)
            if (card.effects == null || card.effects.Length == 0)
            {
                return "";
            }

            var summary = new System.Text.StringBuilder();

            // [리팩토링] effects가 이제 ConditionalEffect[] 타입
            foreach (var group in card.effects)
            {
                if (group.effects == null) continue;

                foreach (var effect in group.effects)
                {
                    string effectText = effect.type switch
                    {
                        EffectType.DrawCard => $"+{effect.value} 카드",
                        EffectType.AddAction => $"+{effect.value} 액션",
                        EffectType.AddGold => $"+{effect.value} 골드",
                        EffectType.CreateTempTreasure => $"임시 {TreasureGradeUtil.GetName(effect.createGrade)} 생성",
                        EffectType.BoostTreasure => $"재화 +{effect.value}등급",
                        EffectType.DestroyCard => "카드 소멸",
                        EffectType.DestroyPollution => "오염 정화",
                        EffectType.Gamble => $"도박 ({effect.successChance}%)",
                        EffectType.DelayedGold => $"{effect.duration}턴 후 +{effect.value}G",
                        EffectType.PersistentGold => $"{effect.duration}턴간 +{effect.value}G",
                        _ => ""
                    };

                    if (!string.IsNullOrEmpty(effectText))
                    {
                        if (summary.Length > 0) summary.Append(", ");
                        summary.Append(effectText);
                    }
                }
            }

            return summary.ToString();
        }

        /// <summary>
        /// 등급 이름
        /// </summary>
        private string GetRarityName(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Basic => "기본",
                CardRarity.Advanced => "고급",
                CardRarity.Rare => "희귀",
                CardRarity.SuperRare => "초희귀",
                CardRarity.Legendary => "전설",
                _ => "?"
            };
        }
    }
}