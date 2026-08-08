using System;
using System.Collections.Generic;
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

    [Header("Unlocked Spells")]
    [SerializeField] private List<string> initialUnlockedSpellIds = new List<string> { "eternal_flame" };

    [Header("Dependencies")]
    [SerializeField] private ToastMessageManager toastMessageManager;

    [Header("UI")]
    [SerializeField] private GameObject chantPanel;
    [SerializeField] private TMP_Text targetTextUI;
    [SerializeField] private ChantInputField chantInputField;
    [SerializeField] private TMP_Text correctCountUI;
    [SerializeField] private TMP_Text manaCostUI;
    [SerializeField] private bool hideNonCandidateSpells;

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
    public event Action<string> OnSpellUnlocked;

    private readonly HashSet<string> unlockedSpellIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> candidateSpellIds = new HashSet<string>(StringComparer.Ordinal);

    private ChantSpellData currentSpell;
    private ChantStageData currentStage;
    private string currentInput = "";
    private int correctCount;
    private int typoCount;
    private IPlayerMana playerMana;
    private bool manaSubscribed;
    private bool interruptPending;
    private bool hasAmbiguousExactMatch;
    private int chantStartedFrame = -1;
    private Keyboard imeKeyboard;

    private const string NormalColor = "#FFFFFF";
    private const string ContextColor = "#AAAAAA";
    private const string DimColor = "#555555";
    private const string CorrectColor = "#00FF88";
    private const string TypoColor = "#FF4444";

    private void Awake()
    {
        foreach (string spellId in initialUnlockedSpellIds)
        {
            if (!string.IsNullOrWhiteSpace(spellId))
            {
                unlockedSpellIds.Add(spellId);
            }
        }

        SetChantUI(false);
    }

    private void OnEnable()
    {
        SubscribeToMana();
        SubscribeToInput();
    }

    private void OnDisable()
    {
        UnsubscribeFromMana();
        UnsubscribeFromInput();
    }

    private void Start()
    {
        if (chantDatabase == null)
        {
            Debug.LogError("[ChantManager] ChantDatabase가 연결되지 않았습니다.");
            return;
        }

        if (!chantDatabase.IsLoaded || chantDatabase.Spells.Count == 0)
        {
            Debug.LogError("[ChantManager] 사용할 수 있는 영창 데이터가 없습니다.");
            return;
        }

        ValidateUnlockedSpells();
        UpdateUI();
    }

    private void Update()
    {
        if (InGameManager.GameplayInputBlocked || State != ChantState.Casting)
            return;

        if (Time.frameCount == chantStartedFrame)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        bool enterPressed = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;

        if (enterPressed)
        {
            ResolveChant();
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
        UnsubscribeFromInput();
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

    public bool UnlockSpell(string spellId)
    {
        if (string.IsNullOrWhiteSpace(spellId))
            return false;

        if (chantDatabase != null && chantDatabase.IsLoaded && chantDatabase.GetSpell(spellId) == null)
        {
            Debug.LogWarning($"[ChantManager] 존재하지 않는 주문은 획득할 수 없습니다: {spellId}");
            return false;
        }

        if (unlockedSpellIds.Contains(spellId))
            return true;

        unlockedSpellIds.Add(spellId);

        if (IsCasting)
        {
            RefreshSpellRecognition();
            EvaluateInput();
        }

        UpdateUI();

        if (IsCasting)
        {
            NotifyPreviewChanged();
        }

        OnSpellUnlocked?.Invoke(spellId);
        return true;
    }

    public bool IsSpellUnlocked(string spellId)
    {
        if (string.IsNullOrWhiteSpace(spellId))
            return false;

        return unlockedSpellIds.Contains(spellId);
    }

    public void StartChant()
    {
        if (State == ChantState.Casting)
            return;

        if (chantDatabase == null || !chantDatabase.IsLoaded)
        {
            Debug.LogWarning("[ChantManager] 영창 데이터가 준비되지 않았습니다.");
            return;
        }

        if (!HasUnlockedSpell())
        {
            Debug.LogWarning("[ChantManager] 획득한 주문이 없습니다.");
            return;
        }

        ClearRuntimeData();
        interruptPending = false;
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

        RefreshSpellRecognition();
        EvaluateInput();

        CastResult result = CreateCastResult();

        if (hasAmbiguousExactMatch)
        {
            ShowAmbiguousChantFeedback();
            CancelChant();
            return result;
        }

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

    private void SubscribeToInput()
    {
        if (chantInputField != null)
        {
            chantInputField.onValueChanged.RemoveListener(OnInputChanged);
            chantInputField.onValueChanged.AddListener(OnInputChanged);
        }
        else
        {
            Debug.LogWarning("[ChantManager] chantInputField가 연결되지 않았습니다.");
        }

        if (imeKeyboard != null)
        {
            imeKeyboard.onIMECompositionChange -= OnImeChanged;
        }

        imeKeyboard = Keyboard.current;

        if (imeKeyboard != null)
        {
            imeKeyboard.onIMECompositionChange += OnImeChanged;
        }
    }

    private void UnsubscribeFromInput()
    {
        if (chantInputField != null)
        {
            chantInputField.onValueChanged.RemoveListener(OnInputChanged);
        }

        if (imeKeyboard != null)
        {
            imeKeyboard.onIMECompositionChange -= OnImeChanged;
            imeKeyboard = null;
        }
    }

    private void OnImeChanged(IMECompositionString composition)
    {
        if (State != ChantState.Casting || chantInputField == null)
            return;

        string composing = composition.ToString();

        if (string.IsNullOrEmpty(composing))
            return;

        OnInputChanged(chantInputField.GetActualText(composing));
    }

    private void OnInputChanged(string value)
    {
        if (State != ChantState.Casting)
            return;

        currentInput = value ?? "";
        RefreshSpellRecognition();
        EvaluateInput();
        UpdateUI();
        NotifyPreviewChanged();
    }

    private void RefreshSpellRecognition()
    {
        currentSpell = null;
        currentStage = null;
        hasAmbiguousExactMatch = false;
        candidateSpellIds.Clear();

        if (chantDatabase == null || string.IsNullOrEmpty(currentInput))
            return;

        ChantSpellData exactSpell = null;
        ChantStageData exactStage = null;
        int exactMatchCount = 0;

        foreach (ChantSpellData spell in chantDatabase.Spells)
        {
            if (!IsUsableSpell(spell))
                continue;

            foreach (ChantStageData stage in spell.stages)
            {
                if (stage == null || string.IsNullOrEmpty(stage.chantText))
                    continue;

                if (!string.Equals(currentInput, stage.chantText, StringComparison.Ordinal))
                    continue;

                exactMatchCount++;
                exactSpell = spell;
                exactStage = stage;
                candidateSpellIds.Add(spell.id);
            }
        }

        if (exactMatchCount > 0)
        {
            if (exactMatchCount == 1)
            {
                currentSpell = exactSpell;
                currentStage = exactStage;
            }
            else
            {
                hasAmbiguousExactMatch = true;
            }

            return;
        }

        ChantSpellData onlyCandidateSpell = null;
        ChantStageData onlyCandidateStage = null;
        int candidateSpellCount = 0;

        foreach (ChantSpellData spell in chantDatabase.Spells)
        {
            if (!IsUsableSpell(spell))
                continue;

            ChantStageData prefixStage = FindBestPrefixStage(spell, currentInput);

            if (prefixStage == null)
                continue;

            candidateSpellIds.Add(spell.id);
            candidateSpellCount++;

            if (candidateSpellCount == 1)
            {
                onlyCandidateSpell = spell;
                onlyCandidateStage = prefixStage;
            }
        }

        if (candidateSpellCount == 1)
        {
            currentSpell = onlyCandidateSpell;
            currentStage = onlyCandidateStage;
            return;
        }

        if (candidateSpellCount > 1)
            return;

        FindBestApproximateMatch(currentInput, out ChantSpellData approximateSpell, out ChantStageData approximateStage);

        if (approximateSpell != null && approximateStage != null)
        {
            currentSpell = approximateSpell;
            currentStage = approximateStage;
            candidateSpellIds.Add(approximateSpell.id);
        }
    }

    private ChantStageData FindBestPrefixStage(ChantSpellData spell, string input)
    {
        if (spell == null || spell.stages == null || string.IsNullOrEmpty(input))
            return null;

        ChantStageData bestStage = null;
        int bestRemainingLength = int.MaxValue;
        int bestLevel = -1;

        foreach (ChantStageData stage in spell.stages)
        {
            if (stage == null || string.IsNullOrEmpty(stage.chantText))
                continue;

            if (!stage.chantText.StartsWith(input, StringComparison.Ordinal))
                continue;

            int remainingLength = stage.chantText.Length - input.Length;

            if (remainingLength < bestRemainingLength)
            {
                bestStage = stage;
                bestRemainingLength = remainingLength;
                bestLevel = stage.chantLevel;
            }
            else if (remainingLength == bestRemainingLength && stage.chantLevel > bestLevel)
            {
                bestStage = stage;
                bestLevel = stage.chantLevel;
            }
        }

        return bestStage;
    }

    private void FindBestApproximateMatch(string input, out ChantSpellData bestSpell, out ChantStageData bestStage)
    {
        bestSpell = null;
        bestStage = null;

        int bestPrefixCount = -1;
        int bestCorrectCount = -1;
        int bestWrongCount = int.MaxValue;
        int bestLengthDifference = int.MaxValue;

        foreach (ChantSpellData spell in chantDatabase.Spells)
        {
            if (!IsUsableSpell(spell))
                continue;

            foreach (ChantStageData stage in spell.stages)
            {
                if (stage == null || string.IsNullOrEmpty(stage.chantText))
                    continue;

                string target = stage.chantText;
                int compareLength = Mathf.Min(input.Length, target.Length);
                int prefixCount = 0;
                int correctCountForStage = 0;
                int wrongCount = 0;
                bool prefixBroken = false;

                for (int i = 0; i < compareLength; i++)
                {
                    if (input[i] == target[i])
                    {
                        correctCountForStage++;

                        if (!prefixBroken)
                        {
                            prefixCount++;
                        }
                    }
                    else
                    {
                        wrongCount++;
                        prefixBroken = true;
                    }
                }

                if (input.Length > target.Length)
                {
                    wrongCount += input.Length - target.Length;
                }

                int lengthDifference = Mathf.Abs(input.Length - target.Length);
                bool better = false;

                if (prefixCount > bestPrefixCount)
                {
                    better = true;
                }
                else if (prefixCount == bestPrefixCount && correctCountForStage > bestCorrectCount)
                {
                    better = true;
                }
                else if (prefixCount == bestPrefixCount && correctCountForStage == bestCorrectCount && wrongCount < bestWrongCount)
                {
                    better = true;
                }
                else if (prefixCount == bestPrefixCount && correctCountForStage == bestCorrectCount && wrongCount == bestWrongCount && lengthDifference < bestLengthDifference)
                {
                    better = true;
                }

                if (!better)
                    continue;

                bestSpell = spell;
                bestStage = stage;
                bestPrefixCount = prefixCount;
                bestCorrectCount = correctCountForStage;
                bestWrongCount = wrongCount;
                bestLengthDifference = lengthDifference;
            }
        }

        if (bestCorrectCount <= 0)
        {
            bestSpell = null;
            bestStage = null;
        }
    }

    private bool IsUsableSpell(ChantSpellData spell)
    {
        if (spell == null || string.IsNullOrEmpty(spell.id))
            return false;

        if (!unlockedSpellIds.Contains(spell.id))
            return false;

        return spell.stages != null && spell.stages.Count > 0;
    }

    private bool HasUnlockedSpell()
    {
        if (chantDatabase == null)
            return false;

        foreach (ChantSpellData spell in chantDatabase.Spells)
        {
            if (IsUsableSpell(spell))
                return true;
        }

        return false;
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
        if (currentSpell == null || currentStage == null || hasAmbiguousExactMatch)
            return false;

        return string.Equals(currentInput, currentStage.chantText, StringComparison.Ordinal);
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
            canResolve = CanCastCurrentStage() && HasEnoughMana
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

    private void UpdateTargetTextUI()
    {
        if (targetTextUI == null)
            return;

        if (chantDatabase == null || !chantDatabase.IsLoaded)
        {
            targetTextUI.text = "";
            return;
        }

        StringBuilder builder = new StringBuilder();
        bool hasInput = !string.IsNullOrEmpty(currentInput);
        bool firstVisibleSpell = true;

        foreach (ChantSpellData spell in chantDatabase.Spells)
        {
            if (!IsUsableSpell(spell))
                continue;

            bool isCandidate = !hasInput || candidateSpellIds.Contains(spell.id);

            if (hasInput && !isCandidate && hideNonCandidateSpells)
                continue;

            if (!firstVisibleSpell)
            {
                builder.AppendLine();
            }

            AppendSpellTarget(builder, spell, hasInput, isCandidate);
            firstVisibleSpell = false;
        }

        targetTextUI.text = builder.ToString();
    }

    private void AppendSpellTarget(StringBuilder builder, ChantSpellData spell, bool hasInput, bool isCandidate)
    {
        ChantStageData displayStage = GetHighestStage(spell);

        if (displayStage == null || string.IsNullOrEmpty(displayStage.chantText))
            return;

        string displayText = displayStage.chantText;

        if (!hasInput)
        {
            AppendColoredString(builder, displayText, NormalColor);
            return;
        }

        if (!isCandidate)
        {
            AppendColoredString(builder, displayText, DimColor);
            return;
        }

        ChantStageData recognitionStage = null;

        if (currentSpell == spell && currentStage != null)
        {
            recognitionStage = currentStage;
        }
        else
        {
            recognitionStage = FindBestPrefixStage(spell, currentInput);
        }

        if (recognitionStage == null || string.IsNullOrEmpty(recognitionStage.chantText))
        {
            AppendColoredString(builder, displayText, NormalColor);
            return;
        }

        string recognitionText = recognitionStage.chantText;
        int recognitionStartIndex = displayText.IndexOf(recognitionText, StringComparison.Ordinal);

        if (recognitionStartIndex < 0)
        {
            AppendColoredString(builder, displayText, NormalColor);
            return;
        }

        int recognitionEndIndex = recognitionStartIndex + recognitionText.Length;

        for (int i = 0; i < displayText.Length; i++)
        {
            char character = displayText[i];

            if (i < recognitionStartIndex || i >= recognitionEndIndex)
            {
                AppendColoredCharacter(builder, character, ContextColor);
                continue;
            }

            int inputIndex = i - recognitionStartIndex;

            if (inputIndex >= currentInput.Length)
            {
                AppendColoredCharacter(builder, character, NormalColor);
                continue;
            }

            if (currentInput[inputIndex] == recognitionText[inputIndex])
            {
                AppendColoredCharacter(builder, character, CorrectColor);
            }
            else
            {
                AppendColoredCharacter(builder, character, TypoColor);
            }
        }
    }

    private ChantStageData GetHighestStage(ChantSpellData spell)
    {
        if (spell == null || spell.stages == null || spell.stages.Count == 0)
            return null;

        ChantStageData highestStage = null;

        foreach (ChantStageData stage in spell.stages)
        {
            if (stage == null)
                continue;

            if (highestStage == null || stage.chantLevel > highestStage.chantLevel)
            {
                highestStage = stage;
            }
        }

        return highestStage;
    }

    private void AppendColoredString(StringBuilder builder, string value, string color)
    {
        if (string.IsNullOrEmpty(value))
            return;

        foreach (char character in value)
        {
            AppendColoredCharacter(builder, character, color);
        }
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

    private void ShowAmbiguousChantFeedback()
    {
        const string message = "같은 영창 문장이 여러 주문에 등록되어 있어 주문을 판별할 수 없습니다.";

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

    private void ValidateUnlockedSpells()
    {
        foreach (string spellId in unlockedSpellIds)
        {
            if (chantDatabase.GetSpell(spellId) == null)
            {
                Debug.LogWarning($"[ChantManager] 획득 목록에 존재하지 않는 주문 ID가 있습니다: {spellId}");
            }
        }
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
        currentSpell = null;
        currentStage = null;
        currentInput = "";
        correctCount = 0;
        typoCount = 0;
        hasAmbiguousExactMatch = false;
        candidateSpellIds.Clear();
    }

    private void ResetChant()
    {
        interruptPending = false;
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

    [ContextMenu("Debug / Unlock Lightning")]
    private void DebugUnlockLightning()
    {
        UnlockSpell("lightning");
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