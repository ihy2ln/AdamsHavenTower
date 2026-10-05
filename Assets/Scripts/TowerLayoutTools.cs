using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // TT 10.30.0 groundwork: Heart-rank floor limits (GDD 8.4, stretched to the ±24 world), moving and demolishing
    // rooms, the colony layer's save version and its own random stream (so new systems never shift the old seeds).
    public sealed partial class TowerRules
    {
        public const int ColonyVersion = 2;   // 2 = town life: residents gain joy (TowerTown.cs)

        // How far up and down each Heart rank (F..SSR) lets the tower open floors. Floors already open stay open.
        private static readonly int[] FloorsUp = { 2, 3, 5, 7, 9, 12, 15, 19, 24 };
        private static readonly int[] FloorsDown = { 1, 3, 5, 7, 9, 12, 15, 19, 24 };

        public static int MaxFloorUp(int heartRank) { return FloorsUp[Mathf.Clamp(heartRank, 1, TowerTiers.MaxRank) - 1]; }
        public static int MaxFloorDown(int heartRank) { return FloorsDown[Mathf.Clamp(heartRank, 1, TowerTiers.MaxRank) - 1]; }

        // The lowest Heart rank that allows floor `number` (MaxRank + 1 when none does).
        public static int HeartRankForFloor(int number)
        {
            for (int rank = 1; rank <= TowerTiers.MaxRank; rank++)
                if (number <= MaxFloorUp(rank) && -number <= MaxFloorDown(rank)) return rank;
            return TowerTiers.MaxRank + 1;
        }

        // Null when the Heart allows floor `number` to be opened, otherwise what it takes.
        public string FloorCapReason(int number)
        {
            int needed = HeartRankForFloor(number);
            if (needed <= State.heartRank) return null;
            if (needed > TowerTiers.MaxRank) return "Floor out of range.";
            return "Raise the Celestium Heart to rank " + TowerTiers.Tier(needed) + " to open floor " +
                (number > 0 ? "+" : "") + number + ".";
        }

        // ---- Layout stamp: caches keyed on the tower's shape (districts, coverage, appeal) watch this -----

        private int layoutStamp;
        public int LayoutStamp { get { return layoutStamp; } }
        private void TouchLayout() { layoutStamp++; }

        // ---- Colony random stream ---------------------------------------------------------------------------

        private float ColonyRandom01()
        {
            int x = State.colonyRandom;
            if (x == 0) x = 90001 + State.slot * 131;
            x ^= x << 13; x ^= (int)((uint)x >> 17); x ^= x << 5;
            State.colonyRandom = x;
            return ((uint)x & 0x00ffffff) / 16777216f;
        }

        // Every load: JsonUtility leaves fields missing from older saves at their defaults, lists as null.
        private void NormalizeColony()
        {
            if (string.IsNullOrEmpty(State.storyteller)) State.storyteller = "balanced";
            if (State.districts == null) State.districts = new List<TowerDistrict>();
            foreach (var district in State.districts)
            {
                if (district.policies == null) district.policies = new List<string>();
                if (district.spec == null) district.spec = "";
            }
            if (State.nextDistrictId < 1) State.nextDistrictId = 1;
            if (State.autoRun == null) State.autoRun = new TowerAutoRun();
            if (State.autoRun.party == null) State.autoRun.party = new List<int>();
            if (State.autoRun.region == null) State.autoRun.region = "";
            if (State.autoReport == null) State.autoReport = "";
            if (State.autoSigilDate == null) State.autoSigilDate = "";
            if (State.outposts == null) State.outposts = new List<TowerOutpost>();
            foreach (var outpost in State.outposts)
            {
                if (outpost.staff == null) outpost.staff = new List<int>();
                if (outpost.report == null) outpost.report = "";
            }
            if (State.outpostSitesSeen == null) State.outpostSitesSeen = new List<string>();
            NormalizeSiege();
            NormalizeBanners();
            NormalizeTown();
            NormalizeLair();
            foreach (var resident in State.residents)
            {
                if (resident.trait2 == null) resident.trait2 = "";
                if (resident.backstory == null) resident.backstory = "";
                if (resident.inspiration == null) resident.inspiration = "";
                if (resident.posting == null) resident.posting = "";
                if (resident.mealMemory == null) resident.mealMemory = "";
                if (resident.funMemory == null) resident.funMemory = "";
            }
        }

        // Runs on every load: list guards first, then the one-time steps for this colony version.
        private void MigrateColony()
        {
            NormalizeColony();
            EnsureDungeonBand();   // one world (TT 10.5.0): a Tower save from before the merge gets its dungeon
            if (RoomBuilder) MigrateFurnishing();   // room builder (GDD 22): older rooms get their preset, once
            if (State.colonyVersion >= ColonyVersion) return;
            if (State.colonyVersion < 1)
            {
                if (State.colonyRandom == 0) State.colonyRandom = 90001 + State.slot * 131;
                State.siegeCooldown = Mathf.Max(State.siegeCooldown, FirstSiegeAfter);
            }
            if (State.colonyVersion < 2)
            {
                // Saves from before town life load joy as 0: start everyone content instead.
                foreach (var list in new[] { State.residents, State.legacyHeroes, State.heartWaiting })
                    if (list != null) foreach (var resident in list) resident.joy = 70;
            }
            State.colonyVersion = ColonyVersion;
        }

        // ---- Moving rooms (GDD 4: "Move everything") ------------------------------------------------------

        public int MoveCost(TowerRoom room) { return room == null ? 0 : 20 + 10 * room.width * room.level; }

        // Whether this room can be picked up at all (MOVE button, long-press); where it may go is CanMoveRoom.
        public string CanStartMove(int roomUid)
        {
            var room = Room(roomUid);
            if (room == null) return "Choose a room to move.";
            if (room.type == "heart" || room.type == "gate") return "The Heart and the Gates stay where they are.";
            if (State.introPhase != "complete") return "Finish founding the Tower first.";
            if (State.incidents.Exists(i => i.roomUid == roomUid)) return "Resolve the incident first.";
            if (State.gold < MoveCost(room)) return "Moving needs " + MoveCost(room) + " gold.";
            return null;
        }

        public string CanMoveRoom(int roomUid, int floor, int x)
        {
            string start = CanStartMove(roomUid);
            if (start != null) return start;
            var room = Room(roomUid);
            if (room.floor == floor && room.x == x) return "The room is already there.";
            var def = TowerCatalog.Get(room.type);
            if (floor < FloorMin || floor > FloorMax || Floor(floor) == null) return "Open that floor first.";
            int width = room.width;
            bool westSide = x + width <= CoreX, eastSide = x > CoreX;
            if (!westSide && !eastSide) return "Rooms cannot cover the Heart shaft.";
            if (x < MinBuildX || x + width - 1 > MaxBuildX) return "Rooms stay within the ten-cell wings.";
            for (int cx = x; cx < x + width; cx++)
                if (!IsFounded(floor, cx))
                    return "Expand the Celestium foundation " + (westSide ? "west" : "east") + " first.";
            string zone = ZoneReason(def, floor);
            if (zone != null) return zone;
            for (int cx = x; cx < x + width; cx++)
                if (Occupied(floor, cx, room)) return "Another room occupies that space.";
            // Like a new room it must lean on the shaft or on another room, never on its own old cells.
            int inner = westSide ? x + width : x - 1;
            if (inner != CoreX && !Occupied(floor, inner, room))
                return "Place it against the Heart shaft or another room.";
            if (State.gold < MoveCost(room)) return "Moving needs " + MoveCost(room) + " gold.";
            return null;
        }

        private bool Occupied(int floor, int x, TowerRoom ignore)
        {
            var other = RoomAt(floor, x);
            return other != null && other != ignore || WorkRoomAt(floor, x) != null;
        }

        // The room keeps its uid, rank, workers, residents, progress and condition; only its place changes.
        public string MoveRoom(int roomUid, int floor, int x)
        {
            string error = CanMoveRoom(roomUid, floor, x);
            if (error != null) return error;
            var room = Room(roomUid);
            int fromFloor = room.floor;
            State.gold -= MoveCost(room);
            room.floor = floor;
            room.x = x;
            room.flip = x > CoreX;   // east of the shaft new bays grow outward, to the right
            foreach (var resident in State.residents)
                if (resident.targetRoom == roomUid || resident.currentRoom == roomUid)
                    resident.travelSeconds = resident.travelDuration = 0;
            TouchLayout();
            Note("Moved the " + TowerCatalog.Get(room.type).displayName + " from floor " + fromFloor + " to floor " + floor + ".");
            Bump("move");
            Emit("move", room.uid, 0, TowerCatalog.Get(room.type).displayName);
            return null;
        }

        // ---- Demolishing -------------------------------------------------------------------------------------

        // Everyone in or bound for a demolished room walks home (or to the Heart); the homeless get a free bed.
        private void Evacuate(int roomUid)
        {
            var heart = State.rooms.Find(r => r.type == "heart");
            var homeless = new List<TowerResident>();
            foreach (var resident in State.residents)
            {
                if (resident.homeRoom == roomUid) { resident.homeRoom = 0; homeless.Add(resident); }
                if (resident.jobRoom == roomUid) { resident.jobRoom = 0; resident.duty = ""; }
                if (resident.currentRoom == roomUid || resident.targetRoom == roomUid)
                {
                    resident.currentRoom = resident.homeRoom > 0 ? resident.homeRoom : heart == null ? 0 : heart.uid;
                    resident.targetRoom = 0;
                    resident.currentTask = "idle";
                    resident.travelSeconds = resident.travelDuration = 0;
                }
            }
            foreach (var resident in homeless)
            {
                var home = AvailableHome();
                if (home == null) break;
                resident.homeRoom = home.uid;
                if (resident.currentRoom == 0 || heart != null && resident.currentRoom == heart.uid)
                    resident.currentRoom = home.uid;
            }
        }
    }
}
