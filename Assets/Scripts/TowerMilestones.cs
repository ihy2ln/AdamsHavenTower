using System;
using UnityEngine;

namespace AdamsHaven.Tower
{
    public static class TowerMilestones
    {
        // Bump when checkpoint generation changes; older unplayed checkpoint files are rebuilt.
        public const int Version = 3;
        // Testing aid: every checkpoint (slots 2-10) gets a Barn, Silo and Warehouse at each rank F to SSR on extra
        // floors above the tower, full stocks (at the cap those storage rooms create) and full resident, room and Heart
        // conditions. These checkpoints are also pinned (TowerState.pinned), so they are never rebuilt or reset by the
        // generator once saved. Set to false to get the original progression-shaped checkpoints back: unpin or delete
        // the slot files, then bump Version.
        public const bool MaxedForTesting = true;
        public static readonly int[] Days = { 1, 10, 20, 30, 40, 50, 60, 70, 80, 90 };
        public static readonly string[] Labels = {
            "Founding Day", "First Hearths", "Working Tower", "Established Haven",
            "Western Wing", "Deepworks", "Skyward Bastion", "Living Citadel",
            "Celestium Ascendant", "Fully Developed Tower"
        };
        private static readonly int[] Up = { 0, 1, 2, 4, 7, 10, 13, 17, 21, 24 };
        private static readonly int[] Down = { 0, 1, 2, 3, 5, 8, 11, 15, 20, 24 };
        private static readonly int[] People = { 0, 12, 18, 24, 32, 38, 44, 50, 56, 60 };
        private static readonly int[] Gold = { 120, 900, 3500, 8000, 14000, 25000, 50000, 125000, 350000, 1000000 };
        private static readonly string[] HeroIds = { "kaela", "ghislaine", "elara", "helda", "daisy", "clarity", "amara" };
        private static readonly string[] HeroNames = { "Kaela", "Ghislaine", "Elara", "Helda", "Daisy", "Clarity", "Amara" };
        private static readonly string[] BodyVariants = { "normal", "short", "tall", "muscle", "hourglass" };

