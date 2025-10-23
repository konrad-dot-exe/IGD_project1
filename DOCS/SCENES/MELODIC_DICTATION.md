# Melodic Dictation Scene

## Overview
- Dictation loop now layers scoring, time pressure, and end-screen stats on top of the classic "listen → echo" exercise.
- `MelodicDictationController` autogenerates a melody (via `melodyGen.Generate()` if provided, or C-major fallback), plays it through `MinisPolySynth`, then listens for octave-agnostic matches from hardware MIDI routed by `MidiProbe`.【F:Assets/Scripts/Core/MelodicDictationController.cs†L24-L121】【F:Assets/Scripts/Audio/MinisPolySynth.cs†L114-L163】【F:Assets/Scripts/Audio/MIDIProbe.cs†L8-L61】
- Each round awards points for perfect runs, penalizes mistakes/replays/slow responses, and can trigger `EndScreenController.ShowDictation` on failure.【F:Assets/Scripts/Core/MelodicDictationController.cs†L50-L210】

## Inputs & Audio Path
- Hardware MIDI keyboards are the only input source; `MidiProbe` logs connectivity, echoes notes through `synth`, and forwards events to the controller (note-off passthrough keeps envelopes clean).【F:Assets/Scripts/Audio/MIDIProbe.cs†L12-L63】
- Playback and monitoring share a single `MinisPolySynth` (`waveform`, `pulseWidth`, `attack`, `release`, etc. are tweakable). Scripted `NoteOn/NoteOff` calls reuse the same voice allocator as hardware input.【F:Assets/Scripts/Audio/MinisPolySynth.cs†L9-L178】

## Scene Composition
- **GameRoot** (or equivalent)
  - `MelodicDictationController`
    - Audio: `synth`, `playbackVelocity`, `noteDuration`, `noteGap`.
    - Timing: `preRollSeconds`.
    - Melody: `noteCount`, `baseNote`, `rangeSemitones`, optional `melodyGen` (required for current seed/timer logic—see Gotchas).
    - UI: `squaresParent`, `squarePrefab`, `replayButton`, `squareBaseColor`, `squareHighlightColor`, `squareClearedColor`, `hideClearedSquares`.
    - Scoring/UI feedback: `scoreText`, `messageText`, `messageDuration`, `messageWinColor`, `messageWrongColor`.
    - SFX: `sfxSource`, `sfxWin`, `sfxWrong`, `sfxGameOver`.
    - Tuning: `pointsPerNote`, `pointsWrongNote`, `pointsReplay`, `pointsPerSecondInput`, `maxWrongPerRound`.
    - Session: `endScreen`, `log`.
  - `MinisPolySynth` + `AudioSource` (with optional filters) handle playback and live monitoring.【F:Assets/Scripts/Core/MelodicDictationController.cs†L16-L63】【F:Assets/Scripts/Audio/MinisPolySynth.cs†L7-L108】
  - `MidiProbe` points at the synth and controller for routing/logging.【F:Assets/Scripts/Audio/MIDIProbe.cs†L8-L61】
- **Canvas (Screen Space - Overlay)**
  - `SquaresParent` (`RectTransform` + layout) receives instantiated `squarePrefab` images each round.
  - `ReplayButton` wires to `OnReplayButtonPressed`.
  - HUD elements hosting `scoreText` / `messageText` (TMP).
  - A 2D `AudioSource` for SFX can live here or on GameRoot.
- **EventSystem** (Input System UI) for button clicks.

## Gameplay & Flow
1. **Awake** – unlock cursor, wire replay button, initialise score/message UI.
2. **Start** – store `runStartTime` and immediately `StartRound()`.
3. **StartRound**
   - Randomise `melodyGen.Seed` and sync `melodyGen.Length = noteCount` before generating; fallback code randomises within `baseNote`+`rangeSemitones` and snaps to C major if no generator is present.
   - Reset counters, (re)build indicator squares, compute `roundTimeBudgetSec` from potential score versus `pointsPerSecondInput` drain, then `PlayMelodyFromTop()`.
4. **Playback coroutine** – wait `preRollSeconds`, flash each square, call `synth.NoteOn/NoteOff`, restore idle colours, then set `state = Listening`.
5. **Listening** – `MidiProbe`-driven `OnMidiNoteOn` compares pitch classes; correct entries hide or fade the square and when complete, award `pointsPerNote × melody.Count`, fire win SFX/message, and queue `WinThenNextRound()`.
6. **Mistakes** – wrong input adds `pointsWrongNote`, increments `wrongGuessesThisRound`, shows a message/SFX, and either replays (if under `maxWrongPerRound`) or `GameOver()`.
7. **Replay** – button or `R` hotkey deducts `pointsReplay` and restarts playback coroutine without resetting the countdown.
8. **Timer drain** – while Listening, `Update()` decrements `roundTimeRemainingSec` and continuously subtracts score according to `pointsPerSecondInput`; reaching zero triggers `GameOver()`.
9. **Game Over** – stop playback, optionally silence synth (helper commented), show failure message/SFX, push high score via `MainMenuController.SetDictationHighScore`, and open `endScreen` with aggregated stats.

