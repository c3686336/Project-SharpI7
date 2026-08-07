using UnityEngine;
using UnityEngine.UI;

public sealed class HeartDisplay : MonoBehaviour
{
    [SerializeField] private MonoBehaviour player;
    [SerializeField] private RectTransform heartPrefab;
    [SerializeField] private float heartSize = 80f;
    [SerializeField] private float spacing = 100f;

    private Color fullHeartColor = Color.white;
    private IPlayerHealth playerHealth;

    private void Awake()
    {
        playerHealth = player as IPlayerHealth;

        if (heartPrefab != null && heartPrefab.TryGetComponent(out Image heartImage))
        {
            fullHeartColor = heartImage.color;
        }
    }

    private void Start()
    {
        if (playerHealth == null || heartPrefab == null)
        {
            Debug.LogWarning(
                "[HeartDisplay] IPlayerHealth 또는 Heart Prefab 참조가 올바르지 않습니다.",
                this
            );
            return;
        }

        Rebuild(Mathf.RoundToInt(playerHealth.MaxHealth));
        UpdateHeartColors(Mathf.RoundToInt(playerHealth.CurrentHealth));
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged += UpdateHealth;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= UpdateHealth;
        }
    }

    private void UpdateHealth(float current, float max)
    {
        int maxHeartCount = Mathf.Max(0, Mathf.RoundToInt(max));

        if (transform.childCount != maxHeartCount)
        {
            Rebuild(maxHeartCount);
        }

        UpdateHeartColors(Mathf.RoundToInt(current));
    }

    public void Rebuild(int maxHealth)
    {
        int heartCount = Mathf.Max(0, maxHealth);
        int existingCount = transform.childCount;

        for (int i = existingCount - 1; i >= heartCount; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        for (int i = existingCount; i < heartCount; i++)
        {
            Instantiate(heartPrefab, transform, false);
        }

        float startX = -(heartCount - 1) * spacing * 0.5f;

        for (int i = 0; i < heartCount; i++)
        {
            RectTransform heart = (RectTransform)transform.GetChild(i);
            heart.name = $"Heart{i + 1}";
            heart.sizeDelta = new Vector2(heartSize, heartSize);
            heart.anchoredPosition = new Vector2(startX + i * spacing, 0f);
        }
    }

    private void UpdateHeartColors(int currentHealth)
    {
        int fullHeartCount = Mathf.Clamp(currentHealth, 0, transform.childCount);

        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).TryGetComponent(out Image heartImage))
            {
                heartImage.color = i < fullHeartCount
                    ? fullHeartColor
                    : Color.black;
            }
        }
    }
}
