using System.Collections;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera pathDrawCamera;

    [SerializeField] private float transitionDuration = 0.7f;

    private Camera activeCamera;
    private bool _isTransitioning = false;

    public bool IsTransitioning => _isTransitioning;

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

    // Switch to path drawing camera with transition
    public void SwitchToPathDrawCamera()
    {
        Debug.Log("[CameraManager] Switching to path draw camera");
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
        _isTransitioning = true;

        mainCamera.gameObject.SetActive(true);
        if (pathDrawCamera != null) pathDrawCamera.gameObject.SetActive(false);
        activeCamera = mainCamera;

        // Get start/end values (relative to mainCamera)
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        float startFOV = mainCamera.fieldOfView;

        Vector3 endPos = targetPosition.position;
        Quaternion endRot = targetPosition.rotation;
        float endFOV = mainCamera.fieldOfView; // Keep same FOV

        yield return StartCoroutine(DoTransition(startPos, startRot, startFOV, endPos, endRot, endFOV, transitionDuration));

        _isTransitioning = false;
    }

    // Smooth transition between cameras
    private IEnumerator TransitionToCamera(Camera targetCamera)
    {
        _isTransitioning = true;

        // Store previous active camera to deactivate later
        Camera previousActiveCamera = activeCamera;

        // Activate the target camera immediately so we can transition *from* its current state if needed
        // But we will animate the mainCamera to the targetCamera's position/rotation/FOV
        targetCamera.gameObject.SetActive(true);

        // Get start/end values
        // Start values are from the mainCamera's current state
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        float startFOV = mainCamera.fieldOfView;

        // End values are from the target camera's state
        Vector3 endPos = targetCamera.transform.position;
        Quaternion endRot = targetCamera.transform.rotation;
        float endFOV = targetCamera.fieldOfView;

        yield return StartCoroutine(DoTransition(startPos, startRot, startFOV, endPos, endRot, endFOV, transitionDuration));

        // After transition, deactivate the previous camera if it wasn't the target
        if (previousActiveCamera != null && previousActiveCamera != targetCamera)
        {
            previousActiveCamera.gameObject.SetActive(false);
        }
        // If the target wasn't the main camera, deactivate the main camera now that the transition is done
        if (targetCamera != mainCamera)
        {
            mainCamera.gameObject.SetActive(false);
        }

        // Set new active camera
        activeCamera = targetCamera;

        _isTransitioning = false;
    }

    // Core transition logic - always animates the mainCamera
    private IEnumerator DoTransition(Vector3 startPos, Quaternion startRot, float startFOV, Vector3 endPos, Quaternion endRot, float endFOV, float duration)
    {
        // Ensure mainCamera starts at the correct state if transitioning *to* it
        mainCamera.transform.position = startPos;
        mainCamera.transform.rotation = startRot;
        mainCamera.fieldOfView = startFOV;

        mainCamera.gameObject.SetActive(true);

        float time = 0;
        while (time < duration)
        {
            float t = time / duration;
            t = t * t * (3f - 2f * t); // Smoothstep easing

            mainCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            mainCamera.fieldOfView = Mathf.Lerp(startFOV, endFOV, t);

            time += Time.deltaTime;
            yield return null;
        }

        // Ensure final transform is set exactly on mainCamera
        mainCamera.transform.position = endPos;
        mainCamera.transform.rotation = endRot;
        mainCamera.fieldOfView = endFOV;
    }
}