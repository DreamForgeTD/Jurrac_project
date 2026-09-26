using UnityEngine;

namespace DreamForgeTD
{
    [DisallowMultipleComponent]
    public sealed class VfxAutoCleanup : MonoBehaviour
    {
        [SerializeField, Min(0.25f)] private float maximumLifetime = 8f;
        private ParticleSystem[] systems;
        private TrailRenderer[] trails;
        private float elapsed;
        private float gioiHan;
        private bool dangGan;
        private bool daPhat;
        internal GameObject Prefab { get; set; }

        private void Awake()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
            trails = GetComponentsInChildren<TrailRenderer>(true);
        }

        private void OnEnable()
        {
            elapsed = 0f;
            gioiHan = maximumLifetime;
            daPhat = false;
        }

        internal void BatDau(bool attached, float lifetime)
        {
            gameObject.SetActive(true);
            elapsed = 0f;
            gioiHan = lifetime > 0f ? Mathf.Max(0.25f, lifetime) : maximumLifetime;
            dangGan = attached;
            daPhat = false;
            XoaHat();
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                // The pool owns lifetime; a particle system must not destroy its reusable owner.
                main.stopAction = ParticleSystemStopAction.None;
                systems[i].Play(false);
            }
            for (int i = 0; i < trails.Length; i++)
                trails[i].emitting = true;
        }

        internal void DungPhat()
        {
            transform.SetParent(null, true);
            dangGan = false;
            elapsed = 0f;
            for (int i = 0; i < systems.Length; i++)
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
            for (int i = 0; i < trails.Length; i++)
                trails[i].emitting = false;
        }

        internal void XoaHat()
        {
            for (int i = 0; i < systems.Length; i++)
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int i = 0; i < trails.Length; i++)
                trails[i].Clear();
        }

        private void Update()
        {
            if (dangGan)
                return;
            elapsed += Time.unscaledDeltaTime;
            bool conHat = false;
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null && systems[i].IsAlive(false))
                {
                    conHat = true;
                    break;
                }
            }
            for (int i = 0; !conHat && i < trails.Length; i++)
                conHat = trails[i] != null && trails[i].positionCount > 0;
            daPhat |= conHat;
            if (elapsed < gioiHan && (conHat || !daPhat))
                return;
            GameVfx.ThuHoi(this);
        }

        private void OnDestroy() => GameVfx.BoTheoDoi(this);
    }
}
