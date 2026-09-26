using System.Collections.Generic;
using UnityEngine;

namespace DreamForgeTD
{
    public static class GameVfx
    {
        private const string LibraryResourcePath = "VFX/GameVfxLibrary";
        private const int SoFxDuMoiPrefab = 128;
        private static GameVfxLibrary library;
        private static Transform gocPool;
        private static readonly Dictionary<GameObject, Stack<VfxAutoCleanup>> pool = new Dictionary<GameObject, Stack<VfxAutoCleanup>>();
        private static readonly HashSet<VfxAutoCleanup> dangPhat = new HashSet<VfxAutoCleanup>();
        private static readonly List<VfxAutoCleanup> canDon = new List<VfxAutoCleanup>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // With domain/scene reload disabled, the previous runtime objects can still exist.
            // Release attached effects before discarding the collections that track them.
            Transform gocCu = gocPool;
            gocPool = null;
            DonHieuUngTrongMan();
            if (gocCu != null)
            {
                gocCu.gameObject.SetActive(false);
                Object.Destroy(gocCu.gameObject);
            }
            library = null;
            pool.Clear();
            dangPhat.Clear();
            canDon.Clear();
        }

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
            if (effect != null)
                effect.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.2f, Mathf.Clamp01(ratio));
        }

        public static void Stop(GameObject effect)
        {
            if (effect != null && effect.activeSelf && effect.TryGetComponent(out VfxAutoCleanup fx))
                fx.DungPhat();
        }

        public static void StopAndClear(GameObject effect)
        {
            if (effect != null && effect.TryGetComponent(out VfxAutoCleanup fx))
                ThuHoi(fx);
        }

        public static GameObject Phat(GameObject prefab, Vector3 position, Quaternion rotation, float maximumLifetime = 0f)
        {
            VfxAutoCleanup fx = Lay(prefab);
            if (fx == null)
                return null;
            fx.transform.SetParent(gocPool, false);
            fx.transform.SetPositionAndRotation(position, rotation);
            fx.transform.localScale = prefab.transform.localScale;
            fx.BatDau(false, maximumLifetime);
            return fx.gameObject;
        }

        private static void Spawn(GameObject prefab, Vector3 position, Vector3 direction)
        {
            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.FromToRotation(Vector3.forward, direction.normalized) : Quaternion.identity;
            Phat(prefab, position, rotation);
        }

        private static GameObject Attach(GameObject prefab, Transform parent)
        {
            if (parent == null)
                return null;
            VfxAutoCleanup fx = Lay(prefab);
            if (fx == null)
                return null;
            fx.transform.SetParent(parent, false);
            fx.transform.localPosition = Vector3.zero;
            fx.transform.localRotation = Quaternion.identity;
            fx.transform.localScale = prefab.transform.localScale;
            fx.BatDau(true, 0f);
            return fx.gameObject;
        }

        private static VfxAutoCleanup Lay(GameObject prefab)
        {
            if (prefab == null)
                return null;
            if (gocPool == null)
            {
                pool.Clear();
                dangPhat.Clear();
                gocPool = new GameObject("Pool FX", typeof(VfxPoolRoot)).transform;
            }
            if (!pool.TryGetValue(prefab, out Stack<VfxAutoCleanup> danhSach))
            {
                danhSach = new Stack<VfxAutoCleanup>();
                pool.Add(prefab, danhSach);
            }
            VfxAutoCleanup fx = null;
            while (danhSach.Count > 0 && fx == null)
                fx = danhSach.Pop();
            if (fx == null)
            {
                GameObject instance = Object.Instantiate(prefab, gocPool, false);
                if (!instance.TryGetComponent(out fx))
                    fx = instance.AddComponent<VfxAutoCleanup>();
                fx.Prefab = prefab;
            }
            dangPhat.Add(fx);
            return fx;
        }

        internal static void ThuHoi(VfxAutoCleanup fx)
        {
            if (fx == null)
                return;
            if (fx.Prefab == null)
            {
                Object.Destroy(fx.gameObject);
                return;
            }
            if (!dangPhat.Remove(fx))
                return;
            fx.XoaHat();
            fx.gameObject.SetActive(false);
            if (gocPool == null || !pool.TryGetValue(fx.Prefab, out Stack<VfxAutoCleanup> danhSach) || danhSach.Count >= SoFxDuMoiPrefab)
            {
                Object.Destroy(fx.gameObject);
                return;
            }
            fx.transform.SetParent(gocPool, false);
            danhSach.Push(fx);
        }

        internal static void DonPool(Transform root)
        {
            if (gocPool != root)
                return;
            gocPool = null;
            DonHieuUngTrongMan();
            pool.Clear();
            dangPhat.Clear();
        }

        internal static void BoTheoDoi(VfxAutoCleanup fx) => dangPhat.Remove(fx);

        public static void DonHieuUngTrongMan()
        {
            canDon.Clear();
            canDon.AddRange(dangPhat);
            for (int i = 0; i < canDon.Count; i++)
                ThuHoi(canDon[i]);
            canDon.Clear();
        }
    }
}
