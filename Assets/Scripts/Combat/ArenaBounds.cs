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

    public static bool TryGetWallInteriorBounds(out Bounds bounds)
    {
        var leftWall = GameObject.Find("LeftWall")?.GetComponent<Collider2D>();
        var rightWall = GameObject.Find("RightWall")?.GetComponent<Collider2D>();
        var upperWall = GameObject.Find("UpperWall")?.GetComponent<Collider2D>();
        var lowerWall = GameObject.Find("LowerWall")?.GetComponent<Collider2D>();

        if (leftWall == null || rightWall == null || upperWall == null || lowerWall == null)
        {
            bounds = default;
            return false;
        }

        var left = leftWall.bounds.max.x;
        var right = rightWall.bounds.min.x;
        var top = upperWall.bounds.min.y;
        var bottom = lowerWall.bounds.max.y;
        if (left >= right || bottom >= top)
        {
            bounds = default;
            return false;
        }

        bounds = new Bounds();
        bounds.SetMinMax(new Vector3(left, bottom, 0f), new Vector3(right, top, 0f));
        return true;
    }
    public static Vector2 ClampPosition(Vector2 position, float padding = 0f)
    {
        return active == null ? position : active.Clamp(position, padding);
    }

    public static bool TryGetWorldBounds(out Bounds bounds)
    {
        if (active == null || active.backgroundRenderer == null)
        {
            bounds = default;
            return false;
        }

        bounds = active.backgroundRenderer.bounds;
        return true;
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
