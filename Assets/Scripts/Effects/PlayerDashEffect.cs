using System;
using System.Collections;
using UnityEngine;

namespace SharpI7.Visuals
{
    [DisallowMultipleComponent]
    public sealed class PlayerDashEffect : MonoBehaviour
    {
        private const string FrameResourcesPath = "DashEffects";
        private const float DefaultPixelsPerUnit = 160f;

        [SerializeField, Min(0.01f)] private float frameDuration = 0.045f;
        [SerializeField] private int sortingOrder = 200;

        private static Sprite[] cachedFrames;
        private SpriteRenderer effectRenderer;
        private Sprite[] frames;
        private Coroutine playRoutine;
        private Transform followTarget;

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
            if (cachedFrames != null)
            {
                return;
            }

            var textures = Resources.LoadAll<Texture2D>(FrameResourcesPath);
            Array.Sort(textures, (left, right) => string.CompareOrdinal(left.name, right.name));
            cachedFrames = new Sprite[textures.Length];
            for (var index = 0; index < textures.Length; index++)
            {
                var texture = textures[index];
                cachedFrames[index] = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.05f, 0.5f),
                    DefaultPixelsPerUnit);
            }
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
