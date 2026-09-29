# Tower life layer (Sept 29)

Simulation (`Assets/Scripts/TowerLife.cs`, partial `TowerRules`):
- Traits (10) and schedules (day / night / flexible). Sleepers go home and lie down; emergencies with priority 2+ wake them.
- Mood = 50 + named thoughts (`Thoughts()`), shown in the MOOD tab. Below 18 for 25s triggers a 40s mood break (no work).
- Storyteller: `State.threat` (0-100) tracks pressure minus defence. It picks incident kinds (raiders need threat 35+),
  shortens event gaps, and drops 8 per resolved incident. Positive events: caravan, harvest festival, self-arriving traveller.
- New incident `cave_in` (underground rooms only), answered by repair priority.
- Goals: three active from `GoalPool`, counters bumped by build/collect/rush/recruit/etc. Claim pays gold/Celestium/tonics.
- Adjacency: same-type neighbours merge (+12% each, max 3); pairs like Kitchen+Well add +8%.

Presentation (`TowerFx.cs`, attached at runtime like TowerArtDirector):
- Day/night tint, warm window glows, fireflies, drifting clouds, Celestium motes up the core shaft.
- Tap-to-collect resource bubbles, hazard alerts with particles, floating result text, mood icons over residents.
- It reads state and listens to `TowerRules.Signal`; the only thing it changes is a bubble tap calling `Collect`.

HUD: story bar (clock, threat meter, GOALS), MOOD tab with schedule cycling, adjacency line in room details.

Tests: `TowerSimulationTests` (28). Run by calling the methods from `Unity_RunCommand`, or via Test Runner.
Do not edit scripts while Play Mode is running: the domain reload leaves the controller with null rules.

## Sept 29 (2): centred Heart, premium buttons, guide and needs validation

- Layout: the Heart shaft (cell 22) is the middle. Each floor has `west` and `east` founded cells; the Gate holds the first
  east cell on the ground floor. Expand with `ExpandFloor(floor, -1|+1)`; rooms build outward from the shaft on both sides.
  Older saves gain `east = 1` on every floor at load. New checkpoints put an east wing on floors -4..+4.
- Buttons: `TowerUiSkin` (glossy tinted body, gold frame, drop shadow) and `TowerButtonFx` (hover/press spring).
- Needs: `IncomePerMinute / UpkeepPerMinute / NetPerMinute` feed the coloured trends in the top bar; `NeedsAdvice()` shows one
  actionable line under the story bar (which producer to build or staff, or a bed for a waiting wanderer).
- Guide: lessons 2 and 5 open the RESIDENTS panel by themselves.
- Tests: 40 in `TowerSimulationTests`, including a full guide run for all three founders and a one-hour fed economy.

## Sept 29 (3): decluttered HUD

- Top bar: day/clock, threat meter, one line of primary stocks (tap it for wood/stone/ore/tonics), TIME and MENU.
- Dock: BUILD, FLOORS, PEOPLE, TASKS. Tap opens a popup (PEOPLE toggles the roster panel). Press and hold (about 0.4s) opens a flyout:
  BUILD -> category shortcuts, FLOORS -> floor jumps, PEOPLE -> tab shortcuts, TASKS -> claim all / recruit / auto-haul,
  TIME -> pause, 1x, 2x, 4x, 8x time lapse, MENU -> save, checkpoints.
- Room panel opens when a room is selected and has its own close button. Rooms are only placed while a card is in hand
  (chip above the dock, CANCEL or Esc to drop it). TASKS turns red on an incident and gold when a goal can be claimed.
- Messages are toasts that fade after a few seconds. `TowerHoldButton` (tap vs hold) and `TowerUiSkin.ApplyPanel` are reusable.

## Sept 29 (4): colony layer (Fallout Shelter x RimWorld)

All in `Assets/Scripts/TowerColony.cs` (partial `TowerRules`), hooked into the existing ticks.
- Levels: collecting, training, rescues, repelling incidents and expeditions give XP. Level-ups heal fully and raise
  `MaxHp` (105 + 2.5 per level). The roster shows `Lv`; resident details show xp/next and HP/max.
- Brownout: with no firewood banked, `TickPower` keeps only as many cells lit as the mills can feed, nearest the Heart
  first. Dark rooms stop working and are drawn dark with "NO FIREWOOD". Mills, Heart and Gate never go dark.
- Gate guard post: the Gate takes 2 workers (task `guard`, uses Defend priority). `StartRaid` puts raiders at the Gate.
  Undefended raiders loot gold (returned if beaten), push to a neighbouring room every 30s (same floor, or via the core
  landing), and only wound the Heart inside its chamber. Merged same-type rooms draw without the dividing post.
- Health: collapsing from hunger is not a wound. Downed residents who are fed get back up on bed rest. Critical ones
  (injury >= 50) bleed out after 240 live seconds untended (a full day if starving). Villagers die into
  `State.memorial`; heroes are pulled back by the Heart. Care works without tonics at 45% strength. Offline catch-up never kills.
- Relationships: residents sharing a room build opinion (`State.bonds`); traits scale it, misery reverses it. Rivals
  can fight (injuries), close friends can fall in love (auto family). Thoughts: friend/rival nearby, mourning, partner lost.
- Mood breaks by temperament: sulk (home), binge (eats stores), tantrum (damages workplace), wander (to the Gate).
- Steward (on by default, TASKS toggle): every 20s (5s when a stock is empty) moves one best-matching worker onto the
  most urgent failing stock, using `PlannedNetPerMinute`. AUTO-ASSIGN IDLE places jobless adults by best stat.
- Checkpoints: `TowerMilestones.Version = 2`. Generation staffs survival rooms and posts two Gate guards. On start, slots
  2-10 built by an older generator are rebuilt and the old file is kept as `slot_XX.json.gen0.bak`. Slot 1 is never touched.
- Tests: 50 in `TowerSimulationTests`, including every checkpoint feeding itself for 30 minutes with no deaths.
