using UnityEngine;
using UnityEditor;
using System.Collections.Generic; // Required if PathEditor uses List

/// <summary>
/// Custom Inspector for the PathEditor script.
/// Adds buttons for saving paths, viewing paths, and deleting paths.
/// </summary>
[CustomEditor(typeof(PathEditor))]
public class PathEditorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector fields
        DrawDefaultInspector();

        // Get the PathEditor component instance
        PathEditor pathEditor = (PathEditor)target;

        // Add some space for clarity
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Path Management", EditorStyles.boldLabel);

        // Display the number of saved paths
        EditorGUILayout.LabelField("Saved Path Count", pathEditor.SavedPaths.Count.ToString());

        // Button to save the current path
        if (GUILayout.Button("Save Current Path"))
        {
            // Record the object state for undo functionality
            Undo.RecordObject(pathEditor, "Save Path");

            // Call the public method to save the path
            pathEditor.SaveCurrentPath();

            // Mark the object as dirty to ensure changes are saved
            // (Might not be strictly necessary if only modifying list data,
            // but good practice for editor actions)
            EditorUtility.SetDirty(pathEditor);
        }

        // Path Viewer Controls Section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Path Viewer Controls", EditorStyles.boldLabel);

        // Use the public property for the toggle to ensure visualization updates
        bool currentShowViewer = pathEditor.ShowPathViewer;
        bool newShowViewer = EditorGUILayout.Toggle("Show Path Viewer", currentShowViewer);
        if (newShowViewer != currentShowViewer)
        {
            Undo.RecordObject(pathEditor, "Toggle Path Viewer");
            pathEditor.ShowPathViewer = newShowViewer;
            EditorUtility.SetDirty(pathEditor);
        }

        // Only show index and buttons if the viewer is enabled and paths exist
        if (pathEditor.ShowPathViewer && pathEditor.SavedPaths.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();

            // Previous Button
            if (GUILayout.Button("<< Prev"))
            {
                Undo.RecordObject(pathEditor, "Show Previous Path");
                pathEditor.ShowPreviousPath();
                EditorUtility.SetDirty(pathEditor);
            }

            // Display Current Index (Read-only is fine here, buttons control it)
            EditorGUILayout.LabelField($"Viewing: {pathEditor.CurrentViewedPathIndex + 1} / {pathEditor.SavedPaths.Count}", EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandWidth(true));

            // Next Button
            if (GUILayout.Button("Next >>"))
            {
                Undo.RecordObject(pathEditor, "Show Next Path");
                pathEditor.ShowNextPath();
                EditorUtility.SetDirty(pathEditor);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Delete Button
            if (GUILayout.Button("Delete Current Path"))
            {
                Undo.RecordObject(pathEditor, "Delete Current Path");
                pathEditor.DeleteCurrentViewedPath(); // Call the method to delete
                EditorUtility.SetDirty(pathEditor);
                // Exit GUI here to prevent potential layout errors after deletion
                // if the path count becomes zero or the index changes.
                GUIUtility.ExitGUI();
            }
        }
        else if (pathEditor.ShowPathViewer)
        {
            EditorGUILayout.HelpBox("No saved paths to view.", MessageType.Info);
        }

        // --- Save All Paths to JSON Button ---
        EditorGUILayout.Space(); // Add some visual separation
        if (GUILayout.Button("Save All Paths to File"))
        {
            pathEditor.SavePathsToJson();
        }
    }
}