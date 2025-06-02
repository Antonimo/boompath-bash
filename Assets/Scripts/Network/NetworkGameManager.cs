using UnityEngine;
using Unity.Netcode;
using System.Collections;
using GameState;

/// <summary>
/// NetworkGameManager is a MID-LEVEL component that wraps GameManager with network-related operations.
/// 
/// Key Responsibilities:
/// - Network synchronization: Syncing relevant game state changes from host across all clients
/// - Game flow coordination: Coordinating distributed game flow (game start, pause, game over)
/// - Local player management: Finding and setting up the local player for GameManager
/// - Network event handling: Responding to network-relevant game state changes
/// - UI coordination: Managing game-related UI in response to network events
/// 
/// Architecture Position:
/// - This is a MIDDLE-LAYER component that bridges high-level match orchestration with low-level game mechanics
/// - Wraps and extends GameManager with network awareness and coordination
/// - Receives coordination commands from higher-level managers (PrivateMatchManager)
/// - Translates network events into local game actions and vice versa
/// - Focused on network-related game logic, not network setup or match orchestration
/// 
/// Separation of Concerns:
/// - PrivateMatchManager handles "how matches are set up" (lobbies, network setup, transitions)
/// - NetworkGameManager handles "how the game is networked" (sync, coordination, distributed flow)
/// - GameManager handles "what happens in the game" (local mechanics, player interactions)
/// 
/// Network vs Local Operations:
/// - Manages the network layer for game-specific operations (not match-level operations)
/// - Coordinates game state synchronization without knowing about lobby or match setup details
/// - Provides network-aware game control while keeping GameManager network-agnostic
/// </summary>
public class NetworkGameManager : NetworkBehaviour
{
    [Header("Client-side Managers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MenuManager menuManager;

    [Header("UI Panels")]
    [SerializeField] private MenuPanel gameOverPanel;

    [Header("Network Setup")]
    [SerializeField] private GameObject playersParent;
    [SerializeField] private PlayerSpawnManager playerSpawnManager;
    [SerializeField] private int requiredPlayerCount = 2;

    // Authoritative winner information from host
    private ulong winnerClientId = 0; // 0 means no winner/draw

    private void ValidateDependencies()
    {
        if (gameManager == null)
        {
            Debug.LogError("NetworkGameManager: gameManager is not assigned.");
            this.enabled = false;
        }

        if (menuManager == null)
        {
            Debug.LogError("NetworkGameManager: menuManager is not assigned.");
            this.enabled = false;
        }

        if (gameOverPanel == null)
        {
            Debug.LogError("NetworkGameManager: gameOverPanel is not assigned.");
            this.enabled = false;
        }

        if (playerSpawnManager == null)
        {
            Debug.LogError("NetworkGameManager: playerSpawnManager is not assigned.");
            this.enabled = false;
        }
    }

    private void Awake()
    {
        ValidateDependencies();
    }

    public override void OnNetworkSpawn()
    {
        // add all the states like IsClient IsServer IsHost, etc..
        Debug.Log($"NetworkGameManager: OnNetworkSpawn: IsClient: {IsClient}, IsServer: {IsServer}, IsHost: {IsHost}");

        if (IsServer)
        {
            Debug.Log("NetworkGameManager: Server spawned.");
            // Server waits for all players to be ready before setting up and launching the game
            // StartCoroutine(WaitForPlayersAndSetupGame());
        }
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from events to prevent memory leaks
        if (gameManager != null && gameManager.StateMachine != null)
        {
            gameManager.StateMachine.OnNetworkRelevantGameStateChanged -= OnNetworkRelevantGameStateChanged;
        }
    }

    public void SetupAndLaunchGame()
    {
        if (IsServer)
        {
            // TODO: do not despawn/respawn if its a new game and not a rematch

            playerSpawnManager.DespawnAllPlayers();

            SetupAndLaunchGameClientRpc();
        }
    }

    private IEnumerator WaitForPlayersAndSetupGame()
    {
        // Wait until all expected players are connected and spawned
        while (!AreAllPlayersReady())
        {
            yield return new WaitForSeconds(0.5f);
        }

        // All players are ready, setup and launch the game on all clients
        SetupAndLaunchGameClientRpc();
    }

    private bool AreAllPlayersReady()
    {
        if (playersParent == null) return false;

        Player[] players = playersParent.GetComponentsInChildren<Player>();
        bool hasEnoughPlayers = players.Length >= requiredPlayerCount;

        if (hasEnoughPlayers)
        {
            Debug.Log($"[NetworkGameManager] All players ready. Found {players.Length} players.");
        }

        return hasEnoughPlayers;
    }

    [ClientRpc]
    private void SetupAndLaunchGameClientRpc()
    {
        // TODO: review all the flows with this check, I was confused that IsHost is also IsClient
        if (!IsClient) return;

        Debug.Log("NetworkGameManager: SetupAndLaunchGameClientRpc - Beginning game setup and launch");

        // Player localPlayer = GetLocalPlayer();

        // if (localPlayer == null)
        // {
        //     Debug.LogError("Local player not found during game setup and launch!");
        //     return;
        // }

        SetupClientManagers();

        gameManager.LoadGame();

        menuManager.ClearAll();
    }

    private Player GetLocalPlayer()
    {
        if (playersParent == null) return null;

        Player[] players = playersParent.GetComponentsInChildren<Player>();
        foreach (Player player in players)
        {
            // Check if this player object is owned by the local client
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsOwner)
            {
                return player;
            }
        }

        return null;
    }

    private void SetupClientManagers()
    {
        // TODO: make sure to subscribe only if not already subscribed
        // TODO: make sure cleanup happens when relevant
        // Subscribe to network-relevant state changes
        gameManager.StateMachine.OnNetworkRelevantGameStateChanged += OnNetworkRelevantGameStateChanged;

        gameManager.IsHost = IsHost;
    }

    private void OnNetworkRelevantGameStateChanged(GameStateType fromState, GameStateType toState)
    {
        Debug.Log($"[NetworkGameManager] Network-relevant state change: {fromState} -> {toState}");

        switch (toState)
        {
            case GameStateType.GameOver:
                HandleGameOver();
                break;
            case GameStateType.Paused:
                HandleGamePaused();
                break;
            case GameStateType.WaitingForPlayers:
                HandleWaitingForPlayers();
                break;
        }
    }

    private void HandleGameOver()
    {
        Debug.Log("[NetworkGameManager] Handling GameOver state");

        if (IsServer)
        {
            // Host determines the winner authoritatively
            ulong winnerClientId = DetermineWinnerClientId();
            Debug.Log($"[NetworkGameManager] Host determined winner client ID: {winnerClientId}");

            // Broadcast game over with winner client ID to all clients
            GameOverClientRpc(winnerClientId);
        }

        // Show game over UI (both server and client)
        ShowGameOverUI();
    }

    /// <summary>
    /// Host-only method to authoritatively determine the winner by client ID
    /// </summary>
    /// <returns>Winner player's client ID or 0 if no winner/draw</returns>
    private ulong DetermineWinnerClientId()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkGameManager] DetermineWinnerClientId called on non-server!");
            return 0;
        }

        Player winnerPlayer = gameManager.WinnerPlayer;
        if (winnerPlayer != null)
        {
            return winnerPlayer.OwnerClientId;
        }

        return 0; // No winner (draw)
    }

    private void HandleGamePaused()
    {
        Debug.Log("[NetworkGameManager] Handling Paused state");

        if (IsServer)
        {
            // Broadcast pause to all clients
            GamePausedClientRpc();
        }

        // Show pause UI (both server and client)
        ShowPauseUI();
    }

    private void HandleWaitingForPlayers()
    {
        Debug.Log("[NetworkGameManager] Handling WaitingForPlayers state");

        // Request from host to spawn the player prefab
        if (IsClient)
        {
            Debug.Log("[NetworkGameManager] Client requesting player spawn from host");
            RequestPlayerSpawnServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerSpawnServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if (!IsServer) return;

        ulong clientId = serverRpcParams.Receive.SenderClientId;
        Debug.Log($"[NetworkGameManager] Host received player spawn request from client {clientId}");

        // Spawn/respawn the player for this specific client
        playerSpawnManager.RespawnPlayer(clientId);

        // After spawning, check if all players are now ready to start the game
        if (AreAllPlayersSpawned())
        {
            Debug.Log("[NetworkGameManager] All players spawned! Transitioning to GameStart state.");
            StartGameClientRpc();
        }
    }

    // TODO: streamline flow management into the Update method?
    // with checks for isCountdownOrGameActive for checking if can transition to GameStart?
    private bool AreAllPlayersSpawned()
    {
        if (playersParent == null) return false;

        Player[] players = playersParent.GetComponentsInChildren<Player>();
        bool hasEnoughPlayers = players.Length >= requiredPlayerCount;

        if (hasEnoughPlayers)
        {
            Debug.Log($"[NetworkGameManager] All players spawned. Found {players.Length} players.");
        }

        return hasEnoughPlayers;
    }

    [ClientRpc]
    private void StartGameClientRpc()
    {
        if (!IsClient) return;

        Debug.Log("[NetworkGameManager] Received transition to GameStart from server");

        // Get the local player now that all players are spawned
        Player localPlayer = GetLocalPlayer();

        if (localPlayer == null)
        {
            Debug.LogError("Local player not found when starting the game!");
            return;
        }

        // Force all clients to enter GameStart state
        if (gameManager != null && gameManager.CurrentState == GameStateType.WaitingForPlayers)
        {
            gameManager.SetupLocalPlayer(localPlayer);

            gameManager.StartGame();
        }
        else
        {
            Debug.LogWarning($"[NetworkGameManager] Cannot transition to GameStart - current state is {gameManager?.CurrentState}");
        }

        if (LobbyManager.Instance != null)
        {
            _ = LobbyManager.Instance.ClearLocalPlayerReadyState();
        }
    }

    [ClientRpc]
    private void GameOverClientRpc(ulong winnerClientId)
    {
        if (!IsClient) return;

        Debug.Log($"[NetworkGameManager] Received GameOver from server with winner client ID: {winnerClientId}");

        // Store the authoritative winner information
        this.winnerClientId = winnerClientId;

        // Force all clients to enter GameOver state if they haven't already
        if (gameManager != null && gameManager.CurrentState != GameStateType.GameOver)
        {
            gameManager.GameOver();
        }
    }

    // TODO: anyone should be able to pause the game?
    // TODO: but clients should request pause from the host?
    [ClientRpc]
    private void GamePausedClientRpc()
    {
        if (!IsClient) return;

        Debug.Log("[NetworkGameManager] Received GamePaused from server");

        // Force all clients to enter Paused state if they haven't already
        if (gameManager != null && gameManager.CurrentState != GameStateType.Paused)
        {
            gameManager.Pause();
        }
    }

    private void ShowGameOverUI()
    {
        Debug.Log("[NetworkGameManager] Showing GameOver UI");

        menuManager.OpenMenuPanel(gameOverPanel);
    }

    private void ShowPauseUI()
    {
        // TODO: Show pause menu panel
        Debug.Log("[NetworkGameManager] Showing Pause UI");

        // Example: menuManager.ShowPausePanel();
    }


    /// <summary>
    /// Gets the authoritative winner player name resolved from the host-determined client ID
    /// </summary>
    /// <returns>Winner player name or empty string if no winner/draw</returns>
    public string GetWinnerPlayerName()
    {
        if (winnerClientId == 0)
        {
            return ""; // No winner / draw
        }

        // Resolve the player name from the authoritative client ID
        Player winnerPlayer = GetPlayerByClientId(winnerClientId);
        if (winnerPlayer != null)
        {
            return winnerPlayer.playerName;
        }

        Debug.LogWarning($"[NetworkGameManager] Could not resolve winner player name for client ID: {winnerClientId}");
        return "Unknown Player";
    }

    /// <summary>
    /// Helper method to find a Player by their client ID
    /// </summary>
    /// <param name="clientId">The client ID to search for</param>
    /// <returns>Player component or null if not found</returns>
    private Player GetPlayerByClientId(ulong clientId)
    {
        if (playersParent == null) return null;

        Player[] players = playersParent.GetComponentsInChildren<Player>();
        foreach (Player player in players)
        {
            if (player.OwnerClientId == clientId)
            {
                return player;
            }
        }

        return null;
    }
}