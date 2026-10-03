using System;
using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;

// The cards behind the BESTIARY + CODEX screen (CodexPanel): one per bestiary form, party fighter, roster character and
// lair boss, with what each side of the card shows. Monster card art lives in Resources/AdamsHaven/Bestiary/Cards/,
// copied from MonsterPrompts by Tools/sync_codex_cards.py; a form whose art is not painted yet uses its battle cutout,
// and its card fills in the next time the codex opens after the art lands.
public enum CodexKind { Monster, Fighter, Roster, Boss }

// One row on a card back: a move, ability or passive.
public sealed class CodexMove
{
    public string Name, Tag, Text;
    public int Cost = -1;                // -1: no cost shown
    public string CostUnit = "EP";
}

public struct CodexStat { public string Label, Value; public float Fill; }

public sealed class CodexEntry
{
    public CodexKind Kind;
    public string Id, Name, Subtitle, Element, Role;
    public int Rank;                     // the rank gem (monsters: the form's lowest rank)
    public BestiaryForm Form;            // monsters, and the form a lair boss takes
    public BestiaryBoss Lair;
    public TowerRegionDef Region;
    public RosterUnit Unit;
    public BattleUnit Fighter;           // the party, JD, and the lair boss as it fights
}

// What the guild has seen. Monsters stay silhouetted until met in battle; a lair boss shows once its land is walked.
public sealed class CodexContext
{
    public ICollection<string> Beasts, Regions, Conquered, Recruited;
    // Sandbox battles have no journal: nothing is hidden.
    public static readonly CodexContext Everything = new CodexContext();

    public static CodexContext From(TowerRules rules)
    {
        if (rules == null || rules.State == null) return Everything;
        var journal = rules.Journal;
        var recruited = new HashSet<string>();
        if (rules.State.residents != null)
            foreach (var r in rules.State.residents) if (r != null && !string.IsNullOrEmpty(r.unitId)) recruited.Add(r.unitId);
        return new CodexContext { Beasts = journal.beasts, Regions = journal.regions, Conquered = rules.State.regionsConquered, Recruited = recruited };
    }

    public bool Knows(CodexEntry e)
    {
        if (e.Kind == CodexKind.Monster) return Beasts == null || Beasts.Contains(e.Id);
        if (e.Kind == CodexKind.Boss) return Regions == null || Regions.Contains(e.Lair.region) || Defeated(e);
        return true;
    }

    public bool Defeated(CodexEntry e) { return e.Kind == CodexKind.Boss && Conquered != null && Conquered.Contains(e.Lair.region); }
    public bool Recruits(CodexEntry e) { return e.Kind == CodexKind.Roster && Recruited != null && Recruited.Contains(e.Id); }
}

public static class CodexCards
{
    public const string CardRoot = "AdamsHaven/Bestiary/Cards/";
    // Where the move panels sit on a painted move-list card when detection found none (normalised, from the top-left).
    public static readonly Rect[] DefaultPanels =
    {
        new Rect(.20f, .436f, .69f, .082f), new Rect(.20f, .561f, .69f, .083f),
        new Rect(.20f, .686f, .69f, .085f), new Rect(.20f, .811f, .69f, .086f),
    };

    // ---- entries -------------------------------------------------------------------------------------------

    public static List<CodexEntry> Monsters()
    {
        var list = new List<CodexEntry>();
        if (!BattleBestiary.Available) return list;
        foreach (var family in BattleBestiary.Families)
            foreach (var form in family.forms)
                list.Add(new CodexEntry { Kind = CodexKind.Monster, Id = form.id, Name = form.name, Form = form, Rank = form.minRank,
                    Element = form.element, Role = form.role, Subtitle = family.name + "  ·  " + form.role });
        return list;
    }

    public static List<CodexEntry> Fighters()
    {
        var list = new List<CodexEntry>();
        foreach (var u in BattleCatalog.Party())
            list.Add(new CodexEntry { Kind = CodexKind.Fighter, Id = u.Id, Name = u.Name, Fighter = u, Element = u.Element.ToString(),
                Role = RoleName(u.Role), Subtitle = "Fighter  ·  " + RoleName(u.Role), Rank = 0 });
        var jd = BattleCatalog.JD();
        list.Add(new CodexEntry { Kind = CodexKind.Fighter, Id = jd.Id, Name = jd.Name, Fighter = jd, Element = jd.Element.ToString(),
            Role = "Summoner", Subtitle = "The Summoner", Rank = 0 });
        return list;
    }

