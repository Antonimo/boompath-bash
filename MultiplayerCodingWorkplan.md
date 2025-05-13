# Multiplayer Implementation Workplan

## 1. Purpose

This document outlines the step-by-step coding tasks for implementing the 1v1 private match feature using UGS and NGO, as detailed in `MultiplayerPlan.md`. It is designed to facilitate a collaborative workflow where each step's code edits are implemented by the AI assistant and reviewed by the user before proceeding to the next.

## 2. Collaboration Guidelines

- **One Step at a Time:** We will implement and review one numbered step from this plan at a time.
- **AI Implementation:** The AI assistant will perform the code edits for the current step.
- **User Review:** The user will review the generated code edits for correctness, adherence to guidelines, and completeness for the current step.
- **Update Workplan:** After each step is completed and reviewed, this workplan file will be updated with:
  - A confirmation of completion (e.g., by checking the checkbox [v] or marking as DONE).
  - "Implementation Notes & Follow-up" for the completed step, detailing any deviations from the plan, decisions made during implementation, or necessary subsequent actions (e.g., manual Editor configurations, further testing notes).
- **This File Last:** This workplan file (`MultiplayerCodingWorkplan.md`) should be the last file updated in any given interaction, to accurately reflect the current status and capture notes related to the just-completed step.
- **Final Step - AI Responsibility:** After all code edits for a numbered step are completed and acknowledged by the user, the AI's _final action_ for that interaction will be to:
  1. Mark the step as complete (e.g., `[v]`).
  2. Fill in the "Implementation Notes & Follow-up" with concise, high-level insights:
     - Key architectural decisions or deviations from the plan.
     - Important considerations or dependencies for subsequent steps.
     - Any self-corrections made during the implementation of the current step.
     - **Avoid** simply restating code changes that are clear from the diff.
  3. The AI will confirm this workplan update before concluding its turn.

## 3. General Notes & Principles

