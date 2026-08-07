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

    // =========================================================
    // References
    // =========================================================

    [Header("Database")]
    [SerializeField]
    private ChantDatabase chantDatabase;

    [SerializeField]
    private string defaultSpellId = "dark_flame";


    // =========================================================
    // UI
    // =========================================================

    [Header("UI")]
    [SerializeField]
    private GameObject chantPanel;

    [SerializeField]
    private TMP_Text spellNameUI;

    [SerializeField]
    private TMP_Text targetTextUI;

    [SerializeField]
    private TMP_InputField chantInputField;


    [Header("Debug / Status UI")]
    [SerializeField]
    private TMP_Text correctCountUI;

    [SerializeField]
    private TMP_Text typoCountUI;

    [SerializeField]
    private TMP_Text chantLevelUI;

    [SerializeField]
    private TMP_Text expectedDamageUI;

    [SerializeField]
    private TMP_Text actualDamageUI;


    // =========================================================
    // Settings
    // =========================================================

    [Header("Damage Settings")]
    [SerializeField]
    [Min(0f)]
    private float penaltyPerTypo = 0.15f;


    // =========================================================
    // Public Properties
    // =========================================================

    public ChantState State { get; private set; } =
        ChantState.Idle;

    public bool IsCasting =>
        State == ChantState.Casting;

    public ChantSpellData CurrentSpell =>
        currentSpell;

    public ChantStageData CurrentStage =>
        currentStage;

    public string CurrentInput =>
        currentInput;

    public int CorrectCount =>
        correctCount;

    public int TypoCount =>
        typoCount;

    /// <summary>
    /// 현재 플레이어가 목표로 하고 있는 단계.
    /// 아직 완성하지 않았더라도 목표 단계는 표시된다.
    /// </summary>
    public int ChantLevel =>
        currentStage?.chantLevel ?? 0;

    public float ExpectedDamage =>
        CalculateExpectedDamage();

    public float ActualDamage =>
        CalculateActualDamage();


    // =========================================================
    // Events
    // =========================================================

    public event Action OnChantStarted;

    public event Action OnChantCancelled;

    public event Action OnChantInterrupted;

    public event Action<CastResult> OnChantCast;


    // =========================================================
    // Internal Data
    // =========================================================

    private ChantSpellData currentSpell;

    private ChantStageData currentStage;

    private string currentInput = "";

    private string lastValidInput = "";

    private int correctCount;

    private int typoCount;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (chantInputField != null)
        {
            chantInputField.onValueChanged.AddListener(
                OnInputChanged
            );
        }

        SetChantUI(false);
    }

    private void Start()
    {
        if (chantDatabase == null)
        {
            Debug.LogError(
                "[ChantManager] ChantDatabase가 연결되지 않았습니다."
            );

            return;
        }

        if (!SetSpell(defaultSpellId))
        {
            Debug.LogError(
                $"[ChantManager] 기본 주문 로드 실패: {defaultSpellId}"
            );
        }

        UpdateUI();
    }

    private void OnDestroy()
    {
        if (chantInputField != null)
        {
            chantInputField.onValueChanged.RemoveListener(
                OnInputChanged
            );
        }
    }


    // =========================================================
    // Spell
    // =========================================================

    public bool SetSpell(string spellId)
    {
        if (State == ChantState.Casting)
        {
            Debug.LogWarning(
                "[ChantManager] 영창 중에는 주문을 변경할 수 없습니다."
            );

            return false;
        }

        if (chantDatabase == null)
        {
            Debug.LogError(
                "[ChantManager] ChantDatabase가 없습니다."
            );

            return false;
        }

        ChantSpellData spell =
            chantDatabase.GetSpell(spellId);

        if (spell == null)
            return false;

        if (spell.stages == null ||
            spell.stages.Count == 0)
        {
            Debug.LogError(
                $"[ChantManager] 영창 단계가 없는 주문입니다: {spellId}"
            );

            return false;
        }

        currentSpell = spell;

        ClearRuntimeData();

        UpdateUI();

        return true;
    }


    // =========================================================
    // Chant Control
    // =========================================================

    public void StartChant()
    {
        if (State == ChantState.Casting)
            return;

        if (currentSpell == null)
        {
            Debug.LogWarning(
                "[ChantManager] 선택된 주문이 없습니다."
            );

            return;
        }

        ClearRuntimeData();

        State = ChantState.Casting;

        SetChantUI(true);

        if (chantInputField != null)
        {
            chantInputField.SetTextWithoutNotify("");

            chantInputField.interactable = true;

            chantInputField.Select();
            chantInputField.ActivateInputField();
        }

        UpdateUI();

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

        ResetChant();

        OnChantInterrupted?.Invoke();
    }

    public CastResult ResolveChant()
    {
        if (State != ChantState.Casting)
            return default;

        EvaluateInput();

        CastResult result =
            CreateCastResult();

        ResetChant();

        OnChantCast?.Invoke(result);

        return result;
    }


    // =========================================================
    // Input
    // =========================================================

    private void OnInputChanged(string value)
    {
        if (State != ChantState.Casting)
            return;

        // Ctrl+V / Cmd+V / Shift+Insert 차단
        if (IsPastePressed())
        {
            RestoreLastValidInput();

            return;
        }

        currentInput =
            value ?? "";

        lastValidInput =
            currentInput;

        // 입력 내용에 가장 잘 맞는 영창 단계를 찾는다.
        currentStage =
            FindBestMatchingStage(
                currentInput
            );

        EvaluateInput();

        UpdateUI();
    }

    private bool IsPastePressed()
    {
        Keyboard keyboard =
            Keyboard.current;

        if (keyboard == null)
            return false;

        bool ctrlOrCommand =
            keyboard.ctrlKey.isPressed ||
            keyboard.leftMetaKey.isPressed ||
            keyboard.rightMetaKey.isPressed;

        bool normalPaste =
            ctrlOrCommand &&
            keyboard.vKey.wasPressedThisFrame;

        bool shiftInsert =
            keyboard.shiftKey.isPressed &&
            keyboard.insertKey.wasPressedThisFrame;

        return
            normalPaste ||
            shiftInsert;
    }

    private void RestoreLastValidInput()
    {
        if (chantInputField != null)
        {
            chantInputField.SetTextWithoutNotify(
                lastValidInput
            );
        }

        currentInput =
            lastValidInput;

        currentStage =
            FindBestMatchingStage(
                currentInput
            );

        EvaluateInput();

        UpdateUI();
    }


    // =========================================================
    // Stage Detection
    // =========================================================

    /// <summary>
    /// 현재 입력이 어느 영창 단계를 향하고 있는지 결정한다.
    ///
    /// 테스트 예:
    /// 불...   -> "불태워라"
    /// 어...   -> "어둠을 불태워라"
    /// 무...   -> "무한한 어둠을 불태워라"
    ///
    /// 오타가 있더라도 각 위치에서 맞은 문자 수를 비교하여
    /// 가장 가능성이 높은 단계를 선택한다.
    /// </summary>
    private ChantStageData FindBestMatchingStage(
        string input
    )
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

        int bestLengthDifference =
            int.MaxValue;

        foreach (
            ChantStageData stage
            in currentSpell.stages
        )
        {
            if (stage == null ||
                string.IsNullOrEmpty(
                    stage.chantText
                ))
            {
                continue;
            }

            string target =
                stage.chantText;

            int compareLength =
                Mathf.Min(
                    input.Length,
                    target.Length
                );

            int stageCorrectCount = 0;

            for (
                int i = 0;
                i < compareLength;
                i++
            )
            {
                if (input[i] == target[i])
                {
                    stageCorrectCount++;
                }
            }

            float accuracy =
                compareLength > 0
                    ? (float)stageCorrectCount /
                      compareLength
                    : 0f;

            int lengthDifference =
                Mathf.Abs(
                    target.Length -
                    input.Length
                );

            bool isBetter = false;

            if (stageCorrectCount >
                bestCorrectCount)
            {
                isBetter = true;
            }
            else if (
                stageCorrectCount ==
                bestCorrectCount &&
                accuracy >
                bestAccuracy
            )
            {
                isBetter = true;
            }
            else if (
                stageCorrectCount ==
                bestCorrectCount &&
                Mathf.Approximately(
                    accuracy,
                    bestAccuracy
                ) &&
                lengthDifference <
                bestLengthDifference
            )
            {
                isBetter = true;
            }

            if (!isBetter)
                continue;

            bestStage =
                stage;

            bestCorrectCount =
                stageCorrectCount;

            bestAccuracy =
                accuracy;

            bestLengthDifference =
                lengthDifference;
        }

        return bestStage;
    }


    // =========================================================
    // Evaluation
    // =========================================================

    private void EvaluateInput()
    {
        correctCount = 0;
        typoCount = 0;

        if (currentStage == null)
            return;

        string target =
            currentStage.chantText;

        for (
            int i = 0;
            i < currentInput.Length;
            i++
        )
        {
            // 목표 영창보다 많이 입력
            if (i >= target.Length)
            {
                typoCount++;
                continue;
            }

            if (currentInput[i] ==
                target[i])
            {
                correctCount++;
            }
            else
            {
                typoCount++;
            }
        }
    }


    // =========================================================
    // Cast State
    // =========================================================

    private bool CanCastCurrentStage()
    {
        if (currentStage == null)
            return false;

        // 지정된 단계의 글자 수까지 입력해야 한다.
        // 오타 자체는 발동을 막지 않고 피해 패널티로 처리.
        return
            currentInput.Length ==
            currentStage.chantText.Length;
    }

    private bool IsPerfectChant()
    {
        return
            CanCastCurrentStage() &&
            typoCount == 0;
    }


    // =========================================================
    // Damage
    // =========================================================

    /// <summary>
    /// 해당 영창 단계를 정상 완료했을 때 기대되는 피해.
    /// </summary>
    private float CalculateExpectedDamage()
    {
        if (currentSpell == null ||
            currentStage == null)
        {
            return 0f;
        }

        return
            currentSpell.baseDamage *
            currentStage.damageMultiplier;
    }

    private float CalculatePenaltyMultiplier()
    {
        return
            1f +
            typoCount *
            penaltyPerTypo;
    }

    /// <summary>
    /// 지금 발동했을 때 실제 적용될 피해량.
    ///
    /// 문장이 아직 완성되지 않았다면 0.
    /// 완성되었다면 오타 수에 따라 피해 감소.
    /// </summary>
    private float CalculateActualDamage()
    {
        if (!CanCastCurrentStage())
            return 0f;

        float expectedDamage =
            CalculateExpectedDamage();

        float penalty =
            CalculatePenaltyMultiplier();

        if (penalty <= 0f)
            return expectedDamage;

        return
            expectedDamage /
            penalty;
    }


    // =========================================================
    // Cast Result
    // =========================================================

    private CastResult CreateCastResult()
    {
        bool canCast =
            CanCastCurrentStage();

        return new CastResult
        {
            spellId =
                currentSpell?.id ?? "",

            spellName =
                currentSpell?.spellName ?? "",

            targetText =
                currentStage?.chantText ?? "",

            typedText =
                currentInput,

            correctCount =
                correctCount,

            typoCount =
                typoCount,

            castLevel =
                canCast
                    ? currentStage.chantLevel
                    : 0,

            expectedDamage =
                CalculateExpectedDamage(),

            actualDamage =
                CalculateActualDamage(),

            penaltyMultiplier =
                CalculatePenaltyMultiplier(),

            manaRelease =
                canCast
                    ? currentSpell?.manaRelease ?? 0f
                    : 0f,

            magicType =
                currentSpell?.magicType ?? "",

            effectId =
                currentSpell?.effectId ?? "",

            canCast =
                canCast,

            completed =
                IsPerfectChant()
        };
    }


    // =========================================================
    // UI
    // =========================================================

    private void UpdateUI()
    {
        UpdateSpellNameUI();

        UpdateTargetTextUI();

        if (correctCountUI != null)
        {
            correctCountUI.text =
                $"정상 입력 : {correctCount}";
        }

        if (typoCountUI != null)
        {
            typoCountUI.text =
                $"오타 : {typoCount}";
        }

        if (chantLevelUI != null)
        {
            chantLevelUI.text =
                $"영창 단계 : {ChantLevel}";
        }

        if (expectedDamageUI != null)
        {
            expectedDamageUI.text =
                $"예상 피해 : {ExpectedDamage:0.#}";
        }

        if (actualDamageUI != null)
        {
            actualDamageUI.text =
                $"실제 피해 : {ActualDamage:0.#}";
        }
    }

    private void UpdateSpellNameUI()
    {
        if (spellNameUI == null)
            return;

        if (currentSpell == null)
        {
            spellNameUI.text = "";
            return;
        }

        spellNameUI.text =
            currentSpell.spellName;
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

        // 아직 어떤 단계를 입력하는지 판별되지 않았다면
        // 전체 영창 문장을 회색으로 표시한다.
        if (currentStage == null)
        {
            targetTextUI.text =
                BuildUnenteredText(
                    currentSpell.fullChantText
                );

            return;
        }

        string target =
            currentStage.chantText;

        StringBuilder builder =
            new StringBuilder();

        for (
            int i = 0;
            i < target.Length;
            i++
        )
        {
            char targetCharacter =
                target[i];

            // 아직 입력하지 않은 부분
            if (i >= currentInput.Length)
            {
                AppendColoredCharacter(
                    builder,
                    targetCharacter,
                    "#888888"
                );

                continue;
            }

            // 정상 입력
            if (currentInput[i] ==
                targetCharacter)
            {
                AppendColoredCharacter(
                    builder,
                    targetCharacter,
                    "#00FF88"
                );
            }
            // 오타
            else
            {
                AppendColoredCharacter(
                    builder,
                    targetCharacter,
                    "#FF4444"
                );
            }
        }

        // 목표보다 추가 입력된 문자
        if (currentInput.Length >
            target.Length)
        {
            for (
                int i = target.Length;
                i < currentInput.Length;
                i++
            )
            {
                AppendColoredCharacter(
                    builder,
                    currentInput[i],
                    "#FF4444"
                );
            }
        }

        targetTextUI.text =
            builder.ToString();
    }

    private string BuildUnenteredText(
        string text
    )
    {
        if (string.IsNullOrEmpty(text))
            return "";

        StringBuilder builder =
            new StringBuilder();

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

        builder.Append(
            EscapeRichTextCharacter(
                character
            )
        );

        builder.Append("</color>");
    }

    private string EscapeRichTextCharacter(
        char character
    )
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


    // =========================================================
    // Reset
    // =========================================================

    private void ClearRuntimeData()
    {
        currentInput = "";

        lastValidInput = "";

        currentStage = null;

        correctCount = 0;

        typoCount = 0;
    }

    private void ResetChant()
    {
        State =
            ChantState.Idle;

        ClearRuntimeData();

        if (chantInputField != null)
        {
            chantInputField.DeactivateInputField();

            chantInputField.SetTextWithoutNotify(
                ""
            );
        }

        SetChantUI(false);

        UpdateUI();
    }


    // =========================================================
    // Debug
    // =========================================================

#if UNITY_EDITOR

    [ContextMenu("Debug / Start Chant")]
    private void DebugStartChant()
    {
        StartChant();
    }

    [ContextMenu("Debug / Cast Chant")]
    private void DebugCastChant()
    {
        CastResult result =
            ResolveChant();

        Debug.Log(
            $"[Chant Result]\n" +
            $"Spell: {result.spellName}\n" +
            $"Target: {result.targetText}\n" +
            $"Input: {result.typedText}\n" +
            $"Correct: {result.correctCount}\n" +
            $"Typo: {result.typoCount}\n" +
            $"Level: {result.castLevel}\n" +
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