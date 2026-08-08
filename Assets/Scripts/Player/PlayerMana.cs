using UnityEngine;

internal sealed class PlayerMana
{
    private readonly float defaultValue;
    private readonly float warningThreshold;
    private readonly float overloadThreshold;
    private readonly float saturationThreshold;
    private readonly float fillSpeed;

    public PlayerMana(
        float defaultValue,
        float warningThreshold,
        float overloadThreshold,
        float saturationThreshold,
        float fillSpeed)
    {
        this.defaultValue = defaultValue;
        this.warningThreshold = warningThreshold;
        this.overloadThreshold = overloadThreshold;
        this.saturationThreshold = saturationThreshold;
        this.fillSpeed = fillSpeed;

        Current = Mathf.Clamp(defaultValue, 0f, saturationThreshold);
    }

    public float Current { get; private set; }
    public float SaturationThreshold => saturationThreshold;
    public ManaStatus Status => new(
        Current,
        warningThreshold,
        overloadThreshold,
        saturationThreshold);

    public bool Deduct(float amount)
    {
        if (amount <= 0f)
        {
            return false;
        }

        Current = Mathf.Max(0f, Current - amount);
        return true;
    }

    public bool Tick(float deltaTime)
    {
        Current = Mathf.Min(saturationThreshold, Current + deltaTime * fillSpeed);
        if (Current < saturationThreshold)
        {
            return false;
        }

        Current = Mathf.Clamp(defaultValue, 0f, saturationThreshold);
        return true;
    }
}
