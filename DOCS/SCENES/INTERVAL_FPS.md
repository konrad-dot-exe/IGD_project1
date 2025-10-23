# Overview

- First-person interval-training shooter: you steer a turret with the mouse, listen to dyads emitted from the targeted ship, and label the interval to launch a homing missile.【F:Assets/Scripts/Player/TurretController.cs†L6-L95】【F:Assets/Scripts/Player/IntervalQuizController.cs†L163-L317】【F:Assets/Scenes/IntervalTrainer.unity†L2160-L2243】
- Learning goal: translate the heard root+interval pair into the correct musical interval name to remove the approaching enemy before it reaches bombing range.【F:Assets/Scripts/Player/IntervalQuizController.cs†L163-L269】【F:Assets/Scripts/Core/IntervalExerciseController.cs†L106-L209】【F:Assets/Scripts/Core/IntervalDefs.cs†L19-L45】
- Input options include mouse aim, right-mouse listening hold, scroll/Q/E for selection, space to fire, and push-to-talk voice recognition that maps phrases to semitone distances.【F:Assets/Scripts/Player/IntervalQuizController.cs†L104-L161】【F:Assets/Scripts/Audio/VoiceIntervalInput.cs†L14-L221】【F:Assets/Scenes/IntervalTrainer.unity†L2160-L2358】
- Core feedback loop: correct answers spawn a missile that destroys the enemy and awards score; misses trigger dud shots, HP loss, or eventual defeat; completing all waves or losing HP feeds into the end screen with run stats.【F:Assets/Scripts/Player/IntervalQuizController.cs†L207-L317】【F:Assets/Scripts/Enemies/HomingMissile.cs†L134-L229】【F:Assets/Scripts/Core/IntervalExerciseController.cs†L160-L374】【F:Assets/Scripts/UI/EndScreenController.cs†L35-L89】

# Scene Composition

