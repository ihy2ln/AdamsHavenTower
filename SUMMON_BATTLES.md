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
- **Summon effects.** A summon opens a circle in the fighter's element, then a flash and particles; a fighter leaving
  dissolves into particles.
- **Movement.** Fighters glide between places, so a fighter taking up the Vanguard post doesn't jump.
- **Tiles.** Cards that target an ally (heals, buffs, Guard) target the **party tiles**. The Vanguard's tile carries
  a VANGUARD tag.
- **Enabling it.** `AdamsHavenPrototype.LaunchExpeditionBattle` sets `BattleMode.SummonMode`. The sandbox can opt in
  with `BattleMode.SummonSandbox` (a PlayerPrefs switch).

Tests: `Assets/Editor/SummonBattleTests.cs`.
