using System;

public interface IChantManager
{
    bool IsCasting { get; }

    void StartChant();

    void CancelChant();

    void InterruptChant();

    CastResult ResolveChant();

    bool SetSpell(string spellId);

    event Action OnChantStarted;

    event Action OnChantCancelled;

    event Action OnChantInterrupted;

    event Action<CastResult> OnChantCast;
}