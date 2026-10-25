# Handoff: work in progress

Last shipped: 0.13.1 "Fungus kept for its meal" (with Ideology, meal bills other than the fungus meal's start with fungus
unticked; an older save's queued meal bills get it unticked once on load, `fungusBillsVersion`). Before it, 0.13.0 "More
to live through" (content round, build priorities shown as a light tint, camps about once a season, lucky charms counted
when carried).

## In progress: defense overhaul (player request)

The player asked whether the defense templates (killboxes, defense lines, traps, turrets, gates) are really the most
effective, and to learn how to set them up properly and make the planner handle them effectively and dynamically.

### Research (done)
- `docs/defense-research/mechanics.md`: game mechanics with numbers, mostly read from the decompiled 1.6 game code
  (raider pathing and targets, leaning and cover, ranges, turrets, traps, raid types, 1.5 → 1.6 changes).
- `docs/defense-research/designs.md`: designs that work in 1.6, with ASCII layouts (K1–K4 killboxes, G1 baffled gate,
  P1 palisade yard), and a template-by-template review of `Defs/DefenseTemplateDefs/Rimfolk_DefenseTemplates.xml`.
- `Tools/DefenseSim/kbsim.py`: a Python port of the game's line of sight, leaning and cover used for that review.

Findings that drive the redesign:
- A raider at the cell before a corridor's last turn leans into it and sees down the final straight with 75% wall cover.
  The current killbox, small killbox and tunnel killbox all let that corner see the firing positions at 75–79%. Fix:
  a final straight of 4 cells (5 with wall pockets) and no shooter within 2 cells of the corridor's axis.
- A raider in a 1-wide doorway has 0% cover against shooters within about 25° of straight ahead; 15% at 34°, 30% at
  45°, 75% beside the opening. Shooters go straight across the doorway, off the axis, never wrapped round its sides.
- Cover is reckoned at the shooter's own cell even while leaning. Best firing cell: a "pocket" (wall in front,
  sandbags on both front diagonals and both sides): 75% straight on, 67–75% at an angle. W-S-W gives 55–80%, plain
  sandbags 55–65%. A pawn behind a continuous wall can't shoot; behind a wall with an open diagonal it leans out.
- Grenades reach 12.9: shooters 13.5+ cells from the doorway when raiders bring grenades (not tribals).
- Turret blasts: 3.9 for 1x1, 5.9 for 2x2 (from the bottom-left cell); autocannon min range 8.9, uranium 11.9.
  The current killbox and turret nest put shooters and turrets inside each other's blast.
- 1.6: raiders may stop and fire on sandbag/barricade cells (only 0.4× as keen), so sandbag "T"s no longer deny a spot.
- Normal raiders take any walkable route however long; they break walls only when nothing is reachable. Any non-wall
  colony building they can touch from outside (cooler, lamp) is trashed before they walk on to colonists.
- Traps: no diagonal adjacency either; put a fence beside each trap (smart raids know 30% of traps).
- Gatehouse/castle gate: no archer can hit any cell of the yard (yard walls are solid). Fix: P1 killing yard.

### Code so far
- `Source/ColonyCouncil/Defense/DefenseSight.cs` (new, pure, no game types): the game's LineOfSight, lean sources,
  CanHit and Cover; `Read(grid)` (worst raider cover while able to shoot at a firing position, coverage) and
  `Problems(grid, grenades)` (raider cover > 45% with a view, firing positions within grenade reach of the doorway,
  turret blasts, big turret too close to the doorway). Off-layout cells on the wall line count as wall.
- `DefenseLayout.Grid` has `doorX/doorZ` (the doorway into the kill zone, set by generated layouts).
- A scratch harness reading the current templates with it confirmed the research verdicts.

### Plan (next steps)
1. `DefenseShapes.cs` (pure): generators that build a layout from the colony's numbers (a `DefenseShapeSpec`:
   shooters, depth, turret size/count, grenades, low tech, weapon range, raid points) and per-template options
   (`DefenseShapeOptions`, deserialised from the template's XML):
   - "box" family: killbox (K1/K2/K4), tunnel killbox (K3), baffled gate (G1), palisade/castle yard (P1). Mouth (open
     or gate), turns (leg length 0/1+), final straight 4/5, box width from shooter offsets ±3, ±5, ... (pocket or plain
     sandbag line; embrasure back wall with a corner gate for P1), depth = max(10/12, offset/0.45, 14 with grenades),
     double side walls and doorway wall for industrial, decoy mini-turrets on the axis 6 and 10 in front of the line or
     one 2x2 turret 6 behind the centre gap, slowing objects every other cell in the outer approach for big raids.
   - cover line behind a plain gate (side lines stay hidden from outside; check with DefenseSight), firing line (pockets),
     post (pockets), turret nest (spacing by turret size), trap corridor (fence beside each trap, 4–8 traps by raid size).
2. Plumbing: `DefenseTemplateDef.shape` (options) + `GridFor(spec)` cache; `DefensePlanConfig.spec` (null = automatic
   from the survey: ranged fighters, best range, bows/pre-industrial, industrial or empire hostiles = grenades, raid
   points, the turret the colony would pick); `DefenseLinePlan.shape` saved so placed plans are re-read with the size they
   were built with; replace `t.Grid`/`t.Width`/`t.line` uses with the grid for the config (call sites are in
   DefensePlanner, Designator_PlanDefense, Window_DefensePlanner, DefenseLineFacts, DefenseLineReview, DefenseStepWorkers,
   DefenseApproachPlanner, DefenseSurvey, DefenseLines, UI/DefensePanel).
3. `DefenseLayout.Problems`: accept a firing position with a wall in front when a front diagonal and a side are see-over
   (the pocket). Tests: every generated layout over a grid of specs passes `DefenseLayout.Problems` and
   `DefenseSight.Problems`; add DefenseSight.cs and DefenseShapes.cs to `Tools/Tests/Tests.csproj`.
4. Planner window: say what the layout was sized for, with +/- for shooters and depth. Line review: suggest widening a
   placed killbox when the colony has outgrown it (re-plan in place; built pieces count as present).
5. XML: templates point at their shapes; new baffled gate; fix tips that are wrong for 1.6 ("never behind a wall",
   "entrance too long", sandbag T at the exit); wall template gets an airlock (gate, cell, gate); mortar pit with walled
   mortars 6 apart and shells 6+ away; fallback line advice: around the bedroom cluster (breachers and sappers aim at beds).
6. Almanac defense pages, README/ROADMAP rows, What's new 0.14.0, About.xml 0.14.0, all checks, commit, push.

## Smaller open items
- `Any_Last` talk pools hold some `[agree]`/`[tease]`/`[comfort]`/`[joke]` lines that can never be said (those talks end
  after three lines). Harmless.
- A confession waiting for company (`Together.WhenInCompany`) is not saved across a load. Minor.
