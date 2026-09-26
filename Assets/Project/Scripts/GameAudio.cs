using UnityEngine;

namespace DreamForgeTD
{
    internal static class GameAudio
    {
        public static void PlayCannonShot(Vector3 position)
        {
            AudioManager.Instance?.PlayCannonShot(position);
        }

        public static void PlayBounce(Vector3 position, float impactSpeed)
        {
            AudioManager.Instance?.PlayBounce(position, impactSpeed);
        }

        public static void PlayPortalEnter(Vector3 position)
        {
            AudioManager.Instance?.PlayPortalEnter(position);
        }

        public static void PlayPortalExit(Vector3 position)
        {
            AudioManager.Instance?.PlayPortalExit(position);
        }

        public static void PlayTargetWin(Vector3 position)
        {
            AudioManager.Instance?.PlayTargetWin(position);
        }

        public static void PlayObstacleCollision(Vector3 position, float impactSpeed)
        {
            AudioManager.Instance?.PlayObstacleCollision(position, impactSpeed);
        }

        public static void PlayButtonClick()
        {
            AudioManager.Instance?.PlayButtonClick();
        }

        public static void PlayBackgroundMusic(AudioClip clip)
        {
            AudioManager.Instance?.PlayBackgroundMusic(clip);
        }
    }
}
