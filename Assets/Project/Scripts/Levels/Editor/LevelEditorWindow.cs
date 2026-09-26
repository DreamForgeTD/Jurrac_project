using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DreamForgeTD.EditorTools
{
    public sealed class LevelEditorWindow : UnityEditor.EditorWindow
    {
        private const string RootName = "[Level Editor Root]";
        private const string DefaultCatalogPath = "Assets/Project/Data/LevelPrefabCatalog.asset";
        private const string LevelAssetsDirectory = "Assets/Project/Data/Levels";
        private const string LevelsFolderRelative = "DreamForgeTD/Levels";
        private const string CannonPrefabPath = "Assets/Project/Resoruce_game/Prefab/Cannon 1.prefab";

        private const int GridColumns = 18;
        private const int GridRows = 32;
        private const float DefaultCellUnits = 0.3875f;

        [Serializable]
        public sealed class GridCell
        {
            public bool isOccupied;
            public string prefabId = "";
            public float rotationDegrees = 0f;
            public int footprintWidth = 1;
            public int footprintHeight = 1;
            public int portalPairId = -1;
            public bool portalIsExit;
        }

        public enum ActivePaletteItem
        {
            Cannon,
            SodaCan,
            Target,
            TuongCube,
            BounceWall,
            PortalPair,
            Magnet,
            OtherCatalogItem,
            Erase
        }

        private enum RotationSelectionKind
        {
            None,
            PlacedObject,
            Cannon
        }

        [SerializeField] private LevelPrefabCatalog catalog;
        private ActivePaletteItem currentPalette = ActivePaletteItem.SodaCan;
        private string customPrefabId = "";
        private float currentRotation = 0f;
        private bool rotatePlacedObjectsMode;
        private RotationSelectionKind rotationSelectionKind;
        private Vector2Int selectedObjectAnchor;
        private bool syncToScene = true;
        private bool previewSyncScheduled;
        private double previewSyncRequestedAt;

        // Grid data at double resolution (18x32).
        private GridCell[,] gridCells = new GridCell[GridColumns, GridRows];
        private int[,] occupiedAnchorX = new int[GridColumns, GridRows];
        private int[,] occupiedAnchorY = new int[GridColumns, GridRows];
        private Vector2Int cannonPos;
        private int cannonFootprintWidth = 1;
        private int cannonFootprintHeight = 1;
        private float cannonRotation = 0f;

        // Quản lý level
        private List<string> manifestLevelIds = new List<string>();
        private int selectedManifestIndex = 0;
        private string currentLevelId = "level_01";
        private string currentDisplayName = "Soda Can Bowling";
        private int soDanBatDau;
        private bool creatingNewLevel;

        private Vector2 mainScroll;
        private GUIStyle cellStyle;
        private GUIStyle headerStyle;
        private GUIStyle paletteButtonStyle;

        [MenuItem("Tools/DreamForge/Level Editor", false, 10)]
        public static void OpenWindow()
        {
            LevelEditorWindow window = GetWindow<LevelEditorWindow>("Level Editor (18x32)");
            window.minSize = new Vector2(740, 720);
            window.Show();
        }

        private void OnEnable()
        {
            InitializeGrid();
            LoadCatalog();
            LoadCannonFootprint();
            cannonPos = GetDefaultCannonPosition();
            RefreshManifest();

            if (!string.IsNullOrEmpty(currentLevelId))
            {
                LoadLevel(currentLevelId, false);
            }
        }

        private void OnDisable()
        {
            if (!previewSyncScheduled)
                return;

            EditorApplication.update -= RunScheduledPreviewSync;
            previewSyncScheduled = false;

            if (syncToScene && !EditorApplication.isPlayingOrWillChangePlaymode)
                SyncGridToScene();
        }

        private void LoadCatalog()
        {
            if (catalog == null)
            {
                catalog = AssetDatabase.LoadAssetAtPath<LevelPrefabCatalog>(DefaultCatalogPath);
                if (catalog == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:LevelPrefabCatalog");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        catalog = AssetDatabase.LoadAssetAtPath<LevelPrefabCatalog>(path);
                    }
                }
            }
        }

        private void InitializeGrid()
        {
            if (gridCells == null || gridCells.GetLength(0) != GridColumns || gridCells.GetLength(1) != GridRows)
            {
                gridCells = new GridCell[GridColumns, GridRows];
            }

            if (occupiedAnchorX == null || occupiedAnchorY == null ||
                occupiedAnchorX.GetLength(0) != GridColumns || occupiedAnchorX.GetLength(1) != GridRows ||
                occupiedAnchorY.GetLength(0) != GridColumns || occupiedAnchorY.GetLength(1) != GridRows)
            {
                occupiedAnchorX = new int[GridColumns, GridRows];
                occupiedAnchorY = new int[GridColumns, GridRows];
            }

            for (int x = 0; x < GridColumns; x++)
            {
                for (int y = 0; y < GridRows; y++)
                {
                    if (gridCells[x, y] == null)
                    {
                        gridCells[x, y] = new GridCell();
                    }
                }
            }
        }

        private void RefreshManifest()
        {
            string preferredLevelId = currentLevelId;
            manifestLevelIds.Clear();
            string manifestPath = GetManifestPath();
            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    LevelManifest manifest = JsonUtility.FromJson<LevelManifest>(json);
                    if (manifest != null && manifest.levelIds != null)
                    {
                        manifestLevelIds.AddRange(manifest.levelIds);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LevelEditor] Không thể đọc levels.json: {ex.Message}");
                }
            }

            if (manifestLevelIds.Count > 0)
            {
                int preferredIndex = manifestLevelIds.FindIndex(id =>
                    string.Equals(id, preferredLevelId, StringComparison.OrdinalIgnoreCase));
                selectedManifestIndex = preferredIndex >= 0
                    ? preferredIndex
                    : Mathf.Clamp(selectedManifestIndex, 0, manifestLevelIds.Count - 1);
                currentLevelId = manifestLevelIds[selectedManifestIndex];
            }
        }

        private string GetLevelsDirectory()
        {
            return Path.Combine(Application.streamingAssetsPath, LevelsFolderRelative).Replace('\\', '/');
        }

        private LevelGridData CreateGridData()
        {
            GameObjectManager manager = FindFirstObjectByType<GameObjectManager>(FindObjectsInactive.Include);
            LevelBoardBounds boardBounds = FindFirstObjectByType<LevelBoardBounds>(FindObjectsInactive.Include);
            if (boardBounds != null && boardBounds.TryGetGridData(
                    manager != null ? manager.transform : null,
                    GridColumns,
                    GridRows,
                    out LevelGridData sceneGrid))
            {
                return sceneGrid;
            }

            return new LevelGridData
            {
                columns = GridColumns,
                rows = GridRows,
                referenceWidth = 1080,
                referenceHeight = 1920,
                worldUnitsPerCell = DefaultCellUnits,
                localCenter = new Vector3(0f, 1f, 0f)
            };
        }

        private string GetManifestPath()
        {
            return Path.Combine(GetLevelsDirectory(), "levels.json").Replace('\\', '/');
        }

        private void OnGUI()
        {
            InitStyles();
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

            DrawTopBar();
            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();

            // Left pane keeps the same 9:16 board area with 18x32 logical cells.
            DrawPhoneBoard();

            GUILayout.Space(16);

            // Cột bên phải: Bảng chọn Palette các ô màu & chức năng
            DrawPaletteSidebar();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        private void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (cellStyle == null)
            {
                cellStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    margin = new RectOffset(1, 1, 1, 1),
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }

            if (paletteButtonStyle == null)
            {
                paletteButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(8, 8, 6, 6)
                };
            }
        }

        private void DrawTopBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("LEVEL DESIGN GRID (18x32)", headerStyle, GUILayout.Width(230));

            // Dropdown chọn level
            if (manifestLevelIds.Count > 0)
            {
                int newIndex = EditorGUILayout.Popup(selectedManifestIndex, manifestLevelIds.ToArray(), GUILayout.Width(100));
                if (newIndex != selectedManifestIndex)
                {
                    selectedManifestIndex = newIndex;
                    currentLevelId = manifestLevelIds[selectedManifestIndex];
                    creatingNewLevel = false;
                    LoadLevel(currentLevelId, true);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No saved levels", EditorStyles.miniLabel, GUILayout.Width(100));
            }

            if (GUILayout.Button(new GUIContent("New Level", "Start a blank level and clear the current grid."),
                    GUILayout.Height(24), GUILayout.Width(90)))
            {
                CreateNewLevel();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID", GUILayout.Width(20));

            currentLevelId = EditorGUILayout.TextField(currentLevelId, GUILayout.Width(90));
            EditorGUILayout.LabelField("Name", GUILayout.Width(38));
            currentDisplayName = EditorGUILayout.TextField(currentDisplayName, GUILayout.MinWidth(110));
            if (creatingNewLevel)
            {
                EditorGUILayout.LabelField("NEW", EditorStyles.miniBoldLabel, GUILayout.Width(32));
            }

            // Nút Lưu Level nổi bật
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f, 1f);
            if (GUILayout.Button("💾 LƯU LEVEL", GUILayout.Height(24), GUILayout.Width(105)))
            {
                SaveLevel();
            }
            GUI.backgroundColor = prevColor;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Số đạn màn này", GUILayout.Width(90));
            soDanBatDau = Mathf.Max(0, EditorGUILayout.IntField(soDanBatDau, GUILayout.Width(70)));
            EditorGUILayout.LabelField("0 = dùng mức mặc định của súng", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawPhoneBoard()
        {
            RebuildOccupiedCellMap();
            EditorGUILayout.BeginVertical(GUILayout.Width(340));

            // Khung ngoài điện thoại
            Rect phoneFrame = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.Box(phoneFrame, GUIContent.none);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📱 MÀN HÌNH EDITOR (1080x1920)", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Click/Rê chuột để tô ô", EditorStyles.miniLabel, GUILayout.Width(130));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 18x32 editor cells fill the same 9:16 board.
            float cellPixelSize = Mathf.Min(34f, Mathf.Min(306f / GridColumns, 544f / GridRows));
            float boardWidth = GridColumns * cellPixelSize;
            float boardHeight = GridRows * cellPixelSize;

            Rect boardRect = GUILayoutUtility.GetRect(boardWidth, boardHeight, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

            // Vẽ viền đen màn hình
            EditorGUI.DrawRect(new Rect(boardRect.x - 2, boardRect.y - 2, boardWidth + 4, boardHeight + 4), new Color(0.1f, 0.1f, 0.12f, 1f));

            Event e = Event.current;
            bool isMouseHovering = boardRect.Contains(e.mousePosition);
            LevelGridPlacement cannonPlacement = GetCannonPlacement();

            for (int y = 0; y < GridRows; y++)
            {
                for (int x = 0; x < GridColumns; x++)
                {
                    Rect cellRect = new Rect(boardRect.x + x * cellPixelSize, boardRect.y + y * cellPixelSize, cellPixelSize - 1, cellPixelSize - 1);

                    bool isCannonCell = IsCellInsidePlacement(x, y, cannonPlacement);
                    bool isCannonAnchor = cannonPos.x == x && cannonPos.y == y;
                    int anchorX = occupiedAnchorX[x, y];
                    int anchorY = occupiedAnchorY[x, y];
                    bool isOccupiedCell = anchorX >= 0 && anchorY >= 0;
                    bool isObjectAnchor = isOccupiedCell && anchorX == x && anchorY == y;
                    GridCell cell = isOccupiedCell ? gridCells[anchorX, anchorY] : gridCells[x, y];
                    bool isSelectedCell = rotationSelectionKind == RotationSelectionKind.Cannon
                        ? isCannonCell
                        : rotationSelectionKind == RotationSelectionKind.PlacedObject && isOccupiedCell &&
                          anchorX == selectedObjectAnchor.x && anchorY == selectedObjectAnchor.y;

                    // Xác định màu sắc và nội dung hiển thị
                    Color cellColor;
                    string cellLabel;

                    if (isCannonCell)
                    {
                        cellColor = new Color(1f, 0.78f, 0.12f, 1f); // Vàng Cannon
                        cellLabel = isCannonAnchor
                            ? cellPixelSize < 24f ? "C" : GetArrowForRotation(cannonRotation) + "\nCANON"
                            : "";
                    }
                    else if (isOccupiedCell)
                    {
                        cellColor = GetColorForPrefabId(cell.prefabId);
                        cellLabel = isObjectAnchor
                            ? (cell.prefabId == "portal_pair"
                                ? (cellPixelSize < 24f
                                    ? (cell.portalIsExit ? "X" : "E")
                                    : (cell.portalIsExit ? "EXIT" : "ENTRY"))
                                : cellPixelSize < 24f
                                    ? GetCompactLabelForPrefabId(cell.prefabId)
                                    : GetShortLabelForPrefabId(cell.prefabId))
                            : "";
                    }
                    else
                    {
                        // Ô trống
                        cellColor = ((x + y) % 2 == 0)
                            ? new Color(0.22f, 0.22f, 0.25f, 1f)
                            : new Color(0.26f, 0.26f, 0.30f, 1f);
                        cellLabel = "";
                    }

                    // Tô màu nền ô
                    EditorGUI.DrawRect(cellRect, cellColor);

                    // Vẽ nhãn chữ
                    if (!string.IsNullOrEmpty(cellLabel))
                    {
                        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = cellPixelSize < 24f ? 7 : 8,
                            normal = { textColor = (isCannonCell || (isOccupiedCell && cell.prefabId == "magnet")) ? Color.black : Color.white }
                        };
                        GUI.Label(cellRect, cellLabel, labelStyle);
                    }

                    if (isSelectedCell)
                    {
                        Handles.color = Color.cyan;
                        Handles.DrawWireCube(cellRect.center, new Vector3(cellPixelSize, cellPixelSize, 0));
                    }

                    // Xử lý Click hoặc Drag chuột vào ô
                    if (cellRect.Contains(e.mousePosition))
                    {
                        // Highlight hover viền trắng
                        Handles.color = isSelectedCell ? Color.cyan : Color.white;
                        Handles.DrawWireCube(cellRect.center, new Vector3(cellPixelSize, cellPixelSize, 0));

                        if ((e.type == EventType.MouseDown ||
                             (!rotatePlacedObjectsMode && e.type == EventType.MouseDrag &&
                              (currentPalette != ActivePaletteItem.PortalPair || e.button == 1))) &&
                            (e.button == 0 || e.button == 1))
                        {
                            if (e.button == 1) // Chuột phải: Xóa nhanh
                            {
                                ClearCell(x, y);
                            }
                            else if (rotatePlacedObjectsMode)
                            {
                                SelectPlacedObjectForRotation(x, y);
                            }
                            else // Chuột trái: Áp dụng Palette hiện tại
                            {
                                ApplyPaletteToCell(x, y);
                            }

                            e.Use();
                            Repaint();

                            if (syncToScene)
                                SchedulePreviewSync();
                        }
                    }
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🗑️ Xóa trắng lưới", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("Xác nhận", "Bạn có chắc muốn xóa toàn bộ vật thể trên lưới?", "Xóa", "Hủy"))
                {
                    ClearAllCells();
                }
            }
            if (GUILayout.Button("🔄 Đặt lại Cannon đáy", GUILayout.Height(24)))
            {
                cannonPos = GetDefaultCannonPosition();
                cannonRotation = 0f;
                Repaint();
                if (syncToScene) SyncGridToScene();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawPaletteSidebar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));

            EditorGUILayout.LabelField("🎨 BẢNG CHỌN Ô MÀU (PALETTE)", headerStyle);
            EditorGUILayout.LabelField("Chọn loại ô bên dưới, sau đó click vào màn hình bên trái để tô:", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(8);

            // 1. Ô Lon nước Win (soda_can)
            DrawPaletteOption(ActivePaletteItem.SodaCan, "🥤 Chai / Lon Win (Soda Can)", new Color(0.92f, 0.26f, 0.24f), "soda_can");

            // 2. Ô Target (target)
            DrawPaletteOption(ActivePaletteItem.Target, "🎯 Mục tiêu Target (Bia bắn)", new Color(0.95f, 0.48f, 0.15f), "target");

            // 3. Ô Cannon (cannon)
            DrawPaletteOption(ActivePaletteItem.Cannon, "🚀 Vị trí Cannon (Súng)", new Color(1f, 0.8f, 0.1f), "cannon");

            // 4. Tường Cube có component TuongNay, hệ số bật nảy bằng 1.
            DrawPaletteOption(ActivePaletteItem.TuongCube, "🧱 Tường Cube (nảy mức 1)", new Color(0.42f, 0.62f, 0.68f), "wall");

            // 5. Ô Bounce Wall (bounce_wall)
            DrawPaletteOption(ActivePaletteItem.BounceWall, "🧱 Tường nảy (Bounce Wall)", new Color(0.2f, 0.58f, 0.95f), "bounce_wall");

            // 6. Ô Portal Pair (portal_pair)
            DrawPaletteOption(ActivePaletteItem.PortalPair, "🌀 Portal: click Entry rồi Exit", new Color(0.68f, 0.28f, 0.95f), "portal_pair");
            if (TryGetPendingPortalEntry(out int pendingX, out int pendingY))
                EditorGUILayout.HelpBox($"Portal Entry: ({pendingX}, {pendingY}). Click another cell for Exit.", MessageType.Info);

            // 7. Ô Magnet (magnet)
            DrawPaletteOption(ActivePaletteItem.Magnet, "🧲 Lực hút Nam châm (Magnet)", new Color(0.12f, 0.78f, 0.72f), "magnet");

            // 8. Ô Tẩy xóa (Erase)
            DrawPaletteOption(ActivePaletteItem.Erase, "✖️ Tẩy / Xóa ô", new Color(0.45f, 0.45f, 0.48f), "");

            // Nếu catalog có prefab khác
            DrawExtraCatalogOptions();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("⚙️ Tùy chỉnh góc xoay vật thể", EditorStyles.boldLabel);

            bool nextRotateMode = EditorGUILayout.ToggleLeft(
                new GUIContent("↻ Rotate existing object", "When enabled, click an object to select it and edit only its angle. Placement is paused."),
                rotatePlacedObjectsMode);
            if (nextRotateMode != rotatePlacedObjectsMode)
            {
                rotatePlacedObjectsMode = nextRotateMode;
                ClearRotationSelection();
            }

            if (rotatePlacedObjectsMode)
            {
                if (TryGetSelectedRotation(out float selectedRotation, out string selectedLabel))
                {
                    float nextRotation = LevelGridUtility.NormalizeRotation(
                        EditorGUILayout.FloatField("Object angle (°)", selectedRotation));
                    if (!Mathf.Approximately(nextRotation, selectedRotation))
                    {
                        ApplyRotationToSelection(nextRotation);
                        selectedRotation = nextRotation;
                    }

                    EditorGUILayout.HelpBox($"Selected: {selectedLabel}, {selectedRotation:0.##}°. Change this value to rotate only the selected object.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Click one object on the board to select it, then edit its angle here. The click will not move or rotate it.", MessageType.Info);
                }
            }
            else
            {
                currentRotation = LevelGridUtility.NormalizeRotation(
                    EditorGUILayout.FloatField("New object angle (°)", currentRotation));
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("📊 Thống kê nhanh", EditorStyles.boldLabel);
            int canCount = 0;
            int targetCount = 0;
            int soTuongCubeNayMucMot = 0;
            int soTuongNay = 0;
            int totalOccupied = 0;

            for (int x = 0; x < GridColumns; x++)
            {
                for (int y = 0; y < GridRows; y++)
                {
                    if (gridCells[x, y].isOccupied)
                    {
                        totalOccupied++;
                        string id = gridCells[x, y].prefabId;
                        if (id == "soda_can") canCount++;
                        else if (id == "target") targetCount++;
                        else if (id == "wall") soTuongCubeNayMucMot++;
                        else if (id == "bounce_wall") soTuongNay++;
                    }
                }
            }

            EditorGUILayout.LabelField($"• Vị trí Cannon: ({cannonPos.x}, {cannonPos.y}) - Hướng: {cannonRotation}°");
            EditorGUILayout.LabelField($"• Số lon Soda (Win): {canCount}");
            EditorGUILayout.LabelField($"• Số mục tiêu Target: {targetCount}");
            EditorGUILayout.LabelField($"• Số tường Cube nảy mức 1: {soTuongCubeNayMucMot}");
            EditorGUILayout.LabelField($"• Số tường bật nảy: {soTuongNay}");
            EditorGUILayout.LabelField($"• Tổng ô vật thể: {totalOccupied}");

            if (canCount == 0 && targetCount == 0)
            {
                EditorGUILayout.HelpBox("Lưu ý: Level chưa có Lon soda hoặc Target nào làm điều kiện chiến thắng!", MessageType.Warning);
            }

            EditorGUILayout.Space(12);
            syncToScene = EditorGUILayout.ToggleLeft("⚡ Tự động đồng bộ lên 3D Scene khi click", syncToScene);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load to Scene 3D", GUILayout.Height(26)))
            {
                SyncGridToScene();
            }
            if (GUILayout.Button("Xóa Preview Scene", GUILayout.Height(26)))
            {
                ClearScenePreview();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawPaletteOption(ActivePaletteItem item, string label, Color color, string prefabId)
        {
            bool isSelected = (currentPalette == item && (item != ActivePaletteItem.OtherCatalogItem || customPrefabId == prefabId));

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? color : Color.Lerp(color, Color.gray, 0.45f);

            string prefix = isSelected ? "▶ " : "   ";
            if (GUILayout.Button(prefix + label, paletteButtonStyle, GUILayout.Height(30)))
            {
                currentPalette = item;
                if (item == ActivePaletteItem.OtherCatalogItem)
                {
                    customPrefabId = prefabId;
                }
            }

            GUI.backgroundColor = prevBg;
            EditorGUILayout.Space(2);
        }

        private void DrawExtraCatalogOptions()
        {
            if (catalog == null) return;

            for (int i = 0; i < catalog.EntryCount; i++)
            {
                LevelPrefabEntry entry = catalog.GetEntry(i);
                if (entry.Id == "soda_can" || entry.Id == "target" || entry.Id == "wall" ||
                    entry.Id == "bounce_wall" || entry.Id == "portal_pair" || entry.Id == "magnet")
                {
                    continue; // Đã có mục riêng ở trên
                }

                DrawPaletteOption(ActivePaletteItem.OtherCatalogItem, $"📦 {entry.Id}", new Color(0.4f, 0.7f, 0.4f), entry.Id);
            }
        }

        private void ApplyPaletteToCell(int x, int y)
        {
            if (currentPalette == ActivePaletteItem.Cannon)
            {
                LevelGridPlacement cannonPlacement = new LevelGridPlacement
                {
                    cellX = x,
                    cellY = y,
                    footprintWidth = cannonFootprintWidth,
                    footprintHeight = cannonFootprintHeight,
                    rotationDegrees = currentRotation
                };
                if (!IsPlacementAreaAvailable(cannonPlacement, -1, -1, false))
                    return;

                cannonPos = new Vector2Int(x, y);
                cannonRotation = currentRotation;
                return;
            }

            if (currentPalette == ActivePaletteItem.Erase)
            {
                ClearCell(x, y);
                return;
            }

            if (currentPalette == ActivePaletteItem.PortalPair)
            {
                GetPrefabFootprint("portal_pair", out int portalWidth, out int portalHeight);
                PlacePortalCell(x, y, currentRotation, portalWidth, portalHeight);
                return;
            }

            string targetPrefabId = "";
            switch (currentPalette)
            {
                case ActivePaletteItem.SodaCan: targetPrefabId = "soda_can"; break;
                case ActivePaletteItem.Target: targetPrefabId = "target"; break;
                case ActivePaletteItem.TuongCube: targetPrefabId = "wall"; break;
                case ActivePaletteItem.BounceWall: targetPrefabId = "bounce_wall"; break;
                case ActivePaletteItem.Magnet: targetPrefabId = "magnet"; break;
                case ActivePaletteItem.OtherCatalogItem: targetPrefabId = customPrefabId; break;
            }

            if (string.IsNullOrEmpty(targetPrefabId)) return;

            GetPrefabFootprint(targetPrefabId, out int footprintWidth, out int footprintHeight);
            LevelGridPlacement placement = new LevelGridPlacement
            {
                cellX = x,
                cellY = y,
                footprintWidth = footprintWidth,
                footprintHeight = footprintHeight,
                rotationDegrees = currentRotation
            };

            int replacedAnchorX = -1;
            int replacedAnchorY = -1;
            TryGetObjectAnchorAtCell(x, y, out replacedAnchorX, out replacedAnchorY);

            // Tường chỉ chiếm ô trống hoặc thay một tường khác; không xóa asset đang đặt tại ô này.
            if (targetPrefabId == "wall" && replacedAnchorX >= 0 &&
                gridCells[replacedAnchorX, replacedAnchorY].prefabId != "wall")
            {
                return;
            }

            if (!IsPlacementAreaAvailable(placement, replacedAnchorX, replacedAnchorY))
                return;

            if (replacedAnchorX >= 0)
                ClearCell(x, y);

            GridCell cell = gridCells[x, y];
            cell.isOccupied = true;
            cell.prefabId = targetPrefabId;
            cell.rotationDegrees = currentRotation;
            cell.footprintWidth = footprintWidth;
            cell.footprintHeight = footprintHeight;
        }

        private void GetPrefabFootprint(string prefabId, out int width, out int height)
        {
            TryGetPrefabFootprint(prefabId, out width, out height);
        }

        private bool TryGetPrefabFootprint(string prefabId, out int width, out int height)
        {
            width = 1;
            height = 1;
            if (catalog == null) LoadCatalog();
            if (catalog == null || !catalog.TryGetPrefab(prefabId, out GameObject prefab) || prefab == null)
                return false;

            return TryReadPrefabFootprint(prefab, out width, out height);
        }

        private static bool TryReadPrefabFootprint(GameObject prefab, out int width, out int height)
        {
            width = 1;
            height = 1;
            if (prefab == null)
                return false;

            LevelPrefabFootprint configuration = prefab.GetComponent<LevelPrefabFootprint>();
            if (configuration == null)
                configuration = prefab.GetComponentInChildren<LevelPrefabFootprint>(true);
            if (configuration != null)
            {
                width = configuration.WidthInCells;
                height = configuration.HeightInCells;
                return true;
            }

            // Read existing marker footprints for prefabs configured before LevelPrefabFootprint was added.
            LevelObjectMarker marker = prefab.GetComponent<LevelObjectMarker>();
            if (marker == null)
                marker = prefab.GetComponentInChildren<LevelObjectMarker>(true);
            if (marker == null)
                return false;

            width = marker.FootprintWidth;
            height = marker.FootprintHeight;
            return true;
        }

        private void LoadCannonFootprint()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CannonPrefabPath);
            if (!TryReadPrefabFootprint(prefab, out int width, out int height))
                return;

            cannonFootprintWidth = Mathf.Clamp(width, 1, GridColumns);
            cannonFootprintHeight = Mathf.Clamp(height, 1, GridRows);
        }

        private Vector2Int GetDefaultCannonPosition()
        {
            int width = Mathf.Clamp(cannonFootprintWidth, 1, GridColumns);
            int height = Mathf.Clamp(cannonFootprintHeight, 1, GridRows);
            return new Vector2Int((GridColumns - width) / 2, GridRows - height);
        }

        private LevelGridPlacement GetCannonPlacement()
        {
            return new LevelGridPlacement
            {
                cellX = cannonPos.x,
                cellY = cannonPos.y,
                footprintWidth = cannonFootprintWidth,
                footprintHeight = cannonFootprintHeight,
                rotationDegrees = cannonRotation
            };
        }

        private static bool IsCellInsidePlacement(int x, int y, LevelGridPlacement placement)
        {
            if (placement == null)
                return false;

            LevelGridUtility.GetFootprint(placement, out int width, out int height);
            return x >= placement.cellX && x < placement.cellX + width &&
                   y >= placement.cellY && y < placement.cellY + height;
        }

        private static LevelGridPlacement ResizePlacementPreservingCenter(
            LevelGridData grid,
            LevelGridPlacement source,
            int width,
            int height)
        {
            if (source == null)
                return null;

            LevelGridUtility.GetFootprint(source, out int oldWidth, out int oldHeight);
            LevelGridPlacement resized = new LevelGridPlacement
            {
                footprintWidth = Mathf.Max(1, width),
                footprintHeight = Mathf.Max(1, height),
                rotationDegrees = source.rotationDegrees
            };
            LevelGridUtility.GetFootprint(resized, out int newWidth, out int newHeight);
            resized.cellX = Mathf.RoundToInt(source.cellX + (oldWidth - newWidth) * 0.5f);
            resized.cellY = Mathf.RoundToInt(source.cellY + (oldHeight - newHeight) * 0.5f);
            resized.cellX = Mathf.Clamp(resized.cellX, 0, Mathf.Max(0, grid.columns - newWidth));
            resized.cellY = Mathf.Clamp(resized.cellY, 0, Mathf.Max(0, grid.rows - newHeight));
            return resized;
        }

        private static LevelGridPlacement MapPlacementToConfiguredFootprint(
            LevelGridData sourceGrid,
            LevelGridData targetGrid,
            LevelGridPlacement sourcePlacement,
            int width,
            int height)
        {
            if (sourcePlacement == null)
                return null;

            LevelGridUtility.GetFootprint(sourcePlacement, out int sourceWidth, out int sourceHeight);
            float scaleX = targetGrid.columns / (float)Mathf.Max(1, sourceGrid.columns);
            float scaleY = targetGrid.rows / (float)Mathf.Max(1, sourceGrid.rows);
            LevelGridPlacement mapped = new LevelGridPlacement
            {
                footprintWidth = Mathf.Max(1, width),
                footprintHeight = Mathf.Max(1, height),
                rotationDegrees = sourcePlacement.rotationDegrees
            };
            LevelGridUtility.GetFootprint(mapped, out int targetWidth, out int targetHeight);
            mapped.cellX = Mathf.RoundToInt((sourcePlacement.cellX + sourceWidth * 0.5f) * scaleX - targetWidth * 0.5f);
            mapped.cellY = Mathf.RoundToInt((sourcePlacement.cellY + sourceHeight * 0.5f) * scaleY - targetHeight * 0.5f);
            mapped.cellX = Mathf.Clamp(mapped.cellX, 0, Mathf.Max(0, targetGrid.columns - targetWidth));
            mapped.cellY = Mathf.Clamp(mapped.cellY, 0, Mathf.Max(0, targetGrid.rows - targetHeight));
            return mapped;
        }

        private bool TryGetObjectAnchorAtCell(int x, int y, out int anchorX, out int anchorY)
        {
            for (int candidateX = 0; candidateX < GridColumns; candidateX++)
            {
                for (int candidateY = 0; candidateY < GridRows; candidateY++)
                {
                    GridCell candidate = gridCells[candidateX, candidateY];
                    if (!candidate.isOccupied)
                        continue;

                    LevelGridPlacement placement = new LevelGridPlacement
                    {
                        cellX = candidateX,
                        cellY = candidateY,
                        footprintWidth = Mathf.Max(1, candidate.footprintWidth),
                        footprintHeight = Mathf.Max(1, candidate.footprintHeight),
                        rotationDegrees = candidate.rotationDegrees
                    };
                    LevelGridUtility.GetFootprint(placement, out int width, out int height);
                    if (x >= candidateX && x < candidateX + width &&
                        y >= candidateY && y < candidateY + height)
                    {
                        anchorX = candidateX;
                        anchorY = candidateY;
                        return true;
                    }
                }
            }

            anchorX = -1;
            anchorY = -1;
            return false;
        }

        private void RebuildOccupiedCellMap()
        {
            for (int x = 0; x < GridColumns; x++)
            {
                for (int y = 0; y < GridRows; y++)
                {
                    occupiedAnchorX[x, y] = -1;
                    occupiedAnchorY[x, y] = -1;
                }
            }

            for (int anchorX = 0; anchorX < GridColumns; anchorX++)
            {
                for (int anchorY = 0; anchorY < GridRows; anchorY++)
                {
                    GridCell cell = gridCells[anchorX, anchorY];
                    if (!cell.isOccupied)
                        continue;

                    LevelGridPlacement placement = new LevelGridPlacement
                    {
                        cellX = anchorX,
                        cellY = anchorY,
                        footprintWidth = Mathf.Max(1, cell.footprintWidth),
                        footprintHeight = Mathf.Max(1, cell.footprintHeight),
                        rotationDegrees = cell.rotationDegrees
                    };
                    LevelGridUtility.GetFootprint(placement, out int width, out int height);

                    for (int x = anchorX; x < Mathf.Min(GridColumns, anchorX + width); x++)
                    {
                        for (int y = anchorY; y < Mathf.Min(GridRows, anchorY + height); y++)
                        {
                            occupiedAnchorX[x, y] = anchorX;
                            occupiedAnchorY[x, y] = anchorY;
                        }
                    }
                }
            }
        }

        private bool IsPlacementAreaAvailable(
            LevelGridPlacement placement,
            int ignoredAnchorX,
            int ignoredAnchorY,
            bool includeCannon = true)
        {
            LevelGridData grid = CreateGridData();
            if (!LevelGridUtility.IsValidPlacement(grid, placement))
                return false;

            if (includeCannon && Overlaps(placement, GetCannonPlacement()))
                return false;

            LevelGridUtility.GetFootprint(placement, out int width, out int height);
            for (int x = placement.cellX; x < placement.cellX + width; x++)
            {
                for (int y = placement.cellY; y < placement.cellY + height; y++)
                {
                    if (TryGetObjectAnchorAtCell(x, y, out int occupiedAnchorX, out int occupiedAnchorY) &&
                        (occupiedAnchorX != ignoredAnchorX || occupiedAnchorY != ignoredAnchorY))
                        return false;
                }
            }

            return true;
        }

        private static bool Overlaps(LevelGridPlacement first, LevelGridPlacement second)
        {
            if (first == null || second == null)
                return false;

            LevelGridUtility.GetFootprint(first, out int firstWidth, out int firstHeight);
            LevelGridUtility.GetFootprint(second, out int secondWidth, out int secondHeight);
            return first.cellX < second.cellX + secondWidth && second.cellX < first.cellX + firstWidth &&
                   first.cellY < second.cellY + secondHeight && second.cellY < first.cellY + firstHeight;
        }

        private void SelectPlacedObjectForRotation(int x, int y)
        {
            if (IsCellInsidePlacement(x, y, GetCannonPlacement()))
            {
                rotationSelectionKind = RotationSelectionKind.Cannon;
                return;
            }

            if (!TryGetObjectAnchorAtCell(x, y, out int anchorX, out int anchorY))
            {
                ClearRotationSelection();
                return;
            }

            rotationSelectionKind = RotationSelectionKind.PlacedObject;
            selectedObjectAnchor = new Vector2Int(anchorX, anchorY);
        }

        private bool TryGetSelectedRotation(out float rotation, out string label)
        {
            rotation = 0f;
            label = "";

            if (rotationSelectionKind == RotationSelectionKind.Cannon)
            {
                rotation = LevelGridUtility.NormalizeRotation(cannonRotation);
                label = "Cannon";
                return true;
            }

            if (rotationSelectionKind != RotationSelectionKind.PlacedObject ||
                selectedObjectAnchor.x < 0 || selectedObjectAnchor.x >= GridColumns ||
                selectedObjectAnchor.y < 0 || selectedObjectAnchor.y >= GridRows)
            {
                ClearRotationSelection();
                return false;
            }

            GridCell cell = gridCells[selectedObjectAnchor.x, selectedObjectAnchor.y];
            if (!cell.isOccupied)
            {
                ClearRotationSelection();
                return false;
            }

            rotation = LevelGridUtility.NormalizeRotation(cell.rotationDegrees);
            label = $"{cell.prefabId} at ({selectedObjectAnchor.x}, {selectedObjectAnchor.y})";
            return true;
        }

        private void ApplyRotationToSelection(float degrees)
        {
            float normalizedRotation = LevelGridUtility.NormalizeRotation(degrees);
            if (rotationSelectionKind == RotationSelectionKind.Cannon)
            {
                cannonRotation = normalizedRotation;
            }
            else if (rotationSelectionKind == RotationSelectionKind.PlacedObject &&
                     selectedObjectAnchor.x >= 0 && selectedObjectAnchor.x < GridColumns &&
                     selectedObjectAnchor.y >= 0 && selectedObjectAnchor.y < GridRows &&
                     gridCells[selectedObjectAnchor.x, selectedObjectAnchor.y].isOccupied)
            {
                gridCells[selectedObjectAnchor.x, selectedObjectAnchor.y].rotationDegrees = normalizedRotation;
            }

            Repaint();
            if (syncToScene)
                SyncGridToScene();
        }

        private void ClearRotationSelection()
        {
            rotationSelectionKind = RotationSelectionKind.None;
            selectedObjectAnchor = new Vector2Int(-1, -1);
        }

        private void ClearCell(int x, int y)
        {
            if (IsCellInsidePlacement(x, y, GetCannonPlacement()))
            {
                cannonPos = GetDefaultCannonPosition();
                cannonRotation = 0f;
                if (rotationSelectionKind == RotationSelectionKind.Cannon)
                    ClearRotationSelection();
                return;
            }
            if (TryGetObjectAnchorAtCell(x, y, out int anchorX, out int anchorY))
            {
                if (rotationSelectionKind == RotationSelectionKind.PlacedObject &&
                    selectedObjectAnchor.x == anchorX && selectedObjectAnchor.y == anchorY)
                    ClearRotationSelection();
                x = anchorX;
                y = anchorY;
            }

            GridCell cell = gridCells[x, y];
            if (cell.isOccupied && cell.prefabId == "portal_pair" && cell.portalPairId >= 0)
            {
                int pairId = cell.portalPairId;
                for (int otherX = 0; otherX < GridColumns; otherX++)
                {
                    for (int otherY = 0; otherY < GridRows; otherY++)
                    {
                        GridCell other = gridCells[otherX, otherY];
                        if (other.prefabId == "portal_pair" && other.portalPairId == pairId)
                            ResetCell(other);
                    }
                }
            }
            else
            {
                ResetCell(cell);
            }
        }

        private static void ResetCell(GridCell cell)
        {
            cell.isOccupied = false;
            cell.prefabId = "";
            cell.portalPairId = -1;
            cell.portalIsExit = false;
            cell.rotationDegrees = 0f;
            cell.footprintWidth = 1;
            cell.footprintHeight = 1;
        }

        private void PlacePortalCell(int x, int y, float rotationDegrees, int footprintWidth = 1, int footprintHeight = 1)
        {
            GridCell pending = null;
            int pendingX = -1;
            int pendingY = -1;
            int nextId = 0;
            for (int column = 0; column < GridColumns; column++)
            {
                for (int row = 0; row < GridRows; row++)
                {
                    GridCell candidate = gridCells[column, row];
                    if (!candidate.isOccupied || candidate.prefabId != "portal_pair")
                        continue;
                    nextId = Mathf.Max(nextId, candidate.portalPairId + 1);
                    if (candidate.portalIsExit)
                        continue;
                    if (FindPortalExit(candidate.portalPairId) == null)
                    {
                        pending = candidate;
                        pendingX = column;
                        pendingY = row;
                    }
                }
            }

            if (pending != null && TryGetObjectAnchorAtCell(x, y, out int clickedAnchorX, out int clickedAnchorY) &&
                clickedAnchorX == pendingX && clickedAnchorY == pendingY)
                return;

            int replacedAnchorX = -1;
            int replacedAnchorY = -1;
            TryGetObjectAnchorAtCell(x, y, out replacedAnchorX, out replacedAnchorY);
            LevelGridPlacement placement = new LevelGridPlacement
            {
                cellX = x,
                cellY = y,
                footprintWidth = Mathf.Max(1, footprintWidth),
                footprintHeight = Mathf.Max(1, footprintHeight),
                rotationDegrees = rotationDegrees
            };
            if (!IsPlacementAreaAvailable(placement, replacedAnchorX, replacedAnchorY))
                return;

            ClearCell(x, y);
            GridCell cell = gridCells[x, y];
            cell.isOccupied = true;
            cell.prefabId = "portal_pair";
            cell.rotationDegrees = rotationDegrees;
            cell.footprintWidth = footprintWidth;
            cell.footprintHeight = footprintHeight;
            cell.portalPairId = pending != null ? pending.portalPairId : nextId;
            cell.portalIsExit = pending != null;
        }

        private GridCell FindPortalExit(int pairId)
        {
            for (int x = 0; x < GridColumns; x++)
                for (int y = 0; y < GridRows; y++)
                    if (gridCells[x, y].isOccupied && gridCells[x, y].portalIsExit &&
                        gridCells[x, y].portalPairId == pairId)
                        return gridCells[x, y];
            return null;
        }

        private bool TryGetPendingPortalEntry(out int entryX, out int entryY)
        {
            for (int x = 0; x < GridColumns; x++)
            {
                for (int y = 0; y < GridRows; y++)
                {
                    GridCell cell = gridCells[x, y];
                    if (cell.isOccupied && cell.prefabId == "portal_pair" && !cell.portalIsExit &&
                        FindPortalExit(cell.portalPairId) == null)
                    {
                        entryX = x;
                        entryY = y;
                        return true;
                    }
                }
            }
            entryX = -1;
            entryY = -1;
            return false;
        }

        private LevelGridPlacement GetPortalExitPlacement(int pairId)
        {
            for (int x = 0; x < GridColumns; x++)
            {
                for (int y = 0; y < GridRows; y++)
                {
                    GridCell cell = gridCells[x, y];
                    if (cell.isOccupied && cell.portalIsExit && cell.portalPairId == pairId)
                    {
                        return new LevelGridPlacement
                        {
                            cellX = x,
                            cellY = y,
                            footprintWidth = Mathf.Max(1, cell.footprintWidth),
                            footprintHeight = Mathf.Max(1, cell.footprintHeight),
                            rotationDegrees = cell.rotationDegrees
                        };
                    }
                }
            }
            return null;
        }

        private void ClearAllCells(bool updateScene = true)
        {
            for (int x = 0; x < GridColumns; x++)
            {
                for (int y = 0; y < GridRows; y++)
                {
                    ResetCell(gridCells[x, y]);
                }
            }
            cannonPos = GetDefaultCannonPosition();
            cannonRotation = 0f;
            ClearRotationSelection();
            Repaint();
            if (updateScene && syncToScene) SyncGridToScene();
        }

        private Color GetColorForPrefabId(string id)
        {
            if (string.IsNullOrEmpty(id)) return Color.gray;
            switch (id.ToLowerInvariant())
            {
                case "soda_can": return new Color(0.92f, 0.26f, 0.24f, 1f); // Đỏ lon
                case "target": return new Color(0.95f, 0.48f, 0.15f, 1f);   // Cam bia
                case "wall": return new Color(0.42f, 0.62f, 0.68f, 1f); // Tường Cube nảy mức 1
                case "bounce_wall": return new Color(0.2f, 0.58f, 0.95f, 1f); // Xanh tường nảy
                case "portal_pair": return new Color(0.68f, 0.28f, 0.95f, 1f); // Tím portal
                case "magnet": return new Color(0.12f, 0.78f, 0.72f, 1f);      // Xanh magnet
                default: return new Color(0.4f, 0.7f, 0.4f, 1f);
            }
        }

        private static Vector3 LayTiLeTuongTheoO(LevelGridData grid)
        {
            if (!LevelGridUtility.IsValidGrid(grid))
                return Vector3.one;

            // Prefab gốc có cạnh 0.3875 đơn vị; co giãn theo ô lưới để cạnh các khối luôn khít.
            return Vector3.one * (grid.worldUnitsPerCell / DefaultCellUnits);
        }

        private string GetShortLabelForPrefabId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            switch (id.ToLowerInvariant())
            {
                case "soda_can": return "🥤\nCAN";
                case "target": return "🎯\nTGT";
                case "wall": return "■\nNẢY 1";
                case "bounce_wall": return "↗\nNẢY";
                case "portal_pair": return "🌀\nPORT";
                case "magnet": return "🧲\nMAG";
                default: return id.Length > 4 ? id.Substring(0, 4).ToUpperInvariant() : id.ToUpperInvariant();
            }
        }

        private string GetCompactLabelForPrefabId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            switch (id.ToLowerInvariant())
            {
                case "soda_can": return "S";
                case "target": return "T";
                case "wall": return "■";
                case "bounce_wall": return "W";
                case "portal_pair": return "P";
                case "magnet": return "M";
                default: return id.Substring(0, 1).ToUpperInvariant();
            }
        }

        private string GetArrowForRotation(float rot)
        {
            int quarter = Mathf.RoundToInt(rot / 90f) % 4;
            if (quarter < 0) quarter += 4;
            switch (quarter)
            {
                case 1: return "▶";
                case 2: return "▼";
                case 3: return "◀";
                default: return "▲";
            }
        }

        public void LoadLevel(string levelId, bool notify)
        {
            if (string.IsNullOrWhiteSpace(levelId)) return;

            if (!IsValidLevelId(levelId))
            {
                if (notify) EditorUtility.DisplayDialog("Invalid Level ID", "Use letters, numbers, underscores, and hyphens only.", "OK");
                return;
            }

            string assetPath = GetLevelAssetPath(levelId);
            LevelDefinition definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(assetPath);
            string filePath = Path.Combine(GetLevelsDirectory(), $"{levelId}.json").Replace('\\', '/');
            LevelDocument doc = definition != null ? definition.Data : null;
            if (doc == null && !File.Exists(filePath))
            {
                if (notify) EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy file: {filePath}", "OK");
                return;
            }

            if (doc == null)
                doc = JsonUtility.FromJson<LevelDocument>(File.ReadAllText(filePath));
            if (doc == null) return;

            creatingNewLevel = false;
            rotatePlacedObjectsMode = false;
            currentLevelId = doc.id;
            currentDisplayName = doc.displayName;
            soDanBatDau = Mathf.Max(0, doc.startingBulletCount);

            ClearAllCells(false);

            LevelGridData sourceGrid = doc.grid ?? new LevelGridData
            {
                columns = 9,
                rows = 16,
                worldUnitsPerCell = 0.775f
            };
            if (sourceGrid.columns <= 0 || sourceGrid.rows <= 0)
            {
                sourceGrid.columns = 9;
                sourceGrid.rows = 16;
            }
            LevelGridData grid = CreateGridData();

            // Nạp vị trí cannon
            if (doc.cannonPlacement != null)
            {
                LevelGridPlacement mappedCannon = MapPlacementToConfiguredFootprint(
                    sourceGrid,
                    grid,
                    doc.cannonPlacement,
                    cannonFootprintWidth,
                    cannonFootprintHeight);
                if (mappedCannon != null)
                {
                    cannonPos = new Vector2Int(mappedCannon.cellX, mappedCannon.cellY);
                    cannonRotation = mappedCannon.rotationDegrees;
                }
            }

            // Nạp các object
            if (doc.objects != null)
            {
                for (int i = 0; i < doc.objects.Length; i++)
                {
                    LevelObjectData obj = doc.objects[i];
                    int cx, cy;
                    float rot = 0f;
                    bool hasConfiguredFootprint = TryGetPrefabFootprint(
                        obj.prefabId,
                        out int configuredWidth,
                        out int configuredHeight);
                    LevelGridPlacement mappedPlacement = obj.gridPlacement != null
                        ? MapPlacementToGrid(sourceGrid, grid, obj.gridPlacement)
                        : null;
                    if (mappedPlacement != null && hasConfiguredFootprint)
                    {
                        mappedPlacement = ResizePlacementPreservingCenter(
                            grid,
                            mappedPlacement,
                            configuredWidth,
                            configuredHeight);
                    }

                    if (mappedPlacement != null)
                    {
                        cx = mappedPlacement.cellX;
                        cy = mappedPlacement.cellY;
                        rot = mappedPlacement.rotationDegrees;
                    }
                    else
                    {
                        LevelGridUtility.TryGetCell(
                            grid,
                            obj.localPosition,
                            hasConfiguredFootprint ? configuredWidth : 1,
                            hasConfiguredFootprint ? configuredHeight : 1,
                            obj.localEulerAngles.z,
                            out cx,
                            out cy);
                        rot = obj.localEulerAngles.z;
                    }

                    if (cx >= 0 && cx < GridColumns && cy >= 0 && cy < GridRows)
                    {
                        if (obj.prefabId == "portal_pair")
                        {
                            PlacePortalCell(cx, cy, rot,
                                mappedPlacement != null ? mappedPlacement.footprintWidth : 1,
                                mappedPlacement != null ? mappedPlacement.footprintHeight : 1);
                            LevelGridPlacement mappedExit = obj.portalExitPlacement != null
                                ? MapPlacementToGrid(sourceGrid, grid, obj.portalExitPlacement)
                                : null;
                            if (mappedExit != null && hasConfiguredFootprint)
                            {
                                mappedExit = ResizePlacementPreservingCenter(
                                    grid,
                                    mappedExit,
                                    configuredWidth,
                                    configuredHeight);
                            }
                            if (mappedExit != null && LevelGridUtility.IsValidPlacement(grid, mappedExit))
                            {
                                PlacePortalCell(mappedExit.cellX, mappedExit.cellY, mappedExit.rotationDegrees,
                                    mappedExit.footprintWidth, mappedExit.footprintHeight);
                            }
                            continue;
                        }
                        gridCells[cx, cy].isOccupied = true;
                        gridCells[cx, cy].prefabId = obj.prefabId;
                        gridCells[cx, cy].rotationDegrees = rot;
                        gridCells[cx, cy].footprintWidth = mappedPlacement != null
                            ? mappedPlacement.footprintWidth
                            : 1;
                        gridCells[cx, cy].footprintHeight = mappedPlacement != null
                            ? mappedPlacement.footprintHeight
                            : 1;
                    }
                }
            }

            Repaint();
            if (syncToScene) SyncGridToScene();

            if (notify)
            {
                Debug.Log($"[LevelEditor] Đã tải level '{currentLevelId}' lên bảng lưới!");
            }
        }

        private static LevelGridPlacement MapPlacementToGrid(
            LevelGridData sourceGrid,
            LevelGridData targetGrid,
            LevelGridPlacement sourcePlacement)
        {
            if (sourcePlacement == null)
                return null;

            float scaleX = targetGrid.columns / (float)Mathf.Max(1, sourceGrid.columns);
            float scaleY = targetGrid.rows / (float)Mathf.Max(1, sourceGrid.rows);
            LevelGridUtility.GetFootprint(sourcePlacement, out int sourceWidth, out int sourceHeight);
            LevelGridPlacement mapped = new LevelGridPlacement
            {
                footprintWidth = Mathf.Max(1, Mathf.RoundToInt(sourcePlacement.footprintWidth * scaleX)),
                footprintHeight = Mathf.Max(1, Mathf.RoundToInt(sourcePlacement.footprintHeight * scaleY)),
                rotationDegrees = sourcePlacement.rotationDegrees
            };
            LevelGridUtility.GetFootprint(mapped, out int targetWidth, out int targetHeight);
            mapped.cellX = Mathf.RoundToInt((sourcePlacement.cellX + sourceWidth * 0.5f) * scaleX - targetWidth * 0.5f);
            mapped.cellY = Mathf.RoundToInt((sourcePlacement.cellY + sourceHeight * 0.5f) * scaleY - targetHeight * 0.5f);

            mapped.cellX = Mathf.Clamp(mapped.cellX, 0, Mathf.Max(0, targetGrid.columns - targetWidth));
            mapped.cellY = Mathf.Clamp(mapped.cellY, 0, Mathf.Max(0, targetGrid.rows - targetHeight));
            return mapped;
        }

        public void SaveLevel()
        {
            if (string.IsNullOrWhiteSpace(currentLevelId))
            {
                EditorUtility.DisplayDialog("Lỗi", "Level ID không được để trống!", "OK");
                return;
            }

            if (!IsValidLevelId(currentLevelId))
            {
                EditorUtility.DisplayDialog("Invalid Level ID", "Use letters, numbers, underscores, and hyphens only.", "OK");
                return;
            }

            if (creatingNewLevel && IsLevelIdInUse(currentLevelId))
            {
                EditorUtility.DisplayDialog("Level ID already exists",
                    $"The ID '{currentLevelId}' is already in use. Choose another ID before saving this new level.", "OK");
                return;
            }

            if (catalog == null) LoadCatalog();
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("Missing prefab catalog", $"Could not load the catalog at {DefaultCatalogPath}.", "OK");
                return;
            }

            LevelGridData grid = CreateGridData();

            List<LevelObjectData> objectsList = new List<LevelObjectData>();

            for (int x = 0; x < GridColumns; x++)
            {
                for (int y = 0; y < GridRows; y++)
                {
                    GridCell cell = gridCells[x, y];
                    if (!cell.isOccupied || string.IsNullOrEmpty(cell.prefabId)) continue;
                    if (cell.prefabId == "portal_pair" && cell.portalIsExit) continue;

                    if (!catalog.TryGetPrefab(cell.prefabId, out GameObject prefab) || prefab == null)
                    {
                        EditorUtility.DisplayDialog("Missing prefab", $"Prefab ID '{cell.prefabId}' is missing from the level catalog.", "OK");
                        return;
                    }

                    LevelGridPlacement placement = new LevelGridPlacement
                    {
                        cellX = x,
                        cellY = y,
                        footprintWidth = Mathf.Max(1, cell.footprintWidth),
                        footprintHeight = Mathf.Max(1, cell.footprintHeight),
                        rotationDegrees = cell.rotationDegrees
                    };

                    LevelObjectData data = new LevelObjectData
                    {
                        prefabId = cell.prefabId,
                        instanceName = $"{cell.prefabId}_{x}_{y}",
                        localPosition = LevelGridUtility.GetLocalPosition(grid, placement),
                        localEulerAngles = new Vector3(0f, 0f, cell.rotationDegrees),
                        localScale = cell.prefabId == "wall" ? LayTiLeTuongTheoO(grid) : Vector3.one,
                        gridPlacement = placement
                    };

                    if (cell.prefabId == "portal_pair")
                    {
                        data.portalExitPlacement = GetPortalExitPlacement(cell.portalPairId);
                        if (data.portalExitPlacement == null)
                        {
                            EditorUtility.DisplayDialog("Portal pair incomplete",
                                $"Place an Exit for the Entry at ({x}, {y}) before saving.", "OK");
                            return;
                        }
                    }

                    objectsList.Add(data);
                }
            }

            LevelDocument doc = new LevelDocument
            {
                schemaVersion = 1,
                id = currentLevelId,
                displayName = currentDisplayName,
                startingBulletCount = soDanBatDau,
                grid = grid,
                cannonPlacement = GetCannonPlacement(),
                objects = objectsList.ToArray()
            };

            string filePath = PersistLevelDocument(doc);
            creatingNewLevel = false;

            EditorUtility.DisplayDialog("Thành công", $"Đã lưu level '{currentLevelId}' với {objectsList.Count} vật thể!\nĐường dẫn: {filePath}", "OK");
        }

        private void CreateNewLevel()
        {
            RefreshManifest();

            int number = 1;
            string newLevelId;
            do
            {
                newLevelId = $"level_{number:D2}";
                number++;
            }
            while (IsLevelIdInUse(newLevelId));

            currentLevelId = newLevelId;
            currentDisplayName = $"Level {number - 1:D2}";
            soDanBatDau = 0;
            selectedManifestIndex = manifestLevelIds.Count > 0
                ? Mathf.Clamp(selectedManifestIndex, 0, manifestLevelIds.Count - 1)
                : 0;
            creatingNewLevel = true;
            rotatePlacedObjectsMode = false;
            currentPalette = ActivePaletteItem.SodaCan;
            customPrefabId = "";
            currentRotation = 0f;
            ClearAllCells(false);

            if (syncToScene)
                SyncGridToScene();
            else
                ClearScenePreview();

            PersistLevelDocument(new LevelDocument
            {
                schemaVersion = 1,
                id = currentLevelId,
                displayName = currentDisplayName,
                startingBulletCount = soDanBatDau,
                grid = CreateGridData(),
                cannonPlacement = GetCannonPlacement(),
                objects = new LevelObjectData[0]
            });
            creatingNewLevel = false;
            Repaint();
        }

        private string PersistLevelDocument(LevelDocument doc)
        {
            SaveLevelDefinitionAsset(doc);

            string dir = GetLevelsDirectory();
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, $"{doc.id}.json").Replace('\\', '/');
            File.WriteAllText(filePath, JsonUtility.ToJson(doc, true));

            UpdateManifestWithLevelId(doc.id);
            AssetDatabase.Refresh();
            GameManager[] managers = FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < managers.Length; i++)
            {
                managers[i].RefreshLevelDefinitionsFromAssets();
            }

            currentLevelId = doc.id;
            RefreshManifest();
            return filePath;
        }

        private bool IsLevelIdInUse(string levelId)
        {
            if (manifestLevelIds.Exists(id =>
                    string.Equals(id, levelId, StringComparison.OrdinalIgnoreCase)))
                return true;

            string assetPath = GetLevelAssetPath(levelId);
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                return true;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absoluteAssetPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(absoluteAssetPath))
                return true;

            string jsonPath = Path.Combine(GetLevelsDirectory(), $"{levelId}.json");
            if (File.Exists(jsonPath))
                return true;

            if (!AssetDatabase.IsValidFolder(LevelAssetsDirectory))
                return false;

            string[] definitionGuids = AssetDatabase.FindAssets("t:LevelDefinition", new[] { LevelAssetsDirectory });
            for (int i = 0; i < definitionGuids.Length; i++)
            {
                string definitionPath = AssetDatabase.GUIDToAssetPath(definitionGuids[i]);
                LevelDefinition definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(definitionPath);
                if (definition != null && definition.Data != null &&
                    string.Equals(definition.Data.id, levelId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void SaveLevelDefinitionAsset(LevelDocument doc)
        {
            EnsureLevelAssetsDirectory();
            string assetPath = GetLevelAssetPath(doc.id);
            LevelDefinition definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<LevelDefinition>();
                definition.name = $"Level_{doc.id}";
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            definition.SetData(doc);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelEditor] Saved LevelDefinition asset: {assetPath}");
        }

        private static void EnsureLevelAssetsDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Project/Data"))
                AssetDatabase.CreateFolder("Assets/Project", "Data");

            if (!AssetDatabase.IsValidFolder(LevelAssetsDirectory))
                AssetDatabase.CreateFolder("Assets/Project/Data", "Levels");
        }

        private static string GetLevelAssetPath(string levelId)
        {
            return $"{LevelAssetsDirectory}/{levelId}.asset";
        }

        private static bool IsValidLevelId(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId)) return false;
            for (int i = 0; i < levelId.Length; i++)
            {
                char character = levelId[i];
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                    return false;
            }

            return true;
        }

        private void UpdateManifestWithLevelId(string levelId)
        {
            string manifestPath = GetManifestPath();
            LevelManifest manifest = new LevelManifest();

            if (File.Exists(manifestPath))
            {
                try
                {
                    manifest = JsonUtility.FromJson<LevelManifest>(File.ReadAllText(manifestPath)) ?? new LevelManifest();
                }
                catch { }
            }

            List<string> ids = new List<string>(manifest.levelIds ?? new string[0]);
            if (!ids.Exists(id => string.Equals(id, levelId, StringComparison.OrdinalIgnoreCase)))
            {
                ids.Add(levelId);
                manifest.levelIds = ids.ToArray();
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            }
        }

        private void SyncGridToScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            GameObject stagingRoot = null;
            try
            {
                SyncGridToSceneInternal(ref stagingRoot);
            }
            catch (Exception ex)
            {
                if (stagingRoot != null)
                    Undo.DestroyObjectImmediate(stagingRoot);
                Debug.LogError($"[LevelEditor] Không thể đồng bộ level lên Scene: {ex}");
            }
        }

        private void SchedulePreviewSync()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            previewSyncRequestedAt = EditorApplication.timeSinceStartup;
            if (previewSyncScheduled)
                return;

            previewSyncScheduled = true;
            EditorApplication.update += RunScheduledPreviewSync;
        }

        private void RunScheduledPreviewSync()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= RunScheduledPreviewSync;
                previewSyncScheduled = false;
                return;
            }

            if (EditorApplication.timeSinceStartup - previewSyncRequestedAt < 0.12d)
                return;

            EditorApplication.update -= RunScheduledPreviewSync;
            previewSyncScheduled = false;
            if (syncToScene)
                SyncGridToScene();
        }

        private void SyncGridToSceneInternal(ref GameObject stagingRoot)
        {
            if (catalog == null) LoadCatalog();
            if (catalog == null) return;

            stagingRoot = new GameObject($"{RootName} (Building)");
            GameObjectManager manager = FindFirstObjectByType<GameObjectManager>(FindObjectsInactive.Include);
            if (manager != null)
                stagingRoot.transform.SetParent(manager.transform, false);
            else
            {
                stagingRoot.transform.position = Vector3.zero;
                stagingRoot.transform.rotation = Quaternion.identity;
            }

            LevelGridData grid = CreateGridData();

            for (int x = 0; x < GridColumns; x++)
            {
                for (int y = 0; y < GridRows; y++)
                {
                    GridCell cell = gridCells[x, y];
                    if (!cell.isOccupied || string.IsNullOrEmpty(cell.prefabId)) continue;
                    if (cell.prefabId == "portal_pair" && cell.portalIsExit) continue;

                    try
                    {
                        if (!catalog.TryGetPrefab(cell.prefabId, out GameObject prefab) || prefab == null)
                        {
                            Debug.LogError($"[LevelEditor] Không tìm thấy prefab ID '{cell.prefabId}' trong LevelPrefabCatalog.");
                            continue;
                        }

                        GameObject instance = InstantiateScenePreviewPrefab(prefab, stagingRoot.transform);
                        if (instance == null)
                        {
                            Debug.LogError($"[LevelEditor] Không thể tạo preview cho prefab ID '{cell.prefabId}'.");
                            continue;
                        }

                        instance.name = $"{cell.prefabId}_{x}_{y}";
                        LevelGridPlacement placement = new LevelGridPlacement
                        {
                            cellX = x,
                            cellY = y,
                            footprintWidth = Mathf.Max(1, cell.footprintWidth),
                            footprintHeight = Mathf.Max(1, cell.footprintHeight),
                            rotationDegrees = cell.rotationDegrees
                        };

                        instance.transform.localPosition = LevelGridUtility.GetLocalPosition(grid, placement);
                        instance.transform.localRotation = LevelGridUtility.GetGridRotation(grid) *
                                                           Quaternion.Euler(0f, 0f, cell.rotationDegrees) *
                                                           prefab.transform.localRotation;
                        instance.transform.localScale = Vector3.Scale(
                            prefab.transform.localScale,
                            cell.prefabId == "wall" ? LayTiLeTuongTheoO(grid) : Vector3.one);

                        MagnetForceField magnet = instance.GetComponent<MagnetForceField>();
                        if (magnet != null)
                            magnet.AlignVisualToFieldCenter();

                        if (cell.prefabId == "portal_pair")
                        {
                            BulletPortalPair pair = instance.GetComponent<BulletPortalPair>();
                            if (pair == null)
                                throw new InvalidOperationException("Portal prefab has no BulletPortalPair component.");
                            pair.PlaceGridEndpoints(grid, manager != null ? manager.transform : null,
                                placement, GetPortalExitPlacement(cell.portalPairId));
                        }

                        LevelObjectMarker marker = instance.GetComponent<LevelObjectMarker>();
                        if (marker == null) marker = instance.AddComponent<LevelObjectMarker>();
                        marker.Setup(cell.prefabId, x, y,
                            Mathf.Max(1, cell.footprintWidth), Mathf.Max(1, cell.footprintHeight), cell.rotationDegrees);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[LevelEditor] Preview failed for '{cell.prefabId}' at cell ({x}, {y}): {ex}");
                    }
                }
            }

            // Use the scene Cannon when it is active; otherwise preview the Cannon prefab.
            CannonController cannon = FindFirstObjectByType<CannonController>();
            if (cannon == null)
            {
                GameObject cannonAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CannonPrefabPath);
                if (cannonAsset != null)
                {
                    GameObject cannonPreview = InstantiateScenePreviewPrefab(cannonAsset, stagingRoot.transform);
                    cannonPreview.name = "Cannon Preview";
                    cannon = cannonPreview.GetComponentInChildren<CannonController>(true);
                }
            }
            if (cannon != null)
            {
                LevelGridPlacement cannonPlacement = GetCannonPlacement();
                Vector3 cPos = LevelGridUtility.GetLocalPosition(grid, cannonPlacement);
                // Keep the prefab's authored camera depth while snapping only its board X/Y.
                cPos.z = manager != null
                    ? manager.transform.InverseTransformPoint(cannon.transform.position).z
                    : cannon.transform.position.z;
                Vector3 worldPosition = manager != null ? manager.transform.TransformPoint(cPos) : cPos;
                Quaternion localRotation = LevelGridUtility.GetGridRotation(grid) * Quaternion.Euler(0f, 0f, cannonRotation);
                Quaternion worldRotation = manager != null
                    ? manager.transform.rotation * localRotation
                    : localRotation;
                Undo.RecordObject(cannon.transform, "Place Cannon from Level Editor");
                cannon.ApplyLevelPlacement(worldPosition, worldRotation);
                EditorUtility.SetDirty(cannon.transform);
            }

            ClearScenePreview();
            stagingRoot.name = RootName;
            Undo.RegisterCreatedObjectUndo(stagingRoot, "Sync Level Editor to Scene");
            SceneView.RepaintAll();
            stagingRoot = null;
        }

        private static GameObject InstantiateScenePreviewPrefab(GameObject prefab, Transform parent)
        {
            return UnityEngine.Object.Instantiate(prefab, parent, false);
        }

        private void ClearScenePreview()
        {
            GameObject root = GameObject.Find(RootName);
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
                SceneView.RepaintAll();
            }
        }
    }
}
