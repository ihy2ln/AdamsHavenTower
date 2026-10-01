using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Forest fog, roads and the threat meter (BATTLE_MODE_GDD.md sections 4 and 10).
    // Fog: walking reveals places within a few trail hops. Roads: every trail walked becomes road,
    // which costs half the rations and raises less threat. Threat: rises with travel, fights and night;
    // camp rest and cleared places lower it; when it fills, the forest stirs and swallows stretches of road.
    public sealed partial class TowerRules
    {
        public const int ThreatMax = 100, ThreatWarn = 75, ThreatNewTrail = 8, ThreatRoad = 3, ThreatNight = 4,
            ThreatBattle = 4, ThreatPoiCleared = 15, ThreatCampRest = 25, ThreatAfterStir = 40, RoadLostOnStir = 2,
            MaxRevealRadius = 3;

        public static string RoadKey(string a, string b) { return string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a; }

        public bool OnRoad(string a, string b) { var run = Run; return run != null && run.road.Contains(RoadKey(a, b)); }

        public bool PartyHasTrait(string trait)
        {
            var run = Run;
            if (run == null) return false;
            foreach (var id in run.party)
            {
                var hero = HeroResident(id);
                if (hero != null && hero.trait == trait) return true;
            }
            return false;
        }

        // Trail hops revealed around the party. A Curious scout sees one further (at night only with a Night Owl
        // along); a level 3 Guild Hall's maps add one more.
        public int RevealRadius
        {
            get
            {
                int radius = 1;
                if (PartyHasTrait("Curious") && (IsDaylight(Hour()) || PartyHasTrait("Night Owl"))) radius++;
                if (GuildLevel >= 3) radius++;
                return Mathf.Min(MaxRevealRadius, radius);
            }
        }

        private void RevealFrom(string nodeId)
        {
            var run = Run; var layout = RunLayout;
            if (run == null || layout == null || layout.Node(nodeId) == null) return;
            var frontier = new List<string> { nodeId };
            if (!run.revealed.Contains(nodeId)) run.revealed.Add(nodeId);
            for (int hop = 0; hop < RevealRadius; hop++)
            {
                var next = new List<string>();
                foreach (var at in frontier)
                    foreach (var node in layout.nodes)
                        if (!run.revealed.Contains(node.id) && layout.Linked(at, node.id)) { run.revealed.Add(node.id); next.Add(node.id); }
                frontier = next;
            }
        }

        // Saves from before fog was tracked: everything already walked reveals again.
        private void RebuildFog()
        {
            var run = Run;
            run.revealed.Clear();
            foreach (var v in run.visited.ToArray()) RevealFrom(v);
        }

        // One trail step on the forest map. Returns true when a ration is due: always on a new trail,
        // every second trip on road.
        private bool WalkTrail(string from, string to)
        {
            var run = Run;
            bool road = OnRoad(from, to);
            if (!road) run.road.Add(RoadKey(from, to));
            bool pay = !road || ++run.roadSteps % 2 == 0;
            RaiseThreat((road ? ThreatRoad : ThreatNewTrail) + (IsDaylight(Hour()) ? 0 : ThreatNight));
            return pay;
        }

        public void RaiseThreat(int amount)
        {
            var run = Run;
            if (run == null || amount <= 0) return;
            int before = run.threat;
            run.threat = Mathf.Min(ThreatMax, run.threat + amount);
            if (before < ThreatWarn && run.threat >= ThreatWarn && run.threat < ThreatMax)
                Note("The forest grows restless. Rest at camp or clear a place before it stirs.");
            if (run.threat >= ThreatMax) ForestStirs();
        }

        public void LowerThreat(int amount)
        {
            var run = Run;
            if (run == null || amount <= 0) return;
            run.threat = Mathf.Max(0, run.threat - amount);
        }

        // Full threat: the trees close over road that is not anchored by the camp, a cleared place or the party.
        private void ForestStirs()
        {
            var run = Run; var layout = RunLayout;
            var candidates = new List<string>();
            foreach (var key in run.road)
            {
                var ends = key.Split('|');
                bool anchored = false;
                foreach (var end in ends)
                    if (end == layout.entrance || end == run.at || run.cleared.Contains(end)) anchored = true;
                if (!anchored) candidates.Add(key);
            }
            var rng = new TowerRng(TowerForestLayouts.Hash(run.layout + ":stir", run.seed + run.visited.Count * 31 + run.road.Count));
            int lost = 0;
            while (lost < RoadLostOnStir && candidates.Count > 0)
            {
                int i = rng.Next(candidates.Count);
                run.road.Remove(candidates[i]);
                candidates.RemoveAt(i);
                lost++;
            }
            run.threat = ThreatAfterStir;
            // Full threat also brings the forest down on the party (P2 events): fight or flee.
            if (TraversalEvents && TowerEvents.Get(TowerEvents.AmbushId) != null && !AmbushPending) StartEvent(TowerEvents.AmbushId);
            Note(lost == 0 ? "The forest stirs, but your roads hold." :
                "The forest stirs: " + lost + (lost == 1 ? " stretch" : " stretches") + " of road vanish under the trees.");
        }
    }
}
