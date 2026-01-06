// =============================================================================
// GameConfigSO.cs
// 게임 설정 ScriptableObject
// =============================================================================
// [E1] JSON 기반 설정을 SO로 전환
// - 에디터에서 직접 수정 가능
// - 기존 GameConfig 정적 클래스 API 유지
// =============================================================================

using System;
using UnityEngine;

namespace DeckBuildingEconomy.Data
{
    /// <summary>
    /// 게임 설정 ScriptableObject
    /// GameConfig.json의 SO 버전
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfigSO", menuName = "DeckBuilding/GameConfigSO")]
    public class GameConfigSO : ScriptableObject
    {
        [Header("버전 정보")]
        public string version = "1.0.0";

        [Header("기본 게임 설정")]
        public GameSettingsSO game = new GameSettingsSO();

        [Header("검증 시스템")]
        public ValidationSettingsSO validation = new ValidationSettingsSO();

        [Header("약탈 시스템")]
        public RaidSettingsSO raid = new RaidSettingsSO();

        [Header("충성도")]
        public LoyaltySettingsSO loyalty = new LoyaltySettingsSO();

        [Header("교배")]
        public BreedingSettingsSO breeding = new BreedingSettingsSO();

        [Header("전직")]
        public PromotionSettingsSO promotion = new PromotionSettingsSO();

        [Header("사유지")]
        public LandSettingsSO land = new LandSettingsSO();

        [Header("이벤트")]
        public EventSettingsSO eventConfig = new EventSettingsSO();

        [Header("전직 직업 확률")]
        public PromotionJobChanceSO promotionJobChance = new PromotionJobChanceSO();

        [Header("카드 등급 확률")]
        public CardRarityChanceSO cardRarityChance = new CardRarityChanceSO();

        [Header("전투")]
        public CombatSettingsSO combat = new CombatSettingsSO();

        [Header("성장")]
        public GrowthSettingsSO growth = new GrowthSettingsSO();

        // =====================================================================
        // GameConfigData 변환 (호환성)
        // =====================================================================

        /// <summary>
        /// 기존 GameConfigData로 변환 (레거시 호환)
        /// </summary>
        public GameConfigData ToGameConfigData()
        {
            return new GameConfigData
            {
                version = version,
                game = game.ToGameSettings(),
                validation = validation.ToValidationSettings(),
                raid = raid.ToRaidSettings(),
                loyalty = loyalty.ToLoyaltySettings(),
                breeding = breeding.ToBreedingSettings(),
                promotion = promotion.ToPromotionSettings(),
                land = land.ToLandSettings(),
                eventConfig = eventConfig.ToEventSettings(),
                promotionJobChance = promotionJobChance.ToPromotionJobChance(),
                cardRarityChance = cardRarityChance.ToCardRarityChance(),
                combat = combat.ToCombatSettings(),
                growth = growth.ToGrowthSettings()
            };
        }

        /// <summary>
        /// GameConfigData에서 값 복사
        /// </summary>
        public void FromGameConfigData(GameConfigData data)
        {
            if (data == null) return;

            version = data.version ?? "1.0.0";
            game.FromGameSettings(data.game);
            validation.FromValidationSettings(data.validation);
            raid.FromRaidSettings(data.raid);
            loyalty.FromLoyaltySettings(data.loyalty);
            breeding.FromBreedingSettings(data.breeding);
            promotion.FromPromotionSettings(data.promotion);
            land.FromLandSettings(data.land);
            eventConfig.FromEventSettings(data.eventConfig);
            promotionJobChance.FromPromotionJobChance(data.promotionJobChance);
            cardRarityChance.FromCardRarityChance(data.cardRarityChance);
            combat.FromCombatSettings(data.combat);
            growth.FromGrowthSettings(data.growth);
        }
    }

    // =========================================================================
    // 하위 Serializable 클래스들
    // =========================================================================

    /// <summary>
    /// 기본 게임 설정
    /// </summary>
    [Serializable]
    public class GameSettingsSO
    {
        [Tooltip("최대 턴 수")]
        public int maxTurns = 60;

        [Tooltip("시작 골드")]
        public int startingGold = 10;

        [Tooltip("시작 사유지 개수")]
        public int startingLands = 2;

        [Tooltip("턴 시작 드로우 수")]
        public int handSize = 5;

        [Tooltip("손패 상한")]
        public int maxHandSize = 10;

        [Tooltip("시작 액션 수")]
        public int startingActions = 1;

        [Tooltip("최대 집 수")]
        public int maxHouses = 12;

