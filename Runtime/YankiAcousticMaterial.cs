using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class YankiAcousticMaterial : MonoBehaviour
{
    public enum PresetType
    {
        Custom,
        Concrete,       // Concrete (High blocking - 0.95)
        Wood,           // Wood (Medium blocking - 0.45)
        Glass,          // Glass (Light-to-medium blocking - 0.25)
        Plasterboard,   // Plasterboard (Light blocking - 0.35)
        Fabric,         // Fabric / Curtain (Very light blocking - 0.15)
        Soundproof      // Fully dampening / Insulated (Complete blocking - 1.0)
    }

    [Header("Acoustic Material Properties")]
    [SerializeField] private PresetType preset = PresetType.Custom;

    [Tooltip("The absorption coefficient of the material (0 = no absorption, 1 = full absorption)")]
    [Range(0f, 1f)]
    public float absorptionCoefficient = 0.5f;

    private void OnValidate()
    {
        ApplyPreset();
    }

    private void ApplyPreset()
    {
        switch (preset)
        {
            case PresetType.Concrete:
                absorptionCoefficient = 0.95f;
                break;
            case PresetType.Wood:
                absorptionCoefficient = 0.45f;
                break;
            case PresetType.Glass:
                absorptionCoefficient = 0.25f;
                break;
            case PresetType.Plasterboard:
                absorptionCoefficient = 0.35f;
                break;
            case PresetType.Fabric:
                absorptionCoefficient = 0.15f;
                break;
            case PresetType.Soundproof:
                absorptionCoefficient = 1.0f; // Completely blocks sound (100% absorption)
                break;
        }
    }
}