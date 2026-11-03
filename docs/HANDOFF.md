# Handoff: start here

Read this first; it is the whole state. CLAUDE.md has the binding rules and the check commands. CHANGELOG.md (written
from `Defs/AlmanacArticleDefs/Almanac_WhatsNew.xml`) has every round in the player's words. The sections under
"Older history" at the bottom are kept for reference only.

## State (1 November 2026, later)

- 3 November 2026, the player's rule change (binding): harm is part of the game. Features may hurt pawns (injuries,
  illness, attacks, fights) when flavourful or interesting, at a rate comparable to vanilla's chance of something bad
  happening; never gratuitous, always from something on the map. It replaces "nobody is hurt for flavour" in
  CLAUDE.md, DESIGN.md, docs/briefs/agents.md. Existing features that say "nobody is hurt" describe how they work
  today; they change only when a feature is reworked or the player asks.
- Queued after the current to-dos (the player's request): review every existing mechanic under the harm rule and
  give each its negative side where it fits, preventable or not ("everything should be possible"); propose per
  system first. In ROADMAP.md. The right-click interactions (queued after notables and letters) include a sparring
  bout, insult or provoke, and a duel of honour where a people's ways fit.
- 0.62.0, the player's request (how long the food lasts, choose any item, review the list): the journey planner's
  packing list. Food and feed days pinned at the foot (`JourneyMath`, `JourneyPack.PeoplePerDay`/`FeedNeeds`), the
  picker `UI/JourneyPick.cs` over `JourneyPack.Holdings`, grouped lines with subtotals, typed counts, lines taken off
  (`JourneyLine.removed`) or added (`added`), kept in order; feed and "other" lines load with the caravan's goods
  (`JourneyPack.OwnPack`). Not tried in the game yet: the picker's place, the text boxes, the reload icon, mod ammo and
  tents in its tabs, a departure with an "other" line.
- 0.61.1, the player's report (every colonist "Trapped"): RimWorld of Magic's ShouldHaveNeed prefix (priority 100)
  switches off its unfinished travel need (TM_Travel) for everyone, takes mana and stamina from the undead and keeps
  golems' needs. Harmony skips it when an earlier prefix on that check has answered, so colonists had the need at 0
  (their save) and the golem lost its energy. `Integrations/MagicNeeds.cs`: a Priority.Last postfix applies the
  mod's rules again; `GameComponent_MagicNeeds` takes the need away on the first tick after a load and daily
  (`Sched.DailyMagicNeeds` 1117); setting `magicNeedRules`. Which mod's prefix answers first isn't known (not in
  the uploads).
- 0.61.0, the player's request (prepare for travel, set off at a time, everyone ready together): journeys, an order of
  their own, not a decree (`Journeys/`: `JourneyMath` pure with `JourneyTests`, `Journeys`, `JourneyRun`,
  `JourneyPack`, `JourneyPatches`; planner `UI/JourneyDialog.cs`; from the Decrees page, World page and region map via
  `NeighbourPanel.Sections` and the new `NeighbourPanel.PlaceSections`). Sleep shifted the night before, last hours
  (setting `journeyFreeHours`) free of work: clothes for the forecast, pack, eat, Dubs Bad Hygiene toilet and wash by
  name, gather at the gate; the game's caravan forming at the hour; a not-ready letter (wait, leave without, call off).
  `Sched.RareJourneys` 173. One journey at a time. Not built, to propose: moods for those who love or dislike travel,
  Dubs Bad Hygiene water bottles, several journeys at once, road clothes back to the outfit when home.
- 0.60.1: Talk with's "trading" needs an open trade window (`Outsiders.TradingNow`).
- 0.60.0, right-click interactions with outsiders (`Source/ColonyCouncil/Outsiders/`: provider
  `FloatMenuOptionProvider_RimfolkOutsiders` "Talk with X...", `JobDriver_OutsiderTalk`/`OutsiderMeal`, pure
  `OutsiderMath` with `OutsiderTests`, component `OutsiderBook`; chat, ask about home, meal/drink, gift, good word,
  carry a letter home (`NeighbourLetter.bearer`), teach, bring next time, invite to stay, befriend, court, faith,
  spar, insult, duel (tribes, medieval, empire nobility); patches keep a bout's blows from counting as an attack;
  settings `outsiderTalk`, `outsiderSpar`, `outsiderProvoke`, `outsiderDuels`; `Sched.RareOutsiders` 241). To check
  in game: the option on every kind of outsider, outsiders staying put, the shared meal, a duel and a spar end to end.
- 0.59.1: Hospitality's guest tab capped at the screen; Rimfolk's checklist scrolls (`GuestCheck.MaxTabHeight`).
- 0.59.0, the diplomacy layer's foundation (the player's Crusader Kings request): notables (`World/Notables.cs`,
  `NotableMath.cs`, component `NotableBook`; nearest 4 towns within 40 tiles, head/heir/envoy/trader, faces met who
  went home; world pawns, at most 2 made an hour; opinions of the colony and single colonists, 8 memory lines; age and
  succession) and letters (`World/NeighbourLetters.cs`, `NeighbourJobs.cs`: purposes greetings, condolences, warning,
  news, invitation, help; writer at a table, courier trips with `CourierTrip.mailId`; answers by messenger; comms
  console calls for industrial+ peoples with aides and callbacks; harm: a waylaid messenger 4-15%, a threat at war
  that comes true). Settings `notables`, `neighbourLetters`; Almanac page `Council_Alm_C_Neighbours`; `Sched.Neighbours`
  64, `RareNeighbours` 211. Registers in `NeighbourPanel.Sections` for the region map. "A guide" as help was left out
  (no way for it to play out yet): propose. Next: right-click interactions (task list), then favors, pacts...
