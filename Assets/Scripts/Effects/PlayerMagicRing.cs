using UnityEngine;

public sealed class PlayerMagicRing : MonoBehaviour
{
    [SerializeField] private Transform ringPrefab;
    [SerializeField] private Color ringColor = new(1f, 0.08f, 0.08f, 1f);
    [SerializeField] private Vector3 footOffset = new(0f, -0.48f, 0.1f);

    private PlayerController playerController;
    private GameObject ringInstance;

    private void Awake()
    {
        if (ringPrefab == null) return;

        playerController = GetComponent<PlayerController>();

        ringInstance = Instantiate(ringPrefab, transform).gameObject;
        ringInstance.name = "Red Magic Ring";
        ringInstance.transform.localPosition = footOffset;
        ringInstance.transform.localRotation = Quaternion.Euler(-101.284f, 13.41701f, -22.905f);
        ringInstance.transform.localScale = new Vector3(0.22f, 0.4f, 1f);

        foreach (var particle in ringInstance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particle.main;
            main.startColor = ringColor;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        ringInstance.SetActive(false);
    }

    private void Update()
    {
        if (ringInstance != null)
        {
            var hasChantInput = playerController != null && playerController.HasChantInput;
            ringInstance.SetActive(hasChantInput);

        }
    }
}
