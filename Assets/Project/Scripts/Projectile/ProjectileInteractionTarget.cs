using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    // One cache per collider object. Runtime AddComponent requires rebuilding the level.
    [DisallowMultipleComponent]
    public sealed class ProjectileInteractionTarget : MonoBehaviour
    {
        private MonoBehaviour[] behaviours;

        private void Awake()
        {
            if (!TryGetComponent<Collider>(out _))
            {
                Debug.LogError($"{name}: Add a Collider on the same object as ProjectileInteractionTarget.", this);
                enabled = false;
                return;
            }
            behaviours = GetComponents<MonoBehaviour>();
        }

        public void Dispatch(Projectile projectile, ProjectileHitContext context)
        {
            if (!isActiveAndEnabled) return;
            // Inspector component order is interaction order. Finish stops dispatch.
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (projectile.State != ProjectileState.Flying) break;
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.isActiveAndEnabled &&
                    behaviour is IProjectileInteractable interaction)
                    interaction.OnProjectileHit(projectile, context);
            }
        }
    }
}
