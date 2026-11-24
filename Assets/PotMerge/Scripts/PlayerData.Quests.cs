using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    // Values are saved: only add new ones at the end.
    public enum QuestType
    {
        MergeAny,       // merge N times (any chain)
        MergeChain,     // merge N times in one chain
        AddItem,        // tap item boxes N times
        ReachLevel,     // make one level-N item (any chain)
        PlayLevel,      // start N levels
        ClearLevel,     // clear N levels
        ServeOrder,     // finish N customer orders
        EarnStars,      // earn N stars
        MergeAllChains, // merge every chain twice (target = 2 x chains)
        ThreeStars,     // clear N timed levels with 3 stars
    }

    [Serializable]
    public class Quest
    {
        public QuestType type;
        public int chain;     // MergeChain only
        public int target;
        public int progress;
        public int reward;    // coins
        public int energy;
        public int[] perChain; // MergeAllChains only
        public bool claimed;

        public bool Done => progress >= target;
    }

    /// <summary>Daily quests: one easy, one normal and one hard quest a day, plus a bonus box when all three are claimed.</summary>
    public static partial class PlayerData
    {
        const int QuestVersion = 2;
        const int MergesPerChain = 2;
        const int OrderQuestLevel = 15, ThreeStarQuestLevel = 10; // orders and timers only exist from these levels on
        public const int QuestBonusCoins = 20, QuestBonusEnergy = 15;

        public static string[] ChainNames = { "Flower", "Cafe", "Bakery", "Ice Cream" };

        public static IReadOnlyList<Quest> Quests
        {
            get { EnsureTodayQuests(); return D.quests; }
        }

        public static string QuestText(Quest q)
        {
            switch (q.type)
            {
                case QuestType.MergeAny: return $"Merge {q.target} times";
                case QuestType.MergeChain: return $"Merge {q.target} {ChainNames[Mathf.Clamp(q.chain, 0, ChainCount - 1)]} items";
                case QuestType.AddItem: return $"Tap item boxes {q.target} times";
                case QuestType.ReachLevel: return $"Make a level {q.target} item";
                case QuestType.PlayLevel: return "Play a level";
                case QuestType.ClearLevel: return $"Clear {q.target} levels";
                case QuestType.ServeOrder: return $"Serve {q.target} customers";
                case QuestType.EarnStars: return $"Earn {q.target} stars";
                case QuestType.MergeAllChains: return $"Merge all 4 kinds, {MergesPerChain} times each";
                case QuestType.ThreeStars: return $"Clear {q.target} timed levels with 3 stars";
            }
            return "?";
        }

        // ---- progress
        static void Advance(QuestType type, int amount = 1)
        {
            EnsureTodayQuests();
            bool changed = false;
            foreach (var q in D.quests)
            {
                if (q.claimed || q.type != type || q.Done) continue;
                q.progress = Mathf.Min(q.progress + amount, q.target);
                changed = true;
            }
            if (changed) Commit();
        }

        public static void OnMerge(int chain, int newLevel)
        {
            EnsureTodayQuests();
            foreach (var q in D.quests)
            {
                if (q.claimed) continue;
                if (q.type == QuestType.MergeAny) q.progress++;
                else if (q.type == QuestType.MergeChain && q.chain == chain) q.progress++;
                else if (q.type == QuestType.ReachLevel && newLevel >= q.target) q.progress = q.target;
                else if (q.type == QuestType.MergeAllChains && InRange(chain))
                {
                    if (q.perChain == null || q.perChain.Length != ChainCount) q.perChain = new int[ChainCount];
                    q.perChain[chain] = Mathf.Min(q.perChain[chain] + 1, MergesPerChain);
                    q.progress = 0;
                    foreach (int n in q.perChain) q.progress += n;
                }
                q.progress = Mathf.Min(q.progress, q.target);
            }
            Commit();
        }

        public static void OnAddItem() => Advance(QuestType.AddItem);
        public static void OnLevelStarted() => Advance(QuestType.PlayLevel);
        public static void OnOrderServed() => Advance(QuestType.ServeOrder);

        public static void OnLevelCleared(int stars, bool timed)
        {
            Advance(QuestType.ClearLevel);
            Advance(QuestType.EarnStars, stars);
            if (timed && stars >= 3) Advance(QuestType.ThreeStars);
        }

        // ---- claiming
        /// <summary>False when claiming now would waste energy (it never goes above the max).</summary>
        public static bool EnergyFits(int amount) => amount <= 0 || Energy + amount <= MaxEnergy;

        /// <summary>Claims a finished quest. False if it is not claimable or its energy does not fit.</summary>
        public static bool ClaimQuest(int index)
        {
            EnsureTodayQuests();
            if (index < 0 || index >= D.quests.Count) return false;
            var q = D.quests[index];
            if (!q.Done || q.claimed || !EnergyFits(q.energy)) return false;
            q.claimed = true;
            D.coins += q.reward;
            GainEnergy(q.energy);
            Commit();
            return true;
        }

        public static bool QuestBonusClaimed { get { EnsureTodayQuests(); return D.questBonusClaimed; } }

        public static bool QuestBonusReady
        {
            get
            {
                EnsureTodayQuests();
                if (D.questBonusClaimed) return false;
                foreach (var q in D.quests) if (!q.claimed) return false;
                return true;
            }
        }

        public static bool ClaimQuestBonus()
        {
            if (!QuestBonusReady || !EnergyFits(QuestBonusEnergy)) return false;
            D.questBonusClaimed = true;
            D.coins += QuestBonusCoins;
            GainEnergy(QuestBonusEnergy);
            Commit();
            return true;
        }

        public static int ClaimableQuestCount()
        {
            EnsureTodayQuests();
            int n = QuestBonusReady ? 1 : 0;
            foreach (var q in D.quests) if (q.Done && !q.claimed) n++;
            return n;
        }

        // ---- today's three
        static void EnsureTodayQuests()
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            bool current = D.questVersion == QuestVersion;
            if (current && D.questDate == today && D.quests != null && D.quests.Count == 3) return;

            // quests from before the new set: pay what was finished but not claimed
            if (!current && D.quests != null)
                foreach (var q in D.quests) if (q.Done && !q.claimed) D.coins += q.reward;

            var rng = new System.Random(int.Parse(today));
            int reached = LevelReached;

            var normal = new List<QuestType> { QuestType.MergeChain, QuestType.ClearLevel, QuestType.EarnStars, QuestType.MergeAllChains };
            if (reached >= OrderQuestLevel) normal.Add(QuestType.ServeOrder);
            var hard = new List<QuestType> { QuestType.ReachLevel };
            if (reached >= ThreeStarQuestLevel) hard.Add(QuestType.ThreeStars);
            var easy = new[] { QuestType.MergeAny, QuestType.AddItem, QuestType.PlayLevel };

            D.quests = new List<Quest>
            {
                MakeQuest(easy[rng.Next(easy.Length)], rng, reached),
                MakeQuest(normal[rng.Next(normal.Count)], rng, reached),
                MakeQuest(hard[rng.Next(hard.Count)], rng, reached),
            };
            D.questDate = today;
            D.questVersion = QuestVersion;
            D.questBonusClaimed = false;
            Save();
        }

        static Quest MakeQuest(QuestType type, System.Random rng, int reached)
        {
            var q = new Quest { type = type };
            switch (type)
            {
                case QuestType.MergeAny: q.target = 10 + 5 * rng.Next(3); q.reward = q.target / 2; break; // 10/15/20 -> 5/7/10
                case QuestType.AddItem: q.target = 10; q.energy = 5; break;
                case QuestType.PlayLevel: q.target = 1; q.energy = 5; break;
                case QuestType.MergeChain: q.chain = rng.Next(ChainCount); q.target = 8; q.reward = 10; break;
                case QuestType.ClearLevel: q.target = 2; q.energy = 10; break;
                case QuestType.ServeOrder: q.target = 2; q.reward = 15; break;
                case QuestType.EarnStars: q.target = 5; q.reward = 15; break;
                case QuestType.MergeAllChains: q.target = MergesPerChain * ChainCount; q.reward = 12; q.perChain = new int[ChainCount]; break;
                case QuestType.ReachLevel: q.target = reached >= 9 ? 6 : 5; q.reward = 20; break;
                case QuestType.ThreeStars: q.target = 2; q.reward = 20; break;
            }
            return q;
        }

        // ------------------------------------------------------------------ collection rewards
        /// <summary>Reward for a newly discovered item, per merge stage (index 0 = stage 1).</summary>
        static readonly (int coins, int energy, int jewels)[] CollectionRewards =
        {
            (2, 0, 0), (0, 2, 0), (4, 0, 0), (0, 3, 0), (8, 0, 0), (0, 5, 0),
            (12, 0, 0), (0, 8, 0), (20, 0, 0), (0, 0, 1), (0, 0, 2),
        };

        public static (int coins, int energy, int jewels) CollectionReward(int level) =>
            level >= 1 && level <= CollectionRewards.Length ? CollectionRewards[level - 1] : (0, 0, 0);

        /// <summary>Discovered, but its reward was not taken yet.</summary>
        public static bool CollectionClaimable(int chain, int level) =>
            IsUnlocked(chain, level) && level <= CollectionRewards.Length && (D.collectionClaimed[chain] & (1 << (level - 1))) == 0;

        /// <summary>New entries in one chain, or in all chains (-1).</summary>
        public static int ClaimableCollectionCount(int chain = -1)
        {
            int n = 0;
            for (int c = 0; c < ChainCount; c++)
                if (chain < 0 || chain == c)
                    for (int lv = 1; lv <= CollectionRewards.Length; lv++) if (CollectionClaimable(c, lv)) n++;
            return n;
        }

        /// <summary>Takes the reward. False if there is none or its energy does not fit.</summary>
        public static bool ClaimCollection(int chain, int level)
        {
            if (!CollectionClaimable(chain, level)) return false;
            var r = CollectionReward(level);
            if (!EnergyFits(r.energy)) return false;
            D.collectionClaimed[chain] |= 1 << (level - 1);
            D.coins += r.coins;
            D.jewels += r.jewels;
            GainEnergy(r.energy);
            Commit();
            return true;
        }
    }
}
