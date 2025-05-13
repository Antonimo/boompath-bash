using UnityEngine;
using UnityEngine.UI;
using TMPro; // Using TextMeshPro for UI elements
using System.Threading.Tasks; // Required for async operations
using System.Text.RegularExpressions; // For regex-based validation

public class PrivateMatchJoinController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField lobbyCodeInputField;
    [SerializeField] private Button joinMatchButton;
    [SerializeField] private TMP_Text statusText;

    private PrivateMatchManager privateMatchManager;
    private const int RequiredLobbyCodeLength = 6;
    // Regex to allow only alphanumeric characters
    private static readonly Regex AlphanumericRegex = new Regex("^[a-zA-Z0-9]*$");

    void Awake()
    {
        privateMatchManager = Object.FindFirstObjectByType<PrivateMatchManager>();

        if (privateMatchManager == null)
        {
            Debug.LogError("PrivateMatchManager not found in the scene! The PrivateMatchJoinController cannot function.");
            SetStatusText("ERROR: Critical component missing.", true);
            DisableInteractions();
            this.enabled = false;
            return;
        }

        if (joinMatchButton == null || lobbyCodeInputField == null || statusText == null)
        {
            Debug.LogError("One or more UI references are not assigned in the Inspector for PrivateMatchJoinController.");
            SetStatusText("ERROR: UI component missing.", true);
            DisableInteractions();
            this.enabled = false;
            return;
        }
    }

    void OnEnable()
    {
        if (lobbyCodeInputField != null)
        {
            lobbyCodeInputField.text = "";
            lobbyCodeInputField.interactable = true;
            lobbyCodeInputField.readOnly = false;
            lobbyCodeInputField.onValueChanged.AddListener(ValidateInput);
        }

        if (joinMatchButton != null)
        {
            joinMatchButton.interactable = false;
            joinMatchButton.onClick.AddListener(OnJoinMatchClicked);
        }

        SetStatusText("Enter a 6-character lobby code.");
        if (lobbyCodeInputField != null)
        {
            ValidateInput(lobbyCodeInputField.text);
        }
        else if (joinMatchButton != null)
        {
            joinMatchButton.interactable = false;
        }
    }

    void OnDisable()
    {
        if (lobbyCodeInputField != null)
        {
            lobbyCodeInputField.onValueChanged.RemoveListener(ValidateInput);
        }
        if (joinMatchButton != null)
        {
            joinMatchButton.onClick.RemoveListener(OnJoinMatchClicked);
        }
    }

    private void ValidateInput(string currentInput)
    {
        if (string.IsNullOrWhiteSpace(currentInput))
        {
            SetStatusText("Enter a 6-character lobby code.");
            if (joinMatchButton != null) joinMatchButton.interactable = false;
            return;
        }

        if (currentInput.Length == RequiredLobbyCodeLength && AlphanumericRegex.IsMatch(currentInput))
        {
            SetStatusText("Valid code format. Ready to join.");
            if (joinMatchButton != null) joinMatchButton.interactable = true;
        }
        else
        {
            string errorMsg = "Invalid code format.";
            if (currentInput.Length != RequiredLobbyCodeLength)
            {
                errorMsg += $" Must be {RequiredLobbyCodeLength} characters.";
            }
            else if (!AlphanumericRegex.IsMatch(currentInput))
            {
                errorMsg += " Must be alphanumeric.";
            }
            SetStatusText(errorMsg);
            if (joinMatchButton != null) joinMatchButton.interactable = false;
        }
    }

    private async void OnJoinMatchClicked()
    {
        if (privateMatchManager == null) // Should have been caught in Awake
        {
            Debug.LogError("PrivateMatchManager is missing, cannot attempt to join lobby.");
            SetStatusText("Error: Cannot connect.", true);
            return;
        }

        string lobbyCode = lobbyCodeInputField.text.Trim().ToUpper(); // Standardize lobby code format

        // Final validation before attempting, though ValidateInput should prevent this state for the button
        if (lobbyCode.Length != RequiredLobbyCodeLength || !AlphanumericRegex.IsMatch(lobbyCode))
        {
            SetStatusText("Invalid Lobby Code. Please check and try again.", true);
            joinMatchButton.interactable = true; // Re-enable button if it was somehow clicked with invalid code
            return;
        }

        SetStatusText($"Attempting to join lobby with code: {lobbyCode}...", true);
        DisableInteractions(true);

        bool success = false;
        try
        {
            success = await privateMatchManager.JoinLobbyByCodeAsync(lobbyCode);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception occurred while trying to join lobby: {e.Message}\n{e.StackTrace}");
            SetStatusText("An error occurred. Please check console.", true);
            EnableInteractionsForRetry();
            return;
        }

        if (success)
        {
            SetStatusText("Successfully joined lobby!", true);
            // Interactions remain disabled as user has successfully joined.
        }
        else
        {
            SetStatusText("Failed to join lobby. Please check the code or try again.", true);
            EnableInteractionsForRetry();
        }
    }

    private void SetStatusText(string message, bool isError = false)
    {
        if (statusText != null)
        {
            statusText.text = message;
            // Optionally change color for errors
            // statusText.color = isError ? Color.red : Color.black; 
        }
    }

    private void DisableInteractions(bool joining = false)
    {
        if (lobbyCodeInputField != null)
        {
            lobbyCodeInputField.interactable = false;
            if (joining) lobbyCodeInputField.readOnly = true;
        }
        if (joinMatchButton != null) joinMatchButton.interactable = false;
    }

    private void EnableInteractionsForRetry()
    {
        if (lobbyCodeInputField != null)
        {
            lobbyCodeInputField.interactable = true;
            lobbyCodeInputField.readOnly = false;
            ValidateInput(lobbyCodeInputField.text);
        }
        else if (joinMatchButton != null)
        {
            joinMatchButton.interactable = false;
        }
    }
}