    // Heroes first, then residents; each by rank, then name.
    public static List<CodexEntry> Roster()
    {
        var list = new List<CodexEntry>();
        if (!TowerRoster.Available) return list;
        var units = new List<RosterUnit>(TowerRoster.All);
        units.Sort((a, b) => a.IsHero != b.IsHero ? (a.IsHero ? -1 : 1) : a.rank != b.rank ? a.rank.CompareTo(b.rank) : string.CompareOrdinal(a.name, b.name));
        foreach (var u in units)
            list.Add(new CodexEntry { Kind = CodexKind.Roster, Id = u.id, Name = u.name, Unit = u, Rank = u.rank, Element = u.element ?? "",
                Role = u.IsHero ? u.role : u.room, Subtitle = u.IsHero ? u.cls + "  ·  " + u.role : "Resident  ·  " + u.room });
        return list;
    }

    // One per region with a lair, in the order the regions open.
    public static List<CodexEntry> Bosses()
    {
        var list = new List<CodexEntry>();
        if (!BattleBestiary.Available) return list;
        foreach (var region in TowerRules.Regions)
        {
            var lair = BattleBestiary.Boss(region.id);
            if (lair == null || BattleBestiary.Family(lair.family) == null) continue;
            var boss = BattleCatalog.LairBoss(lair, region.bossDepth);
            var form = BattleBestiary.Form(boss.Species);
            list.Add(new CodexEntry { Kind = CodexKind.Boss, Id = "boss:" + region.id, Name = lair.title, Lair = lair, Region = region,
                Form = form, Fighter = boss, Rank = boss.Rank, Element = form.element, Role = form.role,
                Subtitle = "Lair of " + region.name + "  ·  " + form.name });
        }
        return list;
    }

    public static string RoleName(BattleRole role)
    {
        return role == BattleRole.Dps ? "Striker" : role.ToString();
    }

    // ---- numbers -------------------------------------------------------------------------------------------

    private static Dictionary<string, float> monsterMax, fighterMax, bossMax;

    // The stat block, with each bar measured against the best in the same group of cards (roster characters: on the
    // Tower's 1..10 scale). Monsters are shown at a rank inside their span, as a pack member at the depth where that
    // rank is natural.
    public static List<CodexStat> Stats(CodexEntry e, int rank = 0)
    {
        var stats = new List<CodexStat>();
        switch (e.Kind)
        {
            case CodexKind.Monster:
            {
                if (monsterMax == null) monsterMax = Maxima(MonsterAtTop());
                var u = BattleCatalog.Specimen(e.Form, rank > 0 ? rank : e.Form.minRank);
                Unit(stats, u, monsterMax, false);
                break;
            }
            case CodexKind.Boss:
            {
                if (bossMax == null) { var all = new List<BattleUnit>(); foreach (var b in Bosses()) all.Add(b.Fighter); bossMax = Maxima(all); }
                Unit(stats, e.Fighter, bossMax, false);
                break;
            }
            case CodexKind.Fighter:
            {
                if (fighterMax == null) fighterMax = Maxima(BattleCatalog.Party());
                Unit(stats, e.Fighter, fighterMax, true);
                break;
            }
            case CodexKind.Roster:
            {
                foreach (var p in RosterPairs(e.Unit))
                    stats.Add(new CodexStat { Label = p.Key, Value = p.Value.ToString("0"), Fill = TowerRoster.GameStat((int)p.Value) / 10f });
                break;
            }
        }
        return stats;
    }

    // The four numbers printed on the card front.
    public static List<CodexStat> FrontStats(CodexEntry e)
    {
        var all = Stats(e);
        if (e.Kind == CodexKind.Roster)
        {
            // The two the character leans on, then grit and luck.
            var u = e.Unit;
            var pick = new List<string> { Short(u.primary), Short(u.secondary), "GRT", "LCK", "MIG", "WIT" };
            var front = new List<CodexStat>();
            foreach (var label in pick)
            {
                var s = all.Find(x => x.Label == label);
                if (s.Label != null && !front.Exists(x => x.Label == label)) front.Add(s);
                if (front.Count == 4) break;
            }
            return front;
        }
        var keep = new List<CodexStat>();
        foreach (var s in all)
            if (s.Label == "HP" || s.Label == "ATK" || s.Label == "MAG" || s.Label == "DEF" || s.Label == "SPD") keep.Add(s);
        // One offence number: whichever the unit hits harder with.
        var atk = keep.Find(x => x.Label == "ATK"); var mag = keep.Find(x => x.Label == "MAG");
        if (atk.Label != null && mag.Label != null) keep.Remove(float.Parse(atk.Value) >= float.Parse(mag.Value) ? mag : atk);
        if (keep.Count > 4) keep.RemoveRange(4, keep.Count - 4);
        return keep;
    }

