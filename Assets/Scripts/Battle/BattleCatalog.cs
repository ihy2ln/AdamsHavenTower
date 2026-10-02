using System;
using System.Collections.Generic;

// Authored combat content ported from core/content.gd and core/ability_cards.gd.
public static class BattleCatalog
{
    private static readonly float[] Tier = { 1f, 1.15f, 1.32f, 1.52f, 1.75f, 2.01f, 2.31f, 2.66f, 3.06f, 3.52f };
    private static readonly Dictionary<string, string[]> Bonds = new Dictionary<string, string[]>
    {
        { "kaela", new [] { "ghislaine", "elara" } },
        { "ghislaine", new [] { "kaela", "daisy" } },
        { "elara", new [] { "kaela", "helda", "clarity" } },
        { "helda", new [] { "elara", "clarity" } },
        { "daisy", new [] { "ghislaine", "clarity" } },
        { "clarity", new [] { "helda", "daisy", "elara" } },
    };

    private static BattleUnit Fighter(string id, string name, BattleRole role, BattleElement element,
        int tier, int hp, float attack, float magic, float defense, float resistance, float speed,
        float crit, float critDamage)
    {
        float scale = Tier[tier] / Tier[3];
        int maxHp = (int)(hp * scale);
        return new BattleUnit { Id = id, Name = name, Art = "Chibi/" + id, Role = role,
            Element = element, MaxHp = maxHp, Hp = maxHp, Attack = attack * scale,
            Magic = magic * scale, Defense = defense * scale, Resistance = resistance * scale,
            Speed = speed, CritRate = crit, CritDamage = critDamage,
            InnateTaunt = role == BattleRole.Tank };
    }

    public static List<BattleUnit> Party()
    {
        return new List<BattleUnit>
        {
            Fighter("kaela", "Kaela Stormfang", BattleRole.Tank, BattleElement.Water, 6, 175, 20, 8, 16, 10, 13, .12f, 1.60f),
            Fighter("ghislaine", "Ghislaine Dedoldia", BattleRole.Tank, BattleElement.Fire, 6, 170, 26, 5, 16, 6, 11, .10f, 1.75f),
            Fighter("elara", "Elara Vellum", BattleRole.Ranger, BattleElement.Lightning, 4, 105, 7, 24, 7, 14, 12, .08f, 1.50f),
            Fighter("helda", "Helda Frostmane", BattleRole.Support, BattleElement.Water, 3, 130, 11, 17, 13, 15, 8, .08f, 1.50f),
            Fighter("daisy", "Daisy Wildheart", BattleRole.Dps, BattleElement.Fire, 5, 160, 24, 12, 13, 10, 13, .18f, 1.70f),
            Fighter("clarity", "Clarity Lockhart", BattleRole.Support, BattleElement.Light, 4, 140, 18, 18, 11, 13, 12, .14f, 1.60f),
        };
    }

    // The chosen fighters, in the order given (expedition party selection).
    public static List<BattleUnit> Party(IList<string> ids)
    {
        var all = Party();
        var chosen = new List<BattleUnit>();
        foreach (string id in ids)
        {
            BattleUnit unit = all.Find(u => u.Id == id);
            if (unit != null) chosen.Add(unit);
        }
        return chosen;
    }

    public static BattleUnit JD()
    {
        return new BattleUnit { Id = "jd", Name = "JD", Art = "Chibi/jd",
            Element = BattleElement.Light, MaxHp = 260, Hp = 260, Defense = 14, Resistance = 14, Speed = 0 };
    }

    private static BattleCard C(string id, string name, string owner, int ep, int ap,
        float power = 0, BattleTarget target = BattleTarget.Enemy, string status = "",
        float magnitude = 0, int duration = 0, float heal = 0, bool magic = false,
        int draw = 0, int epGain = 0, int apGain = 0, bool transferEp = false, bool transferAp = false)
    {
        string[] partners;
        Bonds.TryGetValue(owner ?? "", out partners);
        return new BattleCard { Id = id, Name = name, Owner = owner, Ep = ep, Ap = ap,
            Power = power, Target = target, Status = status, Magnitude = magnitude,
            Duration = duration, Heal = heal, Magic = magic, Draw = draw,
            EpGain = epGain, ApGain = apGain, TransferEp = transferEp, TransferAp = transferAp,
            Partners = partners ?? Array.Empty<string>(),
            Kind = owner == "jd" ? BattleCardKind.Summoner : transferEp || transferAp ? BattleCardKind.Transfer : power > 0 ? BattleCardKind.Attack : BattleCardKind.Skill };
    }

