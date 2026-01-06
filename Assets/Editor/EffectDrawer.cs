// =============================================================================
// EffectDrawer.cs
// Effect 구조체용 PropertyDrawer
// =============================================================================
// [E2] 효과 타입별 필요한 파라미터만 표시
// - DrawCard, AddAction, AddGold: value만
// - CreateTempTreasure: createGrade, value
// - BoostTreasure, SettleCard: target, maxTargets
// - Gamble: successChance, successValueInt, failValueInt
// - PersistentGold: value, duration
// [E3] 이벤트 전용 EffectType 추가
// - GainUnit, RemoveUnit, FreePromotion, AddPromotionLevel
// - AddCardToDeck, RemoveCardFromDeck, UpgradeCardInDeck
// - SpendGoldPercent, PromotionDiscount, MaintenanceModifier
// - cardId 드롭다운 (CardDatabaseSO에서 로드)
// 위치: Assets/Editor/EffectDrawer.cs
// =============================================================================

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using DeckBuildingEconomy.Data;
using System.Collections.Generic;

namespace DeckBuildingEconomy.Editor
{
    /// <summary>
    /// Effect 구조체용 PropertyDrawer
    /// EffectType에 따라 관련 필드만 표시
    /// </summary>
    [CustomPropertyDrawer(typeof(Effect))]
    public class EffectDrawer : PropertyDrawer
    {
        // =====================================================================
        // 상수
        // =====================================================================

        private const float LINE_HEIGHT = 18f;
        private const float SPACING = 2f;
        private const float LABEL_WIDTH = 100f;

        // 카드 목록 캐시
        private static string[] _cardIds;
        private static string[] _cardDisplayNames;
        private static bool _cardsCached = false;

        // =====================================================================
        // GetPropertyHeight
        // =====================================================================

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var typeProp = property.FindPropertyRelative("type");
            EffectType effectType = (EffectType)typeProp.intValue;

            int lineCount = GetLineCount(effectType);
            return (LINE_HEIGHT + SPACING) * lineCount + SPACING;
        }

        /// <summary>
        /// 효과 타입별 필요한 라인 수 계산
        /// [E3] 이벤트 전용 EffectType 추가
        /// </summary>
        private int GetLineCount(EffectType effectType)
        {
            switch (effectType)
            {
                // 1줄: type만
                case EffectType.ShuffleDeck:
                case EffectType.IgnorePollution:
                    return 1;

                // 2줄: type + value
                case EffectType.DrawCard:
                case EffectType.AddAction:
                case EffectType.AddGold:
                case EffectType.DrawUntil:
                case EffectType.SpendGoldPercent:      // [E3] 골드 비율 소모
                case EffectType.PromotionDiscount:     // [E3] 전직 할인
                case EffectType.MaintenanceModifier:   // [E3] 유지비 수정
                case EffectType.AddPromotionLevel:     // [E3] 전직 레벨 추가
                    return 2;

                // 3줄: type + target + maxTargets
                case EffectType.DestroyCard:
                case EffectType.DestroyPollution:
                case EffectType.SettleCard:
                case EffectType.MoveToDeckBottom:
                case EffectType.RemoveUnit:            // [E3] 유닛 제거
                case EffectType.FreePromotion:         // [E3] 무료 전직
                    return 3;

                // 3줄: type + createGrade + value
                case EffectType.CreateTempTreasure:
                    return 3;

                // 3줄: type + cardId + value [E3]
                case EffectType.AddCardToDeck:
                case EffectType.GainUnit:
                    return 3;

                // 3줄: type + cardId + target [E3]
                case EffectType.RemoveCardFromDeck:
                case EffectType.UpgradeCardInDeck:
                    return 3;

                // 4줄: type + value + target + maxTargets
                case EffectType.BoostTreasure:
                case EffectType.PermanentUpgrade:
                    return 4;

                // 4줄: type + successChance + successValue + failValue
                case EffectType.Gamble:
                    return 4;

                // 3줄: type + value + duration
                case EffectType.PersistentGold:
                case EffectType.DelayedGold:
                    return 3;

                // 3줄: type + value + dynamicValue 접힌 상태
                case EffectType.GoldMultiplier:
                case EffectType.GoldBonus:
                    return 3;

                // 기본: 5줄 (모든 주요 필드 표시)
                default:
                    return 5;
            }
        }

