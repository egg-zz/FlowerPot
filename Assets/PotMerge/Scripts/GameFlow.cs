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
        }

        void Update()
        {
            if (board != null && menu != null) board.InputBlocked = menu.PopupOpen;
        }

        public void ShowTitle()
        {
            Current = State.Title;
            EnterMenu();
            menu?.ShowTitle();
        }

        public void ShowHome()
        {
            Current = State.Home;
            EnterMenu();
            menu?.ShowHome();
        }

        public void StartGame()
        {
            Current = State.Game;
            menu?.HideScreens();
            backdrop?.Hide();
            hud?.SetVisible(true);
            if (board != null)
            {
                if (board.IsGameOver) board.Restart();
                board.Active = true;
            }
        }

        void EnterMenu()
        {
            if (board != null) board.Active = false;
            hud?.SetVisible(false);
            backdrop?.Show(cam);
        }

        void OpenSettings() => menu?.OpenSettings();
    }
}
