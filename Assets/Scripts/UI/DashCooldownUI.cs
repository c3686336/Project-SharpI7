using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DashCooldownUI : MonoBehaviour
{
    [SerializeField] private MonoBehaviour player;
    [SerializeField] private Image cooldownFill;

    private IPlayerDash playerDash;

    private void Awake()
    {
        playerDash = player as IPlayerDash;

        if (playerDash == null || cooldownFill == null)
        {
            Debug.LogWarning(
                "[DashCooldownUI] IPlayerDash 또는 Cooldown Fill 참조가 올바르지 않습니다.",
                this
            );
        }
    }

    private void Update()
    {
        if (playerDash == null || cooldownFill == null)
        {
            return;
        }

        cooldownFill.fillAmount = playerDash.DashCooldownProgress;
    }
}
