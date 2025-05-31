using UnityEngine;
using TMPro;
using System.Collections; // For IEnumerator

// TODO: DRY with PrivateMatchLobbyController - maybe shared sub component?
public class PrivateMatchGameOverController : MonoBehaviour
{
    private PrivateMatchManager privateMatchManager;
    private NetworkGameManager networkGameManager;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private GameObject countdownPanel;

    [Header("Menu References")]
    [SerializeField] private MenuManager menuManager;

    private void ValidateDependencies()
    {
        privateMatchManager = FindFirstObjectByType<PrivateMatchManager>();
        if (privateMatchManager == null)
        {
            Debug.LogError("PrivateMatchGameOverController: PrivateMatchManager not found in the scene!");
            enabled = false;
            return;
        }

        networkGameManager = FindFirstObjectByType<NetworkGameManager>();
        if (networkGameManager == null)
        {
            Debug.LogError("PrivateMatchGameOverController: NetworkGameManager not found in the scene!");
            enabled = false;
            return;
        }

        if (statusText == null || winnerText == null)
        {
            Debug.LogError("PrivateMatchGameOverController: One or more UI references are not assigned in the Inspector for PrivateMatchGameOverController.");
            enabled = false;
            return;
        }

        if (menuManager == null)
        {
            Debug.LogError("PrivateMatchGameOverController: MenuManager not found in the scene!");
            enabled = false;
        }
    }

    void Awake()
    {
        ValidateDependencies();
    }

    void OnEnable()
    {
        Debug.Log("PrivateMatchGameOverController: OnEnable - START");

        ValidateDependencies();
        if (!this.enabled) return;

        // Subscribe to countdown events (if needed for rematch functionality)
        PrivateMatchManager.OnCountdownTick += HandleCountdownTick;
        PrivateMatchManager.OnCountdownComplete += HandleCountdownComplete;

        // Hide countdown UI initially
        if (countdownPanel != null)
            countdownPanel.SetActive(false);

        statusText.text = "Game Over";
        winnerText.text = "Determining winner...";

        // Request the winner information from NetworkGameManager
        // TODO: what is the better data flow? maybe NetworkGameManager should set the data onto this component? 
        string winnerName = networkGameManager.GetWinnerPlayerName();
        DisplayWinner(winnerName);

        Debug.Log("PrivateMatchGameOverController: OnEnable - END");
    }

    void OnDisable()
    {
        if (privateMatchManager == null) return; // Guard against potential null ref if disabled early

        // Unsubscribe from countdown events
        PrivateMatchManager.OnCountdownTick -= HandleCountdownTick;
        PrivateMatchManager.OnCountdownComplete -= HandleCountdownComplete;

        Debug.Log("PrivateMatchGameOverController: OnDisable");
        StopAllCoroutines(); // Stop any running coroutines
    }

    private void HandleCountdownTick(float remainingTime)
    {
        // Make countdown visible (for rematch functionality)
        if (countdownPanel != null && !countdownPanel.activeSelf)
            countdownPanel.SetActive(true);

        int seconds = Mathf.CeilToInt(remainingTime);
        if (countdownText != null)
            countdownText.text = $"Rematch starting in {seconds}...";
    }

    private void HandleCountdownComplete()
    {
        if (countdownText != null)
            countdownText.text = "Starting rematch...";
    }

    public void DisplayWinner(string winnerPlayerName)
    {
        Debug.Log($"PrivateMatchGameOverController: Displaying winner: {winnerPlayerName}");

        if (string.IsNullOrEmpty(winnerPlayerName))
        {
            winnerText.text = "It's a draw!";
            statusText.text = "Game ended in a draw";
        }
        else
        {
            winnerText.text = $"{winnerPlayerName} Wins!";
            statusText.text = "Congratulations to the winner!";
        }
    }

    public void ReturnToMainMenu()
    {
        Debug.Log("PrivateMatchGameOverController: Returning to main menu");
        // Leave the lobby and return to main menu
        _ = privateMatchManager.LeaveLobbyAndCleanupAsync();
        // The menu navigation will be handled by the PrivateMatchManager cleanup
    }

    public void RequestRematch()
    {
        Debug.Log("PrivateMatchGameOverController: Rematch requested");
        // TODO: Implement rematch functionality
        // This could involve resetting the game state and starting a new countdown
        statusText.text = "Rematch requested...";
    }
}