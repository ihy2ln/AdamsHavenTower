using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Live-service chrome for the Tower HUD (Wuthering Waves / Genshin layout). When the angular sci-fantasy art pack
// (UI/Wuwa, from UiPrompts/BUTTON_PROMPTS_WUWA_FANTASY.md) is present, buttons and panels use it with Sprite Swap
// states; otherwise they fall back to flat procedural glass with a hairline rim.
// Callers still pass a tint (Teal = normal, Gold = active/primary, Alert = danger, violet = summon); TowerButtonFx
// maps whatever tint is on the Image to a flat style, so existing Refresh code that recolours buttons keeps working.
public static class TowerUiSkin
{
    public static readonly Color GlassFill = new Color(0.075f, 0.09f, 0.12f, 0.93f);
    public static readonly Color TextMain = new Color(0.93f, 0.91f, 0.86f);
    public static readonly Color TextDim = new Color(0.62f, 0.65f, 0.70f);
    public static readonly Color Accent = new Color(0.91f, 0.80f, 0.55f);          // Genshin gold
    public static readonly Color CreamFill = new Color(0.925f, 0.898f, 0.847f);
    public static readonly Color CreamText = new Color(0.21f, 0.23f, 0.29f);

    private const int Size = 64, Radius = 14;
    private static Sprite rounded, roundedRim, circle, ring, shade, diamond;

    public static Sprite Rounded { get { if (rounded == null) rounded = MakeRounded(false); return rounded; } }
    public static Sprite RoundedRim { get { if (roundedRim == null) roundedRim = MakeRounded(true); return roundedRim; } }
    public static Sprite Circle { get { if (circle == null) circle = MakeCircle(false); return circle; } }
    public static Sprite Ring { get { if (ring == null) ring = MakeCircle(true); return ring; } }
    public static Sprite Shade { get { if (shade == null) shade = MakeShade(); return shade; } }
    public static Sprite Diamond { get { if (diamond == null) diamond = MakeDiamond(); return diamond; } }

    // Kept for older callers.
    public static Sprite Body { get { return Rounded; } }
    public static Sprite Frame { get { return RoundedRim; } }
    public static Sprite PanelBody { get { return Rounded; } }

    // Line-art icons from the sleek UI set (icons_dock / icons_utility); null when missing.
    public static Sprite Icon(string sheet, string state)
    {
        return TowerSleekArt.Available ? TowerSleekArt.Get(sheet, state, Vector4.zero) : null;
    }

    // ---------------------------------------------------------------- sprites

