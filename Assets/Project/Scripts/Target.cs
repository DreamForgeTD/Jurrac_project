using System;
using UnityEngine;

namespace DreamForgeTD
{
    public class Target : MonoBehaviour, IBulletMechanic, IBulletTrajectoryRule
    {
        private bool hasWon;

        public event Action<Target> Defeated;

        private void Awake()
        {
            Collider targetCollider = GetComponent<Collider>();
            if (targetCollider == null)
            {
                Debug.LogError("Target needs a Collider on the same GameObject.", this);
                enabled = false;
                return;
            }

            targetCollider.isTrigger = true;
        }

        public void OnBulletHit(BulletHitContext hit)
        {
            if (hasWon || hit.Body == null)
            {
                return;
            }

            hasWon = true;
            GameVfx.PlayTargetVictory(transform.position, transform.up);
            GameAudio.PlayTargetWin(transform.position);

            bool hasDefeatListener = Defeated != null;
            Defeated?.Invoke(this);
            if (!hasDefeatListener)
            {
                // Preserve the standalone target behavior when no level flow is present.
                Debug.Log("YOU WIN!", this);
                Time.timeScale = 0f;
            }
            Destroy(hit.Body.gameObject);
        }

        public BulletTrajectoryResponse PredictTrajectory(BulletTrajectoryHit hit)
        {
            return BulletTrajectoryResponse.Stop(hit.BulletPosition);
        }
    }
}
