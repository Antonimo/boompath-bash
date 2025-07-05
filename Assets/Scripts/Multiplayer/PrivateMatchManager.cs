using System;
using UnityEngine;
using System.Threading.Tasks;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections;
// TODO: refactor to host stuff separate and client stuff separate?

// TODO: refactor Relay to its own service?
//
/// <summary>
/// PrivateMatchManager is a HIGH-LEVEL component responsible for match orchestration and setup.
/// 
/// Key Responsibilities:
/// - Match lifecycle: Creating, joining, and leaving private matches
/// - Lobby management: Coordinating player readiness, countdown, and match initiation
/// - Network setup: Relay allocation, host/client network startup coordination
/// - Game mode and team configuration management
/// - Transition orchestration: Managing the complete flow from lobby to active game
/// 
/// Architecture Position:
/// - This is the TOP-LAYER component that orchestrates the entire match experience
/// - Controls and coordinates lower-level managers (NetworkGameManager, HostStartupManager)
/// - Handles high-level networking setup (Relay, Lobby) and coordination
/// - Manages the transition from menu/lobby phase to active gameplay phase
/// - Responsible for cleanup and error handling across the entire match lifecycle
/// 
/// Separation of Concerns:
/// - PrivateMatchManager handles "how matches are set up and coordinated" (lobbies, networking, transitions)
/// - NetworkGameManager handles "how the game is networked" (network sync, distributed game flow)
/// - GameManager handles "what happens in the game" (local game mechanics, player interactions)
/// 
/// Network Flow Coordination:
/// - Orchestrates the complete flow: Lobby → Network Setup → Game Transition → Active Gameplay
/// - Ensures proper sequencing of network operations (Relay, player spawning, game initialization)
/// - Coordinates between multiple network-aware systems during transitions
/// 
/// Race Condition Solution (See PROJECT_DECISIONS.md):
/// - HandleLobbyStateUpdate: Definitive countdown decision point based on actual state
/// - HandlePlayerReadyStateChanged: Smart event handling that requests current state instead of aggressive cancellation
/// - Maintains immediate UI feedback while preventing false countdown cancellations from async UGS events
/// </summary>
public class PrivateMatchManager : MonoBehaviour
{
    [SerializeField] private HostStartupManager hostStartupManager;
    [SerializeField] private GameManager gameManager;

    // TODO: broeadcast state updates such as lobby deleted / disconnected / etc ?

    // Relay allocation data
    // TODO: relay manager?
    private string _relayJoinCode;
    private Allocation _allocation; // For host's Relay allocation
    private JoinAllocation _joinAllocation; // For client's Relay allocation

    [SerializeField] private GameMode selectedGameMode;
    [SerializeField] private TeamSize selectedTeamSize;

    // Track all players ready state to detect transitions
    // TODO: in the adge case that a player re-joins? or something else that would trigger allPlayersReady while already in game?
    [SerializeField] private bool allPlayersReady = false;

    private void ValidateDependencies()
    {
        if (hostStartupManager == null)
        {
            Debug.LogError("PrivateMatchManager: HostStartupManager not found in the scene!");
            enabled = false;
        }

        if (gameManager == null)
        {
            Debug.LogError("PrivateMatchManager: GameManager not found in the scene!");
            enabled = false;
        }

        if (LobbyManager.Instance == null)
        {
            Debug.LogError("PrivateMatchManager: LobbyService instance not found!");
            enabled = false;
        }
    }

    private async void Awake()
    {
        ValidateDependencies();
        if (!this.enabled) return;

        // Wait for LobbyManager to be ready (it handles UGS initialization)
        await WaitForLobbyManagerReady();

        // Subscribe to lobby events from the centralized service
        LobbyManager.OnLobbyStateBroadcast += HandleLobbyStateUpdate;
        LobbyManager.OnLobbyDeleted += HandleLobbyDeleted;
        LobbyManager.OnPlayerLeft += HandlePlayerLeft;
        LobbyManager.OnPlayerReadyStateChanged += HandlePlayerReadyStateChanged;
        LobbyManager.OnCountdownComplete += HandleCountdownComplete;
    }