    public static List<BattleCard> FighterKit(BattleUnit unit)
    {
        List<BattleCard> cards = new List<BattleCard>();
        string id = unit.Id;
        if (id == "kaela")
        {
            cards.Add(C("mv_frost_jab", "Frost Jab", id, 1, 1, 1.3f));
            cards.Add(C("mv_rime_sweep", "Rime Sweep", id, 2, 1, 1.1f, status: "Slow", magnitude: .25f, duration: 2));
            cards.Add(C("mv_glacier_guard", "Glacier Guard", id, 1, 0, target: BattleTarget.Self, status: "DefenseUp", magnitude: .35f, duration: 2));
            cards.Add(C("mv_shatter_hook", "Shatter", id, 3, 1, 2.2f, status: "DefenseDown", magnitude: .2f, duration: 2));
            cards.Add(C("mv_whiteout_counter", "Whiteout Counter", id, 2, 0, target: BattleTarget.Self, status: "IceCounter", magnitude: 1.4f, duration: 1));
            cards.Add(C("mv_permafrost_brace", "Perma Brace", id, 2, 0, target: BattleTarget.Self, status: "DefenseUp", magnitude: .4f, duration: 2, heal: 24));
        }
        else if (id == "ghislaine")
        {
            cards.Add(C("gh_tiger_cleave", "Tiger Cleave", id, 1, 1, 1.4f));
            cards.Add(C("gh_guard_stance", "Guard Stance", id, 1, 0, target: BattleTarget.Self, status: "DefenseUp", magnitude: .4f, duration: 2));
            cards.Add(C("gh_fireline_cut", "Fireline Cut", id, 2, 1, 1.5f, BattleTarget.AllEnemies));
            cards.Add(C("gh_ember_chain", "Ember Chain", id, 2, 1, 1.2f));
            cards.Add(C("gh_reckless_swing", "Reckless Hit", id, 3, 1, 3f));
            cards.Add(C("gh_tiger_riposte", "Tiger Riposte", id, 2, 1, 1.8f, status: "DefenseDown", magnitude: .3f, duration: 2));
        }
        else if (id == "elara")
        {
            cards.Add(C("el_arc_bolt", "Arc Bolt", id, 1, 1, 1.3f, magic: true));
            cards.Add(C("el_piercing_shot", "Piercing Shot", id, 2, 1, 2.2f, magic: true));
            cards.Add(C("el_forest_scout", "Forest Scout", id, 1, 0, target: BattleTarget.None, draw: 2));
            cards.Add(C("el_sapping_volley", "Sap Volley", id, 2, 1, 1f, status: "AttackDown", magnitude: .25f, duration: 2, magic: true));
            cards.Add(C("el_artillery_barrage", "Barrage", id, 3, 1, 1.5f, BattleTarget.AllEnemies, magic: true));
            cards.Add(C("el_storm_tally", "Storm Tally", id, 2, 1, 1f, BattleTarget.AllEnemies, magic: true));
        }
        else if (id == "helda")
        {
            cards.Add(C("he_hearthfire_mend", "Hearth Mend", id, 1, 1, target: BattleTarget.Ally, heal: 28));
            cards.Add(C("he_chilling_touch", "Chilling Touch", id, 1, 1, 1.1f, magic: true));
            cards.Add(C("he_frosted_ward", "Frost Ward", id, 2, 1, target: BattleTarget.AllAllies, status: "DefenseUp", magnitude: .25f, duration: 2));
            cards.Add(C("he_reserve_tonic", "Reserve Tonic", id, 1, 0, target: BattleTarget.Ally, draw: 1, epGain: 1));
            cards.Add(C("he_lend_strength", "Lend Strength", id, 0, 0, target: BattleTarget.Ally, transferEp: true));
            cards.Add(C("he_bulwark_brew", "Bulwark Brew", id, 3, 1, target: BattleTarget.AllAllies, status: "DefenseUp", magnitude: .5f, duration: 2));
        }
        else if (id == "daisy")
        {
            cards.Add(C("da_bonfire_brand", "Bonfire Brand", id, 1, 1, 1.5f));
            cards.Add(C("da_wand_sweep", "Wand Sweep", id, 2, 1, 1.5f, BattleTarget.AllEnemies));
            cards.Add(C("da_ember_thrust", "Ember Thrust", id, 1, 1, 1.4f));
            cards.Add(C("da_matriarch_pyre", "Matriarch Pyre", id, 3, 1, 1.8f, BattleTarget.AllEnemies, "DefenseDown", .2f, 2));
            cards.Add(C("da_spearflame_rush", "Flame Rush", id, 3, 1, 2.2f, status: "DefenseDown", magnitude: .2f, duration: 2));
            cards.Add(C("da_burning_circle", "Fire Circle", id, 3, 1, 1.8f, BattleTarget.AllEnemies, "DefenseDown", .2f, 2));
        }
        else if (id == "clarity")
        {
            cards.Add(C("cy_radiant_palm", "Radiant Palm", id, 1, 1, 1.4f));
            cards.Add(C("cy_barkeep_tonic", "Tonic", id, 1, 0, target: BattleTarget.Ally, heal: 22));
            cards.Add(C("cy_flare", "Flare", id, 2, 1, 1.6f, BattleTarget.AllEnemies, magic: true));
            cards.Add(C("cy_uplift", "Uplift", id, 2, 1, target: BattleTarget.AllAllies, status: "AttackUp", magnitude: .25f, duration: 2));
            cards.Add(C("cy_smite", "Smite", id, 3, 1, 2.9f, magic: true));
            cards.Add(C("cy_rejuvenating_draught", "Draught", id, 2, 0, target: BattleTarget.Ally, heal: 22, epGain: 1));
        }
        foreach (BattleCard card in cards) card.Element = unit.Element;
        return cards;
    }

