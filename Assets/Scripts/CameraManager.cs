using System.Collections;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera pathDrawCamera;
    
    [SerializeField] private float transitionDuration = 1.0f;
    
    private Camera activeCamera;
    
    private void Start()
    {
        // Set up cameras
        if (mainCamera == null) mainCamera = Camera.main;
        
        // Initially disable path draw camera
        if (pathDrawCamera != null)
        {
            pathDrawCamera.gameObject.SetActive(false);
        }
        
        activeCamera = mainCamera;
    }
    
    // Switch to main gameplay camera with transition
    public void SwitchToMainCamera()
    {
        if (activeCamera != mainCamera)
        {
            StartCoroutine(TransitionToCamera(mainCamera));
        }
    }
    
    // Switch to path drawing camera with transition
    public void SwitchToPathDrawCamera()
    {
        if (activeCamera != pathDrawCamera && pathDrawCamera != null)
        {
            StartCoroutine(TransitionToCamera(pathDrawCamera));
        }
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