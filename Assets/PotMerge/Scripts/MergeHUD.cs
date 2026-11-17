using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static PotMerge.UIKit;

namespace PotMerge
{
    /// <summary>Runtime-built UI Toolkit HUD for the game screen: coins, home/settings, spawn button, toasts and floating texts.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class MergeHUD : MonoBehaviour
    {
        public event Action SpawnClicked;
        public event Action RestartClicked;
        public event Action HomeClicked;
        public event Action SettingsClicked;
        public event Action AdClicked;
        public event Action ExchangeClicked;

        VisualElement root;
        VisualElement fxLayer;
        Label coinLabel;
        Label jewelLabel;
        VisualElement outOfCoinsPanel;
        Button adButton;
        Label adLabel;
        Button exchangeButton;
        Label exchangeLabel;
        Button spawnButton;
        Label spawnTitle;
        Label spawnCost;
        Label toast;
        VisualElement gameOverPanel;
        Label gameOverInfo;
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

            // ---------- bottom spawn button ----------
            var bottom = new VisualElement { pickingMode = PickingMode.Ignore };
            bottom.style.alignItems = Align.Center;
            bottom.style.paddingBottom = 120;

            spawnButton = BigButton("", 440, 140, 50, Green, GreenDark, () => SpawnClicked?.Invoke());
            spawnTitle = MakeLabel("ADD ITEM", 54, Color.white);
            spawnCost = MakeLabel("", 30, new Color(1f, 0.95f, 0.7f));
            spawnButton.Add(spawnTitle);
            spawnButton.Add(spawnCost);
            bottom.Add(spawnButton);

            // ---------- game over overlay ----------
            gameOverPanel = new VisualElement();
            Stretch(gameOverPanel);
            gameOverPanel.style.backgroundColor = Dim;
            gameOverPanel.style.alignItems = Align.Center;
            gameOverPanel.style.justifyContent = Justify.Center;
            gameOverPanel.style.display = DisplayStyle.None;
            var card = Card(60);
            card.style.paddingTop = 70; card.style.paddingBottom = 70; card.style.paddingLeft = 80; card.style.paddingRight = 80;
            card.style.alignItems = Align.Center;
            card.style.width = 860;
            var title = MakeLabel("NO MORE MOVES", 72, Ink);
            title.style.marginBottom = 30;
            gameOverInfo = MakeLabel("", 42, Ink);
            gameOverInfo.style.unityTextAlign = TextAnchor.MiddleCenter;
            gameOverInfo.style.marginBottom = 50;
            gameOverInfo.style.whiteSpace = WhiteSpace.Normal;
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var homeBtn = BigButton("HOME", 300, 140, 50, Pink, PinkDark, () => HomeClicked?.Invoke());
            homeBtn.style.marginRight = 30;
            row.Add(homeBtn);
            row.Add(BigButton("RESTART", 360, 140, 50, Green, GreenDark, () => RestartClicked?.Invoke()));
            card.Add(title);
            card.Add(gameOverInfo);
            card.Add(row);
            gameOverPanel.Add(card);

            // ---------- out of coins (no merge left, can't pay for ADD ITEM) ----------
            outOfCoinsPanel = new VisualElement();
            Stretch(outOfCoinsPanel);
            outOfCoinsPanel.style.backgroundColor = Dim;
            outOfCoinsPanel.style.alignItems = Align.Center;
            outOfCoinsPanel.style.justifyContent = Justify.Center;
            outOfCoinsPanel.style.display = DisplayStyle.None;
            var oc = Card(80);
            oc.style.width = 860;
            oc.style.paddingTop = 60; oc.style.paddingBottom = 60; oc.style.paddingLeft = 60; oc.style.paddingRight = 60;
            oc.style.alignItems = Align.Center;
            oc.Add(MakeLabel("OUT OF COINS", 68, Ink));
            var ocInfo = MakeLabel("No merges left. Get coins to keep going!", 36, InkSoft);
            ocInfo.style.whiteSpace = WhiteSpace.Normal;
            ocInfo.style.unityTextAlign = TextAnchor.MiddleCenter;
            ocInfo.style.marginTop = 16; ocInfo.style.marginBottom = 40;
            oc.Add(ocInfo);

            adButton = BigButton("", 700, 130, 40, new Color(0.40f, 0.70f, 0.95f), new Color(0.25f, 0.50f, 0.78f), () => AdClicked?.Invoke());
            var adRow = Row();
            adRow.Add(Icon(IconShape.Play, 46, Color.white));
            adLabel = MakeLabel("", 40, Color.white);
            adLabel.style.marginLeft = 14;
            adRow.Add(adLabel);
            adButton.Add(adRow);
            oc.Add(adButton);

            exchangeButton = BigButton("", 700, 130, 40, JewelColor, Color.Lerp(JewelColor, Color.black, 0.3f), () => ExchangeClicked?.Invoke());
            exchangeButton.style.marginTop = 24;
            var exRow = Row();
            exRow.Add(JewelIcon(50));
            exchangeLabel = MakeLabel("", 40, Color.white);
            exRow.Add(exchangeLabel);
            exchangeButton.Add(exRow);
            oc.Add(exchangeButton);

            var ocBottom = Row();
            ocBottom.style.marginTop = 36;
            var ocHome = BigButton("HOME", 330, 120, 44, Pink, PinkDark, () => HomeClicked?.Invoke());
            ocHome.style.marginRight = 30;
            ocBottom.Add(ocHome);
            ocBottom.Add(BigButton("RESTART", 330, 120, 44, Green, GreenDark, () => RestartClicked?.Invoke()));
            oc.Add(ocBottom);
            outOfCoinsPanel.Add(oc);

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
            root.Add(bottom);
            root.Add(fxLayer);
            root.Add(toast);
            root.Add(outOfCoinsPanel);
            root.Add(gameOverPanel);
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
            if (punch) coinPunch = 1f;
        }

        /// <summary>Shows/hides the "out of coins" offer: rewarded ad for coins, or jewels for coins.</summary>
        public void ShowOutOfCoins(bool show)
        {
            outOfCoinsPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            int adsLeft = PlayerData.AdsLeftToday;
            adLabel.text = adsLeft > 0 ? $"WATCH AD  +{PlayerData.AdCoins} coins ({adsLeft} left)" : "NO ADS LEFT TODAY";
            SetButtonColors(adButton, adsLeft > 0 ? new Color(0.40f, 0.70f, 0.95f) : Disabled, adsLeft > 0 ? new Color(0.25f, 0.50f, 0.78f) : DisabledDark);
            var pack = PlayerData.CoinPacks[0];
            bool canPay = PlayerData.Jewels >= pack.jewels;
            exchangeLabel.text = $"{pack.jewels}  =  {pack.coins} coins";
            SetButtonColors(exchangeButton, canPay ? JewelColor : Disabled, canPay ? Color.Lerp(JewelColor, Color.black, 0.3f) : DisabledDark);
        }

        public bool OutOfCoinsVisible => outOfCoinsPanel != null && outOfCoinsPanel.style.display == DisplayStyle.Flex;

        static VisualElement Row()
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            r.style.justifyContent = Justify.Center;
            return r;
        }

        public void SetSpawn(string title, string subtitle, bool enabled)
        {
            SetButtonColors(spawnButton, enabled ? Green : Disabled, enabled ? GreenDark : DisabledDark);
            spawnTitle.text = title;
            spawnCost.text = subtitle;
            Show(spawnCost, !string.IsNullOrEmpty(subtitle));
        }

        public void ShowGameOver(bool show, string info)
        {
            gameOverInfo.text = info;
            gameOverPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
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
