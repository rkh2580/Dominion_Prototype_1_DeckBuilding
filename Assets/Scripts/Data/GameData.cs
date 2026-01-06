// =============================================================================
// GameData.cs
// 유닛, 이벤트, 게임 상태 등 데이터 클래스 정의
// =============================================================================
// [R1~R5 수정] UnitInstance 필드 추가, HouseInstance, LandInstance 추가
// [Phase 2] GameConfig JSON 기반으로 리팩토링
// =============================================================================

using DeckBuildingEconomy.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBuildingEconomy.Data
{
    // =========================================================================
    // 유닛 관련
    // =========================================================================

    /// <summary>
    /// 유닛 인스턴스
    /// 게임 중 존재하는 유닛 한 명
    /// </summary>
    [Serializable]
    public class UnitInstance
    {
        public string unitId;
        public string unitName;
        public Job job;
        public GrowthStage stage;
        public int stageRemainingTurns;

        public int promotionLevel;
        public bool promotedThisTurn;

        public int loyalty;
        public string houseId;

        public int combatPower;

        public List<string> ownedCardIds;

        public bool hasDisease;

        public int oldAgeTurns;

        // =====================================================================
        // 하위 호환
        // =====================================================================

        [Obsolete("Use promotionLevel instead")]
        public int upgradeLevel
        {
            get => promotionLevel;
            set => promotionLevel = value;
        }

        [Obsolete("Use promotedThisTurn instead")]
        public bool upgradedThisTurn
        {
            get => promotedThisTurn;
            set => promotedThisTurn = value;
        }

        /// <summary>
        /// 유닛 생성 팩토리 메서드
        /// </summary>
        public static UnitInstance Create(string name, Job job, GrowthStage stage)
        {
            int remainingTurns = GameConfig.GetStageDuration(stage);
            int loyalty = (stage == GrowthStage.Child) ? GameConfig.ChildLoyalty : GameConfig.AdultLoyalty;

            var unit = new UnitInstance
            {
                unitId = Guid.NewGuid().ToString(),
                unitName = name,
                job = job,
                stage = stage,
                stageRemainingTurns = remainingTurns,
                promotionLevel = 0,
                loyalty = loyalty,
                houseId = null,
                combatPower = 0,
                ownedCardIds = new List<string>(),
                hasDisease = false,
                promotedThisTurn = false,
                oldAgeTurns = 0
            };

            unit.RecalculateCombatPower();
            return unit;
        }

        /// <summary>
        /// 성장 단계별 기간 조회
        /// [Phase 2] GameConfig 참조로 변경
        /// </summary>
        public static int GetStageDuration(GrowthStage stage)
        {
            return GameConfig.GetStageDuration(stage);
        }

        /// <summary>
        /// 전직 가능 여부 확인
        /// </summary>
        public bool CanPromote()
        {
            if (stage != GrowthStage.Young && stage != GrowthStage.Middle)
                return false;

            if (promotedThisTurn)
                return false;

            if (hasDisease)
                return false;

            if (promotionLevel >= GameConfig.MaxPromotionLevel)
                return false;

            return true;
        }

        [Obsolete("Use CanPromote instead")]
        public bool CanUpgrade() => CanPromote();

        /// <summary>
        /// 다음 전직 비용 조회
        /// </summary>
        public int GetNextPromotionCost()
        {
            if (promotionLevel >= GameConfig.PromotionCosts.Length)
                return int.MaxValue;
            return GameConfig.PromotionCosts[promotionLevel];
        }

        [Obsolete("Use GetNextPromotionCost instead")]
        public int GetNextUpgradeCost() => GetNextPromotionCost();

        /// <summary>
        /// 사망 확률 조회
        /// [Phase 2] GameConfig 참조로 변경
        /// </summary>
        public int GetDeathChance()
        {
            if (stage != GrowthStage.Old) return 0;
            return GameConfig.GetOldAgeDeathChance(oldAgeTurns);
        }

        /// <summary>
        /// 교배 가능 여부 확인
        /// [D2] 잔여 턴 조건 삭제 - 청년/중년이면 언제든 교배 가능
        /// </summary>
        public bool CanBreed()
        {
            return stage == GrowthStage.Young || stage == GrowthStage.Middle;
        }

        /// <summary>
        /// 전투력 재계산
        /// [Phase 2] GameConfig 참조로 변경
        /// </summary>
        public void RecalculateCombatPower()
        {
            if (stage == GrowthStage.Child || job == Job.None)
            {
                combatPower = 0;
                return;
            }

            int basePower = GameConfig.GetJobBaseCombatPower(job);
            int promotionBonus = promotionLevel * GameConfig.PromotionBonusPerLevel;
            float ageFactor = (stage == GrowthStage.Old) ? GameConfig.OldAgeMultiplier : 1.0f;

            combatPower = Mathf.RoundToInt((basePower + promotionBonus) * ageFactor);
        }
    }

    /// <summary>
    /// 직업 정의
    /// </summary>
    [Serializable]
    public class JobDefinition
    {
        public Job job;
        public string displayName;
        public string description;
        public List<string> cardPoolBasic;
        public List<string> cardPoolAdvanced;
        public List<string> cardPoolRare;
    }

    /// <summary>
    /// 직업 정의 목록 (JSON 루트)
    /// </summary>
    [Serializable]
    public class JobDefinitionList
    {
        public List<JobDefinition> jobs;
    }

    // =========================================================================
    // 시작 설정 관련 (E4 추가)
    // =========================================================================

    /// <summary>
    /// 예정 이벤트 항목
    /// [E4] Inspector에서 특정 턴에 발생할 이벤트 지정
    /// </summary>
    [Serializable]
    public class ScheduledEventEntry
    {
        [Tooltip("발생 턴")]
        public int turn;

        [Tooltip("발생할 이벤트")]
        public EventSO eventSO;
    }

    /// <summary>
    /// 시작 유닛 정의
    /// [E4] Inspector에서 커스텀 시작 유닛 지정
    /// </summary>
    [Serializable]
    public class StartingUnitEntry
    {
        [Tooltip("유닛 이름")]
        public string unitName = "주민";

        [Tooltip("직업")]
        public Job job = Job.Pawn;

        [Tooltip("성장 단계")]
        public GrowthStage stage = GrowthStage.Young;

        [Tooltip("배치할 집 인덱스 (0부터 시작)")]
        public int houseIndex = 0;

        [Tooltip("집 내 슬롯 (adultA/adultB)")]
        public string slot = "adultA";
    }

    // =========================================================================
    // 집(House) 관련
    // =========================================================================

    /// <summary>
    /// 집 인스턴스
    /// </summary>
    [Serializable]
    public class HouseInstance
    {
        public string houseId;
        public string houseName;

        public string adultSlotA;
        public string adultSlotB;
        public string childSlot;

        public bool isPregnant;
        public int pregnancyTurns;
        public int fertilityCounter;

        /// <summary>
        /// 집 생성 팩토리 메서드
        /// </summary>
        public static HouseInstance Create(string name = null)
        {
            return new HouseInstance
            {
                houseId = Guid.NewGuid().ToString(),
                houseName = name ?? "집",
                adultSlotA = null,
                adultSlotB = null,
                childSlot = null,
                isPregnant = false,
                pregnancyTurns = 0,
                fertilityCounter = 0
            };
        }

        /// <summary>
        /// 빈 성인 슬롯이 있는지 확인
        /// </summary>
        public bool HasEmptyAdultSlot()
        {
            return string.IsNullOrEmpty(adultSlotA) || string.IsNullOrEmpty(adultSlotB);
        }

        /// <summary>
        /// 빈 자녀 슬롯이 있는지 확인
        /// </summary>
        public bool HasEmptyChildSlot()
        {
            return string.IsNullOrEmpty(childSlot);
        }

        /// <summary>
        /// 두 성인이 있는지 확인
        /// </summary>
        public bool HasTwoAdults()
        {
            return !string.IsNullOrEmpty(adultSlotA) && !string.IsNullOrEmpty(adultSlotB);
        }

        /// <summary>
        /// 두 성인이 있는지 확인 (별칭)
        /// </summary>
        public bool HasBothAdults()
        {
            return HasTwoAdults();
        }

        /// <summary>
        /// 거주자 수 반환
        /// </summary>
        public int GetResidentCount()
        {
            int count = 0;
            if (!string.IsNullOrEmpty(adultSlotA)) count++;
            if (!string.IsNullOrEmpty(adultSlotB)) count++;
            if (!string.IsNullOrEmpty(childSlot)) count++;
            return count;
        }
    }

    // =========================================================================
    // 사유지(Land) 관련
    // =========================================================================

    /// <summary>
    /// 사유지 인스턴스
    /// </summary>
    [Serializable]
    public class LandInstance
    {
        public string landId;
        public string landName;
        public LandType landType;
        public int level;

        /// <summary>
        /// 빈 땅 생성 팩토리 메서드
        /// </summary>
        public static LandInstance CreateEmpty(string name = null)
        {
            return new LandInstance
            {
                landId = Guid.NewGuid().ToString(),
                landName = name ?? "빈 땅",
                landType = LandType.Empty,
                level = 0
            };
        }
    }

    // =========================================================================
    // 이벤트 관련 (E8 재설계 - ConditionalEffect 재사용)
    // =========================================================================

    /// <summary>
    /// 이벤트 정의 (JSON에서 로드)
    /// 카드 효과 시스템의 ConditionalEffect를 재사용하여 통일된 구조
    /// </summary>
    [Serializable]
    public class EventData
    {
        /// <summary>이벤트 고유 ID</summary>
        public string eventId;

        /// <summary>이벤트 이름 (UI 표시)</summary>
        public string eventName;

        /// <summary>이벤트 카테고리 (1:긍정, 2:부정, 3:선택)</summary>
        public int category;

        /// <summary>이벤트 설명 (UI 표시)</summary>
        public string description;

        /// <summary>
        /// 발생 조건 - 조건을 만족해야 이 이벤트가 후보에 포함됨
        /// 조건 없으면 null 또는 빈 배열 (항상 후보)
        /// EffectCondition 재사용
        /// </summary>
        public EffectCondition[] triggerConditions;

        /// <summary>
        /// 효과 목록 - 카드와 동일한 ConditionalEffect 재사용
        /// 긍정/부정 이벤트에서 즉시 실행
        /// </summary>
        public ConditionalEffect[] effects;

        /// <summary>
        /// 선택지 목록 - 선택 이벤트에서 사용
        /// null이거나 비어있으면 즉시 효과 이벤트
        /// </summary>
        public EventChoice[] choices;

        // =====================================================================
        // 헬퍼 메서드
        // =====================================================================

        /// <summary>
        /// 선택 이벤트인지 확인
        /// </summary>
        public bool IsChoiceEvent => choices != null && choices.Length > 0;

        /// <summary>
        /// 카테고리를 RandomEventCategory로 반환
        /// </summary>
        public RandomEventCategory GetCategory()
        {
            return (RandomEventCategory)category;
        }
    }

    /// <summary>
    /// 선택 이벤트의 선택지
    /// </summary>
    [Serializable]
    public class EventChoice
    {
        /// <summary>선택지 ID</summary>
        public string choiceId;

        /// <summary>선택지 텍스트 (UI 버튼에 표시)</summary>
        public string choiceText;

        /// <summary>
        /// 선택 가능 조건 - 조건 불만족 시 버튼 비활성화
        /// 예: 골드 30 이상 필요
        /// EffectCondition 재사용
        /// </summary>
        public EffectCondition[] requirements;

        /// <summary>
        /// 선택 시 발동하는 효과
        /// ConditionalEffect 재사용
        /// </summary>
        public ConditionalEffect[] effects;

        // =====================================================================
        // 헬퍼 메서드
        // =====================================================================

        /// <summary>
        /// 조건 없이 항상 선택 가능한지
        /// </summary>
        public bool HasNoRequirements => requirements == null || requirements.Length == 0;
    }

    /// <summary>
    /// 이벤트 데이터 목록 (JSON 루트)
    /// </summary>
    [Serializable]
    public class EventDataList
    {
        public List<EventData> events;
    }

    // =========================================================================
    // 게임 상태
    // =========================================================================

    /// <summary>
    /// 게임 상태
    /// </summary>
    [Serializable]
    public class GameState
    {
        public int currentTurn;
        public GamePhase currentPhase;
        public int remainingActions;

        public int gold;
        public int maintenanceCost;

        public List<CardInstance> deck;
        public List<CardInstance> hand;
        public List<CardInstance> discardPile;
        public List<CardInstance> playArea;

        public List<UnitInstance> units;

        public List<HouseInstance> houses;
        public List<LandInstance> lands;

        public int totalCombatPower;

        public List<PersistentEffect> activeEffects;

        public float goldMultiplier;
        public int goldBonus;

        public bool pollutionIgnored;
        public int upgradeDiscount;

        public GameEndState endState;

        /// <summary>
        /// 새 게임 상태 생성
        /// </summary>
        public static GameState CreateNew()
        {
            return new GameState
            {
                currentTurn = 0,
                currentPhase = GamePhase.TurnStart,
                remainingActions = 0,
                gold = GameConfig.StartingGold,
                maintenanceCost = 0,
                deck = new List<CardInstance>(),
                hand = new List<CardInstance>(),
                discardPile = new List<CardInstance>(),
                playArea = new List<CardInstance>(),
                units = new List<UnitInstance>(),
                houses = new List<HouseInstance>(),
                lands = new List<LandInstance>(),
                totalCombatPower = 0,
                activeEffects = new List<PersistentEffect>(),
                goldMultiplier = 1f,
                goldBonus = 0,
                pollutionIgnored = false,
                upgradeDiscount = 0,
                endState = GameEndState.None
            };
        }

        /// <summary>
        /// 턴 시작 시 상태 초기화
        /// </summary>
        public void ResetTurnState()
        {
            goldMultiplier = 1f;
            goldBonus = 0;
            pollutionIgnored = false;
            upgradeDiscount = 0;

            foreach (var unit in units)
            {
                unit.promotedThisTurn = false;
            }

            foreach (var card in deck)
            {
                card.isBoostedThisTurn = false;
                card.boostedGrade = null;
            }
            foreach (var card in hand)
            {
                card.isBoostedThisTurn = false;
                card.boostedGrade = null;
            }
            foreach (var card in discardPile)
            {
                card.isBoostedThisTurn = false;
                card.boostedGrade = null;
            }
        }

        /// <summary>
        /// 전체 전투력 재계산
        /// </summary>
        public void RecalculateCombatPower()
        {
            totalCombatPower = 0;
            foreach (var unit in units)
            {
                unit.RecalculateCombatPower();
                totalCombatPower += unit.combatPower;
            }
        }
    }

    // =========================================================================
    // 지속 효과
    // =========================================================================

    /// <summary>
    /// 지속 효과
    /// </summary>
    [Serializable]
    public class PersistentEffect
    {
        public string effectId;
        public PersistentEffectType type;
        public int remainingTurns;
        public int value;
    }

    /// <summary>
    /// 지속 효과 타입
    /// </summary>
    public enum PersistentEffectType
    {
        DelayedGold,
        GoldPerTurn,
        MaintenanceIncrease
    }

    // =========================================================================
    // 효과 조건 시스템 클래스 (E1 추가)
    // =========================================================================

    /// <summary>
    /// 효과 발동 조건
    /// 조건 타입과 비교값을 조합하여 조건 평가
    /// </summary>
    [Serializable]
    public class EffectCondition
    {
        /// <summary>조건 타입</summary>
        public ConditionType type;

        /// <summary>비교 연산자 (필요한 경우)</summary>
        public ComparisonType comparison;

        /// <summary>비교값</summary>
        public int value;

        /// <summary>
        /// 조건 없음 (항상 참) 생성
        /// </summary>
        public static EffectCondition None => new EffectCondition
        {
            type = ConditionType.None
        };
    }

    /// <summary>
    /// 동적 값 소스
    /// 고정값 또는 게임 상태에서 계산되는 값을 정의
    /// [리팩토링] type → source (JSON 필드명 호환)
    /// </summary>
    [Serializable]
    public class ValueSource
    {
        /// <summary>값 소스 타입</summary>
        public ValueSourceType source;

        /// <summary>기본값 (Fixed일 때 사용)</summary>
        public int baseValue;

        /// <summary>배수 (기본 1.0)</summary>
        public float multiplier = 1f;

        /// <summary>최솟값 (RandomRange용)</summary>
        public int min;

        /// <summary>최댓값 (RandomRange용)</summary>
        public int max;

        /// <summary>
        /// 고정값 생성 헬퍼
        /// </summary>
        public static ValueSource Fixed(int value) => new ValueSource
        {
            source = ValueSourceType.Fixed,
            baseValue = value,
            multiplier = 1f
        };

        /// <summary>
        /// 이전 효과 처리 수 참조 헬퍼
        /// </summary>
        public static ValueSource FromPreviousCount(float multiplier = 1f) => new ValueSource
        {
            source = ValueSourceType.PreviousCount,
            multiplier = multiplier
        };

        // =====================================================================
        // 하위 호환 (Step 4에서 제거)
        // =====================================================================

        /// <summary>type 별칭 (하위 호환)</summary>
        [Obsolete("Use source instead")]
        public ValueSourceType type
        {
            get => source;
            set => source = value;
        }
    }

    // =========================================================================
    // 게임 설정 (GameConfig)
    // [Phase 2] const → JSON 기반 프로퍼티로 리팩토링
    // =========================================================================

    /// <summary>
    /// 게임 상수 설정
    /// JSON에서 로드된 값을 반환, 로드 전에는 기본값 사용
    /// </summary>
    public static class GameConfig
    {
        // =====================================================================
        // JSON 데이터 참조
        // =====================================================================

        private static GameConfigData _data;
        private static GameConfigSO _so;  // [E1] SO 참조 추가
        private static bool _initialized = false;

        /// <summary>
        /// JSON 데이터로 초기화
        /// DataLoader.Awake()에서 호출됨
        /// </summary>
        public static void Initialize(GameConfigData data)
        {
            _data = data;
            _initialized = true;
            Debug.Log($"[GameConfig] 초기화 완료 (v{data?.version ?? "null"})");
        }

        /// <summary>
        /// SO에서 직접 초기화 [E1 추가]
        /// DataLoader에서 SO 로드 성공 시 호출
        /// </summary>
        public static void InitializeFromSO(GameConfigSO so)
        {
            _so = so;
            _data = so?.ToGameConfigData();
            _initialized = true;
            Debug.Log($"[GameConfig] SO에서 초기화 완료 (v{_data?.version ?? "null"})");
        }

        /// <summary>
        /// 초기화 여부 확인
        /// </summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// SO 참조 반환 [E1 추가]
        /// </summary>
        public static GameConfigSO GetSO() => _so;


        // =====================================================================
        // 기본 게임 설정
        // =====================================================================

        public static int MaxTurns => _data?.game?.maxTurns ?? 60;
        public static int StartingGold => _data?.game?.startingGold ?? 10;
        public static int StartingLands => _data?.game?.startingLands ?? 2;
        public static int HandSize => _data?.game?.handSize ?? 5;
        public static int MaxHandSize => _data?.game?.maxHandSize ?? 10;
        public static int StartingActions => _data?.game?.startingActions ?? 1;
        public static int MaxHouses => _data?.game?.maxHouses ?? 12;

        // =====================================================================
        // 검증 시스템
        // =====================================================================

        public static int[] ValidationTurns => _data?.validation?.turns ?? new[] { 10, 20, 30, 40, 50 };
        public static int[] ValidationGoldRequired => _data?.validation?.goldRequired ?? new[] { 40, 100, 200, 350, 500 };

        // =====================================================================
        // 약탈 시스템
        // =====================================================================

        public static int[] RaidTurns => _data?.raid?.turns ?? new[] { 8, 16, 24, 32, 40, 48, 56 };

        /// <summary>
        /// 약탈 적 전투력 계산
        /// </summary>
        public static int GetRaidEnemyPower(int turn)
        {
            int basePower = _data?.raid?.enemyPowerBase ?? 10;
            int perTurn = _data?.raid?.enemyPowerPerTurn ?? 3;
            return (turn * perTurn) + basePower;
        }

        // =====================================================================
        // 최종 전투
        // =====================================================================

        public static int FinalBattleTurn => _data?.raid?.finalBattleTurn ?? 60;
        public static int FinalBattleRequiredPower => _data?.raid?.finalBattleRequiredPower ?? 500;

        // =====================================================================
        // 충성도
        // =====================================================================

        public static int ChildLoyalty => _data?.loyalty?.childDefault ?? 50;
        public static int AdultLoyalty => _data?.loyalty?.adultDefault ?? 100;
        public static int LoyaltyDeficitPenalty => _data?.loyalty?.deficitPenalty ?? -20;
        public static int LoyaltySurplusBonus => _data?.loyalty?.surplusBonus ?? 10;

        // =====================================================================
        // 교배
        // =====================================================================

        public static int PregnancyDuration => _data?.breeding?.pregnancyDuration ?? 3;
        public static int[] FertilityChance => _data?.breeding?.fertilityChance ?? new[] { 20, 40, 60, 80, 100 };

        // =====================================================================
        // 전직
        // =====================================================================

        public static int[] PromotionCosts => _data?.promotion?.costs ?? new[] { 10, 25, 50 };
        public static int MaxPromotionLevel => _data?.promotion?.maxLevel ?? 3;

        // =====================================================================
        // 사유지
        // =====================================================================

        public static int[] LandDevelopCosts => _data?.land?.developCosts ?? new[] { 15, 30, 60 };

        // =====================================================================
        // 전투력
        // =====================================================================

        public static int GetJobBaseCombatPower(Job job)
        {
            if (_data?.combat?.jobBasePower != null)
            {
                return _data.combat.jobBasePower.GetPower(job);
            }

            // 폴백 값
            switch (job)
            {
                case Job.Pawn: return 10;
                case Job.Knight: return 30;
                case Job.Bishop: return 15;
                case Job.Rook: return 25;
                case Job.Queen: return 35;
                default: return 0;
            }
        }

        public static int PromotionBonusPerLevel => _data?.combat?.promotionBonusPerLevel ?? 10;
        public static float OldAgeMultiplier => _data?.combat?.oldAgeMultiplier ?? 0.75f;

        // =====================================================================
        // 이벤트
        // =====================================================================

        public static int EventChance => _data?.eventConfig?.triggerChance ?? 80;
        public static int PositiveEventRatio => _data?.eventConfig?.positiveRatio ?? 50;
        public static int NegativeEventRatio => _data?.eventConfig?.negativeRatio ?? 20;
        public static int ChoiceEventRatio => _data?.eventConfig?.choiceRatio ?? 30;

        // =====================================================================
        // 전직 확률
        // =====================================================================

        public static int KnightChance => _data?.promotionJobChance?.Knight ?? 30;
        public static int BishopChance => _data?.promotionJobChance?.Bishop ?? 30;
        public static int RookChance => _data?.promotionJobChance?.Rook ?? 30;
        public static int QueenChance => _data?.promotionJobChance?.Queen ?? 10;

        // =====================================================================
        // 카드 등급 확률
        // =====================================================================

        public static int BasicCardChance => _data?.cardRarityChance?.Basic ?? 60;
        public static int AdvancedCardChance => _data?.cardRarityChance?.Advanced ?? 35;
        public static int RareCardChance => _data?.cardRarityChance?.Rare ?? 5;

        // =====================================================================
        // 성장 규칙 (UnitInstance에서 참조)
        // =====================================================================

        public static int GetStageDuration(GrowthStage stage)
        {
            if (_data?.growth != null)
            {
                return _data.growth.GetStageDuration(stage);
            }

            // 폴백 값
            switch (stage)
            {
                case GrowthStage.Child: return 3;
                case GrowthStage.Young: return 10;
                case GrowthStage.Middle: return 10;
                case GrowthStage.Old: return 5;
                default: return 0;
            }
        }

        public static int MiddleBreedableRemainingTurns => _data?.growth?.middleBreedableRemainingTurns ?? 5;

        public static int GetOldAgeDeathChance(int oldAgeTurns)
        {
            if (_data?.growth?.oldAgeDeathChance != null)
            {
                return _data.growth.oldAgeDeathChance.GetChance(oldAgeTurns);
            }

            // 폴백 값
            if (oldAgeTurns <= 5) return 0;
            switch (oldAgeTurns)
            {
                case 6: return 20;
                case 7: return 40;
                case 8: return 60;
                case 9: return 80;
                default: return 100;
            }
        }

        // =====================================================================
        // 구버전 호환 (Obsolete)
        // =====================================================================

        [Obsolete("Birth is now handled by BreedingSystem")]
        public static readonly int[] BirthTurns = { };

        [Obsolete("ForcedDeath is removed")]
        public static readonly int[] ForcedDeathTurns = { };
    }
}