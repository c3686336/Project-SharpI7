using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[Serializable]
public sealed class TutorialDialogueData
{
    public string tutorialId;
    public TutorialDialogueStep[] steps;
}

[Serializable]
public sealed class TutorialDialogueStep
{
    public string description;
    public string text;
    public Vector2 arrowPosition;
    public string arrowDirection;
}

[DisallowMultipleComponent]
public sealed class TutorialDialogueController : MonoBehaviour, IPointerClickHandler
{
    private const string DefaultFileName = "tutorial.json";

    [SerializeField] private string fileName = DefaultFileName;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private RectTransform tutorialArrow;

    private TutorialDialogueStep[] steps;
    private InGameManager inGameManager;
    private int currentStepIndex = -1;
    private bool isRunning;

    public void Begin(InGameManager manager)
    {
        inGameManager = manager;

        if (dialogueText == null)
        {
            Debug.LogError(
                "[TutorialDialogue] TutorialCanvas 아래에서 TMP 텍스트를 찾을 수 없습니다.",
                this);
            Finish();
            return;
        }

        if (!TryLoadSteps())
        {
            Finish();
            return;
        }

        currentStepIndex = 0;
        isRunning = true;
        ShowCurrentStep();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isRunning || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        int nextStepIndex = currentStepIndex + 1;
        if (nextStepIndex >= steps.Length)
        {
            Finish();
            return;
        }

        currentStepIndex = nextStepIndex;
        ShowCurrentStep();
    }

    private bool TryLoadSteps()
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"[TutorialDialogue] JSON 파일을 찾을 수 없습니다: {path}", this);
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            TutorialDialogueData data = JsonUtility.FromJson<TutorialDialogueData>(json);

            if (data?.steps == null || data.steps.Length == 0)
            {
                Debug.LogError("[TutorialDialogue] 출력할 튜토리얼 대사가 없습니다.", this);
                return false;
            }

            steps = data.steps;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[TutorialDialogue] JSON 로드에 실패했습니다.\n{exception}", this);
            return false;
        }
    }

    private void ShowCurrentStep()
    {
        TutorialDialogueStep step = steps[currentStepIndex];
        dialogueText.text = step.text ?? string.Empty;
        ApplyArrow(step);
    }

    private void ApplyArrow(TutorialDialogueStep step)
    {
        if (tutorialArrow == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(step.arrowDirection) ||
            step.arrowDirection.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            tutorialArrow.gameObject.SetActive(false);
            return;
        }

        tutorialArrow.gameObject.SetActive(true);
        tutorialArrow.anchoredPosition = step.arrowPosition;
        tutorialArrow.localRotation = Quaternion.Euler(0f, 0f, GetArrowRotation(step.arrowDirection));
    }

    private static float GetArrowRotation(string direction)
    {
        switch (direction.Trim().ToLowerInvariant())
        {
            case "up":
                return 90f;
            case "left":
                return 180f;
            case "down":
                return -90f;
            case "right":
            default:
                return 0f;
        }
    }

    private void Finish()
    {
        isRunning = false;
        currentStepIndex = -1;

        if (tutorialArrow != null)
        {
            tutorialArrow.gameObject.SetActive(false);
        }

        inGameManager?.ResumeGameplay();
        gameObject.SetActive(false);
    }
}
