// MelodicDictationController.cs — wired to MelodyGenerator (Rule–Prob Hybrid)
// Adds: scoring, on-canvas messages, SFX for win/wrong.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

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

        [Header("Scoring & Messaging")]
        [SerializeField] TMP_Text scoreText;           // Assign UIHud/ScoreText
        [SerializeField] TMP_Text messageText;         // Create a TMP Text on Canvas and assign here
        [SerializeField] float messageDuration = 1.2f;
        [SerializeField] Color messageWinColor = new Color(0.2f, 1f, 0.6f, 1f);
        [SerializeField] Color messageWrongColor = new Color(1f, 0.4f, 0.3f, 1f);

        [Header("SFX")] 
        [SerializeField] AudioSource sfxSource;        // Assign a 2D AudioSource on Canvas/GameRoot
        [SerializeField] AudioClip sfxWin;             // Play when full melody correct
        [SerializeField] AudioClip sfxWrong;           // Play when a note is wrong

        [Header("Debug")]
        [SerializeField] bool log = false;

        enum State { Idle, Playing, Listening }
        State state = State.Idle;

        readonly List<int> melody = new();
        readonly List<Image> squares = new();
        int inputIndex = 0;
        Coroutine playingCo;
        Coroutine messageCo;
        int score = 0;

        // Prepare cursor / UI hooks when the controller comes alive.
        void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;

            if (replayButton) replayButton.onClick.AddListener(ReplayMelody);
            UpdateScoreUI();
            if (messageText) messageText.gameObject.SetActive(false);
        }

        // Kick off the first dictation round as soon as we're running.
        void Start() => StartRound();

        // Clean up listeners that were attached in Awake.
        void OnDestroy()
        {
            if (replayButton) replayButton.onClick.RemoveListener(ReplayMelody);
        }

        // ---------- MIDI from MidiProbe ----------
        // MIDI hook invoked by MidiProbe: judges each incoming response note.
        public void OnMidiNoteOn(int midiNoteNumber, float velocity)
        {
            if (state != State.Listening) return;
            if (inputIndex < 0 || inputIndex >= melody.Count) return;

            int expected = melody[inputIndex];
            int gotPC = NormalizePitchClass(midiNoteNumber);
            int expPC = NormalizePitchClass(expected);

            if (gotPC == expPC)
            {
                // Correct → clear this square
                if (squares[inputIndex] != null)
                {
                    if (hideClearedSquares) squares[inputIndex].gameObject.SetActive(false);
                    else squares[inputIndex].color = squareClearedColor;
                }

                inputIndex++;

                // Completed sequence?
                if (inputIndex >= melody.Count)
                {
                    // Score += length, win SFX & message
                    score += melody.Count;
                    UpdateScoreUI();
                    PlaySfx(sfxWin);
                    ShowMessage($"Great! +{melody.Count}", messageWinColor);

                    state = State.Idle;
                    StartCoroutine(WinThenNextRound());
                }
            }
            else
            {
                // Wrong → score -1, message, SFX, reset visuals and replay same melody
                score -= 1;
                UpdateScoreUI();
                PlaySfx(sfxWrong);
                ShowMessage("Wrong note! -1", messageWrongColor);

                inputIndex = 0;
                ResetSquaresVisual();
                ReplayMelody();
            }
        }

        // We currently ignore note-off events (only pitch matters for grading).
        public void OnMidiNoteOff(int midiNoteNumber) { }

        // ---------- Flow ----------
        // Start a fresh melody round (new melody + UI reset + playback).
        void StartRound()
        {
            // reseed generator at round start
            if (melodyGen != null)
            {
                melodyGen.Seed = Random.Range(int.MinValue, int.MaxValue);
                if (log) Debug.Log($"[Dictation] New random seed set: {melodyGen.Seed}");
                melodyGen.Length = Mathf.Max(1, noteCount);
            }

            BuildMelody();
            BuildSquares();
            PlayMelodyFromTop();
        }

        // Replay the current melody for the player without changing state.
        void ReplayMelody()
        {
            if (playingCo != null) StopCoroutine(playingCo);
            PlayMelodyFromTop();
        }

        // Reset progress and begin coroutine playback from the first note.
        void PlayMelodyFromTop()
        {
            inputIndex = 0;
            ResetSquaresVisual();
            state = State.Playing;
            playingCo = StartCoroutine(PlayMelodyCo());
        }

        // Handles timed playback of each generated note and UI highlights.
        IEnumerator PlayMelodyCo()
        {
            if (preRollSeconds > 0f) yield return new WaitForSeconds(preRollSeconds);

            for (int i = 0; i < melody.Count; i++)
            {
                int note = melody[i];

                if (i < squares.Count && squares[i] != null && squares[i].gameObject.activeSelf)
                    squares[i].color = squareHighlightColor;

                if (synth) synth.NoteOn(note, playbackVelocity);
                yield return new WaitForSeconds(noteDuration);
                if (synth) synth.NoteOff(note);

                if (i < squares.Count && squares[i] != null && squares[i].gameObject.activeSelf && i >= inputIndex)
                    squares[i].color = squareBaseColor;

                if (i < melody.Count - 1 && noteGap > 0f)
                    yield return new WaitForSeconds(noteGap);
            }

            state = State.Listening;
        }

        // Brief pause after a win before preparing the next melody.
        IEnumerator WinThenNextRound()
        {
            yield return new WaitForSeconds(0.75f);
            ClearSquares();
            StartRound();
        }

        // ---------- Melody & UI ----------
        // Create the target melody using MelodyGenerator or fallback random logic.
        void BuildMelody()
        {
            melody.Clear();

            if (melodyGen != null)
            {
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

        // Spawn the on-screen squares that represent each note in the melody.
        void BuildSquares()
        {
            ClearSquares();
            squares.Clear();

            if (!squaresParent || !squarePrefab) return;

            for (int i = 0; i < melody.Count; i++)
            {
                var img = Instantiate(squarePrefab, squaresParent);
                img.color = squareBaseColor;
                img.gameObject.SetActive(true);
                squares.Add(img);
            }
        }

        // Restore squares to their initial color / visibility for a new attempt.
        void ResetSquaresVisual()
        {
            for (int i = 0; i < squares.Count; i++)
            {
                if (!squares[i]) continue;
                squares[i].gameObject.SetActive(true);
                squares[i].color = squareBaseColor;
            }
        }

        // Remove any existing square instances from the UI container.
        void ClearSquares()
        {
            if (!squaresParent) return;
            for (int i = squaresParent.childCount - 1; i >= 0; i--)
                Destroy(squaresParent.GetChild(i).gameObject);
        }

        // ---------- Messaging & SFX ----------
        // Display a temporary success/failure message in the HUD.
        void ShowMessage(string msg, Color color)
        {
            if (!messageText) return;
            if (messageCo != null) StopCoroutine(messageCo);
            messageCo = StartCoroutine(ShowMessageCo(msg, color));
        }
        // Coroutine that shows a message for a fixed duration.
        IEnumerator ShowMessageCo(string msg, Color color)
        {
            messageText.text = msg;
            messageText.color = color;
            messageText.gameObject.SetActive(true);
            yield return new WaitForSeconds(messageDuration);
            messageText.gameObject.SetActive(false);
        }
        // Play a one-shot feedback sound if the clip/source are set.
        void PlaySfx(AudioClip clip)
        {
            if (!sfxSource || !clip) return;
            sfxSource.PlayOneShot(clip);
        }

        // ---------- Helpers ----------
        // Refresh the score readout text.
        void UpdateScoreUI()
        {
            if (scoreText) scoreText.text = $"Score: {score}";
        }

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
        // Optional keyboard shortcut for debugging (R to replay).
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                ReplayMelody();
        }
    }
}
