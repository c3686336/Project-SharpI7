using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SharpI7.Combat;
using UnityEngine;

internal sealed class LightningSpellCaster : ISpellCaster, IDisposable
{
    private const float LevelThreeStrikeDamage = 70f;
    private const float LevelThreeTickDamage = 5f;
    private const int LevelThreeTickCount = 5;
    private const float LevelThreeTickInterval = 0.8f;

    private const string LevelOneEffectId = "FlickingLightning_Lv1";
    private const string LevelTwoEffectId = "FlickingLightning_Lv2";
    private const string LevelThreeImpactEffectId = "FlickingLightning_Lv3_Impact";
    private const string LevelThreeTickEffectId = "FlickingLightning_Lv3_Tick";

    private readonly BossHealth target;
    private readonly SpellEffectRegistry effectRegistry;
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly AudioSource audioPlayer;
    private readonly AudioClip lightningSFX;

    private bool disposed;

    public LightningSpellCaster(
        BossHealth target,
        SpellEffectRegistry effectRegistry,
        CancellationToken lifetimeToken,
        AudioSource audioPlayer,
        AudioClip lightningSFX)
    {
        this.target = target;
        this.effectRegistry = effectRegistry;
        this.audioPlayer = audioPlayer;
        this.lightningSFX = lightningSFX;

        cancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
    }

    public void Cast(CastResult result)
    {
        if (disposed || target == null || !target.IsAlive)
            return;

        PlayLightningSFX();

        switch (result.castLevel)
        {
            case 1:
                CastStandardLightning(result, LevelOneEffectId);
                break;
            case 2:
                CastStandardLightning(result, LevelTwoEffectId);
                break;
            case 3:
                CastLevelThreeAsync().Forget();
                break;
            default:
                Debug.LogWarning(
                    $"[LightningSpellCaster] 지원하지 않는 번개 주문 단계입니다: {result.castLevel}"
                );
                break;
        }
    }

    private void CastStandardLightning(CastResult result, string effectId)
    {
        if (target == null || !target.IsAlive)
            return;

        if (effectRegistry != null && !string.IsNullOrEmpty(effectId))
        {
            effectRegistry.SpawnEffect(
                effectId,
                target.transform.position,
                Quaternion.identity
            );
        }

        target.TakeDamageWithoutSpellHitEffect(result.actualDamage);
    }

    private async UniTask CastLevelThreeAsync()
    {
        CancellationToken token = cancellationTokenSource.Token;

        if (!CanContinueLevelThree())
            return;

        SpawnLevelThreeImpactEffect();
        target.TakeDamageWithoutSpellHitEffect(LevelThreeStrikeDamage);

        for (int i = 0; i < LevelThreeTickCount; i++)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(LevelThreeTickInterval),
                cancellationToken: token
            );

            if (!CanContinueLevelThree())
                return;

            SpawnLevelThreeTickEffect();
            target.TakeDamageWithoutSpellHitEffect(LevelThreeTickDamage);
        }
    }

    private bool CanContinueLevelThree()
    {
        if (disposed || cancellationTokenSource.IsCancellationRequested)
            return false;

        if (target == null || !target.IsAlive)
            return false;

        if (target.IsTransitioningToPhaseTwo)
            return false;

        return true;
    }

    private void SpawnLevelThreeImpactEffect()
    {
        if (effectRegistry == null || target == null)
            return;

        effectRegistry.SpawnEffect(
            LevelThreeImpactEffectId,
            target.transform.position,
            Quaternion.identity
        );
    }

    private void SpawnLevelThreeTickEffect()
    {
        if (effectRegistry == null || target == null)
            return;

        GameObject effect = effectRegistry.SpawnEffect(
            LevelThreeTickEffectId,
            target.transform.position,
            Quaternion.identity
        );

        if (effect == null)
        {
            Debug.LogWarning("[LightningSpellCaster] 번개 Tick 이펙트 생성 실패");
        }
    }

    private void PlayLightningSFX()
    {
        if (audioPlayer != null && lightningSFX != null)
        {
            audioPlayer.PlayOneShot(lightningSFX);
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        if (!cancellationTokenSource.IsCancellationRequested)
        {
            cancellationTokenSource.Cancel();
        }

        cancellationTokenSource.Dispose();
    }
}