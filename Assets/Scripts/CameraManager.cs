using System.Collections;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera pathDrawCamera;

    [SerializeField] private float transitionDuration = 0.7f;

    private Camera activeCamera;

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        SetActiveCameraToMain();
    }

    public void SetActiveCameraToMain()
    {
        if (mainCamera != null)
        {
            activeCamera = mainCamera;
            mainCamera.gameObject.SetActive(true);
            pathDrawCamera?.gameObject.SetActive(false);
        }
    }

    public void SetActiveCameraToPathDraw()
    {
        if (pathDrawCamera != null)
        {
            activeCamera = pathDrawCamera;
            pathDrawCamera.gameObject.SetActive(true);
            mainCamera?.gameObject.SetActive(false);
        }
    }

    // Switch to main gameplay camera with transition
    public void SwitchToMainCamera(Transform targetPosition = null)
    {
        if (activeCamera != mainCamera)
        {
            if (targetPosition != null)
            {
                StartCoroutine(TransitionToPosition(targetPosition));
            }
            else
            {
                // TODO: use game start camera position
                StartCoroutine(TransitionToCamera(mainCamera));
            }
        }
    }

    // Switch to path drawing camera with transition
    public void SwitchToPathDrawCamera()
    {
        Debug.Log("[CameraManager] Switching to path draw camera");
        // Log the state of the variables
        Debug.Log($"Active Camera: {activeCamera?.name}");
        Debug.Log($"Path Draw Camera: {pathDrawCamera?.name}");

        if (activeCamera != pathDrawCamera && pathDrawCamera != null)
        {
            StartCoroutine(TransitionToCamera(pathDrawCamera));
        }
    }

    // Smooth transition to a specific position
    private IEnumerator TransitionToPosition(Transform targetPosition)
    {
        // Make sure main camera is active
        mainCamera.gameObject.SetActive(true);
        if (pathDrawCamera != null) pathDrawCamera.gameObject.SetActive(false);
        activeCamera = mainCamera;

        // Store initial transform values
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        float startFOV = mainCamera.fieldOfView;

        Vector3 endPos = targetPosition.position;
        Quaternion endRot = targetPosition.rotation;
        float endFOV = mainCamera.fieldOfView; // Keep same FOV

        // Transition
        float time = 0;
        while (time < transitionDuration)
        {
            float t = time / transitionDuration;

            // Smoothstep for easing
            t = t * t * (3f - 2f * t);

            // Update camera transform
            mainCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            mainCamera.fieldOfView = Mathf.Lerp(startFOV, endFOV, t);

            time += Time.deltaTime;
            yield return null;
        }

        // Set final position
        mainCamera.transform.position = endPos;
        mainCamera.transform.rotation = endRot;
        mainCamera.fieldOfView = endFOV;
    }

    // Smooth transition between cameras
    private IEnumerator TransitionToCamera(Camera targetCamera)
    {
        // Make sure target camera is active
        targetCamera.gameObject.SetActive(true);

        // Store initial transform values
        Vector3 startPos = activeCamera.transform.position;
        Quaternion startRot = activeCamera.transform.rotation;
        float startFOV = activeCamera.fieldOfView;

        Vector3 endPos = targetCamera.transform.position;
        Quaternion endRot = targetCamera.transform.rotation;
        float endFOV = targetCamera.fieldOfView;

        // Transition
        float time = 0;
        while (time < transitionDuration)
        {
            float t = time / transitionDuration;

            // Smoothstep for easing
            t = t * t * (3f - 2f * t);

            // Update active camera transform
            activeCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
            activeCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            activeCamera.fieldOfView = Mathf.Lerp(startFOV, endFOV, t);

            time += Time.deltaTime;
            yield return null;
        }

        // Set final position
        activeCamera.transform.position = endPos;
        activeCamera.transform.rotation = endRot;
        activeCamera.fieldOfView = endFOV;

        // Disable previous active camera if it's different
        if (activeCamera != targetCamera)
        {
            activeCamera.gameObject.SetActive(false);
        }

        // Set new active camera
        activeCamera = targetCamera;
    }
}