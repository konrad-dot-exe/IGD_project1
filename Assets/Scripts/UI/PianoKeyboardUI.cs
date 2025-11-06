using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PianoKeyboardUI : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] EarFPS.MelodicDictationController dictation;  // assign in Inspector
    [SerializeField] MinisPolySynth synth;                  // optional sound

    [SerializeField] FmodNoteSynth fmod;        
    [SerializeField] bool routeToFmod = true;  

    [Header("Range (MIDI notes)")]
    [SerializeField, Range(0,127)] int startNote = 48;  // C3
    [SerializeField, Range(0,127)] int endNote = 72;    // C5

    [Header("Layout")]
    [SerializeField] RectTransform keysParent;          // a bottom-anchored container
    [SerializeField] PianoKeyUI whiteKeyPrefab;
    [SerializeField] PianoKeyUI blackKeyPrefab;
    [SerializeField] float whiteWidth = 48f;
    [SerializeField] float whiteHeight = 180f;
    [SerializeField, Range(0.4f,0.75f)] float blackWidthRatio = 0.6f;
    [SerializeField, Range(0.5f,0.9f)] float blackHeightRatio = 0.65f;
    [SerializeField] float whiteSpacing = 2f;

    [SerializeField, Range(0f, 1f)]
    float blackHorizontalOffset = 0.65f; // relative to white width
    
    [Header("Interactivity / Fade")]
    [SerializeField] CanvasGroup canvasGroup;          // assign the keyboard container (or leave blank; we add one)
    [SerializeField, Range(0f,1f)] float lockedAlpha = 0.35f;
    bool inputLocked;
    Coroutine fadeCo;

    HashSet<int> heldNotes = new HashSet<int>();  // track down notes

    readonly int[] whitePCs = {0,2,4,5,7,9,11};      // pitch-classes with white keys
    readonly int[] blackPCs = {1,3,6,8,10};         // black keys
    readonly Dictionary<int, PianoKeyUI> _keyByNote = new();

    void Reset()
    {
        keysParent = GetComponent<RectTransform>();
    }

    void Start()
    {
        BuildKeyboard();

        // Ensure a CanvasGroup exists so we can fade + block raycasts
        if (!canvasGroup)
        {
            var go = keysParent ? keysParent.gameObject : this.gameObject;
            canvasGroup = go.GetComponent<CanvasGroup>();
            if (!canvasGroup) canvasGroup = go.AddComponent<CanvasGroup>();
        }

        // Start unlocked
        ApplyLockVisual(false, immediate: true);
    }
    
    void OnDisable()
    {
        // If the object gets disabled mid-press, clear visuals/sound
        SilenceAndClearAllHeld();
    }

    bool IsWhite(int pc) {
        for (int i=0;i<whitePCs.Length;i++) if (whitePCs[i]==pc) return true;
        return false;
    }

    static int PitchClass(int midi) => midi % 12;

    void BuildKeyboard()
    {
        // cleanup old
        for (int i = keysParent.childCount-1; i >= 0; i--) Destroy(keysParent.GetChild(i).gameObject);
        _keyByNote.Clear();

        // First pass: create all whites, track X positions per octave step
        var whiteX = new Dictionary<int,float>(); // midi note -> x pos for its white
        float x = 0f;

        for (int n = startNote; n <= endNote; n++)
        {
            if (!IsWhite(PitchClass(n))) continue;

            var key = Instantiate(whiteKeyPrefab, keysParent);
            key.midiNote = n;

            var rt = (RectTransform)key.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0,0);
            rt.pivot = new Vector2(0,0);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = new Vector2(whiteWidth, whiteHeight);

            WireKey(key);
            _keyByNote[n] = key;
            whiteX[n] = x;

            x += whiteWidth + whiteSpacing;
        }

        // Compute black offset within one octave (relative to the preceding white)
        // White order in octave: C D E F G A B  (PC:0,2,4,5,7,9,11)
        // Black positions sit roughly over the gap; tweak with offset
        float blackW = whiteWidth * blackWidthRatio;
        float blackH = whiteHeight * blackHeightRatio;
        float blackOffset = whiteWidth * blackHorizontalOffset;

        for (int n = startNote; n <= endNote; n++)
        {
            int pc = PitchClass(n);
            if (IsWhite(pc)) continue;

            int leftWhite = n - 1;
            while (leftWhite >= startNote && !IsWhite(PitchClass(leftWhite))) leftWhite--;
            if (!_keyByNote.ContainsKey(leftWhite)) continue;

            float baseX = whiteX[leftWhite];

            var key = Instantiate(blackKeyPrefab, keysParent);
            key.midiNote = n;

            var rt = (RectTransform)key.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0,0);
            rt.pivot = new Vector2(0,0);
            rt.anchoredPosition = new Vector2(baseX + blackOffset - blackW * 0.5f, whiteHeight - blackH);
            rt.sizeDelta = new Vector2(blackW, blackH);

            key.transform.SetAsLastSibling();
            WireKey(key);
            _keyByNote[n] = key;
        }

        // Optionally stretch parent to fit
        var totalWidth = x;
        var parentRT = (RectTransform)keysParent;
        var size = parentRT.sizeDelta;
        parentRT.sizeDelta = new Vector2(totalWidth, Mathf.Max(size.y, whiteHeight));
    }

    void WireKey(PianoKeyUI key)
    {
        key.NoteOn += (note, vel) =>
        {
            // Block UI key presses while locked (UI should already be raycast-blocked, this is belt & suspenders)
            if (inputLocked) return;
            heldNotes.Add(note); 
            if (routeToFmod && fmod) fmod.NoteOn(note, vel);
            else if (synth) synth.NoteOn(note, vel);

            if (dictation) dictation.OnMidiNoteOn(note, vel);
        };
        key.NoteOff += (note) =>
        {
            if (inputLocked) return;
            heldNotes.Remove(note); 
            if (routeToFmod && fmod) fmod.NoteOff(note);
            else if (synth) synth.NoteOff(note);

            if (dictation) dictation.OnMidiNoteOff(note);
        };
    }

    

    // Optional external API if you want to mirror playback on the keyboard:
    public void Highlight(int midiNote, bool on)
    {
        if (_keyByNote.TryGetValue(midiNote, out var key))
            key.Highlight(on);
    }

    // Called externally when MIDI input is received (HARDWARE)
    public void OnMidiNoteOnExternal(int midiNote, float velocity)
    {
        if (inputLocked) return; 
        heldNotes.Add(midiNote);                
        if (_keyByNote.TryGetValue(midiNote, out var key)) key.Highlight(true);
    }

    public void OnMidiNoteOffExternal(int midiNote)
    {
        if (inputLocked) return; 
        heldNotes.Remove(midiNote);                 
        if (_keyByNote.TryGetValue(midiNote, out var key)) key.Highlight(false);
    }

    public void LockInput(bool locked, float? fadeToAlpha = null, float fadeTime = 0.12f)
    {
        if (locked)
        {
            // First: release any currently held notes to avoid stuck highlights/sound
            SilenceAndClearAllHeld();                 // flush before flipping the gate
        }
        
        inputLocked = locked;
        float target = locked ? (fadeToAlpha ?? lockedAlpha) : 1f;
        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeTo(target, fadeTime));

        if (canvasGroup)
        {
            // These gate pointer clicks to keys (UI)
            canvasGroup.interactable = !locked;
            canvasGroup.blocksRaycasts = !locked;
        }
    }

    void ApplyLockVisual(bool locked, bool immediate = false)
    {
        if (!canvasGroup) return;
        float target = locked ? lockedAlpha : 1f;
        if (immediate) { canvasGroup.alpha = target; return; }
        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeTo(target, 0.12f));
    }

    IEnumerator FadeTo(float target, float time)
    {
        if (!canvasGroup) yield break;
        float start = canvasGroup.alpha;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, time);
            canvasGroup.alpha = Mathf.Lerp(start, target, t);
            yield return null;
        }
        canvasGroup.alpha = target;
    }

    void SilenceAndClearAllHeld()
    {
        if (heldNotes.Count == 0) return;

        // Snapshot first, then clear, to avoid "Collection was modified" during iteration
        var snapshot = new int[heldNotes.Count];
        heldNotes.CopyTo(snapshot);
        heldNotes.Clear();

        for (int i = 0; i < snapshot.Length; i++)
        {
            int note = snapshot[i];

            // Send NoteOffs to audio + game logic
            if (routeToFmod && fmod) fmod.NoteOff(note);
            else if (synth) synth.NoteOff(note);

            if (dictation) dictation.OnMidiNoteOff(note);

            // Clear highlight safely
            if (_keyByNote != null && _keyByNote.TryGetValue(note, out var key))
                key.Highlight(false);
        }
    }
    
    public void HideImmediate(bool deactivateGO = true)
    {
        // Flush any sounding/held notes, block all input, make fully transparent
        SilenceAndClearAllHeld();
        inputLocked = true;

        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (deactivateGO) gameObject.SetActive(false); // optional: disable the whole keyboard object
    }

    public void ShowImmediate()
    {
        // Re-enable the object if we deactivated it
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        inputLocked = false;

        if (canvasGroup)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }
    
}
