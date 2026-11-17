using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static PotMerge.UIKit;

namespace PotMerge
{
    /// <summary>
    /// Runtime-built UI Toolkit menus: title screen, home screen and the settings / collection / shop popups.
    /// Uses the same PanelSettings as the game HUD and sits above it (higher sorting order).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MenuUI : MonoBehaviour
    {
        public ChainDatabase database;
        public MergeBoard board;

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
        readonly List<Label> titleLetters = new List<Label>();
        VisualElement titleBlock;
        Label tapLabel;

        // home widgets
        Label homeCoins;
        readonly List<VisualElement> chainIcons = new List<VisualElement>();
        readonly List<Label> chainLevels = new List<Label>();
        VisualElement questList;
        Label questBadge;
        Button startButton;

        bool dirty = true;

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
            titleBlock.style.top = Length.Percent(7);
            titleBlock.style.alignItems = Align.Center;
            titleBlock.Add(LetterRow("FLOWER", 190, 0));
            var pot = LetterRow("POT", 250, 6);
            pot.style.marginTop = -30;
            titleBlock.Add(pot);
            layer.Add(titleBlock);

            var tapWrap = new VisualElement { pickingMode = PickingMode.Ignore };
            tapWrap.style.position = Position.Absolute;
            tapWrap.style.left = 0; tapWrap.style.right = 0;
            tapWrap.style.top = Length.Percent(83);
            tapWrap.style.alignItems = Align.Center;
            var tapPill = new VisualElement { pickingMode = PickingMode.Ignore };
            tapPill.style.backgroundColor = new Color(0.25f, 0.17f, 0.12f, 0.55f);
            Round(tapPill, 40);
            tapPill.style.paddingLeft = 50; tapPill.style.paddingRight = 50; tapPill.style.paddingTop = 18; tapPill.style.paddingBottom = 18;
            tapLabel = MakeLabel("TAP TO START", 54, Color.white);
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
                var l = MakeLabel(word[i].ToString(), size, LetterColors[(i + colorOffset) % LetterColors.Length]);
                l.style.unityTextOutlineWidth = size * 0.045f;
                l.style.unityTextOutlineColor = Color.white;
                l.style.textShadow = new TextShadow { offset = new Vector2(0f, size * 0.06f), blurRadius = 0f, color = new Color(0.35f, 0.22f, 0.15f, 0.55f) };
                l.style.marginLeft = size * 0.02f; l.style.marginRight = size * 0.02f;
                row.Add(l);
                titleLetters.Add(l);
            }
            return row;
        }

        VisualElement BuildHome()
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            Stretch(layer);

            // ---- top bar: coins | collection, settings ----
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.position = Position.Absolute;
            top.style.left = 40; top.style.right = 40; top.style.top = 70;
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.alignItems = Align.Center;

            var coinPill = Pill();
            coinPill.style.minWidth = 220;
            coinPill.Add(CoinIcon(52));
            homeCoins = MakeLabel("0", 46, Ink);
            coinPill.Add(homeCoins);
            top.Add(coinPill);

            var buttons = new VisualElement { pickingMode = PickingMode.Ignore };
            buttons.style.flexDirection = FlexDirection.Row;
            var book = IconButton(IconShape.Book, 110, OpenCollection);
            book.style.marginRight = 24;
            buttons.Add(book);
            buttons.Add(IconButton(IconShape.Gear, 110, OpenSettings));
            top.Add(buttons);
            layer.Add(top);

            // ---- best level per chain ----
            var chains = new VisualElement { pickingMode = PickingMode.Ignore };
            chains.style.position = Position.Absolute;
            chains.style.left = 40; chains.style.right = 40; chains.style.top = 220;
            chains.style.flexDirection = FlexDirection.Row;
            chains.style.justifyContent = Justify.SpaceBetween;
            int n = database != null ? Mathf.Min(database.ChainCount, PlayerData.ChainCount) : 0;
            for (int c = 0; c < n; c++) chains.Add(ChainCard(c));
            layer.Add(chains);

            // ---- daily quests (left) ----
            var quests = Card(40);
            quests.pickingMode = PickingMode.Position;
            quests.style.position = Position.Absolute;
            quests.style.left = 40; quests.style.top = 520;
            quests.style.width = 600;
            quests.style.paddingTop = 30; quests.style.paddingBottom = 30; quests.style.paddingLeft = 32; quests.style.paddingRight = 32;
            var qHeader = new VisualElement { pickingMode = PickingMode.Ignore };
            qHeader.style.flexDirection = FlexDirection.Row;
            qHeader.style.alignItems = Align.Center;
            qHeader.style.marginBottom = 18;
            qHeader.Add(MakeLabel("DAILY QUESTS", 40, Ink));
            questBadge = MakeLabel("", 30, Color.white);
            questBadge.style.backgroundColor = Pink;
            Round(questBadge, 22);
            questBadge.style.paddingLeft = 14; questBadge.style.paddingRight = 14;
            questBadge.style.marginLeft = 16;
            qHeader.Add(questBadge);
            quests.Add(qHeader);
            questList = new VisualElement { pickingMode = PickingMode.Ignore };
            quests.Add(questList);
            layer.Add(quests);

            // ---- shop (right) ----
            var shop = new VisualElement { pickingMode = PickingMode.Ignore };
            shop.style.position = Position.Absolute;
            shop.style.right = 40; shop.style.top = 520;
            shop.style.alignItems = Align.Center;
            var shopBtn = IconButton(IconShape.Bag, 150, OpenShop, PinkDark);
            shop.Add(shopBtn);
            var shopLabel = MakeLabel("SHOP", 38, Color.white);
            shopLabel.style.marginTop = 8;
            shopLabel.style.unityTextOutlineWidth = 3f;
            shopLabel.style.unityTextOutlineColor = new Color(0.35f, 0.22f, 0.15f);
            shop.Add(shopLabel);
            layer.Add(shop);

            // ---- GAME START at 3/4 of the screen height ----
            var startWrap = new VisualElement { pickingMode = PickingMode.Ignore };
            startWrap.style.position = Position.Absolute;
            startWrap.style.left = 0; startWrap.style.right = 0;
            startWrap.style.top = Length.Percent(75);
            startWrap.style.alignItems = Align.Center;
            startWrap.style.translate = new Translate(0, Length.Percent(-50));
            startButton = BigButton("GAME START", 640, 180, 70, Green, GreenDark, () => StartClicked?.Invoke());
            startWrap.Add(startButton);
            layer.Add(startWrap);
            return layer;
        }

        VisualElement ChainCard(int chain)
        {
            var ch = database.Get(chain);
            var card = Card(36);
            card.pickingMode = PickingMode.Ignore;
            card.style.width = 232;
            card.style.paddingTop = 14; card.style.paddingBottom = 16;
            card.style.alignItems = Align.Center;
            card.style.borderTopWidth = 10;
            card.style.borderTopColor = ch.color;

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = 150; icon.style.height = 150;
            card.Add(icon);
            chainIcons.Add(icon);

            var name = MakeLabel(ch.displayName, 30, InkSoft);
            card.Add(name);
            var lv = MakeLabel("-", 36, Ink);
            card.Add(lv);
            chainLevels.Add(lv);
            return card;
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

            for (int c = 0; c < chainIcons.Count; c++)
            {
                int best = PlayerData.Best(c);
                var tex = best > 0 ? database.Icon(c, best) : null;
                chainIcons[c].style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
                chainLevels[c].text = best > 0 ? $"Lv {best}/{database.MaxLevel}" : "Lv -";
            }

            questList.Clear();
            var quests = PlayerData.Quests;
            for (int i = 0; i < quests.Count; i++) questList.Add(QuestRow(quests[i], i));
            int claimable = PlayerData.ClaimableQuestCount();
            questBadge.text = claimable > 0 ? claimable.ToString() : "";
            Show(questBadge, claimable > 0);
        }

        VisualElement QuestRow(Quest q, int index)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 12;
            row.style.paddingTop = 14; row.style.paddingBottom = 14; row.style.paddingLeft = 18; row.style.paddingRight = 14;
            row.style.backgroundColor = new Color(0.96f, 0.92f, 0.84f);
            Round(row, 26);

            var info = new VisualElement { pickingMode = PickingMode.Ignore };
            info.style.flexGrow = 1;
            info.style.flexShrink = 1;
            var text = MakeLabel(PlayerData.QuestText(q), 30, q.claimed ? InkSoft : Ink);
            text.style.whiteSpace = WhiteSpace.Normal;
            info.Add(text);

            var barBg = new VisualElement { pickingMode = PickingMode.Ignore };
            barBg.style.height = 18;
            barBg.style.marginTop = 10;
            barBg.style.backgroundColor = new Color(0.86f, 0.80f, 0.70f);
            Round(barBg, 9);
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.height = 18;
            bar.style.width = Length.Percent(100f * Mathf.Clamp01(q.progress / (float)Mathf.Max(1, q.target)));
            bar.style.backgroundColor = q.Done ? Green : Gold;
            Round(bar, 9);
            barBg.Add(bar);
            info.Add(barBg);

            var count = MakeLabel($"{q.progress}/{q.target}", 24, InkSoft);
            count.style.marginTop = 4;
            info.Add(count);
            row.Add(info);

            var right = new VisualElement { pickingMode = PickingMode.Ignore };
            right.style.width = 150;
            right.style.alignItems = Align.Center;
            right.style.marginLeft = 12;
            if (q.claimed)
            {
                right.Add(Icon(IconShape.Check, 56, Green));
            }
            else if (q.Done)
            {
                var claim = BigButton("", 150, 92, 30, Pink, PinkDark, () => Claim(index));
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.style.flexDirection = FlexDirection.Row;
                line.style.alignItems = Align.Center;
                line.Add(CoinIcon(34, 6));
                line.Add(MakeLabel("+" + q.reward, 34, Color.white));
                claim.Add(line);
                right.Add(claim);
            }
            else
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.style.flexDirection = FlexDirection.Row;
                line.style.alignItems = Align.Center;
                line.Add(CoinIcon(38, 8));
                line.Add(MakeLabel("+" + q.reward, 36, Ink));
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
        }

        // ------------------------------------------------------------------ popups
        void ClosePopups() => popupLayer?.Clear();

        void OpenPopup(VisualElement popup)
        {
            ClosePopups();
            popupLayer.Add(popup);
        }

        public void OpenSettings()
        {
            var popup = Popup("SETTINGS", 820, out var body, ClosePopups);

            var soundRow = new VisualElement { pickingMode = PickingMode.Ignore };
            soundRow.style.flexDirection = FlexDirection.Row;
            soundRow.style.justifyContent = Justify.SpaceBetween;
            soundRow.style.alignItems = Align.Center;
            soundRow.style.marginBottom = 40;
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
            var popup = Popup($"COLLECTION  {PlayerData.UnlockedCount()}/{total}", 960, out var body, ClosePopups);

            var tabs = new VisualElement { pickingMode = PickingMode.Ignore };
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.justifyContent = Justify.SpaceBetween;
            tabs.style.marginBottom = 30;
            for (int c = 0; c < database.ChainCount; c++)
            {
                int chain = c;
                var ch = database.Get(c);
                bool sel = c == collectionChain;
                var tab = BigButton(ch.displayName, 196, 90, 30,
                    sel ? ch.color : new Color(0.88f, 0.84f, 0.76f),
                    sel ? Color.Lerp(ch.color, Color.black, 0.3f) : CreamShadow,
                    () => { collectionChain = chain; OpenCollection(); });
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
            cell.style.width = 196; cell.style.height = 250;
            cell.style.marginRight = level % 4 == 0 ? 0 : 16;
            cell.style.marginBottom = 18;
            cell.style.backgroundColor = unlocked ? new Color(0.96f, 0.92f, 0.84f) : new Color(0.90f, 0.87f, 0.82f);
            Round(cell, 28);
            cell.style.alignItems = Align.Center;
            cell.style.paddingTop = 12;

            var lvLabel = MakeLabel(level.ToString(), 24, InkSoft);
            lvLabel.style.position = Position.Absolute;
            lvLabel.style.left = 16; lvLabel.style.top = 10;
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
            var popup = Popup("SHOP", 820, out var body, ClosePopups);
            body.style.alignItems = Align.Center;
            body.Add(Icon(IconShape.Bag, 160, PinkDark));
            var msg = MakeLabel("Costumes for the flower shop owner are coming soon!", 40, Ink);
            msg.style.whiteSpace = WhiteSpace.Normal;
            msg.style.unityTextAlign = TextAnchor.MiddleCenter;
            msg.style.marginTop = 30;
            body.Add(msg);
            var sub = MakeLabel("Save your coins.", 32, InkSoft);
            sub.style.marginTop = 16;
            body.Add(sub);
            OpenPopup(popup);
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
                    float r = Mathf.Sin(t * 1.6f - i * 0.7f) * 4f;
                    titleLetters[i].style.translate = new Translate(0, y);
                    titleLetters[i].style.rotate = new Rotate(r);
                }
                titleBlock.style.translate = new Translate(Mathf.Sin(t * 0.6f) * 12f, Mathf.Sin(t * 0.9f) * 10f);
                tapLabel.style.opacity = 0.55f + 0.45f * Mathf.Sin(t * 3.2f);
            }

            if (homeLayer.style.display != DisplayStyle.None)
            {
                if (dirty) RefreshHome();
                float s = 1f + 0.035f * Mathf.Sin(t * 3f);
                startButton.style.scale = new Scale(new Vector3(s, s, 1f));
            }
        }
    }
}
