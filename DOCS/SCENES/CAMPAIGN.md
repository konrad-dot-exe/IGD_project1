## Melodic Dictation Campaign System — Implementation Summary

## Overview

Implemented a campaign system for the Melodic Dictation module. Players progress through 6 scale modes (Ionian, Mixolydian, Dorian, Aeolian, Phrygian, Lydian), each with 6 levels. Levels unlock sequentially; completing 3 levels in a node unlocks the next.

---

## Architecture Components

### 1. Data Layer (ScriptableObjects)

#### DictationLevel (`Assets/Scripts/Core/DictationLevel.cs`)

- Represents a single campaign level
- Fields:
  - `id`: Unique identifier
  - `title`: Display title
  - `intro`: Tutorial placeholder text
  - `profile`: Reference to DifficultyProfile
  - `useModeOverride`: Enable mode override
  - `modeOverride`: Scale mode override (Ionian, Mixolydian, etc.)
  - `roundsToWin`: Rounds required to complete (default: 3)
- Validation: Auto-generates ID from title; ensures `roundsToWin >= 1`

#### ModeNode (`Assets/Scripts/Core/ModeNode.cs`)

- Represents a mode node in the campaign map
- Fields:
  - `mode`: Scale mode (Ionian, Mixolydian, etc.)
  - `levels`: Array of 6 DictationLevel assets
- Methods:
  - `GetModeName()`: Returns mode display name
  - `IsValid()`: Validates all 6 levels are assigned

#### DictationCampaign (`Assets/Scripts/Core/DictationCampaign.cs`)

- Root campaign asset
- Fields:
  - `nodes`: Ordered array of ModeNode assets
  - `startNodeIndex`: Starting node index (default: 0)
- Methods:
  - `GetStartNode()`: Returns starting node
  - `IsValid()`: Validates campaign structure

#### CampaignSave (`Assets/Scripts/Core/CampaignSave.cs`)

- Serializable save data (JSON persistence)
- Structure:
  - `version`: Save file version
  - `nodes`: List of NodeSaveData (unlock state, level completion, win counts, top scores)
  - `lastNodeIndex`: Last played node index
  - `lastLevelIndex`: Last played level index
- NodeSaveData fields:
  - `unlocked`: Whether node is unlocked
  - `levels`: Completion flags for each level (6 levels)
  - `winsPerLevel`: Win count per level (runtime tracking)
  - `topScoresPerLevel`: Top score achieved per level (persistent)
- Methods:
  - `CreateDefault(int nodeCount)`: Creates default save (first node unlocked)
  - `IsNodeUnlocked(int nodeIndex)`: Checks if node is unlocked
  - `IsLevelComplete(int nodeIndex, int levelIndex)`: Checks if level is complete
  - `MarkLevelComplete(int nodeIndex, int levelIndex)`: Marks level as complete
  - `GetCompletedLevelCount(int nodeIndex)`: Returns completed level count
  - `UnlockNode(int nodeIndex)`: Unlocks a node
  - `GetNextIncompleteLevelIndex(int nodeIndex)`: Returns next incomplete level index
  - `GetTopScore(int nodeIndex, int levelIndex)`: Returns top score for a level
  - `SetTopScore(int nodeIndex, int levelIndex, int score)`: Updates top score if higher

---

### 2. Runtime Service Layer

#### CampaignService (`Assets/Scripts/Core/CampaignService.cs`)

- Singleton MonoBehaviour managing campaign state
- Features:
  - Save/load: JSON persistence to `Application.persistentDataPath`
  - Progression: Tracks current node/level, unlocks next node after 3 completions
  - Level management: Starts levels, validates unlocks, enforces order
  - Top score tracking: Updates and persists top scores per level
  - Camera intro sequence: Plays camera animation before level initialization
  - Debug methods: Reset progress, unlock all, auto-complete levels
- Key Methods:
  - `StartFromMap(int nodeIndex, int levelIndex)`: Starts a level from the map (coroutine)
    - Plays camera intro sequence (rotates from sky view to table view)
    - Displays level intro message during camera rotation
    - Waits for intro completion before initializing level
  - `RecordLevelWin()`: Records a win, checks completion, unlocks nodes, updates top score
  - `StartNextLevel()`: Starts the next level in the current node
  - `IsNodeUnlocked(int nodeIndex)`: Checks unlock status
  - `GetCurrentNodeNextLevel()`: Returns next incomplete level index (-1 if all complete)