- 0.58.0, the player's request (a world map in a window, no screen switch): the region map (`UI/Window_RegionMap.cs`,
  `World/RegionMap.cs`, pure `World/RegionMapMath.cs` with `RegionMapTests`; setting `regionMapRadius`, key
  `Council_Key_RegionMap`, button on the World page). Tiles projected flat at the home tile, drawn once into a
  texture; settlement panel calls `World/NeighbourPanel.Sections` (notables and letters register there). To check in
  game: orientation vs the world view, first-draw time at reach 60, wheel over header/panel, window layering.
- 0.57.1, the player's frozen silk golem (RimWorld of Magic 1.6 DLL decompiled from the player's upload): the pawn had
  no TM_GolemEnergy need, CompGolem.CompTick reads `Energy.CurLevel` unguarded, throws every tick (its comp age stuck at
  1), so the pawn never moved. `Integrations/MagicGolems.cs`: ShouldHaveNeed postfix (Priority.Last) keeps the needs
  TM_Golem.needs lists; a CompTick prefix re-adds a lost energy need (AddOrRemoveNeedsAsAppropriate, else AddNeed) and
  clears `initialized` so ApplyNeeds refills it from the workstation; skips the tick if it can't. Setting
  `magicGolemRepair`. Why the need went missing is unknown (another mod's needs rule is the likeliest).
- 0.57.0, bonds rework, the player's request ("everyone just becomes best friends"; no forced balance, battles keep paying
  off): `Social/BondsMath.cs` (pure, `BondsTests`; Tools/Balance `--social` plays two years of a colony of ten with the
  old and new rules), `Social/Bonds.cs` (`PairBook` saved on the social web as `bonds`, one line per pair: familiarity,
  recent hours, fights side by side, seats lost each way, an awkward spell; `Patch_BondsMemory` TryGainMemory postfix
  reads the game's Insulted/Slighted, HadAngeringFight, HarmedMe, BotchedMySurgery, CheatedOnMe, rebuffs, rejected
  proposals, breakups; `Patch_BondsCouple` on AddDirectRelation), `Social/FightSides.cs` (the 250-tick fight looks, now
  shared by the talk's memories and comradeship, with rescue/tend/guard deeds). Familiarity from hours side by side
  (`SocialLog.Near`, meals and recreation count more), talks and shared memories; friendship needs it
  (`Bonds.FriendsForm`, `FriendCap`). How two get on (`Bonds.Affinity`: game compatibility, faith, views, shared
  passions, kind/abrasive) caps friendships and warm words (`WordsCap` in `Chemistry.Notify_Given`). Comrades only from
  fighting near each other, fading after ten quiet days to `ComradeFloor(battles)`. Grudges keep `SocialTie.cause` and
  `renewedTick` (hold six days, then fade). Seats: everyone who stood (self-vote in the instant vote, entries on the
  map) gets `Council_LostElection` by want (stages 0 felt, 1 mild, 2 bitter), the incumbent `Council_LostOffice`
  (stage 1 bitter), a grudge that grows per loss to the same winner, an abrasive winner's gloat face to face. New
  thoughts `Council_SavedMe`, `Council_GratefulTo`, `Council_JealousOf`. Settings `bondsTakeTime`, `chemistryLimits`,
  `comradesByDanger`, `deedsRemembered`, `seatFeelings`, `loveFeelings`. Old saves: familiarity seeded from days
  together, comrade battles from strength (`SeedBattles`), so ties settle over weeks rather than jump. Bug fixed:
  `QuirkMoments.Fought` always got -1 as the fight's start (read after `ResetCount`).
- 0.56.4: the try hour is any hour alike (no lean); `CampKindDef.arrives` (from~to, across midnight) makes a kind
  likelier at its hours (`CampStoryMath.ArrivalFit`: x3 inside, x0.25 outside) in `Prepare`; returning camps out of
  their hours mostly wait. 0.56.3, the player's wish: camps at any hour. `CampTimer.NextTry` picks each day's try hour by `HourWeight`
  (evening 16-21 most, night often, early morning least; `CampTracker.nextTryTick`, `tryHours` saved; 0.56.4 dropped `HourWeight`); passing reasons
  retry hourly up to `Retries` (23). `rolledDay` replaced.
- 0.56.2, the player's report (never a camp by day 107): CampTimer rolled only at 10, and LetterGap (a question in
  the last 12 hours) shut most mornings (openMornings 1 -> 4 between day 93 and 107). Now the day's one choice
  (`CampTracker.rolledDay`) is made in the first open hour 8..17; passing reasons (threat, weather, hazard, waiting,
  lettergap) retry later that day. `CampTracker.diary`/`diaryCounts`/`diarySince` (saved) log each day's outcome code
  for reviews; World page line (`DiaryLine`) and SelfCheck show it. Read the diary in the player's next save.
- 0.56.1, the player's report: no "lost my seat" for someone voted onto another seat in the same election.
  `Offices.Settle` only records losses (`SeatingRound.lost`/`beaten`); `Finish` tells them once every seat is filled:
  another seat means `Council_MovedSeat` (-2, only if they stood to keep it), `Council_MovedDown` (-3/-4 by
  `Offices.SeatRank`: speaker 2, founding seats 1, the rest 0; `ElectionMath.Move`), or `Council_OtherSeat` (-1/-2)
  for proud losers. `GiveThought` takes an optional stage.
