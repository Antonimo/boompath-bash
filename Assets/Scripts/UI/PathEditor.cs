using UnityEngine;
using System.Collections.Generic;
using System; // Required for [Serializable]
#if UNITY_EDITOR
using UnityEditor; // Required for AssetDatabase and EditorApplication
#endif

public class PathEditor : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private SimplePathDrawing simplePathDrawing;
    [SerializeField] private LineRenderer pathLineRenderer; // Template for ALL path visualizations

    [Header("Show All Paths")]
    [SerializeField] private bool showAllPaths = false; // Toggle to show all saved paths
    [SerializeField] private Color allPathsColor = Color.grey; // Color for the 'show all' paths

    [Header("Path Viewer")]
    [SerializeField] private bool showPathViewer = false; // Toggle to show the single path viewer
    [SerializeField] private Color pathViewerColor = Color.cyan; // Color for the single viewed path
    [SerializeField] private int currentViewedPathIndex = -1; // Index of the path being viewed (-1 means none)

    // Path for saving/loading the JSON file within the Resources folder
    private const string SavedPathsResourceName = "SavedPathsData";

    // List to store saved paths
    [SerializeField] private List<List<Vector3>> savedPaths = new List<List<Vector3>>();
    public List<List<Vector3>> SavedPaths => savedPaths; // Public getter if needed elsewhere


    // --- Visualization Internals ---
    private List<LineRenderer> visualizedPathRenderers = new List<LineRenderer>(); // Keep track of created renderers for showAllPaths
    private LineRenderer activeViewerLine; // The active LineRenderer for the path viewer

    // Public property to control the ShowAllPaths toggle and trigger visualization updates
    public bool ShowAllPaths
    {
        get => showAllPaths;
        set
        {
            if (showAllPaths != value)
            {
                showAllPaths = value;
                UpdateShowAllPathsVisualization();
            }
        }
    }

    // Public property to control the PathViewer toggle and trigger visualization updates
    public bool ShowPathViewer
    {
        get => showPathViewer;
        set
        {
            if (showPathViewer != value)
            {
                showPathViewer = value;
                if (!showPathViewer)
                {
                    // If turning off, ensure the viewer index is reset and visualization cleared
                    CurrentViewedPathIndex = -1; // This will trigger UpdatePathViewerVisualization via its setter
                }
                else
                {
                    // If turning on, and no path is selected, default to the first path if available
                    if (currentViewedPathIndex < 0 && savedPaths.Count > 0)
                    {
                        CurrentViewedPathIndex = 0; // This triggers the update via setter
                    }
                }
            }
            UpdatePathViewerVisualization(); // Update in case state needs refreshing
        }
    }

    // Public property for the index, ensures visualization updates when changed
    public int CurrentViewedPathIndex
    {
        get => currentViewedPathIndex;
        set
        {
            int newIndex = value;
            // Clamp or wrap index if necessary, although Prev/Next handles wrapping.
            // We mainly need to ensure it's valid or -1.
            if (savedPaths.Count == 0)
            {
                newIndex = -1;
            }
            else if (newIndex < -1)
            {
                newIndex = savedPaths.Count - 1; // Wrap backwards
            }
            else if (newIndex >= savedPaths.Count)
            {
                newIndex = 0; // Wrap forwards
            }

            if (currentViewedPathIndex != newIndex)
            {
                // Allow setting to -1 to explicitly view nothing even if viewer is on
                currentViewedPathIndex = newIndex;
                UpdatePathViewerVisualization();
            }
        }
    }

    // --- Unity Lifecycle Methods ---
    void Start()
    {
        Debug.Log("[PathEditor] Starting Path Editor.");

        if (simplePathDrawing == null)
        {
            Debug.LogError("[PathEditor] SimplePathDrawing component not found on the assigned GameObject. Path Editor Mode cannot be enabled.");
            return;
        }

        if (pathLineRenderer == null)
        {
            Debug.LogError("[PathEditor] LineRenderer component not found on the assigned GameObject. Path Editor Mode cannot be enabled.");
            return;
        }

        // TODO: PathIO and load json

        // simplePathDrawing.SetPathEditorMode(true);

        // Initial visualization update if the toggles are on at start
        UpdateShowAllPathsVisualization(); // For showAllPaths
        UpdatePathViewerVisualization(); // For pathViewer
    }

    // --- Public Methods ---
    /// <summary>
    /// Saves the path currently drawn by SimplePathDrawing.
    /// </summary>
    public void SaveCurrentPath()
    {
        if (simplePathDrawing == null || simplePathDrawing.pathPoints.Count < 2)
        {
            Debug.LogWarning("[PathEditor] Cannot save path: Not enough points in the current path.");
            return;
        }

        // Add a copy of the current path
        savedPaths.Add(new List<Vector3>(simplePathDrawing.pathPoints));
        Debug.Log("[PathEditor] Current path saved. Total paths: " + savedPaths.Count);

        // Clear the drawing component's path after saving
        simplePathDrawing.ClearPath();

        CurrentViewedPathIndex = savedPaths.Count - 1;

        UpdateShowAllPathsVisualization();
    }

    void OnDisable()
    {
        if (simplePathDrawing != null)
        {
            // Check if the game is quitting to prevent errors during shutdown
            // Using gameObject.scene.isLoaded as a heuristic
            if (this.gameObject.scene.isLoaded)
            {
                Debug.Log("[PathEditor] Disabling Path Editor Mode.");
                simplePathDrawing.SetPathEditorMode(false);
            }
        }

        // Clean up visualization GameObjects when the component is disabled or destroyed
        ClearAllPathVisualizations();
        ClearPathViewerVisualization();
    }

    // --- Visualization Update Methods ---

    /// <summary>
    /// Updates the visualization of all saved paths based on the showAllPaths toggle.
    /// </summary>
    private void UpdateShowAllPathsVisualization()
    {
        ClearAllPathVisualizations();

        if (showAllPaths)
        {
            // Create a renderer for each saved path
            for (int i = 0; i < savedPaths.Count; i++)
            {
                LineRenderer newRenderer = CreatePathRendererInstance(savedPaths[i], allPathsColor, -1, $"SavedPath_{i}");
                if (newRenderer != null)
                {
                    visualizedPathRenderers.Add(newRenderer); // Keep track for cleanup
                }
            }
        }
    }

    /// <summary>
    /// Updates the visualization for the single path viewer.
    /// Creates, updates, or destroys the activeViewerLine based on state.
    /// </summary>
    private void UpdatePathViewerVisualization()
    {
        ClearPathViewerVisualization();

        if (showPathViewer && currentViewedPathIndex >= 0 && currentViewedPathIndex < savedPaths.Count)
        {
            activeViewerLine = CreatePathRendererInstance(savedPaths[currentViewedPathIndex], pathViewerColor, 0, $"PathViewerLine");
        }
    }

    // --- Visualization Cleanup Methods ---

    /// <summary>
    /// Destroys all GameObjects created for the 'Show All Paths' visualization.
    /// </summary>
    private void ClearAllPathVisualizations()
    {
        foreach (LineRenderer renderer in visualizedPathRenderers)
        {
            if (renderer != null && renderer.gameObject != null)
            {
                Destroy(renderer.gameObject);
            }
        }
        visualizedPathRenderers.Clear();
    }

    /// <summary>
    /// Destroys the GameObject created for the path viewer visualization.
    /// </summary>
    private void ClearPathViewerVisualization()
    {
        if (activeViewerLine != null && activeViewerLine.gameObject != null)
        {
            Destroy(activeViewerLine.gameObject);
            activeViewerLine = null;
        }
    }

    // --- Persistence Methods ---

    /// <summary>
    /// Saves the current list of paths to a JSON file in the project's Resources folder.
    /// Editor Only.
    /// </summary>
    // TODO: move to PathIO?
    public void SavePathsToJson()
    {
#if UNITY_EDITOR
        // Use the PathIO nested classes for structure
        PathIO.PathListData pathListData = new PathIO.PathListData();
        foreach (var path in savedPaths)
        {
            pathListData.paths.Add(new PathIO.PathData(path));
        }

        string json = JsonUtility.ToJson(pathListData, true); // Use pretty print for readability
        string resourcePath = System.IO.Path.Combine(Application.dataPath, "Resources");
        string filePath = System.IO.Path.Combine(resourcePath, SavedPathsResourceName + ".json");

        try
        {
            // Ensure the Resources directory exists
            if (!System.IO.Directory.Exists(resourcePath))
            {
                System.IO.Directory.CreateDirectory(resourcePath);
            }

            System.IO.File.WriteAllText(filePath, json);
            Debug.Log($"[PathEditor] Successfully saved {savedPaths.Count} paths to '{filePath}'.");

            // Important: Refresh the AssetDatabase so Unity recognizes the new/updated file
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PathEditor] Failed to save paths to JSON: {e.Message}");
        }
