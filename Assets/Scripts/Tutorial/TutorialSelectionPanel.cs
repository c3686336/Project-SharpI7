using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialSelectionPanel : MonoBehaviour
{
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private StageData tutorialStageData;

    public event Action<StageData> TutorialSelected;
    public event Action NormalGameSelected;

    private void Awake()
    {
        if (yesButton != null && noButton != null)
        {
            return;
        }

        Debug.LogError("TutorialSelectionPanel has missing button references.", this);
        enabled = false;
    }

    private void OnEnable()
    {
        if (!enabled)
        {
            return;
        }

        yesButton.onClick.AddListener(SelectTutorial);
        noButton.onClick.AddListener(SelectNormalGame);
    }

    private void OnDisable()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(SelectTutorial);
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(SelectNormalGame);
        }
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void SelectTutorial()
    {
        if (tutorialStageData == null)
        {
            Debug.LogError("TutorialSelectionPanel requires a tutorial StageData.", this);
            return;
        }

        if (!tutorialStageData.IsTutorial)
        {
            Debug.LogError(
                $"{tutorialStageData.name} is not marked as a tutorial stage.",
                tutorialStageData);
            return;
        }

        TutorialSelected?.Invoke(tutorialStageData);
    }

    private void SelectNormalGame()
    {
        NormalGameSelected?.Invoke();
    }
}
