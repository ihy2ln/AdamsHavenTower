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

    private static BattleUnit Monster(string id, string name, BattleElement element, int rank, int role,
        int lane, bool large = false)
    {
        float[][] shape = { new [] {1.35f,1.25f,.60f,1.05f,.80f}, new [] {.72f,1.10f,.70f,.80f,1.45f},
            new [] {.85f,.60f,1.50f,.85f,1.05f}, new [] {1.20f,.80f,.85f,1.50f,.85f} };
        float[] s = shape[role];
        float scale = Tier[rank] / Tier[3];
        int hp = (int)(110f * s[0] * scale);
        return new BattleUnit { Id = id + "_" + lane, Species = id, Name = name,
            Art = "FieldModels/" + id, Element = element, Enemy = true,
            Role = role == 3 ? BattleRole.Tank : role == 2 ? BattleRole.Support : BattleRole.Dps,
            InnateTaunt = role == 3, Lane = lane, MaxHp = hp, Hp = hp,
            Attack = 15f * s[1] * scale, Magic = 13f * s[2] * scale,
            Defense = 10f * s[3] * scale, Resistance = 10f * s[3] * scale,
            Speed = 10f * s[4], CritRate = .10f, CritDamage = 1.60f };
    }

    public static List<BattleUnit> Encounter(int floor)
    {
        int depth = Math.Abs(floor);
        if (depth >= 8)
            return new List<BattleUnit> { Monster("eclipse_core_golem", "Eclipse Core Golem", BattleElement.Dark, 8, 0, 0, true),
                Monster("moonstone_ravager", "Moonstone Ravager", BattleElement.Water, 4, 0, 1, true),
                Monster("obsidian_talon", "Obsidian Talon", BattleElement.Fire, 3, 1, 2) };
        if (depth >= 3)
            return new List<BattleUnit> { Monster("thorncrystal_stalker", "Thorncrystal Stalker", BattleElement.Earth, 2, 1, 0),
                Monster("obsidian_talon", "Obsidian Talon", BattleElement.Fire, 3, 1, 1),
                Monster("moonstone_ravager", "Moonstone Ravager", BattleElement.Water, 4, 0, 2, true) };
        return new List<BattleUnit> { Monster("shardling_sprout", "Shardling Sprout", BattleElement.Earth, 0, 3, 0),
            Monster("flintjaw_skitterer", "Flintjaw Skitterer", BattleElement.Fire, 0, 1, 1),
            Monster("amberhide_grazer", "Amberhide Grazer", BattleElement.Earth, 1, 0, 2) };
    }

    public static BattleUnit EnemyCommander(int floor)
    {
        if (Math.Abs(floor) < 3) return null;
        int hp = Math.Abs(floor) >= 8 ? 450 : 260;
        return new BattleUnit { Id = "enemy_commander", Name = "Vale Commander", Art = "FieldModels/enemy_summoner",
            Enemy = true, Element = BattleElement.Dark, MaxHp = hp, Hp = hp,
            Defense = 14, Resistance = 15, Speed = 11, CritRate = .1f, CritDamage = 1.6f };
    }

    public static List<BattleCard> EnemyCards(BattleUnit enemy)
    {
        string id = enemy.Species;
        List<BattleCard> cards = new List<BattleCard>();
        if (id == "shardling_sprout" || id == "amberhide_grazer" || id == "moonstone_ravager")
        {
            cards.Add(C("e_bash", "Crystal Bash", enemy.Id, 1, 1, 1.2f));
            cards.Add(C("e_guard", "Root Guard", enemy.Id, 1, 1, target: BattleTarget.Self, status: "DefenseUp", magnitude: .3f, duration: 2));
        }
        else if (id == "eclipse_core_golem")
        {
            cards.Add(C("e_cleave", "Eclipse Crush", enemy.Id, 2, 1, 2.1f));
            cards.Add(C("e_quake", "Core Quake", enemy.Id, 3, 1, 1.3f, BattleTarget.AllEnemies));
        }
        else
        {
            cards.Add(C("e_claw", "Corrupted Claw", enemy.Id, 1, 1, 1.2f));
            cards.Add(C("e_wail", "Hollow Wail", enemy.Id, 2, 1, .8f, status: "AttackDown", magnitude: .2f, duration: 2));
        }
        foreach (BattleCard card in cards) card.Element = enemy.Element;
        return cards;
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
