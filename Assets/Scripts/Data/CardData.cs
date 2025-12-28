// =============================================================================
// CardData.cs
// 카드 관련 데이터 클래스 정의
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeckBuildingEconomy.Data
{
    // =========================================================================
    // 카드 정의 (데이터 - JSON에서 로드)
    // =========================================================================

    /// <summary>
    /// 카드 데이터 정의
    /// JSON에서 로드되는 카드의 기본 정보
    /// </summary>
    [Serializable]
    public class CardData
    {
        // 기본 정보
        public string id;               // 고유 ID (예: "copper", "labor", "explore")
        public string cardName;         // 표시 이름 (예: "동화", "노동", "탐색")
        public CardType cardType;       // 카드 타입
        public string description;      // 카드 설명 텍스트

        // 재화 카드용
        public TreasureGrade treasureGrade; // 재화 등급 (Treasure 타입만)
        public int goldValue;               // 골드 값 (1, 2, 4, 7, 12, 20, 35)

        // 액션 카드용
        public CardRarity rarity;           // 희귀도 (Action 타입만)
        public Job[] jobPools;              // 속한 직업풀 (복수 가능)
        public CardEffect[] effects;        // 카드 효과 목록

        // 오염 카드용
        public PollutionType pollutionType; // 오염 종류 (Pollution 타입만)
    }

    /// <summary>
    /// 카드 효과 정의
    /// 하나의 카드는 여러 효과를 가질 수 있음
    /// </summary>
    [Serializable]
    public class CardEffect
    {
        public EffectType effectType;   // 효과 종류
        public int value;               // 효과 수치 (예: +3 카드의 3)
        public float floatValue;        // 소수점 수치 (배수 등)
        public TargetType targetType;   // 대상 선택 방식
        public int maxTargets;          // 최대 대상 수 (0 = 무제한)
        
        // 특수 효과용 추가 파라미터
        public TreasureGrade createGrade;   // 생성할 재화 등급 (CreateTempTreasure용)
        public int duration;                // 지속 턴 수 (PersistentGold 등)
        public int successChance;           // 성공 확률 % (Gamble용)
        public int successValue;            // 성공 시 값
        public int failValue;               // 실패 시 값
    }

    // =========================================================================
    // 카드 인스턴스 (런타임 - 게임 중 실제 카드)
    // =========================================================================

    /// <summary>
    /// 카드 인스턴스
    /// 게임 중 실제로 존재하는 카드 한 장
    /// 같은 CardData를 참조하는 여러 인스턴스가 있을 수 있음
    /// </summary>
    [Serializable]
    public class CardInstance
    {
        public string instanceId;       // 고유 인스턴스 ID (GUID)
        public string cardDataId;       // 참조하는 CardData의 id
        public string ownerUnitId;      // 종속 유닛 ID (null이면 무소속)
        
        // 상태
        public bool isTemporary;        // 임시 카드 여부 (턴 종료 시 소멸)
        
        // 이번 턴 부스트 상태
        public bool isBoostedThisTurn;      // 이번 턴 등급 상승 여부
        public TreasureGrade? boostedGrade; // 부스트된 등급 (null이면 기본)

        /// <summary>
        /// 새 카드 인스턴스 생성
        /// </summary>
        public static CardInstance Create(string cardDataId, string ownerUnitId = null, bool isTemporary = false)
        {
            return new CardInstance
            {
                instanceId = Guid.NewGuid().ToString(),
                cardDataId = cardDataId,
                ownerUnitId = ownerUnitId,
                isTemporary = isTemporary,
                isBoostedThisTurn = false,
                boostedGrade = null
            };
        }
    }

    // =========================================================================
    // JSON 직렬화용 래퍼 클래스
    // =========================================================================

    /// <summary>
    /// 카드 데이터 목록 (JSON 루트)
    /// Unity JsonUtility는 배열 직접 파싱 불가 → 래퍼 필요
    /// </summary>
    [Serializable]
    public class CardDataList
    {
        public List<CardData> cards;
    }

    // =========================================================================
    // 재화 등급별 골드 값 매핑
    // =========================================================================

    /// <summary>
    /// 재화 등급 유틸리티
    /// </summary>
    public static class TreasureGradeUtil
    {
        /// <summary>
        /// 등급에 해당하는 골드 값 반환
        /// </summary>
        public static int GetGoldValue(TreasureGrade grade)
        {
            switch (grade)
            {
                case TreasureGrade.Copper:   return 1;
                case TreasureGrade.Silver:   return 2;
                case TreasureGrade.Gold:     return 4;
                case TreasureGrade.Emerald:  return 7;
                case TreasureGrade.Sapphire: return 12;
                case TreasureGrade.Ruby:     return 20;
                case TreasureGrade.Diamond:  return 35;
                default: return 0;
            }
        }

        /// <summary>
        /// 등급에 해당하는 한글 이름 반환
        /// </summary>
        public static string GetName(TreasureGrade grade)
        {
            switch (grade)
            {
                case TreasureGrade.Copper:   return "동화";
                case TreasureGrade.Silver:   return "은화";
                case TreasureGrade.Gold:     return "금화";
                case TreasureGrade.Emerald:  return "에메랄드";
                case TreasureGrade.Sapphire: return "사파이어";
                case TreasureGrade.Ruby:     return "루비";
                case TreasureGrade.Diamond:  return "다이아몬드";
                default: return "알 수 없음";
            }
        }

        /// <summary>
        /// 다음 등급 반환 (최대면 null)
        /// </summary>
        public static TreasureGrade? GetNextGrade(TreasureGrade grade)
        {
            if (grade == TreasureGrade.Diamond) return null;
            return (TreasureGrade)((int)grade + 1);
        }
    }
}
