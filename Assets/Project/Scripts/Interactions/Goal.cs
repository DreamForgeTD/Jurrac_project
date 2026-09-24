using System;
using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    [RequireComponent(typeof(ProjectileInteractionTarget))]
    public sealed class Goal : MonoBehaviour, IProjectileInteractable, ILevelResettable
    {
        private bool reached;
        public event Action Reached;

        public void OnProjectileHit(Projectile projectile, ProjectileHitContext context)
        {
            if (reached || projectile.State != ProjectileState.Flying) return;
            reached = true;
            Reached?.Invoke();
            projectile.Finish();
        }

        public void ResetState() => reached = false;
    }
}
