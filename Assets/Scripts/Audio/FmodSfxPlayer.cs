using UnityEngine;
using FMODUnity;

[DisallowMultipleComponent]
public class FmodSfxPlayer : MonoBehaviour
{
    [Header("FMOD Events")]
    public EventReference uiWin;
    public EventReference uiWrong;
    public EventReference stingerGameOver;
    public EventReference uiCardsSweep;
    public EventReference lightning;
    public EventReference thunder;

    public void PlayWin()        => Play(uiWin);
    public void PlayWrong()      => Play(uiWrong);
    public void PlayGameOver()   => Play(stingerGameOver);
    public void PlayCardsSweep() => Play(uiCardsSweep);
    public void PlayLightning() => Play(lightning);
    public void PlayThunder() => Play(thunder);

    void Play(EventReference ev)
    {
        if (ev.IsNull) return;                       // safe no-op if unassigned
        RuntimeManager.PlayOneShot(ev, transform.position);
    }
}
