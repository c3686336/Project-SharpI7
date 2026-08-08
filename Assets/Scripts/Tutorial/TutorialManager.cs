using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    public string action;
}

[DisallowMultipleComponent]
public sealed class TutorialManager : MonoBehaviour
{
    private const string DefaultFileName = "tutorial.json";
    private const string PreviewManaProfilesAction = "previewManaProfiles";
    private const string PreviewManaSaturationDamageAction = "previewManaSaturationDamage";
    private const string PreviewManaConsumptionAction = "previewManaConsumption";
    private const string WaitForChantEnterAction = "waitForChantEnter";
    private const string HideChantPreviewAction = "hideChantPreview";

    [SerializeField] private string fileName = DefaultFileName;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private RectTransform tutorialArrow;

    [Header("Chant Preview UI")]
    [SerializeField] private GameObject chantPreviewPanel;
    [SerializeField] private BookChantAnimator chantPreviewBook;

    [Header("Mana Preview UI")]
    [SerializeField] private Image manaFillImage;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Sprite warningPortrait;
    [SerializeField] private Sprite saturatedPortrait;

    [Header("Mana Preview Layout")]
    [SerializeField] private Vector2 warningPortraitPosition = new(10f, 10f);
    [SerializeField] private Vector2 warningPortraitSize = new(200f, 200f);
    [SerializeField] private Vector2 saturatedPortraitPosition = new(5f, 60f);
    [SerializeField] private Vector2 saturatedPortraitSize = new(270f, 280f);

    [Header("Mana Preview Fill")]
    [SerializeField, Range(0f, 1f)] private float warningFillAmount = 0.42424244f;
    [SerializeField, Range(0f, 1f)] private float overloadFillAmount = 0.72727275f;
    [SerializeField, Min(0.05f)] private float manaPreviewSegmentDuration = 0.75f;
    [SerializeField, Min(0f)] private float manaPreviewHoldDuration = 0.6f;
    [SerializeField, Min(0.05f)] private float manaConsumptionDuration = 1.5f;

    [Header("Health Preview UI")]
    [SerializeField] private RectTransform heartContainer;
    [SerializeField] private Sprite emptyHeartSprite;

    private TutorialDialogueStep[] steps;
    private InGameManager inGameManager;
    private Coroutine actionRoutine;
    private ManaVisualSnapshot manaVisualSnapshot;
    private HeartVisualSnapshot heartVisualSnapshot;
    private Image previewHeartImage;
    private float actionFinalFillAmount;
    private int currentStepIndex = -1;
    private int inputBlockedThroughFrame = -1;
    private bool isRunning;
    private bool isActionPlaying;
    private bool isWaitingForChantEnter;
    private bool chantPreviewSnapshotCaptured;
    private bool chantPreviewInitialActive;
    private bool manaPreviewActive;
    private bool healthPreviewActive;

    private struct ManaVisualSnapshot
    {
        public float FillAmount;
        public Sprite Portrait;
        public Vector2 PortraitPosition;
        public Vector2 PortraitSize;
    }

    private struct HeartVisualSnapshot
    {
        public Sprite Sprite;
        public Color Color;
    }

    public void Begin(InGameManager manager)
    {
        StopCurrentAction();
        RestoreChantPreview();
        RestoreManaVisuals();
        RestoreHealthVisuals();
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
        inputBlockedThroughFrame = Time.frameCount;
        isWaitingForChantEnter = false;
        isRunning = true;
        ShowCurrentStep();
    }

