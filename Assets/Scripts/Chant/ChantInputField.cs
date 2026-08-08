using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[AddComponentMenu("Project Sharp/Chant Input Field")]
public sealed class ChantInputField : TMP_InputField
{
    public bool HasActiveImeComposition =>
        !string.IsNullOrEmpty(GetImeComposition());

    protected override void Append(string input)
    {
        // TMP uses this overload for clipboard paste; normal typing uses Append(char).
    }

    public string GetActualText(string composition)
    {
        if (string.IsNullOrEmpty(composition))
            return text;

        return text.Insert(m_StringPosition, composition);
    }

    public void CommitImeComposition()
    {
        string composition = GetImeComposition();
        if (string.IsNullOrEmpty(composition))
            return;

        // TMP also uses Append(string) to commit an active IME composition.
        // Call the base implementation explicitly so clipboard paste remains blocked.
        base.Append(composition);
    }

    public void ClearVisibleText()
    {
        SetTextWithoutNotify(string.Empty);

        if (textComponent != null)
        {
            textComponent.text = string.Empty;
        }
    }

    private static string GetImeComposition()
    {
        BaseInputModule inputModule = EventSystem.current?.currentInputModule;
        return inputModule != null
            ? inputModule.input.compositionString
            : Input.compositionString;
    }
}
