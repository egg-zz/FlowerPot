using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    public enum GoalType
    {
        MergeAny,   // merge N times (chain = -1) or N times in one chain
        MakeItem,   // make an item of at least `level` (chain = -1: any chain)
    }

    [Serializable]
    public class LevelGoal
    {
        public GoalType type;
        [Tooltip("-1 = any chain")] public int chain = -1;
        public int level;
        public int count = 1;
    }

    /// <summary>
    /// Designed levels: goals, time limit, a fixed starting board and where the 4 item boxes stand.
    /// Start items are written as "F1 3 3; C2 0 6" = chain letter (F/C/B/I) + level, then cell x y (0..6, y = 0 is the bottom row).
    /// Item boxes are written as "F 0 0; C 2 0" = chain letter, then cell x y.
    /// </summary>
    [CreateAssetMenu(menuName = "PotMerge/Level Database", fileName = "LevelDatabase")]
    public class LevelDatabase : ScriptableObject
    {
        /// <summary>All 4 boxes along the bottom row, with a free cell between each.</summary>
        public const string DefaultBoxes = "F 0 0; C 2 0; B 4 0; I 6 0";

        [Serializable]
        public class Level
        {
            public LevelGoal[] goals;
            [Tooltip("Time limit in seconds. 0 = no time limit (tutorial levels 1-9)")]
            public int seconds = 60;
            [Tooltip("Item boxes: \"F 0 0; C 2 0; B 4 0; I 6 0\". Empty = the default bottom row")]
            public string boxes = "";
            [TextArea] public string startItems;
        }

        public Level[] levels;

        public int Count => levels != null ? levels.Length : 0;

        public Level Get(int number) => levels != null && number >= 1 && number <= levels.Length ? levels[number - 1] : null;

        public static readonly string ChainLetters = "FCBI";

        public static List<(int chain, int level, Vector2Int cell)> ParseStart(string s)
        {
            var list = new List<(int, int, Vector2Int)>();
            if (string.IsNullOrWhiteSpace(s)) return list;
            foreach (var part in s.Split(';'))
            {
                var t = part.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (t.Length != 3 || t[0].Length < 2) continue;
                int chain = ChainLetters.IndexOf(char.ToUpperInvariant(t[0][0]));
                if (chain < 0 || !int.TryParse(t[0].Substring(1), out int lv)) continue;
                if (!int.TryParse(t[1], out int x) || !int.TryParse(t[2], out int y)) continue;
                list.Add((chain, lv, new Vector2Int(x, y)));
            }
            return list;
        }

        public static List<(int chain, Vector2Int cell)> ParseBoxes(string s)
        {
            var list = new List<(int, Vector2Int)>();
            if (string.IsNullOrWhiteSpace(s)) s = DefaultBoxes;
            foreach (var part in s.Split(';'))
            {
                var t = part.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (t.Length != 3 || t[0].Length < 1) continue;
                int chain = ChainLetters.IndexOf(char.ToUpperInvariant(t[0][0]));
                if (chain < 0 || !int.TryParse(t[1], out int x) || !int.TryParse(t[2], out int y)) continue;
                list.Add((chain, new Vector2Int(x, y)));
            }
            return list;
        }

        static LevelGoal Merge(int count, int chain = -1) => new LevelGoal { type = GoalType.MergeAny, chain = chain, count = count };
        static LevelGoal Make(int chain, int level) => new LevelGoal { type = GoalType.MakeItem, chain = chain, level = level, count = 1 };

        /// <summary>Levels 1-9: tutorial-like, no time limit. 10-14: the timer appears. Orders come at 15, board blockers at 25.</summary>
        public static Level[] DefaultLevels() => new[]
        {
            // 1: tutorial, one pair
            new Level { goals = new[] { Merge(1) }, seconds = 0,
                startItems = "F1 2 3; F1 4 3" },
            // 2: three pairs
            new Level { goals = new[] { Merge(3) }, seconds = 0,
                startItems = "F1 1 4; F1 5 2; C1 2 2; C1 4 4; B1 3 5; B1 3 1" },
            // 3: chain merges (1+1 -> 2, 2+2 -> 3)
            new Level { goals = new[] { Merge(5) }, seconds = 0,
                startItems = "F1 1 1; F1 5 1; F1 1 5; F1 5 5; C1 3 2; C1 2 4; C1 4 4; C1 3 6" },
            // 4: make a Leafy Plant (Flower 4)
            new Level { goals = new[] { Make(0, 4) }, seconds = 0,
                startItems = "F1 1 2; F1 5 4; F1 3 5; F2 3 1; C1 0 6; C1 6 1; B1 6 6" },
            // 5: cafe merges, chains mixed
            new Level { goals = new[] { Merge(5, 1) }, seconds = 0,
                startItems = "C1 0 1; C1 6 6; C1 3 3; C1 1 5; C2 5 1; F1 2 1; F1 4 5; B1 0 3; B1 6 3; I1 3 6" },
            // 6: merges + a Bread Slice (Bakery 4)
            new Level { goals = new[] { Merge(8), Make(2, 4) }, seconds = 0,
                startItems = "B1 1 1; B1 5 5; B1 3 1; F1 1 5; F1 5 1; C1 3 6; C2 0 3; C2 6 3" },
            // 7: any level 5 item
            new Level { goals = new[] { Make(-1, 5) }, seconds = 0,
                startItems = "F2 1 1; F2 5 5; F1 3 3; C2 1 5; C1 5 1; B1 0 3; I1 6 3; I2 3 6" },
            // 8: merges + a Soft Serve (Ice Cream 4)
            new Level { goals = new[] { Merge(10), Make(3, 4) }, seconds = 0,
                startItems = "I1 1 1; I1 5 5; I2 3 1; F1 0 4; F1 6 2; C1 2 6; C1 4 6; B1 3 3; B2 1 3" },
            // 9: a Bloom (Flower 6)
            new Level { goals = new[] { Make(0, 6) }, seconds = 0,
                startItems = "F3 1 1; F3 5 5; F2 1 5; F2 5 1; F1 3 2; F1 3 4; C1 0 3; C2 6 3" },

            // ---- 10-14: the timer appears (stars by time left) ----
            // 10: first timed level, simple goal (also the first jewel reward)
            new Level { goals = new[] { Merge(10) }, seconds = 80,
                startItems = "F1 1 2; F1 5 2; C1 2 4; C1 4 4; B1 1 5; B1 5 5; I1 3 3; I1 3 6" },
            // 11: an Orange Juice (Cafe 5)
            new Level { goals = new[] { Make(1, 5) }, seconds = 90,
                startItems = "C2 1 2; C2 5 2; C3 3 4; C1 2 5; F1 4 5; B1 0 4; I1 6 4" },
            // 12: merges + a Bud (Flower 5)
            new Level { goals = new[] { Merge(12), Make(0, 5) }, seconds = 100,
                startItems = "F2 1 2; F2 5 2; F3 3 5; F1 2 4; C1 4 4; B2 0 3; I2 6 3" },
            // 13: any level 6 item
            new Level { goals = new[] { Make(-1, 6) }, seconds = 110,
                startItems = "B3 1 2; B3 5 2; C3 2 5; F3 4 5; I2 3 3; B2 0 4; C2 6 4" },
            // 14: ice cream merges + a Double Scoop (Ice Cream 5)
            new Level { goals = new[] { Merge(8, 3), Make(3, 5) }, seconds = 110,
                startItems = "I2 1 2; I2 5 2; I3 3 5; I1 2 4; I1 4 4; F1 0 3; C1 6 3" },
        };

        [ContextMenu("Reset To Default Levels")]
        void ResetToDefaults() => levels = DefaultLevels();

        void Reset() => levels = DefaultLevels();
    }
}
