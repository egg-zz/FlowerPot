using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    /// <summary>
    /// The town: one island with the four shops around a plaza, bought decorations and served customers taking a walk.
    /// Built at runtime away from the board; takes over the camera while shown.
    /// </summary>
    public class TownScreen : MonoBehaviour
    {
        public TownDatabase town;
        public ChainDatabase database;
        public CostumeDatabase costumes;
        public Vector3 offset = new Vector3(200f, 0f, 0f);
        public float shopSize = 3.5f;
        [Tooltip("World units per meter of the decoration models")] public float propScale = 2f;
        public float plazaScale = 3f;
        public float guestHeight = 1.15f;

        static readonly Color Sky = new Color(0.80f, 0.91f, 0.99f);
        static readonly Color Grass = new Color(0.44f, 0.68f, 0.34f);
        static readonly Color Soil = new Color(0.72f, 0.52f, 0.38f);
        static readonly Color Path = new Color(0.84f, 0.70f, 0.52f);

        // flower and cafe at the back, bakery and ice cream in front
        static readonly Vector3[] ShopPos = { new Vector3(-3.7f, 0f, 5.4f), new Vector3(3.7f, 0f, 5.4f), new Vector3(-3.7f, 0f, -2.4f), new Vector3(3.7f, 0f, -2.4f) };
        static readonly Vector3 PlazaPos = new Vector3(0f, 0f, 1.2f);
        const float Fov = 36f, Pitch = 43f, IslandHalfWidth = 6.9f, GuestRing = 1.65f;

        Transform root, itemRoot, guestRoot;
        Camera cam;
        bool showing;
        Color savedBg;
        CameraClearFlags savedFlags;
        float savedFov;
        int focus = -1;
        Vector3 camTarget;
        float camDistance;
        string built = "";
        readonly List<ShopOwner> guests = new List<ShopOwner>();
        readonly Dictionary<string, Transform> placed = new Dictionary<string, Transform>();
        string popId;
        float popT = -1f;

        public bool Showing => showing;
        /// <summary>Sideways camera offset while moving to or from home.</summary>
        public float Pan { get; set; }

        public void Show(Camera camera)
        {
            Build();
            Refresh();
            if (!showing && camera != null)
            {
                savedBg = camera.backgroundColor;
                savedFlags = camera.clearFlags;
                savedFov = camera.fieldOfView;
            }
            cam = camera;
            showing = true;
            focus = -1;
            root.gameObject.SetActive(true);
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
            cam.fieldOfView = Fov;
            Framing(out camTarget, out camDistance);
            ApplyCamera();
        }

        public void Hide()
        {
            if (showing && cam != null)
            {
                cam.backgroundColor = savedBg;
                cam.clearFlags = savedFlags;
                cam.fieldOfView = savedFov;
            }
            showing = false;
            if (root != null) root.gameObject.SetActive(false);
        }

        /// <summary>-1 = whole town, 0-3 = a shop, 4 = the plaza.</summary>
        public void Focus(int shop) => focus = shop;

        // ------------------------------------------------------------------ camera
        void Framing(out Vector3 target, out float distance)
        {
            float aspect = cam != null ? cam.aspect : 0.5625f;
            float tan = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * Mathf.Min(aspect, 1f);
            if (focus < 0) { target = new Vector3(0f, 0f, 0.6f); distance = IslandHalfWidth / tan + 3f; }
            else if (focus < ShopPos.Length) { target = ShopPos[focus] + new Vector3(0f, 0.2f, -2.2f); distance = 3.5f / tan + 2f; }
            else { target = PlazaPos + new Vector3(0f, 0f, -1.4f); distance = 4.0f / tan + 2f; }
        }

        void ApplyCamera()
        {
            var rot = Quaternion.Euler(Pitch, 0f, 0f);
            cam.transform.rotation = rot;
            cam.transform.position = offset + Vector3.right * Pan + camTarget - rot * Vector3.forward * camDistance;
        }

        // ------------------------------------------------------------------ building
        void Build()
        {
            if (root != null) return;
            root = new GameObject("TownSet").transform;
            root.SetParent(transform, false);
            root.position = offset;

            Plate("Island", 15f, 18f, 0.8f, 5.5f, -0.4f, 0.8f, Grass);
            Plate("Soil", 14f, 17f, 0.9f, 5f, -1.15f, 0.8f, Soil);
            Plate("Street", 1.5f, 15f, 0.05f, 0.7f, 0.0f, 0.8f, Path);
            Plate("StreetBack", 9f, 1.4f, 0.05f, 0.65f, 0.0f, PlazaPos.z + 1.4f, Path);
            Plate("StreetFront", 9f, 1.4f, 0.05f, 0.65f, 0.0f, -5.3f, Path);
            if (town != null && town.plazaModel != null) Prop(town.plazaModel, root, PlazaPos + Vector3.up * 0.04f, 0f, plazaScale);

            for (int s = 0; s < ShopPos.Length; s++)
                Plate("Lot" + s, 4.4f, 4.4f, 0.06f, 1.2f, 0.0f, ShopPos[s].z, Color.Lerp(Grass, Color.white, 0.12f), ShopPos[s].x);

            itemRoot = new GameObject("Items").transform;
            itemRoot.SetParent(root, false);
            guestRoot = new GameObject("Guests").transform;
            guestRoot.SetParent(root, false);
        }

        void Plate(string name, float w, float d, float h, float corner, float y, float z, Color color, float x = 0f)
        {
            var go = RoundedMesh.Create(name, root, w, d, h, corner, Mathf.Min(0.3f, h * 0.4f), color);
            go.transform.localPosition = new Vector3(x, y, z);
        }

        /// <summary>FBX root keeps its axis rotation; the model is turned to face the camera like everywhere else.</summary>
        static Transform Prop(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale)
        {
            var holder = new GameObject(prefab.name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = pos;
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            holder.localScale = Vector3.one * scale;
            var model = Instantiate(prefab, holder, false);
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            return holder;
        }

        Vector3 ItemPosition(TownDatabase.Item item)
        {
            if (item.reward || item.shop >= ShopPos.Length) return new Vector3(item.position.x, 0.06f, item.position.y);
            var shop = ShopPos[item.shop];
            float inward = shop.x < 0f ? 1f : -1f;
            return shop + new Vector3(item.position.x * inward, 0.03f, item.position.y);
        }

        /// <summary>Rebuilds shops, decorations and walkers when something changed. <paramref name="newId"/> pops in.</summary>
        public void Refresh(string newId = null)
        {
            if (root == null || town == null) return;
            var key = new System.Text.StringBuilder();
            for (int s = 0; s < ShopPos.Length; s++) key.Append(PlayerData.ShopOpen(s) ? '1' : '0');
            foreach (var i in town.items) if (PlayerData.OwnsTownItem(i.id)) key.Append(i.id).Append(';');
            foreach (var g in PlayerData.TownGuests) key.Append(g).Append('|');
            if (key.ToString() == built) return;
            built = key.ToString();

            foreach (Transform t in itemRoot) Destroy(t.gameObject);
            placed.Clear();
            for (int s = 0; s < ShopPos.Length; s++)
            {
                if (!PlayerData.ShopOpen(s) || !database.IsValid(s, 11)) continue;
                var holder = new GameObject("Shop" + s).transform;
                holder.SetParent(itemRoot, false);
                holder.localPosition = ShopPos[s] + Vector3.up * 0.03f;
                ModelUtil.Spawn(database.Model(s, 11), holder, shopSize, shopSize);
            }
            foreach (var item in town.items)
                if (item.model != null && PlayerData.OwnsTownItem(item.id))
                    placed[item.id] = Prop(item.model, itemRoot, ItemPosition(item), item.yaw, propScale);
            if (newId != null && placed.ContainsKey(newId)) { popId = newId; popT = 0f; }

            BuildGuests();
        }

        void BuildGuests()
        {
            foreach (var g in guests) if (g != null) Destroy(g.gameObject);
            guests.Clear();
            if (costumes == null) return;
            foreach (var outfit in PlayerData.TownGuests)
            {
                var ids = outfit.Split(',');
                var go = new GameObject("Guest");
                go.transform.SetParent(guestRoot, false);
                var h = go.AddComponent<ShopOwner>();
                h.costumes = costumes;
                h.height = guestHeight;
                h.calm = true;
                h.walk = 0.6f;
                h.position = offset + PlazaPos;
                h.outfit = slot =>
                {
                    foreach (var id in ids) { var c = costumes.Find(id); if (c != null && c.slot == slot) return id; }
                    return "";
                };
                guests.Add(h);
            }
        }

        // ------------------------------------------------------------------ animation
        void LateUpdate()
        {
            if (!showing || cam == null) return;
            Framing(out var target, out float distance);
            float k = 1f - Mathf.Exp(-6f * Time.deltaTime);
            camTarget = Vector3.Lerp(camTarget, target, k);
            camDistance = Mathf.Lerp(camDistance, distance, k);
            ApplyCamera();

            // walkers circle the plaza, spread out
            for (int i = 0; i < guests.Count; i++)
            {
                float a = Time.time * 0.22f + i * 2f * Mathf.PI / guests.Count;
                var dir = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                guests[i].position = offset + PlazaPos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * GuestRing + Vector3.up * 0.05f;
                guests[i].yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 180f;
            }

            if (popT >= 0f && popId != null && placed.TryGetValue(popId, out var p) && p != null)
            {
                popT += Time.deltaTime / 0.5f;
                float t = Mathf.Clamp01(popT), u = t - 1f;
                p.localScale = Vector3.one * propScale * (u * u * (2.7f * u + 1.7f) + 1f);
                if (popT >= 1f) { p.localScale = Vector3.one * propScale; popT = -1f; }
            }
        }
    }
}
