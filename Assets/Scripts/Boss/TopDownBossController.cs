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
        [SerializeField] private DashLaserWallDanger dashLaserWallDangerPrefab;

        [Header("Attack Timing")]
        [SerializeField, Min(0f)] private float firstAttackDelay = 3f;
        [SerializeField, Min(0f)] private float recoveryDuration = 5f;
        [SerializeField, Min(0f)] private float chantOpportunityDuration = 1f;
        [SerializeField, Min(0f)] private float attackDamage = 1f;

        [Header("Tracking Barrage")]
        [SerializeField] private bool enableTrackingBarrage = true;
        [SerializeField, Min(1)] private int minTrackingStrikes = 3;
        [SerializeField, Min(1)] private int maxTrackingStrikes = 5;
        [SerializeField, Min(0.1f)] private float trackingAttackRadius = 2.25f;
        [SerializeField, Min(0.05f)] private float trackingWarningDuration = 1.5f;
        [SerializeField, Min(0f)] private float trackingStrikeInterval = 0.35f;

        [Header("Line Ground Attack")]
        [SerializeField] private bool enableLineGroundAttack = true;
        [SerializeField, Min(0.05f)] private float lineWarningDuration = 2.5f;
        [SerializeField, Min(0.1f)] private float lineAttackLength = 16f;
        [SerializeField, Min(0.1f)] private float lineAttackWidth = 2f;

        [Header("Safe Zone Attack")]
        [SerializeField] private bool enableSafeZoneAttack = true;
        [SerializeField, Min(0.05f)] private float safeZoneWarningDuration = 4f;
        [SerializeField] private Vector2 safeZoneFieldSize = new(30f, 18f);
        [SerializeField, Min(0.1f)] private float safeZoneRadius = 3f;
        [SerializeField, Min(0f)] private float safeZoneMinDistance = 5f;
        [SerializeField, Min(0f)] private float safeZoneMaxDistance = 9f;

        [Header("Rotating Laser Attack")]
        [SerializeField] private bool enableRotatingLaserAttack = true;
        [SerializeField, Min(0.05f)] private float laserWarningDuration = 2f;
        [SerializeField, Min(0.05f)] private float laserActiveDuration = 5f;
        [SerializeField, Min(0.1f)] private float laserLength = 14f;
        [SerializeField, Min(0.1f)] private float laserWidth = 1.2f;
        [SerializeField, Min(0f)] private float laserDamagePerTick = 1f;
        [SerializeField, Min(0.05f)] private float laserPlayerDamageInvulnerabilityDuration = 1f;

        [Header("Dash Laser Wall Attack")]
        [SerializeField] private bool enableDashLaserWallAttack = true;
        [SerializeField] private Vector2 dashLaserWallFieldSize = new(45f, 27f);
        [SerializeField, Min(0.1f)] private float dashLaserWallThickness = 1.4f;
        [SerializeField, Min(0.05f)] private float dashLaserWallWarningDuration = 1.2f;
        [SerializeField, Min(0.05f)] private float dashLaserWallTravelDuration = 2.7f;
        [SerializeField, Min(0f)] private float dashLaserWallDamage = 1f;

        [Header("Contact Damage")]
        [SerializeField, Min(0f)] private float contactDamage = 1f;
        [SerializeField, Min(0.05f)] private float contactDamageInterval = 1f;

        [Header("Phase Two")]
        [SerializeField, Range(0.1f, 1f)] private float phaseTwoAttackDelayMultiplier = 0.7f;
        [SerializeField, Min(0f)] private float phaseTwoMoveSpeedMultiplier = 1.5f;

        [Header("Radial Wobble Orb Attack")]
        [SerializeField] private bool enableRadialWobbleOrbAttack = true;
        [SerializeField, Min(3)] private int radialOrbCount = 12;
        [SerializeField, Min(0.01f)] private float orbMovementSpeed = 1.2f;
        [SerializeField, Min(0f)] private float orbWobbleAmplitude = 0.8f;
        [SerializeField, Min(0f)] private float orbWobbleFrequency = 0.6f;
        [SerializeField, Min(0.05f)] private float orbCollisionRadius = 0.55f;
        [SerializeField, Min(0f)] private float orbDamage = 1f;
        [SerializeField] private Vector2 orbPlayAreaSize = new(30f, 18f);

        [Header("Boss Distance Attack")]
        [SerializeField] private bool enableBossDistanceAttack = true;
        [SerializeField, Min(0.05f)] private float bossDistanceWarningDuration = 4f;
        [SerializeField] private Vector2 bossDistanceFieldSize = new(30f, 18f);
        [SerializeField, Min(0.1f)] private float bossDistanceRadius = 6f;

        private BossHealth bossHealth;
        private BossMovement bossMovement;
        private BossVisual bossVisual;
        private Coroutine attackRoutine;
        private CircularDangerZone activeZone;
        private LineDangerZone activeLineZone;
        private SafeZoneDanger activeSafeZone;
        private RotatingLaserDanger activeLaserZone;
        private DashLaserWallDanger activeDashLaserWall;
        private readonly List<SlowWobbleOrb> activeOrbs = new();
        private readonly List<BossAttackPattern> availableAttacks = new();
        private BossDistanceDanger activeBossDistanceZone;
        private AttackFamily lastAttackFamily = AttackFamily.None;
        private BossDistanceDangerMode nextBossDistanceMode = BossDistanceDangerMode.InnerDanger;
        private IPlayer subscribedPlayer;
        private bool playerWasAcquired;
        private bool combatStopped;
        private bool phaseTwoActive;
        private float nextContactDamageTime;

        private void Awake()
        {
            bossHealth = GetComponent<BossHealth>();
            bossMovement = GetComponent<BossMovement>();
            bossVisual = GetComponent<BossVisual>();
        }

        private void OnEnable()
        {
            combatStopped = false;
            nextContactDamageTime = 0f;
            bossHealth.Died += StopAttacking;
            bossHealth.PhaseTwoTransitionStarted += BeginPhaseTwoTransition;
            bossHealth.PhaseTwoStarted += StartPhaseTwo;
            phaseTwoActive = bossHealth.IsPhaseTwo;
            if (phaseTwoActive && bossMovement != null)
            {
                bossMovement.SetSpeedMultiplier(phaseTwoMoveSpeedMultiplier);
            }
            ResolvePlayerTarget();
            attackRoutine = StartCoroutine(AttackLoop());
        }

        private void Update()
        {
            if (combatStopped || !bossHealth.IsAlive)
            {
                return;
            }

            if (!playerWasAcquired)
            {
                ResolvePlayerTarget();
                return;
            }

            if (!IsPlayerAlive())
            {
                StopAttacking();
            }
        }

        private void OnDisable()
        {
            bossHealth.Died -= StopAttacking;
            bossHealth.PhaseTwoTransitionStarted -= BeginPhaseTwoTransition;
            bossHealth.PhaseTwoStarted -= StartPhaseTwo;
            UnsubscribeFromPlayerDeath();

            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            CancelActiveZone();
            CancelActiveLineZone();
            CancelActiveSafeZone();
            CancelActiveLaserZone();
            CancelActiveDashLaserWall();
            CancelActiveOrbs();
            CancelActiveBossDistanceZone();
        }

        public void SetPlayerTarget(Transform target)
        {
            UnsubscribeFromPlayerDeath();
            playerTarget = target;
            bossMovement.SetPlayerTarget(target);
            playerWasAcquired = target != null;
            SubscribeToPlayerDeath(target);
        }

        public void NotifyPlayerDied()
        {
            StopAttacking();
        }

        private IEnumerator AttackLoop(bool waitForFirstAttack = true)
        {
            if (waitForFirstAttack && firstAttackDelay > 0f)
            {
                yield return new WaitForSeconds(firstAttackDelay);
            }

            while (bossHealth.IsAlive && !combatStopped)
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

                if (chantOpportunityDuration > 0f)
                {
                    yield return new WaitForSeconds(GetPhaseAdjustedDelay(chantOpportunityDuration));
                }

                if (recoveryDuration > 0f)
                {
                    yield return new WaitForSeconds(GetPhaseAdjustedDelay(recoveryDuration));
                }
            }
        }

        private void BuildAvailableAttacks()
        {
            availableAttacks.Clear();

            AddAttackIf(enableTrackingBarrage && dangerZonePrefab != null, BossAttackPattern.TrackingBarrage);
            AddAttackIf(enableLineGroundAttack && lineDangerZonePrefab != null, BossAttackPattern.Line);
            AddAttackIf(enableSafeZoneAttack && safeZoneDangerPrefab != null, BossAttackPattern.SafeZone);
            AddAttackIf(enableRotatingLaserAttack && rotatingLaserDangerPrefab != null, BossAttackPattern.RotatingLaser);
            AddAttackIf(enableDashLaserWallAttack && dashLaserWallDangerPrefab != null, BossAttackPattern.DashLaserWall);
            AddAttackIf(
                lastAttackFamily != AttackFamily.OrbProjectile && enableRadialWobbleOrbAttack
                    && slowWobbleOrbPrefab != null,
                BossAttackPattern.OrbFamily);
            AddAttackIf(enableBossDistanceAttack && bossDistanceDangerPrefab != null, BossAttackPattern.BossDistance);
        }

        private void AddAttackIf(bool condition, BossAttackPattern attack)
        {
            if (condition)
            {
                availableAttacks.Add(attack);
            }
        }

        private IEnumerator PerformAttack(BossAttackPattern attack)
        {
            switch (attack)
            {
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
                case BossAttackPattern.DashLaserWall:
                    yield return PerformDashLaserWallAttack();
                    break;
                case BossAttackPattern.OrbFamily:
                    yield return PerformOrbFamilyAttack();
                    break;
                case BossAttackPattern.BossDistance:
                    yield return PerformBossDistanceAttack();
                    break;
            }
        }

        private void StartPhaseTwo()
        {
            phaseTwoActive = true;
            if (bossMovement != null)
            {
                bossMovement.SetSpeedMultiplier(phaseTwoMoveSpeedMultiplier);
                bossMovement.UnlockMovement();
            }

            if (!combatStopped && attackRoutine == null)
            {
                attackRoutine = StartCoroutine(AttackLoop(false));
            }
        }

        private void BeginPhaseTwoTransition()
        {
            if (combatStopped)
            {
                return;
            }

            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            CancelActiveZone();
            CancelActiveLineZone();
            CancelActiveSafeZone();
            CancelActiveLaserZone();
            CancelActiveDashLaserWall();
            CancelActiveOrbs();
            CancelActiveBossDistanceZone();

            if (bossMovement != null)
            {
                bossMovement.LockMovement();
            }
        }

        private float GetPhaseAdjustedDelay(float delay)
        {
            return phaseTwoActive ? delay * phaseTwoAttackDelayMultiplier : delay;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (combatStopped || !bossHealth.IsAlive || bossHealth.IsTransitioningToPhaseTwo || contactDamage <= 0f || Time.time < nextContactDamageTime)
            {
                return;
            }

            foreach (var behaviour in other.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (behaviour is IPlayer player && player.IsAlive)
                {
                    var previousHealth = player.CurrentHealth;
                    player.TakeDamage(contactDamage);
                    if (player.CurrentHealth < previousHealth)
                    {
                        nextContactDamageTime = Time.time + contactDamageInterval;
                    }

                    return;
                }
            }
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
            yield return PerformRadialWobbleOrbAttack();
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
                laserPlayerDamageInvulnerabilityDuration);

            yield return new WaitForSeconds(laserWarningDuration + laserActiveDuration + 0.15f);
            activeLaserZone = null;
        }

        private IEnumerator PerformDashLaserWallAttack()
        {
            var fieldBounds = new Bounds(
                transform.position,
                new Vector3(dashLaserWallFieldSize.x, dashLaserWallFieldSize.y, 1f));
            var direction = (DashLaserWallDirection)Random.Range(
                0,
                System.Enum.GetValues(typeof(DashLaserWallDirection)).Length);

            activeDashLaserWall = Instantiate(dashLaserWallDangerPrefab, transform.position, Quaternion.identity);
            activeDashLaserWall.Begin(
                playerTarget,
                fieldBounds,
                direction,
                dashLaserWallThickness,
                dashLaserWallWarningDuration,
                dashLaserWallTravelDuration,
                dashLaserWallDamage);

            yield return new WaitForSeconds(dashLaserWallWarningDuration + dashLaserWallTravelDuration + 0.1f);
            activeDashLaserWall = null;
        }

        private IEnumerator PerformSafeZoneAttack()
        {
            if (bossMovement != null)
            {
                bossMovement.LockMovement();
            }

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

            if (bossMovement != null && bossHealth.IsAlive && !bossHealth.IsTransitioningToPhaseTwo)
            {
                bossMovement.UnlockMovement();
            }
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
            activeZone.Begin(playerTarget, trackingAttackRadius, duration, attackDamage);
        }

        private void ResolvePlayerTarget()
        {
            if (playerTarget != null)
            {
                if (!playerWasAcquired)
                {
                    SetPlayerTarget(playerTarget);
                }

                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                SetPlayerTarget(player.transform);
            }
        }

        private void SubscribeToPlayerDeath(Transform target)
        {
            if (target == null)
            {
                return;
            }

            var behaviours = target.GetComponentsInParent<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (!(behaviour is IPlayer player))
                {
                    continue;
                }

                subscribedPlayer = player;
                subscribedPlayer.Died += StopAttacking;
                return;
            }
        }

        private void UnsubscribeFromPlayerDeath()
        {
            if (subscribedPlayer == null)
            {
                return;
            }

            subscribedPlayer.Died -= StopAttacking;
            subscribedPlayer = null;
        }

        private bool IsPlayerAlive()
        {
            if (subscribedPlayer != null)
            {
                return subscribedPlayer.IsAlive;
            }

            if (playerTarget == null || !playerTarget.gameObject.activeInHierarchy)
            {
                return false;
            }

            var behaviours = playerTarget.GetComponentsInParent<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IDamageable damageable)
                {
                    return damageable.IsAlive;
                }
            }

            foreach (var behaviour in behaviours)
            {
                var getHpMethod = behaviour.GetType().GetMethod(
                    "GetHp",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
                    null,
                    System.Type.EmptyTypes,
                    null);
                if (getHpMethod != null && getHpMethod.ReturnType == typeof(int))
                {
                    return (int)getHpMethod.Invoke(behaviour, null) > 0;
                }
            }

            return true;
        }

        private void StopAttacking()
        {
            combatStopped = true;

            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            CancelActiveZone();
            CancelActiveLineZone();
            CancelActiveSafeZone();
            CancelActiveLaserZone();
            CancelActiveDashLaserWall();
            CancelActiveOrbs();
            CancelActiveBossDistanceZone();

            if (bossMovement != null)
            {
                bossMovement.LockMovement();
            }
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

        private void CancelActiveDashLaserWall()
        {
            if (activeDashLaserWall == null)
            {
                return;
            }

            activeDashLaserWall.Cancel();
            activeDashLaserWall = null;
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            firstAttackDelay = Mathf.Max(0f, firstAttackDelay);
            recoveryDuration = Mathf.Max(0f, recoveryDuration);
            chantOpportunityDuration = Mathf.Max(0f, chantOpportunityDuration);
            attackDamage = Mathf.Max(0f, attackDamage);
            minTrackingStrikes = Mathf.Max(1, minTrackingStrikes);
            maxTrackingStrikes = Mathf.Max(minTrackingStrikes, maxTrackingStrikes);
            trackingAttackRadius = Mathf.Max(0.1f, trackingAttackRadius);
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
            laserPlayerDamageInvulnerabilityDuration = Mathf.Max(0.05f, laserPlayerDamageInvulnerabilityDuration);
            dashLaserWallFieldSize.x = Mathf.Max(0.1f, dashLaserWallFieldSize.x);
            dashLaserWallFieldSize.y = Mathf.Max(0.1f, dashLaserWallFieldSize.y);
            dashLaserWallThickness = Mathf.Max(0.1f, dashLaserWallThickness);
            dashLaserWallWarningDuration = Mathf.Max(0.05f, dashLaserWallWarningDuration);
            dashLaserWallTravelDuration = Mathf.Max(0.05f, dashLaserWallTravelDuration);
            dashLaserWallDamage = Mathf.Max(0f, dashLaserWallDamage);
            contactDamage = Mathf.Max(0f, contactDamage);
            contactDamageInterval = Mathf.Max(0.05f, contactDamageInterval);
            phaseTwoAttackDelayMultiplier = Mathf.Clamp(phaseTwoAttackDelayMultiplier, 0.1f, 1f);
            phaseTwoMoveSpeedMultiplier = Mathf.Max(0f, phaseTwoMoveSpeedMultiplier);
            radialOrbCount = Mathf.Max(3, radialOrbCount);
            orbMovementSpeed = Mathf.Max(0.01f, orbMovementSpeed);
            orbWobbleAmplitude = Mathf.Max(0f, orbWobbleAmplitude);
            orbWobbleFrequency = Mathf.Max(0f, orbWobbleFrequency);
            orbCollisionRadius = Mathf.Max(0.05f, orbCollisionRadius);
            orbDamage = Mathf.Max(0f, orbDamage);
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
            TrackingBarrage,
            Line,
            SafeZone,
            RotatingLaser,
            DashLaserWall,
            OrbFamily,
            BossDistance
        }
    }
}
