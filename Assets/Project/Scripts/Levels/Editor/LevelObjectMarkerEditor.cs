using UnityEditor;
using UnityEngine;

namespace DreamForgeTD.EditorTools
{
    [CustomEditor(typeof(LevelObjectMarker))]
    public sealed class LevelObjectMarkerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            LevelObjectMarker marker = (LevelObjectMarker)target;
            if (marker == null) return;

            serializedObject.Update();

            EditorGUILayout.LabelField("Level Grid Object", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"Prefab ID: {marker.PrefabId}\nCell: ({marker.CellX}, {marker.CellY}) | Footprint: {marker.FootprintWidth}x{marker.FootprintHeight} | Rotation: {marker.RotationDegrees}°", MessageType.Info);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("prefabId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useGridPlacement"));

            if (marker.UseGridPlacement)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cellX"), new GUIContent("Cell X"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cellY"), new GUIContent("Cell Y"));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("footprintWidth"), new GUIContent("Width"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("footprintHeight"), new GUIContent("Height"));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationDegrees"));

                EditorGUILayout.Space(6);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Rotate +90°", GUILayout.Height(26)))
                {
                    LevelGridData grid = GetSceneGrid();
                    Undo.RecordObject(marker.transform, "Rotate Level Object");
                    Undo.RecordObject(marker, "Rotate Level Object");
                    Quaternion oldPlacement = LevelGridUtility.GetGridRotation(grid) * Quaternion.Euler(0f, 0f, marker.RotationDegrees);
                    Quaternion prefabRotation = Quaternion.Inverse(oldPlacement) * marker.transform.localRotation;
                    marker.RotationDegrees = (marker.RotationDegrees + 90f) % 360f;
                    marker.transform.localRotation = LevelGridUtility.GetGridRotation(grid) *
                                                     Quaternion.Euler(0f, 0f, marker.RotationDegrees) *
                                                     prefabRotation;
                    EditorUtility.SetDirty(marker);
                }

                if (GUILayout.Button("Snap to Grid", GUILayout.Height(26)))
                {
                    Undo.RecordObject(marker.transform, "Snap Level Object");
                    Undo.RecordObject(marker, "Snap Level Object");
                    marker.SnapToGrid(GetSceneGrid());
                    EditorUtility.SetDirty(marker);
                }

                EditorGUILayout.EndHorizontal();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static LevelGridData GetSceneGrid()
        {
            LevelGridData fallback = new LevelGridData();
            LevelBoardBounds boardBounds = UnityEngine.Object.FindFirstObjectByType<LevelBoardBounds>(FindObjectsInactive.Include);
            GameObjectManager manager = UnityEngine.Object.FindFirstObjectByType<GameObjectManager>(FindObjectsInactive.Include);
            if (boardBounds != null && boardBounds.TryGetGridData(
                    manager != null ? manager.transform : null,
                    fallback.columns,
                    fallback.rows,
                    out LevelGridData sceneGrid))
            {
                return sceneGrid;
            }

            return fallback;
        }
    }
}
