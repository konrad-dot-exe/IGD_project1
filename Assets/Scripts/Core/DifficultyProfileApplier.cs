using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TMPro;
using EarFPS;


namespace Sonoria.Dictation {

[DefaultExecutionOrder(-1000)]

    public class DifficultyProfileApplier : MonoBehaviour
    {
        [Header("Assign Presets in Inspector")] public DifficultyProfile[] presets;
        public int currentIndex = 0;

        [Header("Scene Refs")] public MonoBehaviour melodyGenerator;  // assign MelodyGenerator
        public MonoBehaviour dictationController;                     // assign MelodicDictationController
        public PianoKeyboardUI pianoKeyboardUI;                       // assign PianoKeyboardUI (optional)

        [Header("Hotkeys")] public KeyCode applyAndStartKey = KeyCode.Space;
        public KeyCode replayKey = KeyCode.R;

        [Header("Auto Apply")]
        public bool applyInAwake = true;        // apply profile in Awake (before others Start)
        public bool startRoundOnStart = false;  // only use if controller won't autostart

        [Header("Auto Advance")]
        public bool autoAdvanceOnRounds = true;     // enable/disable feature
        [Min(1)] public int roundsPerLevel = 5;     // how many completed rounds before advancing
        public bool wrapAtEnd = false;              // if false, clamp at last preset

        // runtime trackers
        int _roundsSeen = 0;
        int _lastControllerRounds = 0;

        [Header("HUD UI")]
        public TextMeshProUGUI levelLabel;

        // reflection cache
        Type genType; Type ctrlType; object gen; object ctrl;
        MethodInfo miGenSetMode; MethodInfo miCtrlStartRound;

        void Awake()
        {
            gen = melodyGenerator; ctrl = dictationController;
            genType = gen?.GetType();
            ctrlType = ctrl?.GetType();
            miGenSetMode = genType?.GetMethod("SetMode", BindingFlags.Public | BindingFlags.Instance);
            miCtrlStartRound = ctrlType?.GetMethod("StartRound",
                               BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (applyInAwake) ApplyNow();   // push values into controller + generator
        }

        void Start()
        {
            if (startRoundOnStart) StartRound(); // use ONLY if controller doesn't autostart

            // initialize controller rounds snapshot so we don't insta-advance
            _lastControllerRounds = ReadControllerRounds();
        }

        void Update()
        {
            // 1..9 to switch presets quickly
            for (int i = 0; i < Mathf.Min(9, presets?.Length ?? 0); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    currentIndex = i;
                    Toast("Preset: " + (SafeCurrent()?.displayName ?? $"#{i + 1}"));
                }
            }
            if (Input.GetKeyDown(applyAndStartKey)) { ApplyNow(); StartRound(); }
            if (Input.GetKeyDown(replayKey)) { Call(ctrl, "Replay"); }
        }

        // add this method anywhere in the class
        void LateUpdate()
        {
            if (levelLabel != null)
            {
                // keep it simple and unambiguous: Level is just the preset index + 1
                levelLabel.SetText($"Level: {currentIndex + 1}");
            }
        }

