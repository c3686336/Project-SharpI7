using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

[RequireComponent(typeof(Slider))]
public sealed class MusicVolumeSlider : MonoBehaviour
{
    private Slider volumeSlider;

    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string volumeParamName;

    private void Awake()
    {
        volumeSlider = GetComponent<Slider>();
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.wholeNumbers = false;
    }

    private void OnEnable()
    {
        volumeSlider.SetValueWithoutNotify(GetMixerVolume());
        volumeSlider.onValueChanged.AddListener(SetMixerVolume);
    }

    private void OnDisable()
    {
        volumeSlider.onValueChanged.RemoveListener(BgmVolume.SetVolume);
        BgmVolume.Save();
    }

    private void SetMixerVolume(float volume)
    {
        mixer.SetFloat(
            volumeParamName,
            Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f
        );
    }

    private float GetMixerVolume()
    {
        mixer.GetFloat(volumeParamName, out var mixerdB);

        return Mathf.Clamp01(Mathf.Pow(10f, mixerdB / 20f));
    }
}
