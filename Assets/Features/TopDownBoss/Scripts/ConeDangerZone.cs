using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    public sealed class ConeDangerZone : MonoBehaviour
    {
        [SerializeField] private Color warningColor = new(1f, 0.2f, 0.05f, 0.42f);
        [SerializeField] private Color impactColor = new(1f, 0.05f, 0.02f, 0.9f);
        [SerializeField, Range(32, 256)] private int textureResolution = 128;

        private SpriteRenderer zoneRenderer;
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private Transform damageTarget;
        private Vector2 lockedOrigin;
        private Vector2 lockedDirection;
        private float attackRange;
        private float halfAngle;
        private float damage;
        private Coroutine attackRoutine;
        private bool despawning;

        public void Begin(
            Transform target,
            Vector2 fixedDirection,
            float range,
            float angle,
            float warningDuration,
            float attackDamage)
        {
            damageTarget = target;
            lockedOrigin = transform.position;
            lockedDirection = fixedDirection.sqrMagnitude > 0.001f
                ? fixedDirection.normalized
                : Vector2.right;
            attackRange = Mathf.Max(0.1f, range);
            halfAngle = Mathf.Clamp(angle, 1f, 359f) * 0.5f;
            damage = Mathf.Max(0f, attackDamage);

            transform.position = new Vector3(lockedOrigin.x, lockedOrigin.y, 0f);
            transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(lockedDirection.y, lockedDirection.x) * Mathf.Rad2Deg);
            CreateConeVisual();
            attackRoutine = StartCoroutine(AttackSequence(Mathf.Max(0.05f, warningDuration)));
        }

        public void Cancel()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            Despawn();
        }

        private IEnumerator AttackSequence(float warningDuration)
        {
            yield return new WaitForSeconds(warningDuration);

            if (zoneRenderer != null)
            {
                zoneRenderer.color = impactColor;
            }

            if (IsTargetInsideCone())
            {
                ApplyDamage(damageTarget, damage);
            }

            yield return new WaitForSeconds(0.15f);
            attackRoutine = null;
            Despawn();
        }

        private bool IsTargetInsideCone()
        {
            if (damageTarget == null)
            {
                return false;
            }

            var offset = (Vector2)damageTarget.position - lockedOrigin;
            var distanceSquared = offset.sqrMagnitude;
            if (distanceSquared > attackRange * attackRange)
            {
                return false;
            }

            if (distanceSquared <= 0.001f)
            {
                return true;
            }

            var minimumDot = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            return Vector2.Dot(lockedDirection, offset.normalized) >= minimumDot;
        }

        private void CreateConeVisual()
        {
            var resolution = Mathf.Max(32, textureResolution);
            runtimeTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Runtime Cone Danger Zone",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[resolution * resolution];
            var minimumDot = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var localX = (x + 0.5f) / resolution * 2f - 1f;
                    var localY = (y + 0.5f) / resolution * 2f - 1f;
                    var radiusSquared = localX * localX + localY * localY;
                    var inside = false;

                    if (radiusSquared <= 1f && radiusSquared > 0.0001f)
                    {
                        inside = localX / Mathf.Sqrt(radiusSquared) >= minimumDot;
                    }

                    pixels[y * resolution + x] = inside
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
            runtimeSprite.name = "Runtime Cone Danger Zone";

            zoneRenderer = gameObject.AddComponent<SpriteRenderer>();
            zoneRenderer.sprite = runtimeSprite;
            zoneRenderer.color = warningColor;
            zoneRenderer.sortingOrder = 90;
            transform.localScale = Vector3.one * (attackRange * 2f);
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

        private void Despawn()
        {
            if (despawning)
            {
                return;
            }

            despawning = true;
            Destroy(gameObject);
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