    public static List<BattleCard> SummonerKit()
    {
        return new List<BattleCard>
        {
            C("sm_rally", "Rally", "jd", 0, 1, target: BattleTarget.None, draw: 2),
            C("sm_surge", "Surge", "jd", 0, 1, target: BattleTarget.Ally, epGain: 2),
            C("sm_second", "Second Wind", "jd", 0, 2, target: BattleTarget.Ally, apGain: 1),
            C("sm_banner", "Banner", "jd", 0, 1, target: BattleTarget.AllAllies, status: "AttackUp", magnitude: .3f, duration: 2),
            C("sm_aegis", "Aegis", "jd", 0, 1, target: BattleTarget.AllAllies, status: "DefenseUp", magnitude: .3f, duration: 2),
            C("sm_wither", "Wither", "jd", 0, 1, target: BattleTarget.AllEnemies, status: "DefenseDown", magnitude: .25f, duration: 2),
            C("sm_hush", "Hush", "jd", 0, 1, target: BattleTarget.AllEnemies, status: "AttackDown", magnitude: .25f, duration: 2),
            C("sm_mend", "Blessing", "jd", 0, 2, target: BattleTarget.AllAllies, heal: 30),
            C("sm_draw_eye", "Taunt", "jd", 0, 1, target: BattleTarget.Ally, status: "Taunt", duration: 2),
        };
    }

    public static BattleCard Basic(BattleUnit unit)
    {
        return new BattleCard { Id = "basic_" + unit.Id, Name = "Basic Attack", Owner = unit.Id,
            Element = unit.Element, Ep = 0, Ap = 1, Power = .55f, Target = BattleTarget.Enemy };
    }

    public static List<BattleCard> SummonerUltimates()
    {
        return new List<BattleCard>
        {
            C("ult_sum_a", "Rally Decree", "jd", 0, 0,
                target: BattleTarget.AllAllies, status: "AttackUp", magnitude: .5f, duration: 3, heal: 25),
            C("ult_sum_b", "Sunder Decree", "jd", 0, 0,
                target: BattleTarget.AllEnemies, status: "DefenseDown", magnitude: .4f, duration: 3),
        };
    }

    private static BattleCard Ult(BattleUnit unit, string id, string name, float power, BattleTarget target,
        string status = "", float magnitude = 0, int duration = 0, float heal = 0, bool magic = false)
    {
        BattleCard card = C(id, name, unit.Id, 0, 0, power, target, status, magnitude, duration, heal, magic);
        card.Kind = BattleCardKind.Ultimate; card.Element = unit.Element;
        return card;
    }

