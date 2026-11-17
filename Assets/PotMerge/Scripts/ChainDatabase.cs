using System;
using UnityEngine;

namespace PotMerge
{
    /// <summary>All merge chains: models, stage names, colors and (optional) icons. Level is 1-based.</summary>
    [CreateAssetMenu(menuName = "PotMerge/Chain Database", fileName = "ChainDatabase")]
    public class ChainDatabase : ScriptableObject
    {
        [Serializable]
        public class Chain
        {
            public string id;
            public string displayName;
            public Color color = Color.white;
            [Tooltip("index 0 = level 1")] public GameObject[] models;
            public string[] levelNames;
            [Tooltip("Collection icons (optional), index 0 = level 1")] public Texture2D[] icons;
        }

        public Chain[] chains;

        [Tooltip("Size multiplier per level after fitting to the cell (index 0 = level 1), so higher stages look bigger.")]
        public float[] levelSize = { 0.62f, 0.68f, 0.74f, 0.80f, 0.85f, 0.90f, 0.94f, 0.97f, 1f, 1f, 1f };

        public int ChainCount => chains != null ? chains.Length : 0;

        /// <summary>Shared max level = shortest chain.</summary>
        public int MaxLevel
        {
            get
            {
                if (ChainCount == 0) return 0;
                int m = int.MaxValue;
                foreach (var c in chains) m = Mathf.Min(m, c.models != null ? c.models.Length : 0);
                return m;
            }
        }

        public bool IsValid(int chain, int level) =>
            chain >= 0 && chain < ChainCount && level >= 1 && level <= MaxLevel && chains[chain].models[level - 1] != null;

        public Chain Get(int chain) => chains[chain];

        public GameObject Model(int chain, int level) => chains[chain].models[level - 1];

        public string LevelName(int chain, int level)
        {
            var n = chains[chain].levelNames;
            return n != null && level - 1 < n.Length && !string.IsNullOrEmpty(n[level - 1]) ? n[level - 1] : "Level " + level;
        }

        public Texture2D Icon(int chain, int level)
        {
            var i = chains[chain].icons;
            return i != null && level - 1 < i.Length ? i[level - 1] : null;
        }

        public float SizeFor(int level) =>
            levelSize != null && level - 1 < levelSize.Length && level >= 1 ? levelSize[level - 1] : 1f;
    }
}
