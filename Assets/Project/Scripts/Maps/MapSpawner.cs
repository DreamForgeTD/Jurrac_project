using UnityEngine;

namespace DreamForgeTD
{
    public sealed class MapSpawner : MonoBehaviour
    {
        private const string GeneratedRootName = "Generated Map Content";

        [SerializeField] private MapDefinition mapDefinition;
        [Tooltip("If empty, this GameObject is used as the map's coordinate root.")]
        [SerializeField] private Transform mapRoot;
        [SerializeField] private bool spawnOnStart = true;

        private Transform generatedRoot;

        private void Start()
        {
            if (spawnOnStart)
                SpawnMap();
        }

        public void SpawnMap()
        {
            if (mapDefinition == null)
            {
                Debug.LogError("MapSpawner needs a MapDefinition asset.", this);
                return;
            }

            if (mapDefinition.TargetPrefab == null)
            {
                Debug.LogError("MapDefinition needs a target prefab.", mapDefinition);
                return;
            }

            ClearGeneratedContent();

            Transform parent = mapRoot != null ? mapRoot : transform;
            GameObject rootObject = new GameObject(GeneratedRootName);
            generatedRoot = rootObject.transform;
            generatedRoot.SetParent(parent, false);

            SpawnPrefab(
                mapDefinition.TargetPrefab,
                "Target",
                mapDefinition.TargetLocalPosition,
                mapDefinition.TargetLocalRotation);

            for (int i = 0; i < mapDefinition.ObstacleCount; i++)
            {
                MapObstaclePlacement obstacle = mapDefinition.GetObstacle(i);
                if (obstacle.Prefab == null)
                {
                    Debug.LogWarning($"MapDefinition obstacle at index {i} has no prefab and was skipped.", mapDefinition);
                    continue;
                }

                SpawnPrefab(
                    obstacle.Prefab,
                    obstacle.Prefab.name,
                    obstacle.LocalPosition,
                    obstacle.LocalRotation);
            }
        }

        private void SpawnPrefab(GameObject prefab, string instanceName, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject instance = Instantiate(prefab, generatedRoot, false);
            instance.name = instanceName;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
        }

        private void ClearGeneratedContent()
        {
            if (generatedRoot == null)
                return;

            if (Application.isPlaying)
                Destroy(generatedRoot.gameObject);
            else
                DestroyImmediate(generatedRoot.gameObject);

            generatedRoot = null;
        }
    }
}
