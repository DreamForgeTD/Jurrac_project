using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    // Reflection comes from the collider's Physics Material. This adjusts outgoing speed.
    [RequireComponent(typeof(ProjectileInteractionTarget))]
    public sealed class BounceEffect : MonoBehaviour, IProjectileInteractable
    {
        [SerializeField, Min(0f)] private float speedMultiplier = 1.05f;

        public void OnProjectileHit(Projectile projectile, ProjectileHitContext context)
            => projectile.MultiplyVelocity(speedMultiplier);
    }
}
