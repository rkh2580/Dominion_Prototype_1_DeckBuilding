// =============================================================================
// RaidSystem.cs
// 약탈/전투 시스템
// =============================================================================
// [R7 신규] 2026-01-03
// - 약탈 이벤트 처리 (매 8턴)
// - 최종 전투 처리 (60턴)
// - 전투력 비교 및 결과 처리
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 약탈/전투 시스템 (싱글톤)
    /// - 약탈 방어 처리
    /// - 최종 전투 처리
    /// </summary>
    public class RaidSystem : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static RaidSystem Instance { get; private set; }

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>약탈 시작 (턴, 적 전투력, 아군 전투력)</summary>
        public static event Action<int, int, int> OnRaidStarted;

        /// <summary>약탈 결과 (방어 성공 여부, 적 전투력, 아군 전투력)</summary>
        public static event Action<bool, int, int> OnRaidCompleted;

        /// <summary>약탈 피해 (피해 타입, 피해량 또는 유닛)</summary>
        public static event Action<RaidDamageType, int, UnitInstance> OnRaidDamage;

        /// <summary>최종 전투 시작 (요구 전투력, 아군 전투력)</summary>
        public static event Action<int, int> OnFinalBattleStarted;

        /// <summary>최종 전투 결과 (승리 여부)</summary>
        public static event Action<bool> OnFinalBattleCompleted;

        // =====================================================================
        // 상태 접근
        // =====================================================================

        private GameState State => GameManager.Instance.State;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Debug.Log("[RaidSystem] 초기화 완료");
        }

        // =====================================================================
        // 약탈 처리 (TurnManager에서 호출)
        // =====================================================================

        /// <summary>
        /// 약탈 이벤트 처리
        /// </summary>
        public void ProcessRaid(int turn)
        {
            if (State == null) return;

            // 적 전투력 계산: (턴 x 3) + 10
            int enemyPower = CalculateEnemyPower(turn);
            int playerPower = GetTotalCombatPower();

            Debug.Log($"[RaidSystem] === 약탈 발생 (턴 {turn}) ===");
            Debug.Log($"[RaidSystem] 적 전투력: {enemyPower} vs 아군 전투력: {playerPower}");

            OnRaidStarted?.Invoke(turn, enemyPower, playerPower);

            bool defended = playerPower >= enemyPower;

            if (defended)
            {
                Debug.Log("[RaidSystem] 방어 성공!");
            }
            else
            {
                Debug.Log("[RaidSystem] 방어 실패 - 피해 발생");
                ProcessRaidDamage();
            }

            OnRaidCompleted?.Invoke(defended, enemyPower, playerPower);
        }

        /// <summary>
        /// 적 전투력 계산
        /// </summary>
        private int CalculateEnemyPower(int turn)
        {
            return (turn * 3) + 10;
        }

        /// <summary>
        /// 약탈 피해 처리
        /// </summary>
        private void ProcessRaidDamage()
        {
            // 50% 확률로 골드 손실 또는 유년 납치
            bool goldLoss = UnityEngine.Random.Range(0, 2) == 0;

            if (goldLoss || !HasChildUnit())
            {
                // 골드 30% 손실
                int loss = Mathf.CeilToInt(State.gold * 0.3f);
                loss = Mathf.Max(loss, 10); // 최소 10골드

                GoldSystem.Instance?.SubtractGold(loss);
                Debug.Log($"[RaidSystem] 골드 손실: -{loss}");

                OnRaidDamage?.Invoke(RaidDamageType.GoldLoss, loss, null);
            }
            else
            {
                // 유년 1명 납치
                var child = GetRandomChildUnit();
                if (child != null)
                {
                    Debug.Log($"[RaidSystem] 유년 납치: {child.unitName}");

                    OnRaidDamage?.Invoke(RaidDamageType.ChildKidnapped, 0, child);

                    // HouseSystem에서 제거
                    HouseSystem.Instance?.RemoveUnit(child);

                    // UnitSystem에서 제거
                    UnitSystem.Instance?.KillUnit(child);
                }
            }
        }

        /// <summary>
        /// 유년 유닛 존재 여부
        /// </summary>
        private bool HasChildUnit()
        {
            if (State == null) return false;
            return State.units.Exists(u => u.stage == GrowthStage.Child);
        }

        /// <summary>
        /// 랜덤 유년 유닛 선택
        /// </summary>
        private UnitInstance GetRandomChildUnit()
        {
            if (State == null) return null;

            var children = State.units.FindAll(u => u.stage == GrowthStage.Child);
            if (children.Count == 0) return null;

            int index = UnityEngine.Random.Range(0, children.Count);
            return children[index];
        }

        // =====================================================================
        // 최종 전투 (60턴)
        // =====================================================================

        /// <summary>
        /// 최종 전투 처리
        /// </summary>
        public bool ProcessFinalBattle()
        {
            if (State == null) return false;

            int requiredPower = GameConfig.FinalBattleRequiredPower;
            int playerPower = GetTotalCombatPower();

            Debug.Log($"[RaidSystem] === 최종 전투 ===");
            Debug.Log($"[RaidSystem] 요구 전투력: {requiredPower} vs 아군 전투력: {playerPower}");

            OnFinalBattleStarted?.Invoke(requiredPower, playerPower);

            bool victory = playerPower >= requiredPower;

            if (victory)
            {
                Debug.Log("[RaidSystem] 최종 전투 승리!");
            }
            else
            {
                Debug.Log("[RaidSystem] 최종 전투 패배...");
            }

            OnFinalBattleCompleted?.Invoke(victory);

            return victory;
        }

        // =====================================================================
        // 전투력 계산
        // =====================================================================

        /// <summary>
        /// 총 전투력 계산 (유닛 + 사유지)
        /// </summary>
        public int GetTotalCombatPower()
        {
            int unitPower = GetUnitCombatPower();
            int landPower = LandSystem.Instance?.GetTotalCombatPowerBonus() ?? 0;

            return unitPower + landPower;
        }

        /// <summary>
        /// 유닛 전투력 합계
        /// </summary>
        public int GetUnitCombatPower()
        {
            if (State == null) return 0;

            int total = 0;
            foreach (var unit in State.units)
            {
                total += unit.combatPower;
            }
            return total;
        }

        /// <summary>
        /// 전투력 상세 분석
        /// </summary>
        public (int unitPower, int landPower, int total) GetCombatPowerBreakdown()
        {
            int unitPower = GetUnitCombatPower();
            int landPower = LandSystem.Instance?.GetTotalCombatPowerBonus() ?? 0;
            return (unitPower, landPower, unitPower + landPower);
        }

        // =====================================================================
        // 약탈 예측
        // =====================================================================

        /// <summary>
        /// 다음 약탈 턴
        /// </summary>
        public int GetNextRaidTurn()
        {
            if (State == null) return -1;

            int currentTurn = State.currentTurn;

            foreach (int raidTurn in GameConfig.RaidTurns)
            {
                if (raidTurn > currentTurn)
                {
                    return raidTurn;
                }
            }

            return -1; // 더 이상 약탈 없음
        }

        /// <summary>
        /// 다음 약탈 적 전투력 예측
        /// </summary>
        public int GetNextRaidEnemyPower()
        {
            int nextTurn = GetNextRaidTurn();
            if (nextTurn < 0) return 0;

            return CalculateEnemyPower(nextTurn);
        }

        /// <summary>
        /// 현재 전투력으로 방어 가능 여부
        /// </summary>
        public bool CanDefendNextRaid()
        {
            int nextEnemyPower = GetNextRaidEnemyPower();
            if (nextEnemyPower <= 0) return true;

            return GetTotalCombatPower() >= nextEnemyPower;
        }

        // =====================================================================
        // 디버그
        // =====================================================================

        [ContextMenu("Log Combat Status")]
        public void LogCombatStatus()
        {
            var breakdown = GetCombatPowerBreakdown();

            Debug.Log("=== 전투력 현황 ===");
            Debug.Log($"  유닛 전투력: {breakdown.unitPower}");
            Debug.Log($"  사유지 보너스: {breakdown.landPower}");
            Debug.Log($"  총 전투력: {breakdown.total}");

            int nextRaid = GetNextRaidTurn();
            if (nextRaid > 0)
            {
                int enemyPower = GetNextRaidEnemyPower();
                bool canDefend = CanDefendNextRaid();
                Debug.Log($"  다음 약탈: {nextRaid}턴 (적 {enemyPower}) - {(canDefend ? "방어 가능" : "방어 불가!")}");
            }

            Debug.Log($"  최종 전투 요구: {GameConfig.FinalBattleRequiredPower}");
        }

        [ContextMenu("Debug: Force Raid")]
        public void DebugForceRaid()
        {
            ProcessRaid(State?.currentTurn ?? 8);
        }

        [ContextMenu("Debug: Force Final Battle")]
        public void DebugForceFinalBattle()
        {
            ProcessFinalBattle();
        }
    }

    /// <summary>
    /// 약탈 피해 타입
    /// </summary>
    public enum RaidDamageType
    {
        GoldLoss,       // 골드 손실
        ChildKidnapped  // 유년 납치
    }
}