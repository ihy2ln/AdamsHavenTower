using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.SceneManagement;

// Dungeon Mode presentation over the cutaway (GDD 20.7): adventurer parties as tokens walking the route, monster counts
// on the lairs. Placeholder art until the dungeon gets its own; attached like TowerArtDirector, shown whenever a dungeon
// exists (Dungeon Mode, or the one-world dungeon under a Tower).
public sealed class LairView : MonoBehaviour
{
    private const float Cell = 2.0f, Storey = 2.75f;
    private AdamsHavenPrototype tower;
    private Transform root;
    private Material material;
    private Font font;
    private readonly Dictionary<int, Transform> tokens = new Dictionary<int, Transform>();
    private readonly List<GameObject> badges = new List<GameObject>();
    private float badgeTimer;

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
        if (controller != null && controller.GetComponent<LairView>() == null) controller.gameObject.AddComponent<LairView>();
    }

    private void Awake()
    {
        tower = GetComponent<AdamsHavenPrototype>();
        root = new GameObject("Dungeon view").transform;
        material = new Material(Shader.Find("Sprites/Default"));
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnDestroy() { if (root != null) Destroy(root.gameObject); }

    private static float X(float cell) { return (cell - 17.5f) * Cell; }

    private void LateUpdate()
    {
        var rules = tower == null ? null : tower.Rules;
        bool show = rules != null && rules.LairFounded && !tower.InBattle && !tower.ExpeditionOpen;
        root.gameObject.SetActive(show);
        if (!show) return;
        var parties = rules.State.lair.parties;
        var seen = new HashSet<int>();
        foreach (var party in parties)
        {
            seen.Add(party.id);
            Transform token;
            if (!tokens.TryGetValue(party.id, out token)) { token = MakeToken(party); tokens[party.id] = token; }
            float t = party.moveSeconds <= 0 ? 1 : Mathf.Clamp01((party.stepTotal - party.stepSeconds) / party.moveSeconds);
            var from = new Vector3(X(party.fromX + 0.5f), party.fromFloor * Storey - 0.45f, -1.6f);
            var to = new Vector3(X(party.x + 0.5f), party.floor * Storey - 0.45f, -1.6f);
            token.localPosition = Vector3.Lerp(from, to, t);
            token.GetComponentInChildren<TextMesh>().text = (party.kind == "elite" ? "★" : "⚔") + rules.Alive(party);
        }
        foreach (var id in new List<int>(tokens.Keys))
            if (!seen.Contains(id)) { Destroy(tokens[id].gameObject); tokens.Remove(id); }
        badgeTimer -= Time.unscaledDeltaTime;
        if (badgeTimer <= 0) { badgeTimer = 1f; RebuildBadges(rules); }
    }

    private Transform MakeToken(LairParty party)
    {
        var token = new GameObject("Party " + party.id).transform;
        token.SetParent(root, false);
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(token, false);
        quad.transform.localScale = new Vector3(1.1f, 0.7f, 1);
        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        var block = new MaterialPropertyBlock();
        block.SetColor("_Color", party.kind == "elite" ? new Color(0.9f, 0.25f, 0.2f, 0.92f) : new Color(0.85f, 0.65f, 0.25f, 0.92f));
        renderer.SetPropertyBlock(block);
        Text(token, "", new Vector3(0, 0, -0.05f), Color.white);
        return token;
    }

    private TextMesh Text(Transform parent, string value, Vector3 position, Color color)
    {
        var go = new GameObject("Label", typeof(TextMesh));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var text = go.GetComponent<TextMesh>();
        text.font = font; text.fontSize = 48; text.characterSize = 0.045f;
        text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        text.color = color; text.text = value;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        return text;
    }

    private void RebuildBadges(TowerRules rules)
    {
        foreach (var badge in badges) Destroy(badge);
        badges.Clear();
        foreach (var room in rules.State.rooms)
        {
            if (room.type != "monster_lair") continue;
            int count = rules.MonstersIn(room.uid).Count;
            var label = Text(root, "MONSTERS " + count + "/" + rules.LairCapacity(room),
                new Vector3(X(room.x + room.width * 0.5f), room.floor * Storey + 0.85f, -1.4f), new Color(1f, 0.6f, 0.55f));
            label.characterSize = 0.03f;
            badges.Add(label.gameObject);
        }
        // Where each floor's way down (or up) to the next floor is.
        foreach (var floor in rules.State.floors)
        {
            if (floor.number == rules.VaultFloor || !rules.ShaftSealed(floor.number) && !rules.ShaftSealed(floor.number + rules.LairDir)) continue;
            if (rules.Merged && floor.number >= 0) continue;
            int x = rules.StairX(floor.number);
            if (x == TowerRules.CoreX) continue;
            var label = Text(root, rules.LairDir > 0 ? "▲ STAIRS" : "▼ STAIRS",
                new Vector3(X(x + 0.5f), floor.number * Storey - 0.95f, -1.4f), new Color(0.75f, 0.9f, 1f));
            label.characterSize = 0.03f;
            badges.Add(label.gameObject);
        }
        if (rules.Merged)
        {
            // Where prey come in from the town: the top of B1's east end.
            var gate = Text(root, "▼ DUNGEON GATE", new Vector3(X(rules.DungeonGateX + 0.5f), rules.LairDir * Storey + 1.05f, -1.4f),
                new Color(1f, 0.6f, 0.55f));
            gate.characterSize = 0.03f;
            badges.Add(gate.gameObject);
        }
        int guards = rules.HeartGuards().Count;
        if (guards > 0)
        {
            var label = Text(root, "GUARDS " + guards, new Vector3(X(TowerRules.CoreX + 0.5f), rules.VaultFloor * Storey + 0.85f, -1.4f),
                new Color(1f, 0.6f, 0.55f));
            label.characterSize = 0.03f;
            badges.Add(label.gameObject);
        }
    }
}
