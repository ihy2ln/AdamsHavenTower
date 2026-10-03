using System;
using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.SceneManagement;

// Presentation-only life for the cutaway: day and night, tap-to-collect bubbles, alerts,
// particles, floating results and resident mood icons. It only reads the simulation and
// listens to TowerRules.Signal; nothing here changes game state except a bubble tap.
[DefaultExecutionOrder(1100)]
public sealed class TowerFx : MonoBehaviour
{
    private const float Cell = 2.0f, Storey = 2.75f, Ground = -1.23f;
    private static float X(float cell) { return (cell - 17.5f) * Cell; }
    private static float Y(int floor) { return floor * Storey; }

    private sealed class Bubble
    {
        public GameObject go; public SpriteRenderer disc; public TextMesh letter; public float phase;
        public Vector3 basePosition;
    }
    private sealed class Alert
    {
        public GameObject go; public SpriteRenderer disc; public ParticleSystem particles; public string kind;
        public Transform fill; public TextMesh percent; public float maxHp, fillBase;
    }
    private sealed class Floater { public TextMesh text; public float age, life; public Vector3 start; }
    private sealed class Cloud { public Transform transform; public float speed; public SpriteRenderer renderer; }
    private sealed class MoodIcon { public GameObject go; public SpriteRenderer disc; public TextMesh letter; }

