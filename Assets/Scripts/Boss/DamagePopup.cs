using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    public sealed class DamagePopup : MonoBehaviour
    {
        [SerializeField] private Vector3 worldOffset = new(1.5f, 0.5f, 0f);
        [SerializeField, Min(0.05f)] private float lifetime = 0.9f;
        [SerializeField, Min(0f)] private float riseDistance = 0.8f;
        [SerializeField] private Color textColor = new(1f, 0.82f, 0.12f, 1f);
        [SerializeField, Min(1)] private int fontSize = 64;
        [SerializeField, Min(0.001f)] private float characterSize = 0.08f;

        private TextMesh damageText;
        private Vector3 startPosition;
        private float elapsedTime;
        private bool initialized;

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
            damageText.color = textColor;

            var textRenderer = damageText.GetComponent<MeshRenderer>();
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

            var color = textColor;
            color.a *= 1f - progress;
            damageText.color = color;

            if (elapsedTime >= lifetime)
            {
                Destroy(gameObject);
            }
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
        }
#endif
    }
}
