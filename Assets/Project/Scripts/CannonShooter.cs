using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DreamForgeTD
{
    public class CannonShooter : MonoBehaviour
    {
        [Header("Ammo")]
        [SerializeField, Min(0)] private int startingBulletCount = 20;

        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float fireRate = 0.3f;
        [SerializeField] private Animator firingAnimator;
        [SerializeField] private string fireTriggerName = "Fire";

        private float nextFireTime;
        private CannonController cannonController;
        private Collider[] sourceColliders;
        private float bulletRadius;
        private GameObject chargeVfx;
        private int remainingBulletCount;

        public int ShotSequence { get; private set; }
        public int RemainingBulletCount => remainingBulletCount;
        public int StartingBulletCount => Mathf.Max(0, startingBulletCount);
        public event Action<int, int> BulletCountChanged;
        public Vector3 LastShotPosition { get; private set; }
        public Vector3 LastShotDirection { get; private set; }
        public float LastShotImpulse { get; private set; }
        public bool HasPendingShot { get; private set; }
        public Vector3 PendingShotPosition { get; private set; }
        public Vector3 PendingShotDirection { get; private set; }
        public float PendingShotImpulse { get; private set; }

        private void Awake()
        {
            remainingBulletCount = StartingBulletCount;
            cannonController = GetComponent<CannonController>();
            sourceColliders = GetComponentsInChildren<Collider>();
            bulletRadius = GetBulletRadius();
            if (firingAnimator == null)
                firingAnimator = GetComponentInChildren<Animator>(true);
        }

        private void Update()
        {
            if (cannonController != null && cannonController.IsPulling)
            {
                if (chargeVfx == null)
                    chargeVfx = GameVfx.AttachCannonCharge(transform);

                if (chargeVfx != null)
                    GameVfx.SetChargeIntensity(chargeVfx, cannonController.PullRatio);

                return;
            }

            StopChargeVfx();
            if (bulletPrefab == null || firePoint == null) return;
            if (!IsHoldingSpace() || Time.time < nextFireTime) return;

            Shoot();
        }

        private bool IsHoldingSpace()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#else
            return Input.GetKey(KeyCode.Space);
#endif
        }

        public void Shoot()
        {
            if (bulletPrefab == null || firePoint == null) return;
            Bullet bullet = bulletPrefab.GetComponent<Bullet>();
            RequestShotWithForce(bullet != null ? bullet.LaunchImpulse : 0f);
        }

        public void RequestShotWithForce(float force)
        {
            if (bulletPrefab == null || firePoint == null || remainingBulletCount <= 0 ||
                HasPendingShot || Time.time < nextFireTime) return;

            PendingShotImpulse = Mathf.Max(0f, force);
            PendingShotPosition = GetMuzzleSpawnPosition();
            PendingShotDirection = firePoint.up.normalized;
            HasPendingShot = true;

            if (firingAnimator == null || firingAnimator.runtimeAnimatorController == null)
            {
                OnFiringAnimationEvent();
                return;
            }

            firingAnimator.ResetTrigger(fireTriggerName);
            firingAnimator.SetTrigger(fireTriggerName);
        }

        public void OnFiringAnimationEvent()
        {
            if (!HasPendingShot)
                return;

            float force = PendingShotImpulse;
            HasPendingShot = false;
            ShootWithForce(force);
        }

        public void ShootWithForce(float force)
        {
            if (bulletPrefab == null || firePoint == null || remainingBulletCount <= 0 ||
                Time.time < nextFireTime) return;

            Vector3 spawnPosition = GetMuzzleSpawnPosition();
            GameObject bulletObj = Instantiate(bulletPrefab, spawnPosition, firePoint.rotation);
            GameObject trailVfx = GameVfx.AttachProjectileTrail(bulletObj.transform);
            if (trailVfx != null)
            {
                ProjectileTrailAttachment trailAttachment = bulletObj.GetComponent<ProjectileTrailAttachment>();
                if (trailAttachment == null)
                    trailAttachment = bulletObj.AddComponent<ProjectileTrailAttachment>();
                trailAttachment.Track(trailVfx);
            }

            if (bulletObj.TryGetComponent(out Bullet bullet))
            {
                bullet.SetLaunchImpulse(force);
            }
            remainingBulletCount--;
            BulletCountChanged?.Invoke(remainingBulletCount, StartingBulletCount);
            RecordShot(spawnPosition, force);
            GameVfx.PlayCannonMuzzle(firePoint.position, firePoint.up);
            GameAudio.PlayCannonShot(firePoint.position);
            nextFireTime = Time.time + fireRate;
        }

        public void ResetAmmoForLevel()
        {
            remainingBulletCount = StartingBulletCount;
            nextFireTime = Time.time;
            HasPendingShot = false;
            BulletCountChanged?.Invoke(remainingBulletCount, StartingBulletCount);
        }

        private void RecordShot(Vector3 spawnPosition, float impulse)
        {
            LastShotPosition = spawnPosition;
            LastShotDirection = firePoint.up.normalized;
            LastShotImpulse = Mathf.Max(0f, impulse);
            ShotSequence++;
        }

        private void OnDisable()
        {
            HasPendingShot = false;
            StopChargeVfx();
        }

        private void StopChargeVfx()
        {
            if (chargeVfx == null)
                return;

            GameVfx.Stop(chargeVfx);
            chargeVfx = null;
        }

        public Vector3 GetMuzzleSpawnPosition()
        {
            if (firePoint == null)
                return transform.position;

            Vector3 origin = firePoint.position;
            Vector3 direction = firePoint.up.normalized;
            float furthestColliderProjection = 0f;

            for (int i = 0; sourceColliders != null && i < sourceColliders.Length; i++)
            {
                Collider sourceCollider = sourceColliders[i];
                if (sourceCollider == null || !sourceCollider.enabled || sourceCollider.isTrigger ||
                    !sourceCollider.gameObject.activeInHierarchy)
                    continue;

                Bounds bounds = sourceCollider.bounds;
                Vector3 extents = bounds.extents;
                Vector3 centerOffset = bounds.center - origin;
                float projectedExtent = Mathf.Abs(direction.x) * extents.x +
                    Mathf.Abs(direction.y) * extents.y + Mathf.Abs(direction.z) * extents.z;
                furthestColliderProjection = Mathf.Max(furthestColliderProjection,
                    Vector3.Dot(centerOffset, direction) + projectedExtent);
            }

            return origin + direction * Mathf.Max(0f, furthestColliderProjection + bulletRadius + 0.03f);
        }

        private float GetBulletRadius()
        {
            if (bulletPrefab == null)
                return 0f;

            SphereCollider sphere = bulletPrefab.GetComponent<SphereCollider>();
            if (sphere == null)
                return 0f;

            Vector3 scale = bulletPrefab.transform.lossyScale;
            return sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }
    }
}
