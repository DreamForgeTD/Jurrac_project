using UnityEngine;

namespace DreamForgeTD
{
    [CreateAssetMenu(fileName = "GameVfxLibrary", menuName = "DreamForge/VFX Library")]
    public sealed class GameVfxLibrary : ScriptableObject
    {
        [Header("Cannon")]
        public GameObject cannonMuzzleFlash;
        public GameObject cannonChargeLoop;

        [Header("Projectile")]
        public GameObject bulletTrail;
        public GameObject bulletImpact;
        public GameObject bulletBounce;

        [Header("Gameplay")]
        public GameObject targetVictory;
        public GameObject portalEnter;
        public GameObject portalExit;
    }
}