    public static List<BattleCard> Ultimates(BattleUnit unit)
    {
        List<BattleCard> cards = new List<BattleCard>();
        if (unit == null) return cards;
        switch (unit.Id)
        {
            case "kaela":
                cards.Add(Ult(unit, "ult_kaela_avalanche", "Avalanche Breaker", 3f, BattleTarget.AllEnemies, "Slow", .25f, 2));
                cards.Add(Ult(unit, "ult_kaela_bastion", "Winter Bastion", 0, BattleTarget.AllAllies, "DefenseUp", .45f, 2, 32)); break;
            case "ghislaine":
                cards.Add(Ult(unit, "ult_ghislaine", "White Tiger Rend", 4.6f, BattleTarget.Enemy, "DefenseDown", .35f, 2));
                cards.Add(Ult(unit, "ult_ghislaine_oath", "Warden's Oath", 0, BattleTarget.AllAllies, "DefenseUp", .45f, 2, 32)); break;
            case "elara":
                cards.Add(Ult(unit, "ult_elara", "Stormfall Barrage", 2.9f, BattleTarget.AllEnemies, magic: true));
                cards.Add(Ult(unit, "ult_elara_convergence", "Lightning Convergence", 4.6f, BattleTarget.Enemy, "DefenseDown", .35f, 2, magic: true)); break;
            case "helda":
                cards.Add(Ult(unit, "ult_helda", "Hearth Renewal", 0, BattleTarget.AllAllies, "Regen", 10, 3, 55));
                cards.Add(Ult(unit, "ult_helda_sanctuary", "Winter Sanctuary", 0, BattleTarget.AllAllies, "DefenseUp", .45f, 2, 32)); break;
            case "daisy":
                cards.Add(Ult(unit, "ult_daisy", "Island Conflagration", 3.2f, BattleTarget.AllEnemies, "DefenseDown", .3f, 2));
                cards.Add(Ult(unit, "ult_daisy_inferno", "Matriarch's Inferno", 4.6f, BattleTarget.Enemy, "DefenseDown", .35f, 2)); break;
            case "clarity":
                cards.Add(Ult(unit, "ult_clarity", "Final Heaven", 4.2f, BattleTarget.Enemy, heal: 20));
                cards.Add(Ult(unit, "ult_clarity_feast", "Luminous Feast", 0, BattleTarget.AllAllies, "AttackUp", .5f, 3, 25)); break;
        }
        return cards;
    }

    // One free support action after a fighter recovers from breakdown.
    // These are generated on demand, outside the shared draw pile.
    public static BattleCard Awakening(BattleUnit unit)
    {
        if (unit == null) return null;
        BattleCard card;
        switch (unit.Id)
        {
            case "kaela":
                card = C("aw_kaela", "Winter Resolve", unit.Id, 0, 0, target: BattleTarget.AllAllies,
                    status: "DefenseUp", magnitude: .25f, duration: 2, heal: 12); break;
            case "ghislaine":
                card = C("aw_ghislaine", "Tiger's Return", unit.Id, 0, 0, target: BattleTarget.AllAllies,
                    status: "AttackUp", magnitude: .25f, duration: 2); break;
            case "elara":
                card = C("aw_elara", "Clear Script", unit.Id, 0, 0, target: BattleTarget.AllEnemies,
                    status: "DefenseDown", magnitude: .20f, duration: 2, draw: 1); break;
            case "helda":
                card = C("aw_helda", "Hearth Reprieve", unit.Id, 0, 0, target: BattleTarget.AllAllies,
                    status: "Regen", magnitude: 6f, duration: 2, heal: 18); break;
            case "daisy":
                card = C("aw_daisy", "Wildheart Surge", unit.Id, 0, 0, target: BattleTarget.AllAllies,
                    status: "AttackUp", magnitude: .25f, duration: 2, draw: 1); break;
            case "clarity":
                card = C("aw_clarity", "Recovery", unit.Id, 0, 0, target: BattleTarget.AllAllies,
                    status: "DefenseUp", magnitude: .20f, duration: 2, heal: 20); break;
            default: return null;
        }
        card.Kind = BattleCardKind.Awakening;
        card.Element = unit.Element;
        return card;
    }

    public static List<BattleCard> Deck(IEnumerable<BattleUnit> party)
    {
        List<BattleCard> cards = SummonerKit();
        foreach (BattleUnit unit in party) cards.AddRange(FighterKit(unit));
        return cards;
    }

    // ---- enemies ------------------------------------------------------------------------------
    //
    // The Silverwood bestiary. An encounter is built from a spec (depth, kind, theme, region, seed): the theme
    // picks which species live there, the kind picks the shape of the fight (a pack, an elite with an affix, or a
    // named boss with two phases), and depth scales the numbers. Depth 1 is Brook Edge, 13 the Silverwood Heart.

    // Stat shapes per role: hp, attack, magic, defence, speed.
    private const int Bruiser = 0, Skirmisher = 1, Caster = 2, Guardian = 3;
    private static readonly float[][] Shape = { new [] {1.35f,1.25f,.60f,1.05f,.80f}, new [] {.72f,1.10f,.70f,.80f,1.45f},
        new [] {.85f,.60f,1.50f,.85f,1.05f}, new [] {1.20f,.80f,.85f,1.50f,.85f} };

    private sealed class Species
    {
        public readonly string Id, Name;
        public readonly BattleElement Element;
        public readonly int Role, MinDepth;
        public readonly bool Large;
        public readonly string[] Themes;
        public Species(string id, string name, BattleElement element, int role, bool large, int minDepth, params string[] themes)
        { Id = id; Name = name; Element = element; Role = role; Large = large; MinDepth = minDepth; Themes = themes; }
    }

