using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    public enum DashLaserWallDirection
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    [DisallowMultipleComponent]
    public sealed class DashLaserWallDanger : MonoBehaviour
    {
        [SerializeField] private Color warningColor = new(1f, 0.08f, 0.08f, 0.3f);
        [SerializeField] private Color activeColor = new(1f, 0.2f, 0.02f, 0.95f);

        private SpriteRenderer wallRenderer;
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private Coroutine attackRoutine;
        private Transform damageTarget;
        private Vector2 wallSize;
        private float damage;
        private bool hasDamaged;
        private bool finished;

        public void Begin(
            Transform target,
            Bounds playArea,
            DashLaserWallDirection direction,
            float thickness,
            float warningDuration,
            float travelDuration,
            float attackDamage)
        {
            damageTarget = target;
            damage = Mathf.Max(0f, attackDamage);

            var travel = GetTravelPositions(playArea, direction, Mathf.Max(0.1f, thickness));
            wallSize = travel.wallSize;
            CreateVisual();
            transform.position = travel.startPosition;
            attackRoutine = StartCoroutine(AttackRoutine(
                travel.startPosition,
                travel.endPosition,
                Mathf.Max(0.05f, warningDuration),
                Mathf.Max(0.05f, travelDuration)));
        }

        public void Cancel()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
            }

            Destroy(gameObject);
        }

        private IEnumerator AttackRoutine(
            Vector2 startPosition,
            Vector2 endPosition,
            float warningDuration,
            float travelDuration)
        {
            var elapsed = 0f;
            while (elapsed < warningDuration)
            {
                elapsed += Time.deltaTime;
                var pulse = 0.55f + Mathf.PingPong(elapsed * 3f, 0.45f);
                var color = warningColor;
                color.a *= pulse;
                wallRenderer.color = color;
                yield return null;
            }

            wallRenderer.color = activeColor;
            elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector2.Lerp(startPosition, endPosition, elapsed / travelDuration);

                if (!hasDamaged && damageTarget != null && ContainsPoint(damageTarget.position))
                {
                    hasDamaged = ApplyDamage(damageTarget, damage);
                }

                yield return null;
            }

            finished = true;
            Destroy(gameObject);
        }

        private bool ContainsPoint(Vector3 point)
        {
            var offset = (Vector2)(point - transform.position);
            return Mathf.Abs(offset.x) <= wallSize.x * 0.5f &&
                   Mathf.Abs(offset.y) <= wallSize.y * 0.5f;
        }

        private void CreateVisual()
        {
            runtimeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Dash Laser Wall",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            runtimeTexture.SetPixel(0, 0, Color.white);
            runtimeTexture.Apply();

            runtimeSprite = Sprite.Create(runtimeTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            var wallObject = new GameObject("Dash Laser Wall");
            wallObject.transform.SetParent(transform, false);
            wallObject.transform.localScale = new Vector3(wallSize.x, wallSize.y, 1f);
            wallRenderer = wallObject.AddComponent<SpriteRenderer>();
            wallRenderer.sprite = runtimeSprite;
            wallRenderer.color = warningColor;
            wallRenderer.sortingOrder = 105;
        }

        private static (Vector2 startPosition, Vector2 endPosition, Vector2 wallSize) GetTravelPositions(
            Bounds playArea,
            DashLaserWallDirection direction,
            float thickness)
        {
            var center = (Vector2)playArea.center;
            var halfSize = (Vector2)playArea.extents;
            var horizontal = direction == DashLaserWallDirection.LeftToRight || direction == DashLaserWallDirection.RightToLeft;
            var wallSize = horizontal
                ? new Vector2(thickness, playArea.size.y + thickness * 2f)
                : new Vector2(playArea.size.x + thickness * 2f, thickness);

            var start = center;
            var end = center;
            if (horizontal)
            {
                var fromLeft = direction == DashLaserWallDirection.LeftToRight;
                start.x += fromLeft ? -halfSize.x - thickness : halfSize.x + thickness;
                end.x += fromLeft ? halfSize.x + thickness : -halfSize.x - thickness;
            }
            else
            {
                var fromBottom = direction == DashLaserWallDirection.BottomToTop;
                start.y += fromBottom ? -halfSize.y - thickness : halfSize.y + thickness;
                end.y += fromBottom ? halfSize.y + thickness : -halfSize.y - thickness;
            }

            return (start, end, wallSize);
        }

        private static bool ApplyDamage(Transform target, float amount)
        {
            var behaviours = target.GetComponentsInParent<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IPlayer player && player.IsAlive)
                {
                    var previousHealth = player.CurrentHealth;
                    player.TakeDamage(amount);
                    return player.CurrentHealth < previousHealth;
                }

                if (behaviour is IDamageable damageable && damageable.IsAlive)
                {
                    damageable.TakeDamage(amount);
                    return true;
                }
            }

            target.SendMessage("TakeDamage", amount, SendMessageOptions.DontRequireReceiver);
            return true;
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