    private async Task WaitForLobbyManagerReady()
    {
        // Wait for LobbyManager to be ready (it handles all UGS initialization)
        while (LobbyManager.Instance == null || string.IsNullOrEmpty(LobbyManager.Instance.LocalPlayerId))
        {
            await Task.Delay(100); // Wait 100ms before checking again
            // TODO: timeout?
        }
        Debug.Log("PrivateMatchManager: LobbyManager is ready.");
    }

    public void SelectGameMode(string selectedMode)
    {
        Debug.Log($"Selected Game Mode: {selectedMode}");

        try
        {
            selectedGameMode = (GameMode)Enum.Parse(typeof(GameMode), selectedMode);
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"Invalid game mode string: '{selectedMode}'. Error: {e.Message}");
        }
    }

    public void SelectTeamSize(string selectedSize)
    {
        Debug.Log($"Selected Team Size: {selectedSize}");

        try
        {
            selectedTeamSize = (TeamSize)Enum.Parse(typeof(TeamSize), selectedSize);
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"Invalid team size string: '{selectedSize}'. Error: {e.Message}");
        }
    }

    // TODO: where is this used?
    public async Task<string> CreateLobbyAsync(string lobbyName, bool isPrivate)
    {
        if (string.IsNullOrEmpty(lobbyName))
        {
            lobbyName = "My Private Match"; // Default lobby name
        }

        try
        {
            // Create lobby using the centralized service
            string lobbyCode = await LobbyManager.Instance.CreateLobbyAsync(lobbyName, isPrivate);
            if (string.IsNullOrEmpty(lobbyCode))
            {
                return null;
            }

            // Allocate Relay server for the host
            _relayJoinCode = await AllocateRelayServerAndGetJoinCodeAsync();
            if (string.IsNullOrEmpty(_relayJoinCode) || _allocation == null)
            {
                Debug.LogError("Relay allocation failed. Cleaning up lobby.");
                // TODO: CleanUpLobbyAndRelayOnErrorAsync? Cleanup Relay?
                await LobbyManager.Instance.LeaveLobbyAsync();
                return null;
            }

            // Start the host with Relay
            if (!StartHostWithRelay(_allocation))
            {
                Debug.LogError("Failed to start host with Relay. Cleaning up.");
                // TODO: CleanUpLobbyAndRelayOnErrorAsync? Cleanup Relay?
                await LobbyManager.Instance.LeaveLobbyAsync();
                return null;
            }

            Debug.Log($"Successfully created lobby and started host. Lobby Code: {lobbyCode}");
            return lobbyCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create lobby: {e.Message}");
            return null;
        }
    }

    // TODO: on any lobby error -> cleanup relay

    public async Task<bool> JoinLobbyByCodeAsync(string lobbyCode)
    {
        try
        {
            // Join lobby using the centralized service
            bool joinSuccess = await LobbyManager.Instance.JoinLobbyByCodeAsync(lobbyCode);
            if (!joinSuccess)
            {
                return false;
            }

            // Join Relay server as client
            _joinAllocation = await JoinRelayAndGetClientAllocationAsync();
            if (_joinAllocation == null)
            {
                Debug.LogError("Failed to join Relay server. Leaving lobby.");
                await LobbyManager.Instance.LeaveLobbyAsync();
                return false;
            }

            // Start the client with Relay
            if (!StartClientWithRelay(_joinAllocation))
            {
                Debug.LogError("Failed to start client with Relay. Leaving lobby.");
                await LobbyManager.Instance.LeaveLobbyAsync();
                return false;
            }

            Debug.Log("Successfully joined lobby and started client.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
            return false;
        }
    }

    private async Task<string> AllocateRelayServerAndGetJoinCodeAsync()
    {
        try
        {
            _allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(_allocation.AllocationId);

            Debug.Log($"Relay server allocated. Join Code: {relayJoinCode}");

            // Update lobby with Relay join code using the centralized service
            var relayData = new Dictionary<string, Unity.Services.Lobbies.Models.DataObject>
            {
                {
                    "RelayJoinCode", new Unity.Services.Lobbies.Models.DataObject(
                        visibility: Unity.Services.Lobbies.Models.DataObject.VisibilityOptions.Member,
                        value: relayJoinCode)
                }
            };

            await LobbyManager.Instance.UpdateLobbyDataAsync(relayData);
            Debug.Log("Lobby data updated with Relay Join Code.");

            return relayJoinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Relay allocation failed: {e.Message}");
            _allocation = null;
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"An unexpected error occurred during Relay allocation: {e.Message}");
            _allocation = null;
            return null;
        }
    }

    private string GetRelayJoinCodeFromLobby()
    {
        // Get Relay join code from the centralized service instead of making API calls
        string relayJoinCode = LobbyManager.Instance.GetLobbyData("RelayJoinCode");

        if (string.IsNullOrEmpty(relayJoinCode))
        {
            Debug.LogError("RelayJoinCode not found in lobby data or is empty.");
            return null;
        }

        Debug.Log($"Retrieved Relay Join Code: {relayJoinCode}");
        return relayJoinCode;
    }

    public async Task<JoinAllocation> JoinRelayAndGetClientAllocationAsync()
    {
        try
        {
            string relayJoinCode = GetRelayJoinCodeFromLobby();
            if (string.IsNullOrEmpty(relayJoinCode))
            {
                Debug.LogError("Failed to get Relay join code from lobby data.");
                _joinAllocation = null;
                return null;
            }

            Debug.Log($"Joining Relay server with code: {relayJoinCode}");
            _joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode: relayJoinCode);
            Debug.Log("Client successfully joined Relay allocation.");
            return _joinAllocation;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Client failed to join Relay allocation: {e.Message}");
            _joinAllocation = null;
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"An unexpected error occurred while client was joining Relay: {e.Message}");
            _joinAllocation = null;
            return null;
        }
    }

    private bool StartHostWithRelay(Allocation allocation)
    {
        if (allocation == null)
        {
            Debug.LogError("Cannot start host: Relay allocation is null.");
            return false;
        }

        try
        {
            var unityTransport = GetUnityTransport();
            if (unityTransport == null)
            {
                Debug.LogError("UnityTransport component not found. Cannot start host.");
                return false;
            }

            // Convert Relay allocation data for UnityTransport
            unityTransport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            hostStartupManager.StartHost();

            Debug.Log("NetworkManager started in Host mode with Relay.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start host with Relay: {e.Message}");
            return false;
        }
    }

    private bool StartClientWithRelay(JoinAllocation joinAllocation)
    {
        if (joinAllocation == null)
        {
            Debug.LogError("Cannot start client: Relay join allocation is null.");
            return false;
        }

        try
        {
            var unityTransport = GetUnityTransport();
            if (unityTransport == null)
            {
                Debug.LogError("UnityTransport component not found. Cannot start client.");
                return false;
            }

            // Convert Relay join allocation data for UnityTransport
            unityTransport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

            NetworkManager.Singleton.StartClient();

            Debug.Log("NetworkManager started in Client mode with Relay.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start client with Relay: {e.Message}");
            return false;
        }
    }

    private UnityTransport GetUnityTransport()
    {
        var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (unityTransport == null)
        {
            // Attempt to get it from NetworkConfig if not directly on NetworkManager
            if (NetworkManager.Singleton.NetworkConfig != null && NetworkManager.Singleton.NetworkConfig.NetworkTransport is UnityTransport)
            {
                unityTransport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
            }
        }
        return unityTransport;
    }

    private void HandleLobbyStateUpdate(List<LobbyPlayerData> playersData, string localPlayerId, bool isLocalPlayerHost)
    {
        // Update our tracked state to stay in sync with lobby
        bool currentlyAllReady = AreAllPlayersReady(playersData);

        // Detect transition from "not all ready" to "all ready" - this is when countdown should start
        if (isLocalPlayerHost && currentlyAllReady && !allPlayersReady)
        {
            Debug.Log("PrivateMatchManager: All players ready state transition detected - starting countdown");
            _ = LobbyManager.Instance.StartGameCountdown();
        }

        // Always update our state to match current lobby state
        allPlayersReady = currentlyAllReady;
    }

    private void HandleLobbyDeleted()
    {
        // Clean up Relay and networking when lobby is deleted
        Debug.Log("PrivateMatchManager: Lobby deleted, cleaning up Relay and networking.");
        CleanupRelayAndNetworking();
    }

    private void HandlePlayerLeft()
    {
        // Cancel countdown if a player leaves
        if (LobbyManager.Instance.IsHost)
        {
            _ = LobbyManager.Instance.CancelGameCountdown();
        }
    }

    private void HandlePlayerReadyStateChanged()
    {
        Debug.Log("PrivateMatchManager: Player ready state changed.");

        // Only relevant when we're in lobby state (not countdown) and we're the host
        if (LobbyManager.Instance.IsHost && !LobbyManager.Instance.IsGameCountdownActive)
        {
            // SMART COUNTDOWN LOGIC: 
            // Instead of aggressively canceling on any ready state change,
            // make the countdown decision based on the current actual state.
            // This handles the race condition where UGS events arrive after immediate broadcasts.

            // Request current lobby state to make informed decision
            // This will trigger HandleLobbyStateUpdate which contains the countdown logic
            LobbyManager.Instance.RequestLobbyStateBroadcast();

            // Note: Per PROJECT_DECISIONS.md, we assume ready buttons are disabled after clicking,
            // so ready state changes are primarily additive (players becoming ready).
            // HandleLobbyStateUpdate will start countdown if all players are ready,
            // or naturally not start it if someone isn't ready.
            // No aggressive cancellation needed since unready actions are prevented by UI.
        }
    }

    private void HandleCountdownComplete()
    {
        SetupAndLaunchGame();

        // Clear countdown start time when game launches to prevent countdown UI 
        // from showing when returning to lobby after game ends
        if (LobbyManager.Instance.IsHost)
        {
            _ = LobbyManager.Instance.ClearCountdownOnGameStart();
        }
    }

    private bool AreAllPlayersReady(List<LobbyPlayerData> players)
    {
        // Require at least 2 players (can adjust based on game requirements)
        if (players.Count < 2)
        {
            return false;
        }

        // Check that all players are ready
        foreach (var player in players)
        {
            if (!player.IsReady)
            {
                return false;
            }
        }

        return true;
    }

    private void SetupAndLaunchGame()
    {
        // Only host can actually setup and launch the game
        if (!LobbyManager.Instance.IsHost)
        {
            Debug.Log("PrivateMatchManager: Countdown complete on client. Waiting for host to setup and launch game...");
            return;
        }

        Debug.Log("PrivateMatchManager: Countdown complete! Host setting up and launching game...");
        // TODO: review references to network object components, they might not exist for some network reasons, so they should be null checked
        gameManager.SetupAndLaunchGame();
    }

    public async Task LeaveLobbyAndCleanupAsync()
    {
        Debug.Log("Leaving lobby and cleaning up Relay/networking.");

        // Leave lobby using the centralized service
        await LobbyManager.Instance.LeaveLobbyAsync();

        // Clean up local Relay and networking state
        CleanupRelayAndNetworking();
    }

    /// <summary>
    /// Returns to lobby state, allowing countdown logic to work again.
    /// Call this when game ends, is cancelled, or when showing rematch/game over screen.
    /// </summary>
    public void ReturnToLobbyState()
    {
        Debug.Log("PrivateMatchManager: Returning to lobby state.");

        // Note: allPlayersReady will be updated naturally when lobby updates come in
        // with players' actual ready states. We don't manually set it here.
    }

    private void CleanupRelayAndNetworking()
    {
        // Clean up Relay allocation data
        _allocation = null;
        _joinAllocation = null;
        _relayJoinCode = null;

        // Shutdown networking
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost))
        {
            Debug.Log("Shutting down NetworkManager.");
            NetworkManager.Singleton.Shutdown();
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from lobby events
        if (LobbyManager.Instance != null)
        {
            LobbyManager.OnLobbyStateBroadcast -= HandleLobbyStateUpdate;
            LobbyManager.OnLobbyDeleted -= HandleLobbyDeleted;
            LobbyManager.OnPlayerLeft -= HandlePlayerLeft;
            LobbyManager.OnPlayerReadyStateChanged -= HandlePlayerReadyStateChanged;
            LobbyManager.OnCountdownComplete -= HandleCountdownComplete;
        }
    }
}


