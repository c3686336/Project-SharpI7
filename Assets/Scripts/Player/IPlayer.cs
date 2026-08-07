using System;
using SharpI7.Combat;

public interface IPlayer : IDamageable
{
    event Action<float, float> HealthChanged;
    event Action Died;

    float DashCooldownUntil { get; }
    bool IsDashing { get; }
    float MaxHealth { get; }
    float CurrentHealth { get; }
    float MaxMana { get; }
    float CurrentMana { get; }

    void LockMovement();
    void UnlockMovement();

    void DeductMana(float amount);
}
