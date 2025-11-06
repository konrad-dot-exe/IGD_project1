// Assets/_Project/Scripts/Runtime/Audio/FmodBoot.cs
using UnityEngine;
using FMOD;
using FMODUnity;

public class FmodBoot : MonoBehaviour
{
    [Header("Output")]
    [SerializeField] string preferDriverNameContains = "UR22"; // partial match is fine
    [SerializeField] OUTPUTTYPE output = OUTPUTTYPE.ASIO;

    [Header("Format")]
    [SerializeField] int sampleRate = 48000;
    [SerializeField] uint dspBufferLength = 256;  // try 256 then 128
    [SerializeField] int dspNumBuffers = 4;

    void Awake()
    {
        var sys = RuntimeManager.CoreSystem;

        // Configure before init completes
        sys.setOutput(output);
        sys.setSoftwareFormat(sampleRate, SPEAKERMODE.DEFAULT, 0);
        sys.setDSPBufferSize(dspBufferLength, dspNumBuffers);

        // Pick preferred driver if present
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

        // Echo final settings
        sys.getSoftwareFormat(out int sr, out SPEAKERMODE sm, out _);
        sys.getDSPBufferSize(out uint len, out int num);
        sys.getDriver(out int active);
        sys.getDriverInfo(active, out string activeName, 256, out _, out _, out _, out _);
        UnityEngine.Debug.Log($"[FMOD] Active: '{activeName}', SR:{sr}Hz, Speaker:{sm}, DSP:{len} x {num}");
    }
}
