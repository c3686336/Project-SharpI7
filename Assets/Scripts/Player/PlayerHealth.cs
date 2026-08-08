using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

internal sealed class PlayerHealth
{
    public PlayerHealth(
        float maximum,
        float invincibilityTime,
        CancellationToken lifetimeToken)
    {
        Maximum = maximum;
        Current = maximum;

        this.invincibilityTime = invincibilityTime;
        this.lifetimeToken = lifetimeToken;
    }

    private readonly float invincibilityTime;
    private readonly CancellationToken lifetimeToken;
    public float Maximum { get; }
    public float Current { get; private set; }
    public bool IsAlive => Current > 0f;

    public event Action<CancellationToken> InvincibilityStarted;
    private CancellationTokenSource? InvincibilityEnd;

    private bool isInvincible = false;

    public bool TryTakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f || isInvincible)
        {
            return false;
        }

        StartInvincibility(TimeSpan.FromSeconds(invincibilityTime)).Forget();

        Current = Mathf.Max(0f, Current - amount);
        return true;
    }

    private async UniTask StartInvincibility(TimeSpan duration)
    {
        InvincibilityEnd?.Cancel();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        InvincibilityEnd = cts;
        isInvincible = true;

        try
        {
            InvincibilityStarted?.Invoke(cts.Token);
            await UniTask
                .Delay(duration, cancellationToken: cts.Token)
                .SuppressCancellationThrow();
        }
        finally
        {
            if (InvincibilityEnd == cts)
            {
                isInvincible = false;
                InvincibilityEnd = null;

                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
            }

            cts.Dispose();
        }
    }
}
