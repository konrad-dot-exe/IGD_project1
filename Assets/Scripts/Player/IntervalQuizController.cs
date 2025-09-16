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

        [Header("Input")]
        [SerializeField] Key submitKey = Key.Space;
        [SerializeField] Key optionalListenKey = Key.None; // (optional keyboard hold key; leave None to use only RMB)

        [Header("Lockout")]
        [SerializeField] float missLockout = 1.0f;
        bool isLockedOut = false;

        int selectedIndex = 0; // index into IntervalTable.All
        Coroutine listenLoop;

        void Update()
        {
            // Cycle with Q/E or Wheel
            if (Keyboard.current.qKey.wasPressedThisFrame) Cycle(-1);
            if (Keyboard.current.eKey.wasPressedThisFrame) Cycle(+1);
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) Cycle(scroll > 0 ? +1 : -1);

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // RMB OR optional keyboard key
            bool listenHeld =
                (mouse != null && mouse.rightButton.isPressed) ||
                (kb != null && optionalListenKey != Key.None && kb[optionalListenKey].isPressed);

            if (listenHeld && listenLoop == null)
                listenLoop = StartCoroutine(ListenCoroutine());
            if (!listenHeld && listenLoop != null)
            {
                StopCoroutine(listenLoop);
                listenLoop = null;
            }

            // Submit
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
                    float dist = Vector3.Distance(transform.position, t.transform.position);
                    float vol = Mathf.Lerp(minVolume, 1f, 1f - Mathf.Clamp01(dist / maxListenDist));

                    float rootFreq = ToneSynth.MidiToFreq(t.rootMidi);
                    float targetFreq = ToneSynth.MidiToFreq(t.rootMidi + t.interval.semitones);

                    var clip1 = ToneSynth.CreateTone(rootFreq, beepDur, 48000f, waveform);
                    var clip2 = ToneSynth.CreateTone(targetFreq, beepDur, 48000f, waveform);

                    audioSource.PlayOneShot(clip1, vol);
                    yield return new WaitForSeconds(beepDur + gapDur);
                    audioSource.PlayOneShot(clip2, vol);
                    yield return new WaitForSeconds(beepDur + listenRepeatGap);
                }
                else
                {
                    yield return null;
                }
            }
        }

        void TrySubmit()
        {
            var t = turret.CurrentTarget;
            if (t == null) return;

            var chosen = IntervalTable.ByIndex(selectedIndex);
            if (chosen.semitones == t.interval.semitones)
            {
                // Correct → spawn missile, score, HUD toast
                var go = Instantiate(missilePrefab, missileSpawn ? missileSpawn.position : transform.position, transform.rotation);
                go.GetComponent<HomingMissile>().Init(t);
                GameManager.Instance.OnAnswer(true, t.interval);
                UIHud.Instance?.ToastCorrect(t.interval.displayName, t.transform.position);
            }
            else
            {
                // Wrong → lockout, score penalty, screen flash
                GameManager.Instance.OnAnswer(false, t.interval);
                UIHud.Instance?.FlashWrong();
                StartCoroutine(Lockout());
            }
        }

        IEnumerator Lockout()
        {
            isLockedOut = true;
            yield return new WaitForSeconds(missLockout);
            isLockedOut = false;
        }
    }
}