        int ReadControllerRounds()
        {
            if (ctrl == null) return 0;
            var f = ctrl.GetType().GetField(
                "roundsCompleted",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            return f != null ? (int)f.GetValue(ctrl) : 0;
        }

        void AdvancePresetIndex()
        {
            int next = currentIndex + 1;
            if (next >= presets.Length)
            {
                if (wrapAtEnd) next = 0; else next = presets.Length - 1;
            }
            if (next != currentIndex)
            {
                currentIndex = next;
                Toast($"Advanced to: {SafeCurrent()?.displayName ?? $"Level {currentIndex + 1}"}", 1.5f);
                ApplyNow();          // push new profile immediately
                // Optionally auto-start a new round here:
                // StartRound();
            }
            // if (levelLabel != null)
            //     levelLabel.text = $"Level: {SafeCurrent()?.displayName ?? $"#{currentIndex + 1}"}";
        }

        public DifficultyProfile SafeCurrent()
        {
            if (presets == null || presets.Length == 0) return null;
            currentIndex = Mathf.Clamp(currentIndex, 0, presets.Length - 1);
            return presets[currentIndex];
        }

        /// <summary>
        /// Applies a specific DifficultyProfile directly (used by CampaignService).
        /// </summary>
        /// <param name="profile">The difficulty profile to apply</param>
        /// <param name="modeOverride">Optional scale mode override. If provided (and not null), this mode will be used instead of the profile's allowedModes.</param>
        public void ApplyProfile(DifficultyProfile profile, EarFPS.ScaleMode? modeOverride = null)
        {
            if (profile == null || gen == null || ctrl == null) return;
            ApplyProfileInternal(profile, modeOverride);
        }

        [ContextMenu("Apply Now")]
        public void ApplyNow()
        {
            var p = SafeCurrent(); if (p == null || gen == null || ctrl == null) return;
            ApplyProfileInternal(p, null); // No override when using presets
        }

        /// <summary>
        /// Internal method that applies a profile to generator and controller.
        /// </summary>
        /// <param name="p">The difficulty profile to apply</param>
        /// <param name="modeOverride">Optional scale mode override. If provided, this mode will be used instead of the profile's allowedModes.</param>
        void ApplyProfileInternal(DifficultyProfile p, EarFPS.ScaleMode? modeOverride = null)
        {
            if (p == null || gen == null || ctrl == null) return;

            // ---- Controller fields
            Set(ctrl, "preRollSeconds", p.preRollSeconds);
            Set(ctrl, "replayPreRollMultiplier", p.replayPreRollMultiplier);
            Set(ctrl, "noteGap", p.noteGap);
            Set(ctrl, "playbackVelocity", p.playbackVelocity);
            Set(ctrl, "pointsPerNote", p.pointsPerNote);
            Set(ctrl, "pointsWrongNote", p.pointsWrongNote);
            Set(ctrl, "pointsReplay", p.pointsReplay);
            Set(ctrl, "pointsPerSecondInput", p.pointsPerSecondInput);
            Set(ctrl, "maxWrongPerRound", p.maxWrongPerRound);
            Set(ctrl, "roundTimeLimitSec", p.timeLimitPerRound);

            // ---- Generator fields
            Set(gen, "Length", p.melodyLength);
            Set(ctrl, "noteCount", p.melodyLength);
            Set(gen, "noteDurationSec", p.noteDuration);
            Set(gen, "RandomizeModeEachRound", p.randomizeModeEachRound);
            Set(gen, "registerMinMidi", p.registerMinMidi);
            Set(gen, "registerMaxMidi", p.registerMaxMidi);
            Set(gen, "difficulty", p.difficulty);
            Set(gen, "contour", p.contour);
            Set(gen, "enableTendencyEngine", p.enableTendencies);
            Set(gen, "tendencyResolveProbability", p.tendencyResolveProbability);
            Set(gen, "allowSmallDetours", p.allowSmallDetours);

            // Modes
            EarFPS.ScaleMode modeToUse = EarFPS.ScaleMode.Ionian; // fallback
            bool shouldSetMode = false;

            if (modeOverride.HasValue)
            {
                // Use the override mode if provided
                modeToUse = modeOverride.Value;
                shouldSetMode = true;
            }
            else if (!p.randomizeModeEachRound && p.allowedModes != null && p.allowedModes.Length > 0)
            {
                // Use the profile's first allowed mode
                modeToUse = p.allowedModes[0];
                shouldSetMode = true;
            }

            if (shouldSetMode)
            {
                if (miGenSetMode != null) miGenSetMode.Invoke(gen, new object[] { modeToUse });
                else Set(gen, "mode", modeToUse);
            }

            // Degree masks (requires helpers on generator; no-op if missing)
            Call(gen, "SetAllowedDegrees", p.allowedDegrees);
            Call(gen, "SetAllowedStartDegrees", p.allowedStartDegrees);
            Call(gen, "SetAllowedEndDegrees", p.allowedEndDegrees);

            // Movement Policy
            if (gen != null && genType != null)
            {
                // Set movement policy
                var setMovementPolicyMethod = genType.GetMethod("SetMovementPolicy", 
                    BindingFlags.Public | BindingFlags.Instance);
                if (setMovementPolicyMethod != null)
                {
                    setMovementPolicyMethod.Invoke(gen, new object[] { p.movementPolicy });
                }
                else
                {
                    // Fallback: use reflection to set field directly
                    Set(gen, "movementPolicy", p.movementPolicy);
                }

                // Set max leap steps
                var setMaxLeapStepsMethod = genType.GetMethod("SetMaxLeapSteps", 
                    BindingFlags.Public | BindingFlags.Instance);
                if (setMaxLeapStepsMethod != null)
                {
                    setMaxLeapStepsMethod.Invoke(gen, new object[] { p.maxLeapSteps });
                }
                else
                {
                    // Fallback: use reflection to set field directly
                    Set(gen, "maxLeapSteps", p.maxLeapSteps);
                }
            }

            // Update keyboard opacity for featured notes
            if (pianoKeyboardUI != null)
            {
                // Get current mode from generator (handles randomized modes)
                EarFPS.ScaleMode currentMode = modeToUse; // Use the mode we just set
                if (gen != null)
                {
                    var modeProp = genType?.GetProperty("CurrentMode", BindingFlags.Public | BindingFlags.Instance);
                    if (modeProp != null)
                    {
                        var modeValue = modeProp.GetValue(gen);
                        // Convert via int to handle assembly reference differences between reflection and direct types
                        int modeIntValue = (int)modeValue;
                        currentMode = (EarFPS.ScaleMode)modeIntValue;
                    }
                }

                // Convert to int to avoid assembly reference differences, use int overload
                int finalModeInt = (int)currentMode;
                pianoKeyboardUI.SetFeaturedNotes(finalModeInt, p.allowedDegrees, p.registerMinMidi, p.registerMaxMidi);
            }

            // Update the Level text
            // if (levelLabel != null)
            //     levelLabel.text = $"Level: {SafeCurrent()?.displayName ?? $"#{currentIndex + 1}"}";
        }

        public void StartRound()
        {
            if (miCtrlStartRound != null) miCtrlStartRound.Invoke(ctrl, null);
            else Call(ctrl, "StartNextRound");
            RoundLogger.LogSnapshot(SafeCurrent(), gen, ctrl);
        }


        public void NotifyRoundCompleted()
        {
            if (!autoAdvanceOnRounds) return;

            _roundsSeen++;
            if (_roundsSeen >= Mathf.Max(1, roundsPerLevel))
            {
                _roundsSeen = 0;
                AdvancePresetIndex();     // bumps currentIndex, calls ApplyNow(), updates Level TMP
                // If you also want to auto-start the next round:
                // StartRound();
            }
        }

        // --- reflection helpers ---
        void Set(object target, string fieldOrProp, object value)
        {
            if (target == null) return; var t = target.GetType();
            var p = t.GetProperty(fieldOrProp, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanWrite) { p.SetValue(target, value); return; }
            var f = t.GetField(fieldOrProp, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) { f.SetValue(target, value); return; }
        }

        bool Call(object target, string method, params object[] args)
        {
            if (target == null) return false; var t = target.GetType();
            var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null) return false; m.Invoke(target, args); return true;
        }

        // // tiny toast
        float toastUntil = 0f; string toastText;
        public void Toast(string msg, float seconds = 1.2f) { toastText = msg; toastUntil = Time.time + seconds; }
        //void OnGUI() { if (Time.time < toastUntil) GUI.Label(new Rect(16, 16, 600, 30), toastText); }
    }
}
