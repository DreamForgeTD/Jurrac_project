using System;
using UnityEngine;

namespace DreamForgeTD
{
    /// <summary>
    /// Component gắn vào các object trong màn chơi do Level Editor tạo ra
    /// để lưu thông tin ô lưới (grid placement), footprint, góc xoay và ID prefab.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public sealed class LevelObjectMarker : MonoBehaviour
    {
        [SerializeField] private string prefabId;
        [SerializeField] private int cellX;
        [SerializeField] private int cellY;
        [SerializeField] private int footprintWidth = 1;
        [SerializeField] private int footprintHeight = 1;
        [SerializeField] private float rotationDegrees;
        [SerializeField] private bool useGridPlacement = true;

        public string PrefabId
        {
            get => prefabId;
            set => prefabId = value;
        }

        public int CellX
        {
            get => cellX;
            set => cellX = value;
        }

        public int CellY
        {
            get => cellY;
            set => cellY = value;
        }

        public int FootprintWidth
        {
            get => Mathf.Max(1, footprintWidth);
            set => footprintWidth = Mathf.Max(1, value);
        }

        public int FootprintHeight
        {
            get => Mathf.Max(1, footprintHeight);
            set => footprintHeight = Mathf.Max(1, value);
        }

        public float RotationDegrees
        {
            get => rotationDegrees;
            set => rotationDegrees = LevelGridUtility.NormalizeRotation(value);
        }

        public bool UseGridPlacement
        {
            get => useGridPlacement;
            set => useGridPlacement = value;
        }

        public void Setup(string id, int x, int y, int width, int height, float rotation)
        {
            prefabId = id;
            cellX = x;
            cellY = y;
            footprintWidth = Mathf.Max(1, width);
            footprintHeight = Mathf.Max(1, height);
            rotationDegrees = LevelGridUtility.NormalizeRotation(rotation);
            useGridPlacement = true;
        }

        public LevelGridPlacement ToGridPlacement()
        {
            if (!useGridPlacement)
                return null;

            return new LevelGridPlacement
            {
                cellX = cellX,
                cellY = cellY,
                footprintWidth = FootprintWidth,
                footprintHeight = FootprintHeight,
                rotationDegrees = RotationDegrees
            };
        }

        public void SnapToGrid(LevelGridData grid)
        {
            if (!LevelGridUtility.IsValidGrid(grid))
                return;

            if (LevelGridUtility.TryGetCell(grid, transform.localPosition, FootprintWidth, FootprintHeight, RotationDegrees, out int newX, out int newY))
            {
                cellX = newX;
                cellY = newY;
                LevelGridPlacement placement = ToGridPlacement();
                transform.localPosition = LevelGridUtility.GetLocalPosition(grid, placement);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Vẽ footprint gizmo khi object được chọn trong scene
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.75f);
            LevelGridPlacement placement = ToGridPlacement();
            if (placement != null)
            {
                LevelGridUtility.GetFootprint(placement, out int w, out int h);
                float cellSize = 0.3875f;
                Gizmos.DrawWireCube(transform.position, new Vector3(w * cellSize, h * cellSize, 0.1f));
            }
        }
    }
}
