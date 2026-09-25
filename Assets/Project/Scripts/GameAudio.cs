using System.Collections.Generic;
using UnityEngine;

namespace DreamForgeTD
{
    internal static class GameAudio
    {
        private const string ResourcePath = "Audio/SFX/";
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>(5);

        public static void PlayCannonShot(Vector3 position)
        {
            Play("CannonShot", position, 0.45f);
        }

        public static void PlayBounce(Vector3 position, float impactSpeed)
        {
            float volume = Mathf.Lerp(0.18f, 0.52f, Mathf.Clamp01(impactSpeed / 35f));
            Play("Bounce", position, volume);
        }

        public static void PlayPortalEnter(Vector3 position)
        {
            Play("PortalEnter", position, 0.52f);
        }

        public static void PlayPortalExit(Vector3 position)
        {
            Play("PortalExit", position, 0.52f);
        }

        public static void PlayTargetWin(Vector3 position)
        {
            Play("TargetWin", position, 0.7f);
        }

        private static void Play(string resourceName, Vector3 position, float volume)
        {
            if (!Clips.TryGetValue(resourceName, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(ResourcePath + resourceName);
                Clips.Add(resourceName, clip);

                if (clip == null)
                {
                    Debug.LogWarning("Missing sound effect in Resources/Audio/SFX: " + resourceName);
                    return;
                }
            }

            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position, volume);
            }
        }
    }
}