    private static readonly Species[] Bestiary =
    {
        new Species("shardling_sprout", "Shardling Sprout", BattleElement.Earth, Guardian, false, 1, "briar", "crystal", "marsh", "edge"),
        new Species("flintjaw_skitterer", "Flintjaw Skitterer", BattleElement.Fire, Skirmisher, false, 1, "briar", "ruin", "mine", "edge"),
        new Species("amberhide_grazer", "Amberhide Grazer", BattleElement.Earth, Bruiser, false, 1, "briar", "cave", "heartwood", "edge"),
        new Species("thorncrystal_stalker", "Thorncrystal Stalker", BattleElement.Wind, Skirmisher, false, 1, "briar", "keep", "heartwood", "edge"),
        new Species("quartzback_hound", "Quartzback Hound", BattleElement.Water, Bruiser, false, 1, "cave", "mine", "heartwood", "edge"),
        new Species("glasswing_mite", "Glasswing Mite", BattleElement.Wind, Skirmisher, false, 1, "marsh", "crystal", "cave", "blight"),
        new Species("obsidian_talon", "Obsidian Talon", BattleElement.Fire, Skirmisher, false, 2, "ruin", "keep", "blight", "mine"),
        new Species("viridian_prism_warden", "Prism Warden", BattleElement.Light, Caster, false, 2, "ruin", "heartwood", "keep", "marsh"),
        new Species("cobalt_burrower", "Cobalt Burrower", BattleElement.Earth, Guardian, true, 2, "cave", "mine", "crystal", "keep"),
        new Species("moonstone_ravager", "Moonstone Ravager", BattleElement.Water, Bruiser, true, 3, "marsh", "cave", "blight", "ruin"),
        new Species("stormglass_wyvern", "Stormglass Wyvern", BattleElement.Lightning, Skirmisher, true, 5, "crystal", "heartwood", "ruin"),
        new Species("eclipse_core_golem", "Eclipse Core Golem", BattleElement.Dark, Bruiser, true, 8, "blight"),
    };

    // Every species id, in bestiary order (the guild journal lists them all).
    public static string[] SpeciesIds { get { var ids = new string[Bestiary.Length]; for (int i = 0; i < ids.Length; i++) ids[i] = Bestiary[i].Id; return ids; } }

    // "Earth  •  found in briar, crystal, marsh" for the journal.
    public static string SpeciesInfo(string id)
    {
        foreach (Species s in Bestiary)
            if (s.Id == id) return s.Element + (s.Large ? ", large" : "") + "  •  found in " + string.Join(", ", s.Themes) + " places";
        return "";
    }

    // The bestiary name of a species id (journal), or the id itself when unknown.
    public static string SpeciesName(string id)
    {
        foreach (Species s in Bestiary) if (s.Id == id) return s.Name;
        return id;
    }

    private static Species SpeciesOf(string id)
    {
        foreach (Species s in Bestiary) if (s.Id == id) return s;
        return Bestiary[0];
    }

    // The lair boss of every region: species, title and the theme its minions come from.
    private static readonly Dictionary<string, string[]> Bosses = new Dictionary<string, string[]>
    {
        { "silverbrook_edge", new [] { "amberhide_grazer", "Amberhide Matriarch", "briar" } },
        { "rootside_camp", new [] { "thorncrystal_stalker", "Thornfang Alpha", "briar" } },
        { "shallow_ford", new [] { "quartzback_hound", "Rimecoat Packlord", "cave" } },
        { "moon_shrine", new [] { "viridian_prism_warden", "Moon-Shrine Ascendant", "ruin" } },
        { "old_bridge", new [] { "obsidian_talon", "Obsidian Roc", "keep" } },
        { "sunken_marsh", new [] { "moonstone_ravager", "Drowned Ravager", "marsh" } },
        { "watchpost_ruin", new [] { "cobalt_burrower", "Cobalt Siegeworm", "keep" } },
        { "silverwood_gate", new [] { "stormglass_wyvern", "Stormglass Wyvern Queen", "crystal" } },
        { "silverwood_d1", new [] { "quartzback_hound", "Fallen-Oak Packlord", "heartwood" } },
        { "silverwood_d2", new [] { "moonstone_ravager", "Mirror-Lake Ravager", "marsh" } },
        { "silverwood_d3", new [] { "cobalt_burrower", "Deepmaw of the Mines", "mine" } },
        { "silverwood_d4", new [] { "eclipse_core_golem", "Eclipse Core Golem", "blight" } },
        { "silverwood_d5", new [] { "eclipse_core_golem", "Heartrot Colossus", "blight" } },
    };

    private static int StableHash(string text)
    {
        unchecked { int h = 17; foreach (char c in text ?? "") h = h * 31 + c; return h & 0x7fffffff; }
    }

