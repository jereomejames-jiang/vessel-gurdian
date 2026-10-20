using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuBuilder : EditorWindow
{
    [MenuItem("VesselGuard/重建主界面")]
    public static void CreateFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请退出播放模式后再重建主界面。");
            return;
        }

        Scene original = SceneManager.GetActiveScene();
        string originalPath = original.path;
        if (original.isDirty && !string.IsNullOrEmpty(originalPath)) EditorSceneManager.SaveScene(original);

        Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        menuScene.name = "MainMenu";

        Camera menuCamera = CreateCamera();
        Canvas canvas = CreateCanvas();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = menuCamera;
        canvas.planeDistance = 1f;

        CreateBackground(canvas.transform);
        CreateTitleArea(canvas.transform);
        CreateButtonArea(canvas.transform);
        CreateFooter(canvas.transform);

        GameObject controllerObject = new GameObject("MainMenuUI");
        MainMenuUI controller = controllerObject.AddComponent<MainMenuUI>();
        controller.gameSceneName = "SampleScene";
        controller.startButton = FindButton(canvas.transform, "StartButton");
        controller.settingsButton = FindButton(canvas.transform, "SettingsButton");
        controller.codexButton = FindButton(canvas.transform, "CodexButton");
        controller.achievementsButton = FindButton(canvas.transform, "AchievementsButton");
        controller.quitButton = FindButton(canvas.transform, "QuitButton");
        controller.statusText = FindText(canvas.transform, "StatusText");

        string menuPath = "Assets/Scenes/MainMenu.unity";
        EditorSceneManager.SaveScene(menuScene, menuPath);
        ConfigureBuildSettings(menuPath, "Assets/Scenes/SampleScene.unity");

        if (!string.IsNullOrEmpty(originalPath) && originalPath != menuPath)
        {
            EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("VesselGuard 主界面重建完成。");
    }

    public static void ConfigureBuildSettings(string menuPath, string gamePath)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(menuPath) != null)
            scenes.Add(new EditorBuildSettingsScene(menuPath, true));
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(gamePath) != null)
            scenes.Add(new EditorBuildSettingsScene(gamePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    public static Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("MainMenu Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.012f, 0.02f, 0.045f, 1f);
        return camera;
    }

    public static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
        return canvas;
    }

    public static void CreateBackground(Transform parent)
    {
        // Deep space background
        GameObject background = new GameObject("Background");
        background.transform.SetParent(parent, false);
        Image image = background.AddComponent<Image>();
        image.color = new Color(0.012f, 0.02f, 0.045f, 1f);
        SetRect(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Center glow
        GameObject glow = CreatePanel(parent, "CenterGlow", new Color(0.03f, 0.12f, 0.22f, 0.4f));
        SetRect(glow.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1100f, 750f), Vector2.zero);

        // Top accent line
        GameObject line = CreatePanel(parent, "TopAccent", new Color(0.12f, 0.68f, 0.85f, 0.6f));
        SetRect(line.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(480f, 4f), new Vector2(0f, -100f));
    }

    public static void CreateTitleArea(Transform parent)
    {
        // Title - large and prominent
        Text title = CreateText(parent, "GameTitle", "VESSELGUARD", 72, TextAnchor.MiddleCenter);
        title.color = new Color(0.55f, 0.92f, 1f, 1f);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 110f), new Vector2(0f, -150f));
        AddOutline(title, new Color(0f, 0f, 0f, 0.4f), new Vector2(1f, 1f));

        // Subtitle
        Text subtitle = CreateText(parent, "GameSubtitle", "血 管 守 卫", 42, TextAnchor.MiddleCenter);
        subtitle.color = new Color(0.98f, 0.35f, 0.42f, 1f);
        SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 80f), new Vector2(0f, -250f));
        AddOutline(subtitle, new Color(0f, 0f, 0f, 0.4f), new Vector2(0.5f, 0.5f));

        // Description - well separated from buttons
        Text description = CreateText(parent, "GameDescription", "守护血管健康，击退风险细胞", 24, TextAnchor.MiddleCenter);
        description.color = new Color(0.72f, 0.80f, 0.90f, 1f);
        SetRect(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(800f, 60f), new Vector2(0f, -340f));
    }

    public static void CreateButtonArea(Transform parent)
    {
        // Start button - primary action, centered
        Button start = CreateButton(parent, "StartButton", "开始游戏", 34, new Color(0.12f, 0.58f, 0.72f, 1f));
        SetRect(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400f, 100f), new Vector2(0f, 60f));

        // System entries label
        Text systemTitle = CreateText(parent, "SystemTitle", "其它系统入口", 22, TextAnchor.MiddleCenter);
        systemTitle.color = new Color(0.58f, 0.68f, 0.80f, 1f);
        SetRect(systemTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(480f, 50f), new Vector2(0f, -80f));

        // Secondary buttons row
        Button settings = CreateButton(parent, "SettingsButton", "设置（预留）", 22, new Color(0.14f, 0.20f, 0.30f, 1f));
        SetRect(settings.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260f, 72f), new Vector2(-320f, -180f));

        Button codex = CreateButton(parent, "CodexButton", "血管图鉴", 22, new Color(0.14f, 0.20f, 0.30f, 1f));
        SetRect(codex.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260f, 72f), new Vector2(0f, -180f));

        Button achievements = CreateButton(parent, "AchievementsButton", "成就（预留）", 22, new Color(0.14f, 0.20f, 0.30f, 1f));
        SetRect(achievements.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260f, 72f), new Vector2(320f, -180f));

        // Quit button - bottom center
        Button quit = CreateButton(parent, "QuitButton", "退出", 22, new Color(0.28f, 0.14f, 0.20f, 1f));
        SetRect(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(200f, 64f), new Vector2(0f, 80f));
    }

    public static void CreateFooter(Transform parent)
    {
        Text status = CreateText(parent, "StatusText", "版本 0.1  ·  血管健康教育项目", 18, TextAnchor.MiddleCenter);
        status.color = new Color(0.48f, 0.56f, 0.68f, 1f);
        SetRect(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(900f, 42f), new Vector2(0f, 28f));
    }

    public static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    public static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    public static Button CreateButton(Transform parent, string name, string label, int fontSize, Color color)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(Mathf.Min(1f, color.r + 0.12f), Mathf.Min(1f, color.g + 0.12f), Mathf.Min(1f, color.b + 0.12f), 1f);
        colors.pressedColor = new Color(Mathf.Max(0f, color.r - 0.08f), Mathf.Max(0f, color.g - 0.08f), Mathf.Max(0f, color.b - 0.08f), 1f);
        button.colors = colors;

        Text text = CreateText(buttonObject.transform, "Label", label, fontSize, TextAnchor.MiddleCenter);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 8f);
        textRect.offsetMax = new Vector2(-8f, -8f);
        return button;
    }

    public static void AddOutline(Text text, Color color, Vector2 distance)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = false;
    }

    public static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    public static Button FindButton(Transform parent, string name)
    {
        Transform child = FindChild(parent, name);
        return child == null ? null : child.GetComponent<Button>();
    }

    public static Text FindText(Transform parent, string name)
    {
        Transform child = FindChild(parent, name);
        return child == null ? null : child.GetComponent<Text>();
    }

    public static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
            Transform nested = FindChild(child, name);
            if (nested != null) return nested;
        }
        return null;
    }
}
