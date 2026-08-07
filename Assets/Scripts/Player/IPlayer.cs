using System;
using SharpI7.Combat;

public interface IPlayer : IDamageable
{
    event Action<float, float> HealthChanged;
    event Action Died;

    float DashCooldownUntil { get; }
    float MaxHealth { get; }
    float CurrentHealth { get; }

    void LockMovement();
    void UnlockMovement();
}
