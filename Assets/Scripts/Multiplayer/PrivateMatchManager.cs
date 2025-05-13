using System;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

// TODO: refactor to host stuff separate and client stuff separate?
public class PrivateMatchManager : MonoBehaviour
{
    public static event Action<string> OnLobbyCodeGenerated;
    public static event Action<List<LobbyPlayerData>, string, bool> OnLobbyStateRefreshed; // PlayerList, LocalPlayerId, IsLocalPlayerHost

    public string LobbyCode { get; private set; }
    public string LocalPlayerId { get; private set; }
    public bool IsHost { get; private set; }

    private string _relayJoinCode;
    private Allocation _allocation; // For host's Relay allocation
    private JoinAllocation _joinAllocation; // For client's Relay allocation

    private Lobby _currentLobby;
    [SerializeField] private GameMode selectedGameMode;
    [SerializeField] private TeamSize selectedTeamSize;

    // Constants for UGS Player Data keys
    private const string PlayerDataKeyDisplayName = "DisplayName";
    private const string PlayerDataKeyIsReady = "IsPlayerReady";

    private async void Awake()
    {
        try
        {
            await UnityServices.InitializeAsync();
            Debug.Log("UGS Initialized successfully.");
            await SignInAnonymouslyAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize UGS: {e.Message}");
        }
    }

    private async Task SignInAnonymouslyAsync()
    {
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            LocalPlayerId = AuthenticationService.Instance.PlayerId;
            Debug.Log($"Player signed in. PlayerID: {LocalPlayerId}");

            // Example: Set a default display name upon sign-in
            // This is a good place to set initial player data that won't change often.
            // await UpdatePlayerDisplayNameAsync($"Player{LocalPlayerId.Substring(0, 4)}");
        }
        catch (AuthenticationException ex)
        {
            // Compare error code to AuthenticationErrorCodes
            // Notify the player with the proper error message
            Debug.LogError($"Sign in failed: {ex.Message}");
        }
        catch (RequestFailedException ex)
        {
            // Compare error code to CommonErrorCodes
            // Notify the player with the proper error message
            Debug.LogError($"Sign in request failed: {ex.Message}");
        }
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

    public async Task<string> CreateLobbyAsync(string lobbyName, bool isPrivate)
    {
        if (string.IsNullOrEmpty(lobbyName))
        {
            lobbyName = "My Private Match"; // Default lobby name
        }

        try
        {
            var createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                // Player = GetPlayer(), // Optional: Add player data if needed later
                // Data = new Dictionary<string, DataObject>() // Optional: Add initial lobby data if needed
            };

            Debug.Log($"Creating lobby: {lobbyName}, Max Players: 2, Private: {isPrivate}");
            _currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, 2, createLobbyOptions);

            LobbyCode = _currentLobby.LobbyCode;
            Debug.Log($"Lobby created successfully! Name: {_currentLobby.Name}, ID: {_currentLobby.Id}, Lobby Code: {LobbyCode}");

            _relayJoinCode = await AllocateRelayServerAndGetJoinCodeAsync();
            if (string.IsNullOrEmpty(_relayJoinCode) || _allocation == null)
            {
                Debug.LogError("Relay allocation failed or allocation data not stored. Lobby created but unusable for Relay connection.");
                // TODO: Consider deleting the created lobby if Relay fails.
                _currentLobby = null; // Clear lobby reference
                LobbyCode = null;
                return null;
            }

            // Attempt to start the host with Relay
            if (!await StartHostWithRelayAsync(_allocation))
            {
                Debug.LogError("Failed to start host with Relay. Lobby and Relay allocation might be orphaned.");
                // TODO: Consider deleting the created lobby and cleaning up Relay allocation if StartHost fails.
                _currentLobby = null; // Clear lobby reference
                LobbyCode = null;
                _allocation = null; // Clear allocation
                _relayJoinCode = null;
                IsHost = false;
                ConvertAndBroadcastLobbyState(); // Notify UI of failure/reset
                return null;
            }

