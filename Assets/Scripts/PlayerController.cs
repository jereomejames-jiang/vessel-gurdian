using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float turnSpeed = 180f;
    public float headingAngle = 90f;
    public bool autoMove = true;
    public Rigidbody2D rb;
    public float currentThrottle;
    public float turnInput;

    public Vector2 Heading
    {
        get
        {
            float radians = headingAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.rotation = headingAngle - 90f;
    }

    void Update()
    {
        // Sample keyboard every rendered frame; move only on the physics clock.
        turnInput = 0f;
        if (Time.timeScale <= 0f || !Application.isFocused) return;
        if (Input.GetKey(KeyCode.A)) turnInput += 1f;
        if (Input.GetKey(KeyCode.D)) turnInput -= 1f;
    }

    void FixedUpdate()
    {
        if (Time.timeScale <= 0f || rb == null) return;
        ApplyTurnInput(turnInput, Time.fixedDeltaTime);
        currentThrottle = autoMove ? 1f : 0f;
        // Always advance along the current heading; W/S have no effect.
        rb.MovePosition(rb.position + Heading * (moveSpeed * currentThrottle * Time.fixedDeltaTime));
        rb.MoveRotation(headingAngle - 90f);
    }

    void OnDisable() { turnInput = 0f; }
    void OnApplicationFocus(bool focused) { if (!focused) turnInput = 0f; }

    public void ApplyTurnInput(float input, float deltaTime)
    {
        SetHeading(headingAngle + Mathf.Clamp(input, -1f, 1f) * turnSpeed * Mathf.Max(0f, deltaTime));
    }

    public void SetHeading(float angle)
    {
        headingAngle = Mathf.Repeat(angle + 180f, 360f) - 180f;
    }
}
