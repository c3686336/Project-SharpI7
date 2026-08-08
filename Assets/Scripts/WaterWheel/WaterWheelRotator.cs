using UnityEngine;

public class WaterWheelRotator : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 50f;
    [SerializeField] private bool rotateClockwise = true;

    private void Update()
    {
        float direction = rotateClockwise ? -1f : 1f;
        transform.Rotate(0f, 0f, rotationSpeed * direction * Time.deltaTime);
    }
}