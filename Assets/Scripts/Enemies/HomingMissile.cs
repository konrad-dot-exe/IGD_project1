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
        [SerializeField] GameObject explodeVFX;
        [SerializeField] AudioClip explodeSFX;
        [SerializeField] float sfxVolume = 0.9f;
        [SerializeField] float vfxYOffset = 0f;
        [SerializeField] float vfxEmissionBoost = 6f; // HDR boost for bloom

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

            // 🔊 Play the missile fire SFX at the spawn position
            SfxPalette.I?.OnMissileFire(transform.position);
        }

        void Update()
        {
            if (exploded) return;

            if (target != null)
            {
                Vector3 to = (target.transform.position - transform.position).normalized;
                transform.forward = Vector3.RotateTowards(
                    transform.forward, to, Mathf.Deg2Rad * turnRateDeg * Time.deltaTime, 1f);
            }
            else if (Time.time >= dieAt)
            {
                Explode(null);
                return;
            }

            transform.position += transform.forward * speed * Time.deltaTime;

            if (target != null &&
                (target.transform.position - transform.position).sqrMagnitude <= explodeRadius * explodeRadius)
            {
                Explode(target);
            }
        }

        public void Explode(EnemyShip hit)
        {
            if (exploded) return;
            exploded = true;

            Vector3 fxPos = (hit != null ? hit.transform.position : transform.position) + Vector3.up * vfxYOffset;

            if (explodeVFX)
            {
                var vfx = Instantiate(explodeVFX, fxPos, Quaternion.identity);
                var baseColor = hit ? hit.CurrentColor : Color.white;
                TintExplosion(vfx, baseColor, vfxEmissionBoost);
            }

            // NEW: enemy explode SFX
            SfxPalette.I?.OnEnemyExplode(fxPos);


            if (explodeSFX) AudioSource.PlayClipAtPoint(explodeSFX, fxPos, sfxVolume);

            if (hit != null) hit.Die();

            Destroy(gameObject);
        }

        // Force-tint the instance: startColor, neutralize Color-over-Lifetime, and set material colors.
        static void TintExplosion(GameObject root, Color c, float emissionIntensity)
        {
            // 1) Particle vertex color
            var psList = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(c);

                // If Color over Lifetime is enabled and not white, override it to white so it doesn't fight us
                var col = ps.colorOverLifetime;
                if (col.enabled)
                {
                    var g = new Gradient();
                    g.SetKeys(
                        new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) } // keep fade if you want
                    );
                    col.color = new ParticleSystem.MinMaxGradient(g);
                }
            }

            // 2) Per-instance materials (renderer.material creates a unique instance safely for short-lived VFX)
            Color emission = c * emissionIntensity;

            var psRenderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var r in psRenderers)
            {
                var mat = r.material; // instance
                SetCommonColorProps(mat, c, emission);
            }

            var meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in meshRenderers)
            {
                var mats = r.materials; // instances
                for (int i = 0; i < mats.Length; i++)
                    SetCommonColorProps(mats[i], c, emission);
            }
        }

        // Handles common property names across URP particle/material variants
        static void SetCommonColorProps(Material m, Color baseCol, Color emissionCol)
        {
            if (!m) return;

            // Base color
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
            if (m.HasProperty("_Color"))     m.SetColor("_Color",     baseCol);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", baseCol);

            // Emission
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emissionCol);
            m.EnableKeyword("_EMISSION");
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, explodeRadius);
        }
    }
}
