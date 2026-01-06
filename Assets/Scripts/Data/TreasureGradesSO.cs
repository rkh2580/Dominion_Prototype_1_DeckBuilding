// =============================================================================
// TreasureGradesSO.cs
// 재화 등급 ScriptableObject
// =============================================================================
// [E1] JSON 기반 설정을 SO로 전환
// - 에디터에서 직접 수정 가능
// - 기존 TreasureGradeUtil 정적 클래스 API 유지
// =============================================================================

using System;
using UnityEngine;

namespace DeckBuildingEconomy.Data
{
    /// <summary>
    /// 재화 등급 ScriptableObject
    /// TreasureGrades.json의 SO 버전
    /// </summary>
    [CreateAssetMenu(fileName = "TreasureGradesSO", menuName = "DeckBuilding/TreasureGradesSO")]
    public class TreasureGradesSO : ScriptableObject
    {
        [Header("버전 정보")]
        public string version = "1.0.0";

        [Header("재화 등급 목록")]
        public TreasureGradeEntry[] grades = new TreasureGradeEntry[]
        {
            new TreasureGradeEntry { level = 1, id = "copper", grade = TreasureGrade.Copper, displayName = "동화", goldValue = 1, color = new Color(0.8f, 0.5f, 0.2f) },
            new TreasureGradeEntry { level = 2, id = "silver", grade = TreasureGrade.Silver, displayName = "은화", goldValue = 2, color = new Color(0.75f, 0.75f, 0.75f) },
            new TreasureGradeEntry { level = 3, id = "gold_coin", grade = TreasureGrade.Gold, displayName = "금화", goldValue = 4, color = new Color(1f, 0.84f, 0f) },
            new TreasureGradeEntry { level = 4, id = "emerald", grade = TreasureGrade.Emerald, displayName = "에메랄드", goldValue = 7, color = new Color(0.31f, 0.78f, 0.47f) },
            new TreasureGradeEntry { level = 5, id = "sapphire", grade = TreasureGrade.Sapphire, displayName = "사파이어", goldValue = 12, color = new Color(0.06f, 0.32f, 0.73f) },
            new TreasureGradeEntry { level = 6, id = "ruby", grade = TreasureGrade.Ruby, displayName = "루비", goldValue = 20, color = new Color(0.88f, 0.07f, 0.37f) },
            new TreasureGradeEntry { level = 7, id = "diamond", grade = TreasureGrade.Diamond, displayName = "다이아몬드", goldValue = 35, color = new Color(0.73f, 0.95f, 1f) }
        };

        // =====================================================================
        // 조회 메서드
        // =====================================================================

        /// <summary>
        /// TreasureGrade enum으로 등급 정보 조회
        /// </summary>
        public TreasureGradeEntry GetGradeEntry(TreasureGrade grade)
        {
            foreach (var entry in grades)
            {
                if (entry.grade == grade)
                    return entry;
            }
            return null;
        }

        /// <summary>
        /// 등급별 골드 값 조회
        /// </summary>
        public int GetGoldValue(TreasureGrade grade)
        {
            var entry = GetGradeEntry(grade);
            return entry?.goldValue ?? 0;
        }

        /// <summary>
        /// 등급별 이름 조회
        /// </summary>
        public string GetDisplayName(TreasureGrade grade)
        {
            var entry = GetGradeEntry(grade);
            return entry?.displayName ?? "알 수 없음";
        }

        /// <summary>
        /// 등급별 색상 조회
        /// </summary>
        public Color GetColor(TreasureGrade grade)
        {
            var entry = GetGradeEntry(grade);
            return entry?.color ?? Color.white;
        }

        /// <summary>
        /// 등급별 색상 Hex 조회
        /// </summary>
        public string GetColorHex(TreasureGrade grade)
        {
            var entry = GetGradeEntry(grade);
            return entry?.GetColorHex() ?? "#FFFFFF";
        }

        /// <summary>
        /// 다음 등급 조회 (최대면 null)
        /// </summary>
        public TreasureGrade? GetNextGrade(TreasureGrade grade)
        {
            if (grade == TreasureGrade.Diamond) return null;
            return (TreasureGrade)((int)grade + 1);
        }

        /// <summary>
        /// ID로 등급 조회
        /// </summary>
        public TreasureGradeEntry GetGradeById(string id)
        {
            foreach (var entry in grades)
            {
                if (entry.id == id)
                    return entry;
            }
            return null;
        }

        // =====================================================================
        // TreasureGradesData 변환 (호환성)
        // =====================================================================

        /// <summary>
        /// 기존 TreasureGradesData로 변환 (레거시 호환)
        /// </summary>
        public TreasureGradesData ToTreasureGradesData()
        {
            var data = new TreasureGradesData
            {
                version = version,
                grades = new TreasureGradeInfo[grades.Length],
                upgradeChain = new string[grades.Length]
            };

            for (int i = 0; i < grades.Length; i++)
            {
                data.grades[i] = grades[i].ToTreasureGradeInfo();
                data.upgradeChain[i] = grades[i].id;
            }

            return data;
        }

        /// <summary>
        /// TreasureGradesData에서 값 복사
        /// </summary>
        public void FromTreasureGradesData(TreasureGradesData data)
        {
            if (data == null) return;

            version = data.version ?? "1.0.0";

            if (data.grades != null && data.grades.Length > 0)
            {
                grades = new TreasureGradeEntry[data.grades.Length];
                for (int i = 0; i < data.grades.Length; i++)
                {
                    grades[i] = TreasureGradeEntry.FromTreasureGradeInfo(data.grades[i]);
                }
            }
        }
    }

    /// <summary>
    /// 단일 재화 등급 정보
    /// </summary>
    [Serializable]
    public class TreasureGradeEntry
    {
        [Tooltip("등급 레벨 (1~7)")]
        public int level;

        [Tooltip("고유 ID")]
        public string id;

        [Tooltip("TreasureGrade enum 값")]
        public TreasureGrade grade;

        [Tooltip("표시 이름")]
        public string displayName;

        [Tooltip("골드 값")]
        public int goldValue;

        [Tooltip("표시 색상")]
        public Color color = Color.white;

        /// <summary>
        /// 색상 Hex 문자열 반환
        /// </summary>
        public string GetColorHex()
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        /// <summary>
        /// 색상 Hex 문자열에서 설정
        /// </summary>
        public void SetColorFromHex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color parsed))
            {
                color = parsed;
            }
        }

        /// <summary>
        /// 기존 TreasureGradeInfo로 변환
        /// </summary>
        public TreasureGradeInfo ToTreasureGradeInfo()
        {
            return new TreasureGradeInfo
            {
                level = level,
                id = id,
                enumValue = (int)grade,
                name = displayName,
                goldValue = goldValue,
                color = GetColorHex()
            };
        }

        /// <summary>
        /// TreasureGradeInfo에서 생성
        /// </summary>
        public static TreasureGradeEntry FromTreasureGradeInfo(TreasureGradeInfo info)
        {
            var entry = new TreasureGradeEntry
            {
                level = info.level,
                id = info.id,
                grade = (TreasureGrade)info.enumValue,
                displayName = info.name,
                goldValue = info.goldValue
            };
            entry.SetColorFromHex(info.color);
            return entry;
        }
    }
}