    private void Update()
    {
        if (!isRunning || Time.frameCount <= inputBlockedThroughFrame)
        {
            return;
        }

        if (isWaitingForChantEnter)
        {
            HandleChantEnterInput();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        AdvanceDialogue();
    }

    private void AdvanceDialogue()
    {
        if (isActionPlaying)
        {
            CompleteCurrentAction();
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
        RunStepAction(step);
    }

    private void RunStepAction(TutorialDialogueStep step)
    {
        if (string.IsNullOrWhiteSpace(step.action))
        {
            return;
        }

        switch (step.action)
        {
            case WaitForChantEnterAction:
                isWaitingForChantEnter = true;
                break;
            case HideChantPreviewAction:
                HideChantPreview();
                break;
            case PreviewManaProfilesAction:
                StartManaProfilePreview();
                break;
            case PreviewManaSaturationDamageAction:
                PreviewManaSaturationDamage();
                break;
            case PreviewManaConsumptionAction:
                StartManaConsumptionPreview();
                break;
            default:
                Debug.LogWarning(
                    $"[TutorialDialogue] 알 수 없는 action입니다: {step.action}",
                    this);
                break;
        }
    }

    private void HandleChantEnterInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        bool enterPressed = keyboard.enterKey.wasPressedThisFrame ||
                            keyboard.numpadEnterKey.wasPressedThisFrame;
        if (!enterPressed)
        {
            return;
        }

        isWaitingForChantEnter = false;
        ShowChantPreview();
        AdvanceDialogue();
    }

    private void ShowChantPreview()
    {
        if (chantPreviewPanel == null)
        {
            Debug.LogError(
                "[TutorialDialogue] 영창 시연용 패널이 연결되지 않았습니다.",
                this);
            return;
        }

        if (!chantPreviewSnapshotCaptured)
        {
            chantPreviewInitialActive = chantPreviewPanel.activeSelf;
            chantPreviewSnapshotCaptured = true;
        }

        chantPreviewPanel.SetActive(true);
        chantPreviewBook?.ShowTutorialPreview();
    }

    private void HideChantPreview()
    {
        if (chantPreviewPanel != null)
        {
            chantPreviewPanel.SetActive(false);
        }

        chantPreviewBook?.HideTutorialPreview();
    }

    private void RestoreChantPreview()
    {
        if (chantPreviewSnapshotCaptured && chantPreviewPanel != null)
        {
            chantPreviewPanel.SetActive(chantPreviewInitialActive);
        }

        chantPreviewBook?.HideTutorialPreview();
        chantPreviewSnapshotCaptured = false;
    }

    private void StartManaProfilePreview()
    {
        if (!EnsureManaPreviewStarted())
        {
            return;
        }

        actionFinalFillAmount = 1f;
        isActionPlaying = true;
        actionRoutine = StartCoroutine(PlayManaProfilePreview());
    }

    private IEnumerator PlayManaProfilePreview()
    {
        float warningFill = GetWarningFillAmount();
        float overloadFill = GetOverloadFillAmount();
        float startFill = Mathf.Min(
            manaVisualSnapshot.FillAmount,
            Mathf.Max(0f, warningFill - 0.01f));

        SetTutorialPreviewFill(startFill);
        yield return AnimatePreviewFill(startFill, warningFill, GetSegmentDuration());
        yield return WaitForUnscaledSeconds(GetHoldDuration());
        yield return AnimatePreviewFill(warningFill, overloadFill, GetSegmentDuration());
        yield return WaitForUnscaledSeconds(GetHoldDuration());
        yield return AnimatePreviewFill(overloadFill, 1f, GetSegmentDuration());

        FinishCurrentAction();
    }

    private void PreviewManaSaturationDamage()
    {
        if (EnsureManaPreviewStarted())
        {
            SetTutorialPreviewFill(1f);
        }

        PreviewHeartLoss();
    }

    private void PreviewHeartLoss()
    {
        if (healthPreviewActive)
        {
            return;
        }

        if (heartContainer == null || heartContainer.childCount == 0)
        {
            Debug.LogError(
                "[TutorialDialogue] HeartContainer가 연결되지 않았거나 하트가 없습니다.",
                this);
            return;
        }

        Transform lastHeart = heartContainer.GetChild(heartContainer.childCount - 1);
        if (!lastHeart.TryGetComponent(out previewHeartImage))
        {
            Debug.LogError(
                "[TutorialDialogue] 마지막 하트에서 Image를 찾을 수 없습니다.",
                this);
            return;
        }

        heartVisualSnapshot = new HeartVisualSnapshot
        {
            Sprite = previewHeartImage.sprite,
            Color = previewHeartImage.color
        };

        if (emptyHeartSprite != null)
        {
            previewHeartImage.sprite = emptyHeartSprite;
            previewHeartImage.color = Color.white;
        }
        else
        {
            previewHeartImage.color = new Color32(64, 64, 64, 255);
        }

        healthPreviewActive = true;
    }

    private void StartManaConsumptionPreview()
    {
        if (!EnsureManaPreviewStarted())
        {
            return;
        }

        float targetFill = Mathf.Clamp01(manaVisualSnapshot.FillAmount);
        actionFinalFillAmount = targetFill;
        isActionPlaying = true;
        actionRoutine = StartCoroutine(
            PlayManaConsumptionPreview(1f, targetFill));
    }

    private IEnumerator PlayManaConsumptionPreview(float startFill, float targetFill)
    {
        yield return AnimatePreviewFill(
            startFill,
            targetFill,
            GetConsumptionDuration());

        FinishCurrentAction();
    }

    private IEnumerator AnimatePreviewFill(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetTutorialPreviewFill(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            SetTutorialPreviewFill(Mathf.Lerp(from, to, progress));
            yield return null;
        }

        SetTutorialPreviewFill(to);
    }

    private static IEnumerator WaitForUnscaledSeconds(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private bool EnsureManaPreviewStarted()
    {
        if (manaPreviewActive)
        {
            return true;
        }

        if (manaFillImage == null || portraitImage == null ||
            warningPortrait == null || saturatedPortrait == null)
        {
            Debug.LogError(
                "[TutorialDialogue] 마나 시연용 UI 또는 프로필 스프라이트가 연결되지 않았습니다.",
                this);
            return false;
        }

        RectTransform portraitTransform = portraitImage.rectTransform;
        manaVisualSnapshot = new ManaVisualSnapshot
        {
            FillAmount = manaFillImage.fillAmount,
            Portrait = portraitImage.sprite,
            PortraitPosition = portraitTransform.anchoredPosition,
            PortraitSize = portraitTransform.sizeDelta
        };

        manaPreviewActive = true;
        return true;
    }

    private void CompleteCurrentAction()
    {
        StopCurrentAction();

        if (manaPreviewActive)
        {
            SetTutorialPreviewFill(actionFinalFillAmount);
        }
    }

    private void FinishCurrentAction()
    {
        actionRoutine = null;
        isActionPlaying = false;
    }

    private void StopCurrentAction()
    {
        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }

        isActionPlaying = false;
    }

    private void RestoreManaVisuals()
    {
        if (manaPreviewActive && manaFillImage != null && portraitImage != null)
        {
            manaFillImage.fillAmount = manaVisualSnapshot.FillAmount;
            portraitImage.sprite = manaVisualSnapshot.Portrait;

            RectTransform portraitTransform = portraitImage.rectTransform;
            portraitTransform.anchoredPosition = manaVisualSnapshot.PortraitPosition;
            portraitTransform.sizeDelta = manaVisualSnapshot.PortraitSize;
        }

        manaPreviewActive = false;
    }

    private void RestoreHealthVisuals()
    {
        if (healthPreviewActive && previewHeartImage != null)
        {
            previewHeartImage.sprite = heartVisualSnapshot.Sprite;
            previewHeartImage.color = heartVisualSnapshot.Color;
        }

        previewHeartImage = null;
        healthPreviewActive = false;
    }

    private void SetTutorialPreviewFill(float fillAmount)
    {
        if (!manaPreviewActive || manaFillImage == null || portraitImage == null)
        {
            return;
        }

        float clampedFill = Mathf.Clamp01(fillAmount);
        manaFillImage.fillAmount = clampedFill;

        Sprite nextPortrait;
        Vector2 nextPosition;
        Vector2 nextSize;

        if (clampedFill >= GetOverloadFillAmount())
        {
            nextPortrait = saturatedPortrait;
            nextPosition = saturatedPortraitPosition;
            nextSize = saturatedPortraitSize;
        }
        else if (clampedFill >= GetWarningFillAmount())
        {
            nextPortrait = warningPortrait;
            nextPosition = warningPortraitPosition;
            nextSize = warningPortraitSize;
        }
        else
        {
            nextPortrait = manaVisualSnapshot.Portrait;
            nextPosition = manaVisualSnapshot.PortraitPosition;
            nextSize = manaVisualSnapshot.PortraitSize;
        }

        if (nextPortrait != null)
        {
            portraitImage.sprite = nextPortrait;
        }

        RectTransform portraitTransform = portraitImage.rectTransform;
        portraitTransform.anchoredPosition = nextPosition;
        portraitTransform.sizeDelta = nextSize;
    }

    private float GetWarningFillAmount()
    {
        return Mathf.Clamp01(warningFillAmount);
    }

    private float GetOverloadFillAmount()
    {
        return Mathf.Clamp(overloadFillAmount, GetWarningFillAmount(), 1f);
    }

    private float GetSegmentDuration()
    {
        return manaPreviewSegmentDuration > 0f ? manaPreviewSegmentDuration : 0.75f;
    }

    private float GetHoldDuration()
    {
        return Mathf.Max(0f, manaPreviewHoldDuration);
    }

    private float GetConsumptionDuration()
    {
        return manaConsumptionDuration > 0f ? manaConsumptionDuration : 1.5f;
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
        StopCurrentAction();
        RestoreChantPreview();
        RestoreManaVisuals();
        RestoreHealthVisuals();
        isRunning = false;
        isWaitingForChantEnter = false;
        currentStepIndex = -1;

        if (tutorialArrow != null)
        {
            tutorialArrow.gameObject.SetActive(false);
        }

        inGameManager?.ResumeGameplay();
        gameObject.SetActive(false);
    }
}
