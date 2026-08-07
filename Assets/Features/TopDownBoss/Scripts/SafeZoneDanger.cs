using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    public sealed class SafeZoneDanger : MonoBehaviour
    {
        [SerializeField] private Color dangerColor = new(0.85f, 0.02f, 0.02f, 0.32f);
        [SerializeField] private Color explosionColor = new(1f, 0.08f, 0.02f, 0.62f);
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
        private Vector2 safeWorldPosition;
        private float safeRadius;
        private float damage;
        private bool finished;

        public void Begin(
            Transform target,
            Vector2 safePosition,
            Vector2 fieldSize,
            float radius,
            float warningDuration,
            float attackDamage)
        {
            damageTarget = target;
            safeWorldPosition = safePosition;
            safeRadius = Mathf.Max(0.1f, radius);
            damage = Mathf.Max(0f, attackDamage);

            fieldSize.x = Mathf.Max(safeRadius * 2f, fieldSize.x);
            fieldSize.y = Mathf.Max(safeRadius * 2f, fieldSize.y);

            CreateDangerOverlay(fieldSize);
            CreateSafeMarker();
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
                var pulse = 0.65f + Mathf.PingPong(progress * 4f, 0.35f);

                var currentDangerColor = dangerColor;
                currentDangerColor.a *= pulse;
                dangerRenderer.color = currentDangerColor;

                var currentSafeColor = safeColor;
                currentSafeColor.a *= 0.75f + Mathf.PingPong(progress * 3f, 0.25f);
                safeRenderer.color = currentSafeColor;
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
            safeRenderer.color = safeColor;

            if (damageTarget != null)
            {
                var playerOffset = (Vector2)damageTarget.position - safeWorldPosition;
                if (playerOffset.sqrMagnitude > safeRadius * safeRadius)
                {
                    ApplyDamage(damageTarget, damage);
                }
            }

            Destroy(gameObject, 0.2f);
        }

        private void CreateDangerOverlay(Vector2 fieldSize)
        {
            var resolution = Mathf.Max(64, textureResolution);
            dangerTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Runtime Safe Zone Danger Field",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var safeLocalPosition = safeWorldPosition - (Vector2)transform.position;
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
                    var insideSafeZone = offsetX * offsetX + offsetY * offsetY <= safeRadius * safeRadius;
                    pixels[y * resolution + x] = insideSafeZone
                        ? new Color32(255, 255, 255, 0)
                        : new Color32(255, 255, 255, 255);
                }
            }

            dangerTexture.SetPixels32(pixels);
            dangerTexture.Apply();
            dangerSprite = Sprite.Create(
                dangerTexture,
                new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution);
            dangerSprite.name = "Runtime Safe Zone Danger Field";

            var overlayObject = new GameObject("Danger Field");
            overlayObject.transform.SetParent(transform, false);
            overlayObject.transform.localScale = new Vector3(fieldSize.x, fieldSize.y, 1f);
            dangerRenderer = overlayObject.AddComponent<SpriteRenderer>();
            dangerRenderer.sprite = dangerSprite;
            dangerRenderer.color = dangerColor;
            dangerRenderer.sortingOrder = 100;
        }

        private void CreateSafeMarker()
        {
            const int resolution = 64;
            safeTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Runtime Safe Zone Marker",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[resolution * resolution];
            var center = (resolution - 1) * 0.5f;
            var maxRadius = center;
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var inside = dx * dx + dy * dy <= maxRadius * maxRadius;
                    pixels[y * resolution + x] = inside
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
            safeSprite.name = "Runtime Safe Zone Marker";

            var markerObject = new GameObject("Safe Zone");
            markerObject.transform.SetParent(transform, false);
            markerObject.transform.position = safeWorldPosition;
            markerObject.transform.localScale = Vector3.one * (safeRadius * 2f);
            safeRenderer = markerObject.AddComponent<SpriteRenderer>();
            safeRenderer.sprite = safeSprite;
            safeRenderer.color = safeColor;
            safeRenderer.sortingOrder = 101;
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
