using System.Collections.Generic;
using UnityEngine;

namespace DreamForgeTD
{
    internal sealed class BulletTrajectorySimulator
    {
        private readonly Rigidbody bulletBody;
        private readonly float bulletRadius;
        private readonly Transform cannonRoot;
        private readonly RaycastHit[] castHits = new RaycastHit[32];
        private readonly List<MonoBehaviour> hitComponents = new List<MonoBehaviour>(4);

        public BulletTrajectorySimulator(Rigidbody bulletBody, float bulletRadius, Transform cannonRoot)
        {
            this.bulletBody = bulletBody;
            this.bulletRadius = bulletRadius;
            this.cannonRoot = cannonRoot;
        }

        public void Simulate(
            Vector3 startPosition,
            Vector3 initialDirection,
            float impulse,
            float maxFlightTime,
            float simulationStep,
            int maxInteractions,
            float collisionSkin,
            int collisionMask,
            List<TrajectoryPoint> points)
        {
            points.Clear();
            if (bulletBody == null || bulletBody.mass <= 0f)
                return;

            simulationStep = Mathf.Max(0.005f, simulationStep);
            maxFlightTime = Mathf.Max(simulationStep, maxFlightTime);
            initialDirection = initialDirection.sqrMagnitude > 0.0001f
                ? initialDirection.normalized
                : Vector3.up;

            RigidbodyConstraints constraints = bulletBody.constraints;
            Vector3 position = startPosition;
            Vector3 velocity = initialDirection * (impulse / bulletBody.mass);
            Vector3 gravity = bulletBody.useGravity ? Physics.gravity : Vector3.zero;
            ApplyPositionConstraints(ref velocity, constraints);
            ApplyPositionConstraints(ref gravity, constraints);
            points.Add(new TrajectoryPoint(position, true));

            float elapsed = 0f;
            int interactionCount = 0;
            int maxSteps = Mathf.CeilToInt(maxFlightTime / simulationStep);

            for (int stepIndex = 0; stepIndex < maxSteps && elapsed < maxFlightTime; stepIndex++)
            {
                float deltaTime = Mathf.Min(simulationStep, maxFlightTime - elapsed);
                BulletMotionSample motionSample = new BulletMotionSample(position, velocity);
                Vector3 fieldAcceleration = BulletForceFieldRegistry.GetCombinedAcceleration(motionSample);
                ApplyPositionConstraints(ref fieldAcceleration, constraints);
                Vector3 nextVelocity = velocity + (gravity + fieldAcceleration) * deltaTime;
                ApplyPositionConstraints(ref nextVelocity, constraints);
                Vector3 nextPosition = position + nextVelocity * deltaTime;
                Vector3 movement = nextPosition - position;
                float distance = movement.magnitude;

                if (distance > 0.0001f && TryGetNearestHit(position, movement / distance, distance, collisionMask, out RaycastHit hit))
                {
                    Vector3 hitPosition = position + movement * (hit.distance / distance);
                    points.Add(new TrajectoryPoint(hitPosition, false));

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
                    ApplyPositionConstraints(ref velocity, constraints);

                    if (response.Type == BulletTrajectoryResponseType.Teleport)
                        points.Add(new TrajectoryPoint(position, true));

                    continue;
                }

                position = nextPosition;
                velocity = nextVelocity;
                elapsed += deltaTime;
                points.Add(new TrajectoryPoint(position, false));
            }
        }

        private bool TryGetNearestHit(
            Vector3 origin,
            Vector3 direction,
            float distance,
            int collisionMask,
            out RaycastHit nearestHit)
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
            return candidateTransform == cannonRoot || candidateTransform.IsChildOf(cannonRoot);
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

    internal readonly struct TrajectoryPoint
    {
        public Vector3 Position { get; }
        public bool StartsNewSegment { get; }

        public TrajectoryPoint(Vector3 position, bool startsNewSegment)
        {
            Position = position;
            StartsNewSegment = startsNewSegment;
        }
    }
}
