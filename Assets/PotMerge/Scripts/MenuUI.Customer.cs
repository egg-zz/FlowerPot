using UnityEngine;
using UnityEngine.UIElements;
using static PotMerge.UIKit;

namespace PotMerge
{
    public partial class MenuUI
    {
        /// <summary>What the customer is wearing, with prices and a way to the shop. The game timer waits while it is open.</summary>
        public void OpenCustomer()
        {
            var customer = FindAnyObjectByType<OrderBar>();
            if (customer == null || customer.Outfit.Count == 0) return;
            var popup = Popup("MY OUTFIT", 820, out var body, ClosePopups);
            body.style.alignItems = Align.Center;
            bool buyable = false;
            foreach (var c in customer.Outfit)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.width = 640;
                row.style.marginBottom = 18;
                var img = new VisualElement { pickingMode = PickingMode.Ignore };
                img.style.width = 130; img.style.height = 130; img.style.marginRight = 24;
                if (c.icon != null) img.style.backgroundImage = new StyleBackground(c.icon);
                row.Add(img);
                var name = MakeLabel(c.displayName, 40, Ink);
                name.style.flexGrow = 1;
                row.Add(name);
                if (c.comingSoon) row.Add(MakeLabel("SOON", 36, InkSoft));
                else if (PlayerData.OwnsCostume(c.id)) row.Add(MakeLabel("OWNED", 36, Green));
                else
                {
                    buyable = true;
                    row.Add(c.currency == Currency.Jewel ? JewelIcon(48) : CoinIcon(48, 8));
                    row.Add(MakeLabel(c.price.ToString(), 42, Ink));
                }
                body.Add(row);
            }
            if (buyable)
            {
                var shop = BigButton("SHOP", 420, 130, 52, Pink, PinkDark, OpenShop);
                shop.style.marginTop = 20;
                body.Add(shop);
            }
            OpenPopup(popup);
        }
    }
}
