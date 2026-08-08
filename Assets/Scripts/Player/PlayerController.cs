using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using SharpI7.Combat;
using SharpI7.Visuals;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerController : MonoBehaviour,
    IPlayerHealth,
    IPlayerMana,
    IPlayerDash
{
    [SerializeField, Min(0f)] private float moveSpeed;
    [SerializeField, Min(0f)] private float dashWindupDuration;
    [SerializeField, Min(0f)] private float dashCooldownDuration;
    [SerializeField, Min(0f)] private float dashDuration;
    [SerializeField, Min(0f)] private float dashDistance;
    [SerializeField] private Vector2 referenceHeading;
    [SerializeField, Min(1f)] private float maxHealth = 3f;
    [SerializeField] private float defaultMana = 50f;
    [SerializeField, Min(0f)] private float manaWarningThreshold = 90f;
    [SerializeField, Min(1f)] private float criticalMana = 100f;
    [SerializeField, Min(0f)] private float manaFillSpeed = 10f;
    [SerializeField, Min(0.1f)] private float manaSaturationDamageCooldown = 3f;
    [SerializeField] private ChantManager chantManager;
    [SerializeField] private BossHealth bossHealth;

    private PlayerInputActions input;
    private PlayerHealth health;
    private PlayerMana mana;
    private PlayerLocomotion locomotion;
    private PlayerDash dash;
    private PlayerSpellCaster spellCaster;
    private PlayerDashEffect dashEffectPrefab;
    private Coroutine chantInterruptRoutine;
    private bool isMovementLocked;
    private bool combatEnded;

    public event Action<float, float> HealthChanged;
    public event Action<ManaStatus> ManaStatusChanged;
    public event Action Died;
    public event Action<float> tookDamage;

    public float DashCooldownUntil => dash?.CooldownUntil ?? 0f;
    public float DashCooldownProgress => dash?.CooldownProgress ?? 0f;
    public bool IsDashing => dash?.IsDashing ?? false;
    public float MaxHealth => health?.Maximum ?? maxHealth;
    public float CurrentHealth => health?.Current ?? 0f;
    public bool IsAlive => health?.IsAlive ?? false;
    public float MaxMana => mana?.SaturationThreshold ?? criticalMana;
    public float CurrentMana => mana?.Current ?? 0f;
    public ManaStatus ManaStatus => mana?.Status ?? default;

    private void Awake()
    {
        input = new PlayerInputActions();
        health = new PlayerHealth(maxHealth);
        mana = new PlayerMana(
            defaultMana,
            manaWarningThreshold,
            criticalMana,
            manaFillSpeed,
            manaSaturationDamageCooldown);

        if (chantManager == null || bossHealth == null)
        {
            Debug.LogError(
                "PlayerController requires ChantManager and BossHealth references.",
                this);
            enabled = false;
            return;
        }

        Rigidbody2D rigidbody2D = GetComponent<Rigidbody2D>();
        locomotion = new PlayerLocomotion(
            rigidbody2D,
            bossHealth.transform,
            moveSpeed,
            referenceHeading);
        dash = new PlayerDash(
            rigidbody2D,
            () => locomotion.CurrentMovement,
            dashWindupDuration,
            dashCooldownDuration,
            dashDuration,
            dashDistance,
            destroyCancellationToken,
            SpawnDashEffect);
        spellCaster = new PlayerSpellCaster(bossHealth);

        // Load the prefab and its animation frames before input starts so the
        // first Shift press never has to synchronously load Resources assets.
        dashEffectPrefab = Resources.Load<PlayerDashEffect>("DashEffect");
        PlayerDashEffect.Prewarm();
    }

    private void OnEnable()
    {
        if (input == null || chantManager == null || bossHealth == null || dash == null)
        {
            return;
        }

        input.Movement.Enable();

        chantManager.OnChantStarted += LockMovement;
        chantManager.OnChantCancelled += UnlockMovement;
        chantManager.OnChantInterrupted += UnlockMovement;
        chantManager.OnChantCast += HandleChantCast;
        chantManager.OnChantSubmitRequested += HandleChantSubmit;

        bossHealth.Died -= HandleBossDied;
        bossHealth.Died += HandleBossDied;
    }

    private void OnDisable()
    {
        input?.Movement.Disable();

        if (chantManager != null)
        {
            chantManager.OnChantStarted -= LockMovement;
            chantManager.OnChantCancelled -= UnlockMovement;
            chantManager.OnChantInterrupted -= UnlockMovement;
            chantManager.OnChantCast -= HandleChantCast;
            chantManager.OnChantSubmitRequested -= HandleChantSubmit;
        }

        if (bossHealth != null)
        {
            bossHealth.Died -= HandleBossDied;
        }
    }

    private void Update()
    {
        if (!health.IsAlive || combatEnded)
        {
            return;
        }

        if (input.Movement.Dash.WasPressedThisFrame() && !isMovementLocked)
        {
            dash.ExecuteAsync().Forget();
        }

        if (input.Movement.Spell.WasPressedThisFrame() && !chantManager.IsCasting)
        {
            chantManager.StartChant();
        }

        if (input.Movement.ExitChant.WasPressedThisFrame())
        {
            chantManager.CancelChant();
        }

        bool overloadDamageDue = mana.Tick(Time.deltaTime);
        PublishManaStatus();
        if (overloadDamageDue)
        {
            ApplyDamage(1f, true);
        }
    }

    private void FixedUpdate()
    {
        if (!health.IsAlive || combatEnded)
        {
            return;
        }

        locomotion.FixedTick(
            input.Movement.Movement.ReadValue<Vector2>(),
            !dash.IsDashing && !isMovementLocked);
    }

    private void LockMovement()
    {
        isMovementLocked = true;
    }

    private void UnlockMovement()
    {
        if (!combatEnded)
        {
            isMovementLocked = false;
        }
    }

    public void TakeDamage(float amount)
    {
        tookDamage?.Invoke(amount);

        ApplyDamage(amount, false);
    }

    private void DeductMana(float amount)
    {
        if (mana.Deduct(amount))
        {
            PublishManaStatus();
        }
    }

    private void SpawnDashEffect(Vector2 dashDirection)
    {
        if (dashEffectPrefab == null)
        {
            return;
        }

        // Follow the player's position without inheriting its boss-facing
        // rotation, which keeps the trail opposite to the dash direction.
        var dashEffect = Instantiate(dashEffectPrefab, transform.position, Quaternion.identity);
        dashEffect.Follow(transform);
        dashEffect.Play(dashDirection);
    }

    private void HandleBossDied()
    {
        combatEnded = true;
        locomotion.Stop();
        dash.Stop();

        if (chantManager.IsCasting)
        {
            chantManager.InterruptChant();
        }

        isMovementLocked = true;
        input.Movement.Disable();
    }

    private void HandleChantCast(CastResult result)
    {
        if (spellCaster.TryCast(result, mana.Current))
        {
            DeductMana(result.manaCost);
        }

        UnlockMovement();
    }

    private void ApplyDamage(float amount, bool ignoreDashInvulnerability)
    {
        if (!health.IsAlive || amount <= 0f ||
            (!ignoreDashInvulnerability && dash.IsDashing))
        {
            return;
        }

        RequestChantInterrupt();
        health.TryTakeDamage(amount);
        HealthChanged?.Invoke(health.Current, health.Maximum);

        if (!health.IsAlive)
        {
            LockMovement();
            Died?.Invoke();
            OutGameManager.LoadGameOver();
        }
    }

    private void RequestChantInterrupt()
    {
        if (!chantManager.IsCasting || chantInterruptRoutine != null)
        {
            return;
        }

        chantInterruptRoutine = StartCoroutine(InterruptChantNextFrame());
    }

    private IEnumerator InterruptChantNextFrame()
    {
        yield return null;

        chantInterruptRoutine = null;

        if (chantManager != null && chantManager.IsCasting)
        {
            chantManager.InterruptChant();
        }
    }

    private void PublishManaStatus()
    {
        ManaStatusChanged?.Invoke(mana.Status);
    }

    private void TryResolveChant()
    {
        // 현재 단계 영창을 끝까지 입력하지 않음
        if (!chantManager.CanResolveCurrentStage)
        {
            return;
        }

        // 안전장치:
        // 오타가 있다면 발동하지 않음
        if (chantManager.TypoCount > 0)
        {
            return;
        }

        float manaCost =
            chantManager.CurrentManaCost;

        // 현재 마나 부족
        if (mana.Current < manaCost)
        {
            Debug.Log(
                $"마나 부족. 현재 마나: {mana.Current:0.#}, " +
                $"필요 마나: {manaCost:0.#}",
                this
            );

            return;
        }

        chantManager.ResolveChant();
    }

    private void HandleChantSubmit()
    {
        if (!chantManager.IsCasting)
        {
            return;
        }

        // 오타가 하나라도 있으면
        // Enter 한 번으로 영창 실패/취소
        if (chantManager.TypoCount > 0)
        {
            chantManager.CancelChant();
            return;
        }

        // 아직 현재 단계를 끝까지 입력하지 않았음
        if (!chantManager.CanResolveCurrentStage)
        {
            return;
        }

        // 완벽하게 입력했다면
        // 마나 검사 후 바로 발동
        TryResolveChant();
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
        float displayMaximum = criticalMana + manaFillSpeed * manaSaturationDamageCooldown;
        defaultMana = Mathf.Clamp(defaultMana, 0f, displayMaximum);
    }
#endif
}
