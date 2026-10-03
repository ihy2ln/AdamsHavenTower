using System;
using System.Collections.Generic;
using UnityEngine;

// The Adams Haven bestiary (Resources/AdamsHaven/Bestiary/bestiary.json, built by Tools/build_bestiary.py; design
// notes in BESTIARY.md). Monsters are GKOM variants in families; each family spans a rank range F (1) .. SSR (9)
// and its forms split that range, so a monster evolves into the next form when its rank reaches that form's minimum.
[Serializable] public sealed class BestiaryCard
{
    public string id, name, target, status;
    public int ep, duration;
    public float power, magnitude, heal;
    public bool magic;
}

[Serializable] public sealed class BestiaryForm
{
    public string id, name, flavor, evolvesTo, art, element, role, core, crystal;
    public int minRank, maxRank, stage;
    public bool large, ownArt;
    public BestiaryCard[] cards;
    public string[] drops;
    [NonSerialized] public BestiaryFamily Family;
}

[Serializable] public sealed class BestiaryFamily
{
    public string id, name, inspiration, element, role, lore, material;
    public bool large;
    public int minRank, maxRank;
    public string[] themes;
    public BestiaryForm[] forms;
}

[Serializable] public sealed class BestiaryBoss { public string region, family, title, theme; }

[Serializable] internal sealed class BestiaryFile
{
    public int version;
    public BestiaryFamily[] families;
    public BestiaryBoss[] bosses;
}

public static class BattleBestiary
{
    public static readonly string[] RankNames = { "F", "E", "D", "C", "B", "A", "S", "SS", "SSR" };
    public const int MaxRank = 9;
    // Share of variants at each rank (Weaververse codex, GKOM Variant Rank System): the spawn weights.
    private static readonly float[] Population = { 40f, 28f, 15f, 9f, 4f, 2f, 1f, .5f, .1f };

    private static List<BestiaryFamily> families;
    private static readonly Dictionary<string, BestiaryForm> forms = new Dictionary<string, BestiaryForm>();
    private static readonly Dictionary<string, BestiaryBoss> bosses = new Dictionary<string, BestiaryBoss>();

    // Off = the pre-bestiary 12-species roster in BattleCatalog (a kill switch, and a balance baseline for tests).
    public static bool Enabled = true;
    public static bool Available { get { Load(); return Enabled && families.Count > 0; } }
    public static IList<BestiaryFamily> Families { get { Load(); return families; } }

    public static string RankName(int rank) { return RankNames[Mathf.Clamp(rank, 1, MaxRank) - 1]; }

    // Drops the cached data so the next lookup re-reads bestiary.json (after Tools/build_bestiary.py in the Editor).
    public static void Reload() { families = null; forms.Clear(); bosses.Clear(); }

    private static void Load()
    {
        if (families != null) return;
        families = new List<BestiaryFamily>();
        var text = Resources.Load<TextAsset>("AdamsHaven/Bestiary/bestiary");
        if (text == null) return;
        BestiaryFile file;
        try { file = JsonUtility.FromJson<BestiaryFile>(text.text); }
        catch (Exception ex) { Debug.LogError("Bestiary failed to load: " + ex.Message); return; }
        if (file == null || file.families == null) return;
        foreach (var family in file.families)
        {
            if (family.forms == null || family.forms.Length == 0) continue;
            families.Add(family);
            foreach (var form in family.forms) { form.Family = family; forms[form.id] = form; }
        }
        if (file.bosses != null) foreach (var b in file.bosses) bosses[b.region] = b;
    }

    public static BestiaryForm Form(string id)
    {
        Load();
        BestiaryForm form;
        return id != null && forms.TryGetValue(id, out form) ? form : null;
    }

    public static BestiaryFamily Family(string id)
    {
        foreach (var f in Families) if (f.id == id) return f;
        return null;
    }

    public static BestiaryBoss Boss(string region)
    {
        Load();
        BestiaryBoss boss;
        return region != null && bosses.TryGetValue(region, out boss) ? boss : null;
    }

    // The form a family takes at a rank (clamped into the family's range): evolution by rank threshold.
    public static BestiaryForm FormFor(BestiaryFamily family, int rank)
    {
        rank = Mathf.Clamp(rank, family.minRank, family.maxRank);
        foreach (var form in family.forms) if (rank >= form.minRank && rank <= form.maxRank) return form;
        return family.forms[family.forms.Length - 1];
    }

    // An expedition depth's natural rank: 1-2 F, 3-4 E, 5-6 D, 7-8 C, 9-10 B, 11 A, 12 S, 13+ SS.
    public static int RankForDepth(int depth)
    {
        depth = Math.Max(1, depth);
        return depth <= 10 ? (depth + 1) / 2 : Math.Min(8, depth - 5);
    }

    // The shallowest depth whose natural rank is this one (SSR, never natural, sits at the deepest depth).
    public static int DepthForRank(int rank)
    {
        rank = Mathf.Clamp(rank, 1, MaxRank);
        return rank <= 5 ? rank * 2 - 1 : Math.Min(14, rank + 5);
    }

    // A pack's rank: one either side of the natural rank, weighted by how common each rank is (the natural rank
    // counts double). SSR never rolls for packs; it is reserved for the deepest lair bosses.
    public static int RollRank(int depth, System.Random rng)
    {
        int center = RankForDepth(depth);
        int lo = Math.Max(1, center - 1), hi = Math.Min(8, center + 1);
        float total = 0;
        for (int r = lo; r <= hi; r++) total += Population[r - 1] * (r == center ? 2f : 1f);
        float pick = (float)rng.NextDouble() * total;
        for (int r = lo; r <= hi; r++)
        {
            pick -= Population[r - 1] * (r == center ? 2f : 1f);
            if (pick <= 0) return r;
        }
        return center;
    }

    // Forms living in a theme at a rank. "edge" stands in for an unknown theme; when a theme has nothing at that
    // rank the pool widens to any theme, so every depth always has monsters.
    public static List<BestiaryForm> Spawnable(int rank, string theme, bool smallOnly = false)
    {
        theme = string.IsNullOrEmpty(theme) ? "edge" : theme;
        var list = new List<BestiaryForm>();
        Collect(list, rank, theme, smallOnly);
        if (list.Count < 3) Collect(list, rank, "edge", smallOnly);
        if (list.Count == 0) Collect(list, rank, null, smallOnly);
        return list;
    }

    private static void Collect(List<BestiaryForm> list, int rank, string theme, bool smallOnly)
    {
        foreach (var f in Families)
        {
            if (theme != null && Array.IndexOf(f.themes, theme) < 0) continue;
            foreach (var form in f.forms)
                if (rank >= form.minRank && rank <= form.maxRank && !(smallOnly && form.large) && !list.Contains(form))
                    list.Add(form);
        }
    }

    // "Goblin Scavenger (F-E) > Hobgoblin (D-C) > ..." for the journal.
    public static string EvolutionLine(BestiaryFamily family)
    {
        var parts = new List<string>();
        foreach (var form in family.forms) parts.Add(form.name + " (" + Span(form.minRank, form.maxRank) + ")");
        return string.Join("  >  ", parts);
    }

    public static string Span(int lo, int hi) { return lo == hi ? RankName(lo) : RankName(lo) + "-" + RankName(hi); }
}
