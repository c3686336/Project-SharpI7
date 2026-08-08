using System;
using System.Collections.Generic;
using System.IO;

public enum MagicType
{
    None,
    Fire,
    Lightning
}

[Serializable]
public class ChantSpellDatabaseData
{
    public List<ChantSpellData> spells;
}

[Serializable]
public class ChantSpellData
{
    public string id;
    public string spellName;
    public string fullChantText;
    public float baseDamage;
    public float manaRelease;
    public string magicType;
    public string effectId;
    public List<ChantStageData> stages;

    public MagicType MagicType { get; private set; }

    public void ParseMagicType()
    {
        if (!Enum.TryParse(magicType, false, out MagicType parsedMagicType) ||
            parsedMagicType == MagicType.None ||
            !Enum.IsDefined(typeof(MagicType), parsedMagicType))
        {
            throw new InvalidDataException(
                $"Invalid magic type. Spell: {id}, Type: {magicType}");
        }

        MagicType = parsedMagicType;
    }
}

[Serializable]
public class ChantStageData
{
    public int chantLevel;
    public string chantText;
    public float damageMultiplier;
    public float manaCost;
    public string effectId;
}
