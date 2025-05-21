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
using System.Collections;

// TODO: implement lobby SendHeartbeatPingAsync?

// TODO: refactor to host stuff separate and client stuff separate?
public class PrivateMatchManager : MonoBehaviour
{
    [SerializeField] private HostStartupManager hostStartupManager;
    [SerializeField] private NetworkGameManager networkGameManager;

    // TODO: why static?
    // TODO: should I use singleton instead?
    public static event Action<List<LobbyPlayerData>, string, bool> OnLobbyStateBroadcast; // PlayerList, LocalPlayerId, IsLocalPlayerHost
    // Countdown event for UI to display
    public static event Action<float> OnCountdownTick; // Countdown remaining seconds
    public static event Action OnCountdownComplete; // When countdown reaches zero
    // TODO: broeadcast state updates such as lobby deleted / disconnected / etc

    [Header("Game Start Settings")]
    [SerializeField] private float gameStartCountdownDuration = 3f; // Countdown in seconds before game starts
    private bool countdownActive = false;
    private float countdownTimeRemaining = 0f;
    private const string LobbyDataKeyCountdownStarted = "CountdownStarted";

    // Heartbeat settings
    private Coroutine _heartbeatCoroutine;
    private const float HEARTBEAT_INTERVAL = 15f; // Send heartbeat every 15 seconds (well within the 30s requirement)

    public string LobbyCode => _currentLobby?.LobbyCode;
    // TODO: should be derived from NetworkManager?
    public string LocalPlayerId { get; private set; }
    // TODO: should be derived from NetworkManager?
    public bool IsHost { get; private set; }

    private string _relayJoinCode;
    private Allocation _allocation; // For host's Relay allocation
    private JoinAllocation _joinAllocation; // For client's Relay allocation

    private Lobby _currentLobby;
    private LobbyEventCallbacks _lobbyEventCallbacks;
    // TODO: why?
    private bool _subscribedToLobbyEvents = false;

    // Throttling for GetLobbyAsync (full lobby fetch)
    private float _apiCooldownEndTime = 0f; // When the next API call is permissible
    private Coroutine _queuedFetchCoroutine = null; // Holds the *single* allowed queued fetch
    private const float MIN_API_CALL_INTERVAL = 1.1f; // UGS limit: 1 GetLobby/sec for players

    [SerializeField] private GameMode selectedGameMode;
    [SerializeField] private TeamSize selectedTeamSize;

