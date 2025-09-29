using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

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
                // --- Run tracking (for end screen) ---
        float  runStartTime;
        int    enemiesDestroyed;


        [Header("Hit Points")]
        [SerializeField] int maxHP = 3;
        public int CurrentHP { get; private set; }
        [SerializeField] HPDisplay hpUI;   // drag your HPDisplay here in the inspector

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
        public Transform TurretTransform => turretCtrl != null ? turretCtrl.transform : null;

        [Header("Lose Sequence")]
        [SerializeField] EndScreenController endScreen;
        // Optional knobs
        [SerializeField] bool slowMoOnDeath = true;
        [SerializeField] float slowMoFactor = 0.15f;  // 15% speed
        [SerializeField] float slowMoHold = 0.40f;  // seconds at min speed (unscaled)
        [SerializeField] float slowMoRecover = 0.40f;  // seconds back to 1.0 (unscaled)


        bool _gameOverShown;

        void Start()
        {
            CurrentHP = maxHP;
            if (hpUI != null)
            {
                hpUI.Build(maxHP);
                hpUI.Set(CurrentHP);
            }
            remainingEnemies = waves * enemiesPerWave;
                runStartTime      = Time.time;
                score             = 0;
                enemiesDestroyed  = 0;
                bestStreak        = 0;
            StartCoroutine(RunWaves());
        }

        void Update()
        {
            if (!gameOver)
            {
                elapsed += Time.deltaTime;
                var hud = UIHud.Instance;
                if (hud != null)
                {
                    hud.SetTimer(elapsed);
                }
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
            Vector3 centerDir = turretCtrl != null ? turretCtrl.ClampCenterDir : Vector3.forward;

            // keep spawns fully inside the clamp window
            float allowedHalf = turretCtrl != null ? Mathf.Max(0f, turretCtrl.YawClampHalfAngle - edgePaddingDegrees) : 180f;
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
            var hud = UIHud.Instance;
            if (correctAns)
            {
                correct++;
                streak++;
                bestStreak = Mathf.Max(bestStreak, streak);
                int add = Mathf.RoundToInt(baseScore * (1f + 0.1f * Mathf.Clamp(streak - 1, 0, 10)));
                score += add;
                if (hud != null)
                {
                    hud.SetScore(score, add);
                }
            }
            else
            {
                streak = 0;
                score = Mathf.Max(0, score - wrongPenalty);
                if (hud != null)
                {
                    hud.SetScore(score, -wrongPenalty);
                }
            }

            if (hud != null)
            {
                hud.SetAccuracy(correct, attempts);
            }
        }

        public void OnEnemyDestroyed(EnemyShip _)
        {
            remainingEnemies--;
            enemiesDestroyed++;
            if (remainingEnemies <= 0 && !gameOver)
            {
                gameOver = true;
                var hud = UIHud.Instance;
                if (hud != null)
                {
                    hud.ShowWin(score, elapsed, bestStreak, correct, attempts);
                }
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

        public void GameOver()
        {
            if (_gameOverShown) return;
            _gameOverShown = true;

            var stats = BuildRunStats();
            if (!isActiveAndEnabled)
            {
                // Component disabled (e.g., scene shutdown); finish the flow synchronously.
                endScreen.Show(stats);
                return;
            }

            StartCoroutine(DeathFlowCo(stats));
        }

        public void PlayerHit(int amount = 1)
        {
            CurrentHP = Mathf.Max(0, CurrentHP - amount);
            if (hpUI != null)
            {
                hpUI.Set(CurrentHP);
            }

            // existing “player bombed” feedback (screen flash, camera shake, SFX)
            PlayWrongAnswerFeedback();              // or a dedicated PlayerBombed feedback

            if (CurrentHP <= 0)
            {
                GameOver();
            }
        }


        public void PlayHitFeedback()
        {
            // 3-pulse strobe + subtle shake
            var hud = UIHud.Instance;
            if (hud != null)
            {
                hud.HitStrobe(3, 0.05f, 0.05f, Color.red);
            }

            var shaker = FindFirstObjectByType<CameraShake>();
            if (shaker != null)
            {
                shaker.Shake(0.28f, 0.28f);
            }
        }

        public void PlayWrongAnswerFeedback()
        {
            // // punchy red strobe, a bit quicker than bomb
            // UIHud.Instance?.HitStrobe(
            //     pulses: 3,
            //     on: 0.045f,
            //     off: 0.040f,
            //     color: new Color(1f, 0.25f, 0.25f) // red tint
            // );

            // Debug.Log($"[TrySubmit] wrong: paletteNull={SfxPalette.I == null}");
            // NEW: wrong-guess SFX (2D)
            //SfxPalette.I?.OnWrongGuess();

            // small tap shake (so it's distinct from a bomb)
            // var shaker = FindFirstObjectByType<CameraShake>();
            // shaker?.Shake(duration: 0.18f, amplitude: 0.18f);
        }

        RunStats BuildRunStats()
        {
            return new RunStats
            {
                score            = score,
                timeSeconds      = Time.time - runStartTime,
                enemiesDestroyed = enemiesDestroyed,
                correct          = correct,
                total            = attempts,
                bestStreak       = bestStreak,
            };
        }
        
        IEnumerator DeathFlowCo(RunStats stats)
        {
            // 1) brief death sequence (placeholder): red strobe + slow-mo
            // reuse your existing feedback method if available
            PlayWrongAnswerFeedback();    // strobe + small shake you already had

            if (slowMoOnDeath)
            {
                float originalScale = Time.timeScale;
                Time.timeScale = slowMoFactor;
                float t = 0f;

                // hold at min speed
                while (t < slowMoHold)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                // recover to 1.0
                t = 0f;
                while (t < slowMoRecover)
                {
                    t += Time.unscaledDeltaTime;
                    Time.timeScale = Mathf.Lerp(slowMoFactor, 1f, t / slowMoRecover);
                    yield return null;
                }
                Time.timeScale = 1f;
            }
            else
            {
                // small delay so the strobe isn’t cut off
                yield return new WaitForSecondsRealtime(0.6f);
            }

            endScreen.Show(stats);
        }

        public void RestartLevel()
        {
            // reset time scale in case we change this later
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        public void QuitToDashboard()
        {
            Time.timeScale = 1f;
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
        }



    }
}
