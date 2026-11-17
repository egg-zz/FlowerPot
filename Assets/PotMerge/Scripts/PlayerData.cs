using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    public enum QuestType
    {
        MergeAny,    // merge N times (any chain)
        MergeChain,  // merge N times in one chain
        AddItem,     // add N items with coins
        ReachLevel,  // make one level-N item (any chain)
    }

    [Serializable]
    public class Quest
    {
        public QuestType type;
        public int chain;     // MergeChain only
        public int target;
        public int progress;
        public int reward;
        public bool claimed;

        public bool Done => progress >= target;
    }

    /// <summary>
    /// Persistent player progress (PlayerPrefs JSON): coin wallet, best level and unlocked items per chain,
    /// sound setting and daily quests. Board layout is saved separately by <see cref="MergeBoard"/>.
    /// </summary>
    public static class PlayerData
    {
        public const int ChainCount = 4;
        public const int StartCoins = 15;
        const string Key = "PotMerge.Player.v1";

        [Serializable]
        class Data
        {
            public long coins = StartCoins;
            public int[] best = new int[ChainCount];
            public int[] unlockedMask = new int[ChainCount]; // bit (level-1) set = unlocked
            public bool soundOn = true;
            public string questDate = "";
            public List<Quest> quests = new List<Quest>();
            public long jewels;
            public long luckyBoxNextTicks;   // UTC ticks when the lucky box opens again (0 = ready now)
            public string adDate = "";
            public int adsToday;
        }

        static Data data;

        /// <summary>Raised whenever coins, unlocks, settings or quests change.</summary>
        public static event Action Changed;

        static Data D
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        // ------------------------------------------------------------------ coins
        public static long Coins => D.coins;

        public static void AddCoins(long amount)
        {
            if (amount == 0) return;
            D.coins = Math.Max(0, D.coins + amount);
            Commit();
        }

        public static bool TrySpend(long amount)
        {
            if (D.coins < amount) return false;
            D.coins -= amount;
            Commit();
            return true;
        }

        /// <summary>A new game always starts with at least <see cref="StartCoins"/>.</summary>
        public static void EnsureStartCoins()
        {
            if (D.coins >= StartCoins) return;
            D.coins = StartCoins;
            Commit();
        }

        // ------------------------------------------------------------------ jewels (premium currency)
        // Jewels come from purchases or the free daily lucky box. They buy coins and, later, premium costumes.
        // Rewarded ads only ever give coins.
        public const int LuckyBoxHours = 24;
        public const int LuckyBoxMinJewels = 1, LuckyBoxMaxJewels = 5;
        public const int AdCoins = 15;
        public const int AdsPerDay = 5;

        /// <summary>Jewel -> coin exchange packs (jewels, coins).</summary>
        public static readonly (int jewels, int coins)[] CoinPacks = { (5, 20), (12, 60), (25, 150) };

        public static long Jewels => D.jewels;

        public static void AddJewels(long amount)
        {
            if (amount == 0) return;
            D.jewels = Math.Max(0, D.jewels + amount);
            Commit();
        }

        /// <summary>Buys coins with jewels. Returns false when there are not enough jewels.</summary>
        public static bool BuyCoinPack(int index)
        {
            if (index < 0 || index >= CoinPacks.Length) return false;
            var pack = CoinPacks[index];
            if (D.jewels < pack.jewels) return false;
            D.jewels -= pack.jewels;
            D.coins += pack.coins;
            Commit();
            return true;
        }

        public static bool LuckyBoxReady => DateTime.UtcNow.Ticks >= D.luckyBoxNextTicks;

        public static TimeSpan LuckyBoxRemaining =>
            LuckyBoxReady ? TimeSpan.Zero : new TimeSpan(D.luckyBoxNextTicks - DateTime.UtcNow.Ticks);

        /// <summary>Opens the lucky box if it is ready. Returns the jewels won (0 if not ready).</summary>
        public static int OpenLuckyBox()
        {
            if (!LuckyBoxReady) return 0;
            int won = UnityEngine.Random.Range(LuckyBoxMinJewels, LuckyBoxMaxJewels + 1);
            D.jewels += won;
            D.luckyBoxNextTicks = DateTime.UtcNow.AddHours(LuckyBoxHours).Ticks;
            Commit();
            return won;
        }

        public static int AdsLeftToday
        {
            get
            {
                string today = DateTime.Now.ToString("yyyyMMdd");
                return D.adDate == today ? Mathf.Max(0, AdsPerDay - D.adsToday) : AdsPerDay;
            }
        }

        /// <summary>Call after a rewarded ad finished. Grants <see cref="AdCoins"/> and counts toward the daily limit.</summary>
        public static bool RewardAd()
        {
            if (AdsLeftToday <= 0) return false;
            string today = DateTime.Now.ToString("yyyyMMdd");
            if (D.adDate != today) { D.adDate = today; D.adsToday = 0; }
            D.adsToday++;
            D.coins += AdCoins;
            Commit();
            return true;
        }

        // ------------------------------------------------------------------ chains / collection
        public static int Best(int chain) => InRange(chain) ? D.best[chain] : 0;

        public static bool IsUnlocked(int chain, int level) =>
            InRange(chain) && level >= 1 && level <= 31 && (D.unlockedMask[chain] & (1 << (level - 1))) != 0;

        public static int UnlockedCount()
        {
            int n = 0;
            for (int c = 0; c < ChainCount; c++)
                for (int m = D.unlockedMask[c]; m != 0; m &= m - 1) n++;
            return n;
        }

        /// <summary>Records that an item exists on the board. Returns true if it was never seen before (new collection entry).</summary>
        public static bool ReportItem(int chain, int level)
        {
            if (!InRange(chain) || level < 1 || level > 31) return false;
            bool isNew = !IsUnlocked(chain, level);
            bool changed = isNew;
            if (isNew) D.unlockedMask[chain] |= 1 << (level - 1);
            if (level > D.best[chain]) { D.best[chain] = level; changed = true; }
            if (changed) Commit();
            return isNew;
        }

        // ------------------------------------------------------------------ settings
        public static bool SoundOn
        {
            get => D.soundOn;
            set { if (D.soundOn == value) return; D.soundOn = value; Commit(); }
        }

        // ------------------------------------------------------------------ daily quests
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
                case QuestType.AddItem: return $"Add {q.target} items";
                case QuestType.ReachLevel: return $"Make a level {q.target} item";
            }
            return "?";
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
                q.progress = Mathf.Min(q.progress, q.target);
            }
            Commit();
        }

        public static void OnAddItem()
        {
            EnsureTodayQuests();
            foreach (var q in D.quests)
                if (!q.claimed && q.type == QuestType.AddItem) q.progress = Mathf.Min(q.progress + 1, q.target);
            Commit();
        }

        /// <summary>Claims a finished quest and pays its reward. Returns the reward (0 if not claimable).</summary>
        public static int ClaimQuest(int index)
        {
            EnsureTodayQuests();
            if (index < 0 || index >= D.quests.Count) return 0;
            var q = D.quests[index];
            if (!q.Done || q.claimed) return 0;
            q.claimed = true;
            D.coins += q.reward;
            Commit();
            return q.reward;
        }

        public static int ClaimableQuestCount()
        {
            int n = 0;
            foreach (var q in Quests) if (q.Done && !q.claimed) n++;
            return n;
        }

        static void EnsureTodayQuests()
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            if (D.questDate == today && D.quests != null && D.quests.Count == 3) return;

            var rng = new System.Random(int.Parse(today));
            var types = new List<QuestType> { QuestType.MergeAny, QuestType.MergeChain, QuestType.AddItem, QuestType.ReachLevel };
            types.RemoveAt(rng.Next(types.Count)); // 3 different quest types per day

            D.quests = new List<Quest>();
            foreach (var t in types)
            {
                var q = new Quest { type = t };
                switch (t)
                {
                    case QuestType.MergeAny: q.target = 10 + 5 * rng.Next(3); q.reward = q.target / 2; break;      // 10/15/20 -> 5/7/10
                    case QuestType.MergeChain: q.chain = rng.Next(ChainCount); q.target = 5 + rng.Next(4); q.reward = q.target; break;
                    case QuestType.AddItem: q.target = 5 + 5 * rng.Next(2); q.reward = q.target; break;
                    case QuestType.ReachLevel: q.target = 5 + rng.Next(3); q.reward = 5 * (q.target - 3); break;  // L5/6/7 -> 10/15/20
                }
                D.quests.Add(q);
            }
            D.questDate = today;
            Save();
        }

        // ------------------------------------------------------------------ persistence
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key);
            data = new Data();
            Commit();
        }

        static bool InRange(int chain) => chain >= 0 && chain < ChainCount;

        static void Commit()
        {
            Save();
            Changed?.Invoke();
        }

        public static void Save()
        {
            if (data == null) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        static void Load()
        {
            data = null;
            if (PlayerPrefs.HasKey(Key))
            {
                try { data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key)); }
                catch (Exception e) { Debug.LogWarning("[PotMerge] Player data unreadable, starting fresh: " + e.Message); }
            }
            if (data == null) data = new Data();
            if (data.best == null || data.best.Length != ChainCount) Array.Resize(ref data.best, ChainCount);
            if (data.unlockedMask == null || data.unlockedMask.Length != ChainCount) Array.Resize(ref data.unlockedMask, ChainCount);
            if (data.quests == null) data.quests = new List<Quest>();
        }

#if UNITY_EDITOR
        // Domain reload may be disabled: make sure static state is re-read when entering play mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { data = null; Changed = null; }
#endif
    }
}
