using TMPro;
using UnityEngine;

[AddComponentMenu("Project Sharp/Chant Input Field")]
public sealed class ChantInputField : TMP_InputField
{
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
}
