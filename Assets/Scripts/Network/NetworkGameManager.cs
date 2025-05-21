using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class NetworkGameManager : NetworkBehaviour
{
    [Header("Client-side Managers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MenuManager menuManager;

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
    }

    private void Awake()
    {
        ValidateDependencies();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Debug.Log("NetworkGameManager: Server spawned.");
            // Server waits for all players to be ready before starting the game
            // StartCoroutine(WaitForPlayersAndStartGame());
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
            // Set the current player in GameManager
            gameManager.SetupLocalPlayer(localPlayer);
            gameManager.StartGame();
        }
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