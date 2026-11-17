using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static PotMerge.UIKit;

namespace PotMerge
{
    /// <summary>
    /// Runtime-built UI Toolkit menus: title screen, home screen and the quests / settings / collection / shop popups.
    /// Uses the same PanelSettings as the game HUD and sits above it (higher sorting order).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MenuUI : MonoBehaviour
    {
        public ChainDatabase database;
        public MergeBoard board;
        [Tooltip("Illustrated title logo. When empty, the title is built from letters.")]
        public Texture2D titleImage;
        public float titleImageWidth = 900f;

        public event Action TitleTapped;
        public event Action StartClicked;

        public bool PopupOpen => popupLayer != null && popupLayer.childCount > 0;

        static readonly Color[] LetterColors =
        {
            new Color(0.98f, 0.45f, 0.60f), new Color(1f, 0.72f, 0.25f), new Color(0.40f, 0.78f, 0.55f),
            new Color(0.55f, 0.65f, 1f), new Color(0.85f, 0.52f, 0.95f), new Color(1f, 0.55f, 0.40f),
        };

        VisualElement root;
        VisualElement titleLayer, homeLayer, popupLayer;
        readonly List<VisualElement> titleLetters = new List<VisualElement>();
        VisualElement titleBlock;
        Label tapLabel;

        // home widgets
        Label homeCoins;
        Label homeJewels;
        Label questBadge;
        Label luckyBadge;
        Label luckyLabel;
        float luckyTick;
        Button startButton;

        bool dirty = true;
        bool questsOpen;

        // ------------------------------------------------------------------ build
        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            Stretch(root);

            titleLayer = BuildTitle();
            homeLayer = BuildHome();
            popupLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            Stretch(popupLayer);

            root.Add(titleLayer);
            root.Add(homeLayer);
            root.Add(popupLayer);
            HideScreens();

            PlayerData.Changed += MarkDirty;
        }

        void OnDisable() => PlayerData.Changed -= MarkDirty;

        void MarkDirty() => dirty = true;

        VisualElement BuildTitle()
        {
            var layer = new VisualElement();
            Stretch(layer);
            layer.pickingMode = PickingMode.Position;
            layer.RegisterCallback<PointerDownEvent>(_ => { SoundFx.Pop(); TitleTapped?.Invoke(); });

            titleBlock = new VisualElement { pickingMode = PickingMode.Ignore };
            titleBlock.style.position = Position.Absolute;
            titleBlock.style.left = 0; titleBlock.style.right = 0;
            titleBlock.style.top = Length.Percent(8);
            titleBlock.style.alignItems = Align.Center;
            if (titleImage != null)
            {
                var img = new VisualElement { pickingMode = PickingMode.Ignore };
                img.style.width = titleImageWidth;
                img.style.height = titleImageWidth * titleImage.height / Mathf.Max(1f, titleImage.width);
                img.style.backgroundImage = new StyleBackground(titleImage);
                titleBlock.Add(img);
                titleLetters.Add(img);
            }
            else
            {
                titleBlock.Add(LetterRow("FLOWER", 245, 0));
                var pot = LetterRow("POT", 340, 6);
                pot.style.marginTop = TitleFont != null ? -180 : -30; // Modak has a very tall line box
                titleBlock.Add(pot);
            }
            layer.Add(titleBlock);

            var tapWrap = new VisualElement { pickingMode = PickingMode.Ignore };
            tapWrap.style.position = Position.Absolute;
            tapWrap.style.left = 0; tapWrap.style.right = 0;
            tapWrap.style.top = Length.Percent(84);
            tapWrap.style.alignItems = Align.Center;
            var tapPill = new VisualElement { pickingMode = PickingMode.Ignore };
            tapPill.style.backgroundColor = new Color(1f, 1f, 1f, 0.85f);
            Round(tapPill, 48);
            tapPill.style.paddingLeft = 56; tapPill.style.paddingRight = 56; tapPill.style.paddingTop = 20; tapPill.style.paddingBottom = 20;
            tapLabel = MakeLabel("TAP TO START", 50, PinkDark);
            tapPill.Add(tapLabel);
            tapWrap.Add(tapPill);
            layer.Add(tapWrap);
            return layer;
        }

