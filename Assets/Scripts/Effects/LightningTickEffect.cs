using UnityEngine;

public sealed class LightningTickEffect : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float lifetime = 0.8f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}