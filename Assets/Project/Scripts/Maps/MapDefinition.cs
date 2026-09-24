using UnityEngine;

namespace DreamForgeTD
{
    [CreateAssetMenu(menuName = "DreamForge/Maps/Map Definition", fileName = "Map_")]
    public sealed class MapDefinition : ScriptableObject
    {
        [Header("Target")]
        [SerializeField] private GameObject targetPrefab;
        [Tooltip("Position relative to the MapSpawner's map root.")]
        [SerializeField] private Vector3 targetLocalPosition;
        [Tooltip("Rotation in Euler angles, relative to the MapSpawner's map root.")]
        [SerializeField] private Vector3 targetLocalEulerAngles;

        [Header("Obstacles")]
        [SerializeField] private MapObstaclePlacement[] obstacles = new MapObstaclePlacement[0];

        public GameObject TargetPrefab => targetPrefab;
        public Vector3 TargetLocalPosition => targetLocalPosition;
        public Quaternion TargetLocalRotation => Quaternion.Euler(targetLocalEulerAngles);
        public int ObstacleCount => obstacles == null ? 0 : obstacles.Length;

        public MapObstaclePlacement GetObstacle(int index)
        {
            return obstacles[index];
        }
    }

    [System.Serializable]
    public struct MapObstaclePlacement
    {
        [SerializeField] private GameObject prefab;
        [Tooltip("Position relative to the MapSpawner's map root.")]
        [SerializeField] private Vector3 localPosition;
        [Tooltip("Rotation in Euler angles, relative to the MapSpawner's map root.")]
        [SerializeField] private Vector3 localEulerAngles;

        public GameObject Prefab => prefab;
        public Vector3 LocalPosition => localPosition;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
    }
}
