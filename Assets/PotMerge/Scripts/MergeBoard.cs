using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    /// <summary>
    /// Core of the flower merge game.
    /// Rules:
    ///  - Merging two level-N items gives one level-(N+1) item and +N coins.
    ///  - Start: 20 coins, 7 random items (level 1-4).
    ///  - When no merge is possible, 1 coin buys a new random item (level 1-4).
    ///  - No merge possible and (no coins or board full) = game over.
    /// </summary>
    public class MergeBoard : MonoBehaviour
    {
        [Header("Stages (index 0 = level 1)")]
        public GameObject[] potModels;
        public string[] levelNames =
        {
            "Seed", "Empty Pot", "Sprout", "Leafy Plant", "Bud", "Bloom",
            "Triple Bloom", "Gift Flower", "Bouquet", "Flower Stall", "Flower Shop"
        };
        [Tooltip("Extra per-level size multiplier (index 0 = level 1). Missing entries = 1.")]
        public float[] levelScale = { 1.5f, 1f, 1f, 1f, 1f, 1f, 0.9f, 1.15f, 1.2f, 1.0f, 1.05f };
        public bool turnModelsToCamera = true;

        [Header("Board")]
        public int columns = 7;
        public int rows = 7;
        public float cellSize = 1.7f;
        public float potScale = 1.3f;
        public Color tileA = new Color(0.47f, 0.70f, 0.30f);
        public Color tileB = new Color(0.40f, 0.62f, 0.25f);
        public Color boardColor = new Color(0.45f, 0.29f, 0.18f);

        [Header("Rules")]
        public long startCoins = 20;
        public int startingItems = 7;
        public int spawnMinLevel = 1;
        public int spawnMaxLevel = 4;
        public int spawnCost = 1;
        public float hintDelay = 6f;

        [Header("Refs")]
        public Camera cam;
        public MergeHUD hud;

        const string SaveKey = "PotMerge.Save.v2";

        PotItem[,] grid;
        long coins;
        int bestLevel = 1;
        bool gameOver;
        float idleTimer;
        float saveTimer;

        PotItem dragging;
        Vector3 dragOffset;
        Transform potsRoot;
        ParticleSystem burst;
        float lastAspect = -1f;
        Transform highlight;
        int busyMerges;

        int MaxLevel => potModels != null ? potModels.Length : 0;

        // ------------------------------------------------------------------ lifecycle
        void Start()
        {
            if (cam == null) cam = Camera.main;
            grid = new PotItem[columns, rows];
            BuildBoard();
            BuildBurst();
            potsRoot = new GameObject("Pots").transform;
            potsRoot.SetParent(transform, false);

            if (hud != null)
            {
                hud.SpawnClicked += TryBuySpawn;
                hud.RestartClicked += Restart;
            }

            if (!Load()) NewGame();
            RefreshState();
        }

        void OnDestroy()
        {
            if (hud != null)
            {
                hud.SpawnClicked -= TryBuySpawn;
                hud.RestartClicked -= Restart;
            }
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() => Save();

        void Update()
        {
            if (grid == null) return; // e.g. after a script reload in play mode
            FitCamera();
            if (!gameOver) HandleInput();
            TickHint();

            saveTimer += Time.deltaTime;
            if (saveTimer > 5f) { saveTimer = 0f; Save(); }
        }

        void NewGame()
        {
            coins = startCoins;
            bestLevel = 1;
            gameOver = false;
            for (int i = 0; i < startingItems; i++)
            {
                int lv = RandomSpawnLevel();
                SpawnRandom(lv, 0.08f * i);
                bestLevel = Mathf.Max(bestLevel, lv);
            }
        }

        void Restart()
        {
            StopAllCoroutines();
            busyMerges = 0;
            dragging = null;
            highlight.gameObject.SetActive(false);
            foreach (Transform t in potsRoot) Destroy(t.gameObject);
            grid = new PotItem[columns, rows];
            hud?.ShowGameOver(false, "");
            NewGame();
            RefreshState();
            Save();
        }

        // ------------------------------------------------------------------ board building
        void BuildBoard()
        {
            var boardRoot = new GameObject("BoardVisual").transform;
            boardRoot.SetParent(transform, false);

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            Destroy(slab.GetComponent<Collider>());
            slab.transform.SetParent(boardRoot, false);
            slab.transform.localScale = new Vector3(columns * cellSize + 0.5f, 0.5f, rows * cellSize + 0.5f);
            slab.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            SetColor(slab, boardColor);

            for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Tile_{x}_{y}";
                Destroy(tile.GetComponent<Collider>());
                tile.transform.SetParent(boardRoot, false);
                tile.transform.localScale = new Vector3(cellSize * 0.92f, 0.12f, cellSize * 0.92f);
                tile.transform.position = CellToWorld(new Vector2Int(x, y)) + Vector3.down * 0.06f;
                SetColor(tile, (x + y) % 2 == 0 ? tileA : tileB);
            }

            var hl = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hl.name = "DropHighlight";
            Destroy(hl.GetComponent<Collider>());
            hl.transform.SetParent(boardRoot, false);
            hl.transform.localScale = new Vector3(cellSize * 0.92f, 0.14f, cellSize * 0.92f);
            SetColor(hl, new Color(1f, 0.95f, 0.55f));
            hl.SetActive(false);
            highlight = hl.transform;
        }

        void BuildBurst()
        {
            var go = new GameObject("MergeBurst");
            go.transform.SetParent(transform, false);
            burst = go.AddComponent<ParticleSystem>();
            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = burst.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            var emission = burst.emission;
            emission.rateOverTime = 0f;
            var shape = burst.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var sizeOverLife = burst.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = new Material(Shader.Find("Sprites/Default"));
        }

        static void SetColor(GameObject go, Color c) => go.GetComponent<Renderer>().material.color = c;

        // ------------------------------------------------------------------ coordinates
        Vector3 CellToWorld(Vector2Int c)
        {
            float x = (c.x - (columns - 1) * 0.5f) * cellSize;
            float z = (c.y - (rows - 1) * 0.5f) * cellSize;
            return transform.position + new Vector3(x, 0f, z);
        }

        bool WorldToCell(Vector3 w, out Vector2Int cell)
        {
            Vector3 local = w - transform.position;
            int x = Mathf.RoundToInt(local.x / cellSize + (columns - 1) * 0.5f);
            int y = Mathf.RoundToInt(local.z / cellSize + (rows - 1) * 0.5f);
            cell = new Vector2Int(x, y);
            return x >= 0 && y >= 0 && x < columns && y < rows;
        }

        bool ScreenToGround(Vector2 screen, out Vector3 point)
        {
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, transform.position);
            if (plane.Raycast(ray, out float d)) { point = ray.GetPoint(d); return true; }
            point = Vector3.zero;
            return false;
        }

        void FitCamera()
        {
            if (cam == null || Mathf.Approximately(cam.aspect, lastAspect)) return;
            lastAspect = cam.aspect;
            const float pitch = 60f;
            float boardW = columns * cellSize + 1.6f;
            float boardD = rows * cellSize + 0.6f;
            float vHalf = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * cam.aspect);
            float distW = boardW * 0.5f / Mathf.Tan(hHalf);
            float distD = (boardD * Mathf.Sin(pitch * Mathf.Deg2Rad) * 0.5f + 1.2f) / (Mathf.Tan(vHalf) * 0.68f);
            float dist = Mathf.Max(distW, distD);
            var rot = Quaternion.Euler(pitch, 0f, 0f);
            Vector3 target = transform.position + new Vector3(0f, 0.5f, 0.3f);
            cam.transform.rotation = rot;
            cam.transform.position = target - rot * Vector3.forward * dist;
        }

        // ------------------------------------------------------------------ input
        void HandleInput()
        {
            if (cam == null) return;
            Vector2 mouse = Input.mousePosition;

            if (Input.GetMouseButtonDown(0) && dragging == null)
            {
                idleTimer = 0f;
                if (hud != null && hud.IsPointerOverUI(mouse)) return;
                var pot = PickPot(mouse);
                if (pot != null && ScreenToGround(mouse, out var g))
                {
                    dragging = pot;
                    dragOffset = pot.transform.position - g;
                    dragOffset.y = 0f;
                    pot.SetLifted(true);
                }
            }

            if (dragging != null && Input.GetMouseButton(0))
            {
                idleTimer = 0f;
                if (ScreenToGround(mouse, out var g))
                {
                    Vector3 target = g + dragOffset + Vector3.up * 0.6f;
                    dragging.transform.position = Vector3.Lerp(dragging.transform.position, target, 1f - Mathf.Exp(-25f * Time.deltaTime));
                }
                UpdateHighlight();
            }

            if (dragging != null && Input.GetMouseButtonUp(0))
            {
                var pot = dragging;
                dragging = null;
                highlight.gameObject.SetActive(false);
                Drop(pot);
            }
        }

        PotItem PickPot(Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 500f);
            PotItem best = null;
            float bestD = float.MaxValue;
            foreach (var h in hits)
            {
                var p = h.collider.GetComponentInParent<PotItem>();
                if (p != null && h.distance < bestD) { best = p; bestD = h.distance; }
            }
            if (best != null) return best;
            if (ScreenToGround(screen, out var g) && WorldToCell(g, out var c)) return grid[c.x, c.y];
            return null;
        }

        void UpdateHighlight()
        {
            Vector3 p = dragging.transform.position;
            if (WorldToCell(p, out var c) && c != dragging.Cell)
            {
                var other = grid[c.x, c.y];
                bool canMerge = other != null && other.Level == dragging.Level && dragging.Level < MaxLevel;
                highlight.gameObject.SetActive(true);
                highlight.position = CellToWorld(c) + Vector3.down * 0.04f;
                highlight.GetComponent<Renderer>().material.color = canMerge ? new Color(1f, 0.9f, 0.3f) : new Color(0.92f, 0.92f, 0.92f);
            }
            else highlight.gameObject.SetActive(false);
        }

        void Drop(PotItem pot)
        {
            pot.SetLifted(false);
            Vector2Int from = pot.Cell;

            if (!WorldToCell(pot.transform.position, out var to) || to == from)
            {
                pot.MoveTo(CellToWorld(from));
                return;
            }

            var other = grid[to.x, to.y];
            if (other == null)
            {
                grid[from.x, from.y] = null;
                Place(pot, to);
                pot.MoveTo(CellToWorld(to), 0.12f, pot.PlayPunch);
            }
            else if (other.Level == pot.Level && pot.Level < MaxLevel)
            {
                Merge(pot, other, to);
                return;
            }
            else if (other.Level == pot.Level)
            {
                pot.MoveTo(CellToWorld(from));
                other.PlayShake();
                hud?.ShowToast("MAX LEVEL!", new Color(1f, 0.85f, 0.3f));
            }
            else
            {
                Place(other, from);
                Place(pot, to);
                other.MoveTo(CellToWorld(from), 0.18f, other.PlayPunch);
                pot.MoveTo(CellToWorld(to), 0.12f, pot.PlayPunch);
            }
            RefreshState();
            Save();
        }

        void Place(PotItem pot, Vector2Int cell)
        {
            grid[cell.x, cell.y] = pot;
            pot.Cell = cell;
        }

        // ------------------------------------------------------------------ merge / spawn
        void Merge(PotItem a, PotItem b, Vector2Int cell)
        {
            int fromLevel = a.Level;
            grid[a.Cell.x, a.Cell.y] = null;
            grid[b.Cell.x, b.Cell.y] = null;
            Vector3 pos = CellToWorld(cell);

            // reserve the cell so nothing spawns into it during the merge animation
            busyMerges++;
            a.MoveTo(pos, 0.08f);
            StartCoroutine(FinishMerge(a, b, cell, fromLevel, pos));
        }

        IEnumerator FinishMerge(PotItem a, PotItem b, Vector2Int cell, int fromLevel, Vector3 pos)
        {
            yield return new WaitForSeconds(0.08f);
            Destroy(a.gameObject);
            Destroy(b.gameObject);

            int newLevel = fromLevel + 1;
            var pot = CreatePot(newLevel, cell);
            pot.transform.position = pos;
            pot.PlaySpawn();
            EmitBurst(pos + Vector3.up * 0.6f, newLevel);

            long reward = fromLevel; // +1 for level-1 merges, +2 for level-2, ...
            coins += reward;
            hud?.FloatText(pos + Vector3.up * 1.4f, "+" + MergeHUD.Format(reward), new Color(1f, 0.85f, 0.25f), 56f);

            if (newLevel > bestLevel)
            {
                bestLevel = newLevel;
                hud?.ShowToast("NEW! " + LevelName(newLevel), Color.white);
                EmitBurst(pos + Vector3.up * 1.2f, newLevel, 90);
            }
            busyMerges--;
            RefreshState(true);
            Save();
        }

        void TryBuySpawn()
        {
            idleTimer = 0f;
            if (gameOver || busyMerges > 0) return;
            if (HasAnyMerge(out _, out _)) { hud?.ShowToast("Merge first!", new Color(1f, 0.75f, 0.5f)); ShowHint(); return; }
            if (FreeCellCount() == 0) { hud?.ShowToast("Board full!", new Color(1f, 0.6f, 0.5f)); return; }
            if (coins < spawnCost) { hud?.ShowToast("Not enough coins", new Color(1f, 0.6f, 0.5f)); return; }
            coins -= spawnCost;
            int lv = RandomSpawnLevel();
            SpawnRandom(lv, 0f);
            if (lv > bestLevel) bestLevel = lv;
            hud?.SetCoins(coins, true);
            RefreshState();
            Save();
        }

        int RandomSpawnLevel() => UnityEngine.Random.Range(spawnMinLevel, Mathf.Min(spawnMaxLevel, MaxLevel) + 1);

        void SpawnRandom(int level, float delay)
        {
            var empty = new List<Vector2Int>();
            for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
                if (grid[x, y] == null) empty.Add(new Vector2Int(x, y));
            if (empty.Count == 0) return;

            var cell = empty[UnityEngine.Random.Range(0, empty.Count)];
            var pot = CreatePot(level, cell);
            Vector3 target = CellToWorld(cell);
            pot.transform.position = target + Vector3.up * 2.5f;
            pot.PlaySpawn(delay);
            StartCoroutine(DelayedDrop(pot, target, delay));
        }

        IEnumerator DelayedDrop(PotItem pot, Vector3 target, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (pot != null && !pot.IsDragging) pot.MoveTo(target, 0.25f, pot.PlayPunch);
        }

        PotItem CreatePot(int level, Vector2Int cell)
        {
            var go = new GameObject($"Item_L{level}");
            go.transform.SetParent(potsRoot, false);
            var item = go.AddComponent<PotItem>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            // keep the model's own root rotation (FBX axis conversion from 3ds Max Z-up)
            var model = Instantiate(potModels[level - 1], visual, false);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            // 3ds Max front (-Y) arrives facing +Z; turn it toward the camera
            if (turnModelsToCamera) model.transform.Rotate(0f, 180f, 0f, Space.World);

            // ground the model and center it on the cell
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                Vector3 root = go.transform.position;
                model.transform.position -= new Vector3(b.center.x - root.x, b.min.y - root.y, b.center.z - root.z);
            }

            float scale = potScale * (levelScale != null && level - 1 < levelScale.Length ? levelScale[level - 1] : 1f);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f * potScale, 0f);
            col.size = new Vector3(cellSize * 0.85f, 1.0f * potScale, cellSize * 0.85f);

            item.Init(level, visual, scale);
            Place(item, cell);
            return item;
        }

        void EmitBurst(Vector3 pos, int level, int count = 45)
        {
            if (burst == null) return;
            Color[] palette =
            {
                new Color(0.55f, 0.85f, 0.35f), new Color(1f, 0.85f, 0.25f), new Color(0.4f, 0.85f, 0.9f),
                new Color(1f, 0.5f, 0.7f), new Color(0.7f, 0.55f, 1f), Color.white
            };
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < count; i++)
            {
                ep.position = pos;
                ep.startColor = palette[(i + level) % palette.Length];
                burst.Emit(ep, 1);
            }
        }

        // ------------------------------------------------------------------ state / hints
        bool HasAnyMerge(out PotItem a, out PotItem b)
        {
            var seen = new Dictionary<int, PotItem>();
            foreach (var p in grid)
            {
                if (p == null || p.Level >= MaxLevel) continue;
                if (seen.TryGetValue(p.Level, out var other)) { a = other; b = p; return true; }
                seen[p.Level] = p;
            }
            a = b = null;
            return false;
        }

        int FreeCellCount()
        {
            int n = 0;
            foreach (var p in grid) if (p == null) n++;
            return n;
        }

        void RefreshState(bool punch = false)
        {
            if (hud == null) return;
            hud.SetCoins(coins, punch);
            hud.SetBest(bestLevel, MaxLevel, LevelName(bestLevel));

            if (busyMerges > 0) return;
            bool canMerge = HasAnyMerge(out _, out _);
            bool full = FreeCellCount() == 0;
            if (canMerge)
                hud.SetSpawn("MERGE!", "drag matching items together", false);
            else if (full)
                hud.SetSpawn("BOARD FULL", "no moves left", false);
            else
                hud.SetSpawn("ADD ITEM", spawnCost + (spawnCost == 1 ? " coin" : " coins"), coins >= spawnCost);

            bool over = !canMerge && (full || coins < spawnCost);
            if (over && !gameOver)
            {
                gameOver = true;
                hud.ShowGameOver(true, $"Best: {LevelName(bestLevel)} ({bestLevel}/{MaxLevel})\nCoins left: {coins}");
            }
        }

        void TickHint()
        {
            if (gameOver || dragging != null) return;
            idleTimer += Time.deltaTime;
            if (idleTimer >= hintDelay)
            {
                idleTimer = 0f;
                ShowHint();
            }
        }

        void ShowHint()
        {
            if (HasAnyMerge(out var a, out var b))
            {
                a.PlayShake();
                b.PlayShake();
            }
        }

        string LevelName(int level) =>
            levelNames != null && level - 1 < levelNames.Length ? levelNames[level - 1] : "Level " + level;

        // ------------------------------------------------------------------ save / load
        [Serializable]
        class SaveData
        {
            public int[] cells;
            public long coins;
            public int bestLevel;
        }

        void Save()
        {
            if (grid == null) return;
            if (gameOver) { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); return; }
            var data = new SaveData { cells = new int[columns * rows], coins = coins, bestLevel = bestLevel };
            for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
                data.cells[y * columns + x] = grid[x, y] != null ? grid[x, y].Level : 0;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        bool Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return false;
            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            if (data == null || data.cells == null || data.cells.Length != columns * rows) return false;
            coins = data.coins;
            bestLevel = Mathf.Max(1, data.bestLevel);
            float d = 0f;
            int count = 0;
            for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
            {
                int lv = data.cells[y * columns + x];
                if (lv <= 0 || lv > MaxLevel) continue;
                var p = CreatePot(lv, new Vector2Int(x, y));
                p.transform.position = CellToWorld(new Vector2Int(x, y));
                p.PlaySpawn(d);
                d += 0.02f;
                count++;
            }
            return count > 0;
        }

        [ContextMenu("Reset Save")]
        public void ResetSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            Debug.Log("[PotMerge] Save reset.");
        }
    }
}
