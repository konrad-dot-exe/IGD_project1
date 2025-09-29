# Architecture

## Game Flow (authoritative)

**GameManager**
- Owns run state: `Playing → Dying → EndScreen`.
- Tracks: score, time, HP, accuracy (correct/total), enemies destroyed, best streak.
- Death flow: brief strobe + optional slow-mo → recover → show end screen.
- Exposes:
  - `void ApplyDamage(int amount)` → handles HP and death.
  - `void OnAnswer(bool correct, IntervalDef interval)` → scoring & streaks (invoked by quiz).
  - `void GameOver()` → kicks off death coroutine.
  - `RunStats BuildRunStats()` → aggregates stats for UI.
  - `void RestartLevel()` / `void QuitToDashboard()`.

**EndScreenController**
- Canvas overlay (blocks raycasts & input).
- `Show(RunStats stats, Action onReplay, Action onQuit)` → fades in, populates stats, binds buttons, pauses time, unlocks cursor.
- Buttons: **Retry** (scene reload), **Dashboard** (quit app for now).

## Player / Input

**PlayerCamera / ListenZoom**
- Aiming and any listen-UI zoom/vignette feedback.  
- `ListenZoom.Begin(on)` / `SetImmediate(on)` provide animated or instant transitions.

**IntervalQuizController**
- Decides when a submission can be made (target acquisition).
- Two submits:
  - `TrySubmit()` (spacebar path, optional).
  - `TrySubmitInterval(IntervalDef chosen)` (voice path).
- On **correct**: `FireAtTarget()` → spawns `HomingMissile`, calls `muzzleRecoil.Kick()`, SFX, notifies `GameManager.OnAnswer(true, …)`.
- On **wrong**: still fires a missile, but set as **dud** → notifies `GameManager.OnAnswer(false, …)`.

**VoiceIntervalInput** (Windows only)
- PTT (**V**). Uses `KeywordRecognizer` on a grammar → enqueues recognition on a **thread-safe queue** and consumes on main thread.
- On mapped phrase → calls `quiz.TrySubmitInterval(def)`.
- Drives `VoiceUI.SetListening(bool)` + result toasts.

## Combat

**HomingMissile**
- Flight: speed, turn rate, lifetime.
- Outcome:
  - **Kill**: normal explosion (radius), SFX, enemy destroyed.
  - **Dud**: uses same flight; on proximity to enemy shield:
    - Spawns **Shield Bubble VFX** at enemy (tinted to enemy color; fast flash then fade).
    - Spawns **Missile Fizzle VFX** at hit (tinted to **missile** color).
    - Plays dud SFX (3D).
    - No damage / stun; missile destroyed.
- Tint:
  - `SetTint(Color c)` sets internal emissive, trails, and **missile Light** color (fixed to avoid mismatched hues).

**EnemyShip**
- Holds team/tint color (used by shield bubble). Emits bomb attacks → hits player (HP-1).  
- Destroy logic increments `enemiesDestroyed`.

## UI Layer

**UIHud**
- Displays score/time/accuracy (cyan), HP icons, etc.

**VoiceUI**
- Simple status text (“Listening…”, “✓ Major Sixth”, “✗ Unknown”), mic level meter bar.  
- Processing pill **removed** per design.

**ScreenFlash**
- Red strobe on damage/death.

## Cross-Script Interactions (today)

- VoiceIntervalInput → IntervalQuizController (submit via `TrySubmitInterval`).
- IntervalQuizController → GameManager (`OnAnswer(correct, interval)`).
- IntervalQuizController → HomingMissile (spawn & init).
- HomingMissile → EnemyShip (hit/destroy) & SFX/VFX.
- EnemyShip → GameManager (`ApplyDamage(1)` on player hit).
- GameManager → EndScreenController (`Show(stats, callbacks)`).

## Proposed Events (optional future refactor)

- `public static event Action<bool, IntervalDef, EnemyShip> OnAnswerEvaluated;`
- `public static event Action<int,int> OnPlayerHPChanged;`
- `public static event Action<RunStats> OnRunEnded;`

Using events will further decouple UI and systems.

