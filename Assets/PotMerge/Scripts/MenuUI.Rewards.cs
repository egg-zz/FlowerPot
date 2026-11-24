using UnityEngine;
using UnityEngine.UIElements;
using static PotMerge.UIKit;

namespace PotMerge
{
    /// <summary>Daily quests and the collection: everything that is claimed for a reward.</summary>
    public partial class MenuUI
    {
        static readonly Color NewRed = new Color(0.93f, 0.22f, 0.25f);
        const string EnergyFullNote = "Energy is almost full. Use some first!";

        Label collectionBadge; // red dot on the COLLECTION button
        int collectionChain;

        /// <summary>Coins / energy / jewels in one line; zero amounts are left out.</summary>
        static VisualElement RewardLine(int coins, int energy, int jewels, float size, Color color)
        {
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            line.style.justifyContent = Justify.Center;
            void Part(VisualElement icon, int n)
            {
                if (n <= 0) return;
                if (line.childCount > 0) icon.style.marginLeft = 12;
                line.Add(icon);
                line.Add(MakeLabel("+" + n, size, color));
            }
            Part(CoinIcon(size * 1.1f, 6), coins);
            Part(Icon(IconShape.Bolt, size * 1.1f, EnergyColor), energy);
            Part(JewelIcon(size * 1.1f, 4), jewels);
            return line;
        }

        // ------------------------------------------------------------------ daily quests
        void OpenQuests() => OpenQuests(null);

        void OpenQuests(string message)
        {
            var popup = Popup("DAILY QUESTS", 900, out var body, ClosePopups);
            var quests = PlayerData.Quests;
            string[] grades = { "EASY", "NORMAL", "HARD" };
            for (int i = 0; i < quests.Count; i++)
            {
                int index = i;
                var q = quests[i];
                body.Add(QuestRow(grades[Mathf.Min(i, 2)], PlayerData.QuestText(q), q.progress, q.target, q.reward, q.energy,
                    q.claimed, q.Done, new Color(0.96f, 0.92f, 0.84f),
                    () => Claimed(PlayerData.ClaimQuest(index))));
            }
            int done = 0;
            foreach (var q in quests) if (q.claimed) done++;
            body.Add(QuestRow("BONUS", "Finish all 3 quests", done, quests.Count, PlayerData.QuestBonusCoins, PlayerData.QuestBonusEnergy,
                PlayerData.QuestBonusClaimed, PlayerData.QuestBonusReady, new Color(1f, 0.90f, 0.72f),
                () => Claimed(PlayerData.ClaimQuestBonus())));

            var note = MakeLabel(message ?? "New quests every day", 28, message != null ? NewRed : InkSoft);
            note.style.marginTop = 24;
            note.style.unityTextAlign = TextAnchor.MiddleCenter;
            body.Add(note);
            OpenPopup(popup);
            questsOpen = true;
        }

        void Claimed(bool ok)
        {
            if (ok) SoundFx.Coin(); else SoundFx.Error();
            RefreshHome();
            OpenQuests(ok ? null : EnergyFullNote);
        }

        static VisualElement QuestRow(string grade, string title, int progress, int target, int coins, int energy,
            bool claimed, bool done, Color background, System.Action claim)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 16;
            row.style.paddingTop = 18; row.style.paddingBottom = 18; row.style.paddingLeft = 36; row.style.paddingRight = 22;
            row.style.backgroundColor = background;
            Round(row, 60);

            var info = new VisualElement { pickingMode = PickingMode.Ignore };
            info.style.flexGrow = 1;
            info.style.flexShrink = 1;
            info.Add(MakeLabel(grade, 24, InkSoft));
            var text = MakeLabel(title, 36, claimed ? InkSoft : Ink);
            text.style.whiteSpace = WhiteSpace.Normal;
            info.Add(text);

            var barRow = new VisualElement { pickingMode = PickingMode.Ignore };
            barRow.style.flexDirection = FlexDirection.Row;
            barRow.style.alignItems = Align.Center;
            barRow.style.marginTop = 10;
            var barBg = new VisualElement { pickingMode = PickingMode.Ignore };
            barBg.style.height = 24;
            barBg.style.flexGrow = 1;
            barBg.style.backgroundColor = new Color(0.86f, 0.80f, 0.70f);
            Round(barBg, 12);
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.height = 24;
            bar.style.width = Length.Percent(100f * Mathf.Clamp01(progress / (float)Mathf.Max(1, target)));
            bar.style.backgroundColor = progress >= target ? Green : Gold;
            Round(bar, 12);
            barBg.Add(bar);
            barRow.Add(barBg);
            var count = MakeLabel($"{progress}/{target}", 28, InkSoft);
            count.style.marginLeft = 16;
            count.style.minWidth = 80;
            barRow.Add(count);
            info.Add(barRow);
            row.Add(info);

