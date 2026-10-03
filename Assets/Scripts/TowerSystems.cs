using System;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Gameplay runs here; the cutaway and HUD only issue commands and display this state.
    public sealed partial class TowerRules
    {
        public float MatchScore(TowerResident resident, TowerRoom room)
        {
            if (resident == null || room == null || resident.ageStage != 0) return 0;
            var def = TowerCatalog.Get(room.type);
            if (def == null) return 0;
            return resident.Stat(def.stat) + resident.tool * 0.75f + resident.luck * 0.12f;
        }

        public float ProductionRate(TowerRoom room)
        {
            if (room == null) return 0;
            float rate = 0;
            foreach (var resident in State.residents)
            {
                if (resident.currentTask != "production" || resident.currentRoom != room.uid ||
                    resident.downed || resident.exploring) continue;
                float need = Mathf.Clamp01(Mathf.Min(resident.hunger, resident.thirst, resident.rest) / 50f);
                rate += (0.35f + MatchScore(resident, room) * 0.19f) *
                    Mathf.Lerp(0.65f, 1.15f, resident.happiness / 100f) * need *
                    TraitWorkMultiplier(resident);
            }
            if (rate <= 0) return 0;   // nobody producing: skip the neighbour scan
            return rate * Mathf.Clamp(room.condition / 100f, 0.2f, 1f) * AdjacencyBonus(room) * DistrictBonus(room);
        }

        public float AssignmentImpact(TowerResident resident, TowerRoom room)
        {
            if (resident == null || room == null || resident.ageStage != 0 || resident.downed ||
                resident.away || resident.exploring || resident.priorityProduction == 0 || room.ready ||
                room.condition < 20 || State.incidents.Exists(i => i.roomUid == room.uid) ||
                !IsPowered(room)) return 0;
            var def = TowerCatalog.Get(room.type);
            if (def == null || string.IsNullOrEmpty(def.produces) && def.kind != "train") return 0;
            if (resident.currentRoom == room.uid && resident.currentTask == "production") return 0;
            float need = Mathf.Clamp01(Mathf.Min(resident.hunger, resident.thirst, resident.rest) / 50f);
            return (0.35f + MatchScore(resident, room) * 0.19f) *
                Mathf.Lerp(0.65f, 1.15f, resident.happiness / 100f) * need *
                TraitWorkMultiplier(resident) *
                Mathf.Clamp(room.condition / 100f, 0.2f, 1f) * AdjacencyBonus(room) * DistrictBonus(room);
        }

        public string TaskExplanation(TowerResident resident)
        {
            if (resident == null) return "Select a resident.";
            if (resident.ageStage == 1) return "Child: needs a home and supplies; cannot work yet.";
            if (resident.downed) return "Downed: a resident with care priority and tonics must rescue them.";
            if (resident.breakSeconds > 0) return "Mood break: too unhappy to work for a moment.";
            if (resident.currentTask == "rest" && Sleeping(resident)) return "Asleep: keeping a " + resident.schedule + " schedule.";
            if (IsPosted(resident)) return "Away: " + PostingLabel(resident) + ".";
            if (resident.exploring) return "Exploring for " + resident.exploreChoice + ".";
            if (IsErrand(resident.currentTask)) return ErrandLabel(resident);
            if (resident.currentTask != "idle")
                return resident.currentRoom != resident.targetRoom ? "Traveling to " + resident.currentTask + "." :
                    "Working: " + resident.currentTask + ".";
            var room = Room(resident.jobRoom);
            if (room == null) return "Idle: assign a workplace.";
            if (resident.priorityProduction == 0) return "Idle: production priority is off.";
            if (room.ready) return "Idle: collect the ready room or unlock hauling.";
            if (room.condition < 20) return "Idle: workplace needs repair.";
            if (!IsPowered(room))
                return "Idle: the workplace is dark. Staff a Lumber Mill to relight it.";
            return "Idle: available for an urgent task.";
        }

        public float RushChance(int roomUid)
        {
            var room = Room(roomUid);
            if (room == null) return 0;
            if (room.condition < 20 || State.incidents.Exists(i => i.roomUid == roomUid)) return 0;
            float skill = 0, luck = 0;
            int count = 0;
            foreach (var resident in State.residents)
                if (resident.jobRoom == roomUid && resident.ageStage == 0 && !resident.downed)
                { skill += MatchScore(resident, room) + (HasTrait(resident, "Careful") ? 0.8f : 0); luck += resident.luck; count++; }
            if (count == 0) return 0;
            return Mathf.Clamp(0.27f + skill / count * 0.052f + luck / count * 0.013f +
                room.condition * 0.0015f - room.rushFatigue * 0.20f, 0.15f, 0.90f);
        }

        public string Rush(int roomUid)
        {
            var room = Room(roomUid);
            var def = room == null ? null : TowerCatalog.Get(room.type);
            if (def == null || string.IsNullOrEmpty(def.produces) || room.ready) return "That room cannot be rushed.";
            if (RushChance(roomUid) <= 0) return "Assign an adult worker first.";
            if (room.rushFatigue >= 2.5f) return "Let this room recover before rushing again.";
            bool success = Random01() < RushChance(roomUid);
            if (State.tutorialStep == 3) State.tutorialStep = 4;
            room.rushFatigue += 0.8f;
            if (success)
            {
                room.ready = true; room.progress = 1;
                foreach (var resident in State.residents)
                    if (resident.jobRoom == roomUid) resident.happiness = Mathf.Min(100, resident.happiness + 3);
                Note("Rush succeeded in " + def.displayName + ". Collect the supplies.");
                Bump("rush_ok");
                Emit("rush_ok", roomUid, 0, def.displayName);
            }
            else
            {
                room.condition = Mathf.Max(5, room.condition - 7);
                StartIncident(Random01() < 0.55f ? "fire" : "pests", roomUid);
                Note("Rush failed in " + def.displayName + ". Respond to the incident!");
                Emit("rush_fail", roomUid, 0, def.displayName);
            }
            return null;
        }

        public string SetPriority(int residentId, string job, int value)
        {
            var resident = Resident(residentId);
            if (resident == null || resident.ageStage != 0) return "Choose an adult resident.";
            if (value < 0 || value > 3) return "Priority must be between 0 and 3.";
            if (value > 0 && Incapable(resident) == job)
                return resident.name + " will not do " + job + " work (" + Backstory(resident.backstory).name + ").";
            switch (job)
            {
                case "production": resident.priorityProduction = value; break;
                case "haul": resident.priorityHaul = value; break;
                case "repair": resident.priorityRepair = value; break;
                case "fire": resident.priorityFire = value; break;
                case "care": resident.priorityCare = value; break;
                case "defense": resident.priorityDefense = value; break;
                default: return "Unknown work type.";
            }
            Note(resident.name + "'s " + job + " priority is now " + value + ".");
            if (State.tutorialStep == 4)
            {
                State.tutorialStep = State.incidents.Count > 0 ? 6 : 5;
                if (State.tutorialStep == 5) State.eventCooldown = Mathf.Min(State.eventCooldown, 20f);
            }
            return null;
        }

        public string UnlockHauling()
        {
            if (State.haulingUnlocked) return "Automatic hauling is already unlocked.";
            if (State.gold < 250 || State.wood < 10 || State.residents.Count < 3)
                return "Auto-hauling needs 250 gold, 10 wood, and three residents.";
            State.gold -= 250; State.wood -= 10;
            State.haulingUnlocked = true;
            if (!State.policies.Contains("auto_haul")) State.policies.Add("auto_haul");
            Note("Residents can now haul ready production automatically.");
            return null;
        }

        public string PairFamily(int firstId, int secondId)
        {
            var a = Resident(firstId); var b = Resident(secondId);
            if (a == null || b == null || a.id == b.id || a.ageStage != 0 || b.ageStage != 0)
                return "Select two adult residents.";
            if (a.familyPartnerId != 0 || b.familyPartnerId != 0) return "One resident already has a family partner.";
            if (a.homeRoom == 0 || b.homeRoom == 0) return "Both residents need a home.";
            if (a.happiness < 45 || b.happiness < 45) return "Both residents need better morale.";
            if (BiologicalPopulation() >= PopulationCap()) return "Build more housing first.";
            a.familyPartnerId = b.id; b.familyPartnerId = a.id;
            a.familySeconds = b.familySeconds = 0;
            Note(a.name + " and " + b.name + " formed a family.");
            return null;
        }

        public string RecruitVisitor()
        {
            if (State.pendingVisitors <= 0) return "No visitor is waiting at the Gate.";
            if (BiologicalPopulation() >= DwellerCap(State.heartRank))
                return "The Heart shelters " + DwellerCap(State.heartRank) + " people at rank " + TowerTiers.Tier(State.heartRank) +
                    ". Raise it to take in more.";
            var home = AvailableHome();
            if (home == null) return "A visitor needs an open bed.";
            int id = State.nextResidentId;
            var resident = AddResident("", "Silverbrook Wanderer " + id, "villager", 1);
            resident.homeRoom = resident.currentRoom = home.uid;
            RollTemperament(resident);
            resident.might = 2 + (int)(Random01() * 5);
            resident.sight = 2 + (int)(Random01() * 5);
            resident.grit = 2 + (int)(Random01() * 5);
            resident.charm = 2 + (int)(Random01() * 5);
            resident.wit = 2 + (int)(Random01() * 5);
            resident.grace = 2 + (int)(Random01() * 5);
            resident.luck = 2 + (int)(Random01() * 5);
            GiveDepth(resident);
            State.pendingVisitors--;
            Note(resident.name + " joined the Tower (" + resident.trait + ").");
            Bump("recruit");
            Emit("recruit", home.uid, resident.id, resident.name);
            return null;
        }

        public string CraftEquipment(int residentId, string kind)
        {
            var resident = Resident(residentId);
            if (resident == null || resident.ageStage != 0) return "Choose an adult resident.";
            if (!State.rooms.Exists(r => r.type == "forge")) return "Build a Forge first.";
            if (State.gold < 70 || State.ore < 2 || State.wood < 2)
                return "Crafting needs 70 gold, 2 ore, and 2 wood.";
            if (kind == "weapon" && resident.weapon >= 3 || kind == "tool" && resident.tool >= 3)
                return "That equipment is already at maximum quality.";
            if (kind != "weapon" && kind != "tool") return "Unknown equipment.";
            State.gold -= 70; State.ore -= 2; State.wood -= 2;
            if (kind == "weapon") resident.weapon++; else resident.tool++;
            Note(resident.name + " received an improved " + kind + ".");
            return null;
        }

        public string StartIncident(string kind, int roomUid)
        {
            var room = Room(roomUid);
            if (room == null || room.type == "heart" || room.type == "gate") return "Choose a regular room.";
            if (kind != "fire" && kind != "pests" && kind != "raiders" && kind != "illness" &&
                kind != "cave_in") return "Unknown incident.";
            if (State.incidents.Exists(i => i.roomUid == roomUid)) return "That room is already in danger.";
            float hp = kind == "raiders" ? 100 : kind == "pests" ? 65 * (Researched("DEF-2") ? 0.8f : 1f) : kind == "cave_in" ? 70 : 45;
            State.incidents.Add(new TowerIncident { roomUid = roomUid, kind = kind, hp = hp,
                severity = 1 + Mathf.Min(2, State.residents.Count / 20f) });
            if (State.tutorialStep == 5) State.tutorialStep = 6;
            Note(kind + " reported in " + TowerCatalog.Get(room.type).displayName + "!");
            Emit("incident", roomUid, 0, kind);
            return null;
        }

        private bool townTick;   // residents run errands this tick (live play only, TowerTown.cs)

        private void Tick(float dt, bool live)
        {
            townTick = live && TownErrands;
            State.clock += dt;
            TickConstruction(dt);
            TickNeeds(dt, live);
            FlushDeaths();
            TickLife(dt, live);
            TickFamilies(dt);
            TickExploration(dt, live);
            TickPower();
            TickSocial(dt, live);
            if (State.steward) TickSteward(dt);
            if (State.heartWaiting.Count > 0) ReleaseHeartWaiting();
            PlanJobs();
            TickTravel(dt);
            TickWork(dt, live);
            TickRooms(dt);
            TickVisitors(dt);
            TickDistricts(dt);
            TickTown(dt);
            TickOutposts(dt, live);
            if (live) { TickSiege(dt); TickIncidents(dt); TickEvents(dt); }
            // DEF-8 Celestial aegis: a calm Heart chamber slowly mends the Heart.
            if (Researched("DEF-8") && State.heartHp > 0 && State.heartHp < HeartMaxHp(State.heartRank) &&
                !State.incidents.Exists(i => i.roomUid == Room0("heart")))
                State.heartHp = Mathf.Min(HeartMaxHp(State.heartRank), State.heartHp + 0.5f * dt);
            CheckHeartStage();
            if (State.heartHp <= 0) { State.heartHp = 0; State.defeated = true; }
        }
    }
}
