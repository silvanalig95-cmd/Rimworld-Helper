# Handoff: work in progress

Last shipped: 0.16.7 "The enemy dead, as the council said" (the player's rule that answers show on the map as
accurately as possible, CLAUDE.md, starting with `Council_Dilemma_EnemyDead`. The player likes the pyre as an event
but wants the crematorium used rather than bodies lying about, so the crematorium is its own answer, unavailable
without a working one (`DilemmaEffect_HaulDead.Unavailable`), and the pyre's haul starts at 14 to 17. PlayOut/PlayDead.cs:
`PlayBurial` (graves/trench: Grave frames in `p.plots` turned `p.plotRot`, FinishFrame and HaulToContainer, graves set
to strangers, leftover frames cancelled when nobody is left to bury), `PlayCremation` and `JobDriver_PlayOutCremate`,
`JobDriver_PlayOutLightPyre`; OnMapTimed's pyre keeps the bodies on it lit (`Flames` every 20 ticks on the viewed map,
`BurnOut` at dawn). Not tried in the game here: the frames' construction and `HaulToContainer` into a stranger-set grave
are the game's own jobs, given directly.)

Before it, 0.16.6 "Families across the years" (the player found Toni 36, her daughter Morgan 31 and Morgan's son
Hector 57 strange; the game builds families by calendar years and allows any order cryptosleep can explain,
`ChildRelationUtility.GetParentAgeFactor` with `MaxPossibleBioAgeAt`. They chose both ideas: `Social/FamilyYearsMath.cs`
(pure, `FamilyYearsTests`: direct line only, an uncle younger than his nephew doesn't count) and `Social/FamilyYears.cs`
(the People roster's "N years in cryptosleep", the story's "Across the years" section, a remark a pair at most every
ten days via `Together.When`, pool `Council_Pool_SayFamilyYears` with [elder]/[younger] and [near]/[grand]).
The player wants more family dynamics (generated families, marriage and births, adoption and the like): a list of
ideas is to be proposed, after a survey of what Rimfolk already does with family.)

Before it, 0.16.5 "Guests and the pyre" (from the player's save: a Hospitality guest with 4/4 friends had a grey
Recruit button. Her record in the save said arrived, not in a mental state, faction with goodwill, so by Hospitality's
own `DialogUtility.DrawRecruitButton` / `GuestUtility.MayRecruitRightNow` the button should have been live. Hospitality's
`CompUtility.guestComps` is a static `Dictionary<Pawn, CompGuest>` never emptied, and `Thing` is `IEquatable<Thing>` by
thingIDNumber and def, so after a load in the same session a pawn is read from the earlier save's comp.
`Guests.ForgetLastGame` (Integrations/ModStrangers.cs) clears it from `CouncilGameComponent`'s constructor and
FinalizeInit. Also the pyre: the answer's outcome rides on the `TimedThing.text` and is told when it burns down, and a
pyre never lit in two days lets its bodies go (`End` unforbids them).)

Next: an audit of the other answers for gaps between their words and the map, to be proposed to the player as a
list; and a list of family ideas (from a survey of what Rimfolk already does with family) for the player to choose from.

Before it, 0.16.4 "A better sales table" (the player asked to replace RimMarket's colony stall sales table, which
they find hard to use: `Integrations/RimMarketStall.cs` finds RimMarket 1.0.6's stall, sale lines (`StallOrder`) and
`ColonyStalls` by name and swaps `RimMarket.UI.Dialog_ColonyStall` for `UI/Window_StallCounter.cs` in a
`WindowStack.Add` prefix; `Integrations/StallCounterMath.cs` is tested in `StallCounterTests`; setting
`marketStallWindow`. RimMarket's 1.6 and 1.7 builds were the same for these types. Not tried in the game here.)
Before it, 0.16.3 "Materials for every build" (from the player's save: Hector, Construction 2 and Mining 3,
mined and handled animals while a hi-tech research bench frame sat at 11 steel for two days and a drug lab blueprint
got nothing. No Rimfolk system held them: no build priority set, no clean sweep, no work party, no ideoligion rule,
no reservation or en-route haul, the doors open, 210 steel and 19 components on the map. The game's
`ItemAvailability.ThingsAvailableAnywhere` caches its bool per tick under (material, faction) without the amount, so
a larger need asked first (most likely the bench's: its full cost under the player's mods isn't in the save) refuses
that material to every other build. Not confirmed in the game: the frame's inspect pane would show the bench's need. `Patches/Patch_MaterialsOnHand.cs`
caches the count on hand instead. The bench itself still needs its whole remaining steel on the map, the game's rule.)
Before it, 0.16.2 "A button for the defenses" (the player asked for the defense window to have its own button in
the bottom bar next to Rimfolk's: `Council_DefenseButton`, `UI/MainTabWindow_Defenses.cs` drawing `DefensePanel`,
setting `defenseButton`; `MainTabWindow_Council.OpenTab("Defenses")` opens it while the button is shown). Before it,
0.16.1 "Decrees when they're needed" (the player asked that a needed, wanted decree, like a clean sweep
after many fights, come again sooner and be forgiven more, without becoming constant). Before it, 0.16.0 "Weather
that changes like weather" (all six dynamic weather options the player chose, biome by biome), 0.15.0 "The weather remembered" (the weather ideas the player chose), 0.14.2 "Into cover"
(the TakeCover mod finds cover spots on Ctrl + right-drag, but in 1.7 letting go gives no order), 0.14.1 "Not everyone
gets along", 0.14.0 "Shaped for the colony" (the defense overhaul) and 0.13.1 "Fungus kept for its meal".

## Decrees when they're needed, as shipped (0.16.1)

- `Decrees/DecreeMath.cs` (pure, `DecreeMathTests`): `Need` (moment strengths, 1 or 2 each, three's worth is full),
  `Want` (love 1, like 0.75, dislike -0.5, hate -0.75 over everyone, +0.3 for a petition), `Understanding` (need x
  (0.4 + 0.6 want), halved once declared twice in the last month), `CooldownTicks` (up to 65% off, never under a day),
  `BacklashFactor` (up to 60% off).
- `Decrees/Decrees.cs`: `DecreeDef.neededWhen` (Clean sweep and Sick bay: RecentThreat); `DecreeTracker.NeedOf` (cached
  250 ticks; moments from requestWhen, orWhen and neededWhen; strong when bad), `ThreatsWithin` from `threatTicks` (the
  game's LastThreatBigTick noted hourly, 8 kept, 15 days). `ReadyAt` is the effective time (stored readyAt stays end +
  full rest, so it shortens and lengthens with the need); `UnrestChance`, `Backlash` and the decree mood's
  `ActiveDecree.Resentment` take `BacklashFactor(understood)` (saved on the decree as enacted).
- `UI/DecreePanel.cs`: a green "Needed now: ..." line on a card on cooldown or recently used.

## Weather that changes like weather, as shipped (0.16.0)

The player chose all six options of the proposal (spells, what follows what, temperature, events, a forecast, the
year), and asked that every part consider the biome and every combination of mods. Harmony, not a hidden condition, so
nothing is added to the map or the save beyond the forecast and the events' pulls, and taking Rimfolk out leaves the
game's own weather.
- `Weather/WeatherDynamicsMath.cs` (pure, `WeatherDynamicsTests`): `WeatherKind` from a weather's traits; the land
  (`BiomeWeather`: rain a year, coldest and warmest month, the biome's wet share, Seasonal Weather's seasons, open
  water; arid, humid, frozen, tropical, continental); `Factor` = spell x follow-on x events x year x season, clamped
  0.3 to 2.5, special weathers 1, a downpour and any rain in a drought never above 1; `Shared` renormalises over the
  weathers that could come so the ordinary ones keep their chance together (the specials keep theirs exactly);
  `TempTarget`/`Ease` (at most 5 °C down, 3 up), `FogTicks`, `ForecastAccuracy`/`ReadsWeather`, `YearOf` (hashed by
  tile and year), `YearWords`/`YearWordsFor`, the events' pulls (`TryParsePull`, `AddPull`). The tests run 100,000
  random combinations over eight lands, focused checks, and a hundred simulated years per land with and without:
  rain shares stay within 0.75 to 1.33 of the game's (measured within about 10% over 300 years and eight seeds), spells
  last longer, dry spells end, specials come as often, a desert stays one, a tropical land gets its wet season.
- `Weather/WeatherDynamics.cs`: `Patch_WeatherChoice` (CurrentWeatherCommonality postfix, factors cached per map and
  tick), `Patch_WeatherForecastHeld` (ChooseNextWeather prefix: the foretold weather if it still can come),
  `Patch_WeatherStarted` (StartNextWeather postfix: the forecast's outcome, a fog's length), `Patch_WeatherTemperature`
  (GameConditionManager.AggregateTemperatureOffset postfix, map managers only). The weather's kind is read from its
  def (lightning, a fog overlay, sand; very good, toxic, monolith or mood weathers are special). Seasonal Weather's four
  lists are read by field name for the land's wet share. Conditions folded in: heat wave and heat dome, cold snap and
  polar front, the warm spell, any condition named Drought, the region's drought, hard winter and floods. The state
  lives in `WeatherBook.dyn` (eased offset at `Sched.RareWeather` 97; the day's hours for a cold front; the forecast
  and pulls saved). Forecasts once per weather, 8 to 2 hours before it ends, by the best Plants reader (6, or 3 at 50);
  stations `Stormproof_WeatherForecaster`, `NCWC_WeatherStation`, `NCWC_WindSock` (powered if they use power).
- Events: `HappeningExtension.weatherAfter` on the long rains, growing spell, thirsty herd, ice fair and high water;
  the hail's end clears the sky for a day. Annals: "It was a wet and warm year." Overview: "Hector expects rain."
- Settings `dynamicWeather`, `weatherTemperature`, `weatherForecast` (Events page, "The weather"; deep hooks).
- Not tried in the game here: worth a look at the log for the four patches applied, a forecast bubble and the
  Overview line, a cold clear night's temperature against the game's, and a few weeks of weather on the player's map.

## Taking cover with a drag, as shipped (0.14.2)

TakeCover (Workshop 3749200746, 1.6 only) transpiles `Selector.HandleMapClicks`, runs its own controller and patches
and unpatches `Selector.SelectorOnGUI` at run time to catch the drag and the release; its release never arrives in the
player's 1.7 (the cause wasn't seen in the 1.7 code: only its reference assemblies are here, where every member it
uses is unchanged). Rimfolk's version leans on the game's own `MultiPawnGotoController` instead, which polls the
mouse button for the release:
- `Defense/CoverDragMath.cs` (pure, `CoverDragTests`): the drag's frame, cell scores (the game's block chance from the
  enemy side x100, -60 for a shooter's cell it can't shoot from, the preferred depth by reach, the place on the line),
  and a greedy pick of colonist and cell with a neighbor penalty and a reach check on the pairs about to be chosen.
- `Defense/CoverDrag.cs`: `Patch_CoverDragStart` (SelectorOnGUI prefix: Ctrl + right press with drafted colonists on
  open ground starts the game's drag and uses up the press, so TakeCover's code never sees it), `Patch_CoverDragCells`
  (RecomputeDestinations prefix), `Patch_CoverDragOrders` (FinalizeInteraction prefix: orders with the cell itself as
  the click, so a drag ending on the map edge never sets exitMapOnArrival), `Patch_CoverDragEnd`, `Patch_CoverDragLabels`.
  The controller's private `start`, `end`, `pawns`, `dests` are read by name (checked unchanged in the 1.7 reference).
- Setting `coverDrag` (Interface page, needs the deep hooks). Not tried in the game here: worth a look at the cover
  labels, a drag toward raiders behind a wall, and a Ctrl + right-click with enemies in sight.

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

## The weather round, as shipped (0.15.0)

The player asked for weather ideas 1, 3, 5, 6, 7 and 8, a proposal on more dynamic weather, and soft integrations for
weather mods safe to add to a running save.
- `Weather/WeatherMath.cs` (pure, `WeatherTests`): days, quadrums, records (temperature after 20 days and by 1 °C;
  spells told once when they pass an earlier one, `wetHeld`/`dryHeld`), comparisons with the year before, season names
  (5 °C from the tile's twelfth means, 48 storm hours, 70% wet days, 12 dry days on land with 600 mm or more), the
  first snow across the turn of the year, event requirements ("rain:4/7", "[fall]rain:3/7", "dry:6", "frost:5/7",
  "storm:1"), packing.
- `Weather/WeatherBook.cs`: a MapComponent, hourly at `Sched.Weather` (2185); saves `days` (30) and `quarters` (12) as
  short lines. Remarks (`WeatherTalk.SayOne`), news, the chronicle, a season-compare journal, the annals line (with the
  year's quakes), the Overview Weather card. `HappeningExtension.weather`, checked in `IncidentWorker_Happening.Gates`
  for nine events.
- Hail (`Happenings/Workers_Nature.cs`): two hours coming (`Coming`, `Falling(map)`), the reader's warning
  (`WeatherTalk.SayBy`), a harvest work party from the event's effects; rain forced only inside Rain's temperature
  range; its own rain handed back with `StartNextWeather` when it ends. Warm spell remark in `WeatherTalk`.
- Quirks (`Council_Quirks_Habits.xml`): SnowLover (Snowing), SunSeeker (Sunny, from outdoors practice), RainSleeper
  (RainAtRest); habits SnowFall and Sun (`Immersion/QuirkHabits.cs`).
- `Immersion/WeatherTouches.cs`: breath, fire steam, sauna door steam, the lightning flinch (`Patch_LightningFlinch` on
  `WeatherEventHandler.AddEvent`; `jitterer`, `WeatherEvent.map` and `strikeLoc` by name, checked in the 1.7 reference).
- `Weather/WeatherVisitors.cs`: a trade caravan at its spot with under 8 hours left gets 12 more in a storm or heavy
  snow (its `Trigger_TicksPassed` set back, once per lord, not saved); snowbound missions half again as long.
- `Weather/WeatherDisasters.cs` (Seasonal Weather): `AdvisorWorker_Firebreak` with a mark button; the earthquake's end
  starts a repair/clean/haul party.
- `Integrations/WeatherMods.cs`: Stormproof, Weather is kinda cozy, SS Lightning Rod, Disasters: Tornado, Quiet
  Wonders. The survey and the mods not recommended are in `Modlists/mods/D_NatureAnimals.md`.
- Not tried in the game here: worth a look at the Overview's Weather card after a few weeks, a hailstorm's two hours,
  breath on a cold morning, and a trader caught by a storm.

### More dynamic weather: the proposal (built in 0.16.0, above)
How the game chooses weather (1.6 code): the next weather is rolled only when the current one ends (0.27 to 2.7 days;
storms shorter), by the biome's weights times rainfall and condition factors; nothing carries over from one weather to
the next, weather never changes the temperature, and nothing is known ahead. Seasonal Weather swaps each vanilla
biome's weight list by season. Options, each a postfix bias that leaves Seasonal Weather's lists and its gate alone:
1. Spells that hold: after a few wet days rain is likelier for a while, and a long dry spell breaks with a storm.
2. Weather that follows on: a storm seldom comes out of a clear sky, fog after a cold clear night or rain, snow after a
   cold front.
3. Weather in the temperature: a few degrees cooler under rain on a hot day, colder on clear nights, milder under
   cloud (a hidden map condition's temperature offset).
4. Rimfolk's events moving the real weather: the long rains bring days of rain, a dry spell's events less of it, a
   warm spell clear skies (a hidden condition's weather factor).
5. A forecast: a weather-wise colonist (or a forecaster building from Stormproof or WeatherControl) foretells the next
   weather, which is then held to unless it can no longer come.
6. Years with a character of their own: each year a little wetter or drier, warmer or colder, for the annals.

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