    private static IEnumerable<BattleUnit> MonsterAtTop()
    {
        foreach (var family in BattleBestiary.Families)
            foreach (var form in family.forms) yield return BattleCatalog.Specimen(form, form.maxRank);
    }

    private static Dictionary<string, float> Maxima(IEnumerable<BattleUnit> units)
    {
        var max = new Dictionary<string, float>();
        foreach (var u in units)
        {
            Raise(max, "HP", u.MaxHp); Raise(max, "ATK", u.Attack); Raise(max, "MAG", u.Magic); Raise(max, "DEF", u.Defense);
            Raise(max, "RES", u.Resistance); Raise(max, "SPD", u.Speed); Raise(max, "CRIT", u.CritRate * 100f);
        }
        return max;
    }

    private static void Raise(Dictionary<string, float> max, string key, float v) { float m; if (!max.TryGetValue(key, out m) || v > m) max[key] = v; }

    private static float Fill(float v, Dictionary<string, float> max, string key)
    {
        float m;
        return max.TryGetValue(key, out m) && m > 0 ? Mathf.Clamp01(v / m) : 0f;
    }

    private static void Unit(List<CodexStat> stats, BattleUnit u, Dictionary<string, float> max, bool party)
    {
        Add(stats, "HP", u.MaxHp, max);
        if (u.Attack > 0 || u.Magic <= 0) Add(stats, "ATK", u.Attack, max);
        if (u.Magic > 0) Add(stats, "MAG", u.Magic, max);
        Add(stats, "DEF", u.Defense, max);
        Add(stats, "RES", u.Resistance, max);
        if (u.Speed > 0) Add(stats, "SPD", u.Speed, max);
        if (party && u.Speed > 0) stats.Add(new CodexStat { Label = "CRIT", Value = Mathf.RoundToInt(u.CritRate * 100f) + "%", Fill = Fill(u.CritRate * 100f, max, "CRIT") });
    }

    private static void Add(List<CodexStat> stats, string label, float v, Dictionary<string, float> max)
    {
        stats.Add(new CodexStat { Label = label, Value = Mathf.RoundToInt(v).ToString(), Fill = Fill(v, max, label) });
    }

    private static IEnumerable<KeyValuePair<string, float>> RosterPairs(RosterUnit u)
    {
        var s = u.stats;
        yield return new KeyValuePair<string, float>("MIG", s.might);
        yield return new KeyValuePair<string, float>("SGT", s.sight);
        yield return new KeyValuePair<string, float>("GRT", s.grit);
        yield return new KeyValuePair<string, float>("CHA", s.charm);
        yield return new KeyValuePair<string, float>("WIT", s.wit);
        yield return new KeyValuePair<string, float>("GRC", s.grace);
        yield return new KeyValuePair<string, float>("LCK", s.luck);
    }

    private static string Short(string stat)
    {
        switch ((stat ?? "").ToLowerInvariant())
        {
            case "might": return "MIG"; case "sight": return "SGT"; case "grit": return "GRT"; case "charm": return "CHA";
            case "wit": return "WIT"; case "grace": return "GRC"; case "luck": return "LCK";
        }
        return "";
    }

    // ---- moves ---------------------------------------------------------------------------------------------

