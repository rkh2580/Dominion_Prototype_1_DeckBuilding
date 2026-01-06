// =============================================================================
// JsonToSOConverter.cs
// JSON → ScriptableObject 변환 에디터 도구
// =============================================================================
// [E1] GameConfig.json, TreasureGrades.json → SO 에셋 변환
// [E2] Cards.json, Jobs.json → 개별 SO 에셋 + Database 변환
// 위치: Assets/Editor/JsonToSOConverter.cs
// 메뉴: Tools/DeckBuilding/Convert JSON to SO
// =============================================================================

#if UNITY_EDITOR

using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DeckBuildingEconomy.Data;

namespace DeckBuildingEconomy.Editor
{
    /// <summary>
    /// JSON 파일을 ScriptableObject 에셋으로 변환하는 에디터 도구
    /// </summary>
    public static class JsonToSOConverter
    {
        // =====================================================================
        // 상수
        // =====================================================================

        private const string MENU_ROOT = "Tools/DeckBuilding/";
        private const string OUTPUT_PATH = "Assets/Resources/Data/";
        private const string STREAMING_DATA_PATH = "Assets/StreamingAssets/Data/";
        private const string RESOURCES_DATA_PATH = "Assets/Resources/Data/";

        // [E2] 카드/직업 폴더 경로
        private const string CARDS_PATH = "Assets/Resources/Data/Cards/";
        private const string JOBS_PATH = "Assets/Resources/Data/Jobs/";

        // =====================================================================
        // 메뉴 명령
        // =====================================================================

        /// <summary>
        /// 모든 JSON → SO 변환
        /// </summary>
        [MenuItem(MENU_ROOT + "Convert All JSON to SO", false, 100)]
        public static void ConvertAllJsonToSO()
        {
            Debug.Log("[JsonToSOConverter] === 전체 JSON → SO 변환 시작 ===");

            bool configSuccess = ConvertGameConfigToSO();
            bool treasureSuccess = ConvertTreasureGradesToSO();
            bool cardsSuccess = ConvertCardsToSO();
            bool jobsSuccess = ConvertJobsToSO();
            bool eventsSuccess = ConvertEventsToSO();  // [E3] 추가

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (configSuccess && treasureSuccess && cardsSuccess && jobsSuccess && eventsSuccess)
            {
                Debug.Log("[JsonToSOConverter] === 전체 변환 완료 ===");
                EditorUtility.DisplayDialog(
                    "변환 완료",
                    "GameConfig.json → GameConfigSO.asset\n" +
                    "TreasureGrades.json → TreasureGradesSO.asset\n" +
                    "Cards.json → CardSO 에셋들 + CardDatabaseSO\n" +
                    "Jobs.json → JobSO 에셋들 + JobDatabaseSO\n" +
                    "Events.json → EventSO 에셋들 + EventDatabaseSO\n\n" +
                    "변환이 완료되었습니다.",
                    "확인");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "변환 실패",
                    "일부 변환에 실패했습니다. Console 로그를 확인하세요.",
                    "확인");
            }
        }

        /// <summary>
        /// GameConfig.json → SO 변환
        /// </summary>
        [MenuItem(MENU_ROOT + "Convert GameConfig JSON to SO", false, 101)]
        public static bool ConvertGameConfigToSO()
        {
            Debug.Log("[JsonToSOConverter] GameConfig.json → SO 변환 시작");

            // 1. JSON 로드 (StreamingAssets → Resources 순서)
            string jsonText = LoadJsonFile("GameConfig");
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("[JsonToSOConverter] GameConfig.json을 찾을 수 없습니다.");
                return false;
            }

            // 2. JSON 파싱
            GameConfigData data;
            try
            {
                data = JsonUtility.FromJson<GameConfigData>(jsonText);
                Debug.Log($"[JsonToSOConverter] GameConfig 파싱 완료 (v{data.version})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JsonToSOConverter] GameConfig.json 파싱 실패: {e.Message}");
                return false;
            }