- 0.56.0, the player's request (Smart Farming sowed, cut early and sowed again): fields by the weather
  (`Fields/FieldWeather*.cs`, tested in `FieldWeatherTests`; settings `fieldsByWeather`, `frostHarvestSuggest`; hourly
  slot `Sched.Fields`). Per-zone switch pauses sowing (Prefix on `WorkGiver_GrowerSow.JobOnCell`, restricts only) when a
  crop sown now brings under 60% of a full harvest by an hourly forecast (`OutdoorTemperatureAt` + active conditions +
  Rimfolk's weather push); frost harvest is a suggestion (source `fields`) naming the plants each plant's own cold
  limit would lose (`Plant.LeaflessTemperatureThresh` by reflection; 1.6: min growth temperature −18 to −10). The
  player keeps ReGrowth 2's built-in Smart Farming copy and drops the standalone. To check in game: forecast vs map
  temperature (dev log line), the desert save's winter outlook, the frost card, undo and the check after the frost.
- 0.55.3/0.55.4, the player's request: the health panel shows chronic conditions that can kill or cost a part
  (carcinoma: no lethalSeverity, it has a growth comp and destroys its part) as stable, shrinking or getting worse
  (`HudHealthMath.Shows`, `Grave`, `Lasting`); other chronic conditions stay off.
- 0.55.0, the player's request (colonists stuck at full mood): used to the good life (`Moods/GoodLife.cs`,
  `GoodLifeMath.cs`; since 0.55.1 each good mood group is felt at a share in `Patch_ComfortStandards`, with a note in
  its tooltip and the Mood tab readout, no bad mood of its own: the player found a big flat "-56" line unconvincing;
  since 0.55.2 spread by how ordinary each kind is (`GoodLifeMath.Weight`/`Spread`: lasting moods and memories had
  often lately lose most, new ones least, life goals none; recent counts kept per colonist in memory, not saved;
  since 0.55.3 also by the memory's age, `AgeWeight`, so a wedding lands nearly in full and settles over six days,
  and standards' comfort cut eases the same way, `ComfortFor`); line 45/40/35/30 by standards level, half of the
  first 20 points over it and three quarters of the rest taken back, fading to nothing at the minor-break line; reads the game's
  `SituationalThoughtHandler.cachedThoughts`, present in 1.6 and 1.7) and quiet troubles (`Setbacks/Troubles.cs`,
  Life's ups and downs, from the third content day). Settings `usedToGoodLife` and `quietTroubles`; tested in
  `GoodLifeTests`; `Tools/Balance` models both. To check in game: the "Why" sum against the Needs tab, the drift
  down from 100% on the player's save, the nightmare message for a bad night.
- 0.54.2, the player's request: with Alpha Skills, a drink before work for the drunken passion
  (`Integrations/DrinkBeforeWork.cs`, `DrinkBeforeWorkMath.cs`, `DrinkBeforeWorkTests`; setting `drinkBeforeWork`;
  pool `Rimfolk_Pool_DrinkBeforeWork`). The other conditional passions are in `docs/alphaskills-proposals.md` (P1 to
  P17), nothing built: the player picks.
- 0.54.1, the player's request: answers show hints in words, never numbers, under the answer and on hover
  (`Dilemmas/ImpactHints.cs`, tested in `ImpactHintTests`; setting `impactShows` = Hints (default) / Exact / Off,
  replacing `showImpact`; `DilemmaRecord.impactHint`).
- 0.54.0, the player's request: elections play out (`Offices/Election.cs` saved as `OfficeTracker.election`,
  `ElectionMath.cs` tested in `ElectionTests`, `ElectionPlanks.cs` with `<planks>` on the offices, `OfficesElection.cs`,
  `ElectionGathering.cs` with `LordJob_Election`, `ElectionLetter.cs`, `UI/ElectionPanel.cs` on the Decrees page;
  `Score` = `Leaning` + luck, `CampaignLean`; ranked ballots; setting `electionEvent`). 0.53.5: outdoor gatherings use
  `PlayOut/PlayYard.cs` + `YardMath.cs` (tested) through `PlaySpots.OpenCell`. 0.53.6: `Immersion/MoodBubbleText.cs`
  names the mood beside the game's mood bubble (setting `moodBubbleText`).

- 0.53.1: every source of words said over a head is in `ImmersionTests` (pools collected from all Defs folders, def
  `<lines>`, keyed fallbacks); 336 lines made one sentence. Open, the player's call: the wishes', ambitions' and
  decrees' own lines (about 3,600 of 6,300 are two sentences; they are also letter quotes; a bubble shows whole
  sentences up to 90 characters). Social-log narration is third person and left out of the rule.
- 0.53.2: ailment balance (`Tools/Balance`, 800 runs a setup): well run 9.3%, average 13.9%, neglectful 30.5% of the
  time with an everyday ailment (was 11.7 / 17.2 / 36.6); catching clock `AilmentTracker.EverydayCatchingDays` 35 to
  50 days, mild ailments shorter, bed rest and tending count for more. `docs/balance-report.md` regenerated.
