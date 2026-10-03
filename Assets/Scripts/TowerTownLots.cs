using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // One town lot on the TOWN view's grid (TowerTownMap tile coordinates).
    [Serializable] public sealed class TowerLot
    {
        public int x, z;
        public string type = "";   // a TownBuildingDefs id; "" = vacant
        public int rank = 1;       // 1 (F) .. 9 (SSR); the look follows its band (F-D, C-B, A-SSR)
        public bool manual;        // set by the player: the town never changes it on its own
    }

    public sealed class TowerTownBuildingDef
    {
        public readonly string id, name, district;
        public TowerTownBuildingDef(string id, string name, string district) { this.id = id; this.name = name; this.district = district; }
    }

    // Town lots (TOWER_MODE_GDD 19.2, TT 10.3.3). Owner ruling 2026-10-03: the town runs itself AND the player can take
    // full control. Auto lots are chosen and upgraded by the town from its population, rooms and Heart rank; any lot the
    // player builds, swaps, clears or locks becomes manual and the town leaves it alone until the player hands it back.
    // Group commands act on any set of tiles (a dragged box in the HUD). Lots are data for now: their effects on the
    // simulation (town homes, venues, workshops) arrive with the economy slice.
    public sealed partial class TowerRules
    {
        public static readonly string[] TownDistricts = { "residential", "market", "industry", "arcane", "defence" };

        public static readonly TowerTownBuildingDef[] TownBuildingDefs = {
            new TowerTownBuildingDef("cottage", "Cottage", "residential"),
            new TowerTownBuildingDef("rowhouse", "Row House", "residential"),
            new TowerTownBuildingDef("manor", "Townhouse Manor", "residential"),
            new TowerTownBuildingDef("stall", "Market Stall", "market"),
            new TowerTownBuildingDef("inn", "Inn", "market"),
            new TowerTownBuildingDef("bazaar", "Bazaar Hall", "market"),
            new TowerTownBuildingDef("workshop", "Workshop", "industry"),
            new TowerTownBuildingDef("mill", "Mill", "industry"),
            new TowerTownBuildingDef("foundry", "Foundry", "industry"),
            new TowerTownBuildingDef("shrine", "Celestium Shrine", "arcane"),
            new TowerTownBuildingDef("library", "Library", "arcane"),
            new TowerTownBuildingDef("bathhouse", "Bathhouse", "arcane"),
            new TowerTownBuildingDef("gatehouse", "Gatehouse", "defence"),
            new TowerTownBuildingDef("wallsegment", "Wall Segment", "defence"),
            new TowerTownBuildingDef("watchtower", "Watchtower", "defence"),
        };

        // Share of auto lots per district (residential, market, industry, arcane, defence).
        private static readonly float[] TownMix = { 0.45f, 0.2f, 0.2f, 0.1f, 0.05f };

        public const float TownGrowSeconds = 20f, TownUpgradeSeconds = 60f;
        public const int TownLotGold = 40;   // per rank, for a lot the player builds or swaps (draft)

        public static TowerTownBuildingDef TownBuilding(string id)
        {
            foreach (var def in TownBuildingDefs) if (def.id == id) return def;
            return null;
        }

        public static int RankBand(int rank) { return rank <= 3 ? 0 : rank <= 5 ? 1 : 2; }   // F-D, C-B, A-SSR

        // Lots by tile, rebuilt when the list is replaced or a lot is added or removed (types change in place).
        private readonly Dictionary<long, TowerLot> lotIndex = new Dictionary<long, TowerLot>();
        private List<TowerLot> lotIndexList;
        private int lotIndexCount = -1;

        private static long LotKey(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        public TowerLot Lot(int x, int z)
        {
            if (lotIndexList != State.townLots || lotIndexCount != State.townLots.Count)
            {
                lotIndex.Clear();
                foreach (var lot in State.townLots) lotIndex[LotKey(lot.x, lot.z)] = lot;
                lotIndexList = State.townLots; lotIndexCount = State.townLots.Count;
            }
            TowerLot found;
            return lotIndex.TryGetValue(LotKey(x, z), out found) ? found : null;
        }

        // How many built lots the town wants: it grows with the people and rooms, capped by the open ring.
        public int TownTargetLots()
        {
            int want = 6 + State.residents.Count * 2 + State.rooms.Count / 6;
            return Mathf.Min(want, TowerTownMap.LotCount(State.heartRank));
        }

        public int TownBuiltLots()
        {
            int count = 0;
            foreach (var lot in State.townLots) if (lot.type.Length > 0) count++;
            return count;
        }

        private float townGrowCarry, townUpgradeCarry;

        // Runs every tick (live and offline): the town fills its best empty lot every TownGrowSeconds and raises one
        // auto lot a rank every TownUpgradeSeconds. A fresh or older save fills to its target at once.
        private void TickTown(float dt)
        {
            if (State.introPhase != "complete") return;
            if (State.townLots.Count == 0)
            {
                for (int i = 0, target = TownTargetLots(); i < target; i++) if (!GrowTown()) break;
                return;
            }
            townGrowCarry += dt;
            while (townGrowCarry >= TownGrowSeconds)
            {
                townGrowCarry -= TownGrowSeconds;
                if (TownBuiltLots() >= TownTargetLots() || !GrowTown()) { townGrowCarry = 0; break; }
            }
            townUpgradeCarry += dt;
            while (townUpgradeCarry >= TownUpgradeSeconds)
            {
                townUpgradeCarry -= TownUpgradeSeconds;
                UpgradeTown();
            }
        }

        // Builds one auto lot: road frontage nearest the Tower first, the district furthest below its share.
        private bool GrowTown()
        {
            int rank = State.heartRank, r = TowerTownMap.Radius(rank);
            int bestX = 0, bestZ = 0;
            float best = float.MaxValue;
            for (int x = -r; x <= r; x++)
                for (int z = -r; z <= r; z++)
                {
                    if (!TowerTownMap.Buildable(x, z, rank) || Lot(x, z) != null) continue;
                    float score = x * x + z * z - (TowerTownMap.Frontage(x, z, rank) ? 400 : 0);
                    if (score < best) { best = score; bestX = x; bestZ = z; }
                }
            if (best == float.MaxValue) return false;
            State.townLots.Add(new TowerLot { x = bestX, z = bestZ, type = AutoType(bestX, bestZ), rank = 1 });
            return true;
        }

        private string AutoType(int x, int z)
        {
            var counts = new int[TownDistricts.Length];
            int total = 0;
            foreach (var lot in State.townLots)
            {
                var def = TownBuilding(lot.type);
                if (def == null) continue;
                counts[Array.IndexOf(TownDistricts, def.district)]++;
                total++;
            }
            int pick = 0;
            float worst = float.MaxValue;
            for (int d = 0; d < TownDistricts.Length; d++)
            {
                float share = total == 0 ? 0 : counts[d] / (float)total;
                float gap = share - TownMix[d];
                if (gap < worst) { worst = gap; pick = d; }
            }
            var choices = new List<TowerTownBuildingDef>();
            foreach (var def in TownBuildingDefs) if (def.district == TownDistricts[pick]) choices.Add(def);
            // Defence grows watchtowers and walls on its own; gatehouses are the player's call.
            if (TownDistricts[pick] == "defence") choices.RemoveAll(d => d.id == "gatehouse");
            uint h = unchecked((uint)(x * 73856093) ^ (uint)(z * 19349663));
            return choices[(int)(h % (uint)choices.Count)].id;
        }

        // Raises the lowest auto lot nearest the Tower by one rank, up to the Heart's rank.
        private void UpgradeTown()
        {
            TowerLot pick = null;
            foreach (var lot in State.townLots)
            {
                if (lot.manual || lot.type.Length == 0 || lot.rank >= State.heartRank) continue;
                if (pick == null || lot.rank < pick.rank ||
                    lot.rank == pick.rank && lot.x * lot.x + lot.z * lot.z < pick.x * pick.x + pick.z * pick.z) pick = lot;
            }
            if (pick != null) pick.rank++;
        }

        // ---------------------------------------------------------------- the player's controls

        public int LotCost(string type, int rank) { return type.Length == 0 ? 0 : TownLotGold * Mathf.Max(1, rank); }

        // Build or swap one lot by hand (type "" clears it and keeps it vacant). The lot becomes manual.
        public string SetLot(int x, int z, string type, int rank = 0)
        {
            if (!TowerTownMap.Buildable(x, z, State.heartRank)) return "That tile is not a town lot.";
            if (type.Length > 0 && TownBuilding(type) == null) return "Unknown town building.";
            var lot = Lot(x, z);
            if (rank <= 0) rank = lot != null && lot.type.Length > 0 ? lot.rank : 1;
            rank = Mathf.Clamp(rank, 1, State.heartRank);
            bool same = lot != null && lot.type == type && lot.rank == rank;
            int cost = same ? 0 : LotCost(type, rank);
            if (State.gold < cost) return "Needs " + cost + " gold.";
            State.gold -= cost;
            if (lot == null) { lot = new TowerLot { x = x, z = z }; State.townLots.Add(lot); }
            lot.type = type; lot.rank = rank; lot.manual = true;
            return null;
        }

        // Re-types every lot in a group (a dragged box). Tiles that are not lots are skipped. All or nothing on gold.
        public string SetLots(IList<Vector2Int> tiles, string type, int rank = 0)
        {
            int cost = 0, count = 0;
            foreach (var t in tiles)
            {
                if (!TowerTownMap.Buildable(t.x, t.y, State.heartRank)) continue;
                var lot = Lot(t.x, t.y);
                int r = Mathf.Clamp(rank > 0 ? rank : lot != null && lot.type.Length > 0 ? lot.rank : 1, 1, State.heartRank);
                if (lot == null || lot.type != type || lot.rank != r) cost += LotCost(type, r);
                count++;
            }
            if (count == 0) return "No town lots in the selection.";
            if (State.gold < cost) return "Needs " + cost + " gold for " + count + " lots.";
            foreach (var t in tiles) if (TowerTownMap.Buildable(t.x, t.y, State.heartRank)) SetLot(t.x, t.y, type, rank);
            return null;
        }

        // Hands lots back to the town (auto = true) or locks them as they are (auto = false). Free.
        public int SetLotsAuto(IList<Vector2Int> tiles, bool auto)
        {
            int changed = 0;
            foreach (var t in tiles)
            {
                var lot = Lot(t.x, t.y);
                if (lot == null || lot.manual == !auto) continue;
                lot.manual = !auto;
                if (auto && lot.type.Length == 0) State.townLots.Remove(lot);   // a vacant lot handed back is open again
                changed++;
            }
            return changed;
        }

        public string LotLabel(TowerLot lot)
        {
            if (lot == null) return "empty lot";
            var def = TownBuilding(lot.type);
            string what = def == null ? "vacant (kept empty)" : def.name + " " + TowerTiers.Tier(lot.rank);
            return what + (lot.manual ? "  •  yours" : "  •  town");
        }

        private void NormalizeTown()
        {
            if (State.townLots == null) State.townLots = new List<TowerLot>();
            State.townLots.RemoveAll(l => l == null);
            foreach (var lot in State.townLots)
            {
                if (lot.type == null) lot.type = "";
                if (lot.type.Length > 0 && TownBuilding(lot.type) == null) lot.type = "cottage";
                lot.rank = Mathf.Clamp(lot.rank, 1, TowerTiers.MaxRank);
            }
        }
    }
}
