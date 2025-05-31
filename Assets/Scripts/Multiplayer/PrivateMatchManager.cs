using System;
using UnityEngine;
using System.Threading.Tasks;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
// TODO: refactor to host stuff separate and client stuff separate?

// TODO: refactor Relay to its own service?
//
public class PrivateMatchManager : MonoBehaviour
{
    [SerializeField] private HostStartupManager hostStartupManager;
    [SerializeField] private NetworkGameManager networkGameManager;

    // TODO: broeadcast state updates such as lobby deleted / disconnected / etc ?

    // Relay allocation data
    private string _relayJoinCode;
    private Allocation _allocation; // For host's Relay allocation
    private JoinAllocation _joinAllocation; // For client's Relay allocation

    [SerializeField] private GameMode selectedGameMode;
    [SerializeField] private TeamSize selectedTeamSize;

    private void ValidateDependencies()
    {
        if (hostStartupManager == null)
        {
            Debug.LogError("PrivateMatchManager: HostStartupManager not found in the scene!");
            enabled = false;
        }

        if (networkGameManager == null)
        {
            Debug.LogError("PrivateMatchManager: NetworkGameManager not found in the scene!");
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
            if (!await StartHostWithRelayAsync(_allocation))
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
            if (!await StartClientWithRelayAsync(_joinAllocation))
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

    private async Task<string> GetRelayJoinCodeFromLobbyAsync()
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
            string relayJoinCode = await GetRelayJoinCodeFromLobbyAsync();
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

    private async Task<bool> StartHostWithRelayAsync(Allocation allocation)
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

    private async Task<bool> StartClientWithRelayAsync(JoinAllocation joinAllocation)
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

    // TODO: this should remain disabled after coundown completes, ignoring lobby updates, until we are back 
    // to the state when this is relevant...
    private void HandleLobbyStateUpdate(List<LobbyPlayerData> playersData, string localPlayerId, bool isLocalPlayerHost)
    {
        // Game countdown logic: Check if local player is host and all players are ready
        if (isLocalPlayerHost && AreAllPlayersReady(playersData))
        {
            _ = LobbyManager.Instance.StartGameCountdown();
        }
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
        // Cancel countdown if any player changes ready state to false
        if (LobbyManager.Instance.IsHost)
        {
            // We need to check the current lobby state to see if all players are still ready
            // The countdown should only continue if all players remain ready
            // Since this event fired, we know something changed, so we'll cancel and let
            // HandleLobbyStateUpdate restart it if appropriate
            _ = LobbyManager.Instance.CancelGameCountdown();
        }
    }

    private void HandleCountdownComplete()
    {
        StartGame();
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

    private void StartGame()
    {
        // Only host can actually start the game
        if (!LobbyManager.Instance.IsHost)
        {
            Debug.Log("Countdown complete on client. Waiting for host to start game...");
            return;
        }

        Debug.Log("Countdown complete! Host starting game...");
        networkGameManager.StartGame();
    }

    public async Task LeaveLobbyAndCleanupAsync()
    {
        Debug.Log("Leaving lobby and cleaning up Relay/networking.");

        // Leave lobby using the centralized service
        await LobbyManager.Instance.LeaveLobbyAsync();

        // Clean up local Relay and networking state
        CleanupRelayAndNetworking();
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


