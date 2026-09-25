using System;
using UnityEngine;

namespace DreamForgeTD
{
    [CreateAssetMenu(menuName = "DreamForge/Levels/Prefab Catalog", fileName = "LevelPrefabCatalog")]
    public sealed class LevelPrefabCatalog : ScriptableObject
    {
        [SerializeField] private LevelPrefabEntry[] entries = new LevelPrefabEntry[0];

        public bool TryGetPrefab(string prefabId, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrWhiteSpace(prefabId) || entries == null)
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].Id, prefabId, StringComparison.OrdinalIgnoreCase))
                {
                    prefab = entries[i].Prefab;
                    return prefab != null;
                }
            }

            return false;
        }
    }

    [Serializable]
    public struct LevelPrefabEntry
    {
        [SerializeField] private string id;
        [SerializeField] private GameObject prefab;

        public string Id => id;
        public GameObject Prefab => prefab;
    }
}
