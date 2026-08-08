using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    public sealed class SlowWobbleOrb : MonoBehaviour
    {
        [SerializeField] private Color orbColor = new(1f, 0.08f, 0.02f, 0.72f);
        [SerializeField, Range(32, 128)] private int circleResolution = 64;

        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private Transform damageTarget;
        private Vector2 startPosition;
        private Vector2 forwardDirection;
        private Vector2 perpendicularDirection;
        private Bounds despawnBounds;
        private float movementSpeed;
        private float wobbleAmplitude;
        private float wobbleFrequency;
        private float collisionRadius;
        private float damage;
        private float elapsedTime;
        private float forwardDistance;
        private bool initialized;
        private bool despawning;

        public void Begin(
            Transform target,
            Vector2 fixedDirection,
            float speed,
            float amplitude,
            float frequency,
            float radius,
            float attackDamage,
            Bounds playAreaBounds)
        {
            damageTarget = target;
            startPosition = transform.position;
            forwardDirection = fixedDirection.sqrMagnitude > 0.001f
                ? fixedDirection.normalized
                : Vector2.right;
            perpendicularDirection = new Vector2(-forwardDirection.y, forwardDirection.x);
            movementSpeed = Mathf.Max(0.01f, speed);
            wobbleAmplitude = Mathf.Max(0f, amplitude);
            wobbleFrequency = Mathf.Max(0f, frequency);
            collisionRadius = Mathf.Max(0.05f, radius);
            damage = Mathf.Max(0f, attackDamage);
            despawnBounds = playAreaBounds;
            despawnBounds.Expand(collisionRadius * 2f);

            CreateOrbVisual();
            initialized = true;
        }

        public void Cancel()
        {
            Despawn();
        }

        private void Update()
        {
            if (!initialized || despawning)
            {
                return;
            }

            elapsedTime += Time.deltaTime;
            forwardDistance += movementSpeed * Time.deltaTime;
            var wobbleOffset = Mathf.Sin(elapsedTime * wobbleFrequency * Mathf.PI * 2f)
                * wobbleAmplitude;
            var nextPosition = startPosition
                + forwardDirection * forwardDistance
                + perpendicularDirection * wobbleOffset;
            transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);

            if (HitsPlayer())
            {
                ApplyDamage(damageTarget, damage);
                Despawn();
                return;
            }

            if (!despawnBounds.Contains(transform.position))
            {
                Despawn();
            }
        }

        private bool HitsPlayer()
        {
            if (damageTarget == null)
            {
                return false;
            }

            var offset = (Vector2)(damageTarget.position - transform.position);
            return offset.sqrMagnitude <= collisionRadius * collisionRadius;
        }

        private void CreateOrbVisual()
        {
            var resolution = Mathf.Max(32, circleResolution);
            runtimeTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Runtime Slow Wobble Orb",
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

            runtimeTexture.SetPixels32(pixels);
            runtimeTexture.Apply();
            runtimeSprite = Sprite.Create(
                runtimeTexture,
                new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution);
            runtimeSprite.name = "Runtime Slow Wobble Orb";

            var orbRenderer = gameObject.AddComponent<SpriteRenderer>();
            orbRenderer.sprite = runtimeSprite;
            orbRenderer.color = orbColor;
            orbRenderer.sortingOrder = 103;
            transform.localScale = Vector3.one * (collisionRadius * 2f);
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
