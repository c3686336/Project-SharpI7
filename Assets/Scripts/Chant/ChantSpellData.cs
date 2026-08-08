using System;
using System.Collections.Generic;

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
}

[Serializable]
public class ChantStageData
{
    public int chantLevel;

    public string chantText;

    public float damageMultiplier;

    // 해당 영창 단계 발동 시 필요한 마나
    public float manaCost;
}