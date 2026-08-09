using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    /// <summary>
    /// Plays the boss attack frames in the final portion of a telegraph,
    /// immediately before that telegraph resolves its damage.
    /// The attack is rendered on a child layer so its visual scale never
    /// changes the boss collider or movement scale.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossAttackAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] attackFrames;
        [SerializeField, Min(0.05f)] private float animationDuration = 0.56f;
        [SerializeField, Min(0.1f)] private float attackVisualScale = 1.75f;
        [SerializeField] private bool playDuringPhaseTwo;

        private SpriteRenderer spriteRenderer;
        private SpriteRenderer attackRenderer;
        private BossMovementAnimator movementAnimator;
        private BossHealth bossHealth;
        private Coroutine playbackRoutine;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            movementAnimator = GetComponent<BossMovementAnimator>();
            bossHealth = GetComponent<BossHealth>();
        }

        private void OnDisable()
        {
            StopAnimation();
        }

        public void PlayBeforeImpact(float warningDuration)
        {
            if (!CanPlay())
            {
                return;
            }

            StopAnimation();
            var duration = Mathf.Min(animationDuration, Mathf.Max(0.05f, warningDuration));
            var delay = Mathf.Max(0f, warningDuration - duration);
            playbackRoutine = StartCoroutine(PlayRoutine(delay, duration));
        }

        public void PlayImmediately()
        {
            if (!CanPlay())
            {
                return;
            }

            StopAnimation();
            playbackRoutine = StartCoroutine(PlayRoutine(0f, animationDuration));
        }

        public void StopAnimation()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }

            RestoreBaseRenderer();
            movementAnimator?.SetAttackAnimationPlaying(false);
        }

        private bool CanPlay()
        {
            return attackFrames != null && attackFrames.Length > 0 &&
                   spriteRenderer != null &&
                   (playDuringPhaseTwo || bossHealth == null || !bossHealth.IsPhaseTwo);
        }

        private IEnumerator PlayRoutine(float delay, float duration)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (!CanPlay())
            {
                playbackRoutine = null;
                yield break;
            }

            EnsureAttackRenderer();
            movementAnimator?.SetAttackAnimationPlaying(true);
            // Attack frames replace the base visual while they play, preventing the two animations from overlapping.
            spriteRenderer.enabled = false;
            attackRenderer.enabled = true;

            var frameDuration = duration / attackFrames.Length;
            for (var frameIndex = 0; frameIndex < attackFrames.Length; frameIndex++)
            {
                if (attackFrames[frameIndex] != null)
                {
                    attackRenderer.sprite = attackFrames[frameIndex];
                    attackRenderer.flipX = spriteRenderer.flipX;
                }

                yield return new WaitForSeconds(frameDuration);
            }

            playbackRoutine = null;
            RestoreBaseRenderer();
            movementAnimator?.SetAttackAnimationPlaying(false);
        }

        private void EnsureAttackRenderer()
        {
            if (attackRenderer != null)
            {
                return;
            }

            var attackVisual = new GameObject("Boss Attack Visual");
            attackVisual.transform.SetParent(transform, false);
            attackVisual.transform.localPosition = Vector3.zero;
            attackVisual.transform.localRotation = Quaternion.identity;
            attackVisual.transform.localScale = Vector3.one * attackVisualScale;

            attackRenderer = attackVisual.AddComponent<SpriteRenderer>();
            attackRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            attackRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            attackRenderer.enabled = false;
        }

        private void RestoreBaseRenderer()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }

            if (attackRenderer != null)
            {
                attackRenderer.enabled = false;
            }
        }
    }
}