    private AdamsHavenPrototype tower;
    private TowerRules hooked;
    private Camera cam;
    private Transform root;
    private Sprite soft, ring, disc, white, cloudSprite;
    private Material particleMaterial;
    private Font worldFont;
    private SpriteRenderer overlay;
    private ParticleSystem motes, fireflies, sparkleBurst, dustBurst, smokeBurst;
    private readonly Dictionary<int, Bubble> bubbles = new Dictionary<int, Bubble>();
    private readonly Dictionary<int, Alert> alerts = new Dictionary<int, Alert>();
    private readonly Dictionary<int, SpriteRenderer> glows = new Dictionary<int, SpriteRenderer>();
    private readonly Dictionary<int, MoodIcon> moods = new Dictionary<int, MoodIcon>();
    private readonly List<Floater> floaters = new List<Floater>();
    private readonly List<Floater> spare = new List<Floater>();
    private readonly List<Cloud> clouds = new List<Cloud>();
    private readonly List<SpriteRenderer> ringPulses = new List<SpriteRenderer>();
    private readonly List<float> ringAges = new List<float>();
    private readonly List<float> ringTargets = new List<float>();
    private float refreshTimer, clock;
    private float nightFactor;

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
        var controller = UnityEngine.Object.FindAnyObjectByType<AdamsHavenPrototype>();
        if (controller != null && controller.GetComponent<TowerFx>() == null)
            controller.gameObject.AddComponent<TowerFx>();
    }

    private void Awake() { tower = GetComponent<AdamsHavenPrototype>(); }

    // ---- Procedural art -------------------------------------------------------------

    private static Sprite Make(string name, int width, int height, Func<float, float, Color> pixel)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = name };
        var colors = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                colors[y * width + x] = pixel((x + 0.5f) / width * 2 - 1, (y + 0.5f) / height * 2 - 1);
        texture.SetPixels(colors);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width);
    }

    private void BuildSprites()
    {
        soft = Make("fx_soft", 64, 64, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01(1 - r); return new Color(1, 1, 1, a * a);
        });
        ring = Make("fx_ring", 128, 128, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01(1 - Mathf.Abs(r - 0.86f) / 0.10f); return new Color(1, 1, 1, a * a);
        });
        disc = Make("fx_disc", 64, 64, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float edge = Mathf.Clamp01((0.96f - r) / 0.06f);
            float shade = r > 0.78f ? 0.55f : 1f;
            return new Color(shade, shade, shade, edge);
        });
        white = Make("fx_white", 4, 4, (x, y) => Color.white);
        cloudSprite = Make("fx_cloud", 128, 64, (x, y) =>
        {
            float a = 0;
            a = Mathf.Max(a, Blob(x, y, -0.45f, -0.10f, 0.42f, 0.42f));
            a = Mathf.Max(a, Blob(x, y, -0.10f, 0.12f, 0.46f, 0.55f));
            a = Mathf.Max(a, Blob(x, y, 0.28f, -0.02f, 0.44f, 0.46f));
            a = Mathf.Max(a, Blob(x, y, 0.60f, -0.16f, 0.32f, 0.34f));
            a = Mathf.Max(a, Blob(x, y, 0.05f, -0.30f, 0.90f, 0.30f));
            return new Color(1, 1, 1, a);
        });
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        particleMaterial = new Material(shader) { mainTexture = soft.texture };
        worldFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static float Blob(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        float a = Mathf.Clamp01(1 - r);
        return a * a * (3 - 2 * a) * 1.4f > 1 ? 1 : a * a * (3 - 2 * a) * 1.4f;
    }

    private SpriteRenderer Quad(string name, Sprite sprite, Vector3 position, Vector2 size, Color color,
        int order, Transform parent = null)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent == null ? root : parent, false);
        go.transform.localPosition = position;
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
        go.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
        return renderer;
    }

    private TextMesh Text(string value, Color color, float characterSize, Transform parent, int order = 720)
    {
        var go = new GameObject("fx text", typeof(TextMesh));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMesh>();
        text.font = worldFont; text.fontSize = 48; text.characterSize = characterSize;
        text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        text.color = color; text.text = value; text.fontStyle = FontStyle.Bold;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = worldFont.material; renderer.sortingOrder = order;
        return text;
    }

    private ParticleSystem MakeParticles(string name, Transform parent, Color color, float size, float life,
        float rate, Vector3 box, Vector2 velocityY, bool world, int max = 300)
    {
        var go = new GameObject(name, typeof(ParticleSystem));
        go.transform.SetParent(parent, false);
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size);
        main.startColor = color;
        main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.maxParticles = max;
        var emission = ps.emission; emission.rateOverTime = rate;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = box;
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(velocityY.x, velocityY.y);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0.8f, 0.7f),
                new GradientAlphaKey(0, 1) });
        var over = ps.colorOverLifetime; over.enabled = true; over.color = fade;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial; renderer.sortingOrder = 650;
        ps.Play();
        return ps;
    }

    private ParticleSystem MakeBurst(string name, Color color, float size, float life, float speed, float gravity)
    {
        var go = new GameObject(name, typeof(ParticleSystem));
        go.transform.SetParent(root, false);
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        var emission = ps.emission; emission.rateOverTime = 0;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.25f;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0, 1) });
        var over = ps.colorOverLifetime; over.enabled = true; over.color = fade;
        var shrink = ps.sizeOverLifetime; shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial; renderer.sortingOrder = 660;
        return ps;
    }

    private void Burst(ParticleSystem ps, Vector3 position, int count, Color? tint = null)
    {
        if (ps == null) return;
        ps.transform.position = position;
        var emit = new ParticleSystem.EmitParams();
        if (tint.HasValue) emit.startColor = tint.Value;
        ps.Emit(emit, count);
    }

    private void Build()
    {
        root = new GameObject("Tower FX").transform;
        BuildSprites();
        overlay = Quad("Day night tint", white, new Vector3(0, 0, 5), new Vector2(60, 40), Color.clear, 500);
        sparkleBurst = MakeBurst("Sparkle burst", Color.white, 0.20f, 1.1f, 2.2f, -0.08f);
        dustBurst = MakeBurst("Dust burst", new Color(0.82f, 0.74f, 0.6f, 0.9f), 0.32f, 1.2f, 1.5f, 0.05f);
        smokeBurst = MakeBurst("Smoke burst", new Color(0.35f, 0.33f, 0.36f, 0.85f), 0.45f, 1.6f, 0.9f, -0.12f);
        motes = MakeParticles("Celestium motes", root, new Color(0.6f, 0.95f, 1f, 0.9f), 0.12f, 4.5f, 10,
            new Vector3(1.1f, 4, 0.2f), new Vector2(0.35f, 0.8f), true, 500);
        fireflies = MakeParticles("Fireflies", root, new Color(0.85f, 1f, 0.45f, 1f), 0.16f, 5f, 0,
            new Vector3(62, 2.4f, 0.5f), new Vector2(-0.05f, 0.12f), true, 160);
        fireflies.transform.position = new Vector3(X(12), Ground + 1.1f, -2f);
        var noise = fireflies.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.3f;
        var random = new System.Random(77);
        for (int i = 0; i < 9; i++)
        {
            float y = Ground + 7 + (float)random.NextDouble() * (i < 4 ? 14 : 62);
            var renderer = Quad("Cloud " + i, cloudSprite,
                new Vector3(X((float)random.NextDouble() * 44 - 4), y, 15.4f),
                new Vector2(9 + (float)random.NextDouble() * 8, 3.6f + (float)random.NextDouble() * 2),
                new Color(1, 1, 1, 0.6f), -20);
            clouds.Add(new Cloud { transform = renderer.transform, renderer = renderer,
                speed = 0.08f + (float)random.NextDouble() * 0.16f });
        }
    }

    // ---- Simulation hookup ----------------------------------------------------------

    private void Hook(TowerRules rules)
    {
        if (hooked == rules) return;
        if (hooked != null && hooked.Signal == OnSignal) hooked.Signal = null;
        hooked = rules;
        if (rules != null) rules.Signal = OnSignal;
        ClearDynamic();
    }

    private void ClearDynamic()
    {
        foreach (var bubble in bubbles.Values) if (bubble.go != null) Destroy(bubble.go);
        foreach (var alert in alerts.Values) if (alert.go != null) Destroy(alert.go);
        foreach (var glow in glows.Values) if (glow != null) Destroy(glow.gameObject);
        foreach (var mood in moods.Values) if (mood.go != null) Destroy(mood.go);
        bubbles.Clear(); alerts.Clear(); glows.Clear(); moods.Clear();
    }

    private static Color ResourceColor(string produces)
    {
        switch (produces)
        {
            case "food": return new Color(0.98f, 0.62f, 0.25f);
            case "water": return new Color(0.35f, 0.72f, 1f);
            case "firewood": return new Color(0.70f, 0.46f, 0.27f);
            case "celestium": return new Color(0.55f, 0.96f, 1f);
            case "gold": return new Color(1f, 0.85f, 0.30f);
            case "tonics": return new Color(1f, 0.52f, 0.68f);
            default: return new Color(0.9f, 0.9f, 0.9f);
        }
    }

    private static string ResourceLetter(string produces)
    {
        switch (produces)
        {
            case "food": return "F"; case "water": return "W"; case "firewood": return "L";
            case "celestium": return "C"; case "gold": return "G"; case "tonics": return "T";
            default: return "+";
        }
    }

    private static Color IncidentColor(string kind)
    {
        switch (kind)
        {
            case "fire": return new Color(1f, 0.42f, 0.12f);
            case "pests": return new Color(0.62f, 0.86f, 0.25f);
            case "raiders": return new Color(0.92f, 0.20f, 0.20f);
            case "illness": return new Color(0.74f, 0.42f, 0.88f);
            default: return new Color(0.68f, 0.60f, 0.52f);
        }
    }

    private Vector3 RoomTop(TowerRoom room)
    { return new Vector3(X(room.x + room.width * 0.5f), Y(room.floor) + 0.75f, -2.5f); }

    private void OnSignal(string kind, int roomUid, int residentId, string text)
    {
        if (root == null || cam == null) return;
        var room = tower.Rules == null ? null : tower.Rules.Room(roomUid);
        Vector3 at = room != null ? RoomTop(room) :
            new Vector3(cam.transform.position.x, cam.transform.position.y + cam.orthographicSize * 0.55f, -2.5f);
        Vector3 residentAt;
        if (residentId > 0 && tower.TryResidentPosition(residentId, out residentAt))
            at = new Vector3(residentAt.x, residentAt.y + 0.9f, -2.5f);
        switch (kind)
        {
            case "collect":
            {
                string[] parts = text.Split(':');
                Color color = ResourceColor(parts[0]);
                Float("+" + (parts.Length > 1 ? parts[1] : "") + " " + parts[0], at, color);
                Burst(sparkleBurst, at, 14, color);
                Pulse(at, color, 1.6f);
                break;
            }
            case "build": Burst(dustBurst, at + Vector3.down * 0.5f, 22); Burst(sparkleBurst, at, 10,
                new Color(1, 0.9f, 0.55f)); Float("Built", at, new Color(1, 0.9f, 0.6f)); break;
            case "leaving": Float(text + " wants to leave", at, new Color(1f, 0.55f, 0.45f)); break;
            case "depart": Burst(smokeBurst, at, 10, new Color(0.6f, 0.6f, 0.7f, 0.8f)); Float(text + " left the Tower", at,
                new Color(0.8f, 0.8f, 0.9f)); break;
            case "inspired": Burst(sparkleBurst, at, 22, new Color(1f, 0.85f, 0.35f)); Float(text, at, new Color(1f, 0.88f, 0.45f)); break;
            case "outpost": Burst(sparkleBurst, at, 24, new Color(0.6f, 0.95f, 0.7f)); Float("Outpost founded", at,
                new Color(0.7f, 1f, 0.75f)); break;
            case "outpost_site": Float("Outpost site: " + text, at, new Color(0.75f, 0.95f, 1f)); break;
            case "caravan_home": Float("Caravan from " + text, at, new Color(1, 0.85f, 0.5f)); break;
            case "outpost_raid": Pulse(at, IncidentColor("raiders"), 2f); Float(text + " raided", at, new Color(1f, 0.45f, 0.4f)); break;
            case "auto_depart": Float("Party left for " + text, at, new Color(0.85f, 0.95f, 1f)); break;
            case "district": Float(text + " zoned", at, new Color(0.8f, 0.9f, 1f)); break;
            case "siege_warning": Float("A siege is gathering", at, new Color(1f, 0.45f, 0.35f)); break;
            case "siege_won": Burst(sparkleBurst, at, 30, new Color(1f, 0.85f, 0.4f)); Float("Siege broken!", at,
                new Color(1f, 0.9f, 0.5f)); break;
            case "siege_lost": Burst(smokeBurst, at, 24); Float("LAST STAND", at, new Color(1f, 0.35f, 0.3f)); break;
            case "move": Burst(dustBurst, at + Vector3.down * 0.5f, 16); Float("Moved", at, new Color(0.85f, 0.92f, 1f)); break;
            case "demolish": Burst(dustBurst, at, 26); Float("Demolished", at, new Color(0.9f, 0.75f, 0.6f)); break;
            case "upgrade": Burst(sparkleBurst, at, 26, new Color(1, 0.85f, 0.4f)); Pulse(at, new Color(1, 0.85f, 0.4f), 2.4f);
                Float("LEVEL " + text, at, new Color(1, 0.85f, 0.4f)); break;
            case "rush_ok": Burst(sparkleBurst, at, 30, new Color(1, 0.86f, 0.3f)); Pulse(at, new Color(1, 0.86f, 0.3f), 2.4f);
                Float("RUSH!", at, new Color(1, 0.86f, 0.3f)); break;
            case "rush_fail": Burst(smokeBurst, at, 18); Float("Rush failed", at, new Color(1, 0.4f, 0.3f)); break;
            case "incident": Burst(smokeBurst, at, 10); Pulse(at, IncidentColor(text), 3f);
                Float(text.Replace('_', ' ').ToUpperInvariant() + "!", at, IncidentColor(text)); break;
            case "resolved": Burst(sparkleBurst, at, 22, new Color(0.6f, 1, 0.65f)); Float("Resolved", at,
                new Color(0.6f, 1, 0.65f)); break;
            case "recruit": Burst(sparkleBurst, at, 14, new Color(0.7f, 0.9f, 1)); Float("Welcome, " + text, at,
                new Color(0.75f, 0.92f, 1)); break;
            case "child": Burst(sparkleBurst, at, 20, new Color(1, 0.7f, 0.85f)); Float("A child is born", at,
                new Color(1, 0.75f, 0.88f)); break;
            case "healed": Burst(sparkleBurst, at, 12, new Color(0.6f, 1, 0.75f)); Float("Healed", at,
                new Color(0.6f, 1, 0.75f)); break;
            case "trained": Burst(sparkleBurst, at, 10, new Color(1, 0.7f, 0.4f)); Float("+1 skill", at,
                new Color(1, 0.75f, 0.45f)); break;
            case "expedition": Float("Returned", at, new Color(0.85f, 0.95f, 1)); break;
            case "goal": Burst(sparkleBurst, at, 30, new Color(1, 0.92f, 0.5f)); Float("GOAL READY", at,
                new Color(1, 0.92f, 0.5f)); break;
            case "reward": Float(text, at, new Color(1, 0.9f, 0.5f)); break;
            case "break": Burst(smokeBurst, at, 12, new Color(0.7f, 0.4f, 0.9f, 0.8f));
                Float(string.IsNullOrEmpty(text) ? "MOOD BREAK" : text, at, new Color(0.82f, 0.55f, 1)); break;
            case "level_up": Burst(sparkleBurst, at, 24, new Color(0.55f, 0.95f, 1f));
                Pulse(at, new Color(0.55f, 0.95f, 1f), 1.8f);
                Float("LEVEL " + text, at, new Color(0.6f, 0.95f, 1f)); break;
            case "death": Burst(smokeBurst, at, 20, new Color(0.35f, 0.35f, 0.45f, 0.9f));
                Float("In memory of " + text, at, new Color(0.78f, 0.78f, 0.9f)); break;
            case "fight": Burst(dustBurst, at, 18); Pulse(at, new Color(1f, 0.45f, 0.3f), 1.5f);
                Float("FIGHT!", at, new Color(1f, 0.5f, 0.35f)); break;
            case "romance": Burst(sparkleBurst, at, 20, new Color(1f, 0.55f, 0.75f));
                Float("In love: " + text, at, new Color(1f, 0.65f, 0.82f)); break;
            case "raid_advance": Burst(smokeBurst, at, 14); Pulse(at, IncidentColor("raiders"), 3f);
                Float("RAIDERS PUSH IN", at, IncidentColor("raiders")); break;
            case "steward": Float("Steward: " + text, at, new Color(0.75f, 0.92f, 0.8f)); break;
            case "brink": Burst(sparkleBurst, at, 26, new Color(0.45f, 0.9f, 1f));
                Float("The Heart holds " + text, at, new Color(0.55f, 0.92f, 1f)); break;
            case "caravan": Float("Caravan +" + text, at, new Color(1, 0.85f, 0.5f));
                Burst(sparkleBurst, at, 18, new Color(1, 0.85f, 0.5f)); break;
            case "festival":
                Float("HARVEST FESTIVAL", at, new Color(1, 0.8f, 0.4f));
                for (int i = 0; i < 6; i++)
                    Burst(sparkleBurst, at + new Vector3((i - 2.5f) * 1.5f, -0.5f + (i % 2), 0), 12,
                        Color.HSVToRGB(i / 6f, 0.6f, 1f));
                break;
        }
    }

    private void Float(string value, Vector3 at, Color color)
    {
        Floater floater;
        if (spare.Count > 0) { floater = spare[spare.Count - 1]; spare.RemoveAt(spare.Count - 1); }
        else
        {
            floater = new Floater();
            floater.text = Text("", color, 0.05f, root, 730);
        }
        floater.text.gameObject.SetActive(true);
        floater.text.text = value; floater.text.color = color;
        floater.age = 0; floater.life = 1.8f;
        floater.start = at + new Vector3(UnityEngine.Random.Range(-0.25f, 0.25f), 0, 0);
        floater.text.transform.position = floater.start;
        floaters.Add(floater);
    }

    // Long-press feedback (AdamsHavenPrototype.TickRoomHold): a ring closes in on the room until it is picked up.
    private SpriteRenderer holdRing;

    public void ShowHold(TowerRoom room, float progress)
    {
        if (root == null || ring == null || room == null) return;
        if (holdRing == null) holdRing = Quad("Hold ring", ring, Vector3.zero, new Vector2(1, 1), Color.white, 650);
        holdRing.gameObject.SetActive(true);
        holdRing.transform.localPosition = RoomTop(room) + Vector3.down * 0.6f;
        float size = Mathf.Lerp(2.4f, 1.0f, Mathf.Clamp01(progress));
        holdRing.transform.localScale = new Vector3(size / ring.bounds.size.x, size / ring.bounds.size.y, 1);
        holdRing.color = new Color(1f, 0.86f, 0.5f, 0.3f + 0.65f * Mathf.Clamp01(progress));
    }

    public void HideHold() { if (holdRing != null) holdRing.gameObject.SetActive(false); }

    private void Pulse(Vector3 at, Color color, float size)
    {
        var renderer = Quad("Pulse ring", ring, at, new Vector2(0.3f, 0.3f), color, 640);
        renderer.transform.localScale = Vector3.one * 0.1f;
        ringPulses.Add(renderer); ringAges.Add(0); ringTargets.Add(size);
    }

    // ---- Frame update ---------------------------------------------------------------

    private static float NightFactor(float hour)
    {
        if (hour >= 21f || hour < 4.5f) return 1;
        if (hour >= 19f) return (hour - 19f) / 2f;
        if (hour < 6f) return 1 - (hour - 4.5f) / 1.5f;
        return 0;
    }

    private static Color SkyTint(float hour)
    {
        float night = NightFactor(hour);
        Color night_ = new Color(0.07f, 0.10f, 0.30f, 0.40f);
        Color dusk = new Color(1f, 0.52f, 0.28f, 0.17f);
        Color dawn = new Color(1f, 0.74f, 0.58f, 0.12f);
        if (hour >= 17f && hour < 19f) return Color.Lerp(Color.clear, dusk, (hour - 17f) / 2f);
        if (hour >= 19f && hour < 21f) return Color.Lerp(dusk, night_, (hour - 19f) / 2f);
        if (hour >= 6f && hour < 8f) return Color.Lerp(dawn, Color.clear, (hour - 6f) / 2f);
        if (hour >= 4.5f && hour < 6f) return Color.Lerp(night_, dawn, (hour - 4.5f) / 1.5f);
        return night > 0 ? night_ : Color.clear;
    }

    private void LateUpdate()
    {
        if (tower == null) return;
        cam = Camera.main;
        var rules = tower.Rules;
        bool active = rules != null && cam != null && !tower.InBattle;
        if (root == null && active) Build();
        if (root == null) return;
        if (root.gameObject.activeSelf != active) root.gameObject.SetActive(active);
        overlay.enabled = active;
        if (!active) return;
        Hook(rules);
        float dt = Time.deltaTime;
        clock += dt;
        float hour = rules.Hour();
        nightFactor = NightFactor(hour);
        if (rules.State.introPhase != "complete") { SetOverlay(Color.clear); }
        else SetOverlay(SkyTint(hour));
        UpdateClouds(dt, hour);
        UpdateAmbient(rules);
        refreshTimer -= dt;
        if (refreshTimer <= 0)
        {
            refreshTimer = 0.2f;
            RefreshBubbles(rules); RefreshAlerts(rules); RefreshGlows(rules); RefreshMoods(rules);
        }
        AnimateBubbles(); AnimateAlerts(); AnimateFloaters(dt); AnimatePulses(dt); PlaceMoods(rules);
    }

    private void SetOverlay(Color color)
    {
        overlay.color = color;
        if (overlay.transform.parent != cam.transform) overlay.transform.SetParent(cam.transform, false);
        overlay.transform.localPosition = new Vector3(0, 0, 5);
        overlay.transform.localRotation = Quaternion.identity;
        float height = cam.orthographicSize * 2.4f, width = height * cam.aspect * 1.2f;
        overlay.transform.localScale = new Vector3(width / overlay.sprite.bounds.size.x,
            height / overlay.sprite.bounds.size.y, 1);
    }

    private void UpdateClouds(float dt, float hour)
    {
        Color day = new Color(1, 1, 1, 0.65f);
        Color color = Color.Lerp(day, new Color(0.45f, 0.5f, 0.75f, 0.30f), nightFactor);
        if (hour >= 17f && hour < 21f && nightFactor < 1)
            color = Color.Lerp(color, new Color(1f, 0.66f, 0.5f, 0.6f), 0.5f * (1 - nightFactor));
        float left = X(-10), right = X(46);
        foreach (var cloud in clouds)
        {
            var p = cloud.transform.position;
            p.x += cloud.speed * dt;
            if (p.x > right) p.x = left;
            cloud.transform.position = p;
            cloud.renderer.color = color;
        }
    }

    private void UpdateAmbient(TowerRules rules)
    {
        bool complete = rules.State.introPhase == "complete";
        int low = 0, high = 0;
        foreach (var floor in rules.State.floors) { low = Mathf.Min(low, floor.number); high = Mathf.Max(high, floor.number); }
        var emission = motes.emission;
        emission.rateOverTime = complete ? Mathf.Clamp((high - low + 1) * 3, 6, 140) : 0;
        var shape = motes.shape;
        shape.scale = new Vector3(1.1f, Mathf.Max(3, (high - low + 1) * Storey), 0.2f);
        motes.transform.position = new Vector3(X(22.5f), (high + low) * 0.5f * Storey, -1.5f);
        var fly = fireflies.emission;
        fly.rateOverTime = nightFactor * 16f;
    }

    private bool InView(int floor, float margin = 2f)
    { return Mathf.Abs(Y(floor) - cam.transform.position.y) <= cam.orthographicSize + margin; }

    private void RefreshBubbles(TowerRules rules)
    {
        var wanted = new HashSet<int>();
        foreach (var room in rules.State.rooms)
        {
            if (!room.ready || !InView(room.floor)) continue;
            var def = TowerCatalog.Get(room.type);
            if (def == null || string.IsNullOrEmpty(def.produces)) continue;
            wanted.Add(room.uid);
            Bubble bubble;
            if (!bubbles.TryGetValue(room.uid, out bubble) || bubble.go == null)
            {
                bubble = new Bubble { phase = room.uid * 0.7f };
                bubble.go = new GameObject("Collect bubble " + room.uid);
                bubble.go.transform.SetParent(root, false);
                bubble.disc = Quad("disc", disc, Vector3.zero, new Vector2(0.62f, 0.62f),
                    ResourceColor(def.produces), 700, bubble.go.transform);
                Quad("shine", soft, new Vector3(-0.09f, 0.10f, -0.01f), new Vector2(0.24f, 0.18f),
                    new Color(1, 1, 1, 0.55f), 705, bubble.go.transform);
                bubble.letter = Text(ResourceLetter(def.produces), new Color(0.1f, 0.08f, 0.06f), 0.05f,
                    bubble.go.transform, 710);
                bubble.letter.transform.localPosition = new Vector3(0, 0, -0.02f);
                bubble.basePosition = new Vector3(X(room.x + room.width * 0.5f), Y(room.floor) + 1.02f, -2.6f);
                bubbles[room.uid] = bubble;
                bubble.go.transform.localScale = Vector3.zero;
            }
        }
        var remove = new List<int>();
        foreach (var pair in bubbles) if (!wanted.Contains(pair.Key)) remove.Add(pair.Key);
        foreach (int uid in remove)
        {
            if (bubbles[uid].go != null)
            {
                var room = rules.Room(uid);
                if (room != null) { }
                Destroy(bubbles[uid].go);
            }
            bubbles.Remove(uid);
        }
    }

    private void AnimateBubbles()
    {
        foreach (var bubble in bubbles.Values)
        {
            if (bubble.go == null) continue;
            float bob = Mathf.Sin(clock * 3f + bubble.phase) * 0.06f;
            bubble.go.transform.position = bubble.basePosition + new Vector3(0, bob, 0);
            float target = 1f + 0.06f * Mathf.Sin(clock * 5f + bubble.phase);
            float scale = Mathf.MoveTowards(bubble.go.transform.localScale.x, target, Time.deltaTime * 5f);
            bubble.go.transform.localScale = Vector3.one * scale;
        }
    }

    // Returns true when a ready-bubble consumed the tap so the room selection is not also triggered.
    public bool TryTap(Vector3 world)
    {
        if (tower == null || tower.Rules == null) return false;
        int hit = 0;
        float best = 0.55f;
        foreach (var pair in bubbles)
        {
            if (pair.Value.go == null) continue;
            var p = pair.Value.go.transform.position;
            float distance = Vector2.Distance(new Vector2(p.x, p.y), new Vector2(world.x, world.y));
            if (distance < best) { best = distance; hit = pair.Key; }
        }
        if (hit == 0) return false;
        tower.SelectRoomById(hit);
        tower.Apply(tower.Rules.Collect(hit));
        return true;
    }

    private void RefreshAlerts(TowerRules rules)
    {
        var wanted = new HashSet<int>();
        foreach (var incident in rules.State.incidents)
        {
            var room = rules.Room(incident.roomUid);
            if (room == null) continue;
            wanted.Add(incident.roomUid);
            Alert alert;
            if (alerts.TryGetValue(incident.roomUid, out alert) && alert.go != null && alert.kind == incident.kind)
                continue;
            if (alert != null && alert.go != null) Destroy(alert.go);
            Color color = IncidentColor(incident.kind);
            alert = new Alert { kind = incident.kind };
            alert.go = new GameObject("Alert " + incident.kind + " " + room.uid);
            alert.go.transform.SetParent(root, false);
            alert.go.transform.position = new Vector3(X(room.x + 0.5f), Y(room.floor) + 0.95f, -2.7f);
            alert.disc = Quad("disc", disc, Vector3.zero, new Vector2(0.5f, 0.5f), color, 700, alert.go.transform);
            alert.maxHp = Mathf.Max(1f, incident.hp);
            Quad("bar back", white, new Vector3(0, -0.5f, 0), new Vector2(BarWidth + 0.08f, 0.2f),
                new Color(0.05f, 0.04f, 0.04f, 0.9f), 705, alert.go.transform);
            var fillRenderer = Quad("bar fill", white, new Vector3(-BarWidth * 0.5f, -0.5f, -0.01f),
                new Vector2(BarWidth, 0.14f), new Color(0.45f, 0.9f, 0.4f), 706, alert.go.transform);
            alert.fill = fillRenderer.transform; alert.fillBase = alert.fill.localScale.x;
            alert.percent = Text("CLEARING 0%", Color.white, 0.045f, alert.go.transform, 710);
            alert.percent.transform.localPosition = new Vector3(0, -0.34f, -0.02f);
            var mark = Text("!", Color.white, 0.055f, alert.go.transform, 710);
            mark.transform.localPosition = new Vector3(0, 0, -0.02f);
            float width = room.width * Cell;
            alert.particles = MakeParticles("Hazard particles", alert.go.transform,
                new Color(color.r, color.g, color.b, 0.95f),
                incident.kind == "fire" ? 0.20f : 0.14f, incident.kind == "fire" ? 1.3f : 2.2f,
                incident.kind == "fire" ? 26 : 12, new Vector3(width * 0.8f, 0.2f, 0.1f),
                incident.kind == "cave_in" ? new Vector2(-0.7f, -0.3f) :
                incident.kind == "fire" ? new Vector2(0.5f, 1.1f) : new Vector2(0.05f, 0.3f), true, 120);
            alert.particles.transform.position = new Vector3(X(room.x + room.width * 0.5f),
                Y(room.floor) + (incident.kind == "cave_in" ? 0.9f : -0.6f), -2.2f);
            alerts[incident.roomUid] = alert;
        }
        var remove = new List<int>();
        foreach (var pair in alerts) if (!wanted.Contains(pair.Key)) remove.Add(pair.Key);
        foreach (int uid in remove) { if (alerts[uid].go != null) Destroy(alerts[uid].go); alerts.Remove(uid); }
    }

    private const float BarWidth = 1.3f;

    private void AnimateAlerts()
    {
        foreach (var pair in alerts)
        {
            var alert = pair.Value;
            if (alert.go == null) continue;
            var incident = tower != null && tower.Rules != null
                ? tower.Rules.State.incidents.Find(i => i.roomUid == pair.Key) : null;
            if (incident != null && alert.fill != null)
            {
                alert.maxHp = Mathf.Max(alert.maxHp, incident.hp);
                float done = Mathf.Clamp01(1f - incident.hp / alert.maxHp);
                alert.fill.localScale = new Vector3(alert.fillBase * Mathf.Max(0.0001f, done),
                    alert.fill.localScale.y, 1);
                alert.fill.localPosition = new Vector3(-BarWidth * 0.5f + BarWidth * done * 0.5f,
                    alert.fill.localPosition.y, alert.fill.localPosition.z);
                alert.fill.GetComponent<SpriteRenderer>().color = done > 0.02f
                    ? Color.Lerp(new Color(1f, 0.75f, 0.25f), new Color(0.45f, 0.9f, 0.4f), done)
                    : new Color(1f, 0.75f, 0.25f, 0f);
                alert.percent.text = "CLEARING " + Mathf.RoundToInt(done * 100) + "%";
            }
            float pulse = 1f + 0.18f * Mathf.Sin(clock * 7f);
            alert.disc.transform.localScale = new Vector3(0.5f / alert.disc.sprite.bounds.size.x * pulse,
                0.5f / alert.disc.sprite.bounds.size.y * pulse, 1);
        }
    }

    private void RefreshGlows(TowerRules rules)
    {
        var wanted = new HashSet<int>();
        float strength = rules.State.introPhase == "complete" ? nightFactor : 0;
        foreach (var room in rules.State.rooms)
        {
            if (!InView(room.floor, 1f)) continue;
            bool heart = room.type == "heart";
            if (!heart && strength <= 0.02f) continue;
            bool lit = heart;
            if (!lit)
                foreach (var resident in rules.State.residents)
                    if (resident.currentRoom == room.uid && resident.ageStage <= 1 &&
                        !(resident.currentTask == "rest" && rules.Sleeping(resident))) { lit = true; break; }
            if (!lit) continue;
            wanted.Add(room.uid);
            SpriteRenderer glow;
            if (!glows.TryGetValue(room.uid, out glow) || glow == null)
            {
                glow = Quad("Glow " + room.uid, soft,
                    new Vector3(X(room.x + room.width * 0.5f), Y(room.floor) + 0.05f, -1.8f),
                    new Vector2(room.width * Cell * 1.05f, 2.0f), Color.clear, 600);
                glows[room.uid] = glow;
            }
            Color color = heart ? new Color(0.5f, 0.9f, 1f) : new Color(1f, 0.78f, 0.42f);
            float alpha = heart ? 0.16f + 0.24f * strength : 0.42f * strength;
            glow.color = new Color(color.r, color.g, color.b, alpha *
                (0.92f + 0.08f * Mathf.Sin(clock * 3f + room.uid)));
        }
        var remove = new List<int>();
        foreach (var pair in glows) if (!wanted.Contains(pair.Key)) remove.Add(pair.Key);
        foreach (int uid in remove) { if (glows[uid] != null) Destroy(glows[uid].gameObject); glows.Remove(uid); }
    }

    private static string MoodGlyph(TowerRules rules, TowerResident resident, out Color color)
    {
        color = Color.white;
        if (resident.downed) { color = new Color(1, 0.3f, 0.3f); return "+"; }
        if (resident.breakSeconds > 0) { color = new Color(0.8f, 0.5f, 1f); return "!"; }
        if (resident.currentTask == "rest" && resident.currentRoom == resident.targetRoom &&
            rules.Sleeping(resident)) { color = new Color(0.7f, 0.82f, 1f); return "z"; }
        if (resident.origin == "body")
        {
            if (resident.charge < 300) { color = new Color(1, 0.85f, 0.3f); return "C"; }
            return null;
        }
        if (resident.hunger < 25) { color = new Color(1, 0.6f, 0.25f); return "F"; }
        if (resident.thirst < 25) { color = new Color(0.4f, 0.75f, 1f); return "W"; }
        if (resident.injury > 30 || resident.illness > 30) { color = new Color(1, 0.5f, 0.6f); return "+"; }
        if (resident.happiness > 88) { color = new Color(1, 0.75f, 0.85f); return "*"; }
        return null;
    }

    private void RefreshMoods(TowerRules rules)
    {
        var wanted = new HashSet<int>();
        int spawned = 0;
        foreach (var resident in rules.State.residents)
        {
            Color color;
            string glyph = MoodGlyph(rules, resident, out color);
            if (glyph == null) continue;
            var room = rules.Room(resident.currentRoom) ?? rules.Room(resident.homeRoom);
            if (room == null || !InView(room.floor) || spawned++ > 60) continue;
            wanted.Add(resident.id);
            MoodIcon icon;
            if (!moods.TryGetValue(resident.id, out icon) || icon.go == null)
            {
                icon = new MoodIcon { go = new GameObject("Mood " + resident.id) };
                icon.go.transform.SetParent(root, false);
                icon.disc = Quad("disc", disc, Vector3.zero, new Vector2(0.34f, 0.34f), Color.white, 690,
                    icon.go.transform);
                icon.letter = Text("", Color.white, 0.034f, icon.go.transform, 700);
                icon.letter.transform.localPosition = new Vector3(0, 0, -0.02f);
                moods[resident.id] = icon;
            }
            icon.disc.color = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.92f);
            icon.letter.text = glyph; icon.letter.color = color;
        }
        var remove = new List<int>();
        foreach (var pair in moods) if (!wanted.Contains(pair.Key)) remove.Add(pair.Key);
        foreach (int id in remove) { if (moods[id].go != null) Destroy(moods[id].go); moods.Remove(id); }
    }

    private void PlaceMoods(TowerRules rules)
    {
        foreach (var pair in moods)
        {
            Vector3 position;
            if (pair.Value.go == null || !tower.TryResidentPosition(pair.Key, out position)) continue;
            float bob = Mathf.Sin(clock * 2.5f + pair.Key) * 0.05f;
            pair.Value.go.transform.position = new Vector3(position.x, position.y + 0.98f + bob, -2.4f);
        }
    }

    private void AnimateFloaters(float dt)
    {
        for (int i = floaters.Count - 1; i >= 0; i--)
        {
            var floater = floaters[i];
            floater.age += dt;
            float t = floater.age / floater.life;
            if (t >= 1)
            {
                floater.text.gameObject.SetActive(false);
                spare.Add(floater); floaters.RemoveAt(i);
                continue;
            }
            floater.text.transform.position = floater.start + new Vector3(0, 0.35f + 1.2f * (1 - (1 - t) * (1 - t)), 0);
            var color = floater.text.color;
            color.a = t < 0.7f ? 1 : 1 - (t - 0.7f) / 0.3f;
            floater.text.color = color;
            float pop = t < 0.12f ? 0.6f + t / 0.12f * 0.5f : 1.1f - Mathf.Min(0.1f, (t - 0.12f));
            floater.text.transform.localScale = Vector3.one * pop;
        }
    }

    private void AnimatePulses(float dt)
    {
        for (int i = ringPulses.Count - 1; i >= 0; i--)
        {
            ringAges[i] += dt;
            float t = ringAges[i] / 0.9f;
            if (ringPulses[i] == null || t >= 1)
            {
                if (ringPulses[i] != null) Destroy(ringPulses[i].gameObject);
                ringPulses.RemoveAt(i); ringAges.RemoveAt(i); ringTargets.RemoveAt(i);
                continue;
            }
            float scale = Mathf.Lerp(0.15f, ringTargets[i], 1 - (1 - t) * (1 - t)) / ringPulses[i].sprite.bounds.size.x;
            ringPulses[i].transform.localScale = new Vector3(scale, scale, 1);
            var color = ringPulses[i].color; color.a = 1 - t; ringPulses[i].color = color;
        }
    }

    private void OnDestroy()
    {
        if (hooked != null && hooked.Signal == OnSignal) hooked.Signal = null;
        if (root != null) Destroy(root.gameObject);
        foreach (var sprite in new[] { soft, ring, disc, white, cloudSprite })
            if (sprite != null) { Destroy(sprite.texture); Destroy(sprite); }
        if (particleMaterial != null) Destroy(particleMaterial);
    }
}
