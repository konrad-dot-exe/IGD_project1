using UnityEngine;
using Sonoria.MusicTheory;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Quick validation tests for TheoryScale.
    /// Call ValidateAllModes() to log pitch classes for all modes.
    /// </summary>
    public static class TheoryScaleTests
    {
        /// <summary>
        /// Validates pitch classes for all modes against known correct sequences.
        /// Logs results using Debug.Log.
        /// </summary>
        public static void ValidateAllModes()
        {
            Debug.Log("=== TheoryScale Validation Tests ===");
            
            // Expected pitch classes for C-root modes
            var expected = new System.Collections.Generic.Dictionary<ScaleMode, int[]>
            {
                { ScaleMode.Ionian, new[] { 0, 2, 4, 5, 7, 9, 11 } },      // C major
                { ScaleMode.Dorian, new[] { 0, 2, 3, 5, 7, 9, 10 } },      // C Dorian
                { ScaleMode.Phrygian, new[] { 0, 1, 3, 5, 7, 8, 10 } },    // C Phrygian
                { ScaleMode.Lydian, new[] { 0, 2, 4, 6, 7, 9, 11 } },      // C Lydian
                { ScaleMode.Mixolydian, new[] { 0, 2, 4, 5, 7, 9, 10 } },  // C Mixolydian
                { ScaleMode.Aeolian, new[] { 0, 2, 3, 5, 7, 8, 10 } },     // C natural minor
                { ScaleMode.Locrian, new[] { 0, 1, 3, 5, 6, 8, 10 } }      // C Locrian
            };
            
            bool allPassed = true;
            
            foreach (var kvp in expected)
            {
                var mode = kvp.Key;
                var expectedPcs = kvp.Value;
                var key = new TheoryKey(mode);
                var actualPcs = TheoryScale.GetDiatonicPitchClasses(key);
                
                bool matches = true;
                if (actualPcs.Length != expectedPcs.Length)
                {
                    matches = false;
                }
                else
                {
                    for (int i = 0; i < actualPcs.Length; i++)
                    {
                        if (actualPcs[i] != expectedPcs[i])
                        {
                            matches = false;
                            break;
                        }
                    }
                }
                
                string result = matches ? "PASS" : "FAIL";
                string pcsStr = string.Join(", ", actualPcs);
                Debug.Log($"[{result}] {mode}: [{pcsStr}]");
                
                if (!matches)
                {
                    string expectedStr = string.Join(", ", expectedPcs);
                    Debug.LogError($"  Expected: [{expectedStr}]");
                    allPassed = false;
                }
            }
            
            Debug.Log($"=== Validation {(allPassed ? "PASSED" : "FAILED")} ===");
        }
        
        /// <summary>
        /// Tests individual degree pitch class lookups.
        /// </summary>
        public static void TestDegrees()
        {
            Debug.Log("=== Testing Individual Degrees ===");
            var key = new TheoryKey(ScaleMode.Ionian);
            
            // C Ionian degrees should be: 1=C(0), 2=D(2), 3=E(4), 4=F(5), 5=G(7), 6=A(9), 7=B(11)
            int[] expectedDegrees = { 0, 2, 4, 5, 7, 9, 11 };
            
            for (int deg = 1; deg <= 7; deg++)
            {
                int pc = TheoryScale.GetDegreePitchClass(key, deg);
                int expected = expectedDegrees[deg - 1];
                bool matches = (pc == expected);
                string result = matches ? "PASS" : "FAIL";
                Debug.Log($"[{result}] Degree {deg}: PC={pc} (expected {expected})");
            }
        }
    }
}

