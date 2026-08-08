using SharpI7.Balance;
using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossHealth))]
    public sealed class BossMovement : MonoBehaviour
    {
        [SerializeField] private Transform playerTarget;

        private BossHealth bossHealth;
        private BossMovementBalance balance;
        private bool movementLocked;
        private float speedMultiplier = 1f;
        private float boundaryPadding;

        private void Awake()
        {
            balance = BalanceDataLoader.Current.boss.movement;
            bossHealth = GetComponent<BossHealth>();
            var collider = GetComponent<Collider2D>();
            boundaryPadding = collider == null
                ? 0f
                : Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y);
        }

        private void Update()
        {
            if (movementLocked || !bossHealth.IsAlive || bossHealth.IsTransitioningToPhaseTwo)
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
            if (offset.sqrMagnitude <= balance.stoppingDistance * balance.stoppingDistance)
            {
                return;
            }

            var nextPosition = Vector3.MoveTowards(
                currentPosition,
                targetPosition,
                balance.moveSpeed * speedMultiplier * Time.deltaTime);
            transform.position = ArenaBounds.ClampPosition(nextPosition, boundaryPadding);
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

        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Max(0f, multiplier);
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

    }
}
