using System;

public interface IPlayerMana
{
    event Action<ManaStatus> ManaStatusChanged;

    float MaxMana { get; }
    float CurrentMana { get; }
    ManaStatus ManaStatus { get; }

    void DeductMana(float amount);
}
