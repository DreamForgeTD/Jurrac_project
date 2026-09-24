using System;
using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    public sealed class LevelController : MonoBehaviour
    {
        [SerializeField] private LevelConfig config;
        [SerializeField] private LauncherController launcher;
        [SerializeField] private Goal goal;
        [Tooltip("Explicit list: Switch, Gate, MovingObstacle and other stateful mechanics. Goal resets separately.")]
        [SerializeField] private MonoBehaviour[] resettableComponents;

        private ILevelResettable[] resettables;
        private Projectile projectile;
        private bool initialized;
        private bool resolveFinishedShot;
        public LevelState State { get; private set; } = LevelState.Playing;
        public int RemainingShots { get; private set; }
        public event Action<int> RemainingShotsChanged;
        public event Action<LevelState> StateChanged;
        public event Action LevelWon;
        public event Action LevelFailed;

        // Start ensures all scene dependencies have completed Awake.
        private void Start()
        {
            if (config == null || launcher == null || goal == null ||
                !launcher.IsConfigured || !launcher.isActiveAndEnabled || !goal.isActiveAndEnabled ||
                !launcher.Projectile.isActiveAndEnabled)
            {
                Debug.LogError($"{name}: Assign active, configured Launcher, Goal and LevelConfig.", this);
                if (launcher != null) launcher.SetCanFire(false);
                enabled = false;
                return;
            }

            int count = resettableComponents == null ? 0 : resettableComponents.Length;
            resettables = new ILevelResettable[count];
            for (int i = 0; i < count; i++)
            {
                if (resettableComponents[i] == null || !(resettableComponents[i] is ILevelResettable resettable))
                {
                    Debug.LogError($"{name}: Resettable element {i} must implement ILevelResettable.", this);
                    enabled = false;
                    return;
                }
                resettables[i] = resettable;
            }

            projectile = launcher.Projectile;
            initialized = true;
            Subscribe();
            Restart();
        }

        private void OnEnable() { if (initialized) { Subscribe(); Restart(); } }

        private void Subscribe()
        {
            launcher.Fired += OnShotFired;
            projectile.Finished += OnProjectileFinished;
            goal.Reached += OnGoalReached;
        }

        private void OnDisable()
        {
            if (!initialized) return;
            launcher.Fired -= OnShotFired;
            projectile.Finished -= OnProjectileFinished;
            goal.Reached -= OnGoalReached;
            launcher.SetCanFire(false);
            // No completion event while tearing down or unloading the scene.
            projectile.Prepare(projectile.transform.position, projectile.transform.rotation);
        }

        public void Restart()
        {
            if (!initialized || !isActiveAndEnabled) return;
            launcher.SetCanFire(false);
            resolveFinishedShot = false;
            launcher.PrepareShot(true);
            for (int i = 0; i < resettables.Length; i++) resettables[i].ResetState();
            goal.ResetState();
            RemainingShots = config.ProjectileCount;
            State = LevelState.Playing;
            launcher.SetCanFire(true);
            RemainingShotsChanged?.Invoke(RemainingShots);
            StateChanged?.Invoke(State);
        }

        private void OnShotFired()
        {
            RemainingShots--;
            RemainingShotsChanged?.Invoke(RemainingShots);
        }

        private void OnProjectileFinished()
        {
            // Resolve after physics callbacks; never rearm inside a collision callback.
            if (State == LevelState.Playing) resolveFinishedShot = true;
        }

        private void LateUpdate()
        {
            if (!resolveFinishedShot || State != LevelState.Playing) return;
            resolveFinishedShot = false;
            if (RemainingShots == 0)
            {
                State = LevelState.Fail;
                launcher.SetCanFire(false);
                StateChanged?.Invoke(State);
                LevelFailed?.Invoke();
                return;
            }
            launcher.PrepareShot();
            launcher.SetCanFire(true);
        }

        private void OnGoalReached()
        {
            if (State != LevelState.Playing) return;
            State = LevelState.Win;
            resolveFinishedShot = false;
            launcher.SetCanFire(false);
            projectile.Finish();
            StateChanged?.Invoke(State);
            LevelWon?.Invoke();
        }
    }
}