## Scoring, Timer & Feedback Details
- Score UI is driven solely by `UpdateScoreUI()` (`Score: {score}` text via `scoreText`).【F:Assets/Scripts/Core/MelodicDictationController.cs†L170-L208】
- `ShowMessage()` toggles `messageText` for win/wrong popups with configurable colours/duration; coroutine prevents overlap.【F:Assets/Scripts/Core/MelodicDictationController.cs†L156-L190】
- Audio cues map 1:1 to state transitions (`sfxWin`, `sfxWrong`, `sfxGameOver`). Replay presses intentionally have no dedicated clip.
- Round timers scale with melody length: `roundTimeBudgetSec = (max(pointsPerNote,0) × melody.Count) / |pointsPerSecondInput|`. Setting a non-negative drain effectively grants infinite time (guard clamps to 0.001 for calculations).【F:Assets/Scripts/Core/MelodicDictationController.cs†L121-L148】【F:Assets/Scripts/Core/MelodicDictationController.cs†L198-L217】

## UI Behaviour
- `BuildSquares()` instantiates one `Image` per melody note; `ResetSquaresVisual()` re-enables/colours them before playback, and `ClearSquares()` destroys children between rounds.【F:Assets/Scripts/Core/MelodicDictationController.cs†L148-L189】
- Correct guesses either `SetActive(false)` (if `hideClearedSquares`) or recolour to `squareClearedColor`; replay resets them before the next listen cycle.【F:Assets/Scripts/Core/MelodicDictationController.cs†L74-L110】【F:Assets/Scripts/Core/MelodicDictationController.cs†L175-L189】
- Cursor is intentionally unlocked for UI interaction (`Cursor.lockState = None`).【F:Assets/Scripts/Core/MelodicDictationController.cs†L64-L78】

## Wiring Checklist
- [ ] Place `MelodicDictationController`, `MinisPolySynth`, `AudioSource`, and `MidiProbe` on the same root and assign their cross-references.
- [ ] Provide a `MelodyGenerator` asset/component if you want modal/difficulty control; set its register/mode options for the desired curriculum.
- [ ] Build a Canvas with `SquaresParent`, `ReplayButton`, TMP score/message labels, and a 2D `AudioSource` for `sfxSource`.
- [ ] Assign colour fields, `squarePrefab`, and SFX clips in the inspector.
- [ ] Keep an Input System `EventSystem` active so the button and TMP components work.
- [ ] Ensure the Unity Input System + Minis MIDI package are installed so devices appear at runtime.

## Features Newly Documented / Worth Highlighting
- **MelodyGenerator integration** – The controller now *requires* an assigned `melodyGen` to avoid null refs (seed & length sync happen every round). `MelodyGenerator` exposes modal scale selection (`ScaleMode` enum), contour presets, register limits, and leap heuristics for curriculum-driven sequences rather than random C-major.【F:Assets/Scripts/Core/MelodicDictationController.cs†L121-L147】【F:Assets/Scripts/Core/MelodyGenerator.cs†L1-L112】
- **Scoring system** – Configurable point rewards/penalties, replay tax, per-second drain, and wrong-note strike limit feeding an `EndScreenController`. None of these knobs existed in the previous documentation.【F:Assets/Scripts/Core/MelodicDictationController.cs†L43-L217】
- **Feedback layer** – TMP message banner, win/wrong/game-over SFX hooks, and run statistics captured for the shared end screen.【F:Assets/Scripts/Core/MelodicDictationController.cs†L50-L210】

## Gotchas
- `melodyGen` is dereferenced unconditionally inside `StartRound()`; leave it assigned or add a null guard before using the fallback path.
- Forgetting to wire `scoreText`, `messageText`, or `sfxSource` doesn’t break flow but silently removes feedback—double-check inspector warnings.
- Timer drain and penalties only run while `state == Listening`; changing state elsewhere without resetting `roundTimeRemainingSec` can leave stale countdown values.

## Dependencies
- **Internal** – `MelodicDictationController`, `MelodyGenerator`, `MidiProbe`, `MinisPolySynth`, `EndScreenController`, UI prefabs/labels.
- **External** – Unity Input System + Minis MIDI for hardware support, TextMeshPro for HUD labels, Unity UI for layout.
