using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Minis;

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class MinisPolySynth : MonoBehaviour
{
    public enum Waveform { Sine, Square, Saw, Triangle, Noise }

    [Header("Oscillator")]
    public Waveform waveform = Waveform.Sine;
    [Range(0.01f, 0.99f)]
    public float pulseWidth = 0.5f;          // square duty cycle

    [Header("Tone")]
    [Range(0f, 1f)] public float masterGain = 0.2f;
    [Tooltip("Maximum simultaneous notes.")]
    public int maxVoices = 10;

    [Header("Envelope")]
    public float attack = 8f;                // amp rise rate (units per second)
    public float release = 8f;               // amp fall rate (units per second)

    [Header("Debug")]
    public bool logVoices = false;

    private class Voice
    {
        public int note = -1;
        public float freq;
        public float targetAmp;
        public float amp;
        public double phase;
        public bool active => amp > 0.0001f || targetAmp > 0f;
    }

    private Voice[] voices;
    private int sampleRate;

    // PRNG state for audio-thread noise (LCG)
    private uint noiseState = 0x1234567u;

    // Cache sample rate and allocate voice pool during initialization.
    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
        EnsureVoices();
    }

    // When enabled, subscribe to MIDI devices and ensure state is valid.
    void OnEnable()
    {
        EnsureVoices();

        foreach (var dev in InputSystem.devices.OfType<MidiDevice>())
            Subscribe(dev);

        InputSystem.onDeviceChange += OnDeviceChange;
    }

    // Unregister device callbacks to avoid leaks when disabled.
    void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        foreach (var dev in InputSystem.devices.OfType<MidiDevice>())
            Unsubscribe(dev);
    }

    // Make sure we have the correct number of voice objects and a valid sample rate.
    void EnsureVoices()
    {
        int count = Mathf.Max(1, maxVoices);
        if (voices == null || voices.Length != count)
        {
            voices = new Voice[count];
            for (int i = 0; i < count; i++) voices[i] = new Voice();
        }
        if (sampleRate <= 0) sampleRate = AudioSettings.outputSampleRate;
    }

    // Respond to MIDI devices being added or removed while running.
    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        var md = device as MidiDevice;
        if (md == null) return;
        if (change == InputDeviceChange.Added) Subscribe(md);
        else if (change == InputDeviceChange.Removed) Unsubscribe(md);
    }

    // Attach our handlers to a specific MIDI device.
    void Subscribe(MidiDevice dev)
    {
        dev.onWillNoteOn  += HandleNoteOn;
        dev.onWillNoteOff += HandleNoteOff;
    }

    // Detach MIDI callbacks from a device.
    void Unsubscribe(MidiDevice dev)
    {
        dev.onWillNoteOn  -= HandleNoteOn;
        dev.onWillNoteOff -= HandleNoteOff;
    }

    // -------- MIDI --------
    // Allocate/retune a voice when a MIDI note-on arrives from hardware.
    void HandleNoteOn(MidiNoteControl note, float velocity)
    {
        EnsureVoices();
        float amp = Mathf.Clamp01(velocity);
        float freq = MidiToFreq(note.noteNumber);

        Voice v = FindFreeVoice();
        v.note = note.noteNumber;
        v.freq = freq;
        v.targetAmp = amp;
        if (logVoices) Debug.Log($"NoteOn {note.noteNumber} -> voice assigned");
    }

    // Begin envelope release for the voice that matches the MIDI note-off.
    void HandleNoteOff(MidiNoteControl note)
    {
        if (voices == null) return;
        for (int i = 0; i < voices.Length; i++)
        {
            var v = voices[i];
            if (v.note == note.noteNumber)
            {
                v.targetAmp = 0f; // release
                if (logVoices) Debug.Log($"NoteOff {note.noteNumber} -> release");
                break;
            }
        }
    }

    // Find an inactive voice slot (or steal the first one if all are busy).
    Voice FindFreeVoice()
    {
        for (int i = 0; i < voices.Length; i++)
            if (!voices[i].active) return voices[i];
        return voices[0]; // simple voice-steal
    }

    // -------- AUDIO --------
    // Unity audio callback: renders mixed samples for all active voices.
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (voices == null || voices.Length == 0)
        {
            for (int i = 0; i < data.Length; i++) data[i] = 0f;
            return;
        }

        float stepUp = attack / sampleRate;
        float stepDn = release / sampleRate;
        float pw = Mathf.Clamp(pulseWidth, 0.01f, 0.99f);

        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = 0f;

            for (int vi = 0; vi < voices.Length; vi++)
            {
                var v = voices[vi];
                if (!v.active) continue;

                // Envelope
                if (v.amp < v.targetAmp) v.amp = Mathf.Min(v.amp + stepUp, v.targetAmp);
                else                     v.amp = Mathf.Max(v.amp - stepDn, v.targetAmp);
                if (v.amp < 0.0001f) continue;

                // Phase step
                double inc = (2.0 * Mathf.PI * v.freq) / sampleRate;
                v.phase += inc;
                if (v.phase >= 2.0 * Mathf.PI) v.phase -= 2.0 * Mathf.PI;

                // Oscillator
                float osc = Osc(waveform, v.phase, pw, ref noiseState);
                sample += osc * v.amp;
            }

            sample *= masterGain;

            for (int ch = 0; ch < channels; ch++)
                data[i + ch] = sample;
        }
    }

    // Simple oscillator set
    // Generate a single oscillator sample for the requested waveform.
    static float Osc(Waveform wf, double phase, float pulseWidth, ref uint noiseState)
    {
        const float TWO_PI = 2f * Mathf.PI;
        switch (wf)
        {
            case Waveform.Sine:
                return Mathf.Sin((float)phase);

            case Waveform.Saw:
            {
                // Map phase [0..2pi) to saw [-1..1]
                float norm = (float)(phase / (2.0 * Mathf.PI)); // 0..1
                return norm * 2f - 1f;
            }

            case Waveform.Square:
            {
                float edge = (float)(pulseWidth * TWO_PI);
                return (phase % (2.0 * Mathf.PI) < edge) ? 1f : -1f;
            }

            case Waveform.Triangle:
            {
                // Derive from saw for speed
                float norm = (float)(phase / (2.0 * Mathf.PI)); // 0..1
                float saw = norm * 2f - 1f;                     // -1..1
                float tri = 2f * Mathf.Abs(saw) - 1f;           // -1..1
                // Flip to match common tri shape (optional)
                return -tri;
            }

            case Waveform.Noise:
            default:
            {
                // Fast LCG, normalized to [-1..1]
                noiseState = 1664525u * noiseState + 1013904223u;
                //uint bits = (noiseState >> 9) | 0x3F800000u;   // 23-bit mantissa trick
                //float f = Mathf.FloatToHalf(bits);             // not available everywhere; fallback below
                // Fallback (portable):
                float rnd = ((noiseState & 0x00FFFFFFu) / 8388607.5f) - 1f; // [-1..1]
                return rnd;
            }
        }
    }

    // Convert MIDI pitch numbers to Hertz.
    public static float MidiToFreq(int midiNote)
    {
        return 440f * Mathf.Pow(2f, (midiNote - 69) / 12f);
    }

    // --- Public API for scripted playback (dictation, UI buttons, etc.) ---

    /// <summary>Start a note with given MIDI number and velocity (0..1).</summary>
    public void NoteOn(int midiNote, float velocity = 1f)
    {
        EnsureVoices();
        float amp = Mathf.Clamp01(velocity);
        float freq = MidiToFreq(midiNote);

        var v = FindFreeVoice();
        v.note = midiNote;
        v.freq = freq;
        v.targetAmp = amp;
    }

    /// <summary>Release the note with this MIDI number (if currently active).</summary>
    public void NoteOff(int midiNote)
    {
        if (voices == null) return;
        for (int i = 0; i < voices.Length; i++)
            if (voices[i].note == midiNote)
                voices[i].targetAmp = 0f; // release envelope
    }

    /// <summary>Convenience: play a note for a fixed duration, then release.</summary>
    public void PlayOnce(int midiNote, float velocity, float durationSeconds)
    {
        NoteOn(midiNote, velocity);
        StartCoroutine(ReleaseAfter(midiNote, durationSeconds));
    }

    private System.Collections.IEnumerator ReleaseAfter(int midiNote, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        NoteOff(midiNote);
    }

    /// <summary>Silence everything immediately (optional utility).</summary>
    public void StopAll()
    {
        if (voices == null) return;
        for (int i = 0; i < voices.Length; i++)
            voices[i].targetAmp = 0f;
    }
}
