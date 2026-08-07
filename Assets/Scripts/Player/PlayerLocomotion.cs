using UnityEngine;

internal sealed class PlayerLocomotion
{
    private readonly Rigidbody2D rigidbody2D;
    private readonly Transform target;
    private readonly float moveSpeed;
    private readonly Vector2 referenceHeading;

    public PlayerLocomotion(
        Rigidbody2D rigidbody2D,
        Transform target,
        float moveSpeed,
        Vector2 referenceHeading)
    {
        this.rigidbody2D = rigidbody2D;
        this.target = target;
        this.moveSpeed = moveSpeed;
        this.referenceHeading = referenceHeading;
    }

    public Vector2 CurrentMovement { get; private set; }

    public void FixedTick(Vector2 movement, bool canMove)
    {
        CurrentMovement = movement;
        if (canMove)
        {
            rigidbody2D.MovePosition(rigidbody2D.position + moveSpeed * CurrentMovement);
        }

        Vector2 toTarget = target.position - rigidbody2D.transform.position;
        rigidbody2D.MoveRotation(Vector2.SignedAngle(referenceHeading, toTarget));
    }

    public void Stop()
    {
        CurrentMovement = Vector2.zero;
    }
}