    // Constants for UGS Player Data keys
    private const string PlayerDataKeyDisplayName = "DisplayName";
    private const string PlayerDataKeyIsReady = "IsPlayerReady";

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
    }

    private async void Awake()
    {
        ValidateDependencies();
        if (!this.enabled) return;

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


        OnLobbyStateBroadcast += HandleLobbyStateUpdate;
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

        IsHost = true;

        try
        {
            var createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                // Player = GetPlayer(), // Optional: Add player data if needed later - what is the benefit of this? Show player info in list of lobbies?
                // TODO: add game mode and team size to lobby data
                // Data = new Dictionary<string, DataObject>() // Optional: Add initial lobby data if needed
            };

            Debug.Log($"Creating lobby: {lobbyName}, Max Players: 2, Private: {isPrivate}");
            _currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, 2, createLobbyOptions);

            Debug.Log($"Lobby created successfully! Name: {_currentLobby.Name}, ID: {_currentLobby.Id}, Lobby Code: {LobbyCode}");

            _relayJoinCode = await AllocateRelayServerAndGetJoinCodeAsync();
            if (string.IsNullOrEmpty(_relayJoinCode) || _allocation == null)
            {
                Debug.LogError("Relay allocation failed or allocation data not stored. Lobby created but unusable for Relay connection.");
                await CleanUpLobbyAndRelayOnErrorAsync("Relay allocation failed");
                return null;
            }

            // Attempt to start the host with Relay
            if (!await StartHostWithRelayAsync(_allocation))
            {
                Debug.LogError("Failed to start host with Relay. Lobby and Relay allocation might be orphaned.");
                await CleanUpLobbyAndRelayOnErrorAsync("StartHostWithRelayAsync failed");
                return null;
            }

            await InitializePlayerDefaultData();
            await SubscribeToLobbyEvents();

            // Start sending heartbeats when lobby is created successfully
            StartHeartbeat();

            RequestLobbyStateRefresh("Post CreateLobbyAsync");
            return LobbyCode;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to create lobby: {e.Message}\\n{e.StackTrace}");
            await CleanUpLobbyAndRelayOnErrorAsync("LobbyServiceException during create");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"An unexpected error occurred while creating lobby: {e.Message}\\n{e.StackTrace}");
            await CleanUpLobbyAndRelayOnErrorAsync("Unexpected error during create");
            return null;
        }
    }

    private async Task CleanUpLobbyAndRelayOnErrorAsync(string context)
    {
        Debug.LogError($"Cleaning up due to error: {context}");
        if (_currentLobby != null)
        {
            try
            {
                Debug.LogWarning($"Attempting to delete lobby {_currentLobby.Id} due to error: {context}");
                await LobbyService.Instance.DeleteLobbyAsync(_currentLobby.Id);
                Debug.Log($"Lobby {_currentLobby.Id} deleted after error.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete lobby {_currentLobby.Id} during error cleanup: {ex.Message}");
            }
            _currentLobby = null;
        }
        _allocation = null;
        _joinAllocation = null;
        _relayJoinCode = null;
        IsHost = false; // Ensure host status is reset
        BroadcastLobbyState(); // Notify UI of failure/reset
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
            Debug.LogError($"Relay allocation failed: {e.Message}\\n{e.StackTrace}");
            _allocation = null; // Clear allocation on failure
            return null;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to update lobby with Relay join code: {e.Message}\\n{e.StackTrace}");
            _allocation = null; // Clear allocation on failure (though Relay part might have succeeded)
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"An unexpected error occurred during Relay allocation or lobby update: {e.Message}\\n{e.StackTrace}");
            _allocation = null; // Clear allocation on failure
            return null;
        }
    }

    public string GetCurrentLobbyCode()
    {
        return _currentLobby?.LobbyCode ?? string.Empty;
    }

    public async Task<bool> JoinLobbyByCodeAsync(string lobbyCode)
    {
        if (string.IsNullOrEmpty(lobbyCode))
        {
            Debug.LogError("Lobby code cannot be null or empty.");
            return false;
        }

        IsHost = false;

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
                await CleanUpLobbyAndRelayOnErrorAsync("JoinRelayAndGetClientAllocationAsync failed for client");
                return false;
            }

            // Attempt to start the client with Relay
            if (!await StartClientWithRelayAsync(_joinAllocation))
            {
                Debug.LogError("Failed to start client with Relay. Leaving lobby.");
                await CleanUpLobbyAndRelayOnErrorAsync("StartClientWithRelayAsync failed");
                return false;
            }

            Debug.Log("Client successfully joined lobby, Relay server, and started NGO client.");
            await InitializePlayerDefaultData();
            await SubscribeToLobbyEvents();
            RequestLobbyStateRefresh("Post JoinLobbyByCodeAsync");
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to join lobby with code '{lobbyCode}': {e.Message}\\n{e.StackTrace}");
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
            await CleanUpLobbyAndRelayOnErrorAsync($"LobbyServiceException during join: {e.Reason}");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"An unexpected error occurred while joining lobby with code '{lobbyCode}': {e.Message}\\n{e.StackTrace}");
            await CleanUpLobbyAndRelayOnErrorAsync("Unexpected error during join");
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

        // Attempt to refresh lobby to ensure data is fresh before accessing RelayJoinCode
        try
        {
            _currentLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to refresh lobby before getting RelayJoinCode: {e.Message}");
            // Potentially handle LobbyNotFound case specifically
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
            Debug.LogError($"Client failed to join Relay allocation: {e.Message}\\n{e.StackTrace}");
            _joinAllocation = null;
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"An unexpected error occurred while client was joining Relay: {e.Message}\\n{e.StackTrace}");
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
            // TODO: DRY getting UnityTransport?
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


            hostStartupManager.StartHost();

            Debug.Log("NetworkManager started in Host mode with Relay.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start host with Relay: {e.Message}\\n{e.StackTrace}");
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

            // TODO: NetworkManager doesnt have a util function for this?
            // Convert Relay join allocation data for UnityTransport
            unityTransport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

            NetworkManager.Singleton.StartClient();

            Debug.Log("NetworkManager started in Client mode with Relay.");

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start client with Relay: {e.Message}\\n{e.StackTrace}");
            return false;
        }
    }

    // --- Lobby State Refresh/Fetch Throttling ---
    public async void RequestLobbyStateRefresh(string reason)
    {
        float timeNow = Time.realtimeSinceStartup;
        Debug.Log($"RequestLobbyStateRefresh ({reason}) received at {timeNow:F2}. Current cooldown ends: {_apiCooldownEndTime:F2}");

        if (timeNow < _apiCooldownEndTime) // Currently in cooldown
        {
            if (_queuedFetchCoroutine != null)
            {
                Debug.Log($"Ignoring request ({reason}): API in cooldown AND a fetch is already queued.");
                return;
            }
            else
            {
                float delayForQueued = _apiCooldownEndTime - timeNow;
                Debug.Log($"Queueing request ({reason}) to run after current cooldown. Delay: {delayForQueued:F2}s");
                // This coroutine is only for queued calls.
                _queuedFetchCoroutine = StartCoroutine(DelayedLobbyFetchCoroutine(delayForQueued, reason));
                return;
            }
        }
        else // Not in cooldown, process directly
        {
            Debug.Log($"Processing request ({reason}) directly via async Task (not in cooldown).");
            try
            {
                // ExecuteFullLobbyFetchAsync will set the cooldown immediately upon its execution.
                await ExecuteFullLobbyFetchAsync(reason);
            }
            catch (Exception e)
            {
                // Log the exception from the direct async call.
                // ExecuteFullLobbyFetchAsync itself handles its internal errors and cooldown setting.
                Debug.LogError($"Direct execution of ExecuteFullLobbyFetchAsync for reason '{reason}' encountered an error: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    // This coroutine is only used for QUEUED calls.
    private IEnumerator DelayedLobbyFetchCoroutine(float delay, string reason)
    {
        Debug.Log($"Executing previously queued DelayedLobbyFetchCoroutine for reason: {reason}. Waiting for delay: {delay:F2}s.");
        yield return new WaitForSeconds(delay);
        Debug.Log($"Queued fetch for '{reason}' now proceeding after delay.");

        Task fetchTask = ExecuteFullLobbyFetchAsync(reason);
        yield return new WaitUntil(() => fetchTask.IsCompleted);

        if (fetchTask.IsFaulted)
        {
            Debug.LogError($"Queued execution of ExecuteFullLobbyFetchAsync for reason '{reason}' encountered an error: {fetchTask.Exception?.GetBaseException()?.Message}\n{fetchTask.Exception?.GetBaseException()?.StackTrace}");
        }

        _queuedFetchCoroutine = null;
    }

    // We do not care about "early exists" in here. If there are issues, then there is nothing to do other than showing the errors.
    private async Task ExecuteFullLobbyFetchAsync(string reason)
    {
        // Set the API cooldown for the *next* potential operation, now that this one is starting.
        _apiCooldownEndTime = Time.realtimeSinceStartup + MIN_API_CALL_INTERVAL;
        Debug.Log($"ExecuteFullLobbyFetchAsync for '{reason}' starting. API cooldown set. Next API call possible after: {_apiCooldownEndTime:F2}");

        if (_currentLobby == null || string.IsNullOrEmpty(_currentLobby.Id))
        {
            Debug.LogWarning($"ExecuteFullLobbyFetchAsync ({reason}): Cannot fetch. No current lobby or lobby ID is missing.");

            BroadcastLobbyState();

            return;
        }

        Debug.Log($"ExecuteFullLobbyFetchAsync ({reason}): Fetching lobby ID: {_currentLobby.Id}");
        try
        {
            Lobby fetchedLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
            _currentLobby = fetchedLobby; // Replace local copy with the fresh one
            Debug.Log($"ExecuteFullLobbyFetchAsync ({reason}): Lobby data refreshed successfully for {_currentLobby.Name} ({_currentLobby.Id}).");
            BroadcastLobbyState();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"ExecuteFullLobbyFetchAsync ({reason}): LobbyServiceException for lobby {_currentLobby.Id}: {e.Reason} - {e.Message}");
            if (e.Reason == LobbyExceptionReason.LobbyNotFound)
            {
                Debug.LogWarning("ExecuteFullLobbyFetchAsync: Lobby not found. It might have been deleted.");
                _currentLobby = null; // Clear local reference
                IsHost = false;
                BroadcastLobbyState(); // Broadcast cleared state
            }
            // If rate limited, _apiCooldownEndTime is already set, future RequestLobbyStateRefresh calls will respect it.
        }
        catch (Exception e)
        {
            Debug.LogError($"ExecuteFullLobbyFetchAsync ({reason}): Unexpected error for lobby {_currentLobby.Id}: {e.Message}");
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
            RequestLobbyStateRefresh("ToggleLocalPlayerReadyState - Player Not Found");
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
            BroadcastLobbyState();
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
        // TODO: DRY with BroadcastLobbyState?
        string displayName = PlayerPrefs.GetString("PlayerName", $"Player{LocalPlayerId.Substring(0, 3)}"); // Use PlayerPrefs or a default

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

    private void BroadcastLobbyState()
    {
        Debug.Log("BroadcastLobbyState: Starting broadcast.");
        Debug.Log("BroadcastLobbyState: LocalPlayerId: " + LocalPlayerId);

        if (_currentLobby == null)
        {
            OnLobbyStateBroadcast?.Invoke(new List<LobbyPlayerData>(), LocalPlayerId, IsHost);
            Debug.Log("BroadcastLobbyState: Current lobby is null. Broadcasted empty player list.");
            return;
        }

        if (string.IsNullOrEmpty(LocalPlayerId))
        {
            // This can happen if SignInAnonymouslyAsync hasn't completed yet, or failed.
            // If we have a lobby, but no local player ID, this is an inconsistent state.
            Debug.LogWarning("BroadcastLobbyState: LocalPlayerId is not set, but _currentLobby exists. This might indicate an issue with sign-in or initialization order.");
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

            string displayName = $"Player{ugsPlayer.Id.Substring(0, 3)}"; // Default display name
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

        // TODO: why broadcast the LocalPlayerId and IsHost?
        OnLobbyStateBroadcast?.Invoke(playersData, LocalPlayerId, this.IsHost);
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
                BroadcastLobbyState(); // Refresh state after UGS update
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


    // --- Lobby Event Subscription and Handling ---
    private async Task SubscribeToLobbyEvents()
    {
        if (_currentLobby == null || _subscribedToLobbyEvents)
        {
            if (_subscribedToLobbyEvents) Debug.Log("Already subscribed to lobby events.");
            else Debug.LogWarning("Cannot subscribe to lobby events: Current lobby is null.");
            return;
        }

        _lobbyEventCallbacks = new LobbyEventCallbacks();
        _lobbyEventCallbacks.LobbyDeleted += OnLobbyDeleted;
        _lobbyEventCallbacks.KickedFromLobby += OnKickedFromLobby;
        _lobbyEventCallbacks.PlayerJoined += OnPlayersJoined;
        _lobbyEventCallbacks.PlayerLeft += OnPlayersLeft;
        _lobbyEventCallbacks.LobbyChanged += OnLobbyChanged;
        _lobbyEventCallbacks.DataChanged += OnLobbyDataChanged;
        _lobbyEventCallbacks.PlayerDataChanged += OnPlayerDataChanged;
        // LobbyEventConnectionStateChanged?
        // TODO: consider adding _lobbyEventCallbacks.ConnectionStateChanged += OnConnectionStateChanged;

        try
        {
            await LobbyService.Instance.SubscribeToLobbyEventsAsync(_currentLobby.Id, _lobbyEventCallbacks);
            _subscribedToLobbyEvents = true;
            Debug.Log($"Successfully subscribed to lobby events for lobby ID: {_currentLobby.Id}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Error subscribing to lobby events: {e}");
            _subscribedToLobbyEvents = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Unexpected error subscribing to lobby events: {e}");
            _subscribedToLobbyEvents = false;
        }
    }

    private void HandleLobbyStateUpdate(List<LobbyPlayerData> playersData, string localPlayerId, bool isLocalPlayerHost)
    {
        // Check if local player is host and all players are ready
        if (isLocalPlayerHost && AreAllPlayersReady(playersData) && !countdownActive)
        {
            StartGameCountdown();
        }
    }

    private async void OnLobbyDeleted()
    {
        Debug.Log("Lobby deleted event received.");
        StopHeartbeat(); // Stop sending heartbeats when lobby is deleted
        _currentLobby = null;
        IsHost = false;
        _subscribedToLobbyEvents = false;
        BroadcastLobbyState();
        // TODO: UI navigation to leave lobby screen
        // No need to fetch data for a deleted lobby.
    }

    private async void OnKickedFromLobby()
    {
        Debug.Log("Kicked from lobby event received.");
        _currentLobby = null;
        IsHost = false;
        _subscribedToLobbyEvents = false;
        BroadcastLobbyState();
        // TODO: UI navigation, display message
        // No need to fetch data after being kicked.
    }

    private void OnPlayersJoined(List<LobbyPlayerJoined> playersJoined)
    {
        Debug.Log($"Player(s) joined event received. Count: {playersJoined.Count}");
        if (_currentLobby == null)
        {
            // TODO: why InitiateLobbyStateFetch? there is no lobby! call broadcast directly instead?
            Debug.LogWarning("OnPlayersJoined: _currentLobby is null. Requesting full fetch.");
            RequestLobbyStateRefresh("PlayerJoined with null lobby");
            return;
        }

        foreach (var playerJoinInfo in playersJoined)
        {
            // Check if player already exists (should ideally not happen for a join event)
            if (_currentLobby.Players.Exists(p => p.Id == playerJoinInfo.Player.Id))
            {
                Debug.LogWarning($"OnPlayersJoined: Player {playerJoinInfo.Player.Id} already in local lobby. This might indicate desync or a redundant event.");
                // Potentially update existing player data if necessary, or rely on PlayerDataChanged for that.
                // For now, we will just ensure they are marked as present.
            }
            else
            {
                _currentLobby.Players.Add(playerJoinInfo.Player);
                Debug.Log($"Player {playerJoinInfo.Player.Id} added to local lobby from PlayerJoined event.");
            }
        }
        BroadcastLobbyState(); // Broadcast updated local state
    }

    private void OnPlayersLeft(List<int> playerIndexesLeft)
    {
        Debug.Log($"Player(s) left event received. Indices count: {playerIndexesLeft.Count}");
        if (_currentLobby == null)
        {
            Debug.LogWarning("OnPlayersLeft: _currentLobby is null. Requesting full fetch.");
            RequestLobbyStateRefresh("PlayerLeft with null lobby");
            return;
        }

        // If countdown is active, cancel it since a player left
        if (countdownActive && IsHost)
        {
            CancelGameCountdown();
        }

        // Sort indices in descending order to safely remove elements from the list
        // without affecting the indices of subsequent elements to be removed.
        playerIndexesLeft.Sort((a, b) => b.CompareTo(a));

        foreach (var index in playerIndexesLeft)
        {
            if (index >= 0 && index < _currentLobby.Players.Count)
            {
                Unity.Services.Lobbies.Models.Player removedPlayer = _currentLobby.Players[index];
                _currentLobby.Players.RemoveAt(index);
                Debug.Log($"Player {removedPlayer.Id} (at index {index}) removed from local lobby based on PlayerLeft event.");
            }
            else
            {
                Debug.LogWarning($"OnPlayersLeft: Invalid player index {index} received. Current player count: {_currentLobby.Players.Count}. May need full refresh.");
                // If an invalid index is received, our local list might be out of sync.
                // Fallback to a full refresh to be safe.
                RequestLobbyStateRefresh("PlayerLeft with invalid index");
                return; // Exit after initiating refresh
            }
        }
        BroadcastLobbyState(); // Broadcast updated local state
    }

    private void OnLobbyChanged(ILobbyChanges changes)
    {
        Debug.Log("LobbyChanged event received.");
        if (_currentLobby == null)
        {
            Debug.LogWarning("OnLobbyChanged: _currentLobby is null. This event should ideally not fire if we have no local lobby context. Ignoring event for now, expecting a full fetch if lobby becomes active.");
            return;
        }

        // Log what changed for diagnostic purposes
        if (changes.Name.Changed) { Debug.Log($"Lobby Name changed. Old (local): '{_currentLobby.Name}', New (event): '{changes.Name.Value}'. Will fetch updated lobby."); }
        if (changes.HostId.Changed) { Debug.Log($"Lobby HostId changed. Old (local): '{_currentLobby.HostId}', New (event): '{changes.HostId.Value}'. Will fetch updated lobby."); }
        if (changes.IsPrivate.Changed) { Debug.Log($"Lobby IsPrivate changed. Old (local): '{_currentLobby.IsPrivate}', New (event): '{changes.IsPrivate.Value}'. Will fetch updated lobby."); }
        if (changes.IsLocked.Changed) { Debug.Log($"Lobby IsLocked changed. Old (local): '{_currentLobby.IsLocked}', New (event): '{changes.IsLocked.Value}'. Will fetch updated lobby."); }
        if (changes.AvailableSlots.Changed) { Debug.Log($"Lobby AvailableSlots changed. Old (local): '{_currentLobby.AvailableSlots}', New (event): '{changes.AvailableSlots.Value}'. Will fetch updated lobby."); }
        if (changes.MaxPlayers.Changed) { Debug.Log($"Lobby MaxPlayers changed. Old (local): '{_currentLobby.MaxPlayers}', New (event): '{changes.MaxPlayers.Value}'. Will fetch updated lobby."); }
        if (changes.Data.Changed) { Debug.Log("Lobby Data dictionary potentially changed wholesale. Will fetch updated lobby."); }

        // Since Lobby object properties are read-only, any change reported by ILobbyChanges that affects these
        // requires a full fetch of the Lobby object.
        Debug.Log("OnLobbyChanged: Core lobby properties or data reported as changed. Initiating full lobby state fetch.");
        // RequestLobbyStateRefresh("LobbyChanged property update");
    }

    private void OnLobbyDataChanged(Dictionary<string, ChangedOrRemovedLobbyValue<DataObject>> lobbyDataChanges)
    {
        Debug.Log("Lobby custom data changed event received.");
        if (_currentLobby == null)
        {
            Debug.LogWarning("OnLobbyDataChanged: _currentLobby is null. This event should ideally not fire. Ignoring.");
            return;
        }

        // Check for countdown notification
        if (lobbyDataChanges.ContainsKey(LobbyDataKeyCountdownStarted))
        {
            HandleCountdownNotification(lobbyDataChanges[LobbyDataKeyCountdownStarted]);
        }

        // Log details for diagnostics
        foreach (var entry in lobbyDataChanges)
        {
            if (entry.Value.Removed)
            {
                Debug.Log($"  Detail: Lobby data key '{entry.Key}' was removed.");
            }
            else
            {
                Debug.Log($"  Detail: Lobby data key '{entry.Key}' was changed/added. Event value: {entry.Value.Value?.Value}");
            }
        }
        // Since _currentLobby.Data is a read-only dictionary with potentially immutable DataObjects after Lobby creation,
        // a change to any custom data necessitates a full refresh to get the new state.
        Debug.Log("OnLobbyDataChanged: Custom lobby data items changed. Initiating full lobby state fetch to ensure consistency.");
        RequestLobbyStateRefresh("LobbyDataChanged update");
    }

    private void HandleCountdownNotification(ChangedOrRemovedLobbyValue<DataObject> countdownChange)
    {
        if (countdownChange.Removed || countdownChange.Value == null)
        {
            return;
        }

        string countdownStarted = countdownChange.Value.Value;
        Debug.Log($"Received countdown status update: {countdownStarted}");

        // If we're not the host and countdown is starting, start local countdown
        if (!IsHost && countdownStarted == "true" && !countdownActive)
        {
            Debug.Log("Client starting local countdown based on lobby notification");
            countdownActive = true;
            countdownTimeRemaining = gameStartCountdownDuration;
            OnCountdownTick?.Invoke(countdownTimeRemaining);
        }
        // If countdown is cancelled
        else if (countdownStarted == "false" && countdownActive)
        {
            Debug.Log("Client cancelling local countdown based on lobby notification");
            countdownActive = false;
            countdownTimeRemaining = 0;
        }
    }

    private void OnPlayerDataChanged(Dictionary<int, Dictionary<string, ChangedOrRemovedLobbyValue<PlayerDataObject>>> changesByPlayerIndex)
    {
        Debug.Log("Player data changed event received.");
        if (_currentLobby == null)
        {
            Debug.LogWarning("OnPlayerDataChanged: _currentLobby is null. Requesting full fetch as a fallback.");
            RequestLobbyStateRefresh("PlayerDataChanged with null lobby");
            return;
        }

        // If a player's ready state changed to false, cancel countdown
        if (countdownActive && IsHost)
        {
            foreach (var playerIndexEntry in changesByPlayerIndex)
            {
                int playerIndex = playerIndexEntry.Key;
                if (playerIndex < 0 || playerIndex >= _currentLobby.Players.Count) continue;

                if (playerIndexEntry.Value.ContainsKey(PlayerDataKeyIsReady))
                {
                    var readyChange = playerIndexEntry.Value[PlayerDataKeyIsReady];
                    if (!readyChange.Removed && readyChange.Value != null)
                    {
                        bool isReady = false;
                        bool.TryParse(readyChange.Value.Value, out isReady);
                        if (!isReady)
                        {
                            CancelGameCountdown();
                            break;
                        }
                    }
                }
            }
        }

        bool successfullyPatched = true; // Assume success, set to false on issues

        foreach (var playerIndexEntry in changesByPlayerIndex)
        {
            int playerIndex = playerIndexEntry.Key;
            if (playerIndex < 0 || playerIndex >= _currentLobby.Players.Count)
            {
                Debug.LogWarning($"OnPlayerDataChanged: Invalid player index {playerIndex} received. Max index: {_currentLobby.Players.Count - 1}. Requesting full fetch for safety.");
                successfullyPatched = false;
                break;
            }

            Unity.Services.Lobbies.Models.Player playerToUpdate = _currentLobby.Players[playerIndex];

            if (playerToUpdate.Data == null)
            {
                // If Player.Data is null, we cannot apply changes to it directly.
                // UGS Player objects might initialize with a null Data dictionary if no data was ever set.
                // We can't assign a new dictionary if Player.Data property has a private setter.
                Debug.LogWarning($"OnPlayerDataChanged: Player {playerToUpdate.Id} (idx {playerIndex}): Player.Data is null. Cannot apply changes. Full refresh needed.");
                successfullyPatched = false;
                break;
            }

            foreach (var dataChange in playerIndexEntry.Value)
            {
                if (dataChange.Value.Removed)
                {
                    if (playerToUpdate.Data.Remove(dataChange.Key))
                    {
                        Debug.Log($" Player {playerToUpdate.Id} (idx {playerIndex}): Data key '{dataChange.Key}' removed.");
                    }
                    else
                    {
                        Debug.LogWarning($" Player {playerToUpdate.Id} (idx {playerIndex}): Attempted to remove data key '{dataChange.Key}' but it was not found.");
                    }
                }
                else
                {
                    // This assumes PlayerDataObject is directly assignable or its relevant parts are.
                    // And that the Player.Data dictionary allows item replacement.
                    playerToUpdate.Data[dataChange.Key] = dataChange.Value.Value;
                    Debug.Log($" Player {playerToUpdate.Id} (idx {playerIndex}): Data key '{dataChange.Key}' updated to: {dataChange.Value.Value?.Value}");
                }
            }
            if (!successfullyPatched) break; // If inner loop decided a refresh is needed
        }

        if (!successfullyPatched) // If any issue caused us to abort direct patching
        {
            RequestLobbyStateRefresh("PlayerDataChanged requiring full refresh due to patching issues");
        }
        else
        {
            BroadcastLobbyState(); // Broadcast updated local state if successfully patched
        }
    }

    // Check if all players are ready
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

    public async void StartGameCountdown()
    {
        if (!IsHost) return;

        Debug.Log("All players ready! Starting game countdown.");
        countdownActive = true;
        countdownTimeRemaining = gameStartCountdownDuration;
        OnCountdownTick?.Invoke(countdownTimeRemaining);

        // Update lobby data to notify all clients about countdown start
        try
        {
            var options = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    {
                        LobbyDataKeyCountdownStarted,
                        new DataObject(
                            visibility: DataObject.VisibilityOptions.Member,
                            value: "true"
                        )
                    }
                }
            };

            await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, options);
            Debug.Log("Lobby data updated: Countdown started notification sent");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to update lobby with countdown started notification: {e.Message}");
        }
    }

    // Cancel countdown if a player leaves or changes ready state
    public async void CancelGameCountdown()
    {
        if (!countdownActive) return;

        Debug.Log("Game countdown cancelled.");
        countdownActive = false;
        countdownTimeRemaining = 0;

        // Only the host should update the lobby data
        if (IsHost && _currentLobby != null)
        {
            try
            {
                var options = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        {
                            LobbyDataKeyCountdownStarted,
                            new DataObject(
                                visibility: DataObject.VisibilityOptions.Member,
                                value: "false"
                            )
                        }
                    }
                };

                await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, options);
                Debug.Log("Lobby data updated: Countdown cancelled notification sent");
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"Failed to update lobby with countdown cancelled notification: {e.Message}");
            }
        }
    }

    // Start the game after countdown completes
    private void StartGame()
    {
        // Only host can actually start the game
        if (!IsHost)
        {
            Debug.Log("Countdown complete on client. Waiting for host to start game...");
            return;
        }

        Debug.Log("Countdown complete! Host starting game...");
        // Logic to start the game - using your existing game manager
        networkGameManager.StartGame();
    }

    private void StartHeartbeat()
    {
        if (!IsHost || _currentLobby == null)
        {
            Debug.Log("Not starting heartbeat - either not host or no lobby exists");
            return;
        }

        Debug.Log($"Starting heartbeat for lobby {_currentLobby.Id}");
        StopHeartbeat(); // Ensure any existing heartbeat is stopped
        _heartbeatCoroutine = StartCoroutine(SendHeartbeatsPeriodically());
    }

    private void StopHeartbeat()
    {
        if (_heartbeatCoroutine != null)
        {
            Debug.Log("Stopping heartbeat");
            StopCoroutine(_heartbeatCoroutine);
            _heartbeatCoroutine = null;
        }
    }

    private IEnumerator SendHeartbeatsPeriodically()
    {
        while (IsHost && _currentLobby != null)
        {
            yield return new WaitForSeconds(HEARTBEAT_INTERVAL);

            if (_currentLobby == null || !IsHost)
            {
                Debug.Log("Lobby no longer exists or no longer host. Stopping heartbeat coroutine.");
                break;
            }

            try
            {
                // Fire and forget - we don't need to await this
                LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Debug.LogError($"Failed to send heartbeat: {task.Exception.GetBaseException().Message}");
                        // Optionally handle specific exceptions like LobbyNotFound differently
                    }
                    else
                    {
                        Debug.Log($"Heartbeat sent successfully for lobby {_currentLobby.Id}");
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"Error attempting to send heartbeat: {e.Message}");
            }
        }
    }

    // --- Cleanup ---
    private async void OnDestroy()
    {
        Debug.Log("PrivateMatchManager OnDestroy: Initiating cleanup.");
        // Stop heartbeat immediately to avoid any issues during cleanup
        StopHeartbeat();

        // Unsubscription from Lobby Events: The UGS documentation isn't perfectly clear on explicit unsubscription for ILobbyEvents.
        // It's often handled implicitly when the connection drops or the service instance is disposed.
        // For now, we set our flag and rely on SDK behavior or future clarification for explicit unsubscription.
        _subscribedToLobbyEvents = false;
        await LeaveLobbyAndCleanupAsync(isQuitting: true);

        OnLobbyStateBroadcast -= HandleLobbyStateUpdate;
    }

    public async Task LeaveLobbyAndCleanupAsync(bool isQuitting = false)
    {
        Debug.Log($"LeaveLobbyAndCleanupAsync called. IsQuitting: {isQuitting}");

        // Always stop the heartbeat when leaving a lobby
        StopHeartbeat();

        _subscribedToLobbyEvents = false; // Mark as not subscribed regardless of explicit unsubscribe call.

        string lobbyIdToLeave = _currentLobby?.Id; // Cache ID before _currentLobby is nulled

        if (_currentLobby != null && !string.IsNullOrEmpty(LocalPlayerId))
        {
            try
            {
                if (IsHost)
                {
                    Debug.Log($"Host leaving lobby {lobbyIdToLeave}. Deleting lobby.");
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyIdToLeave);
                    Debug.Log($"Lobby {lobbyIdToLeave} deleted by host.");
                }
                else
                {
                    Debug.Log($"Client {LocalPlayerId} leaving lobby {lobbyIdToLeave}. Removing player.");
                    await LobbyService.Instance.RemovePlayerAsync(lobbyIdToLeave, LocalPlayerId);
                    Debug.Log($"Player {LocalPlayerId} removed from lobby {lobbyIdToLeave}.");
                }
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.LobbyNotFound)
                {
                    Debug.LogWarning($"LeaveLobbyAndCleanupAsync: Lobby {lobbyIdToLeave} not found. Already deleted/left. {e.Message}");
                }
                else
                {
                    Debug.LogError($"LeaveLobbyAndCleanupAsync: LobbyServiceException for lobby {lobbyIdToLeave}: {e.Message}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"LeaveLobbyAndCleanupAsync: Unexpected error for lobby {lobbyIdToLeave}: {e.Message}");
            }
        }
        else if (_currentLobby != null && IsHost) // Host leaving but LocalPlayerId might be null if error occurred early
        {
            Debug.LogWarning($"Host leaving lobby {lobbyIdToLeave} but LocalPlayerId is null/empty. Attempting to delete lobby as host.");
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(lobbyIdToLeave);
                Debug.Log($"Lobby {lobbyIdToLeave} deleted by host (with null LocalPlayerId).");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error deleting lobby {lobbyIdToLeave} as host (with null LocalPlayerId): {e.Message}");
            }
        }
        else
        {
            Debug.Log("LeaveLobbyAndCleanupAsync: No current lobby with player/host context to actively leave/delete.");
        }

        // Final state reset
        _currentLobby = null;
        IsHost = false;
        _allocation = null;
        _joinAllocation = null;
        _relayJoinCode = null;
        BroadcastLobbyState(); // Ensure UI is cleared

        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost))
        {
            Debug.Log("LeaveLobbyAndCleanupAsync: Shutting down NetworkManager.");
            NetworkManager.Singleton.Shutdown();
        }
        if (_queuedFetchCoroutine != null) { StopCoroutine(_queuedFetchCoroutine); _queuedFetchCoroutine = null; }
    }

    private void Update()
    {
        // Only run countdown timer if active
        if (countdownActive && countdownTimeRemaining > 0)
        {
            countdownTimeRemaining -= Time.deltaTime;
            OnCountdownTick?.Invoke(countdownTimeRemaining);

            if (countdownTimeRemaining <= 0)
            {
                countdownActive = false;
                OnCountdownComplete?.Invoke();
                StartGame();
            }
        }
    }
}