        // =====================================================================
        // OnGUI
        // =====================================================================

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 프로퍼티 가져오기
            var typeProp = property.FindPropertyRelative("type");
            var valueProp = property.FindPropertyRelative("value");
            var targetProp = property.FindPropertyRelative("target");
            var maxTargetsProp = property.FindPropertyRelative("maxTargets");
            var createGradeProp = property.FindPropertyRelative("createGrade");
            var durationProp = property.FindPropertyRelative("duration");
            var successChanceProp = property.FindPropertyRelative("successChance");
            var successValueIntProp = property.FindPropertyRelative("successValueInt");
            var failValueIntProp = property.FindPropertyRelative("failValueInt");
            var dynamicValueProp = property.FindPropertyRelative("dynamicValue");
            var cardIdProp = property.FindPropertyRelative("cardId");  // [E3] 추가

            EffectType effectType = (EffectType)typeProp.intValue;

            int line = 0;
            Rect lineRect;

            // === 1줄: 효과 타입 (항상 표시) ===
            lineRect = GetLineRect(position, line++);

            // 타입 이름에 아이콘/설명 추가
            string typeLabel = GetEffectTypeLabel(effectType);
            EditorGUI.PropertyField(lineRect, typeProp, new GUIContent(typeLabel));

            // === 타입별 필드 표시 ===
            switch (effectType)
            {
                // --- 값만 필요한 효과 ---
                case EffectType.DrawCard:
                case EffectType.AddAction:
                case EffectType.AddGold:
                case EffectType.DrawUntil:
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, GetValueLabel(effectType));
                    break;

