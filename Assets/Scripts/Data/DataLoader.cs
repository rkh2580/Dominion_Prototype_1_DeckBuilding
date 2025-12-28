// =============================================================================
// DataLoader.cs
// JSON 파일 로드 및 데이터 관리
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeckBuildingEconomy.Data
{
    /// <summary>
    /// 게임 데이터 로더 및 관리자
    /// JSON 파일에서 데이터를 로드하고 Dictionary로 관리
    /// </summary>
    public class DataLoader : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================
        
        public static DataLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadAllData();
        }

        // =====================================================================
        // 데이터 저장소
        // =====================================================================

        /// <summary>
        /// 카드 데이터 (id → CardData)
        /// </summary>
        public Dictionary<string, CardData> Cards { get; private set; }

        /// <summary>
        /// 직업 정의 (Job → JobDefinition)
        /// </summary>
        public Dictionary<Job, JobDefinition> Jobs { get; private set; }

        /// <summary>
        /// 이벤트 데이터 (id → EventData)
        /// </summary>
        public Dictionary<string, EventData> Events { get; private set; }

        // =====================================================================
        // 데이터 로드
        // =====================================================================

        /// <summary>
        /// 모든 데이터 로드
        /// </summary>
        private void LoadAllData()
        {
            LoadCards();
            LoadJobs();
            LoadEvents();

            Debug.Log($"[DataLoader] 로드 완료 - 카드: {Cards.Count}종, 직업: {Jobs.Count}종, 이벤트: {Events.Count}종");
        }

        /// <summary>
        /// 카드 데이터 로드
        /// </summary>
        private void LoadCards()
        {
            Cards = new Dictionary<string, CardData>();

            // Resources/Data/Cards.json 로드
            TextAsset jsonFile = Resources.Load<TextAsset>("Data/Cards");
            if (jsonFile == null)
            {
                Debug.LogWarning("[DataLoader] Cards.json 파일을 찾을 수 없습니다. 기본 데이터를 사용합니다.");
                LoadDefaultCards();
                return;
            }

            try
            {
                CardDataList dataList = JsonUtility.FromJson<CardDataList>(jsonFile.text);
                foreach (var card in dataList.cards)
                {
                    Cards[card.id] = card;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataLoader] Cards.json 파싱 실패: {e.Message}");
                LoadDefaultCards();
            }
        }

        /// <summary>
        /// 직업 데이터 로드
        /// </summary>
        private void LoadJobs()
        {
            Jobs = new Dictionary<Job, JobDefinition>();

            TextAsset jsonFile = Resources.Load<TextAsset>("Data/Jobs");
            if (jsonFile == null)
            {
                Debug.LogWarning("[DataLoader] Jobs.json 파일을 찾을 수 없습니다. 기본 데이터를 사용합니다.");
                LoadDefaultJobs();
                return;
            }

            try
            {
                JobDefinitionList dataList = JsonUtility.FromJson<JobDefinitionList>(jsonFile.text);
                foreach (var job in dataList.jobs)
                {
                    Jobs[job.job] = job;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataLoader] Jobs.json 파싱 실패: {e.Message}");
                LoadDefaultJobs();
            }
        }

        /// <summary>
        /// 이벤트 데이터 로드
        /// </summary>
        private void LoadEvents()
        {
            Events = new Dictionary<string, EventData>();

            TextAsset jsonFile = Resources.Load<TextAsset>("Data/Events");
            if (jsonFile == null)
            {
                Debug.LogWarning("[DataLoader] Events.json 파일을 찾을 수 없습니다. 기본 데이터를 사용합니다.");
                LoadDefaultEvents();
                return;
            }

            try
            {
                EventDataList dataList = JsonUtility.FromJson<EventDataList>(jsonFile.text);
                foreach (var evt in dataList.events)
                {
                    Events[evt.eventId] = evt;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataLoader] Events.json 파싱 실패: {e.Message}");
                LoadDefaultEvents();
            }
        }

        // =====================================================================
        // 기본 데이터 (JSON 없을 때 폴백)
        // =====================================================================

        /// <summary>
        /// 기본 카드 데이터 (시작덱 + 필수 카드)
        /// </summary>
        private void LoadDefaultCards()
        {
            Cards = new Dictionary<string, CardData>();

            // === 재화 카드 ===
            Cards["copper"] = new CardData
            {
                id = "copper",
                cardName = "동화",
                cardType = CardType.Treasure,
                description = "1 골드",
                treasureGrade = TreasureGrade.Copper,
                goldValue = 1
            };

            Cards["silver"] = new CardData
            {
                id = "silver",
                cardName = "은화",
                cardType = CardType.Treasure,
                description = "2 골드",
                treasureGrade = TreasureGrade.Silver,
                goldValue = 2
            };

            Cards["gold_coin"] = new CardData
            {
                id = "gold_coin",
                cardName = "금화",
                cardType = CardType.Treasure,
                description = "4 골드",
                treasureGrade = TreasureGrade.Gold,
                goldValue = 4
            };

            // === 폰 기본 카드 ===
            Cards["labor"] = new CardData
            {
                id = "labor",
                cardName = "노동",
                cardType = CardType.Action,
                description = "+3 골드",
                rarity = CardRarity.Basic,
                jobPools = new[] { Job.Pawn },
                effects = new[]
                {
                    new CardEffect { effectType = EffectType.AddGold, value = 3 }
                }
            };

            // === 나이트 기본 카드 ===
            Cards["explore"] = new CardData
            {
                id = "explore",
                cardName = "탐색",
                cardType = CardType.Action,
                description = "+2 카드",
                rarity = CardRarity.Basic,
                jobPools = new[] { Job.Knight },
                effects = new[]
                {
                    new CardEffect { effectType = EffectType.DrawCard, value = 2 }
                }
            };

            Cards["encourage"] = new CardData
            {
                id = "encourage",
                cardName = "독려",
                cardType = CardType.Action,
                description = "+2 액션",
                rarity = CardRarity.Basic,
                jobPools = new[] { Job.Knight },
                effects = new[]
                {
                    new CardEffect { effectType = EffectType.AddAction, value = 2 }
                }
            };

            // === 비숍 기본 카드 ===
            Cards["incinerate"] = new CardData
            {
                id = "incinerate",
                cardName = "소각",
                cardType = CardType.Action,
                description = "손패의 카드 1장 소멸",
                rarity = CardRarity.Basic,
                jobPools = new[] { Job.Bishop },
                effects = new[]
                {
                    new CardEffect
                    {
                        effectType = EffectType.DestroyCard,
                        targetType = TargetType.HandCard,
                        maxTargets = 1
                    }
                }
            };

            Cards["purify"] = new CardData
            {
                id = "purify",
                cardName = "정화",
                cardType = CardType.Action,
                description = "손패의 오염 카드 1장 소멸",
                rarity = CardRarity.Basic,
                jobPools = new[] { Job.Bishop },
                effects = new[]
                {
                    new CardEffect
                    {
                        effectType = EffectType.DestroyPollution,
                        targetType = TargetType.HandPollution,
                        maxTargets = 1
                    }
                }
            };

            // === 룩 기본 카드 ===
            Cards["stockpile"] = new CardData
            {
                id = "stockpile",
                cardName = "비축",
                cardType = CardType.Action,
                description = "+3 골드, 다음 턴 +3 골드",
                rarity = CardRarity.Basic,
                jobPools = new[] { Job.Rook },
                effects = new[]
                {
                    new CardEffect { effectType = EffectType.AddGold, value = 3 },
                    new CardEffect { effectType = EffectType.DelayedGold, value = 3, duration = 1 }
                }
            };

            // === 공용 카드 ===
            Cards["luck"] = new CardData
            {
                id = "luck",
                cardName = "행운",
                cardType = CardType.Action,
                description = "+1 카드, +2 골드",
                rarity = CardRarity.Basic,
                jobPools = new[] { Job.Pawn, Job.Knight, Job.Bishop, Job.Rook, Job.Queen },
                effects = new[]
                {
                    new CardEffect { effectType = EffectType.DrawCard, value = 1 },
                    new CardEffect { effectType = EffectType.AddGold, value = 2 }
                }
            };

            // === 오염 카드 ===
            Cards["debt"] = new CardData
            {
                id = "debt",
                cardName = "부채",
                cardType = CardType.Pollution,
                description = "손패만 차지 (플레이 불가)",
                pollutionType = PollutionType.Debt
            };

            Cards["curse"] = new CardData
            {
                id = "curse",
                cardName = "저주",
                cardType = CardType.Pollution,
                description = "턴 종료 시 -2 골드",
                pollutionType = PollutionType.Curse
            };

            Cards["disease"] = new CardData
            {
                id = "disease",
                cardName = "질병",
                cardType = CardType.Pollution,
                description = "해당 유닛 강화 불가",
                pollutionType = PollutionType.Disease
            };

            Cards["damage"] = new CardData
            {
                id = "damage",
                cardName = "파손",
                cardType = CardType.Pollution,
                description = "이번 턴 드로우 -1",
                pollutionType = PollutionType.Damage
            };

            Debug.Log($"[DataLoader] 기본 카드 {Cards.Count}종 로드됨");
        }

        /// <summary>
        /// 기본 직업 데이터
        /// </summary>
        private void LoadDefaultJobs()
        {
            Jobs = new Dictionary<Job, JobDefinition>
            {
                [Job.Pawn] = new JobDefinition
                {
                    job = Job.Pawn,
                    displayName = "폰",
                    description = "경제/생산 특화",
                    cardPoolBasic = new List<string> { "labor" },
                    cardPoolAdvanced = new List<string>(),
                    cardPoolRare = new List<string>()
                },
                [Job.Knight] = new JobDefinition
                {
                    job = Job.Knight,
                    displayName = "나이트",
                    description = "드로우/도박 특화",
                    cardPoolBasic = new List<string> { "explore", "encourage" },
                    cardPoolAdvanced = new List<string>(),
                    cardPoolRare = new List<string>()
                },
                [Job.Bishop] = new JobDefinition
                {
                    job = Job.Bishop,
                    displayName = "비숍",
                    description = "덱 관리/정화 특화",
                    cardPoolBasic = new List<string> { "incinerate", "purify" },
                    cardPoolAdvanced = new List<string>(),
                    cardPoolRare = new List<string>()
                },
                [Job.Rook] = new JobDefinition
                {
                    job = Job.Rook,
                    displayName = "룩",
                    description = "방어/안정 특화",
                    cardPoolBasic = new List<string> { "stockpile" },
                    cardPoolAdvanced = new List<string>(),
                    cardPoolRare = new List<string>()
                },
                [Job.Queen] = new JobDefinition
                {
                    job = Job.Queen,
                    displayName = "퀸",
                    description = "만능/복합",
                    cardPoolBasic = new List<string>(),
                    cardPoolAdvanced = new List<string>(),
                    cardPoolRare = new List<string>()
                }
            };

            Debug.Log($"[DataLoader] 기본 직업 {Jobs.Count}종 로드됨");
        }

        /// <summary>
        /// 기본 이벤트 데이터
        /// </summary>
        private void LoadDefaultEvents()
        {
            Events = new Dictionary<string, EventData>();
            // 이벤트는 나중에 구현
            Debug.Log("[DataLoader] 기본 이벤트 데이터 로드됨 (빈 상태)");
        }

        // =====================================================================
        // 데이터 접근 헬퍼
        // =====================================================================

        /// <summary>
        /// 카드 데이터 가져오기
        /// </summary>
        public CardData GetCard(string cardId)
        {
            if (Cards.TryGetValue(cardId, out CardData card))
            {
                return card;
            }
            Debug.LogWarning($"[DataLoader] 카드를 찾을 수 없음: {cardId}");
            return null;
        }

        /// <summary>
        /// 직업 정의 가져오기
        /// </summary>
        public JobDefinition GetJob(Job job)
        {
            if (Jobs.TryGetValue(job, out JobDefinition jobDef))
            {
                return jobDef;
            }
            Debug.LogWarning($"[DataLoader] 직업을 찾을 수 없음: {job}");
            return null;
        }

        /// <summary>
        /// 이벤트 데이터 가져오기
        /// </summary>
        public EventData GetEvent(string eventId)
        {
            if (Events.TryGetValue(eventId, out EventData evt))
            {
                return evt;
            }
            Debug.LogWarning($"[DataLoader] 이벤트를 찾을 수 없음: {eventId}");
            return null;
        }
    }
}
