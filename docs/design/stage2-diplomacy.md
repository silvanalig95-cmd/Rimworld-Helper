# Stage 2: diplomacy (design, 4 November 2026)

The plan is ROADMAP.md, "The world and its people": stage 2, every one of the brainstorm's fourteen items (the first
two, notables and letters, are built) and the envoy who lives at the colony for a season, in its four rounds, each item
with its bad side under the harm rule and its form in every age. It builds on the foundation
(`docs/design/foundation.md`; DESIGN.md, "Homes" to "Rimfolk's windows") and on stage 1
(`docs/design/stage1-region.md`). The game's side was read in the decompiled 1.6 code (32bitx64bit/RW-Decompiled-1.6),
Faction Territories' side in its decompiled 1.6 assembly, Diplomatic Marriage+ through the adapter that already reads
it; every game name used below is also in the 1.7 reference. Times are the game's: a season is 15 days, a year 60.

## Summary

- Diplomacy is rules and text on records that already exist: the world ledger's ties (two new kinds: a secret known
  and a scheme), its persons and houses, the history (a new `dip:` family), the facts, `Ages.Form` and `Tell`. Nothing
  runs per tick at world level; two passes; every saved list has a bound and a model.
- The player acts where they already do: two new pages in the region window (Diplomacy: the ties with each people and
  what can be done; Household: who is here from outside and who of ours is away, the council's tasks, schemes and the
  colony's houses), the region map's town panel, the right-click "Talk with..." menu, letters and comms calls with new
  purposes, and council questions in which colonists object in their own words. Every answer plays out on the map:
  messengers, retinues, war bands, caravans setting out, oaths sworn at the gate.
- Round a: favours owed both ways and called in for real things; truces that keep a people's raids away and pacts
  that bring their fighters and call on the colony's; ransom both ways in every age; an envoy who lives at the colony
  for a season, and the colony's own envoy abroad.
- Round b: feasts with guests and contests on the map (wrestling, races, hunts, games at the colony's tables);
  marriage alliances from courtship, a match the council seeks or one they offer; wards fostered as the game's lodgers
  and hostages given for pacts.
- Round c: the keeper of secrets, a new office; council tasks as letters, calls, visits by caravan and guests received,
  with results the player sees on the panels (both dropped, 5 November 2026); fealty to a stronger neighbour (tribute, levies, protection) and vassals
  of the colony's own (the hamlet, Faction Territories' vassals, towns taken under protection); schemes gentle and dark,
  ours and theirs, always on the map.
- Round d: the renown of the colony's houses, known for what their members did; stress against one's nature through
  the heart's own strain, episodes and marks.
- Harm comes at the game's rate and from the map: a truce broken by their firebrands (one in ten, the peace talks'
  backfire), a trap at a ransom exchange (the peace talks' disaster, 5%), a bout's real blow (one in 25), pact
  fighters and levies in real fights, a forfeit hostage held in the game's own captivity, a scheme found out, a feud
  carried down to an heir. Eleven releases; nineteen decisions at the end.

## What the game gives (verified in the 1.6 code)

- **Goodwill and standing.** `Faction.TryAffectGoodwillWith` moves goodwill (from -100 to 100); a people turns hostile
  at -75 and below, becomes neutral again at 0, an ally at 75, and stops being one at 0
  (`FactionRelation.CheckKindThresholds`). Goodwill drifts 0.2 a day toward a people's natural goodwill, is capped by
  the goodwill situations (Ideology's memes, natural enemies) and can be locked by a quest
  (`QuestUtility.IsGoodwillLockedByQuest`). Rimfolk moves goodwill only through that method, as it does today, and never
  sets a relation directly.
- **Who raids.** The storyteller's raid chooses its people through `IncidentWorker_RaidEnemy.FactionCanBeGroupSource`
  (hostile, active, past its earliest raid day, a strategy that fits); a raid whose people is set beforehand (a
  quest's, another mod's, Rimfolk's own `HappeningTracker.AddRaid`) skips the choice. A postfix there keeps one people
  from being chosen for one home and changes nothing else. Caravan ambushes choose their people elsewhere
  (`PawnGroupMakerUtility.TryGetRandomFactionForCombatPawnGroup`, which Faction Territories also patches) and are left
  alone.
- **Friendly fighters.** The game's call for military aid (`FactionDialogMaker.CallForAid`) needs an ally, a comms
  console, a day's wait and 25 goodwill, and sends 800 to 1,000 points through `IncidentDefOf.RaidFriendly`. Rimfolk's
  war bands (`WarCamps.MakeFighters` and `Start`: about half the threat, walking in from an edge, a camp after the
  fight) already bring a people's fighters in every age; the diplomacy uses them.
- **Peace talks.** The game's peace-talks site ends in disaster (an ambush on the caravan), backfire, floundering,
  success or triumph by weights 5, 10, 20, 55 and 10, the two bad ones four times likelier with no negotiation ability
  and 0.4 times at its best (`PeaceTalks.Notify_CaravanArrived`); success gives 60 to 70 goodwill and, with the Empire,
  1 to 4 honor. It is the game's own measure of how often a parley goes wrong.
- **Captives.** A kidnapped colonist is kept on the kidnappers' `KidnappedPawnsTracker` and joins them at a mean of 30
  days. The game's ransom demand (`IncidentWorker_RansomDemand`, `ChoiceLetter_RansomDemand`) needs a powered comms
  console, asks 1.2 to 2.2 times the colonist's market value (`DiplomacyTuning.RansomFeeMarketValueFactorRange`) with a
  day to answer, launches the silver, and returns the colonist at the map's edge (a people below industrial) or by drop
  pod. A prisoner released is walked out by the warden (`PrisonerInteractionModeDefOf.Release`,
  `Pawn_GuestTracker.SetExclusiveInteraction`, `JobDefOf.ReleasePrisoner`); leaving healthy gives their people 12
  goodwill, 40 more for their leader, and 1 for each time they were tended, up to 10 (`Faction.GetGoodwillGainForExit`).
- **Lodgers and lent colonists.** Someone joins the colony as another people's lodger through a
  `QuestPart_ExtraFaction` in a live quest, as the game's refugee hosting does (`IsQuestLodger`); Rimfolk already makes
  quests of its own (`Council_MissionQuest`, with its own part). Colonists lent away stay the colony's in the world's
  pawns until they come back (`QuestPart_LendColonistsToFaction`).
- **Caravans.** `CaravanExitMapUtility.ExitMapAndCreateCaravan` and `Caravan_PathFollower.StartPath` send a party to a
  tile; Rimfolk's road missions already see a caravan arrive by its tile. On the road the game's caravan incidents
  apply: ambushes, and diseases that take caravan colonists (`IncidentWorker_DiseaseHuman`). A fight away from home is
  `CaravanIncidentUtility.SetupCaravanAttackMap`, as the peace talks' disaster does.
- **Trade.** `IncidentDefOf.TraderCaravanArrival` with a people set brings that people's trader caravan.
- **Marriage.** `RelationsUtility.RomanceEligiblePair` (age, kin, orientation, prisoners), the faith's rule on how
  many spouses (`SpouseRelationUtility.GetHistoryEventForSpouseCount` with `IdeoUtility.DoerWillingToDo`), the fiancé
  relation (`PawnRelationDefOf.Fiance`), the game's ceremony (`GatheringWorker_MarriageCeremony`, both fiancés on the
  organiser's map), `SpouseRelationUtility.DoDivorce`.
- **Children's lessons.** With Biotech, children learn at the game's lessons (`JobDriver_Lessongiving`,
  `JobDriver_Lessontaking`).
- **Royal titles.** `Pawn_RoyaltyTracker.GetCurrentTitle`, `GainFavor`, `TryRemoveFavor`; the permits are the
  Empire's own aid.
- **Breakdowns.** `CompBreakdownable.DoBreakdown`. A machine breaks down by itself at a mean of 228 days, so a colony
  with ten such machines sees two or three breakdowns a year.

## What Rimfolk already has to build on

- Notables of the nearest towns (head, heir, envoy, trader, acquaintances) with their regard of the colony and of
  single colonists, memory lines, age and succession (`World/Notables.cs`); the towns' founding families as
  `LedgerHouse`.
- Letters and calls (`World/NeighbourLetters.cs`): purposes, a writer at a table, their messenger at the gate, comms
  calls with aides and callbacks, answers by tone, a waylaid messenger.
- The right-click acts with outsiders (`Outsiders/`): chat, gifts, good words, things to bring next time (a Favour
  tie), invitations, befriending, courting to lovers, teaching, faith, sparring, hard words, duels of honour.
- What each people remembers (`FactionMemory`: some seventy kinds of deed), grudges, vendettas, `KinMemory` for single
  colonists.
- People on the map: visitors (`VisitorTrip`, `LordJob_RimfolkVisit`, `VisitEffect_Come`'s lord's retinue by the
  gate), couriers (`Couriers.Deliver`, `Collect`, `Exchange`), guests who eat and sleep at the colony
  (`Missions/Hosting.cs`), war bands (`Camps/WarCamp.cs`), the missions (a neighbour's great day, the invitation, the
  dignitary, refugees, harvest hands, the escort), the play-outs (`DilemmaEffect_CarryOut`, gatherings with guests,
  departures), the journeys' packing and caravans.
- The council's twelve offices in each colony, elections, dilemmas in which a colonist objects before the council
  (as `Council_DlmGuest_Invite` does), trust.
- Secrets, family names (`Social/FamilyNames.cs`), reputation and deeds, nature (eight sides) and marks (`Persona/`),
  the heart (strain, bands, episodes, feelings, needs), the pressures, `WeddingPlans`.

## Shared rules

- **The ledger owns it.** Every bond here is a `Tie`: Favour, Pact, Truce, Marriage, Ward, Hostage, Fealty and Grudge
  exist, and two kinds are new, Secret (from whoever knows it to the one it is about: leverage, which can be called in)
  and Scheme (from the schemer to the target, its progress in its terms). `TieRules.Kinds` becomes 14 and the bound of
  all ties 1,080; the save model gets both before they ship. People are named with refs (`WorldLedger.PersonOf`) and
  kept with `WorldLedger.Keep` and reasons of their own (new: `Envoy`, `Ward`, `Hostage`, `Spouse`, `Lent`). The
  colony's side of a tie is the colony that made it (its home key), so a second colony has its own; goodwill, which the
  game keeps once for the player, stays one. What isn't a tie (offers waiting, an envoy's stay, a feast's plan) is each
  home's part, `DiplomacyHome`, kept by the `ColonyBook` rules, so one colony plays as before; a council's tasks are
  its colony's (`ColonyState`).
- **The history and the memory.** Ties write `tie:KIND:made/kept/calledIn/broken/ended/putOff` already. What isn't a
  tie goes into a new family `dip:` (`dip:ransomed`, `dip:exchanged`, `dip:envoy`, `dip:feast`, `dip:contest`,
  `dip:wedding`, `dip:task`, `dip:tribute`, `dip:levy`, `dip:foundout`, `dip:renown`), milestones when a notable or a
  house is concerned. New deeds a people remembers (`FactionMemory.Kinds`, size in brackets): of the colony, `swore`
  (8), `keptWord` (10), `ransomed` (4), `envoyHonored` (8), `fostered` (12), `married` (15), `hostageReturned` (6),
  `levies` (8), `tribute` (1, merged by season); wrongs, `brokeTruce` (25), `brokePact` (20), `wordBroken` (4, heard by
  the other peoples in reach), `spouseWronged` (30), `schemed` (15), `renounced` (20), `tributeShort` (6); of theirs,
  `brokeWord` (12), `truceBroken` (20), `schemedUs` (12), `protected` (10).
- **Where the player acts.**
  - *Region → Diplomacy* (new page, `MainTabWindow_World.Register`, order 35): a strip of what is owed each way, then
    one card per people within reach (and any people the colony has a tie with): their stance in words, their head, each
    tie in its age's words with since when and on what terms, what is under way (letters, an envoy, a task, a scheme of
    ours), and the acts that fit, each greyed with why not, as the outsiders' menu does.
  - *Region → Household* (new page, order 36): who is here from outside (an envoy, wards, hostages, spouses and their
    kin) and who of ours is away; the council's tasks with each one's last step and what comes next, never as a
    countdown; schemes (ours, and those found out against us); the colony's houses.
  - *The region map's town panel* (`NeighbourPanel.Sections`): the town's ties and the acts that fit it.
  - *Right-click* "Talk with X...": new acts after Invite, for someone of theirs at the colony: ask a favour of their
    people, speak of a pact, speak for a match, offer an exchange, and the keeper's sounding out. Their answer is given
    face to face, at once, with the talk's own tone and patience.
  - *Letters and calls*: new purposes (Favour, Pact, Truce, Match, Ward, Ransom, Envoy, Fealty) in the existing
    machinery: the writer at a table or the caller at the console, their messenger at the gate, the answer by tone.
  - *Council questions* (`Tell.Ask`, after `Tell.CanAsk` when it can wait): the weighty ones (fealty sworn or renounced,
    a hostage given, a match pressed on a colonist, a truce with a people one of ours swore vengeance on, a dark scheme,
    a call to arms refused) come to the council with the colonist who objects and why, as the guest invitation does;
    the impact sheet names the risk before the choice.
  - *On the map*: messengers and porters (`Couriers`), retinues (`VisitEffect_Come`), guests (`Hosting`), war bands
    (`WarCamps`), errands carried out (`DilemmaEffect_CarryOut`), gatherings with guests, the colony's own caravans.
- **One voice.** `Tell.Minor` for news, `Tell.Letter` for an opening or an end, `Tell.Ask` for a question; This week
  lists what waits on the colony (an envoy staying, a favour asked, an offer for a captive, a feast day, tribute
  expected, a call to arms) without a timer. Spoken lines through `Speech.Say`, one sentence each.
- **Words by age.** Each item asks `Ages.Form` for its word (the colony's means) and rite (the other people's ways);
  four new features join the ten (truce, hostage, envoy, levy: the table below). A people with Ideology names a rite
  its faith really has, as invitations already do. The Empire keeps its own titles and honor (decision 9).
- **Harm.** Only from what happens on the map and shown there, at the game's rate, named before any choice that
  brings it, under the harm review's shared rules (one Rimfolk-made injury per colonist in three days at most, never
  babies, children only the mildest) and its "Hurts from everyday life" setting once those are built. Nothing happens
  to anyone off the map but what the game itself does there (its caravan incidents, its captivity).
- **Settings.** A section "Diplomacy": favours and pacts; truces keep raids away (with the deeper hooks); ransom;
  envoys; feasts with guests; marriage alliances; wards and hostages; council tasks; fealty and vassals; schemes, with
  "Schemes against the colony" from 0 to 200% (100%); house renown; against their nature. Off, a part makes nothing new,
  keeps its records and lets its live ties run out by their terms. Links into other systems also sit behind "Systems
  talk to each other".

## Round a

### 1. Favours owed and called in

**What the player sees and does.** When the colony does a people a good turn of size 10 or more in their memory (their
child found, refugees sheltered, a dignitary well hosted, their leader freed, a town rebuilt, a pilgrim escorted), the
letter that tells it adds what follows: "The Lenlia owe the colony a debt of honor for it." A notable helped in person
owes the colonist who did it ("Reeve Hallam owes Ada a favor"). The Diplomacy page lists what is owed each way, what
for and since when.

Calling one in, from the page, the town panel, a letter or call, or face to face with one of theirs, opens what they
can do now, each greyed with why not:
- fighters, only while a threat is on a home: their war band comes (below, pacts);
- a load of goods (food, medicine, wood, steel or silver, worth about 400 silver by their town's fortune), brought in
  by their porters;
- a word for the colony with a third people they are on good terms with (10 goodwill, said in that people's letter);
- a colonist of ours they hold, walked to the gate;
- their help toward peace with a people at war with the colony (that people's next truce answer as if it thought 20
  better of the colony);
- in later rounds, a match considered, and their silence about a scheme of ours found out.

They call in what the colony owes them (their war band's help, the medicine they sent, relief, teaching, goods
brought) for something their trouble really needs, named in the letter or by their envoy: medicine while a sickness
runs at their town, food in a famine, hands at harvest (the existing mission), refugees taken in (the existing
mission), the colony's side in a dispute (the feud and claimant world events), fighters while their town is attacked.
The answer plays out: goods carried out to their messenger, a caravan setting off.

**How it works.**
- A Favour tie from the one who owes (their people, or a notable as a person) to the one owed (the colony, or the
  colonist), origin `debts` (the bring-next-time favours keep theirs), its terms the deed it came from, how big, and
  who did it; until two years after (a debt never asked for is let go). One open each way per people and colony; a new
  good turn while one is open renews it. A notable's personal favours: twelve open at most.
- Called in, it is kept when it is done on the map (the band walks in, the goods are set down) or broken when refused
  or not done within its days. They honour it while their head thinks well of the colony (regard 20 or more), seven
  times in ten at a lower regard, four in ten below 0; a refusal is their letter, the deed `brokeWord`.
- Their calls: a daily look at each favour owed by the colony, called in at a mean of 90 days but only with a need the
  world shows; at most one ask a season per people; unanswered in three days it is refused (broken, the existing deed
  `refused` and 5 goodwill).
- A favour of theirs asked for fighters far from home: two colonists or more set out within five days for their town
  (a caravan); there, the fight is on the game's caravan battle map against their attackers' band at half the home's
  threat points, or, with Faction Territories, its own invasion fight (decision 3). Arriving and fighting keeps it.
- Facts: `tie:favour`, its sides and `:due` exist; new `{FAVOURWHY}` (what it was for, in words).

**Saved and bounds.** Ties only (Favour is bounded at 60 live; here about two per people and twelve personal). No
other record.

**Ages.** A debt of the hearth, of honour, an IOU, a ledger entry (ours); a gift owed back, a pledge given by hand, a
favour on the books, credit owed (theirs). The call goes by the colony's means: their messenger fetches the letter, or
a radio or comms call.

**Bad side.** Fighters called in bring a real fight on the colony's map, and those of theirs who fall leave the colony
owing them in turn. Answering their call sends colonists into the game's own fight away from home, or onto its roads.
Refusing breaks the tie: the deed, goodwill, and the house's renown in round d.

**With and without other mods.** Faction Territories: an invasion of their town within reach is a call to arms that
names it; the colony answers by joining it through the mod's own arrival, and the adapter's hook on `ResolveWithWinner`
says how it ended. Factional War: fighting beside them in a battle on the colony's land is a favour done (the existing
deed `sidedWith`). The Empire: as any people, and a favour called in from them can be a word at court (decision 9).

### 2. Pacts and truces

**What the player sees and does.**
- *A truce* with a people hostile to the colony. It can be offered when the colony has beaten a raid of theirs in the
  last 15 days, holds one of their notables or kin, or a friend of both has called in a favour for it; or they offer
  it themselves after such a defeat. The offer goes by a letter carried by a messenger of a people at peace with both,
  or by radio or comms; their envoy comes to the gate under their sign of parley, the colony's speaker walks out to
  them, and they swear it there. Terms: a season or a year, and a price either way (silver or goods carried out, their
  captives returned, a hostage from round b). While it holds their raids don't come to that home and their raiders'
  camp breaks up (stage 1, "Peace"). The Diplomacy page says how far their goodwill is from peace. At its end it is a
  peace if their goodwill has come to 0 (kept), else it runs out and their raids may come again.
- *A pact* with a people at peace (goodwill 20 or more), proposed by letter, call or envoy, or offered by them (once a
  year at most, when their head thinks 50 or more of the colony and both have an enemy in common). Two kinds (decision
  2): a defence pact, each coming when the other is attacked; a trade pact, their trader caravan each season and a
  cart of the colony's surplus going to them (the good and count chosen in the terms, carried out to their porters).
  A year, renewable.
- *Calling a defence pact in*: while a threat is on a home, "Call the Lenlia" on the page or the alert. The call goes by
  the colony's means: the caller at the comms console, or a signal fire laid and lit by the gate (the bonfire play-out,
  the game's own campfire) that their watchers see. Their band walks in some hours later, at most once a season per
  pact; nine times in ten they come (once in ten their riders are elsewhere: put off, not broken); if the attacker is
  their friend they decline and the pact ends with nobody at fault.
- *Their call*: when their town within reach is attacked (a war world event at it, a Faction Territories invasion),
  their letter asks for fighters, answered as a favour's fighters are (item 1). Refusing breaks the pact.
- *Breaking.* The colony breaks a truce or a pact by attacking that people (their town, caravans, visitors: the deeds
  the memory already notes as `attacked`, `caravanRaided`, `townAttacked`) or by refusing a call; it may end a pact by
  letter without blame. They break a truce when their raid or ambush falls on the colony while it holds.
- A truce with a people one of ours swore vengeance on comes to the council with the avenger's objection.

**How it works.**
- Truce: a tie between the colony and the people (from whoever offered), until its term. Pact: a tie with key
  `defence` or `trade`, until a year, its calls counted in its terms; called in while their band is on its way.
- `Patch_TruceRaids`, a postfix on `IncidentWorker_RaidEnemy.FactionCanBeGroupSource`: false for a people with a live
  truce toward the colony of the map being raided (a set built once a 250-tick stretch). Rimfolk's own raids ask the
  same through `Raids.CanRaid`. Raids whose people is set beforehand (quests, other mods) are not touched; if one of
  theirs comes all the same, it breaks the truce like any raid (decision 1).
- Their firebrands: once per truce, a roll of 10% (the peace talks' backfire), 15% for a people whose natural goodwill
  with the colony is below -50, that a band of theirs raids anyway: `AddRaid` at the game's 0.6 of the threat points,
  its letter naming the broken truce.
- Breaches are read from the memory's deeds as they are noted (`FactionMemory.Note`): a deed of ours against them is
  our breach, `raided` or `killedOurs` of theirs is theirs. Ours: `brokeTruce` or `brokePact`, and `wordBroken` with
  every people at peace with a town in reach.
- Facts: `tie:pact`, `tie:truce` (with `:ours`, `:theirs`, `:due`), `{PACTWITH}`, `{TRUCEWITH}`.

**Saved and bounds.** Ties only: one truce per people and colony, one pact of each kind; the per-kind bound of 60.

**Ages.** A blood oath, a treaty under seal, a signed agreement, an accord by comms (ours); gifts exchanged and a peace
at the river, oaths sworn before witnesses, a trade contract, a registered treaty (theirs). A truce has forms of its
own (below). The call: a fire on the hill or a horn, a rider, a radio call, comms.

**Bad side.** A truce broken by their firebrands (one in ten) or their ambush on the road (the game's ambush rate); a
pact that draws colonists into fights at home and away; a pact or truce broken by the colony costs its memory with
every people in reach. A truce made over an avenger's objection may see the avenger pick a fight with their trader
(the harm review's 3.8.1, if picked).

**With and without other mods.** Faction Territories: invasions are calls (item 1); an ambush in their territory that
the mod forces on the colony's caravan breaks a truce as any ambush of theirs would. Factional War (decision 18): when
a battle begins on a home, a message names a pact or truce partner among the two sides; colonists who fight beside a
pact partner keep the pact (`sidedWith`), harm done to a truce partner's fighters breaks the truce. The Empire makes
truces but no pacts (decision 9). Diplomatic Marriage+'s alliance is not a pact.

### 3. Ransom

**What the player sees and does.**
- *Their kin in the colony's cells.* Two to six days after one of a people is taken prisoner (a people the colony can
  deal with, not one that is everyone's enemy for good), if their people want them back (a notable, their leader or a
  notable's kin always; anyone else six times in ten from a people at peace, three in ten from a hostile one), their
  messenger comes to the gate, or their call comes in at the console, with an offer: silver or goods by their ways,
  worth half to the whole of the captive's market value, half again for a notable, twice for their leader. The answers:
  accept (the warden walks the captive out by the game's own release, and their porters set the price down at the gate);
  ask more (the warden's or speaker's Social decides between a quarter more and their leaving); free them for nothing
  (the game's release, the deed `forgave`, and a favour owed, item 1); keep them. Refused, the offer may come once more
  in a season; refused three times, a grudge. Right-click on a prisoner, "Offer for ransom", writes to their people and
  brings the offer sooner.
- *Ours in their hands.* With a powered comms console, the game's own ransom demand comes as it always has. Without one,
  the same demand comes by their messenger at the gate (decision 4), at the game's rate and fee, with the game's day to
  answer: paid, the silver is carried out, and the colonist walks in from the edge with their escort a few hours later,
  as the game itself returns a colonist from a people of the old ways. The colony may also offer one of theirs in
  exchange (the page, or a letter): both walk at once at the gate.

**How it works.**
- An offer record per captive (six per home at most) on the home's part (`DiplomacyHome`, kept by `ColonyBook`). The
  price goes through the couriers (`Couriers.Deliver` when they pay, `Couriers.Collect` when the colony does), their
  captive out by the game's release, ours in by `KidnappedPawnsTracker.RemoveKidnappedPawn` and an edge cell, as
  `ChoiceLetter_RansomDemand` does; an exchange holds each side until both are at the gate. A grudge from refusals is
  the existing Grudge tie (key `ransomRefused`).
- History `dip:ransomed`, `dip:exchanged`; the game's own goodwill for a captive leaving healthy stands; the deed
  `ransomed`. `KinMemory` remembers the warden who walked them out. A notable who was once the colony's captive keeps it
  in their talk (`them:wascaptive`, `{CAPTOR}`).

**Saved and bounds.** Up to six offers per home, dropped when answered or the captive is gone.

**Ages.** Captives traded back, ransom by messenger, by radio, an exchange by comms (ours); captives for goods, a price
in silver, an exchange at a crossing, an exchange on agreed terms (theirs). A tribe asks and pays in goods.

**Bad side.** With a hostile people the exchange can be a trap, at the peace talks' disaster rate (5%, times the game's
own factor for the negotiator's ability, from 0.4 to 4): a band of theirs falls on the gate as the captive walks out
(a raid of theirs at 0.35 of the threat points, from the gate's side; decision 5). A colonist of ours held long may
join them at the game's own rate. A captive who dies in the cells while an offer stands is the heavier
`prisonerDied` grudge that exists.

**With and without other mods.** Captives from Factional War's battles count as any. The Empire pays and asks in
silver. Nothing else touches it.

### 4. The envoy

**What the player sees and does.** A people at peace (goodwill 30 or more) whose envoy notable the colony knows may
offer to send them to live at the colony for a season, at most once a year per people, one envoy at a time per home; or
the colony asks by letter. Accepted, the envoy walks in with an attendant (a guard or a servant by their ways) and stays
the season as the colony's guest, who does no work (decision 6): eating at its table, sleeping in a bed set aside (a
guest bed with Hospitality, else one the player assigns on the Household page), walking the colony by day, talking with
colonists (the outsider talks start on their own), sitting in at a council meeting that falls during the stay, and
writing home every five days (a courier comes to the gate for the letter). How the stay goes shows in words on the
Household page ("content", "cool", "offended"). With the envoy here, pacts, favours, matches and wards are spoken of
face to face from the right-click menu, with no letter. When they leave, their report moves their people: a stay kept
well (fed, a bed of their own, talks gone well, a gift, a feast) raises their head's regard and may leave a favour owed;
a poor one is the existing `slighted`.

The colony may send its own envoy, a colonist good with people, to live at their town for a season (a council question:
their work is lost for the season). They leave by caravan and are away; every five days their letter brings real news
of the town (its fortune, its notables, the world events about it, a warning of trouble), and their stay raises the
regard of the town's notables by their Social.

**How it works.**
- `EnvoyStay` on the home's part: the envoy, their people, since, the end of the season, the bed, a score for the
  stay, the letters sent; `EnvoyAbroad`: our colonist, the town, since, the end. Kept with `KeepFor.Envoy` (ours away
  with `KeepFor.Lent`, decision 10).
- Their stay is `LordJob_RimfolkVisit` with the hosting rules of the missions' guests (meals, sleep, getting up), at
  the common rooms by day; in danger they keep indoors (`Rimfolk_VisitIndoors`).
- History `dip:envoy`; deeds `envoyHonored` or `slighted`. Facts: `envoy:here`, `{ENVOY}`, `them:envoy`.

**Saved and bounds.** One stay each way per home.

**Ages.** A new feature (below): a guest-friend by the fire, an envoy at court, a consul, a liaison (ours); a guest-gift
and a seat by the fire, letters of credence, credentials presented, credentials sent ahead by comms (theirs). The
Empire sends one of its titled kinds, who expects a room of their own and fine meals, said in their letters home.

**Bad side.** The envoy is on the map through raids, illness and hot tempers: hurt or killed while the colony
sheltered them costs a little regard, left in the open a lot; a hothead's hard words bring the outsiders' fistfight or a
challenge; in round c an envoy may spy (item 11). Our envoy meets the game's roads; nothing happens to them at the town.

**With and without other mods.** Hospitality: its guest beds when it is there. The Empire: as above. Nothing else.

## Round b

### 5. Feasts, hunts, games and contests with guests

**What the player sees and does.** The colony holds a feast with guests: from the Diplomacy page, or at the steward's
suggestion. The player picks whom to invite (up to three notables within three days' travel, at peace, regard 0 or
more: the existing invitation letters and calls), which contests (by what the colony has), a prize if any (a fine thing
from the stores), and the feast (the feast's serving rules, a meal for each guest). On the day, the first fine evening
after the guests can arrive, each comes with a small retinue and waits by the gate; the feast is a gathering with
guests, then the contests, on the map:
- *Wrestling*: bouts between a colonist and a guest by the sparring rules (eight swings, or until one yields to the
  pain).
- *A race*: runners from the gate to a marker and back, at a run, the fastest by speed and luck.
- *A hunt*: colonists and guests hunt the colony's game together (the calendar hunt's rules), counted by kills.
- *A game at the tables*: hoopstone, horseshoes, chess, billiards or poker, played as the game's own recreation at the
  colony's tables, won by skill.
- *A shooting match*: only where a mod gives targets (decision 7).

The winners are said aloud, the prize carried to them; guests go home at first light. Their notables' regard rises
with the welcome, the colony's champion's house gains renown (round d), and talk at their town remembers it.

Invited to theirs (the existing missions, a neighbour's great day and the invitation), the colony's guests go by
caravan, and who of ours won what is told when they come home; nothing happens to them there.

**How it works.** `FeastPlan` on the home's part (the day, the guests, the contests, the prize), one at a time; the
guests are `VisitEffect_Come` retinues fed by the hosting rules, joined to the gathering as guests; the bouts are the
outsiders' spar (`OutsiderMath.SparSwings`, `SparHardBlowChance`), the race a play-out route, the hunt the calendar's,
the tables the game's own joy jobs. `FeastMath` scores each contest. History `dip:feast` and `dip:contest`; the
existing deed `feast`. Facts: `town:lately:dip:feast`, `them:feastguest`, `{FEAST}`.

**Saved and bounds.** One plan per home; at most one feast with guests a season per home; four contests a feast.

**Ages.** A gift feast, a feast in the hall, a banquet, a gala (ours); a great hunt, wrestling and races; a hunt and a
tournament; a fair, a shooting match and races; an exhibition match (theirs). Guests hope for their own: a tribe for a
hunt, an industrial people for a race or a match, the Empire for a fine meal and music (the game's concert gathering
where the colony has an instrument). With Ideology their faith's feast ritual is named.

**Bad side.** A wrestling bout's real blow, one in 25 (the sparring rate); a beast turning on the hunters at the game's
own chance; a guest's night gone sour after drink (the outsiders' fistfight; the harm review's 2.5.1 rate, one guest
night in 15); food poisoning from the cook at the game's rate; a guest left without a chair or a meal (`slighted`); a
sore loser's challenge on a later visit (2.8.1, the rematch, if picked). Children run races only.

**With and without other mods.** Hospitality's guest beds for a guest who stays the night. Recreation buildings from
mods count by their recreation kind. Targets for the shooting match from Combat Training or Vanilla Furniture Expanded -
Security, read by name. The Empire as above.

### 6. Marriage alliances

**What the player sees and does.** A match between a colonist and one of theirs: a notable (an heir, the envoy) or a
member of a notable's house (made into a person of the world when the match is made). Three ways in:
- courting an outsider (built) that has made them lovers: "Speak for a match" turns it into an alliance;
- the council seeks a match for a colonist (the Diplomacy page lists who would fit: the game's own eligibility,
  orientation, the faith's rule on spouses, unmarried, and the colonist asked first);
- they offer one (once a year at most per people, when their head thinks 40 or more of the colony).

The terms: who goes where (theirs comes to live at the colony and joins at the wedding, or ours goes to live in their
town and leaves the colony for good; decision 8), gifts by their rite (carried both ways at the gate), and the
wedding: at the colony, their kin as guests, the game's ceremony through `WeddingPlans` (with Ideology, the faith's
ritual), or at their town, told. A match pressed on a colonist who doesn't want it comes to the council with their
objection.

While it holds, their people answer the colony's call as a defence pact does and call on it in turn (one pact of help,
not two), their notables think 15 better of the colony, kin visit once a season with a small gift, and they write when
trouble is coming. A child of the marriage is kin to both: while one lives, the alliance outlasts a spouse's death.
The Household page lists marriages and in-laws.

**How it works.**
- A Marriage tie from their people (or their house, round d) to the colony, about the spouse who married in, key
  `spouse:pX`, since the wedding, no end. Ended at a spouse's death with no child living; broken when the spouse is
  wronged by the colony (killed by a colonist, executed, imprisoned, sold, banished), with a Grudge from their house
  (key `spouse`) that passes to its heir (round d); ended by the couple's own divorce or breakup, their regard falling.
- The fiancé relation is set when the match is agreed; theirs joins the colony on arriving (`SetFaction`) and the game
  marries them; ours leaves with their kin, becomes one of their people, and is kept as a person and a notable of the
  town (`KeepFor.Spouse`).
- History `tie:marriage:made` (a milestone), `dip:wedding`; deeds `married`, `spouseWronged`. Facts: `tie:marriage`,
  `{INLAW}`, `{SPOUSEOF}`.

**Saved and bounds.** Ties only (three per people at most, 60 in all).

**Ages.** A match between clans, houses, families or firms, a partnership contract between lines (ours); bride gifts
and a shared fire, vows in the hall and a dowry, a wedding and papers signed, a contract witnessed and filed (theirs).

**Bad side.** A spouse unhappy among strangers (the game's moods; the heart's belonging); the in-laws' calls drawing the
colony into their fights; a spouse who dies untended or in a raid left in the open; a spouse wronged, and the feud
carried down to their house's heir, with challenges at the gate (the harm review's 2.9.1 and 2.9.2, if picked).

**With and without other mods.** Diplomatic Marriage+: read only, as today. Its permanent alliance shows on the pages as
the mod's own and counts once toward the colony's renown; Rimfolk never calls its methods or adds its own help to it.
The Empire: a titled noble marries only a titled colonist (decision 9).

**As reshaped (the player's decision, 5 November 2026).** "The colony is the 'family' so to say, so mechanics should
revolve around that": a marriage alliance is a bond between the colony and their people, made through one marriage, never
two families or houses joined. One of theirs who is not a notable is one of the town their head speaks for, nobody's
child or kin (`Matches.MakeOfTown`), where it was a son or daughter of the head's house. A child no longer keeps the
alliance: it ends at a spouse's death, with no grudge unless the colony was to blame (killed by its hand). A grudge for a
spouse wronged is their people's, which their head speaks for, handed down no line (decision 8's "a child keeps it" and the
design's "passes to its heir" are dropped). "In-laws" are simply their people: the season's gift comes from their town,
the raiders' camp warning from the town bound to the colony, Here and away lists the peoples the colony is bound to by
marriage, and the wedding's guests are notables of their town (as they always were in the code). The spouse's own family
(the one who walks in with them, their love sent in letters) stays theirs, as everyday life. The words: the match by the
colony's age is a match between clans, a marriage pact between two peoples, a marriage between two settlements, a
partnership contract between settlements. An older save: an alliance held through a child ends without a word, a grudge
held by a head is made their people's quietly. DESIGN.md, "Marriage alliances", has the whole of it.

### 7. Wards and hostages

**What the player sees and does.**
- *Their ward at the colony*: a youth of a notable's house (a child with Biotech, else someone of 14 to 19) comes for a
  year to learn a skill the colony is known for, from a guardian (a colonist with 10 or more in it). Offered by them
  once a year at most per people (regard 30 or more), or asked for by letter. The ward walks in with their escort and
  lives as the game's lodger: on the colonist bar, a bed of their own, working with the colony; the guardian teaches
  them an hour a day when both are free and together (with Biotech, a child at the game's lessons). The Household page
  shows the ward, their guardian, what they have learned and how they are ("homesick", "settling in", "happy"). At the
  year's end their family come for them; how they were kept moves their people (taught, fed, unhurt, content: a favour
  owed), and the ward remembers: grown into an heir or a head, they still think well of the colony.
- *Our ward with them*: a youth of the colony goes to learn what their people are good at, away for a year, writes
  home, and comes back with the skill and a friend there.
- *Hostages*, given for a pact or a truce (adults only): theirs lives at the colony as a lodger for the term, ours at
  their town. At the end both go home (a pact's at the end of each pact year, even when the pact is renewed; a renewal
  may ask for a new hostage as its own term). If their side breaks the terms, their hostage is the colony's to decide about:
  send them home anyway (`forgave`), arrest them (the game's own arrest makes them a prisoner, and ransom applies), or
  take them in if they would join. If the colony breaks them, ours is held by their people (decision 10).

**How it works.**
- A lodger quest of Rimfolk's (the missions' kind, with the game's `QuestPart_ExtraFaction`, their people the home
  people, and a part of its own for the terms): in the Quests tab, arriving and leaving on foot with an escort.
- Ward: a tie from their people (or house) to the colony, about the ward, key the skill, until a year; kept when they
  go home well, ended otherwise. Hostage: a tie from whoever gave them to whoever holds them, about the hostage, key the
  tie it guarantees; called in when that tie is broken.
- Ours away: a colonist lent for the term, in the world's pawns as the game's lent colonists are, kept with
  `KeepFor.Lent`, walking back in at its end. A forfeit hostage of ours goes onto their `KidnappedPawnsTracker`: the
  game's own captivity, with its ransom demand (or the messenger's, item 3), its rescues, and in time joining them.
- History `tie:ward:*`, `tie:hostage:*`; deeds `fostered`, `hostageReturned`. Facts: `tie:ward`, `tie:hostage`,
  `{WARD}`, `{HOSTAGE}`, `{GUARDIAN}`, `them:fostered`.

**Saved and bounds.** Per home one ward each way and one hostage each way per tie, four in all; ties and the quest.

**Ages.** Fostering with the other clan, a ward at their hall, an apprentice exchange, an exchange posting (ours); a
child raised among them, a youth in their household, an apprenticeship under contract, a posting on agreed terms
(theirs). Hostages get forms of their own (below), so the ward's rites no longer speak of them.

**Bad side.** A ward hurt or killed in the colony's care (raids, illness): their family's grief, little if the colony
sheltered them, much if not, a feud if by a colonist's hand. Homesickness (the ward's own mood). A forfeit hostage of
ours in the game's captivity until ransomed or rescued, and in time lost to them. Theirs forfeit at the colony is the
player's to decide; an execution, which only the player can order, is the gravest wrong their people remember.

**With and without other mods.** Biotech: children and lessons. The Empire: our youth may serve as a page in a titled
household (decision 9). Hospitality: lodgers are not its guests.

**As reshaped (the player's decision, 5 November 2026).** Their ward is a youth of their town, sent by their head, heir
or envoy, never of a notable's house: nobody's child, a name of their own (`Wards.MakeYouth`); their own family walks them
in and comes for them. The colony's own youth goes to them in return as before. The parents' feelings at home stay, being
personal (a youth of the colony's sent away, `Nature.WardGoes`). Hostages are as they were. DESIGN.md, "Wards and
hostages", has the whole of it.

## Round c

### 8. The keeper of secrets and council tasks

**Dropped by the player's decision, 5 November 2026** ("drop council tasks for sure", "remove keeper of secrets"; the
rule since that day: the colony is the family, never a dynasty). Built as release 2c.1 (0.96.0) and given its text
pass, then taken out of the game: no office takes a task, there is no keeper's seat, and the colony's own secrets slip at
their base rate again. Fealty and vassals (items 9 and 10) run without the quartermaster's dues: a vassal's tribute is
its harvest, the liege's is collected at the gate and what is owed sent by "Send what is owed". An older save loads
clean (DESIGN.md, "The keeper of secrets and council tasks (dropped)"). Schemes (item 11), when built, have no keeper:
its parts that lean on one (the keeper's help, its watch, "Sound out") need another holder or go. The design as it was:

**What the player sees and does.** A new office, the keeper of secrets (Social and Intellectual; opens at six voters;
decision 13), elected like the others; while the seat is filled the colony's own secrets slip less often. Each office
with a task below may take one at a time, chosen on the Decrees page's row for the office or from the Diplomacy page
(the target first, then who). A task names its target (a people, a town, a notable, or the colony's own vassals) and
goes forward in steps the player sees:
- *a letter*: the officer sits at a table and writes; their messenger fetches it; the answer comes back;
- *a call*: with a powered console and an industrial people or later, the officer calls;
- *a visit*: once a season at most per officer, at most one officer away per home (decision 14): the officer and a
  companion pack road food, walk out to the edge, and go as a caravan on the world map to the town, stay a day there,
  and come home; their work waits;
- *a guest received*: the target's notable comes (the invitation's visit);
- *a cart sent*: goods for the quartermaster's and the vassals' tasks.

Each step's result shows where the player looks: the notable's regard on the town panel moves with a line saying why;
a favour, an offer of a pact, a secret learned, goods arriving. The Household page lists each officer's task, the last
step and the next in words ("means to ride to Ashford once the snow clears").

The tasks:
- *Speaker*: win them over (a letter every four to six days, a visit a season; the head's regard up by Social; at 60,
  an offer of a pact or a favour owed); speak for us (in a dispute world event, the existing deed `mediated`).
- *Keeper of secrets*: learn their secrets (talks with their visitors and traders when they are here, a paid informer
  met at the gate, a visit; each step a roll of Social and Intellectual against the notable's: a Secret tie on success,
  found out on a bad roll, one in ten); watch the guests (walks with visitors after dark; their schemes caught twice as
  often, item 11); guard our own (the colony's secrets slip half as often).
- *Marshal*: scout the land (a patrol along the edges at dawn; a raiders' camp, stage 1, found sooner; strangers
  seen); drill the levies (a visit to a vassal or pact partner; their band a fifth stronger when it comes).
- *Quartermaster*: open a trade (letters and a visit to their trader: a trade pact offered, or goods ordered for their
  next caravan, the existing bring-next-time favour); collect the dues (Rimfolk's own vassals' tribute, on time or
  explained).
- *Physician*: tend their sick (a town with a sickness running: a visit with medicine from the stores; the existing
  deeds `tended` and `campHealed`).
- *Warden*: bring ours home (a colonist held by them: the price of ransom or an exchange lowered by Social); their kin
  in our cells (offers for captives sooner and higher).
- *Chaplain* (Ideology): keep the faith with them (a people of the colony's faith: regard and certainty); speak for the
  faith (another faith: a notable's certainty moves a little; never a conversion beyond the game's own rules).
- *Steward*: host them (prepares the next feast with guests; the guests' regard counts half again).

**How it works.** `CouncilTask` per office in each colony's council (`ColonyState`): the office, target ref, kind, step,
when the last step was, when the next is due (never shown), results. Steps run in `Pass.Diplomacy` with the existing
jobs (writing, calling, the guests' visit) and the caravan calls; the caravan is the player's own and can be turned
back. `TaskMath` decides timing, results by skill and the rolls. History `dip:task`. Facts: `us:task:KIND`; their talk
mentions an officer who writes often.

**Saved and bounds.** A task per office, thirteen per colony at most; the keeper's secrets as ties (twenty live).

**Ages.** The elders' errands, the council's offices, the committee's portfolios, the board's remits (ours); a word
carried to their elders, an envoy received at court, a delegate at their committee, a liaison to their directorate
(theirs). The keeper of secrets keeps its title in every age; its words come from the schemes' forms.

**Bad side.** An officer on the road meets the game's caravan incidents. A physician visiting a sick town may bring the
sickness back: one visit in five, the game's own disease on the caravan, named before the choice. The keeper caught at
it costs regard, and the one they were watching remembers them (hard words at the next visit).

**With and without other mods.** Faction Territories: the dues of its vassals are its own vassalage points, so the
quartermaster collects only from Rimfolk's own vassals (decision 12). Factional War: none. The Empire: a titled head
receives a titled officer more warmly (regard half again).

### 9. Fealty to a stronger neighbour

**What the player sees and does.** The colony can swear fealty to a people at peace (goodwill 40 or more) with two
towns or more within reach; or such a people offers protection when the colony is struggling (raided twice in a season
with few fighters). "Swear fealty..." on the Diplomacy page shows their terms by their ways: tribute each season (150
to 600 silver's worth by the colony's wealth), levies when called (two fighters for up to five days), and their
protection. The council hears it, with those who object (the proud, an avenger, one of another faith). The oath: their
lord's retinue comes to the gate and the colony's speaker swears by their rite.
- *Tribute*: each season their collector comes to the gate and carries off the agreed goods (`Couriers.Collect`); short
  or late, it is owed (called in), and unpaid by the next season the oath is broken (20 goodwill and the deed).
- *Levies*: when their town is attacked or they go to war (a war world event, a Faction Territories invasion), their
  call takes two colonists to their town, and the fight is on the game's caravan battle map (or the mod's invasion
  fight).
- *Protection*: when a home is raided, their band comes without a call (a war band, half the threat), at most once a
  season; nine times in ten. A season in which they failed to come lets the colony leave without blame once.
- *Leaving*: renouncing is a council question; it ends the oath with their grudge (30 goodwill, `renounced`), unless
  they failed to protect.

**How it works.** A Fealty tie from the colony to the people (key `liege`; tribute and levies in its terms); called in
when tribute is owed; ended when renounced cleanly or the liege is gone; broken when tribute is refused or the colony
attacks them. The grant of land a scenario gives is already such a tie and stays as it is (no tribute) unless the
colony swears anew. History `tie:fealty:made` (a milestone), `dip:tribute`, `dip:levy`; deeds `swore`, `tribute`,
`tributeShort`, `levies`, `renounced`. Facts: `tie:fealty` and `{FEALTYTO}` exist, with `:due` for tribute owed.

**Saved and bounds.** One liege per colony; a tie.

**Ages.** Tribute to a stronger clan, fealty and tribute, a protectorate, a charter (ours); protection for tribute, an
oath of fealty and levies, a company town and a union's dues, a corporate accord (theirs). The levies have forms of
their own (below).

**Bad side.** Tribute that bites in a lean season; levies wounded or killed in someone else's war; renouncing costs.
Their enemies stay the game's business (no goodwill drift with them; decision 11).

**With and without other mods.** The Empire: no fealty to it through Rimfolk; a colonist holding one of its titles shows
on the pages as sworn to it, a view from `GetCurrentTitle`, with its permits as its protection (decision 9). Faction
Territories: its invasions call the levies; it has no fealty for the player's colony to swear. Factional War: a battle
between the liege and its enemy on the colony's land is fought by whoever the colony sends there.

### 10. Vassals of one's own

**What the player sees and does.** The colony's vassals come three ways: the hamlet that swears fealty (stage 1, item
6); Faction Territories' vassals, as the mod makes them; and a struggling town within reach (under 20 prosperity for 30
days) whose people are at peace with the colony and at war elsewhere, asking to be taken under its protection, once a
year at most per home. Each vassal's head is a notable who writes; how they think of the colony shows on the Diplomacy
page, with what moved it.
- *Tribute* (Rimfolk's own vassals): at harvest their carter brings produce to the gate, the amount set by the council
  (light, usual, heavy: heavy costs their goodwill each season).
- *Levies*: when a home is raided the council may call them (their people's war band, a third of the threat), at most
  once a season; their dead weigh on their head's regard.
- *Asks*: help in hard times (the help letters and missions), the colony's judgement in a quarrel with a neighbour,
  protection from a threat near them (a raiders' camp: the colony may break it).
- Their head thinking little of the colony for a season (-20) asks to be released; refused, the tie is broken.

**How it works.** A Fealty tie from the vassal town to the colony (Rimfolk's own), or the adapter's view (Faction
Territories); the head's regard is the notables' own. History `world:vassal` exists, with `dip:tribute`, `dip:levy`.
Facts `tie:fealty:theirs`.

**Saved and bounds.** The hamlet and up to two protected towns per home; Faction Territories' vassals are views, nothing
saved.

**Ages.** The hamlet's protection forms from stage 1 (the clan's spear, a lord's protection, a protectorate, a
charter); their head's title by their age.

**Bad side.** Levies wounded or killed and their people's grief; the colony called to defend them; heavy tribute
breeds a head who wants out.

**With and without other mods.** Faction Territories (decision 12): its vassalage points, purchases and outposts stay
the mod's; Rimfolk gives each outpost a head (a notable of its first people), letters, opinion and asks, and takes no
tribute or levies from it. The Empire's towns never become vassals.

### 11. Schemes

**What the player sees and does.**
- *Ours.* From the Diplomacy or Household page, or the keeper's right-click "Sound out...", the player picks a schemer
  (a colonist, with the keeper's help), a target (a notable, a town, a visiting trader) and a kind:
  - gentle, as before: sway (a notable's stance toward the colony or a proposal), befriend, court across visits,
    uncover a secret (the keeper's task);
  - dark (decision 15): spoil their trade (at night, while their caravan is at the colony, the schemer slips to their
    pack animals and ruins or takes part of the goods; their town's fortune falls a little), theft (something of value
    from a visiting notable's packs, carried to the colony's stores), a provoked quarrel (whispers to two of theirs who
    visit together, who come to blows in the game's social fight; their town is divided for a season), a smear (letters
    to other peoples; the target's regard among them falls).
  Each step needs the target or their people on the colony's map, so the dark steps wait for a caravan, visitors, an
  envoy or feast guests; done at their town, a step is only told and harms nobody. Each step rolls the target's notice
  (their Social and Intellectual and their guards awake) against the schemer's and the keeper's. Found out, it shows:
  their guard catches the schemer at it with a shout, a fistfight or their leaving in anger, the goods taken back;
  their people remember (`schemed`), the schemer's name with them (`KinMemory`), and a challenge may come later.
- *Theirs* (decision 16). A people with a grudge within reach, or a notable who thinks -40 or less of the colony, may
  scheme while someone of theirs is on the colony's map: a visitor or trader slips away at night and breaks a machine
  (industrial and after), takes a small valuable from a stockpile (a totem, a purse, a tool), or provokes a colonist
  with a hot temper into a fight; an envoy may spy and learn a colonist's secret (a Secret tie of theirs, pressed later:
  a demand, refused and the secret is out); a smear goes round the region. Caught by a colonist awake, the keeper on
  watch or a dog: confronted on the map, the thing dropped, the agent gone at once, their people remembering the
  colony caught them (`schemedUs`, 5 goodwill). The Household page lists what was found out.

**How it works.** A Scheme tie from the schemer to the target (key the kind, its progress and steps in its terms, the
keeper in `about`, until a year): running while open, succeeded when kept, found out when broken, given up when ended.
Theirs are ties too, shown only once found out (the keeper's watch may report one running). Secret ties from the knower
to the one it concerns, called in when pressed: complied with (kept), refused and revealed (broken), ended when it no
longer matters. `SchemeMath`: progress by skill, notice rolls, their rate. The thefts and the night walk are Rimfolk's
own jobs; the machine is the game's breakdown. History `tie:scheme:*`, `dip:foundout`. Facts only for what was found
out: `them:caught`, `tie:scheme:theirs:broken`.

**Saved and bounds.** Two of ours running per home, one of theirs; twenty secrets live; ties only.

**Rates.** Theirs: while such a people or notable is within reach, a roll every 15 days at 8% (about two a year), at
most one a season per home, acted out only when one of theirs is on the map and given up after a season; by the
setting. That is about as often as the game's own breakdowns in a colony of ten machines. Ours: the player's choice,
the risk of being found out named on the page before it starts.

**Ages.** Whispers at the fire, a paid informer, a bribe or sabotage, a hack or a data leak (ours); a stolen totem,
intrigue at court, a smear in the papers, a corporate plant (theirs). A dark scheme takes the target's means: a stolen
totem from a tribe, a cut purse, a jammed machine, a console's breakdown.

**Bad side.** It is one, both ways: found-out schemes bring fistfights, challenges and grudges; theirs break machines,
take small things, start fights and learn secrets. Never killing, poison or fire: nothing gratuitous. Under the shared
cap.

**With and without other mods.** The Empire: a titled colonist found scheming against it loses honor (decision 9).
Hospitality's guests can be agents like any visitors. The harm review's 3.5.1 (a rival's sabotage between colonists),
3.9.1 (a secret found out by the wronged one) and 2.9.1 (a challenge by letter), if picked, share this machinery.

## Round d

### 12. House renown (reshaped as the colony's renown, 5 November 2026)

**What the player sees and does.** The colony's families known by name (two or more related colonists sharing a last
name, `FamilyNames`) are houses. The Household page lists each: its members, its renown in its age's words ("a name
known in the valley"), what it is known for, its marriages and wards, its feuds, and the deeds it came from. Renown
grows when the world sees a member do something: a duel or a contest won, a year in office, a marriage into a notable
house, a ward fostered well, a favour earned, an oath or pact sworn, a raid's leader brought down; it falls with a truce
broken, a scheme found out, a call refused, a challenge run from. It does no mood (decision 17):
- notables think up to 15 better of the house's members;
- their good words count up to half again;
- matches with a renowned house are offered and accepted up to twice as readily;
- outsiders speak of the house by name;
- what it is known for (fighters, healers, hosts, traders, makers, the faithful: whichever most of its renown came from)
  tips that part of the world toward it: sick visitors ask for a house known for healers, challengers come to one
  known for fighters, guests accept a house known for hosting more readily.

Their houses, the towns' founding families, have renown too, moved by their ties with the colony. A feud between houses
(a Grudge from their house against ours) passes to their heir when the notable who held it dies, and ends with amends
(a gift, an apology letter, a marriage between the houses) or after two of their generations.

**How it works.** `LedgerHouse` gains renown, six known-for counts and, for ours, a seat (the colony's key); the
colony's houses are made as families form (twelve at most), kept while a member lives or a tie names them, then two
years. `RenownMath`: gains by deed kind, read from the history's index of who did what; bands; a little fading each
season toward a floor. History `dip:renown` when a band changes (a milestone). Facts: the new family `house:`
(`house:renowned`, `house:knownfor:KEY`, `them:house:feud`) and `{HOUSE}`.

**Saved and bounds.** Twelve houses of ours (about 200 bytes each); theirs exist (80).

**Ages.** A clan's name and totem, a house and its arms, a family name and a family firm, a lineage (ours); deeds told
to the children, sung in the hall, in the papers, a name that carries (theirs).

**Bad side.** Renown lost by what members do; feuds carried down; a house known for fighters draws challengers.

**With and without other mods.** The Empire: a title held by a member adds renown (read, never given). Diplomatic
Marriage+: its alliance counts once. Nothing else.

**As reshaped (the player's decision, 5 November 2026).** House renown becomes the colony's renown: the colony's name
among the peoples, how far it carries and what it is known for, built from everything its people do, the living and the
gone, and from its own oaths kept and broken. Each colonist gets a name of their own for their deeds, read the same way
from what they alone did and said by the deed they are best known for ("the one who won the wrestling at the feast with
the Lenlia"), on their card and in talk with a visitor whose people know it. The bands, the known-for, the sources with
their points and fading, and the bounded effects stay, read for the colony as a whole (regard up to 15 and down to -10,
good words, matches never certain, events, a feast's guests); a colonist known by name as a fighter draws the champion's
challenge, and one known for something draws its events a quarter more at most. Houses, the Houses page and House cards go,
and so do other towns' notables' houses: their town is what counts (the towns' founding families stay as their story,
with no renown). Feuds are not handed down family lines: a grudge stays between their people and the colony, or against a
named colonist. People → Renown is where People → Houses was. An older save's houses are dropped as it loads, and the
colony's band is set at the first day's look without a word. DESIGN.md, "Colony renown", has the whole of it.

### 13. Stress against one's nature

**What the player sees and does.** A colonist who does, or is made to do, something against their nature takes it to
heart: their strain rises, and the heart's page says why and what eases it, the way the pressures do ("swore the oath to
the Lenlia, against his pride; a quiet evening with friends would help"). Done with their nature, it eases. The acts, by
the sides of nature they go against (`Persona/`):
- swearing fealty: the one who swears, if proud;
- a dark scheme: the schemer, if warm or trusting (eased for the cold and the psychopathic);
- breaking a truce or a pact: whoever carries it out, if trusting;
- giving a hostage: the hostage if timid, their close kin if warm; sending a ward away: the parents;
- a match against their heart: someone who doesn't love the one chosen, or loves another;
- refusing a challenge: the hot-tempered and the proud; accepting one: the timid;
- a season away as envoy or hostage: the closed (eased for the open);
- haggling for a captive their family is waiting for: the warm;
- a truce with the people they swore vengeance on: the avenger, always.

The council's objections name the nature ("Ada is too proud to kneel to anyone"). The heart does the rest as it does
for everything: its bands (a small mood and slower work), its episodes as the pressure valve, its reliefs (friends,
gatherings, the chaplain).

**How it works.** `NatureMath`, pure: a small table of acts (side, sign, threshold) against a person's eight sides gives
the strain (up to 12, by how far beyond the threshold) or relief (up to 6). Strain goes through
`HeartLogic.Change(p, amount, buildSource: StrainSource.Nature)` and relief through `Interplay.Ease(p, amount,
StrainRelief.Nature)`, both new kinds at the end of their lists (saved by position), so they count in the heart's caps
(about 25 a day for everything), scaled as the pressures' bad side is (`Pressures.LifeFactor`, the "Life's ups and
downs" setting, and the hard-times factor). Repeated acts can leave a mark (`PersonaMarkDef`: three dark schemes in a
year may harden a kind one, an oath sworn may humble a proud one), which is the persona's own data. One act a day per colonist at most. The service is written in round a with every act listed and
no effect, so each round names its own acts as it ships; round d turns it on (decision 19).

**Saved and bounds.** Nothing new: the heart's totals per source gain one entry per colonist.

**Ages.** The same in every age: the nature is the person's.

**Bad side.** Strain and what it brings (the heart's bands and episodes; the harm review's frayed tempers, 3.3.1, if
picked).

**With and without other mods.** Traits from Vanilla Traits Expanded and other mods count through the
`PersonaNatureDef`s that already read them.

**As built (release 2d.2).** Where it differs from the above (DESIGN.md, "Against their nature", has the whole of it):
- Relief goes through `HeartLogic.Relieve` with the nature's own one-a-day cap, not `Interplay.Ease`, which counts every
  kind but gatherings against the friends' cap. Strain goes through `HeartLogic.Build`, the hour's own building (the
  strength setting, traits, the source's cap of 12 and the day's 25); `HeartLogic.Change` applies no caps.
- The scaling reads the "Life's ups and downs" setting itself (`PressureMath.Factor`), not `Pressures.LifeFactor`, which
  is 0 when Rimfolk changes no moods: the strain is no mood. Hard times as the pressures (`HardTimesPressure`).
- Tuned with the balance simulator (`Tools/Balance -- --nature`): just past where a side shows an act brings 0.4 of its
  most (about 5, where the service had a quarter, 3), so it shows on the heart's page for days; the rest as written.
- Breaking a truce or a pact: "whoever carries it out" is whoever the game names for the deed that broke it (the blow's
  hand, the captor, the seller, the first of the caravan that attacked theirs, the killer); a town attacked or destroyed
  and a harmful ability or item name nobody, so nobody feels those. A pact's call refused by the council's answer weighs
  on the speaker who sends the refusal. Breaking the oath of fealty and a protected town's tie count too.
- Haggling: the speaker who asks more for a captive whose family waits (as written), and the captive's own close kin
  among the colony, if warm.
- Refusing and accepting a challenge: the travelling champion's and the rematch's (the challenge renown sends to a
  fighters' house), turned down by the council's answer, faced at the contest itself. The feasts' contests are planned
  by the colony and have no challenge to refuse or accept.
- A hostage given is a season away too: one act a day, the one that weighs most. Hostages, wards and envoys feel it as
  they walk off the map.
- A truce with one's foe reads the vendettas (`Secrets/Vendettas.cs`): each avenger of the home whose vendetta is open.
- Marks: an oath may humble a proud one (half the time); the same act against the same side twice within a year may
  change that side (a word broken twice, a challenge faced twice, a second season away). The dark scheme stays unwired
  and its mark undefined until schemes (2c.3) are built.
- Saved: four values on each heart (the last act's tick, kind, whether it eased, its words); no new list.
- Shown: the heart's page names the act and the side, the news tells it (two wordings or more), the council's questions
  name it before an answer that asks it (the oath, the truce with one's foe, a match pressed, a challenge), and the
  oath's objection has two lines that name a proud colonist's nature. No bubbles: the acts already show on the map where
  they happen (the oath at the gate, the hostage, ward and envoy walking out, the contest).

## Forms by age

The colony's word (its means) / their rite (their ways), as keyed strings beside the ten that exist
(`Rimfolk_Ages.xml`), in `Ages.Form`:

| Feature | Tribal | Medieval | Industrial | Spacer and beyond |
|---|---|---|---|---|
| Truce | a peace at the river / green branches carried between camps | a truce under a flag / a truce sworn on the hilt | a ceasefire / terms signed at a crossing | a ceasefire by comms / terms logged and witnessed |
| Hostage | a pledge of the clan's blood / one of the chief's kin given | a hostage for the pact / hostages exchanged before witnesses | a guarantor / a guarantor held to the contract | a guarantor posted / a guarantor held to the terms |
| Envoy | a guest-friend by the fire / a guest-gift and a seat by the fire | an envoy at court / letters of credence | a consul / credentials presented | a liaison / credentials sent ahead by comms |
| Levy | the clan's spears / warriors at the chief's call | levies / men at arms raised | a militia / volunteers mustered | a security detail / contractors deployed |

With the hostage's forms its own, the ward's rite strings say the ward's side only (a child raised among them, a youth
in their household, an apprenticeship under contract, a posting on agreed terms). The means by age, as letters already
have them: a runner or a fire on the hill, a rider, the radio, comms. The Empire keeps its titles.

## Other mods, all in one place

| Mod | What Rimfolk reads and does | What it never does |
|---|---|---|
| Faction Territories and Vassalage | Its vassals as Fealty views with faces, opinion, letters and asks; its invasions of a partner's or liege's town as calls, answered through its own arrival; an ambush of a truce people in its territory breaks the truce | Takes tribute or levies from its vassals, patches its invasions, ambushes or vassalage |
| Diplomatic Marriage+ | Its permanent alliance as a Marriage view on the pages, once toward renown | Calls its methods (`IsValid` changes the alliance), adds Rimfolk's help to its alliance, uses its system |
| The Empire (Royalty) | Titles (`GetCurrentTitle`), honor in two named places, titled envoys, guests and nobles; truces, favours, ransom, feasts | Fealty to it, pacts with it, its towns as vassals |
| [SR] Factional War | Its battles on a home: the sides named at the start, a pact kept or a truce broken by what the colony does there | Wars between peoples of Rimfolk's own |
| Hospitality | Its guest beds for envoys and feast guests | Changes its guests |

## The order to build it in

Stage 2 is waves 11 to 14 of the plan, after stage 1's last release; each release plays as before with its settings
off and every check green, version numbers as they fall. Each round has its content pass beside it (talk pieces,
letters by purpose and age, news, an Almanac page).

1. **2a.1 Diplomacy's base and favours** (wave 11): the Diplomacy and Household pages, the new letter purposes and
   right-click acts' frame, the `dip:` family and the new deeds, the four new age features, the Secret and Scheme tie
   kinds reserved (rules and bounds, no use yet), the nature service with no effect, the settings section, and favours
   owed and called in (item 1). Needs stage 0 and the region window.
2. **2a.2 Pacts and truces** (item 2): the raid patch, calls by fire or console, war bands, calls on the colony and the
   caravan fight, Faction Territories' and Factional War's hooks. Needs 2a.1.
3. **2a.3 Ransom and the envoy** (items 3 and 4): offers, the messenger's demand, exchanges and the trap; the envoy's
   season both ways. Needs 2a.1; the envoy's long stay is what the feast guests reuse.
4. **2b.1 Feasts and contests** (item 5; wave 12). Needs 2a.3's hosting.
5. **2b.2 Marriage alliances** (item 6). Needs 2a.2 (the alliance helps as a pact) and 2b.1 (wedding guests).
6. **2b.3 Wards and hostages** (item 7): the lodger quest, lent colonists. Needs 2a.2 (hostages for terms) and 2a.3
   (ransom for a forfeit hostage).
7. **2c.1 The keeper of secrets and council tasks** (item 8; wave 13): the office, tasks, the officers' caravans,
   Secret ties in use. Needs round a and 2b.1 (the steward's task).
   Dropped by the player's decision, 5 November 2026, and taken out of the game (item 8).
8. **2c.2 Fealty and vassals** (items 9 and 10). Needs stage 1's hamlet, 2a.2's calls and bands, and 2c.1's dues (the
   dues went with 2c.1).
9. **2c.3 Schemes** (item 11). Needed 2c.1, now dropped: the keeper's watch and Secret ties' code are gone; the harm
   review's rival sabotage (3.5.1) first, if the player picks it.
10. **2d.1 House renown** (item 12; wave 14). Reads every deed and tie before it.
11. **2d.2 Against their nature** (item 13): the acts of every release switched on and tuned with the balance simulator.
    Built: every act but the dark scheme (item 13, "As built").

## Performance

- Nothing per tick at world level. Two passes, each in Sched's table with its `Pass` entry and timed by Rimfolk's
  clock: `Pass.Diplomacy` hourly on a free tick of the cycle (`Sched.HourlyOnCycle`), only with work waiting (an envoy
  staying, a task's step due, one of our caravans away, an offer for a captive, a feast day, a term ending, a scheme
  waiting for its target); `Pass.DailyDiplomacy` once a day (their calls and offers, tribute due, renown's fading, the
  schemes' rolls). Ties reach their ends in the ledger's own upkeep.
- Hooks act when the game does: the raid patch reads a set kept for a 250-tick stretch (a few lookups a raid roll);
  breaches come from the memory's deeds as they are noted; the adapters' hooks exist. Our caravans are found by tile
  in the hourly pass (three at most).
- Pawns: an envoy and an attendant, up to four wards and hostages per home, in-laws and house members made only when a
  match needs one, war bands as today.
- Pages are built every two seconds while open, nothing while closed.
- Save: about 100 more ties at five years (some 10 KB), per home an envoy each way, thirteen tasks, a feast plan, six
  offers and three schemes, twelve houses: under 25 KB a home. Each new list gets its model in `Tools/save_estimate.py`
  before it ships; the history's new kinds fold within its bound of 800.

## Tests and checks

The rules are pure and tested in `Tools/Tests` (`DiplomacyTests`, one section per item):
- Favours: earned by a deed's size, one each way, renewed, honoured by stance, their calls' rate and needs.
- Pacts and truces: who may offer, acceptance, the call's conditions and once a season, ends; the firebrands' roll
  (about one truce in ten over many terms); breaches read from deeds.
- Ransom: offers against the game's tuning, the haggle, the trap's rate against the game's negotiation factor.
- The envoy: the stay's score and its result.
- Feasts: guests' acceptance, contest scoring, one a season.
- Marriage: eligibility given the game's answers as inputs, where they live, ends, the child that keeps it.
- Wards and hostages: teaching hours, the year's result, forfeit both ways.
- Tasks: steps' timing, results by skill, one officer away.
- Fealty and vassals: tribute by wealth, the owed and broken clock, protection, release asked.
- Schemes: progress, notice, their rate (one a season at most, about two a year with an enemy near).
- Renown: gains, bands, known-for, fading. Nature: acts against natures, the caps.
- The ledger's rules for the two new kinds, and five simulated years of the daily rolls with every count within its
  target.

Existing checks that must stay green: `LeanTests` (the two slots), `TellTests` (no direct message), `LedgerTests` and
`HistoryTests` (kinds, family, bounds), `FactsTests` (1,500 talks identical with the new tags), `OutsiderTalkTests`,
`ImmersionTests` (one sentence), `ManyHomesTests` (each colony its own records; one home plays as before),
`WorldMechanicsTests` (the adapters stand down without their mods), the save model (`--strict`), the def validator (the
office, the quest def, the keyed forms), `labelcheck`, `almanaccheck` (keywords unchanged), `interaction_audit` (the
right-click acts together, letters and council answers apart), `playout_audit` (tribute, prices, gifts carried), the
balance simulator (no new moods; strain within the heart's targets), and the 1.7 API diff for the new game names
(`IncidentWorker_RaidEnemy.FactionCanBeGroupSource`, `KidnappedPawnsTracker.RemoveKidnappedPawn`,
`CaravanExitMapUtility.ExitMapAndCreateCaravan`, `Caravan_PathFollower.StartPath`,
`CaravanIncidentUtility.SetupCaravanAttackMap`, `QuestPart_ExtraFaction`,
`SpouseRelationUtility.GetHistoryEventForSpouseCount`, `RelationsUtility.RomanceEligiblePair`,
`Pawn_GuestTracker.SetExclusiveInteraction`, `Pawn_RoyaltyTracker.GainFavor` and `TryRemoveFavor`,
`CompBreakdownable.DoBreakdown`, `DiplomacyTuning.RansomFeeMarketValueFactorRange`).

In the game, with dev tools to force each part: a favour called in for fighters; a truce sworn at the gate and a raid
that doesn't come, then a firebrand's; a pact's band called by a fire; an offer for a captive, an exchange and the
trap; an envoy's season; a feast with a bout, a race and a hunt; a wedding with their kin; a ward's year; an officer's
visit by caravan; a tribute collected; a scheme found out at night; a house's renown changing band; an oath that weighs
on a proud colonist.

## Decisions for the player

1. **Truces keep their raids away.** While a truce holds, the storyteller doesn't choose that people for raids on that
   home (a small patch on the game's choice of raiders, with the deeper hooks); a raid or ambush of theirs that comes
   anyway breaks it, and their firebrands break one truce in ten. Or a truce in words and memory only? Recommended: the
   patch and the firebrands.
2. **Pacts by kind.** A defence pact and a trade pact (their caravan each season, a cart of the colony's surplus to
   them), or defence only? Recommended: both.
3. **Answering calls far away.** Colonists sent to a partner's or liege's town fight on the game's own caravan battle
   map, or with Faction Territories its invasion fight; or calls are answered at home only (goods, and fighters on the
   colony's own map)? Recommended: the fight away.
4. **Ransom without a console.** The game's ransom demand for a kidnapped colonist comes by messenger to a colony with
   no powered comms console, at the game's rate and fee (the game's own letter as always where there is one)?
   Recommended: yes.
5. **The trap at an exchange.** With a hostile people only, at the peace talks' disaster rate (5%, more with a poor
   negotiator, less with a good one)? Recommended: yes.
6. **The envoy.** A guest for a season (eats, sleeps, talks, works nothing), one at a time per home, and the colony's
   own envoy a colonist away for a season? Recommended: yes.
7. **Contests by what the colony has.** Wrestling, races, hunts and games at the colony's tables in every age; a
   shooting match only with a mod's targets? Recommended: yes.
8. **Where a married couple lives.** The spouse from outside comes to live at the colony, or the colonist goes to live
   with them and leaves for good, chosen in each match; a child of the marriage keeps the alliance after a death?
   Recommended: both, and yes.
9. **The Empire.** No fealty to it and no pacts with it through Rimfolk (its titles and permits are its own fealty and
   aid); its titled nobles marry only titled colonists; honor moves in two places only, a favour called in from it (2
   honor to a titled colonist, as its peace talks give 1 to 4) and a titled colonist caught scheming against it (2
   honor lost); a youth of the colony may serve a year as a page in a titled household (regard only)? Recommended: yes.
10. **Wards and hostages.** Theirs at the colony as the game's lodgers (a quest in the Quests tab, on the colonist bar,
    working with the colony); ours away as colonists lent for the term; a forfeit hostage of ours in the game's own
    captivity (its ransom, its rescues, and in time joining them at its rate); children never hostages? Recommended:
    yes.
11. **Fealty.** Protection, tribute and levies, renouncing at a cost, and no goodwill drift with the liege's enemies?
    Recommended: yes.
12. **Faction Territories' vassals.** Faces, opinion, letters and asks from Rimfolk; tribute, purchases and points stay
    the mod's; levies and tribute of Rimfolk's own only from its own vassals? Recommended: yes.
13. **The keeper of secrets.** Social and Intellectual, opening at six voters, its edge "the colony's secrets slip less
    often"? Recommended: yes. (Dropped, 5 November 2026.)
14. **Council tasks.** Officers travel in real caravans for their visits (away from their work, on the game's roads), at
    most one away per home; tasks for eight offices now (speaker, keeper of secrets, marshal, quartermaster, physician,
    warden, chaplain, steward), and the reeve, herdmaster and keeper of days only if a task of theirs is wanted later?
    Recommended: yes. (Dropped, 5 November 2026.)
15. **Our dark schemes.** Spoiling their trade, theft from their packs, a quarrel provoked between two of theirs, a
    smear; never killing, poison or fire? Recommended: yes.
16. **Their dark schemes.** A machine broken, a small theft, a provoked fight, a spy who learns a secret, a smear; about
    two a year while a people with a grudge is in reach, one a season at most, caught by the keeper's watch, the awake
    and the dogs, with a rate setting at 100%? Recommended: yes.
17. **Renown.** Earned and shown in words, with a "known for" that tips the related systems, and nothing spent (no
    legacies bought with it)? Recommended: yes.
18. **Factional War.** A message at a battle's start naming a pact or truce partner among the sides; fighting beside a
    pact partner keeps the pact, harming a truce partner's fighters breaks the truce? Recommended: yes.
19. **Against their nature.** Strain through the heart only (no new mood), easing when someone acts with their nature,
    marks for repeated acts, the service written in round a and switched on in round d? Recommended: yes.

## The player's decisions (4 November 2026)
All nineteen as recommended ("all sounds great").
