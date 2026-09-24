using System;
using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    [RequireComponent(typeof(ProjectileInteractionTarget))]
    public sealed class Switch : MonoBehaviour, IProjectileInteractable, ILevelResettable
    {
        [SerializeField] private Gate targetGate;
        public bool IsActivated { get; private set; }
        public event Action Activated;

        private void Awake()
        {
            if (targetGate != null && targetGate.IsConfigured) return;
            Debug.LogError($"{name}: Assign a configured Gate to Target Gate.", this);
            enabled = false;
        }

        public void OnProjectileHit(Projectile projectile, ProjectileHitContext context)
        {
            if (IsActivated) return;
            IsActivated = true;
            targetGate.Open();
            Activated?.Invoke();
        }

        // Gate restores its own initial state. Reset order does not matter.
        public void ResetState() => IsActivated = false;
    }
}
