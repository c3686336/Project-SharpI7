using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[Serializable]
public sealed class TutorialDialogueData
{
    public string tutorialId;
    public TutorialDialogueStep[] steps;
    public TutorialDialogueStep[] postBossSteps;
}

[Serializable]
public sealed class TutorialArrowData
{
    public Vector2 position;
    public string direction;
}

[Serializable]
public sealed class TutorialDialogueStep
{
    public string description;
    public string text;
    public TutorialArrowData[] arrows;
    public string action;
    public string expectedChant;
}

[DisallowMultipleComponent]
public sealed class TutorialManager : MonoBehaviour
{
    private const string DefaultFileName = "tutorial.json";
    private const string PreviewManaProfilesAction = "previewManaProfiles";
    private const string PreviewManaSaturationDamageAction = "previewManaSaturationDamage";
    private const string PreviewManaConsumptionAction = "previewManaConsumption";
    private const string WaitForChantEnterAction = "waitForChantEnter";
    private const string PracticeChantAction = "practiceChant";
    private const string AdvanceWithEnterAction = "advanceWithEnter";
    private const string HideChantPreviewAction = "hideChantPreview";
    private const string SpaceToContinueMessage =
        "스페이스바로 튜토리얼을 진행할 수 있습니다.";
    private const string FollowInstructionsMessage =
        "튜토리얼의 지시사항을 확인해주세요.";
    private const string RefocusChantInputMessage =
        "위의 영창 입력칸을 한 번 클릭해주세요.";
    private const int ChantInputBlinkCount = 2;
    private const float ChantInputBlinkFadeDuration = 0.12f;
    private const float ChantInputBlinkHoldDuration = 0.08f;
    private static readonly Color ChantInputBlinkColor =
        new(1f, 0.85f, 0.1f, 0.55f);

    [SerializeField] private string fileName = DefaultFileName;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private RectTransform tutorialArrow;

    [Header("Dialogue Typing")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 35f;
    [SerializeField, Min(0f)] private float firstDialogueDelay = 0.5f;
    [SerializeField, Min(0f)] private float inputHintCooldown = 1.5f;

    [Header("Tutorial Feedback")]
    [SerializeField] private ToastMessageManager toastMessageManager;

    [Header("Arrow Animation")]
    [SerializeField, Min(0f)] private float arrowMoveDistance = 8f;
    [SerializeField, Min(0f)] private float arrowMoveSpeed = 2f;

    [Header("Chant Preview UI")]
    [SerializeField] private GameObject chantPreviewPanel;
    [SerializeField] private BookChantAnimator chantPreviewBook;
    [SerializeField] private ChantManager chantManager;

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
    private readonly List<RectTransform> arrowPool = new();
    private readonly List<Vector2> arrowBasePositions = new();
    private readonly List<Vector2> arrowMoveAxes = new();
    private InGameManager inGameManager;
    private ChantInputField chantPracticeInputField;
    private Graphic chantInputBlinkGraphic;
    private Coroutine actionRoutine;
    private Coroutine chantInputBlinkRoutine;
    private Coroutine chantInputCleanupRoutine;
    private Coroutine chantPracticeRestartRoutine;
    private Coroutine typingRoutine;
    private ManaVisualSnapshot manaVisualSnapshot;
    private HeartVisualSnapshot heartVisualSnapshot;
    private Image previewHeartImage;
    private float actionFinalFillAmount;
    private float arrowAnimationElapsed;
    private Color chantInputBlinkOriginalColor;
    private float nextInputHintTime;
    private float nextChantInputHintTime;
    private int activeArrowCount;
    private int currentStepIndex = -1;
    private int inputBlockedThroughFrame = -1;
    private bool isRunning;
    private bool isActionPlaying;
    private bool isTyping;
    private bool isTypingDelayActive;
    private bool isWaitingForChantEnter;
    private bool isWaitingForChantPractice;
    private bool chantPracticeActive;
    private bool canAdvanceWithEnter;
    private bool chantPreviewSnapshotCaptured;
    private bool chantPreviewInitialActive;
    private bool manaPreviewActive;
    private bool healthPreviewActive;
    private string expectedPracticeChant;

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
        BeginSequence(manager, false);
    }

    public void BeginPostBoss(InGameManager manager)
    {
        BeginSequence(manager, true);
    }

