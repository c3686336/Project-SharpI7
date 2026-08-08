using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ArenaBounds : MonoBehaviour
{
    private static ArenaBounds active;

    private SpriteRenderer backgroundRenderer;

    private void Awake()
    {
        backgroundRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        active = this;
    }

    private void OnDisable()
    {
        if (active == this)
        {
            active = null;
        }
    }

    public static Vector2 ClampPosition(Vector2 position, float padding = 0f)
    {
        return active == null ? position : active.Clamp(position, padding);
    }

    private Vector2 Clamp(Vector2 position, float padding)
    {
        var bounds = backgroundRenderer.bounds;
        var horizontalPadding = Mathf.Clamp(padding, 0f, bounds.extents.x);
        var verticalPadding = Mathf.Clamp(padding, 0f, bounds.extents.y);

        return new Vector2(
            Mathf.Clamp(position.x, bounds.min.x + horizontalPadding, bounds.max.x - horizontalPadding),
            Mathf.Clamp(position.y, bounds.min.y + verticalPadding, bounds.max.y - verticalPadding));
    }
}