    private static readonly string[] Affixes = { "Vampiric", "Thorned", "Hasted", "Shielded", "Enraged" };

    // Monster numbers at a depth. Offence climbs faster than health, so deeper fights hit harder without
    // dragging on; power multiplies health (elites, bosses), offence takes its square root.
    // Lair bosses share one shape whatever their species, so every region's boss is a fight of the same weight.
    private static readonly float[] BossShape = { 1.25f, 1.15f, 1.15f, 1.15f, 1.0f };
    public const float EnemyHealth = 120f, EnemyAttack = 35f, EnemyMagic = 31f, HealthPerDepth = .12f, OffencePerDepth = .065f;
    public const float ElitePower = 2.0f, BossPower = 3.4f, MinionPower = .8f, BossOffence = 1.45f;

    private static BattleUnit Monster(Species s, int depth, int lane, float power, string id = null, string name = null, bool boss = false)
    {
        float[] k = boss ? BossShape : Shape[s.Role];
        float d = Math.Max(1, depth) - 1;
        float health = (1f + HealthPerDepth * d) * power * (s.Large && !boss ? 1.25f : 1f);
        float offence = (1f + OffencePerDepth * d) * (boss ? BossOffence : (float)Math.Sqrt(power));
        int hp = Math.Max(1, (int)(EnemyHealth * k[0] * health));
        return new BattleUnit { Id = (id ?? s.Id) + "_" + lane, Species = s.Id, Name = name ?? s.Name,
            Art = "FieldModels/" + s.Id, Element = s.Element, Enemy = true,
            Role = s.Role == Guardian ? BattleRole.Tank : s.Role == Caster ? BattleRole.Support : BattleRole.Dps,
            InnateTaunt = s.Role == Guardian, Lane = lane, MaxHp = hp, Hp = hp,
            Attack = EnemyAttack * k[1] * offence, Magic = EnemyMagic * k[2] * offence,
            Defense = 9f * k[3] * (1f + .05f * d), Resistance = 9f * k[3] * (1f + .05f * d),
            Speed = 10f * k[4], CritRate = .08f, CritDamage = 1.5f };
    }

    // Species that live in a theme and are deep enough to show up; "edge" stands in for any unknown theme.
    private static List<Species> Roster(string theme, int depth)
    {
        var list = new List<Species>();
        foreach (Species s in Bestiary)
            if (depth >= s.MinDepth && Array.IndexOf(s.Themes, string.IsNullOrEmpty(theme) ? "edge" : theme) >= 0) list.Add(s);
        if (list.Count < 3)
            foreach (Species s in Bestiary)
                if (depth >= s.MinDepth && Array.IndexOf(s.Themes, "edge") >= 0 && !list.Contains(s)) list.Add(s);
        return list;
    }

    public static BattleEncounter Build(BattleEncounterSpec spec)
    {
        var rng = new Random(spec.Seed != 0 ? spec.Seed : spec.Depth * 7919 + StableHash(spec.Kind + spec.Theme + spec.Region));
        var e = new BattleEncounter { Kind = spec.Kind, Depth = Math.Max(1, spec.Depth), Theme = spec.Theme ?? "", Region = spec.Region ?? "" };
        int depth = e.Depth;
        List<Species> roster = Roster(spec.Theme, depth);
        if (spec.Kind == "boss")
        {
            string[] boss;
            if (string.IsNullOrEmpty(spec.Region) || !Bosses.TryGetValue(spec.Region, out boss))
                boss = new [] { depth >= 8 ? "eclipse_core_golem" : "moonstone_ravager", depth >= 8 ? "Eclipse Core Golem" : "Grove Tyrant", spec.Theme };
            Species big = SpeciesOf(boss[0]);
            List<Species> minions = Roster(boss[2], depth);
            minions.RemoveAll(s => s.Large);
            if (minions.Count == 0) minions.Add(Bestiary[0]);
            e.Enemies.Add(Monster(minions[rng.Next(minions.Count)], depth, 0, MinionPower));
            BattleUnit lord = Monster(big, depth, 1, BossPower, "boss_" + big.Id, boss[1], true);
            lord.Boss = true; lord.Phase = 1;
            e.Enemies.Add(lord);
            e.Enemies.Add(Monster(minions[rng.Next(minions.Count)], depth, 2, MinionPower));
            e.Boss = lord;
            e.Title = "LAIR BOSS  -  " + boss[1].ToUpperInvariant();
            return e;
        }
        int count = spec.Kind == "elite" ? 3 : depth <= 2 ? 2 + rng.Next(2) : 3;
        for (int lane = 0; lane < count; lane++)
        {
            Species s = roster[rng.Next(roster.Count)];
            e.Enemies.Add(Monster(s, depth, lane, spec.Kind == "ambush" ? .9f : 1f));
        }
        if (spec.Kind == "elite")
        {
            int pick = rng.Next(e.Enemies.Count);
            BattleUnit old = e.Enemies[pick];
            BattleUnit elite = Monster(SpeciesOf(old.Species), depth, pick, ElitePower);
            elite.Affix = Affixes[rng.Next(Affixes.Length)];
            elite.Name = elite.Affix + " " + elite.Name;
            if (elite.Affix == "Hasted") elite.MaxAp = elite.Ap = 2;
            if (elite.Affix == "Shielded") elite.ApplyStatus("Shield", elite.MaxHp * .3f, 99);
            e.Enemies[pick] = elite;
            e.Title = "ELITE  -  " + elite.Name.ToUpperInvariant();
            if (depth >= 3) e.Commander = EnemyCommander(depth);
        }
        else e.Title = spec.Kind == "ambush" ? "AMBUSH" : "SILVERWOOD PACK";
        return e;
    }

