using UnityEditor;
using UnityEngine;

namespace DreamForgeTD.EditorTools
{
    [CustomEditor(typeof(GameManager))]
    public sealed class GameManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            GameManager manager = (GameManager)target;
            if (manager == null) return;

            DrawDefaultInspector();
            VeCauHinhCanMap();

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎮 RUNTIME LEVEL CONTROLS", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                string statusText = manager.IsLevelWon ? "🏆 CHIẾN THẮNG (TẤT CẢ LON ĐÃ BỊ HẠ)!" : "Đang chơi...";
                EditorGUILayout.LabelField($"Trạng thái: {statusText}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Màn hiện tại: {manager.CurrentLevelName} ({manager.CurrentLevelId}) [Index: {manager.CurrentLevelIndex}]");
                EditorGUILayout.LabelField($"Lon còn lại: {manager.RemainingCans} / {manager.TotalCans}");

                EditorGUILayout.Space(6);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("🔄 Chơi lại (Restart)", GUILayout.Height(28)))
                {
                    manager.RestartLevel();
                }

                if (GUILayout.Button("⏭️ Màn tiếp theo (Next)", GUILayout.Height(28)))
                {
                    manager.NextLevel();
                }

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Các nút điều khiển Next / Restart sẽ khả dụng khi chạy Play Mode.", MessageType.Info);

                if (GUILayout.Button("Nạp lại danh sách LevelDefinition từ Level Editor"))
                {
                    Undo.RecordObject(manager, "Refresh LevelDefinition list");
                    manager.RefreshLevelDefinitionsFromAssets();
                    EditorUtility.SetDirty(manager);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static void VeCauHinhCanMap()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Co giãn map theo màn hình", EditorStyles.boldLabel);
            Camera camera = Camera.main;
            LevelBoardBounds khungMap = camera != null ? camera.GetComponent<LevelBoardBounds>() : null;
            if (khungMap == null)
            {
                EditorGUILayout.HelpBox("Gắn LevelBoardBounds trên Main Camera để bật tự căn map.", MessageType.Info);
                return;
            }

            SerializedObject cauHinh = new SerializedObject(khungMap);
            cauHinh.Update();
            EditorGUILayout.PropertyField(cauHinh.FindProperty("tuCanMapTheoManHinh"), new GUIContent("Tự căn map"));
            EditorGUILayout.PropertyField(cauHinh.FindProperty("referenceResolution"), new GUIContent("Độ phân giải thiết kế"));
            cauHinh.ApplyModifiedProperties();
            EditorGUILayout.HelpBox(
                "Cấu hình được lưu trên LevelBoardBounds của Main Camera. Khi Play, camera orthographic tự zoom để thấy đủ khung thiết kế, giữ tỉ lệ và vật lý của map. Có thể có khoảng dư ở mép màn hình. Size gốc lấy từ camera lúc bắt đầu Play.",
                MessageType.Info);
        }
    }
}
