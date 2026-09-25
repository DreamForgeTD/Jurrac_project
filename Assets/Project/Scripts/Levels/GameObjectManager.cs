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
        private Transform managedRoot;

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
            instance = Instantiate(prefab, managedRoot, false);
            instance.name = string.IsNullOrWhiteSpace(objectData.instanceName)
                ? prefab.name
                : objectData.instanceName;

            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = objectData.localPosition;
            instanceTransform.localRotation = Quaternion.Euler(objectData.localEulerAngles);
            instanceTransform.localScale = objectData.localScale;
            managedObjects.Add(instance);
            return true;
        }

        public void ClearManagedObjects()
        {
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
