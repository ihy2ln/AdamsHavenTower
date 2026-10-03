# Codex cards (BM 10.3.0 follow-up)

The CODEX screen shows every beast, party fighter, Tower character and lair boss as a card. It is drawn live from the
game data, so a card changes when its numbers, moves or art change.

## Where it opens

- **Battle**: the CODEX button in the top bar (beside MEDIA) opens the bestiary. The CODEX CARD button on the
  character sheet opens that unit's card: a fighter, its beast form, or a lair boss.
- **Expedition**: CARD CODEX in the guild journal. It opens on LAIR BOSSES from the Regions tab, and on the bestiary
  from the other tabs.
- **Code**: `CodexPanel.Show(tab, entryId, CodexContext.From(rules), onClosed)`, or `CodexPanel.ShowFor(battleUnit, ...)`.

## Tabs

| Tab | Cards | Hidden until |
|---|---|---|
| BESTIARY | All 98 forms, family by family in evolution order | Met in battle (the guild journal's beasts). Until then the card shows the form's battle cutout as a black silhouette, its rank span and its habitat. |
| FIGHTERS | The six party fighters and JD | Always shown |
| ROSTER | Every hero, then every resident, by rank | Always shown; marked IN THE TOWER once summoned |
| LAIR BOSSES | One per region with a lair (13), in the order the regions open | That region has been walked; marked DEFEATED once conquered |

Sandbox battles have no journal, so nothing is hidden there. A beast on the field always counts as met, even in Tower
skirmishes, which keep no journal.

## The card

**Front.** The art fills the card. Over it:

- the rank gem (F grey to SSR prismatic), or FIGHTER / SUMMONER for the party;
- the element tag;
- the name and subtitle;
- four numbers. Units show HP, their stronger offence (ATK or MAG), DEF and SPD. Roster characters show their primary
  and secondary stats, then grit and luck.

Lair bosses also carry a LAIR BOSS ribbon and a red outer edge.

**Back.** Tap the large card, press FLIP CARD, or press Space/F to turn it over.

- **Monsters and lair bosses with painted move art.** The back is the painted move-list card. Each move is written into
  one of its four panels: name, kind, EP cost and effect. There are four rows, as on the painted card: the form's moves
  (up to four), then the GKOM Core and Crystal Hide passives to fill. This is the same list as
  `Tools/build_monster_prompts.py` `action_list`.
- **Lair bosses** list the moves they fight with: two form moves, Gathering Fury, and their signature.
- **Fighters** list their six deck cards, two ultimates and awakening. JD lists the command cards (AP) and decrees.
- **Roster characters** list their weapon, passive, abilities and ultimate, or a resident's perk and tool. The
  "Visual:" art notes are left off.
- **Without painted art** the back is a drawn move list in the same layout.

**Detail column.**

- **Monsters:** stats at any rank inside the form's span (the − / + stepper; each step is the monster at the depth where
  that rank is natural), lore, the evolution ladder ("???" for forms not met), drops and habitat.
- **Lair bosses:** the lair, the danger level and a signature warning.
- **Fighters:** their kit.
- **Roster characters:** quote, personality and origin.

Keys: ← / → step through the cards, Esc closes.

## Art

| Card | Front | Back |
|---|---|---|
| Monster | `Resources/AdamsHaven/Bestiary/Cards/<form>_card.jpg` (grid: `_thumb.jpg`); until painted, the battle cutout `FieldModels/<art>` (or a media-library field picture) over an element wash | `<form>_moves.jpg`, with the panels in `panels.json` |
| Lair boss | The boss form's card | The boss form's move card |
| Fighter | A media-library `unit.<id>.card` import, else `FullCards/<id>` | Drawn |
| Roster | `Roster/Art/<id>/card-front` (residents: `card`) | Drawn |

### Syncing monster art

Monster art comes from `MonsterPrompts/<family>/<form>/playing_card.png` and `action_card.png`. Copy it in with:

```
python Tools/sync_codex_cards.py          # copies new or changed art, safe to re-run while generation continues
python Tools/sync_codex_cards.py --check  # report only
```

The tool never writes to `MonsterPrompts`. It saves 640x960 JPEGs plus 256x384 grid thumbnails, the silhouettes of
unmet beasts (`<form>_shadow.png`, from `battle_front.png`), and each form's **battle cutout**: `battle_left.png`
(the view facing the party) goes to `Resources/AdamsHaven/FieldModels/<form>.png`, so every form fights as itself
instead of borrowing another family's picture. The 17 original hand-placed cutouts are kept. After a sync, run
`python Tools/build_bestiary.py` so `bestiary.json` points each form at its own art (and `BESTIARY.md` lists the
rest as owed). `BattleMode.EnemyHeight` sizes the new sprites from the bestiary: large forms 400, others 320, wide
four-legged ones 20% lower, lair bosses never under 440.

It also finds the four blank move panels on each move card and records them in `panels.json`, so the game can write
into them. To find them it averages the brightness of each row across the panel band. Panel interiors are dark
(below ≈0.21); the borders between them are bright. It then picks the run of four similar-height bands, and the
panels' shared column is the median over the four.

A card where the panels cannot be found gets no entry, and the game falls back to `CodexCards.DefaultPanels`. All 57
cards painted so far were detected.

New art shows the next time the codex opens: it drops its loaded art on open and close. `CodexCardImporter` imports
these textures at their own size, mipmapped and clamped.

## Code

- `Assets/Scripts/Codex/CodexCards.cs`: the entries (`Monsters`, `Fighters`, `Roster`, `Bosses`), plus:
  - `CodexContext`: what the guild has seen;
  - `Stats` / `FrontStats`, `Moves` and `Describe`: move text;
  - art lookup and panels.
- `Assets/Scripts/Codex/CodexPanel.cs`: the IMGUI screen on the 1600x900 canvas. Like `MediaPanel`, it is drawn above
  everything and swallows input. BattleMode treats it as modal; the expedition puts its uGUI click blocker under it.
- `BattleCatalog.Specimen(form, rank)`: a form at a rank, for the card numbers.
- `BattleCatalog.LairBoss(lair, depth)`: the lair boss as fought; encounters use it too, so the card and the fight
  cannot drift apart.
- `BattleBestiary.DepthForRank(rank)`: the inverse of `RankForDepth`.
- Tests: `Assets/Editor/CodexTests.cs`.
