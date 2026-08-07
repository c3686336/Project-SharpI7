using SharpI7.Combat;

internal sealed class PlayerSpellCaster
{
    private readonly IDamageable target;

    public PlayerSpellCaster(IDamageable target)
    {
        this.target = target;
    }

    public bool TryCast(CastResult result, float currentMana)
    {
        if (!result.canCast || result.manaRelease > currentMana)
        {
            return false;
        }

        target.TakeDamage(result.actualDamage);
        return true;
    }
}
