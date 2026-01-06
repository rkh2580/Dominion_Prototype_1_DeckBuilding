// =============================================================================
// CardData.cs
// 카드 관련 데이터 클래스 정의
// =============================================================================
// [E1 수정] Effect, ConditionalEffect 클래스 추가
// [Phase 2] TreasureGradeUtil JSON 기반 리팩토링
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBuildingEconomy.Data
{
    // =========================================================================
    // 카드 정의 (데이터 - JSON에서 로드)
    // =========================================================================

    /// <summary>
    /// 카드 데이터 정의
    /// JSON에서 로드되는 카드의 기본 정보
    /// [리팩토링] effects 필드를 ConditionalEffect[]로 통합
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

        // ★ 통합 효과 시스템 (리팩토링)
        // 모든 카드가 ConditionalEffect[] 사용 (조건 없으면 빈 conditions 배열)
        public ConditionalEffect[] effects;

        // 오염 카드용
        public PollutionType pollutionType; // 오염 종류 (Pollution 타입만)

        // =====================================================================
        // 하위 호환 헬퍼 (Step 4에서 제거 예정)
        // =====================================================================

        /// <summary>
        /// 레거시 CardEffect[] 형태로 반환 (기존 EffectSystem 호환용)
        /// </summary>
        [Obsolete("Use effects directly. Will be removed in Step 4.")]
        public CardEffect[] GetLegacyEffects()
        {
            if (effects == null || effects.Length == 0)
                return new CardEffect[0];

            var result = new System.Collections.Generic.List<CardEffect>();
            foreach (var group in effects)
            {
                if (group.effects == null) continue;
                foreach (var eff in group.effects)
                {
                    result.Add(new CardEffect
                    {
                        effectType = eff.type,
                        value = eff.value,
                        floatValue = eff.dynamicValue?.multiplier ?? 0f,
                        targetType = eff.target,
                        maxTargets = eff.maxTargets,
                        createGrade = eff.createGrade,
                        duration = eff.duration,
                        successChance = eff.successChance,
                        successValue = eff.successValueInt,
                        failValue = eff.failValueInt
                    });
                }
            }
            return result.ToArray();
        }
    }

    /// <summary>
    /// 카드 효과 정의 (기존 - 하위 호환용)
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
    // 신규 효과 시스템 클래스 (E1 추가, 리팩토링 수정)
    // =========================================================================

    /// <summary>
    /// 단일 효과 정의 (CardEffect 확장 버전)
    /// [리팩토링] JSON 필드명에 맞게 수정
    /// - value: int (고정값)
    /// - dynamicValue: ValueSource (동적값, 선택)
    /// - target: TargetType (targetType에서 변경)
    /// </summary>
    [Serializable]
    public class Effect
    {
        /// <summary>효과 타입</summary>
        public EffectType type;

        /// <summary>효과 값 (고정값)</summary>
        public int value;

        /// <summary>동적 값 소스 (선택, null이면 value 사용)</summary>
        public ValueSource dynamicValue;

        /// <summary>대상 타입</summary>
        public TargetType target;

        /// <summary>최대 대상 수 (0 = 무제한)</summary>
        public int maxTargets;

        // === 특수 효과용 파라미터 ===

        /// <summary>생성할 재화 등급 (CreateTempTreasure용)</summary>
        public TreasureGrade createGrade;

        /// <summary>지속 턴 수</summary>
        public int duration;

        // === Gamble용 파라미터 ===

        /// <summary>성공 확률 %</summary>
        public int successChance;

        /// <summary>성공 시 값 (고정)</summary>
        public int successValueInt;

        /// <summary>성공 시 값 (동적, 선택)</summary>
        public ValueSource successValue;

        /// <summary>실패 시 값 (고정)</summary>
        public int failValueInt;

        /// <summary>실패 시 값 (동적, 선택)</summary>
        public ValueSource failValue;

        // === 카드 획득용 파라미터 ===

        /// <summary>획득할 카드 ID (GainCard용)</summary>
        public string cardId;

        /// <summary>획득할 카드 희귀도 (GainRandomCard용)</summary>
        public CardRarity cardRarity;

        /// <summary>획득할 카드 직업풀 (GainRandomCard용)</summary>
        public Job cardJobPool;

        // =========================================================================
        // 헬퍼 메서드
        // =========================================================================

        /// <summary>
        /// 실제 효과 값 계산 (dynamicValue가 있으면 무시하고 외부에서 계산)
        /// </summary>
        public int GetValue() => value;

        /// <summary>
        /// 동적 값 사용 여부
        /// </summary>
        public bool HasDynamicValue => dynamicValue != null && dynamicValue.source != ValueSourceType.Fixed;

        /// <summary>
        /// 간단한 효과 생성 (고정값)
        /// </summary>
        public static Effect Simple(EffectType type, int value, TargetType target = TargetType.None)
        {
            return new Effect
            {
                type = type,
                value = value,
                target = target,
                maxTargets = 1
            };
        }

        /// <summary>
        /// 드로우 효과 생성
        /// </summary>
        public static Effect Draw(int count) => Simple(EffectType.DrawCard, count);

        /// <summary>
        /// 골드 획득 효과 생성
        /// </summary>
        public static Effect Gold(int amount) => Simple(EffectType.AddGold, amount);

        /// <summary>
        /// 액션 추가 효과 생성
        /// </summary>
        public static Effect Action(int count) => Simple(EffectType.AddAction, count);

        // =========================================================================
        // 하위 호환 (Step 4에서 제거)
        // =========================================================================

        /// <summary>targetType 별칭 (하위 호환)</summary>
        [Obsolete("Use target instead")]
        public TargetType targetType
        {
            get => target;
            set => target = value;
        }
    }

    /// <summary>
    /// 조건부 효과 묶음
    /// 조건을 만족하면 effects 실행, 아니면 elseEffects 실행
    /// </summary>
    [Serializable]
    public class ConditionalEffect
    {
        /// <summary>발동 조건 (AND 결합)</summary>
        public EffectCondition[] conditions;

        /// <summary>조건 만족 시 실행할 효과</summary>
        public Effect[] effects;

        /// <summary>조건 불만족 시 실행할 효과 (선택, null 가능)</summary>
        public Effect[] elseEffects;

        // =========================================================================
        // 헬퍼 메서드
        // =========================================================================

        /// <summary>
        /// 조건 없는 효과 (항상 발동) 생성
        /// </summary>
        public static ConditionalEffect Always(params Effect[] effects)
        {
            return new ConditionalEffect
            {
                conditions = new[] { EffectCondition.None },
                effects = effects,
                elseEffects = null
            };
        }

        /// <summary>
        /// 단일 효과를 조건 없이 감싸기
        /// </summary>
        public static ConditionalEffect Simple(EffectType type, int value, TargetType target = TargetType.None)
        {
            return Always(Effect.Simple(type, value, target));
        }
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
    // [Phase 2] JSON 기반으로 리팩토링
    // =========================================================================

    /// <summary>
    /// 재화 등급 유틸리티
    /// [Phase 2] JSON에서 로드된 데이터 사용, 폴백으로 하드코딩 값 유지
    /// </summary>
    public static class TreasureGradeUtil
    {
        private static TreasureGradesData _data;
        private static TreasureGradesSO _so;  // [E1] SO 참조 추가
        private static bool _initialized = false;

        /// <summary>
        /// JSON 데이터로 초기화
        /// DataLoader.Awake()에서 호출됨
        /// </summary>
        public static void Initialize(TreasureGradesData data)
        {
            _data = data;
            _initialized = true;
            Debug.Log($"[TreasureGradeUtil] 초기화 완료 (v{data?.version ?? "null"})");
        }

        /// <summary>
        /// SO에서 직접 초기화 [E1 추가]
        /// DataLoader에서 SO 로드 성공 시 호출
        /// </summary>
        public static void InitializeFromSO(TreasureGradesSO so)
        {
            _so = so;
            _data = so?.ToTreasureGradesData();
            _initialized = true;
            Debug.Log($"[TreasureGradeUtil] SO에서 초기화 완료 (v{_data?.version ?? "null"})");
        }

        /// <summary>
        /// SO 참조 반환 [E1 추가]
        /// </summary>
        public static TreasureGradesSO GetSO() => _so;

        /// <summary>
        /// 초기화 여부 확인
        /// </summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// 등급에 해당하는 골드 값 반환
        /// </summary>
        public static int GetGoldValue(TreasureGrade grade)
        {
            if (_data != null)
            {
                return _data.GetGoldValue(grade);
            }

            // 폴백 값
            switch (grade)
            {
                case TreasureGrade.Copper: return 1;
                case TreasureGrade.Silver: return 2;
                case TreasureGrade.Gold: return 4;
                case TreasureGrade.Emerald: return 7;
                case TreasureGrade.Sapphire: return 12;
                case TreasureGrade.Ruby: return 20;
                case TreasureGrade.Diamond: return 35;
                default: return 0;
            }
        }

        /// <summary>
        /// 등급에 해당하는 한글 이름 반환
        /// </summary>
        public static string GetName(TreasureGrade grade)
        {
            if (_data != null)
            {
                return _data.GetName(grade);
            }

            // 폴백 값
            switch (grade)
            {
                case TreasureGrade.Copper: return "동화";
                case TreasureGrade.Silver: return "은화";
                case TreasureGrade.Gold: return "금화";
                case TreasureGrade.Emerald: return "에메랄드";
                case TreasureGrade.Sapphire: return "사파이어";
                case TreasureGrade.Ruby: return "루비";
                case TreasureGrade.Diamond: return "다이아몬드";
                default: return "알 수 없음";
            }
        }

        /// <summary>
        /// 등급에 해당하는 색상 반환 (Hex 문자열)
        /// [Phase 2 신규]
        /// </summary>
        public static string GetColorHex(TreasureGrade grade)
        {
            if (_data != null)
            {
                return _data.GetColor(grade);
            }

            // 폴백 값
            switch (grade)
            {
                case TreasureGrade.Copper: return "#CD7F32";
                case TreasureGrade.Silver: return "#C0C0C0";
                case TreasureGrade.Gold: return "#FFD700";
                case TreasureGrade.Emerald: return "#50C878";
                case TreasureGrade.Sapphire: return "#0F52BA";
                case TreasureGrade.Ruby: return "#E0115F";
                case TreasureGrade.Diamond: return "#B9F2FF";
                default: return "#FFFFFF";
            }
        }

        /// <summary>
        /// 등급에 해당하는 Color 반환
        /// [Phase 2 신규]
        /// </summary>
        public static Color GetColor(TreasureGrade grade)
        {
            string hex = GetColorHex(grade);
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                return color;
            }
            return Color.white;
        }

        /// <summary>
        /// 다음 등급 반환 (최대면 null)
        /// </summary>
        public static TreasureGrade? GetNextGrade(TreasureGrade grade)
        {
            if (_data != null)
            {
                return _data.GetNextGrade(grade);
            }

            // 폴백 값
            if (grade == TreasureGrade.Diamond) return null;
            return (TreasureGrade)((int)grade + 1);
        }

        /// <summary>
        /// 등급 정보 직접 조회
        /// [Phase 2 신규]
        /// </summary>
        public static TreasureGradeInfo GetGradeInfo(TreasureGrade grade)
        {
            return _data?.GetGradeInfo(grade);
        }
    }
}