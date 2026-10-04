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
    // 3D chibis outlive RebuildScene (spawning a rig is costly); off-screen ones are parked inactive.
    private readonly Dictionary<int, TowerResident3D> residents3D = new Dictionary<int, TowerResident3D>();
    private Transform residents3DRoot;
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
    private float wanderClock;
    private readonly Dictionary<int, Vector3> residentSmoothed = new Dictionary<int, Vector3>();
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
    private int dragResident;          // resident being carried to a room, 0 when none
    private bool dragMoved;
    private Vector2 dragStart, dragScreen;
    private Vector2 lastPointer, pointerStart;
    private float lastPinchDistance;
    private Vector2 lastPinchCenter;
    private bool pinching;
    private float pointerDownAt;       // when the current press began, for long-press to move a room
    private bool holdFired;

    private void Awake()
    {
        TowerMilestones.EnsureSlots();
        // Reopen the slot played last (slot 0 is NEW GAME's own slot); a first launch starts on slot 1.
        LoadSlot(Mathf.Clamp(PlayerPrefs.GetInt(LastSlotKey, 1), 0, 10));
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
        if (battleMode != null || expedition != null) return;
        HandleCameraInput();
        SettleZoom();
        rules.Advance(TowerRules.FrameSeconds(Time.deltaTime, speed), true);
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
        foreach (var actor in residents3D.Values)
            if (actor != null && actor.gameObject.activeSelf) actor.SetPlaybackSpeed(Mathf.Min(speed, 3f));
    }

    private void OnApplicationPause(bool paused) { if (paused) Save(); }
    private void OnApplicationQuit() { Save(); }

    private void Save()
    {
        if (rules == null) return;
        try { TowerSaveFiles.Save(rules.State); }
        catch (Exception ex) { Debug.LogError("Tower save failed: " + ex); }
    }

    private const string LastSlotKey = "AdamsHaven.Tower.LastSlot";

    private void LoadSlot(int number, bool saveCurrent = true)
    {
        if (saveCurrent) Save();
        // Resident ids are per save, so another save's rigs must not be reused.
        foreach (var actor in residents3D.Values) if (actor != null) Destroy(actor.gameObject);
        residents3D.Clear();
        slot = number;
        PlayerPrefs.SetInt(LastSlotKey, slot);
        PlayerPrefs.Save();
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
        movingRoom = 0;
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
        float floorY = WorldY(room.floor);
        if (AtStation(resident))
        {
            // Residents at work (or resting) in one room share it out in id order instead of stacking on one spot.
            int slot = 0, count = 0;
            foreach (TowerResident other in rules.State.residents)
            {
                if (other.away || other.exploring || other.currentRoom != room.uid || !AtStation(other)) continue;
                if (other.id < resident.id) slot++;
                count++;
            }
            if (resident.currentRoom != room.uid) { slot = count; count++; }   // still on the way in
            float x = TowerRoomDepth.WorldXInRoom(WorldX(room.x), WorldX(room.x + room.width),
                (slot + 0.5f) / Math.Max(1, count));
            return TowerRoomDepth.Project(floorY, x, TowerRoomDepth.StationDepth(resident.id));
        }
        float x01, d;
        TowerRoomDepth.Wander(resident.id, wanderClock, out x01, out d);
        float wx = TowerRoomDepth.WorldXInRoom(WorldX(room.x), WorldX(room.x + room.width), x01);
        return TowerRoomDepth.Project(floorY, wx, d);
    }

    private static bool AtStation(TowerResident resident)
    {
        string task = resident.currentTask;
        return resident.downed || task == "production" || task == "repair" || task == "haul" ||
            task == "rest" || (resident.targetRoom > 0 && resident.targetRoom != resident.currentRoom);
    }

    private void RebuildScene()
    {
        if (towerRoot != null) Destroy(towerRoot.gameObject);
        chibis.Clear();
        residentVisuals.Clear();
        residentAnimators.Clear();
        residentVisualOffsets.Clear();
        residentSmoothed.Clear();
        var shown3D = new HashSet<int>();
        towerRoot =new GameObject("Celestium Tower Cutaway").transform;
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
            float leftEdge = WorldX(TowerRules.CoreX - rules.DrawnWest(f)), rightEdge = WorldX(TowerRules.CoreX + 1 + rules.DrawnEast(f));
            float span = rightEdge - leftEdge, middle3d = (leftEdge + rightEdge) * 0.5f;
            Cube("Floor " + number + " slab", new Vector3(middle3d, y - 1.02f, 1.4f),
                new Vector3(span + 0.4f, 0.25f, 2.7f), new Color(0.25f, 0.22f, 0.26f), towerRoot);
            Cube("Floor " + number + " rear wall", new Vector3(middle3d, y + 0.14f, 2.52f),
                new Vector3(span + 0.4f, 2.05f, 0.23f), new Color(0.23f, 0.25f, 0.32f), towerRoot);
            Cube("Floor " + number + " left wall", new Vector3(leftEdge - 0.4f, y + 0.12f, 1.3f),
                new Vector3(0.23f, 2.2f, 2.8f), new Color(0.33f, 0.29f, 0.31f), towerRoot);
            for (int x = TowerRules.CoreX - rules.DrawnWest(f); x <= TowerRules.CoreX + rules.DrawnEast(f); x++)
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
            string modelId = TowerResident3D.ModelFor(resident);
            if (modelId != null)
            {
                TowerResident3D actor3D = Resident3D(resident, modelId);
                if (actor3D != null)
                {
                    shown3D.Add(resident.id);
                    if (actor3D.gameObject.activeSelf)
                        residentSmoothed[resident.id] = actor3D.transform.localPosition; // keep walking, no snap
                    else
                    {
                        actor3D.gameObject.SetActive(true);
                        actor3D.Reveal();
                        actor3D.SetPose(position, false, false, resident.downed || resident.hp <= 0 ||
                            (resident.origin == "body" && resident.charge <= 0), room.type);
                    }
                    residentVisuals[resident.id] = actor3D.transform;
                    residentVisualOffsets[resident.id] = Vector3.zero;
                    rendered = true;
                }
            }
            string motionId = resident.origin == "body" ?
                "body_" + resident.chassisVariant : resident.unitId;
            if (!rendered && !string.IsNullOrEmpty(motionId))
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
                // Roster units use their generated chibi art; units without it stand in as a generic villager.
                bool rosterArt = false;
                if (chibi == null && TowerRoster.Unit(resident.unitId) != null)
                {
                    chibi = TowerRoster.Portrait(resident.unitId, "chibi-work") ??
                        TowerRoster.Portrait(resident.unitId, "chibi-casual");
                    rosterArt = chibi != null;
                    if (chibi == null) chibi = Art("Chibi/villager_generic_v1");
                }
                if (chibi != null)
                {
                    var size = rosterArt ? new Vector2(1.15f * chibi.width / chibi.height, 1.15f) :
                        new Vector2(resident.ageStage == 1 ? 0.56f : 0.85f, resident.ageStage == 1 ? 0.85f : 1.30f);
                    var sprite = Picture(resident.name, chibi, position, size, Color.white, towerRoot);
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
        Park3DResidents(shown3D);
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

    private TowerResident3D Resident3D(TowerResident resident, string modelId)
    {
        TowerResident3D actor;
        if (residents3D.TryGetValue(resident.id, out actor) && actor != null && actor.ModelId == modelId)
            return actor;
        if (actor != null) Destroy(actor.gameObject);
        if (residents3DRoot == null) residents3DRoot = new GameObject("Tower 3D residents").transform;
        actor = TowerResident3D.Create(modelId, resident.name, residents3DRoot);
        if (actor != null) actor.gameObject.SetActive(false);   // the caller reveals it at its spot
        residents3D[resident.id] = actor;
        return actor;
    }

    // Off-screen residents keep their rig, inactive; residents who left the tower (or another save) lose it.
    private void Park3DResidents(HashSet<int> shown)
    {
        var gone = new List<int>();
        foreach (var pair in residents3D)
        {
            if (shown.Contains(pair.Key)) continue;
            if (pair.Value != null && rules.State.residents.Exists(r => r.id == pair.Key))
                pair.Value.gameObject.SetActive(false);
            else gone.Add(pair.Key);
        }
        foreach (int id in gone)
        {
            if (residents3D[id] != null) Destroy(residents3D[id].gameObject);
            residents3D.Remove(id);
        }
    }

    private void OnDestroy()
    {
        if (residents3DRoot != null) Destroy(residents3DRoot.gameObject);
    }

    // Things the cutaway draws that the simple counts miss: where incidents are, which rooms
    // are dark, and room layout changes such as upgrades.
    private int SceneSignature()
    {
        int hash = rules.DarkRoomCount * 7919 + rules.LayoutStamp * 15485863 +   // moves, demolitions, districts
            TowerArtDirector.OverlaySignature(rules);
        foreach (var incident in rules.State.incidents) hash = hash * 31 + incident.roomUid;
        foreach (var room in rules.State.rooms) hash = hash * 17 + room.level + (rules.IsPowered(room) ? 0 : 3);
        foreach (var work in rules.State.works) hash = hash * 13 + work.floor * 101 + work.x + work.side * 7 + work.kind.Length;
        return hash + rules.State.rooms.Count * 104729 + rules.State.floors.Count * 1299709;
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
        Label(new Rect(18, 607, 198, 48), def.displayName + "\n" + rules.LevelLabel(room) +
            "  •  " + Mathf.RoundToInt(room.condition) + "% condition", 17, Color.white);
        if (Button(new Rect(16, 658, 98, 51), room.ready ? "COLLECT" : "WAIT", room.ready))
            Action(rules.Collect(room.uid));
        if (Button(new Rect(124, 658, 98, 51), "UPGRADE", room.level < rules.MaxLevel(room) && room.type != "heart" && room.type != "gate"))
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
    public void CancelPlacing() { placing = false; movingRoom = 0; }

    // Moving a placed room: the next tap on a floor picks where it goes (TowerRules.MoveRoom keeps everything in it).
    private int movingRoom;
    public bool Moving { get { return movingRoom > 0 && rules != null && rules.Room(movingRoom) != null; } }
    public TowerRoom MovingRoom { get { return Moving ? rules.Room(movingRoom) : null; } }

    public void BeginMove(int uid)
    {
        var room = rules == null ? null : rules.Room(uid);
        if (room == null) return;
        placing = false;
        movingRoom = uid;
        message = "Tap where the " + TowerCatalog.Get(room.type).displayName + " should go.";
        if (hud != null) hud.Refresh();
    }
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
        view.transform.position = ClampCamera(new Vector3(view.transform.position.x, number * Storey, -30));
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

    private int expeditionDepth;

    public void LaunchBattleExpedition() { LaunchBattleExpedition(Mathf.Abs(floor)); }

    public void LaunchBattleExpedition(int depth)
    {
        if (battleMode != null || expedition != null || rules == null || rules.State.introPhase != "complete") return;
        expeditionDepth = depth;
        HideTower();
        battleMode = gameObject.AddComponent<BattleMode>();
        if (!rules.SkirmishPays)
            battleMode.RewardLine = "Practice fight: today's " + TowerRules.SkirmishPaidWins + " paid skirmishes are done.";
        battleMode.Begin(depth, OnBattleExpeditionComplete);
    }

    // Another mode (battle, guild expedition) takes the screen: pause, save, hide the Tower.
    private void HideTower()
    {
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
        if (residents3DRoot != null) residents3DRoot.gameObject.SetActive(false);
        var director = GetComponent<TowerArtDirector>();
        if (director != null) director.enabled = false;
    }

    private void ShowTower()
    {
        speed = speedBeforeBattle;
        view.cullingMask = viewMaskBeforeBattle;
        view.backgroundColor = viewColorBeforeBattle;
        view.clearFlags = viewClearBeforeBattle;
        pointerDown = false;
        ignoreInputUntil = Time.unscaledTime + 0.3f;
        var director = GetComponent<TowerArtDirector>();
        if (director != null) director.enabled = true;
        if (residents3DRoot != null) residents3DRoot.gameObject.SetActive(true);
        RebuildScene();
        if (hud != null) { hud.gameObject.SetActive(true); hud.Refresh(); }
        Save();
    }

    // ---------------------------------------------------------------- guild expeditions

    private TowerExpeditionUi expedition;
    public bool ExpeditionOpen { get { return expedition != null; } }

    public void OpenExpedition()
    {
        if (battleMode != null || expedition != null || rules == null) return;
        string error = rules.GuildRequired();
        if (error != null) { Apply(error); return; }
        HideTower();
        expedition = gameObject.AddComponent<TowerExpeditionUi>();
        expedition.Begin(this);
    }

    public void CloseExpedition(string result)
    {
        if (expedition != null) expedition.Shutdown();
        expedition = null;
        if (!string.IsNullOrEmpty(result)) message = result;
        else if (rules.State.log.Count > 0) message = rules.State.log[rules.State.log.Count - 1];
        ShowTower();
    }

    public void SaveExpedition() { Save(); }

    // A dungeon fight: the expedition UI hands over the screen and gets the party back afterwards.
    public void LaunchExpeditionBattle(int depth, List<BattleUnit> field, List<BattleUnit> reserve, Action<bool> done,
        string returnLabel = "BACK TO THE DUNGEON", int seed = 0, BattleEncounter encounter = null)
    {
        if (battleMode != null) return;
        Save();
        battleMode = gameObject.AddComponent<BattleMode>();
        battleMode.ReturnLabel = returnLabel;
        // Spoils go into the run's haul (TowerDungeon.ResolveBattle), never straight to the Tower.
        battleMode.RewardLine = "The spoils go into the expedition haul.";
        battleMode.WithdrawLine = "The party falls back without spoils.";
        battleMode.Seed = seed;
        // Expedition fights are summon battles: the fighters are JD's contracts, called out card by card (BattleSummons.cs).
        battleMode.SummonMode = true;
        // Expedition fights can bring an epiphany (CZN): one card glows, and the upgrades taken last for the run.
        if (encounter != null && rules != null) { encounter.CardMods = rules.CardMods(); encounter.Epiphany = true; }
        battleMode.Encounter = encounter;
        battleMode.Begin(depth, field, reserve, (won, gold) =>
        {
            if (battleMode != null && rules != null) rules.KeepEpiphanies(battleMode.EpiphaniesTaken);
            if (battleMode != null) Destroy(battleMode);
            battleMode = null;
            done(won);
            Save();
        });
    }

    public void RestartCurrentSlot()
    {
        if (rules == null || !rules.State.defeated) return;
        Save();
        // GDD 8.5: the tower resets; heroes, Sigils and summon pity carry into the new run.
        foreach (var actor in residents3D.Values) if (actor != null) Destroy(actor.gameObject);
        residents3D.Clear();
        rules = new TowerRules(TowerRules.LegacyRun(rules.State));
        selectedRoom = selectedResident = pendingWalkResident = 0;
        buildType = "house";
        message = "A new Celestium Heart awaits in slot " + slot + ". Legacy rank " + rules.State.legacyRank + ": " +
            TowerRules.LegacyBonusText(rules.State.legacyRank) + ". Your heroes will return once the Tower is founded.";
        Save();
        FocusOnFloor(0);
    }

    private void OnBattleExpeditionComplete(bool victory, int gold)
    {
        if (victory) message = rules.PaySkirmish(gold, expeditionDepth);
        else message = "The battle party withdrew. The Tower is ready for another expedition.";
        if (battleMode != null) Destroy(battleMode);
        battleMode = null;
        ShowTower();
    }

    // GDD 9.6: lead the battle-ready heroes at home out against a gathering siege (TowerSiegeBattle.cs). Battle
    // Mode decides it; the guards' auto-defend only runs if the warning expires without a fight.
    public void LaunchSiegeBattle()
    {
        if (battleMode != null || expedition != null || rules == null || !rules.SiegeComing) return;
        var ids = rules.SiegeFighters();
        string error = rules.BeginSiegeBattle();
        if (error != null) { Apply(error); return; }
        List<BattleUnit> field, reserve;
        TowerSiegeBattle.Party(rules, ids, out field, out reserve);
        HideTower();
        battleMode = gameObject.AddComponent<BattleMode>();
        battleMode.Encounter = TowerSiegeBattle.Encounter(rules);
        battleMode.ReturnLabel = "BACK TO THE TOWER";
        battleMode.RewardLine = "The siege is broken: +" + 100 * rules.State.heartRank + " gold and +" +
            2 * rules.State.heartRank + " Celestium.";
        battleMode.WithdrawLine = "The heroes fall back and the siege reaches the Gates: Last Stand!";
        battleMode.Begin(TowerSiegeBattle.Depth(rules), field, reserve, (won, gold) =>
        {
            rules.ResolveSiegeBattle(won, ids);
            message = won ? "The siege is broken. The forest pulls back." : "Last Stand at the Gate: drive the raiders out!";
            if (battleMode != null) Destroy(battleMode);
            battleMode = null;
            ShowTower();
        });
    }

    // NEW GAME: slot 0 always restarts from a dormant Heart, so the founding and the guided lessons can be replayed.
    public void NewGame()
    {
        Save();
        var fresh = TowerMilestones.Create(0);
        fresh.label = "New Game";
        TowerSaveFiles.Save(fresh);
        LoadSlot(0, false);
        message = "New game started in slot 0. Touch the Celestium Heart to begin.";
        if (view != null) FocusOnFloor(0);
        if (hud != null) hud.Refresh();
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
        if (Moving) { PlaceMovingRoom(number, x); return; }
        var room = rules.RoomAt(number, x);
        if (room != null) { SelectRoomById(room.uid); return; }
        var site = rules.WorkRoomAt(number, x);
        if (site != null)
        {
            message = "Building " + TowerCatalog.Get(site.type).displayName + ": " + TowerRules.Clock(site.remaining) + " left.";
            if (hud != null) hud.Refresh();
            return;
        }
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
            for (int offset = 1; error != null && blueprint != null && offset < TowerTiers.Bays(buildType, 1); offset++)
            {
                int candidate = x - offset;
                if (rules.CanBuild(buildType, number, candidate) != null) continue;
                start = candidate;
                error = null;
            }
            Apply(error == null ? rules.Build(buildType, number, start) : error);
        }
    }

    // A multi-bay room may be tapped on any of its future cells: try the tapped cell as each bay in turn.
    private void PlaceMovingRoom(int number, int x)
    {
        var moving = MovingRoom;
        int start = x;
        string error = rules.CanMoveRoom(moving.uid, number, start);
        for (int offset = 1; error != null && offset < moving.width; offset++)
        {
            if (rules.CanMoveRoom(moving.uid, number, x - offset) != null) continue;
            start = x - offset;
            error = null;
        }
        if (error != null) { Apply(error); return; }
        movingRoom = 0;
        Apply(rules.MoveRoom(moving.uid, number, start));
        selectedRoom = moving.uid;
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
        position.x -= delta.x * unitsPerPixel;
        position.y -= delta.y * unitsPerPixel;
        view.transform.position = ClampCamera(position);
        floor = FocusFloor;
        if (Mathf.Abs(view.transform.position.y - lastBuiltCameraY) > Storey * 0.65f) RebuildScene();
    }

    private Vector3 ClampCamera(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, WorldX(11), WorldX(34));
        position.y = Mathf.Clamp(position.y, TowerRules.FloorMin * Storey, TowerRules.FloorMax * Storey);
        // Never show past the painted world: keep the whole view inside TowerArtDirector's art bounds.
        float halfHeight = view.orthographicSize, halfWidth = halfHeight * view.aspect;
        position.x = Mathf.Clamp(position.x, TowerArtDirector.ArtLeft + halfWidth, TowerArtDirector.ArtRight - halfWidth);
        position.y = Mathf.Clamp(position.y, TowerArtDirector.ArtBottom + halfHeight, TowerArtDirector.ArtTop - halfHeight);
        return position;
    }

    // Zoom and position limited to the painted world; the art director calls this after framing the tower.
    public void ClampView()
    {
        if (view == null) return;
        view.orthographicSize = Mathf.Min(view.orthographicSize, MaxZoom());
        view.transform.position = ClampCamera(view.transform.position);
    }

    private float MaxZoom()
    {
        return Mathf.Min(ZoomMax, (TowerArtDirector.ArtTop - TowerArtDirector.ArtBottom) / 2f,
            (TowerArtDirector.ArtRight - TowerArtDirector.ArtLeft) / (2f * view.aspect));
    }

    private const float ZoomMin = 2.5f, ZoomMax = 30f;
    private float zoomChangedAt = -1;
    // True once the player zoomed or panned by hand; the art director then stops re-framing the camera.
    public bool UserView { get; private set; }
    public void ClearUserView() { UserView = false; }

    // scale > 1 zooms in. The world point under `anchor` (screen pixels) stays under it, like a map.
    private void ZoomBy(float scale, Vector2 anchor)
    {
        float size = Mathf.Clamp(view.orthographicSize / Mathf.Max(0.05f, scale), ZoomMin, MaxZoom());
        if (Mathf.Approximately(size, view.orthographicSize)) return;
        Vector3 before = view.ScreenToWorldPoint(new Vector3(anchor.x, anchor.y, 0));
        view.orthographicSize = size;
        Vector3 after = view.ScreenToWorldPoint(new Vector3(anchor.x, anchor.y, 0));
        var position = view.transform.position + (before - after);
        position.z = view.transform.position.z;
        view.transform.position = ClampCamera(position);
        floor = FocusFloor;
        UserView = true;
        zoomChangedAt = Time.unscaledTime;
        // Zooming out needs more floors drawn now; zooming in can wait until the gesture settles.
        if (size > lastBuiltZoom * 1.12f) RebuildScene();
    }

    private void SettleZoom()
    {
        if (zoomChangedAt < 0 || Time.unscaledTime - zoomChangedAt < 0.25f) return;
        zoomChangedAt = -1;
        if (Mathf.Abs(view.orthographicSize - lastBuiltZoom) > 0.05f) RebuildScene();
    }

    private void HandleCameraInput()
    {
        if (view == null) return;
        if (Time.unscaledTime < ignoreInputUntil) return;
        if (Mouse.current != null)
        {
            Vector2 position = Mouse.current.position.ReadValue();
            Vector2 wheel = Mouse.current.scroll.ReadValue();
            if (wheel.y != 0 && !OverUI(position))
                ZoomBy(Mathf.Exp(Mathf.Clamp(wheel.y / 700f, -0.3f, 0.3f)), position);
            if (Mouse.current.leftButton.wasPressedThisFrame && !OverUI(position) && !BeginDrag(position))
            { pointerDown = true; pointerMoved = false; pointerStart = lastPointer = position; pointerDownAt = Time.unscaledTime; holdFired = false; }
            if (dragResident > 0)
            {
                if (Mouse.current.leftButton.isPressed) UpdateDrag(position);
                if (Mouse.current.leftButton.wasReleasedThisFrame) EndDrag(position);
            }
            else if (pointerDown && Mouse.current.leftButton.isPressed)
            {
                Vector2 delta = position - lastPointer;
                if ((position - pointerStart).sqrMagnitude > 64) pointerMoved = true;
                if (pointerMoved) { Pan(delta); HideHoldRing(); } else TickRoomHold();
                lastPointer = position;
            }
            if (pointerDown && Mouse.current.leftButton.wasReleasedThisFrame)
            { pointerDown = false; HideHoldRing(); if (!pointerMoved) WorldTap(position); }
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
            if (lastPinchDistance > 20 && distance > 20)
            {
                ZoomBy(distance / lastPinchDistance, (a + b) * 0.5f);
                Pan(((a + b) * 0.5f) - lastPinchCenter);   // two-finger drag pans too
            }
            lastPinchDistance = distance;
            lastPinchCenter = (a + b) * 0.5f;
            pinching = true;
            pointerDown = false;
            HideHoldRing();
            return;
        }
        lastPinchDistance = 0;
        // A finger left over after a pinch must not pan or tap until every finger is up.
        if (pinching) { pinching = count > 0; pointerDown = false; return; }
        Vector2 finger = first.position.ReadValue();
        if (first.press.wasPressedThisFrame && !OverUI(finger) && !BeginDrag(finger))
        { pointerDown = true; pointerMoved = false; pointerStart = lastPointer = finger; pointerDownAt = Time.unscaledTime; holdFired = false; }
        if (dragResident > 0)
        {
            if (first.press.isPressed) UpdateDrag(finger);
            if (first.press.wasReleasedThisFrame) EndDrag(finger);
        }
        else if (pointerDown && first.press.isPressed)
        {
            if ((finger - pointerStart).sqrMagnitude > 64) pointerMoved = true;
            if (pointerMoved) { Pan(finger - lastPointer); HideHoldRing(); } else TickRoomHold();
            lastPointer = finger;
        }
        if (pointerDown && first.press.wasReleasedThisFrame)
        { pointerDown = false; HideHoldRing(); if (!pointerMoved) WorldTap(finger); }
    }

    // Press and hold a room without panning to pick it up and move it, like the room card's MOVE button. A ring
    // closes in from 0.15 s; at 0.45 s the room lifts and the release no longer counts as a tap.
    private const float HoldRingAfter = 0.15f, HoldToMove = 0.45f;

    private void TickRoomHold()
    {
        if (holdFired || Moving || rules == null || rules.State.introPhase != "complete") return;
        float held = Time.unscaledTime - pointerDownAt;
        if (held < HoldRingAfter) return;
        var room = RoomAtScreen(pointerStart);
        if (room == null || room.type == "heart" || room.type == "gate") { HideHoldRing(); return; }
        var fx = GetComponent<TowerFx>();
        if (fx != null) fx.ShowHold(room, Mathf.InverseLerp(HoldRingAfter, HoldToMove, held));
        if (held < HoldToMove) return;
        holdFired = true;
        pointerDown = false;
        HideHoldRing();
        string error = rules.CanStartMove(room.uid);
        if (error != null) { Apply(error); return; }
        selectedRoom = room.uid;
        BeginMove(room.uid);
    }

    private void HideHoldRing()
    {
        var fx = GetComponent<TowerFx>();
        if (fx != null) fx.HideHold();
    }

    // Pressing on a chibi picks them up instead of panning; releasing over a room sends them to work there.
    private bool BeginDrag(Vector2 screen)
    {
        Vector3 world = view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0));
        int found = 0;
        float nearest = float.MaxValue;
        foreach (var resident in rules.State.residents)
        {
            Vector3 at;
            if (resident.ageStage != 0 || resident.downed || resident.away || resident.exploring ||
                !TryResidentPosition(resident.id, out at)) continue;
            float dx = Mathf.Abs(world.x - at.x), dy = world.y - at.y;
            if (dx > 0.5f || dy < -0.45f || dy > 0.95f) continue;
            float distance = dx + Mathf.Abs(dy - 0.25f) * 0.5f;
            if (distance < nearest) { nearest = distance; found = resident.id; }
        }
        if (found == 0) return false;
        dragResident = found;
        dragMoved = false;
        dragStart = dragScreen = screen;
        pointerDown = false;
        return true;
    }

    private void UpdateDrag(Vector2 screen)
    {
        dragScreen = screen;
        if ((screen - dragStart).sqrMagnitude > 64 && !dragMoved)
        {
            dragMoved = true;
            var person = rules.State.residents.Find(r => r.id == dragResident);
            if (person != null) message = person.name + ": drop onto a room to put them to work.";
        }
    }

    private void EndDrag(Vector2 screen)
    {
        int id = dragResident;
        bool moved = dragMoved;
        dragResident = 0; dragMoved = false;
        if (!moved) { SelectPerson(id); return; }
        DragAssign(id, screen);
    }

    private void UpdateResidentVisuals()
    {
        wanderClock += Time.deltaTime * speed;
        foreach (var resident in rules.State.residents)
        {
            Transform visual;
            if (!residentVisuals.TryGetValue(resident.id, out visual) || visual == null) continue;
            var fromRoom = rules.Room(resident.currentRoom) ?? rules.Room(resident.homeRoom);
            if (fromRoom == null) continue;
            Vector3 position = ResidentPosition(fromRoom, resident);
            bool strolling = false;
            if (!(resident.targetRoom > 0 && resident.targetRoom != resident.currentRoom &&
                resident.travelDuration > 0))
            {
                // Glide toward the wander/station spot so state changes walk instead of snapping.
                Vector3 shown;
                if (!residentSmoothed.TryGetValue(resident.id, out shown)) shown = position;
                // 3D chibis stroll at their natural walking pace (~0.7 units/s stride, TowerChibi3D.GroundSpeed).
                float pace = residents3D.ContainsKey(resident.id) ? 0.8f : 1.4f;
                float step = pace * Time.deltaTime * Mathf.Max(1f, speed);
                Vector3 next = Vector3.MoveTowards(shown, position, step);
                strolling = (position - next).sqrMagnitude > 0.0009f;
                residentSmoothed[resident.id] = next;
                position = next;
            }
            else residentSmoothed[resident.id] = position;
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
            bool carried = resident.id == dragResident && dragMoved;
            if (carried)
            {
                Vector3 pointer = view.ScreenToWorldPoint(new Vector3(dragScreen.x, dragScreen.y, 0));
                position = new Vector3(pointer.x, pointer.y - 0.25f, -1f);
                traveling = false;
            }
            bool asleep = resident.origin != "body" && resident.currentTask == "rest" &&
                resident.currentRoom == resident.targetRoom && !resident.downed;
            bool working = resident.currentTask == "production" || resident.currentTask == "repair" ||
                resident.currentTask == "haul";
            bool down = resident.downed || (resident.origin == "body" && resident.charge <= 0);
            TowerResident3D actor3D;
            TowerChibiAnimator animator;
            if (residents3D.TryGetValue(resident.id, out actor3D) && actor3D != null && actor3D.transform == visual)
            {
                actor3D.SetSleeping(asleep);
                actor3D.SetPose(position, traveling || strolling, working, down,
                    resident.currentTask == "haul" ? "barn" : fromRoom.type, WorldX(fromRoom.x + fromRoom.width * 0.5f));
            }
            else if (residentAnimators.TryGetValue(resident.id, out animator) && animator != null)
            {
                animator.SetSleeping(asleep);
                animator.SetPose(position, traveling || strolling, working, down);
            }
            else visual.localPosition = position + residentVisualOffsets[resident.id];
        }
    }
}
