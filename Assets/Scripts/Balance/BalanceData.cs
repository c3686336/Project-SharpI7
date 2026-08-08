using System;
using UnityEngine;

namespace SharpI7.Balance
{
    [Serializable]
    public sealed class BalanceData
    {
        public int schemaVersion;
        public PlayerBalance player;
        public BossBalance boss;
    }

    [Serializable]
    public sealed class PlayerBalance
    {
        public float maxHealth;
        public float moveSpeed;
        public float overloadDamage;
        public PlayerManaBalance mana;
        public PlayerDashBalance dash;
    }

    [Serializable]
    public sealed class PlayerManaBalance
    {
        public float defaultValue;
        public float warningThreshold;
        public float saturationThreshold;
        public float fillSpeed;
        public float saturationDuration;
    }

    [Serializable]
    public sealed class PlayerDashBalance
    {
        public float windupDuration;
        public float cooldownDuration;
        public float duration;
        public float distance;
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
    }

    [Serializable]
    public sealed class BossHealthBalance
    {
        public float maxHealth;
        public bool enablePhaseTwo;
        public float phaseTwoMaxHealth;
        public float phaseTwoTransitionDelay;
        public float damagePerWord;
        public int maxWordDamageStage;
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
    public sealed class BossDistanceAttackBalance
    {
        public bool enabled;
        public float warningDuration;
        public Vector2 fieldSize;
        public float radius;
        public float postAttackDelay;
    }
}