        public GameSettings ToGameSettings()
        {
            return new GameSettings
            {
                maxTurns = maxTurns,
                startingGold = startingGold,
                startingLands = startingLands,
                handSize = handSize,
                maxHandSize = maxHandSize,
                startingActions = startingActions,
                maxHouses = maxHouses
            };
        }

        public void FromGameSettings(GameSettings s)
        {
            if (s == null) return;
            maxTurns = s.maxTurns;
            startingGold = s.startingGold;
            startingLands = s.startingLands;
            handSize = s.handSize;
            maxHandSize = s.maxHandSize;
            startingActions = s.startingActions;
            maxHouses = s.maxHouses;
        }
    }

    /// <summary>
    /// 검증 시스템 설정
    /// </summary>
    [Serializable]
    public class ValidationSettingsSO
    {
        [Tooltip("검증 턴 목록")]
        public int[] turns = { 10, 20, 30, 40, 50 };

        [Tooltip("각 검증에 필요한 골드")]
        public int[] goldRequired = { 40, 100, 200, 350, 500 };

        public ValidationSettings ToValidationSettings()
        {
            return new ValidationSettings
            {
                turns = turns,
                goldRequired = goldRequired
            };
        }

        public void FromValidationSettings(ValidationSettings s)
        {
            if (s == null) return;
            turns = s.turns ?? turns;
            goldRequired = s.goldRequired ?? goldRequired;
        }
    }

    /// <summary>
    /// 약탈 시스템 설정
    /// </summary>
    [Serializable]
    public class RaidSettingsSO
    {
        [Tooltip("약탈 턴 목록")]
        public int[] turns = { 8, 16, 24, 32, 40, 48, 56 };

        [Tooltip("적 기본 전투력")]
        public int enemyPowerBase = 10;

        [Tooltip("턴당 추가 전투력")]
        public int enemyPowerPerTurn = 3;

        [Tooltip("최종 전투 턴")]
        public int finalBattleTurn = 60;

        [Tooltip("최종 전투 요구 전투력")]
        public int finalBattleRequiredPower = 500;

        public RaidSettings ToRaidSettings()
        {
            return new RaidSettings
            {
                turns = turns,
                enemyPowerBase = enemyPowerBase,
                enemyPowerPerTurn = enemyPowerPerTurn,
                finalBattleTurn = finalBattleTurn,
                finalBattleRequiredPower = finalBattleRequiredPower
            };
        }

        public void FromRaidSettings(RaidSettings s)
        {
            if (s == null) return;
            turns = s.turns ?? turns;
            enemyPowerBase = s.enemyPowerBase;
            enemyPowerPerTurn = s.enemyPowerPerTurn;
            finalBattleTurn = s.finalBattleTurn;
            finalBattleRequiredPower = s.finalBattleRequiredPower;
        }
    }

    /// <summary>
    /// 충성도 설정
    /// </summary>
    [Serializable]
    public class LoyaltySettingsSO
    {
        [Tooltip("유년 기본 충성도")]
        public int childDefault = 50;

        [Tooltip("성인 기본 충성도")]
        public int adultDefault = 100;

        [Tooltip("적자 시 충성도 패널티")]
        public int deficitPenalty = -20;

        [Tooltip("흑자 시 충성도 보너스")]
        public int surplusBonus = 10;

        public LoyaltySettings ToLoyaltySettings()
        {
            return new LoyaltySettings
            {
                childDefault = childDefault,
                adultDefault = adultDefault,
                deficitPenalty = deficitPenalty,
                surplusBonus = surplusBonus
            };
        }

        public void FromLoyaltySettings(LoyaltySettings s)
        {
            if (s == null) return;
            childDefault = s.childDefault;
            adultDefault = s.adultDefault;
            deficitPenalty = s.deficitPenalty;
            surplusBonus = s.surplusBonus;
        }
    }

    /// <summary>
    /// 교배 설정
    /// </summary>
    [Serializable]
    public class BreedingSettingsSO
    {
        [Tooltip("임신 기간 (턴)")]
        public int pregnancyDuration = 3;

        [Tooltip("연속 교배 시도 시 임신 확률 (%)")]
        public int[] fertilityChance = { 20, 40, 60, 80, 100 };

        public BreedingSettings ToBreedingSettings()
        {
            return new BreedingSettings
            {
                pregnancyDuration = pregnancyDuration,
                fertilityChance = fertilityChance
            };
        }

