# Player Turn Phases Architecture

## Overview

The Player Turn Phases system manages local, per-client player interactions during the global network-synced "Playing" state. This architecture separates global game flow (network-synced) from local player interactions (client-only) for better organization and performance.

## Key Architectural Concepts

### Network-Synced vs Local State

**Network-Synced GameState** (shared across all clients):

- `WaitingForPlayers`: Waiting for all players to connect and be ready
- `Playing`: Game is active, players can interact
- `GameOver`: Game has ended, no more interactions

**Local PlayerTurnPhase** (per-client only, during Playing state):

- `PlayerTurn`: Player can select units and interact with game
- `DrawingPath`: Player is drawing a path for a selected unit
- `PlayerTurnEnd`: Player turn is ending, transitioning to next player

### Key Design Principles

1. **Global states are network-synced** - all clients see the same GameState
2. **Player phases are local** - each client manages their own player interactions
3. **Phases only exist during Playing state** - phases are reset when leaving Playing
4. **Clean entry/exit handling** - proper component management on phase transitions
5. **Centralized reset system** - single method handles all phase-related cleanup

## State Flow Diagram

```
[WaitingForPlayers] → [Playing] → [GameOver]
                         ↓
                   [PlayerTurnPhase]
                         ↓
    [PlayerTurn] → [DrawingPath] → [PlayerTurnEnd] → [PlayerTurn] (next player)
         ↑                                              ↓
         └──────────────── [Cancel Path] ←──────────────┘
```

## Phase Management System

### Entry Points

**Entering Playing State:**

1. `InitializeClientForPlayingState()` calls `ResetAllPlayerTurnPhases()`
2. `InitializePlayingState()` calls `ChangePlayerTurnPhase(PlayerTurn)`
3. First player turn begins

**Phase Transitions:**

- **Unit Selection**: `SelectUnit()` → `ChangePlayerTurnPhase(DrawingPath)`
- **Path Confirmation**: `ConfirmPath()` → `ChangePlayerTurnPhase(PlayerTurnEnd)`
- **Path Cancellation**: `CancelPathDrawing()` → `ChangePlayerTurnPhase(PlayerTurn)`
- **Turn Complete**: `PlayerTurnEndTransitionSequence()` → `ChangePlayerTurnPhase(PlayerTurn)`

**Leaving Playing State:**

1. `HandleGameStateExit(Playing)` calls `ResetAllPlayerTurnPhases()`
2. All turn-related components disabled
3. Clean state for next game/rematch

### Component Management

**Phase Entry (handled by `ChangePlayerTurnPhase()`):**

1. Calls `HandlePlayerTurnPhaseExit(previousPhase)` to clean up
2. Updates `currentPlayerTurnPhase`
3. Calls appropriate `Start*Phase()` method to set up

**Phase Exit (handled by `HandlePlayerTurnPhaseExit()`):**

- `PlayerTurn` → Disables `PlayerTurn` component
- `DrawingPath` → Disables `PathDrawing` component
- `PlayerTurnEnd` → No specific cleanup needed

## Code Structure

### Core Methods

```csharp
// Phase Management
ChangePlayerTurnPhase(PlayerTurnPhase newPhase)    // Main phase transition method
HandlePlayerTurnPhaseExit(PlayerTurnPhase phase)   // Cleanup on phase exit
ResetAllPlayerTurnPhases()                         // Reset all phases and components

// Phase Entry Points
StartPlayerTurnPhase()     // Enable PlayerTurn, setup camera, etc.
StartPathDrawingPhase()    // Enable PathDrawing, switch camera
StartPlayerTurnEndPhase()  // Start turn end transition sequence

// State Management
HandleGameStateExit(GameState exitingState)       // Cleanup when leaving states
InitializeClientForPlayingState()                 // Setup when entering Playing
```

### Phase-Specific Behaviors

**PlayerTurn Phase:**

- Enables `PlayerTurn` component
- Sets `PlayerTurn.player = CurrentPlayer`
- Switches camera to current player's base
- Player can select units and interact with UI

