// =============================================================================
// EventDatabaseSO.cs
// 이벤트 데이터베이스 ScriptableObject
// =============================================================================
// [E3] 모든 EventSO를 관리하는 데이터베이스
// - 카테고리별 이벤트 조회
// - EventData Dictionary 변환
// - DataLoader에서 사용
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Core;

namespace DeckBuildingEconomy.Data
{
    /// <summary>
    /// 이벤트 데이터베이스 ScriptableObject
    /// 모든 EventSO를 관리
    /// </summary>
    [CreateAssetMenu(fileName = "EventDatabaseSO", menuName = "DeckBuilding/Database/Event Database")]
    public class EventDatabaseSO : ScriptableObject
    {
        // =====================================================================
        // 데이터
        // =====================================================================

        [Header("이벤트 목록")]
        [Tooltip("게임에서 사용하는 모든 이벤트")]
        public EventSO[] events;

        // =====================================================================
        // 변환 메서드
        // =====================================================================

        /// <summary>
        /// SO 배열 → EventData Dictionary 변환
        /// DataLoader에서 사용
        /// </summary>
        public Dictionary<string, EventData> ToEventDataDictionary()
        {
            var dict = new Dictionary<string, EventData>();

            if (events == null) return dict;

            foreach (var eventSO in events)
            {
                if (eventSO == null) continue;
                if (string.IsNullOrEmpty(eventSO.eventId))
                {
                    Debug.LogWarning($"[EventDatabaseSO] 이벤트 ID가 비어있습니다: {eventSO.name}");
                    continue;
                }

                if (dict.ContainsKey(eventSO.eventId))
                {
                    Debug.LogWarning($"[EventDatabaseSO] 중복 이벤트 ID: {eventSO.eventId}");
                    continue;
                }

                dict[eventSO.eventId] = eventSO.ToEventData();
            }

            return dict;
        }

        // =====================================================================
        // 조회 메서드
        // =====================================================================

        /// <summary>
        /// ID로 EventSO 찾기
        /// </summary>
        public EventSO GetEvent(string eventId)
        {
            if (events == null) return null;

            foreach (var evt in events)
            {
                if (evt != null && evt.eventId == eventId)
                    return evt;
            }
            return null;
        }

        /// <summary>
        /// 카테고리별 EventSO 목록 반환
        /// </summary>
        public List<EventSO> GetEventsByCategory(RandomEventCategory category)
        {
            var result = new List<EventSO>();

            if (events == null) return result;

            foreach (var evt in events)
            {
                if (evt != null && evt.category == category)
                    result.Add(evt);
            }

            return result;
        }

        /// <summary>
        /// 긍정 이벤트 목록
        /// </summary>
        public List<EventSO> GetPositiveEvents()
        {
            return GetEventsByCategory(RandomEventCategory.Positive);
        }

        /// <summary>
        /// 부정 이벤트 목록
        /// </summary>
        public List<EventSO> GetNegativeEvents()
        {
            return GetEventsByCategory(RandomEventCategory.Negative);
        }

        /// <summary>
        /// 선택 이벤트 목록
        /// </summary>
        public List<EventSO> GetChoiceEvents()
        {
            return GetEventsByCategory(RandomEventCategory.Choice);
        }

        // =====================================================================
        // 통계
        // =====================================================================

        /// <summary>
        /// 전체 이벤트 수
        /// </summary>
        public int TotalCount => events?.Length ?? 0;

        /// <summary>
        /// 긍정 이벤트 수
        /// </summary>
        public int PositiveCount => GetPositiveEvents().Count;

        /// <summary>
        /// 부정 이벤트 수
        /// </summary>
        public int NegativeCount => GetNegativeEvents().Count;

        /// <summary>
        /// 선택 이벤트 수
        /// </summary>
        public int ChoiceCount => GetChoiceEvents().Count;

