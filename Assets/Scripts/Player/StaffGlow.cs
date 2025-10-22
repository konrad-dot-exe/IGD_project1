// Assets/Scripts/Player/StaffGlow.cs
using System.Collections;
using UnityEngine;

/// <summary>
/// Drives emissive intensity of the orb material: idle pulse + fire flash.
/// Works with URP Lit (uses _EmissionColor). Attach to the staff or orb.
/// </summary>
public class StaffGlow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Renderer orbRenderer;     // MeshRenderer or SkinnedMeshRenderer
    [SerializeField] int orbMaterialIndex = 0; // Index of the orb material on the renderer

    [Header("Color / Intensity")]
    [ColorUsage(true, true)]                    // HDR color picker
    [SerializeField] Color baseColor = new Color(0.2f, 1f, 1f, 1f);
    [SerializeField] float baseIntensity = 1.2f;   // idle baseline
    [SerializeField] float pulseAmplitude = 0.35f; // how much it breathes around baseline
    [SerializeField] float pulseSpeed = 0.5f;      // Hz (cycles per second)

    [Header("Flash On Fire")]
    [SerializeField] float flashPeak = 6f;         // peak extra intensity on fire
    [SerializeField] float flashAttack = 0.05f;    // seconds to reach peak
    [SerializeField] float flashDecay = 0.6f;      // seconds to return to pulse

    Material orbMat;              // instanced material
    float flashAdd;               // additional intensity from current flash
    Coroutine flashCo;

    static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        if (!orbRenderer)
        {
            orbRenderer = GetComponentInChildren<Renderer>();
        }
        if (orbRenderer)
        {
            // Ensure a unique instance so we don't affect shared assets
            var mats = orbRenderer.materials;
            orbMaterialIndex = Mathf.Clamp(orbMaterialIndex, 0, mats.Length - 1);
            orbMat = mats[orbMaterialIndex];
            orbRenderer.materials = mats; // assign back to apply instancing
            orbMat.EnableKeyword("_EMISSION");
        }
    }

    void Update()
    {
        if (!orbMat) return;

        // Idle pulse around baseline
        float pulse = pulseAmplitude * 0.5f * (1f + Mathf.Sin(Time.time * Mathf.PI * 2f * pulseSpeed));
        float intensity = Mathf.Max(0f, baseIntensity + pulse + flashAdd);

        // URP Lit uses _EmissionColor (HDR). Multiply color by intensity.
        orbMat.SetColor(EmissionColorID, baseColor * intensity);
    }

    /// <summary>External trigger from your fire code (or via a MuzzleRecoil event).</summary>
    public void Flash()
    {
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashCo());
    }

    IEnumerator FlashCo()
    {
        // Attack
        float t = 0f;
        while (t < flashAttack)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / flashAttack);
            flashAdd = Mathf.Lerp(0f, flashPeak, u);
            yield return null;
        }

        // Decay
        t = 0f;
        while (t < flashDecay)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / flashDecay);
            flashAdd = Mathf.Lerp(flashPeak, 0f, u);
            yield return null;
        }

        flashAdd = 0f;
        flashCo = null;
    }
}
