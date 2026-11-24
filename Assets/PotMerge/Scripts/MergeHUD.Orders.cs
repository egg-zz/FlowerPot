using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static PotMerge.UIKit;

namespace PotMerge
{
    /// <summary>Order widgets: the customer next to the shop owner, their speech bubble and the delivery zone.</summary>
    public partial class MergeHUD
    {
        public struct OrderView { public Texture2D icon; public bool done; public bool ready; }

        /// <summary>The customer (or the costume on the clear card) was tapped.</summary>
        public event Action CustomerClicked;

        VisualElement bar, bubble, bubbleTail, bubbleItems, ghost, patience, patienceFill;
        Label bubbleReward;
        readonly List<VisualElement> readyIcons = new List<VisualElement>();
        static readonly Color BubbleColor = new Color(1f, 0.98f, 0.93f);
        bool orderVisible;
        int deliveryMode;
        Vector3 ghostWorld;
        // the customer stands at about this fraction of the bar's width
        const float CustomerLeft = 0.50f, CustomerCenter = 0.62f;

        void BuildOrders()
        {
            bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.position = Position.Absolute;
            bar.style.left = 20; bar.style.right = 20; bar.style.top = 222;
            bar.style.height = 420;
            Round(bar, 56);
            bar.style.overflow = Overflow.Hidden;
            bar.style.display = DisplayStyle.None;
            var tap = new VisualElement();
            tap.style.position = Position.Absolute;
            tap.style.left = Length.Percent(52); tap.style.width = Length.Percent(20);
            tap.style.top = 90; tap.style.bottom = 90;
            tap.RegisterCallback<ClickEvent>(e => { SoundFx.Tap(); CustomerClicked?.Invoke(); });
            bar.Add(tap);

            bubble = new VisualElement { pickingMode = PickingMode.Ignore };
            bubble.style.position = Position.Absolute;
            bubble.style.right = 24; bubble.style.top = 400;
            bubble.style.width = 300;
            bubble.style.alignItems = Align.Center;
            bubble.style.backgroundColor = BubbleColor;
            bubble.style.paddingTop = 16; bubble.style.paddingBottom = 14;
            Round(bubble, 44);
            bubble.style.borderBottomWidth = 8; bubble.style.borderBottomColor = CreamShadow;
            bubble.style.display = DisplayStyle.None;
            bubbleTail = new VisualElement { pickingMode = PickingMode.Ignore };
            bubbleTail.style.position = Position.Absolute;
            bubbleTail.style.left = -14; bubbleTail.style.top = 56;
            bubbleTail.style.width = 36; bubbleTail.style.height = 36;
            bubbleTail.style.rotate = new Rotate(45f);
            bubbleTail.style.backgroundColor = BubbleColor;
            bubble.Add(bubbleTail);
            bubbleItems = Row();
            bubble.Add(bubbleItems);
            var pay = Row();
            pay.name = "pay";
            pay.style.marginTop = 4;
            pay.Add(CoinIcon(38, 6));
            bubbleReward = MakeLabel("+1", 34, Ink);
            pay.Add(bubbleReward);
            bubble.Add(pay);
            patience = new VisualElement { pickingMode = PickingMode.Ignore };
            patience.style.width = 230; patience.style.height = 16; patience.style.marginTop = 6;
            patience.style.backgroundColor = CreamShadow;
            Round(patience, 8);
            patienceFill = new VisualElement { pickingMode = PickingMode.Ignore };
            patienceFill.style.height = 16;
            Round(patienceFill, 8);
            patience.Add(patienceFill);
            patience.style.display = DisplayStyle.None;
            bubble.Add(patience);

            ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            ghost.style.position = Position.Absolute;
            ghost.style.width = 130; ghost.style.height = 130;
            ghost.style.display = DisplayStyle.None;

            root.Add(bar);
            root.Add(bubble);
            fxLayer.Add(ghost);
        }

        public void SetBarTexture(Texture texture)
        {
            if (bar != null) bar.style.backgroundImage = Background.FromRenderTexture(texture as RenderTexture);
        }

        /// <summary>Shows the customer with the order; <paramref name="reward"/> 0 hides the coin line.</summary>
        public void SetOrder(List<OrderView> items, int reward)
        {
            SetOrderLayout(true);
            bubbleItems.Clear();
            readyIcons.Clear();
            foreach (var it in items)
            {
                var img = new VisualElement { pickingMode = PickingMode.Ignore };
                img.style.width = 86; img.style.height = 86;
                img.style.marginLeft = 2; img.style.marginRight = 2;
                if (it.icon != null) img.style.backgroundImage = new StyleBackground(it.icon);
                if (it.done)
                {
                    img.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, 0.35f);
                    var check = Icon(IconShape.Check, 56, Green);
                    check.style.position = Position.Absolute;
                    check.style.left = 15; check.style.top = 15;
                    img.Add(check);
                }
                else if (it.ready) readyIcons.Add(img);
                bubbleItems.Add(img);
            }
            bubbleReward.text = "+" + reward;
            Show(bubble.Q("pay"), reward > 0);
            Show(bubble, true);
        }

