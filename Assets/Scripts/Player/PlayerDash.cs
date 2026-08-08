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
    private readonly float boundaryPadding;

    private bool isOnCooldown;
    private float cooldownStartedAt;

    public event Action dash;

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
        var collider = rigidbody2D.GetComponent<Collider2D>();
        boundaryPadding = collider == null
            ? 0f
            : Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y);
    }

    public float CooldownUntil { get; private set; }
    public bool IsDashing { get; private set; }
    public Vector2 DashDirection { get; private set; }
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

        dash?.Invoke();

        // FixedUpdate stops normal locomotion as soon as IsDashing becomes true.
        // Store the input direction first so the windup cannot turn this dash into
        // a zero-distance tween.
        var dashDirection = directionProvider();
        if (dashDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        DashDirection = dashDirection.normalized;
        IsDashing = true;

        await UniTask.Delay(
            TimeSpan.FromSeconds(windupDuration),
            cancellationToken: cancellationToken);

        var destination = ArenaBounds.ClampPosition(
            rigidbody2D.position + DashDirection * distance,
            boundaryPadding);
        await rigidbody2D.DOMove(destination, duration)
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