- Refer to `MultiplayerPlan.md` for the overall architectural design, UGS service details, and high-level connection flow.
- Adhere to the coding guidelines (SRP, C# naming conventions, mobile optimization, minimizing garbage collection, descriptive naming, appropriate commenting) as outlined in the project's general instructions.
- Prioritize clear, robust, and testable code.
- Implement comprehensive error handling for all UGS and network operations, providing user-friendly feedback where possible.
- Consider using C# events or UnityEvents for decoupling UI updates from the UGS logic, as suggested in `MultiplayerPlan.md`.

## 4. Initial Setup (Manual Steps by User)

These steps are typically performed once by the user in the Unity Editor or Unity Dashboard.

- [v] **Action 4.1:** Install UGS Packages

  - In Unity Package Manager (Window -> Package Manager), search for and install `com.unity.services.multiplayer` if not already present. This unified package includes Authentication, Lobby, and Relay.
  - **Implementation Notes & Follow-up:**
    - _To be filled by the user after completion, noting the version installed or any issues._

- [v] **Action 4.2:** Configure UGS in Project Settings and Unity Dashboard
  - Go to Edit -> Project Settings -> Services.
  - Link your Unity Project ID if not already linked.
  - Navigate to the Unity Dashboard (cloud.unity.com) for your project.
  - Ensure the Authentication, Lobby, and Relay services are enabled and configured as needed (e.g., anonymous authentication enabled, lobby settings if any defaults need changing). Refer to UGS documentation for specifics.
  - **Implementation Notes & Follow-up:**
    - _To be filled by the user after completion, noting any specific configurations made or challenges encountered._

## 5. Core Logic Implementation (`PrivateMatchManager.cs`)

This section focuses on creating and populating `PrivateMatchManager.cs`. We will assume this script is attached to a GameObject in a relevant scene (e.g., a "MultiplayerMenu" scene).

- [v] **Step 5.1: Create `PrivateMatchManager.cs`; Implement UGS Initialization & Anonymous Authentication**

  - Create a new C# script named `PrivateMatchManager.cs` in an appropriate scripts folder (e.g., `Assets/Scripts/Multiplayer/`).
  - Add the basic `MonoBehaviour` structure.
  - Implement a method to initialize Unity Gaming Services (`UnityServices.InitializeAsync()`). This should typically be called once, perhaps in `Awake()` or `Start()`.
  - Implement a method for Anonymous Authentication (`AuthenticationService.Instance.SignInAnonymouslyAsync()`). This should be called after successful UGS initialization.
  - Include basic `try-catch` blocks for these asynchronous operations and log errors or success messages to the console (e.g., `Debug.Log`, `Debug.LogError`).
  - **Implementation Notes & Follow-up:**
    - Established the foundation for Unity Gaming Services (UGS) by integrating initialization and anonymous authentication into `PrivateMatchManager.cs` within `Awake()`. This centralized approach ensures UGS is ready before other operations.

- [v] **Step 5.2: Host - Lobby Creation**

  - In `PrivateMatchManager.cs`, add a public asynchronous method for the host to create a private lobby (e.g., `public async Task<string> CreateLobbyAsync(string lobbyName, bool isPrivate)`).
  - Use `LobbyService.Instance.CreateLobbyAsync()` with `maxPlayers = 2`.
  - The method should store relevant lobby data (e.g., Lobby ID, Lobby Code) internally or return the Lobby Code.
  - Expose the Lobby Join Code through a property or an event for the UI to display.
  - Add error handling and logging.
  - **Implementation Notes & Follow-up:**
    - Implemented host-side private lobby creation (`CreateLobbyAsync`) in `PrivateMatchManager.cs`. Key architectural points include storing the `_currentLobby` for subsequent operations (like Relay) and exposing the `LobbyCode` via a property and event for UI updates. The method was designed to be called by `PrivateMatchCreateController.cs`.

- [v] **Step 5.3: Client - Join Lobby by Code**

  - In `PrivateMatchManager.cs`, add a public asynchronous method for the client to join a lobby using a join code (e.g., `public async Task<bool> JoinLobbyByCodeAsync(string lobbyCode)`).
  - Use `LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode)`.
  - Store the joined Lobby object or relevant data internally.
  - The method should return a success/failure status.
  - Add error handling (e.g., lobby not found, invalid code) and logging.
  - **Implementation Notes & Follow-up:**
    - Implemented client-side lobby joining by code (`JoinLobbyByCodeAsync`) in `PrivateMatchManager.cs`. This method stores the joined `_currentLobby` and returns a boolean success status. This step laid the groundwork for the client's connection flow, which was further extended in Step 5.5 to include Relay joining.

- [v] **Step 5.4: Host - Relay Allocation & Storing Relay Join Code in Lobby**

  - In `PrivateMatchManager.cs`, after successful lobby creation by the host, implement a method to create a Relay allocation (e.g., `private async Task<string> AllocateRelayServerAndGetJoinCodeAsync()`).
  - Use `RelayService.Instance.CreateAllocationAsync(maxConnections)`, where `maxConnections` would typically be 1 for the joining client (total 2 players including host).
  - Get the Relay Join Code from the allocation. This is the technical code for NGO.
  - Update the created Lobby's data to include this Relay Join Code. Use `LobbyService.Instance.UpdateLobbyAsync(lobbyId, updateLobbyOptions)` where `updateLobbyOptions.Data` contains a dictionary with the Relay Join Code. Define a clear key for the Relay Join Code (e.g., `"RelayJoinCode"`).
  - Add error handling and logging.
  - **Implementation Notes & Follow-up:**
    - Integrated Relay server allocation for the host into `CreateLobbyAsync` via a new helper `AllocateRelayServerAndGetJoinCodeAsync`. A crucial decision was to store the Relay join code within the lobby's data using `UpdateLobbyAsync`, making it accessible to joining clients. If Relay allocation fails, `CreateLobbyAsync` now returns `null` to prevent an unusable lobby.
    - This process is part of the host's flow initiated by `PrivateMatchCreateController.cs`.

- [v] **Step 5.5: Client - Retrieve Relay Join Code from Lobby & Join Relay**

  - In `PrivateMatchManager.cs`, after a client successfully joins a lobby, implement a method to retrieve the Relay Join Code from the lobby's data (e.g., `private async Task<string> GetRelayJoinCodeFromLobbyAsync(Lobby joinedLobby)`).
  - Access `joinedLobby.Data["RelayJoinCode"].Value`.
  - Once the Relay Join Code is retrieved, use `RelayService.Instance.JoinAllocationAsync(joinCode)` to connect the client to the Relay server.
  - Store the Relay allocation data needed for NGO.
  - Add error handling (e.g., Relay Join Code not found in lobby data) and logging.
  - **Implementation Notes & Follow-up:**
    - The client's process of joining a Relay server is now integrated into the `JoinLobbyByCodeAsync` method. This ensures that a successful return from this method means the client is connected to both the lobby and the Relay.
    - A key decision was to handle Relay join failures within `JoinLobbyByCodeAsync` by clearing the `_currentLobby` reference and returning `false`. This treats the lobby and Relay join as a single atomic operation for the client, preventing an inconsistent state where a client is in a lobby but cannot participate in the Relay-based game.
    - The successfully retrieved `JoinAllocation` data (stored in `_joinAllocation`) is now ready for use in Step 5.6 (NGO Integration) to configure the client's network transport.

- [v] **Step 5.6: NGO Integration - Configure Transport and Start Host/Client**
  - Ensure you have the `UnityTransport` component accessible (e.g., on the same GameObject as `NetworkManager` or obtained via `NetworkManager.Singleton.NetworkConfig.NetworkTransport`).
  - In `PrivateMatchManager.cs`, add methods to:
    - For the Host: After creating the Relay allocation, get `RelayServerData` using the allocation details (host connection data). Configure `UnityTransport` with this data and call `NetworkManager.Singleton.StartHost(allocation)`.
    - For the Client: After joining the Relay allocation, get `RelayServerData` using the allocation details (client connection data). Configure `UnityTransport` with this data and call `NetworkManager.Singleton.StartClient(allocation)`.
  - This requires references to `Unity.Netcode.NetworkManager` and `Unity.Networking.Transport.Relay.UnityRelayTransport`. You may need to add `using Unity.Services.Relay.Models;` for `JoinAllocation`.
  - **Implementation Notes & Follow-up:**
    - NGO transport configuration and host/client startup logic implemented in `PrivateMatchManager.cs` (`StartHostWithRelayAsync`, `StartClientWithRelayAsync`).
    - `RelayServerData` is configured using `AllocationUtils.ToRelayServerData` with the "dtls" connection type, intended for the `com.unity.services.multiplayer` package.
    - **Current Issue:** A persistent linter error: `The type or namespace name 'Utils' does not exist in the namespace 'Unity.Services.Multiplayer'` blocks this approach.
    - **Follow-up Needed:** Resolve the linter error by investigating the `com.unity.services.multiplayer` package installation and version. If `AllocationUtils` is confirmed unavailable for the project's setup, an alternative method for `RelayServerData` construction compatible with existing packages must be used. The user's suggestion to document package versions for future context is noted.

## 6. UI Implementation for Match Creation and Lobby

These steps involve creating UI elements and scripts to manage the flow from creating a private match to players being in a shared lobby.

- [v] **Step 6.1: Create `PrivateMatchCreateController.cs` for Host Flow Initiation**

  - Create a new Unity scene or UI Panel named "PrivateMatchCreate" (or similar).
  - Create a new C# script `PrivateMatchCreateController.cs`.
  - This script will be responsible for:
    - Being activated when the user decides to create a private match.
    - On `OnEnable()` or a button press, it should call `PrivateMatchManager.Instance.CreateLobbyAsync()`.
    - Displaying status messages like "Creating your match..."
    - Upon successful lobby and Relay allocation (indicated by `CreateLobbyAsync` completing successfully and perhaps an event from `PrivateMatchManager`), it should trigger navigation to the "PrivateMatchLobbyShared" view/scene.
    - Handling and displaying any errors during the creation process.
  - **Implementation Notes & Follow-up:**
    - `PrivateMatchCreateController.cs` was implemented to initiate the host's private match creation flow.
    - Key architectural decisions:
      - The lobby creation process is automatically triggered in `OnEnable()`.
      - It relies on `PrivateMatchManager.Instance` for UGS and Relay operations.
      - UI status updates are managed through a `TextMeshProUGUI` component (`statusText`).
      - Navigation to the shared lobby view (`privateMatchLobbyPanel`) upon success is handled by `MenuManager.OpenMenuPanel()`.
      - Basic error handling and logging are included.
    - The script fulfills the requirements of initiating the host flow and transitioning to the lobby view.

- [ ] **Step 6.2: Create `PrivateMatchLobbyController.cs` for Host and Client**

  - Create a new Unity scene or UI Panel named "PrivateMatchLobbyShared" (or similar).
  - Create a new C# script `PrivateMatchLobbyController.cs`.
  - This script will be responsible for:
    - Displaying the Lobby Join Code (e.g., for the host to share).
    - Allowing the host to copy the Lobby Join Code.
    - For clients, this view will be shown after successfully joining a lobby via code.
    - Displaying a list of connected players (initially host and one joining client).
    - Displaying the ready status of each player.
    - A button "I'm Ready" / "Cancel Ready" for each player to toggle their own ready state.
    - Displaying status messages (e.g., "Waiting for players...", "Opponent Ready", "Match starting...").
    - Some UI elements might be visible/interactive only for the host or the local player (e.g., "Start Game" button for host, only local player can click their own ready button).
  - Link this script to UI elements (Text Fields for lobby code, player list, status; Buttons for ready, copy code).
  - `PrivateMatchManager` will need to expose events or properties for player list updates, ready status changes, and the lobby join code.
  - **Implementation Notes & Follow-up:**
    - _To be filled after AI implementation and user review. This replaces the old `PrivateMatchLobbyController.cs` functionality for the host post-creation, and provides the client's lobby view._

- [ ] **Step 6.3: Implement Basic Navigation**

  - Implement the logic to navigate the user:
    - From the main menu (or wherever "Create Private Match" is selected) to the "PrivateMatchCreate" view.
    - From "PrivateMatchCreate" view to "PrivateMatchLobbyShared" view upon successful lobby creation (for host).
    - From the UI where a client enters a join code to "PrivateMatchLobbyShared" view upon successful lobby join (for client).
  - This might involve a simple scene manager or a UI panel manager.
  - **Implementation Notes & Follow-up:**
    - _To be filled after AI implementation and user review._

- [ ] **Step 6.4: Client - UI for Entering Join Code**
  - Create UI elements for the client flow (likely on a "JoinMatch" panel/view):
    - Input Field: For the client to enter the Lobby Join Code.
    - Button: "Join Match" (triggers `PrivateMatchManager.Instance.JoinLobbyByCodeAsync()`).
    - Text Field: Status messages (e.g., "Joining Lobby...", "Connected to Lobby", "Failed to Join").
  - Write a UI script (e.g., `JoinMatchViewController.cs`) to link these elements to `PrivateMatchManager`.
  - Upon successful join, navigate to the `PrivateMatchLobbyController`.
  - **Implementation Notes & Follow-up:**
    - _To be filled after AI implementation and user review._

## 7. Future Steps (Beyond Initial Connection)

This section lists items from `MultiplayerPlan.md` that are subsequent to establishing the basic connection.

- Implement a robust ready-up system within the lobby (the "I'm Ready" button in `PrivateMatchLobbyController.cs` is the start of this).
- Handle transition from the lobby/matchmaking scene to the actual game scene.
- Implement full error handling with user-friendly feedback for all network operations.

---

_This workplan is a living document. We will update it together as we progress._
