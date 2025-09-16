using UnityEngine;
using System;

namespace EarFPS
{
    public enum Waveform { SineWithH2, PureSine }

    public static class ToneSynth
    {
        public static AudioClip CreateTone(float frequency, float durationSec, float sampleRate = 48000f, Waveform wave = Waveform.SineWithH2, float h2Gain = 0.15f)
        {
            int samples = Mathf.CeilToInt(durationSec * sampleRate);
            float[] data = new float[samples];
            float twoPi = Mathf.PI * 2f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / sampleRate;
                float s = Mathf.Sin(twoPi * frequency * t);
                if (wave == Waveform.SineWithH2)
                    s = Mathf.Clamp(s + h2Gain * Mathf.Sin(twoPi * (frequency * 2f) * t), -1f, 1f);
                data[i] = s;
            }
            var clip = AudioClip.Create($"tone_{frequency:F1}_{durationSec:F2}", samples, 1, (int)sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static float MidiToFreq(int midi)
        {
            // A4=440, midi 69
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }
    }
}
