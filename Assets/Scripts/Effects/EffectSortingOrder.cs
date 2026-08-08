using UnityEngine;

public sealed class EffectSortingOrder : MonoBehaviour
{
    [SerializeField] private int sortingOrder = 10;

    private void Awake()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            spriteRenderer.sortingOrder = sortingOrder;
        }
    }
}