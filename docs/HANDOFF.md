# Handoff: work in progress

Last shipped: 0.18.1 "Moving on, by the council's word", round B of the same request. `Social/MovingOn.cs`:
`DilemmaWorker_FamilyElsewhere` (a blood parent or sibling 65+ or a juvenile child with a friendly faction, opinion
20+; `MovingOnContext` {RELATIVE}/{RELATION}, tags old/child), `DilemmaWorker_LoveOutside` (the colonist's love
partner of a friendly faction spawned here; fired from `MovingOn.Hourly` with `OnlyLove`; tags guest/married/engaged)
and `DilemmaWorker_Crowded` (a vanilla SleptInBarracks/SleptOutside/SleptOnGround memory, mood under 35%, no partner
or family at home, 20 days here; `DilemmaEffect_PromiseRoom` raises Council_Wish_OwnRoom). Defs in
`Defs/DilemmaDefs/Rimfolk_Dilemmas_MovingOn.xml`, each defaulting to staying. `DilemmaEffect_LeaveForGood.joinFaction`
makes the leaver one of ctx.faction after the banish, with an exit lord. `MovingOn.Spare`: more than four grown-ups;
`CouncilGameComponent.movingOnAsked` once per key; setting `movingOn` (off in Light touch). Not tried in the game here.

