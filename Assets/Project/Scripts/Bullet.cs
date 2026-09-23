using UnityEngine;

namespace DreamForgeTD
{
    public class Bullet : MonoBehaviour
    {

        [Header("Settings")]
        [Tooltip("Tốc độ bay của viên đạn")]
        [SerializeField] private float speed = 25f;

        [Tooltip("Thời gian tồn tại tối đa trước khi tự hủy (giây)")]
        [SerializeField] private float lifeTime = 4f;
        private Rigidbody rb;
        private Vector3 velocityBeforePhysics;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            Destroy(gameObject, lifeTime);

            if (rb == null)
            {
                Debug.LogError("Bullet prefab needs a Rigidbody component.", this);
                return;
            }

            // Cannon aims in the XY plane by rotating around Z, so its barrel points along local Y.
            rb.useGravity = false;
            rb.linearVelocity = transform.up * speed;
        }

        public void TeleportTo(Transform exit, float exitOffset)
        {
            if (rb == null || exit == null)
            {
                return;
            }

            // Endpoint chỉ làm mốc vị trí: giữ nguyên hướng và tốc độ của viên đạn.
            Vector3 velocity = rb.linearVelocity;
            Vector3 travelDirection = velocity.sqrMagnitude > 0.0001f
                ? velocity.normalized
                : transform.up;
            Vector3 spawnPosition = exit.position + travelDirection * exitOffset;

            // Di chuyển chính viên đạn hiện tại để tránh Instantiate/Destroy khi qua cổng.
            rb.position = spawnPosition;
            rb.linearVelocity = velocity;
            velocityBeforePhysics = velocity;
        }

        private void FixedUpdate()
        {
            if (rb != null)
            {
                velocityBeforePhysics = rb.linearVelocity;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.GetComponentInParent<BounceSurface>() == null || collision.contactCount == 0)
            {
                return;
            }

            Vector3 incomingVelocity = velocityBeforePhysics.sqrMagnitude > 0f
                ? velocityBeforePhysics
                : rb.linearVelocity;

            Vector3 normal = collision.GetContact(0).normal;
            rb.linearVelocity = Vector3.Reflect(incomingVelocity, normal);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<Target>() != null)
            {
                Destroy(gameObject);
            }
        }
    }
}
