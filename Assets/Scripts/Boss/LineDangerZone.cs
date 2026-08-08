using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SharpI7.Combat
{
    public enum LineDangerPattern
    {
        Horizontal,
        Vertical,
        Cross,
        DiagonalCross
    }

    [DisallowMultipleComponent]
    public sealed class LineDangerZone : MonoBehaviour
    {
        [SerializeField] private Color warningColor = new(1f, 0.08f, 0.08f, 0.35f);
        [SerializeField] private Color explosionColor = new(1f, 0.08f, 0.02f, 0.72f);

        private readonly List<ZonePart> zoneParts = new();
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private Coroutine warningRoutine;
        private Transform followTarget;
        private Transform damageTarget;
        private float attackLength;
        private float attackWidth;
        private float damage;
        private bool finished;

        public void Begin(
            Transform anchor,
            Transform target,
            LineDangerPattern pattern,
            float length,
            float width,
            float warningDuration,
            float attackDamage)
        {
            followTarget = anchor;
            damageTarget = target;
            attackLength = Mathf.Max(0.1f, length);
            attackWidth = Mathf.Clamp(width, 0.1f, attackLength);
            damage = Mathf.Max(0f, attackDamage);

            FollowAnchor();
            CreateRuntimeSprite();
            CreatePatternVisuals(pattern);
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
                var pulse = 0.65f + Mathf.PingPong(progress * 4f, 0.35f);
                var color = warningColor;
                color.a *= pulse;
                SetZoneColor(color);
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
            SetZoneColor(explosionColor);

            if (damageTarget != null && ContainsPoint(damageTarget.position))
            {
                ApplyDamage(damageTarget, damage);
            }

            Destroy(gameObject, 0.15f);
        }

        private void CreatePatternVisuals(LineDangerPattern pattern)
        {
            switch (pattern)
            {
                case LineDangerPattern.Horizontal:
                    AddZonePart("Horizontal Warning", 0f);
                    break;
                case LineDangerPattern.Vertical:
                    AddZonePart("Vertical Warning", 90f);
                    break;
                case LineDangerPattern.Cross:
                    AddZonePart("Horizontal Warning", 0f);
                    AddZonePart("Vertical Warning", 90f);
                    break;
                case LineDangerPattern.DiagonalCross:
                    AddZonePart("Diagonal Warning A", 45f);
                    AddZonePart("Diagonal Warning B", -45f);
                    break;
            }
        }

        private void AddZonePart(string objectName, float angle)
        {
            var partObject = new GameObject(objectName);
            partObject.transform.SetParent(transform, false);
            partObject.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            partObject.transform.localScale = new Vector3(attackLength, attackWidth, 1f);

            var partRenderer = partObject.AddComponent<SpriteRenderer>();
            partRenderer.sprite = runtimeSprite;
            partRenderer.color = warningColor;
            partRenderer.sortingOrder = -1;
            zoneParts.Add(new ZonePart(partRenderer, angle));
        }

        private bool ContainsPoint(Vector3 worldPoint)
        {
            var offset = (Vector2)(worldPoint - transform.position);
            var halfLength = attackLength * 0.5f;
            var halfWidth = attackWidth * 0.5f;

            foreach (var part in zoneParts)
            {
                var radians = -part.Angle * Mathf.Deg2Rad;
                var cosine = Mathf.Cos(radians);
                var sine = Mathf.Sin(radians);
                var localX = offset.x * cosine - offset.y * sine;
                var localY = offset.x * sine + offset.y * cosine;

                if (Mathf.Abs(localX) <= halfLength && Mathf.Abs(localY) <= halfWidth)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetZoneColor(Color color)
        {
            foreach (var part in zoneParts)
            {
                part.Renderer.color = color;
            }
        }

        private void CreateRuntimeSprite()
        {
            runtimeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Line Danger Zone",
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
            runtimeSprite.name = "Runtime Line Danger Zone";
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

        private readonly struct ZonePart
        {
            public ZonePart(SpriteRenderer renderer, float angle)
            {
                Renderer = renderer;
                Angle = angle;
            }

            public SpriteRenderer Renderer { get; }
            public float Angle { get; }
        }
    }
}
