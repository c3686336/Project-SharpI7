using System;

public interface IChantManager
{
    bool IsCasting { get; }

    void SetManaSource(IPlayerMana manaSource);
    bool UnlockSpell(string spellId);
    bool IsSpellUnlocked(string spellId);
    void StartChant();
    void CancelChant();
    void InterruptChant();
    CastResult ResolveChant();

    event Action OnChantStarted;
    event Action OnChantCancelled;
    event Action OnChantInterrupted;
    event Action<CastResult> OnChantCast;
    event Action<ChantPreviewData> OnChantPreviewChanged;
    event Action<string> OnSpellUnlocked;
}