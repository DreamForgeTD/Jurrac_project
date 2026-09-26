using UnityEngine;

namespace DreamForgeTD
{
    public static class GameVfx
    {
        private const string LibraryResourcePath = "VFX/GameVfxLibrary";
        private static GameVfxLibrary library;

        private static GameVfxLibrary Library
        {
            get
            {
                if (library == null)
                    library = Resources.Load<GameVfxLibrary>(LibraryResourcePath);
                return library;
            }
        }

        public static void PlayCannonMuzzle(Vector3 position, Vector3 direction)
            => Spawn(Library != null ? Library.cannonMuzzleFlash : null, position, direction);

        public static void PlayBulletImpact(Vector3 position, Vector3 normal)
            => Spawn(Library != null ? Library.bulletImpact : null, position, normal);

        public static void PlayBulletBounce(Vector3 position, Vector3 normal)
            => Spawn(Library != null ? Library.bulletBounce : null, position, normal);

        public static void PlayTargetVictory(Vector3 position, Vector3 normal)
            => Spawn(Library != null ? Library.targetVictory : null, position, normal);

        public static void PlayPortalEnter(Vector3 position, Vector3 normal)
            => Spawn(Library != null ? Library.portalEnter : null, position, normal);

        public static void PlayPortalExit(Vector3 position, Vector3 direction)
            => Spawn(Library != null ? Library.portalExit : null, position, direction);

        public static GameObject AttachCannonCharge(Transform parent)
            => Attach(Library != null ? Library.cannonChargeLoop : null, parent);

        public static GameObject AttachProjectileTrail(Transform parent)
            => Attach(Library != null ? Library.bulletTrail : null, parent);

        public static void SetChargeIntensity(GameObject effect, float ratio)
        {
            if (effect == null)
                return;

            float scale = Mathf.Lerp(0.85f, 1.2f, Mathf.Clamp01(ratio));
            effect.transform.localScale = Vector3.one * scale;
        }

        public static void Stop(GameObject effect)
        {
            Stop(effect, ParticleSystemStopBehavior.StopEmitting);
        }

        public static void StopAndClear(GameObject effect)
        {
            Stop(effect, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void Stop(GameObject effect, ParticleSystemStopBehavior stopBehavior)
        {
            if (effect == null)
                return;

            ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                    systems[i].Stop(true, stopBehavior);
            }
        }

        private static void Spawn(GameObject prefab, Vector3 position, Vector3 direction)
        {
            if (prefab == null)
                return;

            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.FromToRotation(Vector3.forward, direction.normalized)
                : Quaternion.identity;
            PlaySystems(Object.Instantiate(prefab, position, rotation));
        }

        private static GameObject Attach(GameObject prefab, Transform parent)
        {
            if (prefab == null || parent == null)
                return null;

            GameObject instance = Object.Instantiate(prefab, parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            PlaySystems(instance);
            return instance;
        }

        private static void PlaySystems(GameObject instance)
        {
            if (instance == null)
                return;

            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                    systems[i].Play(true);
            }
        }
    }
}
