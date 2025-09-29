# EarFPS (Interval Aim Trainer)

A small Unity FPS-style ear-training game. You aim a turret, listen to a target interval, and speak the interval name to fire a guided missile. Correct answers destroy the enemy; wrong answers fire a **dud** that fizzles harmlessly against the enemy’s shield.

## Highlights

- **Voice input** (Windows `KeywordRecognizer`). Push-to-talk: **V**.  
- **Two missile outcomes**  
  - **Kill**: normal explosion & destroy.  
  - **Dud**: identical flight; on impact, shield bubble flashes (enemy tint), missile fizzles (missile tint), no damage.
- **Player HP**: starts at 3. Bomb hits reduce HP. On 0 HP → death sequence → end screen.
- **End screen** overlay with stats and buttons (**Retry**, **Dashboard**).
- Lightweight, component-driven architecture; most flows are centralized in `GameManager`.

## How to Play

- **Aim** with mouse.  
- **Listen / Speak**: hold **V** (push-to-talk), speak an interval (“major sixth”, “perfect fifth”, etc.), release **V** (you can keep holding; recognition is threaded).  
- If recognized & correct → missile fires and destroys target. If wrong → dud missile fizzles on shield.

## Build & Run

- **Unity**: _fill in your version_ (e.g., 2022.3 LTS, Built-in RP).  
- **Target**: Windows x64 (voice recognition is Windows-only with `UnityEngine.Windows.Speech`).  
- Open `SampleScene` and press Play.

## Project Structure

- `Assets/Scripts/Core` — game flow (`GameManager`), scoring, HP, end screen, events (proposed).
- `Assets/Scripts/Player` — camera/aim, recoil, quiz & submission, voice input.
- `Assets/Scripts/Enemies` — enemy behaviour.
- `Assets/Scripts/UI` — HUD, Voice UI, End Screen.
- `Assets/Prefabs` — missiles, enemies, VFX (explosion, fizzle, shield bubble).
- `Assets/Audio` — SFX; `SfxPalette` helper.

## Status

- Voice “processing pill” **removed** (design decision).  
- Dud missile & shield bubble **implemented**.  
- End screen flow **implemented** (pause, cursor unlock, buttons wired).
- Known polish items in **AI_HINTS.md**.

## CHANGELOG

- Align `GameManager` stats counters with the HUD display data and removed unused fields to keep end-screen summaries accurate.
- Replaced Unity object null propagation with explicit checks to respect Unity's overloaded null semantics.
- Guarded the death coroutine so the end screen still appears if the manager is disabled when game over triggers.