| GameObject / Prefab | Scripts & Components | Key Serialized Fields / Notes |
| --- | --- | --- |
| **GameRoot** | `IntervalExerciseController`, `Transform` | Sets enemy prefab, spawn ring, wave pacing, scoring, HP, interval pool, turret references, and end-screen hooks.【F:Assets/Scenes/IntervalTrainer.unity†L8424-L8479】【F:Assets/Scripts/Core/IntervalExerciseController.cs†L12-L75】 |
| **GameRoot/SfxPalette** | `SfxPalette` | Shared missile, explosion, wrong-answer, and bomb audio clips plus volume/pitch controls.【F:Assets/Scenes/IntervalTrainer.unity†L8603-L8636】【F:Assets/Scripts/Audio/SfxPalette.cs†L5-L77】 |
| **GameRoot/SpawnRing** | `Transform` | Origin and radius used for ring-based enemy spawning around the turret.【F:Assets/Scenes/IntervalTrainer.unity†L2009-L2040】【F:Assets/Scripts/Core/IntervalExerciseController.cs†L124-L158】 |
| **Turret** | `TurretController`, `Transform` | Locks cursor, clamps yaw/pitch, soft-locks nearest enemy, and exposes `CurrentTarget`.【F:Assets/Scenes/IntervalTrainer.unity†L10681-L10732】【F:Assets/Scripts/Player/TurretController.cs†L6-L105】 |
| **Turret/PlayerCamera** | `IntervalQuizController`, `AudioSource`, `VoiceIntervalInput`, `ListenZoom`, `CameraShake` | Missile prefab, missile spawn, scroll & key tuning, listening zoom, voice push-to-talk, and shake target wiring.【F:Assets/Scenes/IntervalTrainer.unity†L2160-L2409】【F:Assets/Scripts/Player/IntervalQuizController.cs†L9-L317】【F:Assets/Scripts/Audio/VoiceIntervalInput.cs†L14-L224】【F:Assets/Scripts/Player/ListenZoom.cs†L9-L125】【F:Assets/Scripts/Player/CameraShake.cs†L6-L84】 |
| **PlayerCamera/Muzzle** | `MuzzleRecoil`, mesh components | Recoil distance/timing and UnityEvent that triggers staff glow flashes on kick.【F:Assets/Scenes/IntervalTrainer.unity†L607-L755】【F:Assets/Scenes/IntervalTrainer.unity†L1995-L2009】【F:Assets/Scripts/Player/MuzzleRecoil.cs†L8-L98】 |
| **Turret/Staff (prefab instance)** | `StaffGlow` | HDR base/flash intensities for the wand orb emissive pulse and shot flash.【F:Assets/Scenes/IntervalTrainer.unity†L11920-L11955】【F:Assets/Scripts/Player/StaffGlow.cs†L5-L94】 |
| **Canvas/UIHud** | `UIHud`, layout components | Score, timer, accuracy, streak, selection labels, toast FX, and hit strobe CanvasGroups.【F:Assets/Scenes/IntervalTrainer.unity†L9549-L9656】【F:Assets/Scripts/UI/UIHud.cs†L8-L170】 |
| **Canvas/IntervalCarousel** | `IntervalCarouselUI`, `CanvasGroup` | Five-slot interval wheel showing selection around the active target while listening.【F:Assets/Scenes/IntervalTrainer.unity†L2560-L2604】【F:Assets/Scripts/UI/IntervalCarouselUI.cs†L6-L96】 |
| **Canvas/VoiceUI** | `VoiceUI`, `MicLevelMeter`, optional image | Displays listening state, recognition result, and mic level meter tied to push-to-talk.【F:Assets/Scenes/IntervalTrainer.unity†L8848-L8981】【F:Assets/Scripts/UI/VoiceUI.cs†L8-L66】【F:Assets/Scripts/UI/MicLevelMeter.cs†L6-L120】 |
| **Canvas/HP** | `HPDisplay`, `RectTransform` | Builds cyan square HP pips sized/spaced per inspector values.【F:Assets/Scenes/IntervalTrainer.unity†L258-L312】【F:Assets/Scripts/UI/HPDisplay.cs†L8-L50】 |
| **Canvas/Crosshair** | `CrosshairUI`, `Image` | Changes crosshair tint/scale when a target is locked via the turret.【F:Assets/Scenes/IntervalTrainer.unity†L20720-L20801】【F:Assets/Scripts/UI/CrosshairUI.cs†L6-L24】 |
| **Canvas/EndScreen** | `EndScreenController`, `CanvasGroup`, TMP labels, buttons | Presents win/lose stats, pauses time, unlocks cursor, and wires retry/dashboard buttons.【F:Assets/Scenes/IntervalTrainer.unity†L3340-L3360】【F:Assets/Scripts/UI/EndScreenController.cs†L11-L89】 |
| **GameRoot/MinimapCamera** | `Camera`, `Transform` | Optional overhead view camera positioned high above the arena.【F:Assets/Scenes/IntervalTrainer.unity†L8982-L9014】 |

# Gameplay Flow

1. **Scene initialization:** `IntervalExerciseController.Start` builds the HP UI, resets streak/score tracking, and kicks off the wave coroutine while the turret locks the cursor and centers its clamps.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L77-L105】【F:Assets/Scripts/Player/TurretController.cs†L48-L60】 
2. **Wave spawning:** `RunWaves` repeatedly calls `SpawnEnemy`, picking arc positions around the `SpawnRing`, root MIDI ranges, and enabled intervals to populate approaching ships.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L106-L158】【F:Assets/Scenes/IntervalTrainer.unity†L2009-L2040】 
3. **Target acquisition & listening:** The turret soft-locks the nearest ship, while holding RMB (or keyboard bind) starts `ListenCoroutine`, which synthesizes the root and target notes with distance-based tempo and volume scaling.【F:Assets/Scripts/Player/TurretController.cs†L62-L95】【F:Assets/Scripts/Player/IntervalQuizController.cs†L133-L198】 
4. **Answer selection:** Players cycle the interval wheel via scroll/Q/E or speak a keyword; the UI carousel and voice HUD update to mirror the currently armed interval.【F:Assets/Scripts/Player/IntervalQuizController.cs†L104-L161】【F:Assets/Scripts/Audio/VoiceIntervalInput.cs†L101-L221】【F:Assets/Scripts/UI/IntervalCarouselUI.cs†L32-L64】 
5. **Submission & feedback:** Pressing space or a recognized voice command invokes `TrySubmit`, firing a missile if correct or a dud if wrong, while scoring updates and lockout timers prevent spamming.【F:Assets/Scripts/Player/IntervalQuizController.cs†L207-L269】【F:Assets/Scripts/Core/IntervalExerciseController.cs†L160-L194】 
6. **Projectile resolution:** `HomingMissile` homes in on the target, detonates with enemy-tinted VFX/SFX on success, or triggers a shield bubble fizzle on misses; destroyed enemies notify the controller to advance the wave count.【F:Assets/Scripts/Enemies/HomingMissile.cs†L61-L229】【F:Assets/Scripts/Enemies/EnemyShip.cs†L84-L118】【F:Assets/Scripts/Core/IntervalExerciseController.cs†L196-L209】 
7. **Damage & loss:** If a ship reaches bomb radius, `EnemyShip.Update` inflicts HP damage and calls `PlayerHit`, which drives HUD feedback and checks for game over.【F:Assets/Scripts/Enemies/EnemyShip.cs†L70-L81】【F:Assets/Scripts/Core/IntervalExerciseController.cs†L255-L270】 
8. **End conditions:** Clearing all enemies runs `WinFlowCo` to celebrate and open the end screen; depleting HP triggers `GameOver`, slow-motion death flow, and the same overlay with failure messaging.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L200-L374】【F:Assets/Scripts/UI/EndScreenController.cs†L35-L69】 

