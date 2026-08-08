using SharpI7.Combat;

internal sealed class PlayerSpellCaster
{
    private readonly BossHealth target;

    public PlayerSpellCaster(BossHealth target)
    {
        this.target = target;
    }

    public void Cast(CastResult result)
    {
        target.TakeSpellDamage(result.castLevel);
    }
}
