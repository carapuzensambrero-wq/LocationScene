using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-wide configuration and events for global map mechanics.
/// Keep one instance in the scene and assign it to the GlobalConfig field
/// on systems that need to consume these settings.
/// </summary>
public sealed class GlobalConfig : MonoBehaviour
{
    public static GlobalConfig Instance { get; private set; }

    [Header("Players")]
    [SerializeField] private List<Transform> players = new List<Transform>();

    [Header("Shrinking field")]
    [SerializeField] private Vector3 fieldCenter;
    [SerializeField, Min(0f)] private float fieldRadius = 100f;

    public Vector3 FieldCenter => fieldCenter;
    public float FieldRadius => fieldRadius;
    public IReadOnlyList<Transform> Players => players;

    public event Action<Vector3> FieldCenterChanged;
    public event Action<float> FieldRadiusChanged;
    public event Action FieldSettingsChanged;

    public bool HasFieldSettings => true;

    public void RegisterPlayer(Transform player)
    {
        if (player != null && !players.Contains(player)) players.Add(player);
    }

    public void UnregisterPlayer(Transform player)
    {
        players.Remove(player);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnValidate()
    {
        fieldRadius = Mathf.Max(0f, fieldRadius);

        if (Application.isPlaying && Instance == this)
            FieldSettingsChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetFieldCenter(Vector3 center)
    {
        if (fieldCenter == center) return;

        fieldCenter = center;
        FieldCenterChanged?.Invoke(fieldCenter);
        FieldSettingsChanged?.Invoke();
    }

    public void SetFieldRadius(float radius)
    {
        radius = Mathf.Max(0f, radius);
        if (Mathf.Approximately(fieldRadius, radius)) return;

        fieldRadius = radius;
        FieldRadiusChanged?.Invoke(fieldRadius);
        FieldSettingsChanged?.Invoke();
    }

    public void SetField(Vector3 center, float radius)
    {
        bool centerChanged = fieldCenter != center;
        float clampedRadius = Mathf.Max(0f, radius);
        bool radiusChanged = !Mathf.Approximately(fieldRadius, clampedRadius);

        fieldCenter = center;
        fieldRadius = clampedRadius;

        if (centerChanged) FieldCenterChanged?.Invoke(fieldCenter);
        if (radiusChanged) FieldRadiusChanged?.Invoke(fieldRadius);
        if (centerChanged || radiusChanged) FieldSettingsChanged?.Invoke();
    }
}
