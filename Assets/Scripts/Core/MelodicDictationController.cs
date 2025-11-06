// MelodicDictationController.cs — Cards Integration (brace-corrected)
// Spawns 3D cards, highlights during playback, flips on correct guesses,
// flips back on wrong, and slides off on win. Scoring/timer preserved.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Reflection; 

namespace EarFPS
{
    public class MelodicDictationController : MonoBehaviour
    {
        [SerializeField] FmodSfxPlayer fmodSfx;

        [SerializeField] FmodNoteSynth fmodNoteSynth;
        [SerializeField] bool useFmodForPlayback = false;
        
        [Header("Audio / Synth")]
        [SerializeField] MinisPolySynth synth;
        [Range(0f, 1f)] public float playbackVelocity = 0.9f;
        public float noteDuration = 0.8f;
        public float noteGap = 0.2f;

        [Header("Pre-roll")]
        public float preRollSeconds = 1.25f;
        [SerializeField, Range(0.1f, 1.0f)] float replayPreRollMultiplier = 0.5f;

        [Header("Melody Settings")]
        public int noteCount = 5;
        public int baseNote = 48;        // C3
        public int rangeSemitones = 12;  // one octave

        [Header("Generator (optional)")]
        [Tooltip("If assigned, this controller will call MelodyGenerator.Generate() and ignore baseNote/rangeSemitones.")]
        [SerializeField] MelodyGenerator melodyGen;

        // --- Legacy Squares UI (kept for compatibility; not used with cards) ---
        [Header("UI (Legacy Squares - optional)")]
        [SerializeField] RectTransform squaresParent;
        [SerializeField] Image squarePrefab;
        [SerializeField] Button replayButton;
        // [SerializeField] Color squareBaseColor = new Color(0, 1, 1, 0.35f);
        // [SerializeField] Color squareHighlightColor = new Color(0, 1, 1, 0.85f);
        // [SerializeField] Color squareClearedColor = new Color(0, 0, 0, 0.0f);
        // [SerializeField] bool hideClearedSquares = true;

        [Header("Scoring & Messaging")]
        [SerializeField] TMP_Text scoreText;           // Assign UIHud/ScoreText
        [SerializeField] TMP_Text roundsText;           // Assign UIHud/RoundsText
        [SerializeField] TMP_Text messageText;         // Optional HUD text
        [SerializeField] float messageDuration = 1.2f;
        [SerializeField] Color messageWinColor = new Color(0.2f, 1f, 0.6f, 1f);
        [SerializeField] Color messageWrongColor = new Color(1f, 0.4f, 0.3f, 1f);

        [Header("SFX")]
        [SerializeField] AudioSource sfxSource;        // 2D source
        [SerializeField] AudioClip sfxWin;
        [SerializeField] AudioClip sfxWrong;
        [SerializeField] AudioClip sfxGameOver;
        [SerializeField] AudioClip sfxCardsSweep;

        // ---- Cards config ----
        [Header("Cards")]
        [SerializeField] GameObject cardPrefab;        // assign CardPrefab
        [SerializeField] Transform cardSpawnLine;      // assign CardSpawnLine (anchor on table)
        [SerializeField] float cardSpacing = 0.18f;
        [SerializeField] float spawnHeight = 0.65f;

        [SerializeField] float holdAfterReveal = 0.25f;   // small beat after the flip finishes
        [SerializeField] float postSweepDelay = 0.20f;   // tiny beat after sweep before next round

        [SerializeField] LightningFX lightning;

        private readonly List<CardController> activeCards = new();
        private int revealCursor = 0;
        [SerializeField] PianoKeyboardUI pianoUI;

        [Header("Scoring Tunables")]
        [Tooltip("Score for a fully-correct melody: pointsPerNote × melodyLength.")]
        public int pointsPerNote = 100;
        [Tooltip("Penalty when a wrong note is entered.")]
        public int pointsWrongNote = -100;
        [Tooltip("Penalty each time Replay is pressed.")]
        public int pointsReplay = -25;
        [Tooltip("Continuous drain per second while Listening (use negative).")]
        public float pointsPerSecondInput = -5f;
        [Tooltip("Max wrong notes allowed per round before Game Over.")]
        public int maxWrongPerRound = 3;

