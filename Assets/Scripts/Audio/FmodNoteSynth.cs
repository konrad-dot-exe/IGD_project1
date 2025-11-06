using System.Collections.Generic;
using UnityEngine;
using FMOD.Studio;
using FMODUnity;

[DisallowMultipleComponent]
public class FmodNoteSynth : MonoBehaviour
{
    [Header("FMOD")]
    [Tooltip("Assign event:/notes/sampler_note (single-sample w/ short attack & release)")]
    public EventReference noteEvent;

    [Header("Playback")]
    [Range(0,127)] public int rootMidi = 60;         // MIDI note that the sample represents (e.g., C4=60)
    [Range(0f,1f)] public float defaultVelocity = 0.9f;
    public bool positional3D = true;                 // if false, plays 2D (no spatialization)

    // simple polyphony: one instance per MIDI note
    private readonly Dictionary<int, EventInstance> _voices = new();

    public void NoteOn(int midi, float velocity = -1f)
    {
        if (_voices.ContainsKey(midi)) return;       // already sounding

        var inst = RuntimeManager.CreateInstance(noteEvent);
        if (!inst.isValid()) return;

        // optional 3D attach
        // if (positional3D)
        //     RuntimeManager.AttachInstanceToGameObject(inst, transform, GetComponent<Rigidbody>());

        // pitch ratio from semitone offset
        float semitone = Mathf.Clamp(midi, 0, 127) - rootMidi;
        float pitchRatio = Mathf.Pow(2f, semitone / 12f);
        inst.setPitch(pitchRatio);

        // quick-and-dirty velocity to volume (0..1). If your event has its own dynamics, you can remove this.
        float v = (velocity >= 0f) ? Mathf.Clamp01(velocity) : defaultVelocity;
        inst.setVolume(v);

        inst.start();
        _voices[midi] = inst;
    }

    public void NoteOff(int midi)
    {
        if (!_voices.TryGetValue(midi, out var inst)) return;
        if (inst.isValid())
        {
            inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);  // uses the event’s release tail
            inst.release();
        }
        _voices.Remove(midi);
    }

    public void PlayOnce(int midi, float velocity, float durationSeconds)
    {
        NoteOn(midi, velocity);
        StartCoroutine(ReleaseAfter(midi, durationSeconds));
    }

    System.Collections.IEnumerator ReleaseAfter(int midi, float s)
    {
        yield return new WaitForSeconds(s);
        NoteOff(midi);
    }

    public void StopAll()
    {
        foreach (var kv in _voices)
        {
            if (kv.Value.isValid())
            {
                kv.Value.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                kv.Value.release();
            }
        }
        _voices.Clear();
    }
}
