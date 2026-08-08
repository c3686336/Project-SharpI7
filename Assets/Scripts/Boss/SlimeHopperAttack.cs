using System.Collections;
using System.Collections.Generic;
using SharpI7.Balance;
using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SlimeHopperAttack : MonoBehaviour
    {
        [SerializeField, Min(1)] private int minProjectileCount = 3;
        [SerializeField, Min(1)] private int maxProjectileCount = 5;
        [SerializeField, Min(0.01f)] private float warningDuration = 0.2f;
        [SerializeField, Min(0.1f)] private float warningWidth = 0.55f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 8.5f;
        [SerializeField, Min(0.05f)] private float projectileRadius = 0.18f;
        [SerializeField, Min(0.05f)] private float projectileScale = 0.22f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 5f;
        [SerializeField, Min(0.1f)] private float bounceFrequency = 22f;
        [SerializeField, Range(0f, 0.5f)] private float bounceScaleAmount = 0.25f;
        [SerializeField, Range(0f, 0.5f)] private float bounceHeight = 0.07f;

        private SpriteRenderer bodyRenderer;
        private Coroutine launchRoutine;
        private readonly List<SlimeHopperProjectile> activeProjectiles = new();
        private readonly List<SlimeHopperWarning> activeWarnings = new();

        private void Awake()
        {
            bodyRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnDisable()
        {
            Cancel();
        }

        public void Configure(SlimeHopperBalance balance)
        {
            if (balance == null)
            {
                return;
            }

            minProjectileCount = Mathf.Max(1, balance.minCount);
            maxProjectileCount = Mathf.Max(minProjectileCount, balance.maxCount);
            warningDuration = Mathf.Max(0.01f, balance.warningDuration);
            warningWidth = Mathf.Max(0.1f, balance.warningWidth);
            projectileSpeed = Mathf.Max(0.1f, balance.speed);
            projectileRadius = Mathf.Max(0.05f, balance.hitRadius);
            projectileScale = Mathf.Max(0.05f, balance.scale);
            projectileLifetime = Mathf.Max(0.1f, balance.lifetime);
            bounceFrequency = Mathf.Max(0.1f, balance.bounceFrequency);
            bounceScaleAmount = Mathf.Clamp(balance.bounceScaleAmount, 0f, 0.5f);
            bounceHeight = Mathf.Clamp(balance.bounceHeight, 0f, 0.5f);
        }

        public float Begin(Transform target, Bounds playArea, float damage)
        {
            Cancel();
            if (target == null || bodyRenderer == null || bodyRenderer.sprite == null)
            {
                return 0f;
            }

            var projectileCount = Random.Range(minProjectileCount, maxProjectileCount + 1);
            launchRoutine = StartCoroutine(LaunchRoutine(target, playArea, damage, projectileCount));
            return projectileCount * warningDuration;
        }

        private IEnumerator LaunchRoutine(Transform target, Bounds playArea, float damage, int projectileCount)
        {
            for (var index = 0; index < projectileCount; index++)
            {
                if (target == null || bodyRenderer == null || bodyRenderer.sprite == null)
                {
                    break;
                }

                var origin = (Vector2)transform.position;
                var direction = ((Vector2)target.position - origin).normalized;
                if (direction.sqrMagnitude < 0.0001f)
                {
                    direction = Vector2.down;
                }

                var warning = CreateWarning(origin, direction, playArea);
                yield return new WaitForSeconds(warningDuration);

                if (warning != null)
                {
                    activeWarnings.Remove(warning);
                    warning.Cancel();
                }

                SpawnProjectile(origin, direction, playArea, damage);
            }

            launchRoutine = null;
        }

        private SlimeHopperWarning CreateWarning(Vector2 origin, Vector2 direction, Bounds playArea)
        {
            activeWarnings.RemoveAll(warning => warning == null);
            var warningLength = GetDistanceToPlayAreaEdge(origin, direction, playArea);
            var warning = SlimeHopperWarning.Create(origin, direction, warningLength, warningWidth, warningDuration);
            activeWarnings.Add(warning);
            return warning;
        }

        private static float GetDistanceToPlayAreaEdge(Vector2 origin, Vector2 direction, Bounds playArea)
        {
            var distanceX = float.PositiveInfinity;
            var distanceY = float.PositiveInfinity;

            if (Mathf.Abs(direction.x) > 0.0001f)
            {
                var edgeX = direction.x > 0f ? playArea.max.x : playArea.min.x;
                distanceX = (edgeX - origin.x) / direction.x;
            }

            if (Mathf.Abs(direction.y) > 0.0001f)
            {
                var edgeY = direction.y > 0f ? playArea.max.y : playArea.min.y;
                distanceY = (edgeY - origin.y) / direction.y;
            }

            var distance = Mathf.Min(
                distanceX > 0f ? distanceX : float.PositiveInfinity,
                distanceY > 0f ? distanceY : float.PositiveInfinity);

            return float.IsInfinity(distance)
                ? Mathf.Max(playArea.size.x, playArea.size.y)
                : Mathf.Max(0.1f, distance);
        }

        private void SpawnProjectile(Vector2 origin, Vector2 direction, Bounds playArea, float damage)
        {
            var spawnPosition = origin + direction * (bodyRenderer.bounds.extents.magnitude + projectileRadius);
            activeProjectiles.RemoveAll(projectile => projectile == null);
            activeProjectiles.Add(SlimeHopperProjectile.Create(
                spawnPosition,
                direction,
                bodyRenderer.sprite,
                bodyRenderer.color,
                projectileSpeed,
                projectileRadius,
                projectileScale,
                projectileLifetime,
                bounceFrequency,
                bounceScaleAmount,
                bounceHeight,
                playArea,
                damage));
        }

        public void Cancel()
        {
            if (launchRoutine != null)
            {
                StopCoroutine(launchRoutine);
                launchRoutine = null;
            }

            foreach (var warning in activeWarnings)
            {
                if (warning != null)
                {
                    warning.Cancel();
                }
            }

            activeWarnings.Clear();

            foreach (var projectile in activeProjectiles)
            {
                if (projectile != null)
                {
                    Destroy(projectile.gameObject);
                }
            }

            activeProjectiles.Clear();
        }
    }

    internal sealed class SlimeHopperWarning : MonoBehaviour
    {
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;

        public static SlimeHopperWarning Create(
            Vector2 origin,
            Vector2 direction,
            float length,
            float width,
            float duration)
        {
            var warningObject = new GameObject("Slime Hopper Warning");
            warningObject.transform.position = origin + direction * (length * 0.5f);
            warningObject.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            var warning = warningObject.AddComponent<SlimeHopperWarning>();
            warning.Initialize(length, width, duration);
            return warning;
        }

        public void Cancel()
        {
            Destroy(gameObject);
        }

        private void Initialize(float length, float width, float duration)
        {
            runtimeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Slime Hopper Warning Texture",
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

            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = runtimeSprite;
            renderer.color = new Color(1f, 0.08f, 0.08f, 0.35f);
            renderer.sortingOrder = -1;
            transform.localScale = new Vector3(length, width, 1f);

            Destroy(gameObject, duration);
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

    internal sealed class SlimeHopperProjectile : MonoBehaviour
    {
        private Vector2 direction;
        private SpriteRenderer spriteRenderer;
        private float speed;
        private float hitRadius;
        private float lifetime;
        private float bounceFrequency;
        private float bounceScaleAmount;
        private float bounceHeight;
        private float damage;
        private Bounds playArea;
        private float elapsed;
        private Vector3 baseScale;
        private Transform player;

        public static SlimeHopperProjectile Create(
            Vector3 position,
            Vector2 direction,
            Sprite sprite,
            Color color,
            float speed,
            float hitRadius,
            float scale,
            float lifetime,
            float bounceFrequency,
            float bounceScaleAmount,
            float bounceHeight,
            Bounds playArea,
            float damage)
        {
            var root = new GameObject("Slime Hopper Projectile");
            root.transform.position = new Vector3(position.x, position.y, 0f);
            var projectile = root.AddComponent<SlimeHopperProjectile>();
            projectile.Initialize(direction, sprite, color, speed, hitRadius, scale, lifetime, bounceFrequency, bounceScaleAmount, bounceHeight, playArea, damage);
            return projectile;
        }

        private void Initialize(
            Vector2 direction,
            Sprite sprite,
            Color color,
            float speed,
            float hitRadius,
            float scale,
            float lifetime,
            float bounceFrequency,
            float bounceScaleAmount,
            float bounceHeight,
            Bounds playArea,
            float damage)
        {
            this.direction = direction.normalized;
            this.speed = speed;
            this.hitRadius = hitRadius;
            this.lifetime = lifetime;
            this.bounceFrequency = bounceFrequency;
            this.bounceScaleAmount = bounceScaleAmount;
            this.bounceHeight = bounceHeight;
            this.playArea = playArea;
            this.damage = damage;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);
            spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 115;
            baseScale = Vector3.one * scale;
            visual.transform.localScale = baseScale;
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            AnimateBounce();

            if (!playArea.Contains(transform.position))
            {
                Destroy(gameObject);
                return;
            }

            if (player != null && Vector2.Distance(player.position, transform.position) <= hitRadius)
            {
                DealDamage(player);
                Destroy(gameObject);
            }
        }

        private void AnimateBounce()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            // A fast, low arc reads as a slime rapidly hopping along the floor.
            var bounce = Mathf.Pow(Mathf.Abs(Mathf.Sin(elapsed * bounceFrequency)), 1.35f);
            var stretch = 1f + bounce * bounceScaleAmount;
            spriteRenderer.transform.localScale = new Vector3(baseScale.x / stretch, baseScale.y * stretch, baseScale.z);
            spriteRenderer.transform.localPosition = new Vector3(0f, bounce * bounceHeight, 0f);
        }

        private void DealDamage(Transform target)
        {
            foreach (var behaviour in target.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (behaviour is IPlayerHealth playerHealth && playerHealth.IsAlive)
                {
                    playerHealth.TakeDamage(damage);
                    return;
                }
            }
        }
    }
}