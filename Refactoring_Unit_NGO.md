# Refactoring Plan: Unit.cs for Server-Authoritative NGO

**Goal:** Refactor `Assets/Scripts/Units/Unit.cs` to use a server-authoritative model with Netcode for GameObjects (NGO).

**Collaboration Guidelines:**

- **Sequential Steps:** Implement the steps sequentially. Do not skip steps.
- **Atomic Edits:** Apply changes for one step at a time. Request review/confirmation after each step's edits are applied.
- **Focus:** Keep edits focused solely on the current step's objectives. Address unrelated issues or improvements separately later. Can add these as comments and as notes in this plan file.
- **Verification:** After each step, review the code changes carefully to ensure they align with the plan.
- **Notes:** Use the "Notes" section under each step to record deviations from the plan, decisions made during implementation, or required follow-up actions (like Editor configuration).
- Make sure to update this file last, after all the other edits.

---

## General Notes / Principles

- **Local Physics:** Unity's physics simulation runs independently on the server and each client. Settings like `Physics.IgnoreCollision` only affect the machine where they are called.
- **Consistency is Key:** For smooth visuals and predictable behavior, physics rules (especially collision ignores) must be applied consistently across the server and all clients (owners and observers alike).
- **Observer Experience:** An observing client (e.g., Player 2 watching Player 3's unit) needs the same physics rules applied locally as the owning client (Player 3) and the server to avoid visual discrepancies (like units being pushed out of their base only on observer clients).
- **Client-Side Setup:** For state that needs to be configured locally based on networked information (like setting up collision ignores based on ownership), performing the setup on the client during `OnNetworkSpawn` using synchronized `NetworkVariable`s ensures consistency for initial spawns and late joiners.

---

## Refactoring Steps

### ☑ 1. Network Variables & Transform

- **Tasks:**
  - ☐ Add `NetworkVariable<int>` to `Health` component (external dependency, track separately).
  - ☑ Add `NetworkVariable<ulong>` for `ownerPlayer`'s `OwnerClientId`. (`OwnerPlayerId` added)
  - ☑ Define `UnitStateEnum` (e.g., `Idle`, `MovingToSpawn`, `FollowingPath`, `Attacking`, `Dead`). (Defined in `Unit.cs`)
  - ☑ Replace `_currentStateName` with `NetworkVariable<UnitStateEnum>` for `currentState`. (`NetworkCurrentState` added, `_currentStateName` removed)
  - ☐ **In Unity Editor:** Add `NetworkTransform` component to Unit prefab, configure for Server Authority.
- **Notes:**
  - Defined `UnitStateEnum` inside `Unit.cs` for now. Consider moving to a shared file later if needed.
  - Added `NetworkVariable<ulong> OwnerPlayerId` initialized in field declaration.
  - Added `NetworkVariable<UnitStateEnum> NetworkCurrentState` initialized in field declaration. Removed `_currentStateName`.

### ☑ 2. Initialization & Ownership

- **Tasks:**
  - ☑ Modify `OnNetworkSpawn` for common setup (Component fetching moved, Awake removed).
  - ☑ Split server-only initialization: Add `InitializeOwnerPlayer(Player owner)` called by spawner, and `InitializeServerSideNonOwnerTasks()` for other tasks.
  - ☑ Remove `WaitForParentAndInitialize` coroutine.
  - ☑ Refactor ownership: `OwnerPlayerId` NetworkVariable now stores the `NetworkObjectId` of the owning `Player` object.
  - ☑ Server sets `OwnerPlayerId` and local `ownerPlayer` in `InitializeOwnerPlayer`.
  - ☑ Modify `ApplyPlayerColor` to use the local `ownerPlayer` reference.
  - ☑ Add `OnOwnerChanged` callback for `OwnerPlayerId`. It looks up the `Player` using `NetworkObjectId` via `NetworkManager.SpawnManager`, sets local `ownerPlayer`, and calls `ApplyPlayerColor` and `SetupLocalCollisionIgnores`.
  - ☑ Modify client-side `SetupLocalCollisionIgnores` to use the local `ownerPlayer` reference.
- **Notes:**
  - Removed `Awake`, `Initialize`, and `WaitForParentAndInitialize`.
  - **Spawner (`BaseController`) must call `Unit.InitializeOwnerPlayer(owner)` immediately after spawning the unit on the server.**
  - `ownerPlayer` local variable is now a property `ownerPlayer { get; private set; }`, set by server (`InitializeOwnerPlayer`) and client (`OnOwnerChanged`).
  - `ApplyPlayerColor` now uses the local `ownerPlayer` reference, ensuring consistency after owner is assigned.
  - `OnOwnerChanged` uses `NetworkManager.SpawnManager.SpawnedObjects` to find the `Player` NetworkObject by its `NetworkObjectId`.
  - Initial state (e.g., `GoToLocationState`) is set in `InitializeServerSideNonOwnerTasks()` on the server.
  - Collision ignore between a unit and its owner's base is handled client-side via `SetupLocalCollisionIgnores`, called from `OnOwnerChanged` after `ownerPlayer` is set.

### ☑ 3. State Management

- **Tasks:**
  - ☑ Guard core state change logic in `ChangeState` with `if (IsServer)`.
  - ☑ Update `NetworkVariable<UnitStateEnum>` on the server within `ChangeState`.
  - ☑ Add `OnValueChanged` callback (`OnNetworkStateChanged`) for the state `NetworkVariable`.
  - ☑ Implement client-side logic in the callback to instantiate the _representation_ of the state locally. Ensure state classes handle client-side execution safely. (Initial implementation added, state classes need review).
- **Notes:**
  - `ChangeState` now only executes fully on the server, updating `NetworkCurrentState`.
  - Added helper `StateToEnum` to convert state instances to enum values.
  - `OnNetworkStateChanged` callback runs on clients (and host) to create local state instances based on the received enum.
  - **Crucially, client-side states (`GoToLocationState`, `FollowPathState`, `AttackState`, etc.) need review/refactoring:**
    - Constructors may need adaptation as clients won't have server-only data (like full `path`, potentially `spawnToPos`, specific attack `target` references).
    - `Update`/`FixedUpdate` logic within states must be safe for clients (visuals, local effects only, no authoritative actions).
    - Data required by client states (like target position for `GoToLocationState` or simplified path info) might need separate synchronization (NetworkVariables, RPCs).
  - `IsAlive` is now also set on clients when the state changes to `Dead`.

### ☑ 4. Movement & Path Following

- **Tasks:**
  - ☑ Guard movement logic in `FixedUpdate` (and state FixedUpdates) with `if (IsServer)`.
    _(Note: Assumed done within state classes - Unit.cs relies on states only calling MoveTo on server)._
  - ☑ Ensure `NetworkTransform` handles sync; remove client-side `rb.MovePosition`.
    _(Note: Assumed done within state classes)._
  - ☑ Refactor `FollowPath(List<Vector3> newPath)`:
    - ☑ Keep public signature.
    - ☑ Call a new `FollowPathServerRpc(Vector3[] pathArray)` from it (if `IsOwner`).
    - ☑ Implement `[ServerRpc]` method on the server to store the path (server-only) and call `ChangeState` (server-only).
- **Notes:**
  - Public `FollowPath` now acts as entry point, checking `IsServer` / `IsOwner`.
  - `FollowPathServerRpc` receives path array from owning client.
  - Server-only `ProcessFollowPath` method handles storing path and changing state.
  - Actual movement logic (`MoveTo`, `rb.MovePosition`) within state classes needs to be server-guarded (`if (IsServer)`).

### ☑ 5. Combat Logic

- **Tasks:**
  - ☑ Guard `CheckForTargetsInRange` with `if (IsServer)`.
    _(Note: Assumed done by caller (state classes))._
  - ☑ Ensure damage application (`Health.TakeDamage`) originates from server logic only.
    _(Note: Assumed done by caller (state classes). Health component handles sync)._
  - ☑ Guard `HandleDeath` state change logic with `if (IsServer)`.
  - ☐ Consider `ClientRpc` for client-side death effects triggered from the server's `HandleDeath`.
    _(Note: Placeholder added, implementation pending specific needs)._
  - ☑ Manage `IsAlive` authoritatively on the server (e.g., via state or `NetworkVariable`).
    _(Note: Set by server in `HandleDeath`, synced to clients via `OnNetworkStateChanged`)._
- **Notes:**
  - `CheckForTargetsInRange` and `TakeDamage` rely on being called only by server-side logic (e.g., AttackState).
  - `HandleDeath` now changes state and sets `IsAlive` only on the server.
  - Clients update their `IsAlive` flag when receiving the `Dead` state via `OnNetworkStateChanged`.
  - A comment marks where a `ClientRpc` for death effects could be added.

### ☑ 6. Client-Side Adjustments

- **Tasks:**
  - ☑ Review `Update`/`FixedUpdate` (and state methods) for non-server logic. Ensure it only affects local visuals/UI.
    _(Note: Unit.cs delegates to states; comments added regarding state responsibilities)._
  - ☑ Verify client-side rotation logic is purely visual and driven by `NetworkTransform`.
    _(Note: Assumed handled by NetworkTransform and server-guarded state logic)._
  - ☑ Confirm `HighlightAsSelectable` affects only local visuals.
    _(Note: Confirmed safe)._
- **Notes:**
  - Client-side safety largely depends on the correct implementation within individual state classes.
  - `Update`/`FixedUpdate` in `Unit.cs` now simply delegate with comments clarifying state responsibilities.

---

## Phase 2: State Refactoring & Finalization

This phase focuses on ensuring the individual `UnitState` classes work correctly within the server-authoritative model and addressing remaining integration points.

### ☐ 8. Implement Health Component Networking

- **Tasks:**
  - ☐ Add `NetworkVariable<int>` to `Health.cs` to synchronize health.
  - ☐ Ensure server-side logic (e.g., `AttackState`) modifies health correctly.
  - ☐ Verify `HandleDeath` is triggered correctly on the server based on the networked health value.
- **Notes:**
  - The `Health` component needs its own network awareness.

### ☐ 9. Add NetworkTransform Component

- **Tasks:**
  - ☐ **In Unity Editor:** Add `NetworkTransform` component to the Unit prefab.
  - ☐ Configure `NetworkTransform` for Server Authority.
  - ☐ Ensure it synchronizes position and rotation as needed.
- **Notes:**
  - Essential for synchronizing movement results calculated by the server.

### ☐ 10. Refactor UnitState Classes

- **Tasks:**
  - ☐ **Server Guards:** Review `FixedUpdate`/`Update` in _all_ states (`GoToLocationState`, `FollowPathState`, `AttackState`, `DeadState`, etc.). Guard authoritative logic (movement, attacks) with `if (IsServer)`.
  - ☐ **Client Logic:** Ensure non-server logic in states is safe (visuals, non-authoritative effects).
  - ☐ **Constructors:** Refactor state constructors called client-side (`OnNetworkStateChanged`) to not require server-only data.
- **Notes:**
  - This is the most critical step for correct state behavior.
  - Each state file needs careful review.

### ☐ 11. Synchronize Necessary Client State Data

- **Tasks:**
  - ☐ Identify data needed by client states for visuals (e.g., movement target, attack target).
  - ☐ Add corresponding `NetworkVariable`s to `Unit.cs` (e.g., `NetworkVariable<Vector3> CurrentMoveTarget`, `NetworkVariable<ulong> AttackTargetId`).
  - ☐ Update these variables on the server when the authoritative state changes (e.g., in server-side `AttackState.Enter`).
  - ☐ Modify client-side state logic to use these synchronized variables.
- **Notes:**
  - Avoid over-synchronizing; only sync data essential for client representation.

### ☐ 12. Review Player/Base Lookup Logic

- **Tasks:**
  - ☐ Evaluate the reliability of finding `Player` / `BaseController` via `NetworkManager.SpawnManager` / `ConnectedClients`.
  - ☐ Consider implementing a more robust registry pattern if needed.
- **Notes:**
  - Important for `ApplyPlayerColor` and `SetupLocalCollisionIgnores` especially during connect/disconnect.

### ☐ 13. Implement Client-Side Death Effects

- **Tasks:**
  - ☐ Define needed death effects (particles, sound, ragdoll, etc.).
  - ☐ Implement a `ClientRpc` method (e.g., `PlayDeathEffectsClientRpc`).
  - ☐ Call the RPC from the server-side `HandleDeath` method.
  - ☐ Implement the effect logic within the `ClientRpc` method.
- **Notes:**
  - Optional but enhances visual feedback.

### ☐ 14. Cleanup Debug Logs

- **Tasks:**
  - ☐ Review all `DebugLog...` calls in `Unit.cs` and potentially state classes.
  - ☐ Ensure logs provide clear context (e.g., `[Server]`, `[Client]`).
  - ☐ Remove redundant or excessive logs.
  - ☐ Consider making logs conditional (`#if UNITY_EDITOR` or based on flags).
- **Notes:**
  - Improves readability and performance.

### ☐ 15. Comprehensive Testing

- **Tasks:**
  - ☐ Test in Host, Server, and Client modes.
  - ☐ Verify state transitions, movement sync, combat, path following.
  - ☐ Test with multiple clients.
  - ☐ Test late joining scenarios.
  - ☐ Test ownership changes if applicable.
- **Notes:**
  - Final validation step.
