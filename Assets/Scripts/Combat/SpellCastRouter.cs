using UnityEngine;

internal sealed class SpellCastRouter
{
    private const string FireMagicType = "Fire";
    private const string LightningMagicType = "Lightning";

    private readonly PlayerSpellCaster fireSpellCaster;
    private readonly LightningSpellCaster lightningSpellCaster;

    public SpellCastRouter(PlayerSpellCaster fireSpellCaster, LightningSpellCaster lightningSpellCaster)
    {
        this.fireSpellCaster = fireSpellCaster;
        this.lightningSpellCaster = lightningSpellCaster;
    }

    public void Cast(CastResult result)
    {
        if (!result.completed || !result.canCast)
            return;

        switch (result.magicType)
        {
            case FireMagicType:
                fireSpellCaster?.Cast(result);
                break;
            case LightningMagicType:
                lightningSpellCaster?.Cast(result);
                break;
            default:
                Debug.LogWarning($"[SpellCastRouter] 지원하지 않는 마법 타입입니다: {result.magicType}");
                break;
        }
    }
}