        // Scoring state
        private int score = 0;
        private int wrongGuessesThisRound = 0;

        // Run/session stats
        private float runStartTime = 0f;
        private int roundsCompleted = 0;

        [Header("Game Over / End Screen")]
        [SerializeField] EndScreenController endScreen; // optional

        [Header("Game Over Cinematic")]
        [SerializeField] List<CandleFlicker> candles = new();   // assign all Candle prefabs placed in scene
        [SerializeField] float goStormDuration = 2.0f;        // total storm window
        [SerializeField] Vector2Int goStrikes = new(6, 8);   // min/max flashes within the window
        [SerializeField] float goExtinguishSpread = 0.5f;       // random spread between candle outs
        [SerializeField] float goExtinguishFade = 0.30f;      // per-candle fade time (matches CandleFlicker default)
        [SerializeField] float goPostExtinguishPause = 0.5f;    // beat before we move on (used next step)
        [Range(0f, 1f)] public float goExtinguishAt = 0.35f;   // 35% into the storm
        [Range(0f, 1f)] public float goPreExtOverlap = 0.40f;   // how much of the violent flicker to play before extinguish
        [SerializeField] KeyCode goSkipKey = KeyCode.Escape;    // skip key during cinematic

        [Header("GO: Candle Pre-Extinguish")]
        [SerializeField] float goPreExtFlickerDur = 1.1f;     // how long candles go wild before fade
        [SerializeField] float goPreExtAmpMul = 2.2f;     // violent amplitude multiplier
        [SerializeField] float goPreExtSpeedMul = 2.6f;     // violent speed multiplier
        [SerializeField] float goPreExtBaseMul = 1.12f;    // slight brightening
        [Header("Game Over • Blackout timing")]



        bool goCinematicRunning = false;

        [Header("Debug")]
        [SerializeField] bool log = false;

        private enum State { Idle, Playing, Listening }
        private State state = State.Idle;

        // Melody & UI
        private readonly List<int> melody = new();
        private readonly List<Image> squares = new(); // legacy (unused)

        // Input/progress
        private int inputIndex = 0;

        // Coroutines
        private Coroutine playingCo;
        private Coroutine messageCo;

        // Round timer derived from potential points
        private float roundTimeBudgetSec = 0f;        // computed at StartRound
        private float roundTimeRemainingSec = 0f;     // counts down only while Listening
        private float drainAccumulator = 0f;          // accumulates fractional score drain

        // ---------------- Unity ----------------
        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (replayButton) replayButton.onClick.AddListener(OnReplayButtonPressed);
            UpdateScoreUI();
            if (messageText) messageText.gameObject.SetActive(false);
        }

        private void Start()
        {
            runStartTime = Time.time;
            StartRound();
        }

        private void OnDestroy()
        {
            if (replayButton) replayButton.onClick.RemoveListener(OnReplayButtonPressed);
        }

