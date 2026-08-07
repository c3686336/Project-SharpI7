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
    private Vector2 currentMovement;

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
    private ChantManager chantManager;

    [SerializeField]
    private BossHealth bossHealth;

    [SerializeField, Min(0f)]
    private float baseSpellDamage = 10f;

    public event Action<float, float> HealthChanged;
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
    public bool IsAlive => CurrentHealth > 0f;

    private void Awake()
    {
        input = new PlayerInputActions();
        rigidbody2D = GetComponent<Rigidbody2D>();
        CurrentHealth = maxHealth;
    }

    private void OnEnable()
    {
        input.Movement.Enable();

        chantManager.OnChantStarted += LockMovement;
        chantManager.OnChantCancelled += UnlockMovement;
        chantManager.OnChantInterrupted += UnlockMovement;
        chantManager.OnChantCast += HandleChantCast;
    }

    private void OnDisable()
    {
        input.Movement.Disable();

        chantManager.OnChantStarted -= LockMovement;
        chantManager.OnChantCancelled -= UnlockMovement;
        chantManager.OnChantInterrupted -= UnlockMovement;
        chantManager.OnChantCast -= HandleChantCast;
    }

    private void Update()
    {
        if (!IsAlive)
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
        if (!IsAlive)
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
        isMovementLocked = false;
    }

    private void HandleChantCast(CastResult result)
    {
        if (!result.completed)
        {
            UnlockMovement();
            return;
        }

        float damage = baseSpellDamage * result.powerMultiplier /
            Mathf.Max(1f, result.penaltyMultiplier);

        bossHealth.TakeDamage(damage);
        UnlockMovement();
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f || IsDashing)
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
        baseSpellDamage = Mathf.Max(0f, baseSpellDamage);
    }
#endif
}
