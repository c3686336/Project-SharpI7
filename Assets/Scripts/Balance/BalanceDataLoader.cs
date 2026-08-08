using System;
using System.IO;
using UnityEngine;

namespace SharpI7.Balance
{
    public static class BalanceDataLoader
    {
        private const string DefaultFileName = "balance.json";

        private static readonly (string Name, int Count)[] RequiredProperties =
        {
            ("schemaVersion", 1),
            ("maxHealth", 2),
            ("moveSpeed", 2),
            ("overloadDamage", 1),
            ("defaultValue", 1),
            ("warningThreshold", 1),
            ("saturationThreshold", 1),
            ("fillSpeed", 1),
            ("saturationDuration", 1),
            ("windupDuration", 1),
            ("cooldownDuration", 1),
            ("duration", 1),
            ("distance", 1),
            ("enablePhaseTwo", 1),
            ("phaseTwoMaxHealth", 1),
            ("phaseTwoTransitionDelay", 1),
            ("spellDamageByStage", 1),
            ("stoppingDistance", 1),
            ("attackDelayMultiplier", 1),
            ("moveSpeedMultiplier", 1),
            ("firstAttackDelay", 1),
            ("recoveryDuration", 1),
            ("chantOpportunityDuration", 1),
            ("attackDamage", 1),
            ("unavailableAttackRetryDelay", 1),
            ("damage", 3),
            ("interval", 1),
            ("enabled", 7),
            ("minStrikes", 1),
            ("maxStrikes", 1),
            ("radius", 3),
            ("warningDuration", 6),
            ("strikeInterval", 1),
            ("postStrikeDelay", 1),
            ("length", 2),
            ("width", 2),
            ("postAttackDelay", 5),
            ("fieldSize", 3),
            ("x", 4),
            ("y", 4),
            ("minDistance", 1),
            ("maxDistance", 1),
            ("activeDuration", 1),
            ("damagePerTick", 1),
            ("damageInvulnerabilityDuration", 1),
            ("sweepDegrees", 1),
            ("thickness", 1),
            ("travelDuration", 1),
            ("count", 1),
            ("movementSpeed", 1),
            ("wobbleAmplitude", 1),
            ("wobbleFrequency", 1),
            ("collisionRadius", 1),
            ("playAreaSize", 1)
        };

        private static BalanceData current;

        public static BalanceData Current => current ??= Load(DefaultFileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            current = null;
        }

        public static BalanceData Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("A balance file name is required.", nameof(fileName));
            }