# Core Systems

## IntervalQuizController
- Handles selection, listening, and submission with serialized tuning for scroll deadzones, keyboard cooldowns, and missile visuals.【F:Assets/Scripts/Player/IntervalQuizController.cs†L30-L64】【F:Assets/Scenes/IntervalTrainer.unity†L2160-L2243】 
- `ListenCoroutine` dynamically adjusts playback tempo/volume by target distance and synthesizes tones via `ToneSynth` with the configured ADSR envelope.【F:Assets/Scripts/Player/IntervalQuizController.cs†L163-L198】【F:Assets/Scripts/Audio/ToneSynth.cs†L18-L65】 
- `TrySubmit` and `TrySubmitInterval` coordinate with `IntervalExerciseController` for scoring, spawn missiles, and start a miss lockout coroutine to throttle inputs.【F:Assets/Scripts/Player/IntervalQuizController.cs†L207-L269】 
- `SetVoiceListening` ducks quiz audio and drives `ListenZoom`'s camera/vignette transition so spoken answers can be heard clearly.【F:Assets/Scripts/Player/IntervalQuizController.cs†L272-L317】【F:Assets/Scripts/Player/ListenZoom.cs†L53-L125】 

## IntervalExerciseController
- Governs wave cadence, spawn geometry, allowed intervals, and enemy targeting of the turret transform provided in the inspector.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L12-L158】【F:Assets/Scenes/IntervalTrainer.unity†L8424-L8473】 
- Tracks score, streak, accuracy, and elapsed time, updating `UIHud` on every answer while collecting `RunStats` for the end screen.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L29-L305】【F:Assets/Scripts/UI/UIHud.cs†L42-L125】【F:Assets/Scripts/Core/RunStats.cs†L4-L13】 
- Provides HP management and hit feedback that drives `UIHud.HitStrobe` and `CameraShake`, plus optional slow-motion death sequences.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L255-L343】 
- Exposes restart/quit helpers used by end-screen buttons to reload the scene or exit play mode.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L345-L360】 

## VoiceIntervalInput & VoiceUI
- Maintains a phrase-to-semitone grammar (P1–M9), queues recognizer callbacks onto the main thread, and submits mapped intervals to the quiz; on unsupported platforms it logs a warning instead of starting the recognizer.【F:Assets/Scripts/Audio/VoiceIntervalInput.cs†L33-L224】 
- `VoiceUI` presents listening state and short-lived success/fail labels, while `MicLevelMeter` monitors microphone RMS/peak levels with optional auto-calibration and opacity blending when not listening.【F:Assets/Scripts/UI/VoiceUI.cs†L8-L66】【F:Assets/Scripts/UI/MicLevelMeter.cs†L6-L120】 

## StaffGlow & MuzzleRecoil
- `MuzzleRecoil.Kick` animates the muzzle back/forward along a configurable axis and invokes a UnityEvent tied to the staff orb flash.【F:Assets/Scripts/Player/MuzzleRecoil.cs†L12-L98】【F:Assets/Scenes/IntervalTrainer.unity†L607-L755】【F:Assets/Scenes/IntervalTrainer.unity†L1995-L2009】 
- `StaffGlow` instantiates a private emissive material, runs an idle pulse, and performs an HDR flash with attack/decay envelopes when the muzzle fires.【F:Assets/Scripts/Player/StaffGlow.cs†L5-L94】【F:Assets/Scenes/IntervalTrainer.unity†L11935-L11955】 

