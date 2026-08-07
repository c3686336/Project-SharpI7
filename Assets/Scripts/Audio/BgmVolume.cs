using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class BgmVolume : MonoBehaviour
{
    private const string PlayerPrefsKey = "BgmVolume";
    private const float DefaultVolume = 0.3f;

    private static event Action<float> VolumeChanged;

    private AudioSource audioSource;

    public static float Current => PlayerPrefs.GetFloat(PlayerPrefsKey, DefaultVolume);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        VolumeChanged = null;
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        ApplyVolume(Current);
        VolumeChanged += ApplyVolume;
    }

    private void OnDestroy()
    {
        VolumeChanged -= ApplyVolume;
    }

    public static void SetVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PlayerPrefsKey, clampedVolume);
        VolumeChanged?.Invoke(clampedVolume);
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    private void ApplyVolume(float volume)
    {
        audioSource.volume = volume;
    }
}
