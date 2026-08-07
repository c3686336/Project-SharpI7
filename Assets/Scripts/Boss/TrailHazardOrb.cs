using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    public sealed class TrailHazardOrb : MonoBehaviour
    {
        [SerializeField] private Color orbColor = new(0.35f, 0.05f, 0.65f, 0.9f);
        [SerializeField, Range(32, 128)] private int circleResolution = 64;

        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private Transform damageTarget;
        private float collisionRadius;
        private float damage;
        private float remainingLifetime;
        private bool playerWasInside;
        private bool initialized;
        private bool despawning;

        public void Begin(Transform target, float radius, float attackDamage, float lifetime)
        {
            damageTarget = target;
            collisionRadius = Mathf.Max(0.05f, radius);
            damage = Mathf.Max(0f, attackDamage);
            remainingLifetime = Mathf.Max(0.05f, lifetime);

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

            remainingLifetime -= Time.deltaTime;
            if (remainingLifetime <= 0f)
            {
                Despawn();
                return;
            }

            var playerIsInside = IsPlayerInside();
            if (playerIsInside && !playerWasInside)
            {
                ApplyDamage(damageTarget, damage);
            }

            playerWasInside = playerIsInside;
        }

        private bool IsPlayerInside()
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
                name = "Runtime Trail Hazard Orb",
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
            runtimeSprite.name = "Runtime Trail Hazard Orb";

            var orbRenderer = gameObject.AddComponent<SpriteRenderer>();
            orbRenderer.sprite = runtimeSprite;
            orbRenderer.color = orbColor;
            orbRenderer.sortingOrder = 102;
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