        private void Update()
        {
            // Quick hotkey to replay during testing
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                OnReplayButtonPressed();

            // Timer drains only while Listening
            if (state == State.Listening)
            {
                float drainRate = Mathf.Abs(pointsPerSecondInput);
                if (drainRate < 0.001f) drainRate = 0.001f;

                float dt = Time.deltaTime;
                roundTimeRemainingSec = Mathf.Max(0f, roundTimeRemainingSec - dt);

                // Accumulate drain and apply in whole-point steps
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

            if (goCinematicRunning && Input.GetKeyDown(goSkipKey))
            {
                // kill pending routines started by us
                StopCoroutine("GameOverCinematic");

                // force-extinguish any remaining candles quickly
                foreach (var c in candles)
                    if (c != null) c.Extinguish(0f, 0.05f);

                goCinematicRunning = false;
                if (endScreen != null)
                {
                    endScreen.ShowDictation(score, roundsCompleted, Time.time - runStartTime);
                }
            }
        }

        // ---------------- MIDI ----------------
        public void OnMidiNoteOn(int midiNoteNumber, float velocity)
        {
            if (state != State.Listening) return;
            if (inputIndex < 0 || inputIndex >= melody.Count) return;

            int expected = melody[inputIndex];
            int gotPC = NormalizePitchClass(midiNoteNumber);
            int expPC = NormalizePitchClass(expected);

            // visual feedback
            if (pianoUI != null)
                pianoUI.OnMidiNoteOnExternal(midiNoteNumber, velocity);

            if (gotPC == expPC)
            {
                RevealNext();
                inputIndex++;

                if (inputIndex >= melody.Count)
                {
                    int gain = Mathf.Max(0, pointsPerNote) * melody.Count;
                    score += gain;
                    roundsCompleted++;
                    var applier = FindFirstObjectByType<Sonoria.Dictation.DifficultyProfileApplier>();
                    applier?.NotifyRoundCompleted();
                    
                    UpdateScoreUI();
                    //PlaySfx(sfxWin);
                    if (fmodSfx) fmodSfx.PlayWin();
                    ShowMessage($"Great! +{gain}", messageWinColor);

                    state = State.Idle;
                    StartCoroutine(WinSequence());  // <-- was WinThenNextRound()
                }
            }
            else
            {
                score += pointsWrongNote;
                UpdateScoreUI();
                //PlaySfx(sfxWrong);
                //if (fmodSfx) fmodSfx.PlayWrong();
                ShowMessage($"Wrong note! {pointsWrongNote}", messageWrongColor);
                if (lightning) lightning.Strike(1);   // 0=single, 1=double flicker

                wrongGuessesThisRound++;
                if (wrongGuessesThisRound >= Mathf.Max(1, maxWrongPerRound))
                {
                    GameOver();
                    return;
                }

                inputIndex = 0;
                RehideRevealed();
                //PlayMelodyFromTop(isReplay: false);
                //ReplayMelody();
            }
        }

        public void OnMidiNoteOff(int midiNoteNumber)
        {
            if (pianoUI != null)
                pianoUI.OnMidiNoteOffExternal(midiNoteNumber);
        }

        // ---------------- Flow ----------------
        private void StartRound()
        {
            if (pianoUI != null)
                pianoUI.ShowImmediate();
            
            // Seed & generator config
            if (melodyGen != null)
            {
                melodyGen.Seed = Random.Range(int.MinValue, int.MaxValue);
                melodyGen.Length = Mathf.Max(1, noteCount);
                if (log) Debug.Log($"[Dictation] New random seed set: {melodyGen.Seed}");
            }

            wrongGuessesThisRound = 0;
            drainAccumulator = 0f;

            BuildMelody();

            // Build cards for this melody
            StartCoroutine(BuildCards(melody.ToArray()));

            // Compute round time budget from potential points
            float potential = Mathf.Max(0, pointsPerNote) * melody.Count;
            float drainRate = Mathf.Abs(pointsPerSecondInput);
            if (drainRate < 0.001f) drainRate = 0.001f;
            roundTimeBudgetSec = potential / drainRate;
            roundTimeRemainingSec = roundTimeBudgetSec;

            if (log) Debug.Log($"[Dictation] Round time budget = {roundTimeBudgetSec:F1}s (mel={melody.Count}, potential={potential}, drain/s={drainRate})");

            // First play uses full pre-roll
            PlayMelodyFromTop(isReplay: false);
        }

        private void OnReplayButtonPressed()
        {
            score += pointsReplay;
            UpdateScoreUI();
            ReplayMelody();
        }

        private void ReplayMelody()
        {
            if (playingCo != null) StopCoroutine(playingCo);
            // Replays use shorter pre-roll
            PlayMelodyFromTop(isReplay: true);
        }

        private void PlayMelodyFromTop(bool isReplay)
        {
            inputIndex = 0;
            RehideRevealed();
            state = State.Playing;
            float delay = preRollSeconds * (isReplay ? replayPreRollMultiplier : 1f);
            playingCo = StartCoroutine(PlayMelodyCo(delay));
        }

        private IEnumerator PlayMelodyCo(float delay)
        {
            pianoUI.LockInput(true);   // fade + disable keyboard
            
            if (delay > 0f) yield return new WaitForSeconds(delay);

            for (int i = 0; i < melody.Count; i++)
            {
                int note = melody[i];

                // highlight the corresponding card during playback
                HighlightPlaybackIndex(i, true);

                if (useFmodForPlayback && fmodNoteSynth)
                    fmodNoteSynth.NoteOn(note, playbackVelocity);
                else if (synth)
                    synth.NoteOn(note, playbackVelocity);   // legacy playback synth; not used

                yield return new WaitForSeconds(noteDuration);

                if (useFmodForPlayback && fmodNoteSynth)
                    fmodNoteSynth.NoteOff(note);
                else if (synth)
                    synth.NoteOff(note);

                HighlightPlaybackIndex(i, false);

                if (i < melody.Count - 1 && noteGap > 0f)
                    yield return new WaitForSeconds(noteGap);
            }

            pianoUI.LockInput(false);  // restore
            state = State.Listening;
            if (log) Debug.Log("[Dictation] Now LISTENING");
        }

        private IEnumerator WinSequence()
        {
            // Wait for the last card’s reveal to visually complete
            if (activeCards.Count > 0)
            {
                var last = activeCards[activeCards.Count - 1];
                float wait = last != null ? last.GetRevealWaitSeconds() : 0.35f;
                yield return new WaitForSeconds(wait);
            }

            // Extra beat for “ahh, nice” ✨
            if (holdAfterReveal > 0f)
                yield return new WaitForSeconds(holdAfterReveal);

            // Sweep them off
            yield return StartCoroutine(WinSlideAndCleanup());

            // Optional tiny beat after the sweep
            if (postSweepDelay > 0f)
                yield return new WaitForSeconds(postSweepDelay);

            // Next round (first play again uses full pre-roll)
            StartRound();
        }

        // ---------------- Melody ----------------
        private void BuildMelody()
        {
            melody.Clear();

            int target = Mathf.Max(1, noteCount);

            if (melodyGen != null)
            {
                const int MaxAttempts = 6;   // try a few times before giving up
                for (int attempt = 0; attempt < MaxAttempts; attempt++)
                {
                    // Generate
                    var gen = melodyGen.Generate();

                    // Copy up to target
                    melody.Clear();
                    if (gen != null)
                    {
                        for (int i = 0; i < gen.Count && i < target; i++)
                            melody.Add(gen[i].midi);
                    }

                    // If exact length, we're done
                    if (melody.Count == target) break;

                    // If too many, trim and stop
                    if (melody.Count > target)
                    {
                        melody.RemoveRange(target, melody.Count - target);
                        break;
                    }

                    // Too short: reseed (if the generator exposes a Seed) and try again
                    // This uses reflection so it’s safe even if Seed doesn’t exist.
                    var seedProp = melodyGen.GetType().GetProperty(
                        "Seed",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    );
                    if (seedProp != null && seedProp.CanWrite)
                    {
                        int newSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                        seedProp.SetValue(melodyGen, newSeed);
                    }
                    // otherwise rely on internal RNG differences and loop again
                }

                // Final guard: if still short, pad by repeating the last pitch (stable for testing)
                while (melody.Count < target)
                {
                    int last = melody.Count > 0 ? melody[melody.Count - 1] : baseNote;
                    melody.Add(last);
                }
            }
            else
            {
                // Fallback: random C-major in baseNote..baseNote+range
                for (int i = 0; i < target; i++)
                {
                    int semis = Random.Range(0, rangeSemitones + 1);
                    int note = baseNote + semis;
                    note = ForceToCMajor(note);
                    melody.Add(note);
                }
            }
        }

        // ---------------- Messaging & SFX ----------------
        private void ShowMessage(string msg, Color color)
        {
            if (!messageText) return;
            if (messageCo != null) StopCoroutine(messageCo);
            messageCo = StartCoroutine(ShowMessageCo(msg, color));
        }

        private IEnumerator ShowMessageCo(string msg, Color color)
        {
            messageText.text = msg;
            messageText.color = color;
            messageText.gameObject.SetActive(true);
            yield return new WaitForSeconds(messageDuration);
            messageText.gameObject.SetActive(false);
        }

        // private void PlaySfx(AudioClip clip)
        // {
        //     if (!sfxSource || !clip) return;
        //     sfxSource.PlayOneShot(clip);
        // }

        // ---------------- Game Over ----------------
        private void GameOver()
        {
            if (log) Debug.Log("[Dictation] GAME OVER");
            state = State.Idle;
            if (playingCo != null) StopCoroutine(playingCo);

            ShowMessage("Game Over", messageWrongColor);
            //PlaySfx(sfxGameOver);
            if (fmodSfx) fmodSfx.PlayGameOver();
            MainMenuController.SetDictationHighScore(score);

            StartCoroutine(GameOverCinematic());

        }

        // ---------------- Helpers ----------------
        private void UpdateScoreUI()
        {
            if (scoreText) scoreText.text = $"Score: {score}";
            if (roundsText) roundsText.text = $"Rounds: {roundsCompleted}";
        }

        private static int NormalizePitchClass(int note) => ((note % 12) + 12) % 12;

        private static int ForceToCMajor(int midiNote)
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

        // ---------------- Cards ----------------
        private void ClearCards()
        {
            foreach (var c in activeCards)
                if (c) Destroy(c.gameObject);

            activeCards.Clear();
            revealCursor = 0;
        }

        // Map from generator's ScaleMode (EarFPS) to NoteNamer.Mode
        private static NoteNamer.Mode MapMode(ScaleMode m)
        {
            switch (m)
            {
                case ScaleMode.Ionian:     return NoteNamer.Mode.Ionian;
                case ScaleMode.Dorian:     return NoteNamer.Mode.Dorian;
                case ScaleMode.Phrygian:   return NoteNamer.Mode.Phrygian;
                case ScaleMode.Lydian:     return NoteNamer.Mode.Lydian;
                case ScaleMode.Mixolydian: return NoteNamer.Mode.Mixolydian;
                case ScaleMode.Aeolian:    return NoteNamer.Mode.Aeolian;
                default:                   return NoteNamer.Mode.Ionian; // safe fallback
            }
        }


        private IEnumerator BuildCards(int[] mel)
        {
            ClearCards();
            if (mel == null || mel.Length == 0 || !cardPrefab || !cardSpawnLine)
                yield break;

            // --- center the row on cardSpawnLine ---
            float totalWidth = (mel.Length - 1) * cardSpacing;
            Vector3 origin = cardSpawnLine.position - cardSpawnLine.right * (totalWidth * 0.5f);

            // ---- KEY CONTEXT (root = C) ----
            // Option A: enums match -> direct cast
            NoteNamer.Mode nnMode = (NoteNamer.Mode)melodyGen.CurrentMode;

            // // ---- KEY CONTEXT (root = C) ----
            // Option B: use mapper
            //NoteNamer.Mode nnMode = MapMode(melodyGen.CurrentMode);

            var keyCtx = new NoteNamer.KeyContext(
                tonicPc: 0,   // C
                mode: nnMode
            );

            for (int i = 0; i < mel.Length; i++)
            {
                Vector3 pos = origin + cardSpawnLine.right * (i * cardSpacing);
                pos.y += spawnHeight;

                int midi = mel[i];

                // Correctly spelled name in the current mode (e.g., Bb in C Dorian)
                string name = NoteNamer.NameForMidi(midi, keyCtx);
                int octave = NoteNamer.OctaveOfMidi(midi);

                var go = Instantiate(cardPrefab, pos, Quaternion.identity);
                var cc = go.GetComponent<CardController>();
                cc.SetNoteAndCorners(name, octave, showCornerLabels: true);

                cc.BeginDrop();
                activeCards.Add(cc);

                yield return new WaitForSeconds(0.05f);
            }
        }

        public void HighlightPlaybackIndex(int idx, bool on)
        {
            if (idx < 0 || idx >= activeCards.Count) return;
            activeCards[idx]?.SetHighlight(on);
        }

        private void RevealNext()
        {
            if (revealCursor < 0 || revealCursor >= activeCards.Count) return;
            activeCards[revealCursor].Reveal();
            revealCursor++;
        }

        private void RehideRevealed()
        {
            for (int i = 0; i < revealCursor; i++)
                activeCards[i].Hide();

            revealCursor = 0;
        }

        private IEnumerator WinSlideAndCleanup()
        {
            //PlaySfx(sfxCardsSweep);
            if (fmodSfx) fmodSfx.PlayCardsSweep();

            foreach (var c in activeCards)
                if (c) StartCoroutine(c.SlideOffAndDestroy());

            activeCards.Clear();
            yield return new WaitForSeconds(0.25f);
        }

        IEnumerator GameOverCinematic()
        {
            goCinematicRunning = true;

             // 1) (lock inputs / stop playback if needed)
            if (pianoUI != null)
                pianoUI.HideImmediate();   // ← fully invisible + no interaction

            if (lightning != null)
            {
                // Apply latest settings
                lightning.stormDuration = goStormDuration;
                lightning.stormMinStrikes = goStrikes.x;
                lightning.stormMaxStrikes = goStrikes.y;

                // 2) Start storm CONCURRENTLY (important!)
                //StartCoroutine(lightning.StormCo(goStormDuration, goStrikes.x, goStrikes.y, true));
                // Fire-and-forget storm so we can overlap our candle timings.
                lightning.PlayStorm(goStormDuration, goStrikes.x, goStrikes.y, true);

                // 3) Wait until our normalized trigger time, then start the violent flicker
                float triggerTime = Mathf.Clamp01(goExtinguishAt) * goStormDuration;
                yield return new WaitForSeconds(triggerTime);

                // Violent flicker (only those still lit)
                foreach (var c in candles)
                {
                    if (c != null && c.IsLit)
                        c.ViolentFlicker(goPreExtFlickerDur, goPreExtAmpMul, goPreExtSpeedMul, goPreExtBaseMul);
                }

                // Let part of the flicker play, then extinguish (overlap for snap)
                yield return new WaitForSeconds(goPreExtFlickerDur * goPreExtOverlap);

                float maxDelay = 0f;
                foreach (var c in candles)
                {
                    if (c == null) continue;
                    float delay = Random.Range(0f, goExtinguishSpread);
                    maxDelay = Mathf.Max(maxDelay, delay);
                    c.Extinguish(delay, goExtinguishFade);
                }

                // 4) Wait for the remainder of the storm (or for fades, whichever is longer)
                float remainingStorm = Mathf.Max(0f, goStormDuration - triggerTime);
                float blackoutSettle = maxDelay + goExtinguishFade + goPostExtinguishPause;
                yield return new WaitForSeconds(Mathf.Max(remainingStorm, blackoutSettle));
            }
            else
            {
                // Fallback: no lightning — just extinguish now
                float maxDelay = 0f;
                foreach (var c in candles)
                {
                    if (c == null) continue;
                    float delay = Random.Range(0f, goExtinguishSpread);
                    maxDelay = Mathf.Max(maxDelay, delay);
                    c.Extinguish(delay, goExtinguishFade);
                }
                yield return new WaitForSeconds(maxDelay + goExtinguishFade + goPostExtinguishPause);
            }

            // (Next: reveal/glow + sequential card flip + correct melody)

            goCinematicRunning = false;
            if (endScreen != null)
                endScreen.ShowDictation(score, roundsCompleted, Time.time - runStartTime);
        }


        // ---------------- Legacy Squares (unused) ----------------
        // private void ResetSquaresVisual()
        // {
        //     for (int i = 0; i < squares.Count; i++)
        //     {
        //         if (!squares[i]) continue;
        //         squares[i].gameObject.SetActive(true);
        //         squares[i].color = squareBaseColor;
        //     }
        // }

        // private void ClearSquares()
        // {
        //     if (!squaresParent) return;
        //     for (int i = squaresParent.childCount - 1; i >= 0; i--)
        //         Destroy(squaresParent.GetChild(i).gameObject);
        // }
    }
}