    // Signed distance to a rounded square inside the texture, in pixels (negative inside).
    private static float Distance(int x, int y, float radius, float inset)
    {
        float half = Size * 0.5f - inset;
        float px = Mathf.Abs(x + 0.5f - Size * 0.5f) - (half - radius);
        float py = Mathf.Abs(y + 0.5f - Size * 0.5f) - (half - radius);
        float ox = Mathf.Max(px, 0), oy = Mathf.Max(py, 0);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(px, py), 0) - radius;
    }

    private static Sprite Finish(Texture2D texture, Color[] pixels, Vector4 border)
    {
        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100, 0,
            SpriteMeshType.FullRect, border);
    }

    private static Sprite MakeRounded(bool rimOnly)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = rimOnly ? "ui_rim" : "ui_rounded" };
        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Distance(x, y, Radius, 1f);
                float alpha = Mathf.Clamp01(0.5f - d);
                if (rimOnly) alpha *= Mathf.Clamp01(d + 2.0f);      // about a 1.5px hairline
                pixels[y * Size + x] = new Color(1, 1, 1, alpha);
            }
        int b = Radius + 2;
        return Finish(texture, pixels, new Vector4(b, b, b, b));
    }

    private static Sprite MakeCircle(bool ringOnly)
    {
        const int s = 128;
        var texture = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = ringOnly ? "ui_ring" : "ui_circle" };
        var pixels = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x + 0.5f - s * 0.5f, dy = y + 0.5f - s * 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) - (s * 0.5f - 1.5f);
                float alpha = Mathf.Clamp01(0.5f - d);
                if (ringOnly) alpha *= Mathf.Clamp01(d + 3.5f);
                pixels[y * s + x] = new Color(1, 1, 1, alpha);
            }
        return Finish(texture, pixels, Vector4.zero);
    }

    // Vertical fade, opaque at the top: the shade behind the top HUD row.
    private static Sprite MakeShade()
    {
        var texture = new Texture2D(4, 64, TextureFormat.RGBA32, false) { name = "ui_shade" };
        var pixels = new Color[4 * 64];
        for (int y = 0; y < 64; y++)
        {
            float t = y / 63f;
            float alpha = t * t * (3 - 2 * t);
            for (int x = 0; x < 4; x++) pixels[y * 4 + x] = new Color(1, 1, 1, alpha);
        }
        return Finish(texture, pixels, Vector4.zero);
    }

    private static Sprite MakeDiamond()
    {
        const int s = 32;
        var texture = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "ui_diamond" };
        var pixels = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Abs(x + 0.5f - s * 0.5f) + Mathf.Abs(y + 0.5f - s * 0.5f) - (s * 0.5f - 1.5f);
                pixels[y * s + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
            }
        return Finish(texture, pixels, Vector4.zero);
    }

    // ---------------------------------------------------------------- applying

    private static Image Rim(Image owner, Sprite sprite, bool sliced)
    {
        var existing = owner.transform.Find("Rim");
        if (existing != null) return existing.GetComponent<Image>();
        var go = new GameObject("Rim", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(owner.transform, false);
        go.transform.SetAsFirstSibling();
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.sprite = sprite; image.raycastTarget = false;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = owner.pixelsPerUnitMultiplier;
        return image;
    }

    private static float Multiplier(float height)
    { return Mathf.Max(1.6f, 2f * (Radius + 2) / Mathf.Max(8f, height) * 1.25f); }

    // ---------------------------------------------------------------- art pack (UI/Wuwa)

    private static readonly Vector4 WideBorder = new Vector4(70, 34, 70, 34);
    private static readonly Vector4 TabBorder = new Vector4(80, 30, 50, 30);
    private static readonly Vector4 FrameBorder = new Vector4(180, 180, 180, 180);
    private static readonly Vector4 HeaderBorder = new Vector4(140, 0, 110, 0);
    private static readonly Vector4 ChipBorder = new Vector4(320, 0, 110, 0);

    public static bool ArtReady { get { return TowerSleekArt.Available && TowerSleekArt.Has("btn_secondary_states", "normal"); } }

    // Which art sheet a button uses, from its name, size and whether it is an icon button.
    public static string FamilyOf(Image image, bool iconMode)
    {
        var size = image.rectTransform.rect.size;
        if (size.x < 1f || size.y < 1f) size = image.rectTransform.sizeDelta;
        bool square = size.y > 1f && size.x / size.y < 1.3f;
        string name = image.gameObject.name;
        if (name == "Dock heart") return "heart";
        if (iconMode) return "round";
        if (square && name.ToLowerInvariant().Contains("close")) return "close";
        if (name.StartsWith("Tab ") || name.StartsWith("Category ")) return "tab";
        return square ? "round" : "wide";
    }

    // Resource counter frame with a diamond icon socket on its left end.
    public static bool ApplyChip(Image image, float height)
    {
        if (!ArtReady) return false;
        var sprite = TowerSleekArt.Get("chip_resource_9slice", "chip", ChipBorder);
        if (sprite == null) return false;
        image.sprite = sprite; image.type = Image.Type.Sliced; image.color = Color.white;
        image.pixelsPerUnitMultiplier = TowerSleekArt.BodySize("chip_resource_9slice", "chip").y / Mathf.Max(8f, height);
        return true;
    }

    // Flare from the fx_flares sheet (whole canvas): star_glint, light_streak, ring_bloom, selection_sweep.
    public static Sprite Flare(string state) { return ArtReady ? TowerSleekArt.GetCanvas("fx_flares", state) : null; }

    // Gem badge from the round sheet: pip_alert, pip_ready, pip_new.
    public static Sprite Pip(string state) { return ArtReady ? TowerSleekArt.Get("btn_round_states", state, Vector4.zero) : null; }

    private static bool ApplyArtPanel(Image image, Vector2 size)
    {
        if (!ArtReady || size.y < 1f) return false;
        bool tall = size.y >= 110f;
        string sheet = tall ? "panel_frame_9slice" : "panel_header_9slice", state = tall ? "frame" : "header";
        var sprite = TowerSleekArt.Get(sheet, state, tall ? FrameBorder : HeaderBorder);
        if (sprite == null) return false;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = tall ? Mathf.Max(6f, 2.1f * 180f / Mathf.Min(size.x, size.y)) :
            TowerSleekArt.BodySize(sheet, state).y / size.y;
        image.color = Color.white;
        var shadow = image.gameObject.GetComponent<Shadow>();
        if (shadow != null) shadow.enabled = false;
        var rim = image.transform.Find("Rim");
        if (rim != null) rim.gameObject.SetActive(false);
        return true;
    }

    // Swaps a button to the art sheet for its family and tint kind. False when the art is missing.
    public static bool ApplyArt(TowerButtonFx fx, Image face, Button button, Text label, Kind kind)
    {
        if (!ArtReady || string.IsNullOrEmpty(fx.family)) return false;
        string sheet, n = "normal", p = "pressed", h = "selected", d = "disabled";
        Vector4 border = Vector4.zero;
        bool sliced = false;
        Color tint = Color.white, text = TextMain;
        switch (fx.family)
        {
            case "tab":
                sheet = "btn_tab_states"; sliced = true; border = TabBorder;
                n = kind == Kind.Primary ? "active" : "inactive"; h = n;
                text = kind == Kind.Primary ? new Color(1f, 0.91f, 0.70f) : new Color(0.80f, 0.82f, 0.86f);
                break;
            case "round":
                sheet = "btn_round_states";
                n = kind == Kind.Primary ? "active" : kind == Kind.Muted ? "disabled" : "normal"; h = n;
                if (kind == Kind.Danger) tint = new Color(1f, 0.74f, 0.70f);
                if (kind == Kind.Summon) tint = new Color(0.86f, 0.80f, 1f);
                break;
            case "heart":
                sheet = "btn_heart_states"; n = kind == Kind.Primary ? "ready" : "idle"; h = n;
                break;
            case "close":
                sheet = "btn_close_states";
                break;
            default:
                sliced = true; border = WideBorder;
                sheet = kind == Kind.Primary ? "btn_primary_states" : kind == Kind.Summon ? "btn_violet_states" :
                    kind == Kind.Danger ? "btn_danger_states" : "btn_secondary_states";
                if (kind == Kind.Muted) { tint = new Color(0.62f, 0.64f, 0.68f); text = TextDim; }
                if (kind == Kind.Primary) text = new Color(1f, 0.90f, 0.68f);
                if (kind == Kind.Danger) text = new Color(1f, 0.86f, 0.82f);
                if (kind == Kind.Summon) text = new Color(0.95f, 0.90f, 1f);
                break;
        }
        Sprite normal = TowerSleekArt.Get(sheet, n, border), pressed = TowerSleekArt.Get(sheet, p, border),
            hover = TowerSleekArt.Get(sheet, h, border), disabled = TowerSleekArt.Get(sheet, d, border);
        if (normal == null || pressed == null || hover == null || disabled == null) return false;
        face.sprite = normal;
        face.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        face.preserveAspect = false;
        if (sliced)
        {
            float height = face.rectTransform.rect.height;
            if (height < 1f) height = face.rectTransform.sizeDelta.y;
            face.pixelsPerUnitMultiplier = TowerSleekArt.BodySize(sheet, n).y / Mathf.Max(8f, height);
        }
        face.color = tint;
        if (button != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            { highlightedSprite = hover, pressedSprite = pressed, selectedSprite = normal, disabledSprite = disabled };
        }
        if (label != null)
        {
            if (fx.family == "close") label.enabled = false;
            else if (!fx.iconMode)
            {
                label.color = text;
                var shadow = label.GetComponent<Shadow>() ?? label.gameObject.AddComponent<Shadow>();
                shadow.enabled = true;
                shadow.effectColor = new Color(0, 0, 0, 0.7f);
                shadow.effectDistance = new Vector2(0, -1);
            }
        }
        if (fx.icon != null && fx.family == "heart") fx.icon.enabled = false;   // the emblem art has its own crystal
        else if (fx.icon != null)
            fx.icon.color = kind == Kind.Primary ? new Color(1f, 0.92f, 0.72f) :
                kind == Kind.Muted ? new Color(1, 1, 1, 0.45f) : Color.white;
        return true;
    }

    // A dark glass panel with a hairline rim, for docks, popups, cards and flyouts.
    public static void ApplyPanel(Image image, Color tint, bool framed = true)
    {
        var size = image.rectTransform.rect.size;
        if (size.y < 1f) size = image.rectTransform.sizeDelta;
        if (ApplyArtPanel(image, size)) return;
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = size.y > 1f && size.y < 40f ? Multiplier(size.y) : 1.2f;
        image.color = GlassFill;
        var shadow = image.gameObject.GetComponent<Shadow>();
        if (shadow != null) shadow.enabled = false;
        if (!framed) return;
        var rim = Rim(image, RoundedRim, true);
        rim.color = new Color(1, 1, 1, 0.13f);
    }

    // Turns a plain Image (already holding its tint) into a flat button face. Wide buttons are rounded
    // rectangles; nearly square ones become circles.
    public static void Apply(Image image, Color tint)
    {
        var size = image.rectTransform.rect.size;
        if (size.x < 1f || size.y < 1f) size = image.rectTransform.sizeDelta;
        bool round = size.y > 1f && size.x / size.y < 1.3f;
        image.sprite = round ? Circle : Rounded;
        image.type = round ? Image.Type.Simple : Image.Type.Sliced;
        image.preserveAspect = false;
        image.pixelsPerUnitMultiplier = round ? 1f : Multiplier(size.y);
        image.color = tint;
        image.raycastTarget = true;
        var shadow = image.gameObject.GetComponent<Shadow>();
        if (shadow != null) shadow.enabled = false;
        var oldFrame = image.transform.Find("Gold frame");
        if (oldFrame != null) oldFrame.gameObject.SetActive(false);
        var fx = image.gameObject.GetComponent<TowerButtonFx>() ?? image.gameObject.AddComponent<TowerButtonFx>();
        fx.frame = Rim(image, round ? Ring : RoundedRim, !round);
        fx.family = FamilyOf(image, fx.iconMode);
        fx.Restyle();
    }

    public static void StyleLabel(Text label)
    {
        label.fontStyle = FontStyle.Bold;
        var shadow = label.GetComponent<Shadow>();
        if (shadow != null) shadow.enabled = false;
        var fx = label.GetComponentInParent<TowerButtonFx>();
        if (fx != null) fx.Restyle();
    }

    // ---------------------------------------------------------------- tint mapping

    public enum Kind { Normal, Primary, Danger, Summon, Muted }

    public static Kind KindOf(Color tint)
    {
        float h, s, v;
        Color.RGBToHSV(tint, out h, out s, out v);
        if (s < 0.22f) return v < 0.45f ? Kind.Muted : Kind.Normal;
        if (h < 0.04f || h > 0.95f) return Kind.Danger;
        if (h >= 0.66f && h <= 0.92f) return Kind.Summon;
        if (h >= 0.05f && h <= 0.17f) return Kind.Primary;
        return Kind.Normal;
    }

    public static void Style(Kind kind, out Color fill, out Color text, out Color rim)
    {
        switch (kind)
        {
            case Kind.Primary: fill = CreamFill; text = CreamText; rim = new Color(1, 1, 1, 0); break;
            case Kind.Danger:
                fill = new Color(0.44f, 0.14f, 0.14f, 0.92f); text = new Color(1f, 0.88f, 0.84f);
                rim = new Color(1f, 0.55f, 0.48f, 0.45f); break;
            case Kind.Summon:
                fill = new Color(0.27f, 0.21f, 0.43f, 0.94f); text = new Color(0.96f, 0.93f, 1f);
                rim = new Color(0.82f, 0.72f, 1f, 0.45f); break;
            case Kind.Muted:
                fill = new Color(0.09f, 0.10f, 0.13f, 0.72f); text = TextDim; rim = new Color(1, 1, 1, 0.07f); break;
            default:
                fill = new Color(0.13f, 0.155f, 0.20f, 0.90f); text = TextMain; rim = new Color(1, 1, 1, 0.15f); break;
        }
    }
}

