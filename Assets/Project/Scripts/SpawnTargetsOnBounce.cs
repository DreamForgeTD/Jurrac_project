using UnityEngine;

namespace DreamForgeTD
{
    [RequireComponent(typeof(BounceSurface))]
    public sealed class SpawnTargetsOnBounce : MonoBehaviour, IBulletMechanic
    {
        [SerializeField] private Target targetPrefab;
        [Tooltip("Offset from this surface's position, rotated with the surface but not scaled.")]
        [SerializeField] private Vector3 firstTargetOffset = new Vector3(-2f, 0f, 0f);
        [Tooltip("Offset from this surface's position, rotated with the surface but not scaled.")]
        [SerializeField] private Vector3 secondTargetOffset = new Vector3(2f, 0f, 0f);
        [SerializeField] private bool spawnOnlyOnce = true;

        private bool hasSpawned;

        private void Awake()
        {
            if (targetPrefab == null)
            {
                Debug.LogError("SpawnTargetsOnBounce needs a Target prefab assigned.", this);
                enabled = false;
            }
        }

        public void OnBulletHit(BulletHitContext hit)
        {
            if (hit.Body == null || targetPrefab == null || (spawnOnlyOnce && hasSpawned))
                return;

            hasSpawned = true;
            SpawnTarget(firstTargetOffset, "A");
            SpawnTarget(secondTargetOffset, "B");
        }

        private void SpawnTarget(Vector3 localOffset, string label)
        {
            Vector3 spawnPosition = transform.position + transform.rotation * localOffset;
            Quaternion spawnRotation = transform.rotation * targetPrefab.transform.rotation;
            Target spawnedTarget = Instantiate(targetPrefab, spawnPosition, spawnRotation);
            spawnedTarget.name = $"{targetPrefab.name} {label}";
        }
    }
}
