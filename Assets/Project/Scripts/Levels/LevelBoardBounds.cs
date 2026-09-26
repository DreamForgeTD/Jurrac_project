using UnityEngine;

namespace DreamForgeTD
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class LevelBoardBounds : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform placementPlane;
        [SerializeField] private Vector2Int referenceResolution = new Vector2Int(1080, 1920);
        [Tooltip("Tự căn camera orthographic để thấy đủ khung thiết kế trên các tỉ lệ màn hình. Chỉ áp dụng khi Play.")]
        [SerializeField] private bool tuCanMapTheoManHinh = true;
        [SerializeField, Min(0.01f)] private float perspectivePlaneDistance = 10f;
        [SerializeField] private Vector3 centerOffset;
        [SerializeField] private bool drawGrid = true;
        [SerializeField] private bool drawCellCenters = true;
        [SerializeField] private Color boundsColor = new Color(1f, 0.72f, 0.12f, 1f);
        [SerializeField] private Color gridColor = new Color(0.25f, 0.8f, 1f, 0.7f);

        public Vector2Int ReferenceResolution => referenceResolution;

        private Camera cameraDaCan;
        private float kichThuocCameraGoc;

        private void OnEnable() => CanMapTheoManHinh();

        private void Update() => CanMapTheoManHinh();

        private void OnDisable() => KhoiPhucCamera();

        private void CanMapTheoManHinh()
        {
            if (!Application.isPlaying || !tuCanMapTheoManHinh)
            {
                KhoiPhucCamera();
                return;
            }

            Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
            if (cameraToUse == null || !cameraToUse.orthographic)
            {
                KhoiPhucCamera();
                return;
            }

            if (cameraDaCan != cameraToUse)
            {
                KhoiPhucCamera();
                cameraDaCan = cameraToUse;
                kichThuocCameraGoc = cameraToUse.orthographicSize;
            }

            if (cameraToUse.aspect <= 0f)
                return;

            float tiLeGoc = (float)Mathf.Max(1, referenceResolution.x) / Mathf.Max(1, referenceResolution.y);
            // Always calculate from the authored size, never from the previous fitted size.
            float kichThuocMoi = kichThuocCameraGoc * Mathf.Max(1f, tiLeGoc / cameraToUse.aspect);
            if (!Mathf.Approximately(cameraToUse.orthographicSize, kichThuocMoi))
                cameraToUse.orthographicSize = kichThuocMoi;
        }

        private void KhoiPhucCamera()
        {
            if (cameraDaCan != null)
                cameraDaCan.orthographicSize = kichThuocCameraGoc;
            cameraDaCan = null;
        }

        private void Reset()
        {
            targetCamera = GetComponent<Camera>();
        }

        public bool TryGetGridData(Transform coordinateRoot, int columns, int rows, out LevelGridData grid)
        {
            grid = null;
            Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
            if (cameraToUse == null || columns <= 0 || rows <= 0)
                return false;

            Vector3 worldCenter;
            Quaternion worldRotation;
            if (placementPlane != null)
            {
                worldCenter = placementPlane.position;
                worldRotation = placementPlane.rotation;
            }
            else
            {
                worldCenter = cameraToUse.transform.position +
                              cameraToUse.transform.forward * Mathf.Max(0.01f, perspectivePlaneDistance);
                worldRotation = cameraToUse.transform.rotation;
            }

            worldCenter += worldRotation * centerOffset;

            float distance = Mathf.Abs(Vector3.Dot(worldCenter - cameraToUse.transform.position, cameraToUse.transform.forward));
            // Runtime framing must not change the grid saved by the Level Editor.
            float orthographicSize = cameraDaCan == cameraToUse
                ? kichThuocCameraGoc : cameraToUse.orthographicSize;
            float worldHeight = cameraToUse.orthographic
                ? orthographicSize * 2f
                : distance * 2f * Mathf.Tan(cameraToUse.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = (float)Mathf.Max(1, referenceResolution.x) / Mathf.Max(1, referenceResolution.y);
            float worldWidth = worldHeight * aspect;
            float worldUnitsPerCell = Mathf.Min(worldWidth / columns, worldHeight / rows);

            if (coordinateRoot != null)
            {
                Quaternion worldBoardRotation = worldRotation;
                worldCenter = coordinateRoot.InverseTransformPoint(worldCenter);
                worldRotation = Quaternion.Inverse(coordinateRoot.rotation) * worldRotation;
                Vector3 localRight = coordinateRoot.InverseTransformVector(worldBoardRotation * Vector3.right);
                Vector3 localUp = coordinateRoot.InverseTransformVector(worldBoardRotation * Vector3.up);
                float localScale = Mathf.Sqrt(Mathf.Max(0.0001f, localRight.magnitude * localUp.magnitude));
                worldUnitsPerCell *= localScale;
            }

            grid = new LevelGridData
            {
                columns = columns,
                rows = rows,
                referenceWidth = Mathf.Max(1, referenceResolution.x),
                referenceHeight = Mathf.Max(1, referenceResolution.y),
                worldUnitsPerCell = worldUnitsPerCell,
                localCenter = worldCenter,
                localEulerAngles = worldRotation.eulerAngles
            };
            return LevelGridUtility.IsValidGrid(grid);
        }

        private void OnDrawGizmos()
        {
            if (!TryGetGridData(null, 18, 32, out LevelGridData grid))
                return;

            Vector3 center = grid.localCenter;
            Quaternion rotation = LevelGridUtility.GetGridRotation(grid);
            float halfWidth = grid.columns * grid.worldUnitsPerCell * 0.5f;
            float halfHeight = grid.rows * grid.worldUnitsPerCell * 0.5f;

            Gizmos.color = boundsColor;
            DrawLine(center, rotation, -halfWidth, -halfHeight, halfWidth, -halfHeight);
            DrawLine(center, rotation, halfWidth, -halfHeight, halfWidth, halfHeight);
            DrawLine(center, rotation, halfWidth, halfHeight, -halfWidth, halfHeight);
            DrawLine(center, rotation, -halfWidth, halfHeight, -halfWidth, -halfHeight);

            if (!drawGrid)
                return;

            Gizmos.color = gridColor;
            for (int column = 1; column < grid.columns; column++)
            {
                float x = -halfWidth + column * grid.worldUnitsPerCell;
                DrawLine(center, rotation, x, -halfHeight, x, halfHeight);
            }

            for (int row = 1; row < grid.rows; row++)
            {
                float y = -halfHeight + row * grid.worldUnitsPerCell;
                DrawLine(center, rotation, -halfWidth, y, halfWidth, y);
            }

            if (!drawCellCenters)
                return;

            float pointSize = Mathf.Max(0.025f, grid.worldUnitsPerCell * 0.045f);
            for (int row = 0; row < grid.rows; row++)
            {
                for (int column = 0; column < grid.columns; column++)
                {
                    Vector3 offset = new Vector3(
                        (column + 0.5f - grid.columns * 0.5f) * grid.worldUnitsPerCell,
                        (grid.rows * 0.5f - row - 0.5f) * grid.worldUnitsPerCell,
                        0f);
                    Gizmos.DrawSphere(center + rotation * offset, pointSize);
                }
            }
        }

        private static void DrawLine(Vector3 center, Quaternion rotation, float x1, float y1, float x2, float y2)
        {
            Vector3 start = center + rotation * new Vector3(x1, y1, 0f);
            Vector3 end = center + rotation * new Vector3(x2, y2, 0f);
            Gizmos.DrawLine(start, end);
        }
    }
}
