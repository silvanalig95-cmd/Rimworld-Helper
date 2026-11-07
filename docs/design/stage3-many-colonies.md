# Stage 3: many colonies, one world (design, 4 November 2026)

> **The player's decisions (6 November 2026):** 1A, 2A, 3A, 4A, 5B, 6B, 7A, 8A, 9A, 10A, 11B, 12A, 13A, 14A, 15B (the
> decisions as numbered in this revision). Not as recommended, and how they are built: 5B, Rimfolk raises the game's
> colony limit, a preference shared by every save, so only by one when a colony is founded (never past the game's five)
> and said in the founding window; 6B, the handed-over town becomes one of Rimfolk's own peoples for its age, and the
> hand-over window says that a save holding one then needs Rimfolk; 11B, former colonists' fate follows the town's
> fortune, kept to the harm rule (CLAUDE.md): a death comes only from a real hardship of the town (a raid on it, a
> famine or plague in the region), never a hidden roll, and is told by letter, naming them and what happened; 15B, the
> town grows at Faction Territories' own pace (about one new town every 30 days).

> **Release 1, a fresh start, is built** (6 November 2026): see "The order to build it in", item 1, for what differs.

> **Release 2a, handing a colony over, is built** (6 November 2026): section 2 but what the town does afterwards, taking
> it back and keeping the layout (release 2b). See "The order to build it in", item 2, for what differs.

> **Release 2b, the town afterwards, is built** (6 November 2026): the kept layout and the town's map made from it (7A),
> its letters, trade, days, homecomings and moving there, and taking it back by letter and by force (10A). See "The
> order to build it in", item 2, for what differs. The raid on the town, hard times, turning against the colony,
> Faction Territories' pace and 11B stay with release 5.

> **Release 3, a scenario's start, is built** (7 November 2026): any loaded scenario's people, what shapes them, its
> kit, arrival and animals and Rimfolk's own parts for the new colony alone, with the decision 3A. See "The order to
> build it in", item 3, for what differs.

> **Release 4, many colonies in play, is built** (7 November 2026): two councils become one when a colony's people come
> to live at another home (12A), each colony its own character (13A), neighbours with faces and world events near every
> home (14A), the pages following the colony in view, and the small ones of 3d. See "The order to build it in", item 4,
> for what differs.

> **Release 5, the town's life, is built** (7 November 2026): the raid on the town and the call for help on its map,
> hard times and the town asking to come back (refused twice, its people at the gate), turning against the colony,
> Faction Territories' pace for its new towns (15B), and a former colonist's death only from a real hardship, told by
> name (11B). See "The order to build it in", item 5, for what differs.

> Revised 6 November 2026: the colony is the family (the town a colony becomes is bound to it as a colony, its next
> head chosen by office or standing, no houses, nothing handed down; a colonist's own ties stay personal); council
> tasks and the keeper of secrets gone; renown the colony's and each colonist's own; banishment where it touches; the
> colonies on the region map's Ours lens (the interface decision of 4 November); the releases reordered so a fresh
> start and the hand-over come first, in this save or a new game; the code's names checked at 0.104.0; seventeen
> decisions restated as fifteen.

The plan is ROADMAP.md, "The world and its people": stage 3, approved by the player, who said they really like starting
a new colony or base on the same world map so the world keeps developing, and playing different colonies within it:
expanding the colony, leaving it behind, or starting a whole new scenario or colony in the same world. Approved: many
colonies in play (every system right with several), handing a colony over (a town of a new people, the former colonists
its notables, the council's speaker its head; it keeps its history, writes, trades, asks for help, and can be taken
back), and a fresh start inside the same save (a new place, a group from the old colony or newcomers; the old colony
handed over, kept or abandoned). It builds on the foundation (DESIGN.md, "Homes" to "Ages"; steps 7a and 7b, with
`docs/design/multicolony-audit.md` for what is done and what was left) and on stage 1's region
(`docs/design/stage1-region.md`). The game's side was read in the decompiled 1.6 code (32bitx64bit/RW-Decompiled-1.6).
A season is 15 days, a year 60.

## Summary

- **A fresh start in the same save.** A place picked on the world map with what Rimfolk knows of it; a founding party
  from the old colony (the journey planner, by caravan, vehicles, pods, shuttle or gravship), newcomers made like a new
  game's group, or both; the old colony kept, handed over or abandoned. The new game's character page can't be shown
  honestly mid-game; the game's own mid-game window (the one it offers after a game over) is the honest alternative,
  and any scenario's start parts (who, their limits, their kit, their arrival, Rimfolk's story parts) can shape the new
  colony alone. The history, the calendar and a record of every colony keep it all.
- **Handing a colony over.** From the settlement's world button (beside the game's Abandon) or the colony's card: who
  stays and who goes, each with their wish shown; the colony's meeting gives its view; a ceremony on the map in the
  age's form; when the last who go have left, the settlement becomes the town of a new people made in play (the
  game's own kind of peaceful people of the colony's age), led by the speaker, its people the former colonists, its
  faith the colony's. The game closes the map as it does for any town; Rimfolk keeps the colony's layout so its map,
  when it is made again, is the colony as it was left. Afterwards it writes, trades, invites, sends people home and
  asks for help, at the game's and the region's rates; it can be raided, fall on hard times or turn against the colony;
  it can come back peacefully or be taken by force.
- **Many colonies in play.** The player can move the seat; two councils become one when people move together; the
  region map's Ours lens shows every home and every town the colony once was, each with its card; the pages follow the
  colony in view; and the places 7b left anchored at the seat (the notables, the world events) work for every home, so
  a second colony far away has neighbours with faces too.
- Only the game's own objects (settlements, factions, world pawns, maps), so a world without Rimfolk is an ordinary
  world. Nothing per tick; no new schedule slot. With one colony and the settings off, it plays as before. Five
  releases, a fresh start first and the hand-over second; fifteen decisions at the end.

## This save or a new game

The player, 6 November 2026, is close to wanting something new without giving up the colony they have, and may go on in
this save or start a new game. The design serves both, and the order to build it in follows from it.
- **In this save.** The first release is the fresh start: a new place, newcomers made as a new game's group, a party
  from the old colony or both, and the old colony kept as a second home (the seat moved to the new one on the player's
  word, so the world's attention follows them) or abandoned. The second lets the old colony be handed over instead, in
  the same act or later: it goes on as a town of its own people, its former colonists its notables, writing, trading,
  asking for help, open to be taken back. Handing over also takes the old map off the game's hands: two homes cost
  about twice the map time, a town the colony was costs a town's. Everything the save holds stays: the history, the
  calendars, the peoples' memory, the colony's renown, the ties.
- **A new game.** Nothing in stage 3 is needed to start one: Rimfolk starts as it always has, its seventeen scenarios
  and every system from the first day, and the game's own new-game pages are where a whole new scenario is honest
  (decision 3). The old colony stays in its own save, untouched, to go back to; Rimfolk carries nothing between two
  worlds. Everything in stage 3 works in the new game too, once its first colony wants a second.

## What the game lets a mod do (verified in the 1.6 code)

- **Settle.** `SettleUtility.AddNewHome(tile, faction)` makes the settlement (named by the game) and
  `GetOrGenerateMapUtility.GetOrGenerateMap(tile, Find.World.info.initialMapSize, null)` its map, which is what a
  caravan's Settle does (`SettleInEmptyTileUtility.Settle`). The game's limit is a player preference, not the save's:
  `Prefs.MaxNumberOfPlayerSettlements` (1 by default, 1 to 5 in the options);
  `SettleUtility.PlayerSettlementsCountLimitReached` counts maps that are a player home under a `Settlement` and
  maps made by a gravship landing. A home with no map doesn't count.
- **Abandon.** `SettlementAbandonUtility` asks, then `Settlement.Abandon`: the settlement is destroyed and a new
  `AbandonedSettlement` (a new world object id) marks the tile; the map goes; colonists still on it pass to the world
  as lost, with the game's letter. If no other home is left, everyone gets the game's new-colony hope. `GameEnder` ends
  the game when no free colonist is left on a map, in a caravan, in pods or on a gravship.
- **The game's own fresh start, the archonexus ending** (Ideology: `QuestPart_NewColony` calls
  `MoveColonyUtility.MoveColonyAndReset`): the chosen people and things drop at a new tile, every home's map goes (each
  old home's tile becomes another people's ordinary town when the quest names one), the colony's caravans and pods are
  gone, and the game starts over: research back to the start, every people's relations reset. It is the game's reset,
  not a fresh start in a world that keeps developing, so Rimfolk doesn't use it.
