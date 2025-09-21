using UnityEngine;

namespace EarFPS
{
    public class SfxPalette : MonoBehaviour
    {
        public static SfxPalette I { get; private set; }

        [Header("Clips")]
        [SerializeField] AudioClip missileFire;
        [SerializeField] AudioClip enemyExplode;
        [SerializeField] AudioClip wrongGuess;
        [SerializeField] AudioClip playerBombed;

        [Header("Defaults")]
        [SerializeField] float volume = 0.9f;
        [SerializeField] float pitchJitter = 0.05f;
        [SerializeField] bool force2D = false;

        // --- IMPORTANT when Domain Reload is disabled ---
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticsOnPlay() { I = null; }

        void Awake()
        {
            // Replace any stale instance (safe in editor when Scene Reload is off)
            I = this;
            // Optional: keep across scene loads
            // DontDestroyOnLoad(gameObject);
        }

        void OnDestroy() { if (I == this) I = null; }

        // Public API
        public void OnMissileFire(Vector3 pos)  => At(missileFire, pos, volume);
        public void OnEnemyExplode(Vector3 pos) => At(enemyExplode, pos, volume);
        public void OnWrongGuess()
        {
            if (!wrongGuess)
            {
                Debug.LogWarning("[SFX] WrongGuess clip is NULL.");
                return;
            }
            Debug.Log("[SFX] WrongGuess");
            UI(wrongGuess, volume);
        }
        public void OnPlayerBombed(Vector3 pos)  => At(playerBombed, pos, volume);

        // 2D
        public void UI(AudioClip clip, float vol = 1f)
        {
            if (!clip) return;
            var go = new GameObject("SFX_UI");
            var a = go.AddComponent<AudioSource>();
            a.spatialBlend = 0f;
            a.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            a.PlayOneShot(clip, vol);
            Destroy(go, clip.length + 0.1f);
        }

        // 3D
        public void At(AudioClip clip, Vector3 pos, float vol = 1f)
        {
            if (!clip) return;
            if (force2D) { UI(clip, vol); return; }

            var go = new GameObject("SFX_3D");
            go.transform.position = pos;
            var a = go.AddComponent<AudioSource>();
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = 10f;
            a.maxDistance = 1000f;
            a.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            a.PlayOneShot(clip, vol);
            Destroy(go, clip.length + 0.1f);
        }
    }
}
