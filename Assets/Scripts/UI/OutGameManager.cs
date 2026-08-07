using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class OutGameManager : MonoBehaviour
{
    private const string OutGameSceneName = "Outgame";
    private const string MainSceneName = "MainScene";

    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject gameOverMenu;
    [SerializeField] private GameObject WinMenu;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button winRestartButton;

    private static OutgameState outgameState;

    private enum OutgameState
    {
        Main,
        Win,
        Lose
    }

    private void Awake()
    {
        if (mainMenu == null || gameOverMenu == null)
        {
            Debug.LogError("OutGameManager requires Main Menu and Game Over Menu references.", this);
            enabled = false;
            return;
        }

        // outgameState = OutgameState.Main;
        SetMenuState();
    }

    public void OnEnable()
    {
        startGameButton.onClick.AddListener(StartGame);
        restartButton.onClick.AddListener(ShowMainMenu);
        winRestartButton.onClick.AddListener(ShowMainMenu);
    }

    public void OnDisable()
    {
        startGameButton.onClick.RemoveListener(StartGame);
        restartButton.onClick.RemoveListener(ShowMainMenu);
        winRestartButton.onClick.RemoveListener(ShowMainMenu);
    }

    public void StartGame()
    {
        outgameState = OutgameState.Main;
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainSceneName);
    }

    public void ShowMainMenu()
    {
        outgameState = OutgameState.Main;
        SetMenuState();
    }

    public static void LoadGameOver()
    {
        outgameState = OutgameState.Lose;
        Time.timeScale = 1f;
        SceneManager.LoadScene(OutGameSceneName);
    }

    public static void LoadWin()
    {
        outgameState = OutgameState.Win;
        Time.timeScale = 1f;
        SceneManager.LoadScene(OutGameSceneName);
    }

    private void SetMenuState()
    {
        switch (outgameState)
        {
            case OutgameState.Main:
                mainMenu.SetActive(true);
                gameOverMenu.SetActive(false);
                WinMenu.SetActive(false);
                break;
            case OutgameState.Win:
                mainMenu.SetActive(false);
                gameOverMenu.SetActive(false);
                WinMenu.SetActive(true);
                break;
            case OutgameState.Lose:
                mainMenu.SetActive(false);
                gameOverMenu.SetActive(true);
                WinMenu.SetActive(false);
                break;
        }
    }
}
