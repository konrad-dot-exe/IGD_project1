using UnityEngine;
using System.Collections.Generic;

namespace EarFPS
{
    public class EnemyShip : MonoBehaviour
    {
        [SerializeField] float speed = 6f;
        [SerializeField] float bombRadius = 6f;
        [SerializeField] Renderer[] emissiveRenderers;
        [SerializeField] bool autoFindRenderers = true;

        // --- Color palette for enemies ---
        [Header("Color / Emission")]
        [SerializeField]
        Color[] palette = new Color[]
        {
            new Color(0.00f, 1.00f, 1.00f), // cyan
            new Color(0.45f, 0.20f, 1.00f), // bluish-purple
            new Color(1.00f, 0.00f, 0.70f), // magenta
            new Color(1.00f, 0.50f, 0.00f), // orange
            new Color(1.00f, 0.95f, 0.20f), // yellow
        };
        [SerializeField] float emissionIntensity = 4.5f; // matches your material’s HDR "Intensity"
        MaterialPropertyBlock _mpb;

        // Assigned by spawner:
        [HideInInspector] public int rootMidi;         // e.g., C3..C5
        [HideInInspector] public IntervalDef interval; // which interval player must identify

        static readonly List<EnemyShip> All = new List<EnemyShip>();

        Color currentColor = Color.cyan;    // stored palette color (not multiplied by intensity)
        public Color CurrentColor => currentColor;

        bool _dead;

        void Awake()
        {
            if (autoFindRenderers || emissiveRenderers == null || emissiveRenderers.Length == 0)
                emissiveRenderers = GetComponentsInChildren<Renderer>(true);
        }

        void OnEnable()
        {
            if (All != null) All.Add(this);
        }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            ApplyRandomColor();
        }

        Transform target;                           // who we chase

        void Update()
        {
            // steer toward turret (fallback to origin if missing)
            Vector3 goal = target ? target.position : Vector3.zero;
            Vector3 to = goal - transform.position;

            if (to.sqrMagnitude > 0.0001f)
            {
                Vector3 dir = to.normalized;
                transform.position += dir * speed * Time.deltaTime;      // your existing speed
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }

            // bomb when close
            if (to.sqrMagnitude <= bombRadius * bombRadius)
            {
                IntervalExerciseController.Instance.PlayHitFeedback(); // strobe + shake every time

                IntervalExerciseController.Instance.PlayerHit(1);   // -1 HP
                SfxPalette.I?.OnPlayerBombed(transform.position);

                //IntervalExerciseController.Instance.GameOver();        // your lose behavior
                IntervalExerciseController.Instance.OnEnemyDestroyed(this);
                Destroy(gameObject);
            }
        }

        public void Die()
        {
            if (_dead) return;  // <— single-fire guard
            _dead = true;
            
            // Spawn the floating "Perfect Fifth!" at the enemy, slightly above it.
            UIHud.Instance?.ToastCorrect(interval.displayName, transform.position + Vector3.up * 1.2f);

            IntervalExerciseController.Instance.OnEnemyDestroyed(this);
            Destroy(gameObject);
        }

        // Soft-lock helper
        public static EnemyShip FindNearestInCone(Vector3 origin, Vector3 forward, float maxAngle, float maxDist)
        {
            EnemyShip best = null;
            float bestDot = Mathf.Cos(maxAngle * Mathf.Deg2Rad);
            float bestDist = Mathf.Infinity;

            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                Vector3 to = e.transform.position - origin;
                float dist = to.magnitude;
                if (dist > maxDist) continue;
                Vector3 dir = to / dist;
                float dot = Vector3.Dot(forward, dir);
                if (dot >= bestDot)
                {
                    // prefer nearest within cone
                    if (dist < bestDist) { best = e; bestDist = dist; }
                }
            }
            return best;
        }

        public void SetTarget(Transform t)          // called by IntervalExerciseController after spawn
        {
            target = t;
        }
        
        public void ApplyRandomColor()
        {
            if (palette == null || palette.Length == 0) return;
            var c = palette[Random.Range(0, palette.Length)];
            ApplyColor(c);
        }

        public void ApplyColor(Color c)
        {
            currentColor = c;                               // <— remember it

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            Color emission = c * emissionIntensity;

            foreach (var r in emissiveRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", c);
                _mpb.SetColor("_EmissionColor", emission);
                r.SetPropertyBlock(_mpb);

                // safety: make sure emission is enabled
                foreach (var m in r.sharedMaterials) m.EnableKeyword("_EMISSION");
            }
        }

    }
}
