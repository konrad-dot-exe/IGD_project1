## Sonoria – Music Theory Core Services (Phase 1 Spec)

0. Purpose of this doc
   This document describes a small, reusable music-theory “kernel” for Sonoria and the first refactor step (Phase 1): extracting and consolidating pitch/key/scale helpers.
   Cursor’s job for this phase:
   Create a clean MusicTheory layer (pure C#; no Unity-specific types).
   Move or wrap existing key/scale logic into that layer.
   Do it incrementally (one or two helpers at a time), with local tests/validation before larger refactors.

1. Current context (what already exists)
   Sonoria already has:
   A robust Melodic Dictation module with:

MelodicDictationController
MelodyGenerator
DifficultyProfile + DifficultyProfileApplier
PianoKeyboardUI and supporting classes

MELODIC_DICTATION

A campaign/progression system built with ScriptableObjects and JSON save data (DictationLevel, ModeNode, DictationCampaign, CampaignSave, CampaignService).
CAMPAIGN

A pluggable LLM integration layer:

ILLMProvider interface
OpenAIProvider
Orchestrator components that can already interpret musical text (“C Dorian”, etc.) and pass structured results to Unity.
LLM_CHAT

Right now, scale/key logic is scattered across classes like MelodyGenerator, DifficultyProfileApplier, and any helper that converts between modes, degrees, and concrete notes.
Phase 1 will not change gameplay, campaign behavior, or LLM behavior. It only reorganizes theory helpers.

2. Target architecture – “Theory Kernel”
   Introduce a new namespace & folder:
   Assets/Scripts/MusicTheory/
   TheoryKey.cs
   TheoryScale.cs
   TheoryPitch.cs
   (later: TheoryChord.cs, TheoryRhythm.cs)
   General rules
   Pure C# logic. No MonoBehaviours, no direct scene references, no UnityEngine types except maybe Debug.Log.
   Functions should be deterministic and side-effect free (other than logging).
   All music systems (Melodic Dictation, future chord tools, LLM modules) should call into this layer for key/scale facts instead of reimplementing them.

2.1 TheoryPitch
Responsibility: low-level mapping between note names, pitch classes, and MIDI.
Tentative API
namespace Sonoria.MusicTheory
{
public enum Accidental { DoubleFlat, Flat, Natural, Sharp, DoubleSharp }

    public readonly struct NoteName
    {
        public char Letter;        // 'A'..'G'
        public Accidental Accidental;
        public int Octave;         // MIDI-style (-1..9, or 0..8)
    }

    public static class TheoryPitch
    {
        public static int ToMidi(NoteName note);
        public static NoteName FromMidi(int midiNote, bool preferSharps = true);

        public static int PitchClassFromMidi(int midiNote);           // 0..11
        public static int PitchClassFromNoteName(NoteName note);      // 0..11
    }

}

Phase 1 constraint:
Don’t aggressively refactor everything to use NoteName yet; start by centralizing MIDI ↔ pitch-class logic where it’s currently duplicated.
2.2 TheoryKey
Responsibility: represent a key (tonic + mode) and provide basic utilities.
namespace Sonoria.MusicTheory
{
public enum ScaleMode
{
Ionian,
Dorian,
Phrygian,
Lydian,
Mixolydian,
Aeolian,
Locrian
// Later: melodic minor modes, etc.
}

    public readonly struct TheoryKey
    {
        public NoteName Tonic { get; }
        public ScaleMode Mode { get; }

        public TheoryKey(NoteName tonic, ScaleMode mode);
    }

    public static class TheoryKeyUtils
    {
        public static TheoryKey FromString(string name);
        // e.g., "C major", "A minor", "D Dorian" – optional later

        public static int GetTonicPitchClass(TheoryKey key);
    }

}

For Phase 1 we can keep it minimal: tonic + mode + tonic pitch-class.
2.3 TheoryScale
Responsibility: given a TheoryKey, answer “what pitch classes/degrees belong to this scale?”
namespace Sonoria.MusicTheory
{
public static class TheoryScale
{
// Interval patterns are internal (Ionian: 2-2-1-2-2-2-1, etc.)

        /// Returns 7 pitch classes (0..11) for the diatonic scale of the key.
        public static int[] GetDiatonicPitchClasses(TheoryKey key);

        /// Returns pitch class of the given scale degree (1..7) in the key.
        public static int GetDegreePitchClass(TheoryKey key, int degree);

        /// True if the given MIDI note is diatonic in the key.
        public static bool IsNoteInScale(TheoryKey key, int midiNote);

        /// Convenience: compute MIDI note for degree + octave.
        public static int GetMidiForDegree(TheoryKey key, int degree, int octave);
    }

}

Internally, this should encapsulate the mode → interval pattern mapping that MelodyGenerator currently knows how to apply.
MELODIC_DICTATION

3. Phase 1 tasks for Cursor
   Task 0 – Safety guardrails
   Do not change signatures of public gameplay scripts yet (MelodicDictationController, CampaignService, etc.).
   Do not change LLM integration code in this phase.
   LLM_CHAT
   Make all new helpers opt-in: first add them, then change one caller at a time.

Task 1 – Create MusicTheory folder + stubs
Add Assets/Scripts/MusicTheory/ folder.

Create the three static classes (with namespaces) as empty shells:

TheoryPitch

TheoryKeyUtils

TheoryScale

Add minimal placeholder implementations (throw NotImplementedException) so the project compiles and we can fill in behavior step by step.

Task 2 – Extract existing scale pattern logic into TheoryScale
Goal: centralize diatonic mode interval patterns in TheoryScale.
Steps:
Search for any code that:

computes mode interval patterns,
maps ScaleMode → semitone intervals,
or builds diatonic scale note lists (likely inside MelodyGenerator, DifficultyProfileApplier, or related music scripts).
MELODIC_DICTATION

Move the data (interval arrays / patterns) into TheoryScale as private static readonly arrays, e.g.:

private static readonly int[] IonianSteps = { 2,2,1,2,2,2,1 };

Implement GetDiatonicPitchClasses(TheoryKey key) in terms of:

TheoryKeyUtils.GetTonicPitchClass(key)
chosen mode step pattern.

Implement GetDegreePitchClass(TheoryKey key, int degree) as “sum steps up to degree-1, mod 12”.

Implement IsNoteInScale by:

PitchClassFromMidi(midiNote) (temp implementation if TheoryPitch is not ready),
checking membership in GetDiatonicPitchClasses.

Testing / confirmation
Add a temporary editor or Debug test (or simple C# test) that logs the pitch classes for a few keys/modes and compares them to known sequences (C Ionian, D Dorian, A Aeolian etc.).

Ensure no behavior changes in Melodic Dictation yet; just confirm the helpers work.

Task 3 – Introduce TheoryKey into existing code (light integration)
Goal: let music systems construct a TheoryKey instead of manually juggling tonic + mode where it’s convenient.
Steps:
Identify where mode + tonic are already grouped conceptually (e.g., wherever ScaleMode and a root note/key are passed together).
MELODIC_DICTATION
For one such location (start with the least risky, e.g., a helper or editor script), replace the pair with a TheoryKey instance.
Use TheoryScale.GetDegreePitchClass or GetDiatonicPitchClasses inside that location instead of any local interval logic.
Verify:

the build still passes,
Melodic Dictation still behaves exactly as before in basic tests (play through one or two levels).

Important: only refactor one or two call sites in this step. Once stable, more can be migrated later.

Task 4 – Implement minimal TheoryPitch
Goal: centralize MIDI ↔ pitch-class conversion first; more advanced note-spelling can come later.
Steps:
Implement:

public static int PitchClassFromMidi(int midiNote)
=> ((midiNote % 12) + 12) % 12;

If the project already has pitch-class logic, swap that internal implementation to call TheoryPitch.PitchClassFromMidi instead of re-encoding the same formula.
Leave NoteName and full note-spelling as simple or unimplemented for now; we just need pitch-class support to stabilize TheoryScale.

4. Style & testing guidance for Cursor
   Small steps only.
   After each helper is wired up, run the game and test:

starting the Melodic Dictation scene,
generating a few melodies,
confirming that notes are in the right mode/scale.

Prefer wrappers over rewrites.
If a script currently has its own logic, consider:

adding a small wrapper that calls the new helper,
or delegating to the new helper inside the old function,
rather than inlining new logic everywhere.

Keep public APIs stable in gameplay-facing scripts until the helpers are clearly correct.

Log & comment.
Where behavior is subtle (e.g., Dorian vs Aeolian degree mapping), add short comments referencing TheoryScale so future work (Phase 2: chords) can build on it confidently.

5. Exit criteria for Phase 1
   Phase 1 is “done enough” when:
   TheoryScale reliably returns correct pitch classes for all existing modes used in Sonoria (Ionian, Dorian, Phrygian, Lydian, Mixolydian, Aeolian, Locrian).
   MELODIC_DICTATION

Core music systems (at least MelodyGenerator) use TheoryScale for diatonic pitch decisions instead of hardcoded patterns.

Melodic Dictation still works as before (same behavior from a player’s perspective).

The new MusicTheory namespace compiles cleanly and has no Unity-scene dependencies.

Once this is in place, Phase 2 (chord/progression helpers) can reuse the same TheoryKey/TheoryScale foundation without more structural changes.
