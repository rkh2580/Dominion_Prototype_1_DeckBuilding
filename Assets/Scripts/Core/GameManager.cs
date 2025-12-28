// =============================================================================
// GameManager.cs
// 게임 전체 흐름 제어 및 초기화
// =============================================================================

using System;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 게임 매니저 (싱글톤)
    /// - 게임 초기화
    /// - 게임 상태 관리
    /// - 시스템 간 조율
    /// </summary>
    public class GameManager : MonoBehaviour
    {
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
        /// </summary>
        public void StartNewGame()
        {
            Debug.Log("[GameManager] 새 게임 시작");

            // 1. 게임 상태 초기화
            State = GameState.CreateNew();

            // 2. 시작 유닛 생성
            CreateStartingUnits();

            // 3. 시작 덱 생성
            CreateStartingDeck();

            // 4. 유지비 계산
            RecalculateMaintenanceCost();

            // 5. 이벤트 발생
            OnGameStarted?.Invoke();
            OnGameStateChanged?.Invoke(State);

            // 6. 첫 턴 시작
            TurnManager.Instance?.StartGame();
        }

        /// <summary>
        /// 게임 종료 처리
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
                GameEndState.Victory => "승리!",
                GameEndState.DefeatBankrupt => "패배 (파산)",
                GameEndState.DefeatValidation => "패배 (검증 실패)",
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
            OnGameStateChanged?.Invoke(State);
            Debug.Log("[GameManager] 게임 상태 로드됨");
        }

        // =====================================================================
        // 초기화 헬퍼
        // =====================================================================

        /// <summary>
        /// 시작 유닛 생성 (폰 2명, 나이트 1명)
        /// </summary>
        private void CreateStartingUnits()
        {
            // 폰 A
            var pawnA = UnitInstance.Create("주민 A", Job.Pawn, GrowthStage.Young);
            State.units.Add(pawnA);

            // 폰 B
            var pawnB = UnitInstance.Create("주민 B", Job.Pawn, GrowthStage.Young);
            State.units.Add(pawnB);

            // 나이트 C
            var knightC = UnitInstance.Create("주민 C", Job.Knight, GrowthStage.Young);
            State.units.Add(knightC);

            Debug.Log($"[GameManager] 시작 유닛 {State.units.Count}명 생성");
        }

        /// <summary>
        /// 시작 덱 생성
        /// - 동화 7장 (무소속)
        /// - 노동 2장 (폰 A, B 종속)
        /// - 탐색 1장 (나이트 C 종속)
        /// </summary>
        private void CreateStartingDeck()
        {
            // 동화 7장 (무소속)
            for (int i = 0; i < 7; i++)
            {
                var copper = CardInstance.Create("copper", null);
                State.deck.Add(copper);
            }

            // 폰 A의 노동
            var laborA = CardInstance.Create("labor", State.units[0].unitId);
            State.units[0].ownedCardIds.Add(laborA.instanceId);
            State.deck.Add(laborA);

            // 폰 B의 노동
            var laborB = CardInstance.Create("labor", State.units[1].unitId);
            State.units[1].ownedCardIds.Add(laborB.instanceId);
            State.deck.Add(laborB);

            // 나이트 C의 탐색
            var explore = CardInstance.Create("explore", State.units[2].unitId);
            State.units[2].ownedCardIds.Add(explore.instanceId);
            State.deck.Add(explore);

            // 덱 셔플
            ShuffleDeck();

            Debug.Log($"[GameManager] 시작 덱 {State.deck.Count}장 생성");
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
        /// 유지비 = 모든 유닛의 종속 카드 수 합계 (무소속 제외)
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

            // 패배: 골드 < 0
            if (State.gold < 0)
            {
                EndGame(GameEndState.DefeatBankrupt);
                return;
            }

            // 승리: 45턴 완료
            if (State.currentTurn >= GameConfig.MaxTurns)
            {
                EndGame(GameEndState.Victory);
                return;
            }
        }

        /// <summary>
        /// 검증 턴 체크
        /// </summary>
        public bool CheckValidation(int turn)
        {
            int index = Array.IndexOf(GameConfig.ValidationTurns, turn);
            if (index < 0) return true; // 검증 턴 아님

            int requiredGold = GameConfig.ValidationGoldRequired[index];
            
            if (State.gold >= requiredGold)
            {
                Debug.Log($"[GameManager] 검증 턴 {turn} 통과! (보유: {State.gold}, 요구: {requiredGold})");
                return true;
            }
            else
            {
                Debug.Log($"[GameManager] 검증 턴 {turn} 실패! (보유: {State.gold}, 요구: {requiredGold})");
                EndGame(GameEndState.DefeatValidation);
                return false;
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

            Debug.Log($"=== 게임 상태 (턴 {State.currentTurn}) ===");
            Debug.Log($"페이즈: {State.currentPhase}");
            Debug.Log($"골드: {State.gold}, 유지비: {State.maintenanceCost}");
            Debug.Log($"덱: {State.deck.Count}, 손패: {State.hand.Count}, 버림: {State.discardPile.Count}");
            Debug.Log($"유닛: {State.units.Count}명");
            Debug.Log($"게임 상태: {State.endState}");
        }
    }
}