// Maps the Image's requested tint to the flat style and gives buttons a light hover/press response.
// Icon buttons (iconMode) keep a dark round face and show their state on the rim and icon instead.
public sealed class TowerButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    public Image frame;
    public Image icon;
    public bool iconMode;
    public string family;          // art sheet family: wide, tab, round, heart, close (TowerUiSkin.FamilyOf)
    private bool artMode;
    private Button button;
    private Image face;
    private Text label;
    private Color applied = new Color(-1, -1, -1, -1);
    private Color rimBase;
    private TowerUiSkin.Kind kind;
    private bool hover, pressed;
    private float scale = 1;

    public TowerUiSkin.Kind CurrentKind { get { return kind; } }

    private void Awake() { button = GetComponent<Button>(); face = GetComponent<Image>(); }
    private void OnEnable() { scale = 1; hover = pressed = false; transform.localScale = Vector3.one; }
    public void OnPointerEnter(PointerEventData eventData) { hover = true; }
    public void OnPointerExit(PointerEventData eventData) { hover = pressed = false; }
    public void OnPointerDown(PointerEventData eventData) { pressed = true; }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; }

    // Re-reads the tint on the next frame (call after changing the label or mode).
    public void Restyle() { applied = new Color(-1, -1, -1, -1); label = null; }

    private void LateUpdate()
    {
        if (face == null) face = GetComponent<Image>();
        if (button == null) button = GetComponent<Button>();
        if (face == null) return;
        if (face.color != applied)
        {
            kind = TowerUiSkin.KindOf(face.color);
            if (label == null) label = GetComponentInChildren<Text>(true);
            artMode = TowerUiSkin.ApplyArt(this, face, button, label, kind);
            if (artMode) applied = face.color;
        }
        if (!artMode && face.color != applied)
        {
            Color fill, text;
            TowerUiSkin.Style(iconMode ? TowerUiSkin.Kind.Normal : kind, out fill, out text, out rimBase);
            if (iconMode)
            {
                Color k, t2, r2;
                TowerUiSkin.Style(kind, out k, out t2, out r2);
                rimBase = kind == TowerUiSkin.Kind.Primary ? new Color(0.91f, 0.80f, 0.55f, 0.95f) :
                    kind == TowerUiSkin.Kind.Normal ? new Color(1, 1, 1, 0.18f) : new Color(r2.r, r2.g, r2.b, 0.9f);
                if (icon != null)
                    icon.color = kind == TowerUiSkin.Kind.Primary ? new Color(1f, 0.90f, 0.66f) :
                        kind == TowerUiSkin.Kind.Muted ? new Color(1, 1, 1, 0.45f) : Color.white;
                fill = new Color(0.07f, 0.085f, 0.11f, 0.82f);
            }
            face.color = fill;
            applied = fill;
            if (label == null) label = GetComponentInChildren<Text>(true);
            if (label != null && !iconMode) label.color = text;
        }
        bool live = button == null || button.interactable;
        float target = !live ? 1f : pressed ? 0.96f : hover ? 1.025f : 1f;
        scale = Mathf.Lerp(scale, target, 1 - Mathf.Exp(-22f * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(scale, scale, 1);
        if (artMode && label != null && !iconMode)   // disabled art buttons also dim their label
        {
            var c = label.color;
            float alpha = live ? 1f : 0.45f;
            if (!Mathf.Approximately(c.a, alpha)) label.color = new Color(c.r, c.g, c.b, alpha);
        }
        if (frame == null) return;
        if (frame.gameObject.activeSelf == artMode) frame.gameObject.SetActive(!artMode);
        if (artMode) return;
        frame.enabled = face.enabled;
        float lift = live && (hover || pressed) ? 0.22f : 0f;
        frame.color = new Color(rimBase.r, rimBase.g, rimBase.b, Mathf.Clamp01(rimBase.a + lift) * (live ? 1f : 0.5f));
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
