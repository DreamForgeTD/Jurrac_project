using UnityEngine;

namespace DreamForgeTD
{
    public sealed class CannonAnimationEventRelay : MonoBehaviour
    {
        [SerializeField] private CannonShooter cannonShooter;

        private void Awake()
        {
            if (cannonShooter == null)
                cannonShooter = GetComponentInParent<CannonShooter>();
        }

        public void Bind(CannonShooter shooter)
        {
            cannonShooter = shooter;
        }

        public void OnCannonFire()
        {
            if (cannonShooter != null)
                cannonShooter.OnFiringAnimationEvent();
        }
    }
}
