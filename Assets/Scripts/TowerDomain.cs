using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AdamsHaven.Tower
{
    [Serializable] public sealed class TowerFloor
    {
        public int number;
        public int west = 1;
        public int east;
        public string landing = "energy";
    }

    [Serializable] public sealed class TowerRoom
    {
        public int uid;
        public string type;
        public int floor;
        public int x;
        public int width;
        public int level = 1;
        public float progress;
        public bool ready;
        public float condition = 100;
        public bool repairOrder;
        public float repairProgress;
        public float rushFatigue;
    }

    [Serializable] public sealed class TowerResident
    {
        public int id;
        public string name;
        public string unitId;
        public string origin = "villager";
        public string chassisVariant = "";
        public int level = 1;
        public int homeRoom;
        public int jobRoom;
        public float hp = 105;
        public float happiness = 70;
        public float charge = 2160;
        public bool downed;
        public bool away;
        public bool exploring;
        public float exploreSeconds;
        public string exploreChoice = "supplies";
        public int exploreGold, exploreWood, exploreStone, exploreOre, exploreEssence, exploreCelestium;
        public float hunger = 85, thirst = 85, rest = 85, injury, illness;
        public int ageStage; // 0 adult, 1 child
        public float growthSeconds;
        public int familyPartnerId;
        public float familySeconds;
        public int currentRoom, targetRoom;
        public float travelSeconds, travelDuration;
        public string currentTask = "idle";
        public int priorityProduction = 2, priorityHaul = 2, priorityRepair = 2;
        public int priorityFire = 3, priorityCare = 2, priorityDefense = 2;
        public int weapon, tool;
        public int might = 3, sight = 3, grit = 3, charm = 3, wit = 3, grace = 3, luck = 3;
        public string trait = "";
        public string schedule = "";
        public float breakSeconds, moodLow;
        public string breakKind = "";
        public int xp;
        public float criticalSeconds;

        public int Stat(string key)
        {
            switch (key)
            {
                case "might": return might; case "sight": return sight;
                case "grit": return grit; case "charm": return charm;
                case "wit": return wit; case "grace": return grace;
                case "luck": return luck; default: return 0;
            }
        }
    }

    [Serializable] public sealed class TowerIncident
    {
        public int roomUid;
        public string kind;
        public float hp;
        public float spreadSeconds;
        public float severity = 1;
        public float marchSeconds;   // raiders: time spent in this room without being fought
        public int fromRoom;         // raiders: the room they just left, so they push onward
        public int stolen;           // raiders: gold carried off, returned if they are beaten
    }

    // RimWorld-style opinion of one resident for another; symmetric, kept once per pair.
    [Serializable] public sealed class TowerBond
    {
        public int a, b;
        public float opinion;
    }

    [Serializable] public sealed class TowerMemorial
    {
        public string name;
        public string cause;
        public int day;
        public float diedAt;
        public int partnerId;
        public List<int> friends = new List<int>();
    }

    [Serializable] public sealed class TowerState
    {
        public int schema = 2;
        public int slot = 1;
        public string label = "Founding Day";
        public int day = 1;
        public string introPhase = "dormant";
        public int nextRoomUid = 1;
        public int nextResidentId = 1;
        public int randomState = 77101;
        public float clock;
        public float gateTimer;
        public float eventTimer;
        public float eventCooldown = 180;
        public bool haulingUnlocked;
        public int tutorialStep;
        public int pendingVisitors;
        public long savedUnix;
        public int gold = 120, celestium, sigils = 1, wood, stone, ore, essence, tonics = 2;
        public float food = 60, water = 60, firewood = 60;
        public int heartRank = 1;
        public float heartHp = 1200;
        public bool defeated;
        public List<TowerFloor> floors = new List<TowerFloor>();
        public List<TowerRoom> rooms = new List<TowerRoom>();
        public List<TowerResident> residents = new List<TowerResident>();
        public List<TowerIncident> incidents = new List<TowerIncident>();
        public List<string> blueprints = new List<string>();
        public List<string> policies = new List<string>();
        public List<string> excavatedCells = new List<string>();
        public List<string> log = new List<string>();
        public float threat, festivalSeconds;
        public int goalsClaimed;
        public List<TowerGoal> goals = new List<TowerGoal>();
        public List<string> notifiedGoals = new List<string>();
        public List<TowerCounter> counters = new List<TowerCounter>();
        public List<TowerBond> bonds = new List<TowerBond>();
        public List<TowerMemorial> memorial = new List<TowerMemorial>();
        public int generator;        // TowerMilestones.Version that built a checkpoint slot
        public bool steward = true;  // re-staffs food, water and firewood before they run out
        public float stewardTimer;
    }

    public sealed class TowerRoomDef
    {
        public readonly string id, displayName, kind, stat, produces;
        public readonly int width, cost;
        public readonly bool groundOnly, undergroundOnly;
        public TowerRoomDef(string id, string name, string kind, int width, int cost,
            string stat = "", string produces = "", bool groundOnly = false, bool undergroundOnly = false)
        {
            this.id = id; displayName = name; this.kind = kind; this.width = width;
            this.cost = cost; this.stat = stat; this.produces = produces;
            this.groundOnly = groundOnly; this.undergroundOnly = undergroundOnly;
        }
    }

    public static class TowerCatalog
    {
        // Values follow Shelter.ROOMS and WorldDefs.TOWER_W in the Godot source.
        public static readonly TowerRoomDef[] All = {
            new TowerRoomDef("gate", "Celestium Gate", "gate", 1, 0, "might"),
            new TowerRoomDef("heart", "Celestium Heart", "heart", 1, 0, "grit"),
            new TowerRoomDef("house", "The Shack", "living", 1, 40, "charm"),
            new TowerRoomDef("cottage", "Cottage", "living", 2, 90, "charm"),
            new TowerRoomDef("nursery", "Hearth Nursery", "living", 2, 150, "charm"),
            new TowerRoomDef("terrace_row", "Terrace Row", "living", 3, 220, "charm"),
            new TowerRoomDef("manor", "Ashgrove Manor", "living", 3, 420, "charm"),
            new TowerRoomDef("kitchen", "Kitchen", "produce", 2, 100, "grace", "food"),
            new TowerRoomDef("farmstead", "Farmstead", "produce", 2, 140, "grace", "food", true),
            new TowerRoomDef("well", "Stone Well", "produce", 1, 80, "sight", "water"),
            new TowerRoomDef("lumber_mill", "Lumber Mill", "produce", 3, 120, "might", "firewood"),
            new TowerRoomDef("quarry", "Stone Quarry", "produce", 3, 180, "grit", "celestium", false, true),
            new TowerRoomDef("barn", "Barn", "storage", 2, 150, "grit"),
            new TowerRoomDef("silo", "Grain Silo", "storage", 1, 110, "grit"),
            new TowerRoomDef("warehouse", "Warehouse", "storage", 3, 260, "grit"),
            new TowerRoomDef("frosted_mug", "The Frosted Mug", "medic", 3, 260, "wit", "tonics"),
            new TowerRoomDef("guild_hall", "Silverbrook Adventure Guild", "herald", 4, 320, "charm"),
            new TowerRoomDef("forge", "The Forge", "train", 2, 240, "might"),
            new TowerRoomDef("deck_hall", "Deck Hall", "train", 3, 260, "wit"),
            new TowerRoomDef("market", "Argent Market", "trade", 3, 220, "luck", "gold")
        };

        public static TowerRoomDef Get(string id)
        {
            foreach (var def in All) if (def.id == id) return def;
            return null;
        }
    }

    public sealed partial class TowerRules
    {
        public const int FloorMin = -24, FloorMax = 24;
        public const int CoreX = 22, GateX = 23, WingCells = 10, MinBuildX = 12, MaxBuildX = 33;
        public const float DaySeconds = 720f;
        public readonly TowerState State;

        public TowerRules(TowerState state) { State = state; Normalize(); }

        public static TowerRules New(int slot = 1)
        {
            var state = new TowerState { slot = slot, randomState = 77101 + slot * 97 };
            // The first west cell (x=21) is the Shack's tutorial lot.
            state.floors.Add(new TowerFloor { number = 0, west = 1 });
            state.blueprints.Add("house");
            var rules = new TowerRules(state);
            rules.AddRoom("heart", 0, CoreX, 1);
            rules.Note("The Celestium Heart lies dormant. Touch it to begin.");
            return rules;
        }

        public void Normalize()
        {
            if (State.introPhase == "complete" && State.day >= 10 && State.tutorialStep == 0)
                State.tutorialStep = 7; // Older populated saves predate the guided opening.
            if (State.floors == null) State.floors = new List<TowerFloor>();
            if (State.rooms == null) State.rooms = new List<TowerRoom>();
            if (State.residents == null) State.residents = new List<TowerResident>();
            if (State.incidents == null) State.incidents = new List<TowerIncident>();
            if (State.blueprints == null) State.blueprints = new List<string>();
            if (State.policies == null) State.policies = new List<string>();
            if (State.excavatedCells == null) State.excavatedCells = new List<string>();
            if (State.log == null) State.log = new List<string>();
            if (State.goals == null) State.goals = new List<TowerGoal>();
            if (State.notifiedGoals == null) State.notifiedGoals = new List<string>();
            if (State.counters == null) State.counters = new List<TowerCounter>();
            if (State.bonds == null) State.bonds = new List<TowerBond>();
            if (State.memorial == null) State.memorial = new List<TowerMemorial>();            if (State.randomState == 0) State.randomState = 77101;
            foreach (var resident in State.residents)
            {
                if (string.IsNullOrEmpty(resident.schedule)) resident.schedule = "flexible";
                if (string.IsNullOrEmpty(resident.trait) && resident.origin != "body")
                    resident.trait = Traits[(resident.id * 7 + 3) % Traits.Length];
            }
            if (State.introPhase == "complete")
            {
                // The Heart sits in the middle now: every founded floor also holds its east side.
                foreach (var floor in State.floors) if (floor.east < 1) floor.east = 1;
                RefillGoals();
            }
            string[] bodyVariants = { "normal", "short", "tall", "muscle", "hourglass" };
            int bodyIndex = 0;
            foreach (var resident in State.residents)
            {
                if (resident.origin == "body")
                {
                    if (string.IsNullOrEmpty(resident.chassisVariant))
                        resident.chassisVariant = bodyVariants[bodyIndex % bodyVariants.Length];
                    bodyIndex++;
                }
                if (string.IsNullOrEmpty(resident.currentTask)) resident.currentTask = "idle";
                if (string.IsNullOrEmpty(resident.exploreChoice)) resident.exploreChoice = "supplies";
                if (resident.currentRoom == 0)
                    resident.currentRoom = resident.jobRoom > 0 ? resident.jobRoom : resident.homeRoom;
            }
        }

        public TowerFloor Floor(int number) { return State.floors.Find(f => f.number == number); }
        public TowerRoom Room(int uid) { return State.rooms.Find(r => r.uid == uid); }
        public TowerResident Resident(int id) { return State.residents.Find(r => r.id == id); }
        public TowerRoom RoomAt(int floor, int x)
        { return State.rooms.Find(r => r.floor == floor && x >= r.x && x < r.x + r.width); }

        public TowerRoom AddRoom(string type, int floor, int x, int level = 1)
        {
            var def = TowerCatalog.Get(type);
            if (def == null) return null;
            var room = new TowerRoom { uid = State.nextRoomUid++, type = type, floor = floor,
                x = x, width = def.width, level = level };
            State.rooms.Add(room);
            return room;
        }

        public void Note(string text)
        {
            State.log.Add(text);
            if (State.log.Count > 40) State.log.RemoveAt(0);
        }

        public string AwakenHeart()
        {
            if (State.introPhase != "dormant") return "The Heart is already awake.";
            State.introPhase = "gate"; Note("The Heart awakened."); return null;
        }

        public string PlaceIntroGate()
        {
            if (State.introPhase != "gate") return "Awaken the Heart first.";
            if (RoomAt(0, GateX) == null) AddRoom("gate", 0, GateX);
            var ground = Floor(0);
            if (ground != null && ground.east < 1) ground.east = 1;
            State.introPhase = "shack"; Note("The Gate joined the Heart."); return null;
        }

        public string ChooseStarter(string unitId)
        {
            if (State.introPhase != "choose") return "Place the Gate and Shack first.";
            if (unitId != "kaela" && unitId != "ghislaine" && unitId != "elara")
                return "Choose Kaela, Ghislaine, or Elara.";
            AddResident(unitId, unitId == "kaela" ? "Kaela" :
                (unitId == "ghislaine" ? "Ghislaine" : "Elara"), "hero", 1);
            TowerRoom shack = State.rooms.Find(r => r.type == "house" && r.floor == 0);
            if (shack != null)
            {
                var starter = State.residents[State.residents.Count - 1];
                starter.homeRoom = starter.currentRoom = shack.uid;
            }
            State.introPhase = "complete";
            State.tutorialStep = 0;
            RefillGoals();
            foreach (string id in new[] { "kitchen", "well", "lumber_mill", "market", "nursery" })
                if (!State.blueprints.Contains(id)) State.blueprints.Add(id);
            // Tower-only progression needs a foundation and income path without battle rewards.
            State.celestium += 45;
            State.gold += 650;
            State.wood += 20;
            State.stone += 12;
            Note("The Tower opened. Survival rooms, 650 gold and 45 Celestium were granted.");
            return null;
        }

        public TowerResident AddResident(string unitId, string name, string origin, int level)
        {
            var resident = new TowerResident { id = State.nextResidentId++, unitId = unitId,
                name = name, origin = origin, level = level };
            if (origin == "body") resident.charge = 2160;
            resident.schedule = "flexible";
            if (origin != "body") resident.trait = Traits[(resident.id * 7 + 3) % Traits.Length];
            State.residents.Add(resident);
            return resident;
        }

        public int BuildCost(string type)
        {
            var def = TowerCatalog.Get(type);
            if (def == null) return 0;
            int count = 0;
            foreach (var room in State.rooms) if (room.type == type) count++;
            return def.cost + 25 * count;
        }

        public int BuildWoodCost(string type)
        {
            var def = TowerCatalog.Get(type);
            return def == null || State.introPhase == "shack" && type == "house" ? 0 : def.width * 2;
        }

        public int BuildStoneCost(string type)
        {
            var def = TowerCatalog.Get(type);
            return def == null || State.introPhase == "shack" && type == "house" ? 0 : def.width;
        }

        public int BlueprintGoldCost(string type)
        {
            var def = TowerCatalog.Get(type);
            return def == null ? 0 : Mathf.Max(60, def.cost / 2);
        }

        public int BlueprintCelestiumCost(string type)
        {
            var def = TowerCatalog.Get(type);
            return def == null ? 0 : 1 + def.width;
        }

        public string ResearchBlueprint(string type)
        {
            var def = TowerCatalog.Get(type);
            if (def == null || type == "heart" || type == "gate") return "Unknown blueprint.";
            if (State.introPhase != "complete") return "Finish founding the Tower first.";
            if (State.blueprints.Contains(type)) return "Blueprint already known.";
            if (State.gold < BlueprintGoldCost(type) ||
                State.celestium < BlueprintCelestiumCost(type))
                return "Research needs " + BlueprintGoldCost(type) + " gold and " +
                    BlueprintCelestiumCost(type) + " Celestium.";
            State.gold -= BlueprintGoldCost(type);
            State.celestium -= BlueprintCelestiumCost(type);
            State.blueprints.Add(type);
            Note("Researched the " + def.displayName + " blueprint.");
            return null;
        }

        // A cell is founded when the floor's foundation reaches it; the shaft cell itself never is.
        public bool IsFounded(int floor, int x)
        {
            var f = Floor(floor);
            if (f == null || x == CoreX) return false;
            return x < CoreX ? x >= CoreX - f.west : x <= CoreX + f.east;
        }

        public string CanBuild(string type, int floor, int x)
        {
            var def = TowerCatalog.Get(type);
            if (def == null || type == "heart" || type == "gate") return "Unknown room.";
            if (!State.blueprints.Contains(type)) return "The Heart has not learned that blueprint.";
            if (floor < FloorMin || floor > FloorMax) return "Floor out of range.";
            bool westSide = x + def.width <= CoreX, eastSide = x > CoreX;
            if (!westSide && !eastSide) return "Rooms cannot cover the Heart shaft.";
            if (x < MinBuildX || x + def.width - 1 > MaxBuildX) return "Rooms stay within the ten-cell wings.";
            var f = Floor(floor);
            if (f == null) return "Open this floor first.";
            if (westSide && x < CoreX - f.west) return "Expand the Celestium foundation west first.";
            if (eastSide && x + def.width - 1 > CoreX + f.east) return "Expand the Celestium foundation east first.";
            if (def.groundOnly && floor != 0) return "This room needs the ground floor.";
            if (def.undergroundOnly && floor >= 0) return "This room belongs underground.";
            for (int cx = x; cx < x + def.width; cx++) if (RoomAt(floor, cx) != null) return "Another room occupies that space.";
            if (westSide && x + def.width != CoreX && RoomAt(floor, x + def.width) == null)
                return "Build contiguously outward from the Heart shaft.";
            if (eastSide && x != CoreX + 1 && RoomAt(floor, x - 1) == null)
                return "Build contiguously outward from the Heart shaft.";
            if (State.gold < BuildCost(type)) return "Not enough gold.";
            if (State.wood < BuildWoodCost(type) || State.stone < BuildStoneCost(type))
                return "Construction needs " + BuildWoodCost(type) + " wood and " +
                    BuildStoneCost(type) + " stone.";
            return null;
        }

        public string Build(string type, int floor, int x)
        {
            string error = CanBuild(type, floor, x);
            if (error != null) return error;
            State.gold -= BuildCost(type);
            State.wood -= BuildWoodCost(type);
            State.stone -= BuildStoneCost(type);
            var built = AddRoom(type, floor, x);
            Bump("build");
            Emit("build", built.uid, 0, type);
            if (State.introPhase == "shack" && type == "house" && floor == 0) State.introPhase = "choose";
            if (State.introPhase == "complete" && State.tutorialStep == 0 && type == "kitchen")
                State.tutorialStep = 1;
            Note("Built " + TowerCatalog.Get(type).displayName + " on floor " + floor + ".");
            return null;
        }

        public int FloorOpenCost(int number)
        { return number > 0 ? 3 + Mathf.CeilToInt(number / 3f) : 4 + Math.Abs(number); }

        public string OpenFloor(int number)
        {
            if (number < FloorMin || number > FloorMax) return "Floor out of range.";
            if (Floor(number) != null) return "Floor already open.";
            if (Floor(number - 1) == null && Floor(number + 1) == null) return "Open floors outward from the Heart.";
            int cost = FloorOpenCost(number);
            if (State.celestium < cost) return "Not enough Celestium.";
            State.celestium -= cost;
            State.floors.Add(new TowerFloor { number = number, east = 1 });
            Note("Opened floor " + number + ".");
            return null;
        }

        // side: -1 grows the west wing, +1 the east wing.
        public int ExpandCost(int number, int side = -1)
        {
            var floor = Floor(number);
            if (floor == null) return 0;
            int cells = side < 0 ? floor.west : floor.east - (number == 0 ? 1 : 0);
            return 3 + (number > 0 ? Mathf.CeilToInt(number / 3f) : -number) + Mathf.Max(0, cells - 1);
        }

        public int WingCellsOf(int number, int side)
        {
            var floor = Floor(number);
            if (floor == null) return 0;
            return side < 0 ? floor.west : floor.east;
        }

        // The cell the next expansion would found on that side.
        public int NextExpansionX(int number, int side)
        {
            var floor = Floor(number);
            if (floor == null) return CoreX;
            return side < 0 ? CoreX - floor.west - 1 : CoreX + floor.east + 1;
        }

        public string Excavate(int number, int x)
        {
            if (number >= 0) return "Only the underground needs excavation.";
            if (Floor(number) == null) return "Open this floor first.";
            string key = number + ":" + x;
            if (!State.excavatedCells.Contains(key)) { State.excavatedCells.Add(key); Note("Excavated " + key + "."); }
            return null;
        }

        public string ExpandFloor(int number) { return ExpandFloor(number, -1); }

        public string ExpandFloor(int number, int side)
        {
            var floor = Floor(number);
            if (floor == null) return "Open this floor first.";
            if (side >= 0) side = 1;
            if (WingCellsOf(number, side) >= WingCells + (side > 0 && number == 0 ? 1 : 0))
                return side < 0 ? "The west wing is fully founded." : "The east wing is fully founded.";
            if (side > 0 && number == 0 && RoomAt(0, GateX) == null) return "Place the Gate first.";
            int x = NextExpansionX(number, side);
            if (number < 0 && !State.excavatedCells.Contains(number + ":" + x))
                return "Excavate that underground cell first.";
            int cost = ExpandCost(number, side);
            if (State.celestium < cost) return "Not enough Celestium.";
            State.celestium -= cost;
            if (side < 0) floor.west++; else floor.east++;
            Note("Expanded floor " + number + (side < 0 ? " westward." : " eastward."));
            return null;
        }

        public string UpgradeLanding(int number, string kind)
        {
            var floor = Floor(number);
            if (floor == null) return "Open this floor first.";
            if (kind != "stairs" && kind != "freight_lift") return "Unknown landing.";
            if (floor.landing == kind || floor.landing == "freight_lift") return "Landing is already upgraded.";
            int cost = kind == "stairs" ? 30 : 120;
            if (State.gold < cost) return "Not enough gold.";
            State.gold -= cost; floor.landing = kind;
            Note("Upgraded floor " + number + " landing to " + kind + ".");
            return null;
        }

        public int Capacity(TowerRoom room) { return room.width * room.level * 2; }

        public int PopulationCap()
        {
            int total = 0;
            foreach (var room in State.rooms)
                if (TowerCatalog.Get(room.type).kind == "living") total += Capacity(room);
            return total;
        }

        public int BiologicalPopulation()
        {
            int total = 0;
            foreach (var resident in State.residents)
                if (resident.origin != "body") total++;
            return total;
        }

        private TowerRoom AvailableHome()
        {
            foreach (var room in State.rooms)
            {
                if (TowerCatalog.Get(room.type).kind != "living") continue;
                int used = 0;
                foreach (var resident in State.residents) if (resident.homeRoom == room.uid) used++;
                if (used < Capacity(room)) return room;
            }
            return null;
        }

        public string Assign(int residentId, int roomUid)
        {
            var resident = Resident(residentId);
            var room = Room(roomUid);
            if (resident == null || room == null) return "Resident or room missing.";
            if (resident.downed || resident.away || resident.exploring) return "This resident is unavailable.";
            if (resident.ageStage != 0) return "Children cannot be assigned to work.";
            var def = TowerCatalog.Get(room.type);
            if (def.kind == "heart") return "The Heart is not a workplace.";
            if (def.kind == "gate" && resident.origin == "body") return "Celestium Bodies cannot stand guard.";
            bool home = def.kind == "living";
            int used = 0;
            foreach (var other in State.residents)
                if (other.id != residentId && (home ? other.homeRoom : other.jobRoom) == roomUid) used++;
            if (used >= Capacity(room)) return "The room is full.";
            if (home) resident.homeRoom = roomUid; else resident.jobRoom = roomUid;
            if (resident.currentRoom == 0) resident.currentRoom = roomUid;
            if (State.tutorialStep == 1 && room.type == "kitchen" && !home)
                State.tutorialStep = 2;
            Note(resident.name + " assigned to " + def.displayName + ".");
            return null;
        }

        public int WorkerCount(int roomUid)
        {
            int count = 0;
            foreach (var resident in State.residents)
                if (resident.jobRoom == roomUid && resident.currentRoom == roomUid &&
                    (resident.currentTask == "production" || resident.currentTask == "guard") && !resident.downed &&
                    !resident.away && !resident.exploring && resident.hp > 0) count++;
            return count;
        }

        public float StockCap()
        {
            float cap = 120;
            foreach (var room in State.rooms)
                if (TowerCatalog.Get(room.type).kind == "storage") cap += 60 * room.width * room.level;
            return cap;
        }

        public float CollectAmount(TowerRoom room)
        {
            var def = TowerCatalog.Get(room.type);
            if (def == null) return 0;
            float mult = room.level == 1 ? 1 : (room.level == 2 ? 1.5f : 2f);
            return def.produces == "celestium" ? room.level :
                (def.produces == "firewood" ? 5 :
                    def.produces == "water" ? 6 : def.produces == "gold" ? 6 : 4) * room.width * mult;
        }

        public string Collect(int roomUid)
        {
            var room = Room(roomUid);
            if (room == null || !room.ready) return "That room is not ready.";
            var def = TowerCatalog.Get(room.type);
            float amount = CollectAmount(room);
            switch (def.produces)
            {
                case "food": State.food = Mathf.Min(StockCap(), State.food + amount); break;
                case "water": State.water = Mathf.Min(StockCap(), State.water + amount); break;
                case "firewood": State.firewood = Mathf.Min(StockCap(), State.firewood + amount); break;
                case "celestium": State.celestium += Mathf.RoundToInt(amount); break;
                case "gold": State.gold += Mathf.RoundToInt(amount); break;
                case "tonics": State.tonics = Mathf.Min(30, State.tonics + Mathf.RoundToInt(amount / 3)); break;
                default: return "This room has nothing to collect.";
            }
            room.ready = false; room.progress = 0;
            if (State.tutorialStep == 2 && room.type == "kitchen") State.tutorialStep = 3;
            if (room.type == "lumber_mill") State.wood += Mathf.Max(1, Mathf.RoundToInt(amount / 5));
            if (room.type == "quarry") { State.stone += room.level * 2; State.ore += room.level; }
            foreach (var resident in State.residents) if (resident.jobRoom == roomUid)
            {
                resident.happiness = Mathf.Min(100, resident.happiness + 2);
                GiveXp(resident, 10 + room.level * 2);
            }
            Note("Collected " + Mathf.RoundToInt(amount) + " " + def.produces + ".");
            Bump("collect");
            Emit("collect", room.uid, 0, def.produces + ":" + Mathf.RoundToInt(amount));
            return null;
        }

        public string UpgradeRoom(int roomUid)
        {
            var room = Room(roomUid);
            if (room == null || room.type == "heart" || room.type == "gate" || room.level >= 3)
                return "That room cannot be upgraded.";
            int cost = (room.level == 1 ? 200 : 600) * room.width;
            if (State.gold < cost) return "Not enough gold.";
            int material = room.level * room.width * 2;
            if (State.wood < material || State.stone < material)
                return "Upgrades need wood and stone.";
            State.gold -= cost; State.wood -= material; State.stone -= material; room.level++;
            Note("Upgraded " + room.type + " to level " + room.level + ".");
            Bump("upgrade");
            Emit("upgrade", room.uid, 0, room.level.ToString());
            return null;
        }

        public string Demolish(int roomUid)
        {
            var room = Room(roomUid);
            if (room == null || room.type == "heart" || room.type == "gate") return "The Heart and Gate stay.";
            if (State.incidents.Exists(i => i.roomUid == roomUid)) return "Resolve the incident first.";
            foreach (var resident in State.residents)
            { if (resident.homeRoom == roomUid) resident.homeRoom = 0; if (resident.jobRoom == roomUid) resident.jobRoom = 0; }
            State.rooms.Remove(room); Note("Demolished " + room.type + "."); return null;
        }

        private float Random01()
        {
            int x = State.randomState;
            x ^= x << 13; x ^= (int)((uint)x >> 17); x ^= x << 5;
            State.randomState = x;
            return ((uint)x & 0x00ffffff) / 16777216f;
        }

        public void Advance(float seconds, bool live)
        {
            if (State.introPhase != "complete" || State.defeated) return;
            float remaining = Mathf.Max(0, seconds);
            while (remaining > 0 && !State.defeated)
            {
                float dt = Mathf.Min(live ? 1f : 30f, remaining);
                Tick(dt, live);
                remaining -= dt;
            }
            State.day = Mathf.Max(State.day, 1 + Mathf.FloorToInt(State.clock / DaySeconds));
        }

        public void CatchUp(long awaySeconds)
        {
            Advance(Mathf.Clamp(awaySeconds, 0, 4 * 3600), false);
            State.savedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public string SendExploring(int residentId)
        {
            return SendExploring(residentId, "supplies");
        }

        public string Recall(int residentId)
        {
            return ReturnExplorer(residentId);
        }
    }

    public static class TowerSaveFiles
    {
        public static string Folder { get { return Path.Combine(Application.persistentDataPath, "AdamsHavenTower"); } }
        public static string PathFor(int slot) { return Path.Combine(Folder, "slot_" + slot.ToString("00") + ".json"); }

        public static TowerState Load(int slot)
        {
            try
            {
                string path = PathFor(slot);
                if (!File.Exists(path)) return null;
                var state = JsonUtility.FromJson<TowerState>(File.ReadAllText(path));
                if (state == null || (state.schema != 1 && state.schema != 2))
                    state = ReadBackup(path);
                if (state == null || (state.schema != 1 && state.schema != 2)) return null;
                Migrate(state);
                new TowerRules(state); return state;
            }
            catch (Exception ex)
            {
                Debug.LogError("Tower save load failed: " + ex);
                var state = ReadBackup(PathFor(slot));
                if (state != null) { Migrate(state); new TowerRules(state); }
                return state;
            }
        }

        public static void Migrate(TowerState state)
        {
            if (state == null || state.schema != 1) return;
            state.schema = 2;
            state.tutorialStep = state.introPhase == "complete" ? 7 : 0;
            state.eventCooldown = 180;
            state.haulingUnlocked = state.policies != null && state.policies.Contains("priority_presets");
            if (state.residents == null) return;
            foreach (var resident in state.residents)
            {
                resident.hunger = resident.thirst = resident.rest = 85;
                resident.priorityProduction = resident.priorityHaul = resident.priorityRepair = 2;
                resident.priorityFire = 3;
                resident.priorityCare = resident.priorityDefense = 2;
                resident.currentTask = "idle";
                resident.currentRoom = resident.jobRoom > 0 ? resident.jobRoom : resident.homeRoom;
                resident.exploreChoice = "supplies";
            }
        }

        private static TowerState ReadBackup(string path)
        {
            try
            {
                string backup = path + ".bak";
                if (!File.Exists(backup)) return null;
                var state = JsonUtility.FromJson<TowerState>(File.ReadAllText(backup));
                return state != null && (state.schema == 1 || state.schema == 2) ? state : null;
            }
            catch (Exception ex) { Debug.LogError("Tower backup load failed: " + ex); return null; }
        }

        public static void Save(TowerState state)
        {
            Directory.CreateDirectory(Folder);
            state.savedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string path = PathFor(state.slot);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(state, true));
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Copy(temp, path, true);
            File.Delete(temp);
        }
    }
}
