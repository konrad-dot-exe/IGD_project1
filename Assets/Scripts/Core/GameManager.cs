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
            Vector2 circle = Random.insideUnitCircle.normalized * ringRadius;
            Vector3 pos = new Vector3(circle.x, Random.Range(6f, 20f), circle.y);
            var go = Instantiate(enemyPrefab, pos, Quaternion.LookRotation(-pos.normalized));
            var es = go.GetComponent<EnemyShip>();
            es.rootMidi = Random.Range(rootMidiMin, rootMidiMax + 1);
            es.interval = IntervalTable.ByIndex(Random.Range(0, IntervalTable.Count));
            // speed override
            var spdField = typeof(EnemyShip).GetField("speed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (spdField != null) spdField.SetValue(es, speed);
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
            UIHud.Instance?.ShowLose(score, elapsed, bestStreak, correct, attempts);
        }

        void OnDrawGizmosSelected()
        {
            if (spawnRing == null) return;
            Gizmos.color = Color.cyan;
            const int steps = 64;
            Vector3 c = spawnRing.position;
            Vector3 prev = c + new Vector3(ringRadius, 0, 0);
            for (int i = 1; i <= steps; i++)
            {
                float a = (i / (float)steps) * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(a)*ringRadius, 0, Mathf.Sin(a)*ringRadius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }

    }
}
