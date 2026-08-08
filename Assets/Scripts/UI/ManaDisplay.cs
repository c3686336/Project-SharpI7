using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ManaDisplay : MonoBehaviour
{
    [SerializeField] private MonoBehaviour player;
    [SerializeField] private Image manaFill;
    [SerializeField] private Image portraitImage;

    [Header("Portraits")]
    [SerializeField] private Sprite normalPortrait;
    [SerializeField] private Sprite warningPortrait;
    [SerializeField] private Sprite saturatedPortrait;

    private IPlayerMana playerMana;

    private void Awake()
    {
        playerMana = player as IPlayerMana;
        ConfigureManaFill();
    }

    private void OnEnable()
    {
        if (playerMana == null || manaFill == null)
        {
            Debug.LogError("ManaDisplay requires a player and a mana visual.", this);
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
        float saturationThreshold = Mathf.Max(0.01f, status.SaturationThreshold);
        manaFill.fillAmount = Mathf.Clamp01(status.Current / saturationThreshold);

        RefreshPortrait(status);
    }

    private void ConfigureManaFill()
    {
        if (manaFill == null)
        {
            return;
        }

        manaFill.type = Image.Type.Filled;
        manaFill.fillMethod = Image.FillMethod.Vertical;
        manaFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        manaFill.fillClockwise = true;
    }

    private void RefreshPortrait(ManaStatus status)
    {
        if (portraitImage == null)
        {
            return;
        }

        Sprite nextPortrait = status.Current >= status.OverloadThreshold
            ? saturatedPortrait
            : status.Current >= status.WarningThreshold
                ? warningPortrait
                : normalPortrait;

        if (nextPortrait != null && portraitImage.sprite != nextPortrait)
        {
            portraitImage.sprite = nextPortrait;
        }
    }
}
