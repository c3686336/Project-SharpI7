using UnityEngine;

internal sealed class PlayerMana
{
    private readonly float defaultValue;
    private readonly float warningThreshold;
    private readonly float saturationThreshold;
    private readonly float fillSpeed;
    private readonly float saturationDuration;

    private float saturationRemaining;

    public PlayerMana(
        float defaultValue,
        float warningThreshold,
        float saturationThreshold,
        float fillSpeed,
        float saturationDuration)
    {
        this.defaultValue = defaultValue;
        this.warningThreshold = warningThreshold;
        this.saturationThreshold = saturationThreshold;
        this.fillSpeed = fillSpeed;
        this.saturationDuration = saturationDuration;

        Current = Mathf.Clamp(defaultValue, 0f, DisplayMaximum);
        saturationRemaining = saturationDuration;
    }

    public float Current { get; private set; }
    public float SaturationThreshold => saturationThreshold;
    public float DisplayMaximum => saturationThreshold + fillSpeed * saturationDuration;
    public ManaStatus Status => new(
        Current,
        warningThreshold,
        saturationThreshold,
        DisplayMaximum,
        saturationDuration,
        Current >= saturationThreshold ? saturationRemaining : 0f,
        GetState());

    public bool Deduct(float amount)
    {
        if (amount <= 0f)
        {
            return false;
        }

        Current = Mathf.Max(0f, Current - amount);
        if (Current < saturationThreshold)
        {
            saturationRemaining = saturationDuration;
        }

        return true;
    }

    public bool Tick(float deltaTime)
    {
        Current = Mathf.Min(DisplayMaximum, Current + deltaTime * fillSpeed);

        if (Current < saturationThreshold)
        {
            saturationRemaining = saturationDuration;
            return false;
        }

        saturationRemaining = Mathf.Max(0f, saturationRemaining - deltaTime);
        if (saturationRemaining > 0f)
        {
            return false;
        }

        Current = Mathf.Clamp(defaultValue, 0f, DisplayMaximum);
        saturationRemaining = saturationDuration;
        return true;
    }

    private ManaState GetState()
    {
        if (Current >= saturationThreshold)
        {
            return ManaState.Saturated;
        }

        return Current >= warningThreshold ? ManaState.Warning : ManaState.Normal;
    }
}
