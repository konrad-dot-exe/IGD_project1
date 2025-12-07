🎵 Melodic Dictation Module — Sonoria

Path: /Assets/Scripts/Core/MelodicDictationController.cs
Docs: /DOCS/SCENES/MELODIC_DICTATION.md
Scene: MelodicDictation.unity

🧭 Overview

The Melodic Dictation scene is a musical ear-training game where players listen to a generated melody and reproduce it on a virtual or MIDI keyboard.
Each session adapts dynamically through Difficulty Profiles, progressing automatically after a fixed number of rounds.

🧩 Key Components
Script Responsibility
MelodicDictationController Central game loop controller: playback, input, scoring, and the game-over cinematic.
MelodyGenerator Produces random melodies using mode, range, and motion constraints.
DifficultyProfile Serialized asset defining one difficulty tier (scale degrees, motion types, etc.).
DifficultyProfileApplier Loads and applies the active profile, updates the level display, and manages progression.
PianoKeyboardUI / PianoKeyUI Keyboard visuals, highlighting, fade-lock behavior, and MIDI interaction.
LightningFX / CandleFlicker Visual FX during the Game-Over cinematic.
EndScreenController Displays final stats: score, rounds, and elapsed time.
CameraIntro Manages camera rotation intro sequence when level starts from campaign map.
CameraAmbientMotion Adds subtle camera drift and breathing motion for visual polish.
🔁 Gameplay Flow
flowchart TD
A[Start Game] --> B[Camera Intro Sequence]
B --> C[Apply Difficulty Profile]
C --> D[Generate Melody]
D --> E[PlayMelodyCo Coroutine]
E --> F[Keyboard Locked + Fade]
F --> G[Player Input Enabled]
G --> H[Evaluate Input]
H -->|Correct| I[Next Round or Level Up]
H -->|Too Many Errors| J[GameOverCinematic]
J --> K[Keyboard Hidden + FX + End Screen]
I -->|All Levels Complete| L[Mode Completion Announcement]
L --> M[Return to Campaign Map]

🎚️ Difficulty System

Each DifficultyProfile defines musical constraints for a learning tier.

Profile Fields

mode — musical mode (Ionian, Dorian, etc.)

allowedDegrees — which scale degrees (1–7) can appear

rangeSemitones — total range from tonic

allowedMovements — allowed interval types (stepwise, thirds, etc.)

minNotes / maxNotes — melody length bounds

roundsPerLevel — number of rounds before difficulty increases

autoApplyOnStart — whether to load automatically at scene start

DifficultyProfileApplier Responsibilities

Load the profile from profiles[difficultyIndex]

Apply to both MelodicDictationController and MelodyGenerator

Update the on-screen Level display

Auto-advance after roundsPerLevel rounds

🎹 Keyboard Behavior
State Behavior
Initial State Hidden (alpha=0) at scene start
Playback Input locked, keys fade to low opacity
Active Play Fully visible, MIDI + mouse input active
Game Over Keyboard hidden (HideImmediate()), all held notes cleared

💡 Safety: calls SilenceAndClearAllHeld() before hiding to avoid collection-mod errors.
💡 Visibility: Keyboard starts hidden and immediately fades to locked opacity when round begins (prevents flash of full opacity).

⚡ Game-Over Cinematic

Coroutine: GameOverCinematic()

Sequence:

Lock + hide keyboard

Trigger lightning storm (LightningFX.PlayStorm)

Candle flicker + extinguish with random delays

Wait for fade/storm completion

Show end screen (EndScreen.ShowDictation())

🔊 Audio System

FMOD Integration:

- Uses FMOD Studio for all sound effects
- `FmodSfxPlayer`: Centralized SFX playback (win, wrong, game over, level complete, etc.)
- `FmodNoteSynth`: Note playback for melody generation with multi-sample support
- `DronePlayer`: Continuous ambient drone sound

Multi-Sample System:

- `FmodNoteSynth` supports multiple samples at different base pitches (e.g., C2, C3, C4)
- Automatically selects the closest sample to minimize pitch-shifting
- Reduces audio latency for lower notes by avoiding extreme pitch-shifting
- Configure in Inspector: enable "Use Multi Sample" and assign samples with their root MIDI notes

