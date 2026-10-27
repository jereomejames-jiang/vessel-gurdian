using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Original procedural art adapter. It follows the readable silhouettes,
/// saturated drops, dark panels, and chunky feedback of survivor games without
/// loading any third-party texture or prefab at runtime.
/// </summary>
public static class MonsterSurvivorArtLibrary
{
    static readonly Dictionary<string, Sprite[]> frameCache = new Dictionary<string, Sprite[]>();
    static readonly Dictionary<string, Sprite> singleCache = new Dictionary<string, Sprite>();

    public static Sprite[] LoadFrames(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return null;
        Sprite[] cached;
        // Unity objects can be destroyed during Play Mode/domain reload while
        // their managed references remain in this static cache (Unity fake-null).
        // Rebuild the complete set whenever any cached frame has been destroyed.
        if (frameCache.TryGetValue(assetName, out cached) && cached != null &&
            cached.Length == 4 && Array.TrueForAll(cached, sprite => sprite != null))
            return cached;
        cached = new Sprite[4];
        for (int i = 0; i < cached.Length; i++) cached[i] = MakeSprite(assetName + "_frame_" + i, assetName, i);
        frameCache[assetName] = cached;
        return cached;
    }

    public static Sprite LoadSingle(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return null;
        Sprite cached;
        if (singleCache.TryGetValue(assetName, out cached) && cached != null) return cached;
        cached = MakeSprite("single_" + assetName, assetName, 0);
        singleCache[assetName] = cached;
        return cached;
    }

    public static Sprite FirstFrame(string assetName)
    {
        Sprite[] frames = LoadFrames(assetName);
        return frames == null || frames.Length == 0 ? null : frames[0];
    }

