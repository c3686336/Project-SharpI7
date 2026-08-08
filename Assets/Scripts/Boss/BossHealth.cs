using System;
using System.Collections;
using SharpI7.Balance;
using UnityEngine;

namespace SharpI7.Combat
{
    public sealed class BossHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private DamagePopup damagePopupPrefab;

        [Header("Hit Effect")]
        [SerializeField] private GameObject littleFireHitEffectPrefab;
        [SerializeField] private Vector3 littleFireHitEffectOffset;

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

        private void Awake()
        {
            balance = BalanceDataLoader.Current.boss.health;
            CurrentHealth = balance.maxHealth;
        }

        public void TakeDamage(float amount)
        {
            tookDamage?.Invoke(amount);

            if (!IsAlive || IsTransitioningToPhaseTwo || amount <= 0f)
            {
                return;
            }

            ShowLittleFireHitEffect();
            ShowDamagePopup(amount);

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

            if (!IsAlive && balance.enablePhaseTwo && !IsPhaseTwo)
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

        private void ShowLittleFireHitEffect()
        {
            if (littleFireHitEffectPrefab == null)
            {
                return;
            }

            Instantiate(
                littleFireHitEffectPrefab,
                transform.position + littleFireHitEffectOffset,
                Quaternion.identity);
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
