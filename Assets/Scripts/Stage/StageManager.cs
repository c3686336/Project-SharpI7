using SharpI7.Combat;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class StageManager : MonoBehaviour
{
    [SerializeField] private StageData stageData;
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [FormerlySerializedAs("tutorialDialogueController")]
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private InGameManager inGameManager;

    private static StageData initialStageOverride;
    private static bool showTutorialOnStart;
    private static bool hasInitialStageRequest;

    private PlayerController player;
    private BossHealthBar bossHealthBar;
    private BossHealth currentBoss;
    private GameObject stageExitInstance;
    private StageExitTrigger stageExitTrigger;
    private bool isChangingStage;
    private bool showTutorialOutroOnBossDefeat;

    public StageData CurrentStage => stageData;

    public static void SetInitialStage(StageData initialStage, bool showTutorial)
    {
        initialStageOverride = initialStage;
        showTutorialOnStart = showTutorial;
        hasInitialStageRequest = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInitialStage()
    {
        initialStageOverride = null;
        showTutorialOnStart = false;
        hasInitialStageRequest = false;
    }

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        bossHealthBar = FindAnyObjectByType<BossHealthBar>(FindObjectsInactive.Include);

        StageData initialStage = initialStageOverride != null
            ? initialStageOverride
            : stageData;
        bool shouldShowTutorial = hasInitialStageRequest && showTutorialOnStart;
        initialStageOverride = null;
        showTutorialOnStart = false;
        hasInitialStageRequest = false;

        bool stageLoaded = LoadStage(initialStage);
        bool tutorialEnabled = stageLoaded && shouldShowTutorial;
        showTutorialOutroOnBossDefeat = tutorialEnabled;
        SetTutorialMode(tutorialEnabled);
    }

    private void OnDestroy()
    {
        UnbindBoss();
        DestroyStageExit();
    }

    private void SetTutorialMode(bool enabled)
    {
        if (tutorialManager == null || inGameManager == null)
        {
            Debug.LogError(
                "[StageManager] TutorialManager or InGameManager reference is missing.",
                this);
            return;
        }

        tutorialManager.gameObject.SetActive(enabled);

        if (enabled)
        {
            inGameManager.PauseGameplay();
            tutorialManager.Begin(inGameManager);
        }
        else
        {
            inGameManager.ResumeGameplay();
        }
    }

    private void ApplyStageData()
    {
        if (stageData == null || backgroundRenderer == null)
        {
            Debug.LogError("[StageManager] StageData 또는 BackgroundRenderer가 없습니다.", this);
            return;
        }

        if (stageData.BackgroundSprite != null)
        {
            backgroundRenderer.sprite = stageData.BackgroundSprite;
        }

        backgroundRenderer.transform.position = new Vector3(0f, 1.5f, 0f);
        backgroundRenderer.sortingOrder = -100;
    }

    private void BindBoss(BossHealth boss)
    {
        UnbindBoss();
        currentBoss = boss;

        if (currentBoss == null)
        {
            Debug.LogError("[StageManager] 현재 스테이지의 BossHealth를 찾을 수 없습니다.", this);
            return;
        }

        currentBoss.Died += HandleBossDied;
        player?.SetCombatTarget(currentBoss);
        bossHealthBar?.BindBoss(currentBoss);
    }

    private bool LoadStage(StageData newStage)
    {
        if (newStage == null)
        {
            Debug.LogError("[StageManager] StageData가 없습니다.", this);
            return false;
        }

        if (newStage.BossPrefab == null)
        {
            Debug.LogError($"[StageManager] {newStage.StageId}의 BossPrefab이 없습니다.", newStage);
            return false;
        }

        GameObject bossObject = Instantiate(newStage.BossPrefab);
        // Keep the runtime instance at the exact size authored on the prefab.
        // This prevents initialization code from leaving it at a different scale
        // from the same prefab placed directly in a scene.
        Vector3 prefabScale = newStage.BossPrefab.transform.localScale;
        bossObject.transform.localScale = prefabScale;
        bossObject.GetComponent<BossMovementAnimator>()?.SetBaseScale(prefabScale);
        BossHealth newBoss = bossObject.GetComponent<BossHealth>();
        if (newBoss == null)
        {
            Debug.LogError($"[StageManager] {bossObject.name}에 BossHealth가 없습니다.", bossObject);
            Destroy(bossObject);
            return false;
        }

        DestroyStageExit();
        stageData = newStage;
        ApplyStageData();
        bossHealthBar?.SetStagePortrait(stageData.StageId);
        BindBoss(newBoss);
        CreateStageExit();
        return true;
    }

    private void UnbindBoss()
    {
        if (currentBoss != null)
        {
            currentBoss.Died -= HandleBossDied;
        }

        currentBoss = null;
    }

    private void CreateStageExit()
    {
        DestroyStageExit();

        if (stageData == null || stageData.StageExitPrefab == null)
        {
            return;
        }

        stageExitInstance = Instantiate(stageData.StageExitPrefab);
        stageExitTrigger = stageExitInstance.GetComponentInChildren<StageExitTrigger>(true);

        if (stageExitTrigger == null)
        {
            Debug.LogError(
                $"[StageManager] {stageData.StageExitPrefab.name}에 StageExitTrigger가 없습니다.",
                stageExitInstance);
            Destroy(stageExitInstance);
            stageExitInstance = null;
            return;
        }

        stageExitTrigger.PlayerEntered += HandleStageExitEntered;
        stageExitInstance.SetActive(false);
    }

    private void DestroyStageExit()
    {
        if (stageExitTrigger != null)
        {
            stageExitTrigger.PlayerEntered -= HandleStageExitEntered;
            stageExitTrigger = null;
        }

        if (stageExitInstance != null)
        {
            Destroy(stageExitInstance);
            stageExitInstance = null;
        }
    }

    private void HandleBossDied()
    {
        if (stageExitInstance != null)
        {
            stageExitInstance.SetActive(true);
        }

        if (showTutorialOutroOnBossDefeat)
        {
            showTutorialOutroOnBossDefeat = false;
            ShowTutorialOutro();
        }
    }

    private void ShowTutorialOutro()
    {
        if (tutorialManager == null || inGameManager == null)
        {
            Debug.LogError(
                "[StageManager] TutorialManager or InGameManager reference is missing.",
                this);
            return;
        }

        inGameManager.PauseGameplay();
        tutorialManager.gameObject.SetActive(true);
        tutorialManager.BeginPostBoss(inGameManager);
    }

    private void HandleStageExitEntered()
    {
        if (isChangingStage || stageData == null)
        {
            return;
        }

        StageData nextStage = stageData.NextStage;
        if (nextStage == null)
        {
            Debug.Log("[StageManager] 마지막 스테이지입니다.", this);
            OutGameManager.LoadWin();
            
            return;
        }

        isChangingStage = true;
        LoadStage(nextStage);
        isChangingStage = false;
    }
}
