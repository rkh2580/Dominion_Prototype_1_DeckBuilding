// =============================================================================
// UnitCard.cs
// 개별 유닛 카드 UI 컴포넌트
// =============================================================================
//
// [역할]
// - 유닛 한 명의 정보를 카드 형태로 표시
// - 클릭 시 상세 팝업 표시 요청
//
// [표시 정보]
// - 이름
// - 직업 (아이콘 또는 텍스트)
// - 성장 단계 + 잔여 턴
// - 전직 단계 (Lv.0~3)
// - 전투력, 충성도 (R8-3 추가)
// - 상태 (질병, 이번 턴 전직 완료, 임신중, 교배 가능 등)
// - 노년일 경우 사망 확률
//
// [시각 구분]
// - 유년: 회색 배경, 점선 테두리
// - 청년: 초록 배경
// - 중년: 파랑 배경
// - 노년: 주황 배경 + 사망 확률 표시
// - 교배 가능: 분홍색 테두리 glow
//
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DeckBuildingEconomy.Data;
using DeckBuildingEconomy.Core;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 유닛 카드 UI 컴포넌트
    /// </summary>
    public class UnitCard : MonoBehaviour, IPointerClickHandler
    {
        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>
        /// 카드 클릭됨 (유닛 인스턴스 전달)
        /// </summary>
        public static event Action<UnitInstance> OnUnitCardClicked;

        // =====================================================================
        // Inspector 연결
        // =====================================================================

        [Header("기본 요소")]
        [SerializeField] private Image cardBackground;      // 카드 배경
        [SerializeField] private Image cardBorder;          // 카드 테두리
        [SerializeField] private TMP_Text nameText;         // 유닛 이름
        [SerializeField] private TMP_Text jobText;          // 직업 텍스트
        [SerializeField] private Image jobIcon;             // 직업 아이콘 (선택)

        [Header("상태 표시 - 기본")]
        [SerializeField] private TMP_Text stageText;        // "청년 7턴"
        [SerializeField] private TMP_Text promotionText;    // "Lv.2" (전직 단계)
        [SerializeField] private TMP_Text deathChanceText;  // "사망 40%" (노년 전용)
        [SerializeField] private GameObject diseaseBadge;   // 질병 배지
        [SerializeField] private GameObject promotedBadge;  // 이번 턴 전직 완료 배지

        [Header("상태 표시 - 신규 (R8-3)")]
        [SerializeField] private TMP_Text combatPowerText;  // "전투력: 15"
        [SerializeField] private TMP_Text loyaltyText;      // "충성도: 80"
        [SerializeField] private GameObject pregnantBadge;  // 임신중 배지
        [SerializeField] private Image breedableGlow;       // 교배 가능 glow (테두리 외곽)

        [Header("색상 설정 - 배경")]
        [SerializeField] private Color childColor = new Color(0.6f, 0.6f, 0.6f);    // 회색
        [SerializeField] private Color youngColor = new Color(0.3f, 0.7f, 0.3f);    // 초록
        [SerializeField] private Color middleColor = new Color(0.3f, 0.5f, 0.8f);   // 파랑
        [SerializeField] private Color oldColor = new Color(0.9f, 0.6f, 0.2f);      // 주황

        [Header("색상 설정 - 테두리")]
        [SerializeField] private Color normalBorderColor = Color.white;
        [SerializeField] private Color childBorderColor = new Color(0.5f, 0.5f, 0.5f);  // 점선 효과용
        [SerializeField] private Color breedableGlowColor = new Color(1f, 0.5f, 0.7f, 0.8f); // 분홍색

        [Header("색상 설정 - 충성도")]
        [SerializeField] private Color loyaltyHighColor = new Color(0.3f, 0.9f, 0.3f);  // 80+ 녹색
        [SerializeField] private Color loyaltyMidColor = new Color(0.9f, 0.9f, 0.3f);   // 50~79 노란색
        [SerializeField] private Color loyaltyLowColor = new Color(0.9f, 0.3f, 0.3f);   // 50 미만 빨간색

        [Header("색상 설정 - 사망 확률")]
        [SerializeField] private Color deathLowColor = Color.yellow;         // 20%
        [SerializeField] private Color deathMediumColor = new Color(1f, 0.5f, 0f); // 40~60%
        [SerializeField] private Color deathHighColor = Color.red;           // 80%+

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private UnitInstance boundUnit;

        // =====================================================================
        // 바인딩
        // =====================================================================

        /// <summary>
        /// 유닛 데이터 바인딩
        /// </summary>
        public void Bind(UnitInstance unit)
        {
            boundUnit = unit;
            Refresh();
        }

        /// <summary>
        /// 현재 바인딩된 유닛
        /// </summary>
        public UnitInstance BoundUnit => boundUnit;

        /// <summary>
        /// UI 갱신
        /// </summary>
        public void Refresh()
        {
            if (boundUnit == null) return;

            UpdateNameDisplay();
            UpdateJobDisplay();
            UpdateStageDisplay();
            UpdatePromotionDisplay();
            UpdateCombatPowerDisplay();
            UpdateLoyaltyDisplay();
            UpdateDeathChanceDisplay();
            UpdateStatusBadges();
            UpdateVisualStyle();
        }

        // =====================================================================
        // UI 갱신 - 개별 요소
        // =====================================================================

        /// <summary>
        /// 이름 표시 갱신
        /// </summary>
        private void UpdateNameDisplay()
        {
            if (nameText != null)
            {
                nameText.text = boundUnit.unitName;
            }
        }

        /// <summary>
        /// 직업 표시 갱신
        /// </summary>
        private void UpdateJobDisplay()
        {
            if (jobText != null)
            {
                jobText.text = GetJobDisplayName(boundUnit.job);
            }

            // 직업 아이콘 (있으면)
            if (jobIcon != null)
            {
                // TODO: 직업별 아이콘 스프라이트 설정
                // jobIcon.sprite = GetJobSprite(boundUnit.job);
            }
        }

        /// <summary>
        /// 성장 단계 + 잔여 턴 표시 갱신
        /// </summary>
        private void UpdateStageDisplay()
        {
            if (stageText == null) return;

            string stageName = GetStageDisplayName(boundUnit.stage);

            if (boundUnit.stage == GrowthStage.Old)
            {
                // 노년은 경과 턴 표시
                stageText.text = $"{stageName} {boundUnit.oldAgeTurns}턴째";
            }
            else
            {
                // 다른 단계는 잔여 턴 표시
                stageText.text = $"{stageName} {boundUnit.stageRemainingTurns}턴";
            }
        }

        /// <summary>
        /// 전직 단계 표시 갱신
        /// </summary>
        private void UpdatePromotionDisplay()
        {
            if (promotionText == null) return;

            int level = boundUnit.promotionLevel;

            if (level == 0)
            {
                promotionText.text = "";
            }
            else
            {
                // Lv.N 형식으로 표시
                promotionText.text = $"Lv.{level}";
            }
        }

        /// <summary>
        /// 전투력 표시 갱신 (R8-3)
        /// </summary>
        private void UpdateCombatPowerDisplay()
        {
            if (combatPowerText == null) return;

            // 유년은 전투력 없음
            if (boundUnit.stage == GrowthStage.Child)
            {
                combatPowerText.text = "";
                return;
            }

            combatPowerText.text = $"전투력: {boundUnit.combatPower}";
        }

        /// <summary>
        /// 충성도 표시 갱신 (R8-3)
        /// </summary>
        private void UpdateLoyaltyDisplay()
        {
            if (loyaltyText == null) return;

            loyaltyText.text = $"충성도: {boundUnit.loyalty}";

            // 색상 (유년 포함 모두 표시)
            loyaltyText.color = boundUnit.loyalty switch
            {
                >= 80 => loyaltyHighColor,
                >= 50 => loyaltyMidColor,
                _ => loyaltyLowColor
            };
        }

        /// <summary>
        /// 사망 확률 표시 갱신 (노년 전용)
        /// </summary>
        private void UpdateDeathChanceDisplay()
        {
            if (deathChanceText == null) return;

            int chance = boundUnit.GetDeathChance();

            if (chance <= 0)
            {
                deathChanceText.gameObject.SetActive(false);
            }
            else
            {
                deathChanceText.gameObject.SetActive(true);
                deathChanceText.text = $"사망 {chance}%";

                // 확률에 따른 색상
                deathChanceText.color = chance switch
                {
                    <= 20 => deathLowColor,
                    <= 60 => deathMediumColor,
                    _ => deathHighColor
                };
            }
        }

        /// <summary>
        /// 상태 배지 갱신
        /// </summary>
        private void UpdateStatusBadges()
        {
            // 질병 배지
            if (diseaseBadge != null)
            {
                diseaseBadge.SetActive(boundUnit.hasDisease);
            }

            // 이번 턴 전직 완료 배지
            if (promotedBadge != null)
            {
                promotedBadge.SetActive(boundUnit.promotedThisTurn);
            }

            // 임신중 배지 (R8-3)
            if (pregnantBadge != null)
            {
                bool isPregnant = IsUnitInPregnantHouse();
                pregnantBadge.SetActive(isPregnant);
            }

            // 교배 가능 glow (R8-3)
            if (breedableGlow != null)
            {
                bool canBreed = IsUnitBreedable();
                breedableGlow.gameObject.SetActive(canBreed);
                if (canBreed)
                {
                    breedableGlow.color = breedableGlowColor;
                }
            }
        }

        /// <summary>
        /// 시각 스타일 갱신 (배경색, 테두리)
        /// </summary>
        private void UpdateVisualStyle()
        {
            // 배경색
            if (cardBackground != null)
            {
                cardBackground.color = boundUnit.stage switch
                {
                    GrowthStage.Child => childColor,
                    GrowthStage.Young => youngColor,
                    GrowthStage.Middle => middleColor,
                    GrowthStage.Old => oldColor,
                    _ => childColor
                };
            }

            // 테두리
            if (cardBorder != null)
            {
                if (boundUnit.stage == GrowthStage.Child)
                {
                    cardBorder.color = childBorderColor;
                    // TODO: 점선 효과는 별도 스프라이트 필요
                }
                else
                {
                    cardBorder.color = normalBorderColor;
                }
            }
        }

        // =====================================================================
        // 상태 체크 헬퍼 (R8-3)
        // =====================================================================

        /// <summary>
        /// 이 유닛이 속한 집이 임신 중인지 확인
        /// </summary>
        private bool IsUnitInPregnantHouse()
        {
            // HouseSystem이 없으면 false
            if (HouseSystem.Instance == null) return false;
            if (string.IsNullOrEmpty(boundUnit.houseId)) return false;

            var house = HouseSystem.Instance.GetHouseById(boundUnit.houseId);
            return house?.isPregnant ?? false;
        }

        /// <summary>
        /// 이 유닛이 교배 가능한지 확인
        /// </summary>
        private bool IsUnitBreedable()
        {
            // 유년은 불가
            if (boundUnit.stage == GrowthStage.Child) return false;

            // 노년은 불가
            if (boundUnit.stage == GrowthStage.Old) return false;

            // CanBreed() 메서드 사용 (청년 또는 중년 전반)
            return boundUnit.CanBreed();
        }

        // =====================================================================
        // 클릭 이벤트
        // =====================================================================

        /// <summary>
        /// 카드 클릭 시
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (boundUnit == null) return;

            Debug.Log($"[UnitCard] 클릭됨: {boundUnit.unitName}");
            OnUnitCardClicked?.Invoke(boundUnit);
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

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
        /// 전직 가능 여부
        /// </summary>
        public bool CanPromote()
        {
            return boundUnit?.CanPromote() ?? false;
        }
    }
}