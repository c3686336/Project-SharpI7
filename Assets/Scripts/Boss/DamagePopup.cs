using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    public sealed class DamagePopup : MonoBehaviour
    {
        [SerializeField] private Vector3 worldOffset = new(1.5f, 0.65f, 0f);
        [SerializeField, Min(0.05f)] private float lifetime = 0.9f;
        [SerializeField, Min(0f)] private float riseDistance = 0.8f;
        [SerializeField] private Color lowDamageColor = new(1f, 0.82f, 0.12f, 1f);
        [SerializeField] private Color mediumDamageColor = new(1f, 0.42f, 0.05f, 1f);
        [SerializeField] private Color highDamageColor = new(1f, 0.1f, 0.08f, 1f);
        [SerializeField, Min(1)] private int fontSize = 58;
        [SerializeField, Min(0.001f)] private float characterSize = 0.35f;
        [SerializeField, Min(1f)] private float damageSizeReference = 100f;
        [SerializeField, Range(0.1f, 1f)] private float minimumDamageSizeMultiplier = 0.4f;
        [SerializeField, Min(1f)] private float maximumDamageSizeMultiplier = 2f;

        private TextMesh damageText;
        private Vector3 startPosition;
        private float elapsedTime;
        private bool initialized;
        private Color displayColor;

        public void Begin(Vector3 bossPosition, float damageAmount)
        {
            startPosition = bossPosition + worldOffset;
            startPosition.z = -0.1f;
            transform.position = startPosition;

            damageText = gameObject.AddComponent<TextMesh>();
            damageText.text = FormatDamage(damageAmount);
            damageText.anchor = TextAnchor.MiddleCenter;
            damageText.alignment = TextAlignment.Center;
            damageText.fontSize = Mathf.Max(1, fontSize);
            damageText.characterSize = Mathf.Max(0.001f, characterSize);
            displayColor = GetDamageColor(damageAmount);
            damageText.color = displayColor;
            transform.localScale = Vector3.one * GetDamageSizeMultiplier(damageAmount);

            var textRenderer = damageText.GetComponent<MeshRenderer>();
            var runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (runtimeFont != null)
            {
                damageText.font = runtimeFont;
                textRenderer.sharedMaterial = runtimeFont.material;
            }

            textRenderer.sortingOrder = 200;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            elapsedTime += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsedTime / lifetime);
            transform.position = startPosition + Vector3.up * (riseDistance * progress);

            var color = displayColor;
            color.a *= 1f - progress;
            damageText.color = color;

            if (elapsedTime >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private Color GetDamageColor(float damageAmount)
        {
            if (damageAmount >= 200f)
            {
                return highDamageColor;
            }

            return damageAmount >= 150f ? mediumDamageColor : lowDamageColor;
        }

        private float GetDamageSizeMultiplier(float damageAmount)
        {
            return Mathf.Clamp(damageAmount / damageSizeReference, minimumDamageSizeMultiplier, maximumDamageSizeMultiplier);
        }

        private static string FormatDamage(float damageAmount)
        {
            var roundedDamage = Mathf.Round(damageAmount);
            return Mathf.Approximately(damageAmount, roundedDamage)
                ? roundedDamage.ToString("0")
                : damageAmount.ToString("0.0");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            lifetime = Mathf.Max(0.05f, lifetime);
            riseDistance = Mathf.Max(0f, riseDistance);
            fontSize = Mathf.Max(1, fontSize);
            characterSize = Mathf.Max(0.001f, characterSize);
            damageSizeReference = Mathf.Max(1f, damageSizeReference);
            minimumDamageSizeMultiplier = Mathf.Clamp(minimumDamageSizeMultiplier, 0.1f, 1f);
            maximumDamageSizeMultiplier = Mathf.Max(1f, maximumDamageSizeMultiplier);
        }
#endif
    }
}