- State Management:
  - Tracks `currentNodeIndex`, `currentLevelIndex`, `winsThisLevel`
  - Persists across scenes with `DontDestroyOnLoad`
  - Auto-saves on progression changes and top score updates

---

### 3. Difficulty System Extensions

#### DifficultyProfile Extension

- Added `MovementPolicy` enum:
  - `StepwiseOnly`: Only ±1 diatonic step
  - `UpToMaxLeap`: Allows leaps up to `maxLeapSteps`
- Added `maxLeapSteps` field (Range: 1-8):
  - 1 = stepwise only
  - 2 = thirds
  - 3 = fourths
  - ...
  - 8 = octaves
- Removed: `allowLeaps`, `MovementSet movement` (replaced by new system)

#### MelodyGenerator Updates (`Assets/Scripts/Core/Music/MelodyGenerator.cs`)

- Updated to use `MovementPolicy` and `maxLeapSteps`
- Methods:
  - `SetMovementPolicy(MovementPolicy policy)`: Sets movement policy
  - `SetMaxLeapSteps(int steps)`: Sets maximum leap steps
- Generation: Respects `maxLeapSteps` for diatonic movement within the current mode

#### DifficultyProfileApplier Updates (`Assets/Scripts/Core/DifficultyProfileApplier.cs`)

- Added mode override support:
  - `ApplyProfile(DifficultyProfile profile, ScaleMode? modeOverride = null)`
  - Uses override if provided; otherwise uses profile's `allowedModes`
- Applies movement policy and max leap steps to generator
- Updates keyboard opacity based on featured notes for the active mode

---

### 4. Controller Integration

#### MelodicDictationController Updates (`Assets/Scripts/Core/MelodicDictationController.cs`)

- Campaign mode support:
  - `isCampaignMode`: Tracks if in campaign mode
  - `winsRequired`: Rounds required for current level
  - `winsThisLevel`: Current win count
  - `levelJustCompleted`: Flag for endscreen display
  - `wrongGuessesThisLevel`: Wrong guesses per level (6 max, persists across rounds)
  - `maxWrongPerLevel`: Maximum wrong guesses before game over (default: 6)
- Methods:
  - `SetWinsRequiredForCampaign(int winsRequired)`: Sets wins required, resets stats, re-ignites candles
  - `DisableCampaignMode()`: Disables campaign mode
  - `StartRound()`: Public method for external calls
  - `Score`: Public property to access current score
- Win logic:
  - Increments `winsThisLevel` on round completion
  - Calls `CampaignService.RecordLevelWin()` when `winsThisLevel >= winsRequired`
  - Shows endscreen when level completes
  - Disables auto-advance in campaign mode
- Stats reset:
  - `score`, `roundsCompleted`, `runStartTime` reset when starting new level
  - `wrongGuessesThisLevel` resets when starting new level or restarting after game over
  - All stats reset when restarting after game over
- Start logic:
  - Checks for campaign map visibility before starting
  - Waits for level selection if map is visible
  - Handles campaign mode initialization
  - Re-ignites candles when starting new level

---

### 5. UI Components

#### CampaignMapView (`Assets/Scripts/UI/CampaignMapView.cs`)

- Displays campaign map with mode nodes
- Features:
  - Horizontal layout of mode nodes
  - Visual lock/unlock states (locked nodes show padlock icon)
  - Clickable unlocked nodes open level picker
  - Auto-refreshes unlock states
- Visual settings:
  - `unlockedColor`: Color for unlocked nodes
  - `lockedColor`: Color for locked nodes (50% opacity)
- Methods:
  - `Show()`: Shows the map view
  - `Hide()`: Hides the map view
  - `RefreshMap()`: Refreshes unlock states

#### CampaignLevelPicker (`Assets/Scripts/UI/CampaignLevelPicker.cs`)

