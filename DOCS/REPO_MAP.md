# Repo Map

## Table of Contents
- [Top-Level Overview](#top-level-overview)
- [Assets Breakdown](#assets-breakdown)
  - [Scenes](#scenes)
  - [Scripts](#scripts)
- [What Not to Read First](#what-not-to-read-first)
- [Next Steps](#next-steps)

## Top-Level Overview

| Folder | Purpose | Notable Subfolders / Files |
| --- | --- | --- |
| `Assets/` | Unity project content: art, scenes, prefabs, code, and resources used at runtime. | See [Assets Breakdown](#assets-breakdown). |
| `DOCS/` | Project documentation and reference notes. | Existing docs such as `FILES.md`; this map lives here. |
| `FMOD/` | FMOD Studio project files for audio design and integration. | Root of FMOD banks and project structure. |
| `Packages/` | Unity package manifest and lock data controlling dependencies. | Managed by Unity; do not edit manually. |
| `ProjectSettings/` | Unity editor and player configuration assets. | Contains input, quality, and other project settings. |
| `YAML Scripts/` | Supplemental YAML-based scripting or configuration samples. | Useful for reference but not part of compiled runtime. |

## Assets Breakdown

| Subfolder | Purpose | Notable Contents |
| --- | --- | --- |
| `Art/` | Source art assets such as textures and sprites for UI and gameplay. | Organize visuals for in-game presentation. |
| `ListBox/` | Custom list box UI components and supporting assets. | Prefabs, scripts, or styles enabling list selection widgets. |
| `MidiPlayer/` | (Not present at top level) Placeholder for MIDI playback tooling mentioned in project context. | Confirm if stored under another path before use. |
| `Plugins/` | Third-party native plugins or managed libraries required by the project. | Drop-in vendor DLLs or frameworks. |
| `Prefabs/` | Reusable prefabbed game objects for scenes. | Group gameplay/UI building blocks. |
| `Resources/` | Assets loaded dynamically at runtime via `Resources.Load`. | Keep runtime-critical data accessible. |
| `Scenes/` | Scene definitions for different game states. | See [Scenes](#scenes). |
| `Scripts/` | C# gameplay and systems scripts. | See [Scripts](#scripts). |
| `Settings/` | ScriptableObject configuration assets controlling game tuning. | Stores gameplay, audio, or UI settings data. |
| `TextMesh Pro/` | Bundled TextMesh Pro assets (fonts, materials, examples). | Largely vendor-provided resources. |
| `InputSystem_Actions.inputactions` | Unity Input System action map asset. | Defines input bindings referenced in code. |

### Scenes

| Scene | Description |
| --- | --- |
| `IntervalTrainer.unity` | Interactive scene likely focused on interval ear-training exercises. |
| `MainMenu.unity` | Entry point menu for navigating to core modes. |
| `MelodicDictation.unity` | Gameplay scene for melodic dictation practice. |
| `LLM_Chat_Terminal.unity` | Sandbox for experimenting with LLM API assisted tasks. |

### Scripts

| Folder | Overview |
| --- | --- |
| `Audio/` | Handles music playback, sound effects, and integration with FMOD or MIDI systems. |
| `Core/` | Fundamental game systems such as managers, utilities, and shared infrastructure. |
| `Enemies/` | Logic for adversarial elements or challenge generators within practice modes. |
| `Environment/` | Controls environmental objects, scenery, or world interactions. |
| `FX/` | Visual effect controllers and particle orchestration. |
| `Player/` | Player avatar or user interaction handling, including input and progression. |
| `UI/` | User interface behaviour scripts for menus, HUDs, and widgets. |

## What Not to Read First

- `TextMesh Pro/` — vendor package content; reference only when editing fonts/materials.
- `Plugins/` — third-party binaries with little explanatory value.
- `ProjectSettings/` — Unity-generated settings assets; review only when adjusting project-wide configuration.
- `InputSystem_Actions.inputactions` — machine-authored asset best inspected through the Unity editor.

## Next Steps

- [ ] Draft scene briefs outlining goals, key prefabs, and entry points for each Unity scene.
- [ ] Map inspector wiring for critical prefabs and ScriptableObjects to clarify dependencies.
- [ ] Document audio signal flow between MIDI, FMOD, and in-game triggers.
- [ ] Create an event-flow diagram covering player input through gameplay feedback loops.
