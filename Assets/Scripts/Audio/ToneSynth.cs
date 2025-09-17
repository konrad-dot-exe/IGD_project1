using UnityEngine;

namespace EarFPS
{
    public enum Waveform { SineWithH2, PureSine }

    [System.Serializable]
    public struct ADSR
    {
        public float attack;   // seconds
        public float decay;    // seconds
        public float sustain;  // 0..1
        public float release;  // seconds
    }

    public static class ToneSynth
    {
        public static AudioClip CreateTone(
            float frequency, float durationSec, float sampleRate = 48000f,
            Waveform wave = Waveform.SineWithH2, float h2Gain = 0.15f,
            ADSR? env = null)
        {
            int samples = Mathf.CeilToInt(durationSec * sampleRate);
            float[] data = new float[samples];
            float twoPi = Mathf.PI * 2f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / sampleRate;
                float s = Mathf.Sin(twoPi * frequency * t);
                if (wave == Waveform.SineWithH2)
                    s = Mathf.Clamp(s + h2Gain * Mathf.Sin(twoPi * frequency * 2f * t), -1f, 1f);
                data[i] = s;
            }

            if (env.HasValue) ApplyADSR(data, sampleRate, env.Value);

            var clip = AudioClip.Create($"tone_{frequency:F1}_{durationSec:F2}", samples, 1, (int)sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void ApplyADSR(float[] data, float sampleRate, ADSR e)
        {
            int N = data.Length;
            int a = Mathf.Max(1, Mathf.RoundToInt(e.attack * sampleRate));
            int d = Mathf.Max(1, Mathf.RoundToInt(e.decay * sampleRate));
            int r = Mathf.Max(1, Mathf.RoundToInt(e.release * sampleRate));

            int sustainStart = Mathf.Min(a + d, N);
            int releaseStart = Mathf.Max(N - r, sustainStart);
            float sLvl = Mathf.Clamp01(e.sustain);

            for (int i = 0; i < N; i++)
            {
                float g;
                if (i < a)                          g = i / (float)a;                                      // attack up 0→1
                else if (i < sustainStart)          g = 1f - (1f - sLvl) * ((i - a) / (float)d);           // decay 1→s
                else if (i < releaseStart)          g = sLvl;                                              // sustain
                else                                 g = sLvl * (1f - (i - releaseStart) / (float)r);       // release s→0
                data[i] *= g;
            }
        }

        public static float MidiToFreq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }
}
