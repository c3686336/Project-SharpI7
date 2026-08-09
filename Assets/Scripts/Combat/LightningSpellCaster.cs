using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SharpI7.Combat;
using UnityEngine;

internal sealed class LightningSpellCaster : ISpellCaster, IDisposable
{
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
    private readonly AudioClip levelOneSFX;
    private readonly AudioClip levelTwoSFX;
    private readonly AudioClip levelThreeImpactSFX;
    private readonly AudioClip levelThreeTickSFX;

    private bool disposed;

    public LightningSpellCaster(
        BossHealth target,
        SpellEffectRegistry effectRegistry,
        CancellationToken lifetimeToken,
        AudioSource audioPlayer,
        AudioClip levelOneSFX,
        AudioClip levelTwoSFX,
        AudioClip levelThreeImpactSFX,
        AudioClip levelThreeTickSFX)
    {
        this.target = target;
        this.effectRegistry = effectRegistry;
        this.audioPlayer = audioPlayer;
        this.levelOneSFX = levelOneSFX;
        this.levelTwoSFX = levelTwoSFX;
        this.levelThreeImpactSFX = levelThreeImpactSFX;
        this.levelThreeTickSFX = levelThreeTickSFX;

        cancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
    }

    public void Cast(CastResult result)
    {
        if (disposed || target == null || !target.IsAlive)
            return;

        switch (result.castLevel)
        {
            case 1:
                CastStandardLightning(result, LevelOneEffectId, levelOneSFX);
                break;

            case 2:
                CastStandardLightning(result, LevelTwoEffectId, levelTwoSFX);
                break;

            case 3:
                CastLevelThreeAsync(result.actualDamage).Forget();
                break;

            default:
                Debug.LogWarning(
                    $"[LightningSpellCaster] 지원하지 않는 번개 주문 단계입니다: {result.castLevel}"
                );
                break;
        }
    }

    private void CastStandardLightning(
        CastResult result,
        string effectId,
        AudioClip castSFX)
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

        PlaySFX(castSFX);

        target.TakeDamageWithoutSpellHitEffect(result.actualDamage);
    }

    private async UniTask CastLevelThreeAsync(float totalDamage)
    {
        CancellationToken token = cancellationTokenSource.Token;

        float totalTickDamage = LevelThreeTickDamage * LevelThreeTickCount;
        float strikeDamage = Mathf.Max(0f, totalDamage - totalTickDamage);

        if (!CanContinueLevelThree())
            return;

        SpawnLevelThreeImpactEffect();
        PlaySFX(levelThreeImpactSFX);

        target.TakeDamageWithoutSpellHitEffect(strikeDamage);

        for (int i = 0; i < LevelThreeTickCount; i++)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(LevelThreeTickInterval),
                    cancellationToken: token
                );
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!CanContinueLevelThree())
                return;

            SpawnLevelThreeTickEffect();
            PlaySFX(levelThreeTickSFX);

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

        GameObject effect = effectRegistry.SpawnEffect(
            LevelThreeImpactEffectId,
            target.transform.position,
            Quaternion.identity
        );

        if (effect == null)
        {
            Debug.LogWarning(
                "[LightningSpellCaster] 번개 Lv3 Impact 이펙트 생성 실패"
            );
        }
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
            Debug.LogWarning(
                "[LightningSpellCaster] 번개 Lv3 Tick 이펙트 생성 실패"
            );
        }
    }

    private void PlaySFX(AudioClip clip)
    {
        if (audioPlayer != null && clip != null)
        {
            audioPlayer.PlayOneShot(clip);
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