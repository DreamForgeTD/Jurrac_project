using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    [RequireComponent(typeof(ProjectileInteractionTarget))]
    public sealed class Hazard : MonoBehaviour, IProjectileInteractable
    {
        public void OnProjectileHit(Projectile projectile, ProjectileHitContext context) => projectile.Finish();
    }
}