        public static TowerState Create(int slot)
        {
            int index = Mathf.Clamp(slot - 1, 0, 9);
            var rules = TowerRules.New(slot);
            TowerState state = rules.State;
            state.generator = Version;
            state.pinned = MaxedForTesting && index >= 1;   // maxed test checkpoints stay exactly as saved
            state.day = Days[index];
            state.label = Labels[index];
            state.clock = (Days[index] - 1) * TowerRules.DaySeconds;
            state.gold = Gold[index];
            state.celestium = new[] { 0, 12, 35, 80, 130, 220, 400, 900, 2200, 5000 }[index];
            state.sigils = new[] { 10, 20, 40, 80, 120, 160, 240, 400, 800, 1200 }[index];   // 10 per pull
            state.wood = state.stone = state.ore = state.essence = index * index * 60;
            if (index == 0) return state; // Slot 1 starts before touching the Heart.

            state.introPhase = "complete";
            state.tutorialStep = 7;
            rules.AddRoom("gate", 0, TowerRules.GateX);
            // Checkpoints climb the GDD 8.4 ladder: building ranks never exceed what the Heart allows.
            state.heartRank = new[] { 1, 1, 2, 2, 3, 4, 6, 7, 8, 9 }[index];
            state.heartHp = TowerRules.HeartMaxHp(state.heartRank);
            int rank = Mathf.Min(TowerTiers.BuildingCap(state.heartRank), new[] { 1, 1, 1, 2, 3, 4, 5, 6, 7, 8 }[index]);
            state.blueprints.Clear();
            foreach (var def in TowerCatalog.All)
                if (def.kind != "heart" && def.kind != "gate" && (index >= 3 ||
                    def.id == "house" || def.id == "kitchen" || def.id == "well" ||
                    def.id == "lumber_mill" || def.id == "barn" || def.id == "nursery")) state.blueprints.Add(def.id);
            if (index >= 2)
            {
                state.policies.Add("priority_presets"); state.policies.Add("auto_repairs");
                state.policies.Add("auto_haul"); state.haulingUnlocked = true;
            }
            if (index >= 5)
                foreach (var policy in new[] { "night_coordination", "emergency_recall", "celestium_wards", "trauma_recovery" })
                    state.policies.Add(policy);

            state.floors.Clear();
            for (int floor = -Down[index]; floor <= Up[index]; floor++)
            {
                state.floors.Add(new TowerFloor { number = floor, west = TowerRules.WingCells,
                    east = floor == 0 ? 1 : 0,
                    landing = index >= 5 && floor % 4 == 0 ? "freight_lift" :
                        (index >= 2 ? "stairs" : "energy") });
                // Producers go first so wide high-rank rooms never crowd the mill or kitchen out of the wing.
                string[] rooms = floor == 0 ? new[] { "lumber_mill", "kitchen", "well", "house", "cottage" } :
                    floor == 1 ? new[] { "terrace_row", "market", "well", "cottage", "house" } :
                    floor > 0 ? (floor % 2 == 0 ?
                        new[] { "manor", "deck_hall", "forge", floor % 4 == 2 ? "nursery" : "cottage" } :
                        new[] { "terrace_row", "market", "guild_hall" }) :
                    (floor % 2 == 0 ? new[] { "quarry", "warehouse", "cottage", "barn" } :
                        new[] { "quarry", "frosted_mug", "terrace_row" });
                int used = 0;
                foreach (string type in rooms)
                {
                    int width = TowerTiers.Bays(type, rank);
                    if (used + width > TowerRules.WingCells) continue;
                    int x = TowerRules.CoreX - used - width;
                    used += width;
                    var room = rules.AddRoom(type, floor, x, rank);
                    room.condition = 92 + (room.uid * 7) % 9;
                }
                // A second mix fills the rest of the west wing (the Heart and Gate are the right edge).
                string[] east = floor == 0 ? new[] { "cottage", "lumber_mill", "farmstead", "barn" } :
                    floor > 0 ? (floor % 2 == 0 ? new[] { "cottage", "kitchen", "well", "cottage" } :
                        new[] { "terrace_row", "well", "market", "cottage" }) :
                    (floor % 2 == 0 ? new[] { "warehouse", "quarry", "barn", "silo" } :
                        new[] { "cottage", "quarry", "warehouse", "well" });
                foreach (string type in east)
                {
                    var eastDef = TowerCatalog.Get(type);
                    if (eastDef.groundOnly && floor != 0) continue;
                    if (eastDef.undergroundOnly && floor >= 0) continue;
                    int eastWidth = TowerTiers.Bays(type, rank);
                    if (used + eastWidth > TowerRules.WingCells) continue;
                    int x = TowerRules.CoreX - used - eastWidth;
                    used += eastWidth;
                    var room = rules.AddRoom(type, floor, x, rank);
                    room.condition = 90 + (room.uid * 5) % 10;
                }
            }

            int heroCount = Mathf.Min(HeroIds.Length, index < 2 ? 2 + index : 3 + index / 2);
            for (int i = 0; i < heroCount; i++)
                rules.AddResident(HeroIds[i], HeroNames[i], "hero", Mathf.Max(1, index * 5));
            int bodyIndex = 0;
            for (int i = state.residents.Count; i < People[index]; i++)
            {
                string[] professions = { "Miner", "Builder", "Cook", "Scout", "Herald", "Scholar", "Trader" };
                var resident = rules.AddResident("", professions[i % professions.Length] + " " + (i + 1),
                    i % 9 == 0 && index >= 4 ? "body" : "villager", 1 + index * 3);
                if (resident.origin == "body")
                {
                    resident.chassisVariant = BodyVariants[bodyIndex++ % BodyVariants.Length];
                    resident.name = "Celestium " + resident.chassisVariant + " " + resident.id;
                }
                resident.might = 2 + i % 5; resident.sight = 2 + (i + 1) % 5;
                resident.grit = 2 + (i + 2) % 5; resident.charm = 2 + (i + 3) % 5;
                resident.wit = 2 + (i + 4) % 5; resident.grace = 2 + (i + 5) % 5;
                resident.luck = 2 + (i + 6) % 5;
                resident.tool = index >= 3 ? 1 + index / 4 : 0;
                resident.weapon = index >= 4 ? 1 + index / 5 : 0;
                resident.currentRoom = 0;
            }
            var homes = state.rooms.FindAll(r => TowerCatalog.Get(r.type).kind == "living");
            var jobs = state.rooms.FindAll(r =>
                TowerCatalog.Get(r.type).kind != "living" &&
                TowerCatalog.Get(r.type).kind != "heart" &&
                TowerCatalog.Get(r.type).kind != "gate");
            var groundHomes = homes.FindAll(r => r.floor == 0);
            var groundJobs = jobs.FindAll(r => r.floor == 0);
            int heroIndex = 0;
            foreach (var resident in state.residents)
            {
                if (resident.origin != "body")
                {
                    bool housed = resident.origin == "hero" && AssignFirst(rules, resident.id, groundHomes);
                    if (!housed) AssignFirst(rules, resident.id, homes);
                }
                bool working = resident.origin == "hero" &&
                    AssignFromOffset(rules, resident.id, groundJobs, heroIndex++);
                if (resident.origin == "body")
                {
                    string target = BodyTaskRoom(resident.chassisVariant);
                    working = AssignFirst(rules, resident.id, jobs.FindAll(r => r.type == target));
                }
                if (!working) AssignFirst(rules, resident.id, jobs);
            }
            if (People[index] >= 38)
            {
                var upperWell = state.rooms.Find(r => r.type == "well" && r.floor == 1);
                if (upperWell != null)
                {
                    var waterCandidates = state.residents.FindAll(r => r.origin == "villager");
                    waterCandidates.Sort((a, b) => b.sight.CompareTo(a.sight));
                    for (int i = 0; i < 4 && i < waterCandidates.Count; i++)
                        rules.Assign(waterCandidates[i].id, upperWell.uid);
                }
            }
            // Checkpoints must be able to feed themselves: move workers until the stocks hold.
            rules.StaffForSurvival(80);
            if (index >= 2)
            {
                var gate = state.rooms.Find(r => r.type == "gate");
                var guards = state.residents.FindAll(r => r.origin == "villager");
                guards.Sort((a, b) => (b.might + b.weapon).CompareTo(a.might + a.weapon));
                for (int i = 0; gate != null && i < 2 && i < guards.Count; i++)
                    rules.Assign(guards[i].id, gate.uid);
                rules.StaffForSurvival(20);
            }
            if (MaxedForTesting) AddStorageRanks(rules, Up[index]);
            float fill = MaxedForTesting ? 1f : Mathf.Min(1, 0.55f + index * 0.05f);
            state.food = state.water = state.firewood = rules.StockCap() * fill;
            state.eventCooldown = 180 + index * 15;
            state.tonics = Mathf.Min(30, 2 + index * 3);
            if (MaxedForTesting) MaxEverything(rules);
            state.log.Clear();
            rules.Note(state.label + " — a safe day " + state.day + " Tower checkpoint.");
            state.savedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return state;
        }

