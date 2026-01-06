// =============================================================================
// RelocatePopup.cs
// 유닛 재배치 팝업
// =============================================================================
// [R8-7 신규] 2026-01-03
// - 유닛을 다른 집의 슬롯으로 이동
// - 버튼 선택 방식 UI
// - 스왑(교환) 지원: 점유된 슬롯 선택 시 자동 교환
// - ScrollRect로 많은 슬롯 수용
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 슬롯 정보 (UI 표시용)
    /// </summary>
    public struct SlotInfo
    {
        public HouseInstance house;
        public HouseSlotType slotType;
        public UnitInstance occupant;  // null이면 빈 슬롯
        public bool isSwap;            // 교환 여부

        public string GetDisplayText()
        {
            string slotName = slotType == HouseSlotType.AdultA ? "어른 A" : "어른 B";

            if (isSwap && occupant != null)
            {
                return $"{house.houseName} - {slotName} ({occupant.unitName}와 교환)";
            }
            else
            {
                return $"{house.houseName} - {slotName} (빈 슬롯)";
            }
        }
    }

    /// <summary>
    /// 유닛 재배치 팝업
    /// </summary>
    public class RelocatePopup : MonoBehaviour
    {
        // =====================================================================
        // Inspector 연결
        // =====================================================================

        [Header("팝업 요소")]
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private Image dimBackground;

        [Header("정보 표시")]
        [SerializeField] private TMP_Text titleText;           // "유닛 재배치"
        [SerializeField] private TMP_Text currentLocationText; // "현재 위치: 1번 집 - 어른 A"
        [SerializeField] private TMP_Text instructionText;     // "이동할 위치를 선택하세요"

        [Header("슬롯 버튼 목록 (ScrollRect 사용)")]
        [SerializeField] private ScrollRect scrollRect;        // 스크롤 뷰
        [SerializeField] private Transform slotButtonContainer; // Content (Vertical Layout)
        [SerializeField] private GameObject slotButtonPrefab;  // 슬롯 선택 버튼 프리팹

        [Header("하단 버튼")]
        [SerializeField] private Button cancelButton;

        [Header("색상")]
        [SerializeField] private Color emptySlotColor = new Color(0.3f, 0.6f, 0.3f);  // 빈 슬롯 (녹색)
        [SerializeField] private Color swapSlotColor = new Color(0.6f, 0.4f, 0.2f);   // 스왑 (주황색)

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private UnitInstance currentUnit;
        private HouseInstance sourceHouse;
        private HouseSlotType sourceSlot;
        private List<GameObject> slotButtons = new List<GameObject>();

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>재배치 완료됨 (유닛, 새 집, 새 슬롯)</summary>
        public static event Action<UnitInstance, HouseInstance, HouseSlotType> OnRelocateCompleted;

        /// <summary>재배치 취소됨</summary>
        public static event Action OnRelocateCancelled;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            // 버튼 이벤트 연결
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(Cancel);
            }

            // DimBackground 클릭 시 취소
            if (dimBackground != null)
            {
                var dimButton = dimBackground.GetComponent<Button>();
                if (dimButton == null)
                {
                    dimButton = dimBackground.gameObject.AddComponent<Button>();
                    dimButton.transition = Selectable.Transition.None;
                }
                dimButton.onClick.AddListener(Cancel);
            }

            // 초기 상태: 숨김
            Hide();
        }

        // =====================================================================
        // 표시/숨김
        // =====================================================================

        /// <summary>
        /// 팝업 표시
        /// </summary>
        public void Show(UnitInstance unit)
        {
            Debug.Log($"[RelocatePopup] Show() 호출됨, unit: {(unit != null ? unit.unitName : "null")}");

            if (unit == null)
            {
                Debug.LogWarning("[RelocatePopup] 유닛이 null");
                return;
            }

            // 기본 조건 체크 (스왑 포함하므로 슬롯 체크는 별도)
            string errorMessage;
            if (!CanRelocateBasic(unit, out errorMessage))
            {
                Debug.LogWarning($"[RelocatePopup] 재배치 불가: {errorMessage}");
                return;
            }

            currentUnit = unit;
            sourceHouse = HouseSystem.Instance?.GetHouseByUnit(unit);
            sourceSlot = FindCurrentSlot(unit, sourceHouse);

            // 이동 가능한 슬롯 확인
            var availableSlots = GetAvailableSlots(unit, sourceHouse);
            if (availableSlots.Count == 0)
            {
                Debug.LogWarning("[RelocatePopup] 이동 가능한 슬롯이 없습니다");
                return;
            }

            // 팝업 활성화
            if (dimBackground != null) dimBackground.gameObject.SetActive(true);
            if (popupRoot != null) popupRoot.SetActive(true);

            // 정보 갱신
            RefreshDisplay(availableSlots);

            // 스크롤 위치 초기화
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;  // 맨 위로
            }

            Debug.Log($"[RelocatePopup] 표시 완료: {unit.unitName}, 슬롯 {availableSlots.Count}개");
        }

        /// <summary>
        /// 팝업 숨김
        /// </summary>
        public void Hide()
        {
            if (dimBackground != null) dimBackground.gameObject.SetActive(false);
            if (popupRoot != null) popupRoot.SetActive(false);

            currentUnit = null;
            sourceHouse = null;

            ClearSlotButtons();
        }

        /// <summary>
        /// 취소
        /// </summary>
        public void Cancel()
        {
            Debug.Log("[RelocatePopup] 취소");
            Hide();
            OnRelocateCancelled?.Invoke();
        }

        // =====================================================================
        // 재배치 가능 여부 체크
        // =====================================================================

        /// <summary>
        /// 기본 재배치 가능 여부 확인 (슬롯 존재 여부 제외)
        /// </summary>
        private bool CanRelocateBasic(UnitInstance unit, out string errorMessage)
        {
            errorMessage = null;

            if (unit == null)
            {
                errorMessage = "유닛이 없습니다";
                return false;
            }

            // 유년은 재배치 불가
            if (unit.stage == GrowthStage.Child)
            {
                errorMessage = "유년은 재배치할 수 없습니다";
                return false;
            }

            // 집 확인
            var house = HouseSystem.Instance?.GetHouseByUnit(unit);
            if (house == null)
            {
                errorMessage = "소속 집이 없습니다";
                return false;
            }

            // 출발 집이 임신 중이면 불가
            if (house.isPregnant)
            {
                errorMessage = "임신 중인 집에서는 이동할 수 없습니다";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 외부에서 호출 가능한 재배치 가능 여부 (버튼 활성화용)
        /// </summary>
        public bool CanRelocate(UnitInstance unit, out string errorMessage)
        {
            if (!CanRelocateBasic(unit, out errorMessage))
            {
                return false;
            }

            var house = HouseSystem.Instance?.GetHouseByUnit(unit);
            var availableSlots = GetAvailableSlots(unit, house);

            if (availableSlots.Count == 0)
            {
                errorMessage = "이동 가능한 슬롯이 없습니다";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 현재 유닛의 슬롯 찾기
        /// </summary>
        private HouseSlotType FindCurrentSlot(UnitInstance unit, HouseInstance house)
        {
            if (house == null || unit == null) return HouseSlotType.AdultA;

            if (house.adultSlotA == unit.unitId) return HouseSlotType.AdultA;
            if (house.adultSlotB == unit.unitId) return HouseSlotType.AdultB;
            if (house.childSlot == unit.unitId) return HouseSlotType.Child;

            return HouseSlotType.AdultA;
        }

        /// <summary>
        /// 이동 가능한 슬롯 목록 반환 (빈 슬롯 + 스왑 가능 슬롯)
        /// </summary>
        private List<SlotInfo> GetAvailableSlots(UnitInstance unit, HouseInstance sourceHouse)
        {
            var result = new List<SlotInfo>();
            var state = GameManager.Instance?.State;
            if (state == null) return result;

            foreach (var house in state.houses)
            {
                // 같은 집 제외 (같은 집 내 이동 불허)
                if (house.houseId == sourceHouse.houseId) continue;

                // 임신 중인 집 제외
                if (house.isPregnant) continue;

                // 어른 슬롯 A 확인
                CheckAndAddSlot(result, house, HouseSlotType.AdultA, house.adultSlotA);

                // 어른 슬롯 B 확인
                CheckAndAddSlot(result, house, HouseSlotType.AdultB, house.adultSlotB);
            }

            return result;
        }

        /// <summary>
        /// 슬롯 확인 후 목록에 추가
        /// </summary>
        private void CheckAndAddSlot(List<SlotInfo> result, HouseInstance house,
            HouseSlotType slotType, string occupantId)
        {
            var slotInfo = new SlotInfo
            {
                house = house,
                slotType = slotType,
                occupant = null,
                isSwap = false
            };

            if (string.IsNullOrEmpty(occupantId))
            {
                // 빈 슬롯
                slotInfo.isSwap = false;
                result.Add(slotInfo);
            }
            else
            {
                // 점유된 슬롯 - 스왑 가능
                var occupant = UnitSystem.Instance?.GetUnitById(occupantId);
                if (occupant != null)
                {
                    slotInfo.occupant = occupant;
                    slotInfo.isSwap = true;
                    result.Add(slotInfo);
                }
            }
        }

        // =====================================================================
        // UI 갱신
        // =====================================================================

        /// <summary>
        /// 전체 표시 갱신
        /// </summary>
        private void RefreshDisplay(List<SlotInfo> availableSlots)
        {
            UpdateInfoTexts();
            CreateSlotButtons(availableSlots);
        }

        /// <summary>
        /// 정보 텍스트 갱신
        /// </summary>
        private void UpdateInfoTexts()
        {
            // 제목
            if (titleText != null)
            {
                titleText.text = $"{currentUnit.unitName} 재배치";
            }

            // 현재 위치
            if (currentLocationText != null && sourceHouse != null)
            {
                string slotName = GetSlotDisplayName(sourceSlot);
                currentLocationText.text = $"현재 위치: {sourceHouse.houseName} - {slotName}";
            }

            // 안내 문구
            if (instructionText != null)
            {
                instructionText.text = "이동할 위치를 선택하세요";
            }
        }

        /// <summary>
        /// 슬롯 표시 이름
        /// </summary>
        private string GetSlotDisplayName(HouseSlotType slot)
        {
            return slot switch
            {
                HouseSlotType.AdultA => "어른 A",
                HouseSlotType.AdultB => "어른 B",
                HouseSlotType.Child => "유년",
                _ => "???"
            };
        }

        /// <summary>
        /// 슬롯 버튼 생성
        /// </summary>
        private void CreateSlotButtons(List<SlotInfo> availableSlots)
        {
            ClearSlotButtons();

            if (slotButtonContainer == null || slotButtonPrefab == null) return;

            foreach (var slotInfo in availableSlots)
            {
                var buttonObj = Instantiate(slotButtonPrefab, slotButtonContainer);
                slotButtons.Add(buttonObj);

                // 버튼 텍스트 설정
                var buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                {
                    buttonText.text = slotInfo.GetDisplayText();
                }

                // 버튼 색상 설정
                var buttonImage = buttonObj.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = slotInfo.isSwap ? swapSlotColor : emptySlotColor;
                }

                // 버튼 클릭 이벤트
                var button = buttonObj.GetComponent<Button>();
                if (button != null)
                {
                    var capturedSlotInfo = slotInfo;  // 클로저용 캡처
                    button.onClick.AddListener(() => OnSlotSelected(capturedSlotInfo));
                }
            }

            Debug.Log($"[RelocatePopup] 슬롯 버튼 {availableSlots.Count}개 생성");
        }

        /// <summary>
        /// 슬롯 버튼 제거
        /// </summary>
        private void ClearSlotButtons()
        {
            foreach (var button in slotButtons)
            {
                if (button != null)
                {
                    Destroy(button);
                }
            }
            slotButtons.Clear();
        }

        // =====================================================================
        // 재배치 실행
        // =====================================================================

        /// <summary>
        /// 슬롯 선택됨
        /// </summary>
        private void OnSlotSelected(SlotInfo slotInfo)
        {
            if (currentUnit == null || sourceHouse == null)
            {
                Debug.LogWarning("[RelocatePopup] 상태 오류");
                Cancel();
                return;
            }

            Debug.Log($"[RelocatePopup] 슬롯 선택: {slotInfo.GetDisplayText()}");

            bool success;

            if (slotInfo.isSwap && slotInfo.occupant != null)
            {
                // 스왑 실행
                success = ExecuteSwap(currentUnit, sourceHouse, sourceSlot,
                    slotInfo.occupant, slotInfo.house, slotInfo.slotType);
            }
            else
            {
                // 단순 이동 실행
                success = ExecuteMove(currentUnit, sourceHouse, slotInfo.house, slotInfo.slotType);
            }

            if (success)
            {
                Debug.Log($"[RelocatePopup] 재배치 성공");
                OnRelocateCompleted?.Invoke(currentUnit, slotInfo.house, slotInfo.slotType);
            }
            else
            {
                Debug.LogWarning("[RelocatePopup] 재배치 실패");
            }

            Hide();
        }

        /// <summary>
        /// 단순 이동 실행 (빈 슬롯으로)
        /// </summary>
        private bool ExecuteMove(UnitInstance unit, HouseInstance fromHouse,
            HouseInstance toHouse, HouseSlotType toSlot)
        {
            if (HouseSystem.Instance == null) return false;

            Debug.Log($"[RelocatePopup] 이동 실행: {unit.unitName} → {toHouse.houseName}.{toSlot}");

            // 1. 기존 집에서 제거
            bool removed = HouseSystem.Instance.RemoveUnitFromHouse(unit, fromHouse);
            if (!removed)
            {
                Debug.LogWarning("[RelocatePopup] 기존 집에서 제거 실패");
                return false;
            }

            // 2. 새 집에 배치
            bool placed = HouseSystem.Instance.PlaceUnit(unit, toHouse, toSlot);
            if (!placed)
            {
                Debug.LogWarning("[RelocatePopup] 새 집에 배치 실패");
                return false;
            }

            // fertilityCounter는 RemoveUnitFromHouse와 PlaceUnit에서 이미 처리됨
            Debug.Log($"[RelocatePopup] 이동 완료");
            Debug.Log($"  - {fromHouse.houseName} fertilityCounter: {fromHouse.fertilityCounter}");
            Debug.Log($"  - {toHouse.houseName} fertilityCounter: {toHouse.fertilityCounter}");

            return true;
        }

        /// <summary>
        /// 스왑 실행 (점유된 슬롯과 교환)
        /// </summary>
        private bool ExecuteSwap(UnitInstance unitA, HouseInstance houseA, HouseSlotType slotA,
            UnitInstance unitB, HouseInstance houseB, HouseSlotType slotB)
        {
            if (HouseSystem.Instance == null) return false;

            Debug.Log($"[RelocatePopup] 스왑 실행: {unitA.unitName}({houseA.houseName}) ↔ {unitB.unitName}({houseB.houseName})");

            // 1. 양쪽 유닛을 집에서 제거 (houseId만 null로, 슬롯은 직접 처리)
            // 주의: RemoveUnitFromHouse는 임신 취소 등 부작용이 있으므로 직접 처리

            // 슬롯 비우기 (임신 취소 없이)
            ClearSlot(houseA, slotA);
            ClearSlot(houseB, slotB);

            // houseId 임시 제거
            unitA.houseId = null;
            unitB.houseId = null;

            // 2. 교차 배치
            SetSlot(houseB, slotB, unitA.unitId);
            unitA.houseId = houseB.houseId;

            SetSlot(houseA, slotA, unitB.unitId);
            unitB.houseId = houseA.houseId;

            // 3. 양쪽 집 fertilityCounter 초기화
            houseA.fertilityCounter = 0;
            houseB.fertilityCounter = 0;

            Debug.Log($"[RelocatePopup] 스왑 완료");
            Debug.Log($"  - {unitA.unitName} → {houseB.houseName}.{slotB}");
            Debug.Log($"  - {unitB.unitName} → {houseA.houseName}.{slotA}");
            Debug.Log($"  - {houseA.houseName} fertilityCounter: {houseA.fertilityCounter}");
            Debug.Log($"  - {houseB.houseName} fertilityCounter: {houseB.fertilityCounter}");

            return true;
        }

        /// <summary>
        /// 슬롯 비우기 (직접 처리)
        /// </summary>
        private void ClearSlot(HouseInstance house, HouseSlotType slot)
        {
            switch (slot)
            {
                case HouseSlotType.AdultA:
                    house.adultSlotA = null;
                    break;
                case HouseSlotType.AdultB:
                    house.adultSlotB = null;
                    break;
                case HouseSlotType.Child:
                    house.childSlot = null;
                    break;
            }
        }

        /// <summary>
        /// 슬롯 설정 (직접 처리)
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
    }
}