    // The card back. A monster's is its move list exactly as the painted card was made for (Tools/build_monster_prompts.py
    // action_list): up to four moves, then GKOM passives to fill four rows.
    public static List<CodexMove> Moves(CodexEntry e)
    {
        var rows = new List<CodexMove>();
        switch (e.Kind)
        {
            case CodexKind.Monster:
            {
                var form = e.Form;
                foreach (var c in form.cards ?? new BestiaryCard[0])
                {
                    if (rows.Count == 4) break;
                    BattleTarget target;
                    if (!Enum.TryParse(c.target, out target)) target = BattleTarget.Enemy;
                    rows.Add(new CodexMove { Name = c.name, Tag = MoveKind(target, c.power, c.heal, c.status), Cost = c.ep,
                        Text = Describe(target, c.power, c.magic, c.heal, c.status, c.magnitude, c.duration) });
                }
                if (form.cards != null && form.cards.Length > 1 && rows.Count == 4 && (rows[3].Tag == "Attack" || rows[3].Tag == "Area")) rows[3].Tag = "Signature";
                if (rows.Count < 4) rows.Add(new CodexMove { Name = "GKOM Core", Tag = "Passive",
                    Text = "Its core orb" + (string.IsNullOrEmpty(form.core) ? "" : " (" + form.core + ")") + " powers it; shatter the core and it falls." });
                if (rows.Count < 4) rows.Add(new CodexMove { Name = "Crystal Hide", Tag = "Passive",
                    Text = Capital(string.IsNullOrEmpty(form.Family.material) ? "crystal" : form.Family.material) + " grows over its body as its rank rises." });
                break;
            }
            case CodexKind.Boss:
                foreach (var c in BattleCatalog.EnemyCards(e.Fighter))
                {
                    var row = Row(c, "EP");
                    if (c.Status == "Charging") { row.Tag = "Charge"; row.Text = "Gathers power: next round it unleashes its signature."; }
                    else if (c.Id == "e_cataclysm") { row.Tag = "Signature"; row.Cost = -1; }
                    rows.Add(row);
                }
                break;
            case CodexKind.Fighter:
            {
                bool jd = e.Id == "jd";
                foreach (var c in jd ? BattleCatalog.SummonerKit() : BattleCatalog.FighterKit(e.Fighter)) rows.Add(Row(c, jd ? "AP" : "EP"));
                foreach (var c in jd ? BattleCatalog.SummonerUltimates() : BattleCatalog.Ultimates(e.Fighter))
                { var row = Row(c, "EP"); row.Tag = jd ? "Decree" : "Ultimate"; row.Cost = -1; rows.Add(row); }
                var awaken = jd ? null : BattleCatalog.Awakening(e.Fighter);
                if (awaken != null) { var row = Row(awaken, "EP"); row.Tag = "Awakening"; row.Cost = -1; rows.Add(row); }
                break;
            }
            case CodexKind.Roster:
            {
                var u = e.Unit;
                if (!string.IsNullOrEmpty(u.weapon)) rows.Add(new CodexMove { Name = u.weapon, Tag = "Weapon", Text = u.weaponDesc });
                Ability(rows, u.passive, "Passive");
                Ability(rows, u.ability1, "Ability");
                Ability(rows, u.ability2, "Ability");
                Ability(rows, u.ability3, "Ability");
                Ability(rows, u.ultimate, "Ultimate");
                Ability(rows, u.perk, "Perk");
                if (!string.IsNullOrEmpty(u.tool)) rows.Add(new CodexMove { Name = u.tool, Tag = "Tool", Text = u.toolDesc });
                break;
            }
        }
        return rows;
    }

    private static CodexMove Row(BattleCard c, string unit)
    {
        int cost = unit == "AP" ? c.Ap : c.Ep;
        string text = Describe(c.Target, c.Power, c.Magic, c.Heal, c.Status, c.Magnitude, c.Duration, c.Draw, c.EpGain, c.ApGain, c.TransferEp, c.TransferAp);
        string keywords = BattleCatalog.KeywordLine(c);
        return new CodexMove { Name = c.Name, Tag = c.Kind == BattleCardKind.Ultimate ? "Ultimate" : MoveKind(c.Target, c.Power, c.Heal, c.Status),
            Cost = cost, CostUnit = unit, Text = string.IsNullOrEmpty(keywords) ? text : text + "  " + keywords };
    }

    // Roster abilities read "Name: what it does. Visual: how it looks"; the card keeps the first two.
    private static void Ability(List<CodexMove> rows, string line, string tag)
    {
        if (string.IsNullOrEmpty(line)) return;
        int visual = line.IndexOf(" Visual:", StringComparison.Ordinal);
        if (visual >= 0) line = line.Substring(0, visual);
        int colon = line.IndexOf(':');
        rows.Add(colon > 0 && colon < 48
            ? new CodexMove { Name = line.Substring(0, colon).Trim(), Tag = tag, Text = line.Substring(colon + 1).Trim() }
            : new CodexMove { Name = tag, Tag = tag, Text = line.Trim() });
    }

    public static string MoveKind(BattleTarget target, float power, float heal, string status)
    {
        if (heal > 0) return "Support";
        if (target == BattleTarget.AllEnemies) return "Area";
        if (target == BattleTarget.Self) return "Guard";
        if (target == BattleTarget.Ally || target == BattleTarget.AllAllies) return "Support";
        if (target == BattleTarget.None) return "Tactic";
        if (status == "Stun" || status == "Slow" || status == "AttackDown" || status == "DefenseDown") return "Control";
        return "Attack";
    }