    // Tower skirmishes (the dock BATTLE button) only know a floor: grove packs, elites from 3, a boss from 8.
    public static List<BattleUnit> Encounter(int floor)
    {
        int depth = Math.Max(1, Math.Abs(floor));
        string kind = depth >= 8 ? "boss" : depth >= 3 ? "elite" : "normal";
        return Build(new BattleEncounterSpec { Depth = depth, Kind = kind, Theme = "briar", Seed = Environment.TickCount }).Enemies;
    }

    public static BattleUnit EnemyCommander(int floor)
    {
        int depth = Math.Max(1, Math.Abs(floor));
        if (depth < 3) return null;
        int hp = (int)(170 * (1f + .10f * (depth - 1)));
        return new BattleUnit { Id = "enemy_commander", Name = "Vale Commander", Art = "FieldModels/enemy_summoner",
            Enemy = true, Element = BattleElement.Dark, MaxHp = hp, Hp = hp,
            Defense = 10f * (1f + .05f * depth), Resistance = 11f * (1f + .05f * depth), Speed = 11, CritRate = .1f, CritDamage = 1.6f };
    }

    private static BattleCard E(string id, string name, string owner, int ep, float power, BattleTarget target = BattleTarget.Enemy,
        string status = "", float magnitude = 0, int duration = 0, float heal = 0, bool magic = false)
    {
        return C(id, name, owner, ep, 1, power, target, status, magnitude, duration, heal, magic);
    }

