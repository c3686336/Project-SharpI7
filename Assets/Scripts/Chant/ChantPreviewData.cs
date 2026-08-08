public struct ChantPreviewData
{
    public int chantLevel;

    public float expectedDamage;

    public float actualDamage;

    public float manaCost;

    public float currentMana;

    public int correctCount;

    public int typoCount;

    public bool hasEnoughMana;

    // 현재 입력, 오타, 마나 조건을 모두 만족했는가
    public bool canResolve;
}
