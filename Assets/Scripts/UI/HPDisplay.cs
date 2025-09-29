// Assets/Scripts/UI/HPDisplay.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EarFPS
{
    public class HPDisplay : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] Vector2 iconSize = new Vector2(18, 18);
        [SerializeField] float spacing = 6f;
        [SerializeField] Sprite squareSprite;

        [Header("Colors")]
        [SerializeField] Color fullColor  = new Color(0.2f, 0.9f, 1f, 1f);
        [SerializeField] Color emptyColor = new Color(0.2f, 0.9f, 1f, 0.18f);

        readonly List<Image> _icons = new List<Image>();

        public void Build(int max)
        {
            foreach (Transform c in transform) Destroy(c.gameObject);
            _icons.Clear();

            float x = 0f;
            for (int i = 0; i < max; i++)
            {
                var go = new GameObject($"HP_{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);    // top-left
                rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = iconSize;
                rt.anchoredPosition = new Vector2(x, 0f);

                var img = go.GetComponent<Image>();
                img.sprite = squareSprite;
                img.type = Image.Type.Simple;
                _icons.Add(img);

                x += iconSize.x + spacing;
            }
        }

        public void Set(int current)
        {
            for (int i = 0; i < _icons.Count; i++)
                _icons[i].color = (i < current) ? fullColor : emptyColor;
        }
    }
}

