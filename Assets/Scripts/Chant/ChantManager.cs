using System;
using System.Text;
using TMPro;
using UnityEngine;

public class ChantManager : MonoBehaviour, IChantManager
{
    public enum ChantState
    {
        Idle,
        Casting
    }

    // =========================================================
    // Database
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


    [Header("Status UI")]
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
    // Damage Settings
    // =========================================================

    [Header("Damage Settings")]
    [SerializeField]
    [Min(0f)]
    private float penaltyPerTypo = 0.15f;


    // =========================================================
    // Paste Settings
    // =========================================================

    [Header("Paste Settings")]
    [SerializeField]
    private bool blockPaste = true;


    // =========================================================
    // Public Properties
    // =========================================================

    public ChantState State { get; private set; }
        = ChantState.Idle;

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
    // Runtime Data
    // =========================================================

    private ChantSpellData currentSpell;

    private ChantStageData currentStage;

    private string currentInput = "";

    private int correctCount;

    private int typoCount;


    // =========================================================
    // Clipboard
    // =========================================================

    // 영창 시작 전에 사용자가 가지고 있던 클립보드.
    private string savedClipboard = "";

    private bool clipboardCaptured = false;


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


    private void Update()
    {
        if (State != ChantState.Casting)
            return;

        if (!blockPaste)
            return;

        BlockClipboard();
    }


    private void OnDestroy()
    {
        if (chantInputField != null)
        {
            chantInputField.onValueChanged.RemoveListener(
                OnInputChanged
            );
        }

        RestoreClipboard();
    }


    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            return;

        if (State != ChantState.Casting)
            return;

        if (!blockPaste)
            return;

        /*
         * 플레이어가
         *
         * 1. 게임에서 Alt + Tab
         * 2. 메모장에서 주문 복사
         * 3. 다시 게임으로 돌아옴
         *
         * 을 했을 때도 클립보드를 제거한다.
         */
        BlockClipboard();
    }


    // =========================================================
    // Clipboard Blocking
    // =========================================================

    private void CaptureClipboard()
    {
        if (!blockPaste)
            return;

        savedClipboard =
            GUIUtility.systemCopyBuffer;

        clipboardCaptured =
            true;

        BlockClipboard();
    }


    private void BlockClipboard()
    {
        /*
         * TMP_InputField는 Ctrl+V 등의 붙여넣기에서
         * systemCopyBuffer를 사용한다.
         *
         * 영창 중에는 내용 자체를 비워서
         * 붙여넣을 텍스트가 존재하지 않게 만든다.
         */
        if (!string.IsNullOrEmpty(
            GUIUtility.systemCopyBuffer
        ))
        {
            GUIUtility.systemCopyBuffer = "";
        }
    }


    private void RestoreClipboard()
    {
        if (!clipboardCaptured)
            return;

        GUIUtility.systemCopyBuffer =
            savedClipboard;

        savedClipboard = "";

        clipboardCaptured =
            false;
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
            chantDatabase.GetSpell(
                spellId
            );

        if (spell == null)
            return false;

        if (spell.stages == null ||
            spell.stages.Count == 0)
        {
            Debug.LogError(
                $"[ChantManager] 영창 단계가 없습니다: {spellId}"
            );

            return false;
        }

        currentSpell =
            spell;

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

        // 영창 시작 전에 클립보드 저장 후 비우기
        CaptureClipboard();

        State =
            ChantState.Casting;

        SetChantUI(true);

        if (chantInputField != null)
        {
            chantInputField.SetTextWithoutNotify(
                ""
            );

            chantInputField.interactable =
                true;

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

        OnChantCast?.Invoke(
            result
        );

        return result;
    }


    // =========================================================
    // Input
    // =========================================================

    private void OnInputChanged(
        string value
    )
    {
        if (State != ChantState.Casting)
            return;

        currentInput =
            value ?? "";

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
        {
            return null;
        }

        ChantStageData bestStage =
            null;

        int bestCorrectCount =
            -1;

        float bestAccuracy =
            -1f;

        int bestLengthDifference =
            int.MaxValue;


        foreach (
            ChantStageData stage
            in currentSpell.stages
        )
        {
            if (stage == null)
                continue;

            if (string.IsNullOrEmpty(
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


            int stageCorrectCount =
                0;


            for (
                int i = 0;
                i < compareLength;
                i++
            )
            {
                if (input[i] ==
                    target[i])
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


            bool better =
                false;


            // 맞은 글자 수 우선
            if (stageCorrectCount >
                bestCorrectCount)
            {
                better =
                    true;
            }

            // 맞은 글자 수가 같으면 정확도
            else if (
                stageCorrectCount ==
                bestCorrectCount &&
                accuracy >
                bestAccuracy
            )
            {
                better =
                    true;
            }

            // 정확도도 같으면 길이가 가까운 단계
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
                better =
                    true;
            }


            if (!better)
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
        correctCount =
            0;

        typoCount =
            0;


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
            // 목표보다 초과 입력
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
    // Cast Validation
    // =========================================================

    private bool CanCastCurrentStage()
    {
        if (currentStage == null)
            return false;

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


    private float CalculateActualDamage()
    {
        if (!CanCastCurrentStage())
        {
            return 0f;
        }


        float expectedDamage =
            CalculateExpectedDamage();


        float penalty =
            CalculatePenaltyMultiplier();


        if (penalty <= 0f)
        {
            return expectedDamage;
        }


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
                canCast &&
                currentStage != null
                    ? currentStage.chantLevel
                    : 0,

            expectedDamage =
                CalculateExpectedDamage(),

            actualDamage =
                CalculateActualDamage(),

            penaltyMultiplier =
                CalculatePenaltyMultiplier(),

            manaRelease =
                canCast &&
                currentSpell != null
                    ? currentSpell.manaRelease
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
            spellNameUI.text =
                "";

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
            targetTextUI.text =
                "";

            return;
        }


        // 아무것도 입력하지 않았을 때
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


            // 아직 입력 안 함
            if (i >= currentInput.Length)
            {
                AppendColoredCharacter(
                    builder,
                    targetCharacter,
                    "#888888"
                );

                continue;
            }


            // 정상
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


        // 목표보다 초과 입력한 문자
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
        {
            return "";
        }


        StringBuilder builder =
            new StringBuilder();


        foreach (
            char character
            in text
        )
        {
            AppendColoredCharacter(
                builder,
                character,
                "#888888"
            );
        }


        return
            builder.ToString();
    }


    private void AppendColoredCharacter(
        StringBuilder builder,
        char character,
        string color
    )
    {
        builder.Append(
            "<color="
        );

        builder.Append(
            color
        );

        builder.Append(
            ">"
        );


        builder.Append(
            EscapeRichTextCharacter(
                character
            )
        );


        builder.Append(
            "</color>"
        );
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


    private void SetChantUI(
        bool active
    )
    {
        if (chantPanel != null)
        {
            chantPanel.SetActive(
                active
            );
        }
    }


    // =========================================================
    // Reset
    // =========================================================

    private void ClearRuntimeData()
    {
        currentInput =
            "";

        currentStage =
            null;

        correctCount =
            0;

        typoCount =
            0;
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


        // 영창이 끝났으므로 원래 클립보드 복구
        RestoreClipboard();
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