# RimWorld 1.6 defense mechanics — research notes for the defense planner

Compiled 2026-10-24. Research only; nothing in `/home/user/Rimworld-Helper` was changed.

## How to read this

**Confidence marks**

| Mark | Meaning |
|---|---|
| **CODE-1.6** | Read in decompiled 1.6 game code. Every `const` quoted here also appears in the official 1.6.4871 reference assembly (Krafs.Rimworld.Ref 1.6.4871). |
| **CODE-1.5** | Read in the decompiled 1.5.4409 code. Used to flag what changed in 1.6. |
| **XML-1.5** | A def value from the 1.5 Data XML. No 1.6 Data mirror could be found. Where a 1.6 wiki page or changelog confirms or changes the value, that is stated. |
| **DEFLIST-1.6** | Def names and English descriptions from Ludeon's official translation repo at the 1.6.4739 commit. |
| **WIKI** | rimworldwiki.com page (current text, generally 1.6) or a wiki changelog page. |
| **ANEC** | Steam or other forum anecdote. |

**Sources** (short IDs used below)

- **[D16]** 1.6 decompile: https://github.com/32bitx64bit/RW-Decompiled-1.6 (paths such as `Verse/PathUtility.cs` are relative to the repo root, branch `main`). The file set matches the Krafs 1.6.4871 reference assembly except for one generated file, so it is a late 1.6 build.
- **[REF]** https://www.nuget.org/packages/Krafs.Rimworld.Ref/1.6.4871 (method bodies stripped, but `const` values are kept; used to cross-check).
- **[D15]** 1.5.4409 decompile: https://github.com/kasirii/RimWorld1.5.4409Decompiled (paths under `Assembly-CSharp/`).
- **[X15]** 1.5 Data XML mirror (Core, Royalty, Ideology, Biotech and Anomaly; no Odyssey): https://github.com/AustenXiyue/RimWorldLibrary/tree/main/vanilla/Data
- **[T16]** Ludeon/RimWorld-de at 1.6.4739: https://github.com/Ludeon/RimWorld-de/tree/5ea2c8f8ea19896f62e514c7b480d754806027a8 (its `<!-- EN: -->` comments carry the English def text).
- **[W:Page]** https://rimworldwiki.com/wiki/Page
- **[CL4518]** https://rimworldwiki.com/wiki/Version/1.6.4518 (1.6/Odyssey release notes). Also [CL4528], [CL4535], [CL4543], [CL4566] and [CL4850] at the same URL pattern.

Note: Ludeon's translation repo moved to "1.7.4976–4980" in Oct 2026, so a 1.7 exists or is in testing. Everything below is 1.6.

Notation for cells: N = (0,+1), E = (+1,0), S = (0,−1), W = (−1,0), and NE, NW, SE, SW for the diagonals. `AngleFlat` gives 0° for north and 90° for east (Unity `LookRotation`). [D16 `Verse/IntVec3.cs`, `Verse/GenAdj.cs`] CODE-1.6

---

## 1. Raider pathing and target choice (assault raids)

### 1.1 What an assault raider tries to do, in priority order
`LordToil_AssaultColony` gives every pawn the `AssaultColony` duty. [D16 `Verse.AI.Group/LordToil_AssaultColony.cs`] CODE-1.6. The duty's think tree is [X15 `Core/Defs/DutyDefs/Duties_Misc.xml`] XML-1.5 (not re-checked in 1.6; the C# job givers it names are the 1.6 ones):
1. `JobGiver_TakeCombatEnhancingDrug`, then the `Abilities_Aggressive` subtree.
2. `JobGiver_AIFightEnemies`: **targetAcquireRadius 65, targetKeepRadius 72.**
3. `JobGiver_AITrashColonyClose`
4. `JobGiver_AITrashBuildingsDistant`
5. `JobGiver_AIGotoNearestHostile`
6. `JobGiver_AITrashBuildingsDistant` with `attackAllInert=true` (walls, doors and so on)
7. `JobGiver_AISapper` with `canMineNonMineables=false` (mines natural rock only)

**FightEnemies target acquisition.** [D16 `RimWorld/JobGiver_AIFightEnemy.cs`, `Verse.AI/AttackTargetFinder.cs`] CODE-1.6
- Scan flags are `NeedLOSToPawns | NeedReachableIfCantHitFromMyPos | NeedThreat | NeedAutoTargetable`. **Pawns need line of sight to be acquired. Non-pawn targets such as turrets do not** (`needLOSToAcquireNonPawnTargets` is false for AssaultColony). A powered turret inside 65 cells that the raider can reach is acquired through walls.
- **Ranged raiders:** if any target can be shot from the current cell, they pick one by weighted random on a score. The score is 60 − min(dist, 40), +10 if the target is aiming at this raider, +40 if this raider hit it within the last 300 ticks, −10 × the target's cover block chance, minus non-combatant and downed penalties (downed −50), × `TargetPriorityFactor`. Targets within 30 points of the best are eligible. If nothing can be shot from the current cell, they take the straight-line-closest target that is reachable or shootable.
- **Melee raiders:** they take the closest reachable target by region BFS, limited to **65 cells and 40 regions** (`searchRegionsMax: maxDist > 800 ? -1 : 40`).
- **Turret priority:** `Building_Turret.TargetPriorityFactor => 1f`, the same as a pawn. **There is no special turret preference.** A turret is only an attack target while powered, manned (if mannable), awake and initiated (`Building_Turret.ThreatDisabled`). An unpowered turret is a "furniture" trash target instead. [D16 `RimWorld/Building_Turret.cs`] CODE-1.6
- A ranged raider that already has more than 1% cover against its target and can hit it, or that is within 5 cells and can hit it, stops and shoots (`Wait_Combat`, 450–550 ticks) instead of moving. CODE-1.6

**Where ranged raiders choose to stand** (`CastPositionFinder.CastPositionPreference`). [D16 `Verse.AI/CastPositionFinder.cs`] CODE-1.6
- Candidate cells must be walkable, reachable, within weapon range of the target, and able to hit it.
- Preference = (0.3 + 0.55 × cover block chance against the target) × 0.967^(cells moved) × (0.7 + 0.3 × closeness to the optimal range). The optimal range is about 0.8 × weapon range.
- Multipliers: ×0.5 if within 5 cells of the target; ×0.4 if the cell is farther from the raider than the target is; **×0.4 if the cell contains a PassThroughOnly thing (sandbag, barricade, chunk, fence)**; −1 (never) if the cell is on fire.
- **Avoid grid:** every hostile pawn of a faction with `canUseAvoidGrid` (all humanlike factions; not mechanoids, insects or entities) multiplies the preference by max(0.1, (37.5 − avoidGrid)/37.5). **A cell in one player turret's line of sight is worth only 10%**, even in non-smart raids.
- Mechanoid pawnkinds have `aiAvoidCover` and prefer cells with little surrounding cover. [X15 `Core/Defs/ThingDefs_Races/Races_Mechanoid.xml`] XML-1.5
- **1.5 → 1.6 change:** 1.5 required `c.Standable()` for a firing position, which excludes sandbag, barricade and chunk cells, and penalised PassThroughOnly cells ×0.2. 1.6 requires only `c.WalkableBy()` and uses ×0.4. **This is the code behind the wiki's "as of 1.6 enemies can stop and fire while standing on any object"** and "barricades at a corridor turn no longer stop enemies using the wall as cover". [D15 `Verse/AI/CastPositionFinder.cs` and `RimWorld/JobGiver_AIFightEnemy.cs` vs D16] CODE-1.5/1.6

