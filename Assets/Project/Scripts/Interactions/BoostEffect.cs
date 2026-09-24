using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    [RequireComponent(typeof(ProjectileInteractionTarget))]
    public sealed class BoostEffect : MonoBehaviour, IProjectileInteractable
    {
        [SerializeField, Min(0f)] private float multiplier = 1.3f;

        public void OnProjectileHit(Projectile projectile, ProjectileHitContext context)
            => projectile.MultiplyVelocity(multiplier);
    }
}
