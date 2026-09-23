using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DreamForgeTD
{
    public class CannonShooter : MonoBehaviour
    {
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float fireRate = 0.3f;

        private float nextFireTime;

        private void Update()
        {
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

        private void Shoot()
        {
            Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
            nextFireTime = Time.time + fireRate;
        }
    }
}
