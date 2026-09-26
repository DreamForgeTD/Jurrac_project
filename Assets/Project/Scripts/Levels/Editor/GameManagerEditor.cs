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
    }
}
