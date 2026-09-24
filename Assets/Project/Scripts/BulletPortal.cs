using System.Collections.Generic;
using UnityEngine;

namespace DreamForgeTD
{
    public class BulletPortal : MonoBehaviour, IBulletMechanic, IBulletTrajectoryRule
    {
        private readonly HashSet<Rigidbody> bulletsToIgnoreUntilExit = new HashSet<Rigidbody>();
        private BulletPortalPair portalPair;

        private void Awake()
        {
            Collider portalCollider = GetComponent<Collider>();
            if (portalCollider == null)
            {
                Debug.LogError("BulletPortal needs a Collider on the same GameObject.", this);
                enabled = false;
                return;
            }

            portalCollider.isTrigger = true;
        }

        public void SetPortalPair(BulletPortalPair pair)
        {
            portalPair = pair;
        }

        public void IgnoreBulletUntilExit(Rigidbody body)
        {
            if (body != null)
                bulletsToIgnoreUntilExit.Add(body);
        }

        public void OnBulletHit(BulletHitContext hit)
        {
            if (portalPair != null && hit.Body != null && !bulletsToIgnoreUntilExit.Contains(hit.Body))
            {
                portalPair.HandlePortalHit(this, hit.Body, hit.IncomingVelocity);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body != null)
                bulletsToIgnoreUntilExit.Remove(body);
        }

        public BulletTrajectoryResponse PredictTrajectory(BulletTrajectoryHit hit)
        {
            if (portalPair == null || !portalPair.TryGetPredictedExit(
                    this,
                    hit.BulletPosition,
                    hit.IncomingVelocity,
                    out Vector3 exitPosition,
                    out Vector3 exitVelocity,
                    out _))
            {
                return BulletTrajectoryResponse.Stop(hit.BulletPosition);
            }

            return BulletTrajectoryResponse.Teleport(exitPosition, exitVelocity);
        }
    }
}
