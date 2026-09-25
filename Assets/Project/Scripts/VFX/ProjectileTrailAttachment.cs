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

        private void OnDestroy()
        {
            if (trail == null)
                return;

            trail.transform.SetParent(null, true);
            GameVfx.Stop(trail);
        }
    }
}
