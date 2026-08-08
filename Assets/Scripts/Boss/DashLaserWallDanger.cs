using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    public enum DashLaserWallDirection
    {
        LeftToRight,
        RightToLeft
    }

    [DisallowMultipleComponent]
    public sealed class DashLaserWallDanger : MonoBehaviour
    {
        [SerializeField] private Material laserMaterial;

        private GameObject activeLaser;
        private Transform damageTarget;
        private Bounds playArea;
        private float damage;
        private float hitWidth;
        private bool hasDamaged;
        private bool finished;
        private Coroutine attackRoutine;
        private GameObject warningObject;

        public void Begin(
            Transform target,
            Bounds arenaBounds,
            DashLaserWallDirection direction,
            float thickness,
            float warningDuration,
            float travelDuration,
            float attackDamage)
        {
            if (laserMaterial == null)
            {
                Debug.LogWarning("Dash laser attack requires a LineRenderer laser material.", this);
                Destroy(gameObject);
                return;
            }

            damageTarget = target;
            playArea = arenaBounds;
            damage = Mathf.Max(0f, attackDamage);
            hitWidth = Mathf.Max(0.1f, thickness);
            attackRoutine = StartCoroutine(AttackRoutine(
                direction,
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

            Cleanup();
            Destroy(gameObject);
        }

        private IEnumerator AttackRoutine(
            DashLaserWallDirection direction,
            float warningDuration,
            float travelDuration)
        {
            var fromLeft = direction == DashLaserWallDirection.LeftToRight;
            warningObject = CreateSideWarning(playArea, fromLeft);
            yield return new WaitForSeconds(warningDuration);
            Destroy(warningObject);
            warningObject = null;

            activeLaser = CreateLaser();

            var halfWidth = hitWidth * 0.5f;
            var startX = fromLeft ? playArea.min.x - halfWidth : playArea.max.x + halfWidth;
            var endX = fromLeft ? playArea.max.x + halfWidth : playArea.min.x - halfWidth;
            var elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.deltaTime;
                var x = Mathf.Lerp(startX, endX, Mathf.Clamp01(elapsed / travelDuration));
                activeLaser.transform.position = new Vector3(x, playArea.center.y, 0f);

                if (!hasDamaged && IsTouchingPlayer(x))
                {
                    hasDamaged = ApplyDamage(damageTarget, damage);
                }

                yield return null;
            }

            finished = true;
            Cleanup();
            Destroy(gameObject);
        }

        private GameObject CreateLaser()
        {
            var laser = new GameObject("Runtime Dash Laser");
            laser.transform.position = new Vector3(playArea.center.x, playArea.center.y, 0f);

            var line = laser.AddComponent<LineRenderer>();
            line.material = laserMaterial;
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.startWidth = hitWidth;
            line.endWidth = hitWidth;
            line.startColor = new Color(1f, 0.08f, 0.02f, 0.72f);
            line.endColor = new Color(1f, 0.08f, 0.02f, 0.72f);
            line.numCapVertices = 4;
            line.sortingOrder = 210;
            var halfHeight = playArea.extents.y + 0.5f;
            line.SetPosition(0, new Vector3(0f, -halfHeight, 0f));
            line.SetPosition(1, new Vector3(0f, halfHeight, 0f));
            return laser;
        }

        private bool IsTouchingPlayer(float laserX)
        {
            if (damageTarget == null)
            {
                return false;
            }

            var playerPosition = damageTarget.position;
            return Mathf.Abs(playerPosition.x - laserX) <= hitWidth * 0.5f &&
                   playerPosition.y >= playArea.min.y &&
                   playerPosition.y <= playArea.max.y;
        }

        private static GameObject CreateSideWarning(Bounds arenaBounds, bool onLeft)
        {
            var warning = new GameObject(onLeft ? "Left Laser Warning" : "Right Laser Warning");
            warning.transform.position = new Vector3(
                onLeft ? arenaBounds.min.x + 1f : arenaBounds.max.x - 1f,
                arenaBounds.max.y - 1.5f,
                0f);

            var text = warning.AddComponent<TextMesh>();
            text.text = "!";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.35f;
            text.fontSize = 120;
            text.color = new Color(1f, 0.08f, 0.08f, 0.65f);

            warning.GetComponent<MeshRenderer>().sortingOrder = 250;
            return warning;
        }

        private void Cleanup()
        {
            if (warningObject != null)
            {
                Destroy(warningObject);
                warningObject = null;
            }

            if (activeLaser != null)
            {
                Destroy(activeLaser);
                activeLaser = null;
            }
        }

        private static bool ApplyDamage(Transform target, float amount)
        {
            var behaviours = target.GetComponentsInParent<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IPlayerHealth player && player.IsAlive)
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
            Cleanup();
        }
    }
}