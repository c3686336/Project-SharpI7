using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public sealed class InGameManager : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button titleButton;
    [SerializeField] private ChantManager chantManager;

    private bool isPauseMenuOpen;
    private bool isGameplayPauseRequested;
    private static int gameplayInputBlockedThroughFrame = -1;

    public static bool GameplayPaused { get; private set; }
    public static bool GameplayInputBlocked =>
        GameplayPaused || Time.frameCount <= gameplayInputBlockedThroughFrame;

    public bool IsPaused => isPauseMenuOpen || isGameplayPauseRequested;
    public bool IsPauseMenuOpen => isPauseMenuOpen;

    private void Awake()
    {
        if (pauseMenu == null || resumeButton == null || titleButton == null || chantManager == null)
        {
            Debug.LogError("InGameManager has missing pause-menu references.", this);
            enabled = false;
            return;
        }

        ApplyPauseState();
    }

    private void OnEnable()
    {
        if (resumeButton == null || titleButton == null)
            return;

        resumeButton.onClick.AddListener(ClosePauseMenu);
        titleButton.onClick.AddListener(ReturnToTitle);
    }

    private void OnDisable()
    {
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(ClosePauseMenu);

        if (titleButton != null)
            titleButton.onClick.RemoveListener(ReturnToTitle);

        isPauseMenuOpen = false;
        isGameplayPauseRequested = false;
        ApplyPauseState();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (isPauseMenuOpen)
            {
                SetPauseMenuOpen(false);
            }
            else if (!isGameplayPauseRequested && !chantManager.IsCasting)
            {
                SetPauseMenuOpen(true);
            }
        }

        if (isPauseMenuOpen && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == null)
        {
            resumeButton.Select();
        }
    }

    public void TogglePause()
    {
        SetPauseMenuOpen(!isPauseMenuOpen);
    }

    public void PauseGameplay()
    {
        isGameplayPauseRequested = true;
        ApplyPauseState();
    }

    public void ResumeGameplay()
    {
        isGameplayPauseRequested = false;
        ApplyPauseState();
    }

    public void ReturnToTitle()
    {
        isPauseMenuOpen = false;
        isGameplayPauseRequested = false;
        ApplyPauseState();
        OutGameManager.LoadTitle();
    }

    private void ClosePauseMenu()
    {
        SetPauseMenuOpen(false);
    }

    private void SetPauseMenuOpen(bool open)
    {
        isPauseMenuOpen = open;
        ApplyPauseState();
    }

    private void ApplyPauseState()
    {
        bool wasPaused = GameplayPaused;
        GameplayPaused = IsPaused;

        if (wasPaused && !GameplayPaused)
            gameplayInputBlockedThroughFrame = Time.frameCount;

        Time.timeScale = GameplayPaused ? 0f : 1f;

        if (pauseMenu != null)
            pauseMenu.SetActive(isPauseMenuOpen);

        if (isPauseMenuOpen && resumeButton != null)
            resumeButton.Select();
    }
}
