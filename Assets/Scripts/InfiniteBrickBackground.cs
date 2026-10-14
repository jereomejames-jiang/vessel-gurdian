using UnityEngine;

// A recycled brick field that stays world anchored while extending forever.
public class InfiniteBrickBackground : MonoBehaviour
{
    public Transform target;
    public int columns = 17;
    public int rows = 21;
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
        BuildField();
    }

    void LateUpdate()
    {
        if (target == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null) target = player.transform;
        }
        if (target == null) return;

        int nextCellX = Mathf.FloorToInt(target.position.x / brickWidth);
        int nextCellY = Mathf.FloorToInt(target.position.y / brickHeight);
        if (nextCellX != currentCellX || nextCellY != currentCellY)
        {
            currentCellX = nextCellX;
            currentCellY = nextCellY;
            LayoutField();
        }
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

    public void LayoutField()
    {
        if (tileRoot == null || target == null) return;
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
                tile.position = new Vector3(worldX * brickWidth + rowOffset, worldY * brickHeight, transform.position.z);
            }
        }
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
