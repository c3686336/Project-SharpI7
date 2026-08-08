using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

internal sealed class PlayerHealth
{
    public PlayerHealth(float maximum, float invincibilityTime)
    {
        Maximum = maximum;
        Current = maximum;

        this.invincibilityTime = invincibilityTime;
    }

    private readonly float invincibilityTime;
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

    private async UniTaskVoid StartInvincibility(TimeSpan duration)
    {
        InvincibilityEnd?.Cancel();
        InvincibilityEnd?.Dispose();

        var cts = new CancellationTokenSource();
        InvincibilityEnd = cts;

        InvincibilityStarted?.Invoke(cts.Token);

        try
        {
            isInvincible = true;

            await UniTask.Delay(duration, cancellationToken: cts.Token);
        }
        finally
        {
            if (InvincibilityEnd == cts)
            {
                isInvincible = false;

                InvincibilityEnd.Dispose();
                InvincibilityEnd = null;
            }
        }
    }
}
