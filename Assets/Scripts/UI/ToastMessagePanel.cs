using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToastMessagePanel : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text messageText;

    [SerializeField] private float riseDistance = 60f;
    [SerializeField] private float displayDuration = 1f;
    [SerializeField] private float fadeDuration = 0.5f;

    private Sequence sequence;

    public void Play(string message)
    {
        messageText.text = message;

        backgroundImage.raycastTarget = false;
        messageText.raycastTarget = false;

        Vector2 startPosition = panel.anchoredPosition;
        float totalDuration = displayDuration + fadeDuration;

        sequence = DOTween.Sequence();

        sequence.Join(
            panel.DOAnchorPosY(
                    startPosition.y + riseDistance,
                    totalDuration)
                .SetEase(Ease.OutCubic));

        sequence.Insert(
            displayDuration,
            backgroundImage.DOFade(0f, fadeDuration));
        sequence.Insert(
            displayDuration,
            messageText.DOFade(0f, fadeDuration));

        sequence.SetUpdate(true);

        sequence.OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }

    private void OnDestroy()
    {
        sequence?.Kill();
    }
}