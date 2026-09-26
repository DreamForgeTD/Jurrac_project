using UnityEngine;

namespace DreamForgeTD
{
    [AddComponentMenu("DreamForge/Levels/Prefab Footprint")]
    [DisallowMultipleComponent]
    public sealed class LevelPrefabFootprint : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("Number of editor grid cells occupied across the prefab.")]
        private int widthInCells = 1;

        [SerializeField, Min(1), Tooltip("Number of editor grid cells occupied down the prefab.")]
        private int heightInCells = 1;

        public int WidthInCells => Mathf.Max(1, widthInCells);
        public int HeightInCells => Mathf.Max(1, heightInCells);

        private void OnValidate()
        {
            widthInCells = Mathf.Max(1, widthInCells);
            heightInCells = Mathf.Max(1, heightInCells);
        }
    }
}
