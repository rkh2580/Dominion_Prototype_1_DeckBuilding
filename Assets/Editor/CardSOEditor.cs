// =============================================================================
// CardSOEditor.cs
// CardSO용 커스텀 에디터
// =============================================================================
// [E2] 카드 타입별 필드 표시/숨김
// - Treasure: treasureGrade, goldValue만 표시
// - Action: rarity, jobPools, effects만 표시
// - Pollution: pollutionType, effects만 표시
// 위치: Assets/Editor/CardSOEditor.cs
// =============================================================================

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Editor
{
    /// <summary>
    /// CardSO 커스텀 인스펙터
    /// 카드 타입에 따라 관련 필드만 표시
    /// </summary>
    [CustomEditor(typeof(CardSO))]
    public class CardSOEditor : UnityEditor.Editor
    {
        // =====================================================================
        // SerializedProperty 캐시
        // =====================================================================

        // 기본 정보
        private SerializedProperty _id;
        private SerializedProperty _cardName;
        private SerializedProperty _cardType;
        private SerializedProperty _description;

        // 재화 카드용
        private SerializedProperty _treasureGrade;
        private SerializedProperty _goldValue;

        // 액션 카드용
        private SerializedProperty _rarity;
        private SerializedProperty _jobPools;

        // 오염 카드용
        private SerializedProperty _pollutionType;

        // 효과
        private SerializedProperty _effects;

        // =====================================================================
        // 스타일
        // =====================================================================

        private GUIStyle _headerStyle;
        private GUIStyle _boxStyle;
        private bool _stylesInitialized;

        // =====================================================================
        // 초기화
        // =====================================================================

        private void OnEnable()
        {
            // 프로퍼티 캐시
            _id = serializedObject.FindProperty("id");
            _cardName = serializedObject.FindProperty("cardName");
            _cardType = serializedObject.FindProperty("cardType");
            _description = serializedObject.FindProperty("description");

            _treasureGrade = serializedObject.FindProperty("treasureGrade");
            _goldValue = serializedObject.FindProperty("goldValue");

            _rarity = serializedObject.FindProperty("rarity");
            _jobPools = serializedObject.FindProperty("jobPools");

            _pollutionType = serializedObject.FindProperty("pollutionType");

            _effects = serializedObject.FindProperty("effects");
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 10, 5)
            };

            _boxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(10, 10, 5, 5),
                margin = new RectOffset(0, 0, 5, 5)
            };

            _stylesInitialized = true;
        }

        // =====================================================================
        // Inspector GUI
        // =====================================================================

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            CardSO cardSO = (CardSO)target;
            CardType currentType = cardSO.cardType;

            // === 기본 정보 섹션 ===
            DrawBasicInfoSection(currentType);

            EditorGUILayout.Space(10);

            // === 타입별 전용 섹션 ===
            switch (currentType)
            {
                case CardType.Treasure:
                    DrawTreasureSection();
                    break;

                case CardType.Action:
                    DrawActionSection();
                    DrawEffectsSection();
                    break;

                case CardType.Pollution:
                    DrawPollutionSection();
                    DrawEffectsSection();
                    break;
            }

            EditorGUILayout.Space(10);

            // === 유효성 검증 ===
            DrawValidationSection(cardSO);

            // === 효과 요약 ===
            DrawEffectSummary(cardSO);

            serializedObject.ApplyModifiedProperties();
        }

        // =====================================================================
        // 섹션별 그리기
        // =====================================================================

        /// <summary>
        /// 기본 정보 섹션
        /// </summary>
        private void DrawBasicInfoSection(CardType currentType)
        {
            EditorGUILayout.LabelField("기본 정보", _headerStyle);

            EditorGUILayout.BeginVertical(_boxStyle);
            {
                EditorGUILayout.PropertyField(_id, new GUIContent("ID", "카드 고유 식별자"));
                EditorGUILayout.PropertyField(_cardName, new GUIContent("이름", "표시되는 카드 이름"));

                // 카드 타입 - 변경 시 경고
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_cardType, new GUIContent("카드 타입"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    // 타입 변경 시 인스펙터 새로고침
                    Repaint();
                }

                // 타입 표시 배지
                DrawTypeBadge(currentType);

                EditorGUILayout.PropertyField(_description, new GUIContent("설명", "카드 효과 설명"));
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 타입 배지 표시
        /// </summary>
        private void DrawTypeBadge(CardType cardType)
        {
            Color badgeColor;
            string badgeText;

            switch (cardType)
            {
                case CardType.Treasure:
                    badgeColor = new Color(1f, 0.84f, 0f); // 금색
                    badgeText = "💰 재화 카드";
                    break;
                case CardType.Action:
                    badgeColor = new Color(0.4f, 0.7f, 1f); // 파란색
                    badgeText = "⚡ 액션 카드";
                    break;
                case CardType.Pollution:
                    badgeColor = new Color(0.6f, 0.3f, 0.6f); // 보라색
                    badgeText = "☠ 오염 카드";
                    break;
                default:
                    badgeColor = Color.gray;
                    badgeText = "? 알 수 없음";
                    break;
            }

            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = badgeColor;
            EditorGUILayout.HelpBox(badgeText, MessageType.None);
            GUI.backgroundColor = originalColor;
        }

        /// <summary>
        /// 재화 카드 전용 섹션
        /// </summary>
        private void DrawTreasureSection()
        {
            EditorGUILayout.LabelField("재화 카드 설정", _headerStyle);

            EditorGUILayout.BeginVertical(_boxStyle);
            {
                EditorGUILayout.PropertyField(_treasureGrade, new GUIContent("재화 등급", "동화, 은화, 금화 등"));
                EditorGUILayout.PropertyField(_goldValue, new GUIContent("골드 값", "이 카드의 골드 가치"));

                // 등급과 골드 값 일치 확인
                // [E2 수정] enumValueIndex가 아닌 intValue 사용 (TreasureGrade는 1부터 시작)
                TreasureGrade grade = (TreasureGrade)_treasureGrade.intValue;

                // 유효한 등급인지 확인
                if (System.Enum.IsDefined(typeof(TreasureGrade), grade))
                {
                    int expectedGold = TreasureGradeUtil.GetGoldValue(grade);
                    int actualGold = _goldValue.intValue;

                    if (actualGold != expectedGold)
                    {
                        EditorGUILayout.HelpBox(
                            $"경고: {grade} 등급의 기본 골드 값은 {expectedGold}입니다. (현재: {actualGold})",
                            MessageType.Warning);

                        if (GUILayout.Button("기본값으로 수정"))
                        {
                            _goldValue.intValue = expectedGold;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"잘못된 등급 값: {_treasureGrade.intValue}. Copper(1)~Diamond(7) 사이여야 합니다.",
                        MessageType.Error);
                }
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 액션 카드 전용 섹션
        /// </summary>
        private void DrawActionSection()
        {
            EditorGUILayout.LabelField("액션 카드 설정", _headerStyle);

            EditorGUILayout.BeginVertical(_boxStyle);
            {
                EditorGUILayout.PropertyField(_rarity, new GUIContent("희귀도", "기본, 고급, 희귀 등"));
                EditorGUILayout.PropertyField(_jobPools, new GUIContent("직업풀", "이 카드가 속한 직업들"), true);

                // 직업풀 요약
                if (_jobPools.arraySize > 0)
                {
                    var jobs = new System.Collections.Generic.List<string>();
                    for (int i = 0; i < _jobPools.arraySize; i++)
                    {
                        // [E2 수정] enumValueIndex 대신 intValue 사용 (Job은 -1부터 시작)
                        var job = (Job)_jobPools.GetArrayElementAtIndex(i).intValue;
                        jobs.Add(job.ToString());
                    }
                    EditorGUILayout.HelpBox($"소속 직업: {string.Join(", ", jobs)}", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("범용 카드 (모든 직업 사용 가능)", MessageType.Info);
                }
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 오염 카드 전용 섹션
        /// </summary>
        private void DrawPollutionSection()
        {
            EditorGUILayout.LabelField("오염 카드 설정", _headerStyle);

            EditorGUILayout.BeginVertical(_boxStyle);
            {
                EditorGUILayout.PropertyField(_pollutionType, new GUIContent("오염 종류", "부채, 저주, 질병, 파손"));

                // 오염 효과 설명
                // [E2 수정] 일관성을 위해 intValue 사용
                PollutionType pType = (PollutionType)_pollutionType.intValue;
                string effectDesc = GetPollutionDescription(pType);
                EditorGUILayout.HelpBox(effectDesc, MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 오염 타입별 설명
        /// </summary>
        private string GetPollutionDescription(PollutionType pollutionType)
        {
            switch (pollutionType)
            {
                case PollutionType.Debt:
                    return "부채: 손패만 차지하는 카드";
                case PollutionType.Curse:
                    return "저주: 턴 종료 시 -2 골드";
                case PollutionType.Disease:
                    return "질병: 해당 유닛 강화 불가";
                case PollutionType.Damage:
                    return "파손: 이번 턴 드로우 -1";
                default:
                    return "알 수 없는 오염 타입";
            }
        }

        /// <summary>
        /// 효과 섹션
        /// </summary>
        private void DrawEffectsSection()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("카드 효과", _headerStyle);

            EditorGUILayout.BeginVertical(_boxStyle);
            {
                EditorGUILayout.PropertyField(_effects, new GUIContent("조건부 효과 목록"), true);

                if (_effects.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("효과가 없습니다. 효과를 추가하세요.", MessageType.Info);
                }
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 유효성 검증 섹션
        /// </summary>
        private void DrawValidationSection(CardSO cardSO)
        {
            if (!cardSO.Validate(out string error))
            {
                EditorGUILayout.HelpBox($"유효성 오류: {error}", MessageType.Error);
            }
        }

        /// <summary>
        /// 효과 요약 표시
        /// </summary>
        private void DrawEffectSummary(CardSO cardSO)
        {
            string summary = cardSO.GetEffectSummary();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("효과 요약", _headerStyle);

            EditorGUILayout.BeginVertical(_boxStyle);
            {
                EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndVertical();
        }
    }
}

#endif