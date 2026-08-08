using System;

namespace SharpI7.Combat
{
    /// <summary>
    /// Common contract used by spell projectiles and enemy attacks.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        void TakeDamage(float amount);

        event Action<float> tookDamage;
    }
}
