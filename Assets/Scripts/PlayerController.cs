using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int virtualKeyCode);
#endif

    public float moveSpeed = 5f;
    public float turnSpeed = 180f;
    public float headingAngle = 90f;
    public bool autoMove = true;
    public Rigidbody2D rb;
    public float currentThrottle;
    public float turnInput;
    public float turnHoldDuration;
    public float signedTurnDuration;
    public int slowZoneCount;
    public float slowZoneMultiplier = 1f;
    public List<float> activeSlowZoneMultipliers = new List<float>();

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
        Application.runInBackground = true;
        moveSpeed = GameBalance.Get("player.move_speed", moveSpeed);
        turnSpeed = GameBalance.Get("player.rotate_speed", turnSpeed);
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.rotation = headingAngle - 90f;
    }

    void Update()
    {
        // Read the held signal every rendered frame. The longer A/D stays held,
        // the longer FixedUpdate keeps rotating the heading.
        if (Time.timeScale <= 0f)
        {
            turnInput = 0f;
            turnHoldDuration = 0f;
            return;
        }

        turnInput = ReadTurnInput();
        if (Mathf.Abs(turnInput) > 0.01f)
        {
            turnHoldDuration += Time.unscaledDeltaTime;
            signedTurnDuration += turnInput * Time.unscaledDeltaTime;
        }
        else
        {
            turnHoldDuration = 0f;
        }
    }

    void FixedUpdate()
    {
        if (Time.timeScale <= 0f || rb == null) return;
        ApplyTurnInput(turnInput, Time.fixedDeltaTime);
        currentThrottle = autoMove ? 1f : 0f;
        // Always advance along the current heading; W/S have no effect.
        rb.MovePosition(rb.position + Heading * (moveSpeed * currentThrottle * slowZoneMultiplier * Time.fixedDeltaTime));
        rb.MoveRotation(headingAngle - 90f);
    }

    void OnDisable()
    {
        turnInput = 0f;
        turnHoldDuration = 0f;
    }

    public float ReadTurnInput()
    {
        bool aPressed = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool dPressed = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            aPressed = aPressed || keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
            dPressed = dPressed || keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
        }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // Unity's input APIs only receive keys while the Game view has focus.
        // The Windows fallback keeps steering responsive when the editor focus
        // is on another panel while the game is running.
        aPressed = aPressed || IsGlobalKeyDown(0x41) || IsGlobalKeyDown(0x25);
        dPressed = dPressed || IsGlobalKeyDown(0x44) || IsGlobalKeyDown(0x27);
#endif
        if (aPressed == dPressed)
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            return Mathf.Clamp(-horizontal, -1f, 1f);
        }
        return aPressed ? 1f : -1f;
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    public bool IsGlobalKeyDown(int virtualKeyCode)
    {
        return (GetAsyncKeyState(virtualKeyCode) & 0x8000) != 0;
    }
#endif

    public void ApplyTurnInput(float input, float deltaTime)
    {
        SetHeading(headingAngle + Mathf.Clamp(input, -1f, 1f) * turnSpeed * Mathf.Max(0f, deltaTime));
    }

    public void SetHeading(float angle)
    {
        headingAngle = Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    public void AddSlowZone(float multiplier)
    {
        activeSlowZoneMultipliers.Add(Mathf.Clamp(multiplier, GameBalance.Get("player.slow_zone_min", 0.1f), 1f));
        slowZoneCount = activeSlowZoneMultipliers.Count;
        RecalculateSlowZoneMultiplier();
    }

    public void RemoveSlowZone(float multiplier)
    {
        float value = Mathf.Clamp(multiplier, GameBalance.Get("player.slow_zone_min", 0.1f), 1f);
        for (int i = 0; i < activeSlowZoneMultipliers.Count; i++)
        {
            if (Mathf.Abs(activeSlowZoneMultipliers[i] - value) < 0.01f)
            {
                activeSlowZoneMultipliers.RemoveAt(i);
                break;
            }
        }
        slowZoneCount = activeSlowZoneMultipliers.Count;
        RecalculateSlowZoneMultiplier();
    }

    public void RecalculateSlowZoneMultiplier()
    {
        slowZoneMultiplier = 1f;
        for (int i = 0; i < activeSlowZoneMultipliers.Count; i++)
            slowZoneMultiplier = Mathf.Min(slowZoneMultiplier, activeSlowZoneMultipliers[i]);
    }
}
