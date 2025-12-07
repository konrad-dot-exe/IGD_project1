using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CardFlipper : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Flip")]
    [SerializeField] float flipDuration = 0.35f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);
    [SerializeField] Axis axis = Axis.Z;              // Z for your setup

    [Header("Lift (visual only)")]
    [Tooltip("Max visual lift (meters) while flipping. 0.02–0.06 looks good.")]
    [SerializeField] float flipHeight = 0.05f;
    [Tooltip("Lift shape over time (0..1). Defaults to a smooth bump peaking mid-flip).")]
    [SerializeField] AnimationCurve liftCurve =
        new AnimationCurve(new Keyframe(0,0,0,2), new Keyframe(0.5f,1,0,0), new Keyframe(1,0,-2,0));

    [Header("Refs")]
    [SerializeField] Transform rotateTarget;          // <- assign 'Visual' here
    [SerializeField] Rigidbody rb;                    // optional; on the prefab root

    [Header("Debug")]
    [SerializeField] bool log;

    public float Duration => flipDuration;

    public bool IsFaceUp { get; private set; }
    Coroutine flipCo;
    Vector3 baseLocalPos;                             // where Visual normally sits

    void Reset()
    {
        rotateTarget = transform;
        rb = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();
    }

    void Awake()
    {
        if (!rotateTarget) rotateTarget = transform;
        baseLocalPos = rotateTarget.localPosition;
    }

    public void InstantFaceDown()
    {
        if (flipCo != null) StopCoroutine(flipCo);
        IsFaceUp = false;
        SetAngleImmediate(0f);
        rotateTarget.localPosition = baseLocalPos;
        if (log) Debug.Log("[CardFlipper] InstantFaceDown");
    }

    [ContextMenu("Debug/Flip To Face Up")]   public void FlipToFaceUp()   => StartFlipTo(180f, true);
    [ContextMenu("Debug/Flip To Face Down")] public void FlipToFaceDown() => StartFlipTo(0f,    false);

    void StartFlipTo(float targetAngle, bool faceUpFlag)
    {
        if (!rotateTarget) rotateTarget = transform;
        if (flipCo != null) StopCoroutine(flipCo);
        flipCo = StartCoroutine(FlipToCo(targetAngle, faceUpFlag));
    }

    IEnumerator FlipToCo(float targetAngle, bool faceUpFlag)
    {
        IsFaceUp = faceUpFlag;

        // Angles
        Vector3 eStart = rotateTarget.localEulerAngles;
        float aStart = GetAxis(eStart); if (aStart > 180f) aStart -= 360f;
        float aEnd   = targetAngle;

        // Pause physics on the RB (if any) while we animate visuals
        bool hadRb = rb != null;
        bool prevKin = false;
        if (hadRb) { prevKin = rb.isKinematic; rb.isKinematic = true; }

        float t = 0f, dur = Mathf.Max(0.01f, flipDuration);
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float u = Mathf.Clamp01(t);
            float a = Mathf.Lerp(aStart, aEnd, ease.Evaluate(u));

            // rotation
            var e = rotateTarget.localEulerAngles;
            SetAxis(ref e, a);
            rotateTarget.localRotation = Quaternion.Euler(e);

            // lift on a bump curve (visual only)
            float h = flipHeight * Mathf.Max(0f, liftCurve.Evaluate(u));
            rotateTarget.localPosition = baseLocalPos + Vector3.up * h;

            yield return null;
        }

        // snap final
        SetAngleImmediate(aEnd);
        rotateTarget.localPosition = baseLocalPos;

        if (hadRb) rb.isKinematic = prevKin;

        if (log) Debug.Log($"[CardFlipper] END angle={targetAngle} IsFaceUp={IsFaceUp}");
        flipCo = null;
    }

    // ---- helpers ----
    float GetAxis(Vector3 e) => axis == Axis.X ? e.x : axis == Axis.Y ? e.y : e.z;
    void  SetAxis(ref Vector3 e, float v)
    {
        if (axis == Axis.X) e.x = v;
        else if (axis == Axis.Y) e.y = v;
        else e.z = v;
    }
    void SetAngleImmediate(float val)
    {
        var e = rotateTarget.localEulerAngles; SetAxis(ref e, val);
        rotateTarget.localRotation = Quaternion.Euler(e);
    }
}
