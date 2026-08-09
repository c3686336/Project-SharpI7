using System.Collections;
using System.Collections.Generic;
using SharpI7.Balance;
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
        [SerializeField] private BossJumpShockwaveAttack jumpShockwaveAttack;
        [SerializeField] private SlimeStretchAttack slimeStretchAttack;
        [SerializeField] private SlimeBounceAttack slimeBounceAttack;
        [SerializeField] private SlimeHopperAttack slimeHopperAttack;

        private float firstAttackDelay;
        private float recoveryDuration;
        private float chantOpportunityDuration;
        private float attackDamage;
        private float unavailableAttackRetryDelay;
        private int maxConsecutiveSamePattern = 2;
        private bool enableTrackingBarrage;
        private int minTrackingStrikes;
        private int maxTrackingStrikes;
        private float trackingAttackRadius;
        private float trackingWarningDuration;
        private float trackingStrikeInterval;
        private float trackingPostStrikeDelay;
        private bool enableLineGroundAttack;
        private float lineWarningDuration;
        private float lineAttackLength;
        private float lineAttackWidth;
        private float linePostAttackDelay;
        private bool enableSafeZoneAttack;
        private float safeZoneWarningDuration;
        private Vector2 safeZoneFieldSize;
        private float safeZoneRadius;
        private float safeZoneMinDistance;
        private float safeZoneMaxDistance;
        private float safeZonePostAttackDelay;
        private bool enableRotatingLaserAttack;
        private float laserWarningDuration;
        private float laserActiveDuration;
        private float laserLength;
        private float laserWidth;
        private float laserDamagePerTick;
        private float laserPlayerDamageInvulnerabilityDuration;
        private float laserSweepDegrees;
        private float laserPostAttackDelay;
        private bool enableDashLaserWallAttack;
        private Vector2 dashLaserWallFieldSize;
        private float dashLaserWallThickness;
        private float dashLaserWallWarningDuration;
        private float dashLaserWallTravelDuration;
        private float dashLaserWallDamage;
        private float dashLaserWallPostAttackDelay;
        private float contactDamage;
        private float contactDamageInterval;
        private float phaseTwoAttackDelayMultiplier;
        private float phaseTwoMoveSpeedMultiplier;
        private bool enableRadialWobbleOrbAttack;
        private int radialOrbCount;
        private float orbMovementSpeed;
        private float orbWobbleAmplitude;
        private float orbWobbleFrequency;
        private float orbCollisionRadius;
        private float orbDamage;
        private Vector2 orbPlayAreaSize;
        private bool enableBossDistanceAttack;
        private float bossDistanceWarningDuration;
        private Vector2 bossDistanceFieldSize;
        private float bossDistanceRadius;
        private float bossDistancePostAttackDelay;

        private BossHealth bossHealth;
        private BossMovement bossMovement;
        private BossVisual bossVisual;
        private BossAttackAnimator bossAttackAnimator;
        private Coroutine attackRoutine;
        private CircularDangerZone activeZone;
        private LineDangerZone activeLineZone;
        private SafeZoneDanger activeSafeZone;
        private RotatingLaserDanger activeLaserZone;
        private DashLaserWallDanger activeDashLaserWall;
        private BossJumpShockwaveAttack activeJumpShockwave;
        private SlimeStretchAttack activeSlimeStretch;
        private SlimeBounceAttack activeSlimeBounce;
        private SlimeHopperAttack activeSlimeHopper;
        private readonly List<SlowWobbleOrb> activeOrbs = new();
        private readonly List<BossAttackPattern> availableAttacks = new();
        private BossDistanceDanger activeBossDistanceZone;
        private AttackFamily lastAttackFamily = AttackFamily.None;
        private BossDistanceDangerMode nextBossDistanceMode = BossDistanceDangerMode.InnerDanger;
        private IPlayerHealth subscribedPlayer;
        private bool playerWasAcquired;
        private bool combatStopped;
        private bool phaseTwoActive;
        private float nextContactDamageTime;
        private BossAttackPattern lastSelectedAttack;
        private int consecutiveAttackCount;

        private void Awake()
        {
            ApplyBalance(BossBalanceProfileSelector.Resolve(gameObject));
            bossHealth = GetComponent<BossHealth>();
            bossMovement = GetComponent<BossMovement>();
            bossVisual = GetComponent<BossVisual>();
            bossAttackAnimator = GetComponent<BossAttackAnimator>();
        }

        private void ApplyBalance(BossBalance data)
        {
            BossAttackTimingBalance timing = data.attackTiming;
            firstAttackDelay = timing.firstAttackDelay;
            recoveryDuration = timing.recoveryDuration;
            chantOpportunityDuration = timing.chantOpportunityDuration;
            attackDamage = timing.attackDamage;
            unavailableAttackRetryDelay = timing.unavailableAttackRetryDelay;
            maxConsecutiveSamePattern = Mathf.Max(1, timing.maxConsecutiveSamePattern);

            TrackingBarrageBalance tracking = data.trackingBarrage;
            enableTrackingBarrage = tracking.enabled;
            minTrackingStrikes = tracking.minStrikes;
            maxTrackingStrikes = tracking.maxStrikes;
            trackingAttackRadius = tracking.radius;
            trackingWarningDuration = tracking.warningDuration;
            trackingStrikeInterval = tracking.strikeInterval;
            trackingPostStrikeDelay = tracking.postStrikeDelay;

            LineAttackBalance line = data.lineAttack;
            enableLineGroundAttack = line.enabled;
            lineWarningDuration = line.warningDuration;
            lineAttackLength = line.length;
            lineAttackWidth = line.width;
            linePostAttackDelay = line.postAttackDelay;

            SafeZoneAttackBalance safeZone = data.safeZoneAttack;
            enableSafeZoneAttack = safeZone.enabled;
            safeZoneWarningDuration = safeZone.warningDuration;
            safeZoneFieldSize = safeZone.fieldSize;
            safeZoneRadius = safeZone.radius;
            safeZoneMinDistance = safeZone.minDistance;
            safeZoneMaxDistance = safeZone.maxDistance;
            safeZonePostAttackDelay = safeZone.postAttackDelay;

            RotatingLaserBalance laser = data.rotatingLaser;
            enableRotatingLaserAttack = laser.enabled;
            laserWarningDuration = laser.warningDuration;
            laserActiveDuration = laser.activeDuration;
            laserLength = laser.length;
            laserWidth = laser.width;
            laserDamagePerTick = laser.damagePerTick;
            laserPlayerDamageInvulnerabilityDuration = laser.damageInvulnerabilityDuration;
            laserSweepDegrees = laser.sweepDegrees;
            laserPostAttackDelay = laser.postAttackDelay;

            DashLaserWallBalance wall = data.dashLaserWall;
            enableDashLaserWallAttack = wall.enabled;
            dashLaserWallFieldSize = wall.fieldSize;
            dashLaserWallThickness = wall.thickness;
            dashLaserWallWarningDuration = wall.warningDuration;
            dashLaserWallTravelDuration = wall.travelDuration;
            dashLaserWallDamage = wall.damage;
            dashLaserWallPostAttackDelay = wall.postAttackDelay;

            contactDamage = data.contact.damage;
            contactDamageInterval = data.contact.interval;
            phaseTwoAttackDelayMultiplier = data.phaseTwo.attackDelayMultiplier;
            phaseTwoMoveSpeedMultiplier = data.phaseTwo.moveSpeedMultiplier;

            RadialOrbBalance orb = data.radialOrb;
            enableRadialWobbleOrbAttack = orb.enabled;
            radialOrbCount = orb.count;
            orbMovementSpeed = orb.movementSpeed;
            orbWobbleAmplitude = orb.wobbleAmplitude;
            orbWobbleFrequency = orb.wobbleFrequency;
            orbCollisionRadius = orb.collisionRadius;
            orbDamage = orb.damage;
            orbPlayAreaSize = orb.playAreaSize;

            BossDistanceAttackBalance distance = data.distanceAttack;
            enableBossDistanceAttack = distance.enabled;
            bossDistanceWarningDuration = distance.warningDuration;
            bossDistanceFieldSize = distance.fieldSize;
            bossDistanceRadius = distance.radius;
            bossDistancePostAttackDelay = distance.postAttackDelay;
            slimeHopperAttack?.Configure(data.slimeHopper);
        }

        private void OnEnable()
        {
            combatStopped = false;
            nextContactDamageTime = 0f;
            lastSelectedAttack = default;
            consecutiveAttackCount = 0;
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
            CancelActiveJumpShockwave();
            CancelActiveSlimeStretch();
            CancelActiveSlimeBounce();
            CancelActiveSlimeHopper();
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
                    yield return new WaitForSeconds(unavailableAttackRetryDelay);
                    continue;
                }

                BuildAvailableAttacks();
                if (availableAttacks.Count == 0)
                {
                    yield return new WaitForSeconds(unavailableAttackRetryDelay);
                    continue;
                }

                var selectedAttack = availableAttacks[Random.Range(0, availableAttacks.Count)];
                RecordSelectedAttack(selectedAttack);
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

            // A slime has its own attack set. It must never inherit a golem
            // pattern just because this controller is shared by both bosses.
            var isSlimeBoss = slimeStretchAttack != null || slimeBounceAttack != null;
            if (isSlimeBoss)
            {
                AddAttackIf(slimeStretchAttack != null, BossAttackPattern.SlimeStretch);
                AddAttackIf(slimeHopperAttack != null, BossAttackPattern.SlimeHopper);
                AddAttackIf(jumpShockwaveAttack != null, BossAttackPattern.JumpShockwave);
                RemoveBlockedConsecutiveAttack();
                return;
            }

            AddAttackIf(enableTrackingBarrage && dangerZonePrefab != null, BossAttackPattern.TrackingBarrage);
            AddAttackIf(enableLineGroundAttack && lineDangerZonePrefab != null, BossAttackPattern.Line);
            AddAttackIf(enableSafeZoneAttack && safeZoneDangerPrefab != null, BossAttackPattern.SafeZone);
            AddAttackIf(enableRotatingLaserAttack && rotatingLaserDangerPrefab != null, BossAttackPattern.RotatingLaser);
            AddAttackIf(enableDashLaserWallAttack && dashLaserWallDangerPrefab != null, BossAttackPattern.DashLaserWall);
            AddAttackIf(jumpShockwaveAttack != null, BossAttackPattern.JumpShockwave);
            AddAttackIf(
                lastAttackFamily != AttackFamily.OrbProjectile && enableRadialWobbleOrbAttack
                    && slowWobbleOrbPrefab != null,
                BossAttackPattern.OrbFamily);
            AddAttackIf(enableBossDistanceAttack && bossDistanceDangerPrefab != null, BossAttackPattern.BossDistance);
            RemoveBlockedConsecutiveAttack();
        }
        private void RemoveBlockedConsecutiveAttack()
        {
            if (consecutiveAttackCount < maxConsecutiveSamePattern)
            {
                return;
            }

            availableAttacks.RemoveAll(attack => attack == lastSelectedAttack);
        }

        private void RecordSelectedAttack(BossAttackPattern attack)
        {
            consecutiveAttackCount = attack == lastSelectedAttack
                ? consecutiveAttackCount + 1
                : 1;
            lastSelectedAttack = attack;
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
                case BossAttackPattern.JumpShockwave:
                    yield return PerformJumpShockwaveAttack();
                    break;
                case BossAttackPattern.SlimeStretch:
                    yield return PerformSlimeStretchAttack();
                    break;
                case BossAttackPattern.SlimeBounce:
                    yield return PerformSlimeBounceAttack();
                    break;
                case BossAttackPattern.SlimeHopper:
                    yield return PerformSlimeHopperAttack();
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
            CancelActiveJumpShockwave();
            CancelActiveSlimeStretch();
            CancelActiveSlimeBounce();
            CancelActiveSlimeHopper();
            CancelActiveOrbs();
            CancelActiveBossDistanceZone();
            bossAttackAnimator?.StopAnimation();

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
                if (behaviour is IPlayerHealth player && player.IsAlive)
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
            // Both distance patterns are centered on the boss, so keep the
            // anchor fixed while their warning and damage resolve.
            if (bossMovement != null)
            {
                bossMovement.LockMovement();
            }

            var fieldCenter = (Vector2)transform.position;
            var fieldSize = bossDistanceFieldSize;
            // Use the background bounds first so the outer danger overlay covers
            // the entire visible map, not only the smaller interior between walls.
            if (ArenaBounds.TryGetWorldBounds(out var arenaBounds))
            {
                fieldCenter = (Vector2)arenaBounds.center;
                fieldSize = (Vector2)arenaBounds.size;
            }
            else if (ArenaBounds.TryGetWallInteriorBounds(out var wallInteriorBounds))
            {
                fieldCenter = (Vector2)wallInteriorBounds.center;
                fieldSize = (Vector2)wallInteriorBounds.size;
            }

            activeBossDistanceZone = Instantiate(
                bossDistanceDangerPrefab,
                new Vector3(fieldCenter.x, fieldCenter.y, 0f),
                Quaternion.identity);
            activeBossDistanceZone.Begin(
                transform,
                playerTarget,
                nextBossDistanceMode,
                fieldCenter,
                fieldSize,
                bossDistanceRadius,
                bossDistanceWarningDuration,
                attackDamage);
            PlayAttackAnimationBeforeImpact(bossDistanceWarningDuration);

            nextBossDistanceMode = nextBossDistanceMode == BossDistanceDangerMode.InnerDanger
                ? BossDistanceDangerMode.OuterDanger
                : BossDistanceDangerMode.InnerDanger;

            yield return new WaitForSeconds(bossDistanceWarningDuration + bossDistancePostAttackDelay);
            activeBossDistanceZone = null;

            if (bossMovement != null && bossHealth.IsAlive && !bossHealth.IsTransitioningToPhaseTwo)
            {
                bossMovement.UnlockMovement();
            }
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
            PlayAttackAnimationImmediately();

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
                laserPlayerDamageInvulnerabilityDuration,
                laserSweepDegrees);
            PlayAttackAnimationBeforeImpact(laserWarningDuration);

            yield return new WaitForSeconds(
                laserWarningDuration + laserActiveDuration + laserPostAttackDelay);
            activeLaserZone = null;
        }

        private IEnumerator PerformDashLaserWallAttack()
        {
            var fieldBounds = new Bounds(
                transform.position,
                new Vector3(dashLaserWallFieldSize.x, dashLaserWallFieldSize.y, 1f));
            if (ArenaBounds.TryGetWallInteriorBounds(out var wallInteriorBounds))
            {
                fieldBounds = wallInteriorBounds;
            }
            else if (ArenaBounds.TryGetWorldBounds(out var arenaBounds))
            {
                fieldBounds = arenaBounds;
            }

            var direction = Random.value < 0.5f
                ? DashLaserWallDirection.LeftToRight
                : DashLaserWallDirection.RightToLeft;

            activeDashLaserWall = Instantiate(dashLaserWallDangerPrefab, transform.position, Quaternion.identity);
            activeDashLaserWall.Begin(
                playerTarget,
                fieldBounds,
                direction,
                dashLaserWallThickness,
                dashLaserWallWarningDuration,
                dashLaserWallTravelDuration,
                dashLaserWallDamage);
            PlayAttackAnimationBeforeImpact(dashLaserWallWarningDuration);

            yield return new WaitForSeconds(
                dashLaserWallWarningDuration + dashLaserWallTravelDuration +
                dashLaserWallPostAttackDelay);
            activeDashLaserWall = null;
        }

        private IEnumerator PerformSlimeHopperAttack()
        {
            var fieldBounds = new Bounds(transform.position, new Vector3(45f, 27f, 1f));
            if (ArenaBounds.TryGetWallInteriorBounds(out var wallInteriorBounds))
            {
                fieldBounds = wallInteriorBounds;
            }
            else if (ArenaBounds.TryGetWorldBounds(out var arenaBounds))
            {
                fieldBounds = arenaBounds;
            }

            activeSlimeHopper = slimeHopperAttack;
            var duration = activeSlimeHopper.Begin(playerTarget, fieldBounds, attackDamage);
            yield return new WaitForSeconds(duration);
            activeSlimeHopper = null;
        }
        private IEnumerator PerformSlimeBounceAttack()
        {
            if (bossMovement != null)
            {
                bossMovement.LockMovement();
            }

            var fieldBounds = new Bounds(transform.position, new Vector3(45f, 27f, 1f));
            if (ArenaBounds.TryGetWallInteriorBounds(out var wallInteriorBounds))
            {
                fieldBounds = wallInteriorBounds;
            }
            else if (ArenaBounds.TryGetWorldBounds(out var arenaBounds))
            {
                fieldBounds = arenaBounds;
            }

            activeSlimeBounce = slimeBounceAttack;
            var duration = activeSlimeBounce.Begin(playerTarget, fieldBounds, attackDamage);
            yield return new WaitForSeconds(duration);
            activeSlimeBounce = null;

            if (bossMovement != null && bossHealth.IsAlive && !bossHealth.IsTransitioningToPhaseTwo)
            {
                bossMovement.UnlockMovement();
            }
        }
        private IEnumerator PerformSlimeStretchAttack()
        {
            if (bossMovement != null)
            {
                bossMovement.LockMovement();
            }

            var horizontal = Random.value < 0.5f;
            var stretchLength = lineAttackLength;
            if (ArenaBounds.TryGetWorldBounds(out var arenaBounds))
            {
                stretchLength = horizontal
                    ? 2f * Mathf.Max(
                        Mathf.Abs(transform.position.x - arenaBounds.min.x),
                        Mathf.Abs(arenaBounds.max.x - transform.position.x))
                    : 2f * Mathf.Max(
                        Mathf.Abs(transform.position.y - arenaBounds.min.y),
                        Mathf.Abs(arenaBounds.max.y - transform.position.y));
            }

            activeSlimeStretch = slimeStretchAttack;
            var duration = activeSlimeStretch.Begin(
                playerTarget,
                horizontal,
                stretchLength,
                lineAttackWidth,
                lineWarningDuration,
                attackDamage);
            yield return new WaitForSeconds(duration);
            activeSlimeStretch = null;

            if (bossMovement != null && bossHealth.IsAlive && !bossHealth.IsTransitioningToPhaseTwo)
            {
                bossMovement.UnlockMovement();
            }
        }
        private IEnumerator PerformJumpShockwaveAttack()
        {
            if (bossMovement != null)
            {
                bossMovement.LockMovement();
            }

            var fieldBounds = new Bounds(transform.position, new Vector3(45f, 27f, 1f));
            if (ArenaBounds.TryGetWallInteriorBounds(out var wallInteriorBounds))
            {
                fieldBounds = wallInteriorBounds;
            }
            else if (ArenaBounds.TryGetWorldBounds(out var arenaBounds))
            {
                fieldBounds = arenaBounds;
            }

            activeJumpShockwave = jumpShockwaveAttack;
            var duration = activeJumpShockwave.Begin(playerTarget, fieldBounds, attackDamage);
            yield return new WaitForSeconds(duration);
            activeJumpShockwave = null;

            if (bossMovement != null && bossHealth.IsAlive && !bossHealth.IsTransitioningToPhaseTwo)
            {
                bossMovement.UnlockMovement();
            }
        }
        private IEnumerator PerformSafeZoneAttack()
        {
            if (bossMovement != null)
            {
                bossMovement.LockMovement();
            }

            var fieldCenter = (Vector2)transform.position;
            var fieldSize = safeZoneFieldSize;
            if (ArenaBounds.TryGetWorldBounds(out var arenaBounds))
            {
                fieldCenter = (Vector2)arenaBounds.center;
                fieldSize = (Vector2)arenaBounds.size;
            }

            var safeZoneBoundsCenter = fieldCenter;
            var safeZoneBoundsSize = fieldSize;
            if (ArenaBounds.TryGetWallInteriorBounds(out var wallInteriorBounds))
            {
                safeZoneBoundsCenter = (Vector2)wallInteriorBounds.center;
                safeZoneBoundsSize = (Vector2)wallInteriorBounds.size;
            }

            var safePosition = ChooseSafeZonePosition(safeZoneBoundsCenter, safeZoneBoundsSize);
            var attackPosition = new Vector3(fieldCenter.x, fieldCenter.y, 0f);

            activeSafeZone = Instantiate(safeZoneDangerPrefab, attackPosition, Quaternion.identity);
            activeSafeZone.Begin(
                playerTarget,
                safePosition,
                fieldSize + Vector2.one * 0.1f,
                safeZoneRadius,
                safeZoneWarningDuration,
                attackDamage);
            PlayAttackAnimationBeforeImpact(safeZoneWarningDuration);

            yield return new WaitForSeconds(safeZoneWarningDuration + safeZonePostAttackDelay);
            activeSafeZone = null;

            if (bossMovement != null && bossHealth.IsAlive && !bossHealth.IsTransitioningToPhaseTwo)
            {
                bossMovement.UnlockMovement();
            }
        }

        private Vector2 ChooseSafeZonePosition(Vector2 fieldCenter, Vector2 fieldSize)
        {
            var availableHalfWidth = Mathf.Max(0f, fieldSize.x * 0.5f - safeZoneRadius);
            var availableHalfHeight = Mathf.Max(0f, fieldSize.y * 0.5f - safeZoneRadius);
            var minimumBossDistance = GetMinimumSafeZoneDistanceFromBoss();
            var safestPosition = fieldCenter;
            var greatestBossDistance = float.NegativeInfinity;

            // Avoid safe zones that cover half or more of the boss.
            for (var attempt = 0; attempt < 24; attempt++)
            {
                var direction = Random.insideUnitCircle;
                if (direction.sqrMagnitude < 0.001f)
                {
                    direction = Vector2.right;
                }

                direction.Normalize();
                var distance = Random.Range(safeZoneMinDistance, safeZoneMaxDistance);
                var candidate = (Vector2)playerTarget.position + direction * distance;
                candidate.x = Mathf.Clamp(candidate.x, fieldCenter.x - availableHalfWidth, fieldCenter.x + availableHalfWidth);
                candidate.y = Mathf.Clamp(candidate.y, fieldCenter.y - availableHalfHeight, fieldCenter.y + availableHalfHeight);

                var bossDistance = Vector2.Distance(candidate, transform.position);
                if (bossDistance > greatestBossDistance)
                {
                    greatestBossDistance = bossDistance;
                    safestPosition = candidate;
                }

                if (bossDistance >= minimumBossDistance)
                {
                    return candidate;
                }
            }

            return safestPosition;
        }

        private float GetMinimumSafeZoneDistanceFromBoss()
        {
            var bossRadius = 0f;
            if (TryGetComponent<CircleCollider2D>(out var bossCollider))
            {
                var scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
                bossRadius = bossCollider.radius * scale;
            }

            return safeZoneRadius + (bossRadius * bossRadius) / Mathf.Max(0.01f, safeZoneRadius * 2f);
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
            PlayAttackAnimationBeforeImpact(lineWarningDuration);

            yield return new WaitForSeconds(lineWarningDuration + linePostAttackDelay);
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
                yield return new WaitForSeconds(
                    trackingWarningDuration + trackingPostStrikeDelay);
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
            PlayAttackAnimationBeforeImpact(duration);
        }

        private void PlayAttackAnimationBeforeImpact(float warningDuration)
        {
            bossAttackAnimator?.PlayBeforeImpact(warningDuration);
        }

        private void PlayAttackAnimationImmediately()
        {
            bossAttackAnimator?.PlayImmediately();
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
                if (!(behaviour is IPlayerHealth player))
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
            bossAttackAnimator?.StopAnimation();

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
            CancelActiveJumpShockwave();
            CancelActiveSlimeStretch();
            CancelActiveSlimeBounce();
            CancelActiveSlimeHopper();
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

        private void CancelActiveSlimeHopper()
        {
            if (slimeHopperAttack != null)
            {
                slimeHopperAttack.Cancel();
            }

            activeSlimeHopper = null;
        }
        private void CancelActiveSlimeBounce()
        {
            if (activeSlimeBounce == null)
            {
                return;
            }

            activeSlimeBounce.Cancel();
            activeSlimeBounce = null;
        }
        private void CancelActiveSlimeStretch()
        {
            if (activeSlimeStretch == null)
            {
                return;
            }

            activeSlimeStretch.Cancel();
            activeSlimeStretch = null;
        }
        private void CancelActiveJumpShockwave()
        {
            if (activeJumpShockwave == null)
            {
                return;
            }

            activeJumpShockwave.Cancel();
            activeJumpShockwave = null;
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
            JumpShockwave,
            SlimeStretch,
            SlimeBounce,
            SlimeHopper,
            OrbFamily,
            BossDistance
        }
    }
}
