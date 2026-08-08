using UnityEngine;

[CreateAssetMenu(
    fileName = "Stage_",
    menuName = "Project Sharp/Stage Data")]
public sealed class StageData : ScriptableObject
{
    [Header("기본 정보")]
    [SerializeField] private string stageId = "stage";
    [SerializeField] private bool isTutorial = false;

    [Header("전투 구성")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private GameObject bossPrefab;

    [Header("다음 스테이지")]
    [SerializeField] private StageData nextStage;

    public string StageId => stageId;
    public bool IsTutorial => isTutorial;
	public Sprite BackgroundSprite => backgroundSprite;
    public GameObject BossPrefab => bossPrefab;
    public StageData NextStage => nextStage;
}