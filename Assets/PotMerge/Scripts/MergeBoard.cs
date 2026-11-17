using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    /// <summary>
    /// Core of the merge game (4 chains x 11 levels on a 7x7 board).
    /// Rules:
    ///  - Only the same chain + same level merge. Dropping onto any other item swaps them.
    ///  - Merging two level-N items gives one level-(N+1) item and +N coins.
    ///  - New game: at least 20 coins, 7 random items (any chain, level 1-2).
    ///  - 1 coin adds a random item (any chain) as long as there is an empty cell:
    ///    level 1-2 at first; making a level 4 opens 1-3, making a level 5 opens 1-4 (per game).
    ///  - No merge possible and (no coins or board full) = game over.
    /// Coins, unlocks and quests live in <see cref="PlayerData"/>; this class saves only the board.
    /// </summary>
    public class MergeBoard : MonoBehaviour
    {
        [Header("Content")]
        public ChainDatabase database;
        [Tooltip("Max footprint (x/z) of an item, relative to the cell size")]
        public float itemFootprint = 0.8f;
        [Tooltip("Max height of an item in world units")]
        public float itemHeight = 2.1f;
        public bool turnModelsToCamera = true;

        [Header("Board")]
        public int columns = 7;
        public int rows = 7;
        public float cellSize = 1.7f;
        public Color tileA = new Color(0.47f, 0.70f, 0.30f);
        public Color tileB = new Color(0.40f, 0.62f, 0.25f);
        public Color boardColor = new Color(0.45f, 0.29f, 0.18f);

        [Header("Rules")]
        public int startingItems = 7;
        public int spawnMinLevel = 1;
        [Tooltip("Max spawn level at the start of a game (starting items and ADD ITEM)")]
        public int earlySpawnMaxLevel = 2;
        [Tooltip("Spawn range grows with this game's best item: making level N allows spawns up to N-1, capped here")]
        public int spawnMaxLevel = 4;
        public int spawnCost = 1;
        public float hintDelay = 6f;

        [Header("Refs")]
        public Camera cam;
        public MergeHUD hud;

        const string SaveKey = "PotMerge.Board.v3";

        PotItem[,] grid;
        int bestChain;
        int bestLevel = 1;
        bool gameOver;
        float idleTimer;
        float saveTimer;

        PotItem dragging;
        Vector3 dragOffset;
        Transform itemsRoot;
        ParticleSystem burst;
        float lastAspect = -1f;
        Transform highlight;
        int busyMerges;

        int MaxLevel => database != null ? database.MaxLevel : 0;
        int ChainCount => database != null ? Mathf.Min(database.ChainCount, PlayerData.ChainCount) : 0;
        static long Coins => PlayerData.Coins;

        // ------------------------------------------------------------------ lifecycle
        void Start()
        {
            if (cam == null) cam = Camera.main;
            if (database == null || ChainCount == 0)
            {
                Debug.LogError("[PotMerge] MergeBoard has no ChainDatabase assigned.");
                enabled = false;
                return;
            }
            grid = new PotItem[columns, rows];
            BuildBoard();
            BuildBurst();
            itemsRoot = new GameObject("Items").transform;
            itemsRoot.SetParent(transform, false);

            if (hud != null)
            {
                hud.SpawnClicked += TryBuySpawn;
                hud.RestartClicked += Restart;
            }
            PlayerData.Changed += OnPlayerDataChanged;

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
            PlayerData.Changed -= OnPlayerDataChanged;
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() => Save();

        void OnPlayerDataChanged()
        {
            if (hud != null) hud.SetCoins(Coins);
        }

        bool active = true;

        /// <summary>False while the title/home screen is shown: no camera control, input or hints.</summary>
        public bool Active
        {
            get => active;
            set
            {
                active = value;
                if (value) { lastAspect = -1f; idleTimer = 0f; FitCamera(); }
                else CancelDrag();
            }
        }

        public bool IsGameOver => gameOver;

        /// <summary>True while a popup (settings, ...) covers the game.</summary>
        public bool InputBlocked { get; set; }

        void CancelDrag()
        {
            if (dragging == null) return;
            var item = dragging;
            dragging = null;
            if (highlight != null) highlight.gameObject.SetActive(false);
            item.SetLifted(false);
            item.MoveTo(CellToWorld(item.Cell));
        }

        void Update()
        {
            if (grid == null) return; // e.g. after a script reload in play mode
            if (!active) return;
            FitCamera();
            if (InputBlocked) { CancelDrag(); idleTimer = 0f; }
            else if (!gameOver) HandleInput();
            TickHint();

            saveTimer += Time.deltaTime;
            if (saveTimer > 5f) { saveTimer = 0f; Save(); }
        }

        void NewGame()
        {
            PlayerData.EnsureStartCoins();
            bestChain = 0;
            bestLevel = 1;
            gameOver = false;
            for (int i = 0; i < startingItems; i++)
            {
                RandomItem(out int chain, out int lv);
                SpawnRandom(chain, lv, 0.08f * i);
            }
        }

        public void Restart()
        {
            StopAllCoroutines();
            busyMerges = 0;
            dragging = null;
            highlight.gameObject.SetActive(false);
            foreach (Transform t in itemsRoot) Destroy(t.gameObject);
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
                var item = PickItem(mouse);
                if (item != null && ScreenToGround(mouse, out var g))
                {
                    dragging = item;
                    dragOffset = item.transform.position - g;
                    dragOffset.y = 0f;
                    item.SetLifted(true);
                    SoundFx.Tap();
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
                var item = dragging;
                dragging = null;
                highlight.gameObject.SetActive(false);
                Drop(item);
            }
        }

        PotItem PickItem(Vector2 screen)
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
                bool canMerge = dragging.Matches(other) && dragging.Level < MaxLevel;
                highlight.gameObject.SetActive(true);
                highlight.position = CellToWorld(c) + Vector3.down * 0.04f;
                highlight.GetComponent<Renderer>().material.color = canMerge ? new Color(1f, 0.9f, 0.3f) : new Color(0.92f, 0.92f, 0.92f);
            }
            else highlight.gameObject.SetActive(false);
        }

        void Drop(PotItem item)
        {
            item.SetLifted(false);
            Vector2Int from = item.Cell;

            if (!WorldToCell(item.transform.position, out var to) || to == from)
            {
                item.MoveTo(CellToWorld(from));
                return;
            }

            var other = grid[to.x, to.y];
            if (other == null)
            {
                grid[from.x, from.y] = null;
                Place(item, to);
                item.MoveTo(CellToWorld(to), 0.12f, item.PlayPunch);
                SoundFx.Swap();
            }
            else if (item.Matches(other) && item.Level < MaxLevel)
            {
                Merge(item, other, to);
                return;
            }
            else if (item.Matches(other))
            {
                item.MoveTo(CellToWorld(from));
                other.PlayShake();
                SoundFx.Error();
                hud?.ShowToast("MAX LEVEL!", new Color(1f, 0.85f, 0.3f));
            }
            else
            {
                // different chain or level: swap places
                Place(other, from);
                Place(item, to);
                other.MoveTo(CellToWorld(from), 0.18f, other.PlayPunch);
                item.MoveTo(CellToWorld(to), 0.12f, item.PlayPunch);
                SoundFx.Swap();
            }
            RefreshState();
            Save();
        }

        void Place(PotItem item, Vector2Int cell)
        {
            grid[cell.x, cell.y] = item;
            item.Cell = cell;
        }

        // ------------------------------------------------------------------ merge / spawn
        void Merge(PotItem a, PotItem b, Vector2Int cell)
        {
            grid[a.Cell.x, a.Cell.y] = null;
            grid[b.Cell.x, b.Cell.y] = null;
            Vector3 pos = CellToWorld(cell);

            // reserve the cell so nothing spawns into it during the merge animation
            busyMerges++;
            a.MoveTo(pos, 0.08f);
            StartCoroutine(FinishMerge(a, b, cell, a.Chain, a.Level, pos));
        }

        IEnumerator FinishMerge(PotItem a, PotItem b, Vector2Int cell, int chain, int fromLevel, Vector3 pos)
        {
            yield return new WaitForSeconds(0.08f);
            Destroy(a.gameObject);
            Destroy(b.gameObject);

            int newLevel = fromLevel + 1;
            var item = CreateItem(chain, newLevel, cell);
            item.transform.position = pos;
            item.PlaySpawn();
            EmitBurst(pos + Vector3.up * 0.6f, chain);
            SoundFx.Merge(newLevel);

            long reward = fromLevel; // +1 for level-1 merges, +2 for level-2, ...
            PlayerData.AddCoins(reward);
            PlayerData.OnMerge(chain, newLevel);
            int spawnMaxBefore = CurrentSpawnMax;
            hud?.FloatText(pos + Vector3.up * 1.4f, "+" + MergeHUD.Format(reward), new Color(1f, 0.85f, 0.25f), 56f);

            bool isNew = TrackBest(chain, newLevel);
            if (CurrentSpawnMax > spawnMaxBefore)
            {
                hud?.ShowToast($"ADD ITEM: up to level {CurrentSpawnMax}!", new Color(1f, 0.9f, 0.4f));
                SoundFx.Unlock();
            }
            else if (isNew)
            {
                hud?.ShowToast("NEW! " + database.LevelName(chain, newLevel), Color.white);
                SoundFx.Unlock();
            }
            if (isNew) EmitBurst(pos + Vector3.up * 1.2f, chain, 90);
            busyMerges--;
            RefreshState(true);
            Save();
        }

        void TryBuySpawn()
        {
            idleTimer = 0f;
            if (gameOver || busyMerges > 0) return;
            if (FreeCellCount() == 0)
            {
                hud?.ShowToast("Board full! Make room first", new Color(1f, 0.6f, 0.5f));
                SoundFx.Error();
                ShowHint();
                return;
            }
            if (!PlayerData.TrySpend(spawnCost))
            {
                hud?.ShowToast("Not enough coins", new Color(1f, 0.6f, 0.5f));
                SoundFx.Error();
                return;
            }
            RandomItem(out int chain, out int lv);
            SpawnRandom(chain, lv, 0f);
            PlayerData.OnAddItem();
            SoundFx.Coin();
            hud?.SetCoins(Coins, true);
            RefreshState();
            Save();
        }

        void RandomItem(out int chain, out int level)
        {
            chain = UnityEngine.Random.Range(0, ChainCount);
            level = UnityEngine.Random.Range(spawnMinLevel, Mathf.Min(CurrentSpawnMax, MaxLevel) + 1);
        }

        /// <summary>
        /// Grows step by step with this game's best item: levels 1-2 at first,
        /// 1-3 after making a level 4, 1-4 after making a level 5.
        /// </summary>
        int CurrentSpawnMax => Mathf.Clamp(bestLevel - 1, earlySpawnMaxLevel, Mathf.Max(earlySpawnMaxLevel, spawnMaxLevel));

        void SpawnRandom(int chain, int level, float delay)
        {
            var empty = new List<Vector2Int>();
            for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
                if (grid[x, y] == null) empty.Add(new Vector2Int(x, y));
            if (empty.Count == 0) return;

            var cell = empty[UnityEngine.Random.Range(0, empty.Count)];
            var item = CreateItem(chain, level, cell);
            Vector3 target = CellToWorld(cell);
            item.transform.position = target + Vector3.up * 2.5f;
            item.PlaySpawn(delay);
            StartCoroutine(DelayedDrop(item, target, delay));
            if (TrackBest(chain, level) && delay <= 0f)
                hud?.ShowToast("NEW! " + database.LevelName(chain, level), Color.white);
        }

        IEnumerator DelayedDrop(PotItem item, Vector3 target, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (item != null && !item.IsDragging)
            {
                item.MoveTo(target, 0.25f, item.PlayPunch);
                SoundFx.Pop();
            }
        }

        PotItem CreateItem(int chain, int level, Vector2Int cell)
        {
            var go = new GameObject($"Item_{database.Get(chain).id}_L{level}");
            go.transform.SetParent(itemsRoot, false);
            var item = go.AddComponent<PotItem>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            ModelUtil.Spawn(database.Model(chain, level), visual, cellSize * itemFootprint, itemHeight,
                database.SizeFor(level), turnModelsToCamera);

            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f * itemHeight * 0.6f, 0f);
            col.size = new Vector3(cellSize * 0.85f, itemHeight * 0.6f, cellSize * 0.85f);

            item.Init(chain, level, visual, 1f);
            Place(item, cell);
            return item;
        }

        /// <summary>Updates the best item of this run and the collection. Returns true for a never-seen item.</summary>
        bool TrackBest(int chain, int level)
        {
            if (level > bestLevel) { bestLevel = level; bestChain = chain; }
            return PlayerData.ReportItem(chain, level);
        }

        void EmitBurst(Vector3 pos, int chain, int count = 45)
        {
            if (burst == null) return;
            Color c = database.Get(chain).color;
            Color[] palette = { c, Color.Lerp(c, Color.white, 0.5f), new Color(1f, 0.85f, 0.25f), Color.white };
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < count; i++)
            {
                ep.position = pos;
                ep.startColor = palette[i % palette.Length];
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
                int key = p.Chain * 100 + p.Level;
                if (seen.TryGetValue(key, out var other)) { a = other; b = p; return true; }
                seen[key] = p;
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
            hud.SetCoins(Coins, punch);

            if (busyMerges > 0) return;
            bool canMerge = HasAnyMerge(out _, out _);
            bool full = FreeCellCount() == 0;
            if (full)
                hud.SetSpawn("BOARD FULL", "make room first", false);
            else
                hud.SetSpawn("ADD ITEM", spawnCost + (spawnCost == 1 ? " coin" : " coins"), Coins >= spawnCost);

            bool over = !canMerge && (full || Coins < spawnCost);
            if (over && !gameOver)
            {
                gameOver = true;
                SoundFx.Error();
                hud.ShowGameOver(true, $"Best: {database.LevelName(bestChain, bestLevel)} ({bestLevel}/{MaxLevel})\nCoins: {Coins}");
                Save();
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

        // ------------------------------------------------------------------ save / load
        [Serializable]
        class SaveData
        {
            public int[] cells; // chain * 100 + level, 0 = empty
            public int bestChain;
            public int bestLevel;
        }

        void Save()
        {
            if (grid == null) return;
            if (gameOver) { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); return; }
            var data = new SaveData { cells = new int[columns * rows], bestChain = bestChain, bestLevel = bestLevel };
            for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
            {
                var p = grid[x, y];
                data.cells[y * columns + x] = p != null ? p.Chain * 100 + p.Level : 0;
            }
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        bool Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return false;
            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            if (data == null || data.cells == null || data.cells.Length != columns * rows) return false;
            bestChain = Mathf.Clamp(data.bestChain, 0, ChainCount - 1);
            bestLevel = Mathf.Max(1, data.bestLevel);
            float d = 0f;
            int count = 0;
            for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
            {
                int v = data.cells[y * columns + x];
                int chain = v / 100, lv = v % 100;
                if (v <= 0 || !database.IsValid(chain, lv) || chain >= ChainCount) continue;
                var p = CreateItem(chain, lv, new Vector2Int(x, y));
                p.transform.position = CellToWorld(new Vector2Int(x, y));
                p.PlaySpawn(d);
                d += 0.02f;
                count++;
            }
            return count > 0;
        }

        /// <summary>Settings > reset progress: wipe the board save and start a fresh board.</summary>
        public void ResetBoard()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            if (grid != null) Restart();
        }

        [ContextMenu("Reset Board Save")]
        public void ResetSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            Debug.Log("[PotMerge] Board save reset.");
        }
    }
}
