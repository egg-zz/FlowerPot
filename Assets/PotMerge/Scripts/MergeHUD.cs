using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static PotMerge.UIKit;

namespace PotMerge
{
    /// <summary>Runtime-built UI Toolkit HUD for the game screen: coins/jewels/energy, home/settings, level goals and timer, result cards, toasts and floating texts.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class MergeHUD : MonoBehaviour
    {
        public event Action HomeClicked;
        public event Action SettingsClicked;
        public event Action EnergyClicked;
        public event Action RetryClicked;
        public event Action NextClicked;
        public event Action MoreTimeAdClicked;
        public event Action MoreTimeJewelClicked;
        public event Action GiveUpHomeClicked;
        public event Action ClearHomeClicked;

        public struct GoalView { public string text; public Texture2D icon; public int progress; public int target; }

        // level widgets
        Label levelLabel;
        Label timerLabel;
        VisualElement timerPill;
        VisualElement goalCard;
        VisualElement resultPanel;
        int lastShownSecond = -1;

        VisualElement root;
        VisualElement fxLayer;
        Label coinLabel;
        Label jewelLabel;
        Label energyLabel;
        float energyTick;
        Label toast;
        float toastTimer;
        float coinPunch;

        class Floater { public Label label; public Vector3 world; public float age; public float life; }
        readonly List<Floater> floaters = new List<Floater>();
        Camera cam;

        void OnEnable()
        {
            cam = Camera.main;
            var doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            root.style.justifyContent = Justify.SpaceBetween;

            fxLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            Stretch(fxLayer);

            // ---------- top bar: coins | home, settings ----------
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.alignItems = Align.Center;
            top.style.paddingTop = 70; top.style.paddingLeft = 40; top.style.paddingRight = 40;

            var coinPill = new VisualElement { pickingMode = PickingMode.Ignore };
            coinPill.style.flexDirection = FlexDirection.Row;
            coinPill.Add(CurrencyPill(false, out coinLabel));
            var jewelPill = CurrencyPill(true, out jewelLabel);
            jewelPill.style.marginLeft = 16;
            jewelPill.style.minWidth = 150;
            coinPill.Add(jewelPill);
            var energyPill = EnergyPill(out energyLabel, () => EnergyClicked?.Invoke());
            energyPill.style.marginLeft = 16;
            coinPill.Add(energyPill);

            var buttons = new VisualElement { pickingMode = PickingMode.Ignore };
            buttons.style.flexDirection = FlexDirection.Row;
            var home = IconButton(IconShape.Home, 110, () => HomeClicked?.Invoke());
            home.style.marginRight = 24;
            buttons.Add(home);
            buttons.Add(IconButton(IconShape.Gear, 110, () => SettingsClicked?.Invoke()));

            top.Add(coinPill);
            top.Add(buttons);

            // ---------- toast ----------
            toast = MakeLabel("", 52, Color.white);
            toast.style.position = Position.Absolute;
            toast.style.top = 260; toast.style.left = 0; toast.style.right = 0;
            toast.style.unityTextAlign = TextAnchor.MiddleCenter;
            toast.style.unityTextOutlineWidth = 3f;
            toast.style.unityTextOutlineColor = new Color(0.15f, 0.1f, 0.05f);
            toast.style.opacity = 0;

            // ---------- bottom hint: boxes cost energy ----------
            var bottom = new VisualElement { pickingMode = PickingMode.Ignore };
            bottom.style.alignItems = Align.Center;
            bottom.style.paddingBottom = 110;
            var hint = Pill();
            hint.style.backgroundColor = new Color(1f, 0.98f, 0.93f, 0.85f);
            hint.Add(MakeLabel("TAP A BOX FOR ITEMS", 34, InkSoft));
            var bolt = Icon(IconShape.Bolt, 40, EnergyColor);
            bolt.style.marginLeft = 16;
            hint.Add(bolt);
            hint.Add(MakeLabel("1", 34, InkSoft));
            bottom.Add(hint);

            // ---------- level: goals (left), level + timer (right) ----------
            goalCard = new VisualElement { pickingMode = PickingMode.Ignore };
            goalCard.style.position = Position.Absolute;
            goalCard.style.left = 30; goalCard.style.top = 210;
            goalCard.style.width = 290;
            goalCard.style.backgroundColor = new Color(1f, 0.98f, 0.93f, 0.92f);
            Round(goalCard, 44);
            goalCard.style.borderBottomWidth = 8; goalCard.style.borderBottomColor = CreamShadow;
            goalCard.style.paddingTop = 18; goalCard.style.paddingBottom = 18; goalCard.style.paddingLeft = 22; goalCard.style.paddingRight = 18;

            var rightCol = new VisualElement { pickingMode = PickingMode.Ignore };
            rightCol.style.position = Position.Absolute;
            rightCol.style.right = 30; rightCol.style.top = 210;
            rightCol.style.width = 260;
            rightCol.style.alignItems = Align.Center;
            levelLabel = MakeLabel("LEVEL 1", 44, Color.white);
            levelLabel.style.unityTextOutlineWidth = 3f;
            levelLabel.style.unityTextOutlineColor = new Color(0.35f, 0.22f, 0.15f);
            levelLabel.style.marginBottom = 10;
            rightCol.Add(levelLabel);
            timerPill = Pill();
            timerPill.style.justifyContent = Justify.Center;
            timerPill.style.minWidth = 230;
            timerPill.style.paddingRight = 28;
            timerPill.Add(ClockIcon(50));
            timerLabel = MakeLabel("0:00", 50, Ink);
            timerLabel.style.marginLeft = 12;
            timerPill.Add(timerLabel);
            rightCol.Add(timerPill);

            // ---------- level result (clear / fail) ----------
            resultPanel = new VisualElement();
            Stretch(resultPanel);
            resultPanel.style.backgroundColor = Dim;
            resultPanel.style.alignItems = Align.Center;
            resultPanel.style.justifyContent = Justify.Center;
            resultPanel.style.display = DisplayStyle.None;

            // ---------- shop owner at the top center ----------
            ownerPortrait = new VisualElement { pickingMode = PickingMode.Ignore };
            ownerPortrait.style.position = Position.Absolute;
            ownerPortrait.style.width = 440; ownerPortrait.style.height = 440;
            ownerPortrait.style.left = Length.Percent(50);
            ownerPortrait.style.top = 150;
            ownerPortrait.style.translate = new Translate(Length.Percent(-50), 0);
            ownerPortrait.style.display = DisplayStyle.None;

            root.Add(ownerPortrait);
            root.Add(top);
            root.Add(goalCard);
            root.Add(rightCol);
            root.Add(bottom);
            root.Add(fxLayer);
            root.Add(toast);
            root.Add(resultPanel);
        }

        static VisualElement ClockIcon(float size)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.width = size; v.style.height = size;
            v.style.flexShrink = 0;
            v.generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                var c = new Vector2(size * 0.5f, size * 0.5f);
                p.fillColor = new Color(0.40f, 0.70f, 0.95f);
                p.BeginPath(); p.Arc(c, size * 0.47f, 0f, 360f); p.Fill();
                p.fillColor = Color.white;
                p.BeginPath(); p.Arc(c, size * 0.35f, 0f, 360f); p.Fill();
                p.strokeColor = Ink; p.lineWidth = size * 0.09f; p.lineCap = LineCap.Round;
                p.BeginPath(); p.MoveTo(c); p.LineTo(c + new Vector2(0f, -size * 0.24f)); p.Stroke();
                p.BeginPath(); p.MoveTo(c); p.LineTo(c + new Vector2(size * 0.17f, size * 0.06f)); p.Stroke();
            };
            return v;
        }

        // ---------------- level API ----------------
        public void SetLevel(int number) => levelLabel.text = "LEVEL " + number;

        /// <summary>Shows the level timer; total &lt;= 0 means the level has no time limit and hides it.</summary>
        public void SetTimer(float secondsLeft, float total)
        {
            bool timed = total > 0f;
            timerPill.style.display = timed ? DisplayStyle.Flex : DisplayStyle.None;
            if (!timed) { lastShownSecond = -1; return; }
            int s = Mathf.CeilToInt(secondsLeft);
            if (s == lastShownSecond) return;
            lastShownSecond = s;
            timerLabel.text = $"{s / 60}:{s % 60:00}";
            bool warn = s <= 10;
            timerLabel.style.color = warn ? new Color(0.90f, 0.25f, 0.25f) : Ink;
            if (warn && s > 0) { timerPill.style.scale = new Scale(new Vector3(1.12f, 1.12f, 1f)); timerPulse = 1f; }
        }

        float timerPulse;

        public void SetGoals(List<GoalView> goals)
        {
            goalCard.Clear();
            var head = MakeLabel("GOALS", 28, InkSoft);
            head.style.marginBottom = 6;
            goalCard.Add(head);
            foreach (var g in goals)
            {
                bool done = g.progress >= g.target;
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 8;
                if (g.icon != null)
                {
                    var img = new VisualElement { pickingMode = PickingMode.Ignore };
                    img.style.width = 64; img.style.height = 64; img.style.flexShrink = 0;
                    img.style.backgroundImage = new StyleBackground(g.icon);
                    row.Add(img);
                }
                else row.Add(Icon(IconShape.Star, 52, Gold));
                var col = new VisualElement { pickingMode = PickingMode.Ignore };
                col.style.marginLeft = 10;
                col.style.flexShrink = 1;
                var t = MakeLabel(g.text, 26, done ? InkSoft : Ink);
                t.style.whiteSpace = WhiteSpace.Normal;
                col.Add(t);
                var count = MakeLabel(done ? "DONE" : $"{g.progress}/{g.target}", 30, done ? Green : PinkDark);
                col.Add(count);
                row.Add(col);
                if (done)
                {
                    var check = Icon(IconShape.Check, 40, Green);
                    check.style.marginLeft = 6;
                    row.Add(check);
                }
                goalCard.Add(row);
            }
        }

        public void HideResults()
        {
            if (resultPanel == null) return;
            resultPanel.Clear();
            resultPanel.style.display = DisplayStyle.None;
        }

        public bool ResultVisible => resultPanel != null && resultPanel.style.display == DisplayStyle.Flex;

        VisualElement ResultCard(string title, Color titleColor)
        {
            resultPanel.Clear();
            resultPanel.style.display = DisplayStyle.Flex;
            var card = Card(80);
            card.style.width = 880;
            card.style.paddingTop = 60; card.style.paddingBottom = 60; card.style.paddingLeft = 60; card.style.paddingRight = 60;
            card.style.alignItems = Align.Center;
            card.Add(MakeLabel(title, 76, titleColor));
            resultPanel.Add(card);
            return card;
        }

        public void ShowLevelClear(int number, int stars, int coins, int jewels, bool hasNext)
        {
            var card = ResultCard($"LEVEL {number} CLEAR!", Green);
            var starRow = Row();
            starRow.style.marginTop = 20; starRow.style.marginBottom = 20;
            for (int i = 0; i < 3; i++)
            {
                var st = Icon(IconShape.Star, i == 1 ? 170 : 140, i < stars ? Gold : new Color(0.85f, 0.80f, 0.72f));
                st.style.marginLeft = 10; st.style.marginRight = 10;
                if (i != 1) st.style.marginTop = 30;
                starRow.Add(st);
            }
            card.Add(starRow);

            if (coins > 0 || jewels > 0)
            {
                var rew = Row();
                rew.style.marginBottom = 36;
                if (coins > 0) { rew.Add(CoinIcon(60, 12)); rew.Add(MakeLabel("+" + coins, 60, Ink)); }
                if (jewels > 0)
                {
                    var j = JewelIcon(62, 10); j.style.marginLeft = coins > 0 ? 40 : 0;
                    rew.Add(j); rew.Add(MakeLabel("+" + jewels, 60, JewelColor));
                }
                card.Add(rew);
            }
            else
            {
                var note = MakeLabel("Rewards are for the first clear", 32, InkSoft);
                note.style.marginBottom = 36;
                card.Add(note);
            }

            var btns = Row();
            var home = BigButton("HOME", 300, 140, 50, Pink, PinkDark, () => ClearHomeClicked?.Invoke());
            btns.Add(home);
            if (hasNext)
            {
                home.style.marginRight = 30;
                btns.Add(BigButton("NEXT", 400, 140, 56, Green, GreenDark, () => NextClicked?.Invoke()));
            }
            card.Add(btns);
            if (!hasNext)
            {
                var soon = MakeLabel("More levels coming soon!", 32, InkSoft);
                soon.style.marginTop = 24;
                card.Add(soon);
            }
        }

        public void ShowLevelFail(int number, bool timeUp, int extraSeconds, int jewelCost, bool adAvailable)
        {
            var card = ResultCard(timeUp ? "TIME'S UP!" : "NO MORE MOVES", PinkDark);
            var info = MakeLabel(timeUp ? $"Keep playing level {number} with +{extraSeconds} seconds?" : "The board is full and nothing can merge.", 36, InkSoft);
            info.style.whiteSpace = WhiteSpace.Normal;
            info.style.unityTextAlign = TextAnchor.MiddleCenter;
            info.style.marginTop = 16; info.style.marginBottom = 36;
            card.Add(info);

            if (timeUp)
            {
                var ad = BigButton("", 720, 130, 40, adAvailable ? new Color(0.40f, 0.70f, 0.95f) : Disabled,
                    adAvailable ? new Color(0.25f, 0.50f, 0.78f) : DisabledDark, () => MoreTimeAdClicked?.Invoke());
                var adRow = Row();
                adRow.Add(Icon(IconShape.Play, 46, Color.white));
                var adText = MakeLabel(adAvailable ? $"WATCH AD  +{extraSeconds}s" : "NO ADS LEFT TODAY", 40, Color.white);
                adText.style.marginLeft = 14;
                adRow.Add(adText);
                ad.Add(adRow);
                card.Add(ad);

                bool canPay = PlayerData.Jewels >= jewelCost;
                var jw = BigButton("", 720, 130, 40, canPay ? JewelColor : Disabled,
                    canPay ? Color.Lerp(JewelColor, Color.black, 0.3f) : DisabledDark, () => MoreTimeJewelClicked?.Invoke());
                jw.style.marginTop = 24;
                var jwRow = Row();
                jwRow.Add(JewelIcon(50));
                jwRow.Add(MakeLabel($"{jewelCost}  =  +{extraSeconds}s", 40, Color.white));
                jw.Add(jwRow);
                card.Add(jw);
            }

            var heartNote = MakeLabel("Giving up costs 1 heart", 30, InkSoft);
            heartNote.style.marginTop = 36;
            card.Add(heartNote);
            var btns = Row();
            btns.style.marginTop = 18;
            var home = BigButton("HOME", 330, 130, 46, Pink, PinkDark, () => GiveUpHomeClicked?.Invoke());
            home.style.marginRight = 30;
            btns.Add(home);
            btns.Add(BigButton("RETRY", 330, 130, 46, Green, GreenDark, () => RetryClicked?.Invoke()));
            card.Add(btns);
        }

        // ---------------- public API ----------------
        public void SetVisible(bool visible)
        {
            if (root != null) root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        VisualElement ownerPortrait;

        /// <summary>Shows the rendered hamster owner at the top center.</summary>
        public void SetOwnerPortrait(Texture texture)
        {
            if (ownerPortrait == null) return;
            ownerPortrait.style.backgroundImage = texture != null ? Background.FromRenderTexture(texture as RenderTexture) : new StyleBackground(StyleKeyword.None);
            ownerPortrait.style.display = texture != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetCoins(long coins, bool punch = false)
        {
            coinLabel.text = Format(coins);
            jewelLabel.text = Format(PlayerData.Jewels);
            energyLabel.text = PlayerData.Energy.ToString();
            if (punch) coinPunch = 1f;
        }

        static VisualElement Row()
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            r.style.justifyContent = Justify.Center;
            return r;
        }

        public void ShowToast(string text, Color color)
        {
            toast.text = text;
            toast.style.color = color;
            toastTimer = 1.8f;
        }

        public void FloatText(Vector3 world, string text, Color color, float size = 44f)
        {
            var l = MakeLabel(text, size, color);
            l.style.position = Position.Absolute;
            l.style.unityTextOutlineWidth = 2.5f;
            l.style.unityTextOutlineColor = new Color(0.15f, 0.1f, 0.05f, 0.9f);
            fxLayer.Add(l);
            floaters.Add(new Floater { label = l, world = world, life = 1.1f });
        }

        /// <summary>True if the pointer is over any UI element of the shared panel (HUD or menus).</summary>
        public bool IsPointerOverUI(Vector2 screenPos)
        {
            var panel = root?.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var picked = panel.Pick(p);
            return picked != null && picked != root && picked != panel.visualTree;
        }

        // ---------------- animation ----------------
        void Update()
        {
            if (root == null) return;
            if (cam == null) cam = Camera.main;

            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(toastTimer / 0.3f);
                float intro = Mathf.Clamp01((1.8f - toastTimer) / 0.15f);
                toast.style.opacity = Mathf.Min(a, intro);
                float s = 1f + 0.25f * (1f - intro);
                toast.style.scale = new Scale(new Vector3(s, s, 1f));
            }

            energyTick += Time.unscaledDeltaTime;
            if (energyTick >= 1f) { energyTick = 0f; energyLabel.text = PlayerData.Energy.ToString(); }

            if (timerPulse > 0f)
            {
                timerPulse = Mathf.Max(0f, timerPulse - Time.unscaledDeltaTime * 4f);
                float s = 1f + 0.12f * timerPulse;
                timerPill.style.scale = new Scale(new Vector3(s, s, 1f));
            }

            if (coinPunch > 0f)
            {
                coinPunch = Mathf.Max(0f, coinPunch - Time.unscaledDeltaTime * 5f);
                float s = 1f + 0.18f * Mathf.Sin(coinPunch * Mathf.PI);
                coinLabel.style.scale = new Scale(new Vector3(s, s, 1f));
            }

            var panel = root.panel;
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.age += Time.unscaledDeltaTime;
                float t = f.age / f.life;
                if (t >= 1f || cam == null || panel == null)
                {
                    f.label.RemoveFromHierarchy();
                    floaters.RemoveAt(i);
                    continue;
                }
                var w = f.world + Vector3.up * (t * 0.8f);
                Vector2 pp = RuntimePanelUtils.CameraTransformWorldToPanel(panel, w, cam);
                f.label.style.left = pp.x - 60f;
                f.label.style.top = pp.y - 30f;
                f.label.style.opacity = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
                float s = t < 0.15f ? Mathf.Lerp(0.5f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 1f, (t - 0.15f) / 0.85f);
                f.label.style.scale = new Scale(new Vector3(s, s, 1f));
            }
        }

        public static string Format(long v) => UIKit.Format(v);
    }
}
