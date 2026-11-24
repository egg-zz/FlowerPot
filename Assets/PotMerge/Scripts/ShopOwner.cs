using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    /// <summary>
    /// The hamster shop owner standing in front of the flower shop (title/home backdrop).
    /// Wears the costumes equipped in <see cref="PlayerData"/> (or tried on in the shop, <see cref="CostumePreview"/>), bounces gently and blinks.
    /// </summary>
    public partial class ShopOwner : MonoBehaviour
    {
        public CostumeDatabase costumes;
        [Tooltip("World position of the hamster's feet")]
        public Vector3 position = new Vector3(82.6f, 0f, -3.4f);
        public float yaw = -12f;
        public float height = 2.4f;
        /// <summary>What to wear per slot (costume id). Null = the player outfit (equipped or tried on).</summary>
        public System.Func<CostumeSlot, string> outfit;

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
            CostumePreview.Changed += MarkDirty;
        }

        void OnDestroy()
        {
            PlayerData.Changed -= MarkDirty;
            CostumePreview.Changed -= MarkDirty;
        }

        void MarkDirty() => dirty = true;

        /// <summary>Puts on what <see cref="outfit"/> says now.</summary>
        public void Redress() => dirty = true;

        void RefreshCostumes()
        {
            dirty = false;
            foreach (CostumeSlot slot in System.Enum.GetValues(typeof(CostumeSlot)))
            {
                var c = costumes.Find(outfit != null ? outfit(slot) : CostumePreview.Shown(slot));
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

        float cheerT = -1f, cheerPower = 1f;
        float smileT;

        public const float StepRate = 9f;
        /// <summary>0..1: waddle of a walking hamster.</summary>
        public float walk;

        [Tooltip("Stand almost still (slow breathing). Used in the game screen so the hamster does not distract from the board")]
        public bool calm;

        /// <summary>Smiles and sways gently from side to side for a moment.</summary>
        public void Smile(float seconds = 1.2f) => smileT = Mathf.Max(smileT, seconds);

        float refuseT = -1f;

        /// <summary>Shakes the head: not what was ordered.</summary>
        public void Refuse() => refuseT = 0f;

        /// <summary>Big happy jump with a spin and a smile: a new merge stage or a delivery.</summary>
        public void Cheer(int level = 1)
        {
            cheerT = 0f;
            Smile(2f);
            cheerPower = Mathf.Lerp(0.8f, 1.6f, Mathf.Clamp01((level - 2) / 8f));
        }

        void Update()
        {
            if (holder == null) return;
            if (dirty) RefreshCostumes();

            // gentle squash-and-stretch bounce
            float t = Time.time;
            float b = calm ? 0.5f + 0.5f * Mathf.Sin(t * 1.6f) : Mathf.Abs(Mathf.Sin(t * 2.4f));
            Vector3 scale = new Vector3(1f + 0.03f * (1f - b), 1f + 0.045f * b - 0.02f, 1f + 0.03f * (1f - b));
            float lift = calm ? 0f : 0.08f * b;
            if (calm) scale = Vector3.Lerp(Vector3.one, scale, 0.4f);
            float spin = 0f;

            if (cheerT >= 0f)
            {
                cheerT += Time.deltaTime / 0.7f;
                float k = Mathf.Clamp01(cheerT);
                lift += Mathf.Sin(k * Mathf.PI) * (calm ? 0.5f : 0.9f) * cheerPower;      // jump
                spin = Mathf.SmoothStep(0f, 360f, k);                       // full turn in the air
                float squash = k < 0.12f ? Mathf.Sin(k / 0.12f * Mathf.PI) : (k > 0.88f ? Mathf.Sin((k - 0.88f) / 0.12f * Mathf.PI) : 0f);
                scale = Vector3.Scale(scale, new Vector3(1f + 0.18f * squash, 1f - 0.2f * squash, 1f + 0.18f * squash));
                if (cheerT >= 1f) cheerT = -1f;
            }


            if (refuseT >= 0f)
            {
                refuseT += Time.deltaTime / 0.5f;
                float k = Mathf.Clamp01(refuseT);
                spin += Mathf.Sin(k * Mathf.PI * 4f) * 16f * (1f - k);
                if (refuseT >= 1f) refuseT = -1f;
            }

            float step = t * StepRate;
            lift += walk * 0.12f * Mathf.Abs(Mathf.Sin(step));
            float waddle = walk * 9f * Mathf.Sin(step);
            UpdateSmile();

            float sway = calm ? 0.3f : 1f;
            holder.localScale = scale;
            holder.position = position + Vector3.up * lift;
            holder.rotation = Quaternion.Euler(0f, yaw + spin + Mathf.Sin(t * 0.8f) * 6f * sway, Mathf.Sin(t * 1.2f) * 2f * sway + waddle + swing * 7f * Mathf.Sin(t * 6f));

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
