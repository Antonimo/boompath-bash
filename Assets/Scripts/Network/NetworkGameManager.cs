using UnityEngine;
using Unity.Netcode;
using System.Collections;
using GameState;

public class NetworkGameManager : NetworkBehaviour
{
    [Header("Client-side Managers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MenuManager menuManager;

    [Header("UI Panels")]
    [SerializeField] private MenuPanel gameOverPanel;

    [Header("Network Setup")]
    [SerializeField] private GameObject playersParent;
    [SerializeField] private int requiredPlayerCount = 2;

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
            // Server waits for all players to be ready before starting the game
            // StartCoroutine(WaitForPlayersAndStartGame());
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

    public void StartGame()
    {
        if (IsServer)
        {
            StartGameClientRpc();
        }
    }

    private IEnumerator WaitForPlayersAndStartGame()
    {
        // Wait until all expected players are connected and spawned
        while (!AreAllPlayersReady())
        {
            yield return new WaitForSeconds(0.5f);
        }

        // All players are ready, start the game on all clients
        StartGameClientRpc();
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
    private void StartGameClientRpc()
    {
        // TODO: review all the flows with this check, I was confused that IsHost is also IsClient
        if (!IsClient) return;

        Debug.Log("NetworkGameManager: StartGameClientRpc");

        // Get the local player
        Player localPlayer = GetLocalPlayer();

        if (localPlayer == null)
        {
            Debug.LogError("Local player not found during game start!");
            return;
        }

        // Initialize client-side managers
        SetupClientManagers(localPlayer);

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

    private void SetupClientManagers(Player localPlayer)
    {
        // Initialize GameManager with the local player
        if (gameManager != null)
        {
            // Subscribe to network-relevant state changes
            gameManager.StateMachine.OnNetworkRelevantGameStateChanged += OnNetworkRelevantGameStateChanged;

            // Set the current player in GameManager
            gameManager.SetupLocalPlayer(localPlayer);
            gameManager.StartGame();
        }
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
            // Broadcast game over to all clients
            GameOverClientRpc();
        }

        // Show game over UI (both server and client)
        ShowGameOverUI();
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

        // This state is typically set by NetworkGameManager itself, not GameManager
        // But we can handle UI updates here
        ShowWaitingForPlayersUI();
    }

    [ClientRpc]
    private void GameOverClientRpc()
    {
        if (!IsClient) return;

        Debug.Log("[NetworkGameManager] Received GameOver from server");

        // Force all clients to enter GameOver state if they haven't already
        if (gameManager != null && gameManager.CurrentState != GameStateType.GameOver)
        {
            // TODO: call gameManager.GameOver() instead?
            gameManager.StateMachine.ChangeState(GameStateType.GameOver);
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
            // TODO: call gameManager.Pause() instead?
            gameManager.StateMachine.ChangeState(GameStateType.Paused);
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

    private void ShowWaitingForPlayersUI()
    {
        // TODO: Show waiting for players UI
        Debug.Log("[NetworkGameManager] Showing WaitingForPlayers UI");

        // Example: menuManager.ShowWaitingForPlayersPanel();
    }

    /// <summary>
    /// Gets the winner player name from the GameManager
    /// </summary>
    /// <returns>Winner player name or empty string if no winner/draw</returns>
    public string GetWinnerPlayerName()
    {
        Player winnerPlayer = gameManager.WinnerPlayer;
        if (winnerPlayer != null)
        {
            return winnerPlayer.playerName;
        }

        return ""; // No winner (draw)
    }

    // Call this when a player disconnects or the game needs to be reset
    // public void ResetGameState()
    // {
    //     if (!IsServer) return;

    //     ResetGameStateClientRpc();
    // }

    // [ClientRpc]
    // private void ResetGameStateClientRpc()
    // {
    //     if (gameManager != null)
    //     {
    //         gameManager.ResetToFirstPlayer();
    //     }
    // }
}