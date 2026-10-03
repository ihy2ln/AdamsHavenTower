using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // A room type that serves the town: meals, drinks or leisure (TOWER_MODE_GDD 19.1). Street lots and venue prices
    // (19.2, 19.3) plug in here later.
    public sealed class TowerVenueDef
    {
        public readonly string type, label, mealThought, funThought;
        public readonly int services;
        public readonly float quality, mealMood, funMood;
        public TowerVenueDef(string type, string label, int services, float quality,
            string mealThought = "", float mealMood = 0, string funThought = "", float funMood = 0)
        {
            this.type = type; this.label = label; this.services = services; this.quality = quality;
            this.mealThought = mealThought; this.mealMood = mealMood; this.funThought = funThought; this.funMood = funMood;
        }
        public bool Serves(int service) { return (services & service) != 0; }
    }

    // One venue in the current layout. Runtime only, never saved.
    public sealed class TowerVenue
    {
        public TowerRoom room;
        public TowerVenueDef def;
        public int seats;
    }

    // Town life, slice 1 (GDD 19.1, TT 10.3.1): residents run errands instead of eating from the stockpile wherever they
    // stand. Hungry or thirsty adults walk to a Kitchen, the Frosted Mug or a Well; when joy runs low (and in their free
    // hours) they go out to the Market, the Guild Hall, the Deck Hall or the Mug. Venues have seats; a full venue sends
    // patrons to the next one. Offline catch-up and towers without a venue keep the old instant meals, and anyone left
    // too hungry eats cold rations from the stores. Venues are self-serve until the town economy (19.3) adds wages.
    public sealed partial class TowerRules
    {
        public const int VenueMeal = 1, VenueDrink = 2, VenueLeisure = 4;
        // Errands start below 50 and fill to 99: a long cycle keeps trips rare (the old instant meals ran 65 to 93).
        public const float ErrandMemorySeconds = 240, ColdRationsBelow = 35, ErrandNeedBelow = 50, ErrandFullAt = 99;
        public const float JoyDrain = 0.07f, JoyFloorWithoutLeisure = 50;
        public const float MealRate = 10f, DrinkRate = 15f, LeisureRate = 5f;   // need points per second at a venue

        // Balance switch for A/B runs: false restores the old instant meals everywhere.
        public static bool TownErrands = true;

        public static readonly TowerVenueDef[] VenueDefs = {
            new TowerVenueDef("kitchen", "the Kitchen", VenueMeal, 1f, "Ate at the Kitchen", 2),
            new TowerVenueDef("frosted_mug", "the Frosted Mug", VenueMeal | VenueDrink | VenueLeisure, 1.2f,
                "Had a good meal at the Frosted Mug", 5, "A round at the Frosted Mug", 5),
            new TowerVenueDef("well", "the Stone Well", VenueDrink, 1f),
            new TowerVenueDef("market", "the Argent Market", VenueLeisure, 1f, "", 0, "Browsed the Argent Market", 4),
            new TowerVenueDef("guild_hall", "the Guild Hall", VenueLeisure, 1f, "", 0, "Swapped tales at the Guild", 4),
            new TowerVenueDef("deck_hall", "the Deck Hall", VenueLeisure, 1.1f, "", 0, "Card night at the Deck Hall", 4),
        };

        public static TowerVenueDef VenueDef(string type)
        {
            foreach (var def in VenueDefs) if (def.type == type) return def;
            return null;
        }

        public static bool IsErrand(string task) { return task == "meal" || task == "drink" || task == "leisure"; }

        private static int ErrandService(string task)
        { return task == "meal" ? VenueMeal : task == "drink" ? VenueDrink : task == "leisure" ? VenueLeisure : 0; }

        // ---------------------------------------------------------------- venue cache

        private readonly List<TowerVenue> venues = new List<TowerVenue>();
        private int venueStamp = -1, venueRooms = -1, venueBuilds;
        private List<TowerRoom> venueList;
        private bool hasLeisureVenue, openMeal, openDrink;   // the open flags are refreshed once per needs pass

        // Bumped every time the venue list is rebuilt (tests watch it).
        public int VenueStamp { get { return venueBuilds; } }

        public List<TowerVenue> Venues() { EnsureVenues(); return venues; }

        private void EnsureVenues()
        {
            if (venueStamp == layoutStamp && venueList == State.rooms && venueRooms == State.rooms.Count) return;
            venueStamp = layoutStamp; venueList = State.rooms; venueRooms = State.rooms.Count;
            venueBuilds++;
            venues.Clear();
            hasLeisureVenue = false;
            foreach (var room in State.rooms)
            {
                var def = VenueDef(room.type);
                if (def == null) continue;
                venues.Add(new TowerVenue { room = room, def = def });
                if (def.Serves(VenueLeisure)) hasLeisureVenue = true;
            }
            foreach (var venue in venues) venue.seats = Capacity(venue.room);   // rank and bays add seats
        }

        private bool VenueOpen(TowerVenue venue, int service)
        {
            var room = venue.room;
            if (room.condition < 20 || !IsPowered(room) || incidentRooms.Contains(room.uid)) return false;
            if (service == VenueMeal) return State.food >= 2f;
            if (service == VenueDrink) return State.water >= 2f;
            return true;
        }

        private bool AnyOpen(int service)
        {
            foreach (var venue in venues) if (venue.def.Serves(service) && VenueOpen(venue, service)) return true;
            return false;
        }

        // Called at the start of every needs pass.
        private void RefreshVenueFlags()
        {
            EnsureVenues();
            openMeal = AnyOpen(VenueMeal);
            openDrink = AnyOpen(VenueDrink);
        }

        public float TravelSeconds(TowerRoom from, TowerRoom to)
        {
            return from == null || to == null ? 0 :
                1.5f + Mathf.Abs(to.floor - from.floor) * 2.3f + Mathf.Abs(to.x - from.x) * 0.17f;
        }

        // Residents other than this one at the venue on an errand.
        private int VenueLoad(TowerResident resident, int roomUid)
        {
            return ClaimedByOthers(resident, "meal", roomUid) + ClaimedByOthers(resident, "drink", roomUid) +
                ClaimedByOthers(resident, "leisure", roomUid);
        }

        // What a venue charges for one serving. Free until the town economy (GDD 19.3).
        public int VenuePrice(TowerVenue venue, int service) { return 0; }

        // ---------------------------------------------------------------- free time and joy

        // A soft evening (or morning, for night owls) block; flexible residents go out on joy alone.
        public bool FreeTime(TowerResident resident)
        {
            float hour = Hour();
            switch (resident.schedule)
            {
                case "day": return hour >= 19f && hour < 22f;
                case "night": return hour >= 7f && hour < 10f;
                default: return false;
            }
        }

        public static string FreeHours(string schedule)
        { return schedule == "day" ? "19:00-22:00" : schedule == "night" ? "07:00-10:00" : "when joy runs low"; }

        private void TickJoy(TowerResident resident, float dt)
        {
            resident.mealMemorySeconds = Mathf.Max(0, resident.mealMemorySeconds - dt);
            resident.funMemorySeconds = Mathf.Max(0, resident.funMemorySeconds - dt);
            if (resident.ageStage != 0 || !TownErrands) return;
            bool atLeisure = resident.currentTask == "leisure" && resident.currentRoom == resident.targetRoom;
            if (!atLeisure) resident.joy = Mathf.Max(0, resident.joy - JoyDrain * dt);
            if (!hasLeisureVenue) resident.joy = Mathf.Max(resident.joy, JoyFloorWithoutLeisure);   // no early-game penalty
        }

        // Live ticks with an open venue of that kind: residents eat out, so the stockpile only feeds the desperate.
        private bool EatsOut(TowerResident resident, bool live, int service)
        {
            if (!live || !TownErrands || resident.ageStage != 0) return false;
            return service == VenueMeal ? openMeal : openDrink;
        }

        private bool BeingServed(TowerResident resident, string task)
        { return resident.currentTask == task && resident.currentRoom == resident.targetRoom; }

        // ---------------------------------------------------------------- planning

        // Scores the meal, drink and leisure errands against the best job PlanJob found.
        private void ConsiderErrand(TowerResident resident, ref float best, ref string task, ref int target)
        {
            EnsureVenues();
            if (venues.Count == 0) return;
            var here = Room(resident.currentRoom);
            bool free = FreeTime(resident);
            for (int service = VenueMeal; service <= VenueLeisure; service <<= 1)
            {
                string errand = service == VenueMeal ? "meal" : service == VenueDrink ? "drink" : "leisure";
                float need = service == VenueMeal ? resident.hunger : service == VenueDrink ? resident.thirst : resident.joy;
                bool committed = resident.currentTask == errand;   // on the way or being served
                float gate = service == VenueLeisure ? (free ? 85 : 40) : ErrandNeedBelow;
                if (!committed && need >= gate) continue;
                if (committed && need >= ErrandFullAt) continue;   // done
                if (service != VenueLeisure && !(service == VenueMeal ? openMeal : openDrink)) continue;
                foreach (var venue in venues)
                {
                    if (!venue.def.Serves(service) || !VenueOpen(venue, service)) continue;
                    int uid = venue.room.uid;
                    bool mine = committed && resident.targetRoom == uid;
                    int load = VenueLoad(resident, uid);
                    if (!mine && load >= venue.seats) continue;
                    float travel = resident.currentRoom == uid ? 0 : TravelSeconds(here ?? venue.room, venue.room);
                    float score;
                    if (mine) score = 90;   // see the errand through unless something urgent calls
                    else if (service == VenueLeisure)
                        score = (free ? 62 + 0.6f * (85 - need) : 52 + 0.6f * (40 - need)) + LeisureTaste(resident, venue.def);
                    else score = Mathf.Min(95, 40 + 1.2f * (75 - need));
                    if (!mine) score -= 0.8f * travel + 4f * load / Mathf.Max(1, venue.seats);
                    if (score > best) { best = score; task = errand; target = uid; }
                }
            }
        }

        private static float LeisureTaste(TowerResident resident, TowerVenueDef def)
        {
            if (HasTrait(resident, "Gregarious") && (def.type == "frosted_mug" || def.type == "guild_hall")) return 4;
            if (HasTrait(resident, "Curious") && (def.type == "deck_hall" || def.type == "market")) return 4;
            if (HasTrait(resident, "Solitary") && def.type == "frosted_mug") return -4;
            return 0;
        }

        // ---------------------------------------------------------------- serving

        // At the venue: the one place an errand turns stock into need. Food and water per point match the old meals.
        private void TickErrand(TowerResident resident, TowerRoom room, float dt)
        {
            var def = VenueDef(room.type);
            int service = ErrandService(resident.currentTask);
            if (def == null || !def.Serves(service)) return;
            float ration = HasTrait(resident, "Frugal") ? 1.7f : 2f;
            if (service == VenueMeal)
            {
                float meal = ration * MealFactor(resident);
                float gain = Mathf.Min(MealRate * dt, 100 - resident.hunger, State.food / meal * 28f);
                if (gain <= 0) return;
                State.food = Mathf.Max(0, State.food - gain / 28f * meal);
                resident.hunger += gain;
                resident.mealMemory = def.type; resident.mealMemorySeconds = ErrandMemorySeconds;
            }
            else if (service == VenueDrink)
            {
                float gain = Mathf.Min(DrinkRate * dt, 100 - resident.thirst, State.water / ration * 30f);
                if (gain <= 0) return;
                State.water = Mathf.Max(0, State.water - gain / 30f * ration);
                resident.thirst += gain;
            }
            else
            {
                resident.joy = Mathf.Min(100, resident.joy + LeisureRate * def.quality * dt);
                resident.funMemory = def.type; resident.funMemorySeconds = ErrandMemorySeconds;
            }
        }

        // ---------------------------------------------------------------- thoughts and labels

        private void AddTownThoughts(TowerResident resident, List<TowerThought> list)
        {
            if (resident.ageStage != 0) return;
            if (resident.mealMemorySeconds > 0)
            {
                if (resident.mealMemory == "cold") list.Add(new TowerThought("Ate cold rations", -3));
                else
                {
                    var def = VenueDef(resident.mealMemory);
                    if (def != null && def.mealMood > 0) list.Add(new TowerThought(def.mealThought, def.mealMood));
                }
            }
            if (resident.funMemorySeconds > 0)
            {
                var def = VenueDef(resident.funMemory);
                if (def != null && def.funMood > 0) list.Add(new TowerThought(def.funThought, def.funMood));
            }
            if (resident.joy >= 75) list.Add(new TowerThought("Well entertained", 3));
            else if (resident.joy < 20 && State.heartRank >= 2 && TownErrands)
            {
                EnsureVenues();
                if (hasLeisureVenue) list.Add(new TowerThought("No free time", -5));
            }
        }

        public string ErrandLabel(TowerResident resident)
        {
            var room = Room(resident.targetRoom);
            var def = room == null ? null : VenueDef(room.type);
            string where = def == null ? "a venue" : def.label;
            bool there = resident.currentRoom == resident.targetRoom;
            switch (resident.currentTask)
            {
                case "meal": return there ? "Eating at " + where + "." : "Heading to " + where + " for a meal.";
                case "drink": return there ? "Drinking at " + where + "." : "Heading to " + where + " for a drink.";
                default: return there ? "Free time at " + where + "." : "Off to " + where + " to unwind.";
            }
        }

        // Room card line for a venue: patrons now and what it serves.
        public string VenueLine(TowerRoom room)
        {
            var def = room == null ? null : VenueDef(room.type);
            if (def == null) return "";
            int patrons = 0;
            foreach (var resident in State.residents)
                if (IsErrand(resident.currentTask) && resident.targetRoom == room.uid && resident.currentRoom == room.uid)
                    patrons++;
            var serves = new List<string>();
            if (def.Serves(VenueMeal)) serves.Add("meals");
            if (def.Serves(VenueDrink)) serves.Add("drinks");
            if (def.Serves(VenueLeisure)) serves.Add("leisure");
            return "Patrons " + patrons + "/" + Capacity(room) + " • " + string.Join(", ", serves.ToArray());
        }
    }
}
