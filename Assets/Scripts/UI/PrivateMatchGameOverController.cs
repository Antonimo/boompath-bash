using UnityEngine;
using TMPro;
using System.Collections; // For IEnumerator

// TODO: DRY with PrivateMatchLobbyController - maybe shared sub component?
public class PrivateMatchGameOverController : MonoBehaviour
{
    private GameManager gameManager;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private GameObject countdownPanel;

    [Header("Menu References")]
    [SerializeField] private MenuManager menuManager;

    private void ValidateDependencies()
    {
        if (LobbyManager.Instance == null)
        {
            Debug.LogError("PrivateMatchGameOverController: LobbyService instance not found!");
            enabled = false;
            return;
        }

        gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("PrivateMatchGameOverController: GameManager not found in the scene!");
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
        LobbyManager.OnCountdownTick += HandleCountdownTick;
        LobbyManager.OnCountdownComplete += HandleCountdownComplete;

        // Hide countdown UI initially
        if (countdownPanel != null)
            countdownPanel.SetActive(false);

        statusText.text = "Game Over";
        winnerText.text = "Determining winner...";

        // Request the winner information from GameManager
        // TODO: what is the better data flow? maybe GameManager should set the data onto this component? 
        string winnerName = gameManager.GetWinnerPlayerName();
        DisplayWinner(winnerName);

        Debug.Log("PrivateMatchGameOverController: OnEnable - END");
    }

    void OnDisable()
    {
        // Unsubscribe from countdown events
        LobbyManager.OnCountdownTick -= HandleCountdownTick;
        LobbyManager.OnCountdownComplete -= HandleCountdownComplete;

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
        // Leave the lobby and return to main menu using LobbyService
        _ = LobbyManager.Instance.LeaveLobbyAsync();
        // The menu navigation will be handled by the LobbyService cleanup
    }

    public void RequestRematch()
    {
        Debug.Log("PrivateMatchGameOverController: Rematch requested");
        // TODO: Implement rematch functionality
        // This could involve resetting the game state and starting a new countdown
        statusText.text = "Rematch requested...";
    }
}