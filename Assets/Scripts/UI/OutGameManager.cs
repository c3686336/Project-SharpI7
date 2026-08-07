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
    [SerializeField] private Button configButton;
    [SerializeField] private Button exitGameButton;
    [SerializeField] private Button closeConfigButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button winRestartButton;
    [SerializeField] private GameObject configPanel;

    private static OutgameState outgameState;

    private enum OutgameState
    {
        Main,
        Win,
        Lose
    }

    private void Awake()
    {
        if (mainMenu == null || gameOverMenu == null || WinMenu == null ||
            startGameButton == null || configButton == null || exitGameButton == null ||
            closeConfigButton == null ||
            restartButton == null || winRestartButton == null || configPanel == null)
        {
            Debug.LogError("OutGameManager has missing menu or button references.", this);
            enabled = false;
            return;
        }

        // outgameState = OutgameState.Main;
        SetMenuState();
    }

    public void OnEnable()
    {
        startGameButton.onClick.AddListener(StartGame);
        configButton.onClick.AddListener(OpenConfig);
        exitGameButton.onClick.AddListener(QuitGame);
        closeConfigButton.onClick.AddListener(CloseConfig);
        restartButton.onClick.AddListener(ShowMainMenu);
        winRestartButton.onClick.AddListener(ShowMainMenu);
    }

    public void OnDisable()
    {
        startGameButton.onClick.RemoveListener(StartGame);
        configButton.onClick.RemoveListener(OpenConfig);
        exitGameButton.onClick.RemoveListener(QuitGame);
        closeConfigButton.onClick.RemoveListener(CloseConfig);
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

    public void OpenConfig()
    {
        configPanel.SetActive(true);
    }

    public void CloseConfig()
    {
        configPanel.SetActive(false);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
        configPanel.SetActive(false);

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
