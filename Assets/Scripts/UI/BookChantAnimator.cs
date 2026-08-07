using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class BookChantAnimator : MonoBehaviour
{
    [SerializeField] private ChantManager chantManager;
    [SerializeField] private float riseDistance = 300f;
    [SerializeField] private float duration = 0.3f;

    private RectTransform bookRect;
    private float restingY;
    private Tween moveTween;

    private void Awake()
    {
        bookRect = GetComponent<RectTransform>();
        restingY = bookRect.anchoredPosition.y;
    }

    private void Start()
    {
        if (chantManager == null)
        {
            chantManager = FindAnyObjectByType<ChantManager>();
        }

        if (chantManager == null)
        {
            Debug.LogWarning("[BookChantAnimator] ChantManager를 찾을 수 없습니다.", this);
            return;
        }

        chantManager.OnChantStarted += Raise;
        chantManager.OnChantCancelled += Lower;
        chantManager.OnChantInterrupted += Lower;
        chantManager.OnChantCast += HandleChantCast;

        if (chantManager.IsCasting)
        {
            Raise();
        }
    }

    private void Raise()
    {
        MoveTo(restingY + riseDistance);
    }

    private void Lower()
    {
        MoveTo(restingY);
    }

    private void HandleChantCast(CastResult _)
    {
        Lower();
    }

    private void MoveTo(float targetY)
    {
        moveTween?.Kill();
        moveTween = bookRect
            .DOAnchorPosY(targetY, duration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    private void OnDestroy()
    {
        moveTween?.Kill();

        if (chantManager == null)
        {
            return;
        }

        chantManager.OnChantStarted -= Raise;
        chantManager.OnChantCancelled -= Lower;
        chantManager.OnChantInterrupted -= Lower;
        chantManager.OnChantCast -= HandleChantCast;
    }
}
