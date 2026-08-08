using System.Collections;
using UnityEngine;

namespace SharpI7.Combat
{
    [DisallowMultipleComponent]
    public sealed class BossArenaCenterSpawn : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;

            Bounds arenaBounds;
            if (!ArenaBounds.TryGetWallInteriorBounds(out arenaBounds) &&
                !ArenaBounds.TryGetWorldBounds(out arenaBounds))
            {
                yield break;
            }

            var center = arenaBounds.center;
            transform.position = new Vector3(center.x, center.y, transform.position.z);
        }
    }
}