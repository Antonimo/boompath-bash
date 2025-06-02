using System;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Singleton service that manages all Unity Game Services (UGS) Lobby functionality.
/// This service handles authentication, lobby creation/joining, event subscription, 
/// and state broadcasting without requiring polling from lobby servers.
/// 
/// Key Design Principles:
/// - Uses UGS lobby events for real-time updates, eliminating the need for polling
/// - Maintains local lobby state that is kept up-to-date via event subscription
/// - Broadcasts lobby state changes to UI components and game logic
/// - Provides a centralized interface for all lobby operations
/// </summary>
public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    // Events for broadcasting lobby state changes to subscribers
    // This allows UI components and game logic to react to lobby changes without polling
    // TODO: refactor OnLobbyStateBroadcast to use a struct or something for readability?
    // TODO: why static?
    public static event Action<List<LobbyPlayerData>, string, bool> OnLobbyStateBroadcast; // PlayerList, LocalPlayerId, IsLocalPlayerHost
    public static event Action OnLobbyDeleted; // When lobby is deleted or we're kicked
    public static event Action<string> OnLobbyError; // For error handling
    public static event Action OnPlayerLeft; // When any player leaves the lobby
    public static event Action OnPlayerReadyStateChanged; // When any player's ready state changes

    // Lobby state properties
    public string LobbyCode => _currentLobby?.LobbyCode;
    public string LocalPlayerId { get; private set; }
    // TODO: how is this managed?
    // TODO: use Lobby.HostId instead?
    public bool IsHost { get; private set; }
    public bool IsInLobby => _currentLobby != null;

    // UGS Lobby objects and state
    private Lobby _currentLobby;
    private LobbyEventCallbacks _lobbyEventCallbacks;
    private bool _subscribedToLobbyEvents = false;

    // Heartbeat management for hosts
    // TODO: make sure only host sends heartbeats
    private Coroutine _heartbeatCoroutine;
    private const float HEARTBEAT_INTERVAL = 15f; // Send heartbeat every 15 seconds (well within the 30s requirement)

    // Constants for UGS Player Data keys
    private const string PlayerDataKeyDisplayName = "DisplayName";
    private const string PlayerDataKeyIsReady = "IsPlayerReady";

    // Constants for UGS Lobby Data keys
    private const string LobbyDataKeyCountdownStarted = "CountdownStarted";

    // Countdown management
    [Header("Game Start Settings")]
    [SerializeField] private float gameStartCountdownDuration = 3f; // Countdown in seconds before game starts
    private bool countdownActive = false;
    private float countdownTimeRemaining = 0f;

    // TODO: why static?
    // TODO: should I use singleton instead?
    // Countdown events for UI to display
    public static event Action<float> OnCountdownTick; // Countdown remaining seconds
    public static event Action OnCountdownComplete; // When countdown reaches zero

    #region Singleton Setup
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAsync();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            CleanupOnDestroy();
        }
    }
    #endregion

    #region Initialization and Authentication
    private async void InitializeAsync()
    {
        try
        {
            // TODO: move to its own service?
            await UnityServices.InitializeAsync();
            Debug.Log("LobbyManager: UGS Initialized successfully.");
            await SignInAnonymouslyAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"LobbyManager: Failed to initialize UGS: {e.Message}");
            OnLobbyError?.Invoke($"Failed to initialize: {e.Message}");
        }
    }

    private async Task SignInAnonymouslyAsync()
    {
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            LocalPlayerId = AuthenticationService.Instance.PlayerId;
            Debug.Log($"LobbyManager: Player signed in. PlayerID: {LocalPlayerId}");
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"LobbyManager: Sign in failed: {ex.Message}");
            OnLobbyError?.Invoke($"Sign in failed: {ex.Message}");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogError($"LobbyManager: Sign in request failed: {ex.Message}");
            OnLobbyError?.Invoke($"Sign in request failed: {ex.Message}");
        }
    }
    #endregion

    #region Lobby Creation and Joining
    public async Task<string> CreateLobbyAsync(string lobbyName, bool isPrivate, int maxPlayers = 2)
    {
        if (string.IsNullOrEmpty(lobbyName))
        {
            lobbyName = "My Private Match"; // Default lobby name
        }

        // TODO: im not sure about this. Is there a way to get lobby owner?
        IsHost = true;

        try
        {
            var createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
            };

            Debug.Log($"LobbyManager: Creating lobby: {lobbyName}, Max Players: {maxPlayers}, Private: {isPrivate}");
            _currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);

            Debug.Log($"LobbyManager: Lobby created successfully! Name: {_currentLobby.Name}, ID: {_currentLobby.Id}, Lobby Code: {LobbyCode}");

            await InitializePlayerDefaultData();
            await SubscribeToLobbyEvents();
            StartHeartbeat();

            // Broadcast initial state - no polling needed as we have the fresh lobby data
            BroadcastLobbyState();

            return LobbyCode;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"LobbyManager: Failed to create lobby: {e.Message}");
            await CleanupOnError("LobbyServiceException during create");
            OnLobbyError?.Invoke($"Failed to create lobby: {e.Message}");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"LobbyManager: An unexpected error occurred while creating lobby: {e.Message}");
            await CleanupOnError("Unexpected error during create");
            OnLobbyError?.Invoke($"An unexpected error occurred: {e.Message}");
            return null;
        }
    }

    public async Task<bool> JoinLobbyByCodeAsync(string lobbyCode)
    {
        if (string.IsNullOrEmpty(lobbyCode))
        {
            Debug.LogError("LobbyManager: Lobby code cannot be null or empty.");
            OnLobbyError?.Invoke("Lobby code cannot be empty.");
            return false;
        }

        IsHost = false;

        try
        {
            Debug.Log($"LobbyManager: Attempting to join lobby with code: {lobbyCode}");
            JoinLobbyByCodeOptions joinOptions = new JoinLobbyByCodeOptions();

            _currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinOptions);

            Debug.Log($"LobbyManager: Successfully joined lobby! Name: {_currentLobby.Name}, ID: {_currentLobby.Id}");

            await InitializePlayerDefaultData();
            await SubscribeToLobbyEvents();

            // Broadcast initial state - no polling needed as we have the fresh lobby data
            BroadcastLobbyState();

            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"LobbyManager: Failed to join lobby with code '{lobbyCode}': {e.Message}");

            string errorMessage = e.Reason switch
            {
                LobbyExceptionReason.LobbyNotFound => "Lobby not found. Please check the code and try again.",
                LobbyExceptionReason.LobbyFull => "The lobby is full.",
                _ => $"Failed to join lobby: {e.Message}"
            };

            await CleanupOnError($"LobbyServiceException during join: {e.Reason}");
            OnLobbyError?.Invoke(errorMessage);
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"LobbyManager: An unexpected error occurred while joining lobby with code '{lobbyCode}': {e.Message}");
            await CleanupOnError("Unexpected error during join");
            OnLobbyError?.Invoke($"An unexpected error occurred: {e.Message}");
            return false;
        }
    }
    #endregion

    #region Player Data Management
    private async Task InitializePlayerDefaultData()
    {
        if (_currentLobby == null || string.IsNullOrEmpty(LocalPlayerId))
        {
            Debug.LogWarning("LobbyManager: Cannot initialize player data: Not in a lobby or LocalPlayerId is missing.");
            return;
        }

        // TODO: DRY with BroadcastLobbyState?
        string displayName = PlayerPrefs.GetString("PlayerName", $"Player{LocalPlayerId.Substring(0, 3)}");

        UpdatePlayerOptions options = new UpdatePlayerOptions
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { PlayerDataKeyDisplayName, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, displayName) },
                { PlayerDataKeyIsReady, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "false") }
            }
        };

        try
        {
            _currentLobby = await LobbyService.Instance.UpdatePlayerAsync(_currentLobby.Id, LocalPlayerId, options);
            Debug.Log($"LobbyManager: Initialized default data for player {LocalPlayerId} (DisplayName: {displayName}, Ready: false).");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"LobbyManager: Failed to initialize player default data in UGS for {LocalPlayerId}: {e}");
        }
    }

    public async Task ToggleLocalPlayerReadyState()
    {
        // TODO: get LocalPlayerId directly from NetworkManager?
        if (_currentLobby == null || string.IsNullOrEmpty(LocalPlayerId))
        {
            Debug.LogWarning("LobbyManager: Cannot toggle ready state: Not in a lobby or LocalPlayerId is missing.");
            return;
        }

        var localUgsPlayer = _currentLobby.Players.Find(p => p.Id == LocalPlayerId);
        if (localUgsPlayer == null)
        {
            Debug.LogError("LobbyManager: Local UGS Player not found in lobby to toggle ready state.");
            return;
        }

        bool currentReadyState = false;
        if (localUgsPlayer.Data != null && localUgsPlayer.Data.TryGetValue(PlayerDataKeyIsReady, out var readyData))
        {
            bool.TryParse(readyData.Value, out currentReadyState);
        }
        bool newReadyState = !currentReadyState;
        Debug.Log($"LobbyManager: Toggling ready state for {LocalPlayerId} from {currentReadyState} to {newReadyState}");

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
            Debug.Log($"LobbyManager: Player {LocalPlayerId} ready state updated in UGS to: {newReadyState}");

            // Broadcast state immediately - no need to poll as we have the updated lobby data
            BroadcastLobbyState();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"LobbyManager: Failed to update player ready state in UGS: {e}");
            OnLobbyError?.Invoke($"Failed to update ready state: {e.Message}");
        }
    }

    public async Task ClearLocalPlayerReadyState()
    {
        if (_currentLobby == null || string.IsNullOrEmpty(LocalPlayerId))
        {
            Debug.LogWarning("LobbyManager: Cannot clear ready state: Not in a lobby or LocalPlayerId is missing.");
            return;
        }

        UpdatePlayerOptions options = new UpdatePlayerOptions
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { PlayerDataKeyIsReady, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "false") }
            }
        };

        try
        {
            _currentLobby = await LobbyService.Instance.UpdatePlayerAsync(_currentLobby.Id, LocalPlayerId, options);
            Debug.Log($"LobbyManager: Local player ready state cleared");
            BroadcastLobbyState();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"LobbyManager: Failed to clear local player ready state: {e}");
        }
    }

    public async Task UpdatePlayerDisplayNameAsync(string newName)
    {
        if (string.IsNullOrEmpty(LocalPlayerId) || string.IsNullOrEmpty(newName))
        {
            Debug.LogWarning("LobbyManager: Cannot update display name: LocalPlayerId or newName is missing.");
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
                Debug.Log($"LobbyManager: Player {LocalPlayerId} display name updated to: {newName} in lobby {_currentLobby.Id}");

                // Broadcast state immediately - no need to poll
                BroadcastLobbyState();
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"LobbyManager: Failed to update player display name in UGS: {e}");
                OnLobbyError?.Invoke($"Failed to update display name: {e.Message}");
            }
        }
        else
        {
            // If not in a lobby, store it in PlayerPrefs to be used when joining/creating next lobby
            PlayerPrefs.SetString("PlayerName", newName);
            PlayerPrefs.Save();
            Debug.Log($"LobbyManager: Player display name preference saved: {newName}");
        }
    }
    #endregion

    #region Lobby Data Updates
    public async Task UpdateLobbyDataAsync(Dictionary<string, DataObject> data)
    {
        if (_currentLobby == null || !IsHost)
        {
            Debug.LogWarning("LobbyManager: Cannot update lobby data: Not in a lobby or not the host.");
            return;
        }

        try
        {
            var options = new UpdateLobbyOptions { Data = data };
            _currentLobby = await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, options);
            Debug.Log("LobbyManager: Lobby data updated successfully");

            // Since the host doesn't receive their own DataChanged events, we need to manually
            // process the lobby data changes that were just applied
            ProcessLobbyDataChangesForHost(data);

            // Broadcast lobby state to update UI and notify subscribers
            BroadcastLobbyState();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"LobbyManager: Failed to update lobby data: {e.Message}");
            OnLobbyError?.Invoke($"Failed to update lobby data: {e.Message}");
        }
    }

    /// <summary>
    /// Processes lobby data changes for the host since they don't receive their own DataChanged events.
    /// This ensures the host triggers the same logic that clients receive via events.
    /// </summary>
    private void ProcessLobbyDataChangesForHost(Dictionary<string, DataObject> data)
    {
        // Handle countdown notifications manually for the host
        if (data.ContainsKey(LobbyDataKeyCountdownStarted))
        {
            ProcessCountdownDataChange(data[LobbyDataKeyCountdownStarted].Value);
        }
    }

    /// <summary>
    /// Common method to process countdown data changes.
    /// Used by both event handlers and manual host processing.
    /// </summary>
    private void ProcessCountdownDataChange(string countdownValue)
    {
        Debug.Log($"LobbyManager: Processing countdown status update: {countdownValue}");

        // All clients (including host) react to countdown notifications the same way
        if (countdownValue == "true" && !countdownActive)
        {
            Debug.Log("LobbyManager: Starting local countdown based on notification");
            countdownActive = true;
            countdownTimeRemaining = gameStartCountdownDuration;
            OnCountdownTick?.Invoke(countdownTimeRemaining);
        }
        // If countdown is cancelled
        else if (countdownValue == "false" && countdownActive)
        {
            Debug.Log("LobbyManager: Cancelling local countdown based on notification");
            countdownActive = false;
            countdownTimeRemaining = 0;
        }
        else
        {
            Debug.LogWarning($"LobbyManager: Invalid countdown value received: {countdownValue} countdownActive: {countdownActive}");
        }
    }

    public string GetLobbyData(string key)
    {
        if (_currentLobby?.Data != null && _currentLobby.Data.TryGetValue(key, out DataObject dataObject))
        {
            return dataObject.Value;
        }
        return null;
    }
    #endregion

    // TODO: lobby broadcasting vs PrivateMatch?
    #region State Broadcasting
    /// <summary>
    /// Broadcasts the current lobby state to all subscribers.
    /// This method assumes the current lobby state is up-to-date because:
    /// 1. We use UGS lobby events to keep our local state synchronized
    /// 2. We update the state immediately after successful operations
    /// 3. We don't need to poll the server as events provide real-time updates
    /// 
    /// UI components can call RequestLobbyStateBroadcast() when they need
    /// the current state (e.g., when OnEnable is called).
    /// </summary>
    public void RequestLobbyStateBroadcast()
    {
        BroadcastLobbyState();
    }

    private void BroadcastLobbyState()
    {
        Debug.Log("LobbyManager: Broadcasting lobby state.");

        if (_currentLobby == null)
        {
            // TODO: broadcast no lobby?
            OnLobbyStateBroadcast?.Invoke(new List<LobbyPlayerData>(), LocalPlayerId, IsHost);
            Debug.Log("LobbyManager: Current lobby is null. Broadcasted empty player list.");
            return;
        }

        if (string.IsNullOrEmpty(LocalPlayerId))
        {
            Debug.LogWarning("LobbyManager: LocalPlayerId is not set, but _currentLobby exists. This might indicate an issue with sign-in or initialization order.");
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

            string displayName = $"Player{ugsPlayer.Id.Substring(0, 3)}";
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
        IsHost = _currentLobby.HostId == LocalPlayerId;

        OnLobbyStateBroadcast?.Invoke(playersData, LocalPlayerId, IsHost);
        Debug.Log($"LobbyManager: Broadcasted Lobby State: {playersData.Count} players. LocalPlayerId: {LocalPlayerId}, IsHost: {IsHost}");
    }
    #endregion

    #region Lobby Event Subscription and Handling
    private async Task SubscribeToLobbyEvents()
    {
        // TODO: why need _subscribedToLobbyEvents if already have _lobbyEventCallbacks?
        if (_currentLobby == null || _subscribedToLobbyEvents)
        {
            if (_subscribedToLobbyEvents) Debug.Log("LobbyManager: Already subscribed to lobby events.");
            else Debug.LogWarning("LobbyManager: Cannot subscribe to lobby events: Current lobby is null.");
            return;
        }

        _lobbyEventCallbacks = new LobbyEventCallbacks();
        _lobbyEventCallbacks.LobbyDeleted += OnLobbyDeletedEvent;
        _lobbyEventCallbacks.KickedFromLobby += OnKickedFromLobbyEvent;
        _lobbyEventCallbacks.PlayerJoined += OnPlayersJoinedEvent;
        _lobbyEventCallbacks.PlayerLeft += OnPlayersLeftEvent;
        _lobbyEventCallbacks.LobbyChanged += OnLobbyChangedEvent;
        _lobbyEventCallbacks.DataChanged += OnLobbyDataChangedEvent;
        _lobbyEventCallbacks.PlayerDataChanged += OnPlayerDataChangedEvent;

        try
        {
            await LobbyService.Instance.SubscribeToLobbyEventsAsync(_currentLobby.Id, _lobbyEventCallbacks);
            _subscribedToLobbyEvents = true;
            Debug.Log($"LobbyManager: Successfully subscribed to lobby events for lobby ID: {_currentLobby.Id}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"LobbyManager: Error subscribing to lobby events: {e}");
            _subscribedToLobbyEvents = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"LobbyManager: Unexpected error subscribing to lobby events: {e}");
            _subscribedToLobbyEvents = false;
        }
    }

    private void OnLobbyDeletedEvent()
    {
        Debug.Log("LobbyManager: Lobby deleted event received.");
        StopHeartbeat();
        _currentLobby = null;
        IsHost = false;
        _subscribedToLobbyEvents = false;
        BroadcastLobbyState();
        OnLobbyDeleted?.Invoke();
    }

    private void OnKickedFromLobbyEvent()
    {
        Debug.Log("LobbyManager: Kicked from lobby event received.");
        // TODO: DRY cleanup
        StopHeartbeat();
        _currentLobby = null;
        IsHost = false;
        _subscribedToLobbyEvents = false;
        BroadcastLobbyState();
        OnLobbyDeleted?.Invoke();
    }

    private void OnPlayersJoinedEvent(List<LobbyPlayerJoined> playersJoined)
    {
        Debug.Log($"LobbyManager: Player(s) joined event received. Count: {playersJoined.Count}");
        if (_currentLobby == null)
        {
            Debug.LogError("LobbyManager: OnPlayersJoined: _currentLobby is null.");
            return;
        }

        // Events only provide change notifications, not updated lobby state
        // We must manually patch our local _currentLobby snapshot to stay in sync
        foreach (var playerJoinInfo in playersJoined)
        {
            if (_currentLobby.Players.Exists(p => p.Id == playerJoinInfo.Player.Id))
            {
                Debug.LogWarning($"LobbyManager: Player {playerJoinInfo.Player.Id} already in local lobby. This might indicate desync or a redundant event.");
            }
            else
            {
                _currentLobby.Players.Add(playerJoinInfo.Player);
                Debug.Log($"LobbyManager: Player {playerJoinInfo.Player.Id} added to local lobby from PlayerJoined event.");
            }
        }
        BroadcastLobbyState();
    }

    private void OnPlayersLeftEvent(List<int> playerIndexesLeft)
    {
        Debug.Log($"LobbyManager: Player(s) left event received. Indices count: {playerIndexesLeft.Count}");
        if (_currentLobby == null)
        {
            Debug.LogWarning("LobbyManager: OnPlayersLeft: _currentLobby is null.");
            return;
        }

        // Sort indices in descending order to safely remove elements
        playerIndexesLeft.Sort((a, b) => b.CompareTo(a));

        // Events only provide change notifications, not updated lobby state
        // We must manually patch our local _currentLobby snapshot to stay in sync
        foreach (var index in playerIndexesLeft)
        {
            if (index >= 0 && index < _currentLobby.Players.Count)
            {
                var removedPlayer = _currentLobby.Players[index];
                _currentLobby.Players.RemoveAt(index);
                Debug.Log($"LobbyManager: Player {removedPlayer.Id} (at index {index}) removed from local lobby based on PlayerLeft event.");
            }
            else
            {
                Debug.LogWarning($"LobbyManager: Invalid player index {index} received. Current player count: {_currentLobby.Players.Count}.");
            }
        }

        // Notify subscribers that a player left (for countdown cancellation logic)
        OnPlayerLeft?.Invoke();
        BroadcastLobbyState();
    }

    private void OnLobbyChangedEvent(ILobbyChanges changes)
    {
        Debug.Log("LobbyManager: LobbyChanged event received.");
        if (_currentLobby == null)
        {
            Debug.LogWarning("LobbyManager: OnLobbyChanged: _currentLobby is null.");
            return;
        }

        // Log what changed for diagnostic purposes
        if (changes.Name.Changed) Debug.Log($"LobbyManager: Lobby Name changed to: {changes.Name.Value}");
        if (changes.HostId.Changed) Debug.Log($"LobbyManager: Lobby HostId changed to: {changes.HostId.Value}");
        if (changes.IsPrivate.Changed) Debug.Log($"LobbyManager: Lobby IsPrivate changed to: {changes.IsPrivate.Value}");
        if (changes.IsLocked.Changed) Debug.Log($"LobbyManager: Lobby IsLocked changed to: {changes.IsLocked.Value}");
        if (changes.AvailableSlots.Changed) Debug.Log($"LobbyManager: Lobby AvailableSlots changed to: {changes.AvailableSlots.Value}");
        if (changes.MaxPlayers.Changed) Debug.Log($"LobbyManager: Lobby MaxPlayers changed to: {changes.MaxPlayers.Value}");

        // Note: Since UGS Lobby properties are read-only, we rely on the events to notify us of changes
        // The actual updated lobby object will come through subsequent events or we can wait for
        // the next successful operation that returns an updated lobby object
        Debug.Log("LobbyManager: Lobby properties changed - relying on subsequent events for updates");
    }

    private void OnLobbyDataChangedEvent(Dictionary<string, ChangedOrRemovedLobbyValue<DataObject>> lobbyDataChanges)
    {
        Debug.Log("LobbyManager: Lobby custom data changed event received.");
        if (_currentLobby == null)
        {
            Debug.LogWarning("LobbyManager: OnLobbyDataChanged: _currentLobby is null.");
            return;
        }

        // TODO: update local lobby data?
        foreach (var entry in lobbyDataChanges)
        {
            if (entry.Value.Removed)
            {
                Debug.Log($"LobbyManager: Lobby data key '{entry.Key}' was removed.");
            }
            else
            {
                Debug.Log($"LobbyManager: Lobby data key '{entry.Key}' was changed/added. Event value: {entry.Value.Value?.Value}");
            }
        }

        // Handle countdown notifications
        if (lobbyDataChanges.ContainsKey(LobbyDataKeyCountdownStarted))
        {
            HandleCountdownNotification(lobbyDataChanges[LobbyDataKeyCountdownStarted]);
        }

        // Note: We rely on the event data and subsequent operations to keep our state updated
        // The lobby object's Data property is read-only, so we work with the event information
        Debug.Log("LobbyManager: Lobby data changes processed via events");
    }

    private void OnPlayerDataChangedEvent(Dictionary<int, Dictionary<string, ChangedOrRemovedLobbyValue<PlayerDataObject>>> changesByPlayerIndex)
    {
        Debug.Log("LobbyManager: Player data changed event received.");
        if (_currentLobby == null)
        {
            Debug.LogWarning("LobbyManager: OnPlayerDataChanged: _currentLobby is null.");
            return;
        }

        bool successfullyPatched = true;
        bool readyStateChanged = false;

        // Events only provide change notifications, not updated lobby state
        // We must manually patch our local _currentLobby snapshot to stay in sync
        foreach (var playerIndexEntry in changesByPlayerIndex)
        {
            int playerIndex = playerIndexEntry.Key;
            if (playerIndex < 0 || playerIndex >= _currentLobby.Players.Count)
            {
                Debug.LogWarning($"LobbyManager: Invalid player index {playerIndex} received. Max index: {_currentLobby.Players.Count - 1}.");
                successfullyPatched = false;
                break;
            }

            var playerToUpdate = _currentLobby.Players[playerIndex];

            if (playerToUpdate.Data == null)
            {
                Debug.LogWarning($"LobbyManager: Player {playerToUpdate.Id} (idx {playerIndex}): Player.Data is null. Cannot apply changes.");
                successfullyPatched = false;
                break;
            }

            foreach (var dataChange in playerIndexEntry.Value)
            {
                if (dataChange.Value.Removed)
                {
                    if (playerToUpdate.Data.Remove(dataChange.Key))
                    {
                        Debug.Log($"LobbyManager: Player {playerToUpdate.Id} (idx {playerIndex}): Data key '{dataChange.Key}' removed.");
                    }
                }
                else
                {
                    playerToUpdate.Data[dataChange.Key] = dataChange.Value.Value;
                    Debug.Log($"LobbyManager: Player {playerToUpdate.Id} (idx {playerIndex}): Data key '{dataChange.Key}' updated to: {dataChange.Value.Value?.Value}");

                    // Check if this was a ready state change
                    if (dataChange.Key == PlayerDataKeyIsReady)
                    {
                        readyStateChanged = true;
                    }
                }
            }
        }

        if (successfullyPatched)
        {
            BroadcastLobbyState();

            // Notify subscribers that player ready state changed (for countdown logic)
            if (readyStateChanged)
            {
                OnPlayerReadyStateChanged?.Invoke();
            }
        }
        else
        {
            Debug.LogWarning("LobbyManager: Could not apply all player data changes via events - some updates may have been missed");
        }
    }
    #endregion

    #region Heartbeat Management
    private void StartHeartbeat()
    {
        if (!IsHost || _currentLobby == null)
        {
            Debug.Log("LobbyManager: Not starting heartbeat - either not host or no lobby exists");
            return;
        }

        Debug.Log($"LobbyManager: Starting heartbeat for lobby {_currentLobby.Id}");
        StopHeartbeat();
        _heartbeatCoroutine = StartCoroutine(SendHeartbeatsPeriodically());
    }

    private void StopHeartbeat()
    {
        if (_heartbeatCoroutine != null)
        {
            Debug.Log("LobbyManager: Stopping heartbeat");
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
                Debug.Log("LobbyManager: Lobby no longer exists or no longer host. Stopping heartbeat coroutine.");
                break;
            }

            try
            {
                LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Debug.LogError($"LobbyManager: Failed to send heartbeat: {task.Exception.GetBaseException().Message}");
                    }
                    else
                    {
                        Debug.Log($"LobbyManager: Heartbeat sent successfully for lobby {_currentLobby.Id}");
                        BroadcastLobbyState();
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbyManager: Error attempting to send heartbeat: {e.Message}");
            }
        }
    }
    #endregion

    #region Cleanup
    public async Task LeaveLobbyAsync()
    {
        Debug.Log("LobbyManager: Leaving lobby and cleaning up.");

        StopHeartbeat();
        _subscribedToLobbyEvents = false;

        string lobbyIdToLeave = _currentLobby?.Id;

        if (_currentLobby != null && !string.IsNullOrEmpty(LocalPlayerId))
        {
            try
            {
                if (IsHost)
                {
                    Debug.Log($"LobbyManager: Host leaving lobby {lobbyIdToLeave}. Deleting lobby.");
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyIdToLeave);
                    Debug.Log($"LobbyManager: Lobby {lobbyIdToLeave} deleted by host.");
                }
                else
                {
                    Debug.Log($"LobbyManager: Client {LocalPlayerId} leaving lobby {lobbyIdToLeave}. Removing player.");
                    await LobbyService.Instance.RemovePlayerAsync(lobbyIdToLeave, LocalPlayerId);
                    Debug.Log($"LobbyManager: Player {LocalPlayerId} removed from lobby {lobbyIdToLeave}.");
                }
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.LobbyNotFound)
                {
                    Debug.LogWarning($"LobbyManager: Lobby {lobbyIdToLeave} not found. Already deleted/left.");
                }
                else
                {
                    Debug.LogError($"LobbyManager: LobbyServiceException for lobby {lobbyIdToLeave}: {e.Message}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbyManager: Unexpected error for lobby {lobbyIdToLeave}: {e.Message}");
            }
        }

        // Final state reset
        _currentLobby = null;
        IsHost = false;
        BroadcastLobbyState();
    }

    private async Task CleanupOnError(string context)
    {
        Debug.LogError($"LobbyManager: Cleaning up due to error: {context}");
        if (_currentLobby != null)
        {
            try
            {
                Debug.LogWarning($"LobbyManager: Attempting to delete lobby {_currentLobby.Id} due to error: {context}");
                await LobbyService.Instance.DeleteLobbyAsync(_currentLobby.Id);
                Debug.Log($"LobbyManager: Lobby {_currentLobby.Id} deleted after error.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"LobbyManager: Failed to delete lobby {_currentLobby.Id} during error cleanup: {ex.Message}");
            }
        }

        _currentLobby = null;
        IsHost = false;
        BroadcastLobbyState();
    }

    private void CleanupOnDestroy()
    {
        StopHeartbeat();
        _subscribedToLobbyEvents = false;

        // Note: We don't await this as OnDestroy cannot be async
        // The Unity application is shutting down anyway
        if (_currentLobby != null)
        {
            _ = LeaveLobbyAsync();
        }
    }
    #endregion

    #region Countdown Management
    private void HandleCountdownNotification(ChangedOrRemovedLobbyValue<DataObject> countdownChange)
    {
        Debug.Log($"LobbyManager: HandleCountdownNotification: {countdownChange.Removed} {countdownChange.Value?.Value}");

        if (countdownChange.Removed || countdownChange.Value == null)
        {
            return;
        }

        if (countdownActive && countdownChange.Value.Value == "true")
        {
            Debug.LogError("LobbyManager: Countdown already active");
        }

        // Use the DRY helper method for the actual countdown processing
        ProcessCountdownDataChange(countdownChange.Value.Value);
    }

    public async Task StartGameCountdown()
    {
        if (!IsHost) return;

        // Prevent multiple countdown start attempts
        if (countdownActive)
        {
            Debug.Log("LobbyManager: Countdown already active, skipping duplicate start request.");
            return;
        }

        Debug.Log("LobbyManager: All players ready! Host updating lobby data to start countdown.");

        // Host only updates lobby data - does NOT start local countdown
        // All clients (including host) will react to the lobby data change
        try
        {
            var countdownData = new Dictionary<string, DataObject>
            {
                {
                    LobbyDataKeyCountdownStarted,
                    new DataObject(
                        visibility: DataObject.VisibilityOptions.Member,
                        value: "true"
                    )
                }
            };

            await UpdateLobbyDataAsync(countdownData);
            Debug.Log("LobbyManager: Lobby data updated: Countdown started notification sent");
        }
        catch (Exception e)
        {
            Debug.LogError($"LobbyManager: Failed to update lobby with countdown started notification: {e.Message}");
        }
    }

    public async Task CancelGameCountdown()
    {
        if (!countdownActive) return;

        Debug.Log("LobbyManager: Game countdown cancelled.");
        countdownActive = false;
        countdownTimeRemaining = 0;

        // Only the host should update the lobby data
        if (IsHost)
        {
            try
            {
                var countdownData = new Dictionary<string, DataObject>
                {
                    {
                        LobbyDataKeyCountdownStarted,
                        new DataObject(
                            visibility: DataObject.VisibilityOptions.Member,
                            value: "false"
                        )
                    }
                };

                await UpdateLobbyDataAsync(countdownData);
                Debug.Log("LobbyManager: Lobby data updated: Countdown cancelled notification sent");
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbyManager: Failed to update lobby with countdown cancelled notification: {e.Message}");
            }
        }
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
            }
        }
    }
    #endregion
}