        public void FromBreedingSettings(BreedingSettings s)
        {
            if (s == null) return;
            pregnancyDuration = s.pregnancyDuration;
            fertilityChance = s.fertilityChance ?? fertilityChance;
        }
    }

    /// <summary>
    /// 전직 설정
    /// </summary>
    [Serializable]
    public class PromotionSettingsSO
    {
        [Tooltip("레벨별 전직 비용")]
        public int[] costs = { 10, 25, 50 };

        [Tooltip("최대 전직 레벨")]
        public int maxLevel = 3;

        public PromotionSettings ToPromotionSettings()
        {
            return new PromotionSettings
            {
                costs = costs,
                maxLevel = maxLevel
            };
        }

        public void FromPromotionSettings(PromotionSettings s)
        {
            if (s == null) return;
            costs = s.costs ?? costs;
            maxLevel = s.maxLevel;
        }
    }

    /// <summary>
    /// 사유지 설정
    /// </summary>
    [Serializable]
    public class LandSettingsSO
    {
        [Tooltip("레벨별 개발 비용")]
        public int[] developCosts = { 15, 30, 60 };

        public LandSettings ToLandSettings()
        {
            return new LandSettings
            {
                developCosts = developCosts
            };
        }

        public void FromLandSettings(LandSettings s)
        {
            if (s == null) return;
            developCosts = s.developCosts ?? developCosts;
        }
    }

    /// <summary>
    /// 이벤트 설정
    /// </summary>
    [Serializable]
    public class EventSettingsSO
    {
        [Tooltip("이벤트 발생 확률 (%)")]
        [Range(0, 100)]
        public int triggerChance = 80;

        [Tooltip("긍정 이벤트 비율 (%)")]
        [Range(0, 100)]
        public int positiveRatio = 50;

        [Tooltip("부정 이벤트 비율 (%)")]
        [Range(0, 100)]
        public int negativeRatio = 20;

        [Tooltip("선택 이벤트 비율 (%)")]
        [Range(0, 100)]
        public int choiceRatio = 30;

        public EventSettings ToEventSettings()
        {
            return new EventSettings
            {
                triggerChance = triggerChance,
                positiveRatio = positiveRatio,
                negativeRatio = negativeRatio,
                choiceRatio = choiceRatio
            };
        }

        public void FromEventSettings(EventSettings s)
        {
            if (s == null) return;
            triggerChance = s.triggerChance;
            positiveRatio = s.positiveRatio;
            negativeRatio = s.negativeRatio;
            choiceRatio = s.choiceRatio;
        }
    }

    /// <summary>
    /// 전직 직업 확률
    /// </summary>
    [Serializable]
    public class PromotionJobChanceSO
    {
        [Range(0, 100)] public int Knight = 30;
        [Range(0, 100)] public int Bishop = 30;
        [Range(0, 100)] public int Rook = 30;
        [Range(0, 100)] public int Queen = 10;

        public PromotionJobChance ToPromotionJobChance()
        {
            return new PromotionJobChance
            {
                Knight = Knight,
                Bishop = Bishop,
                Rook = Rook,
                Queen = Queen
            };
        }

        public void FromPromotionJobChance(PromotionJobChance s)
        {
            if (s == null) return;
            Knight = s.Knight;
            Bishop = s.Bishop;
            Rook = s.Rook;
            Queen = s.Queen;
        }
    }

    /// <summary>
    /// 카드 등급 확률
    /// </summary>
    [Serializable]
    public class CardRarityChanceSO
    {
        [Range(0, 100)] public int Basic = 60;
        [Range(0, 100)] public int Advanced = 35;
        [Range(0, 100)] public int Rare = 5;

        public CardRarityChance ToCardRarityChance()
        {
            return new CardRarityChance
            {
                Basic = Basic,
                Advanced = Advanced,
                Rare = Rare
            };
        }

        public void FromCardRarityChance(CardRarityChance s)
        {
            if (s == null) return;
            Basic = s.Basic;
            Advanced = s.Advanced;
            Rare = s.Rare;
        }
    }

    /// <summary>
    /// 전투 설정
    /// </summary>
    [Serializable]
    public class CombatSettingsSO
    {
        [Header("직업별 기본 전투력")]
        public int pawnPower = 10;
        public int knightPower = 30;
        public int bishopPower = 15;
        public int rookPower = 25;
        public int queenPower = 35;

        [Header("전직 보너스")]
        [Tooltip("레벨당 전투력 증가")]
        public int promotionBonusPerLevel = 10;

        [Header("노년 패널티")]
        [Tooltip("노년 전투력 배율")]
        [Range(0f, 1f)]
        public float oldAgeMultiplier = 0.75f;

