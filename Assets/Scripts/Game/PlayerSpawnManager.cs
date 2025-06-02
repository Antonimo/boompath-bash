using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

// This component manages player spawning, color/team assignment, and initial positioning.
// It is designed to run ONLY on the server/host.
// It should be DISABLED by default and enabled externally only when the host starts.
// Clients do not need this component enabled.
public class PlayerSpawnManager : MonoBehaviour
{
    [SerializeField] private Transform playersLocationsParent; // Assign the "PlayersLocations" GameObject here
    [SerializeField] private Transform playersParent; // Assign the "Players" GameObject here
    [SerializeField] private GameObject playerPrefab; // Assign your Player Prefab (with NetworkObject) here

    private List<Transform> spawnLocations = new List<Transform>();
    private List<Color> playerColors = new List<Color>();

    private int nextLocationIndex = 0;
    private int nextColorIndex = 0;
    private int nextTeamId = 1;

    // Storage for player assignments (preserves data for respawning)
    private Dictionary<ulong/* clientId */, PlayerAssignmentData> playerAssignments = new Dictionary<ulong, PlayerAssignmentData>();

    private struct PlayerAssignmentData
    {
        public Color Color;
        public int TeamId;
        public Vector3 SpawnPosition;
        public Quaternion SpawnRotation;
    }

