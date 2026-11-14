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
        if (frameCache.TryGetValue(assetName, out cached) && cached != null && cached.Length > 0 &&
            Array.TrueForAll(cached, sprite => sprite != null))
            return cached;
        Sprite[] sheet = LoadSheetFromResources(assetName);
        if (sheet != null) { frameCache[assetName] = sheet; return sheet; }
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

    // 美术组条带的切法。帧数/步长/锚点必须逐张写死：条带由 tools/repack_sheet.py
    // 按内容重切回填过，每姿以躯干列对齐到格子中心，所以 pivot 统一 0.5；
    // 攻击帧的刀光比走路宽，步长单独给 100，等宽切走路会切出半张空帧。
    struct SheetSpec
    {
        public readonly int frames;
        public readonly int pitch;
        public readonly float pivotX;
        public SheetSpec(int frames, int pitch, float pivotX)
        {
            this.frames = frames; this.pitch = pitch; this.pivotX = pivotX;
        }
    }

    static readonly Dictionary<string, SheetSpec> sheetSpecs = new Dictionary<string, SheetSpec>
    {
        { "character_doctor", new SheetSpec(5, 75, 0.5f) },
        { "character_doctor_attack", new SheetSpec(5, 84, 0.5f) },
    };

    // 真实导入的序列图优先于程序化占位，美术素材到位就直接顶掉生成图。
    static Sprite[] LoadSheetFromResources(string assetName)
    {
        Texture2D tex = Resources.Load<Texture2D>(assetName);
        if (tex == null && assetName == "enemy_cell_shadow") tex = DecodeEmbeddedCellShadow();
        if (tex == null) return null;
        SheetSpec spec;
        bool hasSpec = sheetSpecs.TryGetValue(assetName, out spec);
        int frames = hasSpec ? spec.frames : 4;
        int pitch = hasSpec ? spec.pitch : tex.width / frames;
        float pivotX = hasSpec ? spec.pivotX : 0.5f;
        if (pitch <= 0 || frames * pitch > tex.width)
        {
            // 以前这里静默退回程序化占位图，素材明明在包里却"形象没变"，查不到原因。
            Debug.LogWarning("序列图 " + assetName + " 是 " + tex.width + "x" + tex.height +
                "，装不下 " + frames + " 帧 x " + pitch + "px，改用程序化占位图。");
            return null;
        }
        int fh = tex.height;
        if (fh <= 0) return null;
        float ppu = Mathf.Max(1f, fh * 1.5f);
        Sprite[] result = new Sprite[frames];
        for (int i = 0; i < frames; i++)
        {
            result[i] = Sprite.Create(tex, new Rect(i * pitch, 0, pitch, fh), new Vector2(pivotX, 0.5f), ppu);
            result[i].name = assetName + "_frame_" + i;
        }
        return result;
    }

    static Texture2D embeddedCellShadow;

    // The sheet is also embedded so the art shows even before Unity imports the
    // Resources PNG (asset import is suppressed while the Editor is in Play Mode).
    static Texture2D DecodeEmbeddedCellShadow()
    {
        if (embeddedCellShadow != null) return embeddedCellShadow;
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.name = "enemy_cell_shadow";
        if (!tex.LoadImage(System.Convert.FromBase64String(CellShadowSheetBase64))) return null;
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        embeddedCellShadow = tex;
        return tex;
    }

    const string CellShadowSheetBase64 = "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";

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
            // 进化技能图标
            case UpgradeType.ContinuousFlush: return LoadSingle("ui_ab_lightning");
            case UpgradeType.UltrasonicScissors: return LoadSingle("ui_ab_sword");
            case UpgradeType.BloodRecoveryForceps: return LoadSingle("ui_ab_heal");
            case UpgradeType.StapleGun: return LoadSingle("ui_ab_lightning");
            case UpgradeType.EmbolismHemostatNet: return LoadSingle("ui_ab_magnet");
            case UpgradeType.VascularSpasmRing: return LoadSingle("ui_ab_thunder_ring");
            case UpgradeType.ExtracorporealPump: return LoadSingle("ui_ab_magnet");
            case UpgradeType.DirectedAblationRain: return LoadSingle("ui_ab_bomb");
            case UpgradeType.HighFrequencyElectroknife: return LoadSingle("ui_ab_sword");
            case UpgradeType.MicroInfusionPump: return LoadSingle("ui_ab_lightning");
            case UpgradeType.AtomizationDrugMachine: return LoadSingle("ui_ab_bomb");
        }
        return null;
    }

    public static string EnemySheet(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.CellShadow: return "enemy_cell_shadow";
            case EnemyKind.SpikeSoldier: return "enemy_bat";
            case EnemyKind.RupturedCell: return "enemy_pumpkin";
            case EnemyKind.MicroThrombus: return "enemy_eye";
            case EnemyKind.BleedingGap: return "enemy_jellyfish";
            case EnemyKind.VenousStasis: return "enemy_slime";
            case EnemyKind.FibrinNet: return "enemy_jellyfish";
            case EnemyKind.PrimitiveCell: return "enemy_pumpkin";
            case EnemyKind.TargetBlocker: return "enemy_eye";
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
        ApplyCharacterFrames(player, "menu_light_point", 0.78f, 7f);
    }

    public static void ApplyCharacter(GameObject player, CharacterRole role)
    {
        string style = role == CharacterRole.Nurse ? "character_nurse" :
            (role == CharacterRole.Pharmacist ? "character_pharmacist" : "character_doctor");
        ApplyCharacterFrames(player, style, 1.65f, 5f);
        // 攻击条带目前只有医生出了素材，其余角色 Play("attack") 会静默跳过。
        if (role != CharacterRole.Doctor || player == null) return;
        MonsterSurvivorSpriteAnimator animator = player.GetComponent<MonsterSurvivorSpriteAnimator>();
        if (animator == null) return;
        animator.SetClip("attack", LoadFrames("character_doctor_attack"), 12f, false);
    }

    // worldHeight 是整张帧在世界上占多高，不是缩放倍数：
    // 美术组的条带是 64px 高、程序化占位是 96px 高，用固定倍数缩放会让真素材
    // 一换上去就小一圈。按帧高归一以后，两种来源的视觉尺寸一致。
    static void ApplyCharacterFrames(GameObject player, string style, float worldHeight, float frameRate)
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
        float bounds = frames[0].bounds.size.y;
        player.transform.localScale = Vector3.one *
            (bounds > 0.0001f ? worldHeight / bounds : worldHeight);
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

    // 升级卡的细发光边框：2px 亮线 + 3px 内衰减，四角保留圆角，中间由 9-slice 拉伸。
    // 只在贴边几个像素里有不透明内容，因此整张卡靠它提轮廓，而不是靠一大块底色。
    public static Sprite CardFrame()
    {
        Sprite cached;
        if (singleCache.TryGetValue("card_frame", out cached) && cached != null) return cached;
        const int size = 128;
        // 与 DrawRoundedPanel 的 s/7 圆角一致，发光线才能压在玻璃底色的那圈暗边上。
        const float radius = 18f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "OriginalSurvivorArt_card_frame";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(new Color[size * size]);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float edge = RoundedRectEdge(x, y, size, radius);
                if (edge < 0f) continue;
                if (edge < 2f) texture.SetPixel(x, y, new Color(1f, 1f, 1f, 0.85f));
                else if (edge < 6f) texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Lerp(0.42f, 0.05f, (edge - 2f) / 4f)));
            }
        }
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0,
            SpriteMeshType.FullRect, new Vector4(26f, 26f, 26f, 26f));
        cached.name = "OriginalSurvivorArt_card_frame";
        singleCache["card_frame"] = cached;
        return cached;
    }

    // 图标背后的柔光：中心到边缘的平方衰减，配合低 alpha 当"发光底"，替代硬色块底框。
    public static Sprite GlowRadial()
    {
        Sprite cached;
        if (singleCache.TryGetValue("glow_radial", out cached) && cached != null) return cached;
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "OriginalSurvivorArt_glow_radial";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(new Color[size * size]);
        float center = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                falloff *= falloff;
                if (falloff <= 0.001f) continue;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, falloff));
            }
        }
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        cached.name = "OriginalSurvivorArt_glow_radial";
        singleCache["glow_radial"] = cached;
        return cached;
    }

    // 圆角矩形内的到边距离：0 表示正好在轮廓上，负数表示在轮廓之外。
    static float RoundedRectEdge(float x, float y, float size, float radius)
    {
        float qx = Mathf.Max(radius - x, x - (size - 1f - radius), 0f);
        float qy = Mathf.Max(radius - y, y - (size - 1f - radius), 0f);
        return radius - Mathf.Sqrt(qx * qx + qy * qy);
    }

    // 明日方舟风：水平扫描线，每 4 像素一条，极淡，叠加在卡体上增加科技感。
    public static Sprite ScanLines()
    {
        Sprite cached;
        if (singleCache.TryGetValue("scan_lines", out cached) && cached != null) return cached;
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "OriginalSurvivorArt_scan_lines";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Repeat;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 每 4 像素一条线，alpha 0.04
                pixels[y * size + x] = (y % 4 == 0) ? new Color(1f, 1f, 1f, 0.04f) : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        cached.name = "OriginalSurvivorArt_scan_lines";
        singleCache["scan_lines"] = cached;
        return cached;
    }

    // 明日方舟风：斜切角矩形框，四角 45° 切角而非圆角，锐利边缘。
    public static Sprite DiagonalFrame()
    {
        Sprite cached;
        if (singleCache.TryGetValue("diagonal_frame", out cached) && cached != null) return cached;
        const int size = 128;
        const float chamfer = 16f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "OriginalSurvivorArt_diagonal_frame";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] clear = new Color[size * size];
        texture.SetPixels(clear);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 四角切角区域：切掉的三角形
                bool cutTopLeft = x < chamfer && y < chamfer && (x + y) < chamfer;
                bool cutTopRight = x > (size - chamfer - 1) && y < chamfer && ((size - 1 - x) + y) < chamfer;
                bool cutBottomLeft = x < chamfer && y > (size - chamfer - 1) && (x + (size - 1 - y)) < chamfer;
                bool cutBottomRight = x > (size - chamfer - 1) && y > (size - chamfer - 1) && ((size - 1 - x) + (size - 1 - y)) < chamfer;
                if (cutTopLeft || cutTopRight || cutBottomLeft || cutBottomRight) continue;
                // 2px 亮边
                bool onEdge = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                if (onEdge) { texture.SetPixel(x, y, new Color(1f, 1f, 1f, 0.85f)); continue; }
                // 内侧 3px 衰减
                float edgeDist = Mathf.Min(x, y, size - 1 - x, size - 1 - y);
                if (edgeDist < 5f) texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Lerp(0.45f, 0.08f, (edgeDist - 2f) / 3f)));
            }
        }
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0,
            SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
        cached.name = "OriginalSurvivorArt_diagonal_frame";
        singleCache["diagonal_frame"] = cached;
        return cached;
    }

    // 明日方舟风：角标括号，32x32，L 形，用于四角装饰。
    public static Sprite CornerBracket()
    {
        Sprite cached;
        if (singleCache.TryGetValue("corner_bracket", out cached) && cached != null) return cached;
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "OriginalSurvivorArt_corner_bracket";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] clear = new Color[size * size];
        texture.SetPixels(clear);
        // 左上角 L 形：左边 2px 宽，顶边 2px 高，长度 12px
        for (int i = 0; i < 12; i++)
        {
            texture.SetPixel(1, 1 + i, Color.white);
            texture.SetPixel(2, 1 + i, Color.white);
            texture.SetPixel(1 + i, 1, Color.white);
            texture.SetPixel(1 + i, 2, Color.white);
        }
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0f, 1f), size);
        cached.name = "OriginalSurvivorArt_corner_bracket";
        singleCache["corner_bracket"] = cached;
        return cached;
    }

    // 明日方舟风：技术刻度线，128x8，带刻度标记。
    public static Sprite TechScale()
    {
        Sprite cached;
        if (singleCache.TryGetValue("tech_scale", out cached) && cached != null) return cached;
        const int w = 128, h = 8;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.name = "OriginalSurvivorArt_tech_scale";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] clear = new Color[w * h];
        texture.SetPixels(clear);
        // 基线
        for (int x = 0; x < w; x++) texture.SetPixel(x, 3, new Color(1f, 1f, 1f, 0.6f));
        // 刻度：每 16px 一个长刻度，每 8px 一个短刻度
        for (int x = 0; x < w; x += 8)
        {
            int tickH = (x % 16 == 0) ? 6 : 4;
            for (int y = 4; y < 4 + tickH && y < h; y++) texture.SetPixel(x, y, new Color(1f, 1f, 1f, 0.8f));
        }
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), h);
        cached.name = "OriginalSurvivorArt_tech_scale";
        singleCache["tech_scale"] = cached;
        return cached;
    }

    // 脚下接触阴影：椭圆径向衰减，纯白靠 SpriteRenderer.color 染成半透明黑。
    // 侧视像素角色没有体积感，贴一块软阴影把它"按"在地面上，比悬空自然。
    public static Sprite GroundShadow()
    {
        Sprite cached;
        if (singleCache.TryGetValue("ground_shadow", out cached) && cached != null) return cached;
        const int w = 64, h = 32;
        Texture2D texture = NewMaskTexture("OriginalSurvivorArt_ground_shadow", w, h, FilterMode.Bilinear);
        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x - w * 0.5f) / (w * 0.5f);
                float dy = (y - h * 0.5f) / (h * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                pixels[y * w + x] = new Color(1f, 1f, 1f, a * a);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 32f);
        cached.name = "OriginalSurvivorArt_ground_shadow";
        singleCache["ground_shadow"] = cached;
        return cached;
    }

    // 朝向罗盘弧：以 +X 为中心的一段圆环扇区，纯白靠 color 染单青。
    // 环半径取得比角色半身高略大，绕角色中心旋转时任何朝向都不会被身体挡住，
    // 读作贴在地面上、指向当前朝向的一抹柔光扫弧。
    public static Sprite HeadingSweep()
    {
        Sprite cached;
        if (singleCache.TryGetValue("heading_sweep", out cached) && cached != null) return cached;
        const int size = 120;
        const float center = size * 0.5f;
        const float innerR = 48f, outerR = 59f;
        const float halfSpan = 35f * Mathf.Deg2Rad;
        Texture2D texture = NewMaskTexture("OriginalSurvivorArt_heading_sweep", size, size, FilterMode.Bilinear);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center, dy = y - center;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r < innerR - 2f || r > outerR + 2f) continue;
                float ang = Mathf.Atan2(dy, dx);
                if (Mathf.Abs(ang) > halfSpan + 0.12f) continue;
                float radial = Mathf.Clamp01((r - (innerR - 2f)) / 3f) * Mathf.Clamp01(((outerR + 2f) - r) / 3f);
                float angular = Mathf.Clamp01((halfSpan + 0.12f - Mathf.Abs(ang)) / 0.12f);
                float a = radial * angular;
                if (a > 0f) pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 54f);
        cached.name = "OriginalSurvivorArt_heading_sweep";
        singleCache["heading_sweep"] = cached;
        return cached;
    }

    // 磨砂玻璃噪点：128x128 随机灰度，alpha 只有 0.055。
    // 之前给到 0.16，叠在深色卡体上直接变成电视雪花，文字全糊掉了——磨砂要"几乎看不见"才像玻璃。
    public static Sprite FrostedNoise()
    {
        Sprite cached;
        if (singleCache.TryGetValue("frosted_noise", out cached) && cached != null) return cached;
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "OriginalSurvivorArt_frosted_noise";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Repeat;
        Color[] pixels = new Color[size * size];
        System.Random rng = new System.Random(42); // 固定种子，保证每次一样
        for (int i = 0; i < pixels.Length; i++)
        {
            float v = (float)rng.NextDouble();
            pixels[i] = new Color(v, v, v, 0.055f);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        cached.name = "OriginalSurvivorArt_frosted_noise";
        singleCache["frosted_noise"] = cached;
        return cached;
    }

    // 玻璃受光：上亮下暗的竖向渐变。之前是中心径向高光，叠在深色卡上等于在文字区打了一束手电。
    public static Sprite SubtleGradient()
    {
        Sprite cached;
        if (singleCache.TryGetValue("subtle_gradient", out cached) && cached != null) return cached;
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "OriginalSurvivorArt_subtle_gradient";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            // alpha 必须是半透明：写死 1 会把卡体颜色整个盖掉，改底色完全看不出效果。
            float t = y / (size - 1f);
            float brightness = Mathf.Lerp(0.35f, 0.90f, t);
            for (int x = 0; x < size; x++) pixels[y * size + x] = new Color(brightness, brightness, brightness, 0.12f);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        cached.name = "OriginalSurvivorArt_subtle_gradient";
        singleCache["subtle_gradient"] = cached;
        return cached;
    }

    // 进度条填充：上缘一道高光 + 自上而下的明度衰减，替代原来那块纯平涂的圆角条。
    // 灰度值会和 Image.color 相乘，所以这里只画亮度关系，颜色由调用方给。
    public static Sprite BarFill()
    {
        Sprite cached;
        if (singleCache.TryGetValue("bar_fill", out cached) && cached != null) return cached;
        const int w = 8, h = 64;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.name = "OriginalSurvivorArt_bar_fill";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            // y 从底往上；顶部 3 像素是高光带，下面线性压暗到 0.62
            float v = y >= h - 3 ? 1f : Mathf.Lerp(0.62f, 0.90f, y / (h - 4f));
            for (int x = 0; x < w; x++) pixels[y * w + x] = new Color(v, v, v, 1f);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 64f);
        cached.name = "OriginalSurvivorArt_bar_fill";
        singleCache["bar_fill"] = cached;
        return cached;
    }

    // ============ 粗野主义构件 ============
    // 全部按目标矩形 1:1 生成像素，并用 Image.Type.Simple 铺满：
    // 之前 9-slice 的 DiagonalFrame 在 300x486 的卡上会被拉成 47x76 的斜角，切角根本不是 45°。

    // 斜切角矩形的内/外判定：inset 往里缩，切角线 x+y=chamfer 也随之内移 sqrt(2)*inset。
    static bool InsideChamfer(float x, float y, int w, int h, float chamfer, float inset)
    {
        float lo = inset;
        float hiX = w - 1f - inset;
        float hiY = h - 1f - inset;
        if (x < lo || y < lo || x > hiX || y > hiY) return false;
        float cut = chamfer - inset * 1.41421356f;
        if (cut <= 0f) return true;
        if (x < lo + cut && y < lo + cut && (x - lo) + (y - lo) < cut) return false;
        if (x > hiX - cut && y < lo + cut && (hiX - x) + (y - lo) < cut) return false;
        if (x < lo + cut && y > hiY - cut && (x - lo) + (hiY - y) < cut) return false;
        if (x > hiX - cut && y > hiY - cut && (hiX - x) + (hiY - y) < cut) return false;
        return true;
    }

    // 卡体：实心切角面板，画成白色由 Image.color 染色。
    public static Sprite ChamferFill(int width, int height, float chamfer)
    {
        width = Mathf.Max(4, width); height = Mathf.Max(4, height);
        string key = "chamfer_fill_" + width + "x" + height + "_" + Mathf.RoundToInt(chamfer);
        Sprite cached;
        if (singleCache.TryGetValue(key, out cached) && cached != null) return cached;
        Texture2D texture = NewMaskTexture(key, width, height, FilterMode.Point);
        Color[] pixels = new Color[width * height];
        Color fill = Color.white;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (InsideChamfer(x, y, width, height, chamfer, 0f)) pixels[y * width + x] = fill;
        texture.SetPixels(pixels);
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 1f);
        cached.name = key;
        singleCache[key] = cached;
        return cached;
    }

    // 边框：只有轮廓那 thickness 像素有不透明内容，硬边无衰减。
    public static Sprite ChamferLine(int width, int height, float chamfer, float thickness)
    {
        width = Mathf.Max(4, width); height = Mathf.Max(4, height);
        string key = "chamfer_line_" + width + "x" + height + "_" + Mathf.RoundToInt(chamfer) + "_" + Mathf.RoundToInt(thickness * 10f);
        Sprite cached;
        if (singleCache.TryGetValue(key, out cached) && cached != null) return cached;
        Texture2D texture = NewMaskTexture(key, width, height, FilterMode.Point);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!InsideChamfer(x, y, width, height, chamfer, 0f)) continue;
                if (InsideChamfer(x, y, width, height, chamfer, thickness)) continue;
                pixels[y * width + x] = Color.white;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 1f);
        cached.name = key;
        singleCache[key] = cached;
        return cached;
    }

    // 几何网格：32px 一格、1px 硬线。ppu=1 让 Image.Type.Tiled 一格正好铺 32 像素。
    public static Sprite GridField()
    {
        Sprite cached;
        if (singleCache.TryGetValue("grid_field", out cached) && cached != null) return cached;
        const int size = 32;
        Texture2D texture = NewMaskTexture("OriginalSurvivorArt_grid_field", size, size, FilterMode.Point);
        Color[] pixels = new Color[size * size];
        Color line = Color.white;
        for (int i = 0; i < size; i++)
        {
            pixels[i] = line;          // 横线只画下边，平铺后正好每 32px 一条
            pixels[i * size] = line;   // 竖线只画左边
        }
        texture.SetPixels(pixels);
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 1f);
        cached.name = "OriginalSurvivorArt_grid_field";
        singleCache["grid_field"] = cached;
        return cached;
    }

    // 警示斜纹：16px 一个周期、45°、条纹占 6/16。颜色由调用方给，方便和强调色统一。
    public static Sprite HazardStripes()
    {
        Sprite cached;
        if (singleCache.TryGetValue("hazard_stripes", out cached) && cached != null) return cached;
        const int size = 16;
        Texture2D texture = NewMaskTexture("OriginalSurvivorArt_hazard_stripes", size, size, FilterMode.Point);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = ((x + y) % size) < 6 ? Color.white : Color.clear;
        texture.SetPixels(pixels);
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 1f);
        cached.name = "OriginalSurvivorArt_hazard_stripes";
        singleCache["hazard_stripes"] = cached;
        return cached;
    }

    // 技术条码：同一个 seed 永远同一串条纹，当技能编号的视觉指纹用。
    public static Sprite Barcode(int seed)
    {
        string key = "barcode_" + seed;
        Sprite cached;
        if (singleCache.TryGetValue(key, out cached) && cached != null) return cached;
        const int width = 96, height = 14;
        Texture2D texture = NewMaskTexture("OriginalSurvivorArt_" + key, width, height, FilterMode.Point);
        Color[] pixels = new Color[width * height];
        System.Random rng = new System.Random(seed == 0 ? 7 : seed);
        int x = 0;
        bool ink = true;
        while (x < width)
        {
            int run = ink ? 1 + rng.Next(3) : 1 + rng.Next(2);
            for (int b = 0; b < run && x < width; b++, x++)
            {
                if (!ink) continue;
                for (int y = 0; y < height; y++) pixels[y * width + x] = Color.white;
            }
            ink = !ink;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 1f);
        cached.name = "OriginalSurvivorArt_" + key;
        singleCache[key] = cached;
        return cached;
    }

    static Texture2D NewMaskTexture(string name, int width, int height, FilterMode filter)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.filterMode = filter;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    static Sprite MakeSprite(string key, string styleKey, int frame)
    {
        int size = styleKey.StartsWith("ui_") ? 128 : 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "OriginalSurvivorArt_" + key; texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(new Color[size * size]); DrawStyle(texture, styleKey, frame); texture.Apply();
        // 面板/进度条使用 9-slice：把圆角与描边锁在四角与四边，中间纯色区拉伸，避免圆角被拉扁。
        Vector4 border = (styleKey.StartsWith("ui_panel") || styleKey.StartsWith("ui_progress"))
            ? new Vector4(size * 0.20f, size * 0.20f, size * 0.20f, size * 0.20f) : Vector4.zero;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect, border);
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
        int c = t.width / 2; float s = t.width / 64f; // Scale factor for 128px textures
        Color cyan = new Color(0.35f, 0.9f, 1f, 1f); Color gold = new Color(1f, 0.72f, 0.2f, 1f);
        if (icon.Contains("shield"))
        {
            Ring(t, c, c, 24f * s, cyan, (int)(6 * s));
            Ring(t, c, c, 18f * s, new Color(0.55f, 0.95f, 1f, 0.6f), (int)(3 * s));
            FillCircle(t, c, c, 8f * s, new Color(0.08f, 0.22f, 0.36f, 1f));
        }
        else if (icon.Contains("lightning")) DrawLightning(t, gold);
        else if (icon.Contains("sword"))
        {
            Line(t, c - 22f * s, c - 22f * s, c + 22f * s, c + 22f * s, Color.white, (int)(7 * s));
            Line(t, c - 22f * s, c + 22f * s, c - 8f * s, c + 10f * s, gold, (int)(5 * s));
            Line(t, c + 8f * s, c - 10f * s, c + 22f * s, c - 22f * s, new Color(0.85f, 0.65f, 0.15f, 1f), (int)(4 * s));
        }
        else if (icon.Contains("heal"))
        {
            FillRect(t, c - (int)(5 * s), c - (int)(24 * s), (int)(10 * s), (int)(48 * s), new Color(0.3f, 1f, 0.55f, 1f));
            FillRect(t, c - (int)(24 * s), c - (int)(5 * s), (int)(48 * s), (int)(10 * s), new Color(0.3f, 1f, 0.55f, 1f));
            FillCircle(t, c, c, 4f * s, new Color(0.8f, 1f, 0.9f, 1f));
        }
        else if (icon.Contains("thunder"))
        {
            Ring(t, c, c, 24f * s, new Color(0.95f, 0.4f, 1f, 1f), (int)(6 * s));
            Ring(t, c, c, 18f * s, new Color(0.85f, 0.6f, 0.95f, 0.5f), (int)(3 * s));
        }
        else if (icon.Contains("magnet"))
        {
            Ring(t, c, c, 20f * s, new Color(1f, 0.35f, 0.45f, 1f), (int)(6 * s));
            FillRect(t, c - (int)(3 * s), c - (int)(24 * s), (int)(6 * s), (int)(12 * s), Color.white);
            FillCircle(t, c, c - 24f * s, 3f * s, new Color(1f, 0.8f, 0.3f, 1f));
        }
        else if (icon.Contains("suction"))
        {
            Ring(t, c, c, 22f * s, new Color(0.45f, 0.82f, 0.95f, 1f), (int)(5 * s));
            FillCircle(t, c, c, 10f * s, new Color(0.15f, 0.45f, 0.65f, 1f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + 0.3f;
                float ix = c + Mathf.Cos(a) * 20f * s;
                float iy = c + Mathf.Sin(a) * 20f * s;
                Line(t, ix, iy, c + Mathf.Cos(a) * 11f * s, c + Mathf.Sin(a) * 11f * s, new Color(0.65f, 0.95f, 1f, 0.9f), (int)(3 * s));
            }
        }
        else if (icon.Contains("bomb"))
        {
            FillCircle(t, c, c + 2f * s, 20f * s, new Color(0.25f, 0.12f, 0.08f, 1f));
            FillCircle(t, c, c + 2f * s, 16f * s, new Color(0.95f, 0.45f, 0.15f, 1f));
            FillCircle(t, c - 4f * s, c - 2f * s, 5f * s, new Color(1f, 0.85f, 0.4f, 0.6f));
            Line(t, c + 2f * s, c - 14f * s, c + 6f * s, c - 24f * s, new Color(1f, 0.82f, 0.2f, 1f), (int)(3 * s));
            FillCircle(t, c + 7f * s, c - 25f * s, 5f * s, new Color(1f, 0.92f, 0.3f, 1f));
            FillCircle(t, c + 7f * s, c - 25f * s, 3f * s, new Color(1f, 1f, 0.7f, 1f));
        }
        else FillCircle(t, c, c, 18f * s, cyan);
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
        int s = t.width;
        int radius = Mathf.Max(14, s / 7);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                int dx = Mathf.Max(radius - x, 0, x - (s - radius - 1));
                int dy = Mathf.Max(radius - y, 0, y - (s - radius - 1));
                if (dx * dx + dy * dy > radius * radius) continue; // 圆角之外保持透明
                // 深色内描边：贴边 3px，给面板一圈收口，避免"一块纯色漂浮"。
                bool onEdge = dx > radius - 3 || dy > radius - 3 || x < 3 || y < 3 || x >= s - 3 || y >= s - 3;
                if (onEdge)
                {
                    t.SetPixel(x, y, new Color(border.r * 0.30f, border.g * 0.30f, border.b * 0.30f, 1f));
                    continue;
                }
                // 竖向受光渐变：上亮下暗，让面板有轻微立体感而非均匀死色。
                float v = (float)y / (s - 1);
                float shade = Mathf.Lerp(0.80f, 1.04f, v);
                t.SetPixel(x, y, new Color(shade, shade, shade, 1f));
            }
        }
    }

    static void DrawLightning(Texture2D t, Color color)
    {
        int c = t.width / 2; float s = t.width / 64f;
        Line(t, c - 14f * s, t.height - 14f * s, c + 5f * s, c + 6f * s, color, (int)(7 * s));
        Line(t, c + 5f * s, c + 6f * s, c - 9f * s, c + 6f * s, color, (int)(7 * s));
        Line(t, c - 9f * s, c + 6f * s, c + 16f * s, 14f * s, color, (int)(7 * s));
        Line(t, c - 10f * s, t.height - 10f * s, c + 3f * s, c + 8f * s, new Color(color.r, color.g, color.b, 0.4f), (int)(3 * s));
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