    private void BeginSequence(InGameManager manager, bool usePostBossSteps)
    {
        StopTyping();
        StopCurrentAction();
        StopChantPractice(false);
        HideAllArrows();
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

        if (!TryLoadSteps(usePostBossSteps))
        {
            Finish();
            return;
        }

        currentStepIndex = 0;
        inputBlockedThroughFrame = Time.frameCount;
        isWaitingForChantEnter = false;
        isWaitingForChantPractice = false;
        nextInputHintTime = 0f;
        nextChantInputHintTime = 0f;
        isRunning = true;
        ShowCurrentStep();
    }

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        UpdateArrowAnimation();

        if (Time.frameCount <= inputBlockedThroughFrame)
        {
            return;
        }

        if (isTypingDelayActive)
        {
            if (WasLeftClickPressedThisFrame())
            {
                ShowToastWithCooldown(SpaceToContinueMessage);
            }

            return;
        }

        if (isWaitingForChantPractice)
        {
            HandleChantPracticeInput();
            return;
        }

        if (isTyping && WasLeftClickPressedThisFrame())
        {
            ShowToastWithCooldown(SpaceToContinueMessage);
            return;
        }

        if (isTyping && WasSpacePressedThisFrame())
        {
            CompleteTyping();
            return;
        }

        if (isWaitingForChantEnter)
        {
            HandleChantEnterInput();
            return;
        }

        if (WasLeftClickPressedThisFrame())
        {
            string message = canAdvanceWithEnter || isActionPlaying
                ? FollowInstructionsMessage
                : SpaceToContinueMessage;
            ShowToastWithCooldown(message);
            return;
        }

        if (!isTyping && canAdvanceWithEnter && WasEnterPressedThisFrame())
        {
            AdvanceDialogue();
            return;
        }

        ShowSpaceHintForKeyboardInput();

        if (isTyping || canAdvanceWithEnter || !WasSpacePressedThisFrame())
        {
            return;
        }