    public static Sprite SkillIcon(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.MembraneStent: return LoadSingle("ui_ab_shield");
            case UpgradeType.FlushNeedle: return LoadSingle("ui_ab_lightning");
            case UpgradeType.DissectionScissors: return LoadSingle("ui_ab_sword");
            case UpgradeType.SidewallForceps: return LoadSingle("ui_ab_heal");
            case UpgradeType.Suture: return LoadSingle("ui_ab_lightning");
            case UpgradeType.MicroscopicHemostat: return LoadSingle("ui_ab_heal");
            case UpgradeType.SpringCoil: return LoadSingle("ui_ab_thunder_ring");
            case UpgradeType.ThrombusCatheter: return LoadSingle("ui_ab_magnet");
            case UpgradeType.SuctionTube: return LoadSingle("ui_ab_suction");
            case UpgradeType.AblationBomb: return LoadSingle("ui_ab_bomb");
        }
        return null;
    }

    public static string EnemySheet(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.CellShadow: return "enemy_slime";
            case EnemyKind.SpikeSoldier: return "enemy_bat";
            case EnemyKind.RupturedCell: return "enemy_pumpkin";
            case EnemyKind.MicroThrombus: return "enemy_eye";
            case EnemyKind.BleedingGap: return "enemy_jellyfish";
            case EnemyKind.NodeBoss: return "enemy_mega_slime";
        }
        return "enemy_slime";
    }

    public static void ApplyPlayer(GameObject player)
    {
        if (player == null) return;
        SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = player.AddComponent<SpriteRenderer>();
        Sprite[] frames = LoadFrames("wizard_walk_new");
        if (frames == null || frames.Length == 0 || frames[0] == null)
        {
            renderer.enabled = true;
            return;
        }
        renderer.sprite = frames[0]; renderer.color = Color.white; renderer.sortingOrder = 5;
        player.transform.localScale = Vector3.one * 1.42f;
        MonsterSurvivorSpriteAnimator animator = player.GetComponent<MonsterSurvivorSpriteAnimator>();
        if (animator == null) animator = player.AddComponent<MonsterSurvivorSpriteAnimator>();
        animator.SetFrames(frames, 9f);
    }

    public static void ApplyMenuLightPoint(GameObject player)
    {
        ApplyCharacterFrames(player, "menu_light_point", Vector3.one * 0.78f, 7f);
    }

    public static void ApplyCharacter(GameObject player, CharacterRole role)
    {
        string style = role == CharacterRole.Nurse ? "character_nurse" :
            (role == CharacterRole.Pharmacist ? "character_pharmacist" : "character_doctor");
        ApplyCharacterFrames(player, style, Vector3.one * 1.65f, 5f);
    }

    static void ApplyCharacterFrames(GameObject player, string style, Vector3 scale, float frameRate)
    {
        if (player == null) return;
        SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = player.AddComponent<SpriteRenderer>();
        Sprite[] frames = LoadFrames(style);
        if (frames == null || frames.Length == 0 || frames[0] == null)
        {
            renderer.enabled = true;
            return;
        }
        renderer.enabled = true;
        renderer.sprite = frames[0];
        renderer.color = Color.white;
        renderer.sortingOrder = 5;
        player.transform.localScale = scale;
        MonsterSurvivorSpriteAnimator animator = player.GetComponent<MonsterSurvivorSpriteAnimator>();
        if (animator == null) animator = player.AddComponent<MonsterSurvivorSpriteAnimator>();
        animator.SetFrames(frames, frameRate);
    }

    public static void ApplyEnemy(GameObject enemyObject, EnemyKind kind)
    {
        if (enemyObject == null) return;
        SpriteRenderer renderer = enemyObject.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = enemyObject.AddComponent<SpriteRenderer>();
        string sheet = EnemySheet(kind); Sprite[] frames = LoadFrames(sheet);
        if (frames == null || frames.Length == 0 || frames[0] == null)
        {
            // Keep the prefab's serialized sprite when runtime procedural art
            // is unavailable (for example during an editor graphics reload).
            renderer.enabled = true;
            return;
        }
        renderer.sprite = frames[0]; renderer.color = Color.white;
        renderer.sortingOrder = kind == EnemyKind.NodeBoss ? 4 : 2;
        float scale = kind == EnemyKind.NodeBoss ? 2.0f : (kind == EnemyKind.SpikeSoldier ? 1.12f : 1.18f);
        // MicroThrombus renders as an elongated clot rather than a round blob.
        enemyObject.transform.localScale = kind == EnemyKind.MicroThrombus ? new Vector3(2.3f, 0.9f, 1f) : Vector3.one * scale;
        MonsterSurvivorSpriteAnimator animator = enemyObject.GetComponent<MonsterSurvivorSpriteAnimator>();
        if (animator == null) animator = enemyObject.AddComponent<MonsterSurvivorSpriteAnimator>();
        animator.SetFrames(frames, kind == EnemyKind.NodeBoss ? 5f : 8f);
        Enemy enemy = enemyObject.GetComponent<Enemy>(); if (enemy != null) enemy.visualColor = Color.white;
    }

    public static void ApplySliced(Image image, string styleKey, Color color)
    {
        if (image == null) return;
        image.sprite = LoadSingle("ui_panel_" + styleKey); image.type = Image.Type.Sliced; image.color = color;
    }

    static Sprite MakeSprite(string key, string styleKey, int frame)
    {
        int size = styleKey.StartsWith("ui_") ? 64 : 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "OriginalSurvivorArt_" + key; texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(new Color[size * size]); DrawStyle(texture, styleKey, frame); texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = "OriginalSurvivorArt_" + key; return sprite;
    }

    static void DrawStyle(Texture2D t, string styleKey, int frame)
    {
        int c = t.width / 2; float pulse = frame % 2 == 0 ? 1f : 1.06f;
        if (styleKey == "wizard_walk_new" || styleKey == "wizard_idle")
        {
            FillCircle(t, c, c, 27f * pulse, new Color(0.03f, 0.12f, 0.2f, 1f)); FillCircle(t, c, c + 2, 21f * pulse, new Color(0.18f, 0.82f, 1f, 1f));
            FillCircle(t, c, c + 2, 10f, new Color(0.92f, 0.24f, 0.38f, 1f)); Line(t, c - 18, c - 20, c + 18, c + 20, new Color(0.92f, 0.95f, 1f, 0.95f), 3); return;
        }
        if (styleKey == "menu_light_point")
        {
            float breathe = frame % 2 == 0 ? 1f : 1.12f;
            FillCircle(t, c, c, 30f * breathe, new Color(0.12f, 0.75f, 1f, 0.18f));
            Ring(t, c, c, 22f * breathe, new Color(0.25f, 0.9f, 1f, 0.82f), 3);
            FillCircle(t, c, c, 11f * breathe, new Color(0.42f, 0.94f, 1f, 1f));
            FillCircle(t, c - 2, c + 3, 5f, new Color(0.94f, 1f, 1f, 1f));
            return;
        }
        if (styleKey == "character_doctor" || styleKey == "character_nurse" || styleKey == "character_pharmacist")
        {
            DrawRoleCharacter(t, styleKey, frame, pulse);
            return;
        }
        if (styleKey.StartsWith("enemy_")) { DrawEnemy(t, styleKey, frame, pulse); return; }
        if (styleKey.StartsWith("ui_ab_")) { DrawIcon(t, styleKey.Substring(6)); return; }
        if (styleKey.StartsWith("ui_panel_")) { DrawRoundedPanel(t, styleKey.Contains("yellow") ? new Color(1f, 0.72f, 0.2f, 1f) : Color.white); return; }
        if (styleKey.StartsWith("ui_progress")) { DrawRoundedPanel(t, Color.white); return; }
        if (styleKey.StartsWith("xp_")) { DrawExperienceGem(t, styleKey); return; }
        if (styleKey == "vfx_lightning_red" || styleKey == "vfx_lightning") { DrawLightning(t, styleKey == "vfx_lightning_red" ? new Color(1f, 0.18f, 0.25f, 1f) : new Color(0.35f, 0.9f, 1f, 1f)); return; }
        if (styleKey == "vfx_sun_spark") { FillCircle(t, c, c, 16f, new Color(1f, 0.86f, 0.28f, 1f)); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; Line(t, c, c, c + Mathf.Cos(a) * 35f, c + Mathf.Sin(a) * 35f, new Color(1f, 0.6f, 0.15f, 0.9f), 2); } }
    }

    static void DrawRoleCharacter(Texture2D t, string style, int frame, float pulse)
    {
        int c = t.width / 2;
        Color outline = new Color(0.025f, 0.09f, 0.16f, 1f);
        Color skin = new Color(1f, 0.72f, 0.56f, 1f);
        bool nurse = style == "character_nurse";
        bool pharmacist = style == "character_pharmacist";
        Color coat = nurse ? new Color(0.94f, 0.48f, 0.68f, 1f) :
            (pharmacist ? new Color(0.34f, 0.78f, 0.54f, 1f) : new Color(0.72f, 0.9f, 1f, 1f));
        float sway = frame % 2 == 0 ? -2f : 2f;

        // Full body silhouette, head and coat are deliberately distinct from
        // the small light-point used before a role has been selected.
        FillCircle(t, c, c - 12, 28f * pulse, outline);
        FillRect(t, c - 19, c - 37, 38, 32, coat);
        FillCircle(t, c - 15 + sway, c - 12, 12f, coat);
        FillCircle(t, c + 15 - sway, c - 12, 12f, coat);
        FillRect(t, c - 19, c - 38, 13, 8, new Color(0.12f, 0.3f, 0.4f, 1f));
        FillRect(t, c + 6, c - 38, 13, 8, new Color(0.12f, 0.3f, 0.4f, 1f));
        FillCircle(t, c, c + 15, 18f, skin);
        FillCircle(t, c, c + 26, 18f, outline);
        FillRect(t, c - 15, c + 18, 30, 10, skin);
        FillRect(t, c - 13, c + 14, 26, 5, new Color(0.1f, 0.15f, 0.22f, 1f));
        FillCircle(t, c - 6, c + 14, 2f, Color.white);
        FillCircle(t, c + 6, c + 14, 2f, Color.white);
        FillCircle(t, c - 6, c + 14, 1f, outline);
        FillCircle(t, c + 6, c + 14, 1f, outline);

        if (nurse)
        {
            FillRect(t, c - 17, c + 25, 34, 8, new Color(0.95f, 0.97f, 1f, 1f));
            FillRect(t, c - 3, c + 24, 6, 11, new Color(0.95f, 0.24f, 0.34f, 1f));
            FillRect(t, c - 6, c + 27, 12, 5, new Color(0.95f, 0.24f, 0.34f, 1f));
            Line(t, c + 11, c - 4, c + 16, c - 14, new Color(0.1f, 0.7f, 0.72f, 1f), 3);
        }
        else if (pharmacist)
        {
            FillRect(t, c - 4, c - 28, 8, 23, new Color(0.88f, 0.97f, 1f, 1f));
            FillRect(t, c - 7, c - 18, 14, 9, new Color(0.35f, 0.95f, 0.75f, 1f));
            FillRect(t, c - 5, c - 5, 10, 8, new Color(0.95f, 0.72f, 0.28f, 1f));
            Line(t, c - 11, c + 2, c + 11, c + 2, new Color(0.15f, 0.38f, 0.26f, 1f), 2);
        }
        else
        {
            FillRect(t, c - 3, c - 27, 6, 17, new Color(0.95f, 0.25f, 0.36f, 1f));
            FillRect(t, c - 9, c - 21, 18, 5, new Color(0.95f, 0.25f, 0.36f, 1f));
            Line(t, c - 8, c - 1, c, c - 9, new Color(0.18f, 0.36f, 0.48f, 1f), 2);
            Line(t, c + 8, c - 1, c, c - 9, new Color(0.18f, 0.36f, 0.48f, 1f), 2);
        }
    }

    static void DrawEnemy(Texture2D t, string kind, int frame, float pulse)
    {
        int c = t.width / 2;
        if (kind == "enemy_slime") { FillCircle(t, c, c, 24f * pulse, new Color(0.02f, 0.2f, 0.28f, 1f)); FillCircle(t, c, c + 2, 18f * pulse, new Color(0.2f, 0.9f, 0.9f, 1f)); FillCircle(t, c - 7, c + 4, 3f, Color.white); FillCircle(t, c + 7, c + 4, 3f, Color.white); return; }
        if (kind == "enemy_bat") { FillCircle(t, c, c, 15f * pulse, new Color(1f, 0.5f, 0.17f, 1f)); FillRect(t, c - 34, c - 5, 24, 10, new Color(0.95f, 0.24f, 0.26f, 1f)); FillRect(t, c + 10, c - 5, 24, 10, new Color(0.95f, 0.24f, 0.26f, 1f)); return; }
        if (kind == "enemy_pumpkin") { FillCircle(t, c, c, 24f * pulse, new Color(0.45f, 0.04f, 0.1f, 1f)); FillCircle(t, c, c, 18f * pulse, new Color(1f, 0.22f, 0.25f, 1f)); Line(t, c - 11, c + 6, c - 3, c - 3, Color.yellow, 3); Line(t, c + 3, c - 3, c + 11, c + 6, Color.yellow, 3); return; }
        if (kind == "enemy_eye") { FillRect(t, c - 23, c - 23, 46, 46, new Color(0.18f, 0.05f, 0.28f, 1f)); FillCircle(t, c, c, 16f * pulse, new Color(0.78f, 0.32f, 1f, 1f)); FillCircle(t, c, c, 6f, new Color(0.1f, 0.02f, 0.16f, 1f)); return; }
        if (kind == "enemy_jellyfish") { FillCircle(t, c, c + 8, 22f * pulse, new Color(1f, 0.16f, 0.66f, 1f)); Line(t, c - 12, c - 6, c - 16, c - 28, new Color(0.7f, 0.08f, 0.5f, 1f), 4); Line(t, c, c - 7, c, c - 32, new Color(0.7f, 0.08f, 0.5f, 1f), 4); Line(t, c + 12, c - 6, c + 16, c - 28, new Color(0.7f, 0.08f, 0.5f, 1f), 4); return; }
        FillCircle(t, c, c, 30f * pulse, new Color(0.03f, 0.06f, 0.18f, 1f)); FillCircle(t, c, c, 22f * pulse, new Color(0.44f, 0.25f, 0.88f, 1f)); Ring(t, c, c, 27f, new Color(1f, 0.75f, 0.2f, 1f), 3);
    }

    static void DrawIcon(Texture2D t, string icon)
    {
        int c = t.width / 2; Color cyan = new Color(0.35f, 0.9f, 1f, 1f); Color gold = new Color(1f, 0.72f, 0.2f, 1f);
        if (icon.Contains("shield"))
        {
            Ring(t, c, c, 24f, cyan, 6);
            Ring(t, c, c, 18f, new Color(0.55f, 0.95f, 1f, 0.6f), 3);
            FillCircle(t, c, c, 8f, new Color(0.08f, 0.22f, 0.36f, 1f));
        }
        else if (icon.Contains("lightning")) DrawLightning(t, gold);
        else if (icon.Contains("sword"))
        {
            Line(t, c - 22f, c - 22f, c + 22f, c + 22f, Color.white, 7);
            Line(t, c - 22f, c + 22f, c - 8f, c + 10f, gold, 5);
            Line(t, c + 8f, c - 10f, c + 22f, c - 22f, new Color(0.85f, 0.65f, 0.15f, 1f), 4);
        }
        else if (icon.Contains("heal"))
        {
            FillRect(t, c - 5, c - 24, 10, 48, new Color(0.3f, 1f, 0.55f, 1f));
            FillRect(t, c - 24, c - 5, 48, 10, new Color(0.3f, 1f, 0.55f, 1f));
            FillCircle(t, c, c, 4f, new Color(0.8f, 1f, 0.9f, 1f));
        }
        else if (icon.Contains("thunder"))
        {
            Ring(t, c, c, 24f, new Color(0.95f, 0.4f, 1f, 1f), 6);
            Ring(t, c, c, 18f, new Color(0.85f, 0.6f, 0.95f, 0.5f), 3);
        }
        else if (icon.Contains("magnet"))
        {
            Ring(t, c, c, 20f, new Color(1f, 0.35f, 0.45f, 1f), 6);
            FillRect(t, c - 3, c - 24, 6, 12, Color.white);
            FillCircle(t, c, c - 24f, 3f, new Color(1f, 0.8f, 0.3f, 1f));
        }
        else if (icon.Contains("suction"))
        {
            Ring(t, c, c, 22f, new Color(0.45f, 0.82f, 0.95f, 1f), 5);
            FillCircle(t, c, c, 10f, new Color(0.15f, 0.45f, 0.65f, 1f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + 0.3f;
                float ix = c + Mathf.Cos(a) * 20f;
                float iy = c + Mathf.Sin(a) * 20f;
                Line(t, ix, iy, c + Mathf.Cos(a) * 11f, c + Mathf.Sin(a) * 11f, new Color(0.65f, 0.95f, 1f, 0.9f), 3);
            }
        }
        else if (icon.Contains("bomb"))
        {
            FillCircle(t, c, c + 2f, 20f, new Color(0.25f, 0.12f, 0.08f, 1f));
            FillCircle(t, c, c + 2f, 16f, new Color(0.95f, 0.45f, 0.15f, 1f));
            FillCircle(t, c - 4f, c - 2f, 5f, new Color(1f, 0.85f, 0.4f, 0.6f));
            Line(t, c + 2f, c - 14f, c + 6f, c - 24f, new Color(1f, 0.82f, 0.2f, 1f), 3);
            FillCircle(t, c + 7f, c - 25f, 5f, new Color(1f, 0.92f, 0.3f, 1f));
            FillCircle(t, c + 7f, c - 25f, 3f, new Color(1f, 1f, 0.7f, 1f));
        }
        else FillCircle(t, c, c, 18f, cyan);
    }

    static void DrawExperienceGem(Texture2D t, string kind)
    {
        int c = t.width / 2;
        Color color = kind.Contains("purple") ? new Color(0.78f, 0.4f, 1f, 1f) :
            (kind.Contains("orange") ? new Color(1f, 0.7f, 0.2f, 1f) :
            (kind.Contains("green") ? new Color(0.3f, 1f, 0.55f, 1f) : new Color(0.25f, 0.9f, 1f, 1f)));
        FillCircle(t, c, c, 17f, new Color(0.04f, 0.12f, 0.2f, 1f));
        FillCircle(t, c, c + 2, 12f, color);
        Line(t, c - 7, c + 9, c, c + 16, Color.white, 2);
        Line(t, c, c + 16, c + 7, c + 9, Color.white, 2);
    }

    static void DrawRoundedPanel(Texture2D t, Color border)
    {
        int s = t.width; int radius = 10;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) { int dx = Mathf.Max(radius - x, 0, x - (s - radius - 1)); int dy = Mathf.Max(radius - y, 0, y - (s - radius - 1)); if (dx * dx + dy * dy <= radius * radius) t.SetPixel(x, y, (x < 4 || x >= s - 4 || y < 4 || y >= s - 4) ? border : Color.white); }
    }

    static void DrawLightning(Texture2D t, Color color)
    {
        int c = t.width / 2;
        Line(t, c - 14f, t.height - 14f, c + 5f, c + 6f, color, 7);
        Line(t, c + 5f, c + 6f, c - 9f, c + 6f, color, 7);
        Line(t, c - 9f, c + 6f, c + 16f, 14f, color, 7);
        Line(t, c - 10f, t.height - 10f, c + 3f, c + 8f, new Color(color.r, color.g, color.b, 0.4f), 3);
    }

    static void FillCircle(Texture2D t, float cx, float cy, float radius, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius)); int maxX = Mathf.Min(t.width - 1, Mathf.CeilToInt(cx + radius)); int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius)); int maxY = Mathf.Min(t.height - 1, Mathf.CeilToInt(cy + radius)); float r2 = radius * radius;
        for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++) if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r2) t.SetPixel(x, y, color);
    }

    static void Ring(Texture2D t, float cx, float cy, float radius, Color color, int width)
    {
        float inner = Mathf.Max(0f, radius - width); float outer = radius * radius; float inner2 = inner * inner;
        for (int y = 0; y < t.height; y++) for (int x = 0; x < t.width; x++) { float d = (x - cx) * (x - cx) + (y - cy) * (y - cy); if (d <= outer && d >= inner2) t.SetPixel(x, y, color); }
    }

    static void FillRect(Texture2D t, int x, int y, int width, int height, Color color)
    {
        for (int yy = Mathf.Max(0, y); yy < Mathf.Min(t.height, y + height); yy++) for (int xx = Mathf.Max(0, x); xx < Mathf.Min(t.width, x + width); xx++) t.SetPixel(xx, yy, color);
    }

    static void Line(Texture2D t, float x0, float y0, float x1, float y1, Color color, int width)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * 2f));
        for (int i = 0; i <= steps; i++) { float p = i / (float)steps; FillCircle(t, Mathf.Lerp(x0, x1, p), Mathf.Lerp(y0, y1, p), width * 0.5f, color); }
    }
}

