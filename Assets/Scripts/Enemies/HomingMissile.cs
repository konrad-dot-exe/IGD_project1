using UnityEngine;

namespace EarFPS
{
    public class HomingMissile : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] float speed = 30f;
        [SerializeField] float turnRateDeg = 360f;
        [SerializeField] float maxLifetime = 10f;

        [Header("Explosion")]
        [SerializeField] float explodeRadius = 1.5f;
        [SerializeField] GameObject explodeVFX;     // assign your ExplosionVFX prefab
        [SerializeField] AudioClip explodeSFX;      // optional
        [SerializeField] float sfxVolume = 0.9f;
        [SerializeField] float vfxYOffset = 0f;     // lift VFX slightly if it clips ground

        EnemyShip target;
        float dieAt;
        bool exploded;

        public void Init(EnemyShip t)
        {
            target = t;
            dieAt = Time.time + maxLifetime;

            if (target != null)
            {
                Vector3 dir = (target.transform.position - transform.position);
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        void Update()
        {
            if (exploded) return;

            // steer toward target if it still exists
            if (target != null)
            {
                Vector3 to = (target.transform.position - transform.position).normalized;
                transform.forward = Vector3.RotateTowards(
                    transform.forward, to, Mathf.Deg2Rad * turnRateDeg * Time.deltaTime, 1f);
            }
            else if (Time.time >= dieAt)
            {
                // no target for too long -> self-destruct
                Explode(null);
                return;
            }

            // advance
            transform.position += transform.forward * speed * Time.deltaTime;

            // proximity detonation
            if (target != null &&
                (target.transform.position - transform.position).sqrMagnitude <= explodeRadius * explodeRadius)
            {
                Explode(target);
            }
        }

        /// <summary>Detonate once; spawns VFX/SFX and kills the hit ship if provided.</summary>
        public void Explode(EnemyShip hit)
        {
            if (exploded) return;
            exploded = true;

            Vector3 fxPos = (hit != null ? hit.transform.position : transform.position) + Vector3.up * vfxYOffset;

            if (explodeVFX) Instantiate(explodeVFX, fxPos, Quaternion.identity);
            if (explodeSFX) AudioSource.PlayClipAtPoint(explodeSFX, fxPos, sfxVolume);

            if (hit != null)
                hit.Die();

            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, explodeRadius);
        }
    }
}
