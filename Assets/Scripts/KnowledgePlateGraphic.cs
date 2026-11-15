using UnityEngine;
using UnityEngine.UI;

// Procedural brass frame and parchment: no external art or material dependency.
public class KnowledgePlateGraphic : Image
{
    public Color accent = new Color(0.16f, 0.65f, 0.85f);
    public bool medallion;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (medallion)
        {
            Polygon(mesh, r, r.width * 0.48f, UITheme.HexA("#0A0806", 0.96f), UITheme.HexA("#0A0806", 0.96f));
            Polygon(mesh, Inset(r, 3f), r.width * 0.44f, UITheme.WarmAccent, UITheme.Hex("#8A6B3F"));
            // R2-04：勋章内盘降为 WarmAccent→WarmPress，不再是全 UI 最亮填充。
            Polygon(mesh, Inset(r, 8f), r.width * 0.36f, UITheme.WarmAccent, UITheme.WarmPress);
            return;
        }
        Polygon(mesh, r, 14f, UITheme.HexA("#0A0806", 0.96f), UITheme.HexA("#0A0806", 0.96f));
        // R2-04：整环降为半透明细环，与模态 1px LineCool 同量级存在感（只改颜色实参，几何不动）。
        Polygon(mesh, Inset(r, 3f), 12f, UITheme.HexA("#D9A05B", 0.45f), UITheme.HexA("#8A6B3F", 0.45f));
        Polygon(mesh, Inset(r, 7f), 10f, UITheme.Hex("#241C15"), UITheme.Hex("#241C15"));
        Polygon(mesh, Inset(r, 10f), 8f, UITheme.Hex("#2C241D"), UITheme.Hex("#2C241D"));
        Rect header = new Rect(r.xMin + 10f, r.center.y + 5f, r.width - 20f, r.height * 0.5f - 15f);
        Polygon(mesh, header, 7f, UITheme.Hex("#322A22"), UITheme.Hex("#2C241D"));
        Quad(mesh, new Rect(r.xMin + 18f, r.center.y + 4f, r.width - 36f, 2f), UITheme.HexA("#D9A05B", 0.6f));
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side < 0 ? r.xMin + 20f : r.xMax - 26f;
            for (int i = 0; i < 3; i++)
                Polygon(mesh, new Rect(x, r.yMin + 19f + i * 6f, 6f, 4f), 2f,
                    UITheme.Hex("#8A6B3F"), UITheme.Hex("#8A6B3F"));
        }
    }

    public Rect Inset(Rect r, float amount)
    {
        return new Rect(r.x + amount, r.y + amount, Mathf.Max(0f, r.width - amount * 2f), Mathf.Max(0f, r.height - amount * 2f));
    }

    public void Quad(VertexHelper mesh, Rect r, Color tint) { Polygon(mesh, r, 0f, tint, tint); }

    public void Polygon(VertexHelper mesh, Rect r, float bevel, Color top, Color bottom)
    {
        float b = Mathf.Clamp(bevel, 0f, Mathf.Min(r.width, r.height) * 0.5f);
        Vector2[] points = {
            new Vector2(r.xMin + b, r.yMin), new Vector2(r.xMax - b, r.yMin),
            new Vector2(r.xMax, r.yMin + b), new Vector2(r.xMax, r.yMax - b),
            new Vector2(r.xMax - b, r.yMax), new Vector2(r.xMin + b, r.yMax),
            new Vector2(r.xMin, r.yMax - b), new Vector2(r.xMin, r.yMin + b)
        };
        int start = mesh.currentVertCount;
        mesh.AddVert(r.center, Color.Lerp(bottom, top, 0.5f), Vector2.zero);
        foreach (Vector2 p in points)
            mesh.AddVert(p, Color.Lerp(bottom, top, Mathf.InverseLerp(r.yMin, r.yMax, p.y)), Vector2.zero);
        for (int i = 0; i < 8; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + ((i + 1) % 8));
    }
}
