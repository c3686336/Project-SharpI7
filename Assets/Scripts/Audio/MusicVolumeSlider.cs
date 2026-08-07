using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public sealed class MusicVolumeSlider : MonoBehaviour
{
    private Slider volumeSlider;

    private void Awake()
    {
        volumeSlider = GetComponent<Slider>();
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.wholeNumbers = false;
    }

    private void OnEnable()
    {
        volumeSlider.SetValueWithoutNotify(BgmVolume.Current);
        volumeSlider.onValueChanged.AddListener(BgmVolume.SetVolume);
    }

    private void OnDisable()
    {
        volumeSlider.onValueChanged.RemoveListener(BgmVolume.SetVolume);
        BgmVolume.Save();
    }
}