- **Turn a player settlement into another people's.** `WorldObject.SetFaction` only sets the faction (the settlement's
  material is cached with the old colors and must be cleared). `Map.IsPlayerHome` reads the parent's faction, except
  for a map made by a gravship landing, which stays a home whatever its parent. Once it is no longer a home,
  `Settlement.ShouldRemoveMapNow` closes the map as soon as no player pawn is on it and nothing blocks removal; the
  world object stays, with its id. `MapDeiniter` passes every pawn to the world: the player's as lost (the game's
  letter), others as plain world pawns of their faction, and colonists on a hostile parent's map as kidnapped. Things
  on the map go with it. When that town's map is made again (an attack), the game makes its people's ordinary base
  (`Base_Faction`, or a base-generation mod's layouts), not the colony.
- **The gravship (Odyssey).** Taking off from a home without a grav anchor calls `GravshipUtility.AbandonMap`: pawns
  left on the ground are lost, then the settlement is abandoned. With an anchor the home stays.
- **Take a town.** `SettlementDefeatUtility.CheckDefeated` replaces a beaten town with a `DestroyedSettlement` that
  keeps its map, and the last town of a people makes it defeated. A destroyed settlement's map has the game's Settle
  button (`SettleInExistingMapUtility.Settle`), which makes it a home under the max-settlements limit.
- **Make a people.** `FactionGenerator.NewGeneratedFaction` gives it an ideoligion (chosen or generated), a name from
  its def, relations with every people by the def's rules (`TryMakeInitialRelationsWith`), a town placed at random
  unless hidden, and a leader generated from its def; `FactionManager.Add` adds it. `faction.leader` can be set,
  `FactionIdeosTracker.SetPrimary` sets its faith, `Faction.SetRelation` its relations. Goodwill with the player drifts
  toward a natural goodwill (`CheckReachNaturalGoodwill`: outside 50 either side of it, 10 points each 50 days),
  which `GoodwillSituationDef`s (same ideoligion, meme compatibility) move and a mod can add to. Only a temporary
  faction is ever removed (once nothing is left of it); a faction whose def is missing is dropped on load with an error.
- **A people's next leader.** When its leader dies (`Faction.Notify_MemberDied`, then `Notify_LeaderDied`) or is lost,
  the game calls `Faction.TryGenerateNewLeader`, which makes a new pawn of the people's leader kinds (a stranger), then
  writes its letter naming whoever `faction.leader` is by then. A prefix for one people can set the leader itself and
  skip the making, so the game's own letter names Rimfolk's choice.
- **World pawns.** The game never lets go of a pawn who was ever a colonist (`WorldPawnGC`, "Colonist"). World pawns
  age (a mothballed one only ages, `Pawn.TickMothballed`); the game doesn't otherwise end their lives off the map. It
  may redress a free world pawn of a people into that people's groups: a town's `previouslyGeneratedInhabitants` first
  when its map is made, else a small chance for any group (`PawnGenerator`).
- **Starting pawns mid-game.** `Page_ConfigureStartingPawns` is a page of the new-game flow: a scenario sets its rules
  in `PostIdeoChosen` (which also clears the game's custom xenotypes and resets the default drug policy), and its Back
  and Next lead into the world and site pages that start a game. The game's own mid-game window is
  `Dialog_ChooseNewWanderers`, offered after a game over: it sets a fresh `GameInitData`, makes one to six people with
  `StartingPawnUtility` (the same portrait area, character card with randomize, Biotech's xenotype and life stage
  options, health, relations and possessions, and the skills summary as the new-game page), then fires
  `GameEndedWanderersJoin`, which walks them in at a home's edge with their possessions.
- **Arriving.** A scenario's `PlayerPawnsArriveMethod` (standing, drop pods, gravship); transport pods
  (`TransportersArrivalAction_FormCaravan`, `_LandInSpecificCell`, `_VisitSettlement`, `_GiveGift`, `_Trade`); Odyssey's
  passenger shuttle; Vehicle Framework's vehicles (and Medieval Overhaul's balloon and airship) travel as caravans.

## Shared rules

- **Words match the map.** What a text says happened is what the map shows: the ceremony, the farewell and the
  welcome play out; a town the colony was is the colony as it was left when its map is made.
- **The game's own objects.** The town is a `Settlement` of a faction of a game def; its people are world pawns; a
  fresh start is a settlement and map the game's way. Without Rimfolk the town is an ordinary town of an ordinary
  people (only its kept layout is Rimfolk's).
- **The colony is the family** (CLAUDE.md, the player's rule of 5 November 2026). A town the colony was is bound to the
  colony because it was the colony, never as a family or a house: its people are the colony's former people, its
  next head is chosen by office or standing (below: the head's second), it has no founding families (a new town's two
  families are other peoples' story, `RegionFaces`), and nothing is handed down. A colonist's own ties stay personal
  and do their part: who goes where with whom, coming home, moving there, "parted". The scenario's two quarrelling
  families (Bad Blood) are not carried into a fresh start (decision 3).
- **Renown is the colony's and each colonist's own** (DESIGN.md, "Colony renown"). The colony has one name among the
  peoples, read from every home as now (`Renown.Read`), so a new colony is the same colony's and its deeds join the
  rest. A hand-over takes nothing from what the colony did: its people's deeds there stay in the history under the
  colony (the gone count with the living), and its own deeds there (an oath or a truce sworn, its word kept, a pact
  kept) are read under every colony on the ledger's record (`Renown` reads only the homes standing today,
  `Homes.All`). What those who stay hold now (the colony's names for them, years in office, notables who think well of
  them) leaves the colony's renown with them, as for anyone who leaves. Each colonist's own name (`Renown.Own`) is
  theirs wherever they live: a founding party carries theirs, and a former colonist's is said in their town's letters
  and talk where the listener's people know it (`Renown.Own` read for a former colonist too, from the history and the
  peoples' memory; today it reads only the colony's people). The town keeps no renown of its own: it is a people of
  the game, and its regard of the colony is the bond (decision 8).
- **Banishment** (DESIGN.md, "Banishment by the council"). Each home's council banishes its own
  (`BanishMath.MinPeople`: six people at that home, `BanishTracker.ColonyAt`), so a new colony asks no banishment until
  it has six. A colonist whose banishment is under way is leaving and is in no party or hand-over: the hand-over waits
  while one plays out at that home, and a banishment question there is called off once it is handed over. The fights
  the council counts show in a colonist's wish ("at odds with most here", a reason to go).
- **The ledger and the history own it.** A new record, `LedgerColony`, for every colony ever founded: its key (the
  settlement id), name, founding tick, its mother colony, how it ended (abandoned, handed over, lost, taken back) and
  when, the town or ruin it left. A town the colony was is a `LedgerTown` of a new kind, `Former`, whose id is the
  colony's own key (the same settlement), so `History.At` finds the colony's past under the colony ref and the town's
  under the town ref. New history kinds in a `colony:` family: `founded`, `seat`, `merged`, `handedOver`,
  `abandoned`, `takenBack`, `homecoming`, all milestones. Former colonists are ledger persons kept with a new reason,
  `KeepFor.Former`. The colony's ties made at a home given up or handed over (a pact, a truce, fealty, a debt) stay
  the colony's: the diplomacy reads a tie whose home is no longer a home at the seat (`Diplomacy.HomeOf`), so nothing
  is moved.
- **Per colony through `ColonyBook`.** Everything new kept per home is an `IColonyEntry` in a book, so a lost home's
  entry is kept thirty days and one colony plays as before.
- **One voice.** `Tell.Letter` for a town's news, `Tell.Minor` for the small news, `Tell.Ask` after `Tell.CanAsk` for
  questions that can wait. The hand-over and founding windows answer the player's own clicks. This week lists a
  hand-over or a founding planned, never with a timer.
- **No new schedule slot.** The pending hand-over and the town's hour ride `Pass.Region` (hourly), the town's day
  `Pass.DailyRegion`, the seat and the merge the council's hour; each with its pass entry and timed.
- **Settings.** A section "Many colonies": fresh starts, newcomers, handing over, the bond in goodwill, the town's
  life (letters, asks, homecomings). Off, a part offers nothing new and keeps its records.
- **Every age.** New `Ages.Form` features (word by our age, rite by theirs):

| Feature | Tribal | Medieval | Industrial | Spacer and beyond |
|---|---|---|---|---|
| The seat | the elders' fire | the council's hall | the town hall | the administration |
| A founding party | a band to make a new hearth | settlers with a charter and carts | a settlement company | a colony mission |
| A hand-over | the fire passed on, the totem left | a charter and the keys | a town charter, the flag lowered | a charter registered, the codes handed over |
| A call for newcomers | word sent along the trade paths | a call for settlers at the markets | an advertisement by radio | a colonization call by comms |
| Coming back | two fires one again | the charter returned, oaths renewed | a merger signed | the charter dissolved |

## 1. A fresh start in the same save

### Choosing a place
"Found a new colony" on a colony's card (the Ours lens, 3c) starts the game's world targeter. For the tile under
the cursor Rimfolk's panel adds to the game's own tile view: whose land it is (Faction Territories), the towns within
reach and their peoples' stance, how remote it would be (stage 1's rule for a tile), the days of travel from each home
by the colony's means, and the game's own checks (a valid tile, the goodwill warning near a town, the max-settlements
limit, named with where to change it: Options, Gameplay; Rimfolk never changes it, decision 5).

### Choosing who
- **From the colony:** the same who-stays-who-goes list as the hand-over (section 2), from any home, with each one's
  wish.
- **Newcomers:** a window built as the game's own after a game over (decision 3): one to six people (or the
  scenario's count and choice), each randomized, with Biotech's xenotype and life stage options, health, relations,
  possessions and the skills summary, made from the player faction's kind as at a new game; their faith the colony's
  or one the world knows. The game's `GameInitData` exists only while the window is open and is cleared when it
  closes, by the X too. **Why not the new game's page:** it is a step of the new-game flow; its scenario rules come
  from `PostIdeoChosen`, which mid-game would also clear the game's custom xenotypes and reset the default drug
  policy, its Back and Next lead into pages that start a game, and character-editor mods hook it expecting one. The
  game's own mid-game window is the honest alternative; what it lacks is the reordering of chosen and left-behind
  people and other mods' editors.
- **Both:** the colony's party settles first, the newcomers walk in within the day and meet them.
- **Newcomers' price** (decision 4): free only when the old colony is handed over or abandoned in the same act (a
  true fresh start, as the game's new wanderers are free); with the old colony kept they come at most once a year and
  the colony pays their passage from its stores (silver and food worth about their kit, by the call's age form).

### A scenario's start (decision 3)
The player may shape the new colony by any scenario (vanilla's, Rimfolk's seventeen, a mod's). Carried, for the new
colony alone: the people (its count and choice, required kinds, xenotypes, ages and skills, read from its
`ScenPart_ConfigPage_ConfigureStartingPawns*` part's fields, never by calling `PostIdeoChosen`), the parts that shape
starting people (forced traits and hediffs, through their pawn hooks), the kit (`PlayerStartingThings`), the
arrival, the starting animals, and Rimfolk's own parts on that colony's council, calendar and people
(`ScenPart_CouncilSeats`, `Days`, `Goal`, `Ways`, `Foe`, `Bond`, `Secret`, `Event`, `Wards`, `Situation`, `Debt`,
`Deserters`, and `Beholden`'s grant as a tie of that colony, without its warm start: goodwill is the whole colony's).
Not carried, and listed in the window: what changes the whole game (the player faction, starting research, rules for
every map, world conditions, incidents switched off) and Bad Blood's two quarrelling families
(`ScenPart_CouncilFamilies`: a feud along family lines). The gravship start is offered only when the old colony is
left behind. A scenario's kit comes as the scenario defines it (the player's chosen start); Rimfolk's own kits by age
hold only what the colony has unlocked.

### What they bring
From the colony: the journey planner's packing list (`JourneyPack`), with a founding kit suggested by age: food for
the road and fifteen days, medicine, tools and weapons, and what raises the first shelter by its means (wood for a
tribe or a medieval colony, steel and components for industry and beyond), from what the colony has and has unlocked;
pack animals, carts and vehicles; what pods carry; the gravship carries itself. Newcomers: their possessions, and the
scenario's kit or a kit by age from the old colony's stores.

What the founders carry besides: each one's own name for their deeds; the colony's names for them (the Healer, the
Builder) from one colony's days, since a name kept without a colony belongs to its holder's colony
(`EpithetMath.ForColony`), while a name earned at one colony of several stays that colony's and is given again there
in its next naming round; the new colony's founders and firstborn are its own (7b's rule: those there its first two
days).

### How they arrive
- **From the colony,** by the game's own travel: a caravan on foot or with pack animals and carts (every age),
  vehicles (Vehicle Framework; Medieval Overhaul's balloon and airship), transport pods (with their research), the
  passenger shuttle (Odyssey), the Empire's shuttle permit for one who holds it, the gravship. Rimfolk adds the
  journey plan's departure (the evening meal together, the farewell at the gate, the hour), a question when the
  caravan reaches the tile ("Settle here?", which runs the game's own Settle; pods first form their caravan, as the
  game does), and the first night: the founders gather at a fire, the eldest or the speaker-to-be speaks.
- **Newcomers,** at the new place as their start says: the settlement and map made the game's way, then they walk in
  from the edge (every age, with a cart and a pack animal for a medieval group), come down in pods (the scenario's
  crash, or the colony's pods if it has them), or land by shuttle (Odyssey, if unlocked). Never a means the colony or
  the scenario doesn't have.

### The old colony
Kept (a second home, under the game's limit; the seat moves to the new one on the player's word, section 3a),
handed over (section 2, in the same act: the hand-over waits for those who go), or abandoned (the game's own abandon
once those who go have left; the `LedgerColony` ends "abandoned", the game's ruin is known to the ledger as the old
home's, its council carried by its people by 7a's rule, and its founding day becomes a remembrance day of the colony
they settle).

### The history and the calendar
The new colony's `LedgerColony` (its mother colony named), a milestone `colony:founded` (the founders "did", the
mother "about"), its calendar from its founding day (`CalendarMath.FoundedTick`), its own founders and firstborn
names (7b's rule), the scenario's days and seats if one shaped it; on the mother's calendar, if kept, "the parting
day", which it may keep as a tradition; the year letter's new part; a new council of its own (7a), its first meeting
called by the founders.

**Ages.** The founding party, the call for newcomers and the first night by the forms table; arrivals by the colony's
means and the scenario's.
**Bad side.** The game's own: caravan incidents on the road, the storyteller's threats at the new colony's small
points, a first winter without walls; the old colony weaker by those who left; "parted" for close bonds split between
the two colonies, for a season. Nothing new hurts anyone.
**Mods.** Faction Territories: the tile's owner and its goodwill; its encounters on the road. Vanilla Outposts: a new
home can be chosen as a delivery colony with the mod's own button; an outpost's people can join the party by leaving
it the mod's way. The Empire: a titled colonist's throne-room needs go with them; the shuttle permit as a means.
Odyssey: the gravship (with or without an anchor left behind) and the passenger shuttle. Vehicle Framework: vehicle
caravans.
**Save and performance.** A new colony is a new map: the game's own cost, which the window names ("two colonies cost
about twice the map time"); Rimfolk's books gain one entry per home. `LedgerColony` records: one per colony ever, 24 at
most (the oldest ended ones fold into the history). The newcomers' window holds its people only while open; those not
chosen are discarded as the game's window discards them. The founding plan is a journey plan (one per home).
**Tests.** Pure (`FoundingMath`, `ScenarioStartMath`): the founding kit by age and unlocked content, the arrival ways
by age, means and scenario, the newcomers' price and yearly cap, which scenario parts carry (Bad Blood's families
not), the tile panel's facts, the names a founder carries; the creation window's `GameInitData` cleared on every close
(a check in the self-check); `ManyHomesTests` for a founding's council, calendar and names.

## 2. Handing a colony over

### What the player does
"Hand over" on the settlement's world buttons (a postfix on the player settlement's gizmos, beside the game's
Abandon) or on the colony's card opens one window:
- **Who stays and who goes.** Every colonist, child, animal, prisoner and slave of that home. Each grown-up shows a
  wish worked out from their own ties (spouse, lover, children and parents go where they go; close friends and
  rivals; at odds with most here, by the fights the council counts; their own bed and room; their office; their age;
  their faith's relic or shrine there) as "wants to stay", "wants to go" or "torn", with the reasons. Children follow
  their parents unless moved; prisoners and slaves are set free or go with those who go; a lodger of another people
  (their envoy, a ward, a hostage) goes with those who go, the colony's charge and not the place's. Those away from it
  (a caravan, an errand abroad as envoy, youth or hostage) are listed as going and come back to the seat. Those who go
  say where: another home, a founding party (section 1), or a caravan the player forms.
- **The head:** the speaker if they stay, else an office holder who stays (by the seats' order, `OfficeDef.order`),
  else the one the others think best of (the colony's view of them, `Social/Gossip/`); the player can pick another.
- **The head's second**, the next head by office or standing, never by blood: the next office holder who stays by
  the same order, else the one the others think best of after the head; the player can pick another. In the notables
  it fills the code's `NotableRole.Heir`, titled for this town by its age (the headman's second, the reeve's deputy,
  the deputy mayor, the deputy administrator).
- **The people's name** ("the folk of Oakhold", by the age's word; the player may rename) and **terms** (decision 9):
  a free town owing nothing, or a pact (stage 2's defence or trade pact, a `Pact` tie for a year, renewed by their
  letter as any). Fealty is not offered at the hand-over: the town may come under the colony's protection later by
  stage 2's own rule (a struggling town that asks).
- **What stays with the place**, listed as the game's own abandon would: the stores and goods (the town keeps them; its
  trade stock is its people's), buildings, fields, animals that stay, a relic (theirs from then on), held entities and
  anything the town can't keep (gone with the map, as when abandoning). Mechs go with their mechanitor.
- **The checks:** at least three grown-ups stay (fewer is a homestead, not a town); someone stays the player's in
  another home, a caravan, pods or a gravship (else the game would end); no hand-over during a fight at that home or
  while a banishment plays out there.

The colony's meeting gives its view first (decision 2): at its next meeting, or at once when the player asks, the
council votes by each member's wish and their regard for the player's council; going against its vote costs trust
among those who stay. The window then sets the day.

### What the game does, in order
1. **The ceremony** (a play-out, at the meeting spot): those who stay and those who go gather; the head speaks (a
   bubble), the colony's banner, totem, keys or codes change hands by the age's form; those who go say farewell to
   those they're close to (`Together.Can`, the social log). Those who go then leave: the caravan forms, the pods or
   the shuttle launch, the gravship takes off.
2. **When the last who go have left the map** (`Pass.Region` notices, or at once if none are there):
   - Rimfolk keeps the colony's layout (decision 7): its buildings, floors, roofs and mined-out rock, as a compact
     record (below).
   - It makes the people: `NewGeneratedFaction` with the def chosen for the colony's age (decision 6), hidden at
     creation so the game places no random town, then shown; its primary faith the colony's (`SetPrimary`); its leader
     the head; its relations: allied with the player (goodwill 80), with every other people half the player's goodwill
     with them within the def's own rules (permanent enemies stay so); `FactionManager.Add`.
   - Those who stay and the animals that stay get the new faction (`Pawn.SetFaction`); prisoners and slaves were set
     free or went with those who go (the window said which: the town keeps no prisoners).
   - The settlement's faction is set and its cached material cleared; a gravship-landed home is closed by Rimfolk,
     since the game would still count it a home.
   - The game closes the map as it does any town's: those who stay become world pawns of their people, no "lost" letter.
3. **The records:** the `LedgerColony` ends "handed over"; a `Former` town record (prosperity seeded from the colony's
   wealth per head, 30 to 70, its founding the colony's founding day, its founder the colony's first speaker); its
   notables: Head, the head's second, Envoy (the most social), Trader (the best trader), the rest ledger persons kept
   with `KeepFor.Former` (the game keeps former colonists anyway); the colony's council, calendar and books lost by the
   usual rule (kept thirty days, then let go); the milestone `colony:handedOver`; a letter ("Oakhold is the Oakfolk's
   now") pointing at the town.

### The new people
- **Its def** (decision 6): a peaceful people of the colony's age that the game already knows: human, not hidden, not
  a permanent or natural enemy, no fixed name, no royal titles; the vanilla def first (tribal: `TribeCivil`;
  industrial: `OutlanderCivil`), else a loaded mod's peaceful people of that age (a medieval one with Medieval
  Overhaul, a spacer one if a mod has one), else the nearest age. Its pawns, caravans and gear follow that def; its
  words follow the colony's age at the hand-over, kept on the people's record.
- **Its heads.** When the head dies or is lost, the second leads: Rimfolk sets them as the people's leader in the
  game's own succession (a prefix on `Faction.TryGenerateNewLeader` for this people only), so the game's letter of
  the new leader names them and no stranger is made; a new second is chosen among the town's people by the same
  rule, former colonists first.
- **The bond in goodwill** (decision 8): a `GoodwillSituationDef`, "once of [colony]", +30 natural goodwill while one
  of the former colonists heads it, +15 after, gone after a war between them or a taking by force. The game's own
  drift and same-faith situation do the rest, so the bond holds unless the colony wrongs them.
- **Its growth** (decision 15): with Faction Territories its territory is drawn around it as for any town; new towns
  of its own only at Rimfolk's pace (a flourishing town, at most one founded each 90 days), through the existing hook
  on Faction Territories' expansion.

### What it does afterwards
- **Letters.** Its head writes as a notable does (the neighbour letters, at most a season apart for news of its own),
  in the voice of someone who was the colony's speaker: new facts `town:former` (the town was the colony),
  `them:former` (they were one of the colony), `{OLDCOLONY}`, the years since; their talk remembers the colony's days,
  and a former colonist's own name for their deeds where the listener's people know it.
- **Trade.** The game's own: a caravan or pods trade at the town; its trader caravans come as any people's, and when
  one is theirs its Trader comes with it (a face the colony knows).
- **Its days.** It keeps the colony's founding day and the day of the hand-over, and invites the colony to one a
  year at most (the invitation missions).
- **Asks for help** in hard times (stage 1's asks of a struggling town), in the words of people who were the colony.
- **Homecomings and moving there.** A former colonist (not the head) whose spouse, child or close friend is in the
  colony, or whose town struggles, may ask to come home: once a season per town at most, `Tell.Ask`; yes, they walk
  in from the town's side and rejoin. A colonist whose own people live in the town (a spouse, a child, a close friend
  among those who stayed) may ask to go and live there: a reason of its own beside the moving-on questions'
  (`Social/MovingOn.cs`), asked of the council as they are; going, they join the town's people.
- **The town's map** is made only when the game makes it (an attack, a fight the colony joins, taking it back):
  from the kept layout, with its people at home in it (below).

### Taking it back
- **Peacefully, the colony asks** (a letter to the head): the answer weighs the head's regard, goodwill, the town's
  fortune (a struggling town is readier), the years since and how much it has grown. Yes: the settlement becomes the
  player's again, its map is made from the kept layout (the game's player-base map, then the layout), its former
  colonists walk out of their rooms as colonists, and up to three of its other people choose to join; the people,
  with no town left, is made temporary so the game lets it go (it stays a people if it has other towns). Told as a
  milestone `colony:takenBack`; the game's max-settlements limit applies.
- **Peacefully, the town asks** (with the town's life, release 5): after a long struggle or under a war it can't win,
  with goodwill 60 or more, its head asks to come back under the colony.
- **By force**, as the game allows: attack the town (its map from the kept layout, its people's defenders by the
  game's own group and lord, its notables at home among them), beat it (the game's destroyed settlement keeps the
  map), and settle it with the game's Settle button. The bond ends; its people, and every people of the same faith,
  remember a heavy wrong (decision 10).

### Keeping the layout (decision 7)
At the hand-over, over the colony's built area (the home area's bounds and a margin): per cell, the building there
(def, stuff, rotation; multi-cell ones at their origin), the floor, a built roof and whether natural rock was mined
out, as small numbered grids against a table of def names, compressed the way the game saves its own grids (bytes,
deflated): about 10 to 40 KB for a mid-game base, 64 KB at most (past that, roofs then floors are dropped first).
Items, plants, filth and art's tales are not kept. When the town's map is made, the game's terrain and rock come from
the same tile, so the layout fits; Rimfolk stands the game's base generation down for that map only (a guarded prefix
on `GenStep_Settlement`, and on a base-generation mod's own step if one runs, read before patching), places the
layout (`Sketch`, as the game places its own structures), then the people: the game's settlement group under its
defend-the-town lord, and the notables at home. A def the game no longer has is left out. Without Rimfolk, or with
the record lost, the game makes its ordinary base.

### The bad side (the harm rule)
- **Raided** (release 5): a world event "a raid on [town]", at the world events' own rate, only for a town the colony
  was, in detail. Its head's runner, rider, radio or comms (by its means) asks for help: send fighters by the colony's
  means, who arrive on the town's map with its defenders and the raiders (a real fight, the game's own harm), or not.
  Unanswered, the outcome follows the game's measure of the two sides' points: the town loses goods and prosperity
  and some of its other people; a former colonist dies only in a fight the player sees, or of old age by the
  notables' rule (decision 11).
- **Hard times:** stage 1's prosperity: famine, plague and war bring asks. The region never gives up a people's seat
  (`RiseFallMath.MayGiveUp`), and this town is its people's only one, so long struggling (`RiseFallMath.Struggling`)
  it asks instead to come back under the colony (above); refused twice, its people come to the colony's gate, the
  former colonists first, asking to come home, and the town is given up as a ruin.
- **Turning against the colony:** the colony's wrongs (the faction memory, the ledger's grudges, a broken pact) and the
  game's drift lower its goodwill; when the head dies the second leads, and a second with a low regard of the colony
  cools the town. Hostile, it raids at the storyteller's rate with its def's raiders (the game may redress a former
  colonist into them, and the letter names them), and stage 1's raiders' camp rule can pick it.
