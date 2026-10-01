using System;
using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Guild expedition screens: region map -> plan -> forest map -> themed dungeon crawl under fog of war.
// Owns its own canvas while the Tower is hidden; all game rules live in TowerRules.
public sealed class TowerExpeditionUi : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.035f, 0.055f, 0.085f, 0.94f);
    private static readonly Color Panel = new Color(0.08f, 0.12f, 0.16f, 0.95f);
    private static readonly Color Glass = new Color(0.62f, 0.78f, 1f, 1f);
    private static readonly Color Gold = new Color(0.95f, 0.77f, 0.43f);
    private static readonly Color Cream = new Color(0.95f, 0.92f, 0.82f);
    private static readonly Color Teal = new Color(0.22f, 0.55f, 0.64f);
    private static readonly Color Alert = new Color(0.86f, 0.30f, 0.24f);
    private static readonly Color Moss = new Color(0.35f, 0.62f, 0.38f);
    private const string Root = "AdamsHaven/Expedition/";
    private const float Side = 320, Top = 52, CellPx = 48;
    private static readonly Color FogTint = new Color(0.025f, 0.04f, 0.065f, 1);

    private enum View { Region, Map, Plan, Forest, Dungeon }

    private AdamsHavenPrototype tower;
    private TowerRules R { get { return tower.Rules; } }
    private Canvas canvas;
    private RectTransform root, content;
    private Font font;
    private View view;
    private int lastWidth, lastHeight;
    private Text toast;
    private float toastTimer;

    private string selectedRegion = "";
    private string selectedLayout = "";
    private string selectedNode = "";
    // forest walk
    private RectTransform walkMap;
    private List<Vector2> walkPts = new List<Vector2>();      // normalised on the plate
    private List<Vector2> walkPx = new List<Vector2>();       // pixels on the map frame
    private readonly List<RectTransform> walkFigures = new List<RectTransform>();
    private readonly List<RectTransform> walkShadows = new List<RectTransform>();
    private string walkFrom = "", walkTo = "";
    private float walkDist, walkLen, walkPrint, fadeClock, walkCommitAt;
    private bool walking, fading, fadingIn, printLeft, walkCommitted;
    private Image fadeCurtain;
    private TowerMapMask walkMask;
    private bool walkMaskMirror;
    // A walk stopped by an event: where the party stands until it is settled, then the rest of the way.
    private List<Vector2> haltPts;
    private float haltAt;               // 0..1 along haltPts
    private string haltLayout = "", haltFrom = "", haltTo = "";
    private float walkPace = 1;
    private Texture2D trailTex;         // the worn roads, redrawn with the map
    private GameObject inspectCard;
    private readonly List<string> planParty = new List<string>();
    private int planRations, planTonics, planFirewood;
    private bool lootOpen;
    private int dismissedAt = -1;

    // dungeon board
    private RectTransform viewport, board, token;
    private Image[] fogCells;
    private readonly List<GameObject> roomIcons = new List<GameObject>();
    private string boardKey = "";
    private float zoom = 1;
    private bool pointerDown, dragged;
    private Vector2 pointerStart, lastPointer;
    private float lastPinch;
    private RectTransform roomPanel, endPanel;
    private readonly List<int> travelPath = new List<int>();
    private readonly List<GameObject> routeMarks = new List<GameObject>();
    private readonly List<RectTransform> partyFigures = new List<RectTransform>();
    private readonly List<Vector2> partyOffsets = new List<Vector2>();
    private int travelIndex;
    private float travelClock;
    private Action commitDungeonStep;
    private bool Travelling { get { return travelIndex < travelPath.Count; } }
    private Camera InputCamera { get { return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera; } }
    private TowerDungeonIllustration dungeonIllustration;

    private static Sprite disc, ring, white;

    // ---------------------------------------------------------------- lifecycle

    public void Begin(AdamsHavenPrototype controller)
    {
        tower = controller;
        commitDungeonStep = tower.SaveExpedition;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasObject = new GameObject("Expedition Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = Screen.width / (float)Screen.height < 16f / 9f ? 0f : 1f;
        root = canvasObject.GetComponent<RectTransform>();
        if (EventSystem.current == null)
        {
            var events = new GameObject("Expedition Event System", typeof(EventSystem));
            events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
        }
        var run = R.Run;
        if (run != null) { selectedRegion = run.region; Go(run.dungeonPoi.Length > 0 ? View.Dungeon : View.Forest); }
        else Go(View.Region);
    }

    public void Shutdown()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        Destroy(this);
    }

    private void Go(View next)
    {
        view = next;
        lootOpen = false;
        Rebuild();
    }

    private void Rebuild()
    {
        CancelTravel();
        if (content != null) Destroy(content.gameObject);
        boardKey = "";
        roomPanel = endPanel = null;
        Canvas.ForceUpdateCanvases();
        lastWidth = Screen.width; lastHeight = Screen.height;
        content = Stretch("Screen", root, Ink).rectTransform;
        content.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 1);
        switch (view)
        {
            case View.Region: BuildRegion(); break;
            case View.Map: BuildMapChoice(); break;
            case View.Plan: BuildPlan(); break;
            case View.Forest: BuildForest(); break;
            case View.Dungeon: BuildDungeon(); break;
        }
        var toastBox = Box("Toast", content, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-Side / 2, -Top - 8),
            new Vector2(640, 34), new Color(0.03f, 0.05f, 0.08f, 0.88f));
        toastBox.raycastTarget = false;
        toast = TextAt(toastBox.transform, "Toast text", "", 10, 0, 620, 34, 15, Gold, TextAnchor.MiddleCenter);
        toastBox.gameObject.SetActive(false);
    }

    private void Say(string text)
    {
        if (string.IsNullOrEmpty(text) || toast == null) return;
        toast.text = text;
        toast.transform.parent.gameObject.SetActive(true);
        toast.transform.parent.SetAsLastSibling();
        toastTimer = 3.5f;
    }

    // Runs a rules call, reports an error, saves, and redraws.
    private void Act(string error, bool rebuild = true)
    {
        if (error != null) Say(error);
        else tower.SaveExpedition();
        if (R.Run == null && view != View.Region && view != View.Map && view != View.Plan) { ShowEnd(); return; }
        if (view == View.Dungeon && R.Run.dungeonPoi.Length == 0)
        {
            string last = R.State.log.Count > 0 ? R.State.log[R.State.log.Count - 1] : "";
            Go(View.Forest);
            Say(last);
            return;
        }
        if (rebuild) { if (view == View.Dungeon) RefreshDungeon(); else Rebuild(); }
    }

    private void Update()
    {
        if (tower == null || canvas == null || !canvas.gameObject.activeSelf) return;
        if ((Screen.width != lastWidth || Screen.height != lastHeight) && !walking && !fading && !fadingIn) Rebuild();
        if (toastTimer > 0)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0 && toast != null) toast.transform.parent.gameObject.SetActive(false);
        }
        if (view == View.Forest && (walking || fading || fadingIn)) UpdateWalk();
        if (view == View.Dungeon && R != null && R.Run != null && R.Dungeon != null && board != null && viewport != null)
        {
            UpdateTravel();
            DungeonInput();
        }
    }

    private void OnDisable() { CancelTravel(); }

    // ---------------------------------------------------------------- widgets

    private static Sprite White { get { if (white == null) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f)); return white; } }

    private static Sprite puff;
    // Soft cloud of fog: dense in the middle, fading out, with a little lumpy noise.
    private static Sprite Puff
    {
        get
        {
            if (puff != null) return puff;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float lump = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.35f;
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01((1 - d - lump * 0.5f) * 1.8f));
                }
            tex.SetPixels(px); tex.Apply();
            puff = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return puff;
        }
    }

    private static Sprite Disc(bool hollow)
    {
        if (hollow ? ring != null : disc != null) return hollow ? ring : disc;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = hollow ? Mathf.Clamp01(1 - Mathf.Abs(d - 0.86f) / 0.09f) : Mathf.Clamp01((1 - d) / 0.06f);
                px[y * size + x] = new Color(1, 1, 1, a);
            }
        tex.SetPixels(px); tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        if (hollow) ring = sprite; else disc = sprite;
        return sprite;
    }

    private static Image Stretch(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Image Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    // Top-left pixel placement inside a parent, like TowerHud.
    private Image At(string name, Transform parent, float x, float y, float w, float h, Color color)
    { return Box(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h), color); }

    private Text TextAt(Transform parent, string name, string value, float x, float y, float width, float height,
        int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        var label = go.GetComponent<Text>();
        label.font = font; label.fontSize = size; label.color = color; label.alignment = align;
        label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
        label.text = value; label.raycastTarget = false;
        return label;
    }

    private Button ButtonAt(Transform parent, string name, string value, float x, float y, float width, float height,
        Action action, Color color, int fontSize = 15, bool interactable = true)
    {
        var image = At(name, parent, x, y, width, height, color);
        var button = image.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1.16f, 1.12f, 1.02f);
        colors.pressedColor = new Color(0.78f, 0.76f, 0.72f);
        colors.disabledColor = new Color(0.52f, 0.54f, 0.56f, 0.70f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        TowerUiSkin.Apply(image, color);
        TowerUiSkin.StyleLabel(TextAt(image.transform, name + " label", value, 4, 0, width - 8, height, fontSize, Cream, TextAnchor.MiddleCenter));
        if (action != null) button.onClick.AddListener(() => action());
        button.interactable = interactable;
        return button;
    }

    private Image PanelAt(string name, Transform parent, float x, float y, float w, float h)
    {
        var panel = At(name, parent, x, y, w, h, Panel);
        TowerUiSkin.ApplyPanel(panel, Glass, true);
        return panel;
    }

    private RectTransform TopBar(string title, string back, Action backAction)
    {
        var bar = Box("Top bar", content, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(root.rect.width, Top),
            new Color(0.02f, 0.03f, 0.05f, 0.92f)).rectTransform;
        TextAt(bar, "Title", title, 16, 0, root.rect.width - 260, Top, 20, Gold);
        if (back != null) ButtonAt(bar, "Back", back, root.rect.width - 222, 7, 210, 38, backAction, Alert, 14);
        return bar;
    }

    // Fits a picture of the given aspect into an area and returns the frame; children use normalized anchors.
    private RectTransform MapFrame(Texture texture, float areaX, float areaY, float areaW, float areaH, Color tint)
    {
        float aspect = texture == null ? 16f / 9f : texture.width / (float)texture.height;
        float w = areaW, h = areaW / aspect;
        if (h > areaH) { h = areaH; w = h * aspect; }
        var go = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(content, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(areaX + (areaW - w) / 2, -(areaY + (areaH - h) / 2));
        rect.sizeDelta = new Vector2(w, h);
        var raw = go.GetComponent<RawImage>();
        raw.texture = texture; raw.color = tint;
        return rect;
    }

    private static Vector2 OnMap(RectTransform map, float x, float y) { return new Vector2(x * map.rect.width, -y * map.rect.height); }

    private static void Line(RectTransform parent, Vector2 a, Vector2 b, float width, Color color)
    {
        var image = new GameObject("Trail", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.sprite = White; image.color = color; image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 0.5f);
        rect.anchoredPosition = a;
        rect.sizeDelta = new Vector2(Vector2.Distance(a, b), width);
        rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
    }

    private Image Badge(RectTransform parent, Vector2 at, float size, Color color, Sprite icon, Action tap)
    {
        var badge = Box("Badge", parent, new Vector2(0, 1), new Vector2(0.5f, 0.5f), at, new Vector2(size, size), color);
        badge.sprite = Disc(false);
        var edge = Box("Edge", badge.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(size, size), new Color(0.1f, 0.07f, 0.04f, 0.9f));
        edge.sprite = Disc(true); edge.raycastTarget = false;
        if (icon != null)
        {
            var glyph = Box("Icon", badge.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(size * 0.62f, size * 0.62f), Color.white);
            glyph.sprite = icon; glyph.preserveAspect = true; glyph.raycastTarget = false;
        }
        if (tap != null) badge.gameObject.AddComponent<Button>().onClick.AddListener(() => tap());
        else badge.raycastTarget = false;
        return badge;
    }

    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private static Sprite Load(string path)
    {
        Sprite sprite;
        if (sprites.TryGetValue(path, out sprite)) return sprite;
        var tex = Resources.Load<Texture2D>(path);
        sprite = tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        sprites[path] = sprite;
        return sprite;
    }

    private static Sprite Icon(string kind)
    {
        switch (kind)
        {
            case "combat": case "enemy": return Load(Root + "Icons/enemy");
            case "elite": case "landmark": return Load(Root + "Icons/elite");
            case "lair": case "boss": return Load(Root + "Icons/boss");
            case "treasure": return Load(Root + "Icons/treasure");
            case "camp": case "rest": return Load(Root + "Icons/rest");
            case "merchant": return Load(Root + "Icons/merchant");
            default: return Load(Root + "Icons/unknown");
        }
    }

    private static string FighterName(string id)
    {
        switch (id)
        {
            case "kaela": return "Kaela"; case "ghislaine": return "Ghislaine"; case "elara": return "Elara";
            case "helda": return "Helda"; case "daisy": return "Daisy"; case "clarity": return "Clarity";
            default: return id;
        }
    }

    private static string ThemeName(string theme)
    {
        switch (theme)
        {
            case "cave": return "Root Cave"; case "marsh": return "Fern Marsh"; case "crystal": return "Crystal Grotto";
            case "briar": return "Briar Thicket"; case "keep": return "Watchpost Keep";
            case "mine": return "Abandoned Mine"; case "blight": return "Blighted Ground"; case "heartwood": return "Heartwood Hollow";
            default: return "Forest Ruin";
        }
    }

    // ---------------------------------------------------------------- region map

    private void BuildRegion()
    {
        var run = R.Run;
        float width = root.rect.width, height = root.rect.height;
        var map = MapFrame(Resources.Load<Texture2D>(Root + "Maps/region"), 0, Top, width - Side - 24, height - Top, Color.white);
        foreach (var region in TowerRules.Regions)
            foreach (var need in region.requires)
            {
                var from = TowerRules.Region(need);
                bool open = R.RegionUnlocked(region.id);
                Line(map, OnMap(map, from.x, from.y), OnMap(map, region.x, region.y), open ? 4 : 2,
                    open ? new Color(1f, 0.85f, 0.5f, 0.75f) : new Color(0.7f, 0.7f, 0.75f, 0.35f));
            }
        var home = OnMap(map, 0.12f, 0.66f);
        Badge(map, home, 40, new Color(0.95f, 0.8f, 0.45f), null, null);
        Label(map, home + new Vector2(0, -30), "SILVERBROOK", 14, Cream);
        foreach (var region in TowerRules.Regions)
        {
            bool unlocked = R.RegionUnlocked(region.id), conquered = R.RegionConquered(region.id);
            Color color = conquered ? Gold : unlocked ? new Color(0.3f, 0.75f, 0.8f) : new Color(0.35f, 0.35f, 0.4f, 0.85f);
            var at = OnMap(map, region.x, region.y);
            string id = region.id;
            var badge = Badge(map, at, selectedRegion == id ? 58 : 48, color, Icon(conquered ? "treasure" : unlocked ? "combat" : "unknown"),
                () => { selectedRegion = id; Rebuild(); });
            if (run != null && run.region == id)
            {
                var halo = Box("Here", badge.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78, 78), Gold);
                halo.sprite = Disc(true); halo.raycastTarget = false;
            }
            Label(map, at + new Vector2(0, -38), region.name.ToUpperInvariant() + (unlocked ? "" : "  (LOCKED)"), 14,
                unlocked ? Cream : new Color(0.75f, 0.75f, 0.78f));
        }
        TopBar("SILVERBROOK FOREST  •  GUILD EXPEDITIONS", "BACK TO TOWER", () => tower.CloseExpedition(null));

        var panel = PanelAt("Region panel", content, width - Side - 12, Top + 12, Side, 330);
        var region0 = TowerRules.Region(selectedRegion);
        if (region0 == null)
        {
            TextAt(panel.transform, "Hint", run != null ?
                "An expedition is under way in " + TowerRules.Region(run.region).name + "." :
                "Choose a region. Start near Silverbrook; conquering a region's lair opens the lands beyond it.",
                18, 18, Side - 36, 120, 16, Cream);
            if (run != null) ButtonAt(panel.transform, "Resume", "RESUME EXPEDITION", 18, 150, Side - 36, 48, Resume, Teal, 17);
            return;
        }
        bool isUnlocked = R.RegionUnlocked(region0.id);
        TextAt(panel.transform, "Name", region0.name.ToUpperInvariant(), 18, 14, Side - 36, 30, 20, Gold);
        TextAt(panel.transform, "Blurb", region0.blurb, 18, 46, Side - 36, 46, 15, Cream);
        TextAt(panel.transform, "Stats", "Danger " + region0.depth + "   •   Loot x" + region0.reward.ToString("0.0") +
            "\n" + (R.RegionConquered(region0.id) ? "CONQUERED" : isUnlocked ? "Open to explore" : "Locked: conquer " +
            string.Join(" and ", Array.ConvertAll(region0.requires, n => TowerRules.Region(n).name))), 18, 96, Side - 36, 60, 15,
            isUnlocked ? Cream : new Color(1f, 0.7f, 0.6f));
        TextAt(panel.transform, "Rule", TowerRules.SilverwoodDepth(region0.id) > 0 ?
            "Choose one of this depth's three maps. Clear its lair to conquer the region." :
            "Each run the forest shifts into one of 15 maps. Clear its lair to conquer the region.",
            18, 160, Side - 36, 60, 13, new Color(0.8f, 0.85f, 0.9f));
        if (run != null)
            ButtonAt(panel.transform, "Resume", "RESUME EXPEDITION", 18, 250, Side - 36, 50, Resume, Teal, 17);
        else
            ButtonAt(panel.transform, "Plan", "PLAN EXPEDITION", 18, 250, Side - 36, 50, OpenPlan, Teal, 17, isUnlocked);
    }

    private void Label(RectTransform parent, Vector2 at, string text, int size, Color color)
    {
        var shadow = TextAt(parent, "Label shadow", text, at.x - 119, -at.y + 1, 240, 22, size, new Color(0, 0, 0, 0.9f), TextAnchor.MiddleCenter);
        shadow.fontStyle = FontStyle.Bold;
        var label = TextAt(parent, "Label", text, at.x - 120, -at.y, 240, 22, size, color, TextAnchor.MiddleCenter);
        label.fontStyle = FontStyle.Bold;
    }

    private void Resume()
    {
        var run = R.Run;
        if (run == null) return;
        Go(run.dungeonPoi.Length > 0 ? View.Dungeon : View.Forest);
    }

    // ---------------------------------------------------------------- map choice

    private void BuildMapChoice()
    {
        float width = root.rect.width, height = root.rect.height;
        MapFrame(Resources.Load<Texture2D>(Root + "Maps/region"), 0, Top, width, height - Top, new Color(0.3f, 0.33f, 0.38f));
        var region = TowerRules.Region(selectedRegion);
        TopBar("CHOOSE MAP  •  " + region.name.ToUpperInvariant(), "BACK TO MAP", () => Go(View.Region));
        int depth = TowerRules.SilverwoodDepth(selectedRegion);
        var ids = TowerForestLayouts.SilverwoodDepthIds(depth);
        float pw = Mathf.Min(1120, width - 40), px = (width - pw) / 2, ph = height - Top - 32;
        var panel = PanelAt("Map choice panel", content, px, Top + 16, pw, ph).transform;
        TextAt(panel, "Title", "Pick where to go. Each map has its own camp, points of interest and lair.", 20, 14, pw - 40, 28, 18, Gold);
        float gap = 16, cw = (pw - 40 - gap * (ids.Length - 1)) / Mathf.Max(1, ids.Length), ch = ph - 150;
        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i];
            var layout = TowerForestLayouts.Get(id);
            float cx = 20 + i * (cw + gap);
            var card = At("Map " + id, panel, cx, 56, cw, ch, new Color(0.08f, 0.11f, 0.15f, 0.97f));
            var thumb = layout == null ? null : Resources.Load<Texture2D>(layout.backdrop);
            float picH = Mathf.Min(ch - 112, (cw - 16) * 9f / 16f * 1.05f);
            if (thumb != null)
            {
                var pic = new GameObject("Plate", typeof(RectTransform), typeof(RawImage));
                pic.transform.SetParent(card.transform, false);
                var pr = pic.GetComponent<RectTransform>();
                pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0, 1);
                pr.anchoredPosition = new Vector2(8, -8); pr.sizeDelta = new Vector2(cw - 16, picH);
                var raw = pic.GetComponent<RawImage>();
                raw.texture = thumb; raw.raycastTarget = false;
                float uvW = Mathf.Clamp01(((cw - 16) / picH) / (thumb.width / (float)thumb.height));
                raw.uvRect = new Rect((1f - uvW) * 0.5f, 0, uvW, 1);
            }
            string name = layout == null ? TowerForestLayouts.Pretty(id) : layout.name;
            TextAt(card.transform, "Name", name.ToUpperInvariant(), 10, picH + 14, cw - 20, 28, 20, Gold, TextAnchor.MiddleCenter);
            int pois = layout == null ? 0 : layout.nodes.FindAll(n => n.kind != "camp" && n.kind != "lair").Count;
            TextAt(card.transform, "Info", ThemeName(layout == null ? "ruin" : layout.theme) + "  •  " + pois + " places  •  1 lair",
                10, picH + 44, cw - 20, 22, 14, Cream, TextAnchor.MiddleCenter);
            ButtonAt(card.transform, "Pick", "GO HERE", 16, ch - 56, cw - 32, 44, () => { selectedLayout = id; OpenParty(); }, Teal, 17);
        }
        ButtonAt(panel, "Random", "RANDOM MAP", pw - 280, ph - 66, 260, 46, () => { selectedLayout = ""; OpenParty(); }, Moss, 17);
        TextAt(panel, "Note", "Random lets the forest shift decide.", 20, ph - 60, pw - 320, 30, 14, new Color(0.8f, 0.85f, 0.9f));
    }

    // ---------------------------------------------------------------- plan

    // Silverwood regions let the player choose one of the depth's maps first; Silverbrook regions keep the random shift.
    private void OpenPlan()
    {
        selectedLayout = "";
        if (TowerRules.SilverwoodDepth(selectedRegion) > 0) Go(View.Map);
        else OpenParty();
    }

    private void OpenParty()
    {
        planParty.Clear();
        foreach (var id in TowerRules.Fighters)
            if (planParty.Count < 3 && R.HeroResident(id) != null) planParty.Add(id);
        foreach (var id in TowerRules.Fighters)
            if (planParty.Count < 3 && !planParty.Contains(id)) planParty.Add(id);
        planRations = Mathf.Min(6, R.MaxRations);
        planTonics = Mathf.Min(1, R.State.tonics);
        planFirewood = Mathf.Min(2, R.MaxFirewood);
        Go(View.Plan);
    }

    private void BuildPlan()
    {
        float width = root.rect.width, height = root.rect.height;
        MapFrame(Resources.Load<Texture2D>(Root + "Maps/region"), 0, Top, width, height - Top, new Color(0.35f, 0.38f, 0.42f));
        var region = TowerRules.Region(selectedRegion);
        TopBar("PLAN EXPEDITION  •  " + region.name.ToUpperInvariant() + (selectedLayout.Length > 0 ? "  •  " + TowerForestLayouts.Get(selectedLayout).name.ToUpperInvariant() : ""),
            "BACK", () => Go(TowerRules.SilverwoodDepth(selectedRegion) > 0 ? View.Map : View.Region));
        float pw = Mathf.Min(1060, width - 40), px = (width - pw) / 2;
        var panel = PanelAt("Plan panel", content, px, Top + 16, pw, height - Top - 32).transform;
        TextAt(panel, "Party title", "PARTY  •  first three fight, the rest wait in reserve", 20, 12, pw - 40, 26, 17, Gold);
        var all = BattleCatalog.Party();
        float cw = (pw - 40 - 5 * 10) / 6f;
        for (int i = 0; i < TowerRules.Fighters.Length; i++)
        {
            string id = TowerRules.Fighters[i];
            int slot = planParty.IndexOf(id);
            var unit = all.Find(u => u.Id == id);
            var hero = R.HeroResident(id);
            float cx = 20 + i * (cw + 10);
            var card = At("Fighter " + id, panel, cx, 44, cw, 210, slot < 0 ? new Color(0.1f, 0.13f, 0.17f, 0.95f) :
                slot < 3 ? new Color(0.16f, 0.36f, 0.4f, 0.98f) : new Color(0.28f, 0.26f, 0.16f, 0.98f));
            card.gameObject.AddComponent<Button>().onClick.AddListener(() => TogglePlan(id));
            var chibi = Resources.Load<Texture2D>("AdamsHaven/Chibi/" + id);
            if (chibi != null)
            {
                var pic = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage));
                pic.transform.SetParent(card.transform, false);
                var pr = pic.GetComponent<RectTransform>();
                pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1); pr.pivot = new Vector2(0.5f, 1);
                float ah = 110, aw = ah * chibi.width / chibi.height;
                pr.anchoredPosition = new Vector2(0, -6); pr.sizeDelta = new Vector2(aw, ah);
                pic.GetComponent<RawImage>().texture = chibi; pic.GetComponent<RawImage>().raycastTarget = false;
            }
            TextAt(card.transform, "Name", unit != null ? unit.Name : FighterName(id), 6, 118, cw - 12, 22, 13, Cream, TextAnchor.MiddleCenter);
            TextAt(card.transform, "Gear", hero == null ? "No Tower gear" : "Weapon +" + hero.weapon + "  Tool +" + hero.tool,
                6, 140, cw - 12, 20, 12, new Color(0.8f, 0.85f, 0.9f), TextAnchor.MiddleCenter);
            TextAt(card.transform, "Stats", unit == null ? "" : "HP " + Mathf.RoundToInt(unit.MaxHp * (1 + R.GearHp(id))) +
                "  ATK " + Mathf.RoundToInt(Mathf.Max(unit.Attack, unit.Magic) * (1 + R.GearAttack(id))), 6, 160, cw - 12, 20, 12, Cream, TextAnchor.MiddleCenter);
            TextAt(card.transform, "Slot", slot < 0 ? "tap to add" : slot < 3 ? "FIELD " + (slot + 1) : "RESERVE", 6, 182, cw - 12, 22, 14,
                slot < 0 ? new Color(0.6f, 0.65f, 0.7f) : Gold, TextAnchor.MiddleCenter);
        }
        TextAt(panel, "Prov title", "PROVISIONS  •  taken from Tower stores, unused ones come home", 20, 268, pw - 40, 26, 17, Gold);
        Stepper(panel, 20, 300, "RATIONS", planRations, R.MaxRations, "1 per trail travelled. Each: " + TowerRules.RationFood +
            " food + " + TowerRules.RationWater + " water.", v => planRations = v);
        Stepper(panel, 20, 350, "TONICS", planTonics, R.State.tonics, "Heal 40% or revive a fallen fighter.", v => planTonics = v);
        Stepper(panel, 20, 400, "FIREWOOD", planFirewood, R.MaxFirewood, "Full rest at camps and fires. Each: " +
            TowerRules.FirewoodBundle + " firewood.", v => planFirewood = v);
        string error = R.CanStartExpedition(selectedRegion, planParty, planRations, planTonics, planFirewood);
        TextAt(panel, "Rules", "Loot you find is your HAUL. If the whole party falls, only the 2-slot SAFE POCKET comes home " +
            "(experience is always kept). Return to the Tower at any time to bank everything.", 20, 452, pw - 300, 60, 13,
            new Color(0.82f, 0.86f, 0.9f));
        if (error != null) TextAt(panel, "Error", error, 20, 512, pw - 300, 24, 14, new Color(1f, 0.6f, 0.5f));
        ButtonAt(panel, "Start", "SET OUT", pw - 260, 460, 240, 60, StartRun, Teal, 20, error == null);
    }

    private void Stepper(Transform parent, float x, float y, string label, int value, int max, string note, Action<int> set)
    {
        TextAt(parent, label, label, x, y, 120, 40, 15, Cream);
        ButtonAt(parent, label + " minus", "−", x + 120, y + 2, 40, 36, () => { set(Mathf.Max(0, value - 1)); Rebuild(); }, Teal, 20, value > 0);
        TextAt(parent, label + " value", value + " / " + max, x + 166, y, 90, 40, 16, Gold, TextAnchor.MiddleCenter);
        ButtonAt(parent, label + " plus", "+", x + 258, y + 2, 40, 36, () => { set(Mathf.Min(max, value + 1)); Rebuild(); }, Teal, 20, value < max);
        TextAt(parent, label + " note", note, x + 312, y, 520, 40, 13, new Color(0.8f, 0.85f, 0.9f));
    }

    private void TogglePlan(string id)
    {
        if (planParty.Contains(id)) planParty.Remove(id);
        else if (planParty.Count < 6) planParty.Add(id);
        Rebuild();
    }

    private void StartRun()
    {
        string error = R.StartExpedition(selectedRegion, planParty, planRations, planTonics, planFirewood, selectedLayout);
        if (error != null) { Say(error); return; }
        tower.SaveExpedition();
        Go(View.Forest);
        Say("The forest has shifted into " + R.RunLayout.name + ". Follow the trails.");
    }

    // ---------------------------------------------------------------- forest map

    private void BuildForest()
    {
        var run = R.Run; var layout = R.RunLayout;
        float width = root.rect.width, height = root.rect.height;
        var tex = Resources.Load<Texture2D>(layout.backdrop) ?? Resources.Load<Texture2D>(Root + "Maps/region");
        var map = MapFrame(tex, 0, Top, width - Side - 12, height - Top, new Color(0.9f, 0.9f, 0.92f));
        if (layout.mirror) map.GetComponent<RawImage>().uvRect = new Rect(1, 0, -1, 1);
        // Tapping open ground puts away the inspect card.
        var ground = map.gameObject.AddComponent<Button>();
        ground.transition = Selectable.Transition.None;
        ground.onClick.AddListener(() => { if (selectedNode.Length > 0 && !walking && !fading) { selectedNode = ""; Rebuild(); } });
        walkMap = map;
        walkFigures.Clear(); walkShadows.Clear();
        walking = false; fading = false;
        inspectCard = null;
        // The routes the party actually walked, worn into the ground (never a line to somewhere it has not been).
        DrawTrails(map, run.trails);
        foreach (var node in layout.nodes)
        {
            var at = OnMap(map, node.x, node.y);
            if (!R.NodeVisible(node.id))
            {
                // Unseen places hide in the fog; a faint glow gives them away.
                var fog = Box("Fog", map, new Vector2(0, 1), new Vector2(0.5f, 0.5f), at, new Vector2(260, 230), new Color(0.12f, 0.14f, 0.17f, 0.9f));
                fog.sprite = Puff; fog.raycastTarget = false;
                var hint = Box("Glow in the fog", map, new Vector2(0, 1), new Vector2(0.5f, 0.5f), at, new Vector2(54, 54), new Color(0.7f, 0.85f, 1f, 0.30f));
                hint.sprite = Puff; hint.raycastTarget = false;
                continue;
            }
            DrawPlace(map, node, run);
        }
        SpawnParty(map, run);
        if (selectedNode.Length > 0 && R.EventBlock() == null && string.IsNullOrEmpty(run.eventResult))
            BuildInspect(map, layout.Node(selectedNode));
        TopBar(R.RunRegion.name.ToUpperInvariant() + "  •  " + layout.name.ToUpperInvariant(), "RETURN TO TOWER",
            () => { tower.CloseExpedition(null); });
        BuildRunPanel(false);
        if (lootOpen) BuildLoot();
        else BuildEventCard();
        ResumeHaltedWalk(run);
        if (fadingIn) { MakeCurtain(1); fadeClock = 0; }
    }

    // First tap looks at a place, a second tap (or WALK THERE) sets off.
    private void TapNode(string id)
    {
        if (walking || fading || fadingIn) return;
        var run = R.Run;
        if (id == run.at) { selectedNode = selectedNode == id ? "" : id; Rebuild(); return; }
        if (id != selectedNode) { selectedNode = id; Rebuild(); return; }
        WalkTo(id);
    }

    private void WalkTo(string id)
    {
        if (walking || fading || fadingIn) return;
        string block = R.EventBlock();
        if (block != null) { Say(block); return; }
        if (!R.RunLayout.Linked(R.Run.at, id)) { Say("No known way there yet."); return; }
        StartWalk(id);
    }

    // A visible place: a soft glow, no ring, no lines, no icon. Landmarks show their painted prop.
    // Places one trail away glow brighter, so the next steps read without drawing a path.
    private void DrawPlace(RectTransform map, TowerForestNode node, TowerRun run)
    {
        var at = OnMap(map, node.x, node.y);
        bool here = node.id == run.at, cleared = run.cleared.Contains(node.id), visited = run.visited.Contains(node.id);
        bool near = !here && R.RunLayout.Linked(run.at, node.id), picked = node.id == selectedNode;
        Color tint = cleared ? new Color(0.5f, 1f, 0.6f, 0.5f) : node.kind == "lair" ? new Color(1f, 0.4f, 0.35f, 0.55f) :
            node.kind == "camp" ? new Color(1f, 0.82f, 0.45f, 0.6f) : visited ? new Color(0.6f, 0.9f, 1f, 0.5f) : new Color(0.7f, 0.85f, 1f, 0.42f);
        if (!here && !near) tint.a *= 0.55f;
        if (picked) tint.a = Mathf.Min(0.95f, tint.a * 1.6f);
        float size = here ? 84 : picked ? 92 : near ? 74 : 62;
        var glow = Box("Place " + node.id, map, new Vector2(0, 1), new Vector2(0.5f, 0.5f), at, new Vector2(size, size), tint);
        glow.sprite = Puff;
        string id = node.id;
        glow.gameObject.AddComponent<Button>().onClick.AddListener(() => TapNode(id));
        if (node.kind == "landmark" && !string.IsNullOrEmpty(node.prop))
        {
            var prop = Load(Root + "Props/" + node.prop);
            if (prop != null)
            {
                var pic = Box("Landmark", map, new Vector2(0, 1), new Vector2(0.5f, 0.15f), at, new Vector2(110, 110), Color.white);
                pic.sprite = prop; pic.preserveAspect = true; pic.raycastTarget = false;
            }
        }
        // A warm spark marks the camp fire; everything else is told apart in the inspect card.
        if (node.kind == "camp")
        {
            var fire = Box("Camp fire", map, new Vector2(0, 1), new Vector2(0.5f, 0.5f), at, new Vector2(18, 18), new Color(1f, 0.75f, 0.35f, 0.95f));
            fire.sprite = Puff; fire.raycastTarget = false;
        }
        if (here && !picked)
            Label(map, at + new Vector2(0, -40), node.name + (cleared ? " (cleared)" : ""), 13, cleared ? new Color(0.7f, 1f, 0.7f) : Cream);
    }

    private static string PlaceKind(TowerForestNode node)
    {
        int floors = TowerForestLayouts.Floors(node.kind);
        if (node.kind == "camp") return "Camp";
        if (node.kind == "lair") return "The region's lair  •  " + floors + " floors";
        return (node.kind == "landmark" ? "Landmark" : TowerForestLayouts.Pretty(node.kind)) + "  •  " + ThemeName(node.theme) +
            ", " + floors + (floors == 1 ? " floor" : " floors");
    }

    // Small card beside a tapped place: what it is, what the way costs, and the button to go.
    private void BuildInspect(RectTransform map, TowerForestNode node)
    {
        if (node == null || !R.NodeVisible(node.id)) return;
        var run = R.Run;
        bool here = node.id == run.at, linked = !here && R.RunLayout.Linked(run.at, node.id), cleared = run.cleared.Contains(node.id);
        float w = 250, h = here ? 70 : linked ? 128 : 92;
        var at = OnMap(map, node.x, node.y);
        float x = at.x + 40, y = -at.y - h / 2;
        if (x + w > map.rect.width - 6) x = at.x - 40 - w;
        x = Mathf.Clamp(x, 6, Mathf.Max(6, map.rect.width - w - 6));
        y = Mathf.Clamp(y, 6, Mathf.Max(6, map.rect.height - h - 6));
        var card = PanelAt("Inspect", map, x, y, w, h);
        inspectCard = card.gameObject;
        var icon = Icon(node.kind);
        if (icon != null)
        {
            var glyph = At("Inspect icon", card.transform, 10, 10, 32, 32, Color.white);
            glyph.sprite = icon; glyph.preserveAspect = true; glyph.raycastTarget = false;
        }
        TextAt(card.transform, "Inspect name", node.name + (cleared ? "  (cleared)" : ""), 50, 8, w - 60, 22, 15, cleared ? new Color(0.7f, 1f, 0.7f) : Gold);
        TextAt(card.transform, "Inspect kind", PlaceKind(node), 50, 30, w - 60, 18, 11, Cream);
        if (here) return;
        string way = !linked ? "No known way from here yet." : R.OnRoad(run.at, node.id) ?
            "Road: a ration every 2nd trip, little threat." : "New trail: 1 ration, raises threat.";
        TextAt(card.transform, "Inspect way", way, 10, 54, w - 20, 22, 12, linked ? new Color(0.8f, 0.86f, 0.92f) : new Color(1f, 0.6f, 0.5f));
        if (linked)
        {
            string id = node.id;
            ButtonAt(card.transform, "Walk", "WALK THERE", 10, h - 46, w - 20, 36, () => WalkTo(id), Teal, 15);
        }
    }

    // Every walked route pressed into the ground as one soft texture: older routes fainter, edges ragged.
    private void DrawTrails(RectTransform map, List<TowerTrail> trails)
    {
        if (trailTex != null) { Destroy(trailTex); trailTex = null; }
        if (trails == null || trails.Count == 0 || map.rect.height <= 0) return;
        int tw = 512, th = Mathf.Clamp(Mathf.RoundToInt(tw * map.rect.height / map.rect.width), 64, 512);
        var alpha = new float[tw * th];
        float radius = tw / 200f;
        for (int t = 0; t < trails.Count; t++)
        {
            var pts = trails[t].pts;
            if (pts == null || pts.Count < 2) continue;
            float strength = Mathf.Lerp(0.5f, 0.85f, trails.Count == 1 ? 1 : t / (float)(trails.Count - 1));
            float walked = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = new Vector2(pts[i - 1].x * tw, pts[i - 1].y * th), b = new Vector2(pts[i].x * tw, pts[i].y * th);
                float seg = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(seg / 0.6f));
                for (int s = 0; s < steps; s++)
                {
                    float d = walked + seg * s / steps;
                    float r = radius * (0.7f + 0.6f * Mathf.PerlinNoise(d * 0.07f, t * 3.1f));
                    Stamp(alpha, tw, th, Vector2.Lerp(a, b, s / (float)steps), r, strength);
                }
                walked += seg;
            }
        }
        var px = new Color32[tw * th];
        for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float a = alpha[y * tw + x];
                if (a <= 0) continue;
                // Grit so it reads as trodden earth rather than paint.
                float grit = 0.75f + 0.5f * Mathf.PerlinNoise(x * 0.5f, y * 0.5f);
                px[(th - 1 - y) * tw + x] = new Color32(168, 136, 92, (byte)Mathf.Clamp(a * grit * 120f, 0, 255));
            }
        trailTex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        trailTex.SetPixels32(px); trailTex.Apply();
        var go = new GameObject("Worn trails", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(map, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var raw = go.GetComponent<RawImage>();
        raw.texture = trailTex; raw.raycastTarget = false;
    }

    private static void Stamp(float[] alpha, int w, int h, Vector2 c, float r, float strength)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(c.x + r));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(c.y + r));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / r;
                if (d >= 1) continue;
                float a = strength * (1 - d * d);
                int i = y * w + x;
                if (a > alpha[i]) alpha[i] = a;
            }
    }

    // The lead hero stands at the current place with the others close behind.
    private void SpawnParty(RectTransform map, TowerRun run)
    {
        var node = R.RunLayout.Node(run.at);
        if (node == null) return;
        var at = OnMap(map, node.x, node.y) + new Vector2(0, 6);
        int shown = 0;
        for (int i = 0; i < run.party.Count && shown < 3; i++)
        {
            if (run.hp[i] <= 0) continue;
            var chibi = Resources.Load<Texture2D>("AdamsHaven/Chibi/" + run.party[i]);
            if (chibi == null) continue;
            var shadow = Box("Party shadow", map, new Vector2(0, 1), new Vector2(0.5f, 0.5f), at, new Vector2(38, 12), new Color(0, 0, 0, 0.4f));
            shadow.sprite = Disc(false); shadow.raycastTarget = false;
            var pic = new GameObject("Party " + run.party[i], typeof(RectTransform), typeof(RawImage));
            pic.transform.SetParent(map, false);
            var pr = pic.GetComponent<RectTransform>();
            float h = shown == 0 ? 62 : 52;
            pr.anchorMin = pr.anchorMax = new Vector2(0, 1); pr.pivot = new Vector2(0.5f, 0);
            pr.sizeDelta = new Vector2(h * chibi.width / (float)chibi.height, h);
            pic.GetComponent<RawImage>().texture = chibi; pic.GetComponent<RawImage>().raycastTarget = false;
            walkFigures.Add(pr); walkShadows.Add(shadow.rectTransform);
            Place(shown, at + Formation(shown), false);
            shown++;
        }
        // Lead drawn last so it stands in front.
        for (int i = walkFigures.Count - 1; i >= 0; i--) walkFigures[i].SetAsLastSibling();
    }

    // Resting formation: followers stand just behind the lead.
    private static Vector2 Formation(int i) { return new Vector2(-18 * i, 6 * i); }

    private void Place(int i, Vector2 pos, bool faceLeft, float bob = 0)
    {
        if (i >= walkFigures.Count) return;
        // Lower on the painting reads as nearer the viewer.
        float s = walkMap == null ? 1 : Mathf.Lerp(0.86f, 1.08f, Mathf.Clamp01(-pos.y / walkMap.rect.height));
        walkFigures[i].anchoredPosition = pos + new Vector2(0, bob);
        walkFigures[i].localScale = new Vector3(faceLeft ? -s : s, s, 1);
        walkShadows[i].anchoredPosition = pos;
        walkShadows[i].localScale = new Vector3(s * (1 - bob * 0.05f), s, 1);
    }

    // ---------------------------------------------------------------- walking

    private const float WalkGap = 30f, CommitAt = 0.55f;

    // The painting's own ground. Stand-in plates are shown mirrored, so their mask is read mirrored too.
    private void LoadWalkMask(TowerForestLayout layout)
    {
        walkMaskMirror = false;
        walkMask = layout.mirror ? null : TowerMapMask.Load(layout.id);
        if (walkMask != null) return;
        walkMask = TowerMapMask.Load(System.IO.Path.GetFileName(layout.backdrop));
        walkMaskMirror = walkMask != null && layout.mirror;
    }

    private Vector2 MaskSpace(Vector2 p) { return walkMaskMirror ? new Vector2(1 - p.x, p.y) : p; }

    private void StartWalk(string to)
    {
        var run = R.Run; var layout = R.RunLayout;
        var a = layout.Node(run.at); var b = layout.Node(to);
        if (a == null || b == null || walkMap == null) return;
        selectedNode = "";
        if (inspectCard != null) { Destroy(inspectCard); inspectCard = null; }
        walkFrom = run.at; walkTo = to;
        haltPts = null;
        // Believable route over the painting's own ground; without one the party fades across.
        LoadWalkMask(layout);
        List<Vector2> route = null;
        if (walkMask != null) route = walkMask.FindPath(MaskSpace(new Vector2(a.x, a.y)), MaskSpace(new Vector2(b.x, b.y)));
        if (route != null) for (int i = 0; i < route.Count; i++) route[i] = MaskSpace(route[i]);
        if (route == null || route.Count < 2) { fading = true; fadeClock = 0; MakeCurtain(0); return; }
        BeginWalk(route, 0, false);
        MakeCurtain(0);     // swallows taps while the party is on the move
    }

    private void BeginWalk(List<Vector2> route, float startAt, bool committed)
    {
        walkPts = route;
        walkPx.Clear();
        foreach (var p in route) walkPx.Add(OnMap(walkMap, p.x, p.y) + new Vector2(0, 6));
        walkLen = 0;
        for (int i = 1; i < walkPx.Count; i++) walkLen += Vector2.Distance(walkPx[i - 1], walkPx[i]);
        walkDist = startAt * walkLen; walkPrint = walkDist; printLeft = true; walkPace = 1;
        walkCommitted = committed; walkCommitAt = walkLen * CommitAt;
        walking = true;
    }

    // An event stopped the party on the way: stand there until it is settled, then walk the rest.
    private void ResumeHaltedWalk(TowerRun run)
    {
        if (haltPts == null) return;
        if (haltLayout != run.layout || haltTo != run.at || walkMap == null) { haltPts = null; return; }
        if (walkMask == null) LoadWalkMask(R.RunLayout);
        BeginWalk(haltPts, haltAt, true);
        walkFrom = haltFrom; walkTo = haltTo;
        if (R.EventBlock() != null || !string.IsNullOrEmpty(run.eventResult) || lootOpen)
        {
            walking = false;
            PoseParty(walkDist, false);
            return;
        }
        haltPts = null;
        PoseParty(walkDist, true);
        MakeCurtain(0);
    }

    private void MakeCurtain(float alpha)
    {
        fadeCurtain = Stretch("Curtain", content, new Color(0, 0, 0, alpha));
        fadeCurtain.raycastTarget = true;
    }

    private Vector2 PointAlong(float d, out Vector2 dir)
    {
        d = Mathf.Clamp(d, 0, walkLen);
        float run = 0;
        for (int i = 1; i < walkPx.Count; i++)
        {
            float seg = Vector2.Distance(walkPx[i - 1], walkPx[i]);
            if (run + seg >= d || i == walkPx.Count - 1)
            {
                dir = seg > 0.001f ? (walkPx[i] - walkPx[i - 1]) / seg : Vector2.right;
                return Vector2.Lerp(walkPx[i - 1], walkPx[i], seg > 0.001f ? Mathf.Clamp01((d - run) / seg) : 0);
            }
            run += seg;
        }
        dir = Vector2.right;
        return walkPx[walkPx.Count - 1];
    }

    // Lead at distance `lead` along the route, followers a step behind; they fall in from and out to the resting formation.
    private void PoseParty(float lead, bool moving)
    {
        if (walkPx.Count < 2) return;
        for (int i = 0; i < walkFigures.Count; i++)
        {
            float d = lead - i * WalkGap;
            Vector2 dir;
            var pos = PointAlong(d, out dir);
            if (d < 0) pos = Vector2.Lerp(pos, walkPx[0] + Formation(i), Mathf.Clamp01(-d / WalkGap));
            else if (d > walkLen) pos = Vector2.Lerp(pos, walkPx[walkPx.Count - 1] + Formation(i), Mathf.Clamp01((d - walkLen) / WalkGap));
            bool stepping = moving && d > 0 && d < walkLen;
            float bob = stepping ? Mathf.Abs(Mathf.Sin(d * 0.18f)) * 3f : 0;
            bool left = stepping && Mathf.Abs(dir.x) > 0.2f ? dir.x < 0 : walkFigures[i].localScale.x < 0;
            Place(i, pos, left, bob);
        }
    }

    private void UpdateWalk()
    {
        if (fadingIn)
        {
            fadeClock += Time.unscaledDeltaTime;
            if (fadeCurtain == null) { fadingIn = false; return; }
            fadeCurtain.color = new Color(0, 0, 0, 1 - Mathf.Clamp01(fadeClock / 0.35f));
            if (fadeClock >= 0.35f) { Destroy(fadeCurtain.gameObject); fadeCurtain = null; fadingIn = false; }
            return;
        }
        if (fading)
        {
            fadeClock += Time.unscaledDeltaTime;
            if (fadeCurtain != null) fadeCurtain.color = new Color(0, 0, 0, Mathf.Clamp01(fadeClock / 0.3f));
            if (fadeClock < 0.3f) return;
            fading = false;
            string error = R.ForestMove(walkTo);
            if (error != null) { Rebuild(); Say(error); return; }
            fadingIn = true;
            Act(null);
            return;
        }
        // Slower over rough ground, easing in and out at the ends.
        float terrain = 1f;
        if (walkMask != null && walkDist < walkLen)
        {
            Vector2 dir;
            var p = PointAlong(walkDist, out dir);
            var n = MaskSpace(new Vector2(p.x / walkMap.rect.width, (6 - p.y) / walkMap.rect.height));
            if (walkMask.Cost(n.x, n.y) > TowerMapMask.OpenCost) terrain = 0.6f;
        }
        walkPace = Mathf.MoveTowards(walkPace, terrain, Time.unscaledDeltaTime * 2f);
        float ease = Mathf.Min(Mathf.Clamp01(0.4f + walkDist / 50f), Mathf.Clamp(0.4f + (walkLen - walkDist) / 50f, 0.4f, 1f));
        walkDist += Mathf.Max(110f, walkMap.rect.width * 0.11f) * walkPace * ease * Time.unscaledDeltaTime;
        PoseParty(walkDist, true);
        // Footprints behind the lead.
        if (walkDist < walkLen && walkDist - walkPrint > 22f && walkFigures.Count > 0)
        {
            walkPrint = walkDist;
            Vector2 dir;
            var spot = PointAlong(walkDist - 14f, out dir);
            var normal = new Vector2(-dir.y, dir.x);
            var print = Box("Footprint", walkMap, new Vector2(0, 1), new Vector2(0.5f, 0.5f), spot + normal * (printLeft ? 4f : -4f) + new Vector2(0, -5),
                new Vector2(8, 4.5f), new Color(0.2f, 0.14f, 0.08f, 0.55f));
            print.sprite = Disc(false); print.raycastTarget = false;
            print.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            print.rectTransform.SetSiblingIndex(Mathf.Max(0, walkFigures[walkFigures.Count - 1].GetSiblingIndex() - walkFigures.Count));
            printLeft = !printLeft;
        }
        // Partway along, the step happens in the rules; an event there stops the party on the spot.
        if (!walkCommitted && walkDist >= Mathf.Min(walkCommitAt, walkLen))
        {
            walkCommitted = true;
            string error = R.ForestMove(walkTo);
            if (error != null) { walking = false; Rebuild(); Say(error); return; }
            if (R.EventBlock() != null)
            {
                walking = false;
                float at = Mathf.Clamp01(walkDist / walkLen);
                haltPts = walkPts; haltAt = at; haltLayout = R.Run.layout; haltFrom = walkFrom; haltTo = walkTo;
                R.RecordTrail(walkFrom, walkTo, Partial(walkPts, at));
                Act(null);
                return;
            }
            tower.SaveExpedition();
        }
        // Done once the last follower has fallen in beside the lead.
        if (walkDist >= walkLen + WalkGap * walkFigures.Count)
        {
            walking = false;
            R.RecordTrail(walkFrom, walkTo, walkPts);
            Act(null);
        }
    }

    // The first part of a route, up to `at` (0..1) of its length.
    private static List<Vector2> Partial(List<Vector2> pts, float at)
    {
        float total = 0;
        for (int i = 1; i < pts.Count; i++) total += Vector2.Distance(pts[i - 1], pts[i]);
        var part = new List<Vector2> { pts[0] };
        float left = total * at;
        for (int i = 1; i < pts.Count && left > 0; i++)
        {
            float seg = Vector2.Distance(pts[i - 1], pts[i]);
            part.Add(seg <= left ? pts[i] : Vector2.Lerp(pts[i - 1], pts[i], left / seg));
            left -= seg;
        }
        if (part.Count < 2) part.Add(pts[0]);
        return part;
    }

    // Right-hand panel shared by the forest map and the dungeon.
    private void BuildRunPanel(bool dungeon)
    {
        var run = R.Run;
        float width = root.rect.width, height = root.rect.height;
        var panel = PanelAt("Run panel", content, width - Side - 6, Top + 6, Side, height - Top - 12).transform;
        float y = 12;
        if (!dungeon)
        {
            var node = R.RunLayout.Node(run.at);
            TextAt(panel, "Node", node.name.ToUpperInvariant(), 16, y, Side - 32, 26, 18, Gold); y += 26;
            string kind = node.kind == "camp" ? "Camp: rest by the fire" : node.kind == "lair" ? "The region's lair: " +
                TowerForestLayouts.Floors(node.kind) + " floors, boss at the bottom" : (node.kind == "landmark" ? "Landmark" :
                TowerForestLayouts.Pretty(node.kind)) + "  •  " + ThemeName(node.theme) + ", " + TowerForestLayouts.Floors(node.kind) +
                (TowerForestLayouts.Floors(node.kind) == 1 ? " floor" : " floors");
            TextAt(panel, "Kind", run.cleared.Contains(node.id) ? "Cleared." : kind, 16, y, Side - 32, 40, 13, Cream); y += 44;
            if (node.kind == "camp")
                ButtonAt(panel, "Rest", "REST AT CAMP (1 firewood)", 16, y, Side - 32, 42, () => Act(R.CampRest()), Teal, 14, run.firewood > 0);
            else
                ButtonAt(panel, "Enter", "ENTER " + (node.kind == "landmark" ? "LANDMARK" : "DUNGEON"), 16, y, Side - 32, 42,
                    () => { string e = R.EnterPoi(); if (e != null) Say(e); else { tower.SaveExpedition(); Go(View.Dungeon); } },
                    Teal, 16, !run.cleared.Contains(node.id));
            y += 50;
            TextAt(panel, "Travel", "Tap a glowing place to look; tap it again or WALK THERE to set off. Brighter glows are one trail away.", 16, y, Side - 32, 34, 12, new Color(0.8f, 0.85f, 0.9f)); y += 38;
        }
        else
        {
            var node = R.RunLayout.Node(run.dungeonPoi);
            var d = R.Dungeon;
            TextAt(panel, "Node", node.name.ToUpperInvariant(), 16, y, Side - 32, 26, 18, Gold); y += 26;
            TextAt(panel, "Floor", ThemeName(d.theme) + "  •  Floor " + (d.floor + 1) + " / " + d.floors, 16, y, Side - 32, 22, 14, Cream); y += 26;
            var vista = Resources.Load<Texture2D>(TowerDungeonIllustration.ArtRoot + d.theme + "_vista");
            if (vista != null && height >= 650)
            {
                var picture = new GameObject("POI battle artwork", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                picture.transform.SetParent(panel, false); picture.texture = vista; picture.raycastTarget = false;
                var rect = picture.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(16, -y); rect.sizeDelta = new Vector2(Side - 32, 116);
                float aspect = (Side - 32) / 116f, sourceAspect = (float)vista.width / vista.height;
                if (sourceAspect < aspect) { float h = sourceAspect / aspect; picture.uvRect = new Rect(0, (1 - h) / 2, 1, h); }
                else { float w = aspect / sourceAspect; picture.uvRect = new Rect((1 - w) / 2, 0, w, 1); }
                y += 124;
            }
            TextAt(panel, "Help", "Tap a discovered room to send the party there. Tap a visible path to explore. WASD / arrows step. Pinch or wheel to zoom; drag to pan.",
                16, y, Side - 32, 48, 12, new Color(0.8f, 0.85f, 0.9f)); y += 52;
        }
        TextAt(panel, "Party title", "PARTY", 16, y, 120, 22, 15, Gold); y += 24;
        for (int i = 0; i < run.party.Count; i++)
        {
            string id = run.party[i];
            int hp = run.hp[i];
            TextAt(panel, "Member " + id, FighterName(id) + (i < 3 ? "" : " (res)"), 16, y, 110, 24, 13, hp > 0 ? Cream : new Color(0.7f, 0.5f, 0.5f));
            At("Hp back " + id, panel, 124, y + 7, 110, 10, new Color(0.05f, 0.05f, 0.07f, 0.9f));
            At("Hp " + id, panel, 124, y + 7, 110 * hp / 100f, 10, hp > 50 ? Moss : hp > 0 ? Gold : Alert);
            ButtonAt(panel, "Tonic " + id, "TONIC", 242, y, 62, 24, () => Act(R.UseTonic(id)), Teal, 11, run.tonics > 0 && hp < 100);
            y += 28;
        }
        y += 6;
        TextAt(panel, "Provisions", "Rations " + run.rations + "   Tonics " + run.tonics + "   Firewood " + run.firewood, 16, y, Side - 32, 22, 13, Cream); y += 26;
        // Threat meter: full threat makes the forest stir and swallow road.
        Color threatColor = run.threat >= TowerRules.ThreatWarn ? Alert : run.threat >= 50 ? Gold : Moss;
        TextAt(panel, "Threat title", "THREAT", 16, y, 66, 22, 13, threatColor);
        At("Threat back", panel, 84, y + 7, Side - 148, 10, new Color(0.05f, 0.05f, 0.07f, 0.9f));
        At("Threat", panel, 84, y + 7, (Side - 148) * run.threat / (float)TowerRules.ThreatMax, 10, threatColor);
        TextAt(panel, "Threat value", run.threat + "%", Side - 58, y, 44, 22, 13, threatColor); y += 26;
        int gold = 0; foreach (var loot in run.haul) gold += loot.gold;
        int safe = 0; foreach (var loot in run.pocket) safe += loot.gold;
        TextAt(panel, "Haul", "Haul: " + run.haul.Count + " finds, " + gold + "g   Pocket " + run.pocket.Count + "/" + TowerRules.PocketSlots +
            " (" + safe + "g)", 16, y, Side - 32, 22, 13, Gold); y += 26;
        ButtonAt(panel, "Loot", "HAUL & SAFE POCKET", 16, y, Side - 32, 36, () => { lootOpen = true; if (view == View.Dungeon) BuildLoot(); else Rebuild(); }, Teal, 14);
        y += 44;
        if (dungeon)
            ButtonAt(panel, "Leave", "LEAVE DUNGEON", 16, y, Side - 32, 40, () => Act(R.LeaveDungeon()), Alert, 15);
        else
            ButtonAt(panel, "Abandon", "END EXPEDITION (bank haul)", 16, y, Side - 32, 40,
                () => { R.EndExpedition(false); tower.CloseExpedition(null); }, Alert, 14);
    }

    private void BuildLoot()
    {
        var run = R.Run;
        float width = root.rect.width, height = root.rect.height;
        var shade = Stretch("Loot shade", content, new Color(0, 0, 0, 0.6f));
        float pw = 640, ph = Mathf.Min(520, height - 80);
        var panel = PanelAt("Loot panel", shade.transform, (width - pw) / 2, (height - ph) / 2, pw, ph).transform;
        TextAt(panel, "Title", "HAUL AND SAFE POCKET", 20, 12, 400, 28, 19, Gold);
        ButtonAt(panel, "Close", "CLOSE", pw - 120, 10, 100, 34, () => { lootOpen = false; Rebuild(); }, Alert, 14);
        TextAt(panel, "Rule", "The pocket's " + TowerRules.PocketSlots + " finds survive even if the party falls.", 20, 42, pw - 40, 22, 13, Cream);
        float y = 72;
        for (int i = 0; i < run.pocket.Count; i++)
        {
            int index = i;
            TextAt(panel, "Pocket " + i, "POCKET: " + TowerRules.LootLine(run.pocket[i]), 20, y, pw - 170, 30, 14, Gold);
            ButtonAt(panel, "Unpocket " + i, "TAKE OUT", pw - 140, y, 120, 30, () => { R.UnpocketLoot(index); tower.SaveExpedition(); BuildLootAgain(); }, Teal, 12);
            y += 36;
        }
        for (int i = 0; i < run.haul.Count && y < ph - 40; i++)
        {
            int index = i;
            TextAt(panel, "Haul " + i, TowerRules.LootLine(run.haul[i]), 20, y, pw - 170, 30, 14, Cream);
            ButtonAt(panel, "Pocket " + i, "POCKET", pw - 140, y, 120, 30, () => { string e = R.PocketLoot(index); if (e != null) Say(e); tower.SaveExpedition(); BuildLootAgain(); },
                Teal, 12, run.pocket.Count < TowerRules.PocketSlots);
            y += 36;
        }
        if (run.haul.Count == 0 && run.pocket.Count == 0) TextAt(panel, "Empty", "Nothing found yet.", 20, y, 300, 30, 14, Cream);
    }

    private void BuildLootAgain() { lootOpen = true; Rebuild(); }

    // ---------------------------------------------------------------- dungeon

    private void BuildDungeon()
    {
        float width = root.rect.width, height = root.rect.height;
        var vp = At("Viewport", content, 0, Top, width - Side - 12, height - Top, new Color(0.05f, 0.04f, 0.03f, 1));
        vp.gameObject.AddComponent<RectMask2D>();
        viewport = vp.rectTransform;
        BuildBoard();
        TopBar(R.RunRegion.name.ToUpperInvariant() + "  •  " + R.RunLayout.Node(R.Run.dungeonPoi).name.ToUpperInvariant(), null, null);
        BuildRunPanel(true);
        RefreshDungeon();
        if (lootOpen) BuildLoot();
    }

    private void BuildBoard()
    {
        var run = R.Run; var d = R.Dungeon;
        boardKey = run.layout + run.dungeonPoi + run.floor + ":" + run.dungeonLayoutVersion;
        float size = TowerDungeon.Size * CellPx;
        board = new GameObject("Board", typeof(RectTransform)).GetComponent<RectTransform>();
        board.SetParent(viewport, false);
        board.anchorMin = board.anchorMax = new Vector2(0.5f, 0.5f);
        board.pivot = new Vector2(0.5f, 0.5f);
        board.sizeDelta = new Vector2(size, size);
        zoom = Mathf.Min(viewport.rect.width / (size + 60), viewport.rect.height / (size + 60)) * 1.6f;
        board.localScale = Vector3.one * zoom;
        if (TowerDungeonIllustration.Available(d.theme))
        {
            dungeonIllustration = board.gameObject.AddComponent<TowerDungeonIllustration>();
            dungeonIllustration.Build(d, CellPx);
            dungeonIllustration.BuildMist(CellPx);
            fogCells = null;
        }
        else
        {
            dungeonIllustration = null;
            var mat = new GameObject("Parchment", typeof(RectTransform), typeof(RawImage));
            mat.transform.SetParent(board, false);
            var mr = mat.GetComponent<RectTransform>();
            mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.offsetMin = new Vector2(-40, -40); mr.offsetMax = new Vector2(40, 40);
            mat.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(Root + "Dungeon/Silverbrook/" + d.theme + "_terrain");
            mat.GetComponent<RawImage>().raycastTarget = false;
            mat.GetComponent<RawImage>().color = new Color(0.24f, 0.29f, 0.35f);
            fogCells = new Image[TowerDungeon.Size * TowerDungeon.Size];
            for (int y = 0; y < TowerDungeon.Size; y++)
                for (int x = 0; x < TowerDungeon.Size; x++)
                {
                    if (d.Walkable(x, y) || IsDungeonWall(d, x, y))
                    {
                        var terrain = Resources.Load<Texture2D>(Root + "Dungeon/Silverbrook/" + d.theme + "_terrain");
                        if (terrain != null) { BuildTerrainCell(d, x, y, terrain); BuildTerrainEdges(d, x, y); continue; }
                    }
                    Sprite sprite = CellSprite(d, x, y);
                    if (sprite != null)
                    {
                        var tile = CellImage("Tile", x, y, Color.white);
                        tile.sprite = sprite;
                    }
                }
            BuildCelestiumPaths(d);
            // Opaque live fog hides terrain and route topology until discovered.
            for (int y = 0; y < TowerDungeon.Size; y++)
                for (int x = 0; x < TowerDungeon.Size; x++)
                {
                    var fog = CellImage("Fog", x, y, Color.white);
                    fog.sprite = White;
                    fogCells[TowerDungeon.Index(x, y)] = fog;
                }
        }
        board.anchoredPosition = -(CellCenter(run.px, run.py) + new Vector2(-size / 2, size / 2)) * zoom;
        ClampBoard();
        token = new GameObject("Party", typeof(RectTransform)).GetComponent<RectTransform>();
        token.SetParent(board, false);
        token.anchorMin = token.anchorMax = new Vector2(0, 1); token.pivot = new Vector2(0.5f, 0.5f);
        token.sizeDelta = new Vector2(CellPx, CellPx);
        var ringImage = Box("Ring", token, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CellPx, CellPx), new Color(0.45f, 0.9f, 1, 0.65f));
        ringImage.sprite = Disc(true); ringImage.raycastTarget = false;
        partyFigures.Clear(); partyOffsets.Clear();
        for (int i = 0; i < run.party.Count; i++)
        {
            var chibi = Resources.Load<Texture2D>("AdamsHaven/Chibi/" + run.party[i]);
            var pic = new GameObject("Party " + run.party[i], typeof(RectTransform));
            pic.transform.SetParent(token, false);
            var pr = pic.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0.2f);
            float height = CellPx * (run.party.Count > 1 ? 1f : 1.25f);
            Vector2 offset = i == 0 ? Vector2.zero : new Vector2((i % 2 == 1 ? -1 : 1) * CellPx * 0.36f,
                -Mathf.Ceil(i / 2f) * CellPx * 0.3f);
            pr.anchoredPosition = offset;
            pr.sizeDelta = new Vector2(chibi != null ? height * chibi.width / chibi.height : height, height);
            if (chibi != null)
            {
                var image = pic.AddComponent<RawImage>(); image.texture = chibi; image.raycastTarget = false;
                if (pic.AddComponent<TowerDungeonPortrait>().Configure(run.party[i]))
                    pr.sizeDelta = new Vector2(height * 124 / 186f, height);
            }
            else
            {
                var label = TextAt(pic.transform, "Missing portrait", run.party[i], 0, 0, height, height, 11, Glass, TextAnchor.MiddleCenter);
                label.raycastTarget = false;
            }
            partyFigures.Add(pr); partyOffsets.Add(offset);
        }
        // Back-row figures first, leader in front.
        for (int i = partyFigures.Count - 1; i >= 0; i--) partyFigures[i].SetAsLastSibling();
        roomIcons.Clear();
    }

    private Image CellImage(string name, int x, int y, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(board, false);
        image.color = color; image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x * CellPx, -y * CellPx);
        rect.sizeDelta = new Vector2(CellPx + 0.5f, CellPx + 0.5f);
        return image;
    }

    private void BuildTerrainCell(TowerDungeon d, int x, int y, Texture2D terrain)
    {
        var tile = new GameObject("POI terrain", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        tile.transform.SetParent(board, false);
        tile.texture = terrain; tile.raycastTarget = false;
        var rect = tile.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x * CellPx, -y * CellPx); rect.sizeDelta = new Vector2(CellPx, CellPx);
        int room = d.RoomAt(x, y);
        if (room >= 0)
        {
            var r = d.rooms[room];
            tile.uvRect = new Rect((x - r.x) / (float)r.w, (r.y + r.h - 1 - y) / (float)r.h, 1f / r.w, 1f / r.h);
        }
        else
        {
            tile.uvRect = new Rect(x / 4f, -y / 4f, 0.25f, 0.25f);
            tile.color = d.Walkable(x, y) ? new Color(0.68f, 0.75f, 0.8f) : new Color(0.25f, 0.32f, 0.39f);
        }
    }

    private void BuildTerrainEdges(TowerDungeon d, int x, int y)
    {
        if (d.Walkable(x, y))
        {
            if (d.CellAt(x, y) == TowerDungeon.Door)
            {
                bool vertical = d.Walkable(x, y - 1) && d.Walkable(x, y + 1);
                var threshold = Box("POI threshold", board, new Vector2(0, 1), new Vector2(0, 1), CellCenter(x, y),
                    vertical ? new Vector2(CellPx * 0.75f, 3) : new Vector2(3, CellPx * 0.75f),
                    new Color(0.55f, 0.66f, 0.72f, 0.6f));
                threshold.raycastTarget = false;
            }
            return;
        }
        foreach (var direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
        {
            if (!d.Walkable(x + direction.x, y + direction.y)) continue;
            Vector2 delta = new Vector2(direction.x, -direction.y);
            var edge = Box("Silverwood room edge", board, new Vector2(0, 1), new Vector2(0, 1),
                CellCenter(x, y) + delta * (CellPx * 0.5f - 2),
                direction.x == 0 ? new Vector2(CellPx, 3) : new Vector2(3, CellPx), new Color(0.48f, 0.61f, 0.7f, 0.65f));
            edge.raycastTarget = false;
        }
    }

    private void BuildCelestiumPaths(TowerDungeon d)
    {
        for (int y = 0; y < TowerDungeon.Size; y++) for (int x = 0; x < TowerDungeon.Size; x++)
        {
            int type = d.CellAt(x, y);
            if (type != TowerDungeon.Corridor && type != TowerDungeon.Door) continue;
            // Each arm lives inside its cell so fog hides undiscovered branches completely.
            foreach (var direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                if (!d.Walkable(x + direction.x, y + direction.y)) continue;
                Vector2 center = CellCenter(x, y);
                Vector2 delta = new Vector2(direction.x, -direction.y);
                // Two fine mineral veins flank the physical walking surface.
                Vector2 normal = new Vector2(-delta.y, delta.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    var glow = Box("Celestium vein", board, new Vector2(0, 1), new Vector2(0, 1),
                        center + delta * CellPx * 0.25f + normal * side * CellPx * 0.27f,
                        direction.x == 0 ? new Vector2(5, CellPx * 0.5f + 1) : new Vector2(CellPx * 0.5f + 1, 5),
                        new Color(0.2f, 0.75f, 0.88f, 0.22f));
                    glow.raycastTarget = false;
                    var core = Box("Silver mineral core", glow.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                        direction.x == 0 ? new Vector2(1.2f, CellPx * 0.5f + 1) : new Vector2(CellPx * 0.5f + 1, 1.2f),
                        new Color(0.55f, 0.88f, 1, 0.65f));
                    core.raycastTarget = false;
                }
            }
        }
    }

    private static Sprite CellSprite(TowerDungeon d, int x, int y)
    {
        int type = d.CellAt(x, y);
        int variant = (x * 7 + y * 13) % 4;
        if (type == TowerDungeon.Floor) return TowerDungeonTiles.Floor(d.theme, variant, false);
        if (type == TowerDungeon.Corridor) return TowerDungeonTiles.Floor(d.theme, variant, true);
        if (type == TowerDungeon.Door) return TowerDungeonTiles.Door(d.theme, d.Walkable(x, y - 1) && d.Walkable(x, y + 1));
        // Rock next to open ground is drawn as wall; deep rock stays plain parchment.
        bool near = false;
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) if (d.Walkable(x + dx, y + dy)) near = true;
        if (!near) return null;
        int mask = (d.Walkable(x, y - 1) ? 1 : 0) | (d.Walkable(x + 1, y) ? 2 : 0) | (d.Walkable(x, y + 1) ? 4 : 0) | (d.Walkable(x - 1, y) ? 8 : 0);
        return TowerDungeonTiles.Wall(d.theme, mask);
    }

    private static bool IsDungeonWall(TowerDungeon d, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            if (d.Walkable(x + dx, y + dy)) return true;
        return false;
    }

    private Vector2 CellCenter(int x, int y) { return new Vector2((x + 0.5f) * CellPx, -(y + 0.5f) * CellPx); }

    private void RefreshDungeon()
    {
        var run = R.Run; var d = R.Dungeon;
        if (run == null || d == null) return;
        if (board == null || boardKey != run.layout + run.dungeonPoi + run.floor + ":" + run.dungeonLayoutVersion) { Rebuild(); return; }
        if (dungeonIllustration != null) dungeonIllustration.Refresh(R);
        else for (int y = 0; y < TowerDungeon.Size; y++)
            for (int x = 0; x < TowerDungeon.Size; x++)
            {
                var fog = fogCells[TowerDungeon.Index(x, y)];
                if (!R.Revealed(x, y)) { fog.color = FogTint; continue; }
                fog.sprite = White;
                fog.color = R.InSight(x, y) ? Color.clear : new Color(0.02f, 0.02f, 0.03f, 0.5f);
            }
        foreach (var icon in roomIcons) Destroy(icon);
        roomIcons.Clear();
        for (int i = 0; i < d.rooms.Count; i++)
        {
            var room = d.rooms[i];
            if (run.roomsDone.Contains(i) || room.kind == "empty" || room.kind == "entrance") continue;
            if (!R.Revealed(room.CenterX, room.CenterY)) continue;
            var position = CellCenter(room.CenterX, room.CenterY);
            position.y += (room.h * 0.5f - 0.65f) * CellPx;
            var badge = At("Encounter marker", board, position.x - 39, -position.y - 12, 78, 24,
                new Color(0.035f, 0.07f, 0.13f, 0.94f));
            badge.raycastTarget = false;
            string title = room.kind == "boss" ? "BOSS" : room.kind == "stairs" ? "DESCEND" :
                room.kind == "treasure" ? "CACHE" : room.kind == "rest" ? "CAMP" :
                room.kind == "merchant" ? "TRADE" : room.kind == "enemy" ? "BATTLE" : room.kind.ToUpperInvariant();
            TextAt(badge.transform, "Encounter", title, 2, 0, 74, 24, 11,
                room.kind == "boss" ? Alert : room.kind == "stairs" ? Gold : new Color(0.65f, 0.94f, 1), TextAnchor.MiddleCenter);
            roomIcons.Add(badge.gameObject);
        }
        token.SetAsLastSibling();
        token.anchoredPosition = CellCenter(run.px, run.py);
        foreach (Transform child in content) if (child.name == "Run panel") { Destroy(child.gameObject); }
        BuildRunPanel(true);
        if (roomPanel != null) { Destroy(roomPanel.gameObject); roomPanel = null; }
        int pending = R.PendingRoom;
        int here = TowerDungeon.Index(run.px, run.py);
        if (pending >= 0 && dismissedAt != here) ShowRoom(pending);
        if (dismissedAt != here && pending < 0) dismissedAt = -1;
        if (toast != null) toast.transform.parent.SetAsLastSibling();
    }

    private void DungeonInput()
    {
        if (roomPanel != null || endPanel != null || lootOpen || Travelling) return;
        var run = R.Run;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            int dx = 0, dy = 0;
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) dy = -1;
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) dy = 1;
            else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) dx = -1;
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) dx = 1;
            if (dx != 0 || dy != 0) { Step(run.px + dx, run.py + dy); return; }
        }
        Vector2 pos; bool down, pressed, released;
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 1 &&
            Touchscreen.current.touches[0].press.isPressed && Touchscreen.current.touches[1].press.isPressed)
        {
            Vector2 a = Touchscreen.current.touches[0].position.ReadValue(), b = Touchscreen.current.touches[1].position.ReadValue();
            float dist = Vector2.Distance(a, b);
            if (lastPinch > 20) ZoomAt(dist / lastPinch, (a + b) / 2);
            lastPinch = dist; pointerDown = false;
            return;
        }
        lastPinch = 0;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))
        {
            var t = Touchscreen.current.primaryTouch;
            pos = t.position.ReadValue(); down = t.press.isPressed; pressed = t.press.wasPressedThisFrame; released = t.press.wasReleasedThisFrame;
        }
        else if (Mouse.current != null)
        {
            pos = Mouse.current.position.ReadValue(); down = Mouse.current.leftButton.isPressed;
            pressed = Mouse.current.leftButton.wasPressedThisFrame; released = Mouse.current.leftButton.wasReleasedThisFrame;
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (wheel != 0 && OverViewport(pos)) ZoomAt(Mathf.Exp(Mathf.Clamp(wheel / 700f, -0.3f, 0.3f)), pos);
        }
        else return;
        if (pressed && OverViewport(pos)) { pointerDown = true; dragged = false; pointerStart = lastPointer = pos; }
        if (pointerDown && down)
        {
            if ((pos - pointerStart).sqrMagnitude > 100) dragged = true;
            if (dragged) PanBy(pos - lastPointer);
            lastPointer = pos;
        }
        if (pointerDown && released)
        {
            pointerDown = false;
            if (!dragged)
            {
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(board, pos, InputCamera, out local))
                {
                    int x = Mathf.FloorToInt((local.x + board.rect.width / 2) / CellPx);
                    int y = Mathf.FloorToInt((board.rect.height / 2 - local.y) / CellPx);
                    if (TowerDungeon.Inside(x, y))
                    {
                        int room = R.Dungeon.RoomAt(x, y);
                        if (room >= 0 && R.Revealed(R.Dungeon.rooms[room].CenterX, R.Dungeon.rooms[room].CenterY))
                        { x = R.Dungeon.rooms[room].CenterX; y = R.Dungeon.rooms[room].CenterY; }
                        Step(x, y);
                    }
                }
            }
        }
    }

    private bool OverViewport(Vector2 screen)
    {
        if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, screen, InputCamera)) return false;
        var data = new PointerEventData(EventSystem.current) { position = screen };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        // Background panels also raycast; only the foremost hit should block board input.
        return hits.Count == 0 || hits[0].gameObject == viewport.gameObject;
    }

    private void PanBy(Vector2 screenDelta)
    {
        float scale = root.rect.width / Screen.width;
        board.anchoredPosition += screenDelta * scale;
        ClampBoard();
    }

    private void ZoomAt(float factor, Vector2 screen)
    {
        float next = Mathf.Clamp(zoom * factor, 0.35f, 3f);
        Vector2 before, after;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screen, InputCamera, out before);
        zoom = next;
        board.localScale = Vector3.one * zoom;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screen, InputCamera, out after);
        board.anchoredPosition += (after - before) * zoom;
        ClampBoard();
    }

    private void ClampBoard()
    {
        float limitX = Mathf.Max(0, board.rect.width * zoom / 2), limitY = Mathf.Max(0, board.rect.height * zoom / 2);
        var p = board.anchoredPosition;
        board.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -limitX, limitX), Mathf.Clamp(p.y, -limitY, limitY));
    }

    private void Step(int x, int y)
    {
        if (Travelling) return;
        List<int> path;
        string error = R.DungeonRoute(x, y, out path);
        if (error != null) { Say(error); return; }
        CancelTravel();
        travelPath.AddRange(path);
        foreach (var figure in partyFigures)
        { var portrait = figure.GetComponent<TowerDungeonPortrait>(); if (portrait != null) portrait.SetTravelling(Travelling); }
        foreach (int cell in path)
        {
            var mark = CellImage("Selected route", cell % TowerDungeon.Size, cell / TowerDungeon.Size, new Color(0.55f, 0.85f, 1, 0.16f));
            routeMarks.Add(mark.gameObject);
        }
        token.SetAsLastSibling();
    }

    private void CancelTravel()
    {
        travelPath.Clear(); travelIndex = 0; travelClock = 0;
        foreach (var mark in routeMarks) if (mark != null) Destroy(mark);
        routeMarks.Clear();
        for (int i = 0; i < partyFigures.Count; i++) if (partyFigures[i] != null) partyFigures[i].anchoredPosition = partyOffsets[i];
        foreach (var figure in partyFigures) if (figure != null)
        { var portrait = figure.GetComponent<TowerDungeonPortrait>(); if (portrait != null) portrait.SetTravelling(false); }
        if (token != null && tower != null && R.Run != null) token.anchoredPosition = CellCenter(R.Run.px, R.Run.py);
    }

    private void UpdateTravel()
    {
        if (!Travelling) return;
        if (roomPanel != null || endPanel != null || lootOpen) { CancelTravel(); return; }
        int cell = travelPath[travelIndex];
        int x = cell % TowerDungeon.Size, y = cell / TowerDungeon.Size;
        travelClock += Time.unscaledDeltaTime * 5;
        token.anchoredPosition = Vector2.Lerp(CellCenter(R.Run.px, R.Run.py), CellCenter(x, y), Mathf.Min(1, travelClock));
        for (int i = 0; i < partyFigures.Count; i++)
            partyFigures[i].anchoredPosition = partyOffsets[i] + new Vector2(0, Mathf.Sin(Time.unscaledTime * 22 + i * 1.5f) * 1.5f);
        KeepPartyInView();
        if (travelClock < 1) return;
        string error = R.DungeonAdvance(x, y);
        if (error != null) { CancelTravel(); Say(error); return; }
        travelIndex++; travelClock = 0;
        commitDungeonStep();
        if (travelIndex <= routeMarks.Count && routeMarks[travelIndex - 1] != null) routeMarks[travelIndex - 1].SetActive(false);
        RefreshDungeon();
        if (R.PendingRoom >= 0 || !Travelling) CancelTravel();
    }

    private void KeepPartyInView()
    {
        // Keep the animated party in view without changing its authoritative cell.
        Vector2 cell = token.anchoredPosition + new Vector2(-board.rect.width / 2, board.rect.height / 2);
        Vector2 onScreen = board.anchoredPosition + cell * zoom;
        float hw = viewport.rect.width / 2 - 80, hh = viewport.rect.height / 2 - 80;
        if (Mathf.Abs(onScreen.x) > hw || Mathf.Abs(onScreen.y) > hh)
        {
            board.anchoredPosition = -cell * zoom;
            ClampBoard();
        }
    }

    // ---------------------------------------------------------------- rooms

    private static readonly string[] CaveThemes = { "cave", "crystal" };

    // silverwoodDepth is 1 to 5 inside the Silverwood depth regions (signature boss rooms), 0 elsewhere.
    private static string RoomArt(string kind, string theme, int seed, int silverwoodDepth = 0)
    {
        bool cave = Array.IndexOf(CaveThemes, theme) >= 0;
        string folder;
        switch (kind)
        {
            case "enemy": folder = "standard_combat"; break;
            case "elite": folder = "elite_combat"; break;
            case "boss":
                if (silverwoodDepth > 0)
                {
                    string signature = Root + "Rooms/boss_d" + silverwoodDepth;
                    if (Resources.Load<Texture2D>(signature) != null) return signature;
                }
                folder = cave ? "cave_boss" : "boss_arena"; break;
            case "treasure": folder = cave ? "cave_loot" : "loot"; break;
            case "rest": folder = cave ? "cave_camp" : (Mathf.Abs(seed) % 2 == 0 ? "safe_camp" : "rest_alcove"); break;
            case "merchant": folder = "merchant"; break;
            case "stairs": folder = "exit"; break;
            case "trap": folder = "trap"; break;
            case "skillcheck": folder = "skill_check"; break;
            case "story": folder = "story"; break;
            case "keygate": folder = "key_gate"; break;
            default: folder = new[] { "secret", "shrine", "puzzle", "skill_check", "story" }[Mathf.Abs(seed) % 5]; break;
        }
        string path = Root + "Rooms/" + folder + "_" + (Mathf.Abs(seed) % 3);
        // A kind whose art has not arrived yet falls back to the original set.
        if (folder == "rest_alcove" || folder == "skill_check" || folder == "story" || folder == "trap" || folder == "key_gate")
            if (Resources.Load<Texture2D>(path) == null) return Root + "Rooms/" + (kind == "rest" ? "safe_camp" : "puzzle") + "_" + (Mathf.Abs(seed) % 3);
        return path;
    }

    private static string RoomTitle(string kind, bool goal, string poiName)
    {
        if (goal) return poiName;
        switch (kind)
        {
            case "enemy": return "Ambush"; case "elite": return "Guardian's Hall"; case "boss": return poiName;
            case "treasure": return "Forgotten Cache"; case "rest": return "Quiet Nook"; case "merchant": return "Wandering Peddler";
            case "stairs": return "Descending Stair"; default: return "Strange Chamber";
        }
    }

    private static string RoomText(string kind, string theme)
    {
        string place = theme == "cave" ? "Roots tighten around the rock." : theme == "marsh" ? "Black water laps at rotten boards." :
            theme == "crystal" ? "Cold light pulses in the crystal." : theme == "briar" ? "Thorns scrape at every step." :
            theme == "keep" ? "Old banners hang in the dark." : theme == "mine" ? "Rusted rails vanish into the dark." :
            theme == "blight" ? "Violet rot creeps across the stone." : theme == "heartwood" ? "Golden light drifts between living roots." :
            "Moss swallows the broken stones.";
        switch (kind)
        {
            case "enemy": return place + " Something moves in the shadows. Fight!";
            case "elite": return place + " A guardian bars the way. It will not let you pass.";
            case "boss": return place + " The master of this place rises to meet you.";
            case "treasure": return place + " A cache lies half-buried here.";
            case "rest": return place + " A sheltered corner, safe enough for a fire.";
            case "merchant": return place + " A hooded peddler grins: \"Tonics, 40 gold.\"";
            case "stairs": return place + " Worn steps spiral down into deeper dark.";
            default: return place + " Carvings, a strange hum... investigate, or leave well alone?";
        }
    }

    private void ShowRoom(int index)
    {
        var run = R.Run; var d = R.Dungeon; var room = d.rooms[index];
        var poi = R.RunLayout.Node(run.dungeonPoi);
        float width = root.rect.width, height = root.rect.height;
        var shade = Stretch("Room shade", content, new Color(0, 0, 0, 0.55f));
        roomPanel = shade.rectTransform;
        float pw = Mathf.Min(760, width - Side - 60), ph = 440;
        float px = (width - Side - pw) / 2, py = Top + (height - Top - ph) / 2;
        var panel = PanelAt("Room", shade.transform, px, py, pw, ph).transform;
        var art = Resources.Load<Texture2D>(RoomArt(room.kind, d.theme, (int)TowerForestLayouts.Hash(run.dungeonPoi, index + run.floor * 17),
            TowerRules.SilverwoodDepth(run.region)));
        float artW = 260;
        if (art != null)
        {
            var frame = At("Art frame", panel, 16, 16, artW, ph - 32, new Color(0, 0, 0, 0.6f));
            var pic = new GameObject("Art", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            pic.transform.SetParent(frame.transform, false);
            var pr = pic.GetComponent<RectTransform>();
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
            pic.GetComponent<RawImage>().texture = art;
            var fit = pic.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = art.width / (float)art.height;
            frame.gameObject.AddComponent<RectMask2D>();
        }
        float tx = art != null ? artW + 36 : 20, tw = pw - tx - 20;
        TextAt(panel, "Title", RoomTitle(room.kind, room.goal, poi.name).ToUpperInvariant(), tx, 18, tw, 34, 24, Gold);
        TextAt(panel, "Kind", (room.goal ? "OBJECTIVE  •  " : "") + TowerForestLayouts.Pretty(room.kind) + "  •  " + ThemeName(d.theme),
            tx, 54, tw, 22, 14, new Color(0.8f, 0.85f, 0.9f));
        TextAt(panel, "Text", RoomText(room.kind, d.theme) + (room.goal ? "\n\nComplete this room to clear " + poi.name + "." : ""),
            tx, 84, tw, 170, 16, Cream);
        float by = ph - 76, bw = (tw - 12) / 2;
        switch (room.kind)
        {
            case "enemy": case "elite": case "boss":
                ButtonAt(panel, "Fight", "FIGHT  (danger " + R.BattleDepth(room.kind) + ")", tx, by, tw, 56, Fight, Alert, 18);
                break;
            case "treasure":
                ButtonAt(panel, "Take", "TAKE THE LOOT", tx, by, tw, 56, () => Act(R.ResolveRoom("take")), Teal, 18);
                break;
            case "rest":
                ButtonAt(panel, "Rest", run.firewood > 0 ? "MAKE CAMP (1 firewood)" : "REST (no firewood)", tx, by, bw, 56, () => Act(R.ResolveRoom("rest")), Teal, 15);
                ButtonAt(panel, "Later", "NOT NOW", tx + bw + 12, by, bw, 56, Dismiss, Alert, 15);
                break;
            case "merchant":
                ButtonAt(panel, "Buy", "BUY TONIC (40g)", tx, by, bw, 56, () => Act(R.ResolveRoom("buy")), Teal, 15);
                ButtonAt(panel, "Leave", "MOVE ON", tx + bw + 12, by, bw, 56, () => Act(R.ResolveRoom("leave")), Alert, 15);
                break;
            case "stairs":
                ButtonAt(panel, "Descend", "DESCEND", tx, by, bw, 56, () => Act(R.ResolveRoom("descend")), Teal, 17);
                ButtonAt(panel, "Later", "NOT YET", tx + bw + 12, by, bw, 56, Dismiss, Alert, 15);
                break;
            default:
                ButtonAt(panel, "Investigate", "INVESTIGATE", tx, by, bw, 56, () => Act(R.ResolveRoom("investigate")), Teal, 16);
                ButtonAt(panel, "Ignore", "LEAVE IT", tx + bw + 12, by, bw, 56, () => Act(R.ResolveRoom("ignore")), Alert, 15);
                break;
        }
    }

    private void Dismiss()
    {
        dismissedAt = TowerDungeon.Index(R.Run.px, R.Run.py);
        if (roomPanel != null) { Destroy(roomPanel.gameObject); roomPanel = null; }
    }

    private void Fight()
    {
        var room = R.Dungeon.rooms[R.PendingRoom];
        FightWithParty(R.BattleDepth(room.kind), "BACK TO THE DUNGEON", (won, hp) =>
        {
            Act(R.ResolveBattle(won, hp));
            if (R.Run != null && won) Say("Victory! The spoils are added to your haul.");
        });
    }

    private void FightAmbush()
    {
        FightWithParty(R.AmbushDepth, "BACK TO THE FOREST", (won, hp) =>
        {
            Act(R.ResolveAmbush(won, hp));
            if (R.Run != null && R.State.log.Count > 0) Say(R.State.log[R.State.log.Count - 1]);
        });
    }

    // Builds the living party (Tower gear applied) and hands the screen to BattleMode; done gets HP percents back.
    private void FightWithParty(int depth, string returnLabel, Action<bool, List<int>> done)
    {
        var run = R.Run;
        var units = BattleCatalog.Party(run.party);
        var alive = new List<BattleUnit>();
        for (int i = 0; i < run.party.Count; i++)
        {
            var unit = units.Find(u => u.Id == run.party[i]);
            if (unit == null || run.hp[i] <= 0) continue;
            unit.Attack *= 1 + R.GearAttack(unit.Id);
            unit.Magic *= 1 + R.GearAttack(unit.Id);
            unit.MaxHp = Mathf.RoundToInt(unit.MaxHp * (1 + R.GearHp(unit.Id)));
            unit.Hp = Mathf.Max(1, Mathf.RoundToInt(unit.MaxHp * run.hp[i] / 100f));
            alive.Add(unit);
        }
        var field = alive.GetRange(0, Mathf.Min(3, alive.Count));
        var reserve = alive.Count > 3 ? alive.GetRange(3, Mathf.Min(3, alive.Count - 3)) : new List<BattleUnit>();
        canvas.gameObject.SetActive(false);
        tower.LaunchExpeditionBattle(depth, field, reserve, won =>
        {
            canvas.gameObject.SetActive(true);
            var hp = new List<int>(run.hp);
            for (int i = 0; i < run.party.Count; i++)
            {
                var unit = alive.Find(u => u.Id == run.party[i]);
                if (unit != null) hp[i] = unit.Hp <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(100f * unit.Hp / unit.MaxHp), 1, 100);
            }
            done(won, hp);
        }, returnLabel);
    }

    // ---------------------------------------------------------------- traversal events

    // Event card (choices with check odds), then its result, or an ambush that must be fought.
    private void BuildEventCard()
    {
        var run = R.Run; var def = R.PendingEvent;
        bool ambush = R.AmbushPending;
        if (def == null && !ambush && string.IsNullOrEmpty(run.eventResult)) return;
        float width = root.rect.width, height = root.rect.height;
        var shade = Stretch("Event shade", content, new Color(0, 0, 0, 0.55f));
        int choices = ambush || def == null ? 1 : def.choices.Count;
        // Optional scene plate for this event (Events/Art/<id>); events without one keep the plain card.
        var art = def == null ? null : Resources.Load<Texture2D>("AdamsHaven/Events/Art/" + def.id);
        float pw = Mathf.Min(760, width - Side - 60), banner = art == null ? 0 : Mathf.Min(190, (height - Top) * 0.28f);
        float ph = Mathf.Min(height - Top - 20, 230 + choices * 56 + banner);
        float px = (width - Side - pw) / 2, py = Top + (height - Top - ph) / 2;
        var panel = PanelAt("Event", shade.transform, px, py, pw, ph).transform;
        if (art != null)
        {
            var scene = new GameObject("Scene", typeof(RectTransform), typeof(RawImage));
            scene.transform.SetParent(panel, false);
            var sr = scene.GetComponent<RectTransform>();
            sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0, 1);
            sr.anchoredPosition = new Vector2(8, -8); sr.sizeDelta = new Vector2(pw - 16, banner);
            var raw = scene.GetComponent<RawImage>();
            raw.texture = art; raw.raycastTarget = false;
            // Centre-crop the 16:9 plate to the banner's wider shape.
            float uvH = Mathf.Clamp01((pw - 16) / banner > 0 ? (art.width / (float)art.height) / ((pw - 16) / banner) : 1f);
            raw.uvRect = new Rect(0, (1f - uvH) * 0.5f, 1, uvH);
        }

        // The first fighter still standing speaks for the party.
        string speaker = run.party[0];
        for (int i = 0; i < run.party.Count; i++) if (run.hp[i] > 0) { speaker = run.party[i]; break; }
        var chibi = Resources.Load<Texture2D>("AdamsHaven/Chibi/" + speaker);
        float tx = 20;
        if (chibi != null)
        {
            var pic = new GameObject("Speaker", typeof(RectTransform), typeof(RawImage));
            pic.transform.SetParent(panel, false);
            var pr = pic.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0, 1);
            float ah = 150, aw = ah * chibi.width / chibi.height;
            pr.anchoredPosition = new Vector2(16, -16 - banner); pr.sizeDelta = new Vector2(aw, ah);
            pic.GetComponent<RawImage>().texture = chibi; pic.GetComponent<RawImage>().raycastTarget = false;
            TextAt(panel, "Speaker name", FighterName(speaker), 16, 168 + banner, aw, 20, 12, Gold, TextAnchor.MiddleCenter);
            tx = aw + 32;
        }
        float tw = pw - tx - 20, by = ph - 20 - choices * 56;
        if (ambush)
        {
            TextAt(panel, "Title", "AMBUSH!", tx, 18, tw, 34, 24, Alert);
            TextAt(panel, "Text", (string.IsNullOrEmpty(run.eventResult) ? "" : run.eventResult + "\n\n") +
                "Enemies burst from the undergrowth. There is no slipping away now.", tx, 58, tw, by - 66, 16, Cream);
            ButtonAt(panel, "Fight", "FIGHT  (danger " + R.AmbushDepth + ")", tx, by, tw, 48, FightAmbush, Alert, 18);
        }
        else if (def == null)
        {
            TextAt(panel, "Title", "WHAT HAPPENED", tx, 18, tw, 34, 22, Gold);
            TextAt(panel, "Text", run.eventResult, tx, 58, tw, by - 66, 16, Cream);
            ButtonAt(panel, "Continue", "CONTINUE", tx, by, tw, 48, () => Act(R.DismissEventResult()), Teal, 17);
        }
        else
        {
            TextAt(panel, "Title", def.title.ToUpperInvariant(), tx, 18 + banner, tw, 34, 22, Gold);
            TextAt(panel, "Text", def.text, tx, 58 + banner, tw, by - 66 - banner, 16, Cream);
            for (int i = 0; i < def.choices.Count; i++)
            {
                int index = i;
                var choice = def.choices[i];
                string odds = "";
                if (choice.chance < 1f)
                {
                    odds = "  •  " + Mathf.RoundToInt(R.ChoiceChance(choice) * 100) + "%";
                    if (choice.trait.Length > 0) odds += R.PartyHasTrait(choice.trait) ? "  (" + choice.trait + " helps)" : "  (" + choice.trait + " would help)";
                }
                string blocked = R.ChoiceBlocked(choice);
                ButtonAt(panel, "Choice " + i, choice.label.ToUpperInvariant() + odds, tx, by + i * 56, tw, 48,
                    () => Act(R.ResolveEvent(index)), i == def.choices.Count - 1 && choice.chance >= 1f ? Alert : Teal, 15, blocked == null);
            }
        }
    }

    private void ShowEnd()
    {
        if (content == null) Rebuild();
        float width = root.rect.width, height = root.rect.height;
        var shade = Stretch("End shade", content, new Color(0, 0, 0, 0.75f));
        endPanel = shade.rectTransform;
        var panel = PanelAt("End", shade.transform, (width - 560) / 2, (height - 260) / 2, 560, 260).transform;
        TextAt(panel, "Title", "THE PARTY HAS FALLEN", 20, 20, 520, 40, 26, Alert, TextAnchor.MiddleCenter);
        TextAt(panel, "Text", "The survivors limp home. Only the Safe Pocket and the experience they earned return to the Tower.",
            30, 70, 500, 80, 16, Cream, TextAnchor.MiddleCenter);
        ButtonAt(panel, "Home", "RETURN TO TOWER", 150, 170, 260, 56, () => tower.CloseExpedition(null), Teal, 18);
    }
}