        /// <summary>
        /// 통계 요약 문자열
        /// </summary>
        public string GetStatsSummary()
        {
            return $"총 {TotalCount}개 (긍정: {PositiveCount}, 부정: {NegativeCount}, 선택: {ChoiceCount})";
        }

        // =====================================================================
        // 유효성 검증
        // =====================================================================

        /// <summary>
        /// 전체 데이터 유효성 검증
        /// </summary>
        public bool ValidateAll(out List<string> errors)
        {
            errors = new List<string>();

            if (events == null || events.Length == 0)
            {
                errors.Add("이벤트가 없습니다.");
                return false;
            }

            var seenIds = new HashSet<string>();

            for (int i = 0; i < events.Length; i++)
            {
                var evt = events[i];

                if (evt == null)
                {
                    errors.Add($"[{i}] null 참조");
                    continue;
                }

                // 개별 검증
                if (!evt.Validate(out string error))
                {
                    errors.Add($"[{i}] {evt.name}: {error}");
                }

                // 중복 ID 검사
                if (!string.IsNullOrEmpty(evt.eventId))
                {
                    if (seenIds.Contains(evt.eventId))
                    {
                        errors.Add($"[{i}] {evt.name}: 중복 ID '{evt.eventId}'");
                    }
                    else
                    {
                        seenIds.Add(evt.eventId);
                    }
                }
            }

            return errors.Count == 0;
        }

        // =====================================================================
        // 에디터 유틸리티
        // =====================================================================

#if UNITY_EDITOR
        /// <summary>
        /// 폴더에서 모든 EventSO 자동 수집
        /// </summary>
        [ContextMenu("Collect Events from Folder")]
        public void CollectEventsFromFolder()
        {
            string path = UnityEditor.AssetDatabase.GetAssetPath(this);
            string folder = System.IO.Path.GetDirectoryName(path);

            var guids = UnityEditor.AssetDatabase.FindAssets("t:EventSO", new[] { folder });
            var collected = new List<EventSO>();

            foreach (var guid in guids)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var eventSO = UnityEditor.AssetDatabase.LoadAssetAtPath<EventSO>(assetPath);
                if (eventSO != null)
                {
                    collected.Add(eventSO);
                }
            }

            events = collected.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[EventDatabaseSO] {collected.Count}개 이벤트 수집 완료");
        }

        /// <summary>
        /// ID 기준 정렬
        /// </summary>
        [ContextMenu("Sort by ID")]
        public void SortById()
        {
            if (events == null) return;

            System.Array.Sort(events, (a, b) =>
            {
                if (a == null) return 1;
                if (b == null) return -1;
                return string.Compare(a.eventId, b.eventId, System.StringComparison.Ordinal);
            });

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[EventDatabaseSO] ID 기준 정렬 완료");
        }

        /// <summary>
        /// 카테고리 기준 정렬
        /// </summary>
        [ContextMenu("Sort by Category")]
        public void SortByCategory()
        {
            if (events == null) return;

            System.Array.Sort(events, (a, b) =>
            {
                if (a == null) return 1;
                if (b == null) return -1;

                int catCompare = ((int)a.category).CompareTo((int)b.category);
                if (catCompare != 0) return catCompare;

                return string.Compare(a.eventId, b.eventId, System.StringComparison.Ordinal);
            });

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[EventDatabaseSO] 카테고리 기준 정렬 완료");
        }

        /// <summary>
        /// 유효성 검증 실행
        /// </summary>
        [ContextMenu("Validate All Events")]
        public void ValidateAllEvents()
        {
            if (ValidateAll(out var errors))
            {
                Debug.Log($"[EventDatabaseSO] 검증 통과! {GetStatsSummary()}");
            }
            else
            {
                Debug.LogError($"[EventDatabaseSO] 검증 실패: {errors.Count}개 오류");
                foreach (var error in errors)
                {
                    Debug.LogError($"  - {error}");
                }
            }
        }
#endif
    }
}
