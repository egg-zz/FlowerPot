using System;
using UnityEngine;

namespace PotMerge
{
    /// <summary>Everything that can stand in the town: shop decorations to buy, and rewards for finishing the collection.</summary>
    [CreateAssetMenu(menuName = "PotMerge/Town Database", fileName = "TownDatabase")]
    public class TownDatabase : ScriptableObject
    {
        /// <summary>Shop index of the plaza (0-3 = the four chains).</summary>
        public const int Plaza = 4;

        [Serializable]
        public class Item
        {
            public string id;
            public string displayName;
            [Tooltip("0 Flower, 1 Cafe, 2 Bakery, 3 Ice Cream, 4 plaza")] public int shop;
            [Tooltip("Given for a finished collection instead of being sold")] public bool reward;
            public Currency currency;
            public int price;
            [Tooltip("Where it stands. Decorations: from their shop (x = toward the plaza, z = back). Rewards: in the town")] public Vector2 position;
            public float yaw;
            public GameObject model;
            public Texture2D icon;
        }

        public Item[] items;
        public GameObject plazaModel;

        public Item Find(string id)
        {
            foreach (var i in items) if (i.id == id) return i;
            return null;
        }
    }
}
