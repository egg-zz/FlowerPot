using System;
using System.Collections.Generic;

namespace PotMerge
{
    /// <summary>
    /// Try-on state for the shop: costumes shown on the hamster without buying or equipping them.
    /// A preview overrides what is equipped in that slot until it is cleared (the shop clears it when it closes).
    /// </summary>
    public static class CostumePreview
    {
        static readonly Dictionary<CostumeSlot, string> slots = new Dictionary<CostumeSlot, string>();

        public static event Action Changed;

        /// <summary>Costume id shown in this slot: the preview if there is one, otherwise the equipped one.</summary>
        public static string Shown(CostumeSlot slot) =>
            slots.TryGetValue(slot, out var id) ? id : PlayerData.Equipped(slot);

        public static bool IsPreviewing(string id)
        {
            foreach (var v in slots.Values) if (v == id) return true;
            return false;
        }

        /// <summary>Tries a costume on, or takes the preview off again if it is already being tried.</summary>
        public static void Toggle(string id, CostumeSlot slot)
        {
            if (slots.TryGetValue(slot, out var cur) && cur == id) slots.Remove(slot);
            else slots[slot] = id;
            Changed?.Invoke();
        }

        public static void ClearSlot(CostumeSlot slot)
        {
            if (slots.Remove(slot)) Changed?.Invoke();
        }

        public static void Clear()
        {
            if (slots.Count == 0) return;
            slots.Clear();
            Changed?.Invoke();
        }

#if UNITY_EDITOR
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { slots.Clear(); Changed = null; }
#endif
    }
}
