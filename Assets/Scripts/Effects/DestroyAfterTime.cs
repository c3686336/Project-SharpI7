using UnityEngine;

public sealed class DestroyAfterTime : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float lifetime = 0.7f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}