#else
        Debug.LogWarning("[PathEditor] Saving paths to JSON is only supported in the Unity Editor.");
#endif
    }

    /// <summary>
    /// Loads paths using PathIO from the JSON file stored in the Resources folder.
    /// </summary>
    private void LoadPathsFromJson()
    {
        savedPaths = PathIO.LoadPathsFromResources(SavedPathsResourceName);

        // Ensure list is never null, even if loading fails
        if (savedPaths == null)
        {
            savedPaths = new List<List<Vector3>>();
            Debug.LogError("[PathEditor] PathIO returned null. Initializing to empty list.");
        }
        else
        {
            Debug.Log($"[PathEditor] Loaded {savedPaths.Count} paths via PathIO.");
        }

        // Reset viewer index if it becomes invalid after loading
        if (currentViewedPathIndex >= savedPaths.Count)
        {
            CurrentViewedPathIndex = savedPaths.Count > 0 ? savedPaths.Count - 1 : -1;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Called in the editor when the script is loaded or a value is changed in the Inspector.
    /// Used to update the path visualization when the toggle is changed via the Inspector during runtime.
    /// </summary>
    void OnValidate()
    {
        // We only want to update the visualization during play mode
        // to avoid potential issues in edit mode before Start() runs.
        // Also, check if the component is active and enabled.
        if (Application.isPlaying && this.isActiveAndEnabled)
        {
            // Use a delayed call to avoid issues with Inspector updates during the same frame.
            // Using Invoke ensures it runs on the main thread after the current update cycle.
            // Check if simplePathDrawing is assigned to avoid errors if called before Start.
            if (simplePathDrawing != null && pathLineRenderer != null)
            {
                // Schedule the update for the next frame to ensure all inspector changes are processed.
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    // Check dependencies before calling updates inside the delayed call
                    if (pathLineRenderer != null)
                    {
                        UpdateShowAllPathsVisualization();
                        UpdatePathViewerVisualization();
                    }
                };
            }
        }
        // Note: We don't automatically clear paths here in edit mode when toggling off,
        // as it might interfere with scene saving or other editor operations.
        // The cleanup in OnDisable handles runtime disabling.
    }
#endif

    // Helper method to allow changing the toggle via Inspector events (e.g., UI Button)
    public void ToggleShowAllPaths(bool value)
    {
        ShowAllPaths = value;
    }

    // Helper method to allow changing the viewer toggle via Inspector events
    public void ToggleShowPathViewer(bool value)
    {
        ShowPathViewer = value;
    }

    // Methods for Prev/Next buttons in the Editor script
    public void ShowPreviousPath()
    {
        if (savedPaths.Count > 0)
        {
            CurrentViewedPathIndex = (currentViewedPathIndex <= 0) ? savedPaths.Count - 1 : currentViewedPathIndex - 1;
            // UpdatePathViewerVisualization is called by the property setter if needed
        }
    }

    public void ShowNextPath()
    {
        if (savedPaths.Count > 0)
        {
            CurrentViewedPathIndex = (currentViewedPathIndex >= savedPaths.Count - 1) ? 0 : currentViewedPathIndex + 1;
            // UpdatePathViewerVisualization is called by the property setter if needed
        }
    }

    /// <summary>
    /// Deletes the path currently being viewed in the Path Viewer.
    /// Adjusts the current index and updates the visualization.
    /// </summary>
    public void DeleteCurrentViewedPath()
    {
        // Ensure the viewer is active and the index is valid
        if (!showPathViewer || currentViewedPathIndex < 0 || currentViewedPathIndex >= savedPaths.Count)
        {
            Debug.LogWarning("[PathEditor] Cannot delete path: No valid path is currently being viewed.");
            return;
        }

        int indexToDelete = currentViewedPathIndex;
        Debug.Log($"[PathEditor] Deleting path at index {indexToDelete}.");

        // Remove the path
        savedPaths.RemoveAt(indexToDelete);

        // Adjust the current index
        // If list becomes empty, set index to -1
        if (savedPaths.Count == 0)
        {
            currentViewedPathIndex = -1;
        }
        // If the deleted item was the last one, decrement the index
        else if (indexToDelete >= savedPaths.Count)
        {
            currentViewedPathIndex = savedPaths.Count - 1;
        }
        // Otherwise, the index points to the next element (or stays the same if it was 0),
        // which is usually desired behavior after deletion. No change needed unless it was the last.

        // Update visualizations
        UpdatePathViewerVisualization(); // Update the single path view
        if (showAllPaths)
        {
            UpdateShowAllPathsVisualization(); // Update the 'all paths' view if active
        }
    }

    /// <summary>
    /// Helper method to create and configure a LineRenderer instance based on the pathLineRenderer template.
    /// </summary>
    /// <param name="pathPoints">The points for the line.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="sortingOrder">The sorting order for the renderer.</param>
    /// <param name="namePrefix">The prefix for the new GameObject name.</param>
    /// <returns>The configured LineRenderer, or null if creation failed.</returns>
    private LineRenderer CreatePathRendererInstance(List<Vector3> pathPoints, Color color, int sortingOrder, string name)
    {
        if (pathLineRenderer == null || pathLineRenderer.gameObject == null)
        {
            Debug.LogError($"[PathEditor] Cannot create path renderer '{name}': pathLineRenderer template is missing or invalid.");
            return null;
        }
        if (pathPoints == null || pathPoints.Count < 2)
        {
            Debug.LogWarning($"[PathEditor] Cannot create path renderer '{name}': Invalid path points provided.");
            return null;
        }

        GameObject newRendererGO = Instantiate(pathLineRenderer.gameObject, transform);
        newRendererGO.name = name;

        // Remove unwanted children and components from the clone
        // Destroy children first using foreach as per the original snippet
        foreach (Transform child in newRendererGO.transform)
        {
            // Use DestroyImmediate if potentially running in Edit mode scenarios,
            // but Destroy is standard for runtime. Sticking to Destroy.
            Destroy(child.gameObject);
        }

        // Get the LineRenderer component from the clone AFTER destroying children
        LineRenderer newRenderer = newRendererGO.GetComponent<LineRenderer>();
        if (newRenderer == null)
        {
            Debug.LogError($"[PathEditor] Cloned GameObject {newRendererGO.name} does not have a LineRenderer component after cleaning children.");
            Destroy(newRendererGO); // Clean up the incomplete clone
            return null;
        }

        // Remove all other components except Transform and LineRenderer
        Component[] components = newRendererGO.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (!(component is Transform) && !(component is LineRenderer))
            {
                // Use DestroyImmediate if potentially running in Edit mode scenarios,
                // but Destroy is standard for runtime. Sticking to Destroy.
                Destroy(component);
            }
        }

        // Check if LineRenderer still exists after component cleanup (should always be true based on logic)
        // This check might be redundant now but kept for safety.
        if (newRenderer == null)
        {
            Debug.LogError($"[PathEditor] LineRenderer component was unexpectedly destroyed on {newRendererGO.name}.");
            // newRendererGO might have already been destroyed if the component was null earlier
            if (newRendererGO != null) Destroy(newRendererGO);
            return null;
        }

        // Configure the renderer
        newRenderer.startColor = color;
        newRenderer.endColor = color;
        newRenderer.sortingOrder = sortingOrder;
        newRenderer.positionCount = pathPoints.Count;
        newRenderer.SetPositions(pathPoints.ToArray());

        return newRenderer;
    }
}