using SharpI7.Combat;
using UnityEngine;

namespace SharpI7.Debugging
{
    [AddComponentMenu("Project Sharp/Debug/Combat Health HUD")]
    [DisallowMultipleComponent]
    public sealed class CombatDebugHud : MonoBehaviour
    {
        [SerializeField] private PlayerMovement player;
        [SerializeField] private BossHealth bossHealth;
        [SerializeField] private Vector2 screenPosition = new(16f, 16f);
        [SerializeField, Min(160f)] private float width = 240f;
        [SerializeField, Min(12)] private int fontSize = 18;

        private GUIStyle labelStyle;

        private void OnGUI()
        {
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold
            };

            var area = new Rect(screenPosition.x, screenPosition.y, width, 72f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(FormatHealth("Player", player?.CurrentHealth, player?.MaxHealth), labelStyle);
            GUILayout.Label(FormatHealth("Boss", bossHealth?.CurrentHealth, bossHealth?.MaxHealth), labelStyle);
            GUILayout.EndArea();
        }

        private static string FormatHealth(string label, float? current, float? maximum)
        {
            return current.HasValue && maximum.HasValue
                ? $"{label} HP: {current:0.#} / {maximum:0.#}"
                : $"{label} HP: not assigned";
        }
    }
}
