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
