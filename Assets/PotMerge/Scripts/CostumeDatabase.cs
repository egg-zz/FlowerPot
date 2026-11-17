using System;
using UnityEngine;

namespace PotMerge
{
    public enum CostumeSlot { Hat, Face, Neck, Body, Hand }
    public enum Currency { Coin, Jewel }

    /// <summary>
    /// Costumes for the hamster shop owner. Each costume model is exported from 3ds Max in the hamster's own space,
    /// so it lines up when spawned as a child of the hamster model.
    /// </summary>
    [CreateAssetMenu(menuName = "PotMerge/Costume Database", fileName = "CostumeDatabase")]
    public class CostumeDatabase : ScriptableObject
    {
        [Serializable]
        public class Costume
        {
            public string id;
            public string displayName;
            public CostumeSlot slot;
            public Currency currency;
            public int price;
            public GameObject model;
            public Texture2D icon;
        }

        public GameObject hamsterModel;
        public Costume[] costumes;

        public Costume Find(string id)
        {
            if (costumes == null || string.IsNullOrEmpty(id)) return null;
            foreach (var c in costumes) if (c.id == id) return c;
            return null;
        }
    }
}
