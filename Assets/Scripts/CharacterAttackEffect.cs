using System.Collections.Generic;
using UnityEngine;

public enum CharacterEffectKind { Sector, Beam, Burst, Pool, Projectile, Flash }

// Lightweight shared geometry; no per-enemy physics query or temporary material.
public class CharacterAttackEffect : MonoBehaviour
{
    public CharacterCombat source;
    public CharacterEffectKind kind;
    public Vector3 origin;
    public Vector3 destination;
    public Vector2 direction;
    public float radius;
    public float angle;
    public float length;
    public float width;
    public int damage;
    public float lifetime;
    public float elapsed;
    public float tickTimer;
    public float tickInterval;
    public bool missile;
    public bool scissors;
    public bool ripple;
    public bool retired;
    public Mesh mesh;
    public MeshRenderer meshRenderer;
    public SpriteRenderer spriteRenderer;
    public List<LineRenderer> lines = new List<LineRenderer>(3);
    public Color color;
    public static Sprite whiteSprite;
    public static Material effectMaterial;

    public static CharacterAttackEffect Create(CharacterCombat owner, Vector3 position,
        CharacterEffectKind effectKind, float duration)
    {
        GameObject obj = new GameObject("Skill_" + effectKind);
        obj.transform.position = position;
        CharacterAttackEffect effect = obj.AddComponent<CharacterAttackEffect>();
        effect.source = owner;
        effect.origin = position;
        effect.kind = effectKind;
        effect.lifetime = duration;
        effect.color = CharacterCatalog.Colors[(int)owner.role];
        if (effectMaterial == null)
        {
            SpriteRenderer playerRenderer = owner.GetComponent<SpriteRenderer>();
            if (playerRenderer != null) effectMaterial = playerRenderer.sharedMaterial;
            if (effectMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) effectMaterial = new Material(shader);
            }
        }
        return effect;
    }

    public static void Sector(CharacterCombat owner, Vector3 position, Vector2 aim, float radius,
        float degrees, int hitDamage, bool scissors)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Sector, 0.23f);
        effect.direction = aim;
        effect.radius = radius;
        effect.angle = degrees;
        effect.damage = hitDamage;
        effect.scissors = scissors;
        effect.DrawSector();
        effect.DamageArea(false);
    }

    public static void Beam(CharacterCombat owner, Vector3 position, Vector2 aim, float length,
        float width, int hitDamage, float duration)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Beam, duration);
        effect.direction = aim;
        effect.length = length;
        effect.width = width;
        effect.damage = hitDamage;
        effect.tickInterval = 0.08f;
        effect.tickTimer = 0f;
        LineRenderer line = effect.NewLine(width * 2f);
        line.positionCount = 2;
        line.SetPosition(0, position);
        line.SetPosition(1, position + (Vector3)(aim * length));
        effect.DamageArea(false);
    }

    public static void Burst(CharacterCombat owner, Vector3 position, float radius, int hitDamage, bool ripple)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Burst, ripple ? 0.6f : 0.3f);
        effect.radius = radius;
        effect.damage = hitDamage;
        effect.ripple = ripple;
        effect.NewLine(0.12f);
        if (ripple) { effect.NewLine(0.07f); effect.NewLine(0.05f); }
        effect.DamageArea(false);
    }

    public static CharacterAttackEffect Pool(CharacterCombat owner, Vector3 position, float radius,
        float duration, int hitDamage, float interval)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Pool, duration);
        effect.radius = radius;
        effect.damage = hitDamage;
        effect.tickInterval = interval;
        effect.tickTimer = interval;
        effect.AddSprite();
        effect.spriteRenderer.sprite = GetWhiteSprite();
        effect.spriteRenderer.color = new Color(effect.color.r, effect.color.g, effect.color.b, 0.12f);
        effect.transform.localScale = Vector3.one * radius * 1.45f;
        effect.NewLine(0.07f);
        return effect;
    }

    public static void Throw(CharacterCombat owner, Vector3 landing, bool missile)
    {
        CharacterAttackEffect effect = Create(owner, owner.transform.position, CharacterEffectKind.Projectile, 0.35f);
        effect.destination = landing;
        effect.missile = missile;
        effect.AddSprite();
        effect.transform.localScale = missile ? new Vector3(0.2f, 0.55f, 1f) : new Vector3(0.22f, 0.35f, 1f);
    }

    public static void Flash(CharacterCombat owner, Vector3 position, float radius, bool slow)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Flash, 0.6f);
        effect.radius = radius;
        effect.color = slow ? new Color(0.8f, 0.95f, 1f) : CharacterCatalog.Colors[2];
        effect.NewLine(0.14f);
    }

    public void Update()
    {
        if (retired || Time.timeScale <= 0f) return;
        if (source == null || source.stats == null || source.stats.currentHP <= 0 ||
            (GameManager.Instance != null && GameManager.Instance.gameEnded)) { Retire(); return; }
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / lifetime);
        if (kind == CharacterEffectKind.Projectile)
        {
            transform.position = Vector3.Lerp(origin, destination, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
            transform.Rotate(0f, 0f, Time.deltaTime * 400f);
            if (elapsed >= lifetime)
            {
                if (missile) Burst(source, destination, 1.7f, source.weapon.bulletDamage * 4 + 2, false);
                else source.AmpouleImpact(destination);
            }
        }
        else if (kind == CharacterEffectKind.Pool)
        {
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f) { tickTimer = tickInterval; DamageArea(true); }
            DrawRing(lines[0], radius, 0.45f);
        }
        else if (kind == CharacterEffectKind.Beam)
        {
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f) { tickTimer = tickInterval; DamageArea(true); }
        }
        else if (kind == CharacterEffectKind.Burst || kind == CharacterEffectKind.Flash)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                float progress = Mathf.Clamp01(t * (1f + i * 0.3f) - i * 0.18f);
                DrawRing(lines[i], Mathf.Lerp(0.15f, radius, progress), 1f - t);
            }
        }
        else if (kind == CharacterEffectKind.Sector)
        {
            if (scissors)
            {
                for (int i = 1; i < lines.Count; i++)
                {
                    float sign = i == 1 ? -1f : 1f;
                    Vector2 blade = Quaternion.Euler(0f, 0f, sign * angle * 0.5f * (1f - t)) * direction;
                    lines[i].SetPosition(0, origin);
                    lines[i].SetPosition(1, origin + (Vector3)(blade * radius));
                }
            }
        }
        if (elapsed >= lifetime) Retire();
    }

    public void DamageArea(bool slow)
    {
        source.RefreshTargets();
        // Damage can remove enemies from the spawner; this snapshot remains stable during the loop.
        for (int i = 0; i < source.targets.Count; i++)
        {
            Enemy enemy = source.targets[i];
            if (enemy == null || enemy.currentHP <= 0 || enemy.deathPending) continue;
            Vector2 offset = enemy.transform.position - origin;
            bool inside;
            if (kind == CharacterEffectKind.Beam)
            {
                float along = Vector2.Dot(offset, direction);
                float across = Mathf.Abs(offset.x * direction.y - offset.y * direction.x);
                inside = along >= 0f && along <= length && across <= width;
            }
            else
            {
                inside = offset.sqrMagnitude <= radius * radius;
                if (inside && kind == CharacterEffectKind.Sector)
                    inside = offset.sqrMagnitude < 0.01f || Vector2.Angle(direction, offset) <= angle * 0.5f;
            }
            if (!inside) continue;
            if (slow) enemy.ApplySlow(0.65f, tickInterval + 0.2f);
            enemy.TakeDamage(damage);
        }
    }

    public LineRenderer NewLine(float lineWidth)
    {
        GameObject obj = new GameObject("SkillLine");
        obj.transform.SetParent(transform, false);
        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.sharedMaterial = effectMaterial;
        line.useWorldSpace = true;
        line.startWidth = line.endWidth = lineWidth;
        line.startColor = line.endColor = color;
        line.sortingOrder = 8;
        lines.Add(line);
        return line;
    }

    public void DrawRing(LineRenderer line, float ringRadius, float alpha)
    {
        line.positionCount = 49;
        Color tint = color;
        tint.a = alpha;
        line.startColor = line.endColor = tint;
        for (int i = 0; i <= 48; i++)
        {
            float radians = i * Mathf.PI * 2f / 48f;
            line.SetPosition(i, origin + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * ringRadius);
        }
    }

    public void DrawSector()
    {
        const int segments = 24;
        Vector3[] vertices = new Vector3[segments + 2];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[segments * 3];
        LineRenderer edge = NewLine(0.1f);
        edge.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            Vector2 ray = Quaternion.Euler(0f, 0f, -angle * 0.5f + angle * i / segments) * direction;
            vertices[i + 1] = ray * radius;
            edge.SetPosition(i, origin + vertices[i + 1]);
            if (i < segments) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 2; triangles[i * 3 + 2] = i + 1; }
        }
        for (int i = 0; i < colors.Length; i++) colors[i] = new Color(color.r, color.g, color.b, 0.22f);
        mesh = new Mesh { name = "ElectroknifeFan" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = effectMaterial;
        meshRenderer.sortingOrder = 7;
        if (scissors)
        {
            NewLine(0.15f).positionCount = 2;
            NewLine(0.15f).positionCount = 2;
        }
    }

    public void AddSprite()
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetWhiteSprite();
        if (effectMaterial != null) spriteRenderer.sharedMaterial = effectMaterial;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = 9;
    }

    public static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width,
            Texture2D.whiteTexture.height), Vector2.one * 0.5f, Texture2D.whiteTexture.width);
        return whiteSprite;
    }

    public void Retire()
    {
        if (retired) return;
        retired = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    public void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
