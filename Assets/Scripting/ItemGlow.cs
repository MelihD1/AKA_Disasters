using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a pickable item glow, breathing bright then dark then bright again so
/// it catches the eye across a room.
///
/// It changes material INSTANCES (renderer.material), so the shared materials
/// and the item prefabs themselves are never modified.
/// </summary>
[DisallowMultipleComponent]
public class ItemGlow : MonoBehaviour
{
    [Tooltip("Colour of the glow.")]
    public Color glowColor = new Color(1f, 0.84f, 0.36f, 1f);

    [Tooltip("How bright the glow gets at its peak.")]
    [Range(0f, 8f)] public float peakIntensity = 2.4f;

    [Tooltip("Breaths per second. 0.8 = a full bright-dark cycle every 1.25s.")]
    [Range(0.05f, 5f)] public float speed = 0.8f;

    [Tooltip("How much of the cycle is spent glowing. 0.5 = half lit, half dark.")]
    [Range(0.05f, 1f)] public float onFraction = 0.5f;

    [Header("Extra light")]
    [Tooltip("Pulse a small point light too, so the glow reads in dark corners.")]
    public bool addLight = true;

    [Tooltip("Range of the pulsing light.")]
    public float lightRange = 3.5f;

    [Tooltip("How bright the pulsing light gets at its peak.")]
    [Range(0f, 6f)] public float lightPeak = 0.9f;

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private Material[] materials;
    private Light glowLight;

    /// <summary>0 = dark, 1 = fully glowing. Handy for tests and tweaking.</summary>
    public float CurrentGlow { get; private set; }

    private void Awake()
    {
        List<Material> found = new List<Material>();

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            // .materials gives this renderer its own copies.
            foreach (Material material in renderer.materials)
            {
                if (material == null ||
                    !material.HasProperty(EmissionColorId))
                {
                    continue;
                }

                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.None;

                material.SetColor(EmissionColorId, Color.black);

                found.Add(material);
            }
        }

        materials = found.ToArray();

        if (!addLight)
            return;

        glowLight = gameObject.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.range = lightRange;
        glowLight.intensity = 0f;
        glowLight.shadows = LightShadows.None;
    }

    private void Update()
    {
        float t = Mathf.Repeat(Time.time * speed, 1f);

        // Rise for onFraction of the cycle, fall for the rest, with the easing
        // so it lingers at both bright and dark.
        float pulse = t < onFraction
            ? t / Mathf.Max(0.0001f, onFraction)
            : 1f - (t - onFraction) / Mathf.Max(0.0001f, 1f - onFraction);

        CurrentGlow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pulse));

        Color emission = glowColor * (CurrentGlow * peakIntensity);

        if (materials != null)
        {
            foreach (Material material in materials)
            {
                if (material != null)
                    material.SetColor(EmissionColorId, emission);
            }
        }

        if (glowLight != null)
            glowLight.intensity = CurrentGlow * lightPeak;
    }

    private void OnDestroy()
    {
        // Leave nothing behind when the item is picked up or the room unloads.
        if (materials == null)
            return;

        foreach (Material material in materials)
        {
            if (material != null)
                Destroy(material);
        }
    }
}
