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

    [SerializeField]
    private TMP_Text correctCountUI;

    [SerializeField]
    private TMP_Text manaCostUI;


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


    // UI에서 직접 표시하지 않더라도
    // 데이터와 외부 접근은 유지.
    public int TypoCount =>
        typoCount;


    // UI에서 직접 표시하지 않더라도
    // 데이터와 외부 접근은 유지.
    public int ChantLevel =>
        currentStage?.chantLevel ?? 0;


    /// <summary>
    /// 현재 영창 단계에서 소비할 마나.
    /// PlayerController가 현재 마나와 비교할 때 사용.
    /// </summary>
    public float CurrentManaCost =>
        currentStage?.manaCost ?? 0f;


    /// <summary>
    /// 현재 단계의 영창문 길이까지 입력했는지 여부.
    /// 마나는 검사하지 않는다.
    /// </summary>
    public bool CanResolveCurrentStage =>
        CanCastCurrentStage();


    /// <summary>
    /// 현재 영창 단계의 예상 피해.
    /// UI 오브젝트가 없어도 데이터는 계속 계산한다.
    /// </summary>
    public float ExpectedDamage =>
        CalculateExpectedDamage();


    /// <summary>
    /// 실제 피해.
    /// 오타가 하나라도 있으면 0.
    /// UI 오브젝트가 없어도 데이터는 계속 계산한다.
    /// </summary>
    public float ActualDamage =>
        CalculateActualDamage();


    // =========================================================
    // Events
    // =========================================================

    public event Action OnChantStarted;

    public event Action OnChantCancelled;

    public event Action OnChantInterrupted;

    /// <summary>
    /// 정상적으로 영창이 발동되었을 때 발생.
    /// PlayerController가 구독한다.
    /// </summary>
    public event Action<CastResult> OnChantCast;


    /// <summary>
    /// 영창 중 Enter가 눌렸을 때 발생.
    ///
    /// PlayerController에서
    /// 오타 여부 / 마나 여부를 확인하고
    /// 취소 또는 발동을 결정한다.
    /// </summary>
    public event Action OnChantSubmitRequested;


    /// <summary>
    /// 영창 입력 상태가 바뀔 때마다 외부 UI 담당에게
    /// 영창 정보를 전달한다.
    ///
    /// ChantPanel에서 직접 표시하지 않는
    /// ChantLevel / TypoCount / ExpectedDamage /
    /// ActualDamage 데이터도 이 이벤트로 전달된다.
    /// </summary>
    public event Action<ChantPreviewData>
        OnChantPreviewChanged;


    // =========================================================
    // Runtime Data
    // =========================================================

    private ChantSpellData currentSpell;

    private ChantStageData currentStage;

    private string currentInput = "";

    private int correctCount;

    private int typoCount;


    // 영창 시작에 사용된 Enter가
    // 같은 프레임에 Submit Enter로 또 처리되는 것을 방지.
    private int chantStartedFrame = -1;


    // =========================================================
    // Interrupt
    // =========================================================

    /*
     * PlayerController.TakeDamage()
     * -> ApplyDamage()
     * -> InterruptChant()
     *
     * 호출이 OnTriggerStay2D 같은 Physics 콜백 내부에서
     * 발생할 수 있다.
     *
     * 그 자리에서 chantPanel.SetActive(false)를 실행하면
     * TMP_InputField.OnDisable 내부에서 DestroyImmediate 관련
     * Unity 경고가 발생할 수 있다.
     *
     * 따라서 Interrupt만 실제 Reset을 LateUpdate로 미룬다.
     */
    private bool interruptPending;


    // =========================================================
    // Clipboard
    // =========================================================

    private string savedClipboard = "";

    private bool clipboardCaptured;


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


        // -----------------------------------------------------
        // Clipboard Blocking
        // -----------------------------------------------------

        if (blockPaste)
        {
            BlockClipboard();
        }


        // -----------------------------------------------------
        // Enter Handling
        // -----------------------------------------------------

        /*
         * 영창 시작에 사용된 Enter가
         * 바로 Submit Enter로 처리되는 것을 막는다.
         */
        if (Time.frameCount == chantStartedFrame)
        {
            return;
        }


        Keyboard keyboard =
            Keyboard.current;


        if (keyboard == null)
            return;


        bool enterPressed =
            keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame;


        if (!enterPressed)
            return;


        OnChantSubmitRequested?.Invoke();
    }


    private void LateUpdate()
    {
        if (!interruptPending)
            return;


        interruptPending =
            false;


        /*
         * Interrupt가 예약된 이후 같은 프레임에
         * 다른 이유로 이미 영창이 끝났을 수도 있다.
         */
        if (State != ChantState.Casting)
            return;


        ResetChant();


        OnChantInterrupted?.Invoke();
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


    private void OnApplicationFocus(
        bool hasFocus
    )
    {
        if (!hasFocus)
            return;


        if (State != ChantState.Casting)
            return;


        if (!blockPaste)
            return;


        /*
         * 게임 밖으로 나갔다가
         * 외부 프로그램에서 텍스트를 복사한 뒤
         * 게임으로 돌아오는 경우에도 클립보드를 비운다.
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
         * TMP_InputField 붙여넣기에서 사용하는
         * 시스템 클립보드 내용을 영창 중 비운다.
         */
        if (!string.IsNullOrEmpty(
            GUIUtility.systemCopyBuffer
        ))
        {
            GUIUtility.systemCopyBuffer =
                "";
        }
    }


    private void RestoreClipboard()
    {
        if (!clipboardCaptured)
            return;


        GUIUtility.systemCopyBuffer =
            savedClipboard;


        savedClipboard =
            "";


        clipboardCaptured =
            false;
    }


    // =========================================================
    // Spell
    // =========================================================

    public bool SetSpell(
        string spellId
    )
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


        interruptPending =
            false;


        CaptureClipboard();


        State =
            ChantState.Casting;


        /*
         * 영창 화면 입장에 사용한 Enter가
         * 같은 프레임에 Submit으로 처리되지 않도록 저장.
         */
        chantStartedFrame =
            Time.frameCount;


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


        /*
         * 동일한 Physics 콜백에서 여러 번
         * 호출되는 것을 방지한다.
         */
        if (interruptPending)
            return;


        /*
         * 즉시 Reset하지 않는다.
         *
         * Physics Trigger/Collision 콜백 내부에서
         * TMP_InputField가 비활성화되는 문제를 피하기 위해
         * LateUpdate에서 처리한다.
         */
        interruptPending =
            true;
    }


    public CastResult ResolveChant()
    {
        if (State != ChantState.Casting)
            return default;


        EvaluateInput();


        CastResult result =
            CreateCastResult();


        /*
         * 현재 단계의 영창문 길이를 끝까지
         * 입력하지 않았다면 발동하지 않는다.
         */
        if (!CanCastCurrentStage())
        {
            return result;
        }


        /*
         * PlayerController가 ResolveChant 호출 전에
         *
         * - 오타 여부
         * - 현재 마나
         * - 필요 마나
         *
         * 를 확인한다.
         */


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


        /*
         * ChantPanel에서 일부 표시용 Text를 삭제해도
         * 외부 UI에는 계속 데이터를 전달한다.
         */
        NotifyPreviewChanged();
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


            // 1. 맞은 글자 수 우선
            if (stageCorrectCount >
                bestCorrectCount)
            {
                better =
                    true;
            }

            // 2. 맞은 글자 수가 같으면 정확도 우선
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

            // 3. 정확도도 같으면 길이가 가까운 단계 우선
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
            /*
             * 목표 영창문보다 더 많이 입력한 문자도
             * 오타로 판정한다.
             */
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


        /*
         * 예상 피해 =
         * 기본 피해 × 현재 영창 단계 배율
         */
        return
            currentSpell.baseDamage *
            currentStage.damageMultiplier;
    }


    private float CalculateActualDamage()
    {
        if (currentSpell == null ||
            currentStage == null)
        {
            return 0f;
        }


        /*
         * 오타가 하나라도 있으면
         * 실제 피해량은 무조건 0.
         */
        if (typoCount > 0)
        {
            return 0f;
        }


        /*
         * 오타가 없다면 예상 피해가
         * 그대로 실제 피해.
         */
        return
            CalculateExpectedDamage();
    }


    // =========================================================
    // Preview Data
    // =========================================================

    private void NotifyPreviewChanged()
    {
        /*
         * ChantPanel에서 직접 Text로 표시하지 않더라도
         * 모든 계산 데이터는 그대로 유지하고
         * 외부 UI 담당에게 전달한다.
         */
        ChantPreviewData preview =
            new ChantPreviewData
            {
                chantLevel =
                    ChantLevel,


                expectedDamage =
                    ExpectedDamage,


                actualDamage =
                    ActualDamage,


                manaCost =
                    CurrentManaCost,


                correctCount =
                    CorrectCount,


                typoCount =
                    TypoCount,


                canResolve =
                    CanCastCurrentStage()
            };


        OnChantPreviewChanged?.Invoke(
            preview
        );
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


            /*
             * 기존 CastResult 필드 유지.
             *
             * 오타 없음 = 1
             * 오타 있음 = 0
             */
            penaltyMultiplier =
                typoCount == 0
                    ? 1f
                    : 0f,


            /*
             * 단계별 실제 마나 소비량.
             */
            manaCost =
                currentStage?.manaCost ?? 0f,


            /*
             * 기존 시스템 호환을 위해 유지.
             * 실제 플레이어 마나 소비에는
             * manaCost를 사용한다.
             */
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


        /*
         * CorrectCount는 현재 ChantPanel에서
         * 계속 표시하므로 유지.
         */
        if (correctCountUI != null)
        {
            correctCountUI.text =
                $"정상 입력 : {correctCount}";
        }


        /*
         * ManaCost도 현재 ChantPanel에서
         * 표시할 수 있도록 유지.
         */
        if (manaCostUI != null)
        {
            manaCostUI.text =
                $"마나 소비 : {CurrentManaCost:0.#}";
        }


        /*
         * 아래 데이터들은 계속 계산되지만
         * ChantPanel에서는 직접 표시하지 않는다.
         *
         * - TypoCount
         * - ChantLevel
         * - ExpectedDamage
         * - ActualDamage
         *
         * 필요한 외부 UI에서는
         * OnChantPreviewChanged를 구독해서 사용한다.
         */
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


        /*
         * 아무것도 입력하지 않은 상태에서는
         * 전체 영창문을 회색으로 표시.
         */
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


            // 아직 입력하지 않은 문자
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


        /*
         * 목표 영창문보다 많이 입력한 문자도
         * 빨간색 오타로 표시.
         */
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
        /*
         * 예약되어 있던 Interrupt가 있다면 제거.
         */
        interruptPending =
            false;


        State =
            ChantState.Idle;


        ClearRuntimeData();


        chantStartedFrame =
            -1;


        if (chantInputField != null)
        {
            chantInputField.DeactivateInputField();


            chantInputField.SetTextWithoutNotify(
                ""
            );
        }


        SetChantUI(false);


        UpdateUI();


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