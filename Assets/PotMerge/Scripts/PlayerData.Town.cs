using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    /// <summary>Town progress: open shops, decorations, idle income and the customers who come for a walk.</summary>
    public static partial class PlayerData
    {
        public const int TownIncomeHours = 8;       // income stops piling up after this long
        const int IncomePerShop = 2, IncomePerDecoration = 1; // coins per hour
        const int MaxGuests = 4;

        /// <summary>Clearing this level opens the shop (0 = open from the start).</summary>
        static readonly int[] ShopOpensAfter = { 0, 5, 10, 15 };

        public static int ShopLevel(int shop) => shop >= 0 && shop < ShopOpensAfter.Length ? ShopOpensAfter[shop] : 0;
        public static bool ShopOpen(int shop) => LevelReached > ShopLevel(shop);

        public static bool OwnsTownItem(string id) => D.townOwned.Contains(id);

        // ---- collection rewards
        public static int ChainUnlocked(int chain)
        {
            int n = 0;
            if (InRange(chain)) for (int lv = 1; lv <= CollectionRewards.Length; lv++) if (IsUnlocked(chain, lv)) n++;
            return n;
        }

        /// <summary>(found, needed) for a reward: its chain, or every chain for the plaza.</summary>
        public static (int found, int needed) TownRewardProgress(int shop)
        {
            int per = CollectionRewards.Length;
            if (shop < ChainCount) return (ChainUnlocked(shop), per);
            int n = 0;
            for (int c = 0; c < ChainCount; c++) n += ChainUnlocked(c);
            return (n, per * ChainCount);
        }

        static bool ShopUsable(TownDatabase.Item item) => item.shop >= ChainCount || ShopOpen(item.shop);

        /// <summary>Can be bought or claimed right now (price aside).</summary>
        public static bool TownItemAvailable(TownDatabase.Item item)
        {
            if (item == null || OwnsTownItem(item.id) || !ShopUsable(item)) return false;
            if (!item.reward) return true;
            var p = TownRewardProgress(item.shop);
            return p.found >= p.needed;
        }

        public static bool TownRewardReady(TownDatabase db)
        {
            if (db == null) return false;
            foreach (var i in db.items) if (i.reward && TownItemAvailable(i)) return true;
            return false;
        }

        /// <summary>Buys a decoration, or takes a collection reward for free.</summary>
        public static bool TakeTownItem(TownDatabase.Item item)
        {
            if (!TownItemAvailable(item)) return false;
            if (!item.reward)
            {
                if (item.currency == Currency.Jewel)
                {
                    if (D.jewels < item.price) return false;
                    D.jewels -= item.price;
                }
                else
                {
                    if (D.coins < item.price) return false;
                    D.coins -= item.price;
                }
            }
            AccrueIncome(); // what piled up so far counts at the old rate
            D.townOwned.Add(item.id);
            Commit();
            return true;
        }

        // ---- idle income
        /// <summary>Coins per hour: every open shop and every decoration.</summary>
        public static int TownIncomeRate
        {
            get
            {
                int rate = D.townOwned.Count * IncomePerDecoration;
                for (int s = 0; s < ChainCount; s++) if (ShopOpen(s)) rate += IncomePerShop;
                return rate;
            }
        }

        public static int TownIncomeMax => TownIncomeRate * TownIncomeHours;

        static void AccrueIncome()
        {
            long now = DateTime.UtcNow.Ticks;
            if (D.townLastTicks == 0 || now < D.townLastTicks) { D.townLastTicks = now; return; }
            double hours = TimeSpan.FromTicks(now - D.townLastTicks).TotalHours;
            D.townStored = Math.Min(D.townStored + hours * TownIncomeRate, TownIncomeMax);
            D.townLastTicks = now;
        }

        /// <summary>Whole coins waiting in the town.</summary>
        public static int TownIncome { get { AccrueIncome(); return (int)D.townStored; } }
        public static bool TownIncomeFull => TownIncome >= TownIncomeMax;

        public static int CollectTownIncome()
        {
            int coins = TownIncome;
            if (coins <= 0) return 0;
            D.townStored -= coins;
            D.coins += coins;
            Commit();
            return coins;
        }

        // ---- walking customers: the outfits of the last ones that were served
        public static IReadOnlyList<string> TownGuests => D.townGuests;

        public static void AddTownGuest(IEnumerable<string> costumeIds)
        {
            D.townGuests.Add(string.Join(",", costumeIds));
            while (D.townGuests.Count > MaxGuests) D.townGuests.RemoveAt(0);
            Commit();
        }
    }
}
