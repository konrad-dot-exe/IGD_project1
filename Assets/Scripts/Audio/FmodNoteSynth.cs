using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMOD.Studio;
using FMODUnity;

[System.Serializable]
public class SampleData
{
    [Tooltip("FMOD EventReference for this sample")]
    public EventReference eventRef;
    
    [Tooltip("MIDI note that this sample represents (e.g., C4=60, C3=48, C2=36)")]
    [Range(0, 127)]
    public int rootMidi = 60;
    
    [Tooltip("Optional label for this sample (for debugging)")]
    public string label = "";
}

[DisallowMultipleComponent]
public class FmodNoteSynth : MonoBehaviour
{
    [Header("FMOD - Single Sample (Legacy)")]
    [Tooltip("Legacy: Single sample for all notes. Use Multi-Sample system below instead.")]
    public EventReference noteEvent;

    [Header("FMOD - Multi-Sample System")]
    [Tooltip("Multiple samples at different base pitches. System will automatically select the closest sample to minimize pitch-shifting.")]
    [SerializeField] SampleData[] samples = new SampleData[0];
    
    [Tooltip("If true, use multi-sample system. If false, use legacy single sample.")]
    [SerializeField] bool useMultiSample = false;

    [Header("Playback")]
    [Range(0,127)] public int rootMidi = 60;         // MIDI note that the sample represents (e.g., C4=60) - Only used if useMultiSample=false
    [Range(0f,1f)] public float defaultVelocity = 0.9f;
    public bool positional3D = true;                 // if false, plays 2D (no spatialization)

    [Header("Debug")]
    [Tooltip("Enable debug logging for pitch-dependent delay diagnosis")]
    [SerializeField] bool debugLogs = false;

    // simple polyphony: one instance per MIDI note
    private readonly Dictionary<int, EventInstance> _voices = new();
    private readonly Dictionary<int, float> _noteStartTimes = new(); // Track when each note was started

    /// <summary>
    /// Finds the best sample to use for a given MIDI note, minimizing pitch-shifting.
    /// Returns the sample index and the pitch ratio needed.
    /// </summary>
    private bool FindBestSample(int midi, out EventReference eventRef, out int sampleRootMidi, out float pitchRatio)
    {
        eventRef = default;
        sampleRootMidi = rootMidi;
        pitchRatio = 1f;

        if (useMultiSample && samples != null && samples.Length > 0)
        {
            // Find the sample with the smallest semitone offset
            int bestIndex = 0;
            int bestSemitoneOffset = Mathf.Abs(midi - samples[0].rootMidi);
            
            for (int i = 1; i < samples.Length; i++)
            {
                if (!samples[i].eventRef.IsNull)
                {
                    int semitoneOffset = Mathf.Abs(midi - samples[i].rootMidi);
                    if (semitoneOffset < bestSemitoneOffset)
                    {
                        bestSemitoneOffset = semitoneOffset;
                        bestIndex = i;
                    }
                }
            }
            
            if (!samples[bestIndex].eventRef.IsNull)
            {
                eventRef = samples[bestIndex].eventRef;
                sampleRootMidi = samples[bestIndex].rootMidi;
                float semitone = midi - sampleRootMidi;
                pitchRatio = Mathf.Pow(2f, semitone / 12f);
                
                if (debugLogs)
                {
                    string label = string.IsNullOrEmpty(samples[bestIndex].label) 
                        ? $"Sample {bestIndex}" 
                        : samples[bestIndex].label;
                    Debug.Log($"[FmodNoteSynth] Selected {label} (rootMidi={sampleRootMidi}) for MIDI={midi}, " +
                             $"Semitone offset={semitone:F1}, PitchRatio={pitchRatio:F4}");
                }
                return true;
            }
        }
        
        // Fall back to legacy single sample
        if (!noteEvent.IsNull)
        {
            eventRef = noteEvent;
            sampleRootMidi = rootMidi;
            float semitone = midi - rootMidi;
            pitchRatio = Mathf.Pow(2f, semitone / 12f);
            return true;
        }
        
        return false;
    }

