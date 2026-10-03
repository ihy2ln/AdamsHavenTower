using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    public sealed class TowerBackstoryDef
    {
        public readonly string id, name, incapable;
        public readonly int might, sight, grit, charm, wit, grace, luck;
        public TowerBackstoryDef(string id, string name, string incapable,
            int might = 0, int sight = 0, int grit = 0, int charm = 0, int wit = 0, int grace = 0, int luck = 0)
        {
            this.id = id; this.name = name; this.incapable = incapable;
            this.might = might; this.sight = sight; this.grit = grit; this.charm = charm;
            this.wit = wit; this.grace = grace; this.luck = luck;
        }
    }

    public sealed class TowerStorytellerDef
    {
        public readonly string id, name, blurb;
        public readonly float delay, positive;   // event gap and positive-event chance multipliers
        public readonly int raidThreat, maxIncidents;
        public readonly bool doubleRaids;
        public TowerStorytellerDef(string id, string name, string blurb, float delay, float positive,
            int raidThreat, bool doubleRaids, int maxIncidents)
        {
            this.id = id; this.name = name; this.blurb = blurb; this.delay = delay; this.positive = positive;
            this.raidThreat = raidThreat; this.doubleRaids = doubleRaids; this.maxIncidents = maxIncidents;
        }
    }

    // TT 10.30.0: RimWorld colony depth is core, not DLC. A second trait and a backstory per newcomer, villagers
    // who give up and leave, inspirations for the content, and a choice of storyteller.
    public sealed partial class TowerRules
    {
        public static readonly TowerBackstoryDef[] Backstories = {
            new TowerBackstoryDef("farmhand", "Brookside farmhand", "", grit: 1, grace: 1),
            new TowerBackstoryDef("hedge_knight", "Hedge knight", "care", might: 2),
            new TowerBackstoryDef("herbalist", "Herbalist", "defense", wit: 1, grace: 1),
            new TowerBackstoryDef("smuggler", "River smuggler", "repair", sight: 1, luck: 1),
            new TowerBackstoryDef("miner", "Deepvein miner", "", grit: 2),
            new TowerBackstoryDef("noble", "Fallen noble", "haul", charm: 2),
            new TowerBackstoryDef("scout", "Forest scout", "", sight: 2),
            new TowerBackstoryDef("tinker", "Tinker", "care", might: 1, wit: 1),
            new TowerBackstoryDef("bard", "Wandering bard", "repair", charm: 1, luck: 1),
            new TowerBackstoryDef("watchman", "Old watchman", "", might: 1, sight: 1)
        };

        // Balanced is the storyteller the Tower always had; calm and chaotic only reshape its pool and pace.
        public static readonly TowerStorytellerDef[] Storytellers = {
            new TowerStorytellerDef("calm", "Lys the Hearthkeeper", "Long quiet spells, kinder visitors, raids stay small.",
                1.4f, 1.3f, 35, false, 2),
            new TowerStorytellerDef("balanced", "The Silverbrook Chronicler", "The Tower's usual rhythm: trouble grows with the Tower.",
                1f, 1f, 35, true, 2),
            new TowerStorytellerDef("chaotic", "The Briar's Whim", "Sooner, stranger, harder. Raiders come early and in numbers.",
                0.75f, 1f, 25, true, 3)
        };

        public const float LeaveWarnSeconds = 540f, LeaveSeconds = 1080f;
        public const float InspireAfter = 720f, InspireLength = 240f;

        public static TowerBackstoryDef Backstory(string id)
        {
            foreach (var def in Backstories) if (def.id == id) return def;
            return null;
        }

        public static TowerStorytellerDef StorytellerDef(string id)
        {
            foreach (var def in Storytellers) if (def.id == id) return def;
            return Storytellers[1];
        }

        public TowerStorytellerDef Storyteller { get { return StorytellerDef(State.storyteller); } }

        // GDD 9.3: two incidents at once until Heart rank C, three from C; a chaotic storyteller allows one more.
        public int MaxActiveIncidents() { return Storyteller.maxIncidents + (State.heartRank >= 4 ? 1 : 0); }

        public string SetStoryteller(string id)
        {
            var def = System.Array.Find(Storytellers, s => s.id == id);
            if (def == null) return "Unknown storyteller.";
            State.storyteller = id;
            Note(def.name + " now tells the Tower's story.");
            return null;
        }

        public string CycleStoryteller()
        {
            int index = System.Array.FindIndex(Storytellers, s => s.id == Storyteller.id);
            return SetStoryteller(Storytellers[(index + 1) % Storytellers.Length].id);
        }

        // ---- Traits and backstories ------------------------------------------------------------------------

        public static string Incapable(TowerResident resident)
        {
            var def = resident == null ? null : Backstory(resident.backstory);
            return def == null ? "" : def.incapable;
        }

        public static string TraitLine(TowerResident resident)
        {
            if (resident == null) return "";
            string text = string.IsNullOrEmpty(resident.trait) ? "No trait" : resident.trait;
            if (!string.IsNullOrEmpty(resident.trait2)) text += ", " + resident.trait2;
            var story = Backstory(resident.backstory);
            if (story != null) text += "   /   " + story.name + (story.incapable.Length > 0 ? " (no " + story.incapable + ")" : "");
            return text;
        }

        private static bool Clashes(string a, string b)
        {
            return a == b || a == "Gregarious" && b == "Solitary" || a == "Solitary" && b == "Gregarious";
        }

        // Newcomers (Gate recruits, grown children) gain a second trait and a backstory from the colony stream.
        // Founders, summons and checkpoint residents keep the one trait they always had.
        private void GiveDepth(TowerResident resident)
        {
            if (resident == null || resident.origin == "body" || !string.IsNullOrEmpty(resident.backstory)) return;
            int start = Mathf.Min(Traits.Length - 1, (int)(ColonyRandom01() * Traits.Length));
            for (int i = 0; i < Traits.Length; i++)
            {
                string pick = Traits[(start + i) % Traits.Length];
                if (!Clashes(resident.trait, pick)) { resident.trait2 = pick; break; }
            }
            var story = Backstories[Mathf.Min(Backstories.Length - 1, (int)(ColonyRandom01() * Backstories.Length))];
            resident.backstory = story.id;
            resident.might = Mathf.Min(10, resident.might + story.might);
            resident.sight = Mathf.Min(10, resident.sight + story.sight);
            resident.grit = Mathf.Min(10, resident.grit + story.grit);
            resident.charm = Mathf.Min(10, resident.charm + story.charm);
            resident.wit = Mathf.Min(10, resident.wit + story.wit);
            resident.grace = Mathf.Min(10, resident.grace + story.grace);
            resident.luck = Mathf.Min(10, resident.luck + story.luck);
            if (story.incapable.Length > 0) ForcePriority(resident, story.incapable, 0);
        }

        private static void ForcePriority(TowerResident resident, string job, int value)
        {
            switch (job)
            {
                case "haul": resident.priorityHaul = value; break;
                case "repair": resident.priorityRepair = value; break;
                case "fire": resident.priorityFire = value; break;
                case "care": resident.priorityCare = value; break;
                case "defense": resident.priorityDefense = value; break;
            }
        }

        // ---- Leaving and inspiration -----------------------------------------------------------------------

        // Villagers who walked in through the Gate (or grew up here) can walk out of it. Heroes, Celestium Bodies and
        // the named residents the Heart summoned (they carry a roster id) are bound to the Heart.
        public static bool CanLeave(TowerResident resident)
        {
            return resident != null && resident.origin == "villager" && string.IsNullOrEmpty(resident.unitId) &&
                resident.ageStage == 0 && string.IsNullOrEmpty(resident.posting);
        }

        private readonly List<TowerResident> departing = new List<TowerResident>();

        // Live play only (TickLife): misery builds toward leaving, lasting contentment toward an inspiration.
        private void TickDepth(TowerResident resident, float dt)
        {
            if (resident.inspirationSeconds > 0)
            {
                resident.inspirationSeconds = Mathf.Max(0, resident.inspirationSeconds - dt);
                if (resident.inspirationSeconds <= 0) resident.inspiration = "";
            }
            if (CanLeave(resident))
            {
                float before = resident.leaveSeconds;
                if (resident.happiness < 25 || resident.breakSeconds > 0) resident.leaveSeconds += dt;
                else if (resident.happiness >= 40) resident.leaveSeconds = Mathf.Max(0, resident.leaveSeconds - 0.5f * dt);
                if (before < LeaveWarnSeconds && resident.leaveSeconds >= LeaveWarnSeconds)
                {
                    Note(resident.name + " is talking about leaving the Tower. Lift their mood soon.");
                    Emit("leaving", resident.currentRoom, resident.id, resident.name);
                }
                if (resident.leaveSeconds >= LeaveSeconds && !departing.Contains(resident)) departing.Add(resident);
            }
            if (resident.breakSeconds <= 0 && resident.happiness >= 85 && !resident.downed)
            {
                resident.contentSeconds += dt;
                if (resident.contentSeconds >= InspireAfter && resident.inspirationSeconds <= 0) Inspire(resident);
            }
            else resident.contentSeconds = Mathf.Max(0, resident.contentSeconds - dt);
        }

        public static string InspirationFor(TowerResident resident)
        {
            string trait = resident.trait;
            if (trait == "Kind" || trait == "Gregarious" || trait == "Careful") return "care";
            if (trait == "Brave" || trait == "Solitary") return "guard";
            return "work";
        }

        public static string InspirationLabel(string kind)
        {
            return kind == "care" ? "Inspired to heal" : kind == "guard" ? "Inspired to guard" : "Work frenzy";
        }

        private void Inspire(TowerResident resident)
        {
            resident.contentSeconds = 0;
            resident.inspiration = InspirationFor(resident);
            resident.inspirationSeconds = InspireLength;
            Note(resident.name + " is inspired: " + InspirationLabel(resident.inspiration).ToLowerInvariant() + ".");
            Bump("inspired");
            Emit("inspired", resident.currentRoom, resident.id, InspirationLabel(resident.inspiration));
        }

        public static float InspirationBonus(TowerResident resident, string kind)
        {
            return resident != null && resident.inspirationSeconds > 0 && resident.inspiration == kind ? 1.5f : 1f;
        }

        private void FlushDepartures()
        {
            foreach (var resident in departing) Depart(resident);
            departing.Clear();
        }

        public void Depart(TowerResident resident)
        {
            if (resident == null || !State.residents.Contains(resident)) return;
            var partner = Resident(resident.familyPartnerId);
            if (partner != null && partner.familyPartnerId == resident.id) partner.familyPartnerId = 0;
            State.bonds.RemoveAll(b => b.a == resident.id || b.b == resident.id);
            bondIndex = null;
            int where = resident.currentRoom > 0 ? resident.currentRoom : resident.homeRoom;
            State.residents.Remove(resident);
            Note(resident.name + " gave up on the Tower and walked out through the Gate.");
            Bump("departed");
            Emit("depart", where, 0, resident.name);
        }

        // ---- Thoughts the depth layer adds ------------------------------------------------------------------

        private void AddDepthThoughts(TowerResident resident, List<TowerThought> list)
        {
            if (resident.inspirationSeconds > 0) list.Add(new TowerThought(InspirationLabel(resident.inspiration), 6));
        }
    }
}
