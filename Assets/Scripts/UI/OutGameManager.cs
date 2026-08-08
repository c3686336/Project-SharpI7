using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class OutGameManager : MonoBehaviour
{
    private const string OutGameSceneName = "Outgame";
    private const string MainSceneName = "MainScene";

    private static string gameOverStageId;

    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject gameOverMenu;
    [SerializeField] private GameObject WinMenu;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button configButton;
    [SerializeField] private Button exitGameButton;
    [SerializeField] private Button closeConfigButton;
    [SerializeField] private Button gameOverTitleButton;
    [SerializeField] private Button gameOverRetryButton;
    [SerializeField] private Button winRestartButton;
    [SerializeField] private GameObject configPanel;
    [SerializeField] private TutorialSelectionPanel tutorialSelectionPanel;
    [SerializeField] private GameObject stage1GameOverBackground;
    [SerializeField] private GameObject stage2GameOverBackground;

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
            gameOverTitleButton == null || winRestartButton == null || configPanel == null ||
            tutorialSelectionPanel == null || gameOverRetryButton == null)
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
        startGameButton.onClick.AddListener(tutorialSelectionPanel.Open);
        tutorialSelectionPanel.TutorialSelected += StartTutorial;
        tutorialSelectionPanel.NormalGameSelected += StartGame;
        configButton.onClick.AddListener(OpenConfig);
        exitGameButton.onClick.AddListener(QuitGame);
        closeConfigButton.onClick.AddListener(CloseConfig);
        gameOverTitleButton.onClick.AddListener(ShowMainMenu);
        gameOverRetryButton.onClick.AddListener(StartGame);
        winRestartButton.onClick.AddListener(ShowMainMenu);
    }

    public void OnDisable()
    {
        startGameButton.onClick.RemoveListener(tutorialSelectionPanel.Open);
        tutorialSelectionPanel.TutorialSelected -= StartTutorial;
        tutorialSelectionPanel.NormalGameSelected -= StartGame;
        configButton.onClick.RemoveListener(OpenConfig);
        exitGameButton.onClick.RemoveListener(QuitGame);
        closeConfigButton.onClick.RemoveListener(CloseConfig);
        gameOverTitleButton.onClick.RemoveListener(ShowMainMenu);
        gameOverRetryButton.onClick.RemoveListener(StartGame);
        winRestartButton.onClick.RemoveListener(ShowMainMenu);
    }

    public void StartGame()
    {
        StageManager.SetInitialStage(null, false);
        LoadMainScene();
    }

    private void StartTutorial(StageData tutorialStageData)
    {
        StageManager.SetInitialStage(tutorialStageData, true);
        LoadMainScene();
    }

    private void LoadMainScene()
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

    public static void LoadGameOver(string stageId)
    {
        outgameState = OutgameState.Lose;
        gameOverStageId = stageId;

        Time.timeScale = 1f;
        SceneManager.LoadScene(OutGameSceneName);
    }

    public static void LoadTitle()
    {
        outgameState = OutgameState.Main;
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
        tutorialSelectionPanel.Close();

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

                switch (gameOverStageId ?? "stage1")
                {
                    case "stage1":
                        stage1GameOverBackground.SetActive(true);
                        stage2GameOverBackground.SetActive(false);
                        break;
                    case "stage2":
                        stage1GameOverBackground.SetActive(false);
                        stage2GameOverBackground.SetActive(true);
                        break;
                }
                break;
        }
    }
}
