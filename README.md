# Unity + GitHub Starter Pack

This repo contains ready-to-use config to version-control a Unity project on GitHub using **Git + Git LFS** and **UnityYAMLMerge**.

## Quick Start
1. Create a **new Unity (3D)** project.
2. In Unity: **Edit → Project Settings**
   - **Version Control** → **Visible Meta Files**
   - **Asset Serialization** → **Force Text**
   - **Line Endings** → **Unix (LF)**
3. Quit Unity to flush settings.
4. Copy the files from this starter pack into the **project root** (the folder that contains `Assets/`, `Packages/`, `ProjectSettings/`).
5. In a terminal at the project root:
   ```bash
   git init
   git lfs install
   git add -A
   git commit -m "Add Unity + GitHub starter pack"
   # optional: set up remote
   # git branch -M main
   # git remote add origin <your-repo-url>
   # git push -u origin main
   ```
6. (One-time per machine) register Unity’s smart merge tool (see `scripts/`).

## What each file is

- **.gitignore**: Tells Git which files/folders to **ignore** (Unity caches, temp, IDE junk, build outputs). Keeps the repo clean and small.
- **.gitattributes**: 
  - Normalizes line endings for text files so teammates on different OSes don’t fight diffs.
  - Routes Unity YAML assets (`*.unity`, `*.prefab`, etc.) to **UnityYAMLMerge** for conflict resolution.
  - Sends large binary assets (art/audio/models/video/fonts) through **Git LFS** so they don’t bloat Git history.
- **.editorconfig**: Defines consistent **code formatting** (indentation, line endings, charset) for C# across editors/IDEs.
- **AI_README.md**: A short **project map** that orients AI coding assistants (and new teammates) to your folder layout and conventions.
- **scripts/setup-unityyamlmerge-*.***: Convenience scripts to configure Git to use Unity’s YAML merge driver on your machine.

## After cloning on a new machine
- Run `git lfs install` once.
- Run the appropriate `setup-unityyamlmerge` script to register the merge driver.

## Large File Storage (LFS)
We pattern-match common binary types to LFS. You can add more by extending the `*.ext filter=lfs` lines in `.gitattributes`.

## File locking (optional)
If artists are editing the same binary asset, enable LFS file locking:
```bash
git lfs install
git config lfs.https://github.com/<user>/<repo>.locksverify true
git lfs lock Assets/Art/character.fbx
git lfs unlock Assets/Art/character.fbx
```

## UnityYAMLMerge
Unity provides a merge tool that understands scenes/prefabs. The scripts below register it in your global Git config.
