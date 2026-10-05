using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Presentation-only layer. The simulation, save data and hit-test grid stay owned by the controller.
[DefaultExecutionOrder(1000)]
public sealed class TowerArtDirector : MonoBehaviour
{
    private const float Cell = 2.0f, Storey = 2.75f;
    private const string Root = "AdamsHaven/TowerPresentation/";
    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Material> terrainMaterials = new Dictionary<string, Material>();
    private readonly List<TextMesh> roomLabels = new List<TextMesh>();
    private AdamsHavenPrototype tower;
    private Transform sourceRoot, artRoot;
    private readonly List<SpriteRenderer> coreBeams = new List<SpriteRenderer>();
    private Camera cameraView;
    private TowerState framedState;
    private int framedWest = -1;
    private TowerHud styledHud;
    private Font worldFont;
    private float portraitTimer;
    private GameObject residentPanel, roomPanel;
    private bool residentsOpen, roomOpen;
    private int lastSelectedRoom, framedPanels = -1, lastLesson = -1;
    private Text residentToggleText, roomToggleText;
    private RectTransform tutorialRect;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Attach(); }
    private static void Attach()
    {
        var controller = Object.FindAnyObjectByType<AdamsHavenPrototype>();
        if (controller != null && controller.GetComponent<TowerArtDirector>() == null)
            controller.gameObject.AddComponent<TowerArtDirector>();
    }
    private void Awake() { tower = GetComponent<AdamsHavenPrototype>(); }

    // The HUD dock drives the side panels; these keep the camera framing in step.
    public bool ResidentsOpen { get { return residentsOpen; } }
    public void SetResidentsOpen(bool open) { residentsOpen = open; }
    public void ToggleResidents() { residentsOpen = !residentsOpen; }
    public void SetRoomOpen(bool open) { roomOpen = open; }
    private static float X(float cell) { return (cell - 17.5f) * Cell; }

    // Skylines-style info views over the cutaway (TowerHudDistricts.cs): off, districts, coverage or appeal.
    public static string Overlay = "off";
    public static int OverlaySignature(TowerRules rules)
    { return Overlay == "off" || rules == null ? 0 : Overlay.Length * 7919 + rules.OverlayStamp * 31; }
    private Sprite overlaySprite;

    private void LateUpdate()
    {
        if (tower == null || tower.Rules == null || Camera.main == null) return;
        cameraView = Camera.main;
        int selected = tower.SelectedRoom == null ? 0 : tower.SelectedRoom.uid;
        if (selected != lastSelectedRoom)
        { lastSelectedRoom = selected; roomOpen = selected != 0; }
        int lesson = tower.Rules.State.introPhase == "complete" ? tower.Rules.State.tutorialStep : -1;
        if (lesson != lastLesson)
        {
            lastLesson = lesson;
            if (lesson == 1 || lesson == 4) residentsOpen = true; // these lessons need the roster and work buttons
        }
        if (residentPanel != null) residentPanel.SetActive(residentsOpen);
        if (roomPanel != null) roomPanel.SetActive(roomOpen);
        if (residentToggleText != null) residentToggleText.text = residentsOpen ? "RESIDENTS  −" : "RESIDENTS  +";
        if (roomToggleText != null) roomToggleText.text = roomOpen ? "ROOM DETAILS  −" : "ROOM DETAILS  +";
        if (sourceRoot == null)
        {
            var source = GameObject.Find("Celestium Tower Cutaway");
            if (source != null) { sourceRoot = source.transform; RebuildArt(); }
        }
        int west = 1, east = 1;
        foreach (var floor in tower.Rules.State.floors)
        { west = Mathf.Max(west, floor.west); east = Mathf.Max(east, floor.east); }
        int wings = west * 100 + east;
        int panels = (residentsOpen ? 1 : 0) + (roomOpen ? 2 : 0);
        bool newState = framedState != tower.Rules.State;
        if (newState || framedWest != wings || framedPanels != panels)
        {
            framedState = tower.Rules.State;
            framedWest = wings;
            framedPanels = panels;
            float width = (west + east + 1) * Cell;
            float viewWidthFraction = 0.88f - (residentsOpen ? 0.14f : 0) - (roomOpen ? 0.14f : 0);   // compact panels (TT 10.4.2)
            float fit = Mathf.Max(3.7f, (width + 1.8f) / (2 * cameraView.aspect * viewWidthFraction));
            // A player who zoomed or panned keeps their view when a wing finishes or a panel opens;
            // a freshly loaded tower is framed to fit.
            if (newState) tower.ClearUserView();
            if (!tower.UserView)
            {
                cameraView.orthographicSize = fit;
                float shift = ((roomOpen ? 1 : 0) - (residentsOpen ? 1 : 0)) *
                    0.08f * cameraView.orthographicSize * 2 * cameraView.aspect;
                cameraView.transform.position = new Vector3(X(22.5f + (east - west) * 0.5f) + shift,
                    newState && west <= 2 ? 0.35f : cameraView.transform.position.y, -30);
                tower.ClampView();
            }
        }
        UpdateSites();
        UpdateGateMarks();
        AnimateHeartBeam();
        var hud = Object.FindAnyObjectByType<TowerHud>();
        if (hud != null && hud != styledHud) { StyleHud(hud); styledHud = hud; }
        portraitTimer += Time.unscaledDeltaTime;
        if (hud != null && portraitTimer >= 0.4f) { portraitTimer = 0; RefreshPortraits(hud); }
    }

    // The Gate's painting: a freestanding gate once its new art exists (Structure/gate_end), else the old gatehouse crop.
    private string GatePath { get { return Resources.Load<Texture2D>(Root + "Structure/gate_end") != null ? "Structure/gate_end" : "Structure/gate"; } }
    private Rect GateCrop { get { return GatePath == "Structure/gate_end" ? Full : new Rect(0.13f, 0.01f, 0.79f, 0.98f); } }

    // "+ GATE" over each open end of the ground floor while a Gate is being placed (TT 10.5.0b).
    private TextMesh[] gateMarks;
    private void UpdateGateMarks()
    {
        bool show = tower.Placing && tower.BuildType == "gate" && tower.Rules.Floor(0) != null;
        if (gateMarks == null)
        {
            if (!show) return;
            gateMarks = new TextMesh[2];
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Gate placement mark " + i, typeof(TextMesh));
                go.transform.SetParent(transform, false);
                var text = go.GetComponent<TextMesh>();
                text.font = font; text.fontSize = 64; text.characterSize = 0.05f; text.text = "+\nGATE";
                text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
                text.color = new Color(1f, 0.85f, 0.45f);
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                gateMarks[i] = text;
            }
        }
        for (int i = 0; i < 2; i++)
        {
            int side = i == 0 ? -1 : 1;
            bool open = show && tower.Rules.GateOn(side) == null;
            gateMarks[i].gameObject.SetActive(open);
            if (open)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
                gateMarks[i].color = new Color(1f, 0.85f, 0.45f, pulse);
                gateMarks[i].transform.position = new Vector3(X(tower.Rules.GateEndX(side) + 0.5f), 0.2f, -2f);
            }
        }
    }

    private Sprite SpriteFor(string path, Rect crop)
    {
        string key = path + ":" + crop;
        Sprite sprite;
        if (sprites.TryGetValue(key, out sprite)) return sprite;
        var texture = Resources.Load<Texture2D>(path);
        if (texture == null) return null;
        texture.wrapMode = TextureWrapMode.Clamp;
        sprite = Sprite.Create(texture, new Rect(crop.x * texture.width, crop.y * texture.height,
            crop.width * texture.width, crop.height * texture.height), new Vector2(0.5f, 0.5f),
            100, 0, SpriteMeshType.FullRect);
        sprite.name = path;
        sprites.Add(key, sprite);
        return sprite;
    }
    private GameObject Layer(string name, string path, Vector3 position, Vector2 size,
        Rect crop, Color tint, Transform parent = null)
    {
        var sprite = SpriteFor(path, crop);
        if (sprite == null) return null;
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent == null ? artRoot : parent, false);
        go.transform.localPosition = position;
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = tint;
        go.transform.localScale = new Vector3(size.x / sprite.bounds.size.x,
            size.y / sprite.bounds.size.y, 1);
        return go;
    }
    private static readonly Rect Full = new Rect(0, 0, 1, 1);
    private GameObject Art(string name, string path, float x, float y, float z,
        float width, float height, Rect? crop = null, Color? tint = null)
    {
        return Layer(name, Root + path, new Vector3(x, y, z), new Vector2(width, height),
            crop ?? Full, tint ?? Color.white);
    }
    // Barns are real 3D models (one per tier, Tools/barn_pipeline.py) seen through the orthographic camera.
    private readonly Dictionary<string, GameObject> modelPrefabs = new Dictionary<string, GameObject>();
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private bool BarnModel(TowerRoom room, float cx, float y, bool lit)
    {
        string path = "AdamsHaven/TowerModels/barn/barn_" + TowerTiers.BarnTier(room.level);
        GameObject prefab;
        if (!modelPrefabs.TryGetValue(path, out prefab))
            modelPrefabs[path] = prefab = Resources.Load<GameObject>(path);
        if (prefab == null) return false;
        var model = Object.Instantiate(prefab, artRoot);
        model.name = "Barn " + room.uid + " " + TowerTiers.BarnTier(room.level);
        // The painted front is one bay per 2 units; the flipped barn anchors on its left bay and grows right.
        model.transform.localPosition = new Vector3(cx, y - 1.0f, 2.6f);
        // The FBX puts the painted front on +Z; turn it to face the camera at -Z.
        model.transform.localRotation = Quaternion.Euler(0, 180, 0) * prefab.transform.localRotation;
        model.transform.localScale = new Vector3(room.flip ? -1 : 1, 1, 1);
        // Assign the tier's baked material directly; the prefab's own material reference can revert on FBX reimport.
        var material = Resources.Load<Material>(path);
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColor, lit ? Color.white : new Color(0.26f, 0.30f, 0.42f));
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (material != null) renderer.sharedMaterial = material;
            renderer.SetPropertyBlock(block);
        }
        return true;
    }

    // Rank art: every building has F, E and D pictures (C and up reuse D). The furnished Shack and Kitchen interiors
    // are rank B and above: shown at F they made a starting room look finished (TT 10.4.3).
    private const int InteriorFromLevel = 5;

    // ---- Room art manifest (TT 10.5.0b, Tools/build_room_art_manifest.py -> Rooms/room_art.json) ----------------------
    // Which picture a building shows at which rank, and the band of the picture that holds the room. A room shows the
    // part of its band that fits its real on-screen size at the picture's own proportions (owner: never stretch a
    // picture; crop while a rank lacks its own art).
    [System.Serializable] private sealed class RoomArtEntry
    {
        public string file, building;
        public int fromRank, toRank, width, height, bays;
        public float left, right, bottom, top;
    }
    [System.Serializable] private sealed class RoomArtManifest { public List<RoomArtEntry> rooms = new List<RoomArtEntry>(); }
    private RoomArtManifest roomArt;

    private RoomArtEntry RoomArtFor(string type, int level)
    {
        if (roomArt == null)
        {
            var json = Resources.Load<TextAsset>(Root + "Rooms/room_art");
            roomArt = json != null ? JsonUtility.FromJson<RoomArtManifest>(json.text) : new RoomArtManifest();
        }
        type = LairCatalog.ArtType(type);
        foreach (var e in roomArt.rooms)
            if (e.building == type && level >= e.fromRank && level <= e.toRank) return e;
        return null;
    }

    // The crop of the entry's band whose proportions match a box `wide` x `tall`: centred across, sitting on the floor.
    private static Rect FitCrop(RoomArtEntry e, float wide, float tall)
    {
        float bandW = (e.right - e.left) * e.width, bandH = (e.top - e.bottom) * e.height;
        float want = wide / tall;
        float w = bandW, h = bandH;
        if (bandW / bandH > want) w = bandH * want; else h = bandW / want;
        float x = e.left * e.width + (bandW - w) / 2, y = e.bottom * e.height;
        return new Rect(x / e.width, y / e.height, w / e.width, h / e.height);
    }

    // The picture and crop for a room `wide` x `tall` on screen; falls back to the old fixed crop without a manifest.
    private string RoomArtFitted(string type, int level, float wide, float tall, out Rect crop)
    {
        var entry = RoomArtFor(type, level);
        if (entry != null && LairCatalog.ArtType(type) != "barn")
        {
            crop = FitCrop(entry, wide, tall);
            return "Rooms/" + entry.file;
        }
        string grade = level >= 3 ? "D" : level == 2 ? "E" : "F";
        return RoomArt(type, grade, out crop, level);
    }

    private string RoomArt(string type, string grade, out Rect crop, int level = 1)
    {
        type = LairCatalog.ArtType(type);   // dungeon rooms borrow Tower art until they get their own
        string path = "Rooms/" + type + "_" + grade;
        if (Resources.Load<Texture2D>(Root + path) == null)
            path = "Rooms/" + (type == "quarry" ? "warehouse" : "cottage") + "_F";
        crop = type == "barn" ? Full : new Rect(0.035f, 0.14f, 0.93f, 0.57f);
        if (type == "house" && level >= InteriorFromLevel && Resources.Load<Texture2D>(Root + "Rooms/living_interior_v1") != null)
        { path = "Rooms/living_interior_v1"; crop = new Rect(0.2f, 0, 0.6f, 1); }
        if (type == "kitchen" && level >= InteriorFromLevel && Resources.Load<Texture2D>(Root + "Rooms/kitchen_interior_v1") != null)
        { path = "Rooms/kitchen_interior_v1"; crop = Full; }
        // Transparent guild hall cutaway (Game Assets/buildings/tower/buildings/Guild Hall); cropped to the hall itself.
        if (type == "guild_hall" && grade == "F" && Resources.Load<Texture2D>(Root + "Rooms/guild_hall_F_v2") != null)
        { path = "Rooms/guild_hall_F_v2"; crop = new Rect(0.04f, 0.145f, 0.92f, 0.55f); }
        return path;
    }

    // ---------------------------------------------------------------- construction sites

    private sealed class Site { public TextMesh label; public Transform fill; public float width; public TowerWork work;
        public SpriteRenderer ghost; }
    private readonly List<Site> sites = new List<Site>();
    private Sprite whiteSprite;

    private void BuildSites(int middle, int range)
    {
        foreach (var work in tower.Rules.State.works)
        {
            if (work.floor < middle - range || work.floor > middle + range) continue;
            float y = work.floor * Storey;
            float left, width;
            SpriteRenderer ghostRenderer = null;
            if (work.kind == "room")
            {
                var def = TowerCatalog.Get(work.type);
                if (def == null) continue;
                left = X(work.x); width = TowerTiers.Bays(work.type, 1) * Cell;
                Rect crop;
                string grade = "F";
                string path = RoomArtFitted(work.type, 1, width - 0.07f, 2.20f, out crop);
                // The finished room fades in behind the timber frame as the work advances.
                // The barn painting is a wide bay sitting on the floor line, not a full-height room.
                float ghostHeight = work.type == "barn" ? (width - 0.07f) * 0.82f : 2.20f;
                float ghostY = work.type == "barn" ? y - 1.0f + ghostHeight / 2 : y + 0.14f;
                var ghost = Art("Site preview " + work.type, path, left + width / 2, ghostY, 2.55f,
                    width - 0.07f, ghostHeight, crop);
                if (ghost != null) ghostRenderer = ghost.GetComponent<SpriteRenderer>();
            }
            else if (work.kind == "wing")
            {
                int cell = tower.Rules.NextExpansionX(work.floor, work.side);
                left = X(cell); width = Cell;
            }
            else if (work.kind == "deconstruct")
            {
                var coming = tower.Rules.Room(work.room);
                if (coming == null) continue;
                left = X(coming.x); width = coming.width * Cell;
            }
            else
            {
                left = X(21); width = 3 * Cell;
            }
            float cx = left + width / 2;
            Art("Site floor " + work.kind, "Structure/construction_floor", cx, y + 0.14f, 2.7f,
                width - 0.07f, 2.20f, new Rect(0.06f, 0.04f, 0.28f, 0.90f), new Color(0.8f, 0.8f, 0.85f));
            Art("Site frame " + work.kind, "Structure/construction_frame", cx, y + 0.13f, -0.6f,
                width + 0.06f, 2.32f, Full, new Color(1.15f, 1.08f, 0.95f));
            // Progress bar under the label.
            if (whiteSprite == null)
                whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0, 0.5f), 4);
            float barWidth = Mathf.Max(0.7f, width * 0.7f);
            var back = new GameObject("Site bar back", typeof(SpriteRenderer));
            back.transform.SetParent(artRoot, false);
            back.transform.localPosition = new Vector3(cx - barWidth / 2, y + 0.28f, -1.3f);
            back.transform.localScale = new Vector3(barWidth, 0.10f, 1);
            var backRenderer = back.GetComponent<SpriteRenderer>();
            backRenderer.sprite = whiteSprite; backRenderer.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);
            var fill = new GameObject("Site bar fill", typeof(SpriteRenderer));
            fill.transform.SetParent(artRoot, false);
            fill.transform.localPosition = new Vector3(cx - barWidth / 2, y + 0.28f, -1.35f);
            fill.transform.localScale = new Vector3(barWidth * work.Progress, 0.10f, 1);
            var fillRenderer = fill.GetComponent<SpriteRenderer>();
            fillRenderer.sprite = whiteSprite; fillRenderer.color = new Color(1f, 0.78f, 0.28f);
            Label("BUILDING  0:00", new Vector3(cx, y + 0.62f, -1.3f), width);
            var label = roomLabels[roomLabels.Count - 1];
            sites.Add(new Site { label = label, fill = fill.transform, width = barWidth, work = work,
                ghost = ghostRenderer });
        }
    }

    private void UpdateSites()
    {
        foreach (var site in sites)
        {
            if (site.label == null || site.fill == null) continue;
            site.label.text = "BUILDING  " + TowerRules.Clock(site.work.remaining);
            var scale = site.fill.localScale;
            scale.x = site.width * site.work.Progress;
            site.fill.localScale = scale;
            if (site.ghost != null)
                site.ghost.color = new Color(1, 1, 1, 0.15f + 0.6f * site.work.Progress);
        }
    }

    private void RebuildArt()
    {
        sites.Clear();
        roomLabels.Clear();
        coreBeams.Clear();
        // Hide placeholder architecture; preserve animated residents and event feedback.
        foreach (Transform child in sourceRoot)
        {
            if (child.GetComponent<TowerChibiAnimator>() != null) continue;
            var resident = tower.Rules.State.residents.Find(r => r.name == child.name);
            if (resident != null && child.GetComponent<MeshRenderer>() != null &&
                child.GetComponent<MeshFilter>() != null && resident.origin != "hero" && resident.origin != "body")
            {
                child.GetComponent<MeshRenderer>().enabled = false;
                var scale = child.lossyScale;
                float height = resident.ageStage == 1 ? 0.85f : 1.30f;
                Layer("Illustrated civilian", Root + "Villagers/villager_" + (resident.id % 12).ToString("00"),
                    new Vector3(0, 0.30f / scale.y, -0.30f / scale.z),
                    new Vector2(height * 0.66f / scale.x, height / scale.y), Full, Color.white, child);
                continue;
            }
            string n = child.name;
            bool architecture = n.StartsWith("Floor ") || n.StartsWith("Room ") ||
                n.StartsWith("Empty lot ") || n.StartsWith("Silverbrook ") ||
                n.StartsWith("Heart") || n.StartsWith("Gate") || n == "Celestium crystal" ||
                n.StartsWith("Bed") || n.StartsWith("Table") || n.StartsWith("Stove") ||
                n.StartsWith("Crate") || n.StartsWith("Barrel") || n.StartsWith("Workbench") ||
                n.StartsWith("Well") || n.StartsWith("Crop") || n.StartsWith("Furnace") ||
                n.StartsWith("Storage");
            if (architecture)
                foreach (var renderer in child.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        }
        artRoot = new GameObject("Layered Tower Architecture").transform;
        artRoot.SetParent(sourceRoot, false);
        BuildLandscape();
        int middle = Mathf.RoundToInt(cameraView.transform.position.y / Storey);
        int range = Mathf.CeilToInt(cameraView.orthographicSize / Storey) + 2;
        int highest = 0;
        foreach (var f in tower.Rules.State.floors) highest = Mathf.Max(highest, f.number);
        foreach (var f in tower.Rules.State.floors)
        {
            if (f.number < middle - range || f.number > middle + range) continue;
            float y = f.number * Storey;
            // The Gates are the ends of the ground floor (owner, TT 10.5.0b): the timber frame stops before them and they
            // stand outside it, against the sky.
            int gateWest = f.number == 0 && tower.Rules.GateOn(-1) != null ? 1 : 0;
            int gateEast = f.number == 0 && tower.Rules.GateOn(1) != null ? 1 : 0;
            int startCell = 22 - tower.Rules.DrawnWest(f) + gateWest;
            int endCell = 23 + tower.Rules.DrawnEast(f) - gateEast;
            float left = X(startCell), right = X(endCell);
            float width = right - left;
            float center = (left + right) / 2;
            // Empty founded cells still have an actual timber interior.
            Art("Floor " + f.number + " timber backing", "Structure/tower_structure",
                center, y + 0.14f, 4.5f, width, 2.38f, new Rect(0.025f, 0.19f, 0.95f, 0.70f));
            foreach (var room in tower.Rules.State.rooms)
            {
                if (room.floor != f.number) continue;
                float cx = X(room.x + room.width * 0.5f), rw = room.width * Cell;
                string grade = room.level >= 3 ? "D" : room.level == 2 ? "E" : "F";
                if (room.type == "heart")
                    Art("Celestium Heart sanctuary", "Structure/heart_sanctuary_v1", cx,
                        y + 0.12f, 2, rw - 0.05f, 2.23f, new Rect(0.32f, 0, 0.36f, 1));
                else if (room.type == "gate")
                {
                    // A freestanding Gate closing the floor: taller than a storey, its foot on the slab, leaning on the
                    // frame's end post. The painting opens to the right: the west Gate is mirrored so both face outward.
                    bool west = room.x < TowerRules.CoreX;
                    var gateTexture = Resources.Load<Texture2D>(Root + GatePath);
                    float gateWide = GatePath == "Structure/gate_end" && gateTexture != null ?
                        2.86f * gateTexture.width / gateTexture.height : rw * 1.22f;   // the new art keeps its proportions
                    var gateArt = Art("Celestium entrance", GatePath, cx + (west ? 0.12f : -0.12f), y + 0.42f, 1,
                        gateWide, 2.86f, GateCrop);
                    if (west) Mirror(gateArt, -1);
                }
                else if (room.type == "barn" && BarnModel(room, cx, y, tower.Rules.IsPowered(room)))
                {
                    if (!tower.Rules.IsPowered(room)) Label("NO FIREWOOD", new Vector3(cx, y + 0.35f, -1.2f), rw);
                }
                else
                {
                    Rect crop;
                    string path = RoomArtFitted(room.type, room.level, rw - 0.07f, 2.20f, out crop);
                    // Rooms the hearths cannot light go dark, Fallout Shelter style.
                    bool lit = tower.Rules.IsPowered(room);
                    Art("Furnished " + room.type + " " + room.uid, path, cx, y + 0.14f, 2.6f,
                        rw - 0.07f, 2.20f, crop,
                        lit ? (LairCatalog.Is(room.type) ? LairCatalog.Tint : new Color(1.12f, 1.08f, 1.01f)) : new Color(0.26f, 0.30f, 0.42f));
                    if (!lit) Label("NO FIREWOOD", new Vector3(cx, y + 0.35f, -1.2f), rw);
                }
                // Same-type neighbours read as one merged hall: no post between them.
                var westNeighbour = tower.Rules.RoomAt(room.floor, room.x - 1);
                if (room.type != "gate" && (westNeighbour == null || westNeighbour.type != room.type || room.type == "heart"))
                    Post(X(room.x), y);
                string label = room.type == "heart" ? "HEART" : room.type == "gate" ? "GATE" :
                    TowerCatalog.Get(room.type).displayName.ToUpperInvariant();
                if (room.type != "gate") Label(label, new Vector3(cx, y + 1.38f, -1.2f), rw);   // the Gate art speaks for itself
            }
            // Tiled beams retain the scale of stonework rather than stretching one texture across a floor.
            for (int cell = startCell; cell < endCell; cell += 2)
            {
                float span = Mathf.Min(2, endCell - cell) * Cell;
                float cx = X(cell) + span / 2;
                Art("Crystal masonry footing", "Structure/tower_frame", cx, y - 1.16f, -0.9f,
                    span, 0.34f, new Rect(0.04f, 0.012f, 0.27f, 0.14f));
                Art("Carved oak lintel", "Structure/tower_frame", cx, y + 1.35f, -0.8f,
                    span, 0.30f, new Rect(0.05f, 0.90f, 0.27f, 0.09f));
            }
            Post(left, y); Post(right, y);
            if (Overlay != "off") DrawOverlay(f, left, right, y);
            if (f.number != tower.Rules.HeartFloor)
            {
                Post(X(TowerRules.CoreX), y);
                Post(X(TowerRules.CoreX + 1), y);
                string landing = f.landing == "freight_lift" ? "freight_lift" : "stairwell";
                bool sealedShaft = tower.Rules.ShaftSealed(f.number);   // Dungeon Mode: stairs sit at the wing ends
                if (f.landing != "energy" && !sealedShaft)
                    Art("Core landing " + f.number, "Structure/" + landing, X(22.5f),
                        y + 0.06f, 1.7f, Cell * 0.90f, 2.24f);
                bool vault = tower.Rules.Merged && f.number == tower.Rules.VaultFloor;   // one world: the Heart's vault
                Label(vault ? "VAULT" : sealedShaft ? "SEALED" : f.landing == "freight_lift" ? "FREIGHT" : f.landing == "stairs" ? "STAIRS" : "CELESTIUM",
                    new Vector3(X(22.5f), y + 1.38f, -1.3f), Cell);
            }
            Label(tower.Rules.FloorLabel(f.number),
                new Vector3(left - 0.48f, y - 1.13f, -1), 0.7f);
            if (f.number == highest && tower.Rules.FloorWork(f.number + 1) == null)
                Roof(center, y + 2.18f, width + 0.72f);
            // The floating rock slab only crowns a tower with nothing dug beneath it; it would cover B1 or a basement.
            if (f.number == 0 && tower.Rules.Floor(-1) == null)
                Art("Ivy and stone foundation", "Structure/foundation_v1", center, y - 2.06f, 3.5f,
                    width + 2.4f, 3.1f);
            for (int cell = startCell; cell < endCell; cell++)
                if (cell != TowerRules.CoreX && tower.Rules.RoomAt(f.number, cell) == null &&
                    tower.Rules.WorkRoomAt(f.number, cell) == null)
                    Label("+", new Vector3(X(cell + 0.5f), y + 0.05f, -0.8f), 0.5f);
        }
        BuildSites(middle, range);
    }

    // The painted world: one landscape/sky painting with a mirrored copy on each side, and geology under all of
    // it. The camera never shows past these bounds (AdamsHavenPrototype.ClampCamera), even fully zoomed out.
    private const float Ground = -1.23f, PaintedWidth = 68f;
    public static float ArtLeft { get { return X(22) - 1.5f * PaintedWidth; } }
    public static float ArtRight { get { return X(22) + 1.5f * PaintedWidth; } }
    public static float ArtTop { get { return Ground + (TowerRules.FloorMax + 3) * Storey; } }
    public static float ArtBottom { get { return Ground - 27 * Storey; } }

    private void BuildLandscape()
    {
        // Match ShelterView's world coordinates: grass is fixed at the surface,
        // Silverwood is east of the gate, and geology continues below every plot.
        const float ground = Ground, worldWidth = PaintedWidth;
        float center = X(22), skyHeight = (TowerRules.FloorMax + 3) * Storey;
        var landscape = Resources.Load<Texture2D>(Root + "Scenery/silverbrook_world_extended_day");
        var material = Resources.Load<Material>(Root + "Scenery/TowerHorizon");
        for (int side = -1; side <= 1; side++)
        {
            float cx = center + side * worldWidth;
            Mirror(Art("Silverbrook high sky " + side, "Scenery/sky_strata_continuous", cx,
                ground + skyHeight / 2, 18, worldWidth, skyHeight), side);
            if (landscape == null) continue;
            float height = worldWidth * landscape.height / landscape.width;
            float bottom = ground - height * 0.215f;
            var horizon = Art("Silverbrook plain and eastern Silverwood " + side, "Scenery/silverbrook_world_extended_day",
                cx, bottom + height / 2, 16, worldWidth, height);
            // A continuous shader blend avoids visible bands between the original sky paintings.
            if (horizon != null && material != null)
                horizon.GetComponent<SpriteRenderer>().sharedMaterial = material;
            Mirror(horizon, side);
        }
        // Columns of geology from ArtLeft to ArtRight (cell = x / Cell + 17.5).
        for (int cell = -30; cell < 78; cell += 6)
        {
            float cx = X(cell + 3);
            TerrainBand("Continuous soil to Celestium rock " + cell, "strata_upper_continuous",
                cx, ground, 12 * Storey, false);
            TerrainBand("Deep bedrock " + cell, "stratum_bedrock", cx,
                ground - 12 * Storey, 3 * Storey, true);
            for (int depth = 15; depth < 27; depth += 3)
                TerrainBand("Celestium depths " + cell + ":" + depth, "stratum_celestium",
                    cx, ground - depth * Storey, 3 * Storey, true);
        }
        if (tower.Rules.State.introPhase == "complete") BuildHeartBeam();
    }
    // ---------------------------------------------------------------- the Heart's beam (TT 10.4.2)

    // Magic waves run out of the Heart along the shaft, up past the highest floor and down past the lowest by two
    // floors each, and fade at the tips. Each half is a column of beam tiles that scroll outward and wrap.
    private const float BeamTile = 3 * Storey, BeamSpeed = 1.4f;
    private readonly List<int> beamDirs = new List<int>();
    private float beamHeartY, beamUp, beamDown;

    private void BuildHeartBeam()
    {
        beamDirs.Clear();
        int lowest = 0, highest = 0;
        foreach (var floor in tower.Rules.State.floors) { lowest = Mathf.Min(lowest, floor.number); highest = Mathf.Max(highest, floor.number); }
        beamHeartY = tower.Rules.HeartFloor * Storey;
        beamUp = (highest + 2) * Storey + Storey * 0.5f - beamHeartY;
        beamDown = beamHeartY - ((lowest - 2) * Storey - Storey * 0.5f);
        for (int dir = -1; dir <= 1; dir += 2)
        {
            int count = Mathf.CeilToInt((dir > 0 ? beamUp : beamDown) / BeamTile) + 1;
            for (int i = 0; i < count; i++)
            {
                var beam = Art("Celestium core energy " + dir + ":" + i, "Structure/heart_beam", X(22.5f), beamHeartY, 3.8f,
                    Cell * 0.78f, BeamTile + 0.02f, new Rect(0.26f, 0.1f, 0.48f, 0.8f), new Color(0.65f, 0.91f, 1, 0.36f));
                if (beam == null) continue;
                var renderer = beam.GetComponent<SpriteRenderer>();
                renderer.flipY = dir < 0;   // the waves curl away from the Heart on both halves
                coreBeams.Add(renderer);
                beamDirs.Add(dir);
            }
        }
    }

    private void AnimateHeartBeam()
    {
        if (coreBeams.Count == 0 || coreBeams.Count != beamDirs.Count) return;
        float t = Time.time, scroll = (t * BeamSpeed) % BeamTile;
        int up = 0, down = 0;
        for (int i = 0; i < coreBeams.Count; i++)
        {
            var beam = coreBeams[i];
            if (beam == null) continue;
            int dir = beamDirs[i], index = dir > 0 ? up++ : down++;
            float far = index * BeamTile + scroll, mid = far - BeamTile * 0.5f, limit = dir > 0 ? beamUp : beamDown;
            beam.transform.localPosition = new Vector3(beam.transform.localPosition.x, beamHeartY + dir * mid, beam.transform.localPosition.z);
            float tip = Mathf.Clamp01((limit - mid) / (BeamTile * 0.75f)) *      // fades out toward the end of the beam
                Mathf.Clamp01(far / BeamTile);                                    // and in as it leaves the Heart
            float wave = 0.72f + 0.28f * Mathf.Sin(mid * 0.9f - t * 3.2f);       // brightness waves travelling outward
            beam.color = new Color(0.65f, 0.91f, 1, 0.38f * wave * tip);
            beam.enabled = far - BeamTile < limit;
        }
    }

    // The side copies are mirrored so their edges meet the middle painting seamlessly.
    private static void Mirror(GameObject layer, int side)
    {
        if (layer == null || side == 0) return;
        var scale = layer.transform.localScale;
        layer.transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
    }

    private void TerrainBand(string name, string texture, float x, float top, float height, bool blend)
    {
        var layer = Art(name, "Scenery/" + texture, x, top - height / 2, 12,
            6 * Cell + 0.015f, height + 0.01f);
        var material = Resources.Load<Material>(Root + "Scenery/TowerTerrainBlend");
        if (blend && layer != null && material != null)
        {
            Material textured;
            if (!terrainMaterials.TryGetValue(texture, out textured))
            {
                textured = new Material(material);
                textured.mainTexture = layer.GetComponent<SpriteRenderer>().sprite.texture;
                terrainMaterials.Add(texture, textured);
            }
            layer.GetComponent<SpriteRenderer>().sharedMaterial = textured;
        }
        // Continue the preceding rock below the boundary so the next stratum blends into it.
        float overlap = Storey * 0.65f;
        Art(name + " transition", "Scenery/" + texture, x, top - height - overlap / 2,
            12.02f, 6 * Cell + 0.015f, overlap + 0.02f,
            new Rect(0, 0, 1, Mathf.Min(1, overlap / height)));
    }
    private void Post(float x, float y)
    {
        Art("Ivy clad timber column", "Structure/tower_frame", x, y + 0.05f, -1,
            0.17f, 2.54f, new Rect(0, 0.17f, 0.042f, 0.80f));
    }
    private void Roof(float center, float y, float width)
    {
        const float height = 2.3f, naturalWidth = 5.75f;
        float left = center - width / 2, right = center + width / 2;
        float capWidth = naturalWidth * 0.30f;
        // Repeat only the plain roof pitch; preserve dormer and end-cap proportions.
        for (float x = left + capWidth; x < right - capWidth; x += 0.5f)
        {
            float span = Mathf.Min(0.5f, right - capWidth - x);
            Art("Slate roof pitch", "Structure/crown_v1", x + span / 2, y - 0.1955f, -0.92f,
                span + 0.01f, height * 0.45f, new Rect(0.26f, 0.19f, 0.09f, 0.45f));
        }
        Art("Roof west cap", "Structure/crown_v1", left + capWidth / 2, y, -0.95f,
            capWidth, height, new Rect(0, 0, 0.30f, 1));
        Art("Roof east cap", "Structure/crown_v1", right - capWidth / 2, y, -0.95f,
            capWidth, height, new Rect(0.70f, 0, 0.30f, 1));
        Art("Central crystal dormer", "Structure/crown_v1", center, y, -0.97f,
            naturalWidth * 0.40f, height, new Rect(0.30f, 0, 0.40f, 1));
    }
    private static readonly Color Unzoned = new Color(0.55f, 0.6f, 0.68f, 0.10f);

    private static Color SpecTint(string spec)
    {
        switch (spec)
        {
            case "residential": return new Color(0.45f, 0.90f, 0.55f, 0.24f);
            case "industry": return new Color(1f, 0.62f, 0.25f, 0.24f);
            case "market": return new Color(1f, 0.86f, 0.30f, 0.24f);
            case "arcane": return new Color(0.70f, 0.50f, 1f, 0.24f);
            default: return new Color(0.40f, 0.75f, 0.85f, 0.22f);
        }
    }

    // One translucent band over the floor plus a name tag at its west end, under the room labels.
    private void DrawOverlay(TowerFloor f, float left, float right, float y)
    {
        var rules = tower.Rules;
        Color tint;
        string text;
        if (Overlay == "districts")
        {
            var district = rules.DistrictAt(f.number);
            var spec = district == null ? null : TowerRules.SpecDef(district.spec);
            tint = district == null ? Unzoned : SpecTint(district.spec);
            text = district == null ? "UNZONED" : district.name.ToUpperInvariant() +
                (spec == null ? "" : "  " + spec.name.ToUpperInvariant());
        }
        else if (Overlay == "coverage")
        {
            int mask = rules.CoverageMask(f.number), count = 0;
            for (int bit = 1; bit <= 8; bit <<= 1) if ((mask & bit) != 0) count++;
            tint = Color.Lerp(new Color(0.95f, 0.30f, 0.25f, 0.22f), new Color(0.30f, 0.90f, 0.45f, 0.22f), count / 4f);
            text = ((mask & TowerRules.MedicalService) != 0 ? "MED " : "") + ((mask & TowerRules.SafetyService) != 0 ? "SAFE " : "") +
                ((mask & TowerRules.FoodService) != 0 ? "FOOD " : "") + ((mask & TowerRules.LeisureService) != 0 ? "FUN" : "");
            if (text.Length == 0) text = "NO SERVICES";
        }
        else
        {
            float appeal = rules.Appeal(f.number);
            tint = Color.Lerp(new Color(0.45f, 0.45f, 0.5f, 0.16f), new Color(1f, 0.55f, 0.85f, 0.30f), appeal / 100f);
            text = "APPEAL " + Mathf.RoundToInt(appeal);
        }
        if (overlaySprite == null)
            overlaySprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        var band = new GameObject("Overlay floor " + f.number, typeof(SpriteRenderer));
        band.transform.SetParent(artRoot, false);
        band.transform.localPosition = new Vector3((left + right) / 2, y + 0.14f, 1.5f);
        band.transform.localScale = new Vector3(right - left, 2.38f, 1);
        var renderer = band.GetComponent<SpriteRenderer>();
        renderer.sprite = overlaySprite;
        renderer.color = tint;
        Label(text, new Vector3(left + 2.1f, y + 0.92f, -1.4f), 3.8f);
    }

    private void Label(string value, Vector3 position, float width)
    {
        if (worldFont == null) worldFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject(value, typeof(TextMesh));
        go.transform.SetParent(artRoot, false);
        go.transform.localPosition = position;
        var text = go.GetComponent<TextMesh>();
        text.font = worldFont; text.fontSize = 48; text.characterSize = 0.037f;
        text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        text.color = new Color(1, 0.85f, 0.53f); text.text = value;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = worldFont.material;
        float actual = renderer.bounds.size.x;
        if (actual > width * 0.86f) go.transform.localScale = Vector3.one * width * 0.86f / actual;
        roomLabels.Add(text);
    }
    private void StyleHud(TowerHud hud)
    {
        // Panels keep the HUD's flat glass skin (TowerUiSkin); only legacy unskinned buttons get restyled here.
        foreach (var image in hud.GetComponentsInChildren<Image>(true))
        {
            if (image.GetComponent<Button>() != null && image.GetComponent<TowerButtonFx>() == null &&
                image.GetComponent<Button>().transition != Selectable.Transition.None && image.color.a > 0.05f)
            {
                var outline = image.gameObject.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.69f, 0.52f, 0.26f, 0.75f);
                outline.effectDistance = new Vector2(1, -1);
                var colors = image.GetComponent<Button>().colors;
                colors.normalColor = new Color(0.72f, 0.77f, 0.82f);
                colors.highlightedColor = new Color(1.22f, 1.14f, 0.98f);
                colors.pressedColor = new Color(0.64f, 0.72f, 0.78f);
                image.GetComponent<Button>().colors = colors;
            }
        }
        foreach (var text in hud.GetComponentsInChildren<Text>(true))
        {
            if (text.name == "Title") { text.text = "ADAMS HAVEN  /  TOWER"; text.fontSize = 20; }
            if (text.name == "Resident heading" || text.name == "Room heading") text.enabled = false;
            if (text.fontSize >= 20) text.fontStyle = FontStyle.Bold;
        }
        var images = hud.GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (image.name == "Resident panel") residentPanel = image.gameObject;
            if (image.name == "Room panel") roomPanel = image.gameObject;
            if (image.name == "Tutorial") tutorialRect = image.rectTransform;
        }
    }
    private Text PanelToggle(Transform parent, bool right, string label, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(right ? 1 : 0, 1);
        rect.pivot = new Vector2(right ? 1 : 0, 1);
        rect.anchoredPosition = new Vector2(right ? -12 : 12, -81);
        rect.sizeDelta = new Vector2(right ? 190 : 154, 34);
        TowerUiSkin.Apply(go.GetComponent<Image>(), new Color(0.13f, 0.20f, 0.27f));
        go.GetComponent<Button>().onClick.AddListener(action);
        var textGo = new GameObject("Panel toggle label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = right ? 15 : 14; text.fontStyle = FontStyle.Bold; text.color = new Color(1, 0.85f, 0.53f);
        text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.text = label;
        TowerUiSkin.StyleLabel(text);
        return text;
    }
    private void RefreshPortraits(TowerHud hud)
    {
        foreach (var button in hud.GetComponentsInChildren<Button>(true))
        {
            var label = button.GetComponentInChildren<Text>();
            if (label == null) continue;
            if (button.image != null) button.image.enabled = !string.IsNullOrEmpty(label.text);
            var outline = button.GetComponent<Outline>();
            if (outline != null) outline.enabled = !string.IsNullOrEmpty(label.text);
            if (button.name.StartsWith("Build choice "))
            {
                RefreshBuildArtwork(button, label);
                continue;
            }
            if (!button.name.StartsWith("Resident row ")) continue;
            var resident = tower.Rules.State.residents.Find(r => label.text.StartsWith(r.name + " "));
            Transform portrait = button.transform.Find("Resident portrait");
            if (resident == null) { if (portrait != null) portrait.gameObject.SetActive(false); continue; }
            if (portrait == null)
            {
                var go = new GameObject("Resident portrait", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(button.transform, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
                rect.pivot = new Vector2(0, 0.5f); rect.anchoredPosition = new Vector2(3, 0);
                rect.sizeDelta = new Vector2(34, 34);
                go.GetComponent<Image>().raycastTarget = false;
                portrait = go.transform;
                label.rectTransform.anchoredPosition += new Vector2(35, 0);
                label.rectTransform.sizeDelta -= new Vector2(35, 0);
                label.alignment = TextAnchor.MiddleLeft;
            }
            string path = resident.origin == "hero" ? "AdamsHaven/Chibi/" + resident.unitId :
                Root + "Villagers/villager_" + (resident.id % 12).ToString("00");
            Rect crop = new Rect(0.08f, 0.49f, 0.84f, 0.5f);
            if (resident.origin == "body")
            {
                path = "AdamsHaven/ChibiMotion/body_" + resident.chassisVariant + "/idle";
                crop = new Rect(2f / 1024, 890f / 1024, 124f / 1024, 131f / 1024);
            }
            var sprite = SpriteFor(path, crop);
            portrait.gameObject.SetActive(sprite != null);
            portrait.GetComponent<Image>().sprite = sprite;
        }
    }
    private void RefreshBuildArtwork(Button button, Text label)
    {
        TowerRoomDef definition = null;
        foreach (var candidate in TowerCatalog.All)
            if (label.text.Contains(candidate.displayName.ToUpperInvariant()))
            { definition = candidate; break; }
        if (definition == null) return;
        var imageTransform = button.transform.Find("Building card artwork");
        if (imageTransform == null)
        {
            var go = new GameObject("Building card artwork", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(button.transform, false);
            imageTransform = go.transform;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(4, 0);
            rect.sizeDelta = new Vector2(43, 41);
            go.GetComponent<Image>().preserveAspect = true;
            go.GetComponent<Image>().raycastTarget = false;
            label.rectTransform.anchoredPosition += new Vector2(46, 0);
            label.rectTransform.sizeDelta -= new Vector2(48, 0);
            label.fontSize = 12;
        }
        string path = Root + "Cards/" + definition.id + "_F";
        if (Resources.Load<Texture2D>(path) == null) path = Root + "Rooms/" + definition.id + "_F";
        var sprite = SpriteFor(path, Full);
        imageTransform.gameObject.SetActive(sprite != null);
        imageTransform.GetComponent<Image>().sprite = sprite;
    }
    private void OnDestroy()
    {
        foreach (var sprite in sprites.Values) if (sprite != null) Destroy(sprite);
        foreach (var material in terrainMaterials.Values) if (material != null) Destroy(material);
    }
}
