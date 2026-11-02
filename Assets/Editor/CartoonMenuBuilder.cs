using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebuilds MainMenu.unity with the bright cartoon skin built from the Kenney
/// UI Pack (buttons/icons) plus generated card and background textures.
/// </summary>
public class CartoonMenuBuilder
{
    const string MenuPath = "Assets/Scenes/MainMenu.unity";
    const string GamePath = "Assets/Scenes/SampleScene.unity";
    const string ArtDir = "Assets/Art/UI/";
    const string PreviewPath = "_analysis/menu_cartoon_preview.png";

    // Palette taken from the Figma design: dark teal board, white card, yellow CTA.
    static Color Hex(int rgb)
    {
        return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }

    static readonly Color Ink = Hex(0x14435C);
    static readonly Color Sky = Hex(0x36BDF7);
    static readonly Color Soft = Hex(0x5E8296);
    static readonly Color Faint = Hex(0x8FB3C6);
    static readonly Color CardWhite = Hex(0xFFFFFF);

    static Font cnFont;
    static Font latinFont;

    [MenuItem("VesselGuard/重建主菜单（卡通版）", false, 1)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请先退出播放模式，再重建主菜单。");
            return;
        }

        PrepareAssets();
        PrepareFont();
        cnFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/simhei.ttf");
        latinFont = AssetDatabase.LoadAssetAtPath<Font>(ArtDir + "Kenney/KenneyFuture.ttf");
        if (cnFont == null) Debug.LogError("CartoonMenu: 找不到中文显示字体 simhei.ttf");

        Scene current = SceneManager.GetActiveScene();
        string currentPath = current.path;
        if (current.isDirty && !string.IsNullOrEmpty(currentPath)) EditorSceneManager.SaveScene(current);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "MainMenu";

        CreateCamera();
        Canvas canvas = CreateCanvas();
        BuildContent(canvas);
        WireController(canvas);

