using System;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Developer cheats behind the DEV panel (TowerHudDev.cs): grants and shortcuts for testing progression.
    // Like every other action they return null on success (after a log line) or the reason they could not run.
    public sealed partial class TowerRules
    {
        public string DevGrant(string what, int amount)
        {
            switch (what)
            {
                case "gold": State.gold += amount; break;
                case "celestium": State.celestium += amount; break;
                case "sigils": State.sigils += amount; break;
                case "tonics": State.tonics += amount; break;
                case "materials":
                    State.wood += amount; State.stone += amount; State.ore += amount; State.essence += amount; break;
                case "supplies":
                    State.food += amount; State.water += amount; State.firewood += amount; break;
                default: return "Unknown grant: " + what + ".";
            }
            Note("DEV: +" + Compact(amount) + " " + what + ".");
            return null;
        }

        // A hero by unit id: a new one moves into a free bed (or waits in the Heart); one already owned levels up.
        public string DevAddHero(string unitId)
        {
            if (State.introPhase != "complete") return "Found the Tower first (or use SKIP INTRO).";
            var owned = Owned(r => r.origin == "hero" && r.unitId == unitId);
            if (owned != null) { owned.level++; Note("DEV: " + owned.name + " rose to level " + owned.level + "."); return null; }
            var unit = TowerRoster.Unit(unitId);
            int pick = Array.IndexOf(SummonHeroIds, unitId);
            string name = unit != null ? unit.name : pick >= 0 ? SummonHeroNames[pick] : unitId;
            var hero = NewResident(unitId, name, "hero", 1);
            hero.rank = unit != null && unit.rank > 0 ? unit.rank : 5;
            if (unit != null) TowerRoster.ApplyStats(hero, unit);
            else RaiseStats(hero, hero.rank - 1);
            bool housed = Admit(hero);
            Note("DEV: " + name + " joined the Tower" + (housed ? "." : " and waits in the Heart for a bed."));
            return null;
        }

        // The next roster hero not owned yet, in roster order.
        public string DevAddRosterHero()
        {
            foreach (var u in TowerRoster.All)
                if (u.IsHero && Owned(r => r.unitId == u.id) == null) return DevAddHero(u.id);
            return "Every roster hero is already in the Tower.";
        }

        // A named roster resident not owned yet (a generic villager once every one has arrived), or a Celestium body.
        public string DevAddResident(string bodyVariant = null)
        {
            if (State.introPhase != "complete") return "Found the Tower first (or use SKIP INTRO).";
            TowerResident person;
            if (!string.IsNullOrEmpty(bodyVariant))
            {
                person = NewResident("", "Celestium " + bodyVariant + " " + State.nextResidentId, "body", 1);
                person.chassisVariant = bodyVariant;
            }
            else
            {
                RosterUnit fresh = null;
                foreach (var u in TowerRoster.All)
                    if (!u.IsHero && Owned(r => r.unitId == u.id) == null) { fresh = u; break; }
                person = fresh != null ? NewResident(fresh.id, fresh.name, "villager", 1) :
                    NewResident("", "Villager " + State.nextResidentId, "villager", 1);
                if (fresh != null) { person.rank = fresh.rank; TowerRoster.ApplyStats(person, fresh); }
            }
            bool housed = Admit(person);
            Note("DEV: " + person.name + " joined the Tower" + (housed ? "." : " and waits in the Heart for a bed."));
            return null;
        }

        public string DevFinishConstruction()
        {
            int count = State.works.Count;
            if (count == 0) return "Nothing is under construction.";
            foreach (var work in State.works.ToArray()) { State.works.Remove(work); Complete(work); }
            Note("DEV: finished " + count + " construction job" + (count == 1 ? "." : "s."));
            return null;
        }

        public string DevFinishResearch()
        {
            if (!Researching) return "Nothing is being researched.";
            string id = State.researching;
            CompleteResearch(id);
            State.researching = "";
            State.researchEndsUnix = 0;
            Note("DEV: research " + id + " finished.");
            return null;
        }

        public string DevResearchAll()
        {
            int before = State.research.Count;
            GrantResearchUpToTier(9);
            Note("DEV: " + (State.research.Count - before) + " research nodes granted.");
            return null;
        }

        public string DevUnlockBlueprints()
        {
            int added = 0;
            foreach (var def in TowerCatalog.All)
                if (def.kind != "heart" && def.kind != "gate" && !State.blueprints.Contains(def.id))
                { State.blueprints.Add(def.id); added++; }
            Note("DEV: unlocked " + added + " blueprints.");
            return null;
        }

        // Every producer becomes ready to collect.
        public string DevReadyRooms()
        {
            int count = 0;
            foreach (var room in State.rooms)
            {
                var def = TowerCatalog.Get(room.type);
                if (def == null || string.IsNullOrEmpty(Product(room)) || room.ready) continue;
                room.progress = 1; room.ready = true; count++;
            }
            Note("DEV: " + count + " rooms ready to collect.");
            return null;
        }

        public string DevHeartRankUp()
        {
            if (State.introPhase != "complete") return "Found the Tower first (or use SKIP INTRO).";
            if (State.heartRank >= TowerTiers.MaxRank) return "The Heart is already rank SSR.";
            State.celestium += HeartUpgradeCelestium();
            State.gold += HeartUpgradeGold();
            return UpgradeHeart();
        }

        public string DevRoomsRankUp()
        {
            int count = 0;
            foreach (var room in State.rooms)
            {
                if (room.type == "heart" || room.level >= Math.Min(MaxLevel(room), RankCap())) continue;
                room.level++; count++;
            }
            if (count == 0) return "Every room is at the Heart's rank cap. Raise the Heart first.";
            Note("DEV: " + count + " rooms ranked up.");
            return null;
        }

        // Residents healthy, fed and rested; incidents gone; the Heart at full HP.
        public string DevHealAll()
        {
            foreach (var r in State.residents)
            {
                r.hp = MaxHp(r); r.downed = false; r.injury = 0; r.illness = 0;
                r.hunger = r.thirst = r.rest = 100;
                if (r.origin == "body") r.charge = 2160;
            }
            foreach (var room in State.rooms) room.condition = 100;
            int incidents = State.incidents.Count;
            State.incidents.Clear();
            State.heartHp = HeartMaxHp(State.heartRank);
            Note("DEV: everyone healed, " + incidents + " incident" + (incidents == 1 ? "" : "s") + " cleared.");
            return null;
        }

        public string DevLevelUpAll(int levels)
        {
            foreach (var r in State.residents) r.level += levels;
            Note("DEV: every resident gained " + levels + " level" + (levels == 1 ? "." : "s."));
            return null;
        }

        // Runs the simulation forward (production, construction, needs, research by wall clock excluded).
        public string DevSkipTime(float seconds)
        {
            if (State.introPhase != "complete") return "Found the Tower first (or use SKIP INTRO).";
            float step = 1f;
            for (float t = 0; t < seconds; t += step) Advance(Math.Min(step, seconds - t), false);
            Note("DEV: skipped " + Clock(seconds) + ".");
            return null;
        }

        // Runs the founding steps for the player: wakes the Heart, places the Gate and Shack, picks Kaela.
        public string DevSkipIntro()
        {
            if (State.introPhase == "complete") return "The Tower is already founded.";
            if (State.introPhase == "dormant") AwakenHeart();
            if (State.introPhase == "gate") PlaceIntroGate();
            if (State.introPhase == "shack")
            {
                State.gold += BuildCost("house"); State.wood += BuildWoodCost("house"); State.stone += BuildStoneCost("house");
                string error = Build("house", 0, CoreX - 1);   // the Shack's tutorial lot
                if (error != null) return error;
            }
            if (State.introPhase != "choose") return "Could not skip the intro from phase " + State.introPhase + ".";
            return ChooseStarter("kaela");
        }

        public string DevTutorialStep(int delta)
        {
            if (State.introPhase != "complete") return "The guided lessons start after the Tower is founded.";
            State.tutorialStep = Mathf.Clamp(State.tutorialStep + delta, 0, 7);
            Note("DEV: tutorial lesson " + State.tutorialStep + (State.tutorialStep >= 7 ? " (finished)." : "."));
            return null;
        }
    }
}
