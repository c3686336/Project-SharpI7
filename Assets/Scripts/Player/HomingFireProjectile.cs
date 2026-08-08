using SharpI7.Combat;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HomingFireProjectile : MonoBehaviour
{
    private BossHealth target;
    private Collider2D[] targetColliders;
    private CastResult castResult;
    private float speed;
    private float hitRadius;
    private float remainingLifetime;
    private bool initialized;
    private bool hasHit;

    public void Initialize(BossHealth bossTarget, CastResult result, float travelSpeed, float contactRadius, float lifetime)
    {
        target = bossTarget;
        targetColliders = target == null ? null : target.GetComponentsInChildren<Collider2D>();
        castResult = result;
        speed = Mathf.Max(0.01f, travelSpeed);
        hitRadius = Mathf.Max(0.01f, contactRadius);
        remainingLifetime = Mathf.Max(0.01f, lifetime);
        hasHit = false;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        if (target == null || !target.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        var currentPosition = (Vector2)transform.position;
        var targetPosition = FindClosestTargetPoint(currentPosition);
        var nextPosition = Vector2.MoveTowards(currentPosition, targetPosition, speed * Time.deltaTime);
        transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);

        if (HasReachedBoss(nextPosition))
        {
            ApplySpellHit();
            Destroy(gameObject);
        }
    }

    private void ApplySpellHit()
    {
        if (hasHit || target == null || !target.IsAlive)
        {
            return;
        }

        hasHit = true;
        target.TakeSpellDamage(castResult);
    }

    private Vector2 FindClosestTargetPoint(Vector2 from)
    {
        var closestPoint = (Vector2)target.transform.position;
        var closestDistance = float.PositiveInfinity;

        if (targetColliders == null)
        {
            return closestPoint;
        }

        foreach (var targetCollider in targetColliders)
        {
            if (targetCollider == null || !targetCollider.enabled)
            {
                continue;
            }

            var point = targetCollider.ClosestPoint(from);
            var distance = (point - from).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = point;
            }
        }

        return closestPoint;
    }

    private bool HasReachedBoss(Vector2 position)
    {
        if (targetColliders == null || targetColliders.Length == 0)
        {
            return Vector2.Distance(position, target.transform.position) <= hitRadius;
        }

        foreach (var targetCollider in targetColliders)
        {
            if (targetCollider == null || !targetCollider.enabled)
            {
                continue;
            }

            if (targetCollider.OverlapPoint(position) ||
                Vector2.Distance(position, targetCollider.ClosestPoint(position)) <= hitRadius)
            {
                return true;
            }
        }

        return false;
    }
}
