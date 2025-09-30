**Project: EarFPS** — Interval Aim Trainer (Unity, URP, Windows)
**Purpose of this file:** Give an assistant enough context to onboard in seconds, then point to the deep docs for details.

## Snapshot

**Platform:** Windows desktop. Voice features depend on UnityEngine.Windows.Speech.KeywordRecognizer (guard with #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN). 

**Render:** URP, neon/abstract aesthetic.

**Core loop:** Aim at enemy → hold “Listen” to audition interval → submit interval (voice or spacebar path) → correct = kill missile; wrong = dud fizzle on enemy shield (no damage). Scoring/HP tracked by GameManager. 

**Processing pill:** intentionally removed; Voice UI shows Listening and ✓/✗ result only. 

## Top-level responsibilities

**GameManager** — Owns run state (Playing → Dying → EndScreen), score/time/HP/accuracy, death flow, stats aggregation, restart/quit. Publics: ApplyDamage(int), OnAnswer(bool, IntervalDef), GameOver(), BuildRunStats(), RestartLevel(), QuitToDashboard(). 

**IntervalQuizController** — Gating & submission:

TrySubmit() (spacebar path) and TrySubmitInterval(IntervalDef) (voice path).
On correct → FireAtTarget() spawns HomingMissile, triggers recoil/SFX, notifies GameManager.OnAnswer(true, …).
On wrong → also fires a dud missile, notifies GameManager.OnAnswer(false, …). 

**HomingMissile** — Homing flight; kill vs dud outcomes.

Kill: normal explosion VFX/SFX; enemy destroyed.
Dud: shield bubble (tinted to enemy), separate fizzle VFX (tinted to missile), 3D dud SFX, no damage. SetTint(Color) keeps emissive/trail/Light in sync. 

**EnemyShip** — Moves toward turret; bombs player (HP–1) on proximity; color tint used by shield bubble. 

**UIHud** — HUD (score/time/accuracy, HP icons).

**EndScreenController** — End overlay; Show(RunStats, onReplay, onQuit) populates stats, pauses time, unlocks cursor; buttons call GameManager to restart/quit. Must block raycasts and disable gameplay inputs while visible. 

**VoiceIntervalInput (Windows)** — PTT V; uses KeywordRecognizer grammar. Important: recognition callback runs on a background thread → it enqueues to a main-thread queue/lock; UI updates and quiz submission happen on Update(). 

**VoiceUI** — Status text & mic level bar. No “processing pill” per current design. 

## Wiring / Inspector checklist (quick audit)

**IntervalQuizController** on PlayerCamera:
    - Assign: turret, audioSource, missileSpawn, missilePrefab.
    - Ensure both submit paths call the same fire path (FireAtTarget()) so recoil/SFX are consistent. 

**MuzzleRecoil** on Turret muzzle mesh; Kick() is called on fire.

**HomingMissile prefab:**
    - Has Light component driven by SetTint(Color). Do not override color in Inspector at runtime. 
    - Explode VFX (kill) & Fizzle VFX (dud); Shield bubble VFX tints from EnemyShip color; Fizzle VFX tints from missile color. 

**EnemyShip prefab:** Renderer array set (or auto-find), holds current tint (used by shield).

**UI** HUD visible in play, EndScreen CanvasGroup set to interactable=true, blocksRaycasts=true, hidden by alpha at start. Buttons wired to GameManager.RestartLevel() /  QuitToDashboard(). Cursor unlock + input gate when visible. 

**Audio** SfxPalette in scene; hooks: missile fire, explode, dud fizzle, wrong guess, player bomb.

**Voice** VoiceIntervalInput present, PTT key set (V), platform guards in place, and mic device selected in Unity audio settings if needed. 

## Game flow (today)

**Playing:** Enemies spawn on arc; player listens (RMB) and submits (voice or spacebar).
**Answer:** IntervalQuizController → GameManager.OnAnswer(...) for score/streaks.
    Correct: homing missile kills target (explosion).
    Wrong: dud missile fizzles on shield (no damage) + wrong-answer feedback. 
**Player hit:** Enemy bombs → GameManager.ApplyDamage(1); HP tracked; when 0 → death flow (strobe + optional slow-mo) → EndScreen. 
**EndScreen:** shows RunStats; Retry reloads scene; Dashboard quits (placeholder). 

## Conventions & gotchas (must-reads)

Always submit through IntervalQuizController so muzzle recoil, SFX, and GameManager hooks all run in sync. 
End screen must pause gameplay and unlock cursor, blocking look/shoot inputs. 
Voice callback is background thread: never touch Unity objects there; enqueue to main thread (already implemented). 
On restart, don’t start coroutines on disabled objects (e.g., mic/voice cleanup). 

## Extension points (near-term)

Events to decouple systems (optional): OnAnswerEvaluated, OnPlayerHPChanged, OnRunEnded. 
AsmDef split by area: Core / Player / Enemies / UI / AudioVFX. 

## Where to learn more (existing docs)

**AI_HINTS.md** — Unity/platform constraints, wiring rules, dud/kill tinting, scene reload gotchas. 
**ARCHITECTURE.md** — Responsibilities and flows for GameManager, EndScreen, Player, Voice, Combat, UI. 
**AI_README.md** — Repo structure and authoring conventions (template). 

