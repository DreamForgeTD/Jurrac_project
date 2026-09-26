using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DreamForgeTD
{
    /// <summary>
    /// Loads gameplay levels from the LevelDefinition assets written by the Level Editor.
    /// The LevelDefinition list on this component is the runtime source of truth.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameObjectManager))]
    [DefaultExecutionOrder(-50)]
    public sealed class GameManager : MonoBehaviour
    {
        private const int SupportedSchemaVersion = 1;
        private const int MaximumObjectsPerLevel = 256;
        private const string LevelAssetsDirectory = "Assets/Project/Data/Levels";
        private const float LoseResultSettleDuration = 0.4f;

        public static GameManager Instance { get; private set; }

        [Header("Level Editor ScriptableObjects")]
        [SerializeField] private LevelPrefabCatalog prefabCatalog;
        [SerializeField] private LevelDefinition[] levelDefinitions = Array.Empty<LevelDefinition>();
        [SerializeField] private GameObject cannonPrefab;
        [SerializeField] private bool loadOnStart = true;
        [SerializeField, Min(0)] private int startingLevelIndex;

        [Header("Win Condition & Game Flow")]
        [Tooltip("Tự động chuyển level tiếp theo khi win sau một khoảng trễ")]
        [SerializeField] private bool autoNextLevelOnWin = true;
        [SerializeField, Min(0.5f)] private float nextLevelDelay = 1.5f;
        [Tooltip("Làm chậm thời gian khi bắn rơi lon cuối cùng (Victory slow-motion)")]
        [SerializeField] private bool slowMotionOnWin = true;

        [Header("In-Game HUD")]
        [SerializeField] private bool showInGameHUD = true;

        [Header("UI & Game Events")]
        [Tooltip("Sự kiện khi bắt đầu load level mới (LevelIndex, LevelId, DisplayName)")]
        public UnityEvent<int, string, string> onLevelLoaded = new UnityEvent<int, string, string>();

        [Tooltip("Sự kiện cập nhật số lon còn lại (Số lon còn lại, Tổng số lon)")]
        public UnityEvent<int, int> onCansCountChanged = new UnityEvent<int, int>();

        [Tooltip("Sự kiện kích hoạt khi người chơi bắn rơi hết toàn bộ lon (Win level)")]
        public UnityEvent onLevelWin = new UnityEvent();

        [Tooltip("Sự kiện khi người chơi đã hoàn thành tất cả các level")]
        public UnityEvent onAllLevelsCompleted = new UnityEvent();

        private readonly List<BowlingCan> danhSachLonTam = new List<BowlingCan>();
        private readonly List<BowlingCan> levelCans = new List<BowlingCan>();
        private GameObjectManager gameObjectManager;
        private CannonController cannonController;
        private CannonShooter cannonShooter;
        private LevelDocument currentLevelDoc;
        private Coroutine autoNextLevelRoutine;
        private int currentLevelIndex = -1;
        private int totalCansCount;
        private int remainingCansCount;
        private float loseResultStableTime;
        private bool cannonControlLocked;
        private bool cannonControllerWasEnabled;
        private bool cannonShooterWasEnabled;
        private bool isLevelWon;
        private bool isLevelLost;
        private GUIStyle hudStyle;
        private GUIStyle winBannerStyle;

        public int MaLuot { get; private set; }
        public bool DaHoanTatTatCaMan { get; private set; }
        public CannonShooter SungHienTai => cannonShooter;
        public CannonController DieuKhienSung => cannonController;
        public int CurrentLevelIndex => currentLevelIndex;
        public string CurrentLevelId => currentLevelDoc != null ? currentLevelDoc.id : string.Empty;
        public string CurrentLevelName => currentLevelDoc != null ? currentLevelDoc.displayName : string.Empty;
        public int TotalLevels => levelDefinitions != null ? levelDefinitions.Length : 0;
        public int TotalCans => totalCansCount;
        public int RemainingCans => remainingCansCount;
        public bool IsLevelWon => isLevelWon;
        public bool IsLevelLost => isLevelLost;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            gameObjectManager = GetComponent<GameObjectManager>();

            if (prefabCatalog == null)
            {
#if UNITY_EDITOR
                prefabCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelPrefabCatalog>(
                    "Assets/Project/Data/LevelPrefabCatalog.asset");
#endif
                if (prefabCatalog == null)
                    prefabCatalog = Resources.Load<LevelPrefabCatalog>("LevelPrefabCatalog");
            }

            if (cannonPrefab == null)
            {
#if UNITY_EDITOR
                cannonPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Project/Resoruce_game/Prefab/Cannon 1.prefab");
#endif
            }
        }

        /// <summary>
        /// Lets the Canvas UI own restart/next controls instead of the legacy IMGUI HUD
        /// and automatic level advance.
        /// </summary>
        public void UseManualUIFlow()
        {
            autoNextLevelOnWin = false;
            showInGameHUD = false;
            StopAutoNextLevel();
        }

        private void Start()
        {
            if (loadOnStart)
                LoadLevel(startingLevelIndex);
        }

        private void Update()
        {
            UpdateOutOfAmmoResult();
        }

        private void OnDestroy()
        {
            UnlockCannonControl();
            UnsubscribeCans();
            UnsubscribeFromCannonShooter();
            if (Instance == this)
                Instance = null;
        }

        public void LoadLevel(int index)
        {
            if (levelDefinitions == null || index < 0 || index >= levelDefinitions.Length)
            {
                ReportLoadFailure($"Level index {index} is outside the assigned LevelDefinition list.");
                return;
            }

            LoadDefinition(levelDefinitions[index], index);
        }

        public void LoadLevel(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId) || levelDefinitions == null)
            {
                ReportLoadFailure("A valid level ID and assigned LevelDefinition assets are required.");
                return;
            }

            for (int i = 0; i < levelDefinitions.Length; i++)
            {
                LevelDocument data = levelDefinitions[i] != null ? levelDefinitions[i].Data : null;
                if (data != null && string.Equals(data.id, levelId, StringComparison.OrdinalIgnoreCase))
                {
                    LoadDefinition(levelDefinitions[i], i);
                    return;
                }
            }

            ReportLoadFailure($"No assigned LevelDefinition has the ID '{levelId}'.");
        }

        public void NextLevel()
        {
            StopAutoNextLevel();
            Time.timeScale = 1f;
            int nextIndex = currentLevelIndex + 1;
            if (nextIndex >= 0 && nextIndex < TotalLevels)
            {
                LoadLevel(nextIndex);
                return;
            }

            DaHoanTatTatCaMan = true;
            onAllLevelsCompleted?.Invoke();
        }

        public void RestartLevel()
        {
            StopAutoNextLevel();
            Time.timeScale = 1f;
            if (currentLevelIndex >= 0)
                LoadLevel(currentLevelIndex);
            else
                LoadLevel(startingLevelIndex);
        }

        /// <summary>
        /// Ends the current level as a loss. Call this from the game's chosen lose condition.
        /// </summary>
        public void LoseCurrentLevel()
        {
            if (currentLevelDoc == null || isLevelWon || isLevelLost)
                return;

            StopAutoNextLevel();
            isLevelLost = true;
            LockCannonControl();
            Time.timeScale = 0f;
            Debug.Log($"[GameManager] Level '{CurrentLevelName}' lost.", this);
        }

        private void LoadDefinition(LevelDefinition definition, int definitionIndex)
        {
            if (gameObjectManager == null)
            {
                ReportLoadFailure("GameManager requires a GameObjectManager component on the same GameObject.");
                return;
            }

            if (prefabCatalog == null)
            {
                ReportLoadFailure("Assign a LevelPrefabCatalog to the GameManager.");
                return;
            }

            if (!TryValidateDefinition(definition, out LevelDocument data, out string validationError))
            {
                ReportLoadFailure(validationError);
                return;
            }

            GameObject[] prefabs = new GameObject[data.objects.Length];
            for (int i = 0; i < data.objects.Length; i++)
            {
                if (!prefabCatalog.TryGetPrefab(data.objects[i].prefabId, out prefabs[i]) || prefabs[i] == null)
                {
                    ReportLoadFailure(
                        $"Level '{data.id}' references prefab ID '{data.objects[i].prefabId}' which is missing from the catalog.");
                    return;
                }
            }

            StopAutoNextLevel();
            if (cannonShooter != null)
                cannonShooter.DonDanTrongMan();
            Time.timeScale = 1f;
            DaHoanTatTatCaMan = false;
            isLevelWon = false;
            isLevelLost = false;
            loseResultStableTime = 0f;
            UnsubscribeCans();
            gameObjectManager.ClearManagedObjects();
            ClearEditorPreviewRoot();
            currentLevelDoc = null;
            currentLevelIndex = -1;
            totalCansCount = 0;
            remainingCansCount = 0;

            for (int i = 0; i < data.objects.Length; i++)
            {
                // The Level Editor saves the final local transform in the SO. Keep that transform
                // authoritative at runtime; gridPlacement is editor metadata for snapping.
                LevelObjectData spawnData = data.objects[i];
                if (!gameObjectManager.TrySpawn(prefabs[i], spawnData, out GameObject spawnedObject))
                {
                    UnsubscribeCans();
                    gameObjectManager.ClearManagedObjects();
                    ReportLoadFailure($"Could not spawn object {i} ('{data.objects[i].prefabId}') from level '{data.id}'.");
                    return;
                }

                if (spawnData.prefabId == "portal_pair" && spawnData.portalExitPlacement != null)
                {
                    BulletPortalPair pair = spawnedObject.GetComponent<BulletPortalPair>();
                    if (pair != null)
                        pair.PlaceGridEndpoints(data.grid, gameObjectManager.transform,
                            spawnData.gridPlacement, spawnData.portalExitPlacement);
                }

                spawnedObject.GetComponentsInChildren(true, danhSachLonTam);
                for (int canIndex = 0; canIndex < danhSachLonTam.Count; canIndex++)
                {
                    if (danhSachLonTam[canIndex] == null || !danhSachLonTam[canIndex].isActiveAndEnabled)
                        continue;

                    // Count a can as soon as its knockdown is confirmed. Hidden remains a
                    // fallback for external deactivation; list removal makes both paths idempotent.
                    danhSachLonTam[canIndex].KnockedDown += HandleCanEliminated;
                    danhSachLonTam[canIndex].Hidden += HandleCanEliminated;
                    levelCans.Add(danhSachLonTam[canIndex]);
                }
            }

            currentLevelDoc = data;
            currentLevelIndex = definitionIndex;
            totalCansCount = levelCans.Count;
            remainingCansCount = totalCansCount;
            ApplyCannonPlacement(data);
            Physics.SyncTransforms();
            MaLuot++;

            if (totalCansCount == 0)
                Debug.LogWarning($"Level '{data.id}' contains no BowlingCan objectives.", this);

            Debug.Log(
                $"[GameManager] Loaded level {currentLevelIndex + 1}/{TotalLevels}: '{data.displayName}' " +
                $"({data.id}) from LevelDefinition '{definition.name}' — {data.objects.Length} objects, {totalCansCount} cans.",
                this);

            onLevelLoaded?.Invoke(currentLevelIndex, data.id, data.displayName);
            onCansCountChanged?.Invoke(remainingCansCount, totalCansCount);
        }

        private bool TryValidateDefinition(LevelDefinition definition, out LevelDocument data, out string error)
        {
            data = definition != null ? definition.Data : null;
            error = null;

            if (definition == null || data == null)
            {
                error = "The selected LevelDefinition asset is missing or has no data.";
                return false;
            }

            if (data.schemaVersion != SupportedSchemaVersion)
            {
                error = $"Level '{data.id}' uses schema version {data.schemaVersion}; expected {SupportedSchemaVersion}.";
                return false;
            }

            if (data.startingBulletCount < 0)
            {
                error = $"Level '{data.id}' has a negative starting bullet count.";
                return false;
            }

            if (!IsSafeLevelId(data.id))
            {
                error = $"LevelDefinition '{definition.name}' has an empty or unsafe level ID.";
                return false;
            }

            if (data.objects == null || data.objects.Length > MaximumObjectsPerLevel)
            {
                error = $"Level '{data.id}' must contain no more than {MaximumObjectsPerLevel} objects.";
                return false;
            }

            if (!LevelGridUtility.IsValidGrid(data.grid))
            {
                error = $"Level '{data.id}' has invalid grid settings.";
                return false;
            }

            if (data.cannonPlacement != null &&
                !LevelGridUtility.IsValidPlacement(data.grid, data.cannonPlacement))
            {
                error = $"Level '{data.id}' has an invalid cannon grid placement.";
                return false;
            }

            for (int i = 0; i < data.objects.Length; i++)
            {
                LevelObjectData item = data.objects[i];
                if (item == null || string.IsNullOrWhiteSpace(item.prefabId))
                {
                    error = $"Level '{data.id}' has an object without a prefab ID at index {i}.";
                    return false;
                }

                if (!IsFinite(item.localPosition) || !IsFinite(item.localEulerAngles) || !IsFinite(item.localScale))
                {
                    error = $"Level '{data.id}' has a non-finite transform at object index {i}.";
                    return false;
                }

                if (item.prefabId == "portal_pair" && item.portalExitPlacement != null &&
                    (!LevelGridUtility.IsValidPlacement(data.grid, item.portalExitPlacement) ||
                     item.gridPlacement == null ||
                     (item.gridPlacement.cellX == item.portalExitPlacement.cellX &&
                      item.gridPlacement.cellY == item.portalExitPlacement.cellY)))
                {
                    error = $"Level '{data.id}' has an invalid portal exit at object index {i}.";
                    return false;
                }

            }

            return true;
        }

        private void ApplyCannonPlacement(LevelDocument data)
        {
            UnlockCannonControl();

            // Ignore stale disabled scene instances; use the assigned prefab fallback if no live cannon exists.
            CannonController cannon = cannonController;
            if (cannon == null)
                cannon = FindFirstObjectByType<CannonController>();
            if (cannon == null && cannonPrefab != null)
            {
                GameObject cannonObject = Instantiate(cannonPrefab, transform, false);
                cannonObject.name = "Cannon";
                cannon = cannonObject.GetComponentInChildren<CannonController>(true);
                if (cannon == null)
                {
                    Destroy(cannonObject);
                    Debug.LogError("The assigned Cannon Prefab has no CannonController component.", this);
                    return;
                }
            }

            if (cannon == null)
            {
                if (data.cannonPlacement != null)
                    Debug.LogError($"Level '{data.id}' defines a cannon placement, but no CannonController or Cannon Prefab is available.", this);
                return;
            }

            cannonController = cannon;
            CannonShooter shooter = cannon.GetComponentInChildren<CannonShooter>(true);
            BindToCannonShooter(shooter);
            if (shooter != null)
            {
                int levelBulletCount = data.startingBulletCount > 0
                    ? data.startingBulletCount
                    : shooter.StartingBulletCount;

                shooter.NapDanTheoMan(levelBulletCount);
            }

            if (data.cannonPlacement == null)
            {
                cannon.ResetLevelPlacement();
                return;
            }

            Vector3 localPosition = LevelGridUtility.GetLocalPosition(data.grid, data.cannonPlacement);
            // Keep the cannon and its bullets on the level's physics plane. The orthographic
            // camera can make different Z values look aligned even though their colliders miss.
            Quaternion localRotation = LevelGridUtility.GetGridRotation(data.grid) *
                                       Quaternion.Euler(0f, 0f, data.cannonPlacement.rotationDegrees);
            Vector3 worldPosition = gameObjectManager.transform.TransformPoint(localPosition);
            Quaternion worldRotation = gameObjectManager.transform.rotation * localRotation;
            cannon.ApplyLevelPlacement(worldPosition, worldRotation);
        }

        private void BindToCannonShooter(CannonShooter shooter)
        {
            if (cannonShooter == shooter)
                return;

            UnsubscribeFromCannonShooter();
            cannonShooter = shooter;
            if (cannonShooter == null)
                return;

            cannonShooter.BulletCountChanged += HandleBulletCountChanged;
        }

        private void UnsubscribeFromCannonShooter()
        {
            if (cannonShooter == null)
                return;

            cannonShooter.BulletCountChanged -= HandleBulletCountChanged;
            cannonShooter = null;
        }

        private void HandleBulletCountChanged(int remainingBullets, int totalBullets)
        {
            CheckForOutOfAmmoLoss();
        }

        private void CheckForOutOfAmmoLoss()
        {
            if (currentLevelDoc == null || isLevelWon || isLevelLost || cannonShooter == null)
                return;

            if (cannonShooter.RemainingBulletCount <= 0)
                LockCannonControl();
            else
                UnlockCannonControl();
        }

        private void UpdateOutOfAmmoResult()
        {
            if (currentLevelDoc == null || isLevelWon || isLevelLost || totalCansCount <= 0)
            {
                loseResultStableTime = 0f;
                return;
            }

            // A cleared objective always wins before the out-of-ammo check.
            if (remainingCansCount <= 0)
            {
                TriggerLevelWin();
                return;
            }

            if (cannonShooter == null || cannonShooter.RemainingBulletCount > 0)
            {
                loseResultStableTime = 0f;
                return;
            }

            LockCannonControl();

            if (cannonShooter.HasPendingShot || cannonShooter.ActiveProjectileCount > 0 || HasMovingLevelCan())
            {
                loseResultStableTime = 0f;
                return;
            }

            loseResultStableTime += Time.unscaledDeltaTime;
            if (loseResultStableTime >= LoseResultSettleDuration)
                LoseCurrentLevel();
        }

        private bool HasMovingLevelCan()
        {
            for (int i = 0; i < levelCans.Count; i++)
            {
                BowlingCan can = levelCans[i];
                if (can != null && can.isActiveAndEnabled && can.DangChuyenDong)
                    return true;
            }

            return false;
        }

        private void LockCannonControl()
        {
            if (cannonControlLocked)
                return;

            cannonControlLocked = true;
            cannonControllerWasEnabled = cannonController != null && cannonController.enabled;
            cannonShooterWasEnabled = cannonShooter != null && cannonShooter.enabled;

            if (cannonController != null)
                cannonController.enabled = false;
            if (cannonShooter != null)
                cannonShooter.enabled = false;
        }

        private void UnlockCannonControl()
        {
            if (!cannonControlLocked)
                return;

            if (cannonController != null)
                cannonController.enabled = cannonControllerWasEnabled;
            if (cannonShooter != null)
                cannonShooter.enabled = cannonShooterWasEnabled;

            cannonControlLocked = false;
        }

        private void HandleCanEliminated(BowlingCan can)
        {
            if (can != null)
            {
                can.KnockedDown -= HandleCanEliminated;
                can.Hidden -= HandleCanEliminated;
            }

            if (isLevelWon || isLevelLost || can == null || !levelCans.Remove(can))
                return;

            remainingCansCount = levelCans.Count;
            onCansCountChanged?.Invoke(remainingCansCount, totalCansCount);

            if (remainingCansCount <= 0)
                TriggerLevelWin();
        }

        private void TriggerLevelWin()
        {
            if (isLevelWon || isLevelLost)
                return;

            isLevelWon = true;
            LockCannonControl();
            Debug.Log($"[GameManager] Level '{CurrentLevelName}' completed.", this);

            if (slowMotionOnWin)
                Time.timeScale = 0.35f;

            onLevelWin?.Invoke();
            if (autoNextLevelOnWin)
                autoNextLevelRoutine = StartCoroutine(AutoNextLevelRoutine());
        }

        private IEnumerator AutoNextLevelRoutine()
        {
            yield return new WaitForSecondsRealtime(nextLevelDelay);
            autoNextLevelRoutine = null;
            NextLevel();
        }

        private void StopAutoNextLevel()
        {
            if (autoNextLevelRoutine == null)
                return;

            StopCoroutine(autoNextLevelRoutine);
            autoNextLevelRoutine = null;
        }

        private void UnsubscribeCans()
        {
            for (int i = 0; i < levelCans.Count; i++)
            {
                if (levelCans[i] != null)
                {
                    levelCans[i].KnockedDown -= HandleCanEliminated;
                    levelCans[i].Hidden -= HandleCanEliminated;
                }
            }

            levelCans.Clear();
        }

        private void ClearEditorPreviewRoot()
        {
            GameObject editorRoot = GameObject.Find("[Level Editor Root]");
            if (editorRoot != null)
            {
                editorRoot.SetActive(false);
                Destroy(editorRoot);
            }
        }

        private void ReportLoadFailure(string message)
        {
            Debug.LogError($"[GameManager] {message}", this);
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

#if UNITY_EDITOR
        public void RefreshLevelDefinitionsFromAssets()
        {
            string[] assetGuids = UnityEditor.AssetDatabase.FindAssets("t:LevelDefinition", new[] { LevelAssetsDirectory });
            List<LevelDefinition> definitions = new List<LevelDefinition>(assetGuids.Length);
            for (int i = 0; i < assetGuids.Length; i++)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                LevelDefinition definition = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDefinition>(assetPath);
                if (definition != null && definition.Data != null && IsSafeLevelId(definition.Data.id))
                    definitions.Add(definition);
            }

            definitions.Sort((left, right) => string.Compare(
                left.Data.id,
                right.Data.id,
                StringComparison.OrdinalIgnoreCase));

            levelDefinitions = definitions.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        private void OnGUI()
        {
            if (!showInGameHUD || !Application.isPlaying)
                return;

            InitHUDStyles();
            float boxWidth = Mathf.Min(340, Screen.width - 20);
            Rect hudRect = new Rect((Screen.width - boxWidth) * 0.5f, 10, boxWidth, isLevelWon ? 95 : 68);

            GUI.Box(hudRect, GUIContent.none, GUI.skin.box);
            GUILayout.BeginArea(hudRect);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Level {currentLevelIndex + 1}/{TotalLevels}:</b> {CurrentLevelName}", hudStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Lon còn lại: <b>{remainingCansCount} / {totalCansCount}</b>", hudStyle);
            if (GUILayout.Button("🔄 Chơi lại", GUILayout.Width(75), GUILayout.Height(22)))
                RestartLevel();
            if (GUILayout.Button("⏭️ Next", GUILayout.Width(60), GUILayout.Height(22)))
                NextLevel();
            GUILayout.EndHorizontal();

            if (isLevelWon)
            {
                GUILayout.Space(2);
                GUI.color = Color.green;
                GUILayout.Label("🎉 CHIẾN THẮNG! Đang chuyển Level...", winBannerStyle);
                GUI.color = Color.white;
            }

            GUILayout.EndArea();
        }

        private void InitHUDStyles()
        {
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (winBannerStyle == null)
            {
                winBannerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }
    }
}