        public void HideOrderBubble() => Show(bubble, false);

        /// <summary>How long the customer still waits (1..0), negative hides the bar.</summary>
        public void SetPatience(float k)
        {
            Show(patience, k >= 0f);
            if (k < 0f) return;
            patienceFill.style.width = Length.Percent(k * 100f);
            patienceFill.style.backgroundColor = k > 0.3f ? Green : new Color(0.92f, 0.33f, 0.33f);
        }


        /// <summary>Order levels: the bar (owner + customers) replaces the owner portrait.</summary>
        public void SetOrderLayout(bool on)
        {
            if (bar == null || orderVisible == on) return;
            orderVisible = on;
            Show(bar, on);
            ownerPortrait.style.visibility = on ? Visibility.Hidden : Visibility.Visible;
        }

        /// <summary>True if the screen point is on the customer or the speech bubble.</summary>
        public bool IsOverDelivery(Vector2 screenPos)
        {
            var panel = root?.panel;
            if (panel == null || !orderVisible) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            if (bubble.style.display == DisplayStyle.Flex && bubble.worldBound.Contains(p)) return true;
            var r = bar.worldBound;
            r.xMin += r.width * CustomerLeft;
            return r.Contains(p);
        }

        /// <summary>0 = none, 1 = this item is wanted, 2 = not wanted.</summary>
        public void SetDeliveryHighlight(int mode)
        {
            if (mode == deliveryMode) return;
            deliveryMode = mode;
            var c = mode == 1 ? new Color(0.72f, 0.94f, 0.70f) : mode == 2 ? new Color(1f, 0.76f, 0.74f) : BubbleColor;
            bubble.style.backgroundColor = c;
            bubbleTail.style.backgroundColor = c;
        }

        public void ShowDeliverGhost(Vector3 world, Texture2D icon)
        {
            ghostWorld = world;
            if (ghost.style.display == DisplayStyle.Flex) return;
            if (icon != null) ghost.style.backgroundImage = new StyleBackground(icon);
            Show(ghost, true);
        }

        public void HideDeliverGhost()
        {
            if (ghost != null && ghost.style.display == DisplayStyle.Flex) Show(ghost, false);
        }

        void AnimateOrders()
        {
            if (!orderVisible) return;
            float s = 1f + 0.10f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
            foreach (var v in readyIcons) v.style.scale = new Scale(new Vector3(s, s, 1f));
            if (ghost.style.display != DisplayStyle.Flex || root.panel == null || cam == null) return;
            float t = Mathf.Repeat(Time.unscaledTime, 1.8f) / 1.8f;
            Vector2 from = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, ghostWorld, cam);
            var pos = Vector2.Lerp(from, new Vector2(bar.worldBound.x + bar.worldBound.width * CustomerCenter, bar.worldBound.center.y), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.75f)));
            ghost.style.left = pos.x - 65f;
            ghost.style.top = pos.y - 65f;
            ghost.style.opacity = 0.8f * (1f - Mathf.Clamp01((t - 0.75f) / 0.2f));
        }

        /// <summary>One costume the customer wore, on the clear card. Tap to see it.</summary>
        VisualElement CostumeTeaser(CostumeDatabase.Costume c)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = new Color(0.87f, 0.93f, 0.98f);
            row.style.paddingLeft = 24; row.style.paddingRight = 34; row.style.paddingTop = 8; row.style.paddingBottom = 8;
            row.style.marginBottom = 30;
            Round(row, 50);
            var img = new VisualElement { pickingMode = PickingMode.Ignore };
            img.style.width = 110; img.style.height = 110; img.style.marginRight = 18;
            if (c.icon != null) img.style.backgroundImage = new StyleBackground(c.icon);
            row.Add(img);
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.Add(MakeLabel(c.displayName, 36, Ink));
            col.Add(MakeLabel(c.comingSoon ? "COMING SOON" : "SEE IT IN THE SHOP", 28, c.comingSoon ? InkSoft : PinkDark));
            row.Add(col);
            row.RegisterCallback<ClickEvent>(e => { SoundFx.Tap(); CustomerClicked?.Invoke(); });
            return row;
        }
    }
}