                // --- [E3] 이벤트 전용: 값만 필요 ---
                case EffectType.SpendGoldPercent:
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "소모 비율 (%)");
                    break;

                case EffectType.PromotionDiscount:
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "할인율 (%)");
                    break;

                case EffectType.MaintenanceModifier:
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "유지비 변화량");
                    break;

                case EffectType.AddPromotionLevel:
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "추가 레벨");
                    break;

                // --- 대상 선택 효과 ---
                case EffectType.DestroyCard:
                case EffectType.DestroyPollution:
                case EffectType.SettleCard:
                case EffectType.MoveToDeckBottom:
                    lineRect = GetLineRect(position, line++);
                    EditorGUI.PropertyField(lineRect, targetProp, new GUIContent("대상"));
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, maxTargetsProp, "최대 대상 수");
                    break;

                // --- [E3] 유닛 제거/무료 전직 ---
                case EffectType.RemoveUnit:
                case EffectType.FreePromotion:
                    lineRect = GetLineRect(position, line++);
                    EditorGUI.PropertyField(lineRect, targetProp, new GUIContent("대상"));
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "수량");
                    break;

                // --- 재화 생성 ---
                case EffectType.CreateTempTreasure:
                    lineRect = GetLineRect(position, line++);
                    EditorGUI.PropertyField(lineRect, createGradeProp, new GUIContent("생성 등급"));
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "생성 개수");
                    break;

                // --- [E3] 카드 추가/유닛 획득 (cardId 드롭다운) ---
                case EffectType.AddCardToDeck:
                    lineRect = GetLineRect(position, line++);
                    DrawCardIdDropdown(lineRect, cardIdProp, "추가할 카드");
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "추가 개수");
                    break;

                case EffectType.GainUnit:
                    lineRect = GetLineRect(position, line++);
                    DrawJobDropdown(lineRect, cardIdProp, "획득 직업");
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "획득 수량");
                    break;

                // --- [E3] 카드 제거/업그레이드 ---
                case EffectType.RemoveCardFromDeck:
                    lineRect = GetLineRect(position, line++);
                    DrawCardIdDropdown(lineRect, cardIdProp, "제거할 카드 (비우면 랜덤)");
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "제거 개수");
                    break;

                case EffectType.UpgradeCardInDeck:
                    lineRect = GetLineRect(position, line++);
                    DrawCardIdDropdown(lineRect, cardIdProp, "업그레이드 대상");
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "업그레이드 수량");
                    break;

                // --- 재화 등급 변경 ---
                case EffectType.BoostTreasure:
                case EffectType.PermanentUpgrade:
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "등급 상승량");
                    lineRect = GetLineRect(position, line++);
                    EditorGUI.PropertyField(lineRect, targetProp, new GUIContent("대상"));
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, maxTargetsProp, "최대 대상 수");
                    break;

                // --- 도박 ---
                case EffectType.Gamble:
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, successChanceProp, "성공 확률 (%)");
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, successValueIntProp, "성공 시 골드");
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, failValueIntProp, "실패 시 골드");
                    break;

                // --- 지속 효과 ---
                case EffectType.PersistentGold:
                case EffectType.DelayedGold:
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "골드");
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, durationProp, "지속 턴");
                    break;

                // --- 골드 배수 ---
                case EffectType.GoldMultiplier:
                case EffectType.GoldBonus:
                    lineRect = GetLineRect(position, line++);
                    EditorGUI.PropertyField(lineRect, dynamicValueProp, new GUIContent("동적 값"), true);
                    break;

                // --- 파라미터 없는 효과 ---
                case EffectType.ShuffleDeck:
                case EffectType.IgnorePollution:
                    // 추가 필드 없음
                    break;

                // --- 기본: 주요 필드 모두 표시 ---
                default:
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, valueProp, "값");
                    lineRect = GetLineRect(position, line++);
                    EditorGUI.PropertyField(lineRect, targetProp, new GUIContent("대상"));
                    lineRect = GetLineRect(position, line++);
                    DrawFieldWithLabel(lineRect, maxTargetsProp, "최대 대상");
                    lineRect = GetLineRect(position, line++);
                    EditorGUI.PropertyField(lineRect, dynamicValueProp, new GUIContent("동적 값"), true);
                    break;
            }

            EditorGUI.EndProperty();
        }

        // =====================================================================
        // 헬퍼 메서드
        // =====================================================================

        /// <summary>
        /// 라인 위치 계산
        /// </summary>
        private Rect GetLineRect(Rect position, int lineIndex)
        {
            return new Rect(
                position.x,
                position.y + (LINE_HEIGHT + SPACING) * lineIndex,
                position.width,
                LINE_HEIGHT
            );
        }

        /// <summary>
        /// 라벨과 함께 필드 그리기
        /// </summary>
        private void DrawFieldWithLabel(Rect rect, SerializedProperty property, string label)
        {
            var labelRect = new Rect(rect.x, rect.y, LABEL_WIDTH, rect.height);
            var fieldRect = new Rect(rect.x + LABEL_WIDTH, rect.y, rect.width - LABEL_WIDTH, rect.height);

            EditorGUI.LabelField(labelRect, label);
            EditorGUI.PropertyField(fieldRect, property, GUIContent.none);
        }

        /// <summary>
        /// [E3] 카드 ID 드롭다운
        /// CardDatabaseSO에서 카드 목록 로드
        /// </summary>
        private void DrawCardIdDropdown(Rect rect, SerializedProperty cardIdProp, string label)
        {
            EnsureCardsCached();

            var labelRect = new Rect(rect.x, rect.y, LABEL_WIDTH, rect.height);
            var fieldRect = new Rect(rect.x + LABEL_WIDTH, rect.y, rect.width - LABEL_WIDTH, rect.height);

            EditorGUI.LabelField(labelRect, label);

            if (_cardIds == null || _cardIds.Length == 0)
            {
                // 카드 DB 없으면 텍스트 필드
                EditorGUI.PropertyField(fieldRect, cardIdProp, GUIContent.none);
            }
            else
            {
                // 드롭다운으로 표시
                string currentId = cardIdProp.stringValue;
                int currentIndex = System.Array.IndexOf(_cardIds, currentId);
                if (currentIndex < 0) currentIndex = 0;

                int newIndex = EditorGUI.Popup(fieldRect, currentIndex, _cardDisplayNames);
                if (newIndex != currentIndex && newIndex >= 0 && newIndex < _cardIds.Length)
                {
                    cardIdProp.stringValue = _cardIds[newIndex];
                }
            }
        }

        /// <summary>
        /// [E3] 직업 드롭다운 (GainUnit용)
        /// </summary>
        private void DrawJobDropdown(Rect rect, SerializedProperty cardIdProp, string label)
        {
            var labelRect = new Rect(rect.x, rect.y, LABEL_WIDTH, rect.height);
            var fieldRect = new Rect(rect.x + LABEL_WIDTH, rect.y, rect.width - LABEL_WIDTH, rect.height);

            EditorGUI.LabelField(labelRect, label);

            string[] jobNames = { "(선택)", "pawn", "knight", "bishop", "rook", "queen" };
            string[] jobDisplayNames = { "(선택 안함)", "폰 (Pawn)", "나이트 (Knight)", "비숍 (Bishop)", "룩 (Rook)", "퀸 (Queen)" };

            string currentId = cardIdProp.stringValue?.ToLower() ?? "";
            int currentIndex = System.Array.IndexOf(jobNames, currentId);
            if (currentIndex < 0) currentIndex = 0;

            int newIndex = EditorGUI.Popup(fieldRect, currentIndex, jobDisplayNames);
            if (newIndex != currentIndex)
            {
                cardIdProp.stringValue = newIndex > 0 ? jobNames[newIndex] : "";
            }
        }

        /// <summary>
        /// [E3] 카드 목록 캐싱
        /// </summary>
        private static void EnsureCardsCached()
        {
            if (_cardsCached) return;

            // CardDatabaseSO 찾기
            var cardDb = Resources.Load<CardDatabaseSO>("Data/CardDatabaseSO");
            if (cardDb == null)
            {
                // 에셋 검색
                var guids = AssetDatabase.FindAssets("t:CardDatabaseSO");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    cardDb = AssetDatabase.LoadAssetAtPath<CardDatabaseSO>(path);
                }
            }

            if (cardDb != null && cardDb.cards != null && cardDb.cards.Length > 0)
            {
                var ids = new List<string> { "" };  // 빈 옵션
                var names = new List<string> { "(선택 안함)" };

                foreach (var card in cardDb.cards)
                {
                    if (card != null && !string.IsNullOrEmpty(card.id))
                    {
                        ids.Add(card.id);
                        names.Add($"{card.cardName} ({card.id})");
                    }
                }

                _cardIds = ids.ToArray();
                _cardDisplayNames = names.ToArray();
            }
            else
            {
                _cardIds = new string[0];
                _cardDisplayNames = new string[0];
            }

            _cardsCached = true;
        }

        /// <summary>
        /// [E3] 캐시 무효화 (에디터에서 카드 변경 시 호출)
        /// </summary>
        public static void InvalidateCardCache()
        {
            _cardsCached = false;
            _cardIds = null;
            _cardDisplayNames = null;
        }

        /// <summary>
        /// 효과 타입별 라벨 (아이콘 포함)
        /// [E3] 이벤트 전용 타입 추가
        /// </summary>
        private string GetEffectTypeLabel(EffectType effectType)
        {
            switch (effectType)
            {
                // 기본 효과
                case EffectType.DrawCard: return "🃏 효과 타입";
                case EffectType.AddAction: return "⚡ 효과 타입";
                case EffectType.AddGold: return "💰 효과 타입";
                case EffectType.CreateTempTreasure: return "✨ 효과 타입";
                case EffectType.BoostTreasure: return "⬆️ 효과 타입";
                case EffectType.SettleCard: return "🔥 효과 타입";
                case EffectType.Gamble: return "🎲 효과 타입";
                case EffectType.DestroyCard: return "🗑️ 효과 타입";
                case EffectType.DestroyPollution: return "🧹 효과 타입";
                case EffectType.PersistentGold: return "⏱️ 효과 타입";

                // [E3] 이벤트 전용
                case EffectType.GainUnit: return "👤 효과 타입";
                case EffectType.RemoveUnit: return "💀 효과 타입";
                case EffectType.FreePromotion: return "⭐ 효과 타입";
                case EffectType.AddPromotionLevel: return "📈 효과 타입";
                case EffectType.AddCardToDeck: return "➕ 효과 타입";
                case EffectType.RemoveCardFromDeck: return "➖ 효과 타입";
                case EffectType.UpgradeCardInDeck: return "🔼 효과 타입";
                case EffectType.SpendGoldPercent: return "💸 효과 타입";
                case EffectType.PromotionDiscount: return "🏷️ 효과 타입";
                case EffectType.MaintenanceModifier: return "🏠 효과 타입";

                default: return "효과 타입";
            }
        }

        /// <summary>
        /// 효과 타입별 value 라벨
        /// </summary>
        private string GetValueLabel(EffectType effectType)
        {
            switch (effectType)
            {
                case EffectType.DrawCard: return "드로우 수";
                case EffectType.AddAction: return "액션 수";
                case EffectType.AddGold: return "골드";
                case EffectType.DrawUntil: return "목표 장수";
                default: return "값";
            }
        }
    }

    /// <summary>
    /// ConditionalEffect용 PropertyDrawer
    /// 조건과 효과를 구분하여 표시
    /// </summary>
    [CustomPropertyDrawer(typeof(ConditionalEffect))]
    public class ConditionalEffectDrawer : PropertyDrawer
    {
        private const float HEADER_HEIGHT = 20f;
        private const float SPACING = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var conditionsProp = property.FindPropertyRelative("conditions");
            var effectsProp = property.FindPropertyRelative("effects");
            var elseEffectsProp = property.FindPropertyRelative("elseEffects");

            float height = HEADER_HEIGHT; // 헤더
            height += EditorGUI.GetPropertyHeight(conditionsProp, true) + SPACING;
            height += EditorGUI.GetPropertyHeight(effectsProp, true) + SPACING;

            if (elseEffectsProp.arraySize > 0)
            {
                height += EditorGUI.GetPropertyHeight(elseEffectsProp, true) + SPACING;
            }

            return height + SPACING * 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var conditionsProp = property.FindPropertyRelative("conditions");
            var effectsProp = property.FindPropertyRelative("effects");
            var elseEffectsProp = property.FindPropertyRelative("elseEffects");

            float y = position.y;

            // 헤더
            var headerRect = new Rect(position.x, y, position.width, HEADER_HEIGHT);

            // 조건 요약 표시
            string conditionSummary = GetConditionSummary(conditionsProp);
            EditorGUI.LabelField(headerRect, conditionSummary, EditorStyles.boldLabel);
            y += HEADER_HEIGHT + SPACING;

            // 조건 배열
            var conditionsRect = new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(conditionsProp, true));
            EditorGUI.PropertyField(conditionsRect, conditionsProp, new GUIContent("발동 조건"), true);
            y += conditionsRect.height + SPACING;

            // 효과 배열
            var effectsRect = new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(effectsProp, true));
            EditorGUI.PropertyField(effectsRect, effectsProp, new GUIContent("✔ 조건 충족 시 효과"), true);
            y += effectsRect.height + SPACING;

            // else 효과 (있으면)
            if (elseEffectsProp.arraySize > 0)
            {
                var elseRect = new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(elseEffectsProp, true));
                EditorGUI.PropertyField(elseRect, elseEffectsProp, new GUIContent("✗ 조건 불충족 시 효과"), true);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// 조건 요약 텍스트 생성
        /// </summary>
        private string GetConditionSummary(SerializedProperty conditionsProp)
        {
            if (conditionsProp.arraySize == 0)
            {
                return "▶ 항상 발동";
            }

            // 첫 번째 조건만 확인
            var firstCondition = conditionsProp.GetArrayElementAtIndex(0);
            var condType = firstCondition.FindPropertyRelative("type");

            if (condType != null)
            {
                ConditionType type = (ConditionType)condType.intValue;
                if (type == ConditionType.None)
                {
                    return "▶ 항상 발동";
                }

                return $"▶ 조건부 ({conditionsProp.arraySize}개 조건)";
            }

            return "▶ 조건부 효과";
        }
    }

    /// <summary>
    /// [E3] EffectCondition용 PropertyDrawer
    /// 조건 타입에 따라 필요한 필드만 표시
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectCondition))]
    public class EffectConditionDrawer : PropertyDrawer
    {
        private const float LINE_HEIGHT = 18f;
        private const float SPACING = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var typeProp = property.FindPropertyRelative("type");
            ConditionType condType = (ConditionType)typeProp.intValue;

            // None이면 1줄, 아니면 3줄
            int lines = (condType == ConditionType.None) ? 1 : 3;
            return (LINE_HEIGHT + SPACING) * lines + SPACING;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var typeProp = property.FindPropertyRelative("type");
            var compProp = property.FindPropertyRelative("comparison");
            var valueProp = property.FindPropertyRelative("value");

            ConditionType condType = (ConditionType)typeProp.intValue;

            int line = 0;
            Rect lineRect;

            // 조건 타입
            lineRect = new Rect(position.x, position.y + (LINE_HEIGHT + SPACING) * line++, position.width, LINE_HEIGHT);
            EditorGUI.PropertyField(lineRect, typeProp, new GUIContent(GetConditionLabel(condType)));

            // None이 아니면 비교 연산자와 값 표시
            if (condType != ConditionType.None)
            {
                lineRect = new Rect(position.x, position.y + (LINE_HEIGHT + SPACING) * line++, position.width, LINE_HEIGHT);
                EditorGUI.PropertyField(lineRect, compProp, new GUIContent("비교"));

                lineRect = new Rect(position.x, position.y + (LINE_HEIGHT + SPACING) * line++, position.width, LINE_HEIGHT);
                EditorGUI.PropertyField(lineRect, valueProp, new GUIContent("값"));
            }

            EditorGUI.EndProperty();
        }

        private string GetConditionLabel(ConditionType type)
        {
            switch (type)
            {
                case ConditionType.None: return "📋 조건 없음";
                case ConditionType.GoldAbove:
                case ConditionType.GoldBelow: return "💰 골드 조건";
                case ConditionType.HandHasTreasure:
                case ConditionType.HandHasPollution:
                case ConditionType.HandHasAction: return "🃏 손패 조건";
                case ConditionType.HasUnit:
                case ConditionType.HasMultipleUnits:
                case ConditionType.HasPromotableUnit: return "👥 유닛 조건";
                case ConditionType.HasCopperInDeck:
                case ConditionType.HasPollutionInDeck: return "📦 덱 조건";
                default: return "조건 타입";
            }
        }
    }
}

#endif