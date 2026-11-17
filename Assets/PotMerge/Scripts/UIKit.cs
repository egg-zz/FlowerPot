using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PotMerge
{
    public enum IconShape { Home, Gear, Book, Bag, Close, Check, Star }

    /// <summary>Shared look for all runtime-built UI Toolkit screens (reference resolution 1080x1920).</summary>
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.20f, 0.16f, 0.13f);
        public static readonly Color InkSoft = new Color(0.45f, 0.38f, 0.32f);
        public static readonly Color Cream = new Color(1f, 0.98f, 0.93f);
        public static readonly Color CreamShadow = new Color(0.82f, 0.76f, 0.66f);
        public static readonly Color Green = new Color(0.33f, 0.74f, 0.36f);
        public static readonly Color GreenDark = new Color(0.22f, 0.55f, 0.25f);
        public static readonly Color Pink = new Color(0.98f, 0.52f, 0.62f);
        public static readonly Color PinkDark = new Color(0.80f, 0.33f, 0.45f);
        public static readonly Color Disabled = new Color(0.62f, 0.62f, 0.60f);
        public static readonly Color DisabledDark = new Color(0.45f, 0.45f, 0.43f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.18f);
        public static readonly Color Dim = new Color(0.1f, 0.07f, 0.05f, 0.6f);

        static bool fontLoaded;
        static Font roundFont;

        /// <summary>
        /// Optional rounded font: put a font at Assets/PotMerge/Resources/RoundFont.ttf (or .otf) and every label uses it.
        /// Without it the default UI font is used.
        /// </summary>
        public static Font RoundFont
        {
            get
            {
                if (!fontLoaded) { roundFont = Resources.Load<Font>("RoundFont"); fontLoaded = true; }
                return roundFont;
            }
        }

        static bool titleFontLoaded;
        static Font titleFont;

        /// <summary>Puffy display font for the title (Assets/PotMerge/Resources/TitleFont.ttf, Modak, SIL OFL).</summary>
        public static Font TitleFont
        {
            get
            {
                if (!titleFontLoaded) { titleFont = Resources.Load<Font>("TitleFont"); titleFontLoaded = true; }
                return titleFont;
            }
        }

        public static Label MakeLabel(string text, float size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            if (RoundFont != null) l.style.unityFontDefinition = FontDefinition.FromFont(RoundFont);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            return l;
        }

        public static VisualElement Pill()
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.flexDirection = FlexDirection.Row;
            v.style.alignItems = Align.Center;
            v.style.backgroundColor = Cream;
            v.style.paddingLeft = 24; v.style.paddingRight = 34; v.style.paddingTop = 14; v.style.paddingBottom = 14;
            Round(v, 50);
            v.style.borderBottomWidth = 8;
            v.style.borderBottomColor = CreamShadow;
            return v;
        }

        public static VisualElement Card(float radius = 64f)
        {
            var v = new VisualElement();
            v.style.backgroundColor = Cream;
            Round(v, radius);
            v.style.borderBottomWidth = 10;
            v.style.borderBottomColor = CreamShadow;
            return v;
        }

        /// <summary>Chunky game button with a darker bottom edge and a press animation.</summary>
        public static Button BigButton(string text, float width, float height, float fontSize, Color face, Color edge, Action onClick)
        {
            var b = new Button(() => { SoundFx.Tap(); onClick?.Invoke(); }) { text = "" };
            b.style.width = width; b.style.height = height;
            Round(b, height * 0.5f); // pill shape
            Border(b, 0, Color.clear);
            b.style.borderBottomWidth = Mathf.Round(height * 0.085f);
            b.style.backgroundColor = face;
            b.style.borderBottomColor = edge;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            b.style.marginLeft = 0; b.style.marginRight = 0; b.style.marginTop = 0; b.style.marginBottom = 0;
            b.style.paddingLeft = 0; b.style.paddingRight = 0;
            if (!string.IsNullOrEmpty(text)) b.Add(MakeLabel(text, fontSize, Color.white));
            AddPress(b);
            return b;
        }

        public static void SetButtonColors(Button b, Color face, Color edge)
        {
            b.style.backgroundColor = face;
            b.style.borderBottomColor = edge;
        }

        /// <summary>Round cream button with a drawn icon.</summary>
        public static Button IconButton(IconShape shape, float size, Action onClick, Color? iconColor = null)
        {
            var b = new Button(() => { SoundFx.Tap(); onClick?.Invoke(); }) { text = "" };
            b.style.width = size; b.style.height = size;
            Round(b, size * 0.5f);
            Border(b, 0, Color.clear);
            b.style.borderBottomWidth = 8;
            b.style.backgroundColor = Cream;
            b.style.borderBottomColor = CreamShadow;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            b.style.marginLeft = 0; b.style.marginRight = 0; b.style.marginTop = 0; b.style.marginBottom = 0;
            b.style.paddingLeft = 0; b.style.paddingRight = 0; b.style.paddingTop = 0; b.style.paddingBottom = 0;
            b.Add(Icon(shape, size * 0.56f, iconColor ?? Ink));
            AddPress(b);
            return b;
        }

        static void AddPress(VisualElement b)
        {
            b.RegisterCallback<PointerDownEvent>(_ => b.style.scale = new Scale(new Vector3(0.94f, 0.94f, 1f)), TrickleDown.TrickleDown);
            b.RegisterCallback<PointerUpEvent>(_ => b.style.scale = new Scale(Vector3.one));
            b.RegisterCallback<PointerLeaveEvent>(_ => b.style.scale = new Scale(Vector3.one));
        }

        /// <summary>Vector icon drawn with Painter2D (no image assets needed).</summary>
        public static VisualElement Icon(IconShape shape, float size, Color color)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.width = size; v.style.height = size;
            v.style.flexShrink = 0;
            v.generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                float s = size;
                p.fillColor = color;
                p.strokeColor = color;
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;
                switch (shape)
                {
                    case IconShape.Home:
                        p.BeginPath();
                        p.MoveTo(new Vector2(s * 0.5f, s * 0.08f));
                        p.LineTo(new Vector2(s * 0.95f, s * 0.5f));
                        p.LineTo(new Vector2(s * 0.82f, s * 0.5f));
                        p.LineTo(new Vector2(s * 0.82f, s * 0.92f));
                        p.LineTo(new Vector2(s * 0.6f, s * 0.92f));
                        p.LineTo(new Vector2(s * 0.6f, s * 0.66f));
                        p.LineTo(new Vector2(s * 0.4f, s * 0.66f));
                        p.LineTo(new Vector2(s * 0.4f, s * 0.92f));
                        p.LineTo(new Vector2(s * 0.18f, s * 0.92f));
                        p.LineTo(new Vector2(s * 0.18f, s * 0.5f));
                        p.LineTo(new Vector2(s * 0.05f, s * 0.5f));
                        p.ClosePath();
                        p.Fill();
                        break;
                    case IconShape.Gear:
                    {
                        var c = new Vector2(s * 0.5f, s * 0.5f);
                        int teeth = 8;
                        float rOut = s * 0.48f, rIn = s * 0.36f;
                        p.BeginPath();
                        for (int i = 0; i < teeth * 4; i++)
                        {
                            float a = (i / (float)(teeth * 4)) * Mathf.PI * 2f;
                            float r = (i % 4 == 0 || i % 4 == 1) ? rOut : rIn;
                            var pt = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                            if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
                        }
                        p.ClosePath();
                        p.Fill();
                        p.fillColor = Cream;
                        p.BeginPath();
                        p.Arc(c, s * 0.15f, 0f, 360f);
                        p.Fill();
                        break;
                    }
                    case IconShape.Book:
                        p.lineWidth = s * 0.08f;
                        p.BeginPath();
                        p.MoveTo(new Vector2(s * 0.5f, s * 0.22f));
                        p.LineTo(new Vector2(s * 0.08f, s * 0.12f));
                        p.LineTo(new Vector2(s * 0.08f, s * 0.8f));
                        p.LineTo(new Vector2(s * 0.5f, s * 0.9f));
                        p.LineTo(new Vector2(s * 0.92f, s * 0.8f));
                        p.LineTo(new Vector2(s * 0.92f, s * 0.12f));
                        p.ClosePath();
                        p.Fill();
                        p.strokeColor = Cream;
                        p.BeginPath();
                        p.MoveTo(new Vector2(s * 0.5f, s * 0.26f));
                        p.LineTo(new Vector2(s * 0.5f, s * 0.84f));
                        p.Stroke();
                        break;
                    case IconShape.Bag:
                        p.BeginPath();
                        p.MoveTo(new Vector2(s * 0.12f, s * 0.34f));
                        p.LineTo(new Vector2(s * 0.88f, s * 0.34f));
                        p.LineTo(new Vector2(s * 0.82f, s * 0.94f));
                        p.LineTo(new Vector2(s * 0.18f, s * 0.94f));
                        p.ClosePath();
                        p.Fill();
                        p.lineWidth = s * 0.08f;
                        p.BeginPath();
                        p.Arc(new Vector2(s * 0.5f, s * 0.34f), s * 0.2f, 180f, 360f);
                        p.Stroke();
                        break;
                    case IconShape.Star:
                    {
                        // chubby star: 5 points with rounded joins
                        var c = new Vector2(s * 0.5f, s * 0.54f);
                        p.lineWidth = s * 0.12f;
                        p.BeginPath();
                        for (int i = 0; i < 10; i++)
                        {
                            float a = -Mathf.PI / 2f + i * Mathf.PI / 5f;
                            float r = i % 2 == 0 ? s * 0.42f : s * 0.2f;
                            var pt = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                            if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
                        }
                        p.ClosePath();
                        p.Fill();
                        p.Stroke();
                        break;
                    }
                    case IconShape.Close:
                        p.lineWidth = s * 0.16f;
                        p.BeginPath();
                        p.MoveTo(new Vector2(s * 0.2f, s * 0.2f)); p.LineTo(new Vector2(s * 0.8f, s * 0.8f));
                        p.MoveTo(new Vector2(s * 0.8f, s * 0.2f)); p.LineTo(new Vector2(s * 0.2f, s * 0.8f));
                        p.Stroke();
                        break;
                    case IconShape.Check:
                        p.lineWidth = s * 0.16f;
                        p.BeginPath();
                        p.MoveTo(new Vector2(s * 0.15f, s * 0.52f));
                        p.LineTo(new Vector2(s * 0.42f, s * 0.78f));
                        p.LineTo(new Vector2(s * 0.86f, s * 0.24f));
                        p.Stroke();
                        break;
                }
            };
            return v;
        }

        /// <summary>Gold coin: dark rim, bright face, inner ring and a small highlight.</summary>
        public static VisualElement CoinIcon(float size, float marginRight = 16f)
        {
            var rim = new VisualElement { pickingMode = PickingMode.Ignore };
            rim.style.width = size; rim.style.height = size;
            rim.style.flexShrink = 0;
            Round(rim, size * 0.5f);
            rim.style.backgroundColor = new Color(0.86f, 0.55f, 0.08f);
            rim.style.marginRight = marginRight;
            rim.style.alignItems = Align.Center;
            rim.style.justifyContent = Justify.Center;

            float f = size * 0.8f;
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.style.width = f; face.style.height = f;
            Round(face, f * 0.5f);
            face.style.backgroundColor = Gold;
            face.style.alignItems = Align.Center;
            face.style.justifyContent = Justify.Center;
            rim.Add(face);

            float r = size * 0.5f;
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.style.width = r; ring.style.height = r;
            Round(ring, r * 0.5f);
            Border(ring, Mathf.Max(2f, size * 0.07f), new Color(0.93f, 0.66f, 0.12f));
            face.Add(ring);

            float h = size * 0.16f;
            var shine = new VisualElement { pickingMode = PickingMode.Ignore };
            shine.style.position = Position.Absolute;
            shine.style.width = h; shine.style.height = h;
            shine.style.left = f * 0.18f; shine.style.top = f * 0.16f;
            Round(shine, h * 0.5f);
            shine.style.backgroundColor = new Color(1f, 0.97f, 0.8f);
            face.Add(shine);
            return rim;
        }

        /// <summary>Full-screen dimmed overlay with a centered card. Tapping outside the card calls onClose.</summary>
        public static VisualElement Popup(string title, float width, out VisualElement body, Action onClose)
        {
            var overlay = new VisualElement();
            Stretch(overlay);
            overlay.style.backgroundColor = Dim;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.RegisterCallback<PointerDownEvent>(e => { if (e.target == overlay) onClose?.Invoke(); });

            var card = Card(80);
            card.style.width = width;
            card.style.paddingTop = 50; card.style.paddingBottom = 60; card.style.paddingLeft = 56; card.style.paddingRight = 56;
            card.style.alignItems = Align.Stretch;

            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 36;
            header.Add(MakeLabel(title, 64, Ink));
            header.Add(IconButton(IconShape.Close, 96, onClose));
            card.Add(header);

            body = new VisualElement { pickingMode = PickingMode.Ignore };
            card.Add(body);
            overlay.Add(card);
            return overlay;
        }

        public static void Stretch(VisualElement v)
        {
            v.style.position = Position.Absolute;
            v.style.left = 0; v.style.top = 0; v.style.right = 0; v.style.bottom = 0;
        }

        public static void Round(VisualElement v, float r)
        {
            v.style.borderTopLeftRadius = r; v.style.borderTopRightRadius = r;
            v.style.borderBottomLeftRadius = r; v.style.borderBottomRightRadius = r;
        }

        public static void Border(VisualElement v, float w, Color c)
        {
            v.style.borderTopWidth = w; v.style.borderBottomWidth = w; v.style.borderLeftWidth = w; v.style.borderRightWidth = w;
            v.style.borderTopColor = c; v.style.borderBottomColor = c; v.style.borderLeftColor = c; v.style.borderRightColor = c;
        }

        public static void Show(VisualElement v, bool show) => v.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        public static string Format(long v)
        {
            if (v >= 1_000_000_000) return (v / 1_000_000_000f).ToString("0.##") + "B";
            if (v >= 1_000_000) return (v / 1_000_000f).ToString("0.##") + "M";
            if (v >= 10_000) return (v / 1_000f).ToString("0.#") + "K";
            return v.ToString();
        }
    }
}
