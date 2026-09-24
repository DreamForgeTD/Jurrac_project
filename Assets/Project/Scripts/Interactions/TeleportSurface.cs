using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    // Extension example: no changes to launcher, projectile dispatch, or level flow.
    [RequireComponent(typeof(ProjectileInteractionTarget))]
    public sealed class TeleportSurface : MonoBehaviour, IProjectileInteractable
    {
        [Tooltip("Place outside the destination collider to avoid immediately entering it.")]
        [SerializeField] private Transform destination;

        private void Awake()
        {
            if (destination != null) return;
            Debug.LogError($"{name}: Assign Destination.", this);
            enabled = false;
        }

        public void OnProjectileHit(Projectile projectile, ProjectileHitContext context)
            => projectile.Teleport(destination.position);
    }
}
