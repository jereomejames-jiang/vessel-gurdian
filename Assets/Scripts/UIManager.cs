using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public Canvas canvas;
    public Slider XPSlider;
    public Slider HPSlider;
    public Text LevelText;
    public Text TimerText;
    public GameObject LevelUpPanel;
    public Button[] choiceButtons;

    public PlayerStats playerStats;
    public LevelUpManager levelUpManager;
    public GameManager gameManager;
    public GameObject GameOverPanel;
    public Text ResultText;
    public Button RestartButton;
    public Font displayFont;
    public Button pauseButton;
    public GameObject PausePanel;
    public Text PauseTitle;
    public Button ResumeButton;
    public Button ExitButton;
    public string mainMenuSceneName = "MainMenu";
    public bool isPausedByMenu;
    public float previousTimeScale = 1f;
    public Button speedTestButton;
    public Button invincibleTestButton;
    public Text speedTestLabel;
    public Text invincibleTestLabel;
    public bool speedSixX;
    public float testSpeedMultiplier = 6f;

    void Awake()
    {
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        XPSlider = FindChild<Slider>(canvas.transform, "XPSlider");
        HPSlider = FindChild<Slider>(canvas.transform, "HPSlider");
        LevelText = FindChild<Text>(canvas.transform, "LevelText");
        TimerText = FindChild<Text>(canvas.transform, "TimerText");
        LevelUpPanel = FindChild<GameObject>(canvas.transform, "LevelUpPanel");

        GameOverPanel = FindChild<GameObject>(canvas.transform, "GameOverPanel");
        ResultText = FindChild<Text>(canvas.transform, "ResultText");
        RestartButton = FindChild<Button>(canvas.transform, "RestartButton");
        if (GameOverPanel != null) GameOverPanel.SetActive(false);
        if (RestartButton != null) RestartButton.onClick.AddListener(() => { if (gameManager != null) gameManager.RestartGame(); });

        // The Windows prototype uses an installed CJK font for Chinese labels.
        displayFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.font = displayFont;
        EnsurePauseView();
        EnsureTestTools();

        choiceButtons = new Button[3];
        choiceButtons[0] = FindChild<Button>(canvas.transform, "Choice1");
        choiceButtons[1] = FindChild<Button>(canvas.transform, "Choice2");
        choiceButtons[2] = FindChild<Button>(canvas.transform, "Choice3");

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;
            if (choiceButtons[i] != null)
            {
                choiceButtons[i].onClick.AddListener(() => Choose(index));
            }
        }

        if (LevelUpPanel != null) LevelUpPanel.SetActive(false);
    }

    void OnEnable()
    {
        playerStats = FindAnyObjectByType<PlayerStats>();
        levelUpManager = FindAnyObjectByType<LevelUpManager>();
        gameManager = GameManager.Instance;
        if (gameManager != null) gameManager.OnGameEnded += ShowGameOver;

        if (playerStats != null)
        {
            playerStats.OnHPChanged += UpdateHP;
            playerStats.OnXPChanged += UpdateXP;
        }

        if (levelUpManager != null)
        {
            levelUpManager.OnShowChoices += ShowChoices;
            levelUpManager.OnChoicesClosed += HideChoices;
        }
    }

    void Start()
    {
        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        if (levelUpManager == null) levelUpManager = FindAnyObjectByType<LevelUpManager>();
        if (playerStats != null)
        {
            UpdateHP(playerStats.currentHP, playerStats.maxHP);
            UpdateXP(playerStats.currentXP, playerStats.GetXPToNextLevel(), playerStats.level);
        }
    }

    void Update()
    {
        if (LevelText != null && playerStats != null)
        {
            LevelText.text = "Lv." + playerStats.level;
        }

        if (TimerText != null && GameManager.Instance != null)
        {
            TimerText.text = GameManager.Instance.selectedLevelName + "  " + FormatTime(GameManager.Instance.GetRemaining());
        }

        if (Input.GetKeyDown(KeyCode.Escape) && !isPausedByMenu)
            TogglePauseMenu();
        if (Input.GetKeyDown(KeyCode.F6)) ToggleSixXSpeed();
        if (Input.GetKeyDown(KeyCode.F7)) ToggleInvincibility();
        UpdateTestLabels();
    }

    void OnDisable()
    {
        if (gameManager != null) gameManager.OnGameEnded -= ShowGameOver;
        if (playerStats != null)
        {
            playerStats.OnHPChanged -= UpdateHP;
            playerStats.OnXPChanged -= UpdateXP;
        }

        if (levelUpManager != null)
        {
            levelUpManager.OnShowChoices -= ShowChoices;
            levelUpManager.OnChoicesClosed -= HideChoices;
        }
    }

    public T FindChild<T>(Transform parent, string childName) where T : class
    {
        if (parent == null) return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                if (typeof(T) == typeof(GameObject)) return child.gameObject as T;
                return child.GetComponent(typeof(T)) as T;
            }

            T nested = FindChild<T>(child, childName);
            if (nested != null) return nested;
        }

        return null;
    }

    public void UpdateHP(int current, int max)
    {
        if (HPSlider == null) return;
        HPSlider.maxValue = Mathf.Max(1, max);
        HPSlider.value = current;
    }

    public void UpdateXP(int current, int needed, int level)
    {
        if (XPSlider == null) return;
        XPSlider.maxValue = Mathf.Max(1, needed);
        XPSlider.value = current;
        if (LevelText != null) LevelText.text = "Lv." + level;
    }

    public void ShowChoices(UpgradeOption[] choices)
    {
        if (LevelUpPanel == null) return;

        LevelUpPanel.SetActive(true);
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        SetTestToolsVisible(false);
        if (SkillCodexManager.Instance != null)
        {
            SkillCodexManager.Instance.SetPanel(false);
            if (SkillCodexManager.Instance.codexButton != null) SkillCodexManager.Instance.codexButton.gameObject.SetActive(false);
        }
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null) continue;

            bool active = choices != null && i < choices.Length && choices[i] != null;
            button.gameObject.SetActive(active);
            if (!active) continue;

            UpgradeOption option = choices[i];
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = option.displayName + "\n" + option.description;
        }
    }

    public void Choose(int index)
    {
        if (levelUpManager == null || levelUpManager.currentChoices == null) return;
        if (index < 0 || index >= levelUpManager.currentChoices.Length) return;

        levelUpManager.ApplyUpgrade(levelUpManager.currentChoices[index]);
    }

    public void HideChoices()
    {
        if (LevelUpPanel != null) LevelUpPanel.SetActive(false);
        if (!isPausedByMenu && pauseButton != null && (gameManager == null || !gameManager.gameEnded))
            pauseButton.gameObject.SetActive(true);
        if (!isPausedByMenu && (gameManager == null || !gameManager.gameEnded)) SetTestToolsVisible(true);
        if (SkillCodexManager.Instance != null && SkillCodexManager.Instance.codexButton != null)
            SkillCodexManager.Instance.codexButton.gameObject.SetActive(true);
    }

    public void ShowGameOver(bool won)
    {
        isPausedByMenu = false;
        if (PausePanel != null) PausePanel.SetActive(false);
        HideChoices();
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        SetTestToolsVisible(false);
        if (ResultText != null) ResultText.text = won ? "守卫成功！\n你坚持了五分钟" : "守卫结束\n再试一次吧";
        if (GameOverPanel != null) GameOverPanel.SetActive(true);
    }

    public void EnsurePauseView()
    {
        if (canvas == null) return;

        pauseButton = FindChild<Button>(canvas.transform, "PauseButton");
        PausePanel = FindChild<GameObject>(canvas.transform, "PausePanel");

        if (pauseButton == null)
        {
            pauseButton = CreateButton(canvas.transform, "PauseButton", "暂停",
                new Vector2(-74f, -38f), new Vector2(116f, 52f),
                new Color(0.08f, 0.18f, 0.28f, 0.95f), new Vector2(1f, 1f));
        }
        pauseButton.onClick.RemoveListener(TogglePauseMenu);
        pauseButton.onClick.AddListener(TogglePauseMenu);

        if (PausePanel == null)
        {
            PausePanel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
            PausePanel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = PausePanel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(520f, 320f);
            panelRect.anchoredPosition = Vector2.zero;
            PausePanel.GetComponent<Image>().color = new Color(0.015f, 0.04f, 0.08f, 0.97f);

            PauseTitle = CreateText(PausePanel.transform, "PauseTitle", "游戏暂停", 34,
                TextAnchor.MiddleCenter, new Vector2(0f, 96f), new Vector2(420f, 58f));
            PauseTitle.color = new Color(0.75f, 0.94f, 1f, 1f);
            ResumeButton = CreateButton(PausePanel.transform, "ResumeButton", "继续游戏",
                new Vector2(0f, 28f), new Vector2(300f, 58f),
                new Color(0.08f, 0.48f, 0.62f, 1f));
            ExitButton = CreateButton(PausePanel.transform, "ExitButton", "退出到主界面",
                new Vector2(0f, -58f), new Vector2(300f, 58f),
                new Color(0.42f, 0.13f, 0.2f, 1f));
            ResumeButton.onClick.AddListener(ResumeGame);
            ExitButton.onClick.AddListener(ExitToMainMenu);
        }

        if (PauseTitle == null) PauseTitle = FindChild<Text>(PausePanel.transform, "PauseTitle");
        if (ResumeButton == null) ResumeButton = FindChild<Button>(PausePanel.transform, "ResumeButton");
        if (ExitButton == null) ExitButton = FindChild<Button>(PausePanel.transform, "ExitButton");
        if (ResumeButton != null)
        {
            ResumeButton.onClick.RemoveListener(ResumeGame);
            ResumeButton.onClick.AddListener(ResumeGame);
        }
        if (ExitButton != null)
        {
            ExitButton.onClick.RemoveListener(ExitToMainMenu);
            ExitButton.onClick.AddListener(ExitToMainMenu);
        }

        PausePanel.SetActive(false);
    }

    public void TogglePauseMenu()
    {
        if (gameManager != null && gameManager.gameEnded) return;
        if (levelUpManager != null && levelUpManager.isChoosing) return;
        if (isPausedByMenu) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        if (PausePanel == null) EnsurePauseView();
        if (PausePanel == null) return;
        previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
        isPausedByMenu = true;
        Time.timeScale = 0f;
        PausePanel.SetActive(true);
        PausePanel.transform.SetAsLastSibling();
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
    }

    public void ResumeGame()
    {
        isPausedByMenu = false;
        if (PausePanel != null) PausePanel.SetActive(false);
        if (pauseButton != null && (gameManager == null || !gameManager.gameEnded))
            pauseButton.gameObject.SetActive(true);
        if (gameManager == null || !gameManager.gameEnded)
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
    }

    public void ExitToMainMenu()
    {
        isPausedByMenu = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void EnsureTestTools()
    {
        if (canvas == null) return;
        speedTestButton = FindChild<Button>(canvas.transform, "SpeedTestButton");
        invincibleTestButton = FindChild<Button>(canvas.transform, "InvincibleTestButton");

        if (speedTestButton == null)
        {
            speedTestButton = CreateButton(canvas.transform, "SpeedTestButton", "加速 ×6",
                new Vector2(-220f, -38f), new Vector2(126f, 52f),
                new Color(0.16f, 0.3f, 0.2f, 0.95f), new Vector2(1f, 1f));
        }
        if (invincibleTestButton == null)
        {
            invincibleTestButton = CreateButton(canvas.transform, "InvincibleTestButton", "无敌：关",
                new Vector2(-365f, -38f), new Vector2(126f, 52f),
                new Color(0.3f, 0.18f, 0.08f, 0.95f), new Vector2(1f, 1f));
        }

        speedTestLabel = speedTestButton.GetComponentInChildren<Text>(true);
        invincibleTestLabel = invincibleTestButton.GetComponentInChildren<Text>(true);
        speedTestButton.onClick.RemoveListener(ToggleSixXSpeed);
        speedTestButton.onClick.AddListener(ToggleSixXSpeed);
        invincibleTestButton.onClick.RemoveListener(ToggleInvincibility);
        invincibleTestButton.onClick.AddListener(ToggleInvincibility);
        UpdateTestLabels();
    }

    public void SetTestToolsVisible(bool visible)
    {
        if (speedTestButton != null) speedTestButton.gameObject.SetActive(visible);
        if (invincibleTestButton != null) invincibleTestButton.gameObject.SetActive(visible);
    }

    public void UpdateTestLabels()
    {
        if (speedTestLabel != null) speedTestLabel.text = speedSixX ? "速度：×6" : "加速 ×6";
        if (invincibleTestLabel != null)
        {
            bool enabled = playerStats != null && playerStats.invincible;
            invincibleTestLabel.text = enabled ? "无敌：开" : "无敌：关";
        }
    }

    public void ToggleSixXSpeed()
    {
        if (gameManager != null && gameManager.gameEnded) return;
        if (levelUpManager != null && levelUpManager.isChoosing) return;
        speedSixX = !speedSixX;
        Time.timeScale = speedSixX ? testSpeedMultiplier : 1f;
        UpdateTestLabels();
    }

    public void ToggleInvincibility()
    {
        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerStats == null) return;
        playerStats.invincible = !playerStats.invincible;
        UpdateTestLabels();
    }

    public Button CreateButton(Transform parent, string objectName, string caption, Vector2 position,
        Vector2 size, Color color, Vector2? anchor = null)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        Vector2 chosenAnchor = anchor ?? new Vector2(0.5f, 0.5f);
        rect.anchorMin = rect.anchorMax = chosenAnchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = obj.GetComponent<Image>();
        image.color = color;
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(Mathf.Min(1f, color.r + 0.14f), Mathf.Min(1f, color.g + 0.14f), Mathf.Min(1f, color.b + 0.14f), 1f);
        colors.pressedColor = new Color(Mathf.Max(0f, color.r - 0.08f), Mathf.Max(0f, color.g - 0.08f), Mathf.Max(0f, color.b - 0.08f), 1f);
        button.colors = colors;
        Text label = CreateText(obj.transform, "Label", caption, 22, TextAnchor.MiddleCenter, Vector2.zero, size - Vector2.one * 10f);
        label.color = Color.white;
        return button;
    }

    public Text CreateText(Transform parent, string objectName, string value, int fontSize,
        TextAnchor alignment, Vector2 position, Vector2 size)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        Text text = obj.GetComponent<Text>();
        text.font = displayFont == null ? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") : displayFont;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return text;
    }

    void OnDestroy() { if (displayFont != null) Destroy(displayFont); }

    public string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return minutes.ToString("00") + ":" + remainingSeconds.ToString("00");
    }
}

