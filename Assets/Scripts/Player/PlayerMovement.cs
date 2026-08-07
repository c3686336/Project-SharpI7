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
    private ISpell spellController;

    [SerializeField]
    private IBoss bossController;

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
    }

    void OnDisable()
    {
        input.Movement.Disable();
    }

    void Update()
    {
        if (input.Movement.Dash.IsPressed())
        {
            Dash().Forget();
        }

        if (input.Movement.Spell.IsPressed())
        {
            LockMovement();
            spellController.Begin();
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
        if (!isDashing && !isMovementLocked) {
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

    public int GetHp()
    {
        return hp;
    }

    public void DealDamage()
    {
        spellController.Cancel();
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
