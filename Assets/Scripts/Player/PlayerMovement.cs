using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System;
using SharpI7.Combat;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovement : MonoBehaviour, IPlayer
{
    private PlayerInputActions input;
    private Rigidbody2D rigidbody2D;
    private bool isDashOnCooldown;
    private float dashCooldownStartedAt;
    private bool isMovementLocked;
    private bool combatEnded;
    private Vector2 currentMovement;

    private float manaSaturationTimer;

    [SerializeField, Min(0f)]
    private float moveSpeed;

    [SerializeField, Min(0f)]
    private float dashWindupDuration;

    [SerializeField, Min(0f)]
    private float dashCooldownDuration;

    [SerializeField, Min(0f)]
    private float dashDuration;

    [SerializeField, Min(0f)]
    private float dashDistance;

    [SerializeField]
    private Vector2 referenceHeading;

    [SerializeField, Min(1f)]
    private float maxHealth = 3f;

    [SerializeField]
    private float defaultMana = 50f;

    [SerializeField, Min(0f)]
    private float manaWarningThreshold = 90f;

    [SerializeField, Min(1f)]
    private float criticalMana = 100f;

    [SerializeField, Min(0f)]
    private float manaFillSpeed = 10f;

    [SerializeField, Min(0.1f)]
    private float manaSaturationDamageCooldown = 3f;

    [SerializeField]
    private ChantManager chantManager;

    [SerializeField]
    private BossHealth bossHealth;

    public event Action<float, float> HealthChanged;
    public event Action<ManaStatus> ManaStatusChanged;
    public event Action Died;

    public float DashCooldownUntil { get; private set; }
    public bool IsDashing { get; private set; }
    public float DashCooldownProgress
    {
        get
        {
            if (IsDashing)
            {
                return 0f;
            }

            if (!isDashOnCooldown)
            {
                return 1f;
            }

            return Mathf.InverseLerp(
                dashCooldownStartedAt,
                DashCooldownUntil,
                Time.time
            );
        }
    }

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }

    public float MaxMana => criticalMana;
    public float CurrentMana { get; private set; }
    public float ManaDisplayMaximum =>
        criticalMana + manaFillSpeed * manaSaturationDamageCooldown;
    public float ManaSaturationRemaining =>
        CurrentMana >= criticalMana ? manaSaturationTimer : 0f;
    public bool IsManaWarning =>
        CurrentMana >= manaWarningThreshold && CurrentMana < criticalMana;
    public bool IsManaSaturated => CurrentMana >= criticalMana;
    public ManaStatus ManaStatus => new(
        CurrentMana,
        manaWarningThreshold,
        criticalMana,
        ManaDisplayMaximum,
        manaSaturationDamageCooldown,
        ManaSaturationRemaining,
        GetManaState());

    public bool IsAlive => CurrentHealth > 0f;

    private void Awake()
    {
        input = new PlayerInputActions();
        rigidbody2D = GetComponent<Rigidbody2D>();
        CurrentHealth = maxHealth;

        // if (bossHealth == null)
        // {
        //     bossHealth = FindFirstObjectByType<BossHealth>();
        // }
        // Just Drag&Drop, dumbass!
        CurrentMana = Mathf.Clamp(defaultMana, 0f, ManaDisplayMaximum);
        manaSaturationTimer = manaSaturationDamageCooldown;
    }

    private void OnEnable()
    {
        input.Movement.Enable();

        chantManager.OnChantStarted += LockMovement;
        chantManager.OnChantCancelled += UnlockMovement;
        chantManager.OnChantInterrupted += UnlockMovement;
        chantManager.OnChantCast += HandleChantCast;

        if (bossHealth != null)
        {
            bossHealth.Died -= HandleBossDied;
            bossHealth.Died += HandleBossDied;
        }
    }

    private void OnDisable()
    {
        input.Movement.Disable();

        chantManager.OnChantStarted -= LockMovement;
        chantManager.OnChantCancelled -= UnlockMovement;
        chantManager.OnChantInterrupted -= UnlockMovement;
        chantManager.OnChantCast -= HandleChantCast;

        if (bossHealth != null)
        {
            bossHealth.Died -= HandleBossDied;
        }
    }

    private void Update()
    {
        if (!IsAlive || combatEnded)
        {
            return;
        }

        if (input.Movement.Dash.WasPressedThisFrame())
        {
            Dash().Forget();
        }

        if (input.Movement.Spell.WasPressedThisFrame())
        {
            if (chantManager.IsCasting)
            {
                chantManager.ResolveChant();
            }
            else
            {
                chantManager.StartChant();
            }
        }

        if (input.Movement.ExitChant.WasPressedThisFrame())
        {
            chantManager.CancelChant();
        }

        UpdateMana(Time.deltaTime);
    }

    private async UniTaskVoid Dash()
    {
        if (isDashOnCooldown || IsDashing || isMovementLocked)
        {
            return;
        }

        IsDashing = true;

        var ct = destroyCancellationToken;

        await UniTask.Delay(TimeSpan.FromSeconds(dashWindupDuration), cancellationToken: ct);

        await rigidbody2D.DOMove(currentMovement * dashDistance, dashDuration)
            .SetRelative()
            .SetEase(Ease.InOutQuad)
            .ToUniTask(cancellationToken: ct);
        IsDashing = false;

        isDashOnCooldown = true;
        dashCooldownStartedAt = Time.time;
        DashCooldownUntil = dashCooldownStartedAt + dashCooldownDuration;
        await UniTask.Delay(TimeSpan.FromSeconds(dashCooldownDuration), cancellationToken: ct);
        isDashOnCooldown = false;
    }

    private void FixedUpdate()
    {
        if (!IsAlive || combatEnded)
        {
            return;
        }

        currentMovement = input.Movement.Movement.ReadValue<Vector2>();
        if (!IsDashing && !isMovementLocked)
        {
            rigidbody2D.MovePosition(rigidbody2D.position + moveSpeed * currentMovement);
        }

        Vector2 toBoss = bossHealth.transform.position - transform.position;
        float angle = Vector2.SignedAngle(referenceHeading, toBoss);

        rigidbody2D.MoveRotation(angle);

    }

    public void LockMovement()
    {
        isMovementLocked = true;
    }

    public void UnlockMovement()
    {
        if (combatEnded)
        {
            return;
        }

        isMovementLocked = false;
    }

    private void HandleBossDied()
    {
        combatEnded = true;
        currentMovement = Vector2.zero;
        IsDashing = false;
        DOTween.Kill(rigidbody2D);

        if (chantManager != null && chantManager.IsCasting)
        {
            chantManager.InterruptChant();
        }

        isMovementLocked = true;
        input.Movement.Disable();
    }

    private void HandleChantCast(CastResult result)
    {
        if (!result.canCast || result.manaRelease > CurrentMana)
        {
            UnlockMovement();
            return;
        }

        if (bossHealth == null)
        {
            bossHealth = FindFirstObjectByType<BossHealth>();
        }

        if (bossHealth != null)
        {
            bossHealth.TakeDamage(result.actualDamage);
            DeductMana(result.manaRelease);
        }

        UnlockMovement();
    }

    private static int CountCompletedWords(CastResult result)
    {
        if (result.typoCount > 0 || string.IsNullOrWhiteSpace(result.typedText))
        {
            return 0;
        }

        string typedText = result.typedText.Trim();
        string targetText = result.targetText ?? string.Empty;
        if (!targetText.StartsWith(typedText, StringComparison.Ordinal))
        {
            return 0;
        }

        // A correct prefix only counts when it ends at a word boundary.
        if (typedText.Length < targetText.Length &&
            !char.IsWhiteSpace(targetText[typedText.Length]))
        {
            return 0;
        }

        return typedText.Split(
            new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public void TakeDamage(float amount)
    {
        ApplyDamage(amount, false);
    }

    public void DeductMana(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        CurrentMana = Mathf.Max(0f, CurrentMana - amount);
        if (CurrentMana < criticalMana)
        {
            manaSaturationTimer = manaSaturationDamageCooldown;
        }

        PublishManaStatus();
    }

    private void UpdateMana(float deltaTime)
    {
        CurrentMana = Mathf.Min(
            ManaDisplayMaximum,
            CurrentMana + deltaTime * manaFillSpeed);

        if (CurrentMana < criticalMana)
        {
            manaSaturationTimer = manaSaturationDamageCooldown;
            PublishManaStatus();
            return;
        }

        manaSaturationTimer = Mathf.Max(0f, manaSaturationTimer - deltaTime);
        if (manaSaturationTimer > 0f)
        {
            PublishManaStatus();
            return;
        }

        CurrentMana = Mathf.Clamp(defaultMana, 0f, ManaDisplayMaximum);
        manaSaturationTimer = manaSaturationDamageCooldown;
        PublishManaStatus();
        ApplyDamage(1f, true);
    }

    private ManaState GetManaState()
    {
        if (IsManaSaturated)
        {
            return ManaState.Saturated;
        }

        return IsManaWarning ? ManaState.Warning : ManaState.Normal;
    }

    private void PublishManaStatus()
    {
        ManaStatusChanged?.Invoke(ManaStatus);
    }

    private void ApplyDamage(float amount, bool ignoreDashInvulnerability)
    {
        if (!IsAlive || amount <= 0f || (!ignoreDashInvulnerability && IsDashing))
        {
            return;
        }

        chantManager.InterruptChant();
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (!IsAlive)
        {
            LockMovement();
            Died?.Invoke();
            OutGameManager.LoadGameOver();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        dashWindupDuration = Mathf.Max(0f, dashWindupDuration);
        dashCooldownDuration = Mathf.Max(0f, dashCooldownDuration);
        dashDuration = Mathf.Max(0f, dashDuration);
        dashDistance = Mathf.Max(0f, dashDistance);
        maxHealth = Mathf.Max(1f, maxHealth);
        criticalMana = Mathf.Max(1f, criticalMana);
        manaWarningThreshold = Mathf.Clamp(manaWarningThreshold, 0f, criticalMana);
        manaFillSpeed = Mathf.Max(0f, manaFillSpeed);
        manaSaturationDamageCooldown = Mathf.Max(0.1f, manaSaturationDamageCooldown);
        defaultMana = Mathf.Clamp(defaultMana, 0f, ManaDisplayMaximum);
    }
#endif
}
