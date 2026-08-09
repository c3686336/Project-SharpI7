using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a short, non-blocking keyboard posture guide after the first tutorial.
/// </summary>
public sealed class KeyboardHandsGuide : MonoBehaviour
{
    private const float HoldDuration = 3f;
    private const float FadeDuration = 0.8f;

    private static bool hasShownThisSession;

    public static void ShowOnce()
    {
        if (hasShownThisSession)
        {
            return;
        }

        hasShownThisSession = true;
        var guideObject = new GameObject("Keyboard Hands Guide");
        guideObject.AddComponent<KeyboardHandsGuide>();
    }

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>().enabled = false;

        CanvasGroup canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.82f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        CreateDimmer();
        CreateGuideContent();
        StartCoroutine(Play(canvasGroup));
    }

    private void CreateDimmer()
    {
        var dimmer = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
        dimmer.transform.SetParent(transform, false);

        RectTransform rect = dimmer.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        dimmer.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.28f);
    }

    private void CreateGuideContent()
    {
        var content = new GameObject("Guide Content", typeof(RectTransform));
        content.transform.SetParent(transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(760f, 800f);

        CreateGuideImage(content.transform);
        CreateMessage(content.transform);
    }

    private static void CreateMessage(Transform parent)
    {
        var textObject = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -20f);
        rect.sizeDelta = new Vector2(0f, 120f);

        TextMeshProUGUI message = textObject.GetComponent<TextMeshProUGUI>();
        message.font = TMP_Settings.defaultFontAsset;
        message.text = "\uBCF8 \uAC8C\uC784\uC740 \uC591\uC190\uC744 \uD0A4\uBCF4\uB4DC\uC5D0 \uC62C\uB824\uB450\uACE0\n\uD50C\uB808\uC774\uD558\uB294 \uAC83\uC744 \uAD8C\uC7A5\uD569\uB2C8\uB2E4";
        message.fontSize = 42f;
        message.fontStyle = FontStyles.Bold;
        message.alignment = TextAlignmentOptions.Center;
        message.color = Color.white;
        message.outlineWidth = 0.18f;
        message.outlineColor = new Color(0f, 0f, 0f, 0.9f);
    }

    private static void CreateGuideImage(Transform parent)
    {
        KeyboardHandsGuideData guideData =
            Resources.Load<KeyboardHandsGuideData>("Tutorial/KeyboardHandsGuideData");
        Sprite guideSprite = guideData != null ? guideData.GuideSprite : null;
        if (guideSprite == null)
        {
            Debug.LogError("[KeyboardHandsGuide] Sprites/UI/keyboard guide sprite is missing.");
            return;
        }

        var imageObject = new GameObject("Keyboard Hands Image", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        // Four times the original 620 x 620 display size, offset so the keyboard stays centered.
        rect.anchoredPosition = new Vector2(0f, -680f);
        rect.sizeDelta = new Vector2(2480f, 2480f);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = guideSprite;
        image.preserveAspect = true;
        image.color = new Color(1f, 1f, 1f, 0.92f);
    }

    private IEnumerator Play(CanvasGroup canvasGroup)
    {
        yield return new WaitForSecondsRealtime(HoldDuration);

        float elapsed = 0f;
        const float initialAlpha = 0.82f;
        while (elapsed < FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(initialAlpha, 0f, elapsed / FadeDuration);
            yield return null;
        }

        Destroy(gameObject);
    }
}