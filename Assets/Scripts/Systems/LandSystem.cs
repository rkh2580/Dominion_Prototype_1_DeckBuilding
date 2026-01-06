// =============================================================================
// LandSystem.cs
// 사유지 시스템
// =============================================================================
// [R7 신규] 2026-01-03
// - 사유지 개발 트리 관리
// - 전투력/골드/드로우 보너스 계산
// - 집 슬롯 보너스 계산
// [R8 수정] 2026-01-03
// - 사유지 획득/개발 시 집 자동 동기화 (SyncHouses)
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 사유지 시스템 (싱글톤)
    /// - 사유지 개발/업그레이드
    /// - 보너스 효과 계산
    /// - 집 동기화
    /// </summary>
    public class LandSystem : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static LandSystem Instance { get; private set; }

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>사유지 개발됨 (사유지, 새 타입, 새 레벨)</summary>
        public static event Action<LandInstance, LandType, int> OnLandDeveloped;

        /// <summary>사유지 획득 (사유지)</summary>
        public static event Action<LandInstance> OnLandAcquired;

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

            Debug.Log("[LandSystem] 초기화 완료");
        }

        // =====================================================================
        // 사유지 생성/획득
        // =====================================================================

        /// <summary>
        /// 새 사유지 획득 (빈 땅 Lv.0)
        /// [R8 수정] 획득 후 집 동기화
        /// </summary>
        public LandInstance AcquireLand(string name = null)
        {
            if (State == null) return null;

            var land = LandInstance.CreateEmpty();
            if (!string.IsNullOrEmpty(name))
            {
                land.landName = name;
            }

            State.lands.Add(land);

            Debug.Log($"[LandSystem] 사유지 획득: {land.landName}");
            OnLandAcquired?.Invoke(land);

            // [R8 추가] 집 동기화
            SyncHouses();

            return land;
        }

        // =====================================================================
        // 사유지 개발
        // =====================================================================

        /// <summary>
        /// 개발 가능 여부 확인
        /// [R9 추가] 구매 페이즈에서만 개발 가능
        /// </summary>
        public bool CanDevelop(LandInstance land)
        {
            if (State == null || land == null) return false;
            if (land.level >= 3) return false; // 최대 레벨

            // [R9 추가] 구매 페이즈에서만 개발 가능
            if (State.currentPhase != GamePhase.Purchase) return false;

            int cost = GetDevelopCost(land);
            return State.gold >= cost;
        }

        /// <summary>
        /// 개발 비용 계산
        /// </summary>
        public int GetDevelopCost(LandInstance land)
        {
            if (land == null) return int.MaxValue;

            // 0->1: 15, 1->2: 30, 2->3: 60
            return GameConfig.LandDevelopCosts[Mathf.Min(land.level, 2)];
        }

        /// <summary>
        /// 사유지 개발 (Lv.0->1: 루트 선택, Lv.1+: 자동 진행)
        /// [R8 수정] 개발 후 집 동기화
        /// </summary>
        /// <param name="land">개발할 사유지</param>
        /// <param name="newType">새 타입 (Lv.0->1 시 필수)</param>
        public bool DevelopLand(LandInstance land, LandType newType = LandType.Empty)
        {
            if (!CanDevelop(land))
            {
                Debug.LogWarning($"[LandSystem] 개발 불가: {land?.landName}");
                return false;
            }

            int cost = GetDevelopCost(land);
            if (!GoldSystem.Instance.TrySpendGold(cost))
            {
                return false;
            }

            // Lv.0 -> Lv.1: 루트 선택
            if (land.level == 0)
            {
                if (newType == LandType.Empty)
                {
                    Debug.LogWarning("[LandSystem] Lv.0 개발 시 루트 타입 필수");
                    GoldSystem.Instance.AddGold(cost); // 환불
                    return false;
                }
                land.landType = newType;
            }
            // 이미 루트가 정해진 경우 타입 유지

            land.level++;

            Debug.Log($"[LandSystem] 사유지 개발: {land.landName} -> {land.landType} Lv.{land.level}");
            OnLandDeveloped?.Invoke(land, land.landType, land.level);

            // [R8 추가] 집 동기화
            SyncHouses();

            return true;
        }

        /// <summary>
        /// Lv.0에서 선택 가능한 루트 목록
        /// </summary>
        public List<LandType> GetAvailableRoutes()
        {
            return new List<LandType>
            {
                LandType.Farm,      // 영지 루트
                LandType.Village,   // 거주 루트
                LandType.Watchtower,// 군사 루트
                LandType.Chapel,    // 신앙 루트
                LandType.Study,     // 학문 루트
                LandType.Shop       // 상업 루트
            };
        }

        // =====================================================================
        // 집 동기화 [R8 추가]
        // =====================================================================

        /// <summary>
        /// 사유지 집 보너스에 맞게 집 개수 동기화
        /// - 집 개수 = 사유지 집 보너스 총합
        /// - 상한 = GameConfig.MaxHouses (12)
        /// </summary>
        public void SyncHouses()
        {
            if (State == null) return;
            if (HouseSystem.Instance == null)
            {
                Debug.LogWarning("[LandSystem] HouseSystem 없음 - 집 동기화 스킵");
                return;
            }

            int targetHouseCount = Mathf.Min(GetTotalHouseBonus(), GameConfig.MaxHouses);
            int currentHouseCount = State.houses.Count;

            if (targetHouseCount > currentHouseCount)
            {
                int housesToCreate = targetHouseCount - currentHouseCount;
                Debug.Log($"[LandSystem] 집 동기화: {currentHouseCount} → {targetHouseCount} (+{housesToCreate})");

                for (int i = 0; i < housesToCreate; i++)
                {
                    HouseSystem.Instance.CreateHouse();
                }
            }
            else if (targetHouseCount < currentHouseCount)
            {
                // 현재 기획상 집 보너스가 줄어드는 경우는 없음
                // 필요 시 빈 집 제거 로직 추가
                Debug.LogWarning($"[LandSystem] 집 보너스 감소: {currentHouseCount} → {targetHouseCount} (처리 안 함)");
            }
        }

        // =====================================================================
        // 보너스 계산
        // =====================================================================

        /// <summary>
        /// 총 집 슬롯 보너스 계산
        /// </summary>
        public int GetTotalHouseBonus()
        {
            if (State == null) return 0;

            int total = 0;
            foreach (var land in State.lands)
            {
                total += GetHouseBonus(land);
            }
            return total;
        }

        /// <summary>
        /// 총 골드 배율 보너스 계산 (퍼센트)
        /// </summary>
        public int GetTotalGoldBonusPercent()
        {
            if (State == null) return 0;

            int total = 0;
            foreach (var land in State.lands)
            {
                total += GetGoldBonusPercent(land);
            }
            return total;
        }

        /// <summary>
        /// 총 전투력 보너스 계산
        /// </summary>
        public int GetTotalCombatPowerBonus()
        {
            if (State == null) return 0;

            int total = 0;
            foreach (var land in State.lands)
            {
                total += GetCombatPowerBonus(land);
            }
            return total;
        }

        /// <summary>
        /// 총 드로우 보너스 계산
        /// </summary>
        public int GetTotalDrawBonus()
        {
            if (State == null) return 0;

            int total = 0;
            foreach (var land in State.lands)
            {
                total += GetDrawBonus(land);
            }
            return total;
        }

        /// <summary>
        /// 총 손패 상한 보너스 계산
        /// </summary>
        public int GetTotalHandSizeBonus()
        {
            if (State == null) return 0;

            int total = 0;
            foreach (var land in State.lands)
            {
                total += GetHandSizeBonus(land);
            }
            return total;
        }

        // =====================================================================
        // 개별 사유지 보너스 (타입/레벨별)
        // =====================================================================

        /// <summary>
        /// 집 슬롯 보너스
        /// </summary>
        public int GetHouseBonus(LandInstance land)
        {
            if (land == null) return 0;

            // 기본: 모든 사유지 +1 집
            int bonus = 1;

            // 거주 루트: 추가 보너스
            switch (land.landType)
            {
                case LandType.Village:
                    if (land.level >= 1) bonus += 1; // Lv.1: +2
                    if (land.level >= 2) bonus += 1; // Lv.2: +3
                    if (land.level >= 3) bonus += 1; // Lv.3: +4
                    break;
                case LandType.Farm:
                    // 농장 -> 목장 분기 시 +2 (간단화: Lv.2에서 +1)
                    if (land.level >= 2) bonus += 1;
                    break;
            }

            return bonus;
        }

        /// <summary>
        /// 골드 배율 보너스 (퍼센트)
        /// </summary>
        public int GetGoldBonusPercent(LandInstance land)
        {
            if (land == null) return 0;

            switch (land.landType)
            {
                case LandType.Farm:
                    if (land.level == 1) return 10;
                    if (land.level == 2) return 20;
                    if (land.level == 3) return 30;
                    break;
                case LandType.Shop:
                    if (land.level == 1) return 10;
                    if (land.level == 2) return 20;
                    if (land.level == 3) return 30;
                    break;
                case LandType.Village:
                    if (land.level == 3) return 10;
                    break;
            }

            return 0;
        }

        /// <summary>
        /// 전투력 보너스
        /// </summary>
        public int GetCombatPowerBonus(LandInstance land)
        {
            if (land == null) return 0;

            switch (land.landType)
            {
                case LandType.Watchtower:
                    if (land.level == 1) return 15;
                    if (land.level == 2) return 30;
                    if (land.level == 3) return 50;
                    break;
            }

            return 0;
        }

        /// <summary>
        /// 드로우 보너스
        /// </summary>
        public int GetDrawBonus(LandInstance land)
        {
            if (land == null) return 0;

            switch (land.landType)
            {
                case LandType.Study:
                    if (land.level == 1) return 1;
                    if (land.level >= 2) return 2;
                    break;
                case LandType.Shop:
                    if (land.level == 3) return 1;
                    break;
                case LandType.Chapel:
                    // 수도원 분기 (간단화: Lv.2에서 +1)
                    if (land.level >= 2) return 1;
                    break;
            }

            return 0;
        }

        /// <summary>
        /// 손패 상한 보너스
        /// </summary>
        public int GetHandSizeBonus(LandInstance land)
        {
            if (land == null) return 0;

            switch (land.landType)
            {
                case LandType.Study:
                    // 서고 분기 (간단화)
                    if (land.level >= 2) return 2;
                    break;
                case LandType.Shop:
                    // 창고 분기 (간단화)
                    if (land.level >= 2) return 2;
                    break;
            }

            return 0;
        }

        // =====================================================================
        // 특수 효과 (오염 처리)
        // =====================================================================

        /// <summary>
        /// 턴 시작 시 오염 처리 (신앙 루트)
        /// </summary>
        public void ProcessPollutionEffects()
        {
            if (State == null) return;

            int removeCount = 0;

            foreach (var land in State.lands)
            {
                if (land.landType == LandType.Chapel)
                {
                    if (land.level == 3) removeCount += 2; // 대성당
                    else if (land.level == 2) removeCount += 1; // 교회
                    // Lv.1 예배당: 덱 맨 밑으로 이동 (별도 처리)
                }
            }

            if (removeCount > 0)
            {
                // TODO: DeckSystem과 연동하여 오염 카드 제거
                Debug.Log($"[LandSystem] 신앙 효과: 오염 {removeCount}장 소멸 예정");
            }
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        /// <summary>
        /// ID로 사유지 찾기
        /// </summary>
        public LandInstance GetLandById(string landId)
        {
            return State?.lands.Find(l => l.landId == landId);
        }

        /// <summary>
        /// 개발 가능한 사유지 목록
        /// </summary>
        public List<LandInstance> GetDevelopableLands()
        {
            if (State == null) return new List<LandInstance>();

            return State.lands.FindAll(l => CanDevelop(l));
        }

        /// <summary>
        /// 사유지 수
        /// </summary>
        public int LandCount => State?.lands.Count ?? 0;

        // =====================================================================
        // 디버그
        // =====================================================================

        [ContextMenu("Log Land Status")]
        public void LogLandStatus()
        {
            if (State == null || State.lands.Count == 0)
            {
                Debug.Log("[LandSystem] 사유지 없음");
                return;
            }

            Debug.Log($"=== 사유지 현황 ({State.lands.Count}개) ===");

            foreach (var land in State.lands)
            {
                string info = $"  {land.landName}: {land.landType} Lv.{land.level}";
                info += $" | 집+{GetHouseBonus(land)}";

                int gold = GetGoldBonusPercent(land);
                if (gold > 0) info += $" | 골드+{gold}%";

                int combat = GetCombatPowerBonus(land);
                if (combat > 0) info += $" | 전투력+{combat}";

                int draw = GetDrawBonus(land);
                if (draw > 0) info += $" | 드로우+{draw}";

                Debug.Log(info);
            }

            Debug.Log($"  총 집 보너스: +{GetTotalHouseBonus()}");
            Debug.Log($"  총 골드 보너스: +{GetTotalGoldBonusPercent()}%");
            Debug.Log($"  총 전투력 보너스: +{GetTotalCombatPowerBonus()}");
            Debug.Log($"  총 드로우 보너스: +{GetTotalDrawBonus()}");
        }
    }
}