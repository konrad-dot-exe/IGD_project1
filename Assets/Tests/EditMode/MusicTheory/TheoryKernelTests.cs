using NUnit.Framework;
using Sonoria.MusicTheory;

namespace Sonoria.Tests
{
    /// <summary>
    /// Regression and sanity tests for the tonic-aware theory kernel.
    /// </summary>
    public class TheoryKernelTests
    {
        private static ChordRecipe Triad(int degree, ChordQuality quality, int rootOffset = 0)
        {
            return new ChordRecipe(
                degree,
                quality,
                ChordExtension.None,
                rootOffset,
                SeventhQuality.None,
                ChordInversion.Root);
        }

        private static ChordRecipe Seventh(int degree, ChordQuality quality, SeventhQuality seventhQuality, int rootOffset = 0)
        {
            return new ChordRecipe(
                degree,
                quality,
                ChordExtension.Seventh,
                rootOffset,
                seventhQuality,
                ChordInversion.Root);
        }

        private static void AssertChordPitchClasses(TheoryKey key, ChordRecipe recipe, int[] expected)
        {
            var pcs = TheoryChord.BuildChordPitchClasses(key, recipe);
            CollectionAssert.AreEquivalent(
                expected,
                pcs,
                $"Chord {TheoryChord.RecipeToRomanNumeral(key, recipe)} in {key} should match expected pitch classes.");
        }

        /// <summary>
        /// Helper to parse a Roman numeral string and analyze it, following the same path Chord Lab uses.
        /// </summary>
        private static ChordFunctionProfile AnalyzeRoman(TheoryKey key, string roman)
        {
            Assert.IsTrue(
                TheoryChord.TryParseRomanNumeral(key, roman, out var recipe),
                $"Failed to parse roman numeral '{roman}' for key {key}"
            );

            var profile = TheoryChord.AnalyzeChordProfile(key, recipe);
            return profile;
        }

        /// <summary>
        /// Asserts that a Roman numeral chord is diatonic in the given key.
        /// </summary>
        private static void AssertDiatonic(TheoryKey key, string roman)
        {
            var profile = AnalyzeRoman(key, roman);
            Assert.AreEqual(
                ChordDiatonicStatus.Diatonic,
                profile.DiatonicStatus,
                $"Expected '{roman}' to be diatonic in {key}, but got {profile.DiatonicStatus}"
            );
        }

        /// <summary>
        /// Asserts that a Roman numeral chord is non-diatonic in the given key.
        /// </summary>
        private static void AssertNonDiatonic(TheoryKey key, string roman)
        {
            var profile = AnalyzeRoman(key, roman);
            Assert.AreEqual(
                ChordDiatonicStatus.NonDiatonic,
                profile.DiatonicStatus,
                $"Expected '{roman}' to be NON-diatonic in {key}, but got {profile.DiatonicStatus}"
            );
        }

        [Test]
        public void CIonian_TriadsMatchLegacyPitchClasses()
        {
            var key = new TheoryKey(ScaleMode.Ionian);

            AssertChordPitchClasses(key, Triad(1, ChordQuality.Major), new[] { 0, 4, 7 });   // C major
            AssertChordPitchClasses(key, Triad(5, ChordQuality.Major), new[] { 7, 11, 2 });  // G major
            AssertChordPitchClasses(key, Triad(6, ChordQuality.Minor), new[] { 9, 0, 4 });   // A minor
            AssertChordPitchClasses(key, Triad(4, ChordQuality.Major), new[] { 5, 9, 0 });   // F major
        }

        [Test]
        public void EbIonian_TriadsTransposeCorrectly()
        {
            var key = new TheoryKey(3, ScaleMode.Ionian); // 3 = Eb

            AssertChordPitchClasses(key, Triad(1, ChordQuality.Major), new[] { 3, 7, 10 });   // Eb major
            AssertChordPitchClasses(key, Triad(5, ChordQuality.Major), new[] { 10, 2, 5 });   // Bb major
            AssertChordPitchClasses(key, Triad(6, ChordQuality.Minor), new[] { 0, 3, 7 });    // C minor
            AssertChordPitchClasses(key, Triad(4, ChordQuality.Major), new[] { 8, 0, 3 });    // Ab major
        }

