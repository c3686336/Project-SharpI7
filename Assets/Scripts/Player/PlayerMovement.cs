using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System;

public class PlayerMovement : MonoBehaviour, IPlayer
{
    private PlayerInputActions input;
    private Rigidbody2D rb;
    private bool isDashing;
    private bool isDashCooldown;
    private bool isMovementLocked;
    private Vector2 currentMovement;
    private float dashCoolDownUntil = 0f;
    private int hp;

    [SerializeField]
    private float speed;

    [SerializeField]
    private Transform boss;

    [SerializeField]
    private Vector2 referenceHeading;

    [SerializeField]
    private int dashPreCoolDownS;

    [SerializeField]
    private int dashCoolDownS;

    [SerializeField]
    private float dashAnimationDurationS;

    [SerializeField]
    private float dashDistance;

    [SerializeField]
    private int maxHp;

    [SerializeField]
    private ChantManager chantManager;

    [SerializeField]
    private DummyBoss bossController;

    [SerializeField, Min(0f)]
    private float baseSpellDamage = 10f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        hp = maxHp;
    }

    void Awake()
    {
        input = new PlayerInputActions();
    }

    void OnEnable()
    {
        input.Movement.Enable();

        chantManager.OnChantStarted += LockMovement;
        chantManager.OnChantCancelled += UnLockMovement;
        chantManager.OnChantInterrupted += UnLockMovement;
        chantManager.OnChantCast += HandleChantCast;
    }

    void OnDisable()
    {
        input.Movement.Disable();

        chantManager.OnChantStarted -= LockMovement;
        chantManager.OnChantCancelled -= UnLockMovement;
        chantManager.OnChantInterrupted -= UnLockMovement;
        chantManager.OnChantCast -= HandleChantCast;
    }

    void Update()
    {
        if (input.Movement.Dash.WasPressedThisFrame())
        {
            Dash().Forget();
        }

        if (input.Movement.Spell.WasPressedThisFrame())
        {
            Debug.Log("asdf");
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

    async UniTaskVoid Dash()
    {
        if (isDashCooldown || isDashing || isMovementLocked) return;

        isDashing = true;

        var ct = destroyCancellationToken;

        await UniTask.Delay(TimeSpan.FromSeconds(dashPreCoolDownS), cancellationToken: ct);

        await rb.DOMove(currentMovement * dashDistance, dashAnimationDurationS).SetRelative().SetEase(Ease.InOutQuad).ToUniTask(cancellationToken: ct);
        isDashing = false;

        isDashCooldown = true;
        dashCoolDownUntil = Time.time + dashCoolDownS;
        await UniTask.Delay(TimeSpan.FromSeconds(dashCoolDownS), cancellationToken: ct);
        isDashCooldown = false;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        currentMovement = input.Movement.Movement.ReadValue<Vector2>();
        if (!isDashing && !isMovementLocked)
        {
            rb.MovePosition(rb.position + speed * currentMovement); // Diagonal movement handled by input action system
        }

        Vector2 toBoss = boss.position - transform.position;
        float angle = Vector2.SignedAngle(referenceHeading, toBoss);

        rb.MoveRotation(angle);
    }

    // IPlayer
    public float GetDashCooldownUntil()
    {
        return dashCoolDownUntil;
    }

    public void LockMovement()
    {
        isMovementLocked = true;
    }

    public void UnLockMovement()
    {
        isMovementLocked = false;
    }

    private void HandleChantCast(CastResult result)
    {
        if (!result.completed)
        {
            SpellFailed();
            return;
        }

        float damage = baseSpellDamage * result.powerMultiplier /
            Mathf.Max(1f, result.penaltyMultiplier);

        SpellSucceeded(damage);
    }

    public int GetHp()
    {
        return hp;
    }

    public void DealDamage()
    {
        chantManager.InterruptChant();
        hp--;
    }

    public void SpellSucceeded(float damage)
    {
        bossController.DealDamage(damage);
        UnLockMovement();
    }

    public void SpellFailed()
    {
        UnLockMovement();
    }
}
