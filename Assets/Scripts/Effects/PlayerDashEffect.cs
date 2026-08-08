using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SharpI7.Visuals
{
    [DisallowMultipleComponent]
    public sealed class PlayerDashEffect : MonoBehaviour
    {
        private const string FrameResourcesPath = "DashEffects";
        private const float DefaultPixelsPerUnit = 160f;
        private const int FrameCount = 6;

        [SerializeField, Min(0.01f)] private float frameDuration = 0.045f;
        [SerializeField] private int sortingOrder = 200;

        private static Sprite[] cachedFrames;
        private SpriteRenderer effectRenderer;
        private Sprite[] frames;
        private Coroutine playRoutine;
        private Transform followTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCachedFrames()
        {
            // With Fast Enter Play Mode Unity can retain static fields while
            // destroying runtime-created Sprite objects between play sessions.
            cachedFrames = null;
        }

        public static void Prewarm()
        {
            CreateCachedFrames();
        }

        public void Play(Vector2 dashDirection)
        {
            CreateFramesIfNeeded();
            if (frames == null || frames.Length == 0)
            {
                Destroy(gameObject);
                return;
            }

            transform.right = -dashDirection.normalized;
            effectRenderer.sortingOrder = sortingOrder;
            playRoutine = StartCoroutine(PlayFrames());
        }

        public void Follow(Transform target)
        {
            followTarget = target;
            if (followTarget != null)
            {
                transform.position = followTarget.position;
            }
        }

        private void LateUpdate()
        {
            // The player itself rotates toward the boss. Follow only position,
            // not rotation, so the dash trail retains its launch direction.
            if (followTarget != null)
            {
                transform.position = followTarget.position;
            }
        }

        private IEnumerator PlayFrames()
        {
            foreach (var frame in frames)
            {
                effectRenderer.sprite = frame;
                yield return new WaitForSeconds(frameDuration);
            }

            Destroy(gameObject);
        }

        private void CreateFramesIfNeeded()
        {
            if (effectRenderer == null)
            {
                effectRenderer = GetComponent<SpriteRenderer>();
                if (effectRenderer == null)
                {
                    effectRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            if (frames == null)
            {
                CreateCachedFrames();
                frames = cachedFrames;
            }
        }

        private static void CreateCachedFrames()
        {
            if (cachedFrames != null && cachedFrames.Length > 0 && cachedFrames[0] != null)
            {
                return;
            }

            cachedFrames = null;
            var loadedFrames = new List<Sprite>(FrameCount);
            for (var frameNumber = 1; frameNumber <= FrameCount; frameNumber++)
            {
                var texture = Resources.Load<Texture2D>(
                    $"{FrameResourcesPath}/dash_{frameNumber:00}");
                if (texture == null)
                {
                    Debug.LogWarning($"Missing dash effect frame: dash_{frameNumber:00}");
                    continue;
                }

                loadedFrames.Add(Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.05f, 0.5f),
                    DefaultPixelsPerUnit));
            }

            cachedFrames = loadedFrames.ToArray();
        }

        private void OnDestroy()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
            }

        }
    }
}
