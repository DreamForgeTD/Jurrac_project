using UnityEngine;

namespace DreamForgeTD
{
    [DisallowMultipleComponent]
    public sealed class MagnetForceField : MonoBehaviour, IBulletForceField
    {
        [SerializeField, Min(0.1f)] private float attractionRadius = 2.5f;
        [SerializeField, Min(0f)] private float maxAcceleration = 18f;
        [SerializeField, Min(0.1f)] private float falloffExponent = 1f;
        [SerializeField] private bool constrainToXY = true;

        public void AlignVisualToFieldCenter()
        {
            if (transform.childCount != 1)
                return;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds visualBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                visualBounds.Encapsulate(renderers[i].bounds);

            Vector3 offset = transform.position - visualBounds.center;
            offset.z = 0f;
            transform.GetChild(0).position += offset;
        }

        private void OnEnable()
        {
            BulletForceFieldRegistry.Register(this);
        }

        private void OnDisable()
        {
            BulletForceFieldRegistry.Unregister(this);
        }

        public Vector3 GetAcceleration(BulletMotionSample bullet)
        {
            if (attractionRadius <= 0f)
                return Vector3.zero;

            Vector3 toMagnet = transform.position - bullet.Position;
            if (constrainToXY)
                toMagnet = Vector3.ProjectOnPlane(toMagnet, Vector3.forward);

            float distanceSquared = toMagnet.sqrMagnitude;
            float radiusSquared = attractionRadius * attractionRadius;
            if (distanceSquared <= 0.000001f || distanceSquared >= radiusSquared)
                return Vector3.zero;

            float distance = Mathf.Sqrt(distanceSquared);
            float proximity = 1f - distance / attractionRadius;
            float smoothFalloff = proximity * proximity * (3f - 2f * proximity);
            float strength = maxAcceleration * Mathf.Pow(smoothFalloff, falloffExponent);
            return toMagnet / distance * strength;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 0.9f, 1f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, attractionRadius));
        }
    }
}
