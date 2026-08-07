using UnityEngine;

internal sealed class PlayerHealth
{
    public PlayerHealth(float maximum)
    {
        Maximum = maximum;
        Current = maximum;
    }

    public float Maximum { get; }
    public float Current { get; private set; }
    public bool IsAlive => Current > 0f;

    public bool TryTakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f)
        {
            return false;
        }

        Current = Mathf.Max(0f, Current - amount);
        return true;
    }
}
