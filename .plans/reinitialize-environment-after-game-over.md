# Reinitialize Environment After Game Over

## Overview

After a game over, when the player clicks "Retry" (both campaign and free-play mode), the environment (candles and drone sound) should be reinitialized without reloading the scene. Currently, candles remain extinguished and the drone doesn't restart.

**Important Notes:**

- Game over means the player failed - they can only retry the same level or go back to level selection
- "Continue" button should NOT appear after game over (only after level completion)
- Reinitialization is only needed after game over (candles are extinguished, drone is stopped)
- On level completion, candles stay lit and drone continues, so no reinitialization needed

## Goals

1. Reignite all candles with fade-in transition when restarting after game over
2. Restart DronePlayer from the beginning when restarting after game over
3. Apply to "Retry" button in both campaign mode and free-play mode after game over
4. Avoid reloading the scene - restart in-place
5. Fix campaign mode to show "Retry" button (not "Continue") after game over

## Implementation Plan

### 1. Add Environment Reinitialization Method to MelodicDictationController

**File**: `Assets/Scripts/Core/MelodicDictationController.cs`

- Add `ReinitializeEnvironment()` method:
  - Reignite all candles in the `candles` list using `CandleFlicker.Ignite()` (uses default fade-in)
  - Restart `DronePlayer` by calling `Stop()` then `Play()` (or disable/enable GameObject to trigger OnEnable)
  - Log reinitialization for debugging

### 2. Add Flag to Track Game Over State

**File**: `Assets/Scripts/Core/MelodicDictationController.cs`

- Add private bool field `_gameOverOccurred` to track if game over happened
- Set `_gameOverOccurred = true` in `GameOver()` method
- Reset `_gameOverOccurred = false` when starting a new round after game over
- This flag determines if environment reinitialization is needed

### 3. Modify StartRound() to Reinitialize Environment After Game Over

**File**: `Assets/Scripts/Core/MelodicDictationController.cs`

- Check `_gameOverOccurred` flag at the start of `StartRoundInternal()`
- If `_gameOverOccurred == true`, call `ReinitializeEnvironment()` before starting the round
- Reset `_gameOverOccurred = false` after reinitialization
- If `_gameOverOccurred == false` (normal start), skip reinitialization (candles are already lit, drone is already playing)

### 4. Fix Campaign Mode Game Over Button Display

**File**: `Assets/Scripts/UI/EndScreenController.cs`

- Modify `ShowDictation()` to check if it's a game over (titleOverride == "Game Over" or similar)
- When showing game over in campaign mode:
  - Hide "Continue" button (player failed, cannot proceed to next level)
  - Show "Retry" button (retry same level)
  - Show "Back to Map" button (return to level picker)
- When showing level completion in campaign mode:
  - Show "Continue" button (if next level exists)
  - Show "Back to Map" button
  - Hide "Retry" button

### 5. Modify EndScreenController Retry Button

**File**: `Assets/Scripts/UI/EndScreenController.cs`

- Change `OnClickRetry()` to:
  - Find `MelodicDictationController` in the scene
  - Call `StartRound()` on it (which will trigger environment reinitialization if game over occurred)
  - Hide the end screen
  - Do NOT reload the scene
- Keep scene reload as fallback if controller is not found
- This works for both campaign mode and free-play mode

### 6. Ensure Campaign Mode Level Start Doesn't Reinitialize

**File**: `Assets/Scripts/Core/CampaignService.cs`

- Verify that `StartFromMap()` → `dictationController.StartRound()` flow does NOT trigger reinitialization
- `_gameOverOccurred` should be `false` when starting a level from map, so reinitialization is skipped
- No changes needed if flag-based approach is implemented correctly

### 7. Handle DronePlayer Restart

**File**: `Assets/Scripts/Audio/DronePlayer.cs` (if needed)

- In `ReinitializeEnvironment()`, restart DronePlayer:
  - Call `dronePlayer.Stop()` first (stops and releases FMOD instance)
  - Then disable and enable the GameObject to trigger `OnEnable()`, which creates new instance and starts playing
  - OR: Add a `Restart()` method to DronePlayer that handles stop + start cleanly
- DronePlayer's `OnEnable()` automatically starts the drone, so disable/enable approach should work

## Technical Details

### Candle Reinitialization

- Use `CandleFlicker.Ignite()` with default `riseSeconds` (0.30f) for fade-in effect
- Safe to call even if candle is already lit (method handles this)
- Call on all candles in the `candles` list

### DronePlayer Restart

- Option 1: Call `Stop()` then `Play()` (if Play() works when stopped)
- Option 2: Disable then enable GameObject to trigger `OnEnable()` which starts the drone
- Option 3: Add `Restart()` method to DronePlayer that stops and starts

### Timing

- Reinitialize environment at the start of `StartRound()` ONLY if `_gameOverOccurred == true`
- This ensures environment is ready before the round begins
- On normal level start (from map), skip reinitialization since environment is already active

## Files to Modify

1. `Assets/Scripts/Core/MelodicDictationController.cs`

   - Add `ReinitializeEnvironment()` method
   - Call it from `StartRound()` or `StartRoundInternal()`

2. `Assets/Scripts/UI/EndScreenController.cs`

   - Modify `OnClickRetry()` to restart without reloading scene

3. `Assets/Scripts/Audio/DronePlayer.cs` (optional)
   - Add `Restart()` method if needed for cleaner restart logic

## Testing Considerations

- Test "Retry" button in campaign mode after game over (should retry same level, not go to next)
- Test "Retry" button in free-play mode after game over
- Verify "Continue" button does NOT appear after game over in campaign mode
- Verify "Continue" button DOES appear after level completion in campaign mode
- Verify candles fade in smoothly when retrying after game over
- Verify drone sound restarts and plays correctly when retrying after game over
- Verify scene does not reload (check that state persists)
- Verify environment does NOT reinitialize when starting level from map (candles stay lit, drone continues)
- Test multiple retries in a row
- Test both game over scenarios (timeout and wrong guesses)
- Test level completion to ensure environment stays active (no reinitialization)
