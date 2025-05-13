# 1v1 Private Match Implementation Plan (UGS + NGO)

## 1. Introduction

This document outlines the plan to implement a 1-on-1 private match feature for the game "Boompath Bash" using Unity Gaming Services (UGS) and Netcode for GameObjects (NGO). The goal is to allow a player (Host) to create a private match, receive a join code, and share it with another player (Client) who can then use the code to join the match.

## 2. Core UGS Services & Packages

The following UGS services will be utilized. We will use the unified `com.unity.services.multiplayer` package, as this is the recommended approach.

- **Unity Authentication:**

  - **Purpose:** To uniquely identify players before they can create or join lobbies. Anonymous authentication will likely be sufficient for this simple private match scenario.
  - **Documentation:** [https://docs.unity.com/ugs/manual/authentication/manual/overview](https://docs.unity.com/ugs/manual/authentication/manual/overview)
  - **Package:** `com.unity.services.authentication` (included as dependencty of the unified `com.unity.services.multiplayer` package).

- **Unity Lobby:**

  - **Purpose:** To create a temporary, private "room" (lobby) for the two players. The host creates the lobby, which generates a discoverable join code. The client uses this code to find and join the specific lobby. The lobby will also store the Relay join code once the host has created a Relay allocation.
  - **Documentation:** [https://docs.unity.com/ugs/manual/lobby/manual/unity-lobby-service](https://docs.unity.com/ugs/manual/lobby/manual/unity-lobby-service)
  - **Package:** included in `com.unity.services.multiplayer`.

- **Unity Relay:**
  - **Purpose:** To facilitate the direct network connection between the two players once they are in the same lobby. Relay provides a secure and reliable way to connect players without them needing to expose IP addresses or configure port forwarding. NGO will use the Relay service to establish the peer-to-peer (P2P) or host-client connection.
  - **Documentation:** [https://docs.unity.com/ugs/manual/relay/manual/introduction](https://docs.unity.com/ugs/manual/relay/manual/introduction)
  - **Package:** included in `com.unity.services.multiplayer`

### 2.1. Why Use Lobby in Addition to Relay?

While the Relay service provides a join code necessary for the technical network connection, the Lobby service plays a crucial role in making the private match setup user-friendly and organized:

1.  **User-Friendly Join Codes:**

    - **Lobby Service:** Generates short, human-readable join codes (e.g., "BLUECAT", "73GTE"). These are easy for the host to share (e.g., verbally, via text message) and for the client to type in.
    - **Relay Service:** Its join code is a longer, more complex data string, primarily intended for programmatic use by the game clients to establish the actual network connection with NGO, not for direct manual sharing.

2.  **A "Meeting Place" & Pre-Game Coordination:**

    - The Lobby acts as a virtual waiting room or staging area before the direct game connection is established.
    - **Host:** Creates this "room" and gets the simple Lobby Join Code to share.
    - **Client:** Uses this Lobby Join Code to enter the "room."
    - **Inside the Lobby:**
      - You can display who has joined (e.g., "Player 1", "Player 2").
      - It's the ideal place to implement the ready-up system (e.g., "Player 1: Ready", "Player 2: Not Ready").
      - The host can securely pass the technical Relay Join Code to the client by storing it in the Lobby's shared data.

3.  **Decoupling Match Discovery from Direct Connection:**
    - **Match Discovery (Lobby):** The client uses the simple Lobby Join Code to _find and join the specific match session_ the host created.
    - **Establishing Game Connection (Relay):** Once both players are confirmed in the Lobby (and potentially ready), they then use the Relay Join Code (which the host placed in the Lobby's data, and the client retrieved) to establish the actual peer-to-peer network connection for gameplay via NGO and the Relay servers.

In summary, the Lobby service manages the user-facing aspects of creating, finding, and joining a private match, along with pre-game player coordination. The Relay service then handles the underlying technical networking once players are ready to connect for the game itself.

## 3. High-Level Connection Flow

### Player 1 (Host):

1.  **Initialize UGS & Authenticate:** Initialize all required UGS services (Core, Authentication, Lobby, Relay). Authenticate the player (e.g., anonymously).
2.  **Create Lobby:**
    - Use the Lobby service to create a new private lobby.
    - Specify `maxPlayers = 2`.
    - The Lobby service will return a unique Lobby ID and a human-readable Join Code for this lobby.
3.  **Display Lobby Join Code:** Show this Lobby Join Code to the Host player in the UI so they can share it.
4.  **Create Relay Allocation:**
    - Use the Relay service to create a Relay allocation (i.e., reserve space on a Relay server).
    - The Relay service will return a Relay Join Code (different from the Lobby Join Code). This code contains the information needed for the client to connect to the host via the Relay server.
5.  **Store Relay Join Code in Lobby:** Update the created Lobby's data to include this Relay Join Code. This makes it accessible to the Client once they join the lobby.
6.  **Start Host (NGO):** Call `NetworkManager.Singleton.StartHost()` using the Relay allocation data.
7.  **Wait for Client:** Monitor the lobby for the second player to join. Update UI to show connected players and their ready status.

### Player 2 (Client):

1.  **Initialize UGS & Authenticate:** Initialize all required UGS services. Authenticate the player.
2.  **Enter Lobby Join Code:** Player enters the Lobby Join Code received from the Host.
3.  **Join Lobby:** Use the Lobby service and the entered Lobby Join Code to find and join the host's lobby.
4.  **Retrieve Relay Join Code:** Once joined, retrieve the Relay Join Code from the lobby's data (put there by the Host).
5.  **Join Relay Allocation:** Use the Relay service and the Relay Join Code to connect to the host's Relay allocation.
6.  **Start Client (NGO):** Call `NetworkManager.Singleton.StartClient()` using the Relay allocation data.
7.  **Signal Ready:** Update UI to show connection and allow player to signal "Ready".

## 4. NGO (Netcode for GameObjects) Integration

- NGO will handle the actual gameplay data synchronization.
- UGS (Relay) provides the transport layer abstraction.
- The `UnityTransport` component in NGO will be configured to use the UGS Relay service.
- Host calls `NetworkManager.Singleton.StartHost(relayServerData)`.
- Client calls `NetworkManager.Singleton.StartClient(relayServerData)`.
- The `PrivateMatchManager` or a similar script will orchestrate the UGS steps and then hand off to NGO.

## 5. UI Elements (Private Match Screen)

- **Host View:**
  - Display area for the sharable Lobby Join Code.
  - List of current players in the lobby and their ready status (e.g., "Player 1: Ready", "Player 2: Waiting...").
  - A "Ready" button (or "Start Match" if both are ready and host controls start).
- **Client View:**
  - Input field for the Lobby Join Code.
  - "Join Match" button.
  - List of current players and their ready status.
  - A "I'm Ready" button.

## 6. Editor Setup / Initial Steps

1.  **Install UGS Packages:**
    - Open Package Manager (Window -> Package Manager).
    - Search for and install `com.unity.services.multiplayer`
2.  **Configure UGS in Project:**
    - Go to Edit -> Project Settings -> Services.
    - Link your Unity Project ID.
    - UGS services (Lobby, Relay) might require specific setup or enabling in the Unity Dashboard (cloud.unity.com) for your project.
3.  **Update `PrivateMatchManager.cs`:** This script will handle:
    - UGS Initialization and Authentication.
    - Lobby creation, joining, and management (sending/retrieving join codes, player data).
    - Relay allocation creation and joining.
    - Interfacing with the UI elements.
    - Starting NGO host or client.
4.  **Develop UI Scene/Prefabs:** Create the UI for the private match screen.
5.  **Implement Ready System:** Logic for players to signal readiness and for the game to start once all players are ready.

## 7. Architecture Considerations

- **`PrivateMatchManager.cs`:** This MonoBehaviour will be the central point for managing the private match setup flow. It should not be a Singleton if multiple instances are not an issue, but ensure it's easily accessible from UI elements.
- **UGS Wrapper/Helper (Optional but Recommended):** Consider creating simple wrapper functions or a separate static helper class for common UGS operations (e.g., `InitializeUGS`, `AuthenticatePlayerAsync`, `CreateLobbyAsync`, `JoinLobbyByCodeAsync`, `CreateRelayAsync`, `JoinRelayAsync`). This can keep `PrivateMatchManager.cs` cleaner.
- **Event-Driven UI Updates:** Use C# events or UnityEvents to decouple UI updates from the UGS logic. For example, when a player joins the lobby, `PrivateMatchManager` can invoke an event that the UI subscribes to, triggering a refresh of the player list.
- **Error Handling:** Implement robust error handling for all UGS calls (e.g., lobby not found, join code invalid, Relay allocation failed, network issues). Provide user-friendly feedback.
- **State Management:** The `PrivateMatchManager` will need to manage states like:
  - Idle
  - Initializing UGS
  - Authenticating
  - Creating Lobby / Waiting for Client to Enter Code
  - Joining Lobby
  - Setting up Relay
  - Waiting for Players Ready
  - Transitioning to Game Scene

## 8. What's Next (Focus on Connection)

1.  Implement UGS initialization and anonymous authentication.
2.  Implement Host flow: Create Lobby, get Lobby Join Code, display it.
3.  Implement Client flow: Enter Lobby Join Code, join Lobby.
4.  Implement Relay setup: Host creates Relay allocation and stores Relay Join Code in Lobby. Client retrieves it and joins Relay.
5.  Integrate with NGO: Host starts as host, Client starts as client using Relay data.
6.  Basic UI for displaying join code and player connection status.

This plan focuses on the connection aspect. The ready-up system and transition to the game scene will follow once players can reliably connect.
