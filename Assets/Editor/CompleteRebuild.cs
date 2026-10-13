using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CompleteRebuild : EditorWindow
{
    [MenuItem("VesselGuard/完全重建场景")]
    public static void RebuildFromMenu()
    {
        CompleteRebuild window = GetWindow<CompleteRebuild>("VesselGuard 重建");
        window.Rebuild();
        window.Close();
    }

    public void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出播放模式后重建。");
        EnsureFolders();
        Scene original = EditorSceneManager.GetActiveScene();
        string backup = "Assets/Scenes/Backups/BeforeRebuild_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
        EditorSceneManager.SaveScene(original, backup, true);
        Time.timeScale = 1f;
        SetInputHandling();
        ClearScene();

        GameObject bulletPrefab = CreateBulletPrefab();
        GameObject xpOrbPrefab = CreateXPOrbPrefab();
        GameObject enemyPrefab = CreateEnemyPrefab(xpOrbPrefab);
        UpgradeOption[] upgrades = CreateUpgradeOptions();

        Camera camera = CreateCamera();
        CreateLight();
        CreateBackground();
        GameObject player = CreatePlayer(bulletPrefab);
        camera.GetComponent<CameraFollow>().target = player.transform;
        CreateEnemySpawner(enemyPrefab, player.transform);
        CreateDirectionArrow(player);
        CreateCanvas();
        CreateGameManager(upgrades);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (string.IsNullOrEmpty(scene.path))
        {
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/SampleScene.unity");
        }
        else
        {
            EditorSceneManager.SaveScene(scene);
        }

        var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity") != null)
            buildScenes.Add(new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true));
        buildScenes.Add(new EditorBuildSettingsScene(scene.path, true));
        EditorBuildSettings.scenes = buildScenes.ToArray();
        Selection.activeGameObject = player;
        Debug.Log("VesselGuard 场景重建完成。");
    }

    public void OnGUI()
    {
        EditorGUILayout.LabelField("VesselGuard 场景重建", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("点击下方按钮会清空当前场景，并重新创建完整游戏框架。", MessageType.Info);
        if (GUILayout.Button("完全重建场景", GUILayout.Height(32)))
        {
            Rebuild();
        }
    }

    public static void EnsureFolders()
    {
        CreateFolderIfMissing("Assets/Generated", "Assets");
        CreateFolderIfMissing("Assets/Prefabs", "Assets");
        CreateFolderIfMissing("Assets/Resources", "Assets");
        CreateFolderIfMissing("Assets/Editor", "Assets");
        CreateFolderIfMissing("Assets/Scenes", "Assets");
        CreateFolderIfMissing("Assets/Scenes/Backups", "Assets/Scenes");
    }

    public static void CreateFolderIfMissing(string path, string parent)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string folderName = path.Substring(parent.Length + 1);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    public static void SetInputHandling()
    {
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var handler = settings.FindProperty("activeInputHandler");
        if (handler == null) throw new InvalidOperationException("找不到 activeInputHandler 设置。");
        handler.intValue = 0; // 0 = Input Manager (Old), 1 = New, 2 = Both.
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void ClearScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            UnityEngine.Object.DestroyImmediate(roots[i]);
        }
    }

    public static GameObject CreateBulletPrefab()
    {
        string path = "Assets/Prefabs/Bullet.prefab";

        GameObject bullet = new GameObject("Bullet");
        bullet.transform.localScale = Vector3.one * 0.22f;
        SpriteRenderer renderer = bullet.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSquareSprite(Color.yellow, 16);
        renderer.sortingOrder = 3;

        Rigidbody2D body = bullet.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D collider = bullet.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.45f;

        Bullet bulletComponent = bullet.AddComponent<Bullet>();
        bulletComponent.damage = 1;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(bullet, path);
        UnityEngine.Object.DestroyImmediate(bullet);
        return prefab;
    }

    public static GameObject CreateEnemyPrefab(GameObject xpOrbPrefab)
    {
        string path = "Assets/Prefabs/Enemy.prefab";

        GameObject enemy = new GameObject("Enemy");
        SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSquareSprite(Color.red, 24);
        renderer.sortingOrder = 1;

        Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D collider = enemy.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = Vector2.one * 0.9f;

        Enemy enemyComponent = enemy.AddComponent<Enemy>();
        enemyComponent.maxHP = 3;
        enemyComponent.moveSpeed = 2f;
        enemyComponent.contactDamage = 1;
        enemyComponent.xpOrbPrefab = xpOrbPrefab;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(enemy, path);
        UnityEngine.Object.DestroyImmediate(enemy);
        return prefab;
    }

    public static GameObject CreateXPOrbPrefab()
    {
        string path = "Assets/Prefabs/ExperienceOrb.prefab";

        GameObject orb = new GameObject("ExperienceOrb");
        orb.transform.localScale = Vector3.one * 0.3f;
        SpriteRenderer renderer = orb.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateCircleSprite(Color.green, 20);
        renderer.sortingOrder = 2;

        Rigidbody2D body = orb.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        CircleCollider2D collider = orb.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.45f;

        ExperienceOrb orbComponent = orb.AddComponent<ExperienceOrb>();
        orbComponent.xpValue = 1;
        orbComponent.magnetRange = 3f;
        orbComponent.magnetSpeed = 8f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(orb, path);
        UnityEngine.Object.DestroyImmediate(orb);
        return prefab;
    }

    public static UpgradeOption[] CreateUpgradeOptions()
    {
        string[] paths =
        {
            "Assets/Resources/DamagePlusOne.asset",
            "Assets/Resources/AttackSpeedPlusTen.asset",
            "Assets/Resources/MoveSpeedPlusFifteen.asset",
            "Assets/Resources/MaxHPPlusTwo.asset",
            "Assets/Resources/BulletSpeedPlusTwenty.asset"
        };



        UpgradeOption[] options = new UpgradeOption[5];
        options[0] = CreateUpgrade(paths[0], "伤害 +1", "子弹伤害增加 1", UpgradeType.Damage, 1f);
        options[1] = CreateUpgrade(paths[1], "攻速 +10%", "攻击间隔缩短 10%", UpgradeType.AttackSpeed, 0.1f);
        options[2] = CreateUpgrade(paths[2], "移速 +15%", "玩家移动速度增加 15%", UpgradeType.MoveSpeed, 0.15f);
        options[3] = CreateUpgrade(paths[3], "血量 +2", "最大生命值增加 2", UpgradeType.MaxHP, 2f);
        options[4] = CreateUpgrade(paths[4], "弹速 +20%", "子弹速度增加 20%", UpgradeType.BulletSpeed, 0.2f);
        options[0].skillCard = CreateSkillCard("DamageCard", options[0], "减少烟草暴露、控制血脂，有助于保护血管内皮。");
        options[1].skillCard = CreateSkillCard("AttackSpeedCard", options[1], "规律监测血压，帮助心脏更轻松地工作。");
        options[2].skillCard = CreateSkillCard("MoveSpeedCard", options[2], "循序渐进的有氧活动能促进循环，运动要量力而行。");
        options[3].skillCard = CreateSkillCard("MaxHPCard", options[3], "少盐少油、多蔬果，配合体检可以更早发现风险。");
        options[4].skillCard = CreateSkillCard("BulletSpeedCard", options[4], "出现持续胸痛、呼吸困难等情况，应及时寻求专业帮助。");
        for (int i = 0; i < options.Length; i++) EditorUtility.SetDirty(options[i]);
        return options;
    }

    public static SkillCardDefinition CreateSkillCard(string assetName, UpgradeOption option, string educationText)
    {
        string path = "Assets/Resources/" + assetName + ".asset";
        SkillCardDefinition card = AssetDatabase.LoadAssetAtPath<SkillCardDefinition>(path);
        bool isNew = card == null;
        if (isNew) card = ScriptableObject.CreateInstance<SkillCardDefinition>();
        card.cardId = option.type.ToString();
        card.displayName = option.displayName;
        card.description = option.description;
        card.educationText = educationText;
        card.linkedUpgradeType = option.type;
        card.cardColor = Color.white;
        if (isNew) AssetDatabase.CreateAsset(card, path);
        EditorUtility.SetDirty(card);
        return card;
    }
    public static UpgradeOption CreateUpgrade(string path, string displayName, string description, UpgradeType type, float value)
    {
        UpgradeOption option = AssetDatabase.LoadAssetAtPath<UpgradeOption>(path);
        bool isNew = option == null;
        if (isNew) option = ScriptableObject.CreateInstance<UpgradeOption>();
        option.displayName = displayName;
        option.description = description;
        option.type = type;
        option.value = value;
        if (isNew) AssetDatabase.CreateAsset(option, path);
        EditorUtility.SetDirty(option);
        return option;
    }

    public static Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 7f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.04f, 0.04f, 0.08f, 1f);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<UniversalAdditionalCameraData>();

        CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
        follow.smoothSpeed = 10f;
        follow.orthographicSize = 7f;
        follow.lookAheadDistance = 1.4f;
        follow.lookAheadSmoothTime = 0.22f;
        follow.offset = new Vector3(0f, 0f, -10f);
        return camera;
    }

    public static void CreateLight()
    {
        GameObject lightObject = new GameObject("Global Light 2D");
        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;
        light.color = Color.white;
    }

    public static void CreateBackground()
    {
        GameObject background = new GameObject("Background");
        SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateBackgroundSprite(256);
        renderer.sortingOrder = -100;
        background.transform.position = new Vector3(0f, 0f, 5f);
        background.transform.localScale = new Vector3(40f, 25f, 1f);
    }

    public static void CreateDirectionArrow(GameObject player)
    {
        GameObject arrow = new GameObject("Direction Arrow");
        SpriteRenderer renderer = arrow.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateArrowSprite(new Color(1f, 0.82f, 0.1f, 1f), 32);
        renderer.sortingOrder = 0;

        DirectionArrow directionArrow = arrow.AddComponent<DirectionArrow>();
        directionArrow.target = player.transform;
        directionArrow.playerController = player.GetComponent<PlayerController>();
        directionArrow.worldOffset = new Vector3(0f, -0.82f, -0.1f);
        directionArrow.followSmooth = 24f;
        arrow.transform.position = player.transform.position + directionArrow.worldOffset;
    }

    public static GameObject CreatePlayer(GameObject bulletPrefab)
    {
        GameObject player = new GameObject("Player");
        player.transform.position = Vector3.zero;

        SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSquareSprite(Color.blue, 28);
        renderer.sortingOrder = 1;

        Rigidbody2D body = player.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
        collider.isTrigger = false;
        collider.size = Vector2.one * 0.85f;

        PlayerController controller = player.AddComponent<PlayerController>();
        controller.moveSpeed = 5f;
        controller.turnSpeed = 180f;
        controller.headingAngle = 90f;
        controller.autoMove = true;
        controller.rb = body;

        AutoAttack attack = player.AddComponent<AutoAttack>();
        attack.attackInterval = 0.5f;
        attack.bulletSpeed = 10f;
        attack.bulletLifetime = 2f;
        attack.bulletDamage = 1;
        attack.bulletPrefab = bulletPrefab;

        PlayerStats stats = player.AddComponent<PlayerStats>();
        stats.maxHP = 5;
        stats.xpPerLevel = 5;
        stats.xpGrowth = 1.5f;
        stats.level = 1;

        return player;
    }

    public static void CreateEnemySpawner(GameObject enemyPrefab, Transform player)
    {
        GameObject spawnerObject = new GameObject("Enemy Spawner");
        EnemySpawner spawner = spawnerObject.AddComponent<EnemySpawner>();
        spawner.enemyPrefab = enemyPrefab;
        spawner.spawnInterval = 2f;
        spawner.spawnRadius = 12f;
        spawner.player = player;
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
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();

        Slider xp = CreateSlider(canvas.transform, "XPSlider", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(520f, 24f), new Vector2(0f, -45f), new Color(0.2f, 0.9f, 0.3f, 1f));
        xp.minValue = 0f;
        xp.maxValue = 5f;
        xp.value = 0f;

        Slider hp = CreateSlider(canvas.transform, "HPSlider", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(520f, 24f), new Vector2(0f, -78f), new Color(0.9f, 0.2f, 0.2f, 1f));
        hp.minValue = 0f;
        hp.maxValue = 5f;
        hp.value = 5f;

        Text level = CreateText(canvas.transform, "LevelText", "Lv.1", 30, TextAnchor.MiddleLeft);
        SetRect(level.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180f, 50f), new Vector2(115f, -35f));

        Text timer = CreateText(canvas.transform, "TimerText", "05:00", 30, TextAnchor.MiddleRight);
        SetRect(timer.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(220f, 50f), new Vector2(-135f, -35f));

        GameObject panel = CreatePanel(canvas.transform, "LevelUpPanel", new Vector2(620f, 300f), new Color(0.04f, 0.04f, 0.08f, 0.95f));
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620f, 300f), Vector2.zero);

        Button choice1 = CreateButton(panel.transform, "Choice1", "升级 1");
        Button choice2 = CreateButton(panel.transform, "Choice2", "升级 2");
        Button choice3 = CreateButton(panel.transform, "Choice3", "升级 3");
        SetRect(choice1.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(180f, 170f), new Vector2(105f, 0f));
        SetRect(choice2.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180f, 170f), Vector2.zero);
        SetRect(choice3.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(180f, 170f), new Vector2(-105f, 0f));

        panel.SetActive(false);
        GameObject gameOver = CreatePanel(canvas.transform, "GameOverPanel", new Vector2(620f, 300f), new Color(0.04f, 0.04f, 0.08f, 0.97f));
        SetRect(gameOver.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620f, 300f), Vector2.zero);
        Text result = CreateText(gameOver.transform, "ResultText", "守卫结束", 32, TextAnchor.MiddleCenter);
        SetRect(result.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(560f, 140f), new Vector2(0f, 50f));
        Button restart = CreateButton(gameOver.transform, "RestartButton", "再来一次");
        SetRect(restart.GetComponent<RectTransform>(), Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(240f, 64f), new Vector2(0f, -85f));
        gameOver.SetActive(false);

        CreateCardRevealPanel(canvas.transform);

        return canvas;
    }

    public static GameManager CreateGameManager(UpgradeOption[] upgrades)
    {
        GameObject managerObject = new GameObject("GameManager");
        GameManager gameManager = managerObject.AddComponent<GameManager>();
        gameManager.gameDuration = 300f;

        LevelUpManager levelUpManager = managerObject.AddComponent<LevelUpManager>();
        levelUpManager.allUpgrades = upgrades;
        levelUpManager.choicesPerLevel = 3;
        levelUpManager.playerStats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
        levelUpManager.playerController = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        levelUpManager.autoAttack = UnityEngine.Object.FindAnyObjectByType<AutoAttack>();

        UIManager uiManager = managerObject.AddComponent<UIManager>();
        uiManager.canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        uiManager.playerStats = levelUpManager.playerStats;
        uiManager.levelUpManager = levelUpManager;

        CardRevealUI cardReveal = managerObject.AddComponent<CardRevealUI>();
        SetupCardRevealReferences(cardReveal);

        SkillCodexManager codex = managerObject.AddComponent<SkillCodexManager>();
        codex.canvas = uiManager.canvas;
        codex.cards = new SkillCardDefinition[upgrades.Length];
        for (int i = 0; i < upgrades.Length; i++) codex.cards[i] = upgrades[i] == null ? null : upgrades[i].skillCard;
        KnowledgeTicker ticker = managerObject.AddComponent<KnowledgeTicker>();
        ticker.canvas = uiManager.canvas;
        gameManager.skillCodexManager = codex;
        gameManager.knowledgeTicker = ticker;
        gameManager.levelUpManager = levelUpManager;
        PlayerStats stats = levelUpManager.playerStats;
        if (stats != null)
        {
            PlayerSkillCollection collection = stats.gameObject.GetComponent<PlayerSkillCollection>();
            if (collection == null) collection = stats.gameObject.AddComponent<PlayerSkillCollection>();
            collection.Initialize(upgrades);
            gameManager.playerSkillCollection = collection;
        }
        return gameManager;
    }

    public static void SetupCardRevealReferences(CardRevealUI reveal)
    {
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        reveal.revealPanel = FindChildObject(canvas.transform, "CardRevealPanel");
        if (reveal.revealPanel != null)
        {
            reveal.cardBackground = FindChild<Image>(reveal.revealPanel.transform, "CardBackground");
            reveal.cardTitle = FindChild<Text>(reveal.revealPanel.transform, "CardTitle");
            reveal.cardDescription = FindChild<Text>(reveal.revealPanel.transform, "CardDescription");
            reveal.educationText = FindChild<Text>(reveal.revealPanel.transform, "EducationText");
            reveal.glowBorder = FindChild<Image>(reveal.revealPanel.transform, "GlowBorder");
        }
    }

    public static GameObject FindChildObject(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child.gameObject;
            GameObject found = FindChildObject(child, name);
            if (found != null) return found;
        }
        return null;
    }

    public static T FindChild<T>(Transform parent, string name) where T : class
    {
        GameObject obj = FindChildObject(parent, name);
        if (obj != null) return obj.GetComponent(typeof(T)) as T;
        return null;
    }

    public static void CreateCardRevealPanel(Transform canvasParent)
    {
        GameObject panel = new GameObject("CardRevealPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasParent, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        panel.SetActive(false);

        GameObject cardObj = new GameObject("CardFrame", typeof(RectTransform), typeof(Image));
        cardObj.transform.SetParent(panel.transform, false);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = Vector2.one * 0.5f;
        cardRect.sizeDelta = new Vector2(420f, 560f);
        cardRect.localScale = Vector3.zero;
        cardObj.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.14f, 1f);

        Image glow = new GameObject("GlowBorder", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        glow.transform.SetParent(cardObj.transform, false);
        RectTransform glowRect = glow.rectTransform;
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-6f, -6f);
        glowRect.offsetMax = new Vector2(6f, 6f);
        glow.color = new Color(1f, 0.84f, 0f, 0.8f);

        Image bg = new GameObject("CardBackground", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        bg.transform.SetParent(cardObj.transform, false);
        RectTransform bgRect = bg.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = new Vector2(4f, 4f);
        bgRect.offsetMax = new Vector2(-4f, -4f);
        bg.color = new Color(0.12f, 0.2f, 0.35f, 0.92f);

        Text title = CreateText(cardObj.transform, "CardTitle", "", 30, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.1f, 0.78f);
        titleRect.anchorMax = new Vector2(0.9f, 0.92f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
        title.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei" }, 30);

        Text desc = CreateText(cardObj.transform, "CardDescription", "", 22, TextAnchor.MiddleCenter);
        RectTransform descRect = desc.rectTransform;
        descRect.anchorMin = new Vector2(0.1f, 0.58f);
        descRect.anchorMax = new Vector2(0.9f, 0.74f);
        descRect.offsetMin = descRect.offsetMax = Vector2.zero;
        desc.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei" }, 22);

        GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(cardObj.transform, false);
        RectTransform divRect = divider.GetComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0.15f, 0.54f);
        divRect.anchorMax = new Vector2(0.85f, 0.54f);
        divRect.offsetMin = divRect.offsetMax = Vector2.zero;
        divRect.sizeDelta = new Vector2(0f, 2f);
        divider.GetComponent<Image>().color = new Color(1f, 0.84f, 0f, 0.5f);

        Text edu = CreateText(cardObj.transform, "EducationText", "", 20, TextAnchor.MiddleCenter);
        RectTransform eduRect = edu.rectTransform;
        eduRect.anchorMin = new Vector2(0.1f, 0.12f);
        eduRect.anchorMax = new Vector2(0.9f, 0.50f);
        eduRect.offsetMin = eduRect.offsetMax = Vector2.zero;
        edu.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei" }, 20);
        edu.color = new Color(0.9f, 0.9f, 0.8f, 1f);

        Text hint = CreateText(cardObj.transform, "DismissHint", "点击或按空格继续", 16, TextAnchor.MiddleCenter);
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0.08f);
        hintRect.offsetMin = hintRect.offsetMax = Vector2.zero;
        hint.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei" }, 16);
        hint.color = new Color(0.7f, 0.7f, 0.7f, 1f);
    }

    public static Sprite CreateSquareSprite(Color color, int size)
    {
        return CreateSpriteAsset(color, size, false);
    }

    public static Sprite CreateCircleSprite(Color color, int size)
    {
        return CreateSpriteAsset(color, size, true);
    }

    public static Sprite CreateArrowSprite(Color color, int size)
    {
        string colorKey = ColorUtility.ToHtmlStringRGBA(color);
        string path = "Assets/Generated/Arrow_" + colorKey + "_" + size + ".asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            float t = 1f - (float)y / (size - 1);
            float halfWidth = Mathf.Lerp(1.5f, size * 0.42f, t);
            for (int x = 0; x < size; x++)
            {
                bool inside = Mathf.Abs(x - center) <= halfWidth;
                pixels[y * size + x] = inside ? color : new Color(0f, 0f, 0f, 0f);
            }
        }

        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = "Arrow_" + colorKey;
        AssetDatabase.AddObjectToAsset(sprite, texture);
        EditorUtility.SetDirty(texture);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static Sprite CreateBackgroundSprite(int size)
    {
        string path = "Assets/Generated/Background.asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color pixel = new Color(0.035f, 0.055f, 0.105f, 1f);
                if (x % 32 == 0 || y % 32 == 0) pixel = new Color(0.07f, 0.10f, 0.17f, 1f);

                float vesselY = size * 0.5f + Mathf.Sin(x * 0.055f) * size * 0.12f;
                if (Mathf.Abs(y - vesselY) < 2.2f) pixel = new Color(0.18f, 0.045f, 0.07f, 1f);

                float vesselX = size * 0.28f + Mathf.Sin(y * 0.045f) * size * 0.10f;
                if (Mathf.Abs(x - vesselX) < 1.6f) pixel = new Color(0.12f, 0.035f, 0.06f, 1f);

                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance > size * 0.43f && distance < size * 0.45f) pixel = new Color(0.08f, 0.12f, 0.20f, 1f);
                pixels[y * size + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = "Background";
        AssetDatabase.AddObjectToAsset(sprite, texture);
        EditorUtility.SetDirty(texture);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static Sprite CreateSpriteAsset(Color color, int size, bool circle)
    {
        string prefix = circle ? "Circle" : "Square";
        string colorKey = ColorUtility.ToHtmlStringRGBA(color);
        string path = "Assets/Generated/" + prefix + "_" + colorKey + "_" + size + ".asset";

        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = !circle || Vector2.Distance(new Vector2(x, y), center) <= radius;
                pixels[y * size + x] = inside ? color : new Color(0f, 0f, 0f, 0f);
            }
        }

        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();

        AssetDatabase.CreateAsset(texture, path);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = prefix + "_" + colorKey;
        AssetDatabase.AddObjectToAsset(sprite, texture);
        EditorUtility.SetDirty(texture);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    public static Slider CreateSlider(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position, Color fillColor)
    {
        GameObject sliderObject = new GameObject(name);
        sliderObject.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
        SetRect(sliderRect, anchorMin, anchorMax, size, position);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(sliderObject.transform, false);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.12f, 0.16f, 0.95f);
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject fillAreaObject = new GameObject("Fill Area");
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillAreaObject.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(3f, 3f);
        fillAreaRect.offsetMax = new Vector2(-3f, -3f);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        Image fill = fillObject.AddComponent<Image>();
        fill.color = fillColor;
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        slider.targetGraphic = fill;
        slider.fillRect = fillRect;
        return slider;
    }

    public static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.raycastTarget = false;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    public static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        panel.GetComponent<RectTransform>().sizeDelta = size;
        return panel;
    }

    public static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.15f, 0.25f, 0.4f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(buttonObject.transform, "Label", label, 22, TextAnchor.MiddleCenter);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 8f);
        textRect.offsetMax = new Vector2(-8f, -8f);
        return button;
    }
}



