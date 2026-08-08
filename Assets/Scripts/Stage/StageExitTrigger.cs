using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class StageExitTrigger : MonoBehaviour
{
    public event Action PlayerEntered;

    private bool wasEntered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (wasEntered || !other.CompareTag("Player"))
        {
            return;
        }

        wasEntered = true;
        PlayerEntered?.Invoke();
    }
}