## Enemy & Projectile Behaviours
- `EnemyShip` homes toward the turret, bombs within `bombRadius`, and colors meshes via a palette; `FindNearestInCone` underpins the turret soft lock.【F:Assets/Scripts/Enemies/EnemyShip.cs†L6-L150】 
- `HomingMissile` steers toward the last known target, kills enemies with tinted VFX/SFX, or spawns a shield bubble fizzle when the answer was wrong.【F:Assets/Scripts/Enemies/HomingMissile.cs†L61-L330】 
- `SfxPalette` centralizes missile/bomb/wrong-guess sounds for both world and UI playback.【F:Assets/Scripts/Audio/SfxPalette.cs†L5-L77】 

# Audio & Visual Feedback

- Interval prompts are synthesized on the fly using `ToneSynth.CreateTone` with configurable waveform and ADSR, modulated by distance-based timing and ducked volume during voice listening.【F:Assets/Scripts/Player/IntervalQuizController.cs†L163-L198】【F:Assets/Scripts/Player/IntervalQuizController.cs†L272-L301】【F:Assets/Scripts/Audio/ToneSynth.cs†L18-L65】 
- Correct submissions trigger `MuzzleRecoil` and `StaffGlow` flashes, `SfxPalette.OnMissileFire`, and homing missiles that explode with enemy-tinted particles and optional 3D audio.【F:Assets/Scripts/Player/IntervalQuizController.cs†L303-L317】【F:Assets/Scripts/Enemies/HomingMissile.cs†L134-L185】【F:Assets/Scripts/Audio/SfxPalette.cs†L35-L47】 
- Wrong answers spawn shield bubble fizzles, optionally play wrong-answer feedback, and start a lockout timer while HUD elements can strobe red when the player is hit.【F:Assets/Scripts/Player/IntervalQuizController.cs†L223-L268】【F:Assets/Scripts/Enemies/HomingMissile.cs†L187-L229】【F:Assets/Scripts/Core/IntervalExerciseController.cs†L255-L285】 
- `ListenZoom` narrows FOV, deepens vignette, and fades an overlay while listening; `CameraShake` offsets a child camera transform for impact feedback; `UIHud` provides screen flash and toast utilities.【F:Assets/Scripts/Player/ListenZoom.cs†L9-L125】【F:Assets/Scripts/Player/CameraShake.cs†L6-L84】【F:Assets/Scripts/UI/UIHud.cs†L24-L152】 
- `VoiceUI` and `MicLevelMeter` report listening state, recognized interval names, and mic input levels to guide voice submissions.【F:Assets/Scripts/UI/VoiceUI.cs†L48-L66】【F:Assets/Scripts/UI/MicLevelMeter.cs†L6-L120】 

# Game State & Event Flow

- Key flags: `IntervalQuizController.IsListening` and `isLockedOut` control listening loops and submission gating; `VoiceIntervalInput` holds `isListening`; `IntervalExerciseController.CurrentHP` and `_gameOverShown` gate end flows; `HomingMissile` tracks `exploded` and `isDud` state.【F:Assets/Scripts/Player/IntervalQuizController.cs†L69-L75】【F:Assets/Scripts/Player/IntervalQuizController.cs†L46-L237】【F:Assets/Scripts/Audio/VoiceIntervalInput.cs†L56-L160】【F:Assets/Scripts/Core/IntervalExerciseController.cs†L41-L75】【F:Assets/Scripts/Enemies/HomingMissile.cs†L45-L230】 
- Event progression:
  - Start Round → `RunWaves` instantiates enemy ships around `SpawnRing` → Turret soft-locks a target → Player listens via RMB or voice PTT → `TrySubmit` validates the chosen interval → `FireMissile` launches homing projectile → Enemy ship dies or bombs → `OnEnemyDestroyed` schedules the next interval or ends the run.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L77-L209】【F:Assets/Scripts/Player/TurretController.cs†L62-L95】【F:Assets/Scripts/Player/IntervalQuizController.cs†L133-L317】【F:Assets/Scripts/Enemies/EnemyShip.cs†L70-L94】 

