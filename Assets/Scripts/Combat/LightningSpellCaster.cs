using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SharpI7.Combat;
using UnityEngine;

internal sealed class LightningSpellCaster : IDisposable
{
    private const float LevelThreeTickDamage = 5f;
    private const int LevelThreeTickCount = 5;
    private const float LevelThreeTickInterval = 0.8f;
    private const float EffectLifetime = 1.05f;

    private const string LevelOneEffectId = "FlickingLightning_Lv1";
    private const string LevelTwoEffectId = "FlickingLightning_Lv2";
    private const string LevelThreeImpactEffectId = "FlickingLightning_Lv3_Impact";
    private const string LevelThreeTickEffectId = "FlickingLightning_Lv3_Tick";

    private readonly BossHealth target;
    private readonly Transform playerEffectOrigin;
    private readonly SpellEffectRegistry effectRegistry;
    private readonly CancellationTokenSource cancellationTokenSource;

    private bool disposed;

    public LightningSpellCaster(BossHealth target, Transform playerEffectOrigin, SpellEffectRegistry effectRegistry, CancellationToken lifetimeToken)
    {
        this.target = target;
        this.playerEffectOrigin = playerEffectOrigin;
        this.effectRegistry = effectRegistry;
        cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
    }

    public void Cast(CastResult result)
    {
        if (disposed || target == null || !target.IsAlive)
            return;

        switch (result.castLevel)
        {
            case 1:
                CastStandardLightning(result.actualDamage, LevelOneEffectId);
                break;
            case 2:
                CastStandardLightning(result.actualDamage, LevelTwoEffectId);
                break;
            case 3:
                CastLevelThreeAsync(result.actualDamage).Forget();
                break;
            default:
                Debug.LogWarning($"[LightningSpellCaster] 지원하지 않는 번개 주문 단계입니다: {result.castLevel}");
                break;
        }
    }

    private void CastStandardLightning(float damage, string effectId)
    {
        if (target == null || !target.IsAlive)
            return;

        if (effectRegistry != null)
        {
            GameObject effect = effectRegistry.SpawnEffect(
                effectId,
                target.transform.position,
                Quaternion.identity);

            if (effect != null)
            {
                UnityEngine.Object.Destroy(effect, EffectLifetime);
            }
        }

        target.TakeDamageWithoutSpellHitEffect(damage);
    }

    private async UniTask CastLevelThreeAsync(float totalDamage)
    {
        CancellationToken token = cancellationTokenSource.Token;

        if (!CanContinueLevelThree())
            return;

        SpawnLevelThreeImpactEffect();
        float strikeDamage = Mathf.Max(0f, totalDamage - LevelThreeTickDamage * LevelThreeTickCount);
        target.TakeDamageWithoutSpellHitEffect(strikeDamage);

        for (int i = 0; i < LevelThreeTickCount; i++)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(LevelThreeTickInterval), cancellationToken: token);

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

        GameObject effect = effectRegistry.SpawnEffect(
            LevelThreeImpactEffectId,
            target.transform.position,
            Quaternion.identity);

        if (effect != null)
        {
            UnityEngine.Object.Destroy(effect, EffectLifetime);
        }
    }

    private void SpawnLevelThreeTickEffect()
    {
        if (effectRegistry == null || playerEffectOrigin == null || target == null)
            return;

        GameObject effect = effectRegistry.SpawnEffect(LevelThreeTickEffectId, playerEffectOrigin.position, Quaternion.identity);

        if (effect == null)
            return;

        LightningProjectile projectile = effect.GetComponent<LightningProjectile>();

        if (projectile == null)
        {
            Debug.LogWarning($"[LightningSpellCaster] {LevelThreeTickEffectId} 프리팹에 LightningProjectile 컴포넌트가 없습니다.");
            UnityEngine.Object.Destroy(effect);
            return;
        }

        projectile.SetTarget(target.transform);
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