- **Parted:** colonists whose spouse, lover, child, parent or close friend stayed (or went) have a season's mood
  "parted from [name]" with a hint (letters, an invitation, a visit); those sent against their wish a season's
  "uprooted" (decision 2).

**Ages.** The ceremony, the letters' channels, the call for help and coming back by the forms table; the head's title
by their age (headman, reeve, mayor, administrator) and the second's (above); the Empire keeps its titles: a titled
colonist who stays keeps their title and honor, and the Empire is never the new people.

**Mods.**
- *Faction Territories:* territory drawn around the town; its invasions can take it (the history's `world:taken`; the
  former colonists who live come as refugees); its vassalage is not used at the hand-over (decision 9), and stage 2's
  fealty uses it later as it does for the hamlet.
- *Vanilla Outposts:* outposts sending to that colony are named in the window; the player points them at another home
  with the mod's own button. Their pawns stay the player's.
- *The Empire:* titles stay with their holders; a titled head's throne-room needs end with the faction change, as
  the game rules.
- *Odyssey's gravship:* leaving without a grav anchor with people staying: the hand-over runs instead of the game's
  abandon (a prefix on `GravshipUtility.AbandonMap` for that map only), after the takeoff, so the layout is the
  ground the ship left.
- *Factional War, Hospitality, quests:* the town's people is a people like any other; they may fight on a home, send
  guests, give quests. *Base generation mods, Real Ruins:* left alone except for the map of a town the colony was.

