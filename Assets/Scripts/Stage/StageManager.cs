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
    }
}