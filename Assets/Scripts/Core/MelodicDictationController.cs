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
        public int baseNote = 48;        // C3
        public int rangeSemitones = 12;  // one octave

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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;

            if (replayButton) replayButton.onClick.AddListener(ReplayMelody);
        }

        void Start() => StartRound();

        void OnDestroy()
        {
            if (replayButton) replayButton.onClick.RemoveListener(ReplayMelody);
        }

        // ---------- MIDI from MidiProbe ----------
        public void OnMidiNoteOn(int midiNoteNumber, float velocity)
        {
            if (state != State.Listening)
            {
                if (log) Debug.Log($"[Dictation] Ignored note {midiNoteNumber} – state={state}");
                return;
            }

            if (inputIndex < 0 || inputIndex >= melody.Count)
            {
                if (log) Debug.LogWarning("[Dictation] inputIndex out of range.");
                return;
            }

            int expected = melody[inputIndex];
            int gotPC = NormalizePitchClass(midiNoteNumber);
            int expPC = NormalizePitchClass(expected);

            if (log) Debug.Log($"[Dictation] Got {midiNoteNumber}({gotPC}) expected {expected}({expPC}) at idx={inputIndex}");

            if (gotPC == expPC)
            {
                // Correct → clear this square
                if (squares[inputIndex] != null)
                {
                    if (hideClearedSquares)
                        squares[inputIndex].gameObject.SetActive(false);
                    else
                        squares[inputIndex].color = squareClearedColor;
                }

                inputIndex++;

                // Completed sequence?
                if (inputIndex >= melody.Count)
                {
                    state = State.Idle;
                    if (log) Debug.Log("[Dictation] ROUND COMPLETE");
                    StartCoroutine(WinThenNextRound());
                }
            }
            else
            {
                // Wrong → reset visual and replay same melody
                if (log) Debug.Log("[Dictation] WRONG NOTE → reset and replay");
                inputIndex = 0;
                ResetSquaresVisual();   // also re-enables squares if hidden
                ReplayMelody();
            }
        }

        public void OnMidiNoteOff(int midiNoteNumber) { /* not used */ }

        // ---------- Flow ----------
        void StartRound()
        {
            BuildMelody();
            BuildSquares();
            PlayMelodyFromTop();     // sets state correctly
        }

        void ReplayMelody()
        {
            if (playingCo != null) StopCoroutine(playingCo);
            PlayMelodyFromTop();
        }

        void PlayMelodyFromTop()
        {
            inputIndex = 0;
            ResetSquaresVisual();
            state = State.Playing;
            playingCo = StartCoroutine(PlayMelodyCo());
        }

        IEnumerator PlayMelodyCo()
        {
            if (preRollSeconds > 0f) yield return new WaitForSeconds(preRollSeconds);

            for (int i = 0; i < melody.Count; i++)
            {
                int note = melody[i];

                // highlight
                if (i < squares.Count && squares[i] != null && squares[i].gameObject.activeSelf)
                    squares[i].color = squareHighlightColor;

                // sound
                if (synth) synth.NoteOn(note, playbackVelocity);
                yield return new WaitForSeconds(noteDuration);
                if (synth) synth.NoteOff(note);

                // return to base unless player already cleared it
                if (i < squares.Count && squares[i] != null && squares[i].gameObject.activeSelf && i >= inputIndex)
                    squares[i].color = squareBaseColor;

                if (i < melody.Count - 1 && noteGap > 0f)
                    yield return new WaitForSeconds(noteGap);
            }

            state = State.Listening;
            if (log) Debug.Log("[Dictation] Now LISTENING");
        }

        IEnumerator WinThenNextRound()
        {
            yield return new WaitForSeconds(0.75f);
            ClearSquares();
            StartRound();
        }

        // ---------- Melody & UI ----------
        void BuildMelody()
        {
            melody.Clear();
            for (int i = 0; i < noteCount; i++)
            {
                int semis = Random.Range(0, rangeSemitones + 1);
                int note = baseNote + semis;
                note = ForceToCMajor(note);
                melody.Add(note);
            }
            if (log) Debug.Log($"[Dictation] New melody: {string.Join(",", melody)}");
        }

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

        void ResetSquaresVisual()
        {
            for (int i = 0; i < squares.Count; i++)
            {
                if (!squares[i]) continue;
                squares[i].gameObject.SetActive(true);
                squares[i].color = squareBaseColor;
            }
        }

        void ClearSquares()
        {
            if (!squaresParent) return;
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