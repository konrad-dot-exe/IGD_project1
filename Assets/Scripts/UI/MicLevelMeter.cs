using UnityEngine;
using UnityEngine.UI;

namespace EarFPS
{
    public class MicLevelMeter : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] Image barFill;                  // Image Type=Filled, Method=Vertical, Origin=Bottom
        [SerializeField] RectTransform barRect;          // (optional) drive height instead of fill
        [SerializeField] float maxHeight = 200f;
        [SerializeField] CanvasGroup group;              // (optional) fades when not listening

        [Header("Mic Capture")]
        [SerializeField] string deviceName = null;       // null => default
        [SerializeField] int sampleRate = 16000;
        [SerializeField] int bufferSeconds = 1;
        [SerializeField] bool startOnEnable = true;

        public enum MeterMode { RMS, Peak }
        [Header("Metering")]
        [SerializeField] MeterMode mode = MeterMode.RMS;
        [SerializeField] float minDb = -60f;             // floor (silence)
        [SerializeField] float maxDb = -20f;             // loud
        [SerializeField] float boostDb = 6f;             // shifts sensitivity up
        [SerializeField, Range(0.25f, 3f)] float curve = 0.7f; // <1 = more lift in low levels
        [SerializeField] float attack = 60f;             // rise units/sec
        [SerializeField] float release = 18f;            // fall units/sec
        [SerializeField] float idleOpacity = 0.35f;      // UI alpha when not listening
        [SerializeField] bool dampenWhenNotListening = true;

        [Header("Auto-Calibrate")]
        [SerializeField] bool autoCalibrateOnStart = true;
        [SerializeField] float calibrateSeconds = 0.6f;  // listen to room noise this long
        [SerializeField] float headroomDb = 28f;         // window height above floor
        [SerializeField] float floorMarginDb = 3f;       // minDb = floor - margin

        [Header("Debug")]
        [SerializeField] bool debugLabel = false;
        [SerializeField] UnityEngine.UI.Text dbgText;    // optional legacy Text or TMP via wrapper

        AudioClip micClip;
        float[] tempBuf;
        bool listening;
        float current;                                   // smoothed 0..1
        float lastDb;                                    // most recent raw dB

        public float level => current;   // for existing VoiceUI.cs
        public float Level => current;   // optional, nicer casing

        public void StartMeter() => StartMic();
        public void StopMeter()  => StopMic();

        void OnEnable()
        {
            if (startOnEnable) StartMic();
            ApplyOpacity();

            if (autoCalibrateOnStart) StartCoroutine(CoAutoCalibrate());
        }

        void OnDisable()
        {
            StopMic();
        }

        void Update()
        {
            if (micClip == null) return;

            // Read instantaneous level
            float raw01 = ReadLevel01(out lastDb);

            // Smooth
            float speed = (raw01 > current) ? attack : release;
            current = Mathf.MoveTowards(current, raw01, speed * Time.unscaledDeltaTime);

            // Drive UI
            if (barFill) barFill.fillAmount = current;
            if (barRect) barRect.sizeDelta = new Vector2(barRect.sizeDelta.x, current * maxHeight);

            if (debugLabel && dbgText)
                dbgText.text = $"dB: {lastDb:0.0}  t:{raw01:0.00}";
        }

        // ---- Public API -----------------------------------------------------

        public void SetActiveListening(bool value)
        {
            listening = value;
            ApplyOpacity();
            if (!listening && dampenWhenNotListening)
                current = 0f; // visually drop when not listening
        }

        public void StartMic()
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[MicLevelMeter] No microphone devices found.");
                return;
            }
            if (micClip != null) return;

            micClip = Microphone.Start(deviceName, true, bufferSeconds, sampleRate);
            tempBuf = new float[Mathf.Max(256, sampleRate / 40)]; // shorter window → snappier
            // Optionally wait until microphone actually starts producing data
            // (not strictly needed; our read safely returns 0 until ready)
        }

        public void StopMic()
        {
            if (micClip == null) return;
            Microphone.End(deviceName);
            micClip = null;
            tempBuf = null;
        }

        // ---- Internals ------------------------------------------------------

        System.Collections.IEnumerator CoAutoCalibrate()
        {
            // sample ambient noise floor
            float t = 0f;
            float minSeen =  999f;
            float maxSeen = -999f;

            while (t < calibrateSeconds)
            {
                t += Time.unscaledDeltaTime;
                _ = ReadLevel01(out float dbInstant);
                if (dbInstant < minSeen) minSeen = dbInstant;
                if (dbInstant > maxSeen) maxSeen = dbInstant;
                yield return null;
            }

            // Treat minSeen as floor, add margin; define a usable window above it
            float newMin = Mathf.Min(minSeen - floorMarginDb, -80f); // clamp lower bound
            float newMax = newMin + Mathf.Max(10f, headroomDb);

            minDb = newMin;
            maxDb = newMax;
            // Debug.Log($"[MicLevelMeter] Auto-calibrated: minDb={minDb:0.0}, maxDb={maxDb:0.0}");
        }

        float ReadLevel01(out float outDb)
        {
            outDb = -80f;

            int pos = Microphone.GetPosition(deviceName);
            if (pos <= 0 || tempBuf == null || micClip == null) return 0f;

            int n = tempBuf.Length;
            int start = pos - n;
            if (start < 0) start += micClip.samples;      // wrap
            micClip.GetData(tempBuf, start);

            // RMS or Peak
            float value;
            if (mode == MeterMode.RMS)
            {
                double sum = 0;
                for (int i = 0; i < n; i++) sum += tempBuf[i] * tempBuf[i];
                float rms = Mathf.Sqrt((float)(sum / n));
                outDb = 20f * Mathf.Log10(Mathf.Max(1e-7f, rms));
            }
            else
            {
                float peak = 0f;
                for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(tempBuf[i]));
                outDb = 20f * Mathf.Log10(Mathf.Max(1e-7f, peak));
            }

            // Map to 0..1 with boost + curve
            float t = Mathf.InverseLerp(minDb, maxDb, outDb + boostDb);
            t = Mathf.Clamp01(Mathf.Pow(t, curve));

            // Optional dampening when not listening
            if (!listening && dampenWhenNotListening) t *= 0.15f;

            return t;
        }

        void ApplyOpacity()
        {
            if (!group) return;
            group.alpha = listening ? 1f : idleOpacity;
        }
    }
}
