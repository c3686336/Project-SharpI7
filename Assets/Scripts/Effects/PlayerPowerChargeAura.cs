using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerPowerChargeAura : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int FresnelColorId = Shader.PropertyToID("_FresnelColor");

    [Header("Power Charge Aura")]
    [SerializeField] private Transform auraPrefab;
    [SerializeField, Min(1)] private int minimumCharacters = 6;
    [SerializeField] private Vector3 worldOffset = new(0f, 0.15f, 0.2f);
    [SerializeField, Min(0.01f)] private float worldScale = 0.5f;
    [SerializeField, Min(0.1f)] private float horizontalScaleMultiplier = 1.3f;
    [SerializeField, Range(0.1f, 1f)] private float verticalScaleMultiplier = 2f / 3f;
    [SerializeField] private Color fireColor = new(1f, 0.08f, 0.01f, 1f);

    private PlayerController playerController;
    private Transform auraInstance;
    private ParticleSystem[] particles;
    private bool wasActive;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        CreateAura();
    }

    private void LateUpdate()
    {
        if (auraInstance == null) return;

        auraInstance.position = transform.position + worldOffset;
        var shouldShow = playerController != null &&
                         playerController.IsChanting &&
                         playerController.ChantInputLength >= minimumCharacters;

        if (auraInstance.gameObject.activeSelf != shouldShow)
        {
            auraInstance.gameObject.SetActive(shouldShow);
        }

        if (shouldShow && !wasActive)
        {
            foreach (var particle in particles)
            {
                particle.Clear(true);
                particle.Play(true);
            }
        }

        wasActive = shouldShow;
    }

    private void OnDestroy()
    {
        if (auraInstance != null)
            Destroy(auraInstance.gameObject);
    }

    private void CreateAura()
    {
        if (auraPrefab == null) return;

        auraInstance = Instantiate(auraPrefab);
        auraInstance.name = "Fire Power Charge Aura";
        auraInstance.position = transform.position + worldOffset;
        // Keep the source prefab's orientation and proportions; only scale its visible size.
        auraInstance.localScale = Vector3.Scale(auraPrefab.localScale, new Vector3(worldScale * horizontalScaleMultiplier, worldScale * verticalScaleMultiplier, worldScale));

        particles = auraInstance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var particle in particles)
        {
            var main = particle.main;
            main.startColor = fireColor;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var particleRenderer = particle.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer == null) continue;

            var material = particleRenderer.material;
            if (material.HasProperty(ColorId)) material.SetColor(ColorId, fireColor);
            if (material.HasProperty(TintId)) material.SetColor(TintId, fireColor);
            if (material.HasProperty(FresnelColorId)) material.SetColor(FresnelColorId, fireColor);
        }

        auraInstance.gameObject.SetActive(false);
    }
}