- Displays 6 level tiles for selected mode node
- Features:
  - Shows completion status (checkmark for completed levels)
  - Enforces level order (only next incomplete level is clickable)
  - Visual states: Completed (green), Available (white), Locked (gray)
  - "Play" button (enabled only for next incomplete level)
  - "Replay" button text for completed levels
- Methods:
  - `ShowForNode(int nodeIndex)`: Shows levels for a node
  - `Hide()`: Hides the level picker

#### CampaignHUD (`Assets/Scripts/UI/CampaignHUD.cs`)

- Displays campaign info during gameplay
- Format: "Mode: {Name} — Level: {N}"
- Features:
  - Auto-updates based on campaign state
  - Hides when not in campaign mode (optional)
  - Updates in real-time via `Update()` method

#### EndScreenController Updates (`Assets/Scripts/UI/EndScreenController.cs`)

- Campaign mode support:
  - `ShowCampaign(int score, int roundsCompleted, float timeSeconds)`: Shows campaign endscreen
  - "Continue" button: Starts next level (hidden when all levels complete)
  - "Back to Map" button: Returns to campaign map or level picker based on completion status
  - Button state: Continue button only shown if next level exists
  - Level complete sound: Plays when level completes (not on game over)
- Navigation logic:
  - When all levels in node are complete: "Back to Map" navigates to campaign map
  - When levels remain: "Back to Map" navigates to level picker for current node
- Integration:
  - Auto-detects campaign mode
  - Shows appropriate buttons based on context
  - Displays "Level Score" and "Level Time" (per-level stats)

#### CampaignDebugPanel (`Assets/Scripts/UI/CampaignDebugPanel.cs`)

- Debug/testing panel
- Buttons:
  - Reset Progress: Resets save to default state
  - Unlock All Nodes: Unlocks all nodes
  - Auto Complete All Levels: Marks 3 levels per node as complete
- Settings:
  - `devOnly`: Show only in development builds

#### UnlockAnnouncementController Updates (`Assets/Scripts/UI/UnlockAnnouncementController.cs`)

- Extended to support mode completion announcements
- Methods:
  - `ShowCompletion(string modeName, ScaleMode? mode, System.Action onContinue)`: Shows "Mode Complete: [Mode Name]" announcement
  - Displays completion message before end screen when all levels in a node are finished
  - Uses same visual style as unlock announcements

---

### 6. Additional Features

#### Top Score Tracking

- Persistent top score per level (stored in `CampaignSave`)
- Updated automatically when level completes if current score is higher
- Keyed by `nodeIndex + levelIndex` for per-level tracking
- Ready for future UI display (not yet shown in UI)

#### Mode Completion Announcement

- Shows "Mode Complete" announcement when all 6 levels in a node are completed
- Uses `UnlockAnnouncementController` with completion variant
- Displays before end screen (similar to unlock announcement flow)
- Keyboard display shows the completed mode

#### Duplicate Melody Prevention

- Prevents same melody sequence two rounds in a row
- Implementation:
  - Stores previous melody in `MelodicDictationController`
  - Compares new melody with previous
  - Regenerates if duplicate (up to 6 attempts)

#### Card Slap Sound

- FMOD event plays when card drops onto table
- Implementation:
  - `CardController.cs`: Plays one-shot sound on collision with table
  - `hasPlayedSlapSound` flag prevents multiple plays
  - Debouncing prevents too many simultaneous sounds (max 1 per 50ms)

#### Keyboard Opacity Feature

- Non-featured keys use reduced opacity
- Implementation:
  - `PianoKeyboardUI.SetFeaturedNotes()`: Sets featured notes based on difficulty profile
  - `PianoKeyUI._baseOpacity`: Preserves opacity when keys are highlighted
  - Configurable opacity (default: 0.3 for non-featured keys)

#### Camera Intro Sequence

- Visual polish when starting a level from campaign map
- Implementation:
  - `CameraIntro.cs`: Manages camera rotation animation
  - Camera starts at -65° X rotation (pointing at sky)
  - Rotates down to 38° X rotation (pointing at table) over 1 second
  - Level intro message displays during rotation (e.g., "Mixolydian — Level 4")
  - Message fades in quickly, then fades out over rotation duration
  - Integrated into `CampaignService.StartFromMap()` coroutine
  - Plays before level initialization (candles, cards, etc.)

