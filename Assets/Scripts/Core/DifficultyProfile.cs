using UnityEngine;
using EarFPS;

namespace Sonoria.Dictation {
public enum MovementSet { Stepwise, StepwisePlusThirds, DiatonicUpToFifths, DiatonicAll }
public enum MovementPolicy { StepwiseOnly, UpToMaxLeap }
// public enum MelodyDifficulty { Beginner, Intermediate, Advanced }
// public enum ContourType { Random, Arch, InvertedArch, Ascending, Descending }
// public enum ScaleMode { Ionian, Dorian, Phrygian, Lydian, Mixolydian, Aeolian, Locrian }


[CreateAssetMenu(menuName="Sonoria/Difficulty Profile", fileName="DifficultyProfile")]
public class DifficultyProfile : ScriptableObject {
[Header("Identity")] public string displayName = "Level"; [TextArea] public string notes;


[Header("Melody Length & Tempo")]
[Range(1,64)] public int melodyLength = 5;
[Range(0.2f, 2.0f)] public float noteDuration = 0.8f;
[Range(0f, 1.0f)] public float noteGap = 0.2f;


[Header("Key/Mode")]
public bool randomizeModeEachRound = false;
public EarFPS.ScaleMode[] allowedModes = new[]{ EarFPS.ScaleMode.Ionian };


[Header("Register / Range (MIDI)")]
public int registerMinMidi = 60; // C4
public int registerMaxMidi = 72; // C5


[Header("Scale Degree Pool (1..7)")]
[Tooltip("Whitelist of allowed degrees for content selection.")]
public bool[] allowedDegrees = new bool[7]{ true,true,true,false,false,false,false };


[Header("Anchors (start/end degree masks)")]
public bool[] allowedStartDegrees = new bool[7]{ true,false,true,false,true,false,false };
public bool[] allowedEndDegrees = new bool[7]{ true,false,false,false,false,false,false };


[Header("Movement")]
[Tooltip("Movement policy: StepwiseOnly = only ±1 diatonic step, UpToMaxLeap = allows leaps up to maxLeapSteps.")]
public MovementPolicy movementPolicy = MovementPolicy.UpToMaxLeap;

[Tooltip("Maximum diatonic steps allowed for leaps (1=stepwise only, 2=thirds, 3=fourths, ..., 8=octaves). Only used when movementPolicy is UpToMaxLeap.")]
[Range(1, 8)]
public int maxLeapSteps = 1;


[Header("Contour & Difficulty Biasing")]
public MelodyDifficulty difficulty = MelodyDifficulty.Beginner;
 public ContourType contour = ContourType.Random;


 [Header("Tendency Engine (Semitone Resolution)")]
 public bool enableTendencies = true;
 public float tendencyResolveProbability = 0.8f;
 public bool allowSmallDetours = true;


 [Header("Round Meta (Controller)")]
 public float preRollSeconds = 1.25f;
 public float replayPreRollMultiplier = 0.5f;
 public float playbackVelocity = 0.9f;


 [Header("Scoring & Timer (Controller)")]
 public int pointsPerNote = 100;
 public int pointsWrongNote = -100;
 public int pointsReplay = -25;
 public float pointsPerSecondInput = -5f;
 public int maxWrongPerRound = 3;
 [Tooltip("Time limit per round in seconds. Player loses if time expires.")]
 public float timeLimitPerRound = 60.0f;
}
}
