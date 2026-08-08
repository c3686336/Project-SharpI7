using System;
using System.Collections.Generic;
using UnityEngine;

internal interface ISpellCaster
{
    void Cast(CastResult result);
}

internal sealed class SpellCastRouter : IDisposable
{
    private readonly Dictionary<MagicType, ISpellCaster> casters = new();

    public void Register(MagicType magicType, ISpellCaster caster)
    {
        if (magicType == MagicType.None)
            throw new ArgumentOutOfRangeException(nameof(magicType));

        casters.Add(magicType, caster ?? throw new ArgumentNullException(nameof(caster)));
    }

    public void Cast(CastResult result)
    {
        if (!result.completed || !result.canCast)
            return;

        if (casters.TryGetValue(result.magicType, out ISpellCaster caster))
        {
            caster.Cast(result);
            return;
        }

        Debug.LogWarning($"[SpellCastRouter] 지원하지 않는 마법 타입입니다: {result.magicType}");
    }

    public void Dispose()
    {
        foreach (ISpellCaster caster in casters.Values)
        {
            if (caster is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        casters.Clear();
    }
}
