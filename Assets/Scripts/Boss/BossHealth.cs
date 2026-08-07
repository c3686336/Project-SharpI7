using System;
using UnityEngine;

namespace SharpI7.Combat
{
    public sealed class BossHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 500f;
        [SerializeField] private DamagePopup damagePopupPrefab;
        [SerializeField, Min(0f)] private float damagePerWord = 15f;
        [SerializeField, Min(1)] private int maxWordDamageStage = 3;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            ShowDamagePopup(amount);
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);

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
            var damageStage = Mathf.Clamp(completedWordCount, 0, maxWordDamageStage);
            return damageStage * damagePerWord;
        }

        public void RestoreFullHealth()
        {
            CurrentHealth = maxHealth;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        private void ShowDamagePopup(float amount)
        {
            if (damagePopupPrefab == null)
            {
                return;
            }

            var popup = Instantiate(damagePopupPrefab, transform.position, Quaternion.identity);
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
            damagePerWord = Mathf.Max(0f, damagePerWord);
            maxWordDamageStage = Mathf.Max(1, maxWordDamageStage);
        }
#endif
    }
}
