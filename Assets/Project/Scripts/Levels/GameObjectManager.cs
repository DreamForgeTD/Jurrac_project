using System.Collections.Generic;
using UnityEngine;

namespace DreamForgeTD
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GameObjectManager : MonoBehaviour
    {
        [SerializeField] private string managedRootName = "Managed Level Objects";

        private readonly List<GameObject> managedObjects = new List<GameObject>();
        private readonly Dictionary<GameObject, Stack<GameObject>> canPools =
            new Dictionary<GameObject, Stack<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> pooledCanPrefabs =
            new Dictionary<GameObject, GameObject>();
        private Transform managedRoot;
        private Transform poolRoot;

        public IReadOnlyList<GameObject> ManagedObjects => managedObjects;

        private void Awake()
        {
            EnsureManagedRoot();
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        public bool TrySpawn(
            GameObject prefab,
            LevelObjectData objectData,
            out GameObject instance)
        {
            instance = null;
            if (prefab == null || objectData == null)
                return false;

            EnsureManagedRoot();
            Quaternion localRotation = Quaternion.Euler(objectData.localEulerAngles) * prefab.transform.localRotation;
            Vector3 worldPosition = managedRoot.TransformPoint(objectData.localPosition);
            Quaternion worldRotation = managedRoot.rotation * localRotation;
            bool isCanPrefab = Application.isPlaying && prefab.GetComponent<BowlingCan>() != null;
            try
            {
                instance = isCanPrefab
                    ? GetPooledCan(prefab, worldPosition, worldRotation)
                    : Instantiate(prefab, worldPosition, worldRotation, managedRoot);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameObjectManager] Failed to instantiate {prefab}: {ex.Message}");
                return false;
            }

            if (instance == null)
                return false;

            instance.name = string.IsNullOrWhiteSpace(objectData.instanceName)
                ? prefab.name
                : objectData.instanceName;

            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = objectData.localPosition;
            instanceTransform.localRotation = localRotation;
            instanceTransform.localScale = Vector3.Scale(prefab.transform.localScale, objectData.localScale);

            if (isCanPrefab)
            {
                pooledCanPrefabs[instance] = prefab;
                BowlingCan[] cans = instance.GetComponentsInChildren<BowlingCan>(true);
                for (int i = 0; i < cans.Length; i++)
                {
                    if (cans[i] != null)
                        cans[i].ResetForSpawn();
                }
            }

            MagnetForceField magnet = instance.GetComponent<MagnetForceField>();
            if (magnet != null)
                magnet.AlignVisualToFieldCenter();

            Rigidbody[] bodies = instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (body == null || body.isKinematic)
                    continue;

                body.position = body.transform.position;
                body.rotation = body.transform.rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.Sleep();
            }

            managedObjects.Add(instance);
            return true;
        }

        public bool ReturnToPool(GameObject instance)
        {
            if (!Application.isPlaying || instance == null)
                return false;

            GameObject pooledInstance = FindPooledCanRoot(instance);
            if (pooledInstance == null || !managedObjects.Contains(pooledInstance) ||
                !pooledCanPrefabs.TryGetValue(pooledInstance, out GameObject prefab))
                return false;

            BowlingCan[] cans = pooledInstance.GetComponentsInChildren<BowlingCan>(true);
            for (int i = 0; i < cans.Length; i++)
            {
                if (cans[i] != null)
                    cans[i].PrepareForPool();
            }

            managedObjects.Remove(pooledInstance);
            pooledInstance.SetActive(false);
            pooledInstance.transform.SetParent(EnsurePoolRoot(), false);
            pooledInstance.transform.localPosition = Vector3.zero;
            pooledInstance.transform.localRotation = Quaternion.identity;
            pooledInstance.transform.localScale = prefab.transform.localScale;

            if (!canPools.TryGetValue(prefab, out Stack<GameObject> pool))
            {
                pool = new Stack<GameObject>();
                canPools.Add(prefab, pool);
            }

            pool.Push(pooledInstance);
            return true;
        }

        public void ClearManagedObjects()
        {
            if (Application.isPlaying)
            {
                for (int i = managedObjects.Count - 1; i >= 0; i--)
                {
                    GameObject instance = managedObjects[i];
                    if (instance != null && pooledCanPrefabs.ContainsKey(instance))
                        ReturnToPool(instance);
                }
            }

            if (managedRoot != null)
            {
                GameObject oldRoot = managedRoot.gameObject;
                oldRoot.SetActive(false);
                if (Application.isPlaying)
                    Destroy(oldRoot);
                else
                    DestroyImmediate(oldRoot);
            }

            managedObjects.Clear();
            managedRoot = null;
            EnsureManagedRoot();
        }

        private GameObject GetPooledCan(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (canPools.TryGetValue(prefab, out Stack<GameObject> pool))
            {
                while (pool.Count > 0)
                {
                    GameObject pooledInstance = pool.Pop();
                    if (pooledInstance == null)
                        continue;

                    pooledInstance.transform.SetParent(managedRoot, false);
                    pooledInstance.transform.SetPositionAndRotation(position, rotation);
                    pooledInstance.SetActive(true);
                    return pooledInstance;
                }
            }

            GameObject instance = Instantiate(prefab, position, rotation, managedRoot);
            if (instance != null)
                pooledCanPrefabs[instance] = prefab;
            return instance;
        }

        private GameObject FindPooledCanRoot(GameObject instance)
        {
            Transform current = instance.transform;
            while (current != null)
            {
                if (pooledCanPrefabs.ContainsKey(current.gameObject))
                    return current.gameObject;
                if (current == transform)
                    break;
                current = current.parent;
            }

            return null;
        }

        private Transform EnsurePoolRoot()
        {
            if (poolRoot != null)
                return poolRoot;

            GameObject root = new GameObject("Can Pool");
            poolRoot = root.transform;
            poolRoot.SetParent(transform, false);
            return poolRoot;
        }

        private void EnsureManagedRoot()
        {
            if (managedRoot != null)
                return;

            GameObject root = new GameObject(string.IsNullOrWhiteSpace(managedRootName)
                ? "Managed Level Objects"
                : managedRootName);
            managedRoot = root.transform;
            managedRoot.SetParent(transform, false);
        }
    }
}
