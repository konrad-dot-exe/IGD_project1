// MelodyGenerator.cs — Rule–Probabilistic Hybrid v1.2 (Modal)
// Adds Inspector-selectable modal scales (C Ionian/Dorian/Phrygian/Lydian/Mixolydian/Aeolian).
// Root remains C; degrees 1..7 map to the chosen mode. Anchors & difficulty logic unchanged.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarFPS
{
    public enum MelodyDifficulty { Beginner, Intermediate, Advanced }
    public enum ContourType { Any, Rising, Falling, Arch, InvertedArch, Random }
    public enum ScaleMode { Ionian, Dorian, Phrygian, Lydian, Mixolydian, Aeolian }
    

    [Serializable]
    public struct MelodyNote
    {
        public int midi;
        public float durationSec;
        public MelodyNote(int midi, float dur) { this.midi = midi; this.durationSec = dur; }
        public override string ToString() => $"{midi}";
    }

    [AddComponentMenu("EarFPS/Melody Generator (Rule-Prob Hybrid)")]
    public class MelodyGenerator : MonoBehaviour
    {
        [Header("Seed & Length")]
        [SerializeField] int seed = 12345;
        [SerializeField, Range(1, 64)] int length = 6;
        [SerializeField] float noteDurationSec = 0.8f;

        [Header("Scale / Mode (root fixed to C)")]
        [SerializeField] ScaleMode mode = ScaleMode.Ionian;   
        [SerializeField] bool randomizeModeEachRound = false;
        public bool RandomizeModeEachRound { get => randomizeModeEachRound; set => randomizeModeEachRound = value; }
        public ScaleMode CurrentMode => mode;

        [Header("Pitch Space (register)")]
        [SerializeField] int registerMinMidi = 55; // G3
        [SerializeField] int registerMaxMidi = 72; // C5

        [Header("Behavior")]
        [SerializeField] MelodyDifficulty difficulty = MelodyDifficulty.Beginner;
        [SerializeField] ContourType contour = ContourType.Random;
        [Tooltip("Probability that a leap resolves immediately by stepback; if not, next step is forced to resolve.")]
        [Range(0f, 1f)] public float immediateLeapResolutionProbability = 0.80f;

        [Header("Movement")]
        [Tooltip("Movement policy from DifficultyProfile. StepwiseOnly = only ±1 diatonic step, UpToMaxLeap = allows leaps up to maxLeapSteps.")]
        [SerializeField] Sonoria.Dictation.MovementPolicy movementPolicy = Sonoria.Dictation.MovementPolicy.UpToMaxLeap;
        
        [Tooltip("Maximum diatonic steps for leaps (from DifficultyProfile). Only used when movementPolicy is UpToMaxLeap.")]
        [Range(1, 8)]
        [SerializeField] int maxLeapSteps = 8;

        [Header("Anchors (Degrees 1..7)")]
        [SerializeField] bool[] allowedDegrees = null;
        // Public setters used by the applier (safe to call even if null)
        public void SetAllowedDegrees(bool[] mask)        { allowedDegrees = (mask != null && mask.Length == 7) ? (bool[])mask.Clone() : null; }
        public void SetAllowedStartDegrees(bool[] mask)   { allowedStartDegrees = (mask != null && mask.Length == 7) ? (bool[])mask.Clone() : null; }
        public void SetAllowedEndDegrees(bool[] mask)     { allowedEndDegrees = (mask != null && mask.Length == 7) ? (bool[])mask.Clone() : null; }
        [Tooltip("Allowed start degrees within the key (1..7). Default: 1,3,5.")]
        [SerializeField] bool[] allowedStartDegrees = new bool[7] { true, false, true, false, true, false, false };
        [Tooltip("Allowed END degrees within the key (1..7). Default: 1 only.")]
        [SerializeField] bool[] allowedEndDegrees = new bool[7] { true, false, false, false, false, false, false };
        
        [Header("Tendency Engine (Semitone Resolution)")]
        [SerializeField] bool enableTendencyEngine = true;
        [SerializeField, Range(0f,1f)] float tendencyResolveProbability = 0.80f; // first chance to resolve
        [SerializeField] bool allowSmallDetours = true;    // allow at most M3 detour on the first step
        [SerializeField] bool debugTendencies = false;     // optional logs



        // Public accessors
        public int Seed { get => seed; set => seed = value; }
        public int Length { get => length; set => length = Mathf.Clamp(value, 1, 64); }
        public bool[] AllowedStartDegrees => allowedStartDegrees;
        public bool[] AllowedEndDegrees => allowedEndDegrees;
        
        // Semitone tendency tracking
        struct TendencyObligation
        {
            public int targetDegree;     // 1..7 in current mode
            public int remainingSteps;   // 2 -> probabilistic; 1 -> mandatory
        }
        readonly Queue<TendencyObligation> _pendingTendencies = new();      // reset per melody
        List<(int upperDeg, int lowerDeg)> _semitonePairs = null;           // computed from mode

        // Internal state
        System.Random rng;
        ContourType resolvedContour;

        // --- Mode data (C-based pitch classes for degrees 1..7) ---
        // pc sets for each mode (tonic = C = degree 1)
        static readonly int[] IonianPC      = { 0,2,4,5,7,9,11 };
        static readonly int[] DorianPC      = { 0,2,3,5,7,9,10 };
        static readonly int[] PhrygianPC    = { 0,1,3,5,7,8,10 };
        static readonly int[] LydianPC      = { 0,2,4,6,7,9,11 };
        static readonly int[] MixolydianPC  = { 0,2,4,5,7,9,10 };
        static readonly int[] AeolianPC     = { 0,2,3,5,7,8,10 };

        // runtime caches derived from mode
        int[] degreeOrder;   // map degree 1..7 -> pitch-class
        bool[] pcMask;       // 12-length mask of allowed pitch classes

        // ---- Public API ----
        public List<MelodyNote> Generate()
        {

            // Pick a mode per round if enabled (deterministic from seed)
            if (randomizeModeEachRound)
            {
                mode = PickRandomModeFromSeed(seed);
                Debug.Log($"[MelodyGenerator] Randomized mode this round: {mode}");
            }
            
            if (enableTendencyEngine) _pendingTendencies.Clear();

            // Resolve register
            int minM = Mathf.Min(registerMinMidi, registerMaxMidi);
            int maxM = Mathf.Max(registerMinMidi, registerMaxMidi);
            if (maxM - minM < 6) maxM = minM + 6;

            // Resolve mode arrays
            ResolveModeData();

            rng = new System.Random(seed);
            resolvedContour = (contour == ContourType.Random) ? RandomContour() : contour;

            var notes = new List<MelodyNote>(Mathf.Max(1, length));

            // 1) Start note
            int start = ChooseStart(minM, maxM);
            notes.Add(new MelodyNote(start, noteDurationSec));

            bool mustResolveNext = false;
            int lastLeapAbsSteps = 0;
            int featuredLeapsUsed = 0;

            var choiceStack = new Stack<ChoiceFrame>();
            int i = 1;
            int safety = 0;

            while (i < length && safety++ < 10000)
            {
                int prev = notes[i - 1].midi;
                var candidates = BuildCandidates(prev, i, length, minM, maxM, mustResolveNext, featuredLeapsUsed);

                if (enableTendencyEngine && candidates.Count > 0)
                {
                    ApplyTendencyRules(prev, i, length, minM, maxM, candidates);
                }

                if (i == length - 1)
                {
                    ApplyApproachBias(candidates, prev, minM, maxM);
                    FilterToAllowedEndDegrees(candidates);
                }

                if (candidates.Count == 0)
                {
                    if (!Backtrack(ref notes, ref i, ref choiceStack))
                    {
                        if (RelaxOrReseed(ref notes, ref i, minM, maxM, ref mustResolveNext))
                            continue;
                        else break;
                    }
                    continue;
                }

                WeightCandidates(candidates, prev, i, length, minM, maxM, lastLeapAbsSteps, mustResolveNext, featuredLeapsUsed);
                if (candidates.Count == 0)
                {
                    if (!Backtrack(ref notes, ref i, ref choiceStack))
                    {
                        if (RelaxOrReseed(ref notes, ref i, minM, maxM, ref mustResolveNext))
                            continue;
                        else break;
                    }
                    continue;
                }

                int chosen = SampleByWeight(candidates);

                choiceStack.Push(new ChoiceFrame { index = i, prevMidi = prev, options = candidates, chosenIndex = FindIndex(candidates, chosen) });

                int diatonicSteps = Mathf.Abs(DiatonicDistance(prev, chosen));
                bool isLeap = diatonicSteps >= 3;
                if (isLeap)
                {
                    bool resolveNow = rng.NextDouble() < immediateLeapResolutionProbability;
                    mustResolveNext = !resolveNow;
                    featuredLeapsUsed += 1;
                }
                else
                {
                    mustResolveNext = false;
                }
                lastLeapAbsSteps = diatonicSteps;

                notes.Add(new MelodyNote(chosen, noteDurationSec));
                i++;

                if (enableTendencyEngine && _semitonePairs != null && _semitonePairs.Count > 0)
                {
                    int lastMidi = notes[i - 1].midi;
                    int lastDeg = DegreeOf(lastMidi);
                    foreach (var pair in _semitonePairs)
                    {
                        if (pair.upperDeg == lastDeg)
                        {
                            _pendingTendencies.Enqueue(new TendencyObligation
                            {
                                targetDegree = pair.lowerDeg,
                                remainingSteps = 2     // next step probabilistic, then mandatory
                            });
                            if (debugTendencies)
                                Debug.Log($"[Tendency] Triggered {pair.upperDeg}→{pair.lowerDeg} at pos {i-1}");
                        }
                    }
                }
            }

            // Final safety: enforce allowed end degree
            if (notes.Count > 0)
            {
                int last = notes[^1].midi;
                if (!IsAllowedEndDegree(last))
                {
                    int fallback = NearestAllowedEndInRegister(minM, maxM, last);
                    if (fallback >= 0) notes[^1] = new MelodyNote(fallback, noteDurationSec);
                }
            }

            if (enableTendencyEngine && _pendingTendencies.Count > 0)
            {
                var last = _pendingTendencies.Peek();
                if (last.remainingSteps <= 1)
                {
                    int lastMidi = notes[^1].midi;
                    int targetDeg = Mathf.Clamp(last.targetDegree, 1, 7);

                    // choose nearest register instance of target degree
                    int resolutionMidi = -1;
                    int bestD = int.MaxValue;
                    for (int m = minM; m <= maxM; m++)
                    {
                        if (!IsInScale(m)) continue;
                        if (DegreeOf(m) != targetDeg) continue;
                        int d = Mathf.Abs(m - lastMidi);
                        if (d < bestD) { bestD = d; resolutionMidi = m; }
                    }

                    if (resolutionMidi >= 0)
                    {
                        // 1) append the semitone resolution the tendency engine wants
                        notes.Add(new MelodyNote(resolutionMidi, noteDurationSec));

                        // 2) if that resolution isn’t an allowed ending, append a final cadence to the nearest allowed end
                        if (!IsAllowedEndDegree(resolutionMidi))
                        {
                            int cadence = NearestAllowedEndInRegister(minM, maxM, resolutionMidi);
                            if (cadence >= 0 && cadence != resolutionMidi)
                                notes.Add(new MelodyNote(cadence, noteDurationSec));
                        }

                        if (debugTendencies)
                            Debug.Log($"[Tendency] Appended resolution to deg {targetDeg} "
                                    + (IsAllowedEndDegree(resolutionMidi) ? " (allowed end)" : " + cadence to allowed end"));
                    }
                    _pendingTendencies.Clear();
                }
            }


            if (notes.Count > 0)
            {
                int last = notes[^1].midi;
                if (!IsAllowedEndDegree(last))
                {
                    int fallback = NearestAllowedEndInRegister(minM, maxM, last);
                    if (fallback >= 0)
                    {
                        // Replace the final note to guarantee legality
                        notes[^1] = new MelodyNote(fallback, noteDurationSec);
                    }
                }
}
            return notes;
        }

        // ---------- Candidate construction & weighting ----------
        struct Candidate { public int midi; public float w; }

        List<Candidate> BuildCandidates(int prev, int pos, int len, int minM, int maxM, bool mustResolveNext, int featuredUsed)
        {
            var list = new List<Candidate>(16);
            var allowedSteps = GetAllowedDiatonicSteps();

            foreach (int steps in allowedSteps)
            {
                int up = MoveDiatonic(prev, +steps);
                int dn = MoveDiatonic(prev, -steps);
                if (InRegister(up, minM, maxM)) list.Add(new Candidate { midi = up, w = 1f });
                if (InRegister(dn, minM, maxM)) list.Add(new Candidate { midi = dn, w = 1f });
            }

            list.RemoveAll(c => !IsInScale(c.midi));

            // enforce global allowed degree pool
            for (int idx = list.Count - 1; idx >= 0; idx--) {
                int deg = DegreeOf(list[idx].midi);
                if (!IsDegreeAllowed(deg)) list.RemoveAt(idx);
            }

            if (mustResolveNext && list.Count > 0)
            {
                int center = (minM + maxM) / 2;
                bool prevAbove = prev > center;
                list.RemoveAll(c => Mathf.Abs(DiatonicDistance(prev, c.midi)) != 1 || (prevAbove ? c.midi >= prev : c.midi <= prev));
            }

            return list;
        }

        void ApplyApproachBias(List<Candidate> list, int prev, int minM, int maxM)
        {
            // If tonic is allowed, nudge penultimate degree 2/7 when appropriate
            for (int i = 0; i < list.Count; i++)
            {
                int d = DegreeOf(list[i].midi);
                if (IsEndDegreeAllowed(1) && (d == 2 || d == 7))
                    list[i] = new Candidate { midi = list[i].midi, w = list[i].w * 1.25f };
            }
        }

        void FilterToAllowedEndDegrees(List<Candidate> list)
        {
            bool any = AnyTrue(allowedEndDegrees);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                int deg = DegreeOf(list[i].midi);
                bool ok = any ? IsEndDegreeAllowed(deg) : (deg == 1);
                if (!ok) list.RemoveAt(i);
            }
        }

        void WeightCandidates(List<Candidate> list, int prev, int pos, int len, int minM, int maxM, int lastLeapAbsSteps, bool mustResolveNext, int featuredLeapsUsed)
        {
            if (list.Count == 0) return;

            foreach (var idx in Indices(list.Count))
            {
                var c = list[idx];
                int steps = Mathf.Abs(DiatonicDistance(prev, c.midi));
                float baseW = BaseWeightForSteps(steps);

                float dirPref = DesiredDirectionBias(pos, len);
                int dir = Math.Sign(c.midi - prev);
                float contourFactor = 1f + 0.6f * dirPref * dir;

                float rangeFactor = RangeElasticity(prev, c.midi, minM, maxM);
                float resolutionFactor = (lastLeapAbsSteps >= 3 && steps == 1) ? 1.2f : 1f;

                bool isLeap = steps >= 3;
                float featuredFactor = 1f;
                if (difficulty == MelodyDifficulty.Intermediate && isLeap)
                    featuredFactor = (featuredLeapsUsed == 0) ? 1.1f : 0.7f;
                else if (difficulty == MelodyDifficulty.Advanced && isLeap)
                    featuredFactor = (featuredLeapsUsed < 2) ? 1.05f : 0.75f;

                c.w = Mathf.Max(0f, baseW * contourFactor * rangeFactor * resolutionFactor * featuredFactor);
                list[idx] = c;
            }

            float sum = 0f; foreach (var c in list) sum += c.w;
            if (sum <= 0f) { for (int i = 0; i < list.Count; i++) { var c = list[i]; c.w = 1f; list[i] = c; } }
        }

        float BaseWeightForSteps(int steps)
        {
            switch (difficulty)
            {
                case MelodyDifficulty.Beginner:
                    if (steps == 1) return 1.0f;
                    if (steps == 2) return 0.35f;
                    return 0f;
                case MelodyDifficulty.Intermediate:
                    if (steps == 1) return 1.0f;
                    if (steps == 2) return 0.5f;
                    if (steps == 3 || steps == 4) return 0.2f;
                    return 0f;
                default:
                    if (steps == 1) return 1.0f;
                    if (steps == 2) return 0.55f;
                    if (steps == 3 || steps == 4) return 0.25f;
                    if (steps == 5 || steps == 6) return 0.08f;
                    if (steps == 7) return 0.03f;
                    return 0f;
            }
        }

        float DesiredDirectionBias(int pos, int len)
        {
            float t = (len <= 1) ? 0f : (pos / (float)(len - 1));
            float bias = 0f;
            ContourType c = resolvedContour;
            if (c == ContourType.Random) c = ContourType.Any;

            switch (c)
            {
                case ContourType.Any: bias = 0f; break;
                case ContourType.Rising: bias = Mathf.Lerp(0f, +1f, t); break;
                case ContourType.Falling: bias = Mathf.Lerp(0f, -1f, t); break;
                case ContourType.Arch:
                    bias = (t <= 0.5f) ? Mathf.Lerp(0f, +1f, t / 0.5f) : Mathf.Lerp(+1f, -1f, (t - 0.5f) / 0.5f);
                    break;
                case ContourType.InvertedArch:
                    bias = (t <= 0.5f) ? Mathf.Lerp(0f, -1f, t / 0.5f) : Mathf.Lerp(-1f, +1f, (t - 0.5f) / 0.5f);
                    break;
            }

            float strength = (difficulty == MelodyDifficulty.Beginner) ? 0.25f : (difficulty == MelodyDifficulty.Intermediate ? 0.45f : 0.6f);
            return bias * strength;
        }

        float RangeElasticity(int prev, int cand, int minM, int maxM)
        {
            float center = (minM + maxM) * 0.5f;
            float half = (maxM - minM) * 0.5f;
            float normPrev = Mathf.Clamp((prev - center) / Mathf.Max(1f, half), -1f, 1f);
            float dir = Math.Sign(cand - prev);
            float towardCenter = (normPrev > 0 && dir < 0) || (normPrev < 0 && dir > 0) ? 1f : 0f;
            float awayFromEdge = towardCenter > 0 ? 1.2f : 0.9f;
            return awayFromEdge;
        }

        int SampleByWeight(List<Candidate> list)
        {
            double sum = 0; foreach (var c in list) sum += c.w;
            if (sum <= 0) return list[rng.Next(list.Count)].midi;
            double r = rng.NextDouble() * sum;
            double acc = 0;
            for (int i = 0; i < list.Count; i++) { acc += list[i].w; if (r <= acc) return list[i].midi; }
            return list[list.Count - 1].midi;
        }

        int FindIndex(List<Candidate> list, int midi)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].midi == midi) return i; return -1;
        }

        struct ChoiceFrame
        {
            public int index;
            public int prevMidi;
            public List<Candidate> options;
            public int chosenIndex;
        }

        bool Backtrack(ref List<MelodyNote> notes, ref int pos, ref Stack<ChoiceFrame> stack)
        {
            while (stack.Count > 0)
            {
                var frame = stack.Pop();

                while (notes.Count > frame.index)
                    notes.RemoveAt(notes.Count - 1);
                pos = frame.index;

                if (frame.options != null && frame.options.Count > 0)
                {
                    if (frame.chosenIndex >= 0 && frame.chosenIndex < frame.options.Count)
                        frame.options.RemoveAt(frame.chosenIndex);

                    if (frame.options.Count == 0)
                        continue;

                    int nextMidi = SampleByWeight(frame.options);
                    frame.chosenIndex = FindIndex(frame.options, nextMidi);
                    stack.Push(frame);

                    if (notes.Count == pos)
                        notes.Add(new MelodyNote(nextMidi, noteDurationSec));
                    else
                        notes[pos] = new MelodyNote(nextMidi, noteDurationSec);

                    pos = frame.index + 1;
                    return true;
                }
            }
            return false;
        }

        bool RelaxOrReseed(ref List<MelodyNote> notes, ref int pos, int minM, int maxM, ref bool mustResolveNext)
        {
            seed = Hash(seed, pos);
            rng = new System.Random(seed);
            mustResolveNext = false;
            return true;
        }

        int ChooseStart(int minM, int maxM)
        {
            var candidates = new List<int>(16);

            if (allowedStartDegrees == null || allowedStartDegrees.Length != 7 || !AnyTrue(allowedStartDegrees))
            {
                CollectDegreeInRegister(1, minM, maxM, candidates);
                CollectDegreeInRegister(3, minM, maxM, candidates);
                CollectDegreeInRegister(5, minM, maxM, candidates);
            }
            else
            {
                for (int d = 1; d <= 7; d++) if (IsStartDegreeAllowed(d) && IsDegreeAllowed(d)) CollectDegreeInRegister(d, minM, maxM, candidates);
            }

            if (candidates.Count == 0)
                for (int m = minM; m <= maxM; m++) if (IsInScale(m)) candidates.Add(m);

            int center = (minM + maxM) / 2;
            var weighted = new List<Candidate>(candidates.Count);
            foreach (var m in candidates)
            {
                float d = Mathf.Abs(m - center);
                float w = 1f / (1f + d * 0.15f);
                weighted.Add(new Candidate { midi = m, w = w });
            }
            return SampleByWeight(weighted);
        }

        // ---------- Mode helpers ----------
        void ResolveModeData()
        {
            degreeOrder = mode switch
            {
                ScaleMode.Ionian     => IonianPC,
                ScaleMode.Dorian     => DorianPC,
                ScaleMode.Phrygian   => PhrygianPC,
                ScaleMode.Lydian     => LydianPC,
                ScaleMode.Mixolydian => MixolydianPC,
                ScaleMode.Aeolian    => AeolianPC,
                _ => IonianPC
            };

            pcMask = new bool[12];
            for (int i = 0; i < 7; i++) pcMask[degreeOrder[i]] = true;

            // --- Semitone pairs for tendency engine ---
            // Build pairs (upper → lower) for adjacent degrees a,b where diff == 1 semitone.
            // Also include wrap 7→1 if (12 - degreeOrder[6]) == 1 (e.g., Ionian leading tone).
            _semitonePairs = new List<(int, int)>();
            for (int i = 0; i < 6; i++)
            {
                int diff = (degreeOrder[i + 1] - degreeOrder[i] + 12) % 12;
                if (diff == 1) _semitonePairs.Add((i + 2, i + 1));   // e.g., 4→3 in Ionian, 2→1 in Phrygian
            }
            int wrapDiff = (12 - degreeOrder[6]) % 12;
            if (wrapDiff == 1) _semitonePairs.Add((7, 1));           // e.g., 7→1 leading tone
            if (debugTendencies)
            {
                var parts = new List<string>();
                foreach (var p in _semitonePairs) parts.Add($"{p.upperDeg}->{p.lowerDeg}");
                Debug.Log($"[Tendency] Semitone pairs: {string.Join(", ", parts)}");
            }
        }

        static int Mod12(int x) { int m = x % 12; return m < 0 ? m + 12 : m; }
        static bool InRegister(int midi, int minM, int maxM) => midi >= minM && midi <= maxM;

        bool IsInScale(int midi) => pcMask[Mod12(midi)];

        int DegreeOf(int midi)
        {
            int pc = Mod12(midi);
            for (int i = 0; i < 7; i++) if (degreeOrder[i] == pc) return i + 1;
            return -1;
        }
        bool IsDegree(int midi, int degree) => DegreeOf(midi) == degree;

        bool IsDegreeAllowed(int degree) {
            if (allowedDegrees == null || allowedDegrees.Length != 7) return true; // unrestricted
            int idx = Mathf.Clamp(degree - 1, 0, 6);
            return allowedDegrees[idx];
        }

        bool IsStartDegreeAllowed(int degree)
        {
            if (allowedStartDegrees == null || allowedStartDegrees.Length != 7) return degree == 1 || degree == 3 || degree == 5;
            int idx = Mathf.Clamp(degree - 1, 0, 6);
            return allowedStartDegrees[idx];
        }
        bool IsEndDegreeAllowed(int degree)
        {
            if (allowedEndDegrees == null || allowedEndDegrees.Length != 7) return degree == 1;
            int idx = Mathf.Clamp(degree - 1, 0, 6);
            return allowedEndDegrees[idx];
        }
        bool IsAllowedEndDegree(int midi) => IsEndDegreeAllowed(DegreeOf(midi));

        static bool AnyTrue(bool[] arr)
        {
            if (arr == null) return false;
            for (int i = 0; i < arr.Length; i++) if (arr[i]) return true;
            return false;
        }

        int NearestAllowedEndInRegister(int minM, int maxM, int center)
        {
            int best = -1;
            int bestDist = int.MaxValue;

            for (int m = minM; m <= maxM; m++)
            {
                if (!IsInScale(m)) continue;

                int deg = DegreeOf(m);
                if (!IsEndDegreeAllowed(deg)) continue; // <-- enforce your allowed endings (e.g. 1,3,5)

                int d = Mathf.Abs(m - center);
                if (d < bestDist) { bestDist = d; best = m; }
            }

            // Fallback: if user somehow disabled all endings, bias to tonic in register
            if (best < 0)
            {
                var tonics = new List<int>();
                CollectDegreeInRegister(1, minM, maxM, tonics);
                if (tonics.Count > 0)
                {
                    // pick the tonic closest to 'center'
                    int pick = tonics[0];
                    int dist = Mathf.Abs(pick - center);
                    for (int i = 1; i < tonics.Count; i++)
                    {
                        int d = Mathf.Abs(tonics[i] - center);
                        if (d < dist) { dist = d; pick = tonics[i]; }
                    }
                    best = pick;
                }
            }
            return best;
        }


        void CollectDegreeInRegister(int degree, int minM, int maxM, List<int> outList)
        {
            int pc = degreeOrder[Mathf.Clamp(degree - 1, 0, 6)];
            for (int m = minM; m <= maxM; m++) if (Mod12(m) == pc) outList.Add(m);
        }

        // Diatonic stepping in current mode
        int MoveDiatonic(int midi, int steps)
        {
            int dir = Math.Sign(steps);
            int remain = Mathf.Abs(steps);
            int m = midi;
            while (remain-- > 0) m = (dir > 0) ? NextScaleNoteUp(m) : NextScaleNoteDown(m);
            return m;
        }
        int NextScaleNoteUp(int midi)
        {
            int m = midi + 1;
            while (!IsInScale(m)) m++;
            return m;
        }
        int NextScaleNoteDown(int midi)
        {
            int m = midi - 1;
            while (!IsInScale(m)) m--;
            return m;
        }

        int DiatonicDistance(int a, int b)
        {
            if (a == b) return 0;
            int dir = Math.Sign(b - a);
            int steps = 0; int m = a;
            while (m != b)
            {
                m = (dir > 0) ? NextScaleNoteUp(m) : NextScaleNoteDown(m);
                steps++;
                if (steps > 64) break;
            }
            return steps * dir;
        }

        int[] GetAllowedDiatonicSteps()
        {
            // StepwiseOnly: only allow stepwise motion (±1 diatonic step)
            if (movementPolicy == Sonoria.Dictation.MovementPolicy.StepwiseOnly)
            {
                return new[] { 1 };
            }
            
            // UpToMaxLeap: allow leaps up to maxLeapSteps (1-8 diatonic steps)
            if (movementPolicy == Sonoria.Dictation.MovementPolicy.UpToMaxLeap && maxLeapSteps >= 1 && maxLeapSteps <= 8)
            {
                // Generate array of allowed steps from 1 to maxLeapSteps
                int[] steps = new int[maxLeapSteps];
                for (int i = 0; i < maxLeapSteps; i++)
                {
                    steps[i] = i + 1;
                }
                return steps;
            }
            
            // Default fallback (should not normally reach here)
            return new[] { 1 };
        }

        // Public setters for movement policy (used by DifficultyProfileApplier)
        public void SetMovementPolicy(Sonoria.Dictation.MovementPolicy policy)
        {
            movementPolicy = policy;
        }

        public void SetMaxLeapSteps(int steps)
        {
            maxLeapSteps = Mathf.Clamp(steps, 1, 8);
        }
        
        // Adjusts candidate list based on current tendency obligations.
        // - If mandatory: only allow the resolution target degree (octave-accommodated).
        // - If probabilistic: with probability p, force the resolution;
        //   else allow only small detours (≤ M3) and decrement window.
        void ApplyTendencyRules(int prevMidi, int pos, int len, int minM, int maxM, List<Candidate> candidates)
        {
            if (_pendingTendencies.Count == 0) return;

            // Oldest obligation wins
            var obligation = _pendingTendencies.Peek();
            int targetDeg = Mathf.Clamp(obligation.targetDegree, 1, 7);

            // Helper: keep only candidates whose degree matches targetDeg (with octave fit)
            void FilterToTargetDegree()
            {
                for (int idx = candidates.Count - 1; idx >= 0; idx--)
                {
                    int d = DegreeOf(candidates[idx].midi);
                    if (d != targetDeg)
                    {
                        // Try octave shift if pitch-class matches but outside register constraints
                        // (We prefer to keep candidates list clean; backtracking will handle dead-ends.)
                        candidates.RemoveAt(idx);
                    }
                }
            }

            // Mandatory step: force the resolution
            if (obligation.remainingSteps <= 1)
            {
                FilterToTargetDegree();
                if (debugTendencies)
                    Debug.Log($"[Tendency] Mandatory resolution → degree {targetDeg} (pos {pos}/{len})");
                _pendingTendencies.Dequeue();  // will resolve this step
                return;
            }

            // First step (probabilistic)
            bool resolveNow = UnityEngine.Random.value < tendencyResolveProbability;
            if (resolveNow)
            {
                FilterToTargetDegree();
                if (debugTendencies)
                    Debug.Log($"[Tendency] Probabilistic resolution → degree {targetDeg} (pos {pos}/{len})");
                _pendingTendencies.Dequeue();  // resolve now
                return;
            }

            // Not resolving now → allow a small detour and tighten options
            if (allowSmallDetours)
            {
                // remove leaps beyond M3 (≥ 4 diatonic steps) from prev
                for (int idx = candidates.Count - 1; idx >= 0; idx--)
                {
                    int steps = Mathf.Abs(DiatonicDistance(prevMidi, candidates[idx].midi));
                    if (steps >= 4) candidates.RemoveAt(idx);
                }
            }

            // Decrement obligation window (still pending)
            obligation.remainingSteps = Mathf.Max(1, obligation.remainingSteps - 1);
            _pendingTendencies.Dequeue();
            _pendingTendencies.Enqueue(obligation);
        }

        ContourType RandomContour()
        {
            var values = (ContourType[])Enum.GetValues(typeof(ContourType));
            var list = new List<ContourType>();
            foreach (var v in values) if (v != ContourType.Random) list.Add(v);
            return list[rng.Next(list.Count)];
        }

        ScaleMode PickRandomModeFromSeed(int roundSeed)
        {
            // Only the six diatonic modes we support (no Locrian)
            var options = new[]
            {
                ScaleMode.Ionian, ScaleMode.Dorian, ScaleMode.Phrygian,
                ScaleMode.Lydian, ScaleMode.Mixolydian, ScaleMode.Aeolian
            };

            // Use your existing Hash() to keep runs deterministic for a given Seed
            int idx = Mathf.Abs(Hash(roundSeed, 1337)) % options.Length;
            return options[idx];
        }

        static IEnumerable<int> Indices(int n) { for (int i = 0; i < n; i++) yield return i; }

        static int Hash(int a, int b)
        {
            unchecked
            {
                uint x = 0x9E3779B9u;
                x ^= (uint)a + ((x << 6) + (x >> 2));
                x ^= (uint)b + ((x << 6) + (x >> 2));
                return (int)x;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Test Generate (Log)")]
        void TestGenerateLog()
        {
            ResolveModeData();
            var mel = Generate();
            Debug.Log($"[MelodyGenerator] seed={seed} mode={mode} diff={difficulty} contour={resolvedContour} len={mel.Count} : " + string.Join(",", mel));
        }
#endif
    }
}
