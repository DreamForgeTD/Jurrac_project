using UnityEngine;

namespace DreamForgeTD
{
    [CreateAssetMenu(menuName = "DreamForge/Levels/Level Definition", fileName = "Level_")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField] private LevelDocument data = new LevelDocument();

        public LevelDocument Data => data;

        public void SetData(LevelDocument levelData)
        {
            data = levelData ?? new LevelDocument();
        }
    }
}
