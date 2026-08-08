using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class ChantManager : MonoBehaviour, IChantManager
{
    public enum ChantState
    {
        Idle,
        Casting
    }

    [Header("Database")]
    [SerializeField] private ChantDatabase chantDatabase;
    [SerializeField] private string defaultSpellId = "dark_flame";

    [Header("Dependencies")]
    [SerializeField] private ToastMessageManager toastMessageManager;

    [Header("UI")]
    [SerializeField] private GameObject chantPanel;
    [SerializeField] private TMP_Text spellNameUI;
    [SerializeField] private TMP_Text targetTextUI;
    [SerializeField] private ChantInputField chantInputField;
    [SerializeField] private TMP_Text correctCountUI;
    [SerializeField] private TMP_Text manaCostUI;

    public ChantState State { get; private set; } = ChantState.Idle;
    public bool IsCasting => State == ChantState.Casting;
    public ChantSpellData CurrentSpell => currentSpell;
    public ChantStageData CurrentStage => currentStage;
    public string CurrentInput => currentInput;
    public int CorrectCount => correctCount;
    public int TypoCount => typoCount;
    public int ChantLevel => currentStage?.chantLevel ?? 0;
    public float CurrentMana => playerMana?.CurrentMana ?? 0f;
    public bool HasEnoughMana => playerMana != null && CurrentMana >= CurrentManaCost;
    public float ExpectedDamage => CalculateExpectedDamage();
    public float ActualDamage => CalculateActualDamage();

    private float CurrentManaCost => currentStage?.manaCost ?? 0f;

    public event Action OnChantStarted;
    public event Action OnChantCancelled;
    public event Action OnChantInterrupted;
    public event Action<CastResult> OnChantCast;
    public event Action<ChantPreviewData> OnChantPreviewChanged;

    private ChantSpellData currentSpell;
    private ChantStageData currentStage;

    private string currentInput = "";
    private string imeComposition = "";

    private int correctCount;
    private int typoCount;

    private IPlayerMana playerMana;
    private bool manaSubscribed;

    private int chantStartedFrame = -1;

    private bool resolvePending;
    private int resolveRequestedFrame = -1;

    private bool interruptPending;

    private void Awake()
    {
        SetChantUI(false);
    }

    private void OnEnable()
    {
        SubscribeToMana();

        if (chantInputField != null)
        {
            chantInputField.onValueChanged.AddListener(OnInputChanged);

            if (Keyboard.current != null)
                Keyboard.current.onIMECompositionChange += OnImeChanged;
        }
        else
        {
            Debug.LogWarning("[ChantManager] chantInputField가 연결되지 않았습니다.");
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromMana();

        if (chantInputField != null)
        {
            chantInputField.onValueChanged.RemoveListener(OnInputChanged);

            if (Keyboard.current != null)
                Keyboard.current.onIMECompositionChange -= OnImeChanged;
        }
    }

    private void Start()
    {
        if (chantDatabase == null)
        {
            Debug.LogError("[ChantManager] ChantDatabase가 연결되지 않았습니다.");
            return;
        }

        if (!SetSpell(defaultSpellId))
            Debug.LogError($"[ChantManager] 기본 주문 로드 실패: {defaultSpellId}");

        UpdateUI();
    }

    private void Update()
    {
        if (State != ChantState.Casting)
            return;

        RefreshInputState();

        if (resolvePending && Time.frameCount > resolveRequestedFrame)
        {
            resolvePending = false;
            resolveRequestedFrame = -1;

            RefreshInputState();
            ResolveChant();
            return;
        }

        if (Time.frameCount == chantStartedFrame)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        bool enterPressed =
            keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame;

        if (enterPressed)
        {
            resolvePending = true;
            resolveRequestedFrame = Time.frameCount;
        }
    }

    private void LateUpdate()
    {
        if (!interruptPending)
            return;

        interruptPending = false;

        if (State != ChantState.Casting)
            return;

        ResetChant();
        OnChantInterrupted?.Invoke();
    }

    private void OnDestroy()
    {
        if (chantInputField != null)
            chantInputField.onValueChanged.RemoveListener(OnInputChanged);

        if (Keyboard.current != null)
            Keyboard.current.onIMECompositionChange -= OnImeChanged;
    }

    public bool SetSpell(string spellId)
    {
        if (State == ChantState.Casting)
        {
            Debug.LogWarning("[ChantManager] 영창 중에는 주문을 변경할 수 없습니다.");
            return false;
        }

        if (chantDatabase == null)
        {
            Debug.LogError("[ChantManager] ChantDatabase가 없습니다.");
            return false;
        }

        ChantSpellData spell = chantDatabase.GetSpell(spellId);

        if (spell == null)
            return false;

        if (spell.stages == null || spell.stages.Count == 0)
        {
            Debug.LogError($"[ChantManager] 영창 단계가 없습니다: {spellId}");
            return false;
        }

        currentSpell = spell;

        ClearRuntimeData();
        UpdateUI();

        return true;
    }

    public void SetManaSource(IPlayerMana manaSource)
    {
        UnsubscribeFromMana();

        playerMana = manaSource;

        if (isActiveAndEnabled)
            SubscribeToMana();

        UpdateUI();

        if (IsCasting)
            NotifyPreviewChanged();
    }

    public void StartChant()
    {
        if (State == ChantState.Casting)
            return;

        if (currentSpell == null)
        {
            Debug.LogWarning("[ChantManager] 선택된 주문이 없습니다.");
            return;
        }

        ClearRuntimeData();

        interruptPending = false;
        resolvePending = false;
        resolveRequestedFrame = -1;

        State = ChantState.Casting;
        chantStartedFrame = Time.frameCount;

        SetChantUI(true);

        if (chantInputField != null)
        {
            chantInputField.SetTextWithoutNotify("");
            chantInputField.interactable = true;
            chantInputField.Select();
            chantInputField.ActivateInputField();
        }

        UpdateUI();
        NotifyPreviewChanged();
        OnChantStarted?.Invoke();
    }

    public void CancelChant()
    {
        if (State != ChantState.Casting)
            return;

        ResetChant();
        OnChantCancelled?.Invoke();
    }

    public void InterruptChant()
    {
        if (State != ChantState.Casting)
            return;

        if (interruptPending)
            return;

        interruptPending = true;
    }

    public CastResult ResolveChant()
    {
        if (State != ChantState.Casting)
            return default;

        RefreshInputState();
        EvaluateInput();

        CastResult result = CreateCastResult();

        if (!result.completed)
        {
            CancelChant();
            return result;
        }

        if (!HasEnoughMana)
        {
            ShowInsufficientManaFeedback();
            CancelChant();
            return result;
        }

        ResetChant();
        OnChantCast?.Invoke(result);

        return result;
    }

    private void ShowInsufficientManaFeedback()
    {
        string message =
            $"마나가 부족합니다. 현재 {CurrentMana:0.#} / 필요 {CurrentManaCost:0.#}";

        if (toastMessageManager != null)
        {
            toastMessageManager.Show(message);
            return;
        }

        Debug.LogWarning(message, this);
    }

    private void HandleManaStatusChanged(ManaStatus _)
    {
        if (!IsCasting)
            return;

        UpdateUI();
        NotifyPreviewChanged();
    }

    private void SubscribeToMana()
    {
        if (manaSubscribed || playerMana == null)
            return;

        playerMana.ManaStatusChanged += HandleManaStatusChanged;
        manaSubscribed = true;
    }

    private void UnsubscribeFromMana()
    {
        if (!manaSubscribed || playerMana == null)
            return;

        playerMana.ManaStatusChanged -= HandleManaStatusChanged;
        manaSubscribed = false;
    }

    private void OnImeChanged(IMECompositionString composition)
    {
        imeComposition = composition.ToString();
        RefreshInputState();
    }

    private void OnInputChanged(string value)
    {
        if (State != ChantState.Casting)
            return;

        RefreshInputState();
    }

    private void RefreshInputState()
    {
        if (State != ChantState.Casting || chantInputField == null)
            return;

        string actualInput;

        if (string.IsNullOrEmpty(imeComposition))
        {
            actualInput = chantInputField.text ?? "";
        }
        else
        {
            actualInput = chantInputField.GetActualText(imeComposition);
        }

        if (actualInput == currentInput)
            return;

        currentInput = actualInput;
        currentStage = FindBestMatchingStage(currentInput);

        EvaluateInput();
        UpdateUI();
        NotifyPreviewChanged();
    }

    private ChantStageData FindBestMatchingStage(string input)
    {
        if (currentSpell == null ||
            currentSpell.stages == null ||
            currentSpell.stages.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrEmpty(input))
            return null;

        ChantStageData bestStage = null;
        int bestCorrectCount = -1;
        float bestAccuracy = -1f;
        int bestLengthDifference = int.MaxValue;

        foreach (ChantStageData stage in currentSpell.stages)
        {
            if (stage == null || string.IsNullOrEmpty(stage.chantText))
                continue;

            string target = stage.chantText;
            int compareLength = Mathf.Min(input.Length, target.Length);
            int stageCorrectCount = 0;

            for (int i = 0; i < compareLength; i++)
            {
                if (input[i] == target[i])
                    stageCorrectCount++;
            }

            float accuracy =
                compareLength > 0
                    ? (float)stageCorrectCount / compareLength
                    : 0f;

            int lengthDifference = Mathf.Abs(target.Length - input.Length);

            bool better = false;

            if (stageCorrectCount > bestCorrectCount)
            {
                better = true;
            }
            else if (stageCorrectCount == bestCorrectCount &&
                     accuracy > bestAccuracy)
            {
                better = true;
            }
            else if (stageCorrectCount == bestCorrectCount &&
                     Mathf.Approximately(accuracy, bestAccuracy) &&
                     lengthDifference < bestLengthDifference)
            {
                better = true;
            }

            if (!better)
                continue;

            bestStage = stage;
            bestCorrectCount = stageCorrectCount;
            bestAccuracy = accuracy;
            bestLengthDifference = lengthDifference;
        }

        return bestStage;
    }

    private void EvaluateInput()
    {
        correctCount = 0;
        typoCount = 0;

        if (currentStage == null)
            return;

        string target = currentStage.chantText;

        for (int i = 0; i < currentInput.Length; i++)
        {
            if (i >= target.Length)
            {
                typoCount++;
                continue;
            }

            if (currentInput[i] == target[i])
                correctCount++;
            else
                typoCount++;
        }
    }

    private bool CanCastCurrentStage()
    {
        if (currentStage == null)
            return false;

        return currentInput.Length == currentStage.chantText.Length;
    }

    private bool IsPerfectChant()
    {
        return CanCastCurrentStage() && typoCount == 0;
    }

    private float CalculateExpectedDamage()
    {
        if (currentSpell == null || currentStage == null)
            return 0f;

        return currentSpell.baseDamage * currentStage.damageMultiplier;
    }

    private float CalculateActualDamage()
    {
        if (currentSpell == null || currentStage == null)
            return 0f;

        if (typoCount > 0)
            return 0f;

        return CalculateExpectedDamage();
    }

    private void NotifyPreviewChanged()
    {
        ChantPreviewData preview = new ChantPreviewData
        {
            chantLevel = ChantLevel,
            expectedDamage = ExpectedDamage,
            actualDamage = ActualDamage,
            manaCost = CurrentManaCost,
            currentMana = CurrentMana,
            correctCount = CorrectCount,
            typoCount = TypoCount,
            hasEnoughMana = HasEnoughMana,
            canResolve =
                CanCastCurrentStage() &&
                TypoCount == 0 &&
                HasEnoughMana
        };

        OnChantPreviewChanged?.Invoke(preview);
    }

    private CastResult CreateCastResult()
    {
        bool canCast = CanCastCurrentStage();

        return new CastResult
        {
            spellId = currentSpell?.id ?? "",
            spellName = currentSpell?.spellName ?? "",
            targetText = currentStage?.chantText ?? "",
            typedText = currentInput,
            correctCount = correctCount,
            typoCount = typoCount,
            castLevel =
                canCast && currentStage != null
                    ? currentStage.chantLevel
                    : 0,
            expectedDamage = CalculateExpectedDamage(),
            actualDamage = CalculateActualDamage(),
            penaltyMultiplier = typoCount == 0 ? 1f : 0f,
            manaCost = currentStage?.manaCost ?? 0f,
            magicType = currentSpell?.magicType ?? "",
            effectId = currentSpell?.effectId ?? "",
            canCast = canCast,
            completed = IsPerfectChant()
        };
    }

    private void UpdateUI()
    {
        UpdateSpellNameUI();
        UpdateTargetTextUI();

        if (correctCountUI != null)
            correctCountUI.text = $"정상 입력 : {correctCount}";

        if (manaCostUI != null)
        {
            manaCostUI.text =
                $"마나 : {CurrentMana:0.#} / 필요 : {CurrentManaCost:0.#}";
        }
    }

    private void UpdateSpellNameUI()
    {
        if (spellNameUI == null)
            return;

        spellNameUI.text =
            currentSpell == null
                ? ""
                : currentSpell.spellName;
    }

    private void UpdateTargetTextUI()
    {
        if (targetTextUI == null)
            return;

        if (currentSpell == null)
        {
            targetTextUI.text = "";
            return;
        }

        ChantStageData displayStage = currentStage;

        if (displayStage == null)
        {
            if (currentSpell.stages == null ||
                currentSpell.stages.Count == 0)
            {
                targetTextUI.text = "";
                return;
            }

            displayStage = currentSpell.stages[0];
        }

        string target = displayStage.chantText;

        if (string.IsNullOrEmpty(target))
        {
            targetTextUI.text = "";
            return;
        }

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < target.Length; i++)
        {
            char targetCharacter = target[i];

            if (i >= currentInput.Length)
            {
                AppendColoredCharacter(
                    builder,
                    targetCharacter,
                    "#888888"
                );

                continue;
            }

            if (currentInput[i] == targetCharacter)
            {
                AppendColoredCharacter(
                    builder,
                    targetCharacter,
                    "#00FF88"
                );
            }
            else
            {
                AppendColoredCharacter(
                    builder,
                    targetCharacter,
                    "#FF4444"
                );
            }
        }

        if (currentInput.Length > target.Length)
        {
            for (int i = target.Length; i < currentInput.Length; i++)
            {
                AppendColoredCharacter(
                    builder,
                    currentInput[i],
                    "#FF4444"
                );
            }
        }

        targetTextUI.text = builder.ToString();
    }

    private string BuildUnenteredText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        StringBuilder builder = new StringBuilder();

        foreach (char character in text)
        {
            AppendColoredCharacter(
                builder,
                character,
                "#888888"
            );
        }

        return builder.ToString();
    }

    private void AppendColoredCharacter(
        StringBuilder builder,
        char character,
        string color
    )
    {
        builder.Append("<color=");
        builder.Append(color);
        builder.Append(">");
        builder.Append(EscapeRichTextCharacter(character));
        builder.Append("</color>");
    }

    private string EscapeRichTextCharacter(char character)
    {
        return character switch
        {
            '<' => "&lt;",
            '>' => "&gt;",
            '&' => "&amp;",
            _ => character.ToString()
        };
    }

    private void SetChantUI(bool active)
    {
        if (chantPanel != null)
            chantPanel.SetActive(active);
    }

    private void ClearRuntimeData()
    {
        currentInput = "";
        imeComposition = "";
        currentStage = null;
        correctCount = 0;
        typoCount = 0;
    }

    private void ResetChant()
    {
        interruptPending = false;
        resolvePending = false;
        resolveRequestedFrame = -1;

        State = ChantState.Idle;

        ClearRuntimeData();

        chantStartedFrame = -1;

        if (chantInputField != null)
        {
            chantInputField.DeactivateInputField();
            chantInputField.SetTextWithoutNotify("");
        }

        SetChantUI(false);
        UpdateUI();
    }

#if UNITY_EDITOR

    [ContextMenu("Debug / Start Chant")]
    private void DebugStartChant()
    {
        StartChant();
    }

    [ContextMenu("Debug / Cast Chant")]
    private void DebugCastChant()
    {
        CastResult result = ResolveChant();

        Debug.Log(
            $"[Chant Result]\n" +
            $"Spell: {result.spellName}\n" +
            $"Target: {result.targetText}\n" +
            $"Input: {result.typedText}\n" +
            $"Correct: {result.correctCount}\n" +
            $"Typo: {result.typoCount}\n" +
            $"Level: {result.castLevel}\n" +
            $"Mana Cost: {result.manaCost}\n" +
            $"Expected Damage: {result.expectedDamage}\n" +
            $"Actual Damage: {result.actualDamage}\n" +
            $"Can Cast: {result.canCast}\n" +
            $"Perfect: {result.completed}"
        );
    }

    [ContextMenu("Debug / Cancel Chant")]
    private void DebugCancelChant()
    {
        CancelChant();
    }

    [ContextMenu("Debug / Interrupt Chant")]
    private void DebugInterruptChant()
    {
        InterruptChant();
    }

#endif
}