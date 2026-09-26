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
            Quaternion localRotation = Quaternion.Euler(objectData.localEulerAngles) * prefab.transform.localRotation;
            Vector3 worldPosition = managedRoot.TransformPoint(objectData.localPosition);
            Quaternion worldRotation = managedRoot.rotation * localRotation;
            try
            {
                instance = Instantiate(prefab, worldPosition, worldRotation, managedRoot);
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
