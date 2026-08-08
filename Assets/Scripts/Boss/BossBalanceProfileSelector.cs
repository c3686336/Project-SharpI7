using SharpI7.Balance;
using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    public sealed class BossBalanceProfileSelector : MonoBehaviour
    {
        [SerializeField] private BossBalanceProfile profile = BossBalanceProfile.FloorOne;

        public BossBalanceProfile Profile => profile;

        public static BossBalance Resolve(GameObject owner)
        {
            var selector = owner.GetComponent<BossBalanceProfileSelector>();
            var profile = selector != null ? selector.Profile : BossBalanceProfile.FloorOne;
            return BalanceDataLoader.Current.boss.Get(profile);
        }
    }
}