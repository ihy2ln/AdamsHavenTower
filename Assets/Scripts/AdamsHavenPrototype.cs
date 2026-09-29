using System;
using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// The existing Unity scene keeps this component's GUID. It now runs only the Tower.
public sealed class AdamsHavenPrototype : MonoBehaviour
{
    private const float Cell = 2.0f;
    private const float Storey = 2.75f;
    private static readonly Dictionary<string, Material> FlatMaterials = new Dictionary<string, Material>();
    private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    private readonly List<TowerChibiAnimator> chibis = new List<TowerChibiAnimator>();
    private readonly Dictionary<int, Transform> residentVisuals = new Dictionary<int, Transform>();
    private readonly Dictionary<int, TowerChibiAnimator> residentAnimators = new Dictionary<int, TowerChibiAnimator>();
    private readonly Dictionary<int, Vector3> residentVisualOffsets = new Dictionary<int, Vector3>();
    private TowerRules rules;
    private Camera view;
    private Transform towerRoot;
    private Transform backgroundVisual;
    private int slot = 1;
    private int floor;
    private int selectedRoom;
    private int selectedResident;
    private string buildType = "house";
    private int buildPage;
    private string message = "Touch the Celestium Heart to begin.";
    private float saveTimer;
    private float sceneTimer;
    private int shownResidents;
    private int shownReady;
    private int shownIncidents;
    private int shownDowned;
    private int shownSignature;
    private int pendingWalkResident;
    private Vector3 pendingWalkFrom;
    private float speed = 1;
    private bool placing;
    private bool savesOpen;
    private TowerHud hud;
    private BattleMode battleMode;
    private float speedBeforeBattle;
    private int viewMaskBeforeBattle;
    private Color viewColorBeforeBattle;
    private CameraClearFlags viewClearBeforeBattle;
    private float ignoreInputUntil;
    private float lastBuiltCameraY, lastBuiltZoom;
    private bool pointerDown, pointerMoved;
    private Vector2 lastPointer, pointerStart;
    private float lastPinchDistance;

    private void Awake()
    {
        TowerMilestones.EnsureSlots();
        LoadSlot(1);
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        view = Camera.main;
        if (view == null)
        {
            var cameraObject = new GameObject("Tower Camera");
            view = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }
        view.orthographic = true;
        view.orthographicSize = 12.5f;
        view.transform.position = new Vector3(WorldX(22.5f), 0, -30);
        view.transform.rotation = Quaternion.identity;
        view.backgroundColor = new Color(0.09f, 0.13f, 0.19f);
        if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.LandscapeLeft;
        RebuildScene();
        hud = new GameObject("Tower HUD").AddComponent<TowerHud>();
        hud.Initialize(this);
    }

    private void Update()
    {
        if (rules == null) return;
        if (battleMode != null) return;
        HandleCameraInput();
        rules.Advance(Mathf.Min(Time.deltaTime * speed, 0.25f), true);
        saveTimer += Time.deltaTime;
        if (saveTimer >= 20) { saveTimer = 0; Save(); }
        sceneTimer += Time.deltaTime;
        if (sceneTimer >= 1)
        {
            sceneTimer = 0;
            int ready = 0;
            int middle = Mathf.RoundToInt(view.transform.position.y / Storey);
            int visibleFloors = Mathf.CeilToInt(view.orthographicSize / Storey) + 1;
            foreach (TowerRoom room in rules.State.rooms)
                if (room.ready && room.floor >= middle - visibleFloors &&
                    room.floor <= middle + visibleFloors) ready++;
            if (rules.State.residents.Count != shownResidents || ready != shownReady ||
                rules.State.incidents.Count != shownIncidents ||
                rules.State.residents.FindAll(r => r.downed || (r.origin == "body" && r.charge <= 0)).Count != shownDowned ||
                SceneSignature() != shownSignature)
                RebuildScene();
            if (hud != null) hud.Refresh();
        }
        UpdateResidentVisuals();
        if (backgroundVisual != null)
            backgroundVisual.localPosition = new Vector3(WorldX(22.5f), view.transform.position.y, 13);
        for (int i = 0; i < chibis.Count; i++)
            if (chibis[i] != null) chibis[i].SetPlaybackSpeed(Mathf.Min(speed, 3f));
    }

    private void OnApplicationPause(bool paused) { if (paused) Save(); }
    private void OnApplicationQuit() { Save(); }

    private void Save()
    {
        if (rules == null) return;
        try { TowerSaveFiles.Save(rules.State); }
        catch (Exception ex) { Debug.LogError("Tower save failed: " + ex); }
    }

    private void LoadSlot(int number)
    {
        Save();
        slot = number;
        TowerState state = TowerSaveFiles.Load(number);
        if (state == null) state = TowerMilestones.Create(number);
        rules = new TowerRules(state);
        if (state.savedUnix > 0 && state.introPhase == "complete")
            rules.CatchUp(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - state.savedUnix);
        floor = 0;
        selectedRoom = 0;
        selectedResident = 0;
        pendingWalkResident = 0;
        placing = false;
        message = "Loaded slot " + slot + ": " + state.label + ", day " + state.day + ".";
        savesOpen = false;
        if (view != null) RebuildScene();
    }

    private Texture2D Art(string path)
    {
        Texture2D texture;
        if (!textures.TryGetValue(path, out texture))
        {
            texture = Resources.Load<Texture2D>("AdamsHaven/" + path);
            textures.Add(path, texture);
        }
        return texture;
    }