    public static string Describe(BattleTarget target, float power, bool magic, float heal, string status, float magnitude, int duration,
        int draw = 0, int epGain = 0, int apGain = 0, bool transferEp = false, bool transferAp = false)
    {
        string who = Who(target);
        var parts = new List<string>();
        if (power > 0) parts.Add((magic ? "Magic" : "Physical") + " hit to " + who + " (" + power.ToString("0.##") + "x)");
        if (heal > 0) parts.Add("heals " + who + " for " + heal.ToString("0"));
        if (!string.IsNullOrEmpty(status))
        {
            string dur = duration > 1 ? " for " + duration + " turns" : duration == 1 ? " for 1 turn" : "";
            parts.Add(StatusText(status, magnitude) + (power > 0 || heal > 0 ? "" : " on " + who) + dur);
        }
        if (draw > 0) parts.Add("draw " + draw + (draw == 1 ? " card" : " cards"));
        if (epGain > 0) parts.Add("+" + epGain + " EP" + (target == BattleTarget.Ally ? " to an ally" : ""));
        if (apGain > 0) parts.Add("+" + apGain + " AP" + (target == BattleTarget.Ally ? " to an ally" : ""));
        if (transferEp) parts.Add("hands your EP to an ally");
        if (transferAp) parts.Add("hands your AP to an ally");
        if (parts.Count == 0) return "";
        return Capital(string.Join("; ", parts.ToArray())) + ".";
    }

    private static string Who(BattleTarget target)
    {
        switch (target)
        {
            case BattleTarget.AllEnemies: return "every foe";
            case BattleTarget.Self: return "itself";
            case BattleTarget.Ally: return "an ally";
            case BattleTarget.AllAllies: return "all allies";
            case BattleTarget.None: return "the field";
            default: return "one foe";
        }
    }

    private static string StatusText(string status, float m)
    {
        switch (status)
        {
            case "DefenseUp": return "Defense up" + Pct(m);
            case "AttackUp": return "Attack up" + Pct(m);
            case "DefenseDown": return "Defense down" + Pct(m);
            case "AttackDown": return "Attack down" + Pct(m);
            case "Slow": return "Slow" + Pct(m);
            case "Burn": case "Poison": case "Regen": return status + (m > 0 ? " " + m.ToString("0") + "/turn" : "");
            case "Shield": return "Shield " + m.ToString("0");
            case "IceCounter": return "Counters the next hit (" + m.ToString("0.#") + "x)";
            case "Charging": return "Gathers power";
        }
        return status;
    }

    private static string Pct(float m) { return m > 0f && m <= 1f ? " " + Mathf.RoundToInt(m * 100f) + "%" : ""; }

