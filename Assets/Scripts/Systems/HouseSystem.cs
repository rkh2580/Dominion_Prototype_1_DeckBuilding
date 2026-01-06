// =============================================================================
// HouseSystem.cs
// 집(House) 관리 시스템
// =============================================================================
// [R3 신규] 2026-01-03
// - 유닛 슬롯 배치/이동/제거
// - 임신 상태 관리
// - 유닛 사망 시 슬롯 정리
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 집 슬롯 타입
    /// </summary>
    public enum HouseSlotType
    {
        AdultA,     // 어른 슬롯 A
        AdultB,     // 어른 슬롯 B
        Child       // 유년 슬롯
    }

    /// <summary>
    /// 집 시스템 (싱글톤)
    /// - 유닛 배치/이동
    /// - 임신 상태 관리
    /// - 집 추가/제거
    /// </summary>
    public class HouseSystem : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static HouseSystem Instance { get; private set; }

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>집 생성됨</summary>
        public static event Action<HouseInstance> OnHouseCreated;

        /// <summary>집 제거됨</summary>
        public static event Action<HouseInstance> OnHouseRemoved;

        /// <summary>유닛이 집에 배치됨 (집, 유닛, 슬롯)</summary>
        public static event Action<HouseInstance, UnitInstance, HouseSlotType> OnUnitPlaced;

        /// <summary>유닛이 집에서 제거됨 (집, 유닛)</summary>
        public static event Action<HouseInstance, UnitInstance> OnUnitRemoved;

        /// <summary>임신 시작됨</summary>
        public static event Action<HouseInstance> OnPregnancyStarted;

        /// <summary>임신 취소됨 (유닛 사망/이동 등)</summary>
        public static event Action<HouseInstance> OnPregnancyCancelled;

        /// <summary>출산됨 (집, 새 유닛)</summary>
        public static event Action<HouseInstance, UnitInstance> OnBirth;

        // =====================================================================
        // 상태 접근
        // =====================================================================

        private GameState State => GameManager.Instance.State;

        public List<HouseInstance> Houses => State?.houses;
        public int HouseCount => State?.houses.Count ?? 0;

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

            Debug.Log("[HouseSystem] 초기화 완료");
        }

        private void OnEnable()
        {
            // 유닛 사망 이벤트 구독
            UnitSystem.OnUnitDied += HandleUnitDied;

            // ※ [R8 추가] 사유지 개발 이벤트 구독
            LandSystem.OnLandDeveloped += OnLandDeveloped;
            LandSystem.OnLandAcquired += OnLandAcquired;
        }

        private void OnDisable()
        {
            UnitSystem.OnUnitDied -= HandleUnitDied;

            // ※ [R8 추가] 사유지 이벤트 구독 해제
            LandSystem.OnLandDeveloped -= OnLandDeveloped;
            LandSystem.OnLandAcquired -= OnLandAcquired;
        }


        // =====================================================================
        // 집 생성/제거
        // =====================================================================

        /// <summary>
        /// 새 집 생성
        /// </summary>
        public HouseInstance CreateHouse(string name = null)
        {
            if (State == null) return null;

            string houseName = name ?? $"{State.houses.Count + 1}번 집";
            var house = HouseInstance.Create(houseName);
            State.houses.Add(house);

            Debug.Log($"[HouseSystem] 집 생성: {houseName}");
            OnHouseCreated?.Invoke(house);

            return house;
        }

        /// <summary>
        /// 집 제거 (내부 유닛도 처리 필요)
        /// </summary>
        public bool RemoveHouse(HouseInstance house)
        {
            if (State == null || house == null) return false;
            if (!State.houses.Contains(house))
            {
                Debug.LogWarning($"[HouseSystem] 제거할 집을 찾지 못함: {house.houseName}");
                return false;
            }

            // 집에 유닛이 있으면 제거 불가 (또는 강제 퇴거 처리)
            if (house.GetResidentCount() > 0)
            {
                Debug.LogWarning($"[HouseSystem] 집에 유닛이 있어 제거 불가: {house.houseName}");
                return false;
            }

            State.houses.Remove(house);
            Debug.Log($"[HouseSystem] 집 제거: {house.houseName}");
            OnHouseRemoved?.Invoke(house);

            return true;
        }

        // =====================================================================
        // 유닛 배치
        // =====================================================================

        /// <summary>
        /// 유닛을 집 슬롯에 배치
        /// </summary>
        public bool PlaceUnit(UnitInstance unit, HouseInstance house, HouseSlotType slot)
        {
            if (State == null || unit == null || house == null) return false;

            // 슬롯 적합성 검사
            if (!IsSlotValidForUnit(unit, slot))
            {
                Debug.LogWarning($"[HouseSystem] 슬롯 부적합: {unit.unitName}({unit.stage}) -> {slot}");
                return false;
            }

            // 슬롯 비어있는지 검사
            if (!IsSlotEmpty(house, slot))
            {
                Debug.LogWarning($"[HouseSystem] 슬롯 이미 사용 중: {house.houseName}.{slot}");
                return false;
            }

            // 기존 집에서 제거 (다른 집에 있었다면)
            if (!string.IsNullOrEmpty(unit.houseId))
            {
                var oldHouse = GetHouseById(unit.houseId);
                if (oldHouse != null && oldHouse != house)
                {
                    RemoveUnitFromHouse(unit, oldHouse);
                }
            }

            // 슬롯에 배치
            SetSlot(house, slot, unit.unitId);
            unit.houseId = house.houseId;

            // 임신 중인 집으로 이동 시 경고 (실제로는 막아야 하지만 일단 허용)
            if (house.isPregnant)
            {
                Debug.LogWarning($"[HouseSystem] 임신 중인 집에 유닛 배치됨 (주의)");
            }

            // 교배 확률 카운터 리셋 (새 유닛이 들어왔으므로)
            house.fertilityCounter = 0;

            Debug.Log($"[HouseSystem] 유닛 배치: {unit.unitName} -> {house.houseName}.{slot}");
            OnUnitPlaced?.Invoke(house, unit, slot);

            return true;
        }

        /// <summary>
        /// 유닛을 집에서 제거 (집 지정)
        /// </summary>
        public bool RemoveUnitFromHouse(UnitInstance unit, HouseInstance house)
        {
            if (unit == null || house == null) return false;

            // 어느 슬롯에 있는지 찾기
            HouseSlotType? slot = FindUnitSlot(house, unit.unitId);
            if (slot == null)
            {
                Debug.LogWarning($"[HouseSystem] 유닛이 해당 집에 없음: {unit.unitName}");
                return false;
            }

            // 슬롯 비우기
            SetSlot(house, slot.Value, null);
            unit.houseId = null;

            // 임신 중이면 취소
            if (house.isPregnant)
            {
                CancelPregnancy(house);
            }

            // 교배 확률 카운터 리셋
            house.fertilityCounter = 0;

            Debug.Log($"[HouseSystem] 유닛 제거: {unit.unitName} <- {house.houseName}");
            OnUnitRemoved?.Invoke(house, unit);

            return true;
        }

        /// <summary>
        /// 유닛을 현재 집에서 제거
        /// </summary>
        public bool RemoveUnit(UnitInstance unit)
        {
            if (unit == null || string.IsNullOrEmpty(unit.houseId)) return false;

            var house = GetHouseById(unit.houseId);
            if (house == null) return false;

            return RemoveUnitFromHouse(unit, house);
        }

        /// <summary>
        /// [E3] 유닛을 빈 슬롯에 자동 배치
        /// 이벤트로 영입된 유닛 등에 사용
        /// </summary>
        public bool AutoPlaceUnit(UnitInstance unit)
        {
            if (State == null || unit == null) return false;

            // 모든 집에서 빈 슬롯 찾기
            foreach (var house in State.houses)
            {
                if (unit.stage == GrowthStage.Child)
                {
                    // 아이: 아이 슬롯만
                    if (IsSlotEmpty(house, HouseSlotType.Child))
                    {
                        if (PlaceUnit(unit, house, HouseSlotType.Child))
                        {
                            Debug.Log($"[HouseSystem] 자동 배치 성공: {unit.unitName} -> {house.houseName}.Child");
                            return true;
                        }
                    }
                }
                else
                {
                    // 성인: AdultA -> AdultB 순서
                    if (IsSlotEmpty(house, HouseSlotType.AdultA))
                    {
                        if (PlaceUnit(unit, house, HouseSlotType.AdultA))
                        {
                            Debug.Log($"[HouseSystem] 자동 배치 성공: {unit.unitName} -> {house.houseName}.AdultA");
                            return true;
                        }
                    }
                    if (IsSlotEmpty(house, HouseSlotType.AdultB))
                    {
                        if (PlaceUnit(unit, house, HouseSlotType.AdultB))
                        {
                            Debug.Log($"[HouseSystem] 자동 배치 성공: {unit.unitName} -> {house.houseName}.AdultB");
                            return true;
                        }
                    }
                }
            }

            Debug.LogWarning($"[HouseSystem] 자동 배치 실패: {unit.unitName} - 빈 슬롯 없음");
            return false;
        }

        /// <summary>
        /// 유닛을 다른 집으로 이동
        /// </summary>
        public bool MoveUnit(UnitInstance unit, HouseInstance newHouse, HouseSlotType newSlot)
        {
            if (unit == null || newHouse == null) return false;

            // 현재 집에서 임신 중이면 이동 불가
            if (!string.IsNullOrEmpty(unit.houseId))
            {
                var currentHouse = GetHouseById(unit.houseId);
                if (currentHouse != null && currentHouse.isPregnant)
                {
                    Debug.LogWarning($"[HouseSystem] 임신 중인 집에서 이동 불가: {unit.unitName}");
                    return false;
                }
            }

            // 새 집에 배치 (내부에서 기존 집 제거 처리)
            return PlaceUnit(unit, newHouse, newSlot);
        }

        // =====================================================================
        // 유닛 사망 처리
        // =====================================================================

        /// <summary>
        /// 유닛 사망 시 집 슬롯 정리
        /// </summary>
        private void HandleUnitDied(UnitInstance unit)
        {
            if (unit == null || string.IsNullOrEmpty(unit.houseId)) return;

            var house = GetHouseById(unit.houseId);
            if (house == null) return;

            Debug.Log($"[HouseSystem] 유닛 사망으로 슬롯 정리: {unit.unitName}");

            // 슬롯 비우기 (이벤트 없이 직접 처리 - 이미 사망 이벤트가 발생했으므로)
            HouseSlotType? slot = FindUnitSlot(house, unit.unitId);
            if (slot != null)
            {
                SetSlot(house, slot.Value, null);
            }

            // 임신 중이면 취소
            if (house.isPregnant)
            {
                CancelPregnancy(house);
            }

            // 교배 확률 카운터 리셋
            house.fertilityCounter = 0;
        }

        // =====================================================================
        // 임신 관리
        // =====================================================================

        /// <summary>
        /// 임신 시작
        /// </summary>
        public void StartPregnancy(HouseInstance house)
        {
            if (house == null || house.isPregnant) return;

            house.isPregnant = true;
            house.pregnancyTurns = 0;

            Debug.Log($"[HouseSystem] 임신 시작: {house.houseName}");
            OnPregnancyStarted?.Invoke(house);
        }

        /// <summary>
        /// 임신 취소
        /// </summary>
        public void CancelPregnancy(HouseInstance house)
        {
            if (house == null || !house.isPregnant) return;

            house.isPregnant = false;
            house.pregnancyTurns = 0;

            Debug.Log($"[HouseSystem] 임신 취소: {house.houseName}");
            OnPregnancyCancelled?.Invoke(house);
        }

        /// <summary>
        /// 출산 처리 (BreedingSystem에서 호출)
        /// </summary>
        public UnitInstance ProcessBirth(HouseInstance house)
        {
            if (house == null || !house.isPregnant) return null;

            // 유년 슬롯 비어있는지 확인
            if (!house.HasEmptyChildSlot())
            {
                Debug.LogWarning($"[HouseSystem] 유년 슬롯이 이미 차있음: {house.houseName}");
                return null;
            }

            // 유년 유닛 생성
            string newName = GenerateUnitName();
            // [버그수정] 유년은 카드 없이 생성 - 전직(직업선택) 시 부여
            var newUnit = UnitSystem.Instance?.CreateUnit(newName, Job.Pawn, GrowthStage.Child, grantStartingCards: false);
            if (newUnit == null) return null;

            // 유년 슬롯에 배치
            PlaceUnit(newUnit, house, HouseSlotType.Child);

            // 임신 상태 초기화
            house.isPregnant = false;
            house.pregnancyTurns = 0;
            house.fertilityCounter = 0;

            Debug.Log($"[HouseSystem] 출산: {house.houseName} -> {newName}");
            OnBirth?.Invoke(house, newUnit);

            return newUnit;
        }

        /// <summary>
        /// 유닛 이름 생성
        /// </summary>
        private string GenerateUnitName()
        {
            int count = State?.units.Count ?? 0;
            char letter = (char)('A' + (count % 26));
            int number = count / 26;

            if (number == 0)
                return $"주민 {letter}";
            else
                return $"주민 {letter}{number + 1}";
        }

        // =====================================================================
        // 슬롯 유틸리티
        // =====================================================================

        /// <summary>
        /// 슬롯이 유닛에게 적합한지 검사
        /// </summary>
        private bool IsSlotValidForUnit(UnitInstance unit, HouseSlotType slot)
        {
            if (unit.stage == GrowthStage.Child)
            {
                // 유년은 유년 슬롯만
                return slot == HouseSlotType.Child;
            }
            else
            {
                // 청년/중년/노년은 어른 슬롯만
                return slot == HouseSlotType.AdultA || slot == HouseSlotType.AdultB;
            }
        }

        /// <summary>
        /// 슬롯이 비어있는지 검사
        /// </summary>
        private bool IsSlotEmpty(HouseInstance house, HouseSlotType slot)
        {
            string unitId = GetSlot(house, slot);
            return string.IsNullOrEmpty(unitId);
        }

        /// <summary>
        /// 슬롯 값 가져오기
        /// </summary>
        private string GetSlot(HouseInstance house, HouseSlotType slot)
        {
            switch (slot)
            {
                case HouseSlotType.AdultA: return house.adultSlotA;
                case HouseSlotType.AdultB: return house.adultSlotB;
                case HouseSlotType.Child: return house.childSlot;
                default: return null;
            }
        }

        /// <summary>
        /// 슬롯 값 설정
        /// </summary>
        private void SetSlot(HouseInstance house, HouseSlotType slot, string unitId)
        {
            switch (slot)
            {
                case HouseSlotType.AdultA:
                    house.adultSlotA = unitId;
                    break;
                case HouseSlotType.AdultB:
                    house.adultSlotB = unitId;
                    break;
                case HouseSlotType.Child:
                    house.childSlot = unitId;
                    break;
            }
        }

        /// <summary>
        /// 유닛이 어느 슬롯에 있는지 찾기
        /// </summary>
        private HouseSlotType? FindUnitSlot(HouseInstance house, string unitId)
        {
            if (house.adultSlotA == unitId) return HouseSlotType.AdultA;
            if (house.adultSlotB == unitId) return HouseSlotType.AdultB;
            if (house.childSlot == unitId) return HouseSlotType.Child;
            return null;
        }

        // =====================================================================
        // 조회 유틸리티
        // =====================================================================

        /// <summary>
        /// ID로 집 찾기
        /// </summary>
        public HouseInstance GetHouseById(string houseId)
        {
            return State?.houses.Find(h => h.houseId == houseId);
        }

        /// <summary>
        /// 유닛이 있는 집 찾기
        /// </summary>
        public HouseInstance GetHouseByUnit(UnitInstance unit)
        {
            if (unit == null || string.IsNullOrEmpty(unit.houseId)) return null;
            return GetHouseById(unit.houseId);
        }

        /// <summary>
        /// 빈 어른 슬롯이 있는 집 목록
        /// </summary>
        public List<HouseInstance> GetHousesWithEmptyAdultSlot()
        {
            if (State == null) return new List<HouseInstance>();

            return State.houses.FindAll(h =>
                string.IsNullOrEmpty(h.adultSlotA) ||
                string.IsNullOrEmpty(h.adultSlotB)
            );
        }

        /// <summary>
        /// 빈 유년 슬롯이 있는 집 목록
        /// </summary>
        public List<HouseInstance> GetHousesWithEmptyChildSlot()
        {
            if (State == null) return new List<HouseInstance>();

            return State.houses.FindAll(h => h.HasEmptyChildSlot());
        }

        /// <summary>
        /// 교배 가능한 집 목록 (어른 2명 + 유년 슬롯 빔)
        /// </summary>
        public List<HouseInstance> GetBreedableHouses()
        {
            if (State == null) return new List<HouseInstance>();

            var result = new List<HouseInstance>();

            foreach (var house in State.houses)
            {
                if (!house.HasBothAdults()) continue;
                if (!house.HasEmptyChildSlot()) continue;

                // 두 어른이 모두 교배 가능한지 확인
                var adultA = UnitSystem.Instance?.GetUnitById(house.adultSlotA);
                var adultB = UnitSystem.Instance?.GetUnitById(house.adultSlotB);

                if (adultA != null && adultB != null &&
                    adultA.CanBreed() && adultB.CanBreed())
                {
                    result.Add(house);
                }
            }

            return result;
        }

        /// <summary>
        /// 특정 집의 거주 유닛 목록
        /// </summary>
        public List<UnitInstance> GetResidents(HouseInstance house)
        {
            var residents = new List<UnitInstance>();
            if (house == null) return residents;

            if (!string.IsNullOrEmpty(house.adultSlotA))
            {
                var unit = UnitSystem.Instance?.GetUnitById(house.adultSlotA);
                if (unit != null) residents.Add(unit);
            }

            if (!string.IsNullOrEmpty(house.adultSlotB))
            {
                var unit = UnitSystem.Instance?.GetUnitById(house.adultSlotB);
                if (unit != null) residents.Add(unit);
            }

            if (!string.IsNullOrEmpty(house.childSlot))
            {
                var unit = UnitSystem.Instance?.GetUnitById(house.childSlot);
                if (unit != null) residents.Add(unit);
            }

            return residents;
        }

        // =====================================================================
        // 디버그
        // =====================================================================

        /// <summary>
        /// 모든 집 상태 로그
        /// </summary>
        [ContextMenu("Log All Houses")]
        public void LogAllHouses()
        {
            if (State == null || State.houses.Count == 0)
            {
                Debug.Log("[HouseSystem] 집 없음");
                return;
            }

            Debug.Log($"=== 집 현황 ({State.houses.Count}개) ===");

            foreach (var house in State.houses)
            {
                var residents = GetResidents(house);
                string residentNames = residents.Count > 0
                    ? string.Join(", ", residents.ConvertAll(u => u.unitName))
                    : "(비어있음)";

                string pregnancyStatus = house.isPregnant
                    ? $"임신 중 ({house.pregnancyTurns}/{GameConfig.PregnancyDuration}턴)"
                    : $"교배 카운터: {house.fertilityCounter}";

                Debug.Log($"  {house.houseName}: {residentNames} | {pregnancyStatus}");
            }
        }

        /// <summary>
        /// 사유지 개발 시 집 동기화
        /// </summary>
        private void OnLandDeveloped(LandInstance land, LandType newType, int newLevel)
        {
            SyncHousesWithLandBonus();
        }

        /// <summary>
        /// 사유지 획득 시 집 동기화
        /// </summary>
        private void OnLandAcquired(LandInstance land)
        {
            SyncHousesWithLandBonus();
        }

        /// <summary>
        /// 사유지 집 보너스에 맞게 집 개수 동기화
        /// 보너스가 현재 집 수보다 많으면 새 집 생성
        /// </summary>
        public void SyncHousesWithLandBonus()
        {
            if (State == null) return;

            int totalHouseBonus = LandSystem.Instance?.GetTotalHouseBonus() ?? 0;
            int currentHouses = State.houses.Count;

            if (totalHouseBonus > currentHouses)
            {
                int housesToCreate = totalHouseBonus - currentHouses;
                Debug.Log($"[HouseSystem] 사유지 보너스로 집 {housesToCreate}개 추가 생성 (보너스: {totalHouseBonus}, 현재: {currentHouses})");

                for (int i = 0; i < housesToCreate; i++)
                {
                    CreateHouse();
                }
            }
        }
    }
}