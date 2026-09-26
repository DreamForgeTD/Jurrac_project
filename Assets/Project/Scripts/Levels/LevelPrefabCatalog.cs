using System;
using UnityEngine;

namespace DreamForgeTD
{
    [CreateAssetMenu(menuName = "DreamForge/Levels/Prefab Catalog", fileName = "LevelPrefabCatalog")]
    public sealed class LevelPrefabCatalog : ScriptableObject
    {
        [SerializeField] private LevelPrefabEntry[] entries = new LevelPrefabEntry[0];

        public int EntryCount => entries != null ? entries.Length : 0;

        public LevelPrefabEntry GetEntry(int index)
        {
            return entries[index];
        }

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
#if UNITY_EDITOR
                    if (prefab != null)
                    {
                        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(prefab);
                        if (!string.IsNullOrEmpty(assetPath))
                            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    }
                    if (prefab == null)
                    {
                        prefab = FindFallbackPrefab(entries[i].Id);
                    }
#endif
                    return prefab != null;
                }
            }

#if UNITY_EDITOR
            prefab = FindFallbackPrefab(prefabId);
            return prefab != null;
#else
            return false;
#endif
        }

#if UNITY_EDITOR
        private static GameObject FindFallbackPrefab(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            switch (id.ToLowerInvariant())
            {
                case "soda_can":
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Resoruce_game/Prefab/SodaCan_330ml.prefab");
                case "target":
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Resoruce_game/Prefab/Target.prefab");
                case "bounce_wall":
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Resoruce_game/Prefab/Wall_bounce.prefab");
                case "portal_pair":
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Resoruce_game/Prefab/PortalPair.prefab");
                case "magnet":
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Resoruce_game/Prefab/Magnett.prefab");
                default:
                    return null;
            }
        }
#endif
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
