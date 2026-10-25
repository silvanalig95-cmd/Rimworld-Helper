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
- `DefenseSight.cs` (pure): the game's LineOfSight, lean sources, CanHit and Cover; `Read(grid)` (worst raider cover
  while able to shoot at a firing position, leaving out cells at or behind the line) and `Problems(grid, grenades)`.
- `DefenseShapes.cs` (pure): `DefenseShapeSpec` (shooters, depth, turret size/count, grenades, low tech, range, raid
  points; `Save`/`Parse` for the save), `DefenseShapeOptions` (the XML `<shape>`), and generators: box (killbox, small,
  tunnel, baffled gate, palisade/castle yard: L entrance, straight 4/5 lengthened until the corner is blind, depth from
  the cone (offset/0.45), grenades (13.4) and reach, double walls past bows, mini decoys on the axis or a 2x2 turret
  behind the gap, slows and traps in the way in, none behind a gate), side lines (searched smallest first until hidden
  from outside, ≤30% in the gate, out of a throw), firing line and post of pockets, turret nest (4 / 7 apart), trap
  corridor (fence beside each trap, 4–8 traps).
- `DefenseLayout.Problems` accepts pockets (`Pocket`), size cap 41.
- Plumbing: `DefenseTemplateDef.shape`, `GridFor(spec)` / `GridFor(map, cfg)` / `SpecFor` / `ColonySpec` (survey:
  ranged ≥ 4, best range, bows or pre-industrial, industrial or empire hostiles = grenades, raid points, the turret the
  plan would pick; a manned turret is no box decoy), `BehindFor`, `ResolveCached(map, out reason, cfg)` keyed by spec;
  `DefensePlanConfig.spec / fixedLayout / moreShooters / moreDepth`, `Of(saved)`; `DefensePlan.grid / spec`;
  `DefenseLinePlan.shape` saved (null = fixed layout from before); all old `t.Grid/Width/Height/line` uses replaced.
  The line review takes a plan's firing position behind a wall when it can lean out (`FirePos.lean`).
- XML: every gate, line, post, killbox, trap corridor, nest and the palisade/castle have a `<shape>` (their `<layout>`
  is kept for plans placed before), new `Rimfolk_Defense_BaffledGate`, wall with an airlock, mortar pit 8 apart with
  the shells 6 off behind a wall, tips fixed for 1.6.
- Tests: `DefenseTests.DrawnChecks` draws every drawn template for ~650 colonies and checks both Problems.

### Plan (next steps)
1. Planner window: say what the layout was sized for, with +/- for shooters and depth (`cfg.Resize`, `ColonySize`).
   Line review: suggest re-planning a placed killbox when the colony has outgrown it (re-plan in place; built pieces
   count as present), and plans placed before 0.14 (`shape` null).
2. Almanac defense pages (Almanac_Defense.xml, Almanac_DefendingLines.xml), README/ROADMAP rows, What's new 0.14.0,
   About.xml 0.14.0, all checks, commit, push.

## Smaller open items
- `Any_Last` talk pools hold some `[agree]`/`[tease]`/`[comfort]`/`[joke]` lines that can never be said (those talks end
  after three lines). Harmless.
- A confession waiting for company (`Together.WhenInCompany`) is not saved across a load. Minor.
