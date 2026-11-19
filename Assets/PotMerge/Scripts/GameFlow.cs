using UnityEngine;

namespace PotMerge
{
    /// <summary>Screen flow: Title -> Home -> Game (and back to Home). Owns the camera hand-off between board and backdrop.</summary>
    public class GameFlow : MonoBehaviour
    {
        public enum State { Title, Home, Game }

        public MergeBoard board;
        public MergeHUD hud;
        public MenuUI menu;
        public ShopBackdrop backdrop;
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
                hud.HomeClicked += ShowHome;
                hud.SettingsClicked += OpenSettings;
            }
            if (menu != null)
            {
                menu.TitleTapped += ShowHome;
                menu.StartClicked += StartGame;
            }
            if (board != null)
            {
                board.LeaveRequested += ShowHome;
                board.NeedHearts += OpenHearts;
                board.NeedEnergy += OpenEnergy;
            }
            if (hud != null) hud.EnergyClicked += OpenEnergy;
            ShowTitle();
        }

        void OnDestroy()
        {
            if (hud != null)
            {
                hud.HomeClicked -= ShowHome;
                hud.SettingsClicked -= OpenSettings;
            }
            if (menu != null)
            {
                menu.TitleTapped -= ShowHome;
                menu.StartClicked -= StartGame;
            }
            if (board != null)
            {
                board.LeaveRequested -= ShowHome;
                board.NeedHearts -= OpenHearts;
                board.NeedEnergy -= OpenEnergy;
            }
            if (hud != null) hud.EnergyClicked -= OpenEnergy;
        }

        void OpenHearts() => menu?.OpenHearts();
        void OpenEnergy() => menu?.OpenEnergy();

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

        /// <summary>GAME START: resume the level in progress, or start the next level (needs a heart).</summary>
        public void StartGame()
        {
            if (board == null) return;
            bool resume = board.LevelInProgress;
            if (!resume && PlayerData.Hearts <= 0) { OpenHearts(); return; }
            Current = State.Game;
            menu?.HideScreens();
            backdrop?.Hide();
            hud?.SetVisible(true);
            board.Active = true;
            if (!resume) board.StartLevel(Mathf.Min(PlayerData.LevelReached, board.LevelCount));
        }

        void EnterMenu(bool home)
        {
            if (board != null) board.Active = false;
            hud?.SetVisible(false);
            backdrop?.Show(cam, home);
        }

        void OpenSettings() => menu?.OpenSettings();
    }
}
