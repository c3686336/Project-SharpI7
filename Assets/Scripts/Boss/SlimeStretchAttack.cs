using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SlimeStretchAttack : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float stretchDuration = 0.28f;
        [SerializeField, Min(0.05f)] private float retractDuration = 0.28f;
        [SerializeField, Min(1f)] private float horizontalStretchMultiplier = 4.5f;
        [SerializeField, Min(1f)] private float verticalStretchMultiplier = 10f;
        [SerializeField, Range(0.1f, 1f)] private float verticalWidthMultiplier = 0.45f;
        [SerializeField] private Color warningColor = new(1f, 0.08f, 0.08f, 0.35f);
        [SerializeField] private Color hitColor = new(1f, 0.04f, 0.02f, 0.78f);

        private SpriteRenderer spriteRenderer;
        private BossMovementAnimator movementAnimator;
        private Collider2D bodyCollider;
        private Coroutine routine;
        private GameObject warningObject;
        private Sprite warningSprite;
        private Texture2D warningTexture;
        private Vector3 originalScale;
        private Vector3 originalPosition;
        private Vector3 originalVisualCenter;
        private Vector2 attackCenter;
        private Sprite originalSprite;
        private bool animatorWasEnabled;
        private bool colliderWasEnabled;
        private bool bodyIsStretched;

        public bool IsActive => routine != null;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            movementAnimator = GetComponent<BossMovementAnimator>();
            bodyCollider = GetComponent<Collider2D>();
        }

        public float Begin(Transform target, bool horizontal, float length, float width, float warningDuration, float damage)
        {
            Cancel();
            routine = StartCoroutine(AttackRoutine(target, horizontal, length, width, warningDuration, damage));
            return Mathf.Max(0.05f, warningDuration) + stretchDuration + retractDuration;
        }

        public void Cancel()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            RestoreBody();
            DestroyWarning();
        }

        private void OnDisable()
        {
            Cancel();
        }

        private IEnumerator AttackRoutine(Transform target, bool horizontal, float length, float width, float warningDuration, float damage)
        {
            CreateWarning(horizontal, length, width);
            var duration = Mathf.Max(0.05f, warningDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var color = warningColor;
                color.a *= 0.65f + Mathf.PingPong(elapsed * 4f, 0.35f);
                SetWarningColor(color);
                yield return null;
            }

            DestroyWarning();
            yield return StretchBody(horizontal, length);
            if (IsInsideAttack(target, horizontal, length, width))
            {
                ApplyDamage(target, damage);
            }

            yield return RetractBody();
            RestoreBody();
            routine = null;
        }

        private void CreateWarning(bool horizontal, float length, float width)
        {
            DestroyWarning();
            warningTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Slime Stretch Warning"
            };
            warningTexture.SetPixel(0, 0, Color.white);
            warningTexture.Apply();
            warningSprite = Sprite.Create(warningTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

            warningObject = new GameObject("Slime Stretch Warning");
            // Keep this marker in world space. Parenting it to a scaled boss
            // shrinks the warning so it no longer matches the actual hit area.
            warningObject.transform.position = new Vector3(transform.position.x, transform.position.y, 0.05f);
            warningObject.transform.localScale = horizontal
                ? new Vector3(length, width, 1f)
                : new Vector3(width, length, 1f);

            var renderer = warningObject.AddComponent<SpriteRenderer>();
            renderer.sprite = warningSprite;
            renderer.color = warningColor;
            renderer.sortingOrder = 101;
        }

        private void SetWarningColor(Color color)
        {
            if (warningObject == null)
            {
                return;
            }

            var renderer = warningObject.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = color;
            }
        }

        private IEnumerator StretchBody(bool horizontal, float targetLength)
        {
            if (spriteRenderer == null)
            {
                yield break;
            }

            originalScale = transform.localScale;
            originalPosition = transform.position;
            attackCenter = originalPosition;
            originalVisualCenter = spriteRenderer.bounds.center;
            originalSprite = spriteRenderer.sprite;
            animatorWasEnabled = movementAnimator != null && movementAnimator.enabled;
            colliderWasEnabled = bodyCollider != null && bodyCollider.enabled;
            if (movementAnimator != null)
            {
                movementAnimator.enabled = false;
            }
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            var currentLength = horizontal ? spriteRenderer.bounds.size.x : spriteRenderer.bounds.size.y;
            var configuredMultiplier = horizontal ? horizontalStretchMultiplier : verticalStretchMultiplier;
            var multiplier = Mathf.Max(configuredMultiplier, targetLength / Mathf.Max(0.01f, currentLength));
            var targetScale = horizontal
                ? Vector3.Scale(originalScale, new Vector3(multiplier, 1f, 1f))
                : Vector3.Scale(originalScale, new Vector3(verticalWidthMultiplier, multiplier, 1f));

            bodyIsStretched = true;
            spriteRenderer.color = hitColor;
            var elapsed = 0f;
            while (elapsed < stretchDuration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.SmoothStep(0f, 1f, elapsed / stretchDuration);
                transform.localScale = Vector3.LerpUnclamped(originalScale, targetScale, progress);
                KeepSpriteCenterAtAttackOrigin();
                yield return null;
            }

            transform.localScale = targetScale;
            KeepSpriteCenterAtAttackOrigin();
        }

        private IEnumerator RetractBody()
        {
            if (!bodyIsStretched)
            {
                yield break;
            }

            var stretchedScale = transform.localScale;
            var elapsed = 0f;
            while (elapsed < retractDuration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.SmoothStep(0f, 1f, elapsed / retractDuration);
                transform.localScale = Vector3.LerpUnclamped(stretchedScale, originalScale, progress);
                KeepSpriteCenterAtAttackOrigin();
                yield return null;
            }

            transform.localScale = originalScale;
            transform.position = originalPosition;
        }

        private void KeepSpriteCenterAtAttackOrigin()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            transform.position += originalVisualCenter - spriteRenderer.bounds.center;
        }

        private void RestoreBody()
        {
            if (!bodyIsStretched)
            {
                return;
            }

            transform.localScale = originalScale;
            transform.position = originalPosition;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
                if (originalSprite != null)
                {
                    spriteRenderer.sprite = originalSprite;
                }
            }
            if (bodyCollider != null)
            {
                bodyCollider.enabled = colliderWasEnabled;
            }
            if (movementAnimator != null)
            {
                movementAnimator.enabled = animatorWasEnabled;
                movementAnimator.RefreshVisual();
            }

            originalSprite = null;
            bodyIsStretched = false;
        }

        private bool IsInsideAttack(Transform target, bool horizontal, float length, float width)
        {
            if (target == null)
            {
                return false;
            }

            var offset = target.position - (Vector3)attackCenter;
            var halfLength = Mathf.Max(0.1f, length) * 0.5f;
            var halfWidth = Mathf.Max(0.1f, width) * 0.5f;
            return horizontal
                ? Mathf.Abs(offset.x) <= halfLength && Mathf.Abs(offset.y) <= halfWidth
                : Mathf.Abs(offset.x) <= halfWidth && Mathf.Abs(offset.y) <= halfLength;
        }

        private static void ApplyDamage(Transform target, float damage)
        {
            foreach (var behaviour in target.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (behaviour is IPlayerHealth player && player.IsAlive)
                {
                    player.TakeDamage(damage);
                    return;
                }
            }
        }

        private void DestroyWarning()
        {
            if (warningObject != null)
            {
                Destroy(warningObject);
                warningObject = null;
            }
            if (warningSprite != null)
            {
                Destroy(warningSprite);
                warningSprite = null;
            }
            if (warningTexture != null)
            {
                Destroy(warningTexture);
                warningTexture = null;
            }
        }
    }
}