        EditorSceneManager.SaveScene(scene, MenuPath);
        MainMenuBuilder.ConfigureBuildSettings(MenuPath, GamePath);
        AssetDatabase.SaveAssets();
        Debug.Log("VesselGuard: 卡通主菜单已重建 -> " + MenuPath);
    }

    // ---------------------------------------------------------------- assets

    static void PrepareAssets()
    {
        AssetDatabase.Refresh();

        // Kenney buttons are 576x192 after the 3x upscale; 36 keeps the rounded
        // corner and the bottom shadow inside the border when nine-sliced.
        foreach (string tone in new[] { "green", "blue", "red", "grey" })
        {
            SetSprite(ArtDir + "Kenney/btn_" + tone + ".png", new Vector4(36, 36, 36, 36), 256);
            SetSprite(ArtDir + "Kenney/btn_" + tone + "_hover.png", new Vector4(36, 36, 36, 36), 256);
            SetSprite(ArtDir + "Kenney/btn_" + tone + "_pressed.png", new Vector4(36, 36, 36, 36), 256);
        }
        SetSprite(ArtDir + "Kenney/icon_play.png", Vector4.zero, 256);
        SetSprite(ArtDir + "Kenney/btn_line.png", new Vector4(36, 36, 36, 36), 256);
        SetSprite(ArtDir + "Kenney/btn_line_pressed.png", new Vector4(36, 36, 36, 36), 256);
        SetSprite(ArtDir + "Generated/panel_white.png", new Vector4(96, 96, 96, 96), 1024);
        SetSprite(ArtDir + "Generated/bg_teal.png", Vector4.zero, 1024);
        SetSprite(ArtDir + "Generated/btn_round_26.png", new Vector4(32, 32, 32, 32), 256);
        SetSprite(ArtDir + "Generated/btn_round_20.png", new Vector4(26, 26, 26, 26), 256);
        SetSprite(ArtDir + "Generated/btn_round_16.png", new Vector4(24, 24, 24, 24), 256);

        // Runtime popups load the same shapes from Resources, so the level / character
        // panels stay in sync with this builder instead of drifting to flat boxes.
        SetSprite("Assets/Resources/UI/panel_white.png", new Vector4(96, 96, 96, 96), 1024);
        SetSprite("Assets/Resources/UI/btn_round_26.png", new Vector4(32, 32, 32, 32), 256);
        SetSprite("Assets/Resources/UI/btn_round_20.png", new Vector4(26, 26, 26, 26), 256);
        SetSprite("Assets/Resources/UI/btn_round_16.png", new Vector4(24, 24, 24, 24), 256);
        SetSprite(ArtDir + "Generated/panel_shadow.png", new Vector4(96, 96, 96, 96), 1024);
        SetSprite(ArtDir + "Generated/bg_sky.png", Vector4.zero, 1024);
    }

    // The project ships simhei baked at 16 px (Unicode atlas), so anything larger
    // than body text was a magnified bitmap. Dynamic keeps large titles crisp.
    static void PrepareFont()
    {
        TrueTypeFontImporter importer = AssetImporter.GetAtPath("Assets/Resources/simhei.ttf") as TrueTypeFontImporter;
        if (importer == null) return;
        if (importer.fontTextureCase == FontTextureCase.Dynamic) return;
        importer.fontTextureCase = FontTextureCase.Dynamic;
        importer.SaveAndReimport();
        Debug.Log("CartoonMenu: simhei 已切换为动态字体（原先为位图烘焙，大字号会发糊）。");
    }

    static void SetSprite(string path, Vector4 border, int maxSize)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (importer == null)
        {
            Debug.LogError("CartoonMenu: 无法导入 " + path);
            return;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = border;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    static Sprite Sprite(string relative)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + relative + ".png");
        if (sprite == null) Debug.LogError("CartoonMenu: 缺少贴图 " + relative);
        return sprite;
    }

    // ---------------------------------------------------------------- scene

    static Camera CreateCamera()
    {
        GameObject go = new GameObject("MainMenu Camera");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 0f, -10f);
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.4f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.35f, 0.72f, 0.92f, 1f);
        return cam;
    }

    static Canvas CreateCanvas()
    {
        GameObject go = new GameObject("Canvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        go.AddComponent<GraphicRaycaster>();

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
        return canvas;
    }

    static void BuildContent(Canvas canvas)
    {
        Image background = NewImage(canvas.transform, "Background", Sprite("Generated/bg_teal"), Vector2.zero);
        Stretch(background.rectTransform);
        background.raycastTarget = false;

        // The card sits 16 px below screen centre in the Figma file while the text
        // and buttons keep their screen position, so every child offset is +16.
        // The design has no drop shadow: the card edge is a clean white cut.
        Image card = NewImage(canvas.transform, "Card", Sprite("Generated/panel_white"), new Vector2(904f, 764f));
        card.rectTransform.anchoredPosition = new Vector2(0f, -16f);
        card.raycastTarget = false;

        NewText(card.transform, "GameTitle", "血管守卫", 100, Ink, cnFont,
            new Vector2(0f, 284f), new Vector2(820f, 130f));

        NewText(card.transform, "GameTitleEn", "V E S S E L G U A R D", 32, Sky, latinFont,
            new Vector2(0f, 198f), new Vector2(820f, 50f));

        NewText(card.transform, "GameDescription", "守护血管健康，击退风险细胞", 26, Soft, cnFont,
            new Vector2(0f, 148f), new Vector2(820f, 44f));

        MakeButton(card.transform, "StartButton", "开始游戏", Hex(0xFFCC00), Hex(0xFF8D28), 26,
            new Vector2(0f, 38f), new Vector2(500f, 104f), 46, CardWhite);

        // Runtime MainMenuUI reuses these names; keeping them in the scene stops
        // it from spawning its own centre-anchored copies on top of the layout.
        MakeButton(card.transform, "LevelSelectButton", "选择关卡", Hex(0x36BDF7), Hex(0x146587), 20,
            new Vector2(-260f, -88f), new Vector2(240f, 76f), 28, CardWhite);
        MakeButton(card.transform, "CharacterSelectButton", "选择角色", Hex(0x36BDF7), Hex(0x146587), 20,
            new Vector2(0f, -88f), new Vector2(240f, 76f), 28, CardWhite);
        MakeButton(card.transform, "CodexButton", "血管图鉴", Hex(0x36BDF7), Hex(0x146587), 20,
            new Vector2(260f, -88f), new Vector2(240f, 76f), 28, CardWhite);

        MakeButton(card.transform, "SettingsButton", "设置", Hex(0xB9BCCB), Hex(0x666880), 16,
            new Vector2(-212f, -186f), new Vector2(188f, 60f), 24, Ink);
        MakeButton(card.transform, "AchievementsButton", "成就", Hex(0xAC7F5E), Hex(0x7A5238), 16,
            new Vector2(0f, -186f), new Vector2(188f, 60f), 24, Ink);
        MakeButton(card.transform, "QuitButton", "退出", Hex(0xFF627B), Hex(0x871023), 16,
            new Vector2(212f, -186f), new Vector2(188f, 60f), 24, CardWhite);

        NewText(card.transform, "StatusText", "版本 0.1  ·  血管健康教育项目", 18, Faint, cnFont,
            new Vector2(0f, -292f), new Vector2(780f, 34f));
    }

    static void WireController(Canvas canvas)
    {
        GameObject go = new GameObject("MainMenuUI");
        MainMenuUI ui = go.AddComponent<MainMenuUI>();
        ui.gameSceneName = "SampleScene";
        ui.startButton = FindButton(canvas.transform, "StartButton");
        ui.settingsButton = FindButton(canvas.transform, "SettingsButton");
        ui.codexButton = FindButton(canvas.transform, "CodexButton");
        ui.achievementsButton = FindButton(canvas.transform, "AchievementsButton");
        ui.quitButton = FindButton(canvas.transform, "QuitButton");
        ui.levelSelectButton = FindButton(canvas.transform, "LevelSelectButton");
        ui.characterSelectButton = FindButton(canvas.transform, "CharacterSelectButton");
        ui.statusText = FindText(canvas.transform, "StatusText");
    }

    // ---------------------------------------------------------------- widgets

    static Button MakeButton(Transform parent, string name, string caption,
        Color face, Color depth, int radius, Vector2 position, Vector2 size, int fontSize, Color labelColor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        // The root image is only a raycast target: the visible layers are children,
        // so the button can be a flat face plus a darker bottom edge, like the design.
        Image hitArea = go.GetComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);
        hitArea.raycastTarget = true;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Sprite round = Sprite("Generated/btn_round_" + radius);
        Image depthImage = NewImage(go.transform, "Depth", round, size);
        depthImage.color = depth;
        depthImage.rectTransform.anchoredPosition = new Vector2(0f, -8f);

        Image faceImage = NewImage(go.transform, "Face", round, size);
        faceImage.color = face;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = faceImage;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        Text label = NewText(go.transform, "Label", caption, fontSize, labelColor, cnFont,
            Vector2.zero, size - new Vector2(24f, 12f));
        if (labelColor.r + labelColor.g + labelColor.b > 1.8f)
            AddShadow(label, new Vector2(0f, -3f), new Color(0f, 0f, 0f, 0.22f));
        return button;
    }

    static Image NewImage(Transform parent, string name, Sprite sprite, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return image;
    }

    static Text NewText(Transform parent, string name, string value, int size, Color color, Font font,
        Vector2 position, Vector2 box, TextAnchor alignment = TextAnchor.MiddleCenter, bool wrap = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = font != null ? font : cnFont;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = box;
        rect.anchoredPosition = position;
        return text;
    }

    static void AddShadow(Text text, Vector2 distance, Color color)
    {
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = false;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static Button FindButton(Transform root, string name)
    {
        Transform found = Find(root, name);
        return found == null ? null : found.GetComponent<Button>();
    }

    static Text FindText(Transform root, string name)
    {
        Transform found = Find(root, name);
        return found == null ? null : found.GetComponent<Text>();
    }

    static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = Find(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    // ---------------------------------------------------------------- preview

    [MenuItem("VesselGuard/渲染主菜单预览", false, 2)]
    public static void RenderPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请先退出播放模式。");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        Camera cam = Object.FindAnyObjectByType<Camera>();
        if (canvas == null || cam == null)
        {
            Debug.LogError("CartoonMenu: 场景里找不到 Canvas 或 Camera。");
            return;
        }

        const int W = 1920, H = 1080;
        RenderTexture rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        cam.targetTexture = rt;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.35f, 0.72f, 0.92f, 1f);

        Canvas.ForceUpdateCanvases();
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0f, 0f, W, H), 0, 0);
        tex.Apply();

        string outPath = Path.Combine(Directory.GetCurrentDirectory(), PreviewPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        File.WriteAllBytes(outPath, tex.EncodeToPNG());

        cam.targetTexture = null;
        RenderTexture.active = null;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("CartoonMenu: 预览已输出 -> " + outPath);
    }
}
