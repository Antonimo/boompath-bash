using UnityEngine;
using TMPro;
using System.Collections; // For IEnumerator

public class PrivateMatchLobbyController : MonoBehaviour
{
    private PrivateMatchManager privateMatchManager;

    // UI Elements - Assign these in the Inspector
    public TextMeshProUGUI lobbyCodeText;
    public TextMeshProUGUI statusText; // General lobby status/messages
    public TextMeshProUGUI copiedMessageText; // UI element for "Copied!" message
    public float copiedMessageDuration = 1.5f; // How long to display the "Copied!" message

    public LobbyPlayerListController playerListController; // Assign in Inspector

    // TODO: Add UI elements for player list display
    // public TextMeshProUGUI player1NameText;
    // public TextMeshProUGUI player1StatusText;
    // public TextMeshProUGUI player2NameText;
    // public TextMeshProUGUI player2StatusText;

    // TODO: Add UI Button for Ready functionality
    // public Button readyButton;

    private int updateCount = 0; // For testing Update() calls

    void Awake()
    {
        privateMatchManager = FindFirstObjectByType<PrivateMatchManager>();
        if (privateMatchManager == null)
        {
            Debug.LogError("PrivateMatchLobbyController: PrivateMatchManager not found in the scene!");
            if (statusText != null) statusText.text = "Error: Manager not found!";
            enabled = false;
            return;
        }
        Debug.Log("PrivateMatchLobbyController Awake: PrivateMatchManager found.");

        if (copiedMessageText != null)
        {
            copiedMessageText.gameObject.SetActive(false); // Hide "Copied!" message initially
        }
        else
        {
            Debug.LogWarning("PrivateMatchLobbyController: copiedMessageText is not assigned. \"Copied!\" message will not be shown.");
        }

        if (playerListController == null)
        {
            Debug.LogWarning("PrivateMatchLobbyController: playerListController is not assigned in Awake. Player list and ready functionality will be unavailable.");
        }
    }

    void OnEnable()
    {
        if (privateMatchManager == null || !enabled) return;

        Debug.Log("PrivateMatchLobbyController: OnEnable - START");

        PrivateMatchManager.OnLobbyCodeGenerated += HandleLobbyCodeGenerated;
        // TODO: Subscribe to an event from PMM for overall status updates if needed for this controller's statusText
        // Example: PrivateMatchManager.OnOverallLobbyStatusUpdate += HandleOverallLobbyStatusUpdate;

        if (lobbyCodeText != null) lobbyCodeText.text = "----"; // Placeholder until code is received
        else Debug.LogWarning("lobbyCodeText is not assigned in OnEnable, cannot display placeholder.");

        if (statusText != null) statusText.text = "Connecting to lobby..."; // Initial status
        Debug.Log("PrivateMatchLobbyController: OnEnable - UI set. Subscribed to events.");

        // Player list and its specific statuses are handled by LobbyPlayerListController.
        // This controller focuses on lobby code and high-level status not covered by LobbyPlayerListController.

        // Trigger a refresh of lobby data from PMM.
        // PMM should then fire events that LobbyPlayerListController (and this controller if needed) can pick up.
        // TODO: the lobby lifecycle should be only for a specific lobby instance, the join code shouldnt change suddenly, so we should just get the relevant infor from the manager and not listen to updates, other than what is relevant like the player list and ready status, connection status, etc.
        RequestLobbyDataRefresh();

        Debug.Log("PrivateMatchLobbyController: OnEnable - END");
    }

    void OnDisable()
    {
        if (privateMatchManager == null) return; // Guard against potential null ref if disabled early

        PrivateMatchManager.OnLobbyCodeGenerated -= HandleLobbyCodeGenerated;
        // TODO: Unsubscribe from PMM events here
        // Example: PrivateMatchManager.OnOverallLobbyStatusUpdate -= HandleOverallLobbyStatusUpdate;
        Debug.Log("PrivateMatchLobbyController: OnDisable - Unsubscribed from events.");
        StopAllCoroutines(); // Stop any running coroutines, like ShowCopiedMessage
    }

    private void HandleLobbyCodeGenerated(string lobbyCode)
    {
        Debug.Log($"PrivateMatchLobbyController: HandleLobbyCodeGenerated EVENT received Lobby Code: {lobbyCode}");
        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = lobbyCode;
        }
        else
        {
            Debug.LogWarning("lobbyCodeText is not assigned, cannot display lobby code from event.");
        }
        if (statusText != null)
        {
            // This status is for the lobby code itself. Player-specific status is handled by LobbyPlayerListController.
            statusText.text = "Lobby Active. Share code or wait for host to start.";
        }
    }

    // Renamed to reflect its purpose: trigger PMM to send updates.
    private void RequestLobbyDataRefresh()
    {
        if (privateMatchManager != null)
        {
            Debug.Log("PrivateMatchLobbyController: Requesting lobby data refresh from PrivateMatchManager.");
            privateMatchManager.RequestLobbyStateRefresh();
        }
        else
        {
            Debug.LogWarning("PrivateMatchLobbyController: PrivateMatchManager is null, cannot refresh lobby display.");
        }
    }

    // TODO: Example of handling more global status updates from PMM for this controller's statusText
    // private void HandleOverallLobbyStatusUpdate(string message)
    // {
    //     if (statusText != null) statusText.text = message;
    //     Debug.Log($"PrivateMatchLobbyController: Overall status updated to '{message}'");
    // }

    public void CopyLobbyCodeToClipboard()
    {
        string codeToCopy = null;
        if (lobbyCodeText != null)
        {
            codeToCopy = lobbyCodeText.text;
        }

        if (string.IsNullOrWhiteSpace(codeToCopy) || codeToCopy == "----" || codeToCopy == "ERROR")
        {
            Debug.LogWarning($"No valid lobby code to copy. Current displayed text: '{codeToCopy ?? "(lobbyCodeText is null)"}'");
            if (copiedMessageText != null)
            {
                StartCoroutine(ShowTemporaryMessage("No code to copy!", 2f));
            }
            return;
        }

        GUIUtility.systemCopyBuffer = codeToCopy;
        Debug.Log($"Copied to clipboard: {codeToCopy}");

        if (copiedMessageText != null)
        {
            StartCoroutine(ShowTemporaryMessage("Copied!", copiedMessageDuration));
        }
    }

    private IEnumerator ShowTemporaryMessage(string message, float duration)
    {
        if (copiedMessageText == null) yield break;

        copiedMessageText.text = message;
        copiedMessageText.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        copiedMessageText.gameObject.SetActive(false);
    }

    // TODO: Implement HandlePlayerListChanged method
    // private void HandlePlayerListChanged(List<Player> players) { ... update player UI ... }

    // TODO: Implement HandlePlayerReadyStatusChanged method
    // private void HandlePlayerReadyStatusChanged(string playerId, bool isReady) { ... update specific player status UI ... }

    // TODO: Implement method for "I'm Ready" button click
    // public void OnReadyButtonClicked() { ... call PrivateMatchManager to toggle ready status ... }

}