// =============================================================================
// JobSO.cs
// 직업 ScriptableObject 정의
// =============================================================================
// [E2] 개별 직업을 SO로 관리
// - JSON의 JobInfo를 SO로 변환
// - 에디터에서 직접 수정 가능
// - JobDatabaseSO에서 참조
// [E3] startingCards 필드 추가
// - 직업별 기본 시작 카드 정의
// - UnitSystem.CreateUnit()에서 자동 부여
// - Default.json 의존 제거
// =============================================================================

using System;
using UnityEngine;

namespace DeckBuildingEconomy.Data
{
    /// <summary>
    /// 직업 카드풀 데이터
    /// 희귀도별 카드 ID 목록
    /// </summary>
    [Serializable]
    public class JobCardPoolData
    {
        [Tooltip("기본 카드 ID 목록 (60% 등장)")]
        public string[] basic;

        [Tooltip("고급 카드 ID 목록 (35% 등장)")]
        public string[] advanced;

        [Tooltip("희귀 카드 ID 목록 (5% 등장)")]
        public string[] rare;

        /// <summary>
        /// 특정 희귀도의 카드 목록 반환
        /// </summary>
        public string[] GetCardsByRarity(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Basic:
                    return basic ?? new string[0];
                case CardRarity.Advanced:
                    return advanced ?? new string[0];
                case CardRarity.Rare:
                case CardRarity.SuperRare:
                case CardRarity.Legendary:
                    return rare ?? new string[0];
                default:
                    return new string[0];
            }
        }

        /// <summary>
        /// 모든 카드 ID 반환
        /// </summary>
        public string[] GetAllCards()
        {
            var all = new System.Collections.Generic.List<string>();
            if (basic != null) all.AddRange(basic);
            if (advanced != null) all.AddRange(advanced);
            if (rare != null) all.AddRange(rare);
            return all.ToArray();
        }

