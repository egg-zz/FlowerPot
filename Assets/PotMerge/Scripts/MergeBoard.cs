using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    /// <summary>
    /// Core of the merge game (4 chains x 11 levels on a 7x7 board), played as designed levels.
    /// Rules:
    ///  - Only the same chain + same level merge. Dropping onto any other item swaps them.
    ///  - Merging two level-N items gives one level-(N+1) item and +(N-1) coins (level-1 merges give none).
    ///  - Each level (<see cref="LevelDatabase"/>) has goals, a time limit (0 = none), a fixed starting board and
    ///    4 item boxes (one per chain) standing on board cells. Tapping a box costs 1 energy and drops a level 1-2 item
    ///    (1-3 from level 25, when board blockers start)
    ///    of that chain into the nearest empty cell. Boxes can be dragged around but never merge.
    ///  - All goals done = clear (1-3 stars by time left). Time up or board full with no merge = fail. Giving up a failed
    ///    level costs a heart; time up can be extended with an ad or jewels.
    /// A level in progress is kept in memory while the player visits the home screen; it is not saved to disk.
    /// </summary>
    public class MergeBoard : MonoBehaviour
    {
        [Header("Content")]
        public ChainDatabase database;
        public LevelDatabase levels;
        [Tooltip("Max footprint (x/z) of an item, relative to the cell size")]
        public float itemFootprint = 0.8f;
        [Tooltip("Max height of an item in world units")]
        public float itemHeight = 2.1f;
        public bool turnModelsToCamera = true;

        [Header("Board")]
        [Tooltip("Camera tilt in degrees (90 = straight down). Lower shows more of each item's front.")]
        public float cameraPitch = 48f;
        public int columns = 7;
        public int rows = 7;
        public float cellSize = 1.7f;
        public Color tileA = new Color(0.47f, 0.70f, 0.30f);
        public Color tileB = new Color(0.40f, 0.62f, 0.25f);
        public Color boardColor = new Color(0.45f, 0.29f, 0.18f);

        [Header("Item boxes")]
        [Tooltip("Box model per chain (0 Flower, 1 Cafe, 2 Bakery, 3 Ice Cream)")]
        public GameObject[] boxModels = new GameObject[4];
        [Tooltip("Lowest level a box drops")]
        public int boxMinLevel = 1;
        [Tooltip("Highest level a box drops in early levels (rolled evenly between min and this)")]
        public int boxMaxLevelEarly = 2;
        [Tooltip("Highest level a box drops from boxUpgradeLevel on")]
        public int boxMaxLevel = 3;
        [Tooltip("From this level on (board blockers start at 25) boxes can drop up to boxMaxLevel")]
        public int boxUpgradeLevel = 25;
        public int energyPerTap = 1;
        [Tooltip("A press that moves less than this (pixels) counts as a tap on a box")]
        public float tapSlop = 18f;

        [Header("Rules")]
        public float hintDelay = 6f;
        [Tooltip("Seconds added by the 'more time' offer after time runs out")]
        public int extraSeconds = 30;
        [Tooltip("Jewels for the 'more time' offer")]
        public int extraTimeJewels = 5;

        [Header("Refs")]
        public Camera cam;
        public MergeHUD hud;

        const string OldSaveKey = "PotMerge.Board.v3"; // endless-mode board save, no longer used

        public enum LevelState { None, Playing, Cleared, Failed }

        /// <summary>Raised after every merge with (chain, new level).</summary>
        public event Action<int, int> Merged;
        /// <summary>Raised when the player leaves a decided level from a result popup (go to the home screen).</summary>
        public event Action LeaveRequested;
        /// <summary>Raised when the player wants to play on but has no hearts left.</summary>
        public event Action NeedHearts;
        /// <summary>Raised when a box is tapped without enough energy.</summary>
        public event Action NeedEnergy;

        PotItem[,] grid;
        bool gameOver;   // true once the level is decided (cleared or failed): no more input
        float idleTimer;

        PotItem dragging;
        Vector3 dragOffset;
        Vector2 pressScreen;
        bool pressMoved;
        Transform itemsRoot;
        ParticleSystem burst;
        float lastAspect = -1f;
        Transform highlight;
        int busyMerges;

        // current level
        int levelNumber;
        LevelDatabase.Level level;
        LevelState state = LevelState.None;
        float timeLeft;
        int[] goalProgress = new int[0];
        bool extended;   // bought extra time: the clear is worth 1 star
        bool failTimeUp;

        int MaxLevel => database != null ? database.MaxLevel : 0;
        int ChainCount => database != null ? Mathf.Min(database.ChainCount, PlayerData.ChainCount) : 0;
        static long Coins => PlayerData.Coins;

        // ------------------------------------------------------------------ lifecycle
        void Start()
        {
            if (cam == null) cam = Camera.main;
            if (database == null || ChainCount == 0 || levels == null || levels.Count == 0)
            {
                Debug.LogError("[PotMerge] MergeBoard needs a ChainDatabase and a LevelDatabase.");
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
                hud.RetryClicked += GiveUpAndRetry;
                hud.NextClicked += OnNextClicked;
                hud.MoreTimeAdClicked += OnMoreTimeAd;
                hud.MoreTimeJewelClicked += OnMoreTimeJewels;
                hud.GiveUpHomeClicked += GiveUp;
                hud.ClearHomeClicked += LeaveAfterClear;
            }
            PlayerData.Changed += OnPlayerDataChanged;
            if (PlayerPrefs.HasKey(OldSaveKey)) { PlayerPrefs.DeleteKey(OldSaveKey); PlayerPrefs.Save(); }
        }

        void OnDestroy()
        {
            if (hud != null)
            {
                hud.RetryClicked -= GiveUpAndRetry;
                hud.NextClicked -= OnNextClicked;
                hud.MoreTimeAdClicked -= OnMoreTimeAd;
                hud.MoreTimeJewelClicked -= OnMoreTimeJewels;
                hud.GiveUpHomeClicked -= GiveUp;
                hud.ClearHomeClicked -= LeaveAfterClear;
            }
            PlayerData.Changed -= OnPlayerDataChanged;
        }

        void OnPlayerDataChanged()
        {
            if (hud != null) hud.SetCoins(Coins);
        }

        bool active = true;

        /// <summary>False while the title/home screen is shown: no camera control, input, hints or timer.</summary>
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

        /// <summary>A level is being played (possibly paused on the home screen).</summary>
        public bool LevelInProgress => state == LevelState.Playing;
        public int LevelNumber => levelNumber;
        public LevelState State => state;
        public int LevelCount => levels != null ? levels.Count : 0;

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
            TickTimer();
        }

        // ------------------------------------------------------------------ levels
        /// <summary>Starts (or restarts) a level from its designed board.</summary>
        public void StartLevel(int number)
        {
            if (grid == null || levels == null || levels.Count == 0) return;
            number = Mathf.Clamp(number, 1, levels.Count);
            var def = levels.Get(number);
            if (def == null) return;
            ClearBoard();
            levelNumber = number;
            level = def;
            state = LevelState.Playing;
            gameOver = false;
            extended = false;
            timeLeft = def.seconds;
            goalProgress = new int[def.goals != null ? def.goals.Length : 0];
            hud?.HideResults();

            float d = 0f;
            foreach (var (chain, cell) in LevelDatabase.ParseBoxes(def.boxes))
            {
                if (chain >= ChainCount || boxModels == null || chain >= boxModels.Length || boxModels[chain] == null) continue;
                if (cell.x < 0 || cell.y < 0 || cell.x >= columns || cell.y >= rows || grid[cell.x, cell.y] != null) continue;
                var box = CreateItem(chain, 0, cell);
                box.transform.position = CellToWorld(cell);
                box.PlaySpawn(d);
                d += 0.06f;
            }
            foreach (var (chain, lv, cell) in LevelDatabase.ParseStart(def.startItems))
            {
                if (chain >= ChainCount || !database.IsValid(chain, lv)) continue;
                if (cell.x < 0 || cell.y < 0 || cell.x >= columns || cell.y >= rows || grid[cell.x, cell.y] != null) continue;
                var p = CreateItem(chain, lv, cell);
                Vector3 target = CellToWorld(cell);
                p.transform.position = target + Vector3.up * 2.5f;
                p.PlaySpawn(d);
                StartCoroutine(DelayedDrop(p, target, d));
                PlayerData.ReportItem(chain, lv);
                d += 0.06f;
            }
            hud?.SetLevel(levelNumber);
            hud?.SetTimer(timeLeft, def.seconds);
            RefreshGoals();
            RefreshState();
        }

        void ClearBoard()
        {
            StopAllCoroutines();
            busyMerges = 0;
            dragging = null;
            if (highlight != null) highlight.gameObject.SetActive(false);
            if (itemsRoot != null) foreach (Transform t in itemsRoot) Destroy(t.gameObject);
            grid = new PotItem[columns, rows];
        }

        bool Timed => level != null && level.seconds > 0;

        void TickTimer()
        {
            if (level == null) return;
            if (!Timed) { hud?.SetTimer(0f, 0f); return; }
            if (state == LevelState.Playing && !InputBlocked)
            {
                timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
                if (timeLeft <= 0f && busyMerges == 0) Fail(true);
            }
            hud?.SetTimer(timeLeft, level.seconds);
        }

        void Fail(bool timeUp)
        {
            if (state != LevelState.Playing) return;
            state = LevelState.Failed;
            gameOver = true;
            failTimeUp = timeUp;
            CancelDrag();
            SoundFx.Error();
            hud?.ShowLevelFail(levelNumber, timeUp, extraSeconds, extraTimeJewels, PlayerData.AdsLeftToday > 0);
        }

        void Clear()
        {
            if (state != LevelState.Playing) return;
            state = LevelState.Cleared;
            gameOver = true;
            CancelDrag();
            // untimed (tutorial) levels always give 3 stars
            float frac = Timed ? timeLeft / level.seconds : 1f;
            int stars = extended ? 1 : frac >= 0.5f ? 3 : frac >= 0.25f ? 2 : 1;
            var reward = PlayerData.CompleteLevel(levelNumber, stars);
            StartCoroutine(ShowClearSoon(stars, reward.coins, reward.jewels, reward.energy));
        }

        IEnumerator ShowClearSoon(int stars, int coins, int jewels, int energy)
        {
            yield return new WaitForSeconds(0.7f);
            SoundFx.Unlock();
            hud?.ShowLevelClear(levelNumber, stars, coins, jewels, energy, levelNumber < levels.Count);
        }

        /// <summary>Gives up a failed (or stuck) level: costs a heart.</summary>
        void LoseLevel()
        {
            if (state != LevelState.Failed && state != LevelState.Playing) return;
            state = LevelState.None;
            gameOver = false;
            PlayerData.LoseHeart();
        }

        void GiveUpAndRetry()
        {
            LoseLevel();
            hud?.HideResults();
            if (PlayerData.Hearts <= 0) { ClearBoard(); LeaveRequested?.Invoke(); NeedHearts?.Invoke(); return; }
            StartLevel(levelNumber);
        }

        void GiveUp()
        {
            LoseLevel();
            hud?.HideResults();
            ClearBoard();
            LeaveRequested?.Invoke();
        }

        void LeaveAfterClear()
        {
            state = LevelState.None;
            hud?.HideResults();
            ClearBoard();
            LeaveRequested?.Invoke();
        }

        void OnNextClicked()
        {
            hud?.HideResults();
            if (levelNumber >= levels.Count) { LeaveAfterClear(); return; }
            if (PlayerData.Hearts <= 0) { LeaveAfterClear(); NeedHearts?.Invoke(); return; }
            StartLevel(levelNumber + 1);
        }

        void Extend()
        {
            if (state != LevelState.Failed) return;
            state = LevelState.Playing;
            gameOver = false;
            extended = true;
            timeLeft += extraSeconds;
            hud?.HideResults();
            hud?.ShowToast($"+{extraSeconds} seconds!", new Color(0.6f, 0.9f, 1f));
            RefreshState();
        }

        void OnMoreTimeAd()
        {
            if (state != LevelState.Failed || !failTimeUp) return;
            if (PlayerData.AdsLeftToday <= 0) { SoundFx.Error(); return; }
            AdService.ShowRewarded(Extend);
        }

        void OnMoreTimeJewels()
        {
            if (state != LevelState.Failed || !failTimeUp) return;
            if (!PlayerData.TrySpendJewels(extraTimeJewels))
            {
                SoundFx.Error();
                hud?.ShowToast("Not enough jewels", new Color(1f, 0.6f, 0.5f));
                return;
            }
            SoundFx.Coin();
            Extend();
        }

        /// <summary>A new item appeared: merges count for merge goals; any new item (merged or from a box) counts for "make" goals.</summary>
        void ProgressGoals(int chain, int newLevel, bool fromMerge = true)
        {
            if (level?.goals == null) return;
            for (int i = 0; i < level.goals.Length; i++)
            {
                var g = level.goals[i];
                if (g.chain >= 0 && g.chain != chain) continue;
                if (g.type == GoalType.MergeAny && fromMerge) goalProgress[i] = Mathf.Min(g.count, goalProgress[i] + 1);
                else if (g.type == GoalType.MakeItem && newLevel >= g.level) goalProgress[i] = g.count;
            }
            RefreshGoals();
        }

        bool AllGoalsDone()
        {
            if (level?.goals == null || level.goals.Length == 0) return false;
            for (int i = 0; i < level.goals.Length; i++) if (goalProgress[i] < level.goals[i].count) return false;
            return true;
        }

        void RefreshGoals()
        {
            if (hud == null || level?.goals == null) return;
            var list = new List<MergeHUD.GoalView>();
            for (int i = 0; i < level.goals.Length; i++)
            {
                var g = level.goals[i];
                string text;
                Texture2D icon = null;
                if (g.type == GoalType.MergeAny)
                    text = g.chain < 0 ? "Merge" : "Merge " + PlayerData.ChainNames[Mathf.Clamp(g.chain, 0, PlayerData.ChainCount - 1)];
                else if (g.chain >= 0)
                {
                    text = database.LevelName(g.chain, g.level);
                    icon = database.Icon(g.chain, g.level);
                }
                else text = $"Any Lv {g.level}";
                list.Add(new MergeHUD.GoalView { text = text, icon = icon, progress = goalProgress[i], target = g.count });
            }
            hud.SetGoals(list);
        }

        // ------------------------------------------------------------------ board building
        void BuildBoard()
        {
            var boardRoot = new GameObject("BoardVisual").transform;
            boardRoot.SetParent(transform, false);

            // soft, rounded board: a pillowy slab with rounded tiles
            var slab = RoundedMesh.Create("Slab", boardRoot, columns * cellSize + 0.7f, rows * cellSize + 0.7f, 0.5f, 0.9f, 0.18f, boardColor);
            slab.transform.localPosition = new Vector3(0f, -0.3f, 0f);

            float tileSize = cellSize * 0.9f;
            for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
            {
                var tile = RoundedMesh.Create($"Tile_{x}_{y}", boardRoot, tileSize, tileSize, 0.14f, tileSize * 0.24f, 0.06f,
                    (x + y) % 2 == 0 ? tileA : tileB);
                tile.transform.position = CellToWorld(new Vector2Int(x, y)) + Vector3.down * 0.07f;
            }

            var hl = RoundedMesh.Create("DropHighlight", boardRoot, tileSize, tileSize, 0.16f, tileSize * 0.24f, 0.06f, new Color(1f, 0.95f, 0.55f));
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
            float pitch = cameraPitch;
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
                    pressScreen = mouse;
                    pressMoved = false;
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
                if ((mouse - pressScreen).sqrMagnitude > tapSlop * tapSlop) pressMoved = true;
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
                if (item.IsGenerator && !pressMoved)
                {
                    item.SetLifted(false);
                    item.MoveTo(CellToWorld(item.Cell));
                    TapBox(item);
                }
                else Drop(item);
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

            long reward = MergeCoins(fromLevel);
            PlayerData.AddCoins(reward);
            PlayerData.OnMerge(chain, newLevel);
            Merged?.Invoke(chain, newLevel);
            if (reward > 0)
                hud?.FloatText(pos + Vector3.up * 1.4f, "+" + MergeHUD.Format(reward), new Color(1f, 0.85f, 0.25f), 56f);

            if (PlayerData.ReportItem(chain, newLevel))
            {
                hud?.ShowToast("NEW! " + database.LevelName(chain, newLevel), Color.white);
                SoundFx.Unlock();
                EmitBurst(pos + Vector3.up * 1.2f, chain, 90);
            }
            busyMerges--;
            if (state == LevelState.Playing)
            {
                ProgressGoals(chain, newLevel);
                if (AllGoalsDone()) { hud?.SetCoins(Coins, true); Clear(); yield break; }
            }
            RefreshState(true);
        }

        /// <summary>Coins for merging two level-N items: level-1 merges give nothing, then N-1 (2 -> +1, 3 -> +2, ...).</summary>
        public static long MergeCoins(int fromLevel) => Math.Max(0, fromLevel - 1);

        /// <summary>Tapping an item box: costs energy, drops a level 1-2 item (1-3 from boxUpgradeLevel) of the box's chain next to it.</summary>
        void TapBox(PotItem box)
        {
            idleTimer = 0f;
            if (gameOver || state != LevelState.Playing) return;
            if (!NearestEmpty(box.Cell, out var cell))
            {
                box.PlayShake();
                hud?.ShowToast("Board full! Make room first", new Color(1f, 0.6f, 0.5f));
                SoundFx.Error();
                ShowHint();
                return;
            }
            if (!PlayerData.TrySpendEnergy(energyPerTap))
            {
                box.PlayShake();
                SoundFx.Error();
                NeedEnergy?.Invoke();
                return;
            }
            int top = levelNumber >= boxUpgradeLevel ? boxMaxLevel : boxMaxLevelEarly;
            int lo = Mathf.Clamp(boxMinLevel, 1, MaxLevel), hi = Mathf.Clamp(top, lo, MaxLevel);
            int lv = UnityEngine.Random.Range(lo, hi + 1);
            var item = CreateItem(box.Chain, lv, cell);
            item.transform.position = box.transform.position + Vector3.up * 1.2f;
            item.PlaySpawn();
            item.MoveTo(CellToWorld(cell), 0.28f, item.PlayPunch);
            box.PlayPunch();
            SoundFx.Pop();
            hud?.FloatText(box.transform.position + Vector3.up * 1.8f, "-" + energyPerTap, new Color(0.55f, 0.85f, 1f), 46f);
            if (PlayerData.ReportItem(box.Chain, lv)) hud?.ShowToast("NEW! " + database.LevelName(box.Chain, lv), Color.white);
            PlayerData.OnAddItem();
            ProgressGoals(box.Chain, lv, false);
            if (AllGoalsDone()) { Clear(); return; }
            RefreshState();
        }

        /// <summary>Closest empty cell to <paramref name="from"/> (random among equally close ones).</summary>
        bool NearestEmpty(Vector2Int from, out Vector2Int cell)
        {
            float best = float.MaxValue;
            var ties = new List<Vector2Int>();
            for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
            {
                if (grid[x, y] != null) continue;
                float d = (new Vector2Int(x, y) - from).sqrMagnitude;
                if (d < best - 0.01f) { best = d; ties.Clear(); ties.Add(new Vector2Int(x, y)); }
                else if (Mathf.Abs(d - best) <= 0.01f) ties.Add(new Vector2Int(x, y));
            }
            cell = ties.Count > 0 ? ties[UnityEngine.Random.Range(0, ties.Count)] : default;
            return ties.Count > 0;
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

        PotItem CreateItem(int chain, int lv, Vector2Int cell)
        {
            bool isBox = lv == 0;
            var go = new GameObject(isBox ? $"Box_{database.Get(chain).id}" : $"Item_{database.Get(chain).id}_L{lv}");
            go.transform.SetParent(itemsRoot, false);
            var item = go.AddComponent<PotItem>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            ModelUtil.Spawn(isBox ? boxModels[chain] : database.Model(chain, lv), visual, cellSize * itemFootprint, itemHeight,
                isBox ? 0.92f : database.SizeFor(lv), turnModelsToCamera);

            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f * itemHeight * 0.6f, 0f);
            col.size = new Vector3(cellSize * 0.85f, itemHeight * 0.6f, cellSize * 0.85f);

            item.Init(chain, lv, visual, 1f);
            Place(item, cell);
            return item;
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
                if (p == null || p.IsGenerator || p.Level >= MaxLevel) continue;
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
            if (state != LevelState.Playing || busyMerges > 0) return;
            // board full and nothing to merge: the level is lost (boxes have nowhere to drop items)
            if (!HasAnyMerge(out _, out _) && FreeCellCount() == 0) Fail(false);
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

        // ------------------------------------------------------------------ reset
        /// <summary>Settings > reset progress: drop the level in progress.</summary>
        public void ResetBoard()
        {
            if (grid == null) return;
            state = LevelState.None;
            gameOver = false;
            level = null;
            hud?.HideResults();
            ClearBoard();
        }
    }
}
