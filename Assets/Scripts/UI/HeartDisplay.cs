using UnityEngine;

public class HeartDisplay : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    [SerializeField] private RectTransform heartPrefab;
    [SerializeField] private float heartSize = 80f;
    [SerializeField] private float spacing = 100f;

    private void Start()
    {
        if (player == null)
        {
            Debug.LogWarning("[HeartDisplay] PlayerMovement를 찾을 수 없습니다.", this);
            return;
        }

        Rebuild(Mathf.RoundToInt(player.MaxHealth));
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
}
