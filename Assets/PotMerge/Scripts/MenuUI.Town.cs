using System;
using UnityEngine;
using UnityEngine.UIElements;
using static PotMerge.UIKit;

namespace PotMerge
{
    /// <summary>The town screen's UI: wallet, idle income, shop tabs and the decorations of the chosen shop.</summary>
    public partial class MenuUI
    {
        public event Action TownClicked;
        public event Action TownClosed;

        public TownScreen town;

        VisualElement townLayer, townTabs, townCards, townPanel;
        Label townCoins, townJewels, townIncome, townRate, townNote, townBadge;
        Button townCollect;
        int townShop = -1; // -1 = whole town
        float townTick;

        static readonly string[] TownTabNames = { "FLOWER", "CAFE", "BAKERY", "ICE", "PLAZA" };

        VisualElement BuildTown()
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            Stretch(layer);

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.position = Position.Absolute;
            top.style.left = 40; top.style.right = 40; top.style.top = 70;
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.alignItems = Align.Center;
            var wallet = new VisualElement { pickingMode = PickingMode.Ignore };
            wallet.style.flexDirection = FlexDirection.Row;
            wallet.Add(CurrencyPill(false, out townCoins));
            var j = CurrencyPill(true, out townJewels);
            j.style.marginLeft = 16; j.style.minWidth = 150;
            wallet.Add(j);
            top.Add(wallet);
            top.Add(IconButton(IconShape.Home, 110, () => TownClosed?.Invoke()));
            layer.Add(top);

            // idle income
            var income = new VisualElement { pickingMode = PickingMode.Ignore };
            income.style.position = Position.Absolute;
            income.style.left = 0; income.style.right = 0; income.style.top = 205;
            income.style.alignItems = Align.Center;
            townCollect = BigButton("", 430, 116, 40, Gold, Color.Lerp(Gold, Color.black, 0.3f), CollectIncome);
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            line.Add(CoinIcon(52, 12));
            townIncome = MakeLabel("COLLECT", 42, Color.white);
            line.Add(townIncome);
            townCollect.Add(line);
            income.Add(townCollect);
            townRate = MakeLabel("", 28, Ink);
            townRate.style.marginTop = 10;
            income.Add(townRate);
            layer.Add(income);

            // bottom: cards of the chosen shop, then the tabs
            var bottom = new VisualElement { pickingMode = PickingMode.Ignore };
            bottom.style.position = Position.Absolute;
            bottom.style.left = 20; bottom.style.right = 20; bottom.style.bottom = 60;
            townPanel = new VisualElement();
            townPanel.style.backgroundColor = new Color(1f, 0.98f, 0.93f, 0.96f);
            Round(townPanel, 56);
            townPanel.style.paddingLeft = 14; townPanel.style.paddingRight = 14; townPanel.style.paddingTop = 18; townPanel.style.paddingBottom = 18;
            townPanel.style.marginBottom = 20;
            townNote = MakeLabel("", 28, InkSoft);
            townNote.style.unityTextAlign = TextAnchor.MiddleCenter;
            townNote.style.marginBottom = 10;
            townPanel.Add(townNote);
            townCards = new VisualElement { pickingMode = PickingMode.Ignore };
            townCards.style.flexDirection = FlexDirection.Row;
            townCards.style.justifyContent = Justify.Center;
            townPanel.Add(townCards);
            bottom.Add(townPanel);
            townTabs = new VisualElement { pickingMode = PickingMode.Ignore };
            townTabs.style.flexDirection = FlexDirection.Row;
            townTabs.style.justifyContent = Justify.SpaceBetween;
            bottom.Add(townTabs);
            layer.Add(bottom);
            return layer;
        }

        public bool TownVisible => townLayer != null && townLayer.style.display != DisplayStyle.None;

        public void ShowTown()
        {
            HideScreens();
            Show(townLayer, true);
            townShop = -1;
            RefreshTown();
        }

        void CollectIncome()
        {
            int coins = PlayerData.CollectTownIncome();
            if (coins > 0) SoundFx.Coin(); else SoundFx.Error();
            RefreshTown();
        }

        void PickTownShop(int shop)
        {
            townShop = townShop == shop ? -1 : shop;
            if (town != null) town.Focus(townShop);
            RefreshTown();
        }

