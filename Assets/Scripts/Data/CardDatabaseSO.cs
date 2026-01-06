// =============================================================================
// CardDatabaseSO.cs
// 카드 데이터베이스 ScriptableObject 정의
// =============================================================================
// [E2] 전체 카드 목록을 관리하는 SO
// - 개별 CardSO 참조를 배열로 관리
// - 런타임에 Dictionary로 캐시하여 빠른 조회
// - DataLoader에서 사용
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBuildingEconomy.Data
{
    /// <summary>
    /// 카드 데이터베이스 ScriptableObject
    /// 전체 카드 목록을 관리
    /// </summary>
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "DeckBuilding/Card Database")]
    public class CardDatabaseSO : ScriptableObject
    {
        // =====================================================================
        // 데이터
        // =====================================================================

        [Header("버전 정보")]
        [Tooltip("데이터 버전")]
        public string version = "1.0.0";

        [Header("카드 목록")]
        [Tooltip("전체 카드 SO 참조 목록")]
        public CardSO[] cards;

        // =====================================================================
        // 런타임 캐시
        // =====================================================================

        private Dictionary<string, CardSO> _cardDict;
        private Dictionary<CardType, List<CardSO>> _cardsByType;
        private Dictionary<Job, List<CardSO>> _cardsByJob;
        private bool _cacheBuilt = false;

        // =====================================================================
        // 초기화
        // =====================================================================

        /// <summary>
        /// 캐시 빌드 (최초 조회 시 자동 호출)
        /// </summary>
        public void BuildCache()
        {
            _cardDict = new Dictionary<string, CardSO>();
            _cardsByType = new Dictionary<CardType, List<CardSO>>();
            _cardsByJob = new Dictionary<Job, List<CardSO>>();

            // 타입별 리스트 초기화
            foreach (CardType type in Enum.GetValues(typeof(CardType)))
            {
                _cardsByType[type] = new List<CardSO>();
            }

            // 직업별 리스트 초기화
            foreach (Job job in Enum.GetValues(typeof(Job)))
            {
                _cardsByJob[job] = new List<CardSO>();
            }

            // 카드 등록
            if (cards != null)
            {
                foreach (var card in cards)
                {
                    if (card == null) continue;

                    // ID로 등록
                    if (!string.IsNullOrEmpty(card.id))
                    {
                        if (_cardDict.ContainsKey(card.id))
                        {
                            Debug.LogWarning($"[CardDatabaseSO] 중복 카드 ID: {card.id}");
                        }
                        _cardDict[card.id] = card;
                    }

                    // 타입별 등록
                    _cardsByType[card.cardType].Add(card);

                    // 직업별 등록
                    if (card.jobPools != null)
                    {
                        foreach (var job in card.jobPools)
                        {
                            _cardsByJob[job].Add(card);
                        }
                    }
                }
            }

            _cacheBuilt = true;
            Debug.Log($"[CardDatabaseSO] 캐시 빌드 완료 - {_cardDict.Count}개 카드");
        }

        /// <summary>
        /// 캐시 무효화 (에디터에서 데이터 변경 시)
        /// </summary>
        public void InvalidateCache()
        {
            _cacheBuilt = false;
            _cardDict = null;
            _cardsByType = null;
            _cardsByJob = null;
        }

        private void EnsureCache()
        {
            if (!_cacheBuilt) BuildCache();
        }

        // =====================================================================
        // 조회 메서드
        // =====================================================================

        /// <summary>
        /// ID로 카드 조회
        /// </summary>
        public CardSO GetCard(string id)
        {
            EnsureCache();

            if (string.IsNullOrEmpty(id)) return null;

            _cardDict.TryGetValue(id, out var card);
            return card;
        }

        /// <summary>
        /// ID로 카드 존재 여부 확인
        /// </summary>
        public bool HasCard(string id)
        {
            EnsureCache();
            return !string.IsNullOrEmpty(id) && _cardDict.ContainsKey(id);
        }

        /// <summary>
        /// 타입별 카드 목록 조회
        /// </summary>
        public CardSO[] GetCardsByType(CardType type)
        {
            EnsureCache();
            return _cardsByType.TryGetValue(type, out var list) ? list.ToArray() : new CardSO[0];
        }

        /// <summary>
        /// 직업별 카드 목록 조회
        /// </summary>
        public CardSO[] GetCardsByJob(Job job)
        {
            EnsureCache();
            return _cardsByJob.TryGetValue(job, out var list) ? list.ToArray() : new CardSO[0];
        }

        /// <summary>
        /// 직업 + 희귀도로 카드 목록 조회
        /// </summary>
        public CardSO[] GetCardsByJobAndRarity(Job job, CardRarity rarity)
        {
            EnsureCache();

            if (!_cardsByJob.TryGetValue(job, out var jobCards))
                return new CardSO[0];

            var result = new List<CardSO>();
            foreach (var card in jobCards)
            {
                if (card.rarity == rarity)
                    result.Add(card);
            }
            return result.ToArray();
        }

        /// <summary>
        /// 모든 카드 ID 목록
        /// </summary>
        public string[] GetAllCardIds()
        {
            EnsureCache();
            var ids = new string[_cardDict.Count];
            _cardDict.Keys.CopyTo(ids, 0);
            return ids;
        }

        /// <summary>
        /// 카드 총 개수
        /// </summary>
        public int Count
        {
            get
            {
                EnsureCache();
                return _cardDict.Count;
            }
        }

        // =====================================================================
        // 변환 메서드
        // =====================================================================

        /// <summary>
        /// SO 목록 → CardData Dictionary 변환 (기존 시스템 호환용)
        /// </summary>
        public Dictionary<string, CardData> ToCardDataDictionary()
        {
            var result = new Dictionary<string, CardData>();

            if (cards != null)
            {
                foreach (var card in cards)
                {
                    if (card == null || string.IsNullOrEmpty(card.id)) continue;
                    result[card.id] = card.ToCardData();
                }
            }

            return result;
        }

        // =====================================================================
        // 유효성 검증
        // =====================================================================

        /// <summary>
        /// 전체 데이터베이스 유효성 검증
        /// </summary>
        public bool ValidateAll(out List<string> errors)
        {
            errors = new List<string>();

            if (cards == null || cards.Length == 0)
            {
                errors.Add("카드가 없습니다.");
                return false;
            }

            var ids = new HashSet<string>();

            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];

                if (card == null)
                {
                    errors.Add($"[{i}] null 참조");
                    continue;
                }

                // 개별 유효성 검증
                if (!card.Validate(out var cardError))
                {
                    errors.Add($"[{i}] {card.id}: {cardError}");
                }

                // ID 중복 검사
                if (!string.IsNullOrEmpty(card.id))
                {
                    if (ids.Contains(card.id))
                    {
                        errors.Add($"[{i}] 중복 ID: {card.id}");
                    }
                    else
                    {
                        ids.Add(card.id);
                    }
                }
            }

            return errors.Count == 0;
        }

        // =====================================================================
        // 에디터 헬퍼
        // =====================================================================

        /// <summary>
        /// 카드 추가 (에디터용)
        /// </summary>
        public void AddCard(CardSO card)
        {
            if (card == null) return;

            var list = new List<CardSO>(cards ?? new CardSO[0]);
            if (!list.Contains(card))
            {
                list.Add(card);
                cards = list.ToArray();
                InvalidateCache();
            }
        }

        /// <summary>
        /// 카드 제거 (에디터용)
        /// </summary>
        public void RemoveCard(CardSO card)
        {
            if (card == null || cards == null) return;

            var list = new List<CardSO>(cards);
            if (list.Remove(card))
            {
                cards = list.ToArray();
                InvalidateCache();
            }
        }

        /// <summary>
        /// ID로 카드 제거 (에디터용)
        /// </summary>
        public void RemoveCardById(string id)
        {
            if (string.IsNullOrEmpty(id) || cards == null) return;

            var list = new List<CardSO>(cards);
            list.RemoveAll(c => c != null && c.id == id);
            cards = list.ToArray();
            InvalidateCache();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 변경 시 캐시 무효화
        /// </summary>
        private void OnValidate()
        {
            InvalidateCache();
        }
#endif
    }
}
