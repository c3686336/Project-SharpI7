using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ManaDisplay : MonoBehaviour
{
    [SerializeField] private MonoBehaviour player;
    [SerializeField] private ManaRingGraphic ring;
    [SerializeField] private TMP_Text saturationCountdownText;
    [SerializeField] private Color saturationColor = new(1f, 0.12f, 0.08f, 1f);

    private IPlayerMana playerMana;

    private void Awake()
    {
        playerMana = player as IPlayerMana;
    }

    private void OnEnable()
    {
        if (playerMana == null || ring == null || saturationCountdownText == null)
        {
            Debug.LogError("ManaDisplay requires all Inspector references.", this);
            enabled = false;
            return;
        }

        playerMana.ManaStatusChanged += Refresh;
        Refresh(playerMana.ManaStatus);
    }

    private void OnDisable()
    {
        if (playerMana != null)
        {
            playerMana.ManaStatusChanged -= Refresh;
        }
    }

    private void Refresh(ManaStatus status)
    {
        ring.SetStatus(status);

        bool showCountdown = status.IsSaturated;
        saturationCountdownText.gameObject.SetActive(showCountdown);
        if (!showCountdown)
        {
            return;
        }

        saturationCountdownText.text = $"OVERLOAD\n{status.SaturationRemaining:0.0}";

        float progress = 1f - Mathf.Clamp01(
            status.SaturationRemaining / Mathf.Max(0.01f, status.SaturationDuration));
        float pulseSpeed = Mathf.Lerp(6f, 14f, progress);
        float pulse = 0.82f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.18f;
        Color color = saturationColor;
        color.a = pulse;
        saturationCountdownText.color = color;
        saturationCountdownText.rectTransform.localScale =
            Vector3.one * Mathf.Lerp(1f, 1.08f, pulse);
    }
}
