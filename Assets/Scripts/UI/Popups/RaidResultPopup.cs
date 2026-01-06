// =============================================================================
// RaidResultPopup.cs
// 약탈/최종전투 결과 팝업
// =============================================================================
// [R8-8 신규] 2026-01-03
// - 약탈 방어 성공/실패 표시
// - 피해 내역 (골드 손실, 유년 납치)
// - 최종 전투 결과 표시
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 약탈/최종전투 결과 팝업
    /// </summary>
    public class RaidResultPopup : MonoBehaviour
    {
        // =====================================================================
        // Inspector 연결
        // =====================================================================

        [Header("팝업 요소")]
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private Image dimBackground;

        [Header("제목/결과")]
        [SerializeField] private TMP_Text titleText;           // "??? 방어 성공!" / "?? 방어 실패"
        [SerializeField] private Image titleBackground;        // 제목 배경 (색상 변경용)

        [Header("전투력 비교")]
        [SerializeField] private TMP_Text allyPowerText;       // "아군 전투력: 85"
        [SerializeField] private TMP_Text enemyPowerText;      // "적 전투력: 60"
        [SerializeField] private Slider powerCompareSlider;    // 전투력 비교 슬라이더 (선택)

        [Header("상세 정보")]
        [SerializeField] private TMP_Text detailText;          // "영지가 무사합니다" / "골드 15 약탈당함"
        [SerializeField] private TMP_Text damageText;          // 피해 상세 (별도 표시)

        [Header("버튼")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmButtonText;

        [Header("색상")]
        [SerializeField] private Color successColor = new Color(0.2f, 0.7f, 0.3f);    // 녹색
        [SerializeField] private Color failureColor = new Color(0.8f, 0.3f, 0.3f);    // 빨간색
        [SerializeField] private Color victoryColor = new Color(1f, 0.84f, 0f);       // 금색
        [SerializeField] private Color defeatColor = new Color(0.4f, 0.4f, 0.4f);     // 회색

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private bool isFinalBattle = false;
        private RaidDamageType? pendingDamageType = null;
        private int pendingDamageAmount = 0;
        private UnitInstance pendingDamagedUnit = null;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            // 버튼 이벤트 연결
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(Hide);
            }

            // 초기 상태: 숨김
            Hide();
        }

        private void OnEnable()
        {
            // RaidSystem 이벤트 구독
            RaidSystem.OnRaidCompleted += OnRaidCompleted;
            RaidSystem.OnRaidDamage += OnRaidDamage;
            RaidSystem.OnFinalBattleCompleted += OnFinalBattleCompleted;
        }

        private void OnDisable()
        {
            RaidSystem.OnRaidCompleted -= OnRaidCompleted;
            RaidSystem.OnRaidDamage -= OnRaidDamage;
            RaidSystem.OnFinalBattleCompleted -= OnFinalBattleCompleted;
        }

        // =====================================================================
        // 표시/숨김
        // =====================================================================

        /// <summary>
        /// 팝업 표시
        /// </summary>
        private void ShowPopup()
        {
            if (dimBackground != null) dimBackground.gameObject.SetActive(true);
            if (popupRoot != null) popupRoot.SetActive(true);

            Debug.Log("[RaidResultPopup] 팝업 표시");
        }

        /// <summary>
        /// 팝업 숨김
        /// </summary>
        public void Hide()
        {
            if (dimBackground != null) dimBackground.gameObject.SetActive(false);
            if (popupRoot != null) popupRoot.SetActive(false);

            // 상태 초기화
            isFinalBattle = false;
            pendingDamageType = null;
            pendingDamageAmount = 0;
            pendingDamagedUnit = null;

            Debug.Log("[RaidResultPopup] 팝업 숨김");
        }

        // =====================================================================
        // 이벤트 핸들러
        // =====================================================================

        /// <summary>
        /// 약탈 결과 (OnRaidDamage보다 나중에 호출됨)
        /// </summary>
        private void OnRaidCompleted(bool defended, int enemyPower, int allyPower)
        {
            isFinalBattle = false;

            Debug.Log($"[RaidResultPopup] 약탈 결과: 방어 {(defended ? "성공" : "실패")}, 적 {enemyPower} vs 아군 {allyPower}");

            // 제목 설정
            if (titleText != null)
            {
                titleText.text = defended ? "??? 방어 성공!" : "?? 방어 실패...";
            }

            // 제목 배경 색상
            if (titleBackground != null)
            {
                titleBackground.color = defended ? successColor : failureColor;
            }

            // 전투력 표시
            if (allyPowerText != null)
            {
                allyPowerText.text = $"아군 전투력: {allyPower}";
            }

            if (enemyPowerText != null)
            {
                enemyPowerText.text = $"적 전투력: {enemyPower}";
            }

            // 전투력 비교 슬라이더 (선택)
            if (powerCompareSlider != null)
            {
                int maxPower = Mathf.Max(allyPower, enemyPower, 1);
                powerCompareSlider.maxValue = maxPower;
                powerCompareSlider.value = allyPower;
            }

            // 상세 정보
            if (detailText != null)
            {
                if (defended)
                {
                    detailText.text = "영지가 무사합니다.";
                }
                else
                {
                    // 피해 정보는 OnRaidDamage에서 이미 받았을 수 있음
                    detailText.text = GetDamageDescription();
                }
            }

            // 피해 텍스트 (방어 실패 시만)
            if (damageText != null)
            {
                if (defended)
                {
                    damageText.gameObject.SetActive(false);
                }
                else
                {
                    damageText.gameObject.SetActive(true);
                    damageText.text = GetDamageDetailText();
                }
            }

            // 버튼 텍스트
            if (confirmButtonText != null)
            {
                confirmButtonText.text = "확인";
            }

            ShowPopup();
        }

        /// <summary>
        /// 약탈 피해 (OnRaidCompleted보다 먼저 호출됨)
        /// </summary>
        private void OnRaidDamage(RaidDamageType damageType, int amount, UnitInstance unit)
        {
            Debug.Log($"[RaidResultPopup] 약탈 피해: {damageType}, 양: {amount}, 유닛: {unit?.unitName ?? "없음"}");

            // 피해 정보 저장 (OnRaidCompleted에서 사용)
            pendingDamageType = damageType;
            pendingDamageAmount = amount;
            pendingDamagedUnit = unit;
        }

        /// <summary>
        /// 최종 전투 결과
        /// </summary>
        private void OnFinalBattleCompleted(bool victory)
        {
            isFinalBattle = true;

            Debug.Log($"[RaidResultPopup] 최종 전투: {(victory ? "승리" : "패배")}");

            // 제목 설정
            if (titleText != null)
            {
                titleText.text = victory ? "?? 최종 승리!" : "?? 패배...";
            }

            // 제목 배경 색상
            if (titleBackground != null)
            {
                titleBackground.color = victory ? victoryColor : defeatColor;
            }

            // 전투력 표시
            int allyPower = RaidSystem.Instance?.GetTotalCombatPower() ?? 0;
            int requiredPower = GameConfig.FinalBattleRequiredPower;

            if (allyPowerText != null)
            {
                allyPowerText.text = $"아군 전투력: {allyPower}";
            }

            if (enemyPowerText != null)
            {
                enemyPowerText.text = $"요구 전투력: {requiredPower}";
            }

            // 전투력 비교 슬라이더
            if (powerCompareSlider != null)
            {
                powerCompareSlider.maxValue = requiredPower;
                powerCompareSlider.value = Mathf.Min(allyPower, requiredPower);
            }

            // 상세 정보
            if (detailText != null)
            {
                if (victory)
                {
                    detailText.text = "왕국을 지켜냈습니다!\n60턴을 버텨낸 당신의 승리입니다.";
                }
                else
                {
                    detailText.text = "적의 공격에 무너졌습니다.\n전투력이 부족했습니다.";
                }
            }

            // 피해 텍스트 숨김 (최종 전투는 피해 없음)
            if (damageText != null)
            {
                damageText.gameObject.SetActive(false);
            }

            // 버튼 텍스트
            if (confirmButtonText != null)
            {
                confirmButtonText.text = victory ? "축하합니다!" : "다시 도전";
            }

            ShowPopup();
        }

        // =====================================================================
        // 헬퍼 메서드
        // =====================================================================

        /// <summary>
        /// 피해 설명 반환
        /// </summary>
        private string GetDamageDescription()
        {
            if (pendingDamageType == null)
            {
                return "피해를 입었습니다.";
            }

            switch (pendingDamageType.Value)
            {
                case RaidDamageType.GoldLoss:
                    return $"?? 골드 {pendingDamageAmount} 약탈당함!";

                case RaidDamageType.ChildKidnapped:
                    string unitName = pendingDamagedUnit?.unitName ?? "유년";
                    return $"?? {unitName} 납치됨!";

                default:
                    return "피해를 입었습니다.";
            }
        }

        /// <summary>
        /// 피해 상세 텍스트 반환
        /// </summary>
        private string GetDamageDetailText()
        {
            if (pendingDamageType == null)
            {
                return "";
            }

            switch (pendingDamageType.Value)
            {
                case RaidDamageType.GoldLoss:
                    return $"보유 골드의 30%({pendingDamageAmount}G)를 잃었습니다.";

                case RaidDamageType.ChildKidnapped:
                    string unitName = pendingDamagedUnit?.unitName ?? "유년";
                    return $"{unitName}이(가) 적에게 납치되었습니다.\n유년 유닛이 소멸합니다.";

                default:
                    return "";
            }
        }
    }
}