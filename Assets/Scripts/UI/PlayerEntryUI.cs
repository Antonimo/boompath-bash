using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerEntryUI : MonoBehaviour
{
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerStatusText; // e.g. "Ready", "Not Ready", "Host"
    public Image highlightImage; // Optional: To highlight local player or host

    public void Setup(string playerName, string status, bool isVacant, bool isHighlighted)
    {
        gameObject.SetActive(true);
        if (isVacant)
        {
            playerNameText.text = "Waiting for Player...";
            playerStatusText.text = string.Empty;
        }
        else
        {
            playerNameText.text = playerName;
            playerStatusText.text = status;
        }

        if (highlightImage != null)
        {
            highlightImage.enabled = isHighlighted && !isVacant;
        }
    }

    public void SetVacant()
    {
        gameObject.SetActive(true);
        playerNameText.text = "Waiting for Player...";
        playerStatusText.text = string.Empty;
        if (highlightImage != null)
        {
            highlightImage.enabled = false;
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}