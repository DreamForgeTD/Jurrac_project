using System;
using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float maxSpeed = 25f;
        [SerializeField, Min(0.1f)] private float lifetime = 10f;
        [SerializeField, Min(0f)] private float stoppedSpeed = 0.1f;
        [SerializeField, Min(0.1f)] private float stoppedDuration = 1f;
        [Tooltip("World-space bounds; authored per level.")]
        [SerializeField] private Bounds playBounds = new Bounds(Vector3.zero, new Vector3(30f, 50f, 4f));

        private Rigidbody body;
        private float elapsed;
        private float stoppedTime;
        private int teleportedFrame = -1;
        public Rigidbody Rigidbody => body;
        public ProjectileState State { get; private set; } = ProjectileState.Ready;
        public float MaxSpeed => maxSpeed;
        public event Action Finished;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        public void Prepare(Vector3 position, Quaternion rotation)
        {
            if (body == null) return;
            StopBody();
            body.position = position;
            body.rotation = rotation;
            elapsed = 0f;
            stoppedTime = 0f;
            teleportedFrame = -1;
            State = ProjectileState.Ready;
        }

        public bool Launch(Vector3 velocity)
        {
            if (!isActiveAndEnabled || body == null || State != ProjectileState.Ready) return false;
            State = ProjectileState.Flying;
            body.isKinematic = false;
            body.detectCollisions = true;
            SetVelocity(velocity);
            body.WakeUp();
            return true;
        }

        public void MultiplyVelocity(float multiplier) => SetVelocity(body.linearVelocity * Mathf.Max(0f, multiplier));

        public void SetVelocity(Vector3 velocity)
        {
            if (State != ProjectileState.Flying) return;
            body.linearVelocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        }

        public void Teleport(Vector3 position)
        {
            if (State != ProjectileState.Flying || teleportedFrame == Time.frameCount) return;
            body.position = position;
            teleportedFrame = Time.frameCount;
            stoppedTime = 0f;
        }

        public void Finish()
        {
            if (State != ProjectileState.Flying) return;
            State = ProjectileState.Finished;
            StopBody();
            Finished?.Invoke();
        }

        private void StopBody()
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        private void FixedUpdate()
        {
            if (State != ProjectileState.Flying) return;
            SetVelocity(body.linearVelocity);
            elapsed += Time.fixedDeltaTime;
            stoppedTime = body.linearVelocity.sqrMagnitude <= stoppedSpeed * stoppedSpeed
                ? stoppedTime + Time.fixedDeltaTime : 0f;
            if (elapsed >= lifetime || stoppedTime >= stoppedDuration || !playBounds.Contains(body.position)) Finish();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (State != ProjectileState.Flying || collision.contactCount == 0) return;
            ContactPoint contact = collision.GetContact(0);
            Dispatch(collision.collider, contact.point, contact.normal);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (State != ProjectileState.Flying) return;
            // A trigger has no physical contact normal.
            Dispatch(other, body.position, Vector3.zero);
        }

        private void Dispatch(Collider other, Vector3 point, Vector3 normal)
        {
            // Ignore callbacks remaining at the old position after a teleport.
            if (teleportedFrame == Time.frameCount) return;
            if (other.TryGetComponent(out ProjectileInteractionTarget target))
                target.Dispatch(this, new ProjectileHitContext(point, normal, other));
            if (State == ProjectileState.Flying) SetVelocity(body.linearVelocity);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(playBounds.center, playBounds.size);
        }
    }
}
