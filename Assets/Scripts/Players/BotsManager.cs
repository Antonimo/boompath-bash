using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages bot-related functionalities, including loading available paths for bots.
/// </summary>
public class BotsManager : MonoBehaviour
{
    // Name of the path data file in the Resources folder
    private const string SavedPathsResourceName = "SavedPathsData";

    // Public property to provide access to the loaded paths
    public List<List<Vector3>> AvailablePaths { get; private set; } = new List<List<Vector3>>();

    void Awake()
    {
        Debug.Log("[BotsManager] Initializing...");
        LoadBotPaths();
    }

    private void LoadBotPaths()
    {
        // Use the static PathIO utility to load paths
        AvailablePaths = PathIO.LoadPathsFromResources(SavedPathsResourceName);

        // Check if the list is null (PathIO should return empty, but good practice)
        if (AvailablePaths == null)
        {
            Debug.LogError("[BotsManager] PathIO returned null when loading paths. Initializing to empty list.");
            AvailablePaths = new List<List<Vector3>>();
        }
        else
        {
            Debug.Log($"[BotsManager] Loaded {AvailablePaths.Count} paths for bots.");
        }
    }

    public List<Vector3> GetRandomPath()
    {
        if (AvailablePaths == null || AvailablePaths.Count == 0)
        {
            Debug.LogWarning("[BotsManager] Request for random path failed: No paths loaded.");
            return null; // Or return an empty list: new List<Vector3>();
        }
        int randomIndex = Random.Range(0, AvailablePaths.Count);
        return AvailablePaths[randomIndex];
    }
}