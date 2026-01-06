// =============================================================================
// UnitDetailPopup.cs
// 유닛 상세 정보 팝업
// =============================================================================
//
// [역할]
// - 유닛 카드 클릭 시 상세 정보 표시
// - 종속 카드 목록 표시
// - 전직 버튼 제공 (구매 페이즈에서만 활성)
// - [R8-7] 재배치 버튼 추가 (구매 페이즈에서만 활성)
//
// [표시 정보]
// - 이름, 직업, 성장 단계, 잔여 턴
// - 전직 단계 (0~3)
// - 다음 전직 비용
// - 전투력 (기본 + 보너스 분해) (R8-4)
// - 충성도 (현재/최대 + 위험 경고) (R8-4)
// - 교배 상태 (R8-4)
// - 사망 확률 (노년)
// - 종속 카드 목록
//
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 유닛 상세 정보 팝업
    /// </summary>
    public class UnitDetailPopup : MonoBehaviour
    {
        // =====================================================================
        // Inspector 연결
        // =====================================================================

        [Header("팝업 요소")]
        [SerializeField] private GameObject popupRoot;          // 팝업 루트 (On/Off용)
        [SerializeField] private Image dimBackground;           // 배경 딤 처리

        [Header("헤더")]
        [SerializeField] private TMP_Text titleText;            // "주민 A 상세"

        [Header("기본 정보")]
        [SerializeField] private TMP_Text jobText;              // "직업: 폰"
        [SerializeField] private TMP_Text stageText;            // "단계: 청년 (7턴 남음)"
        [SerializeField] private TMP_Text promotionLevelText;   // "전직 단계: Lv.2/3"
        [SerializeField] private TMP_Text promotionCostText;    // "다음 전직 비용: 25 골드"
        [SerializeField] private TMP_Text deathChanceText;      // "사망 확률: 40%" (노년만)

        [Header("전투/충성도 정보 (R8-4)")]
        [SerializeField] private TMP_Text combatPowerText;      // "전투력: 15 (기본 10 + 전직 5)"
        [SerializeField] private TMP_Text loyaltyText;          // "충성도: 80/100"
        [SerializeField] private TMP_Text breedingStatusText;   // "교배 가능" 또는 사유

        [Header("종속 카드")]
        [SerializeField] private TMP_Text cardListHeader;       // "─── 종속 카드 (3장) ───"
        [SerializeField] private Transform cardListContainer;   // 카드 목록 컨테이너
        [SerializeField] private GameObject cardListItemPrefab; // 카드 항목 프리팹

        [Header("버튼")]
        [SerializeField] private Button promotionButton;        // 전직 버튼
        [SerializeField] private TMP_Text promotionButtonText;  // 버튼 텍스트
        [SerializeField] private Button relocateButton;         // [R8-7] 재배치 버튼
        [SerializeField] private TMP_Text relocateButtonText;   // [R8-7] 재배치 버튼 텍스트
        [SerializeField] private Button closeButton;            // 닫기 버튼

        [Header("색상 - 버튼")]
        [SerializeField] private Color canPromoteColor = new Color(0.2f, 0.6f, 0.2f);   // 전직 가능
        [SerializeField] private Color cannotPromoteColor = new Color(0.5f, 0.5f, 0.5f); // 전직 불가
        [SerializeField] private Color canRelocateColor = new Color(0.2f, 0.4f, 0.7f);  // [R8-7] 재배치 가능
        [SerializeField] private Color cannotRelocateColor = new Color(0.5f, 0.5f, 0.5f); // [R8-7] 재배치 불가

        [Header("색상 - 충성도 (R8-4)")]
        [SerializeField] private Color loyaltyHighColor = new Color(0.3f, 0.9f, 0.3f);  // 80+ 녹색
        [SerializeField] private Color loyaltyMidColor = new Color(0.9f, 0.9f, 0.3f);   // 50~79 노란색
        [SerializeField] private Color loyaltyLowColor = new Color(0.9f, 0.3f, 0.3f);   // 50 미만 빨간색

        [Header("색상 - 교배 상태 (R8-4)")]
        [SerializeField] private Color breedableColor = new Color(1f, 0.5f, 0.7f);      // 분홍색
        [SerializeField] private Color notBreedableColor = new Color(0.6f, 0.6f, 0.6f); // 회색

        [Header("재배치 팝업 참조 (R8-7)")]
        [SerializeField] private RelocatePopup relocatePopup;   // RelocatePopup 참조

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private UnitInstance currentUnit;
        private List<GameObject> cardListItems = new List<GameObject>();

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>
        /// 전직 버튼 클릭됨 (유닛 전달)
        /// </summary>
        public static event System.Action<UnitInstance> OnPromotionRequested;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            // 버튼 이벤트 연결
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }

            if (promotionButton != null)
            {
                promotionButton.onClick.AddListener(OnPromotionButtonClicked);
            }

            // [R8-7] 재배치 버튼 이벤트 연결
            if (relocateButton != null)
            {
                relocateButton.onClick.AddListener(OnRelocateButtonClicked);
            }

            // DimBackground 클릭 시 닫기 (Button 컴포넌트 사용)
            if (dimBackground != null)
            {
                var dimButton = dimBackground.GetComponent<Button>();
                if (dimButton == null)
                {
                    dimButton = dimBackground.gameObject.AddComponent<Button>();
                    dimButton.transition = Selectable.Transition.None; // 시각 효과 없음
                }
                dimButton.onClick.AddListener(Hide);
            }

            // 초기 상태: 숨김
            if (dimBackground != null)
            {
                dimBackground.gameObject.SetActive(false);
            }
            if (popupRoot != null)
            {
                popupRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            // UnitCard 클릭 이벤트 구독
            UnitCard.OnUnitCardClicked += Show;

            // 페이즈 변경 이벤트 구독 (버튼 상태 갱신용)
            TurnManager.OnPhaseChanged += OnPhaseChanged;

            // 전직 완료 시 팝업 닫기
            UpgradePopup.OnPromotionCompleted += Hide;

            // [R8-7] 재배치 완료 시 팝업 닫기
            RelocatePopup.OnRelocateCompleted += OnRelocateCompleted;
        }

        private void OnDisable()
        {
            UnitCard.OnUnitCardClicked -= Show;
            TurnManager.OnPhaseChanged -= OnPhaseChanged;
            UpgradePopup.OnPromotionCompleted -= Hide;
            RelocatePopup.OnRelocateCompleted -= OnRelocateCompleted;
        }

        // =====================================================================
        // 표시/숨김
        // =====================================================================

        /// <summary>
        /// 팝업 표시
        /// </summary>
        public void Show(UnitInstance unit)
        {
            if (unit == null) return;

            currentUnit = unit;

            // 팝업 활성화
            if (dimBackground != null)
            {
                dimBackground.gameObject.SetActive(true);
            }
            if (popupRoot != null)
            {
                popupRoot.SetActive(true);
            }

            // 정보 갱신
            RefreshDisplay();

            Debug.Log($"[UnitDetailPopup] 표시: {unit.unitName}");
        }

        /// <summary>
        /// 팝업 숨김
        /// </summary>
        public void Hide()
        {
            if (dimBackground != null)
            {
                dimBackground.gameObject.SetActive(false);
            }
            if (popupRoot != null)
            {
                popupRoot.SetActive(false);
            }

            currentUnit = null;

            Debug.Log("[UnitDetailPopup] 숨김");
        }

        // =====================================================================
        // 정보 갱신
        // =====================================================================

        /// <summary>
        /// 전체 표시 갱신
        /// </summary>
        private void RefreshDisplay()
        {
            if (currentUnit == null) return;

            UpdateHeader();
            UpdateBasicInfo();
            UpdateCombatInfo();
            UpdateCardList();
            UpdatePromotionButton();
            UpdateRelocateButton(); // [R8-7]
        }

        /// <summary>
        /// 헤더 갱신
        /// </summary>
        private void UpdateHeader()
        {
            if (titleText != null)
            {
                titleText.text = $"{currentUnit.unitName} 상세";
            }
        }

        /// <summary>
        /// 기본 정보 갱신
        /// </summary>
        private void UpdateBasicInfo()
        {
            // 직업
            if (jobText != null)
            {
                string jobName = GetJobDisplayName(currentUnit.job);
                jobText.text = $"직업: {jobName}";
            }

            // 성장 단계 + 잔여 턴
            if (stageText != null)
            {
                string stageName = GetStageDisplayName(currentUnit.stage);
                stageText.text = $"단계: {stageName} ({currentUnit.stageRemainingTurns}턴 남음)";
            }

            // 전직 단계
            if (promotionLevelText != null)
            {
                promotionLevelText.text = $"전직 단계: Lv.{currentUnit.promotionLevel}/3";
            }

            // 다음 전직 비용
            if (promotionCostText != null)
            {
                if (currentUnit.promotionLevel >= 3)
                {
                    promotionCostText.text = "전직 완료 (최대)";
                }
                else
                {
                    int cost = currentUnit.GetNextPromotionCost();
                    promotionCostText.text = $"다음 전직 비용: {cost} 골드";
                }
            }

            // 사망 확률 (노년만)
            if (deathChanceText != null)
            {
                if (currentUnit.stage == GrowthStage.Old)
                {
                    int chance = currentUnit.GetDeathChance();
                    deathChanceText.text = $"사망 확률: {chance}%";
                    deathChanceText.gameObject.SetActive(true);
                }
                else
                {
                    deathChanceText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 전투/충성도 정보 갱신 (R8-4)
        /// </summary>
        private void UpdateCombatInfo()
        {
            // 전투력
            if (combatPowerText != null)
            {
                if (currentUnit.stage == GrowthStage.Child)
                {
                    combatPowerText.text = "전투력: 0 (유년)";
                }
                else
                {
                    int basePower = GameConfig.GetJobBaseCombatPower(currentUnit.job);
                    int promotionBonus = currentUnit.promotionLevel * 10;
                    float ageFactor = (currentUnit.stage == GrowthStage.Old) ? 0.75f : 1.0f;
                    int total = currentUnit.combatPower;

                    if (currentUnit.stage == GrowthStage.Old)
                    {
                        combatPowerText.text = $"전투력: {total} (기본 {basePower} + 전직 {promotionBonus}) × 75%";
                    }
                    else
                    {
                        combatPowerText.text = $"전투력: {total} (기본 {basePower} + 전직 {promotionBonus})";
                    }
                }
            }

            // 충성도
            if (loyaltyText != null)
            {
                int max = (currentUnit.stage == GrowthStage.Child) ? 50 : 100;
                loyaltyText.text = $"충성도: {currentUnit.loyalty}/{max}";

                // 색상 적용
                if (currentUnit.loyalty >= 80)
                {
                    loyaltyText.color = loyaltyHighColor;
                }
                else if (currentUnit.loyalty >= 50)
                {
                    loyaltyText.color = loyaltyMidColor;
                }
                else
                {
                    loyaltyText.color = loyaltyLowColor;
                }
            }

            // 교배 상태
            if (breedingStatusText != null)
            {
                var (canBreed, statusText) = GetBreedingStatus();
                breedingStatusText.text = $"교배: {statusText}";
                breedingStatusText.color = canBreed ? breedableColor : notBreedableColor;
            }
        }

        /// <summary>
        /// 교배 상태 확인 (R8-4)
        /// </summary>
        private (bool canBreed, string status) GetBreedingStatus()
        {
            if (currentUnit.stage == GrowthStage.Child)
            {
                return (false, "불가 (유년)");
            }

            if (currentUnit.stage == GrowthStage.Old)
            {
                return (false, "불가 (노년)");
            }

            if (!currentUnit.CanBreed())
            {
                return (false, "불가 (중년 후반)");
            }

            // 임신 중인지 확인
            if (!string.IsNullOrEmpty(currentUnit.houseId))
            {
                var house = HouseSystem.Instance?.GetHouseById(currentUnit.houseId);
                if (house != null && house.isPregnant)
                {
                    int remainingTurns = GameConfig.PregnancyDuration - house.pregnancyTurns;
                    return (false, $"임신 중 ({remainingTurns}턴 남음)");
                }
            }

            return (true, "교배 가능");
        }

        /// <summary>
        /// 종속 카드 목록 갱신
        /// </summary>
        private void UpdateCardList()
        {
            // 기존 항목 제거
            ClearCardList();

            // 헤더 갱신
            int cardCount = currentUnit.ownedCardIds?.Count ?? 0;
            if (cardListHeader != null)
            {
                cardListHeader.text = $"─── 종속 카드 ({cardCount}장) ───";
            }

            // 카드 목록 없으면 종료
            if (cardListContainer == null || cardListItemPrefab == null) return;
            if (currentUnit.ownedCardIds == null || currentUnit.ownedCardIds.Count == 0) return;

            // 카드 항목 생성
            foreach (string cardInstanceId in currentUnit.ownedCardIds)
            {
                // 카드 인스턴스 찾기
                var cardInstance = FindCardInstance(cardInstanceId);
                if (cardInstance == null) continue;

                // 카드 정의 가져오기
                var cardDef = DataLoader.Instance?.GetCard(cardInstance.cardDataId);
                if (cardDef == null) continue;

                // 항목 생성
                var itemObj = Instantiate(cardListItemPrefab, cardListContainer);
                var itemText = itemObj.GetComponent<TMP_Text>();

                if (itemText != null)
                {
                    string gradeName = GetCardGradeDisplayName(cardDef);
                    itemText.text = $"• {cardDef.cardName} [{gradeName}]";
                }

                cardListItems.Add(itemObj);
            }
        }

        /// <summary>
        /// 카드 목록 항목 제거
        /// </summary>
        private void ClearCardList()
        {
            foreach (var item in cardListItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }
            cardListItems.Clear();
        }

        /// <summary>
        /// 전직 버튼 상태 갱신
        /// </summary>
        private void UpdatePromotionButton()
        {
            if (promotionButton == null) return;

            // 구매 페이즈인지 확인
            bool isBuyPhase = TurnManager.Instance?.CurrentPhase == GamePhase.Purchase;

            // 전직 가능 여부 확인
            bool canPromote = UnitSystem.Instance?.CanPromoteUnit(currentUnit) ?? false;

            // 버튼 활성화
            promotionButton.interactable = isBuyPhase && canPromote;

            // 버튼 색상
            var buttonImage = promotionButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = (isBuyPhase && canPromote) ? canPromoteColor : cannotPromoteColor;
            }

            // 버튼 텍스트
            if (promotionButtonText != null)
            {
                if (!isBuyPhase)
                {
                    promotionButtonText.text = "구매 페이즈에서 전직";
                }
                else if (currentUnit.stage == GrowthStage.Child)
                {
                    promotionButtonText.text = "유년은 전직 불가";
                }
                else if (currentUnit.stage == GrowthStage.Old)
                {
                    promotionButtonText.text = "노년은 전직 불가";
                }
                else if (currentUnit.promotionLevel >= 3)
                {
                    promotionButtonText.text = "최대 전직 (Lv.3)";
                }
                else if (currentUnit.promotedThisTurn)
                {
                    promotionButtonText.text = "이번 턴 전직 완료";
                }
                else if (currentUnit.hasDisease)
                {
                    promotionButtonText.text = "질병으로 전직 불가";
                }
                else if (!canPromote)
                {
                    promotionButtonText.text = "골드 부족";
                }
                else
                {
                    int cost = UnitSystem.Instance?.GetPromotionCost(currentUnit) ?? 0;
                    promotionButtonText.text = $"전직하기 ({cost}G)";
                }
            }
        }

        /// <summary>
        /// [R8-7] 재배치 버튼 상태 갱신
        /// </summary>
        private void UpdateRelocateButton()
        {
            if (relocateButton == null) return;

            // 구매 페이즈인지 확인
            bool isBuyPhase = TurnManager.Instance?.CurrentPhase == GamePhase.Purchase;

            // 재배치 가능 여부 확인
            string errorMessage;
            bool canRelocate = CanRelocateUnit(currentUnit, out errorMessage);

            // 버튼 활성화
            relocateButton.interactable = isBuyPhase && canRelocate;

            // 버튼 색상
            var buttonImage = relocateButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = (isBuyPhase && canRelocate) ? canRelocateColor : cannotRelocateColor;
            }

            // 버튼 텍스트
            if (relocateButtonText != null)
            {
                if (!isBuyPhase)
                {
                    relocateButtonText.text = "구매 페이즈에서 재배치";
                }
                else if (!canRelocate && !string.IsNullOrEmpty(errorMessage))
                {
                    relocateButtonText.text = errorMessage;
                }
                else
                {
                    relocateButtonText.text = "재배치하기";
                }
            }
        }

        /// <summary>
        /// [R8-7] 재배치 가능 여부 확인 (스왑 포함)
        /// </summary>
        private bool CanRelocateUnit(UnitInstance unit, out string errorMessage)
        {
            errorMessage = null;

            if (unit == null)
            {
                errorMessage = "유닛 없음";
                return false;
            }

            // 유년은 재배치 불가
            if (unit.stage == GrowthStage.Child)
            {
                errorMessage = "유년은 재배치 불가";
                return false;
            }

            // 집 확인
            var house = HouseSystem.Instance?.GetHouseByUnit(unit);
            if (house == null)
            {
                errorMessage = "소속 집 없음";
                return false;
            }

            // 출발 집이 임신 중이면 불가
            if (house.isPregnant)
            {
                errorMessage = "임신 중 이동 불가";
                return false;
            }

            // 이동 가능한 슬롯이 있는지 확인 (스왑 포함)
            if (!HasAvailableSlot(unit, house))
            {
                errorMessage = "이동 가능한 슬롯 없음";
                return false;
            }

            return true;
        }

        /// <summary>
        /// [R8-7] 이동 가능한 슬롯이 있는지 확인 (빈 슬롯 + 스왑 가능 슬롯)
        /// </summary>
        private bool HasAvailableSlot(UnitInstance unit, HouseInstance sourceHouse)
        {
            var state = GameManager.Instance?.State;
            if (state == null) return false;

            foreach (var house in state.houses)
            {
                // 같은 집 제외
                if (house.houseId == sourceHouse.houseId) continue;

                // 임신 중인 집 제외
                if (house.isPregnant) continue;

                // 어른 슬롯 A 또는 B가 있으면 이동 가능 (빈 슬롯이든 스왑이든)
                // 스왑이므로 슬롯이 비어있지 않아도 됨
                return true;
            }

            return false;
        }

        // =====================================================================
        // 이벤트 핸들러
        // =====================================================================

        /// <summary>
        /// 전직 버튼 클릭
        /// </summary>
        private void OnPromotionButtonClicked()
        {
            if (currentUnit == null) return;

            Debug.Log($"[UnitDetailPopup] 전직 요청: {currentUnit.unitName}");
            OnPromotionRequested?.Invoke(currentUnit);
        }

        /// <summary>
        /// [R8-7] 재배치 버튼 클릭
        /// </summary>
        private void OnRelocateButtonClicked()
        {
            if (currentUnit == null) return;

            // ★ 중요: Hide() 전에 유닛 참조 저장
            var unitToRelocate = currentUnit;

            Debug.Log($"[UnitDetailPopup] 재배치 요청: {unitToRelocate.unitName}");

            // RelocatePopup 찾기
            RelocatePopup popup = relocatePopup;

            if (popup == null)
            {
                // Inspector에서 연결 안 됐으면 FindObjectOfType 시도 (비활성화 포함)
                popup = FindFirstObjectByType<RelocatePopup>(FindObjectsInactive.Include);
            }

            if (popup != null)
            {
                Hide();  // 현재 팝업 닫기 (currentUnit = null 됨)
                popup.Show(unitToRelocate);  // 저장된 참조 사용
            }
            else
            {
                Debug.LogWarning("[UnitDetailPopup] RelocatePopup을 찾을 수 없음! 씬에 추가해주세요.");
            }
        }

        /// <summary>
        /// [R8-7] 재배치 완료됨
        /// </summary>
        private void OnRelocateCompleted(UnitInstance unit, HouseInstance house, HouseSlotType slot)
        {
            // 팝업이 열려있으면 닫기 (이미 Hide()된 상태일 수 있음)
            Hide();
        }

        /// <summary>
        /// 페이즈 변경됨 - 버튼 상태 갱신
        /// </summary>
        private void OnPhaseChanged(GamePhase newPhase)
        {
            if (currentUnit != null && popupRoot != null && popupRoot.activeSelf)
            {
                UpdatePromotionButton();
                UpdateRelocateButton(); // [R8-7]
            }
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        /// <summary>
        /// 카드 인스턴스 찾기
        /// </summary>
        private CardInstance FindCardInstance(string cardInstanceId)
        {
            var state = GameManager.Instance?.State;
            if (state == null) return null;

            // 모든 영역에서 검색
            var allCards = new List<CardInstance>();
            allCards.AddRange(state.deck);
            allCards.AddRange(state.hand);
            allCards.AddRange(state.discardPile);
            allCards.AddRange(state.playArea);

            return allCards.Find(c => c.instanceId == cardInstanceId);
        }

        /// <summary>
        /// 직업 표시 이름
        /// </summary>
        private string GetJobDisplayName(Job job)
        {
            return job switch
            {
                Job.Pawn => "폰",
                Job.Knight => "나이트",
                Job.Bishop => "비숍",
                Job.Rook => "룩",
                Job.Queen => "퀸",
                _ => "???"
            };
        }

        /// <summary>
        /// 성장 단계 표시 이름
        /// </summary>
        private string GetStageDisplayName(GrowthStage stage)
        {
            return stage switch
            {
                GrowthStage.Child => "유년",
                GrowthStage.Young => "청년",
                GrowthStage.Middle => "중년",
                GrowthStage.Old => "노년",
                _ => "???"
            };
        }

        /// <summary>
        /// 카드 등급 표시 이름 (카드 타입에 따라 다름)
        /// </summary>
        private string GetCardGradeDisplayName(CardData cardDef)
        {
            if (cardDef == null) return "?";

            // 카드 타입에 따라 등급 표시
            switch (cardDef.cardType)
            {
                case CardType.Treasure:
                    return TreasureGradeUtil.GetName(cardDef.treasureGrade);

                case CardType.Action:
                    return cardDef.rarity switch
                    {
                        CardRarity.Basic => "기본",
                        CardRarity.Advanced => "고급",
                        CardRarity.Rare => "희귀",
                        CardRarity.SuperRare => "초희귀",
                        CardRarity.Legendary => "전설",
                        _ => "?"
                    };

                case CardType.Pollution:
                    return "오염";

                default:
                    return "?";
            }
        }
    }
}