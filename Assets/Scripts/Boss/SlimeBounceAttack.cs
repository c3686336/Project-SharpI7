using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SlimeBounceAttack : MonoBehaviour
    {
        [SerializeField] private Sprite[] jumpFrames;
        [SerializeField] private Sprite[] phaseTwoJumpFrames;
        [SerializeField, Min(0.1f)] private float firstJumpDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float secondJumpDuration = 0.55f;
        [SerializeField, Min(0.1f)] private float finalJumpDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float firstWarningDuration = 0.55f;
        [SerializeField, Min(0.05f)] private float secondWarningDuration = 0.4f;
        [SerializeField, Min(0.05f)] private float finalWarningDuration = 0.25f;
        [SerializeField, Min(0.1f)] private float landingRadius = 1.35f;
        [Header("Phase Two Frenzy")]
        [SerializeField, Range(0.2f, 1f)] private float phaseTwoTempoMultiplier = 0.85f;
        [SerializeField, Min(0f)] private float finalShockwaveDelay = 0.5f;
        [SerializeField, Min(0.5f)] private float finalShockwaveRadius = 5.5f;
        [SerializeField, Min(0.1f)] private float finalShockwaveSpeed = 10f;
        [SerializeField, Min(0.05f)] private float finalShockwaveWidth = 0.9f;
        [SerializeField] private Color warningColor = new(1f, 0.08f, 0.08f, 0.35f);
        [SerializeField] private Color impactColor = new(1f, 0.04f, 0.02f, 0.8f);

        private SpriteRenderer spriteRenderer;
        private BossMovementAnimator movementAnimator;
        private BossHealth bossHealth;
        private Coroutine routine;
        private Vector3 originalScale;
        private bool animatorWasEnabled;
        private bool visualPrepared;
        private GameObject marker;
        private Sprite markerSprite;
        private Texture2D markerTexture;
        private LineRenderer shockwaveRenderer;
        private Material shockwaveMaterial;

        public bool IsActive => routine != null;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            movementAnimator = GetComponent<BossMovementAnimator>();
            bossHealth = GetComponent<BossHealth>();
        }

        public float Begin(Transform target, Bounds bounds, float damage)
        {
            Cancel();
            routine = StartCoroutine(BounceRoutine(target, bounds, damage));
            var isFrenzied = bossHealth != null && bossHealth.IsPhaseTwo;
            var tempo = isFrenzied ? phaseTwoTempoMultiplier : 1f;
            var jumpDuration = (firstWarningDuration + firstJumpDuration
                + secondWarningDuration + secondJumpDuration
                + finalWarningDuration + finalJumpDuration) * tempo
                + 0.24f; // Three short landing flashes.

            var shockwaveRadius = GetFinalShockwaveRadius(transform.position, bounds);
            return isFrenzied
                ? jumpDuration + finalShockwaveDelay + shockwaveRadius / finalShockwaveSpeed
                : jumpDuration;
        }

        public void Cancel()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            RestoreVisual();
            DestroyMarker();
            DestroyShockwave();
        }

        private void OnDisable()
        {
            Cancel();
        }

        private IEnumerator BounceRoutine(Transform target, Bounds bounds, float damage)
        {
            PrepareVisual();
            var isFrenzied = bossHealth != null && bossHealth.IsPhaseTwo;
            var tempo = isFrenzied ? phaseTwoTempoMultiplier : 1f;
            var jumps = new[]
            {
                new BounceStep(0.35f, firstWarningDuration * tempo, firstJumpDuration * tempo),
                new BounceStep(0.6f, secondWarningDuration * tempo, secondJumpDuration * tempo),
                new BounceStep(1f, finalWarningDuration * tempo, finalJumpDuration * tempo)
            };

            for (var index = 0; index < jumps.Length; index++)
            {
                var step = jumps[index];
                var landing = ChooseLandingPosition(target, bounds, step.targetProgress);
                yield return ShowWarning(landing, step.warningDuration);
                yield return JumpTo(landing, step.travelDuration);
                DamageAtLanding(target, landing, damage);

                if (isFrenzied && index == jumps.Length - 1)
                {
                    yield return new WaitForSeconds(finalShockwaveDelay);
                    yield return EmitFinalShockwave(target, landing, damage, GetFinalShockwaveRadius(landing, bounds));
                }
            }

            RestoreVisual();
            routine = null;
        }

        private Vector3 ChooseLandingPosition(Transform target, Bounds bounds, float targetProgress)
        {
            var current = transform.position;
            var targetPosition = target != null ? target.position : current;
            targetPosition.z = current.z;
            var desired = Vector3.Lerp(current, targetPosition, targetProgress);
            desired.x = Mathf.Clamp(desired.x, bounds.min.x + landingRadius, bounds.max.x - landingRadius);
            desired.y = Mathf.Clamp(desired.y, bounds.min.y + landingRadius, bounds.max.y - landingRadius);
            return desired;
        }

        private IEnumerator ShowWarning(Vector3 landing, float duration)
        {
            CreateMarker(landing, warningColor);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var color = warningColor;
                color.a *= 0.65f + Mathf.PingPong(elapsed * 5f, 0.35f);
                SetMarkerColor(color);
                yield return null;
            }
        }

        private IEnumerator JumpTo(Vector3 landing, float duration)
        {
            var start = transform.position;
            var frames = GetActiveJumpFrames();
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(start, landing, progress);
                transform.localScale = Vector3.Scale(originalScale, new Vector3(1f + Mathf.Sin(progress * Mathf.PI) * 0.18f, 1f - Mathf.Sin(progress * Mathf.PI) * 0.1f, 1f));
                SetJumpFrame(frames, progress);
                yield return null;
            }

            transform.position = landing;
            transform.localScale = originalScale;
            SetMarkerColor(impactColor);
            yield return new WaitForSeconds(0.08f);
            DestroyMarker();
        }

        private float GetFinalShockwaveRadius(Vector3 center, Bounds bounds)
        {
            // Use the farthest arena corner so the ring travels through the
            // full play field instead of ending as a small circle near slime.
            var farthestX = Mathf.Max(Mathf.Abs(center.x - bounds.min.x), Mathf.Abs(bounds.max.x - center.x));
            var farthestY = Mathf.Max(Mathf.Abs(center.y - bounds.min.y), Mathf.Abs(bounds.max.y - center.y));
            return Mathf.Max(finalShockwaveRadius, Mathf.Sqrt(farthestX * farthestX + farthestY * farthestY));
        }
        private IEnumerator EmitFinalShockwave(Transform target, Vector3 center, float damage, float maxRadius)
        {
            CreateShockwave();
            var radius = 0.05f;
            var hit = false;
            while (radius < maxRadius)
            {
                radius += finalShockwaveSpeed * Time.deltaTime;
                DrawShockwave(center, radius);
                if (!hit && target != null && Mathf.Abs(Vector2.Distance(target.position, center) - radius) <= finalShockwaveWidth * 0.5f + 0.35f)
                {
                    hit = true;
                    foreach (var behaviour in target.GetComponentsInParent<MonoBehaviour>(true))
                    {
                        if (behaviour is IPlayerHealth player && player.IsAlive)
                        {
                            player.TakeDamage(damage);
                            break;
                        }
                    }
                }
                yield return null;
            }

            DestroyShockwave();
        }

        private void CreateShockwave()
        {
            DestroyShockwave();
            var objectWithLine = new GameObject("Slime Frenzy Final Shockwave");
            shockwaveRenderer = objectWithLine.AddComponent<LineRenderer>();
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            shockwaveMaterial = shader == null ? null : new Material(shader);
            shockwaveRenderer.material = shockwaveMaterial;
            shockwaveRenderer.useWorldSpace = true;
            shockwaveRenderer.loop = true;
            shockwaveRenderer.positionCount = 72;
            shockwaveRenderer.startWidth = finalShockwaveWidth;
            shockwaveRenderer.endWidth = finalShockwaveWidth;
            shockwaveRenderer.startColor = impactColor;
            shockwaveRenderer.endColor = impactColor;
            shockwaveRenderer.sortingOrder = 220;
        }

        private void DrawShockwave(Vector3 center, float radius)
        {
            if (shockwaveRenderer == null)
            {
                return;
            }

            for (var index = 0; index < shockwaveRenderer.positionCount; index++)
            {
                var angle = index / (float)shockwaveRenderer.positionCount * Mathf.PI * 2f;
                shockwaveRenderer.SetPosition(index, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private void DestroyShockwave()
        {
            if (shockwaveRenderer != null)
            {
                Destroy(shockwaveRenderer.gameObject);
                shockwaveRenderer = null;
            }
            if (shockwaveMaterial != null)
            {
                Destroy(shockwaveMaterial);
                shockwaveMaterial = null;
            }
        }
        private void DamageAtLanding(Transform target, Vector3 landing, float damage)
        {
            if (target == null || Vector2.Distance(target.position, landing) > landingRadius)
            {
                return;
            }

            foreach (var behaviour in target.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (behaviour is IPlayerHealth player && player.IsAlive)
                {
                    player.TakeDamage(damage);
                    return;
                }
            }
        }

        private void PrepareVisual()
        {
            visualPrepared = true;
            originalScale = transform.localScale;
            animatorWasEnabled = movementAnimator != null && movementAnimator.enabled;
            if (movementAnimator != null)
            {
                movementAnimator.enabled = false;
            }
        }

        private void RestoreVisual()
        {
            // Begin() cancels a previous attack before this one prepares its visual state.
            // In that case, keep the walking animator untouched.
            if (!visualPrepared)
            {
                return;
            }

            transform.localScale = originalScale == Vector3.zero ? transform.localScale : originalScale;
            if (movementAnimator != null)
            {
                movementAnimator.enabled = animatorWasEnabled;
                movementAnimator.RefreshVisual();
            }

            visualPrepared = false;
        }

        private Sprite[] GetActiveJumpFrames()
        {
            return bossHealth != null && bossHealth.IsPhaseTwo && phaseTwoJumpFrames != null && phaseTwoJumpFrames.Length > 0
                ? phaseTwoJumpFrames
                : jumpFrames;
        }

        private void SetJumpFrame(Sprite[] frames, float progress)
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            var index = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(progress * frames.Length));
            if (frames[index] != null)
            {
                spriteRenderer.sprite = frames[index];
            }
        }

        private void CreateMarker(Vector3 position, Color color)
        {
            DestroyMarker();
            markerTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    var point = new Vector2(x - 31.5f, y - 31.5f) / 31.5f;
                    markerTexture.SetPixel(x, y, point.sqrMagnitude <= 1f ? Color.white : Color.clear);
                }
            }
            markerTexture.Apply();
            markerSprite = Sprite.Create(markerTexture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f);

            marker = new GameObject("Slime Bounce Landing Warning");
            marker.transform.position = new Vector3(position.x, position.y, 0.05f);
            marker.transform.localScale = Vector3.one * (landingRadius * 2f);
            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = markerSprite;
            renderer.color = color;
            renderer.sortingOrder = 102;
        }

        private void SetMarkerColor(Color color)
        {
            if (marker == null)
            {
                return;
            }

            var renderer = marker.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = color;
            }
        }

        private void DestroyMarker()
        {
            if (marker != null)
            {
                Destroy(marker);
                marker = null;
            }
            if (markerSprite != null)
            {
                Destroy(markerSprite);
                markerSprite = null;
            }
            if (markerTexture != null)
            {
                Destroy(markerTexture);
                markerTexture = null;
            }
        }

        private readonly struct BounceStep
        {
            public readonly float targetProgress;
            public readonly float warningDuration;
            public readonly float travelDuration;

            public BounceStep(float targetProgress, float warningDuration, float travelDuration)
            {
                this.targetProgress = targetProgress;
                this.warningDuration = warningDuration;
                this.travelDuration = travelDuration;
            }
        }
    }
}