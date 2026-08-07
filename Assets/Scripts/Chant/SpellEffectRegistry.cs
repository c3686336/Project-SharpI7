using System;
using System.Collections.Generic;
using UnityEngine;

public class SpellEffectRegistry : MonoBehaviour
{
    [Serializable]
    private class EffectEntry
    {
        public string effectId;

        public GameObject prefab;
    }

    [SerializeField]
    private List<EffectEntry> effects =
        new List<EffectEntry>();

    public GameObject GetEffect(
        string effectId
    )
    {
        if (string.IsNullOrEmpty(effectId))
            return null;

        foreach (EffectEntry entry in effects)
        {
            if (entry == null)
                continue;

            if (entry.effectId == effectId)
            {
                return entry.prefab;
            }
        }

        Debug.LogWarning(
            $"[SpellEffectRegistry] 이펙트를 찾지 못했습니다: {effectId}"
        );

        return null;
    }

    public GameObject SpawnEffect(
        string effectId,
        Vector3 position,
        Quaternion rotation
    )
    {
        GameObject prefab =
            GetEffect(effectId);

        if (prefab == null)
            return null;

        return Instantiate(
            prefab,
            position,
            rotation
        );
    }
}