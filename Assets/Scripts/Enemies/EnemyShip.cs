using UnityEngine;
using System.Collections.Generic;

namespace EarFPS
{
    public class EnemyShip : MonoBehaviour
    {
        [SerializeField] float speed = 6f;
        [SerializeField] float bombRadius = 6f;
        [SerializeField] Renderer[] emissiveRenderers;

        // Assigned by spawner:
        [HideInInspector] public int rootMidi;         // e.g., C3..C5
        [HideInInspector] public IntervalDef interval; // which interval player must identify

        static readonly List<EnemyShip> All = new List<EnemyShip>();
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        Transform target;                           // who we chase

        void Update()
        {
            // steer toward turret (fallback to origin if missing)
            Vector3 goal = target ? target.position : Vector3.zero;
            Vector3 to   = goal - transform.position;

            if (to.sqrMagnitude > 0.0001f)
            {
                Vector3 dir = to.normalized;
                transform.position += dir * speed * Time.deltaTime;      // your existing speed
                transform.rotation  = Quaternion.LookRotation(dir, Vector3.up);
            }

            // bomb when close
            if (to.sqrMagnitude <= bombRadius * bombRadius)
            {
                GameManager.Instance.PlayHitFeedback(); // strobe + shake every time
                GameManager.Instance.GameOver();        // your lose behavior
                Destroy(gameObject);
            }
        }


        public void Die()
        {
            // Spawn the floating "Perfect Fifth!" at the enemy, slightly above it.
            UIHud.Instance?.ToastCorrect(interval.displayName, transform.position + Vector3.up * 1.2f);

            GameManager.Instance.OnEnemyDestroyed(this);
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

        public void SetTarget(Transform t)          // called by GameManager after spawn
        {
            target = t;
        }
    }
}
