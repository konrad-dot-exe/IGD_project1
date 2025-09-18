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
        [SerializeField] IntervalQuizController quiz;
        [SerializeField] Key  pushToTalkKey = Key.V;
        [SerializeField] bool showHeardToast = true;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [SerializeField] ConfidenceLevel confidence = ConfidenceLevel.Medium;
#endif

        [Header("Debug")]
        [SerializeField] bool debugLogs = true;

        // --- phrase -> semitones map (extend as you like) ---
        static readonly (int semis, string[] phrases)[] Grammar =
        {
            (0,  new[] { "unison", "perfect unison", "p1", "first" }),
            (1,  new[] { "minor second", "m2", "semitone", "half step" }),
            (2,  new[] { "major second", "m a j o r second", "m2 whole", "whole tone", "whole step", "m2 up", "m2", "M2", "major two", "two" }),
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

        Dictionary<string,int> phraseToSemis;
        bool isListening = false;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        KeywordRecognizer recognizer;
#endif

        void Awake()
        {
            // Build phrase dictionary (lowercase keys)
            phraseToSemis = new Dictionary<string, int>();
            foreach (var g in Grammar)
                foreach (var p in g.phrases)
                    phraseToSemis[p.ToLowerInvariant()] = g.semis;

            Log($"Awake → grammar phrases: {phraseToSemis.Count}");
        }

        void OnDestroy()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (recognizer != null)
            {
                Log("OnDestroy → disposing recognizer");
                recognizer.OnPhraseRecognized -= OnPhraseRecognized;
                if (recognizer.IsRunning) recognizer.Stop();
                recognizer.Dispose();
            }
#endif
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || quiz == null) return;

            bool down = kb[pushToTalkKey].wasPressedThisFrame;
            bool up   = kb[pushToTalkKey].wasReleasedThisFrame;

            if (down) { Log("PTT down"); StartListening(); }
            if (up)   { Log("PTT up");   StopListening();   }
        }

        void StartListening()
        {
            if (isListening) { Log("StartListening ignored (already listening)"); return; }
            isListening = true;

            // Mute quiz beeps while we listen
            quiz.SetVoiceListening(true);   // in StartListening()
            Log("Muted quiz audio");

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (recognizer == null)
            {
                var phrases = new List<string>(phraseToSemis.Keys).ToArray();
                recognizer = new KeywordRecognizer(phrases, confidence);
                recognizer.OnPhraseRecognized += OnPhraseRecognized;
                //Log($"Created KeywordRecognizer (phrases={phrases.Length}, confidence={confidence})");
            }
            if (!recognizer.IsRunning)
            {
                recognizer.Start();
                Log("Recognizer.Start()");
            }
#else
            Log("Voice not supported on this platform (KeywordRecognizer requires Windows).");
#endif

            UIHud.Instance?.Toast("Voice: listening…");
        }

        void StopListening()
        {
            if (!isListening) { Log("StopListening ignored (not listening)"); return; }
            isListening = false;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (recognizer != null && recognizer.IsRunning)
            {
                recognizer.Stop();
                //Log("Recognizer.Stop()");
            }
#endif
            quiz.SetVoiceListening(false);  // in StopListening()
            //Log("Unmuted quiz audio");
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        void OnPhraseRecognized(PhraseRecognizedEventArgs args)
        {
            Log($"OnPhraseRecognized → text=\"{args.text}\", conf={args.confidence}, dur={args.phraseDuration.TotalMilliseconds:F0}ms");

            string heard = args.text.ToLowerInvariant();
            if (!phraseToSemis.TryGetValue(heard, out int semis))
            {
                Log($"Unmapped phrase: \"{args.text}\"");
                if (showHeardToast) UIHud.Instance?.Toast($"Voice: \"{args.text}\" ?");
                return;
            }

            // Find the IntervalDef by semitone count
            var def = FindDefBySemitones(semis);
            if (def == null)
            {
                Log($"Mapped semis={semis} but IntervalDef not found");
                if (showHeardToast) UIHud.Instance?.Toast($"Voice: unmapped ({args.text})");
                return;
            }

            Log($"Mapped → {def.Value.displayName} ({def.Value.semitones} semitones). Submitting.");
            bool ok = quiz.TrySubmitInterval(def.Value);
            Log($"Submit result: {(ok ? "attempted (target existed)" : "no target")}");
            if (showHeardToast) UIHud.Instance?.Toast($"Voice → {def.Value.displayName}");
        }
#endif

        // Helper: walk the interval table to find the one with the requested semitone distance
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
            if (!debugLogs) return;
            Debug.Log($"[Voice] {msg}");
        }
    }
}
