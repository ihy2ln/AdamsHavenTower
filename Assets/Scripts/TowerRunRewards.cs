using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // A card upgraded during the current run (BattleCard.Level, 1 to MaxCardLevel).
    [Serializable] public sealed class TowerCardLevel { public string card = ""; public int level = 1; }

    public sealed class TowerRelicDef
    {
        public readonly string id, name, text;
        public TowerRelicDef(string id, string name, string text) { this.id = id; this.name = name; this.text = text; }
    }

    // Run rewards (expedition review step 3). Every won fight offers a pick of three: upgrade one of the party's
    // cards, take a relic that lasts the run, or a supply. Elites and the lair boss always offer a relic.
    // Upgrades and relics are lost when the run ends, like the GDD's run deckbuilding; stress carries between fights.
    public sealed partial class TowerRules
    {
        public const int MaxCardLevel = 3, RewardChoices = 3;

        public static readonly TowerRelicDef[] Relics =
        {
            new TowerRelicDef("war_drum", "War Drum", "Every attack the party makes deals 8% more damage."),
            new TowerRelicDef("ember_heart", "Ember Heart", "The party's critical hit chance rises by 6%."),
            new TowerRelicDef("calm_tea", "Calming Tea", "Stress builds 30% slower in battle."),
            new TowerRelicDef("scout_lens", "Scout's Lens", "The lead fighter starts every fight with an extra EP."),
            new TowerRelicDef("bark_ward", "Bark Ward", "The lead fighter starts every fight with its defence raised."),
            new TowerRelicDef("stormglass", "Stormglass Shard", "The first magic card of each fight hits 25% harder; other attacks lose 10%."),
            new TowerRelicDef("ironbark", "Ironbark Charm", "The first physical card of each fight hits 25% harder; magic loses 10%."),
            new TowerRelicDef("mending_moss", "Mending Moss", "The party recovers 8% health after every won fight."),
            new TowerRelicDef("lucky_coin", "Lucky Coin", "Fights pay 25% more gold."),
        };

        public static TowerRelicDef Relic(string id)
        {
            foreach (var r in Relics) if (r.id == id) return r;
            return null;
        }

        public bool HasRelic(string id) { var run = Run; return run != null && run.relics != null && run.relics.Contains(id); }
        public bool RewardPending { get { var run = Run; return run != null && run.rewardOffer != null && run.rewardOffer.Count > 0; } }

        private void NormalizeRewards(TowerRun run)
        {
            if (run.rewardOffer == null) run.rewardOffer = new List<string>();
            if (run.relics == null) run.relics = new List<string>();
            if (run.cardLevels == null) run.cardLevels = new List<TowerCardLevel>();
            if (run.stress == null) run.stress = new List<int>();
            while (run.stress.Count < run.party.Count) run.stress.Add(0);
        }

        public int CardLevel(string cardId)
        {
            var run = Run;
            if (run == null || run.cardLevels == null) return 1;
            var entry = run.cardLevels.Find(c => c.card == cardId);
            return entry == null ? 1 : entry.level;
        }

        // Card id -> level for the battle deck.
        public Dictionary<string, int> CardLevels()
        {
            var levels = new Dictionary<string, int>();
            var run = Run;
            if (run != null && run.cardLevels != null) foreach (var c in run.cardLevels) levels[c.card] = c.level;
            return levels;
        }

        // Card id -> epiphanies ("swift,echo") taken earlier in this run, for the battle deck.
        public Dictionary<string, string> CardMods()
        {
            var mods = new Dictionary<string, string>();
            var run = Run;
            if (run == null || run.epiphanies == null) return mods;
            foreach (var line in run.epiphanies)
            {
                int eq = line.IndexOf('=');
                if (eq > 0) mods[line.Substring(0, eq)] = line.Substring(eq + 1);
            }
            return mods;
        }

        // Adds the epiphanies a fight produced (card id -> mods taken in that fight) to the run's own.
        public void KeepEpiphanies(Dictionary<string, string> taken)
        {
            var run = Run;
            if (run == null || taken == null || taken.Count == 0) return;
            if (run.epiphanies == null) run.epiphanies = new List<string>();
            var all = CardMods();
            foreach (var pair in taken)
            {
                string before;
                all[pair.Key] = all.TryGetValue(pair.Key, out before) && before.Length > 0 ? before + "," + pair.Value : pair.Value;
                Note("Epiphany: a card grew stronger for the rest of the expedition.");
            }
            run.epiphanies.Clear();
            foreach (var pair in all) run.epiphanies.Add(pair.Key + "=" + pair.Value);
        }

        // What the relics change inside a fight (BattleState applies these).
        public BattleRunModifiers BattleModifiers()
        {
            var mods = new BattleRunModifiers();
            if (HasRelic("stormglass")) mods.Relic = "stormglass";
            else if (HasRelic("ironbark")) mods.Relic = "ironbark";
            mods.Scout = HasRelic("scout_lens");
            mods.Fortify = HasRelic("bark_ward");
            if (HasRelic("war_drum")) mods.DamageBonus = .08f;
            if (HasRelic("ember_heart")) mods.CritBonus = .06f;
            if (HasRelic("calm_tea")) mods.StressScale = .7f;
            return mods;
        }

        // After a won fight: three distinct offers. Boss and elite fights always include a relic.
        // poi: the place the fight was in (its mods may promise a relic), "" for an ambush.
        private void OfferRewards(string kind, TowerRng rng, string poi = "")
        {
            var run = Run;
            NormalizeRewards(run);
            var offers = new List<string>();
            var cards = new List<BattleCard>();
            var units = BattleCatalog.Party(run.party);
            for (int i = 0; i < run.party.Count; i++)
            {
                var unit = units.Find(u => u.Id == run.party[i]);
                if (unit == null || run.hp[i] <= 0) continue;
                foreach (var card in BattleCatalog.FighterKit(unit)) if (CardLevel(card.Id) < MaxCardLevel) cards.Add(card);
            }
            var relics = new List<TowerRelicDef>();
            foreach (var r in Relics) if (!HasRelic(r.id)) relics.Add(r);
            // A Fortified place (TowerAtlasNodes) always has a relic on offer too.
            bool relicDue = kind == "boss" || kind == "elite" || kind == "treasure" ||
                (!string.IsNullOrEmpty(poi) && NodeTotals(poi).relic) || rng.Value() < .35f;
            if (relicDue && relics.Count > 0) offers.Add("relic:" + relics[rng.Next(relics.Count)].id);
            while (offers.Count < RewardChoices && cards.Count > 0)
            {
                var card = cards[rng.Next(cards.Count)];
                cards.Remove(card);
                offers.Add("upgrade:" + card.Id);
            }
            string[] supplies = { "tonic", "rations", "firewood" };
            for (int i = 0; offers.Count < RewardChoices && i < supplies.Length; i++) offers.Add(supplies[(i + rng.Next(3)) % 3]);
            run.rewardOffer = offers;
        }

        public static string RewardTitle(string offer)
        {
            if (offer.StartsWith("relic:", StringComparison.Ordinal)) { var r = Relic(offer.Substring(6)); return r == null ? "Relic" : r.name; }
            if (offer.StartsWith("upgrade:", StringComparison.Ordinal))
            {
                var card = FindCard(offer.Substring(8));
                return card == null ? "Upgrade" : "Upgrade " + card.Name;
            }
            return offer == "tonic" ? "A tonic" : offer == "rations" ? "Two rations" : "A bundle of firewood";
        }

        public string RewardText(string offer)
        {
            if (offer.StartsWith("relic:", StringComparison.Ordinal)) { var r = Relic(offer.Substring(6)); return r == null ? "" : r.text; }
            if (offer.StartsWith("upgrade:", StringComparison.Ordinal))
            {
                string id = offer.Substring(8);
                var card = FindCard(id);
                int level = CardLevel(id);
                return card == null ? "" : FighterName(card.Owner) + "'s card, level " + level + " to " + (level + 1) +
                    ": +" + Mathf.RoundToInt(BattleCard.LevelStep * 100) + "% power and healing for the rest of the run.";
            }
            return offer == "tonic" ? "Heals 40% or revives a fallen fighter." : offer == "rations" ? "Food and water for the road." :
                "Enough for one more warm rest.";
        }

        private static BattleCard FindCard(string id)
        {
            foreach (var unit in BattleCatalog.Party())
                foreach (var card in BattleCatalog.FighterKit(unit)) if (card.Id == id) return card;
            return null;
        }

        private static string FighterName(string unitId)
        {
            foreach (var unit in BattleCatalog.Party()) if (unit.Id == unitId) return unit.Name.Split(' ')[0];
            return unitId;
        }

        public string ChooseReward(int index)
        {
            var run = Run;
            if (!RewardPending) return "No reward to choose.";
            if (index < 0 || index >= run.rewardOffer.Count) return "Pick one of the rewards.";
            string offer = run.rewardOffer[index];
            run.rewardOffer = new List<string>();
            if (offer.StartsWith("relic:", StringComparison.Ordinal))
            {
                string id = offer.Substring(6);
                if (!run.relics.Contains(id)) run.relics.Add(id);
                Note("Relic found: " + RewardTitle(offer) + ".");
            }
            else if (offer.StartsWith("upgrade:", StringComparison.Ordinal))
            {
                string id = offer.Substring(8);
                var entry = run.cardLevels.Find(c => c.card == id);
                if (entry == null) { entry = new TowerCardLevel { card = id, level = 1 }; run.cardLevels.Add(entry); }
                entry.level = Mathf.Min(MaxCardLevel, entry.level + 1);
                Note(RewardTitle(offer) + " (level " + entry.level + ").");
            }
            else if (offer == "tonic") { run.tonics++; Note("Took a tonic."); }
            else if (offer == "rations") { run.rations += 2; Note("Took two rations."); }
            else { run.firewood++; Note("Took a bundle of firewood."); }
            return null;
        }

        // Stress after a fight, per party member (same order as run.party).
        public void RecordStress(List<int> stress)
        {
            var run = Run;
            if (run == null || stress == null) return;
            NormalizeRewards(run);
            for (int i = 0; i < run.party.Count && i < stress.Count; i++) run.stress[i] = Mathf.Clamp(stress[i], 0, 100);
        }

        public int Stress(int partyIndex) { var run = Run; return run == null || run.stress == null || partyIndex >= run.stress.Count ? 0 : run.stress[partyIndex]; }

        private void EaseStress(int amount)
        {
            var run = Run;
            if (run == null) return;
            NormalizeRewards(run);
            for (int i = 0; i < run.stress.Count; i++) run.stress[i] = Mathf.Max(0, run.stress[i] - amount);
        }

        // A won fight: offer rewards and let the run's relics act.
        private void AfterWin(string kind, TowerRng rng, string poi = "")
        {
            if (HasRelic("mending_moss")) HealParty(8, false);
            OfferRewards(kind, rng, poi);
        }
    }
}
