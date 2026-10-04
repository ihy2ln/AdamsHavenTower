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

    // ---------------------------------------------------------------- selection

    // Tiles the player has picked: one by a tap, a rectangle by a drag in box mode. The HUD acts on them.
    public readonly List<Vector2Int> Selected = new List<Vector2Int>();
    public int SelectionStamp { get; private set; }
    public bool BoxMode { get; set; }
    private readonly List<GameObject> markers = new List<GameObject>();
    private Transform markerRoot;
    private Vector2Int boxStart;

    public bool TileAt(Vector2 screen, out Vector2Int tile)
    {
        tile = default;
        var ray = cam.ScreenPointToRay(screen);
        if (Mathf.Approximately(ray.direction.y, 0)) return false;
        float t = (Origin.y - ray.origin.y) / ray.direction.y;
        if (t < 0) return false;
        var hit = ray.GetPoint(t) - Origin;
        tile = new Vector2Int(Mathf.RoundToInt(hit.x), Mathf.RoundToInt(hit.z));
        return true;
    }

    public string Describe(Vector2Int t)
    {
        var tile = TowerTownMap.At(t.x, t.y, ShownRank);
        switch (tile)
        {
            case TowerTownMap.Tile.Tower: return "the Tower";
            case TowerTownMap.Tile.Gate: return t.x < 0 ? "West Gate" : "East Gate";
            case TowerTownMap.Tile.Plaza: return "Gate square";
            case TowerTownMap.Tile.Road: return "road";
            case TowerTownMap.Tile.Lot:
                return PreviewRank > 0 ? "lot (preview)" : tower.Rules.LotLabel(tower.Rules.Lot(t.x, t.y));
            default:
                return ShownRank < TowerTiers.MaxRank && TowerTownMap.InRing(t.x, t.y, ShownRank + 1) ?
                    "wild, opens at Heart rank " + TowerTiers.Tier(ShownRank + 1) : "Silverwood";
        }
    }

    public void ClearSelection() { Selected.Clear(); SelectionStamp++; ShowMarkers(); }

    private void Tap(Vector2 screen)
    {
        Vector2Int t;
        if (!TileAt(screen, out t)) return;
        Selected.Clear();
        Selected.Add(t);
        SelectionStamp++;
        ShowMarkers();
    }

    private void SelectBox(Vector2Int a, Vector2Int b)
    {
        Selected.Clear();
        for (int x = Mathf.Min(a.x, b.x); x <= Mathf.Max(a.x, b.x); x++)
            for (int z = Mathf.Min(a.y, b.y); z <= Mathf.Max(a.y, b.y); z++)
                if (TowerTownMap.Buildable(x, z, ShownRank)) Selected.Add(new Vector2Int(x, z));
        SelectionStamp++;
        ShowMarkers();
    }

    private void ShowMarkers()
    {
        if (markerRoot == null) { markerRoot = new GameObject("Selection").transform; markerRoot.SetParent(transform, false); }
        while (markers.Count < Selected.Count)
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(m.GetComponent<Collider>());
            m.transform.SetParent(markerRoot, false);
            m.transform.localScale = new Vector3(1.0f, 0.04f, 1.0f);
            m.GetComponent<Renderer>().sharedMaterial = Mat(new Color(1f, 0.85f, 0.35f));
            markers.Add(m);
        }
        for (int i = 0; i < markers.Count; i++)
        {
            bool on = i < Selected.Count;
            markers[i].SetActive(on);
            if (on) markers[i].transform.position = Origin + new Vector3(Selected[i].x, 0.02f, Selected[i].y);
        }
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
        BoxMode = false;
        ClearSelection();
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
        else if (PreviewRank == 0 && LotSignature() != builtLots) RebuildLots();
    }

    // Changes whenever a lot is added, removed, re-typed, re-ranked or locked.
    private int LotSignature()
    {
        unchecked
        {
            int h = tower.Rules.State.townLots.Count;
            foreach (var lot in tower.Rules.State.townLots)
                h = h * 31 + lot.x * 7919 + lot.z * 104729 + lot.type.GetHashCode() + lot.rank * 13 + (lot.manual ? 1 : 0);
            return h;
        }
    }

    // Call after a HUD command so the change shows at once rather than on the next 1 s check.
    public void RefreshLots() { if (Active && PreviewRank == 0) RebuildLots(); }

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

    private void Press(Vector2 at)
    {
        dragging = true; moved = false; pointerStart = lastPointer = at;
        if (BoxMode) TileAt(at, out boxStart);
    }

    private void Drag(Vector2 at)
    {
        if ((at - pointerStart).sqrMagnitude > 64) moved = true;
        Vector2Int tile;
        if (moved && BoxMode) { if (TileAt(at, out tile)) SelectBox(boxStart, tile); }
        else if (moved) Pan(at - lastPointer);
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
        lotRoot = null;
        if (PreviewRank > 0) BuildPlaceholderTown(rank, rules.State.residents.Count, rules.State.rooms.Count);
        else RebuildLots();
        BuildForest(rank);
        BuildWalkers(rank);
    }

    // ---------------------------------------------------------------- the town's real lots

    private Transform lotRoot;
    private int builtLots;

    // District colours (walls, roofs) until the modelled buildings replace the blocks.
    private static readonly Dictionary<string, Color[]> DistrictLook = new Dictionary<string, Color[]> {
        { "residential", new[] { new Color(0.82f, 0.72f, 0.56f), new Color(0.60f, 0.28f, 0.22f) } },
        { "market", new[] { new Color(0.90f, 0.84f, 0.70f), new Color(0.85f, 0.62f, 0.20f) } },
        { "industry", new[] { new Color(0.55f, 0.52f, 0.50f), new Color(0.32f, 0.30f, 0.30f) } },
        { "arcane", new[] { new Color(0.80f, 0.82f, 0.90f), new Color(0.35f, 0.40f, 0.75f) } },
        { "defence", new[] { new Color(0.50f, 0.50f, 0.55f), new Color(0.38f, 0.38f, 0.44f) } },
    };

    private void RebuildLots()
    {
        if (lotRoot != null) Destroy(lotRoot.gameObject);
        lotRoot = new GameObject("Lots").transform;
        lotRoot.SetParent(root, false);
        builtLots = LotSignature();
        int built = 0;
        foreach (var lot in tower.Rules.State.townLots)
        {
            var def = TowerRules.TownBuilding(lot.type);
            var at = new Vector3(lot.x, 0, lot.z);
            if (def == null) { if (lot.manual) LotBlock("Vacant", PrimitiveType.Cube, at + Vector3.up * 0.02f, new Vector3(0.9f, 0.04f, 0.9f), new Color(0.55f, 0.47f, 0.34f)); continue; }
            built++;
            BuildLot(lot, def, at);
            if (lot.manual)   // a small banner pole marks lots the player controls
            {
                LotBlock("Flag pole", PrimitiveType.Cube, at + new Vector3(0.42f, 0.42f, 0.42f), new Vector3(0.025f, 0.84f, 0.025f), new Color(0.3f, 0.25f, 0.2f));
                LotBlock("Flag", PrimitiveType.Cube, at + new Vector3(0.42f, 0.76f, 0.35f), new Vector3(0.02f, 0.13f, 0.14f), new Color(0.95f, 0.78f, 0.3f));
            }
        }
        Buildings = built;
    }

    private GameObject LotBlock(string name, PrimitiveType type, Vector3 local, Vector3 scale, Color color, Vector3 euler = default)
    {
        var go = Block(name, type, local, scale, color, euler);
        go.transform.SetParent(lotRoot, false);
        return go;
    }

    // Modelled buildings (Tools/town_to_3d.py): TowerModels/town_<type>/<type>_<fd|cb|assr>, a 1x1-footprint prefab.
    private static readonly string[] BandKeys = { "fd", "cb", "assr" };
    private static readonly float[] BandFill = { 0.82f, 0.9f, 0.98f };
    private readonly Dictionary<string, GameObject> townPrefabs = new Dictionary<string, GameObject>();

    private GameObject TownPrefab(string type, int band)
    {
        string path = "AdamsHaven/TowerModels/town_" + type + "/" + type + "_" + BandKeys[band];
        GameObject prefab;
        if (!townPrefabs.TryGetValue(path, out prefab)) townPrefabs[path] = prefab = Resources.Load<GameObject>(path);
        return prefab;
    }

    // A readable stand-in per type: band 0/1/2 (F-D, C-B, A-SSR) sets height and footprint fill.
    private void BuildLot(TowerLot lot, TowerTownBuildingDef def, Vector3 at)
    {
        int band = TowerRules.RankBand(lot.rank);
        var prefab = TownPrefab(def.id, band);
        if (prefab != null)
        {
            var model = Instantiate(prefab, lotRoot);
            model.name = def.id + " " + lot.x + "," + lot.z;
            model.transform.localPosition = at;
            // Vary the facing by lot so a street of the same model does not repeat exactly.
            model.transform.localRotation = Quaternion.Euler(0, 90 * ((lot.x * 7 + lot.z * 13) & 3), 0) * prefab.transform.localRotation;
            model.transform.localScale = prefab.transform.localScale * BandFill[band];
            return;
        }
        var look = DistrictLook[def.district];
        Color wall = look[0], roof = look[1];
        float fill = 0.62f + band * 0.14f, h = 0.6f + band * 0.45f;
        switch (def.id)
        {
            case "stall":
                LotBlock("Counter", PrimitiveType.Cube, at + Vector3.up * 0.2f, new Vector3(fill, 0.4f, fill * 0.6f), wall);
                LotBlock("Awning", PrimitiveType.Cube, at + new Vector3(0, 0.55f + band * 0.1f, 0), new Vector3(fill + 0.1f, 0.06f, fill), roof, new Vector3(12, 0, 0));
                return;
            case "watchtower":
                LotBlock("Tower", PrimitiveType.Cube, at + Vector3.up * (h * 1.2f), new Vector3(0.38f + band * 0.08f, h * 2.4f, 0.38f + band * 0.08f), wall);
                LotBlock("Lookout", PrimitiveType.Cube, at + Vector3.up * (h * 2.4f + 0.1f), new Vector3(0.6f + band * 0.1f, 0.2f, 0.6f + band * 0.1f), roof);
                return;
            case "wallsegment":
                LotBlock("Wall", PrimitiveType.Cube, at + Vector3.up * (0.35f + band * 0.15f), new Vector3(1f, 0.7f + band * 0.3f, 0.35f), wall);
                return;
            case "gatehouse":
                LotBlock("Gatehouse", PrimitiveType.Cube, at + Vector3.up * (h * 0.7f), new Vector3(fill, h * 1.4f, fill), wall);
                LotBlock("Arch", PrimitiveType.Cube, at + Vector3.up * 0.25f, new Vector3(fill + 0.02f, 0.5f, 0.32f), new Color(0.15f, 0.13f, 0.12f));
                return;
            case "mill":
            case "foundry":
                LotBlock("Hall", PrimitiveType.Cube, at + Vector3.up * h / 2, new Vector3(fill, h, fill * 0.8f), wall);
                LotBlock("Chimney", PrimitiveType.Cylinder, at + new Vector3(fill * 0.3f, h + 0.3f, -fill * 0.2f), new Vector3(0.14f, 0.5f + band * 0.15f, 0.14f), roof);
                if (def.id == "mill") LotBlock("Wheel", PrimitiveType.Cylinder, at + new Vector3(-fill * 0.55f, h * 0.5f, 0), new Vector3(h * 0.9f, 0.05f, h * 0.9f), new Color(0.4f, 0.3f, 0.2f), new Vector3(0, 0, 90));
                return;
            case "shrine":
                LotBlock("Plinth", PrimitiveType.Cube, at + Vector3.up * 0.15f, new Vector3(fill, 0.3f, fill), wall);
                LotBlock("Crystal", PrimitiveType.Cube, at + Vector3.up * (0.6f + band * 0.2f), Vector3.one * (0.25f + band * 0.08f), new Color(0.55f, 0.85f, 1f), new Vector3(45, 45, 0));
                return;
            case "bathhouse":
            case "library":
            case "bazaar":
                LotBlock("Hall", PrimitiveType.Cube, at + Vector3.up * h / 2, new Vector3(fill, h, fill), wall);
                LotBlock("Dome", PrimitiveType.Sphere, at + Vector3.up * h, new Vector3(fill * 0.7f, fill * 0.5f, fill * 0.7f), roof);
                return;
            default:   // homes, inn, workshop: walls and a gable
                bool alongX = ((lot.x * 7 + lot.z * 3) & 1) == 0;
                float hh = def.id == "manor" ? h * 1.3f : def.id == "rowhouse" ? h * 1.1f : h;
                LotBlock("Walls", PrimitiveType.Cube, at + Vector3.up * hh / 2, new Vector3(fill, hh, fill), wall);
                LotBlock("Roof", PrimitiveType.Cube, at + Vector3.up * hh, new Vector3(alongX ? fill + 0.08f : fill * 0.72f, fill * 0.72f, alongX ? fill * 0.72f : fill + 0.08f),
                    roof, alongX ? new Vector3(45, 0, 0) : new Vector3(0, 0, 45));
                return;
        }
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
            body.transform.localPosition = new Vector3(0, 0.12f, 0);
            body.transform.localScale = new Vector3(0.1f, 0.11f, 0.1f);
            body.GetComponent<Renderer>().sharedMaterial = Mat(cloaks[i % cloaks.Length]);
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(figure, false);
            head.transform.localPosition = new Vector3(0, 0.29f, 0);
            head.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
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
        // The modelled Tower (TownPrompts/tower, by Heart rank band) when it exists: fit the 5x5 centre, cap the height.
        var prefab = TownPrefab("tower", TowerRules.RankBand(ShownRank));
        if (prefab != null)
        {
            var model = Instantiate(prefab, root);
            model.name = "Tower model";
            model.transform.localPosition = Vector3.zero;
            float tall = 1f;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) tall = Mathf.Max(tall, r.bounds.size.y / model.transform.lossyScale.y);
            float k = Mathf.Min(width - 0.4f, 7.5f / tall);
            model.transform.localScale = prefab.transform.localScale * k;
            return;
        }
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