**Trash job givers** (steps 3, 4 and 6). [D16 `RimWorld/JobGiver_AITrashColonyClose.cs`, `JobGiver_AITrashBuildingsDistant.cs`, `TrashUtility.cs`] CODE-1.6 (same in 1.5)
- **TrashColonyClose:** samples 35 random cells in an 11×11 box around the raider (`CellRect.CenteredOn(pos, 5)`). Any colony edifice there that it may trash and has line of sight to is attacked. Plants are also eligible when the pawn has an ignite verb.
- **TrashBuildingsDistant:** samples 75 random colony buildings anywhere on the map and takes the first that `ShouldTrashBuilding` allows. This means it must be reachable by touch (`CanReach Touch, Danger.Some`), not burning and hostile.
- **Inert buildings are trashed only on a seeded 0.8% roll** (`ChanceHateInertBuilding`). The roll is seeded per building, per pawn and per 3-in-game-hour window. Inert buildings are walls, all doors (`DoorBase isInert`), fences, sandbags, barricades, columns, power conduits and graves [X15 Buildings_*.xml]. **Traps are never trashed.** Mech-faction buildings and dormant buildings are skipped. The `attackAllInert` variant (step 6) skips the 0.8% gate, so walls and doors become normal targets once nothing else is reachable.
- How a building is attacked: 70% of the time the raider uses a building-destroyer verb if it has one (grenades, rocket launchers, thump cannon, incendiary launcher; `ai_IsBuildingDestroyer`). Otherwise 35% ignite if the target is flammable, is not a door and the raider has an ignite verb. Otherwise melee. Trash jobs expire after 450–500 ticks and are re-evaluated.
- **Design consequence (CODE-1.6):** any non-inert colony building that raiders can touch from outside (cooler or vent in the outer wall, lamp, turret, solar panel, crop-side furniture) is a target before "go to nearest colonist". Doors and walls are only rarely chosen (0.8% roll) while something else is reachable.

**GotoNearestHostile** picks the straight-line nearest attack target that is reachable (`CanReach OnCell, Danger.Deadly`), with no distance or path-length limit. [D16 `RimWorld/JobGiver_AIGotoNearestHostile.cs`] CODE-1.6