        AdvanceDialogue();
    }

    private void AdvanceDialogue()
    {
        if (isActionPlaying)
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

    private bool TryLoadSteps(bool usePostBossSteps)
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
            TutorialDialogueStep[] selectedSteps = usePostBossSteps
                ? data?.postBossSteps
                : data?.steps;

            if (selectedSteps == null || selectedSteps.Length == 0)
            {
                Debug.LogError("[TutorialDialogue] 출력할 튜토리얼 대사가 없습니다.", this);
                return false;
            }

            steps = selectedSteps;
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
        canAdvanceWithEnter = false;
        float typingDelay = currentStepIndex == 0
            ? Mathf.Max(0f, firstDialogueDelay)
            : 0f;

        StartTyping(step.text ?? string.Empty, typingDelay);
        ApplyArrows(step);
        RunStepAction(step);
    }

    private void StartTyping(string text, float delay)
    {
        StopTyping();

        dialogueText.text = text;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        int characterCount = dialogueText.textInfo.characterCount;
        if (characterCount == 0)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        isTyping = true;
        typingRoutine = StartCoroutine(TypeDialogue(characterCount, delay));
    }

    private IEnumerator TypeDialogue(int characterCount, float delay)
    {
        if (delay > 0f)
        {
            isTypingDelayActive = true;
            yield return new WaitForSecondsRealtime(delay);
            isTypingDelayActive = false;
        }

        float visibleCharacterCount = 0f;
        float typingSpeed = Mathf.Max(1f, charactersPerSecond);

        while (visibleCharacterCount < characterCount)
        {
            visibleCharacterCount += typingSpeed * Time.unscaledDeltaTime;
            dialogueText.maxVisibleCharacters = Mathf.Min(
                characterCount,
                Mathf.FloorToInt(visibleCharacterCount));
            yield return null;
        }

        dialogueText.maxVisibleCharacters = int.MaxValue;
        typingRoutine = null;
        isTyping = false;
    }

    private void CompleteTyping()
    {
        if (!isTyping)
        {
            return;
        }

        StopTyping();
        dialogueText.maxVisibleCharacters = int.MaxValue;
    }

    private void StopTyping()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        isTyping = false;
        isTypingDelayActive = false;
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
            case PracticeChantAction:
                StartChantPractice(step);
                break;
            case AdvanceWithEnterAction:
                canAdvanceWithEnter = true;
                break;
            case HideChantPreviewAction:
                StopChantPractice(false);
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
        if (WasEnterPressedThisFrame())
        {
            isWaitingForChantEnter = false;
            ShowChantPreview();
            AdvanceDialogue();
            return;
        }

        if (WasSpacePressedThisFrame() || WasLeftClickPressedThisFrame())
        {
            ShowToastWithCooldown(FollowInstructionsMessage);
        }
    }

    private static bool WasEnterPressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null &&
               (keyboard.enterKey.wasPressedThisFrame ||
                keyboard.numpadEnterKey.wasPressedThisFrame);
    }

    private static bool WasLeftClickPressedThisFrame()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    private static bool WasSpacePressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
    }

    private void ShowSpaceHintForKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (canAdvanceWithEnter || keyboard == null ||
            WasSpacePressedThisFrame() ||
            !keyboard.anyKey.wasPressedThisFrame ||
            toastMessageManager == null || Time.unscaledTime < nextInputHintTime)
        {
            return;
        }

        ShowToastWithCooldown(SpaceToContinueMessage);
    }

    private void ShowToastWithCooldown(string message)
    {
        if (toastMessageManager == null || Time.unscaledTime < nextInputHintTime)
        {
            return;
        }

        toastMessageManager.Show(message);
        nextInputHintTime = Time.unscaledTime + Mathf.Max(0f, inputHintCooldown);
    }

    private void ShowChantInputRefocusHint()
    {
        if (toastMessageManager == null ||
            Time.unscaledTime < nextChantInputHintTime)
        {
            return;
        }

        toastMessageManager.Show(RefocusChantInputMessage);
        nextChantInputHintTime =
            Time.unscaledTime + Mathf.Max(0f, inputHintCooldown);
        StartChantInputBlink();
    }

    private void StartChantPractice(TutorialDialogueStep step)
    {
        StopChantInputBlink();
        StopChantInputCleanup();
        StopChantPracticeRestart();

        if (chantPracticeInputField == null && chantPreviewPanel != null)
        {
            chantPracticeInputField =
                chantPreviewPanel.GetComponentInChildren<ChantInputField>(true);
        }

        if (chantManager == null)
        {
            Debug.LogError(
                "[TutorialDialogue] 영창 연습에 사용할 ChantManager가 연결되지 않았습니다.",
                this);
            return;
        }

        expectedPracticeChant = NormalizeChant(step.expectedChant);
        if (string.IsNullOrEmpty(expectedPracticeChant))
        {
            Debug.LogError(
                "[TutorialDialogue] practiceChant 단계에 expectedChant가 없습니다.",
                this);
            return;
        }

        if (chantManager.IsCasting)
        {
            chantManager.CancelChant();
        }

        chantManager.StartChant();
        chantPracticeActive = chantManager.IsCasting;
        isWaitingForChantPractice = chantPracticeActive;

        if (!chantPracticeActive)
        {
            Debug.LogError(
                "[TutorialDialogue] 영창 연습 모드를 시작하지 못했습니다.",
                chantManager);
        }
    }

    private void HandleChantPracticeInput()
    {
        if (chantPracticeRestartRoutine != null)
        {
            return;
        }

        if (IsChantInputFocused())
        {
            StopChantInputBlink();
        }

        if (WasLeftClickPressedThisFrame())
        {
            if (IsPointerOverChantInputField())
            {
                StopChantInputBlink();
            }
            else
            {
                ShowToastWithCooldown(FollowInstructionsMessage);
            }

            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (!IsChantInputFocused() && keyboard != null &&
            keyboard.anyKey.wasPressedThisFrame)
        {
            ShowChantInputRefocusHint();
            return;
        }

        if (!WasEnterPressedThisFrame())
        {
            return;
        }

        string submittedChant = NormalizeChant(
            chantManager?.CommitImeCompositionAndGetInput());
        if (!string.Equals(
                submittedChant,
                expectedPracticeChant,
                StringComparison.Ordinal))
        {
            RestartChantPractice();
            ShowChantPracticeRetryMessage();
            return;
        }

        StopChantPractice(true);
        inputBlockedThroughFrame = Time.frameCount;
        AdvanceDialogue();
    }

    private bool IsPointerOverChantInputField()
    {
        if (chantPracticeInputField == null && chantPreviewPanel != null)
        {
            chantPracticeInputField =
                chantPreviewPanel.GetComponentInChildren<ChantInputField>(true);
        }

        Mouse mouse = Mouse.current;
        if (chantPracticeInputField == null || mouse == null)
        {
            return false;
        }

        RectTransform inputRect =
            chantPracticeInputField.GetComponent<RectTransform>();
        Canvas inputCanvas = chantPracticeInputField.GetComponentInParent<Canvas>();
        Camera eventCamera = inputCanvas != null &&
                             inputCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? inputCanvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(
            inputRect,
            mouse.position.ReadValue(),
            eventCamera);
    }

    private bool IsChantInputFocused()
    {
        if (chantPracticeInputField == null)
        {
            return false;
        }

        GameObject selectedObject =
            EventSystem.current?.currentSelectedGameObject;
        return chantPracticeInputField.isFocused ||
               selectedObject == chantPracticeInputField.gameObject;
    }

    private void StartChantInputBlink()
    {
        StopChantInputBlink();

        Graphic targetGraphic = chantPracticeInputField?.targetGraphic;
        if (targetGraphic == null)
        {
            return;
        }

        chantInputBlinkGraphic = targetGraphic;
        chantInputBlinkOriginalColor = targetGraphic.color;
        chantInputBlinkRoutine = StartCoroutine(BlinkChantInput(targetGraphic));
    }

    private IEnumerator BlinkChantInput(Graphic targetGraphic)
    {
        Color originalColor = chantInputBlinkOriginalColor;

        for (int i = 0; i < ChantInputBlinkCount; i++)
        {
            yield return AnimateGraphicColor(
                targetGraphic,
                originalColor,
                ChantInputBlinkColor,
                ChantInputBlinkFadeDuration);
            yield return WaitForUnscaledSeconds(ChantInputBlinkHoldDuration);
            yield return AnimateGraphicColor(
                targetGraphic,
                ChantInputBlinkColor,
                originalColor,
                ChantInputBlinkFadeDuration);
            yield return WaitForUnscaledSeconds(ChantInputBlinkHoldDuration);
        }

        targetGraphic.color = originalColor;
        chantInputBlinkRoutine = null;
        chantInputBlinkGraphic = null;
    }

    private static IEnumerator AnimateGraphicColor(
        Graphic targetGraphic,
        Color from,
        Color to,
        float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            targetGraphic.color = Color.Lerp(
                from,
                to,
                Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        targetGraphic.color = to;
    }

    private void StopChantInputBlink()
    {
        if (chantInputBlinkRoutine == null)
        {
            return;
        }

        StopCoroutine(chantInputBlinkRoutine);
        chantInputBlinkRoutine = null;

        if (chantInputBlinkGraphic != null)
        {
            chantInputBlinkGraphic.color = chantInputBlinkOriginalColor;
            chantInputBlinkGraphic = null;
        }
    }

    private void RestartChantPractice()
    {
        if (chantManager == null)
        {
            isWaitingForChantPractice = false;
            chantPracticeActive = false;
            return;
        }

        if (chantManager.IsCasting)
        {
            chantManager.CancelChant();
        }

        chantPracticeActive = false;
        chantPracticeRestartRoutine =
            StartCoroutine(RestartChantPracticeAfterImeEnds());
    }

    private IEnumerator RestartChantPracticeAfterImeEnds()
    {
        while (chantPracticeInputField != null &&
               chantPracticeInputField.HasActiveImeComposition)
        {
            yield return null;
        }

        chantManager.StartChant();
        chantPracticeActive = chantManager.IsCasting;
        isWaitingForChantPractice = chantPracticeActive;
        chantPracticeRestartRoutine = null;
    }

    private void StopChantPracticeRestart()
    {
        if (chantPracticeRestartRoutine == null)
        {
            return;
        }

        StopCoroutine(chantPracticeRestartRoutine);
        chantPracticeRestartRoutine = null;
    }

    private void ShowChantPracticeRetryMessage()
    {
        StopTyping();
        dialogueText.text = $"‘{expectedPracticeChant}’이라고 정확히 입력해 봐.";
        dialogueText.maxVisibleCharacters = int.MaxValue;
    }

    private void StopChantPractice(bool keepPreviewVisible)
    {
        StopChantInputBlink();
        StopChantInputCleanup();
        StopChantPracticeRestart();

        if (chantPracticeActive && chantManager != null && chantManager.IsCasting)
        {
            chantManager.CancelChant();
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        chantPracticeActive = false;
        isWaitingForChantPractice = false;
        expectedPracticeChant = string.Empty;

        if (keepPreviewVisible)
        {
            ShowChantPreview();
            StartChantInputCleanup();
        }
    }

    private void StartChantInputCleanup()
    {
        if (chantPracticeInputField == null && chantPreviewPanel != null)
        {
            chantPracticeInputField =
                chantPreviewPanel.GetComponentInChildren<ChantInputField>(true);
        }

        if (chantPracticeInputField == null)
        {
            return;
        }

        chantPracticeInputField.ClearVisibleText();
        chantInputCleanupRoutine = StartCoroutine(ClearChantInputAfterImeEnds());
    }

    private IEnumerator ClearChantInputAfterImeEnds()
    {
        while (chantPracticeInputField != null &&
               chantPracticeInputField.HasActiveImeComposition)
        {
            chantPracticeInputField.ClearVisibleText();
            yield return null;
        }

        if (chantPracticeInputField != null)
        {
            chantPracticeInputField.ClearVisibleText();
            chantPracticeInputField.ForceLabelUpdate();
        }

        chantInputCleanupRoutine = null;
    }

    private void StopChantInputCleanup()
    {
        if (chantInputCleanupRoutine == null)
        {
            return;
        }

        StopCoroutine(chantInputCleanupRoutine);
        chantInputCleanupRoutine = null;
    }

    private static string NormalizeChant(string chant)
    {
        return string.IsNullOrEmpty(chant)
            ? string.Empty
            : chant.Replace("\u200B", string.Empty).Trim();
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

    private void ApplyArrows(TutorialDialogueStep step)
    {
        HideAllArrows();

        if (tutorialArrow == null)
        {
            return;
        }

        if (step.arrows == null || step.arrows.Length == 0)
        {
            return;
        }

        EnsureArrowPoolInitialized();
        arrowBasePositions.Clear();
        arrowMoveAxes.Clear();
        arrowAnimationElapsed = 0f;

        foreach (TutorialArrowData arrowData in step.arrows)
        {
            if (arrowData == null || string.IsNullOrWhiteSpace(arrowData.direction) ||
                arrowData.direction.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string direction = arrowData.direction.Trim().ToLowerInvariant();
            RectTransform arrow = GetArrowFromPool(activeArrowCount);

            arrowBasePositions.Add(arrowData.position);
            arrowMoveAxes.Add(GetArrowMoveAxis(direction));
            arrow.anchoredPosition = arrowData.position;
            arrow.localRotation = Quaternion.Euler(0f, 0f, GetArrowRotation(direction));
            arrow.gameObject.SetActive(true);
            activeArrowCount++;
        }
    }

    private void UpdateArrowAnimation()
    {
        if (activeArrowCount == 0)
        {
            return;
        }

        arrowAnimationElapsed += Time.unscaledDeltaTime;
        float phase = arrowAnimationElapsed * arrowMoveSpeed * Mathf.PI * 2f;
        float offset = Mathf.Sin(phase) * arrowMoveDistance;

        for (int index = 0; index < activeArrowCount; index++)
        {
            arrowPool[index].anchoredPosition =
                arrowBasePositions[index] + arrowMoveAxes[index] * offset;
        }
    }

    private void EnsureArrowPoolInitialized()
    {
        if (arrowPool.Count == 0 && tutorialArrow != null)
        {
            arrowPool.Add(tutorialArrow);
        }
    }

    private RectTransform GetArrowFromPool(int index)
    {
        EnsureArrowPoolInitialized();

        while (arrowPool.Count <= index)
        {
            RectTransform arrowClone = Instantiate(tutorialArrow, tutorialArrow.parent);
            arrowClone.name = $"{tutorialArrow.name} ({arrowPool.Count + 1})";
            arrowPool.Add(arrowClone);
        }

        return arrowPool[index];
    }

    private void HideAllArrows()
    {
        EnsureArrowPoolInitialized();

        foreach (RectTransform arrow in arrowPool)
        {
            if (arrow != null)
            {
                arrow.gameObject.SetActive(false);
            }
        }

        activeArrowCount = 0;
        arrowBasePositions.Clear();
        arrowMoveAxes.Clear();
    }

    private static Vector2 GetArrowMoveAxis(string direction)
    {
        switch (direction)
        {
            case "up":
                return Vector2.up;
            case "left":
                return Vector2.left;
            case "down":
                return Vector2.down;
            case "right":
            default:
                return Vector2.right;
        }
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
        StopTyping();
        StopCurrentAction();
        StopChantPractice(false);
        RestoreChantPreview();
        RestoreManaVisuals();
        RestoreHealthVisuals();
        isRunning = false;
        isWaitingForChantEnter = false;
        isWaitingForChantPractice = false;
        canAdvanceWithEnter = false;
        currentStepIndex = -1;

        HideAllArrows();

        inGameManager?.ResumeGameplay();
        gameObject.SetActive(false);
    }
}
