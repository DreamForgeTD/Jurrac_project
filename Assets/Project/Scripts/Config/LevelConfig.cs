using UnityEngine;

namespace DreamForgeTD.PhysicsPuzzle
{
    [CreateAssetMenu(menuName = "Physics Puzzle/Level Config")]
    public sealed class LevelConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int projectileCount = 3;
        public int ProjectileCount => Mathf.Max(1, projectileCount);
    }
}
