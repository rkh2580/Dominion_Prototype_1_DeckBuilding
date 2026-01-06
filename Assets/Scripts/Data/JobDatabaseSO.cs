// =============================================================================
// JobDatabaseSO.cs
// 직업 데이터베이스 ScriptableObject 정의
// =============================================================================
// [E2] 전체 직업 목록을 관리하는 SO
// - 개별 JobSO 참조를 배열로 관리
// - 런타임에 Dictionary로 캐시하여 빠른 조회
// - DataLoader에서 사용
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBuildingEconomy.Data
{
    /// <summary>
    /// 직업 데이터베이스 ScriptableObject
    /// 전체 직업 목록을 관리
    /// </summary>
    [CreateAssetMenu(fileName = "JobDatabase", menuName = "DeckBuilding/Job Database")]
    public class JobDatabaseSO : ScriptableObject
    {
        // =====================================================================
        // 데이터
        // =====================================================================

        [Header("버전 정보")]
        [Tooltip("데이터 버전")]
        public string version = "1.0.0";

        [Header("직업 목록")]
        [Tooltip("전체 직업 SO 참조 목록")]
        public JobSO[] jobs;

        // =====================================================================
        // 런타임 캐시
        // =====================================================================

        private Dictionary<string, JobSO> _jobDictById;
        private Dictionary<Job, JobSO> _jobDictByEnum;
        private bool _cacheBuilt = false;

        // =====================================================================
        // 초기화
        // =====================================================================

        /// <summary>
        /// 캐시 빌드 (최초 조회 시 자동 호출)
        /// </summary>
        public void BuildCache()
        {
            _jobDictById = new Dictionary<string, JobSO>();
            _jobDictByEnum = new Dictionary<Job, JobSO>();

            if (jobs != null)
            {
                foreach (var job in jobs)
                {
                    if (job == null) continue;

                    // ID로 등록
                    if (!string.IsNullOrEmpty(job.id))
                    {
                        if (_jobDictById.ContainsKey(job.id))
                        {
                            Debug.LogWarning($"[JobDatabaseSO] 중복 직업 ID: {job.id}");
                        }
                        _jobDictById[job.id] = job;
                    }

                    // Enum으로 등록
                    if (_jobDictByEnum.ContainsKey(job.job))
                    {
                        Debug.LogWarning($"[JobDatabaseSO] 중복 직업 Enum: {job.job}");
                    }
                    _jobDictByEnum[job.job] = job;
                }
            }

            _cacheBuilt = true;
            Debug.Log($"[JobDatabaseSO] 캐시 빌드 완료 - {_jobDictByEnum.Count}개 직업");
        }

        /// <summary>
        /// 캐시 무효화 (에디터에서 데이터 변경 시)
        /// </summary>
        public void InvalidateCache()
        {
            _cacheBuilt = false;
            _jobDictById = null;
            _jobDictByEnum = null;
        }

        private void EnsureCache()
        {
            if (!_cacheBuilt) BuildCache();
        }

        // =====================================================================
        // 조회 메서드
        // =====================================================================

        /// <summary>
        /// ID로 직업 조회
        /// </summary>
        public JobSO GetJob(string id)
        {
            EnsureCache();

            if (string.IsNullOrEmpty(id)) return null;

            _jobDictById.TryGetValue(id, out var job);
            return job;
        }

        /// <summary>
        /// Enum으로 직업 조회
        /// </summary>
        public JobSO GetJob(Job jobEnum)
        {
            EnsureCache();

            _jobDictByEnum.TryGetValue(jobEnum, out var job);
            return job;
        }

        /// <summary>
        /// 직업 존재 여부 확인 (ID)
        /// </summary>
        public bool HasJob(string id)
        {
            EnsureCache();
            return !string.IsNullOrEmpty(id) && _jobDictById.ContainsKey(id);
        }

        /// <summary>
        /// 직업 존재 여부 확인 (Enum)
        /// </summary>
        public bool HasJob(Job jobEnum)
        {
            EnsureCache();
            return _jobDictByEnum.ContainsKey(jobEnum);
        }

        /// <summary>
        /// 모든 직업 ID 목록
        /// </summary>
        public string[] GetAllJobIds()
        {
            EnsureCache();
            var ids = new string[_jobDictById.Count];
            _jobDictById.Keys.CopyTo(ids, 0);
            return ids;
        }

        /// <summary>
        /// 모든 직업 Enum 목록
        /// </summary>
        public Job[] GetAllJobEnums()
        {
            EnsureCache();
            var enums = new Job[_jobDictByEnum.Count];
            _jobDictByEnum.Keys.CopyTo(enums, 0);
            return enums;
        }

        /// <summary>
        /// 직업 총 개수
        /// </summary>
        public int Count
        {
            get
            {
                EnsureCache();
                return _jobDictByEnum.Count;
            }
        }

        // =====================================================================
        // 카드풀 조회
        // =====================================================================

        /// <summary>
        /// 직업의 특정 희귀도 카드 목록 조회
        /// </summary>
        public string[] GetCardsByJobAndRarity(Job jobEnum, CardRarity rarity)
        {
            var job = GetJob(jobEnum);
            if (job == null) return new string[0];

            return job.GetCardsByRarity(rarity);
        }

        /// <summary>
        /// 직업의 모든 카드 ID 조회
        /// </summary>
        public string[] GetAllCardsByJob(Job jobEnum)
        {
            var job = GetJob(jobEnum);
            if (job == null) return new string[0];

            return job.GetAllCards();
        }

        // =====================================================================
        // 변환 메서드
        // =====================================================================

        /// <summary>
        /// SO 목록 → JobDefinition Dictionary 변환 (기존 시스템 호환용)
        /// </summary>
        public Dictionary<Job, JobDefinition> ToJobDefinitionDictionary()
        {
            var result = new Dictionary<Job, JobDefinition>();

            if (jobs != null)
            {
                foreach (var job in jobs)
                {
                    if (job == null) continue;
                    result[job.job] = job.ToJobDefinition();
                }
            }

            return result;
        }

        /// <summary>
        /// SO 목록 → JobsData 변환 (JSON 구조 호환용)
        /// </summary>
        public JobsData ToJobsData()
        {
            var result = new JobsData
            {
                version = this.version,
                jobs = new JobInfo[jobs?.Length ?? 0]
            };

            if (jobs != null)
            {
                for (int i = 0; i < jobs.Length; i++)
                {
                    if (jobs[i] != null)
                    {
                        result.jobs[i] = jobs[i].ToJobInfo();
                    }
                }
            }

            return result;
        }

        // =====================================================================
        // 유효성 검증
        // =====================================================================

        /// <summary>
        /// 전체 데이터베이스 유효성 검증
        /// </summary>
        public bool ValidateAll(out List<string> errors)
        {
            errors = new List<string>();

            if (jobs == null || jobs.Length == 0)
            {
                errors.Add("직업이 없습니다.");
                return false;
            }

            var ids = new HashSet<string>();
            var enums = new HashSet<Job>();

            for (int i = 0; i < jobs.Length; i++)
            {
                var job = jobs[i];

                if (job == null)
                {
                    errors.Add($"[{i}] null 참조");
                    continue;
                }

                // 개별 유효성 검증
                if (!job.Validate(out var jobError))
                {
                    errors.Add($"[{i}] {job.id}: {jobError}");
                }

                // ID 중복 검사
                if (!string.IsNullOrEmpty(job.id))
                {
                    if (ids.Contains(job.id))
                    {
                        errors.Add($"[{i}] 중복 ID: {job.id}");
                    }
                    else
                    {
                        ids.Add(job.id);
                    }
                }

                // Enum 중복 검사
                if (enums.Contains(job.job))
                {
                    errors.Add($"[{i}] 중복 Enum: {job.job}");
                }
                else
                {
                    enums.Add(job.job);
                }
            }

            return errors.Count == 0;
        }

        // =====================================================================
        // 에디터 헬퍼
        // =====================================================================

        /// <summary>
        /// 직업 추가 (에디터용)
        /// </summary>
        public void AddJob(JobSO job)
        {
            if (job == null) return;

            var list = new List<JobSO>(jobs ?? new JobSO[0]);
            if (!list.Contains(job))
            {
                list.Add(job);
                jobs = list.ToArray();
                InvalidateCache();
            }
        }

        /// <summary>
        /// 직업 제거 (에디터용)
        /// </summary>
        public void RemoveJob(JobSO job)
        {
            if (job == null || jobs == null) return;

            var list = new List<JobSO>(jobs);
            if (list.Remove(job))
            {
                jobs = list.ToArray();
                InvalidateCache();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 변경 시 캐시 무효화
        /// </summary>
        private void OnValidate()
        {
            InvalidateCache();
        }
#endif
    }
}