**Save and performance.** Per town the colony was: the town record and its persons (12 at most), the kept layout (64
KB at most), the people (the game's: relations with every people, a few KB; its faith shared), a pact tie. Six such
towns at most (the game allows five homes). The former colonists are kept by the game anyway. The layout is read once
(a scan of the map's things inside the game's long event) and placed once per map made. The town's day rides
`Pass.DailyRegion`; one more people in every faction loop. Nothing per tick. Each new list gets its model in
`Tools/save_estimate.py`.

**Tests.** Pure (`HandOverMath`, `FormerMath`, `LayoutMath`): the checks (three stay, someone stays the player's, no
banishment under way, max settlements), each grown-up's wish from their own ties, the head's and the second's choice by
office and standing, relations carried, the prosperity seed, the bond by state, the def's choice by age from def
descriptions, the layout's encoding round trip, size cap and dropping order, the take-back answer, the homecoming's
chance and caps. `ManyHomesTests`: a hand-over that leaves one colony plays as one colony; `LedgerTests` and
`HistoryTests` for the town and the new kinds; `RenownTests`: the colony's renown keeps a handed-over home's deeds, a
former colonist's own name.

## 3. Many colonies in play: what is left

### 3a. Moving the seat
`Homes.SetSeat` exists with no button. **What the player does:** "Make this the seat" on a colony's card (3c), for a
home on the ground (a home in orbit can't be one: nobody walks in, as `HappeningTracker.HomeMap` already rules), at most
once a season. **What moves:** what the colony as a whole has, which reads the seat: the events and letters about no
home of their own (about fifty-five callers of `HomeMap`), the seat's council (`comp.offices` and the others: the
speaker who signs letters to the world), the seat's calendar's world days and nemeses from then on (the old seat keeps
those it had), the region's notables and world events until every home has its own (3d), the region map's default, the
year letter's first part. What doesn't move: the game's founders and firstborn (the colony settled with the game),
names, each colony's own council and calendar. **Told:** a milestone `colony:seat`; the old seat's people trust the
council a little less (trust, not a mood); a lost seat goes to the first home founded on the ground, as now. The seat
comes with the fresh start (release 1): with the old colony kept, it is how the world's attention follows the player to
the new one.

### 3b. Two councils become one (decision 12)
When a colony's people come to live at another (their home given up or handed over, or a founding party that joins
an existing home), 7a's rules already hold: a moving officer steps down, the old council is kept thirty days, then let
go. What merges, once the newcomers have lived a day at the new home:
- *Traditions* (the dilemmas' and the calendar's): one only the newcomers kept becomes the colony's at half its
  strength, and lapses by the usual rule unless kept; one both kept keeps the stronger.
