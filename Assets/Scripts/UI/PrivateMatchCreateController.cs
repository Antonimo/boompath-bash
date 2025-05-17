using UnityEngine;
using TMPro;
using System.Threading.Tasks;

public class PrivateMatchCreateController : MonoBehaviour
{
    private PrivateMatchManager privateMatchManager;

    public TextMeshProUGUI statusText;
    public MenuManager menuManager;
    public MenuPanel privateMatchLobbyPanel;

    // TODO: clear menu nav stack when opened

    private void ValidateDependencies()
    {
        privateMatchManager = FindFirstObjectByType<PrivateMatchManager>();
        if (privateMatchManager == null)
        {
            Debug.LogError("PrivateMatchCreateController: PrivateMatchManager not found in the scene!");
            if (statusText != null) statusText.text = "Error: Manager not found!";
            enabled = false; // Disable component if manager is missing
            return;
        }

        if (menuManager == null || privateMatchLobbyPanel == null)
        {
            Debug.LogError("PrivateMatchCreateController: MenuManager or PrivateMatchLobbyPanel not found in the scene!");
            enabled = false; // Disable component if manager is missing
        }

        if (statusText == null)
        {
            Debug.LogError("PrivateMatchCreateController: statusText is not assigned. Cannot display 'Creating...' message.");
            enabled = false; // Disable component if manager is missing
        }
    }

    async void OnEnable()
    {
        ValidateDependencies();
        if (!this.enabled) return;

        Debug.Log("PrivateMatchCreateController: OnEnable - START");

        statusText.text = "Creating your private match...";

        try
        {
            string lobbyCode = await privateMatchManager.CreateLobbyAsync("My Private Match", true);

            if (!string.IsNullOrEmpty(lobbyCode))
            {
                Debug.Log($"PrivateMatchCreateController: Lobby creation successful. Lobby Code: {lobbyCode}. Navigating to shared lobby view (placeholder).");
                statusText.text = $"Lobby created! Code: {lobbyCode}. Joining...";
                menuManager.OpenMenuPanel(privateMatchLobbyPanel);
            }
            else
            {
                Debug.LogError("PrivateMatchCreateController: Lobby creation failed (returned null or empty code).");
                // TODO: meaningful error message, what exactly went wrong?
                statusText.text = "Failed to create lobby..";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"PrivateMatchCreateController: Error during lobby creation: {e.Message}\n{e.StackTrace}");
            statusText.text = "Error creating lobby. See console.";
        }
        Debug.Log("PrivateMatchCreateController: OnEnable - END");
    }

    void OnDisable()
    {
        Debug.Log("PrivateMatchCreateController: OnDisable");
    }
}