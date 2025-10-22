// Assets/Scripts/Player/MuzzleRecoil.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace EarFPS
{
    public class MuzzleRecoil : MonoBehaviour
    {
        public enum AxisMode { LocalX, LocalY, LocalZ, TransformRight, TransformUp, TransformForward, Custom }

        [Header("Recoil (local space)")]
        [SerializeField] float distance = 0.15f;
        [SerializeField] float backTime = 0.04f;
        [SerializeField] float returnTime = 0.12f;

        [Header("Direction")]
        [SerializeField] AxisMode axisMode = AxisMode.TransformUp; // your cylinder points along +Y visually, which is transform.up
        [SerializeField] bool invert = true;                        // recoil usually goes *backward*
        [SerializeField] Vector3 customLocalAxis = new Vector3(0, -1, 0); // used if AxisMode.Custom

        [Header("Easing")]
        [SerializeField] AnimationCurve easeOut = AnimationCurve.EaseInOut(0, 0, 1, 1); // back
        [SerializeField] AnimationCurve easeIn = AnimationCurve.EaseInOut(0, 0, 1, 1); // return
        
        [SerializeField] UnityEvent onKick;

        Vector3 baseLocalPos;
        Coroutine co;

        void Start()  // Start instead of Awake in case something positions this on enable
        {
            baseLocalPos = transform.localPosition;
        }

        Vector3 GetAxis()
        {
            Vector3 a;
            switch (axisMode)
            {
                case AxisMode.LocalX:         a = Vector3.right;   break;
                case AxisMode.LocalY:         a = Vector3.up;      break;
                case AxisMode.LocalZ:         a = Vector3.forward; break;
                case AxisMode.TransformRight: a = transform.right; break;
                case AxisMode.TransformUp:    a = transform.up;    break;
                case AxisMode.TransformForward: a = transform.forward; break;
                default:                      a = transform.TransformDirection(customLocalAxis); break;
            }
            if (invert) a = -a;
            return a.normalized;
        }

        public void Kick()
        {
            //Debug.Log($"[MuzzleRecoil] Kick() on {name}  enabled={isActiveAndEnabled}  t={Time.time:0.000}");
            if (!isActiveAndEnabled) return;
            onKick?.Invoke();               
            if (co != null) StopCoroutine(co);
            co = StartCoroutine(RecoilCo());
        }

        IEnumerator RecoilCo()
        {
            // Debug marker so we know this is running
            // Debug.Log($"[MuzzleRecoil] Kick at {Time.time}");

            Vector3 axis = GetAxis();
            Vector3 start = baseLocalPos;
            Vector3 end   = baseLocalPos + (transform.parent ? transform.parent.InverseTransformDirection(axis) : axis) * distance;

            // Back
            float t = 0f;
            while (t < backTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / backTime);
                transform.localPosition = Vector3.LerpUnclamped(start, end, easeOut.Evaluate(u));
                yield return null;
            }

            // Return
            t = 0f;
            while (t < returnTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / returnTime);
                transform.localPosition = Vector3.LerpUnclamped(end, start, easeIn.Evaluate(u));
                yield return null;
            }

            transform.localPosition = baseLocalPos;
            co = null;
        }

        // Handy inspector button: click the 3-dot menu on the component and choose this.
        [ContextMenu("Test Kick")]
        void TestKick() { Kick(); }
    }
}
