using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace DreamForgeTD
{
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameObjectManager))]
    public sealed class LevelManager : MonoBehaviour
    {
        private const int SupportedSchemaVersion = 1;
        private const int MaximumObjectsPerLevel = 256;

        [SerializeField] private LevelPrefabCatalog prefabCatalog;
        [SerializeField] private string levelsRelativeDirectory = "DreamForgeTD/Levels";
        [SerializeField] private string manifestFileName = "levels.json";
        [SerializeField, Min(0)] private int startingLevelIndex;
        [SerializeField] private bool loadOnStart = true;

        private readonly List<Target> remainingTargets = new List<Target>();
        private readonly List<BowlingCan> remainingBowlingCans = new List<BowlingCan>();
        private GameObjectManager gameObjectManager;
        private string[] levelIds = new string[0];
        private LevelDocument currentLevel;
        private Coroutine activeLoad;
        private int currentLevelIndex = -1;
        private bool currentLevelCompleted;

        public static LevelManager Instance { get; private set; }

        public string CurrentLevelId => currentLevel != null ? currentLevel.id : string.Empty;
        public string CurrentLevelName => currentLevel != null ? currentLevel.displayName : string.Empty;
        public int CurrentLevelIndex => currentLevelIndex;
        public int LevelCount => levelIds.Length;
        public bool IsLoading => activeLoad != null;

        public event Action<int, string> LevelLoaded;
        public event Action<string> LevelCompleted;
        public event Action AllLevelsCompleted;
        public event Action<string> LevelLoadFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            gameObjectManager = GetComponent<GameObjectManager>();
        }

        private void Start()
        {
            if (loadOnStart)
                StartCoroutine(LoadStartupLevel());
        }

        private void OnDestroy()
        {
            UnsubscribeFromObjectives();
            if (Instance == this)
                Instance = null;
        }

        public void LoadLevel(int index)
        {
            if (index < 0 || index >= levelIds.Length)
            {
                ReportLoadFailure($"Level index {index} is outside the manifest.");
                return;
            }

            BeginLoad(levelIds[index], index);
        }

        public void LoadLevel(string levelId)
        {
            int index = Array.FindIndex(levelIds, id =>
                string.Equals(id, levelId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                ReportLoadFailure($"Level '{levelId}' is not listed in {manifestFileName}.");
                return;
            }

            BeginLoad(levelIds[index], index);
        }

        public void LoadNextLevel()
        {
            if (currentLevelIndex < 0)
            {
                ReportLoadFailure("No level is currently loaded.");
                return;
            }

            int nextIndex = currentLevelIndex + 1;
            if (nextIndex >= levelIds.Length)
            {
                AllLevelsCompleted?.Invoke();
                return;
            }

            LoadLevel(nextIndex);
        }

        public void RestartCurrentLevel()
        {
            if (currentLevelIndex < 0)
            {
                ReportLoadFailure("No level is currently loaded.");
                return;
            }

            LoadLevel(currentLevelIndex);
        }

        private IEnumerator LoadStartupLevel()
        {
            if (prefabCatalog == null)
            {
                ReportLoadFailure("Assign a LevelPrefabCatalog to the LevelManager.");
                yield break;
            }

            string manifestUri = BuildStreamingAssetsUri(Path.Combine(levelsRelativeDirectory, manifestFileName));
            using (UnityWebRequest request = UnityWebRequest.Get(manifestUri))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    ReportLoadFailure($"Could not load level manifest: {request.error}");
                    yield break;
                }

                LevelManifest manifest = null;
                string parseError = null;
                try
                {
                    manifest = JsonUtility.FromJson<LevelManifest>(request.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    parseError = exception.Message;
                }

                if (!string.IsNullOrEmpty(parseError))
                {
                    ReportLoadFailure($"Invalid level manifest JSON: {parseError}");
                    yield break;
                }

                if (!IsValidManifest(manifest, out string validationError))
                {
                    ReportLoadFailure(validationError);
                    yield break;
                }

                levelIds = manifest.levelIds;
            }

            int initialIndex = Mathf.Clamp(startingLevelIndex, 0, levelIds.Length - 1);
            BeginLoad(levelIds[initialIndex], initialIndex);
        }

        private void BeginLoad(string levelId, int manifestIndex)
        {
            if (activeLoad != null)
                StopCoroutine(activeLoad);

            activeLoad = StartCoroutine(LoadLevelRoutine(levelId, manifestIndex));
        }

        private IEnumerator LoadLevelRoutine(string levelId, int manifestIndex)
        {
            string levelUri = BuildStreamingAssetsUri(Path.Combine(levelsRelativeDirectory, $"{levelId}.json"));
            using (UnityWebRequest request = UnityWebRequest.Get(levelUri))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    activeLoad = null;
                    ReportLoadFailure($"Could not load level '{levelId}': {request.error}");
                    yield break;
                }

                LevelDocument level = null;
                string parseError = null;
                try
                {
                    level = JsonUtility.FromJson<LevelDocument>(request.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    parseError = exception.Message;
                }

                if (!string.IsNullOrEmpty(parseError))
                {
                    activeLoad = null;
                    ReportLoadFailure($"Invalid JSON for level '{levelId}': {parseError}");
                    yield break;
                }

                if (!IsValidLevel(level, levelId, out string validationError))
                {
                    activeLoad = null;
                    ReportLoadFailure(validationError);
                    yield break;
                }

                // Validate every prefab ID before clearing the currently playable level.
                for (int i = 0; i < level.objects.Length; i++)
                {
                    if (!prefabCatalog.TryGetPrefab(level.objects[i].prefabId, out _))
                    {
                        activeLoad = null;
                        ReportLoadFailure($"Level '{levelId}' references unknown prefab ID '{level.objects[i].prefabId}'.");
                        yield break;
                    }
                }

                UnsubscribeFromObjectives();
                currentLevelCompleted = false;
                gameObjectManager.ClearManagedObjects();

                for (int i = 0; i < level.objects.Length; i++)
                {
                    LevelObjectData objectData = level.objects[i];
                    prefabCatalog.TryGetPrefab(objectData.prefabId, out GameObject prefab);
                    LevelObjectData spawnData = LevelGridUtility.CreateSpawnData(level.grid, objectData);
                    if (!gameObjectManager.TrySpawn(prefab, spawnData, out GameObject spawnedObject))
                    {
                        gameObjectManager.ClearManagedObjects();
                        activeLoad = null;
                        ReportLoadFailure($"Could not spawn '{objectData.prefabId}' in level '{levelId}'.");
                        yield break;
                    }

                    if (spawnData.prefabId == "portal_pair" && spawnData.portalExitPlacement != null)
                    {
                        BulletPortalPair pair = spawnedObject.GetComponent<BulletPortalPair>();
                        if (pair != null)
                            pair.PlaceGridEndpoints(level.grid, gameObjectManager.transform,
                                spawnData.gridPlacement, spawnData.portalExitPlacement);
                    }

                    Target[] targets = spawnedObject.GetComponentsInChildren<Target>(true);
                    for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                    {
                        targets[targetIndex].Defeated += HandleTargetDefeated;
                        remainingTargets.Add(targets[targetIndex]);
                    }

                    BowlingCan[] bowlingCans = spawnedObject.GetComponentsInChildren<BowlingCan>(true);
                    for (int canIndex = 0; canIndex < bowlingCans.Length; canIndex++)
                    {
                        bowlingCans[canIndex].KnockedDown += HandleCanKnockedDown;
                        remainingBowlingCans.Add(bowlingCans[canIndex]);
                    }
                }

                ApplyCannonPlacement(level);

                currentLevel = level;
                currentLevelIndex = manifestIndex;
                Time.timeScale = 1f;
                activeLoad = null;

                if (remainingTargets.Count == 0 && remainingBowlingCans.Count == 0)
                    Debug.LogWarning($"Level '{levelId}' has no Target or BowlingCan objectives; no completion event will be raised.", this);

                LevelLoaded?.Invoke(currentLevelIndex, CurrentLevelId);
                Debug.Log($"Loaded level {currentLevelIndex + 1}/{levelIds.Length}: {CurrentLevelName}", this);
            }
        }

        private bool IsValidManifest(LevelManifest manifest, out string error)
        {
            error = null;
            if (manifest == null || manifest.schemaVersion != SupportedSchemaVersion)
            {
                error = $"Unsupported level manifest schema version. Expected {SupportedSchemaVersion}.";
                return false;
            }

            if (manifest.levelIds == null || manifest.levelIds.Length == 0)
            {
                error = "The level manifest must contain at least one level ID.";
                return false;
            }

            HashSet<string> uniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < manifest.levelIds.Length; i++)
            {
                string id = manifest.levelIds[i];
                if (!IsSafeLevelId(id) || !uniqueIds.Add(id))
                {
                    error = $"Level ID at manifest index {i} is empty, unsafe, or duplicated: '{id}'.";
                    return false;
                }
            }

            return true;
        }

        private bool IsValidLevel(LevelDocument level, string requestedId, out string error)
        {
            error = null;
            if (level == null || level.schemaVersion != SupportedSchemaVersion)
            {
                error = $"Level '{requestedId}' uses an unsupported schema version. Expected {SupportedSchemaVersion}.";
                return false;
            }

            if (!string.Equals(level.id, requestedId, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Level file ID '{level.id}' does not match requested ID '{requestedId}'.";
                return false;
            }

            if (level.objects == null || level.objects.Length > MaximumObjectsPerLevel)
            {
                error = $"Level '{requestedId}' must contain between 0 and {MaximumObjectsPerLevel} objects.";
                return false;
            }

            if (level.grid != null && !LevelGridUtility.IsValidGrid(level.grid))
            {
                error = $"Level '{requestedId}' has invalid grid settings.";
                return false;
            }

            if (level.cannonPlacement != null &&
                !LevelGridUtility.IsValidPlacement(level.grid, level.cannonPlacement))
            {
                error = $"Level '{requestedId}' has an invalid cannon grid placement.";
                return false;
            }

            for (int i = 0; i < level.objects.Length; i++)
            {
                LevelObjectData entry = level.objects[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.prefabId))
                {
                    error = $"Level '{requestedId}' has an object with no prefabId at index {i}.";
                    return false;
                }

                if (!IsFinite(entry.localPosition) || !IsFinite(entry.localEulerAngles) || !IsFinite(entry.localScale))
                {
                    error = $"Level '{requestedId}' has a non-finite transform at object index {i}.";
                    return false;
                }

                if (entry.gridPlacement != null &&
                    !LevelGridUtility.IsValidPlacement(level.grid, entry.gridPlacement))
                {
                    error = $"Level '{requestedId}' has an invalid grid placement at object index {i}.";
                    return false;
                }

                if (entry.portalExitPlacement != null &&
                    (!LevelGridUtility.IsValidPlacement(level.grid, entry.portalExitPlacement) ||
                     entry.gridPlacement == null ||
                     (entry.gridPlacement.cellX == entry.portalExitPlacement.cellX &&
                      entry.gridPlacement.cellY == entry.portalExitPlacement.cellY)))
                {
                    error = $"Level '{requestedId}' has an invalid portal exit at object index {i}.";
                    return false;
                }
            }

            return true;
        }

        private void ApplyCannonPlacement(LevelDocument level)
        {
            CannonController cannon = UnityEngine.Object.FindFirstObjectByType<CannonController>(FindObjectsInactive.Include);
            if (cannon == null)
            {
                if (level.cannonPlacement != null)
                    Debug.LogWarning($"Level '{level.id}' defines a cannon position, but no CannonController exists in the active scene.", this);
                return;
            }

            if (level.cannonPlacement == null)
            {
                cannon.ResetLevelPlacement();
                return;
            }

            Vector3 localPosition = LevelGridUtility.GetLocalPosition(level.grid, level.cannonPlacement);
            Vector3 worldPosition = gameObjectManager.transform.TransformPoint(localPosition);
            Quaternion worldRotation = gameObjectManager.transform.rotation *
                                       LevelGridUtility.GetGridRotation(level.grid) *
                                       Quaternion.Euler(0f, 0f, level.cannonPlacement.rotationDegrees);
            cannon.ApplyLevelPlacement(worldPosition, worldRotation);
        }

        private void HandleTargetDefeated(Target target)
        {
            if (!remainingTargets.Remove(target))
                return;

            TryCompleteCurrentLevel();
        }

        private void HandleCanKnockedDown(BowlingCan can)
        {
            if (can != null)
                can.KnockedDown -= HandleCanKnockedDown;

            if (!remainingBowlingCans.Remove(can))
                return;

            TryCompleteCurrentLevel();
        }

        private void TryCompleteCurrentLevel()
        {
            if (currentLevelCompleted || remainingTargets.Count > 0 || remainingBowlingCans.Count > 0)
                return;

            currentLevelCompleted = true;
            Time.timeScale = 0f;
            LevelCompleted?.Invoke(CurrentLevelId);
            Debug.Log($"Level completed: {CurrentLevelName}", this);
        }

        private void UnsubscribeFromObjectives()
        {
            for (int i = 0; i < remainingTargets.Count; i++)
            {
                if (remainingTargets[i] != null)
                    remainingTargets[i].Defeated -= HandleTargetDefeated;
            }

            remainingTargets.Clear();

            for (int i = 0; i < remainingBowlingCans.Count; i++)
            {
                if (remainingBowlingCans[i] != null)
                    remainingBowlingCans[i].KnockedDown -= HandleCanKnockedDown;
            }

            remainingBowlingCans.Clear();
        }

        private void ReportLoadFailure(string message)
        {
            Debug.LogError(message, this);
            LevelLoadFailed?.Invoke(message);
        }

        private string BuildStreamingAssetsUri(string relativePath)
        {
            string basePath = Application.streamingAssetsPath.TrimEnd('/', '\\');
            string combinedPath = $"{basePath}/{relativePath.Replace('\\', '/')}";
            if (combinedPath.Contains("://") || combinedPath.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
                return combinedPath;

            return new Uri(combinedPath).AbsoluteUri;
        }

        private static bool IsSafeLevelId(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
                return false;

            for (int i = 0; i < levelId.Length; i++)
            {
                char character = levelId[i];
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                    return false;
            }

            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    public sealed class LevelManifest
    {
        public int schemaVersion = 1;
        public string[] levelIds;
    }

    [Serializable]
    public sealed class LevelDocument
    {
        public int schemaVersion = 1;
        public string id;
        public string displayName;
        [Min(0)] public int startingBulletCount;
        public LevelGridData grid;
        public LevelGridPlacement cannonPlacement;
        public LevelObjectData[] objects;
    }

    [Serializable]
    public sealed class LevelObjectData
    {
        public string prefabId;
        public string instanceName;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        public LevelGridPlacement gridPlacement;
        public LevelGridPlacement portalExitPlacement;
    }
}
