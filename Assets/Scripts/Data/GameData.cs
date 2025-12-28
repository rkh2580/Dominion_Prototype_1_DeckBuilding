// =============================================================================
// GameData.cs
// 유닛, 이벤트, 게임 상태 등 데이터 클래스 정의
// =============================================================================

using System;
using System.Collections.Generic;

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
        public string unitId;               // 고유 ID
        public string unitName;             // 표시 이름 (예: "주민 A")
        public Job job;                     // 직업
        public GrowthStage stage;           // 성장 단계
        public int stageRemainingTurns;     // 현재 단계 잔여 턴
        public int upgradeLevel;            // 강화 레벨 (0~5)
        public List<string> ownedCardIds;   // 종속 카드 인스턴스 ID 목록

        // 상태
        public bool hasDisease;             // 질병 상태 (강화 불가)
        public bool upgradedThisTurn;       // 이번 턴 강화 여부

        // 노년 전용
        public int oldAgeTurns;             // 노년 경과 턴 (사망 확률 계산용)

        /// <summary>
        /// 새 유닛 생성
        /// </summary>
        public static UnitInstance Create(string name, Job job, GrowthStage stage)
        {
            int remainingTurns = GetStageDuration(stage);
            
            return new UnitInstance
            {
                unitId = Guid.NewGuid().ToString(),
                unitName = name,
                job = job,
                stage = stage,
                stageRemainingTurns = remainingTurns,
                upgradeLevel = 0,
                ownedCardIds = new List<string>(),
                hasDisease = false,
                upgradedThisTurn = false,
                oldAgeTurns = 0
            };
        }

        /// <summary>
        /// 성장 단계별 기본 지속 턴 수
        /// </summary>
        public static int GetStageDuration(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Child:  return 3;
                case GrowthStage.Young:  return 10;
                case GrowthStage.Middle: return 10;
                case GrowthStage.Old:    return 5; // 기본 5턴, 이후 사망 확률
                default: return 0;
            }
        }

        /// <summary>
        /// 강화 가능 여부
        /// </summary>
        public bool CanUpgrade()
        {
            // 청년/중년만 강화 가능
            if (stage != GrowthStage.Young && stage != GrowthStage.Middle)
                return false;
            
            // 이번 턴 이미 강화했으면 불가
            if (upgradedThisTurn)
                return false;
            
            // 질병 상태면 불가
            if (hasDisease)
                return false;
            
            // 최대 레벨이면 불가
            if (upgradeLevel >= 5)
                return false;
            
            return true;
        }

        /// <summary>
        /// 다음 강화 비용
        /// </summary>
        public int GetNextUpgradeCost()
        {
            switch (upgradeLevel)
            {
                case 0: return 5;
                case 1: return 15;
                case 2: return 30;
                case 3: return 60;
                case 4: return 100;
                default: return int.MaxValue; // 최대 레벨
            }
        }

        /// <summary>
        /// 노년 사망 확률 (%)
        /// </summary>
        public int GetDeathChance()
        {
            if (stage != GrowthStage.Old) return 0;
            
            switch (oldAgeTurns)
            {
                case int n when n <= 5: return 0;
                case 6: return 20;
                case 7: return 40;
                case 8: return 60;
                case 9: return 80;
                default: return 100; // 10턴 이상
            }
        }
    }

    /// <summary>
    /// 직업 정의 (JSON에서 로드)
    /// </summary>
    [Serializable]
    public class JobDefinition
    {
        public Job job;
        public string displayName;      // 표시 이름 (예: "폰", "나이트")
        public string description;      // 직업 설명
        public List<string> cardPoolBasic;      // 기본 등급 카드 ID 목록
        public List<string> cardPoolAdvanced;   // 고급 등급 카드 ID 목록
        public List<string> cardPoolRare;       // 희귀 등급 카드 ID 목록
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
    // 이벤트 관련
    // =========================================================================

    /// <summary>
    /// 이벤트 정의 (JSON에서 로드)
    /// </summary>
    [Serializable]
    public class EventData
    {
        public string eventId;          // 고유 ID
        public string eventName;        // 표시 이름
        public EventType eventType;     // 이벤트 종류
        public string description;      // 설명 텍스트

        // 발생 조건
        public EventCondition condition;

        // 효과 (선택 이벤트는 choiceA, choiceB)
        public EventEffect[] effects;           // 기본 효과 또는 선택지 A
        public EventEffect[] choiceBEffects;    // 선택지 B (선택 이벤트만)
        public string choiceAText;              // 선택지 A 버튼 텍스트
        public string choiceBText;              // 선택지 B 버튼 텍스트
    }

    /// <summary>
    /// 이벤트 발생 조건
    /// </summary>
    [Serializable]
    public class EventCondition
    {
        public EventConditionType type;
        public int value;               // 비교 값 (GoldGreaterThan 등에서 사용)
    }

    /// <summary>
    /// 이벤트 효과
    /// </summary>
    [Serializable]
    public class EventEffect
    {
        public EventEffectType effectType;
        public int value;
        public string cardId;           // 카드 추가/삭제 시 대상 카드 ID
        public string pollutionType;    // 오염 카드 추가 시 종류
    }

    /// <summary>
    /// 이벤트 효과 종류
    /// </summary>
    public enum EventEffectType
    {
        AddGold,                // 골드 추가/감소
        AddPollutionCard,       // 오염 카드 추가
        RemovePollutionCard,    // 오염 카드 제거 (랜덤)
        AddCard,                // 특정 카드 추가
        UpgradeCopperToSilver,  // 동화 → 은화 업그레이드
        FreeUpgrade,            // 무료 강화
        UpgradeDiscount,        // 강화 할인 (이번 턴)
        AddUnit,                // 유닛 추가
        AddMaintenanceCost,     // 유지비 추가 (지속)
        UnitUpgradeBonus        // 유닛 강화 레벨 추가
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
    /// 게임 전체 상태
    /// </summary>
    [Serializable]
    public class GameState
    {
        // 턴 정보
        public int currentTurn;
        public GamePhase currentPhase;
        public int remainingActions;

        // 경제
        public int gold;
        public int maintenanceCost;         // 유지비 (자동 계산)

        // 덱 상태
        public List<CardInstance> deck;
        public List<CardInstance> hand;
        public List<CardInstance> discardPile;
        public List<CardInstance> playArea;  // 이번 턴 플레이한 카드

        // 유닛
        public List<UnitInstance> units;

        // 지속 효과
        public List<PersistentEffect> activeEffects;

        // 이번 턴 부스트 상태
        public float goldMultiplier;        // 골드 배수 (기본 1.0)
        public int goldBonus;               // 골드 고정 보너스

        // 이번 턴 상태
        public bool pollutionIgnored;       // 오염 효과 무시 여부
        public int upgradeDiscount;         // 강화 할인율 (%)

        // 게임 종료
        public GameEndState endState;

        /// <summary>
        /// 새 게임 상태 생성 (초기화)
        /// </summary>
        public static GameState CreateNew()
        {
            return new GameState
            {
                currentTurn = 0,
                currentPhase = GamePhase.TurnStart,
                remainingActions = 0,
                gold = 0,
                maintenanceCost = 0,
                deck = new List<CardInstance>(),
                hand = new List<CardInstance>(),
                discardPile = new List<CardInstance>(),
                playArea = new List<CardInstance>(),
                units = new List<UnitInstance>(),
                activeEffects = new List<PersistentEffect>(),
                goldMultiplier = 1f,
                goldBonus = 0,
                pollutionIgnored = false,
                upgradeDiscount = 0,
                endState = GameEndState.None
            };
        }

        /// <summary>
        /// 턴 시작 시 임시 상태 초기화
        /// </summary>
        public void ResetTurnState()
        {
            goldMultiplier = 1f;
            goldBonus = 0;
            pollutionIgnored = false;
            upgradeDiscount = 0;

            // 유닛 턴 상태 초기화
            foreach (var unit in units)
            {
                unit.upgradedThisTurn = false;
            }

            // 카드 부스트 상태 초기화
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
    }

    // =========================================================================
    // 지속 효과
    // =========================================================================

    /// <summary>
    /// 지속 효과 (여러 턴에 걸쳐 적용)
    /// </summary>
    [Serializable]
    public class PersistentEffect
    {
        public string effectId;             // 고유 ID
        public PersistentEffectType type;   // 효과 종류
        public int remainingTurns;          // 남은 턴 수
        public int value;                   // 효과 값
    }

    /// <summary>
    /// 지속 효과 종류
    /// </summary>
    public enum PersistentEffectType
    {
        DelayedGold,            // N턴 후 골드 획득 (비축)
        GoldPerTurn,            // 매 턴 골드 획득 (견고함)
        MaintenanceIncrease     // 유지비 증가 (흉년)
    }

    // =========================================================================
    // 게임 설정 (상수)
    // =========================================================================

    /// <summary>
    /// 게임 설정 상수
    /// </summary>
    public static class GameConfig
    {
        // 기본 설정
        public const int MaxTurns = 45;
        public const int StartingGold = 0;
        public const int HandSize = 5;
        public const int MaxHandSize = 10;
        public const int StartingActions = 1;

        // 검증 턴
        public static readonly int[] ValidationTurns = { 11, 23, 35 };
        public static readonly int[] ValidationGoldRequired = { 30, 80, 150 }; // TBD - 플레이테스트 후 조정

        // 출생/사망 턴
        public static readonly int[] BirthTurns = { 6, 12, 18, 24, 30, 36, 42 };
        public static readonly int[] ForcedDeathTurns = { 12, 24, 36 };

        // 이벤트 확률
        public const int EventChance = 80;          // 80%
        public const int PositiveEventRatio = 50;   // 50%
        public const int NegativeEventRatio = 20;   // 20%
        public const int ChoiceEventRatio = 30;     // 30%

        // 전직 확률
        public const int KnightChance = 30;
        public const int BishopChance = 30;
        public const int RookChance = 30;
        public const int QueenChance = 10;

        // 강화 3지선다 확률
        public const int BasicCardChance = 60;
        public const int AdvancedCardChance = 35;
        public const int RareCardChance = 5;
    }
}
