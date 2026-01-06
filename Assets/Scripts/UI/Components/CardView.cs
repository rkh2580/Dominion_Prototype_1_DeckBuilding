// =============================================================================
// CardView.cs
// 개별 카드 표시 컴포넌트
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DeckBuildingEconomy.Core;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.UI
{
    /// <summary>
    /// 카드 뷰 컴포넌트
    /// 카드 한 장의 시각적 표현을 담당
    /// 
    /// [역할]
    /// - CardInstance 데이터를 받아 UI에 표시
    /// - 카드 타입별 시각 구분 (재화/액션/오염)
    /// - 클릭 이벤트 처리
    /// 
    /// [사용법]
    /// 1. 프리팹으로 만들어서 HandPanel이 생성
    /// 2. Bind(cardInstance) 호출하면 UI 갱신
    /// </summary>
    public class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        // =====================================================================
        // Inspector 연결
        // =====================================================================

        [Header("기본 요소")]
        [SerializeField] private Image cardBackground;      // 카드 배경
        [SerializeField] private Image cardBorder;          // 카드 테두리
        [SerializeField] private TMP_Text cardNameText;     // 카드 이름
        [SerializeField] private TMP_Text effectText;       // 효과/골드 값 표시

        [Header("상태 표시")]
        [SerializeField] private GameObject temporaryBadge; // "임시" 배지 (주조 등)
        [SerializeField] private GameObject boostEffect;    // 부스트 이펙트

        [Header("색상 설정 - 테두리")]
        [SerializeField] private Color treasureBorderColor = new Color(1f, 0.84f, 0f);      // 금색
        [SerializeField] private Color actionBorderColor = new Color(0.3f, 0.5f, 1f);       // 파란색
        [SerializeField] private Color pollutionBorderColor = new Color(0.5f, 0.2f, 0.5f);  // 보라색

        [Header("색상 설정 - 배경 (재화 등급별)")]
        [SerializeField] private Color copperColor = new Color(0.72f, 0.45f, 0.2f);         // 동색
        [SerializeField] private Color silverColor = new Color(0.75f, 0.75f, 0.75f);        // 은색
        [SerializeField] private Color goldColor = new Color(1f, 0.84f, 0f);                // 금색
        [SerializeField] private Color emeraldColor = new Color(0.31f, 0.78f, 0.47f);       // 에메랄드
        [SerializeField] private Color sapphireColor = new Color(0.06f, 0.32f, 0.73f);      // 사파이어
        [SerializeField] private Color rubyColor = new Color(0.88f, 0.07f, 0.37f);          // 루비
        [SerializeField] private Color diamondColor = new Color(0.9f, 0.9f, 1f);            // 다이아몬드

        [Header("색상 설정 - 기타")]
        [SerializeField] private Color actionBackgroundColor = new Color(0.2f, 0.3f, 0.5f); // 액션 배경
        [SerializeField] private Color pollutionBackgroundColor = new Color(0.2f, 0.1f, 0.2f); // 오염 배경
        [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);   // 비활성

        [Header("호버 설정")]
        [SerializeField] private float hoverScale = 1.1f;   // 마우스 오버 시 확대 비율
        [SerializeField] private float hoverDuration = 0.1f; // 확대 애니메이션 시간

        // =====================================================================
        // 데이터
        // =====================================================================

        /// <summary>현재 바인딩된 카드 인스턴스</summary>
        public CardInstance BoundCard { get; private set; }

        /// <summary>현재 바인딩된 카드 데이터</summary>
        public CardData BoundCardData { get; private set; }

        /// <summary>플레이 가능 여부</summary>
        public bool IsPlayable { get; private set; }

        // 원래 크기 (호버 복원용)
        private Vector3 originalScale;

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>카드 클릭됨 (CardView 전달)</summary>
        public static event System.Action<CardView> OnCardClicked;

        /// <summary>카드 우클릭됨 (상세보기용)</summary>
        public static event System.Action<CardView> OnCardRightClicked;

        /// <summary>카드 호버 시작</summary>
        public static event System.Action<CardView> OnCardHoverEnter;

        /// <summary>카드 호버 종료</summary>
        public static event System.Action<CardView> OnCardHoverExit;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            originalScale = transform.localScale;
        }

        // =====================================================================
        // 데이터 바인딩
        // =====================================================================

        /// <summary>
        /// 카드 데이터 바인딩
        /// HandPanel에서 카드 생성 시 호출
        /// </summary>
        /// <param name="cardInstance">표시할 카드 인스턴스</param>
        public void Bind(CardInstance cardInstance)
        {
            BoundCard = cardInstance;
            BoundCardData = DataLoader.Instance?.GetCard(cardInstance.cardDataId);

            if (BoundCardData == null)
            {
                Debug.LogWarning($"[CardView] 카드 데이터 없음: {cardInstance.cardDataId}");
                return;
            }

            Refresh();
        }

        /// <summary>
        /// UI 갱신
        /// </summary>
        public void Refresh()
        {
            if (BoundCardData == null) return;

            // 타입별 표시 (이름 포함)
            switch (BoundCardData.cardType)
            {
                case CardType.Treasure:
                    SetupTreasureCard();
                    break;

                case CardType.Action:
                    SetupActionCard();
                    break;

                case CardType.Pollution:
                    SetupPollutionCard();
                    break;
            }

            // 임시 카드 배지
            if (temporaryBadge != null)
            {
                temporaryBadge.SetActive(BoundCard.isTemporary);
            }

            // 부스트 이펙트
            if (boostEffect != null)
            {
                boostEffect.SetActive(BoundCard.isBoostedThisTurn);
            }

            // 플레이 가능 여부 갱신
            UpdatePlayableState();
        }

        /// <summary>
        /// 재화 카드 설정
        /// </summary>
        private void SetupTreasureCard()
        {
            // 부스트 적용 시 표시할 등급 결정
            TreasureGrade displayGrade = BoundCardData.treasureGrade;
            bool isBoosted = BoundCard.isBoostedThisTurn && BoundCard.boostedGrade.HasValue;

            if (isBoosted)
            {
                displayGrade = BoundCard.boostedGrade.Value;
            }

            // 카드 이름: 부스트 시 해당 등급 이름 표시
            if (cardNameText != null)
            {
                string displayName = TreasureGradeUtil.GetName(displayGrade);

                // 임시 부스트 표시 (이름 앞에 ★ 추가)
                if (isBoosted)
                {
                    displayName = $"★{displayName}";
                }

                cardNameText.text = displayName;
            }

            // 테두리: 금색 (부스트 시 더 밝게)
            if (cardBorder != null)
            {
                if (isBoosted)
                {
                    // 부스트 시 밝은 노란색 테두리
                    cardBorder.color = new Color(1f, 1f, 0.5f);
                }
                else
                {
                    cardBorder.color = treasureBorderColor;
                }
            }

            // 배경: 등급별 색상 (부스트 반영)
            if (cardBackground != null)
            {
                cardBackground.color = GetTreasureColor(displayGrade);
            }

            // 효과 텍스트: 부스트 시 동적 계산, 일반 상태에서는 SO description 사용
            if (effectText != null)
            {
                if (isBoosted)
                {
                    int goldValue = TreasureGradeUtil.GetGoldValue(displayGrade);
                    effectText.text = $"{goldValue} G";
                }
                else
                {
                    effectText.text = BoundCardData.description;
                }
            }
        }

        /// <summary>
        /// 액션 카드 설정
        /// </summary>
        private void SetupActionCard()
        {
            // 카드 이름
            if (cardNameText != null)
            {
                cardNameText.text = BoundCardData.cardName;
            }

            // 테두리: 파란색
            if (cardBorder != null)
            {
                cardBorder.color = actionBorderColor;
            }

            // 배경
            if (cardBackground != null)
            {
                cardBackground.color = actionBackgroundColor;
            }

            // 효과 텍스트
            if (effectText != null)
            {
                effectText.text = BoundCardData.description;
            }
        }

        /// <summary>
        /// 오염 카드 설정
        /// </summary>
        private void SetupPollutionCard()
        {
            // 카드 이름
            if (cardNameText != null)
            {
                cardNameText.text = BoundCardData.cardName;
            }

            // 테두리: 보라색
            if (cardBorder != null)
            {
                cardBorder.color = pollutionBorderColor;
            }

            // 배경: 어두운 보라
            if (cardBackground != null)
            {
                cardBackground.color = pollutionBackgroundColor;
            }

            // 효과 텍스트
            if (effectText != null)
            {
                effectText.text = BoundCardData.description;
            }
        }

        /// <summary>
        /// 재화 등급별 색상 반환
        /// </summary>
        private Color GetTreasureColor(TreasureGrade grade)
        {
            switch (grade)
            {
                case TreasureGrade.Copper: return copperColor;
                case TreasureGrade.Silver: return silverColor;
                case TreasureGrade.Gold: return goldColor;
                case TreasureGrade.Emerald: return emeraldColor;
                case TreasureGrade.Sapphire: return sapphireColor;
                case TreasureGrade.Ruby: return rubyColor;
                case TreasureGrade.Diamond: return diamondColor;
                default: return copperColor;
            }
        }

        // =====================================================================
        // 플레이 가능 상태
        // =====================================================================

        /// <summary>
        /// 플레이 가능 상태 갱신
        /// </summary>
        public void UpdatePlayableState()
        {
            if (BoundCard == null) return;

            // DeckSystem에서 플레이 가능 여부 확인
            IsPlayable = DeckSystem.Instance?.CanPlayCard(BoundCard) ?? false;

            // 시각적 피드백
            ApplyPlayableVisual();
        }

        /// <summary>
        /// 플레이 가능 여부에 따른 시각 처리
        /// </summary>
        private void ApplyPlayableVisual()
        {
            // 오염 카드는 항상 어둡게
            if (BoundCardData?.cardType == CardType.Pollution)
            {
                SetCardAlpha(0.7f);
                return;
            }

            // 재화 카드는 항상 밝게 (자동 정산)
            if (BoundCardData?.cardType == CardType.Treasure)
            {
                SetCardAlpha(1f);
                return;
            }

            // 액션 카드: 플레이 가능 여부에 따라
            if (IsPlayable)
            {
                SetCardAlpha(1f);
            }
            else
            {
                SetCardAlpha(0.5f);
            }
        }

        /// <summary>
        /// 카드 투명도 설정
        /// </summary>
        private void SetCardAlpha(float alpha)
        {
            if (cardBackground != null)
            {
                Color c = cardBackground.color;
                c.a = alpha;
                cardBackground.color = c;
            }
        }

        // =====================================================================
        // 인터랙션 (IPointer 인터페이스 구현)
        // =====================================================================

        /// <summary>
        /// 클릭 처리
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // 좌클릭: 카드 플레이 시도
                OnCardClicked?.Invoke(this);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                // 우클릭: 상세 보기
                OnCardRightClicked?.Invoke(this);
            }
        }

        /// <summary>
        /// 마우스 진입
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            // 확대 효과
            transform.localScale = originalScale * hoverScale;

            // 이벤트 발생
            OnCardHoverEnter?.Invoke(this);
        }

        /// <summary>
        /// 마우스 이탈
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            // 원래 크기로 복원
            transform.localScale = originalScale;

            // 이벤트 발생
            OnCardHoverExit?.Invoke(this);
        }

        // =====================================================================
        // 디버그
        // =====================================================================

        [ContextMenu("Log Card Info")]
        private void DebugLogCardInfo()
        {
            if (BoundCard == null)
            {
                Debug.Log("[CardView] 바인딩된 카드 없음");
                return;
            }

            Debug.Log($"[CardView] {BoundCardData?.cardName} ({BoundCardData?.cardType})");
            Debug.Log($"  - 인스턴스 ID: {BoundCard.instanceId}");
            Debug.Log($"  - 소속 유닛: {BoundCard.ownerUnitId ?? "무소속"}");
            Debug.Log($"  - 임시: {BoundCard.isTemporary}");
            Debug.Log($"  - 플레이 가능: {IsPlayable}");
        }
    }
}