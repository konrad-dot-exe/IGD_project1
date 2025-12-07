using UnityEngine;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// MonoBehaviour component for running TheoryChord tests from Unity Editor.
    /// Attach to any GameObject and use Context Menu (right-click component) to run tests.
    /// </summary>
    public class TheoryChordTestRunner : MonoBehaviour
    {
        [ContextMenu("Test C Ionian Diatonic Triads")]
        void TestCIonianTriads()
        {
            TheoryChordTests.TestCIonianTriads();
        }

        [ContextMenu("Test Augmented Chord")]
        void TestAugmentedChord()
        {
            TheoryChordTests.TestAugmentedChord();
        }

        [ContextMenu("Test Diminished Chord Variants")]
        void TestDiminishedVariants()
        {
            TheoryChordTests.TestDiminishedVariants();
        }

        [ContextMenu("Test Build Progression")]
        void TestBuildProgression()
        {
            TheoryChordTests.TestBuildProgression();
        }

        [ContextMenu("Test Diatonic Seventh Qualities")]
        void TestDiatonicSeventhQualities()
        {
            TheoryChordTests.TestDiatonicSeventhQualities();
        }
    }
}

