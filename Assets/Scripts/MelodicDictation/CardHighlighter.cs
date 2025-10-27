using UnityEngine;

[DisallowMultipleComponent]
public class CardHighlighter : MonoBehaviour
{
    [SerializeField] Renderer[] targets;          // assign GlowQuad’s Renderer here
    [SerializeField] Color highlightColor = new Color(1f, 0.9f, 0.5f, 1f); // warm
    [SerializeField] float fadeSpeed = 6f;        // higher = snappier

    // we’ll lerp emission per-renderer using MaterialPropertyBlock (no material instancing)
    MaterialPropertyBlock mpb;
    Color current; // tracked emission color
    Color target;  // desired emission color

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        current = Color.black;           // start off
        target = Color.black;
        ApplyToAll(current);
    }

    void Update()
    {
        if (current == target) return;
        current = Color.Lerp(current, target, Time.deltaTime * fadeSpeed);
        ApplyToAll(current);
    }

    public void Set(bool on)
    {
        target = on ? highlightColor : Color.black;
    }

    void ApplyToAll(Color emission)
    {
        if (targets == null) return;
        foreach (var r in targets)
        {
            if (!r) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", emission);   // URP uses _EmissionColor
            r.SetPropertyBlock(mpb);
            // Toggle renderer if you prefer hard on/off:
            // r.enabled = emission.maxColorComponent > 0.001f;
        }
    }
}
