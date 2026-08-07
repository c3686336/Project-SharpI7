using UnityEngine;

[AddComponentMenu("Project Sharp/Temporary/Dummy Boss")]
[DisallowMultipleComponent]
public sealed class DummyBoss : MonoBehaviour, IBoss
{
    [SerializeField, Min(0f)]
    private float maxHp = 100f;

    public float Hp { get; private set; }

    private void Awake()
    {
        Hp = maxHp;
    }

    public void DealDamage(float damage)
    {
        float appliedDamage = Mathf.Max(0f, damage);
        Hp = Mathf.Max(0f, Hp - appliedDamage);

        Debug.Log($"[DummyBoss] Took {appliedDamage:0.##} damage. HP: {Hp:0.##}/{maxHp:0.##}");
    }
}
