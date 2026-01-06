// =============================================================================
// EventSOEditor.cs
// EventSO 커스텀 에디터
// =============================================================================
// [E3] 기획자 친화적 이벤트 편집 UI
// - 카테고리별 다른 레이아웃
// - 선택 이벤트 선택지 편집
// - 유효성 검증 버튼
// - 이벤트 복사/붙여넣기
// [E3-Fix] BeginFoldoutHeaderGroup 중첩 문제 해결
//          → 일반 Foldout 사용으로 변경
// 위치: Assets/Editor/EventSOEditor.cs
// =============================================================================

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using DeckBuildingEconomy.Data;
using DeckBuildingEconomy.Core;

namespace DeckBuildingEconomy.Editor
{
    /// <summary>
    /// EventSO 커스텀 인스펙터
    /// </summary>
    [CustomEditor(typeof(EventSO))]
    public class EventSOEditor : UnityEditor.Editor
    {
        // =====================================================================
        // SerializedProperty
        // =====================================================================

        private SerializedProperty eventIdProp;
        private SerializedProperty eventNameProp;
        private SerializedProperty categoryProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty triggerConditionsProp;
        private SerializedProperty effectsProp;
        private SerializedProperty choicesProp;

        // 폴드아웃 상태
        private bool showBasicInfo = true;
        private bool showTriggerConditions = true;
        private bool showEffects = true;
        private bool showChoices = true;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void OnEnable()
        {
            eventIdProp = serializedObject.FindProperty("eventId");
            eventNameProp = serializedObject.FindProperty("eventName");
            categoryProp = serializedObject.FindProperty("category");
            descriptionProp = serializedObject.FindProperty("description");
            triggerConditionsProp = serializedObject.FindProperty("triggerConditions");
            effectsProp = serializedObject.FindProperty("effects");
            choicesProp = serializedObject.FindProperty("choices");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EventSO eventSO = (EventSO)target;
            RandomEventCategory category = (RandomEventCategory)categoryProp.intValue;

            // === 헤더 ===
            DrawHeader(eventSO, category);

            EditorGUILayout.Space(10);

            // === 기본 정보 ===
            // [E3-Fix] BeginFoldoutHeaderGroup → Foldout으로 변경 (중첩 허용)
            showBasicInfo = EditorGUILayout.Foldout(showBasicInfo, "📋 기본 정보", true, EditorStyles.foldoutHeader);
            if (showBasicInfo)
            {
                EditorGUI.indentLevel++;
                DrawBasicInfo();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // === 발동 조건 ===
            showTriggerConditions = EditorGUILayout.Foldout(showTriggerConditions,
                $"🎯 발동 조건 ({triggerConditionsProp.arraySize}개)", true, EditorStyles.foldoutHeader);
            if (showTriggerConditions)
            {
                EditorGUI.indentLevel++;
                DrawTriggerConditions();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // === 카테고리별 섹션 ===
            if (category == RandomEventCategory.Choice)
            {
                // 선택 이벤트: 선택지 섹션
                showChoices = EditorGUILayout.Foldout(showChoices,
                    $"🔘 선택지 ({choicesProp.arraySize}개)", true, EditorStyles.foldoutHeader);
                if (showChoices)
                {
                    EditorGUI.indentLevel++;
                    DrawChoices();
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                // 긍정/부정 이벤트: 효과 섹션
                showEffects = EditorGUILayout.Foldout(showEffects,
                    $"✨ 즉시 효과 ({effectsProp.arraySize}개)", true, EditorStyles.foldoutHeader);
                if (showEffects)
                {
                    EditorGUI.indentLevel++;
                    DrawEffects();
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(10);

            // === 도구 버튼 ===
            DrawToolButtons(eventSO);

            serializedObject.ApplyModifiedProperties();
        }

        // =====================================================================
        // 섹션 그리기
        // =====================================================================

        /// <summary>
        /// 헤더 (이벤트 요약)
        /// </summary>
        private void DrawHeader(EventSO eventSO, RandomEventCategory category)
        {
            // 배경색
            Color bgColor = GetCategoryColor(category);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 카테고리 아이콘 + 이름
            string icon = GetCategoryIcon(category);
            string categoryName = GetCategoryDisplayName(category);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };

            EditorGUILayout.LabelField($"{icon} [{categoryName}] {eventSO.eventName}", titleStyle);

            // 요약
            GUIStyle summaryStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            string summary = eventSO.GetEffectSummary();
            if (eventSO.HasTriggerConditions)
            {
                summary = $"조건부 | {summary}";
            }

            EditorGUILayout.LabelField(summary, summaryStyle);

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 기본 정보 섹션
        /// </summary>
        private void DrawBasicInfo()
        {
            EditorGUILayout.PropertyField(eventIdProp, new GUIContent("이벤트 ID"));
            EditorGUILayout.PropertyField(eventNameProp, new GUIContent("표시 이름"));
            EditorGUILayout.PropertyField(categoryProp, new GUIContent("카테고리"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("설명", EditorStyles.boldLabel);
            descriptionProp.stringValue = EditorGUILayout.TextArea(
                descriptionProp.stringValue,
                GUILayout.MinHeight(50));
        }

        /// <summary>
        /// 발동 조건 섹션
        /// </summary>
        private void DrawTriggerConditions()
        {
            if (triggerConditionsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("조건 없음 - 항상 이벤트 후보에 포함됩니다.", MessageType.Info);
            }

            EditorGUILayout.PropertyField(triggerConditionsProp, new GUIContent("조건 목록"), true);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 조건 추가", GUILayout.Width(100)))
            {
                triggerConditionsProp.InsertArrayElementAtIndex(triggerConditionsProp.arraySize);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 효과 섹션 (긍정/부정 이벤트)
        /// </summary>
        private void DrawEffects()
        {
            if (effectsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("효과가 없습니다. 최소 1개의 효과를 추가하세요.", MessageType.Warning);
            }

            EditorGUILayout.PropertyField(effectsProp, new GUIContent("효과 목록"), true);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 효과 그룹 추가", GUILayout.Width(120)))
            {
                effectsProp.InsertArrayElementAtIndex(effectsProp.arraySize);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 선택지 섹션 (선택 이벤트)
        /// </summary>
        private void DrawChoices()
        {
            if (choicesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("선택지가 없습니다. 선택 이벤트에는 최소 1개의 선택지가 필요합니다.", MessageType.Warning);
            }

            for (int i = 0; i < choicesProp.arraySize; i++)
            {
                DrawSingleChoice(i);
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 선택지 추가", GUILayout.Width(100)))
            {
                choicesProp.InsertArrayElementAtIndex(choicesProp.arraySize);

                // 새 선택지 기본값 설정
                var newChoice = choicesProp.GetArrayElementAtIndex(choicesProp.arraySize - 1);
                newChoice.FindPropertyRelative("choiceId").stringValue = $"choice{choicesProp.arraySize}";
                newChoice.FindPropertyRelative("choiceText").stringValue = "새 선택지";
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 개별 선택지 그리기
        /// </summary>
        private void DrawSingleChoice(int index)
        {
            var choiceProp = choicesProp.GetArrayElementAtIndex(index);
            var choiceIdProp = choiceProp.FindPropertyRelative("choiceId");
            var choiceTextProp = choiceProp.FindPropertyRelative("choiceText");
            var requirementsProp = choiceProp.FindPropertyRelative("requirements");
            var choiceEffectsProp = choiceProp.FindPropertyRelative("effects");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 헤더
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"🔘 선택지 {index + 1}", EditorStyles.boldLabel);

            if (GUILayout.Button("삭제", GUILayout.Width(50)))
            {
                choicesProp.DeleteArrayElementAtIndex(index);
                return;
            }
            EditorGUILayout.EndHorizontal();

            // 기본 정보
            EditorGUILayout.PropertyField(choiceIdProp, new GUIContent("ID"));
            EditorGUILayout.PropertyField(choiceTextProp, new GUIContent("버튼 텍스트"));

            // 선택 조건 - [E3-Fix] 일반 Foldout은 중첩 가능
            EditorGUILayout.PropertyField(requirementsProp, new GUIContent("선택 가능 조건"), true);

            // 효과
            EditorGUILayout.PropertyField(choiceEffectsProp, new GUIContent("선택 시 효과"), true);

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 도구 버튼
        /// </summary>
        private void DrawToolButtons(EventSO eventSO)
        {
            EditorGUILayout.BeginHorizontal();

            // 유효성 검증
            if (GUILayout.Button("✓ 유효성 검증"))
            {
                if (eventSO.Validate(out string error))
                {
                    EditorUtility.DisplayDialog("검증 성공", "이벤트 데이터가 유효합니다.", "확인");
                }
                else
                {
                    EditorUtility.DisplayDialog("검증 실패", error, "확인");
                }
            }

            // JSON 미리보기
            if (GUILayout.Button("📄 JSON 미리보기"))
            {
                EventData data = eventSO.ToEventData();
                string json = JsonUtility.ToJson(data, true);
                Debug.Log($"[EventSO] {eventSO.eventId} JSON:\n{json}");
                EditorUtility.DisplayDialog("JSON 미리보기", "콘솔에 JSON을 출력했습니다.", "확인");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            // ID로 이름 동기화
            if (GUILayout.Button("ID → 에셋 이름"))
            {
                if (!string.IsNullOrEmpty(eventSO.eventId))
                {
                    string path = AssetDatabase.GetAssetPath(eventSO);
                    AssetDatabase.RenameAsset(path, $"Event_{eventSO.eventId}");
                    AssetDatabase.SaveAssets();
                }
            }

            // 에셋 복제
            if (GUILayout.Button("복제"))
            {
                string path = AssetDatabase.GetAssetPath(eventSO);
                string newPath = AssetDatabase.GenerateUniqueAssetPath(path);
                AssetDatabase.CopyAsset(path, newPath);
                AssetDatabase.SaveAssets();

                // 복제본 선택
                var newAsset = AssetDatabase.LoadAssetAtPath<EventSO>(newPath);
                Selection.activeObject = newAsset;
            }

            EditorGUILayout.EndHorizontal();
        }

        // =====================================================================
        // 헬퍼 메서드
        // =====================================================================

        private string GetCategoryIcon(RandomEventCategory category)
        {
            switch (category)
            {
                case RandomEventCategory.Positive: return "🌟";
                case RandomEventCategory.Negative: return "⚡";
                case RandomEventCategory.Choice: return "🔀";
                default: return "❓";
            }
        }

        private string GetCategoryDisplayName(RandomEventCategory category)
        {
            switch (category)
            {
                case RandomEventCategory.Positive: return "긍정 이벤트";
                case RandomEventCategory.Negative: return "부정 이벤트";
                case RandomEventCategory.Choice: return "선택 이벤트";
                default: return "알 수 없음";
            }
        }

        private Color GetCategoryColor(RandomEventCategory category)
        {
            switch (category)
            {
                case RandomEventCategory.Positive: return new Color(0.7f, 1f, 0.7f); // 연두
                case RandomEventCategory.Negative: return new Color(1f, 0.7f, 0.7f); // 연분홍
                case RandomEventCategory.Choice: return new Color(0.7f, 0.85f, 1f);  // 연파랑
                default: return Color.white;
            }
        }
    }

    /// <summary>
    /// [E3] EventDatabaseSO 커스텀 에디터
    /// </summary>
    [CustomEditor(typeof(EventDatabaseSO))]
    public class EventDatabaseSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EventDatabaseSO db = (EventDatabaseSO)target;

            // 통계 헤더
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 이벤트 통계", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(db.GetStatsSummary());
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 기본 인스펙터
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            // 도구 버튼
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🔍 폴더에서 수집"))
            {
                db.CollectEventsFromFolder();
            }

            if (GUILayout.Button("📋 ID 정렬"))
            {
                db.SortById();
            }

            if (GUILayout.Button("📂 카테고리 정렬"))
            {
                db.SortByCategory();
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("✓ 전체 검증"))
            {
                db.ValidateAllEvents();
            }
        }
    }
}

#endif