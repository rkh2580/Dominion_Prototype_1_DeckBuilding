// =============================================================================
// EffectSystem.cs
// 카드 효과 실행 시스템 (비동기 콜백 기반)
// =============================================================================
// [E2 수정] 조건부 효과 시스템 추가, EffectResultStack 추가
// [E7 수정] ShuffleDeck 추가, AllHandTreasure/AllHandPollution 자동 수집
// =============================================================================
//
// [구조 설명]
// - 모든 카드 효과를 중앙에서 처리
// - 대상 선택이 필요한 효과는 UI에 요청 후 콜백으로 완료
// - 효과 큐를 통해 순차적 실행 보장
//
// [흐름 - 기존]
// PlayCard() → ExecuteEffects() → ProcessNextEffect() → (대상 선택) → CompleteEffect()
//
// [흐름 - 신규 조건부]
// PlayCard() → ExecuteConditionalEffects() → EvaluateConditions() → ExecuteEffects()
//
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    // =========================================================================
    // 효과 결과 클래스 (E2 추가)
    // =========================================================================

    /// <summary>
    /// 효과 실행 결과
    /// 다음 효과에서 참조 가능 (예: "정산한 장수만큼 드로우")
    /// </summary>
    public class EffectResult
    {
        /// <summary>실행한 효과 타입</summary>
        public EffectType effectType;

        /// <summary>성공 여부</summary>
        public bool success;

        /// <summary>처리한 수 (정산 장수 등)</summary>
        public int count;

        /// <summary>결과값 (획득한 골드 등)</summary>
        public int value;
    }

    /// <summary>
    /// 효과 결과 스택
    /// 이전 효과 결과 참조용
    /// </summary>
    public class EffectResultStack
    {
        private Stack<EffectResult> results = new Stack<EffectResult>();

        public void Push(EffectResult result) => results.Push(result);
        public EffectResult Pop() => results.Count > 0 ? results.Pop() : null;
        public EffectResult Peek() => results.Count > 0 ? results.Peek() : null;
        public void Clear() => results.Clear();
        public int Count => results.Count;

        /// <summary>가장 최근 결과의 count 반환 (없으면 0)</summary>
        public int GetLastCount() => results.Count > 0 ? results.Peek().count : 0;

        /// <summary>가장 최근 결과의 value 반환 (없으면 0)</summary>
        public int GetLastValue() => results.Count > 0 ? results.Peek().value : 0;
    }

    // =========================================================================
    // EffectSystem 메인 클래스
    // =========================================================================

    /// <summary>
    /// 카드 효과 실행 시스템 (싱글톤)
    /// </summary>
    public class EffectSystem : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static EffectSystem Instance { get; private set; }

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>카드 효과 실행 시작</summary>
        public static event Action<CardInstance> OnEffectsStarted;

        /// <summary>카드 효과 실행 완료</summary>
        public static event Action<CardInstance> OnEffectsCompleted;

        /// <summary>
        /// 대상 선택 요청
        /// - TargetType: 선택해야 할 대상 종류
        /// - int: 선택해야 할 개수 (0 = 무제한)
        /// - List<CardInstance>: 선택 가능한 카드 목록
        /// - Action: 선택 완료 시 호출할 콜백
        /// </summary>
        public static event Action<TargetType, int, List<CardInstance>, Action<List<CardInstance>>> OnTargetSelectionRequired;

        /// <summary>단일 효과 실행 완료 (디버그/UI용)</summary>
        public static event Action<EffectType, int> OnEffectExecuted;

        // =====================================================================
        // 내부 상태
        // =====================================================================

        /// <summary>대기 중인 효과 (기존 시스템)</summary>
        private class PendingEffect
        {
            public CardEffect effect;
            public CardInstance sourceCard;
        }

        /// <summary>대기 중인 새 효과 (E2 추가)</summary>
        private class PendingNewEffect
        {
            public Effect effect;
            public CardInstance sourceCard;
            public int resolvedValue;  // 미리 계산된 값
        }

        private Queue<PendingEffect> effectQueue = new Queue<PendingEffect>();
        private Queue<PendingNewEffect> newEffectQueue = new Queue<PendingNewEffect>();
        private CardInstance currentCard;
        private PendingEffect currentEffect;
        private PendingNewEffect currentNewEffect;
        private bool isProcessing = false;
        private bool isUsingNewSystem = false;  // 새 시스템 사용 중인지

        // E2 추가: 효과 결과 스택
        private EffectResultStack resultStack = new EffectResultStack();

        // 상태 접근
        private GameState State => GameManager.Instance?.State;

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

            Debug.Log("[EffectSystem] 초기화 완료");
        }

        // =====================================================================
        // 메인 진입점 (기존 - CardEffect[] 사용)
        // =====================================================================

        /// <summary>
        /// 카드의 모든 효과 실행 시작 (기존 시스템)
        /// </summary>
        public void ExecuteEffects(CardInstance card, CardEffect[] effects)
        {
            if (effects == null || effects.Length == 0)
            {
                Debug.Log($"[EffectSystem] 효과 없음: {card.cardDataId}");
                OnEffectsCompleted?.Invoke(card);
                return;
            }

            if (isProcessing)
            {
                Debug.LogWarning("[EffectSystem] 이미 효과 처리 중 - 큐에 추가");
            }

            isUsingNewSystem = false;
            currentCard = card;
            effectQueue.Clear();
            resultStack.Clear();

            foreach (var effect in effects)
            {
                effectQueue.Enqueue(new PendingEffect
                {
                    effect = effect,
                    sourceCard = card
                });
            }

            Debug.Log($"[EffectSystem] 효과 실행 시작: {card.cardDataId} ({effects.Length}개 효과)");
            OnEffectsStarted?.Invoke(card);

            ProcessNextEffect();
        }

        // =====================================================================
        // 메인 진입점 (신규 - ConditionalEffect[] 사용) [E2 추가]
        // =====================================================================

        /// <summary>
        /// 조건부 효과 실행 시작 (새 시스템)
        /// </summary>
        public void ExecuteConditionalEffects(CardInstance card, ConditionalEffect[] conditionalEffects)
        {
            if (conditionalEffects == null || conditionalEffects.Length == 0)
            {
                Debug.Log($"[EffectSystem] 조건부 효과 없음: {card.cardDataId}");
                OnEffectsCompleted?.Invoke(card);
                return;
            }

            if (isProcessing)
            {
                Debug.LogWarning("[EffectSystem] 이미 효과 처리 중 - 큐에 추가");
            }

            isUsingNewSystem = true;
            currentCard = card;
            newEffectQueue.Clear();
            resultStack.Clear();

            Debug.Log($"[EffectSystem] 조건부 효과 실행 시작: {card.cardDataId} ({conditionalEffects.Length}개 조건부 효과)");
            OnEffectsStarted?.Invoke(card);

            foreach (var condEffect in conditionalEffects)
            {
                bool conditionMet = EvaluateConditions(condEffect.conditions);
                Effect[] effectsToRun = conditionMet ? condEffect.effects : condEffect.elseEffects;

                if (effectsToRun == null || effectsToRun.Length == 0)
                {
                    Debug.Log($"[EffectSystem] 조건 {(conditionMet ? "만족" : "불만족")} - 실행할 효과 없음");
                    continue;
                }

                Debug.Log($"[EffectSystem] 조건 {(conditionMet ? "만족" : "불만족")} - {effectsToRun.Length}개 효과 큐잉");

                foreach (var effect in effectsToRun)
                {
                    int resolvedValue = ResolveEffectValue(effect);

                    newEffectQueue.Enqueue(new PendingNewEffect
                    {
                        effect = effect,
                        sourceCard = card,
                        resolvedValue = resolvedValue
                    });
                }
            }

            ProcessNextNewEffect();
        }

        /// <summary>
        /// 이벤트 효과 실행 (카드 없이 호출)
        /// EventSystem에서 사용
        /// </summary>
        public void ExecuteEventEffects(ConditionalEffect[] conditionalEffects)
        {
            if (conditionalEffects == null || conditionalEffects.Length == 0)
            {
                Debug.Log("[EffectSystem] 이벤트 효과 없음");
                return;
            }

            if (isProcessing)
            {
                Debug.LogWarning("[EffectSystem] 이미 효과 처리 중 - 큐에 추가");
            }

            isUsingNewSystem = true;
            currentCard = null;  // 이벤트는 카드 없음
            newEffectQueue.Clear();
            resultStack.Clear();

            Debug.Log($"[EffectSystem] 이벤트 효과 실행 시작 ({conditionalEffects.Length}개 조건부 효과)");

            foreach (var condEffect in conditionalEffects)
            {
                bool conditionMet = EvaluateConditions(condEffect.conditions);
                Effect[] effectsToRun = conditionMet ? condEffect.effects : condEffect.elseEffects;

                if (effectsToRun == null || effectsToRun.Length == 0)
                {
                    Debug.Log($"[EffectSystem] 조건 {(conditionMet ? "만족" : "불만족")} - 실행할 효과 없음");
                    continue;
                }

                Debug.Log($"[EffectSystem] 조건 {(conditionMet ? "만족" : "불만족")} - {effectsToRun.Length}개 효과 큐잉");

                foreach (var effect in effectsToRun)
                {
                    int resolvedValue = ResolveEffectValue(effect);

                    newEffectQueue.Enqueue(new PendingNewEffect
                    {
                        effect = effect,
                        sourceCard = null,  // 이벤트는 카드 없음
                        resolvedValue = resolvedValue
                    });
                }
            }

            if (newEffectQueue.Count > 0)
            {
                isProcessing = true;
                ProcessNextNewEffect();
            }
            else
            {
                Debug.Log("[EffectSystem] 이벤트 효과 완료 (실행할 효과 없음)");
            }
        }


        // =====================================================================
        // 조건 평가 (E2 추가)
        // =====================================================================

        private bool EvaluateConditions(EffectCondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0)
                return true;

            foreach (var condition in conditions)
            {
                if (!EvaluateCondition(condition))
                    return false;
            }
            return true;
        }

        private bool EvaluateCondition(EffectCondition condition)
        {
            if (condition == null || condition.type == ConditionType.None)
                return true;

            switch (condition.type)
            {
                case ConditionType.GoldAbove:
                    return (State?.gold ?? 0) >= condition.value;

                case ConditionType.GoldBelow:
                    return (State?.gold ?? 0) < condition.value;

                case ConditionType.HandHasTreasure:
                    return HasCardOfType(CardType.Treasure);

                case ConditionType.HandHasPollution:
                    return HasCardOfType(CardType.Pollution);

                case ConditionType.HandHasAction:
                    return HasCardOfType(CardType.Action);

                case ConditionType.HandCountAbove:
                    return (State?.hand?.Count ?? 0) >= condition.value;

                case ConditionType.HandCountBelow:
                    return (State?.hand?.Count ?? 0) < condition.value;

                case ConditionType.DeckTopIsTreasure:
                    return IsDeckTopOfType(CardType.Treasure);

                case ConditionType.DeckTopIsPollution:
                    return IsDeckTopOfType(CardType.Pollution);

                case ConditionType.DeckTopIsAction:
                    return IsDeckTopOfType(CardType.Action);

                case ConditionType.DeckNotEmpty:
                    return (State?.deck?.Count ?? 0) > 0;

                case ConditionType.HasUnit:
                    return (State?.units?.Count ?? 0) >= 1;

                case ConditionType.HasMultipleUnits:
                    return (State?.units?.Count ?? 0) >= 2;

                case ConditionType.HasPromotableUnit:
                    return HasPromotableUnit();

                case ConditionType.PreviousEffectSucceeded:
                    return resultStack.Count > 0 && resultStack.Peek().success;

                case ConditionType.PreviousCountAbove:
                    return resultStack.GetLastCount() >= condition.value;

                default:
                    Debug.LogWarning($"[EffectSystem] 미구현 조건 타입: {condition.type}");
                    return true;
            }
        }

        private bool HasCardOfType(CardType cardType)
        {
            if (State?.hand == null) return false;

            foreach (var card in State.hand)
            {
                var data = DataLoader.Instance?.GetCard(card.cardDataId);
                if (data?.cardType == cardType)
                    return true;
            }
            return false;
        }

        private bool IsDeckTopOfType(CardType cardType)
        {
            if (State?.deck == null || State.deck.Count == 0)
                return false;

            var topCard = State.deck[0];
            var data = DataLoader.Instance?.GetCard(topCard.cardDataId);
            return data?.cardType == cardType;
        }

        private bool HasPromotableUnit()
        {
            if (State?.units == null) return false;

            foreach (var unit in State.units)
            {
                if (unit.CanPromote())
                    return true;
            }
            return false;
        }

        // =====================================================================
        // 동적 값 계산 (E2 추가)
        // =====================================================================

        /// <summary>
        /// Effect에서 실제 값 계산 (리팩토링 추가)
        /// dynamicValue가 있으면 동적 계산, 없으면 고정값 사용
        /// </summary>
        public int ResolveEffectValue(Effect effect)
        {
            if (effect == null) return 0;

            // dynamicValue가 있으면 동적 계산
            if (effect.dynamicValue != null && effect.dynamicValue.source != ValueSourceType.Fixed)
            {
                return ResolveValue(effect.dynamicValue);
            }

            // 없으면 고정값 반환
            return effect.value;
        }

        public int ResolveValue(ValueSource source)
        {
            if (source == null)
                return 0;

            int baseResult = 0;

            switch (source.source)
            {
                case ValueSourceType.Fixed:
                    baseResult = source.baseValue;
                    break;

                case ValueSourceType.PreviousCount:
                    baseResult = resultStack.GetLastCount();
                    break;

                case ValueSourceType.PreviousValue:
                    baseResult = resultStack.GetLastValue();
                    break;

                case ValueSourceType.CurrentGold:
                    baseResult = State?.gold ?? 0;
                    break;

                case ValueSourceType.CurrentGoldPercent:
                    baseResult = Mathf.RoundToInt((State?.gold ?? 0) * source.baseValue / 100f);
                    break;

                case ValueSourceType.HandCount:
                    baseResult = State?.hand?.Count ?? 0;
                    break;

                case ValueSourceType.DeckCount:
                    baseResult = State?.deck?.Count ?? 0;
                    break;

                case ValueSourceType.UnitCount:
                    baseResult = State?.units?.Count ?? 0;
                    break;

                case ValueSourceType.DeckTopGoldValue:
                    baseResult = GetDeckTopGoldValue();
                    break;

                case ValueSourceType.TargetGoldValue:
                    baseResult = 0;
                    break;

                case ValueSourceType.RandomRange:
                    baseResult = UnityEngine.Random.Range(source.min, source.max + 1);
                    break;

                case ValueSourceType.RandomDice:
                    baseResult = UnityEngine.Random.Range(1, source.baseValue + 1);
                    break;

                default:
                    Debug.LogWarning($"[EffectSystem] 미구현 값 소스: {source.source}");
                    baseResult = source.baseValue;
                    break;
            }

            return Mathf.RoundToInt(baseResult * source.multiplier);
        }

        private int GetDeckTopGoldValue()
        {
            if (State?.deck == null || State.deck.Count == 0)
                return 0;

            var topCard = State.deck[0];
            var data = DataLoader.Instance?.GetCard(topCard.cardDataId);

            if (data?.cardType == CardType.Treasure)
                return data.goldValue;

            return 0;
        }

        // =====================================================================
        // 효과 처리 흐름 (기존 시스템)
        // =====================================================================

        private void ProcessNextEffect()
        {
            if (effectQueue.Count == 0)
            {
                CompleteAllEffects();
                return;
            }

            isProcessing = true;
            currentEffect = effectQueue.Dequeue();
            var effect = currentEffect.effect;

            Debug.Log($"[EffectSystem] 효과 처리: {effect.effectType} (값: {effect.value})");

            if (RequiresTargetSelection(effect))
            {
                RequestTargetSelection(effect);
            }
            else
            {
                // [E7 수정] 자동 대상 수집 (AllHandTreasure 등)
                var autoTargets = GetAutoTargets(effect.targetType);
                ExecuteEffectImmediate(effect, autoTargets);
            }
        }

        // =====================================================================
        // 효과 처리 흐름 (새 시스템) [E2 추가]
        // =====================================================================

        private void ProcessNextNewEffect()
        {
            if (newEffectQueue.Count == 0)
            {
                CompleteAllEffects();
                return;
            }

            isProcessing = true;
            currentNewEffect = newEffectQueue.Dequeue();
            var effect = currentNewEffect.effect;

            Debug.Log($"[EffectSystem] 새 효과 처리: {effect.type} (값: {currentNewEffect.resolvedValue})");

            if (RequiresTargetSelectionNew(effect))
            {
                RequestTargetSelectionNew(effect);
            }
            else
            {
                // [E7 수정] 자동 대상 수집 (AllHandTreasure 등)
                var autoTargets = GetAutoTargets(effect.target);
                ExecuteNewEffectImmediate(effect, autoTargets, currentNewEffect.resolvedValue);
            }
        }

        private bool RequiresTargetSelectionNew(Effect effect)
        {
            switch (effect.target)
            {
                case TargetType.None:
                case TargetType.Self:
                case TargetType.Random:
                case TargetType.AllHandTreasure:
                case TargetType.AllHandPollution:
                case TargetType.DeckTop:
                    return false;

                case TargetType.HandCard:
                case TargetType.HandTreasure:
                case TargetType.HandPollution:
                case TargetType.HandAction:
                    return true;

                default:
                    return false;
            }
        }

        private void RequestTargetSelectionNew(Effect effect)
        {
            var selectableCards = GetSelectableCards(effect.target);
            int maxTargets = effect.maxTargets > 0 ? effect.maxTargets : selectableCards.Count;

            if (selectableCards.Count == 0)
            {
                Debug.Log($"[EffectSystem] 선택 가능한 대상 없음 - 스킵");
                CompleteCurrentNewEffect(false, 0, 0);
                return;
            }

            Debug.Log($"[EffectSystem] 대상 선택 요청: {effect.target}, 최대 {maxTargets}장");

            if (OnTargetSelectionRequired != null)
            {
                OnTargetSelectionRequired.Invoke(
                    effect.target,
                    maxTargets,
                    selectableCards,
                    OnNewTargetsSelected
                );
            }
            else
            {
                Debug.LogWarning("[EffectSystem] 대상 선택 UI 없음 - 자동 선택");
                var autoSelected = selectableCards.GetRange(0, Mathf.Min(maxTargets, selectableCards.Count));
                OnNewTargetsSelected(autoSelected);
            }
        }

        public void OnNewTargetsSelected(List<CardInstance> selectedTargets)
        {
            if (currentNewEffect == null)
            {
                Debug.LogError("[EffectSystem] 대상 선택됐지만 현재 효과 없음");
                return;
            }

            Debug.Log($"[EffectSystem] 대상 선택 완료: {selectedTargets?.Count ?? 0}장");
            ExecuteNewEffectImmediate(currentNewEffect.effect, selectedTargets, currentNewEffect.resolvedValue);
        }

        private void ExecuteNewEffectImmediate(Effect effect, List<CardInstance> targets, int resolvedValue)
        {
            bool success = true;
            int count = targets?.Count ?? 0;
            int resultValue = 0;

            int actualValue = resolvedValue;
            if (effect.dynamicValue != null && effect.dynamicValue.source != ValueSourceType.Fixed)
            {
                actualValue = ResolveEffectValue(effect);
                Debug.Log($"[EffectSystem] 동적 값 재계산: {effect.dynamicValue.source} -> {actualValue}");
            }

            switch (effect.type)
            {
                case EffectType.DrawCard:
                    DeckSystem.Instance?.DrawCards(actualValue);
                    count = actualValue;
                    Debug.Log($"[EffectSystem] +{actualValue} 카드 드로우");
                    break;

                case EffectType.AddAction:
                    TurnManager.Instance?.AddActions(actualValue);
                    Debug.Log($"[EffectSystem] +{actualValue} 액션");
                    break;

                case EffectType.AddGold:
                    GoldSystem.Instance?.AddGold(actualValue);
                    resultValue = actualValue;
                    Debug.Log($"[EffectSystem] +{actualValue} 골드");
                    break;

                case EffectType.CreateTempTreasure:
                    HandleCreateTempTreasureNew(effect);
                    break;

                case EffectType.BoostTreasure:
                    HandleBoostTreasure(null, targets);
                    break;

                case EffectType.PermanentUpgrade:
                    HandlePermanentUpgrade(null, targets);
                    break;

                case EffectType.SettleCard:
                    int gold = HandleSettleCardNew(targets);
                    count = targets?.Count ?? 0;
                    resultValue = gold;
                    break;

                case EffectType.GoldMultiplier:
                    if (State != null && effect.dynamicValue != null)
                    {
                        State.goldMultiplier *= effect.dynamicValue.multiplier;
                        Debug.Log($"[EffectSystem] 골드 배수: x{State.goldMultiplier}");
                    }
                    break;

                case EffectType.GoldBonus:
                    if (State != null)
                    {
                        State.goldBonus += actualValue;
                        Debug.Log($"[EffectSystem] 골드 보너스: +{State.goldBonus}");
                    }
                    break;

                case EffectType.DestroyCard:
                    HandleDestroyCard(null, targets);
                    break;

                case EffectType.DestroyPollution:
                    HandleDestroyPollution(null, targets);
                    break;

                case EffectType.MoveToDeckBottom:
                    HandleMoveToDeckBottom(null, targets);
                    break;

                case EffectType.Gamble:
                    resultValue = HandleGambleNew(effect);
                    break;

                case EffectType.RevealAndGain:
                    HandleRevealAndGain(null);
                    break;

                case EffectType.DelayedGold:
                    HandleDelayedGoldNew(effect, actualValue);
                    break;

                case EffectType.PersistentGold:
                    HandlePersistentGoldNew(effect, actualValue);
                    break;

                case EffectType.PersistentMaintenance:
                    HandlePersistentMaintenanceNew(effect, actualValue);
                    break;

                case EffectType.DrawUntil:
                    HandleDrawUntilNew(actualValue);
                    break;

                case EffectType.IgnorePollution:
                    if (State != null) State.pollutionIgnored = true;
                    Debug.Log("[EffectSystem] 이번 턴 오염 효과 무시");
                    break;

                // [E7 추가] 덱 셔플
                case EffectType.ShuffleDeck:
                    DeckSystem.Instance?.ShuffleDeck();
                    Debug.Log("[EffectSystem] 덱 셔플");
                    break;

                // === 이벤트 전용 효과 (E8 추가) ===

                case EffectType.GainUnit:
                    HandleGainUnit(effect);
                    count = 1;
                    break;

                case EffectType.RemoveUnit:
                    HandleRemoveUnit(effect);
                    count = 1;
                    break;

                case EffectType.FreePromotion:
                    HandleFreePromotion();
                    count = 1;
                    break;

                case EffectType.AddPromotionLevel:
                    HandleAddPromotionLevel(effect, actualValue);
                    count = 1;
                    break;

                case EffectType.AddCardToDeck:
                    HandleAddCardToDeck(effect, actualValue);
                    count = actualValue > 0 ? actualValue : 1;
                    break;

                case EffectType.RemoveCardFromDeck:
                    count = HandleRemoveCardFromDeck(effect, actualValue);
                    break;

                case EffectType.UpgradeCardInDeck:
                    HandleUpgradeCardInDeck(effect);
                    count = 1;
                    break;

                case EffectType.SpendGoldPercent:
                    resultValue = HandleSpendGoldPercent(actualValue);
                    break;

                case EffectType.PromotionDiscount:
                    HandlePromotionDiscount(actualValue);
                    break;

                case EffectType.MaintenanceModifier:
                    HandleMaintenanceModifier(effect, actualValue);
                    break;


                default:
                    Debug.LogWarning($"[EffectSystem] 미구현 효과: {effect.type}");
                    break;
            }

            CompleteCurrentNewEffect(success, count, resultValue);
        }

        private void CompleteCurrentNewEffect(bool success, int count, int value)
        {
            resultStack.Push(new EffectResult
            {
                effectType = currentNewEffect.effect.type,
                success = success,
                count = count,
                value = value
            });

            OnEffectExecuted?.Invoke(currentNewEffect.effect.type, currentNewEffect.resolvedValue);
            ProcessNextNewEffect();
        }

        // =====================================================================
        // 새 시스템용 핸들러 (E2 추가)
        // =====================================================================

        private void HandleCreateTempTreasureNew(Effect effect)
        {
            string cardId = GetTreasureCardId(effect.createGrade);
            if (!string.IsNullOrEmpty(cardId))
            {
                DeckSystem.Instance?.AddCardToHand(cardId, isTemporary: true);
                Debug.Log($"[EffectSystem] 임시 {effect.createGrade} 생성");
            }
        }

        private int HandleSettleCardNew(List<CardInstance> targets)
        {
            if (targets == null || targets.Count == 0) return 0;

            int totalGold = DeckSystem.Instance?.SettleCards(targets) ?? 0;

            if (totalGold > 0)
            {
                GoldSystem.Instance?.AddGold(totalGold);
            }

            Debug.Log($"[EffectSystem] 정산 완료: 총 {totalGold} 골드 ({targets.Count}장)");
            return totalGold;
        }

        private int HandleGambleNew(Effect effect)
        {
            int roll = UnityEngine.Random.Range(0, 100);
            bool success = roll < effect.successChance;

            int successVal = effect.successValue != null ? ResolveValue(effect.successValue) : 0;
            int failVal = effect.failValue != null ? ResolveValue(effect.failValue) : 0;

            int result = success ? successVal : failVal;
            GoldSystem.Instance?.AddGold(result);

            Debug.Log($"[EffectSystem] 도박: {(success ? "성공" : "실패")} ({roll}% < {effect.successChance}%) -> {result} 골드");
            return result;
        }

        private void HandleDelayedGoldNew(Effect effect, int value)
        {
            if (State == null) return;

            int duration = effect.duration > 0 ? effect.duration : 1;

            State.activeEffects.Add(new PersistentEffect
            {
                effectId = Guid.NewGuid().ToString(),
                type = PersistentEffectType.DelayedGold,
                value = value,
                remainingTurns = duration
            });

            Debug.Log($"[EffectSystem] {duration}턴 후 +{value} 골드 예약");
        }

        private void HandlePersistentGoldNew(Effect effect, int value)
        {
            if (State == null) return;

            int duration = effect.duration > 0 ? effect.duration : 1;

            State.activeEffects.Add(new PersistentEffect
            {
                effectId = Guid.NewGuid().ToString(),
                type = PersistentEffectType.GoldPerTurn,
                value = value,
                remainingTurns = duration
            });

            Debug.Log($"[EffectSystem] {duration}턴간 매 턴 +{value} 골드");
        }

        private void HandlePersistentMaintenanceNew(Effect effect, int value)
        {
            if (State == null) return;

            int duration = effect.duration > 0 ? effect.duration : 1;

            State.activeEffects.Add(new PersistentEffect
            {
                effectId = Guid.NewGuid().ToString(),
                type = PersistentEffectType.MaintenanceIncrease,
                value = value,
                remainingTurns = duration
            });

            Debug.Log($"[EffectSystem] {duration}턴간 유지비 +{value}");
        }

        private void HandleDrawUntilNew(int targetCount)
        {
            if (State == null) return;

            int currentCount = State.hand.Count;
            if (currentCount >= targetCount)
            {
                Debug.Log($"[EffectSystem] 이미 손패 {currentCount}장 >= {targetCount}장");
                return;
            }

            int drawCount = targetCount - currentCount;
            DeckSystem.Instance?.DrawCards(drawCount);
            Debug.Log($"[EffectSystem] 손패 {targetCount}장까지 +{drawCount} 드로우");
        }

        // =====================================================================
        // 모든 효과 완료
        // =====================================================================

        private void CompleteAllEffects()
        {
            isProcessing = false;
            var completedCard = currentCard;
            currentCard = null;
            currentEffect = null;
            currentNewEffect = null;

            Debug.Log($"[EffectSystem] 모든 효과 완료: {completedCard?.cardDataId}");
            OnEffectsCompleted?.Invoke(completedCard);
        }

        private void CompleteCurrentEffect()
        {
            OnEffectExecuted?.Invoke(currentEffect.effect.effectType, currentEffect.effect.value);
            ProcessNextEffect();
        }

        // =====================================================================
        // 대상 선택 (기존 시스템)
        // =====================================================================

        private bool RequiresTargetSelection(CardEffect effect)
        {
            switch (effect.targetType)
            {
                case TargetType.None:
                case TargetType.Self:
                case TargetType.Random:
                case TargetType.AllHandTreasure:
                case TargetType.AllHandPollution:
                case TargetType.DeckTop:
                    return false;

                case TargetType.HandCard:
                case TargetType.HandTreasure:
                case TargetType.HandPollution:
                case TargetType.HandAction:
                    return true;

                default:
                    return false;
            }
        }

        private void RequestTargetSelection(CardEffect effect)
        {
            var selectableCards = GetSelectableCards(effect.targetType);

            if (effect.effectType == EffectType.PermanentUpgrade)
            {
                selectableCards = selectableCards.FindAll(c => !c.isTemporary);
            }

            int maxTargets = effect.maxTargets > 0 ? effect.maxTargets : selectableCards.Count;

            if (selectableCards.Count == 0)
            {
                Debug.Log($"[EffectSystem] 선택 가능한 대상 없음 - 스킵");
                CompleteCurrentEffect();
                return;
            }

            Debug.Log($"[EffectSystem] 대상 선택 요청: {effect.targetType}, 최대 {maxTargets}장");

            if (OnTargetSelectionRequired != null)
            {
                OnTargetSelectionRequired.Invoke(
                    effect.targetType,
                    maxTargets,
                    selectableCards,
                    OnTargetsSelected
                );
            }
            else
            {
                Debug.LogWarning("[EffectSystem] 대상 선택 UI 없음 - 자동 선택");
                var autoSelected = selectableCards.GetRange(0, Mathf.Min(maxTargets, selectableCards.Count));
                OnTargetsSelected(autoSelected);
            }
        }

        public void OnTargetsSelected(List<CardInstance> selectedTargets)
        {
            if (currentEffect == null)
            {
                Debug.LogError("[EffectSystem] 대상 선택됐지만 현재 효과 없음");
                return;
            }

            Debug.Log($"[EffectSystem] 대상 선택 완료: {selectedTargets?.Count ?? 0}장");
            ExecuteEffectImmediate(currentEffect.effect, selectedTargets);
        }

        private List<CardInstance> GetSelectableCards(TargetType targetType)
        {
            if (State == null) return new List<CardInstance>();

            switch (targetType)
            {
                case TargetType.HandCard:
                    return new List<CardInstance>(State.hand);

                case TargetType.HandTreasure:
                    return State.hand.FindAll(c =>
                    {
                        var data = DataLoader.Instance?.GetCard(c.cardDataId);
                        return data?.cardType == CardType.Treasure;
                    });

                case TargetType.HandPollution:
                    return State.hand.FindAll(c =>
                    {
                        var data = DataLoader.Instance?.GetCard(c.cardDataId);
                        return data?.cardType == CardType.Pollution;
                    });

                case TargetType.HandAction:
                    return State.hand.FindAll(c =>
                    {
                        var data = DataLoader.Instance?.GetCard(c.cardDataId);
                        return data?.cardType == CardType.Action;
                    });

                // [E7 추가] 전체 대상 타겟
                case TargetType.AllHandTreasure:
                    return State.hand.FindAll(c =>
                    {
                        var data = DataLoader.Instance?.GetCard(c.cardDataId);
                        return data?.cardType == CardType.Treasure;
                    });

                case TargetType.AllHandPollution:
                    return State.hand.FindAll(c =>
                    {
                        var data = DataLoader.Instance?.GetCard(c.cardDataId);
                        return data?.cardType == CardType.Pollution;
                    });

                default:
                    return new List<CardInstance>();
            }
        }

        /// <summary>
        /// [E7 추가] 자동 대상 수집
        /// AllHandTreasure, AllHandPollution 등 전체 대상 타겟용
        /// </summary>
        private List<CardInstance> GetAutoTargets(TargetType targetType)
        {
            switch (targetType)
            {
                case TargetType.AllHandTreasure:
                case TargetType.AllHandPollution:
                    var targets = GetSelectableCards(targetType);
                    Debug.Log($"[EffectSystem] 자동 대상 수집: {targetType} -> {targets.Count}장");
                    return targets;
                default:
                    return null;
            }
        }

        // =====================================================================
        // 효과 실행 (기존 핸들러 분기)
        // =====================================================================

        private void ExecuteEffectImmediate(CardEffect effect, List<CardInstance> targets)
        {
            switch (effect.effectType)
            {
                case EffectType.DrawCard:
                    HandleDrawCard(effect);
                    break;

                case EffectType.AddAction:
                    HandleAddAction(effect);
                    break;

                case EffectType.AddGold:
                    HandleAddGold(effect);
                    break;

                case EffectType.CreateTempTreasure:
                    HandleCreateTempTreasure(effect);
                    break;

                case EffectType.BoostTreasure:
                    HandleBoostTreasure(effect, targets);
                    break;

                case EffectType.PermanentUpgrade:
                    HandlePermanentUpgrade(effect, targets);
                    break;

                case EffectType.SettleCard:
                    HandleSettleCard(effect, targets);
                    break;

                case EffectType.GoldMultiplier:
                    HandleGoldMultiplier(effect);
                    break;

                case EffectType.GoldBonus:
                    HandleGoldBonus(effect);
                    break;

                case EffectType.DestroyCard:
                    HandleDestroyCard(effect, targets);
                    break;

                case EffectType.DestroyPollution:
                    HandleDestroyPollution(effect, targets);
                    break;

                case EffectType.MoveToDeckBottom:
                    HandleMoveToDeckBottom(effect, targets);
                    break;

                case EffectType.Gamble:
                    HandleGamble(effect);
                    break;

                case EffectType.RevealAndGain:
                    HandleRevealAndGain(effect);
                    break;

                case EffectType.DelayedGold:
                    HandleDelayedGold(effect);
                    break;

                case EffectType.PersistentGold:
                    HandlePersistentGold(effect);
                    break;

                case EffectType.PersistentMaintenance:
                    HandlePersistentMaintenance(effect);
                    break;

                case EffectType.DrawUntil:
                    HandleDrawUntil(effect);
                    break;

                case EffectType.IgnorePollution:
                    HandleIgnorePollution(effect);
                    break;

                // [E7 추가] 덱 셔플
                case EffectType.ShuffleDeck:
                    DeckSystem.Instance?.ShuffleDeck();
                    Debug.Log("[EffectSystem] 덱 셔플");
                    break;

                default:
                    Debug.LogWarning($"[EffectSystem] 미구현 효과: {effect.effectType}");
                    break;
            }

            CompleteCurrentEffect();
        }

        // =====================================================================
        // 기존 효과 핸들러들
        // =====================================================================

        private void HandleDrawCard(CardEffect effect)
        {
            DeckSystem.Instance?.DrawCards(effect.value);
            Debug.Log($"[EffectSystem] +{effect.value} 카드 드로우");
        }

        private void HandleAddAction(CardEffect effect)
        {
            TurnManager.Instance?.AddActions(effect.value);
            Debug.Log($"[EffectSystem] +{effect.value} 액션");
        }

        private void HandleAddGold(CardEffect effect)
        {
            GoldSystem.Instance?.AddGold(effect.value);
            Debug.Log($"[EffectSystem] +{effect.value} 골드");
        }

        private void HandleCreateTempTreasure(CardEffect effect)
        {
            string cardId = GetTreasureCardId(effect.createGrade);
            if (!string.IsNullOrEmpty(cardId))
            {
                DeckSystem.Instance?.AddCardToHand(cardId, isTemporary: true);
                Debug.Log($"[EffectSystem] 임시 {effect.createGrade} 생성");
            }
        }

        private void HandleBoostTreasure(CardEffect effect, List<CardInstance> targets)
        {
            if (targets == null || targets.Count == 0) return;

            int boostValue = effect?.value ?? 1;

            foreach (var card in targets)
            {
                var cardData = DataLoader.Instance?.GetCard(card.cardDataId);
                if (cardData?.cardType != CardType.Treasure) continue;

                var currentGrade = card.boostedGrade ?? cardData.treasureGrade;
                int newGradeValue = (int)currentGrade + boostValue;
                newGradeValue = Mathf.Min(newGradeValue, (int)TreasureGrade.Diamond);

                card.isBoostedThisTurn = true;
                card.boostedGrade = (TreasureGrade)newGradeValue;

                Debug.Log($"[EffectSystem] {cardData.cardName} -> {card.boostedGrade} 부스트");
            }

            DeckSystem.Instance?.NotifyHandChanged();
        }

        private void HandlePermanentUpgrade(CardEffect effect, List<CardInstance> targets)
        {
            if (targets == null || targets.Count == 0) return;

            foreach (var card in targets)
            {
                var cardData = DataLoader.Instance?.GetCard(card.cardDataId);
                if (cardData?.cardType != CardType.Treasure) continue;

                var nextGrade = TreasureGradeUtil.GetNextGrade(cardData.treasureGrade);
                if (!nextGrade.HasValue)
                {
                    Debug.Log($"[EffectSystem] {cardData.cardName}은 이미 최고 등급");
                    continue;
                }

                string newCardId = GetTreasureCardId(nextGrade.Value);
                if (string.IsNullOrEmpty(newCardId)) continue;

                State.hand.Remove(card);
                var newCard = CardInstance.Create(newCardId, card.ownerUnitId);
                State.hand.Add(newCard);

                Debug.Log($"[EffectSystem] 영구 업그레이드: {cardData.cardName} -> {TreasureGradeUtil.GetName(nextGrade.Value)}");
            }

            DeckSystem.Instance?.NotifyHandChanged();
        }

        private void HandleSettleCard(CardEffect effect, List<CardInstance> targets)
        {
            if (targets == null || targets.Count == 0) return;

            int totalGold = DeckSystem.Instance?.SettleCards(targets) ?? 0;

            if (totalGold > 0)
            {
                GoldSystem.Instance?.AddGold(totalGold);
            }

            Debug.Log($"[EffectSystem] 정산 완료: 총 {totalGold} 골드");
        }

        private void HandleGoldMultiplier(CardEffect effect)
        {
            if (State == null) return;
            State.goldMultiplier *= effect.floatValue;
            Debug.Log($"[EffectSystem] 골드 배수: x{State.goldMultiplier}");
        }

        private void HandleGoldBonus(CardEffect effect)
        {
            if (State == null) return;
            State.goldBonus += effect.value;
            Debug.Log($"[EffectSystem] 골드 보너스: +{State.goldBonus}");
        }

        private void HandleDestroyCard(CardEffect effect, List<CardInstance> targets)
        {
            if (targets == null) return;

            foreach (var card in targets)
            {
                DeckSystem.Instance?.DestroyCard(card);
                Debug.Log($"[EffectSystem] 카드 소멸: {card.cardDataId}");
            }
        }

        private void HandleDestroyPollution(CardEffect effect, List<CardInstance> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                targets = GetSelectableCards(TargetType.HandPollution);
                int maxDestroy = effect?.value > 0 ? effect.value : targets.Count;
                targets = targets.GetRange(0, Mathf.Min(maxDestroy, targets.Count));
            }

            foreach (var card in targets)
            {
                DeckSystem.Instance?.DestroyCard(card);
                Debug.Log($"[EffectSystem] 오염 카드 소멸: {card.cardDataId}");
            }
        }

        private void HandleMoveToDeckBottom(CardEffect effect, List<CardInstance> targets)
        {
            if (targets == null || targets.Count == 0) return;

            DeckSystem.Instance?.MoveCardsToDeckBottom(targets);
            Debug.Log($"[EffectSystem] 덱 맨 아래로 이동: {targets.Count}장");
        }

        private void HandleGamble(CardEffect effect)
        {
            int roll = UnityEngine.Random.Range(0, 100);
            bool success = roll < effect.successChance;

            int result = success ? effect.successValue : effect.failValue;
            GoldSystem.Instance?.AddGold(result);

            Debug.Log($"[EffectSystem] 도박: {(success ? "성공" : "실패")} ({roll}% < {effect.successChance}%) -> {result} 골드");
        }

        private void HandleRevealAndGain(CardEffect effect)
        {
            if (State == null) return;

            int revealCount = effect?.value ?? 3;
            int actualCount = Mathf.Min(revealCount, State.deck.Count);

            if (actualCount == 0)
            {
                Debug.Log("[EffectSystem] RevealAndGain: 덱이 비어있음");
                return;
            }

            Debug.Log($"[EffectSystem] 덱에서 {actualCount}장 공개");

            List<CardInstance> revealedCards = new List<CardInstance>();
            for (int i = 0; i < actualCount; i++)
            {
                revealedCards.Add(State.deck[i]);
            }

            List<CardInstance> treasureCards = new List<CardInstance>();
            List<CardInstance> otherCards = new List<CardInstance>();

            foreach (var card in revealedCards)
            {
                var cardData = DataLoader.Instance?.GetCard(card.cardDataId);
                if (cardData?.cardType == CardType.Treasure)
                {
                    treasureCards.Add(card);
                    Debug.Log($"[EffectSystem] 공개 - 재화: {cardData.cardName} -> 손패로");
                }
                else
                {
                    otherCards.Add(card);
                    Debug.Log($"[EffectSystem] 공개 - 기타: {cardData?.cardName ?? card.cardDataId} -> 버림더미로");
                }
            }

            for (int i = actualCount - 1; i >= 0; i--)
            {
                State.deck.RemoveAt(i);
            }

            foreach (var card in treasureCards)
            {
                State.hand.Add(card);
            }

            foreach (var card in otherCards)
            {
                State.discardPile.Add(card);
            }

            DeckSystem.Instance?.NotifyHandChanged();
            Debug.Log($"[EffectSystem] RevealAndGain 완료: 재화 {treasureCards.Count}장 획득, {otherCards.Count}장 버림");
        }

        private void HandleDelayedGold(CardEffect effect)
        {
            if (State == null) return;

            State.activeEffects.Add(new PersistentEffect
            {
                effectId = Guid.NewGuid().ToString(),
                type = PersistentEffectType.DelayedGold,
                value = effect.value,
                remainingTurns = effect.duration
            });

            Debug.Log($"[EffectSystem] {effect.duration}턴 후 +{effect.value} 골드 예약");
        }

        private void HandlePersistentGold(CardEffect effect)
        {
            if (State == null) return;

            State.activeEffects.Add(new PersistentEffect
            {
                effectId = Guid.NewGuid().ToString(),
                type = PersistentEffectType.GoldPerTurn,
                value = effect.value,
                remainingTurns = effect.duration
            });

            Debug.Log($"[EffectSystem] {effect.duration}턴간 매 턴 +{effect.value} 골드");
        }

        private void HandlePersistentMaintenance(CardEffect effect)
        {
            if (State == null) return;

            State.activeEffects.Add(new PersistentEffect
            {
                effectId = Guid.NewGuid().ToString(),
                type = PersistentEffectType.MaintenanceIncrease,
                value = effect.value,
                remainingTurns = effect.duration
            });

            Debug.Log($"[EffectSystem] {effect.duration}턴간 유지비 +{effect.value}");
        }

        private void HandleDrawUntil(CardEffect effect)
        {
            if (State == null) return;

            int targetCount = effect.value;
            int currentCount = State.hand.Count;

            if (currentCount >= targetCount)
            {
                Debug.Log($"[EffectSystem] 이미 손패 {currentCount}장 >= {targetCount}장");
                return;
            }

            int drawCount = targetCount - currentCount;
            DeckSystem.Instance?.DrawCards(drawCount);
            Debug.Log($"[EffectSystem] 손패 {targetCount}장까지 +{drawCount} 드로우");
        }

        private void HandleIgnorePollution(CardEffect effect)
        {
            if (State == null) return;
            State.pollutionIgnored = true;
            Debug.Log("[EffectSystem] 이번 턴 오염 효과 무시");
        }

        // =====================================================================
        // 이벤트 전용 핸들러 (E8 추가)
        // =====================================================================

        private void HandleGainUnit(Effect effect)
        {
            if (State == null) return;

            // cardId를 직업 힌트로 사용
            Job job = Job.Pawn;
            if (!string.IsNullOrEmpty(effect.cardId))
            {
                switch (effect.cardId.ToLower())
                {
                    case "knight": job = Job.Knight; break;
                    case "bishop": job = Job.Bishop; break;
                    case "rook": job = Job.Rook; break;
                    case "queen": job = Job.Queen; break;
                    default: job = Job.Pawn; break;
                }
            }

            // [E3] UnitSystem.CreateUnit 사용 (시작 카드 자동 부여)
            string unitName = $"영입 {State.units.Count + 1}";
            var newUnit = UnitSystem.Instance?.CreateUnit(unitName, job, GrowthStage.Young);

            // [E3] 집에 자동 배치
            if (newUnit != null)
            {
                HouseSystem.Instance?.AutoPlaceUnit(newUnit);
            }

            Debug.Log($"[EffectSystem] 유닛 획득: {unitName} ({job})");
        }

        /// <summary>
        /// 유닛 제거/희생 (신비한 제단 등)
        /// 랜덤 유닛 1명 제거
        /// </summary>
        private void HandleRemoveUnit(Effect effect)
        {
            if (State == null || State.units.Count == 0) return;

            // 랜덤 유닛 선택
            int index = UnityEngine.Random.Range(0, State.units.Count);
            var victim = State.units[index];

            // UnitSystem을 통해 제거 (이벤트 발생 + 종속 카드 제거 + 집 슬롯 정리)
            UnitSystem.Instance?.KillUnit(victim);
        }

        /// <summary>
        /// 무료 전직 (이벤트 효과)
        /// 전직 가능한 유닛 중 1명에게 무료 전직 팝업 표시
        /// </summary>
        private void HandleFreePromotion()
        {
            if (State == null) return;

            // 전직 가능한 유닛 찾기
            var promotableUnits = new List<UnitInstance>();
            foreach (var unit in State.units)
            {
                if (unit.CanPromote())
                {
                    promotableUnits.Add(unit);
                }
            }

            if (promotableUnits.Count == 0)
            {
                Debug.Log("[EffectSystem] 전직 가능한 유닛 없음");
                return;
            }

            // 랜덤 선택
            int index = UnityEngine.Random.Range(0, promotableUnits.Count);
            var target = promotableUnits[index];

            // [E3] 무료 전직 팝업 요청 (직접 레벨 증가 X)
            UnitSystem.Instance?.RequestFreePromotion(target);

            Debug.Log($"[EffectSystem] 무료 전직 요청: {target.unitName}");
        }

        /// <summary>
        /// 전직 레벨 추가 (신비한 제단 보상 등)
        /// </summary>
        private void HandleAddPromotionLevel(Effect effect, int levels)
        {
            if (State == null || State.units.Count == 0) return;

            // 랜덤 유닛 선택 (희생된 유닛 외)
            int index = UnityEngine.Random.Range(0, State.units.Count);
            var target = State.units[index];

            int oldLevel = target.promotionLevel;
            target.promotionLevel = Mathf.Min(3, target.promotionLevel + levels);
            target.RecalculateCombatPower();

            Debug.Log($"[EffectSystem] 전직 레벨 추가: {target.unitName} Lv.{oldLevel} -> Lv.{target.promotionLevel}");
        }

        /// <summary>
        /// 덱에 카드 추가 (수거집 보물, 오염 카드 추가 등)
        /// </summary>
        private void HandleAddCardToDeck(Effect effect, int count)
        {
            if (State == null) return;

            string cardId = effect.cardId;
            if (string.IsNullOrEmpty(cardId))
            {
                Debug.LogWarning("[EffectSystem] AddCardToDeck: cardId가 없음");
                return;
            }

            var cardData = DataLoader.Instance?.GetCard(cardId);
            if (cardData == null)
            {
                Debug.LogWarning($"[EffectSystem] AddCardToDeck: 카드 '{cardId}' 없음");
                return;
            }

            int addCount = count > 0 ? count : 1;
            for (int i = 0; i < addCount; i++)
            {
                var newCard = CardInstance.Create(cardId);
                State.discardPile.Add(newCard);
            }

            Debug.Log($"[EffectSystem] 덱에 카드 추가: {cardData.cardName} x{addCount}");
        }

        /// <summary>
        /// 덱에서 카드 제거 (정화의 바람 등)
        /// 오염 카드 또는 특정 카드 제거
        /// </summary>
        private int HandleRemoveCardFromDeck(Effect effect, int count)
        {
            if (State == null) return 0;

            int removeCount = count > 0 ? count : 1;
            int removed = 0;

            for (int i = 0; i < removeCount; i++)
            {
                CardInstance target = null;
                List<CardInstance> sourceList = null;

                // 특정 카드 ID가 있으면 해당 카드 찾기
                if (!string.IsNullOrEmpty(effect.cardId))
                {
                    target = FindCardInAllZones(effect.cardId, out sourceList);
                }
                else
                {
                    // 카드 ID가 없으면 오염 카드 찾기
                    target = FindPollutionCardInAllZones(out sourceList);
                }

                if (target != null && sourceList != null)
                {
                    sourceList.Remove(target);
                    removed++;

                    var cardData = DataLoader.Instance?.GetCard(target.cardDataId);
                    Debug.Log($"[EffectSystem] 카드 제거: {cardData?.cardName ?? target.cardDataId}");
                }
            }

            if (removed == 0)
            {
                Debug.Log("[EffectSystem] 제거할 카드 없음");
            }

            return removed;
        }

        /// <summary>
        /// 덱 카드 업그레이드 (장인의 축복: 동화->은화)
        /// </summary>
        private void HandleUpgradeCardInDeck(Effect effect)
        {
            if (State == null) return;

            string fromCardId = effect.cardId; // "copper"
            if (string.IsNullOrEmpty(fromCardId))
            {
                fromCardId = "copper"; // 기본값
            }

            // 업그레이드 대상 카드 찾기
            List<CardInstance> sourceList;
            var target = FindCardInAllZones(fromCardId, out sourceList);

            if (target == null)
            {
                Debug.Log($"[EffectSystem] 업그레이드할 {fromCardId} 없음");
                return;
            }

            // 업그레이드 매핑
            string toCardId = fromCardId switch
            {
                "copper" => "silver",
                "silver" => "gold_coin",
                "gold_coin" => "emerald",
                _ => null
            };

            if (toCardId == null)
            {
                Debug.Log($"[EffectSystem] {fromCardId}는 업그레이드 불가");
                return;
            }

            // 교체
            int index = sourceList.IndexOf(target);
            sourceList.RemoveAt(index);
            var upgraded = CardInstance.Create(toCardId, target.ownerUnitId);
            sourceList.Insert(index, upgraded);

            var fromData = DataLoader.Instance?.GetCard(fromCardId);
            var toData = DataLoader.Instance?.GetCard(toCardId);
            Debug.Log($"[EffectSystem] 카드 업그레이드: {fromData?.cardName} -> {toData?.cardName}");
        }

        /// <summary>
        /// 골드 비율 소모 (도적 습격: 20% 손실)
        /// </summary>
        private int HandleSpendGoldPercent(int percent)
        {
            if (State == null || GoldSystem.Instance == null) return 0;

            int currentGold = GoldSystem.Instance.CurrentGold;
            int loss = Mathf.RoundToInt(currentGold * percent / 100f);

            if (loss > 0)
            {
                GoldSystem.Instance.SubtractGold(loss);
                Debug.Log($"[EffectSystem] 골드 {percent}% 손실: -{loss} (현재: {currentGold - loss})");
            }

            return loss;
        }

        /// <summary>
        /// 전직 할인 (행상인 방문: 이번 턴 50% 할인)
        /// </summary>
        private void HandlePromotionDiscount(int discountPercent)
        {
            if (State == null) return;

            State.upgradeDiscount = discountPercent;
            Debug.Log($"[EffectSystem] 전직 할인: {discountPercent}%");
        }

        /// <summary>
        /// 유지비 수정 (흉년: 3턴간 유지비 +2)
        /// </summary>
        private void HandleMaintenanceModifier(Effect effect, int value)
        {
            if (State == null) return;

            int duration = effect.duration > 0 ? effect.duration : 3;

            State.activeEffects.Add(new PersistentEffect
            {
                effectId = Guid.NewGuid().ToString(),
                type = PersistentEffectType.MaintenanceIncrease,
                value = value,
                remainingTurns = duration
            });

            Debug.Log($"[EffectSystem] {duration}턴간 유지비 +{value}");
        }

        // =====================================================================
        // 이벤트 전용 유틸리티 메소드 (E8 추가)
        // =====================================================================

        /// <summary>
        /// 모든 영역에서 특정 카드 ID 찾기
        /// </summary>
        private CardInstance FindCardInAllZones(string cardId, out List<CardInstance> sourceList)
        {
            // 덱에서 찾기
            foreach (var card in State.deck)
            {
                if (card.cardDataId == cardId)
                {
                    sourceList = State.deck;
                    return card;
                }
            }

            // 버림더미에서 찾기
            foreach (var card in State.discardPile)
            {
                if (card.cardDataId == cardId)
                {
                    sourceList = State.discardPile;
                    return card;
                }
            }

            // 손패에서 찾기 (보통 이벤트에서는 손패 제외하지만 일단 포함)
            foreach (var card in State.hand)
            {
                if (card.cardDataId == cardId)
                {
                    sourceList = State.hand;
                    return card;
                }
            }

            sourceList = null;
            return null;
        }

        /// <summary>
        /// 모든 영역에서 오염 카드 찾기
        /// </summary>
        private CardInstance FindPollutionCardInAllZones(out List<CardInstance> sourceList)
        {
            // 덱에서 찾기
            foreach (var card in State.deck)
            {
                var data = DataLoader.Instance?.GetCard(card.cardDataId);
                if (data?.cardType == CardType.Pollution)
                {
                    sourceList = State.deck;
                    return card;
                }
            }

            // 버림더미에서 찾기
            foreach (var card in State.discardPile)
            {
                var data = DataLoader.Instance?.GetCard(card.cardDataId);
                if (data?.cardType == CardType.Pollution)
                {
                    sourceList = State.discardPile;
                    return card;
                }
            }

            // 손패에서 찾기
            foreach (var card in State.hand)
            {
                var data = DataLoader.Instance?.GetCard(card.cardDataId);
                if (data?.cardType == CardType.Pollution)
                {
                    sourceList = State.hand;
                    return card;
                }
            }

            sourceList = null;
            return null;
        }

        /// <summary>
        /// 모든 영역에서 특정 인스턴스 ID의 카드 제거
        /// </summary>
        private void RemoveCardFromAllZones(string instanceId)
        {
            State.deck.RemoveAll(c => c.instanceId == instanceId);
            State.hand.RemoveAll(c => c.instanceId == instanceId);
            State.discardPile.RemoveAll(c => c.instanceId == instanceId);
            State.playArea.RemoveAll(c => c.instanceId == instanceId);
        }


        // =====================================================================
        // 유틸리티
        // =====================================================================

        private string GetTreasureCardId(TreasureGrade grade)
        {
            switch (grade)
            {
                case TreasureGrade.Copper: return "copper";
                case TreasureGrade.Silver: return "silver";
                case TreasureGrade.Gold: return "gold_coin";
                case TreasureGrade.Emerald: return "emerald";
                case TreasureGrade.Sapphire: return "sapphire";
                case TreasureGrade.Ruby: return "ruby";
                case TreasureGrade.Diamond: return "diamond";
                default: return null;
            }
        }

        public bool IsProcessing => isProcessing;

        public void CancelTargetSelection()
        {
            if (!isProcessing) return;

            Debug.Log("[EffectSystem] 대상 선택 취소");

            if (isUsingNewSystem && currentNewEffect != null)
            {
                CompleteCurrentNewEffect(false, 0, 0);
            }
            else if (currentEffect != null)
            {
                CompleteCurrentEffect();
            }
        }
    }
}