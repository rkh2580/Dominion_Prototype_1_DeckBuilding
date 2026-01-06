// =============================================================================
// LoyaltySystem.cs
// 충성도 시스템
// =============================================================================
// [R6 신규] 2026-01-03
// - 적자 시 충성도 감소 (-20)
// - 흑자 시 충성도 회복 (+10)
// - 충성도 0 이하 시 가출 (유닛 소멸)
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 충성도 시스템 (싱글톤)
    /// - 턴 종료 시 골드 상태에 따라 충성도 변동
    /// - 충성도 0 이하 유닛 가출 처리
    /// </summary>
    public class LoyaltySystem : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static LoyaltySystem Instance { get; private set; }

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>충성도 변동 (유닛, 변동량, 새 충성도)</summary>
        public static event Action<UnitInstance, int, int> OnLoyaltyChanged;

        /// <summary>유닛 가출 (유닛)</summary>
        public static event Action<UnitInstance> OnUnitDeserted;

        /// <summary>전체 충성도 변동 완료 (적자 여부, 변동량)</summary>
        public static event Action<bool, int> OnLoyaltyProcessed;

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

            Debug.Log("[LoyaltySystem] 초기화 완료");
        }

        // =====================================================================
        // 메인 처리 (TurnManager에서 호출)
        // =====================================================================

        /// <summary>
        /// 턴 종료 시 충성도 처리
        /// </summary>
        public void ProcessLoyalty()
        {
            if (State == null) return;

            bool isDeficit = State.gold < 0;
            int changeAmount = isDeficit ? GameConfig.LoyaltyDeficitPenalty : GameConfig.LoyaltySurplusBonus;

            Debug.Log($"[LoyaltySystem] === 충성도 처리 === 골드: {State.gold}, 적자: {isDeficit}, 변동: {changeAmount}");

            // 모든 유닛 충성도 변경
            foreach (var unit in State.units.ToArray()) // ToArray로 순회 중 삭제 대비
            {
                // 유년은 충성도 변동 없음 (가출도 안 함)
                if (unit.stage == GrowthStage.Child)
                {
                    continue;
                }

                int oldLoyalty = unit.loyalty;
                unit.loyalty = Mathf.Clamp(unit.loyalty + changeAmount, 0, GameConfig.AdultLoyalty);

                if (oldLoyalty != unit.loyalty)
                {
                    Debug.Log($"[LoyaltySystem] {unit.unitName}: {oldLoyalty} → {unit.loyalty}");
                    OnLoyaltyChanged?.Invoke(unit, changeAmount, unit.loyalty);
                }
            }

            OnLoyaltyProcessed?.Invoke(isDeficit, changeAmount);
        }

        /// <summary>
        /// 턴 시작 시 가출 처리 (충성도 0 이하인 유닛)
        /// </summary>
        public void ProcessDesertions()
        {
            if (State == null) return;

            var deserters = new List<UnitInstance>();

            foreach (var unit in State.units)
            {
                // 유년은 가출 안 함
                if (unit.stage == GrowthStage.Child)
                {
                    continue;
                }

                if (unit.loyalty <= 0)
                {
                    deserters.Add(unit);
                }
            }

            if (deserters.Count == 0)
            {
                return;
            }

            Debug.Log($"[LoyaltySystem] === 가출 처리 === {deserters.Count}명");

            foreach (var unit in deserters)
            {
                ProcessDesertion(unit);
            }
        }

        /// <summary>
        /// 단일 유닛 가출 처리
        /// </summary>
        private void ProcessDesertion(UnitInstance unit)
        {
            Debug.Log($"[LoyaltySystem] 유닛 가출: {unit.unitName} (충성도: {unit.loyalty})");

            // 이벤트 먼저 발생 (UI 업데이트용)
            OnUnitDeserted?.Invoke(unit);

            // UnitSystem을 통해 유닛 제거 (종속 카드 삭제 포함)
            UnitSystem.Instance?.KillUnit(unit);
        }

        // =====================================================================
        // 충성도 조작
        // =====================================================================

        /// <summary>
        /// 특정 유닛 충성도 변경
        /// </summary>
        public void ChangeLoyalty(UnitInstance unit, int amount)
        {
            if (unit == null) return;

            int oldLoyalty = unit.loyalty;
            unit.loyalty = Mathf.Clamp(unit.loyalty + amount, 0, GameConfig.AdultLoyalty);

            if (oldLoyalty != unit.loyalty)
            {
                Debug.Log($"[LoyaltySystem] {unit.unitName} 충성도 변경: {oldLoyalty} → {unit.loyalty} ({amount:+#;-#;0})");
                OnLoyaltyChanged?.Invoke(unit, amount, unit.loyalty);
            }
        }

        /// <summary>
        /// 특정 유닛 충성도 설정 (절대값)
        /// </summary>
        public void SetLoyalty(UnitInstance unit, int value)
        {
            if (unit == null) return;

            int oldLoyalty = unit.loyalty;
            unit.loyalty = Mathf.Clamp(value, 0, GameConfig.AdultLoyalty);

            if (oldLoyalty != unit.loyalty)
            {
                int change = unit.loyalty - oldLoyalty;
                Debug.Log($"[LoyaltySystem] {unit.unitName} 충성도 설정: {oldLoyalty} → {unit.loyalty}");
                OnLoyaltyChanged?.Invoke(unit, change, unit.loyalty);
            }
        }

        /// <summary>
        /// 모든 유닛 충성도 최대로 회복
        /// </summary>
        public void RestoreAllLoyalty()
        {
            if (State == null) return;

            foreach (var unit in State.units)
            {
                if (unit.stage != GrowthStage.Child)
                {
                    SetLoyalty(unit, GameConfig.AdultLoyalty);
                }
            }

            Debug.Log("[LoyaltySystem] 모든 유닛 충성도 회복");
        }

        // =====================================================================
        // 조회 유틸리티
        // =====================================================================

        /// <summary>
        /// 가출 위험 유닛 목록 (충성도 20 이하)
        /// </summary>
        public List<UnitInstance> GetLowLoyaltyUnits(int threshold = 20)
        {
            if (State == null) return new List<UnitInstance>();

            return State.units.FindAll(u =>
                u.stage != GrowthStage.Child &&
                u.loyalty <= threshold
            );
        }

        /// <summary>
        /// 평균 충성도
        /// </summary>
        public float GetAverageLoyalty()
        {
            if (State == null || State.units.Count == 0) return 0;

            int total = 0;
            int count = 0;

            foreach (var unit in State.units)
            {
                if (unit.stage != GrowthStage.Child)
                {
                    total += unit.loyalty;
                    count++;
                }
            }

            return count > 0 ? (float)total / count : 0;
        }

        /// <summary>
        /// 가출 예정 유닛 수 (충성도 0 이하)
        /// </summary>
        public int GetDesertionCount()
        {
            if (State == null) return 0;

            int count = 0;
            foreach (var unit in State.units)
            {
                if (unit.stage != GrowthStage.Child && unit.loyalty <= 0)
                {
                    count++;
                }
            }
            return count;
        }

        // =====================================================================
        // 디버그
        // =====================================================================

        /// <summary>
        /// 충성도 상태 로그
        /// </summary>
        [ContextMenu("Log Loyalty Status")]
        public void LogLoyaltyStatus()
        {
            if (State == null || State.units.Count == 0)
            {
                Debug.Log("[LoyaltySystem] 유닛 없음");
                return;
            }

            Debug.Log("=== 충성도 현황 ===");

            foreach (var unit in State.units)
            {
                string status = "";
                if (unit.stage == GrowthStage.Child)
                {
                    status = "(유년 - 가출 면제)";
                }
                else if (unit.loyalty <= 0)
                {
                    status = "[!] 가출 예정";
                }
                else if (unit.loyalty <= 20)
                {
                    status = "[!] 위험";
                }
                else if (unit.loyalty >= GameConfig.AdultLoyalty)
                {
                    status = "최대";
                }

                Debug.Log($"  {unit.unitName}: {unit.loyalty}/{GameConfig.AdultLoyalty} {status}");
            }

            Debug.Log($"  평균 충성도: {GetAverageLoyalty():F1}");
            Debug.Log($"  가출 예정: {GetDesertionCount()}명");
        }

        /// <summary>
        /// 디버그: 모든 유닛 충성도 -30
        /// </summary>
        [ContextMenu("Debug: Reduce All Loyalty -30")]
        public void DebugReduceLoyalty()
        {
            if (State == null) return;

            foreach (var unit in State.units)
            {
                if (unit.stage != GrowthStage.Child)
                {
                    ChangeLoyalty(unit, -30);
                }
            }
        }

        /// <summary>
        /// 디버그: 강제 가출 처리
        /// </summary>
        [ContextMenu("Debug: Force Process Desertions")]
        public void DebugForceDesertions()
        {
            ProcessDesertions();
        }
    }
}