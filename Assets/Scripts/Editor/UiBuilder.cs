using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ayudantes para crear UI (uGUI con texto Legacy) por código. Los usan los constructores
/// de la escena del menú y de la pista, así ambas pantallas comparten el mismo estilo.
/// </summary>
public static class UiBuilder
{
    public static readonly Color White = Color.white;
    public static readonly Color Accent = new Color(1f, 0.55f, 0.1f);
    public static readonly Color Gold = new Color(1f, 0.85f, 0.2f);
    public static readonly Color ButtonIdle = new Color(0.13f, 0.15f, 0.19f, 0.92f);
    public static readonly Color ButtonPressed = new Color(0.8f, 0.42f, 0.06f, 1f);

    public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    /// <summary>Canvas a pantalla completa que escala desde 1920×1080.</summary>
    public static Canvas Canvas(string name, bool interactive)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        // Solo los menús con botones necesitan recibir clics.
        if (interactive) go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static GameObject UIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    public static RectTransform Stretch(GameObject go)
    {
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static RectTransform Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = UIObject(name, parent);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    /// <summary>Fondo a pantalla completa con una textura (por ejemplo, un degradado).</summary>
    public static RawImage Background(string name, Transform parent, Texture texture, Color tint)
    {
        GameObject go = UIObject(name, parent);
        Stretch(go);
        RawImage image = go.AddComponent<RawImage>();
        image.texture = texture;
        image.color = tint;
        image.raycastTarget = false;
        return image;
    }

    public static Text Label(string name, Transform parent, Font font, string content, int size, Vector2 anchor,
        Vector2 position, Color color, FontStyle style, TextAnchor alignment, bool outline, Vector2? boxSize = null)
    {
        GameObject go = UIObject(name, parent);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = boxSize ?? new Vector2(1400f, size * 1.4f);

        Text text = go.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.supportRichText = true;
        text.lineSpacing = 1.15f;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        if (outline)
        {
            // Borde oscuro para que el texto se lea sobre cualquier fondo.
            Outline border = go.AddComponent<Outline>();
            border.effectColor = new Color(0f, 0f, 0f, 0.75f);
            border.effectDistance = new Vector2(2f, -2f);
        }
        return text;
    }

    /// <summary>
    /// Botón rectangular con texto. Se ve oscuro en reposo y naranja al pasar el mouse
    /// o al seleccionarlo con teclado/mando, para que siempre se sepa cuál está elegido.
    /// </summary>
    public static Button Button(string name, Transform parent, Font font, string text, int fontSize, Vector2 position, Vector2 size)
    {
        GameObject go = UIObject(name, parent);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = go.AddComponent<Image>();
        image.color = Color.white; // El color real lo pone el ColorBlock del botón.

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonIdle;
        colors.highlightedColor = Accent;
        colors.selectedColor = Accent;
        colors.pressedColor = ButtonPressed;
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Label("Texto", go.transform, font, text, fontSize, new Vector2(0.5f, 0.5f), Vector2.zero, White, FontStyle.Bold, TextAnchor.MiddleCenter, false, size);
        return button;
    }
}
