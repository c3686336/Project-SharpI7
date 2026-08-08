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
        [SerializeField] private Color warningColor = new(1f, 0.08f, 0.08f, 0.35f);
        [SerializeField] private Color explosionColor = new(1f, 0.08f, 0.02f, 0.72f);
        [SerializeField] private Color safeColor = new(0.1f, 1f, 0.35f, 0.42f);
        [SerializeField, Range(64, 256)] private int textureResolution = 128;

        private SpriteRenderer dangerRenderer;
        private SpriteRenderer safeRenderer;
        private Sprite dangerSprite;
        private Sprite safeSprite;
        private Texture2D dangerTexture;
        private Texture2D safeTexture;
        private Coroutine warningRoutine;
        private Transform damageTarget;
        private BossDistanceDangerMode dangerMode;
        private Vector2 anchorPosition;
        private float dangerRadius;
        private float damage;
        private bool finished;

        public void Begin(
            Transform anchor,
            Transform target,
            BossDistanceDangerMode mode,
            Vector2 fieldCenter,
            Vector2 fieldSize,
            float radius,
            float warningDuration,
            float attackDamage)
        {
            damageTarget = target;
            dangerMode = mode;
            anchorPosition = anchor == null ? (Vector2)transform.position : (Vector2)anchor.position;
            dangerRadius = Mathf.Max(0.1f, radius);
            damage = Mathf.Max(0f, attackDamage);
            fieldSize.x = Mathf.Max(dangerRadius * 2f, fieldSize.x) + 2f;
            fieldSize.y = Mathf.Max(dangerRadius * 2f, fieldSize.y) + 2f;

            transform.position = new Vector3(fieldCenter.x, fieldCenter.y, 0f);
            CreateDangerVisual(fieldSize);
            if (dangerMode == BossDistanceDangerMode.OuterDanger)
            {
                CreateSafeMarker();
            }

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
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var pulse = 0.6f + Mathf.PingPong(progress * 4f, 0.4f);
                var dangerColor = warningColor;
                dangerColor.a *= pulse;
                dangerRenderer.color = dangerColor;

                if (safeRenderer != null)
                {
                    var currentSafeColor = safeColor;
                    currentSafeColor.a *= 0.75f + Mathf.PingPong(progress * 3f, 0.25f);
                    safeRenderer.color = currentSafeColor;
                }

                yield return null;
            }

            Explode();
        }

        private void Explode()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            dangerRenderer.color = explosionColor;
            if (safeRenderer != null)
            {
                safeRenderer.color = safeColor;
            }

            if (damageTarget != null)
            {
                var offset = (Vector2)damageTarget.position - anchorPosition;
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
            dangerTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Runtime Boss Distance Danger",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var safeLocalPosition = anchorPosition - (Vector2)transform.position;
            var pixels = new Color32[resolution * resolution];
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var normalizedX = (x + 0.5f) / resolution - 0.5f;
                    var normalizedY = (y + 0.5f) / resolution - 0.5f;
                    var worldX = normalizedX * fieldSize.x;
                    var worldY = normalizedY * fieldSize.y;
                    var offsetX = worldX - safeLocalPosition.x;
                    var offsetY = worldY - safeLocalPosition.y;
                    var isInside = offsetX * offsetX + offsetY * offsetY <= dangerRadius * dangerRadius;
                    var isDangerous = dangerMode == BossDistanceDangerMode.InnerDanger
                        ? isInside
                        : !isInside;
                    pixels[y * resolution + x] = isDangerous
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            dangerTexture.SetPixels32(pixels);
            dangerTexture.Apply();
            dangerSprite = Sprite.Create(
                dangerTexture,
                new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution);

            var overlayObject = new GameObject(
                dangerMode == BossDistanceDangerMode.InnerDanger
                    ? "Move Away - Inner Danger"
                    : "Come Close - Outer Danger Field");
            overlayObject.transform.SetParent(transform, false);
            overlayObject.transform.localScale = new Vector3(fieldSize.x, fieldSize.y, 1f);
            dangerRenderer = overlayObject.AddComponent<SpriteRenderer>();
            dangerRenderer.sprite = dangerSprite;
            dangerRenderer.color = warningColor;
            dangerRenderer.sortingOrder = 104;
        }

        private void CreateSafeMarker()
        {
            const int resolution = 64;
            safeTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Runtime Boss Proximity Safe Zone",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[resolution * resolution];
            var center = (resolution - 1) * 0.5f;
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var isInside = dx * dx + dy * dy <= center * center;
                    pixels[y * resolution + x] = isInside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            safeTexture.SetPixels32(pixels);
            safeTexture.Apply();
            safeSprite = Sprite.Create(
                safeTexture,
                new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution);

            var markerObject = new GameObject("Boss Proximity Safe Zone");
            markerObject.transform.SetParent(transform, false);
            markerObject.transform.position = anchorPosition;
            markerObject.transform.localScale = Vector3.one * (dangerRadius * 2f);
            safeRenderer = markerObject.AddComponent<SpriteRenderer>();
            safeRenderer.sprite = safeSprite;
            safeRenderer.color = safeColor;
            safeRenderer.sortingOrder = 105;
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
            if (dangerSprite != null)
            {
                Destroy(dangerSprite);
            }

            if (safeSprite != null)
            {
                Destroy(safeSprite);
            }

            if (dangerTexture != null)
            {
                Destroy(dangerTexture);
            }

            if (safeTexture != null)
            {
                Destroy(safeTexture);
            }
        }
    }
}