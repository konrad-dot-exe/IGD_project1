// MelodicDictationController.cs — wired to MelodyGenerator (Rule–Prob Hybrid)
// Adds automatic reseeding of MelodyGenerator each round (via public Seed property).

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace EarFPS
{
    public class MelodicDictationController : MonoBehaviour
    {
        [Header("Audio / Synth")]
        [SerializeField] MinisPolySynth synth;
        [Range(0f, 1f)] public float playbackVelocity = 0.9f;
        public float noteDuration = 0.8f;
        public float noteGap = 0.2f;

        [Header("Pre-roll")]
        public float preRollSeconds = 1.25f;

        [Header("Melody Settings")]
        public int noteCount = 5;
        [Tooltip("Legacy base/range are only used if MelodyGenerator is not assigned.")]
        public int baseNote = 48;        // C3
        public int rangeSemitones = 12;  // one octave

        [Header("Generator (optional)")]
        [Tooltip("If assigned, this controller will call MelodyGenerator.Generate() and ignore baseNote/rangeSemitones.")]
        [SerializeField] MelodyGenerator melodyGen;

        [Header("UI")]
        [SerializeField] RectTransform squaresParent;
        [SerializeField] Image squarePrefab;
        [SerializeField] Button replayButton;
        [SerializeField] Color squareBaseColor = new Color(0, 1, 1, 0.35f);
        [SerializeField] Color squareHighlightColor = new Color(0, 1, 1, 0.85f);
        [SerializeField] Color squareClearedColor = new Color(0, 0, 0, 0.0f);
        [Tooltip("If true, correct notes will hide the square (SetActive false). If false, they will fade to squareClearedColor.")]
        [SerializeField] bool hideClearedSquares = true;

        [Header("Debug")]
        [SerializeField] bool log = false;

        enum State { Idle, Playing, Listening }
        State state = State.Idle;

        readonly List<int> melody = new();
        readonly List<Image> squares = new();
        int inputIndex = 0;
        Coroutine playingCo;

        void Awake()
        {
            // This is a UI-driven mini-game, so ensure the mouse cursor is usable when the
            // scene loads and hook up button callbacks before any gameplay begins.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;

            if (replayButton) replayButton.onClick.AddListener(ReplayMelody);
        }

        void Start() => StartRound();

        void OnDestroy()
        {
            // Avoid lingering event handlers if the component is destroyed while the app is
            // still running (e.g. domain reloads inside the editor).
            if (replayButton) replayButton.onClick.RemoveListener(ReplayMelody);
        }

        // ---------- MIDI from MidiProbe ----------
        public void OnMidiNoteOn(int midiNoteNumber, float velocity)
        {
            if (state != State.Listening) return;

            if (inputIndex < 0 || inputIndex >= melody.Count) return;

            // Compare only the pitch class so that users can answer in any octave.
            int expected = melody[inputIndex];
            int gotPC = NormalizePitchClass(midiNoteNumber);
            int expPC = NormalizePitchClass(expected);

            if (gotPC == expPC)
            {
                // Mark the square as completed so the player gets immediate feedback.
                if (squares[inputIndex] != null)
                {
                    if (hideClearedSquares) squares[inputIndex].gameObject.SetActive(false);
                    else squares[inputIndex].color = squareClearedColor;
                }

                inputIndex++;

                if (inputIndex >= melody.Count)
                {
                    // Full melody was answered correctly: show win feedback then launch a new round.
                    state = State.Idle;
                    StartCoroutine(WinThenNextRound());
                }
            }
            else
            {
                // Wrong answer: restart the attempt from the top to keep the exercise consistent.
                inputIndex = 0;
                ResetSquaresVisual();
                ReplayMelody();
            }
        }

        public void OnMidiNoteOff(int midiNoteNumber) { }

        // ---------- Flow ----------
        void StartRound()
        {
            // reseed generator at round start
            if (melodyGen != null)
            {
                melodyGen.Seed = Random.Range(int.MinValue, int.MaxValue);
                if (log) Debug.Log($"[Dictation] New random seed set: {melodyGen.Seed}");

                // Optional: keep generator length in sync with noteCount
                melodyGen.Length = Mathf.Max(1, noteCount);
            }

            BuildMelody();
            BuildSquares();
            PlayMelodyFromTop();
        }

        void ReplayMelody()
        {
            // Only one playback coroutine should run at a time so restart cleanly.
            if (playingCo != null) StopCoroutine(playingCo);
            PlayMelodyFromTop();
        }

        void PlayMelodyFromTop()
        {
            // Reset listening state/UI before handing control to the playback coroutine.
            inputIndex = 0;
            ResetSquaresVisual();
            state = State.Playing;
            playingCo = StartCoroutine(PlayMelodyCo());
        }

        IEnumerator PlayMelodyCo()
        {
            // Allow a configurable count-in so players can prepare.
            if (preRollSeconds > 0f) yield return new WaitForSeconds(preRollSeconds);

            for (int i = 0; i < melody.Count; i++)
            {
                int note = melody[i];

                // Light up the current square to show progress during playback.
                if (i < squares.Count && squares[i] != null && squares[i].gameObject.activeSelf)
                    squares[i].color = squareHighlightColor;

                if (synth) synth.NoteOn(note, playbackVelocity);
                yield return new WaitForSeconds(noteDuration);
                if (synth) synth.NoteOff(note);

                // Return the square to its idle colour once the note has sounded.
                if (i < squares.Count && squares[i] != null && squares[i].gameObject.activeSelf && i >= inputIndex)
                    squares[i].color = squareBaseColor;

                // Add a short rest between notes so successive pitches are easier to identify.
                if (i < melody.Count - 1 && noteGap > 0f)
                    yield return new WaitForSeconds(noteGap);
            }

            // Playback finished: start accepting answers.
            state = State.Listening;
        }

        IEnumerator WinThenNextRound()
        {
            // Give the player a brief celebration/confirmation before clearing UI and starting again.
            yield return new WaitForSeconds(0.75f);
            ClearSquares();
            StartRound();
        }

        // ---------- Melody & UI ----------
        void BuildMelody()
        {
            melody.Clear();

            if (melodyGen != null)
            {
                // Use the procedural generator so difficulty/contour settings are respected.
                var gen = melodyGen.Generate();
                for (int i = 0; i < gen.Count; i++) melody.Add(gen[i].midi);
            }
            else
            {
                // Legacy fallback: random C-major in baseNote..baseNote+range
                for (int i = 0; i < noteCount; i++)
                {
                    int semis = Random.Range(0, rangeSemitones + 1);
                    int note = baseNote + semis;
                    note = ForceToCMajor(note);
                    melody.Add(note);
                }
            }
        }

        void BuildSquares()
        {
            ClearSquares();
            squares.Clear();

            if (!squaresParent || !squarePrefab) return;

            for (int i = 0; i < melody.Count; i++)
            {
                // Squares act as progress indicators; spawn one per note in the melody.
                var img = Instantiate(squarePrefab, squaresParent);
                img.color = squareBaseColor;
                img.gameObject.SetActive(true);
                squares.Add(img);
            }
        }

        void ResetSquaresVisual()
        {
            for (int i = 0; i < squares.Count; i++)
            {
                if (!squares[i]) continue;
                // Ensure each square is visible and in the base colour before replaying or retrying.
                squares[i].gameObject.SetActive(true);
                squares[i].color = squareBaseColor;
            }
        }

        void ClearSquares()
        {
            if (!squaresParent) return;
            // Destroy any previous round's UI elements so we do not accumulate stale children.
            for (int i = squaresParent.childCount - 1; i >= 0; i--)
                Destroy(squaresParent.GetChild(i).gameObject);
        }

        // ---------- Helpers ----------
        static int NormalizePitchClass(int note) => ((note % 12) + 12) % 12;

        static int ForceToCMajor(int midiNote)
        {
            int pc = NormalizePitchClass(midiNote);
            int[] scale = { 0, 2, 4, 5, 7, 9, 11 };
            if (System.Array.IndexOf(scale, pc) >= 0) return midiNote;

            for (int k = 1; k <= 6; k++)
            {
                if (System.Array.IndexOf(scale, NormalizePitchClass(midiNote + k)) >= 0) return midiNote + k;
                if (System.Array.IndexOf(scale, NormalizePitchClass(midiNote - k)) >= 0) return midiNote - k;
            }
            return midiNote;
        }

        // Optional hotkey to replay during testing
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                ReplayMelody();
        }
    }
}
