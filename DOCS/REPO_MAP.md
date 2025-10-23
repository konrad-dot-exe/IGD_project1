# Repo Map

## Table of Contents
- [Top-Level Overview](#top-level-overview)
- [Assets Breakdown](#assets-breakdown)
- [Scenes](#scenes)
- [Scripts Overview](#scripts-overview)
- [What Not to Read First](#what-not-to-read-first)
- [Next Steps](#next-steps)

## Top-Level Overview

| Folder | Purpose | Notable Subfolders / Files |
| --- | --- | --- |
| `Assets/` | Core Unity content for the project. | Art, ListBox, Plugins, Prefabs, Resources, Scenes, Scripts, Settings, `TextMesh Pro`, `InputSystem_Actions.inputactions`. |
| `FMOD/` | FMOD Studio project for adaptive audio and metadata integration. | `Metadata/`, `Sonoria.fspro`. |
| `Packages/` | Unity-managed package manifest and lock data (do not edit manually). | — |
| `ProjectSettings/` | Unity project configuration (rendering, input, platform settings). | — |
| `YAML Scripts/` | Helper scripts to set up UnityYAMLMerge for version control. | `setup-unityyamlmerge-mac.sh`, `setup-unityyamlmerge-win.ps1`. |

## Assets Breakdown

| Folder | Purpose | Notable Subfolders / Files |
| --- | --- | --- |
| `Assets/Art/` | Source art assets spanning audio, imagery, and materials. | Audio, Images, Materials, Models, `RT_Minimap.renderTexture`, Textures. |
| `Assets/ListBox/` | Custom list box UI component and editor tooling. | `Editor/`, `ListBox.cs`. |
| `Assets/MidiPlayer/` | *(Not present in this snapshot; likely expected to hold MIDI playback tooling—check history or external packages.)* | — |
| `Assets/Plugins/` | Third-party plugins bundled with the project. | `CSharpSynth/`. |
| `Assets/Prefabs/` | Reusable prefab game objects for gameplay and UI. | `EnemyShield.prefab`, `EnemyShip.prefab`, `ExplosionVFX.prefab`, `HomingMissile.prefab`, `Melodic Dictation/`, `MissileFizzle.prefab`, `WorldText.prefab`. |
| `Assets/Resources/` | Dynamically loaded assets at runtime, including sound banks and MIDI data. | `Analog Bank/`, `FM Bank/`, `GM Bank/`, `Midis/`. |
| `Assets/Scenes/` | Scene files defining playable and test environments. | `IntervalTrainer.unity`, `MelodicDictation.unity`, `SampleScene/`. |
| `Assets/Scripts/` | Gameplay and systems code organized by discipline. | Audio, Core, Enemies, Environment, FX, Player, UI. |
| `Assets/Settings/` | Rendering pipeline and volume profiles for URP configurations. | `DefaultVolumeProfile.asset`, `Mobile_RPAsset.asset`, `Mobile_Renderer.asset`, `PC_RPAsset.asset`, `PC_Renderer.asset`, `SampleSceneProfile.asset`, `UniversalRenderPipelineGlobalSettings.asset`. |
| `Assets/TextMesh Pro/` | Unity TextMesh Pro package assets (fonts, shaders, samples). | `Examples & Extras/`, Fonts, Resources, Shaders, Sprites. |

## Scenes

| Scene | Description |
| --- | --- |
| `Assets/Scenes/IntervalTrainer.unity` | Interval training gameplay scene—likely focuses on musical interval drills. |
| `Assets/Scenes/MelodicDictation.unity` | Melodic dictation scene for practicing transcription exercises. |
| `Assets/Scenes/SampleScene/` | Default Unity sample scene folder kept for reference or testing. |

## Scripts Overview

| Folder | Focus |
| --- | --- |
| `Assets/Scripts/Audio/` | Audio playback, synthesis, and integration logic for gameplay and training modes. |
| `Assets/Scripts/Core/` | Foundational systems such as game managers, state handling, and shared utilities. |
| `Assets/Scripts/Enemies/` | Enemy ship behaviors and AI challenges used in training scenarios. |
| `Assets/Scripts/Environment/` | World setup, level elements, and environmental interactions. |
| `Assets/Scripts/FX/` | Visual and audio effects triggers and controllers. |
| `Assets/Scripts/Player/` | Player controls, input handling, and character logic. |
| `Assets/Scripts/UI/` | User interface flow, HUD elements, and menu interactions. |

## What Not to Read First

- `Assets/TextMesh Pro/` – Vendor package content; stick to official docs unless customizing fonts or shaders.
- `Assets/Plugins/CSharpSynth/` – Third-party MIDI synthesis implementation; treat as black box unless debugging audio playback internals.
- `Packages/` and `ProjectSettings/` – Unity-generated configuration; review via Unity Editor rather than manual edits.
- `Assets/Resources/Analog Bank`, `FM Bank`, `GM Bank` – Large audio bank data with minimal explanatory context.
- `FMOD/` – Detailed audio authoring project; explore only when focusing on adaptive audio design.

## Next Steps

- [ ] Draft scene briefs summarizing goals, key prefabs, and entry points for each gameplay scene.
- [ ] Map inspector wiring for major managers (especially in `Assets/Scripts/Core`) to show serialized dependencies.
- [ ] Outline event-flow diagrams covering audio triggers between `CSharpSynth`/FMOD and gameplay scripts.
- [ ] Catalog critical prefabs with their scripts and configurable parameters for quick reference.
