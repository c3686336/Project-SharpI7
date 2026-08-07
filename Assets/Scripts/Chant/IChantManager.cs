using UnityEngine;
using System;

public interface IChantManager
{
    void StartChant();
    void CancelChant();
    void InterruptChant();
    CastResult ResolveChant();
    bool IsCasting { get; }
    event Action OnChantStarted;
    event Action OnChantCancelled;
    event Action OnChantInterrupted;
}
