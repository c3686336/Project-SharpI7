using SharpI7.Combat;

internal sealed class PlayerSpellCaster
{
    private readonly IDamageable target;

    public PlayerSpellCaster(IDamageable target)
    {
        this.target = target;
    }

    public void Cast(CastResult result)
    {
        target.TakeDamage(result.actualDamage);
    }
}
