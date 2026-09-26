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

        private const int GridColumns = 9;
        private const int GridRows = 16;
        private const float DefaultCellUnits = 0.775f;

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
            BounceWall,
            PortalPair,
            Magnet,
            OtherCatalogItem,
            Erase
        }

        [SerializeField] private LevelPrefabCatalog catalog;
        private ActivePaletteItem currentPalette = ActivePaletteItem.SodaCan;
        private string customPrefabId = "";
        private float currentRotation = 0f;
        private bool syncToScene = true;

        // Dữ liệu lưới 9x16
        private GridCell[,] gridCells = new GridCell[GridColumns, GridRows];
        private Vector2Int cannonPos = new Vector2Int(4, 15);
        private float cannonRotation = 0f;

        // Quản lý level
        private List<string> manifestLevelIds = new List<string>();
        private int selectedManifestIndex = 0;
        private string currentLevelId = "level_01";
        private string currentDisplayName = "Soda Can Bowling";
        private bool creatingNewLevel;

        private Vector2 mainScroll;
        private GUIStyle cellStyle;
        private GUIStyle headerStyle;
        private GUIStyle paletteButtonStyle;

        [MenuItem("Tools/DreamForge/Level Editor", false, 10)]
        public static void OpenWindow()
        {
            LevelEditorWindow window = GetWindow<LevelEditorWindow>("Level Editor (9x16)");
            window.minSize = new Vector2(740, 720);
            window.Show();
        }

        private void OnEnable()
        {
            InitializeGrid();
            LoadCatalog();
            RefreshManifest();

            if (!string.IsNullOrEmpty(currentLevelId))
            {
                LoadLevel(currentLevelId, false);
            }
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

            // Cột bên trái: Màn hình điện thoại 9:16 chứa lưới 9x16 ô
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

            EditorGUILayout.LabelField("🎮 BẢNG THIẾT KẾ LEVEL (MÀN DỌC 9x16)", headerStyle, GUILayout.Width(310));

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
            EditorGUILayout.EndVertical();
        }

        private void DrawPhoneBoard()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(340));

            // Khung ngoài điện thoại
            Rect phoneFrame = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.Box(phoneFrame, GUIContent.none);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📱 MÀN HÌNH EDITOR (1080x1920)", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Click/Rê chuột để tô ô", EditorStyles.miniLabel, GUILayout.Width(130));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Bảng lưới 9 cột x 16 dòng
            float cellPixelSize = 34f;
            float boardWidth = GridColumns * cellPixelSize;
            float boardHeight = GridRows * cellPixelSize;

            Rect boardRect = GUILayoutUtility.GetRect(boardWidth, boardHeight, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

            // Vẽ viền đen màn hình
            EditorGUI.DrawRect(new Rect(boardRect.x - 2, boardRect.y - 2, boardWidth + 4, boardHeight + 4), new Color(0.1f, 0.1f, 0.12f, 1f));

            Event e = Event.current;
            bool isMouseHovering = boardRect.Contains(e.mousePosition);

            for (int y = 0; y < GridRows; y++)
            {
                for (int x = 0; x < GridColumns; x++)
                {
                    Rect cellRect = new Rect(boardRect.x + x * cellPixelSize, boardRect.y + y * cellPixelSize, cellPixelSize - 1, cellPixelSize - 1);

                    bool isCannonCell = (cannonPos.x == x && cannonPos.y == y);
                    GridCell cell = gridCells[x, y];

                    // Xác định màu sắc và nội dung hiển thị
                    Color cellColor;
                    string cellLabel;

                    if (isCannonCell)
                    {
                        cellColor = new Color(1f, 0.78f, 0.12f, 1f); // Vàng Cannon
                        cellLabel = GetArrowForRotation(cannonRotation) + "\nCANON";
                    }
                    else if (cell.isOccupied)
                    {
                        cellColor = GetColorForPrefabId(cell.prefabId);
                        cellLabel = cell.prefabId == "portal_pair"
                            ? (cell.portalIsExit ? "EXIT" : "ENTRY")
                            : GetShortLabelForPrefabId(cell.prefabId);
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
                            fontSize = 8,
                            normal = { textColor = (isCannonCell || (cell.isOccupied && cell.prefabId == "magnet")) ? Color.black : Color.white }
                        };
                        GUI.Label(cellRect, cellLabel, labelStyle);
                    }

                    // Xử lý Click hoặc Drag chuột vào ô
                    if (cellRect.Contains(e.mousePosition))
                    {
                        // Highlight hover viền trắng
                        Handles.color = Color.white;
                        Handles.DrawWireCube(cellRect.center, new Vector3(cellPixelSize, cellPixelSize, 0));

                        if ((e.type == EventType.MouseDown ||
                             (e.type == EventType.MouseDrag && (currentPalette != ActivePaletteItem.PortalPair || e.button == 1))) &&
                            (e.button == 0 || e.button == 1))
                        {
                            if (e.button == 1) // Chuột phải: Xóa nhanh
                            {
                                ClearCell(x, y);
                            }
                            else // Chuột trái: Áp dụng Palette hiện tại
                            {
                                ApplyPaletteToCell(x, y);
                            }

                            e.Use();
                            Repaint();

                            if (syncToScene)
                            {
                                SyncGridToScene();
                            }
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
                cannonPos = new Vector2Int(4, 15);
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

            // 4. Ô Bounce Wall (bounce_wall)
            DrawPaletteOption(ActivePaletteItem.BounceWall, "🧱 Tường nảy (Bounce Wall)", new Color(0.2f, 0.58f, 0.95f), "bounce_wall");

            // 5. Ô Portal Pair (portal_pair)
            DrawPaletteOption(ActivePaletteItem.PortalPair, "🌀 Portal: click Entry rồi Exit", new Color(0.68f, 0.28f, 0.95f), "portal_pair");
            if (TryGetPendingPortalEntry(out int pendingX, out int pendingY))
                EditorGUILayout.HelpBox($"Portal Entry: ({pendingX}, {pendingY}). Click another cell for Exit.", MessageType.Info);

            // 6. Ô Magnet (magnet)
            DrawPaletteOption(ActivePaletteItem.Magnet, "🧲 Lực hút Nam châm (Magnet)", new Color(0.12f, 0.78f, 0.72f), "magnet");

            // 7. Ô Tẩy xóa (Erase)
            DrawPaletteOption(ActivePaletteItem.Erase, "✖️ Tẩy / Xóa ô", new Color(0.45f, 0.45f, 0.48f), "");

            // Nếu catalog có prefab khác
            DrawExtraCatalogOptions();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("⚙️ Tùy chỉnh góc xoay vật thể", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Góc xoay: {currentRotation}°", GUILayout.Width(90));
            if (GUILayout.Button("0°")) currentRotation = 0f;
            if (GUILayout.Button("90°")) currentRotation = 90f;
            if (GUILayout.Button("180°")) currentRotation = 180f;
            if (GUILayout.Button("270°")) currentRotation = 270f;
            if (GUILayout.Button("+90°", GUILayout.Width(45))) currentRotation = (currentRotation + 90f) % 360f;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("📊 Thống kê nhanh", EditorStyles.boldLabel);
            int canCount = 0;
            int targetCount = 0;
            int wallCount = 0;
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
                        else if (id == "bounce_wall") wallCount++;
                    }
                }
            }

            EditorGUILayout.LabelField($"• Vị trí Cannon: ({cannonPos.x}, {cannonPos.y}) - Hướng: {cannonRotation}°");
            EditorGUILayout.LabelField($"• Số lon Soda (Win): {canCount}");
            EditorGUILayout.LabelField($"• Số mục tiêu Target: {targetCount}");
            EditorGUILayout.LabelField($"• Số tường bật nảy: {wallCount}");
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
                if (entry.Id == "soda_can" || entry.Id == "target" || entry.Id == "bounce_wall" || entry.Id == "portal_pair" || entry.Id == "magnet")
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
                ClearCell(x, y);
                cannonPos = new Vector2Int(x, y);
                cannonRotation = currentRotation;
                return;
            }

            // Nếu click trùng vị trí cannon bằng tool khác, dời cannon ra chỗ khác
            if (cannonPos.x == x && cannonPos.y == y)
            {
                cannonPos = new Vector2Int(4, 15);
            }

            if (currentPalette == ActivePaletteItem.Erase)
            {
                ClearCell(x, y);
                return;
            }

            if (currentPalette == ActivePaletteItem.PortalPair)
            {
                PlacePortalCell(x, y, currentRotation);
                return;
            }

            string targetPrefabId = "";
            switch (currentPalette)
            {
                case ActivePaletteItem.SodaCan: targetPrefabId = "soda_can"; break;
                case ActivePaletteItem.Target: targetPrefabId = "target"; break;
                case ActivePaletteItem.BounceWall: targetPrefabId = "bounce_wall"; break;
                case ActivePaletteItem.Magnet: targetPrefabId = "magnet"; break;
                case ActivePaletteItem.OtherCatalogItem: targetPrefabId = customPrefabId; break;
            }

            if (string.IsNullOrEmpty(targetPrefabId)) return;

            ClearCell(x, y);
            GridCell cell = gridCells[x, y];
            cell.isOccupied = true;
            cell.prefabId = targetPrefabId;
            cell.rotationDegrees = currentRotation;
            cell.footprintWidth = 1;
            cell.footprintHeight = 1;
        }

        private void ClearCell(int x, int y)
        {
            if (cannonPos.x == x && cannonPos.y == y)
            {
                cannonPos = new Vector2Int(4, 15);
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
        }

        private void PlacePortalCell(int x, int y, float rotationDegrees)
        {
            GridCell pending = null;
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
                        pending = candidate;
                }
            }

            if (gridCells[x, y] == pending)
                return;

            ClearCell(x, y);
            GridCell cell = gridCells[x, y];
            cell.isOccupied = true;
            cell.prefabId = "portal_pair";
            cell.rotationDegrees = rotationDegrees;
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
                            footprintWidth = 1,
                            footprintHeight = 1,
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
            cannonPos = new Vector2Int(4, 15);
            cannonRotation = 0f;
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
                case "bounce_wall": return new Color(0.2f, 0.58f, 0.95f, 1f); // Xanh tường
                case "portal_pair": return new Color(0.68f, 0.28f, 0.95f, 1f); // Tím portal
                case "magnet": return new Color(0.12f, 0.78f, 0.72f, 1f);      // Xanh magnet
                default: return new Color(0.4f, 0.7f, 0.4f, 1f);
            }
        }

        private string GetShortLabelForPrefabId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            switch (id.ToLowerInvariant())
            {
                case "soda_can": return "🥤\nCAN";
                case "target": return "🎯\nTGT";
                case "bounce_wall": return "🧱\nWALL";
                case "portal_pair": return "🌀\nPORT";
                case "magnet": return "🧲\nMAG";
                default: return id.Length > 4 ? id.Substring(0, 4).ToUpperInvariant() : id.ToUpperInvariant();
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
            currentLevelId = doc.id;
            currentDisplayName = doc.displayName;

            ClearAllCells(false);

            LevelGridData grid = doc.grid ?? new LevelGridData();

            // Nạp vị trí cannon
            if (doc.cannonPlacement != null)
            {
                cannonPos = new Vector2Int(
                    Mathf.Clamp(doc.cannonPlacement.cellX, 0, GridColumns - 1),
                    Mathf.Clamp(doc.cannonPlacement.cellY, 0, GridRows - 1)
                );
                cannonRotation = doc.cannonPlacement.rotationDegrees;
            }

            // Nạp các object
            if (doc.objects != null)
            {
                for (int i = 0; i < doc.objects.Length; i++)
                {
                    LevelObjectData obj = doc.objects[i];
                    int cx, cy;
                    float rot = 0f;

                    if (obj.gridPlacement != null)
                    {
                        cx = obj.gridPlacement.cellX;
                        cy = obj.gridPlacement.cellY;
                        rot = obj.gridPlacement.rotationDegrees;
                    }
                    else
                    {
                        LevelGridUtility.TryGetCell(grid, obj.localPosition, 1, 1, obj.localEulerAngles.z, out cx, out cy);
                        rot = obj.localEulerAngles.z;
                    }

                    if (cx >= 0 && cx < GridColumns && cy >= 0 && cy < GridRows)
                    {
                        if (obj.prefabId == "portal_pair")
                        {
                            PlacePortalCell(cx, cy, rot);
                            if (obj.portalExitPlacement != null &&
                                LevelGridUtility.IsValidPlacement(grid, obj.portalExitPlacement))
                            {
                                PlacePortalCell(obj.portalExitPlacement.cellX,
                                    obj.portalExitPlacement.cellY, obj.portalExitPlacement.rotationDegrees);
                            }
                            continue;
                        }
                        gridCells[cx, cy].isOccupied = true;
                        gridCells[cx, cy].prefabId = obj.prefabId;
                        gridCells[cx, cy].rotationDegrees = rot;
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
                        footprintWidth = 1,
                        footprintHeight = 1,
                        rotationDegrees = cell.rotationDegrees
                    };

                    LevelObjectData data = new LevelObjectData
                    {
                        prefabId = cell.prefabId,
                        instanceName = $"{cell.prefabId}_{x}_{y}",
                        localPosition = LevelGridUtility.GetLocalPosition(grid, placement),
                        localEulerAngles = new Vector3(0f, 0f, cell.rotationDegrees),
                        localScale = Vector3.one,
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
                grid = grid,
                cannonPlacement = new LevelGridPlacement
                {
                    cellX = cannonPos.x,
                    cellY = cannonPos.y,
                    footprintWidth = 1,
                    footprintHeight = 1,
                    rotationDegrees = cannonRotation
                },
                objects = objectsList.ToArray()
            };

            SaveLevelDefinitionAsset(doc);

            string dir = GetLevelsDirectory();
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string filePath = Path.Combine(dir, $"{currentLevelId}.json").Replace('\\', '/');
            string json = JsonUtility.ToJson(doc, true);
            File.WriteAllText(filePath, json);

            UpdateManifestWithLevelId(currentLevelId);
            AssetDatabase.Refresh();
            GameManager[] managers = FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < managers.Length; i++)
            {
                managers[i].RefreshLevelDefinitionsFromAssets();
            }
            RefreshManifest();
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
            selectedManifestIndex = manifestLevelIds.Count > 0
                ? Mathf.Clamp(selectedManifestIndex, 0, manifestLevelIds.Count - 1)
                : 0;
            creatingNewLevel = true;
            currentPalette = ActivePaletteItem.SodaCan;
            customPrefabId = "";
            currentRotation = 0f;
            ClearAllCells(false);

            if (syncToScene)
                SyncGridToScene();
            else
                ClearScenePreview();

            Repaint();
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

            try
            {
                SyncGridToSceneInternal();
            }
            catch (Exception ex)
            {
                ClearScenePreview();
                Debug.LogError($"[LevelEditor] Không thể đồng bộ level lên Scene: {ex}");
            }
        }

        private void SyncGridToSceneInternal()
        {
            if (catalog == null) LoadCatalog();
            if (catalog == null) return;

            ClearScenePreview();

            GameObject root = new GameObject(RootName);
            GameObjectManager manager = FindFirstObjectByType<GameObjectManager>(FindObjectsInactive.Include);
            if (manager != null)
                root.transform.SetParent(manager.transform, false);
            else
            {
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;
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

                        GameObject instance = InstantiateScenePreviewPrefab(prefab, root.transform);
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
                            footprintWidth = 1,
                            footprintHeight = 1,
                            rotationDegrees = cell.rotationDegrees
                        };

                        instance.transform.localPosition = LevelGridUtility.GetLocalPosition(grid, placement);
                        instance.transform.localRotation = LevelGridUtility.GetGridRotation(grid) *
                                                           Quaternion.Euler(0f, 0f, cell.rotationDegrees) *
                                                           prefab.transform.localRotation;
                        instance.transform.localScale = Vector3.Scale(prefab.transform.localScale, Vector3.one);

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
                        marker.Setup(cell.prefabId, x, y, 1, 1, cell.rotationDegrees);
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
                GameObject cannonAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Project/Resoruce_game/Prefab/Cannon 1.prefab");
                if (cannonAsset != null)
                {
                    GameObject cannonPreview = InstantiateScenePreviewPrefab(cannonAsset, root.transform);
                    cannonPreview.name = "Cannon Preview";
                    cannon = cannonPreview.GetComponentInChildren<CannonController>(true);
                }
            }
            if (cannon != null)
            {
                LevelGridPlacement cannonPlacement = new LevelGridPlacement
                {
                    cellX = cannonPos.x,
                    cellY = cannonPos.y,
                    footprintWidth = 1,
                    footprintHeight = 1,
                    rotationDegrees = cannonRotation
                };
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

            Undo.RegisterCreatedObjectUndo(root, "Sync Level Editor to Scene");
            SceneView.RepaintAll();
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
