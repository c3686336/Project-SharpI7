using SharpI7.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BossHealthBar : MonoBehaviour
{
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthText;

    [Header("Phase UI")]
    [SerializeField] private Image phaseOneImage;
    [SerializeField] private Image phaseTwoImage;

    private void Start()
    {
        EnsureReferences();

        BossHealth initialBoss = bossHealth != null
            ? bossHealth
            : FindAnyObjectByType<BossHealth>();

        if (initialBoss != null)
        {
            BindBoss(initialBoss);
        }
    }

    public void BindBoss(BossHealth newBossHealth)
    {
        EnsureReferences();

        if (bossHealth != null)
        {
            bossHealth.HealthChanged -= UpdateHealth;
            bossHealth.PhaseTwoStarted -= HandlePhaseTwoStarted;
            bossHealth.Died -= Hide;
        }

        bossHealth = newBossHealth;

        if (bossHealth == null)
        {
            Debug.LogWarning("[BossHealthBar] BossHealth를 찾을 수 없습니다.", this);
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        bossHealth.HealthChanged += UpdateHealth;
        bossHealth.PhaseTwoStarted += HandlePhaseTwoStarted;
        bossHealth.Died += Hide;

        UpdateHealth(bossHealth.CurrentHealth, bossHealth.MaxHealth);
        UpdatePhaseUI();
    }

    private void EnsureReferences()
    {
        if (healthSlider == null)
        {
            healthSlider = GetComponent<Slider>();
        }

        if (healthText == null)
        {
            healthText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void UpdateHealth(float current, float max)
    {
        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }

        if (healthText != null)
        {
            float healthPercent = max > 0f ? (current / max) * 100f : 0f;
            healthText.text = $"{healthPercent:0.0}%";
        }
    }

    private void HandlePhaseTwoStarted()
    {
        UpdatePhaseUI();
    }

    private void UpdatePhaseUI()
    {
        bool isPhaseTwo = bossHealth != null && bossHealth.IsPhaseTwo;

        if (phaseOneImage != null)
        {
            phaseOneImage.color = Color.red;
        }

        if (phaseTwoImage != null)
        {
            phaseTwoImage.color = isPhaseTwo ? Color.black : Color.red;
        }
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (bossHealth != null)
        {
            bossHealth.HealthChanged -= UpdateHealth;
            bossHealth.PhaseTwoStarted -= HandlePhaseTwoStarted;
            bossHealth.Died -= Hide;
        }
    }
}
