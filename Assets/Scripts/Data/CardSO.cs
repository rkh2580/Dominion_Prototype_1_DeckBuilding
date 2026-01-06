// =============================================================================
// CardSO.cs
// 카드 ScriptableObject 정의
// =============================================================================
// [E2] 개별 카드를 SO로 관리
// - JSON의 CardData를 SO로 변환
// - 에디터에서 직접 수정 가능
// - CardDatabaseSO에서 참조
// =============================================================================

using System;
using UnityEngine;

namespace DeckBuildingEconomy.Data
{
    /// <summary>
    /// 카드 ScriptableObject
    /// 개별 카드 데이터를 에셋으로 관리
    /// </summary>
    [CreateAssetMenu(fileName = "NewCard", menuName = "DeckBuilding/Card")]
    public class CardSO : ScriptableObject
    {
        // =====================================================================
        // 기본 정보
        // =====================================================================

        [Header("기본 정보")]
        [Tooltip("카드 고유 ID (예: copper, labor, explore)")]
        public string id;

        [Tooltip("표시 이름 (예: 동화, 노동, 탐색)")]
        public string cardName;

        [Tooltip("카드 타입")]
        public CardType cardType;

        [TextArea(2, 4)]
        [Tooltip("카드 설명")]
        public string description;

        // =====================================================================
        // 재화 카드 전용
        // =====================================================================

        [Header("재화 카드 (Treasure)")]
        [Tooltip("재화 등급 (Treasure 타입만)")]
        public TreasureGrade treasureGrade;

        [Tooltip("골드 값 (1, 2, 4, 7, 12, 20, 35)")]
        public int goldValue;

        // =====================================================================
        // 액션 카드 전용
        // =====================================================================

        [Header("액션 카드 (Action)")]
        [Tooltip("희귀도 (Action 타입만)")]
        public CardRarity rarity;

        [Tooltip("속한 직업풀 (복수 선택 가능)")]
        public Job[] jobPools;

        // =====================================================================
        // 오염 카드 전용
        // =====================================================================

        [Header("오염 카드 (Pollution)")]
        [Tooltip("오염 종류 (Pollution 타입만)")]
        public PollutionType pollutionType;

        // =====================================================================
        // 효과
        // =====================================================================

        [Header("카드 효과")]
        [Tooltip("조건부 효과 목록 (조건 없으면 항상 발동)")]
        public ConditionalEffect[] effects;

        // =====================================================================
        // 변환 메서드
        // =====================================================================

        /// <summary>
        /// SO → CardData 변환 (기존 시스템 호환용)
        /// </summary>
        public CardData ToCardData()
        {
            return new CardData
            {
                id = this.id,
                cardName = this.cardName,
                cardType = this.cardType,
                description = this.description,
                treasureGrade = this.treasureGrade,
                goldValue = this.goldValue,
                rarity = this.rarity,
                jobPools = this.jobPools != null ? (Job[])this.jobPools.Clone() : null,
                pollutionType = this.pollutionType,
                effects = CloneEffects(this.effects)
            };
        }

        /// <summary>
        /// CardData → SO로 데이터 복사
        /// </summary>
        public void FromCardData(CardData data)
        {
            if (data == null) return;

            this.id = data.id;
            this.cardName = data.cardName;
            this.cardType = data.cardType;
            this.description = data.description;
            this.treasureGrade = data.treasureGrade;
            this.goldValue = data.goldValue;
            this.rarity = data.rarity;
            this.jobPools = data.jobPools != null ? (Job[])data.jobPools.Clone() : null;
            this.pollutionType = data.pollutionType;
            this.effects = CloneEffects(data.effects);
        }

        /// <summary>
        /// 효과 배열 깊은 복사
        /// </summary>
        private static ConditionalEffect[] CloneEffects(ConditionalEffect[] source)
        {
            if (source == null) return null;

            var result = new ConditionalEffect[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var src = source[i];
                result[i] = new ConditionalEffect
                {
                    conditions = src.conditions != null ? (EffectCondition[])src.conditions.Clone() : null,
                    effects = CloneEffectArray(src.effects),
                    elseEffects = CloneEffectArray(src.elseEffects)
                };
            }
            return result;
        }

