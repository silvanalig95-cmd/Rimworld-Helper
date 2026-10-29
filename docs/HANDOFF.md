# Handoff: work in progress

Last shipped: 0.23.2 "Slots kept just so", the player's asks on 0.23.1. Entries are `ShelfKind {kind, slots, only,
perStack, when}` (`ShelfReserve.entries`; the 0.23.0/0.23.1 "kinds" name-per-slot list loads into entries). `State`
per entry: `kindOf`, `active` (its `when` holds: "" always, "patient" = someone in a bed in the shelf's room who is
downed, should seek medical rest or needs tending), `stacksOf`; slots come only from active entries. `Barred` (matches
an inactive entry and no active one) blocks a thing and moves its stacks out; `CapReached` (an `only` entry with as many
stacks as slots) blocks a new stack; `LimitFor` (lowest `perStack`) blocks a cell whose stack is at the limit, keeps
on-shelf merges (WorkGiver_Merge) within it, and caps the haul count (`Patch_ShelfReserveHaulCount`, a postfix on
`HaulAIUtility.HaulToCellStorageJob`, via `ShelfReserveMath.SpaceFor`). `MoveOut` takes barred stacks, then stacks past
a cap (filling no slot, smallest first), then room for the waiting slots. Over-limit stacks are left to be used up.
Pick Up And Haul fills from inventory by its own count, so it can bring a little past a per-stack limit once. The window
(Building/ShelfReserveWindow.cs) has two lines per kind: slots, "no more than that", per stack (steps below the kind's
own stack size, then full), and "when". Not tried in the game here.

Before it, 0.23.1 "More to reserve slots for", the player's asks on 0.23.0. Kinds are one entry per slot (a kind
named twice keeps two; `Set` no longer dedupes). `ShelfReserveMath.Fill` matches stacks to slots (augmenting paths,
bigger stacks first), so each slot needs its own stack; `Missing` is the unfilled slots and `MoveOut` takes only stacks
filling no slot. "best:Cat" kinds (`Kind.best`, `targets` from `ShelfReserves.BestAtHand` per look: `Rankable` food
or medicine, `RankOf` preferability then worth, or medical potency; `ShelfReserveMath.Best`). `Dialog_ShelfReservePick`
(Building/ShelfReservePick.cs) replaces the float menu: on the shelf now, the category tree of the allowed defs (any,
and best at hand where `RankableGroup`), and every def the shelves accept, with a search box and only visible rows
drawn. The shelf window groups slots per kind with minus and plus, shows slots reserved against capacity, and has
"Keep what's on it now". Not tried in the game here.

Before it, 0.23.0 "Reserved slots, and drinks out of the freezer", two asks of the player's. Pure rules in
`Building/ShelfReserveMath.cs` (Tools/Tests/ShelfReserveTests.cs): `Missing`, `MayTakeSlot(capacity, stacks,
fillsMissing, missingWaiting)`, `MoveOut` (the most-held def's smallest stack, never a reserved kind's last), and
`ChewSpotMath.StoreRoom` (stores and below 0 °C, or stores and no seat). `Building/ShelfReserves.cs`: MapComponent
`ShelfReserves` (saved `reserves`, `ShelfReserve {shelf, kinds}`, kinds "def:X" or "cat:Y"; static `Reserved` set so the
hooks cost nothing without any), `StateOf(shelf)` cached 120 ticks or until the held count changes (capacity is the sum
of `GetMaxItemsAllowedInCell`, which LWM's `Building.MaxItemsInCell` prefix feeds; a kind is waiting when the map has
some not forbidden, in storage or the home area, allowed by the shelf). Hooks: `Patch_ShelfReserveCell` (postfix on the
private `StoreUtility.NoStorageBlockersIn`, after LWM's prefix: a stack joining one in the cell always fits, a moving-out
stack fits nowhere on its own shelf), `Patch_ShelfReserveAccepts` (`Building_Storage.Accepts` false for the moving-out
stacks, so ListerHaulables hauls them on), `Patch_ShelfReserveGizmo` (`Command_ShelfReserve`, grouped; not on storage
under two stacks) and `Dialog_ShelfReserve` (candidates: haulables on the map every selected shelf accepts, plus a
category when two or more share `FirstThingCategory`). Copy/paste of storage settings doesn't carry reservations.
`Building/ChewSpots.cs`: `Patch_ChewOutOfStores` postfixes `Toils_Ingest.TryFindChairOrSpot`; a standing spot in a
store room becomes a seat at a table within 40 cells (validator like the game's), else the nearest spot within 18 cells
outside it. Settings `shelfReserves`, `chewOutOfStores` (Building page, "Stores"; deep hooks). Not tried in the game here.

Before it, 0.22.0 "A tidier bottom bar", the player's ask to put the bottom bar's unused mod buttons under one
expandable button, picked in the game. `UI/BarMore.cs` (pure part `UI/BarMoreMath.cs`, Tools/Tests/BarMoreTests.cs):
MainButtonDef `Rimfolk_MoreButton` (worker `MainButtonWorker_More`, minimized, icon Icons/More from make_icons.py,
placed at Menu.order - 1 by `BarMore.Place` from `CouncilStartup.ApplyButtonPosition`, which leaves it out of the
council button's reckoning). Settings `barMore` (needs deep hooks) and `barPutAway` (def names, `BarMoreMath.Clean` on
load; covered by the "barArrange" action row). Hiding is a postfix on `MainButtonWorker.Visible`
(`Patch_BarPutAway`) and on every worker class's own override (`Patch_BarPutAwayOwn`, `TargetMethods` over
`GenTypes.AllSubclasses`; the mechs tab's ignores buttonVisible), never by writing `buttonVisible`: Dubs Mint Menus
rewrites that every frame from its own settings. `BarMore.WouldShow` asks a worker with the postfix stood down
(`asking`). `Patch_BarRightClick` (DoButton prefix, right mouse down) offers "Put it away"; a worker overriding DoButton
without base gets no right-click but is in the arrange window. More's FloatMenu: the put-away buttons that would show
(disabled ones greyed), a right-justified pin extra part that pins back and closes, then "Arrange the bottom bar"
(`Dialog_ArrangeBar`, also from the settings). Hotkeys of hidden buttons still work (MainButtonsOnGUI checks every
button's key). Not tried in the game here.

Before it, 0.21.0 "Wedding plans" and 0.20.2 "What the words say, done", the player's "go ahead on all" on the
leftovers (all but the family waiting by the bed during labour, which they turned down).

0.21.0: `Social/WeddingPlans.cs`. `DilemmaWorker_WeddingPlans` (Council_DlmFam_WeddingPlans) takes a couple at home
engaged two days or more (`DirectPawnRelation.startTicks`), asked once (`CouncilGameComponent.weddingAsked`, pair keys,
40 kept), with no `LordJob_Joinable_MarriageCeremony` running. `DilemmaEffect_PlanWedding style` saves a `WeddingPlan`
(`weddingPlans`: from two hours on, or 10 to 15 days for "wait"; five days to be held). `WeddingPlans.Hourly` (from
Social/Family.cs) starts the game's own `lordsStarter.TryStartMarriageCeremony` between 15:00 and 20:59 when
`AcceptableGameConditionsToStartCeremony` and both `FianceReadyToStartCeremony`; a lapsed plan says so and is dropped.
`Patch_Married` calls `WeddingPlans.Married`: "feast" a `DilemmaEffect_Feast` for the colony, "quiet" a
`DilemmaEffect_Toast` (CloseToA) led by the friend who thinks best of the two.

0.20.2 closes the answers audit (docs/answers-audit.md: no candidates left in playout_audit). Work parties
(PlayOut/PlayWork.cs): `WorkTask` Wood (`FellForPile`, `CarryToHeap` with `HaulMode.ToCellNonStorage` to a heap by the
gate), Stone (chunks only), Roof (`MarkRoofs`: the BuildRoof area over a room's unroofed home cells, 200 at most);
`mode` "butcher"/"storeroom" (`At`). `PlayOut/PlaySmash.cs`: `DilemmaWorker_Likeness` needs a sculpture by A
(`PieceBy`, by CompArt author name) as the context's place; `DilemmaEffect_Smash` breaks it after the visit
(`PlayAfter.Smash`). `PlayOut/PlayMarkers.cs`: `DilemmaEffect_MarkGround` (rebury/stone, at the walk's end, or the
fallback), `DilemmaEffect_MarkGraves` (OnMapTimed Marking, `JobDriver_PlayOutMark`, Council_PlayOutMark). OnMapTimed:
Smudge (`DilemmaEffect_SmudgeFires`: campfires along the field edges, kept fuelled, ash at the end), Race
(`DilemmaEffect_Race`: queued jog laps of `RaceCourse`, with the gathering or by itself at 16 to 20), Lanterns
`at` "doors". `Vigil torches` on a patrol (`Glows.Torch`, `VisualTouches.Torch`). `Camps/CampLost.cs` (the camp's child
and a beast lying out 28 to 42 cells away; `DilemmaEffect_FindLost how`, the search's last cell set to theirs,
`CampWork`/`CampLook` give their jobs first). `DilemmaEffect_Rounds serve` fetches meals, then hands them out.
`DilemmaEffect_SowPatch` (fence, outOfWay) for Story_Seeds. ProphetHill and BanditRoad: `DilemmaEffect_Paid`,
`CarryOut toGroup`, `SendAway ofGroup`. Fixes: `TalkMath.Turns` gives friendly talks a fourth line now and then (the
`Any_Last` lines), and `Secret.sayBy/sayUntil/confideTo/confideFrom` keep a waiting confession or confiding over a load
(`SecretTracker.Rewait` once in the first hour). Not tried in the game here.

Before them, 0.20.1 "Ties weigh, they don't bar", the player's ask that ties at home make recruiting harder but
never impossible. `GuestWill.Will.ties` carries each tie's kind (`GuestWillMath.Tie`); `TieWeight` (partner, children,
leader 2; parent, leader's family, faith 1) and `ExtraFriends(weight, reasons)` (each reason beyond the first takes one
off) give `Will.Extra`; `Willing` no longer counts ties. Hospitality's friends are read by name
(`GuestWill.HospitalityFriends`: GetFriendsInColony, FriendsRequired + GetEnemiesInColony, as its tab does);
`EnoughFriends` (friends >= required + Extra) gates the Recruit button, `WouldStay` (friends >= Extra) the happy
guest's join and the council's guest who wants to stay. Titled guests go by Hospitality's seniority: no extra.
`Patch_GuestWillFriends` postfixes the private `ITab_Pawn_Guest.GetRequiredFriends` so Hospitality's own bar shows the
higher number; it's small enough that the JIT may inline it, in which case only the button and tooltip count it. Not
tried in the game here.

Before it, 0.20.0 "Guests who would stay", the player's five approved Hospitality ideas and their ask for wants by
character (a passionate cook wants a good kitchen). `Integrations/GuestWill.cs` (pure numbers in `GuestWillMath.cs`,
Tools/Tests/GuestWillTests.cs), setting `guestWill` (deep hooks, Hospitality). `GuestWill.Of(guest)` (cached 250
ticks) reads ties (a love partner not here, juvenile children not here, parents while under 25 and not in love here,
the faction's leader or their family, with Ideology certainty 0.85+ in another ideoligion), reasons (love or family
here, `WorldSituations.HardTimes` (a running `bad` situation for their faction), visit score 0.75+, the colony's
primary ideoligion), room (`FreeBed`: a colonist bed, not medical or a guest bed; alone in a room for Greedy, Jealous,
titled; four days of `TotalHumanEdibleNutrition`) and up to two wants (`GuestWillMath.Wants`: Gourmand, then passions
by `People.PassionLevel`; none for Ascetic or a child; only what `Avail.CanBuild` allows; checks in `Met`). By name:
`Patch_GuestWillButton` (DialogUtility.DrawRecruitButton prefix: `hasEnoughFriends` false when unwilling, so only
Force recruit; a TipRegion with `GuestWill.Tip`), `Patch_GuestWillHappy` (IncidentWorker_HappyGuestJoins.IsHappyGuest
postfix), `Patch_GuestWillRecruit` (GuestUtility.Recruit prefix: `MayRecruit` fires Council_DlmGuest_Invite through
`GuestWill.pending` when `Objector` finds one; blocked with a message while that or another dilemma letter waits;
`RecruitNow` re-invokes with a bypass), `Patch_GuestWillAdopt` (GuestUtility.Adopt prefix/postfix: `GuestKin.Joined`).
`StatPart_GuestCost` goes on RecruitRelationshipDamage and ForcedRecruitRelationshipDamage at startup
(`GuestWillMath.CostFactor`). `GuestKin` (saved `CouncilGameComponent.guestKin`, `GuestKin.Hourly` from Social/Family.cs)
rolls `AskChance` once when due (10 to 30 days), brings the closest `MovingOnHeld.CanCome` relative in by
`Arrivals.Visit` to the gate and fires Council_DlmGuest_KinAsks (`KinAsksContext`, a MovingOnContext): go home
together (`DilemmaEffect_GoHomeWith` registers `DilemmaEffect_LeaveForGood.GoesWith`, so the relative joins their exit
lord), silver, or stay (`DilemmaEffect_HoldBack asAsked`: "love" for a partner). `DilemmaWorker_Guest` also needs
`WouldStay`. Not tried in the game here.

Before it, 0.19.0 "What a life goal leaves behind", the player's five approved ideas. `Aspirations/GoalRewards.cs`
(pure names in `GoalRewardMath.cs`, Tools/Tests/GoalRewardTests.cs), called from `Aspirations.Fulfil` before the
letter, whose lines it returns: `SkillOf` (the goal's skill, record, counter, office or worker; combat and craft-or-art
pick the pawn's better), saved as `Aspiration.mark` (old saves fill it in `Build`); `Inspire` (by
`InspirationFor`, else Frenzy_Work, else any; replaces the old 50% random one); `Toast` (a `DilemmaEffect_Toast` with the
new `DilemmaTarget.CloseToA`, organized by the friend who thinks best of them, keyed lines CC_Asp_Toast_*, held moods
Council_Asp_Toasted/RaisedGlass); `PassOn` (a `DilemmaEffect_Lesson` for a younger colonist with a passion and less of
the skill, Council_Asp_LearnedFromThem/PassedItOn); a deed Rimfolk_Deed_GoalReached with `Aspiration.Deed` (the new
`AspirationDef.deed`, past tense, on every goal) for gossip and the Story tab ("Remembered as the one who ...", and the
marked skills). `Patch_GoalMarkDecay` (SkillRecord.Interval prefix) and `Patch_GoalMarkLearn` (LearnRateFactor
postfix, x1.15) need the deeper game hooks. Not tried in the game here.

Before it, 0.18.4 "Kept at a price, and faith that holds", two asks of the player's. Keeping someone who asked to
move on always costs (`Social/MovingOnMath.cs`, Tools/Tests/MovingOnTests.cs; `Social/MovingOnHeld.cs`): the first
asks' stay answers now carry `DilemmaEffect_HoldBack` (trust by `TrustLoss(base, strength)`; strength from the workers:
`MovingOnMath.Family/Love/Room`, on `MovingOnContext.strength`), which makes a saved `HeldBack`
(`CouncilGameComponent.movingOnHeld`) read by three situational thoughts (`ThoughtWorker_HeldBack*`,
Council_Leave_Long*, stages by `MovingOnMath.Stage`). `MovingOnHeld.Daily` resolves records (own bedroom; partner a
colonist; relative a colonist or dead), breaks a promised room at 15 days (-10 trust), ends at 60, and fires the second
asking (`DilemmaWorker_AskAgain*`, Council_DlmMove_Again*: `DilemmaEffect_BringRelative`, recruit, `DilemmaEffect_GiveRoom`,
or `HoldBack again` with Council_Leave_Refused); love's at the partner's next visit from `MovingOnHeld.Hourly`, which
also brings a sent-for relative in by day (`Arrivals.Spawn`, `RecruitUtility.Recruit`, a meeting visit). Faith:
`LeaningMath.DecreeCeiling` (0.3: the decree feeds no leaning past it), `Resists` (openness under -20, pride 40+,
preaching frowned on), `Defiance` (saved on `FaithPerson.defiance/defied/defianceTold`; damps the decreed faith's
whole feed, `Steady` gives certainty back; told once at 50%, shown on the Culture tab), and `ReturnPull(left,
underDecree)` (up to 0.3). Not tried in the game here.

Before it, 0.18.3 "Camps dressed for the weather", the player's ask that camps come in any weather but the
really extreme. `Camps/CampWeatherMath.cs` (Tools/Tests/CampWeatherTests.cs): `TooExtremeToCome` (-30/45 °C),
`TooExtremeToStay` (5 past), `StayLow`/`StayHigh`, `Lasting` (a running condition's offset kept on its side),
`Shortfall`, `WouldStop` (the stay's low and high within the safe range), `PitchForCold` (low under 2 °C),
`PitchForHeat` (high over 30 °C). `Camps/CampWeather.cs`: `Hazard` (fallout, volcanic winter, flashstorm, noxious haze,
Stormproof storms), `StayRange` (hourly `TileTemperaturesComp.OutdoorTemperatureAt` over the stay, plus now), `Dress`
(up to four layers from `ThingStuffPair.AllWith` apparel that `canBeGeneratedToSatisfyWarmth`, by shortfall, capped at
1.5 cloth parkas, the faction's tech, the colony's era and the game's tribal rule; `PostProcessApparel`), `Clothes`
(words), `CampPitch` (on `CampSetup` and, unsaved, `Camp.pitch` for the arrival letter's facts). `CampTracker.Prepare`
rolls the stay first, lays out for the cold (`CampSite.Layout(cold)`: beds within 3.5 of the fire, a windbreak at 4,
`ColdWindbreak` log walls for kinds with no wall or hut) and finds a shaded spot in heat (`TryFind(shade)`, trees
weighted 0.9; `Shaded`: 6 trees within 8). `CampLife.BadWeather` no longer leaves for a cold snap or heat wave. Not tried
in the game here.

Before it, 0.18.2 "Stall goods for ships, camps in warm weather", from the player's save with no camp and no
merchant in 62 days, and stall goods missing from a trade ship's screen. Fixes first:
`Patch_StallGoodsSellable` (Integrations/RimMarketStall.cs) postfixes the private `TradeDeal.InSellablePosition`,
which drops any unspawned thing but genepacks, books and outfit-stand apparel, so `Patch_StallGoodsForShips` never
reached the screen; for a `TradeShip` a colony stall's counter good (`RimMarketStall.CounterOf`) gets the stall's own
answer. Camps: `Camps/CampWeatherMath.cs` (Tools/Tests/CampWeatherTests.cs). `CampTracker.Prepare` turned a camp away
when any one of them was past their comfortable range at ten in the morning (a hot summer turned all away); now
`WouldStop` allows 6 °C past it, and `CampLife.BadWeather` packs up when most are past the game's safe range (10 °C).
`CampTracker.WhyNot` gives the start gate's reason and `Prepare(map, why)` the setup's. "Rimfolk: check myself" has a
"Camps and trade caravans" part: the start gate, a trial `Prepare` (pawns discarded), the last caravan day, and per
friendly trading faction `FactionCanBeGroupSource` with the reason (`CaravanStop`: a vanilla blocking lord, i.e. a
group of their enemies on the map not hostile to the colony; temperature). In that save no caravan came after day 27:
Rimfolk never touches the caravan path; a neutral faction at war with every other one (a Stoneborn rough outlander
people) blocks all caravans while any of theirs is on the map, and RimMarket merchants come only with caravans. Also
`Integrations/MarketGuests.cs`: with Hospitality, a `Reachability.CanReach` postfix (Priority.Last) asks again with
`CompGuest.guestArea_int` set aside a moment when an arrived guest's target is within 4.5 cells of a colony stall
(setting `marketGuestsShop`, deep hooks). None of it tried in the game here.

Before it, 0.18.1 "Moving on, by the council's word", round B of the same request. `Social/MovingOn.cs`:
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

The audit gaps listed here then were closed in 0.20.2, and the wedding planned with the council came in 0.21.0. Close
family waiting by the bed during labour was turned down by the player (left to the game's own birth ritual).

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
