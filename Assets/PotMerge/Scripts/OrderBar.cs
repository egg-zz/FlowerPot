using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    /// <summary>
    /// The little jazz bar at the top of order levels (15+): a long table hides the hamsters from the waist down, the owner
    /// stands behind it and customers waddle in from the right with an order, then walk out again.
    /// Customers wear costumes the player does not own yet (and coming-soon ones), so the bar also shows off the shop.
    /// Built on a hidden stage and rendered into a wide texture for the HUD.
    /// </summary>
    public class OrderBar : MonoBehaviour
    {
        public CostumeDatabase costumes;
        public MergeBoard board;
        public MergeHUD hud;
        public Vector3 stage = new Vector3(0f, -340f, 0f);
        public int maxPieces = 3;

        /// <summary>What the current customer wears, one costume per slot. Empty before the first customer of a level.</summary>
        public List<CostumeDatabase.Costume> Outfit { get; } = new List<CostumeDatabase.Costume>();

        const float OwnerX = -1.1f, CustomerX = 0.95f, OffscreenX = 6f, FloorY = 0.45f;

        ShopOwner owner, customer;
        Camera cam;
        RenderTexture rt;
        Transform root;
        int lastStep;
        readonly HashSet<string> lastIds = new HashSet<string>();

        void Start()
        {
            if (costumes == null || costumes.hamsterModel == null || board == null || hud == null) { enabled = false; return; }
            BuildStage();
            owner = AddHamster("BarOwner", OwnerX, 2.2f);
            customer = AddHamster("BarCustomer", OffscreenX, 1.95f);
            customer.outfit = WornIn;

            rt = new RenderTexture(1040, 420, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "OrderBar" };
            cam = new GameObject("BarCamera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.24f, 0.13f, 0.22f);
            cam.fieldOfView = 20f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 30f;
            cam.transform.position = stage + new Vector3(0f, 3.15f, -11.2f);
            cam.transform.LookAt(stage + new Vector3(0f, 2.05f, 0f));
            cam.targetTexture = rt;
            cam.depth = -11f;
            cam.enabled = false;
            hud.SetBarTexture(rt);

            board.OrdersReset += OnReset;
            board.CustomerArriving += OnArriving;
            board.OrderItemDelivered += OnDelivered;
            board.OrderRefused += OnRefused;
            board.CustomerLost += OnLost;
            board.Merged += OnMerged;
            board.Unlocked += OnUnlocked;
        }

        void OnDestroy()
        {
            if (board != null)
            {
                board.OrdersReset -= OnReset;
                board.CustomerArriving -= OnArriving;
                board.OrderItemDelivered -= OnDelivered;
                board.OrderRefused -= OnRefused;
                board.CustomerLost -= OnLost;
                board.Merged -= OnMerged;
                board.Unlocked -= OnUnlocked;
            }
            if (rt != null) rt.Release();
        }

        ShopOwner AddHamster(string name, float x, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var h = go.AddComponent<ShopOwner>();
            h.costumes = costumes;
            h.position = stage + new Vector3(x, FloorY, 0f);
            h.yaw = 0f;
            h.height = height;
            h.calm = true;
            return h;
        }

        // ------------------------------------------------------------------ reactions
        void OnReset() { Outfit.Clear(); customer?.Redress(); }

        void OnArriving()
        {
            PickOutfit();
            customer?.Redress();
            SoundFx.Bell();
        }

        void OnDelivered(bool orderDone)
        {
            owner?.Cheer(8);
            if (orderDone) PlayerData.AddTownGuest(Outfit.ConvertAll(c => c.id));
            if (orderDone) customer?.Cheer(8);
            else customer?.Smile(1.6f);
        }

        void OnRefused() => customer?.Refuse();
        void OnLost() => customer?.Refuse();
        void OnMerged(int chain, int newLevel) { if (board.HasOrders) owner?.Smile(); }
        void OnUnlocked() { if (board.HasOrders) owner?.Cheer(8); }

        // ------------------------------------------------------------------ outfit
        string WornIn(CostumeSlot slot)
        {
            foreach (var c in Outfit) if (c.slot == slot) return c.id;
            return "";
        }

        /// <summary>Up to <see cref="maxPieces"/> costumes in different slots: unowned and coming-soon ones first, not the last customer's.</summary>
        void PickOutfit()
        {
            Outfit.Clear();
            var pool = new List<CostumeDatabase.Costume>();
            foreach (var c in costumes.costumes)
                if (c.model != null && (c.comingSoon || !PlayerData.OwnsCostume(c.id))) pool.Add(c);
            if (pool.Count == 0)
                foreach (var c in costumes.costumes) if (c.model != null) pool.Add(c);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            for (int pass = 0; pass < 2 && Outfit.Count == 0; pass++)
                foreach (var c in pool)
                {
                    if (Outfit.Count >= maxPieces) break;
                    if (pass == 0 && lastIds.Contains(c.id)) continue;
                    if (WornIn(c.slot) == "") Outfit.Add(c);
                }
            lastIds.Clear();
            foreach (var c in Outfit) lastIds.Add(c.id);
        }

        // ------------------------------------------------------------------ walking
        void Update()
        {
            bool on = board.Active && board.HasOrders;
            cam.enabled = on;
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
            if (!on) return;

            float p = board.CustomerPresence;
            int dir = board.CustomerMoving;
            customer.position = stage + new Vector3(Mathf.Lerp(OffscreenX, CustomerX, p), FloorY, 0f);
            customer.walk = Mathf.MoveTowards(customer.walk, dir != 0 ? 1f : 0f, Time.deltaTime * 5f);
            float face = dir > 0 ? 62f : dir < 0 ? -62f : 0f;
            customer.yaw = Mathf.MoveTowards(customer.yaw, face, Time.deltaTime * 320f);
            if (dir != 0)
            {
                int step = Mathf.FloorToInt(Time.time * ShopOwner.StepRate / Mathf.PI);
                if (step != lastStep) { lastStep = step; SoundFx.Step(); }
            }
        }

        // ------------------------------------------------------------------ stage
        void BuildStage()
        {
            root = new GameObject("BarStage").transform;
            root.SetParent(transform, false);
            root.position = stage;

            var wall = new Color(0.36f, 0.19f, 0.31f);
            var wood = new Color(0.45f, 0.25f, 0.16f);
            var woodLight = new Color(0.68f, 0.40f, 0.24f);
            var brass = new Color(0.95f, 0.74f, 0.32f);

            Prim(PrimitiveType.Cube, new Vector3(0f, 2f, 1.9f), new Vector3(18f, 8f, 0.2f), wall);
            for (int i = -6; i <= 6; i++) // soft vertical panels
                Prim(PrimitiveType.Cube, new Vector3(i * 1.1f, 2f, 1.78f), new Vector3(0.07f, 8f, 0.06f), Color.Lerp(wall, Color.black, 0.25f));

            // shelves with bottles and glasses
            Color[] bottle = { new Color(0.95f, 0.55f, 0.62f), new Color(0.55f, 0.80f, 0.72f), new Color(0.98f, 0.80f, 0.42f), new Color(0.62f, 0.62f, 0.92f), new Color(0.93f, 0.90f, 0.82f) };
            var rng = new System.Random(7);
            foreach (float y in new[] { 1.75f, 2.75f })
            {
                Prim(PrimitiveType.Cube, new Vector3(0f, y, 1.5f), new Vector3(9.4f, 0.09f, 0.5f), wood);
                for (float x = -4.4f; x <= 4.4f; x += 0.42f)
                {
                    if (rng.NextDouble() < 0.22) continue;
                    float h = 0.22f + 0.12f * (float)rng.NextDouble();
                    var c = bottle[rng.Next(bottle.Length)];
                    Prim(PrimitiveType.Capsule, new Vector3(x, y + 0.04f + h * 0.85f, 1.5f), new Vector3(0.22f, h * 0.85f, 0.22f), c);
                    Prim(PrimitiveType.Cylinder, new Vector3(x, y + 0.04f + h * 1.75f, 1.5f), new Vector3(0.08f, 0.1f, 0.08f), Color.Lerp(c, Color.white, 0.4f));
                }
            }

            // pendant lamps, warm light
            foreach (float x in new[] { -2.9f, 0f, 2.9f })
            {
                Prim(PrimitiveType.Cylinder, new Vector3(x, 4.2f, 0.5f), new Vector3(0.03f, 0.6f, 0.03f), brass);
                Prim(PrimitiveType.Sphere, new Vector3(x, 3.6f, 0.5f), new Vector3(0.62f, 0.36f, 0.62f), brass);
                Prim(PrimitiveType.Sphere, new Vector3(x, 3.46f, 0.45f), new Vector3(0.3f, 0.26f, 0.3f), new Color(1f, 0.95f, 0.72f));
                var l = new GameObject("BarLight").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.localPosition = new Vector3(x, 3.1f, -0.4f);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.78f, 0.5f);
                l.range = 4.5f;
                l.intensity = 0.3f;
            }

            // a neon music note
            var neon = new Color(1f, 0.62f, 0.78f);
            Prim(PrimitiveType.Sphere, new Vector3(-0.28f, 3.25f, 1.6f), new Vector3(0.24f, 0.18f, 0.1f), neon);
            Prim(PrimitiveType.Sphere, new Vector3(0.2f, 3.33f, 1.6f), new Vector3(0.24f, 0.18f, 0.1f), neon);
            Prim(PrimitiveType.Cube, new Vector3(-0.18f, 3.5f, 1.6f), new Vector3(0.05f, 0.5f, 0.05f), neon);
            Prim(PrimitiveType.Cube, new Vector3(0.3f, 3.58f, 1.6f), new Vector3(0.05f, 0.5f, 0.05f), neon);

            // the long table in front: only the upper halves show
            Prim(PrimitiveType.Cube, new Vector3(0f, 0.2f, -1.45f), new Vector3(16f, 1.5f, 0.8f), wood);
            for (float x = -6f; x <= 6f; x += 1.5f)
                Prim(PrimitiveType.Cube, new Vector3(x, 0.4f, -1.87f), new Vector3(1.2f, 0.6f, 0.06f), Color.Lerp(wood, woodLight, 0.35f));
            Prim(PrimitiveType.Cube, new Vector3(0f, 0.84f, -1.95f), new Vector3(16f, 0.05f, 0.05f), brass);
            var top = RoundedMesh.Create("TableTop", root, 16f, 1.3f, 0.16f, 0.12f, 0.06f, woodLight);
            top.transform.localPosition = new Vector3(0f, 0.98f, -1.5f);
            root.gameObject.SetActive(false);
        }

        void Prim(PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var m = go.GetComponent<Renderer>().material;
            m.color = color;
            m.SetFloat("_Glossiness", 0.2f);
        }
    }
}
