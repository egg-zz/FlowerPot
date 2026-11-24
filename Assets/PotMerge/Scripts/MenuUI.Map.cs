using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static PotMerge.UIKit;

namespace PotMerge
{
    /// <summary>
    /// Level map: a winding path of level nodes from the bottom (level 1) up.
    /// Cleared levels are green with their stars, the next level is pink and pulses, later ones are locked.
    /// Tapping a cleared or the next level raises <see cref="LevelChosen"/>.
    /// </summary>
    public partial class MenuUI
    {
        /// <summary>The player picked a level on the map (1-based).</summary>
        public event Action<int> LevelChosen;
        /// <summary>The player closed the map.</summary>
        public event Action MapClosed;

        const float NodeStep = 230f;      // vertical distance between nodes
        const float NodeSize = 150f;
        const float CurrentSize = 184f;
        const float PathSwing = 250f;     // how far the path winds left/right

        static readonly Color MapBg = new Color(0.84f, 0.94f, 0.84f);
        static readonly Color MapPath = new Color(1f, 0.98f, 0.92f, 0.95f);
        static readonly Color NodeDone = new Color(0.40f, 0.76f, 0.40f);
        static readonly Color NodeDoneEdge = new Color(0.27f, 0.56f, 0.28f);
        static readonly Color NodeLocked = new Color(0.78f, 0.76f, 0.71f);
        static readonly Color NodeLockedEdge = new Color(0.62f, 0.60f, 0.56f);

        VisualElement mapLayer;
        ScrollView mapScroll;
        VisualElement mapContent;
        Label mapStars;
        Button mapPlay;
        Label mapPlayLabel;
        VisualElement mapCurrentNode;
        float mapScrollTarget = -1f;

        public bool MapVisible => mapLayer != null && mapLayer.style.display != DisplayStyle.None;

        VisualElement BuildMap()
        {
            var layer = new VisualElement();
            Stretch(layer);
            layer.style.backgroundColor = MapBg;

            mapScroll = new ScrollView(ScrollViewMode.Vertical);
            mapScroll.style.position = Position.Absolute;
            mapScroll.style.left = 0; mapScroll.style.right = 0; mapScroll.style.top = 0; mapScroll.style.bottom = 0;
            mapScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            mapScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            mapScroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            mapScroll.mouseWheelScrollSize = 160;
            mapScroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => ApplyMapScroll());
            layer.Add(mapScroll);

            // header band so the nodes scroll under a soft strip instead of the title
            var band = new VisualElement { pickingMode = PickingMode.Ignore };
            band.style.position = Position.Absolute;
            band.style.left = 0; band.style.right = 0; band.style.top = 0; band.style.height = 220;
            band.style.backgroundColor = new Color(0.98f, 0.95f, 0.86f);
            band.style.borderBottomWidth = 10; band.style.borderBottomColor = new Color(0.86f, 0.80f, 0.68f);
            band.style.borderBottomLeftRadius = 60; band.style.borderBottomRightRadius = 60;
            layer.Add(band);

            // header: home | LEVELS | total stars
            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.style.position = Position.Absolute;
            header.style.left = 40; header.style.right = 40; header.style.top = 70;
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.Add(IconButton(IconShape.Home, 110, () => MapClosed?.Invoke()));
            var title = MakeLabel("LEVELS", 64, Ink);
            header.Add(title);
            var starPill = Pill();
            starPill.style.minWidth = 170;
            starPill.Add(Icon(IconShape.Star, 50, Gold));
            mapStars = MakeLabel("0", 44, Ink);
            mapStars.style.marginLeft = 10;
            starPill.Add(mapStars);
            header.Add(starPill);
            layer.Add(header);

            // play button for the next level
            var playWrap = new VisualElement { pickingMode = PickingMode.Ignore };
            playWrap.style.position = Position.Absolute;
            playWrap.style.left = 0; playWrap.style.right = 0; playWrap.style.bottom = 90;
            playWrap.style.alignItems = Align.Center;
            mapPlay = BigButton("", 600, 160, 60, Green, GreenDark, () => LevelChosen?.Invoke(NextLevel));
            mapPlayLabel = MakeLabel("PLAY", 62, Color.white);
            mapPlay.Add(mapPlayLabel);
            playWrap.Add(mapPlay);
            layer.Add(playWrap);
            return layer;
        }

