// =============================================================================
// DeckSystem.cs
// 덱빌딩 핵심 시스템 - 덱/손패/버림패 관리
// =============================================================================
// [리팩토링] 통합 효과 시스템 적용 (ConditionalEffect[] 사용)
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 덱 시스템
    /// 카드 드로우, 셔플, 버림, 플레이 등 덱빌딩 핵심 기능 담당
    /// </summary>
    public class DeckSystem : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static DeckSystem Instance { get; private set; }

        // =====================================================================
        // ...
        // =====================================================================

        // ...
        public static event Action<List<CardInstance>> OnCardsDrawn;

        // ...
        public static event Action OnHandChanged;

        // ...
        public static event Action<int> OnDeckChanged;

        // ...
        public static event Action<int> OnDiscardChanged;

        // ...
        public static event Action<CardInstance> OnCardPlayed;

        // ...
        public static event Action<CardInstance> OnCardDestroyed;

        // ...
        public static event Action OnDeckShuffled;

        // =====================================================================
        // ...
        // =====================================================================

        private GameState State => GameManager.Instance.State;

        public int DeckCount => State?.deck.Count ?? 0;
        public int HandCount => State?.hand.Count ?? 0;
        public int DiscardCount => State?.discardPile.Count ?? 0;
        public List<CardInstance> Hand => State?.hand;

        // =====================================================================
        // ...
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
        // ...
        // =====================================================================

        /// <summary>
        // ...
        /// </summary>
        // ...
        // ...
        public List<CardInstance> DrawCards(int count)
        {
            if (State == null) return new List<CardInstance>();

            // [D1] 손패 상한 체크 - 상한 도달 시 드로우 중단
            if (State.hand.Count >= GameConfig.MaxHandSize)
            {
                Debug.Log("[DeckSystem] 손패 상한 도달 - 드로우 중단");
                return new List<CardInstance>();
            }

            // 파손 카드에 의한 드로우 감소
            int drawReduction = CountDamageCardsInHand();
            int actualCount = Mathf.Max(0, count - drawReduction);

            if (drawReduction > 0)
            {
                Debug.Log($"[DeckSystem] 파손 효과: 드로우 -{drawReduction} (요청 {count} -> 실제 {actualCount})");
            }

            List<CardInstance> drawnCards = new List<CardInstance>();

            for (int i = 0; i < actualCount; i++)
            {
                // ...
                if (State.deck.Count == 0)
                {
                    ShuffleDiscardIntoDeck();
                }

                // ...
                if (State.deck.Count == 0)
                {
                    Debug.Log("[DeckSystem] 덱과 버림더미 모두 비어있음");
                    break;
                }

                // ...
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
        // ...
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
        // ...
        // =====================================================================

        /// <summary>
        // ...
        /// </summary>
        public void ShuffleDiscardIntoDeck()
        {
            if (State == null || State.discardPile.Count == 0) return;

            Debug.Log($"[DeckSystem] 버림더미 {State.discardPile.Count}장 -> 덱으로 셔플");

            // ...
            State.deck.AddRange(State.discardPile);
            State.discardPile.Clear();

            // ...
            ShuffleDeck();

            OnDeckShuffled?.Invoke();
            OnDeckChanged?.Invoke(State.deck.Count);
            OnDiscardChanged?.Invoke(State.discardPile.Count);
        }

        /// <summary>
        // ...
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
        // ...
        // =====================================================================

        /// <summary>
        // ...
        // ...
        /// </summary>
        public bool CanPlayCard(CardInstance card)
        {
            if (State == null) return false;
            if (!State.hand.Contains(card)) return false;

            // ...
            if (State.currentPhase != GamePhase.Deck)
            {
                return false;
            }

            var cardData = DataLoader.Instance?.GetCard(card.cardDataId);
            if (cardData == null) return false;

            // ...
            if (cardData.cardType == CardType.Pollution)
            {
                return false;
            }

            // ...
            if (cardData.cardType == CardType.Treasure)
            {
                return false;
            }

            // ...
            if (cardData.cardType == CardType.Action)
            {
                return State.remainingActions > 0;
            }

            return false;
        }

        /// <summary>
        // ...
        /// </summary>
        // ...
        // ...
        public bool PlayCard(CardInstance card)
        {
            if (!CanPlayCard(card))
            {
                Debug.LogWarning($"[DeckSystem] 카드 플레이 불가: {card.cardDataId}");
                return false;
            }

            // ...
            State.hand.Remove(card);
            State.playArea.Add(card);

            // ...
            TurnManager.Instance?.ConsumeAction();

            Debug.Log($"[DeckSystem] 카드 플레이: {card.cardDataId}");
            OnCardPlayed?.Invoke(card);
            OnHandChanged?.Invoke();

            // ...
            ExecuteCardEffects(card);

            return true;
        }

        /// <summary>
        // ...
        // ...
        /// </summary>
        private void ExecuteCardEffects(CardInstance card)
        {
            var cardData = DataLoader.Instance?.GetCard(card.cardDataId);
            if (cardData == null)
            {
                Debug.Log($"[DeckSystem] 카드 데이터 없음: {card.cardDataId}");
                return;
            }

            // [리팩토링] 통합 효과 시스템 - effects가 이제 ConditionalEffect[] 타입
            if (cardData.effects == null || cardData.effects.Length == 0)
            {
                Debug.Log($"[DeckSystem] 카드 효과 없음: {card.cardDataId}");
                return;
            }

            // EffectSystem에 위임 (ConditionalEffect[] 사용)
            if (EffectSystem.Instance != null)
            {
                Debug.Log($"[DeckSystem] 효과 실행: {card.cardDataId}");
                EffectSystem.Instance.ExecuteConditionalEffects(card, cardData.effects);
            }
            else
            {
                Debug.LogWarning("[DeckSystem] EffectSystem 없음 - 효과 실행 불가");
            }
        }

        // =====================================================================
        // ...
        // =====================================================================

        /// <summary>
        // ...
        /// </summary>
        // ...
        public int CalculateTreasureGold()
        {
            if (State == null) return 0;

            int total = 0;

            foreach (var card in State.hand)
            {
                var cardData = DataLoader.Instance?.GetCard(card.cardDataId);
                if (cardData?.cardType != CardType.Treasure) continue;

                // ...
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
        // ...
        // =====================================================================

        /// <summary>
        // ...
        /// </summary>
        public void CleanupCards()
        {
            if (State == null) return;

            // ...
            RemoveTemporaryCards();

            // ...
            State.discardPile.AddRange(State.hand);
            State.hand.Clear();

            // ...
            State.discardPile.AddRange(State.playArea);
            State.playArea.Clear();

            Debug.Log($"[DeckSystem] 카드 정리 완료 (버림더미: {State.discardPile.Count})");
            OnHandChanged?.Invoke();
            OnDiscardChanged?.Invoke(State.discardPile.Count);
        }

        /// <summary>
        // ...
        /// </summary>
        private void RemoveTemporaryCards()
        {
            // ...
            State.hand.RemoveAll(c =>
            {
                if (c.isTemporary)
                {
                    Debug.Log($"[DeckSystem] 임시 카드 소멸: {c.cardDataId}");
                    return true;
                }
                return false;
            });

            // ...
            State.playArea.RemoveAll(c => c.isTemporary);
        }

        // =====================================================================
        // ...
        // =====================================================================

        /// <summary>
        // ...
        /// </summary>
        public void DestroyCard(CardInstance card)
        {
            if (State == null) return;

            // ...
            bool removed = State.deck.Remove(card) ||
                          State.hand.Remove(card) ||
                          State.discardPile.Remove(card) ||
                          State.playArea.Remove(card);

            if (!removed)
            {
                Debug.LogWarning($"[DeckSystem] 소멸할 카드를 찾지 못함: {card.cardDataId}");
                return;
            }

            // ...
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
        // ...
        // =====================================================================

        /// <summary>
        // ...
        /// </summary>
        public CardInstance AddCardToDeck(string cardId, string ownerUnitId = null)
        {
            if (State == null) return null;

            var card = CardInstance.Create(cardId, ownerUnitId);
            State.deck.Add(card);

            // ...
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
        // ...
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
        // ...
        // =====================================================================

        /// <summary>
        // ...
        /// </summary>
        // ...
        // ...
        public int SettleCards(List<CardInstance> cards)
        {
            if (State == null || cards == null) return 0;

            int totalGold = 0;

            foreach (var card in cards)
            {
                var cardData = DataLoader.Instance?.GetCard(card.cardDataId);
                if (cardData?.cardType != CardType.Treasure) continue;

                // ...
                int goldValue = cardData.goldValue;
                if (card.isBoostedThisTurn && card.boostedGrade.HasValue)
                {
                    goldValue = TreasureGradeUtil.GetGoldValue(card.boostedGrade.Value);
                }

                totalGold += goldValue;

                // ...
                if (State.hand.Remove(card))
                {
                    State.discardPile.Add(card);
                }

                Debug.Log($"[DeckSystem] 정산: {cardData.cardName} = {goldValue} 골드");
            }

            // ...
            OnHandChanged?.Invoke();
            OnDiscardChanged?.Invoke(State.discardPile.Count);

            return totalGold;
        }

        /// <summary>
        // ...
        /// </summary>
        // ...
        public void MoveCardsToDeckBottom(List<CardInstance> cards)
        {
            if (State == null || cards == null) return;

            foreach (var card in cards)
            {
                // ...
                if (State.hand.Remove(card))
                {
                    // ...
                    State.deck.Add(card);
                    Debug.Log($"[DeckSystem] 덱 맨 아래로: {card.cardDataId}");
                }
            }

            // ...
            OnHandChanged?.Invoke();
            OnDeckChanged?.Invoke(State.deck.Count);
        }

        // =====================================================================
        // ...
        // =====================================================================

        /// <summary>
        // ...
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
        // ...
        /// </summary>
        public List<CardInstance> GetTreasureCardsInHand()
        {
            return GetCardsInHand(CardType.Treasure);
        }

        /// <summary>
        // ...
        /// </summary>
        public List<CardInstance> GetPollutionCardsInHand()
        {
            return GetCardsInHand(CardType.Pollution);
        }

        /// <summary>
        // ...
        // ...
        /// </summary>
        public void NotifyHandChanged()
        {
            OnHandChanged?.Invoke();
        }
    }
}