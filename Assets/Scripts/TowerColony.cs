using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // The colony layer: Fallout Shelter's levels, brownouts and marching raiders, and
    // RimWorld's relationships, varied mood breaks, bleeding out and remembering the dead.
    public sealed partial class TowerRules
    {
        public const float CriticalLimit = 240f;   // live seconds a critical resident survives untended
        public const int LevelCap = 50;
        public const float RaidMarchSeconds = 30f; // undefended raiders push on after this long

        // ---- Levels -----------------------------------------------------------------

        public static float MaxHp(TowerResident resident)
        { return 105f + 2.5f * (Mathf.Max(1, resident.level) - 1); }

        public static int XpToNext(TowerResident resident) { return 30 + resident.level * 15; }

        public void GiveXp(TowerResident resident, int amount)
        {
            if (resident == null || resident.ageStage != 0 || amount <= 0) return;
            resident.xp += amount;
            while (resident.level < LevelCap && resident.xp >= XpToNext(resident))
            {
                resident.xp -= XpToNext(resident);
                resident.level++;
                if (!resident.downed) resident.hp = MaxHp(resident);
                resident.happiness = Mathf.Min(100, resident.happiness + 5);
                Note(resident.name + " reached level " + resident.level + ".");
                Bump("level_up");
                Emit("level_up", resident.currentRoom, resident.id, resident.level.ToString());
            }
            if (resident.level >= LevelCap) resident.xp = 0;
        }

        // ---- Health: recovery, bleeding out, death ------------------------------------

        private readonly List<TowerResident> dying = new List<TowerResident>();

        public static bool IsCritical(TowerResident resident)
        {
            return resident.downed && (resident.injury >= 50 || resident.hunger < 5 || resident.thirst < 5);
        }

        public bool IsTended(TowerResident patient)
        {
            int where = patient.currentRoom > 0 ? patient.currentRoom : patient.homeRoom;
            return State.residents.Exists(o => o.id != patient.id && o.currentTask == "care" &&
                o.targetRoom == where && o.currentRoom == where);
        }

        private void TickRecovery(TowerResident resident, float dt, bool live, bool shortage)
        {
            if (!resident.downed)
            {
                resident.criticalSeconds = 0;
                if (!shortage && resident.injury > 0 && resident.injury < 50)
                    resident.injury = Mathf.Max(0, resident.injury - 0.012f * dt);
                return;
            }
            if (!IsCritical(resident))
            {
                resident.criticalSeconds = Mathf.Max(0, resident.criticalSeconds - dt);
                if (shortage) return;
                // Bed rest: a fed, stable resident gets back up on their own, just slowly.
                resident.hp = Mathf.Min(MaxHp(resident), resident.hp + 0.05f * dt);
                resident.injury = Mathf.Max(0, resident.injury - 0.01f * dt);
                if (resident.hp >= 35)
                {
                    resident.downed = false;
                    Note(resident.name + " is back on their feet.");
                    Emit("healed", resident.currentRoom, resident.id, resident.name);
                }
                return;
            }
            if (!live || resident.origin == "body" || IsTended(resident)) return;
            resident.criticalSeconds += dt;
            // Wounds bleed out in minutes; hunger takes a full day, giving the Steward time to act.
            bool wounded = resident.injury >= 50;
            float limit = (wounded ? CriticalLimit : DaySeconds) *
                (State.policies.Contains("trauma_recovery") ? 1.5f : 1f);
            if (resident.criticalSeconds < limit) return;
            if (resident.origin == "hero")
            {
                // Roster heroes are bound to the Heart: it drags them back instead of letting go.
                resident.criticalSeconds = 0;
                resident.injury = 45; resident.hp = Mathf.Max(resident.hp, 12);
                resident.hunger = Mathf.Max(resident.hunger, 20); resident.thirst = Mathf.Max(resident.thirst, 20);
                resident.happiness = Mathf.Max(0, resident.happiness - 15);
                Note("The Heart pulled " + resident.name + " back from the brink.");
                Emit("brink", resident.currentRoom, resident.id, resident.name);
                return;
            }
            if (!dying.Contains(resident)) dying.Add(resident);
        }

        private void FlushDeaths()
        {
            foreach (var resident in dying)
                Die(resident, resident.hunger < 5 || resident.thirst < 5 ? "hunger and thirst" : "their wounds");
            dying.Clear();
        }

        public void Die(TowerResident resident, string cause)
        {
            if (resident == null || !State.residents.Contains(resident)) return;
            var entry = new TowerMemorial { name = resident.name, cause = cause, day = State.day,
                diedAt = State.clock, partnerId = resident.familyPartnerId };
            foreach (var bond in State.bonds)
                if ((bond.a == resident.id || bond.b == resident.id) && bond.opinion >= 40)
                    entry.friends.Add(bond.a == resident.id ? bond.b : bond.a);
            State.memorial.Add(entry);
            if (State.memorial.Count > 30) State.memorial.RemoveAt(0);
            var partner = Resident(resident.familyPartnerId);
            if (partner != null && partner.familyPartnerId == resident.id) partner.familyPartnerId = 0;
            State.bonds.RemoveAll(b => b.a == resident.id || b.b == resident.id);
            bondIndex = null;
            int where = resident.currentRoom > 0 ? resident.currentRoom : resident.homeRoom;
            State.residents.Remove(resident);
            Note(resident.name + " died of " + cause + ". The Heart remembers them.");
            Bump("death");
            Emit("death", where, 0, resident.name);
        }

        // ---- Hearth power: the Fallout Shelter brownout -------------------------------

        private readonly HashSet<int> darkRooms = new HashSet<int>();

        private static float PowerDistance(TowerRoom room)
        { return Mathf.Abs(room.floor) * 3f + Mathf.Abs(room.x + room.width * 0.5f - (CoreX + 0.5f)); }

        // With no firewood banked, only as much of the Tower as the mills can feed stays lit,
        // starting nearest the Heart. Lumber Mills, the Heart and the Gate never go dark.
        private void TickPower()
        {
            darkRooms.Clear();
            if (State.firewood > 0.01f) return;
            float upkeep = UpkeepPerMinute("firewood");
            float ratio = upkeep <= 0 ? 1 : Mathf.Clamp01(IncomePerMinute("firewood") / upkeep);
            var lit = State.rooms.FindAll(r => r.type != "heart" && r.type != "gate" && r.type != "lumber_mill");
            lit.Sort((a, b) => PowerDistance(a).CompareTo(PowerDistance(b)));
            int total = 0;
            foreach (var room in lit) total += room.width;
            float budget = ratio * total, used = 0;
            foreach (var room in lit)
            {
                used += room.width;
                if (used > budget + 0.001f) darkRooms.Add(room.uid);
            }
        }

        public bool IsPowered(TowerRoom room) { return room != null && !darkRooms.Contains(room.uid); }
        public int DarkRoomCount { get { return darkRooms.Count; } }

        // ---- Raiders ------------------------------------------------------------------

        public static string IncidentName(string kind)
        {
            switch (kind)
            {
                case "raiders": return "Silverwood raiders";
                case "pests": return "Thornvermin";
                case "cave_in": return "cave-in";
                default: return kind;
            }
        }

        // Raiders arrive at the Gate, where guards meet them first.
        public string StartRaid()
        {
            var gate = State.rooms.Find(r => r.type == "gate");
            if (gate == null) return "There is no Gate to attack.";
            if (State.incidents.Exists(i => i.roomUid == gate.uid)) return "The Gate is already under attack.";
            State.incidents.Add(new TowerIncident { roomUid = gate.uid, kind = "raiders",
                hp = 80 + State.threat * 0.6f, severity = 1 + Mathf.Min(2, State.residents.Count / 20f) });
            if (State.tutorialStep == 5) State.tutorialStep = 6;
            Note("Silverwood raiders are at the Gate!");
            Emit("incident", gate.uid, 0, "raiders");
            return null;
        }

        public int GuardCount()
        {
            var gate = State.rooms.Find(r => r.type == "gate");
            return gate == null ? 0 : State.residents.FindAll(r => r.jobRoom == gate.uid && !r.downed &&
                !r.exploring && !r.away).Count;
        }

        private List<TowerRoom> RaidNeighbours(TowerRoom room)
        {
            var list = new List<TowerRoom>();
            foreach (var other in State.rooms)
            {
                if (other.uid == room.uid) continue;
                bool touching = other.floor == room.floor &&
                    (other.x + other.width == room.x || room.x + room.width == other.x);
                bool atCore = room.x + room.width == CoreX || room.x == CoreX + 1 || room.type == "heart";
                bool otherAtCore = other.x + other.width == CoreX || other.x == CoreX + 1 || other.type == "heart";
                bool landing = Mathf.Abs(other.floor - room.floor) == 1 && atCore && otherAtCore;
                if (touching || landing) list.Add(other);
            }
            return list;
        }

        // Undefended raiders push deeper, room by room. Returns true when they moved.
        private bool MarchRaiders(TowerIncident incident, TowerRoom room, float dt)
        {
            incident.marchSeconds += dt;
            if (incident.marchSeconds < RaidMarchSeconds || room.type == "heart") return false;
            var options = RaidNeighbours(room).FindAll(r => r.uid != incident.fromRoom &&
                !State.incidents.Exists(i => i.roomUid == r.uid));
            if (options.Count == 0)
                options = RaidNeighbours(room).FindAll(r => !State.incidents.Exists(i => i.roomUid == r.uid));
            incident.marchSeconds = 0;
            if (options.Count == 0) return false;
            var next = options[Mathf.Min(options.Count - 1, (int)(Random01() * options.Count))];
            incident.fromRoom = room.uid;
            incident.roomUid = next.uid;
            Note("Raiders broke into the " + TowerCatalog.Get(next.type).displayName + ".");
            Emit("raid_advance", next.uid, 0, TowerCatalog.Get(next.type).displayName);
            return true;
        }

        // ---- Relationships ------------------------------------------------------------

        private Dictionary<long, TowerBond> bondIndex;
        private float socialTimer;

        private static long BondKey(int a, int b)
        { return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }

        private TowerBond BondOf(int a, int b, bool create)
        {
            if (bondIndex == null || bondIndex.Count != State.bonds.Count)
            {
                bondIndex = new Dictionary<long, TowerBond>();
                foreach (var bond in State.bonds) bondIndex[BondKey(bond.a, bond.b)] = bond;
            }
            TowerBond found;
            if (bondIndex.TryGetValue(BondKey(a, b), out found) || !create) return found;
            found = new TowerBond { a = Mathf.Min(a, b), b = Mathf.Max(a, b) };
            State.bonds.Add(found);
            bondIndex[BondKey(a, b)] = found;
            return found;
        }

        public float Opinion(int a, int b)
        {
            var bond = BondOf(a, b, false);
            return bond == null ? 0 : bond.opinion;
        }

        public static string OpinionLabel(float opinion)
        {
            return opinion >= 75 ? "Close friend" : opinion >= 40 ? "Friend" : opinion <= -40 ? "Rival" :
                opinion <= -15 ? "Dislikes" : "Acquaintance";
        }

        // The strongest ties first; the HUD shows a few of these.
        public List<KeyValuePair<TowerResident, float>> Relations(TowerResident resident)
        {
            var list = new List<KeyValuePair<TowerResident, float>>();
            foreach (var bond in State.bonds)
            {
                if (bond.a != resident.id && bond.b != resident.id) continue;
                var other = Resident(bond.a == resident.id ? bond.b : bond.a);
                if (other != null && Mathf.Abs(bond.opinion) >= 10)
                    list.Add(new KeyValuePair<TowerResident, float>(other, bond.opinion));
            }
            list.Sort((x, y) => Mathf.Abs(y.Value).CompareTo(Mathf.Abs(x.Value)));
            return list;
        }

        private static bool Social(TowerResident r)
        {
            return r.origin != "body" && r.ageStage == 0 && !r.downed && !r.away && !r.exploring &&
                (r.targetRoom == 0 || r.currentRoom == r.targetRoom) && r.currentRoom > 0;
        }

        private void TickSocial(float dt, bool live)
        {
            socialTimer += dt;
            if (socialTimer < 5f) return;
            float step = socialTimer;
            socialTimer = 0;
            var byRoom = new Dictionary<int, List<TowerResident>>();
            foreach (var resident in State.residents)
            {
                if (!Social(resident)) continue;
                List<TowerResident> list;
                if (!byRoom.TryGetValue(resident.currentRoom, out list))
                    byRoom[resident.currentRoom] = list = new List<TowerResident>();
                list.Add(resident);
            }
            foreach (var pair in byRoom)
            {
                var people = pair.Value;
                for (int i = 0; i < people.Count; i++)
                    for (int j = i + 1; j < people.Count; j++)
                        Socialise(people[i], people[j], pair.Key, step, live);
            }
        }

        private void Socialise(TowerResident a, TowerResident b, int roomUid, float step, bool live)
        {
            var bond = BondOf(a.id, b.id, true);
            float rate = 0.06f;
            if (HasTrait(a, "Gregarious") || HasTrait(b, "Gregarious")) rate *= 1.5f;
            if (HasTrait(a, "Solitary") || HasTrait(b, "Solitary")) rate *= 0.5f;
            if (HasTrait(a, "Kind") || HasTrait(b, "Kind")) rate *= 1.25f;
            if (a.happiness < 25 || b.happiness < 25 || a.breakSeconds > 0 || b.breakSeconds > 0) rate = -0.08f;
            bond.opinion = Mathf.Clamp(bond.opinion + rate * step, -100, 100);
            if (!live) return;
            var room = Room(roomUid);
            string where = room == null ? "the Tower" : "the " + TowerCatalog.Get(room.type).displayName;
            if (bond.opinion <= -35 && (a.happiness < 35 || b.happiness < 35) && Random01() < 0.03f * step / 5f)
            {
                foreach (var hurt in new[] { a, b })
                {
                    hurt.hp = Mathf.Max(1, hurt.hp - 8);
                    hurt.injury = Mathf.Min(100, hurt.injury + 10);
                    hurt.happiness = Mathf.Max(0, hurt.happiness - 5);
                }
                bond.opinion = Mathf.Max(-100, bond.opinion - 12);
                Note(a.name + " and " + b.name + " came to blows in " + where + ".");
                Bump("fight");
                Emit("fight", roomUid, a.id, a.name + " vs " + b.name);
                return;
            }
            if (bond.opinion >= 75 && a.familyPartnerId == 0 && b.familyPartnerId == 0 &&
                a.happiness >= 55 && b.happiness >= 55 && a.homeRoom > 0 && b.homeRoom > 0 &&
                BiologicalPopulation() < PopulationCap() && Random01() < 0.01f * step / 5f &&
                PairFamily(a.id, b.id) == null)
            {
                Note(a.name + " and " + b.name + " fell in love in " + where + ".");
                Emit("romance", roomUid, a.id, a.name + " & " + b.name);
            }
        }

        private void AddSocialThoughts(TowerResident resident, List<TowerThought> list)
        {
            bool friend = false, rival = false;
            if (Social(resident))
                foreach (var other in State.residents)
                {
                    if (other.id == resident.id || other.currentRoom != resident.currentRoom || !Social(other)) continue;
                    float opinion = Opinion(resident.id, other.id);
                    if (opinion >= 40) friend = true;
                    if (opinion <= -30) rival = true;
                }
            if (friend) list.Add(new TowerThought("Beside a friend", 5));
            if (rival) list.Add(new TowerThought("Stuck with a rival", -7));
            bool generalGrief = false;
            foreach (var dead in State.memorial)
            {
                float since = State.clock - dead.diedAt;
                if (since < 0 || since > DaySeconds * 2) continue;
                if (dead.partnerId == resident.id) list.Add(new TowerThought("Lost their partner, " + dead.name, -20));
                else if (dead.friends.Contains(resident.id)) list.Add(new TowerThought("Mourning " + dead.name, -9));
                else if (since < DaySeconds) generalGrief = true;
            }
            if (generalGrief) list.Add(new TowerThought("A death in the Tower", -3));
        }

        // ---- Mood breaks ----------------------------------------------------------------

        private string ChooseBreak(TowerResident resident)
        {
            switch (resident.trait)
            {
                case "Stout": case "Frugal": return "binge";
                case "Brave": case "Industrious": return "tantrum";
                case "Curious": case "Night Owl": case "Solitary": return "wander";
                case "Kind": case "Gregarious": case "Careful": return "sulk";
            }
            string[] kinds = { "sulk", "binge", "tantrum", "wander" };
            return kinds[Mathf.Min(3, (int)(Random01() * 4))];
        }

        public static string BreakLabel(string kind)
        {
            switch (kind)
            {
                case "binge": return "Food binge";
                case "tantrum": return "Tantrum";
                case "wander": return "Wandering";
                default: return "Sulking";
            }
        }

        private int BreakTarget(TowerResident resident)
        {
            var heart = State.rooms.Find(r => r.type == "heart");
            int home = resident.homeRoom > 0 ? resident.homeRoom : heart == null ? 0 : heart.uid;
            switch (resident.breakKind)
            {
                case "tantrum": return resident.jobRoom > 0 ? resident.jobRoom : home;
                case "wander":
                    var gate = State.rooms.Find(r => r.type == "gate");
                    return gate == null ? home : gate.uid;
                case "binge":
                    var kitchen = State.rooms.Find(r => r.type == "kitchen" || r.type == "frosted_mug");
                    return kitchen == null ? home : kitchen.uid;
                default: return home;
            }
        }

        private void BeginBreak(TowerResident resident)
        {
            resident.breakSeconds = 40;
            resident.breakKind = ChooseBreak(resident);
            string what = resident.breakKind == "binge" ? " is raiding the stores in a food binge." :
                resident.breakKind == "tantrum" ? " is throwing a tantrum and smashing things." :
                resident.breakKind == "wander" ? " walked off to stare out of the Gate." : " is sulking in their room.";
            Note(resident.name + what);
            Emit("break", resident.currentRoom, resident.id, BreakLabel(resident.breakKind).ToUpperInvariant());
        }

        private void ApplyBreak(TowerResident resident, float dt)
        {
            if (resident.currentRoom != resident.targetRoom) return;
            if (resident.breakKind == "binge")
            {
                float eaten = Mathf.Min(State.food, 0.25f * dt);
                State.food -= eaten;
                resident.hunger = Mathf.Min(100, resident.hunger + eaten * 4);
            }
            else if (resident.breakKind == "tantrum")
            {
                var room = Room(resident.currentRoom);
                if (room != null && room.type != "heart" && room.type != "gate")
                    room.condition = Mathf.Max(5, room.condition - 0.12f * dt);
            }
        }

        // ---- Steward: RimWorld-style self-organising work, Fallout Shelter-style stat matching -----

        public string SetSteward(bool on)
        {
            State.steward = on;
            Note(on ? "The Steward will move workers to keep food, water and firewood flowing." :
                "The Steward stepped down. Assignments are entirely yours.");
            return null;
        }

        private float WorkerRate(TowerResident resident, TowerRoom room)
        {
            return (0.35f + MatchScore(resident, room) * 0.19f) *
                Mathf.Lerp(0.65f, 1.15f, resident.happiness / 100f) * TraitWorkMultiplier(resident) *
                Mathf.Clamp(room.condition / 100f, 0.2f, 1f) * AdjacencyBonus(room);
        }

        private int AssignedTo(TowerRoom room, bool home)
        {
            int used = 0;
            foreach (var r in State.residents) if ((home ? r.homeRoom : r.jobRoom) == room.uid) used++;
            return used;
        }

        // Income the current assignments should sustain, allowing for meals and sleep.
        public float PlannedIncomePerMinute(string resource)
        {
            float perSecond = 0;
            foreach (var room in State.rooms)
            {
                var def = TowerCatalog.Get(room.type);
                if (def == null || def.produces != resource) continue;
                float rate = 0;
                foreach (var r in State.residents)
                    if (r.jobRoom == room.uid && r.ageStage == 0 && !r.downed && !r.exploring && !r.away)
                        rate += WorkerRate(r, room);
                perSecond += rate * 0.75f / 90f * CollectAmount(room);
            }
            return perSecond * 60f;
        }

        public float PlannedNetPerMinute(string resource)
        { return PlannedIncomePerMinute(resource) - UpkeepPerMinute(resource); }

        private bool Urgent(string resource)
        {
            float net = PlannedNetPerMinute(resource);
            if (net >= -0.05f) return false;
            float stock = StockOf(resource);
            return stock / -net < 10f || stock < StockCap() * 0.3f;
        }

        private bool Movable(TowerResident r)
        {
            if (r.ageStage != 0 || r.downed || r.exploring || r.away || r.breakSeconds > 0) return false;
            var job = Room(r.jobRoom);
            if (job == null) return true;
            if (job.type == "gate") return false;
            var def = TowerCatalog.Get(job.type);
            // Never pull someone off a stock that is itself running short.
            return System.Array.IndexOf(Stocks, def.produces) < 0 || !Urgent(def.produces) &&
                StockOf(def.produces) > StockCap() * 0.5f && PlannedNetPerMinute(def.produces) > 2f;
        }

        // Moves one worker towards the most urgent shortage. Returns true when it moved someone.
        private bool StewardPass()
        {
            string worst = null;
            float worstMinutes = float.MaxValue;
            foreach (string resource in Stocks)
            {
                if (!Urgent(resource)) continue;
                float minutes = StockOf(resource) / -PlannedNetPerMinute(resource);
                if (minutes < worstMinutes) { worst = resource; worstMinutes = minutes; }
            }
            if (worst == null) return false;
            TowerRoom target = null;
            foreach (var room in State.rooms)
            {
                var def = TowerCatalog.Get(room.type);
                if (def == null || def.produces != worst || room.condition < 20 ||
                    State.incidents.Exists(i => i.roomUid == room.uid) || AssignedTo(room, false) >= Capacity(room)) continue;
                if (target == null || AdjacencyBonus(room) > AdjacencyBonus(target)) target = room;
            }
            if (target == null) return false;
            TowerResident best = null;
            float bestScore = float.MinValue;
            foreach (var r in State.residents)
            {
                if (!Movable(r)) continue;
                float score = MatchScore(r, target) + (r.jobRoom == 0 ? 3 : 0);
                if (score > bestScore) { best = r; bestScore = score; }
            }
            if (best == null || Assign(best.id, target.uid) != null) return false;
            Note("Steward: " + best.name + " moved to the " + TowerCatalog.Get(target.type).displayName +
                " to keep " + worst + " flowing.");
            Emit("steward", target.uid, best.id, best.name);
            return true;
        }

        private void TickSteward(float dt)
        {
            State.stewardTimer += dt;
            bool empty = State.food <= 1 || State.water <= 1 || State.firewood <= 1;
            if (State.stewardTimer < (empty ? 5f : 20f)) return;
            State.stewardTimer = 0;
            StewardPass();
        }

        public int StaffForSurvival(int maxMoves)
        {
            int moves = 0;
            while (moves < maxMoves && StewardPass()) moves++;
            return moves;
        }

        // Fallout Shelter's sort-by-stat: every idle adult goes where their best stat counts.
        public string AutoAssignIdle()
        {
            int placed = 0;
            foreach (var r in State.residents)
            {
                if (r.jobRoom != 0 || r.ageStage != 0 || r.downed || r.exploring || r.away) continue;
                TowerRoom best = null;
                float bestScore = float.MinValue;
                foreach (var room in State.rooms)
                {
                    var def = TowerCatalog.Get(room.type);
                    if (def == null || (string.IsNullOrEmpty(def.produces) && def.kind != "train") ||
                        AssignedTo(room, false) >= Capacity(room)) continue;
                    float score = MatchScore(r, room) + (System.Array.IndexOf(Stocks, def.produces) >= 0 &&
                        PlannedNetPerMinute(def.produces) < 0 ? 6 : 0);
                    if (score > bestScore) { best = room; bestScore = score; }
                }
                if (best != null && Assign(r.id, best.uid) == null) placed++;
            }
            if (placed == 0) return "Everyone already has a workplace, or no room has space.";
            Note("Placed " + placed + " idle resident" + (placed == 1 ? "" : "s") + " by their best stat.");
            return null;
        }
    }
}
