using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime skin shared by the menu popups (level select, character select).
/// Mirrors CartoonMenuBuilder: white rounded card plus flat two-layer buttons.
/// Sprites live in Resources/UI so panels built at runtime can reuse them.
/// </summary>
public static class MenuSkin
{
    public static readonly Color Ink = Hex(0x14435C);
    public static readonly Color Muted = Hex(0x5E8296);
    public static readonly Color Faint = Hex(0x8FB3C6);
    public static readonly Color Blue = Hex(0x36BDF7);
    public static readonly Color BlueDepth = Hex(0x146587);
    public static readonly Color Grey = Hex(0xB9BCCB);
    public static readonly Color GreyDepth = Hex(0x666880);
    public static readonly Color Yellow = Hex(0xFFCC00);
    public static readonly Color YellowDepth = Hex(0xFF8D28);
    public static readonly Color Pink = Hex(0xFF627B);
    public static readonly Color PinkDepth = Hex(0x871023);
    public static readonly Color White = Hex(0xFFFFFF);
    public static readonly Color CardIdle = Hex(0xE6EDF2);
    public static readonly Color CardIdleDepth = Hex(0xB9C9D3);
    // Opaque board colour: at 94% the white menu card bled through as a grey halo
    // (linear colour space makes a low alpha over white read much brighter).
    public static readonly Color Scrim = new Color(1f / 255f, 85f / 255f, 117f / 255f, 1f);

    static Color Hex(int rgb)
    {
        return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }

    static Sprite card, round26, round20, round16;

    static Sprite CardSprite
    {
        get { if (card == null) card = Resources.Load<Sprite>("UI/panel_white"); return card; }
    }

    static Sprite Round(int radius)
    {
        if (radius >= 24)
        {
            if (round26 == null) round26 = Resources.Load<Sprite>("UI/btn_round_26");
            return round26;
        }
        if (radius >= 18)
        {
            if (round20 == null) round20 = Resources.Load<Sprite>("UI/btn_round_20");
            return round20;
        }
        if (round16 == null) round16 = Resources.Load<Sprite>("UI/btn_round_16");
        return round16;
    }

    public static Image FullScreen(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    public static Image Card(Transform parent, string name, Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = CardSprite;
        image.type = Image.Type.Sliced;
        image.color = White;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return image;
    }

    public static Text Label(Transform parent, string name, string value, int size, Color color,
        TextAnchor alignment, Vector2 position, Vector2 box)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = RuntimeFont.Get();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = box;
        rect.anchoredPosition = position;
        return text;
    }

    public static Button Button(Transform parent, string name, string caption, Color face, Color depth,
        int radius, Vector2 position, Vector2 size, int fontSize, Color textColor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        // Root image is only a raycast target; face and depth are children.
        Image hitArea = go.GetComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);
        hitArea.raycastTarget = true;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Sprite sprite = Round(radius);
        Image depthImage = NewImage(go.transform, "Depth", sprite, size, depth);
        depthImage.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        Image faceImage = NewImage(go.transform, "Face", sprite, size, face);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = faceImage;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
        colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        Label(go.transform, "Label", caption, fontSize, textColor, TextAnchor.MiddleCenter,
            Vector2.zero, size - new Vector2(16f, 10f));
        return button;
    }

    static Image NewImage(Transform parent, string name, Sprite sprite, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return image;
    }

    // Recolours an existing skin button without rebuilding it.
    public static void Recolor(Button button, Color face, Color depth, Color textColor)
    {
        if (button == null) return;
        Transform faceTransform = button.transform.Find("Face");
        Transform depthTransform = button.transform.Find("Depth");
        Transform label = button.transform.Find("Label");
        if (faceTransform != null) faceTransform.GetComponent<Image>().color = face;
        if (depthTransform != null) depthTransform.GetComponent<Image>().color = depth;
        if (label != null) label.GetComponent<Text>().color = textColor;
    }
}