    private static Material Flat(Color color, Texture2D texture = null)
    {
        string key = ColorUtility.ToHtmlStringRGBA(color) + ":" +
            (texture == null ? "solid" : texture.name);
        Material cached;
        if (FlatMaterials.TryGetValue(key, out cached) && cached != null) return cached;
        var template = Resources.Load<Material>(texture == null ?
            "AdamsHaven/Tower/flat_solid" : "AdamsHaven/Tower/flat_textured");
        if (template == null)
            throw new InvalidOperationException("Tower rendering material is missing from Resources.");
        var material = new Material(template) { color = color };
        if (texture != null) material.mainTexture = texture;
        FlatMaterials[key] = material;
        return material;
    }

    private GameObject Cube(string name, Vector3 position, Vector3 size, Color color, Transform parent)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = Flat(color);
        Destroy(obj.GetComponent<Collider>());
        return obj;
    }

    private GameObject Shape(string name, PrimitiveType shape, Vector3 position,
        Vector3 size, Color color, Transform parent)
    {
        var obj = GameObject.CreatePrimitive(shape);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = Flat(color);
        Destroy(obj.GetComponent<Collider>());
        return obj;
    }

    private void Furnish(TowerRoom room, float x, float y)
    {
        // The painted room plates already contain furniture.
        if (room.type != "heart" && room.type != "gate") return;
        float left = x - room.width * Cell * 0.32f;
        switch (room.type)
        {
            case "heart":
                Shape("Celestium crystal", PrimitiveType.Sphere, new Vector3(x, y + 0.12f, 0.40f),
                    new Vector3(0.70f, 1.10f, 0.62f), new Color(0.23f, 0.83f, 1), towerRoot);
                break;
            case "gate":
                Cube("Gate pillar", new Vector3(x, y, 0.52f), new Vector3(0.27f, 1.88f, 0.45f),
                    new Color(0.47f, 0.69f, 0.78f), towerRoot);
                break;
            case "house": case "cottage": case "terrace_row": case "manor":
                Cube("Bed", new Vector3(left, y - 0.53f, 0.61f),
                    new Vector3(0.65f, 0.22f, 0.84f), new Color(0.44f, 0.28f, 0.24f), towerRoot);
                Cube("Bed cover", new Vector3(left, y - 0.39f, 0.60f),
                    new Vector3(0.57f, 0.10f, 0.74f), new Color(0.60f, 0.38f, 0.45f), towerRoot);
                Cube("Table", new Vector3(x + 0.30f, y - 0.53f, 0.55f),
                    new Vector3(0.54f, 0.13f, 0.45f), new Color(0.58f, 0.35f, 0.18f), towerRoot);
                break;
            case "well":
                Shape("Well rim", PrimitiveType.Cylinder, new Vector3(x, y - 0.49f, 0.47f),
                    new Vector3(0.70f, 0.25f, 0.70f), new Color(0.48f, 0.57f, 0.60f), towerRoot);
                Shape("Well water", PrimitiveType.Cylinder, new Vector3(x, y - 0.35f, 0.47f),
                    new Vector3(0.49f, 0.02f, 0.49f), new Color(0.18f, 0.63f, 0.85f), towerRoot);
                break;
            case "kitchen": case "frosted_mug":
                Cube("Stove", new Vector3(left, y - 0.43f, 0.57f),
                    new Vector3(0.68f, 0.62f, 0.67f), new Color(0.35f, 0.33f, 0.35f), towerRoot);
                Shape("Hearth fire", PrimitiveType.Sphere, new Vector3(left, y - 0.13f, 0.33f),
                    new Vector3(0.31f, 0.31f, 0.18f), new Color(1, 0.48f, 0.13f), towerRoot);
                break;
            case "lumber_mill": case "forge": case "quarry":
                for (int i = 0; i < 3; i++)
                    Cube("Work material", new Vector3(left + i * 0.45f, y - 0.62f, 0.48f),
                        new Vector3(0.38f, 0.29f, 0.62f), room.type == "lumber_mill" ?
                            new Color(0.58f, 0.33f, 0.16f) : new Color(0.45f, 0.48f, 0.52f), towerRoot);
                break;
            default:
                Cube("Work bench", new Vector3(left, y - 0.50f, 0.50f),
                    new Vector3(0.78f, 0.34f, 0.65f), new Color(0.51f, 0.36f, 0.22f), towerRoot);
                Cube("Storage crate", new Vector3(x + 0.36f, y - 0.59f, 0.32f),
                    new Vector3(0.46f, 0.43f, 0.44f), new Color(0.42f, 0.30f, 0.21f), towerRoot);
                break;
        }
    }

    private GameObject Picture(string name, Texture2D texture, Vector3 position, Vector2 size,
        Color tint, Transform parent)
    {
        if (texture == null) return null;
        var obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localRotation = Quaternion.Euler(0, 180, 0);
        obj.transform.localScale = new Vector3(size.x, size.y, 1);
        obj.GetComponent<Renderer>().sharedMaterial = Flat(tint, texture);
        Destroy(obj.GetComponent<Collider>());
        return obj;
    }

    private static float WorldX(float cell) { return (cell - 17.5f) * Cell; }
    private float WorldY(int number) { return number * Storey; }

    private Vector3 ResidentPosition(TowerRoom room, TowerResident resident)
    {
        float x = WorldX(room.x + 0.45f + resident.id % Math.Max(1, room.width) * 0.6f);
        return new Vector3(x, WorldY(room.floor) - 0.17f, -0.30f);
    }

    private void RebuildScene()
    {
        if (towerRoot != null) Destroy(towerRoot.gameObject);
        chibis.Clear();
        residentVisuals.Clear();
        residentAnimators.Clear();
        residentVisualOffsets.Clear();
        towerRoot = new GameObject("Celestium Tower Cutaway").transform;
        int middle = Mathf.RoundToInt(view.transform.position.y / Storey);
        int visibleFloors = Mathf.CeilToInt(view.orthographicSize / Storey) + 1;
        var background = Art("Environment/silverbrook_parallax_background_v1");
        var backdrop = Picture("Silverbrook background", background,
            new Vector3(WorldX(22.5f), view.transform.position.y, 13),
            new Vector2(52, view.orthographicSize * 2.5f),
            new Color(0.55f, 0.62f, 0.70f, 1), towerRoot);
        backgroundVisual = backdrop == null ? null : backdrop.transform;
        var foreground = Art("Environment/silverbrook_parallax_foreground_v1");
        Picture("Silverbrook foreground", foreground, new Vector3(WorldX(22.5f), -4.5f, -7),
            new Vector2(27, 12), new Color(1, 1, 1, 0.32f), towerRoot);

        for (int number = middle - visibleFloors; number <= middle + visibleFloors; number++)
        {
            TowerFloor f = rules.Floor(number);
            if (f == null) continue;
            float y = WorldY(number);
            float leftEdge = WorldX(TowerRules.CoreX - f.west), rightEdge = WorldX(TowerRules.CoreX + 1 + f.east);
            float span = rightEdge - leftEdge, middle3d = (leftEdge + rightEdge) * 0.5f;
            Cube("Floor " + number + " slab", new Vector3(middle3d, y - 1.02f, 1.4f),
                new Vector3(span + 0.4f, 0.25f, 2.7f), new Color(0.25f, 0.22f, 0.26f), towerRoot);
            Cube("Floor " + number + " rear wall", new Vector3(middle3d, y + 0.14f, 2.52f),
                new Vector3(span + 0.4f, 2.05f, 0.23f), new Color(0.23f, 0.25f, 0.32f), towerRoot);
            Cube("Floor " + number + " left wall", new Vector3(leftEdge - 0.4f, y + 0.12f, 1.3f),
                new Vector3(0.23f, 2.2f, 2.8f), new Color(0.33f, 0.29f, 0.31f), towerRoot);
            for (int x = TowerRules.CoreX - f.west; x <= TowerRules.CoreX + f.east; x++)
            {
                bool founded = rules.IsFounded(number, x);
                if (founded && rules.RoomAt(number, x) == null)
                    Cube("Empty lot " + number + ":" + x, new Vector3(WorldX(x + 0.5f), y - 0.03f, 1.73f),
                        new Vector3(Cell - 0.04f, 1.8f, 0.1f), new Color(0.28f, 0.30f, 0.36f), towerRoot);
            }
            foreach (TowerRoom room in rules.State.rooms)
            {
                if (room.floor != number) continue;
                float cx = WorldX(room.x + room.width * 0.5f);
                float width = room.width * Cell - 0.06f;
                Cube("Room " + room.uid + " frame", new Vector3(cx, y - 0.03f, 1.7f),
                    new Vector3(width, 1.83f, 0.17f), new Color(0.27f, 0.23f, 0.30f), towerRoot);
                string artName = room.type == "gate" ? "gate_celestium" : room.type;
                string grade = room.level >= 3 ? "D" : room.level == 2 ? "E" : "F";
                Texture2D art = Art("Tower/" + artName + "_" + grade);
                if (art == null) art = Art("Tower/" + artName + "_F");
                Picture("Room " + room.uid + " art", art, new Vector3(cx, y, 1.52f),
                    new Vector2(width, 1.79f), Color.white, towerRoot);
                Furnish(room, cx, y);
                var incident = rules.State.incidents.Find(i => i.roomUid == room.uid);
                if (incident != null)
                {
                    if (incident.kind == "fire")
                    {
                        var flames = Art("Tower/Incident/fire_overlay_v1");
                        Picture("Fire in room " + room.uid, flames,
                            new Vector3(cx, y - 0.28f, 0.95f),
                            new Vector2(width, 1.22f), Color.white, towerRoot);
                    }
                    Color warning = incident.kind == "illness" ? new Color(0.70f, 0.35f, 0.78f) :
                        incident.kind == "pests" ? new Color(0.58f, 0.83f, 0.27f) :
                        new Color(0.94f, 0.30f, 0.18f);
                    Cube("Incident warning " + room.uid, new Vector3(cx, y + 0.91f, 0.83f),
                        new Vector3(width, 0.10f, 0.1f), warning, towerRoot);
                }
                if (room.ready)
                    Cube("Ready glow " + room.uid, new Vector3(cx, y + 0.91f, 1.3f),
                        new Vector3(width, 0.07f, 0.1f), new Color(1, 0.78f, 0.30f), towerRoot);
            }
        }
        foreach (TowerResident resident in rules.State.residents)
        {
            if (resident.away || resident.exploring) continue;
            TowerRoom room = rules.Room(resident.currentRoom) ?? rules.Room(resident.jobRoom) ??
                rules.Room(resident.homeRoom);
            TowerRoom destinationRoom = rules.Room(resident.targetRoom);
            if (room == null ||
                (room.floor < middle - visibleFloors || room.floor > middle + visibleFloors) &&
                (destinationRoom == null || destinationRoom.floor < middle - visibleFloors ||
                    destinationRoom.floor > middle + visibleFloors)) continue;
            Vector3 position = ResidentPosition(room, resident);
            bool rendered = false;
            string motionId = resident.origin == "body" ?
                "body_" + resident.chassisVariant : resident.unitId;
            if (!string.IsNullOrEmpty(motionId))
            {
                var actor = new GameObject(resident.name + " civilian motion");
                actor.transform.SetParent(towerRoot, false);
                TowerChibiAnimator animator = actor.AddComponent<TowerChibiAnimator>();
                Vector3? walkFrom = pendingWalkResident == resident.id ? pendingWalkFrom : (Vector3?)null;
                bool task = resident.jobRoom > 0 && (resident.origin != "body" ||
                    room.type == TowerMilestones.BodyTaskRoom(resident.chassisVariant));
                rendered = animator.Configure(motionId, task,
                    resident.downed || resident.hp <= 0 ||
                    (resident.origin == "body" && resident.charge <= 0), position, walkFrom);
                if (rendered)
                {
                    chibis.Add(animator);
                    residentVisuals[resident.id] = actor.transform;
                    residentAnimators[resident.id] = animator;
                    residentVisualOffsets[resident.id] = Vector3.zero;
                }
                else Destroy(actor);
            }
            if (!rendered)
            {
                Texture2D chibi = string.IsNullOrEmpty(resident.unitId) ?
                    Art(resident.ageStage == 1 ? "Chibi/child_generic_v1" :
                        "Chibi/villager_generic_v1") : Art("Chibi/" + resident.unitId);
                if (chibi != null)
                {
                    var sprite = Picture(resident.name, chibi, position,
                        new Vector2(resident.ageStage == 1 ? 0.56f : 0.85f,
                            resident.ageStage == 1 ? 0.85f : 1.30f), Color.white, towerRoot);
                    if (sprite != null)
                    { residentVisuals[resident.id] = sprite.transform; residentVisualOffsets[resident.id] = Vector3.zero; }
                }
                else
                {
                    var figure = Cube(resident.name, position + new Vector3(0, -0.30f, 0.30f),
                        resident.ageStage == 1 ? new Vector3(0.26f, 0.49f, 0.28f) :
                            new Vector3(0.35f, 0.75f, 0.32f), resident.origin == "body" ?
                            new Color(0.60f, 0.88f, 0.95f) : resident.ageStage == 1 ?
                            new Color(0.84f, 0.66f, 0.47f) : new Color(0.63f, 0.57f, 0.46f), towerRoot);
                    residentVisuals[resident.id] = figure.transform;
                    residentVisualOffsets[resident.id] = new Vector3(0, -0.30f, 0.30f);
                }
            }
        }
        pendingWalkResident = 0;
        shownResidents = rules.State.residents.Count;
        shownIncidents = rules.State.incidents.Count;
        shownDowned = rules.State.residents.FindAll(r => r.downed ||
            (r.origin == "body" && r.charge <= 0)).Count;
        shownSignature = SceneSignature();
        shownReady = 0;
        foreach (TowerRoom room in rules.State.rooms)
            if (room.ready && room.floor >= middle - visibleFloors &&
                room.floor <= middle + visibleFloors) shownReady++;
        lastBuiltCameraY = view.transform.position.y;
        lastBuiltZoom = view.orthographicSize;
    }

    // Things the cutaway draws that the simple counts miss: where incidents are, which rooms
    // are dark, and room layout changes such as upgrades.
    private int SceneSignature()
    {
        int hash = rules.DarkRoomCount * 7919;
        foreach (var incident in rules.State.incidents) hash = hash * 31 + incident.roomUid;
        foreach (var room in rules.State.rooms) hash = hash * 17 + room.level + (rules.IsPowered(room) ? 0 : 3);
        return hash;
    }

    private static void Fill(Rect rect, Color color)
    {
        Color before = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = before;
    }

    private static void Label(Rect rect, string value, int fontSize, Color color,
        TextAnchor align = TextAnchor.MiddleLeft)
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = fontSize, alignment = align, wordWrap = true };
        style.normal.textColor = color;
        GUI.Label(rect, value, style);
    }

    private static bool Button(Rect rect, string value, bool enabled = true)
    {
        Color before = GUI.color;
        GUI.color = enabled ? new Color(0.90f, 0.82f, 0.66f) : new Color(0.40f, 0.42f, 0.43f);
        bool pressed = GUI.Button(rect, value, new GUIStyle(GUI.skin.button) { fontSize = 16, wordWrap = true });
        GUI.color = before;
        return enabled && pressed;
    }

    private void Action(string error)
    {
        message = error ?? rules.State.log[rules.State.log.Count - 1];
        if (error == null) { RebuildScene(); Save(); }
    }

    private Rect CellRect(float cell, float y, float width, float height)
    {
        Vector3 screen = view.WorldToScreenPoint(new Vector3(WorldX(cell), y, 0));
        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        float pixels = Screen.height / (view.orthographicSize * 2f) / scale;
        return new Rect(screen.x / scale - width * pixels / 2,
            (Screen.height - screen.y) / scale - height * pixels / 2,
            width * pixels, height * pixels);
    }

    private void OnGUI()
    {
        if (hud != null) return;
        if (rules == null || view == null) return;
        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        Matrix4x4 before = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        DrawUI();
        GUI.matrix = before;
    }

    private void DrawUI()
    {
        TowerState state = rules.State;
        Fill(new Rect(0, 0, 1600, 82), new Color(0.035f, 0.065f, 0.10f, 0.94f));
        Label(new Rect(22, 5, 480, 42), "ADAMS HAVEN  •  CELESTIUM TOWER", 28,
            new Color(0.96f, 0.83f, 0.58f));
        Label(new Rect(22, 43, 600, 30), "Day " + state.day + "   |   " + state.label +
            "   |   Heart " + Mathf.CeilToInt(state.heartHp), 18, Color.white);
        Label(new Rect(530, 12, 760, 54), "Gold " + state.gold + "    Celestium " + state.celestium +
            "    Food " + Mathf.CeilToInt(state.food) + "    Water " + Mathf.CeilToInt(state.water) +
            "    Firewood " + Mathf.CeilToInt(state.firewood), 18, Color.white, TextAnchor.MiddleRight);
        if (Button(new Rect(1300, 16, 135, 49), "SAVES")) savesOpen = !savesOpen;
        if (Button(new Rect(1440, 16, 135, 49), speed == 1 ? "SPEED 1×" : "SPEED 2×"))
            speed = speed == 1 ? 2 : 1;

        Fill(new Rect(0, 82, 235, 650), new Color(0.05f, 0.08f, 0.13f, 0.88f));
        Label(new Rect(16, 94, 208, 38), "FLOOR " + (floor >= 0 ? "+" : "") + floor,
            26, Color.white, TextAnchor.MiddleCenter);
        if (Button(new Rect(16, 140, 98, 45), "UP", rules.Floor(floor + 1) != null))
        { floor++; RebuildScene(); }
        if (Button(new Rect(124, 140, 98, 45), "DOWN", rules.Floor(floor - 1) != null))
        { floor--; RebuildScene(); }
        if (Button(new Rect(16, 198, 206, 51), "Open floor above\n" + rules.FloorOpenCost(floor + 1) + " Celestium",
            rules.Floor(floor + 1) == null && floor < 24)) Action(rules.OpenFloor(floor + 1));
        if (Button(new Rect(16, 259, 206, 51), "Open floor below\n" + rules.FloorOpenCost(floor - 1) + " Celestium",
            rules.Floor(floor - 1) == null && floor > -24)) Action(rules.OpenFloor(floor - 1));
        TowerFloor current = rules.Floor(floor);
        if (current != null)
        {
            if (Button(new Rect(16, 320, 206, 54), "Expand west  " + rules.ExpandCost(floor) + " C\n" +
                current.west + "/10 cells", current.west < 10))
            {
                if (floor < 0) rules.Excavate(floor, TowerRules.CoreX - current.west - 1);
                Action(rules.ExpandFloor(floor));
            }
            if (Button(new Rect(16, 386, 206, 48), "Upgrade landing", current.landing != "freight_lift"))
                Action(rules.UpgradeLanding(floor, current.landing == "energy" ? "stairs" : "freight_lift"));
        }
        DrawResident();
        DrawSelectedRoom();

        if (state.introPhase != "complete") DrawTutorial();
        else DrawClickableLots();
        Fill(new Rect(0, 732, 1600, 168), new Color(0.035f, 0.065f, 0.10f, 0.97f));
        Label(new Rect(20, 740, 1350, 34), message, 18, Color.white);
        DrawBuildBar();
        if (savesOpen) DrawSaveMenu();
    }

    private void DrawResident()
    {
        var people = rules.State.residents;
        Label(new Rect(16, 449, 206, 28), "RESIDENTS  " + rules.BiologicalPopulation() +
            "/" + rules.PopulationCap(), 17, new Color(0.96f, 0.80f, 0.46f));
        if (people.Count == 0)
        { Label(new Rect(16, 481, 206, 75), "Choose a starter at the Heart.", 16, Color.white); return; }
        selectedResident = Mathf.Clamp(selectedResident, 0, people.Count - 1);
        TowerResident resident = people[selectedResident];
        Label(new Rect(16, 479, 206, 42), resident.name + "\nHP " + Mathf.RoundToInt(resident.hp) +
            "   Mood " + Mathf.RoundToInt(resident.happiness), 15, Color.white);
        if (Button(new Rect(16, 527, 98, 42), "NEXT"))
            selectedResident = (selectedResident + 1) % people.Count;
        if (Button(new Rect(124, 527, 98, 42), resident.exploring ? "RECALL" : "EXPLORE"))
            Action(resident.exploring ? rules.Recall(resident.id) : rules.SendExploring(resident.id));
    }

    private void DrawSelectedRoom()
    {
        TowerRoom room = rules.Room(selectedRoom);
        Label(new Rect(16, 580, 206, 27), "SELECTED ROOM", 17,
            new Color(0.96f, 0.80f, 0.46f));
        if (room == null)
        { Label(new Rect(18, 610, 198, 78), "Touch a room to see its workers and upgrades.", 16, Color.white); return; }
        TowerRoomDef def = TowerCatalog.Get(room.type);
        Label(new Rect(18, 607, 198, 48), def.displayName + "\nLevel " + room.level +
            "  •  " + Mathf.RoundToInt(room.condition) + "% condition", 17, Color.white);
        if (Button(new Rect(16, 658, 98, 51), room.ready ? "COLLECT" : "WAIT", room.ready))
            Action(rules.Collect(room.uid));
        if (Button(new Rect(124, 658, 98, 51), "UPGRADE", room.level < 3 && room.type != "heart" && room.type != "gate"))
            Action(rules.UpgradeRoom(room.uid));
        if (Button(new Rect(16, 709, 206, 20), "ASSIGN SELECTED RESIDENT", rules.State.residents.Count > 0 &&
            room.type != "heart" && room.type != "gate"))
        {
            TowerResident resident = rules.State.residents[selectedResident];
            TowerRoom previous = rules.Room(resident.jobRoom) ?? rules.Room(resident.homeRoom);
            string error = rules.Assign(resident.id, room.uid);
            if (error == null && previous != null && previous.floor == room.floor)
            {
                pendingWalkResident = resident.id;
                pendingWalkFrom = ResidentPosition(previous, resident);
            }
            Action(error);
        }
    }

    private void DrawTutorial()
    {
        Fill(new Rect(345, 144, 850, 142), new Color(0.035f, 0.07f, 0.11f, 0.91f));
        string phase = rules.State.introPhase;
        string title = phase == "dormant" ? "Awaken the Celestium Heart" :
            phase == "gate" ? "Place the Celestium Gate" :
            phase == "shack" ? "Build the Shack beside the Heart" : "Choose your first resident";
        Label(new Rect(370, 153, 800, 43), title, 26, new Color(1, 0.84f, 0.55f), TextAnchor.MiddleCenter);
        if (phase == "dormant" && Button(new Rect(602, 206, 335, 59), "AWAKEN HEART")) Action(rules.AwakenHeart());
        if (phase == "gate" && Button(new Rect(602, 206, 335, 59), "PLACE GATE")) Action(rules.PlaceIntroGate());
        if (phase == "shack")
            Label(new Rect(380, 209, 775, 51), "Select The Shack below, then touch the empty lot west of the Heart.", 18,
                Color.white, TextAnchor.MiddleCenter);
        if (phase == "choose")
            for (int i = 0; i < 3; i++)
            {
                string[] ids = { "kaela", "ghislaine", "elara" };
                if (Button(new Rect(390 + i * 260, 207, 245, 58), ids[i].ToUpperInvariant()))
                    Action(rules.ChooseStarter(ids[i]));
            }
        if (phase == "shack") DrawClickableLots();
    }

    private void DrawClickableLots()
    {
        for (int x = 12; x <= 33; x++)
        {
            TowerRoom room = rules.RoomAt(floor, x);
            Rect rect = CellRect(x + 0.5f, 0, Cell, 1.8f);
            if (room != null)
            {
                if (room.x != x) continue;
                rect = CellRect(room.x + room.width * 0.5f, 0, room.width * Cell, 1.8f);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                { selectedRoom = room.uid; message = TowerCatalog.Get(room.type).displayName + " selected."; }
            }
            else if (rules.IsFounded(floor, x))
            {
                if (GUI.Button(rect, "+", new GUIStyle(GUI.skin.button) { fontSize = 27 }))
                {
                    string error = rules.Build(buildType, floor, x);
                    Action(error);
                }
            }
        }
    }

    private void DrawBuildBar()
    {
        var state = rules.State;
        int index = 0;
        int visible = 0;
        foreach (TowerRoomDef def in TowerCatalog.All)
        {
            if (def.kind == "heart" || def.kind == "gate" || !state.blueprints.Contains(def.id)) continue;
            if (index++ < buildPage * 8) continue;
            if (visible >= 8) break;
            Rect rect = new Rect(19 + visible * 174, 786, 162, 92);
            Fill(rect, buildType == def.id ? new Color(0.48f, 0.35f, 0.19f) : new Color(0.11f, 0.18f, 0.22f));
            if (Button(rect, def.displayName + "\n" + rules.BuildCost(def.id) + " gold"))
            { buildType = def.id; message = "Selected " + def.displayName + ". Touch an empty lot."; }
            visible++;
        }
        if (index == 0) Label(new Rect(20, 790, 700, 68), "Build cards unlock after the Heart awakens.", 18, Color.white);
        if (Button(new Rect(1417, 786, 160, 43), "ROOMS PAGE " + (buildPage + 1)))
        {
            int count = state.blueprints.Count;
            buildPage = count > 16 ? (buildPage + 1) % 3 : count > 8 ? (buildPage + 1) % 2 : 0;
        }
        if (Button(new Rect(1417, 835, 160, 43), "SAVE NOW")) { Save(); message = "Tower saved."; }
    }

    private void DrawSaveMenu()
    {
        Fill(new Rect(265, 100, 1060, 626), new Color(0.025f, 0.045f, 0.075f, 0.97f));
        Label(new Rect(295, 116, 900, 48), "TOWER CHECKPOINTS", 30,
            new Color(1, 0.83f, 0.54f));
        if (Button(new Rect(1190, 117, 105, 47), "CLOSE")) savesOpen = false;
        for (int i = 0; i < 10; i++)
        {
            int number = i + 1;
            Rect rect = new Rect(294 + i % 2 * 500, 188 + i / 2 * 103, 475, 83);
            if (Button(rect, "SLOT " + number + "   •   DAY " + TowerMilestones.Days[i] +
                "\n" + TowerMilestones.Labels[i] + (number == slot ? "  (CURRENT)" : "")))
            { LoadSlot(number); break; }
        }
    }

    public TowerRules Rules { get { return rules; } }
    public int CurrentSlot { get { return slot; } }
    public int FocusFloor { get { return view == null ? 0 : Mathf.RoundToInt(view.transform.position.y / Storey); } }
    public TowerRoom SelectedRoom { get { return rules == null ? null : rules.Room(selectedRoom); } }
    public TowerResident SelectedPerson
    {
        get
        {
            return rules == null || rules.State.residents.Count == 0 ? null :
                rules.State.residents[Mathf.Clamp(selectedResident, 0, rules.State.residents.Count - 1)];
        }
    }
    public bool InBattle { get { return battleMode != null; } }

    // Building only happens while a room card is in hand (or during the founding Shack step).
    public bool Placing { get { return placing || (rules != null && rules.State.introPhase == "shack"); } }
    public void CancelPlacing() { placing = false; }
    public string CurrentMessage { get { return message; } }

    public bool TryResidentPosition(int id, out Vector3 position)
    {
        Transform visual;
        if (residentVisuals.TryGetValue(id, out visual) && visual != null)
        { position = visual.position; return true; }
        position = Vector3.zero;
        return false;
    }
    public string BuildType { get { return buildType; } }
    public float Speed { get { return speed; } }

    public void SetSpeed(float value)
    {
        speed = value <= 0 ? 0 : value >= 8 ? 8 : value >= 4 ? 4 : value >= 2 ? 2 : 1;
        if (hud != null) hud.Refresh();
    }

    public void FocusOnFloor(int number)
    {
        if (view == null || number < TowerRules.FloorMin || number > TowerRules.FloorMax) return;
        floor = number;
        view.transform.position = new Vector3(view.transform.position.x, number * Storey, -30);
        RebuildScene();
        if (hud != null) hud.Refresh();
    }

    public void SelectPerson(int id)
    {
        int index = rules.State.residents.FindIndex(r => r.id == id);
        if (index < 0) return;
        selectedResident = index;
        message = rules.State.residents[index].name + " selected. Tap a room to inspect the assignment.";
        if (hud != null) hud.Refresh();
    }

    public void SelectRoomById(int uid)
    {
        var room = rules.Room(uid);
        if (room == null) return;
        selectedRoom = uid;
        message = TowerCatalog.Get(room.type).displayName + " selected.";
        if (hud != null) hud.Refresh();
    }

    public void SelectBuildType(string id)
    {
        if (TowerCatalog.Get(id) == null) return;
        buildType = id;
        placing = true;
        message = "Tap an empty + lot to build " + TowerCatalog.Get(id).displayName + ".";
        if (hud != null) hud.Refresh();
    }

    public void Apply(string result)
    {
        Action(result);
        if (hud != null) hud.Refresh();
    }

    public void SaveNow()
    {
        Save();
        message = "Tower saved.";
        if (hud != null) hud.Refresh();
    }

    public void LaunchBattleExpedition()
    {
        if (battleMode != null || rules == null || rules.State.introPhase != "complete") return;
        Save();
        speedBeforeBattle = speed;
        speed = 0;
        viewMaskBeforeBattle = view.cullingMask;
        viewColorBeforeBattle = view.backgroundColor;
        viewClearBeforeBattle = view.clearFlags;
        view.cullingMask = 0;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = new Color(0.025f, 0.045f, 0.075f);
        if (hud != null) hud.gameObject.SetActive(false);
        var director = GetComponent<TowerArtDirector>();
        if (director != null) director.enabled = false;
        battleMode = gameObject.AddComponent<BattleMode>();
        battleMode.Begin(Mathf.Abs(floor), OnBattleExpeditionComplete);
    }

    public void RestartCurrentSlot()
    {
        if (rules == null || !rules.State.defeated) return;
        Save();
        var state = TowerMilestones.Create(1);
        state.slot = slot;
        rules = new TowerRules(state);
        selectedRoom = selectedResident = pendingWalkResident = 0;
        buildType = "house";
        message = "A new Celestium Heart awaits in slot " + slot + ".";
        Save();
        FocusOnFloor(0);
    }

    private void OnBattleExpeditionComplete(bool victory, int gold)
    {
        if (victory)
        {
            rules.State.gold += gold;
            rules.State.ore += 1 + Mathf.Abs(floor) / 4;
            rules.Note("Battle expedition returned with " + gold + " gold and ore.");
            message = "Battle won: +" + gold + " gold and ore.";
        }
        else message = "The battle party withdrew. The Tower is ready for another expedition.";
        if (battleMode != null) Destroy(battleMode);
        battleMode = null;
        speed = speedBeforeBattle;
        view.cullingMask = viewMaskBeforeBattle;
        view.backgroundColor = viewColorBeforeBattle;
        view.clearFlags = viewClearBeforeBattle;
        pointerDown = false;
        ignoreInputUntil = Time.unscaledTime + 0.3f;
        var director = GetComponent<TowerArtDirector>();
        if (director != null) director.enabled = true;
        if (hud != null) { hud.gameObject.SetActive(true); hud.Refresh(); }
        Save();
    }

    public void LoadCheckpoint(int number)
    {
        LoadSlot(number);
        if (view != null) FocusOnFloor(0);
        if (hud != null) hud.Refresh();
    }

    public void AssignSelectedToRoom(int uid)
    {
        var person = SelectedPerson;
        Apply(person == null ? "Select a resident first." : rules.Assign(person.id, uid));
    }

    public void DragAssign(int residentId, Vector2 screen)
    {
        if (OverUI(screen)) return;
        var room = RoomAtScreen(screen);
        if (room == null) { message = "Drop the resident onto a room."; hud.Refresh(); return; }
        SelectPerson(residentId);
        Apply(rules.Assign(residentId, room.uid));
    }

    private TowerRoom RoomAtScreen(Vector2 screen)
    {
        Vector3 world = view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0));
        int number = Mathf.RoundToInt(world.y / Storey);
        int x = Mathf.FloorToInt(world.x / Cell + 17.5f);
        if (Mathf.Abs(world.y - number * Storey) > 1.05f) return null;
        return rules.RoomAt(number, x);
    }

    private void WorldTap(Vector2 screen)
    {
        Vector3 world = view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0));
        var fx = GetComponent<TowerFx>();
        if (fx != null && fx.TryTap(world)) return;
        int number = Mathf.RoundToInt(world.y / Storey);
        int x = Mathf.FloorToInt(world.x / Cell + 17.5f);
        if (Mathf.Abs(world.y - number * Storey) > 1.05f) return;
        var room = rules.RoomAt(number, x);
        if (room != null) { SelectRoomById(room.uid); return; }
        if (number >= TowerRules.FloorMin && number <= TowerRules.FloorMax &&
            rules.Floor(number) != null && rules.IsFounded(number, x))
        {
            if (!Placing)
            {
                message = "Open BUILD and pick a room to place here.";
                if (hud != null) hud.Refresh();
                return;
            }
            int start = x;
            string error = rules.CanBuild(buildType, number, start);
            var blueprint = TowerCatalog.Get(buildType);
            for (int offset = 1; error != null && blueprint != null && offset < blueprint.width; offset++)
            {
                int candidate = x - offset;
                if (rules.CanBuild(buildType, number, candidate) != null) continue;
                start = candidate;
                error = null;
            }
            Apply(error == null ? rules.Build(buildType, number, start) : error);
        }
    }

    private bool OverUI(Vector2 screen)
    {
        if (EventSystem.current == null) return false;
        var data = new PointerEventData(EventSystem.current) { position = screen };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        return hits.Count > 0;
    }

    private void Pan(Vector2 delta)
    {
        float unitsPerPixel = view.orthographicSize * 2 / Mathf.Max(1, Screen.height);
        var position = view.transform.position;
        position.x = Mathf.Clamp(position.x - delta.x * unitsPerPixel, WorldX(11), WorldX(34));
        position.y = Mathf.Clamp(position.y - delta.y * unitsPerPixel,
            TowerRules.FloorMin * Storey, TowerRules.FloorMax * Storey);
        view.transform.position = position;
        floor = FocusFloor;
        if (Mathf.Abs(position.y - lastBuiltCameraY) > Storey * 0.65f) RebuildScene();
    }

    private void Zoom(float delta)
    {
        view.orthographicSize = Mathf.Clamp(view.orthographicSize * (1 - delta), 3.7f, 30f);
        if (Mathf.Abs(view.orthographicSize - lastBuiltZoom) > 0.3f) RebuildScene();
    }

    private void HandleCameraInput()
    {
        if (view == null) return;
        if (Time.unscaledTime < ignoreInputUntil) return;
        if (Mouse.current != null)
        {
            Vector2 position = Mouse.current.position.ReadValue();
            Vector2 wheel = Mouse.current.scroll.ReadValue();
            if (wheel.y != 0 && !OverUI(position)) Zoom(Mathf.Clamp(wheel.y / 900f, -0.15f, 0.15f));
            if (Mouse.current.leftButton.wasPressedThisFrame && !OverUI(position))
            { pointerDown = true; pointerMoved = false; pointerStart = lastPointer = position; }
            if (pointerDown && Mouse.current.leftButton.isPressed)
            {
                Vector2 delta = position - lastPointer;
                if ((position - pointerStart).sqrMagnitude > 64) pointerMoved = true;
                if (pointerMoved) Pan(delta);
                lastPointer = position;
            }
            if (pointerDown && Mouse.current.leftButton.wasReleasedThisFrame)
            { pointerDown = false; if (!pointerMoved) WorldTap(position); }
        }
        if (Touchscreen.current == null) return;
        var touches = Touchscreen.current.touches;
        var first = Touchscreen.current.primaryTouch;
        int count = 0;
        Vector2 a = Vector2.zero, b = Vector2.zero;
        foreach (var touch in touches)
            if (touch.press.isPressed)
            { if (count == 0) a = touch.position.ReadValue(); else if (count == 1) b = touch.position.ReadValue(); count++; }
        if (count >= 2)
        {
            float distance = Vector2.Distance(a, b);
            if (lastPinchDistance > 0) Zoom(Mathf.Clamp((distance - lastPinchDistance) / 400f, -0.12f, 0.12f));
            lastPinchDistance = distance;
            pointerDown = false;
            return;
        }
        lastPinchDistance = 0;
        Vector2 finger = first.position.ReadValue();
        if (first.press.wasPressedThisFrame && !OverUI(finger))
        { pointerDown = true; pointerMoved = false; pointerStart = lastPointer = finger; }
        if (pointerDown && first.press.isPressed)
        {
            if ((finger - pointerStart).sqrMagnitude > 64) pointerMoved = true;
            if (pointerMoved) Pan(finger - lastPointer);
            lastPointer = finger;
        }
        if (pointerDown && first.press.wasReleasedThisFrame)
        { pointerDown = false; if (!pointerMoved) WorldTap(finger); }
    }

    private void UpdateResidentVisuals()
    {
        foreach (var resident in rules.State.residents)
        {
            Transform visual;
            if (!residentVisuals.TryGetValue(resident.id, out visual) || visual == null) continue;
            var fromRoom = rules.Room(resident.currentRoom) ?? rules.Room(resident.homeRoom);
            if (fromRoom == null) continue;
            Vector3 position = ResidentPosition(fromRoom, resident);
            bool traveling = resident.targetRoom > 0 && resident.targetRoom != resident.currentRoom &&
                resident.travelDuration > 0;
            if (traveling)
            {
                var toRoom = rules.Room(resident.targetRoom);
                if (toRoom != null)
                {
                    float t = Mathf.Clamp01(resident.travelSeconds / resident.travelDuration);
                    Vector3 shaftAtStart = new Vector3(WorldX(TowerRules.CoreX + 0.5f), position.y, position.z);
                    Vector3 destination = ResidentPosition(toRoom, resident);
                    Vector3 shaftAtEnd = new Vector3(shaftAtStart.x, destination.y, position.z);
                    position = t < 0.25f ? Vector3.Lerp(position, shaftAtStart, t * 4) :
                        t < 0.75f ? Vector3.Lerp(shaftAtStart, shaftAtEnd, (t - 0.25f) * 2) :
                        Vector3.Lerp(shaftAtEnd, destination, (t - 0.75f) * 4);
                }
            }
            TowerChibiAnimator animator;
            if (residentAnimators.TryGetValue(resident.id, out animator) && animator != null)
            {
                animator.SetSleeping(resident.origin != "body" && resident.currentTask == "rest" &&
                    resident.currentRoom == resident.targetRoom && !resident.downed);
                animator.SetPose(position, traveling, resident.currentTask == "production",
                    resident.downed || (resident.origin == "body" && resident.charge <= 0));
            }
            else visual.localPosition = position + residentVisualOffsets[resident.id];
        }
    }
}
