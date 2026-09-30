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
    private static readonly Color FogTint = new Color(0.55f, 0.5f, 0.46f, 1);   // unexplored map reads darker than the parchment

    private enum View { Region, Plan, Forest, Dungeon }

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

    private static Sprite disc, ring, white;

    // ---------------------------------------------------------------- lifecycle

    public void Begin(AdamsHavenPrototype controller)
    {
        tower = controller;
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
        if (R.Run == null && view != View.Region && view != View.Plan) { ShowEnd(); return; }
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
        if (Screen.width != lastWidth || Screen.height != lastHeight) Rebuild();
        if (toastTimer > 0)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0 && toast != null) toast.transform.parent.gameObject.SetActive(false);
        }
        if (view == View.Dungeon && R != null && R.Run != null && R.Dungeon != null && board != null && viewport != null) DungeonInput();
    }

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
            case "briar": return "Briar Thicket"; case "keep": return "Watchpost Keep"; default: return "Forest Ruin";
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
        TextAt(panel.transform, "Rule", "Each run the forest shifts into one of 15 maps. Clear its lair to conquer the region.",
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

    // ---------------------------------------------------------------- plan

    private void OpenPlan()
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
        TopBar("PLAN EXPEDITION  •  " + region.name.ToUpperInvariant(), "BACK TO MAP", () => Go(View.Region));
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
        string error = R.StartExpedition(selectedRegion, planParty, planRations, planTonics, planFirewood);
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
        foreach (var node in layout.nodes)
            foreach (var link in node.links)
            {
                if (!R.NodeVisible(node.id) || !R.NodeVisible(link)) continue;
                var other = layout.Node(link);
                bool live = node.id == run.at || link == run.at;
                bool walked = run.visited.Contains(node.id) && run.visited.Contains(link);
                Line(map, OnMap(map, node.x, node.y), OnMap(map, other.x, other.y), live ? 5 : 3,
                    live ? new Color(1f, 0.82f, 0.4f, 0.95f) : walked ? new Color(0.95f, 0.9f, 0.8f, 0.6f) : new Color(0.85f, 0.85f, 0.9f, 0.3f));
            }
        foreach (var node in layout.nodes)
        {
            var at = OnMap(map, node.x, node.y);
            if (!R.NodeVisible(node.id))
            {
                var fog = Box("Fog", map, new Vector2(0, 1), new Vector2(0.5f, 0.5f), at, new Vector2(260, 230), new Color(0.12f, 0.14f, 0.17f, 0.95f));
                fog.sprite = Puff; fog.raycastTarget = false;
                continue;
            }
            bool here = node.id == run.at, cleared = run.cleared.Contains(node.id), visited = run.visited.Contains(node.id);
            if (node.kind == "landmark" && !string.IsNullOrEmpty(node.prop))
            {
                var prop = Load(Root + "Props/" + node.prop);
                if (prop != null)
                {
                    var pic = Box("Landmark", map, new Vector2(0, 1), new Vector2(0.5f, 0.15f), at, new Vector2(110, 110), Color.white);
                    pic.sprite = prop; pic.preserveAspect = true; pic.raycastTarget = false;
                }
            }
            string id = node.id;
            Color color = cleared ? Moss : node.kind == "lair" ? Alert : node.kind == "camp" ? Gold :
                visited ? new Color(0.35f, 0.6f, 0.7f) : new Color(0.3f, 0.4f, 0.48f, 0.9f);
            Badge(map, at, here ? 54 : 44, color, Icon(node.kind), () => TapNode(id));
            if (here)
            {
                var halo = Box("Party", map, new Vector2(0, 1), new Vector2(0.5f, 0.5f), at, new Vector2(74, 74), Gold);
                halo.sprite = Disc(true); halo.raycastTarget = false;
                var chibi = Resources.Load<Texture2D>("AdamsHaven/Chibi/" + run.party[0]);
                if (chibi != null)
                {
                    var pic = new GameObject("Party token", typeof(RectTransform), typeof(RawImage));
                    pic.transform.SetParent(map, false);
                    var pr = pic.GetComponent<RectTransform>();
                    pr.anchorMin = pr.anchorMax = new Vector2(0, 1); pr.pivot = new Vector2(0.5f, 0);
                    pr.anchoredPosition = at + new Vector2(0, 22); pr.sizeDelta = new Vector2(64 * chibi.width / (float)chibi.height, 64);
                    pic.GetComponent<RawImage>().texture = chibi; pic.GetComponent<RawImage>().raycastTarget = false;
                }
            }
            Label(map, at + new Vector2(0, -34), node.name + (cleared ? " (cleared)" : ""), 13, cleared ? new Color(0.7f, 1f, 0.7f) : Cream);
        }
        TopBar(R.RunRegion.name.ToUpperInvariant() + "  •  " + layout.name.ToUpperInvariant(), "RETURN TO TOWER",
            () => { tower.CloseExpedition(null); });
        BuildRunPanel(false);
        if (lootOpen) BuildLoot();
    }

    private void TapNode(string id)
    {
        var run = R.Run;
        if (id == run.at) return;
        Act(R.ForestMove(id));
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
            TextAt(panel, "Travel", "Tap a trail-linked place to travel (1 ration).", 16, y, Side - 32, 20, 12, new Color(0.8f, 0.85f, 0.9f)); y += 24;
        }
        else
        {
            var node = R.RunLayout.Node(run.dungeonPoi);
            var d = R.Dungeon;
            TextAt(panel, "Node", node.name.ToUpperInvariant(), 16, y, Side - 32, 26, 18, Gold); y += 26;
            TextAt(panel, "Floor", ThemeName(d.theme) + "  •  Floor " + (d.floor + 1) + " / " + d.floors, 16, y, Side - 32, 22, 14, Cream); y += 26;
            TextAt(panel, "Help", "Tap an explored cell to walk there. Arrow keys / WASD step. Wheel or pinch to zoom, drag to pan.",
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
        boardKey = run.layout + run.dungeonPoi + run.floor;
        float size = TowerDungeon.Size * CellPx;
        board = new GameObject("Board", typeof(RectTransform)).GetComponent<RectTransform>();
        board.SetParent(viewport, false);
        board.anchorMin = board.anchorMax = new Vector2(0.5f, 0.5f);
        board.pivot = new Vector2(0.5f, 0.5f);
        board.sizeDelta = new Vector2(size, size);
        zoom = Mathf.Min(viewport.rect.width / (size + 60), viewport.rect.height / (size + 60)) * 1.6f;
        board.localScale = Vector3.one * zoom;
        var mat = new GameObject("Parchment", typeof(RectTransform), typeof(RawImage));
        mat.transform.SetParent(board, false);
        var mr = mat.GetComponent<RectTransform>();
        mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.offsetMin = new Vector2(-40, -40); mr.offsetMax = new Vector2(40, 40);
        mat.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(Root + "Dungeon/mat_blank");
        mat.GetComponent<RawImage>().raycastTarget = false;
        mat.GetComponent<RawImage>().color = new Color(0.72f, 0.66f, 0.58f);   // solid rock: dim parchment, not a bright hole
        var fogTex = Resources.Load<Texture2D>(Root + "Dungeon/mat_fog");
        fogCells = new Image[TowerDungeon.Size * TowerDungeon.Size];
        for (int y = 0; y < TowerDungeon.Size; y++)
            for (int x = 0; x < TowerDungeon.Size; x++)
            {
                Sprite sprite = CellSprite(d, x, y);
                if (sprite != null)
                {
                    var tile = CellImage("Tile", x, y, Color.white);
                    tile.sprite = sprite;
                }
            }
        // Fog: unexplored cells show the darkened map, cut cell by cell from the fog mat.
        for (int y = 0; y < TowerDungeon.Size; y++)
            for (int x = 0; x < TowerDungeon.Size; x++)
            {
                var fog = CellImage("Fog", x, y, Color.white);
                if (fogTex != null)
                {
                    float c = fogTex.width / (float)TowerDungeon.Size;
                    fog.sprite = Sprite.Create(fogTex, new Rect(x * c, (TowerDungeon.Size - 1 - y) * c, c, c), new Vector2(0.5f, 0.5f), c);
                }
                fogCells[TowerDungeon.Index(x, y)] = fog;
            }
        board.anchoredPosition = -(CellCenter(run.px, run.py) + new Vector2(-size / 2, size / 2)) * zoom;
        ClampBoard();
        // Grid lines, D&D style.
        for (int i = 0; i <= TowerDungeon.Size; i++)
        {
            var v = new GameObject("Grid", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            v.transform.SetParent(board, false); v.color = new Color(0.1f, 0.07f, 0.04f, 0.28f); v.raycastTarget = false;
            v.rectTransform.anchorMin = v.rectTransform.anchorMax = new Vector2(0, 1); v.rectTransform.pivot = new Vector2(0.5f, 1);
            v.rectTransform.anchoredPosition = new Vector2(i * CellPx, 0); v.rectTransform.sizeDelta = new Vector2(1.2f, size);
            var h = new GameObject("Grid", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            h.transform.SetParent(board, false); h.color = new Color(0.1f, 0.07f, 0.04f, 0.28f); h.raycastTarget = false;
            h.rectTransform.anchorMin = h.rectTransform.anchorMax = new Vector2(0, 1); h.rectTransform.pivot = new Vector2(0, 0.5f);
            h.rectTransform.anchoredPosition = new Vector2(0, -i * CellPx); h.rectTransform.sizeDelta = new Vector2(size, 1.2f);
        }
        token = new GameObject("Party", typeof(RectTransform)).GetComponent<RectTransform>();
        token.SetParent(board, false);
        token.anchorMin = token.anchorMax = new Vector2(0, 1); token.pivot = new Vector2(0.5f, 0.5f);
        token.sizeDelta = new Vector2(CellPx, CellPx);
        var ringImage = Box("Ring", token, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CellPx, CellPx), Gold);
        ringImage.sprite = Disc(true); ringImage.raycastTarget = false;
        var chibi = Resources.Load<Texture2D>("AdamsHaven/Chibi/" + run.party[0]);
        if (chibi != null)
        {
            var pic = new GameObject("Leader", typeof(RectTransform), typeof(RawImage));
            pic.transform.SetParent(token, false);
            var pr = pic.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0.2f);
            pr.sizeDelta = new Vector2(CellPx * 1.1f * chibi.width / chibi.height, CellPx * 1.1f);
            pic.GetComponent<RawImage>().texture = chibi; pic.GetComponent<RawImage>().raycastTarget = false;
        }
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

    private Vector2 CellCenter(int x, int y) { return new Vector2((x + 0.5f) * CellPx, -(y + 0.5f) * CellPx); }

    private void RefreshDungeon()
    {
        var run = R.Run; var d = R.Dungeon;
        if (run == null || d == null) return;
        if (board == null || boardKey != run.layout + run.dungeonPoi + run.floor) { Rebuild(); return; }
        for (int y = 0; y < TowerDungeon.Size; y++)
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
            var badge = Badge(board, CellCenter(room.CenterX, room.CenterY), CellPx * 0.95f,
                room.kind == "boss" ? Alert : room.kind == "stairs" ? Gold : new Color(0.2f, 0.18f, 0.15f, 0.92f),
                room.kind == "stairs" ? null : Icon(room.kind), null);
            if (room.kind == "stairs")
            {
                var label = TextAt(badge.transform, "Stairs", "STAIRS", 0, 0, CellPx * 0.95f, CellPx * 0.95f, 10, Color.black, TextAnchor.MiddleCenter);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.fontStyle = FontStyle.Bold;
            }
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
        if (roomPanel != null || endPanel != null || lootOpen) return;
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
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(board, pos, null, out local))
                {
                    int x = Mathf.FloorToInt((local.x + board.rect.width / 2) / CellPx);
                    int y = Mathf.FloorToInt((board.rect.height / 2 - local.y) / CellPx);
                    if (TowerDungeon.Inside(x, y)) Step(x, y);
                }
            }
        }
    }

    private bool OverViewport(Vector2 screen)
    {
        if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, screen, null)) return false;
        var data = new PointerEventData(EventSystem.current) { position = screen };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        foreach (var hit in hits) if (hit.gameObject != viewport.gameObject) return false;
        return true;
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
        RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screen, null, out before);
        zoom = next;
        board.localScale = Vector3.one * zoom;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screen, null, out after);
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
        string error = R.DungeonMove(x, y);
        if (error != null) { Say(error); return; }
        tower.SaveExpedition();
        // Keep the party in view.
        Vector2 cell = CellCenter(R.Run.px, R.Run.py) + new Vector2(-board.rect.width / 2, board.rect.height / 2);
        Vector2 onScreen = board.anchoredPosition + cell * zoom;
        float hw = viewport.rect.width / 2 - 80, hh = viewport.rect.height / 2 - 80;
        if (Mathf.Abs(onScreen.x) > hw || Mathf.Abs(onScreen.y) > hh)
        {
            board.anchoredPosition = -cell * zoom;
            ClampBoard();
        }
        RefreshDungeon();
    }

    // ---------------------------------------------------------------- rooms

    private static readonly string[] CaveThemes = { "cave", "crystal" };

    private static string RoomArt(string kind, string theme, int seed)
    {
        bool cave = Array.IndexOf(CaveThemes, theme) >= 0;
        string folder;
        switch (kind)
        {
            case "enemy": folder = "standard_combat"; break;
            case "elite": folder = "elite_combat"; break;
            case "boss": folder = cave ? "cave_boss" : "boss_arena"; break;
            case "treasure": folder = cave ? "cave_loot" : "loot"; break;
            case "rest": folder = cave ? "cave_camp" : "safe_camp"; break;
            case "merchant": folder = "merchant"; break;
            case "stairs": folder = "exit"; break;
            default: folder = new[] { "secret", "shrine", "puzzle" }[Mathf.Abs(seed) % 3]; break;
        }
        return Root + "Rooms/" + folder + "_" + (Mathf.Abs(seed) % 3);
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
            theme == "keep" ? "Old banners hang in the dark." : "Moss swallows the broken stones.";
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
        var art = Resources.Load<Texture2D>(RoomArt(room.kind, d.theme, (int)TowerForestLayouts.Hash(run.dungeonPoi, index + run.floor * 17)));
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
        var run = R.Run; var d = R.Dungeon;
        var room = d.rooms[R.PendingRoom];
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
        tower.LaunchExpeditionBattle(R.BattleDepth(room.kind), field, reserve, won =>
        {
            canvas.gameObject.SetActive(true);
            var hp = new List<int>(run.hp);
            for (int i = 0; i < run.party.Count; i++)
            {
                var unit = alive.Find(u => u.Id == run.party[i]);
                if (unit != null) hp[i] = unit.Hp <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(100f * unit.Hp / unit.MaxHp), 1, 100);
            }
            Act(R.ResolveBattle(won, hp));
            if (R.Run != null && won) Say("Victory! The spoils are added to your haul.");
        });
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
