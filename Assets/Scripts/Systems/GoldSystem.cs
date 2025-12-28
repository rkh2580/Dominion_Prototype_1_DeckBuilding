// =============================================================================
// GoldSystem.cs
// 골드 관리 시스템
// =============================================================================

using System;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 골드 시스템 (싱글톤)
    /// - 골드 추가/차감
    /// - 배수/보너스 적용
    /// - 골드 계산
    /// </summary>
    public class GoldSystem : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static GoldSystem Instance { get; private set; }

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>골드 변경됨 (이전 값, 현재 값, 변경량)</summary>
        public static event Action<int, int, int> OnGoldChanged;

        // =====================================================================
        // 상태 접근
        // =====================================================================

        private GameState State => GameManager.Instance.State;

        public int CurrentGold => State?.gold ?? 0;
        public int MaintenanceCost => State?.maintenanceCost ?? 0;
        public float GoldMultiplier => State?.goldMultiplier ?? 1f;
        public int GoldBonus => State?.goldBonus ?? 0;

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

            Debug.Log("[GoldSystem] 초기화 완료");
        }

        // =====================================================================
        // 골드 조작
        // =====================================================================

        /// <summary>
        /// 골드 추가 (배수/보너스 적용)
        /// </summary>
        /// <param name="baseAmount">기본 골드량</param>
        /// <param name="applyMultiplier">배수 적용 여부 (기본 true)</param>
        public void AddGold(int baseAmount, bool applyMultiplier = true)
        {
            if (State == null) return;

            int finalAmount = baseAmount;

            // 배수 적용 (투자, 황금기 등)
            if (applyMultiplier && State.goldMultiplier != 1f)
            {
                finalAmount = Mathf.FloorToInt(finalAmount * State.goldMultiplier);
            }

            // 보너스 적용
            if (applyMultiplier && State.goldBonus > 0)
            {
                finalAmount += State.goldBonus;
            }

            int oldGold = State.gold;
            State.gold += finalAmount;

            Debug.Log($"[GoldSystem] +{finalAmount} 골드 (기본 {baseAmount}, 배수 {State.goldMultiplier}x) → 현재 {State.gold}");
            OnGoldChanged?.Invoke(oldGold, State.gold, finalAmount);

            // 골드 변경 후 승패 체크
            GameManager.Instance?.CheckWinLoseCondition();
        }

        /// <summary>
        /// 골드 차감 (배수 적용 없음)
        /// </summary>
        public void SubtractGold(int amount)
        {
            if (State == null) return;

            int oldGold = State.gold;
            State.gold -= amount;

            Debug.Log($"[GoldSystem] -{amount} 골드 → 현재 {State.gold}");
            OnGoldChanged?.Invoke(oldGold, State.gold, -amount);

            // 골드 변경 후 승패 체크
            GameManager.Instance?.CheckWinLoseCondition();
        }

        /// <summary>
        /// 골드 직접 설정 (디버그용)
        /// </summary>
        public void SetGold(int amount)
        {
            if (State == null) return;

            int oldGold = State.gold;
            int delta = amount - oldGold;
            State.gold = amount;

            Debug.Log($"[GoldSystem] 골드 설정: {amount}");
            OnGoldChanged?.Invoke(oldGold, State.gold, delta);
        }

        // =====================================================================
        // 배수/보너스 설정
        // =====================================================================

        /// <summary>
        /// 골드 배수 설정 (이번 턴)
        /// 예: 투자 +50% → SetMultiplier(1.5f)
        /// 예: 황금기 2배 → SetMultiplier(2f)
        /// </summary>
        public void SetMultiplier(float multiplier)
        {
            if (State == null) return;

            State.goldMultiplier = multiplier;
            Debug.Log($"[GoldSystem] 골드 배수 설정: {multiplier}x");
        }

        /// <summary>
        /// 골드 배수 추가 (중첩)
        /// 예: 기존 1.5x + 투자 +50% → 1.5 * 1.5 = 2.25x
        /// </summary>
        public void MultiplyMultiplier(float factor)
        {
            if (State == null) return;

            State.goldMultiplier *= factor;
            Debug.Log($"[GoldSystem] 골드 배수 중첩: {factor}x → 현재 {State.goldMultiplier}x");
        }

        /// <summary>
        /// 골드 고정 보너스 설정 (이번 턴)
        /// </summary>
        public void SetBonus(int bonus)
        {
            if (State == null) return;

            State.goldBonus = bonus;
            Debug.Log($"[GoldSystem] 골드 보너스 설정: +{bonus}");
        }

        /// <summary>
        /// 골드 고정 보너스 추가
        /// </summary>
        public void AddBonus(int bonus)
        {
            if (State == null) return;

            State.goldBonus += bonus;
            Debug.Log($"[GoldSystem] 골드 보너스 추가: +{bonus} → 현재 +{State.goldBonus}");
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        /// <summary>
        /// 골드가 충분한지 확인
        /// </summary>
        public bool HasEnoughGold(int amount)
        {
            return State != null && State.gold >= amount;
        }

        /// <summary>
        /// 골드 소모 시도 (충분하면 차감하고 true 반환)
        /// </summary>
        public bool TrySpendGold(int amount)
        {
            if (!HasEnoughGold(amount))
            {
                Debug.Log($"[GoldSystem] 골드 부족: 필요 {amount}, 보유 {State?.gold ?? 0}");
                return false;
            }

            SubtractGold(amount);
            return true;
        }

        /// <summary>
        /// 퍼센트 기반 골드 계산 (내림)
        /// 예: 도적 습격 20% 손실
        /// </summary>
        public int CalculatePercentage(int baseAmount, int percentage)
        {
            return Mathf.FloorToInt(baseAmount * percentage / 100f);
        }
    }
}
