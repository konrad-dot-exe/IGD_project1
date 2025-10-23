# Melodic Dictation Scene

## Overview
Melodic Dictation teaches players to recognize and reproduce short diatonic melodies: the controller builds a random C-major phrase, plays it back through the in-scene synth while highlighting visual steps, then listens for the learner to echo the pattern on a MIDI keyboard before automatically rolling into the next challenge.【F:Assets/Scripts/Core/MelodicDictationController.cs†L19-L88】【F:Assets/Scripts/Core/MelodicDictationController.cs†L117-L170】

## Inputs supported
- Hardware MIDI keyboards routed through the Minis package; `MidiProbe` forwards `NoteOn`/`NoteOff` events to both the synth and the dictation controller.【F:Assets/Scripts/Audio/MIDIProbe.cs†L8-L57】【F:Assets/Scripts/Audio/MIDIProbe.cs†L60-L71】
- No on-screen piano is instantiated in this scene, but the controller’s MIDI API matches the shared Piano UI scripts if a virtual keyboard is later added.【F:Assets/Scripts/Core/MelodicDictationController.cs†L47-L88】

## Scene Composition
- **GameRoot (GameObject)**
  - `MelodicDictationController` – owns gameplay flow. Inspector fields: `synth`, `playbackVelocity` (0.9), `noteDuration` (0.8s), `noteGap` (0.2s), `preRollSeconds` (1.25s), `noteCount` (6 in scene), `baseNote` (48/C3), `rangeSemitones` (12), `squaresParent`, `squarePrefab`, `replayButton`, `squareBaseColor` (cyan tint), `squareHighlightColor` (white), `squareClearedColor` (transparent), `hideClearedSquares`, `log`.【F:Assets/Scripts/Core/MelodicDictationController.cs†L11-L43】【F:Assets/Scenes/MelodicDictation.unity†L8334-L8358】
  - `MinisPolySynth` – triangle waveform polysynth with `masterGain` 0.2, `maxVoices` 10, `attack`/`release` 8; requires the colocated `AudioSource` plus the scene adds `AudioChorusFilter` and `AudioReverbFilter` for colour.【F:Assets/Scripts/Audio/MinisPolySynth.cs†L1-L115】【F:Assets/Scenes/MelodicDictation.unity†L8338-L8347】【F:Assets/Scenes/MelodicDictation.unity†L8184-L8237】
  - `MidiProbe` – assigns `synth` and `dictation` references for MIDI routing/logging.【F:Assets/Scripts/Audio/MIDIProbe.cs†L8-L28】【F:Assets/Scenes/MelodicDictation.unity†L8184-L8198】
- **Canvas (Screen Space - Overlay)** hosts UI. Children include `SquaresParent`, `ReplayBtn`, and other HUD panels; paired with `CanvasScaler` and `GraphicRaycaster` for responsiveness.【F:Assets/Scenes/MelodicDictation.unity†L1905-L1993】
- **SquaresParent (RectTransform)** – centered container with `HorizontalLayoutGroup` (spacing 12, child force expand). Disabled `Image` component keeps it invisible. Receives instantiated square images per note.【F:Assets/Scenes/MelodicDictation.unity†L12588-L12635】
- **SquarePrefab (UI Image)** – 100×100 rect with `Image` + `CanvasRenderer`. Used exclusively for melodic step indicators.【F:Assets/Prefabs/Melodic Dictation/SquarePrefab.prefab†L1-L53】
- **ReplayBtn (Button)** – anchored bottom-left, uses default button visuals; the controller registers `ReplayMelody` at runtime.【F:Assets/Scenes/MelodicDictation.unity†L12040-L12148】【F:Assets/Scripts/Core/MelodicDictationController.cs†L35-L44】
- **EventSystem** – Input System UI module so the mouse cursor can interact with buttons.【F:Assets/Scenes/MelodicDictation.unity†L9360-L9404】

## Game Flow
1. **Scene load / Awake** – controller unlocks and shows the cursor, wires the replay button click handler, and immediately triggers `StartRound`.【F:Assets/Scripts/Core/MelodicDictationController.cs†L35-L48】
2. **StartRound** – randomizes a C-major melody within `baseNote`+`rangeSemitones`, spawns matching UI squares, then calls `PlayMelodyFromTop`.【F:Assets/Scripts/Core/MelodicDictationController.cs†L103-L137】
3. **Pre-roll & playback** – coroutine waits `preRollSeconds`, then iterates the melody: highlight current square, call `synth.NoteOn`, hold for `noteDuration`, release with `NoteOff`, fade square back to `squareBaseColor`, and pause `noteGap` between steps. State switches to `Listening` once playback ends.【F:Assets/Scripts/Core/MelodicDictationController.cs†L137-L167】
4. **Listening** – while `state == Listening`, incoming `NoteOn` events are evaluated against `melody[inputIndex]` using pitch-class comparison so octave equivalents count.【F:Assets/Scripts/Core/MelodicDictationController.cs†L59-L105】
5. **Success** – correct notes hide or fade their squares, advance the index, and once all notes are cleared the controller waits 0.75s, destroys the old squares, and restarts at step 2 with a fresh melody.【F:Assets/Scripts/Core/MelodicDictationController.cs†L63-L112】【F:Assets/Scripts/Core/MelodicDictationController.cs†L167-L170】
6. **Failure** – a wrong pitch resets the index, restores all square visuals, immediately replays the same melody, and keeps the learner on the current round.【F:Assets/Scripts/Core/MelodicDictationController.cs†L85-L105】【F:Assets/Scripts/Core/MelodicDictationController.cs†L117-L137】

