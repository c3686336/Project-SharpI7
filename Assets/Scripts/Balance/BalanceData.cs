using System;
using UnityEngine;

namespace SharpI7.Balance
{
    [Serializable]
    public sealed class BalanceData
    {
        public int schemaVersion;
        public PlayerBalance player;
        public BossBalanceCollection boss;
    }

    [Serializable]
    public sealed class PlayerBalance
    {
        public float maxHealth;
        public float moveSpeed;
        public float overloadDamage;
        public float invincibilityDuration;
        public PlayerManaBalance mana;
        public PlayerDashBalance dash;
    }

    [Serializable]
    public sealed class PlayerManaBalance
    {
        public float defaultValue;
        public float warningThreshold;
        public float overloadThreshold;
        public float saturationThreshold;
        public float fillSpeed;
    }

    [Serializable]
    public sealed class PlayerDashBalance
    {
        public float windupDuration;
        public float cooldownDuration;
        public float duration;
        public float distance;
    }

    public enum BossBalanceProfile
    {
        FloorOneGolem,
        FloorTwoSlime,
        TrainingDummy
    }

    [Serializable]
    public sealed class BossBalanceCollection
    {
        public BossBalance floorOneGolem;
        public BossBalance floorTwoSlime;
        public BossBalance dummy;

        public BossBalance Get(BossBalanceProfile profile)
        {
            return profile switch
            {
                BossBalanceProfile.FloorTwoSlime => floorTwoSlime,
                BossBalanceProfile.TrainingDummy => dummy ?? floorOneGolem,
                _ => floorOneGolem
            };
        }
    }
    [Serializable]
    public sealed class BossBalance
    {
        public BossHealthBalance health;
        public BossMovementBalance movement;
        public BossPhaseTwoBalance phaseTwo;
        public BossAttackTimingBalance attackTiming;
        public BossContactBalance contact;
        public TrackingBarrageBalance trackingBarrage;
        public LineAttackBalance lineAttack;
        public SafeZoneAttackBalance safeZoneAttack;
        public RotatingLaserBalance rotatingLaser;
        public DashLaserWallBalance dashLaserWall;
        public RadialOrbBalance radialOrb;
        public BossDistanceAttackBalance distanceAttack;
        public SlimeHopperBalance slimeHopper;
    }

    [Serializable]
    public sealed class BossHealthBalance
    {
        public float maxHealth;
        public bool enablePhaseTwo;
        public float phaseTwoMaxHealth;
        public float phaseTwoTransitionDelay;
    }

    [Serializable]
    public sealed class BossMovementBalance
    {
        public float moveSpeed;
        public float stoppingDistance;
    }

    [Serializable]
    public sealed class BossPhaseTwoBalance
    {
        public float attackDelayMultiplier;
        public float moveSpeedMultiplier;
    }

    [Serializable]
    public sealed class BossAttackTimingBalance
    {
        public float firstAttackDelay;
        public float recoveryDuration;
        public float chantOpportunityDuration;
        public float attackDamage;
        public float unavailableAttackRetryDelay;
        public int maxConsecutiveSamePattern;
    }

    [Serializable]
    public sealed class BossContactBalance
    {
        public float damage;
        public float interval;
    }

    [Serializable]
    public sealed class TrackingBarrageBalance
    {
        public bool enabled;
        public int minStrikes;
        public int maxStrikes;
        public float radius;
        public float warningDuration;
        public float strikeInterval;
        public float postStrikeDelay;
    }

    [Serializable]
    public sealed class LineAttackBalance
    {
        public bool enabled;
        public float warningDuration;
        public float length;
        public float width;
        public float postAttackDelay;
    }

    [Serializable]
    public sealed class SafeZoneAttackBalance
    {
        public bool enabled;
        public float warningDuration;
        public Vector2 fieldSize;
        public float radius;
        public float minDistance;
        public float maxDistance;
        public float postAttackDelay;
    }

    [Serializable]
    public sealed class RotatingLaserBalance
    {
        public bool enabled;
        public float warningDuration;
        public float activeDuration;
        public float length;
        public float width;
        public float damagePerTick;
        public float damageInvulnerabilityDuration;
        public float sweepDegrees;
        public float postAttackDelay;
    }

    [Serializable]
    public sealed class DashLaserWallBalance
    {
        public bool enabled;
        public Vector2 fieldSize;
        public float thickness;
        public float warningDuration;
        public float travelDuration;
        public float damage;
        public float postAttackDelay;
    }

    [Serializable]
    public sealed class RadialOrbBalance
    {
        public bool enabled;
        public int count;
        public float movementSpeed;
        public float wobbleAmplitude;
        public float wobbleFrequency;
        public float collisionRadius;
        public float damage;
        public Vector2 playAreaSize;
    }

    [Serializable]
    public sealed class SlimeHopperBalance
    {
        public int minCount;
        public int maxCount;
        public float warningDuration;
        public float warningWidth;
        public float speed;
        public float hitRadius;
        public float scale;
        public float lifetime;
        public float bounceFrequency;
        public float bounceScaleAmount;
        public float bounceHeight;
    }
    [Serializable]
    public sealed class BossDistanceAttackBalance
    {
        public bool enabled;
        public float warningDuration;
        public Vector2 fieldSize;
        public float radius;
        public float postAttackDelay;
    }
}
