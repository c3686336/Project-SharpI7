using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossJumpShockwaveAttack : MonoBehaviour
    {
        [Header("Jump Animation")]
        [SerializeField] private Sprite[] jumpFrames;
        [SerializeField] private Sprite[] phaseTwoJumpFrames;
        [SerializeField, Min(0.03f)] private float frameDuration = 0.18f;

        [Header("Dash Shockwave")]
        [SerializeField, Min(0.1f)] private float waveWidth = 1.1f;
        [SerializeField, Min(0.1f)] private float waveSpeed = 12f;
        [SerializeField] private Color waveColor = new(1f, 0.08f, 0.02f, 0.9f);
        [SerializeField, Range(24, 128)] private int circleSegments = 72;

        private SpriteRenderer spriteRenderer;
        private BossMovementAnimator movementAnimator;
        private BossHealth bossHealth;
        private Coroutine attackRoutine;
        private LineRenderer waveRenderer;
        private Material waveMaterial;
        private Sprite originalSprite;
        private bool wasAnimatorEnabled;
        private bool playerWasCrossed;
        private bool visualPrepared;

        public bool IsActive => attackRoutine != null;

        public float Begin(Transform target, Bounds arenaBounds, float damage)
        {
            Cancel();
            attackRoutine = StartCoroutine(AttackRoutine(target, arenaBounds, damage));
            return GetTotalDuration(arenaBounds);
        }

        public void Cancel()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            RestoreBossVisual();
            DestroyWave();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            movementAnimator = GetComponent<BossMovementAnimator>();
            bossHealth = GetComponent<BossHealth>();
        }

        private void OnDisable()
        {
            Cancel();
        }

        private IEnumerator AttackRoutine(Transform target, Bounds arenaBounds, float damage)
        {
            PrepareBossVisual();

            var activeJumpFrames = GetActiveJumpFrames();
            if (activeJumpFrames != null)
            {
                foreach (var frame in activeJumpFrames)
                {
                    if (frame != null)
                    {
                        spriteRenderer.sprite = frame;
                    }

                    yield return new WaitForSeconds(frameDuration);
                }
            }

            CreateWave();
            var waveCenter = (Vector2)transform.position;
            var maxRadius = GetMaxRadius(waveCenter, arenaBounds);
            var radius = 0.05f;
            playerWasCrossed = false;

            while (radius < maxRadius)
            {
                radius += waveSpeed * Time.deltaTime;
                DrawWave(waveCenter, radius);

                if (!playerWasCrossed && IsInsideWave(target, waveCenter, radius))
                {
                    playerWasCrossed = true;
                    ApplyDashableDamage(target, damage);
                }

                yield return null;
            }

            RestoreBossVisual();
            DestroyWave();
            attackRoutine = null;
        }

        private float GetTotalDuration(Bounds arenaBounds)
        {
            var animationDuration = (GetActiveJumpFrames()?.Length ?? 0) * frameDuration;
            return animationDuration + GetMaxRadius(transform.position, arenaBounds) / waveSpeed;
        }

        private static float GetMaxRadius(Vector2 center, Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            return Mathf.Max(
                Vector2.Distance(center, new Vector2(min.x, min.y)),
                Vector2.Distance(center, new Vector2(min.x, max.y)),
                Vector2.Distance(center, new Vector2(max.x, min.y)),
                Vector2.Distance(center, new Vector2(max.x, max.y))) + 1f;
        }

        private Sprite[] GetActiveJumpFrames()
        {
            return bossHealth != null && bossHealth.IsPhaseTwo && phaseTwoJumpFrames != null && phaseTwoJumpFrames.Length > 0
                ? phaseTwoJumpFrames
                : jumpFrames;
        }
        private void PrepareBossVisual()
        {
            visualPrepared = true;
            originalSprite = spriteRenderer.sprite;
            wasAnimatorEnabled = movementAnimator != null && movementAnimator.enabled;
            if (movementAnimator != null)
            {
                movementAnimator.enabled = false;
            }
        }

        private void RestoreBossVisual()
        {
            if (!visualPrepared)
            {
                return;
            }

            if (spriteRenderer != null && originalSprite != null)
            {
                spriteRenderer.sprite = originalSprite;
            }

            if (movementAnimator != null)
            {
                movementAnimator.enabled = wasAnimatorEnabled;
                movementAnimator.RefreshVisual();
            }

            originalSprite = null;
            visualPrepared = false;
        }

        private void CreateWave()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            var waveObject = new GameObject("Dash Shockwave");
            waveObject.transform.SetParent(transform, false);
            waveObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            waveRenderer = waveObject.AddComponent<LineRenderer>();
            waveMaterial = shader != null ? new Material(shader) : null;
            waveRenderer.material = waveMaterial;
            waveRenderer.useWorldSpace = true;
            waveRenderer.loop = true;
            waveRenderer.positionCount = circleSegments;
            waveRenderer.startWidth = waveWidth;
            waveRenderer.endWidth = waveWidth;
            waveRenderer.startColor = waveColor;
            waveRenderer.endColor = waveColor;
            waveRenderer.numCapVertices = 4;
            waveRenderer.sortingOrder = 220;
        }

        private void DrawWave(Vector2 center, float radius)
        {
            if (waveRenderer == null)
            {
                return;
            }

            for (var index = 0; index < circleSegments; index++)
            {
                var angle = index / (float)circleSegments * Mathf.PI * 2f;
                waveRenderer.SetPosition(index, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private bool IsInsideWave(Transform target, Vector2 center, float radius)
        {
            if (target == null)
            {
                return false;
            }

            var targetRadius = 0.35f;
            var collider = target.GetComponentInChildren<Collider2D>();
            if (collider != null)
            {
                targetRadius = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y);
            }

            var distance = Vector2.Distance(center, target.position);
            return Mathf.Abs(distance - radius) <= waveWidth * 0.5f + targetRadius;
        }

        private static void ApplyDashableDamage(Transform target, float damage)
        {
            foreach (var behaviour in target.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (behaviour is IPlayerHealth player && player.IsAlive)
                {
                    player.TakeDamage(damage);
                    return;
                }
            }

            target.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        }

        private void DestroyWave()
        {
            if (waveRenderer != null)
            {
                Destroy(waveRenderer.gameObject);
                waveRenderer = null;
            }

            if (waveMaterial != null)
            {
                Destroy(waveMaterial);
                waveMaterial = null;
            }
        }
    }
}