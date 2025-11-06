using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sonoria.Dictation {
    public static class RoundLogger {
    [Serializable] class Snapshot {
    public string t; public string preset; public string notes;
    public int len; public float dur; public float gap;
    public string modes; public int regMin; public int regMax;
    public bool[] degrees; public bool[] start; public bool[] end;
    public string movement; public string diff; public string contour;
    public bool tendencies; public float tendProb; public bool detours;
    public float preRoll; public float replayRoll; public float vel;
    public int pNote, pWrong, pReplay, maxWrong; public float pPerSec;
    }


    public static void LogSnapshot(DifficultyProfile p, object gen, object ctrl){
    try{
    if (p==null) return;
    var s = new Snapshot{
    t = DateTime.UtcNow.ToString("o"),
    preset = p.displayName, notes = p.notes,
    len = p.melodyLength, dur = p.noteDuration, gap = p.noteGap,
    modes = string.Join(",", p.allowedModes), regMin = p.registerMinMidi, regMax = p.registerMaxMidi,
    degrees = p.allowedDegrees, start = p.allowedStartDegrees, end = p.allowedEndDegrees,
    movement = p.movement.ToString(), diff = p.difficulty.ToString(), //contour = p.contour.ToString(),
    tendencies = p.enableTendencies, tendProb = p.tendencyResolveProbability, detours = p.allowSmallDetours,
    preRoll = p.preRollSeconds, replayRoll = p.replayPreRollMultiplier, vel = p.playbackVelocity,
    pNote = p.pointsPerNote, pWrong = p.pointsWrongNote, pReplay = p.pointsReplay, maxWrong = p.maxWrongPerRound, pPerSec = p.pointsPerSecondInput
    };
    var json = JsonUtility.ToJson(s);
    var path = Path.Combine(Application.persistentDataPath, "sonoria_dictation_test_log.jsonl");
    File.AppendAllText(path, json+"\n");
    } catch (Exception e) { Debug.LogWarning($"RoundLogger failed: {e.Message}"); }
    }
    }
}