        private static Effect[] CloneEffectArray(Effect[] source)
        {
            if (source == null) return null;

            var result = new Effect[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var src = source[i];
                result[i] = new Effect
                {
                    type = src.type,
                    value = src.value,
                    dynamicValue = src.dynamicValue,
                    target = src.target,
                    maxTargets = src.maxTargets,
                    createGrade = src.createGrade,
                    duration = src.duration,
                    successChance = src.successChance,
                    successValueInt = src.successValueInt,
                    successValue = src.successValue,
                    failValueInt = src.failValueInt,
                    failValue = src.failValue,
                    cardId = src.cardId,
                    cardRarity = src.cardRarity,
                    cardJobPool = src.cardJobPool
                };
            }
            return result;
        }

        // =====================================================================
        // 유효성 검증
        // =====================================================================

        /// <summary>
        /// 카드 데이터 유효성 검증
        /// </summary>
        public bool Validate(out string error)
        {
            error = null;

            // ID 필수
            if (string.IsNullOrEmpty(id))
            {
                error = "ID가 비어있습니다.";
                return false;
            }

            // 카드 이름 필수
            if (string.IsNullOrEmpty(cardName))
            {
                error = "카드 이름이 비어있습니다.";
                return false;
            }

            // 타입별 검증
            switch (cardType)
            {
                case CardType.Treasure:
                    if (goldValue <= 0)
                    {
                        error = "재화 카드의 goldValue가 0 이하입니다.";
                        return false;
                    }
                    break;

                case CardType.Action:
                    // jobPools 없어도 됨 (범용 카드)
                    break;

                case CardType.Pollution:
                    // 특별한 검증 없음
                    break;
            }

            return true;
        }

        // =====================================================================
        // 헬퍼 메서드
        // =====================================================================

        /// <summary>
        /// 재화 카드인지 확인
        /// </summary>
        public bool IsTreasure => cardType == CardType.Treasure;

        /// <summary>
        /// 액션 카드인지 확인
        /// </summary>
        public bool IsAction => cardType == CardType.Action;

        /// <summary>
        /// 오염 카드인지 확인
        /// </summary>
        public bool IsPollution => cardType == CardType.Pollution;

        /// <summary>
        /// 특정 직업풀에 속하는지 확인
        /// </summary>
        public bool BelongsToJob(Job job)
        {
            if (jobPools == null) return false;
            foreach (var j in jobPools)
            {
                if (j == job) return true;
            }
            return false;
        }

        /// <summary>
        /// 효과 요약 텍스트 생성
        /// </summary>
        public string GetEffectSummary()
        {
            if (effects == null || effects.Length == 0)
            {
                if (cardType == CardType.Treasure)
                    return $"+{goldValue} 골드";
                return "(효과 없음)";
            }

            var summary = new System.Text.StringBuilder();
            foreach (var ce in effects)
            {
                if (ce.effects == null) continue;
                foreach (var eff in ce.effects)
                {
                    if (summary.Length > 0) summary.Append(", ");
                    summary.Append(GetEffectText(eff));
                }
            }
            return summary.Length > 0 ? summary.ToString() : "(효과 없음)";
        }

        private string GetEffectText(Effect eff)
        {
            switch (eff.type)
            {
                case EffectType.DrawCard:
                    return $"+{eff.value}장 드로우";
                case EffectType.AddAction:
                    return $"+{eff.value} 액션";
                case EffectType.AddGold:
                    return $"+{eff.value} 골드";
                case EffectType.SettleCard:
                    return "재화 정산";
                case EffectType.DestroyCard:
                    return "카드 소멸";
                case EffectType.DestroyPollution:
                    return "오염 소멸";
                case EffectType.Gamble:
                    return $"도박 ({eff.successChance}%)";
                default:
                    return eff.type.ToString();
            }
        }
    }
}
