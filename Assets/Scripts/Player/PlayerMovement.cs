using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private PlayerInputActions input;
    private Rigidbody2D body;

    [SerializeField]
    private float speed;

    [SerializeField]
    private Transform boss;

    [SerializeField]
    private Vector2 referenceHeading;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        body = GetComponent<Rigidbody2D>();
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

    // Update is called once per frame
    void FixedUpdate()
    {
        body.MovePosition(body.position + speed * input.Movement.Movement.ReadValue<Vector2>());

        Vector2 toBoss = boss.position - transform.position;
        float angle = Vector2.SignedAngle(referenceHeading, toBoss);

        body.MoveRotation(angle);
        Debug.Log(angle);
    }
}