**DrawingPath Phase:**

- Enables `PathDrawing` component
- Sets `PathDrawing.pathStartPosition` to selected unit position
- Switches to path drawing camera
- Player draws path for selected unit

**PlayerTurnEnd Phase:**

- All interaction components disabled (by exit handling)
- Runs `PlayerTurnEndTransitionSequence()` coroutine
- Switches camera back to current player base
- Waits for camera transition + 2 seconds
- Calls `StartNextPlayerTurn()` and returns to PlayerTurn phase

## Integration Points

### With NetworkBehaviour Components

The phases interact with several Unity components:

- **PlayerTurn**: Handles unit selection and player input
- **PathDrawing**: Handles path drawing interface and validation
- **CameraManager**: Switches between player base and path drawing cameras
- **Unit**: Selected unit for path drawing

### With Network Synchronization

While phases are local, they trigger network operations:

- **Path Confirmation**: Calls `CurrentPlayer.RequestUnitPathAssignment()` (ServerRpc)
- **Unit Updates**: Server updates unit states, synced to all clients
- **Game Over**: Server detects win conditions, changes global GameState

## Error Handling and Edge Cases

### Game State Interruption

- If GameState changes to GameOver during any phase, `HandleGameStateExit(Playing)` resets all phases
- Player interactions are cleanly stopped without leaving components in inconsistent state

### Path Drawing Cancellation

- ESC key during DrawingPath phase calls `CancelPathDrawing()`
- Properly exits DrawingPath phase and returns to PlayerTurn phase
- Selected unit is reset, path drawing disabled

### Player Disconnection

- Network handles player disconnection at GameState level
- Local phases are reset when returning to lobby or game over

## Local Co-op Support

The system supports multiple players on the same client:

- `currentPlayerIndex` tracks which local player's turn it is
- `StartNextPlayerTurn()` cycles through local players
- Each local player gets their own PlayerTurn → DrawingPath → PlayerTurnEnd cycle
- Camera switches to each player's base appropriately

## Performance Considerations

### Why Local Phases?

1. **Reduced Network Traffic**: No need to sync every UI interaction
2. **Responsive UI**: Local phase changes are immediate, no network latency
3. **Scalability**: Each client handles their own player interactions independently
4. **Offline Support**: Local co-op works without network connection

### Component Lifecycle

Components are enabled/disabled rather than created/destroyed:

- Better performance (no allocation/deallocation)
- Preserves component configuration
- Faster transitions between phases

## Future Enhancements

### Potential Network Synchronization

For enhanced multiplayer experience, phases could be optionally networked:

- Other players could see who is drawing paths
- Spectator mode could show current player's actions
- Turn timers could be enforced server-side

### PlayerInteractionManager

A separate `PlayerInteractionManager` could be created to handle:

- Input validation and filtering
- Complex interaction sequences
- Player-specific UI state management
- Advanced local co-op features

## Debugging and Monitoring

### Debug Logs

Enable `enableDebugLogs` in GameManager to see:

- Phase transitions: "Player turn phase changed: PlayerTurn -> DrawingPath"
- State entries: "Starting Player1's turn"
- Cleanup operations: "Resetting all player turn phases"

### Common Issues

1. **Components not disabled**: Check `HandlePlayerTurnPhaseExit()` implementation
2. **Phases persisting after game over**: Verify `HandleGameStateExit(Playing)` is called
3. **Camera not switching**: Ensure `CameraManager` is properly assigned
4. **Turn not advancing**: Check `StartNextPlayerTurn()` logic and player setup

## Summary

The Player Turn Phases architecture provides:

- Clean separation between global game state and local interactions
- Proper component lifecycle management
- Responsive local player experience
- Network efficiency through reduced synchronization
- Extensible foundation for future enhancements

This system maintains the simplicity of the original state machine pattern while adapting to the unified NetworkBehaviour architecture and supporting both networked multiplayer and local co-op gameplay.
