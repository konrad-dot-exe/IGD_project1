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

    // NEW: modal selection (Locrian intentionally omitted)
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
        [SerializeField] ScaleMode mode = ScaleMode.Ionian;     // <- choose in Inspector

        [Header("Pitch Space (register)")]
        [SerializeField] int registerMinMidi = 55; // G3
        [SerializeField] int registerMaxMidi = 72; // C5

        [Header("Behavior")]
        [SerializeField] MelodyDifficulty difficulty = MelodyDifficulty.Beginner;
        [SerializeField] ContourType contour = ContourType.Random;
        [Tooltip("When OFF, restrict to stepwise motion regardless of difficulty.")]
        [SerializeField] bool allowLeaps = true;
        [Tooltip("Probability that a leap resolves immediately by stepback; if not, next step is forced to resolve.")]
        [Range(0f, 1f)] public float immediateLeapResolutionProbability = 0.80f;

        [Header("Anchors (Degrees 1..7)")]
        [Tooltip("Allowed start degrees within the key (1..7). Default: 1,3,5.")]
        [SerializeField] bool[] allowedStartDegrees = new bool[7] { true, false, true, false, true, false, false };
        [Tooltip("Allowed END degrees within the key (1..7). Default: 1 only.")]
        [SerializeField] bool[] allowedEndDegrees   = new bool[7] { true, false, false, false, false, false, false };

        // Public accessors
        public int Seed { get => seed; set => seed = value; }
        public int Length { get => length; set => length = Mathf.Clamp(value, 1, 64); }
        public bool[] AllowedStartDegrees => allowedStartDegrees;
        public bool[] AllowedEndDegrees   => allowedEndDegrees;

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

            return notes;
        }

        // ---------- Candidate construction & weighting ----------
        struct Candidate { public int midi; public float w; }

        List<Candidate> BuildCandidates(int prev, int pos, int len, int minM, int maxM, bool mustResolveNext, int featuredUsed)
        {
            var list = new List<Candidate>(16);
            var allowedSteps = GetAllowedDiatonicSteps();
            if (!allowLeaps) allowedSteps = new int[] { 1 };

            foreach (int steps in allowedSteps)
            {
                int up = MoveDiatonic(prev, +steps);
                int dn = MoveDiatonic(prev, -steps);
                if (InRegister(up, minM, maxM)) list.Add(new Candidate { midi = up, w = 1f });
                if (InRegister(dn, minM, maxM)) list.Add(new Candidate { midi = dn, w = 1f });
            }

            list.RemoveAll(c => !IsInScale(c.midi));

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
                for (int d = 1; d <= 7; d++) if (IsStartDegreeAllowed(d)) CollectDegreeInRegister(d, minM, maxM, candidates);
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
            int best = -1; int bestD = int.MaxValue;
            for (int m = minM; m <= maxM; m++)
            {
                if (!IsInScale(m)) continue;
                int d = Mathf.Abs(m - center);
                if (d < bestD) { bestD = d; best = m; }
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
            if (!allowLeaps) return new[] { 1 };
            switch (difficulty)
            {
                case MelodyDifficulty.Beginner:      return new[] { 1, 2 };
                case MelodyDifficulty.Intermediate:  return new[] { 1, 2, 3, 4 };
                default:                              return new[] { 1, 2, 3, 4, 5, 6, 7 };
            }
        }

        ContourType RandomContour()
        {
            var values = (ContourType[])Enum.GetValues(typeof(ContourType));
            var list = new List<ContourType>();
            foreach (var v in values) if (v != ContourType.Random) list.Add(v);
            return list[rng.Next(list.Count)];
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