- *Elections*: when the newcomers are a third or more of the grown-ups, the next election comes within the season, so
  they have a say. Their decrees ended with their council.
- *Names*: names under the old colony's key are looked at again in the next naming round; where both colonies have a
  holder, the better keeps it, and a merge makes no rival in its first season.
- *The calendar*: the old colony's founding day becomes a remembrance day of the new one while some of its people live
  there (inside the cap of twelve written days).
- *The welcome*: a meal together on the first evening (the play-out's shared meal), the speaker's greeting in a
  bubble; a milestone `colony:merged`. Old hands and newcomers keep apart at first through the groups' own rule
  (people of two colonies form no group), which is the friction the game already has.

### 3c. What the player sees switching between colonies
- **The colonies on the region map's Ours lens** (the player's interface decision 7, 4 November 2026; no page of their
  own, docs/design/interface.md, "Where stage 2 and stage 3 go"). Its list shows each home (name, seat, grown-ups and
  children, its speaker, mood and strain, wealth, its next day, a threat now), each town the colony was (its head,
  fortune, stance) and the colonies of the past (founded, ended how), each opening its card. A colony's card has the
  acts: go there (the map in view changes), make it the seat, hand it over, found a new colony. No new bar button, tab
  or view: the windows' rule stands. The list, the cards and their acts come with the fresh start (release 1).
- **A colony chip in every view's header** names the colony in view and leads to another (today `ColonyView.Header`
  heads only the Decrees, Ambition, Dilemmas and Calendar pages).
- **The pages follow the colony in view.** The People view's pages (the table, moods, bonds, faiths, and Renown's
  people known by name, the colony's name staying one) show the colony in view with more than one home (everyone a
  click away); the region window's Peoples and Here and away views and the town panels show the colony in view's
  neighbours (the seat's today); the Chronicle is filtered by colony (all a click away); the HUD's council panel names
  the colony.
- Already so: messages and letters name the settlement, This week lists every colony named, the year letter has a
  part per colony, the Overview's wealth tile names each, the region map draws the colony in view's region.

### 3d. What 7b left that matters in real play
- **The notables near every home** (decision 14). `NotableBook.Home` is the seat, so a second colony's nearby towns
  have no faces, letters or calls. Each further home gets its nearest two towns staffed (beside the seat's four near,
  two dealt with and two new), and acquaintances live in their people's town nearest their colony. (The region's towns
  in detail are per home already.)
- **World events across the homes.** `WorldSituations` places events from the seat; it picks the home first (weighted
  by its people), so a far colony hears of the world near it. The two running slots stay two.
- **The colony's character per colony** (decision 13): the names for a place and its work (famed hall, walled,
  artful, hallowed, green-fingered, builders...) are about one place and its people, so each colony earns its own;
  "walled" can't be said of a colony with no walls. The five ways follow each council's choices too; a colony founded
  from another starts with half its mother's, newcomers start fresh.
- **Small ones:** the frost alert for every home, not only the one in view (the harvest itself is per map already);
  an outsider's distance and relatives among the colony from their own colony; the favour bearer and the courier's
  fallback by the colony the answer is about; growth needs' arrivals at their own home.
- **Stays shared on purpose:** the chronicle and annals (each colony named), the wishes' list (counted per colony),
  trust (per person), the heart's episode cooldown, the remembered camps, the faith a decree names (one per people in
  the game), the colony's age (research is the faction's; the means are per map already: a console at that home), the
  colony's renown (one name among the peoples).

**Ages.** The seat by its age's word (the table); everything else is the systems' own forms.
**Bad side.** None new: a seat moved costs a little trust; a merge's friction is the groups' own.
**Mods.** Faction Territories' territory and Vanilla Outposts' delivery colony are read per home already
(`WorldMechanics.OurHoldings(colony)`); nothing else changes.
**Save and performance.** The notables' cap grows by eight people per further home, all made by the existing rules
(two an hour at most); two more towns per home in the notables' hour. The character per colony: five ways and the
names per home in a `ColonyBook` (under 2 KB a home). Nothing per tick.
**Tests.** Pure rules beside `ColonyStateMath`: the seat (ground only, once a season, what reads it), the merge
(traditions' union and strengths, the early election, names' keys), the character's split (an old save's single
character to the seat; a founded colony's half). `ManyHomesTests`: with one colony every answer as before.

## The order to build it in

Stage 3 is waves 15 and 16 of the plan, after stage 2. The fresh start and the hand-over come first, for the player who
wants something new without giving up the colony they have (above, "This save or a new game"); the polish for many
colonies at once after. Each release plays as before with its settings off and one colony, with every check green
(version numbers as they fall).

