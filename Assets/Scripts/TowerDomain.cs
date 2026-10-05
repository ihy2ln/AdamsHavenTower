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
        public bool flip;   // barn only: the anchor bay is on the left, so the barn grows right
        public bool furnished;   // room builder (GDD 22): furniture placed or preset given; never re-preset on load
        public List<TowerFurniture> furniture = new List<TowerFurniture>();
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
        public string duty = "";   // what this worker does in their job room: production, repair or haul
        public int rank;           // summon rank 1 (F) to 9 (SSR); 0 for villagers who walked in through the Gate
        public string schedule = "";
        public float breakSeconds, moodLow;
        public string breakKind = "";
        public int xp;
        public float criticalSeconds;
        // TT 10.30.0 colony depth (TowerColonyDepth.cs): newcomers carry a second trait and a backstory
        public string trait2 = "";
        public string backstory = "";          // TowerRules.Backstories id; may bar one job
        public float leaveSeconds;             // villagers: time spent miserable, toward walking out of the Gate
        public float contentSeconds;           // time spent delighted, toward an inspiration
        public string inspiration = "";        // "work", "care" or "guard" while inspirationSeconds > 0
        public float inspirationSeconds;
        public string posting = "";            // "" at home, "auto" on an auto expedition, "outpost:<region>" stationed
        public float postedSeconds;            // how long this posting has lasted
        // TT 10.3.1 town life (TowerTown.cs): errands to venues ("meal", "drink", "leisure" tasks)
        public float joy = 70;                 // recreation need, refilled at leisure venues
        public string mealMemory = "";         // venue type of the last meal, or "cold" for cold rations
        public string funMemory = "";          // venue type of the last outing
        public float mealMemorySeconds, funMemorySeconds;
        public float playerAssignedAt = -1;    // TT 10.4.3: game clock of the player's last job pick; the Steward leaves it alone a day

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
        public float lootCarry;      // raiders: fraction of a gold coin not yet taken, so theft does not depend on frame rate
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
        public string heartStage = "stable";   // stable, strained or critical (GDD 8.5 warning stages)
        public bool defeated;
        public int runs;                        // how many times a fallen Heart restarted this slot
        public int legacyPoints;                // rogue-lite meta score earned by every fallen run (TowerRules.LegacyEarned)
        public int legacyRank;                  // 0..10, from legacyPoints: the stacking start bonus of this run
        public List<TowerResident> legacyHeroes = new List<TowerResident>();   // heroes carried over from a fallen run
        public List<TowerResident> heartWaiting = new List<TowerResident>();   // summons with no free bed, held in the Heart
        public int summonPity;                  // pulls since the last SSR
        public bool freeSummonUsed;
        public List<TowerFloor> floors = new List<TowerFloor>();
        public List<TowerRoom> rooms = new List<TowerRoom>();
        public List<TowerResident> residents = new List<TowerResident>();
        public List<TowerIncident> incidents = new List<TowerIncident>();
        public List<string> blueprints = new List<string>();
        public List<string> research = new List<string>();     // finished research nodes (TowerResearch.cs), e.g. "CON-3"
        public string researching = "";                         // the node under study, "" when idle
        public long researchEndsUnix;                           // wall-clock end of the current study
        public int researchVersion;                             // TowerRules.ResearchVersion that migrated this save
        public float sigilCarry;                                // Sigil focus fractions waiting for the next grant
        public List<string> policies = new List<string>();
        public List<string> excavatedCells = new List<string>();
        public List<string> log = new List<string>();
        public float threat, festivalSeconds;
        public int goalsClaimed;
        public List<TowerGoal> goals = new List<TowerGoal>();
        public List<string> notifiedGoals = new List<string>();
        public string dailyDate = "";          // local calendar day of the daily board (TowerSigils.cs), yyyy-MM-dd
        public List<TowerDaily> daily = new List<TowerDaily>();
        public bool dailyBonusClaimed;
        public int dailyExpeditionSigils;       // expedition returns that paid Sigils today
        public int dailySkirmishWins;           // dock BATTLE wins that paid spoils today (TowerRules.SkirmishPaidWins)
        public TowerJournal journal = new TowerJournal();   // beasts, events and regions met on expeditions (TowerExpeditionLife.cs)
        public List<TowerCounter> counters = new List<TowerCounter>();
        public List<TowerBond> bonds = new List<TowerBond>();
        public List<TowerMemorial> memorial = new List<TowerMemorial>();
        public int generator;        // TowerMilestones.Version that built a checkpoint slot
        public bool pinned;          // true = a fixed save: TowerMilestones never rebuilds it, whatever the generator version
        public bool steward = true;  // re-staffs food, water and firewood before they run out
        public float stewardTimer;
        public List<TowerWork> works = new List<TowerWork>();   // rooms, wings and floors still under construction
        public List<string> regionsUnlocked = new List<string>();
        public List<string> regionsConquered = new List<string>();
        public bool hasRun;                                     // a guild expedition is under way (run is valid)
        public TowerRun run = new TowerRun();
        public string lastLayout = "";
        public int layoutVersion;    // 1 = two Gates, wings both sides of the Heart (TowerRules.LayoutVersion)
        // TT 10.30.0 colony layer (TowerLayoutTools.cs and the files it introduced)
        public int colonyVersion;    // TowerRules.ColonyVersion that migrated this save
        public int colonyRandom;     // the colony layer's own xorshift stream, so randomState sequences never shift
        public string storyteller = "balanced";   // TowerRules.Storytellers id: calm, balanced or chaotic
        public List<TowerDistrict> districts = new List<TowerDistrict>();   // zoned floor bands (TowerDistricts.cs)
        public int nextDistrictId = 1;
        public float policyCarry;    // district policy upkeep accrued but not yet paid
        public bool policiesLapsed;  // true while the treasury cannot pay policy upkeep
        public bool hasAutoRun;                                 // an auto expedition is out (TowerAutoExpedition.cs)
        public TowerAutoRun autoRun = new TowerAutoRun();
        public string autoReport = "";
        public string autoSigilDate = "";
        public int autoSigilsToday;
        public List<TowerOutpost> outposts = new List<TowerOutpost>();   // staffed colonies in conquered regions
        public List<string> outpostSitesSeen = new List<string>();
        public float siegeCooldown;  // live seconds before Dire threat can draw a Gate siege (TowerSiege.cs)
        public float siegeWarning;   // seconds until a gathering siege hits; 0 when none
        public int siegesWon, siegesLost;
        public bool siegeBattle;     // the heroes are meeting the siege in Battle Mode; the warning waits for them
        // TT 10.3.1 summon banners (TowerBanners.cs). SSR pity stays the shared summonPity above.
        public string summonBanner = "standard";   // the banner the Heart hub last showed
        public bool featuredMissed;  // the last SS/SSR on Featured was not the featured hero: the next one is
        public string pickTarget = "";             // Pick-Your-Hero target (an SS or SSR hero's unit id)
        public bool pickMissed;      // the same guarantee for Pick-Your-Hero; it carries over when the target changes
        public int residentPity;     // Resident banner pulls since the last A or better
        public List<TowerLot> townLots = new List<TowerLot>();   // TT 10.3.3 town lots (TowerTownLots.cs)
        // TT 10.4.0 Dungeon Mode (LairMode.cs and the Lair* files)
        public string mode = "tower";                            // "tower" or "lair"
        public TowerLairState lair = new TowerLairState();       // empty in a Tower save
        public List<TowerReceipt> receipts = new List<TowerReceipt>();   // TT 10.4.1 sell-back window (TowerSellBack.cs)
        public bool suppliesQuestDone;   // TT 10.4.2 story quest: the first supplies expedition (TowerLife.Quests)
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
        // Values follow Shelter.ROOMS in the Godot source. Width is the most bays a building
        // reaches (TOWER_MODE_GDD 5.2): 1 for single-bay buildings, 3 for every other one.
        public static readonly TowerRoomDef[] All = {
            new TowerRoomDef("gate", "Celestium Gate", "gate", 1, 0, "might"),
            new TowerRoomDef("heart", "Celestium Heart", "heart", 1, 0, "grit"),
            new TowerRoomDef("house", "The Shack", "living", 1, 40, "charm"),
            new TowerRoomDef("cottage", "Cottage", "living", 3, 90, "charm"),
            new TowerRoomDef("nursery", "Hearth Nursery", "living", 3, 150, "charm"),
            new TowerRoomDef("terrace_row", "Terrace Row", "living", 3, 220, "charm"),
            new TowerRoomDef("manor", "Ashgrove Manor", "living", 3, 420, "charm"),
            new TowerRoomDef("kitchen", "Kitchen", "produce", 3, 100, "grace", "food"),
            new TowerRoomDef("farmstead", "Farmstead", "produce", 3, 140, "grace", "food", true),
            new TowerRoomDef("well", "Stone Well", "produce", 1, 80, "sight", "water"),
            new TowerRoomDef("lumber_mill", "Lumber Mill", "produce", 3, 120, "might", "firewood"),
            new TowerRoomDef("quarry", "Stone Quarry", "produce", 3, 180, "grit", "celestium", false, true),
            new TowerRoomDef("barn", "Barn", "storage", 3, 150, "grit"),
            new TowerRoomDef("silo", "Grain Silo", "storage", 1, 110, "grit"),
            new TowerRoomDef("warehouse", "Warehouse", "storage", 3, 260, "grit"),
            new TowerRoomDef("frosted_mug", "The Frosted Mug", "medic", 3, 260, "wit", "tonics"),
            new TowerRoomDef("guild_hall", "Silverbrook Adventure Guild", "herald", 3, 320, "charm"),
            new TowerRoomDef("forge", "The Forge", "train", 3, 240, "might"),
            new TowerRoomDef("deck_hall", "Deck Hall", "train", 3, 260, "wit"),
            new TowerRoomDef("market", "Argent Market", "trade", 3, 220, "luck", "gold")
        };

        public static TowerRoomDef Get(string id)
        {
            foreach (var def in All) if (def.id == id) return def;
            return LairCatalog.Get(id);
        }
    }

    // Every building climbs nine ranks (F to SSR) in place (TOWER_MODE_GDD 5.1-5.2). Level 1 is F.
    // Multi-bay buildings are one bay at F-D, two at C-B and three at A-SSR; single-bay ones never widen.
    public static class TowerTiers
    {
        public static readonly string[] Names = { "F", "E", "D", "C", "B", "A", "S", "SS", "SSR" };
        public static readonly string[] BarnNames = Names;
        public const int MaxRank = 9;
        public static int MaxLevel(string type) { return MaxRank; }
        public static bool SingleBay(string type)
        { return type == "gate" || type == "heart" || type == "house" || type == "well" || type == "silo" || LairCatalog.SingleBay(type); }
        public static int Bays(string type, int level)
        { return SingleBay(type) ? 1 : level <= 3 ? 1 : (level <= 5 ? 2 : 3); }
        public static int BarnBays(int level) { return Bays("barn", level); }
        public static string Tier(int level) { return Names[Math.Max(0, Math.Min(Names.Length - 1, level - 1))]; }
        public static string BarnTier(int level) { return Tier(level); }

        // GDD 8.4: the Heart's rank caps the rank of every building (F Heart: D buildings ... SS Heart: SSR).
        private static readonly int[] Caps = { 3, 4, 5, 6, 6, 7, 8, 9, 9 };
        public static int BuildingCap(int heartRank) { return Caps[Math.Max(0, Math.Min(Caps.Length - 1, heartRank - 1))]; }
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
            if (State.introPhase == "complete" && State.tutorialStep == 1 && State.residents != null && State.rooms != null &&
                State.residents.Exists(r => { var job = State.rooms.Find(x => x.uid == r.jobRoom); var d = job == null ? null : TowerCatalog.Get(job.type);
                    return d != null && !string.IsNullOrEmpty(d.produces); }))
                State.tutorialStep = 2;   // someone already works a producing room: the MATCH lesson is done
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
            if (State.works == null) State.works = new List<TowerWork>();
            if (State.receipts == null) State.receipts = new List<TowerReceipt>();
            NormalizeExpeditions();
            if (State.memorial == null) State.memorial = new List<TowerMemorial>();            if (State.randomState == 0) State.randomState = 77101;
            if (State.legacyHeroes == null) State.legacyHeroes = new List<TowerResident>();
            if (State.heartWaiting == null) State.heartWaiting = new List<TowerResident>();
            if (string.IsNullOrEmpty(State.heartStage)) State.heartStage = "stable";
            foreach (var resident in State.residents)
            {
                if (resident.origin == "hero" && resident.rank == 0) resident.rank = 3;   // founding heroes count as D
                if (string.IsNullOrEmpty(resident.schedule)) resident.schedule = "flexible";
                if (string.IsNullOrEmpty(resident.trait) && resident.origin != "body")
                    resident.trait = Traits[(resident.id * 7 + 3) % Traits.Length];
            }
            MigrateLayout();
            if (State.researching == null) State.researching = "";
            MigrateResearch();
            MigrateColony();
            if (State.introPhase == "complete") { RefillGoals(); RefreshDaily(); }
            State.heartRank = Mathf.Clamp(State.heartRank, 1, TowerTiers.MaxRank);
            foreach (var room in State.rooms)
            {
                if (room.furniture == null) room.furniture = new List<TowerFurniture>();
                if (Furnished(room)) continue;   // furnished rooms choose their own width (GDD 22.2)
                // Older saves placed rooms at their full catalogue width; a room is now only as wide as
                // its rank's bays. The rightmost bay stays put and the extra cells on its left are freed.
                int bays = TowerTiers.Bays(room.type, room.level);
                if (room.width > bays) { room.x += room.width - bays; room.width = bays; }
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
        // Room lookups run inside every per-resident loop, so they go through an index instead of a list search.
        // AddRoom and Demolish drop the index; a replaced or resized room list rebuilds it too.
        private Dictionary<int, TowerRoom> roomIndex;
        private List<TowerRoom> indexedRooms;
        private int indexedCount;

        public TowerRoom Room(int uid)
        {
            if (uid <= 0) return null;
            if (roomIndex == null || indexedRooms != State.rooms || indexedCount != State.rooms.Count)
            {
                if (roomIndex == null) roomIndex = new Dictionary<int, TowerRoom>();
                roomIndex.Clear();
                foreach (var r in State.rooms) roomIndex[r.uid] = r;
                indexedRooms = State.rooms;
                indexedCount = State.rooms.Count;
            }
            TowerRoom room;
            return roomIndex.TryGetValue(uid, out room) ? room : null;
        }
        public TowerResident Resident(int id) { return State.residents.Find(r => r.id == id); }
        public TowerRoom RoomAt(int floor, int x)
        { return State.rooms.Find(r => r.floor == floor && x >= r.x && x < r.x + r.width); }

        public TowerRoom AddRoom(string type, int floor, int x, int level = 1)
        {
            var def = TowerCatalog.Get(type);
            if (def == null) return null;
            var room = new TowerRoom { uid = State.nextRoomUid++, type = type, floor = floor,
                x = x, width = TowerTiers.Bays(type, level), level = level };
            // East of the Heart a room grows its new bays outward (to the right), away from the shaft.
            if (x > CoreX && type != "gate" && type != "heart") room.flip = true;
            if (Furnished(room)) FurnishPreset(room, level);   // a new room comes with its starter furniture
            State.rooms.Add(room);
            roomIndex = null;
            TouchLayout();
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
            State.introPhase = IsLair ? "lair_site" : "gate"; Note("The Heart awakened."); return null;
        }

        public string PlaceIntroGate()
        {
            if (State.introPhase != "gate") return "Awaken the Heart first.";
            if (RoomAt(0, GateX) == null) AddRoom("gate", 0, GateX);
            State.layoutVersion = LayoutVersion;
            State.introPhase = "shack"; Note("The Gate joined the Heart."); return null;
        }

        public string ChooseStarter(string unitId)
        {
            if (State.introPhase != "choose") return "Place the Gate and Shack first.";
            if (unitId != "kaela" && unitId != "ghislaine" && unitId != "elara")
                return "Choose Kaela, Ghislaine, or Elara.";
            AddResident(unitId, unitId == "kaela" ? "Kaela" :
                (unitId == "ghislaine" ? "Ghislaine" : "Elara"), "hero", 1).rank = 3;
            TowerRoom shack = State.rooms.Find(r => r.type == "house" && r.floor == StarterFloor);
            if (shack != null)
            {
                var starter = State.residents[State.residents.Count - 1];
                starter.homeRoom = starter.currentRoom = shack.uid;
            }
            State.introPhase = "complete";
            State.tutorialStep = 0;
            PlaceGates();
            ReturnLegacyHeroes();
            RefillGoals();
            RefreshDaily();
            foreach (string id in StartingBlueprints)
                if (!State.blueprints.Contains(id)) State.blueprints.Add(id);
            State.researchVersion = ResearchVersion;   // a new tower unlocks the rest through research
            // Tower-only progression needs a foundation and income path without battle rewards.
            State.celestium += 45;
            State.gold += 650;
            State.wood += 20;
            State.stone += 12;
            Note("The Tower opened. Survival rooms, 650 gold and 45 Celestium were granted.");
            if (IsLair) LairFoundedNow();
            else if (OneWorld) FoundDungeonBand();   // one world: the dungeon opens under the new Tower (GDD 21.1)
            return null;
        }

        public TowerResident AddResident(string unitId, string name, string origin, int level)
        {
            var resident = NewResident(unitId, name, origin, level);
            State.residents.Add(resident);
            return resident;
        }

        // A resident not yet placed anywhere (summons decide between the Tower and the Heart's waiting list).
        private TowerResident NewResident(string unitId, string name, string origin, int level)
        {
            var resident = new TowerResident { id = State.nextResidentId++, unitId = unitId,
                name = name, origin = origin, level = level };
            if (origin == "body") resident.charge = 2160;
            resident.schedule = "flexible";
            if (origin != "body") resident.trait = Traits[(resident.id * 7 + 3) % Traits.Length];
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
            // Buildings now unlock through the Construction research branch (GDD 8.2).
            var node = UnlockNode(type);
            if (node != null) return "Research " + node.id + " " + node.name + " at the Heart to unlock the " + def.displayName + ".";
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
            int footprint = TowerTiers.Bays(type, 1);
            bool westSide = x + footprint <= CoreX, eastSide = x > CoreX;
            if (!westSide && !eastSide) return "Rooms cannot cover the Heart shaft.";
            if (x < MinBuildX || x + footprint - 1 > MaxBuildX) return "Rooms stay within the ten-cell wings.";
            var f = Floor(floor);
            if (f == null) return "Open this floor first.";
            if (westSide && x < CoreX - f.west) return "Expand the Celestium foundation west first.";
            if (eastSide && x + footprint - 1 > CoreX + f.east) return "Expand the Celestium foundation east first.";
            string zone = ZoneReason(def, floor);
            if (zone != null) return zone;
            for (int cx = x; cx < x + footprint; cx++)
                if (RoomAt(floor, cx) != null || WorkRoomAt(floor, cx) != null) return "Another room occupies that space.";
            if (westSide && x + footprint != CoreX && RoomAt(floor, x + footprint) == null &&
                WorkRoomAt(floor, x + footprint) == null)
                return "Build contiguously outward from the Heart shaft.";
            if (eastSide && x != CoreX + 1 && RoomAt(floor, x - 1) == null && WorkRoomAt(floor, x - 1) == null)
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
            int gold = BuildCost(type), wood = BuildWoodCost(type), stone = BuildStoneCost(type);
            State.gold -= gold;
            State.wood -= wood;
            State.stone -= stone;
            if (Timed)
            {
                float seconds = RoomBuildSeconds(type);
                StartWork("room", floor, x, 0, type, seconds);
                PriceLastWork(gold, wood, stone, 0);
                Note("Started building " + TowerCatalog.Get(type).displayName + " on floor " + floor +
                    " (" + Clock(seconds) + ").");
                return null;
            }
            var built = AddRoom(type, floor, x);
            RecordReceipt(built, "build", gold, wood, stone);
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
            if (IsLair) return LairDigFloor();
            // One world: below ground the Tower digs living floors above the vault; dungeon floors come from DIG DUNGEON FLOOR.
            if (Merged && number < 0) return LairAddLivingFloor();
            if (number < FloorMin || number > FloorMax) return "Floor out of range.";
            if (Floor(number) != null) return "Floor already open.";
            if (FloorWork(number) != null) return "That floor is already being built.";
            if (Floor(number - 1) == null && Floor(number + 1) == null) return "Open floors outward from the Heart.";
            string capped = FloorCapReason(number);   // GDD 8.4: the Heart's rank sets how far the tower reaches
            if (capped != null) return capped;
            int cost = FloorOpenCost(number);
            if (State.celestium < cost) return "Not enough Celestium.";
            State.celestium -= cost;
            if (Timed)
            {
                float seconds = FloorBuildSeconds(number);
                StartWork("floor", number, 0, 0, null, seconds);
                PriceLastWork(0, 0, 0, cost);
                Note("Started building floor " + number + " (" + Clock(seconds) + ").");
                return null;
            }
            State.floors.Add(new TowerFloor { number = number });
            Note("Opened floor " + number + ".");
            return null;
        }

        // side: -1 grows the west wing, +1 the east wing.
        public int ExpandCost(int number, int side = -1)
        {
            var floor = Floor(number);
            if (floor == null) return 0;
            int cells = side < 0 ? floor.west : floor.east;
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

        // No side given: the ground floor takes its shorter side (west on a tie), other floors grow west.
        public string ExpandFloor(int number)
        {
            var floor = Floor(number);
            int side = number == 0 && floor != null && floor.east < floor.west ? 1 : -1;
            return ExpandFloor(number, side);
        }

        public string ExpandFloor(int number, int side)
        {
            var floor = Floor(number);
            if (floor == null) return "Open this floor first.";
            side = side < 0 ? -1 : 1;
            if (WingWork(number, side) != null) return "That foundation is already being extended.";
            if (WingCellsOf(number, side) >= WingCells)
                return side < 0 ? "The west wing is fully founded." : "The east wing is fully founded.";
            // The Heart stays between the two Gates: on the ground floor a wing may run at most one cell ahead.
            if (number == 0 && WingCellsOf(0, side) > WingCellsOf(0, -side))
                return "Extend the " + (side < 0 ? "east" : "west") + " side next, so the Heart stays in the middle.";
            int x = NextExpansionX(number, side);
            if (number < 0 && !State.excavatedCells.Contains(number + ":" + x))
                return "Excavate that underground cell first.";
            int cost = ExpandCost(number, side);
            if (State.celestium < cost) return "Not enough Celestium.";
            State.celestium -= cost;
            if (Timed)
            {
                float seconds = WingBuildSeconds(number, side);
                StartWork("wing", number, 0, side, null, seconds);
                PriceLastWork(0, 0, 0, cost);
                if (number == 0) PlaceGates();   // the Gate steps out of the way as soon as work starts
                Note("Started extending floor " + number + (side < 0 ? " westward" : " eastward") +
                    " (" + Clock(seconds) + ").");
                return null;
            }
            if (side < 0) floor.west++; else floor.east++;
            if (number == 0) PlaceGates();
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

        // Two places per bay, plus one more for every rank above F (GDD 5.3; counts are a first pass).
        public int Capacity(TowerRoom room)
        {
            int places = Furnished(room) ? (HousesByDefault(room) ? HomePlaces(room) : JobPlaces(room)) :
                room.width * 2 + room.level - 1;
            var def = TowerCatalog.Get(room.type);
            if (def != null && def.kind == "living" && Researched("SET-3")) places = Mathf.CeilToInt(places * 1.1f);   // Better beds
            if (room.type == "gate" && Researched("DEF-3")) places++;                                                    // third guard
            return places;
        }

        // GDD 8.4: how many people the Heart can shelter at each rank (F..SSR), whatever the housing.
        private static readonly int[] DwellerCaps = { 8, 14, 22, 32, 44, 58, 74, 92, 110 };
        public static int DwellerCap(int heartRank) { return DwellerCaps[Mathf.Clamp(heartRank, 1, TowerTiers.MaxRank) - 1]; }

        // Beds in every home.
        public int HousingCap()
        {
            int total = 0;
            foreach (var room in State.rooms)
            {
                if (Furnished(room)) total += BedsIn(room);   // beds count in any room (GDD 22.3)
                else if (TowerCatalog.Get(room.type).kind == "living") total += Capacity(room);
            }
            return total;
        }

        // Newcomers (Gate arrivals, births, summons leaving the Heart) need a bed and room under the Heart's cap.
        // A tower already above the cap (older saves, checkpoints) keeps everyone; it just stops growing.
        public int PopulationCap() { return Mathf.Min(HousingCap(), DwellerCap(State.heartRank)); }

        public bool HeartLimitsPopulation() { return DwellerCap(State.heartRank) < HousingCap(); }

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
                int beds = Furnished(room) ? BedsIn(room) : TowerCatalog.Get(room.type).kind == "living" ? Capacity(room) : 0;
                if (beds <= 0) continue;
                int used = 0;
                foreach (var resident in State.residents) if (resident.homeRoom == room.uid) used++;
                if (used < beds) return room;
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
            if (def.kind == "lair") return "Dungeon rooms are run by traps and monsters.";
            if (def.kind == "gate" && resident.origin == "body") return "Celestium Bodies cannot stand guard.";
            bool home = HousesByDefault(room);
            int used = 0;
            foreach (var other in State.residents)
                if (other.id != residentId && (home ? other.homeRoom : other.jobRoom) == roomUid) used++;
            if (used >= Capacity(room)) return "The room is full.";
            if (home) resident.homeRoom = roomUid; else resident.jobRoom = roomUid;
            if (!home) resident.duty = BestDuty(resident, room);
            if (resident.currentRoom == 0) resident.currentRoom = roomUid;
            // Any producing room counts for the MATCH lesson, by button or by dragging the resident there (TT 10.4.3).
            if (State.tutorialStep == 1 && !home && !string.IsNullOrEmpty(Product(room)))
                State.tutorialStep = 2;
            Note(resident.name + " assigned to " + def.displayName +
                (home ? "." : ": " + DutyLabel(resident, room) + "."));
            return null;
        }

        // A worker does whatever their strongest applicable skill suits in the room they are sent to.
        public string BestDuty(TowerResident resident, TowerRoom room)
        {
            var def = TowerCatalog.Get(room.type);
            string best = "repair";
            int score = resident.might;
            // Workplaces that produce or train always get a producer (TT 10.4.1: a strong newcomer used to be made the
            // room's repairer and produced nothing); hauling only once auto-hauling is unlocked.
            if (def.kind == "gate" || def.kind == "train" || !string.IsNullOrEmpty(Product(room)))
            { best = "production"; score = resident.Stat(def.stat); }
            if (!string.IsNullOrEmpty(Product(room)) && State.haulingUnlocked && resident.grit > score + 2) best = "haul";
            return best;
        }

        public string DutyLabel(TowerResident resident, TowerRoom room)
        {
            var def = TowerCatalog.Get(room.type);
            switch (resident.duty)
            {
                case "repair": return "repairs (Might " + resident.might + ")";
                case "haul": return "hauls output (Grit " + resident.grit + ")";
                default: return (def.kind == "gate" ? "guards" : def.kind == "train" ? "trains" : "works") +
                    " (" + char.ToUpperInvariant(def.stat[0]) + def.stat.Substring(1) + " " + resident.Stat(def.stat) + ")";
            }
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
            {
                if (Furnished(room)) cap += TowerFurnishing.StoragePerUnit * room.level * StorageWeight(room);
                else if (TowerCatalog.Get(room.type).kind == "storage") cap += 60 * OutputBays(room) * room.level;
            }
            return cap * StockCapBonus();
        }

        // Everything one collect yields (a furnished room may make several resources).
        public float CollectAmount(TowerRoom room)
        {
            var def = room == null ? null : TowerCatalog.Get(room.type);
            if (def == null) return 0;
            if (!Furnished(room)) return BaseCollect(room, def.produces);
            float total = 0;
            foreach (var resource in ProductWeights(room).Keys) total += YieldOf(room, resource);
            return total;
        }

        // A full collect of one resource at this room's rank and width.
        private float BaseCollect(TowerRoom room, string resource)
        {
            if (string.IsNullOrEmpty(resource)) return 0;
            float mult = (1 + 0.5f * (room.level - 1)) * YieldBonus(resource);
            // Quarries: one Celestium per collect per rank up to D, then half a rank's worth (slower Heart climb).
            return resource == "celestium" ? QuarryCelestium(room.level) :
                (resource == "firewood" ? 5 :
                    resource == "water" ? 6 : resource == "gold" ? 6 : 4) * OutputBays(room) * mult;
        }

        public static int QuarryCelestium(int level) { return level <= 2 ? level : 2 + (level - 2) / 2; }

        // First-pass balance for GDD 5.2: rooms now open as one bay, but output and storage start from
        // the old fixed widths so a rank F room is no weaker than before. Extra bays only add beyond that.
        private static int OutputBays(TowerRoom room)
        {
            switch (room.type)
            {
                case "kitchen": case "farmstead": return Mathf.Max(2, room.width);
                case "lumber_mill": case "quarry": case "warehouse": case "frosted_mug": case "market":
                    return Mathf.Max(3, room.width);
                default: return room.width;
            }
        }

        public string Collect(int roomUid)
        {
            var room = Room(roomUid);
            if (room == null || !room.ready) return "That room is not ready.";
            var def = TowerCatalog.Get(room.type);
            var products = new List<string>();
            if (Furnished(room)) products.AddRange(ProductWeights(room).Keys);
            else if (!string.IsNullOrEmpty(def.produces)) products.Add(def.produces);
            var parts = new List<string>();
            float total = 0;
            foreach (var resource in products)
            {
                float amount = YieldOf(room, resource);
                if (!AddCollected(resource, amount)) continue;
                total += amount;
                parts.Add(Mathf.RoundToInt(amount) + " " + resource);
                // Side yields follow what was made, wherever it was made.
                if (resource == "firewood") { State.wood += Mathf.Max(1, Mathf.RoundToInt(amount / 5)); State.stone += 1; }   // cleared rubble: a trickle of early stone
                if (resource == "celestium") { State.stone += room.level * 2; State.ore += room.level; }
            }
            if (parts.Count == 0) return "This room has nothing to collect.";
            room.ready = false; room.progress = 0;
            if (State.tutorialStep == 2 && (room.type == "kitchen" || room.type == "well")) State.tutorialStep = 3;
            foreach (var resident in State.residents) if (resident.jobRoom == roomUid)
            {
                resident.happiness = Mathf.Min(100, resident.happiness + 2);
                GiveXp(resident, 10 + room.level * 2);
            }
            Note("Collected " + string.Join(", ", parts.ToArray()) + ".");
            Bump("collect");
            Emit("collect", room.uid, 0, products[0] + ":" + Mathf.RoundToInt(total));
            return null;
        }

        private bool AddCollected(string resource, float amount)
        {
            switch (resource)
            {
                case "food": State.food = Mathf.Min(StockCap(), State.food + amount); return true;
                case "water": State.water = Mathf.Min(StockCap(), State.water + amount); return true;
                case "firewood": State.firewood = Mathf.Min(StockCap(), State.firewood + amount); return true;
                case "celestium": State.celestium += Mathf.RoundToInt(amount) + (Researched("PRO-8") ? 1 : 0); return true;
                case "gold": State.gold += Mathf.RoundToInt(amount); return true;
                case "tonics": State.tonics = Mathf.Min(30, State.tonics + Mathf.RoundToInt(amount / 3) + (Researched("DEF-6") ? 1 : 0)); return true;
            }
            return false;
        }

        public int MaxLevel(TowerRoom room) { return TowerTiers.MaxLevel(room.type); }

        // The highest building rank the Heart currently allows.
        public int RankCap() { return TowerTiers.BuildingCap(State.heartRank); }

        public string LevelLabel(TowerRoom room) { return "Rank " + TowerTiers.Tier(room.level); }

        // GDD 5.2: the rightmost bay is fixed and every new bay is added on its left. When that cell is
        // taken, unfounded or across the shaft, the upgrade waits until the player clears it.
        // Barns from older saves that already grew to the right (flip) keep growing that way.
        public string BayGrowth(TowerRoom room, int newWidth, out int newX)
        {
            int grow = newWidth - room.width;
            newX = room.flip ? room.x : room.x - grow;
            int from = room.flip ? room.x + room.width : room.x - grow;
            bool westSide = room.x + room.width <= CoreX;
            for (int cx = from; cx < from + grow; cx++)
            {
                bool free = cx >= MinBuildX && cx <= MaxBuildX && IsFounded(room.floor, cx) &&
                    (westSide ? cx < CoreX : cx > CoreX) &&
                    RoomAt(room.floor, cx) == null && WorkRoomAt(room.floor, cx) == null;
                if (!free)
                    return TowerCatalog.Get(room.type).displayName + " needs the cell " +
                        (room.flip ? "to its right" : "to its left") + " free and founded to grow to rank " +
                        TowerTiers.Tier(room.level + 1) + " (" + newWidth + " bays).";
            }
            return null;
        }

        public int UpgradeGoldCost(TowerRoom room)
        {
            int newWidth = TowerTiers.Bays(room.type, room.level + 1);
            int cost = (room.level == 1 ? 200 : 600 * (room.level - 1)) * newWidth;
            return Researched("CON-8") ? Mathf.RoundToInt(cost * 0.9f) : cost;
        }

        public int UpgradeMaterialCost(TowerRoom room)
        { return room.level * TowerTiers.Bays(room.type, room.level + 1) * 2; }

        public string UpgradeRoom(int roomUid)
        {
            var room = Room(roomUid);
            if (room == null || room.type == "heart" || room.type == "gate" || room.level >= MaxLevel(room))
                return "That room cannot be upgraded.";
            if (Furnished(room)) return "Furnish the room to raise its rank.";   // GDD 22.4: furniture makes the rank
            if (room.level >= RankCap())
            {
                int needed = State.heartRank;
                while (needed < TowerTiers.MaxRank && TowerTiers.BuildingCap(needed) <= room.level) needed++;
                return "Raise the Celestium Heart to rank " + TowerTiers.Tier(needed) + " to upgrade past rank " +
                    TowerTiers.Tier(room.level) + ".";
            }
            if (room.level + 1 >= TowerTiers.MaxRank && !Researched("CON-7"))
                return "Research CON-7 Celestium fittings at the Heart to upgrade to rank SSR.";
            int newWidth = TowerTiers.Bays(room.type, room.level + 1);
            int newX = room.x;
            if (newWidth > room.width)
            {
                string blocked = BayGrowth(room, newWidth, out newX);
                if (blocked != null) return blocked;
            }
            int cost = UpgradeGoldCost(room);
            if (State.gold < cost) return "Not enough gold.";
            int material = UpgradeMaterialCost(room);
            if (State.wood < material || State.stone < material)
                return "Upgrades need wood and stone.";
            int fromLevel = room.level, fromX = room.x, fromWidth = room.width;
            State.gold -= cost; State.wood -= material; State.stone -= material; room.level++;
            room.x = newX; room.width = newWidth;
            RecordReceipt(room, "upgrade", cost, material, material, fromLevel, fromX, fromWidth);
            TouchLayout();
            Note("Upgraded" + TowerCatalog.Get(room.type).displayName + " to rank " + TowerTiers.Tier(room.level) + ".");
            Bump("upgrade");
            Emit("upgrade", room.uid, 0, room.level.ToString());
            return null;
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
            RefreshAllRoomRanks();   // the Heart or CON-7 may have lifted a furnished room's cap
            RefreshDaily();
            TickResearch();
            TickAutoExpedition();
            int dayBefore = State.day;
            float remaining = Mathf.Max(0, seconds);
            while (remaining > 0 && !State.defeated)
            {
                float dt = Mathf.Min(live ? 1f : 30f, remaining);
                Tick(dt, live);
                remaining -= dt;
            }
            // Days turn over at midnight: the clock starts at 06:00, a quarter of a day in (TT 10.4.2).
            State.day = Mathf.Max(State.day, 1 + Mathf.FloorToInt((State.clock + DaySeconds * 0.25f) / DaySeconds));
            // SET-6 Festival: every second day opens with a harvest festival.
            if (State.day != dayBefore && State.day % 2 == 0 && Researched("SET-6") && State.festivalSeconds <= 0)
            {
                State.festivalSeconds = 90;
                Note("Day " + State.day + " opens with a harvest festival.");
                Emit("festival", 0, 0, "");
            }
        }

        // Game seconds to simulate for one rendered frame. The frame hitch cap applies to the real time, not to the
        // speed-multiplied time, so 4x and 8x stay proportional on a slow frame rate. Speed 0 (paused) gives 0.
        public static float FrameSeconds(float deltaTime, float speed)
        {
            return Mathf.Min(Mathf.Max(0f, deltaTime), 0.25f) * Mathf.Max(0f, speed);
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
            var resident = Resident(residentId);
            if (IsPosted(resident)) return resident.posting == "auto" ? RecallAuto() : Unstation(residentId);
            return ReturnExplorer(residentId);
        }
    }

    public static class TowerSaveFiles
    {
        public static string FolderFor(string mode) { return Path.Combine(Application.persistentDataPath, TowerModes.FolderName(mode)); }
        public static string Folder { get { return FolderFor(TowerModes.Current); } }
        public static string PathFor(int slot) { return PathFor(TowerModes.Current, slot); }
        public static string PathFor(string mode, int slot) { return Path.Combine(FolderFor(mode), "slot_" + slot.ToString("00") + ".json"); }

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
            string mode = state.mode == TowerModes.Lair ? TowerModes.Lair : TowerModes.Tower;   // the save's own folder
            Directory.CreateDirectory(FolderFor(mode));
            state.savedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string path = PathFor(mode, state.slot);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(state, true));
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Copy(temp, path, true);
            File.Delete(temp);
        }
    }
}
