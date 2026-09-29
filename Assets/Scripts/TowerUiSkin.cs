using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Procedural "premium" button chrome for the Tower HUD: a glossy beveled body that takes its
// colour from Image.color, a gold metal frame, a drop shadow and springy hover/press motion.
public static class TowerUiSkin
{
    private const int Size = 64, Border = 15;
    private static Sprite body, frame, panel;

    public static Sprite Body { get { if (body == null) body = MakeBody(); return body; } }
    public static Sprite Frame { get { if (frame == null) frame = MakeFrame(); return frame; } }
    public static Sprite PanelBody { get { if (panel == null) panel = MakePanel(); return panel; } }

    private static Sprite MakePanel()
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "tower_panel_body" };
        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Distance(x, y, 13f, 1f);
                float alpha = Mathf.Clamp01(0.5f - d) * 0.97f;
                float v = (y + 0.5f) / Size;
                float light = 0.10f + 0.16f * v * v;           // deep at the base, a little lift at the top
                float depth = -d;
                if (depth < 2f) light += 0.10f;                 // faint inner rim
                pixels[y * Size + x] = new Color(light, light, light, alpha);
            }
        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100, 0,
            SpriteMeshType.FullRect, new Vector4(Border, Border, Border, Border));
    }

    // A dark glass panel with a thin gold frame, for docks, popups and flyouts.
    public static void ApplyPanel(Image image, Color tint, bool framed = true)
    {
        image.sprite = PanelBody;
        image.type = Image.Type.Sliced;
        image.color = tint;
        var shadow = image.gameObject.GetComponent<Shadow>() ?? image.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.5f);
        shadow.effectDistance = new Vector2(0, -4);
        if (!framed || image.transform.Find("Gold frame") != null) return;
        var go = new GameObject("Gold frame", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(image.transform, false);
        go.transform.SetAsFirstSibling();
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var border = go.GetComponent<Image>();
        border.sprite = Frame; border.type = Image.Type.Sliced; border.raycastTarget = false;
        border.color = new Color(1, 1, 1, 0.85f);
    }

    // Signed distance to a rounded square inside the texture, in pixels (negative inside).
    private static float Distance(int x, int y, float radius, float inset)
    {
        float half = Size * 0.5f - inset;
        float px = Mathf.Abs(x + 0.5f - Size * 0.5f) - (half - radius);
        float py = Mathf.Abs(y + 0.5f - Size * 0.5f) - (half - radius);
        float ox = Mathf.Max(px, 0), oy = Mathf.Max(py, 0);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(px, py), 0) - radius;
    }

    private static Sprite MakeBody()
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "tower_button_body" };
        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Distance(x, y, 13f, 1f);
                float alpha = Mathf.Clamp01(0.5f - d);
                float v = (y + 0.5f) / Size;                  // 0 bottom .. 1 top
                float light = v > 0.5f ? 0.76f + (v - 0.5f) * 0.46f : 0.40f + v * 0.62f;
                float depth = -d;                              // pixels in from the edge
                if (depth < 2.2f) light = v > 0.5f ? 1.02f : 0.30f;       // bevel: lit top edge, dark base edge
                else if (depth < 5f) light *= 0.90f + 0.10f * Mathf.InverseLerp(2.2f, 5f, depth);
                if (v > 0.5f && v < 0.53f) light += 0.10f;     // crisp gloss seam
                pixels[y * Size + x] = new Color(light, light, light, alpha);
            }
        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100, 0,
            SpriteMeshType.FullRect, new Vector4(Border, Border, Border, Border));
    }

    private static Sprite MakeFrame()
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "tower_button_frame" };
        var pixels = new Color[Size * Size];
        Color lightGold = new Color(1f, 0.92f, 0.64f), midGold = new Color(0.86f, 0.66f, 0.30f),
            darkGold = new Color(0.42f, 0.28f, 0.10f);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float outer = Distance(x, y, 13f, 0.4f);
                float inner = Distance(x, y, 13f, 3.2f);
                float ring = Mathf.Clamp01(0.5f - outer) * Mathf.Clamp01(inner + 0.5f);
                float dark = Mathf.Clamp01(0.5f - Distance(x, y, 13f, 3.2f)) *
                    Mathf.Clamp01(Distance(x, y, 13f, 4.4f) + 0.5f) * 0.55f;
                float t = Mathf.Clamp01(((float)x / Size + (1f - (y + 0.5f) / Size)) * 0.5f);
                Color metal = t < 0.5f ? Color.Lerp(lightGold, midGold, t * 2) : Color.Lerp(midGold, darkGold, (t - 0.5f) * 2);
                Color pixel = new Color(metal.r, metal.g, metal.b, ring);
                if (dark > ring) pixel = new Color(0.05f, 0.03f, 0.01f, dark);
                pixels[y * Size + x] = pixel;
            }
        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100, 0,
            SpriteMeshType.FullRect, new Vector4(Border, Border, Border, Border));
    }

    // Turns a plain Image (already holding its tint) into a skinned button face.
    public static void Apply(Image image, Color tint)
    {
        image.sprite = Body;
        image.type = Image.Type.Sliced;
        image.color = tint;
        image.raycastTarget = true;
        var shadow = image.gameObject.GetComponent<Shadow>() ?? image.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.55f);
        shadow.effectDistance = new Vector2(0, -3);
        var fx = image.gameObject.GetComponent<TowerButtonFx>() ?? image.gameObject.AddComponent<TowerButtonFx>();
        if (fx.frame != null) return;
        var go = new GameObject("Gold frame", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(image.transform, false);
        go.transform.SetAsFirstSibling();
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var border = go.GetComponent<Image>();
        border.sprite = Frame; border.type = Image.Type.Sliced; border.raycastTarget = false;
        fx.frame = border;
    }

    public static void StyleLabel(Text label)
    {
        label.fontStyle = FontStyle.Bold;
        var shadow = label.GetComponent<Shadow>() ?? label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.85f);
        shadow.effectDistance = new Vector2(1, -1.5f);
    }
}

