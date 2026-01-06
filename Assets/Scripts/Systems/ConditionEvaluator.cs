// =============================================================================
// ConditionEvaluator.cs
// 공용 조건 평가기 - EventSystem, EffectSystem에서 공유
// =============================================================================
//
// [역할]
// - 게임 상태 기반 조건 평가 로직 통합
// - 중복 코드 제거 및 일관성 보장
//
// [지원 조건]
// - 골드 조건: GoldAbove, GoldBelow
// - 유닛 조건: HasUnit, HasMultipleUnits, HasPromotableUnit
// - 덱 전체 조건: HasCopperInDeck, HasPollutionInDeck, HasSpecificCardInDeck
//
// [미지원 - 각 시스템에서 처리]
// - 손패 조건 (EffectSystem 전용)
// - 덱 탑 조건 (EffectSystem 전용)
// - 이전 효과 조건 (EffectSystem 전용 - resultStack 의존)
//
// =============================================================================

using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 공용 조건 평가기
    /// </summary>
    public static class ConditionEvaluator
    {
        // =====================================================================
        // 메인 평가 메서드
        // =====================================================================

        /// <summary>
        /// 이 평가기가 처리할 수 있는 조건인지 확인
        /// </summary>
        public static bool CanEvaluate(ConditionType type)
        {
            switch (type)
            {
                // 공용 조건
                case ConditionType.None:
                case ConditionType.GoldAbove:
                case ConditionType.GoldBelow:
                case ConditionType.HasUnit:
                case ConditionType.HasMultipleUnits:
                case ConditionType.HasPromotableUnit:
                case ConditionType.HasCopperInDeck:
                case ConditionType.HasPollutionInDeck:
                case ConditionType.HasSpecificCardInDeck:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 조건 평가 (공용 조건만)
        /// </summary>
        public static bool Evaluate(EffectCondition condition, GameState state)
        {
            if (condition == null || condition.type == ConditionType.None)
                return true;

            if (state == null)
                return false;

            switch (condition.type)
            {
                // === 골드 조건 ===
                case ConditionType.GoldAbove:
                    return CompareValue(state.gold, condition.comparison, condition.value);

                case ConditionType.GoldBelow:
                    return state.gold < condition.value;

                // === 유닛 조건 ===
                case ConditionType.HasUnit:
                    return state.units != null && state.units.Count >= 1;

                case ConditionType.HasMultipleUnits:
                    return state.units != null && state.units.Count >= condition.value;

                case ConditionType.HasPromotableUnit:
                    return state.units != null && state.units.Exists(u => u.CanPromote());

                // === 덱 전체 조건 (이벤트용) ===
                case ConditionType.HasCopperInDeck:
                    return HasCardById(state, "copper");

                case ConditionType.HasPollutionInDeck:
                    return HasCardOfType(state, CardType.Pollution);

                case ConditionType.HasSpecificCardInDeck:
                    return true; // cardId 필드 확장 시 구현

                default:
                    Debug.LogWarning($"[ConditionEvaluator] 미지원 조건: {condition.type}");
                    return true;
            }
        }

        // =====================================================================
        // 비교 연산
        // =====================================================================

        /// <summary>
        /// 비교 연산 수행
        /// </summary>
        public static bool CompareValue(int actual, ComparisonType comparison, int target)
        {
            return comparison switch
            {
                ComparisonType.Equal => actual == target,
                ComparisonType.NotEqual => actual != target,
                ComparisonType.GreaterThan => actual > target,
                ComparisonType.LessThan => actual < target,
                ComparisonType.GreaterOrEqual => actual >= target,
                ComparisonType.LessOrEqual => actual <= target,
                _ => true
            };
        }

        // =====================================================================
        // 헬퍼 메서드 - 덱 전체 검색
        // =====================================================================

        /// <summary>
        /// 덱 전체(덱+손패+버린더미)에서 특정 ID 카드 존재 확인
        /// </summary>
        private static bool HasCardById(GameState state, string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;

            // 덱 검색
            if (state.deck != null)
            {
                foreach (var card in state.deck)
                {
                    if (card.cardDataId == cardId) return true;
                }
            }

            // 손패 검색
            if (state.hand != null)
            {
                foreach (var card in state.hand)
                {
                    if (card.cardDataId == cardId) return true;
                }
            }

            // 버린 더미 검색
            if (state.discardPile != null)
            {
                foreach (var card in state.discardPile)
                {
                    if (card.cardDataId == cardId) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 덱 전체(덱+손패+버린더미)에서 특정 타입 카드 존재 확인
        /// </summary>
        private static bool HasCardOfType(GameState state, CardType cardType)
        {
            // 덱 검색
            if (state.deck != null)
            {
                foreach (var card in state.deck)
                {
                    var data = DataLoader.Instance?.GetCard(card.cardDataId);
                    if (data?.cardType == cardType) return true;
                }
            }

            // 손패 검색
            if (state.hand != null)
            {
                foreach (var card in state.hand)
                {
                    var data = DataLoader.Instance?.GetCard(card.cardDataId);
                    if (data?.cardType == cardType) return true;
                }
            }

            // 버린 더미 검색
            if (state.discardPile != null)
            {
                foreach (var card in state.discardPile)
                {
                    var data = DataLoader.Instance?.GetCard(card.cardDataId);
                    if (data?.cardType == cardType) return true;
                }
            }

            return false;
        }
    }
}