    // Each species fights its own way; bosses add a charged signature move (see BattleState.RollIntents).
    public static List<BattleCard> EnemyCards(BattleUnit enemy)
    {
        string o = enemy.Id;
        var cards = new List<BattleCard>();
        switch (enemy.Species)
        {
            case "shardling_sprout":
                cards.Add(E("e_bash", "Crystal Bash", o, 1, 1.2f));
                cards.Add(E("e_root_guard", "Root Guard", o, 1, 0, BattleTarget.Self, "DefenseUp", .3f, 2)); break;
            case "amberhide_grazer":
                cards.Add(E("e_bash", "Crystal Bash", o, 1, 1.2f));
                cards.Add(E("e_amber_charge", "Amber Charge", o, 2, 1.6f));
                cards.Add(E("e_root_guard", "Root Guard", o, 1, 0, BattleTarget.Self, "DefenseUp", .3f, 2)); break;
            case "flintjaw_skitterer":
                cards.Add(E("e_claw", "Corrupted Claw", o, 1, 1.2f));
                cards.Add(E("e_ember_bite", "Ember Bite", o, 1, .9f, status: "Burn", magnitude: 6, duration: 2)); break;
            case "thorncrystal_stalker":
                cards.Add(E("e_pounce", "Thorn Pounce", o, 1, 1.35f));
                cards.Add(E("e_wail", "Hollow Wail", o, 2, .8f, status: "AttackDown", magnitude: .2f, duration: 2)); break;
            case "quartzback_hound":
                cards.Add(E("e_frost_fang", "Frost Fang", o, 1, 1.25f, status: "Slow", magnitude: .3f, duration: 1));
                cards.Add(E("e_pack_howl", "Pack Howl", o, 1, 0, BattleTarget.AllAllies, "AttackUp", .2f, 2)); break;
            case "glasswing_mite":
                cards.Add(E("e_glass_sting", "Glass Sting", o, 1, .8f, status: "Poison", magnitude: 5, duration: 2));
                cards.Add(E("e_swarm", "Glittering Swarm", o, 2, .6f, BattleTarget.AllEnemies)); break;
            case "obsidian_talon":
                cards.Add(E("e_rake", "Talon Rake", o, 1, 1.3f));
                cards.Add(E("e_dive", "Obsidian Dive", o, 2, 1.8f)); break;
            case "viridian_prism_warden":
                cards.Add(E("e_prism_bolt", "Prism Bolt", o, 1, 1.2f, magic: true));
                cards.Add(E("e_verdant_mend", "Verdant Mend", o, 1, 0, BattleTarget.Ally, heal: 26));
                cards.Add(E("e_prism_ward", "Prism Ward", o, 2, 0, BattleTarget.AllAllies, "DefenseUp", .25f, 2)); break;
            case "cobalt_burrower":
                cards.Add(E("e_burrow_slam", "Burrow Slam", o, 2, 1.3f, status: "Stun", magnitude: 1, duration: 1));
                cards.Add(E("e_cobalt_shell", "Cobalt Shell", o, 1, 0, BattleTarget.Self, "DefenseUp", .4f, 2)); break;
            case "moonstone_ravager":
                cards.Add(E("e_moon_maul", "Moon Maul", o, 1, 1.5f));
                cards.Add(E("e_lunar_roar", "Lunar Roar", o, 2, 0, BattleTarget.AllAllies, "AttackUp", .25f, 2)); break;
            case "stormglass_wyvern":
                cards.Add(E("e_storm_breath", "Storm Breath", o, 2, .9f, BattleTarget.AllEnemies, magic: true));
                cards.Add(E("e_lightning_dive", "Lightning Dive", o, 1, 1.6f)); break;
            case "eclipse_core_golem":
                cards.Add(E("e_cleave", "Eclipse Crush", o, 2, 1.9f));
                cards.Add(E("e_quake", "Core Quake", o, 3, 1.15f, BattleTarget.AllEnemies)); break;
            default:
                cards.Add(E("e_claw", "Corrupted Claw", o, 1, 1.2f));
                cards.Add(E("e_wail", "Hollow Wail", o, 2, .8f, status: "AttackDown", magnitude: .2f, duration: 2)); break;
        }
        if (enemy.Boss)
        {
            cards.Add(E("e_gather", "Gathering Fury", o, 1, 0, BattleTarget.Self, "Charging", 1, 2));
            cards.Add(BossSignature(enemy));
        }
        foreach (BattleCard card in cards) card.Element = enemy.Element;
        return cards;
    }

    // A boss's charged move: only ever played the round after "Gathering Fury" (the intent shows it coming).
    public static BattleCard BossSignature(BattleUnit boss)
    {
        BattleCard card = E("e_cataclysm", boss.Element == BattleElement.Dark ? "Eclipse Cataclysm" : boss.Element == BattleElement.Water ? "Tidal Ruin" :
            boss.Element == BattleElement.Lightning ? "Stormfall" : boss.Element == BattleElement.Fire ? "Cinder Storm" :
            boss.Element == BattleElement.Light ? "Prism Judgement" : "Shattering Rage", boss.Id, 0, 1.6f, BattleTarget.AllEnemies, "DefenseDown", .2f, 2);
        card.Element = boss.Element;
        return card;
    }

    public static List<BattleCard> EnemyCommanderCards()
    {
        return new List<BattleCard>
        {
            C("es_dirge", "Dirge", "enemy_commander", 0, 1,
                target: BattleTarget.AllEnemies, status: "AttackDown", magnitude: .20f, duration: 2),
            C("es_rot", "Creeping Rot", "enemy_commander", 0, 1,
                target: BattleTarget.AllEnemies, status: "Poison", magnitude: 7f, duration: 2),
            C("es_vigor", "Vale Vigor", "enemy_commander", 0, 1,
                target: BattleTarget.AllAllies, status: "DefenseUp", magnitude: .25f, duration: 2),
        };
    }
}

// What an expedition fight should be: depth sets the numbers, kind the shape ("normal", "elite", "boss",
// "ambush"), theme the species, region the lair boss. The same seed always builds the same fight.
public sealed class BattleEncounterSpec
{
    public int Depth = 1, Seed;
    public string Kind = "normal", Theme = "", Region = "";
}

public sealed class BattleEncounter
{
    public readonly List<BattleUnit> Enemies = new List<BattleUnit>();
    public BattleUnit Commander, Boss;
    public string Kind = "normal", Theme = "", Region = "", Title = "";
    public int Depth = 1;
    // JD grows with the party he leads: extra health (and half as much extra defence) as a fraction, e.g. 0.4 = +40%.
    public float SummonerVigor;
    // The run's card upgrades (card id -> level) and relic effects; null for a plain fight.
    public Dictionary<string, int> CardLevels;
    public BattleRunModifiers Modifiers;
}
