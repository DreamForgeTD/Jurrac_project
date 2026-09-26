using System.Collections.Generic;
using UnityEngine;

namespace DreamForgeTD
{
    /// <summary>
    /// Persistent owner for music and one-shot game audio.
    /// Creates itself when no AudioManager component is present in the loaded scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private const string SoundEffectsResourcePath = "Audio/SFX/";
        private const string BackgroundMusicResourcePath = "Audio/BGM/BackgroundMusic";
        private const int InitialOneShotSourceCount = 10;
        private const int MaximumOneShotSourceCount = 16;

        private static AudioManager instance;
        private static bool applicationIsQuitting;

        [Header("Clip assignments (Resources fallback if unset)")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private AudioClip levelStartClip;
        [SerializeField] private AudioClip buttonClickClip;
        [SerializeField] private AudioClip cannonShotClip;
        [SerializeField] private AudioClip bounceClip;
        [SerializeField] private AudioClip obstacleCollisionClip;
        [SerializeField] private AudioClip canCollisionClip;
        [SerializeField] private AudioClip portalEnterClip;
        [SerializeField] private AudioClip portalExitClip;
        [SerializeField] private AudioClip targetHitClip;
        [SerializeField] private AudioClip targetWinClip;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float soundEffectsVolume = 1f;

        private readonly Dictionary<string, AudioClip> soundEffectCache = new Dictionary<string, AudioClip>(8);
        private readonly List<AudioSource> oneShotSources = new List<AudioSource>(InitialOneShotSourceCount);
        private AudioSource musicSource;
        private int nextOneShotSource;

        public static AudioManager Instance
        {
            get
            {
                if (applicationIsQuitting)
                    return null;

                if (instance != null)
                    return instance;

                instance = FindFirstObjectByType<AudioManager>();
                if (instance == null)
                {
                    GameObject managerObject = new GameObject("Audio Manager");
                    instance = managerObject.AddComponent<AudioManager>();
                }

                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            applicationIsQuitting = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureManagerExists()
        {
            if (!applicationIsQuitting)
                Instance?.PlayBackgroundMusic();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.audioMasterMute)
            {
                UnityEditor.EditorUtility.audioMasterMute = false;
                Debug.LogWarning("Unity Game View audio mute was enabled; it has been turned off.", this);
            }
#endif

            if (instance != null && instance != this)
                return;

            instance = this;
            AudioListener.pause = false;
            AudioListener.volume = 1f;
            LoadMissingClips();
            EnsureAudioSources();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            if (transform.parent != null)
                transform.SetParent(null, true);

            DontDestroyOnLoad(gameObject);
            AudioListener.pause = false;
            AudioListener.volume = 1f;
            LoadMissingClips();
            EnsureAudioSources();

            if (FindFirstObjectByType<AudioListener>() == null)
                Debug.LogError("AudioManager needs an active AudioListener in the loaded scene.", this);
        }

        private void Start()
        {
            PlayBackgroundMusic();
            PlaySoundEffect(levelStartClip, Vector3.zero, 0.65f, false);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        public void PlayCannonShot(Vector3 position)
        {
            AudioClip clip = cannonShotClip != null
                ? cannonShotClip
                : LoadSoundEffect("CannonShot_Cartoon") ?? LoadSoundEffect("CannonShot");
            PlaySoundEffect(clip, position, 0.45f, true);
        }

        public void PlayObstacleCollision(Vector3 position, float impactSpeed)
        {
            AudioClip clip = obstacleCollisionClip != null
                ? obstacleCollisionClip
                : LoadSoundEffect("ObstacleCollision_Cartoon") ??
                  LoadSoundEffect("Bounce_Cartoon") ??
                  LoadSoundEffect("Bounce");
            float volume = Mathf.Lerp(0.16f, 0.5f, Mathf.Clamp01(impactSpeed / 35f));
            PlaySoundEffect(clip, position, volume, true);
        }

        public void PlayBounce(Vector3 position, float impactSpeed)
        {
            float volume = Mathf.Lerp(0.18f, 0.52f, Mathf.Clamp01(impactSpeed / 35f));
            AudioClip clip = bounceClip != null
                ? bounceClip
                : LoadSoundEffect("Bounce_Cartoon") ?? LoadSoundEffect("Bounce");
            PlaySoundEffect(clip, position, volume, true);
        }

        public void PlayCanCollision(Vector3 position, float impactSpeed)
        {
            if (impactSpeed < 0.45f)
                return;

            AudioClip clip = canCollisionClip != null
                ? canCollisionClip
                : LoadSoundEffect("CanCollision_Cartoon") ??
                  LoadSoundEffect("ObstacleCollision_Cartoon") ??
                  LoadSoundEffect("Bounce");
            float volume = Mathf.Lerp(0.12f, 0.58f, Mathf.Clamp01(impactSpeed / 12f));
            PlaySoundEffect(clip, position, volume, true);
        }

        public void PlayPortalEnter(Vector3 position)
        {
            AudioClip clip = portalEnterClip != null ? portalEnterClip : LoadSoundEffect("PortalEnter");
            PlaySoundEffect(clip, position, 0.52f, true);
        }

        public void PlayPortalExit(Vector3 position)
        {
            AudioClip clip = portalExitClip != null ? portalExitClip : LoadSoundEffect("PortalExit");
            PlaySoundEffect(clip, position, 0.52f, true);
        }

        public void PlayTargetWin(Vector3 position)
        {
            AudioClip hitClip = targetHitClip != null ? targetHitClip : LoadSoundEffect("TargetHit_Cartoon");
            PlaySoundEffect(hitClip, position, 0.62f, true);

            AudioClip winClip = targetWinClip != null ? targetWinClip : LoadSoundEffect("TargetWin");
            PlaySoundEffect(winClip, position, 0.55f, true);
        }

        /// <summary>Can be assigned directly to a Unity UI Button event.</summary>
        public void PlayButtonClick()
        {
            AudioClip clip = buttonClickClip != null
                ? buttonClickClip
                : LoadSoundEffect("ButtonClick_Cartoon") ?? LoadSoundEffect("ButtonClick");
            PlaySoundEffect(clip, Vector3.zero, 0.65f, false);
        }

        public void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            PlaySoundEffect(clip, Vector3.zero, volume, false);
        }

        public void PlayBackgroundMusic()
        {
            if (backgroundMusic == null)
                backgroundMusic = Resources.Load<AudioClip>(BackgroundMusicResourcePath);

            if (backgroundMusic != null)
                PlayBackgroundMusic(backgroundMusic);
        }

        public void PlayBackgroundMusic(AudioClip clip)
        {
            if (clip == null)
            {
                StopBackgroundMusic();
                return;
            }

            EnsureAudioSources();
            backgroundMusic = clip;
            if (musicSource.resource == clip && musicSource.isPlaying)
                return;

            musicSource.resource = clip;
            musicSource.loop = true;
            musicSource.volume = masterVolume * musicVolume;
            musicSource.Play();
        }

        public void StopBackgroundMusic()
        {
            if (musicSource == null)
                return;

            musicSource.Stop();
            musicSource.resource = null;
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        public void SetSoundEffectsVolume(float volume)
        {
            soundEffectsVolume = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        private void EnsureAudioSources()
        {
            if (musicSource == null)
            {
                musicSource = CreateAudioSource("Background Music");
                musicSource.resource = backgroundMusic;
            }

            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;

            while (oneShotSources.Count < InitialOneShotSourceCount)
            {
                int index = oneShotSources.Count;
                AudioSource source = CreateAudioSource("Sound Effect " + index);
                source.resource = GetInitialOneShotClip(index);
                oneShotSources.Add(source);
            }

            ApplyVolumes();
        }

        private void LoadMissingClips()
        {
            if (backgroundMusic == null)
                backgroundMusic = Resources.Load<AudioClip>(BackgroundMusicResourcePath);
            if (levelStartClip == null)
                levelStartClip = LoadSoundEffect("LevelStart_Cartoon");
            if (buttonClickClip == null)
                buttonClickClip = LoadSoundEffect("ButtonClick_Cartoon");
            if (cannonShotClip == null)
                cannonShotClip = LoadSoundEffect("CannonShot_Cartoon") ?? LoadSoundEffect("CannonShot");
            if (bounceClip == null)
                bounceClip = LoadSoundEffect("Bounce_Cartoon") ?? LoadSoundEffect("Bounce");
            if (obstacleCollisionClip == null)
                obstacleCollisionClip = LoadSoundEffect("ObstacleCollision_Cartoon") ??
                                         LoadSoundEffect("Bounce_Cartoon") ??
                                         LoadSoundEffect("Bounce");
            if (canCollisionClip == null)
                canCollisionClip = LoadSoundEffect("CanCollision_Cartoon") ??
                                   LoadSoundEffect("ObstacleCollision_Cartoon") ??
                                   LoadSoundEffect("Bounce");
            if (portalEnterClip == null)
                portalEnterClip = LoadSoundEffect("PortalEnter");
            if (portalExitClip == null)
                portalExitClip = LoadSoundEffect("PortalExit");
            if (targetHitClip == null)
                targetHitClip = LoadSoundEffect("TargetHit_Cartoon");
            if (targetWinClip == null)
                targetWinClip = LoadSoundEffect("TargetWin");
        }

        private AudioClip GetInitialOneShotClip(int index)
        {
            switch (index % InitialOneShotSourceCount)
            {
                case 0: return buttonClickClip;
                case 1: return cannonShotClip;
                case 2: return bounceClip;
                case 3: return obstacleCollisionClip;
                case 4: return canCollisionClip;
                case 5: return portalEnterClip;
                case 6: return portalExitClip;
                case 7: return targetHitClip;
                case 8: return targetWinClip;
                default: return levelStartClip;
            }
        }

        private AudioSource CreateAudioSource(string sourceName)
        {
            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 100f;
            return source;
        }

        private AudioClip LoadSoundEffect(string resourceName)
        {
            if (soundEffectCache.TryGetValue(resourceName, out AudioClip cachedClip))
                return cachedClip;

            AudioClip clip = Resources.Load<AudioClip>(SoundEffectsResourcePath + resourceName);
            if (clip != null)
                soundEffectCache.Add(resourceName, clip);

            return clip;
        }

        private void PlaySoundEffect(AudioClip clip, Vector3 position, float volume, bool spatial)
        {
            if (clip == null)
            {
                Debug.LogWarning("AudioManager skipped a sound because its AudioClip and Resources fallback are both missing.", this);
                return;
            }

            EnsureAudioSources();
            AudioSource source = GetOneShotSource();
            source.transform.position = position;
            // The playfield is a flat screen-space game. Keep most gameplay sound in 2D
            // so camera distance does not make impacts and shots too quiet.
            source.spatialBlend = spatial ? 0.15f : 0f;
            source.resource = clip;
            source.volume = masterVolume * soundEffectsVolume * Mathf.Clamp01(volume);
            source.Play();
        }

        private AudioSource GetOneShotSource()
        {
            for (int i = 0; i < oneShotSources.Count; i++)
            {
                int index = (nextOneShotSource + i) % oneShotSources.Count;
                AudioSource source = oneShotSources[index];
                if (source == null)
                {
                    source = CreateAudioSource("Sound Effect " + index);
                    source.resource = GetInitialOneShotClip(index);
                    oneShotSources[index] = source;
                    nextOneShotSource = (index + 1) % oneShotSources.Count;
                    return source;
                }

                if (!source.isPlaying)
                {
                    nextOneShotSource = (index + 1) % oneShotSources.Count;
                    return source;
                }
            }

            if (oneShotSources.Count < MaximumOneShotSourceCount)
            {
                int index = oneShotSources.Count;
                AudioSource source = CreateAudioSource("Sound Effect " + index);
                source.resource = GetInitialOneShotClip(index);
                oneShotSources.Add(source);
                nextOneShotSource = 0;
                return source;
            }

            AudioSource reusedSource = oneShotSources[nextOneShotSource];
            if (reusedSource == null)
            {
                reusedSource = CreateAudioSource("Sound Effect " + nextOneShotSource);
                reusedSource.resource = GetInitialOneShotClip(nextOneShotSource);
                oneShotSources[nextOneShotSource] = reusedSource;
            }
            else
            {
                reusedSource.Stop();
            }

            nextOneShotSource = (nextOneShotSource + 1) % oneShotSources.Count;
            return reusedSource;
        }

        private void ApplyVolumes()
        {
            if (musicSource != null)
                musicSource.volume = masterVolume * musicVolume;

            for (int i = 0; i < oneShotSources.Count; i++)
            {
                if (oneShotSources[i] != null)
                    oneShotSources[i].volume = masterVolume * soundEffectsVolume;
            }
        }
    }
}
