using UnityEngine;
using System.Collections.Generic;
using System; // For Exception

public static class PathIO
{
    // Nested classes for JSON deserialization structure
    [Serializable]
    public class PathListData
    {
        public List<PathData> paths = new List<PathData>();
    }

    [Serializable]
    public class PathData
    {
        public List<Vector3> points = new List<Vector3>();

        // Constructor might not be strictly needed for JsonUtility deserialization,
        // but can be useful for manual creation if ever required.
        public PathData() { }

        public PathData(List<Vector3> pathPoints)
        {
            points = pathPoints;
        }
    }

    /// <summary>
    /// Loads paths from a JSON TextAsset stored in the Resources folder.
    /// </summary>
    /// <param name="resourceName">The name of the TextAsset in the Resources folder (without extension).</param>
    /// <returns>A list of paths (List<List<Vector3>>). Returns an empty list if loading fails or file not found.</returns>
    public static List<List<Vector3>> LoadPathsFromResources(string resourceName)
    {
        List<List<Vector3>> loadedPaths = new List<List<Vector3>>();
        TextAsset jsonAsset = Resources.Load<TextAsset>(resourceName);

        if (jsonAsset == null)
        {
            Debug.Log($"[PathIO] No saved paths file found at 'Resources/{resourceName}.json'. Returning empty list.");
            return loadedPaths; // Return empty list
        }

        try
        {
            PathListData loadedData = JsonUtility.FromJson<PathListData>(jsonAsset.text);

            if (loadedData != null && loadedData.paths != null)
            {
                foreach (var pathData in loadedData.paths)
                {
                    // Ensure loaded data is valid before adding
                    if (pathData != null && pathData.points != null && pathData.points.Count > 0)
                    {
                        // Add a copy to avoid potential modification issues if the source is reused
                        loadedPaths.Add(new List<Vector3>(pathData.points));
                    }
                }
                Debug.Log($"[PathIO] Successfully loaded {loadedPaths.Count} paths from 'Resources/{resourceName}.json'.");
            }
            else
            {
                Debug.LogWarning($"[PathIO] Loaded path data from 'Resources/{resourceName}.json' was null or empty.");
                // Return the initialized empty list
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PathIO] Failed to load paths from JSON 'Resources/{resourceName}.json': {e.Message}");
            // Return the initialized empty list on error
        }

        return loadedPaths;
    }

    // TODO: Add a SavePathsToResources method if needed later, mirroring PathEditor's save logic.
}