1. **A fresh start** (section 1 without a scenario's parts): the place picker and its panel, the who-goes list with
   wishes, the newcomers' window and their price, the journey plan to a tile with the founding kit, the arrival ways,
   the settling question and the first night, `LedgerColony` and the `colony:` kinds, the names founders carry, the old
   colony kept or abandoned; the seat's button and what moves (3a); the colonies on the Ours lens with their cards and
   acts, and the colony chip (3c). Needs only 7b.

   **Built (release 1, 6 November 2026; `Source/ColonyCouncil/Colonies/`, DESIGN.md "A fresh start").** As above, with
   the decisions 1A, 2A, 3A, 4A and 5B. What differs from this section and from section 1:
   - *Who goes* is the home's free people, grown-ups and children; animals go through the journey planner (pack animals
     and mounts added there); prisoners and slaves aren't in the list, and with the old colony abandoned they are the
     game's to settle as its Abandon does. A wish's reasons are the ties above but a faith's relic or shrine.
   - *Newcomers* are made with the game's window when the colony stands (a letter asks), or at once when nobody goes
     from the colony; with a party they are not made in the founding window. They walk in from the edge with their
     possessions; a medieval group's cart and pack animal, and newcomers by pods or shuttle, are left for the content
     pass. Their faith is the one the game gives the player's people (no choice of another).
   - *The means*: caravan by the journey planner, the colony's own pods or shuttle where it has a launcher, the
     gravship; Vehicle Framework's vehicles and the Empire's shuttle permit are not offered (a vehicle caravan formed by
     the player and stopped at the place is asked "Settle here?" as any caravan is). The kit is fitted to 90% of the
     party's room (fewer days of food first, then less of the materials).
   - *The old colony abandoned* waits for anyone of the party still on its map and says so once; the player's own
     Abandon is always there. Its council goes with its people to the new home.
   - *The game's limit* (5B) is raised as the founding starts, and set back by one once the old colony is abandoned or
     the founding is called off, while it is still what Rimfolk set.
   - *The records*: `LedgerColony` is written only once a second colony is founded (a save of one colony keeps none);
     the history kinds `colony:founded`, `colony:seat`, `colony:abandoned` are written (the others are named for
     releases 2 and 4); the parting day is a calendar kind of its own (`Parting`), kept as a remembrance. No first
     meeting is called by the founders: the new council keeps 7a's own rhythm. The year letter already had a part per
     colony; This week doesn't list a founding planned (its journey shows as any journey does).
   - *The Ours lens*: a home's next day is named by its date (no countdown); a colony of the past opens a card from its
     record. Tests: `Tools/Tests/FoundingTests.cs` (the pure rules and the game's calls) beside `ManyHomesTests`.
2. **Handing a colony over** (section 2): the window, the meeting's vote, the ceremony, the new people (def choice,
   its heads, the bond), the town and its notables, the kept layout and its placing, letters, trade and its days,
   homecomings and moving there, taking it back peacefully and by force; the gravship case; the old colony handed over
   in the same act as a fresh start, with free newcomers. Needs 1 (the who-goes list, the colony record, the cards)
   and stage 1's towns.

   **Built (release 2a, 6 November 2026; `Source/ColonyCouncil/Colonies/HandOver*.cs`, `HandOvers.cs`,
   `World/Ledger/LedgerFormers.cs`; DESIGN.md "A town of our own").** The window, the meeting's vote, the day, the
   ceremony, the people made and the town, its records and notables, its heads, the bond, the gravship case and the
   hand-over in the same act as a fresh start, with the decisions 2A, 6B, 8A and 9A. Release 2b is what the town does
   afterwards (its letters, trade, days, homecomings and moving there), taking it back (10A) and keeping the layout
   (7A), marked `// release 2b` in the code. What differs from section 2:
   - *The people's kind* is Rimfolk's own four by the colony's age (decision 6B, not the vanilla def first): tribal and
     medieval on the game's tribal parent, industrial and spacer on its outlander parent; with a mod of a medieval (or
     spacer) people loaded, Medieval Overhaul's first, its pawn groups and traders are lent to Rimfolk's people of that
     age. The window says a save holding one needs Rimfolk.
   - *The meeting* is a gathering of its own, called in the evening after the window, not the colony's next ordinary
     meeting (whose business is ambitions and their pitches); the vote counts those who came, every member if nobody
     gathered. The player's go-ahead sets the day: the next day, said by its date. Going against the vote costs 6 trust
     among those who stay, which they carry into the town.
   - *The ceremony*: the banner, totem, keys or codes change hands in words, face to face, and in the social log, not as
     an item (the game has none for it, and one made for it would be a thing the town couldn't keep); the town's letter
     says it happened only when it did. Farewells are each who goes with their two closest who stay.
   - *Who stays*: a guest of another people's goes with those who go, and the town waits for them to leave; a colonist
     of another home visiting goes home first; anyone away from home is listed as going and stays the colony's
     wherever they are (nobody is moved to the seat). Prisoners set free go back to their own people; slaves set free
     live in the town as its own; "taken along" is the caravan the player forms, and any still there are set free.
     Mechs left behind are the town's.
   - *The town's founder* on its record is one of the colony's founders who stayed (else the head): no record of the
     colony's first speaker is kept.
   - *Relations*: half the colony's goodwill with each other people, at war only while the colony is (and the half is
     below nothing); a permanent or a natural enemy (the defs' own -100 and -80) stays one.
   - *The terms*: the pact is the seat's, or the colony's as a whole while it has no other home yet (a new colony on its
     way). Fealty is never offered, as decided.
   - *The notables*: a town the colony was stays among the colony's neighbours wherever it lies, beside the usual
     towns, and a place left empty is filled from its former people first.
   - *The gravship*: the prefix on `GravshipUtility.AbandonMap` makes the town only once the ceremony is over and three
     grown-ups are on the ground; before that, the game abandons the home as always. Anyone of those who go left on the
     ground stays with the town.
   - *In the same act*: the founding window's third choice opens the hand-over window with the party as those who go;
     the founding waits inside the hand-over and sets out when the ceremony is over. "Found a new colony with them" in
     the hand-over window opens the place picker with the third choice set.
   - *This week* doesn't list a hand-over (as release 1's founding); the colony's card says where it stands.

   **Built (release 2b, 6 November 2026; `Source/ColonyCouncil/Colonies/TownAfterMath.cs`, `TownLayout.cs`,
   `TownsAfter.cs`, `TownTakeBack.cs`, `TownAfterPatches.cs`, `TownAfterFacts.cs`, `Missions/TownDayMission.cs`,
   `Social/MovingOnTown.cs`; DESIGN.md "The town afterwards").** The kept layout and the town's map from it (7A), its
   letters, trade, days, homecomings and moving there, and taking it back by letter and by force (10A). What differs from
   section 2:
   - *The layout*: two buildings a cell at most (the edifice, and what lies under or on it: a conduit, a lamp), each at
     its origin; "the rock that is gone" is every cell of the area with no natural rock at the hand-over, so a map whose
     ground came out otherwise (another landing on the same place turned its seed) still has the colony's open ground.
     Past 64 KB, after the roofs and the floors the lesser buildings go, then nothing is kept. The player's own colony
     (the Atia save of 5 November: 166 by 156 cells kept, 935 buildings, 140 defs) takes about 7.5 KB, most of it the
     table of names; made-up bases in the tests 3 KB (an 80 by 80 home area) to 14 KB (most of a 275 map); less than the
     design's 10 to 40 (a dev-mode log line gives each layout's size at the hand-over).
   - *Base generation*: one guarded prefix covers the game's step and the Vanilla Expanded Framework's (its KCSG
     GenStep_Settlement derives from the game's, and Vanilla Base Generation Expanded gives it to the game's tribal and
     outlander peoples, ours among them), so no mod's own step is patched; the game's power step also stands down for
     that map.
   - *Its people on the town's map*: its notables at home (their own beds), and the game's settlement group with the
     points the game gives a town less what the notables bring; its other former colonists stay in the world. Taken back
     by its people's word, every former colonist free at home comes.
   - *Letters*: written by the people's leader and carried to the gate by their messenger, as the answers to the colony's
     own letters are (a message over comms in the spacer age; the word comes the old way when nobody of theirs can come).
   - *Trade*: the Trader notable is the one who trades in a caravan of theirs, in place of a stranger the game makes.
   - *Its days*: a mission of its own on the invitation's worker (`Rimfolk_Mission_TownDay`, base chance 0), a few days
     before the day; the founding day from the first season after the hand-over, the hand-over's from its first year.
   - *Homecomings*: a close friend is 40 regard both ways; each former colonist is asked about once; the one they came
     home to meets them face to face (the welcome plays out).
   - *Moving there*: a moving-on question of its own (`Rimfolk_DlmMove_Town`); going, they count among the town's former
     colonists.
   - *Asking for it back*: a letter purpose of the neighbour letters' own (`TakeBack`), to the town's head only, at peace,
     once a year and while the colony limit allows one more home. A yes is taken up by the player (the letter or the
     town's card) and lapses after a year; the same settlement becomes the colony's (its key the colony's, the colony's
     record standing again), its map at the size the colony was. The people's leader steps aside; with no other town the
     people is made temporary and defeated, and Rimfolk's records drop it when saved.
   - *By force*: noticed in the region's hour (the game's destroyed settlement at its place); its people remember the
     wrong only when they keep another town (else the game counts them defeated and only the ledger's grudge stays); the
     history has `colony:townBeaten` for the beating and `colony:takenBack` once it is settled.
   - *Ages*: coming back is a feature of its own (`AgeFeature.Back`, the forms table's "Coming back" row).
   - *Setting*: one, "The town's life" (letters, trade, days, homecomings, moving there); the kept layout and taking it
     back are always on with handing over.
3. **A scenario's start** (section 1's scenario parts): any scenario's people, limits, kit, arrival and animals, and
   Rimfolk's own parts, for the new colony alone. Needs 1.

   **Built (release 3, 7 November 2026; `Source/ColonyCouncil/Colonies/ScenarioStartMath.cs`, `ScenarioStarts.cs`,
   `ScenarioStartWindow.cs`, `ScenarioStartPatches.cs`; DESIGN.md "A scenario's start").** The founding window's choice
   of start (a new game's group, or a scenario's start from the list of loaded scenarios with what each carries), the
   newcomers' window by the scenario's people part, its shaping parts through their pawn hooks, its kit, arrival and
   animals, and Rimfolk's own parts on that colony, with the decision 3A. What differs from section 1:
   - *The list* holds every scenario def loaded, this game's own when it is none of those, and the player's own and the
     workshop's scenarios only as the game already holds them (nothing is read from the disk mid-game). A scenario that
     isn't loaded any more by the time the newcomers are chosen gives a new game's group, said in the window.
   - *The people*: the four people parts the game has (the plain one, kinds, xenotypes and Anomaly's mutants) are read
     field by field; another mod's people part is listed as not carried and the newcomers come as a new game's group,
     its other parts still carried. The count is the scenario's own (1 to 10, chosen from its choice, never 1 to 6 with
     the plus); those left behind are let go, not kept as world pawns as a new game keeps them. The character card's
     child option still follows the game's own scenario, and with the game's own scenario giving no possessions the
     chosen scenario can't give them any.
   - *The shaping parts* need the deeper game hooks (the scenario's three pawn hooks are routed while the window is
     open); with them off they are listed as not carried. A part hidden until the map is made is applied as they are
     confirmed.
   - *The kit* is its `PlayerStartingThings` and also what the scenario lays about the start (its scatter parts), near
     where they arrive or anywhere on the land, made as the game's scatter makes them.
   - *The arrival*: standing is walking in from the edge of the land with the kit (a start spot mid-game would be people
     appearing out of nothing); drop pods come down near the middle of a new colony's land, or at the colony's drop spot
     when its party is there first; the gravship comes only with the old colony left behind and on a map made for the
     newcomers alone (nobody of the colony there before them), else they walk in.
   - *The price (4A)*: with the old colony kept the passage covers the scenario's kit too (its things, what it scatters,
     its animals and mechs by the game's market values, each child in its care the least passage), since the kit is
     made, not taken from the stores; free with the old colony left behind.
   - *Rimfolk's parts* are set up once the newcomers are on the map (out of their pods), or a day after they came, on that
     colony's council, calendar and people; a reputation moves the colony's one character (each colony's own is release
     4's decision 13); the Beholden's grant is that colony's fealty tie and the people still remember it, without the warm
     start's goodwill.
   - *Not carried* also: the scenario's own people when it is another kind (the newcomers join the colony's people),
     its own kind of map, and its opening words. No setting of its own: the choice is made in the founding window.
4. **Many colonies in play** (section 3's rest): merging councils, the pages following the colony in view, notables
   near every home, world events across homes, the character per colony (if chosen), the small ones. Needs 1.

   **Built (release 4, 7 November 2026; `Source/ColonyCouncil/Colonies/ManyColoniesMath.cs`, `Merges.cs`, `HomesMerge.cs`,
   `ManyColoniesDebug.cs`, `Character/ColonyCharacters.cs`, `Calendar/CalendarMerge.cs`, `UI/ColonyView.cs`; DESIGN.md
   "Many colonies in play").** 3b, 3c and 3d with the decisions 12A, 13A and 14A. With one colony nothing runs, no roll is
   added and the save is as it was. What differs from section 3:
   - *When two councils become one*: only when a colony's home is lost (given up or handed over) and its people come to
     live at a home with grown-ups of its own while the old council is still kept (thirty days). Release 1's founding
     always makes a new home, so a founding party joining an existing home never arises; a fresh start's party with the
     old colony handed over in the same act is their founding, not a merge (`IsMerge`, a colony founded from theirs within
     the season); a home of theirs alone is their colony moved, 7a's carried council. Its people are those whose last
     home it was and those who moved from it within those thirty days (`HomeTie.prev`, kept only with more than one home).
     It comes once the first of them has lived a day at the home most of them live at, waiting while others of theirs are
     still on the road or a fight is on the map, three days after the loss at most.
   - *Traditions* are the calendar's (the dilemmas' customs have been calendar days since 0.115.0), matched by the day's id;
     half its strength is half its years kept and half its weight, still a tradition; their founding day and anyone's own
     day are not traditions to merge. The calendar's own `TryAdd` keeps them inside its cap.
   - *The election* is the council's own (on the map, or the instant vote), called three days after the merge, unless
     one is under way or was held since.
   - *Names*: the better by the name's own count, the colony's holder on a tie, told in a line of news; whoever lost it
     writes it in their journal when it was the name they were known by. In the first season a name taken over in the
     colony makes no rival and no mood of a name lost.
   - *The welcome* is the feast engine's meal for everyone at home (17 to 23, so the first evening), its host the speaker,
     the greeting the host's cup; no mood of its own (the meal's and the cup's are the game's and the feast's). Without
     play-outs the greeting alone, in a bubble. One letter says what merged, the meal only when it was set going.
   - *Old hands and newcomers* keep apart only by the groups' own pace: once merged they are one colony's people, so the
     groups' rule that people of two colonies form no group no longer holds them apart.
   - *The character*: also a colony that moves (the old one abandoned once the new stands) takes all of its character with
     its council. Word among the peoples (the goodwill a generous or open colony earns) is the seat's ways' only, and
     renown's part of the colony's name is told by the seat's name only, so neither rises with more homes. "Walled" needs
     the calendar's walls-up line, 80 wall sections, at that home. A name's change in the chronicle names the colony.
   - *Notables*: further homes on the ground only (a home in orbit has no neighbours), their nearest two towns within twice
     the region's reach and not the seat's already.
   - *World events*: placed near a home on the ground picked by its people (`Homes.People`); the rolls and the rhythm are
     the seat's as before with one home on the ground.
   - *The pages*: a chip "This colony" beside the colony chip turns to "Every colony" for the session; the People view's
     five pages and the Chronicle follow, and on the region window the Faces (the people known by name, the colony in
     view's neighbours) and Here and away's who is here and who is away. The peoples' own cards stay every people's (a
     people is the colony's as a whole), and what is coming, the letters on the road, the missions and the marriages stay
     the colony's as a whole. Every staffed town has its card's Who, and a people's card lists their towns in the colony
     in view's reach. The council panel's bar names the colony, and its trust is its people's.
   - *This week* lists a founding and a hand-over under way where they stand, as their colony's card has it (release 1's
     and 2a's "left for release 4").
   - *Setting*: one, "Two councils become one" (Many colonies); the rest needs none.
   - *Tests*: `Tools/Tests/ManyColoniesTests.cs` ("many colonies: "), beside `ManyHomesTests`.
5. **The town's life**: the raid and the call for help on its map, hard times and the town asking to come back,
   turning against the colony, Faction Territories' pace for its new towns. Needs 2 and stage 1's rise and fall and
   raiders' camps.

   **Built (release 5, 7 November 2026; `Source/ColonyCouncil/Colonies/TownLifeMath.cs`, `TownLife.cs`, `TownRaid.cs`,
   `TownLifeLetters.cs`, `TownLifePatches.cs`, `TownLifeDebug.cs`; DESIGN.md "The town's life").** The raid and the fight
   on its map, hard times and its ask to come back, its people at the gate, turning against the colony, growth at
   Faction Territories' pace (15B) and 11B. What differs from section 2:
   - *The raid* is a world event of its own (`Rimfolk_WS_TownRaid`, weight 4, a fight of its own, so only with
     world-event battles on), drawn from the world events' usual rhythm: it never adds an event to the year. Its raiders
     are a people at war with both the town and the colony, so the colony's fighters can fight them. The town holds out
     five days for the colony's caravan (two grown colonists or more; pods and a shuttle land as a caravan, so they count),
     and on while it is on the road. Vehicles and a gravship are not offered by name.
   - *The fight* is on the town's own map (the kept layout, its notables and its defenders at the raid's own points), the
     raiders in at the far edge: the game's defeat check stands down for a town at peace with the colony there, since the
     game counts any town defeated when nobody of its people is a threat to the colony. Without the deeper hooks no map
     is made and the colony isn't asked to send anyone.
   - *Unanswered*, the game's measure of the two sides' points decides: held, hurt or sacked, with its fortune, a share of
     its trade stock and up to two of its notables who were never the colony's (its other people the colony knows by
     name; the rest are not counted); a former colonist may die (one at most, never its head) only in a raid it lost,
     where the design had "of old age by the notables' rule": the player's decision 11B ruled that out.
   - *Hard times*: "long struggling" is the region's own rule (`RiseFallMath.Struggling`); the town's ask is a letter by
     its means, its yes standing until the colony answers (ten days unanswered is a no); refused twice, the town is given
     up two to five days later as the region's own ruin, its former colonists and up to three others at the colony's gate
     (take them all in, only the colony's own, or send them on); with no other town, the people is made temporary and
     defeated, as when it is taken back. "Under a war it can't win" is not a reason of its own: a war's blows are among
     the region's.
   - *Turning against*: one goodwill situation of its own ("wronged by [colony]"), the colony's open wrongs in the faction
     memory, the ledger's live grudges and a head's low regard, beside the bond; the game's drift does the rest. A broken
     pact counts through the memory's own wrong for it. A new head who thinks little of the colony is told in the news.
   - *Hostile raids* are the game's own (its storyteller and the people's def): Rimfolk only names a former colonist the
     game redressed into them, in the raid's letter. The raiders' camps needed no change to pick the people.
   - *Growth (15B)*: only with Faction Territories, at its own pace through the existing hooks, told in the news and the
     history; Rimfolk's own new towns and ruins settled again are never these peoples' (without the mod the town stays one
     town).
   - *11B*: a death comes only in a fight on a map the player sees, a raid the town lost, or a plague or famine of its
     people or within 20 tiles (6% or 4% each when it ends, more if struggling or old, less where the colony helped; one at
     most), each told by letter with the name.
   - *Setting*: none new; "The town's life" covers it.

Beside them, the content pass (wave 16): letters, talk, news and journals for the towns the colony was, the founders
and the newcomers in every age's form; the Almanac pages "A new start", "A town of our own", "Many colonies".

Existing checks that must stay green: `ManyHomesTests`, `HomesTests`, `ColonyStateTests`, `LeanTests` (no new slot),
`TellTests`, `LedgerTests`, `HistoryTests`, `RenownTests`, `BanishTests`, `MovingOnTests`, `FactsTests` (1500 talks
unchanged with the new facts), `OutsiderTalkTests` (new tags and placeholders), `ImmersionTests` (one-sentence
bubbles), `RegionViewsTests` and `CardsTests` (the colonies on the Ours lens, the colony card's acts), `PagesTests` (no
new view), the save model (`--strict`), the def validator (the goodwill situation, the forms, the world event, the
letters), `labelcheck`, `almanaccheck`, `playout_audit` (the ceremony, the farewell, the welcome), `interaction_audit`,
and the 1.7 API diff for the new game names (`SettleUtility.AddNewHome`, `GetOrGenerateMapUtility.GetOrGenerateMap`,
`WorldObject.SetFaction`, `Settlement.ShouldRemoveMapNow`, `FactionGenerator.NewGeneratedFaction`,
`FactionManager.Add`, `FactionIdeosTracker.SetPrimary`, `Faction.SetRelation`, `Faction.TryGenerateNewLeader`,
`GoodwillSituationWorker`, `GravshipUtility.AbandonMap`, `SettleInExistingMapUtility`, `GenStep_Settlement`,
`Sketch.Spawn`, `StartingPawnUtility`, `CharacterCardUtility.DrawCharacterCard`, `ScenPart.PlayerStartingThings`).

In the game, with dev tools to force each part: a fresh start by each arrival, with the old colony kept (the seat
moved and an event that follows it) and abandoned; the newcomers' window with Biotech's options, closed with the X; the
game's limit at one colony; a hand-over with a caravan leaving, with everyone going to another home, and with a
gravship leaving without an anchor; the town on the world map (colours, notables, letters); its head's death and the
second leading (the game's letter); attacking it (the layout, the notables at home); taking it back both ways;
Faction Territories' territory around it; two colonies merged.

## Decisions for the player

In the order the releases need them.

**A fresh start (releases 1 and 3)**

1. **The seat.** Can the player move the seat, the place the colony as a whole is run from? *Yes:* "Make this the
   seat" on a colony's card, a home on the ground, at most once a season; the events and letters about no home of
   their own, the speaker who signs to the world, the seat's world days and the region's faces move with it, and the
   old seat's people trust the council a little less (no mood). *No:* the seat stays the first home founded (a lost
   seat goes to the next, as now), so with the old colony kept the world's attention stays there. Recommended: yes.
2. **Who goes and who stays: how much say do the colonists have?** *Advice with a cost:* each one's wish is shown;
   before a hand-over the colony's meeting votes, and going against its vote costs trust among those who stay; anyone
   sent or kept against their own wish is "uprooted" for a season (a mood with a hint) and may ask later to move to the
   other colony or the town; close bonds split are "parted" for a season; nobody refuses the player. *The player
   alone:* the wishes shown, no vote and no "uprooted"; only "parted". Recommended: advice with a cost.
3. **Newcomers and a scenario.** How are newcomers made, and can a scenario shape the new colony? *The game's own
   mid-game window* (the one after a game over: one to six people, randomize, Biotech's options, the skills summary),
   with any scenario's start parts read for the new colony alone (its people and their limits, kit, arrival, animals,
   Rimfolk's own parts but Bad Blood's two quarrelling families, a feud along family lines); what changes the whole game
   (the player's people, research, rules for every map, world conditions, incidents off) not carried and listed; the
   gravship start only when the old colony is left behind. *The new game's page itself, mid-game:* closer to a new game,
   but its scenario setup clears the game's custom xenotypes and resets the default drug policy, and character-editor
   mods expect a new game. *No scenarios:* newcomers as a new game's group only. Recommended: the mid-game window with
   scenario parts (the window in release 1, the scenarios in release 3); a whole new scenario on its own page is what a
   new game is for.
4. **The newcomers' price.** Do newcomers cost the colony anything? *A price when the old colony is kept:* free when
   the old colony is handed over or abandoned in the same act (a true fresh start, as the game's new wanderers are
   free); with it kept, once a year at most and their passage paid from its stores (silver and food worth about their
   kit). *Free always.* Recommended: the price.
5. **The game's colony limit.** Who decides how many colonies the game allows? *The player:* the game's own option
   (Options, Gameplay: one by default, up to five) is respected and named in the windows, never changed by Rimfolk, so
   keeping the old colony and founding a new one means raising it once. *Rimfolk:* it raises the option when the player
   founds a colony. Recommended: the player.

**Handing a colony over (release 2)**

6. **The new people's kind.** What kind of people does the town become? *One the game already knows:* a peaceful
   people of the colony's age (vanilla first, else a loaded mod's, else the nearest age), so the town outlives
   Rimfolk's removal as an ordinary town. *Rimfolk's own four people defs:* exact by age, but a save with such a town
   then needs Rimfolk, and the town is dropped with an error without it. Recommended: one the game knows.
7. **The town's map.** When the town's map is made (an attack, taking it back), what does it look like? *The colony as
   it was left:* its layout kept (10 to 40 KB a town, 64 KB at most) and placed. *The game's ordinary base* for that
   people. Recommended: the colony as it was left.
8. **The bond in goodwill.** Does the town stay warm to the colony on its own? *Yes:* a goodwill situation "once of
   [colony]" (+30 while a former colonist heads the town, +15 after, gone with a war or a taking by force), the game's
   drift and same faith doing the rest. *No:* the game's own drift alone, so the town cools toward the colony like any
   people. Recommended: yes.
9. **What the town owes the colony.** Is the town bound to the colony by more than goodwill? *Free or a pact:* a free
   town owing nothing, or a defence or trade pact if the player picks one in the window (a year, renewed by letter);
   fealty only later, by stage 2's own rule (a struggling town may ask the colony's protection), and Faction
   Territories' vassal outpost not used. *The colony's vassal from the first day:* its harvest at the gate and its
   levies, a town that never chose it. Recommended: free or a pact.
