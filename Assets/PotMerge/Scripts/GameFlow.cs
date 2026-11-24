using UnityEngine;

namespace PotMerge
{
    /// <summary>Screen flow: Title -> Home -> Level map -> Game (and back to Home). Owns the camera hand-off between board and backdrop.</summary>
    public class GameFlow : MonoBehaviour
    {
        public enum State { Title, Home, Game, Town }

        public MergeBoard board;
        public MergeHUD hud;
        public MenuUI menu;
        public ShopBackdrop backdrop;
        public TownScreen town;
        public Camera cam;

        public State Current { get; private set; } = State.Title;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (board != null) board.Active = false;
        }

        void Start()
        {
            if (hud != null)
            {
                hud.HomeClicked += OnGameHome;
                hud.SettingsClicked += OpenSettings;
            }
            if (menu != null)
            {
                menu.TitleTapped += ShowHome;
                menu.StartClicked += OnStartClicked;
                menu.LevelChosen += PlayLevel;
                menu.MapClosed += ShowHome;
                menu.TownClicked += GoToTown;
                menu.TownClosed += LeaveTown;
            }
            if (board != null)
            {
                board.LeaveRequested += ShowHome;
                board.NeedEnergy += OpenEnergy;
            }
            if (hud != null) { hud.EnergyClicked += OpenEnergy; hud.CustomerClicked += OpenCustomer; }
            ShowTitle();
        }

        void OnDestroy()
        {
            if (hud != null)
            {
                hud.HomeClicked -= OnGameHome;
                hud.SettingsClicked -= OpenSettings;
            }
            if (menu != null)
            {
                menu.TitleTapped -= ShowHome;
                menu.StartClicked -= OnStartClicked;
                menu.LevelChosen -= PlayLevel;
                menu.MapClosed -= ShowHome;
                menu.TownClicked -= GoToTown;
                menu.TownClosed -= LeaveTown;
            }
            if (board != null)
            {
                board.LeaveRequested -= ShowHome;
                board.NeedEnergy -= OpenEnergy;
            }
            if (hud != null) { hud.EnergyClicked -= OpenEnergy; hud.CustomerClicked -= OpenCustomer; }
        }

        void OpenEnergy() => menu?.OpenEnergy();
        void OpenCustomer() => menu?.OpenCustomer();

        void Update()
        {
            if (board != null && menu != null) board.InputBlocked = menu.PopupOpen;
        }

        public void ShowTitle()
        {
            Current = State.Title;
            EnterMenu(false);
            menu?.ShowTitle();
        }

        public void ShowHome()
        {
            Current = State.Home;
            EnterMenu(true);
            menu?.ShowHome();
        }

        /// <summary>HOME in the game: ask first, leaving gives the level up (in bonus time it just ends it).</summary>
        void OnGameHome()
        {
            if (board == null || !board.LevelInProgress) { ShowHome(); return; }
            if (board.BonusTime) { board.LeaveLevel(); return; }
            menu?.AskLeave(board.LeaveLevel);
        }

        // the town lies to the right of home: the camera slides over there behind a bank of clouds
        void GoToTown() => menu.PlayClouds(1, ShowTown, Pan);
        void LeaveTown() => menu.PlayClouds(-1, ShowHome, Pan);

        void Pan(float k)
        {
            if (backdrop != null) backdrop.Pan = k * 9f;
            if (town != null) town.Pan = k * 12f;
        }

        public void ShowTown()
        {
            if (town == null) return;
            Current = State.Town;
            backdrop?.Hide();
            town.Show(cam);
            menu?.ShowTown();
        }

        /// <summary>Home's big button: resume the level in progress, otherwise open the level map.</summary>
        void OnStartClicked()
        {
            if (board != null && board.LevelInProgress) StartGame();
            else menu?.ShowMap();
        }

        /// <summary>Resume the level in progress, or start the next level.</summary>
        public void StartGame()
        {
            if (board == null) return;
            if (board.LevelInProgress) { EnterGame(); return; }
            PlayLevel(Mathf.Min(PlayerData.LevelReached, board.LevelCount));
        }

        /// <summary>Starts a level picked on the map (a cleared one to replay, or the next one).</summary>
        public void PlayLevel(int number)
        {
            if (board == null) return;
            EnterGame();
            board.StartLevel(number);
        }

        void EnterGame()
        {
            Current = State.Game;
            menu?.HideScreens();
            town?.Hide();
            backdrop?.Hide();
            hud?.SetVisible(true);
            board.Active = true;
        }

        void EnterMenu(bool home)
        {
            if (board != null) board.Active = false;
            hud?.SetVisible(false);
            town?.Hide();
            backdrop?.Show(cam, home);
        }

        void OpenSettings() => menu?.OpenSettings();
    }
}
