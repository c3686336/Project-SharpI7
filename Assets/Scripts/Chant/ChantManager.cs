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

    [Header("UI")]
    [SerializeField] private GameObject chantPanel;
    [SerializeField] private TMP_Text targetTextUI;
    [SerializeField] private TMP_InputField chantInputField;

    [Header("Debug UI")]
    [SerializeField] private TMP_Text correctCountUI;
    [SerializeField] private TMP_Text typoCountUI;
    [SerializeField] private TMP_Text chantLevelUI;

    [Header("Chant Settings")]
    [TextArea]
    [SerializeField]
    private string defaultTargetText = "무한한 어둠을 불태워라";

    [SerializeField]
    private int charactersPerLevel = 5;

    [SerializeField]
    private int maxChantLevel = 5;

    [SerializeField]
    private float powerPerLevel = 0.25f;

    [SerializeField]
    private float penaltyPerTypo = 0.15f;


    // =========================================================
    // Public Properties
    // =========================================================

    public ChantState State { get; private set; } = ChantState.Idle;

    public bool IsCasting => State == ChantState.Casting;

    public string TargetText => targetText;
    public string CurrentInput => currentInput;

    public int CorrectCount => correctCount;
    public int TypoCount => typoCount;
    public int ChantLevel => chantLevel;


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

    private string targetText = "";
    private string currentInput = "";

    private int correctCount;
    private int typoCount;
    private int chantLevel;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (chantInputField != null)
        {
            chantInputField.onValueChanged.AddListener(OnInputChanged);
        }

        SetChantUI(false);
    }

    private void Start()
    {
        SetTargetText(defaultTargetText);

        UpdateUI();
    }

    private void OnDestroy()
    {
        if (chantInputField != null)
        {
            chantInputField.onValueChanged.RemoveListener(OnInputChanged);
        }
    }


    // =========================================================
    // Chant Control
    // 외부 Player 코드에서 호출
    // =========================================================

    /// <summary>
    /// 영창 시작.
    /// Player 쪽에서 Space 입력 등을 처리한 뒤 호출.
    /// </summary>
    public void StartChant()
    {
        if (State == ChantState.Casting)
            return;

        if (string.IsNullOrEmpty(targetText))
        {
            Debug.LogWarning(
                "[ChantManager] 영창 목표 문장이 없습니다."
            );

            return;
        }

        State = ChantState.Casting;

        currentInput = "";

        correctCount = 0;
        typoCount = 0;
        chantLevel = 0;

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


    /// <summary>
    /// 영창 취소.
    /// Player 쪽에서 Esc 입력 등을 처리한 뒤 호출.
    /// </summary>
    public void CancelChant()
    {
        if (State != ChantState.Casting)
            return;

        ResetChant();

        OnChantCancelled?.Invoke();
    }


    /// <summary>
    /// 피격 등에 의해 영창 중단.
    /// Player 피격 처리 코드에서 호출.
    /// </summary>
    public void InterruptChant()
    {
        if (State != ChantState.Casting)
            return;

        ResetChant();

        OnChantInterrupted?.Invoke();
    }


    /// <summary>
    /// 현재 영창을 발동하고 결과 반환.
    /// Player 쪽에서 Enter 입력 등을 처리한 뒤 호출.
    /// </summary>
    public CastResult ResolveChant()
    {
        if (State != ChantState.Casting)
            return default;

        EvaluateInput();

        CastResult result = CreateCastResult();

        ResetChant();

        OnChantCast?.Invoke(result);

        return result;
    }


    // =========================================================
    // Target Text
    // =========================================================

    /// <summary>
    /// 영창할 목표 문장 설정.
    /// </summary>
    public void SetTargetText(string newTargetText)
    {
        targetText = newTargetText ?? "";

        if (State == ChantState.Casting)
        {
            EvaluateInput();
        }

        UpdateUI();
    }


    // =========================================================
    // Text Input
    // =========================================================

    private void OnInputChanged(string value)
    {
        if (State != ChantState.Casting)
            return;

        currentInput = value;

        EvaluateInput();
        UpdateUI();
    }


    // =========================================================
    // Evaluation
    // =========================================================

    private void EvaluateInput()
    {
        correctCount = 0;
        typoCount = 0;

        for (int i = 0; i < currentInput.Length; i++)
        {
            // 목표 문장보다 많이 입력한 경우
            if (i >= targetText.Length)
            {
                typoCount++;
                continue;
            }

            // 같은 위치의 문자 비교
            if (currentInput[i] == targetText[i])
            {
                correctCount++;
            }
            else
            {
                typoCount++;
            }
        }

        chantLevel = CalculateChantLevel();
    }


    // =========================================================
    // Calculation
    // =========================================================

    private int CalculateChantLevel()
    {
        if (charactersPerLevel <= 0)
            return 0;

        int level =
            correctCount / charactersPerLevel;

        return Mathf.Clamp(
            level,
            0,
            maxChantLevel
        );
    }


    private float CalculatePowerMultiplier()
    {
        return 1f +
               chantLevel * powerPerLevel;
    }


    private float CalculatePenaltyMultiplier()
    {
        return 1f +
               typoCount * penaltyPerTypo;
    }


    private CastResult CreateCastResult()
    {
        return new CastResult
        {
            targetText = targetText,
            typedText = currentInput,

            correctCount = correctCount,
            typoCount = typoCount,

            castLevel = chantLevel,

            powerMultiplier =
                CalculatePowerMultiplier(),

            penaltyMultiplier =
                CalculatePenaltyMultiplier(),

            completed =
                currentInput.Length == targetText.Length &&
                typoCount == 0
        };
    }


    // =========================================================
    // UI
    // =========================================================

    private void UpdateUI()
    {
        UpdateTargetTextUI();

        if (correctCountUI != null)
        {
            correctCountUI.text =
                $"정답 : {correctCount}";
        }

        if (typoCountUI != null)
        {
            typoCountUI.text =
                $"오타 : {typoCount}";
        }

        if (chantLevelUI != null)
        {
            chantLevelUI.text =
                $"영창 단계 : {chantLevel}";
        }
    }


    private void UpdateTargetTextUI()
    {
        if (targetTextUI == null)
            return;

        if (string.IsNullOrEmpty(targetText))
        {
            targetTextUI.text = "";
            return;
        }

        StringBuilder builder =
            new StringBuilder();

        for (int i = 0; i < targetText.Length; i++)
        {
            char targetChar =
                targetText[i];

            // 아직 입력하지 않음
            if (i >= currentInput.Length)
            {
                AppendColoredCharacter(
                    builder,
                    targetChar,
                    "#888888"
                );

                continue;
            }

            // 정답
            if (currentInput[i] == targetChar)
            {
                AppendColoredCharacter(
                    builder,
                    targetChar,
                    "#00FF88"
                );
            }
            // 오타
            else
            {
                AppendColoredCharacter(
                    builder,
                    targetChar,
                    "#FF4444"
                );
            }
        }

        // 목표보다 초과해서 입력한 문자
        if (currentInput.Length > targetText.Length)
        {
            for (
                int i = targetText.Length;
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
            EscapeRichTextCharacter(character)
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

    private void ResetChant()
    {
        State = ChantState.Idle;

        currentInput = "";

        correctCount = 0;
        typoCount = 0;
        chantLevel = 0;

        if (chantInputField != null)
        {
            chantInputField.DeactivateInputField();
            chantInputField.SetTextWithoutNotify("");
        }

        SetChantUI(false);

        UpdateUI();
    }


    // =========================================================
    // Debug
    // 플레이어 구현 전 단독 테스트용
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
            $"Target: {result.targetText}\n" +
            $"Input: {result.typedText}\n" +
            $"Correct: {result.correctCount}\n" +
            $"Typo: {result.typoCount}\n" +
            $"Level: {result.castLevel}\n" +
            $"Power: {result.powerMultiplier}\n" +
            $"Penalty: {result.penaltyMultiplier}"
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