## Events & State Machine
- States: `Idle`, `Playing`, `Listening`; `PlayMelodyFromTop` sets `Playing`, the coroutine switches to `Listening`, and completion returns to `Idle`. Tracking fields include `List<int> melody`, `List<Image> squares`, `int inputIndex`, and `Coroutine playingCo`.【F:Assets/Scripts/Core/MelodicDictationController.cs†L45-L117】
- MIDI handling: `MidiProbe` listens to Minis `MidiDevice.onWillNoteOn`, logging and forwarding each note to `MelodicDictationController.OnMidiNoteOn`. `NoteOff` is forwarded for completeness but unused by the dictation logic.【F:Assets/Scripts/Audio/MIDIProbe.cs†L29-L71】【F:Assets/Scripts/Core/MelodicDictationController.cs†L105-L112】
- Event timeline examples:
  - **Start Round** – `StartRound` → `BuildMelody` → `BuildSquares` → `PlayMelodyFromTop` (resets visuals, starts coroutine).
  - **Right Note** – `OnMidiNoteOn` (listening) → matches pitch class → clear/hide square → increment index → if finished, `WinThenNextRound` coroutine.
  - **Wrong Note** – `OnMidiNoteOn` mismatch → reset `inputIndex` → `ResetSquaresVisual` → `ReplayMelody` (stops any playing coroutine, restarts playback).
  - **Replay button or R key** – UI click or hotkey → `ReplayMelody` → `PlayMelodyFromTop`; state returns to `Playing` until the coroutine finishes.【F:Assets/Scripts/Core/MelodicDictationController.cs†L33-L44】【F:Assets/Scripts/Core/MelodicDictationController.cs†L117-L156】

## Audio Path
- Playback uses `MinisPolySynth.NoteOn/NoteOff` with inspector-tuned `playbackVelocity`, `noteDuration`, and `noteGap`; melodies are monophonic but the synth supports up to 10 simultaneous voices for future expansion.【F:Assets/Scripts/Core/MelodicDictationController.cs†L13-L40】【F:Assets/Scripts/Audio/MinisPolySynth.cs†L11-L111】【F:Assets/Scenes/MelodicDictation.unity†L8338-L8347】
- `MinisPolySynth` generates audio in `OnAudioFilterRead` using the selected waveform (triangle by default) and envelope, then routes through the attached `AudioSource`, with optional chorus/reverb filters already on the GameRoot for additional texture.【F:Assets/Scripts/Audio/MinisPolySynth.cs†L83-L197】【F:Assets/Scenes/MelodicDictation.unity†L8184-L8237】
- Hardware MIDI also triggers the same synth via `MidiProbe`, so learners hear their input even outside scripted playback.【F:Assets/Scripts/Audio/MIDIProbe.cs†L47-L71】

## UI Behaviour
- Square indicators are instantiated each round under `SquaresParent`, inheriting layout spacing for even distribution across the canvas. Each starts with `squareBaseColor`, flashes to `squareHighlightColor` during playback, then either hides (`hideClearedSquares`) or fades to `squareClearedColor` when the learner plays the correct pitch.【F:Assets/Scripts/Core/MelodicDictationController.cs†L118-L158】【F:Assets/Scenes/MelodicDictation.unity†L12588-L12635】
- `ResetSquaresVisual` re-enables and recolors every square at round start or after mistakes, ensuring hidden elements return before a replay.【F:Assets/Scripts/Core/MelodicDictationController.cs†L144-L153】
- The replay button’s `OnClick` is wired in `Awake`; during playback extra presses restart the coroutine from the top without waiting for completion. A keyboard `R` shortcut mirrors this for quick iteration.【F:Assets/Scripts/Core/MelodicDictationController.cs†L33-L44】【F:Assets/Scripts/Core/MelodicDictationController.cs†L170-L176】
- Cursor lock is disabled so the player can operate UI elements with the mouse; keep this scene in mind when switching from FPS-focused modules.【F:Assets/Scripts/Core/MelodicDictationController.cs†L33-L38】

