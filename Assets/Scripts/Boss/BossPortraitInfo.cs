using UnityEngine;

public sealed class BossPortraitInfo : MonoBehaviour
{
    [SerializeField] private Sprite phaseOnePortrait;
    [SerializeField] private Sprite phaseTwoPortrait;

    public Sprite PhaseOnePortrait => phaseOnePortrait;
    public Sprite PhaseTwoPortrait => phaseTwoPortrait;
}