            // 3. SO 에셋 생성 또는 로드
            EnsureDirectoryExists(OUTPUT_PATH);
            string assetPath = OUTPUT_PATH + "GameConfigSO.asset";

            GameConfigSO so = AssetDatabase.LoadAssetAtPath<GameConfigSO>(assetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<GameConfigSO>();
                AssetDatabase.CreateAsset(so, assetPath);
                Debug.Log($"[JsonToSOConverter] 새 GameConfigSO 에셋 생성: {assetPath}");
            }
            else
            {
                Debug.Log($"[JsonToSOConverter] 기존 GameConfigSO 에셋 업데이트: {assetPath}");
            }

            // 4. 데이터 복사
            so.FromGameConfigData(data);

            // 5. 저장
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();

            Debug.Log("[JsonToSOConverter] GameConfig → SO 변환 완료");
            return true;
        }

        /// <summary>
        /// TreasureGrades.json → SO 변환
        /// </summary>
        [MenuItem(MENU_ROOT + "Convert TreasureGrades JSON to SO", false, 102)]
        public static bool ConvertTreasureGradesToSO()
        {
            Debug.Log("[JsonToSOConverter] TreasureGrades.json → SO 변환 시작");

            // 1. JSON 로드
            string jsonText = LoadJsonFile("TreasureGrades");
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("[JsonToSOConverter] TreasureGrades.json을 찾을 수 없습니다.");
                return false;
            }

            // 2. JSON 파싱
            TreasureGradesData data;
            try
            {
                data = JsonUtility.FromJson<TreasureGradesData>(jsonText);
                Debug.Log($"[JsonToSOConverter] TreasureGrades 파싱 완료 (v{data.version}, {data.grades?.Length ?? 0}개 등급)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JsonToSOConverter] TreasureGrades.json 파싱 실패: {e.Message}");
                return false;
            }

            // 3. SO 에셋 생성 또는 로드
            EnsureDirectoryExists(OUTPUT_PATH);
            string assetPath = OUTPUT_PATH + "TreasureGradesSO.asset";

            TreasureGradesSO so = AssetDatabase.LoadAssetAtPath<TreasureGradesSO>(assetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<TreasureGradesSO>();
                AssetDatabase.CreateAsset(so, assetPath);
                Debug.Log($"[JsonToSOConverter] 새 TreasureGradesSO 에셋 생성: {assetPath}");
            }
            else
            {
                Debug.Log($"[JsonToSOConverter] 기존 TreasureGradesSO 에셋 업데이트: {assetPath}");
            }

            // 4. 데이터 복사
            so.FromTreasureGradesData(data);

            // 5. 저장
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();

            Debug.Log("[JsonToSOConverter] TreasureGrades → SO 변환 완료");
            return true;
        }

        // =====================================================================
        // [E2] Cards.json → SO 변환
        // =====================================================================

        /// <summary>
        /// Cards.json → 개별 CardSO + CardDatabaseSO 변환
        /// </summary>
        [MenuItem(MENU_ROOT + "Convert Cards JSON to SO", false, 110)]
        public static bool ConvertCardsToSO()
        {
            Debug.Log("[JsonToSOConverter] Cards.json → SO 변환 시작");

            // 1. JSON 로드
            string jsonText = LoadJsonFile("Cards");
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("[JsonToSOConverter] Cards.json을 찾을 수 없습니다.");
                return false;
            }

            // 2. JSON 파싱
            CardDataList dataList;
            try
            {
                dataList = JsonUtility.FromJson<CardDataList>(jsonText);
                Debug.Log($"[JsonToSOConverter] Cards 파싱 완료 ({dataList.cards?.Count ?? 0}개 카드)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JsonToSOConverter] Cards.json 파싱 실패: {e.Message}");
                return false;
            }

            if (dataList.cards == null || dataList.cards.Count == 0)
            {
                Debug.LogError("[JsonToSOConverter] Cards.json에 카드 데이터가 없습니다.");
                return false;
            }

            // 3. 타입별 폴더 생성
            EnsureDirectoryExists(CARDS_PATH);
            EnsureDirectoryExists(CARDS_PATH + "Treasure/");
            EnsureDirectoryExists(CARDS_PATH + "Action/");
            EnsureDirectoryExists(CARDS_PATH + "Pollution/");

            // 4. 개별 CardSO 생성
            var createdCards = new List<CardSO>();
            int created = 0, updated = 0;

            foreach (var cardData in dataList.cards)
            {
                if (string.IsNullOrEmpty(cardData.id))
                {
                    Debug.LogWarning("[JsonToSOConverter] ID가 없는 카드 스킵");
                    continue;
                }

                // 타입별 폴더 결정
                string folderPath = GetCardFolderPath(cardData.cardType);
                string assetPath = folderPath + cardData.id + ".asset";

                // 기존 에셋 확인 또는 새로 생성
                CardSO cardSO = AssetDatabase.LoadAssetAtPath<CardSO>(assetPath);
                if (cardSO == null)
                {
                    cardSO = ScriptableObject.CreateInstance<CardSO>();
                    AssetDatabase.CreateAsset(cardSO, assetPath);
                    created++;
                }
                else
                {
                    updated++;
                }

                // 데이터 복사
                cardSO.FromCardData(cardData);
                EditorUtility.SetDirty(cardSO);

                createdCards.Add(cardSO);
            }

            Debug.Log($"[JsonToSOConverter] CardSO 생성: {created}개 새로 생성, {updated}개 업데이트");

            // 5. CardDatabaseSO 생성/업데이트
            string dbPath = OUTPUT_PATH + "CardDatabaseSO.asset";
            CardDatabaseSO database = AssetDatabase.LoadAssetAtPath<CardDatabaseSO>(dbPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<CardDatabaseSO>();
                AssetDatabase.CreateAsset(database, dbPath);
                Debug.Log($"[JsonToSOConverter] 새 CardDatabaseSO 에셋 생성: {dbPath}");
            }

            // 카드 목록 등록
            database.cards = createdCards.ToArray();
            database.version = "2.0"; // Cards.json 버전과 일치
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[JsonToSOConverter] Cards → SO 변환 완료 (총 {createdCards.Count}개 카드)");
            return true;
        }

        /// <summary>
        /// 카드 타입에 따른 폴더 경로 반환
        /// </summary>
        private static string GetCardFolderPath(CardType cardType)
        {
            switch (cardType)
            {
                case CardType.Treasure:
                    return CARDS_PATH + "Treasure/";
                case CardType.Action:
                    return CARDS_PATH + "Action/";
                case CardType.Pollution:
                    return CARDS_PATH + "Pollution/";
                default:
                    return CARDS_PATH;
            }
        }

        // =====================================================================
        // [E2] Jobs.json → SO 변환
        // =====================================================================

        /// <summary>
        /// Jobs.json → 개별 JobSO + JobDatabaseSO 변환
        /// </summary>
        [MenuItem(MENU_ROOT + "Convert Jobs JSON to SO", false, 111)]
        public static bool ConvertJobsToSO()
        {
            Debug.Log("[JsonToSOConverter] Jobs.json → SO 변환 시작");

            // 1. JSON 로드
            string jsonText = LoadJsonFile("Jobs");
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("[JsonToSOConverter] Jobs.json을 찾을 수 없습니다.");
                return false;
            }

            // 2. JSON 파싱
            JobsData jobsData;
            try
            {
                jobsData = JsonUtility.FromJson<JobsData>(jsonText);
                Debug.Log($"[JsonToSOConverter] Jobs 파싱 완료 (v{jobsData.version}, {jobsData.jobs?.Length ?? 0}개 직업)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JsonToSOConverter] Jobs.json 파싱 실패: {e.Message}");
                return false;
            }

            if (jobsData.jobs == null || jobsData.jobs.Length == 0)
            {
                Debug.LogError("[JsonToSOConverter] Jobs.json에 직업 데이터가 없습니다.");
                return false;
            }

            // 3. 폴더 생성
            EnsureDirectoryExists(JOBS_PATH);

            // 4. 개별 JobSO 생성
            var createdJobs = new List<JobSO>();
            int created = 0, updated = 0;

            foreach (var jobInfo in jobsData.jobs)
            {
                if (string.IsNullOrEmpty(jobInfo.id))
                {
                    Debug.LogWarning("[JsonToSOConverter] ID가 없는 직업 스킵");
                    continue;
                }

                string assetPath = JOBS_PATH + jobInfo.id + ".asset";

                // 기존 에셋 확인 또는 새로 생성
                JobSO jobSO = AssetDatabase.LoadAssetAtPath<JobSO>(assetPath);
                if (jobSO == null)
                {
                    jobSO = ScriptableObject.CreateInstance<JobSO>();
                    AssetDatabase.CreateAsset(jobSO, assetPath);
                    created++;
                }
                else
                {
                    updated++;
                }

                // 데이터 복사
                jobSO.FromJobInfo(jobInfo);
                EditorUtility.SetDirty(jobSO);

                createdJobs.Add(jobSO);
            }

            Debug.Log($"[JsonToSOConverter] JobSO 생성: {created}개 새로 생성, {updated}개 업데이트");

            // 5. JobDatabaseSO 생성/업데이트
            string dbPath = OUTPUT_PATH + "JobDatabaseSO.asset";
            JobDatabaseSO database = AssetDatabase.LoadAssetAtPath<JobDatabaseSO>(dbPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<JobDatabaseSO>();
                AssetDatabase.CreateAsset(database, dbPath);
                Debug.Log($"[JsonToSOConverter] 새 JobDatabaseSO 에셋 생성: {dbPath}");
            }

            // 직업 목록 등록
            database.jobs = createdJobs.ToArray();
            database.version = jobsData.version ?? "1.0.0";
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[JsonToSOConverter] Jobs → SO 변환 완료 (총 {createdJobs.Count}개 직업)");
            return true;
        }

        // =====================================================================
        // 검증 도구
        // =====================================================================

        /// <summary>
        /// SO 에셋 검증 (JSON과 비교)
        /// </summary>
        [MenuItem(MENU_ROOT + "Validate SO Assets", false, 200)]
        public static void ValidateSOAssets()
        {
            Debug.Log("[JsonToSOConverter] === SO 에셋 검증 시작 ===");

            bool configValid = ValidateGameConfigSO();
            bool treasureValid = ValidateTreasureGradesSO();
            bool cardsValid = ValidateCardDatabaseSO();
            bool jobsValid = ValidateJobDatabaseSO();

            if (configValid && treasureValid && cardsValid && jobsValid)
            {
                Debug.Log("[JsonToSOConverter] === 모든 SO 에셋 검증 통과 ===");
                EditorUtility.DisplayDialog("검증 완료", "모든 SO 에셋이 유효합니다.", "확인");
            }
            else
            {
                EditorUtility.DisplayDialog("검증 실패", "일부 SO 에셋에 문제가 있습니다.\nConsole 로그를 확인하세요.", "확인");
            }
        }

        private static bool ValidateGameConfigSO()
        {
            string assetPath = OUTPUT_PATH + "GameConfigSO.asset";
            GameConfigSO so = AssetDatabase.LoadAssetAtPath<GameConfigSO>(assetPath);

            if (so == null)
            {
                Debug.LogWarning("[JsonToSOConverter] GameConfigSO.asset이 없습니다.");
                return false;
            }

            string jsonText = LoadJsonFile("GameConfig");
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogWarning("[JsonToSOConverter] GameConfig.json을 찾을 수 없어 검증 불가");
                return true; // JSON 없으면 SO만 사용 가능
            }

            GameConfigData jsonData = JsonUtility.FromJson<GameConfigData>(jsonText);
            GameConfigData soData = so.ToGameConfigData();

            // 핵심 값 비교
            bool valid = true;

            if (jsonData.game.startingGold != soData.game.startingGold)
            {
                Debug.LogError($"[검증 실패] startingGold: JSON={jsonData.game.startingGold}, SO={soData.game.startingGold}");
                valid = false;
            }

            if (jsonData.game.handSize != soData.game.handSize)
            {
                Debug.LogError($"[검증 실패] handSize: JSON={jsonData.game.handSize}, SO={soData.game.handSize}");
                valid = false;
            }

            if (jsonData.validation.turns.Length != soData.validation.turns.Length)
            {
                Debug.LogError($"[검증 실패] validation.turns 길이: JSON={jsonData.validation.turns.Length}, SO={soData.validation.turns.Length}");
                valid = false;
            }

            if (valid)
            {
                Debug.Log("[JsonToSOConverter] GameConfigSO 검증 통과");
            }

            return valid;
        }

        private static bool ValidateTreasureGradesSO()
        {
            string assetPath = OUTPUT_PATH + "TreasureGradesSO.asset";
            TreasureGradesSO so = AssetDatabase.LoadAssetAtPath<TreasureGradesSO>(assetPath);

            if (so == null)
            {
                Debug.LogWarning("[JsonToSOConverter] TreasureGradesSO.asset이 없습니다.");
                return false;
            }

            string jsonText = LoadJsonFile("TreasureGrades");
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogWarning("[JsonToSOConverter] TreasureGrades.json을 찾을 수 없어 검증 불가");
                return true;
            }

            TreasureGradesData jsonData = JsonUtility.FromJson<TreasureGradesData>(jsonText);
            TreasureGradesData soData = so.ToTreasureGradesData();

            bool valid = true;

            if (jsonData.grades.Length != soData.grades.Length)
            {
                Debug.LogError($"[검증 실패] grades 길이: JSON={jsonData.grades.Length}, SO={soData.grades.Length}");
                valid = false;
            }
            else
            {
                for (int i = 0; i < jsonData.grades.Length; i++)
                {
                    if (jsonData.grades[i].goldValue != soData.grades[i].goldValue)
                    {
                        Debug.LogError($"[검증 실패] grades[{i}].goldValue: JSON={jsonData.grades[i].goldValue}, SO={soData.grades[i].goldValue}");
                        valid = false;
                    }
                }
            }

            if (valid)
            {
                Debug.Log("[JsonToSOConverter] TreasureGradesSO 검증 통과");
            }

            return valid;
        }

        /// <summary>
        /// [E2] CardDatabaseSO 검증
        /// </summary>
        private static bool ValidateCardDatabaseSO()
        {
            string assetPath = OUTPUT_PATH + "CardDatabaseSO.asset";
            CardDatabaseSO so = AssetDatabase.LoadAssetAtPath<CardDatabaseSO>(assetPath);

            if (so == null)
            {
                Debug.LogWarning("[JsonToSOConverter] CardDatabaseSO.asset이 없습니다.");
                return false;
            }

            // 내부 유효성 검증
            if (!so.ValidateAll(out var errors))
            {
                foreach (var error in errors)
                {
                    Debug.LogError($"[검증 실패] CardDatabase: {error}");
                }
                return false;
            }

            // JSON과 개수 비교
            string jsonText = LoadJsonFile("Cards");
            if (!string.IsNullOrEmpty(jsonText))
            {
                var dataList = JsonUtility.FromJson<CardDataList>(jsonText);
                if (dataList.cards.Count != so.cards.Length)
                {
                    Debug.LogWarning($"[검증 경고] 카드 개수 불일치: JSON={dataList.cards.Count}, SO={so.cards.Length}");
                }
            }

            Debug.Log($"[JsonToSOConverter] CardDatabaseSO 검증 통과 ({so.cards?.Length ?? 0}개 카드)");
            return true;
        }

        /// <summary>
        /// [E2] JobDatabaseSO 검증
        /// </summary>
        private static bool ValidateJobDatabaseSO()
        {
            string assetPath = OUTPUT_PATH + "JobDatabaseSO.asset";
            JobDatabaseSO so = AssetDatabase.LoadAssetAtPath<JobDatabaseSO>(assetPath);

            if (so == null)
            {
                Debug.LogWarning("[JsonToSOConverter] JobDatabaseSO.asset이 없습니다.");
                return false;
            }

            // 내부 유효성 검증
            if (!so.ValidateAll(out var errors))
            {
                foreach (var error in errors)
                {
                    Debug.LogError($"[검증 실패] JobDatabase: {error}");
                }
                return false;
            }

            // JSON과 개수 비교
            string jsonText = LoadJsonFile("Jobs");
            if (!string.IsNullOrEmpty(jsonText))
            {
                var jobsData = JsonUtility.FromJson<JobsData>(jsonText);
                if (jobsData.jobs.Length != so.jobs.Length)
                {
                    Debug.LogWarning($"[검증 경고] 직업 개수 불일치: JSON={jobsData.jobs.Length}, SO={so.jobs.Length}");
                }
            }

            Debug.Log($"[JsonToSOConverter] JobDatabaseSO 검증 통과 ({so.jobs?.Length ?? 0}개 직업)");
            return true;
        }

        // =====================================================================
        // 헬퍼 메서드
        // =====================================================================

        /// <summary>
        /// JSON 파일 로드 (StreamingAssets → Resources 순서)
        /// </summary>
        private static string LoadJsonFile(string dataName)
        {
            // 1. StreamingAssets 시도
            string streamingPath = STREAMING_DATA_PATH + dataName + ".json";
            if (File.Exists(streamingPath))
            {
                Debug.Log($"[JsonToSOConverter] StreamingAssets에서 로드: {streamingPath}");
                return File.ReadAllText(streamingPath, System.Text.Encoding.UTF8);
            }

            // 2. Resources 시도
            string resourcesPath = RESOURCES_DATA_PATH + dataName + ".json";
            if (File.Exists(resourcesPath))
            {
                Debug.Log($"[JsonToSOConverter] Resources에서 로드: {resourcesPath}");
                return File.ReadAllText(resourcesPath, System.Text.Encoding.UTF8);
            }

            // 3. TextAsset으로 시도 (이미 빌드된 경우)
            TextAsset textAsset = Resources.Load<TextAsset>("Data/" + dataName);
            if (textAsset != null)
            {
                Debug.Log($"[JsonToSOConverter] Resources TextAsset에서 로드: Data/{dataName}");
                return textAsset.text;
            }

            Debug.LogWarning($"[JsonToSOConverter] {dataName}.json을 찾을 수 없습니다.");
            return null;
        }

        /// <summary>
        /// 디렉토리 존재 확인 및 생성
        /// </summary>
        private static void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path.TrimEnd('/')))
            {
                // 상위 폴더부터 생성
                string[] parts = path.Split('/');
                string currentPath = parts[0];

                for (int i = 1; i < parts.Length; i++)
                {
                    if (string.IsNullOrEmpty(parts[i])) continue;

                    string newPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                        Debug.Log($"[JsonToSOConverter] 폴더 생성: {newPath}");
                    }
                    currentPath = newPath;
                }
            }
        }

        // =====================================================================
        // [E3] Events.json → SO 변환
        // =====================================================================

        private const string EVENTS_PATH = "Assets/Resources/Data/Events/";

        /// <summary>
        /// Events.json → 개별 EventSO + EventDatabaseSO 변환
        /// </summary>
        [MenuItem(MENU_ROOT + "Convert Events JSON to SO", false, 120)]
        public static bool ConvertEventsToSO()
        {
            Debug.Log("[JsonToSOConverter] Events.json → SO 변환 시작");

            // 1. JSON 로드
            string jsonText = LoadJsonFile("Events");
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("[JsonToSOConverter] Events.json을 찾을 수 없습니다.");
                return false;
            }

            // 2. JSON 파싱
            EventDataList dataList;
            try
            {
                dataList = JsonUtility.FromJson<EventDataList>(jsonText);
                Debug.Log($"[JsonToSOConverter] Events 파싱 완료 ({dataList.events?.Count ?? 0}개 이벤트)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JsonToSOConverter] Events.json 파싱 실패: {e.Message}");
                return false;
            }

            if (dataList.events == null || dataList.events.Count == 0)
            {
                Debug.LogError("[JsonToSOConverter] Events.json에 이벤트 데이터가 없습니다.");
                return false;
            }

            // 3. 카테고리별 폴더 생성
            EnsureDirectoryExists(EVENTS_PATH);
            EnsureDirectoryExists(EVENTS_PATH + "Positive/");
            EnsureDirectoryExists(EVENTS_PATH + "Negative/");
            EnsureDirectoryExists(EVENTS_PATH + "Choice/");

            // 4. 개별 EventSO 생성
            var createdEvents = new List<EventSO>();
            int created = 0, updated = 0;

            foreach (var eventData in dataList.events)
            {
                if (string.IsNullOrEmpty(eventData.eventId))
                {
                    Debug.LogWarning("[JsonToSOConverter] ID가 없는 이벤트 스킵");
                    continue;
                }

                // 카테고리별 폴더 결정
                string folderPath = GetEventFolderPath(eventData.category);
                string assetPath = folderPath + "Event_" + eventData.eventId + ".asset";

                // 기존 에셋 확인 또는 새로 생성
                EventSO eventSO = AssetDatabase.LoadAssetAtPath<EventSO>(assetPath);
                if (eventSO == null)
                {
                    eventSO = ScriptableObject.CreateInstance<EventSO>();
                    AssetDatabase.CreateAsset(eventSO, assetPath);
                    created++;
                }
                else
                {
                    updated++;
                }

                // 데이터 복사
                eventSO.FromEventData(eventData);
                EditorUtility.SetDirty(eventSO);

                createdEvents.Add(eventSO);
            }

            Debug.Log($"[JsonToSOConverter] EventSO 생성: {created}개 새로 생성, {updated}개 업데이트");

            // 5. EventDatabaseSO 생성/업데이트
            string dbPath = OUTPUT_PATH + "EventDatabaseSO.asset";
            EventDatabaseSO database = AssetDatabase.LoadAssetAtPath<EventDatabaseSO>(dbPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<EventDatabaseSO>();
                AssetDatabase.CreateAsset(database, dbPath);
                Debug.Log($"[JsonToSOConverter] 새 EventDatabaseSO 에셋 생성: {dbPath}");
            }

            database.events = createdEvents.ToArray();
            EditorUtility.SetDirty(database);

            // 6. 저장
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[JsonToSOConverter] Events → SO 변환 완료 (총 {createdEvents.Count}개 이벤트)");
            return true;
        }

        /// <summary>
        /// 이벤트 카테고리별 폴더 경로
        /// </summary>
        private static string GetEventFolderPath(int category)
        {
            switch (category)
            {
                case 1: return EVENTS_PATH + "Positive/";
                case 2: return EVENTS_PATH + "Negative/";
                case 3: return EVENTS_PATH + "Choice/";
                default: return EVENTS_PATH;
            }
        }
    }
}

#endif