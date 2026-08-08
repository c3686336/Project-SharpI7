public readonly struct ManaStatus
{
    public ManaStatus(
        float current,
        float warningThreshold,
        float overloadThreshold,
        float saturationThreshold)
    {
        Current = current;
        WarningThreshold = warningThreshold;
        OverloadThreshold = overloadThreshold;
        SaturationThreshold = saturationThreshold;
    }

    public float Current { get; }
    public float WarningThreshold { get; }
    public float OverloadThreshold { get; }
    public float SaturationThreshold { get; }
}
