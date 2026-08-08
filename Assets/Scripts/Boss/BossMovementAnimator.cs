using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossMovementAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField, Min(0.01f)] private float frameDuration = 0.08f;
        [SerializeField, Min(0.0001f)] private float movementThreshold = 0.001f;

        private SpriteRenderer spriteRenderer;
        private Sprite idleSprite;
        private Vector3 previousPosition;
        private float frameTimer;
        private int currentFrame;
        private bool isWalking;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            idleSprite = spriteRenderer.sprite;
            previousPosition = transform.position;
        }

        private void LateUpdate()
        {
            var movement = transform.position - previousPosition;
            previousPosition = transform.position;
            var isMoving = movement.sqrMagnitude >= movementThreshold * movementThreshold;

            if (!isMoving || walkFrames == null || walkFrames.Length == 0)
            {
                StopWalking();
                return;
            }

            if (Mathf.Abs(movement.x) > movementThreshold)
            {
                spriteRenderer.flipX = movement.x < 0f;
            }

            if (!isWalking)
            {
                isWalking = true;
                frameTimer = 0f;
                currentFrame = 0;
                spriteRenderer.sprite = walkFrames[currentFrame];
                return;
            }

            frameTimer += Time.deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                currentFrame = (currentFrame + 1) % walkFrames.Length;
                spriteRenderer.sprite = walkFrames[currentFrame];
            }
        }

        private void StopWalking()
        {
            if (!isWalking)
            {
                return;
            }

            isWalking = false;
            frameTimer = 0f;
            currentFrame = 0;
            spriteRenderer.sprite = idleSprite;
        }
    }
}