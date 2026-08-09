using SharpI7.Combat;
using UnityEngine;

internal sealed class FireSpellCaster : ISpellCaster
{
    private readonly BossHealth target;
    private readonly Transform effectOrigin;
    private readonly SpellEffectRegistry effectRegistry;
    private readonly float projectileSpeed;
    private readonly float projectileScale;
    private readonly float levelTwoScaleMultiplier;
    private readonly float levelThreeScaleMultiplier;
    private readonly float hitRadius;
    private readonly float lifetime;
    private readonly AudioSource audioPlayer;
    private readonly AudioClip levelOneSFX;
    private readonly AudioClip levelTwoSFX;
    private readonly AudioClip levelThreeSFX;

    public FireSpellCaster(
        BossHealth target,
        Transform effectOrigin,
        SpellEffectRegistry effectRegistry,
        float projectileSpeed,
        float projectileScale,
        float levelTwoScaleMultiplier,
        float levelThreeScaleMultiplier,
        float hitRadius,
        float lifetime,
        AudioSource audioPlayer,
        AudioClip levelOneSFX,
        AudioClip levelTwoSFX,
        AudioClip levelThreeSFX)
    {
        this.target = target;
        this.effectOrigin = effectOrigin;
        this.effectRegistry = effectRegistry;
        this.projectileSpeed = projectileSpeed;
        this.projectileScale = projectileScale;
        this.levelTwoScaleMultiplier = levelTwoScaleMultiplier;
        this.levelThreeScaleMultiplier = levelThreeScaleMultiplier;
        this.hitRadius = hitRadius;
        this.lifetime = lifetime;
        this.audioPlayer = audioPlayer;
        this.levelOneSFX = levelOneSFX;
        this.levelTwoSFX = levelTwoSFX;
        this.levelThreeSFX = levelThreeSFX;
    }

    public void Cast(CastResult result)
    {
        if (target == null || !target.IsAlive || effectRegistry == null)
            return;

        PlayCastSFX(result.castLevel);

        GameObject effect = effectRegistry.SpawnEffect(
            result.effectId,
            effectOrigin.position,
            Quaternion.identity
        );

        if (effect == null)
            return;

        effect.transform.localScale *= projectileScale * GetScaleMultiplier(result.castLevel);

        HomingFireProjectile projectile = effect.GetComponent<HomingFireProjectile>();

        if (projectile == null)
        {
            projectile = effect.AddComponent<HomingFireProjectile>();
        }

        projectile.Initialize(
            target,
            result,
            projectileSpeed,
            hitRadius,
            lifetime
        );
    }

    private void PlayCastSFX(int castLevel)
    {
        if (audioPlayer == null)
            return;

        AudioClip clip = castLevel switch
        {
            1 => levelOneSFX,
            2 => levelTwoSFX,
            3 => levelThreeSFX,
            _ => null
        };

        if (clip != null)
        {
            audioPlayer.PlayOneShot(clip);
        }
    }

    private float GetScaleMultiplier(int castLevel)
    {
        if (castLevel >= 3)
            return levelThreeScaleMultiplier;

        return castLevel >= 2 ? levelTwoScaleMultiplier : 1f;
    }
}