# UI & Controls

- Mouse look + cursor lock are managed by `TurretController`, while scroll/Q/E cycle the interval carousel and Space submits the current selection.【F:Assets/Scripts/Player/TurretController.cs†L48-L95】【F:Assets/Scripts/Player/IntervalQuizController.cs†L104-L161】【F:Assets/Scripts/UI/IntervalCarouselUI.cs†L32-L64】 
- Holding RMB (or optional key) starts audio playback, with `ListenZoom` adjusting FOV and overlays to emphasize listening mode.【F:Assets/Scripts/Player/IntervalQuizController.cs†L133-L150】【F:Assets/Scripts/Player/ListenZoom.cs†L53-L125】 
- Voice control uses `Key.V` (by default) as push-to-talk, highlights status via `VoiceUI`, and displays recognized interval names; optional HUD toasts can log raw phrases.【F:Assets/Scripts/Audio/VoiceIntervalInput.cs†L101-L224】【F:Assets/Scripts/UI/VoiceUI.cs†L48-L66】 
- `UIHud` shows score, timer, accuracy, streak, and currently selected interval; `HPDisplay` builds cyan pips; `EndScreenController` pauses the game, unlocks the cursor, and presents final stats with retry/quit buttons.【F:Assets/Scripts/UI/UIHud.cs†L42-L170】【F:Assets/Scripts/UI/HPDisplay.cs†L21-L50】【F:Assets/Scripts/UI/EndScreenController.cs†L35-L89】 

# Dependencies

- **Internal:** `IntervalExerciseController`, `IntervalQuizController`, `TurretController`, `VoiceIntervalInput`, `ListenZoom`, `UIHud`, `HPDisplay`, `EndScreenController`, `SfxPalette`, and enemy scripts must all be present and cross-wired as in `GameRoot` and its children.【F:Assets/Scenes/IntervalTrainer.unity†L607-L2409】【F:Assets/Scripts/Core/IntervalExerciseController.cs†L12-L374】 
- **External APIs:** Uses the Unity Input System (`UnityEngine.InputSystem`) for scroll/key detection, `UnityEngine.Windows.Speech.KeywordRecognizer` for voice capture on Windows, and the Unity `Microphone` API for live level metering.【F:Assets/Scripts/Player/IntervalQuizController.cs†L1-L155】【F:Assets/Scripts/Audio/VoiceIntervalInput.cs†L4-L140】【F:Assets/Scripts/UI/MicLevelMeter.cs†L14-L88】 

# Known Issues / Gotchas

- Voice recognition is Windows-only; non-Windows builds fall back to logging “Voice recognition requires Windows,” so include an alternative input path for other platforms.【F:Assets/Scripts/Audio/VoiceIntervalInput.cs†L6-L143】 
- Microphone capture warns and aborts if no device is available; ensure microphone permissions are granted before shipping voice features.【F:Assets/Scripts/UI/MicLevelMeter.cs†L59-L99】 
- Misses trigger a one-second lockout; the player cannot submit again until the coroutine finishes, so communicate this through UI if tuning the value.【F:Assets/Scripts/Player/IntervalQuizController.cs†L46-L238】 
- The turret keeps the cursor locked until the end screen runs; `EndScreenController.Show` restores cursor state and time scale, so avoid alternate exit paths that skip it.【F:Assets/Scripts/Player/TurretController.cs†L48-L64】【F:Assets/Scripts/UI/EndScreenController.cs†L35-L89】 
- `PlayWrongAnswerFeedback` is currently a placeholder; add audiovisual cues there rather than duplicating logic elsewhere.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L289-L292】 
- `HomingMissile` dud handling expects a `ShieldBubble` component on the assigned prefab; omitting it removes the shield pop effect.【F:Assets/Scripts/Enemies/HomingMissile.cs†L187-L205】 
- `UIHud.ToastCorrect` is stubbed out, so world-space interval toasts require either enabling the prefab logic or supplying your own feedback script.【F:Assets/Scripts/UI/UIHud.cs†L63-L75】 

# Extensibility / TODO

