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
    // layered grid map (TowerMapView), played as an Atlas web: the selected place, its route card, the names over places
    private TowerMapView gridView;
    private string nodeSel = "";
    private bool gridWalking;
    private int gridCenteredFor = -1;
    private GameObject gridBar;
    private RectTransform mapImage;
    private readonly List<KeyValuePair<Text, Vector2Int>> mapLabels = new List<KeyValuePair<Text, Vector2Int>>();
    private int labelVersion = -1;
    private static readonly Color RingColor = new Color(0.62f, 0.86f, 1f, 0.7f);
    private GameObject mediaBlocker;        // swallows uGUI clicks under the IMGUI media library (or codex) while it is open
    // Phone test readout on the layered maps (frame rate, frame time, memory). Switch on for map tuning only.
    public static bool ShowMapStats = false;
    private Text mapStats;
    private float statsClock;
    private int statsFrames;
    private RawImage shiftFade;             // the old forest fading out after a shift
    private RenderTexture shiftFrame;
    private float shiftClock;
    private readonly List<string> planParty = new List<string>();
    private int planRations, planTonics, planFirewood;
    private bool lootOpen;
    private bool retreatArmed;          // END EXPEDITION away from camp needs a second tap
    // Immersion (review step 7): the region card on setting out, the journal, a line of banter, sounds played once.
    private bool introOpen, journalOpen;
    private int journalTab;
    private GameObject banterBubble;
    private float banterClock;
    private int movesSinceBanter;
    private string soundedEvent = "", soundedReward = "";
    private readonly System.Random banterRng = new System.Random();
    private int dismissedAt = -1;

    // dungeon board
    private RectTransform viewport, board, token;
    private Image[] fogCells;
    private string boardKey = "";
    private float zoom = 1;
    private bool pointerDown, dragged;
    private Vector2 pointerStart, lastPointer;
    private float lastPinch;
    private RectTransform roomPanel, endPanel, autoBar;
    private readonly List<GameObject> roomBadges = new List<GameObject>();   // one per room, built with the board
    private float[] dungeonLight;
    private string boardFog = "";
    private int boardDone = -1;
    // Rooms the player turned down on this floor (NOT NOW / NOT YET): AUTO and glides pass them by.
    private readonly HashSet<int> skippedRooms = new HashSet<int>();
    private string skipKey = "", lastDungeonPoi = "";
    private int travelTargetRoom = -1;
    private const float GlideCellsPerSecond = 12.5f;     // about 0.08 s a cell
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
        TowerRules.GridMaps = true;         // new expeditions use the layered grid map
        if (R.MigratePlateRun()) tower.SaveExpedition();
        if (R.MigrateWebRun()) tower.SaveExpedition();
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
        TowerAudio.Ambience("");
        TowerAudio.Music("");
        if (canvas != null) Destroy(canvas.gameObject);
        if (gridView != null) Destroy(gridView);
        Destroy(this);
    }

    private void Go(View next)
    {
        view = next;
        lootOpen = false;
        if (gridView != null) gridView.SetActive(next == View.Forest || next == View.Region);
        Rebuild();
    }

    private void Rebuild()
    {
        CancelTravel();
        if (content != null) Destroy(content.gameObject);
        boardKey = "";
        roomPanel = endPanel = autoBar = null;
        mapLabels.Clear(); labelVersion = -1;
        Canvas.ForceUpdateCanvases();
        lastWidth = Screen.width; lastHeight = Screen.height;
        mapStats = null;
        content = Stretch("Screen", root, Ink).rectTransform;
        content.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 1);
        switch (view)
        {
            case View.Region: BuildRegion(); break;
            case View.Plan: BuildPlan(); break;
            case View.Forest: if (R.Run != null) BuildForest(); break;
            case View.Dungeon: if (R.Run != null) BuildDungeon(); break;
        }
        bool ended = R.Run == null && (view == View.Forest || view == View.Dungeon);
        if (ended && gridView != null) gridView.SetActive(false);
        if (journalOpen && !ended) BuildJournal();
        var toastBox = Box("Toast", content, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-Side / 2, -Top - 8),
            new Vector2(640, 34), new Color(0.03f, 0.05f, 0.08f, 0.88f));
        toastBox.raycastTarget = false;
        toast = TextAt(toastBox.transform, "Toast text", "", 10, 0, 620, 34, 15, Gold, TextAnchor.MiddleCenter);
        toastBox.gameObject.SetActive(false);
        if (ended) ShowSummary();
        UpdateAmbience();
        // The media library draws over everything; its blocker stays the top uGUI layer.
        if (mediaBlocker != null) mediaBlocker.transform.SetAsLastSibling();
    }

    // The looping bed for what is on screen: the dungeon's theme, the biome (with crickets after dusk), or the forest.
    private void UpdateAmbience()
    {
        var run = R.Run;
        if (run == null) { TowerAudio.Ambience(view == View.Region || view == View.Plan ? "forest" : ""); return; }
        if (view == View.Dungeon && R.Dungeon != null) TowerAudio.Ambience(TowerAudio.ThemeBed(R.Dungeon.theme));
        else if (view == View.Forest) TowerAudio.Ambience(TowerAudio.BiomeBed(run.biome), TowerRules.NightLevel(R.RunHour()));
        else TowerAudio.Ambience("forest");
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
        if (error != null) { Say(error); TowerAudio.Play("error", 0.6f); }
        else tower.SaveExpedition();
        if (R.Run == null && view != View.Region && view != View.Plan) { Rebuild(); return; }
        if (view == View.Dungeon && R.Run.dungeonPoi.Length == 0)
        {
            string last = R.State.log.Count > 0 ? R.State.log[R.State.log.Count - 1] : "";
            bool cleared = lastDungeonPoi.Length > 0 && R.Run.cleared.Contains(lastDungeonPoi);
            Go(View.Forest);
            Say(last);
            if (cleared) Sfx("atlas_complete", "reward", 0.8f);
            return;
        }
        if (rebuild) { if (view == View.Dungeon) RefreshDungeon(); else Rebuild(); }
    }

    private void Update()
    {
        if (tower == null || canvas == null || !canvas.gameObject.activeSelf) return;
        if ((Screen.width != lastWidth || Screen.height != lastHeight) && !gridWalking) Rebuild();
        if (toastTimer > 0)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0 && toast != null) toast.transform.parent.gameObject.SetActive(false);
        }
        // Names over places move only when the map camera did (pan, zoom, follow).
        if (gridView != null && gridView.CameraVersion != labelVersion && (view == View.Region || view == View.Forest))
        {
            labelVersion = gridView.CameraVersion;
            if (view == View.Region) PlaceLabels(atlasLabels, atlasImage); else PlaceLabels(mapLabels, mapImage);
        }
        if (banterBubble != null && (banterClock -= Time.unscaledDeltaTime) <= 0) { Destroy(banterBubble); banterBubble = null; }
        if (mapStats != null)
        {
            statsFrames++;
            statsClock += Time.unscaledDeltaTime;
            if (statsClock >= 0.5f)
            {
                float fps = statsFrames / statsClock;
                long mem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
                var rt = gridView != null ? gridView.Texture : null;
                mapStats.text = Mathf.RoundToInt(fps) + " fps  •  " + (1000f / Mathf.Max(1, fps)).ToString("0.0") + " ms" +
                    (mem > 0 ? "  •  " + mem + " MB" : "") + (rt != null ? "  •  map " + rt.width + "x" + rt.height : "");
                statsClock = 0; statsFrames = 0;
            }
        }
        if (shiftFrame != null)
        {
            shiftClock += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(shiftClock / 1.6f);
            if (shiftFade != null) shiftFade.color = new Color(1, 1, 1, 1 - t * t * (3 - 2 * t));
            if (t >= 1 || shiftFade == null)
            {
                if (shiftFade != null) Destroy(shiftFade.gameObject);
                shiftFade = null;
                shiftFrame.Release(); Destroy(shiftFrame); shiftFrame = null;
            }
        }
        if (view == View.Dungeon && R != null && R.Run != null && R.Dungeon != null && board != null && viewport != null)
        {
            UpdateTravel();
            if (!MediaPanel.IsOpen && !CodexPanel.IsOpen) DungeonInput();
        }
    }

    private void OnDisable() { CancelTravel(); }

    // ---------------------------------------------------------------- widgets

    // A recorded effect from Resources/AdamsHaven/Audio/Sfx/<id> when it exists, else a synthesised stand-in.
    private static void Sfx(string id, string fallback, float volume = 0.8f)
    {
        if (TowerAudio.Has("Sfx/" + id)) TowerAudio.Play("Sfx/" + id, volume);
        else if (!string.IsNullOrEmpty(fallback)) TowerAudio.Play(fallback, volume);
    }

    // The media library (IMGUI) over the expedition: a clear uGUI blocker under it eats clicks meant for it.
    private void OpenMedia()
    {
        if (MediaPanel.IsOpen || CodexPanel.IsOpen) return;
        if (mediaBlocker == null)
        {
            var blocker = Stretch("Media blocker", root, new Color(0, 0, 0, 0));
            blocker.raycastTarget = true;
            mediaBlocker = blocker.gameObject;
        }
        mediaBlocker.transform.SetAsLastSibling();
        MediaPanel.Show(null, null, () =>
        {
            if (mediaBlocker != null) Destroy(mediaBlocker);
            mediaBlocker = null;
            Rebuild();
        });
    }

    // The card codex (IMGUI) over the expedition, behind the same click blocker as the media library.
    private void OpenCodex(string tab)
    {
        if (MediaPanel.IsOpen || CodexPanel.IsOpen) return;
        if (mediaBlocker == null)
        {
            var blocker = Stretch("Media blocker", root, new Color(0, 0, 0, 0));
            blocker.raycastTarget = true;
            mediaBlocker = blocker.gameObject;
        }
        mediaBlocker.transform.SetAsLastSibling();
        CodexPanel.Show(tab, null, CodexContext.From(R), () =>
        {
            if (mediaBlocker != null) Destroy(mediaBlocker);
            mediaBlocker = null;
            Rebuild();
        });
    }

    private static Sprite White { get { if (white == null) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f)); return white; } }

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
        if (action != null) button.onClick.AddListener(() => { TowerAudio.Play("click", 0.7f); action(); });
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
        float right = root.rect.width - 230;
        ButtonAt(bar, "Sound", TowerAudio.Muted ? "SOUND OFF" : "SOUND ON", right - 112, 7, 104, 38,
            () => { TowerAudio.Muted = !TowerAudio.Muted; Rebuild(); }, TowerAudio.Muted ? new Color(0.35f, 0.37f, 0.4f) : Moss, 12);
        right -= 120;
        if (R.Run != null && (view == View.Forest || view == View.Dungeon))
        {
            TextAt(bar, "Clock", R.ClockLine(), right - 380, 0, 372, Top, 14, Cream, TextAnchor.MiddleRight);
            right -= 388;
        }
        TextAt(bar, "Title", title, 16, 0, Mathf.Max(120, right - 16), Top, 20, Gold);
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

    // Textures by Resources path, looked up once (the board asked for its terrain once per cell; screens are rebuilt often).
    private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    private static Texture2D Tex(string path)
    {
        Texture2D tex;
        if (textures.TryGetValue(path, out tex) && (tex != null || !Application.isEditor)) return tex;
        tex = Resources.Load<Texture2D>(path);
        textures[path] = tex;
        return tex;
    }

    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private static Sprite Load(string path)
    {
        Sprite sprite;
        if (sprites.TryGetValue(path, out sprite)) return sprite;
        var tex = Tex(path);
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

    private void BuildRegion() { BuildAtlas(); }

    // Right-hand panel of the region screen (old region map and Atlas alike).
    private void BuildRegionPanel()
    {
        var run = R.Run;
        float width = root.rect.width;
        var panel = PanelAt("Region panel", content, width - Side - 12, Top + 12, Side, 376);
        var region0 = TowerRules.Region(selectedRegion);
        if (region0 == null)
        {
            TextAt(panel.transform, "Hint", run != null ?
                "An expedition is under way in " + TowerRules.Region(run.region).name + "." :
                "Choose a region. Start near Silverbrook; conquering a region's lair opens the lands beyond it.",
                18, 18, Side - 36, 120, 16, Cream);
            if (run != null) ButtonAt(panel.transform, "Resume", "RESUME EXPEDITION", 18, 150, Side - 36, 48, Resume, Teal, 17);
            ButtonAt(panel.transform, "Journal", "GUILD JOURNAL", 18, 206, Side - 36, 38, OpenJournal, Moss, 14);
            ButtonAt(panel.transform, "Media", "MEDIA LIBRARY", 18, 252, Side - 36, 38, OpenMedia, new Color(0.3f, 0.34f, 0.46f), 14);
            return;
        }
        bool isUnlocked = R.RegionUnlocked(region0.id);
        TextAt(panel.transform, "Name", region0.name.ToUpperInvariant(), 18, 14, Side - 36, 30, 20, Gold);
        TextAt(panel.transform, "Blurb", region0.blurb, 18, 46, Side - 36, 46, 15, Cream);
        TextAt(panel.transform, "Stats", "Danger " + region0.depth + "   •   Loot x" + region0.reward.ToString("0.0") +
            "\n" + (R.RegionConquered(region0.id) ? "CONQUERED" + OutpostLine(region0.id) : isUnlocked ? "Open to explore" : R.RegionLockReason(region0.id)), 18, 96, Side - 36, 60, 15,
            isUnlocked ? Cream : new Color(1f, 0.7f, 0.6f));
        TextAt(panel.transform, "Rule", "Every expedition finds this forest rearranged. Clear its lair to conquer the region and open the way beyond.",
            18, 160, Side - 36, 60, 13, new Color(0.8f, 0.85f, 0.9f));
        if (run != null)
            ButtonAt(panel.transform, "Resume", "RESUME EXPEDITION", 18, 226, Side - 36, 46, Resume, Teal, 17);
        else
            ButtonAt(panel.transform, "Plan", "PLAN EXPEDITION", 18, 226, Side - 36, 46, OpenPlan, Teal, 17, isUnlocked);
        ButtonAt(panel.transform, "Journal", "GUILD JOURNAL", 18, 278, Side - 36, 38, OpenJournal, Moss, 14);
        ButtonAt(panel.transform, "Media", "MEDIA LIBRARY", 18, 324, Side - 36, 38, OpenMedia, new Color(0.3f, 0.34f, 0.46f), 14);
    }

    // ---------------------------------------------------------------- Atlas (layered region map)

    private readonly List<KeyValuePair<Text, Vector2Int>> atlasLabels = new List<KeyValuePair<Text, Vector2Int>>();
    private RectTransform atlasImage;

    private void BuildAtlas()
    {
        float width = root.rect.width, height = root.rect.height, areaW = width - Side - 24, areaH = height - Top;
        var image = new GameObject("Atlas view", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(content, false);
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(0, -Top); rect.sizeDelta = new Vector2(areaW, areaH);
        atlasImage = rect;
        if (gridView == null) gridView = gameObject.AddComponent<TowerMapView>();
        float scale = canvas.scaleFactor;
        gridView.Resize(Mathf.RoundToInt(areaW * scale), Mathf.RoundToInt(areaH * scale));
        bool fresh = gridView.Family != "atlas";
        var source = new TowerAtlasSource(R);
        gridView.Bind(source);
        gridView.SetActive(true);
        if (fresh) gridView.ShowAll();
        gridCenteredFor = -1;           // the run map recentres on the party when it comes back
        image.texture = gridView.Texture;
        var input = image.gameObject.AddComponent<TowerMapInput>();
        input.Tapped = TapAtlas;
        input.Dragged = d => gridView.Pan(d);
        input.Zoomed = (f, at) => gridView.ZoomBy(f, at);
        var selected = TowerAtlas.Map.Poi(selectedRegion);
        if (selected != null) gridView.ShowPreview(new List<Vector2Int> { new Vector2Int(selected.x, selected.y), new Vector2Int(selected.x, selected.y) });
        else gridView.ClearPreview();
        // Names float over the places the guild knows of.
        atlasLabels.Clear();
        foreach (var p in TowerAtlas.Map.pois)
        {
            bool seen = source.Seen(p.x, p.y);
            if (!seen && !source.Known(p)) continue;
            var region = TowerRules.Region(p.id);
            bool open = region == null || R.RegionUnlocked(p.id);
            string name = p.name.ToUpperInvariant() + (region != null && R.Outpost(p.id) != null ? "  (OUTPOST)" :
                region != null && R.RegionConquered(p.id) ? "  (CONQUERED)" : open ? "" : "  (LOCKED)");
            var label = TextAt(rect, "Atlas label " + p.id, name, 0, 0, 220, 22, p.id == selectedRegion ? 15 : 13,
                p.id == selectedRegion ? Gold : open ? Cream : new Color(0.7f, 0.74f, 0.8f), TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.pivot = new Vector2(0.5f, 1);
            label.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            atlasLabels.Add(new KeyValuePair<Text, Vector2Int>(label, new Vector2Int(p.x, p.y)));
        }
        PlaceLabels(atlasLabels, atlasImage);
        labelVersion = gridView.CameraVersion;
        TopBar("THE SILVERWOOD ATLAS  •  GUILD EXPEDITIONS", "BACK TO TOWER", () => tower.CloseExpedition(null));
        BuildRegionPanel();
        AddMapStats(areaW);
    }

    // The Tower's outpost in a conquered region (TowerOutposts.cs), for the Atlas region panel.
    private string OutpostLine(string region)
    {
        var outpost = R.Outpost(region);
        if (outpost == null) return TowerRules.OutpostSpec(region) == null ? "" : "  •  outpost site open (found it from the Guild)";
        return "  •  OUTPOST: " + TowerRules.OutpostSpec(region).name + " " + TowerTiers.Tier(outpost.level) + ", " +
            outpost.staff.Count + " staff";
    }

    private void AddMapStats(float areaW)
    {
        if (!ShowMapStats) return;
        mapStats = TextAt(content, "Map stats", "", areaW - 440, Top + 6, 430, 20, 12, Cream, TextAnchor.MiddleRight);
        mapStats.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
        statsClock = 0; statsFrames = 0;
    }

    // Names float just under their places (the Atlas regions, the run map's places).
    private void PlaceLabels(List<KeyValuePair<Text, Vector2Int>> labels, RectTransform image)
    {
        if (image == null || gridView == null) return;
        float w = image.rect.width, h = image.rect.height;
        foreach (var pair in labels)
        {
            if (pair.Key == null) continue;
            var vp = gridView.CellToViewport(pair.Value.x, pair.Value.y);
            pair.Key.rectTransform.anchoredPosition = new Vector2(vp.x * w, -(1 - vp.y) * h - 10);
            pair.Key.enabled = vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
        }
    }

    private void TapAtlas(Vector2 viewport)
    {
        var cell = gridView.CellAt(viewport);
        var poi = TowerAtlas.RegionNear(cell.x, cell.y, 3);
        if (poi == null || poi.id == TowerAtlas.HomeId) { selectedRegion = ""; Rebuild(); return; }
        var source = new TowerAtlasSource(R);
        if (!source.Seen(poi.x, poi.y) && !source.Known(poi)) { Say("Unknown lands. Conquer the regions before them to find the way."); return; }
        selectedRegion = poi.id;
        Rebuild();
    }

    private void Resume()
    {
        var run = R.Run;
        if (run == null) return;
        Go(run.dungeonPoi.Length > 0 ? View.Dungeon : View.Forest);
    }

    // ---------------------------------------------------------------- plan

    private void OpenPlan() { OpenParty(); }

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
        MapFrame(Tex(Root + "Maps/region"), 0, Top, width, height - Top, new Color(0.35f, 0.38f, 0.42f));
        var region = TowerRules.Region(selectedRegion);
        TopBar("PLAN EXPEDITION  •  " + region.name.ToUpperInvariant(), "BACK", () => Go(View.Region));
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
            var chibi = Tex("AdamsHaven/Chibi/" + id);
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
        Stepper(panel, 20, 300, "RATIONS", planRations, R.MaxRations, "Eaten by distance walked; roads are cheaper. Each: " + TowerRules.RationFood +
            " food + " + TowerRules.RationWater + " water.", v => planRations = v);
        Stepper(panel, 20, 350, "TONICS", planTonics, R.State.tonics, "Heal 40% or revive a fallen fighter.", v => planTonics = v);
        Stepper(panel, 20, 400, "FIREWOOD", planFirewood, R.MaxFirewood, "Full rest at camps and fires. Each: " +
            TowerRules.FirewoodBundle + " firewood.", v => planFirewood = v);
        string error = R.CanStartExpedition(selectedRegion, planParty, planRations, planTonics, planFirewood);
        TextAt(panel, "Rules", "Loot you find is your HAUL. Head home from a camp to bank all of it; leaving from anywhere else drops a quarter. " +
            "If the whole party falls, only the 2-slot SAFE POCKET comes home (experience is always kept).", 20, 452, pw - 300, 60, 13,
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
        introOpen = true;
        movesSinceBanter = 0;
        Go(View.Forest);
        TowerAudio.Play("chime", 0.8f);
    }

    // ---------------------------------------------------------------- run map: the Atlas web

    // The run's grid map, played like the Path of Exile 2 Atlas: places are nodes on a web of trails; tap one to see the
    // way there and its cost, tap again (or GO) and the party walks the whole route. Only places linked to a completed
    // one can be reached; completing a place brings its neighbours out of the fog.
    private void BuildForest()
    {
        var run = R.Run;
        float width = root.rect.width, height = root.rect.height, areaW = width - Side - 12, areaH = height - Top;
        var image = new GameObject("Map view", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(content, false);
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(0, -Top); rect.sizeDelta = new Vector2(areaW, areaH);
        mapImage = rect;
        if (gridView == null) gridView = gameObject.AddComponent<TowerMapView>();
        float scale = canvas.scaleFactor;
        gridView.Resize(Mathf.RoundToInt(areaW * scale), Mathf.RoundToInt(areaH * scale));
        gridView.Bind(new TowerRunMapSource(R));
        gridView.SetThreat(run.threat / (float)TowerRules.ThreatMax);
        gridView.SetActive(true);
        if (gridView.LastRevealed > 0 && !gridWalking) Sfx("atlas_reveal", "chime", 0.7f);
        UpdateWeb();
        TowerAudio.Music("atlas");
        image.color = DayTint(R.RunHour());
        BuildWeather(rect, R.Weather);
        if (gridCenteredFor != run.gridSeed + run.shift * 31) { gridCenteredFor = run.gridSeed + run.shift * 31; gridView.CenterOnParty(); }
        image.texture = gridView.Texture;
        var frame = gridView.TakeShiftFrame();
        if (frame != null)
        {
            // The forest moved: show the old trees and let them dissolve into the new arrangement.
            if (shiftFrame != null) { shiftFrame.Release(); Destroy(shiftFrame); }
            shiftFrame = frame; shiftClock = 0;
            shiftFade = new GameObject("Forest shift", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            shiftFade.transform.SetParent(content, false);
            var fr = shiftFade.rectTransform;
            fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0, 1);
            fr.anchoredPosition = rect.anchoredPosition; fr.sizeDelta = rect.sizeDelta;
            shiftFade.texture = frame; shiftFade.raycastTarget = false;
        }
        var input = image.gameObject.AddComponent<TowerMapInput>();
        input.Tapped = TapNode;
        input.Dragged = d => { if (!gridWalking) gridView.Pan(d); };
        input.Zoomed = (f, at) => gridView.ZoomBy(f, at);
        gridBar = null;
        if (nodeSel.Length > 0 && R.Overworld.Poi(nodeSel) != null) SelectNode(nodeSel);
        else { nodeSel = ""; gridView.ClearPreview(); gridView.HideRing(); BuildNodeLabels(); }

        TopBar(R.RunRegion.name.ToUpperInvariant() + "  •  " + R.RunLayout.name.ToUpperInvariant(), "RETURN TO TOWER",
            () => { tower.CloseExpedition(null); });
        BuildRunPanel(false);
        if (lootOpen) BuildLoot();
        else if (introOpen) BuildIntroCard();
        else BuildEventCard();
        AddMapStats(areaW);
        // A trip an event interrupted carries on by itself once the event is settled.
        if (!gridWalking && !lootOpen && !introOpen && R.GridHasTarget && R.EventBlock() == null && string.IsNullOrEmpty(run.eventResult))
        {
            var step = R.GridResume();
            if (step.error != null) { R.GridCancelTarget(); Say(step.error); }
            else StartGridWalk(step);
        }
    }

    // The web's trails between places the party knows of: faint, brighter from a completed place, gold once travelled.
    private void UpdateWeb()
    {
        var web = R.Web; var run = R.Run;
        if (web == null || gridView == null) return;
        var lines = new List<TowerMapView.WebLine>();
        var key = new System.Text.StringBuilder();
        foreach (var e in web.edges)
        {
            int a = R.NodeState(e.a), b = R.NodeState(e.b);
            if (a < TowerRules.NodeScouted || b < TowerRules.NodeScouted) continue;
            int style = run.road.Contains(e.key) ? 2 : a == TowerRules.NodeDone || b == TowerRules.NodeDone ? 1 : 0;
            lines.Add(new TowerMapView.WebLine { cells = e.path, style = style });
            key.Append(e.key).Append(style).Append(';');
        }
        key.Append(run.shift);
        gridView.SetWeb(key.ToString(), lines);
    }

    private static string TierHex(int tier)
    {
        int band = TowerRules.TierBand(tier);
        return band == 0 ? "F2EEE0" : band == 1 ? "F5C842" : "EE5A44";
    }

    private static string TierText(int tier) { return "<color=#" + TierHex(tier) + ">TIER " + TowerRules.Roman(tier) + "</color>"; }

    // Name and tier over every place the party knows of (and the lair's beacon).
    private void BuildNodeLabels()
    {
        foreach (var pair in mapLabels) if (pair.Key != null) Destroy(pair.Key.gameObject);
        mapLabels.Clear();
        if (mapImage == null || R.Overworld == null) return;
        foreach (var p in R.Overworld.pois)
        {
            int state = R.NodeState(p.id);
            if (state < TowerRules.NodeBeacon) continue;
            bool sel = p.id == nodeSel;
            var color = sel ? Gold : state == TowerRules.NodeDone ? new Color(0.72f, 0.8f, 0.72f) :
                state == TowerRules.NodeOpen ? Cream : new Color(0.66f, 0.7f, 0.78f);
            var label = TextAt(mapImage, "Place label " + p.id, NodeLabel(p, state), 0, 0, 220, 34, sel ? 14 : 12, color, TextAnchor.UpperCenter);
            label.fontStyle = FontStyle.Bold;
            label.supportRichText = true;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.pivot = new Vector2(0.5f, 1);
            label.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            mapLabels.Add(new KeyValuePair<Text, Vector2Int>(label, new Vector2Int(p.x, p.y)));
        }
        PlaceLabels(mapLabels, mapImage);
        if (gridView != null) labelVersion = gridView.CameraVersion;
    }

    private string NodeLabel(TowerOverworldPoi p, int state)
    {
        string name = p.kind == "lair" && state == TowerRules.NodeBeacon ? "THE LAIR" : p.name.ToUpperInvariant();
        if (p.kind == "camp") return name;
        string second;
        if (state == TowerRules.NodeDone) second = p.kind == "tower" ? "climbed" : TowerRules.VisitKind(p.kind) ? "visited" : "cleared";
        else if (TowerRules.ModdedKind(p.kind))
        {
            int mods = R.NodeModIds(p.id).Count;
            second = TierText(R.NodeTier(p.id)) + (mods > 0 ? "  •  " + mods + (mods == 1 ? " mod" : " mods") : "");
        }
        else second = p.kind == "tower" ? "tower" : p.kind;
        if (state == TowerRules.NodeScouted || state == TowerRules.NodeBeacon) second += "  •  out of reach";
        return name + "\n<size=11>" + second + "</size>";
    }

    private void TapNode(Vector2 viewport)
    {
        if (gridWalking || R.Run == null) return;
        var map = gridView.Map;
        var cell = gridView.CellAt(viewport);
        // A tap on a place's glade or on its picture (drawn standing above the cell) means the place.
        TowerOverworldPoi best = null; int bestD = 9;
        foreach (var p in map.pois)
        {
            if (R.NodeState(p.id) < TowerRules.NodeBeacon) continue;
            int d = Mathf.Min((p.x - cell.x) * (p.x - cell.x) + (p.y - cell.y) * (p.y - cell.y),
                (p.x - cell.x) * (p.x - cell.x) + (p.y - 2 - cell.y) * (p.y - 2 - cell.y));
            if (d < bestD) { bestD = d; best = p; }
        }
        if (best == null) { ClearNodeSelection(); return; }
        if (best.id == nodeSel) { TravelGo(); return; }
        TowerAudio.Play("click", 0.5f);
        SelectNode(best.id);
    }

    // Selects a place: its route on the map (a tower also shows the circle its view covers) and the route card.
    private void SelectNode(string id)
    {
        var p = R.Overworld.Poi(id);
        if (p == null) { ClearNodeSelection(); return; }
        nodeSel = id;
        var route = R.AtlasRoute(id);
        if (route.error == null && route.path.Count > 1) gridView.ShowPreview(route.path); else gridView.ClearPreview();
        if (p.kind == "tower") gridView.ShowRing(new Vector2Int(p.x, p.y), TowerRules.TowerRadius, RingColor); else gridView.HideRing();
        BuildRouteCard(route, p);
        BuildNodeLabels();
    }

    private void ClearNodeSelection(bool relabel = true)
    {
        bool had = nodeSel.Length > 0;
        nodeSel = "";
        if (gridView != null) { gridView.ClearPreview(); gridView.HideRing(); }
        if (gridBar != null) { Destroy(gridBar); gridBar = null; }
        if (had && relabel) BuildNodeLabels();
    }

    // Route card along the bottom of the map: the place, its tier and mods, what the trip costs, and GO.
    private void BuildRouteCard(TowerAtlasRoute route, TowerOverworldPoi poi)
    {
        if (gridBar != null) Destroy(gridBar);
        var run = R.Run;
        var node = R.RunLayout.Node(poi.id);
        int state = R.NodeState(poi.id);
        bool cleared = run.cleared.Contains(poi.id);
        var mods = cleared ? new List<TowerMapMod>() : R.NodeMods(poi.id);
        float areaW = root.rect.width - Side - 12, w = Mathf.Min(700, areaW - 24), h = 102 + mods.Count * 18;
        var panel = PanelAt("Route", content, (areaW - w) / 2, root.rect.height - h - 22, w, h);
        gridBar = panel.gameObject;
        string title = (state == TowerRules.NodeBeacon ? "The Lair" : poi.name) + (cleared ? "  (cleared)" : "");
        if (TowerRules.ModdedKind(poi.kind) && !cleared) title += "    " + TierText(R.NodeTier(poi.id));
        string kind = PlaceKind(node);
        string hint = !cleared ? R.PlaceHint(node) : "";
        if (hint.Length > 0) kind += "  •  " + hint;
        var icon = Icon(poi.kind);
        float tx = 40, textW = w - tx - 200;
        if (icon != null)
        {
            var glyph = At("Route icon", panel.transform, 38, 14, 34, 34, Color.white);
            glyph.sprite = icon; glyph.preserveAspect = true; glyph.raycastTarget = false;
            tx = 80; textW = w - tx - 200;
        }
        var titleText = TextAt(panel.transform, "Route title", title, tx, 10, textW, 24, 16, Gold);
        titleText.supportRichText = true;
        TextAt(panel.transform, "Route kind", kind, tx, 34, textW, 18, 12, Cream);
        float y = 54;
        foreach (var m in mods)
        {
            TextAt(panel.transform, "Route mod " + m.id, "•  " + m.Line, tx, y, textW, 18, 12, new Color(1f, 0.78f, 0.55f));
            y += 18;
        }
        if (route.Here)
        {
            TextAt(panel.transform, "Route cost", "You are here.", tx, y + 4, textW, 18, 12, new Color(0.8f, 0.86f, 0.92f));
            return;
        }
        if (route.error != null)
        {
            TextAt(panel.transform, "Route cost", route.error, tx, y + 4, textW, 18, 12, new Color(1f, 0.6f, 0.5f));
            return;
        }
        int roadPct = route.roadCells + route.trailCells == 0 ? 0 : Mathf.RoundToInt(100f * route.roadCells / (route.roadCells + route.trailCells));
        int stops = route.hops.Count - 2;
        string cost = route.rations.ToString("0.0") + " rations  •  +" + route.threat + " threat  •  ~" + route.hours.ToString("0.0") + " h  •  " +
            roadPct + "% on road" + (stops > 0 ? "  •  via " + stops + (stops == 1 ? " place" : " places") : "");
        // Telegraph the shift: this trip would fill the threat meter.
        bool shifts = run.threat + route.threat >= TowerRules.ThreatMax;
        if (shifts) cost += "  •  THE FOREST WILL SHIFT";
        TextAt(panel.transform, "Route cost", cost, tx, y + 4, textW, 18, 12,
            shifts || route.rations > run.rations + 0.01f ? new Color(1f, 0.6f, 0.5f) : new Color(0.8f, 0.86f, 0.92f));
        ButtonAt(panel.transform, "Go", "GO", w - 186, (h - 48) / 2, 146, 48, TravelGo, Teal, 18);
    }

    private static string GroundName(TowerTerrain t)
    {
        switch (t)
        {
            case TowerTerrain.DenseForest: return "Deep forest";
            case TowerTerrain.Forest: return "Forest";
            case TowerTerrain.Clearing: return "Clearing";
            case TowerTerrain.Road: return "Old road";
            case TowerTerrain.Bridge: return "Bridge";
            case TowerTerrain.Ford: return "Ford";
            case TowerTerrain.Hill: return "Hillside";
            case TowerTerrain.RuinGround: return "Ruins";
            case TowerTerrain.Blight: return "Blighted ground";
            case TowerTerrain.Marsh: return "Marsh";
            default: return TowerForestLayouts.Pretty(t.ToString().ToLowerInvariant());
        }
    }

    private void TravelGo()
    {
        if (gridWalking || nodeSel.Length == 0) return;
        var step = R.AtlasTravel(nodeSel);
        if (step.error != null) { Say(step.error); TowerAudio.Play("error", 0.6f); return; }
        Sfx("atlas_travel", "confirm", 0.7f);
        StartGridWalk(step);
    }

    // The rules already made the whole trip; the map walks the party along it, faster on long routes.
    private void StartGridWalk(TowerGridStep step)
    {
        if (step.error != null) { Say(step.error); return; }
        ClearNodeSelection(false);
        tower.SaveExpedition();
        gridWalking = true;
        MakeCurtain(0);         // swallows taps while the party is on the move
        float speed = Mathf.Clamp(step.walked.Count / 28f, 1f, 2.4f);
        gridView.Walk(step.walked, () =>
        {
            gridWalking = false;
            Act(null);
            if (R.Run == null) return;
            if (step.shifted) { Say("The forest shifts! Places nobody had seen have moved."); TowerAudio.Play("shift"); }
            else if (step.towerClimbed) { Sfx("tower_activate", "chime", 0.9f); Say("From the tower the woods lie open. Set a tablet."); }
            else if (step.arrived != null && !step.halted && R.EventBlock() == null) Say("Arrived: " + step.arrived.name + ".");
            if (!step.halted && R.EventBlock() == null && !R.RewardPending) MaybeBanter(false);
        }, speed);
    }

    private static string PlaceKind(TowerForestNode node)
    {
        int floors = TowerForestLayouts.Floors(node.kind);
        if (node.kind == "camp") return "Camp";
        if (node.kind == "lair") return "The region's lair  •  " + floors + " floors";
        if (node.kind == "tower") return "Tower  •  " + ThemeName(node.theme);
        if (node.kind == "merchant" || node.kind == "shrine") return TowerForestLayouts.Pretty(node.kind) + "  •  " + ThemeName(node.theme);
        return (node.kind == "landmark" ? "Landmark" : TowerForestLayouts.Pretty(node.kind)) + "  •  " + ThemeName(node.theme) +
            ", " + floors + (floors == 1 ? " floor" : " floors");
    }

    private void MakeCurtain(float alpha)
    {
        Stretch("Curtain", content, new Color(0, 0, 0, alpha)).raycastTarget = true;
    }

    // Right-hand panel shared by the forest map and the dungeon.
    private void BuildRunPanel(bool dungeon)
    {
        var run = R.Run;
        float width = root.rect.width, height = root.rect.height;
        var panel = PanelAt("Run panel", content, width - Side - 6, Top + 6, Side, height - Top - 12).transform;
        float y = 12;
        var here = dungeon ? null : R.RunLayout.Node(run.at);
        if (!dungeon && here == null)
        {
            // Only an old save or a broken trip leaves the party between places.
            var near = R.NearestPoi(run.cx, run.cy);
            TextAt(panel, "Node", "THE WILDS", 16, y, Side - 32, 26, 18, Gold); y += 26;
            TextAt(panel, "Kind", GroundName(R.Overworld.At(run.cx, run.cy)) + (near != null && R.NodeVisible(near.id) ? "  •  near " + near.name : ""),
                16, y, Side - 32, 40, 13, Cream); y += 44;
        }
        if (!dungeon && here != null)
        {
            var node = here;
            bool cleared = run.cleared.Contains(node.id);
            TextAt(panel, "Node", node.name.ToUpperInvariant(), 16, y, Side - 32, 26, 18, Gold); y += 26;
            string kind = node.kind == "camp" ? "Camp: rest by the fire" : node.kind == "lair" ? "The region's lair: " +
                TowerForestLayouts.Floors(node.kind) + " floors, boss at the bottom" : node.kind == "tower" ? TowerLine(node.id) :
                (node.kind == "landmark" ? "Landmark" : TowerForestLayouts.Pretty(node.kind)) + "  •  " + ThemeName(node.theme) +
                (node.kind == "merchant" || node.kind == "shrine" ? "" : ", " + TowerForestLayouts.Floors(node.kind) +
                (TowerForestLayouts.Floors(node.kind) == 1 ? " floor" : " floors"));
            TextAt(panel, "Kind", cleared ? "Cleared." : kind, 16, y, Side - 32, 36, 13, Cream); y += 38;
            if (R.GridRun && TowerRules.ModdedKind(node.kind) && !cleared)
            {
                // Tier and map mods of the place (waystone style).
                var mods = R.NodeMods(node.id);
                var names = mods.ConvertAll(m => m.name);
                var tierLine = TextAt(panel, "Tier", TierText(R.NodeTier(node.id)) + (names.Count > 0 ? "  •  " + string.Join(", ", names.ToArray()) : "  •  no mods"),
                    16, y, Side - 32, 20, 12, new Color(1f, 0.82f, 0.6f));
                tierLine.supportRichText = true;
                y += 22;
            }
            if (node.kind == "camp")
            {
                ButtonAt(panel, "Rest", "REST AT CAMP (1 firewood)", 16, y, Side - 32, 42, () => { string e = R.CampRest(); Act(e); if (e == null) MaybeBanter(true); }, Teal, 14, run.firewood > 0);
                y += 48;
                float half = (Side - 40) / 2;
                ButtonAt(panel, "Cook", "COOK (1 ration)", 16, y, half, 38, () => Act(R.CampCook()), Teal, 12, run.rations > 0);
                ButtonAt(panel, "Scout", "SCOUT (2 hours)", 24 + half, y, half, 38, () => Act(R.CampScout()), Teal, 12, R.GridRun);
                y -= 8;
            }
            else if (node.kind != "tower")
                ButtonAt(panel, "Enter", "ENTER " + (node.kind == "landmark" ? "LANDMARK" : node.kind == "merchant" ? "THE STALL" :
                    node.kind == "shrine" ? "THE SHRINE" : "DUNGEON"), 16, y, Side - 32, 42,
                    () => { string e = R.EnterPoi(); if (e != null) Say(e); else { tower.SaveExpedition(); TowerAudio.Play("door"); Go(View.Dungeon); } },
                    Teal, 16, !cleared);
            if (node.kind != "tower") y += 50;
        }
        if (!dungeon)
        {
            string effect = TowerRules.WeatherEffect(R.Weather);
            TextAt(panel, "Travel", effect.Length > 0 ? effect + " Tap a place, then GO." :
                "Tap a place for its route and cost; tap again or GO to travel. Clear a place to open the places linked to it.",
                16, y, Side - 32, 62, 12, effect.Length > 0 ? new Color(0.75f, 0.88f, 1f) : new Color(0.8f, 0.85f, 0.9f));
            y += 66;
        }
        else
        {
            var node = R.RunLayout.Node(run.dungeonPoi);
            var d = R.Dungeon;
            TextAt(panel, "Node", node.name.ToUpperInvariant(), 16, y, Side - 32, 26, 18, Gold); y += 26;
            TextAt(panel, "Floor", ThemeName(d.theme) + "  •  Floor " + (d.floor + 1) + " / " + d.floors, 16, y, Side - 32, 22, 14, Cream); y += 24;
            if (R.GridRun && TowerRules.ModdedKind(node.kind))
            {
                var mods = R.NodeMods(node.id);
                var names = mods.ConvertAll(m => m.name);
                var tierLine = TextAt(panel, "Tier", TierText(R.NodeTier(node.id)) + (names.Count > 0 ? "  •  " + string.Join(", ", names.ToArray()) : ""),
                    16, y, Side - 32, 20, 12, new Color(1f, 0.82f, 0.6f));
                tierLine.supportRichText = true;
                y += 22;
            }
            var vista = Tex(TowerDungeonIllustration.ArtRoot + d.theme + "_vista");
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
            TextAt(panel, "Help", "Tap a room to glide there. AUTO (space) heads for the nearest room left to explore; GO TO GOAL once it is found. Drag to pan, wheel or pinch to zoom.",
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
        if (run.relics != null && run.relics.Count > 0)
        {
            // Run relics (TowerRunRewards), lost when the expedition ends.
            var names = run.relics.ConvertAll(id => TowerRules.Relic(id) != null ? TowerRules.Relic(id).name : id);
            TextAt(panel, "Relics", "Relics: " + string.Join(", ", names.ToArray()), 16, y, Side - 32, 36, 12, new Color(1f, 0.85f, 0.5f));
            y += 38;
        }
        float third = (Side - 48) / 3;
        ButtonAt(panel, "Loot", "HAUL", 16, y, third, 36, () => { lootOpen = true; if (view == View.Dungeon) BuildLoot(); else Rebuild(); }, Teal, 13);
        ButtonAt(panel, "Journal", "JOURNAL", 24 + third, y, third, 36, OpenJournal, Moss, 13);
        ButtonAt(panel, "Media", "MEDIA LIBRARY", 32 + third * 2, y, third, 36, OpenMedia, new Color(0.3f, 0.34f, 0.46f), 11);
        y += 44;
        if (dungeon)
            ButtonAt(panel, "Leave", "LEAVE DUNGEON", 16, y, Side - 32, 40, () => Act(R.LeaveDungeon()), Alert, 15);
        else
        {
            // Heading home from the camp banks everything; anywhere else is a retreat that drops part of the haul.
            int loss = R.RetreatLoss;
            string label = R.AtSafeExit ? "HEAD HOME FROM CAMP (bank haul)" :
                retreatArmed ? "TAP AGAIN: RETREAT, LOSE " + loss + "g" : "RETREAT HOME (lose " + Mathf.RoundToInt(TowerRules.RetreatTax * 100) + "% of haul)";
            ButtonAt(panel, "Abandon", label, 16, y, Side - 32, 40, () =>
            {
                if (!R.AtSafeExit && !retreatArmed) { retreatArmed = true; Rebuild(); return; }
                retreatArmed = false;
                R.EndExpedition(false);
                tower.SaveExpedition();
                Rebuild();
            }, Alert, 13);
        }
    }

    // A tower's line in the run panel: climbed or not, and the tablet set there.
    private string TowerLine(string id)
    {
        var run = R.Run;
        if (!R.NodeComplete(id)) return "Tower: climb it to see " + TowerRules.TowerRadius + " cells around and set a tablet";
        string set = run.tablets.Find(t => t.StartsWith(id + ":", StringComparison.Ordinal));
        var mod = set == null ? null : TowerRules.TabletMod(set);
        return mod != null ? "Tower: a " + mod.name + " tablet is set (" + R.TabletReach(set) + " places in its circle)" : "Tower: climbed";
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
        lastDungeonPoi = R.Run.dungeonPoi;
        string floorKey = R.Run.dungeonPoi + ":" + R.Run.floor;
        if (floorKey != skipKey) { skipKey = floorKey; skippedRooms.Clear(); }
        BuildBoard();
        TopBar(R.RunRegion.name.ToUpperInvariant() + "  •  " + R.RunLayout.Node(R.Run.dungeonPoi).name.ToUpperInvariant(), null, null);
        BuildRunPanel(true);
        TowerAudio.Music("dungeon");
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
            mat.GetComponent<RawImage>().texture = Tex(Root + "Dungeon/Silverbrook/" + d.theme + "_terrain");
            mat.GetComponent<RawImage>().raycastTarget = false;
            mat.GetComponent<RawImage>().color = new Color(0.24f, 0.29f, 0.35f);
            fogCells = new Image[TowerDungeon.Size * TowerDungeon.Size];
            var terrain = Tex(Root + "Dungeon/Silverbrook/" + d.theme + "_terrain");
            for (int y = 0; y < TowerDungeon.Size; y++)
                for (int x = 0; x < TowerDungeon.Size; x++)
                {
                    if (terrain != null && (d.Walkable(x, y) || IsDungeonWall(d, x, y)))
                    { BuildTerrainCell(d, x, y, terrain); BuildTerrainEdges(d, x, y); continue; }
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
            var chibi = Tex("AdamsHaven/Chibi/" + run.party[i]);
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
        // One badge per room that holds something, built once and only shown or hidden as the fog lifts.
        roomBadges.Clear();
        for (int i = 0; i < d.rooms.Count; i++)
        {
            var room = d.rooms[i];
            if (room.kind == "empty" || room.kind == "entrance") { roomBadges.Add(null); continue; }
            var position = CellCenter(room.CenterX, room.CenterY);
            position.y += (room.h * 0.5f - 0.65f) * CellPx;
            var badge = At("Encounter marker", board, position.x - 39, -position.y - 12, 78, 24, new Color(0.035f, 0.07f, 0.13f, 0.94f));
            badge.raycastTarget = false;
            string title = room.kind == "boss" ? "BOSS" : room.kind == "stairs" ? "DESCEND" :
                room.kind == "treasure" ? "CACHE" : room.kind == "rest" ? "CAMP" :
                room.kind == "merchant" ? "TRADE" : room.kind == "enemy" ? "BATTLE" : room.kind == "skillcheck" ? "BLOCKED" :
                room.kind == "keygate" ? "SEALED" : room.kind == "story" ? "LORE" : room.kind == "unknown" ? "MYSTERY" : room.kind.ToUpperInvariant();
            TextAt(badge.transform, "Encounter", title, 2, 0, 74, 24, 11,
                room.kind == "boss" ? Alert : room.kind == "stairs" ? Gold : new Color(0.65f, 0.94f, 1), TextAnchor.MiddleCenter);
            badge.gameObject.SetActive(false);
            roomBadges.Add(badge.gameObject);
        }
        boardFog = ""; boardDone = -1;
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

    // The board only: fog (room by room), dimmed finished rooms, badges and the party token. Cheap enough for every
    // room the party walks into during a glide.
    private void RefreshBoard()
    {
        var run = R.Run; var d = R.Dungeon;
        if (run == null || d == null || board == null) return;
        dungeonLight = R.DungeonLight(dungeonLight);
        if (dungeonIllustration != null) dungeonIllustration.Refresh(R);
        else if (fogCells != null)
            for (int i = 0; i < fogCells.Length; i++)
            {
                float lit = dungeonLight[i];
                fogCells[i].color = lit <= 0 ? FogTint : lit >= 1 ? Color.clear : new Color(0.02f, 0.02f, 0.03f, (1 - lit) * 0.85f);
            }
        for (int i = 0; i < roomBadges.Count && i < d.rooms.Count; i++)
        {
            if (roomBadges[i] == null) continue;
            var room = d.rooms[i];
            roomBadges[i].SetActive(!run.roomsDone.Contains(i) && R.Revealed(room.CenterX, room.CenterY));
        }
        token.SetAsLastSibling();
        token.anchoredPosition = CellCenter(run.px, run.py);
        boardFog = run.fog; boardDone = run.roomsDone.Count;
    }

    private void RefreshDungeon()
    {
        var run = R.Run; var d = R.Dungeon;
        if (run == null || d == null) return;
        if (board == null || boardKey != run.layout + run.dungeonPoi + run.floor + ":" + run.dungeonLayoutVersion) { Rebuild(); return; }
        RefreshBoard();
        foreach (Transform child in content) if (child.name == "Run panel") { Destroy(child.gameObject); }
        BuildRunPanel(true);
        BuildAutoBar();
        if (roomPanel != null) { Destroy(roomPanel.gameObject); roomPanel = null; }
        int pending = R.PendingRoom;
        int here = TowerDungeon.Index(run.px, run.py);
        if (pending >= 0 && dismissedAt != here) ShowRoom(pending);
        if (dismissedAt != here && pending < 0) dismissedAt = -1;
        // A won fight's reward pick sits on top of the board until a reward is taken.
        foreach (Transform child in content) if (child.name == "Reward shade") Destroy(child.gameObject);
        if (R.RewardPending && !lootOpen) BuildRewardCard();
        if (toast != null) toast.transform.parent.SetAsLastSibling();
    }

    // AUTO and GO TO GOAL along the bottom of the board, with how much of the floor is explored.
    private void BuildAutoBar()
    {
        if (autoBar != null) Destroy(autoBar.gameObject);
        var run = R.Run; var d = R.Dungeon;
        if (viewport == null || d == null) return;
        float vw = viewport.rect.width, w = Mathf.Min(600, vw - 24), h = 56;
        var bar = PanelAt("Auto bar", content, (vw - w) / 2, root.rect.height - h - 14, w, h);
        autoBar = bar.rectTransform;
        int done = 0, unseen = 0;
        for (int i = 0; i < d.rooms.Count; i++)
        {
            if (run.roomsDone.Contains(i)) done++;
            if (!R.Revealed(d.rooms[i].CenterX, d.rooms[i].CenterY)) unseen++;
        }
        TextAt(bar.transform, "Explored", "ROOMS " + done + " / " + d.rooms.Count + (unseen > 0 ? "\n" + unseen + " unseen" : ""), 14, 0, 140, h, 12, Cream);
        int goal = R.DungeonGoalRoom, pending = R.PendingRoom;
        bool fight = pending >= 0 && TowerRules.BattleRoom(d.rooms[pending].kind);
        float bw = (w - 170) / 2;
        ButtonAt(bar.transform, "Auto", "AUTO  (space)", 150, 8, bw, 40, AutoExplore, Teal, 15, !fight);
        ButtonAt(bar.transform, "Goal", goal >= 0 && d.rooms[goal].kind == "stairs" ? "GO TO STAIRS" : "GO TO GOAL", 160 + bw, 8, bw, 40,
            GoToGoal, Gold, 15, goal >= 0 && !fight);
    }

    private void AutoExplore()
    {
        if (Travelling || roomPanel != null || R.RewardPending || R.Dungeon == null) return;
        int target = R.DungeonAutoTarget(skippedRooms);
        if (target < 0)
        {
            Say(R.DungeonGoalRoom >= 0 ? "Nothing else to explore here. GO TO GOAL when ready." : "Nothing left to explore here.");
            return;
        }
        StepToRoom(target);
    }

    private void GoToGoal()
    {
        if (Travelling || R.Dungeon == null) return;
        int goal = R.DungeonGoalRoom;
        if (goal < 0) { Say("The way on has not been found yet."); return; }
        skippedRooms.Remove(goal);
        if (R.Dungeon.RoomAt(R.Run.px, R.Run.py) == goal) { dismissedAt = -1; RefreshDungeon(); return; }   // reopen it
        StepToRoom(goal);
    }

    private void StepToRoom(int index)
    {
        var room = R.Dungeon.rooms[index];
        Step(room.CenterX, room.CenterY);
    }

    private void DungeonInput()
    {
        if (roomPanel != null || endPanel != null || lootOpen || Travelling || R.RewardPending) return;
        var run = R.Run;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.spaceKey.wasPressedThisFrame) { AutoExplore(); return; }
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
                        // A tap anywhere in a known room sends the party to the room.
                        int room = R.Dungeon.RoomAt(x, y);
                        if (room >= 0 && R.Revealed(R.Dungeon.rooms[room].CenterX, R.Dungeon.rooms[room].CenterY)) StepToRoom(room);
                        else Step(x, y);
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
        float next = Mathf.Clamp(zoom * factor, 0.35f, 2.2f);   // the 1254 px backdrop turns blurry past about 2x
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
        travelTargetRoom = R.Dungeon.RoomAt(x, y);
        // The party is already in the room it was sent to (a dismissed rest or stairs): open it again.
        if (path.Count == 0 && travelTargetRoom >= 0 && R.PendingRoom == travelTargetRoom)
        { skippedRooms.Remove(travelTargetRoom); dismissedAt = -1; RefreshDungeon(); return; }
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

    // A glide: about 0.08 s a cell. The rules advance cell by cell; the board is redrawn only when a room is entered
    // (the fog lifts by room), and the run is saved once, where the glide stops: at the first room with something to
    // resolve (rooms turned down for now are passed through unless they are the destination) or at the destination.
    private void UpdateTravel()
    {
        if (!Travelling) return;
        if (roomPanel != null || endPanel != null || lootOpen || R.RewardPending) { CancelTravel(); return; }
        int cell = travelPath[travelIndex];
        int x = cell % TowerDungeon.Size, y = cell / TowerDungeon.Size;
        travelClock += Mathf.Min(Time.unscaledDeltaTime, 0.05f) * GlideCellsPerSecond;
        token.anchoredPosition = Vector2.Lerp(CellCenter(R.Run.px, R.Run.py), CellCenter(x, y), Mathf.Min(1, travelClock));
        for (int i = 0; i < partyFigures.Count; i++)
            partyFigures[i].anchoredPosition = partyOffsets[i] + new Vector2(0, Mathf.Sin(Time.unscaledTime * 22 + i * 1.5f) * 1.5f);
        KeepPartyInView();
        if (travelClock < 1) return;
        string fogBefore = R.Run.fog;
        string error = R.DungeonAdvance(x, y);
        if (error != null) { CancelTravel(); Say(error); commitDungeonStep(); RefreshDungeon(); return; }
        if (travelIndex % 2 == 0) TowerAudio.PlayVaried("step", 0.4f);
        travelIndex++; travelClock = 0;
        if (travelIndex <= routeMarks.Count && routeMarks[travelIndex - 1] != null) routeMarks[travelIndex - 1].SetActive(false);
        if (!ReferenceEquals(fogBefore, R.Run.fog)) { Sfx("room_reveal", "door", 0.35f); RefreshBoard(); }
        else if (boardDone != R.Run.roomsDone.Count) RefreshBoard();
        else token.anchoredPosition = CellCenter(R.Run.px, R.Run.py);
        int pending = R.PendingRoom;
        bool stop = pending >= 0 && (TowerRules.BattleRoom(R.Dungeon.rooms[pending].kind) || !skippedRooms.Contains(pending) || pending == travelTargetRoom);
        if (stop || !Travelling)
        {
            if (stop) { skippedRooms.Remove(pending); dismissedAt = -1; }
            commitDungeonStep();
            CancelTravel();
            RefreshDungeon();
        }
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
                    if (Tex(signature) != null) return signature;
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
            case "shrine": folder = "shrine"; break;
            default: folder = new[] { "secret", "shrine", "puzzle", "skill_check", "story" }[Mathf.Abs(seed) % 5]; break;
        }
        string path = Root + "Rooms/" + folder + "_" + (Mathf.Abs(seed) % 3);
        // A kind whose art has not arrived yet falls back to the original set.
        if (folder == "rest_alcove" || folder == "skill_check" || folder == "story" || folder == "trap" || folder == "key_gate")
            if (Tex(path) == null) return Root + "Rooms/" + (kind == "rest" ? "safe_camp" : "puzzle") + "_" + (Mathf.Abs(seed) % 3);
        return path;
    }

    private static string RoomTitle(string kind, bool goal, string poiName)
    {
        if (goal) return poiName;
        switch (kind)
        {
            case "enemy": return "Ambush"; case "elite": return "Guardian's Hall"; case "boss": return poiName;
            case "treasure": return "Forgotten Cache"; case "rest": return "Quiet Nook"; case "merchant": return "Wandering Peddler";
            case "stairs": return "Descending Stair"; case "shrine": return "Forest Shrine"; case "trap": return "Trapped Passage";
            case "skillcheck": return "Blocked Way"; case "story": return "Old Inscription"; case "keygate": return "Sealed Door";
            default: return "Strange Chamber";
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
            case "merchant": return place + " A hooded peddler spreads out his wares: tonics, rations, firewood, and one or two curious trinkets.";
            case "shrine": return place + " An old shrine hums with quiet power. It will bless you, but blessings here are paid in blood.";
            case "trap": return place + " Tripwires and pressure plates line the floor. Someone careful could strip it for parts.";
            case "skillcheck": return place + " Rubble and a fallen beam block a side passage. Strong arms could clear it.";
            case "story": return place + " Words are carved deep into the wall, half lost to moss and time.";
            case "keygate": return place + " A door sealed with old wards. Something valuable waits behind it.";
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
        var art = Tex(RoomArt(room.kind, d.theme, (int)TowerForestLayouts.Hash(run.dungeonPoi, index + run.floor * 17),
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
                ButtonAt(panel, "Take", "TAKE THE LOOT", tx, by, tw, 56, () => { Sfx("treasure_open", "reward", 0.8f); Act(R.ResolveRoom("take")); }, Teal, 18);
                break;
            case "rest":
                ButtonAt(panel, "Rest", run.firewood > 0 ? "MAKE CAMP (1 firewood)" : "REST (no firewood)", tx, by, bw, 56, () => Act(R.ResolveRoom("rest")), Teal, 15);
                ButtonAt(panel, "Later", "NOT NOW", tx + bw + 12, by, bw, 56, Dismiss, Alert, 15);
                break;
            case "merchant":
            {
                // Haul gold pays; MOVE ON closes the stall.
                TextAt(panel, "Purse", "Haul gold: " + R.HaulGold + "g", tx, by - 120, tw, 20, 14, Gold);
                string[] items = { "tonic", "rations", "firewood", "relic" };
                string[] labels = { "TONIC", "2 RATIONS", "FIREWOOD", "RELIC" };
                float iw = (tw - 12) / 2;
                for (int k = 0; k < items.Length; k++)
                {
                    string item = items[k];
                    int price = TowerRules.MerchantPrice(item);
                    ButtonAt(panel, "Buy " + item, labels[k] + " (" + price + "g)", tx + (k % 2) * (iw + 12), by - 96 + (k / 2) * 48, iw, 42,
                        () => Act(R.ResolveRoom("buy_" + item)), Teal, 13, R.HaulGold >= price);
                }
                ButtonAt(panel, "Leave", "MOVE ON", tx, by, tw, 56, () => Act(R.ResolveRoom("leave")), Alert, 15);
                break;
            }
            case "shrine":
                ButtonAt(panel, "Offer", "OFFER BLOOD (-15% HP: relic)", tx, by - 64, tw, 52, () => Act(R.ResolveRoom("offer")), Gold, 14);
                ButtonAt(panel, "Pray", "PRAY (heal, calm)", tx, by, bw, 56, () => Act(R.ResolveRoom("pray")), Teal, 14);
                ButtonAt(panel, "Leave", "LEAVE", tx + bw + 12, by, bw, 56, () => Act(R.ResolveRoom("leave")), Alert, 15);
                break;
            case "trap": case "skillcheck": case "keygate":
            {
                string verb = room.kind == "trap" ? "DISARM" : room.kind == "skillcheck" ? "CLEAR IT" : "BREAK THE SEAL";
                string choice = room.kind == "trap" ? "disarm" : room.kind == "skillcheck" ? "attempt" : "force";
                string trait = TowerRules.RoomTrait(room.kind);
                string odds = Mathf.RoundToInt(R.RoomChance(room.kind) * 100) + "%" + (trait.Length > 0 ? (R.PartyHasTrait(trait) ? "  (" + trait + " helps)" : "  (" + trait + " would help)") : "");
                ButtonAt(panel, "Try", verb + "  •  " + odds, tx, by - 64, tw, 52, () => CheckedRoom(room.kind, choice), Teal, 14);
                ButtonAt(panel, "Other", room.kind == "trap" ? "RUSH THROUGH (-8% HP)" : "LEAVE IT", tx, by, tw, 56,
                    () => CheckedRoom(room.kind, room.kind == "trap" ? "rush" : "leave"), Alert, 15);
                break;
            }
            case "story":
                ButtonAt(panel, "Read", "READ IT", tx, by, bw, 56, () => Act(R.ResolveRoom("read")), Teal, 16);
                ButtonAt(panel, "Ignore", "MOVE ON", tx + bw + 12, by, bw, 56, () => Act(R.ResolveRoom("leave")), Alert, 15);
                break;
            case "stairs":
                ButtonAt(panel, "Descend", "DESCEND", tx, by, bw, 56, () => { Sfx("stairs_descend", "door", 0.8f); Act(R.ResolveRoom("descend")); }, Teal, 17);
                ButtonAt(panel, "Later", "NOT YET", tx + bw + 12, by, bw, 56, Dismiss, Alert, 15);
                break;
            default:
                ButtonAt(panel, "Investigate", "INVESTIGATE", tx, by, bw, 56, () => Act(R.ResolveRoom("investigate")), Teal, 16);
                ButtonAt(panel, "Ignore", "LEAVE IT", tx + bw + 12, by, bw, 56, () => Act(R.ResolveRoom("ignore")), Alert, 15);
                break;
        }
    }

    // NOT NOW / NOT YET: the room stays open, and AUTO and glides pass it by until the party is sent to it.
    private void Dismiss()
    {
        dismissedAt = TowerDungeon.Index(R.Run.px, R.Run.py);
        if (R.PendingRoom >= 0) skippedRooms.Add(R.PendingRoom);
        if (roomPanel != null) { Destroy(roomPanel.gameObject); roomPanel = null; }
    }

    // A trait-checked room (trap, blocked way, sealed door): a spring of the trap or a find gets its own sound.
    private void CheckedRoom(string kind, string choice)
    {
        int hpBefore = 0; foreach (var h in R.Run.hp) hpBefore += h;
        int haulBefore = R.Run.haul.Count;
        string error = R.ResolveRoom(choice);
        if (error == null && R.Run != null)
        {
            int hpAfter = 0; foreach (var h in R.Run.hp) hpAfter += h;
            if (hpAfter < hpBefore && kind == "trap") Sfx("trap_spring", "status", 0.8f);
            else if (R.Run.haul.Count > haulBefore) Sfx("treasure_open", "reward", 0.7f);
        }
        Act(error);
    }

    private void Fight()
    {
        var room = R.Dungeon.rooms[R.PendingRoom];
        FightWithParty(R.RoomEncounter(room.kind), "BACK TO THE DUNGEON", (won, hp) =>
        {
            Act(R.ResolveBattle(won, hp));
            if (R.Run != null && won) Say("Victory! The spoils are added to your haul.");
        });
    }

    private void FightAmbush()
    {
        FightWithParty(R.AmbushEncounter, "BACK TO THE FOREST", (won, hp) =>
        {
            Act(R.ResolveAmbush(won, hp));
            if (R.Run != null && R.State.log.Count > 0) Say(R.State.log[R.State.log.Count - 1]);
        });
    }

    // Builds the living party (Tower gear applied) and hands the screen to BattleMode; done gets HP percents back.
    private void FightWithParty(BattleEncounterSpec spec, string returnLabel, Action<bool, List<int>> done)
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
            unit.MaxHp = Mathf.RoundToInt(unit.MaxHp * (1 + R.GearHp(unit.Id) + R.LevelHp(unit.Id)));
            unit.Defense *= 1 + R.LevelGuard(unit.Id);
            unit.Resistance *= 1 + R.LevelGuard(unit.Id);
            unit.Hp = Mathf.Max(1, Mathf.RoundToInt(unit.MaxHp * run.hp[i] / 100f));
            unit.Stress = R.Stress(i);
            alive.Add(unit);
        }
        var field = alive.GetRange(0, Mathf.Min(3, alive.Count));
        var reserve = alive.Count > 3 ? alive.GetRange(3, Mathf.Min(3, alive.Count - 3)) : new List<BattleUnit>();
        canvas.gameObject.SetActive(false);
        // The spec's seed builds the line-up and drives the fight, so reloading mid-fight replays the same battle.
        var encounter = BattleCatalog.Build(spec);
        var species = new List<string>();
        foreach (var enemy in encounter.Enemies) if (!string.IsNullOrEmpty(enemy.Species)) species.Add(enemy.Species);
        R.RecordBeasts(species);
        float vigor = 0f;
        foreach (var unit in alive) vigor += R.LevelHp(unit.Id) + R.GearHp(unit.Id);
        encounter.SummonerVigor = alive.Count > 0 ? vigor / alive.Count : 0f;
        encounter.CardLevels = R.CardLevels();
        encounter.Modifiers = R.BattleModifiers();
        tower.LaunchExpeditionBattle(spec.Depth, field, reserve, won =>
        {
            canvas.gameObject.SetActive(true);
            var hp = new List<int>(run.hp);
            var stress = new List<int>();
            for (int i = 0; i < run.party.Count; i++) { var u = alive.Find(a => a.Id == run.party[i]); stress.Add(u != null ? u.Stress : R.Stress(i)); }
            R.RecordStress(stress);
            for (int i = 0; i < run.party.Count; i++)
            {
                var unit = alive.Find(u => u.Id == run.party[i]);
                // Round to nearest (rounding up made HP creep upward fight after fight); a living fighter keeps 1%.
                if (unit != null) hp[i] = unit.Hp <= 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(100f * unit.Hp / unit.MaxHp), 1, 100);
            }
            done(won, hp);
        }, returnLabel, spec.Seed, encounter);
    }

    // ---------------------------------------------------------------- traversal events

    // Event card (choices with check odds), then its result, or an ambush that must be fought.
    // Pick one of three after a won fight (TowerRunRewards): relic, card upgrade or supply.
    private void BuildRewardCard()
    {
        var run = R.Run;
        if (!R.RewardPending) return;
        float width = root.rect.width, height = root.rect.height;
        string offerKey = string.Join("|", run.rewardOffer.ToArray());
        if (offerKey != soundedReward) { soundedReward = offerKey; TowerAudio.Play("reward"); }
        var shade = Stretch("Reward shade", content, new Color(0, 0, 0, 0.6f));
        int n = run.rewardOffer.Count;
        float pw = Mathf.Min(900, width - Side - 60), ph = 330;
        float px = (width - Side - pw) / 2, py = Top + (height - Top - ph) / 2;
        var panel = PanelAt("Reward", shade.transform, px, py, pw, ph).transform;
        TextAt(panel, "Title", "VICTORY  •  CHOOSE ONE REWARD", 20, 14, pw - 40, 30, 21, Gold);
        TextAt(panel, "Hint", "Upgrades and relics last until this expedition ends.", 20, 44, pw - 40, 22, 13, new Color(0.8f, 0.85f, 0.9f));
        float cw = (pw - 40 - (n - 1) * 14) / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            int index = i;
            string offer = run.rewardOffer[i];
            bool relic = offer.StartsWith("relic:", StringComparison.Ordinal);
            bool upgrade = offer.StartsWith("upgrade:", StringComparison.Ordinal);
            float cx = 20 + i * (cw + 14);
            var card = At("Offer " + i, panel, cx, 76, cw, 236, relic ? new Color(0.24f, 0.19f, 0.08f, 0.98f) :
                upgrade ? new Color(0.10f, 0.22f, 0.27f, 0.98f) : new Color(0.12f, 0.15f, 0.12f, 0.98f));
            TextAt(card.transform, "Kind", relic ? "RELIC" : upgrade ? "CARD UPGRADE" : "SUPPLY", 12, 10, cw - 24, 20, 12, relic ? Gold : Teal);
            TextAt(card.transform, "Name", TowerRules.RewardTitle(offer), 12, 32, cw - 24, 52, 18, Cream);
            TextAt(card.transform, "Text", R.RewardText(offer), 12, 88, cw - 24, 88, 13, new Color(0.85f, 0.88f, 0.92f));
            ButtonAt(card.transform, "Take", "TAKE", 12, 182, cw - 24, 42, () => { Act(R.ChooseReward(index)); }, relic ? Gold : Teal, 16);
        }
    }

    // A climbed tower: pick one of three tablets; each adds its map mod to every place in the tower's circle.
    private void BuildTabletCard()
    {
        var run = R.Run;
        float width = root.rect.width, height = root.rect.height;
        var tower = R.Overworld.Poi(run.at);
        if (tower != null && gridView != null) gridView.ShowRing(new Vector2Int(tower.x, tower.y), TowerRules.TowerRadius, RingColor);
        var shade = Stretch("Tablet shade", content, new Color(0, 0, 0, 0.35f));
        int n = run.tabletOffer.Count;
        float pw = Mathf.Min(900, width - Side - 60), ph = 320;
        float px = (width - Side - pw) / 2, py = height - ph - 24;
        var panel = PanelAt("Tablets", shade.transform, px, py, pw, ph).transform;
        TextAt(panel, "Title", "THE TOWER  •  SET ONE TABLET", 20, 14, pw - 40, 30, 21, Gold);
        TextAt(panel, "Hint", "A tablet changes every place in the ringed circle that still has a dungeon: harder, and richer.",
            20, 44, pw - 40, 22, 13, new Color(0.8f, 0.85f, 0.9f));
        float cw = (pw - 40 - (n - 1) * 14) / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            int index = i;
            string offer = run.tabletOffer[i];
            var mod = TowerRules.TabletMod(offer);
            if (mod == null) continue;
            float cx = 20 + i * (cw + 14);
            var card = At("Tablet " + i, panel, cx, 76, cw, 226, new Color(0.12f, 0.16f, 0.24f, 0.98f));
            TextAt(card.transform, "Kind", "TABLET", 12, 10, cw - 24, 20, 12, Teal);
            TextAt(card.transform, "Name", mod.name.ToUpperInvariant(), 12, 30, cw - 24, 30, 19, Cream);
            TextAt(card.transform, "Danger", "Danger: " + mod.danger + ".", 12, 64, cw - 24, 40, 13, new Color(1f, 0.66f, 0.55f));
            TextAt(card.transform, "Reward", "Reward: " + mod.reward + ".", 12, 106, cw - 24, 40, 13, new Color(0.7f, 0.95f, 0.7f));
            int reach = R.TabletReach(offer);
            TextAt(card.transform, "Reach", reach + (reach == 1 ? " place" : " places") + " in the circle", 12, 146, cw - 24, 20, 12, new Color(0.8f, 0.85f, 0.9f));
            ButtonAt(card.transform, "Take", "SET IT", 12, 172, cw - 24, 42, () => { Act(R.ChooseTablet(index)); }, Teal, 16);
        }
    }

    // The lair is cleared: a proper moment for the conquest, then the reward pick behind it.
    private void BuildConquestCard()
    {
        var run = R.Run;
        var region = TowerRules.Region(run.conquest);
        float width = root.rect.width, height = root.rect.height;
        if (soundedEvent != "conquest:" + run.conquest) { soundedEvent = "conquest:" + run.conquest; TowerAudio.Play("victory"); }
        var shade = Stretch("Conquest shade", content, new Color(0, 0, 0, 0.7f));
        float pw = Mathf.Min(760, width - Side - 60), ph = 300;
        float px = (width - Side - pw) / 2, py = Top + (height - Top - ph) / 2;
        var panel = PanelAt("Conquest", shade.transform, px, py, pw, ph).transform;
        TextAt(panel, "Title", "REGION CONQUERED", 20, 18, pw - 40, 40, 30, Gold, TextAnchor.MiddleCenter);
        TextAt(panel, "Name", region != null ? region.name.ToUpperInvariant() : "", 20, 62, pw - 40, 30, 22, Cream, TextAnchor.MiddleCenter);
        var opened = new List<string>();
        foreach (var r in TowerRules.Regions)
            if (Array.IndexOf(r.requires, run.conquest) >= 0) opened.Add(r.name);
        TextAt(panel, "Text", "The lair has fallen and the woods grow quiet. +" + TowerRules.ConquestSigils + " Sigils go home with you." +
            (opened.Count > 0 ? "\n\nThe way now leads on to " + string.Join(" and ", opened.ToArray()) + "." : "\n\nThe last lair has fallen."),
            30, 104, pw - 60, 110, 16, Cream, TextAnchor.UpperCenter);
        ButtonAt(panel, "Continue", "CONTINUE", pw / 2 - 120, ph - 70, 240, 50, () => { R.DismissConquest(); Rebuild(); }, Gold, 18);
    }

    private void BuildEventCard()
    {
        var run = R.Run; var def = R.PendingEvent;
        if (R.ConquestPending) { BuildConquestCard(); return; }
        if (R.RewardPending) { BuildRewardCard(); return; }
        if (R.TabletPending) { BuildTabletCard(); return; }
        bool ambush = R.AmbushPending;
        if (def == null && !ambush && string.IsNullOrEmpty(run.eventResult)) return;
        string eventKey = ambush ? "ambush:" + run.steps : def != null ? def.id + ":" + run.steps : "";
        if (eventKey.Length > 0 && eventKey != soundedEvent) { soundedEvent = eventKey; TowerAudio.Play(ambush ? "status" : "chime", 0.8f); }
        float width = root.rect.width, height = root.rect.height;
        var shade = Stretch("Event shade", content, new Color(0, 0, 0, 0.55f));
        int choices = ambush || def == null ? 1 : def.choices.Count;
        // Optional scene plate for this event (Events/Art/<id>); events without one keep the plain card.
        var art = def == null ? null : Tex("AdamsHaven/Events/Art/" + def.id);
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
        var chibi = Tex("AdamsHaven/Chibi/" + speaker);
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

    // ---------------------------------------------------------------- immersion (review step 7)

    // The map darkens and cools through dusk to night, and warms at dawn.
    private static Color DayTint(float hour)
    {
        float night = TowerRules.NightLevel(hour);
        Color tint = Color.Lerp(Color.white, new Color(0.42f, 0.5f, 0.72f), night);
        if (hour >= 17f && hour < 20f) tint = Color.Lerp(tint, new Color(1f, 0.82f, 0.66f), 0.35f * (1f - Mathf.Abs(hour - 18.5f) / 1.5f));
        if (hour >= 5f && hour < 7.5f) tint = Color.Lerp(tint, new Color(1f, 0.86f, 0.76f), 0.3f * (1f - Mathf.Abs(hour - 6.25f) / 1.25f));
        return tint;
    }

    // Fog is a pale haze over the map; rain is a sheet of falling streaks.
    private void BuildWeather(RectTransform map, string weather)
    {
        if (weather == "fog")
        {
            var haze = new GameObject("Fog haze", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            haze.transform.SetParent(map, false);
            var r = haze.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            haze.color = new Color(0.78f, 0.82f, 0.86f, 0.22f); haze.raycastTarget = false;
        }
        else if (weather == "rain")
        {
            var rain = new GameObject("Rain", typeof(RectTransform), typeof(RawImage), typeof(TowerRainLayer)).GetComponent<RawImage>();
            rain.transform.SetParent(map, false);
            var r = rain.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            rain.raycastTarget = false;
        }
    }

    // A line from the party: always at camp, otherwise now and then after a walk.
    private void MaybeBanter(bool always)
    {
        var run = R.Run;
        if (run == null || view != View.Forest || gridView == null) return;
        movesSinceBanter++;
        if (!always && (movesSinceBanter < 3 || banterRng.NextDouble() > 0.45)) return;
        string line = TowerBanter.Pick(run.party, run.hp, TowerBanter.Mood(R), banterRng);
        if (line == null) return;
        movesSinceBanter = 0;
        if (banterBubble != null) Destroy(banterBubble);
        float areaW = root.rect.width - Side - 12, areaH = root.rect.height - Top;
        var vp = gridView.CellToViewport(run.cx, run.cy);
        float w = 300, h = 56;
        float x = Mathf.Clamp(vp.x * areaW - w / 2, 8, areaW - w - 8), y = Mathf.Clamp(Top + (1 - vp.y) * areaH - h - 46, Top + 8, root.rect.height - h - 8);
        var bubble = PanelAt("Banter", content, x, y, w, h);
        bubble.raycastTarget = false;
        TextAt(bubble.transform, "Line", line, 12, 4, w - 24, h - 8, 13, Cream, TextAnchor.MiddleLeft).fontStyle = FontStyle.Italic;
        banterBubble = bubble.gameObject;
        banterClock = 5.5f;
    }

    // Setting out: the region, its danger and a hook, over a painting of what lies ahead.
    private void BuildIntroCard()
    {
        var run = R.Run; var region = R.RunRegion;
        float width = root.rect.width, height = root.rect.height;
        var shade = Stretch("Intro shade", content, new Color(0, 0, 0, 0.62f));
        float pw = Mathf.Min(820, width - Side - 60), ph = 340, px = (width - Side - pw) / 2, py = (height - ph) / 2;
        var panel = PanelAt("Intro", shade.transform, px, py, pw, ph).transform;
        var art = Tex(TowerDungeonIllustration.ArtRoot + IntroArt(run.biome) + "_vista");
        float artW = 0;
        if (art != null)
        {
            artW = Mathf.Min(260, pw * 0.34f);
            var pic = new GameObject("Region art", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            pic.transform.SetParent(panel, false); pic.texture = art; pic.raycastTarget = false;
            var pr = pic.rectTransform; pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0, 1);
            pr.anchoredPosition = new Vector2(12, -12); pr.sizeDelta = new Vector2(artW, ph - 24);
            float aspect = artW / (ph - 24), src = art.width / (float)art.height;
            pic.uvRect = src > aspect ? new Rect((1 - aspect / src) / 2, 0, aspect / src, 1) : new Rect(0, (1 - src / aspect) / 2, 1, src / aspect);
            artW += 12;
        }
        float tx = 24 + artW, tw = pw - tx - 24;
        TextAt(panel, "Kicker", "THE GUILD SETS OUT", tx, 22, tw, 22, 14, new Color(0.75f, 0.85f, 0.95f));
        TextAt(panel, "Name", region.name.ToUpperInvariant(), tx, 46, tw, 44, 32, Gold);
        TextAt(panel, "Biome", BiomeName(run.biome) + "  •  Danger " + region.depth + "  •  " + TowerRules.WeatherName(R.Weather), tx, 92, tw, 22, 14, Cream);
        TextAt(panel, "Blurb", region.blurb + " " + BiomeHook(run.biome), tx, 122, tw, 96, 16, Cream, TextAnchor.UpperLeft);
        TextAt(panel, "Goal", "Find the lair and clear it to conquer the region. Head home from camp to bank the haul.", tx, 222, tw, 40, 13,
            new Color(0.8f, 0.86f, 0.92f), TextAnchor.UpperLeft);
        ButtonAt(panel, "Go", "SET OFF", pw - 224, ph - 70, 200, 52, () => { introOpen = false; Rebuild(); }, Teal, 18);
    }

    private static string IntroArt(string biome)
    {
        switch (biome)
        {
            case "lakes": return "marsh";
            case "mountains": return "mine";
            case "ruins": return "ruin";
            case "heart": return "heartwood";
            default: return "briar";
        }
    }

    private static string BiomeName(string biome)
    {
        switch (biome)
        {
            case "lakes": return "Lakes and marsh";
            case "mountains": return "Mountain passes";
            case "ruins": return "Old ruins";
            case "heart": return "The Heart of the Silverwood";
            default: return "The forest edge";
        }
    }

    private static string BiomeHook(string biome)
    {
        switch (biome)
        {
            case "lakes": return "Mist lies on the water, and something moves under it.";
            case "mountains": return "Thin air, deep mines, and things that dig.";
            case "ruins": return "Stone remembers what the forest forgot.";
            case "heart": return "The Silverwood's heart beats slow and sick.";
            default: return "Old roads and older trees. The forest is waking.";
        }
    }

    // The end of a run: what came home, what was lost, and where the party went.
    private void ShowSummary()
    {
        var s = R.LastSummary;
        float width = root.rect.width, height = root.rect.height;
        var shade = Stretch("End shade", content, new Color(0, 0, 0, 0.78f));
        endPanel = shade.rectTransform;
        float pw = Mathf.Min(620, width - 40), ph = 420;
        var panel = PanelAt("End", shade.transform, (width - pw) / 2, (height - ph) / 2, pw, ph).transform;
        bool wiped = s != null && s.wiped;
        string title = s == null ? "THE EXPEDITION IS OVER" : wiped ? "THE PARTY HAS FALLEN" : s.retreated ? "A HURRIED RETREAT" : "THE GUILD RETURNS";
        TextAt(panel, "Title", title, 20, 18, pw - 40, 40, 28, wiped ? Alert : Gold, TextAnchor.MiddleCenter);
        if (s != null && soundedEvent != "end")
        {
            soundedEvent = "end";
            TowerAudio.Play(wiped ? "defeat" : s.retreated ? "confirm" : "victory");
        }
        if (s == null)
        {
            TextAt(panel, "Text", "The survivors are home.", 30, 80, pw - 60, 40, 16, Cream, TextAnchor.MiddleCenter);
        }
        else
        {
            TextAt(panel, "Region", s.region + "  •  " + s.days + (s.days == 1 ? " day" : " days") + " in the wild", 30, 62, pw - 60, 24, 16, Cream, TextAnchor.MiddleCenter);
            string journey = s.places + " places reached  •  " + s.cleared + " cleared  •  " + s.fights + (s.fights == 1 ? " fight" : " fights") + " won";
            string growth = s.relics + (s.relics == 1 ? " relic" : " relics") + " and " + s.upgrades + " card upgrades (they stay in the wild)";
            TextAt(panel, "Journey", journey, 30, 100, pw - 60, 24, 15, Cream, TextAnchor.MiddleCenter);
            TextAt(panel, "Growth", growth, 30, 126, pw - 60, 24, 13, new Color(0.8f, 0.86f, 0.92f), TextAnchor.MiddleCenter);
            TextAt(panel, "Banked title", wiped ? "ONLY THE SAFE POCKET CAME HOME" : "BANKED IN THE TOWER", 30, 166, pw - 60, 24, 15, Gold, TextAnchor.MiddleCenter);
            var parts = new List<string> { s.gold + " gold" };
            if (s.ore > 0) parts.Add(s.ore + " ore");
            if (s.essence > 0) parts.Add(s.essence + " essence");
            if (s.celestium > 0) parts.Add(s.celestium + " celestium");
            if (s.sigils > 0) parts.Add(s.sigils + " Sigils");
            TextAt(panel, "Banked", string.Join("  •  ", parts.ToArray()), 30, 194, pw - 60, 30, 20, Cream, TextAnchor.MiddleCenter);
            string loss = s.goldLeft > 0 ? s.goldLeft + " gold was dropped in the retreat. Head home from a camp to keep it all." :
                s.fallen > 0 ? s.fallen + (s.fallen == 1 ? " fighter comes" : " fighters come") + " home wounded and will need rest." : "";
            if (loss.Length > 0) TextAt(panel, "Loss", loss, 30, 236, pw - 60, 44, 14, new Color(1f, 0.7f, 0.6f), TextAnchor.MiddleCenter);
        }
        ButtonAt(panel, "Home", "RETURN TO TOWER", (pw - 280) / 2, ph - 84, 280, 58, () => tower.CloseExpedition(null), Teal, 18);
    }

    // ---------------------------------------------------------------- guild journal

    private void OpenJournal() { journalOpen = true; Rebuild(); }

    private void BuildJournal()
    {
        var journal = R.Journal;
        float width = root.rect.width, height = root.rect.height;
        var shade = Stretch("Journal shade", content, new Color(0, 0, 0, 0.7f));
        float pw = Mathf.Min(900, width - 40), ph = Mathf.Min(600, height - 70);
        var panel = PanelAt("Journal", shade.transform, (width - pw) / 2, (height - ph) / 2 + 10, pw, ph).transform;
        TextAt(panel, "Title", "GUILD JOURNAL", 22, 12, 300, 32, 22, Gold);
        ButtonAt(panel, "Close", "CLOSE", pw - 124, 12, 104, 34, () => { journalOpen = false; Rebuild(); }, Alert, 14);
        ButtonAt(panel, "Codex", "CARD CODEX", pw - 278, 12, 144, 34, () => OpenCodex(journalTab == 2 ? "LAIR BOSSES" : "BESTIARY"), Teal, 13);
        string[] tabs = { "BEASTS", "EVENTS", "REGIONS" };
        int[] counts = { journal.beasts.Count, journal.events.Count, journal.regions.Count };
        int[] totals = { BattleCatalog.SpeciesIds.Length, TowerEvents.All.Count, TowerRules.Regions.Length };
        for (int i = 0; i < tabs.Length; i++)
        {
            int tab = i;
            ButtonAt(panel, "Tab " + i, tabs[i] + "  " + counts[i] + "/" + totals[i], 22 + i * 190, 54, 180, 36,
                () => { journalTab = tab; Rebuild(); }, journalTab == i ? Teal : new Color(0.2f, 0.24f, 0.3f), 13);
        }
        var rows = new List<KeyValuePair<string, string>>();
        if (journalTab == 0 && BattleBestiary.Available)
            // One row per bestiary family: its evolution ladder, with forms not yet met shown as "???".
            foreach (var family in BattleBestiary.Families)
            {
                int seen = 0;
                var ladder = new List<string>();
                foreach (var form in family.forms)
                {
                    bool met = journal.beasts.Contains(form.id);
                    if (met) seen++;
                    ladder.Add((met ? form.name : "???") + " (" + BattleBestiary.Span(form.minRank, form.maxRank) + ")");
                }
                rows.Add(seen == 0 ? new KeyValuePair<string, string>("Unknown beast", "Not yet met in battle.")
                    : new KeyValuePair<string, string>(family.name + "  ·  " + family.element + "  ·  " + seen + "/" + family.forms.Length + " forms",
                        string.Join("  >  ", ladder)));
            }
        else if (journalTab == 0)
            foreach (var id in BattleCatalog.SpeciesIds)
                rows.Add(journal.beasts.Contains(id) ? new KeyValuePair<string, string>(BattleCatalog.SpeciesName(id), BattleCatalog.SpeciesInfo(id))
                    : new KeyValuePair<string, string>("Unknown beast", "Not yet met in battle."));
        else if (journalTab == 1)
        {
            foreach (var id in journal.events)
            {
                var def = TowerEvents.Get(id);
                if (def != null) rows.Add(new KeyValuePair<string, string>(def.title, def.text));
            }
            if (rows.Count == 0) rows.Add(new KeyValuePair<string, string>("Nothing yet", "Events met in the wild are written down here."));
        }
        else
            foreach (var region in TowerRules.Regions)
                rows.Add(journal.regions.Contains(region.id) || R.RegionConquered(region.id)
                    ? new KeyValuePair<string, string>(region.name + (R.RegionConquered(region.id) ? "  (conquered)" : ""), region.blurb + " Danger " + region.depth + ".")
                    : new KeyValuePair<string, string>("Uncharted", "No expedition has set out for this land yet."));
        ScrollRows(panel, 22, 102, pw - 44, ph - 120, rows);
    }

    // A scrolling list of title + text rows.
    private void ScrollRows(Transform parent, float x, float y, float w, float h, List<KeyValuePair<string, string>> rows)
    {
        var holder = At("Rows view", parent, x, y, w, h, new Color(0.02f, 0.03f, 0.05f, 0.6f));
        holder.gameObject.AddComponent<RectMask2D>();
        const float rowH = 62;
        var list = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
        list.SetParent(holder.transform, false);
        list.anchorMin = new Vector2(0, 1); list.anchorMax = new Vector2(1, 1); list.pivot = new Vector2(0.5f, 1);
        list.anchoredPosition = Vector2.zero; list.sizeDelta = new Vector2(0, Mathf.Max(h, rows.Count * rowH + 8));
        for (int i = 0; i < rows.Count; i++)
        {
            bool unknown = rows[i].Key == "Unknown beast" || rows[i].Key == "Uncharted";
            TextAt(list, "Row title " + i, rows[i].Key, 14, 8 + i * rowH, w - 28, 20, 15, unknown ? new Color(0.55f, 0.6f, 0.66f) : Gold);
            TextAt(list, "Row text " + i, rows[i].Value, 14, 28 + i * rowH, w - 28, 32, 12, unknown ? new Color(0.5f, 0.55f, 0.6f) : Cream, TextAnchor.UpperLeft);
        }
        var scroll = holder.gameObject.AddComponent<ScrollRect>();
        scroll.content = list; scroll.viewport = holder.rectTransform;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
    }
}
