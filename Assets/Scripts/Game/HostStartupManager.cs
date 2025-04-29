using UnityEngine;
using Unity.Netcode;

public class HostStartupManager : MonoBehaviour
{
    [SerializeField] private PlayerSpawnManager playerSpawnManager; // Assign your PlayerSpawnManager component here

    void Start()
    {
        // Ensure the PlayerSpawnManager reference is set
        if (playerSpawnManager == null)
        {
            Debug.LogError("HostStartupManager: PlayerSpawnManager reference is not set in the Inspector!");
            enabled = false; // Disable this script if the reference is missing
            return;
        }

        // Ensure the PlayerSpawnManager starts disabled
        // We do this here AND potentially in the Inspector for robustness
        playerSpawnManager.enabled = false;
    }

    // Call this method from your UI button instead of NetworkManager.Singleton.StartHost()
    public void StartHostWithApprovalSetup()
    {
        if (playerSpawnManager == null)
        {
            Debug.LogError("HostStartupManager: Cannot start host, PlayerSpawnManager reference is null.");
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("HostStartupManager: NetworkManager.Singleton is not found. Cannot start host.");
            return;
        }

        Debug.Log("HostStartupManager: Preparing to start host...");

        // 1. Enable PlayerSpawnManager - its OnEnable will handle callback registration.
        // TODO: no need to make sure that ConnectionApprovalCallback is registered only for the host
        // because its used only on the server.
        // The OnClientConnectedCallback though is also called on the client, so we need to make sure its enabled only for the host.
        playerSpawnManager.enabled = true;

        // 2. Start the Host
        // PlayerSpawnManager's OnEnable should have registered the approval callback by now.
        bool startResult = NetworkManager.Singleton.StartHost();

        if (startResult)
        {
            Debug.Log("HostStartupManager: NetworkManager.StartHost() called successfully.");
        }
        else
        {
            Debug.LogError("HostStartupManager: NetworkManager.StartHost() failed!");
            // Optional: Disable PlayerSpawnManager again if startup failed?
            // playerSpawnManager.enabled = false;
        }
    }
}