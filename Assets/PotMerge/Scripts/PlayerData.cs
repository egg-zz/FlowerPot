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
            public List<string> ownedCostumes = new List<string>();
            public string[] equipped = new string[5]; // costume id per CostumeSlot, "" = none
            public int levelReached = 1;          // highest level the player may play (1-based)
            public List<int> levelStars = new List<int>(); // best stars per level, index = level - 1
            public int hearts = MaxHearts;
            public long heartNextTicks;           // UTC ticks when the next heart comes back (0 = none pending)
            public int energy = MaxEnergy;        // spent by tapping item boxes on the board
            public long energyNextTicks;          // UTC ticks when the next energy point comes back
            public bool energyInit;               // false in saves from before energy existed
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

        // ------------------------------------------------------------------ owner costumes
        public static bool OwnsCostume(string id) => D.ownedCostumes.Contains(id);

        /// <summary>Buys a costume with coins or jewels. Returns false if already owned or not affordable.</summary>
        public static bool BuyCostume(string id, Currency currency, int price)
        {
            if (string.IsNullOrEmpty(id) || OwnsCostume(id)) return false;
            if (currency == Currency.Coin) { if (D.coins < price) return false; D.coins -= price; }
            else { if (D.jewels < price) return false; D.jewels -= price; }
            D.ownedCostumes.Add(id);
            Commit();
            return true;
        }

        public static string Equipped(CostumeSlot slot) => D.equipped[(int)slot] ?? "";

        /// <summary>Wears an owned costume (replacing the one in the same slot), or takes it off if it is already worn.</summary>
        public static void ToggleEquip(string id, CostumeSlot slot)
        {
            if (!OwnsCostume(id)) return;
            int s = (int)slot;
            D.equipped[s] = D.equipped[s] == id ? "" : id;
            Commit();
        }

        // ------------------------------------------------------------------ levels
        public const int ClearEnergy = 3;          // energy for every clear (replays too)
        public const int JewelLevelInterval = 10;  // every 10th level also gives jewels
        public const int JewelLevelReward = 3;

        public static int LevelReached => Mathf.Max(1, D.levelReached);

        public static int Stars(int level) =>
            level >= 1 && level <= D.levelStars.Count ? D.levelStars[level - 1] : 0;

        /// <summary>Coins for a first clear: a random amount that grows with the stars (1: 2-3, 2: 3-6, 3: 4-9).</summary>
        public static int RollClearCoins(int stars)
        {
            stars = Mathf.Clamp(stars, 1, 3);
            return UnityEngine.Random.Range(stars + 1, stars * 3 + 1);
        }

        /// <summary>
        /// Records a cleared level. Every clear gives <see cref="ClearEnergy"/> energy (it may go above the max).
        /// The first clear also gives a random coin amount (<see cref="RollClearCoins"/>) and, on every 10th level,
        /// <see cref="JewelLevelReward"/> jewels.
        /// </summary>
        public static (int coins, int jewels, int energy) CompleteLevel(int level, int stars)
        {
            if (level < 1) return (0, 0, 0);
            while (D.levelStars.Count < level) D.levelStars.Add(0);
            bool firstClear = level >= D.levelReached;
            D.levelStars[level - 1] = Mathf.Max(D.levelStars[level - 1], stars);
            int coins = 0, jewels = 0, energy = ClearEnergy;
            RegenEnergy();
            D.energy += energy;
            if (D.energy >= MaxEnergy) D.energyNextTicks = 0;
            if (firstClear)
            {
                coins = RollClearCoins(stars);
                if (level % JewelLevelInterval == 0) jewels = JewelLevelReward;
                D.coins += coins;
                D.jewels += jewels;
                D.levelReached = level + 1;
            }
            Commit();
            return (coins, jewels, energy);
        }

        // ------------------------------------------------------------------ hearts (lives)
        public const int MaxHearts = 5;
        public const int HeartMinutes = 30;
        public const int HeartRefillJewels = 10;

        /// <summary>Current hearts, after adding the ones that came back while away.</summary>
        public static int Hearts { get { RegenHearts(); return D.hearts; } }

        /// <summary>Time until the next heart comes back (zero when full).</summary>
        public static TimeSpan HeartRemaining
        {
            get
            {
                RegenHearts();
                if (D.hearts >= MaxHearts || D.heartNextTicks == 0) return TimeSpan.Zero;
                return new TimeSpan(Math.Max(0, D.heartNextTicks - DateTime.UtcNow.Ticks));
            }
        }

        static void RegenHearts()
        {
            if (D.hearts >= MaxHearts) { D.heartNextTicks = 0; return; }
            long now = DateTime.UtcNow.Ticks;
            long step = TimeSpan.FromMinutes(HeartMinutes).Ticks;
            if (D.heartNextTicks == 0) { D.heartNextTicks = now + step; Save(); return; }
            if (now < D.heartNextTicks) return;
            int gained = 1 + (int)((now - D.heartNextTicks) / step);
            D.hearts = Mathf.Min(MaxHearts, D.hearts + gained);
            D.heartNextTicks = D.hearts >= MaxHearts ? 0 : D.heartNextTicks + gained * step;
            Commit();
        }

        /// <summary>A failed level costs one heart.</summary>
        public static void LoseHeart()
        {
            RegenHearts();
            if (D.hearts <= 0) return;
            D.hearts--;
            if (D.heartNextTicks == 0) D.heartNextTicks = DateTime.UtcNow.AddMinutes(HeartMinutes).Ticks;
            Commit();
        }

        public static void AddHeart()
        {
            RegenHearts();
            D.hearts = Mathf.Min(MaxHearts, D.hearts + 1);
            if (D.hearts >= MaxHearts) D.heartNextTicks = 0;
            Commit();
        }

        /// <summary>Refills all hearts for <see cref="HeartRefillJewels"/> jewels.</summary>
        public static bool RefillHeartsWithJewels()
        {
            if (D.jewels < HeartRefillJewels) return false;
            D.jewels -= HeartRefillJewels;
            D.hearts = MaxHearts;
            D.heartNextTicks = 0;
            Commit();
            return true;
        }

        // ------------------------------------------------------------------ energy (item boxes)
        public const int MaxEnergy = 100;
        public const int EnergyMinutes = 2;        // 1 energy comes back every 2 minutes
        public const int AdEnergy = 20;
        public const int EnergyRefillJewels = 10;

        /// <summary>Current energy, after adding what came back while away.</summary>
        public static int Energy { get { RegenEnergy(); return D.energy; } }

        public static TimeSpan EnergyRemaining
        {
            get
            {
                RegenEnergy();
                if (D.energy >= MaxEnergy || D.energyNextTicks == 0) return TimeSpan.Zero;
                return new TimeSpan(Math.Max(0, D.energyNextTicks - DateTime.UtcNow.Ticks));
            }
        }

        static void RegenEnergy()
        {
            if (D.energy >= MaxEnergy) { D.energyNextTicks = 0; return; }
            long now = DateTime.UtcNow.Ticks;
            long step = TimeSpan.FromMinutes(EnergyMinutes).Ticks;
            if (D.energyNextTicks == 0) { D.energyNextTicks = now + step; Save(); return; }
            if (now < D.energyNextTicks) return;
            int gained = 1 + (int)((now - D.energyNextTicks) / step);
            D.energy = Mathf.Min(MaxEnergy, D.energy + gained);
            D.energyNextTicks = D.energy >= MaxEnergy ? 0 : D.energyNextTicks + gained * step;
            Commit();
        }

        public static bool TrySpendEnergy(int amount)
        {
            RegenEnergy();
            if (D.energy < amount) return false;
            D.energy -= amount;
            if (D.energyNextTicks == 0 && D.energy < MaxEnergy) D.energyNextTicks = DateTime.UtcNow.AddMinutes(EnergyMinutes).Ticks;
            Commit();
            return true;
        }

        /// <summary>Rewarded ad for energy (counts toward the daily ad limit).</summary>
        public static bool RewardAdEnergy()
        {
            if (AdsLeftToday <= 0) return false;
            string today = DateTime.Now.ToString("yyyyMMdd");
            if (D.adDate != today) { D.adDate = today; D.adsToday = 0; }
            D.adsToday++;
            RegenEnergy();
            D.energy = Mathf.Min(MaxEnergy, D.energy + AdEnergy);
            if (D.energy >= MaxEnergy) D.energyNextTicks = 0;
            Commit();
            return true;
        }

        public static bool RefillEnergyWithJewels()
        {
            if (D.jewels < EnergyRefillJewels) return false;
            D.jewels -= EnergyRefillJewels;
            D.energy = MaxEnergy;
            D.energyNextTicks = 0;
            Commit();
            return true;
        }

        /// <summary>Pays jewels for extra time in a level.</summary>
        public static bool TrySpendJewels(long amount)
        {
            if (D.jewels < amount) return false;
            D.jewels -= amount;
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
                case QuestType.AddItem: return $"Tap item boxes {q.target} times";
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
            if (data.ownedCostumes == null) data.ownedCostumes = new List<string>();
            // the lucky box interval got shorter: never wait longer than the current interval
            long maxNext = DateTime.UtcNow.AddHours(LuckyBoxHours).Ticks;
            if (data.luckyBoxNextTicks > maxNext) data.luckyBoxNextTicks = maxNext;
            if (data.equipped == null || data.equipped.Length != 5) Array.Resize(ref data.equipped, 5);
            if (data.levelStars == null) data.levelStars = new List<int>();
            if (data.levelReached < 1) data.levelReached = 1;
            // saves from before hearts existed deserialize hearts as 0: start them full
            if (data.hearts <= 0 && data.heartNextTicks == 0) data.hearts = MaxHearts;
            if (!data.energyInit) { data.energy = MaxEnergy; data.energyNextTicks = 0; data.energyInit = true; }
        }

#if UNITY_EDITOR
        // Domain reload may be disabled: make sure static state is re-read when entering play mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { data = null; Changed = null; }
#endif
    }
}