    public void NoteOn(int midi, float velocity = -1f)
    {
        if (_voices.ContainsKey(midi)) return;       // already sounding

        // Find the best sample for this MIDI note
        if (!FindBestSample(midi, out EventReference eventRef, out int sampleRootMidi, out float pitchRatio))
        {
            if (debugLogs) Debug.LogError($"[FmodNoteSynth] No valid sample found for MIDI note {midi}");
            return;
        }

        float noteStartTime = Time.realtimeSinceStartup;
        
        var inst = RuntimeManager.CreateInstance(eventRef);
        if (!inst.isValid())
        {
            if (debugLogs) Debug.LogWarning($"[FmodNoteSynth] Failed to create instance for MIDI note {midi}");
            return;
        }

        // optional 3D attach
        // if (positional3D)
        //     RuntimeManager.AttachInstanceToGameObject(inst, transform, GetComponent<Rigidbody>());

        float beforePitchTime = Time.realtimeSinceStartup;
        inst.setPitch(pitchRatio);
        float afterPitchTime = Time.realtimeSinceStartup;

        // quick-and-dirty velocity to volume (0..1). If your event has its own dynamics, you can remove this.
        float v = (velocity >= 0f) ? Mathf.Clamp01(velocity) : defaultVelocity;
        inst.setVolume(v);

        // Check event description for potential delays before starting
        if (debugLogs)
        {
            inst.getDescription(out EventDescription desc);
            if (desc.isValid())
            {
                desc.getLength(out int lengthMs);
                desc.getMinMaxDistance(out float minDist, out float maxDist);
                Debug.Log($"[FmodNoteSynth] Event description: Length={lengthMs}ms, MinDist={minDist:F2}, MaxDist={maxDist:F2}");
            }
        }

        float beforeStartTime = Time.realtimeSinceStartup;
        inst.start();
        float afterStartTime = Time.realtimeSinceStartup;
        
        _voices[midi] = inst;
        _noteStartTimes[midi] = noteStartTime;

        if (debugLogs)
        {
            float semitone = midi - sampleRootMidi;
            Debug.Log($"[FmodNoteSynth] NoteOn: MIDI={midi}, SampleRootMidi={sampleRootMidi}, " +
                     $"Semitone={semitone:F2}, PitchRatio={pitchRatio:F4}, " +
                     $"setPitch took {(afterPitchTime - beforePitchTime) * 1000:F2}ms, " +
                     $"start() took {(afterStartTime - beforeStartTime) * 1000:F2}ms");
        }

        // Start coroutine to monitor when playback actually begins
        if (debugLogs)
        {
            StartCoroutine(MonitorPlaybackStart(midi, inst, noteStartTime, pitchRatio));
        }
    }

