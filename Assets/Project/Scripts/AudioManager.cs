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
        private const int InitialOneShotSourceCount = 8;
        private const int MaximumOneShotSourceCount = 16;

        private static AudioManager instance;
        private static bool applicationIsQuitting;

        [Header("Optional clip overrides")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private AudioClip buttonClickClip;
        [SerializeField] private AudioClip obstacleCollisionClip;

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
            EnsureAudioSources();
        }

        private void Start()
        {
            PlayBackgroundMusic();
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
            PlaySoundEffect(LoadSoundEffect("CannonShot"), position, 0.45f, true);
        }

        public void PlayObstacleCollision(Vector3 position, float impactSpeed)
        {
            AudioClip clip = obstacleCollisionClip != null
                ? obstacleCollisionClip
                : LoadSoundEffect("Bounce");
            float volume = Mathf.Lerp(0.16f, 0.5f, Mathf.Clamp01(impactSpeed / 35f));
            PlaySoundEffect(clip, position, volume, true);
        }

        public void PlayBounce(Vector3 position, float impactSpeed)
        {
            float volume = Mathf.Lerp(0.18f, 0.52f, Mathf.Clamp01(impactSpeed / 35f));
            PlaySoundEffect(LoadSoundEffect("Bounce"), position, volume, true);
        }

        public void PlayPortalEnter(Vector3 position)
        {
            PlaySoundEffect(LoadSoundEffect("PortalEnter"), position, 0.52f, true);
        }

        public void PlayPortalExit(Vector3 position)
        {
            PlaySoundEffect(LoadSoundEffect("PortalExit"), position, 0.52f, true);
        }

        public void PlayTargetWin(Vector3 position)
        {
            PlaySoundEffect(LoadSoundEffect("TargetWin"), position, 0.7f, true);
        }

        /// <summary>Can be assigned directly to a Unity UI Button event.</summary>
        public void PlayButtonClick()
        {
            AudioClip clip = buttonClickClip != null
                ? buttonClickClip
                : LoadSoundEffect("ButtonClick");
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
            if (musicSource.clip == clip && musicSource.isPlaying)
                return;

            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.volume = masterVolume * musicVolume;
            musicSource.Play();
        }

        public void StopBackgroundMusic()
        {
            if (musicSource == null)
                return;

            musicSource.Stop();
            musicSource.clip = null;
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
                musicSource = CreateAudioSource("Background Music");

            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;

            while (oneShotSources.Count < InitialOneShotSourceCount)
                oneShotSources.Add(CreateAudioSource("Sound Effect " + oneShotSources.Count));

            ApplyVolumes();
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
                return;

            EnsureAudioSources();
            AudioSource source = GetOneShotSource();
            source.transform.position = position;
            source.spatialBlend = spatial ? 1f : 0f;
            source.volume = masterVolume * soundEffectsVolume;
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
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
                AudioSource source = CreateAudioSource("Sound Effect " + oneShotSources.Count);
                oneShotSources.Add(source);
                nextOneShotSource = 0;
                return source;
            }

            AudioSource reusedSource = oneShotSources[nextOneShotSource];
            if (reusedSource == null)
            {
                reusedSource = CreateAudioSource("Sound Effect " + nextOneShotSource);
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
