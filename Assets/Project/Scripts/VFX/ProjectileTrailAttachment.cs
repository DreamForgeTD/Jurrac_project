using UnityEngine;

namespace DreamForgeTD
{
    [DisallowMultipleComponent]
    public sealed class ProjectileTrailAttachment : MonoBehaviour
    {
        private GameObject trail;

        public void Track(GameObject trailInstance)
        {
            trail = trailInstance;
        }

        public void ClearImmediately()
        {
            if (trail == null)
                return;

            GameVfx.StopAndClear(trail);
            trail = null;
        }

        private void OnDisable()
        {
            if (trail == null)
                return;

            GameVfx.Stop(trail);
            trail = null;
        }
    }
}
