using UnityEngine;
using TMPro;

namespace EarFPS
{
    public class IntervalCarouselUI : MonoBehaviour
    {
        [SerializeField] IntervalQuizController quiz;
        [SerializeField] CanvasGroup group;
        [SerializeField] TextMeshProUGUI[] items; // 5 items, top->bottom
        [SerializeField] float stepHeight = 28f;
        [SerializeField] float ease = 12f;        // 6–10 = smooth, 12 = snappy
        [SerializeField] float fadeSpeed = 10f;

        [Header("Edge Fade / Scale")]
        [SerializeField] float alphaCenter = 1.0f;
        [SerializeField] float alphaNear   = 0.5f;
        [SerializeField] float alphaFar    = 0.15f;
        [SerializeField] Vector3 scaleCenter = Vector3.one * 1.00f;
        [SerializeField] Vector3 scaleNear   = Vector3.one * 0.75f;
        [SerializeField] Vector3 scaleFar    = Vector3.one * 0.50f;

        // Continuous indices for smooth motion across wrap
        float displayContinuous;   // what we render (eases)
        float targetContinuous;    // where we want to be
        float runningSel;          // selected index as a continuous track (increments/decrements by ±1)
        int   lastSel = -1;

        void Start()
        {
            if (!group) group = GetComponent<CanvasGroup>();
            if (quiz)
            {
                lastSel = quiz.SelectedIndex;
                runningSel = lastSel;
                displayContinuous = targetContinuous = runningSel;
            }
        }

        void Update()
        {
            if (!quiz || items == null || items.Length != 5) return;

            // Show only while listening
            float a = quiz.IsListening ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, a, fadeSpeed * Time.deltaTime);

            int count = IntervalTable.Count;
            int sel = quiz.SelectedIndex;

            // When selection changes (by ±1), advance our continuous trackers with wrap awareness
            if (lastSel < 0) { lastSel = sel; runningSel = sel; targetContinuous = displayContinuous = runningSel; }
            if (sel != lastSel)
            {
                int forward  = (sel - lastSel + count) % count;      // steps forward
                int backward = (lastSel - sel + count) % count;      // steps backward
                int step = (forward == 1 && backward == count - 1) ? +1 :
                           (backward == 1 && forward == count - 1) ? -1 :
                           (forward <= backward) ? forward : -backward;

                runningSel      += step;
                targetContinuous += step;
                lastSel = sel;
            }

            // Ease the visual position toward the target (no wrap jump)
            displayContinuous = Mathf.Lerp(displayContinuous, targetContinuous, 1f - Mathf.Exp(-ease * Time.deltaTime));

            // Slide relative to the selected index so the CENTER ITEM == SELECTED, always
            float slide = displayContinuous - runningSel; // typically in [-1..+1] during a step

            // Fill the 5 rows around the actual selected index
            for (int i = 0; i < 5; i++)
            {
                int offset = i - 2; // -2,-1,0,+1,+2 (center row is i=2)
                int tableIndex = Mod(sel - offset, count);   // higher above, lower below
                var def = IntervalTable.ByIndex(tableIndex);

                var tmp = items[i];
                tmp.text = def.displayName; // long names like "Perfect Fifth"

                // Vertical position: slide items smoothly by 'slide'
                float y = -(offset - slide) * stepHeight;
                tmp.rectTransform.anchoredPosition = new Vector2(0f, y);

                // Alpha + scale by distance from the CENTER ROW (i==2)
                int dist = Mathf.Abs(offset);
                float alpha = (dist == 0) ? alphaCenter : (dist == 1 ? alphaNear : alphaFar);
                var c = tmp.color; c.a = alpha; tmp.color = c;
                tmp.rectTransform.localScale = (dist == 0) ? scaleCenter : (dist == 1 ? scaleNear : scaleFar);
            }
        }

        static int Mod(int x, int m) { int r = x % m; return r < 0 ? r + m : r; }
    }
}
