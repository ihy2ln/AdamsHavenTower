using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // A district is a band of consecutive floors the player zones, Cities: Skylines style (TT 10.30.0).
    [Serializable] public sealed class TowerDistrict
    {
        public int id;
        public string name;
        public int floorMin, floorMax;
        public string spec = "";                          // "", residential, industry, market or arcane
        public List<string> policies = new List<string>();
    }

    public sealed class TowerPolicyDef
    {
        public readonly string id, name, effect;
        public readonly int upkeep;          // gold per game day while enacted
        public readonly bool needsGround;    // only a district holding the ground floor (and its Gates) can enact it
        public TowerPolicyDef(string id, string name, string effect, int upkeep, bool needsGround = false)
        { this.id = id; this.name = name; this.effect = effect; this.upkeep = upkeep; this.needsGround = needsGround; }
    }

    public sealed class TowerSpecDef
    {
        public readonly string id, name, effect;
        public readonly string[] kinds;      // room kinds that count toward purity and take the bonus
        public TowerSpecDef(string id, string name, string effect, params string[] kinds)
        { this.id = id; this.name = name; this.effect = effect; this.kinds = kinds; }
    }

    // Districts zone floors; each can take a specialisation and enact policies. Service coverage and appeal are
    // computed for every floor whether zoned or not. Everything here is a bonus or an opt-in trade-off: zoning
    // never makes an unzoned tower worse.
    public sealed partial class TowerRules
    {
        public static readonly TowerSpecDef[] Specs = {
            new TowerSpecDef("residential", "Residential", "Homes here lift the mood of everyone who lives in them.", "living"),
            new TowerSpecDef("industry", "Industry", "Production and storage rooms here work up to 15% faster.", "produce", "storage"),
            new TowerSpecDef("market", "Market", "Markets and the Guild here earn up to 20% more.", "trade", "herald"),
            new TowerSpecDef("arcane", "Arcane", "Training, Tonics and care here run up to 20% faster.", "medic", "train")
        };

        public static readonly TowerPolicyDef[] Policies = {
            new TowerPolicyDef("rationing", "Rationing", "Meals 20% smaller. Residents here -4 mood.", 0),
            new TowerPolicyDef("overtime", "Overtime", "+15% production. Workers here -3 mood.", 30),
            new TowerPolicyDef("curfew", "Curfew", "No brawls on these floors. Residents here -2 mood.", 15),
            new TowerPolicyDef("festival_days", "Festival Days", "Residents here +6 mood.", 80),
            new TowerPolicyDef("quiet_hours", "Quiet Hours", "Rest at home 30% faster. Production -5%.", 10),
            new TowerPolicyDef("hearth_watch", "Hearth Watch", "Fire and pest damage here -25%.", 25),
            new TowerPolicyDef("open_gate", "Open Gate", "Wanderers arrive 25% sooner. Threat +4.", 20, true),
            new TowerPolicyDef("beautification", "Beautification", "+25 appeal on these floors.", 40)
        };

        // Districts the Heart allows at each rank (F..SSR): the first comes with rank E.
        private static readonly int[] DistrictCaps = { 0, 1, 1, 2, 2, 3, 4, 5, 6 };
        public static int MaxDistricts(int heartRank) { return DistrictCaps[Mathf.Clamp(heartRank, 1, TowerTiers.MaxRank) - 1]; }

        private static readonly string[] DistrictNames = {
            "Hearthward", "Lanternrow", "Mossdeep", "Stonebrook", "Silverstep", "Ashgrove Rise", "Briarhold", "Moonwell"
        };

        public const int MedicalService = 1, SafetyService = 2, FoodService = 4, LeisureService = 8;
        public const float CoverageRefresh = 10f;

        public static TowerSpecDef SpecDef(string id)
        {
            foreach (var def in Specs) if (def.id == id) return def;
            return null;
        }

        public static TowerPolicyDef PolicyDef(string id)
        {
            foreach (var def in Policies) if (def.id == id) return def;
            return null;
        }

        public TowerDistrict District(int id) { return State.districts.Find(d => d.id == id); }

        // ---- Per-floor caches (indexed floor - FloorMin), rebuilt when the layout or the zoning changes -------------

        private const int FloorSlots = FloorMax - FloorMin + 1;
        private readonly int[] floorDistrict = new int[FloorSlots];     // index into State.districts, -1 unzoned
        private readonly int[] floorPolicies = new int[FloorSlots];     // bit per Policies entry, while enacted
        private readonly int[] floorCoverage = new int[FloorSlots];     // service bits
        private readonly float[] floorAppeal = new float[FloorSlots];   // 0..100
        private readonly List<float> districtPurity = new List<float>();
        private int zonedStamp = -1, zonedRooms = -1, zonedFloors = -1, zonedResidents = -1;
        private List<TowerDistrict> zonedList;
        private float serviceTimer = CoverageRefresh;
        private float homeAppeal;
        private int overlayStamp;
        public int OverlayStamp { get { return overlayStamp; } }

        private static int Slot(int floor) { return Mathf.Clamp(floor - FloorMin, 0, FloorSlots - 1); }

        private void EnsureZoning()
        {
            if (zonedStamp == layoutStamp && zonedRooms == State.rooms.Count && zonedFloors == State.floors.Count &&
                zonedList == State.districts && zonedResidents == State.residents.Count) return;
            zonedStamp = layoutStamp; zonedRooms = State.rooms.Count; zonedFloors = State.floors.Count;
            zonedList = State.districts; zonedResidents = State.residents.Count;
            for (int i = 0; i < FloorSlots; i++) { floorDistrict[i] = -1; floorPolicies[i] = 0; }
            districtPurity.Clear();
            for (int d = 0; d < State.districts.Count; d++)
            {
                var district = State.districts[d];
                int mask = 0;
                if (!State.policiesLapsed)
                    for (int p = 0; p < Policies.Length; p++)
                        if (district.policies.Contains(Policies[p].id)) mask |= 1 << p;
                for (int f = district.floorMin; f <= district.floorMax; f++)
                    if (f >= FloorMin && f <= FloorMax) { floorDistrict[Slot(f)] = d; floorPolicies[Slot(f)] = mask; }
                districtPurity.Add(Purity(district));
            }
            RefreshServices();
        }

        // Share of the band's room cells that match its specialisation.
        private float Purity(TowerDistrict district)
        {
            var spec = SpecDef(district.spec);
            if (spec == null) return 0;
            int cells = 0, matching = 0;
            foreach (var room in State.rooms)
            {
                if (room.floor < district.floorMin || room.floor > district.floorMax ||
                    room.type == "heart" || room.type == "gate") continue;
                cells += room.width;
                if (Array.IndexOf(spec.kinds, TowerCatalog.Get(room.type).kind) >= 0) matching += room.width;
            }
            return cells == 0 ? 0 : matching / (float)cells;
        }

        public float DistrictPurity(TowerDistrict district)
        {
            EnsureZoning();
            int index = State.districts.IndexOf(district);
            return index < 0 || index >= districtPurity.Count ? 0 : districtPurity[index];
        }

        // Coverage and appeal: who is staffed changes without touching the layout, so these also refresh on a timer.
        private void RefreshServices()
        {
            for (int i = 0; i < FloorSlots; i++) floorCoverage[i] = 0;
            foreach (var room in State.rooms)
            {
                int service = ServiceOf(room.type);
                if (service == 0 || !Staffed(room)) continue;
                int radius = room.type == "gate" ? 2 + GuardCount() / 2 : 1 + room.level / 3;
                for (int f = room.floor - radius; f <= room.floor + radius; f++)
                    if (f >= FloorMin && f <= FloorMax) floorCoverage[Slot(f)] |= service;
            }
            var points = new float[FloorSlots];
            var rooms = new int[FloorSlots];
            foreach (var room in State.rooms)
            {
                if (room.type == "heart" || room.type == "gate") continue;
                int s = Slot(room.floor);
                rooms[s]++;
                if (room.condition >= 80) points[s] += 1;
                points[s] += Mathf.Min(1f, (room.level - 1) / 4f);
                if (Decorative(room.type)) points[s] += 1;
            }
            for (int s = 0; s < FloorSlots; s++)
            {
                float appeal = rooms[s] == 0 ? 0 : points[s] / (3f * rooms[s]) * 100f;
                if ((floorPolicies[s] & PolicyBit("beautification")) != 0) appeal += 25;
                int d = floorDistrict[s];
                if (d >= 0 && State.districts[d].spec == "residential") appeal += 10 * districtPurity[d];
                floorAppeal[s] = Mathf.Clamp(appeal, 0, 100);
            }
            // Average appeal of the floors people live on: what a wanderer sees from the Gate.
            var homeFloors = new bool[FloorSlots];
            foreach (var room in State.rooms)
                if (TowerCatalog.Get(room.type).kind == "living") homeFloors[Slot(room.floor)] = true;
            float total = 0;
            int count = 0;
            for (int s = 0; s < FloorSlots; s++) if (homeFloors[s]) { total += floorAppeal[s]; count++; }
            homeAppeal = count == 0 ? 0 : total / count;
            serviceTimer = 0;
            overlayStamp++;
        }

        private static int ServiceOf(string type)
        {
            switch (type)
            {
                case "frosted_mug": return MedicalService;
                case "gate": return SafetyService;
                case "kitchen": case "farmstead": return FoodService;
                case "market": case "guild_hall": case "deck_hall": return LeisureService;
                default: return 0;
            }
        }

        private static bool Decorative(string type)
        { return type == "manor" || type == "terrace_row" || type == "frosted_mug" || type == "market"; }

        private bool Staffed(TowerRoom room)
        {
            foreach (var resident in State.residents)
                if (resident.jobRoom == room.uid && !resident.exploring && !resident.away) return true;
            return false;
        }

        private static int PolicyBit(string id)
        {
            for (int p = 0; p < Policies.Length; p++) if (Policies[p].id == id) return 1 << p;
            return 0;
        }

        // ---- Lookups the simulation uses (constant time once the caches are warm) --------------------------------

        public TowerDistrict DistrictAt(int floor)
        {
            EnsureZoning();
            int d = floorDistrict[Slot(floor)];
            return d < 0 ? null : State.districts[d];
        }

        public bool PolicyOn(int floor, string policy)
        {
            EnsureZoning();
            return (floorPolicies[Slot(floor)] & PolicyBit(policy)) != 0;
        }

        public bool Covered(int floor, int service)
        {
            EnsureZoning();
            return (floorCoverage[Slot(floor)] & service) != 0;
        }

        public int CoverageMask(int floor) { EnsureZoning(); return floorCoverage[Slot(floor)]; }

        public float Appeal(int floor) { EnsureZoning(); return floorAppeal[Slot(floor)]; }

        private float SpecBonus(TowerRoom room, string spec, float strength)
        {
            int d = floorDistrict[Slot(room.floor)];
            if (d < 0 || State.districts[d].spec != spec) return 1f;
            var def = SpecDef(spec);
            if (Array.IndexOf(def.kinds, TowerCatalog.Get(room.type).kind) < 0) return 1f;
            return 1f + strength * districtPurity[d];
        }

        // Production and training speed in this room from its district: specialisation and Overtime / Quiet Hours.
        public float DistrictBonus(TowerRoom room)
        {
            if (room == null || State.districts.Count == 0) return 1f;
            EnsureZoning();
            float bonus = SpecBonus(room, "industry", 0.15f) * SpecBonus(room, "market", 0.20f) *
                SpecBonus(room, "arcane", 0.20f);
            int mask = floorPolicies[Slot(room.floor)];
            if ((mask & PolicyBit("overtime")) != 0) bonus *= 1.15f;
            if ((mask & PolicyBit("quiet_hours")) != 0) bonus *= 0.95f;
            return bonus;
        }

        private int HomeFloor(TowerResident resident, out bool housed)
        {
            var home = Room(resident.homeRoom);
            housed = home != null;
            return home == null ? 0 : home.floor;
        }

        // Food eaten per meal: Rationing at home, and a Kitchen or Farmstead nearby (Food coverage).
        private float MealFactor(TowerResident resident)
        {
            bool housed;
            int floor = HomeFloor(resident, out housed);
            if (!housed) return 1f;
            float factor = Covered(floor, FoodService) ? 0.9f : 1f;
            if (PolicyOn(floor, "rationing")) factor *= 0.8f;
            return factor;
        }

        private float RestFactor(TowerResident resident)
        {
            if (State.districts.Count == 0 || resident.currentRoom != resident.homeRoom) return 1f;
            bool housed;
            int floor = HomeFloor(resident, out housed);
            return housed && PolicyOn(floor, "quiet_hours") ? 1.3f : 1f;
        }

        // Care on a floor the Frosted Mug reaches, sped up again in an Arcane district.
        private float CareFactor(TowerRoom where)
        {
            if (where == null) return 1f;
            EnsureZoning();
            float factor = Covered(where.floor, MedicalService) ? 1.25f : 1f;
            int d = floorDistrict[Slot(where.floor)];
            if (d >= 0 && State.districts[d].spec == "arcane") factor *= 1f + 0.10f * districtPurity[d];
            return factor;
        }

        private float SafetyFactor(TowerRoom where) { return where != null && Covered(where.floor, SafetyService) ? 1.2f : 1f; }

        private float HazardFactor(TowerRoom where, string kind)
        {
            return where != null && (kind == "fire" || kind == "pests") && State.districts.Count > 0 &&
                PolicyOn(where.floor, "hearth_watch") ? 0.75f : 1f;
        }

        public bool CurfewAt(TowerRoom where) { return where != null && State.districts.Count > 0 && PolicyOn(where.floor, "curfew"); }

        // Wanderers come sooner to an appealing tower, and sooner still with the Gate thrown open.
        public float GateArrivalFactor()
        {
            EnsureZoning();
            float factor = 1f + 0.4f * homeAppeal / 100f;
            if (OpenGate()) factor *= 1.25f;
            return factor;
        }

        private bool OpenGate()
        {
            foreach (var d in State.districts)
                if (!State.policiesLapsed && d.policies.Contains("open_gate") && d.floorMin <= 0 && d.floorMax >= 0) return true;
            return false;
        }

        public float DistrictThreat() { return OpenGate() ? 4f : 0f; }

        private void AddDistrictThoughts(TowerResident resident, List<TowerThought> list)
        {
            bool housed;
            int floor = HomeFloor(resident, out housed);
            if (!housed) return;
            EnsureZoning();
            int s = Slot(floor);
            float appeal = floorAppeal[s];
            if (appeal >= 75) list.Add(new TowerThought("Lovely district", 5));
            else if (appeal >= 50) list.Add(new TowerThought("Pleasant floor", 2));
            if ((floorCoverage[s] & LeisureService) != 0) list.Add(new TowerThought("Leisure nearby", 3));
            var job = Room(resident.jobRoom);   // Overtime is felt where they work, wherever they live
            if (job != null && (floorPolicies[Slot(job.floor)] & PolicyBit("overtime")) != 0)
                list.Add(new TowerThought("Overtime", -3));
            int d = floorDistrict[s];
            if (d < 0) return;
            if (State.districts[d].spec == "residential")
                list.Add(new TowerThought("Residential quarter", 2 + 4 * districtPurity[d]));
            int mask = floorPolicies[s];
            if ((mask & PolicyBit("festival_days")) != 0) list.Add(new TowerThought("Festival days", 6));
            if ((mask & PolicyBit("rationing")) != 0) list.Add(new TowerThought("Rationing", -4));
            if ((mask & PolicyBit("curfew")) != 0) list.Add(new TowerThought("Curfew", -2));
        }

        // ---- Upkeep ------------------------------------------------------------------------------------------------

        public int PolicyUpkeepPerDay()
        {
            int total = 0;
            foreach (var d in State.districts)
                foreach (string id in d.policies)
                {
                    var def = PolicyDef(id);
                    if (def != null) total += def.upkeep;
                }
            return total;
        }

        // Upkeep is charged as it accrues. When the treasury cannot pay, every policy lapses until it can.
        private void TickDistricts(float dt)
        {
            serviceTimer += dt;
            if (serviceTimer >= CoverageRefresh) { EnsureZoning(); RefreshServices(); }
            int upkeep = PolicyUpkeepPerDay();
            if (upkeep <= 0) { SetLapsed(false); return; }
            State.policyCarry += upkeep * dt / DaySeconds;
            int due = Mathf.FloorToInt(State.policyCarry);
            if (due <= 0) return;
            if (State.gold >= due)
            {
                State.gold -= due;
                State.policyCarry -= due;
                SetLapsed(false);
            }
            else
            {
                State.policyCarry = Mathf.Min(State.policyCarry, 1f);
                SetLapsed(true);
            }
        }

        private void SetLapsed(bool lapsed)
        {
            if (State.policiesLapsed == lapsed) return;
            State.policiesLapsed = lapsed;
            zonedStamp = -1;
            Note(lapsed ? "The treasury is empty: every district policy has lapsed until gold comes in." :
                "District policies are back in force.");
        }

        // ---- Zoning commands -------------------------------------------------------------------------------------

        private string ZoneError(int floorMin, int floorMax, int ignoreId)
        {
            if (floorMin > floorMax) return "A district needs at least one floor.";
            for (int f = floorMin; f <= floorMax; f++)
            {
                if (Floor(f) == null) return "Floor " + f + " is not open.";
                var other = State.districts.Find(d => d.id != ignoreId && f >= d.floorMin && f <= d.floorMax);
                if (other != null) return "Floor " + f + " already belongs to " + other.name + ".";
            }
            return null;
        }

        public string CreateDistrict(int floor)
        {
            if (State.introPhase != "complete") return "Found the Tower first.";
            int cap = MaxDistricts(State.heartRank);
            if (State.districts.Count >= cap)
                return cap == 0 ? "Raise the Celestium Heart to rank E to zone a district." :
                    "The Heart allows " + cap + " district" + (cap == 1 ? "" : "s") + " at this rank.";
            string error = ZoneError(floor, floor, 0);
            if (error != null) return error;
            int id = State.nextDistrictId++;
            var district = new TowerDistrict { id = id, name = DistrictNames[(id - 1) % DistrictNames.Length],
                floorMin = floor, floorMax = floor };
            State.districts.Add(district);
            TouchLayout();
            Note(district.name + " was zoned on floor " + floor + ".");
            Emit("district", 0, 0, district.name);
            return null;
        }

        public string ResizeDistrict(int id, int floorMin, int floorMax)
        {
            var district = District(id);
            if (district == null) return "No such district.";
            string error = ZoneError(floorMin, floorMax, id);
            if (error != null) return error;
            if (district.policies.Contains("open_gate") && (floorMin > 0 || floorMax < 0))
                return "Repeal Open Gate first: it needs the ground floor.";
            district.floorMin = floorMin;
            district.floorMax = floorMax;
            TouchLayout();
            return null;
        }

        public string DissolveDistrict(int id)
        {
            var district = District(id);
            if (district == null) return "No such district.";
            State.districts.Remove(district);
            TouchLayout();
            Note(district.name + " was unzoned.");
            return null;
        }

        public string SetDistrictSpec(int id, string spec)
        {
            var district = District(id);
            if (district == null) return "No such district.";
            if (spec != "" && SpecDef(spec) == null) return "Unknown specialisation.";
            district.spec = spec;
            TouchLayout();
            Note(district.name + (spec == "" ? " has no specialisation." : " is now a " + SpecDef(spec).name + " district."));
            return null;
        }

        public string CycleDistrictSpec(int id)
        {
            var district = District(id);
            if (district == null) return "No such district.";
            int index = Array.FindIndex(Specs, s => s.id == district.spec);   // -1 for none
            return SetDistrictSpec(id, index + 1 >= Specs.Length ? "" : Specs[index + 1].id);
        }

        public string TogglePolicy(int id, string policy)
        {
            var district = District(id);
            var def = PolicyDef(policy);
            if (district == null || def == null) return "No such district or policy.";
            if (district.policies.Contains(policy))
            {
                district.policies.Remove(policy);
                Note(district.name + " repealed " + def.name + ".");
            }
            else
            {
                if (def.needsGround && (district.floorMin > 0 || district.floorMax < 0))
                    return def.name + " needs a district that holds the ground floor.";
                district.policies.Add(policy);
                Note(district.name + " enacted " + def.name + (def.upkeep > 0 ? " (" + def.upkeep + " gold a day)." : "."));
            }
            TouchLayout();
            return null;
        }
    }
}