        // One Barn, Silo and Warehouse at every rank F to SSR, west of the Heart, on new floors above `topFloor`.
        // Added after staffing so no resident is assigned to a storage room. Placement ignores the Heart rank cap.
        private static void AddStorageRanks(TowerRules rules, int topFloor)
        {
            var state = rules.State;
            string[] types = { "barn", "silo", "warehouse" };
            int floor = topFloor + 1, cursor = TowerRules.CoreX - 1;
            TowerFloor current = null;
            for (int level = 1; level <= TowerTiers.MaxRank; level++)
                foreach (string type in types)
                {
                    int width = TowerTiers.Bays(type, level);
                    if (current == null || cursor - width + 1 < TowerRules.MinBuildX)
                    {
                        if (current != null) floor++;
                        current = rules.Floor(floor);
                        if (current == null)
                        {
                            current = new TowerFloor { number = floor, west = TowerRules.WingCells, east = 0, landing = "stairs" };
                            state.floors.Add(current);
                        }
                        current.west = Mathf.Max(current.west, TowerRules.WingCells);
                        cursor = TowerRules.CoreX - 1;
                    }
                    var room = rules.AddRoom(type, floor, cursor - width + 1, level);
                    room.condition = 100;
                    cursor -= width;
                }
        }

        // Everything at its best: stock at the cap, residents, rooms and the Heart at full health, no danger.
        private static void MaxEverything(TowerRules rules)
        {
            var state = rules.State;
            float cap = rules.StockCap();
            state.food = state.water = state.firewood = cap;
            state.wood = state.stone = state.ore = state.essence = Mathf.RoundToInt(cap);
            state.gold = 999999; state.celestium = 9999; state.sigils = 9999; state.tonics = 99;
            state.heartHp = TowerRules.HeartMaxHp(state.heartRank); state.heartStage = "stable";
            state.threat = 5; state.incidents.Clear();
            foreach (var room in state.rooms) room.condition = 100;
            foreach (var resident in state.residents)
            {
                resident.hp = TowerRules.MaxHp(resident); resident.happiness = 100;
                resident.hunger = resident.thirst = resident.rest = 100;
                resident.injury = 0; resident.illness = 0; resident.downed = false;
            }
        }

        private static bool AssignFirst(TowerRules rules, int residentId,
            System.Collections.Generic.List<TowerRoom> rooms)
        {
            foreach (TowerRoom room in rooms)
                if (rules.Assign(residentId, room.uid) == null) return true;
            return false;
        }

        private static bool AssignFromOffset(TowerRules rules, int residentId,
            System.Collections.Generic.List<TowerRoom> rooms, int offset)
        {
            for (int i = 0; i < rooms.Count; i++)
                if (rules.Assign(residentId, rooms[(offset + i) % rooms.Count].uid) == null) return true;
            return false;
        }

        public static string BodyTaskRoom(string variant)
        {
            switch (variant)
            {
                case "short": return "market";
                case "tall": return "well";
                case "muscle": return "warehouse";
                case "hourglass": return "farmstead";
                default: return "kitchen";
            }
        }

        public static void EnsureSlots()
        {
            for (int slot = 1; slot <= 10; slot++)
            {
                var existing = TowerSaveFiles.Load(slot);
                if (existing == null) { TowerSaveFiles.Save(Create(slot)); continue; }
                // Slot 1 is the player's own Tower. Slots 2-10 are developer checkpoints: rebuild
                // ones made by an older generator, keeping the old file beside the new one.
                if (slot == 1 || existing.pinned || existing.generator >= Version) continue;
                string path = TowerSaveFiles.PathFor(slot);
                System.IO.File.Copy(path, path + ".gen" + existing.generator + ".bak", true);
                TowerSaveFiles.Save(Create(slot));
            }
        }
    }
}
