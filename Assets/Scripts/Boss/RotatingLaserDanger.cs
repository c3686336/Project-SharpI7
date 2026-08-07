using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    public sealed class RotatingLaserDanger : MonoBehaviour
    {
        [SerializeField] private Color warningColor = new(1f, 0.08f, 0.08f, 0.32f);
        [SerializeField] private Color activeColor = new(1f, 0.18f, 0.02f, 0.9f);

        private SpriteRenderer laserRenderer;
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private Coroutine laserRoutine;
        private Transform followTarget;
        private Transform damageTarget;
        private float laserLength;
        private float laserWidth;
        private float damage;
        private bool finished;

        public void Begin(
            Transform anchor,
            Transform target,
            float length,
            float width,
            float warningDuration,
            float activeDuration,
            float damagePerTick,
            float damageTickInterval)
        {
            followTarget = anchor;
            damageTarget = target;
            laserLength = Mathf.Max(0.1f, length);
            laserWidth = Mathf.Clamp(width, 0.1f, laserLength);
            damage = Mathf.Max(0f, damagePerTick);

            FollowAnchor();
            CreateLaserVisual();
            AimAtTarget();

            const float clockwiseSweepDegrees = -350f;
            laserRoutine = StartCoroutine(LaserRoutine(
                Mathf.Max(0.05f, warningDuration),
                Mathf.Max(0.05f, activeDuration),
                clockwiseSweepDegrees,
                Mathf.Max(0.05f, damageTickInterval)));
        }

        public void Cancel()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            if (laserRoutine != null)
            {
                StopCoroutine(laserRoutine);
            }

            Destroy(gameObject);
        }

        private IEnumerator LaserRoutine(
            float warningDuration,
            float activeDuration,
            float signedRotationDegrees,
            float damageTickInterval)
        {
            var elapsed = 0f;
            while (elapsed < warningDuration)
            {
                FollowAnchor();
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / warningDuration);
                var pulse = 0.55f + Mathf.PingPong(progress * 5f, 0.45f);
                var color = warningColor;
                color.a *= pulse;
                laserRenderer.color = color;
                yield return null;
            }

            laserRenderer.color = activeColor;
            var startAngle = transform.eulerAngles.z;
            var nextDamageTime = 0f;
            elapsed = 0f;

            while (elapsed < activeDuration)
            {
                FollowAnchor();
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / activeDuration);
                var currentAngle = startAngle + signedRotationDegrees * progress;
                transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);

                if (elapsed >= nextDamageTime)
                {
                    nextDamageTime = elapsed + damageTickInterval;
                    if (damageTarget != null && ContainsPoint(damageTarget.position))
                    {
                        ApplyDamage(damageTarget, damage);
                    }
                }

                yield return null;
            }

            finished = true;
            Destroy(gameObject, 0.1f);
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

        private void AimAtTarget()
        {
            if (damageTarget == null)
            {
                return;
            }

            var direction = (Vector2)(damageTarget.position - transform.position);
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector2.right;
            }

            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private bool ContainsPoint(Vector3 worldPoint)
        {
            var localPoint = transform.InverseTransformPoint(worldPoint);
            return localPoint.x >= 0f
                && localPoint.x <= laserLength
                && Mathf.Abs(localPoint.y) <= laserWidth * 0.5f;
        }

        private void CreateLaserVisual()
        {
            runtimeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Rotating Laser",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            runtimeTexture.SetPixel(0, 0, Color.white);
            runtimeTexture.Apply();

            runtimeSprite = Sprite.Create(
                runtimeTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            runtimeSprite.name = "Runtime Rotating Laser";

            var laserObject = new GameObject("Rotating Laser Beam");
            laserObject.transform.SetParent(transform, false);
            laserObject.transform.localPosition = new Vector3(laserLength * 0.5f, 0f, 0f);
            laserObject.transform.localScale = new Vector3(laserLength, laserWidth, 1f);
            laserRenderer = laserObject.AddComponent<SpriteRenderer>();
            laserRenderer.sprite = runtimeSprite;
            laserRenderer.color = warningColor;
            laserRenderer.sortingOrder = 102;
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