- The player's answers (2 November): letter texts and quotes may have two or more sentences, and they don't want
  everything short (the one-sentence rule is for words said over a head only; the wishes', ambitions' and decrees'
  own lines stay as they are); of the Alpha Genes proposals only P3 (mood hints) and P5 (ailment risks from genes)
  are wanted (both shipped in 0.53.4; `Tools/Balance` doesn't model risk lines); an evening with a single piece of news stays a message (not a letter).
- (Answered, kept for the record) Open questions to the player: the six Alpha Genes proposals P1 to P6 (`docs/alphagenes-proposals.md`; the fixes F1
  to F7 shipped in 0.53.3: `PlayOut/PlaySafety.cs` in PlaySpots.Ok and PlayOuts.CanJoin, a "fasting" tag from the real
  hunger rate, natural armour, the `LikeThought` and `Heard` extensions, comfort offsets per body, no forecast with a
  weather maker; tested in `BodyTests`); whether an evening with only one piece of news should also come as a letter
  (`News.Send` shows a single item as a message, as before 0.44.1).

- 0.53.0, plan ahead phase 3 (details in `docs/plan-ahead.md` "Phase 3 as built"): `Integrations/ColonyManagerBridge.cs`
  (reflection on FluffyManager, not yet run against the real mod), `Building/AheadMore.cs` + `AheadMoreMath.cs` (tested,
  `AheadMoreTests`), `UI/AheadMorePanel.cs`; spare clothes bills; `AheadField.feedOnly`; quest joiners in beds and seats;
  `BuildAhead.KeptGround`; busy benches by recorded hours; mining in a room's hours; Building ahead shown with next
  steps off. Not done: livestock and foraging targets only shown; components for breakdowns not counted.

- 0.52.1, the player's idea: `Integrations/CoveredLooks.cs` patches VEF's `HediffComp_ThoughtEffecter.CompPostTickInterval`
  (by name) so Alpha Genes' AG_Disgust (repulsive gene) and AG_Awe (angelic beauty) auras stay still while the carrier
  wears a FullHead piece plus torso and legs coverage; setting `coveredLooks`; mood hint `Rimfolk_MoodHints_AlphaGenes.xml`.
  The aura reaches everyone within 6 cells through walls (GenRadial, no sight check). Alpha Genes' files were unpacked
  in the session scratchpad; a review of its other genes for Rimfolk interactions goes to `docs/alphagenes-proposals.md`.

- New content from `docs/content-proposals-A.md` and `-B.md` (parallel builders), 0.49.0 to 0.50.1: situations and
  events (`World/Workers_NewSituations.cs`, `Happenings/Workers_NewEvents.cs`, `Strangers/StrangerEffectsNew.cs`);
  ailments (`Rimfolk_Hediffs_Ailments_New.xml`, triggers in `AilmentChecks.Sample`/`AilmentTracker.Notify_Ate`, the
  commonest trimmed to keep the time ill down; the hourly cause mask is full at 32 bits; `Immersion/FirstPot.cs`);
  DLC visitors and regulars (`Visitors/VisitorsMore2.cs`, `VisitorWork2Math.cs`, `GravshipLanding.cs`,
  `Happenings/Workers_Visitors2.cs`); camps (`Camps/CampKindsLand.cs`, `CampLandMath.cs`, `World/Workers_Run.cs`);
  wishes and life moments (`Wishes/WishWorkers_LifeNew.cs`, `PlayOut/PlayPetGrave.cs`, `Lives/LifeNew.cs`,
  `ArcDef.again`). Each builder's "needs a look in the game" list is in its round's commit and the reports; dropped:
  kin taking a colonist away, the spacers' liftoff. 0.50.2: callers (`Visitors/Callers.cs` saved GameComponent `Callers`, `CallerJobs.cs`,
  `CallerMath.cs`, `Happenings/Workers_Callers.cs`). 0.52.0: list B dilemmas and customs (`Rimfolk_Dilemmas_New.xml`, `Rimfolk_Dilemmas_Customs_New.xml`,
  `Dilemmas/DilemmaWorkers_Variety.cs`, `Dilemmas/AnswerShifts.cs` saved component, route walks in
  `PlayOut/PlayRoutes.cs`) and the second balance pass (0 left in it; 58 first-pass dilemmas still flagged). Still
  building: plan ahead phase 3. 0.51.0: the same rules for the existing advisor/base/plan/making/automations/defense (`Building/Suggest/SameRules.cs`;
  keys `adv:`, `base:`, `def:`, `plan:research`, `line:<id>:`, `auto:`; `RoomGrowth.Wanting` reads building ahead's
  rows; `BaseReviewMath.WalkGrid`; `MakingMath.StapleCount`); details in docs/plan-ahead.md "The same rules as built".

- 0.48.0, plan ahead phase 2 (details in `docs/plan-ahead.md` "Phase 2 as built"): `Building/GrowthMath.cs` (pure,
  `GrowthTests`), `GrowthNeeds.cs`, `BuildAhead.cs` (room steps placed through `TemplatePlanner.Execute` at a
  `FindSpots` spot with `extraNear`, `UndoRoomPlaced`), `BuildProjects.cs` (`BuildAheadTracker` as `comp.buildAhead`,
  stages, `Why`), `UI/BuildAheadPanel.cs` at the top of the Plan page, `BuildAheadOverlay` (kept strips while placing).
  Open: the advisor's own "outgrown" signals can repeat these steps (route them through the core in the "same
  rules" pass); busy benches read queued bills, not bench hours; quest joiners aren't read; the ground plan doesn't
  avoid kept strips. Next: the same rules for the existing advisor/base/plan/defense/automations, then phase 3.

- 0.47.0, dilemmas (a parallel agent): `DilemmaEffect.Describe(DilemmaContext, ImpactSheet)` on all effect classes
  (a new effect must override it), `Dilemmas/ImpactMath.cs` (tested, `ImpactTests`), `DiaOption_Impact` +
  `Patch_DiaOptionImpact` (the short line under each answer, the full list on hover), `DilemmaRecord.impact`, setting
  `showImpact`; `Tools/dilemma_balance.py` and `docs/dilemma-balance.md` (points per scale; dominated/easy verdicts;
  `--defs DIR`); rebalanced numbers in the general dilemma files; new `Defs/DilemmaDefs/Rimfolk_Dilemmas_Choices.xml`.
  Second pass still to do: 37 flagged dilemmas in Strangers 13, Seasons 7, Customs 5, Customs2 4, Dilemmas3 5,
  Camps 2, Health 1 (listed in docs/dilemma-balance.md).

- The player approved building the content-variety proposals (new events, illnesses, dilemmas, visitors, camps,
  customs from `docs/content-proposals-A.md` and `-B.md`) without showing them a list first.

