using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(SpriteRenderer))]
public sealed class PlayerMoveVideoVisual : MonoBehaviour
{
    [SerializeField] private Sprite[] movementFrames;
    [SerializeField, Min(0.01f)] private float framesPerSecond = 10f;
    [SerializeField] private Sprite[] dashFrames;
    [SerializeField, Min(0.01f)] private float dashFramesPerSecond = 10f;
    [SerializeField, Min(0.01f)] private float dashFrameBlendDuration = 0.1f;

    private PlayerController playerController;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer previousDashRenderer;
    private float elapsed;
    private float previousDashFadeRemaining;
    private int frameIndex;
    private int dashFrameIndex = -1;
    private bool wasDashing;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        CreatePreviousDashRenderer();
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

        if (wasDashing)
        {
            BeginPreviousDashFade(spriteRenderer.sprite);
        }

        wasDashing = false;
        UpdatePreviousDashFade();

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
            dashFrameIndex = -1;
            wasDashing = true;
            HidePreviousDashFrame();
        }

        var dashDirection = playerController.DashDirection;
        if (Mathf.Abs(dashDirection.x) > 0.001f)
        {
            spriteRenderer.flipX = dashDirection.x < 0f;
        }

        elapsed += Time.deltaTime;
        var nextDashFrameIndex = Mathf.Min(
            Mathf.FloorToInt(elapsed * dashFramesPerSecond),
            dashFrames.Length - 1);

        if (nextDashFrameIndex != dashFrameIndex)
        {
            if (dashFrameIndex >= 0)
            {
                BeginPreviousDashFade(spriteRenderer.sprite);
            }

            dashFrameIndex = nextDashFrameIndex;
            spriteRenderer.sprite = dashFrames[dashFrameIndex];
        }

        UpdatePreviousDashFade();
    }

    private void CreatePreviousDashRenderer()
    {
        var previousFrameObject = new GameObject("Previous Dash Frame");
        previousFrameObject.transform.SetParent(transform, false);

        previousDashRenderer = previousFrameObject.AddComponent<SpriteRenderer>();
        previousDashRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        previousDashRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        previousDashRenderer.enabled = false;
    }

    private void BeginPreviousDashFade(Sprite sprite)
    {
        if (previousDashRenderer == null || sprite == null)
        {
            return;
        }

        previousDashRenderer.sprite = sprite;
        previousDashRenderer.flipX = spriteRenderer.flipX;
        previousDashRenderer.color = Color.white;
        previousDashRenderer.enabled = true;
        previousDashFadeRemaining = dashFrameBlendDuration;
    }

    private void UpdatePreviousDashFade()
    {
        if (previousDashRenderer == null || !previousDashRenderer.enabled)
        {
            return;
        }

        previousDashFadeRemaining -= Time.deltaTime;
        var alpha = Mathf.Clamp01(previousDashFadeRemaining / dashFrameBlendDuration);
        previousDashRenderer.color = new Color(1f, 1f, 1f, alpha);

        if (previousDashFadeRemaining <= 0f)
        {
            HidePreviousDashFrame();
        }
    }

    private void HidePreviousDashFrame()
    {
        if (previousDashRenderer != null)
        {
            previousDashRenderer.enabled = false;
        }
    }
}