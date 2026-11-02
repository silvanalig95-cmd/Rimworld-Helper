# Plan ahead: the planning helper (spec)

The player's request (1 November 2026): they feel overwhelmed balancing many parts of the game at once and want help
with what needs planning ahead (construction, production chains, the base's growth), not with reacting to events.
They are early or mid game (their save: day ~96, 9 colonists, Hospitality guests). Binding:

- **Suggest only.** Nothing acts by itself. Each suggestion has a button the player presses to carry it out, "Not this"
  (it stops being suggested), and Undo after it was done. "Act by itself" may come later, once the player has tested
  that the suggestions are consistent and helpful, so build every suggestion as a discrete step that can be applied
  and undone and that keeps its reason.
- **From the live game, never fixed advice.** Every suggestion is computed from what is on the map now and what the
  colony really used lately; it shows its reason in numbers ("2 meals in store; 17 eat here, about 27 meals' worth a
  day: 0.1 days"), is ordered by urgency (days until something runs short), disappears by itself when its reason is
  gone, and after the player applied it, checks whether it worked (the stock rising, the zone sown) and if not says
  what is in the way rather than suggesting more of the same.
- **Measure food in nutrition, and storage in cells per nutrition, never in item counts.** Some crops (Vanilla Cooking
  Expanded's chickpeas) give little nutrition per item and fill storage; a big pile is not a big food stock.
- **Soft integrations; don't duplicate the player's mods.** Colony Manager Fork (hunting, forestry, livestock,
  foraging, mining; its production manager is switched off), Better Workbench Management (linked bills, counted extra
  products), Everybody Gets One (per-colonist bill targets: leave those bills alone), Smart Farming, Food Alert, Better
  Pawn Control (writes work priorities when its presets switch), Deep Storage and RimFridge (denser storage).
- Player rules from CLAUDE.md apply (only unlocked content, words match the map, no countdowns in text).

## The model (shared by every part)

A reader of the live colony (`ColonyNeeds.For(map)`, cached hourly, built on what exists: `Making` stock reading,
`ColonyUse`/`UseLog` measured use, `Chains`), with its arithmetic in a pure, tested class:

- **Who eats here**: colonists and slaves, prisoners, guests who eat the colony's food (Hospitality), and the colony's
  animals that eat stored feed. Each one's daily nutrition from its own hunger (`Need_Food` fall per tick x ticks a
  day), so genes, pregnancy, life stage and the like count.
- **What there is**: nutrition in store by kind (meals ready, raw food humans eat, animal feed), with the storage cells
  each takes (stacks by the def's stack limit).
- **What is used**: per day, from the record of use when it covers a day (`ColonyUse`), else estimated from the eaters.
- **What the land gives**: per growing zone, its crop's nutrition per cell per day (harvest yield x the product's
  nutrition / grow days, adjusted by the zone's soil fertility and the plant's fertility sensitivity), and the growing
  days left this year from the map's temperatures by twelfth.
- **What the benches and people can do**: per bill, the work per batch and who can do it (work type enabled, skill
  needed), so time and hands are known.

## Phase 1: production (first)

1. **Stock targets**: one row per good the colony relies on (meals; raw food for the season ahead; medicine; spare
   clothes; feed for the animals; the materials queued blueprints need), each with have, use a day, days left and a
   target = use a day x the days chosen (settings), limited by storage and by spoiling (meals without a freezer keep a
   day or two). Status: short, low, enough, more than needed (and the storage it takes).
2. **Bill changes as one-click suggestions** on the player's own bills: raise or lower a "Do until X", add a missing
   bill with what feeds it (reuse `Making.SetUpChain`), move work to a second bench. Skip Everybody Gets One's bills.
3. **Why it's stuck**: for a bill that isn't moving, name the reason (ingredients missing or out of its radius; nobody
   free with the work enabled and the skill; the bench unpowered, unfuelled, broken or forbidden; nowhere to store the
   product) with a fix where one exists. Also used to check an applied suggestion that didn't work.
4. **Field plan**: crop cells from the nutrition the colony needs a day (measured) against what its zones give a day,
   plus a reserve for the days without growing; suggest growing a zone onto neighbouring fertile cells, a new zone, or
   switching cells from a crop the colony has far more of than it uses (by nutrition and storage), shown on the map.

Where: the Making page gets a section at the top ("Ahead"): the stock rows and the suggestions, each with its numbers,
Do it / Show / Not this, and a short log of what was done with Undo and whether it worked.

### Phase 1 as built

- **The model**: `Building/ColonyNeeds.cs`, `ColonyNeeds.For(map, force = false)` (cached per home map for an hour; a
  forced read after a step). Fields the next phases use:
  - who eats: `colonists`, `slaves`, `prisoners`, `guests` (Hospitality, `Strangers.IsHospitalityGuest`), `feedAnimals`
    (can't graze, or nothing grows: `PlantUtility.GrowthSeasonNow` for grass), `grazers`; `humanNeed`, `feedNeed`
    (nutrition a day, each from `Need_Food.FoodFallPerTickAssumingCategory(Fed) x 60000`);
  - what is used: `record` (`ColonyUse.Read`), `measured`, `measuredDays`, `plantShare` (plants' share of the raw food
    used), `medicineEstimate`;
  - what there is: `stock` (`Making.Stock`), `goods` (`AheadGood`: kind meal/raw/feed/medicine/clothes/material, count,
    nutrition each, stack limit, storage cells, rot days, frozen share, used a day, short for building), `defs`
    (defName to ThingDef), `freezer` (a store below 0 that takes meals), `mealRotDays`, `mealRoom`, `HaveOf(kind)`;
  - the land: `fields` (`AheadField` per growing zone: crop, product, food, cells, sown, nutrition a cell a day at its
    soil, its own growth factor for each of the next 60 days, free fertile cells beside it, product stock and use),
    `zones` (key to Zone_Growing), `crops` (food crops the colony can sow now), `plants`, `dayTemps`, `growFactors`,
    `growingLeftThisYear`, `dayOfYear`, `newGround`;
  - benches and bills: `bills` (`AheadBill`: meal, medicine and feed bills: mode, target, suspended, `managed` by another
    mod, BWM `linked` count, what its bench can make a day, a free bench), `billOf`, `freeBenchOf`, `canAdd`/`addOf`;
    `ColonyNeeds.WorkTypeFor(bench, recipe)` and `ColonyNeeds.Able(pawn, bill, work)`;
  - `Input(mealDays, reserveDays, declined)` gives the planner's plain input.
- **The arithmetic and the planner**: `Building/AheadMath.cs` (pure, `Tools/Tests/AheadTests.cs`, which also plans the
  reference colony above end to end). `AheadMath.Plan(AheadInput)` returns `AheadPlan` (rows, ideas, field numbers).
- **The steps**: `Building/Ahead.cs` (`Ahead.For(map)` makes each idea a `Suggestion`; `Ahead.Open(map)`),
  `Building/AheadStuck.cs` (`BillStuck.Why`, `FieldStuck.Why`: the reason and its fix), checks `AheadStockCheck`,
  `AheadSownCheck`, `AheadGoneCheck`. Bill mods: `Integrations/BillMods.cs` (Everybody Gets One's repeat modes
  TD_PersonCount, TD_XPerPerson, TD_WithSurplusIng, and any repeat mode not the game's own, are only read; Better
  Workbench Management's linked set from `ImprovedWorkbenches.ExtendedBillDataStorage.GetBillSetContaining(bill).Bills`
  is changed together, because BWM copies the bill under the mouse onto its whole set).
- **The page**: `UI/AheadPanel.cs` at the top of `UI/MakingPanel.cs`. Settings `aheadMealDays` (2) and
  `aheadReserveDays` (5), Building page, "Planning ahead". Texts in `Languages/English/Keyed/Rimfolk_Ahead.xml`, the
  Almanac page `Council_Alm_C_Ahead` (`Defs/AlmanacArticleDefs/Almanac_Ahead.xml`).
- **Record of use**: `Patch_UseEaten` also counts Hospitality guests eating from the colony's stores (not their packs).
- **When it runs**: `Ahead.Hourly` (from `HourlyPeople`) checks the steps done and works each home map's plan out
  again; the page asks `Ahead.For(map)` (cached until the needs are read again, a setting changes or the tracker's
  `Version` moves). The free ground beside fields is looked for only beside the six best food fields.
- **Paste**: with a working nutrient paste dispenser the meal row goes by the record alone (no hunger floor).
- Not yet: medicine and feed bills are handled like meals. (Done in phase 3: spare clothes bills per garment; hay
  and other animal-only crops kept out of the people's pool.)

## The shared suggestion core

Every source of suggestions (Ahead now; the advisor, the base review, the Plan page's next steps, the defense advice
and the eight automations next) goes through `Building/Suggest/`:

- `Suggestion` (not saved; the source works it out again from the live game whenever asked, so it goes by itself once
  its reason is gone): `key` (stable, "source:what:where"; "Not this" and the log go by it), `source`, `area`,
  `title`, `reason` (with the numbers), `days` (until something runs short; `SuggestMath.NoRush` when it isn't about
  time) or `level` (0 to 4, for sources that rank by level), `map`, `doLabel` and `doTip` (the button's whole words and
  what it does on the map), `apply` (returns a `SuggestDone` with its undo and check, or null when it couldn't; a null
  `apply` is a suggestion that only says why and shows where), `cells` (what Show outlines), `look`.
- `SuggestUndo` (abstract, saved with the step): `Possible(map)`, `Undo(map)`. Ready-made in `SuggestUndos.cs`:
  `UndoBillTarget` (bills' counts), `UndoBillsAdded`, `UndoBillSetting` (suspend, radius, worker),
  `UndoWorkPriority`, `UndoForbid`, `UndoZone` (cells added, zone made, cells moved, sowing, the crop).
- `SuggestCheck` (abstract, saved): `waitTicks` (the wait before the first look and between looks) and
  `Worked(done, out whyNot)`. A step not working at its first look gets a second; then it is Failed with the reason.
- `SuggestTracker` (`CouncilGameComponent.suggest`, saved): `Declined(key)`, `Decline(s, days = 0)` (0: until brought
  back), `BringBack(key)`, `DeclinedFor(source)`, `DeclinedKeys()` (for a planner that gives a set-aside share to
  the next way), `Waiting(key)` (a done step still to be checked isn't offered again), `Open(suggestions)` (without
  those set aside or waiting, the soonest first), `Apply(s)`, `Undo(done)`, `DoneFor(source, map, n)`, `Hourly()`
  (the checks; from `CouncilGameComponent.HourlyPeople` through `Ahead.Hourly`), `Version` (goes up on every change,
  so a page knows to work its list out again).
- `SuggestMath` (pure, tested): the order, `LevelOf(days)`, how long a "Not this" holds, when a check is due, what a
  look makes of a step, how much is kept (`DoneKept` 20, `DeclinedKept` 80).
- To route another source through it: build its suggestions as `Suggestion`s with keys "source:..." in its own
  `For(map)`, draw them with the same three buttons (see `UI/AheadPanel.DrawStep`), and give each `apply` an undo from
  `SuggestUndos.cs` (or a new `SuggestUndo`) and a `SuggestCheck`. A source whose advice isn't a `Suggestion` (an
  advisor card, a defense note) sets aside by key with `Decline(key, title)`; `DeclinedFor`/`DoneFor` take several
  sources (`SuggestMath.OfSource`: "base" takes "base:stubs"). The automations keep their `AutoAction` log, but a new
  action holds the same `SuggestUndo` a step done by hand does (see the last section).

## Phase 2: construction and the base's growth

5. **What the colony will be short of**: per room kind, need against what is built (beds for the people and those
   likely to join, freezer cells for the food stock and the winter reserve, dining seats, hospital beds, workshop room
   when benches are busy), proposed before it is short, at a spot from the ground plan or "find a good spot", shown on
   the map.
6. **Build order**: the next one or two rooms for the colony's size and research go onto the Plan page.
7. **Projects in stages**: clear, then walls/doors/roof, then climate and power, then floors, then furniture; build
   priorities by stage, a stockpile for the materials by the site, what is holding the project up, and never more queued
   than the builders can do in a couple of days.
8. **Room to grow**: keep the side a room will grow into free (the freezer's free wall, the end of the bedroom row) and
   warn before something is built there.

### Phase 2 as built

- **The arithmetic**: `Building/GrowthMath.cs` (pure, `Tools/Tests/GrowthTests.cs`, which also runs the reference colony;
  `RIMFOLK_GROWTH_PRINT=1` prints it). `GrowthInput` (numbers) to `GrowthPlan` (`GrowthRow` per kind: need, soon, built,
  planned, days, level, why; `GrowthIdea` per room to build, ordered by days, then level, then the kind's order). Kinds
  `beds`, `cold`, `dining`, `hospital`, `workshop`, `storage`. Rules: beds for colonists and slaves (lodgers included),
  plus prisoners being recruited "once they join"; cold store places (stack places) for `mealDays` of meals and
  `reserveDays` of raw food, then the winter's days without growing (`WinterIn` = the first day under
  `AheadMath.NoGrowFactor`, the row's days), each by the nutrition a place of the colony's own rotting raw food holds;
  seats for colonists, slaves and guests less `DiningSlack` 1; hospital beds `ceil(people x 0.25)` or the most laid up
  at once lately; workshop bench places = benches + one for each busy bench with no idle twin (`Busy`: queued hours over
  2 days x 8 h), built = benches in a workshop + `SparePlaces` (10 free cells each); store places for 5+ stacks lying
  about. Builders: `BuildHours(work) = work x 2 / (1.7 x 2500)`, `BuilderHoursADay = speeds x 6`, `Fits` = queued + room
  within 2 days (any room when nothing is queued, nothing without builders). Stages `StageOf` (clear, walls+roof,
  climate+power, floors, furniture), `StageMoved`, `RoomGoing`. Room to grow: `KeepDepth`, `GrowSide` (not the door's
  side, most free rows, the far side on a tie, 3 to 6 deep), `Strip`.
- **The reader**: `Building/GrowthNeeds.cs`, `GrowthNeeds.For(map, force)` (hourly, forgotten after a step). Beds:
  `Building_Bed` for humans, not medical/prisoner/crib/Hospitality guest bed (by type name), planned from bed blueprints
  and frames (not inside a hospital being built). Seats: chairs cardinally beside an eating table (planned: chair
  blueprints beside a table or table blueprint). Hospital: medical beds; patients = in a medical bed or
  `HealthAIUtility.ShouldSeekMedicalRest`, the day's most kept 15 days (`BuildAheadTracker.patients`). Cold: Ahead's meal
  and raw rows' use, slot groups in a room at or below 0 °C or a powered fridge (`ColonyNeeds.Fridge`, now public) that
  take the meal or a rotting raw food. Workshop: `WorkshopBench` (the bench's `workTableRoomRole`, else not a stove or
  research bench; spots left out), queued hours of bills still wanted that `BillStuck.Why` lets go ahead. Stores:
  `listerHaulables` at home, not in storage, that some store takes; free places in the non-cold stores. Builders:
  Construction active, `ConstructionSpeed` summed; queued work = every blueprint's `WorkToBuild` and frame's `WorkLeft`.
  Rooms being built add their `adds` as planned (cold, hospital, workshop, storage).
- **The steps**: `Building/BuildAhead.cs`, source `"build"`. `BuildAhead.For(map)` (cached until the needs, the
  tracker's `Version` or the projects' `Version` move). Room steps `build:room:<kind>` (at most
  `GrowthMath.MaxRoomSteps` 2, while they fit the builders' 2 days, else `View.heldBack`): `Pick` takes the kind's
  room type (`Council_RT_Bedroom` standard then cozy, `_Freezer`, `_DiningRoom`, `_Hospital` without the surgery,
  `_Workshop`, `_Storeroom`), the smallest size whose resolved furniture adds what is wanted (else the largest), and
  `TemplatePlanner.FindSpots` (new optional `extraNear`: bedrooms by bedrooms, freezer and dining by the kitchen,
  workshop and storeroom side by side; the rooms followed and their kept sides as `taken`), the first valid spot with no
  field under it that has a side to grow, else the best. Do it re-plans at the spot (else picks again) and runs
  `TemplatePlanner.Execute` as the planner does; `UndoRoomPlaced` cancels its blueprints and frames (by cell and def),
  unmarks what it marked and drops what it queued; `BuildRoomCheck` two days on. A room with no plan the colony can
  build, or no spot, is a step that only says why. Nobody building: `build:nobuilder` with `BillStuck.LetDo`.
- **Projects in stages**: `Building/BuildProjects.cs`. `BuildProject` (saved in `BuildAheadTracker`,
  `CouncilGameComponent.buildAhead`, 16 at most): footprint, kind, adds, the kept strip. `BuildAhead.Placed(plan, map)`
  is called by a step and by `Designator_PlanRoom.Place` (so the player's own rooms are followed and keep a side too).
  `BuildProjects.Read` (pieces left per stage, work left, materials still needed, cutting, mining and taking down marked,
  queued pieces, the roof: marked or not once the walls stand); `BuildProjects.Why` (nobody with Construction, plant
  cutting or mining on, a roof nobody marked, skill, reach, forbidden, materials short with how many, research, the
  builders' other work) with fixes (`LetDo`, mark the roof `UndoRoofMarked`, allow `UndoAllowed`). Steps per project:
  held up `build:held:<id>:<why>`; else the stage at hand to high priority `build:stage:<id>:<stage>` (when build
  priorities are on and other building waits; `UndoBuildPriority`, `BuildStageCheck`), and a stockpile for a material
  needed 40 or more with no store taking it within 14 cells `build:stock:<id>:<def>` (preferred priority; `UndoZone`,
  `BuildStockCheck`). Hourly (`BuildAhead.Hourly` from `Ahead.Hourly`): finished rooms marked done (kept while their
  walls stand, for their strip), cancelled ones forgotten, the patients noted.
- **Room to grow**: the strip is chosen when a room is placed; `build:grow:<id>` warns when blueprints or frames of the
  colony stand in it (Do it cancels the blueprints, `UndoBlueprintsCancelled` places them again; frames stay), and
  `BuildAheadOverlay` (a MapComponent) outlines every kept strip with its room's name while a build, zone or room
  designator is selected, before anything is placed.
- **The page**: `UI/BuildAheadPanel.cs` at the top of the Plan page (`UI/NextStepsPanel.cs`): the table (need, built,
  planned, how it stands; the reason in numbers as its tooltip), the rooms being built with their stage, the held-back
  line, the cards (`AheadPanel.DrawStep`, now public, as are `AheadPanel.Done` and `Aside` with a source), done lately
  and set aside. `Suggestion.when` (new) is the pill's words when a step isn't about days. Texts in
  `Languages/English/Keyed/Rimfolk_AheadBuild.xml`; the Almanac's Ahead page has two new sections.
- **The reference colony** (the fixture's own fill-ins in `GrowthTests.Reference`): beds 9 of 9; nothing kept cold, 38
  places short now (7 for meals, 31 for five days of raw food at 15.1 a day, 2.44 a place), 100 through the 10 days
  without growing ahead (from day 13); 17 eat at 8 seats, 8 short; 2 hospital beds for 9, one short, no rush; the
  workshop and the stores enough. The rooms in order: the cold store, the dining room, then the hospital. Nothing is
  queued, so the freezer is suggested whatever its size; the dining room beside it only while the two together take the
  four builders' 2 days (48 hours at 24 a day), else it waits for the freezer and the page says so.
- Not yet: nothing left from this list. (Done in the "same rules" pass: the advisor's room signals read these rows and
  the plan proposes the room once. Done in phase 3: busy benches by the record of bench hours; mining counted in a
  room's hours; joiners from accepted quests counted; the ground plan keeps clear of kept sides.)

## Phase 3

9. **Colony Manager**: show its hunting, wood and mining targets beside the stock targets; suggest raising one when a
   chain needs more (by reflection, failing quietly; namespace `FluffyManager`, `Manager.For(map)`,
   `JobStack.FullStack<T>()`, `Trigger_Threshold.TargetCount`).
10. **Shopping list**: what the colony needs and can't make (neutroamine, components before Fabrication, steel when
    deposits run low), for the next trader; where to mine.

### Phase 3 as built

- **The arithmetic**: `Building/AheadMoreMath.cs` (pure, `Tools/Tests/AheadMoreTests.cs`, which also runs the
  reference colony with a 600-cell hay field). `ManagerJobInfo` (a job's kind, label, goods in words, target, current,
  slider top, suspended/managed/completed, `raises`, `wanted` and `wantedWhy`), `ManagerState` (jobs, active, the
  Managing work type, managers on and able, desks and working desks), `ShopNeed` (want, have, pending, price, reasons,
  deposit, `managerMines`, `traderHas`), `ClothesKind` (worn, frayed, soon, spare, billCover, forever, wornOutADay,
  canMake, partsInStock, managed). Rules: `RoundTarget` (tens to 200, fifties above), `RaiseTo` (never past the slider's
  `MaxUpperThreshold`), `ShouldRaise`/`ShouldResume`, `Stall` (no desk, desk off, nobody managing, nobody able; only
  while a job is active), `RaiseWorked`; `Gross`/`AddReason` (each reason's use is its "more than stock" plus the stock,
  the uses added, the stock taken off once), `ShopShort`, `Order` (short x price, at most `MaxShopRows` 12),
  `CellsToMark` (at most `MaxMineCells` 40), `MineWorked`; `DaysToFray` (below `FrayedShare` 0.5 at the def's
  `wearPerDay`), `ClothesWanted` (frayed + soon + worn through x days), `ClothesShort`, `ClothesTarget`,
  `ClothesSteps` (not managed, most short first, `MaxClothesSteps` 3).
- **Colony Manager** (`Integrations/ColonyManagerBridge.cs`, reflection only): the map's `FluffyManager.Manager` found
  among `map.components` (never made where there is none), `JobStack.FullStack()`, each job's `Suspended`, `Managed`,
  `Completed`, `Label`; its own `Trigger` read from the job's type down (the jobs hide the base field with `new`);
  `Trigger_Threshold.TargetCount`, `MaxUpperThreshold`, `ThresholdFilter`, `CurrentCount`; `Trigger_PawnKind.CountTargets`,
  `Counts`, `pawnKind`; forestry's `Type` (only Logging keeps a stock); mining's `AllowedMinerals`. Desks are
  `Building_ManagerStation` (a working `Building_AIManager` manages by itself); the work type `Managing`. Anything
  missing logs one message and the bridge stays quiet. A two-second cache serves `Possible` (asked every frame).
  What a job's goods are wanted for (`AheadMore.Manager`): hunting, the raw row's target x the meat share of what was
  eaten (only while the raw row is short or low and the record covers a day); logging, the blueprints' shortfall, the
  chains' parts and the wood the record saw used a day x the reserve days; mining, the shopping list's minerals. Steps:
  `ahead:manager:raise:<kind>:<i>` (`UndoManagerTarget`, only while the target is still the one set;
  `ManagerCountCheck` two days on), `ahead:manager:resume:<kind>:<i>` (`UndoManagerSuspend`), and
  `ahead:manager:<stall>` (no desk: says which desk can be built; desk forbidden: allow, `UndoForbid`; nobody managing:
  `BillStuck.LetDo` the best by intellectual + social; `ManagerStallCheck`).
- **The shopping list** (`AheadMore.Shopping`): the parts down `Making.For(map).chains` with no maker and no plant;
  the blueprints' shortfalls whose material roots no chain (nothing makes it); starved bills whose one missing kind
  nothing makes (batches left, 10 at most, 3 for a bill for ever); the medicine row short or low (its chain by
  `Making.ChainFor` for the shortfall, or the medicine itself when no bench makes it); garments no bench makes. Left
  out: crops, meat, hides, wool, animal products, plant matter, chunks, and whatever a plant on the map or one the
  colony can sow yields (wood). Pending: cells marked to mine, at `YieldPerCell` (the rock's yield, the storyteller's
  mining factor, the best miner's yield where it wastes). Traders: pawns with a `trader` not hostile, and
  `passingShipManager.passingShips` trade ships, each one's `Goods` counted against the list (a line each).
- **Where to mine** (`AheadMore.Mining`, `ahead:mine:<mineral>`, two at most): for a shopping row with a known
  deposit and no active manager mining job for it, `MineSpot` takes the lump (cardinally joined rock of the mineral,
  seen, none of it marked) nearest the colony's middle with a cell beside open ground the colony reaches, and walks
  it from there so each cell can be reached once the one before is mined, skipping a roofed cell with no roof holder
  left within `RoofCollapseUtility.RoofMaxSupportDistance`. Do it adds `Mine` designations (`UndoOrders` cells);
  `AheadMineCheck` two days on (half mined, else nobody mining or how many are left).
- **Spare clothes** (`AheadMore.Clothes`, `ahead:clothes:<def>`): the colony's people's worn apparel that cares
  about damage; spares are ColonyNeeds' clothes goods (whole, in store); the record's worn-through rate. A game-mode
  "do until" bill is raised (`UndoBillTarget`, BWM links together), else a bill added with `hpRange` 50-100%
  (`UndoBillsAdded`); `BillStockCheck` two days on; parts not in stock: a step that only says why.
- **Leftovers**: `AheadField.feedOnly` (the product is `Feed`): `AheadMath.Fields` splits the need into the people's
  (raw x plant share) and the animals'; hay feeds only the animals, the people's fields feed the animals with what is
  left; steps add to the people first; hay over-supply is judged against the animals' need (or as unused).
  `GrowthInput.questJoining` (`GrowthNeeds.QuestJoiners`: ongoing quests' `QuestPart_JoinPlayer` with `joinPlayer`
  and not prisoners, and `QuestPart_PawnsArrive` with `joinPlayer`, humanlike, not babies, not the colony's yet; a
  quest naming no map counts for the first home map) adds to the beds' and seats' "soon". `BuildAhead.KeptGround`
  seeds the ground plan's `taken`.
- **Building ahead, asked later**: busy benches by the record (`GrowthNeeds.WorkedHours`: what each bench made a day
  by `ColonyUse`, in batches of the recipe that makes it there, at the hours a batch takes the hand who did the most
  batches there; `GrowthMath.WorkedHours`, `BusyByRecord` (worked `BusyShare` 0.75 of the 8-hour bench day with as
  much again waiting), `BusyBench` (the record when it covers a day, else the queued-hours rule); `workedADay` and
  `busyMeasured` for the words). Mining in a room's hours (`GrowthMath.MineHours`: `JobDriver_Mine`'s 100 ticks a
  swing, 80 damage to natural rock and 40 to the rest, by the rock's hit points; `MinerHoursADay`; `MineAsBuildHours`
  turns the miners' days into the builders' hours): a pick's `plan.mine` cells (`RoomPick.mineCells`/`mineHours`, in
  its reason) and the `Mine` marks inside the rooms being built (`GrowthInput.queuedMineHours`, in `queuedHours`). The
  Plan page with the next steps switched off draws Building ahead alone (`NextStepsPanel.DrawBuildAheadOnly`) with the
  line that the steps are off; the Ahead section on the Making page never depended on that setting.
- **The page**: `UI/AheadMorePanel.cs`, drawn by `AheadPanel.Draw`: the manager's jobs under the stock rows (with the
  stall in a line), the shopping list under the fields with the traders' lines; the steps are cards among the
  others (source "ahead"). Texts in `Languages/English/Keyed/Rimfolk_AheadMore.xml`; the Almanac's Ahead page has
  sections on spare clothes, Colony Manager and the shopping list.
- Not yet (needs a look in the game): the names read from Colony Manager were checked against its 1.6 source, not a
  running game; a suspended job is only flagged when its goods are wanted; livestock and foraging are shown, never
  raised; breakdowns' components aren't counted in the list.

## Rules of thumb (starting values only, replaced by the colony's own numbers as they're recorded)

About 3 to 4 meals per colonist a day and 3 days of food in stock; about 25 crop cells per colonist on normal soil
(16 to 20 rice in the best case); about 10 freezer cells per colonist, one wall kept free to grow; bedrooms about 25
cells; one vitals monitor covers 8 hospital beds; corridors at least 2 wide on main paths; the kitchen next to the
freezer, the workshop by the store. Sources: the RimWorld wiki's Colony Building Guide, Food, Farming and Intermediate
Midgame Guide pages, and Steam discussions.

## The reference colony (the player's save, day ~96)

9 colonists and about 8 Hospitality guests; 2 cooked meals in store; the stove's meal bills "until 2" (fine) and
"until 5" (simple); raw food: chickpeas 2390 (35 cells), corn 440, potatoes 268, rice 252; steel 31 and components 25
with machining bills that need them; 15 growing zones (wheat x4, healroot 79, drago tree 92, hops 63, tobacco 52 and
others); one of each bench; nothing under construction.

## The same rules for what already exists (the player's direction, 1 November 2026)

Everything the mod already suggests or automates follows these rules too, through the shared suggestion core phase 1
builds (one step type, one "Not this" store, one log with undo and the check whether it worked). What the inventory
found missing, by system:

- **Advisor cards** (Priorities page): no "Not this"; few act on a click; room-growth signals (dining, kitchen,
  freezer, hospital, workshop outgrown) react once a room is already short: give them the live numbers (eaters with
  guests, seats, freezer cells against the food stock target) and let them warn before it is short.
- **Base review findings**: no "Not this"; distances are straight lines between room centres (use walking distance);
  one finding per rule; several kinds (rooms far apart, the long meal walk, the freezer airlock, catch-all and spread
  stores, empty filters) have no fix button.
- **Plan page (next steps)**: a step that was let go comes back; steps carry no numbers or urgency of their own.
- **Making page**: staple bills get fixed targets when set (meals at most 60, medicine at most 30) and are never
  resized; only the first bench of a kind; idle benches only named. Phase 1's stock targets replace the fixed numbers.
- **The 8 automations** (off by default, they act by themselves when switched on): each one's candidate should also
  show as a suggestion the player can carry out by hand; an undone action must not come back after its cooldown (undo
  counts as "Not this" for that key); their reasons carry numbers. Their switches stay as they are (off unless the
  player turns them on); "act by itself" for the new helper is for later, after the player has tested the suggestions.
- **Defense advice and line reviews**: "Not this" on notes; numbers in reasons where they're missing.

### The same rules as built

Shared: `Building/Suggest/SameRules.cs` (`SameRules.StillOpen(map, key)` for "adv:" and "base:" keys, `SameRules.Orders`
runs an order-giving action as a step by diffing blueprints and designations, `OwnerOf`/`SettingsOf` name a store by
"zone:<id>"/"thing:<ThingID>"); undos `UndoOrders` (blueprints placed by cell and def, designations on things and
cells), `UndoStorePriority`, `UndoStoreAllow`, `UndoPlanTaken`, `UndoResearchPick`, `UndoRelay` (LineRoutes.cs); checks
`SourceGoneCheck` (the advice or finding behind a key gone; else the pieces unbuilt, nobody building, the orders
waiting), `BillStockCheck`. `SuggestTracker.Decline(key, title)`, `Offered(key)`, `UndoneLast(key)`,
`DeclinedFor`/`DoneFor` over several sources. UI pieces in `AheadPanel`: `NotThis`, `DoStep`, `DoTip`,
`ButtonsWidth`, and `Done`/`Aside` over several sources.

- **Advisor cards**: done. Keys `AdvisorSteps.KeyOf` ("adv:<def>:<key>", the same the Plan page proposes by), so "Not
  this" on either page holds on both; `CouncilMapComponent.Signals` leaves out what is set aside or waiting for its
  check (every page and the automations go by it), `AllSignals` keeps all. An action that gives orders
  (`AdvisorSteps.ActIsStep`, not `actOpens`) is a step with `UndoOrders` and a `SourceGoneCheck` two days on. Buttons
  as wide as their labels, each with a tooltip; "Done lately" and "Set aside" under the cards. One source of truth
  with building ahead: `RoomGrowth.Wanting(map, kind)` gives GrowthNeeds' row when the colony is short now or soon
  (and not when the player set the room aside); dining outgrown, the plain dining advice, the plain freezer advice,
  freezer outgrown, no medical bed and no bed read their row (`ForBuildAhead`: the row's numbers in the detail,
  `buildKind`, "Go to Plan", and the room step's own Do it on the card), and `NextSteps` leaves signals with a
  `buildKind` out of its proposals. Dining cramped and the eaten-without-a-table advice step aside while the seats
  row wants. The kitchen reads the Ahead meal row (short or low, the meal bills' make a day below the use, no free
  stove: `AdvisorMath.KitchenOutgrown(people, stoves, cooks, mealsShort, behind)`); the hospital upgrade, low food,
  medicine and no doctor got their numbers. Not yet: most other advice texts were left as they were (they mostly carry
  counts in their titles); the workshop upgrade isn't a GrowthMath kind (bench places) and stays its own advice.
- **Base review**: done. `BaseReview.For` leaves out what is set aside or waiting, `All` keeps all; `KeyOf`,
  `StepOf`, `Apply`; `BaseFinding.fix` returns a `SuggestDone`. Rooms far apart and the meal walk go by walking
  (`BaseReviewMath.WalkGrid`, cost 10 straight and 14 on the slant, no corner cut past a wall or door, stops when every
  target is reached; `TooFarWalking`, `MealWalkOf`), the straight line in the words beside it; the meal walk offers a
  dining room. New fixes: the freezer airlock (`BaseReviewMath.AirlockAt`: door, lobby, side walls, the cells left to
  walk round; `UndoOrders`), catch-alls (only those at one priority are a muddle, `StorageReviewMath.CatchAllMuddle`;
  the others go one priority lower, `UndoStorePriority`), spread stores (the room holding the most keeps the kind,
  `UndoStoreAllow`). Take-downs and stockpiles keep `UndoOrders`/`UndoZone` and a `SourceGoneCheck`; the relay of lines
  `UndoRelay`. The empty-filter finding has its cells counted but no fix (an empty store may be meant). Not yet: the
  bench far from its store still measures in a straight line.
- **Plan page**: done. "Not this" (planned or proposed, with a source) declines the key for good; old saves' let-go
  steps are converted; a planned step set aside on its own page leaves the plan. Proposals skip what is set aside or
  waiting. The level pill on planned steps too; the research step counts idle benches and researchers (key
  "plan:research", started through the core with `UndoResearchPick`); a base fix and an advisor action from a step go
  through the core. "Done lately" and "Set aside" for adv/def/base/plan at the end of the page. Button columns grow to
  their longest label.
- **Making page staples**: done. `MakingMath.StapleCount(rowTarget, otherBillsKeep, useADay, unit)`: meals, medicine
  and feed by the Ahead rows (target less the other bills' cover, a day's use at least, rounded up to fives);
  `MakingOption.targetWhy` under the batch and on the button. Not yet: only the first bench of a kind; idle benches
  only named.
- **Automations**: done. Each `AutoPending` has its `stepKey` ("auto:bill:<bench>:<recipe>", "auto:plan:<step>",
  "auto:research:<def>", "auto:firewood", "auto:grow:<def>", or the base finding's or advisor card's own key), its
  `reason`, and `run` (the doing, returning its undo and check) apart from `act` (run, then the log); candidates that
  aren't `Offered` are skipped, and by the automation also those whose last step was undone. `HandSteps(map)` gives
  them as `Suggestion`s, drawn as cards under each switch on the Making page with "Done lately" and "Set aside". A
  new `AutoAction` keeps `key` and the step's `undo`; undoing it declines the key. Log lines carry their numbers (in
  store and wanted, the research cost, pieces and their worth, the wood the trees give).
- **Defense advice and line notes**: done. `DefenseAdvice.For` leaves out what is set aside (`All` keeps all), "Not
  this" on each card and "Set aside" under the advice; the keys "def:<key>" are the Plan page's too. Numbers added to
  wood, traps, turrets, the archers' post, the fallback, sieges, mechanoids and medicine. Notes on a line: "Not this"
  per note (key "line:<id>:<note>"), and a line under the notes brings them back. Not yet: the notes that are plain
  facts (the gate shut, no way on) carry no number.
