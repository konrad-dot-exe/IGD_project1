// MelodyGenerator.cs — Rule–Probabilistic Hybrid v1
// Generates short, musical, ear‑training‑friendly melodies for dictation.
// Deterministic via seed, soft contour bias, difficulty‑based interval behavior, backtracking with soft‑rule relaxation.
// Namespace aligns with existing project code.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarFPS
{
    public enum MelodyDifficulty { Beginner, Intermediate, Advanced }
    public enum ContourType { Any, Rising, Falling, Arch, InvertedArch, Random }

    [Serializable]
    public struct MelodyNote
    {
        public int midi;            // absolute MIDI note number
        public float durationSec;   // uniform in v1 (host can override)
        public MelodyNote(int midi, float dur) { this.midi = midi; this.durationSec = dur; }
        public override string ToString() => $"{midi}";
    }

    [AddComponentMenu("EarFPS/Melody Generator (Rule-Prob Hybrid)")]
    public class MelodyGenerator : MonoBehaviour
    {
        [Header("Seed & Length")]
        [SerializeField] int seed = 12345;
        [SerializeField, Range(1, 64)] int length = 6;
        [SerializeField] float noteDurationSec = 0.8f; // v1: uniform

        [Header("Pitch Space (v1 fixed to C major)")]
        [SerializeField] int registerMinMidi = 55; // G3
        [SerializeField] int registerMaxMidi = 72; // C5

        [Header("Behavior")]
        [SerializeField] MelodyDifficulty difficulty = MelodyDifficulty.Beginner;
        [SerializeField] ContourType contour = ContourType.Random; // Random resolves at runtime
        [Tooltip("When OFF, restrict to stepwise motion regardless of difficulty.")]
        [SerializeField] bool allowLeaps = true;
        [Tooltip("Probability that a leap resolves immediately by stepback; if not, next step is forced to resolve.")]
        [Range(0f, 1f)] public float immediateLeapResolutionProbability = 0.80f;

        [Header("Anchors")] 
        [Tooltip("Start on degree 1, 3, or 5 (within register)")]
        [SerializeField] bool startOn135 = true;
        [Tooltip("End on degree 1 (tonic)")]
        [SerializeField] bool endOn1 = true;

        public int Seed   { get => seed;   set => seed   = value; }
        public int Length { get => length; set => length = Mathf.Clamp(value, 1, 64); }

        // --- Internal state ---
        System.Random rng;
        ContourType resolvedContour;

        // Scale (C major) pitch-class mask; C=0..B=11
        static readonly bool[] CMajorMask = { true, false, true, false, true, true, false, true, false, true, false, true };
        static readonly int[] DegreeOrder = { 0,2,4,5,7,9,11 }; // 1..7 → pitch classes for C major

        // ---- Public API ----
        /// <summary>
        /// Main entry point. Builds a diatonic melody one note at a time while honouring
        /// contour, difficulty, and cadence requirements. Falls back to backtracking when
        /// a dead-end is reached so that the caller always gets a playable line.
        /// </summary>
        public List<MelodyNote> Generate()
        {
            // Clamp/validate
            int minM = Mathf.Min(registerMinMidi, registerMaxMidi);
            int maxM = Mathf.Max(registerMinMidi, registerMaxMidi);
            if (maxM - minM < 6) maxM = minM + 6; // ensure some room (≥ one octave diatonic space ~7 steps)

            rng = new System.Random(seed);
            resolvedContour = (contour == ContourType.Random) ? RandomContour() : contour;

            var notes = new List<MelodyNote>(Mathf.Max(1, length));

            // 1) Choose start note
            int start = ChooseStart(minM, maxM);
            notes.Add(new MelodyNote(start, noteDurationSec));

            // Trackers for resolution/featured leaps
            bool mustResolveNext = false;
            int lastLeapAbsSteps = 0; // diatonic steps magnitude of last move
            int featuredLeapsUsed = 0;

            // Cached for backtracking
            var choiceStack = new Stack<ChoiceFrame>();

            int i = 1; // position index
            int safety = 0; // deadlock guard
            while (i < length && safety++ < 10000)
            {
                int prev = notes[i - 1].midi;
                // Assemble a short list of legal next notes before weighting them.
                var candidates = BuildCandidates(prev, i, length, minM, maxM, mustResolveNext, featuredLeapsUsed);

                // If penultimate and endOn1, bias approach tones 2 or 7 → 1
                if (endOn1 && i == length - 1)
                    ApplyCadentialBias(candidates, prev, minM, maxM);

                // Filter final note to tonic if required
                if (endOn1 && i == length - 1)
                    FilterToFinalTonic(candidates, minM, maxM);

                // No candidates? Backtrack / relax / reseed as per policy
                if (candidates.Count == 0)
                {
                    if (!Backtrack(ref notes, ref i, ref choiceStack))
                    {
                        // Relax soft rules progressively or reseed
                        if (RelaxOrReseed(ref notes, ref i, minM, maxM, ref mustResolveNext))
                            continue;
                        else
                            break; // give up (should be extremely rare)
                    }
                    // After backtrack, continue loop (recompute candidates at this i)
                    continue;
                }

                // Weight and select
                WeightCandidates(candidates, prev, i, length, minM, maxM, lastLeapAbsSteps, mustResolveNext, featuredLeapsUsed);
                if (candidates.Count == 0)
                {
                    // No viable after weighting (all weights ~0) → backtrack
                    if (!Backtrack(ref notes, ref i, ref choiceStack))
                    {
                        if (RelaxOrReseed(ref notes, ref i, minM, maxM, ref mustResolveNext))
                            continue;
                        else break;
                    }
                    continue;
                }

                int chosen = SampleByWeight(candidates);

                // Push frame for possible backtrack (track rejected candidates)
                choiceStack.Push(new ChoiceFrame { index = i, prevMidi = prev, options = candidates, chosenIndex = FindIndex(candidates, chosen) });

                // Update resolution tracking
                int diatonicSteps = Mathf.Abs(DiatonicDistance(prev, chosen));
                bool isLeap = diatonicSteps >= 3;
                if (isLeap)
                {
                    // immediate resolution attempt with configurable probability
                    bool resolveNow = rng.NextDouble() < immediateLeapResolutionProbability;
                    mustResolveNext = !resolveNow; // if not resolved now, force next
                    featuredLeapsUsed += 1;
                }
                else
                {
                    // If we had a pending resolution, a step satisfies it (enforced in candidate builder)
                    mustResolveNext = false;
                }
                lastLeapAbsSteps = diatonicSteps;

                notes.Add(new MelodyNote(chosen, noteDurationSec));
                i++;
            }

            // Final validation (anchors, bounds, etc.) — minimal because enforced along the way
            if (endOn1 && notes.Count > 0)
            {
                int last = notes[notes.Count - 1].midi;
                if (!IsDegree(last, 1))
                {
                    // emergency fix: snap to nearest tonic in register
                    int tonic = NearestDegreeInRegister(1, minM, maxM, center: last);
                    if (tonic >= 0) notes[notes.Count - 1] = new MelodyNote(tonic, noteDurationSec);
                }
            }

            return notes;
        }

        // ---- Candidate construction & weighting ----
        // Lightweight structure used for weighting potential notes during generation.
        struct Candidate { public int midi; public float w; }

        List<Candidate> BuildCandidates(int prev, int pos, int len, int minM, int maxM, bool mustResolveNext, int featuredUsed)
        {
            var list = new List<Candidate>(16);

            // Allowed diatonic step sizes per difficulty (1=step, 2=third, ... 7=octave)
            var allowedSteps = GetAllowedDiatonicSteps();
            if (!allowLeaps) allowedSteps = new int[] { 1 }; // stepwise only

            foreach (int steps in allowedSteps)
            {
                // Up and down symmetric; will be shaped by contour & range
                int up = MoveDiatonic(prev, +steps);
                int dn = MoveDiatonic(prev, -steps);

                if (InRegister(up, minM, maxM)) list.Add(new Candidate { midi = up, w = 1f });
                if (InRegister(dn, minM, maxM)) list.Add(new Candidate { midi = dn, w = 1f });
            }

            // Enforce scale (defensive; MoveDiatonic already ensures scale notes)
            list.RemoveAll(c => !IsInCMajor(c.midi));

            // Enforce must-resolve-next: require step (1) opposite of last leap direction toward center
            if (mustResolveNext && list.Count > 0)
            {
                int center = (minM + maxM) / 2;
                bool prevAbove = prev > center;
                list.RemoveAll(c => Mathf.Abs(DiatonicDistance(prev, c.midi)) != 1 || (prevAbove ? c.midi >= prev : c.midi <= prev));
            }

            return list;
        }

        // Very gentle weighting to favour sensible approach tones before the cadence.
        void ApplyCadentialBias(List<Candidate> list, int prev, int minM, int maxM)
        {
            // Gentle boost if penultimate is degree 2 or 7 moving to 1 next
            for (int i = 0; i < list.Count; i++)
            {
                int d = DegreeOf(list[i].midi);
                if (d == 2 || d == 7) list[i] = new Candidate { midi = list[i].midi, w = list[i].w * 1.25f };
            }
        }

        // Hard filter that ensures the final melody note actually lands on the tonic when required.
        void FilterToFinalTonic(List<Candidate> list, int minM, int maxM)
        {
            for (int i = list.Count - 1; i >= 0; i--)
                if (!IsDegree(list[i].midi, 1)) list.RemoveAt(i);
        }

        void WeightCandidates(List<Candidate> list, int prev, int pos, int len, int minM, int maxM, int lastLeapAbsSteps, bool mustResolveNext, int featuredLeapsUsed)
        {
            if (list.Count == 0) return;

            // Difficulty base weights by diatonic distance
            foreach (var idx in Indices(list.Count))
            {
                var c = list[idx];
                int steps = Mathf.Abs(DiatonicDistance(prev, c.midi));
                float baseW = BaseWeightForSteps(steps);

                // Contour factor
                float dirPref = DesiredDirectionBias(pos, len); // -1..+1 (down..up)
                int dir = Math.Sign(c.midi - prev);
                float contourFactor = 1f + 0.6f * dirPref * dir; // modest multiplicative bias

                // Range elasticity: nudge toward center
                float rangeFactor = RangeElasticity(prev, c.midi, minM, maxM);

                // Leap resolution factor (if last move was leap and not yet resolved, we already enforced mustResolveNext in candidates; here just small extra weight)
                float resolutionFactor = (lastLeapAbsSteps >= 3 && steps == 1) ? 1.2f : 1f;

                // Featured leap soft-cap (Intermediate/Advanced)
                bool isLeap = steps >= 3;
                float featuredFactor = 1f;
                if (difficulty == MelodyDifficulty.Intermediate && isLeap)
                {
                    featuredFactor = (featuredLeapsUsed == 0) ? 1.1f : 0.7f;
                }
                else if (difficulty == MelodyDifficulty.Advanced && isLeap)
                {
                    featuredFactor = (featuredLeapsUsed < 2) ? 1.05f : 0.75f;
                }

                c.w = Mathf.Max(0f, baseW * contourFactor * rangeFactor * resolutionFactor * featuredFactor);
                list[idx] = c;
            }

            // Normalize small weights
            float sum = 0f; foreach (var c in list) sum += c.w;
            if (sum <= 0f)
            {
                // As a last resort before backtracking, flatten to equal weights
                for (int i = 0; i < list.Count; i++) { var c = list[i]; c.w = 1f; list[i] = c; }
            }
        }

        // Maps diatonic distance to an initial probability weight per difficulty tier.
        float BaseWeightForSteps(int steps)
        {
            // steps: 1(step),2(third),3(fourth),4(fifth),5(sixth),6(seventh),7(octave)
            switch (difficulty)
            {
                case MelodyDifficulty.Beginner:
                    if (steps == 1) return 1.0f;
                    if (steps == 2) return 0.35f;
                    return 0f; // disallow ≥4th
                case MelodyDifficulty.Intermediate:
                    if (steps == 1) return 1.0f;
                    if (steps == 2) return 0.5f;
                    if (steps == 3 || steps == 4) return 0.2f; // 4th/5th
                    return 0f; // disallow ≥6th
                default: // Advanced
                    if (steps == 1) return 1.0f;
                    if (steps == 2) return 0.55f;
                    if (steps == 3 || steps == 4) return 0.25f;
                    if (steps == 5 || steps == 6) return 0.08f;
                    if (steps == 7) return 0.03f; // octave rare
                    return 0f;
            }
        }

        // Computes the contour bias (-1..1) used to nudge the melody in the requested direction.
        float DesiredDirectionBias(int pos, int len)
        {
            // Returns -1..+1 (down..up). 0 = neutral.
            float t = (len <= 1) ? 0f : (pos / (float)(len - 1)); // 0..1
            float bias = 0f;
            ContourType c = resolvedContour;
            if (c == ContourType.Random) c = ContourType.Any; // should've been resolved already

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

            // Scale bias strength by difficulty (light→strong)
            float strength = (difficulty == MelodyDifficulty.Beginner) ? 0.25f : (difficulty == MelodyDifficulty.Intermediate ? 0.45f : 0.6f);
            return bias * strength;
        }

        // Soft range management. Keeps the melody inside the playable register without feeling forced.
        float RangeElasticity(int prev, int cand, int minM, int maxM)
        {
            // Pull away from edges, push toward center
            float center = (minM + maxM) * 0.5f;
            float half = (maxM - minM) * 0.5f;
            float normPrev = Mathf.Clamp((prev - center) / Mathf.Max(1f, half), -1f, 1f);
            float dir = Math.Sign(cand - prev);
            // If we're high (normPrev>0) and moving further up (dir>0) → downweight; if moving toward center → upweight
            float towardCenter = (normPrev > 0 && dir < 0) || (normPrev < 0 && dir > 0) ? 1f : 0f;
            float awayFromEdge = towardCenter > 0 ? 1.2f : 0.9f;
            return awayFromEdge;
        }

        // Roulette-wheel selection helper used for both generation and backtracking paths.
        int SampleByWeight(List<Candidate> list)
        {
            double sum = 0; foreach (var c in list) sum += c.w;
            if (sum <= 0) return list[rng.Next(list.Count)].midi;
            double r = rng.NextDouble() * sum;
            double acc = 0;
            for (int i = 0; i < list.Count; i++) { acc += list[i].w; if (r <= acc) return list[i].midi; }
            return list[list.Count - 1].midi;
        }

        // Utility to map a MIDI value back to its candidate index after sampling.
        int FindIndex(List<Candidate> list, int midi)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].midi == midi) return i; return -1;
        }

        // ---- Backtracking & relaxation ----
        struct ChoiceFrame
        {
            public int index;               // melody position this choice belongs to
            public int prevMidi;            // previous note at that position
            public List<Candidate> options; // full candidate list (weighted)
            public int chosenIndex;         // index we took
        }

        /// <summary>
        /// Pops previous decision points until an alternative path exists. This avoids
        /// regenerating the whole melody when a late note violates a constraint.
        /// </summary>
        bool Backtrack(ref List<MelodyNote> notes, ref int pos, ref Stack<ChoiceFrame> stack)
        {
            // Trim melody to the position where the last choice was made,
            // then try an alternative candidate. Avoid RemoveAt(pos) OOR when pos==notes.Count.
            while (stack.Count > 0)
            {
                var frame = stack.Pop();

                // Trim notes back to the decision index (frame.index)
                while (notes.Count > frame.index)
                    notes.RemoveAt(notes.Count - 1);
                pos = frame.index;

                // Remove previously chosen option and try another
                if (frame.options != null && frame.options.Count > 0)
                {
                    if (frame.chosenIndex >= 0 && frame.chosenIndex < frame.options.Count)
                        frame.options.RemoveAt(frame.chosenIndex);

                    if (frame.options.Count == 0)
                        continue; // no alternatives at this frame; pop an earlier one

                    int nextMidi = SampleByWeight(frame.options);
                    frame.chosenIndex = FindIndex(frame.options, nextMidi);
                    stack.Push(frame); // keep this frame with the updated choice

                    // Place the chosen note at frame.index
                    if (notes.Count == pos)
                        notes.Add(new MelodyNote(nextMidi, noteDurationSec));
                    else
                        notes[pos] = new MelodyNote(nextMidi, noteDurationSec);

                    pos = frame.index + 1;
                    return true;
                }
            }
            return false; // nothing left to backtrack
        }

        /// <summary>
        /// When all candidates fail (even with backtracking) this method softens a few
        /// heuristics or reseeds the RNG so generation can continue instead of failing.
        /// </summary>
        bool RelaxOrReseed(ref List<MelodyNote> notes, ref int pos, int minM, int maxM, ref bool mustResolveNext)
        {
            // Minimal soft relaxation strategy: weaken contour & resolution biases by moving to neutral for the current step,
            // otherwise reseed the RNG for this branch.
            // For v1, we simply reseed a little and retry from current pos.
            seed = Hash(seed, pos); // deterministic sub-seed
            rng = new System.Random(seed);
            mustResolveNext = false; // drop the pending must-resolve to unstick
            // also, nudge contour toward Any for the remainder (soft)
            resolvedContour = (resolvedContour == ContourType.Any) ? ContourType.Any : resolvedContour;
            return true; // instruct caller to recompute
        }

        // ---- Start, degree, and diatonic helpers ----
        // Selects a starting note based on configuration and central register weighting.
        int ChooseStart(int minM, int maxM)
        {
            var candidates = new List<int>(16);
            if (startOn135)
            {
                CollectDegreeInRegister(1, minM, maxM, candidates);
                CollectDegreeInRegister(3, minM, maxM, candidates);
                CollectDegreeInRegister(5, minM, maxM, candidates);
            }
            else
            {
                // Any scale note in register
                for (int m = minM; m <= maxM; m++) if (IsInCMajor(m)) candidates.Add(m);
            }

            // Prefer central register: weight by inverse distance to center
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

        static bool InRegister(int midi, int minM, int maxM) => midi >= minM && midi <= maxM;
        static bool IsInCMajor(int midi) => CMajorMask[Mod12(midi)];
        static int Mod12(int x) { int m = x % 12; return m < 0 ? m + 12 : m; }

        // Returns the diatonic degree (1..7) within C major or -1 if non-diatonic.
        static int DegreeOf(int midi)
        {
            // 1..7 degrees for C major (octave ignored)
            int pc = Mod12(midi);
            for (int i = 0; i < 7; i++) if (DegreeOrder[i] == pc) return i + 1;
            return -1; // non-diatonic (shouldn't happen)
        }
        static bool IsDegree(int midi, int degree) => DegreeOf(midi) == degree;

        // Used during emergency correction to snap a note to the closest tonic candidate.
        static int NearestDegreeInRegister(int degree, int minM, int maxM, int center)
        {
            int pc = DegreeOrder[Mathf.Clamp(degree - 1, 0, 6)];
            int best = -1; int bestD = int.MaxValue;
            for (int m = minM; m <= maxM; m++) if (Mod12(m) == pc)
            {
                int d = Mathf.Abs(m - center);
                if (d < bestD) { bestD = d; best = m; }
            }
            return best;
        }

        // Helper that gathers every scale note for a requested diatonic degree within the active register.
        void CollectDegreeInRegister(int degree, int minM, int maxM, List<int> outList)
        {
            int pc = DegreeOrder[Mathf.Clamp(degree - 1, 0, 6)];
            for (int m = minM; m <= maxM; m++) if (Mod12(m) == pc) outList.Add(m);
        }

        // Walks the scale by the requested number of diatonic steps without ever leaving C major.
        int MoveDiatonic(int midi, int steps)
        {
            // Move by N diatonic steps within C major, preserving scale membership. Positive = up.
            int dir = Math.Sign(steps);
            int remain = Mathf.Abs(steps);
            int m = midi;
            while (remain-- > 0)
            {
                m = (dir > 0) ? NextScaleNoteUp(m) : NextScaleNoteDown(m);
            }
            return m;
        }

        // Step upward to the next available note in the scale.
        static int NextScaleNoteUp(int midi)
        {
            int m = midi + 1;
            while (!IsInCMajor(m)) m++;
            return m;
        }
        // Step downward to the previous available note in the scale.
        static int NextScaleNoteDown(int midi)
        {
            int m = midi - 1;
            while (!IsInCMajor(m)) m--;
            return m;
        }

        // Counts how many scale steps separate two notes (signed: positive=upward).
        static int DiatonicDistance(int a, int b)
        {
            // Count diatonic steps between a and b in C major (ignoring semitone sizes)
            if (a == b) return 0;
            int dir = Math.Sign(b - a);
            int steps = 0; int m = a;
            while (m != b)
            {
                m = (dir > 0) ? NextScaleNoteUp(m) : NextScaleNoteDown(m);
                steps++;
                if (steps > 64) break; // safety
            }
            return steps * dir;
        }

        // Difficulty-dependent rule set that restricts how far the melody can leap.
        int[] GetAllowedDiatonicSteps()
        {
            if (!allowLeaps) return new[] { 1 };
            switch (difficulty)
            {
                case MelodyDifficulty.Beginner: return new[] { 1, 2 }; // step, third
                case MelodyDifficulty.Intermediate: return new[] { 1, 2, 3, 4 }; // up to 5th
                default: return new[] { 1, 2, 3, 4, 5, 6, 7 }; // up to octave
            }
        }

        // Picks a deterministic contour when the user selects "Random".
        ContourType RandomContour()
        {
            var values = (ContourType[])Enum.GetValues(typeof(ContourType));
            // exclude Random itself when picking
            var list = new List<ContourType>();
            foreach (var v in values) if (v != ContourType.Random) list.Add(v);
            return list[rng.Next(list.Count)];
        }

        static IEnumerable<int> Indices(int n) { for (int i = 0; i < n; i++) yield return i; }

        // Simple integer hash used to derive deterministic sub-seeds during relaxation.
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
            var mel = Generate();
            Debug.Log($"[MelodyGenerator] seed={seed} diff={difficulty} contour={resolvedContour} len={mel.Count} : " + string.Join(",", mel));
        }
#endif
    }
}
