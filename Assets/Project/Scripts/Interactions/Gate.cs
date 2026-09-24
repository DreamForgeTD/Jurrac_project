using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    public sealed class Gate : MonoBehaviour, ILevelResettable
    {
        [SerializeField] private Collider blockingCollider;
        [SerializeField] private Renderer gateVisual;
        [SerializeField] private bool initiallyOpen;
        public bool IsConfigured => blockingCollider != null && gateVisual != null;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (!IsConfigured)
            {
                Debug.LogError($"{name}: Assign Blocking Collider and Gate Visual.", this);
                enabled = false;
                return;
            }
            ResetState();
        }

        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);
        public void ResetState() => SetOpen(initiallyOpen);

        private void SetOpen(bool open)
        {
            if (!IsConfigured) return;
            IsOpen = open;
            blockingCollider.enabled = !open;
            gateVisual.enabled = !open;
        }
    }
}
