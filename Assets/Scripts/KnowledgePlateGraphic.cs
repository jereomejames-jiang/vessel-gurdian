using UnityEngine;
using UnityEngine.UI;

// Procedural brass frame and parchment: no external art or material dependency.
public class KnowledgePlateGraphic : MaskableGraphic
{
    public Color accent = new Color(0.16f, 0.65f, 0.85f);
    public bool medallion;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (medallion)
        {
            Polygon(mesh, r, r.width * 0.48f, new Color(0.08f, 0.04f, 0.025f), new Color(0.08f, 0.04f, 0.025f));
            Polygon(mesh, Inset(r, 3f), r.width * 0.44f, new Color(0.98f, 0.83f, 0.44f), new Color(0.46f, 0.28f, 0.09f));
            Polygon(mesh, Inset(r, 8f), r.width * 0.36f, Color.Lerp(accent, Color.white, 0.3f), Color.Lerp(accent, Color.black, 0.5f));
            return;
        }
        Polygon(mesh, r, 14f, new Color(0.065f, 0.025f, 0.015f, 0.96f), new Color(0.065f, 0.025f, 0.015f, 0.96f));
        Polygon(mesh, Inset(r, 3f), 12f, new Color(0.98f, 0.82f, 0.46f), new Color(0.42f, 0.25f, 0.09f));
        Polygon(mesh, Inset(r, 7f), 10f, new Color(0.29f, 0.16f, 0.09f), new Color(0.15f, 0.065f, 0.03f));
        Polygon(mesh, Inset(r, 10f), 8f, new Color(0.97f, 0.88f, 0.69f), new Color(0.79f, 0.64f, 0.40f));
        Rect header = new Rect(r.xMin + 10f, r.center.y + 5f, r.width - 20f, r.height * 0.5f - 15f);
        Polygon(mesh, header, 7f, new Color(0.21f, 0.12f, 0.105f), new Color(0.11f, 0.055f, 0.055f));
        Quad(mesh, new Rect(r.xMin + 18f, r.center.y + 4f, r.width - 36f, 2f), new Color(0.83f, 0.61f, 0.27f));
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side < 0 ? r.xMin + 20f : r.xMax - 26f;
            for (int i = 0; i < 3; i++)
                Polygon(mesh, new Rect(x, r.yMin + 19f + i * 6f, 6f, 4f), 2f,
                    new Color(0.65f, 0.43f, 0.16f), new Color(0.65f, 0.43f, 0.16f));
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
