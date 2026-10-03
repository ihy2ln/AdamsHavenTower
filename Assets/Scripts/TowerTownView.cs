using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// The TOWN view (TOWER_MODE_GDD 19.2), greybox: the town around the Tower on a square tile grid with a circle-ish ring
// that widens with the Heart rank. It owns its own camera, light and scene root far from the Tower cutaway, so the
// Tower's controller is untouched; the HUD's town overlay (TowerHudTown.cs) catches input so the Tower ignores it.
// Buildings are placeholder blocks until GDD 19.2's rules slice gives the town real lots.
public sealed class TowerTownView : MonoBehaviour
{
    public enum Angle { Iso30, Iso35, Perspective45 }

    private static readonly Vector3 Origin = new Vector3(5000, 0, 0);
    private const int PixelsPerTile = 8, Margin = 6;

    private AdamsHavenPrototype tower;
    private GameObject catcher;
    private Camera cam, towerCam;
    private Light sun;
    private Transform root;
    private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
    private Material groundMaterial;
    private Texture2D groundTexture;
    private readonly List<Light> sceneLights = new List<Light>();
    private readonly List<Transform> walkers = new List<Transform>();
    private Transform walkerRoot;

    private Vector3 focus;
    private float size = 14;
    private int builtRank = -1, builtResidents = -1, builtFloors = -1;
    private float checkTimer;

    // Pointer state
    private bool dragging, pinching, moved;
    private Vector2 lastPointer, pointerStart, lastPinchCenter;
    private float lastPinchDistance;

    // The last tapped tile, for the HUD ("" when nothing is selected).
    public string Selection { get; private set; } = "";
    private GameObject selectionMarker;

    private void Tap(Vector2 screen)
    {
        var ray = cam.ScreenPointToRay(screen);
        if (Mathf.Approximately(ray.direction.y, 0)) return;
        float t = (Origin.y - ray.origin.y) / ray.direction.y;
        if (t < 0) return;
        var hit = ray.GetPoint(t) - Origin;
        int x = Mathf.RoundToInt(hit.x), z = Mathf.RoundToInt(hit.z);
        var tile = TowerTownMap.At(x, z, ShownRank);
        string what;
        switch (tile)
        {
            case TowerTownMap.Tile.Tower: what = "the Tower"; break;
            case TowerTownMap.Tile.Gate: what = x < 0 ? "West Gate" : "East Gate"; break;
            case TowerTownMap.Tile.Plaza: what = "Gate square"; break;
            case TowerTownMap.Tile.Road: what = "road"; break;
            case TowerTownMap.Tile.Lot: what = TowerTownMap.Frontage(x, z, ShownRank) ? "lot, road frontage" : "lot"; break;
            default: what = ShownRank < TowerTiers.MaxRank && TowerTownMap.InRing(x, z, ShownRank + 1) ?
                "wild, opens at Heart rank " + TowerTiers.Tier(ShownRank + 1) : "Silverwood"; break;
        }
        Selection = "(" + x + ", " + z + ")  " + what;
        if (selectionMarker == null)
        {
            selectionMarker = Block("Selection", PrimitiveType.Cube, Vector3.zero, new Vector3(1.04f, 0.05f, 1.04f),
                new Color(1f, 0.92f, 0.5f, 1f));
            selectionMarker.transform.SetParent(transform, false);
        }
        selectionMarker.transform.position = Origin + new Vector3(x, 0.03f, z);
        selectionMarker.SetActive(true);
    }

    public bool Active { get; private set; }
    public Angle Mode { get; private set; } = Angle.Iso35;
    public int PreviewRank { get; private set; }   // 0 = follow the Heart

    public int ShownRank
    { get { return PreviewRank > 0 ? PreviewRank : tower == null || tower.Rules == null ? 1 : tower.Rules.State.heartRank; } }

    public int Buildings { get; private set; }

    public string AngleLabel
    { get { return Mode == Angle.Iso30 ? "ISO 30°" : Mode == Angle.Iso35 ? "ISO 35°" : "PERSPECTIVE 45°"; } }

