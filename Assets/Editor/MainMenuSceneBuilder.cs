using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainMenuSceneBuilder
{
    [MenuItem("VesselGuard/重建主菜单场景")]
    public static void RebuildMainMenu()
    {
        string scenePath = "Assets/Scenes/MainMenu.unity";
        var scene = EditorSceneManager.OpenScene(scenePath);
        var root = scene.GetRootGameObjects();

        // 清理旧的 UI 元素
        foreach (var obj in root)
        {
            if (obj.name == "Canvas" || obj.name == "EventSystem")
            {
                Object.DestroyImmediate(obj);
            }
        }

        // 创建背景滚动对象
        GameObject backgroundObj = new GameObject("MainMenuBackground");
        var bgScript = backgroundObj.AddComponent<MainMenuBackground>();

        // 创建主角
        GameObject playerObj = new GameObject("MainMenuPlayer");
        var playerSprite = playerObj.AddComponent<SpriteRenderer>();
        playerSprite.sprite = CreateCircleSprite();
        playerSprite.color = CharacterCatalog.Colors[0];
        playerSprite.sortingOrder = 5;
        playerObj.transform.position = new Vector3(0f, 5f, 0f);
        playerObj.transform.localScale = Vector3.one * 1.2f;

        bgScript.playerTransform = playerObj.transform;
        bgScript.playerSprite = playerSprite;

        // 创建 Canvas
        GameObject canvasObj = new GameObject("Canvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasObj.AddComponent<GraphicRaycaster>();

        // 创建 EventSystem
        GameObject eventObj = new GameObject("EventSystem");
        eventObj.AddComponent<EventSystem>();
        eventObj.AddComponent<StandaloneInputModule>();

        // 创建设置按钮（左侧）
        var settingsBtn = CreateButton(canvas.transform, "SettingsButton", "设置",
            new Vector2(-700f, -400f), new Vector2(200f, 60f), new Color(0.2f, 0.3f, 0.4f, 1f));

        // 创建图鉴按钮（右侧）
        var codexBtn = CreateButton(canvas.transform, "CodexButton", "血管图鉴",
            new Vector2(700f, -400f), new Vector2(200f, 60f), new Color(0.2f, 0.3f, 0.4f, 1f));

        // 创建开始按钮（底部中央）
        var startBtn = CreateButton(canvas.transform, "StartButton", "开始游戏",
            new Vector2(0f, -300f), new Vector2(280f, 80f), new Color(0.3f, 0.6f, 0.4f, 1f));

        // 创建关卡选择按钮（开始按钮下方）
        var levelBtn = CreateButton(canvas.transform, "LevelSelectButton", "选择关卡",
            new Vector2(0f, -400f), new Vector2(200f, 50f), new Color(0.25f, 0.4f, 0.5f, 1f));
        levelBtn.onClick.AddListener(() => {
            var ui = MainMenuUI.Instance;
            if (ui != null) ui.OpenLevelSelect();
        });

        // 添加 MainMenuUI 组件
        var uiScript = canvasObj.AddComponent<MainMenuUI>();
        uiScript.startButton = startBtn;
        uiScript.settingsButton = settingsBtn;
        uiScript.codexButton = codexBtn;
        uiScript.background = bgScript;

        // 保存场景
        EditorSceneManager.SaveScene(scene);
        Debug.Log("主菜单场景已重建完成");
    }

    static Button CreateButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, Color color)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        var rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;

        var image = btnObj.GetComponent<Image>();
        image.color = color;

        var button = btnObj.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(Mathf.Min(1f, color.r + 0.1f), Mathf.Min(1f, color.g + 0.1f), Mathf.Min(1f, color.b + 0.1f), 1f);
        colors.pressedColor = new Color(Mathf.Max(0f, color.r - 0.1f), Mathf.Max(0f, color.g - 0.1f), Mathf.Max(0f, color.b - 0.1f), 1f);
        button.colors = colors;

        // 添加文字
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        var textComp = textObj.GetComponent<Text>();
        textComp.text = text;
        textComp.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/simhei.ttf");
        textComp.fontSize = 24;
        textComp.alignment = TextAnchor.MiddleCenter;
        textComp.color = Color.white;
        textComp.raycastTarget = false;

        return button;
    }

    static Sprite CreateCircleSprite()
    {
        Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        tex.name = "MainMenuPlayerSprite";
        Color[] pixels = new Color[64 * 64];
        Vector2 center = new Vector2(31.5f, 31.5f);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / 31.5f;
                pixels[y * 64 + x] = dist <= 1f ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 64f);
    }
}
