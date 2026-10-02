using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    public sealed partial class TowerRules
    {
        public string SendExploring(int residentId, string choice)
        {
            var resident = Resident(residentId);
            if (resident == null || resident.ageStage != 0 || resident.downed ||
                resident.away || resident.exploring) return "Resident unavailable.";
            if (choice != "supplies" && choice != "relics" && choice != "patrol")
                return "Choose supplies, relics, or patrol.";
            resident.exploring = true;
            resident.exploreChoice = choice;
            resident.currentTask = "exploring";
            resident.targetRoom = 0;
            Note(resident.name + " set out to seek " + choice + ".");
            return null;
        }

        private string ReturnExplorer(int residentId)
        {
            var resident = Resident(residentId);
            if (resident == null || !resident.exploring) return "Resident is not exploring.";
            resident.exploring = false;
            resident.exploreSeconds = 0;
            State.gold += resident.exploreGold;
            State.wood += resident.exploreWood;
            State.stone += resident.exploreStone;
            State.ore += resident.exploreOre;
            State.essence += resident.exploreEssence;
            State.celestium += resident.exploreCelestium;
            Note(resident.name + " returned with " + resident.exploreGold + " gold, " +
                resident.exploreWood + " wood, " + resident.exploreStone + " stone and " +
                resident.exploreCelestium + " Celestium.");
            resident.exploreGold = resident.exploreWood = resident.exploreStone = 0;
            resident.exploreOre = resident.exploreEssence = resident.exploreCelestium = 0;
            GiveXp(resident, 15);
            var heart = RoomAt(0, CoreX);
            resident.currentRoom = resident.homeRoom > 0 ? resident.homeRoom : heart == null ? 0 : heart.uid;
            resident.currentTask = "idle";
            Bump("expedition");
            Emit("expedition", resident.currentRoom, resident.id, resident.name);
            return null;
        }

        private void TickNeeds(float dt, bool live)
        {
            int cells = 0;
            foreach (var room in State.rooms) if (room.type != "heart" && room.type != "gate") cells += room.width;
            State.firewood = Mathf.Max(0, State.firewood - 0.00065f * cells * dt);
            BeginMoodPass();
            try { NeedsPass(dt, live); }
            finally { floorHeadcount = null; cachedAmenities = -1; }
        }

        private readonly Dictionary<int, int> headcountBuffer = new Dictionary<int, int>();

        private void BeginMoodPass()
        {
            headcountBuffer.Clear();
            foreach (var other in State.residents)
            {
                if (other.origin == "body" || other.currentRoom <= 0) continue;
                var room = Room(other.currentRoom);
                if (room == null) continue;
                int count;
                headcountBuffer.TryGetValue(room.floor, out count);
                headcountBuffer[room.floor] = count + 1;
            }
            cachedAmenities = -1;
            cachedAmenities = AmenityKinds();
            floorHeadcount = headcountBuffer;
        }

        private void NeedsPass(float dt, bool live)
        {
            foreach (var resident in State.residents)
            {
                if (resident.origin == "body")
                {
                    resident.charge = Mathf.Max(0, resident.charge - dt);
                    if (resident.currentTask == "recharge" && resident.currentRoom == resident.targetRoom)
                        resident.charge = Mathf.Min(2160, resident.charge + 4 * dt);
                    continue;
                }
                if (resident.away) continue;
                resident.hunger = Mathf.Max(0, resident.hunger - (resident.ageStage == 1 ? 0.11f : 0.15f) *
                    (HasTrait(resident, "Stout") ? 0.85f : 1f) * dt);
                resident.thirst = Mathf.Max(0, resident.thirst - (resident.ageStage == 1 ? 0.13f : 0.17f) * dt);
                resident.rest = Mathf.Max(0, resident.rest - (resident.ageStage == 1 ? 0.04f : 0.09f) * dt);
                float ration = HasTrait(resident, "Frugal") ? 1.7f : 2f;
                if (resident.hunger < 65 && State.food >= ration)
                { State.food -= ration; resident.hunger = Mathf.Min(100, resident.hunger + 28); }
                if (resident.thirst < 65 && State.water >= ration)
                { State.water -= ration; resident.thirst = Mathf.Min(100, resident.thirst + 30); }
                if (resident.ageStage == 1 || resident.currentTask == "rest")
                    resident.rest = Mathf.Min(100, resident.rest + 0.70f * dt);
                bool shortage = resident.hunger < 15 || resident.thirst < 15 || State.firewood <= 0;
                float moodTarget = MoodTarget(resident);
                if (shortage) moodTarget = Mathf.Min(moodTarget, 28);
                resident.happiness = Mathf.MoveTowards(resident.happiness, moodTarget, 0.09f * dt);
                if (resident.illness > 0)
                    resident.illness = Mathf.Max(0, resident.illness - 0.015f * dt);
                if (shortage || resident.illness > 50)
                {
                    resident.hp = Mathf.Max(live ? 0 : 1, resident.hp -
                        (shortage ? 0.07f : 0.03f) * dt);
                    // Collapsing from hunger is not a wound: feeding them is the cure.
                    if (resident.hp <= 0) resident.downed = true;
                }
                else if (!resident.downed && resident.hp < MaxHp(resident) && resident.injury < 15)
                    resident.hp = Mathf.Min(MaxHp(resident), resident.hp + 0.025f * dt);
                TickRecovery(resident, dt, live, shortage);
            }
        }

        private void TickFamilies(float dt)
        {
            foreach (var resident in State.residents.ToArray())
            {
                if (resident.ageStage == 1)
                {
                    var nursery = Room(resident.homeRoom);
                    resident.growthSeconds += dt * (nursery != null && nursery.type == "nursery" ? 1.4f : 1f);
                    if (resident.growthSeconds >= 2 * DaySeconds)
                    {
                        resident.ageStage = 0;
                        resident.origin = "villager";
                        resident.name = "Silverbrook Youth " + resident.id;
                        resident.hunger = resident.thirst = resident.rest = 80;
                        Note(resident.name + " is ready to work.");
                    }
                    continue;
                }
                if (resident.familyPartnerId <= resident.id) continue;
                var partner = Resident(resident.familyPartnerId);
                if (partner == null || partner.familyPartnerId != resident.id) continue;
                if (resident.downed || partner.downed || resident.happiness < 55 || partner.happiness < 55 ||
                    resident.homeRoom == 0 || partner.homeRoom == 0 || BiologicalPopulation() >= PopulationCap())
                    continue;
                resident.familySeconds += dt;
                if (resident.familySeconds < DaySeconds) continue;
                TowerRoom home = null;
                foreach (var candidate in State.rooms)
                    if (candidate.type == "nursery")
                    {
                        int used = State.residents.FindAll(r => r.homeRoom == candidate.uid).Count;
                        if (used < Capacity(candidate)) { home = candidate; break; }
                    }
                if (home == null) home = AvailableHome();
                if (home == null) continue;
                int id = State.nextResidentId;
                var child = AddResident("", "Child of " + resident.name + " and " + partner.name,
                    "child", 1);
                child.ageStage = 1;
                child.homeRoom = child.currentRoom = home.uid;
                child.might = Mathf.Max(1, (resident.might + partner.might) / 2);
                child.sight = Mathf.Max(1, (resident.sight + partner.sight) / 2);
                child.grit = Mathf.Max(1, (resident.grit + partner.grit) / 2);
                child.charm = Mathf.Max(1, (resident.charm + partner.charm) / 2);
                child.wit = Mathf.Max(1, (resident.wit + partner.wit) / 2);
                child.grace = Mathf.Max(1, (resident.grace + partner.grace) / 2);
                child.luck = Mathf.Max(1, (resident.luck + partner.luck) / 2);
                resident.familySeconds = -2 * DaySeconds;
                Note("A child joined " + resident.name + " and " + partner.name + "'s family.");
                Bump("child");
                Emit("child", home.uid, child.id, child.name);
            }
        }

        private void TickExploration(float dt, bool live)
        {
            foreach (var resident in State.residents.ToArray())
            {
                if (!resident.exploring) continue;
                resident.exploreSeconds += dt;
                while (resident.exploreSeconds >= 60)
                {
                    resident.exploreSeconds -= 60;
                    if (resident.exploreChoice == "relics")
                    {
                        resident.exploreGold += 3 + resident.luck;
                        resident.exploreOre += 1 + resident.sight / 5;
                        resident.exploreCelestium += Random01() < 0.4f ? 1 : 0;
                        if (Random01() < 0.08f) resident.tool = Mathf.Min(3, resident.tool + 1);
                    }
                    else if (resident.exploreChoice == "patrol")
                    {
                        resident.exploreGold += 7 + resident.luck * 2;
                        resident.exploreEssence += Random01() < 0.35f ? 1 : 0;
                        if (Random01() < 0.08f) resident.weapon = Mathf.Min(3, resident.weapon + 1);
                    }
                    else
                    {
                        resident.exploreGold += 5 + resident.luck;
                        resident.exploreWood += 1 + resident.might / 5;
                        resident.exploreStone += 1 + resident.grit / 5;
                    }
                    if (live && Random01() < 0.045f + (resident.exploreChoice == "patrol" ? 0.025f : 0))
                    {
                        resident.hp = Mathf.Max(1, resident.hp - Mathf.Max(3, 12 - resident.grit - resident.weapon));
                        resident.injury = Mathf.Min(100, resident.injury + 8);
                    }
                    if (resident.hp < 20) { ReturnExplorer(resident.id); break; }
                }
            }
        }

        // How many residents hold each (task, target room) pair. PlanJobs keeps it current as it assigns, so each
        // resident still sees the claims made by the residents planned before it.
        private readonly Dictionary<string, Dictionary<int, int>> claims = new Dictionary<string, Dictionary<int, int>>();
        private readonly HashSet<int> incidentRooms = new HashSet<int>();

        private void Claim(string task, int room, int delta)
        {
            if (room <= 0 || task == null) return;
            Dictionary<int, int> byRoom;
            if (!claims.TryGetValue(task, out byRoom)) { byRoom = new Dictionary<int, int>(); claims[task] = byRoom; }
            int count;
            byRoom.TryGetValue(room, out count);
            byRoom[room] = count + delta;
        }

        // Residents other than this one already on (task, room).
        private int ClaimedByOthers(TowerResident resident, string task, int room)
        {
            Dictionary<int, int> byRoom;
            int count;
            if (!claims.TryGetValue(task, out byRoom) || !byRoom.TryGetValue(room, out count)) return 0;
            return resident.currentTask == task && resident.targetRoom == room ? count - 1 : count;
        }

        private void PlanJobs()
        {
            foreach (var byRoom in claims.Values) byRoom.Clear();
            foreach (var r in State.residents) Claim(r.currentTask, r.targetRoom, 1);
            incidentRooms.Clear();
            foreach (var incident in State.incidents) incidentRooms.Add(incident.roomUid);
            TowerRoom heartRoom = null;
            foreach (var room in State.rooms) if (room.type == "heart") { heartRoom = room; break; }
            foreach (var resident in State.residents)
            {
                string oldTask = resident.currentTask;
                int oldTarget = resident.targetRoom;
                PlanJob(resident, heartRoom);
                if (resident.currentTask != oldTask || resident.targetRoom != oldTarget)
                { Claim(oldTask, oldTarget, -1); Claim(resident.currentTask, resident.targetRoom, 1); }
            }
        }

        private void PlanJob(TowerResident resident, TowerRoom heartRoom)
        {
            {
                if (resident.away || resident.exploring || resident.downed || resident.ageStage != 0)
                { resident.currentTask = resident.ageStage == 1 ? "child" : resident.downed ? "downed" : "away"; return; }
                if (resident.origin == "body" && resident.charge < 300)
                {
                    SetTask(resident, "recharge", heartRoom == null ? 0 : heartRoom.uid);
                    return;
                }
                if (resident.origin != "body" &&
                    (resident.rest < 25 || resident.hunger < 12 || resident.thirst < 12))
                {
                    SetTask(resident, "rest", resident.homeRoom > 0 ? resident.homeRoom :
                        heartRoom == null ? 0 : heartRoom.uid);
                    return;
                }
                if (resident.breakSeconds > 0)
                {
                    SetTask(resident, "break", BreakTarget(resident));
                    return;
                }
                string task = "idle";
                int target = resident.homeRoom;
                float best = 0;
                if (resident.origin != "body" && resident.homeRoom > 0 && Sleeping(resident))
                { best = 98; task = "rest"; target = resident.homeRoom; }
                foreach (var incident in State.incidents)
                {
                    int priority = incident.kind == "fire" ? resident.priorityFire :
                        incident.kind == "illness" ? resident.priorityCare :
                        incident.kind == "cave_in" ? resident.priorityRepair : resident.priorityDefense;
                    if (priority <= 0) continue;
                    string candidate = incident.kind == "fire" ? "fire" :
                        incident.kind == "illness" ? "care" :
                        incident.kind == "cave_in" ? "repair" : "defense";
                    int assigned = ClaimedByOthers(resident, candidate, incident.roomUid);
                    if (assigned >= 4) continue;
                    float score = 80 + priority * 12 + incident.hp * 0.03f -
                        Mathf.Abs((Room(resident.currentRoom) ?? Room(incident.roomUid)).floor -
                            Room(incident.roomUid).floor) * 2;
                    score -= assigned * 6;
                    if (score > best) { best = score; task = candidate; target = incident.roomUid; }
                }
                // Care still happens without tonics, only slower: triage keeps the downed alive.
                if (resident.priorityCare > 0)
                    foreach (var patient in State.residents)
                    {
                        if (patient.id == resident.id ||
                            (!patient.downed && patient.injury < 30 && patient.illness < 30)) continue;
                        int room = patient.currentRoom > 0 ? patient.currentRoom : patient.homeRoom;
                        if (room > 0 && 65 + resident.priorityCare * 9 > best &&
                            ClaimedByOthers(resident, "care", room) == 0)
                        { best = 65 + resident.priorityCare * 9; task = "care"; target = room; }
                    }
                if (resident.priorityRepair > 0 && State.wood > 0 && State.stone > 0)
                    foreach (var room in State.rooms)
                        if (room.condition < 65 && !incidentRooms.Contains(room.uid) &&
                            45 + resident.priorityRepair * 9 > best &&
                            ClaimedByOthers(resident, "repair", room.uid) == 0)
                        { best = 45 + resident.priorityRepair * 9; task = "repair"; target = room.uid; }
                if (State.haulingUnlocked && resident.priorityHaul > 0)
                    foreach (var room in State.rooms)
                        if (room.ready && 25 + resident.priorityHaul * 8 > best)
                        {
                            if (ClaimedByOthers(resident, "haul", room.uid) > 0) continue;
                            float score = 25 + resident.priorityHaul * 8 -
                                Mathf.Abs((Room(resident.currentRoom) ?? room).floor - room.floor) * 0.5f;
                            if (score > best) { best = score; task = "haul"; target = room.uid; }
                        }
                var workplace = Room(resident.jobRoom);
                if (workplace != null && workplace.type == "gate")
                {
                    // The Gate is a guard post: guards hold it unless something more urgent calls.
                    if (resident.priorityDefense > 0 && 40 + resident.priorityDefense * 8 > best)
                    { best = 40 + resident.priorityDefense * 8; task = "guard"; target = workplace.uid; }
                }
                else if (workplace != null && resident.priorityProduction > 0 && !workplace.ready &&
                    workplace.condition >= 20 && IsPowered(workplace) &&
                    40 + resident.priorityProduction * 8 > best)
                { best = 40 + resident.priorityProduction * 8; task = "production"; target = workplace.uid; }
                if (workplace != null && workplace.type != "gate")
                {
                    if (resident.duty == "repair" && resident.priorityRepair > 0 && workplace.condition < 100 &&
                        State.wood > 0 && State.stone > 0 && 60 + resident.priorityRepair * 8 > best)
                    { best = 60 + resident.priorityRepair * 8; task = "repair"; target = workplace.uid; }
                    else if (resident.duty == "haul" && resident.priorityHaul > 0 && workplace.ready &&
                        60 + resident.priorityHaul * 8 > best)
                    { best = 60 + resident.priorityHaul * 8; task = "haul"; target = workplace.uid; }
                }
                SetTask(resident, task, target);
            }
        }

        private void SetTask(TowerResident resident, string task, int roomUid)
        {
            if (roomUid == 0) { resident.currentTask = "idle"; resident.targetRoom = 0; return; }
            if (resident.targetRoom != roomUid)
            {
                var from = Room(resident.currentRoom);
                var to = Room(roomUid);
                resident.travelSeconds = 0;
                resident.travelDuration = from == null || to == null ? 0 :
                    1.5f + Mathf.Abs(to.floor - from.floor) * 2.3f + Mathf.Abs(to.x - from.x) * 0.17f;
            }
            resident.currentTask = task;
            resident.targetRoom = roomUid;
        }

        private void TickTravel(float dt)
        {
            foreach (var resident in State.residents)
            {
                if (resident.ageStage != 0 || resident.away || resident.exploring || resident.downed ||
                    resident.targetRoom <= 0 || resident.currentRoom == resident.targetRoom) continue;
                resident.travelSeconds += dt;
                if (resident.travelSeconds >= resident.travelDuration)
                { resident.currentRoom = resident.targetRoom; resident.travelSeconds = resident.travelDuration = 0; }
            }
        }

        private void TickWork(float dt, bool live)
        {
            foreach (var resident in State.residents)
            {
                if (resident.ageStage != 0 || resident.downed || resident.exploring ||
                    resident.currentRoom != resident.targetRoom) continue;
                var room = Room(resident.currentRoom);
                if (room == null) continue;
                if (resident.currentTask == "haul" && room.ready) Collect(room.uid);
                else if (resident.currentTask == "repair" && State.wood > 0 && State.stone > 0)
                {
                    room.condition = Mathf.Min(100, room.condition + (0.25f + resident.might * 0.05f) * dt);
                    room.repairProgress += dt;
                    if (room.repairProgress >= 20)
                    { room.repairProgress -= 20; State.wood--; State.stone--; }
                }
                else if (resident.currentTask == "care")
                {
                    float quality = State.tonics > 0 ? 1f : 0.45f;
                    TowerResident patient = null;
                    foreach (var other in State.residents)
                        if (other.id != resident.id &&
                            (other.currentRoom == room.uid || other.currentRoom == 0 && other.homeRoom == room.uid) &&
                            (other.downed || other.injury >= 30 || other.illness >= 30))
                        { patient = other; break; }
                    if (patient != null)
                    {
                        float oldIllness = patient.illness;
                        float oldInjury = patient.injury;
                        patient.hp = Mathf.Min(MaxHp(patient), patient.hp + (0.3f + resident.wit * 0.1f) * quality * dt);
                        patient.injury = Mathf.Max(0, patient.injury - 0.25f * quality * dt);
                        patient.illness = Mathf.Max(0, patient.illness -
                            (0.25f + resident.wit * 0.10f) * quality * dt);
                        if (patient.hp >= 35 && patient.downed)
                        {
                            patient.downed = false; patient.criticalSeconds = 0;
                            if (State.tonics > 0) State.tonics--;
                            Note(patient.name + " was rescued by " + resident.name + ".");
                            GiveXp(resident, 8);
                            Bump("healed"); Emit("healed", room.uid, patient.id, patient.name);
                        }
                        else if (oldIllness >= 30 && patient.illness < 30 ||
                            oldInjury >= 30 && patient.injury < 30)
                        {
                            if (State.tonics > 0) State.tonics--;
                            Note(patient.name + " recovered with care.");
                            GiveXp(resident, 5);
                            Bump("healed"); Emit("healed", room.uid, patient.id, patient.name);
                        }
                    }
                }
            }
        }

        private void TickRooms(float dt)
        {
            foreach (var room in State.rooms)
            {
                room.rushFatigue = Mathf.Max(0, room.rushFatigue - dt / 220f);
                var def = TowerCatalog.Get(room.type);
                if (def == null || room.condition < 20 || !IsPowered(room) ||
                    State.incidents.Count > 0 && State.incidents.Exists(i => i.roomUid == room.uid)) continue;
                float rate = ProductionRate(room);
                if (rate <= 0) continue;
                if (def.kind == "train")
                {
                    room.progress += rate * dt / 240f;
                    if (room.progress >= 1)
                    {
                        room.progress = 0;
                        Bump("trained");
                        Emit("trained", room.uid, 0, def.displayName);
                        foreach (var resident in State.residents)
                            if (resident.currentRoom == room.uid && resident.currentTask == "production")
                            {
                                if (room.type == "forge") resident.might = Mathf.Min(10, resident.might + 1);
                                else resident.wit = Mathf.Min(10, resident.wit + 1);
                                GiveXp(resident, 6);
                                Note(resident.name + " trained in " + def.displayName + ".");
                            }
                    }
                }
                else if (!string.IsNullOrEmpty(def.produces) && !room.ready)
                {
                    room.progress += rate * dt / 90f;
                    if (room.progress >= 1) { room.progress = 1; room.ready = true; }
                }
                room.condition = Mathf.Max(0, room.condition - 0.0006f * dt);
            }
        }

        private void TickVisitors(float dt)
        {
            if (RoomAt(0, GateX) == null || BiologicalPopulation() + State.pendingVisitors >= PopulationCap()) return;
            State.gateTimer += dt;
            if (State.gateTimer < 240) return;
            State.gateTimer -= 240;
            State.pendingVisitors = Mathf.Min(2, State.pendingVisitors + 1);
            Note("A wanderer is waiting at the Gate. Recruit them when a bed is open.");
        }

        private void TickIncidents(float dt)
        {
            foreach (var incident in State.incidents.ToArray())
            {
                var room = Room(incident.roomUid);
                if (room == null) { State.incidents.Remove(incident); continue; }
                float response = 0;
                int defenders = 0;
                foreach (var resident in State.residents)
                {
                    if (resident.currentRoom != room.uid || resident.downed || resident.ageStage != 0) continue;
                    float brave = HasTrait(resident, "Brave") ? 1.2f : 1f;
                    if (incident.kind == "fire" && resident.currentTask == "fire")
                    { response += (0.45f + resident.sight * 0.16f + resident.tool * 0.2f) * brave; defenders++; }
                    else if (incident.kind == "illness" && resident.currentTask == "care" && State.tonics > 0)
                    { response += (0.35f + resident.wit * 0.16f) * (HasTrait(resident, "Kind") ? 1.2f : 1f); defenders++; }
                    else if (incident.kind == "cave_in" && resident.currentTask == "repair")
                    { response += 0.30f + resident.might * 0.15f + resident.tool * 0.25f; defenders++; }
                    else if ((incident.kind == "raiders" || incident.kind == "pests") &&
                        (resident.currentTask == "defense" || resident.currentTask == "guard"))
                    { response += (0.35f + resident.might * 0.17f + resident.weapon * 0.42f) * brave; defenders++; }
                }
                incident.hp -= response * dt;
                if (incident.hp <= 0)
                {
                    State.incidents.Remove(incident);
                    if (State.tutorialStep == 6) State.tutorialStep = 7;
                    State.gold += (incident.kind == "raiders" ? 50 : 20) + incident.stolen;
                    Note("The " + IncidentName(incident.kind) + " in " + TowerCatalog.Get(room.type).displayName +
                        " was resolved" + (incident.stolen > 0 ? " and " + incident.stolen + " stolen gold recovered." : "."));
                    State.threat = Mathf.Max(0, State.threat - 8);
                    foreach (var resident in State.residents)
                        if (resident.currentRoom == room.uid && !resident.downed && resident.ageStage == 0 &&
                            resident.currentTask != "idle" && resident.currentTask != "rest")
                            GiveXp(resident, incident.kind == "raiders" ? 20 : 10);
                    Bump("resolved");
                    Emit("resolved", room.uid, 0, incident.kind);
                    continue;
                }
                if (room.type != "heart" && room.type != "gate")
                    room.condition = Mathf.Max(0, room.condition -
                        (incident.kind == "cave_in" ? 0.03f : 0.015f) * incident.severity * dt);
                if (incident.kind == "raiders" && defenders == 0)
                {
                    // Undefended raiders loot, and in the Heart's own chamber they wound it.
                    if (room.type == "heart")
                        State.heartHp = Mathf.Max(0, State.heartHp - 1.4f * incident.severity * dt);
                    // Carry the fraction so the loot rate is the same at any frame rate or game speed.
                    incident.lootCarry += 0.6f * incident.severity * dt;
                    int take = Mathf.Min(State.gold, Mathf.FloorToInt(incident.lootCarry));
                    incident.lootCarry -= take;
                    State.gold -= take; incident.stolen += take;
                    if (MarchRaiders(incident, room, dt)) continue;
                }
                // GDD 9.4: only breaches (raiders in the Heart's chamber, above) and a fire left burning on the Heart's
                // floor hurt the Heart. Pests never do.
                if (incident.kind == "fire" && defenders == 0)
                {
                    var heartRoom = State.rooms.Find(r => r.type == "heart");
                    if (heartRoom != null && heartRoom.floor == room.floor)
                        State.heartHp = Mathf.Max(0, State.heartHp - 0.5f * incident.severity * dt);
                }
                foreach (var resident in State.residents)
                {
                    if (resident.currentRoom != room.uid || resident.ageStage == 1 || resident.downed) continue;
                    if (incident.kind == "illness") resident.illness = Mathf.Min(100, resident.illness + 0.06f * dt);
                    else
                    {
                        resident.hp = Mathf.Max(0, resident.hp -
                            (incident.kind == "raiders" ? 0.12f : 0.035f) * incident.severity * dt /
                            Mathf.Max(1, defenders));
                        resident.injury = Mathf.Min(100, resident.injury + 0.03f * dt);
                        if (resident.hp <= 0) { resident.downed = true; resident.injury = Mathf.Max(50, resident.injury); }
                    }
                }
                incident.spreadSeconds += dt;
                if (incident.spreadSeconds >= 70 && defenders == 0 && State.incidents.Count < 4 &&
                    (incident.kind == "fire" || incident.kind == "pests"))
                {
                    incident.spreadSeconds = 0;
                    var neighbor = State.rooms.Find(r => r.floor == room.floor && r.uid != room.uid &&
                        (r.x + r.width == room.x || room.x + room.width == r.x) &&
                        !State.incidents.Exists(i => i.roomUid == r.uid));
                    if (neighbor != null) StartIncident(incident.kind, neighbor.uid);
                }
            }
        }

        private void TickEvents(float dt)
        {
            if (State.incidents.Count >= 2) return;
            State.eventCooldown -= dt;
            if (State.eventCooldown > 0) return;
            bool guidedIncident = State.tutorialStep == 5;
            State.eventTimer++;
            State.eventCooldown = NextEventDelay();
            var rooms = State.rooms.FindAll(r => r.type != "heart" && r.type != "gate" &&
                !State.incidents.Exists(i => i.roomUid == r.uid));
            if (rooms.Count == 0) return;
            if (!guidedIncident && State.eventTimer > 1 && TryPositiveEvent()) return;
            string kind = ChooseIncident(guidedIncident);
            if (kind == "cave_in")
            {
                var underground = rooms.FindAll(r => r.floor < 0);
                if (underground.Count > 0) rooms = underground; else kind = "fire";
            }
            if (kind == "raiders" && StartRaid() == null) return;
            var room = rooms[Mathf.Min(rooms.Count - 1, (int)(Random01() * rooms.Count))];
            StartIncident(kind, room.uid);
        }
    }
}
