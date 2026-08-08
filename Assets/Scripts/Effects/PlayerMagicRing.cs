using UnityEngine;

public sealed class PlayerMagicRing : MonoBehaviour
{
    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private Transform ringPrefab;
    [SerializeField] private Color ringColor = new(1f, 0.08f, 0.08f, 1f);
    [SerializeField] private Vector3 footOffset = new(0f, -0.48f, 0.1f);
    [SerializeField] private Shader chantAuraShader;
    [SerializeField] private Texture2D chantAuraTexture;
    [SerializeField] private Color chantAuraColor = new(1f, 0.1f, 0.01f, 0.62f);
    [SerializeField] private Vector2 chantAuraSize = new(2.2f, 1.5f);

    private PlayerController playerController;
    private GameObject ringInstance;
    private GameObject auraInstance;
    private Material auraMaterial;

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
        CreateChantAura();
    }

    private void Update()
    {
        if (ringInstance != null)
        {
            var hasChantInput = playerController != null && playerController.HasChantInput;
            ringInstance.SetActive(hasChantInput);
        }

        if (auraInstance != null)
            auraInstance.SetActive(playerController != null && playerController.IsChanting);
    }

    private void OnDestroy()
    {
        if (auraMaterial != null)
            Destroy(auraMaterial);
    }

    private void CreateChantAura()
    {
        if (chantAuraShader == null || chantAuraTexture == null) return;

        auraInstance = GameObject.CreatePrimitive(PrimitiveType.Quad);
        auraInstance.name = "Chant Energy Aura";
        auraInstance.transform.SetParent(transform, false);
        auraInstance.transform.localPosition = new Vector3(0f, -0.02f, 0.15f);
        auraInstance.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        auraInstance.transform.localScale = chantAuraSize;

        var collider = auraInstance.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        var renderer = auraInstance.GetComponent<MeshRenderer>();
        auraMaterial = new Material(chantAuraShader);
        auraMaterial.SetTexture(MainTextureId, chantAuraTexture);
        auraMaterial.SetColor(ColorId, chantAuraColor);
        renderer.sharedMaterial = auraMaterial;
        renderer.sortingOrder = GetComponent<SpriteRenderer>().sortingOrder - 1;
        auraInstance.SetActive(false);
    }
}
