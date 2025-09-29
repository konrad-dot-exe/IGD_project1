using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace EarFPS
{
    public class IntervalQuizController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] TurretController turret;
        [SerializeField] AudioSource audioSource;
        [SerializeField] Transform missileSpawn;
        [SerializeField] GameObject missilePrefab;

        [Header("Listen")]
        [SerializeField] float beepDur = 0.20f;
        [SerializeField] float gapDur = 0.20f;
        [SerializeField] Waveform waveform = Waveform.SineWithH2;
        [SerializeField] float listenRepeatGap = 0.20f;
        [SerializeField] float minVolume = 0.25f;
        [SerializeField] float maxListenDist = 150f;
        [SerializeField] ListenZoom listenZoom;

        [Header("Listen rate vs distance")]
        [SerializeField] float noteGapFar = 0.20f;  // gap BETWEEN note1 & note2 when far
        [SerializeField] float noteGapNear = 0.04f;  // ...when very close (tremolo feel)
        [SerializeField] float pairGapFar = 0.50f;  // gap AFTER the pair when far
        [SerializeField] float pairGapNear = 0.06f;  // ...when very close

        [Header("Input")]
        [SerializeField] Key submitKey = Key.Space;
        [SerializeField] Key optionalListenKey = Key.None; // leave None to use only RMB

        [Header("Scroll Tuning (mouse wheel)")]
        [SerializeField] float scrollDeadzone = 0.5f;   // how big dy must be to count
        [SerializeField] float rearmDeadzone = 0.15f;  // how small dy must be to re-arm
        [SerializeField] float scrollCooldown = 0.12f;  // time between steps
        float nextScrollTime = 0f;
        int scrollLatchSign = 0;                      // 0=armed, +1/-1 latched until quiet
        InputAction scrollAction;                       // "<Mouse>/scroll" action

        [Header("Q/E Tuning (optional)")]
        [SerializeField] float keyStepCooldown = 0.06f;
        float nextKeyTime = 0f;

        [Header("Lockout")]
        [SerializeField] float missLockout = 1.0f;
        bool isLockedOut = false;

        [Header("Tone Envelope")]
        [SerializeField] ADSR envelope = new ADSR { attack = 0.005f, decay = 0.060f, sustain = 0.65f, release = 0.060f };
        [SerializeField] int toneSampleRate = 48000;

        [Header("Voice Ducking")]
        [SerializeField, Range(0f, 1f)] float duckedVolume = 0.25f; // volume while listening
        [SerializeField] float duckAttack = 0.05f;   // fade down time
        [SerializeField] float duckRelease = 0.12f;  // fade up time

        [Header("Muzzle")]
        [SerializeField] MuzzleRecoil muzzleRecoil; // drag your Muzzle here

        [Header("Missile")]
        [SerializeField] Color missileTint = new Color(0.30f, 0.90f, 1f);

        float duck = 1f;          // current duck multiplier
        Coroutine duckCo;


        int selectedIndex = 0; // index into IntervalTable.All
        Coroutine listenLoop;

        public int SelectedIndex => selectedIndex;
        public bool IsListening => listenLoop != null;

        void Awake()
        {
            Debug.Log($"[IQC] Awake on {name} (instanceID={GetInstanceID()})");
        }

        void OnEnable()
        {
            if (scrollAction == null)
                scrollAction = new InputAction(type: InputActionType.Value, binding: "<Mouse>/scroll");
            scrollAction.Enable();
            Debug.Log($"[IQC] OnEnable on {name} (instanceID={GetInstanceID()})");
        }

        void OnDisable()
        {
            scrollAction?.Disable();
        }

        void Start()
        {
            UIHud.Instance?.SetSelectedInterval(IntervalTable.ByIndex(selectedIndex));
            if (!muzzleRecoil) muzzleRecoil = FindFirstObjectByType<MuzzleRecoil>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // ----- SCROLL: edge-latched (one step per flick direction) + cooldown -----
            float dy = 0f;
            if (scrollAction != null) dy = scrollAction.ReadValue<Vector2>().y;
            else if (mouse != null) dy = mouse.scroll.ReadValue().y;

            // determine sign based on deadzone
            int sign = 0;
            if (dy > scrollDeadzone) sign = +1;
            else if (dy < -scrollDeadzone) sign = -1;

            // re-arm when truly quiet
            if (Mathf.Abs(dy) <= rearmDeadzone)
                scrollLatchSign = 0;

            // fire once when armed and not cooling down
            if (sign != 0 && scrollLatchSign == 0 && Time.time >= nextScrollTime)
            {
                Cycle(sign);
                scrollLatchSign = sign;                     // latch until quiet
                nextScrollTime = Time.time + scrollCooldown;
            }

            // ----- Q/E keys (with small cooldown) -----
            if (kb != null && Time.time >= nextKeyTime)
            {
                if (kb.qKey.wasPressedThisFrame) { Cycle(-1); nextKeyTime = Time.time + keyStepCooldown; }
                if (kb.eKey.wasPressedThisFrame) { Cycle(+1); nextKeyTime = Time.time + keyStepCooldown; }
            }

            // ----- LISTEN HOLD (RMB or optional keyboard key) -----
            bool listenHeld =
                (mouse != null && mouse.rightButton.isPressed) ||
                (kb != null && optionalListenKey != Key.None && kb[optionalListenKey].isPressed);

            // start listening
            if (listenHeld && listenLoop == null)
            {
                listenLoop = StartCoroutine(ListenCoroutine());
                listenZoom?.SetListening(true);
            }
            // stop listening
            if (!listenHeld && listenLoop != null)
            {
                StopCoroutine(listenLoop);
                listenLoop = null;
                listenZoom?.SetListening(false);
            }

            // ----- SUBMIT -----
            if (!isLockedOut && kb != null && kb[submitKey].wasPressedThisFrame)
                TrySubmit();
        }

        void Cycle(int dir)
        {
            selectedIndex = (selectedIndex + dir + IntervalTable.Count) % IntervalTable.Count;
            UIHud.Instance?.SetSelectedInterval(IntervalTable.ByIndex(selectedIndex));
        }

        IEnumerator ListenCoroutine()
        {
            while (true)
            {
                var t = turret.CurrentTarget;
                if (t != null)
                {
                    // ---- distance & volume ----
                    float dist = Vector3.Distance(transform.position, t.transform.position);
                    float vol = Mathf.Lerp(minVolume, 1f, 1f - Mathf.Clamp01(dist / maxListenDist));
                    vol = Mathf.Clamp01(vol * duck); // apply ducking

                    // ---- dynamic timing (closer = faster) ----
                    float closeness = Mathf.InverseLerp(maxListenDist, 0f, dist);
                    closeness = Mathf.SmoothStep(0f, 1f, closeness); // nicer ramp near the end

                    float gapBetweenNotes = Mathf.Lerp(noteGapFar, noteGapNear, closeness);
                    float gapAfterPair = Mathf.Lerp(pairGapFar, pairGapNear, closeness);

                    // ---- tone generation (with ADSR) ----
                    float rootFreq = ToneSynth.MidiToFreq(t.rootMidi);
                    float targetFreq = ToneSynth.MidiToFreq(t.rootMidi + t.interval.semitones);

                    // make clips long enough to include the release tail
                    float lenWithRelease = beepDur + envelope.release;

                    var clip1 = ToneSynth.CreateTone(rootFreq, lenWithRelease, toneSampleRate, waveform, 0.15f, envelope);
                    var clip2 = ToneSynth.CreateTone(targetFreq, lenWithRelease, toneSampleRate, waveform, 0.15f, envelope);

                    // ---- play pair, overlapping naturally if release > gaps ----
                    audioSource.PlayOneShot(clip1, vol);
                    yield return new WaitForSeconds(beepDur + gapBetweenNotes);

                    audioSource.PlayOneShot(clip2, vol);
                    yield return new WaitForSeconds(beepDur + gapAfterPair);
                }
                else
                {
                    // no target this frame — try again next frame
                    yield return null;
                }
            }
        }

        // Spacebar (or UI) submit using the wheel-selected interval
        void TrySubmit()
        {
            var t = turret.CurrentTarget;
            if (t == null) return;

            var chosen = IntervalTable.ByIndex(selectedIndex);
            if (chosen.semitones == t.interval.semitones)
            {
                // CORRECT:
                FireMissile(t, dud:false);
                GameManager.Instance.OnAnswer(true, t.interval);
                UIHud.Instance?.ToastCorrect(t.interval.displayName, t.transform.position);
            }
            else
            {
                // WRONG: fire a DUD + existing feedback
                FireMissile(t, dud:true);

                GameManager.Instance.OnAnswer(false, t.interval);
                //UIHud.Instance?.FlashWrong();
                GameManager.Instance.PlayWrongAnswerFeedback(); // if you already use this
                StartCoroutine(Lockout());
            }
        }

        IEnumerator Lockout()
        {
            isLockedOut = true;
            yield return new WaitForSeconds(missLockout);
            isLockedOut = false;
        }

        // Let voice temporarily mute the quiz tones while listening
        public void SetMuted(bool muted)
        {
            if (audioSource) audioSource.mute = muted;
        }

        // Submit using a specific interval (doesn't rely on the wheel selection)
        public bool TrySubmitInterval(IntervalDef chosen)
        {
            var t = turret.CurrentTarget;
            if (t == null) return false;

            if (chosen.semitones == t.interval.semitones)
            {
                // CORRECT:
                FireMissile(t, dud:false);
                GameManager.Instance.OnAnswer(true, t.interval);
                UIHud.Instance?.ToastCorrect(t.interval.displayName, t.transform.position);
            }
            else
            {
                // WRONG: fire a DUD + existing feedback
                FireMissile(t, dud:true);

                GameManager.Instance.OnAnswer(false, t.interval);
                //UIHud.Instance?.FlashWrong();
                GameManager.Instance.PlayWrongAnswerFeedback(); // if you already use this
                StartCoroutine(Lockout());
            }
            return true;
        }

        public void SetVoiceListening(bool listening)
        {
            if (duckCo != null) StopCoroutine(duckCo);
            float target = listening ? duckedVolume : 1f;
            float time   = listening ? duckAttack   : duckRelease;
            duckCo = StartCoroutine(DuckTo(target, time));

            if (listenZoom == null) return;

            if (listenZoom.isActiveAndEnabled && listenZoom.gameObject.activeInHierarchy)
                listenZoom.Begin(listening);        // animated coroutine version
            else
                listenZoom.SetImmediate(listening); // no coroutine; set values directly
        }


        IEnumerator DuckTo(float target, float time)
        {
            float start = duck;
            float t = 0f;
            // unscaled so it still feels snappy if you ever pause/slow time
            while (t < time)
            {
                t += Time.unscaledDeltaTime;
                duck = Mathf.Lerp(start, target, Mathf.Clamp01(t / time));
                yield return null;
            }
            duck = target;
            duckCo = null;
        }
        
        // --- shared fire routine ---
        void FireMissile(EnemyShip t, bool dud)
        {
            var spawnPos = missileSpawn ? missileSpawn.position : transform.position;
            var lookRot  = Quaternion.LookRotation(t.transform.position - spawnPos);

            var go = Instantiate(missilePrefab, spawnPos, lookRot);
            var hm = go.GetComponent<HomingMissile>();
            hm.Init(t, dud, missileTint);  // ← pass the shared missile color
            // use the prefab’s default tint, or replace with your chosen color
            hm.SetTint(hm.defaultMissileTint);

            muzzleRecoil?.Kick();
            SfxPalette.I?.OnMissileFire(spawnPos);
        }

    }
}
