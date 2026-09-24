using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        [SerializeField, Min(0.001f)] private float lineWidth = 0.045f;
        [SerializeField] private Color lineColor = new Color(0.25f, 0.9f, 1f, 0.95f);

        private readonly List<LineRenderer> lineSegments = new List<LineRenderer>(2);
        private readonly List<MonoBehaviour> hitComponents = new List<MonoBehaviour>(4);
        private readonly RaycastHit[] castHits = new RaycastHit[32];

        private Rigidbody bulletBody;
        private SphereCollider bulletCollider;
        private Material lineMaterial;
        private int activeSegmentIndex;
        private LineRenderer activeLine;
        private float bulletRadius;

        private void Awake()
        {
            if (bulletPrefab == null)
            {
                Debug.LogError("BulletTrajectoryPreview needs the Bullet prefab assigned.", this);
                enabled = false;
                return;
            }

            if (firePoint == null)
                firePoint = transform.Find("FirePoint");

            if (firePoint == null)
            {
                Debug.LogError("BulletTrajectoryPreview needs a FirePoint transform.", this);
                enabled = false;
                return;
            }

            bulletBody = bulletPrefab.GetComponent<Rigidbody>();
            bulletCollider = bulletPrefab.GetComponent<SphereCollider>();
            if (bulletBody == null)
            {
                Debug.LogError("The assigned Bullet prefab needs a Rigidbody on its root.", bulletPrefab);
                enabled = false;
                return;
            }

            if (bulletCollider == null)
            {
                Debug.LogError("The assigned Bullet prefab needs a root SphereCollider for trajectory prediction.", bulletPrefab);
                enabled = false;
                return;
            }

            Vector3 scale = bulletPrefab.transform.lossyScale;
            bulletRadius = bulletCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            CreateLineMaterial();
            if (lineMaterial == null)
            {
                enabled = false;
                return;
            }

            CreateLineSegment();
        }

        private void LateUpdate()
        {
            DrawTrajectory();
        }

        private void OnDisable()
        {
            ClearLineSegments();
        }

        private void OnDestroy()
        {
            if (lineMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(lineMaterial);
            else
                DestroyImmediate(lineMaterial);
        }

        private void DrawTrajectory()
        {
            if (firePoint == null || bulletPrefab == null || bulletBody == null || bulletBody.mass <= 0f)
            {
                ClearLineSegments();
                return;
            }

            ClearLineSegments();
            BeginLineSegment();

            Vector3 position = firePoint.position;
            Vector3 initialDirection = firePoint.up;
            Vector3 velocity = initialDirection * (bulletPrefab.LaunchImpulse / bulletBody.mass);
            Vector3 gravity = bulletBody.useGravity ? Physics.gravity : Vector3.zero;
            ApplyPositionConstraints(ref velocity, bulletBody.constraints);
            ApplyPositionConstraints(ref gravity, bulletBody.constraints);
            AddLinePoint(position);

            float elapsed = 0f;
            int interactionCount = 0;
            int maxSteps = Mathf.CeilToInt(maxFlightTime / simulationStep);

            for (int stepIndex = 0; stepIndex < maxSteps && elapsed < maxFlightTime; stepIndex++)
            {
                float deltaTime = Mathf.Min(simulationStep, maxFlightTime - elapsed);
                Vector3 nextVelocity = velocity + gravity * deltaTime;
                ApplyPositionConstraints(ref nextVelocity, bulletBody.constraints);
                Vector3 nextPosition = position + nextVelocity * deltaTime;
                Vector3 movement = nextPosition - position;
                float distance = movement.magnitude;

                if (distance > 0.0001f && TryGetNearestHit(position, movement / distance, distance, out RaycastHit hit))
                {
                    Vector3 hitPosition = position + movement.normalized * hit.distance;
                    AddLinePoint(hitPosition);

                    if (interactionCount >= maxInteractions || !TryGetTrajectoryRule(hit.collider, out IBulletTrajectoryRule rule))
                        break;

                    BulletTrajectoryHit trajectoryHit = new BulletTrajectoryHit(
                        hit.collider,
                        hitPosition,
                        hit.point,
                        hit.normal,
                        nextVelocity,
                        collisionSkin);
                    BulletTrajectoryResponse response = rule.PredictTrajectory(trajectoryHit);
                    interactionCount++;
                    elapsed += deltaTime;

                    if (response.Type == BulletTrajectoryResponseType.Stop)
                        break;

                    position = response.Position;
                    velocity = response.Velocity;
                    ApplyPositionConstraints(ref velocity, bulletBody.constraints);

                    if (response.Type == BulletTrajectoryResponseType.Teleport)
                    {
                        BeginLineSegment();
                        AddLinePoint(position);
                    }

                    continue;
                }

                position = nextPosition;
                velocity = nextVelocity;
                elapsed += deltaTime;
                AddLinePoint(position);
            }
        }

        private bool TryGetNearestHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearestHit)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                bulletRadius,
                direction,
                castHits,
                distance,
                collisionMask,
                QueryTriggerInteraction.Collide);

            bool foundHit = false;
            float nearestDistance = float.PositiveInfinity;
            nearestHit = default;

            for (int i = 0; i < hitCount; i++)
            {
                Collider candidate = castHits[i].collider;
                if (candidate == null || IsOwnedByCannon(candidate) || candidate.GetComponentInParent<Bullet>() != null)
                    continue;

                if (castHits[i].distance < nearestDistance)
                {
                    nearestHit = castHits[i];
                    nearestDistance = castHits[i].distance;
                    foundHit = true;
                }
            }

            return foundHit;
        }

        private bool IsOwnedByCannon(Collider candidate)
        {
            Transform candidateTransform = candidate.transform;
            return candidateTransform == transform || candidateTransform.IsChildOf(transform);
        }

        private bool TryGetTrajectoryRule(Collider collider, out IBulletTrajectoryRule rule)
        {
            hitComponents.Clear();
            collider.GetComponents(hitComponents);

            for (int i = 0; i < hitComponents.Count; i++)
            {
                MonoBehaviour behaviour = hitComponents[i];
                if (behaviour != null && behaviour.isActiveAndEnabled && behaviour is IBulletTrajectoryRule trajectoryRule)
                {
                    rule = trajectoryRule;
                    return true;
                }
            }

            rule = null;
            return false;
        }

        private void BeginLineSegment()
        {
            activeSegmentIndex = lineSegments.Count == 0 ? 0 : activeSegmentIndex + 1;
            while (lineSegments.Count <= activeSegmentIndex)
                CreateLineSegment();

            activeLine = lineSegments[activeSegmentIndex];
            activeLine.positionCount = 0;
        }

        private void AddLinePoint(Vector3 point)
        {
            if (activeLine == null)
                return;

            int pointIndex = activeLine.positionCount;
            activeLine.positionCount = pointIndex + 1;
            activeLine.SetPosition(pointIndex, point);
        }

        private void ClearLineSegments()
        {
            activeLine = null;
            activeSegmentIndex = -1;

            for (int i = 0; i < lineSegments.Count; i++)
            {
                if (lineSegments[i] != null)
                    lineSegments[i].positionCount = 0;
            }
        }

        private void CreateLineSegment()
        {
            GameObject segmentObject = new GameObject("Trajectory Preview Segment");
            segmentObject.layer = 2;
            segmentObject.transform.SetParent(transform, false);

            LineRenderer line = segmentObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = lineWidth;
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 10;
            line.sharedMaterial = lineMaterial;
            line.positionCount = 0;
            lineSegments.Add(line);
        }

        private void CreateLineMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogError("No supported shader was found for the trajectory preview line.", this);
                return;
            }

            lineMaterial = new Material(shader)
            {
                name = "Runtime Bullet Trajectory Preview"
            };
            lineMaterial.color = lineColor;
            if (lineMaterial.HasProperty("_BaseColor"))
                lineMaterial.SetColor("_BaseColor", lineColor);
        }

        private static void ApplyPositionConstraints(ref Vector3 vector, RigidbodyConstraints constraints)
        {
            if ((constraints & RigidbodyConstraints.FreezePositionX) != 0)
                vector.x = 0f;
            if ((constraints & RigidbodyConstraints.FreezePositionY) != 0)
                vector.y = 0f;
            if ((constraints & RigidbodyConstraints.FreezePositionZ) != 0)
                vector.z = 0f;
        }
    }
}
