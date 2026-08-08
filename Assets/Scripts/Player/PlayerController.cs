using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using SharpI7.Balance;
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
    [SerializeField] private Vector2 referenceHeading;
    [SerializeField] private ChantManager chantManager;
    [SerializeField] private BossHealth bossHealth;

    private PlayerInputActions input;
    private PlayerHealth health;
    private PlayerMana mana;
    private PlayerLocomotion locomotion;
    private PlayerDash dash;
    private PlayerSpellCaster spellCaster;
    private PlayerBalance balance;
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
    public bool IsChanting => chantManager != null && chantManager.IsCasting;
    public bool HasChantInput => IsChanting && !string.IsNullOrEmpty(chantManager.CurrentInput);
    public Vector2 MoveDirection => locomotion?.CurrentMovement ?? Vector2.zero;
    public bool IsMoving => locomotion != null && locomotion.CurrentMovement.sqrMagnitude > 0.001f &&
                            !isMovementLocked && !IsDashing && !combatEnded && health != null && health.IsAlive;
    public float MaxHealth => health?.Maximum ?? balance?.maxHealth ?? 0f;
    public float CurrentHealth => health?.Current ?? 0f;
    public bool IsAlive => health?.IsAlive ?? false;
    public float MaxMana => mana?.SaturationThreshold ?? balance?.mana?.saturationThreshold ?? 0f;
    public float CurrentMana => mana?.Current ?? 0f;
    public ManaStatus ManaStatus => mana?.Status ?? default;

    private void Awake()
    {
        balance = BalanceDataLoader.Current.player;
        input = new PlayerInputActions();
        health = new PlayerHealth(balance.maxHealth);
        mana = new PlayerMana(
            balance.mana.defaultValue,
            balance.mana.warningThreshold,
            balance.mana.saturationThreshold,
            balance.mana.fillSpeed,
            balance.mana.saturationDuration);

        if (chantManager == null || bossHealth == null)
        {
            Debug.LogError(
                "PlayerController requires ChantManager and BossHealth references.",
                this);
            enabled = false;
            return;
        }

        chantManager.SetManaSource(this);

        Rigidbody2D rigidbody2D = GetComponent<Rigidbody2D>();
        locomotion = new PlayerLocomotion(
            rigidbody2D,
            bossHealth.transform,
            balance.moveSpeed,
            referenceHeading);
        dash = new PlayerDash(
            rigidbody2D,
            () => locomotion.CurrentMovement,
            balance.dash.windupDuration,
            balance.dash.cooldownDuration,
            balance.dash.duration,
            balance.dash.distance,
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
            ApplyDamage(balance.overloadDamage, true);
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
        spellCaster.Cast(result);
        DeductMana(result.manaCost);

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

}
