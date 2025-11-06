using UnityEngine;
using UnityEngine.InputSystem;

namespace EarFPS
{
    public class MidiProbe : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] MinisPolySynth synth;                 // optional (for monitoring sound)
        [SerializeField] MelodicDictationController dictation; // <-- assign in Inspector
        [SerializeField] FmodNoteSynth fmod;         // optional: FMOD live monitor
        [SerializeField] bool routeToFmod = true;    // toggle in Inspector

        [Header("Velocity Handling")]
        [Tooltip("If true, Note-On with velocity <= 0 will be treated as Note-Off (MIDI-legal).")]
        [SerializeField] bool treatZeroVelocityNoteOnAsOff = true;

        [Tooltip("Gamma curve applied to 0..1 velocity before sending to synth. 1 = linear, <1 brightens, >1 darkens.")]
        [SerializeField] float velocityCurveGamma = 1.0f;

        [Tooltip("Optional minimum velocity floor after curve. Set to 0 to disable.")]
        [Range(0f, 1f)]
        [SerializeField] float minVelocityFloor = 0f;

        void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;

            // hook already-present devices
            foreach (var dev in InputSystem.devices)
                TryHook(dev);
        }

        void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;

            foreach (var dev in InputSystem.devices)
                TryUnhook(dev);
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added)   TryHook(device);
            if (change == InputDeviceChange.Removed) TryUnhook(device);
        }

        void TryHook(InputDevice device)
        {
            var midi = device as Minis.MidiDevice;
            if (midi == null) return;

            midi.onWillNoteOn  += OnNoteOn;
            midi.onWillNoteOff += OnNoteOff;
        }

        void TryUnhook(InputDevice device)
        {
            var midi = device as Minis.MidiDevice;
            if (midi == null) return;

            midi.onWillNoteOn  -= OnNoteOn;
            midi.onWillNoteOff -= OnNoteOff;
        }

        // ---- Callbacks from Minis ----
        void OnNoteOn(Minis.MidiNoteControl note, float velocity)
        {
            // Some keyboards send "NoteOn with velocity 0" to mean NoteOff.
            if (treatZeroVelocityNoteOnAsOff && velocity <= 0f)
            {
                OnNoteOff(note);
                return;
            }

            // Normalize, curve, and floor the velocity (0..1)
            float v = Mathf.Clamp01(velocity);
            if (velocityCurveGamma != 1f)
                v = Mathf.Pow(v, velocityCurveGamma);
            if (minVelocityFloor > 0f)
                v = Mathf.Lerp(minVelocityFloor, 1f, v);

            // (optional) monitor on local synth
            //if (synth) synth.NoteOn(note.noteNumber, v);

            // NEW: mirror to FMOD
            if (routeToFmod && fmod) fmod.NoteOn(note.noteNumber, v);

            // forward to dictation controller (use the same processed velocity)
            if (dictation) dictation.OnMidiNoteOn(note.noteNumber, v);
        }

        void OnNoteOff(Minis.MidiNoteControl note)
        {
            //if (synth) synth.NoteOff(note.noteNumber);
            if (routeToFmod && fmod) fmod.NoteOff(note.noteNumber);  
            if (dictation) dictation.OnMidiNoteOff(note.noteNumber);
        }
    }
}
