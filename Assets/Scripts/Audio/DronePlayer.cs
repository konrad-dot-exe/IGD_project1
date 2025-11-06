using UnityEngine;
using FMOD.Studio;
using FMODUnity;

[DisallowMultipleComponent]
public class DronePlayer : MonoBehaviour
{
    public EventReference droneEvent;
    [Range(0f,1f)] public float startVolume = 0.35f;
    public float volFadeSecs = 0.12f;
    public float pitchRampSecs = 0.08f;

    EventInstance _inst;
    Coroutine _volCo, _pitCo;

    void OnEnable()
    {
        _inst = RuntimeManager.CreateInstance(droneEvent);
        _inst.setVolume(0f);
        _inst.start();                         // event loops in FMOD
        if (isActiveAndEnabled) _volCo = StartCoroutine(FadeVolCo(0f, startVolume, volFadeSecs));
        else _inst.setVolume(startVolume);
    }

    // --- public API ---
    public void SetSemitones(int semis)
    {
        float to = Mathf.Pow(2f, semis / 12f);
        if (_pitCo != null) StopCoroutine(_pitCo);
        if (isActiveAndEnabled) _pitCo = StartCoroutine(RampPitchCo(to, pitchRampSecs));
        else _inst.setPitch(to);
    }

    public void SetVolume(float v)
    {
        v = Mathf.Clamp01(v);
        if (_volCo != null) StopCoroutine(_volCo);
        if (isActiveAndEnabled) _volCo = StartCoroutine(FadeVolCo(GetCurrentVol(), v, volFadeSecs));
        else _inst.setVolume(v);
    }

    // --- lifecycle cleanup ---
    void OnDisable()
    {
        // Object is inactive now -> NO coroutines. Do a graceful FMOD stop.
        if (_inst.isValid())
        {
            _inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT); // relies on AHDSR/loop tail
            _inst.release();
        }
        _volCo = _pitCo = null;
    }

    void OnDestroy()
    {
        // Extra safety if OnDisable didn’t run (domain reload, etc.)
        if (_inst.isValid())
        {
            _inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _inst.release();
        }
    }

    // --- helpers ---
    float GetCurrentVol()
    {
        _inst.getVolume(out float v, out _);
        return v;
    }

    System.Collections.IEnumerator FadeVolCo(float from, float to, float t)
    {
        float a = 0f;
        while (a < t && _inst.isValid())
        {
            a += Time.deltaTime;
            _inst.setVolume(Mathf.Lerp(from, to, a / t));
            yield return null;
        }
        if (_inst.isValid()) _inst.setVolume(to);
    }

    System.Collections.IEnumerator RampPitchCo(float to, float t)
    {
        _inst.getPitch(out float from);
        float a = 0f;
        while (a < t && _inst.isValid())
        {
            a += Time.deltaTime;
            _inst.setPitch(Mathf.Lerp(from, to, a / t));
            yield return null;
        }
        if (_inst.isValid()) _inst.setPitch(to);
    }
}
