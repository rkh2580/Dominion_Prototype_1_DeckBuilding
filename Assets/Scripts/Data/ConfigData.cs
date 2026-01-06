// =============================================================================
// ConfigData.cs
// JSON ì„¤ì • íŒŒì¼ ë§¤í•‘ìš© ë°ì´í„° í´ëž˜ìŠ¤
// =============================================================================
// [Phase 2] ê²Œìž„ ìƒìˆ˜ ë°ì´í„°í™”
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBuildingEconomy.Data
{
    // =========================================================================
    // GameConfig.json ë§¤í•‘
    // =========================================================================

    /// <summary>
    /// GameConfig.json ë£¨íŠ¸ í´ëž˜ìŠ¤
    /// </summary>
    [Serializable]
    public class GameConfigData
    {
        public string version;
        public GameSettings game;
        public ValidationSettings validation;
        public RaidSettings raid;
        public LoyaltySettings loyalty;
        public BreedingSettings breeding;
        public PromotionSettings promotion;
        public LandSettings land;
        public EventSettings eventConfig;
        public PromotionJobChance promotionJobChance;
        public CardRarityChance cardRarityChance;
        public CombatSettings combat;
        public GrowthSettings growth;
    }

    /// <summary>
    /// ê¸°ë³¸ ê²Œìž„ ì„¤ì •
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        public int maxTurns;
        public int startingGold;
        public int startingLands;
        public int handSize;
        public int maxHandSize;
        public int startingActions;
        public int maxHouses;
    }

    /// <summary>
    /// ê²€ì¦ ì‹œìŠ¤í…œ ì„¤ì •
    /// </summary>
    [Serializable]
    public class ValidationSettings
    {
        public int[] turns;
        public int[] goldRequired;
    }

    /// <summary>
    /// ì•½íƒˆ ì‹œìŠ¤í…œ ì„¤ì •
    /// </summary>
    [Serializable]
    public class RaidSettings
    {
        public int[] turns;
        public int enemyPowerBase;
        public int enemyPowerPerTurn;
        public int finalBattleTurn;
        public int finalBattleRequiredPower;
    }

    /// <summary>
    /// ì¶©ì„±ë„ ì„¤ì •
    /// </summary>
    [Serializable]
    public class LoyaltySettings
    {
        public int childDefault;
        public int adultDefault;
        public int deficitPenalty;
        public int surplusBonus;
    }

    /// <summary>
    /// êµë°° ì„¤ì •
    /// </summary>
    [Serializable]
    public class BreedingSettings
    {
        public int pregnancyDuration;
        public int[] fertilityChance;
    }

    /// <summary>
    /// ì „ì§ ì„¤ì •
    /// </summary>
    [Serializable]
    public class PromotionSettings
    {
        public int[] costs;
        public int maxLevel;
    }

    /// <summary>
    /// ì‚¬ìœ ì§€ ì„¤ì •
    /// </summary>
    [Serializable]
    public class LandSettings
    {
        public int[] developCosts;
    }

    /// <summary>
    /// ì´ë²¤íŠ¸ ì„¤ì •
    /// </summary>
    [Serializable]
    public class EventSettings
    {
        public int triggerChance;
        public int positiveRatio;
        public int negativeRatio;
        public int choiceRatio;
    }

    /// <summary>
    /// ì „ì§ ì‹œ ì§ì—… í™•ë¥ 
    /// </summary>
    [Serializable]
    public class PromotionJobChance
    {
        public int Knight;
        public int Bishop;
        public int Rook;
        public int Queen;
    }

    /// <summary>
    /// ì¹´ë“œ ë“±ê¸‰ í™•ë¥ 
    /// </summary>
    [Serializable]
    public class CardRarityChance
    {
        public int Basic;
        public int Advanced;
        public int Rare;
    }

    /// <summary>
    /// ì „íˆ¬ ì„¤ì •
    /// </summary>
    [Serializable]
    public class CombatSettings
    {
        public JobBasePower jobBasePower;
        public int promotionBonusPerLevel;
        public float oldAgeMultiplier;
    }

    /// <summary>
    /// ì§ì—…ë³„ ê¸°ë³¸ ì „íˆ¬ë ¥
    /// </summary>
    [Serializable]
    public class JobBasePower
    {
        public int Pawn;
        public int Knight;
        public int Bishop;
        public int Rook;
        public int Queen;

        /// <summary>
        /// Job enumìœ¼ë¡œ ì „íˆ¬ë ¥ ì¡°íšŒ
        /// </summary>
        public int GetPower(Job job)
        {
            switch (job)
            {
                case Job.Pawn: return Pawn;
                case Job.Knight: return Knight;
                case Job.Bishop: return Bishop;
                case Job.Rook: return Rook;
                case Job.Queen: return Queen;
                default: return 0;
            }
        }
    }

    /// <summary>
    /// ì„±ìž¥ ì„¤ì •
    /// </summary>
    [Serializable]
    public class GrowthSettings
    {
        public int childDuration;
        public int youngDuration;
        public int middleDuration;
        public int oldBaseDuration;
        public int middleBreedableRemainingTurns;
        public OldAgeDeathChance oldAgeDeathChance;

        /// <summary>
        /// GrowthStageë¡œ ê¸°ê°„ ì¡°íšŒ
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
    }

    /// <summary>
    /// ë…¸ë…„ ì‚¬ë§ í™•ë¥  (í„´ë³„)
    /// </summary>
    [Serializable]
    public class OldAgeDeathChance
    {
        public int turn6;
        public int turn7;
        public int turn8;
        public int turn9;
        public int turn10Plus;

        /// <summary>
        /// ë…¸ë…„ ê²½ê³¼ í„´ìœ¼ë¡œ ì‚¬ë§ í™•ë¥  ì¡°íšŒ
        /// </summary>
        public int GetChance(int oldAgeTurns)
        {
            if (oldAgeTurns <= 5) return 0;
            switch (oldAgeTurns)
            {
                case 6: return turn6;
                case 7: return turn7;
                case 8: return turn8;
                case 9: return turn9;
                default: return turn10Plus; // 10í„´ ì´ìƒ
            }
        }
    }

    // =========================================================================
    // TreasureGrades.json ë§¤í•‘
    // =========================================================================

    /// <summary>
    /// TreasureGrades.json ë£¨íŠ¸ í´ëž˜ìŠ¤
    /// </summary>
    [Serializable]
    public class TreasureGradesData
    {
        public string version;
        public TreasureGradeInfo[] grades;
        public string[] upgradeChain;

        /// <summary>
        /// TreasureGrade enumìœ¼ë¡œ ë“±ê¸‰ ì •ë³´ ì¡°íšŒ
        /// </summary>
        public TreasureGradeInfo GetGradeInfo(TreasureGrade grade)
        {
            int enumVal = (int)grade;
            if (grades != null)
            {
                foreach (var g in grades)
                {
                    if (g.enumValue == enumVal)
                        return g;
                }
            }
            return null;
        }

        /// <summary>
        /// TreasureGrade enumìœ¼ë¡œ ê³¨ë“œ ê°’ ì¡°íšŒ
        /// </summary>
        public int GetGoldValue(TreasureGrade grade)
        {
            var info = GetGradeInfo(grade);
            return info?.goldValue ?? 0;
        }

        /// <summary>
        /// TreasureGrade enumìœ¼ë¡œ ì´ë¦„ ì¡°íšŒ
        /// </summary>
        public string GetName(TreasureGrade grade)
        {
            var info = GetGradeInfo(grade);
            return info?.name ?? "ì•Œ ìˆ˜ ì—†ìŒ";
        }

        /// <summary>
        /// TreasureGrade enumìœ¼ë¡œ ìƒ‰ìƒ ì¡°íšŒ
        /// </summary>
        public string GetColor(TreasureGrade grade)
        {
            var info = GetGradeInfo(grade);
            return info?.color ?? "#FFFFFF";
        }

        /// <summary>
        /// ë‹¤ìŒ ë“±ê¸‰ ì¡°íšŒ (ìµœëŒ€ë©´ null)
        /// </summary>
        public TreasureGrade? GetNextGrade(TreasureGrade grade)
        {
            if (grade == TreasureGrade.Diamond) return null;
            return (TreasureGrade)((int)grade + 1);
        }
    }

    /// <summary>
    /// ë‹¨ì¼ ìž¬í™” ë“±ê¸‰ ì •ë³´
    /// </summary>
    [Serializable]
    public class TreasureGradeInfo
    {
        public int level;
        public string id;
        public int enumValue;
        public string name;
        public int goldValue;
        public string color;
    }

    // =========================================================================
    // Jobs.json ë§¤í•‘
    // =========================================================================

    /// <summary>
    /// Jobs.json ë£¨íŠ¸ í´ëž˜ìŠ¤
    /// </summary>
    [Serializable]
    public class JobsData
    {
        public string version;
        public JobInfo[] jobs;

        /// <summary>
        /// Job enumìœ¼ë¡œ ì§ì—… ì •ë³´ ì¡°íšŒ
        /// </summary>
        public JobInfo GetJobInfo(Job job)
        {
            int enumVal = (int)job;
            if (jobs != null)
            {
                foreach (var j in jobs)
                {
                    if (j.enumValue == enumVal)
                        return j;
                }
            }
            return null;
        }

        /// <summary>
        /// Job enumìœ¼ë¡œ ê¸°ë³¸ ì „íˆ¬ë ¥ ì¡°íšŒ
        /// </summary>
        public int GetBaseCombatPower(Job job)
        {
            var info = GetJobInfo(job);
            return info?.baseCombatPower ?? 0;
        }
    }

    /// <summary>
    /// ë‹¨ì¼ ì§ì—… ì •ë³´ (í™•ìž¥)
    /// </summary>
    [Serializable]
    public class JobInfo
    {
        public string id;
        public int enumValue;
        public string displayName;
        public string description;
        public int baseCombatPower;
        public JobCardPools cardPools;

        /// <summary>
        /// JobDefinitionìœ¼ë¡œ ë³€í™˜ (ê¸°ì¡´ ì‹œìŠ¤í…œ í˜¸í™˜)
        /// </summary>
        public JobDefinition ToJobDefinition()
        {
            return new JobDefinition
            {
                job = (Job)enumValue,
                displayName = displayName,
                description = description,
                cardPoolBasic = cardPools?.basic != null ? new List<string>(cardPools.basic) : new List<string>(),
                cardPoolAdvanced = cardPools?.advanced != null ? new List<string>(cardPools.advanced) : new List<string>(),
                cardPoolRare = cardPools?.rare != null ? new List<string>(cardPools.rare) : new List<string>()
            };
        }
    }

    /// <summary>
    /// ì§ì—…ë³„ ì¹´ë“œí’€
    /// </summary>
    [Serializable]
    public class JobCardPools
    {
        public string[] basic;
        public string[] advanced;
        public string[] rare;
    }
}