### 1.2 Path cost model (1.6 pathfinder)
1.6 rewrote the pathfinder as Burst jobs ("Pathfinding will be significantly more accurate, especially at larger distances"). [CL4518] WIKI. Code: [D16 `Verse/PathFinderJob.cs`, `Verse/PathGridJob.cs`, `Verse.AI/PathGrid.cs`, `Verse/PathUtility.cs`, `Verse/PathFinderCostTuning.cs`] CODE-1.6
- A* step cost = cell cost + the pawn's own ticks per move. The default is 13 cardinal / 18 diagonal (`DefaultMoveTicksCardinal = 13`); the real value comes from `pawn.TicksPerMoveCardinal`.
- **One open cell is worth about 13 cost units for a baseline human.**
- Heuristic weight: 1 for colonists; 1.75 for animals; for other AI it rises from 1 at ≤40 cells to 2 at ≥120 cells straight-line distance (rounded to an integer).
- **There is no node-count cut-off and no path-cost cut-off.** The search runs until the open set is empty.
- **1.5 was different:** after 2000 opened nodes, non-colonists switched to region-based pathing; the region heuristic weight ramped 1→5 between 3500 and 4500 nodes; there was a hard `SearchLimit = 160000`; the human heuristic rose to 2.8 at 120 cells. [D15 `Verse/AI/PathFinder.cs`] CODE-1.5
- Cell cost = max(terrain pathCost, the highest thing pathCost in the cell, snow or sand add-on).
  - +45 when stepping between two adjacent closed doors.
  - In the static perceived grid: +1000 if the cell is on fire, +150 per adjacent burning cell.
  - Then: + perceived terrain cost + avoid grid × 8 + 600 (outside the allowed area or fogged, only for pawns with those restrictions) + 300 (danger, when flagged) + 70 (off the lord walk grid) + 400 (own faction's reserved blueprint or frame).
  - Final cost = max(cell, provider cost from doors or traps) + 175 if a pawn is blocking the cell.
- **Things whose pathCost is ≥25 and that have `pathCostIgnoreRepeat` (the C# default is true) cost nothing extra when the previous cell also had such a thing.** A raider pays for the first sandbag, fence or chunk of a contiguous run only. [D16 `Verse.AI/PathGrid.cs`, `Verse/BuildableDef.cs`] CODE-1.6

| Obstacle | What a normal assault raider's pathfinder sees | Source / mark |
|---|---|---|
| Held-open door, or an open door not about to close (`FreePassage`) | 0, passable | `PathUtility.GetDoorCost`, `Building_Door.FreePassage` CODE-1.6 |
| Any currently open colony door | Passable for hostiles (`CanPhysicallyPass`: `Open && p.HostileTo(door)`). A door left open by a passing colonist (auto-close delay 110 ticks) lets raiders slip through. | `Building_Door.cs` CODE-1.6 |
| Closed colony door | **Impassable.** Raiders can't open it: `PawnCanOpen` checks `GenAI.MachinesLike(doorFaction, pawn)`. Their Goto, attack and trash jobs have `canBashDoors=false`, so door-bash pathing is off. | CODE-1.6 |
| Closed door, for jobs with `canBashDoors` (berserk, murderous rage, entity escape, revenant escape) | Flat **300** (`Cost_DoorToBash`), independent of HP | `PathUtility.cs`, `JobGiver_Berserk.cs` CODE-1.6 |
| Forbidden door | **Forbidding means nothing to raiders.** `IsForbidden(Thing, Faction)` returns false for any non-player faction. A forbidden held-open door is free passage for raiders but impassable for colonists, **including drafted colonists** (`IsForbiddenToPass` uses `bypassDraftedCheck:true`). | `RimWorld/ForbidUtility.cs`, `PathUtility.GetDoorCost` CODE-1.6; 1.6.4518 also fixed "pawns walking through open forbidden doors circumstantially" [CL4518] |
| Wall (any impassable building) | Impassable. Normal raiders never weigh breaking a wall against a detour. | `PathGridJob.CellIsPassable` CODE-1.6 |
| Wall, for sapper / exit-map-dig / revenant-escape paths (`PassAllDestroyableThings`, default tuning) | **70 + 0.2 × HP** per wall cell. Natural rock is the same (extra 0). Doors 50 + 0.2 × HP. | `PathFinderCostTuning.DefaultTuning = (70, 0.2, 0, 50, 0.2, 70, 300, 800)` CODE-1.6 (identical in 1.5) |
| Wall, for breach-raid path planning | (20 + 0.015 × HP) from the tuning plus the same again from the breach customizer, so **about 40 + 0.03 × HP**. Natural rock +20, Perlin-noise patches +30, fogged rooms with dormant pawns or hives +600. **1.5 used 10 + 0.01 × HP.** | `Verse.AI/BreachingGrid.cs` (`tweakWallCost`, `tweakWallHpCost`) CODE-1.6/1.5 |
| Wall or door, for hostile shamblers | **75 flat** per player-owned wall or door. Non-player walls and natural rock are impassable. | `RimWorld/JobGiver_ShamblerFight.cs` CODE-1.6 |
| Fence, for a humanlike | pathCost 80; contiguous fences pay once | [X15 Buildings_Structure.xml] XML-1.5; [W:Fence] says 80 for 1.6 |
| Fence or barricade, for roamer (pen) animals | Impassable. Bashable at 300 if the job has `canBashFences`, which manhunter melee jobs set. | `PathUtility.GetBuildingCost`, `RaceProperties.FenceBlocked => Roamer` CODE-1.6 |
| Sandbags, barricade | pathCost **42**, ignore-repeat. A barricade is `isFence`, so it blocks roamers like a fence. | XML-1.5; [W:Barricade], [W:Sandbags] give 42 for 1.6 |
| Chunk | 42, ignore-repeat. Tree 42, no ignore-repeat. | XML-1.5 |
| Trap, pawn does not know it | **0**: it plans straight over it | `Building_Trap.PathFindCostFor` CODE-1.6 |
| Trap, pawn knows it | **+800** path-find cost and +40 walking ticks | CODE-1.6 |
| Shallow water, ocean shallow, moving shallow, marsh | pathCost 30 + **perceived +180 for undrafted pawns** (+18 when drafted). Raiders are undrafted, so they see about 210 per water cell, the same as roughly 16 extra open cells. | `Verse/PerceptualSource.cs`, `PathGridJob.CostForCell` CODE-1.6; values XML-1.5 (`WaterBase` `extraNonDraftedPerceivedPathCost`) |
| Chest-deep moving water | 42 + 180 | XML-1.5 |
| Deep water | Impassable (pathCost 300). Pawns with `waterCellCost` (swimmers, some genes; flying pawns count 1) use that instead. | XML-1.5; `Pawn.WaterCellCost` CODE-1.6 |
| Mud, marshy soil, soft sand, ice | 14; sand 4 | XML-1.5 |

**Movement slowdown while crossing** (ticks to enter a cell). [D16 `Verse.AI/Pawn_PathFollower.CostToMoveIntoCell`] CODE-1.6
- Ticks = ticks/move + `PathGrid.CalculatedCostAt` + edifice `PathWalkCostFor` (known trap +40). Capped at 450. Doubled when walking, ×0.75 when sprinting.
- For a baseline human (about 13 ticks): sandbag, barricade or chunk ≈ 55 ticks (about 4.2× a normal step); fence ≈ 93 ticks (about 7×); shallow water ≈ 43 ticks.
- Only the first object of a contiguous run is paid, so **alternate object, gap, object** to slow raiders the most. The wiki says the same. [W:Defense structures] WIKI
- Darkness: humanlike move speed ×0.8 at 0% light, rising linearly to ×1.0 at 30%. Raiders are affected; mechs, insects and animals are not. [X15 `Core/Defs/Stats/Stats_Pawns_General.xml` StatPart_Glow] XML-1.5; [W:Light] WIKI

### 1.3 "Too long" detours and when walls get bashed
- **CODE-1.6:** normal assault raiders never compare a detour with breaking a wall. Walls and closed doors are simply impassable in their pathing. As long as one walkable route exists, they take it however long it is. Reachability is region-based and binary.
- Raiders attack walls or doors only in these cases:
  - (a) Nothing is reachable. Step 6 then picks random touchable colony buildings, walls included. The wiki's "random section of accessible wall" matches.
  - (b) The seeded 0.8% "hate inert building" roll comes up while trashing.
  - (c) A door or wall blocks their very next path cell, for example a door closed in their face. Then `Pawn_PathFollower.TryEnterNextPathCell` makes a bash job if the pawn is hostile to the building (`MakeBashBlockerJob`). [D16 `Verse.AI/Pawn_PathFollower.cs`] CODE-1.6
  - (d) Special raid types: see §7.
- **No "too long" cut-off was found in 1.6 code.** The wiki line "Don't make your entryway excessively long, otherwise raiders will think it's not worth it" [W:Defense structures] has no matching mechanism. Classify it as ANEC/legacy. Mechanisms that could produce the observed effect:
  - Melee target search stops at 40 regions / 65 cells. Long winding corridors made of many small regions can hide the target.
  - FightEnemies only acquires targets within 65 cells. Beyond that, raiders fall through to the trash givers, which come *before* GotoNearestHostile, and wander off to trash random reachable non-inert buildings.
  - More time spent outside means more trash rolls.
  - 1.5's region-based heuristic made long paths sloppy.
- Raiders give up and leave after **26,000–38,000 ticks** (≈10.4–15.2 in-game hours; sappers and breachers 33,000–38,000). They leave "satisfied" once damage dealt to the colony exceeds max(25–35% of the colony's starting "health total", 900). [D16 `RimWorld/LordJob_AssaultColony.cs`, `Trigger_FractionColonyDamageTaken.cs`] CODE-1.6 (same in 1.5)

### 1.4 Smart raids and the avoid grid
- `AvoidGrid.PrintAvoidGridAroundTurret` adds **+45 per player turret** to every walkable cell that is within range + 4 (and outside min range) and has line of sight to it. Only `Building_TurretGun` with `ai_combatDangerous` counts; mortars and foam turrets are not combat-dangerous. Cap 255. The value is also copied into adjacent edifices. In path cost it is ×8 (`AvoidGridTuning.PathCostFactor = 8`), so **+360 per turret per cell (about 28 open cells)**. [D16 `RimWorld/AvoidGrid.cs`, `RimWorld/AvoidGridTuning.cs`] CODE-1.6 (same in 1.5)
- Who uses it for pathing: lord toils with `useAvoidGrid`, which means smart immediate attack, sapper and smart-breach raids. Exit-map and panic-flee toils always use it. Mechanoid, insect and entity factions never do (`canUseAvoidGrid=false`). [D16 `RimWorld/PawnUtility.TryGetAvoidGrid`, `LordJob_AssaultColony.cs`, `RaidStrategyWorker_ImmediateAttackSmart.cs`] CODE-1.6; [X15 FactionDefs] XML-1.5
- **The avoid grid is a cost, never a block.** If the killbox is the only route, smart raiders still walk it. "If there is no path… around turret line of sight… they will walk through". [W:Raider] WIKI

---

## 2. Leaning and shooting around corners (`ShootLeanUtility`)

Code: [D16 `Verse/ShootLeanUtility.cs`, `Verse/Verb.cs`, `Verse/CoverUtility.cs`, `RimWorld/ShotReport.cs`] CODE-1.6. The lean logic is **identical in 1.5** [D15]. "Blocked" means the cell can't be seen over: an edifice with Fillage Full (walls, closed doors, natural rock). **Open doors can be seen over** and give 0 cover.

**Lean source cells for a pawn at P shooting at T:**
- **Lean E** if E is open and either (N blocked, NE open, T in the north half) or (S blocked, SE open, T in the south half).
- **Lean W** if W is open and either (N blocked, NW open, T north) or (S blocked, SW open, T south).
- **Lean S** if S is open and either (W blocked, SW open, T in the west half) or (E blocked, SE open, T east).
- **Lean N** if N is open and either (W blocked, NW open, T west) or (E blocked, NE open, T east).
- P itself is a source if it can be seen over (always, for a pawn).
- **Also every cardinal neighbour that holds non-blocking cover (sandbag, barricade, chunk, and so on) and faces T's half-plane.** The pawn effectively shoots "from" the sandbag cell in front of it.

**Consequences:**
- **Pawn directly behind a continuous wall** (N, NE and NW all walls): no lean, no line of sight, **cannot shoot through it.** The corner rule needs the matching diagonal open.
- **Pawn behind a single wall block or at a wall end** (N wall, NE open, E open): leans to E and shoots past the corner at targets in the north half.
- **Pawn behind a wall corner next to a 1-wide gap.** Example: P at (0,0), walls at (0,1) and (2,1), gap at (1,1), E (1,0) open. P leans to (1,0) and shoots straight through the gap. Enemies straight out in front of the gap are about 6° off P's north, so P's own N wall covers it at **75%**.
- **Turrets never lean.** Non-pawn casters shoot only from their own footprint cells (any of the 4 cells of a 2×2 turret). [D16 `Verse/Verb.TryFindShootLineFromTo`] CODE-1.6
- **Range** is measured from the shooter's real cell. Leaning does not add range.
- **Incoming fire against a leaning pawn:**
  - Shooters can hit any of the pawn's lean cells (`CalcShootableCellsOf` → `LeanShootingSourcesFromTo` for pawn targets). A pawn at a corner is always targetable through its lean cells, not only while it fires.
  - **Cover is computed from the pawn's real cell** (`CalculateOverallBlockChance(target.Cell, caster.Position)`), so **the wall counts fully** (75% × angle factor). The wiki's "lean out… wall not provide its full cover" is only the angle falloff.
- **Cover formula**, per adjacent cell holding cover:
  - Base: Fillage Full = 0.75, otherwise fillPercent (sandbag or barricade 0.55, chunk 0.5, fence 0.25, tree 0.25).
  - × 1 / 0.8 / 0.6 / 0.4 / 0.2 for angle difference < 15 / 27 / 40 / 52 / 65°, and nothing at ≥ 65°. Diagonal cover cells use angle × 1.75.
  - × 0.3333 if the shooter is < 1.9 cells from that cover cell, × 0.66666 if < 2.9.
  - Multiple covers combine as 1 − Π(1 − b).
  - A shooter standing on the cover cell itself is ignored for that cell.
  - CODE-1.6; matches [W:Cover]
- **Wall–sandbag–wall firing slot** (walls at NE and NW, sandbag at N, pawn at P). Computed with the formula above; CODE-derived:
  - **Arc:** through the sandbag lean source, about **±63°** (lines leave row N before hitting the flanking walls when |dx/dz| < 2). From P's own cell alone the arc is only about ±27°.
  - **Pawn's cover** against a distant enemy at angle θ from straight ahead: 55% at 0°, 62% at 10°, 61% at 20°, 73% at 30°, 80% at 45°, 64% at 60°.
  - **Plain three-sandbag line, for comparison:** 55% at 0° and 65% at 45°. **A single wall corner:** 75% under 15°, 60% at 20°, 45% at 30°, 30% at 45°.
  - So the slot beats sandbags at oblique angles and ties head-on. A corner lean is best when enemies come from nearly straight ahead past the corner.
- **Projectile interception.** [D16 `Verse/Projectile.CheckForFreeIntercept`, `Verse/VerbUtility.InterceptChanceFactorFromDistance`] CODE-1.6
  - Nothing within 5 cells of the shooter intercepts a shot. Interception ramps from 0 at 5 cells to full at 12 cells.
  - A full-fill wall or closed door in the path always stops the shot.
  - Pawns: 40% × body size, ×0.1 if lying down; friendly pawns also × the difficulty's friendly-fire factor. Objects with fill > 0.2: 15% × fill, or 100% × fill when adjacent to the target. Open doors: 5%.
  - So colonists or sandbags within 5 cells in front of a shooter or turret don't eat its shots. Melee blockers 5–12 cells in front do.

---

## 3. Embrasures, barricades, fences, sandbags

- **There is no vanilla embrasure in 1.6.**
  - The full 1.6 def lists of Core, Royalty, Ideology, Biotech, Anomaly and Odyssey contain no embrasure or loophole building. `Buildings_Security` holds Barricade, Sandbags, the IED traps, TrapSpike, HunterDroneTrap and WaspDroneTrap. [T16 `*/DefInjected/ThingDef/`] DEFLIST-1.6
  - The wiki has no page for it (API search for "embrasure" returns nothing). WIKI
  - Embrasures come only from mods.
  - Closest vanilla analogues: the wall–sandbag–wall slot (§2), and Odyssey's **gravship shield generator**. It is a 3×3, 200 W building on gravship substructure within 19 cells of the grav engine. It projects a 25-cell-radius shield of 500 HP that lasts 100 s (2.4 h), with a 4 h cooldown. It blocks incoming bullets *and* mortars while "those inside can still shoot out". Max 2 per gravship. [W:Gravship shield generator] WIKI; [T16 Odyssey Buildings_Gravship] DEFLIST-1.6
- **Barricade:** PassThroughOnly; any pawn can walk over it (pathCost 42); cover 0.55; HP 300 × stuff; `isFence`, so pen (roamer) animals can't cross it. In 1.6 raiders may stand on it and fire from it (§1.1). [W:Barricade] WIKI; XML-1.5; CODE-1.6
- **Sandbags:** the same 42 / 0.55 / 300 HP, not a fence, textile only, nonflammable. [W:Sandbags] WIKI; XML-1.5
- **Fence:** PassThroughOnly; humanlike pathCost 80 (ignore-repeat); cover 0.25; HP 40; `ai_neverTrashThis`, so raiders never deliberately trash fences. Roamers treat fences, barricades and fence gates as impassable and can't open fence gates (`roamerCanOpen`) unless the job may bash fences (manhunters: bash cost 300). Non-roamer animals (predators, most wild and hostile animals) and insects pass fences like humans. **Flying pawns (1.6) pass over fences and never trigger traps.** [X15 Buildings_Structure.xml] XML-1.5; [D16 `Verse/RaceProperties.cs`, `Building_Door.PawnCanOpen`, `Building_Trap.Tick` (`Flying: false`)] CODE-1.6; [CL4518] WIKI
- **"Vault" and slow numbers:** see the table in §1.2. Raiders do **not** avoid sandbag, fence or barricade cells strongly. 42 or 80 is only about 3–6 open cells of detour, so a 1-wide corridor full of alternating sandbags will still be used if it is the shortest route.

---

## 4. Accuracy by distance and weapon ranges

**Hit chance.** [D16 `RimWorld/ShotReport.cs`, `Verse/VerbProperties.GetHitChanceFactor`, `Verse/ShootTuning.cs`] CODE-1.6
- Hit chance = clamp01(Aim × (1 − cover)).
- Aim = max(0.0201, shooterAcc^distance × [pawn range factors] × weaponAcc(d) × weather × 0.7 if blind smoke on the line, + darkness offset) × target size × posture.
  - Shooter accuracy per cell: pawns use `ShootingAccuracyPawn` by Shooting skill (0 → 0.89, 4 → 0.94, 8 → 0.96, 10 → 0.97, 14 → 0.98, 20 → 0.99) [X15 Stats_Pawns_Combat.xml]. Turrets use `ShootingAccuracyTurret`: mini-turret, autocannon and rocketswarm 0.96; uranium slug 0.98; foam 1.0 [X15 Buildings_Security_Turrets.xml]. The pawn range factors (`ShootingAccuracyFactor_Touch…Long`, used by e.g. the nearsighted gene) were applied wrongly in 1.5 and were fixed in 1.6.4518/4543 [CL4518] [CL4543].
  - Weapon accuracy: Touch at ≤ 3 cells, then linear to Short at 12, to Medium at 25, to Long at 40, flat beyond. Clamped to 0.01–1.
  - Target size: pawn body size clamped to 0.5–2; buildings use fill × footprint × 2.5.
  - Posture: ×0.5 if lying down and ≥ 4.5 cells away.
  - Darkness offset: added only with Ideology, from `ShootingAccuracy{Outdoors,Indoors}{Lit,Dark}Offset`. These default to 0 and come from precepts or genes, so **plain darkness does not change accuracy**. "Dark" outdoors means sky glow ≤ 0.35. [D16 `DarknessCombatUtility.cs`] CODE-1.6
- Shooter factor for a Shooting-8 pawn or a 0.96 turret: 0.885 at 3 cells, 0.61 at 12, 0.36 at 25, 0.20 at 40. For Shooting 14 or the uranium turret (0.98): 0.94, 0.78, 0.60, 0.45. CODE-derived

**Weapon table.** "r" is range (and min range, where there is one); accuracy is Touch/Short/Medium/Long. Values are from [X15] XML-1.5 with 1.6.4518 changes applied from [CL4518] and the wiki weapon pages. Rows marked W come from 1.6 wiki infoboxes.

| Weapon (who uses it) | r (min) | Acc T/S/M/L | Notes |
|---|---|---|---|
| Short bow (tribal) | 22.9 | .75/.65/.45/.25 | |
| Recurve bow | 25.9 | .70/.78/.65/.35 | |
| Greatbow | 29.9 | .65/.85/.75/.50 | |
| Pila (tribal) | 18.9 | **.80**/.71/.50/.32 | 1.6: touch .50→.80, cooldown 4→2.5 s |
| Autopistol / Revolver | 25.9 / 25.9 | .80/.70/.40/.30 · .80/.75/**.55/.40** | Revolver medium/long buffed in 1.6 |
| Machine pistol | 19.9 | .90/.65/.35/.15 | |
| Pump shotgun / Chain shotgun | 15.9 / 12.9 | .80/.87/.77/.64 · .57/.64/.55/.45 | |
| Heavy SMG | 22.9 | .85/.65/.35/.20 | |
| Assault rifle | 30.9 | .60/.70/.65/.55 | |
| Bolt-action rifle | 36.9 | .65/.80/.90/.80 | |
| Sniper rifle | 44.9 | .50/.70/**.88/.90** | 1.6: cooldown 2.3→1.5 s |
| LMG | 25.9 | .40/.48/.35/.26 | 1.6: damage 11→12, cooldown 1.8→1.6 |
| Minigun (also centipede) | 30.9 | **.20**/.25/.25/.18 | 1.6: cooldown 2.3→1.5 |
| Charge rifle | **27.9** | .55/.64/.55/.45 | 1.6: 25.9→27.9, damage 15→16 |
| Charge lance (lancer) | **32.9** | .65/.85/.85/.75 | 1.6: 29.9→32.9; outranges the mini-turret (28.9) |
| Needle gun (pikeman) | 44.9 | .60/.80/.90/.85 | |
| Needle launcher (legionary) | 24.9 | .60/.80/.90/.85 | |
| Heavy charge blaster (centipede) | 26.9 | .18/.26/.26/.18 | 24-shot burst |
| Inferno cannon (centipede) | 26.9 | – | Flame, radius 2.4 |
| Beam graser (tesseron) | 24.9 (3.9) | .60/.70/.65/.55 | 1.6: also craftable |
| Mini-flameblaster (scorcher) | 4.9 | – | |
| Mini-shotgun (militor) | 12.9 | .80/.87/.70/.55 | |
| Thump cannon (termite) | 24.9 | .80/.87/.77/.64 | Thump radius 1.9; heavy wall damage |
| Charge blaster turret (centurion, war queen) | 44.9 | .60/.80/.90/.85 | |
| Hellsphere cannon (diabolus) | 18.9 (5.9) | 1/1/1/1 | Vaporize radius 4.9 |
| Toxic needle gun (apocriton) | 44.9 | .60/.80/.90/.92 | |
| Spiner (war urchin) | 6.9 | – | |
| Beam repeater (Odyssey cyclops) | 21.9 W | .65/.72/.65/.60 W | 4 s warmup, 30-shot burst, pierces shields |
| Gorehulk spine launch (Anomaly) | 17.9 W | – | Gorehulks kite |
| Hellcat rifle / Nerve spiker / Incinerator (Anomaly) | 26.9 / 29.9 / 15.9 (5.9) | .60/.70/.65/.55 · .70/.78/.65/.35 · – | |
| Frag / EMP / molotov / tox grenades | 12.9 | – | Radius 1.9 / 3.5 / 1.1 / 1.9; forced miss 1.9 |
| Incendiary / EMP / smoke / tox launcher | 23.9 | – | Radius 1.1 / 1.1 / 2.4 / 1.9 |
| Triple rocket / doomsday launcher | 35.9 | – | Radius 3.9 ×3 / 7.8; one use |
| Marine grenadier armor / phoenix armor (Royalty) | 12.9 / 14.9 | – | Bomb 1.9 / flame 2.9 |
| Psychic shock / insanity lance | 41.9 (shard 16.9) | – | |

---

## 5. Turrets

Values: [X15 Buildings_Security_Turrets.xml, Royalty Buildings_Mech_Turrets.xml, Anomaly Buildings_Misc.xml] XML-1.5, cross-checked against the 1.6 wiki infoboxes, which agree. Explosion behaviour: [D16 `RimWorld/CompExplosive.cs`] CODE-1.6.

| Turret | Range (min) | HP | Size | Power | Explosion when destroyed | Manning | Notes |
|---|---|---|---|---|---|---|---|
| Mini-turret | 28.9 | 100 × metal stuff factor (steel 100, plasteel 280) | 1×1 | 80 W | 3.9 Bomb; 50% of turrets never explode | Auto | Accuracy .77/.70/.45/.24, burst 2, cooldown 4.8 s, 60 steel barrel; minifiable |
| Autocannon | 32.9 (**8.9**) | 380 | 2×2 | 150 W | 5.9; 50% never | Auto | 27 damage ×3, accuracy .28/.72/.66/.50 |
| Uranium slug | 45.9 (**11.9**) | 380 | 2×2 | 150 W | 5.9; 50% never | Auto | 55 damage, accuracy .22/.33/.44/.95, turret accuracy 0.98; cooldown 3.2 s in XML-1.5 vs 240 ticks on the wiki (unresolved) |
| Foam turret | 19.9 | 100 | 1×1 | 80 W | Damageless foam burst, 4-cell radius; never explodes from damage | **Activated by a pawn** | 10 charges, 3600-tick cooldown; "raiders do not consider foam turrets priority targets" [W:Foam turret] |
| Rocketswarm launcher | 29.9 | 200 | 2×2 | 150 W | 6.9; 80% never | **Activated and aimed by a pawn** | 12 rockets × 24 Bomb, radius 2.9, miss radius 5.75; 180 chemfuel per reload, 7200-tick cooldown, fires only when full |
| Mortar | 500 (**29.9**) | 180 × stuff | 2×2 | – | 4.9; always | **Must be manned** | Forced miss 9 × the operator's mortar miss factor |
| Tactical turret (Anomaly turret pack) | 19.9 | 100 | 1×1 | Battery | 3.9; 50% never | Auto | Thrown up to 22.9 cells; can cause friendly fire |
| Mech cluster (Royalty, enemy): auto mini-turret / auto charge blaster / auto inferno / auto-mortar | 45.9 / 45.9 (3.9) / 45.9 (3.9) / 500 (29.9) | 100 / 180 / 180 / 260 | 1×1 / 2×2 / 2×2 / 2×2 | – | 3.9 / 5.9 / 5.9 / 4.9 | – | Dormant until woken |
| Ancient defender turret (Odyssey, ruins) | 36.9 W | 175 W | 1×1 | Battery | – | – | Not player-buildable |

- **Explosion rules:**
  - When damage would kill the turret, it detonates immediately.
  - At ≤ 20% HP it lights a 240-tick wick; stun or EMP damage stops the wick.
  - "Never explode" is a per-turret seeded roll (`chanceNeverExplodeFromDamage` 0.5 for most; 0.8 rocketswarm; 0 for the mortar).
  - The wiki's "≥ 3 cells between turrets" rule therefore understates it: the blast radius is 3.9 (mini) or 5.9 (2×2). CODE-1.6
- **Do raiders target turrets preferentially? No**, beyond normal scoring (factor 1, the same as pawns). Two things make turrets attract fire anyway:
  - They are acquirable without line of sight through walls (§1.1).
  - Humanlike raiders avoid *standing* in turret line of sight (×0.1 per covered cell) and smart raids avoid *walking* in it (§1.4).
  - **Manhunters** go for a turret they can see and reach within 70 cells whenever no pawn is reachable. [D16 `RimWorld/JobGiver_Manhunter.FindTurretTarget`] CODE-1.6
- **Smoke:** blind smoke on the line gives ×0.7 aim, and target acquisition is blocked by smoke for scans with `LOSBlockableByGas`. [W:Smokepop pack] notes a smoke user nullifies killbox turrets. CODE-1.6 / WIKI
- **Biotech** adds no player turrets. **Odyssey** adds the gravship shield generator (§3), plus enemy hunter and wasp traps and sentry drones.

---

## 6. Traps

Code: [D16 `RimWorld/Building_Trap.cs`, `Building_TrapDamager.cs`, `Building_TrapExplosive.cs`, `Building_TrapReleaseEntity.cs`, `Building_TrapReleaseHunter.cs`, `Building_TrapReleaseWasp.cs`, `CompHunterDrone.cs`, `PlaceWorker_NeverAdjacentTrap.cs`] CODE-1.6

**Spring chance per step onto the trap:**
- 1.0 × trap `TrapSpringChance` × pawn `PawnTrapSpringChance`, unless the pawn's kind is `immuneToTraps` (0) or the pawn knows the trap.
- **Nimble trait: ×0.1.** [X15 Traits_Singular.xml] XML-1.5
- If the pawn knows the trap:
  - Same faction (colonists, colony animals): 0.5%.
  - Any other faction that knows it (allies; **raiders who know it**): **0%**.
  - Factionless peaceful animal: 20% × the trap's peaceful-animal factor (IEDs 0.25, giving 5%).
  - Factionless humanlike (wild man): 30%.
- Flying pawns never trigger traps; this is new in 1.6 [CL4518].

**Who knows a trap** (`KnowsOfTrap`):
- Non-hostile factions; calm wild animals; released guests; non-prisoners hosted by the trap's faction; humanlikes forming a caravan; factionless humanlikes.
- **New in 1.6:** a member of a lord whose job is `ILordAvoidTraps` knows the trap when `Rand.ValueAsync(hash(faction loadID, trap ID)) < AvoidTrapRatio`. `LordJob_AssaultColony.AvoidTrapRatio` is **0.3 when `useAvoidGridSmart`**, i.e. smart raids, sapper raids and smart breach raids, and 0 otherwise.
  - The roll is fixed per faction and trap, so the *same* 30% of traps are known to every raid of that faction.
  - **Raiders never learn traps by seeing one spring.** There is no such state.
  - 1.5 had no lord clause [D15 `RimWorld/Building_Trap.cs`], so smart raiders knew nothing in 1.5 ("Fix: Smart raiders now correctly avoid some traps" [CL4518]). The wiki's Raider page still says smart raids "do not avoid traps" (outdated); [W:Template:Trap Note] has the correct 30%.
- Known traps: +800 path-find cost and +40 walking ticks, and `IsDangerousFor` is true. Unknown traps: 0.

**Adjacency:** `PlaceWorker_NeverAdjacentTrap` checks `OccupiedRect.ExpandedBy(1)`, a 3×3 box. **Traps cannot be orthogonally *or diagonally* adjacent** to any trap, blueprint or frame of a trap. It is on spike traps, all IEDs and the hunter trap. [X15 Buildings_Security.xml] XML-1.5; hunter-trap description [T16] DEFLIST-1.6

**Spike trap:**
- 5 Stab hits on the triggering pawn.
- Per hit = TrapMeleeDamage 100 × stuff sharp multiplier × rand(0.8–1.2, rolled once) / 5; AP = per-hit damage × 0.015.
- HP 40; destroyed on spring; auto-rearm places a blueprint.
- Sources: [W:Spike trap] WIKI; `Building_TrapDamager` CODE-1.6; XML-1.5

**IED traps:**
- Spring starts a **15-tick wick**; any Bullet damage also starts it; HP 40.
- An explosion that deals ≥ remaining HP detonates other IEDs **instantly**. The HE IED does 50 Bomb in **3.9**, which kills 40-HP IEDs, so **IEDs inside 3.9 of an HE IED chain**.
- Radii: HE 3.9 (Bomb 50); incendiary 3.9 (Flame 10); EMP **10.9** (stuns mechs and turrets 25 s, breaks shields); smoke 8.6; firefoam 9.9; antigrain warhead 14.9 (BombSuper); tox (Biotech) 8.9 gas; deadlife (Anomaly) 8.9 in 1.5, **reduced in 1.6.4518 to an unknown value** [W:IED deadlife trap].
- Non-drafted pawns flee a lit wick. [W:IED trap] WIKI
- Sources: XML-1.5, CODE-1.6, WIKI

**Odyssey hunter trap and wasp trap** (`Building_TrapReleaseEntity`):
- **Proximity trigger, not touch** (`SpringChance` 0). Every 60 ticks the trap checks for any hostile, non-invisible pawn within **20 cells *with line of sight***. Player traps ignore prisoners and downed pawns.
- On trigger it spawns its drone(s) with the trigger pawn as the target and is consumed. Destroying it also springs it unless it is stunned, so EMP it first.
- **Hunter:** one hunter drone.
  - Lights a 120-tick wick when an enemy is within 1.5 cells (region scan through doors) and explodes for Bomb (comp default 50 damage, radius 1.9; def values unverified).
  - Detonates instantly when killed.
  - Player-buildable: research "hunter drones", 40 steel + 40 chemfuel, HP 50, can't be adjacent to other traps [W:Hunter trap]. Drones path through doors without breaking them [W:Hunter drone].
- **Wasp:** three wasp drones (fast flyers, about 16 c/s) whose stings stun about 3 s. **Not player-buildable**; found in Odyssey ruins. [W:Wasp trap] [W:Wasp drone]
- Sources: CODE-1.6; WIKI; DEFLIST-1.6

**Traps are immune to trashing** (`TrashUtility.ShouldTrashBuilding` returns false for `isTrap`). Revenants and sightstealers never trigger traps. [W:Template:Trap Note] WIKI; CODE-1.6

---

## 7. Raid types and how a fixed defense fares

Raid strategies and weights: [X15 Storyteller/RaidStrategies_*.xml] XML-1.5. Sieges can happen from 500 points in 1.6, down from 700 [CL4518]. Arrival mix per [W:Raider]: about 45% edge walk-in, 15% edge groups, 30% drop pods, 10% haywire pods.

- **Edge assault (immediate, or stage-then-attack):** follows §1 exactly.
  - The killbox works if it is the **only walkable route**, and if **nothing non-inert is touchable from outside except at the killbox**.
  - Keep turrets or defenders within 65 cells of the approach so FightEnemies stays in charge.
  - **Smart** variant: avoids turret line of sight while pathing (§1.4) and knows 30% of traps (§6). It still walks the killbox if no other route exists.
- **Drop pods:**
  - *Center drop:* 40% chance to land on a powered orbital trade beacon (non-mech factions only). Otherwise near a random free colonist, within about 10 cells. Pods may punch thin roofs but **never thick (mountain) roofs**; mechanoids can't roof-punch or land indoors. Pods open after 520 ticks. If no spot is found, the raid falls back to edge drop.
  - *Edge drop:* lands at the map edge and then acts like an edge assault.
  - *Random/scatter drop:* lands all over the map.
  - The perimeter killbox is bypassed by center and scatter drops, so interior fallback positions are needed.
  - Sources: [D16 `RimWorld/PawnsArrivalModeWorker_CenterDrop.cs`, `DropCellFinder.CanPhysicallyDropInto`] CODE-1.6
- **Sappers** (smart, ≥ 700 points, ≥ 2 pawns):
  - Destination is `GenAI.RandomRaidDest`: a random **owned, constructed bed** (colonist, prisoner or animal; sleeping spots excluded) reachable through destroyable things. Fallbacks: a cell next to a random non-inert colony building, then a colonist, then any cell.
  - Path is `PassAllDestroyableThings` with default tuning: wall or rock **70 + 0.2 × HP** per cell, avoid grid on.
  - Example costs: wood wall 109 ≈ 8 open cells, steel wall 130 ≈ 10, granite-block wall 172 ≈ 13, plasteel wall 238 ≈ 18, natural granite (900 HP) 250 ≈ 19, ore (1500 HP) 370 ≈ 28, uranium ore 870, plasteel ore 1670.
  - They therefore **dig the cheapest line toward a bedroom while dodging turret line of sight** (+360 per turret per cell). They also prefer player walls over thick natural rock and ore. The wiki notes they avoid ore and turret range.
  - The best miner digs; a raider with a building-destroyer weapon blasts instead. Escorts stay within 15–19 cells (ranged) or 23–26 cells (melee).
  - On arrival within 10 cells of the destination, in the same room, they become normal assaulters.
  - **Defense:** turret coverage over the likely dig lines toward bedrooms; interior chokepoints; putting bedrooms deep inside. A perimeter killbox alone doesn't stop them.
  - Sources: [D16 `RimWorld/LordToil_AssaultColonySappers.cs`, `JobGiver_AISapper.cs`, `DigUtility.cs`, `Verse.AI/GenAI.RandomRaidDest`] CODE-1.6
- **Breachers** (≥ 700 points, edge walk-in only, normal or smart):
  - The same random-owned-bed destination.
  - The breach path is planned once, with walls costing only about 40 + 0.03 × HP (granite-block wall ≈ 55, about 4 open cells), so the line is **nearly straight**. Perlin noise adds randomness; fogged rooms with dormant threats are avoided; the smart variant uses the avoid grid.
  - The path is widened by a breach radius of 1–2 (1 below 120 raiders), giving a 3–5 wide corridor. Every player or factionless impassable building or door, and any rock, on it is destroyed.
  - Breach tools: tribal **breach axes**, pirate/outlander/empire **frag grenades**, mech **termites** with thump cannons (one shot kills wood walls, 2 shots stone, 3 uranium, 4 plasteel [W:Termite]). Ranged breachers fire from ≤ 12 cells.
  - Escorts follow the breachers.
  - **Defense:** fixed killboxes are ignored. Turrets and defense lines must cover the direction of approach toward the bedrooms, with layered interior walls and fallback cover. EMP or melee stops termites.
  - Sources: [D16 `RimWorld/LordToil_AssaultColonyBreaching.cs`, `BreachingUtility.cs`, `Verse.AI/BreachingGrid.cs`] CODE-1.6; [W:Raider] WIKI
- **Siege** (≥ 500 points in 1.6, ≥ 4 pawns):
  - The camp is placed at a spot at least 70 cells from colonists and combat buildings, stepping down to 60, 50 … 20, then trying 100, then 10.
  - They build 2 mortars (range 500, min 29.9) and sandbags.
  - They switch to a normal assault after 1.5–3 days, on an 8% chance per pawn harmed, at 30% pawns lost, or when they run out of builders or artillery.
  - **Defense:** own mortars, or a sortie. The killbox only matters after they switch to the assault.
  - Sources: [D16 `RimWorld/LordJob_Siege.cs`, `RCellFinder.FindSiegePositionFrom`] CODE-1.6
- **Mech cluster** (Royalty):
  - Lands dormant. It wakes on damage, on construction within 2 cells, through a proximity activator (12.9 cells) or through a countdown activator (0.01–20 days, mean 4.45).
  - Turrets reach 45.9 cells; mech high- and low-shields block mortars and bullets.
  - Awake mechs mostly stay near the cluster.
  - **Defense:** it doesn't come to the killbox. Use mortars or an EMP-backed assault.
  - Source: [W:Mech cluster] WIKI
- **Mechanoid raids:**
  - The same assault AI as humans, but never the avoid grid, so never "smart". They never flee (`autoFlee` applies only to humanlike factions), prefer open cells, and don't seek cover.
  - Since 1.5 they can emerge from water (shallow or chest-deep), from about 1000 points [X15 `EmergeFromWater` curve].
  - Lancers (32.9) and pikemen (44.9) outrange the mini-turret. **Defense:** a killbox with a bent entry that forces close range; EMP IEDs (10.9 radius) at the entry.
  - Sources: CODE-1.6; WIKI
- **Manhunter packs:**
  - They target pawns at any distance (search radius 9999, unlimited regions) and **follow any corridor length**.
  - They melee if the target is reachable without bashing. Otherwise they attack a turret they can see and reach within 70 cells; otherwise they mill around near the last door on the path.
  - **They bash a door only if their target escapes behind it mid-chase** (`attackDoorIfTargetLost`). Pen animals bash fences (300).
  - Never smart, never flee; traps work at 100%.
  - Sources: [D16 `RimWorld/JobGiver_Manhunter.cs`] CODE-1.6
- **Infestation:**
  - Spawns under overhead mountain within 30 cells of a colony structure, at temperatures above −17 °C; it prefers dark spots far from unroofed cells.
  - Insects dig random walls when idle. 1.6 fixed insects fleeing from gunshots.
  - A perimeter killbox is useless against it. Use interior chokepoints, light and no deep dark roofed areas.
  - Sources: [W:Infestation] WIKI; [CL4518]
- **Termite breach mechs** (Biotech): see Breachers.
- **Shamblers** (Anomaly):
  - They attack only once a pawn is within sight, unless they come as a raid. Acquisition radius 20, keep 40.
  - They path through player walls and doors at **75 flat each** (≈ 6 open cells), so they bash straight through any wall when the detour is longer. They can't dig natural rock.
  - **Defense:** melee and fire at their entry point; a mountain wall stops them.
  - Sources: [D16 `RimWorld/JobGiver_ShamblerFight.cs`] CODE-1.6
- **Fleshbeasts:**
  - Pit-burrow attacks spawn burrows **anywhere ≥ 10 cells from the map edge, preferring outdoors and not under buildings, with no distance limit to the colony**. They can appear inside the perimeter.
  - Pit gates send waves about every 14 days. Large fleshbeasts split, and ranged spikes are short and inaccurate.
  - **Defense:** area weapons and interior melee lines, not the perimeter.
  - Sources: [D16 `RimWorld/IncidentWorker_FleshbeastAttack.cs`] CODE-1.6; [W:Fleshbeasts] [W:Pit gate] WIKI
- **Gorehulk:** spine launch 17.9, kites. It doesn't pursue around corners or behind doors, so bent corridors and corner ambushes work. [W:Gorehulk] WIKI
- **Devourer:** leap and swallow. It can leap from sandbags and barricades since 1.6.4518, and smashes buildings, walls and doors if no pawns are targetable. **Defense:** a slow stone door as a bait gate, fire to force a regurgitation, tox gas. [W:Devourer] WIKI
- **Noctol** (unnatural darkness): light above 30% gives up to ×0.5 move speed and a +200% melee cooldown. **Defense:** flood-lit killbox and interior. [W:Noctol] WIKI
- **Revenant:** invisible; opens doors on the way to its target and mines through walls when trapped; **immune to traps**; EMP stuns it. A fixed defense is irrelevant. [W:Revenant] WIKI
- **Chimera:** stalk and attack modes, rage speed ×1.75. Behaves like manhunters, **does not attack structures and waits outside a walled colony**. Melee blocking at a door with grenades behind works. [W:Chimera] WIKI
- **Sightstealer:** invisible, never triggers traps; otherwise behaves like manhunters. [W:Sightstealer] WIKI
- **Pit gate:** spawns fleshbeast waves (above). Tunnelers: the Biotech mech "tunneler" is a labor or summon-only mech, not a raid digger. [W:Tunneler] WIKI
- **Odyssey extras:**
  - Gravship maps can be attacked by "pursuing mechanoids", two big raids per gravship map [CL4566], and the mechhive raids normally or with breach and water-emerge raids.
  - Odyssey ruins contain hunter and wasp traps, defender turrets and sentry drones.
  - Sources: [W:Mechanoid hive] WIKI

---

## 8. Other 1.5/1.6 changes and mechanics that matter for killboxes

- **Raiders stand on PassThroughOnly objects in 1.6** (§1.1). A sandbag "T" at a corridor exit no longer denies the corner position. Firing from such cells is only ×0.4 preferred, so it still discourages them somewhat. CODE-1.5/1.6
- **Smart, sapper and smart-breach raiders know 30% of traps** (new in 1.6, §6).
- **Pathfinder rewrite** (§1.2): no region fallback and no node limit; long mazes are still found. [CL4518], CODE
- **Breach path wall cost raised** from 10 + 0.01 × HP to 20 + 0.015 × HP in 1.6. Breach paths are still nearly straight. CODE-1.5/1.6
- **Fleeing:** humanlike (`autoFlee`) lords flee once pawns lost violently ≥ a seeded 40–70% of all pawns ever in the lord (`attackersDownPercentageRangeForAutoFlee`). Fleeing uses the avoid grid. Mechs, insects and entities never flee. [D16 `Verse.AI.Group/Lord.cs`, `Trigger_FractionPawnsLost.cs`, `RimWorld/FactionDef.cs`] CODE-1.6
- **Give-up and satisfaction timers:** see §1.3.
- **Darkness:**
  - Humanlike move speed ×0.8 at 0% light (an unlit killbox slows human raiders and your colonists equally).
  - No base accuracy penalty; Ideology precepts and genes can add offsets.
  - Unnatural darkness (Anomaly) hurts colonists in unlit cells and makes colonist pathing avoid darkness (heuristic 1.5). Raiders are not hurt.
  - Sources: XML-1.5 / CODE-1.6 / [W:Unnatural darkness]
- **Water moats:** undrafted pawns perceive +180 per water cell (§1.2), so raiders strongly avoid wading if a dry route exists. Drafted colonists see only +18. CODE-1.6 / XML-1.5
- **Fire:** pathing avoids burning cells (+1000) and cells next to fire (+150). A burning killbox floor diverts raiders. CODE-1.6
- **Doors:** 1.6.4518 fixed explosions passing through closed doors and made doors block wind. 1.6.4566 fixed "Raiders pathing directly through doors to trash newly blocked things". [CL4518] [CL4566] WIKI
- **Corners (Odyssey diagonal walls):** 1.6.4535 fixed "corner punching turrets" and enemies setting fire through corners. Re-test diagonal-wall turret placements. [CL4535] WIKI
- **Weapon buffs in 1.6.4518:** charge lance 33 (lancer combat power raised to 190), charge rifle 28, sniper and minigun faster (§4). This matters for turret range versus mech range. [CL4518] WIKI
- **Flying pawns** (1.6: birds, wasp drones, some Odyssey creatures) ignore fences, walls of fences and traps. [CL4518] WIKI
- **"Raiders attacking junk" fixed** in 1.6.4518 (ancient junk no longer baits raiders). [CL4518] WIKI

## Open questions (not settled)

1. The exact 1.6 Data values for sandbag, fence and barricade pathCost and HP, turret stats, IED radii and terrain perceived costs. No 1.6 Data mirror was found. The 1.5 values above match the 1.6 wiki infoboxes where the wiki has them.
2. The new 1.6 deadlife IED radius (the wiki doesn't know it either).
3. Uranium slug turret cooldown: 3.2 s in XML-1.5 vs 240 ticks on the wiki.
4. Whether the `AssaultColony` DutyDef think tree, with radii 65/72 and node order, is unchanged in 1.6 XML. The C# is 1.6; the XML seen is 1.5.
5. The "entryway too long → raiders go elsewhere" wiki claim: no code mechanism exists in 1.6. Needs an in-game test of a very long (more than 40-region) maze with melee raiders.
6. The hunter drone's actual explosion damage and radius in the 1.6 def (the comp defaults are 50 Bomb, 1.9).
7. Odyssey diagonal-wall interaction with `ShootLeanUtility`, which only considers cardinal and diagonal *cells*. How corner walls affect line of sight and cover was not checked.