        VisualElement LetterRow(string word, float size, int colorOffset)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            for (int i = 0; i < word.Length; i++)
            {
                // puffy sticker letters: bubble font, dark brown outline and a solid drop shadow
                var l = MakeLabel(word[i].ToString(), size, LetterColors[(i + colorOffset) % LetterColors.Length]);
                if (TitleFont != null) l.style.unityFontDefinition = FontDefinition.FromFont(TitleFont);
                l.style.unityTextOutlineWidth = size * 0.035f;
                l.style.unityTextOutlineColor = new Color(0.36f, 0.22f, 0.16f);
                l.style.textShadow = new TextShadow { offset = new Vector2(size * 0.025f, size * 0.05f), blurRadius = 0f, color = new Color(0.36f, 0.22f, 0.16f, 0.9f) };
                l.style.marginLeft = -size * 0.02f; l.style.marginRight = -size * 0.02f;
                row.Add(l);
                titleLetters.Add(l);
            }
            return row;
        }

        VisualElement BuildHome()
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            Stretch(layer);

            // ---- top bar: coins | settings ----
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.position = Position.Absolute;
            top.style.left = 40; top.style.right = 40; top.style.top = 70;
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.alignItems = Align.Center;

            var wallet = new VisualElement { pickingMode = PickingMode.Ignore };
            wallet.style.flexDirection = FlexDirection.Row;
            wallet.Add(CurrencyPill(false, out homeCoins));
            var jewelPill = CurrencyPill(true, out homeJewels);
            jewelPill.style.marginLeft = 16;
            jewelPill.style.minWidth = 150;
            wallet.Add(jewelPill);
            top.Add(wallet);

            // top right: collection, then settings
            var topRight = new VisualElement { pickingMode = PickingMode.Ignore };
            topRight.style.flexDirection = FlexDirection.Row;
            topRight.style.alignItems = Align.FlexStart;
            topRight.Add(MenuButton(IconShape.Book, "COLLECTION", new Color(0.40f, 0.70f, 0.95f), OpenCollection, out _, 110, 200));
            topRight.Add(IconButton(IconShape.Gear, 110, OpenSettings));
            top.Add(topRight);
            layer.Add(top);

            // left column under the coins: quests, then shop
            var left = new VisualElement { pickingMode = PickingMode.Ignore };
            left.style.position = Position.Absolute;
            left.style.left = 30; left.style.top = 210;
            left.Add(MenuButton(IconShape.Star, "QUESTS", new Color(1f, 0.72f, 0.25f), OpenQuests, out questBadge));
            var shop = MenuButton(IconShape.Bag, "SHOP", PinkDark, OpenShop, out _);
            shop.style.marginTop = 24;
            left.Add(shop);
            var lucky = MenuButton(IconShape.Gift, "LUCKY BOX", JewelColor, OpenLuckyBox, out luckyBadge);
            lucky.style.marginTop = 24;
            luckyLabel = lucky[lucky.childCount - 1] as Label; // MenuButton adds its caption last
            left.Add(lucky);
            layer.Add(left);

            // ---- GAME START at 3/4 of the screen height ----
            var startWrap = new VisualElement { pickingMode = PickingMode.Ignore };
            startWrap.style.position = Position.Absolute;
            startWrap.style.left = 0; startWrap.style.right = 0;
            startWrap.style.top = Length.Percent(75);
            startWrap.style.alignItems = Align.Center;
            startWrap.style.translate = new Translate(0, Length.Percent(-50));
            startButton = BigButton("GAME START", 660, 180, 70, Green, GreenDark, () => StartClicked?.Invoke());
            startWrap.Add(startButton);
            layer.Add(startWrap);
            return layer;
        }

        static VisualElement MenuButton(IconShape shape, string text, Color iconColor, Action onClick, out Label badge,
            float size = 150f, float width = 190f)
        {
            var wrap = new VisualElement { pickingMode = PickingMode.Ignore };
            wrap.style.alignItems = Align.Center;
            wrap.style.width = width;
            var btn = IconButton(shape, size, onClick, iconColor);
            wrap.Add(btn);

            badge = MakeLabel("", 34, Color.white);
            badge.style.position = Position.Absolute;
            badge.style.top = -8; badge.style.right = (width - size) * 0.5f - 14f;
            badge.style.minWidth = 56; badge.style.height = 56;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.backgroundColor = Pink;
            Round(badge, 28);
            Border(badge, 5, Color.white);
            badge.style.display = DisplayStyle.None;
            wrap.Add(badge);

            var label = MakeLabel(text, size >= 140f ? 32 : 26, Color.white);
            label.style.marginTop = 8;
            label.style.unityTextOutlineWidth = 3f;
            label.style.unityTextOutlineColor = new Color(0.35f, 0.22f, 0.15f);
            wrap.Add(label);
            return wrap;
        }

        // ------------------------------------------------------------------ screens
        public void ShowTitle()
        {
            ClosePopups();
            Show(titleLayer, true);
            Show(homeLayer, false);
        }

        public void ShowHome()
        {
            ClosePopups();
            Show(titleLayer, false);
            Show(homeLayer, true);
            dirty = true;
            RefreshHome();
        }

        public void HideScreens()
        {
            ClosePopups();
            Show(titleLayer, false);
            Show(homeLayer, false);
        }

        void RefreshHome()
        {
            dirty = false;
            homeCoins.text = Format(PlayerData.Coins);
            homeJewels.text = Format(PlayerData.Jewels);
            int claimable = PlayerData.ClaimableQuestCount();
            questBadge.text = claimable.ToString();
            Show(questBadge, claimable > 0);
            RefreshLucky();
        }

        void RefreshLucky()
        {
            bool ready = PlayerData.LuckyBoxReady;
            luckyBadge.text = "!";
            Show(luckyBadge, ready);
            if (luckyLabel != null) luckyLabel.text = ready ? "LUCKY BOX" : ShortTime(PlayerData.LuckyBoxRemaining);
        }

        static string ShortTime(TimeSpan t)
        {
            if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h";
            if (t.TotalHours >= 1) return $"{t.Hours}h {t.Minutes}m";
            return $"{t.Minutes}m {t.Seconds}s";
        }

        // ------------------------------------------------------------------ lucky box
        void OpenLuckyBox()
        {
            var popup = Popup("LUCKY BOX", 820, out var body, ClosePopups);
            body.style.alignItems = Align.Center;
            var gift = Icon(IconShape.Gift, 220, JewelColor);
            body.Add(gift);
            if (PlayerData.LuckyBoxReady)
            {
                var msg = MakeLabel($"Open it for {PlayerData.LuckyBoxMinJewels}-{PlayerData.LuckyBoxMaxJewels} jewels!", 38, Ink);
                msg.style.marginTop = 24; msg.style.marginBottom = 36;
                msg.style.whiteSpace = WhiteSpace.Normal;
                msg.style.unityTextAlign = TextAnchor.MiddleCenter;
                body.Add(msg);
                body.Add(BigButton("OPEN", 420, 140, 56, JewelColor, Color.Lerp(JewelColor, Color.black, 0.3f), () =>
                {
                    int won = PlayerData.OpenLuckyBox();
                    if (won > 0) ShowLuckyResult(won);
                }));
            }
            else
            {
                var msg = MakeLabel("Next box opens in", 36, InkSoft);
                msg.style.marginTop = 24;
                body.Add(msg);
                var time = MakeLabel(ShortTime(PlayerData.LuckyBoxRemaining), 64, Ink);
                time.style.marginTop = 8;
                body.Add(time);
                var note = MakeLabel($"A free box every {PlayerData.LuckyBoxHours / 24} days", 30, InkSoft);
                note.style.marginTop = 20;
                body.Add(note);
            }
            OpenPopup(popup);
        }

        void ShowLuckyResult(int won)
        {
            SoundFx.Unlock();
            var popup = Popup("YAY!", 820, out var body, ClosePopups);
            body.style.alignItems = Align.Center;
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.Add(JewelIcon(140, 20));
            row.Add(MakeLabel("+" + won, 110, JewelColor));
            body.Add(row);
            var ok = BigButton("OK", 360, 130, 52, Green, GreenDark, ClosePopups);
            ok.style.marginTop = 40;
            body.Add(ok);
            OpenPopup(popup);
            RefreshHome();
        }

        // ------------------------------------------------------------------ popups
        void ClosePopups()
        {
            questsOpen = false;
            popupLayer?.Clear();
        }

        void OpenPopup(VisualElement popup)
        {
            ClosePopups();
            popupLayer.Add(popup);
        }

        void OpenQuests()
        {
            var popup = Popup("DAILY QUESTS", 900, out var body, ClosePopups);
            var quests = PlayerData.Quests;
            for (int i = 0; i < quests.Count; i++) body.Add(QuestRow(quests[i], i));
            var note = MakeLabel("New quests every day", 28, InkSoft);
            note.style.marginTop = 24;
            note.style.unityTextAlign = TextAnchor.MiddleCenter;
            body.Add(note);
            OpenPopup(popup);
            questsOpen = true;
        }

        VisualElement QuestRow(Quest q, int index)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 16;
            row.style.paddingTop = 22; row.style.paddingBottom = 22; row.style.paddingLeft = 36; row.style.paddingRight = 22;
            row.style.backgroundColor = new Color(0.96f, 0.92f, 0.84f);
            Round(row, 60);

            var info = new VisualElement { pickingMode = PickingMode.Ignore };
            info.style.flexGrow = 1;
            info.style.flexShrink = 1;
            var text = MakeLabel(PlayerData.QuestText(q), 36, q.claimed ? InkSoft : Ink);
            text.style.whiteSpace = WhiteSpace.Normal;
            info.Add(text);

            var barRow = new VisualElement { pickingMode = PickingMode.Ignore };
            barRow.style.flexDirection = FlexDirection.Row;
            barRow.style.alignItems = Align.Center;
            barRow.style.marginTop = 12;
            var barBg = new VisualElement { pickingMode = PickingMode.Ignore };
            barBg.style.height = 24;
            barBg.style.flexGrow = 1;
            barBg.style.backgroundColor = new Color(0.86f, 0.80f, 0.70f);
            Round(barBg, 12);
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.height = 24;
            bar.style.width = Length.Percent(100f * Mathf.Clamp01(q.progress / (float)Mathf.Max(1, q.target)));
            bar.style.backgroundColor = q.Done ? Green : Gold;
            Round(bar, 12);
            barBg.Add(bar);
            barRow.Add(barBg);
            var count = MakeLabel($"{q.progress}/{q.target}", 28, InkSoft);
            count.style.marginLeft = 16;
            count.style.minWidth = 80;
            barRow.Add(count);
            info.Add(barRow);
            row.Add(info);

            var right = new VisualElement { pickingMode = PickingMode.Ignore };
            right.style.width = 190;
            right.style.alignItems = Align.Center;
            right.style.marginLeft = 16;
            if (q.claimed)
            {
                right.Add(Icon(IconShape.Check, 64, Green));
            }
            else if (q.Done)
            {
                var claim = BigButton("", 180, 100, 30, Pink, PinkDark, () => Claim(index));
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.style.flexDirection = FlexDirection.Row;
                line.style.alignItems = Align.Center;
                line.Add(CoinIcon(40, 8));
                line.Add(MakeLabel("+" + q.reward, 38, Color.white));
                claim.Add(line);
                right.Add(claim);
            }
            else
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.style.flexDirection = FlexDirection.Row;
                line.style.alignItems = Align.Center;
                line.Add(CoinIcon(44, 8));
                line.Add(MakeLabel("+" + q.reward, 40, Ink));
                right.Add(line);
            }
            row.Add(right);
            return row;
        }

        void Claim(int index)
        {
            int reward = PlayerData.ClaimQuest(index);
            if (reward > 0) SoundFx.Coin();
            RefreshHome();
            OpenQuests();
        }

        public void OpenSettings()
        {
            var popup = Popup("SETTINGS", 820, out var body, ClosePopups);

            var soundRow = new VisualElement { pickingMode = PickingMode.Ignore };
            soundRow.style.flexDirection = FlexDirection.Row;
            soundRow.style.justifyContent = Justify.SpaceBetween;
            soundRow.style.alignItems = Align.Center;
            soundRow.style.marginBottom = 40;
            soundRow.style.paddingLeft = 36; soundRow.style.paddingRight = 18; soundRow.style.paddingTop = 18; soundRow.style.paddingBottom = 18;
            soundRow.style.backgroundColor = new Color(0.96f, 0.92f, 0.84f);
            Round(soundRow, 70);
            soundRow.Add(MakeLabel("Sound", 48, Ink));
            Button toggle = null;
            Label toggleLabel = MakeLabel("", 44, Color.white);
            toggle = BigButton("", 220, 110, 44, Green, GreenDark, () =>
            {
                PlayerData.SoundOn = !PlayerData.SoundOn;
                UpdateSoundToggle(toggle, toggleLabel);
                SoundFx.Tap();
            });
            toggle.Add(toggleLabel);
            UpdateSoundToggle(toggle, toggleLabel);
            soundRow.Add(toggle);
            body.Add(soundRow);

            var reset = BigButton("RESET PROGRESS", 708, 130, 44, Pink, PinkDark, OpenResetConfirm);
            body.Add(reset);
            var hint = MakeLabel("Coins, collection, quests and the board", 28, InkSoft);
            hint.style.marginTop = 16;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            body.Add(hint);
            OpenPopup(popup);
        }

        static void UpdateSoundToggle(Button b, Label l)
        {
            bool on = PlayerData.SoundOn;
            l.text = on ? "ON" : "OFF";
            SetButtonColors(b, on ? Green : Disabled, on ? GreenDark : DisabledDark);
        }

        void OpenResetConfirm()
        {
            var popup = Popup("RESET?", 820, out var body, OpenSettings);
            var msg = MakeLabel("All coins, collection, quests and the current board will be deleted.", 36, Ink);
            msg.style.whiteSpace = WhiteSpace.Normal;
            msg.style.unityTextAlign = TextAnchor.MiddleCenter;
            msg.style.marginBottom = 44;
            body.Add(msg);
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.Add(BigButton("CANCEL", 330, 130, 44, Disabled, DisabledDark, OpenSettings));
            row.Add(BigButton("RESET", 330, 130, 44, Pink, PinkDark, () =>
            {
                PlayerData.ResetAll();
                board?.ResetBoard();
                ClosePopups();
                if (homeLayer.style.display != DisplayStyle.None) RefreshHome();
            }));
            body.Add(row);
            OpenPopup(popup);
        }

        int collectionChain;

        void OpenCollection()
        {
            int total = database.ChainCount * database.MaxLevel;
            var popup = Popup($"COLLECTION  {PlayerData.UnlockedCount()}/{total}", 980, out var body, ClosePopups);

            // one tab per chain, showing that chain's best level
            var tabs = new VisualElement { pickingMode = PickingMode.Ignore };
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.justifyContent = Justify.SpaceBetween;
            tabs.style.marginBottom = 30;
            for (int c = 0; c < database.ChainCount; c++)
            {
                int chain = c;
                var ch = database.Get(c);
                bool sel = c == collectionChain;
                var tab = BigButton("", 204, 120, 30,
                    sel ? ch.color : new Color(0.90f, 0.86f, 0.78f),
                    sel ? Color.Lerp(ch.color, Color.black, 0.3f) : CreamShadow,
                    () => { collectionChain = chain; OpenCollection(); });
                var name = MakeLabel(ch.displayName, 30, sel ? Color.white : InkSoft);
                int best = PlayerData.Best(c);
                var lv = MakeLabel(best > 0 ? $"Lv {best}/{database.MaxLevel}" : "Lv -", 26, sel ? new Color(1f, 1f, 1f, 0.9f) : InkSoft);
                tab.Add(name);
                tab.Add(lv);
                tabs.Add(tab);
            }
            body.Add(tabs);

            var grid = new VisualElement { pickingMode = PickingMode.Ignore };
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.FlexStart;
            for (int lv = 1; lv <= database.MaxLevel; lv++) grid.Add(CollectionCell(collectionChain, lv));
            body.Add(grid);
            OpenPopup(popup);
        }

        VisualElement CollectionCell(int chain, int level)
        {
            bool unlocked = PlayerData.IsUnlocked(chain, level);
            var cell = new VisualElement { pickingMode = PickingMode.Ignore };
            cell.style.width = 200; cell.style.height = 250;
            cell.style.marginRight = level % 4 == 0 ? 0 : 16;
            cell.style.marginBottom = 18;
            cell.style.backgroundColor = unlocked ? new Color(0.96f, 0.92f, 0.84f) : new Color(0.90f, 0.87f, 0.82f);
            Round(cell, 60);
            cell.style.alignItems = Align.Center;
            cell.style.paddingTop = 14;

            var lvLabel = MakeLabel(level.ToString(), 24, Color.white);
            lvLabel.style.position = Position.Absolute;
            lvLabel.style.left = 14; lvLabel.style.top = 12;
            lvLabel.style.width = 40; lvLabel.style.height = 40;
            lvLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            lvLabel.style.backgroundColor = unlocked ? database.Get(chain).color : Disabled;
            Round(lvLabel, 20);
            cell.Add(lvLabel);

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = 150; icon.style.height = 150;
            var tex = database.Icon(chain, level);
            if (tex != null)
            {
                icon.style.backgroundImage = new StyleBackground(tex);
                if (!unlocked) icon.style.unityBackgroundImageTintColor = new Color(0.35f, 0.3f, 0.28f, 0.35f);
            }
            cell.Add(icon);

            var name = MakeLabel(unlocked ? database.LevelName(chain, level) : "???", 24, unlocked ? Ink : InkSoft);
            name.style.whiteSpace = WhiteSpace.Normal;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            name.style.width = 180;
            name.style.marginTop = 6;
            cell.Add(name);
            return cell;
        }

        void OpenShop()
        {
            var popup = Popup("SHOP", 940, out var body, ClosePopups);

            // wallet line
            var wallet = new VisualElement { pickingMode = PickingMode.Ignore };
            wallet.style.flexDirection = FlexDirection.Row;
            wallet.style.justifyContent = Justify.Center;
            wallet.style.marginBottom = 26;
            var c = CurrencyPill(false, out var cl); cl.text = Format(PlayerData.Coins); wallet.Add(c);
            var j = CurrencyPill(true, out var jl); jl.text = Format(PlayerData.Jewels); j.style.marginLeft = 16; wallet.Add(j);
            body.Add(wallet);

            // coins for jewels
            body.Add(SectionTitle("COINS"));
            var coinRow = ShopRow();
            for (int i = 0; i < PlayerData.CoinPacks.Length; i++)
            {
                int index = i;
                var pack = PlayerData.CoinPacks[i];
                bool afford = PlayerData.Jewels >= pack.jewels;
                var card = ShopCard(CoinIcon(90, 0), $"{pack.coins}");
                var buy = BigButton("", 230, 96, 36, afford ? JewelColor : Disabled,
                    afford ? Color.Lerp(JewelColor, Color.black, 0.3f) : DisabledDark, () => BuyCoins(index));
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.style.flexDirection = FlexDirection.Row; line.style.alignItems = Align.Center;
                line.Add(Icon(IconShape.Gem, 40, Color.white)); line.Add(MakeLabel(" " + pack.jewels, 38, Color.white));
                buy.Add(line);
                card.Add(buy);
                coinRow.Add(card);
            }
            body.Add(coinRow);

            // jewels for real money (store not connected yet)
            body.Add(SectionTitle("JEWELS"));
            var jewelRow = ShopRow();
            foreach (var amount in new[] { 30, 80, 200 })
            {
                var card = ShopCard(JewelIcon(90, 0), amount.ToString());
                card.Add(BigButton("SOON", 230, 96, 36, Disabled, DisabledDark, () => SoundFx.Error()));
                jewelRow.Add(card);
            }
            body.Add(jewelRow);

            var note = MakeLabel("Costumes for the shop owner are coming soon!", 30, InkSoft);
            note.style.marginTop = 20;
            note.style.unityTextAlign = TextAnchor.MiddleCenter;
            body.Add(note);
            OpenPopup(popup);
        }

        void BuyCoins(int index)
        {
            if (!PlayerData.BuyCoinPack(index)) { SoundFx.Error(); return; }
            SoundFx.Coin();
            RefreshHome();
            OpenShop();
        }

        static Label SectionTitle(string text)
        {
            var l = MakeLabel(text, 40, InkSoft);
            l.style.marginBottom = 14;
            l.style.marginLeft = 10;
            return l;
        }

        static VisualElement ShopRow()
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.flexDirection = FlexDirection.Row;
            r.style.justifyContent = Justify.SpaceBetween;
            r.style.marginBottom = 30;
            return r;
        }

        static VisualElement ShopCard(VisualElement icon, string amount)
        {
            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.style.width = 256;
            card.style.alignItems = Align.Center;
            card.style.paddingTop = 24; card.style.paddingBottom = 20;
            card.style.backgroundColor = new Color(0.96f, 0.92f, 0.84f);
            Round(card, 60);
            card.Add(icon);
            var l = MakeLabel(amount, 46, Ink);
            l.style.marginTop = 8; l.style.marginBottom = 14;
            card.Add(l);
            return card;
        }

        // ------------------------------------------------------------------ animation
        void Update()
        {
            if (root == null) return;
            float t = Time.unscaledTime;

            if (titleLayer.style.display != DisplayStyle.None)
            {
                // letters float up and down one after another, the whole block drifts gently
                for (int i = 0; i < titleLetters.Count; i++)
                {
                    float y = Mathf.Sin(t * 2.2f - i * 0.55f) * 22f;
                    float r = Mathf.Sin(t * 1.6f - i * 0.7f) * (titleImage != null ? 2f : 4f);
                    titleLetters[i].style.translate = new Translate(0, y);
                    titleLetters[i].style.rotate = new Rotate(r);
                }
                titleBlock.style.translate = new Translate(Mathf.Sin(t * 0.6f) * 12f, Mathf.Sin(t * 0.9f) * 10f);
                float p = 1f + 0.04f * Mathf.Sin(t * 3.2f);
                tapLabel.parent.style.scale = new Scale(new Vector3(p, p, 1f));
            }

            if (homeLayer.style.display != DisplayStyle.None)
            {
                if (dirty)
                {
                    RefreshHome();
                    if (questsOpen) OpenQuests();
                }
                float s = 1f + 0.035f * Mathf.Sin(t * 3f);
                startButton.style.scale = new Scale(new Vector3(s, s, 1f));
                luckyTick += Time.unscaledDeltaTime;
                if (luckyTick >= 1f) { luckyTick = 0f; RefreshLucky(); }
            }
        }
    }
}
