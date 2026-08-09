using UnityEngine;

/// <summary>
/// Applies the global field center and radius to a 3D field mesh.
/// The mesh is assumed to represent a unit-radius field in local XZ space.
/// </summary>
public sealed class ShrinkingField : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private GlobalConfig globalConfig;
    [SerializeField] private bool followCenter = true;

    [Header("Mesh")]
    [SerializeField, Min(0.0001f)] private float meshRadius = 1f;
    [SerializeField] private bool preserveYScale = true;

    private Vector3 initialScale;

    private void Awake()
    {
        initialScale = transform.localScale;
        ResolveConfig();
    }

    private void OnEnable()
    {
        ResolveConfig();
        Subscribe();
        ApplySettings();
    }

    private void Update()
    {
        // This also supports direct changes to serialized GlobalConfig fields
        // from the Inspector or another system that does not use setter methods.
        if (globalConfig == null)
            ResolveConfig();

        ApplySettings();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveConfig()
    {
        if (globalConfig == null)
            globalConfig = GlobalConfig.Instance;
    }

    private void Subscribe()
    {
        if (globalConfig == null) return;
        globalConfig.FieldSettingsChanged += ApplySettings;
    }

    private void Unsubscribe()
    {
        if (globalConfig == null) return;
        globalConfig.FieldSettingsChanged -= ApplySettings;
    }

    private void ApplySettings()
    {
        if (globalConfig == null) return;

        if (followCenter)
        {
            Vector3 position = transform.position;
            position.x = globalConfig.FieldCenter.x;
            position.z = globalConfig.FieldCenter.z;
            transform.position = position;
        }

        float scale = globalConfig.FieldRadius / meshRadius;
        transform.localScale = new Vector3(
            initialScale.x * scale,
            preserveYScale ? initialScale.y : initialScale.y * scale,
            initialScale.z * scale);
    }
}
