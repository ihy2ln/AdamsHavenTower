using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Traversal events: the event director (BATTLE_MODE_GDD.md section 5).
    // Event content is data: Resources/AdamsHaven/Events/traversal.json. Each choice rolls one check
    // (base chance, plus a bonus when a hero with the named trait is in the party) and lands on a
    // success, partial or failure outcome.
    [Serializable] public sealed class TowerEventOutcome
    {
        public string text = "";
        public int rations, firewood, tonics, hp, gold, ore, essence, threat, reveal;
        public bool ambush;                 // the party is attacked on the spot
        public string flag = "";            // remembered for later events (needsFlag)
        public bool Empty { get { return string.IsNullOrEmpty(text); } }
    }

    [Serializable] public sealed class TowerEventChoice
    {
        public string label = "", trait = "";
        public float chance = 1f, traitBonus = 0.3f;
        public int costRations, costFirewood, costTonics, costGold;
        public TowerEventOutcome success = new TowerEventOutcome(), partial = new TowerEventOutcome(),
            failure = new TowerEventOutcome();
    }

    [Serializable] public sealed class TowerEventDef
    {
        public string id = "", title = "", text = "";
        public string themes = "";          // comma list of forest themes, empty = anywhere
        public string time = "any";         // any, day, night
        public string needsFlag = "";
        public int minDepth = 1, maxDepth = 99, weight = 10;   // weight 0 = only fired directly
        public bool onRoad = true, repeat;
        public List<TowerEventChoice> choices = new List<TowerEventChoice>();
    }

    [Serializable] internal sealed class TowerEventFile { public List<TowerEventDef> events = new List<TowerEventDef>(); }

    public static class TowerEvents
    {
        public const string Path = "AdamsHaven/Events/traversal";
        public const string AmbushId = "threat_ambush", RestId = "camp_quiet_glade";

        private static List<TowerEventDef> all;

        public static List<TowerEventDef> All { get { if (all == null) Load(); return all; } }
        public static TowerEventDef Get(string id) { return string.IsNullOrEmpty(id) ? null : All.Find(e => e.id == id); }
        public static void ClearCache() { all = null; }

        private static void Load()
        {
            all = new List<TowerEventDef>();
            var text = Resources.Load<TextAsset>(Path);
            if (text == null) return;
            var file = JsonUtility.FromJson<TowerEventFile>(text.text);
            if (file == null || file.events == null) return;
            foreach (var e in file.events)
                if (e != null && e.id.Length > 0 && e.choices.Count > 0 && Get(e.id) == null) all.Add(e);
        }
    }

    public sealed partial class TowerRules
    {
        // Tests that walk the forest for other reasons switch the director off.
        public static bool TraversalEvents = true;

        public const float EventChanceTrail = 0.35f, EventChanceRoad = 0.10f, EventThreatBoost = 0.25f, EventPartialBand = 0.2f;

        public TowerEventDef PendingEvent { get { var run = Run; return run == null ? null : TowerEvents.Get(run.eventId); } }
        public bool AmbushPending { get { var run = Run; return run != null && run.ambushDepth > 0; } }

        // Null when the party is free to act on the forest map.
        public string EventBlock()
        {
            if (AmbushPending) return "Fight off the ambush first.";
            if (PendingEvent != null) return "Decide what to do first.";
            if (RewardPending) return "Choose your reward first.";
            return null;
        }

        private TowerRng EventRng(string salt)
        {
            var run = Run;
            return new TowerRng(TowerForestLayouts.Hash(run.layout + ":" + salt, run.seed + run.steps * 7919));
        }

        // After each forest step. Camp and lair are quiet; the first place next to the lair always offers a rest;
        // otherwise a new trail rolls 35% (road 10%), more with threat, never twice in a row.
        private void RollTraversalEvent(string nodeId, bool road)
        {
            var run = Run; var layout = RunLayout; var node = layout.Node(nodeId);
            run.steps++;
            if (!TraversalEvents) { run.lastStepEvent = false; return; }
            if (run.eventId.Length > 0) { run.lastStepEvent = true; return; }      // the forest already stirred
            bool fired = false;
            if (node.kind != "camp" && node.kind != "lair")
            {
                var lair = layout.nodes.Find(n => n.kind == "lair");
                if (lair != null && layout.Linked(nodeId, lair.id) && !run.eventsSeen.Contains(TowerEvents.RestId) &&
                    TowerEvents.Get(TowerEvents.RestId) != null)
                {
                    StartEvent(TowerEvents.RestId); fired = true;
                }
                else if (!run.lastStepEvent)
                {
                    var rng = EventRng("event");
                    float chance = (road ? EventChanceRoad : EventChanceTrail) + EventThreatBoost * run.threat / ThreatMax;
                    if (rng.Value() < chance)
                    {
                        var def = PickEvent(node, road, rng);
                        if (def != null) { StartEvent(def.id); fired = true; }
                    }
                }
            }
            run.lastStepEvent = fired;
        }

        public List<TowerEventDef> EventCandidates(TowerForestNode node, bool road)
        {
            var run = Run;
            int depth = RunRegion == null ? 1 : RunRegion.depth;
            bool day = IsDaylight(RunHour());
            return TowerEvents.All.FindAll(e => e.weight > 0 && depth >= e.minDepth && depth <= e.maxDepth &&
                (e.themes.Length == 0 || ("," + e.themes.Replace(" ", "") + ",").Contains("," + node.theme + ",")) &&
                (e.time == "any" || (e.time == "day") == day) && (!road || e.onRoad) &&
                (e.needsFlag.Length == 0 || run.flags.Contains(e.needsFlag)) && (e.repeat || !run.eventsSeen.Contains(e.id)));
        }

        private TowerEventDef PickEvent(TowerForestNode node, bool road, TowerRng rng)
        {
            var pool = EventCandidates(node, road);
            int total = 0;
            foreach (var e in pool) total += e.weight;
            if (total <= 0) return null;
            int pick = rng.Next(total);
            foreach (var e in pool) { pick -= e.weight; if (pick < 0) return e; }
            return null;
        }

        private void StartEvent(string id)
        {
            var run = Run;
            run.eventId = id;
            run.eventResult = "";
            if (!run.eventsSeen.Contains(id)) run.eventsSeen.Add(id);
        }

        public float ChoiceChance(TowerEventChoice choice)
        {
            float chance = choice.chance;
            if (choice.trait.Length > 0 && PartyHasTrait(choice.trait)) chance += choice.traitBonus;
            return Mathf.Clamp01(chance);
        }

        public string ChoiceBlocked(TowerEventChoice choice)
        {
            var run = Run;
            if (run.rations < choice.costRations) return "Not enough rations.";
            if (run.firewood < choice.costFirewood) return "Not enough firewood.";
            if (run.tonics < choice.costTonics) return "Not enough tonics.";
            if (HaulGold < choice.costGold) return "Not enough gold in the haul.";
            return null;
        }

        public string ResolveEvent(int index)
        {
            var run = Run; var def = PendingEvent;
            if (def == null) return "Nothing is happening.";
            if (index < 0 || index >= def.choices.Count) return "Unknown choice.";
            var choice = def.choices[index];
            string error = ChoiceBlocked(choice);
            if (error != null) return error;
            run.rations -= choice.costRations;
            run.firewood -= choice.costFirewood;
            run.tonics -= choice.costTonics;
            int owed = choice.costGold;
            foreach (var loot in run.haul) { int take = Mathf.Min(owed, loot.gold); loot.gold -= take; owed -= take; }

            float roll = EventRng("choice:" + def.id + ":" + index).Value(), chance = ChoiceChance(choice);
            TowerEventOutcome outcome; string tier;
            if (roll < chance || (choice.partial.Empty && choice.failure.Empty)) { outcome = choice.success; tier = "Success"; }
            else if (!choice.partial.Empty && (roll < chance + EventPartialBand || choice.failure.Empty)) { outcome = choice.partial; tier = "Partly"; }
            else { outcome = choice.failure; tier = "Failure"; }
            run.eventId = "";
            run.eventResult = (chance < 1f ? tier + ": " : "") + outcome.text;
            ApplyOutcome(outcome, def);
            return null;
        }

        private void ApplyOutcome(TowerEventOutcome o, TowerEventDef def)
        {
            var run = Run;
            run.rations = Mathf.Max(0, run.rations + o.rations);
            run.firewood = Mathf.Max(0, run.firewood + o.firewood);
            run.tonics = Mathf.Max(0, run.tonics + o.tonics);
            if (o.hp != 0)
                for (int i = 0; i < run.hp.Count; i++)
                    if (run.hp[i] > 0) run.hp[i] = Mathf.Clamp(run.hp[i] + o.hp, 1, 100);
            if (o.gold > 0 || o.ore > 0 || o.essence > 0)
                run.haul.Add(new TowerLoot { name = def.title, gold = o.gold, ore = o.ore, essence = o.essence });
            if (o.reveal > 0) RevealHidden(o.reveal);
            if (o.flag.Length > 0 && !run.flags.Contains(o.flag)) run.flags.Add(o.flag);
            if (o.ambush) run.ambushDepth = AmbushDepth;
            Note(def.title + ": " + o.text);
            if (o.threat > 0) RaiseThreat(o.threat); else LowerThreat(-o.threat);
        }

        // Scouting: lifts the fog from the nearest hidden places along known trails.
        private void RevealHidden(int count)
        {
            var run = Run; var layout = RunLayout;
            for (int n = 0; n < count; n++)
            {
                var next = layout.nodes.Find(node => !run.revealed.Contains(node.id) &&
                    run.revealed.Exists(seen => layout.Linked(seen, node.id)));
                if (next == null) return;
                run.revealed.Add(next.id);
            }
        }

        public string DismissEventResult()
        {
            var run = Run;
            if (run == null) return "No expedition.";
            run.eventResult = "";
            return null;
        }

        public int AmbushDepth { get { return Mathf.Max(1, RunRegion == null ? 1 : RunRegion.depth); } }

        // Ambushers come from the nearest place's theme.
        public BattleEncounterSpec AmbushEncounter
        {
            get
            {
                var run = Run;
                string theme = "";
                if (GridRun) { var near = NearestPoi(run.cx, run.cy); if (near != null) theme = near.theme; }
                else { var node = RunLayout.Node(run.at); if (node != null) theme = node.theme; }
                return new BattleEncounterSpec { Depth = AmbushDepth, Kind = "ambush", Theme = theme, Region = run.region,
                    Seed = (int)(TowerForestLayouts.Hash("ambush:" + run.steps + ":" + run.cx + "," + run.cy, run.seed) & 0x7fffffff) | 1 };
            }
        }

        // hp: percent per party member after the ambush fight (same order as run.party).
        public string ResolveAmbush(bool won, List<int> hp)
        {
            var run = Run;
            if (!AmbushPending) return "No ambush.";
            if (hp != null && hp.Count == run.hp.Count) for (int i = 0; i < hp.Count; i++) run.hp[i] = Mathf.Clamp(hp[i], 0, 100);
            run.ambushDepth = 0;
            run.eventResult = "";
            PassBattleTime();
            if (!PartyAlive) return EndExpedition(true);
            if (won)
            {
                if (run.dungeonPoi.Length == 0) run.floor = 0;      // the floor only means something inside a dungeon
                var loot = RollLoot(EventRng("ambush"), 1, "Ambush spoils");
                run.haul.Add(loot);
                run.battlesWon++;
                AwardExpeditionXp(15);
                AfterWin("ambush", EventRng("ambush reward"));
                LowerThreat(10);
                Note("The ambush was beaten back: " + LootLine(loot) + ".");
            }
            else
            {
                if (run.rations > 0) run.rations--;
                Note("The party scattered and regrouped, dropping a ration.");
            }
            return null;
        }
    }
}
