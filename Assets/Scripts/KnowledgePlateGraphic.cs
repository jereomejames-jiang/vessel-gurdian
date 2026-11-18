using UnityEngine;
using UnityEngine.UI;

// Procedural brass frame and parchment: no external art or material dependency.
public class KnowledgePlateGraphic : Image
{
    public Color accent = new Color(0.16f, 0.65f, 0.85f);
    public bool medallion;
    // 入场时分隔线左→右画出（0→1），KnowledgeTicker 每帧驱动。
    public float dividerT = 1f;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (medallion)
        {
            // 方案A：深色方形切角井 + 技能色半透环 + 琥珀角括弧，像素图标在深底上原色可读。
            Color ring = new Color(accent.r, accent.g, accent.b, 0.5f);
            Polygon(mesh, r, 10f, ring, ring);
            Polygon(mesh, Inset(r, 3f), 8f, UITheme.Deep, UITheme.Deep);
            Rect b = Inset(r, 6f);
            float arm = 14f, th = 3f;
            Quad(mesh, new Rect(b.xMin, b.yMax - th, arm, th), UITheme.WarmAccent);
            Quad(mesh, new Rect(b.xMin, b.yMax - arm, th, arm), UITheme.WarmAccent);
            Quad(mesh, new Rect(b.xMax - arm, b.yMax - th, arm, th), UITheme.WarmAccent);
            Quad(mesh, new Rect(b.xMax - th, b.yMax - arm, th, arm), UITheme.WarmAccent);
            Quad(mesh, new Rect(b.xMin, b.yMin, arm, th), UITheme.WarmAccent);
            Quad(mesh, new Rect(b.xMin, b.yMin, th, arm), UITheme.WarmAccent);
            Quad(mesh, new Rect(b.xMax - arm, b.yMin, arm, th), UITheme.WarmAccent);
            Quad(mesh, new Rect(b.xMax - th, b.yMin, th, arm), UITheme.WarmAccent);
            return;
        }
        Polygon(mesh, r, 14f, UITheme.HexA("#0A0806", 0.96f), UITheme.HexA("#0A0806", 0.96f));
        // R2-04：整环降为半透明细环，与模态 1px LineCool 同量级存在感（只改颜色实参，几何不动）。
        Polygon(mesh, Inset(r, 3f), 12f, UITheme.HexA("#D9A05B", 0.45f), UITheme.HexA("#8A6B3F", 0.45f));
        Polygon(mesh, Inset(r, 7f), 10f, UITheme.Hex("#241C15"), UITheme.Hex("#241C15"));
        Polygon(mesh, Inset(r, 10f), 8f, UITheme.Hex("#2C241D"), UITheme.Hex("#2C241D"));
        Rect header = new Rect(r.xMin + 10f, r.yMax - 82f, r.width - 20f, 72f);
        Color headerTint = Color.Lerp(UITheme.Hex("#2C241D"), UITheme.Hex("#322A22"), 0.55f);
        Polygon(mesh, header, 7f, headerTint, headerTint);
        // 分隔线从图标井右侧起笔（不再横穿勋章），宽度随 dividerT 画出。
        float divX = r.xMin + 116f;
        float divW = Mathf.Max(0f, r.xMax - 18f - divX) * Mathf.Clamp01(dividerT);
        if (divW > 0f) Quad(mesh, new Rect(divX, r.yMax - 82f, divW, 2f), UITheme.HexA("#D9A05B", 0.6f));
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
