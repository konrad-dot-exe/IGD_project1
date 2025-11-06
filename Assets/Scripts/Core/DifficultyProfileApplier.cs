using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TMPro;


namespace Sonoria.Dictation {

[DefaultExecutionOrder(-1000)]

    public class DifficultyProfileApplier : MonoBehaviour
    {
        [Header("Assign Presets in Inspector")] public DifficultyProfile[] presets;
        public int currentIndex = 0;

        [Header("Scene Refs")] public MonoBehaviour melodyGenerator;  // assign MelodyGenerator
        public MonoBehaviour dictationController;                     // assign MelodicDictationController

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

        [ContextMenu("Apply Now")]
        public void ApplyNow()
        {
            var p = SafeCurrent(); if (p == null || gen == null || ctrl == null) return;

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

            // ---- Generator fields
            Set(gen, "Length", p.melodyLength);
            Set(ctrl, "noteCount", p.melodyLength);
            Set(gen, "noteDurationSec", p.noteDuration);
            Set(gen, "RandomizeModeEachRound", p.randomizeModeEachRound);
            Set(gen, "registerMinMidi", p.registerMinMidi);
            Set(gen, "registerMaxMidi", p.registerMaxMidi);
            Set(gen, "difficulty", p.difficulty);
            Set(gen, "contour", p.contour);
            Set(gen, "allowLeaps", p.allowLeaps);
            Set(gen, "enableTendencyEngine", p.enableTendencies);
            Set(gen, "tendencyResolveProbability", p.tendencyResolveProbability);
            Set(gen, "allowSmallDetours", p.allowSmallDetours);

            // Modes
            if (!p.randomizeModeEachRound && p.allowedModes != null && p.allowedModes.Length > 0)
            {
                if (miGenSetMode != null) miGenSetMode.Invoke(gen, new object[] { p.allowedModes[0] });
                else Set(gen, "mode", p.allowedModes[0]);
            }

            // Degree masks (requires helpers on generator; no-op if missing)
            Call(gen, "SetAllowedDegrees", p.allowedDegrees);
            Call(gen, "SetAllowedStartDegrees", p.allowedStartDegrees);
            Call(gen, "SetAllowedEndDegrees", p.allowedEndDegrees);

            // Movement override
            var steps = MovementToSteps(p.movement);
            Call(gen, "SetAllowedStepSizes", steps);

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

        List<int> MovementToSteps(MovementSet set)
        {
            switch (set)
            {
                case MovementSet.Stepwise: return new List<int> { 1 };
                case MovementSet.StepwisePlusThirds: return new List<int> { 1, 2 };
                case MovementSet.DiatonicUpToFifths: return new List<int> { 1, 2, 3, 4, 5 };
                default: return new List<int> { 1, 2, 3, 4, 5, 6, 7 };
            }
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
