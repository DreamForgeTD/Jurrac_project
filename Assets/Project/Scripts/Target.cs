using UnityEngine;

namespace DreamForgeTD
{
    public class Target : MonoBehaviour
    {
        private bool hasWon;

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

        private void OnTriggerEnter(Collider other)
        {
            if (hasWon || other.GetComponentInParent<Bullet>() == null)
            {
                return;
            }

            hasWon = true;
            Debug.Log("YOU WIN!", this);
            Time.timeScale = 0f;
        }
    }
}