Before it, 0.18.0 "Realistic ways to die", the player's approved items 3 to 6 (the player wants death a real
possibility, always with a warning and a way to fight it). Pure numbers in `Ailments/LifeRiskMath.cs`
(Tools/Tests/LifeRiskTests.cs, which also runs the frail through the illnesses' own courses). `Ailments/LifeRisks.cs`:
`Sickness(map)` (crowded = no bed or a bed in barracks or the open; the dirtiest kitchen; size) divides the serious
catching clock in `AilmentTracker.Clock`, with `AdvisorWorker_SicknessRisk` (Council_Adv_SicknessRisk);
`StatPart_Frailty` is added in code to `StatDefOf.ImmunityGainSpeed` (`LifeRisksStartup`) and `FrailNote` goes into
`SeriousLetter`; `Patch_DirtyWound` postfixes `HediffComp_Infecter.CheckMakeInfection` and, where it failed (-3) on an
untended wound, rolls `ExtraInfectionChance` by `DirtWhere`. `Health/LastSeason.cs`: `Daily` (DailySecond) starts
`Council_LastSeason` by `YearlyChance`; `HediffComp_LastSeason` moves it hourly (`DeclinePerDay` by mood, own bedroom,
a good social memory of the last day), turns back under 77, tells the colony at 0.85 with up to three sit-down
visits, and at 1 kills them asleep (`Pawn.Kill(null, hediff)`); the setting off sets `remove`. Settings
`crowdedSickness`, `frailty`, `dirtyWounds`, `endOfLife` (off in Light touch). Not tried in the game here.

Before it, 0.17.10 "A life goal counts in full", a fix after the player saw a life goal reached give +3 for five
days (a spoiled colonist's standards halved it, and good times halved its time). `Moods/Milestone.cs`: a
`Milestone` DefModExtension (on `Council_Asp_Fulfilled`) skips `Standards.MoodFactor` in `GiveThought`, is never
`GoodTimes.IsGoodMemory` (no fading sooner) and is left out of `SmallJoys.Rank`; `Milestone.Restore` in
`FinalizeInit` sets such memories already given back to the reward strength alone and their full time.

Before it, 0.17.9 "Family names, suppers and visits", family round D. `Social/FamilyNames.cs`: `FamilyNames`
builds, at most hourly, the known families: free colonists grouped by last name, then `FamilyLifeMath.Clusters` over
`Family.Of` or a love relation, two or more (`Plural` "Boyds", `Members`, `Same`). `Standing` averages
`Reputations.Standing` over them (`FamilyLifeMath.FamilyStanding`, `FamilySides`), `ReputeLine` words it with
`CC_FamRep_*`, and `StoryLine` heads the Story tab's family section and tip. `Gossip.Tags` adds "aboutfamily" (30%,
teller outside the family) and "famgood"/"fambad" by `Leans`; `Gossip.Line` passes {FAMILY}; lines in
`Defs/FlavorPoolDefs/Rimfolk_Gossip_Family.xml`. `DilemmaWorker_SharedName` (`Council_DlmFam_SharedName`,
`SharedNameContext` with {LASTA}/{LASTB}, tag "engaged"): couples with two names, once each
(`CouncilGameComponent.sharedNameAsked`); a day after an engagement (`Notify_Engaged` from `FamilyLife`, session
only, `OnlyEngaged`) if the game's roll kept their names, else in its turn. `DilemmaEffect_SharedName` sets both
`relations.nextMarriageNameChange` for an engaged couple (the game applies it in `MarriageCeremonyUtility.Married`) or
calls `SpouseRelationUtility.ChangeNameAfterMarriage` now for a married one (`birthLastName` kept), checking the
game's `GotMarried_*` history events for faith. `Social/FamilyBonds.cs` (session only): once a day per map, a
`DilemmaEffect_Feast` at the dining table for a family cluster of three or more with `AdvisorFacts.Meals` enough
(`FamilyLifeMath.Supper`, chance 0.2, six days apart, `Council_Pool_PlayFamilySupper`, held `Council_Fam_FamilySupper`);
grown Sibling/HalfSibling pairs (`FamilyLifeMath.Siblings`: close both 20+, rivals either -10 or less; 0.15, five days)
through `Together.When`; a parent of 60+ (`LookInChance`, ill: down, sick or wanting bed rest) gets a sit-down
`DilemmaEffect_Visit` from the fondest grown child with held moods both ways. Not tried in the game here.

Before it, 0.17.8 "Fed, sheltered, and a fire laid by hand", audit gaps 8, 9, 16 and 22. `PlayOut/PlayStrangers.cs`:
`DilemmaEffect_CarryOut.eat` (at the hand-over, `PlayGroups.HandOver` shares the food from the receiver's pack round
their lord, an `Ingest` each; meals one each, raw food half) and `night` (`StayTheNight`: the `LordJob_VisitColony`
length and its `Trigger_TicksPassed` stretched to dawn by name through `AccessTools.FieldRefAccess`, a courier trip's
`stayUntil` and `LordJob_Courier.lateTicks`; from 21:00 a `LayDown` by a player wall near `PlaySpots.GateCell`,
session-only list stepped from `PlayOutTracker.GameComponentTick`). `PlayDepart.HoldSendAway` now holds on a pending
Carry to X too, and `Over` sends a fed visitor on only once they've eaten (`SendOnAfterMeal`). `PlayOut/PlayFire.cs`:
`TryStartGathering` waits on `PlayFire.Waits` (a `Council_PlayOutLayFire` job carries wood to `PlayFire.Cell` and
spawns the campfire; `PlayOut.fireLaid` becomes the lord's `laidFire`); `TryLayFire`/`UnlayFire` are gone.
`PlayOut/PlaySnow.cs`: `DilemmaEffect_Snowman` marks the answer's gathering or work party; a sixth of the way in the
builder gets the game's `BuildSnowman` job (found by name). CarryOut `to` Person with `whom`: silver into a colonist's
pack (Keepsake, PetBite, Goal_Purse). Not tried in the game here.

Before it, 0.17.7 "The stalls and the ships", the player's request that a RimMarket colony stall's goods count for
orbital trade once there is a beacon. `Integrations/RimMarketStall.cs`: `Patch_StallGoodsForShips` (postfix on
`TradeUtility.AllLaunchableThingsForTrade`, wrapping the result: the beacon's things first, then, only with
`Building_OrbitalTradeBeacon.AllPowered(map)`, `RimMarketStall.ShipGoods`, the stock held in each colony stall's
`StallStock` ThingOwner, never silver, filtered by `TradeUtility.PlayerSellableNow`); `Patch_StallGoodsSoldToShip`
(prefix on `TradeShip.GiveSoldThingToTrader`: a thing whose `ParentHolder` is a colony stall calls the stall's own
`Notify_Sold`, so its line counts down and haulers don't restock it; `Thing.SplitOff` takes it out of the stall's
container). Both `Prepare` only with RimMarket; setting `marketStallShips`. Not tried in the game here.

Before it, 0.17.6 "Who raises them, and the one left", family round C. `Social/FamilyGuardians.cs`: `Guardians`
(`Unparented`: a child of ours with no living colonist parent by blood or `Council_AdoptiveParent`; `Notify_Died`
records orphans for the session, `Hourly` fires `DilemmaWorker_Guardian` a day later with `OnlyOrphaned`;
`CouncilGameComponent.guardianAsked`, once per child), `DilemmaEffect_Adopt` (a `DilemmaEffect_Needs` so it runs
first; `childChooses` sets B), `Defs/DilemmaDefs/Rimfolk_Dilemmas_Family.xml` (tags "orphaned"/"baby" via the new
`DilemmaWorker.AddTags`). The relation: `Defs/Misc/Rimfolk_Relations.xml` and `PawnRelationWorker_AdoptedChild`;
`Family.ParentsOf`/`ChildrenOf`/`Adoptive`, and `DilemmaWorker_Family` counts them. `Social/FamilyInheritance.cs`:
`Inheritance.Bonus` (`FamilyLifeMath.HeirBonus`) orders heirlooms (Lives/Legacy.cs); `Patch_FamilyDied` now has a
prefix keeping the dead's `ownership.Bedroom` bed, passed a day later (session only) to family without a bedroom.
The one left: `Council_Arc_Widowed` (`ArcWorker_Widowed`; `ArcWorker_Grief` steps aside when it `Takes`);
`CalendarTracker.Notify_Death` turns a wedding day `Personal` for the survivor and `Personal()` calls
`FamilyLife.WeddingDayAlone` (grave by id, else a walk); `FamilyLife.Reactions` gives a widowed parent's children
`NewPartnerTake` with `Council_Pool_SayParentNewPartner`. Fixes: `Legacy.ComingOfAge` read the parent relation the
wrong way round; an arc's grave visit with no grave of the lost now goes to its `orTo` instead of any grave; babies
aren't seated by sit-down visits.

Before it, 0.17.5 "The story at a glance", the player's request for a shorter Story tab. `UI/ITab_Pawn_Story.cs`:
`StoryView.Build` makes an `Overview` (character words from the "character" section's first line, standing pills:
trust, office, `HealthTracker.StoryTag`; the goal with `UiKit.Bar`; `ArcView.Now` with stage dots; quirk, heirloom,
vendetta and nemesis pills whose tips are `QuirkEffects.Tip` and the old lines; the family here as `UiKit.PawnChip`s
from `Family.Tree`, the rest counted, the whole tree in the tip) drawn as a measured `UiKit.Card`, then the sections:
the character section skips its words line (`CharacterView.DrawSection(skip)`), the family tree is a table
(`Family.TreeEntry`, `WhereKind`), "life" holds goals reached and `ArcView.Past`, journal and dreams are quotes.
`Folds` takes sections that start shut (`StoryView.FoldedInTab`); the People roster's folds start all open.

Next: the audit's next gaps (docs/answers-audit.md: 12 work parties, 13 sculpture, 14 burials and markers, 15 smudge
fires and torches, 17 lost child, 19 soup rounds, 20 children's race, 21 mushroom supper, Story_Seeds, ProphetHill,
BanditRoad). Round B leftovers not built: an engaged couple planning the wedding with the council; close family
waiting by the bed during labour (left to the game's own birth ritual).

Before it, 0.17.4 "Sat down, seen off, asked in", audit gaps 2, 3 and 6. `PlayOut/PlaySit.cs`: a Visit with `sit`
(and `sitBy` Fire) to someone up and free starts the visitor's job at a seat (`targetC` = the other) and the other's
`JobDriver_PlayOutSitWith` across a table or by a fire (20 answers marked). `PlayOut/PlayDepart.cs`:
`DilemmaEffect_LeaveForGood` holds the leaver on the answer's walk (`leaving`, gone at phase 2), `DilemmaEffect_SendAway`
on the visit to them (`walked`: sets off at the visit's start, else when it ends), `ReleasePet` and `TurnOut` lead the
animals (`led`, `FollowClose`) and let them go at the far end; whatever is held happens at Done or Fallback.
`PlayOut/PlayGuests.cs`: a gathering's `guests` ("stranger", "camp", "kin") are resolved when it starts
(`LordJob_PlayOut.guests`), walked to a ring round the spot (`JobDriver_PlayOutAttend`) and served at a feast.

Before it, 0.17.3 "A family's life together", family round B. `Social/FamilyLifeMath.cs` (pure, `FamilyLifeTests`)
and `Social/FamilyLife.cs`: `Patch_FamilyCouple` (`Pawn_RelationsTracker.AddDirectRelation` postfix, Lover or Fiance
between two of ours: journals, chronicle, up to four close ones react via `Together.When` with `Council_Fam_GladForThem`,
`Council_Fam_MatchDoubted` (family who can't stand the partner) or `Council_Fam_ChildEngaged`); `Patch_FamilyProposal`
(a refusal: a Walk visit and a friend's `Council_Fam_Comforted`); the hourly pregnancy check (`ShowingPregnancy`, told
once per hediff `loadID` in `CouncilGameComponent.expectingTold`; wanted by `Hediff_Pregnant.Attitude` and the couple's
`PregnancyApproach`); `Patch_FamilyMiscarried` (only when the mother has the game's fresh "Miscarried" memory, so a
surgical termination isn't counted) and stillbirths in `Patch_Birth` go to `Notify_Lost` (grandparents' grief,
`CalendarTracker.RestFor`, a sitting-with visit); `Patch_FamilyBabyNames` (a `Dialog_NamePawn` constructor prefix that
adds First-name suggestions); `NamedAfterLine` in the story; `Patch_FamilyBirthday` (`Pawn_AgeTracker.BirthdayBiological`:
growth birthdays and 18, a `DilemmaEffect_Toast` with the new `DilemmaTarget.FamilyOfA`, held moods). The crib wish's
`parentOfLittleOne` counts expecting parents (`FamilyLife.Expecting`). Next: round C (adoption and guardians, orphaned
here, inheritance by blood, widowhood and remarriage), alternating with the audit's ranked gaps.

Before it, 0.17.2 "Made before it's given", B4 of the answers audit (`docs/answers-audit.md`): results an answer
used to make the moment it was given now come with the work. `PlayOut/PlayMake.cs`: a `DilemmaEffect_Made` with a maker
(and a hand-over) is a Give play in phase `PlayMake.Making` until the maker works at the bench found by the recipe that
makes the thing (`RecipeFor`, `AllRecipeUsers`), the camp's fire for one of the camp, else the table
(`JobDriver_PlayOutMake`, time kept in `p.tally`, `p.makeTicks`); `KeepMade` (mode `PlayGive.Keep`) makes and keeps it at
the bench, fetching an ingredient first (`p.place`: the fallen star) and with a fixed quality (`makeQuality`) or bench
(`makeAt`). `OnMapTimed` kinds `Felling` (the named visitor, their lord's people, the camp's or A cut the trees with
`JobDefOf.CutPlant`; logs made since `firstId` near the stumps are unforbidden and counted; `topUp` for the struck tree)
and `Planting` (`JobDriver_PlayOutPlant`, a child each or A, at cells chosen up front). Both can carry the answer's
outcome (`PlayOuts.RegisterTimed`, `TellWhenWorkDone`) and say so if nothing was done. `DilemmaEffect_PlanRoom` stores
the plan on the answer's walk (`planRoom`, `planAnchor`, `planRot`, `planMid`), staked at the walk's far end
(`Stake`). The storyteller events: `HappeningContext.plays` lets `Resolved` defer a result onto the answer's visit to the
place (`PlayAfter.AfterVisit`, results run only when it happened: `Run(p, happened)`), visits can show work
(`DilemmaEffect_Visit.work`). Not tried in the game here: the jobs are the game's own or ours, given directly.

Before it, 0.17.1 "As the answer said, in its own time", the first fixes from the answers audit
(`docs/answers-audit.md`: 22 ranked gaps between answers' words and the map, and five plainly broken, B1 to B5).
B3: a feast's raw food (a `DilemmaEffect_Consume` of rawfood beside a `DilemmaEffect_Feast`) is held on the feast
(`PlayCook.Hold`) and cooked in the hours before it by its organizer or best cook, with a vanilla `Bill_Production`
put first on the stove (restricted to the cook, one meal more than needed so the game never calls it finished,
suspended by `Patch_FeastCooked` once enough are made) and `WorkGiver_DoBill.JobOnThing(forced)`; the feast waits
(`PlayCook.Waits`, before the hours check in the tracker). B2: `DilemmaEffect_Fight` and `DilemmaEffect_MentalState`
hold their effect on the play-out the pawn takes part in (`PlayOuts.HoldRisk`, `HeldRisk`), fired a third of the way in
(`PlayOutMath.RiskAfter`) or at Done/Fallback. B5: errands carry load after load (`StepCarry`), camp goods go to a camp
member (`PlaySpots.CampHand`). B1: a fall-through of a play holding the outcome always posts why. Not tried in the game
here: the cooking bill and job are the game's own, given directly. Next: family round B, and the audit's ranked gaps
in order of how visible they are.

Before it, 0.17.0 "Family, wherever they are", the first of four rounds of family features the player chose
(all sixteen from the proposal). Round A here: `Social/FamilyMath.cs` (pure, `FamilyTests`) and `Social/Family.cs`:
family by marriage (ParentInLaw, ChildInLaw, Stepparent, Stepchild) in Worry, belonging, `Setbacks.Closeness`,
`ArcUtil.Closeness`, family visits and remembrance days; the story's Family section (`TreeLines`); reunions (a
newcomer's first day, `Council_Fam_Reunited`, `Rimfolk_Mem_Reunited` with talk lines); kin among raiders
(`Council_Fam_KinAmongRaiders`) and `Council_DlmS_KinInChains` (`StrangerRole.kinOfA`; `{RELATION}` is now in every
dilemma's text, `DilemmaContext.AddArgs`); `Patch_FamilyDied` for a death in the family by marriage. Next rounds:
B couples and proposals, expecting a child, losses before birth, naming the baby, growth moments; C adoption and
guardians, orphaned here, inheritance by blood, widowhood and remarriage; D family meals, adult siblings and ageing
parents, family names.

Before it, 0.16.7 "The enemy dead, as the council said" (the player's rule that answers show on the map as
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
