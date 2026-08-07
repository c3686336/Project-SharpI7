using System;
using SharpI7.Combat;

public enum ManaState
{
    Normal,
    Warning,
    Saturated
}

public readonly struct ManaStatus
{
    public ManaStatus(
        float current,
        float warningThreshold,
        float saturationThreshold,
        float displayMaximum,
        float saturationDuration,
        float saturationRemaining,
        ManaState state)
    {
        Current = current;
        WarningThreshold = warningThreshold;
        SaturationThreshold = saturationThreshold;
        DisplayMaximum = displayMaximum;
        SaturationDuration = saturationDuration;
        SaturationRemaining = saturationRemaining;
        State = state;
    }

    public float Current { get; }
    public float WarningThreshold { get; }
    public float SaturationThreshold { get; }
    public float DisplayMaximum { get; }
    public float SaturationDuration { get; }
    public float SaturationRemaining { get; }
    public ManaState State { get; }
    public bool IsWarning => State == ManaState.Warning;
    public bool IsSaturated => State == ManaState.Saturated;
}

public interface IPlayer : IDamageable
{
    event Action<float, float> HealthChanged;
    event Action<ManaStatus> ManaStatusChanged;
    event Action Died;

    float DashCooldownUntil { get; }
    bool IsDashing { get; }
    float MaxHealth { get; }
    float CurrentHealth { get; }
    float MaxMana { get; }
    float CurrentMana { get; }
    ManaStatus ManaStatus { get; }

    void LockMovement();
    void UnlockMovement();

    void DeductMana(float amount);
}
