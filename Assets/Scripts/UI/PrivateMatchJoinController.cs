using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;

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

    [Header("Menu References")]
    [SerializeField] private MenuManager menuManager;
    [SerializeField] private MenuPanel privateMatchLobbyPanel;

    private void ValidateDependencies()
    {
        privateMatchManager = Object.FindFirstObjectByType<PrivateMatchManager>();

        if (privateMatchManager == null)
        {
            Debug.LogError("PrivateMatchManager not found in the scene! The PrivateMatchJoinController cannot function.");
            this.enabled = false;
            return;
        }

        // TODO: is this enough to not need to do null checks in rest of the code?
        if (joinMatchButton == null || lobbyCodeInputField == null || statusText == null)
        {
            Debug.LogError("One or more UI references are not assigned in the Inspector for PrivateMatchJoinController.");
            this.enabled = false;
        }

        if (menuManager == null || privateMatchLobbyPanel == null)
        {
            Debug.LogError("MenuManager or PrivateMatchLobbyPanel not found in the scene! The PrivateMatchJoinController cannot function.");
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

        lobbyCodeInputField.text = "";
        lobbyCodeInputField.interactable = true;
        lobbyCodeInputField.readOnly = false;
        lobbyCodeInputField.onValueChanged.AddListener(ValidateInput);
        ValidateInput(lobbyCodeInputField.text);

        SetJoinMatchButtonInteractable(false);
        joinMatchButton.onClick.AddListener(OnJoinMatchClicked);
    }

    void OnDisable()
    {
        lobbyCodeInputField?.onValueChanged.RemoveListener(ValidateInput);
        joinMatchButton?.onClick.RemoveListener(OnJoinMatchClicked);
    }

    private void ValidateInput(string currentInput)
    {
        if (string.IsNullOrWhiteSpace(currentInput))
        {
            SetStatusText("Enter a 6-character lobby code.");
            SetJoinMatchButtonInteractable(false);
            return;
        }

        if (currentInput.Length != RequiredLobbyCodeLength || !AlphanumericRegex.IsMatch(currentInput))
        {
            string errorMsg = "Invalid code format.";
            if (currentInput.Length != RequiredLobbyCodeLength) errorMsg += $" Must be {RequiredLobbyCodeLength} characters.";
            if (!AlphanumericRegex.IsMatch(currentInput)) errorMsg += " Must be alphanumeric.";
            SetStatusText(errorMsg);
            SetJoinMatchButtonInteractable(false);
            return;
        }

        SetStatusText("Valid code format. Ready to join.");
        SetJoinMatchButtonInteractable(true);
    }

    private async void OnJoinMatchClicked()
    {
        string lobbyCode = lobbyCodeInputField.text.Trim().ToUpper(); // Standardize lobby code format

        SetStatusText($"Attempting to join lobby with code: {lobbyCode}...");
        DisableInteractions(true);

        bool success = false;
        try
        {
            success = await privateMatchManager.JoinLobbyByCodeAsync(lobbyCode);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception occurred while trying to join lobby: {e.Message}\n{e.StackTrace}");
            SetStatusText("Error");
            EnableInteractionsForRetry();
            return;
        }

        if (success)
        {
            SetStatusText("Successfully joined lobby!");
            // Interactions remain disabled as user has successfully joined.

            menuManager.OpenMenuPanel(privateMatchLobbyPanel);
        }
        else
        {
            // TODO: nice to have: meaningful error messages for why it failed?
            SetStatusText("Failed to join lobby. Please check the code or try again.");
            EnableInteractionsForRetry();
        }
    }

    private void SetStatusText(string message)
    {
        statusText.text = message;
    }

    private void DisableInteractions(bool joining = false)
    {
        lobbyCodeInputField.interactable = false;
        if (joining) lobbyCodeInputField.readOnly = true;
        SetJoinMatchButtonInteractable(false);
    }

    private void EnableInteractionsForRetry()
    {
        lobbyCodeInputField.interactable = true;
        lobbyCodeInputField.readOnly = false;
        ValidateInput(lobbyCodeInputField.text);
    }

    private void SetJoinMatchButtonInteractable(bool interactable)
    {
        joinMatchButton.interactable = interactable;
    }
}