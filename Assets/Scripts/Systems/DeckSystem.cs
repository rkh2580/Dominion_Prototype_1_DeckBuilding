// =============================================================================
// DeckSystem.cs
// 덱, 손패, 버림더미 관리
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 덱 시스템 (싱글톤)
    /// - 덱/손패/버림더미 관리
    /// - 드로우/셔플
    /// - 카드 플레이
    /// </summary>
    public class DeckSystem : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static DeckSystem Instance { get; private set; }

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>카드 드로우됨 (드로우된 카드 목록)</summary>
        public static event Action<List<CardInstance>> OnCardsDrawn;

        /// <summary>손패 변경됨</summary>
        public static event Action OnHandChanged;

        /// <summary>덱 변경됨 (덱 잔여 장수)</summary>
        public static event Action<int> OnDeckChanged;

        /// <summary>버림더미 변경됨 (버림더미 장수)</summary>
        public static event Action<int> OnDiscardChanged;

        /// <summary>카드 플레이됨 (플레이한 카드)</summary>
        public static event Action<CardInstance> OnCardPlayed;

        /// <summary>카드 소멸됨 (소멸한 카드)</summary>
        public static event Action<CardInstance> OnCardDestroyed;

        /// <summary>덱 셔플됨</summary>
        public static event Action OnDeckShuffled;

        // =====================================================================
        // 상태 접근
        // =====================================================================

        private GameState State => GameManager.Instance.State;

        public int DeckCount => State?.deck.Count ?? 0;
        public int HandCount => State?.hand.Count ?? 0;
        public int DiscardCount => State?.discardPile.Count ?? 0;
        public List<CardInstance> Hand => State?.hand;

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

            Debug.Log("[DeckSystem] 초기화 완료");
        }

        // =====================================================================
        // 드로우
        // =====================================================================

        /// <summary>
        /// 카드 드로우
        /// </summary>
        /// <param name="count">드로우 장수</param>
        /// <returns>실제 드로우한 카드 목록</returns>
        public List<CardInstance> DrawCards(int count)
        {
            if (State == null) return new List<CardInstance>();

            // 파손 카드 효과 체크 (드로우 -1)
            int drawReduction = CountDamageCardsInHand();
            int actualCount = Mathf.Max(0, count - drawReduction);
            
            if (drawReduction > 0)
            {
                Debug.Log($"[DeckSystem] 파손 효과: 드로우 -{drawReduction} (요청 {count} → 실제 {actualCount})");
            }

            List<CardInstance> drawnCards = new List<CardInstance>();

            for (int i = 0; i < actualCount; i++)
            {
                // 덱이 비었으면 셔플
                if (State.deck.Count == 0)
                {
                    ShuffleDiscardIntoDeck();
                }

                // 셔플 후에도 비었으면 종료
                if (State.deck.Count == 0)
                {
                    Debug.Log("[DeckSystem] 덱과 버림더미 모두 비어있음");
                    break;
                }

                // 드로우
                var card = State.deck[0];
                State.deck.RemoveAt(0);
                State.hand.Add(card);
                drawnCards.Add(card);
            }

            if (drawnCards.Count > 0)
            {
                Debug.Log($"[DeckSystem] {drawnCards.Count}장 드로우 (덱 잔여: {State.deck.Count}, 손패: {State.hand.Count})");
                OnCardsDrawn?.Invoke(drawnCards);
                OnHandChanged?.Invoke();
                OnDeckChanged?.Invoke(State.deck.Count);
            }

            return drawnCards;
        }

        /// <summary>
        /// 손패의 파손 카드 수 (드로우 감소용)
        /// </summary>
        private int CountDamageCardsInHand()
        {
            int count = 0;
            foreach (var card in State.hand)
            {
                var cardData = DataLoader.Instance?.GetCard(card.cardDataId);
                if (cardData?.cardType == CardType.Pollution && 
                    cardData.pollutionType == PollutionType.Damage)
                {
                    count++;
                }
            }
            return count;
        }

        // =====================================================================
        // 셔플
        // =====================================================================

        /// <summary>
        /// 버림더미를 덱으로 셔플
        /// </summary>
        public void ShuffleDiscardIntoDeck()
        {
            if (State == null || State.discardPile.Count == 0) return;

            Debug.Log($"[DeckSystem] 버림더미 {State.discardPile.Count}장 → 덱으로 셔플");

            // 버림더미 → 덱
            State.deck.AddRange(State.discardPile);
            State.discardPile.Clear();

            // 셔플
            ShuffleDeck();

            OnDeckShuffled?.Invoke();
            OnDeckChanged?.Invoke(State.deck.Count);
            OnDiscardChanged?.Invoke(State.discardPile.Count);
        }

        /// <summary>
        /// 덱 셔플 (Fisher-Yates)
        /// </summary>
        public void ShuffleDeck()
        {
            if (State == null) return;

            var deck = State.deck;
            int n = deck.Count;

            for (int i = n - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }

            Debug.Log("[DeckSystem] 덱 셔플 완료");
        }

        // =====================================================================
        // 카드 플레이
        // =====================================================================

        /// <summary>
        /// 카드 플레이 가능 여부 확인
        /// </summary>
        public bool CanPlayCard(CardInstance card)
        {
            if (State == null) return false;
            if (!State.hand.Contains(card)) return false;

            var cardData = DataLoader.Instance?.GetCard(card.cardDataId);
            if (cardData == null) return false;

            // 오염 카드는 플레이 불가
            if (cardData.cardType == CardType.Pollution)
            {
                return false;
            }

            // 재화 카드는 플레이 불필요 (자동 정산)
            if (cardData.cardType == CardType.Treasure)
            {
                return false;
            }

            // 액션 카드: 액션 필요
            if (cardData.cardType == CardType.Action)
            {
                return State.remainingActions > 0;
            }

            return false;
        }

        /// <summary>
        /// 카드 플레이
        /// </summary>
        /// <param name="card">플레이할 카드</param>
        /// <returns>성공 여부</returns>
        public bool PlayCard(CardInstance card)
        {
            if (!CanPlayCard(card))
            {
                Debug.LogWarning($"[DeckSystem] 카드 플레이 불가: {card.cardDataId}");
                return false;
            }

            // 손패 → 플레이 영역
            State.hand.Remove(card);
            State.playArea.Add(card);

            // 액션 소모
            TurnManager.Instance?.ConsumeAction();

            Debug.Log($"[DeckSystem] 카드 플레이: {card.cardDataId}");
            OnCardPlayed?.Invoke(card);
            OnHandChanged?.Invoke();

            // 효과 실행 (TODO: EffectSystem에서 처리)
            ExecuteCardEffects(card);

            return true;
        }

        /// <summary>
        /// 카드 효과 실행 (기본 구현)
        /// </summary>
        private void ExecuteCardEffects(CardInstance card)
        {
            var cardData = DataLoader.Instance?.GetCard(card.cardDataId);
            if (cardData?.effects == null) return;

            foreach (var effect in cardData.effects)
            {
                ExecuteEffect(effect, card);
            }
        }

        /// <summary>
        /// 단일 효과 실행
        /// </summary>
        private void ExecuteEffect(CardEffect effect, CardInstance sourceCard)
        {
            switch (effect.effectType)
            {
                case EffectType.DrawCard:
                    DrawCards(effect.value);
                    break;

                case EffectType.AddAction:
                    TurnManager.Instance?.AddActions(effect.value);
                    break;

                case EffectType.AddGold:
                    GoldSystem.Instance?.AddGold(effect.value);
                    break;

                case EffectType.DelayedGold:
                    AddPersistentEffect(PersistentEffectType.DelayedGold, effect.value, effect.duration);
                    break;

                // TODO: 나머지 효과들은 EffectSystem에서 구현
                default:
                    Debug.Log($"[DeckSystem] 미구현 효과: {effect.effectType}");
                    break;
            }
        }

        /// <summary>
        /// 지속 효과 추가
        /// </summary>
        private void AddPersistentEffect(PersistentEffectType type, int value, int duration)
        {
            State.activeEffects.Add(new PersistentEffect
            {
                effectId = Guid.NewGuid().ToString(),
                type = type,
                value = value,
                remainingTurns = duration
            });
            Debug.Log($"[DeckSystem] 지속 효과 추가: {type}, 값 {value}, {duration}턴");
        }

        // =====================================================================
        // 재화 정산
        // =====================================================================

        /// <summary>
        /// 손패의 재화 카드 골드 계산
        /// </summary>
        /// <returns>총 골드</returns>
        public int CalculateTreasureGold()
        {
            if (State == null) return 0;

            int total = 0;

            foreach (var card in State.hand)
            {
                var cardData = DataLoader.Instance?.GetCard(card.cardDataId);
                if (cardData?.cardType != CardType.Treasure) continue;

                // 부스트 적용 여부 확인
                int goldValue = cardData.goldValue;
                
                if (card.isBoostedThisTurn && card.boostedGrade.HasValue)
                {
                    goldValue = TreasureGradeUtil.GetGoldValue(card.boostedGrade.Value);
                }

                total += goldValue;
            }

            Debug.Log($"[DeckSystem] 재화 정산: {total} 골드");
            return total;
        }

        // =====================================================================
        // 카드 정리
        // =====================================================================

        /// <summary>
        /// 턴 종료 시 카드 정리
        /// </summary>
        public void CleanupCards()
        {
            if (State == null) return;

            // 임시 카드 소멸 (주조, 제련 등)
            RemoveTemporaryCards();

            // 손패 → 버림더미
            State.discardPile.AddRange(State.hand);
            State.hand.Clear();

            // 플레이 영역 → 버림더미
            State.discardPile.AddRange(State.playArea);
            State.playArea.Clear();

            Debug.Log($"[DeckSystem] 카드 정리 완료 (버림더미: {State.discardPile.Count})");
            OnHandChanged?.Invoke();
            OnDiscardChanged?.Invoke(State.discardPile.Count);
        }

        /// <summary>
        /// 임시 카드 제거
        /// </summary>
        private void RemoveTemporaryCards()
        {
            // 손패에서 제거
            State.hand.RemoveAll(c =>
            {
                if (c.isTemporary)
                {
                    Debug.Log($"[DeckSystem] 임시 카드 소멸: {c.cardDataId}");
                    return true;
                }
                return false;
            });

            // 플레이 영역에서 제거
            State.playArea.RemoveAll(c => c.isTemporary);
        }

        // =====================================================================
        // 카드 소멸
        // =====================================================================

        /// <summary>
        /// 카드 소멸 (게임에서 완전 제거)
        /// </summary>
        public void DestroyCard(CardInstance card)
        {
            if (State == null) return;

            // 모든 영역에서 제거
            bool removed = State.deck.Remove(card) ||
                          State.hand.Remove(card) ||
                          State.discardPile.Remove(card) ||
                          State.playArea.Remove(card);

            if (!removed)
            {
                Debug.LogWarning($"[DeckSystem] 소멸할 카드를 찾지 못함: {card.cardDataId}");
                return;
            }

            // 유닛 종속 카드면 유닛에서도 제거
            if (!string.IsNullOrEmpty(card.ownerUnitId))
            {
                var unit = State.units.Find(u => u.unitId == card.ownerUnitId);
                unit?.ownedCardIds.Remove(card.instanceId);
                GameManager.Instance?.RecalculateMaintenanceCost();
            }

            Debug.Log($"[DeckSystem] 카드 소멸: {card.cardDataId}");
            OnCardDestroyed?.Invoke(card);
            OnHandChanged?.Invoke();
        }

        // =====================================================================
        // 카드 추가
        // =====================================================================

        /// <summary>
        /// 덱에 카드 추가
        /// </summary>
        public CardInstance AddCardToDeck(string cardId, string ownerUnitId = null)
        {
            if (State == null) return null;

            var card = CardInstance.Create(cardId, ownerUnitId);
            State.deck.Add(card);

            // 유닛 종속이면 유닛에도 등록
            if (!string.IsNullOrEmpty(ownerUnitId))
            {
                var unit = State.units.Find(u => u.unitId == ownerUnitId);
                unit?.ownedCardIds.Add(card.instanceId);
                GameManager.Instance?.RecalculateMaintenanceCost();
            }

            Debug.Log($"[DeckSystem] 카드 추가: {cardId} (소속: {ownerUnitId ?? "무소속"})");
            OnDeckChanged?.Invoke(State.deck.Count);

            return card;
        }

        /// <summary>
        /// 손패에 카드 추가 (임시 카드 등)
        /// </summary>
        public CardInstance AddCardToHand(string cardId, bool isTemporary = false)
        {
            if (State == null) return null;

            var card = CardInstance.Create(cardId, null, isTemporary);
            State.hand.Add(card);

            Debug.Log($"[DeckSystem] 손패에 카드 추가: {cardId} (임시: {isTemporary})");
            OnHandChanged?.Invoke();

            return card;
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        /// <summary>
        /// 손패에서 특정 타입 카드 찾기
        /// </summary>
        public List<CardInstance> GetCardsInHand(CardType type)
        {
            if (State == null) return new List<CardInstance>();

            return State.hand.FindAll(card =>
            {
                var data = DataLoader.Instance?.GetCard(card.cardDataId);
                return data?.cardType == type;
            });
        }

        /// <summary>
        /// 손패에서 재화 카드 찾기
        /// </summary>
        public List<CardInstance> GetTreasureCardsInHand()
        {
            return GetCardsInHand(CardType.Treasure);
        }

        /// <summary>
        /// 손패에서 오염 카드 찾기
        /// </summary>
        public List<CardInstance> GetPollutionCardsInHand()
        {
            return GetCardsInHand(CardType.Pollution);
        }
    }
}
