// =============================================================================
// Enums.cs
// 게임에서 사용하는 모든 열거형(Enum) 정의
// =============================================================================
// [R1 수정] LandType, ScheduledEventType 확장, GameEndState 확장
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
        Treasure = 0,   // 재화 카드 - 골드 생산, 액션 소모 안 함
        Action = 1,     // 액션 카드 - 효과 발동, 액션 1 소모
        Pollution = 2   // 오염 카드 - 패널티, 플레이 불가
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
        Basic = 0,      // 기본 (60% 등장)
        Advanced = 1,   // 고급 (35% 등장)
        Rare = 2,       // 희귀 (5% 등장)
        SuperRare = 3,  // 초희귀 (이벤트 전용)
        Legendary = 4   // 전설 (이벤트 전용)
    }

    /// <summary>
    /// 오염 카드 종류
    /// </summary>
    public enum PollutionType
    {
        Debt = 0,       // 부채 - 손패만 차지
        Curse = 1,      // 저주 - 턴 종료 시 -2 골드
        Disease = 2,    // 질병 - 해당 유닛 강화 불가
        Damage = 3      // 파손 - 이번 턴 드로우 -1
    }

    // =========================================================================
    // 유닛 관련 Enum
    // =========================================================================

    /// <summary>
    /// 직업 종류
    /// </summary>
    public enum Job
    {
        None = -1,      // 없음 (유년기)
        Pawn = 0,       // 폰 - 경제/생산 특화
        Knight = 1,     // 나이트 - 드로우/도박 특화
        Bishop = 2,     // 비숍 - 덱 관리/정화 특화
        Rook = 3,       // 룩 - 방어/안정 특화
        Queen = 4       // 퀸 - 만능/복합
    }

    /// <summary>
    /// 성장 단계
    /// </summary>
    public enum GrowthStage
    {
        Child = 0,      // 유년 - 3턴, 전직 불가
        Young = 1,      // 청년 - 10턴, 전직 가능
        Middle = 2,     // 중년 - 10턴, 전직 가능
        Old = 3         // 노년 - 5+a턴, 전직 불가, 사망 확률
    }

    // =========================================================================
    // 집/사유지 관련 Enum (R1 추가)
    // =========================================================================

    /// <summary>
    /// 사유지 타입
    /// </summary>
    public enum LandType
    {
        Empty = 0,      // 빈 땅 (Lv.0)

        // 영지 루트
        Farm = 1,       // 농장 (Lv.1)
        // -> 대농장/목장 (Lv.2) -> 곡창 (Lv.3)

        // 거주 루트
        Village = 2,    // 촌락 (Lv.1)
        // -> 마을/장원 (Lv.2) -> 읍 (Lv.3)

        // 군사 루트
        Watchtower = 3, // 망루 (Lv.1)
        // -> 요새/병영 (Lv.2) -> 성 (Lv.3)

        // 신앙 루트
        Chapel = 4,     // 예배당 (Lv.1)
        // -> 교회/수도원 (Lv.2) -> 대성당 (Lv.3)

        // 학문 루트
        Study = 5,      // 서재 (Lv.1)
        // -> 도서관/서고 (Lv.2) -> 대학 (Lv.3)

        // 상업 루트
        Shop = 6        // 상점 (Lv.1)
        // -> 시장/창고 (Lv.2) -> 대시장 (Lv.3)
    }

    // =========================================================================
    // 게임 진행 관련 Enum
    // =========================================================================

    /// <summary>
    /// 게임 페이즈
    /// </summary>
    public enum GamePhase
    {
        TurnStart = 0,      // 턴 시작 처리 (성장, 이벤트)
        Event = 1,          // 이벤트 처리 중
        Deck = 2,           // 덱 페이즈 (카드 플레이)
        Purchase = 3,       // 구매 페이즈 (전직)
        TurnEnd = 4,        // 턴 종료 처리
        GameOver = 5        // 게임 종료
    }

    /// <summary>
    /// 게임 종료 상태
    /// [R1 확장] 전투 패배, 전원 사망 추가
    /// </summary>
    public enum GameEndState
    {
        None = 0,               // 진행 중
        Victory = 1,            // 승리 (60턴 최종 전투 승리)
        DefeatBankrupt = 2,     // 패배 (골드 < 0)
        DefeatValidation = 3,   // 패배 (검증 턴 실패)
        DefeatBattle = 4,       // 패배 (약탈/최종 전투 패배)
        DefeatAllUnitsDead = 5  // 패배 (유닛 전멸)
    }

    // =========================================================================
    // 이벤트 관련 Enum
    // =========================================================================

    /// <summary>
    /// 이벤트 종류
    /// </summary>
    public enum EventType
    {
        Scheduled = 0,  // 예정 이벤트 (출생, 사망, 검증)
        Positive = 1,   // 긍정 이벤트
        Negative = 2,   // 부정 이벤트
        Choice = 3      // 선택 이벤트
    }

    /// <summary>
    /// 예정 이벤트 종류
    /// [R1 확장] 약탈, 최종 전투 추가
    /// </summary>
    public enum ScheduledEventType
    {
        Birth = 0,          // 유닛 출생 (삭제됨 - 교배 시스템으로 대체)
        Validation = 1,     // 검증 턴 (10, 20, 30, 40, 50턴)
        ForcedDeath = 2,    // 강제 사망 (삭제됨)
        Raid = 3,           // 약탈 (8, 16, 24, 32, 40, 48, 56턴)
        FinalBattle = 4     // 최종 전투 (60턴)
    }

    // =========================================================================
    // 카드 효과 관련 Enum
    // =========================================================================

    /// <summary>
    /// 카드 효과 타입
    /// JSON에서 사용할 때 숫자로 참조
    /// </summary>
    public enum EffectType
    {
        // === 기본 효과 ===
        DrawCard = 0,           // +N 카드 드로우
        AddAction = 1,          // +N 액션
        AddGold = 2,            // +N 골드 (즉시)

        // === 재화 관련 ===
        CreateTempTreasure = 3, // 임시 재화 카드 생성
        BoostTreasure = 4,      // 재화 등급 임시 상승
        PermanentUpgrade = 5,   // 재화 영구 업그레이드
        SettleCard = 6,         // 카드 정산 (골드 확정 후 버림)

        // === 골드 배수 ===
        GoldMultiplier = 7,     // 골드 획득량 배수 (이번 턴)
        GoldBonus = 8,          // 골드 획득량 고정 보너스 (이번 턴)

        // === 덱 관리 ===
        DestroyCard = 9,        // 카드 소멸
        DestroyPollution = 10,  // 오염 카드 소멸
        MoveToDeckBottom = 11,  // 카드를 덱 맨 아래로

        // === 도박/랜덤 ===
        Gamble = 12,            // 확률 기반 골드 획득/손실
        RevealAndGain = 13,     // 덱에서 공개 후 조건부 획득

        // === 지속 효과 ===
        DelayedGold = 14,       // N턴 후 골드 획득
        PersistentGold = 15,    // N턴간 매 턴 골드 획득
        PersistentMaintenance = 16, // N턴간 유지비 증가

        // === 특수 ===
        DrawUntil = 17,         // 손패가 N장 될 때까지 드로우
        IgnorePollution = 18,   // 이번 턴 오염 카드 효과 무시

        // === 덱 조작 ===
        ShuffleDeck = 23,       // 덱 셔플

        // === 이벤트 전용 효과 (E8 추가) ===

        // 유닛 관련
        GainUnit = 50,              // 유닛 획득 (떠돌이 기사)
        RemoveUnit = 51,            // 유닛 제거/희생 (신비한 제단)
        FreePromotion = 52,         // 무료 전직 (영감)
        AddPromotionLevel = 53,     // 전직 레벨 추가 (신비한 제단 보상)

        // 카드 관련 (덱 전체 대상)
        AddCardToDeck = 54,         // 덱에 카드 추가 (숨겨진 보물)
        RemoveCardFromDeck = 55,    // 덱에서 카드 제거 (정화의 바람)
        UpgradeCardInDeck = 56,     // 덱 카드 업그레이드 (장인의 축복)

        // 골드 관련
        SpendGoldPercent = 57,      // 골드 비율 소모 (도적 습격)

        // 턴 지속 효과
        PromotionDiscount = 58,     // 전직 할인 (행상인 방문)
        MaintenanceModifier = 59    // 유지비 수정 (흉년)
    }

    /// <summary>
    /// 효과 대상 타입
    /// </summary>
    public enum TargetType
    {
        None = 0,               // 대상 없음 (자동 적용)
        Self = 1,               // 자기 자신 (카드)
        HandCard = 2,           // 손패의 아무 카드
        HandTreasure = 3,       // 손패의 재화 카드
        HandPollution = 4,      // 손패의 오염 카드
        HandAction = 5,         // 손패의 액션 카드
        AllHandTreasure = 6,    // 손패의 모든 재화 카드
        AllHandPollution = 7,   // 손패의 모든 오염 카드
        DeckTop = 8,            // 덱 맨 위
        Random = 9              // 랜덤 대상
    }

    // =========================================================================
    // 이벤트 조건 관련 Enum
    // =========================================================================

    /// <summary>
    /// 이벤트 발생 조건 타입
    /// </summary>
    public enum EventConditionType
    {
        None = 0,                   // 조건 없음
        GoldGreaterThan = 1,        // 골드 > N
        GoldLessThan = 2,           // 골드 < N
        HasPollutionCard = 3,       // 오염 카드 보유
        HasCopperCard = 4,          // 동화 보유
        HasYoungOrMiddleUnit = 5,   // 청년/중년 유닛 존재
        HasMultipleUnits = 6,       // 유닛 2명 이상
        UnitExists = 7              // 유닛 1명 이상
    }

    // =========================================================================
    // 효과 조건 시스템 관련 Enum (E1 추가)
    // =========================================================================

    /// <summary>
    /// 효과 발동 조건 타입
    /// 카드/이벤트 효과의 발동 조건을 정의
    /// </summary>
    public enum ConditionType
    {
        None = 0,                   // 조건 없음 (항상 발동)

        // === 골드 조건 ===
        GoldAbove = 1,              // 골드 >= N
        GoldBelow = 2,              // 골드 < N

        // === 손패 조건 ===
        HandHasTreasure = 10,       // 손패에 재화 카드 있음
        HandHasPollution = 11,      // 손패에 오염 카드 있음
        HandHasAction = 12,         // 손패에 액션 카드 있음
        HandCountAbove = 13,        // 손패 장수 >= N
        HandCountBelow = 14,        // 손패 장수 < N

        // === 덱 조건 ===
        DeckTopIsTreasure = 20,     // 덱 탑이 재화 카드
        DeckTopIsPollution = 21,    // 덱 탑이 오염 카드
        DeckTopIsAction = 22,       // 덱 탑이 액션 카드
        DeckNotEmpty = 23,          // 덱이 비어있지 않음

        // === 유닛 조건 ===
        HasUnit = 30,               // 유닛 1명 이상
        HasMultipleUnits = 31,      // 유닛 2명 이상
        HasPromotableUnit = 32,     // 전직 가능 유닛 있음

        // === 이전 효과 조건 ===
        PreviousEffectSucceeded = 40,   // 이전 효과 성공
        PreviousCountAbove = 41,        // 이전 효과 처리 수 >= N

        // === 이벤트 전용 조건 (E8 추가) ===
        HasCopperInDeck = 60,           // 덱에 동화 있음 (장인의 축복)
        HasPollutionInDeck = 61,        // 덱에 오염 카드 있음 (정화의 바람)
        HasSpecificCardInDeck = 62,     // 덱에 특정 카드 있음
    }

    /// <summary>
    /// 조건 비교 연산자
    /// ConditionType과 함께 사용하여 조건 평가
    /// </summary>
    public enum ComparisonType
    {
        Equal = 0,          // ==
        NotEqual = 1,       // !=
        GreaterThan = 2,    // >
        LessThan = 3,       // <
        GreaterOrEqual = 4, // >=
        LessOrEqual = 5     // <=
    }

    /// <summary>
    /// 동적 값 소스 타입
    /// 효과의 값을 고정값 대신 게임 상태에서 계산
    /// </summary>
    public enum ValueSourceType
    {
        Fixed = 0,              // 고정값 (기본)

        // === 이전 효과 참조 ===
        PreviousCount = 10,     // 이전 효과에서 처리한 수 (정산 장수 등)
        PreviousValue = 11,     // 이전 효과의 결과값

        // === 현재 상태 참조 ===
        CurrentGold = 20,       // 현재 보유 골드
        CurrentGoldPercent = 21,// 현재 골드의 N%
        HandCount = 22,         // 현재 손패 장수
        DeckCount = 23,         // 현재 덱 장수
        UnitCount = 24,         // 현재 유닛 수

        // === 덱/카드 참조 ===
        DeckTopGoldValue = 30,  // 덱 탑 카드의 골드 값
        TargetGoldValue = 31,   // 선택한 대상의 골드 값

        // === 랜덤 ===
        RandomRange = 40,       // min~max 사이 랜덤
        RandomDice = 41         // 1~N 주사위 (value = N)
    }

    // =========================================================================
    // 시작 설정 관련 Enum (E4 추가)
    // =========================================================================

    /// <summary>
    /// 시작 이벤트 모드
    /// [E4] Default.json 제거 후 Inspector에서 설정
    /// </summary>
    public enum StartEventMode
    {
        /// <summary>기본: EventSystem 랜덤 이벤트</summary>
        Default = 0,

        /// <summary>커스텀: Inspector에서 지정한 턴에 지정한 이벤트 발생</summary>
        Custom = 1
    }

    /// <summary>
    /// 시작 덱 모드
    /// [E4] Default.json 제거 후 Inspector에서 설정
    /// </summary>
    public enum StartDeckMode
    {
        /// <summary>기본: 동화 7장 + JobSO.startingCards 기반 유닛 카드</summary>
        Default = 0,

        /// <summary>커스텀: Inspector에서 직접 지정한 카드/유닛으로 시작</summary>
        Custom = 1
    }
}