using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Room builder (GDD 22, owner 2026-10-05): rooms are furnished item by item on a slot grid, and the furniture makes
    // the room's rank. Slice R1 is rules only, behind TowerRules.RoomBuilder (off in Play until the R3 furnish UI).

    [Serializable] public sealed class TowerFurniture
    {
        public string item;   // TowerFurnitureDef.id
        public int tier = 1;  // 1 (F) .. 9 (SSR)
        public int col;       // leftmost column inside the room: 0 .. 4 x width - 1, left to right
        public int row;       // TowerFurnishing.Wall, Back or Front
    }

    public sealed class TowerFurnitureDef
    {
        public readonly string id, displayName, role, resource, material;
        public readonly int row, cols, baseGold;
        public readonly string[] homeTypes;   // empty: fits every room (counts as matching everywhere)
        public TowerFurnitureDef(string id, string name, string role, int row, int cols, int baseGold, string material,
            string resource, params string[] homeTypes)
        {
            this.id = id; displayName = name; this.role = role; this.row = row; this.cols = cols;
            this.baseGold = baseGold; this.material = material; this.resource = resource ?? ""; this.homeTypes = homeTypes;
        }
        public bool Universal { get { return homeTypes.Length == 0; } }
        public bool Matches(string roomType) { return Universal || Array.IndexOf(homeTypes, roomType) >= 0; }
    }

    public static class TowerFurnishing
    {
        public const int Wall = 0, Back = 1, Front = 2;
        public static readonly string[] RowNames = { "wall", "back", "front" };
        public const int ColsPerBay = 4;
        public const int MaxRoomBays = 3;            // v1 cap (GDD 22.2), tunable
        public const float CoverageNeed = 3f;        // weighted columns of rank-r-or-better furniture per bay for rank r
        public const float OffTypeFactor = 0.8f;     // an item that does not match the room's type works at 80%
        public const float DecorWeight = 0.5f;       // decor and lights count half toward the rank
        public const float SellShare = 0.5f;
        public const int StoragePerUnit = 30;        // stock cap per storage unit per rank

        public const string Bed = "bed", Station = "station", Storage = "storage", Amenity = "amenity", Decor = "decor";

        private static readonly string[] Living = { "house", "cottage", "nursery", "terrace_row", "manor" };
        private static readonly string[] StoreRooms = { "barn", "silo", "warehouse" };

        // First-pass catalog: about two to four pieces per building type plus shared decor (GDD 22.5 grows it to ~100).
        public static readonly TowerFurnitureDef[] All = {
            new TowerFurnitureDef("bed", "Bed", Bed, Back, 1, 12, "wood", "", Living),
            new TowerFurnitureDef("cradle", "Cradle", Bed, Back, 1, 14, "wood", "", "nursery"),
            new TowerFurnitureDef("wash_tub", "Wash Tub", Amenity, Back, 1, 16, "stone", "", Living),
            new TowerFurnitureDef("stove", "Iron Stove", Station, Back, 1, 18, "stone", "food", "kitchen"),
            new TowerFurnitureDef("planter", "Sun Planter", Station, Back, 1, 16, "wood", "food", "farmstead"),
            new TowerFurnitureDef("pump", "Well Pump", Station, Back, 1, 16, "stone", "water", "well"),
            new TowerFurnitureDef("saw_bench", "Saw Bench", Station, Back, 2, 20, "wood", "firewood", "lumber_mill"),
            new TowerFurnitureDef("rock_drill", "Rock Drill", Station, Back, 2, 26, "stone", "celestium", "quarry"),
            new TowerFurnitureDef("brew_still", "Tonic Still", Station, Back, 1, 22, "stone", "tonics", "frosted_mug"),
            new TowerFurnitureDef("trade_counter", "Trade Counter", Station, Back, 2, 22, "wood", "gold", "market"),
            new TowerFurnitureDef("anvil", "Anvil", Station, Back, 1, 22, "stone", "train", "forge"),
            new TowerFurnitureDef("war_table", "War Table", Station, Back, 2, 24, "wood", "train", "deck_hall"),
            new TowerFurnitureDef("quest_board", "Quest Board", Station, Back, 1, 20, "wood", "", "guild_hall"),
            new TowerFurnitureDef("loading_cart", "Loading Cart", Station, Back, 1, 14, "wood", "", StoreRooms),
            new TowerFurnitureDef("grain_bin", "Grain Bin", Storage, Back, 1, 14, "wood", "", "silo", "barn"),
            new TowerFurnitureDef("stock_rack", "Stock Rack", Storage, Back, 1, 14, "wood", "", "warehouse", "barn"),
            new TowerFurnitureDef("crate", "Crate", Storage, Front, 1, 8, "wood", ""),
            new TowerFurnitureDef("table", "Table", Amenity, Front, 1, 10, "wood", ""),
            new TowerFurnitureDef("rug", "Rug", Decor, Front, 1, 6, "wood", ""),
            new TowerFurnitureDef("plant", "Potted Plant", Decor, Front, 1, 6, "stone", ""),
            new TowerFurnitureDef("lamp", "Lamp", Decor, Wall, 1, 6, "stone", ""),
            new TowerFurnitureDef("window", "Window", Decor, Wall, 1, 8, "wood", ""),
            new TowerFurnitureDef("shelf", "Wall Shelf", Decor, Wall, 1, 6, "wood", ""),
            new TowerFurnitureDef("painting", "Painting", Decor, Wall, 1, 10, "wood", ""),
        };

        private static readonly int[] TierCost = { 1, 2, 4, 8, 14, 22, 32, 45, 60 };

        public static TowerFurnitureDef Get(string id)
        {
            foreach (var def in All) if (def.id == id) return def;
            return null;
        }

        // Beds and stations hold more people at higher tiers: 1 each at F-E, 2 at D-B, 3 at A-SSR (per column).
        public static int PlacesPerCol(int tier) { return tier <= 2 ? 1 : tier <= 5 ? 2 : 3; }

        public static int Places(TowerFurnitureDef def, int tier)
        {
            if (def == null) return 0;
            if (def.role == Storage) return 2 * def.cols;   // storage units, the same at every tier
            return PlacesPerCol(tier) * def.cols;
        }

        public static int GoldCost(TowerFurnitureDef def, int tier)
        { return def == null ? 0 : def.baseGold * TierCost[Mathf.Clamp(tier, 1, 9) - 1]; }

        public static int MaterialCost(TowerFurnitureDef def, int tier) { return def == null ? 0 : tier * def.cols; }

        public static float MatchFactor(TowerFurnitureDef def, string roomType)
        { return def != null && def.Matches(roomType) ? 1f : OffTypeFactor; }

        public static float RankWeight(TowerFurnitureDef def, string roomType)
        {
            if (def == null) return 0;
            return def.cols * (def.role == Decor ? DecorWeight : 1f) * MatchFactor(def, roomType);
        }

        // The rank the furniture alone earns, before the Heart's cap: the best r such that the room holds
        // CoverageNeed weighted columns per bay of furniture at tier r or better (GDD 22.4).
        public static int CoverageRank(TowerRoom room)
        {
            if (room == null || room.furniture == null) return 1;
            float need = CoverageNeed * Mathf.Max(1, room.width);
            for (int r = TowerTiers.MaxRank; r > 1; r--)
            {
                float have = 0;
                foreach (var piece in room.furniture)
                    if (piece.tier >= r) have += RankWeight(Get(piece.item), room.type);
                if (have >= need - 0.001f) return r;
            }
            return 1;
        }

        // Weighted columns at tier r or better against the need: the furnish card's rank meter.
        public static float Coverage(TowerRoom room, int rank)
        {
            if (room == null || room.furniture == null) return 0;
            float have = 0;
            foreach (var piece in room.furniture)
                if (piece.tier >= rank) have += RankWeight(Get(piece.item), room.type);
            return have / (CoverageNeed * Mathf.Max(1, room.width));
        }

        public static bool Overlaps(TowerRoom room, int row, int col, int cols, TowerFurniture ignore)
        {
            foreach (var piece in room.furniture)
            {
                if (piece == ignore || piece.row != row) continue;
                var def = Get(piece.item);
                int width = def == null ? 1 : def.cols;
                if (col < piece.col + width && piece.col < col + cols) return true;
            }
            return false;
        }

        // The station a room type is built around (presets), or null for types without one.
        public static string MainStation(string type)
        {
            foreach (var def in All) if (def.role == Station && def.homeTypes.Length > 0 && def.homeTypes[0] == type) return def.id;
            switch (type)
            {
                case "barn": case "silo": case "warehouse": return "loading_cart";
                case "guild_hall": return "quest_board";
            }
            return null;
        }

        public static string MainStorage(string type)
        {
            switch (type)
            {
                case "silo": return "grain_bin";
                case "barn": case "warehouse": return "stock_rack";
            }
            return null;
        }
    }

    public sealed partial class TowerRules
    {
        // Off in Play until the R3 furnish UI lands; EditMode suites opt in (RoomBuilderTests).
        public static bool ForceRoomBuilder;
        public static bool RoomBuilder { get { return ForceRoomBuilder; } }

        private static HashSet<string> furnishableTypes;

        // Tower rooms are furnished; the Heart, the Gates and the dungeon's rooms keep their own rules (GDD 22: dungeon
        // rooms come in slice R7).
        public static bool FurnishableType(string type)
        {
            if (furnishableTypes == null)
            {
                furnishableTypes = new HashSet<string>();
                foreach (var def in TowerCatalog.All) if (def.id != "gate" && def.id != "heart") furnishableTypes.Add(def.id);
            }
            return type != null && furnishableTypes.Contains(type);
        }

        public bool Furnished(TowerRoom room) { return RoomBuilder && room != null && FurnishableType(room.type); }

        // ---- What the furniture gives ---------------------------------------------------------------------

        private int PlacesOf(TowerRoom room, string role)
        {
            int total = 0;
            if (room == null || room.furniture == null) return 0;
            foreach (var piece in room.furniture)
            {
                var def = TowerFurnishing.Get(piece.item);
                if (def != null && def.role == role) total += TowerFurnishing.Places(def, piece.tier);
            }
            return total;
        }

        public int HomePlaces(TowerRoom room) { return PlacesOf(room, TowerFurnishing.Bed); }
        public int JobPlaces(TowerRoom room) { return PlacesOf(room, TowerFurnishing.Station); }
        public int StorageUnits(TowerRoom room) { return PlacesOf(room, TowerFurnishing.Storage); }
        public int AmenityPlaces(TowerRoom room) { return PlacesOf(room, TowerFurnishing.Amenity); }

        // Sleeping places, with SET-3 Better beds (+10%) as for the old living rooms.
        public int BedsIn(TowerRoom room)
        {
            int beds = HomePlaces(room);
            return Researched("SET-3") ? Mathf.CeilToInt(beds * 1.1f) : beds;
        }

        // Storage units weighted by match (off-type racks hold 80%).
        public float StorageWeight(TowerRoom room)
        {
            float total = 0;
            if (room == null || room.furniture == null) return 0;
            foreach (var piece in room.furniture)
            {
                var def = TowerFurnishing.Get(piece.item);
                if (def != null && def.role == TowerFurnishing.Storage)
                    total += TowerFurnishing.Places(def, piece.tier) * TowerFurnishing.MatchFactor(def, room.type);
            }
            return total;
        }

        // Whether Assign makes this room someone's home: a living room with no workstations (a Shack with a stove is a
        // workplace whose beds are filled by AssignHome or by newcomers).
        public bool HousesByDefault(TowerRoom room)
        {
            var def = room == null ? null : TowerCatalog.Get(room.type);
            if (def == null) return false;
            return Furnished(room) ? def.kind == "living" && JobPlaces(room) == 0 : def.kind == "living";
        }

        // Each resource the room's workstations make, weighted by places x match (off-type stations at 80%).
        public Dictionary<string, float> ProductWeights(TowerRoom room)
        {
            var weights = new Dictionary<string, float>();
            if (room == null || room.furniture == null) return weights;
            foreach (var piece in room.furniture)
            {
                var def = TowerFurnishing.Get(piece.item);
                if (def == null || def.role != TowerFurnishing.Station || def.resource == "" || def.resource == "train") continue;
                float w = TowerFurnishing.Places(def, piece.tier) * TowerFurnishing.MatchFactor(def, room.type);
                float old;
                weights[def.resource] = (weights.TryGetValue(def.resource, out old) ? old : 0) + w;
            }
            return weights;
        }

        private int ProducingPlaces(TowerRoom room)
        {
            int total = 0;
            foreach (var piece in room.furniture)
            {
                var def = TowerFurnishing.Get(piece.item);
                if (def != null && def.role == TowerFurnishing.Station && def.resource != "" && def.resource != "train")
                    total += TowerFurnishing.Places(def, piece.tier);
            }
            return total;
        }

        // The room's main product: its type's in the old rules; in a furnished room, whatever most of its stations make
        // (the type's own product wins a tie), or "" when no station produces.
        public string Product(TowerRoom room)
        {
            var def = room == null ? null : TowerCatalog.Get(room.type);
            if (def == null) return "";
            if (!Furnished(room)) return def.produces ?? "";
            string best = ""; float bestWeight = 0;
            foreach (var pair in ProductWeights(room))
                if (pair.Value > bestWeight + 0.001f || Mathf.Abs(pair.Value - bestWeight) <= 0.001f && pair.Key == def.produces)
                { best = pair.Key; bestWeight = pair.Value; }
            return best;
        }

        public bool Makes(TowerRoom room, string resource)
        {
            if (string.IsNullOrEmpty(resource) || room == null) return false;
            if (!Furnished(room)) { var def = TowerCatalog.Get(room.type); return def != null && def.produces == resource; }
            return ProductWeights(room).ContainsKey(resource);
        }

        // What one collect of this room yields of one resource. A furnished room splits its collect between its products
        // by station weight, so off-type stations make 80% of what matching ones do.
        public float YieldOf(TowerRoom room, string resource)
        {
            if (!Makes(room, resource)) return 0;
            float full = BaseCollect(room, resource);
            if (!Furnished(room)) return full;
            int places = ProducingPlaces(room);
            float weight;
            if (places <= 0 || !ProductWeights(room).TryGetValue(resource, out weight)) return 0;
            return full * weight / places;
        }

        // Seats a venue offers its patrons (TowerTown): workers' places plus tables and other amenities.
        public int VenueSeats(TowerRoom room) { return Furnished(room) ? Capacity(room) + AmenityPlaces(room) : Capacity(room); }

        // ---- Rank from furniture ----------------------------------------------------------------------------

        private int FurnitureRankCap(TowerRoom room)
        {
            int cap = Mathf.Max(RankCap(), room.level);                  // never caps an older, higher room down
            if (!Researched("CON-7")) cap = Mathf.Min(cap, Mathf.Max(TowerTiers.MaxRank - 1, room.level));
            return cap;
        }

        // Rank follows the furniture both ways (GDD 22.4); the walls swap with it (view, slice R2).
        public void RefreshRoomRank(TowerRoom room)
        {
            if (!Furnished(room)) return;
            int rank = Mathf.Clamp(TowerFurnishing.CoverageRank(room), 1, FurnitureRankCap(room));
            if (rank == room.level) return;
            bool up = rank > room.level;
            room.level = rank;
            TouchLayout();
            if (up)
            {
                Note(TowerCatalog.Get(room.type).displayName + " reached rank " + TowerTiers.Tier(rank) + ". New walls!");
                Bump("upgrade");
                Emit("upgrade", room.uid, 0, rank.ToString());
            }
            else Emit("rankdown", room.uid, 0, rank.ToString());
        }

        public void RefreshAllRoomRanks()
        {
            if (!RoomBuilder) return;
            foreach (var room in State.rooms) RefreshRoomRank(room);
        }

        // The highest tier the player may buy right now.
        public int FurnitureTierCap() { return Researched("CON-7") ? RankCap() : Mathf.Min(RankCap(), TowerTiers.MaxRank - 1); }

        // ---- Placing, moving and selling -------------------------------------------------------------------

        public string CanPlaceFurniture(TowerRoom room, string itemId, int tier, int col, int row, TowerFurniture ignore = null)
        {
            if (!Furnished(room)) return "This room cannot be furnished.";
            var def = TowerFurnishing.Get(itemId);
            if (def == null) return "Unknown furniture.";
            if (row != def.row) return def.displayName + " goes on the " + TowerFurnishing.RowNames[def.row] + " row.";
            if (col < 0 || col + def.cols > room.width * TowerFurnishing.ColsPerBay) return "That does not fit in the room.";
            if (TowerFurnishing.Overlaps(room, row, col, def.cols, ignore)) return "Something is already there.";
            if (tier < 1 || tier > TowerTiers.MaxRank) return "Unknown tier.";
            return null;
        }

        public string PlaceFurniture(int roomUid, string itemId, int tier, int col, int row)
        {
            var room = Room(roomUid);
            string blocked = CanPlaceFurniture(room, itemId, tier, col, row);
            if (blocked != null) return blocked;
            if (tier > FurnitureTierCap())
                return tier >= TowerTiers.MaxRank && RankCap() >= TowerTiers.MaxRank ? "Research CON-7 Celestium fittings for SSR furniture." :
                    "Raise the Celestium Heart to buy rank " + TowerTiers.Tier(tier) + " furniture.";
            var def = TowerFurnishing.Get(itemId);
            int gold = TowerFurnishing.GoldCost(def, tier), material = TowerFurnishing.MaterialCost(def, tier);
            if (State.gold < gold) return "Not enough gold.";
            if (def.material == "stone" ? State.stone < material : State.wood < material)
                return def.displayName + " needs " + material + " " + def.material + ".";
            State.gold -= gold;
            if (def.material == "stone") State.stone -= material; else State.wood -= material;
            room.furniture.Add(new TowerFurniture { item = itemId, tier = tier, col = col, row = row });
            room.furnished = true;
            RefreshRoomRank(room);
            TouchLayout();
            Emit("furnish", room.uid, 0, itemId);
            return null;
        }

        public string MoveFurniture(int roomUid, int index, int col)
        {
            var room = Room(roomUid);
            if (!Furnished(room) || index < 0 || index >= room.furniture.Count) return "Choose a piece to move.";
            var piece = room.furniture[index];
            string blocked = CanPlaceFurniture(room, piece.item, piece.tier, col, piece.row, piece);
            if (blocked != null) return blocked;
            piece.col = col;
            TouchLayout();
            return null;
        }

        public int FurnitureRefund(TowerFurniture piece)
        { return piece == null ? 0 : Mathf.FloorToInt(TowerFurnishing.GoldCost(TowerFurnishing.Get(piece.item), piece.tier) * TowerFurnishing.SellShare); }

        public string SellFurniture(int roomUid, int index)
        {
            var room = Room(roomUid);
            if (!Furnished(room) || index < 0 || index >= room.furniture.Count) return "Choose a piece to sell.";
            var piece = room.furniture[index];
            var def = TowerFurnishing.Get(piece.item);
            State.gold += FurnitureRefund(piece);
            int material = Mathf.FloorToInt(TowerFurnishing.MaterialCost(def, piece.tier) * TowerFurnishing.SellShare);
            if (def != null && def.material == "stone") State.stone += material; else State.wood += material;
            room.furniture.RemoveAt(index);
            EvictOverflow(room);
            RefreshRoomRank(room);
            TouchLayout();
            return null;
        }

        // Fewer beds or stations than people: the last ones assigned move out (homes are found again by the colony tick).
        private void EvictOverflow(TowerRoom room)
        {
            int homes = BedsIn(room), jobs = JobPlaces(room);
            int housed = 0, working = 0;
            foreach (var resident in State.residents)
            {
                if (resident.homeRoom == room.uid && ++housed > homes) resident.homeRoom = 0;
                if (resident.jobRoom == room.uid && ++working > jobs) { resident.jobRoom = 0; resident.duty = ""; }
            }
        }

        public string AssignHome(int residentId, int roomUid)
        {
            var resident = Resident(residentId);
            var room = Room(roomUid);
            if (resident == null || room == null) return "Resident or room missing.";
            int beds = Furnished(room) ? BedsIn(room) : (TowerCatalog.Get(room.type).kind == "living" ? Capacity(room) : 0);
            if (beds <= 0) return "This room has no beds.";
            int used = 0;
            foreach (var other in State.residents) if (other.id != residentId && other.homeRoom == roomUid) used++;
            if (used >= beds) return "Every bed here is taken.";
            resident.homeRoom = roomUid;
            Note(resident.name + " now sleeps in the " + TowerCatalog.Get(room.type).displayName + ".");
            return null;
        }

        // ---- Width: EXPAND and SHRINK (GDD 22.2) -------------------------------------------------------------

        public int ExpandGoldCost(TowerRoom room) { return room == null ? 0 : 80 * room.width; }
        public int ExpandWoodCost(TowerRoom room) { return room == null ? 0 : 10 * room.width; }

        public string ExpandRoom(int roomUid)
        {
            var room = Room(roomUid);
            if (!Furnished(room)) return "This room cannot be expanded.";
            if (room.width >= TowerFurnishing.MaxRoomBays) return "Rooms are at most " + TowerFurnishing.MaxRoomBays + " bays wide.";
            int newX;
            if (BayGrowth(room, room.width + 1, out newX) != null)
                return "Expanding needs the cell " + (room.flip ? "to its right" : "to its left") + " free and founded.";
            int gold = ExpandGoldCost(room), wood = ExpandWoodCost(room);
            if (State.gold < gold) return "Not enough gold.";
            if (State.wood < wood) return "Expanding needs " + wood + " wood.";
            State.gold -= gold; State.wood -= wood;
            // A room growing to the left gains its new bay at column 0: everything inside slides one bay right.
            if (!room.flip) foreach (var piece in room.furniture) piece.col += TowerFurnishing.ColsPerBay;
            room.x = newX; room.width++;
            roomIndex = null;
            RefreshRoomRank(room);
            TouchLayout();
            Note(TowerCatalog.Get(room.type).displayName + " expanded to " + room.width + " bays.");
            return null;
        }

        public string ShrinkRoom(int roomUid)
        {
            var room = Room(roomUid);
            if (!Furnished(room)) return "This room cannot be shrunk.";
            if (room.width <= 1) return "A room is at least one bay wide.";
            int from = room.flip ? (room.width - 1) * TowerFurnishing.ColsPerBay : 0;
            foreach (var piece in room.furniture)
            {
                var def = TowerFurnishing.Get(piece.item);
                int width = def == null ? 1 : def.cols;
                if (piece.col < from + TowerFurnishing.ColsPerBay && from < piece.col + width)
                    return "Clear the " + (room.flip ? "right" : "left") + " bay first.";
            }
            if (!room.flip) { foreach (var piece in room.furniture) piece.col -= TowerFurnishing.ColsPerBay; room.x++; }
            room.width--;
            State.gold += ExpandGoldCost(room) / 2;
            roomIndex = null;
            RefreshRoomRank(room);
            TouchLayout();
            return null;
        }

        // ---- Presets: the painted rooms as one-tap layouts, and the migration ----------------------------------

        // Furnishes an empty room so it keeps today's rank, beds, worker places and storage (GDD 22.6 R1). Used for every
        // room on the first load with the builder on, for newly built rooms (a starter kit) and for checkpoint layouts.
        public void FurnishPreset(TowerRoom room, int rank)
        {
            if (!Furnished(room)) return;
            room.furniture.Clear();
            room.furnished = true;
            var def = TowerCatalog.Get(room.type);
            rank = Mathf.Clamp(rank, 1, TowerTiers.MaxRank);
            int legacyPlaces = room.width * 2 + rank - 1;                 // GDD 5.3's first-pass count
            int needHomes = def.kind == "living" ? legacyPlaces : 0;
            int needJobs = def.kind == "living" ? 0 : legacyPlaces;
            int needUnits = def.kind == "storage" ? 2 * OutputBays(room) : 0;
            string bed = room.type == "nursery" ? "cradle" : "bed";
            string station = TowerFurnishing.MainStation(room.type), storage = TowerFurnishing.MainStorage(room.type);
            int cols = room.width * TowerFurnishing.ColsPerBay;

            int homes = 0, jobs = 0, units = 0;
            for (int c = 0; c < cols; c++)
            {
                string pick = null;
                if (homes < needHomes) pick = bed;
                else if (jobs < needJobs && station != null) pick = station;
                else if (units < needUnits && storage != null) pick = storage;
                if (pick == null) break;
                var item = TowerFurnishing.Get(pick);
                if (c + item.cols > cols || TowerFurnishing.Overlaps(room, TowerFurnishing.Back, c, item.cols, null)) continue;
                room.furniture.Add(new TowerFurniture { item = pick, tier = rank, col = c, row = TowerFurnishing.Back });
                int got = TowerFurnishing.Places(item, rank);
                if (item.role == TowerFurnishing.Bed) homes += got;
                else if (item.role == TowerFurnishing.Station) jobs += got;
                else units += got;
                c += item.cols - 1;
            }
            // Storage that did not fit at the back goes in front-row crates.
            int front = 0;
            for (; front < cols && units < needUnits; front++)
            {
                room.furniture.Add(new TowerFurniture { item = "crate", tier = rank, col = front, row = TowerFurnishing.Front });
                units += TowerFurnishing.Places(TowerFurnishing.Get("crate"), rank);
            }
            // Lights and windows on the wall, a rug or plant in front, then tables until the furniture earns the rank.
            string[] wall = { "window", "lamp", "shelf", "painting" };
            for (int c = 0; c < cols; c++)
                room.furniture.Add(new TowerFurniture { item = wall[c % wall.Length], tier = rank, col = c, row = TowerFurnishing.Wall });
            string[] decor = { "plant", "rug" };
            for (int c = front, i = 0; c < cols && TowerFurnishing.CoverageRank(room) < rank; c++, i++)
                room.furniture.Add(new TowerFurniture { item = i % 3 == 2 ? "table" : decor[i % 2], tier = rank, col = c, row = TowerFurnishing.Front });
            room.level = Mathf.Max(room.level, rank);
        }

        // Every load with the builder on: rooms built before it get their preset at their current rank, once.
        private void MigrateFurnishing()
        {
            foreach (var room in State.rooms)
            {
                if (room.furniture == null) room.furniture = new List<TowerFurniture>();
                if (Furnished(room) && !room.furnished) FurnishPreset(room, room.level);
            }
        }
    }
}
