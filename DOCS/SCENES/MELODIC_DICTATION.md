🎵 Melodic Dictation Module — Sonoria

Path: /Assets/Scripts/Core/MelodicDictationController.cs
Docs: /DOCS/SCENES/MELODIC_DICTATION.md
Scene: MelodicDictation.unity

🧭 Overview

The Melodic Dictation scene is a musical ear-training game where players listen to a generated melody and reproduce it on a virtual or MIDI keyboard.
Each session adapts dynamically through Difficulty Profiles, progressing automatically after a fixed number of rounds.

🧩 Key Components
Script	Responsibility
MelodicDictationController	Central game loop controller: playback, input, scoring, and the game-over cinematic.
MelodyGenerator	Produces random melodies using mode, range, and motion constraints.
DifficultyProfile	Serialized asset defining one difficulty tier (scale degrees, motion types, etc.).
DifficultyProfileApplier	Loads and applies the active profile, updates the level display, and manages progression.
PianoKeyboardUI / PianoKeyUI	Keyboard visuals, highlighting, fade-lock behavior, and MIDI interaction.
LightningFX / CandleFlicker	Visual FX during the Game-Over cinematic.
EndScreenController	Displays final stats: score, rounds, and elapsed time.
🔁 Gameplay Flow
flowchart TD
A[Start Game] --> B[Apply Difficulty Profile]
B --> C[Generate Melody]
C --> D[PlayMelodyCo Coroutine]
D --> E[Keyboard Locked + Fade]
E --> F[Player Input Enabled]
F --> G[Evaluate Input]
G -->|Correct| H[Next Round or Level Up]
G -->|Too Many Errors| I[GameOverCinematic]
I --> J[Keyboard Hidden + FX + End Screen]

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
State	Behavior
Playback	Input locked, keys fade to low opacity
Active Play	Fully visible, MIDI + mouse input active
Game Over	Keyboard hidden (HideImmediate()), all held notes cleared

💡 Safety: calls SilenceAndClearAllHeld() before hiding to avoid collection-mod errors.

⚡ Game-Over Cinematic

Coroutine: GameOverCinematic()

Sequence:

Lock + hide keyboard

Trigger lightning storm (LightningFX.PlayStorm)

Candle flicker + extinguish with random delays

Wait for fade/storm completion

Show end screen (EndScreen.ShowDictation())

🕹️ Round Lifecycle
Method	Purpose
StartRound()	Clears data, generates new melody, applies difficulty params
PlayMelodyFromTop()	Locks keyboard, plays melody, unlocks afterward
CheckPlayerInput()	Compares player MIDI input to target melody
WinSequence()	Plays success FX, increments round counter
GameOverCinematic()	Runs storm and candle FX, then reveals score screen
📊 Level Display

Controlled by DifficultyProfileApplier

Shows “Level X” where X = profile index + 1

Auto-updates when difficulty increases

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