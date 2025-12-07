using UnityEngine;
using UnityEngine.UI;

public class FilmGrainUI : MonoBehaviour
{
    [SerializeField] private Image grainImage;

    public void Show(float alpha = 0.1f)
    {
        SetAlpha(alpha);
        grainImage.enabled = true;
    }

    public void Hide()
    {
        grainImage.enabled = false;
    }

    private void SetAlpha(float a)
    {
        if (grainImage == null) return;
        var c = grainImage.color;
        c.a = a;
        grainImage.color = c;
    }
}
