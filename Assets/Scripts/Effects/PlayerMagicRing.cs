using UnityEngine;

public sealed class PlayerMagicRing : MonoBehaviour
{
    [SerializeField] private Transform ringPrefab;
    [SerializeField] private Color ringColor = new(1f, 0.08f, 0.08f, 1f);

    private PlayerController playerController;
    private GameObject ringInstance;

    private void Awake()
    {
        if (ringPrefab == null) return;

        playerController = GetComponent<PlayerController>();

        ringInstance = Instantiate(ringPrefab, transform).gameObject;
        ringInstance.name = "Red Magic Ring";
        ringInstance.transform.localPosition = new Vector3(0f, -0.25f, 0.1f);
        ringInstance.transform.localRotation = Quaternion.identity;
        ringInstance.transform.localScale = Vector3.one * 0.275f;

        foreach (var particle in ringInstance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particle.main;
            main.startColor = ringColor;
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
