using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossHealth))]
    public sealed class BossMovement : MonoBehaviour
    {
        [SerializeField] private Transform playerTarget;
        [SerializeField, Min(0f)] private float moveSpeed = 1.25f;
        [SerializeField, Min(0f)] private float stoppingDistance = 1.5f;

        private BossHealth bossHealth;
        private bool movementLocked;

        private void Awake()
        {
            bossHealth = GetComponent<BossHealth>();
        }

        private void Update()
        {
            if (movementLocked || !bossHealth.IsAlive)
            {
                return;
            }

            ResolvePlayerTarget();
            if (playerTarget == null)
            {
                return;
            }

            var currentPosition = transform.position;
            var targetPosition = playerTarget.position;
            targetPosition.z = currentPosition.z;
            var offset = targetPosition - currentPosition;
            if (offset.sqrMagnitude <= stoppingDistance * stoppingDistance)
            {
                return;
            }

            transform.position = Vector3.MoveTowards(
                currentPosition,
                targetPosition,
                moveSpeed * Time.deltaTime);
        }

        public void SetPlayerTarget(Transform target)
        {
            playerTarget = target;
        }

        public void LockMovement()
        {
            movementLocked = true;
        }

        public void UnlockMovement()
        {
            movementLocked = false;
        }

        private void ResolvePlayerTarget()
        {
            if (playerTarget != null)
            {
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTarget = player.transform;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            stoppingDistance = Mathf.Max(0f, stoppingDistance);
        }
#endif
    }
}
