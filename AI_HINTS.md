# AI Hints (Conventions & Gotchas)

## Unity & Platform
- Voice recognition relies on `UnityEngine.Windows.Speech.KeywordRecognizer`. Keep voice features behind `#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN`.
- Target is Windows desktop.

## Flows to Respect
- Always submit answers via `IntervalQuizController` (`TrySubmitInterval` or `TrySubmit`) so recoil/SFX and GameManager hooks are consistent.
- Use `GameManager.OnAnswer(correct, interval)` to update scoring/streaks.
- Use `GameManager.ApplyDamage(1)` when the player is bombed; it decides death.
- Show end screen with `EndScreenController.Show(stats, onReplay, onQuit)`; it will pause time and unlock the cursor.

## UI & Input State
- End screen **must**:
  - `CanvasGroup.blocksRaycasts = true` and `interactable = true`.
  - Pause gameplay (`Time.timeScale = 0` or GameManager state) and **unlock** cursor.
  - Disable gameplay inputs (camera look) while visible (GameManager/PlayerCamera gate).
- Buttons should call `GameManager.RestartLevel()` and `GameManager.QuitToDashboard()` (or pass in callbacks from `Show`).

## Missile Color / Light
- Set tint through `HomingMissile.SetTint(Color)` to keep emissive, trails, and `Light.color` in sync. Avoid direct inspector overrides at runtime.

## Dud vs Kill
- Dud must not damage or stun.  
- Shield bubble VFX uses **enemy tint**.  
- Fizzle VFX uses **missile tint**.  
- Dud may trigger earlier than kill (slightly larger trigger radius) to sell the shield contact.

## Voice UX
- The “processing pill” was removed intentionally. The Voice UI shows:
  - “Listening…” while PTT active,
  - then ✓ or ✗ result label.  
- Recognition callback runs on a background thread; always **queue to main thread** (already implemented with a `PendingCommand` struct + lock).

## Scene Reload & Coroutines
- On restart, avoid starting coroutines on disabled/inactive objects.  
  - Example fix: in `VoiceIntervalInput.OnDisable`, don’t call into components on inactive objects; simply stop the recognizer & clean up without touching other dependencies.

## Future Refactors (Optional)
- Introduce events: `OnAnswerEvaluated`, `OnRunEnded`, `OnPlayerHPChanged` to reduce direct references.
- Split AsmDefs by folder: `Core`, `Player`, `Enemies`, `UI`, `AudioVFX`.

## Style
- Keep UI colors to HUD cyan; end screen labels/values match.
- Prefer small, data-only structs for VFX/SFX settings passed to runtime (e.g., `MissileVfxData`).

