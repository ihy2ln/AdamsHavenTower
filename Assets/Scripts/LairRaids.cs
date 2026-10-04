using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Adventurer parties raid the dungeon (GDD 20.5-20.6). They replace raiders and Gate visitors in Dungeon Mode:
    // parties enter at a Gate, zig-zag down the route toward the Heart and meet every trap, snare, lair and vault once.
    // Kills pay and raise Notoriety; survivors who flee spread Fame. Notoriety mirrors the Tower's threat, so the siege
    // machinery turns into the elite team. All randomness comes from the dungeon's own stream (LairRandom01).
    public sealed partial class TowerRules
    {
        private static readonly string[] Classes = { "Warrior", "Rogue", "Mage", "Cleric", "Ranger" };
        private static readonly float[] ClassPower = { 1.1f, 0.9f, 1.15f, 0.85f, 1.0f };
        private static readonly float[] ClassHp = { 1.3f, 1.0f, 0.8f, 1.0f, 1.0f };
        private static readonly string[] AdventurerElements = { "Fire", "Wind", "Earth", "Lightning", "Water", "Light", "Dark" };
        private static readonly string[] FirstNames = {
            "Aldo", "Brenna", "Cass", "Dorian", "Edda", "Fenn", "Garrick", "Hilde", "Ivo", "Jessa", "Korin", "Lysa",
            "Marek", "Nell", "Orrin", "Pella", "Quill", "Rhea", "Sten", "Tamsin", "Ulric", "Vera", "Wynn", "Yara" };
        private static readonly string[] PartyNames = {
            "Copper Lanterns", "Sixth Bell", "Ashen Road", "Gilded Spur", "Brook Wardens", "Lucky Knot", "Iron Thimble",
            "Silver Eel", "Dawnbreakers", "Mossback Company", "Red Ledger", "Last Candle" };

        public static string NotorietyHint(float notoriety)
        {
            return notoriety >= 75 ? "an elite team is coming for the Heart" : notoriety >= 50 ? "the guilds are watching" :
                notoriety >= 25 ? "word of the dead is spreading" : "few remember the fallen";
        }

        public int MaxParties() { return State.heartRank >= 4 ? 3 : 2; }

        public float PartyInterval()
        {
            return Mathf.Clamp(LairBalance.ArrivalBase * (1.3f - State.lair.fame / 100f) * Storyteller.delay,
                LairBalance.ArrivalMin, LairBalance.ArrivalMax);
        }

        private void AddNotoriety(LairParty party, float amount)
        {
            float before = State.lair.notoriety;
            State.lair.notoriety = Mathf.Clamp(before + amount, 0, 100);
            if (party != null) party.notorietyDelta += State.lair.notoriety - before;
            State.threat = State.lair.notoriety;
        }

        private void AddFame(LairParty party, float amount)
        {
            float before = State.lair.fame;
            State.lair.fame = Mathf.Clamp(before + amount, 0, 100);
            if (party != null) party.fameDelta += State.lair.fame - before;
        }

        // ---- Tick -----------------------------------------------------------------------------------------------

        private void TickLair(float dt, bool live)
        {
            if (!LairFounded) return;
            var lair = State.lair;
            lair.notoriety = Mathf.Max(0, lair.notoriety - LairBalance.NotorietyDecayPerMinute * dt / 60f);
            lair.fame = Mathf.MoveTowards(lair.fame, LairBalance.FameRest, LairBalance.FameDriftPerMinute * dt / 60f);
            State.threat = lair.notoriety;
            TickMonsters(dt);
            TickVaults(dt);
            if (live) lair.offlineArrivals = 0;
            lair.nextPartySeconds -= dt * (live ? 1f : 0.5f);
            if (lair.nextPartySeconds <= 0)
            {
                lair.nextPartySeconds = PartyInterval();
                if ((live || lair.offlineArrivals < LairBalance.OfflineArrivalCap) && LairSpawnParty("normal") == null && !live)
                    lair.offlineArrivals++;
            }
            foreach (var party in lair.parties.ToArray())
            {
                if (!live) { ResolveParty(party, false); continue; }
                party.stepSeconds -= dt;
                for (int guard = 0; guard < 8 && party.stepSeconds <= 0 && lair.parties.Contains(party); guard++)
                    StepParty(party, true);
            }
        }

        private void TickVaults(float dt)
        {
            State.lair.vaults.RemoveAll(v => { var room = Room(v.room); return room == null || room.type != "bait_vault"; });
            foreach (var room in State.rooms)
            {
                if (room.type != "bait_vault") continue;
                var vault = VaultOf(room);
                float cap = LairBalance.VaultPerLevel * room.level;
                if (vault.gold >= cap || State.gold <= LairBalance.VaultReserve) continue;
                vault.carry += LairBalance.VaultRefill * dt;
                int coins = Mathf.Min(Mathf.FloorToInt(vault.carry), Mathf.CeilToInt(cap - vault.gold),
                    State.gold - (int)LairBalance.VaultReserve);
                if (coins <= 0) continue;
                vault.carry -= coins;
                vault.gold = Mathf.Min(cap, vault.gold + coins);
                State.gold -= coins;
            }
        }

        private LairVault VaultOf(TowerRoom room)
        {
            var vault = State.lair.vaults.Find(v => v.room == room.uid);
            if (vault == null) { vault = new LairVault { room = room.uid }; State.lair.vaults.Add(vault); }
            return vault;
        }

        // ---- Arrivals ---------------------------------------------------------------------------------------------

        public string LairSpawnParty(string kind)
        {
            if (!LairFounded || State.introPhase != "complete") return "Found the dungeon first.";
            bool elite = kind == "elite";
            if (!elite && State.lair.parties.Count >= MaxParties()) return "The dungeon is already full of adventurers.";
            var entry = RouteEntry();
            if (RouteDistance(entry.x, entry.y) < 0) return "No way leads from the Gates to the Heart.";
            float fame = State.lair.fame;
            int size = elite ? 5 : Mathf.Min(5, 2 + (fame >= 30 ? 1 : 0) + (fame >= 60 ? 1 : 0) + (LairRandom01() < 0.5f ? 1 : 0));
            int avg = elite ? Mathf.Min(9, State.heartRank + 2) :
                Mathf.Clamp(Mathf.RoundToInt(0.7f * State.heartRank + fame / 30f + (LairRandom01() * 2f - 1f)), 1, 9);
            var party = new LairParty { id = State.lair.nextPartyId++, kind = elite ? "elite" : "normal",
                floor = entry.x, x = entry.y, fromFloor = entry.x, fromX = entry.y, stepSeconds = LairBalance.CellSeconds,
                stepTotal = LairBalance.CellSeconds, moveSeconds = LairBalance.CellSeconds };
            for (int i = 0; i < size; i++)
            {
                int rank = elite ? avg : Mathf.Clamp(avg + Mathf.RoundToInt(LairRandom01() * 2f - 1f), 1, 9);
                party.members.Add(NewAdventurer(rank, Classes[LairPick(Classes.Length)], elite ? 1.5f : 1f));
            }
            if (elite) MakeEliteLeader(party.members[0], avg);
            party.name = elite ? party.members[0].name + "'s elite team" : "The " + PartyNames[LairPick(PartyNames.Length)];
            State.lair.parties.Add(party);
            party.lines.Add(party.name + " (" + size + ", average rank " + TowerTiers.Tier(avg) + ") entered at the " +
                (entry.y < CoreX ? "west" : "east") + " Gate.");
            Note(party.name + " entered the dungeon: " + size + " adventurers, rank " + TowerTiers.Tier(avg) + ".");
            Emit("lair_party", 0, 0, party.name);
            return null;
        }

        private LairAdventurer NewAdventurer(int rank, string cls, float hpScale)
        {
            int c = System.Array.IndexOf(Classes, cls);
            int level = NaturalLevel(rank);
            float power = (21 + 4 * rank) * (1 + 0.08f * rank) * (0.5f + 0.5f * level / (float)LevelCap) *
                ClassPower[c] * (0.85f + 0.3f * LairRandom01());
            float maxHp = (40 + 14 * rank) * ClassHp[c] * hpScale;
            return new LairAdventurer { name = FirstNames[LairPick(FirstNames.Length)], cls = cls, rank = rank, level = level,
                element = AdventurerElements[LairPick(AdventurerElements.Length)], power = power, hp = maxHp, maxHp = maxHp };
        }

        // An elite team is led by a named roster hero (the adventurer-named hook, GDD 20.8).
        private void MakeEliteLeader(LairAdventurer leader, int rank)
        {
            var pool = TowerRoster.OfRank(Mathf.Clamp(rank, 1, 9), true);
            if (pool.Count == 0) return;
            var unit = pool[LairPick(pool.Count)];
            leader.name = unit.name;
            leader.heroId = unit.id;
            leader.named = true;
            if (!string.IsNullOrEmpty(unit.element)) leader.element = unit.element;
            var record = State.lair.named.Find(n => n.unitId == unit.id);
            if (record == null) { record = new LairNamed { unitId = unit.id, name = unit.name }; State.lair.named.Add(record); }
            record.rank = Mathf.Max(record.rank, rank);
            record.visits++;
        }

        // The siege warning ran out: the elite team walks in at the Gates.
        public void LairEliteArrives()
        {
            State.siegeCooldown = 3.5f * DaySeconds * Storyteller.delay;
            if (LairSpawnParty("elite") == null)
                Note("The elite team is inside the dungeon. Every trap and monster stands between them and the Heart.");
            Emit("siege_warning", 0, 0, "elite");
        }

        // The heroes beat the elite team in Battle Mode before it reached the Gates.
        private void LairEliteRepelled()
        {
            AddNotoriety(null, -30);
            AddFame(null, 5);
        }

        // ---- Walking --------------------------------------------------------------------------------------------

        public int Alive(LairParty party) { int n = 0; foreach (var m in party.members) if (m.Alive) n++; return n; }

        private float PartyPower(LairParty party)
        {
            float power = 0; bool cleric = false;
            foreach (var m in party.members)
            {
                if (!m.Alive) continue;
                power += m.power * (0.5f + 0.5f * Mathf.Clamp01(m.hp / m.maxHp));
                cleric |= m.cls == "Cleric";
            }
            return power * (cleric ? LairBalance.ClericPower : 1f);
        }

        private float PartyHpShare(LairParty party)
        {
            float hp = 0, max = 0;
            foreach (var m in party.members) if (m.Alive) { hp += m.hp; max += m.maxHp; }
            return max <= 0 ? 0 : hp / max;
        }

        private int Rogues(LairParty party) { int n = 0; foreach (var m in party.members) if (m.Alive && m.cls == "Rogue") n++; return n; }

        // Offline: the whole raid at once, same rules, no timing.
        private void ResolveParty(LairParty party, bool live)
        {
            for (int guard = 0; guard < 600 && State.lair.parties.Contains(party); guard++) StepParty(party, live);
            if (State.lair.parties.Contains(party)) FinishParty(party, "fled", live);
        }

        private void StepParty(LairParty party, bool live)
        {
            var next = RouteNext(party.floor, party.x);
            if (next.x == party.floor && next.y == party.x)
            {
                FinishParty(party, RouteDistance(party.floor, party.x) == 0 ? "breached" : "fled", live);
                return;
            }
            bool vertical = next.x != party.floor;
            party.fromFloor = party.floor; party.fromX = party.x;
            party.floor = next.x; party.x = next.y;
            float move = vertical ? LairBalance.StairSeconds : LairBalance.CellSeconds, total = move;
            var room = RoomAt(party.floor, party.x);
            bool atHeart = room != null && room.type == "heart";
            if (room != null && !party.visited.Contains(room.uid))
            {
                party.visited.Add(room.uid);
                total += Encounter(party, room, live);
                ClericMends(party);
            }
            party.moveSeconds = move; party.stepTotal = total; party.stepSeconds += total;
            if (!State.lair.parties.Contains(party)) return;
            if (Alive(party) == 0) FinishParty(party, "wiped", live);
            else if (atHeart) FinishParty(party, "breached", live);
            else if (Alive(party) * 2 <= party.members.Count ||
                PartyHpShare(party) < (party.sated ? LairBalance.SatedFleeHp : LairBalance.FleeHp))
                FinishParty(party, "fled", live);
        }

        private void ClericMends(LairParty party)
        {
            if (!party.members.Exists(m => m.Alive && m.cls == "Cleric")) return;
            foreach (var m in party.members)
                if (m.Alive) m.hp = Mathf.Min(m.maxHp, m.hp + m.maxHp * LairBalance.ClericHeal);
        }

        // ---- Encounters (seconds the party lingers) ---------------------------------------------------------------

        private float Encounter(LairParty party, TowerRoom room, bool live)
        {
            string where = FloorLabel(room.floor) + " " + TowerCatalog.Get(room.type).displayName;
            switch (room.type)
            {
                case "spike_hall": SpikeHall(party, room, where); break;
                case "snare_pit": SnarePit(party, room, where); break;
                case "monster_lair":
                    MonsterFight(party, MonstersIn(room.uid), 1f + 0.08f * (room.level - 1), where); break;
                case "bait_vault": BaitVault(party, room, where); return LairBalance.EncounterSeconds + LairBalance.VaultSeconds;
                case "heart": HeartBreach(party, room, live); break;
                default: ResidentDefence(party, room, where); break;
            }
            return LairBalance.EncounterSeconds;
        }

        private void SpikeHall(LairParty party, TowerRoom room, string where)
        {
            if (room.condition < LairBalance.TrapDisabledBelow)
            { party.lines.Add(where + ": the broken spikes did nothing. Repair the trap."); return; }
            int lvl = room.level, rogues = Rogues(party);
            float chance = Mathf.Clamp(LairBalance.SpikeChance + LairBalance.SpikeChancePerLevel * (lvl - 1) -
                LairBalance.RogueTrapPenalty * rogues, 0.2f, 0.95f);
            if (LairRandom01() >= chance)
            {
                if (rogues > 0 && LairRandom01() < LairBalance.DisarmChance)
                {
                    room.condition = Mathf.Max(0, room.condition - LairBalance.DisarmWear);
                    party.lines.Add(where + ": a rogue spotted and jammed the spikes.");
                }
                else party.lines.Add(where + ": they slipped past the spikes.");
                return;
            }
            room.condition = Mathf.Max(0, room.condition - LairBalance.SpikeWear);
            int hits = Mathf.Min(Alive(party), 1 + lvl / 3);
            float damage = LairBalance.SpikeDamage * (1 + LairBalance.SpikeDamagePerLevel * (lvl - 1)) * room.width;
            var names = new List<string>();
            for (int i = 0; i < hits; i++)
            {
                var alive = party.members.FindAll(m => m.Alive);
                if (alive.Count == 0) break;
                var target = alive[LairPick(alive.Count)];
                names.Add(target.name);
                Hurt(party, target, damage, null);
            }
            party.lines.Add(where + ": spikes hit " + string.Join(", ", names.ToArray()) + " for " + Mathf.RoundToInt(damage) + ".");
        }

        private void SnarePit(LairParty party, TowerRoom room, string where)
        {
            if (room.condition < LairBalance.TrapDisabledBelow) { party.lines.Add(where + ": the broken snare caught nobody."); return; }
            int alive = Alive(party);
            float chance = LairBalance.SnareChance + LairBalance.SnareChancePerLevel * (room.level - 1) -
                LairBalance.RogueTrapPenalty * Rogues(party);
            if (alive <= 1 || LairRandom01() >= chance) { party.lines.Add(where + ": nobody fell in."); return; }
            var list = party.members.FindAll(m => m.Alive);
            var caught = list[LairPick(list.Count)];
            caught.status = "captured";
            party.captured++;
            State.lair.captures++;
            int ransom = Mathf.RoundToInt(LairBalance.RansomPerRank * caught.rank * (1 + LairBalance.RansomPerLevel * (room.level - 1)));
            party.ransom += ransom;
            AddNotoriety(party, 1);
            AddFame(party, 0.5f);
            party.lines.Add(where + ": " + caught.name + " was caught (ransom " + ransom + " gold).");
        }

        private void BaitVault(LairParty party, TowerRoom room, string where)
        {
            var vault = VaultOf(room);
            int alive = Alive(party);
            int take = Mathf.Min(Mathf.FloorToInt(vault.gold), 25 * alive * Mathf.Max(1, AverageRank(party)));
            if (take <= 0) { party.lines.Add(where + ": the vault was empty."); return; }
            vault.gold -= take;
            party.loot += take;
            if (take >= 10 * alive) party.sated = true;
            party.lines.Add(where + ": they pocketed " + take + " gold" + (party.sated ? " and grew careless." : "."));
        }

        public static int AverageRank(LairParty party)
        {
            if (party.members.Count == 0) return 1;
            float total = 0;
            foreach (var m in party.members) total += m.rank;
            return Mathf.RoundToInt(total / party.members.Count);
        }

        // Monsters against the party: power ratio through AutoTier, element counters worth 10% per monster.
        private void MonsterFight(LairParty party, List<LairMonster> monsters, float scale, string where)
        {
            var fighters = monsters.FindAll(MonsterReady);
            if (fighters.Count == 0) { party.lines.Add(where + ": no monster was fit to fight."); return; }
            float power = 0; int counters = 0;
            foreach (var m in fighters)
            {
                power += MonsterFightPower(m);
                if (party.members.Exists(a => a.Alive && ElementBeats(m.element, a.element))) counters++;
            }
            power *= scale * (1 + 0.10f * counters);
            int tier = AutoTier(power / Mathf.Max(1f, PartyPower(party)));
            int deadBefore = party.members.FindAll(m => m.status == "dead").Count;
            foreach (var a in party.members.FindAll(m => m.Alive))
                Hurt(party, a, LairBalance.PartyLoss[tier] * a.maxHp * (0.8f + 0.4f * LairRandom01()), fighters);
            foreach (var m in fighters)
            {
                WoundMonster(m, LairBalance.DefenderLoss[tier]);
                if (tier >= 2) MonsterWon(m);
            }
            int killed = party.members.FindAll(m => m.status == "dead").Count - deadBefore;
            party.lines.Add(where + ": " + fighters.Count + " monster" + (fighters.Count == 1 ? "" : "s") + " fought (" +
                AutoTiers[tier].ToLowerInvariant() + " for the dungeon)" + (killed > 0 ? ", " + killed + " fell." : "."));
        }

        // Living, Heart and Gate floors: residents on defence duty on that floor stand in the way. Downed, never killed.
        private void ResidentDefence(LairParty party, TowerRoom room, string where)
        {
            var defenders = State.residents.FindAll(r => r.ageStage == 0 && !r.downed && !r.away && !r.exploring &&
                r.origin != "body" && r.priorityDefense > 0 && Room(r.currentRoom) != null && Room(r.currentRoom).floor == room.floor);
            if (defenders.Count == 0)
            {
                int take = Mathf.Min(State.gold, 10 * Alive(party));
                State.gold -= take; party.loot += take; party.goldLost += take;
                party.lines.Add(where + ": nobody defended it; they ransacked " + take + " gold.");
                return;
            }
            float power = 0;
            foreach (var r in defenders) power += HeroPower(r) * (r.origin == "hero" ? 1f : 0.6f) * (1 + 0.1f * r.weapon);
            int tier = AutoTier(power / Mathf.Max(1f, PartyPower(party)));
            foreach (var a in party.members.FindAll(m => m.Alive))
                Hurt(party, a, LairBalance.PartyLoss[tier] * a.maxHp * (0.8f + 0.4f * LairRandom01()), null);
            int downed = 0;
            foreach (var r in defenders)
            {
                r.hp = Mathf.Max(0, r.hp - LairBalance.DefenderLoss[tier] * MaxHp(r));
                if (r.hp <= 0) { r.downed = true; r.injury = Mathf.Max(50, r.injury); downed++; }
                else GiveXp(r, 10);
            }
            party.lines.Add(where + ": " + defenders.Count + " resident" + (defenders.Count == 1 ? "" : "s") + " held the floor (" +
                AutoTiers[tier].ToLowerInvariant() + ")" + (downed > 0 ? ", " + downed + " downed." : "."));
        }

        private void HeartBreach(LairParty party, TowerRoom room, bool live)
        {
            var guards = HeartGuards();
            if (guards.Exists(MonsterReady))
                MonsterFight(party, guards, LairBalance.GuardPower, "HEART guards");
            int alive = Alive(party);
            if (alive == 0) return;
            float rankSum = 0;
            foreach (var m in party.members) if (m.Alive) rankSum += m.rank;
            float damage = LairBalance.BreachPerRank * rankSum * (party.kind == "elite" ? LairBalance.EliteBreach : 1f);
            float floorHp = live ? 0 : Mathf.Min(State.heartHp, LairBalance.OfflineHeartFloor * HeartMaxHp(State.heartRank));
            float before = State.heartHp;
            State.heartHp = Mathf.Max(floorHp, State.heartHp - damage);
            party.heartDamage += before - State.heartHp;
            int gold = Mathf.Min(State.gold, 40 * alive * Mathf.Max(1, AverageRank(party)));
            int celestium = Mathf.Min(State.celestium, alive);
            State.gold -= gold; State.celestium -= celestium;
            party.loot += gold; party.goldLost += gold;
            party.lines.Add("HEART: breached for " + Mathf.RoundToInt(before - State.heartHp) + " Heart HP; they took " + gold +
                " gold and " + celestium + " Celestium.");
            Emit("heart_hit", room.uid, 0, party.name);
        }

        // Damage to an adventurer; at 0 they die, and the dungeon collects (GDD 20.6).
        private void Hurt(LairParty party, LairAdventurer a, float damage, List<LairMonster> credit)
        {
            if (!a.Alive) return;
            a.hp -= damage;
            if (a.hp > 0) return;
            a.hp = 0;
            int aliveBefore = Alive(party);
            a.status = "dead";
            party.kills++;
            State.lair.kills++;
            int share = aliveBefore <= 0 ? party.loot : party.loot / aliveBefore;
            party.loot -= share;
            int gold = 15 * a.rank + 2 * a.level + share;
            State.gold += gold;
            State.essence += a.rank;
            if (a.rank >= 3) State.ore++;
            State.lair.sigilProgress += 0.05f * a.rank;
            if (State.lair.sigilProgress >= 1)
            {
                int whole = Mathf.FloorToInt(State.lair.sigilProgress);
                State.lair.sigilProgress -= whole;
                GrantSigils(whole);
            }
            AddNotoriety(party, 5 + a.rank + (party.kind == "elite" ? 8 : 0));
            if (credit != null && credit.Count > 0) credit[LairPick(credit.Count)].kills++;
            party.lines.Add(a.name + " the " + a.cls + " died (+" + gold + " gold, +" + a.rank + " Essence).");
            Bump("lair_kill");
        }

        // ---- The end of a raid ------------------------------------------------------------------------------------

        private void FinishParty(LairParty party, string outcome, bool live)
        {
            if (!State.lair.parties.Remove(party)) return;
            int alive = Alive(party), rank = Mathf.Max(1, AverageRank(party));
            int gained = 0;
            switch (outcome)
            {
                case "wiped":
                    gained += party.loot;
                    State.gold += party.loot;
                    party.loot = 0;
                    AddFame(party, -3);
                    State.lair.wipes++;
                    party.lines.Add("Nobody walked out. Their loot stayed in the dungeon.");
                    break;
                case "breached":
                    AddNotoriety(party, -5);
                    AddFame(party, 6);
                    State.lair.breaches++;
                    party.lines.Add("They reached the Heart and walked out heroes.");
                    break;
                default:
                    int income = 5 * alive * rank;   // the town profits from returning adventurers (placeholder)
                    State.gold += income; gained += income;
                    AddNotoriety(party, -2);
                    AddFame(party, Mathf.Min(8f, 3f + party.loot / 100f));
                    State.lair.escapes++;
                    party.lines.Add(alive + " fled" + (party.loot > 0 ? " with " + party.loot + " gold" : "") +
                        ". They spent " + income + " gold in town.");
                    outcome = "fled";
                    break;
            }
            if (party.ransom > 0)
            {
                State.gold += party.ransom; gained += party.ransom;
                party.lines.Add("Ransom paid for " + party.captured + " captive" + (party.captured == 1 ? "" : "s") + ": " + party.ransom + " gold.");
            }
            foreach (var m in party.members)
                if (m.named)
                {
                    var record = State.lair.named.Find(n => n.unitId == m.heroId);
                    if (record != null) { record.lastOutcome = m.Alive ? outcome : m.status; record.grudge += m.Alive ? 1 : 3; }
                }
            var report = new LairReport { day = State.day, clock = State.clock, partyName = party.name, kind = party.kind,
                outcome = outcome, size = party.members.Count, avgRank = rank, kills = party.kills, captured = party.captured,
                goldGained = gained, goldLost = party.goldLost, heartDamage = party.heartDamage,
                notorietyDelta = party.notorietyDelta, fameDelta = party.fameDelta, lines = party.lines };
            State.lair.reports.Insert(0, report);
            if (State.lair.reports.Count > LairBalance.Reports) State.lair.reports.RemoveAt(State.lair.reports.Count - 1);
            Note(party.name + " " + (outcome == "wiped" ? "was wiped out" : outcome == "breached" ? "breached the Heart" : "fled") +
                ": " + party.kills + " killed, " + party.captured + " captured" +
                (party.heartDamage > 0 ? ", Heart -" + Mathf.RoundToInt(party.heartDamage) : "") + ".");
            Emit("lair_raid", 0, 0, outcome);
        }

        // ---- HUD lines ----------------------------------------------------------------------------------------------

        public string LairTopLine()
        {
            var lair = State.lair;
            string parties = lair.parties.Count == 0 ? "NO PARTY" :
                lair.parties.Count + (lair.parties.Count == 1 ? " PARTY IN " : " PARTIES, FIRST IN ") + FloorLabel(lair.parties[0].floor);
            return "NOTORIETY " + ThreatLabel().ToUpperInvariant() + "  ·  FAME " + Mathf.RoundToInt(lair.fame) +
                "  ·  HEART " + Mathf.CeilToInt(State.heartHp) + "  ·  " + parties;
        }

        public string LairRoomAdvice(TowerRoom room)
        {
            int lvl = room.level;
            switch (room.type)
            {
                case "spike_hall":
                    return "LETHAL TRAP  •  " + Mathf.RoundToInt(100 * Mathf.Clamp(LairBalance.SpikeChance + LairBalance.SpikeChancePerLevel * (lvl - 1), 0.2f, 0.95f)) +
                        "% to spring, hits " + (1 + lvl / 3) + " for " +
                        Mathf.RoundToInt(LairBalance.SpikeDamage * (1 + LairBalance.SpikeDamagePerLevel * (lvl - 1)) * room.width) +
                        "\nEach trigger wears it 6%; below 20% it stops working. Rogues dodge and jam it.";
                case "snare_pit":
                    return "SNARE  •  " + Mathf.RoundToInt(100 * (LairBalance.SnareChance + LairBalance.SnareChancePerLevel * (lvl - 1))) +
                        "% to catch one adventurer (never the last)\nCaptives are ransomed for gold when the raid ends. No killing, little notoriety.";
                case "monster_lair":
                    return "MONSTERS " + MonstersIn(room.uid).Count + "/" + LairCapacity(room) + "  •  open DUNGEON > MONSTERS to move them here" +
                        "\nThe lair fights each party once. Element counters add 10% per monster.";
                case "bait_vault":
                    return "LURE  •  holds " + Mathf.FloorToInt(VaultOf(room).gold) + "/" + LairBalance.VaultPerLevel * lvl + " gold" +
                        "\nRefills from the treasury. A party that grabs enough grows careless and flees early, spreading Fame.";
            }
            return "";
        }

        // ---- Dev --------------------------------------------------------------------------------------------------

        public string DevLairParty() { return LairSpawnParty("normal"); }
        public string DevLairElite() { if (!LairFounded) return "Found the dungeon first."; LairEliteArrives(); return null; }

        public string DevLairNotoriety(float amount)
        {
            if (!LairFounded) return "Found the dungeon first.";
            AddNotoriety(null, amount);
            State.siegeCooldown = 0;
            State.eventCooldown = Mathf.Min(State.eventCooldown, 3f);
            return null;
        }

        public string DevLairMonsters(int count)
        {
            if (!LairFounded) return "Found the dungeon first.";
            for (int i = 0; i < count && State.lair.monsters.Count < MonsterCap(); i++)
                NewMonster(Mathf.Clamp(State.heartRank + (int)(LairRandom01() * 3), 1, 9));
            return null;
        }
    }
}
