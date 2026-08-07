using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossHealth))]
    public sealed class TopDownBossController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private CircularDangerZone dangerZonePrefab;
        [SerializeField] private LineDangerZone lineDangerZonePrefab;
        [SerializeField] private SafeZoneDanger safeZoneDangerPrefab;
        [SerializeField] private RotatingLaserDanger rotatingLaserDangerPrefab;
        [SerializeField] private SlowWobbleOrb slowWobbleOrbPrefab;
        [SerializeField] private BossDistanceDanger bossDistanceDangerPrefab;
        [SerializeField] private TrailHazardOrb trailHazardOrbPrefab;
        [SerializeField] private ConeDangerZone coneDangerZonePrefab;

        [Header("Circular Ground Attack")]
        [SerializeField] private bool enableCircleGroundAttack = true;
        [SerializeField, Min(0f)] private float firstAttackDelay = 1f;
        [SerializeField, Min(0.05f)] private float warningDuration = 2.5f;
        [SerializeField, Min(0f)] private float recoveryDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float attackRadius = 2.25f;
        [SerializeField, Min(0f)] private float attackDamage = 20f;

        [Header("Tracking Barrage")]
        [SerializeField] private bool enableTrackingBarrage = true;
        [SerializeField, Min(1)] private int minTrackingStrikes = 3;
        [SerializeField, Min(1)] private int maxTrackingStrikes = 5;
        [SerializeField, Min(0.05f)] private float trackingWarningDuration = 0.8f;
        [SerializeField, Min(0f)] private float trackingStrikeInterval = 0.1f;

        [Header("Line Ground Attack")]
        [SerializeField] private bool enableLineGroundAttack = true;
        [SerializeField, Min(0.05f)] private float lineWarningDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float lineAttackLength = 16f;
        [SerializeField, Min(0.1f)] private float lineAttackWidth = 2f;

        [Header("Safe Zone Attack")]
        [SerializeField] private bool enableSafeZoneAttack = true;
        [SerializeField, Min(0.05f)] private float safeZoneWarningDuration = 3f;
        [SerializeField] private Vector2 safeZoneFieldSize = new(30f, 18f);
        [SerializeField, Min(0.1f)] private float safeZoneRadius = 3f;
        [SerializeField, Min(0f)] private float safeZoneMinDistance = 5f;
        [SerializeField, Min(0f)] private float safeZoneMaxDistance = 9f;

        [Header("Rotating Laser Attack")]
        [SerializeField] private bool enableRotatingLaserAttack = true;
        [SerializeField, Min(0.05f)] private float laserWarningDuration = 1f;
        [SerializeField, Min(0.05f)] private float laserActiveDuration = 5f;
        [SerializeField, Min(0.1f)] private float laserLength = 14f;
        [SerializeField, Min(0.1f)] private float laserWidth = 1.2f;
        [SerializeField, Min(0f)] private float laserDamagePerTick = 10f;
        [SerializeField, Min(0.05f)] private float laserDamageTickInterval = 0.5f;

        [Header("Slow Wobble Orb Attack")]
        [SerializeField] private bool enableSlowWobbleOrbAttack = true;
        [SerializeField] private bool enableRadialWobbleOrbAttack = true;
        [SerializeField, Min(3)] private int radialOrbCount = 12;
        [SerializeField, Min(0.01f)] private float orbMovementSpeed = 1.2f;
        [SerializeField, Min(0f)] private float orbWobbleAmplitude = 0.8f;
        [SerializeField, Min(0f)] private float orbWobbleFrequency = 0.6f;
        [SerializeField, Min(0.05f)] private float orbCollisionRadius = 0.55f;
        [SerializeField, Min(0f)] private float orbDamage = 15f;
        [SerializeField, Min(0f)] private float aimedOrbSpawnOffset = 1.35f;
        [SerializeField, Min(1)] private int aimedOrbShotsPerAttack = 3;
        [SerializeField, Min(0.05f)] private float aimedOrbShotInterval = 1.5f;
        [SerializeField] private Vector2 orbPlayAreaSize = new(30f, 18f);

        [Header("Boss Distance Attack")]
        [SerializeField] private bool enableBossDistanceAttack = true;
        [SerializeField, Min(0.05f)] private float bossDistanceWarningDuration = 3f;
        [SerializeField] private Vector2 bossDistanceFieldSize = new(30f, 18f);
        [SerializeField, Min(0.1f)] private float bossDistanceRadius = 6f;

        [Header("Movement Trail Attack")]
        [SerializeField] private bool enableMovementTrailAttack = true;
        [SerializeField, Min(0.05f)] private float trailPatternDuration = 4f;
        [SerializeField, Min(0.05f)] private float trailSpawnInterval = 0.75f;
        [SerializeField, Min(0.05f)] private float trailOrbLifetime = 3f;
        [SerializeField, Min(0.05f)] private float trailOrbCollisionRadius = 0.45f;
        [SerializeField, Min(0f)] private float trailOrbDamage = 15f;

        [Header("Forward Cone Attack")]
        [SerializeField] private bool enableForwardConeAttack = true;
        [SerializeField, Min(0.05f)] private float coneWarningDuration = 1.25f;
        [SerializeField, Min(0.1f)] private float coneAttackRange = 6f;
        [SerializeField, Range(1f, 359f)] private float coneAttackAngle = 70f;
        [SerializeField, Min(0f)] private float coneAttackDamage = 25f;

        private BossHealth bossHealth;
        private BossMovement bossMovement;
        private BossVisual bossVisual;
        private Coroutine attackRoutine;
        private CircularDangerZone activeZone;
        private LineDangerZone activeLineZone;
        private SafeZoneDanger activeSafeZone;
        private RotatingLaserDanger activeLaserZone;
        private readonly List<SlowWobbleOrb> activeOrbs = new();
        private readonly List<TrailHazardOrb> activeTrailHazards = new();
        private readonly List<BossAttackPattern> availableAttacks = new();
        private BossDistanceDanger activeBossDistanceZone;
        private ConeDangerZone activeConeZone;
        private AttackFamily lastAttackFamily = AttackFamily.None;
        private BossDistanceDangerMode nextBossDistanceMode = BossDistanceDangerMode.InnerDanger;

        private void Awake()
        {
            bossHealth = GetComponent<BossHealth>();
            bossMovement = GetComponent<BossMovement>();
            bossVisual = GetComponent<BossVisual>();
        }

        private void OnEnable()
        {
            bossHealth.Died += StopAttacking;
            attackRoutine = StartCoroutine(AttackLoop());
        }

        private void OnDisable()
        {
            bossHealth.Died -= StopAttacking;

            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            CancelActiveZone();
            CancelActiveLineZone();
            CancelActiveSafeZone();
            CancelActiveLaserZone();
            CancelActiveOrbs();
            CancelActiveBossDistanceZone();
            CancelActiveTrailHazards();
            CancelActiveConeZone();
        }

        public void SetPlayerTarget(Transform target)
        {
            playerTarget = target;
            bossMovement.SetPlayerTarget(target);
        }

        private IEnumerator AttackLoop()
        {
            if (firstAttackDelay > 0f)
            {
                yield return new WaitForSeconds(firstAttackDelay);
            }

            while (bossHealth.IsAlive)
            {
                ResolvePlayerTarget();

                if (playerTarget == null)
                {
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }

                BuildAvailableAttacks();
                if (availableAttacks.Count == 0)
                {
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }

                var selectedAttack = availableAttacks[Random.Range(0, availableAttacks.Count)];
                yield return PerformAttack(selectedAttack);
                lastAttackFamily = selectedAttack == BossAttackPattern.OrbFamily
                    ? AttackFamily.OrbProjectile
                    : AttackFamily.NonOrb;

                if (recoveryDuration > 0f)
                {
                    yield return new WaitForSeconds(recoveryDuration);
                }
            }
        }

        private void BuildAvailableAttacks()
        {
            availableAttacks.Clear();

            AddAttackIf(enableCircleGroundAttack && dangerZonePrefab != null, BossAttackPattern.Circle);
            AddAttackIf(enableTrackingBarrage && dangerZonePrefab != null, BossAttackPattern.TrackingBarrage);
            AddAttackIf(enableLineGroundAttack && lineDangerZonePrefab != null, BossAttackPattern.Line);
            AddAttackIf(enableSafeZoneAttack && safeZoneDangerPrefab != null, BossAttackPattern.SafeZone);
            AddAttackIf(enableRotatingLaserAttack && rotatingLaserDangerPrefab != null, BossAttackPattern.RotatingLaser);
            AddAttackIf(
                lastAttackFamily != AttackFamily.OrbProjectile
                    && (enableSlowWobbleOrbAttack || enableRadialWobbleOrbAttack)
                    && slowWobbleOrbPrefab != null,
                BossAttackPattern.OrbFamily);
            AddAttackIf(enableBossDistanceAttack && bossDistanceDangerPrefab != null, BossAttackPattern.BossDistance);
            AddAttackIf(enableMovementTrailAttack && trailHazardOrbPrefab != null, BossAttackPattern.MovementTrail);
            AddAttackIf(
                enableForwardConeAttack
                    && coneDangerZonePrefab != null
                    && IsPlayerWithinBossSize(),
                BossAttackPattern.ForwardCone);
        }

        private void AddAttackIf(bool condition, BossAttackPattern attack)
        {
            if (condition)
            {
                availableAttacks.Add(attack);
            }
        }

        private bool IsPlayerWithinBossSize()
        {
            if (playerTarget == null)
            {
                return false;
            }

            var bossSize = bossVisual != null ? bossVisual.VisualSize : 2.4f;
            var offset = (Vector2)(playerTarget.position - transform.position);
            return offset.sqrMagnitude <= bossSize * bossSize;
        }

        private IEnumerator PerformAttack(BossAttackPattern attack)
        {
            switch (attack)
            {
                case BossAttackPattern.Circle:
                    yield return PerformSingleGroundAttack();
                    break;
                case BossAttackPattern.TrackingBarrage:
                    yield return PerformTrackingBarrage();
                    break;
                case BossAttackPattern.Line:
                    yield return PerformLineGroundAttack();
                    break;
                case BossAttackPattern.SafeZone:
                    yield return PerformSafeZoneAttack();
                    break;
                case BossAttackPattern.RotatingLaser:
                    yield return PerformRotatingLaserAttack();
                    break;
                case BossAttackPattern.OrbFamily:
                    yield return PerformOrbFamilyAttack();
                    break;
                case BossAttackPattern.BossDistance:
                    yield return PerformBossDistanceAttack();
                    break;
                case BossAttackPattern.MovementTrail:
                    yield return PerformMovementTrailAttack();
                    break;
                case BossAttackPattern.ForwardCone:
                    yield return PerformForwardConeAttack();
                    break;
            }
        }

        private IEnumerator PerformForwardConeAttack()
        {
            if (bossMovement != null)
            {
                bossMovement.LockMovement();
            }

            var attackPosition = transform.position;
            attackPosition.z = 0f;
            var fixedDirection = (Vector2)(playerTarget.position - attackPosition);
            if (fixedDirection.sqrMagnitude < 0.001f)
            {
                fixedDirection = Vector2.right;
            }

            activeConeZone = Instantiate(coneDangerZonePrefab, attackPosition, Quaternion.identity);
            activeConeZone.Begin(
                playerTarget,
                fixedDirection.normalized,
                coneAttackRange,
                coneAttackAngle,
                coneWarningDuration,
                coneAttackDamage);

            yield return new WaitForSeconds(coneWarningDuration + 0.2f);
            activeConeZone = null;

            if (bossMovement != null && bossHealth.IsAlive)
            {
                bossMovement.UnlockMovement();
            }
        }

        private IEnumerator PerformMovementTrailAttack()
        {
            var elapsed = 0f;

            while (elapsed < trailPatternDuration && bossHealth.IsAlive)
            {
                ResolvePlayerTarget();
                SpawnTrailHazard();

                var waitDuration = Mathf.Min(trailSpawnInterval, trailPatternDuration - elapsed);
                if (waitDuration <= 0f)
                {
                    break;
                }

                yield return new WaitForSeconds(waitDuration);
                elapsed += waitDuration;
            }
        }

        private void SpawnTrailHazard()
        {
            activeTrailHazards.RemoveAll(hazard => hazard == null);
            var spawnPosition = transform.position;
            spawnPosition.z = 0f;
            var hazard = Instantiate(trailHazardOrbPrefab, spawnPosition, Quaternion.identity);
            activeTrailHazards.Add(hazard);
            hazard.Begin(
                playerTarget,
                trailOrbCollisionRadius,
                trailOrbDamage,
                trailOrbLifetime);
        }

        private IEnumerator PerformBossDistanceAttack()
        {
            var attackPosition = transform.position;
            attackPosition.z = 0f;
            activeBossDistanceZone = Instantiate(
                bossDistanceDangerPrefab,
                attackPosition,
                Quaternion.identity);
            activeBossDistanceZone.Begin(
                transform,
                playerTarget,
                nextBossDistanceMode,
                bossDistanceFieldSize,
                bossDistanceRadius,
                bossDistanceWarningDuration,
                attackDamage);

            nextBossDistanceMode = nextBossDistanceMode == BossDistanceDangerMode.InnerDanger
                ? BossDistanceDangerMode.OuterDanger
                : BossDistanceDangerMode.InnerDanger;

            yield return new WaitForSeconds(bossDistanceWarningDuration + 0.25f);
            activeBossDistanceZone = null;
        }

        private IEnumerator PerformOrbFamilyAttack()
        {
            var useRadialPattern = enableRadialWobbleOrbAttack
                && (!enableSlowWobbleOrbAttack || Random.value < 0.5f);

            if (useRadialPattern)
            {
                yield return PerformRadialWobbleOrbAttack();
            }
            else
            {
                yield return PerformSlowWobbleOrbAttack();
            }
        }

        private IEnumerator PerformSlowWobbleOrbAttack()
        {
            var shotCount = Mathf.Max(1, aimedOrbShotsPerAttack);

            for (var shotIndex = 0; shotIndex < shotCount && bossHealth.IsAlive; shotIndex++)
            {
                ResolvePlayerTarget();
                if (playerTarget == null)
                {
                    yield break;
                }

                var bossPosition = transform.position;
                bossPosition.z = 0f;
                var fixedDirection = (Vector2)(playerTarget.position - bossPosition);
                if (fixedDirection.sqrMagnitude < 0.001f)
                {
                    fixedDirection = Vector2.right;
                }

                fixedDirection.Normalize();
                var spawnPosition = bossPosition
                    + (Vector3)(fixedDirection * aimedOrbSpawnOffset);
                var playAreaBounds = CreateOrbPlayAreaBounds(bossPosition);
                SpawnWobbleOrb(spawnPosition, fixedDirection, playAreaBounds);

                if (shotIndex < shotCount - 1)
                {
                    yield return new WaitForSeconds(aimedOrbShotInterval);
                }
            }
        }

        private IEnumerator PerformRadialWobbleOrbAttack()
        {
            var spawnPosition = transform.position;
            spawnPosition.z = 0f;
            var playAreaBounds = CreateOrbPlayAreaBounds(spawnPosition);
            var projectileCount = Mathf.Max(3, radialOrbCount);

            for (var orbIndex = 0; orbIndex < projectileCount; orbIndex++)
            {
                var angle = 360f * orbIndex / projectileCount;
                var direction = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad));
                SpawnWobbleOrb(spawnPosition, direction, playAreaBounds);
            }

            yield return null;
        }

        private Bounds CreateOrbPlayAreaBounds(Vector3 center)
        {
            return new Bounds(
                center,
                new Vector3(orbPlayAreaSize.x, orbPlayAreaSize.y, 100f));
        }

        private void SpawnWobbleOrb(Vector3 spawnPosition, Vector2 direction, Bounds playAreaBounds)
        {
            activeOrbs.RemoveAll(orb => orb == null);
            var orb = Instantiate(slowWobbleOrbPrefab, spawnPosition, Quaternion.identity);
            activeOrbs.Add(orb);
            orb.Begin(
                playerTarget,
                direction,
                orbMovementSpeed,
                orbWobbleAmplitude,
                orbWobbleFrequency,
                orbCollisionRadius,
                orbDamage,
                playAreaBounds);
        }

        private IEnumerator PerformRotatingLaserAttack()
        {
            var attackPosition = transform.position;
            attackPosition.z = 0f;
            activeLaserZone = Instantiate(rotatingLaserDangerPrefab, attackPosition, Quaternion.identity);
            activeLaserZone.Begin(
                transform,
                playerTarget,
                laserLength,
                laserWidth,
                laserWarningDuration,
                laserActiveDuration,
                laserDamagePerTick,
                laserDamageTickInterval);

            yield return new WaitForSeconds(laserWarningDuration + laserActiveDuration + 0.15f);
            activeLaserZone = null;
        }

        private IEnumerator PerformSafeZoneAttack()
        {
            var fieldCenter = (Vector2)transform.position;
            var safePosition = ChooseSafeZonePosition(fieldCenter);
            var attackPosition = transform.position;
            attackPosition.z = 0f;

            activeSafeZone = Instantiate(safeZoneDangerPrefab, attackPosition, Quaternion.identity);
            activeSafeZone.Begin(
                playerTarget,
                safePosition,
                safeZoneFieldSize,
                safeZoneRadius,
                safeZoneWarningDuration,
                attackDamage);

            yield return new WaitForSeconds(safeZoneWarningDuration + 0.25f);
            activeSafeZone = null;
        }

        private Vector2 ChooseSafeZonePosition(Vector2 fieldCenter)
        {
            var direction = Random.insideUnitCircle;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            var distance = Random.Range(safeZoneMinDistance, safeZoneMaxDistance);
            var desiredPosition = (Vector2)playerTarget.position + direction * distance;
            var availableHalfWidth = Mathf.Max(0f, safeZoneFieldSize.x * 0.5f - safeZoneRadius);
            var availableHalfHeight = Mathf.Max(0f, safeZoneFieldSize.y * 0.5f - safeZoneRadius);

            desiredPosition.x = Mathf.Clamp(
                desiredPosition.x,
                fieldCenter.x - availableHalfWidth,
                fieldCenter.x + availableHalfWidth);
            desiredPosition.y = Mathf.Clamp(
                desiredPosition.y,
                fieldCenter.y - availableHalfHeight,
                fieldCenter.y + availableHalfHeight);
            return desiredPosition;
        }

        private IEnumerator PerformLineGroundAttack()
        {
            var patternCount = System.Enum.GetValues(typeof(LineDangerPattern)).Length;
            var pattern = (LineDangerPattern)Random.Range(0, patternCount);
            var attackPosition = transform.position;
            attackPosition.z = 0f;

            activeLineZone = Instantiate(lineDangerZonePrefab, attackPosition, Quaternion.identity);
            activeLineZone.Begin(
                transform,
                playerTarget,
                pattern,
                lineAttackLength,
                lineAttackWidth,
                lineWarningDuration,
                attackDamage);

            yield return new WaitForSeconds(lineWarningDuration + 0.2f);
            activeLineZone = null;
        }

        private IEnumerator PerformSingleGroundAttack()
        {
            SpawnDangerZone(warningDuration);
            yield return new WaitForSeconds(warningDuration + 0.2f);
            activeZone = null;
        }

        private IEnumerator PerformTrackingBarrage()
        {
            var strikeCount = Random.Range(minTrackingStrikes, maxTrackingStrikes + 1);

            for (var strikeIndex = 0; strikeIndex < strikeCount && bossHealth.IsAlive; strikeIndex++)
            {
                ResolvePlayerTarget();
                if (playerTarget == null)
                {
                    yield break;
                }

                // The position is sampled again for every strike, so the warnings
                // form a trail that follows a moving player instead of stacking.
                SpawnDangerZone(trackingWarningDuration);
                yield return new WaitForSeconds(trackingWarningDuration + 0.2f);
                activeZone = null;

                if (trackingStrikeInterval > 0f && strikeIndex < strikeCount - 1)
                {
                    yield return new WaitForSeconds(trackingStrikeInterval);
                }
            }
        }

        private void SpawnDangerZone(float duration)
        {
            var targetPosition = playerTarget.position;
            targetPosition.z = 0f;
            activeZone = Instantiate(dangerZonePrefab, targetPosition, Quaternion.identity);
            activeZone.Begin(playerTarget, attackRadius, duration, attackDamage);
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

        private void StopAttacking()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            CancelActiveZone();
            CancelActiveLineZone();
            CancelActiveSafeZone();
            CancelActiveLaserZone();
            CancelActiveOrbs();
            CancelActiveBossDistanceZone();
            CancelActiveTrailHazards();
            CancelActiveConeZone();
        }

        private void CancelActiveZone()
        {
            if (activeZone == null)
            {
                return;
            }

            activeZone.Cancel();
            activeZone = null;
        }

        private void CancelActiveLineZone()
        {
            if (activeLineZone == null)
            {
                return;
            }

            activeLineZone.Cancel();
            activeLineZone = null;
        }

        private void CancelActiveSafeZone()
        {
            if (activeSafeZone == null)
            {
                return;
            }

            activeSafeZone.Cancel();
            activeSafeZone = null;
        }

        private void CancelActiveLaserZone()
        {
            if (activeLaserZone == null)
            {
                return;
            }

            activeLaserZone.Cancel();
            activeLaserZone = null;
        }

        private void CancelActiveOrbs()
        {
            for (var orbIndex = activeOrbs.Count - 1; orbIndex >= 0; orbIndex--)
            {
                if (activeOrbs[orbIndex] != null)
                {
                    activeOrbs[orbIndex].Cancel();
                }
            }

            activeOrbs.Clear();
        }

        private void CancelActiveBossDistanceZone()
        {
            if (activeBossDistanceZone == null)
            {
                return;
            }

            activeBossDistanceZone.Cancel();
            activeBossDistanceZone = null;
        }

        private void CancelActiveTrailHazards()
        {
            for (var hazardIndex = activeTrailHazards.Count - 1; hazardIndex >= 0; hazardIndex--)
            {
                if (activeTrailHazards[hazardIndex] != null)
                {
                    activeTrailHazards[hazardIndex].Cancel();
                }
            }

            activeTrailHazards.Clear();
        }

        private void CancelActiveConeZone()
        {
            if (activeConeZone != null)
            {
                activeConeZone.Cancel();
                activeConeZone = null;
            }

            if (bossMovement != null)
            {
                bossMovement.UnlockMovement();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            firstAttackDelay = Mathf.Max(0f, firstAttackDelay);
            warningDuration = Mathf.Max(0.05f, warningDuration);
            recoveryDuration = Mathf.Max(0f, recoveryDuration);
            attackRadius = Mathf.Max(0.1f, attackRadius);
            attackDamage = Mathf.Max(0f, attackDamage);
            minTrackingStrikes = Mathf.Max(1, minTrackingStrikes);
            maxTrackingStrikes = Mathf.Max(minTrackingStrikes, maxTrackingStrikes);
            trackingWarningDuration = Mathf.Max(0.05f, trackingWarningDuration);
            trackingStrikeInterval = Mathf.Max(0f, trackingStrikeInterval);
            lineWarningDuration = Mathf.Max(0.05f, lineWarningDuration);
            lineAttackLength = Mathf.Max(0.1f, lineAttackLength);
            lineAttackWidth = Mathf.Clamp(lineAttackWidth, 0.1f, lineAttackLength);
            safeZoneWarningDuration = Mathf.Max(0.05f, safeZoneWarningDuration);
            safeZoneRadius = Mathf.Max(0.1f, safeZoneRadius);
            safeZoneFieldSize.x = Mathf.Max(safeZoneRadius * 2f, safeZoneFieldSize.x);
            safeZoneFieldSize.y = Mathf.Max(safeZoneRadius * 2f, safeZoneFieldSize.y);
            safeZoneMinDistance = Mathf.Max(0f, safeZoneMinDistance);
            safeZoneMaxDistance = Mathf.Max(safeZoneMinDistance, safeZoneMaxDistance);
            laserWarningDuration = Mathf.Max(0.05f, laserWarningDuration);
            laserActiveDuration = Mathf.Max(0.05f, laserActiveDuration);
            laserLength = Mathf.Max(0.1f, laserLength);
            laserWidth = Mathf.Clamp(laserWidth, 0.1f, laserLength);
            laserDamagePerTick = Mathf.Max(0f, laserDamagePerTick);
            laserDamageTickInterval = Mathf.Max(0.05f, laserDamageTickInterval);
            radialOrbCount = Mathf.Max(3, radialOrbCount);
            orbMovementSpeed = Mathf.Max(0.01f, orbMovementSpeed);
            orbWobbleAmplitude = Mathf.Max(0f, orbWobbleAmplitude);
            orbWobbleFrequency = Mathf.Max(0f, orbWobbleFrequency);
            orbCollisionRadius = Mathf.Max(0.05f, orbCollisionRadius);
            orbDamage = Mathf.Max(0f, orbDamage);
            aimedOrbSpawnOffset = Mathf.Max(0f, aimedOrbSpawnOffset);
            aimedOrbShotsPerAttack = Mathf.Max(1, aimedOrbShotsPerAttack);
            aimedOrbShotInterval = Mathf.Max(0.05f, aimedOrbShotInterval);
            orbPlayAreaSize.x = Mathf.Max(orbCollisionRadius * 2f, orbPlayAreaSize.x);
            orbPlayAreaSize.y = Mathf.Max(orbCollisionRadius * 2f, orbPlayAreaSize.y);
            bossDistanceWarningDuration = Mathf.Max(0.05f, bossDistanceWarningDuration);
            bossDistanceRadius = Mathf.Max(0.1f, bossDistanceRadius);
            bossDistanceFieldSize.x = Mathf.Max(
                bossDistanceRadius * 2f,
                bossDistanceFieldSize.x);
            bossDistanceFieldSize.y = Mathf.Max(
                bossDistanceRadius * 2f,
                bossDistanceFieldSize.y);
            trailPatternDuration = Mathf.Max(0.05f, trailPatternDuration);
            trailSpawnInterval = Mathf.Max(0.05f, trailSpawnInterval);
            trailOrbLifetime = Mathf.Max(0.05f, trailOrbLifetime);
            trailOrbCollisionRadius = Mathf.Max(0.05f, trailOrbCollisionRadius);
            trailOrbDamage = Mathf.Max(0f, trailOrbDamage);
            coneWarningDuration = Mathf.Max(0.05f, coneWarningDuration);
            coneAttackRange = Mathf.Max(0.1f, coneAttackRange);
            coneAttackAngle = Mathf.Clamp(coneAttackAngle, 1f, 359f);
            coneAttackDamage = Mathf.Max(0f, coneAttackDamage);
        }
#endif

        private enum AttackFamily
        {
            None,
            NonOrb,
            OrbProjectile
        }

        private enum BossAttackPattern
        {
            Circle,
            TrackingBarrage,
            Line,
            SafeZone,
            RotatingLaser,
            OrbFamily,
            BossDistance,
            MovementTrail,
            ForwardCone
        }
    }
}