## How to Run / Wire
- Drop a `MelodicDictationController` in the scene and assign `synth`, `squaresParent`, `squarePrefab`, and `replayButton` references in the Inspector.
- Ensure the same GameObject hosts `MinisPolySynth` plus an `AudioSource` (with optional chorus/reverb filters) so the synth can emit audio.【F:Assets/Scripts/Audio/MinisPolySynth.cs†L5-L33】【F:Assets/Scenes/MelodicDictation.unity†L8184-L8237】
- Add `MidiProbe`, linking its `synth` and `dictation` fields to the components above for MIDI routing and monitoring.【F:Assets/Scripts/Audio/MIDIProbe.cs†L8-L28】
- Create a `Canvas` (Screen Space - Overlay) with `SquaresParent` (RectTransform + `HorizontalLayoutGroup`) and a `ReplayBtn` (`Button` + `Image` + `Text`).【F:Assets/Scenes/MelodicDictation.unity†L1905-L1993】【F:Assets/Scenes/MelodicDictation.unity†L12588-L12635】【F:Assets/Scenes/MelodicDictation.unity†L12040-L12148】
- Reference the `SquarePrefab` asset (`Assets/Prefabs/Melodic Dictation/SquarePrefab.prefab`) for the indicator visuals.【F:Assets/Prefabs/Melodic Dictation/SquarePrefab.prefab†L1-L53】
- Keep an `EventSystem` with the Input System UI module in the scene so buttons remain clickable.【F:Assets/Scenes/MelodicDictation.unity†L9360-L9404】
- Make sure the Unity Input System package is active and the Minis MIDI package is installed so devices register at runtime.【F:Assets/Scripts/Audio/MIDIProbe.cs†L12-L43】【F:Assets/Scripts/Audio/MinisPolySynth.cs†L33-L60】

## Dependencies
- **Internal** – `MelodicDictationController`, `MidiProbe`, `MinisPolySynth`, UI layout prefabs (`SquarePrefab`, Canvas hierarchy), EventSystem.
- **External** – Minis (Keijiro) MIDI package and Unity Input System (for hardware device events), Unity UI system for layout/interaction.【F:Assets/Scripts/Audio/MIDIProbe.cs†L12-L71】【F:Assets/Scripts/Audio/MinisPolySynth.cs†L33-L76】

## Gotchas / Known Issues
- Leaving `synth`, `squaresParent`, or `squarePrefab` unassigned silently skips audio or UI because the controller null-checks and returns early; double-check Inspector wiring.【F:Assets/Scripts/Core/MelodicDictationController.cs†L117-L139】
- While the melody replays, input is ignored (`state != Listening`), so live MIDI presses during playback will not count—use the replay button or wait for the listening phase.【F:Assets/Scripts/Core/MelodicDictationController.cs†L59-L72】【F:Assets/Scripts/Core/MelodicDictationController.cs†L137-L167】
- MIDI comparison ignores octaves (pitch class only); this is intentional but means learners can answer with any octave of the target pitch.【F:Assets/Scripts/Core/MelodicDictationController.cs†L72-L88】
- `hideClearedSquares` removes GameObjects during success; `ResetSquaresVisual` must be called before replaying to revive them. Avoid manual deactivation elsewhere or the controller may not restore them.【F:Assets/Scripts/Core/MelodicDictationController.cs†L63-L101】【F:Assets/Scripts/Core/MelodicDictationController.cs†L144-L153】

## Extensibility / TODO
- Parameterize scale selection (e.g., minor modes) instead of the hard-coded `ForceToCMajor` helper.【F:Assets/Scripts/Core/MelodicDictationController.cs†L154-L170】
- Expose melody length ranges or difficulty presets beyond the single `noteCount` value.
- Add metronome or count-in audio to complement the pre-roll timer.
- Track streaks/score for UI feedback and persistence between rounds.
- Allow seeding or saving generated melodies for debugging/regression testing.
- Integrate the reusable on-screen piano UI so non-MIDI users can participate.
- Support alternative playback styles (e.g., arpeggiation speed controls, swing feel).

## Quick Setup Checklist
- [ ] Add `MelodicDictationController`, `MinisPolySynth`, `AudioSource`, `MidiProbe` to a shared GameObject and wire their references.
- [ ] Create/verify a `Canvas` with `SquaresParent` (HorizontalLayoutGroup) and drop in a `ReplayBtn` button.
- [ ] Assign `SquarePrefab` and color fields on the controller for the desired look.
- [ ] Keep an `EventSystem` with Input System UI module active.
- [ ] Confirm Unity’s Input System and Minis MIDI packages are installed and a MIDI device is connected for testing.
- [ ] Play the scene and press `R` or the Replay button to verify playback before practicing input.
