// =============================================================================
// DataLoader.cs
// JSON 파일 로드 및 데이터 관리
// =============================================================================
// [Phase 2] GameConfig 로드 기능 추가
// [Phase 3] StreamingAssets 외부 로드 지원 추가
//   - 빌드 후에도 JSON 수정 가능
//   - 로드 순서: StreamingAssets -> Resources (폴백)
// [E1] ScriptableObject 우선 로드 지원 추가
//   - 로드 순서: SO -> StreamingAssets -> Resources -> 기본값
//   - 에디터에서 직접 수정 가능
// =============================================================================

using System.Collections.Generic;
using System.IO;
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
        /// 카드 데이터 (id -> CardData)
        /// </summary>
        public Dictionary<string, CardData> Cards { get; private set; }

        /// <summary>
        /// 직업 정의 (Job -> JobDefinition)
        /// </summary>
        public Dictionary<Job, JobDefinition> Jobs { get; private set; }

        /// <summary>
        /// 이벤트 데이터 (id -> EventData)
        /// </summary>
        public Dictionary<string, EventData> Events { get; private set; }

        /// <summary>
        /// 게임 설정 데이터 [Phase 2 추가]
        /// </summary>
        public GameConfigData Config { get; private set; }

        /// <summary>
        /// 재화 등급 데이터 [Phase 2 추가]
        /// </summary>
        public TreasureGradesData TreasureGrades { get; private set; }

        /// <summary>
        /// 직업 데이터 (확장) [Phase 2 추가]
        /// </summary>
        public JobsData JobsExtended { get; private set; }

        // =====================================================================
        // [E1] ScriptableObject 참조
        // =====================================================================

        /// <summary>
        /// GameConfig SO [E1 추가]
        /// Inspector에서 할당하거나 Resources에서 자동 로드
        /// </summary>
        [Header("[E1] ScriptableObject 설정")]
        [Tooltip("GameConfigSO 에셋 (비어있으면 Resources에서 자동 로드)")]
        [SerializeField] private GameConfigSO _gameConfigSO;

        /// <summary>
        /// TreasureGrades SO [E1 추가]
        /// Inspector에서 할당하거나 Resources에서 자동 로드
        /// </summary>
        [Tooltip("TreasureGradesSO 에셋 (비어있으면 Resources에서 자동 로드)")]
        [SerializeField] private TreasureGradesSO _treasureGradesSO;

        #region [E2] Card/Job Database SO 참조

        /// <summary>
        /// CardDatabase SO [E2 추가]
        /// Inspector에서 할당하거나 Resources에서 자동 로드
        /// </summary>
        [Tooltip("CardDatabaseSO 에셋 (비어있으면 Resources에서 자동 로드)")]
        [SerializeField] private CardDatabaseSO _cardDatabaseSO;

        /// <summary>
        /// JobDatabase SO [E2 추가]
        /// Inspector에서 할당하거나 Resources에서 자동 로드
        /// </summary>
        [Tooltip("JobDatabaseSO 에셋 (비어있으면 Resources에서 자동 로드)")]
        [SerializeField] private JobDatabaseSO _jobDatabaseSO;

        /// <summary>
        /// CardDatabaseSO 접근자 [E2 추가]
        /// </summary>
        public CardDatabaseSO CardDatabaseSO => _cardDatabaseSO;

        /// <summary>
        /// JobDatabaseSO 접근자 [E2 추가]
        /// </summary>
        public JobDatabaseSO JobDatabaseSO => _jobDatabaseSO;

        #endregion

        #region [E3] Event Database SO 참조

        /// <summary>
        /// EventDatabase SO [E3 추가]
        /// Inspector에서 할당하거나 Resources에서 자동 로드
        /// </summary>
        [Tooltip("EventDatabaseSO 에셋 (비어있으면 Resources에서 자동 로드)")]
        [SerializeField] private EventDatabaseSO _eventDatabaseSO;

        /// <summary>
        /// EventDatabaseSO 접근자 [E3 추가]
        /// </summary>
        public EventDatabaseSO EventDatabaseSO => _eventDatabaseSO;

        #endregion

        /// <summary>
        /// GameConfigSO 접근자 [E1 추가]
        /// </summary>
        public GameConfigSO GameConfigSO => _gameConfigSO;

        /// <summary>
        /// TreasureGradesSO 접근자 [E1 추가]
        /// </summary>
        public TreasureGradesSO TreasureGradesSO => _treasureGradesSO;



        // =====================================================================
        // [Phase 3] StreamingAssets 로드 헬퍼
        // =====================================================================

        /// <summary>
        /// StreamingAssets 경로 반환
        /// </summary>
        private string GetStreamingAssetsPath(string relativePath)
        {
            return Path.Combine(Application.streamingAssetsPath, "Data", relativePath);
        }

        /// <summary>
        /// StreamingAssets에서 JSON 로드 시도
        /// </summary>
        private string TryLoadFromStreamingAssets(string relativePath)
        {
            string fullPath = GetStreamingAssetsPath(relativePath);

            if (File.Exists(fullPath))
            {
                try
                {
                    string json = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                    Debug.Log($"[DataLoader] StreamingAssets에서 로드: {relativePath}");
                    return json;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[DataLoader] StreamingAssets 읽기 실패 ({relativePath}): {e.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// StreamingAssets 또는 Resources에서 JSON 로드
        /// </summary>
        private string LoadJsonWithFallback(string dataName)
        {
            // 1. StreamingAssets 시도
            string json = TryLoadFromStreamingAssets($"{dataName}.json");
            if (!string.IsNullOrEmpty(json))
            {
                return json;
            }

            // 2. Resources 폴백
            TextAsset resourceFile = Resources.Load<TextAsset>($"Data/{dataName}");
            if (resourceFile != null)
            {
                Debug.Log($"[DataLoader] Resources에서 로드: {dataName}");
                return resourceFile.text;
            }

            Debug.LogWarning($"[DataLoader] {dataName}.json을 찾을 수 없습니다.");
            return null;
        }

        /// <summary>
        /// [Phase 3] 외부 데이터 경로 존재 여부 확인
        /// </summary>
        public bool HasExternalData()
        {
            string dataPath = Path.Combine(Application.streamingAssetsPath, "Data");
            return Directory.Exists(dataPath);
        }

        /// <summary>
        /// [Phase 3] 외부 데이터 경로 반환
        /// </summary>
        public string GetExternalDataPath()
        {
            return Path.Combine(Application.streamingAssetsPath, "Data");
        }


        // =====================================================================
        // 데이터 로드
        // =====================================================================

        /// <summary>
        /// 모든 데이터 로드
        /// </summary>
        private void LoadAllData()
        {
            LoadGameConfig();  // [Phase 2] GameConfig 먼저 로드
            LoadTreasureGrades();  // [Phase 2] TreasureGrades 로드
            LoadCards();
            LoadJobs();
            LoadEvents();

            Debug.Log($"[DataLoader] 로드 완료 - 카드: {Cards.Count}종, 직업: {Jobs.Count}종, 이벤트: {Events.Count}종");
        }

        // =====================================================================
        // [Phase 2] GameConfig 로드
        // =====================================================================

        /// <summary>
        /// GameConfig 로드 [E1 수정: SO 우선]
        /// 로드 순서: SO -> StreamingAssets JSON -> Resources JSON -> 기본값
        /// </summary>
        private void LoadGameConfig()
        {
            // [E1] 1. SO 우선 로드 시도
            if (_gameConfigSO == null)
            {
                _gameConfigSO = Resources.Load<GameConfigSO>("Data/GameConfigSO");
            }

            if (_gameConfigSO != null)
            {
                // SO에서 GameConfigData 변환
                Config = _gameConfigSO.ToGameConfigData();
                GameConfig.InitializeFromSO(_gameConfigSO);
                Debug.Log($"[DataLoader] GameConfig SO에서 로드 완료 (v{Config.version})");
                return;
            }

            // 2. JSON 폴백 (기존 로직)
            string jsonText = LoadJsonWithFallback("GameConfig");
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogWarning("[DataLoader] GameConfig.json 파일을 찾을 수 없습니다. 기본값을 사용합니다.");
                LoadDefaultGameConfig();
                return;
            }

            try
            {
                Config = JsonUtility.FromJson<GameConfigData>(jsonText);
                Debug.Log($"[DataLoader] GameConfig JSON에서 로드 완료 (v{Config.version})");

                // GameConfig 정적 클래스 초기화
                GameConfig.Initialize(Config);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataLoader] GameConfig.json 파싱 실패: {e.Message}");
                LoadDefaultGameConfig();
            }
        }

        /// <summary>
        /// 기본 GameConfig (JSON 없을 때 폴백)
        /// </summary>
        private void LoadDefaultGameConfig()
        {
            Config = new GameConfigData
            {
                version = "1.0.0-default",
                game = new GameSettings
                {
                    maxTurns = 60,
                    startingGold = 10,
                    handSize = 5,
                    maxHandSize = 10,
                    startingActions = 1,
                    maxHouses = 12
                },
                validation = new ValidationSettings
                {
                    turns = new[] { 10, 20, 30, 40, 50 },
                    goldRequired = new[] { 40, 100, 200, 350, 500 }
                },
                raid = new RaidSettings
                {
                    turns = new[] { 8, 16, 24, 32, 40, 48, 56 },
                    enemyPowerBase = 10,
                    enemyPowerPerTurn = 3,
                    finalBattleTurn = 60,
                    finalBattleRequiredPower = 500
                },
                loyalty = new LoyaltySettings
                {
                    childDefault = 50,
                    adultDefault = 100,
                    deficitPenalty = -20,
                    surplusBonus = 10
                },
                breeding = new BreedingSettings
                {
                    pregnancyDuration = 3,
                    fertilityChance = new[] { 20, 40, 60, 80, 100 }
                },
                promotion = new PromotionSettings
                {
                    costs = new[] { 10, 25, 50 },
                    maxLevel = 3
                },
                land = new LandSettings
                {
                    developCosts = new[] { 15, 30, 60 }
                },
                eventConfig = new EventSettings
                {
                    triggerChance = 80,
                    positiveRatio = 50,
                    negativeRatio = 20,
                    choiceRatio = 30
                },
                promotionJobChance = new PromotionJobChance
                {
                    Knight = 30,
                    Bishop = 30,
                    Rook = 30,
                    Queen = 10
                },
                cardRarityChance = new CardRarityChance
                {
                    Basic = 60,
                    Advanced = 35,
                    Rare = 5
                },
                combat = new CombatSettings
                {
                    jobBasePower = new JobBasePower
                    {
                        Pawn = 10,
                        Knight = 30,
                        Bishop = 15,
                        Rook = 25,
                        Queen = 35
                    },
                    promotionBonusPerLevel = 10,
                    oldAgeMultiplier = 0.75f
                },
                growth = new GrowthSettings
                {
                    childDuration = 3,
                    youngDuration = 10,
                    middleDuration = 10,
                    oldBaseDuration = 5,
                    middleBreedableRemainingTurns = 5,
                    oldAgeDeathChance = new OldAgeDeathChance
                    {
                        turn6 = 20,
                        turn7 = 40,
                        turn8 = 60,
                        turn9 = 80,
                        turn10Plus = 100
                    }
                }
            };

            Debug.Log("[DataLoader] 기본 GameConfig 로드됨");
            GameConfig.Initialize(Config);
        }

        // =====================================================================
        // [Phase 2] TreasureGrades 로드
        // =====================================================================

        /// <summary>
        /// TreasureGrades 로드 [E1 수정: SO 우선]
        /// 로드 순서: SO -> StreamingAssets JSON -> Resources JSON -> 기본값
        /// </summary>
        private void LoadTreasureGrades()
        {
            // [E1] 1. SO 우선 로드 시도
            if (_treasureGradesSO == null)
            {
                _treasureGradesSO = Resources.Load<TreasureGradesSO>("Data/TreasureGradesSO");
            }

            if (_treasureGradesSO != null)
            {
                // SO에서 TreasureGradesData 변환
                TreasureGrades = _treasureGradesSO.ToTreasureGradesData();
                TreasureGradeUtil.InitializeFromSO(_treasureGradesSO);
                Debug.Log($"[DataLoader] TreasureGrades SO에서 로드 완료 (v{TreasureGrades.version}, {TreasureGrades.grades?.Length ?? 0}개 등급)");
                return;
            }

            // 2. JSON 폴백 (기존 로직)
            string jsonText = LoadJsonWithFallback("TreasureGrades");
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogWarning("[DataLoader] TreasureGrades.json 파일을 찾을 수 없습니다. 기본값을 사용합니다.");
                LoadDefaultTreasureGrades();
                return;
            }

            try
            {
                TreasureGrades = JsonUtility.FromJson<TreasureGradesData>(jsonText);
                Debug.Log($"[DataLoader] TreasureGrades JSON에서 로드 완료 (v{TreasureGrades.version}, {TreasureGrades.grades?.Length ?? 0}개 등급)");

                // TreasureGradeUtil 초기화
                TreasureGradeUtil.Initialize(TreasureGrades);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataLoader] TreasureGrades.json 파싱 실패: {e.Message}");
                LoadDefaultTreasureGrades();
            }
        }


        /// <summary>
        /// 기본 TreasureGrades (JSON 없을 때 폴백)
        /// </summary>
        private void LoadDefaultTreasureGrades()
        {
            TreasureGrades = new TreasureGradesData
            {
                version = "1.0.0-default",
                grades = new[]
                {
                    new TreasureGradeInfo { level = 1, id = "copper", enumValue = 0, name = "동화", goldValue = 1, color = "#CD7F32" },
                    new TreasureGradeInfo { level = 2, id = "silver", enumValue = 1, name = "은화", goldValue = 2, color = "#C0C0C0" },
                    new TreasureGradeInfo { level = 3, id = "gold_coin", enumValue = 2, name = "금화", goldValue = 4, color = "#FFD700" },
                    new TreasureGradeInfo { level = 4, id = "emerald", enumValue = 3, name = "에메랄드", goldValue = 7, color = "#50C878" },
                    new TreasureGradeInfo { level = 5, id = "sapphire", enumValue = 4, name = "사파이어", goldValue = 12, color = "#0F52BA" },
                    new TreasureGradeInfo { level = 6, id = "ruby", enumValue = 5, name = "루비", goldValue = 20, color = "#E0115F" },
                    new TreasureGradeInfo { level = 7, id = "diamond", enumValue = 6, name = "다이아몬드", goldValue = 35, color = "#B9F2FF" }
                },
                upgradeChain = new[] { "copper", "silver", "gold_coin", "emerald", "sapphire", "ruby", "diamond" }
            };

            Debug.Log("[DataLoader] 기본 TreasureGrades 로드됨");
            TreasureGradeUtil.Initialize(TreasureGrades);
        }

        /// <summary>
        /// 카드 데이터 로드 [E2 수정: SO 우선 로드]
        /// 로드 순서: SO -> StreamingAssets JSON -> Resources JSON -> 기본값
        /// </summary>
        private void LoadCards()
        {
            Cards = new Dictionary<string, CardData>();

            // [E2] 1. SO 우선 로드 시도
            if (_cardDatabaseSO == null)
            {
                _cardDatabaseSO = Resources.Load<CardDatabaseSO>("Data/CardDatabaseSO");
            }

            if (_cardDatabaseSO != null && _cardDatabaseSO.cards != null && _cardDatabaseSO.cards.Length > 0)
            {
                // SO에서 CardData Dictionary 변환
                Cards = _cardDatabaseSO.ToCardDataDictionary();
                Debug.Log($"[DataLoader] Cards SO에서 로드 완료 ({Cards.Count}개 카드)");
                return;
            }

            // 2. JSON 폴백 (기존 로직)
            string jsonText = LoadJsonWithFallback("Cards");
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogWarning("[DataLoader] Cards.json 파일을 찾을 수 없습니다. 기본 데이터를 사용합니다.");
                LoadDefaultCards();
                return;
            }

            try
            {
                CardDataList dataList = JsonUtility.FromJson<CardDataList>(jsonText);
                foreach (var card in dataList.cards)
                {
                    Cards[card.id] = card;
                }
                Debug.Log($"[DataLoader] Cards JSON에서 로드 완료 ({Cards.Count}개 카드)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataLoader] Cards.json 파싱 실패: {e.Message}");
                LoadDefaultCards();
            }
        }


        /// <summary>
        /// 직업 데이터 로드 [E2 수정: SO 우선 로드]
        /// 로드 순서: SO -> StreamingAssets JSON -> Resources JSON -> 기본값
        /// </summary>
        private void LoadJobs()
        {
            Jobs = new Dictionary<Job, JobDefinition>();

            // [E2] 1. SO 우선 로드 시도
            if (_jobDatabaseSO == null)
            {
                _jobDatabaseSO = Resources.Load<JobDatabaseSO>("Data/JobDatabaseSO");
            }

            if (_jobDatabaseSO != null && _jobDatabaseSO.jobs != null && _jobDatabaseSO.jobs.Length > 0)
            {
                // SO에서 JobDefinition Dictionary 변환
                Jobs = _jobDatabaseSO.ToJobDefinitionDictionary();

                // JobsExtended도 설정 (다른 시스템에서 참조할 수 있음)
                JobsExtended = _jobDatabaseSO.ToJobsData();

                Debug.Log($"[DataLoader] Jobs SO에서 로드 완료 ({Jobs.Count}개 직업)");
                return;
            }

            // 2. JSON 폴백 (기존 로직)
            string jsonText = LoadJsonWithFallback("Jobs");
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogWarning("[DataLoader] Jobs.json 파일을 찾을 수 없습니다. 기본 데이터를 사용합니다.");
                LoadDefaultJobs();
                return;
            }

            try
            {
                // 새로운 JobsData 구조로 로드 시도
                JobsExtended = JsonUtility.FromJson<JobsData>(jsonText);

                if (JobsExtended != null && JobsExtended.jobs != null && JobsExtended.jobs.Length > 0)
                {
                    // JobsData에서 JobDefinition으로 변환
                    foreach (var jobInfo in JobsExtended.jobs)
                    {
                        var jobDef = jobInfo.ToJobDefinition();
                        Jobs[jobDef.job] = jobDef;
                    }
                    Debug.Log($"[DataLoader] Jobs JSON에서 로드 완료 (v{JobsExtended.version}, {Jobs.Count}개 직업)");
                }
                else
                {
                    // 기존 JobDefinitionList 구조로 폴백 시도
                    JobDefinitionList dataList = JsonUtility.FromJson<JobDefinitionList>(jsonText);
                    if (dataList != null && dataList.jobs != null)
                    {
                        foreach (var job in dataList.jobs)
                        {
                            Jobs[job.job] = job;
                        }
                        Debug.Log($"[DataLoader] Jobs JSON 레거시 형식에서 로드 완료 ({Jobs.Count}개 직업)");
                    }
                    else
                    {
                        LoadDefaultJobs();
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataLoader] Jobs.json 파싱 실패: {e.Message}");
                LoadDefaultJobs();
            }
        }



        /// <summary>
        /// 이벤트 데이터 로드 [E3 수정: SO 우선 로드]
        /// 로드 순서: SO -> StreamingAssets JSON -> Resources JSON -> 기본값
        /// </summary>
        private void LoadEvents()
        {
            Events = new Dictionary<string, EventData>();

            // [E3] 1. SO 우선 로드 시도
            if (_eventDatabaseSO == null)
            {
                _eventDatabaseSO = Resources.Load<EventDatabaseSO>("Data/EventDatabaseSO");
            }

            if (_eventDatabaseSO != null && _eventDatabaseSO.events != null && _eventDatabaseSO.events.Length > 0)
            {
                // SO에서 EventData Dictionary 변환
                Events = _eventDatabaseSO.ToEventDataDictionary();
                Debug.Log($"[DataLoader] Events SO에서 로드 완료 ({Events.Count}개 이벤트)");
                return;
            }

            // 2. JSON 폴백 (기존 로직)
            string jsonText = LoadJsonWithFallback("Events");
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogWarning("[DataLoader] Events.json 파일을 찾을 수 없습니다. 기본 데이터를 사용합니다.");
                LoadDefaultEvents();
                return;
            }

            try
            {
                EventDataList dataList = JsonUtility.FromJson<EventDataList>(jsonText);
                foreach (var evt in dataList.events)
                {
                    Events[evt.eventId] = evt;
                }
                Debug.Log($"[DataLoader] Events JSON에서 로드 완료 ({Events.Count}개 이벤트)");
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
        /// CardEffect를 ConditionalEffect로 래핑하는 헬퍼
        /// [리팩토링] 하드코딩 폴백 데이터용
        /// </summary>
        private static ConditionalEffect[] WrapEffects(params CardEffect[] legacyEffects)
        {
            if (legacyEffects == null || legacyEffects.Length == 0)
                return new ConditionalEffect[0];

            var effects = new Effect[legacyEffects.Length];
            for (int i = 0; i < legacyEffects.Length; i++)
            {
                var le = legacyEffects[i];
                effects[i] = new Effect
                {
                    type = le.effectType,
                    value = le.value,
                    target = le.targetType,
                    maxTargets = le.maxTargets,
                    createGrade = le.createGrade,
                    duration = le.duration,
                    successChance = le.successChance,
                    successValueInt = le.successValue,
                    failValueInt = le.failValue
                };

                if (le.floatValue != 0)
                {
                    effects[i].dynamicValue = new ValueSource
                    {
                        source = ValueSourceType.Fixed,
                        multiplier = le.floatValue
                    };
                }
            }

            return new[] { new ConditionalEffect { conditions = new EffectCondition[0], effects = effects } };
        }

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

            // === 기본 액션 카드 ===
            Cards["labor"] = new CardData
            {
                id = "labor",
                cardName = "노동",
                cardType = CardType.Action,
                description = "+3 골드",
                rarity = CardRarity.Basic,
                jobPools = new[] { Job.Pawn },
                effects = WrapEffects(
                    new CardEffect { effectType = EffectType.AddGold, value = 3 }
                )
            };

            Cards["explore"] = new CardData
            {
                id = "explore",
                cardName = "탐색",
                cardType = CardType.Action,
                description = "+2 카드",
                rarity = CardRarity.Basic,
                jobPools = new[] { Job.Knight },
                effects = WrapEffects(
                    new CardEffect { effectType = EffectType.DrawCard, value = 2 }
                )
            };

            Cards["encourage"] = new CardData
            {
                id = "encourage",
                cardName = "독려",
                cardType = CardType.Action,
                description = "+2 액션",
                rarity = CardRarity.Basic,
                jobPools = new[] { Job.Knight },
                effects = WrapEffects(
                    new CardEffect { effectType = EffectType.AddAction, value = 2 }
                )
            };

            Cards["settle"] = new CardData
            {
                id = "settle",
                cardName = "결산",
                cardType = CardType.Action,
                description = "손패의 재화 1장을 정산 (골드로 변환)",
                rarity = CardRarity.Basic,
                jobPools = new[] { Job.Pawn },
                effects = WrapEffects(
                    new CardEffect
                    {
                        effectType = EffectType.SettleCard,
                        value = 1,
                        targetType = TargetType.HandTreasure,
                        maxTargets = 1
                    }
                )
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
                    cardPoolBasic = new List<string> { "labor", "settle" },
                    cardPoolAdvanced = new List<string> { "ledger" },
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
                    cardPoolAdvanced = new List<string> { "liquidate" },
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

        // =====================================================================
        // 이벤트 카테고리별 조회 (E8 추가)
        // =====================================================================

        /// <summary>
        /// 카테고리별 이벤트 목록 반환
        /// </summary>
        public List<EventData> GetEventsByCategory(int category)
        {
            var result = new List<EventData>();
            foreach (var evt in Events.Values)
            {
                if (evt.category == category)
                {
                    result.Add(evt);
                }
            }
            return result;
        }

        /// <summary>
        /// 긍정 이벤트 목록
        /// </summary>
        public List<EventData> GetPositiveEvents() => GetEventsByCategory(1);

        /// <summary>
        /// 부정 이벤트 목록
        /// </summary>
        public List<EventData> GetNegativeEvents() => GetEventsByCategory(2);

        /// <summary>
        /// 선택 이벤트 목록
        /// </summary>
        public List<EventData> GetChoiceEvents() => GetEventsByCategory(3);
    }
}