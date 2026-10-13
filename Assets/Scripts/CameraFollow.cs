using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 10f;
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    public float orthographicSize = 7f;
    public float lookAheadDistance = 1.4f;
    public float lookAheadSmoothTime = 0.22f;
    public float teleportDistance = 15f;
    public Camera viewCamera;
    public PlayerController playerController;
    public Vector3 followVelocity;
    public Vector3 lookAheadVelocity;
    public Vector3 currentLookAhead;
    public Vector3 previousTargetPosition;
    public Transform trackedTarget;
    public bool initialized;

    void Awake() { viewCamera = GetComponent<Camera>(); }
    void OnEnable() { initialized = false; }
    void Start() { SnapToTarget(); }

    void LateUpdate() { TickFollow(Time.deltaTime); }

    public void SnapToTarget()
    {
        if (target == null) return;
        if (viewCamera == null) viewCamera = GetComponent<Camera>();
        playerController = target.GetComponent<PlayerController>();
        trackedTarget = target;
        currentLookAhead = DesiredLookAhead();
        followVelocity = Vector3.zero;
        lookAheadVelocity = Vector3.zero;
        previousTargetPosition = target.position;
        transform.SetPositionAndRotation(target.position + offset + currentLookAhead, Quaternion.identity);
        viewCamera.orthographic = true;
        viewCamera.orthographicSize = Mathf.Max(1f, orthographicSize);
        initialized = true;
    }

    public Vector3 DesiredLookAhead()
    {
        if (target == null || playerController == null || !playerController.autoMove) return Vector3.zero;
        // Use the same interpolated pose as the visible player and arrow.
        return target.up * Mathf.Max(0f, lookAheadDistance);
    }

    public void TickFollow(float deltaTime)
    {
        if (target == null) { initialized = false; return; }
        if (!initialized || trackedTarget != target ||
            Vector3.Distance(previousTargetPosition, target.position) > teleportDistance)
        {
            SnapToTarget();
            return;
        }
        if (deltaTime <= 0f) return; // Freeze framing during upgrades and game over.
        previousTargetPosition = target.position;
        currentLookAhead = Vector3.SmoothDamp(currentLookAhead, DesiredLookAhead(),
            ref lookAheadVelocity, Mathf.Max(0.01f, lookAheadSmoothTime), Mathf.Infinity, deltaTime);
        Vector3 desired = target.position + offset + currentLookAhead;
        transform.position = Vector3.SmoothDamp(transform.position, desired,
            ref followVelocity, 1f / Mathf.Max(0.1f, smoothSpeed), Mathf.Infinity, deltaTime);
        transform.rotation = Quaternion.identity;
    }
}
