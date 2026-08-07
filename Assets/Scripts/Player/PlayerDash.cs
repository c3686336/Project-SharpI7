using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

internal sealed class PlayerDash
{
    private readonly Rigidbody2D rigidbody2D;
    private readonly Func<Vector2> directionProvider;
    private readonly float windupDuration;
    private readonly float cooldownDuration;
    private readonly float duration;
    private readonly float distance;
    private readonly CancellationToken cancellationToken;

    private bool isOnCooldown;
    private float cooldownStartedAt;

    public PlayerDash(
        Rigidbody2D rigidbody2D,
        Func<Vector2> directionProvider,
        float windupDuration,
        float cooldownDuration,
        float duration,
        float distance,
        CancellationToken cancellationToken)
    {
        this.rigidbody2D = rigidbody2D;
        this.directionProvider = directionProvider;
        this.windupDuration = windupDuration;
        this.cooldownDuration = cooldownDuration;
        this.duration = duration;
        this.distance = distance;
        this.cancellationToken = cancellationToken;
    }

    public float CooldownUntil { get; private set; }
    public bool IsDashing { get; private set; }
    public float CooldownProgress
    {
        get
        {
            if (IsDashing)
            {
                return 0f;
            }

            if (!isOnCooldown)
            {
                return 1f;
            }

            return Mathf.InverseLerp(cooldownStartedAt, CooldownUntil, Time.time);
        }
    }

    public bool CanStart => !isOnCooldown && !IsDashing;

    public async UniTask ExecuteAsync()
    {
        if (!CanStart)
        {
            return;
        }

        IsDashing = true;

        await UniTask.Delay(
            TimeSpan.FromSeconds(windupDuration),
            cancellationToken: cancellationToken);

        await rigidbody2D.DOMove(directionProvider() * distance, duration)
            .SetRelative()
            .SetEase(Ease.InOutQuad)
            .ToUniTask(cancellationToken: cancellationToken);
        IsDashing = false;

        isOnCooldown = true;
        cooldownStartedAt = Time.time;
        CooldownUntil = cooldownStartedAt + cooldownDuration;
        await UniTask.Delay(
            TimeSpan.FromSeconds(cooldownDuration),
            cancellationToken: cancellationToken);
        isOnCooldown = false;
    }

    public void Stop()
    {
        IsDashing = false;
        DOTween.Kill(rigidbody2D);
    }
}
