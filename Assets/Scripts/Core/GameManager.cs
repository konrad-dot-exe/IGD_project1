using UnityEngine;
using System.Collections;

namespace EarFPS
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        void Awake() { Instance = this; }

        [Header("Spawn")]
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] Transform spawnRing; // center at (0,0,0)
        [SerializeField] float ringRadius = 80f;

        [Header("Waves")]
        [SerializeField] int waves = 3;
        [SerializeField] int enemiesPerWave = 6;
        [SerializeField] float spawnIntervalStart = 6f;
        [SerializeField] float spawnIntervalEnd = 3.5f;
        [SerializeField] float enemySpeedStart = 6f;
        [SerializeField] float enemySpeedEnd = 9f;

        [Header("Roots")]
        [SerializeField] int rootMidiMin = 48; // C3 ≈ 48
        [SerializeField] int rootMidiMax = 72; // C5 ≈ 72

        [Header("Scoring")]
        [SerializeField] int baseScore = 100;
        [SerializeField] int wrongPenalty = 50;
        int score = 0, correct = 0, attempts = 0, bestStreak = 0, streak = 0;
        int remainingEnemies;
        bool gameOver = false;
        float elapsed = 0f;

        [Header("Intervals Used In Game")]
        // Semitone distances that are allowed to spawn (edit in Inspector)
        [SerializeField] int[] enabledSemitones = new int[] { 0, 2, 4, 5, 7, 9, 12 };


        [SerializeField] Transform turret;
        //[SerializeField] TurretController turretCtrl;   // ← drag your Turret here
        [SerializeField] float spawnArcDegrees = 140f;  // desired arc width
        [SerializeField] float edgePaddingDegrees = 10f;// keep away from clamp edges
        [SerializeField] float spawnYMin = 6f, spawnYMax = 22f;
        //public Transform TurretTransform;

        // --- Turret reference (single source of truth) ---
        [SerializeField] TurretController turretCtrl;   // drag your Turret root here in the Inspector
        public Transform TurretTransform => turretCtrl ? turretCtrl.transform : null;

        
        void Start()
        {
            remainingEnemies = waves * enemiesPerWave;
            UIHud.Instance?.SetRemaining(remainingEnemies);
            StartCoroutine(RunWaves());
        }

        void Update()
        {
            if (!gameOver)
            {
                elapsed += Time.deltaTime;
                UIHud.Instance?.SetTimer(elapsed);
            }
        }

        IEnumerator RunWaves()
        {
            for (int w = 0; w < waves; w++)
            {
                float t = waves <= 1 ? 0f : (float)w / (waves - 1);
                float spawnGap = Mathf.Lerp(spawnIntervalStart, spawnIntervalEnd, t);
                float enemySpeed = Mathf.Lerp(enemySpeedStart, enemySpeedEnd, t);

                for (int i = 0; i < enemiesPerWave; i++)
                {
                    SpawnEnemy(enemySpeed);
                    yield return new WaitForSeconds(spawnGap);
                }
                // small breather between waves
                yield return new WaitForSeconds(2f);
            }
        }

        void SpawnEnemy(float speed)
        {
            if (!enemyPrefab) { Debug.LogError("Enemy Prefab missing"); return; }

            // --- center the arc on the turret's *clamp center direction* (world space) ---
            Vector3 centerDir = turretCtrl ? turretCtrl.ClampCenterDir : Vector3.forward;

            // keep spawns fully inside the clamp window
            float allowedHalf = turretCtrl ? Mathf.Max(0f, turretCtrl.YawClampHalfAngle - edgePaddingDegrees) : 180f;
            float desiredHalf = spawnArcDegrees * 0.5f;
            float half = Mathf.Min(desiredHalf, allowedHalf);

            // slight center bias so edges are rarer
            float u = Random.value; u = u * u * (3f - 2f * u); // SmoothStep
            float offset = Mathf.Lerp(-half, +half, u);

            // rotate the centerDir around Y by 'offset'
            Vector3 dir = (Quaternion.AngleAxis(offset, Vector3.up) * centerDir).normalized;

            // spawn position
            Vector3 c = spawnRing ? spawnRing.position : Vector3.zero;
            Vector3 pos = c + dir * ringRadius;
            pos.y = Random.Range(spawnYMin, spawnYMax);

            // face inward
            Quaternion rot = Quaternion.LookRotation((c - pos).normalized, Vector3.up);

            var go = Instantiate(enemyPrefab, pos, rot);

            var es = go.GetComponent<EnemyShip>();
            es.rootMidi = Random.Range(rootMidiMin, rootMidiMax + 1);
            es.interval = PickRandomEnabledInterval();
            es.SetTarget(TurretTransform);
            // set speed if your EnemyShip exposes it or has a setter
        }

        public void OnAnswer(bool correctAns, IntervalDef interval)
        {
            attempts++;
            if (correctAns)
            {
                correct++;
                streak++;
                bestStreak = Mathf.Max(bestStreak, streak);
                int add = Mathf.RoundToInt(baseScore * (1f + 0.1f * Mathf.Clamp(streak - 1, 0, 10)));
                score += add;
                UIHud.Instance?.SetScore(score, add);
            }
            else
            {
                streak = 0;
                score = Mathf.Max(0, score - wrongPenalty);
                UIHud.Instance?.SetScore(score, -wrongPenalty);
            }
            UIHud.Instance?.SetAccuracy(correct, attempts);
        }

        public void OnEnemyDestroyed(EnemyShip _)
        {
            remainingEnemies--;
            UIHud.Instance?.SetRemaining(remainingEnemies);
            if (remainingEnemies <= 0 && !gameOver)
            {
                gameOver = true;
                UIHud.Instance?.ShowWin(score, elapsed, bestStreak, correct, attempts);
            }
        }

        public void GameOver()
        {
            if (gameOver) return;
            gameOver = true;

            // feedback
            UIHud.Instance?.HitStrobe(3, 0.05f, 0.05f, Color.red);   // try Color.red for damage vibe
            var shaker = FindFirstObjectByType<CameraShake>();
            shaker?.Shake(0.28f, 0.28f);

            UIHud.Instance?.ShowLose(score, elapsed, bestStreak, correct, attempts);
        }

        void OnDrawGizmosSelected()
        {
            if (!turret) return;
            Gizmos.color = Color.cyan;

            float centerYaw = turret.eulerAngles.y * Mathf.Deg2Rad;
            float half = Mathf.Max(0f, (spawnArcDegrees * 0.5f) - edgePaddingDegrees) * Mathf.Deg2Rad;
            Vector3 c = spawnRing ? spawnRing.position : Vector3.zero;

            const int steps = 48;
            for (int i = 0; i < steps; i++)
            {
                float a0 = centerYaw + Mathf.Lerp(-half, +half, i / (float)steps);
                float a1 = centerYaw + Mathf.Lerp(-half, +half, (i + 1) / (float)steps);
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0) * ringRadius, 0f, Mathf.Sin(a0) * ringRadius);
                Vector3 p1 = c + new Vector3(Mathf.Cos(a1) * ringRadius, 0f, Mathf.Sin(a1) * ringRadius);
                Gizmos.DrawLine(p0, p1);
            }
        }

        static IntervalDef? FindDefBySemitones(int semis)
        {
            for (int i = 0; i < IntervalTable.Count; i++)
            {
                var d = IntervalTable.ByIndex(i);
                if (d.semitones == semis) return d;
            }
            return null;
        }

        IntervalDef PickRandomEnabledInterval()
        {
            if (enabledSemitones != null && enabledSemitones.Length > 0)
            {
                // try a few times in case someone put an invalid number
                for (int tries = 0; tries < 6; tries++)
                {
                    int sem = enabledSemitones[Random.Range(0, enabledSemitones.Length)];
                    var def = FindDefBySemitones(sem);
                    if (def != null) return def.Value;
                }
            }
            // fallback: any interval
            return IntervalTable.ByIndex(Random.Range(0, IntervalTable.Count));
        }
        
        public void PlayHitFeedback()
        {
            // 3-pulse strobe + subtle shake
            UIHud.Instance?.HitStrobe(3, 0.05f, 0.05f, Color.white);
            var shaker = FindFirstObjectByType<CameraShake>();
            shaker?.Shake(0.28f, 0.28f);
        }


    }
}
