using UnityEngine;
using TMPro;
using System.Threading.Tasks;

public class PrivateMatchCreateController : MonoBehaviour
{
    private PrivateMatchManager privateMatchManager;

    // UI Elements - Assign these in the Inspector
    public TextMeshProUGUI statusText;
    // TODO: Add a reference to a UIManager/NavigationManager to switch views
    public MenuManager menuManager;
    public MenuPanel privateMatchLobbyPanel;

    // TODO: remove self from the menu stack when closed
    // TODO: also the select game mode and team size

    void Awake()
    {
        privateMatchManager = FindFirstObjectByType<PrivateMatchManager>();
        if (privateMatchManager == null)
        {
            Debug.LogError("PrivateMatchCreateController: PrivateMatchManager not found in the scene!");
            if (statusText != null) statusText.text = "Error: Manager not found!";
            enabled = false; // Disable component if manager is missing
            return;
        }
        Debug.Log("PrivateMatchCreateController: Awake - PrivateMatchManager found.");
    }

    async void OnEnable()
    {
        if (privateMatchManager == null || !enabled) return;

        Debug.Log("PrivateMatchCreateController: OnEnable - START");

        if (statusText != null) statusText.text = "Creating your private match...";
        else Debug.LogWarning("PrivateMatchCreateController: statusText is not assigned. Cannot display 'Creating...' message.");

        try
        {
            string lobbyCode = await privateMatchManager.CreateLobbyAsync("MyPrivateMatch", true); // Using default name and private lobby

            if (!string.IsNullOrEmpty(lobbyCode))
            {
                Debug.Log($"PrivateMatchCreateController: Lobby creation successful. Lobby Code: {lobbyCode}. Navigating to shared lobby view (placeholder).");
                if (statusText != null) statusText.text = $"Lobby created! Code: {lobbyCode}. Joining...";
                menuManager.OpenMenuPanel(privateMatchLobbyPanel);
            }
            else
            {
                Debug.LogError("PrivateMatchCreateController: Lobby creation failed (returned null or empty code).");
                if (statusText != null) statusText.text = "Failed to create lobby. Check console.";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"PrivateMatchCreateController: Error during lobby creation: {e.Message}\n{e.StackTrace}");
            if (statusText != null) statusText.text = "Error creating lobby. See console.";
        }
        Debug.Log("PrivateMatchCreateController: OnEnable - END");
    }

    void OnDisable()
    {
        // Potential cleanup if needed, e.g., if we had cancellable tasks
        Debug.Log("PrivateMatchCreateController: OnDisable");
    }
}