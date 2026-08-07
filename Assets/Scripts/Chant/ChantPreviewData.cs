public struct ChantPreviewData
{
    public int chantLevel;

    public float expectedDamage;

    public float actualDamage;

    public float manaCost;

    public int correctCount;

    public int typoCount;

    // 현재 영창 단계의 길이를 만족했는가
    public bool canResolve;
}