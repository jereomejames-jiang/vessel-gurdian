using UnityEngine;

/// <summary>
/// Drives the actual level brick field behind the menu preview. A hidden flow
/// target advances while the menu hero stays in view, so the field scrolls as
/// if the hero were moving forward.
/// </summary>
public class MainMenuBackground : MonoBehaviour
{
    public Transform playerTransform;
    public SpriteRenderer playerSprite;
    public InfiniteBrickBackground brickBackground;
    public float scrollSpeed = 5f;
    public float centerDuration = 1.5f;
    public Vector3 initialPosition = new Vector3(0f, 1.55f, 0f);
    public Vector3 centerPosition = Vector3.zero;
    public bool isMovingToCenter;
    public bool isReturningToStart;
    public float moveTimer;
    public Vector3 startPosition;
    Transform backgroundFlow;

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.Find("MainMenuPlayer");
            if (player != null) playerTransform = player.transform;
        }
        if (playerSprite == null && playerTransform != null)
            playerSprite = playerTransform.GetComponent<SpriteRenderer>();

        if (playerTransform != null)
        {
            playerTransform.position = initialPosition;
            if (playerSprite != null) playerSprite.color = Color.white;
        }

        EnsureLevelBackground();
    }

    void Update()
    {
        if (backgroundFlow != null)
        {
            if (brickBackground != null) brickBackground.ScrollDown(scrollSpeed * Time.deltaTime);
        }

        if ((isMovingToCenter || isReturningToStart) && playerTransform != null)
        {
            moveTimer += Time.deltaTime;
            float t = Mathf.Clamp01(moveTimer / centerDuration);
            float eased = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
            Vector3 target = isReturningToStart ? initialPosition : centerPosition;
            playerTransform.position = Vector3.Lerp(startPosition, target, eased);
            if (t >= 1f)
            {
                bool arrivedAtCenter = isMovingToCenter;
                isMovingToCenter = false;
                isReturningToStart = false;
                if (arrivedAtCenter && MainMenuUI.Instance != null)
                    MainMenuUI.Instance.OnPlayerArrivedAtCenter();
            }
        }
    }

    public void MoveToCenter()
    {
        if (playerTransform == null) return;
        startPosition = playerTransform.position;
        isMovingToCenter = true;
        isReturningToStart = false;
        moveTimer = 0f;
    }

    public void MoveToStart()
    {
        if (playerTransform == null) return;
        startPosition = playerTransform.position;
        isMovingToCenter = false;
        isReturningToStart = true;
        moveTimer = 0f;
    }

    void EnsureLevelBackground()
    {
        backgroundFlow = new GameObject("MainMenuBackgroundFlow").transform;
        backgroundFlow.position = Vector3.zero;

        brickBackground = GetComponent<InfiniteBrickBackground>();
        if (brickBackground == null) brickBackground = gameObject.AddComponent<InfiniteBrickBackground>();
        brickBackground.columns = 25;
        brickBackground.rows = 31;
        brickBackground.brickWidth = 2f;
        brickBackground.brickHeight = 1f;
        brickBackground.pixelsPerUnit = 32f;
        brickBackground.sortingOrder = -100;
        brickBackground.target = backgroundFlow;
        ApplySelectedLevelTheme(brickBackground);
        brickBackground.BuildField();

        Camera menuCamera = Camera.main;
        if (menuCamera != null)
        {
            menuCamera.orthographic = true;
            menuCamera.orthographicSize = 7.2f;
            menuCamera.backgroundColor = new Color(0.015f, 0.025f, 0.05f, 1f);
        }
    }

    public void RefreshSelectedLevelTheme()
    {
        if (brickBackground == null) return;
        ApplySelectedLevelTheme(brickBackground);
        brickBackground.RefreshTheme();
    }

    static void ApplySelectedLevelTheme(InfiniteBrickBackground background)
    {
        int level = Mathf.Clamp(PlayerPrefs.GetInt("VesselGuard.SelectedLevel", 1), 1, 3);
        if (level == 1)
        {
            background.brickColor = new Color(0.16f, 0.075f, 0.12f, 1f);
            background.mortarColor = new Color(0.035f, 0.018f, 0.035f, 1f);
        }
        else if (level == 2)
        {
            background.brickColor = new Color(0.035f, 0.11f, 0.2f, 1f);
            background.mortarColor = new Color(0.008f, 0.025f, 0.055f, 1f);
        }
        else
        {
            background.brickColor = new Color(0.2f, 0.045f, 0.055f, 1f);
            background.mortarColor = new Color(0.045f, 0.008f, 0.012f, 1f);
        }
    }
}
