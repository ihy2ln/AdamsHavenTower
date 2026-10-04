using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // One monster revealed by the Monster banner.
    public sealed class LairSummon
    {
        public string name, element;
        public int rank, level;
        public bool fused, released;
    }

    // Dungeon monsters (GDD 20.3). They are not residents: a list of their own keeps every colony system (needs, bonds,
    // errands, the cutaway's villagers) untouched. Forms come from the Battle bestiary; power sits on the HeroPower scale,
    // so monsters, heroes and adventurers compare directly.
    public sealed partial class TowerRules
    {
        public List<LairSummon> LastMonsters { get; private set; }

        public int MonsterCap() { return LairBalance.MonsterCaps[Mathf.Clamp(State.heartRank, 1, 9) - 1]; }

        private float LairRandom01()
        {
            int x = State.lair.random;
            if (x == 0) x = 51203 + State.slot * 173;
            x ^= x << 13; x ^= (int)((uint)x >> 17); x ^= x << 5;
            State.lair.random = x;
            return ((uint)x & 0x00ffffff) / 16777216f;
        }

        private int LairPick(int count) { return Mathf.Min(count - 1, (int)(LairRandom01() * count)); }

        public static int NaturalLevel(int rank) { return 1 + 5 * (Mathf.Clamp(rank, 1, 9) - 1); }

        // ---- Monster banner ------------------------------------------------------------------------------------

        public string SummonMonsters(int count)
        {
            if (!IsLair) return "Monsters answer only a dungeon Heart.";
            if (State.introPhase != "complete") return "Found the dungeon first.";
            if (count != 1 && count != 10) return "Summon one or ten.";
            int cost = count == 10 ? LairBalance.MonsterTenCost : LairBalance.MonsterPullCost;
            if (State.sigils < cost) return "The Monster banner needs " + cost + " Sigils.";
            if (State.lair.monsters.Count >= MonsterCap())
                return "The dungeon holds " + MonsterCap() + " monsters at Heart rank " + TowerTiers.Tier(State.heartRank) +
                    ". Release one or raise the Heart.";
            State.sigils -= cost;
            var ranks = new int[count];
            int best = 0;
            for (int i = 0; i < count; i++) { ranks[i] = RollMonsterRank(); best = Mathf.Max(best, ranks[i]); }
            if (count == 10 && best < TenPullFloor) ranks[count - 1] = TenPullFloor;
            LastMonsters = new List<LairSummon>();
            foreach (int rank in ranks) LastMonsters.Add(SummonMonster(rank));
            var top = LastMonsters[0];
            foreach (var pull in LastMonsters) if (pull.rank > top.rank) top = pull;
            Note((count == 10 ? "Ten monsters" : "A monster") + " answered the Heart; best: " + top.name + " (" + TowerTiers.Tier(top.rank) + ").");
            Bump("summon");
            Emit("summon", Room0("heart"), 0, TowerTiers.Tier(top.rank));
            return null;
        }

        // The Resident ladder (SSR 1%, the rest at Standard weights) with its own A-or-better guarantee.
        private int RollMonsterRank()
        {
            var rates = BannerRates(Banner("resident"));
            int rank = 1;
            if (State.lair.monsterPity + 1 >= LairBalance.MonsterPityAt)
            {
                float total = 0;
                for (int i = LairBalance.MonsterFloorRank - 1; i < 9; i++) total += rates[i];
                float roll = LairRandom01() * total;
                rank = 9;
                for (int i = LairBalance.MonsterFloorRank - 1; i < 9; i++) { roll -= rates[i]; if (roll < 0) { rank = i + 1; break; } }
            }
            else
            {
                float roll = LairRandom01() * 100f;
                rank = 9;
                for (int i = 0; i < 9; i++) { roll -= rates[i]; if (roll < 0) { rank = i + 1; break; } }
            }
            State.lair.monsterPity = rank >= LairBalance.MonsterFloorRank ? 0 : State.lair.monsterPity + 1;
            return rank;
        }

        private LairSummon SummonMonster(int rank)
        {
            BestiaryForm form = RollForm(rank);
            string formId = form == null ? "" : form.id;
            var owned = formId.Length == 0 ? null : State.lair.monsters.Find(m => m.formId == formId);
            if (owned != null)
            {
                owned.level = Mathf.Min(LairBalance.MonsterLevelCap, owned.level + LairBalance.DuplicateLevels);
                return new LairSummon { name = owned.name, element = owned.element, rank = owned.rank, level = owned.level, fused = true };
            }
            if (State.lair.monsters.Count >= MonsterCap())
            {
                int essence = 2 * rank;
                State.essence += essence;
                return new LairSummon { name = form == null ? "Wild beast" : form.name, rank = rank, level = NaturalLevel(rank), released = true };
            }
            var monster = NewMonster(rank, form);
            return new LairSummon { name = monster.name, element = monster.element, rank = rank, level = monster.level };
        }

        // A random bestiary family that lives at this rank, in its form for that rank.
        private BestiaryForm RollForm(int rank)
        {
            if (!BattleBestiary.Available) return null;
            var families = new List<BestiaryFamily>();
            foreach (var family in BattleBestiary.Families)
                if (family.minRank <= rank && rank <= family.maxRank) families.Add(family);
            if (families.Count == 0) return null;
            return BattleBestiary.FormFor(families[LairPick(families.Count)], rank);
        }

        private LairMonster NewMonster(int rank, BestiaryForm form = null)
        {
            if (form == null) form = RollForm(rank);
            var monster = new LairMonster { id = State.lair.nextMonsterId++, rank = rank, level = NaturalLevel(rank) };
            if (form != null)
            {
                monster.formId = form.id;
                monster.name = form.name;
                monster.element = form.element == "Neutral" ? "" : form.element ?? "";
                monster.family = form.Family == null ? "" : form.Family.id;
            }
            else monster.name = "Dungeon beast";
            State.lair.monsters.Add(monster);
            return monster;
        }

        // ---- Power ---------------------------------------------------------------------------------------------

        private static readonly Dictionary<string, float> specimenScores = new Dictionary<string, float>();
        private static readonly float[] rankMeans = new float[10];

        private static float SpecimenScore(BestiaryForm form, int rank)
        {
            string key = form.id + "@" + rank;
            float score;
            if (specimenScores.TryGetValue(key, out score)) return score;
            var unit = BattleCatalog.Specimen(form, rank);
            score = unit == null ? 1f : unit.MaxHp / 120f + (unit.Attack + unit.Magic) / 66f +
                (unit.Defense + unit.Resistance) / 18f + unit.Speed / 10f;
            specimenScores[key] = score;
            return score;
        }

        private static float RankMean(int rank)
        {
            if (rankMeans[rank] > 0) return rankMeans[rank];
            float total = 0; int count = 0;
            foreach (var family in BattleBestiary.Families)
                foreach (var form in family.forms)
                    if (form.minRank <= rank && rank <= form.maxRank) { total += SpecimenScore(form, rank); count++; }
            rankMeans[rank] = count == 0 ? 1f : total / count;
            return rankMeans[rank];
        }

        // How the form compares with the other monsters of its rank (role and build), 0.8 to 1.25.
        public static float SpecimenFactor(string formId, int rank)
        {
            rank = Mathf.Clamp(rank, 1, 9);
            var form = BattleBestiary.Form(formId);
            if (form == null) return 1f;
            float mean = RankMean(rank);
            return mean <= 0 ? 1f : Mathf.Clamp(SpecimenScore(form, rank) / mean, 0.8f, 1.25f);
        }

        public static float MonsterPower(LairMonster m)
        {
            if (m == null) return 0;
            int rank = Mathf.Clamp(m.rank, 1, 9);
            return (24 + 4 * rank) * (1 + 0.08f * rank) * (0.5f + 0.5f * Mathf.Clamp(m.level, 1, LairBalance.MonsterLevelCap) /
                (float)LairBalance.MonsterLevelCap) * SpecimenFactor(m.formId, rank);
        }

        public bool MonsterReady(LairMonster m) { return m != null && m.woundSeconds <= 0 && m.hp > LairBalance.WoundAt; }

        // Power as it fights right now: health, hunger, and half strength when guarding the Heart without a lair.
        public float MonsterFightPower(LairMonster m)
        {
            if (!MonsterReady(m)) return 0;
            return MonsterPower(m) * m.hp * (State.food > 0 ? 1f : LairBalance.HungryPower);
        }

        public List<LairMonster> MonstersIn(int roomUid)
        { return State.lair.monsters.FindAll(m => m.lairRoom == roomUid); }

        // Monsters with no lair (or whose lair was demolished) guard the Heart.
        public List<LairMonster> HeartGuards()
        {
            return State.lair.monsters.FindAll(m =>
            {
                var room = Room(m.lairRoom);
                return m.lairRoom == 0 || room == null || room.type != "monster_lair";
            });
        }

        // ---- Lairs -----------------------------------------------------------------------------------------------

        public int LairCapacity(TowerRoom room)
        {
            if (room == null || room.type != "monster_lair") return 0;
            return 1 + room.width + (room.level >= 6 ? 1 : 0) + (room.level >= 8 ? 1 : 0);
        }

        public LairMonster Monster(int id) { return State.lair.monsters.Find(m => m.id == id); }

        public string AssignMonster(int monsterId, int roomUid)
        {
            var monster = Monster(monsterId);
            var room = Room(roomUid);
            if (monster == null) return "Choose a monster.";
            if (room == null || room.type != "monster_lair") return "Select a Monster Lair first.";
            if (monster.lairRoom == roomUid) return monster.name + " already lives there.";
            if (MonstersIn(roomUid).Count >= LairCapacity(room))
                return "This lair holds " + LairCapacity(room) + " monsters. Upgrade it or build another.";
            monster.lairRoom = roomUid;
            Note(monster.name + " moved into the Monster Lair on " + FloorLabel(room.floor) + ".");
            return null;
        }

        public string UnassignMonster(int monsterId)
        {
            var monster = Monster(monsterId);
            if (monster == null) return "Choose a monster.";
            monster.lairRoom = 0;
            Note(monster.name + " now guards the Heart.");
            return null;
        }

        public int ReleaseRefund(LairMonster m) { return m == null ? 0 : 2 * m.rank; }

        public string ReleaseMonster(int monsterId)
        {
            var monster = Monster(monsterId);
            if (monster == null) return "Choose a monster.";
            int essence = ReleaseRefund(monster);
            State.lair.monsters.Remove(monster);
            State.essence += essence;
            Note(monster.name + " was released into the wild. +" + essence + " Essence.");
            return null;
        }

        // ---- Upkeep ----------------------------------------------------------------------------------------------

        private void TickMonsters(float dt)
        {
            var monsters = State.lair.monsters;
            if (monsters.Count == 0) return;
            State.food = Mathf.Max(0, State.food - LairBalance.MonsterFood * monsters.Count * dt);
            foreach (var m in monsters)
            {
                if (m.woundSeconds > 0) m.woundSeconds = Mathf.Max(0, m.woundSeconds - dt);
                m.hp = Mathf.Min(1f, m.hp + LairBalance.MonsterRegen * dt);
            }
        }

        // Hurt from a fight: at 5% or less it is wounded and sits out five minutes.
        private void WoundMonster(LairMonster m, float loss)
        {
            m.hp = Mathf.Max(0, m.hp - loss);
            if (m.hp <= LairBalance.WoundAt) { m.hp = LairBalance.WoundAt; m.woundSeconds = LairBalance.WoundSeconds; }
        }

        private void MonsterWon(LairMonster m)
        {
            m.xp += LairBalance.XpPerWin;
            while (m.xp >= LairBalance.XpPerLevel && m.level < LairBalance.MonsterLevelCap) { m.xp -= LairBalance.XpPerLevel; m.level++; }
            if (m.level >= LairBalance.MonsterLevelCap) m.xp = 0;
        }
    }
}