        int MapLevelCount => board != null ? board.LevelCount : 0;
        int NextLevel => Mathf.Clamp(PlayerData.LevelReached, 1, Mathf.Max(1, MapLevelCount));

        public void ShowMap()
        {
            ClosePopups();
            Show(titleLayer, false);
            Show(homeLayer, false);
            Show(townLayer, false);
            Show(mapLayer, true);
            RebuildMap();
        }

        void RebuildMap()
        {
            int count = MapLevelCount;
            int reached = PlayerData.LevelReached;
            bool allDone = reached > count;
            int total = 0;
            for (int i = 1; i <= count; i++) total += PlayerData.Stars(i);
            mapStars.text = total.ToString();
            mapPlayLabel.text = allDone ? $"REPLAY LEVEL {count}" : $"PLAY LEVEL {NextLevel}";
            mapPlayLabel.style.fontSize = allDone ? 48 : 62;

            mapScroll.Clear();
            float bottomPad = 420f, topPad = 380f;
            float height = bottomPad + topPad + (count) * NodeStep;
            mapContent = new VisualElement { pickingMode = PickingMode.Ignore };
            mapContent.style.height = height;
            mapContent.style.width = Length.Percent(100);
            mapScroll.Add(mapContent);

            // node centers (level 1 at the bottom); index count = the "more soon" cloud
            var centers = new List<Vector2>();
            for (int i = 0; i <= count; i++)
                centers.Add(new Vector2(540f + Mathf.Sin(i * 1.05f) * PathSwing, height - bottomPad - i * NodeStep));

            var path = new VisualElement { pickingMode = PickingMode.Ignore };
            Stretch(path);
            path.generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                // soft wide road, then dashes on top
                p.strokeColor = new Color(0.73f, 0.88f, 0.66f);
                p.lineWidth = 70f;
                p.BeginPath();
                for (int i = 0; i < centers.Count; i++) { if (i == 0) p.MoveTo(centers[i]); else p.LineTo(centers[i]); }
                p.Stroke();
                p.strokeColor = MapPath;
                p.lineWidth = 16f;
                for (int i = 0; i + 1 < centers.Count; i++)
                {
                    Vector2 a = centers[i], b = centers[i + 1];
                    for (int d = 1; d < 6; d += 2)
                    {
                        p.BeginPath();
                        p.MoveTo(Vector2.Lerp(a, b, d / 6f));
                        p.LineTo(Vector2.Lerp(a, b, (d + 0.8f) / 6f));
                        p.Stroke();
                    }
                }
            };
            mapContent.Add(path);

            mapCurrentNode = null;
            for (int lv = 1; lv <= count; lv++)
            {
                bool done = lv < reached;
                bool current = lv == reached;
                mapContent.Add(MapNode(lv, centers[lv - 1], done, current));
            }

            // "more soon" cloud at the top of the path
            var soon = Pill();
            soon.style.position = Position.Absolute;
            soon.style.left = centers[count].x - 190f; soon.style.top = centers[count].y - 50f;
            soon.style.width = 380f;
            soon.style.justifyContent = Justify.Center;
            soon.Add(MakeLabel("MORE SOON!", 40, InkSoft));
            mapContent.Add(soon);

