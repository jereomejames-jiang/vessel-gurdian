using UnityEngine;
using UnityEngine.UI;

public class CardRevealUI : MonoBehaviour
{
    public static CardRevealUI Instance;

    public GameObject revealPanel;
    public Image cardBackground;
    public Text cardTitle;
    public Text cardDescription;
    public Text educationText;
    public Image glowBorder;
    public RectTransform cardRect;

    public float showDuration = 2.8f;
    public float timer;
    public bool isShowing;
    public Font displayFont;

    void Awake()
    {
        Instance = this;
        displayFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        if (revealPanel != null && displayFont != null)
            foreach (Text label in revealPanel.GetComponentsInChildren<Text>(true)) label.font = displayFont;
        EnsureCardRect();
        if (revealPanel != null) revealPanel.SetActive(false);
    }

    public void EnsureCardRect()
    {
        if (revealPanel == null) return;
        if (cardRect == null)
        {
            Transform frame = revealPanel.transform.Find("CardFrame");
            if (frame != null) cardRect = frame as RectTransform;
        }
        if (cardRect != null && !isShowing) cardRect.localScale = Vector3.one;
    }

    public void Show(SkillCardDefinition card)
    {
        if (card == null || revealPanel == null) return;

        EnsureCardRect();
        revealPanel.SetActive(true);
        revealPanel.transform.SetAsLastSibling();
        // Keep the dimmer stable; only the card itself should pop in.
        revealPanel.transform.localScale = Vector3.one;

        if (cardTitle != null) cardTitle.text = card.displayName;
        if (cardDescription != null) cardDescription.text = card.description;
        if (educationText != null) educationText.text = card.educationText;
        if (cardBackground != null) cardBackground.color = new Color(card.cardColor.r, card.cardColor.g, card.cardColor.b, 0.92f);
        if (glowBorder != null) glowBorder.color = new Color(1f, 0.84f, 0f, 1f);

        if (cardRect != null) cardRect.localScale = Vector3.zero;

        isShowing = true;
        timer = 0f;
        Time.timeScale = 0f;
    }

    void Update()
    {
        if (!isShowing || revealPanel == null) return;

        timer += Time.unscaledDeltaTime;
        EnsureCardRect();
        if (cardRect == null) return;

        float t = Mathf.Clamp01(timer / 0.45f);
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        float scale = eased * 1.08f;
        if (t >= 1f) scale = 1f + Mathf.Sin(timer * 3f) * 0.015f;
        cardRect.localScale = Vector3.one * scale;

        if (glowBorder != null)
        {
            float glow = 0.6f + Mathf.Sin(timer * 4f) * 0.4f;
            glowBorder.color = new Color(1f, 0.84f, 0f, glow);
        }

        if (timer > 0.6f && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            Dismiss();
            return;
        }

        if (timer >= showDuration) Dismiss();
    }

    public void Dismiss()
    {
        isShowing = false;
        if (revealPanel != null) revealPanel.SetActive(false);
        if (cardRect != null) cardRect.localScale = Vector3.one;

        LevelUpManager levels = FindAnyObjectByType<LevelUpManager>();
        if ((levels == null || !levels.isChoosing) &&
            (SkillCodexManager.Instance == null || !SkillCodexManager.Instance.panelOpen) &&
            (GameManager.Instance == null || !GameManager.Instance.gameEnded))
        {
            Time.timeScale = 1f;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (displayFont != null) Destroy(displayFont);
    }
}