Audio Safety:

- Rate limiting: Lightning/thunder sounds have minimum intervals (0.1s/0.2s)
- Sound debouncing: Card slap sounds limited to 1 per 50ms
- Guard checks: `GameOver()` method prevents multiple simultaneous calls
- Instance cleanup: All EventInstances properly released on component destroy

Configuration:

- `FmodBoot.cs`: Configures FMOD system settings (channels, buffers)
- Software channels: 256 (configurable)
- Command queue size: 256KB (prevents dynamic growth warnings)

🕹️ Round Lifecycle
Method Purpose
StartRound() Clears data, generates new melody, applies difficulty params
PlayMelodyFromTop() Locks keyboard, plays melody, unlocks afterward
CheckPlayerInput() Compares player MIDI input to target melody
WinSequence() Plays success FX, increments round counter
GameOverCinematic() Runs storm and candle FX, then reveals score screen

💯 Scoring System

Score Calculation:

- Gross gain: `pointsPerNote × melodyLength` (e.g., 100 × 5 = 500)
- Penalties (deferred until round completion):
  - Time penalty: Accumulated from `pointsPerSecondInput` drain
  - Wrong note penalties: Accumulated per wrong guess
  - Replay penalties: Accumulated per melody replay
- Net gain: `grossGain - totalPenalties` (clamped to 0 minimum)
- Completion message shows net points: `"Correct! +{netGain}"`

Score Reset:

- Resets to 0 when starting a new level (`SetWinsRequiredForCampaign()`)
- Resets to 0 when restarting after game over
- Per-level tracking (not cumulative across levels)

Top Score Tracking:

- Persistent top scores stored per level in `CampaignSave`
- Updated only on successful level completion (not on game over)
- Stored persistently across game sessions

🕯️ Hitpoints System (Campaign Mode)

Wrong Guess Tracking:

- Per-level system: 6 wrong guesses allowed per level (not per round)
- Counter persists across rounds within the same level
- Resets when starting a new level or restarting after game over

Candle Visual Feedback:

- 6 candles in scene (one per wrong guess)
- 1 candle extinguished per wrong guess (with violent flicker animation)
- Candles persist across rounds within the same level
- Candles re-ignite when:
  - Starting a new level
  - Restarting after game over
- Game over cinematic extinguishes all remaining candles

Game Over:

- Triggered when `wrongGuessesThisLevel >= maxWrongPerLevel` (default: 6)
- Guard check prevents multiple `GameOver()` calls

🎬 Camera System

Camera Intro Sequence:

- Plays when level starts from campaign map
- Camera rotates from sky view (-65° X rotation) to table view (38° X rotation)
- Duration: 1 second (configurable)
- Level intro message displays during rotation (e.g., "Mixolydian — Level 4")
- Message fades in quickly, then fades out over rotation duration

Camera Ambient Motion:

- Subtle Y-axis rotation drift (oscillating, 1-2° range)
- Soft vertical "breathing" motion
- Always active by default, toggleable
- Parameters adjustable via Inspector
- Automatically waits for camera intro to complete before initializing

📊 Level Display

Controlled by DifficultyProfileApplier

Shows "Level X" where X = profile index + 1

Auto-updates when difficulty increases

🎯 Campaign Integration

Mode Completion:

- When all levels in a node are completed, shows completion announcement
- Announcement similar to mode unlock screen ("Mode Complete: [Mode Name]")
- End screen returns to campaign map (not level picker) when node is complete
- Level complete sound plays when level ends successfully (not on game over)

✅ Integration Checklist

Assign references in Inspector

MelodyGenerator, DifficultyProfileApplier, PianoKeyboardUI, LightningFX, etc.

Ensure valid array of difficulty profiles

Enable Auto Apply for starting profile

Test keyboard fade and hide transitions

Verify level progression increments correctly

⚠️ Safety Notes

Never enumerate heldKeys while modifying (use safe clear).

Disable MIDI input while keyboard is locked or hidden.

Always call SilenceAndClearAllHeld() before hiding the keyboard.