            IsHost = true;
            OnLobbyCodeGenerated?.Invoke(LobbyCode);
            // After successful lobby creation and host start, update and broadcast lobby state.
            await InitializePlayerDefaultData(); // Set initial data for the host player
            ConvertAndBroadcastLobbyState();
            return LobbyCode;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to create lobby: {e.Message}\n{e.StackTrace}");
            // Potentially throw or handle specific LobbyExceptionReason
            _currentLobby = null;
            LobbyCode = null;
            IsHost = false;
            ConvertAndBroadcastLobbyState(); // Notify UI of failure/reset
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"An unexpected error occurred while creating lobby: {e.Message}\n{e.StackTrace}");
            _currentLobby = null;
            LobbyCode = null;
            IsHost = false;
            ConvertAndBroadcastLobbyState(); // Notify UI of failure/reset
            return null;
        }
    }

    private async Task<string> AllocateRelayServerAndGetJoinCodeAsync()
    {
        try
        {
            _allocation = await RelayService.Instance.CreateAllocationAsync(1); // Store the allocation
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(_allocation.AllocationId);

            Debug.Log($"Relay server allocated. Join Code: {relayJoinCode}");

            var updateLobbyOptions = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    {
                        "RelayJoinCode", new DataObject(
                            visibility: DataObject.VisibilityOptions.Member,
                            value: relayJoinCode)
                    }
                }
            };
            await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, updateLobbyOptions);
            Debug.Log("Lobby data updated with Relay Join Code.");

            return relayJoinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Relay allocation failed: {e.Message}\n{e.StackTrace}");
            _allocation = null; // Clear allocation on failure
            return null;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to update lobby with Relay join code: {e.Message}\n{e.StackTrace}");
            _allocation = null; // Clear allocation on failure (though Relay part might have succeeded)
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"An unexpected error occurred during Relay allocation or lobby update: {e.Message}\n{e.StackTrace}");
            _allocation = null; // Clear allocation on failure
            return null;
        }
    }

    public async Task<bool> JoinLobbyByCodeAsync(string lobbyCode)
    {
        if (string.IsNullOrEmpty(lobbyCode))
        {
            Debug.LogError("Lobby code cannot be null or empty.");
            return false;
        }

        try
        {
            Debug.Log($"Attempting to join lobby with code: {lobbyCode}");
            JoinLobbyByCodeOptions joinOptions = new JoinLobbyByCodeOptions();
            // We can add Player data here if needed in the future, similar to GetPlayer() in CreateLobbyAsync
            // joinOptions.Player = GetPlayer(); 

            _currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinOptions);

            Debug.Log($"Successfully joined lobby! Name: {_currentLobby.Name}, ID: {_currentLobby.Id}");

            _joinAllocation = await JoinRelayAndGetClientAllocationAsync();
            if (_joinAllocation == null)
            {
                Debug.LogError("Failed to join Relay server after joining lobby. Undoing lobby join.");
                _currentLobby = null;
                IsHost = false; // Client is never host initially upon joining
                ConvertAndBroadcastLobbyState(); // Notify UI of failure/reset
                return false;
            }

            // Attempt to start the client with Relay
            if (!await StartClientWithRelayAsync(_joinAllocation))
            {
                Debug.LogError("Failed to start client with Relay. Leaving lobby.");
                // TODO: Consider explicitly leaving the lobby if StartClient fails.
                _currentLobby = null;
                _joinAllocation = null;
                IsHost = false; // Explicitly set IsHost to false for client
                ConvertAndBroadcastLobbyState(); // Notify UI of failure/reset
                return false;
            }

            Debug.Log("Client successfully joined lobby, Relay server, and started NGO client.");
            IsHost = false; // Explicitly set IsHost to false for client
            // After successful lobby join and client start, update and broadcast lobby state.
            await InitializePlayerDefaultData(); // Set initial data for the joining player
            ConvertAndBroadcastLobbyState();
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to join lobby with code '{lobbyCode}': {e.Message}\n{e.StackTrace}");
            // Handle specific reasons, e.g., LobbyNotFound, LobbyFull
            if (e.Reason == LobbyExceptionReason.LobbyNotFound)
            {
                Debug.LogError("Lobby not found. Please check the code and try again.");
            }
            else if (e.Reason == LobbyExceptionReason.LobbyFull)
            {
                Debug.LogError("The lobby is full.");
            }
            // Add more specific error handling as needed
            _currentLobby = null;
            IsHost = false;
            ConvertAndBroadcastLobbyState(); // Notify UI of failure/reset
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"An unexpected error occurred while joining lobby with code '{lobbyCode}': {e.Message}\n{e.StackTrace}");
            _currentLobby = null;
            IsHost = false;
            ConvertAndBroadcastLobbyState(); // Notify UI of failure/reset
            return false;
        }
    }

    // Method to get Relay join code from lobby data (used by JoinRelayAndGetClientAllocationAsync)
    private async Task<string> GetRelayJoinCodeFromLobbyAsync() // Made this async to align if it ever needs awaits
    {
        if (_currentLobby == null)
        {
            Debug.LogError("Cannot get Relay join code: Current lobby is null.");
            return null;
        }
        if (_currentLobby.Data == null)
        {
            Debug.LogError("Cannot get Relay join code: Lobby data is null.");
            return null;
        }

        if (_currentLobby.Data.TryGetValue("RelayJoinCode", out DataObject relayCodeObject))
        {
            if (relayCodeObject != null && !string.IsNullOrEmpty(relayCodeObject.Value))
            {
                Debug.Log($"Retrieved Relay Join Code: {relayCodeObject.Value}");
                return relayCodeObject.Value;
            }
            else
            {
                Debug.LogError("RelayJoinCode found in lobby data but is null or empty.");
                return null;
            }
        }
        else
        {
            Debug.LogError("RelayJoinCode not found in lobby data.");
            // It's possible the host hasn't set it yet. Consider polling or using lobby events.
            return null;
        }
    }

    // Renamed and ensured _joinAllocation is set here
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
            Debug.LogError($"Client failed to join Relay allocation: {e.Message}\n{e.StackTrace}");
            _joinAllocation = null;
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"An unexpected error occurred while client was joining Relay: {e.Message}\n{e.StackTrace}");
            _joinAllocation = null;
            return null;
        }
    }

    // NGO Host Start Logic
    private async Task<bool> StartHostWithRelayAsync(Allocation allocation)
    {
        if (allocation == null)
        {
            Debug.LogError("Cannot start host: Relay allocation is null.");
            return false;
        }

        try
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

            if (unityTransport == null)
            {
                Debug.LogError("UnityTransport component not found on NetworkManager or in NetworkConfig. Cannot start host.");
                return false;
            }

            // Convert Relay allocation data for UnityTransport
            // The connection type ("dtls" or "udp") should match what clients expect. "dtls" is recommended.
            unityTransport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            NetworkManager.Singleton.StartHost();
            Debug.Log("NetworkManager started in Host mode with Relay.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start host with Relay: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    // NGO Client Start Logic
    private async Task<bool> StartClientWithRelayAsync(JoinAllocation joinAllocation)
    {
        if (joinAllocation == null)
        {
            Debug.LogError("Cannot start client: Relay join allocation is null.");
            return false;
        }

        try
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

            if (unityTransport == null)
            {
                Debug.LogError("UnityTransport component not found on NetworkManager or in NetworkConfig. Cannot start client.");
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
            Debug.LogError($"Failed to start client with Relay: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    public void TriggerLobbyDataRefresh()
    {
        RequestLobbyStateRefresh(); // Internally, it should now call the more comprehensive refresh
    }

    // New methods for UI interaction and lobby state management
    public void RequestLobbyStateRefresh()
    {
        Debug.Log("PrivateMatchManager: RequestLobbyStateRefresh called.");
        if (_currentLobby != null)
        {
            // In a real scenario, you might want to fetch the latest lobby state here
            // e.g., _currentLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
            // For now, we assume _currentLobby is up-to-date or updated by other processes (like heartbeat)
            ConvertAndBroadcastLobbyState();
        }
        else
        {
            // Broadcast an empty/default state if no lobby
            OnLobbyStateRefreshed?.Invoke(new List<LobbyPlayerData>(), LocalPlayerId, IsHost);
        }
    }

    public async Task ToggleLocalPlayerReadyState()
    {
        if (_currentLobby == null || string.IsNullOrEmpty(LocalPlayerId))
        {
            Debug.LogWarning("Cannot toggle ready state: Not in a lobby or LocalPlayerId is missing.");
            return;
        }

        Unity.Services.Lobbies.Models.Player localUgsPlayer = _currentLobby.Players.Find(p => p.Id == LocalPlayerId);
        if (localUgsPlayer == null)
        {
            Debug.LogError("Local UGS Player not found in lobby to toggle ready state.");
            return;
        }

        bool currentReadyState = false;
        if (localUgsPlayer.Data != null && localUgsPlayer.Data.TryGetValue(PlayerDataKeyIsReady, out var readyData))
        {
            bool.TryParse(readyData.Value, out currentReadyState);
        }
        bool newReadyState = !currentReadyState;
        Debug.Log($"Toggling ready state for {LocalPlayerId} from {currentReadyState} to {newReadyState}");

        UpdatePlayerOptions options = new UpdatePlayerOptions
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { PlayerDataKeyIsReady, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, newReadyState.ToString().ToLower()) }
            }
        };

        try
        {
            _currentLobby = await LobbyService.Instance.UpdatePlayerAsync(_currentLobby.Id, LocalPlayerId, options);
            Debug.Log($"Player {LocalPlayerId} ready state updated in UGS to: {newReadyState}");
            ConvertAndBroadcastLobbyState(); // Refresh state after UGS update
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to update player ready state in UGS: {e}");
        }
    }

    private async Task InitializePlayerDefaultData()
    {
        if (_currentLobby == null || string.IsNullOrEmpty(LocalPlayerId))
        {
            Debug.LogWarning("Cannot initialize player data: Not in a lobby or LocalPlayerId is missing.");
            return;
        }

        // Set initial display name and ready state for the local player in the lobby
        string displayName = PlayerPrefs.GetString("PlayerName", $"Player{LocalPlayerId.Substring(0, 4)}"); // Use PlayerPrefs or a default

        UpdatePlayerOptions options = new UpdatePlayerOptions
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { PlayerDataKeyDisplayName, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, displayName) },
                { PlayerDataKeyIsReady, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "false") } // Default to not ready
            }
        };

        try
        {
            _currentLobby = await LobbyService.Instance.UpdatePlayerAsync(_currentLobby.Id, LocalPlayerId, options);
            Debug.Log($"Initialized default data for player {LocalPlayerId} (DisplayName: {displayName}, Ready: false).");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to initialize player default data in UGS for {LocalPlayerId}: {e}");
        }
    }

    private void ConvertAndBroadcastLobbyState()
    {
        if (string.IsNullOrEmpty(LocalPlayerId)) // Ensure LocalPlayerId is set
        {
            Debug.LogWarning("ConvertAndBroadcastLobbyState: LocalPlayerId is not set. Aborting broadcast.");
            // Optionally broadcast an empty state or specific error state
            OnLobbyStateRefreshed?.Invoke(new List<LobbyPlayerData>(), null, false);
            return;
        }

        if (_currentLobby == null)
        {
            // If lobby is null (e.g., left or failed to join), broadcast empty player list
            // and reset local player's host status for UI consistency.
            IsHost = false; // Reset host status if no lobby
            OnLobbyStateRefreshed?.Invoke(new List<LobbyPlayerData>(), LocalPlayerId, IsHost);
            Debug.Log("ConvertAndBroadcastLobbyState: Current lobby is null. Broadcasted empty player list.");
            return;
        }

        List<LobbyPlayerData> playersData = new List<LobbyPlayerData>();
        foreach (var ugsPlayer in _currentLobby.Players)
        {
            bool isPlayerReady = false;
            if (ugsPlayer.Data != null && ugsPlayer.Data.TryGetValue(PlayerDataKeyIsReady, out var readyData))
            {
                bool.TryParse(readyData.Value, out isPlayerReady);
            }

            string displayName = $"Player{ugsPlayer.Id.Substring(0, 4)}"; // Default display name
            if (ugsPlayer.Data != null && ugsPlayer.Data.TryGetValue(PlayerDataKeyDisplayName, out var nameData))
            {
                displayName = nameData.Value;
            }

            playersData.Add(new LobbyPlayerData
            {
                PlayerId = ugsPlayer.Id,
                DisplayName = displayName,
                IsHost = ugsPlayer.Id == _currentLobby.HostId,
                IsReady = isPlayerReady,
                IsLocal = ugsPlayer.Id == LocalPlayerId
            });
        }

        // Update local IsHost status based on current lobby data
        this.IsHost = _currentLobby.HostId == LocalPlayerId;

        OnLobbyStateRefreshed?.Invoke(playersData, LocalPlayerId, this.IsHost);
        Debug.Log($"Broadcasted Lobby State: {playersData.Count} players. LocalPlayerId: {LocalPlayerId}, IsHost: {this.IsHost}");
    }

    // Example: Method to update player display name (could be called from a settings UI)
    public async Task UpdatePlayerDisplayNameAsync(string newName)
    {
        if (string.IsNullOrEmpty(LocalPlayerId) || string.IsNullOrEmpty(newName))
        {
            Debug.LogWarning("Cannot update display name: LocalPlayerId or newName is missing.");
            return;
        }

        if (_currentLobby != null)
        {
            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { PlayerDataKeyDisplayName, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, newName) }
                }
            };
            try
            {
                _currentLobby = await LobbyService.Instance.UpdatePlayerAsync(_currentLobby.Id, LocalPlayerId, options);
                Debug.Log($"Player {LocalPlayerId} display name updated to: {newName} in lobby {_currentLobby.Id}");
                ConvertAndBroadcastLobbyState(); // Refresh state after UGS update
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"Failed to update player display name in UGS: {e}");
            }
        }
        else
        {
            // If not in a lobby, perhaps store it in PlayerPrefs to be used when joining/creating next lobby
            PlayerPrefs.SetString("PlayerName", newName);
            PlayerPrefs.Save();
            Debug.Log($"Player display name preference saved: {newName}");
        }
    }

    // Helper method to create a Player object (can be expanded later)
    /*
    private Player GetPlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                // Example: { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "MyName") }
            }
        };
    }
    */
}
