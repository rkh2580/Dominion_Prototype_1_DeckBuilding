// =============================================================================
// EventSO.cs
// 이벤트 ScriptableObject 정의
// =============================================================================
// [E3] 개별 이벤트를 SO로 관리
// - JSON의 EventData를 SO로 변환
// - 에디터에서 직접 수정 가능
// - EventDatabaseSO에서 참조
// - 기존 EventData, EventChoice 클래스 재사용
// =============================================================================

using System;
using UnityEngine;
using DeckBuildingEconomy.Core;

namespace DeckBuildingEconomy.Data
{
    /// <summary>
    /// 이벤트 ScriptableObject
    /// 개별 이벤트 데이터를 에셋으로 관리
    /// </summary>
    [CreateAssetMenu(fileName = "NewEvent", menuName = "DeckBuilding/Event")]
    public class EventSO : ScriptableObject
    {
        // =====================================================================
        // 기본 정보
        // =====================================================================

        [Header("기본 정보")]
        [Tooltip("이벤트 고유 ID (예: good_harvest, bandit_raid)")]
        public string eventId;

        [Tooltip("표시 이름 (예: 풍년, 도적 습격)")]
        public string eventName;

        [Tooltip("이벤트 카테고리")]
        public RandomEventCategory category = RandomEventCategory.Positive;

        [TextArea(2, 4)]
        [Tooltip("이벤트 설명 (UI에 표시)")]
        public string description;

        // =====================================================================
        // 발동 조건
        // =====================================================================

        [Header("발동 조건")]
        [Tooltip("이벤트 발생을 위한 조건 (모두 AND)\n비어있으면 항상 후보에 포함")]
        public EffectCondition[] triggerConditions;

        // =====================================================================
        // 효과 (긍정/부정 이벤트)
        // =====================================================================

        [Header("즉시 효과 (긍정/부정 이벤트)")]
        [Tooltip("선택지 없이 즉시 발동하는 효과")]
        public ConditionalEffect[] effects;

        // =====================================================================
        // 선택지 (선택 이벤트)
        // =====================================================================

        [Header("선택지 (선택 이벤트)")]
        [Tooltip("플레이어가 선택할 수 있는 옵션들\n비어있으면 즉시 효과 이벤트")]
        public EventChoice[] choices;

        // =====================================================================
        // 변환 메서드
        // =====================================================================

        /// <summary>
        /// SO → EventData 변환 (기존 시스템 호환용)
        /// </summary>
        public EventData ToEventData()
        {
            return new EventData
            {
                eventId = this.eventId,
                eventName = this.eventName,
                category = (int)this.category,
                description = this.description,
                triggerConditions = CloneConditions(this.triggerConditions),
                effects = CloneConditionalEffects(this.effects),
                choices = CloneChoices(this.choices)
            };
        }

        /// <summary>
        /// EventData → SO로 데이터 복사
        /// </summary>
        public void FromEventData(EventData data)
        {
            if (data == null) return;

            this.eventId = data.eventId;
            this.eventName = data.eventName;
            this.category = (RandomEventCategory)data.category;
            this.description = data.description;
            this.triggerConditions = CloneConditions(data.triggerConditions);
            this.effects = CloneConditionalEffects(data.effects);
            this.choices = CloneChoices(data.choices);
        }

        // =====================================================================
        // 깊은 복사 헬퍼
        // =====================================================================

        private static EffectCondition[] CloneConditions(EffectCondition[] source)
        {
            if (source == null) return null;

            var result = new EffectCondition[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var src = source[i];
                result[i] = new EffectCondition
                {
                    type = src.type,
                    comparison = src.comparison,
                    value = src.value
                };
            }
            return result;
        }

        private static ConditionalEffect[] CloneConditionalEffects(ConditionalEffect[] source)
        {
            if (source == null) return null;

            var result = new ConditionalEffect[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var src = source[i];
                result[i] = new ConditionalEffect
                {
                    conditions = CloneConditions(src.conditions),
                    effects = CloneEffects(src.effects),
                    elseEffects = CloneEffects(src.elseEffects)
                };
            }
            return result;
        }

        private static Effect[] CloneEffects(Effect[] source)
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
                    dynamicValue = src.dynamicValue != null ? new ValueSource
                    {
                        source = src.dynamicValue.source,
                        baseValue = src.dynamicValue.baseValue,
                        multiplier = src.dynamicValue.multiplier,
                        min = src.dynamicValue.min,
                        max = src.dynamicValue.max
                    } : null,
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

        private static EventChoice[] CloneChoices(EventChoice[] source)
        {
            if (source == null) return null;

            var result = new EventChoice[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var src = source[i];
                result[i] = new EventChoice
                {
                    choiceId = src.choiceId,
                    choiceText = src.choiceText,
                    requirements = CloneConditions(src.requirements),
                    effects = CloneConditionalEffects(src.effects)
                };
            }
            return result;
        }

        // =====================================================================
        // 유효성 검증
        // =====================================================================

        /// <summary>
        /// 이벤트 데이터 유효성 검증
        /// </summary>
        public bool Validate(out string error)
        {
            error = null;

            // ID 필수
            if (string.IsNullOrEmpty(eventId))
            {
                error = "이벤트 ID가 비어있습니다.";
                return false;
            }

            // 이름 필수
            if (string.IsNullOrEmpty(eventName))
            {
                error = "이벤트 이름이 비어있습니다.";
                return false;
            }

            // 선택 이벤트는 선택지 필요
            if (category == RandomEventCategory.Choice)
            {
                if (choices == null || choices.Length == 0)
                {
                    error = "선택 이벤트에는 최소 1개의 선택지가 필요합니다.";
                    return false;
                }
            }

            // 비선택 이벤트는 효과 필요
            if (category != RandomEventCategory.Choice)
            {
                if (effects == null || effects.Length == 0)
                {
                    error = "긍정/부정 이벤트에는 최소 1개의 효과가 필요합니다.";
                    return false;
                }
            }

            return true;
        }

        // =====================================================================
        // 헬퍼 메서드
        // =====================================================================

        /// <summary>
        /// 선택 이벤트인지 확인
        /// </summary>
        public bool IsChoiceEvent => choices != null && choices.Length > 0;

        /// <summary>
        /// 긍정 이벤트인지 확인
        /// </summary>
        public bool IsPositive => category == RandomEventCategory.Positive;

        /// <summary>
        /// 부정 이벤트인지 확인
        /// </summary>
        public bool IsNegative => category == RandomEventCategory.Negative;

        /// <summary>
        /// 조건부 이벤트인지 확인
        /// </summary>
        public bool HasTriggerConditions => triggerConditions != null && triggerConditions.Length > 0;

        /// <summary>
        /// 효과 요약 텍스트 생성
        /// </summary>
        public string GetEffectSummary()
        {
            if (IsChoiceEvent)
            {
                return $"선택지 {choices.Length}개";
            }

            if (effects == null || effects.Length == 0)
            {
                return "(효과 없음)";
            }

            int totalEffects = 0;
            foreach (var ce in effects)
            {
                if (ce.effects != null) totalEffects += ce.effects.Length;
            }

            return $"효과 {totalEffects}개";
        }

        /// <summary>
        /// 카테고리 한글 이름
        /// </summary>
        public string GetCategoryName()
        {
            switch (category)
            {
                case RandomEventCategory.Positive: return "긍정";
                case RandomEventCategory.Negative: return "부정";
                case RandomEventCategory.Choice: return "선택";
                default: return "알 수 없음";
            }
        }
    }
}
