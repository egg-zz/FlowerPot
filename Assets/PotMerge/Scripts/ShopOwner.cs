using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    /// <summary>
    /// The hamster shop owner standing in front of the flower shop (title/home backdrop).
    /// Wears the costumes equipped in <see cref="PlayerData"/>, bounces gently and blinks.
    /// </summary>
    public class ShopOwner : MonoBehaviour
    {
        public CostumeDatabase costumes;
        [Tooltip("World position of the hamster's feet")]
        public Vector3 position = new Vector3(82.6f, 0f, -3.4f);
        public float yaw = -12f;
        public float height = 2.4f;

        Transform holder, fit;
        GameObject model;
        readonly Dictionary<CostumeSlot, GameObject> worn = new Dictionary<CostumeSlot, GameObject>();
        readonly List<Transform> eyes = new List<Transform>();
        Vector3 eyeScale = Vector3.one;
        float blinkTimer = 2f, blinkT = -1f;
        bool dirty = true;

        void Start()
        {
            if (costumes == null || costumes.hamsterModel == null) { enabled = false; return; }
            holder = new GameObject("Hamster").transform;
            holder.SetParent(transform, false);
            holder.position = position;
            holder.rotation = Quaternion.Euler(0f, yaw, 0f);
            model = ModelUtil.Spawn(costumes.hamsterModel, holder, height * 0.75f, height);
            fit = model.transform.parent;
            foreach (var t in model.GetComponentsInChildren<Transform>())
                if (t.name.Contains("_Eye")) eyes.Add(t); // eyes and their highlights close together
            if (eyes.Count > 0) eyeScale = eyes[0].localScale;
            PlayerData.Changed += MarkDirty;
        }

        void OnDestroy() => PlayerData.Changed -= MarkDirty;

        void MarkDirty() => dirty = true;

        void RefreshCostumes()
        {
            dirty = false;
            foreach (CostumeSlot slot in System.Enum.GetValues(typeof(CostumeSlot)))
            {
                var c = costumes.Find(PlayerData.Equipped(slot));
                worn.TryGetValue(slot, out var current);
                if (c == null || c.model == null)
                {
                    if (current != null) Destroy(current);
                    worn.Remove(slot);
                    continue;
                }
                if (current != null && current.name == c.id) continue;
                if (current != null) Destroy(current);
                // costumes share the hamster's model space: copy the hamster model's local placement
                var go = Instantiate(c.model, fit, false);
                go.name = c.id;
                go.transform.localPosition = model.transform.localPosition;
                go.transform.localRotation = model.transform.localRotation;
                go.transform.localScale = model.transform.localScale;
                foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
                worn[slot] = go;
            }
        }

        void Update()
        {
            if (holder == null) return;
            if (dirty) RefreshCostumes();

            // gentle squash-and-stretch bounce
            float t = Time.time;
            float b = Mathf.Abs(Mathf.Sin(t * 2.4f));
            holder.localScale = new Vector3(1f + 0.03f * (1f - b), 1f + 0.045f * b - 0.02f, 1f + 0.03f * (1f - b));
            holder.position = position + Vector3.up * (0.08f * b);
            holder.rotation = Quaternion.Euler(0f, yaw + Mathf.Sin(t * 0.8f) * 6f, Mathf.Sin(t * 1.2f) * 2f);

            // blink every few seconds
            blinkTimer -= Time.deltaTime;
            if (blinkTimer <= 0f) { blinkT = 0f; blinkTimer = Random.Range(2.5f, 5f); }
            if (blinkT >= 0f)
            {
                blinkT += Time.deltaTime / 0.16f;
                float k = blinkT < 1f ? 1f - Mathf.Sin(blinkT * Mathf.PI) * 0.9f : 1f;
                foreach (var e in eyes) if (e != null) e.localScale = new Vector3(eyeScale.x, eyeScale.y, eyeScale.z * k);
                if (blinkT >= 1f) blinkT = -1f;
            }
        }
    }
}
