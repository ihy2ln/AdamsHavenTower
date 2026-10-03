using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    [Serializable] public sealed class TowerCounter
    {
        public string key;
        public int value;
    }

    [Serializable] public sealed class TowerGoal
    {
        public string id;
        public int baseline;
        public bool claimed;
    }

    public sealed class TowerThought
    {
        public readonly string label;
        public readonly float value;
        public TowerThought(string label, float value) { this.label = label; this.value = value; }
    }

    public sealed class TowerGoalDef
    {
        public readonly string id, title, counter;
        public readonly int target, gold, celestium, tonics, sigils;
        public TowerGoalDef(string id, string title, string counter, int target,
            int gold, int celestium = 0, int tonics = 0, int sigils = 0)
        {
            this.id = id; this.title = title; this.counter = counter; this.target = target;
            this.gold = gold; this.celestium = celestium; this.tonics = tonics; this.sigils = sigils;
        }
        public string Reward
        {
            get
            {
                string text = "";
                if (gold > 0) text += gold + "g ";
                if (celestium > 0) text += celestium + "C ";
                if (tonics > 0) text += tonics + " tonics ";
                if (sigils > 0) text += sigils + " Sigils";
                return text.Trim();
            }
        }
    }

    // A structured-life layer over the room economy: temperament, moods, a storyteller and goals.
    public sealed partial class TowerRules
    {
        public static readonly string[] Traits = {
            "Industrious", "Careful", "Brave", "Kind", "Gregarious",
            "Solitary", "Night Owl", "Stout", "Curious", "Frugal"
        };

        public static readonly TowerGoalDef[] GoalPool = {
            // Each goal also pays 2 to 4 Sigils (TowerSigils.cs income plan).
            new TowerGoalDef("harvest", "Collect 5 harvests", "collect", 5, 90, 0, 0, 2),
            new TowerGoalDef("builder", "Build 3 rooms", "build", 3, 140, 2, 0, 3),
            new TowerGoalDef("firefighter", "Resolve 2 incidents", "resolved", 2, 60, 4, 0, 3),
            new TowerGoalDef("welcome", "Welcome 2 wanderers", "recruit", 2, 80, 0, 2, 2),
            new TowerGoalDef("rushing", "Rush 3 rooms successfully", "rush_ok", 3, 70, 0, 0, 2),
            new TowerGoalDef("training", "Finish 3 training sessions", "trained", 3, 100, 3, 0, 3),
            new TowerGoalDef("rescuer", "Rescue or heal 2 residents", "healed", 2, 50, 0, 3, 2),
            new TowerGoalDef("explorer", "Return 2 expeditions", "expedition", 2, 120, 2, 0, 4),
            new TowerGoalDef("upgrader", "Upgrade a room", "upgrade", 1, 160, 0, 0, 3),
            new TowerGoalDef("family", "Welcome a family child", "child", 1, 90, 5, 0, 4)
        };

        public const int ActiveGoals = 3;

        // Presentation hook: FX and audio observe what the simulation already decided.
        public Action<string, int, int, string> Signal;

        private void Emit(string kind, int roomUid, int residentId, string text)
        { if (Signal != null) Signal(kind, roomUid, residentId, text); }

        public int Counter(string key)
        {
            var entry = State.counters.Find(c => c.key == key);
            return entry == null ? 0 : entry.value;
        }

        private void Bump(string key, int amount = 1)
        {
            var entry = State.counters.Find(c => c.key == key);
            if (entry == null) State.counters.Add(new TowerCounter { key = key, value = amount });
            else entry.value += amount;
            CompleteGoals();
        }

        public static TowerGoalDef GoalDef(string id)
        {
            foreach (var def in GoalPool) if (def.id == id) return def;
            return null;
        }

        public int GoalProgress(TowerGoal goal)
        {
            var def = GoalDef(goal.id);
            return def == null ? 0 : Mathf.Min(def.target, Counter(def.counter) - goal.baseline);
        }

        public bool GoalComplete(TowerGoal goal)
        {
            var def = GoalDef(goal.id);
            return def != null && GoalProgress(goal) >= def.target;
        }

        private void RefillGoals()
        {
            if (State.goals == null) State.goals = new List<TowerGoal>();
            State.goals.RemoveAll(g => g.claimed);
            int guard = 0;
            while (State.goals.Count < ActiveGoals && guard++ < GoalPool.Length * 2)
            {
                var def = GoalPool[(State.goalsClaimed + State.goals.Count + guard - 1) % GoalPool.Length];
                if (State.goals.Exists(g => g.id == def.id)) continue;
                State.goals.Add(new TowerGoal { id = def.id, baseline = Counter(def.counter) });
            }
        }

        private void CompleteGoals()
        {
            foreach (var goal in State.goals)
                if (!goal.claimed && GoalComplete(goal) && !State.notifiedGoals.Contains(goal.id))
                {
                    State.notifiedGoals.Add(goal.id);
                    Note("Goal ready to claim: " + GoalDef(goal.id).title + ".");
                    Emit("goal", 0, 0, GoalDef(goal.id).title);
                }
        }

        public string ClaimGoal(int index)
        {
            if (index < 0 || index >= State.goals.Count) return "No such goal.";
            var goal = State.goals[index];
            if (goal.claimed || !GoalComplete(goal)) return "That goal is not finished.";
            var def = GoalDef(goal.id);
            State.gold += def.gold;
            State.celestium += def.celestium;
            State.tonics = Mathf.Min(30, State.tonics + def.tonics);
            GrantSigils(def.sigils);
            goal.claimed = true;
            State.notifiedGoals.Remove(goal.id);
            State.goalsClaimed++;
            Note("Claimed " + def.Reward + " for: " + def.title + ".");
            Emit("reward", 0, 0, def.Reward);
            RefillGoals();
            return null;
        }

        // ---- Clock -----------------------------------------------------------------

        // The day begins at dawn (06:00) so the first minutes of a run are daylight.
        public float Hour()
        { return (6f + State.clock / DaySeconds * 24f) % 24f; }

        public static bool IsDaylight(float hour) { return hour >= 6f && hour < 19f; }

        public bool Sleeping(TowerResident resident)
        {
            float hour = Hour();
            switch (resident.schedule)
            {
                case "day": return hour >= 22f || hour < 5.5f;
                case "night": return hour >= 10f && hour < 17.5f;
                default: return false;
            }
        }

        // ---- Temperament -----------------------------------------------------------

        public static bool HasTrait(TowerResident resident, string trait)
        { return resident != null && (resident.trait == trait || resident.trait2 == trait); }

        public void RollTemperament(TowerResident resident)
        {
            resident.trait = Traits[Mathf.Min(Traits.Length - 1, (int)(Random01() * Traits.Length))];
            resident.schedule = resident.trait == "Night Owl" ? "night" : "day";
        }

        public string SetSchedule(int residentId, string schedule)
        {
            var resident = Resident(residentId);
            if (resident == null || resident.ageStage != 0) return "Choose an adult resident.";
            if (resident.origin == "body") return "Celestium Bodies do not sleep.";
            if (schedule != "day" && schedule != "night" && schedule != "flexible")
                return "Unknown schedule.";
            resident.schedule = schedule;
            Note(resident.name + " now keeps a " + schedule + " schedule.");
            return null;
        }

        // ---- Mood ------------------------------------------------------------------

        // Filled for the length of one TickNeeds pass (nothing moves or is built during it); null otherwise.
        private Dictionary<int, int> floorHeadcount;
        private int cachedAmenities = -1;

        private int AmenityKinds()
        {
            if (cachedAmenities >= 0) return cachedAmenities;
            int kinds = 0;
            foreach (string type in new[] { "market", "frosted_mug", "guild_hall" })
                if (State.rooms.Exists(r => r.type == type)) kinds++;
            return kinds;
        }

        public List<TowerThought> Thoughts(TowerResident resident)
        {
            var list = new List<TowerThought>();
            if (resident == null || resident.origin == "body" || resident.ageStage != 0 && resident.ageStage != 1)
                return list;
            if (resident.hunger < 15) list.Add(new TowerThought("Starving", -25));
            else if (resident.hunger < 30) list.Add(new TowerThought("Hungry", -14));
            else if (resident.hunger > 70) list.Add(new TowerThought("Well fed", 4));
            if (resident.thirst < 15) list.Add(new TowerThought("Parched", -25));
            else if (resident.thirst < 30) list.Add(new TowerThought("Thirsty", -14));
            else if (resident.thirst > 70) list.Add(new TowerThought("Hydrated", 4));
            if (resident.rest < 20) list.Add(new TowerThought("Exhausted", -14));
            else if (resident.rest < 35) list.Add(new TowerThought("Tired", -8));
            else if (resident.rest > 80) list.Add(new TowerThought("Well rested", 4));
            if (Researched("SET-1")) list.Add(new TowerThought("Shared meals", 3));
            if (Researched("SET-2")) list.Add(new TowerThought("Cozy lighting", 3));
            if (State.firewood <= 0) list.Add(new TowerThought("Cold hearth", -12));
            else list.Add(new TowerThought("Warm hearth", 3));

            var home = Room(resident.homeRoom);
            if (home == null) list.Add(new TowerThought("No bed of their own", -15));
            else list.Add(new TowerThought(home.level >= 3 ? "Fine home" : home.level == 2 ? "Comfortable home" :
                "Cozy home", 4 * home.level));
            int amenities = AmenityKinds();
            if (amenities > 0) list.Add(new TowerThought("Tower amenities", Mathf.Min(8, amenities * 4)));

            if (resident.familyPartnerId > 0) list.Add(new TowerThought("With family", 10));
            int neighbours = 0;
            var here = Room(resident.currentRoom);
            if (here != null && floorHeadcount != null)
            {
                floorHeadcount.TryGetValue(here.floor, out neighbours);
                neighbours--;   // the count includes this resident
            }
            else if (here != null)
                foreach (var other in State.residents)
                    if (other.id != resident.id && other.origin != "body" && other.currentRoom > 0 &&
                        Room(other.currentRoom) != null && Room(other.currentRoom).floor == here.floor) neighbours++;
            if (HasTrait(resident, "Gregarious"))
                list.Add(new TowerThought(neighbours >= 2 ? "Among company" : "Lonely", neighbours >= 2 ? 6 : -6));
            else if (HasTrait(resident, "Solitary"))
                list.Add(new TowerThought(neighbours >= 4 ? "Crowded" : "Quiet floor", neighbours >= 4 ? -6 : 4));
            else if (neighbours >= 1) list.Add(new TowerThought("Friendly faces", 3));
            if (HasTrait(resident, "Kind")) list.Add(new TowerThought("Kind heart", 3));
            if (HasTrait(resident, "Night Owl"))
                list.Add(new TowerThought("Night owl", IsDaylight(Hour()) ? -2 : 4));

            if (State.incidents.Count > 0)
            {
                bool mine = State.incidents.Exists(i => i.roomUid == resident.currentRoom);
                list.Add(new TowerThought(mine ? "Danger nearby" : "Tower under threat", mine ? -15 : -6));
            }
            if (State.heartHp < heartMaxHp() * 0.4f) list.Add(new TowerThought("The Heart is failing", -10));
            if (resident.injury > 10) list.Add(new TowerThought("Injured", -Mathf.Min(12, resident.injury / 8f)));
            if (resident.illness > 10) list.Add(new TowerThought("Ill", -8));

            var job = Room(resident.jobRoom);
            if (resident.ageStage == 0)
            {
                if (job == null) list.Add(new TowerThought("Nothing to do", -4));
                else if (MatchScore(resident, job) >= 6f) list.Add(new TowerThought("Doing what they do best", 5));
                if (resident.tool >= 1) list.Add(new TowerThought("Good tools", 2));
            }
            if (State.festivalSeconds > 0) list.Add(new TowerThought("Harvest festival", 16));
            if (resident.ageStage == 0) AddSocialThoughts(resident, list);
            AddDepthThoughts(resident, list);
            AddDistrictThoughts(resident, list);
            AddOutpostThoughts(resident, list);
            return list;
        }

        private float heartMaxHp()
        { return HeartMaxHp(State.heartRank); }

        public float MoodTarget(TowerResident resident)
        {
            float mood = 50;
            foreach (var thought in Thoughts(resident)) mood += thought.value;
            return Mathf.Clamp(mood, 0, 100);
        }

        public string MoodLabel(TowerResident resident)
        {
            if (resident.breakSeconds > 0) return BreakLabel(resident.breakKind);
            float m = resident.happiness;
            return m >= 80 ? "Delighted" : m >= 60 ? "Content" : m >= 40 ? "Uneasy" : m >= 20 ? "Unhappy" : "Breaking";
        }

        // ---- Trait production modifiers -------------------------------------------

        public float TraitWorkMultiplier(TowerResident resident)
        {
            float mult = 1;
            if (HasTrait(resident, "Industrious")) mult *= 1.10f;
            if (HasTrait(resident, "Night Owl") && !IsDaylight(Hour())) mult *= 1.08f;
            mult *= InspirationBonus(resident, "work");
            if (resident.breakSeconds > 0) mult = 0;
            return mult;
        }

        // ---- Room adjacency (Fallout Shelter style merging) -----------------------

        public int MergeNeighbours(TowerRoom room)
        {
            if (room == null || room.type == "heart" || room.type == "gate") return 0;
            int count = 0;
            for (int direction = -1; direction <= 1; direction += 2)
            {
                int x = direction < 0 ? room.x - 1 : room.x + room.width;
                for (int step = 0; step < 2; step++)
                {
                    var next = RoomAt(room.floor, x);
                    if (next == null || next.type != room.type) break;
                    count++;
                    x = direction < 0 ? next.x - 1 : next.x + next.width;
                }
            }
            return Mathf.Min(3, count);
        }

        public float AdjacencyBonus(TowerRoom room)
        {
            if (room == null) return 1;
            float bonus = 1 + 0.12f * MergeNeighbours(room);
            var def = TowerCatalog.Get(room.type);
            if (def != null && def.produces != null)
                foreach (var neighbour in new[] { RoomAt(room.floor, room.x - 1),
                    RoomAt(room.floor, room.x + room.width) })
                {
                    if (neighbour == null || neighbour.type == room.type) continue;
                    if (SynergyPair(room.type, neighbour.type)) bonus += 0.08f;
                }
            return bonus;
        }

        private static bool SynergyPair(string a, string b)
        {
            return a == "kitchen" && (b == "well" || b == "farmstead" || b == "barn") ||
                a == "farmstead" && (b == "well" || b == "kitchen") ||
                a == "market" && (b == "warehouse" || b == "guild_hall") ||
                a == "forge" && (b == "quarry" || b == "lumber_mill") ||
                a == "frosted_mug" && (b == "kitchen" || b == "market");
        }

        public string AdjacencyNote(TowerRoom room)
        {
            int merged = MergeNeighbours(room);
            float bonus = AdjacencyBonus(room);
            if (bonus <= 1.001f) return "";
            return (merged > 0 ? "Merged x" + (merged + 1) + "  " : "Adjacent synergy  ") +
                "+" + Mathf.RoundToInt((bonus - 1) * 100) + "%";
        }

        // ---- Needs: what the Tower makes, what it burns, and what to do about it -------

        public static readonly string[] Stocks = { "food", "water", "firewood" };

        public float StockOf(string resource)
        {
            switch (resource)
            {
                case "food": return State.food;
                case "water": return State.water;
                case "firewood": return State.firewood;
                default: return 0;
            }
        }

        // Steady-state income while workers stay at their posts and ready rooms get collected.
        public float IncomePerMinute(string resource)
        {
            float perSecond = 0;
            foreach (var room in State.rooms)
            {
                var def = TowerCatalog.Get(room.type);
                if (def == null || def.produces != resource || State.incidents.Exists(i => i.roomUid == room.uid))
                    continue;
                perSecond += ProductionRate(room) / 90f * CollectAmount(room);
            }
            return perSecond * 60f;
        }

        public float UpkeepPerMinute(string resource)
        {
            float perSecond = 0;
            if (resource == "firewood")
            {
                int cells = 0;
                foreach (var room in State.rooms) if (room.type != "heart" && room.type != "gate") cells += room.width;
                return 0.00065f * cells * 60f;
            }
            foreach (var resident in State.residents)
            {
                if (resident.origin == "body" || resident.away || IsPosted(resident)) continue;
                bool child = resident.ageStage == 1;
                if (resource == "food") perSecond += (child ? 0.11f : 0.15f) * 2f / 28f * MealFactor(resident);
                else if (resource == "water") perSecond += (child ? 0.13f : 0.17f) * 2f / 30f;
            }
            return perSecond * 60f;
        }

        public float NetPerMinute(string resource) { return IncomePerMinute(resource) - UpkeepPerMinute(resource); }

        public static string ProducerName(string resource)
        {
            return resource == "food" ? "Kitchen or Farmstead" : resource == "water" ? "Stone Well" : "Lumber Mill";
        }

        private static string ProducerId(string resource)
        {
            return resource == "food" ? "kitchen" : resource == "water" ? "well" : "lumber_mill";
        }

        // One actionable line for the most pressing shortfall, or "" when the Tower is comfortable.
        public string NeedsAdvice()
        {
            if (State.introPhase != "complete" || State.tutorialStep < 7 || State.defeated) return "";
            string worst = null;
            float worstMinutes = 8f;
            foreach (string resource in Stocks)
            {
                float net = NetPerMinute(resource);
                if (net >= -0.05f) continue;
                float minutes = StockOf(resource) / -net;
                if (minutes < worstMinutes) { worst = resource; worstMinutes = minutes; }
            }
            if (worst != null)
            {
                string label = worst.ToUpperInvariant();
                bool built = State.rooms.Exists(r => TowerCatalog.Get(r.type).produces == worst);
                string when = worstMinutes < 1f ? "is nearly gone" : "lasts about " + Mathf.CeilToInt(worstMinutes) + " min";
                if (!built)
                {
                    bool known = State.blueprints.Contains(ProducerId(worst));
                    return label + " " + when + ". " + (known ? "Build a " : "Study and build a ") + ProducerName(worst) + ".";
                }
                return label + " " + when + ". Staff the " + ProducerName(worst) + " (drag a resident onto it).";
            }
            if (State.pendingVisitors > 0 && BiologicalPopulation() >= PopulationCap() && HeartLimitsPopulation())
                return "A wanderer waits at the Gate, but the Heart shelters only " + DwellerCap(State.heartRank) +
                    " people at rank " + TowerTiers.Tier(State.heartRank) + ". Raise the Heart.";
            if (State.pendingVisitors > 0 && BiologicalPopulation() >= PopulationCap())
                return "A wanderer waits at the Gate. " + (State.blueprints.Contains("cottage") ?
                    "Build a Cottage for a free bed." : State.blueprints.Contains("nursery") ?
                    "Build a Hearth Nursery, or research CON-2 for the Cottage, for a free bed." :
                    "Research CON-2 at the Heart, then build a Cottage for a free bed.");
            foreach (var resident in State.residents)
                if (resident.ageStage == 0 && resident.origin != "body" && resident.jobRoom == 0 &&
                    !resident.exploring && !resident.away)
                    return resident.name + " has no workplace. Assign them to a room.";
            return "";
        }

        // ---- Storyteller -----------------------------------------------------------

        public float ThreatTarget()
        {
            float pressure = BiologicalPopulation() * 1.2f + State.rooms.Count * 0.25f +
                Mathf.Max(0, State.day - 3) * 0.5f + State.heartRank * 3f + DistrictThreat();
            float defence = 0;
            foreach (var resident in State.residents)
                if (resident.ageStage == 0 && !resident.downed && resident.priorityDefense > 0)
                    defence += resident.weapon * 2f + resident.might * 0.3f;
            return Mathf.Clamp(pressure - defence * 0.3f, 0, 100);
        }

        public string ThreatLabel()
        {
            float t = State.threat;
            return t < 25 ? "Calm" : t < 50 ? "Uneasy" : t < 75 ? "Tense" : "Dire";
        }

        private void TickLife(float dt, bool live)
        {
            if (State.threat <= 0 && State.clock < 1) State.threat = ThreatTarget() * 0.5f;
            State.threat = Mathf.MoveTowards(State.threat, ThreatTarget(), 0.5f * dt);
            if (State.festivalSeconds > 0) State.festivalSeconds = Mathf.Max(0, State.festivalSeconds - dt);
            foreach (var resident in State.residents)
            {
                if (resident.origin == "body" || resident.ageStage != 0) continue;
                if (live) TickDepth(resident, dt);
                if (resident.breakSeconds > 0)
                {
                    ApplyBreak(resident, dt);
                    resident.breakSeconds -= dt;
                    if (resident.breakSeconds <= 0)
                    {
                        resident.breakSeconds = 0; resident.moodLow = 0;
                        resident.happiness = Mathf.Max(resident.happiness, 35);
                        Note(resident.name + " calmed down.");
                    }
                    continue;
                }
                if (live && resident.happiness < 18 && !resident.downed && !resident.exploring)
                {
                    resident.moodLow += dt;
                    if (resident.moodLow >= 25) BeginBreak(resident);
                }
                else resident.moodLow = Mathf.Max(0, resident.moodLow - dt * 2);
            }
            FlushDepartures();
        }

        // Chooses what the storyteller sends, weighted by threat so the early Tower stays kind.
        private string ChooseIncident(bool guided)
        {
            if (guided || State.eventTimer <= 1) return "fire";
            float threat = State.threat;
            var pool = new List<string> { "fire", "pests" };
            if (State.residents.Count >= 5 && threat >= 20) pool.Add("illness");
            var teller = Storyteller;
            if (threat >= teller.raidThreat) pool.Add("raiders");
            if (threat >= 55 && teller.doubleRaids) pool.Add("raiders");
            if (State.rooms.Exists(r => r.floor < 0 && r.type != "heart")) pool.Add("cave_in");
            return pool[Mathf.Min(pool.Count - 1, (int)(Random01() * pool.Count))];
        }

        private bool TryPositiveEvent()
        {
            float roll = Random01();
            float kind = Storyteller.positive;   // a calm storyteller sends friendlier visitors more often
            if (roll < 0.13f * kind)
            {
                int gift = 20 + Mathf.Min(100, State.residents.Count * 4);
                State.food = Mathf.Min(StockCap(), State.food + gift);
                State.water = Mathf.Min(StockCap(), State.water + gift);
                Note("A Silverbrook caravan shared " + gift + " food and water.");
                Emit("caravan", 0, 0, gift.ToString());
                return true;
            }
            if (roll < 0.20f * kind && State.residents.Count >= 3)
            {
                State.festivalSeconds = 90;
                Note("The Tower is holding a harvest festival. Spirits are high.");
                Emit("festival", 0, 0, "");
                return true;
            }
            if (roll < 0.26f * kind && BiologicalPopulation() + State.pendingVisitors < PopulationCap())
            {
                State.pendingVisitors = Mathf.Min(2, State.pendingVisitors + 1);
                Note("A traveller has come to the Gate on their own.");
                return true;
            }
            return false;
        }

        // Seconds until the next event, shortened as the Tower attracts more attention.
        private float NextEventDelay()
        {
            float pace = Mathf.Lerp(1.2f, 0.65f, Mathf.Clamp01(State.threat / 100f));
            return (190 + Random01() * 130 + State.incidents.Count * 50) * pace * Storyteller.delay;
        }
    }
}
