using UnityEngine;

public sealed class StageManager : MonoBehaviour
{
    [SerializeField] private StageData stageData;
    [SerializeField] private SpriteRenderer backgroundRenderer;

    private void Start()
    {
        ApplyStageData();
    }

    private void ApplyStageData()
    {
        if (stageData == null || backgroundRenderer == null)
        {
            return;
        }

        backgroundRenderer.sprite = stageData.BackgroundSprite;
		backgroundRenderer.transform.position = new Vector3(0f, 1.5f, 0f);
		backgroundRenderer.sortingOrder = -100;
    }
}