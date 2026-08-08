using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading;
using System;
using DG.Tweening;

public sealed class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    [SerializeField] private RectTransform curtain;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        ResetPosition();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            Trigger(() => { }).Forget();
        }
    }

    public async UniTaskVoid Trigger(Action actualTransition)
    {
        var previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        await curtain
            .DOAnchorPosY(0f, 0.4f)
            .SetEase(Ease.InOutCubic)
            .SetUpdate(true)
            .ToUniTask();

        try
        { actualTransition.Invoke(); }
        finally
        {
            await UniTask.DelayFrame(2);

            await curtain
             .DOAnchorPosY(-curtain.rect.height, 0.4f)
             .SetEase(Ease.InOutCubic)
             .SetUpdate(true)
             .ToUniTask();
            Debug.Log("asdf");

            ResetPosition();

            Time.timeScale = previousTimeScale;
        }
    }

    private void ResetPosition()
    {
        float height = curtain.rect.height;
        curtain.anchoredPosition = new Vector2(0f, height);
    }
}
