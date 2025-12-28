// =============================================================================
// Enums.cs
// 게임에서 사용하는 모든 열거형(Enum) 정의
// =============================================================================

namespace DeckBuildingEconomy.Data
{
    // =========================================================================
    // 카드 관련 Enum
    // =========================================================================

    /// <summary>
    /// 카드 타입 - 재화, 액션, 오염 세 종류
    /// </summary>
    public enum CardType
    {
        Treasure,   // 재화 카드 - 골드 생산, 액션 소모 안 함
        Action,     // 액션 카드 - 효과 발동, 액션 1 소모
        Pollution   // 오염 카드 - 패널티, 플레이 불가
    }

    /// <summary>
    /// 재화 카드 등급 (1~7)
    /// 값 자체가 등급 번호를 의미
    /// </summary>
    public enum TreasureGrade
    {
        Copper = 1,     // 동화 - 1골드
        Silver = 2,     // 은화 - 2골드
        Gold = 3,       // 금화 - 4골드
        Emerald = 4,    // 에메랄드 - 7골드
        Sapphire = 5,   // 사파이어 - 12골드
        Ruby = 6,       // 루비 - 20골드
        Diamond = 7     // 다이아몬드 - 35골드
    }

    /// <summary>
    /// 액션 카드 등급
    /// </summary>
    public enum CardRarity
    {
        Basic,      // 기본 (60% 등장)
        Advanced,   // 고급 (35% 등장)
        Rare,       // 희귀 (5% 등장)
        SuperRare,  // 초희귀 (이벤트 전용)
        Legendary   // 전설 (이벤트 전용)
    }

    /// <summary>
    /// 오염 카드 종류
    /// </summary>
    public enum PollutionType
    {
        Debt,       // 부채 - 손패만 차지
        Curse,      // 저주 - 턴 종료 시 -2 골드
        Disease,    // 질병 - 해당 유닛 강화 불가
        Damage      // 파손 - 이번 턴 드로우 -1
    }

    // =========================================================================
    // 유닛 관련 Enum
    // =========================================================================

    /// <summary>
    /// 직업 종류
    /// </summary>
    public enum Job
    {
        Pawn,       // 폰 - 경제/생산 특화
        Knight,     // 나이트 - 드로우/도박 특화
        Bishop,     // 비숍 - 덱 관리/정화 특화
        Rook,       // 룩 - 방어/안정 특화
        Queen       // 퀸 - 만능/복합
    }

    /// <summary>
    /// 성장 단계
    /// </summary>
    public enum GrowthStage
    {
        Child,      // 유년 - 3턴, 강화 불가
        Young,      // 청년 - 10턴, 강화 가능
        Middle,     // 중년 - 10턴, 강화 가능
        Old         // 노년 - 5+α턴, 강화 불가, 사망 확률
    }

    // =========================================================================
    // 게임 진행 관련 Enum
    // =========================================================================

    /// <summary>
    /// 게임 페이즈
    /// </summary>
    public enum GamePhase
    {
        TurnStart,      // 턴 시작 처리 (성장, 이벤트)
        Event,          // 이벤트 처리 중
        Deck,           // 덱 페이즈 (카드 플레이)
        Purchase,       // 구매 페이즈 (강화)
        TurnEnd,        // 턴 종료 처리
        GameOver        // 게임 종료
    }

    /// <summary>
    /// 게임 종료 상태
    /// </summary>
    public enum GameEndState
    {
        None,           // 진행 중
        Victory,        // 승리 (45턴 생존)
        DefeatBankrupt, // 패배 (골드 < 0)
        DefeatValidation // 패배 (검증 턴 실패)
    }

    // =========================================================================
    // 이벤트 관련 Enum
    // =========================================================================

    /// <summary>
    /// 이벤트 종류
    /// </summary>
    public enum EventType
    {
        Scheduled,  // 예정 이벤트 (출생, 사망, 검증)
        Positive,   // 긍정 이벤트
        Negative,   // 부정 이벤트
        Choice      // 선택 이벤트
    }

    /// <summary>
    /// 예정 이벤트 종류
    /// </summary>
    public enum ScheduledEventType
    {
        Birth,          // 유닛 출생 (6, 12, 18, 24, 30, 36, 42턴)
        Validation,     // 검증 턴 (11, 23, 35턴)
        ForcedDeath     // 강제 사망 (12, 24, 36턴)
    }

    // =========================================================================
    // 카드 효과 관련 Enum
    // =========================================================================

    /// <summary>
    /// 카드 효과 타입
    /// </summary>
    public enum EffectType
    {
        // 기본 효과
        DrawCard,           // +N 카드 드로우
        AddAction,          // +N 액션
        AddGold,            // +N 골드 (즉시)

        // 재화 관련
        CreateTempTreasure, // 임시 재화 카드 생성
        BoostTreasure,      // 재화 등급 임시 상승
        PermanentUpgrade,   // 재화 영구 업그레이드
        SettleCard,         // 카드 정산 (골드 확정 후 버림)

        // 골드 배수
        GoldMultiplier,     // 골드 획득량 배수 (이번 턴)
        GoldBonus,          // 골드 획득량 고정 보너스 (이번 턴)

        // 덱 관리
        DestroyCard,        // 카드 소멸
        DestroyPollution,   // 오염 카드 소멸
        MoveToDeckBottom,   // 카드를 덱 맨 아래로

        // 도박/랜덤
        Gamble,             // 확률 기반 골드 획득/손실
        RevealAndGain,      // 덱에서 공개 후 조건부 획득

        // 지속 효과
        DelayedGold,        // N턴 후 골드 획득
        PersistentGold,     // N턴간 매 턴 골드 획득
        PersistentMaintenance, // N턴간 유지비 증가

        // 특수
        DrawUntil,          // 손패가 N장 될 때까지 드로우
        IgnorePollution     // 이번 턴 오염 카드 효과 무시
    }

    /// <summary>
    /// 효과 대상 타입
    /// </summary>
    public enum TargetType
    {
        None,               // 대상 없음 (자동 적용)
        Self,               // 자기 자신 (카드)
        HandCard,           // 손패의 아무 카드
        HandTreasure,       // 손패의 재화 카드
        HandPollution,      // 손패의 오염 카드
        HandAction,         // 손패의 액션 카드
        AllHandTreasure,    // 손패의 모든 재화 카드
        AllHandPollution,   // 손패의 모든 오염 카드
        DeckTop,            // 덱 맨 위
        Random              // 랜덤 대상
    }

    // =========================================================================
    // 이벤트 조건 관련 Enum
    // =========================================================================

    /// <summary>
    /// 이벤트 발생 조건 타입
    /// </summary>
    public enum EventConditionType
    {
        None,                   // 조건 없음
        GoldGreaterThan,        // 골드 > N
        GoldLessThan,           // 골드 < N
        HasPollutionCard,       // 오염 카드 보유
        HasCopperCard,          // 동화 보유
        HasYoungOrMiddleUnit,   // 청년/중년 유닛 존재
        HasMultipleUnits,       // 유닛 2명 이상
        UnitExists              // 유닛 1명 이상
    }
}
