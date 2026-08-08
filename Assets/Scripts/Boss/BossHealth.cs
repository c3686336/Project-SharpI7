using System;
using System.Collections;
using SharpI7.Balance;
using UnityEngine;
using UnityEngine.Video;

namespace SharpI7.Combat
{
    public sealed class BossHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private DamagePopup damagePopupPrefab;

        [Header("Hit Effect")]
        [SerializeField] private VideoClip spellHitVideoClip;
        [SerializeField] private Vector3 spellHitVideoOffset = new(0f, -0.35f, -0.1f);
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

        private void Awake()
        {
            balance = BalanceDataLoader.Current.boss.health;
            CurrentHealth = balance.maxHealth;
            PrepareSpellHitVideo();
        }

        public void TakeDamage(float amount)
        {
            tookDamage?.Invoke(amount);

            if (!IsAlive || IsTransitioningToPhaseTwo || amount <= 0f)
            {
                return;
            }

            ShowSpellHitEffect();
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

        public void TakeSpellDamage(int completedWordCount)
        {
            if (completedWordCount <= 0)
            {
                return;
            }

            TakeDamage(GetSpellDamage(completedWordCount));
        }

        public float GetSpellDamage(int completedWordCount)
        {
            var damageStage = Mathf.Clamp(completedWordCount, 0, balance.maxWordDamageStage);
            return damageStage * balance.damagePerWord;
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
            PlaySpellHitVideo();
        }

        private void PrepareSpellHitVideo()
        {
            if (spellHitVideoClip == null || spellHitVideoObject != null)
            {
                return;
            }

            var shader = Shader.Find("SharpI7/Additive Video");
            if (shader == null)
            {
                return;
            }

            spellHitVideoObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spellHitVideoObject.name = "Spell Fire Hit Effect";
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

        private void PlaySpellHitVideo()
        {
            PrepareSpellHitVideo();
            if (spellHitVideoObject == null || spellHitVideoPlayer == null)
            {
                return;
            }

            spellHitVideoObject.transform.position = transform.position + spellHitVideoOffset;
            spellHitVideoRenderer.enabled = true;
            spellHitVideoPlayer.Stop();
            spellHitVideoPlayer.time = 0d;
            spellHitVideoPlayer.Play();

            if (hideSpellHitVideoRoutine != null)
            {
                StopCoroutine(hideSpellHitVideoRoutine);
            }

            hideSpellHitVideoRoutine = StartCoroutine(HideSpellHitVideoAfterPlayback());
        }

        private IEnumerator HideSpellHitVideoAfterPlayback()
        {
            yield return new WaitForSeconds((float)spellHitVideoClip.length + 0.05f);
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
        [ContextMenu("Test Spell Damage/1 Word (15)")]
        private void TestOneWordDamage()
        {
            if (Application.isPlaying)
            {
                TakeSpellDamage(1);
            }
        }

        [ContextMenu("Test Spell Damage/2 Words (30)")]
        private void TestTwoWordDamage()
        {
            if (Application.isPlaying)
            {
                TakeSpellDamage(2);
            }
        }

        [ContextMenu("Test Spell Damage/3 Words (45)")]
        private void TestThreeWordDamage()
        {
            if (Application.isPlaying)
            {
                TakeSpellDamage(3);
            }
        }

#endif
    }
}