- Introduce difficulty scaling by adjusting wave counts, spawn gaps, and enemy speed curves to keep advanced players challenged.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L17-L118】 
- Expand or tier the interval pool by editing `enabledSemitones` and `IntervalTable`, enabling curriculum-based playlists or descending intervals.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L46-L236】【F:Assets/Scripts/Core/IntervalDefs.cs†L19-L45】 
- Implement adaptive feedback in `PlayWrongAnswerFeedback` (e.g., HUD strobes, SFX) and hook it into `SfxPalette.OnWrongGuess` for richer error signaling.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L263-L292】【F:Assets/Scripts/Audio/SfxPalette.cs†L35-L47】 
- Enhance voice grammar or multi-language support by extending `Grammar` and phrase mappings in `VoiceIntervalInput`.【F:Assets/Scripts/Audio/VoiceIntervalInput.cs†L33-L224】 
- Experiment with spawn patterns by altering `SpawnEnemy` (e.g., vertical layering or arcs tied to interval difficulty) to diversify engagements.【F:Assets/Scripts/Core/IntervalExerciseController.cs†L124-L158】 
- Surface detailed run analytics on the end screen by expanding `RunStats` consumption (accuracy graph, hit streak history).【F:Assets/Scripts/Core/RunStats.cs†L4-L13】【F:Assets/Scripts/UI/EndScreenController.cs†L35-L69】 

# Quick Setup Checklist

1. Place a `Turret` GameObject with `TurretController`, ensure the child camera is assigned to `cam`, and verify cursor lock/pitch limits suit the scene.【F:Assets/Scenes/IntervalTrainer.unity†L10681-L10732】【F:Assets/Scripts/Player/TurretController.cs†L8-L105】 
2. On the camera, add `IntervalQuizController`, `AudioSource`, `VoiceIntervalInput`, `ListenZoom`, and `CameraShake`; wire the turret reference, missile prefab/spawn, voice UI, and zoom volume as shown in the scene prefab.【F:Assets/Scenes/IntervalTrainer.unity†L2160-L2409】【F:Assets/Scripts/Player/IntervalQuizController.cs†L9-L317】【F:Assets/Scripts/Audio/VoiceIntervalInput.cs†L14-L224】 
3. Parent a `Muzzle` object with `MuzzleRecoil`, attach the missile spawn transform, and hook its `onKick` UnityEvent to a `StaffGlow` component on the staff mesh to drive emissive flashes.【F:Assets/Scenes/IntervalTrainer.unity†L607-L643】【F:Assets/Scenes/IntervalTrainer.unity†L1995-L2009】【F:Assets/Scripts/Player/StaffGlow.cs†L5-L94】 
4. Configure `IntervalExerciseController` on `GameRoot` with the enemy prefab, spawn ring, HP display, turret references, and end screen; confirm enabled intervals match your training goals.【F:Assets/Scenes/IntervalTrainer.unity†L8424-L8479】【F:Assets/Scripts/Core/IntervalExerciseController.cs†L12-L75】 
5. Populate the UI canvas with `UIHud`, `HP` (`HPDisplay`), `IntervalCarouselUI`, `VoiceUI` (with `MicLevelMeter`), and `EndScreenController`, wiring their serialized fields to the appropriate text, canvas groups, and buttons.【F:Assets/Scenes/IntervalTrainer.unity†L2560-L2604】【F:Assets/Scenes/IntervalTrainer.unity†L9549-L9656】【F:Assets/Scenes/IntervalTrainer.unity†L8848-L8981】【F:Assets/Scenes/IntervalTrainer.unity†L3340-L3360】 
6. Assign audio clips on `SfxPalette` for missile fire, explosions, wrong guesses, and player hits, or replace them with FMOD events as needed.【F:Assets/Scenes/IntervalTrainer.unity†L8603-L8636】【F:Assets/Scripts/Audio/SfxPalette.cs†L9-L77】 
7. Provide a `HomingMissile` prefab with the expected VFX, shield bubble, and light references to ensure both kill and dud flows render correctly.【F:Assets/Scripts/Enemies/HomingMissile.cs†L12-L229】 
8. Test voice input on target platforms, confirming microphone access, KeywordRecognizer availability, and UI feedback before relying on spoken answers for progression.【F:Assets/Scripts/Audio/VoiceIntervalInput.cs†L101-L224】【F:Assets/Scripts/UI/MicLevelMeter.cs†L59-L120】 
