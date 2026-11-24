using UnityEngine;
using UnityEngine.UIElements;
using static PotMerge.UIKit;

namespace PotMerge
{
    public partial class MenuUI
    {
        /// <summary>Shop tab: energy packs for coins, a full refill for jewels.</summary>
        void AddEnergySection(VisualElement body)
        {
            int now = PlayerData.Energy;
            bool full = now >= PlayerData.MaxEnergy;

            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.style.flexDirection = FlexDirection.Row;
            line.style.justifyContent = Justify.Center;
            line.style.alignItems = Align.Center;
            line.style.marginBottom = 24;
            line.Add(Icon(IconShape.Bolt, 64, EnergyColor));
            line.Add(MakeLabel($"{now}/{PlayerData.MaxEnergy}", 52, Ink));
            body.Add(line);

            var row = ShopRow();
            for (int i = 0; i < PlayerData.EnergyPacks.Length; i++)
            {
                int index = i;
                var pack = PlayerData.EnergyPacks[i];
                bool ok = PlayerData.EnergyPackFits(i) && PlayerData.Coins >= pack.coins;
                row.Add(EnergyCard($"+{pack.energy}", false, pack.coins, ok, () => PlayerData.BuyEnergyPack(index)));
            }
            bool refill = !full && PlayerData.Jewels >= PlayerData.EnergyRefillJewels;
            row.Add(EnergyCard("FULL", true, PlayerData.EnergyRefillJewels, refill, PlayerData.RefillEnergyWithJewels));
            body.Add(row);

            var note = MakeLabel(full ? "Energy is full!" : $"Energy never goes above {PlayerData.MaxEnergy}.", 28, InkSoft);
            note.style.unityTextAlign = TextAnchor.MiddleCenter;
            body.Add(note);
        }

        VisualElement EnergyCard(string amount, bool jewel, int price, bool enabled, System.Func<bool> buy)
        {
            var card = ShopCard(Icon(IconShape.Bolt, 90, EnergyColor), amount);
            Color face = !enabled ? Disabled : jewel ? JewelColor : Gold;
            var btn = BigButton("", 230, 96, 36, face, enabled ? Color.Lerp(face, Color.black, 0.3f) : DisabledDark, () =>
            {
                // check again: energy may have come back since the card was drawn
                if (!enabled || (jewel && PlayerData.Energy >= PlayerData.MaxEnergy) || !buy()) { SoundFx.Error(); return; }
                SoundFx.Unlock();
                RefreshHome();
                OpenShop();
            });
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.style.flexDirection = FlexDirection.Row; line.style.alignItems = Align.Center;
            line.Add(jewel ? Icon(IconShape.Gem, 40, Color.white) : CoinIcon(40, 6));
            line.Add(MakeLabel(" " + price, 38, Color.white));
            btn.Add(line);
            card.Add(btn);
            return card;
        }
    }
}
