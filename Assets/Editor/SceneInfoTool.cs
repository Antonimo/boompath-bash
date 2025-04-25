using UnityEngine;
using UnityEditor;

public class SceneInfoTool : EditorWindow
{
    private static Vector3 _sceneCameraPos = Vector3.zero;
    private static Vector3 _mouseWorldPos = Vector3.zero;
    private static Vector3 _selectedObjectScaledSize = Vector3.zero;
    private static Transform _selectedTransform;
    private static float _distanceBetweenObjects = 0f; // New field for distance

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
            }
            _lastMouseUpdateTime = currentTime;
        }

        // Update camera position
        if (sceneView.camera != null)
        {
            _sceneCameraPos = sceneView.camera.transform.position;
        }
        // Repaint if camera moves, might be redundant if Repaint is called elsewhere
        // Repaint();
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

        // Calculate distance if exactly two objects are selected
        if (Selection.gameObjects.Length == 2)
        {
            Transform transform1 = Selection.gameObjects[0].transform;
            Transform transform2 = Selection.gameObjects[1].transform;
            _distanceBetweenObjects = Vector3.Distance(transform1.position, transform2.position);
        }
        else
        {
            _distanceBetweenObjects = 0f; // Reset distance if not exactly two objects
        }

        Repaint(); // Repaint the Editor window when selection changes
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Scene Camera Position:", _sceneCameraPos.ToString("F2"));
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

        // Display distance if exactly two objects are selected
        if (Selection.gameObjects.Length == 2)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Distance Between Objects:");
            EditorGUILayout.LabelField($"{_distanceBetweenObjects:F2} units");
        }
    }
}