        /// <summary>
        /// 직업별 전투력 반환
        /// </summary>
        public int GetJobBasePower(Job job)
        {
            switch (job)
            {
                case Job.Pawn: return pawnPower;
                case Job.Knight: return knightPower;
                case Job.Bishop: return bishopPower;
                case Job.Rook: return rookPower;
                case Job.Queen: return queenPower;
                default: return 0;
            }
        }

        public CombatSettings ToCombatSettings()
        {
            return new CombatSettings
            {
                jobBasePower = new JobBasePower
                {
                    Pawn = pawnPower,
                    Knight = knightPower,
                    Bishop = bishopPower,
                    Rook = rookPower,
                    Queen = queenPower
                },
                promotionBonusPerLevel = promotionBonusPerLevel,
                oldAgeMultiplier = oldAgeMultiplier
            };
        }

        public void FromCombatSettings(CombatSettings s)
        {
            if (s == null) return;
            if (s.jobBasePower != null)
            {
                pawnPower = s.jobBasePower.Pawn;
                knightPower = s.jobBasePower.Knight;
                bishopPower = s.jobBasePower.Bishop;
                rookPower = s.jobBasePower.Rook;
                queenPower = s.jobBasePower.Queen;
            }
            promotionBonusPerLevel = s.promotionBonusPerLevel;
            oldAgeMultiplier = s.oldAgeMultiplier;
        }
    }

    /// <summary>
    /// 성장 설정
    /// </summary>
    [Serializable]
    public class GrowthSettingsSO
    {
        [Header("성장 단계별 기간 (턴)")]
        public int childDuration = 3;
        public int youngDuration = 10;
        public int middleDuration = 10;
        public int oldBaseDuration = 5;

        [Header("교배 조건")]
        [Tooltip("중년 교배 가능 최소 잔여 턴 (D2에서 삭제 예정)")]
        public int middleBreedableRemainingTurns = 5;

        [Header("노년 사망 확률 (%)")]
        public int deathChanceTurn6 = 20;
        public int deathChanceTurn7 = 40;
        public int deathChanceTurn8 = 60;
        public int deathChanceTurn9 = 80;
        public int deathChanceTurn10Plus = 100;

        /// <summary>
        /// 성장 단계별 기간 반환
        /// </summary>
        public int GetStageDuration(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Child: return childDuration;
                case GrowthStage.Young: return youngDuration;
                case GrowthStage.Middle: return middleDuration;
                case GrowthStage.Old: return oldBaseDuration;
                default: return 0;
            }
        }

        /// <summary>
        /// 노년 사망 확률 반환
        /// </summary>
        public int GetOldAgeDeathChance(int oldAgeTurns)
        {
            if (oldAgeTurns <= 5) return 0;
            switch (oldAgeTurns)
            {
                case 6: return deathChanceTurn6;
                case 7: return deathChanceTurn7;
                case 8: return deathChanceTurn8;
                case 9: return deathChanceTurn9;
                default: return deathChanceTurn10Plus;
            }
        }

        public GrowthSettings ToGrowthSettings()
        {
            return new GrowthSettings
            {
                childDuration = childDuration,
                youngDuration = youngDuration,
                middleDuration = middleDuration,
                oldBaseDuration = oldBaseDuration,
                middleBreedableRemainingTurns = middleBreedableRemainingTurns,
                oldAgeDeathChance = new OldAgeDeathChance
                {
                    turn6 = deathChanceTurn6,
                    turn7 = deathChanceTurn7,
                    turn8 = deathChanceTurn8,
                    turn9 = deathChanceTurn9,
                    turn10Plus = deathChanceTurn10Plus
                }
            };
        }

        public void FromGrowthSettings(GrowthSettings s)
        {
            if (s == null) return;
            childDuration = s.childDuration;
            youngDuration = s.youngDuration;
            middleDuration = s.middleDuration;
            oldBaseDuration = s.oldBaseDuration;
            middleBreedableRemainingTurns = s.middleBreedableRemainingTurns;
            if (s.oldAgeDeathChance != null)
            {
                deathChanceTurn6 = s.oldAgeDeathChance.turn6;
                deathChanceTurn7 = s.oldAgeDeathChance.turn7;
                deathChanceTurn8 = s.oldAgeDeathChance.turn8;
                deathChanceTurn9 = s.oldAgeDeathChance.turn9;
                deathChanceTurn10Plus = s.oldAgeDeathChance.turn10Plus;
            }
        }
    }
}