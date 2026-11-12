using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PotMerge
{
    /// <summary>Runtime-built UI Toolkit HUD: coins, best level, spawn button, toasts and floating texts.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class MergeHUD : MonoBehaviour
    {
        public event Action SpawnClicked;
        public event Action RestartClicked;

        static readonly Color Ink = new Color(0.20f, 0.16f, 0.13f);
        static readonly Color Cream = new Color(1f, 0.98f, 0.93f);
        static readonly Color Green = new Color(0.33f, 0.74f, 0.36f);
        static readonly Color GreenDark = new Color(0.22f, 0.55f, 0.25f);
        static readonly Color Disabled = new Color(0.62f, 0.62f, 0.60f);
        static readonly Color Gold = new Color(1f, 0.78f, 0.18f);

        VisualElement root;
        VisualElement fxLayer;
        Label coinLabel;
        Label bestLabel;
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
            fxLayer.style.position = Position.Absolute;
            fxLayer.style.left = 0; fxLayer.style.top = 0; fxLayer.style.right = 0; fxLayer.style.bottom = 0;

            // ---------- top bar ----------
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.alignItems = Align.Center;
            top.style.paddingTop = 70; top.style.paddingLeft = 40; top.style.paddingRight = 40;

            var coinPill = Pill();
            var coinIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            coinIcon.style.width = 58; coinIcon.style.height = 58;
            Round(coinIcon, 29);
            coinIcon.style.backgroundColor = Gold;
            Border(coinIcon, 5, new Color(0.85f, 0.58f, 0.08f));
            coinIcon.style.marginRight = 18;
            coinLabel = MakeLabel("0", 54, Ink);
            coinPill.Add(coinIcon);
            coinPill.Add(coinLabel);

            var bestPill = Pill();
            bestLabel = MakeLabel("Best: -", 38, Ink);
            bestPill.Add(bestLabel);

            top.Add(coinPill);
            top.Add(bestPill);

            // ---------- toast ----------
            toast = MakeLabel("", 56, Color.white);
            toast.style.position = Position.Absolute;
            toast.style.top = 260; toast.style.left = 0; toast.style.right = 0;
            toast.style.unityTextAlign = TextAnchor.MiddleCenter;
            toast.style.unityTextOutlineWidth = 3f;
            toast.style.unityTextOutlineColor = new Color(0.15f, 0.1f, 0.05f);
            toast.style.opacity = 0;

            // ---------- bottom spawn button ----------
            var bottom = new VisualElement { pickingMode = PickingMode.Ignore };
            bottom.style.alignItems = Align.Center;
            bottom.style.paddingBottom = 110;

            spawnButton = new Button(() => SpawnClicked?.Invoke());
            spawnButton.text = "";
            spawnButton.style.width = 620; spawnButton.style.height = 190;
            Round(spawnButton, 60);
            spawnButton.style.backgroundColor = Green;
            Border(spawnButton, 0, Color.clear);
            spawnButton.style.borderBottomWidth = 16;
            spawnButton.style.borderBottomColor = GreenDark;
            spawnButton.style.alignItems = Align.Center;
            spawnButton.style.justifyContent = Justify.Center;
            spawnTitle = MakeLabel("ADD ITEM", 64, Color.white);
            spawnCost = MakeLabel("1 coin", 40, new Color(1f, 0.95f, 0.7f));
            spawnButton.Add(spawnTitle);
            spawnButton.Add(spawnCost);
            bottom.Add(spawnButton);

            // ---------- game over overlay ----------
            gameOverPanel = new VisualElement();
            gameOverPanel.style.position = Position.Absolute;
            gameOverPanel.style.left = 0; gameOverPanel.style.top = 0; gameOverPanel.style.right = 0; gameOverPanel.style.bottom = 0;
            gameOverPanel.style.backgroundColor = new Color(0.1f, 0.07f, 0.05f, 0.6f);
            gameOverPanel.style.alignItems = Align.Center;
            gameOverPanel.style.justifyContent = Justify.Center;
            gameOverPanel.style.display = DisplayStyle.None;
            var card = new VisualElement();
            card.style.backgroundColor = Cream;
            Round(card, 60);
            card.style.paddingTop = 70; card.style.paddingBottom = 70; card.style.paddingLeft = 80; card.style.paddingRight = 80;
            card.style.alignItems = Align.Center;
            card.style.width = 860;
            var title = MakeLabel("NO MORE MOVES", 76, Ink);
            title.style.marginBottom = 30;
            gameOverInfo = MakeLabel("", 44, Ink);
            gameOverInfo.style.unityTextAlign = TextAnchor.MiddleCenter;
            gameOverInfo.style.marginBottom = 50;
            gameOverInfo.style.whiteSpace = WhiteSpace.Normal;
            var restart = new Button(() => RestartClicked?.Invoke());
            restart.text = "";
            restart.style.width = 520; restart.style.height = 160;
            Round(restart, 50);
            restart.style.backgroundColor = Green;
            Border(restart, 0, Color.clear);
            restart.style.borderBottomWidth = 14;
            restart.style.borderBottomColor = GreenDark;
            restart.style.justifyContent = Justify.Center;
            restart.style.alignItems = Align.Center;
            restart.Add(MakeLabel("RESTART", 64, Color.white));
            card.Add(title);
            card.Add(gameOverInfo);
            card.Add(restart);
            gameOverPanel.Add(card);

            root.Add(top);
            root.Add(bottom);
            root.Add(fxLayer);
            root.Add(toast);
            root.Add(gameOverPanel);
        }

        // ---------------- public API ----------------
        public void SetCoins(long coins, bool punch = false)
        {
            coinLabel.text = Format(coins);
            if (punch) coinPunch = 1f;
        }

        public void SetBest(int level, int maxLevel, string name)
        {
            bestLabel.text = $"Best: {name}  {level}/{maxLevel}";
        }

        public void SetSpawn(string title, string subtitle, bool enabled)
        {
            spawnButton.style.backgroundColor = enabled ? Green : Disabled;
            spawnButton.style.borderBottomColor = enabled ? GreenDark : new Color(0.45f, 0.45f, 0.43f);
            spawnTitle.text = title;
            spawnCost.text = subtitle;
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

        public bool IsPointerOverUI(Vector2 screenPos)
        {
            var panel = root?.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var picked = panel.Pick(p);
            return picked != null && picked != root;
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

        // ---------------- helpers ----------------
        static Label MakeLabel(string text, float size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.paddingLeft = 0; l.style.paddingRight = 0;
            return l;
        }

        static VisualElement Pill()
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.flexDirection = FlexDirection.Row;
            v.style.alignItems = Align.Center;
            v.style.backgroundColor = Cream;
            v.style.paddingLeft = 24; v.style.paddingRight = 34; v.style.paddingTop = 14; v.style.paddingBottom = 14;
            Round(v, 50);
            v.style.borderBottomWidth = 8;
            v.style.borderBottomColor = new Color(0.82f, 0.76f, 0.66f);
            return v;
        }

        static void Round(VisualElement v, float r)
        {
            v.style.borderTopLeftRadius = r; v.style.borderTopRightRadius = r;
            v.style.borderBottomLeftRadius = r; v.style.borderBottomRightRadius = r;
        }

        static void Border(VisualElement v, float w, Color c)
        {
            v.style.borderTopWidth = w; v.style.borderBottomWidth = w; v.style.borderLeftWidth = w; v.style.borderRightWidth = w;
            v.style.borderTopColor = c; v.style.borderBottomColor = c; v.style.borderLeftColor = c; v.style.borderRightColor = c;
        }

        public static string Format(long v)
        {
            if (v >= 1_000_000_000) return (v / 1_000_000_000f).ToString("0.##") + "B";
            if (v >= 1_000_000) return (v / 1_000_000f).ToString("0.##") + "M";
            if (v >= 10_000) return (v / 1_000f).ToString("0.#") + "K";
            return v.ToString();
        }
    }
}
