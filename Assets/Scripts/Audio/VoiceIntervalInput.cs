// Assets/Scripts/Player/VoiceIntervalInput.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace EarFPS
{
    public class VoiceIntervalInput : MonoBehaviour
    {
        [Header("Game")]
        [SerializeField] IntervalQuizController quiz;

        [Header("Controls")]
        [SerializeField] Key pushToTalkKey = Key.V;

        [Header("UI")]
        [SerializeField] VoiceUI voiceUI;
        [SerializeField] bool showHeardToast = false;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [Header("Recognizer")]
        [SerializeField] ConfidenceLevel confidence = ConfidenceLevel.Medium;
        KeywordRecognizer recognizer;
#endif

        [Header("Debug")]
        [SerializeField] bool debugLogs = false;

        // ---------- Grammar ----------
        static readonly (int semis, string[] phrases)[] Grammar =
        {
            (0,  new[] { "unison", "perfect unison", "p1", "first" }),
            (1,  new[] { "minor second", "m2", "semitone", "half step" }),
            (2,  new[] { "major second", "m2 whole", "whole tone", "whole step", "m2 up", "m2", "m two", "major two", "M2" }),
            (3,  new[] { "minor third", "m3", "flat third" }),
            (4,  new[] { "major third", "M3" }),
            (5,  new[] { "perfect fourth", "p4", "fourth" }),
            (6,  new[] { "tritone", "augmented fourth", "diminished fifth" }),
            (7,  new[] { "perfect fifth", "p5", "fifth" }),
            (8,  new[] { "minor sixth", "m6" }),
            (9,  new[] { "major sixth", "M6" }),
            (10, new[] { "minor seventh", "m7" }),
            (11, new[] { "major seventh", "M7" }),
            (12, new[] { "octave", "perfect octave", "p8", "eighth" }),
            (13, new[] { "minor ninth", "m9" }),
            (14, new[] { "major ninth", "M9" }),
        };

        Dictionary<string, int> phraseToSemis;
        VoiceUI _vui;

        // Listening state
        bool isListening;

        // ---------- Main-thread queue (fixes threading) ----------
        struct PendingCommand { public string raw; public int semis; public bool mapped; }
        readonly Queue<PendingCommand> _queue = new Queue<PendingCommand>(4);
        readonly object _lock = new object();

        // ---------- Lifecycle ----------
        void Awake()
        {
            // Build grammar lookup
            phraseToSemis = new Dictionary<string, int>();
            foreach (var g in Grammar)
                foreach (var p in g.phrases)
                    phraseToSemis[p.ToLowerInvariant()] = g.semis;

            _vui = voiceUI ? voiceUI : FindFirstObjectByType<VoiceUI>();
            Log($"Grammar ready ({phraseToSemis.Count} phrases).");
        }

        void OnDestroy()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (recognizer != null)
            {
                recognizer.OnPhraseRecognized -= OnPhraseRecognized;
                if (recognizer.IsRunning) recognizer.Stop();
                recognizer.Dispose();
            }
#endif
        }

        void Update()
        {

            if (quiz == null)
            {
                if (debugLogs) Debug.LogWarning("[Voice] Quiz is null; skipping.");
                return;
            }

            var kb = Keyboard.current;
            if (kb == null || quiz == null) return;

            // PTT
            if (kb[pushToTalkKey].wasPressedThisFrame) StartListening();
            if (kb[pushToTalkKey].wasReleasedThisFrame) StopListening();

            // Consume any pending command queued by the recognizer thread
            PendingCommand? job = null;
            lock (_lock)
            {
                if (_queue.Count > 0) job = _queue.Dequeue();
            }
            if (job.HasValue) HandleVoiceCommand(job.Value);

            if (job.HasValue)
            {
                HandleVoiceCommand(job.Value);
            }
        }

        // ---------- Control ----------
        void StartListening()
        {
            if (isListening) return;
            isListening = true;

            quiz.SetVoiceListening(true);
            _vui?.SetListening(true);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (recognizer == null)
            {
                var phrases = new List<string>(phraseToSemis.Keys).ToArray();
                recognizer = new KeywordRecognizer(phrases, confidence);
                recognizer.OnPhraseRecognized += OnPhraseRecognized;
                Log($"KeywordRecognizer created (phrases={phrases.Length}, conf={confidence})");
            }
            if (!recognizer.IsRunning)
            {
                recognizer.Start();
                Log("Recognizer.Start()");
            }
#else
            Log("Voice recognition requires Windows (KeywordRecognizer).");
#endif
        }

        void StopListening()
        {
            if (!isListening) return;
            isListening = false;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (recognizer != null && recognizer.IsRunning)
            {
                recognizer.Stop();
                Log("Recognizer.Stop()");
            }
#endif
            quiz.SetVoiceListening(false);
            _vui?.SetListening(false);
        }

        // ---------- Recognizer thread → queue to main thread ----------
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        void OnPhraseRecognized(PhraseRecognizedEventArgs args)
        {
            string heard = args.text ?? "";
            string key = heard.ToLowerInvariant();

            // DO NOT touch Unity objects here (background thread)
            bool mapped = phraseToSemis.TryGetValue(key, out int semis);

            if (debugLogs) Debug.Log($"[Voice] heard: \"{heard}\" conf={args.confidence}");

            // Queue for Update() on main thread
            lock (_lock)
            {
                // keep it bounded so it can't grow forever
                if (_queue.Count < 8) _queue.Enqueue(new PendingCommand { raw = heard, semis = semis, mapped = mapped });
            }
        }
#endif

        // ---------- Helpers ----------
        static IntervalDef? FindDefBySemitones(int semis)
        {
            for (int i = 0; i < IntervalTable.Count; i++)
            {
                var d = IntervalTable.ByIndex(i);
                if (d.semitones == semis) return d;
            }
            return null;
        }

        void Log(string msg)
        {
            if (debugLogs) Debug.Log(msg);
        }

        void HandleVoiceCommand(PendingCommand cmd)
        {
            var ui = _vui; // your property or reference to VoiceUI

            // 1) Not mapped at all
            if (!cmd.mapped)
            {
                ui?.ShowResult(false, "Unknown");
                if (showHeardToast) UIHud.Instance?.Toast($"Voice: \"{cmd.raw}\" ?");
                return;
            }

            // 2) Find interval def by semitones
            var def = FindDefBySemitones(cmd.semis);
            if (def == null)
            {
                ui?.ShowResult(false, "Unmapped");
                if (showHeardToast) UIHud.Instance?.Toast($"Voice unmapped ({cmd.raw})");
                return;
            }

            // 3) Submit to the quiz; show result immediately
            bool ok = quiz.TrySubmitInterval(def.Value);
            ui?.ShowResult(ok, def.Value.displayName);
            if (showHeardToast) UIHud.Instance?.Toast($"Voice → {def.Value.displayName}");
        }

        void OnDisable()
        {
        #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (recognizer != null && recognizer.IsRunning) recognizer.Stop();
        #endif
            isListening = false;
            quiz?.SetVoiceListening(false);
            _vui?.SetListening(false);
        }


    }
    
    
}
