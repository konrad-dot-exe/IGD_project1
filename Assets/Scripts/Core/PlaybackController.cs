using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MusicDataController))]
public class PlaybackController : MonoBehaviour
{
    [Header("References")]
    public MusicDataController dataController;
    public FmodNoteSynth synth;   // drag your FmodNoteSynth here

    Coroutine _playCo;

    public bool IsReady => dataController?.Current != null;

    public void StopAll()
    {
        if (_playCo != null) StopCoroutine(_playCo);
        _playCo = null;
        if (synth != null) synth.StopAll();
    }

    public void Play()
    {
        if (!IsReady || synth == null) return;
        StopAll();
        _playCo = StartCoroutine(PlayCo());
    }

    IEnumerator PlayCo()
    {
        var md = dataController.Current;
        float spb = 60f / Mathf.Max(1, md.meta.tempo_bpm); // seconds per beat

        // simple real-time scheduler (coarse, good enough for MVP)
        float startTime = Time.realtimeSinceStartup;
        int i = 0;

        while (i < md.notes.Count)
        {
            var n = md.notes[i];

            // wait until it's time for this note (convert beats->seconds)
            float target = startTime + (n.t_beats * spb);
            float now = Time.realtimeSinceStartup;
            if (now < target) { yield return null; continue; }

            // map MIDI velocity (1..127) to 0..1
            float vel01 = Mathf.Clamp01(n.velocity / 127f);

            // duration in seconds
            float durSec = n.dur_beats * spb;

            // fire
            synth.PlayOnce(n.pitch, vel01, durSec);

            i++;
            yield return null;
        }
    }
}

