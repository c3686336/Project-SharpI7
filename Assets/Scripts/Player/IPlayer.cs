using UnityEngine;

public interface IPlayer {
    float GetDashCooldownUntil();
    void LockMovement();
    void UnLockMovement();
    int GetHp();
    void DealDamage();
    void SpellSucceeded(float damage);
    void SpellFailed();
}
