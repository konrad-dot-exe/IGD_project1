# Files (Hot Index)

> Paths assume `Assets/`.

## Core
- `Scripts/Core/GameManager.cs` — Run state (HP, score, timers, streaks), death flow, end screen hooks.
- `Scripts/Core/EndScreenController.cs` — End screen UI, stats population, fade, button callbacks.
- `Scripts/Core/RunStats.cs` 

## Audio
- `Scripts/Audio/SfxPalette.cs` — Centralized SFX hooks (optional).
- `Scripts/Audio/ToneSynth.cs` - Generates the tones for musical intervals
- `Scripts/Player/VoiceIntervalInput.cs` — Windows speech; PTT **V**; grammar → `TrySubmitInterval`; uses thread-safe queue.

## Player
- `Scripts/Player/IntervalQuizController.cs` — Targeting & submission (spacebar/voice); spawns missiles; calls `GameManager.OnAnswer(...)`.
- `Scripts/Player/ListenZoom.cs` — Listen FOV/vignette/overlay; `Begin(bool on)`, `SetImmediate(bool on)`.
- `Scripts/Player/MuzzleRecoil.cs` — Local-space kickback when firing (curve-based).

## Enemies & Combat
- `Scripts/Enemies/EnemyShip.cs` — Enemy logic; exposes tint; damage to player.
- `Scripts/Enemies/HomingMissile.cs` — Homing flight & outcome (kill/dud); VFX/SFX instantiation; `SetTint(Color)`.
- `Scripts/Enemies/ShieldBubble.cs` - Enemy shield effect logic
- `Scripts/Enemies/SpawnAppear.cs` - Enemy spawn effect logic

## Environment
- `Scripts/Environment/ProceduralGround.cs` - Creates procedural terrain for ground plane

## FX
- `Scripts/FX/ExplosionLightPulse.cs` - Lighting effect for explosions

## UI
- `Scripts/UI/UIHud.cs` — HUD labels; HP icons.
- `Scripts/UI/VoiceUI.cs` — Listening/result text; mic level meter.
- `Scripts/UI/MicLevelMeter.cs` — Reads mic input; drives bar fill.

## Prefabs (examples)
- `Prefabs/HomingMissile.prefab` — missile model, light, trails; script refs.
- `Prefabs/ExplosionVFX.prefab` — kill explosion.
- `Prefabs/MissileFizzle.prefab` — dud fizzle (missile tint).
- `Prefabs/EnemyShield.prefab` — shield bubble (enemy tint).

