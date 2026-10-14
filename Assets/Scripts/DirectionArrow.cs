using UnityEngine;

[DefaultExecutionOrder(110)]
public class DirectionArrow : MonoBehaviour
{
    public Transform target;
    public Vector3 worldOffset = new Vector3(0f, -0.82f, -0.1f);
    // Retained for compatibility with existing rebuild scripts.
    public float followSmooth = 24f;
    public PlayerController playerController;

    void LateUpdate() { UpdatePose(); }

    public void UpdatePose()
    {
        if (target == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
            if (playerController == null) return;
            target = playerController.transform;
        }
        // No second smoothing pass: stay exactly beneath the rendered player.
        transform.position = target.position + worldOffset;
        // Player Rigidbody2D intentionally freezes physical rotation. Use the
        // controller heading directly so the direction marker visibly turns.
        float visualAngle = playerController != null ? playerController.headingAngle - 90f : target.eulerAngles.z;
        transform.rotation = Quaternion.Euler(0f, 0f, visualAngle);
    }
}
