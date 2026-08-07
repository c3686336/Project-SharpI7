public struct CastResult
{
    public string spellId;
    public string spellName;

    public string targetText;
    public string typedText;

    public int correctCount;
    public int typoCount;

    public int castLevel;

    public float expectedDamage;
    public float actualDamage;

    public float penaltyMultiplier;

    // 해당 영창 단계에서 실제 소비할 마나
    public float manaCost;

    // 기존 데이터. manaCost와 다른 값
    public float manaRelease;

    public string magicType;
    public string effectId;

    public bool canCast;

    public bool completed;
}