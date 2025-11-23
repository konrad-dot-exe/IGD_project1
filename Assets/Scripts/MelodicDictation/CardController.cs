using TMPro;
using UnityEngine;
using System.Collections;
using FMODUnity;

[DisallowMultipleComponent]
public class CardController : MonoBehaviour
{
    [Header("State")]
    public int noteMidi;
    public int index;

    [Header("Refs")]
    [SerializeField] TMP_Text noteLabel;
    [SerializeField] TMP_Text labelTR;     
    [SerializeField] TMP_Text labelBL;
    [SerializeField] bool   showCornerText = true;
    [SerializeField] CardFlipper flipper;
    [SerializeField] CardHighlighter highlighter;
    [SerializeField] Rigidbody rb;
    [SerializeField] EventReference cardSlapEvent;

    [Header("Drop Settings")]
    [SerializeField] bool usePhysicsDrop = true;
    [Tooltip("m/s under which the card is considered slow enough to start the settle timer")]
    [SerializeField] float settleSpeedThreshold = 0.05f;
    [Tooltip("Seconds the card must remain under the speed threshold before parking")]
    [SerializeField] float settleHoldTime = 0.20f;
    [Tooltip("Optional: tag of your table collider to nudge velocities on first contact")]
    [SerializeField] string tableTag = "Table";

    bool settling;
    private bool hasPlayedSlapSound = false;

    // --- DEBUG MENUS ---
    [ContextMenu("Debug/Reveal")]        void _dbg_Reveal() => Reveal();
    [ContextMenu("Debug/Hide")]          void _dbg_Hide() => Hide();
    [ContextMenu("Debug/Highlight On")]  void HighlightOn() => SetHighlight(true);
    [ContextMenu("Debug/Highlight Off")] void HighlightOff() => SetHighlight(false);

    void Reset()
    {
        noteLabel   = GetComponentInChildren<TMP_Text>(true);
        flipper     = GetComponent<CardFlipper>();
        highlighter = GetComponent<CardHighlighter>();
        rb          = GetComponent<Rigidbody>();
    }

    public void Init(int midi, int idx, string labelText)
    {
        noteMidi = midi;
        index = idx;
        if (noteLabel) { noteLabel.text = labelText; noteLabel.enabled = false; }
        if (flipper) flipper.InstantFaceDown();
        if (highlighter) highlighter.Set(false);
        if (rb) rb.WakeUp();
        hasPlayedSlapSound = false; // Reset flag for card reuse
        gameObject.SetActive(true);
    }

    // Call this when a card is created/configured
    public void SetNoteAndCorners(string noteText, int octave, bool showCornerLabels = true)
    {
        if (noteLabel) noteLabel.text = noteText;

        showCornerText = showCornerLabels;
        if (labelTR) { labelTR.text = octave.ToString(); labelTR.gameObject.SetActive(showCornerLabels); }
        if (labelBL) { labelBL.text = octave.ToString(); labelBL.gameObject.SetActive(showCornerLabels); }
    }

    public void BeginDrop()
    {
        if (!rb || !usePhysicsDrop)
        {
            //Debug.Log("[Card] BeginDrop: no RB or physics disabled → ParkKinematic");
            ParkKinematic();
            return;
        }

        rb.isKinematic = false;
        rb.useGravity  = true;

        rb.constraints = RigidbodyConstraints.FreezePositionX
                    | RigidbodyConstraints.FreezePositionZ
                    | RigidbodyConstraints.FreezeRotation;

        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;

        if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
        if (!rb.isKinematic) rb.angularVelocity = Vector3.zero;

        // Make sure the RB is active for the next physics step
        rb.WakeUp();

        settling = true;
        //Debug.Log($"[Card] BeginDrop OK (kinematic={rb.isKinematic}, gravity={rb.useGravity}, constraints={(int)rb.constraints}) on {name}");
        StartCoroutine(WaitForSettle());
    }

    IEnumerator WaitForSettle()
    {
        //  Give physics at least one step to apply gravity
        yield return new WaitForFixedUpdate();
        if (rb) rb.WakeUp();

        float underTimer = 0f;
        float threshSq = settleSpeedThreshold * settleSpeedThreshold;

        while (true)
        {
            if (!rb) yield break;

            // If the RB fell asleep by mistake, nudge it awake (don’t treat sleep as “settled”)
            if (rb.IsSleeping()) rb.WakeUp();

            // Only velocity decides “slow”—never IsSleeping
            bool slow = rb.linearVelocity.sqrMagnitude <= threshSq;

            underTimer = slow ? (underTimer + Time.fixedDeltaTime) : 0f;

            if (underTimer >= settleHoldTime)
            {
                ParkKinematic();
                yield break;
            }

            yield return new WaitForFixedUpdate();
        }
    }


    void ParkKinematic()
    {
        if (!rb) return;
        if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
        if (!rb.isKinematic) rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        settling = false;
    } 

    void OnCollisionEnter(Collision c)
    {
        if (!usePhysicsDrop || !settling || !rb) return;
        if (!string.IsNullOrEmpty(tableTag) && c.collider.CompareTag(tableTag))
        {
            // Play card slap sound on first table contact
            if (!hasPlayedSlapSound && !cardSlapEvent.IsNull)
            {
                Vector3 soundPosition = c.contactCount > 0 ? c.contacts[0].point : transform.position;
                RuntimeManager.PlayOneShot(cardSlapEvent, soundPosition);
                hasPlayedSlapSound = true;
            }
            
            // Nudge to stop sliding forever on first contact
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            if (!rb.isKinematic) rb.angularVelocity = Vector3.zero; 
            rb.isKinematic = true; 
        }
    }

    public void Reveal()
    {
        if (flipper) flipper.FlipToFaceUp();
        if (noteLabel) noteLabel.enabled = true;
    }

    public void Hide()
    {
        //if (noteLabel) noteLabel.enabled = false;
        if (flipper) flipper.FlipToFaceDown();
    }

    public void SetHighlight(bool on)
    {
        if (highlighter) highlighter.Set(on);
    }

    /// <summary>Slide off to the right then destroy (called on round win).</summary>
    public IEnumerator SlideOffAndDestroy(float distance = 2.5f, float duration = 0.5f)
    {
        var t0 = Time.time;
        var start = transform.position;
        var end = start + transform.right * distance;

        if (rb)
        {
            
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            if (!rb.isKinematic) rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        while (Time.time - t0 < duration)
        {
            float u = (Time.time - t0) / duration;
            transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, u));
            yield return null;
        }
        Destroy(gameObject);
    }

    // How long to wait after calling Reveal() for the flip to complete.
    public float GetRevealWaitSeconds(float fallback = 0.35f)
    {
        // small buffer to cover easing/bloom, etc.
        return (flipper != null ? flipper.Duration + 0.05f : fallback);
    }
}
