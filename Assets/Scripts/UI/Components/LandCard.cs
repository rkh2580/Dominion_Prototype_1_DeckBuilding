// =============================================================================
// LandCard.cs
// 개별 사유지 카드
// =============================================================================
// [R8-6 신규] 2026-01-03
// - HouseCard와 일관된 구조
// - 사유지 정보 표시 + 개발 버튼
// - Lv.0 개발 시 루트 선택 버튼 표시
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
    /// 개별 사유지 카드
    /// 
    /// 구조:
    /// LandCard
    /// ├── LandNameText ("농장 Lv.2")
    /// ├── BonusText ("+20% 골드")
    /// ├── CostText ("개발: 30G")
    /// ├── DevelopButton (개발 버튼)
    /// └── RouteButtonContainer (Lv.0 루트 선택용, 6개 버튼)
    /// </summary>
    public class LandCard : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("UI 요소")]
        [SerializeField] private TMP_Text landNameText;       // "빈 땅" / "농장 Lv.2"
        [SerializeField] private TMP_Text bonusText;          // "+20% 골드, +15 전투력"
        [SerializeField] private TMP_Text costText;           // "개발: 30G"
        [SerializeField] private Button developButton;        // 개발 버튼 (Lv.1 이상)
        [SerializeField] private TMP_Text developButtonText;  // 버튼 텍스트

        [Header("루트 선택 (Lv.0 전용)")]
        [SerializeField] private GameObject routeButtonContainer;  // 루트 버튼 컨테이너
        [SerializeField] private Button farmButton;           // 영지 루트
        [SerializeField] private Button villageButton;        // 거주 루트
        [SerializeField] private Button watchtowerButton;     // 군사 루트
        [SerializeField] private Button chapelButton;         // 신앙 루트
        [SerializeField] private Button studyButton;          // 학문 루트
        [SerializeField] private Button shopButton;           // 상업 루트

        [Header("배경")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color cardColor = new Color(0.4f, 0.7f, 0.4f, 1f);  // 항상 밝은 녹색

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private LandInstance boundLand;

        public LandInstance BoundLand => boundLand;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            SetupButtons();
        }

        private void SetupButtons()
        {
            // 메인 개발 버튼
            if (developButton != null)
            {
                developButton.onClick.AddListener(OnDevelopClicked);
            }

            // 루트 선택 버튼들
            if (farmButton != null)
                farmButton.onClick.AddListener(() => OnRouteSelected(LandType.Farm));
            if (villageButton != null)
                villageButton.onClick.AddListener(() => OnRouteSelected(LandType.Village));
            if (watchtowerButton != null)
                watchtowerButton.onClick.AddListener(() => OnRouteSelected(LandType.Watchtower));
            if (chapelButton != null)
                chapelButton.onClick.AddListener(() => OnRouteSelected(LandType.Chapel));
            if (studyButton != null)
                studyButton.onClick.AddListener(() => OnRouteSelected(LandType.Study));
            if (shopButton != null)
                shopButton.onClick.AddListener(() => OnRouteSelected(LandType.Shop));
        }

        // =====================================================================
        // 바인딩
        // =====================================================================

        /// <summary>
        /// 사유지 데이터 바인딩
        /// </summary>
        public void Bind(LandInstance land)
        {
            boundLand = land;
            Refresh();
        }

        /// <summary>
        /// UI 전체 갱신
        /// </summary>
        public void Refresh()
        {
            if (boundLand == null) return;

            UpdateName();
            UpdateBonus();
            UpdateCost();
            UpdateButtons();
            UpdateBackground();
        }

        /// <summary>
        /// 개발 버튼만 갱신 (골드 변경 시)
        /// </summary>
        public void RefreshDevelopButton()
        {
            UpdateButtons();
            UpdateBackground();  // 골드 변경 시 배경색도 갱신
        }

        // =====================================================================
        // UI 갱신 세부
        // =====================================================================

        private void UpdateName()
        {
            if (landNameText == null) return;

            string typeName = GetLandTypeName(boundLand.landType);

            if (boundLand.level == 0)
            {
                landNameText.text = "빈 땅";
            }
            else
            {
                landNameText.text = $"{typeName} Lv.{boundLand.level}";
            }
        }

        private void UpdateBonus()
        {
            if (bonusText == null) return;

            var bonuses = new List<string>();

            // 집 보너스
            int house = LandSystem.Instance?.GetHouseBonus(boundLand) ?? 0;
            if (house > 0) bonuses.Add($"집+{house}");

            // 골드 보너스
            int gold = LandSystem.Instance?.GetGoldBonusPercent(boundLand) ?? 0;
            if (gold > 0) bonuses.Add($"골드+{gold}%");

            // 전투력 보너스
            int combat = LandSystem.Instance?.GetCombatPowerBonus(boundLand) ?? 0;
            if (combat > 0) bonuses.Add($"전투력+{combat}");

            // 드로우 보너스
            int draw = LandSystem.Instance?.GetDrawBonus(boundLand) ?? 0;
            if (draw > 0) bonuses.Add($"드로우+{draw}");

            // 손패 상한 보너스
            int handSize = LandSystem.Instance?.GetHandSizeBonus(boundLand) ?? 0;
            if (handSize > 0) bonuses.Add($"손패+{handSize}");

            bonusText.text = bonuses.Count > 0 ? string.Join(", ", bonuses) : "-";
        }

        private void UpdateCost()
        {
            if (costText == null) return;

            if (boundLand.level >= 3)
            {
                costText.text = "최대 레벨";
            }
            else
            {
                int cost = LandSystem.Instance?.GetDevelopCost(boundLand) ?? 0;
                costText.text = $"개발: {cost}G";
            }
        }

        private void UpdateButtons()
        {
            bool isLevelZero = boundLand.level == 0;
            bool canDevelop = LandSystem.Instance?.CanDevelop(boundLand) ?? false;
            bool isMaxLevel = boundLand.level >= 3;

            // Lv.0: 루트 선택 버튼 표시, 메인 개발 버튼 숨김
            // Lv.1+: 메인 개발 버튼 표시, 루트 선택 숨김

            if (routeButtonContainer != null)
            {
                routeButtonContainer.SetActive(isLevelZero && !isMaxLevel);
            }

            if (developButton != null)
            {
                developButton.gameObject.SetActive(!isLevelZero && !isMaxLevel);
                developButton.interactable = canDevelop;
            }

            if (developButtonText != null)
            {
                developButtonText.text = canDevelop ? "개발" : "골드 부족";
            }

            // 루트 버튼 활성화 상태
            if (isLevelZero)
            {
                bool canAfford = canDevelop;
                SetRouteButtonInteractable(farmButton, canAfford);
                SetRouteButtonInteractable(villageButton, canAfford);
                SetRouteButtonInteractable(watchtowerButton, canAfford);
                SetRouteButtonInteractable(chapelButton, canAfford);
                SetRouteButtonInteractable(studyButton, canAfford);
                SetRouteButtonInteractable(shopButton, canAfford);
            }
        }

        private void SetRouteButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        private void UpdateBackground()
        {
            if (backgroundImage == null) return;
            backgroundImage.color = cardColor;
        }

        // =====================================================================
        // 버튼 클릭 핸들러
        // =====================================================================

        /// <summary>
        /// 메인 개발 버튼 클릭 (Lv.1 이상)
        /// </summary>
        private void OnDevelopClicked()
        {
            if (boundLand == null) return;
            if (boundLand.level == 0) return; // Lv.0은 루트 선택으로

            bool success = LandSystem.Instance?.DevelopLand(boundLand) ?? false;
            if (success)
            {
                Debug.Log($"[LandCard] 개발 성공: {boundLand.landName} -> Lv.{boundLand.level}");
                Refresh();
            }
        }

        /// <summary>
        /// 루트 선택 (Lv.0 전용)
        /// </summary>
        private void OnRouteSelected(LandType route)
        {
            if (boundLand == null) return;
            if (boundLand.level != 0) return; // Lv.0만

            bool success = LandSystem.Instance?.DevelopLand(boundLand, route) ?? false;
            if (success)
            {
                Debug.Log($"[LandCard] 루트 선택: {boundLand.landName} -> {route} Lv.1");
                Refresh();
            }
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        /// <summary>
        /// LandType → 한글 이름
        /// </summary>
        private string GetLandTypeName(LandType type)
        {
            return type switch
            {
                LandType.Empty => "빈 땅",
                LandType.Farm => "농장",
                LandType.Village => "촌락",
                LandType.Watchtower => "망루",
                LandType.Chapel => "예배당",
                LandType.Study => "서재",
                LandType.Shop => "상점",
                _ => "알 수 없음"
            };
        }

        /// <summary>
        /// LandType → 간단 설명
        /// </summary>
        public static string GetRouteDescription(LandType type)
        {
            return type switch
            {
                LandType.Farm => "골드 보너스",
                LandType.Village => "집 슬롯 증가",
                LandType.Watchtower => "전투력 증가",
                LandType.Chapel => "오염 처리",
                LandType.Study => "드로우 증가",
                LandType.Shop => "골드+드로우",
                _ => ""
            };
        }

        /// <summary>
        /// 사유지 ID 반환
        /// </summary>
        public string GetLandId() => boundLand?.landId;
    }
}