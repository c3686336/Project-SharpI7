using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(SpriteRenderer))]
public sealed class PlayerMoveVideoVisual : MonoBehaviour
{
    [SerializeField] private Sprite[] movementFrames;
    [SerializeField, Min(0.01f)] private float framesPerSecond = 10f;
    [SerializeField] private Sprite[] dashFrames;
    [SerializeField, Min(0.01f)] private float dashFramesPerSecond = 20f;

    private PlayerController playerController;
    private SpriteRenderer spriteRenderer;
    private float elapsed;
    private int frameIndex;
    private bool wasDashing;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (playerController != null && playerController.IsDashing && dashFrames != null && dashFrames.Length > 0)
        {
            PlayDashAnimation();
            return;
        }

        wasDashing = false;

        if (movementFrames == null || movementFrames.Length == 0)
        {
            return;
        }

        if (playerController == null || !playerController.IsMoving)
        {
            elapsed = 0f;
            frameIndex = 0;
            spriteRenderer.sprite = movementFrames[0];
            return;
        }

        var horizontalMovement = playerController.MoveDirection.x;
        if (Mathf.Abs(horizontalMovement) > 0.001f)
        {
            spriteRenderer.flipX = horizontalMovement < 0f;
        }

        elapsed += Time.deltaTime;
        if (elapsed < 1f / framesPerSecond)
        {
            return;
        }

        elapsed = 0f;
        frameIndex = (frameIndex + 1) % movementFrames.Length;
        spriteRenderer.sprite = movementFrames[frameIndex];
    }

    private void PlayDashAnimation()
    {
        if (!wasDashing)
        {
            elapsed = 0f;
            frameIndex = 0;
            wasDashing = true;
        }

        var dashDirection = playerController.DashDirection;
        if (Mathf.Abs(dashDirection.x) > 0.001f)
        {
            spriteRenderer.flipX = dashDirection.x < 0f;
        }

        elapsed += Time.deltaTime;
        frameIndex = Mathf.Min(Mathf.FloorToInt(elapsed * dashFramesPerSecond), dashFrames.Length - 1);
        spriteRenderer.sprite = dashFrames[frameIndex];
    }
}