using UnityEngine;

namespace SharpI7.Combat
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossVisual : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float visualSize = 2.4f;
        [SerializeField, Range(32, 256)] private int textureResolution = 128;
        [SerializeField] private Color bodyColor = new(0.32f, 0.035f, 0.08f, 1f);
        [SerializeField] private Color rimColor = new(0.9f, 0.22f, 0.08f, 1f);
        [SerializeField] private Color eyeColor = new(1f, 0.8f, 0.2f, 1f);

        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private SpriteRenderer bossRenderer;

        public float VisualSize => visualSize;

        private void OnEnable()
        {
            CreateVisual();
        }

        private void CreateVisual()
        {
            DestroyVisual();
            var resolution = Mathf.Max(32, textureResolution);
            runtimeTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Runtime Boss Visual",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[resolution * resolution];
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var normalizedX = (x + 0.5f) / resolution * 2f - 1f;
                    var normalizedY = (y + 0.5f) / resolution * 2f - 1f;
                    var radiusSquared = normalizedX * normalizedX + normalizedY * normalizedY;
                    var color = Color.clear;

                    if (radiusSquared <= 0.92f * 0.92f)
                    {
                        color = radiusSquared >= 0.76f * 0.76f ? rimColor : bodyColor;

                        var leftEye = IsInsideEllipse(normalizedX, normalizedY, -0.3f, 0.16f, 0.14f, 0.1f);
                        var rightEye = IsInsideEllipse(normalizedX, normalizedY, 0.3f, 0.16f, 0.14f, 0.1f);
                        if (leftEye || rightEye)
                        {
                            color = eyeColor;
                        }
                    }

                    pixels[y * resolution + x] = color;
                }
            }

            runtimeTexture.SetPixels32(pixels);
            runtimeTexture.Apply();
            runtimeSprite = Sprite.Create(
                runtimeTexture,
                new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution / Mathf.Max(0.1f, visualSize));
            runtimeSprite.name = "Runtime Boss Visual";

            bossRenderer = GetComponent<SpriteRenderer>();
            bossRenderer.sprite = runtimeSprite;
            bossRenderer.sortingOrder = 100;
        }

        private static bool IsInsideEllipse(
            float x,
            float y,
            float centerX,
            float centerY,
            float radiusX,
            float radiusY)
        {
            var normalizedX = (x - centerX) / radiusX;
            var normalizedY = (y - centerY) / radiusY;
            return normalizedX * normalizedX + normalizedY * normalizedY <= 1f;
        }

        private void OnDisable()
        {
            DestroyVisual();
        }

        private void OnDestroy()
        {
            DestroyVisual();
        }

        private void DestroyVisual()
        {
            if (bossRenderer != null)
            {
                bossRenderer.sprite = null;
            }

            if (runtimeSprite != null)
            {
                DestroyRuntimeObject(runtimeSprite);
                runtimeSprite = null;
            }

            if (runtimeTexture != null)
            {
                DestroyRuntimeObject(runtimeTexture);
                runtimeTexture = null;
            }
        }

        private static void DestroyRuntimeObject(Object runtimeObject)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeObject);
            }
            else
            {
                DestroyImmediate(runtimeObject);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            visualSize = Mathf.Max(0.1f, visualSize);
            textureResolution = Mathf.Clamp(textureResolution, 32, 256);
        }
#endif
    }
}
