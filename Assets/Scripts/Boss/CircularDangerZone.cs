using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    public sealed class CircularDangerZone : MonoBehaviour
    {
        [SerializeField] private Color warningColor = new(1f, 0.08f, 0.08f, 0.35f);
        [SerializeField] private Color explosionColor = new(1f, 0.08f, 0.02f, 0.72f);
        [SerializeField] private GameObject explosionEffectPrefab;
        [SerializeField, Min(0.01f)] private float explosionEffectScale = 0.7f;
        [SerializeField, Min(0.1f)] private float explosionEffectLifetime = 3f;
        [SerializeField, Range(16, 128)] private int circleResolution = 64;

        private SpriteRenderer zoneRenderer;
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private Coroutine warningRoutine;
        private Transform damageTarget;
        private float radius;
        private float damage;
        private bool finished;

        public void Begin(Transform target, float zoneRadius, float warningDuration, float attackDamage)
        {
            damageTarget = target;
            radius = Mathf.Max(0.1f, zoneRadius);
            damage = Mathf.Max(0f, attackDamage);
            transform.localScale = Vector3.one * (radius * 2f);

            if (warningRoutine != null)
            {
                StopCoroutine(warningRoutine);
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

        private void Awake()
        {
            zoneRenderer = GetComponent<SpriteRenderer>();
            if (zoneRenderer == null)
            {
                zoneRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            CreateCircleSprite();
            zoneRenderer.sprite = runtimeSprite;
            zoneRenderer.color = warningColor;
            zoneRenderer.sortingOrder = -1;
        }

        private IEnumerator WarningRoutine(float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var pulse = 0.65f + Mathf.PingPong(progress * 4f, 0.35f);
                var color = warningColor;
                color.a *= pulse;
                zoneRenderer.color = color;
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
            zoneRenderer.color = explosionColor;
            SpawnExplosionEffect();

            if (damageTarget != null)
            {
                var offset = (Vector2)(damageTarget.position - transform.position);
                if (offset.sqrMagnitude <= radius * radius)
                {
                    ApplyDamage(damageTarget, damage);
                }
            }

            Destroy(gameObject, 0.15f);
        }

        private void SpawnExplosionEffect()
        {
            if (explosionEffectPrefab == null)
            {
                return;
            }

            var effectPosition = transform.position;
            effectPosition.z = -0.2f;
            var effect = Instantiate(explosionEffectPrefab, effectPosition, Quaternion.identity);
            effect.transform.localScale *= explosionEffectScale;

            // The source prefab is configured as a looping demonstration effect.
            // A danger zone must only produce one burst at its own explosion time.
            foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.loop = false;
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }

            Destroy(effect, explosionEffectLifetime);
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

            // Keeps the hazard compatible with a teammate's health component
            // even when it cannot implement this feature's interface yet.
            target.SendMessage("TakeDamage", amount, SendMessageOptions.DontRequireReceiver);
        }

        private void CreateCircleSprite()
        {
            var size = Mathf.Max(16, circleResolution);
            runtimeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Danger Zone Circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var maxRadius = center;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var inside = dx * dx + dy * dy <= maxRadius * maxRadius;
                    pixels[y * size + x] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            runtimeTexture.SetPixels32(pixels);
            runtimeTexture.Apply();
            runtimeSprite = Sprite.Create(
                runtimeTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            runtimeSprite.name = "Runtime Danger Zone Circle";
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