            var right = new VisualElement { pickingMode = PickingMode.Ignore };
            right.style.width = 220;
            right.style.alignItems = Align.Center;
            right.style.marginLeft = 16;
            if (claimed) right.Add(Icon(IconShape.Check, 64, Green));
            else if (done)
            {
                var btn = BigButton("", 216, 100, 30, Pink, PinkDark, claim);
                btn.Add(RewardLine(coins, energy, 0, 34, Color.white));
                right.Add(btn);
            }
            else right.Add(RewardLine(coins, energy, 0, 34, Ink));
            row.Add(right);
            return row;
        }

        // ------------------------------------------------------------------ collection
        void OpenCollection() => OpenCollection(null);

        void OpenCollection(string message)
        {
            int total = database.ChainCount * database.MaxLevel;
            var popup = Popup($"COLLECTION  {PlayerData.UnlockedCount()}/{total}", 980, out var body, ClosePopups);

            // one tab per chain, showing that chain's best level
            var tabs = new VisualElement { pickingMode = PickingMode.Ignore };
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.justifyContent = Justify.SpaceBetween;
            tabs.style.marginBottom = 20;
            for (int c = 0; c < database.ChainCount; c++)
            {
                int chain = c;
                var ch = database.Get(c);
                bool sel = c == collectionChain;
                var tab = BigButton("", 204, 120, 30,
                    sel ? ch.color : new Color(0.90f, 0.86f, 0.78f),
                    sel ? Color.Lerp(ch.color, Color.black, 0.3f) : CreamShadow,
                    () => { collectionChain = chain; OpenCollection(); });
                int best = PlayerData.Best(c);
                tab.Add(MakeLabel(ch.displayName, 30, sel ? Color.white : InkSoft));
                tab.Add(MakeLabel(best > 0 ? $"Lv {best}/{database.MaxLevel}" : "Lv -", 26, sel ? new Color(1f, 1f, 1f, 0.9f) : InkSoft));
                if (PlayerData.ClaimableCollectionCount(c) > 0) tab.Add(RedDot(34, -6, -6));
                tabs.Add(tab);
            }
            body.Add(tabs);

            var note = MakeLabel(message ?? "Tap a new item to get its reward", 28, message != null ? NewRed : InkSoft);
            note.style.unityTextAlign = TextAnchor.MiddleCenter;
            note.style.marginBottom = 16;
            body.Add(note);

            var grid = new VisualElement { pickingMode = PickingMode.Ignore };
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.FlexStart;
            for (int lv = 1; lv <= database.MaxLevel; lv++) grid.Add(CollectionCell(collectionChain, lv));
            body.Add(grid);
            OpenPopup(popup);
        }

        static VisualElement RedDot(float size, float top, float right)
        {
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.style.position = Position.Absolute;
            dot.style.top = top; dot.style.right = right;
            dot.style.width = size; dot.style.height = size;
            dot.style.backgroundColor = NewRed;
            Round(dot, size * 0.5f);
            Border(dot, 4, Color.white);
            return dot;
        }

        VisualElement CollectionCell(int chain, int level)
        {
            bool unlocked = PlayerData.IsUnlocked(chain, level);
            bool claimable = PlayerData.CollectionClaimable(chain, level);
            var reward = PlayerData.CollectionReward(level);
            var cell = new VisualElement { pickingMode = claimable ? PickingMode.Position : PickingMode.Ignore };
            cell.style.width = 200; cell.style.height = 262;
            cell.style.marginRight = level % 4 == 0 ? 0 : 16;
            cell.style.marginBottom = 16;
            cell.style.backgroundColor = claimable ? new Color(1f, 0.90f, 0.66f) : unlocked ? new Color(0.96f, 0.92f, 0.84f) : new Color(0.90f, 0.87f, 0.82f);
            Round(cell, 60);
            cell.style.alignItems = Align.Center;
            cell.style.paddingTop = 10;

            var lvLabel = MakeLabel(level.ToString(), 24, Color.white);
            lvLabel.style.position = Position.Absolute;
            lvLabel.style.left = 14; lvLabel.style.top = 12;
            lvLabel.style.width = 40; lvLabel.style.height = 40;
            lvLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            lvLabel.style.backgroundColor = unlocked ? database.Get(chain).color : Disabled;
            Round(lvLabel, 20);
            cell.Add(lvLabel);

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = 140; icon.style.height = 140;
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
            cell.Add(name);

            if (claimable)
            {
                var line = RewardLine(reward.coins, reward.energy, reward.jewels, 30, Ink);
                line.style.marginTop = 2;
                cell.Add(line);
                cell.Add(RedDot(40, -4, -2));
                cell.RegisterCallback<ClickEvent>(e =>
                {
                    bool ok = PlayerData.ClaimCollection(chain, level);
                    if (ok) SoundFx.Unlock(); else SoundFx.Error();
                    RefreshHome();
                    if (ok) ShowCollectionReward(chain, level); else OpenCollection(EnergyFullNote);
                });
            }
            return cell;
        }
    }
}
