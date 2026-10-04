# Summon battles (expeditions)

In the Tower the fighters live as bodies. On expeditions they are JD's **contracts**: JD stands on the field, and
the fighters are summoned onto it card by card. Tower skirmishes and the battle sandbox keep the classic field
(everyone standing).

## Rules (`BattleState.SummonMode`, `BattleRules.cs`)

- **Summoning.** Playing a fighter's card summons them: they appear in a summon circle, act, and dissolve.
- **The Vanguard.** A card that **anchors** keeps its fighter on the field as the Vanguard. Anchoring cards are:
  - a self shield, including Guard;
  - a hold on themselves: Taunt, a counter stance (IceCounter), Defense Up, or a charge.

  JD's Taunt card calls the chosen fighter out as the Vanguard.
  - Only one Vanguard is out at a time; a new anchor replaces the old one.
  - The Vanguard returns to the contract at round start once the hold has run out. A Guard shield lasts through the
    enemy turn.
  - A Vanguard who is knocked out is recalled at once.
- **Enemy targets.**
  - Single-target attacks hit the Vanguard; with no Vanguard they hit **JD**.
  - Area attacks hit the Vanguard and JD.
  - JD has `SummonJdVitality` (1.6x) health. JD falling ends the battle, as before.
- **Team-ups.** A fighter's card is a team-up when one of their bond partners is the Vanguard or has already acted
  this turn. The partner appears and strikes alongside: "TEAM-UP!" shows, and the card hits for `TeamUpPower` (1.25x).
  - The bond partners come from the existing bond table.
  - Each fighter joins one team-up per turn.
- **JD's pool.** AP and EP belong to JD, not to the fighters: one pool the whole contract draws on, refilled each
  round to the field fighters' maxima added up (3 AP, 9 EP for the usual three). Any fighter can be called again while
  the pool lasts. SP and CP were already JD's. JD's plate shows `AP / EP / SP / CP`; the party tiles drop their pips.
  - A fighter's own AP now only says whether they can be called this round (a stun empties it).
  - AP/EP gains and a break's +1 AP go to the pool; AP/EP transfers between fighters do nothing in a summon battle.
- **Contracts.** HP, stress and the ultimate meter live on the party tiles. A fighter only takes damage while they
  are the Vanguard. Party-wide heals and buffs ("all allies") also reach JD.
- **Tuning** (`ExpeditionBalanceTests`, run in summon mode; health lost counts JD and the fighters together):
  - `SummonEnemyPower` 1.6 for packs and elites;
  - `SummonBossPower` 1.05 for lair bosses, whose charged area blow lands entirely on JD and the Vanguard;
  - all 39 region / kind cells are inside the difficulty bands.
- **AUTO and the balance sim** (`BattleAutoPlayer`) raise a Vanguard when the coming enemy turn would cost JD a sixth
  of their health, and heal JD among the party.

## Field (`BattleSummons.cs`)

- **Layout.** JD stands at the left, drawn from the full-figure cutout `FieldModels/jd` (his chibi rig is for
  plates and portraits).
  - The **Vanguard place** is in front of JD.
  - An attacker steps out further forward when a Vanguard holds.
  - A team-up partner stands just behind.
  - Enemies get the field from x 840.
- **Sizes.** Fighters and JD draw at `AllyScale` 1.25x, enemies at `EnemyScale` 1.1x of their (taller) base, so a
  regular humanoid enemy stands level with a fighter and large beasts and bosses tower over them. The pack stands in two
  staggered rows (`PackStep` 0.5) and is fitted to x 790..1572.
- **Screen shape.** The HUD is pinned to the real screen edges, not the 16:9 canvas: the top bar to the top, the hand,
  party tiles and END TURN to the bottom, corner groups to the sides (`Hud` / `Edge` in BattleMode.cs). On a screen
  taller than 16:9 the ground line drops into the freed space (`FieldDrop`) and units grow up to 1.3x (`TallBoost`);
  on a wider one the pack also uses the right margin.
- **Summon effects.** A summon opens a circle in the fighter's element, then a flash and particles; a fighter leaving
  dissolves into particles.
- **Movement.** Fighters glide between places, so a fighter taking up the Vanguard post doesn't jump.
- **Tiles.** Cards that target an ally (heals, buffs, Guard) target the **party tiles**. The Vanguard's tile carries
  a VANGUARD tag.
- **Enabling it.** `AdamsHavenPrototype.LaunchExpeditionBattle` sets `BattleMode.SummonMode`. The sandbox can opt in
  with `BattleMode.SummonSandbox` (a PlayerPrefs switch).

Tests: `Assets/Editor/SummonBattleTests.cs`.
