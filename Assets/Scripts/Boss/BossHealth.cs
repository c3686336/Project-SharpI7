using System;
using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    public sealed class BossHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 500f;
        [Header("Phase Two")]
        [SerializeField] private bool enablePhaseTwo = true;
        [SerializeField, Min(1f)] private float phaseTwoMaxHealth = 1000f;
        [SerializeField, Min(0f)] private float phaseTwoTransitionDelay = 2f;
        [SerializeField] private DamagePopup damagePopupPrefab;
        [SerializeField, Min(0f)] private float damagePerWord = 15f;
        [SerializeField, Min(1)] private int maxWordDamageStage = 3;

        public event Action<float, float> HealthChanged;
        public event Action Died;
        public event Action PhaseTwoTransitionStarted;
        public event Action PhaseTwoStarted;
        public event Action<float> tookDamage;

        public float MaxHealth => IsPhaseTwo ? phaseTwoMaxHealth : maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f || IsTransitioningToPhaseTwo;
        public bool IsPhaseTwo { get; private set; }
        public bool IsTransitioningToPhaseTwo { get; private set; }

        private Coroutine phaseTwoTransitionRoutine;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            tookDamage?.Invoke(amount);

            if (!IsAlive || IsTransitioningToPhaseTwo || amount <= 0f)
            {
                return;
            }

            ShowDamagePopup(amount);
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

            if (!IsAlive && enablePhaseTwo && !IsPhaseTwo)
            {
                BeginPhaseTwoTransition();
                return;
            }

            HealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (!IsAlive)
            {
                Died?.Invoke();
                OutGameManager.LoadWin();
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
            var damageStage = Mathf.Clamp(completedWordCount, 0, maxWordDamageStage);
            return damageStage * damagePerWord;
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
            CurrentHealth = maxHealth;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        private void BeginPhaseTwoTransition()
        {
            IsTransitioningToPhaseTwo = true;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            PhaseTwoTransitionStarted?.Invoke();
            phaseTwoTransitionRoutine = StartCoroutine(StartPhaseTwoAfterDelay());
        }

        private IEnumerator StartPhaseTwoAfterDelay()
        {
            if (phaseTwoTransitionDelay > 0f)
            {
                yield return new WaitForSeconds(phaseTwoTransitionDelay);
            }

            phaseTwoTransitionRoutine = null;
            IsTransitioningToPhaseTwo = false;
            StartPhaseTwo();
        }

        private void StartPhaseTwo()
        {
            IsPhaseTwo = true;
            CurrentHealth = phaseTwoMaxHealth;
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

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            phaseTwoMaxHealth = Mathf.Max(1f, phaseTwoMaxHealth);
            phaseTwoTransitionDelay = Mathf.Max(0f, phaseTwoTransitionDelay);
            damagePerWord = Mathf.Max(0f, damagePerWord);
            maxWordDamageStage = Mathf.Max(1, maxWordDamageStage);
        }
#endif
    }
}
