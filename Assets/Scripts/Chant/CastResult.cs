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
    public float manaRelease;

    public string magicType;
    public string effectId;

    // 길이까지 입력하여 발동 가능한 상태
    public bool canCast;

    // 오타까지 하나도 없는 완벽한 영창
    public bool completed;
}