            // scroll so the next level sits a bit below the middle
            int focus = Mathf.Clamp(reached, 1, count);
            mapScrollTarget = centers[focus - 1].y;
            ApplyMapScroll();
        }

        /// <summary>Scrolls to the pending target once the scroll range is known (retried every frame until then).</summary>
        void ApplyMapScroll()
        {
            if (mapScrollTarget < 0f || mapScroll == null) return;
            float view = mapScroll.contentViewport.layout.height;
            float content = mapScroll.contentContainer.layout.height;
            if (float.IsNaN(view) || view <= 0f || content <= view) return;
            if (mapScroll.verticalScroller.highValue <= 0f) return; // range not updated yet
            float y = Mathf.Clamp(mapScrollTarget - view * 0.6f, 0f, content - view);
            mapScroll.scrollOffset = new Vector2(0f, y);
            mapScrollTarget = -1f;
        }

        VisualElement MapNode(int lv, Vector2 center, bool done, bool current)
        {
            float size = current ? CurrentSize : NodeSize;
            var wrap = new VisualElement { pickingMode = PickingMode.Ignore };
            wrap.style.position = Position.Absolute;
            wrap.style.width = 260f;
            wrap.style.left = center.x - 130f;
            wrap.style.top = center.y - size * 0.5f;
            wrap.style.alignItems = Align.Center;

            Color face = done ? NodeDone : current ? Pink : NodeLocked;
            Color edge = done ? NodeDoneEdge : current ? PinkDark : NodeLockedEdge;
            var btn = BigButton("", size, size, 60, face, edge, () =>
            {
                if (done || current) LevelChosen?.Invoke(lv);
                else SoundFx.Error();
            });
            Border(btn, 6, Color.white);
            btn.style.borderBottomWidth = 14; btn.style.borderBottomColor = edge;
            if (done || current)
            {
                var n = MakeLabel(lv.ToString(), current ? 76 : 62, Color.white);
                n.style.unityTextOutlineWidth = 3f;
                n.style.unityTextOutlineColor = Color.Lerp(edge, Color.black, 0.2f);
                btn.Add(n);
            }
            else btn.Add(Icon(IconShape.Lock, 64, new Color(1f, 1f, 1f, 0.85f)));
            wrap.Add(btn);

            // timed levels get a little clock badge, every 10th level shows its jewel reward until cleared
            var level = board != null && board.LevelCount >= lv ? board.levels.Get(lv) : null;
            if (level != null && level.seconds > 0)
            {
                var clock = new VisualElement { pickingMode = PickingMode.Ignore };
                clock.style.position = Position.Absolute;
                clock.style.left = 130f + size * 0.5f - 44f; clock.style.top = -6f;
                clock.style.width = 52; clock.style.height = 52;
                clock.style.backgroundColor = Color.white;
                Round(clock, 26);
                clock.style.alignItems = Align.Center; clock.style.justifyContent = Justify.Center;
                clock.Add(Icon(IconShape.Clock, 42, new Color(0.40f, 0.70f, 0.95f)));
                wrap.Add(clock);
            }
            if (lv % PlayerData.JewelLevelInterval == 0 && !done)
            {
                var gem = JewelIcon(56, 0);
                gem.style.position = Position.Absolute;
                gem.style.left = 130f - size * 0.5f - 18f; gem.style.top = -10f;
                wrap.Add(gem);
            }

            if (done)
            {
                var stars = new VisualElement { pickingMode = PickingMode.Ignore };
                stars.style.flexDirection = FlexDirection.Row;
                stars.style.marginTop = 4;
                int got = PlayerData.Stars(lv);
                for (int i = 0; i < 3; i++)
                {
                    var st = Icon(IconShape.Star, 40, i < got ? Gold : new Color(1f, 1f, 1f, 0.7f));
                    st.style.marginLeft = 2; st.style.marginRight = 2;
                    stars.Add(st);
                }
                wrap.Add(stars);
            }
            else if (current)
            {
                var tag = MakeLabel("PLAY", 34, Color.white);
                tag.style.marginTop = 6;
                tag.style.unityTextOutlineWidth = 3f;
                tag.style.unityTextOutlineColor = PinkDark;
                wrap.Add(tag);
                mapCurrentNode = btn;
            }
            return wrap;
        }

        void AnimateMap(float t)
        {
            if (!MapVisible) return;
            ApplyMapScroll();
            if (mapCurrentNode != null)
            {
                float s = 1f + 0.06f * Mathf.Sin(t * 4f);
                mapCurrentNode.style.scale = new Scale(new Vector3(s, s, 1f));
            }
            float p = 1f + 0.03f * Mathf.Sin(t * 3f);
            mapPlay.style.scale = new Scale(new Vector3(p, p, 1f));
        }
    }
}