public sealed class TowerButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    public Image frame;
    private Button button;
    private bool hover, pressed;
    private float scale = 1;

    private void Awake() { button = GetComponent<Button>(); }
    private void OnEnable() { scale = 1; hover = pressed = false; transform.localScale = Vector3.one; }
    public void OnPointerEnter(PointerEventData eventData) { hover = true; }
    public void OnPointerExit(PointerEventData eventData) { hover = pressed = false; }
    public void OnPointerDown(PointerEventData eventData) { pressed = true; }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; }

    private void Update()
    {
        bool live = button == null || button.interactable;
        float target = !live ? 1f : pressed ? 0.95f : hover ? 1.045f : 1f;
        scale = Mathf.Lerp(scale, target, 1 - Mathf.Exp(-20f * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(scale, scale, 1);
        if (frame == null) return;
        var face = GetComponent<Image>();
        frame.enabled = face != null && face.enabled;
        frame.color = !live ? new Color(0.45f, 0.45f, 0.45f, 0.7f) :
            hover || pressed ? Color.white : new Color(0.9f, 0.88f, 0.82f);
    }
}

// Tap runs the button's click; holding it for a moment opens a flyout of related options instead.
public sealed class TowerHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float holdSeconds = 0.38f;
    public System.Action onHold;
    public RectTransform progress;
    public float fullWidth = 100;
    private bool down, fired, suppress;
    private float held;

    // Called by the click handler: true means the press already opened the flyout, so skip the tap.
    public bool Consume() { bool value = suppress; suppress = false; return value; }

    public void OnPointerDown(PointerEventData eventData) { down = true; fired = false; suppress = false; held = 0; }
    public void OnPointerUp(PointerEventData eventData) { down = false; ShowProgress(0); }
    public void OnPointerExit(PointerEventData eventData) { down = false; ShowProgress(0); }
    private void OnDisable() { down = false; ShowProgress(0); }

    private void ShowProgress(float t)
    {
        if (progress != null) progress.sizeDelta = new Vector2(fullWidth * Mathf.Clamp01(t), progress.sizeDelta.y);
    }

    private void Update()
    {
        if (!down || fired) return;
        held += Time.unscaledDeltaTime;
        ShowProgress(held / holdSeconds);
        if (held < holdSeconds) return;
        fired = true; suppress = true;
        ShowProgress(0);
        if (onHold != null) onHold();
    }
}
