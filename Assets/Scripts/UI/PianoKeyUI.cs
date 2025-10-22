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

    void Awake() { _img = GetComponent<Image>(); _img.color = upColor; }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_isDown) return;
        _isDown = true;
        _img.color = downColor;
        NoteOn?.Invoke(midiNote, fixedVelocity);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();
    public void OnPointerExit(PointerEventData eventData) { if (_isDown) Release(); }

    void Release()
    {
        _isDown = false;
        _img.color = upColor;
        NoteOff?.Invoke(midiNote);
    }

    // For external highlighting (e.g., while the melody plays)
    public void Highlight(bool on)
    {
        _img.color = on ? downColor : upColor;
    }
}
