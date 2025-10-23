// MelodicDictationController.cs — Scoring & Game Over v1
// Uses newly commented baseline; adds round-based timer, penalties, rewards, and Game Over flow.
// No DSP scheduling; playback uses the existing coroutine path.

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
        [SerializeField] TMP_Text messageText;         // Optional HUD text for pop-up messages
        [SerializeField] float messageDuration = 1.2f;
        [SerializeField] Color messageWinColor = new Color(0.2f, 1f, 0.6f, 1f);
        [SerializeField] Color messageWrongColor = new Color(1f, 0.4f, 0.3f, 1f);

        [Header("SFX")] 
        [SerializeField] AudioSource sfxSource;        // Assign a 2D AudioSource on Canvas/GameRoot
        [SerializeField] AudioClip sfxWin;             // Play when full melody correct
        [SerializeField] AudioClip sfxWrong;           // Play when a note is wrong
        [SerializeField] AudioClip sfxGameOver;        // Optional game-over sting

        [Header("Scoring Tunables")]
        [Tooltip("Score awarded for a fully-correct melody: pointsPerNote × melodyLength.")]
        public int pointsPerNote = 100;
        [Tooltip("Penalty applied on each wrong input note.")]
        public int pointsWrongNote = -100;
        [Tooltip("Penalty applied each time Replay is pressed (does not reset timer).")]
        public int pointsReplay = -25;
        [Tooltip("Continuous drain while Listening (negative value).")]
        public float pointsPerSecondInput = -5f;
        [Tooltip("Maximum wrong notes allowed per round before Game Over.")]
        public int maxWrongPerRound = 3;

        // Scoring state
        int score = 0;
        int wrongGuessesThisRound = 0;

        // Run/session stats
        float runStartTime = 0f;
        int roundsCompleted = 0;

        [Header("Game Over / End Screen")]
        [Tooltip("If assigned, this shared End Screen will be shown on Game Over.")]
        [SerializeField] EndScreenController endScreen; // Optional; shared with other module

        [Header("Debug")]
        [SerializeField] bool log = false;

        enum State { Idle, Playing, Listening }
        State state = State.Idle;

        // Melody & UI
        readonly List<int> melody = new();
        readonly List<Image> squares = new();
        int inputIndex = 0;
        Coroutine playingCo;
        Coroutine messageCo;


        // Round timer derived from potential points
        float roundTimeBudgetSec = 0f;        // computed at StartRound
        float roundTimeRemainingSec = 0f;     // counts down only while Listening
        float drainAccumulator = 0f;          // accumulates fractional score drain between int UI updates

        void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;

            if (replayButton) replayButton.onClick.AddListener(OnReplayButtonPressed);
            UpdateScoreUI();
            if (messageText) messageText.gameObject.SetActive(false);
        }

        void Start()
        {
            runStartTime = Time.time;
            StartRound();
        }

        void OnDestroy()
        {
            if (replayButton) replayButton.onClick.RemoveListener(OnReplayButtonPressed);
        }

        // ---------- MIDI from MidiProbe ----------
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
                    int gain = Mathf.Max(0, pointsPerNote) * melody.Count;
                    score += gain;
                    roundsCompleted += 1;  
                    UpdateScoreUI();
                    PlaySfx(sfxWin);
                    ShowMessage($"Great! +{gain}", messageWinColor);

                    state = State.Idle;
                    StartCoroutine(WinThenNextRound());
                }
            }
            else
            {
                // Wrong → score penalty, increment per-round counter, then either game over or replay same melody
                score += pointsWrongNote;
                UpdateScoreUI();
                PlaySfx(sfxWrong);
                ShowMessage($"Wrong note! {pointsWrongNote}", messageWrongColor);

                wrongGuessesThisRound++;
                if (wrongGuessesThisRound >= Mathf.Max(1, maxWrongPerRound))
                {
                    GameOver();
                    return;
                }

                inputIndex = 0;
                ResetSquaresVisual();
                ReplayMelody();
            }
        }

        public void OnMidiNoteOff(int midiNoteNumber) { /* not used */ }

        // ---------- Flow ----------
        void StartRound()
        {
            
            melodyGen.Seed = Random.Range(int.MinValue, int.MaxValue);
            if (log) Debug.Log($"[Dictation] New random seed set: {melodyGen.Seed}");
            melodyGen.Length = Mathf.Max(1, noteCount);
            
            wrongGuessesThisRound = 0;
            drainAccumulator = 0f;

            BuildMelody();
            BuildSquares();

            // Compute round time budget from potential points
            float potential = Mathf.Max(0, pointsPerNote) * melody.Count;
            float drainRate = Mathf.Abs(pointsPerSecondInput);
            if (drainRate < 0.001f) drainRate = 0.001f; // guard against zero/near-zero
            roundTimeBudgetSec = potential / drainRate;
            roundTimeRemainingSec = roundTimeBudgetSec;
            if (log) Debug.Log($"[Dictation] Round time budget = {roundTimeBudgetSec:F1}s (mel={melody.Count}, potential={potential}, drain/s={drainRate})");

            PlayMelodyFromTop();
        }

        void OnReplayButtonPressed()
        {
            // Apply replay penalty every time; timer does not reset (but will pause during playback)
            score += pointsReplay;
            UpdateScoreUI();
            ReplayMelody();
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

            state = State.Listening; // timer drain begins in Update()
            if (log) Debug.Log("[Dictation] Now LISTENING");
        }

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

        // ---------- Messaging & SFX ----------
        void ShowMessage(string msg, Color color)
        {
            if (!messageText) return;
            if (messageCo != null) StopCoroutine(messageCo);
            messageCo = StartCoroutine(ShowMessageCo(msg, color));
        }
        IEnumerator ShowMessageCo(string msg, Color color)
        {
            messageText.text = msg;
            messageText.color = color;
            messageText.gameObject.SetActive(true);
            yield return new WaitForSeconds(messageDuration);
            messageText.gameObject.SetActive(false);
        }
        void PlaySfx(AudioClip clip)
        {
            if (!sfxSource || !clip) return;
            sfxSource.PlayOneShot(clip);
        }

        // ---------- Game Over ----------
        void GameOver()
        {
            if (log) Debug.Log("[Dictation] GAME OVER");
            state = State.Idle;
            if (playingCo != null) StopCoroutine(playingCo);

            // stop any lingering synth notes (if your synth has a helper; otherwise safe to skip)
            //if (synth != null) synth.AllNotesOff?.Invoke();

            ShowMessage("Game Over", messageWrongColor);
            PlaySfx(sfxGameOver);
            MainMenuController.SetDictationHighScore(score);

            if (endScreen != null)
            {
                var stats = BuildRunStats();

                if (!isActiveAndEnabled)
                {
                    // Component disabled (e.g., scene shutdown); finish synchronously
                    endScreen.ShowDictation(score, roundsCompleted, Time.time - runStartTime);
                    return;
                }

                endScreen.ShowDictation(score, roundsCompleted, Time.time - runStartTime);
            }
        }

        // ---------- Helpers ----------
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

        RunStats BuildRunStats()
        {
            return new RunStats
            {
                score            = score,
                timeSeconds      = Time.time - runStartTime,
                enemiesDestroyed = 0,               // not used in this module
                correct          = roundsCompleted, // repurpose as "rounds completed"
                total            = roundsCompleted, // same for now; can change later
                bestStreak       = 0,               // not tracked (yet)
            };
        }


        void Update()
        {
            // Quick hotkey to replay during testing
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                OnReplayButtonPressed();

            // Round timer runs ONLY while Listening
            if (state == State.Listening)
            {
                float drainRate = Mathf.Abs(pointsPerSecondInput);
                if (drainRate < 0.001f) drainRate = 0.001f;

                float dt = Time.deltaTime;
                roundTimeRemainingSec = Mathf.Max(0f, roundTimeRemainingSec - dt);

                // Accumulate drain and apply in whole-point steps for a stable UI
                drainAccumulator += drainRate * dt; // positive magnitude
                int whole = Mathf.FloorToInt(drainAccumulator);
                if (whole > 0)
                {
                    score -= whole;
                    drainAccumulator -= whole;
                    UpdateScoreUI();
                }

                if (roundTimeRemainingSec <= 0f)
                {
                    GameOver();
                }
            }
        }
    }
}
