using System;
using UnityEngine;

namespace DreamForgeTD
{
    [Serializable]
    public sealed class LevelGridData
    {
        public int columns = 18;
        public int rows = 32;
        public int referenceWidth = 1080;
        public int referenceHeight = 1920;
        public float worldUnitsPerCell = 0.3875f;
        public Vector3 localCenter = new Vector3(0f, 1f, 0f);
        public Vector3 localEulerAngles = Vector3.zero;
    }

    [Serializable]
    public sealed class LevelGridPlacement
    {
        public int cellX;
        public int cellY;
        public int footprintWidth = 1;
        public int footprintHeight = 1;
        public float rotationDegrees;
    }

    public static class LevelGridUtility
    {
        public static bool IsValidGrid(LevelGridData grid)
        {
            return grid != null &&
                   grid.columns > 0 && grid.rows > 0 &&
                   grid.referenceWidth > 0 && grid.referenceHeight > 0 &&
                   IsFinite(grid.worldUnitsPerCell) && grid.worldUnitsPerCell > 0f &&
                   IsFinite(grid.localCenter) && IsFinite(grid.localEulerAngles);
        }

        public static bool IsValidPlacement(LevelGridData grid, LevelGridPlacement placement)
        {
            if (!IsValidGrid(grid) || placement == null ||
                placement.footprintWidth <= 0 || placement.footprintHeight <= 0 ||
                !IsFinite(placement.rotationDegrees))
                return false;

            GetFootprint(placement, out int width, out int height);
            return placement.cellX >= 0 && placement.cellY >= 0 &&
                   placement.cellX + width <= grid.columns &&
                   placement.cellY + height <= grid.rows;
        }

        public static void GetFootprint(LevelGridPlacement placement, out int width, out int height)
        {
            width = Mathf.Max(1, placement.footprintWidth);
            height = Mathf.Max(1, placement.footprintHeight);
        }

        public static float NormalizeRotation(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                return 0f;

            return Mathf.Repeat(degrees, 360f);
        }

        public static Vector3 GetLocalPosition(LevelGridData grid, LevelGridPlacement placement)
        {
            GetFootprint(placement, out int width, out int height);
            Vector3 gridOffset = new Vector3(
                (placement.cellX + width * 0.5f - grid.columns * 0.5f) * grid.worldUnitsPerCell,
                (grid.rows * 0.5f - placement.cellY - height * 0.5f) * grid.worldUnitsPerCell,
                0f);
            return grid.localCenter + GetGridRotation(grid) * gridOffset;
        }

        public static Quaternion GetGridRotation(LevelGridData grid)
        {
            return grid == null ? Quaternion.identity : Quaternion.Euler(grid.localEulerAngles);
        }

        public static LevelObjectData CreateSpawnData(LevelGridData grid, LevelObjectData source)
        {
            if (source == null)
                return null;

            LevelObjectData result = new LevelObjectData
            {
                prefabId = source.prefabId,
                instanceName = source.instanceName,
                localPosition = source.localPosition,
                localEulerAngles = source.localEulerAngles,
                localScale = source.localScale,
                gridPlacement = source.gridPlacement,
                portalExitPlacement = source.portalExitPlacement
            };

            if (IsValidGrid(grid) && source.gridPlacement != null)
            {
                result.localPosition = GetLocalPosition(grid, source.gridPlacement);
                Quaternion objectOffset = Quaternion.Euler(
                    source.localEulerAngles.x,
                    source.localEulerAngles.y,
                    0f);
                Quaternion placementRotation = GetGridRotation(grid) *
                                               Quaternion.Euler(0f, 0f, source.gridPlacement.rotationDegrees) *
                                               objectOffset;
                result.localEulerAngles = placementRotation.eulerAngles;
            }

            return result;
        }

        public static bool TryGetCell(
            LevelGridData grid,
            Vector3 localPosition,
            int footprintWidth,
            int footprintHeight,
            float rotationDegrees,
            out int cellX,
            out int cellY)
        {
            cellX = 0;
            cellY = 0;
            if (!IsValidGrid(grid) || !IsFinite(localPosition) ||
                footprintWidth <= 0 || footprintHeight <= 0 || !IsFinite(rotationDegrees))
                return false;

            LevelGridPlacement temporary = new LevelGridPlacement
            {
                footprintWidth = footprintWidth,
                footprintHeight = footprintHeight,
                rotationDegrees = rotationDegrees
            };
            GetFootprint(temporary, out int width, out int height);

            Vector3 gridOffset = Quaternion.Inverse(GetGridRotation(grid)) * (localPosition - grid.localCenter);
            float cell = grid.worldUnitsPerCell;
            cellX = Mathf.RoundToInt(gridOffset.x / cell - width * 0.5f + grid.columns * 0.5f);
            cellY = Mathf.RoundToInt(grid.rows * 0.5f - gridOffset.y / cell - height * 0.5f);
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

}