#### Camera Ambient Motion

- Subtle camera movement for visual polish
- Implementation:
  - `CameraAmbientMotion.cs`: Adds drift and breathing motion
  - Y-axis rotation drift (oscillating, 1-2° range)
  - Soft vertical "breathing" motion
  - Always active by default, toggleable
  - Automatically waits for camera intro to complete before initializing

---

## Data Flow

### Campaign Start Flow

1. Player opens game → CampaignService loads save file
2. CampaignMapView displays → Shows unlocked/locked nodes
3. Player clicks unlocked node → CampaignLevelPicker shows 6 levels
4. Player selects level → CampaignService.StartFromMap() called
5. Camera intro sequence → Camera rotates from sky to table view (1 second)
   - Level intro message displays during rotation
   - Message shows mode name and level number
6. Profile applied → DifficultyProfileApplier applies profile with mode override
7. Controller configured → MelodicDictationController set to campaign mode
8. Round starts → Player plays rounds until `winsRequired` reached
9. Level complete → CampaignService.RecordLevelWin() called
10. Endscreen shown → Continue/Back to Map buttons displayed
11. Next node unlocked → After 3 levels completed in node

### Save/Load Flow

1. Save file location: `Application.persistentDataPath/sonoria_campaign.json`
2. Auto-save: On level completion, node unlock, progress reset
3. Auto-load: On CampaignService.Awake()
4. Sync: Save data synced with campaign structure on load

---

## Key Design Decisions

1. ScriptableObject-based data: Campaign structure defined in assets, not code
2. JSON persistence: Simple, human-readable save format
3. Order enforcement: Players must complete levels in order
4. Mode override: Levels can override profile mode for flexibility
5. Replay support: Completed levels can be replayed
6. Singleton service: CampaignService persists across scenes
7. Reflection-based application: DifficultyProfileApplier uses reflection for flexibility

---

## File Structure

```
Assets/Scripts/
├── Core/
│   ├── DictationLevel.cs          # Level ScriptableObject
│   ├── ModeNode.cs                # Mode node ScriptableObject
│   ├── DictationCampaign.cs       # Campaign root ScriptableObject
│   ├── CampaignSave.cs            # Save data serialization
│   ├── CampaignService.cs         # Runtime campaign manager
│   ├── DifficultyProfile.cs       # Extended with MovementPolicy
│   ├── DifficultyProfileApplier.cs # Extended with mode override
│   └── MelodicDictationController.cs # Extended with campaign mode
├── UI/
│   ├── CampaignMapView.cs         # Campaign map UI
│   ├── CampaignLevelPicker.cs     # Level selection UI
│   ├── CampaignHUD.cs             # In-game HUD
│   ├── CampaignDebugPanel.cs      # Debug panel
│   └── EndScreenController.cs     # Extended with campaign support
└── Player/
    ├── CameraIntro.cs             # Camera rotation intro sequence
    └── CameraAmbientMotion.cs     # Subtle camera drift and breathing
```

---

## Testing Status

- Completed:

  - Map view displays correctly
  - Level picker shows levels
  - Level start and completion
  - Endscreen appears correctly
  - Node unlocking (3 levels → next node)
  - Save/load persistence
  - Mode override functionality
  - Replay completed levels

- Remaining:
  - CampaignHUD verification
  - CampaignDebugPanel testing
  - Full 6-mode campaign flow
  - Edge case testing

---

## Future Extension Points

1. Victory sequences: Hooks for custom victory animations
2. Tutorial system: `DictationLevel.intro` field ready for tutorial content
3. Analytics: Hooks for tracking progression
4. Multi-profile users: Save system supports versioning for future expansion
5. Additional modes: Easy to add more modes to the campaign
6. Level variants: Can create multiple level sets per mode

---

## Summary

The campaign system includes:

- 36 levels (6 modes × 6 levels) with progression rules
- Save/load with JSON persistence
- UI for map, level selection, HUD, and endscreen
- Mode override for flexible level configuration
- Movement system with configurable leap constraints
- Debug tools for testing
- Integration with existing Melodic Dictation systems

The system is modular, data-driven, and ready for authoring the remaining mode nodes and final QA testing.