        /// <summary>
        /// 깊은 복사
        /// </summary>
        public JobCardPoolData Clone()
        {
            return new JobCardPoolData
            {
                basic = basic != null ? (string[])basic.Clone() : null,
                advanced = advanced != null ? (string[])advanced.Clone() : null,
                rare = rare != null ? (string[])rare.Clone() : null
            };
        }
    }

    /// <summary>
    /// 직업 ScriptableObject
    /// 개별 직업 데이터를 에셋으로 관리
    /// </summary>
    [CreateAssetMenu(fileName = "NewJob", menuName = "DeckBuilding/Job")]
    public class JobSO : ScriptableObject
    {
        // =====================================================================
        // 기본 정보
        // =====================================================================

        [Header("기본 정보")]
        [Tooltip("직업 ID (예: Pawn, Knight, Bishop)")]
        public string id;

        [Tooltip("직업 열거형 값")]
        public Job job;

        [Tooltip("표시 이름 (예: 폰, 나이트, 비숍)")]
        public string displayName;

        [TextArea(2, 4)]
        [Tooltip("직업 설명")]
        public string description;

        // =====================================================================
        // 능력치
        // =====================================================================

        [Header("능력치")]
        [Tooltip("기본 전투력")]
        [Range(5, 50)]
        public int baseCombatPower = 10;

        // =====================================================================
        // [E3] 시작 카드
        // =====================================================================

        [Header("시작 카드")]
        [Tooltip("유닛 생성 시 자동 부여되는 기본 카드 ID 목록\n(유지비에 반영됨)")]
        public string[] startingCards;

        // =====================================================================
        // 카드풀
        // =====================================================================

        [Header("카드풀 (전직 시 획득 가능)")]
        [Tooltip("직업별 카드 목록")]
        public JobCardPoolData cardPools;

        // =====================================================================
        // [E3] 시작 카드 헬퍼
        // =====================================================================

        /// <summary>
        /// 시작 카드 개수 반환
        /// </summary>
        public int StartingCardCount => startingCards?.Length ?? 0;

        /// <summary>
        /// 시작 카드 유효성 검증
        /// </summary>
        public bool HasStartingCards => startingCards != null && startingCards.Length > 0;

        // =====================================================================
        // 변환 메서드
        // =====================================================================

        /// <summary>
        /// SO → JobDefinition 변환 (기존 시스템 호환용)
        /// </summary>
        public JobDefinition ToJobDefinition()
        {
            return new JobDefinition
            {
                job = this.job,
                displayName = this.displayName,
                description = this.description,
                cardPoolBasic = cardPools?.basic != null ? new System.Collections.Generic.List<string>(cardPools.basic) : new System.Collections.Generic.List<string>(),
                cardPoolAdvanced = cardPools?.advanced != null ? new System.Collections.Generic.List<string>(cardPools.advanced) : new System.Collections.Generic.List<string>(),
                cardPoolRare = cardPools?.rare != null ? new System.Collections.Generic.List<string>(cardPools.rare) : new System.Collections.Generic.List<string>()
            };
        }

        /// <summary>
        /// SO → JobInfo 변환 (JSON 구조 호환용)
        /// </summary>
        public JobInfo ToJobInfo()
        {
            return new JobInfo
            {
                id = this.id,
                enumValue = (int)this.job,
                displayName = this.displayName,
                description = this.description,
                baseCombatPower = this.baseCombatPower,
                cardPools = new JobCardPools
                {
                    basic = cardPools?.basic != null ? (string[])cardPools.basic.Clone() : new string[0],
                    advanced = cardPools?.advanced != null ? (string[])cardPools.advanced.Clone() : new string[0],
                    rare = cardPools?.rare != null ? (string[])cardPools.rare.Clone() : new string[0]
                }
            };
        }

        /// <summary>
        /// JobInfo → SO로 데이터 복사
        /// </summary>
        public void FromJobInfo(JobInfo info)
        {
            if (info == null) return;

            this.id = info.id;
            this.job = (Job)info.enumValue;
            this.displayName = info.displayName;
            this.description = info.description;
            this.baseCombatPower = info.baseCombatPower;

            if (info.cardPools != null)
            {
                this.cardPools = new JobCardPoolData
                {
                    basic = info.cardPools.basic != null ? (string[])info.cardPools.basic.Clone() : null,
                    advanced = info.cardPools.advanced != null ? (string[])info.cardPools.advanced.Clone() : null,
                    rare = info.cardPools.rare != null ? (string[])info.cardPools.rare.Clone() : null
                };
            }
        }

        /// <summary>
        /// JobDefinition → SO로 데이터 복사
        /// </summary>
        public void FromJobDefinition(JobDefinition def)
        {
            if (def == null) return;

            this.id = def.job.ToString();
            this.job = def.job;
            this.displayName = def.displayName;
            this.description = def.description;
            this.baseCombatPower = 10; // JobDefinition에는 combatPower가 없으므로 기본값

            this.cardPools = new JobCardPoolData
            {
                basic = def.cardPoolBasic != null ? def.cardPoolBasic.ToArray() : null,
                advanced = def.cardPoolAdvanced != null ? def.cardPoolAdvanced.ToArray() : null,
                rare = def.cardPoolRare != null ? def.cardPoolRare.ToArray() : null
            };
        }

        // =====================================================================
        // 유효성 검증
        // =====================================================================

        /// <summary>
        /// 직업 데이터 유효성 검증
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

            // 표시 이름 필수
            if (string.IsNullOrEmpty(displayName))
            {
                error = "표시 이름이 비어있습니다.";
                return false;
            }

            // 전투력 범위 확인
            if (baseCombatPower <= 0)
            {
                error = "기본 전투력이 0 이하입니다.";
                return false;
            }

            // 카드풀 확인 (최소 1개 카드 필요)
            if (cardPools == null ||
                (cardPools.basic == null || cardPools.basic.Length == 0) &&
                (cardPools.advanced == null || cardPools.advanced.Length == 0) &&
                (cardPools.rare == null || cardPools.rare.Length == 0))
            {
                error = "카드풀에 카드가 없습니다.";
                return false;
            }

            return true;
        }

        // =====================================================================
        // 헬퍼 메서드
        // =====================================================================

        /// <summary>
        /// 특정 희귀도의 카드 목록 반환
        /// </summary>
        public string[] GetCardsByRarity(CardRarity rarity)
        {
            return cardPools?.GetCardsByRarity(rarity) ?? new string[0];
        }

        /// <summary>
        /// 모든 카드 ID 반환
        /// </summary>
        public string[] GetAllCards()
        {
            return cardPools?.GetAllCards() ?? new string[0];
        }

        /// <summary>
        /// 카드풀 요약 텍스트
        /// </summary>
        public string GetCardPoolSummary()
        {
            if (cardPools == null) return "(카드풀 없음)";

            int basic = cardPools.basic?.Length ?? 0;
            int advanced = cardPools.advanced?.Length ?? 0;
            int rare = cardPools.rare?.Length ?? 0;

            return $"기본: {basic}, 고급: {advanced}, 희귀: {rare}";
        }

        /// <summary>
        /// [E3] 시작 카드 요약 텍스트
        /// </summary>
        public string GetStartingCardsSummary()
        {
            if (!HasStartingCards) return "(시작 카드 없음)";
            return string.Join(", ", startingCards);
        }
    }
}