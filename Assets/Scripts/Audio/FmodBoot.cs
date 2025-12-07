// Assets/_Project/Scripts/Runtime/Audio/FmodBoot.cs
using System.Collections;
using UnityEngine;
using FMOD;
using FMOD.Studio;
using FMODUnity;

public class FmodBoot : MonoBehaviour
{
    [Header("Driver Selection")]
    [Tooltip("Selects a driver whose name contains this string (partial match is fine). Only driver selection can be changed after FMOD initialization. Other settings must be configured in FMOD Studio Settings asset.")]
    [SerializeField] string preferDriverNameContains = "UR22";
    
    // NOTE: The following settings CANNOT be changed after FMOD initialization.
    // They must be configured in FMOD Studio Settings asset (Window > FMOD > Settings):
    // - Output Type (e.g., ASIO)
    // - Sample Rate (e.g., 48000)
    // - DSP Buffer Length (e.g., 256)
    // - DSP Buffer Count (e.g., 4)
    // - Software Channels (e.g., 256)
    // - DSP Buffer Pool Size (e.g., 256KB = 262144 bytes)
    // - Studio Command Queue Size (e.g., 256KB = 262144 bytes)

    void Start()
    {
        // Use Start() instead of Awake() to ensure RuntimeManager is fully initialized
        // Start() always runs after all Awake() methods, regardless of execution order
        
        // Wait a frame to ensure RuntimeManager is fully ready (defensive)
        StartCoroutine(ConfigureFMODDelayed());
    }

    System.Collections.IEnumerator ConfigureFMODDelayed()
    {
        // Wait one frame to ensure RuntimeManager is fully initialized
        yield return null;
        
        FMOD.System sys;
        try
        {
            sys = RuntimeManager.CoreSystem;
        }
        catch (System.Exception e)
        {
            // Use LogWarning instead of LogError to avoid pausing editor if "Pause on Error" is enabled
            UnityEngine.Debug.LogWarning($"[FmodBoot] Failed to access RuntimeManager.CoreSystem: {e.Message}. FMOD configuration skipped.");
            yield break;
        }

        // Only configure settings that can be changed after initialization
        // Most settings (output type, sample rate, buffer sizes, advanced settings) must be configured
        // in FMOD Studio Settings asset before initialization

        // Pick preferred driver if present (driver selection can be changed after init)
        sys.getNumDrivers(out int n);
        int chosen = -1;
        for (int i = 0; i < n; i++)
        {
            sys.getDriverInfo(i, out string name, 256, out _, out int rate,
                              out SPEAKERMODE mode, out int chans);
            UnityEngine.Debug.Log($"[FMOD] Driver {i}: {name} @ {rate}Hz, {mode}, chans:{chans}");
            if (chosen < 0 && !string.IsNullOrEmpty(preferDriverNameContains) &&
                name.ToLower().Contains(preferDriverNameContains.ToLower()))
                chosen = i;
        }
        if (chosen >= 0)
        {
            sys.setDriver(chosen);
            UnityEngine.Debug.Log($"[FMOD] Selected driver index {chosen} (pref='{preferDriverNameContains}')");
        }

        // Echo final settings for verification
        sys.getSoftwareFormat(out int sr, out SPEAKERMODE sm, out _);
        sys.getDSPBufferSize(out uint len, out int num);
        sys.getSoftwareChannels(out int swChans);
        sys.getDriver(out int active);
        sys.getDriverInfo(active, out string activeName, 256, out _, out _, out _, out _);
        UnityEngine.Debug.Log($"[FMOD] Active: '{activeName}', SR:{sr}Hz, Speaker:{sm}, DSP:{len} x {num}, SoftwareChannels:{swChans}");
    }
}
