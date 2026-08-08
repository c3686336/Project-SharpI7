using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using SharpI7.Balance;
using SharpI7.Combat;
using UnityEngine;
using System.Threading;
using DG.Tweening;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public sealed class PlayerController : MonoBehaviour,
    IPlayerHealth,
    IPlayerMana,
    IPlayerDash
{
    [SerializeField, Min(0f)] private float spellImpactDelay = 0.1f;
    [SerializeField] private GameObject homingFireProjectilePrefab;
    [SerializeField] private GameObject homingFireProjectileLevelTwoPrefab;
    [SerializeField] private GameObject homingFireProjectileLevelThreePrefab;
    [SerializeField, Min(0.01f)] private float homingFireProjectileSpeed = 12f;
    [SerializeField, Min(0.01f)] private float homingFireProjectileScale = 1.8f;
    [SerializeField, Min(0.01f)] private float homingFireProjectileLevelTwoScaleMultiplier = 1.5f;
    [SerializeField, Min(0.01f)] private float homingFireProjectileLevelThreeScaleMultiplier = 2f;
    [SerializeField, Min(0.01f)] private float homingFireHitRadius = 0.12f;
    [SerializeField, Min(0.1f)] private float homingFireLifetime = 3f;
    [SerializeField] private Vector2 referenceHeading;
    [SerializeField] private ChantManager chantManager;
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private Color flashColor;

    [Header("Spell Effects")]
    [SerializeField] private SpellEffectRegistry spellEffectRegistry;
    [SerializeField] private Transform lightningEffectOrigin;

    private PlayerInputActions input;
    private PlayerHealth health;
    private PlayerMana mana;
    private PlayerLocomotion locomotion;
    private PlayerDash dash;
    private PlayerSpellCaster fireSpellCaster;
    private LightningSpellCaster lightningSpellCaster;
    private SpellCastRouter spellCastRouter;
    private PlayerBalance balance;
    private Coroutine chantInterruptRoutine;
    private bool isMovementLocked;
    private bool combatEnded;
    private SpriteRenderer sr;

    public event Action<float, float> HealthChanged;
    public event Action<ManaStatus> ManaStatusChanged;
    public event Action Died;
    public event Action<float> tookDamage;

    public float DashCooldownUntil => dash?.CooldownUntil ?? 0f;
    public float DashCooldownProgress => dash?.CooldownProgress ?? 0f;
    public bool IsDashing => dash?.IsDashing ?? false;
    public Vector2 DashDirection => dash?.DashDirection ?? Vector2.right;
    public bool IsChanting => chantManager != null && chantManager.IsCasting;
    public bool HasChantInput => IsChanting && !string.IsNullOrEmpty(chantManager.CurrentInput);
    public Vector2 MoveDirection => locomotion?.CurrentMovement ?? Vector2.zero;
    public bool IsMoving => locomotion != null && locomotion.CurrentMovement.sqrMagnitude > 0.001f &&
                            !isMovementLocked && !IsDashing && health != null && health.IsAlive;
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
        health = new PlayerHealth(
            balance.maxHealth,
            balance.invincibilityDuration,
            destroyCancellationToken);
        health.InvincibilityStarted += OnInvincibilityStarted;

        mana = new PlayerMana(
            balance.mana.defaultValue,
            balance.mana.warningThreshold,
            balance.mana.overloadThreshold,
            balance.mana.saturationThreshold,
            balance.mana.fillSpeed);

        if (chantManager == null)
        {
            Debug.LogError("PlayerController requires a ChantManager reference.", this);
            enabled = false;
            return;
        }

        chantManager.SetManaSource(this);

        Rigidbody2D rigidbody2D = GetComponent<Rigidbody2D>();

        locomotion = new PlayerLocomotion(
            rigidbody2D,
            balance.moveSpeed,
            referenceHeading
        );

        dash = new PlayerDash(
            rigidbody2D,
            () => input.Movement.Movement.ReadValue<Vector2>(),
            balance.dash.windupDuration,
            balance.dash.cooldownDuration,
            balance.dash.duration,
            balance.dash.distance,
            destroyCancellationToken);

        BuildSpellCasters();
        combatEnded = bossHealth == null;

        sr = GetComponent<SpriteRenderer>();

        if (spellEffectRegistry == null)
        {
            Debug.LogWarning("[PlayerController] SpellEffectRegistry가 연결되지 않았습니다. 번개 공격 데미지는 들어가지만 이펙트는 표시되지 않습니다.", this);
        }
    }

    private void OnEnable()
    {
        if (input == null || chantManager == null || dash == null)
            return;

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

    private void OnDestroy()
    {
        lightningSpellCaster?.Dispose();
    }

    private void Update()
    {
        if (InGameManager.GameplayInputBlocked || !health.IsAlive || combatEnded)
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
        if (InGameManager.GameplayInputBlocked || !health.IsAlive || combatEnded)
        {
            return;
        }

        locomotion.FixedTick(
            input.Movement.Movement.ReadValue<Vector2>(),
            !dash.IsDashing && !isMovementLocked
        );
    }

    private void BuildSpellCasters()
    {
        lightningSpellCaster?.Dispose();

        fireSpellCaster = null;
        lightningSpellCaster = null;
        spellCastRouter = null;

        if (bossHealth == null)
            return;

        fireSpellCaster = new PlayerSpellCaster(bossHealth);

        Transform effectOrigin = lightningEffectOrigin != null
            ? lightningEffectOrigin
            : transform;

        lightningSpellCaster = new LightningSpellCaster(
            bossHealth,
            effectOrigin,
            spellEffectRegistry,
            destroyCancellationToken
        );

        spellCastRouter = new SpellCastRouter(
            fireSpellCaster,
            lightningSpellCaster
        );
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

    public void SetCombatTarget(BossHealth newBossHealth)
    {
        if (bossHealth != null)
        {
            bossHealth.Died -= HandleBossDied;
        }

        bossHealth = newBossHealth;
        BuildSpellCasters();

        combatEnded = bossHealth == null;
        isMovementLocked = false;

        if (bossHealth != null)
        {
            bossHealth.Died -= HandleBossDied;
            bossHealth.Died += HandleBossDied;
            input?.Movement.Enable();
        }
    }

    private void DeductMana(float amount)
    {
        if (mana.Deduct(amount))
        {
            PublishManaStatus();
        }
    }

    private void HandleBossDied()
    {
        combatEnded = true;
        lightningSpellCaster?.Dispose();

        locomotion.Stop();
        dash.Stop();

        if (chantManager.IsCasting)
        {
            chantManager.InterruptChant();
        }

        isMovementLocked = false;
    }

    private void HandleChantCast(CastResult result)
    {
        DeductMana(result.manaCost);
        StartCoroutine(ApplySpellDamageAfterDelay(result));
        UnlockMovement();
    }

    private IEnumerator ApplySpellDamageAfterDelay(CastResult result)
    {
        if (spellImpactDelay > 0f)
        {
            yield return new WaitForSeconds(spellImpactDelay);
        }

        if (bossHealth == null || !bossHealth.IsAlive)
        {
            yield break;
        }

        if (result.magicType != "Fire")
        {
            spellCastRouter?.Cast(result);
            yield break;
        }

        var projectilePrefab = GetHomingFireProjectilePrefab(result.castLevel);


        var spawnPosition = transform.position;
        spawnPosition.z = 0f;
        var projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        projectile.transform.localScale *= homingFireProjectileScale * GetHomingFireProjectileScaleMultiplier(result.castLevel);
        var homingFire = projectile.AddComponent<HomingFireProjectile>();
        homingFire.Initialize(
            bossHealth,
            result,
            homingFireProjectileSpeed,
            homingFireHitRadius,
            homingFireLifetime);
    }
    private float GetHomingFireProjectileScaleMultiplier(int spellLevel)
    {
        if (spellLevel >= 3)
        {
            return homingFireProjectileLevelThreeScaleMultiplier;
        }

        return spellLevel >= 2 ? homingFireProjectileLevelTwoScaleMultiplier : 1f;
    }
    private GameObject GetHomingFireProjectilePrefab(int spellLevel)
    {
        if (spellLevel >= 3 && homingFireProjectileLevelThreePrefab != null)
        {
            return homingFireProjectileLevelThreePrefab;
        }

        if (spellLevel >= 2 && homingFireProjectileLevelTwoPrefab != null)
        {
            return homingFireProjectileLevelTwoPrefab;
        }

        return homingFireProjectilePrefab;
    }

    private void ApplyDamage(float amount, bool ignoreDashInvulnerability)
    {
        if (!health.IsAlive || amount <= 0f || (!ignoreDashInvulnerability && dash.IsDashing))
            return;

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
            return;

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

    private void OnInvincibilityStarted(CancellationToken token)
    {
        FlashRed(token).Forget();
    }

    private async UniTaskVoid FlashRed(CancellationToken token)
    {
        if (sr == null)
        {
            return;
        }

        Color originalColor = sr.color;
        Sequence flashTween = DOTween.Sequence()
            .Append(sr
                .DOColor(flashColor, 0.1f)
                .SetEase(Ease.InOutSine))
            .Append(sr
                .DOColor(originalColor, 0.1f)
                .SetEase(Ease.InOutSine))
            .SetLoops(-1, LoopType.Restart)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        try
        {
            await UniTask.WaitUntilCanceled(token);
        }
        finally
        {
            if (flashTween.IsActive())
            {
                flashTween.Kill();
            }

            if (sr != null)
            {
                sr.color = originalColor;
            }
        }
    }
}
