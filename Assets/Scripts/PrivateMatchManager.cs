using System;
using UnityEngine;

public class PrivateMatchManager : MonoBehaviour
{
    [SerializeField] private GameMode selectedGameMode;
    [SerializeField] private TeamSize selectedTeamSize;

    public void SelectGameMode(string selectedMode)
    {
        Debug.Log($"Selected Game Mode: {selectedMode}");

        try
        {
            selectedGameMode = (GameMode)Enum.Parse(typeof(GameMode), selectedMode);
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"Invalid game mode string: '{selectedMode}'. Error: {e.Message}");
        }
    }

    public void SelectTeamSize(string selectedSize)
    {
        Debug.Log($"Selected Team Size: {selectedSize}");

        try
        {
            selectedTeamSize = (TeamSize)Enum.Parse(typeof(TeamSize), selectedSize);
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"Invalid team size string: '{selectedSize}'. Error: {e.Message}");
        }
    }
}
