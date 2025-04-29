using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class NetworkGameManager : NetworkBehaviour
{
    [Header("Client-side Managers")]
    [SerializeField] private GameManager gameManager;

    [Header("Network Setup")]
    [SerializeField] private GameObject playersParent;
    [SerializeField] private int requiredPlayerCount = 2;

    private void Awake()
    {
        if (gameManager == null)
        {
            Debug.LogError("NetworkGameManager is not properly initialized. Some references are not assigned.");
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Server waits for all players to be ready before starting the game
            StartCoroutine(WaitForPlayersAndStartGame());
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

        // Get the local player
        Player localPlayer = GetLocalPlayer();

        if (localPlayer == null)
        {
            Debug.LogError("Local player not found during game start!");
            return;
        }

        // Initialize client-side managers
        SetupClientManagers(localPlayer);
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