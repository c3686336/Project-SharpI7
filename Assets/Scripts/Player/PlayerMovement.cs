using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class PlayerMovement : MonoBehaviour
{
    private PlayerInputActions input;
    private Rigidbody2D rb;
    private bool isDashing;
    private bool isDashCooldown;
    private Vector2 currentMovement;

    [SerializeField]
    private float speed;

    [SerializeField]
    private Transform boss;

    [SerializeField]
    private Vector2 referenceHeading;

    [SerializeField]
    private int dashPreCoolDownMs;

    [SerializeField]
    private int dashCoolDownMs;

    [SerializeField]
    private float dashAnimationDurationS;

    [SerializeField]
    private float dashDistance;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
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
    }

    async UniTaskVoid Dash()
    {
        if (isDashCooldown || isDashing) return;

        var ct = destroyCancellationToken;
        
        isDashCooldown = true;
        await UniTask.Delay(dashPreCoolDownMs, cancellationToken: ct);
        isDashing = true;
        Debug.Log("adsf123");
        await rb.DOMove(currentMovement * dashDistance, dashAnimationDurationS).SetRelative().SetEase(Ease.InOutQuad).ToUniTask(cancellationToken: ct);
        Debug.Log("adsf345");
        isDashing = false;

        await UniTask.Delay(dashCoolDownMs, cancellationToken: ct);
        isDashCooldown = false;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        currentMovement = input.Movement.Movement.ReadValue<Vector2>();
        if (!isDashing) {
            rb.MovePosition(rb.position + speed * currentMovement); // Diagonal movement handled by input action system
        }

        Vector2 toBoss = boss.position - transform.position;
        float angle = Vector2.SignedAngle(referenceHeading, toBoss);

        rb.MoveRotation(angle);
    }
}
