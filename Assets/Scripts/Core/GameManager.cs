// =============================================================================
// GameManager.cs
// 게임 전체 흐름 제어 및 초기화
// =============================================================================
// [R2 리팩토링] 2026-01-03
// - 60턴 체제 반영
// - 새 GameEndState 지원 (DefeatBattle, DefeatAllUnitsDead)
// - 시작 설정 변경 (집/사유지 추가 준비)
// - CheckWinLoseCondition에서 승리 조건은 최종 전투로 이동
// [R8 수정] 2026-01-03
// - 초기화 순서 변경: 사유지 먼저 → 집 자동 동기화 → 유닛 배치
// - CreateStartingHouses() 제거 (LandSystem.SyncHouses()가 담당)
// - 검증 통과 보상으로 사유지 획득
// [Phase 2] 2026-01-04
// - StartConfig JSON 지원 추가
// [Phase 3] TestDeckPreset enum 제거됨 - JSON만 사용
// - Inspector에서 JSON 프리셋 선택 가능
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    // =========================================================================
    // 테스트 덱 프리셋 정의 (레거시, 하위 호환)
    // =========================================================================

    /// <summary>
    /// 테스트용 덱 프리셋 (레거시)
    /// [Phase 2] JSON 프리셋 사용 권장
    /// </summary>
    // [Phase 3] TestDeckPreset enum 제거됨
    // 모든 시작 설정은 JSON으로만 관리 (StreamingAssets/Data/*.json)

    /// <summary>
    /// 게임 매니저 (싱글톤)
    /// - 게임 초기화
    /// - 게임 상태 관리
    /// - 시스템 간 조율
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // =====================================================================
        // 디버그 설정
        // =====================================================================

        [Header("디버그 설정")]
        [Tooltip("체크하면 파산/검증 실패로 게임오버 되지 않음")]
        [SerializeField] private bool disableWinLoseCheck = false;

        // =====================================================================
        // 이벤트 설정 (E4: Inspector에서 직접 설정)
        // =====================================================================

        [Header("이벤트 설정")]
        [Tooltip("이벤트 모드 선택\n- Default: 기존 랜덤 이벤트\n- Custom: 예정 이벤트만 발생")]
        [SerializeField] private StartEventMode startEventMode = StartEventMode.Default;

        [Tooltip("커스텀 모드: 예정 이벤트 목록")]
        [SerializeField] private List<ScheduledEventEntry> scheduledEvents = new List<ScheduledEventEntry>();

        // =====================================================================
        // 덱 설정 (E4: Inspector에서 직접 설정)
        // =====================================================================

        [Header("덱 설정")]
        [Tooltip("덱 모드 선택\n- Default: 동화 7장 + JobSO.startingCards\n- Custom: 직접 지정")]
        [SerializeField] private StartDeckMode startDeckMode = StartDeckMode.Default;

        [Tooltip("덱 셔플 여부")]
        [SerializeField] private bool shuffleDeck = true;

        [Tooltip("커스텀 모드: 기본 덱 (무소속 카드)")]
        [SerializeField] private List<CardSO> customBaseDeck = new List<CardSO>();

        [Tooltip("커스텀 모드: 시작 유닛")]
        [SerializeField] private List<StartingUnitEntry> customStartUnits = new List<StartingUnitEntry>();

        // =====================================================================
        // 외부 접근용 프로퍼티 (E4)
        // =====================================================================

        /// <summary>디버그: 승패 체크 비활성화 여부</summary>
        public bool DebugDisableWinLoseCheck => disableWinLoseCheck;

        /// <summary>이벤트 모드 (EventSystem에서 참조)</summary>
        public StartEventMode EventMode => startEventMode;

        /// <summary>예정 이벤트 목록 (EventSystem에서 참조)</summary>
        public IReadOnlyList<ScheduledEventEntry> ScheduledEvents => scheduledEvents;

        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static GameManager Instance { get; private set; }

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>게임 시작됨</summary>
        public static event Action OnGameStarted;

        /// <summary>게임 종료됨 (승패 결과)</summary>
        public static event Action<GameEndState> OnGameEnded;

        /// <summary>게임 상태 변경됨 (디버그/UI용)</summary>
        public static event Action<GameState> OnGameStateChanged;

        // =====================================================================
        // 게임 상태
        // =====================================================================

        /// <summary>현재 게임 상태</summary>
        public GameState State { get; private set; }

        /// <summary>게임 진행 중 여부</summary>
        public bool IsGameRunning => State != null && State.endState == GameEndState.None;

        // =====================================================================
        // [R8 추가] 검증 보상 카운터
        // =====================================================================

        private int validationRewardCount = 0;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            // 싱글톤 설정
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("[GameManager] 초기화 완료");
        }

        private void Start()
        {
            // 자동 게임 시작 (테스트용, 나중에 메뉴에서 호출하도록 변경)
            StartNewGame();
        }

        // =====================================================================
        // 게임 흐름 제어
        // =====================================================================

        /// <summary>
        /// 새 게임 시작
        /// [E4] Inspector 설정 기반으로 변경
        /// </summary>
        public void StartNewGame()
        {
            Debug.Log("[GameManager] 새 게임 시작");

            // 디버그 모드 표시
            if (disableWinLoseCheck)
            {
                Debug.LogWarning("[GameManager] ⚠️ 디버그 모드: 승패 체크 비활성화됨");
            }

            // [E4] 시작 모드 로그
            Debug.Log($"[GameManager] 이벤트 모드: {startEventMode}, 덱 모드: {startDeckMode}");

            // 검증 보상 카운터 초기화
            validationRewardCount = 0;

            // 1. 게임 상태 초기화
            State = GameState.CreateNew();

            // 2. 시작 골드 설정 (GameConfig에서)
            State.gold = GameConfig.StartingGold;
            Debug.Log($"[GameManager] 시작 골드: {State.gold}");

            // 3. 시작 사유지 생성
            CreateStartingLands();

            // 4. 시작 유닛 생성 + 집 배치
            CreateStartingUnits();

            // 5. 시작 덱 생성
            CreateStartingDeck();

            // 6. 유지비 계산
            RecalculateMaintenanceCost();

            // 7. 전투력 계산
            State.RecalculateCombatPower();
            Debug.Log($"[GameManager] 시작 전투력: {State.totalCombatPower}");

            // 8. 이벤트 발생
            OnGameStarted?.Invoke();
            OnGameStateChanged?.Invoke(State);

            // 9. 첫 턴 시작
            TurnManager.Instance?.StartGame();
        }

        // =====================================================================
        // 시작 사유지 생성 (E4)
        // =====================================================================

        /// <summary>
        /// 게임 종료 처리
        /// [R2 확장] 새로운 GameEndState 지원
        /// </summary>
        public void EndGame(GameEndState endState)
        {
            if (State.endState != GameEndState.None)
            {
                Debug.LogWarning("[GameManager] 이미 게임이 종료됨");
                return;
            }

            State.endState = endState;
            State.currentPhase = GamePhase.GameOver;

            string resultText = endState switch
            {
                GameEndState.Victory => "승리! (최종 전투 승리)",
                GameEndState.DefeatBankrupt => "패배 (파산)",
                GameEndState.DefeatValidation => "패배 (검증 실패)",
                GameEndState.DefeatBattle => "패배 (전투 패배)",
                GameEndState.DefeatAllUnitsDead => "패배 (전원 사망)",
                _ => "알 수 없음"
            };

            Debug.Log($"[GameManager] 게임 종료 - {resultText}");
            OnGameEnded?.Invoke(endState);
        }

        /// <summary>
        /// 게임 상태 로드 (저장/로드용)
        /// </summary>
        public void LoadState(GameState loadedState)
        {
            State = loadedState;
            RecalculateMaintenanceCost();
            State.RecalculateCombatPower();
            OnGameStateChanged?.Invoke(State);
            Debug.Log("[GameManager] 게임 상태 로드됨");
        }

        // =====================================================================
        // 초기화 헬퍼
        // =====================================================================

        /// <summary>
        /// 시작 사유지 생성
        /// [E4] GameConfig.StartingLands 사용
        /// </summary>
        private void CreateStartingLands()
        {
            int landCount = GameConfig.StartingLands;

            for (int i = 0; i < landCount; i++)
            {
                LandSystem.Instance?.AcquireLand($"{i + 1}번 영토");
            }

            Debug.Log($"[GameManager] 시작 사유지 {State.lands.Count}개 생성 → 집 {State.houses.Count}개 동기화됨");
        }

        /// <summary>
        /// 시작 유닛 생성
        /// [E4] 덱 모드에 따라 분기
        /// </summary>
        private void CreateStartingUnits()
        {
            if (startDeckMode == StartDeckMode.Custom && customStartUnits.Count > 0)
            {
                CreateCustomUnits();
            }
            else
            {
                CreateDefaultUnits();
            }
        }

        /// <summary>
        /// 커스텀 유닛 생성
        /// [E4] Inspector에서 지정한 StartingUnitEntry 기반
        /// </summary>
        private void CreateCustomUnits()
        {
            foreach (var entry in customStartUnits)
            {
                // 집 인덱스 검증
                if (entry.houseIndex >= State.houses.Count)
                {
                    Debug.LogWarning($"[GameManager] 유닛 '{entry.unitName}': 집 인덱스 {entry.houseIndex} 초과");
                    continue;
                }

                var house = State.houses[entry.houseIndex];

                // UnitSystem.CreateUnit 사용 (시작 카드 자동 부여)
                var unit = UnitSystem.Instance?.CreateUnit(entry.unitName, entry.job, entry.stage);
                if (unit == null)
                {
                    Debug.LogWarning($"[GameManager] 유닛 생성 실패: {entry.unitName}");
                    continue;
                }

                unit.houseId = house.houseId;

                // 슬롯 배치
                switch (entry.slot?.ToLower())
                {
                    case "adulta":
                        house.adultSlotA = unit.unitId;
                        break;
                    case "adultb":
                        house.adultSlotB = unit.unitId;
                        break;
                    case "child":
                        house.childSlot = unit.unitId;
                        break;
                    default:
                        // 자동 배치
                        if (string.IsNullOrEmpty(house.adultSlotA))
                            house.adultSlotA = unit.unitId;
                        else if (string.IsNullOrEmpty(house.adultSlotB))
                            house.adultSlotB = unit.unitId;
                        else if (string.IsNullOrEmpty(house.childSlot))
                            house.childSlot = unit.unitId;
                        break;
                }
            }

            Debug.Log($"[GameManager] 커스텀 유닛 {State.units.Count}명 생성");
        }

        /// <summary>
        /// 기본 유닛 생성
        /// [E4] 폰 2명 + 나이트 1명
        /// </summary>
        private void CreateDefaultUnits()
        {
            var house1 = State.houses[0];
            var house2 = State.houses.Count > 1 ? State.houses[1] : house1;

            // 폰 A - 집1 어른 슬롯 A
            var pawnA = UnitSystem.Instance?.CreateUnit("주민 A", Job.Pawn, GrowthStage.Young);
            if (pawnA != null)
            {
                pawnA.houseId = house1.houseId;
                house1.adultSlotA = pawnA.unitId;
            }

            // 폰 B - 집1 어른 슬롯 B
            var pawnB = UnitSystem.Instance?.CreateUnit("주민 B", Job.Pawn, GrowthStage.Young);
            if (pawnB != null)
            {
                pawnB.houseId = house1.houseId;
                house1.adultSlotB = pawnB.unitId;
            }

            // 나이트 C - 집2 어른 슬롯 A
            var knightC = UnitSystem.Instance?.CreateUnit("주민 C", Job.Knight, GrowthStage.Young);
            if (knightC != null)
            {
                knightC.houseId = house2.houseId;
                house2.adultSlotA = knightC.unitId;
            }

            Debug.Log($"[GameManager] 기본 유닛 {State.units.Count}명 생성");
        }

        /// <summary>
        /// 시작 덱 생성
        /// [E4] 덱 모드에 따라 분기
        /// </summary>
        private void CreateStartingDeck()
        {
            if (startDeckMode == StartDeckMode.Custom && customBaseDeck.Count > 0)
            {
                CreateCustomDeck();
            }
            else
            {
                CreateDefaultDeck();
            }

            // 셔플
            if (shuffleDeck)
            {
                ShuffleDeck();
            }

            Debug.Log($"[GameManager] 시작 덱 {State.deck.Count}장 생성 (셔플: {shuffleDeck})");
        }

        /// <summary>
        /// 커스텀 덱 생성
        /// [E4] Inspector에서 지정한 CardSO 기반
        /// </summary>
        private void CreateCustomDeck()
        {
            foreach (var cardSO in customBaseDeck)
            {
                if (cardSO == null) continue;

                var cardInstance = CardInstance.Create(cardSO.id);
                State.deck.Add(cardInstance);
            }

            Debug.Log($"[GameManager] 커스텀 덱 {customBaseDeck.Count}장 추가");
        }

        /// <summary>
        /// 기본 덱 생성
        /// [E4] 동화 7장 (유닛 종속 카드는 UnitSystem.CreateUnit에서 자동 부여)
        /// </summary>
        private void CreateDefaultDeck()
        {
            // 동화 7장
            for (int i = 0; i < 7; i++)
            {
                var cardInstance = CardInstance.Create("copper");
                State.deck.Add(cardInstance);
            }

            Debug.Log("[GameManager] 기본 덱 7장 추가");
        }
        /// <summary>
        /// 덱 셔플 (Fisher-Yates 알고리즘)
        /// </summary>
        private void ShuffleDeck()
        {
            var deck = State.deck;
            int n = deck.Count;

            for (int i = n - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }

        // =====================================================================
        // 유지비 관리
        // =====================================================================

        /// <summary>
        /// 유지비 재계산
        /// </summary>
        public void RecalculateMaintenanceCost()
        {
            int cost = 0;
            foreach (var unit in State.units)
            {
                cost += unit.ownedCardIds.Count;
            }
            State.maintenanceCost = cost;
            Debug.Log($"[GameManager] 유지비 재계산: {cost}");

            OnGameStateChanged?.Invoke(State);
        }

        // =====================================================================
        // 승패 체크
        // =====================================================================

        /// <summary>
        /// 승패 조건 체크
        /// </summary>
        public void CheckWinLoseCondition()
        {
            if (!IsGameRunning) return;

            if (disableWinLoseCheck)
            {
                return;
            }

            // 패배: 유닛 전멸
            if (State.units.Count == 0)
            {
                EndGame(GameEndState.DefeatAllUnitsDead);
                return;
            }
        }

        /// <summary>
        /// 검증 턴 체크
        /// </summary>
        public bool CheckValidation(int turn)
        {
            if (disableWinLoseCheck)
            {
                Debug.LogWarning($"[GameManager] ⚠️ 검증 턴 {turn} 건너뜀 - 디버그 모드");
                return true;
            }

            int index = Array.IndexOf(GameConfig.ValidationTurns, turn);
            if (index < 0) return true;

            int requiredGold = GameConfig.ValidationGoldRequired[index];

            if (State.gold >= requiredGold)
            {
                Debug.Log($"[GameManager] 검증 턴 {turn} 통과! (보유: {State.gold}, 요구: {requiredGold})");
                GrantValidationReward();
                return true;
            }
            else
            {
                Debug.Log($"[GameManager] 검증 턴 {turn} 실패! (보유: {State.gold}, 요구: {requiredGold})");
                EndGame(GameEndState.DefeatValidation);
                return false;
            }
        }

        /// <summary>
        /// 검증 통과 보상 지급
        /// </summary>
        private void GrantValidationReward()
        {
            validationRewardCount++;
            string landName = $"보상 영토 {validationRewardCount}";

            var newLand = LandSystem.Instance?.AcquireLand(landName);
            if (newLand != null)
            {
                Debug.Log($"[GameManager] 검증 보상: {landName} 획득! (총 사유지: {State.lands.Count}개)");
            }
        }

        // =====================================================================
        // 디버그
        // =====================================================================

        /// <summary>
        /// 현재 상태 로그 출력
        /// </summary>
        [ContextMenu("Log Game State")]
        public void LogGameState()
        {
            if (State == null)
            {
                Debug.Log("[GameManager] 게임 상태 없음");
                return;
            }

            Debug.Log($"=== 게임 상태 (턴 {State.currentTurn}/{GameConfig.MaxTurns}) ===");
            Debug.Log($"페이즈: {State.currentPhase}");
            Debug.Log($"골드: {State.gold}, 유지비: {State.maintenanceCost}");
            Debug.Log($"전투력: {State.totalCombatPower}");
            Debug.Log($"골드 배수: {State.goldMultiplier}x, 골드 보너스: +{State.goldBonus}");
            Debug.Log($"덱: {State.deck.Count}, 손패: {State.hand.Count}, 버림: {State.discardPile.Count}");
            Debug.Log($"유닛: {State.units.Count}명, 집: {State.houses.Count}개, 사유지: {State.lands.Count}개");
            Debug.Log($"게임 상태: {State.endState}");
            Debug.Log($"디버그 모드: {(disableWinLoseCheck ? "ON" : "OFF")}");
            Debug.Log($"이벤트 모드: {startEventMode}, 덱 모드: {startDeckMode}");
        }

        /// <summary>
        /// 런타임에서 디버그 모드 토글
        /// </summary>
        [ContextMenu("Toggle Debug Mode")]
        public void ToggleDebugMode()
        {
            disableWinLoseCheck = !disableWinLoseCheck;
            Debug.Log($"[GameManager] 디버그 모드: {(disableWinLoseCheck ? "ON" : "OFF")}");
        }
    }
}