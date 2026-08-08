using UnityEngine;

internal sealed class PlayerLocomotion
{
    private readonly Rigidbody2D rigidbody2D;
    private readonly Transform target;
    private readonly float moveSpeed;
    private readonly Vector2 referenceHeading;
    private readonly float boundaryPadding;

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
        var collider = rigidbody2D.GetComponent<Collider2D>();
        boundaryPadding = collider == null
            ? 0f
            : Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y);
    }

    public Vector2 CurrentMovement { get; private set; }

    public void FixedTick(Vector2 movement, bool canMove)
    {
        CurrentMovement = movement;
        if (canMove)
        {
            var nextPosition = rigidbody2D.position + moveSpeed * CurrentMovement;
            rigidbody2D.MovePosition(ArenaBounds.ClampPosition(nextPosition, boundaryPadding));
        }
    }

    public void Stop()
    {
        CurrentMovement = Vector2.zero;
    }
}
