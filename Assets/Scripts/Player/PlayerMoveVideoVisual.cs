using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(SpriteRenderer))]
public sealed class PlayerMoveVideoVisual : MonoBehaviour
{
    [SerializeField] private Sprite[] movementFrames;
    [SerializeField, Min(0.01f)] private float framesPerSecond = 10f;

    private PlayerController playerController;
    private SpriteRenderer spriteRenderer;
    private float elapsed;
    private int frameIndex;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (movementFrames == null || movementFrames.Length == 0 || spriteRenderer == null)
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
            // The supplied character art faces right by default.
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
}
