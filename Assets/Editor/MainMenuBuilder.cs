using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuBuilder : EditorWindow
{
    [MenuItem("VesselGuard/创建主界面")]
    public static void CreateFromMenu()
    {
        MainMenuBuilder window = GetWindow<MainMenuBuilder>("VesselGuard 主界面");
        window.Build();
        window.Close();
    }

    public void OnGUI()
    {
        EditorGUILayout.LabelField("VesselGuard 主界面", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("创建主菜单、开始游戏按钮和其它系统预留入口。", MessageType.Info);
        if (GUILayout.Button("创建主界面", GUILayout.Height(32))) Build();
    }

    public void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请退出播放模式后再创建主界面。");
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
        CreateTitle(canvas.transform);
        CreateMenuButtons(canvas.transform);
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
        Debug.Log("VesselGuard 主界面创建完成。");
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
        camera.backgroundColor = new Color(0.015f, 0.025f, 0.055f, 1f);
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
        GameObject background = new GameObject("Background");
        background.transform.SetParent(parent, false);
        Image image = background.AddComponent<Image>();
        image.color = new Color(0.025f, 0.045f, 0.09f, 1f);
        SetRect(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject glow = CreatePanel(parent, "CenterGlow", new Color(0.04f, 0.14f, 0.24f, 0.55f));
        SetRect(glow.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1050f, 720f), Vector2.zero);

        GameObject line = CreatePanel(parent, "TopAccent", new Color(0.15f, 0.75f, 0.9f, 0.8f));
        SetRect(line.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(500f, 5f), new Vector2(0f, -110f));
    }

    public static void CreateTitle(Transform parent)
    {
        Text title = CreateText(parent, "GameTitle", "VESSELGUARD", 66, TextAnchor.MiddleCenter);
        title.color = new Color(0.55f, 0.95f, 1f, 1f);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 100f), new Vector2(0f, -185f));

        Text subtitle = CreateText(parent, "GameSubtitle", "血 管 守 卫", 38, TextAnchor.MiddleCenter);
        subtitle.color = new Color(1f, 0.35f, 0.42f, 1f);
        SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 68f), new Vector2(0f, -270f));

        Text description = CreateText(parent, "GameDescription", "守护血管健康，击退风险细胞", 22, TextAnchor.MiddleCenter);
        description.color = new Color(0.75f, 0.82f, 0.9f, 1f);
        SetRect(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(800f, 50f), new Vector2(0f, -330f));
    }

    public static void CreateMenuButtons(Transform parent)
    {
        Button start = CreateButton(parent, "StartButton", "开始游戏", 30, new Color(0.12f, 0.58f, 0.72f, 1f));
        SetRect(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 84f), new Vector2(0f, 170f));

        Text systemTitle = CreateText(parent, "SystemTitle", "其它系统入口", 22, TextAnchor.MiddleCenter);
        systemTitle.color = new Color(0.6f, 0.7f, 0.82f, 1f);
        SetRect(systemTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(480f, 50f), new Vector2(0f, 45f));

        Button settings = CreateButton(parent, "SettingsButton", "设置（预留）", 20, new Color(0.14f, 0.2f, 0.3f, 1f));
        SetRect(settings.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(250f, 68f), new Vector2(-285f, -55f));

        Button codex = CreateButton(parent, "CodexButton", "血管图鉴（预留）", 20, new Color(0.14f, 0.2f, 0.3f, 1f));
        SetRect(codex.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(250f, 68f), new Vector2(0f, -55f));

        Button achievements = CreateButton(parent, "AchievementsButton", "成就（预留）", 20, new Color(0.14f, 0.2f, 0.3f, 1f));
        SetRect(achievements.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(250f, 68f), new Vector2(285f, -55f));

        Button quit = CreateButton(parent, "QuitButton", "退出", 20, new Color(0.28f, 0.12f, 0.18f, 1f));
        SetRect(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(170f, 58f), new Vector2(0f, 88f));
    }

    public static void CreateFooter(Transform parent)
    {
        Text status = CreateText(parent, "StatusText", "版本 0.1  ·  血管健康教育项目", 18, TextAnchor.MiddleCenter);
        status.color = new Color(0.5f, 0.58f, 0.7f, 1f);
        SetRect(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(900f, 42f), new Vector2(0f, 30f));
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
