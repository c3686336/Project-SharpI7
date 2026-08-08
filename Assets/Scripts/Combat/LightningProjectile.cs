using UnityEngine;

public sealed class LightningProjectile : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 8f;
    [SerializeField, Min(0.01f)] private float lifetime = 0.5f;
    [SerializeField] private bool rotateTowardDirection = true;

    private Vector3 moveDirection;
    private bool launched;

    public void LaunchOutward()
    {
        float angle = Random.Range(0f, 360f);
        float radians = angle * Mathf.Deg2Rad;

        moveDirection = new Vector3(
            Mathf.Cos(radians),
            Mathf.Sin(radians),
            0f
        ).normalized;

        if (rotateTowardDirection)
        {
            float rotationAngle =
                Mathf.Atan2(moveDirection.y, moveDirection.x) *
                Mathf.Rad2Deg;

            transform.rotation =
                Quaternion.Euler(0f, 0f, rotationAngle);
        }

        launched = true;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (!launched)
            return;

        transform.position +=
            moveDirection * moveSpeed * Time.deltaTime;
    }
}