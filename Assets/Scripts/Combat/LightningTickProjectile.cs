using SharpI7.Combat;
using UnityEngine;

public class LightningTickProjectile : MonoBehaviour
{
    [SerializeField] private float hitDistance = 0.1f;
    [SerializeField] private float fallbackLifetime = 2f;

    private Transform target;
    private IDamageable targetDamageable;
    private float damage;
    private float speed;
    private bool initialized;

    public void Initialize(Transform target, IDamageable targetDamageable, float damage, float travelTime)
    {
        this.target = target;
        this.targetDamageable = targetDamageable;
        this.damage = damage;

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        float safeTravelTime = Mathf.Max(0.01f, travelTime);
        speed = distance / safeTravelTime;
        initialized = true;

        Destroy(gameObject, fallbackLifetime);
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) <= hitDistance)
        {
            targetDamageable?.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}