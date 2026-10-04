using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Dungeon Mode layout (GDD 20.2). The Heart sits at the summit or in the depths, centred on the shaft; the residents
    // live on the floors beside it, and every floor between the Heart and the Gates is dungeon. Floor 0 keeps the Gates
    // and becomes the entrance. Every helper here returns the Tower's own answer when the save is a Tower.
    public sealed partial class TowerRules
    {
        public bool IsLair { get { return State.mode == TowerModes.Lair; } }
        public int HeartFloor { get { return IsLair ? State.lair.heartFloor : 0; } }
        // +1 when the Heart crowns the summit, -1 in the depths, 0 while a dungeon is still dormant.
        public int LairDir { get { return HeartFloor > 0 ? 1 : HeartFloor < 0 ? -1 : 0; } }
        public bool LairFounded { get { return IsLair && State.lair.heartFloor != 0; } }

        // A dormant dungeon: the Tower's dormant Heart, flagged for Dungeon Mode.
        public static TowerRules NewLair(int slot = 1)
        {
            var rules = New(slot);
            rules.State.mode = TowerModes.Lair;
            rules.State.label = "Dormant Dungeon";
            rules.State.lair = new TowerLairState { random = 51203 + slot * 173 };
            rules.State.log.Clear();
            rules.Note("The Celestium Heart lies dormant. Touch it to raise a dungeon around it.");
            return rules;
        }

        // gate, dungeon, living, heart or outside (Tower saves: gate for floor 0, outside elsewhere).
        public string FloorKind(int floor)
        {
            if (floor == 0) return "gate";
            int dir = LairDir;
            if (dir == 0) return "outside";
            if (floor == HeartFloor) return "heart";
            int depth = floor * dir, heart = HeartFloor * dir, living = State.lair.livingFloors;
            if (depth <= 0 || depth > heart) return "outside";
            return depth >= heart - living ? "living" : "dungeon";
        }

        public int DungeonFloorCount { get { return LairFounded ? Mathf.Abs(HeartFloor) - State.lair.livingFloors - 1 : 0; } }

        // Where the first resident moves in: the Tower's ground floor, or the living floor next to the Heart.
        public int StarterFloor { get { return LairFounded ? HeartFloor - LairDir : 0; } }

        public string FloorLabel(int floor)
        {
            if (!LairFounded) return floor == 0 ? "GROUND" : (floor > 0 ? "+" : "") + floor.ToString("00");
            switch (FloorKind(floor))
            {
                case "gate": return "ENTRANCE";
                case "heart": return "HEART";
                case "living": return "LIVING";
                case "dungeon": return "B" + Mathf.Abs(floor);
                default: return (floor > 0 ? "+" : "") + floor.ToString("00");
            }
        }

        // Where a room may stand. The Tower keeps its ground-floor and underground rules word for word; a dungeon puts
        // traps and lairs on the entrance and dungeon floors and everything else beside the Heart.
        public string ZoneReason(TowerRoomDef def, int floor)
        {
            if (!IsLair)
            {
                if (def.groundOnly && floor != 0) return "This room needs the ground floor.";
                if (def.undergroundOnly && floor >= 0) return "This room belongs underground.";
                return null;
            }
            string kind = FloorKind(floor);
            if (def.kind == "lair")
                return kind == "gate" || kind == "dungeon" ? null : "Traps and lairs belong on the entrance and dungeon floors.";
            return kind == "living" || kind == "heart" ? null : "Living rooms belong on the floors beside the Heart.";
        }

        public static bool IsLairRoom(TowerRoom room) { return room != null && LairCatalog.Is(room.type); }

        // Dungeon rooms join the build menu only in a dungeon.
        public List<TowerRoomDef> BuildableDefs()
        {
            var list = new List<TowerRoomDef>();
            if (IsLair) list.AddRange(LairCatalog.All);
            list.AddRange(TowerCatalog.All);
            return list;
        }

        // Every load: older saves have no mode or dungeon blob.
        private void NormalizeLair()
        {
            if (string.IsNullOrEmpty(State.mode)) State.mode = TowerModes.Tower;
            if (State.lair == null) State.lair = new TowerLairState();
            var lair = State.lair;
            if (lair.monsters == null) lair.monsters = new List<LairMonster>();
            if (lair.parties == null) lair.parties = new List<LairParty>();
            if (lair.reports == null) lair.reports = new List<LairReport>();
            if (lair.named == null) lair.named = new List<LairNamed>();
            if (lair.vaults == null) lair.vaults = new List<LairVault>();
            if (lair.livingFloors < 1) lair.livingFloors = 1;
            foreach (var party in lair.parties)
            {
                if (party.members == null) party.members = new List<LairAdventurer>();
                if (party.visited == null) party.visited = new List<int>();
                if (party.lines == null) party.lines = new List<string>();
            }
            foreach (var report in lair.reports) if (report.lines == null) report.lines = new List<string>();
            if (lair.random == 0) lair.random = 51203 + State.slot * 173;
        }

        // ---- Founding ---------------------------------------------------------------------------------------

        public string LairFound(bool summit)
        {
            if (!IsLair) return "This is a Tower, not a dungeon.";
            if (State.introPhase != "lair_site") return "Awaken the Heart first.";
            int dir = summit ? 1 : -1;
            const int dungeon = 2, living = 1;
            int heart = (dungeon + living + 1) * dir;
            var heartRoom = State.rooms.Find(r => r.type == "heart");
            if (heartRoom == null) heartRoom = AddRoom("heart", heart, CoreX);
            heartRoom.floor = heart;
            heartRoom.x = CoreX;
            State.lair.heartFloor = heart;
            State.lair.livingFloors = living;
            State.floors.Clear();
            for (int depth = 0; depth <= Mathf.Abs(heart); depth++)
                State.floors.Add(new TowerFloor { number = depth * dir, west = depth == 0 || depth == Mathf.Abs(heart) ? 2 : 3,
                    east = depth == 0 || depth == Mathf.Abs(heart) ? 2 : 3, landing = "stairs" });
            foreach (var def in LairCatalog.All) if (!State.blueprints.Contains(def.id)) State.blueprints.Add(def.id);
            foreach (string id in new[] { "house", "kitchen", "well", "lumber_mill" })
                if (!State.blueprints.Contains(id)) State.blueprints.Add(id);
            AddRoom("spike_hall", 0, 21);
            AddRoom("monster_lair", dir, 21);
            AddRoom("snare_pit", dir, 23);
            AddRoom("bait_vault", 2 * dir, 21);
            AddRoom("spike_hall", 2 * dir, 23);
            AddRoom("house", 3 * dir, 21);
            AddRoom("well", 3 * dir, 20);
            AddRoom("kitchen", 3 * dir, 23);
            AddRoom("lumber_mill", heart, 23);
            State.layoutVersion = LayoutVersion;
            PlaceGates();
            TouchLayout();
            State.introPhase = "choose";
            Note("The Heart " + (summit ? "rose to the summit" : "sank into the depths") +
                ". Traps line the way to it; the living floor waits beside it.");
            return null;
        }

        // ChooseStarter's dungeon ending: no Tower lessons, two free monsters, the first party in four minutes.
        private void LairFoundedNow()
        {
            State.tutorialStep = 7;
            if (State.label == "Dormant Dungeon") State.label = "Founding Day";
            State.lair.nextPartySeconds = LairBalance.FirstParty;
            var lairRoom = State.rooms.Find(r => r.type == "monster_lair");
            for (int i = 0; i < 2; i++)
            {
                var monster = NewMonster(1);
                if (lairRoom != null) monster.lairRoom = lairRoom.uid;
            }
            foreach (var vault in State.rooms.FindAll(r => r.type == "bait_vault")) VaultOf(vault).gold = LairBalance.VaultPerLevel;
            Note("The dungeon is open. Two young monsters took the Monster Lair. Adventurers will come.");
        }

        // ---- Growing the dungeon -----------------------------------------------------------------------------

        // Moves every floor at or beyond `from` (in direction dir) one floor further out; rooms keep their uids.
        public void ShiftFloors(int from, int dir)
        {
            System.Func<int, bool> moves = f => dir > 0 ? f >= from : f <= from;
            foreach (var floor in State.floors) if (moves(floor.number)) floor.number += dir;
            foreach (var room in State.rooms) if (moves(room.floor)) room.floor += dir;
            foreach (var work in State.works) if (moves(work.floor)) work.floor += dir;
            for (int i = 0; i < State.excavatedCells.Count; i++)
            {
                string[] parts = State.excavatedCells[i].Split(':');
                int f;
                if (parts.Length == 2 && int.TryParse(parts[0], out f) && moves(f))
                    State.excavatedCells[i] = (f + dir) + ":" + parts[1];
            }
            foreach (var district in State.districts)
            {
                if (moves(district.floorMin)) district.floorMin += dir;
                if (moves(district.floorMax)) district.floorMax += dir;
            }
            foreach (var party in State.lair.parties)
            {
                if (moves(party.floor)) party.floor += dir;
                if (moves(party.fromFloor)) party.fromFloor += dir;
            }
            if (moves(State.lair.heartFloor)) State.lair.heartFloor += dir;
            foreach (var resident in State.residents) resident.travelSeconds = resident.travelDuration = 0;
            TouchLayout();
        }

        public int LairDigCost() { return LairBalance.DigBase + LairBalance.DigPerFloor * DungeonFloorCount; }
        public int LairDigCap() { return LairBalance.DigCaps[Mathf.Clamp(State.heartRank, 1, 9) - 1]; }
        public int LairLivingCost() { return LairBalance.LivingBase + LairBalance.LivingPerFloor * State.lair.livingFloors; }
        public int LairLivingCap() { return LairBalance.LivingCaps[Mathf.Clamp(State.heartRank, 1, 9) - 1]; }

        // A new dungeon floor opens where the living floors began; they and the Heart move one floor further out.
        public string LairDigFloor()
        {
            if (!LairFounded) return "Found the dungeon first.";
            if (State.lair.parties.Count > 0) return "Wait until no party is inside the dungeon.";
            if (DungeonFloorCount >= LairDigCap())
                return "The Heart at rank " + TowerTiers.Tier(State.heartRank) + " holds " + LairDigCap() + " dungeon floors. Raise it to dig deeper.";
            if (Mathf.Abs(HeartFloor) + 1 > FloorMax) return "The dungeon cannot grow any further.";
            int cost = LairDigCost();
            if (State.celestium < cost) return "Digging a dungeon floor needs " + cost + " Celestium.";
            State.celestium -= cost;
            int dir = LairDir, at = HeartFloor - dir * State.lair.livingFloors;
            ShiftFloors(at, dir);
            State.floors.Add(new TowerFloor { number = at, west = 2, east = 2, landing = "stairs" });
            State.floors.Sort((a, b) => a.number.CompareTo(b.number));
            TouchLayout();
            Note("Dug dungeon floor " + FloorLabel(at) + ". The living floors and the Heart moved one floor " +
                (dir > 0 ? "up." : "down."));
            Emit("lair_dig", 0, 0, at.ToString());
            return null;
        }

        // A new living floor opens beside the Heart; the Heart moves one floor further out.
        public string LairAddLivingFloor()
        {
            if (!LairFounded) return "Found the dungeon first.";
            if (State.lair.parties.Count > 0) return "Wait until no party is inside the dungeon.";
            if (State.lair.livingFloors >= LairLivingCap())
                return "The Heart at rank " + TowerTiers.Tier(State.heartRank) + " keeps " + LairLivingCap() + " living floors. Raise it for more.";
            if (Mathf.Abs(HeartFloor) + 1 > FloorMax) return "The dungeon cannot grow any further.";
            int cost = LairLivingCost();
            if (State.celestium < cost) return "A new living floor needs " + cost + " Celestium.";
            State.celestium -= cost;
            int at = HeartFloor;
            ShiftFloors(at, LairDir);
            State.floors.Add(new TowerFloor { number = at, west = 2, east = 2, landing = "stairs" });
            State.floors.Sort((a, b) => a.number.CompareTo(b.number));
            State.lair.livingFloors++;
            TouchLayout();
            Note("Opened a new living floor beside the Heart.");
            return null;
        }
    }
}
