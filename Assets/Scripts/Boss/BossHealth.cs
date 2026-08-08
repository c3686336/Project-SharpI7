using System;
using System.Collections;
using SharpI7.Balance;
using UnityEngine;
using UnityEngine.Video;

namespace SharpI7.Combat
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class BossHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private DamagePopup damagePopupPrefab;

        [Header("Hit Effect")]
        [SerializeField] private VideoClip spellHitVideoClip;
        [SerializeField] private VideoClip spellHitVideoLevelTwoClip;
        [SerializeField] private VideoClip spellHitVideoLevelThreeClip;
        [SerializeField, Min(1f)] private float spellHitVideoLevelTwoScale = 1.25f;
        [SerializeField, Min(1f)] private float spellHitVideoLevelThreeScale = 1.6f;
        [SerializeField, Min(1f)] private float spellHitVideoLevelTwoIntensity = 5f;
        [SerializeField, Min(1f)] private float spellHitVideoLevelThreeIntensity = 2f;
        [SerializeField, Min(1f)] private float spellHitVideoLevelThreeWidthMultiplier = 1.8f;
        [SerializeField] private Vector3 spellHitVideoOffset = new(0f, -0.35f, -0.1f);
        [SerializeField] private Vector3 spellHitVideoLevelThreeOffset = new(0f, 0.15f, -0.1f);
        [SerializeField] private Vector2 spellHitVideoSize = new(4.8f, 4.8f);

        public event Action<float, float> HealthChanged;
        public event Action Died;
        public event Action PhaseTwoTransitionStarted;
        public event Action PhaseTwoStarted;
        public event Action<float> tookDamage;

        public float MaxHealth => IsPhaseTwo ? balance.phaseTwoMaxHealth : balance.maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f || IsTransitioningToPhaseTwo;
        public bool IsPhaseTwo { get; private set; }
        public bool IsTransitioningToPhaseTwo { get; private set; }

        private Coroutine phaseTwoTransitionRoutine;
        private BossHealthBalance balance;
        private GameObject spellHitVideoObject;
        private MeshRenderer spellHitVideoRenderer;
        private VideoPlayer spellHitVideoPlayer;
        private RenderTexture spellHitVideoTexture;
        private Material spellHitVideoMaterial;
        private Coroutine hideSpellHitVideoRoutine;

        private AudioSource audioPlayer;

        private void Awake()
        {
            balance = BossBalanceProfileSelector.Resolve(gameObject).health;
            CurrentHealth = balance.maxHealth;
            PrepareSpellHitVideo();

            audioPlayer = GetComponent<AudioSource>();
        }

        public void TakeDamage(float amount)
        {
            ApplyDamage(amount, true);
        }

        public void TakeDamageWithoutSpellHitEffect(float amount)
        {
            ApplyDamage(amount, false);
        }

        private void ApplyDamage(float amount, bool showSpellHitEffect)
        {
            tookDamage?.Invoke(amount);

            if (!IsAlive || IsTransitioningToPhaseTwo || amount <= 0f)
            {
                return;
            }

            if (showSpellHitEffect)
            {
                ShowSpellHitEffect();
            }

            ShowDamagePopup(amount);

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (!IsAlive && spellHitVideoObject != null)
            {
                Destroy(spellHitVideoObject, (float)spellHitVideoClip.length + 0.05f);
            }

            if (!IsAlive && balance.enablePhaseTwo && !IsPhaseTwo)
            {
                BeginPhaseTwoTransition();
                return;
            }

            HealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (!IsAlive)
            {
                Died?.Invoke();
                Destroy(gameObject);
            }
        }

        public void TakeSpellDamage(CastResult result)
        {
            var castLevel = result.castLevel;
            var damage = result.actualDamage;
            if (castLevel <= 0)
            {
                return;
            }

            var originalClip = spellHitVideoClip;
            if (castLevel >= 3 && spellHitVideoLevelThreeClip != null)
            {
                spellHitVideoClip = spellHitVideoLevelThreeClip;
            }
            else if (castLevel >= 2 && spellHitVideoLevelTwoClip != null)
            {
                spellHitVideoClip = spellHitVideoLevelTwoClip;
            }

            TakeDamage(damage);
            spellHitVideoClip = originalClip;
        }

        public void RestoreFullHealth()
        {
            if (phaseTwoTransitionRoutine != null)
            {
                StopCoroutine(phaseTwoTransitionRoutine);
                phaseTwoTransitionRoutine = null;
            }

            IsPhaseTwo = false;
            IsTransitioningToPhaseTwo = false;
            CurrentHealth = balance.maxHealth;
            HealthChanged?.Invoke(CurrentHealth, balance.maxHealth);
        }

        private void ShowSpellHitEffect()
        {
            PlaySpellHitAudio();
            PlaySpellHitVideo();
        }

        private void PrepareSpellHitVideo()
        {
            if (spellHitVideoClip == null)
            {
                return;
            }

            if (spellHitVideoObject != null)
            {
                if (spellHitVideoPlayer != null && spellHitVideoPlayer.clip != spellHitVideoClip)
                {
                    spellHitVideoPlayer.Stop();
                    spellHitVideoPlayer.clip = spellHitVideoClip;
                    spellHitVideoPlayer.Prepare();
                }

                return;
            }

            var shader = Shader.Find("SharpI7/Additive Video");
            if (shader == null)
            {
                return;
            }

            spellHitVideoObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spellHitVideoObject.name = "Spell Fire Hit Effect";
            spellHitVideoObject.transform.SetParent(transform, false);
            spellHitVideoObject.transform.localScale = new Vector3(spellHitVideoSize.x, spellHitVideoSize.y, 1f);

            var collider = spellHitVideoObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            spellHitVideoRenderer = spellHitVideoObject.GetComponent<MeshRenderer>();
            spellHitVideoTexture = new RenderTexture(
                Mathf.Max(16, (int)spellHitVideoClip.width),
                Mathf.Max(16, (int)spellHitVideoClip.height),
                0);
            spellHitVideoMaterial = new Material(shader) { mainTexture = spellHitVideoTexture };
            spellHitVideoRenderer.material = spellHitVideoMaterial;
            spellHitVideoRenderer.enabled = false;

            var bossSprite = GetComponent<SpriteRenderer>();
            if (bossSprite != null)
            {
                spellHitVideoRenderer.sortingLayerID = bossSprite.sortingLayerID;
                spellHitVideoRenderer.sortingOrder = bossSprite.sortingOrder + 1;
            }

            spellHitVideoPlayer = spellHitVideoObject.AddComponent<VideoPlayer>();
            spellHitVideoPlayer.playOnAwake = false;
            spellHitVideoPlayer.isLooping = false;
            spellHitVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            spellHitVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            spellHitVideoPlayer.targetTexture = spellHitVideoTexture;
            spellHitVideoPlayer.clip = spellHitVideoClip;
            spellHitVideoPlayer.Prepare();
        }

        private void PlaySpellHitAudio()
        {
            audioPlayer.Play();
        }

        private void PlaySpellHitVideo()
        {
            PrepareSpellHitVideo();
            if (spellHitVideoObject == null || spellHitVideoPlayer == null)
            {
                return;
            }

            spellHitVideoObject.transform.localPosition = spellHitVideoClip == spellHitVideoLevelThreeClip ? spellHitVideoLevelThreeOffset : spellHitVideoOffset;
            var isLevelThreeEffect = spellHitVideoClip == spellHitVideoLevelThreeClip;
            var isEnhancedEffect = spellHitVideoClip == spellHitVideoLevelTwoClip || isLevelThreeEffect;
            var scaleMultiplier = isLevelThreeEffect
                ? spellHitVideoLevelThreeScale
                : isEnhancedEffect ? spellHitVideoLevelTwoScale : 1f;
            var widthMultiplier = isLevelThreeEffect ? spellHitVideoLevelThreeWidthMultiplier : 1f;
            spellHitVideoObject.transform.localScale = new Vector3(
                spellHitVideoSize.x * scaleMultiplier * widthMultiplier,
                spellHitVideoSize.y * scaleMultiplier,
                1f);
            var intensity = isLevelThreeEffect ? spellHitVideoLevelThreeIntensity : isEnhancedEffect ? spellHitVideoLevelTwoIntensity : 1f;
            spellHitVideoMaterial.SetFloat("_Intensity", intensity);
            spellHitVideoRenderer.enabled = true;
            spellHitVideoPlayer.Stop();
            spellHitVideoPlayer.time = 0d;
            spellHitVideoPlayer.Play();

            if (hideSpellHitVideoRoutine != null)
            {
                StopCoroutine(hideSpellHitVideoRoutine);
            }

            hideSpellHitVideoRoutine = StartCoroutine(HideSpellHitVideoAfterPlayback((float)spellHitVideoClip.length));
        }

        private IEnumerator HideSpellHitVideoAfterPlayback(float playbackLength)
        {
            yield return new WaitForSeconds(playbackLength + 0.05f);
            if (spellHitVideoRenderer != null)
            {
                spellHitVideoRenderer.enabled = false;
            }

            hideSpellHitVideoRoutine = null;
        }

        private void BeginPhaseTwoTransition()
        {
            IsTransitioningToPhaseTwo = true;
            HealthChanged?.Invoke(CurrentHealth, balance.maxHealth);
            PhaseTwoTransitionStarted?.Invoke();
            phaseTwoTransitionRoutine = StartCoroutine(StartPhaseTwoAfterDelay());
        }

        private IEnumerator StartPhaseTwoAfterDelay()
        {
            if (balance.phaseTwoTransitionDelay > 0f)
            {
                yield return new WaitForSeconds(balance.phaseTwoTransitionDelay);
            }

            phaseTwoTransitionRoutine = null;
            IsTransitioningToPhaseTwo = false;
            StartPhaseTwo();
        }

        private void StartPhaseTwo()
        {
            IsPhaseTwo = true;
            CurrentHealth = balance.phaseTwoMaxHealth;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            PhaseTwoStarted?.Invoke();
        }

        private void ShowDamagePopup(float amount)
        {
            var popup = damagePopupPrefab != null
                ? Instantiate(damagePopupPrefab, transform.position, Quaternion.identity)
                : new GameObject("DamagePopup").AddComponent<DamagePopup>();
            popup.Begin(transform.position, amount);
        }

#if UNITY_EDITOR
        [ContextMenu("Test damage (10)")]
        private void TestOneWordDamage()
        {
            if (Application.isPlaying)
            {
                TakeDamage(10);
            }
        }

        [ContextMenu("Test damage (30)")]
        private void TestTwoWordDamage()
        {
            if (Application.isPlaying)
            {
                TakeDamage(30);
            }
        }

        [ContextMenu("Test damage (70)")]
        private void TestThreeWordDamage()
        {
            if (Application.isPlaying)
            {
                TakeDamage(70);
            }
        }

#endif
    }
}
