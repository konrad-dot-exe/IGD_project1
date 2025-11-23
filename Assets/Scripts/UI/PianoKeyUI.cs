using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class PianoKeyUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Range(0,127)] public int midiNote = 60;
    [Range(0f,1f)] public float fixedVelocity = 0.85f;
    public Color upColor = new(1f,1f,1f,1f);
    public Color downColor = new(0.85f,0.85f,0.85f,1f);

    public event Action<int,float> NoteOn;
    public event Action<int> NoteOff;

    Image _img;
    bool _isDown;
    float _baseOpacity = 1.0f; // Track the opacity set by SetOpacity

    void Awake() { _img = GetComponent<Image>(); _img.color = upColor; }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_isDown) return;
        _isDown = true;
        var c = downColor;
        c.a = _baseOpacity; // Preserve base opacity
        _img.color = c;
        NoteOn?.Invoke(midiNote, fixedVelocity);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();
    public void OnPointerExit(PointerEventData eventData) { if (_isDown) Release(); }

    void Release()
    {
        _isDown = false;
        if (_img == null) return;
        var c = upColor;
        c.a = _baseOpacity; // Preserve base opacity
        _img.color = c;
        NoteOff?.Invoke(midiNote);
    }

    // For external highlighting (e.g., while the melody plays)
    public void Highlight(bool on)
    {
        if (_img == null) return;
        var c = on ? downColor : upColor;
        c.a = _baseOpacity; // Preserve base opacity
        _img.color = c;
    }

    // Set opacity (alpha) while preserving RGB color values
    public void SetOpacity(float alpha)
    {
        if (_img == null) return;
        _baseOpacity = Mathf.Clamp01(alpha); // Store the base opacity
        var c = _img.color;
        c.a = _baseOpacity;
        _img.color = c;
    }

}
