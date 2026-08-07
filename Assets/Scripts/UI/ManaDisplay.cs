using UnityEngine;

[DisallowMultipleComponent]
public sealed class ManaDisplay : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    [SerializeField] private ManaRingGraphic ring;

    private void OnEnable()
    {
        if (player == null || ring == null)
        {
            Debug.LogError("ManaDisplay requires all Inspector references.", this);
            enabled = false;
            return;
        }

        player.ManaStatusChanged += Refresh;
        Refresh(player.ManaStatus);
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.ManaStatusChanged -= Refresh;
        }
    }

    private void Refresh(ManaStatus status)
    {
        ring.SetStatus(status);
    }
}