10. **Taking back by force.** Can the colony take its old town back by force? *As the game allows:* attack, beat,
    settle; the bond ends, and the town's people and every people of their faith remember a heavy wrong. *Never:*
    only peacefully. Recommended: as the game allows.
11. **Former colonists off the map.** How can the people who stayed die? *Only where the player sees it:* in a fight
    on a map, or of old age by the notables' rule (widened to them and the head); a raid the colony doesn't answer
    costs the town goods, prosperity and its other people. *Like any town's people:* rolled with the town's fortune,
    former colonists too. Recommended: only where the player sees it.

**Many colonies in play (release 4)**

12. **Merging councils.** When two colonies' people come to live together, what merges? *As designed:* the newcomers'
    traditions join at half strength, an election within the season when they are a third of the grown-ups, names
    looked at again with no rival from a merge, the old founding day a remembrance day, a welcome meal. *7a's rules
    alone:* a moving officer steps down and they join the council, nothing else. Recommended: as designed.
13. **Each colony its own character.** Does each colony earn a character of its own? *Per colony:* its own place and
    work names and its own five ways (a colony founded from another starts with half of the other's, newcomers fresh).
    *One for the game, as now.* Recommended: per colony.
14. **The region near every home.** Do the region's faces and events come to every home? *Every home:* the nearest two
    towns of each further home get notables (eight more people a home) and world events are placed near any home by
    its people. *The seat only, as now:* a far colony's neighbours have no faces until it is the seat. Recommended:
    every home.

**The town's life (release 5)**

15. **The town's growth with Faction Territories.** How fast does the town found towns of its own? *Rimfolk's pace:*
    only while it flourishes, one at most each 90 days. *The mod's pace:* about one each 30 days, as for every people.
    Recommended: Rimfolk's pace.

Beyond the plan, proposed for the content pass and built only if picked: a wedding in a town the colony was between
two former colonists, with the colony invited; old hands and newcomers as a question for the council in a merged
colony's first season; a founders' monument on the new colony's first anniversary; a colonist the council would
banish sent to live at another home instead, where there are two on the ground (their trouble left behind).
