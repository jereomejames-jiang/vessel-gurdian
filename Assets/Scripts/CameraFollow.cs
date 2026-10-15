using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 10f;
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    // 初始视野覆盖更大的战斗区域，参考割草游戏的宽阔战场。
    public float orthographicSize = 9.5f;
    public float levelViewStep = 0.45f;
    public float maxOrthographicSize = 13f;
    public float zoomSmoothTime = 0.18f;
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
    public PlayerStats playerStats;
    public float targetOrthographicSize;
    public float zoomVelocity;
    public int trackedLevel = 1;
    public bool levelSubscribed;
    public bool initialized;

    void Awake() { viewCamera = GetComponent<Camera>(); }
    void OnEnable() { initialized = false; }
    void Start()
    {
        playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.OnLevelUp += HandleLevelUp;
            levelSubscribed = true;
            trackedLevel = Mathf.Max(1, playerStats.level);
        }
        ApplyLevelView(trackedLevel, true);
        SnapToTarget();
    }

    void LateUpdate()
    {
        TickFollow(Time.deltaTime);
        TickZoom(Time.deltaTime);
    }

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
        if (targetOrthographicSize <= 0f) ApplyLevelView(trackedLevel, true);
        viewCamera.orthographicSize = Mathf.Max(1f, targetOrthographicSize);
        initialized = true;
    }

    public void HandleLevelUp(int newLevel)
    {
        ApplyLevelView(newLevel, false);
    }

    public float GetViewSizeForLevel(int level)
    {
        return Mathf.Clamp(orthographicSize + Mathf.Max(0, level - 1) * levelViewStep,
            1f, Mathf.Max(orthographicSize, maxOrthographicSize));
    }

    public void ApplyLevelView(int level, bool instant)
    {
        trackedLevel = Mathf.Max(1, level);
        targetOrthographicSize = GetViewSizeForLevel(trackedLevel);
        if (instant || viewCamera == null || !initialized)
        {
            zoomVelocity = 0f;
            if (viewCamera != null) viewCamera.orthographicSize = targetOrthographicSize;
        }
    }

    public void TickZoom(float deltaTime)
    {
        if (viewCamera == null || deltaTime <= 0f || targetOrthographicSize <= 0f) return;
        viewCamera.orthographicSize = Mathf.SmoothDamp(viewCamera.orthographicSize,
            targetOrthographicSize, ref zoomVelocity, Mathf.Max(0.01f, zoomSmoothTime),
            Mathf.Infinity, deltaTime);
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

    void OnDestroy()
    {
        if (levelSubscribed && playerStats != null)
            playerStats.OnLevelUp -= HandleLevelUp;
    }
}
