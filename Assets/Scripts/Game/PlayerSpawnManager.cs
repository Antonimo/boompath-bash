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

    // Temporary storage for data assigned during connection approval
    private Dictionary<ulong/* clientId */, PlayerAssignmentData> pendingAssignments = new Dictionary<ulong, PlayerAssignmentData>();

    private struct PlayerAssignmentData
    {
        public Color Color;
        public int TeamId;
        public Vector3 SpawnPosition;
        public Quaternion SpawnRotation;
    }

    void OnEnable()
    {
        // Since HostStartupManager enables this *after* host start, we can initialize directly.
        Debug.Log("PlayerSpawnManager: Enabled by HostStartupManager. Initializing server-side logic.");

        // Ensure NetworkManager is available (should always be true here, but good practice)
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("PlayerSpawnManager: NetworkManager.Singleton is null in OnEnable! This shouldn't happen when activated by HostStartupManager.");
            return; // Cannot proceed without NetworkManager
        }

        InitializeLocations();
        InitializeColors();

        // Subscribe to NetworkManager connection events (server-only)
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        // Optional: Listen for when a client disconnects to potentially free up resources
        // NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

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
            // NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            Debug.Log("PlayerSpawnManager: Unsubscribed from NetworkManager connection events.");
        }
        else
        {
            Debug.LogWarning("PlayerSpawnManager: NetworkManager.Singleton was null during CleanupConnectionCallbacks. Could not unsubscribe from events.");
        }
    }


    private void InitializeLocations()
    {
        if (playersLocationsParent == null)
        {
            Debug.LogError("PlayersLocations parent is not assigned in PlayerSpawnManager.");
            return;
        }
        spawnLocations = playersLocationsParent.Cast<Transform>().OrderBy(t => t.name).ToList();
        if (spawnLocations.Count == 0)
        {
            Debug.LogWarning("No spawn locations found under PlayersLocations parent.");
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

                // Store assignment data TEMPORARILY until the player object spawns manually
                pendingAssignments[clientId] = new PlayerAssignmentData
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

                Debug.Log($"Approving connection for client {clientId}. Assigning Location: {spawnLocation.name}, Color: {ColorUtility.ToHtmlStringRGB(assignedColor)}, TeamId: {assignedTeamId}. Pending setup.");
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

        // Check if we have pending assignment data for this client
        if (pendingAssignments.TryGetValue(clientId, out PlayerAssignmentData assignment))
        {
            if (playerPrefab == null)
            {
                Debug.LogError($"PlayerSpawnManager: Player Prefab is not assigned! Cannot spawn player for client {clientId}.");
                pendingAssignments.Remove(clientId); // Clean up to prevent repeated errors
                // Optionally: Disconnect the client here? NetworkManager.Singleton.DisconnectClient(clientId);
                return;
            }

            // Instantiate the player object at the assigned position and rotation
            GameObject playerInstance = Instantiate(playerPrefab, assignment.SpawnPosition, assignment.SpawnRotation);

            // Get the NetworkObject component
            NetworkObject playerNetworkObject = playerInstance.GetComponent<NetworkObject>();
            if (playerNetworkObject == null)
            {
                Debug.LogError($"PlayerSpawnManager: Player Prefab '{playerPrefab.name}' does not have a NetworkObject component! Cannot spawn player for client {clientId}.");
                Destroy(playerInstance); // Clean up the wrongly configured instantiated object
                pendingAssignments.Remove(clientId);
                // Optionally: Disconnect the client here?
                return;
            }

            // Spawn the object over the network, assigning ownership to the connected client
            // This must happen BEFORE trying to access/modify NetworkVariables or call RPCs on the object
            playerNetworkObject.SpawnAsPlayerObject(clientId);
            Debug.Log($"PlayerSpawnManager: Spawned player object for client {clientId} manually.");

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

            // Clean up the pending assignment data now that spawning and setup are done
            pendingAssignments.Remove(clientId);
        }
        else
        {
            // This might happen for the host client itself, or if ApprovalCheck logic changes.
            // Host client connects slightly differently.
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log($"HandleClientConnected called for host client ({clientId}). No pending assignment data needed as host setup might differ.");
            }
            else
            {
                Debug.LogWarning($"HandleClientConnected called for client {clientId}, but no pending assignment data found. This could indicate an issue in the ApprovalCheck logic or connection sequence.");
            }
        }
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