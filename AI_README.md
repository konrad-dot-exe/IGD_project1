# AI Readme — Unity Project Map (Template)

_Purpose_: Give an AI assistant a fast mental model of this project so its suggestions land in the right files.

## Project Overview
- **Game name**: Sonoria
- **Unity version**: 6.0
- **Render pipeline**: URP
- **Entry scene**: `Assets/_Project/Scenes/_Boot.unity` (loads first playable scene)

## Folder Conventions
```
Assets/
  _Project/
    Scripts/
    Art/
    Audio/
    Prefabs/
    Scenes/
      _Boot.unity
      Dev/
      Prod/
    Settings/
  Plugins/           # third-party (prefer UPM)
  Resources/         # minimal; prefer Addressables where possible
Packages/            # UPM manifest & embedded packages
ProjectSettings/     # must be committed
```

## How to Extend
- **New gameplay system** → add a folder under `Scripts/Runtime/Systems/Foo/` with a public entry point `FooSystem`.
- **New component** → create a `MonoBehaviour` in `Scripts/Runtime/Components/` (one class per file).
- **Editor tools** → add into `Scripts/Editor/` guarded by `#if UNITY_EDITOR` if needed.
- **Tests** → put edit-mode and play-mode tests in `Scripts/Tests/` (NUnit via Test Runner).

## Coding Guidelines
- PascalCase for types, camelCase for locals/params, `_underscore` for private fields.
- Prefer ScriptableObjects for configuration.
- Keep classes small and cohesive. Write XML `///` summaries for public APIs.

## Scenes
- `_Boot.unity` initializes singletons/services, then loads the first level via Addressables/SceneManager.
- Dev scratch scenes live in `Scenes/Dev/`. Production scenes in `Scenes/Prod/`.