    private void ValidateDependencies()
    {
        if (playersLocationsParent == null)
        {
            Debug.LogError("PlayersLocations parent is not assigned in PlayerSpawnManager.");
            this.enabled = false;
        }
        else
        {
            spawnLocations = playersLocationsParent.Cast<Transform>().OrderBy(t => t.name).ToList();
            if (spawnLocations.Count == 0)
            {
                Debug.LogError("No spawn locations found under PlayersLocations parent.");
                this.enabled = false;
            }
        }
        if (playersParent == null)
        {
            Debug.LogError("Players parent is not assigned in PlayerSpawnManager.");
            this.enabled = false;
        }
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned in PlayerSpawnManager.");
            this.enabled = false;
        }
    }

    void Awake()
    {
        ValidateDependencies();
    }

    void OnEnable()
    {
        ValidateDependencies();
        if (!this.enabled)
        {
            return;
        }

        // Since HostStartupManager enables this *after* host start, we can initialize directly.
        Debug.Log("PlayerSpawnManager: Enabled by HostStartupManager. Initializing server-side logic.");

        // Ensure NetworkManager is available (should always be true here, but good practice)
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("PlayerSpawnManager: NetworkManager.Singleton is null in OnEnable! This shouldn't happen when activated by HostStartupManager.");
            return; // Cannot proceed without NetworkManager
        }

        // We don't check IsServer here since host hasn't started yet.
        // HostStartupManager enables this component before starting the host,
        // so we trust that it will be used correctly as a server component.

        InitializeLocations();
        InitializeColors();

        // Subscribe to NetworkManager connection events (server-only)
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        Debug.Log("PlayerSpawnManager: Subscribed to NetworkManager connection events.");
    }

    void OnDisable()
    {
        // Unsubscribe from OnServerStarted event if NetworkManager is still available
        // REMOVED: Unsubscription for OnServerStarted no longer needed here.

        // Clean up connection callbacks
        CleanupConnectionCallbacks();
    }

    // Helper method to unsubscribe from connection-related events
    private void CleanupConnectionCallbacks()
    {
        // Clean up callbacks ONLY if we subscribed and NetworkManager is available
        // REMOVED: Check for subscribedToConnectionEvents flag removed.
        if (NetworkManager.Singleton != null) // Still check if NM exists before unsubscribing
        {
            NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            Debug.Log("PlayerSpawnManager: Unsubscribed from NetworkManager connection events.");
        }
        else
        {
            Debug.LogWarning("PlayerSpawnManager: NetworkManager.Singleton was null during CleanupConnectionCallbacks. Could not unsubscribe from events.");
        }
    }

    private void InitializeLocations()
    {
        spawnLocations = playersLocationsParent.Cast<Transform>().OrderBy(t => t.name).ToList();
        if (spawnLocations.Count == 0)
        {
            Debug.LogError("No spawn locations found under PlayersLocations parent.");
            this.enabled = false;
        }
        else
        {
            Debug.Log($"PlayerSpawnManager: Found {spawnLocations.Count} spawn locations.");
        }
    }

    // TODO: use scriptable object for this
    private void InitializeColors()
    {
        // Define the player colors based on your list
        playerColors.Add(HexToColor("8B1E3F")); // Crimson Dusk
        playerColors.Add(HexToColor("2B6A6E")); // Steel Teal
        playerColors.Add(HexToColor("D98736")); // Amber Glow
        playerColors.Add(HexToColor("1F2A5E")); // Midnight Sapphire
        playerColors.Add(HexToColor("4A7043")); // Jade Mirage
        playerColors.Add(HexToColor("5C3A7D")); // Violet Nebula
        playerColors.Add(HexToColor("4B5357")); // Slate Phantom
        playerColors.Add(HexToColor("E86A5B")); // Coral Flame
        playerColors.Add(HexToColor("A67B2F")); // Ochre Vanguard
        playerColors.Add(HexToColor("8A7A9B")); // Frost Lilac
        playerColors.Add(HexToColor("E8B923")); // Saffron Blaze
        playerColors.Add(HexToColor("D4608A")); // Rose Nova
    }

    // Called on the server when a client requests connection
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // Removed IsServer check - this callback is subscribed only after server start
        // and this component assumes it's only enabled on the server.

        ulong clientId = request.ClientNetworkId;
        byte[] connectionData = request.Payload;

        Debug.Log($"Connection request from client {clientId}");

        // --- Approval Logic ---
        bool approveConnection = false;
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;
        Color assignedColor = Color.white;
        int assignedTeamId = 0;

        if (spawnLocations.Count > 0 && nextLocationIndex < spawnLocations.Count)
        {
            // Check if we have enough colors (optional, can reuse colors)
            if (playerColors.Count > 0)
            {
                approveConnection = true;

                // Get spawn location
                Transform spawnLocation = spawnLocations[nextLocationIndex];
                spawnPosition = spawnLocation.position;
                spawnRotation = spawnLocation.rotation;

                // Get color (cycle through colors if needed)
                assignedColor = playerColors[nextColorIndex % playerColors.Count];

                // Get Team ID
                assignedTeamId = nextTeamId;

                // Store assignment data for spawning and respawning
                playerAssignments[clientId] = new PlayerAssignmentData
                {
                    Color = assignedColor,
                    TeamId = assignedTeamId,
                    SpawnPosition = spawnPosition,
                    SpawnRotation = spawnRotation
                };

                // Increment for the next player
                nextLocationIndex++;
                nextColorIndex++;
                nextTeamId++;

                Debug.Log($"Approving connection for client {clientId}. Assigning Location: {spawnLocation.name}, Color: {ColorUtility.ToHtmlStringRGB(assignedColor)}, TeamId: {assignedTeamId}.");
            }
            else
            {
                Debug.LogWarning("No player colors defined in PlayerSpawnManager.");
                response.Reason = "Server configuration error: No player colors available.";
            }

        }
        else
        {
            Debug.LogWarning($"Connection denied for client {clientId}. No available spawn locations.");
            response.Reason = "Server configuration error: No available spawn locations.";
        }

        // --- Respond to NetworkManager ---
        response.Approved = approveConnection;
        response.CreatePlayerObject = false; // IMPORTANT: We will manually spawn the player object
        response.PlayerPrefabHash = null; // Not needed if CreatePlayerObject is false
        // Position and Rotation are also not set here, as we handle it during manual spawn
        // response.Position = spawnPosition;
        // response.Rotation = spawnRotation;
        // response.Pending = false; // Respond immediately
    }

    // Called ONLY on the server when a client's connection is approved and they are formally connected.
    // This is where we manually spawn the player object for the client.
    private void HandleClientConnected(ulong clientId)
    {
        // Removed IsServer check - this callback is subscribed only after server start
        // and this component assumes it's only enabled on the server.

        Debug.Log($"HandleClientConnected: Processing manual spawn for client {clientId} on the server.");

        // Check if we have assignment data for this client
        if (playerAssignments.TryGetValue(clientId, out PlayerAssignmentData assignment))
        {
            SpawnPlayerObject(clientId, assignment);
        }
        else
        {
            // This might happen for the host client itself, or if ApprovalCheck logic changes.
            // Host client connects slightly differently.
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log($"HandleClientConnected called for host client ({clientId}). No assignment data needed as host setup might differ.");
            }
            else
            {
                Debug.LogWarning($"HandleClientConnected called for client {clientId}, but no assignment data found. This could indicate an issue in the ApprovalCheck logic or connection sequence.");
            }
        }
    }

    // Called when a client disconnects to clean up their persistent assignment data
    private void HandleClientDisconnected(ulong clientId)
    {
        // Removed IsServer check - this callback is subscribed only after server start
        // and this component assumes it's only enabled on the server.

        if (playerAssignments.ContainsKey(clientId))
        {
            Debug.Log($"Client {clientId} disconnected. Cleaning up assignment data.");
            playerAssignments.Remove(clientId);
        }
    }

    // Shared method for spawning player objects (used by initial spawn and respawn)
    private void SpawnPlayerObject(ulong clientId, PlayerAssignmentData assignment)
    {
        // Instantiate the player object at the assigned position and rotation
        GameObject playerInstance = Instantiate(playerPrefab, assignment.SpawnPosition, assignment.SpawnRotation);

        // Get the NetworkObject component
        NetworkObject playerNetworkObject = playerInstance.GetComponent<NetworkObject>();
        if (playerNetworkObject == null)
        {
            Debug.LogError($"PlayerSpawnManager: Player Prefab '{playerPrefab.name}' does not have a NetworkObject component! Cannot spawn player for client {clientId}.");
            Destroy(playerInstance); // Clean up the wrongly configured instantiated object
            return;
        }

        // Spawn the object over the network, assigning ownership to the connected client
        // This must happen BEFORE trying to access/modify NetworkVariables or call RPCs on the object
        playerNetworkObject.SpawnAsPlayerObject(clientId);
        Debug.Log($"PlayerSpawnManager: Spawned player object for client {clientId}.");

        // Now that it's spawned, find the Player script and set it up
        Player playerScript = playerInstance.GetComponent<Player>();
        if (playerScript != null)
        {
            // Call the Setup method on the Player script
            playerScript.Setup(assignment.Color, assignment.TeamId);
            Debug.Log($"PlayerSpawnManager: Called Setup on Player script for client {clientId}.");

            // Set the parent transform (do this *after* spawning if parenting affects network state, otherwise before is fine)
            if (playersParent != null)
            {
                playerInstance.transform.SetParent(playersParent, worldPositionStays: true); // Use true if spawn position is world space
                Debug.Log($"Set parent for player {clientId} to {playersParent.name}");
            }
            else
            {
                Debug.LogWarning("PlayerSpawnManager: Players Parent transform is not assigned! Player object will remain at root.");
            }
        }
        else
        {
            Debug.LogError($"PlayerSpawnManager: Player script not found on instantiated prefab for client {clientId}");
        }
    }

    /// <summary>
    /// Despawns a specific player's game object, keeping their assignment data for potential respawn.
    /// </summary>
    /// <param name="clientId">The client ID of the player to despawn</param>
    /// <returns>True if despawn was successful, false otherwise</returns>
    public bool DespawnPlayer(ulong clientId)
    {
        // Verify this is running on server
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("DespawnPlayer can only be called on the server.");
            return false;
        }

        // Check if we have assignment data for this client
        if (!playerAssignments.ContainsKey(clientId))
        {
            Debug.LogWarning($"Cannot despawn player {clientId}: No assignment data found. Player may not have been spawned through this manager.");
            return false;
        }

        // Find and destroy the existing player object
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            if (client.PlayerObject != null)
            {
                Debug.Log($"Despawning player object for client {clientId}.");
                client.PlayerObject.Despawn(destroy: true);
                return true;
            }
            else
            {
                Debug.LogWarning($"Client {clientId} is connected but has no PlayerObject to despawn.");
                return false;
            }
        }
        else
        {
            Debug.LogWarning($"Cannot despawn player {clientId}: Client is not connected.");
            return false;
        }
    }

    /// <summary>
    /// Despawns all currently connected players' game objects, keeping their assignment data for potential respawn.
    /// </summary>
    /// <returns>The number of players successfully despawned</returns>
    public int DespawnAllPlayers()
    {
        // Verify this is running on server
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("DespawnAllPlayers can only be called on the server.");
            return 0;
        }

        int despawnedCount = 0;

        // Get all Player components under the players parent
        Player[] players = playersParent.GetComponentsInChildren<Player>();

        Debug.Log($"Attempting to despawn {players.Length} player objects found under {playersParent.name}.");

        // Despawn each player object
        foreach (Player player in players)
        {
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                try
                {
                    networkObject.Despawn(destroy: true);
                    despawnedCount++;
                    Debug.Log($"Successfully despawned player object: {player.name}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to despawn player object {player.name}: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"Player {player.name} does not have a valid NetworkObject or is not spawned.");
            }
        }

        Debug.Log($"Successfully despawned {despawnedCount} out of {players.Length} player objects.");
        return despawnedCount;
    }

    /// <summary>
    /// Respawns a specific player, removing their current game object and spawning a new one
    /// with the same assigned properties (color, team, spawn location).
    /// </summary>
    /// <param name="clientId">The client ID of the player to respawn</param>
    /// <returns>True if respawn was successful, false otherwise</returns>
    public bool RespawnPlayer(ulong clientId)
    {
        // Verify this is running on server
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("RespawnPlayer can only be called on the server.");
            return false;
        }

        // Check if we have assignment data for this client
        if (!playerAssignments.TryGetValue(clientId, out PlayerAssignmentData assignment))
        {
            Debug.LogWarning($"Cannot respawn player {clientId}: No assignment data found. Player may not have been spawned through this manager.");
            return false;
        }

        // Despawn existing player object
        if (!DespawnPlayer(clientId))
        {
            Debug.LogWarning($"Failed to despawn existing player object for client {clientId}. Proceeding with spawn anyway.");
        }

        // Spawn new player object with preserved assignment data
        SpawnPlayerObject(clientId, assignment);

        Debug.Log($"Successfully respawned player {clientId} with preserved assignment data.");
        return true;
    }

    /// <summary>
    /// Respawns all currently connected players, removing their current game objects
    /// and spawning new ones with their preserved assigned properties.
    /// </summary>
    /// <returns>The number of players successfully respawned</returns>
    public int RespawnAllPlayers()
    {
        // Verify this is running on server
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("RespawnAllPlayers can only be called on the server.");
            return 0;
        }

        int respawnedCount = 0;
        List<ulong> clientsToRespawn = new List<ulong>();

        // Collect all connected clients that have assignment data
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (playerAssignments.ContainsKey(clientId))
            {
                clientsToRespawn.Add(clientId);
            }
        }

        Debug.Log($"Attempting to respawn {clientsToRespawn.Count} players.");

        // Despawn all players first
        DespawnAllPlayers();

        // Then respawn each player
        foreach (ulong clientId in clientsToRespawn)
        {
            if (playerAssignments.TryGetValue(clientId, out PlayerAssignmentData assignment))
            {
                SpawnPlayerObject(clientId, assignment);
                respawnedCount++;
            }
        }

        Debug.Log($"Successfully respawned {respawnedCount} out of {clientsToRespawn.Count} players.");
        return respawnedCount;
    }

    // Helper to convert hex string to Color
    private Color HexToColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color color))
        {
            return color;
        }
        Debug.LogWarning($"Failed to parse hex color: {hex}");
        return Color.white;
    }

    // Public property to access the Players parent transform
    public Transform PlayersParentTransform => playersParent;
}