    /// <summary>
    /// Monitors FMOD playback state to detect when audio actually starts playing.
    /// </summary>
    IEnumerator MonitorPlaybackStart(int midi, EventInstance inst, float startTime, float pitchRatio)
    {
        float checkInterval = 0.001f; // Check every 1ms
        float maxWaitTime = 1.0f; // Don't wait more than 1 second
        float elapsed = 0f;
        bool playbackStateDetected = false;
        bool timelineStarted = false;
        float timelineStartTime = -1f;
        int lastTimelinePos = 0;
        float lastTimelineCheckTime = startTime;
        int timelineCheckCount = 0;

        // Check for start offset immediately after start()
        if (inst.isValid())
        {
            inst.getTimelinePosition(out int timelinePos);
            lastTimelinePos = timelinePos;
            if (debugLogs)
            {
                Debug.Log($"[FmodNoteSynth] Immediately after start(): TimelinePosition={timelinePos}ms");
            }
        }

        while (elapsed < maxWaitTime)
        {
            yield return new WaitForSeconds(checkInterval);
            elapsed += checkInterval;
            timelineCheckCount++;

            if (inst.isValid())
            {
                inst.getPlaybackState(out PLAYBACK_STATE state);
                inst.getTimelinePosition(out int timelinePos);
                
                if (state == PLAYBACK_STATE.PLAYING)
                {
                    // Log when we first detect PLAYING state
                    if (!playbackStateDetected)
                    {
                        float delayToPlayingState = Time.realtimeSinceStartup - startTime;
                        playbackStateDetected = true;
                        Debug.Log($"[FmodNoteSynth] PLAYING state detected: MIDI={midi}, PitchRatio={pitchRatio:F4}, " +
                                 $"Delay={delayToPlayingState * 1000:F2}ms, TimelinePos={timelinePos}ms");
                    }
                    
                    // Track when timeline actually starts moving (this is the key metric!)
                    if (!timelineStarted)
                    {
                        if (timelinePos > lastTimelinePos)
                        {
                            // Timeline has started moving!
                            timelineStarted = true;
                            timelineStartTime = Time.realtimeSinceStartup;
                            float delayToTimelineStart = timelineStartTime - startTime;
                            
                            Debug.Log($"[FmodNoteSynth] *** TIMELINE STARTED MOVING ***: MIDI={midi}, PitchRatio={pitchRatio:F4}, " +
                                     $"TimelinePos={timelinePos}ms (was {lastTimelinePos}ms), " +
                                     $"Delay from NoteOn={delayToTimelineStart * 1000:F2}ms ({delayToTimelineStart:F4}s)");
                        }
                        else if (timelineCheckCount % 100 == 0) // Log every 100ms while waiting
                        {
                            float currentDelay = Time.realtimeSinceStartup - startTime;
                            Debug.Log($"[FmodNoteSynth] Waiting for timeline: MIDI={midi}, PitchRatio={pitchRatio:F4}, " +
                                     $"Elapsed={currentDelay * 1000:F2}ms, TimelinePos={timelinePos}ms (stuck at 0)");
                        }
                    }
                    else
                    {
                        // Timeline is moving - log progression periodically
                        if (timelineCheckCount % 200 == 0) // Every 200ms
                        {
                            float timeSinceTimelineStart = Time.realtimeSinceStartup - timelineStartTime;
                            Debug.Log($"[FmodNoteSynth] Timeline progressing: MIDI={midi}, TimelinePos={timelinePos}ms, " +
                                     $"TimeSinceTimelineStart={timeSinceTimelineStart * 1000:F2}ms");
                        }
                    }
                    
                    lastTimelinePos = timelinePos;
                    lastTimelineCheckTime = Time.realtimeSinceStartup;
                }
                else if (state == PLAYBACK_STATE.STOPPED || state == PLAYBACK_STATE.STOPPING)
                {
                    Debug.LogWarning($"[FmodNoteSynth] Note {midi} stopped. State: {state}, FinalTimelinePos={timelinePos}ms");
                    break;
                }
            }
            else
            {
                // Instance invalidated
                Debug.LogWarning($"[FmodNoteSynth] Instance invalidated for MIDI={midi}");
                break;
            }
        }

        // Final summary
        if (inst.isValid() && debugLogs)
        {
            inst.getTimelinePosition(out int finalTimelinePos);
            inst.getDescription(out EventDescription desc);
            
            string summary = $"[FmodNoteSynth] Summary for MIDI={midi}, PitchRatio={pitchRatio:F4}: ";
            
            if (playbackStateDetected)
            {
                float playingStateDelay = Time.realtimeSinceStartup - startTime;
                summary += $"PLAYING state detected at {playingStateDelay * 1000:F2}ms. ";
            }
            
            if (timelineStarted)
            {
                float timelineDelay = timelineStartTime - startTime;
                summary += $"Timeline started at {timelineDelay * 1000:F2}ms. ";
            }
            else
            {
                summary += "Timeline NEVER started moving! ";
            }
            
            summary += $"FinalTimelinePos={finalTimelinePos}ms";
            
            if (desc.isValid())
            {
                desc.getLength(out int lengthMs);
                summary += $", EventLength={lengthMs}ms";
            }
            
            Debug.Log(summary);
        }
    }

    public void NoteOff(int midi)
    {
        if (!_voices.TryGetValue(midi, out var inst)) return;
        if (inst.isValid())
        {
            inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);  // uses the event's release tail
            inst.release();
        }
        _voices.Remove(midi);
        _noteStartTimes.Remove(midi); // Clean up tracking
    }

    public void PlayOnce(int midi, float velocity, float durationSeconds)
    {
        NoteOn(midi, velocity);
        StartCoroutine(ReleaseAfter(midi, durationSeconds));
    }

    System.Collections.IEnumerator ReleaseAfter(int midi, float s)
    {
        yield return new WaitForSeconds(s);
        NoteOff(midi);
    }

    public void StopAll()
    {
        foreach (var kv in _voices)
        {
            if (kv.Value.isValid())
            {
                kv.Value.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                kv.Value.release();
            }
        }
        _voices.Clear();
        _noteStartTimes.Clear(); // Clean up tracking
    }

    void OnDestroy()
    {
        // Safety cleanup: ensure all note instances are released when component is destroyed
        StopAll();
    }

    void OnDisable()
    {
        // Also cleanup when component is disabled (in case it's re-enabled later)
        StopAll();
    }
}
