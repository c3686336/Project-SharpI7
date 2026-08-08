using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ChantManager : MonoBehaviour, IChantManager
{
    public enum ChantState
    {
        Idle,
        Casting
    }

    [Header("Database")]
    [SerializeField] private ChantDatabase chantDatabase;
    [SerializeField] private string defaultSpellId = "eternal_flame";

    [Header("Dependencies")]
    [SerializeField] private ToastMessageManager toastMessageManager;

    [Header("UI")]
    [SerializeField] private GameObject chantPanel;
    [SerializeField] private TMP_Text spellNameUI;
    [SerializeField] private TMP_Text targetTextUI;
    [SerializeField] private TMP_InputField chantInputField;
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
    private float CurrentManaCost => currentStage?.manaCost ?? 0f;
    public float CurrentMana => playerMana?.CurrentMana ?? 0f;
    public bool HasEnoughMana => playerMana != null && CurrentMana >= CurrentManaCost;
    public float ExpectedDamage => CalculateExpectedDamage();
    public float ActualDamage => CalculateActualDamage();

    public event Action OnChantStarted;
    public event Action OnChantCancelled;
    public event Action OnChantInterrupted;
    public event Action<CastResult> OnChantCast;
    public event Action<ChantPreviewData> OnChantPreviewChanged;

    private ChantSpellData currentSpell;
    private ChantStageData currentStage;
    private string currentInput = "";
    private int correctCount;
    private int typoCount;

    private IPlayerMana playerMana;
    private bool manaSubscribed;

    // 영창 시작 Enter가 같은 프레임에 발동 Enter로 처리되는 것을 방지
    private int chantStartedFrame = -1;

    // TMP onSubmit과 Keyboard.current가 같은 Enter를 중복 처리하는 것을 방지
    private int lastSubmitFrame = -1;

    // 미완성 상태에서 Enter를 누른 후 InputField 포커스 복구용
    private bool refocusInputPending;

    // Physics 콜백에서 TMP UI를 즉시 비활성화하지 않기 위한 지연 Interrupt
    private bool interruptPending;

    private void Awake()
    {
        if (chantInputField != null)
        {
            chantInputField.onValueChanged.AddListener(OnInputChanged);
            chantInputField.onSubmit.AddListener(OnInputSubmitted);
        }

        SetChantUI(false);
    }

    private void OnEnable()
    {
        SubscribeToMana();
    }

    private void OnDisable()
    {
        UnsubscribeFromMana();
    }

    private void Start()
    {
        if (chantDatabase == null)
        {
            Debug.LogError("[ChantManager] ChantDatabase가 연결되지 않았습니다.", this);
            return;
        }

        if (!SetSpell(defaultSpellId))
        {
            Debug.LogError($"[ChantManager] 기본 주문 로드 실패: {defaultSpellId}", this);
        }

        UpdateUI();
    }

    private void Update()
    {
        if (State != ChantState.Casting)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool enterPressed = keyboard.enterKey.wasPressedThisFrame ||
                            keyboard.numpadEnterKey.wasPressedThisFrame;

        if (enterPressed)
        {
            SubmitChantOnce();
        }
    }

    private void LateUpdate()
    {
        if (interruptPending)
        {
            interruptPending = false;

            if (State == ChantState.Casting)
            {
                ResetChant();
                OnChantInterrupted?.Invoke();
            }
        }

        if (!refocusInputPending)
            return;

        refocusInputPending = false;

        if (State != ChantState.Casting || chantInputField == null)
            return;

        chantInputField.Select();
        chantInputField.ActivateInputField();
    }

    private void OnDestroy()
    {
        if (chantInputField != null)
        {
            chantInputField.onValueChanged.RemoveListener(OnInputChanged);
            chantInputField.onSubmit.RemoveListener(OnInputSubmitted);
        }
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
        {
            SubscribeToMana();
        }

        UpdateUI();

        if (IsCasting)
        {
            NotifyPreviewChanged();
        }
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

    private void HandleManaStatusChanged(ManaStatus _)
    {
        if (!IsCasting)
            return;

        UpdateUI();
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
        refocusInputPending = false;
        State = ChantState.Casting;
        chantStartedFrame = Time.frameCount;
        lastSubmitFrame = -1;

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
        if (State != ChantState.Casting || interruptPending)
            return;

        interruptPending = true;
    }

    public CastResult ResolveChant()
    {
        if (State != ChantState.Casting)
            return default;

        EvaluateInput();
        CastResult result = CreateCastResult();

        // 오타가 있으면 Enter로 즉시 영창 취소
        if (result.typoCount > 0)
        {
            CancelChant();
            return result;
        }

        // 아직 현재 단계를 끝까지 입력하지 않았다면 영창 유지
        if (!result.canCast)
        {
            return result;
        }

        // 마나가 부족하면 메시지를 표시하고 영창 종료
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

    private void OnInputSubmitted(string _)
    {
        SubmitChantOnce();
    }

    private void SubmitChantOnce()
    {
        if (State != ChantState.Casting)
            return;

        // 영창 시작에 사용된 Enter는 발동 Enter로 사용하지 않음
        if (Time.frameCount == chantStartedFrame)
            return;

        // TMP onSubmit과 Keyboard.current가 같은 Enter를 잡아도 한 번만 처리
        if (lastSubmitFrame == Time.frameCount)
            return;

        lastSubmitFrame = Time.frameCount;

        ResolveChant();

        // 미완성 상태라 영창이 유지됐다면 입력 포커스 복구
        if (State == ChantState.Casting)
        {
            refocusInputPending = true;
        }
    }

    private void ShowInsufficientManaFeedback()
    {
        string message = $"마나가 부족합니다. 현재 {CurrentMana:0.#} / 필요 {CurrentManaCost:0.#}";

        if (toastMessageManager != null)
        {
            toastMessageManager.Show(message);
            return;
        }

        Debug.LogWarning(message, this);
    }

    private void OnInputChanged(string value)
    {
        if (State != ChantState.Casting)
            return;

        currentInput = value ?? "";
        currentStage = FindBestMatchingStage(currentInput);

        EvaluateInput();
        UpdateUI();
        NotifyPreviewChanged();
    }

    private ChantStageData FindBestMatchingStage(string input)
    {
        if (currentSpell == null || currentSpell.stages == null || currentSpell.stages.Count == 0)
            return null;

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
                {
                    stageCorrectCount++;
                }
            }

            float accuracy = compareLength > 0
                ? (float)stageCorrectCount / compareLength
                : 0f;

            int lengthDifference = Mathf.Abs(target.Length - input.Length);
            bool better = false;

            if (stageCorrectCount > bestCorrectCount)
            {
                better = true;
            }
            else if (stageCorrectCount == bestCorrectCount && accuracy > bestAccuracy)
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
            {
                correctCount++;
            }
            else
            {
                typoCount++;
            }
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
            canResolve = CanCastCurrentStage() && TypoCount == 0 && HasEnoughMana
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
            castLevel = canCast && currentStage != null ? currentStage.chantLevel : 0,
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
        {
            correctCountUI.text = $"정상 입력 : {correctCount}";
        }

        if (manaCostUI != null)
        {
            manaCostUI.text = $"마나 : {CurrentMana:0.#} / 필요 : {CurrentManaCost:0.#}";
        }
    }

    private void UpdateSpellNameUI()
    {
        if (spellNameUI == null)
            return;

        spellNameUI.text = currentSpell?.spellName ?? "";
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

        if (currentStage == null)
        {
            targetTextUI.text = BuildUnenteredText(currentSpell.fullChantText);
            return;
        }

        string target = currentStage.chantText;
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < target.Length; i++)
        {
            char targetCharacter = target[i];

            if (i >= currentInput.Length)
            {
                AppendColoredCharacter(builder, targetCharacter, "#888888");
                continue;
            }

            if (currentInput[i] == targetCharacter)
            {
                AppendColoredCharacter(builder, targetCharacter, "#00FF88");
            }
            else
            {
                AppendColoredCharacter(builder, targetCharacter, "#FF4444");
            }
        }

        if (currentInput.Length > target.Length)
        {
            for (int i = target.Length; i < currentInput.Length; i++)
            {
                AppendColoredCharacter(builder, currentInput[i], "#FF4444");
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
            AppendColoredCharacter(builder, character, "#888888");
        }

        return builder.ToString();
    }

    private void AppendColoredCharacter(StringBuilder builder, char character, string color)
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
        {
            chantPanel.SetActive(active);
        }
    }

    private void ClearRuntimeData()
    {
        currentInput = "";
        currentStage = null;
        correctCount = 0;
        typoCount = 0;
    }

    private void ResetChant()
    {
        interruptPending = false;
        refocusInputPending = false;

        State = ChantState.Idle;

        ClearRuntimeData();

        chantStartedFrame = -1;
        lastSubmitFrame = -1;

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