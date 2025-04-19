using UnityEngine;
using UnityEditor;

public class SceneInfoTool : EditorWindow
{
    private static Vector3 _mouseWorldPos = Vector3.zero;
    private static Vector3 _selectedObjectScaledSize = Vector3.zero;
    private static Transform _selectedTransform;

    private float _mouseUpdateInterval = 0.1f; // Update every 0.1 seconds
    private double _lastMouseUpdateTime;

    [MenuItem("Tools/Scene Info Tool")]
    public static void ShowWindow()
    {
        GetWindow<SceneInfoTool>("Scene Info");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += UpdateSelectedObjectInfo;
        UpdateSelectedObjectInfo(); // Initial update on enable
        _lastMouseUpdateTime = EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Selection.selectionChanged -= UpdateSelectedObjectInfo;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        double currentTime = EditorApplication.timeSinceStartup;
        if (currentTime - _lastMouseUpdateTime >= _mouseUpdateInterval)
        {
            Event current = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero); // Adjust if needed
            float rayDistance;

            if (groundPlane.Raycast(ray, out rayDistance))
            {
                _mouseWorldPos = ray.GetPoint(rayDistance);
                Repaint(); // Repaint the Editor window when mouse position updates
                // sceneView.Repaint(); // No need to repaint SceneView every mouse update in this setup
            }
            _lastMouseUpdateTime = currentTime;
        }
    }

    private void UpdateSelectedObjectInfo()
    {
        _selectedTransform = Selection.activeTransform;
        if (_selectedTransform != null)
        {
            Renderer selectedRenderer = _selectedTransform.GetComponent<Renderer>();
            if (selectedRenderer != null)
            {
                _selectedObjectScaledSize = selectedRenderer.bounds.size;
            }
            else
            {
                Collider selectedCollider = _selectedTransform.GetComponent<Collider>();
                if (selectedCollider != null)
                {
                    _selectedObjectScaledSize = selectedCollider.bounds.size;
                }
                else
                {
                    _selectedObjectScaledSize = Vector3.zero; // No renderer or collider
                }
            }
        }
        else
        {
            _selectedObjectScaledSize = Vector3.zero; // Nothing selected
        }
        Repaint(); // Repaint the Editor window when selection changes
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Mouse World Position:", _mouseWorldPos.ToString("F2"));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Selected Object Scaled Size:");
        if (_selectedTransform != null)
        {
            EditorGUILayout.LabelField(_selectedObjectScaledSize.ToString("F2"));
        }
        else
        {
            EditorGUILayout.LabelField("No object selected.");
        }
    }
}