        void RefreshTownIncome()
        {
            int income = PlayerData.TownIncome;
            townIncome.text = income > 0 ? $"COLLECT +{income}" : "COLLECT";
            SetButtonColors(townCollect, income > 0 ? Gold : Disabled, income > 0 ? Color.Lerp(Gold, Color.black, 0.3f) : DisabledDark);
            townRate.text = PlayerData.TownIncomeFull ? "FULL! Collect to earn more" : $"{PlayerData.TownIncomeRate} coins per hour, up to {PlayerData.TownIncomeMax}";
        }

        void RefreshTown()
        {
            townCoins.text = Format(PlayerData.Coins);
            townJewels.text = Format(PlayerData.Jewels);
            RefreshTownIncome();

            townTabs.Clear();
            var db = town != null ? town.town : null;
            for (int s = 0; s < TownTabNames.Length; s++)
            {
                int shop = s;
                bool open = s >= PlayerData.ChainCount || PlayerData.ShopOpen(s);
                bool sel = townShop == s;
                Color face = sel ? (s < database.ChainCount ? database.Get(s).color : Pink) : open ? Cream : new Color(0.82f, 0.80f, 0.76f);
                var tab = BigButton("", 198, 116, 30, face, Color.Lerp(face, Color.black, 0.25f), () => PickTownShop(shop));
                tab.Add(MakeLabel(TownTabNames[s], 30, sel ? Color.white : Ink));
                if (!open) tab.Add(MakeLabel($"Clear Lv {PlayerData.ShopLevel(s)}", 22, InkSoft));
                if (db != null)
                    foreach (var i in db.items)
                        if (i.reward && i.shop == s && PlayerData.TownItemAvailable(i)) { tab.Add(RedDot(34, -6, -4)); break; }
                townTabs.Add(tab);
            }

            Show(townPanel, townShop >= 0);
            townCards.Clear();
            if (townShop < 0 || db == null) return;
            bool shopOpen = townShop >= PlayerData.ChainCount || PlayerData.ShopOpen(townShop);
            townNote.text = shopOpen ? "Every decoration earns +1 coin per hour" : $"Opens after clearing level {PlayerData.ShopLevel(townShop)}";
            foreach (var item in db.items) if (item.shop == townShop) townCards.Add(TownCard(item, shopOpen));
        }

        VisualElement TownCard(TownDatabase.Item item, bool shopOpen)
        {
            bool owned = PlayerData.OwnsTownItem(item.id);
            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.style.width = 192; card.style.height = 320;
            card.style.marginLeft = 4; card.style.marginRight = 4;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.paddingTop = 14; card.style.paddingBottom = 14;
            card.style.backgroundColor = item.reward ? new Color(1f, 0.90f, 0.66f) : new Color(0.96f, 0.92f, 0.84f);
            Round(card, 46);
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = 130; icon.style.height = 130;
            if (item.icon != null) icon.style.backgroundImage = new StyleBackground(item.icon);
            if (!shopOpen) icon.style.unityBackgroundImageTintColor = new Color(0.35f, 0.3f, 0.28f, 0.35f);
            card.Add(icon);
            var name = MakeLabel(item.displayName, 25, Ink);
            name.style.whiteSpace = WhiteSpace.Normal;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(name);

            if (owned) { card.Add(Icon(IconShape.Check, 66, Green)); return card; }
            if (!shopOpen) { card.Add(Icon(IconShape.Lock, 60, Disabled)); return card; }
            if (item.reward)
            {
                var p = PlayerData.TownRewardProgress(item.shop);
                if (PlayerData.TownItemAvailable(item)) card.Add(BigButton("GET", 168, 84, 34, Pink, PinkDark, () => TakeTownItem(item)));
                else
                {
                    var need = MakeLabel($"COLLECTION\n{p.found}/{p.needed}", 24, InkSoft);
                    need.style.unityTextAlign = TextAnchor.MiddleCenter;
                    card.Add(need);
                }
                return card;
            }
            bool jewel = item.currency == Currency.Jewel;
            bool afford = jewel ? PlayerData.Jewels >= item.price : PlayerData.Coins >= item.price;
            Color face = !afford ? Disabled : jewel ? JewelColor : Gold;
            var btn = BigButton("", 168, 84, 30, face, afford ? Color.Lerp(face, Color.black, 0.3f) : DisabledDark, () => { if (afford) AskTownItem(item); else SoundFx.Error(); });
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.style.flexDirection = FlexDirection.Row; line.style.alignItems = Align.Center;
            line.Add(jewel ? Icon(IconShape.Gem, 36, Color.white) : CoinIcon(36, 4));
            line.Add(MakeLabel(" " + item.price, 34, Color.white));
            btn.Add(line);
            card.Add(btn);
            return card;
        }