    public void Initialize(AdamsHavenPrototype controller, GameObject inputCatcher)
    {
        tower = controller;
        catcher = inputCatcher;
        root = new GameObject("Town Root").transform;
        root.SetParent(transform, false);
        root.position = Origin;
        cam = new GameObject("Town Camera").AddComponent<Camera>();
        cam.transform.SetParent(transform, false);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.70f, 0.84f);
        cam.nearClipPlane = 0.3f; cam.farClipPlane = 800f;
        cam.enabled = false;
        sun = new GameObject("Town Sun").AddComponent<Light>();
        sun.transform.SetParent(transform, false);
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(50, -30, 0);
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.Soft;
        sun.enabled = false;
        root.gameObject.SetActive(false);
    }

    public void Enter()
    {
        if (Active) return;
        towerCam = Camera.main;
        if (towerCam != null && towerCam != cam) towerCam.enabled = false;
        // The town has its own sun (with shadows); the Tower's lights are off while the cutaway's camera is.
        sceneLights.Clear();
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light != sun && light.enabled && light.type == LightType.Directional) { light.enabled = false; sceneLights.Add(light); }
        sun.enabled = true;
        root.gameObject.SetActive(true);
        cam.enabled = true;
        Active = true;
        Rebuild();
        ApplyCamera();
    }

    public void Exit()
    {
        if (!Active) return;
        Active = false;
        cam.enabled = false;
        sun.enabled = false;
        foreach (var light in sceneLights) if (light != null) light.enabled = true;
        sceneLights.Clear();
        root.gameObject.SetActive(false);
        if (towerCam != null) towerCam.enabled = true;
        dragging = pinching = false;
        Selection = "";
        if (selectionMarker != null) selectionMarker.SetActive(false);
    }

    public void CycleAngle()
    {
        Mode = Mode == Angle.Iso30 ? Angle.Iso35 : Mode == Angle.Iso35 ? Angle.Perspective45 : Angle.Iso30;
        ApplyCamera();
    }

    // Steps a preview of the ring for another Heart rank (the save is untouched); back past the live rank follows it again.
    public void StepPreview(int step)
    {
        int live = tower.Rules.State.heartRank;
        int next = Mathf.Clamp(ShownRank + step, 1, TowerTiers.MaxRank);
        PreviewRank = next == live ? 0 : next;
        Rebuild();
        focus = Vector3.zero;
        size = Mathf.Clamp(TowerTownMap.Radius(ShownRank) * 0.8f + 2f, 4f, TowerTownMap.Radius(ShownRank) + 6f);
        ApplyCamera();
    }

    private void Update()
    {
        if (!Active || tower == null || tower.Rules == null) return;
        HandleInput();
        ApplyCamera();
        MoveWalkers();
        checkTimer += Time.unscaledDeltaTime;
        if (checkTimer < 1) return;
        checkTimer = 0;
        if (ShownRank != builtRank || tower.Rules.State.residents.Count != builtResidents || FloorsAbove() != builtFloors)
            Rebuild();
    }

    private int FloorsAbove()
    {
        int count = 0;
        foreach (var floor in tower.Rules.State.floors) if (floor.number > 0) count++;
        return count;
    }

    // ---------------------------------------------------------------- camera and input

    private float Pitch { get { return Mode == Angle.Iso30 ? 30 : Mode == Angle.Iso35 ? 35 : 45; } }

    private void ApplyCamera()
    {
        var rotation = Quaternion.Euler(Pitch, 45, 0);
        cam.transform.rotation = rotation;
        var forward = rotation * Vector3.forward;
        if (Mode == Angle.Perspective45)
        {
            cam.orthographic = false;
            cam.fieldOfView = 35;
            float distance = size / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            cam.transform.position = Origin + focus - forward * distance;
        }
        else
        {
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.transform.position = Origin + focus - forward * 200;
        }
    }

    private void ClampFocus()
    {
        float limit = TowerTownMap.Radius(ShownRank) + 4;
        if (focus.magnitude > limit) focus = focus.normalized * limit;
        focus.y = 0;
    }

    private void Pan(Vector2 delta)
    {
        float unitsPerPixel = size * 2 / Mathf.Max(1, Screen.height);
        var rotation = Quaternion.Euler(Pitch, 45, 0);
        var right = rotation * Vector3.right; right.y = 0; right.Normalize();
        var ahead = rotation * Vector3.forward; ahead.y = 0; ahead.Normalize();
        focus -= right * delta.x * unitsPerPixel + ahead * delta.y * unitsPerPixel / Mathf.Sin(Pitch * Mathf.Deg2Rad);
        ClampFocus();
    }

    private void Zoom(float factor)
    {
        size = Mathf.Clamp(size / Mathf.Max(0.01f, factor), 4f, TowerTownMap.Radius(ShownRank) + 6f);
    }

    private bool OverCatcher(Vector2 screen)
    {
        if (EventSystem.current == null) return true;
        var data = new PointerEventData(EventSystem.current) { position = screen };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        return hits.Count > 0 && hits[0].gameObject == catcher;
    }

    private void HandleInput()
    {
        if (Touchscreen.current != null)
        {
            int count = 0;
            Vector2 a = Vector2.zero, b = Vector2.zero;
            foreach (var touch in Touchscreen.current.touches)
                if (touch.press.isPressed)
                { if (count == 0) a = touch.position.ReadValue(); else if (count == 1) b = touch.position.ReadValue(); count++; }
            if (count >= 2)
            {
                float distance = Vector2.Distance(a, b);
                Vector2 center = (a + b) * 0.5f;
                if (lastPinchDistance > 20 && distance > 20) { Zoom(distance / lastPinchDistance); Pan(center - lastPinchCenter); }
                lastPinchDistance = distance; lastPinchCenter = center;
                pinching = true; dragging = false;
                return;
            }
            lastPinchDistance = 0;
            if (pinching) { pinching = count > 0; return; }
            var first = Touchscreen.current.primaryTouch;
            Vector2 finger = first.position.ReadValue();
            if (first.press.wasPressedThisFrame && OverCatcher(finger)) Press(finger);
            else if (dragging && first.press.isPressed) Drag(finger);
            if (dragging && first.press.wasReleasedThisFrame) Release(finger);
        }
        if (Mouse.current == null) return;
        Vector2 position = Mouse.current.position.ReadValue();
        Vector2 wheel = Mouse.current.scroll.ReadValue();
        if (wheel.y != 0 && OverCatcher(position)) Zoom(Mathf.Exp(Mathf.Clamp(wheel.y / 700f, -0.3f, 0.3f)));
        if (Mouse.current.leftButton.wasPressedThisFrame && OverCatcher(position)) Press(position);
        else if (dragging && Mouse.current.leftButton.isPressed) Drag(position);
        if (dragging && Mouse.current.leftButton.wasReleasedThisFrame) Release(position);
    }

    private void Press(Vector2 at) { dragging = true; moved = false; pointerStart = lastPointer = at; }

    private void Drag(Vector2 at)
    {
        if ((at - pointerStart).sqrMagnitude > 64) moved = true;
        if (moved) Pan(at - lastPointer);
        lastPointer = at;
    }

    private void Release(Vector2 at)
    {
        dragging = false;
        if (!moved) Tap(at);
    }

    // ---------------------------------------------------------------- scene

    private static readonly Color WildColor = new Color(0.17f, 0.29f, 0.19f);
    private static readonly Color NextRingColor = new Color(0.24f, 0.37f, 0.24f);
    private static readonly Color LotColor = new Color(0.45f, 0.61f, 0.32f);
    private static readonly Color RoadColor = new Color(0.60f, 0.50f, 0.36f);
    private static readonly Color PlazaColor = new Color(0.70f, 0.68f, 0.62f);
    private static readonly Color StoneColor = new Color(0.33f, 0.33f, 0.38f);

    private void Rebuild()
    {
        var rules = tower.Rules;
        int rank = ShownRank;
        builtRank = rank; builtResidents = rules.State.residents.Count; builtFloors = FloorsAbove();
        for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        BuildGround(rank);
        BuildTower();
        BuildRing(TowerTownMap.Radius(rank) + 0.5f, new Color(0.95f, 0.80f, 0.45f), 0.18f, "Ring");
        if (rank < TowerTiers.MaxRank)
            BuildRing(TowerTownMap.Radius(rank + 1) + 0.5f, new Color(1f, 1f, 1f, 0.5f), 0.08f, "Next ring");
        BuildPlaceholderTown(rank, rules.State.residents.Count, rules.State.rooms.Count);
        BuildForest(rank);
        BuildWalkers(rank);
    }

    // ---------------------------------------------------------------- townsfolk on the roads

    // One small figure per adult out on an errand (TowerTown.cs): they stroll the roads and the Gate squares, so the
    // town shows the same life the cutaway does. Placeholder capsules until the chibis move in with the lots.
    private void BuildWalkers(int rank)
    {
        walkers.Clear();
        walkerRoot = new GameObject("Townsfolk").transform;
        walkerRoot.SetParent(root, false);
        int count = 0;
        foreach (var resident in tower.Rules.State.residents)
            if (resident.ageStage == 0 && !resident.away && TowerRules.IsErrand(resident.currentTask)) count++;
        count = Mathf.Clamp(count, Mathf.Min(3, tower.Rules.State.residents.Count), 40);
        var skin = new Color(0.95f, 0.80f, 0.66f);
        Color[] cloaks = { new Color(0.55f, 0.20f, 0.22f), new Color(0.20f, 0.35f, 0.60f), new Color(0.30f, 0.50f, 0.25f), new Color(0.60f, 0.50f, 0.20f) };
        for (int i = 0; i < count; i++)
        {
            var figure = new GameObject("Townsfolk " + i).transform;
            figure.SetParent(walkerRoot, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(figure, false);
            body.transform.localPosition = new Vector3(0, 0.26f, 0);
            body.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            body.GetComponent<Renderer>().sharedMaterial = Mat(cloaks[i % cloaks.Length]);
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(figure, false);
            head.transform.localPosition = new Vector3(0, 0.6f, 0);
            head.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            head.GetComponent<Renderer>().sharedMaterial = Mat(skin);
            walkers.Add(figure);
        }
    }

    // Each figure paces a stretch of road: west or east of the Tower, or the north-south road once it is open.
    private void MoveWalkers()
    {
        if (walkers.Count == 0) return;
        int rank = ShownRank;
        float reach = TowerTownMap.Radius(rank) - 1f;
        float t = Time.unscaledTime;
        for (int i = 0; i < walkers.Count; i++)
        {
            uint h = Hash(i * 31 + 7, i * 17 + 3);
            float speed = 0.35f + (h % 100) / 100f * 0.35f;
            float phase = ((h >> 8) % 1000) / 1000f * 20f;
            float along = Mathf.PingPong((t + phase) * speed, reach - (TowerTownMap.TowerHalf + 2)) + TowerTownMap.TowerHalf + 2;
            float lane = ((h >> 16) % 3 - 1) * 0.28f;
            int road = (int)((h >> 20) % (rank >= TowerTownMap.CrossRoadRank ? 4 : 2));
            Vector3 at = road == 0 ? new Vector3(-along, 0, lane) : road == 1 ? new Vector3(along, 0, lane) :
                road == 2 ? new Vector3(lane, 0, along) : new Vector3(lane, 0, -along);
            walkers[i].localPosition = at;
        }
    }

    private Material Mat(Color color)
    {
        Material material;
        if (materials.TryGetValue(color, out material) && material != null) return material;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        material = new Material(shader) { color = color, enableInstancing = true };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
        materials[color] = material;
        return material;
    }

    private GameObject Block(string name, PrimitiveType type, Vector3 local, Vector3 scale, Color color, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        go.transform.SetParent(root, false);
        go.transform.localPosition = local;
        go.transform.localScale = scale;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.GetComponent<Renderer>().sharedMaterial = Mat(color);
        return go;
    }

    // One textured quad for every tile: lots, roads, the plaza, the Tower's footprint and the forest beyond the ring.
    private void BuildGround(int rank)
    {
        int half = TowerTownMap.MaxRadius + Margin, tiles = half * 2 + 1, pixels = tiles * PixelsPerTile;
        if (groundTexture == null || groundTexture.width != pixels)
        {
            groundTexture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        }
        var colors = new Color32[pixels * pixels];
        for (int tx = 0; tx < tiles; tx++)
            for (int tz = 0; tz < tiles; tz++)
            {
                int x = tx - half, z = tz - half;
                var tile = TowerTownMap.At(x, z, rank);
                Color color;
                switch (tile)
                {
                    case TowerTownMap.Tile.Lot: color = LotColor; break;
                    case TowerTownMap.Tile.Road: color = RoadColor; break;
                    case TowerTownMap.Tile.Plaza: color = PlazaColor; break;
                    case TowerTownMap.Tile.Tower:
                    case TowerTownMap.Tile.Gate: color = StoneColor; break;
                    default:
                        color = rank < TowerTiers.MaxRank && TowerTownMap.InRing(x, z, rank + 1) ? NextRingColor : WildColor;
                        break;
                }
                for (int px = 0; px < PixelsPerTile; px++)
                    for (int pz = 0; pz < PixelsPerTile; pz++)
                    {
                        // Faint grid lines on lots so the tiles read.
                        bool edge = tile == TowerTownMap.Tile.Lot && (px == 0 || pz == 0);
                        Color c = edge ? color * 0.88f : color;
                        c.a = 1;
                        colors[(tz * PixelsPerTile + pz) * pixels + tx * PixelsPerTile + px] = c;
                    }
            }
        groundTexture.SetPixels32(colors);
        groundTexture.Apply();
        if (groundMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            groundMaterial = new Material(shader);
        }
        groundMaterial.mainTexture = groundTexture;
        if (groundMaterial.HasProperty("_BaseMap")) groundMaterial.SetTexture("_BaseMap", groundTexture);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ground.name = "Ground";
        Destroy(ground.GetComponent<Collider>());
        ground.transform.SetParent(root, false);
        ground.transform.localPosition = Vector3.zero;
        ground.transform.localRotation = Quaternion.Euler(90, 0, 0);
        ground.transform.localScale = new Vector3(tiles, tiles, 1);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
    }

    // The Tower as the town's landmark: a stone keep that grows with the floors built above ground, the Heart crystal
    // on top, and the two Gates facing west and east.
    private void BuildTower()
    {
        // Shorter than true scale so the landmark never hides the town behind it (a tall tower is the cutaway's job).
        float height = Mathf.Min(7.5f, 3f + FloorsAbove() * 0.25f);
        float width = TowerTownMap.TowerHalf * 2 + 1;
        Block("Tower", PrimitiveType.Cube, new Vector3(0, height / 2, 0), new Vector3(width - 0.4f, height, width - 0.4f),
            new Color(0.40f, 0.38f, 0.44f));
        Block("Tower crown", PrimitiveType.Cube, new Vector3(0, height + 0.3f, 0), new Vector3(width, 0.6f, width),
            new Color(0.30f, 0.28f, 0.34f));
        Block("Heart crystal", PrimitiveType.Cube, new Vector3(0, height + 1.9f, 0), new Vector3(1.5f, 1.5f, 1.5f),
            new Color(0.55f, 0.85f, 1f), new Vector3(45, 45, 0));
        foreach (int side in new[] { -1, 1 })
            Block(side < 0 ? "West Gate" : "East Gate", PrimitiveType.Cube,
                new Vector3(side * (TowerTownMap.TowerHalf + 1), 0.9f, 0), new Vector3(0.9f, 1.8f, 1.6f),
                new Color(0.85f, 0.72f, 0.42f));
    }

    private void BuildRing(float radius, Color color, float width, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        const int Points = 128;
        line.positionCount = Points;
        for (int i = 0; i < Points; i++)
        {
            float a = i * Mathf.PI * 2 / Points;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.06f, Mathf.Sin(a) * radius));
        }
        line.widthMultiplier = width;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var material = new Material(shader) { color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        line.sharedMaterial = material;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static uint Hash(int x, int z)
    {
        unchecked
        {
            uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return h;
        }
    }

    // Placeholder houses, venues and workshops: the lots nearest the roads and the Tower fill first, and the count grows
    // with the population, so a young village hugs the Gates and a city fills the ring.
    private void BuildPlaceholderTown(int rank, int residents, int rooms)
    {
        int r = TowerTownMap.Radius(rank);
        var lots = new List<Vector2Int>();
        for (int x = -r; x <= r; x++)
            for (int z = -r; z <= r; z++)
                if (TowerTownMap.Buildable(x, z, rank)) lots.Add(new Vector2Int(x, z));
        lots.Sort((a, b) =>
        {
            float da = a.sqrMagnitude - (TowerTownMap.Frontage(a.x, a.y, rank) ? 400 : 0);
            float db = b.sqrMagnitude - (TowerTownMap.Frontage(b.x, b.y, rank) ? 400 : 0);
            return da.CompareTo(db);
        });
        int wanted = PreviewRank > 0 ? lots.Count * PreviewRank / (TowerTiers.MaxRank + 2) : 8 + residents * 2 + rooms / 6;
        Buildings = Mathf.Clamp(wanted, 0, lots.Count);
        Color[] walls = { new Color(0.80f, 0.70f, 0.55f), new Color(0.88f, 0.84f, 0.74f), new Color(0.58f, 0.58f, 0.60f) };
        Color[] roofs = { new Color(0.58f, 0.28f, 0.22f), new Color(0.22f, 0.50f, 0.55f), new Color(0.30f, 0.33f, 0.40f) };
        for (int i = 0; i < Buildings; i++)
        {
            var lot = lots[i];
            uint h = Hash(lot.x, lot.y);
            int kind = (int)(h % 7) < 4 ? 0 : (int)(h % 7) < 6 ? 1 : 2;   // homes, venues, workshops
            float height = 0.7f + (h >> 8) % 100 / 100f * (kind == 0 ? 0.6f : 1.0f);
            bool alongX = ((h >> 4) & 1) == 0;
            var at = new Vector3(lot.x, 0, lot.y);
            Block("Lot", PrimitiveType.Cube, at + Vector3.up * height / 2, new Vector3(0.82f, height, 0.82f), walls[kind]);
            // A gable: a cube turned 45 degrees along the ridge.
            Block("Roof", PrimitiveType.Cube, at + Vector3.up * height, new Vector3(alongX ? 0.9f : 0.6f, 0.6f, alongX ? 0.6f : 0.9f),
                roofs[kind], alongX ? new Vector3(45, 0, 0) : new Vector3(0, 0, 45));
        }
    }

    // Silverwood beyond the ring: trees on a band of wild tiles, thinning out at the edge of the map.
    private void BuildForest(int rank)
    {
        int r = TowerTownMap.Radius(rank), outer = Mathf.Min(TowerTownMap.MaxRadius + Margin, r + 9);
        var trunk = new Color(0.36f, 0.27f, 0.20f);
        var leaves = new Color(0.16f, 0.36f, 0.20f);
        for (int x = -outer; x <= outer; x++)
            for (int z = -outer; z <= outer; z++)
            {
                if (TowerTownMap.InRing(x, z, rank)) continue;
                float d = Mathf.Sqrt(x * x + z * z);
                if (d > outer) continue;
                uint h = Hash(x + 911, z - 377);
                if (h % 100 > 38) continue;
                float jx = ((h >> 8) % 100 / 100f - 0.5f) * 0.6f, jz = ((h >> 16) % 100 / 100f - 0.5f) * 0.6f;
                float tall = 1.2f + (h >> 20) % 100 / 100f * 0.9f;
                var at = new Vector3(x + jx, 0, z + jz);
                Block("Trunk", PrimitiveType.Cylinder, at + Vector3.up * 0.3f, new Vector3(0.18f, 0.3f, 0.18f), trunk);
                Block("Tree", PrimitiveType.Capsule, at + Vector3.up * (0.5f + tall / 2), new Vector3(0.75f, tall / 2, 0.75f), leaves);
            }
    }
}
