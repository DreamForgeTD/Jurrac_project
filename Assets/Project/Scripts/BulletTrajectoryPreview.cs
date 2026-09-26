using System.Collections.Generic;
using UnityEngine;

namespace DreamForgeTD
{
    [DefaultExecutionOrder(100)]
    public sealed class BulletTrajectoryPreview : MonoBehaviour
    {
        [Header("Shot")]
        [SerializeField] private Bullet bulletPrefab;
        [SerializeField] private Transform firePoint;

        [Header("Simulation")]
        [SerializeField, Min(0.1f)] private float maxFlightTime = 3.5f;
        [SerializeField, Min(0.005f)] private float simulationStep = 0.02f;
        [SerializeField, Min(0)] private int maxInteractions = 8;
        [SerializeField, Min(0f)] private float collisionSkin = 0.015f;
        [SerializeField] private int collisionMask = Physics.DefaultRaycastLayers;

        [Header("Appearance")]
        [SerializeField] private Sprite trajectoryDotSprite;
        [SerializeField] private Material trajectoryDotMaterial;
        [SerializeField, Min(0.05f)] private float dotSpacing = 0.12f;
        [SerializeField, Min(0.05f)] private float maxDotSpacing = 0.36f;
        [SerializeField, Min(0.01f)] private float dotSize = 0.12f;
        [SerializeField, Min(1)] private int maxDots = 48;
        [SerializeField, Min(0f)] private float previewCameraOffset = 0.12f;
        [SerializeField] private Color dotColor = new Color(1f, 0.82f, 0.08f, 1f);

        private readonly List<TrajectoryPoint> trajectoryPoints = new List<TrajectoryPoint>(256);

        private Rigidbody bulletBody;
        private CannonController cannonController;
        private CannonShooter cannonShooter;
        private BulletTrajectorySimulator simulator;
        private TrajectoryDotRenderer dotRenderer;
        private Camera previewCamera;
        private float bulletRadius;
        private bool hasLockedTrajectory;
        private int lastObservedShotSequence;

        private void Awake()
        {
            dotSpacing = Mathf.Max(0.05f, dotSpacing);
            maxDotSpacing = Mathf.Max(dotSpacing, maxDotSpacing);
            dotSize = Mathf.Max(0.01f, dotSize);
            maxDots = Mathf.Clamp(maxDots, 1, 128);

            if (!ValidateShotReferences())
            {
                enabled = false;
                return;
            }

            if (!ValidateVisualReferences())
            {
                enabled = false;
                return;
            }

            Vector3 scale = bulletPrefab.transform.lossyScale;
            bulletRadius = bulletPrefab.GetComponent<SphereCollider>().radius *
                Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            cannonController = GetComponent<CannonController>();
            cannonShooter = GetComponent<CannonShooter>();
            previewCamera = Camera.main;
            lastObservedShotSequence = cannonShooter != null ? cannonShooter.ShotSequence : 0;

            simulator = new BulletTrajectorySimulator(bulletBody, bulletRadius, transform);
            dotRenderer = new TrajectoryDotRenderer(transform, trajectoryDotSprite, trajectoryDotMaterial, maxDots);
        }

        private void LateUpdate()
        {
            if (cannonController != null && cannonController.IsPulling)
            {
                hasLockedTrajectory = false;
                if (cannonShooter != null)
                    lastObservedShotSequence = cannonShooter.ShotSequence;

                DrawTrajectory(cannonController.CurrentLaunchForce);
                return;
            }

            if (cannonShooter != null && cannonShooter.ShotSequence != lastObservedShotSequence)
            {
                lastObservedShotSequence = cannonShooter.ShotSequence;
                DrawTrajectory(cannonShooter.LastShotImpulse,
                    cannonShooter.LastShotDirection,
                    cannonShooter.LastShotPosition);
                hasLockedTrajectory = true;
                return;
            }

            if (cannonShooter != null && cannonShooter.HasPendingShot)
            {
                DrawTrajectory(cannonShooter.PendingShotImpulse,
                    cannonShooter.PendingShotDirection,
                    cannonShooter.PendingShotPosition);
                hasLockedTrajectory = true;
                return;
            }

            if (!hasLockedTrajectory)
                DrawTrajectory(bulletPrefab.LaunchImpulse);
        }

        private bool ValidateShotReferences()
        {
            if (bulletPrefab == null)
            {
                Debug.LogError("BulletTrajectoryPreview needs the Bullet prefab assigned.", this);
                return false;
            }

            if (firePoint == null)
                firePoint = transform.Find("FirePoint");

            if (firePoint == null)
            {
                Debug.LogError("BulletTrajectoryPreview needs a FirePoint transform.", this);
                return false;
            }

            bulletBody = bulletPrefab.GetComponent<Rigidbody>();
            if (bulletBody == null)
            {
                Debug.LogError("The assigned Bullet prefab needs a Rigidbody on its root.", bulletPrefab);
                return false;
            }

            if (bulletPrefab.GetComponent<SphereCollider>() == null)
            {
                Debug.LogError("The assigned Bullet prefab needs a root SphereCollider for trajectory prediction.", bulletPrefab);
                return false;
            }

            return true;
        }

        private bool ValidateVisualReferences()
        {
            if (trajectoryDotSprite == null)
            {
                Debug.LogError("Assign the trajectory dot Sprite in the Inspector.", this);
                return false;
            }

            if (trajectoryDotMaterial == null)
            {
                Debug.LogError("Assign the trajectory dot Material in the Inspector.", this);
                return false;
            }

            return true;
        }

        private void DrawTrajectory(float impulse)
        {
            Vector3 position = cannonShooter != null
                ? cannonShooter.GetMuzzleSpawnPosition()
                : firePoint.position;
            DrawTrajectory(impulse, firePoint.up, position);
        }

        private void DrawTrajectory(float impulse, Vector3 initialDirection, Vector3 startPosition)
        {
            if (bulletBody == null || bulletBody.mass <= 0f)
            {
                dotRenderer.Clear();
                return;
            }

            float forceRatio = cannonController != null
                ? cannonController.GetLaunchForceRatio(impulse)
                : Mathf.Clamp01(impulse / Mathf.Max(0.01f, bulletPrefab.LaunchImpulse));
            float spacing = Mathf.Lerp(dotSpacing, maxDotSpacing, forceRatio);

            simulator.Simulate(
                startPosition,
                initialDirection,
                impulse,
                maxFlightTime,
                simulationStep,
                maxInteractions,
                collisionSkin,
                collisionMask,
                trajectoryPoints);

            Vector3 cameraOffset = previewCamera != null
                ? -previewCamera.transform.forward * previewCameraOffset
                : Vector3.back * previewCameraOffset;
            dotRenderer.Draw(trajectoryPoints, spacing, dotColor, dotSize, cameraOffset);
        }

        private void OnDisable()
        {
            if (dotRenderer != null)
                dotRenderer.Clear();
        }

        private void OnDestroy()
        {
            if (dotRenderer != null)
                dotRenderer.Dispose();
        }
    }
}
