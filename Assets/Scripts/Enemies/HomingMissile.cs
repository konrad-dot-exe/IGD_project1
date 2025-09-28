using UnityEngine;

namespace EarFPS
{
    public class HomingMissile : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] float speed = 30f;
        [SerializeField] float turnRateDeg = 360f;
        [SerializeField] float maxLifetime = 10f;

        [Header("Normal Kill Explosion")]
        [SerializeField] float explodeRadius = 1.5f;
        [SerializeField] GameObject explodeVFX;
        [SerializeField] AudioClip explodeSFX;
        [SerializeField] float explodeSfxVolume = 0.9f;

        [Header("Dud / Shield Hit")]
        [SerializeField] float dudTriggerRadius = 2.2f;
        [SerializeField] GameObject shieldHitVFX;
        [SerializeField] AudioClip dudFizzleSFX;
        [SerializeField] float dudSfxVolume = 0.9f;
        // Keep your existing fizzle (your current "MissileFizzle" prefab)
        [SerializeField] GameObject dudFizzleVFX;
        [SerializeField] GameObject shieldBubbleVFX; // NEW: the bubble prefab (should have ShieldBubble component from earlier)
        [SerializeField] float shieldBubbleRadiusScale = 1.0f;   // 1.0 = dudTriggerRadius

        [Header("VFX Common")]
        [SerializeField] float vfxYOffset = 0f;
        [SerializeField] float vfxEmissionBoost = 6f;

        [Header("Missile Tint")]
        [SerializeField] public Color defaultMissileTint = new Color(0.0f, 0.1f, 1f);
        [SerializeField] TrailRenderer[] trails;    // optional
        [SerializeField] Light[] lights;            // optional

        // --- Missile Tint / Lights ---
        [SerializeField] Color missileTint = Color.white;
        [SerializeField] Light[] emissiveLights;     // assign in prefab or auto-find
        [SerializeField] bool autoFindLights = true;
        [SerializeField, Min(0f)] float lightIntensity = 1000f;   // how bright the glow light should be



        EnemyShip target;
        float dieAt;
        bool exploded;
        bool isDud;

        // Robustness
        Vector3 lastKnownTargetPos;
        bool hadTarget;              // we ever had a valid target


        void Awake()
        {
            if ((emissiveLights == null || emissiveLights.Length == 0) && autoFindLights)
                emissiveLights = GetComponentsInChildren<Light>(true);
        }

        public void Init(EnemyShip t, bool dud = false, Color? tint = null)
        {
            target = t;
            isDud = dud;
            missileTint = tint ?? defaultMissileTint;

            dieAt = Time.time + maxLifetime;

            ApplyMissileTint(missileTint); // pushes to lights + visuals

            if (target != null)
            {
                hadTarget = true;
                lastKnownTargetPos = target.transform.position;
                Vector3 dir = (lastKnownTargetPos - transform.position);
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        void Update()
        {
            if (exploded) return;

            // If target exists, keep last known pos fresh
            if (target != null)
            {
                lastKnownTargetPos = target.transform.position;

                // PRE-MOVE proximity check (handles spawn-inside or frame edge cases)
                float preDist = Vector3.Distance(transform.position, lastKnownTargetPos);
                if (!isDud && preDist <= explodeRadius) { DoKill(target); return; }
                if (isDud && preDist <= dudTriggerRadius) { DoDudFizzle(target); return; }

                // steer
                Vector3 toDir = (lastKnownTargetPos - transform.position).normalized;
                transform.forward = Vector3.RotateTowards(
                    transform.forward, toDir, Mathf.Deg2Rad * turnRateDeg * Time.deltaTime, 1f);

                // advance (optionally clamp to not step past target)
                float step = speed * Time.deltaTime;
                float clamp = Mathf.Min(step, preDist); // avoids huge overshoot on very close targets
                transform.position += transform.forward * clamp;

                // POST-MOVE proximity check
                float postDist = Vector3.Distance(transform.position, lastKnownTargetPos);
                if (!isDud && postDist <= explodeRadius) { DoKill(target); return; }
                if (isDud && postDist <= dudTriggerRadius) { DoDudFizzle(target); return; }
            }
            else
            {
                // Target vanished: show a graceful visual and end immediately
                if (hadTarget)
                {
                    if (isDud) DoDudFizzle(null);            // fizzle at current/last area
                    else DoKillVisualOnly();           // explode visually even if enemy is gone
                    return;
                }

                // We never had a target; self-destruct after lifetime
                if (Time.time >= dieAt)
                {
                    DoDudFizzle(null); // quiet fizzle
                    return;
                }

                // Drift forward so it doesn’t feel frozen
                transform.position += transform.forward * (speed * Time.deltaTime);
            }
        }

        // ---------------- effects ----------------

        void DoKill(EnemyShip hit)
        {
            if (exploded) return;
            exploded = true;

            Vector3 fxPos = hit.transform.position + Vector3.up * vfxYOffset;

            if (explodeVFX)
            {
                var vfx = Instantiate(explodeVFX, fxPos, Quaternion.identity);
                var baseColor = hit ? hit.CurrentColor : Color.white; // kill uses enemy color
                TintExplosionLikeEnemy(vfx, baseColor, vfxEmissionBoost);
            }
            if (explodeSFX) AudioSource.PlayClipAtPoint(explodeSFX, fxPos, explodeSfxVolume);

            hit.Die();
            Destroy(gameObject);
        }

        // Visual-only kill when target vanished (don’t call Die)
        void DoKillVisualOnly()
        {
            if (exploded) return;
            exploded = true;

            Vector3 fxPos = lastKnownTargetPos + Vector3.up * vfxYOffset;

            if (explodeVFX)
            {
                var vfx = Instantiate(explodeVFX, fxPos, Quaternion.identity);
                // enemy is gone → use missile tint so it still looks consistent
                TintExplosionLikeEnemy(vfx, missileTint, vfxEmissionBoost);
            }

            // if (explodeSFX) AudioSource.PlayClipAtPoint(explodeSFX, fxPos, explodeSfxVolume);
            if (explodeSFX)
            {
                var go = new GameObject("ExplodeSFX_3D");
                go.transform.position = fxPos;
                var src = go.AddComponent<AudioSource>();
                src.clip = explodeSFX;
                src.volume = explodeSfxVolume;
                src.spatialBlend = 1f;                 // 3D
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 18f;                 // make it audible from turret
                src.maxDistance = 120f;
                src.Play();
                Destroy(go, src.clip.length + 0.2f);
            }

            Destroy(gameObject);
        }

        void DoDudFizzle(EnemyShip hit)
        {
            if (exploded) return;
            exploded = true;

            Vector3 fxPos = (hit ? hit.transform.position : transform.position) + Vector3.up * vfxYOffset;

            // --- Shield bubble pop (tinted to the ENEMY) ---
            if (shieldBubbleVFX)
            {
                var bubbleGo = Instantiate(shieldBubbleVFX, fxPos, Quaternion.identity);
                var bubble = bubbleGo.GetComponent<ShieldBubble>();
                float radius = dudTriggerRadius * Mathf.Max(0.01f, shieldBubbleRadiusScale);

                // use enemy tint if available; fallback to missile tint
                Color bubbleTint = hit ? hit.CurrentColor : missileTint;
                bubble?.Init(bubbleTint, radius);
            }

            // --- Fizzle (still tinted like the missile) ---
            if (dudFizzleVFX)
            {
                var vfx = Instantiate(dudFizzleVFX, fxPos, Quaternion.identity);
                TintExplosionLikeEnemy(vfx, missileTint, vfxEmissionBoost);
            }

            // --- SFX (unchanged) ---
            if (dudFizzleSFX)
            {
                var go = new GameObject("DudSFX_3D");
                go.transform.position = fxPos;
                var src = go.AddComponent<AudioSource>();
                src.clip = dudFizzleSFX;
                src.volume = dudSfxVolume;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 18f;
                src.maxDistance = 120f;
                src.Play();
                Destroy(go, src.clip.length + 0.2f);
            }

            Destroy(gameObject);
        }

        // tint helpers
        void ApplyMissileTint(Color c)
        {
            if (trails != null)
                foreach (var tr in trails)
                    if (tr) { tr.startColor = c; tr.endColor = c; }

            if (lights != null)
                foreach (var l in lights)
                    if (l) l.color = c;

            if (emissiveLights != null)
            {
                for (int i = 0; i < emissiveLights.Length; i++)
                {
                    var L = emissiveLights[i];
                    if (!L) continue;
                    L.color = c;              // no HDR multiply here
                    L.intensity = lightIntensity;
                }
            }
        }

        static void TintExplosionLikeEnemy(GameObject root, Color c, float emissionIntensity)
        {
            var psList = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(c);

                var col = ps.colorOverLifetime;
                if (col.enabled)
                {
                    var g = new Gradient();
                    g.SetKeys(
                        new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
                    );
                    col.color = new ParticleSystem.MinMaxGradient(g);
                }
            }

            Color emission = c * emissionIntensity;

            var psRenderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var r in psRenderers)
            {
                var mat = r.material;
                SetCommonColorProps(mat, c, emission);
            }

            var meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in meshRenderers)
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                    SetCommonColorProps(mats[i], c, emission);
            }
        }

        static void SetCommonColorProps(Material m, Color baseCol, Color emissionCol)
        {
            if (!m) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
            if (m.HasProperty("_Color")) m.SetColor("_Color", baseCol);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", baseCol);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emissionCol);
            m.EnableKeyword("_EMISSION");
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, explodeRadius);

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, dudTriggerRadius);
        }
        
        public void SetTint(Color c)
        {
            // store if you use it elsewhere
            defaultMissileTint = c;

            if (emissiveLights != null)
            {
                for (int i = 0; i < emissiveLights.Length; i++)
                {
                    var L = emissiveLights[i];
                    if (!L) continue;
                    L.useColorTemperature = false;   // avoid Kelvin overriding color
                    L.color = c;                     // DO NOT multiply by emission boost here
                    L.intensity = lightIntensity;    // brightness is via intensity
                }
            }

            // If you also tint trails/particles/materials, do that here (those can use HDR * c)
        }
    }
}
