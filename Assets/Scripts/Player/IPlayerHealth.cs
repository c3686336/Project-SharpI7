using System;
using SharpI7.Combat;

public interface IPlayerHealth : IDamageable
{
    event Action<float, float> HealthChanged;
    event Action Died;

    float MaxHealth { get; }
    float CurrentHealth { get; }
}
