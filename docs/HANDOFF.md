# Handoff: work in progress

Last shipped: 0.14.1 "Not everyone gets along" (the player: everyone becomes friends with enough time together;
time should count, but not everyone has to get along). Before it, 0.14.0 "Shaped for the colony" (the defense
overhaul), 0.13.1 "Fungus kept for its meal" and 0.13.0 "More to live through".

## Friendships, as shipped (0.14.1)

From the player's save (9 colonists, day 63): every pair were comrades at strength 90+, so `Council_Tie_Comrades` gave
+14 to 64 of 72 opinions; the game's deep talks (+15 each, five per pair) gave the rest. 36 of 72 opinions were 20 or
more (the Social tab's "Friend"), including pairs whose game compatibility is -1.1 to -1.5.
- `Social/ChemistryMath.cs` (pure, tested in `ChemistryTests`): factors on a memory's opinion as it's given (good x0.35
  to x1.15, bad x1.35 to x0.85), on Rimfolk's warm moments (x0.4 to x1.2) and frictions (x2 to x0.6), and the lasting
  feeling's stage by compatibility and days together (-3/-6/-10 at -0.6/-1.2/-1.8 after 5/15/30 days; +3/+6 at 1.5/2.5).
- `Social/Chemistry.cs`: compatibility is the game's `CompatibilityWith` (the same number behind deep talk and insult
  rates); `Patch_Chemistry` (TryGainMemory prefix) scales memories that come of conversation (every InteractionDef's
  thoughts, `Rimfolk_Talk_*`) between two of the colony's people; `ThoughtWorker_Chemistry` (`Council_Chemistry`).
  Partners, babies and anyone outside the colony are left alone. Gated by the social web setting and the deep hooks.
- Comrades: +3/+5/+8 (was +4/+8/+14). The Social tab's pair note says when two don't click or get on easily.
- Estimated on the save: 29 of 72 opinions at 20 or more (was 36), two who clash hardest at -12 and -16. Existing
  memories keep their old values until they fade (deep talks last 20 days).

## Weather round (in progress, not shipped)

The player asked for weather ideas 1, 3, 5, 6, 7 and 8 (the colony sees Rimfolk's own weather coming; a year's weather
remembered; weather quirks; visible touches; visitors, camps and missions in weather; Seasonal Weather's disasters),
a proposal on making weather change more dynamic (with Rimfolk's events included), and soft integrations for weather
mods that are safe to add to a running save. Begun but not yet in the repository: `Weather/WeatherMath.cs` (pure: days,
quadrums, records, comparisons, season names, event requirements like "rain:5/7", packing). Found on the way:
`GameCondition_Hail` forces Rain with no temperature check and leaves the rain running after the hail ends; several
land events (the long rains, high water, a thirsty herd...) claim weather that is never checked.

## The defense overhaul, as shipped

Research: `docs/defense-research/mechanics.md` (game mechanics with numbers, mostly from the decompiled 1.6 code) and
`designs.md` (designs that hold in 1.6, K1–K4, G1, P1, and a template-by-template review); `Tools/DefenseSim/kbsim.py`.

Code:
- `DefenseSight.cs` (pure): the game's LineOfSight, lean sources, CanHit and Cover; `Read(grid)` (worst raider cover
  while able to shoot at a firing position, leaving out cells at or behind the line) and `Problems(grid, grenades)`.
- `DefenseShapes.cs` (pure): `DefenseShapeSpec` (shooters, depth, turret size/count, grenades, low tech, range, raid
  points; `Save`/`Parse`), `DefenseShapeOptions` (the XML `<shape>`), and the generators: box (killbox, small, tunnel,
  baffled gate, palisade/castle yard), side lines, firing line and post of pockets, turret nest, trap corridor. The box
  lengthens its straight (corner seen) or deepens (doorway angle) until `DefenseSight.Read` passes; side lines try
  placements smallest first until hidden from outside, ≤30% in the gate and out of a throw.
- `DefenseLayout.Problems` accepts pockets (`Pocket`); size cap 41.
- `DefenseTemplateDef`: `shape`, `ShapeFor/GridFor(spec)`, `GridFor(map, cfg)`, `SpecFor`, `ColonySpec` (survey: ranged
  ≥ 4, best range, bows or pre-industrial, industrial or empire hostiles = grenades, raid points, the turret the plan
  picks; a manned turret is no box decoy), `BehindFor`, `SampleGrid`, `ResolveCached(map, out reason, cfg)` keyed by
  spec. `DefensePlanConfig.spec / fixedLayout / moreShooters / moreDepth`, `Resize`, `ColonySize`, `Of(saved)`.
  `DefensePlan.grid / spec`; `DefenseLinePlan.shape` saved (null = fixed layout, or placed before 0.14).
- Line review: `FirePos.lean` (pockets count and see by leaning); the killbox step notes an outgrown or pre-0.14
  killbox and offers "Plan it again"; close the gap offers the baffled gate against grenades when its trial fits.
- Planner window: "Drawn for your colony" with Shooters and Depth −/+, what it was drawn for, a grenade warning.
- Tests: `DefenseTests.DrawnChecks` draws every drawn template for ~650 colonies (8,400 plans) and checks both
  Problems, the world checks for a middle colony, and the spec's save round trip.

Not done, ideas for later (propose to the player first): a second rank of shooters for very big raids; mortars and
EMP steps on the ladder; a bait yard with an orbital trade beacon (40% of center drops); turrets as targets in the
sight check (the corner camper can see axis decoys); the line review re-planning the gate lines placed before 0.14
(the old gate's lines stand within a grenade's throw). Nothing was tried in the game itself here: worth a look at a
killbox's preview, the planner's new section, and a built pocket line in a line's review.

## Smaller open items
- `Any_Last` talk pools hold some `[agree]`/`[tease]`/`[comfort]`/`[joke]` lines that can never be said (those talks end
  after three lines). Harmless.
- A confession waiting for company (`Together.WhenInCompany`) is not saved across a load. Minor.
