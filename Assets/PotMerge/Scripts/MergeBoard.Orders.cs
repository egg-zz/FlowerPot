using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    /// <summary>
    /// Hamster orders (level 15+) are bonus quests: the level is cleared by its goals alone, but when the goals are done first,
    /// the customers still to come may be served in "bonus time" (until they leave, time runs out or the player taps FINISH).
    /// Now and then a customer walks in
    /// and asks for 1-3 items; drag exactly those (same chain and level) onto them for 10-30 coins by difficulty and 3 energy.
    /// A customer who waits too long leaves again.
    /// </summary>
    public partial class MergeBoard
    {
        /// <summary>A level started: no customer yet.</summary>
        public event Action OrdersReset;
        /// <summary>A customer starts walking in.</summary>
        public event Action CustomerArriving;
        /// <summary>An item was handed over; true = that finished the order.</summary>
        public event Action<bool> OrderItemDelivered;
        /// <summary>The dropped item is not part of the order.</summary>
        public event Action OrderRefused;
        /// <summary>The customer waited too long.</summary>
        public event Action CustomerLost;

        enum CustomerPhase { None, Waiting, Arriving, Open, Served, Leaving }

        const int DeliverTutorialLevel = 15;
        const float WalkSeconds = 2.6f, ServedSeconds = 1.1f;

        CustomerPhase phase;
        float phaseTime;         // seconds left in the phase (patience while Open)
        float patienceTotal;     
        int orderIndex, ordersServed;
        bool anyDelivered;
        readonly List<(int chain, int level)> orderItems = new List<(int, int)>();
        readonly List<bool> orderDone = new List<bool>();

        int OrderCount => level != null && level.orders != null ? level.orders.Length : 0;
        bool OrderOpen => phase == CustomerPhase.Open;
        public bool HasOrders => state != LevelState.None && OrderCount > 0;

        /// <summary>0 = outside, 1 = at the table.</summary>
        public float CustomerPresence =>
            phase == CustomerPhase.Arriving ? 1f - Mathf.Clamp01(phaseTime / WalkSeconds) :
            phase == CustomerPhase.Leaving ? Mathf.Clamp01(phaseTime / WalkSeconds) :
            phase == CustomerPhase.Open || phase == CustomerPhase.Served ? 1f : 0f;

        /// <summary>1 = walking in, -1 = walking out, 0 = standing.</summary>
        public int CustomerMoving =>
            OrdersPaused ? 0 : phase == CustomerPhase.Arriving ? 1 : phase == CustomerPhase.Leaving ? -1 : 0;

        bool OrdersPaused => state != LevelState.Playing || InputBlocked;

        void StartOrders()
        {
            orderIndex = ordersServed = 0;
            anyDelivered = false;
            orderItems.Clear();
            orderDone.Clear();
            phase = CustomerPhase.None;
            hud?.HideDeliverGhost();
            hud?.HideOrderBubble();
            hud?.SetOrderLayout(OrderCount > 0);
            hud?.SetLevelStars(OrderCount > 0 ? OrderStars : 0);
            hud?.ShowFinish(false);
            OrdersReset?.Invoke();
            if (OrderCount > 0) WaitForCustomer(UnityEngine.Random.Range(2f, 4f));
        }

        void WaitForCustomer(float seconds)
        {
            phase = orderIndex < OrderCount ? CustomerPhase.Waiting : CustomerPhase.None;
            phaseTime = seconds;
        }

        void OpenOrder()
        {
            orderItems.Clear();
            orderDone.Clear();
            orderItems.AddRange(LevelDatabase.ParseOrder(level.orders[orderIndex]));
            foreach (var _ in orderItems) orderDone.Add(false);
            int units = LevelDatabase.OrderUnits(orderItems);
            patienceTotal = Timed ? 30f + 1.5f * units : 60f + 2f * units;
            phase = CustomerPhase.Open;
            phaseTime = patienceTotal;
            RefreshOrder();
        }

        /// <summary>1 star for the goals, up to 3 when every customer was served.</summary>
        int OrderStars => OrderCount > 0 ? 1 + 2 * ordersServed / OrderCount : 1;

        /// <summary>A customer is here or still to come who is worth serving: the order pays, or more stars than before are possible.</summary>
        bool BonusLeft
        {
            get
            {
                if (OrderCount == 0 || phase == CustomerPhase.None) return false;
                int next = orderIndex + (phase == CustomerPhase.Served ? 1 : 0);
                if (1 + 2 * (ordersServed + OrderCount - next) / OrderCount > Mathf.Max(OrderStars, PlayerData.Stars(levelNumber))) return true;
                for (int i = next; i < OrderCount; i++)
                    if (PlayerData.OrderPays(levelNumber, i)) return true;
                return false;
            }
        }

        /// <summary>All goals are done. Clears the level (true), or starts bonus time while customers can still be served.</summary>
        bool GoalsReached()
        {
            if (bonusTime) return false;
            clearTimeLeft = timeLeft;
            if (!BonusLeft) { Clear(); return true; }
            bonusTime = true;
            if (phase == CustomerPhase.Waiting) phaseTime = Mathf.Min(phaseTime, 0.5f);
            SoundFx.Unlock();
            hud?.ShowToast("GOALS DONE! BONUS TIME", new Color(0.7f, 1f, 0.7f));
            hud?.ShowFinish(true);
            return false;
        }

        void FinishBonus()
        {
            if (bonusTime && busyMerges == 0) Clear();
        }

        void LeaveCustomer()
        {
            orderIndex++;
            phase = CustomerPhase.Leaving;
            phaseTime = WalkSeconds;
            hud?.HideOrderBubble();
            RefreshGoals();
            RefreshState();
        }

        void RefreshOrder()
        {
            if (hud == null || !OrderOpen) return;
            var views = new List<MergeHUD.OrderView>();
            for (int i = 0; i < orderItems.Count; i++)
                views.Add(new MergeHUD.OrderView
                {
                    icon = database.Icon(orderItems[i].chain, orderItems[i].level),
                    done = orderDone[i],
                    ready = !orderDone[i] && FindOnBoard(orderItems[i].chain, orderItems[i].level) != null
                });
            hud.SetOrder(views, OrderPays ? LevelDatabase.OrderReward(orderItems) : 0, PlayerData.OrderEnergy);
        }

        /// <summary>Orders pay once each, until the level is cleared for the first time.</summary>
        bool OrderPays => PlayerData.OrderPays(levelNumber, orderIndex);

        PotItem FindOnBoard(int chain, int lv)
        {
            foreach (var p in grid)
                if (p != null && !p.IsGenerator && p.Chain == chain && p.Level == lv) return p;
            return null;
        }

        int WantedSlot(PotItem item)
        {
            if (!OrderOpen || item == null || item.IsGenerator) return -1;
            for (int i = 0; i < orderItems.Count; i++)
                if (!orderDone[i] && orderItems[i].chain == item.Chain && orderItems[i].level == item.Level) return i;
            return -1;
        }

        bool HasDeliverable()
        {
            foreach (var p in grid) if (WantedSlot(p) >= 0) return true;
            return false;
        }

        bool OverCustomer(Vector2 screen) => hud != null && OrderOpen && hud.IsOverDelivery(screen);

        /// <summary>The dragged item was let go over the customer.</summary>
        void Deliver(PotItem item)
        {
            item.SetLifted(false);
            int slot = state == LevelState.Playing ? WantedSlot(item) : -1;
            if (slot < 0)
            {
                item.MoveTo(CellToWorld(item.Cell), 0.2f);
                if (OrderOpen) { OrderRefused?.Invoke(); SoundFx.Error(); }
                return;
            }

            grid[item.Cell.x, item.Cell.y] = null;
            Destroy(item.gameObject);
            orderDone[slot] = true;
            anyDelivered = true;
            SoundFx.Pop();
            bool finished = !orderDone.Contains(false);
            RefreshOrder();
            if (finished) FinishOrder();
            OrderItemDelivered?.Invoke(finished);
            RefreshState();
        }

        void FinishOrder()
        {
            int coins = OrderPays ? LevelDatabase.OrderReward(orderItems) : 0;
            if (coins > 0)
            {
                int energy = PlayerData.PayOrder(levelNumber, orderIndex, coins);
                hud?.ShowToast(energy > 0 ? $"BONUS! +{coins} COINS, +{energy} ENERGY" : $"BONUS! +{coins} COINS", new Color(1f, 0.85f, 0.3f));
                hud?.SetCoins(Coins, true);
                SoundFx.Coin();
            }
            else hud?.ShowToast("ORDER DONE!", Color.white);
            ordersServed++;
            PlayerData.OnOrderServed();
            hud?.SetLevelStars(OrderStars);
            phase = CustomerPhase.Served;
            phaseTime = ServedSeconds;
            RefreshGoals();
        }

        /// <summary>Customers coming and going, delivery feedback while dragging, and the "drag it here" ghost on the first order level.</summary>
        void TickOrders()
        {
            if (hud == null || OrderCount == 0) return;
            if (!OrdersPaused) TickCustomer();

            int mode = 0;
            if (dragging != null && OverCustomer(Input.mousePosition)) mode = WantedSlot(dragging) >= 0 ? 1 : 2;
            hud.SetDeliveryHighlight(mode);
            hud.SetPatience(OrderOpen && patienceTotal > 0f ? Mathf.Clamp01(phaseTime / patienceTotal) : -1f, phaseTime);

            PotItem show = null;
            if (levelNumber == DeliverTutorialLevel && !anyDelivered && dragging == null && !OrdersPaused)
                foreach (var p in grid) if (WantedSlot(p) >= 0) { show = p; break; }
            if (show != null) hud.ShowDeliverGhost(show.transform.position, database.Icon(show.Chain, show.Level));
            else hud.HideDeliverGhost();
        }

        void TickCustomer()
        {
            if (phase == CustomerPhase.None) return;
            if (OrderOpen && patienceTotal <= 0f) return; // waits forever
            phaseTime -= Time.deltaTime;
            if (phaseTime > 0f) return;
            switch (phase)
            {
                case CustomerPhase.Waiting:
                    phase = CustomerPhase.Arriving;
                    phaseTime = WalkSeconds;
                    hud?.ShowToast("A CUSTOMER IS COMING!", new Color(1f, 0.9f, 0.6f));
                    CustomerArriving?.Invoke();
                    break;
                case CustomerPhase.Arriving:
                    OpenOrder();
                    break;
                case CustomerPhase.Open:
                    hud?.ShowToast("The customer left...", new Color(1f, 0.75f, 0.7f));
                    SoundFx.Error();
                    CustomerLost?.Invoke();
                    LeaveCustomer();
                    break;
                case CustomerPhase.Served:
                    LeaveCustomer();
                    break;
                case CustomerPhase.Leaving:
                    if (bonusTime && !BonusLeft) { phase = CustomerPhase.None; Clear(); break; }
                    WaitForCustomer(bonusTime ? 0.5f : UnityEngine.Random.Range(6f, 10f));
                    break;
            }
        }
    }
}
