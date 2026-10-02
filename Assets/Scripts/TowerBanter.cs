using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Short lines the party says on the expedition map, picked by situation. Each fighter has a few lines per mood;
    // a situation with no line for the chosen speaker falls back to the general pool.
    // These are first-draft lines written without character bios; rewrite freely, the keys are all the code needs.
    public static class TowerBanter
    {
        public const string Night = "night", Rain = "rain", Fog = "fog", Threat = "threat", Hurt = "hurt", Camp = "camp",
            Lair = "lair", Road = "road", Wild = "wild";

        private static readonly Dictionary<string, Dictionary<string, string[]>> Lines = new Dictionary<string, Dictionary<string, string[]>>
        {
            { "kaela", new Dictionary<string, string[]> {
                { Night, new[] { "Stay close. I can't guard what I can't see.", "The dark doesn't scare me. What's in it might." } },
                { Rain, new[] { "Rain's on our side. Nothing tracks us through this." } },
                { Threat, new[] { "Something's pacing us. Shields up.", "Whatever it is, it goes through me first." } },
                { Hurt, new[] { "I'm fine. Keep walking. ...I'm mostly fine." } },
                { Camp, new[] { "First watch is mine. Sleep." } },
                { Lair, new[] { "That's the lair. Breathe, check your gear, then we go." } },
                { Wild, new[] { "Eyes on the treeline.", "Quiet. Too quiet." } } } },
            { "ghislaine", new Dictionary<string, string[]> {
                { Night, new[] { "Good. The fire will look brighter." } },
                { Fog, new[] { "Can't see a thing. Let them come to me, then." } },
                { Threat, new[] { "Let them come. I'm getting bored.", "Finally, something worth the walk." } },
                { Hurt, new[] { "Just a scratch. A big scratch." } },
                { Camp, new[] { "Who's cooking? Not me. Never me." } },
                { Lair, new[] { "Now THAT is a door worth kicking in." } },
                { Road, new[] { "A road at last. My boots thank it." } } } },
            { "elara", new Dictionary<string, string[]> {
                { Night, new[] { "Starlight's enough for me. Follow my steps." } },
                { Fog, new[] { "I can hear further than I can see. Hush." } },
                { Threat, new[] { "Tracks. Fresh ones. Bigger than ours." } },
                { Wild, new[] { "There's a path here if you know how to look.", "Birds went quiet to the east." } },
                { Lair, new[] { "I count one way in. I don't like counting one." } },
                { Road, new[] { "Old road. Someone kept it clear, once." } } } },
            { "helda", new Dictionary<string, string[]> {
                { Hurt, new[] { "Sit. Let me see that before you argue.", "Nobody falls on my watch. Hold still." } },
                { Rain, new[] { "Cold rain. Keep your hands warm, you'll need them." } },
                { Camp, new[] { "Eat something warm. Healing starts in the belly." } },
                { Night, new[] { "Frost by morning. Wrap up." } },
                { Wild, new[] { "Slow down. We arrive together or not at all." } } } },
            { "daisy", new Dictionary<string, string[]> {
                { Wild, new[] { "Are we there yet? Kidding! ...Are we?", "I bet there's treasure over that hill." } },
                { Threat, new[] { "Ooh, something wants a fight!", "I've been waiting all day for this." } },
                { Rain, new[] { "Rain! My hair's going to be a disaster." } },
                { Camp, new[] { "Marshmallows. Tell me someone packed marshmallows." } },
                { Lair, new[] { "Biggest monster, biggest loot. That's the rule, right?" } },
                { Road, new[] { "Race you to the next bend!" } } } },
            { "clarity", new Dictionary<string, string[]> {
                { Night, new[] { "I'll keep a light burning. Small, so it can't be seen far." } },
                { Fog, new[] { "The fog thins near the old stones. Keep left." } },
                { Threat, new[] { "The forest has noticed us. Let's not linger." } },
                { Hurt, new[] { "We should rest before we push on. Please." } },
                { Lair, new[] { "Whatever waits below has been waiting a long time." } },
                { Wild, new[] { "Every map I've seen of this place is wrong. Fascinating." } } } },
        };

        // Who speaks and what, or null when nobody has a line. Picks a living fighter that has one for the mood.
        public static string Pick(List<string> party, List<int> hp, string mood, System.Random rng)
        {
            var speakers = new List<string>();
            for (int i = 0; i < party.Count; i++)
            {
                if (i < hp.Count && hp[i] <= 0) continue;
                Dictionary<string, string[]> own;
                if (Lines.TryGetValue(party[i], out own) && own.ContainsKey(mood)) speakers.Add(party[i]);
            }
            if (speakers.Count == 0) return null;
            string who = speakers[rng.Next(speakers.Count)];
            var lines = Lines[who][mood];
            return Name(who) + ": \"" + lines[rng.Next(lines.Length)] + "\"";
        }

        private static string Name(string id) { return id.Length == 0 ? id : char.ToUpperInvariant(id[0]) + id.Substring(1); }

        // The mood of the moment, most pressing first.
        public static string Mood(TowerRules rules)
        {
            var run = rules.Run;
            if (run == null) return Wild;
            int hurt = 0; foreach (var h in run.hp) if (h > 0 && h < 40) hurt++;
            if (hurt > 0) return Hurt;
            if (run.threat >= TowerRules.ThreatWarn) return Threat;
            var map = rules.Overworld;
            if (map != null && map.Lair != null && Mathf.Abs(map.Lair.x - run.cx) <= 6 && Mathf.Abs(map.Lair.y - run.cy) <= 6
                && !run.cleared.Contains(map.Lair.id)) return Lair;
            var node = rules.RunLayout != null ? rules.RunLayout.Node(run.at) : null;
            if (node != null && node.kind == "camp") return Camp;
            string weather = rules.Weather;
            if (weather == "rain") return Rain;
            if (weather == "fog") return Fog;
            if (TowerRules.NightLevel(rules.RunHour()) > 0.5f) return Night;
            if (rules.CellIsRoad(run.cx, run.cy)) return Road;
            return Wild;
        }
    }
}