        [Test]
        public void RecipeToRomanNumeral_KeyAwareAccidentalsMatchDocs()
        {
            var cAeolian = new TheoryKey(ScaleMode.Aeolian);

            Assert.AreEqual("vi", TheoryChord.RecipeToRomanNumeral(cAeolian, Triad(6, ChordQuality.Minor)));
            Assert.AreEqual("nvi", TheoryChord.RecipeToRomanNumeral(cAeolian, Triad(6, ChordQuality.Minor, 1)));
            Assert.AreEqual("bVII", TheoryChord.RecipeToRomanNumeral(cAeolian, Triad(7, ChordQuality.Major, -1)));

            var cIonian = new TheoryKey(ScaleMode.Ionian);

            Assert.AreEqual("vi", TheoryChord.RecipeToRomanNumeral(cIonian, Triad(6, ChordQuality.Minor)));
            Assert.AreEqual("vi", TheoryChord.RecipeToRomanNumeral(cIonian, Triad(6, ChordQuality.Minor, 0))); // "nvi" input collapses to vi
            Assert.AreEqual("bVI", TheoryChord.RecipeToRomanNumeral(cIonian, Triad(6, ChordQuality.Major, -1)));
        }

        [Test]
        public void CIonian_SeventhChordsAreDiatonic()
        {
            var key = new TheoryKey(ScaleMode.Ionian);

            // ii7 and iii7 should be diatonic in C Ionian
            var ii7 = Seventh(2, ChordQuality.Minor, SeventhQuality.Minor7);
            var iii7 = Seventh(3, ChordQuality.Minor, SeventhQuality.Minor7);

            var ii7Profile = TheoryChord.AnalyzeChordProfile(key, ii7);
            var iii7Profile = TheoryChord.AnalyzeChordProfile(key, iii7);

            Assert.AreEqual(ChordDiatonicStatus.Diatonic, ii7Profile.DiatonicStatus,
                "ii7 should be Diatonic in C Ionian");
            Assert.AreEqual(ChordDiatonicStatus.Diatonic, iii7Profile.DiatonicStatus,
                "iii7 should be Diatonic in C Ionian");
        }

        [Test]
        public void GIonian_SeventhChordsAreDiatonic()
        {
            var key = new TheoryKey(7, ScaleMode.Ionian); // G Ionian (tonic = 7)

            // ii7 and iii7 should be diatonic in G Ionian
            var ii7 = Seventh(2, ChordQuality.Minor, SeventhQuality.Minor7);
            var iii7 = Seventh(3, ChordQuality.Minor, SeventhQuality.Minor7);

            var ii7Profile = TheoryChord.AnalyzeChordProfile(key, ii7);
            var iii7Profile = TheoryChord.AnalyzeChordProfile(key, iii7);

            Assert.AreEqual(ChordDiatonicStatus.Diatonic, ii7Profile.DiatonicStatus,
                "ii7 should be Diatonic in G Ionian");
            Assert.AreEqual(ChordDiatonicStatus.Diatonic, iii7Profile.DiatonicStatus,
                "iii7 should be Diatonic in G Ionian");
        }

        [Test]
        public void CIonian_Roman_ii7_iii7_AreDiatonic()
        {
            var key = new TheoryKey(0, ScaleMode.Ionian); // C Ionian

            AssertDiatonic(key, "ii7");   // Dm7
            AssertDiatonic(key, "iii7");  // Em7
        }

        [Test]
        public void GIonian_Roman_ii7_iii7_AreDiatonic()
        {
            var key = new TheoryKey(7, ScaleMode.Ionian); // G Ionian (tonic PC 7)

            AssertDiatonic(key, "ii7");   // Am7
            AssertDiatonic(key, "iii7");  // Bm7
        }

        [Test]
        public void CIonian_Roman_bVI7_IsNonDiatonic()
        {
            var key = new TheoryKey(0, ScaleMode.Ionian); // C Ionian
            AssertNonDiatonic(key, "bVI7"); // Ab7
        }

        [Test]
        public void GIonian_Roman_V7_IsDiatonic()
        {
            var key = new TheoryKey(7, ScaleMode.Ionian); // G Ionian
            AssertDiatonic(key, "V7"); // D7
        }
    }
}