            string path = Path.Combine(Application.streamingAssetsPath, fileName);
            string json;

            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Failed to read balance data at '{path}'.", exception);
            }

            ValidateRequiredProperties(json, path);

            BalanceData data;

            try
            {
                data = JsonUtility.FromJson<BalanceData>(json);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Failed to parse balance data at '{path}'.", exception);
            }

            Validate(data, path);
            Debug.Log($"[BalanceDataLoader] Loaded balance data from {path}");
            return data;
        }

        private static void ValidateRequiredProperties(string json, string path)
        {
            foreach ((string name, int expectedCount) in RequiredProperties)
            {
                string token = $"\"{name}\"";
                int actualCount = 0;
                int index = 0;

                while ((index = json.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
                {
                    actualCount++;
                    index += token.Length;
                }

                if (actualCount != expectedCount)
                {
                    throw new InvalidDataException(
                        $"Expected {expectedCount} '{name}' properties in '{path}', found {actualCount}.");
                }
            }
        }

        private static void Validate(BalanceData data, string path)
        {
            if (data == null || data.schemaVersion != 1)
            {
                throw new InvalidDataException($"Unsupported or missing balance schema version in '{path}'.");
            }

            if (data?.player?.mana == null || data.player.dash == null)
            {
                throw new InvalidDataException($"Player balance data is incomplete in '{path}'.");
            }

            BossBalance boss = data.boss;
            if (boss?.health == null || boss.movement == null || boss.phaseTwo == null ||
                boss.attackTiming == null || boss.contact == null || boss.trackingBarrage == null ||
                boss.lineAttack == null || boss.safeZoneAttack == null || boss.rotatingLaser == null ||
                boss.dashLaserWall == null || boss.radialOrb == null || boss.distanceAttack == null)
            {
                throw new InvalidDataException($"Boss balance data is incomplete in '{path}'.");
            }

            RequirePositive(data.player.maxHealth, "player.maxHealth", path);
            RequireNonNegative(data.player.moveSpeed, "player.moveSpeed", path);
            RequireNonNegative(data.player.overloadDamage, "player.overloadDamage", path);
            RequireNonNegative(data.player.mana.defaultValue, "player.mana.defaultValue", path);
            RequireNonNegative(data.player.mana.warningThreshold, "player.mana.warningThreshold", path);
            RequirePositive(data.player.mana.saturationThreshold, "player.mana.saturationThreshold", path);
            RequireNonNegative(data.player.mana.fillSpeed, "player.mana.fillSpeed", path);
            RequireAtLeast(
                data.player.mana.saturationDuration,
                0.1f,
                "player.mana.saturationDuration",
                path);

            if (data.player.mana.warningThreshold > data.player.mana.saturationThreshold)
            {
                throw new InvalidDataException($"player.mana.warningThreshold exceeds saturationThreshold in '{path}'.");
            }

            float manaMaximum = data.player.mana.saturationThreshold +
                                data.player.mana.fillSpeed * data.player.mana.saturationDuration;
            if (data.player.mana.defaultValue > manaMaximum)
            {
                throw new InvalidDataException($"player.mana.defaultValue exceeds the display maximum in '{path}'.");
            }

            RequireNonNegative(data.player.dash.windupDuration, "player.dash.windupDuration", path);
            RequireNonNegative(data.player.dash.cooldownDuration, "player.dash.cooldownDuration", path);
            RequireNonNegative(data.player.dash.duration, "player.dash.duration", path);
            RequireNonNegative(data.player.dash.distance, "player.dash.distance", path);

            RequirePositive(boss.health.maxHealth, "boss.health.maxHealth", path);
            RequirePositive(boss.health.phaseTwoMaxHealth, "boss.health.phaseTwoMaxHealth", path);
            RequireNonNegative(boss.health.phaseTwoTransitionDelay, "boss.health.phaseTwoTransitionDelay", path);
            if (boss.health.spellDamageByStage == null || boss.health.spellDamageByStage.Length == 0)
            {
                throw new InvalidDataException($"boss.health.spellDamageByStage is required in '{path}'.");
            }

            for (var index = 0; index < boss.health.spellDamageByStage.Length; index++)
            {
                RequireNonNegative(
                    boss.health.spellDamageByStage[index],
                    $"boss.health.spellDamageByStage[{index}]",
                    path);
            }
            RequireNonNegative(boss.movement.moveSpeed, "boss.movement.moveSpeed", path);
            RequireNonNegative(boss.movement.stoppingDistance, "boss.movement.stoppingDistance", path);
            RequireAtLeast(
                boss.phaseTwo.attackDelayMultiplier,
                0.1f,
                "boss.phaseTwo.attackDelayMultiplier",
                path);
            RequireNonNegative(boss.phaseTwo.moveSpeedMultiplier, "boss.phaseTwo.moveSpeedMultiplier", path);

            if (boss.phaseTwo.attackDelayMultiplier > 1f)
            {
                throw new InvalidDataException($"boss.phaseTwo.attackDelayMultiplier cannot exceed one in '{path}'.");
            }

            RequireNonNegative(boss.attackTiming.firstAttackDelay, "boss.attackTiming.firstAttackDelay", path);
            RequireNonNegative(boss.attackTiming.recoveryDuration, "boss.attackTiming.recoveryDuration", path);
            RequireNonNegative(
                boss.attackTiming.chantOpportunityDuration,
                "boss.attackTiming.chantOpportunityDuration",
                path);
            RequireNonNegative(boss.attackTiming.attackDamage, "boss.attackTiming.attackDamage", path);
            RequirePositive(
                boss.attackTiming.unavailableAttackRetryDelay,
                "boss.attackTiming.unavailableAttackRetryDelay",
                path);
            RequireNonNegative(boss.contact.damage, "boss.contact.damage", path);
            RequireAtLeast(boss.contact.interval, 0.05f, "boss.contact.interval", path);

            if (!boss.trackingBarrage.enabled && !boss.lineAttack.enabled &&
                !boss.safeZoneAttack.enabled && !boss.rotatingLaser.enabled &&
                !boss.dashLaserWall.enabled && !boss.radialOrb.enabled &&
                !boss.distanceAttack.enabled)
            {
                throw new InvalidDataException($"At least one boss attack must be enabled in '{path}'.");
            }

            if (boss.trackingBarrage.minStrikes < 1 ||
                boss.trackingBarrage.maxStrikes < boss.trackingBarrage.minStrikes)
            {
                throw new InvalidDataException($"Tracking barrage strike counts are invalid in '{path}'.");
            }

            RequireAtLeast(boss.trackingBarrage.radius, 0.1f, "boss.trackingBarrage.radius", path);
            RequireAtLeast(
                boss.trackingBarrage.warningDuration,
                0.05f,
                "boss.trackingBarrage.warningDuration",
                path);
            RequireNonNegative(
                boss.trackingBarrage.strikeInterval,
                "boss.trackingBarrage.strikeInterval",
                path);
            RequireNonNegative(
                boss.trackingBarrage.postStrikeDelay,
                "boss.trackingBarrage.postStrikeDelay",
                path);

            RequireAtLeast(boss.lineAttack.warningDuration, 0.05f, "boss.lineAttack.warningDuration", path);
            RequireAtLeast(boss.lineAttack.length, 0.1f, "boss.lineAttack.length", path);
            RequireAtLeast(boss.lineAttack.width, 0.1f, "boss.lineAttack.width", path);
            RequireNonNegative(boss.lineAttack.postAttackDelay, "boss.lineAttack.postAttackDelay", path);
            if (boss.lineAttack.width > boss.lineAttack.length)
            {
                throw new InvalidDataException($"boss.lineAttack.width exceeds its length in '{path}'.");
            }

            RequireAtLeast(
                boss.safeZoneAttack.warningDuration,
                0.05f,
                "boss.safeZoneAttack.warningDuration",
                path);
            RequireVectorPositive(boss.safeZoneAttack.fieldSize, "boss.safeZoneAttack.fieldSize", path);
            RequireAtLeast(boss.safeZoneAttack.radius, 0.1f, "boss.safeZoneAttack.radius", path);
            RequireNonNegative(boss.safeZoneAttack.minDistance, "boss.safeZoneAttack.minDistance", path);
            RequireNonNegative(boss.safeZoneAttack.maxDistance, "boss.safeZoneAttack.maxDistance", path);
            RequireNonNegative(
                boss.safeZoneAttack.postAttackDelay,
                "boss.safeZoneAttack.postAttackDelay",
                path);
            if (boss.safeZoneAttack.maxDistance < boss.safeZoneAttack.minDistance)
            {
                throw new InvalidDataException($"Safe-zone distances are invalid in '{path}'.");
            }

            RequireFieldContainsRadius(
                boss.safeZoneAttack.fieldSize,
                boss.safeZoneAttack.radius,
                "boss.safeZoneAttack.fieldSize",
                path);

            RequireAtLeast(
                boss.rotatingLaser.warningDuration,
                0.05f,
                "boss.rotatingLaser.warningDuration",
                path);
            RequireAtLeast(
                boss.rotatingLaser.activeDuration,
                0.05f,
                "boss.rotatingLaser.activeDuration",
                path);
            RequireAtLeast(boss.rotatingLaser.length, 0.1f, "boss.rotatingLaser.length", path);
            RequireAtLeast(boss.rotatingLaser.width, 0.1f, "boss.rotatingLaser.width", path);
            RequireNonNegative(boss.rotatingLaser.damagePerTick, "boss.rotatingLaser.damagePerTick", path);
            RequireAtLeast(
                boss.rotatingLaser.damageInvulnerabilityDuration,
                0.05f,
                "boss.rotatingLaser.damageInvulnerabilityDuration",
                path);
            RequireFinite(boss.rotatingLaser.sweepDegrees, "boss.rotatingLaser.sweepDegrees", path);
            RequireNonNegative(
                boss.rotatingLaser.postAttackDelay,
                "boss.rotatingLaser.postAttackDelay",
                path);
            if (boss.rotatingLaser.width > boss.rotatingLaser.length)
            {
                throw new InvalidDataException($"boss.rotatingLaser.width exceeds its length in '{path}'.");
            }

            RequireVectorAtLeast(boss.dashLaserWall.fieldSize, 0.1f, "boss.dashLaserWall.fieldSize", path);
            RequireAtLeast(boss.dashLaserWall.thickness, 0.1f, "boss.dashLaserWall.thickness", path);
            RequireAtLeast(
                boss.dashLaserWall.warningDuration,
                0.05f,
                "boss.dashLaserWall.warningDuration",
                path);
            RequireAtLeast(
                boss.dashLaserWall.travelDuration,
                0.05f,
                "boss.dashLaserWall.travelDuration",
                path);
            RequireNonNegative(boss.dashLaserWall.damage, "boss.dashLaserWall.damage", path);
            RequireNonNegative(
                boss.dashLaserWall.postAttackDelay,
                "boss.dashLaserWall.postAttackDelay",
                path);

            if (boss.radialOrb.count < 3)
            {
                throw new InvalidDataException($"boss.radialOrb.count must be at least three in '{path}'.");
            }

            RequireAtLeast(boss.radialOrb.movementSpeed, 0.01f, "boss.radialOrb.movementSpeed", path);
            RequireNonNegative(boss.radialOrb.wobbleAmplitude, "boss.radialOrb.wobbleAmplitude", path);
            RequireNonNegative(boss.radialOrb.wobbleFrequency, "boss.radialOrb.wobbleFrequency", path);
            RequireAtLeast(
                boss.radialOrb.collisionRadius,
                0.05f,
                "boss.radialOrb.collisionRadius",
                path);
            RequireNonNegative(boss.radialOrb.damage, "boss.radialOrb.damage", path);
            RequireVectorPositive(boss.radialOrb.playAreaSize, "boss.radialOrb.playAreaSize", path);
            RequireFieldContainsRadius(
                boss.radialOrb.playAreaSize,
                boss.radialOrb.collisionRadius,
                "boss.radialOrb.playAreaSize",
                path);

            RequireAtLeast(
                boss.distanceAttack.warningDuration,
                0.05f,
                "boss.distanceAttack.warningDuration",
                path);
            RequireVectorPositive(boss.distanceAttack.fieldSize, "boss.distanceAttack.fieldSize", path);
            RequireAtLeast(boss.distanceAttack.radius, 0.1f, "boss.distanceAttack.radius", path);
            RequireNonNegative(
                boss.distanceAttack.postAttackDelay,
                "boss.distanceAttack.postAttackDelay",
                path);
            RequireFieldContainsRadius(
                boss.distanceAttack.fieldSize,
                boss.distanceAttack.radius,
                "boss.distanceAttack.fieldSize",
                path);
        }

        private static void RequirePositive(float value, string field, string path)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                throw new InvalidDataException($"{field} must be greater than zero in '{path}'.");
            }
        }

        private static void RequirePositive(int value, string field, string path)
        {
            if (value <= 0)
            {
                throw new InvalidDataException($"{field} must be greater than zero in '{path}'.");
            }
        }

        private static void RequireNonNegative(float value, string field, string path)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new InvalidDataException($"{field} must be non-negative in '{path}'.");
            }
        }

        private static void RequireAtLeast(float value, float minimum, string field, string path)
        {
            if (!IsFinite(value) || value < minimum)
            {
                throw new InvalidDataException($"{field} must be at least {minimum} in '{path}'.");
            }
        }

        private static void RequireFinite(float value, string field, string path)
        {
            if (!IsFinite(value))
            {
                throw new InvalidDataException($"{field} must be finite in '{path}'.");
            }
        }

        private static void RequireVectorPositive(Vector2 value, string field, string path)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || value.x <= 0f || value.y <= 0f)
            {
                throw new InvalidDataException($"{field} components must be greater than zero in '{path}'.");
            }
        }

        private static void RequireVectorAtLeast(
            Vector2 value,
            float minimum,
            string field,
            string path)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || value.x < minimum || value.y < minimum)
            {
                throw new InvalidDataException(
                    $"{field} components must be at least {minimum} in '{path}'.");
            }
        }

        private static void RequireFieldContainsRadius(
            Vector2 fieldSize,
            float radius,
            string field,
            string path)
        {
            if (fieldSize.x < radius * 2f || fieldSize.y < radius * 2f)
            {
                throw new InvalidDataException($"{field} must contain its configured radius in '{path}'.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
