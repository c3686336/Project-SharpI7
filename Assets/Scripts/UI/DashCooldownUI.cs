using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DashCooldownUI : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    [SerializeField] private Image cooldownFill;

    private void Awake()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerMovement>();
        }

        if (cooldownFill == null)
        {
            Transform fillTransform = transform.Find("BrightFill");

            if (fillTransform != null)
            {
                cooldownFill = fillTransform.GetComponent<Image>();
            }
        }

        if (player == null || cooldownFill == null)
        {
            Debug.LogWarning(
                "[DashCooldownUI] PlayerMovement 또는 BrightFill Image를 찾을 수 없습니다.",
                this
            );
        }
    }

    private void Update()
    {
        if (player == null || cooldownFill == null)
        {
            return;
        }

        cooldownFill.fillAmount = player.DashCooldownProgress;
    }
}