    private static string Capital(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

    // ---- lore ----------------------------------------------------------------------------------------------

    public static string EvolutionNote(BestiaryForm form)
    {
        var next = string.IsNullOrEmpty(form.evolvesTo) ? null : BattleBestiary.Form(form.evolvesTo);
        return next == null ? "Final form of its family." : "Evolves into " + next.name + " at rank " + BattleBestiary.RankName(next.minRank) + ".";
    }

    public static string Habitat(BestiaryFamily family)
    {
        if (family.themes == null || family.themes.Length == 0) return "";
        var names = new List<string>();
        foreach (var t in family.themes) names.Add(t == "edge" ? "anywhere" : t);
        return string.Join(", ", names.ToArray());
    }

    // ---- art -----------------------------------------------------------------------------------------------

    [Serializable] private sealed class PanelFile { public PanelCard[] cards; }
    [Serializable] private sealed class PanelCard { public string id; public float[] panels; }

    private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    private static Dictionary<string, Rect[]> panels;

    // Drops loaded art (and remembered misses) so cards painted since the last look show up.
    public static void Forget() { textures.Clear(); panels = null; monsterMax = fighterMax = bossMax = null; }

    private static Texture2D Load(string path)
    {
        Texture2D tex;
        if (!textures.TryGetValue(path, out tex)) { tex = Resources.Load<Texture2D>(path); textures[path] = tex; }
        return tex;
    }

    // The card front illustration, or null when this card has none yet (thumb: the small copy for the grid).
    public static Texture2D Front(CodexEntry e, bool thumb)
    {
        switch (e.Kind)
        {
            case CodexKind.Monster:
            case CodexKind.Boss:
            {
                var tex = Load(CardRoot + e.Form.id + (thumb ? "_thumb" : "_card"));
                return tex != null || !thumb ? tex : Load(CardRoot + e.Form.id + "_card");
            }
            case CodexKind.Fighter:
                return MediaLibrary.ImageFor("unit." + e.Id + ".card") ?? Load("AdamsHaven/FullCards/" + e.Id);
            case CodexKind.Roster:
                // Heroes have card-front art; residents have card.
                return Load(TowerRoster.ArtPath(e.Unit, "card-front")) ?? Load(TowerRoster.ArtPath(e.Unit, "card"));
        }
        return null;
    }

    // The battle cutout of a monster (transparent background): the stand-in front and the silhouette of unmet beasts.
    public static Texture2D Cutout(CodexEntry e)
    {
        if (e.Form == null) return null;
        var own = MediaLibrary.ImageFor("unit." + e.Form.id + ".model");
        if (own != null) return own;
        return Load("AdamsHaven/FieldModels/" + (string.IsNullOrEmpty(e.Form.art) ? e.Form.id : e.Form.art));
    }

    // An unmet beast's shape: white with the outline in its alpha, from its own battle sprite, else from a battle cutout
    // with a clean background (Tools/sync_codex_cards.py). Null: no trustworthy shape yet.
    public static Texture2D Silhouette(CodexEntry e)
    {
        if (e.Form == null) return null;
        return Load(CardRoot + e.Form.id + "_shadow")
            ?? Load(CardRoot + "field_" + (string.IsNullOrEmpty(e.Form.art) ? e.Form.id : e.Form.art) + "_shadow");
    }

    // The painted move-list card (four blank panels) for a monster or lair boss, or null.
    public static Texture2D MovesArt(CodexEntry e)
    {
        return e.Form == null ? null : Load(CardRoot + e.Form.id + "_moves");
    }

    public static bool HasPaintedCard(CodexEntry e) { return e.Form != null && Front(e, true) != null; }

    // The four move panels on that card, top to bottom.
    public static Rect[] Panels(CodexEntry e)
    {
        if (panels == null)
        {
            panels = new Dictionary<string, Rect[]>();
            var text = Resources.Load<TextAsset>(CardRoot + "panels");
            PanelFile file = null;
            if (text != null) { try { file = JsonUtility.FromJson<PanelFile>(text.text); } catch (Exception ex) { Debug.LogWarning("Codex panels: " + ex.Message); } }
            if (file != null && file.cards != null)
                foreach (var c in file.cards)
                {
                    if (c == null || string.IsNullOrEmpty(c.id) || c.panels == null || c.panels.Length != 16) continue;
                    var rects = new Rect[4];
                    for (int i = 0; i < 4; i++) rects[i] = new Rect(c.panels[i * 4], c.panels[i * 4 + 1], c.panels[i * 4 + 2], c.panels[i * 4 + 3]);
                    panels[c.id] = rects;
                }
        }
        Rect[] found;
        return e.Form != null && panels.TryGetValue(e.Form.id, out found) ? found : DefaultPanels;
    }

    // ---- colours -------------------------------------------------------------------------------------------

    public static Color ElementColor(string element)
    {
        switch (element)
        {
            case "Fire": return new Color(1f, .52f, .20f);
            case "Water": return new Color(.38f, .82f, 1f);
            case "Wind": return new Color(.56f, 1f, .72f);
            case "Earth": return new Color(.94f, .74f, .32f);
            case "Lightning": return new Color(1f, .94f, .36f);
            case "Light": return new Color(1f, .94f, .72f);
            case "Dark": return new Color(.76f, .42f, 1f);
        }
        return new Color(.80f, .86f, .94f);
    }

    // F grey .. SSR prismatic (t: seconds, for the SSR shimmer).
    public static Color RankColor(int rank, float t = 0f)
    {
        switch (rank)
        {
            case 1: return new Color(.66f, .70f, .76f);
            case 2: return new Color(.50f, .86f, .52f);
            case 3: return new Color(.42f, .72f, 1f);
            case 4: return new Color(.36f, .92f, .88f);
            case 5: return new Color(.72f, .52f, 1f);
            case 6: return new Color(1f, .80f, .36f);
            case 7: return new Color(1f, .52f, .30f);
            case 8: return new Color(1f, .36f, .56f);
            case 9: return Color.HSVToRGB(Mathf.Repeat(t * .15f, 1f), .55f, 1f);
        }
        return new Color(.97f, .82f, .48f);   // fighters: gold
    }
}
