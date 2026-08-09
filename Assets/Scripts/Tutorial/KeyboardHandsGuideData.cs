using UnityEngine;

[CreateAssetMenu(menuName = "Project Sharp/Keyboard Hands Guide Data")]
public sealed class KeyboardHandsGuideData : ScriptableObject
{
    [SerializeField] private Sprite guideSprite;

    public Sprite GuideSprite => guideSprite;
}