using UnityEngine;

// A recycled brick field that stays world anchored while extending forever.
public class InfiniteBrickBackground : MonoBehaviour
{
    public Transform target;
    // 覆盖初始视野以及升级后的最大缩放范围。
    public int columns = 25;
    public int rows = 31;
    public float brickWidth = 2f;
    public float brickHeight = 1f;
    public float pixelsPerUnit = 32f;
    public Color brickColor = new Color(0.055f, 0.075f, 0.12f, 1f);
    public Color mortarColor = new Color(0.015f, 0.025f, 0.05f, 1f);
    public int sortingOrder = -100;
    public SpriteRenderer sourceRenderer;
    public Sprite brickSprite;
    public Transform tileRoot;
    public int currentCellX;
    public int currentCellY;
    float lastCoverageOrthoSize = -1f;
    float lastCoverageAspect = -1f;
    float scrollOffsetY;

    void Awake()
    {
        transform.localScale = Vector3.one;
        if (sourceRenderer == null) sourceRenderer = GetComponent<SpriteRenderer>();
        if (sourceRenderer != null) sourceRenderer.enabled = false;
        if (target == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null) target = player.transform;
        }
        // A menu preview assigns its independent scrolling target immediately
        // after AddComponent. Defer field construction until that target exists.
        if (target != null) BuildField();
    }

    void LateUpdate()
    {
        if (target == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null) target = player.transform;
        }
        if (target == null) return;

        EnsureCameraCoverage();

        int nextCellX = Mathf.FloorToInt(target.position.x / brickWidth);
        int nextCellY = Mathf.FloorToInt(target.position.y / brickHeight);
        if (nextCellX != currentCellX || nextCellY != currentCellY)
        {
            currentCellX = nextCellX;
            currentCellY = nextCellY;
            LayoutField();
        }
    }

    void EnsureCameraCoverage()
    {
        Camera view = Camera.main;
        if (view == null || !view.orthographic) return;
        float aspect = Mathf.Max(0.5f, view.aspect);
        if (Mathf.Abs(lastCoverageOrthoSize - view.orthographicSize) < 0.05f &&
            Mathf.Abs(lastCoverageAspect - aspect) < 0.02f) return;
        lastCoverageOrthoSize = view.orthographicSize;
        lastCoverageAspect = aspect;

        // Keep a comfortable margin around every camera edge. The recycled
        // grid follows the target forever; dimensions only grow when the view
        // becomes wider or more zoomed out than the original layout.
        int neededColumns = Mathf.CeilToInt((view.orthographicSize * 2f * aspect + brickWidth * 4f) / brickWidth);
        int neededRows = Mathf.CeilToInt((view.orthographicSize * 2f + brickHeight * 4f) / brickHeight);
        neededColumns = Mathf.Max(columns, neededColumns);
        neededRows = Mathf.Max(rows, neededRows);
        if ((neededColumns & 1) == 0) neededColumns++;
        if ((neededRows & 1) == 0) neededRows++;
        if (neededColumns == columns && neededRows == rows) return;
        columns = neededColumns;
        rows = neededRows;
        BuildField();
    }

    public void BuildField()
    {
        if (tileRoot == null)
        {
            GameObject root = new GameObject("InfiniteBrickTiles");
            tileRoot = root.transform;
            tileRoot.SetParent(transform, false);
        }
        if (brickSprite == null) brickSprite = CreateBrickSprite();

        for (int i = tileRoot.childCount - 1; i >= 0; i--)
            Destroy(tileRoot.GetChild(i).gameObject);

        columns = Mathf.Max(5, columns | 1);
        rows = Mathf.Max(7, rows | 1);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                GameObject tile = new GameObject("Brick");
                tile.transform.SetParent(tileRoot, false);
                SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = brickSprite;
                renderer.color = Color.white;
                renderer.sortingOrder = sortingOrder;
            }
        }
        LayoutField();
    }

    public void RefreshTheme()
    {
        if (brickSprite != null)
        {
            Texture2D oldTexture = brickSprite.texture;
            Destroy(brickSprite);
            if (oldTexture != null) Destroy(oldTexture);
            brickSprite = null;
        }
        BuildField();
    }

    public void SetTheme(Color newBrickColor, Color newMortarColor)
    {
        brickColor = newBrickColor;
        mortarColor = newMortarColor;
        RefreshTheme();
    }

    public void LayoutField()
    {
        if (tileRoot == null || target == null) return;
        scrollOffsetY = 0f;
        tileRoot.localPosition = Vector3.zero;
        int index = 0;
        int halfColumns = columns / 2;
        int halfRows = rows / 2;
        for (int y = 0; y < rows; y++)
        {
            int worldY = currentCellY + y - halfRows;
            float rowOffset = (worldY & 1) == 0 ? 0f : brickWidth * 0.5f;
            for (int x = 0; x < columns; x++)
            {
                int worldX = currentCellX + x - halfColumns;
                Transform tile = tileRoot.GetChild(index++);
                tile.localPosition = new Vector3(worldX * brickWidth + rowOffset - transform.position.x,
                    worldY * brickHeight - transform.position.y, 0f);
            }
        }
    }

    /// <summary>Scrolls a fixed camera background and recycles each passed row.</summary>
    public void ScrollDown(float distance)
    {
        if (tileRoot == null || distance <= 0f || brickHeight <= 0f) return;
        scrollOffsetY -= distance;
        // The masonry pattern repeats every two rows (including the half-brick
        // horizontal offset), so wrap the visual scroll phase instead of moving
        // rows through the field. This keeps the camera coverage centered forever.
        float repeatDistance = brickHeight * 2f;
        while (scrollOffsetY <= -repeatDistance) scrollOffsetY += repeatDistance;
        tileRoot.localPosition = new Vector3(0f, scrollOffsetY, 0f);
    }

    public Sprite CreateBrickSprite()
    {
        int width = Mathf.Max(16, Mathf.RoundToInt(brickWidth * pixelsPerUnit));
        int height = Mathf.Max(12, Mathf.RoundToInt(brickHeight * pixelsPerUnit));
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "RuntimeBrickTexture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool mortar = x < 2 || y < 2 || x >= width - 2 || y >= height - 2;
                pixels[y * width + x] = mortar ? mortarColor : brickColor;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        brickSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        brickSprite.name = "RuntimeBrick";
        return brickSprite;
    }
}
