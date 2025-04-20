using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Store the GUI's enabled state
        bool previousGUIState = GUI.enabled;

        // Disable the control so it appears in the inspector but can't be edited
        GUI.enabled = false;

        // Draw the property as usual
        EditorGUI.PropertyField(position, property, label, true);

        // Restore the GUI's enabled state
        GUI.enabled = previousGUIState;
    }
}