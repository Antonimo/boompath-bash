# Networked Game Start Flow - Simplified Implementation

## System Components

### LobbyManager

- Manages UGS (Unity Game Services) lobby functionality
- Handles player authentication and lobby membership
- Manages player ready states via lobby data
- Implements countdown system using Unix timestamps in lobby data
- Broadcasts lobby state changes to subscribers

### PrivateMatchManager

- Orchestrates the match flow from lobby to game
- Subscribes to LobbyManager events
- Tracks `allPlayersReady` state to mirror true lobby state
- Detects `false → true` transitions to trigger countdown
- Calls GameManager.SetupAndLaunchGame() when countdown completes

### GameManager

- **Network-synced (Server controlled)**: Game states (WaitingForPlayers, Playing, GameOver), player NetworkObjects, bases, units
- **Client-only**: UI, interactions, camera, local co-op turn management (`currentPlayerIndex`)
- Inherits from NetworkBehaviour, always running
- Handles player spawning/despawning coordination
- **Simplified architecture**: Each client automatically initializes when detecting state transitions

### PlayerSpawnManager

- Manages network player object lifecycle
- Handles spawning/despawning of player NetworkObjects
- Maintains player assignment data (colors, teams, positions)

## Simplified Flow Sequence

### Initial Setup

1. Players join lobby → automatically marked as "not ready"
2. LobbyManager broadcasts player list with ready states
3. Players click ready button → lobby data updated
4. LobbyManager events trigger PrivateMatchManager.HandleLobbyStateUpdate()

### Countdown Trigger

1. PrivateMatchManager.HandleLobbyStateUpdate() always updates `allPlayersReady` to match lobby state
2. Detects transition from `false → true` (not all ready → all ready)
3. If host + transition detected → LobbyManager.StartGameCountdown() called
4. LobbyManager prevents duplicate countdowns (built-in protection)
5. LobbyManager sets CountdownStartTime in lobby data (Unix timestamp)
6. All clients receive lobby data change event
7. LobbyManager.Update() calculates remaining time and fires OnCountdownTick events

### Game Launch - Simplified Architecture

1. Countdown reaches zero → LobbyManager fires OnCountdownComplete event
2. PrivateMatchManager.HandleCountdownComplete() calls gameManager.SetupAndLaunchGame()
3. GameManager executes 3-phase setup:
   - Phase 1: Server despawns all existing players
   - Phase 2: Server waits for hierarchy cleanup verification
   - Phase 3: Server sends ClientRpc to all clients
4. Clients receive SetupAndLaunchGameClientRpc():
   - Clear local player list
   - Clear menu UI
   - Send RequestPlayerSpawnServerRpc to server
5. Server receives spawn requests → PlayerSpawnManager spawns new player objects
6. **When all players spawned → Server transitions directly to Playing state**
7. **All clients automatically detect Playing state transition and initialize themselves**

### Automatic Client Initialization

**Key Improvement**: No separate RPC needed for initialization!

1. **Server**: `ChangeGameState(GameState.Playing)` (NetworkVariable automatically syncs)
2. **All Clients**: Detect state change in `HandleGameStateTransition()`
3. **Each Client Automatically**:
   - Sets up local player reference
   - Resets local co-op turn management (`currentPlayerIndex = 0`)
   - Enables bases (script enabled state not network-synced)
   - Sets up camera to initial position
   - Clears lobby ready state
   - Starts local player turn flow

### State Synchronization Flow

**Network-Synced (Server Authority):**

- Game states: `WaitingForPlayers → Playing → GameOver`
- Player NetworkObjects, bases, units
- Winner determination

**Client-Only (Local Management):**

- UI states, camera, interactions
- Local co-op turn order (`currentPlayerIndex`)
- Player turn phases (`PlayerTurnPhase`)

**Ready State Reset (Game Start):**

- Each client automatically clears their ready state when entering Playing
- Lobby updates naturally through normal process
- Ready for rematch cycle

## Simplified Event Flow

```
Player clicks Ready
→ LobbyManager updates lobby data
→ LobbyManager fires OnLobbyStateBroadcast
→ PrivateMatchManager.HandleLobbyStateUpdate()
→ Updates allPlayersReady to match lobby state
→ If false→true transition detected: LobbyManager.StartGameCountdown()
→ LobbyManager sets lobby CountdownStartTime (with duplicate protection)
→ Countdown completes → OnCountdownComplete event
→ PrivateMatchManager calls gameManager.SetupAndLaunchGame()
→ GameManager 3-phase setup process
→ Players spawned → Server: ChangeGameState(Playing)
→ NetworkVariable automatically syncs to all clients
→ Each client detects state change → InitializeClientForPlayingState()
→ Game begins → Ready for next rematch cycle
```

## Architecture Benefits

1. **No separate RPC needed** - clients react to NetworkVariable state changes
2. **Works for returning players** - each client initializes based on current network state
3. **Cleaner separation** - network vs client responsibilities clearly defined
4. **Automatic synchronization** - NetworkVariable handles state distribution
5. **Simplified debugging** - fewer moving parts, easier to trace flow
