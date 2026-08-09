using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossMovementAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] private Sprite phaseTwoIdleSprite;
        [SerializeField] private Sprite[] phaseTwoWalkFrames;
        [SerializeField] private Vector3 phaseTwoScaleMultiplier = new(1.24f, 1.11f, 1f);
        [SerializeField, Min(0.01f)] private float frameDuration = 0.08f;
        [SerializeField, Min(0.0001f)] private float movementThreshold = 0.001f;

        private SpriteRenderer spriteRenderer;
        private BossHealth bossHealth;
        private BossMovement bossMovement;
        private Sprite phaseOneIdleSprite;
        private Sprite idleSprite;
        private Sprite[] activeWalkFrames;
        private Vector3 phaseOneScale;
        private Vector3 previousPosition;
        private float frameTimer;
        private int currentFrame;
        private bool isWalking;
        private bool attackAnimationPlaying;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            bossHealth = GetComponent<BossHealth>();
            bossMovement = GetComponent<BossMovement>();
            phaseOneIdleSprite = spriteRenderer.sprite;
            phaseOneScale = transform.localScale;
            SetPhaseVisual(bossHealth != null && bossHealth.IsPhaseTwo);
            previousPosition = transform.position;
        }

        private void OnEnable()
        {
            if (bossHealth == null)
            {
                bossHealth = GetComponent<BossHealth>();
            }

            if (bossMovement == null)
            {
                bossMovement = GetComponent<BossMovement>();
            }
            if (bossHealth != null)
            {
                bossHealth.PhaseTwoStarted += OnPhaseTwoStarted;
            }
        }

        private void OnDisable()
        {
            if (bossHealth != null)
            {
                bossHealth.PhaseTwoStarted -= OnPhaseTwoStarted;
            }
        }

        private void LateUpdate()
        {
            if (attackAnimationPlaying)
            {
                return;
            }
            var movement = transform.position - previousPosition;
            previousPosition = transform.position;
            var isMoving = bossMovement != null
                ? bossMovement.IsMoving
                : movement.sqrMagnitude >= movementThreshold * movementThreshold;

            if (!isMoving || activeWalkFrames == null || activeWalkFrames.Length == 0)
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
                spriteRenderer.sprite = activeWalkFrames[currentFrame];
                return;
            }

            frameTimer += Time.deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                currentFrame = (currentFrame + 1) % activeWalkFrames.Length;
                spriteRenderer.sprite = activeWalkFrames[currentFrame];
            }
        }


        /// <summary>Temporarily gives BossAttackAnimator exclusive control of the sprite.</summary>
        public void SetAttackAnimationPlaying(bool isPlaying)
        {
            attackAnimationPlaying = isPlaying;
            if (isPlaying)
            {
                isWalking = false;
                frameTimer = 0f;
                return;
            }

            if (bossMovement == null || !bossMovement.IsMoving)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }
        private void StopWalking()
        {
            isWalking = false;
            frameTimer = 0f;
            currentFrame = 0;
            if (idleSprite != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }
        private void OnPhaseTwoStarted()
        {
            SetPhaseVisual(true);
        }

        /// <summary>
        /// Restores the size authored on the boss prefab after it is spawned at runtime.
        /// Stage spawning can run after Awake, so keep the animator's cached base scale in sync.
        /// </summary>
        public void SetBaseScale(Vector3 baseScale)
        {
            phaseOneScale = baseScale;

            var usePhaseTwoVisual = bossHealth != null && bossHealth.IsPhaseTwo;
            transform.localScale = Vector3.Scale(
                phaseOneScale,
                usePhaseTwoVisual ? phaseTwoScaleMultiplier : Vector3.one);
        }
        /// <summary>Immediately selects the correct idle visual for the current boss phase.</summary>
        public void RefreshVisual()
        {
            SetPhaseVisual(bossHealth != null && bossHealth.IsPhaseTwo);
        }
        private void SetPhaseVisual(bool usePhaseTwoVisual)
        {
            var hasPhaseTwoVisual = phaseTwoIdleSprite != null;
            idleSprite = usePhaseTwoVisual && hasPhaseTwoVisual ? phaseTwoIdleSprite : phaseOneIdleSprite;
            activeWalkFrames = usePhaseTwoVisual && phaseTwoWalkFrames != null && phaseTwoWalkFrames.Length > 0
                ? phaseTwoWalkFrames
                : walkFrames;

            isWalking = false;
            frameTimer = 0f;
            currentFrame = 0;
            transform.localScale = Vector3.Scale(phaseOneScale, usePhaseTwoVisual ? phaseTwoScaleMultiplier : Vector3.one);

            if (spriteRenderer != null && idleSprite != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }
    }
}