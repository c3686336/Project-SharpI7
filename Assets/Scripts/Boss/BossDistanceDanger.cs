using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    public enum BossDistanceDangerMode
    {
        InnerDanger,
        OuterDanger
    }

    [DisallowMultipleComponent]
    public sealed class BossDistanceDanger : MonoBehaviour
    {
        [SerializeField] private Color warningColor = new(0.9f, 0.02f, 0.02f, 0.38f);
        [SerializeField] private Color explosionColor = new(1f, 0.12f, 0.02f, 0.75f);
        [SerializeField, Range(64, 256)] private int textureResolution = 128;

        private SpriteRenderer dangerRenderer;
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private Coroutine warningRoutine;
        private Transform followTarget;
        private Transform damageTarget;
        private BossDistanceDangerMode dangerMode;
        private float dangerRadius;
        private float damage;
        private bool finished;

        public void Begin(
            Transform anchor,
            Transform target,
            BossDistanceDangerMode mode,
            Vector2 fieldSize,
            float radius,
            float warningDuration,
            float attackDamage)
        {
            followTarget = anchor;
            damageTarget = target;
            dangerMode = mode;
            dangerRadius = Mathf.Max(0.1f, radius);
            damage = Mathf.Max(0f, attackDamage);
            fieldSize.x = Mathf.Max(dangerRadius * 2f, fieldSize.x);
            fieldSize.y = Mathf.Max(dangerRadius * 2f, fieldSize.y);

            FollowAnchor();
            CreateDangerVisual(fieldSize);
            warningRoutine = StartCoroutine(WarningRoutine(Mathf.Max(0.05f, warningDuration)));
        }

        public void Cancel()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            if (warningRoutine != null)
            {
                StopCoroutine(warningRoutine);
            }

            Destroy(gameObject);
        }

        private IEnumerator WarningRoutine(float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                FollowAnchor();
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var pulse = 0.6f + Mathf.PingPong(progress * 4f, 0.4f);
                var color = warningColor;
                color.a *= pulse;
                dangerRenderer.color = color;
                yield return null;
            }

            FollowAnchor();
            Explode();
        }

        private void FollowAnchor()
        {
            if (followTarget == null)
            {
                return;
            }

            var anchorPosition = followTarget.position;
            anchorPosition.z = 0f;
            transform.position = anchorPosition;
        }

        private void Explode()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            dangerRenderer.color = explosionColor;

            if (damageTarget != null)
            {
                var offset = (Vector2)(damageTarget.position - transform.position);
                var isInside = offset.sqrMagnitude <= dangerRadius * dangerRadius;
                var shouldDamage = dangerMode == BossDistanceDangerMode.InnerDanger
                    ? isInside
                    : !isInside;

                if (shouldDamage)
                {
                    ApplyDamage(damageTarget, damage);
                }
            }

            Destroy(gameObject, 0.2f);
        }

        private void CreateDangerVisual(Vector2 fieldSize)
        {
            var resolution = Mathf.Max(64, textureResolution);
            runtimeTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Runtime Boss Distance Danger",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[resolution * resolution];
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var normalizedX = (x + 0.5f) / resolution - 0.5f;
                    var normalizedY = (y + 0.5f) / resolution - 0.5f;
                    var worldX = normalizedX * fieldSize.x;
                    var worldY = normalizedY * fieldSize.y;
                    var isInside = worldX * worldX + worldY * worldY
                        <= dangerRadius * dangerRadius;
                    var isDangerous = dangerMode == BossDistanceDangerMode.InnerDanger
                        ? isInside
                        : !isInside;
                    pixels[y * resolution + x] = isDangerous
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            runtimeTexture.SetPixels32(pixels);
            runtimeTexture.Apply();
            runtimeSprite = Sprite.Create(
                runtimeTexture,
                new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution);
            runtimeSprite.name = "Runtime Boss Distance Danger";

            var visualObject = new GameObject(
                dangerMode == BossDistanceDangerMode.InnerDanger
                    ? "Move Away - Inner Danger"
                    : "Come Close - Outer Danger");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localScale = new Vector3(fieldSize.x, fieldSize.y, 1f);
            dangerRenderer = visualObject.AddComponent<SpriteRenderer>();
            dangerRenderer.sprite = runtimeSprite;
            dangerRenderer.color = warningColor;
            dangerRenderer.sortingOrder = 104;
        }

        private static void ApplyDamage(Transform target, float amount)
        {
            var behaviours = target.GetComponentsInParent<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IDamageable damageable && damageable.IsAlive)
                {
                    damageable.TakeDamage(amount);
                    return;
                }
            }

            target.SendMessage("TakeDamage", amount, SendMessageOptions.DontRequireReceiver);
        }

        private void OnDestroy()
        {
            if (runtimeSprite != null)
            {
                Destroy(runtimeSprite);
            }

            if (runtimeTexture != null)
            {
                Destroy(runtimeTexture);
            }
        }
    }
}