        void AskTownItem(TownDatabase.Item item)
        {
            var popup = Popup("BUY?", 760, out var body, ClosePopups);
            body.style.alignItems = Align.Center;
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = 240; icon.style.height = 240;
            if (item.icon != null) icon.style.backgroundImage = new StyleBackground(item.icon);
            body.Add(icon);
            body.Add(MakeLabel(item.displayName, 48, Ink));
            bool jewel = item.currency == Currency.Jewel;
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.style.flexDirection = FlexDirection.Row; line.style.alignItems = Align.Center;
            line.style.marginTop = 10; line.style.marginBottom = 34;
            line.Add(jewel ? JewelIcon(56) : CoinIcon(56, 12));
            line.Add(MakeLabel(item.price.ToString(), 52, Ink));
            body.Add(line);
            body.Add(YesNo("YES", () => TakeTownItem(item), "NO", ClosePopups));
            OpenPopup(popup);
        }

        static VisualElement YesNo(string yes, Action onYes, string no, Action onNo)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            var y = BigButton(yes, 280, 124, 46, Green, GreenDark, onYes);
            y.style.marginRight = 28;
            row.Add(y);
            row.Add(BigButton(no, 280, 124, 46, new Color(0.62f, 0.66f, 0.72f), new Color(0.45f, 0.48f, 0.54f), onNo));
            return row;
        }

        void TakeTownItem(TownDatabase.Item item)
        {
            ClosePopups();
            if (PlayerData.TakeTownItem(item))
            {
                SoundFx.Unlock();
                if (town != null) town.Refresh(item.id);
            }
            else SoundFx.Error();
            RefreshTown();
        }

        void AnimateTown()
        {
            if (!TownVisible) return;
            townTick += Time.unscaledDeltaTime;
            if (townTick < 1f) return;
            townTick = 0f;
            RefreshTownIncome();
        }

        // ------------------------------------------------------------------ leaving a level
        /// <summary>Asks before a level in progress is given up. The game waits while this is open.</summary>
        public void AskLeave(Action leave)
        {
            var popup = Popup("LEAVE?", 820, out var body, ClosePopups);
            body.style.alignItems = Align.Center;
            var text = MakeLabel("Do you really want to leave?\nYour progress and used energy\nwill not come back.", 38, Ink);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.marginBottom = 28;
            body.Add(text);
            var buttons = YesNo("YES", () => { ClosePopups(); leave?.Invoke(); }, "NO", ClosePopups);
            buttons.style.marginTop = 30;
            body.Add(buttons);
            OpenPopup(popup);
        }

        // ------------------------------------------------------------------ collection reward message
        void ShowCollectionReward(int chain, int level)
        {
            var r = PlayerData.CollectionReward(level);
            var popup = Popup("NEW ITEM!", 820, out var body, OpenCollection);
            body.style.alignItems = Align.Center;
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = 280; icon.style.height = 280;
            var tex = database.Icon(chain, level);
            if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
            body.Add(icon);
            body.Add(MakeLabel(database.LevelName(chain, level), 60, Ink));
            var text = MakeLabel($"You found {database.Get(chain).displayName} stage {level}!", 34, InkSoft);
            text.style.marginTop = 6; text.style.marginBottom = 26;
            body.Add(text);
            body.Add(MakeLabel("YOU GOT", 32, InkSoft));
            var line = RewardLine(r.coins, r.energy, r.jewels, 68, Ink);
            line.style.marginTop = 6; line.style.marginBottom = 10;
            body.Add(line);
            string what = r.coins > 0 ? $"{r.coins} coins" : r.energy > 0 ? $"{r.energy} energy" : r.jewels == 1 ? "1 jewel" : $"{r.jewels} jewels";
            body.Add(MakeLabel(what, 34, InkSoft));
            var ok = BigButton("OK", 360, 130, 52, Green, GreenDark, OpenCollection);
            ok.style.marginTop = 34;
            body.Add(ok);
            OpenPopup(popup);
        }
    }
}
