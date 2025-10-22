using UnityEngine;
using UnityEngine.InputSystem;

namespace EarFPS
{
    public class MidiProbe : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] MinisPolySynth synth;                       // optional (for monitoring sound)
        [SerializeField] MelodicDictationController dictation;       // <-- assign in Inspector

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

            Debug.Log($"[MIDI] Connected: {midi.displayName}");

            midi.onWillNoteOn  += OnNoteOn;
            midi.onWillNoteOff += OnNoteOff;
            // midi.onWillPitchBend    += OnPitchBend;
            // midi.onWillControlChange += OnCC;
        }

        void TryUnhook(InputDevice device)
        {
            var midi = device as Minis.MidiDevice;
            if (midi == null) return;

            midi.onWillNoteOn  -= OnNoteOn;
            midi.onWillNoteOff -= OnNoteOff;
            // midi.onWillPitchBend    -= OnPitchBend;
            // midi.onWillControlChange -= OnCC;

            Debug.Log($"[MIDI] Disconnected: {midi.displayName}");
        }

        // ---- Callbacks from Minis ----
        void OnNoteOn(Minis.MidiNoteControl note, float velocity)
        {
            Debug.Log($"[MIDI] NoteOn  {note.noteNumber}  vel={velocity:0.00}");

            // (optional) monitor on local synth
            if (synth) synth.NoteOn(note.noteNumber, velocity);

            // forward to dictation controller
            if (dictation) dictation.OnMidiNoteOn(note.noteNumber, velocity);
        }

        void OnNoteOff(Minis.MidiNoteControl note)
        {
            Debug.Log($"[MIDI] NoteOff {note.noteNumber}");

            if (synth) synth.NoteOff(note.noteNumber);
            if (dictation) dictation.OnMidiNoteOff(note.noteNumber);
        }

        // Optional extras if you want later:
        // void OnPitchBend(float value) { }
        // void OnCC(int cc, float value) { }
    }
}