- 0.46.0, plan ahead phase 1 (a parallel agent; details in `docs/plan-ahead.md` "Phase 1 as built" and "The shared
  suggestion core"): `Building/AheadMath.cs` (pure, `AheadTests`), `Building/ColonyNeeds.cs` (`ColonyNeeds.For(map)`,
  hourly; fridges by a comp named like "Refrigerator" count as cold storage, for RimFridge), `Building/Suggest/`
  (`Suggestion`, `SuggestTracker` as `comp.suggest`: Not this, log with undo, checks), `Building/Ahead.cs`,
  `AheadStuck.cs`, `Integrations/BillMods.cs` (Everybody Gets One bills read only; Better Workbench Management linked
  bills change together), `UI/AheadPanel.cs` at the top of the Making page, settings `aheadMealDays`/`aheadReserveDays`.
  Not yet: spare clothes bills; hay counts in the food crop pool. Next: phase 2 (construction and expansion), then
  the same rules for the existing advisor, base, plan, defense and automations, then phase 3.

- Content rounds 0.45.0 to 0.45.4 (five parallel agents, text variety: wishes and nuisances, customs with
  `Dilemmas/CustomYearsMath.cs` tags [again]/[years]/[tradition]/[missed] and `Flavor.PickFresh` for openings and
  outcomes, ailments with `AilmentTexts.Pick` and two new ailments (sore elbow, chapped hands), visitors and camps,
  world situations and storyteller events). Known mismatches left for the next batch (words vs map):
  `Council_Evt_StrayBeast` says the branded beast is in the barn, but no animal exists unless "keep it" spawns one
  (fixed in 0.47.1: `HappeningSetting.stray`, spawned in `HappeningWorker_Setting.Begin`, held by `LordJob_StrayBeast`
  and the `Rimfolk_StrayHold` duty, fetched home through `Couriers.Fetch` by `DilemmaEffect_StrayHome`, kept by
  `DilemmaEffect_KeepStray`); `SituationOption` now has `outcomes`/`riskOutcomes` (picked by `Dilemmas/OutcomeMath.cs`). The player then clarified that variety means
  NEW events, illnesses, dilemmas, visitors, camps and customs, not only text: proposals are being gathered in
  `docs/content-proposals-A.md` and `-B.md`, to be shown to the player as a list before building.
- Other spoken pools may be said over heads without being in `ImmersionTests.SpokenPools` (Rimfolk_VisitorLines was;
  21 two-sentence lines were rewritten and the pool added). Worth an audit of every pool passed to Speech.Say.

