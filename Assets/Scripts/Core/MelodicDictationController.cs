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
        [SerializeField] DronePlayer dronePlayer;

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
        [Tooltip("Max wrong notes allowed per level before Game Over.")]
        public int maxWrongPerLevel = 6;

        [Header("Melody Generation")]
        [Tooltip("Prevent the same melody from appearing in two consecutive rounds.")]
        [SerializeField] bool preventDuplicateMelodies = true;

        // Scoring state
        private int score = 0;
        public int Score => score; // Public accessor for current score
        private int wrongGuessesThisLevel = 0;  // Wrong guesses per level (not per round)
        private int wrongNotePenaltiesThisRound = 0;  // Accumulated wrong note penalties (not applied until round completion)
        private int replayPenaltiesThisRound = 0;     // Accumulated replay penalties (not applied until round completion)

        // Campaign mode state
        [Header("Campaign Mode")]
        [Tooltip("Whether the controller is in campaign mode (managed by CampaignService)")]
        [SerializeField] bool isCampaignMode = false;
        [Tooltip("Number of wins required to complete the current level (campaign mode only)")]
        [SerializeField] int winsRequired = 0;
        private bool levelJustCompleted = false;
        private int newlyUnlockedNodeIndex = -1; // -1 if no node was unlocked

        // Run/session stats
        private float runStartTime = 0f;
        private int roundsCompleted = 0;

        [Header("Game Over / End Screen")]
        [SerializeField] EndScreenController endScreen; // optional

        [Header("Unlock Announcement")]
        [Tooltip("Unlock announcement controller (shown when a new module is unlocked)")]
        [SerializeField] EarFPS.UnlockAnnouncementController unlockAnnouncement;

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

        [Header("Candle Hitpoints")]
        [Tooltip("Use candles as hitpoint indicators. On wrong guess, extinguish 2 random lit candles.")]
        [SerializeField] bool useCandlesAsHitpoints = true;
        [Tooltip("Duration of violent flicker before extinguishing on wrong guess.")]
        [SerializeField] float wrongGuessFlickerDuration = 0.5f;
        [Tooltip("How much of the flicker duration to wait before starting extinguish (0 = wait full duration, 1 = start immediately).")]
        [Range(0f, 1f)]
        [SerializeField] float wrongGuessFlickerOverlap = 0.4f;
        [Tooltip("Amplitude multiplier for violent flicker on wrong guess.")]
        [SerializeField] float wrongGuessFlickerAmpMul = 2.2f;
        [Tooltip("Speed multiplier for violent flicker on wrong guess.")]
        [SerializeField] float wrongGuessFlickerSpeedMul = 2.6f;
        [Tooltip("Base intensity multiplier for violent flicker on wrong guess.")]
        [SerializeField] float wrongGuessFlickerBaseMul = 1.12f;
        [Tooltip("Fade duration when extinguishing candles on wrong guess.")]
        [SerializeField] float wrongGuessExtinguishFade = 0.30f;

        [Header("Game Over • Blackout timing")]



        bool goCinematicRunning = false;
        private bool _gameOverOccurred = false; // Track if game over happened (needed for environment reinitialization)
        private bool _droneStartedForLevel = false; // Track if drone has been started for the current level

        [Header("Debug")]
        [SerializeField] bool log = false;

        private enum State { Idle, Playing, Listening }
        private State state = State.Idle;

        // Melody & UI
        private readonly List<int> melody = new();
        private readonly List<Image> squares = new(); // legacy (unused)
        private List<int> previousMelody = null; // Store the last completed melody to prevent duplicates

        // Input/progress
        private int inputIndex = 0;

        // Coroutines
        private Coroutine playingCo;
        private Coroutine messageCo;

        // Round timer derived from potential points
        private float roundTimeBudgetSec = 0f;        // computed at StartRound
        private float roundTimeRemainingSec = 0f;     // counts down only while Listening
        private float drainAccumulator = 0f;          // accumulates fractional score drain

        // Time limit timer (independent of point deduction)
        [Tooltip("Time limit per round in seconds (set by DifficultyProfile).")]
        public float roundTimeLimitSec = 60.0f;      // time limit from profile
        private float roundTimeLimitRemainingSec = 0f; // remaining time limit
        private bool timeLimitStarted = false;        // whether timer has started
        private float lastLogTime = 0f;               // last time we logged (for 1-second intervals)

        /// <summary>
        /// Gets the remaining time limit in seconds.
        /// </summary>
        public float GetTimeLimitRemaining() => roundTimeLimitRemainingSec;

        /// <summary>
        /// Gets the total time limit in seconds.
        /// </summary>
        public float GetTimeLimit() => roundTimeLimitSec;

        /// <summary>
        /// Returns whether the time limit timer has started.
        /// </summary>
        public bool IsTimeLimitActive() => timeLimitStarted;

        /// <summary>
        /// Returns whether the controller is currently in Listening state (player can input).
        /// </summary>
        public bool IsListening() => state == State.Listening;

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
            // Reset previous melody at game start to allow any first melody
            previousMelody = null;
            
            // FIRST: Check if campaign map exists - enable it if disabled, then check visibility
            var mapView = FindFirstObjectByType<EarFPS.CampaignMapView>(FindObjectsInactive.Include);
            if (mapView != null)
            {
                // Enable the map view if it was disabled in editor (workflow convenience)
                if (!mapView.gameObject.activeSelf)
                {
                    mapView.gameObject.SetActive(true);
                    Debug.Log("[Dictation] CampaignMapView was disabled in editor - enabled automatically");
                }
                
                // Also enable level picker if it exists and is disabled
                var levelPicker = FindFirstObjectByType<EarFPS.CampaignLevelPicker>(FindObjectsInactive.Include);
                if (levelPicker != null && !levelPicker.gameObject.activeSelf)
                {
                    levelPicker.gameObject.SetActive(true);
                    Debug.Log("[Dictation] CampaignLevelPicker was disabled in editor - enabled automatically");
                }
                
                // Check if map is visible in hierarchy (should be true now if we just enabled it)
                if (mapView.gameObject.activeInHierarchy)
                {
                    // Map is visible - don't start free-play, wait for level selection
                    Debug.Log("[Dictation] Campaign map visible. Waiting for level selection.");
                    return;
                }
            }
            
            // Check if campaign mode is enabled but no level is actually active
            if (isCampaignMode)
            {
                // If campaign mode is enabled, verify that a level is actually active
                var campaignService = FindFirstObjectByType<Sonoria.Dictation.CampaignService>();
                if (campaignService == null || campaignService.CurrentLevel == null || winsRequired == 0)
                {
                    // Campaign mode was enabled but no level is active - check map again
                    if (mapView == null)
                        mapView = FindFirstObjectByType<EarFPS.CampaignMapView>(FindObjectsInactive.Include);
                    
                    if (mapView != null)
                    {
                        // Enable if disabled
                        if (!mapView.gameObject.activeSelf)
                            mapView.gameObject.SetActive(true);
                        
                        if (mapView.gameObject.activeInHierarchy)
                        {
                            // Map is visible - don't disable campaign mode or start free-play
                            Debug.Log("[Dictation] Campaign mode enabled but no active level. Map visible - waiting for level selection.");
                            return;
                        }
                    }
                    
                    // No map visible - disable campaign mode and start free-play
                    Debug.LogWarning("[Dictation] Campaign mode enabled but no active level. Disabling campaign mode and starting free-play.");
                    DisableCampaignMode();
                    StartRoundInternal();
                    return;
                }
                
                // Campaign mode is active and level is set - wait for CampaignService to start the round
                if (log) Debug.Log("[Dictation] Campaign mode active. Waiting for CampaignService to start level.");
                return;
            }
            
            // Free-play mode: start normally
            StartRoundInternal();
        }

        /// <summary>
        /// Sets the wins required for campaign mode (called by CampaignService).
        /// </summary>
        public void SetWinsRequiredForCampaign(int winsRequired)
        {
            this.winsRequired = winsRequired;
            levelJustCompleted = false;
            isCampaignMode = true;
            
            // Reset drone started flag when a new level is configured
            _droneStartedForLevel = false;
            
            // Reset session stats for new level
            roundsCompleted = 0;
            score = 0;
            runStartTime = Time.time;
            
            // Reset wrong guesses counter for new level
            wrongGuessesThisLevel = 0;
            
            // Re-ignite all candles when starting a new level
            if (useCandlesAsHitpoints)
            {
                foreach (var candle in candles)
                {
                    if (candle != null && !candle.IsLit)
                    {
                        candle.Ignite();
                    }
                }
                if (log) Debug.Log("[Dictation] All candles re-lit at level start");
            }
            
            if (log) Debug.Log($"[Dictation] Campaign mode enabled: {winsRequired} wins required");
        }

        /// <summary>
        /// Disables campaign mode (for free-play).
        /// </summary>
        public void DisableCampaignMode()
        {
            isCampaignMode = false;
            winsRequired = 0;
            levelJustCompleted = false;
            
            // Reset drone started flag when disabling campaign mode (for free-play)
            _droneStartedForLevel = false;
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

            // Time limit timer (runs independently once started)
            if (timeLimitStarted)
            {
                float dt = Time.deltaTime;
                roundTimeLimitRemainingSec = Mathf.Max(0f, roundTimeLimitRemainingSec - dt);

                // Log remaining time every second
                if (Time.time - lastLogTime >= 1.0f)
                {
                    lastLogTime = Time.time;
                }

                // Check if time limit expired
                if (roundTimeLimitRemainingSec <= 0f)
                {
                    GameOver();
                    return; // Exit early to prevent point deduction
                }
            }

            // Point deduction timer (only while Listening and time limit not expired)
            // Accumulate time penalty but don't apply until round completion
            if (state == State.Listening && roundTimeLimitRemainingSec > 0f)
            {
                float drainRate = Mathf.Abs(pointsPerSecondInput);
                if (drainRate < 0.001f) drainRate = 0.001f;

                float dt = Time.deltaTime;
                roundTimeRemainingSec = Mathf.Max(0f, roundTimeRemainingSec - dt);

                // Accumulate drain penalty (will be applied when round completes)
                drainAccumulator += drainRate * dt; // positive magnitude

                // Note: roundTimeRemainingSec check removed - time limit is now the only expiration condition
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
                    // Calculate gross gain (points per note * melody length)
                    int grossGain = Mathf.Max(0, pointsPerNote) * melody.Count;
                    
                    // Calculate time penalty (convert accumulated drain to whole points)
                    int timePenalty = Mathf.FloorToInt(drainAccumulator);
                    
                    // Calculate total penalties (time + wrong notes + replays)
                    int totalPenalties = timePenalty + wrongNotePenaltiesThisRound + replayPenaltiesThisRound;
                    
                    // Calculate net gain (gross gain minus all penalties, clamped to 0 minimum)
                    int netGain = Mathf.Max(0, grossGain - totalPenalties);
                    
                    // Apply net gain to score
                    score += netGain;
                    roundsCompleted++;
                    
                    // Reset accumulators after applying penalties
                    drainAccumulator = 0f;
                    wrongNotePenaltiesThisRound = 0;
                    replayPenaltiesThisRound = 0;
                    
                    // Store the completed melody to prevent duplicates in the next round
                    if (preventDuplicateMelodies)
                    {
                        previousMelody = new List<int>(melody);
                        if (log) Debug.Log($"[Dictation] Stored previous melody: [{string.Join(", ", previousMelody)}]");
                    }
                    
                    // Campaign mode: record win and check for level completion
                    levelJustCompleted = false;
                    newlyUnlockedNodeIndex = -1;
                    if (isCampaignMode)
                    {
                        var campaignService = FindFirstObjectByType<Sonoria.Dictation.CampaignService>();
                        if (campaignService != null)
                        {
                            // RecordLevelWin increments counter, checks if level is complete, and handles unlock logic
                            var winResult = campaignService.RecordLevelWin();
                            levelJustCompleted = winResult.levelComplete;
                            newlyUnlockedNodeIndex = winResult.newlyUnlockedNodeIndex;
                        }
                    }
                    else
                    {
                        // Free-play mode: use applier's auto-advance
                        var applier = FindFirstObjectByType<Sonoria.Dictation.DifficultyProfileApplier>();
                        applier?.NotifyRoundCompleted();
                    }
                    
                    UpdateScoreUI();
                    //PlaySfx(sfxWin);
                    if (fmodSfx) fmodSfx.PlayWin();
                    ShowMessage($"Correct! +{netGain}", messageWinColor);

                    state = State.Idle;
                    StartCoroutine(WinSequence());  // <-- was WinThenNextRound()
                }
            }
            else
            {
                // Track wrong note penalty (don't apply until round completion)
                wrongNotePenaltiesThisRound += Mathf.Abs(pointsWrongNote); // Accumulate as positive value
                UpdateScoreUI();
                //PlaySfx(sfxWrong);
                //if (fmodSfx) fmodSfx.PlayWrong();
                ShowMessage($"Wrong! {pointsWrongNote}", messageWrongColor);
                if (lightning) lightning.Strike(1);   // 0=single, 1=double flicker

                wrongGuessesThisLevel++;
                
                // Extinguish candles on wrong guess if enabled
                if (useCandlesAsHitpoints)
                {
                    ExtinguishCandlesOnWrongGuess();
                }

                if (wrongGuessesThisLevel >= Mathf.Max(1, maxWrongPerLevel))
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
        /// <summary>
        /// Starts a new round. Public method for external calls (e.g., CampaignService).
        /// </summary>
        public void StartRound()
        {
            StartRoundInternal();
        }

        private void StartRoundInternal()
        {
            // Reinitialize environment if game over occurred (candles extinguished, drone stopped)
            if (_gameOverOccurred)
            {
                // Reset session stats when restarting after game over
                roundsCompleted = 0;
                score = 0;
                runStartTime = Time.time;
                wrongGuessesThisLevel = 0; // Reset wrong guesses counter when restarting level
                UpdateScoreUI(); // Update UI to reflect reset score
                
                // Reset campaign win counter when restarting after game over
                if (isCampaignMode)
                {
                    var campaignService = FindFirstObjectByType<Sonoria.Dictation.CampaignService>();
                    if (campaignService != null)
                    {
                        campaignService.ResetWinsThisLevel();
                        if (log) Debug.Log("[Dictation] Reset campaign win counter after game over");
                    }
                }
                
                ReinitializeEnvironment();
                _gameOverOccurred = false; // Reset flag after reinitialization
                _droneStartedForLevel = true; // Mark drone as started after reinitialization
            }
            else if (!_droneStartedForLevel)
            {
                // First round of level: start drone (drone should not start automatically when scene loads)
                if (dronePlayer != null)
                {
                    dronePlayer.StartDrone();
                    _droneStartedForLevel = true; // Mark drone as started
                }
            }
            // If _droneStartedForLevel is true, drone is already playing - don't restart it

            // Reset state
            state = State.Idle;
            inputIndex = 0;
            
            if (pianoUI != null)
            {
                pianoUI.ShowImmediate();
                // Immediately lock the keyboard so it appears at faded opacity (prevents flash of full opacity)
                // Use 0f fade time to make it instant
                pianoUI.LockInput(true, fadeTime: 0f);
            }
            
            // Seed & generator config
            if (melodyGen != null)
            {
                melodyGen.Seed = Random.Range(int.MinValue, int.MaxValue);
                melodyGen.Length = Mathf.Max(1, noteCount);
                if (log) Debug.Log($"[Dictation] New random seed set: {melodyGen.Seed}");
            }

            // Note: wrongGuessesThisLevel is NOT reset here - it persists across rounds within the same level
            drainAccumulator = 0f;
            wrongNotePenaltiesThisRound = 0;
            replayPenaltiesThisRound = 0;

            // Candles are NOT re-lit at round start - they persist across rounds within the same level

            // Reset time limit timer (will start when Listening state begins)
            roundTimeLimitRemainingSec = roundTimeLimitSec;
            timeLimitStarted = false;
            lastLogTime = 0f;

            BuildMelody();
            
            // Safety check: ensure melody was generated
            if (melody == null || melody.Count == 0)
            {
                Debug.LogError("[Dictation] Failed to generate melody in StartRound. Cannot start round.");
                return;
            }

            // Build cards for this melody
            StartCoroutine(BuildCards(melody.ToArray()));

            // Compute round time budget from potential points (for point deduction calculation only)
            float potential = Mathf.Max(0, pointsPerNote) * melody.Count;
            float drainRate = Mathf.Abs(pointsPerSecondInput);
            if (drainRate < 0.001f) drainRate = 0.001f;
            roundTimeBudgetSec = potential / drainRate;
            roundTimeRemainingSec = roundTimeBudgetSec;

            if (log) Debug.Log($"[Dictation] Round time budget = {roundTimeBudgetSec:F1}s (mel={melody.Count}, potential={potential}, drain/s={drainRate})");
            if (log) Debug.Log($"[Dictation] Time limit = {roundTimeLimitSec:F1}s");

            // First play uses full pre-roll
            PlayMelodyFromTop(isReplay: false);
        }

        private void OnReplayButtonPressed()
        {
            // Can't replay if no melody has been generated yet
            if (melody == null || melody.Count == 0)
            {
                if (log) Debug.LogWarning("[Dictation] Cannot replay: No melody generated yet. Starting a new round instead.");
                // If no melody exists, try to start a round instead
                if (state == State.Idle)
                {
                    StartRoundInternal();
                }
                return;
            }
            
            // Track replay penalty (don't apply until round completion)
            replayPenaltiesThisRound += Mathf.Abs(pointsReplay); // Accumulate as positive value
            UpdateScoreUI();
            ReplayMelody();
        }

        private void ReplayMelody()
        {
            // Safety check: ensure melody exists
            if (melody == null || melody.Count == 0)
            {
                if (log) Debug.LogWarning("[Dictation] Cannot replay: No melody to replay.");
                return;
            }
            
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
            
            // Start time limit timer when Listening state begins (only on first entry)
            if (!timeLimitStarted)
            {
                timeLimitStarted = true;
                roundTimeLimitRemainingSec = roundTimeLimitSec;
                lastLogTime = Time.time;
                if (log) Debug.Log($"[Dictation] Time limit started: {roundTimeLimitSec:F1}s");
            }
            
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

            // Campaign mode: if level is complete, check for unlock and show appropriate screens
            if (isCampaignMode && levelJustCompleted)
            {
                if (log) Debug.Log("[Dictation] Level complete.");
                
                var campaignService = FindFirstObjectByType<Sonoria.Dictation.CampaignService>();
                
                // Check if all levels in the current node are complete
                bool allLevelsComplete = false;
                if (campaignService != null)
                {
                    int nextLevel = campaignService.GetCurrentNodeNextLevel();
                    allLevelsComplete = (nextLevel < 0); // -1 means all levels complete
                }
                
                // Check if a new node was unlocked
                if (newlyUnlockedNodeIndex >= 0 && unlockAnnouncement != null)
                {
                    // Get the mode name and ScaleMode enum for the newly unlocked node
                    if (campaignService != null)
                    {
                        string modeName = campaignService.GetNodeModeName(newlyUnlockedNodeIndex);
                        EarFPS.ScaleMode mode = campaignService.GetNodeMode(newlyUnlockedNodeIndex);
                        
                        if (!string.IsNullOrEmpty(modeName))
                        {
                            if (log) Debug.Log($"[Dictation] New node unlocked: {modeName}. Showing unlock announcement.");
                            
                            // Show unlock announcement with keyboard display, then show end screen when Continue is clicked
                            unlockAnnouncement.ShowUnlock(modeName, mode, () =>
                            {
                                // After unlock announcement is dismissed, show end screen
                                if (endScreen != null)
                                {
                                    endScreen.ShowDictation(score, roundsCompleted, Time.time - runStartTime, "Level Complete");
                                }
                                // Reset flags
                                levelJustCompleted = false;
                                newlyUnlockedNodeIndex = -1;
                            });
                            
                            // Don't continue - unlock announcement callback will handle showing end screen
                            yield break;
                        }
                    }
                }
                
                // Check if all levels in current node are complete (show completion announcement)
                if (allLevelsComplete && unlockAnnouncement != null && campaignService != null)
                {
                    int currentNodeIndex = campaignService.CurrentNodeIndex;
                    if (currentNodeIndex >= 0)
                    {
                        string modeName = campaignService.GetNodeModeName(currentNodeIndex);
                        EarFPS.ScaleMode mode = campaignService.GetNodeMode(currentNodeIndex);
                        
                        if (!string.IsNullOrEmpty(modeName))
                        {
                            if (log) Debug.Log($"[Dictation] All levels in node {currentNodeIndex} ({modeName}) are complete. Showing completion announcement.");
                            
                            // Show completion announcement with keyboard display, then show end screen when Continue is clicked
                            unlockAnnouncement.ShowCompletion(modeName, mode, () =>
                            {
                                // After completion announcement is dismissed, show end screen
                                if (endScreen != null)
                                {
                                    endScreen.ShowDictation(score, roundsCompleted, Time.time - runStartTime, "Level Complete");
                                }
                                // Reset flags
                                levelJustCompleted = false;
                                newlyUnlockedNodeIndex = -1;
                            });
                            
                            // Don't continue - completion announcement callback will handle showing end screen
                            yield break;
                        }
                    }
                }
                
                // No unlock/completion or unlock announcement not available - show end screen directly
                if (log) Debug.Log("[Dictation] Showing endscreen.");
                levelJustCompleted = false; // Reset flag
                newlyUnlockedNodeIndex = -1;
                
                // Show endscreen with "Level Complete" title
                if (endScreen != null)
                {
                    endScreen.ShowDictation(score, roundsCompleted, Time.time - runStartTime, "Level Complete");
                }
                yield break; // Don't start next round
            }

            // Next round (first play again uses full pre-roll)
            StartRoundInternal();
        }

        // ---------------- Melody ----------------
        
        /// <summary>
        /// Compares two melodies for exact sequence equality (same length, same MIDI notes in same order).
        /// </summary>
        private bool MelodiesAreEqual(List<int> a, List<int> b)
        {
            // Both null or both empty
            if ((a == null || a.Count == 0) && (b == null || b.Count == 0))
                return true;
            
            // One is null/empty and the other isn't
            if (a == null || b == null || a.Count != b.Count)
                return false;
            
            // Compare each MIDI note in sequence
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            
            return true;
        }
        
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

                    // If exact length, check for duplicate
                    if (melody.Count == target)
                    {
                        // Check if this melody matches the previous one (if duplicate prevention is enabled)
                        if (preventDuplicateMelodies && previousMelody != null && MelodiesAreEqual(melody, previousMelody))
                        {
                            Debug.Log($"[Dictation] Duplicate melody detected, regenerating... (attempt {attempt + 1}/{MaxAttempts})");
                            
                            // Reseed and continue loop to regenerate
                            var seedProp = melodyGen.GetType().GetProperty(
                                "Seed",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                            );
                            if (seedProp != null && seedProp.CanWrite)
                            {
                                int newSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                                seedProp.SetValue(melodyGen, newSeed);
                            }
                            continue; // Try again with new seed
                        }
                        
                        // Not a duplicate (or duplicate prevention disabled), we're done
                        break;
                    }

                    // If too many, trim and check for duplicate
                    if (melody.Count > target)
                    {
                        melody.RemoveRange(target, melody.Count - target);
                        
                        // Check for duplicate after trimming
                        if (preventDuplicateMelodies && previousMelody != null && MelodiesAreEqual(melody, previousMelody))
                        {
                            if (log) Debug.Log($"[Dictation] Duplicate melody detected after trim, regenerating... (attempt {attempt + 1}/{MaxAttempts})");
                            
                            // Reseed and continue loop to regenerate
                            var seedProp = melodyGen.GetType().GetProperty(
                                "Seed",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                            );
                            if (seedProp != null && seedProp.CanWrite)
                            {
                                int newSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                                seedProp.SetValue(melodyGen, newSeed);
                            }
                            continue; // Try again with new seed
                        }
                        
                        // Not a duplicate, we're done
                        break;
                    }

                    // Too short: reseed (if the generator exposes a Seed) and try again
                    // This uses reflection so it's safe even if Seed doesn't exist.
                    var seedPropShort = melodyGen.GetType().GetProperty(
                        "Seed",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    );
                    if (seedPropShort != null && seedPropShort.CanWrite)
                    {
                        int newSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                        seedPropShort.SetValue(melodyGen, newSeed);
                    }
                    // otherwise rely on internal RNG differences and loop again
                }

                // Final guard: if still short, pad by repeating the last pitch (stable for testing)
                while (melody.Count < target)
                {
                    int last = melody.Count > 0 ? melody[melody.Count - 1] : baseNote;
                    melody.Add(last);
                }
                
                // Check for duplicate after padding (if duplicate prevention is enabled)
                if (preventDuplicateMelodies && previousMelody != null && MelodiesAreEqual(melody, previousMelody))
                {
                    if (log) Debug.Log($"[Dictation] Duplicate melody detected after padding, regenerating...");
                    
                    // Reseed one more time and regenerate
                    var seedProp = melodyGen.GetType().GetProperty(
                        "Seed",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    );
                    if (seedProp != null && seedProp.CanWrite)
                    {
                        int newSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                        seedProp.SetValue(melodyGen, newSeed);
                        
                        // Regenerate once more (this is a safety attempt beyond MaxAttempts)
                        var gen = melodyGen.Generate();
                        melody.Clear();
                        if (gen != null)
                        {
                            for (int i = 0; i < gen.Count && i < target; i++)
                                melody.Add(gen[i].midi);
                        }
                        
                        // Pad again if needed
                        while (melody.Count < target)
                        {
                            int last = melody.Count > 0 ? melody[melody.Count - 1] : baseNote;
                            melody.Add(last);
                        }
                        
                        // If still duplicate after regeneration, accept it (avoid infinite loop)
                        if (MelodiesAreEqual(melody, previousMelody))
                        {
                            if (log) Debug.LogWarning($"[Dictation] Unable to generate non-duplicate melody after additional attempt, accepting duplicate.");
                        }
                    }
                }
            }
            else
            {
                // Fallback: random C-major in baseNote..baseNote+range
                const int FallbackMaxAttempts = 10;
                for (int attempt = 0; attempt < FallbackMaxAttempts; attempt++)
                {
                    melody.Clear();
                    for (int i = 0; i < target; i++)
                    {
                        int semis = Random.Range(0, rangeSemitones + 1);
                        int note = baseNote + semis;
                        note = ForceToCMajor(note);
                        melody.Add(note);
                    }
                    
                    // Check for duplicate in fallback path
                    if (preventDuplicateMelodies && previousMelody != null && MelodiesAreEqual(melody, previousMelody))
                    {
                        if (log && attempt < FallbackMaxAttempts - 1)
                            Debug.Log($"[Dictation] Duplicate melody in fallback generation, regenerating... (attempt {attempt + 1}/{FallbackMaxAttempts})");
                        continue; // Try again
                    }
                    
                    // Not a duplicate (or duplicate prevention disabled), we're done
                    break;
                }
                
                // If all attempts were duplicates, accept the last one (avoid infinite loop)
                if (preventDuplicateMelodies && previousMelody != null && MelodiesAreEqual(melody, previousMelody))
                {
                    if (log) Debug.LogWarning($"[Dictation] Unable to generate non-duplicate melody in fallback path after {FallbackMaxAttempts} attempts, accepting duplicate.");
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
            // Prevent multiple calls - if game over already occurred, return early
            if (_gameOverOccurred) return;
            _gameOverOccurred = true;
            
            if (log) Debug.Log("[Dictation] GAME OVER");
            state = State.Idle;
            if (playingCo != null) StopCoroutine(playingCo);

            if (dronePlayer != null) dronePlayer.Stop();

            // ShowMessage("Game Over", messageWrongColor);
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
                    // Only extinguish if still lit (in case candles were used as hitpoints)
                    if (c.IsLit)
                    {
                        float delay = Random.Range(0f, goExtinguishSpread);
                        maxDelay = Mathf.Max(maxDelay, delay);
                        c.Extinguish(delay, goExtinguishFade);
                    }
                }
                yield return new WaitForSeconds(maxDelay + goExtinguishFade + goPostExtinguishPause);
            }

            // (Next: reveal/glow + sequential card flip + correct melody)

            goCinematicRunning = false;
            if (endScreen != null)
                endScreen.ShowDictation(score, roundsCompleted, Time.time - runStartTime);
        }

        // ---------------- Environment Reinitialization ----------------
        /// <summary>
        /// Gets a list of all currently lit candles.
        /// </summary>
        private List<CandleFlicker> GetLitCandles()
        {
            List<CandleFlicker> litCandles = new List<CandleFlicker>();
            foreach (var candle in candles)
            {
                if (candle != null && candle.IsLit)
                {
                    litCandles.Add(candle);
                }
            }
            return litCandles;
        }

        /// <summary>
        /// Extinguishes 2 random lit candles on wrong guess (if enabled).
        /// Uses violent flicker animation before extinguishing.
        /// </summary>
        private void ExtinguishCandlesOnWrongGuess()
        {
            List<CandleFlicker> litCandles = GetLitCandles();
            
            if (litCandles.Count == 0)
            {
                if (log) Debug.LogWarning("[Dictation] No lit candles to extinguish on wrong guess!");
                return;
            }

            // Determine how many candles to extinguish (1 per wrong guess, or remaining if less than 1)
            int candlesToExtinguish = Mathf.Min(1, litCandles.Count);

            // Randomly select candles to extinguish
            List<CandleFlicker> candlesToExt = new List<CandleFlicker>();
            for (int i = 0; i < candlesToExtinguish; i++)
            {
                int randomIndex = Random.Range(0, litCandles.Count);
                candlesToExt.Add(litCandles[randomIndex]);
                litCandles.RemoveAt(randomIndex); // Remove to avoid selecting same candle twice
            }

            // Start violent flicker and schedule extinguishing for each selected candle
            foreach (var candle in candlesToExt)
            {
                if (candle != null && candle.IsLit)
                {
                    // Start violent flicker immediately (synchronized with lightning)
                    candle.ViolentFlicker(wrongGuessFlickerDuration, wrongGuessFlickerAmpMul, wrongGuessFlickerSpeedMul, wrongGuessFlickerBaseMul);
                    
                    // Schedule extinguishing after part of flicker duration (overlap for earlier extinguish)
                    float extinguishDelay = wrongGuessFlickerDuration * wrongGuessFlickerOverlap;
                    StartCoroutine(ExtinguishCandleAfterFlicker(candle, extinguishDelay, wrongGuessExtinguishFade));
                }
            }

            if (log) Debug.Log($"[Dictation] Extinguishing {candlesToExt.Count} candle(s) on wrong guess. {GetLitCandles().Count - candlesToExtinguish} remaining.");
        }

        /// <summary>
        /// Coroutine that waits for flicker duration, then extinguishes the candle.
        /// </summary>
        private IEnumerator ExtinguishCandleAfterFlicker(CandleFlicker candle, float flickerDuration, float fadeDuration)
        {
            yield return new WaitForSeconds(flickerDuration);
            
            if (candle != null && candle.IsLit)
            {
                candle.Extinguish(0f, fadeDuration);
            }
        }

        /// <summary>
        /// Reinitializes the environment after a game over.
        /// Reignites all candles and restarts the drone sound.
        /// </summary>
        private void ReinitializeEnvironment()
        {
            if (log) Debug.Log("[Dictation] Reinitializing environment after game over...");

            // Reignite all candles with fade-in transition
            foreach (var candle in candles)
            {
                if (candle != null)
                {
                    candle.Ignite(); // Uses default fade-in duration
                }
            }

            // Restart drone player
            if (dronePlayer != null)
            {
                dronePlayer.StartDrone();
            }

            if (log) Debug.Log("[Dictation] Environment reinitialized.");
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
