// =============================================================================
// EventSystem.cs
// 랜덤 이벤트 시스템 (JSON 데이터 기반 - E8 재작성)
// =============================================================================
//
// [역할]
// - 턴 시작 시 랜덤 이벤트 발생 판정
// - JSON에서 로드한 EventData 기반으로 이벤트 처리
// - EffectSystem의 ConditionalEffect 재사용
// - 선택 이벤트 UI 연동 (N개 선택지 지원)
//
// [이벤트 발생 규칙]
// - 80% 확률로 이벤트 발생
// - 1턴: 이벤트 없음
// - 검증 턴: 부정 이벤트 제외
//
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Core
{
    /// <summary>
    /// 랜덤 이벤트 종류
    /// </summary>
    public enum RandomEventCategory
    {
        Positive = 1,   // 긍정 이벤트 (50%)
        Negative = 2,   // 부정 이벤트 (20%)
        Choice = 3      // 선택 이벤트 (30%)
    }

    /// <summary>
    /// 선택지 정보 (UI 전달용)
    /// </summary>
    public class EventChoiceInfo
    {
        public int index;           // 선택지 인덱스
        public string choiceId;     // 선택지 ID
        public string choiceText;   // 선택지 텍스트
        public bool canSelect;      // 선택 가능 여부
    }

    /// <summary>
    /// 이벤트 시스템 (싱글톤) - JSON 데이터 기반
    /// </summary>
    public class EventSystem : MonoBehaviour
    {
        // =====================================================================
        // 싱글톤
        // =====================================================================

        public static EventSystem Instance { get; private set; }

        // =====================================================================
        // 이벤트 (C# Event)
        // =====================================================================

        /// <summary>랜덤 이벤트 발생 (이벤트 ID, 카테고리)</summary>
        public static event Action<string, RandomEventCategory> OnRandomEventTriggered;

        /// <summary>
        /// 선택 이벤트 선택 필요 (새 시그니처 - N개 선택지 지원)
        /// (이벤트 ID, 설명, 선택지 목록, 콜백)
        /// </summary>
        public static event Action<string, string, List<EventChoiceInfo>, Action<int>> OnChoiceRequired;

        /// <summary>이벤트 효과 적용 완료 (이벤트 ID, 결과 메시지)</summary>
        public static event Action<string, string> OnEventCompleted;

        // =====================================================================
        // 상태 접근
        // =====================================================================

        private GameState State => GameManager.Instance?.State;

        // 현재 처리 중인 이벤트
        private EventData currentEvent;
        private bool isProcessingEvent;

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

            Debug.Log("[EventSystem] 초기화 완료 (E8 데이터 기반)");
        }

        // =====================================================================
        // 메인 진입점
        // =====================================================================

        /// <summary>
        /// 랜덤 이벤트 처리 (TurnManager에서 호출)
        /// [E4] GameManager.EventMode 기반으로 변경
        /// </summary>
        public void ProcessRandomEvent(int currentTurn)
        {
            // 1턴은 이벤트 없음
            if (currentTurn <= 1)
            {
                Debug.Log("[EventSystem] 1턴 - 랜덤 이벤트 스킵");
                return;
            }

            // [E4] 이벤트 모드 확인
            var eventMode = GameManager.Instance?.EventMode ?? StartEventMode.Default;

            Debug.Log($"[EventSystem] 이벤트 모드: {eventMode}, 턴: {currentTurn}");

            if (eventMode == StartEventMode.Custom)
            {
                // 커스텀 모드: 예정 이벤트만 처리
                ProcessScheduledEvents(currentTurn);
                return;
            }

            // === 기본 모드: 랜덤 이벤트 로직 ===

            // 80% 확률로 이벤트 발생
            int roll = UnityEngine.Random.Range(0, 100);
            if (roll >= GameConfig.EventChance)
            {
                Debug.Log($"[EventSystem] 이벤트 미발생 (굴림: {roll} >= {GameConfig.EventChance})");
                return;
            }

            // 검증 턴인지 확인
            bool isValidationTurn = Array.IndexOf(GameConfig.ValidationTurns, currentTurn) >= 0;

            // 이벤트 카테고리 결정
            RandomEventCategory category = RollEventCategory(isValidationTurn);

            // 해당 카테고리에서 이벤트 선택
            EventData eventData = SelectEventFromCategory(category);

            if (eventData == null)
            {
                Debug.Log("[EventSystem] 적합한 이벤트 없음");
                return;
            }

            Debug.Log($"[EventSystem] 이벤트 발생: {eventData.eventId} ({category})");
            OnRandomEventTriggered?.Invoke(eventData.eventId, category);

            // 이벤트 실행
            ExecuteEvent(eventData);
        }

        /// <summary>
        /// 예정 이벤트 처리
        /// [E4] GameManager.ScheduledEvents 기반
        /// </summary>
        private void ProcessScheduledEvents(int currentTurn)
        {
            var scheduledEvents = GameManager.Instance?.ScheduledEvents;
            if (scheduledEvents == null || scheduledEvents.Count == 0)
            {
                Debug.Log($"[EventSystem] 커스텀 모드 - 예정 이벤트 없음");
                return;
            }

            foreach (var entry in scheduledEvents)
            {
                if (entry.turn == currentTurn && entry.eventSO != null)
                {
                    Debug.Log($"[EventSystem] 예정 이벤트 발생: {entry.eventSO.eventId} (턴 {currentTurn})");
                    var eventData = entry.eventSO.ToEventData();
                    OnRandomEventTriggered?.Invoke(eventData.eventId, eventData.GetCategory());
                    ExecuteEvent(eventData);
                    return; // 턴당 1개 이벤트만
                }
            }

            Debug.Log($"[EventSystem] 커스텀 모드 - {currentTurn}턴 예정 이벤트 없음");
        }

        // =====================================================================
        // 이벤트 카테고리 결정
        // =====================================================================

        /// <summary>
        /// 이벤트 카테고리 결정 (긍정 50% / 부정 20% / 선택 30%)
        /// </summary>
        private RandomEventCategory RollEventCategory(bool excludeNegative)
        {
            int roll = UnityEngine.Random.Range(0, 100);

            if (excludeNegative)
            {
                // 검증 턴: 부정 제외 (긍정 62.5%, 선택 37.5%)
                if (roll < 62)
                    return RandomEventCategory.Positive;
                else
                    return RandomEventCategory.Choice;
            }
            else
            {
                // 일반: 긍정 50%, 부정 20%, 선택 30%
                if (roll < GameConfig.PositiveEventRatio)
                    return RandomEventCategory.Positive;
                else if (roll < GameConfig.PositiveEventRatio + GameConfig.NegativeEventRatio)
                    return RandomEventCategory.Negative;
                else
                    return RandomEventCategory.Choice;
            }
        }

        /// <summary>
        /// 카테고리에서 이벤트 선택 (조건 체크 포함)
        /// </summary>
        private EventData SelectEventFromCategory(RandomEventCategory category)
        {
            // DataLoader에서 해당 카테고리 이벤트 가져오기
            List<EventData> eventPool = DataLoader.Instance?.GetEventsByCategory((int)category);

            if (eventPool == null || eventPool.Count == 0)
            {
                Debug.LogWarning($"[EventSystem] 카테고리 {category} 이벤트 없음");
                return null;
            }

            // 셔플
            ShuffleList(eventPool);

            // 조건 충족하는 첫 번째 이벤트 선택
            foreach (var evt in eventPool)
            {
                if (CheckEventCondition(evt))
                {
                    return evt;
                }
            }

            // 조건 충족하는 이벤트 없으면 null
            return null;
        }

        // =====================================================================
        // 이벤트 조건 체크 (EffectCondition 재사용)
        // =====================================================================

        /// <summary>
        /// 이벤트 발생 조건 체크
        /// </summary>
        private bool CheckEventCondition(EventData eventData)
        {
            if (State == null) return false;

            // 조건이 없으면 항상 발생 가능
            if (eventData.triggerConditions == null || eventData.triggerConditions.Length == 0)
            {
                return true;
            }

            // 모든 조건을 AND로 평가
            foreach (var condition in eventData.triggerConditions)
            {
                if (!EvaluateCondition(condition))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 선택지 요구사항 체크
        /// </summary>
        private bool CheckChoiceRequirements(EventChoice choice)
        {
            if (choice.HasNoRequirements)
                return true;

            foreach (var req in choice.requirements)
            {
                if (!EvaluateCondition(req))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 개별 조건 평가
        /// </summary>
        private bool EvaluateCondition(EffectCondition condition)
        {
            if (condition == null || condition.type == ConditionType.None)
                return true;

            switch (condition.type)
            {
                // === 골드 조건 ===
                case ConditionType.GoldAbove:
                    return CompareValue(State.gold, condition.comparison, condition.value);

                case ConditionType.GoldBelow:
                    return State.gold < condition.value;

                // === 유닛 조건 ===
                case ConditionType.HasUnit:
                    return State.units.Count >= 1;

                case ConditionType.HasMultipleUnits:
                    return State.units.Count >= condition.value;

                case ConditionType.HasPromotableUnit:
                    return State.units.Exists(u => u.CanPromote());

                // === 이벤트 전용 조건 (E8 추가) ===
                case ConditionType.HasCopperInDeck:
                    return HasCardOfType("copper");

                case ConditionType.HasPollutionInDeck:
                    return HasPollutionCard();

                case ConditionType.HasSpecificCardInDeck:
                    return true; // 기본 true

                default:
                    Debug.LogWarning($"[EventSystem] 미지원 조건: {condition.type}");
                    return true;
            }
        }

        /// <summary>
        /// 비교 연산 수행
        /// </summary>
        private bool CompareValue(int actual, ComparisonType comparison, int target)
        {
            return comparison switch
            {
                ComparisonType.Equal => actual == target,
                ComparisonType.NotEqual => actual != target,
                ComparisonType.GreaterThan => actual > target,
                ComparisonType.LessThan => actual < target,
                ComparisonType.GreaterOrEqual => actual >= target,
                ComparisonType.LessOrEqual => actual <= target,
                _ => true
            };
        }

        // =====================================================================
        // 이벤트 실행
        // =====================================================================

        /// <summary>
        /// 이벤트 실행
        /// </summary>
        private void ExecuteEvent(EventData eventData)
        {
            if (eventData == null) return;

            currentEvent = eventData;
            isProcessingEvent = true;

            Debug.Log($"[EventSystem] 이벤트 실행: {eventData.eventName}");

            // 선택 이벤트인지 확인
            if (eventData.IsChoiceEvent)
            {
                // 선택 이벤트: UI에 선택 요청
                ShowChoiceEvent(eventData);
            }
            else
            {
                // 즉시 효과 이벤트: 효과 실행
                ExecuteEventEffects(eventData.effects);
                CompleteEvent($"{eventData.eventName} 발생!");
            }
        }

        /// <summary>
        /// 선택 이벤트 UI 표시 (N개 선택지 지원)
        /// </summary>
        private void ShowChoiceEvent(EventData eventData)
        {
            var choiceInfos = new List<EventChoiceInfo>();

            for (int i = 0; i < eventData.choices.Length; i++)
            {
                var choice = eventData.choices[i];
                bool canSelect = CheckChoiceRequirements(choice);

                choiceInfos.Add(new EventChoiceInfo
                {
                    index = i,
                    choiceId = choice.choiceId,
                    choiceText = choice.choiceText,
                    canSelect = canSelect
                });
            }

            // UI에 선택 요청
            OnChoiceRequired?.Invoke(
                eventData.eventId,
                eventData.description,
                choiceInfos,
                OnChoiceSelected
            );
        }

        /// <summary>
        /// 선택 완료 콜백
        /// </summary>
        private void OnChoiceSelected(int choiceIndex)
        {
            if (currentEvent == null || !currentEvent.IsChoiceEvent)
            {
                Debug.LogWarning("[EventSystem] 선택할 이벤트 없음");
                return;
            }

            if (choiceIndex < 0 || choiceIndex >= currentEvent.choices.Length)
            {
                Debug.LogWarning($"[EventSystem] 잘못된 선택 인덱스: {choiceIndex}");
                return;
            }

            var selectedChoice = currentEvent.choices[choiceIndex];
            Debug.Log($"[EventSystem] 선택: {selectedChoice.choiceText}");

            // 선택지 효과 실행
            if (selectedChoice.effects != null && selectedChoice.effects.Length > 0)
            {
                ExecuteEventEffects(selectedChoice.effects);
            }

            CompleteEvent($"{currentEvent.eventName}: {selectedChoice.choiceText}");
        }

        /// <summary>
        /// 이벤트 효과 실행 (EffectSystem 사용)
        /// </summary>
        private void ExecuteEventEffects(ConditionalEffect[] effects)
        {
            if (effects == null || effects.Length == 0)
            {
                Debug.Log("[EventSystem] 실행할 효과 없음");
                return;
            }

            // EffectSystem의 이벤트용 오버로드 사용
            if (EffectSystem.Instance != null)
            {
                EffectSystem.Instance.ExecuteEventEffects(effects);
            }
            else
            {
                Debug.LogWarning("[EventSystem] EffectSystem 없음");
            }
        }

        /// <summary>
        /// 이벤트 완료 처리
        /// </summary>
        private void CompleteEvent(string resultMessage)
        {
            if (currentEvent != null)
            {
                OnEventCompleted?.Invoke(currentEvent.eventId, resultMessage);
                Debug.Log($"[EventSystem] 이벤트 완료: {resultMessage}");
            }

            currentEvent = null;
            isProcessingEvent = false;
        }

        // =====================================================================
        // 헬퍼 메서드
        // =====================================================================

        /// <summary>
        /// 오염 카드 존재 여부 확인
        /// </summary>
        private bool HasPollutionCard()
        {
            if (State == null) return false;

            foreach (var card in State.deck)
            {
                var data = DataLoader.Instance?.GetCard(card.cardDataId);
                if (data?.cardType == CardType.Pollution) return true;
            }

            foreach (var card in State.hand)
            {
                var data = DataLoader.Instance?.GetCard(card.cardDataId);
                if (data?.cardType == CardType.Pollution) return true;
            }

            foreach (var card in State.discardPile)
            {
                var data = DataLoader.Instance?.GetCard(card.cardDataId);
                if (data?.cardType == CardType.Pollution) return true;
            }

            return false;
        }

        /// <summary>
        /// 특정 카드 존재 여부 확인
        /// </summary>
        private bool HasCardOfType(string cardId)
        {
            if (State == null) return false;

            foreach (var card in State.deck)
            {
                if (card.cardDataId == cardId) return true;
            }

            foreach (var card in State.hand)
            {
                if (card.cardDataId == cardId) return true;
            }

            foreach (var card in State.discardPile)
            {
                if (card.cardDataId == cardId) return true;
            }

            return false;
        }

        /// <summary>
        /// 리스트 셔플
        /// </summary>
        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        // =====================================================================
        // 공개 API
        // =====================================================================

        /// <summary>
        /// 이벤트 정보 조회 (이름, 설명)
        /// </summary>
        public (string name, string description) GetEventInfo(string eventId)
        {
            var eventData = DataLoader.Instance?.GetEvent(eventId);
            if (eventData != null)
            {
                return (eventData.eventName, eventData.description);
            }
            return ("알 수 없는 이벤트", "");
        }

        /// <summary>
        /// 현재 이벤트 처리 중인지
        /// </summary>
        public bool IsProcessing => isProcessingEvent;

        /// <summary>
        /// 수동으로 특정 이벤트 실행 (디버그/테스트용)
        /// </summary>
        public void TriggerEventById(string eventId)
        {
            var eventData = DataLoader.Instance?.GetEvent(eventId);
            if (eventData != null)
            {
                OnRandomEventTriggered?.Invoke(eventId, eventData.GetCategory());
                ExecuteEvent(eventData);
            }
            else
            {
                Debug.LogWarning($"[EventSystem] 이벤트 없음: {eventId}");
            }
        }
    }
}