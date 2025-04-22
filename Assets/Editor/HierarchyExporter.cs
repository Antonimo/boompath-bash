using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

// TODO: also include the names of the Script Components attached to the GameObjects

[InitializeOnLoad]
public class HierarchyExporter
{
    // Add menu item to Tools menu
    [MenuItem("Tools/Export Scene Hierarchy")]
    public static void ExportHierarchyMenuItem()
    {
        Debug.Log("[HierarchyExporter] Manual export triggered from Tools menu");
        ExportHierarchy();
    }

    static HierarchyExporter()
    {
        Debug.Log("[HierarchyExporter] Initializing and registering scene callbacks");
        EditorSceneManager.sceneOpened += OnSceneChanged;
        EditorSceneManager.sceneSaved += OnSceneChanged;
    }

    private static void OnSceneChanged(Scene scene, OpenSceneMode mode)
    {
        Debug.Log($"[HierarchyExporter] Scene opened: {scene.name} (Path: {scene.path}) with mode: {mode}");
        ExportHierarchy();
    }

    private static void OnSceneChanged(Scene scene)
    {
        Debug.Log($"[HierarchyExporter] Scene saved: {scene.name} (Path: {scene.path})");
        ExportHierarchy();
    }

    public static void ExportHierarchy()
    {
        Debug.Log("[HierarchyExporter] Starting hierarchy export...");

        string folderPath = "Assets/Documentation";
        Debug.Log($"[HierarchyExporter] Using documentation folder path: {folderPath}");

        if (!Directory.Exists(folderPath))
        {
            Debug.Log($"[HierarchyExporter] Creating documentation directory at: {folderPath}");
            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }

        string filePath = $"{folderPath}/HierarchyReferences.md";
        Debug.Log($"[HierarchyExporter] Writing to file: {filePath}");

        try
        {
            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                writer.WriteLine("# Hierarchy References");
                writer.WriteLine("Below is the list of top-level GameObjects in the current scene:");
                writer.WriteLine();

                var rootObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                Debug.Log($"[HierarchyExporter] Found {rootObjects.Length} total GameObjects in scene");

                int rootCount = 0;
                foreach (GameObject obj in rootObjects)
                {
                    if (obj.transform.parent == null)
                    {
                        rootCount++;
                        Debug.Log($"[HierarchyExporter] Processing root object: {obj.name}");
                        writer.WriteLine($"- **{obj.name}**");
                        WriteChildren(obj.transform, writer, 1);
                    }
                }
                Debug.Log($"[HierarchyExporter] Processed {rootCount} root GameObjects");
            }

            AssetDatabase.Refresh();
            Debug.Log($"[HierarchyExporter] Successfully exported hierarchy to {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HierarchyExporter] Error while exporting hierarchy: {e.Message}\n{e.StackTrace}");
        }
    }

    private static void WriteChildren(Transform parent, StreamWriter writer, int indentLevel)
    {
        int childCount = parent.childCount;
        Debug.Log($"[HierarchyExporter] Writing {childCount} children for {parent.name}");

        foreach (Transform child in parent)
        {
            string indent = new string(' ', indentLevel * 2);
            writer.WriteLine($"{indent}- {child.name}");
            WriteChildren(child, writer, indentLevel + 1);
        }
    }
}