- The player approved all eighteen items of the to-do list ("like all your suggestions"), then asked for (a) deep
  automation so a complex late game can be "outsourced" (rooms, production chains, base layout, expansion), and (b) a
  content variety, immersion and flavour update (more content, deeper existing mechanics, not new ones), in that order.
  Research for (a) was under way: what Rimfolk already automates, and late-game pain points against the player's own
  management mods (Colony Manager Fork, Better Workbench Management, Allow Tool, Pick Up And Haul, Haul to Stack,
  Smart Medicine, Nice Bill Tab, Deep Storage; the full list is in the save's header). For (b), the player's save
  shows the most repeated things: the shoddy clothes complaint (11), the armor wish (11), the comfortable temperature
  wish (10), the tradition dance thought (9), the own room wish (8), the harvest dance (7); midwinter, midsummer and
  the spoils have two opening texts each, and wishes have one line per temperament.
- 0.44.1: `PlayCook.LetGo` (a visiting cook off the feast's bill ends the bill or stove-refuel job and drops a carried
  ingredient or fuel; a carried thing stays in hand when a job ends). `Weather/RainWash.cs` (hourly from WeatherBook:
  filth with `rainWashes` thins outdoors with chance min(0.25, 0.15 x RainRate) from rain 0.2; setting `rainWashesMore`).
  Colony tools (a parallel agent): `Building/UseLog.cs` (pure, tested) and `Building/ColonyUse.cs` (`use` on the game
  component, five read-only hooks: Thing.Ingested, TendUtility.DoTend, Bill_Production.Notify_IterationCompleted,
  CompRefuelable.Refuel(List), Pawn_ApparelTracker.TakeWearoutDamageForDay by name) feed `Chains.cs` per bench
  (`BenchFactor`, `FindHand`, `ChainMath.BenchSpeed/HandHours/DoingBonus`); `AutomationTracker.SettingsLine` under
  each automation switch; `RoomArrangeMath.Tuning` (aisle, keep) from two rows in the rearrange window (not saved);
  the quiet-day news fold (`NewsQuiet`, setting `quietNewsDays`) was reverted in 0.49.3 at the player's request:
  the evening news is always a letter. Worth a look in game: the Making page after a day
  or two, the settings page with a game loaded, and that TakeWearoutDamageForDay doesn't throw on 1.7.
- 0.44.0, defense (two parallel agents): `UI/DefenseLinesPanel.PlanMenu/Plan` ("Plan this step..." with every plan);
  `DefenseSight` counts turrets as targets and watchers (`TurretSees`, `CanHitTurret`, `Reading.turretCover`), and
  `DefenseShapes.BuildBox/DrawBox` move turrets through `DecoyOffsets`/`BigOffsets` out of the way in's sight;
  `DefenseLineFacts.Turrets/TurretSight/OldLines` and `DefenseLineReview.Sight/OldLines` (a null saved shape, or the
  `DefenseLineMath.OldGateLines` geometry, marks a pre-0.14 gate line); `DefenseShapeSpec.SecondRank` (raids of 3000
  points or more, `DefenseTests.SecondRankChecks`); `Defense/DefenseStepWorkers_Threats.cs` (StepWorker_Emp,
  StepWorker_BaitYard, StepWorker_Mortars, filtered by `DefenseStepWorker.OnLadder`; defs in
  `Defs/DefenseStepDefs/Rimfolk_DefenseSteps_Threats.xml` and `Defs/DefenseTemplateDefs/Rimfolk_DefenseTemplates_Threats.xml`;
  beacon role 'B', template kind "bait", placement "core"). Not tried in game: the menu, killbox previews, the bait
  yard beside a trap corridor, the EMP lane's distance from turrets.
- The player's direction for (a), binding: they are early/mid game (the save is day ~96, 9 colonists), overwhelmed by
  balancing many things; they want help with what needs planning ahead (construction, production chains, base
  expansion), not reacting to events. The helper ONLY SUGGESTS; the player does each step with one click ("act by
  itself" to be reconsidered once they've tested that suggestions are consistent and helpful, so build each suggestion
  as a discrete, undoable step with its reason). Every suggestion must come from the live game state (eaters incl.
  guests, stocks, measured use, bench and worker throughput, season and growing days, freezer space, materials,
  builder hours), show its reason in numbers, order by urgency, vanish when its reason is gone, and check whether the
  last step worked; never fixed advice. "Not this" on everything. Research notes and the save's facts are in the
  session scratchpad (automation/inventory-gaps.md); the save: 2 cooked meals for ~17 eaters (stove bills until 2 and
  5), 2390 chickpeas from 35 cells, steel 31 / components 25 with machining bills waiting, nothing under construction.
- 0.44.2, visitors (two parallel agents): `Visitors/VisitorMusic.cs` (StartPlaying/StopPlaying on a built instrument),
  `Visitors/VisitorHunt.cs` + `VisitorActsMath.Fits`/`GamePrey` (wild, nobody's, unmarked, no predator, deer-sized at
  most, the land's own kinds; `Patch_HuntedBeastHolds` on Pawn_MindState.StartManhunterBecauseOfPawnAction;
  JobDriver_VisitDress), `Visitors/Borne.cs` + `Camps/CampDead.cs` (a real body from `Borne.MakeDead`, generated without
  relations and killed off-map; road mourners carry it off, camp mourners lay it by the lamp; colonists near it get the
  game's observed-corpse thought), `Visitors/VisitorBeds.cs` (Hospitality.Building_GuestBed by name,
  ClaimBedIfNonMedical, given back on any ending), hooks in `Visitors/VisitorActs.cs`. Not confirmed without the game:
  that a singer's StartPlaying keeps sounding outside the game's play job; that Hospitality's bed patch lets
  LayDown through for Rimfolk's visitors.
- Visitors trade (a parallel agent, in 0.44.2): `Integrations/RimMarketSale.cs` (RimMarketStall is
  partial; it copies RimMarket's `ColonyStalls.Sell` step by step by name, `SaleReady` false if anything is missing),
  `Visitors/VisitorTradeMath.cs` (tested), `Visitors/VisitorTrade.cs` (`JobGiver_VisitShop`, `JobDriver_MarketBuy`),
  `Camps/CampMarket.cs` (`CampKindDef.buys`), `Camps/CampMilk.cs` (the drovers' "milk" ask, paid on delivery through
  `CampAsk.price`), setting `marketCampersBuy`. 

## State (1 November 2026)

- 0.43.5, a break the player saw: RimMarket 1.0.13 (BenFD) gave `Building_MarketStall.SetOrder` two more parameters
  (`bool refill, int reserve`) and `StallOrder` the fields `refill` and `reserve`, so Rimfolk's reflection lookup for the
  4-parameter SetOrder failed and its sales table fell back to RimMarket's window. `Integrations/RimMarketStall.cs` now
  takes either form (the 6-parameter one first), reads both fields into `Line`, and `SetLine` keeps a line's refill and
  reserve when only its count or quality changes. `RimMarketStall.Keeps` is true with the new form; then
  `UI/Window_StallCounter.cs` draws a taller card with Refill and Keep home. Every other type and member Rimfolk uses
  in RimMarket is unchanged in 1.0.13 (checked in both its 1.6 and 1.7 builds).
- Shipped and pushed: 0.43.5 (0.43.4 before it), on `claude/gifted-noether-32w12b` (this session's branch) and `claude/charming-pasteur-9qbjle`.
  Tree clean. Every check green at that commit: build (only the CS0108 warning in Decrees.cs), DefValidator 0 errors,
  Tools/Tests "All tests passed", labelcheck, almanaccheck, save_estimate --strict, interaction_audit, playout_audit,
  ApiDiff "Nothing the DLL uses has changed".
- 0.41.1 was the player's request: a finished step of a defense line keeps "Plan it again" (the same plan, or another
  of the step's; `DefenseStepWorker.Choices` and `DistanceFor`, `DefenseLineReview.PlanAgain`, `DefenseLinesPanel.AgainMenu`).
  The outgrown killbox's own button folded into it.
- 0.41.2, also asked: "My animal fought for me" (`Council_Pet_Defended`) lasts 0.17 days, not 12. A loaded
  `Thought_CouncilMemory` keeps no longer a time than its def's (`Causes.cs` ExposeData), so old copies end too.
- 0.41.3, a bug the player saw: the guest checklist (`Integrations/GuestCheck.cs` Draw) overflowed Hospitality's
  fixed-height listing into a second column out of sight, so `CurHeight` read short and the tab never grew. It now
  draws with `listing.maxOneColumn` set.
- 0.41.4, the player's report (silver rush prospectors lay about in the dining room) and their rule that every visit
  plays out its words: `Visitors/VisitorWork.cs` (JobGiver_VisitRest only when tired or at night, on open ground;
  JobGiver_VisitWander off the colony's rooms and fields; JobGiver_VisitWork + JobDriver_VisitWork for a trip's `work`:
  prospect, glean, sing, hunt; JobDefs and the Rimfolk_VisitWork duty in `Defs/Misc/Rimfolk_Visitors.xml`). The vanilla
  JobGiver_GetRest in a plain priority node lies down whenever not fully rested: that was the "lay around" bug for
  every visitor. VisitEffect_Stay has `work`, `inside`, `room` (a roof whatever the weather, the barn first);
  VisitEffect_Come has `work`; water spots go to water clear of the colony (`Visitors.WaterSide`); gatherings take
  `guests` "visitors" (the world event's visit people, late arrivals join). `Tools/Tests/VisitorWorkTests.cs` checks
  the defs (known work, roof words match `inside`).
- 0.42.0, the player's request: conduits and pipes laid again under the walls. `Building/LineRouteMath.cs` (pure,
  `LineRouteTests`: Steiner growth by nearest-piece Dijkstra over cell costs wall 10 < furniture 30 < ground 50 < floor
  80, a line in sight kept at 80%; then reverse-delete; reachers need an own line within 6 nearer than any other net;
  `Removable` re-checks at take-up time), `Building/LineRoutes.cs` (reads power nets from the game's PowerNet, pipe
  nets by adjacency of BaseReview.PipeKey things; shut cells beside other nets and within reach of their connectors;
  the Base page finding "relay:KIND" and status "relaying:KIND"; `LineRelayTracker` as `relays` on the component,
  hourly from HourlyPeople). Plans are kept six hours, nets with fewer than three lines in sight skipped. Not tried in
  the game here: worth a look at the finding on the player's base, the yellow/white overlay, and one relay to the end.
- 0.43.0, the player's ask that visitors use, make and take items and touch the colony more: `Visitors/VisitorCamp.cs`
  (a camp out on the land lays a vanilla Campfire of the visitors' faction, fed while they stay, put out to ash when
  the trip leaves Here; `JoyGiver_SitWithVisitors` + `JobDriver_SitWithVisitors` for colonists), `VisitorWork.Done`
  (prospectors' finds: silver 35%, gold 4%, into inventory; gleaners: the zone's crop), JobGiver_VisitFire (17-23h),
  JobGiver_VisitSit (a seat near the gate), social modes on the work toils, pool moments find/glean/fire. Ideas not
  built (propose first): mourners carrying their dead, hunters taking real game (a hunted beast can turn on them),
  the singer playing the colony's instrument, guests in Hospitality's guest beds, drovers milking, campers trading.
- 0.43.1, the player's ask to apply items and touchpoints to every NPC kind: PlayGroups.HandOver feeds an animal
  by setting the food down (Feed); CampAnswers.Pay(Camp, n, by) and CampAnswers.Carry carry silver, provisions and cart
  makings over (DilemmaEffect_CarryOut to Camp); Health/OutsiderTend.cs (JobDriver_TendOutsider, DilemmaEffect_TendStranger)
  for camp sick, war wounded, the outside patient; Strangers/EncounterGifts.cs (DilemmaEffect_Carving,
  Council_EncP_RefugeeCarving); ResidentBeast.Steal walks to the stores. Three parallel pieces for 0.44.0 (visitors'
  trades, mission guests' meals and beds, camp help from real sources) were in flight at this point.
- 0.43.2 (built by a parallel agent, merged): `Camps/CampCarry.cs` (a camp's good turns gathered, milked or taken
  from stock and carried to the colony's edge; JobDriver_CampBring; DilemmaEffect_CampBrings for the caravan's stores),
  the blessing walk, mending at the colonist's side, dig and stall errands, mourners' ashes, the dead carried home
  (Council_CampCarryHome). Camp beasts start with milk or wool part grown (`CompHasGatherableBodyResource.fullness`
  by name, present in the 1.6 and 1.7 references).
- 0.43.3 (a parallel agent, merged): `Missions/Hosting.cs` (hosted guests eat stored meals, sleep in counted beds lent
  by ClaimBedIfNonMedical and given back in GiveBack; Council_GuestSleep = JobDriver_GuestSleep, a lie-down that doesn't
  look for other jobs, also used now by PlayStrangers' nights).
- 0.43.4 (a parallel agent, merged): `Happenings/VisitorServices.cs` (a saved GameComponent on Sched.RareTimed: the
  tinker's mending, the doctor's doses, standing still for a worn piece; jobs in `Defs/Misc/Rimfolk_VisitorServices.xml`),
  PlayCook.CookFromOutside (the visiting cook's bill drops meals by the stove), PlayCarry "coat" and one-of-each errands
  ("meals+coat"). Risks to look at in game: a visiting cook whose visit ends mid-bill may carry an ingredient off.
- Ideas not built, for the player (propose first): mourners carrying their dead on a real cart, hunters taking real game,
  the singer playing the colony's instrument, drovers' herds giving milk to buy, campers trading at the colony's market.
- Open with the player: they asked whether rain washes blood and dirt away. The game does it already outdoors (the
  filth def's `rainWashes`, thinned in `SteadyEnvironmentEffects`); nothing was built. Wait for their answer.
- Rounds 0.30.0 to 0.41.0 came before: the player's event rounds (camps, neighbours, visitors, world, animals, Pokémon),
  the five follow-up ideas, two explicit requests (rearranging rooms; production chains and opt-in automations) and
  the year's letter.
- Candidates if the player wants more (propose as a list first): chain rates per bench rather than per recipe, and
  consumption measured from the last days' real use; the automations' preview on the settings page itself; per-room
  "rearrange" tuning knobs (aisle width, keep weight); a quiet-season digest that folds the evening news for a day;
  the template choice on a step that isn't done yet (today "Plan this step" places the step's own pick).

## Where this session's systems live

- Camps and neighbours (0.30.0, 0.31.0, 0.37.0): `Camps/` (`Camp` partial classes: CampStory, CampLife, CampRequests,
  Holding, RoadPost), `CampTracker` (camp, holding, past; `PlayCamp` finds the camp a play-out means), `WarCamp.cs`,
  defs in `Defs/CampDefs/`. The wardens' post as a trading stop: `Camps/RoadPost.cs` (`CampAsk.settledOnly` asks,
  `DilemmaEffect_RoadCarry`, `Camp.owed*`, `PayRoad`, `roadLoads`), word from the road via `CampKindDef.roadWord` and
  the pool Council_Pool_RoadWord. Allies: `World/Allies.cs` (`Allies.Of()` from the history's "alliance" record;
  Council_Evt_AllyFood; the war camp's helper prefers the ally).
- Visitors and the world (0.32.0, 0.33.0): `Happenings/Regulars.cs`, `Visitors/`, `World/Workers_World.cs`
  (other settlers, the road over a year, the crossroads), `World/RivalMath.cs` (tested), `WorldSituationDef.afterFlags`.
- Animals and Pokémon (0.34.0, 0.35.0): `Animals/HerdStory.cs`, `Animals/ResidentBeast.cs`, `Animals/HerdStoryMath.cs`
  (tested), `Integrations/PokeWorldCamp.cs`; the Overview card and the check-myself part (0.36.0) in `UI/OverviewPanel.cs`
  `Land` and `UI/SelfCheck.cs` `Stories`.
- Rearrange a room (0.38.0): `Building/RoomArrangeMath.cs` (greedy placement, light, watch areas, move cost, `Refine`
  for the whole room) and `Building/RoomWalkMath.cs` (pure walking model: Dijkstra from the doors, ways, squeezes, dead
  ends, open stretch), both tested in `Tools/Tests/RoomArrangeTests.cs` (set RIMFOLK_ARRANGE_DEBUG=1 to dump rooms);
  game side `Building/RoomRearranger.cs`, window `Building/Designator_RearrangeRoom.cs` ("How the room walks").
- Production chains (0.39.0, 0.40.0): `Building/Making.cs` (`Compute`: building needs, starved bills, `Result.chains`
  trees flattened leaf first into `needs`), `Building/Chains.cs` (`ChainNode`, rates, `BestMaker`, `Sowing`),
  `Building/ChainMath.cs` and `MakingMath.cs` (tested), page `UI/MakingPanel.cs` (`DrawLink`).
- Automations (0.39.0, 0.40.0): `Building/Automations.cs` (`AutomationTracker` on the game component as `automations`,
  run from `CouncilGameComponent.HourlyPeople`; `Candidates` feeds `Hourly` and `Preview`; `AutoAction` + `Undo`),
  `Building/AutomationMath.cs` (tested), switches in `CouncilSettings` (`auto*`, all off by default) listed in the
  "Automations" section of `UI/SettingsCatalog.cs`, texts in `Languages/English/Keyed/Rimfolk_Automations.xml`.
- The year's letter (0.41.0): `World/YearLetter.cs` from `CouncilDynamics.DailyFirst`, `comp.yearLetterLast`, keyed in
  `Rimfolk_YearLetter.xml`. The annals (`Annals.cs`) and the founding-day event (`HappeningWorker_Founding`) are separate.

## How to work here (learned the hard way)

- A fresh container has no .NET. Ubuntu's packages work (the dotnet-install script was refused):
  `apt-get update && apt-get install -y dotnet-sdk-8.0 dotnet-targeting-pack-10.0` (the second is for ApiDiff). The 1.7
  references come from restoring any throwaway net8.0 project with `<PackageReference Include="Krafs.Rimworld.Ref"
  Version="1.7.4977-alpha.2" ExcludeAssets="all" />`. With an SDK in `$HOME/.dotnet` instead, every command needs
  `export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet`. The working directory resets between commands, so use
  absolute paths or `cd /home/user/Rimworld-Helper && ...` in each command.
- The test suite takes about four minutes: run it in the background writing to a log, append `echo "exit $?"`, and
  poll the log. Read failures before starting another run (a new run overwrites the log).
- Test rules that bite: settings fields one per line (`public bool x = false;`); a thought with `RewardInstead` has
  `baseMoodEffect` 0; feelings must be existing HeartDefs; spoken pool lines are one sentence, 90 characters at most,
  one '!' at most, no surrounding quotes, listed in `ImmersionTests.SpokenPools`; `Sched` rare offsets unique and not
  colliding with hourly offsets mod 250; a new saved collection needs a MODEL line in `Tools/save_estimate.py`
  (`"Path/File.cs:field": ("fixed", N, bytes, "why")`); two-pawn methods need `// apart:` or `// together:` comments
  (interaction_audit); an answer whose words describe a done thing needs a play-out or a line in
  `Tools/playout_skips.txt` (`DefName [option N] why`).
- Releases: a round in Almanac_WhatsNew.xml (2 to 8 points, no em dashes, no exclamation marks), `About.xml` modVersion
  equal to it, `python3 Tools/changelog.py` then `--check`, this file's head updated, then all checks, commit with the
  session's attribution lines, push both branches. No model names anywhere in the repo.
- The game is played on 1.7 but compiled against 1.6: new game API names can be checked with
  `grep -c Name ~/.nuget/packages/krafs.rimworld.ref/1.6.4871/ref/net472/Assembly-CSharp.dll` before use, and ApiDiff
  confirms 1.7 afterwards.

## Older history (reference only)

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
