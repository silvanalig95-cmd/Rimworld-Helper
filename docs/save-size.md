# Save size over a long game (estimate)

Written by `Tools/save_estimate.py` (run `python3 Tools/save_estimate.py` from the repository root; `--strict` fails
on a saved collection with no model, or one that grows without end). Every collection Rimfolk saves, the rule that
keeps it in bounds, and how many entries it holds after one, two and five in-game years (60-day years) in a
colony of 20, with the game keeping 30 more people it knows and 5 more each year. Rates are
generous guesses; sizes are rough XML sizes. Caps named as constants are read from the source. "Counted under" rows are
kept inside another record (a journal's lines, a heart's lists) and are already in that record's size.

Saved collections found: 659; modelled: 659; growing without end: 0.
Rimfolk's part of the save, roughly: **2,892 KB** after a year, **3,562 KB** after two, **3,942 KB** after five.

| Collection | Rule | Kind | 1 year | 2 years | 5 years | KB at 5 years |
|---|---|---|---|---|---|---|
| `Journal.cs:journals` | the living's, and the 30 departed written in most lately | cap | 30 | 50 | 50 | 468.8 |
| `Letters/LetterBook.cs:exchanges` | letters exchanged with the world, kept to be read: LetterBook.MaxExchanges at most (the newest LetterBook.MaxPerNotable with any one notable), each two or three pages, the newest LetterBook.MaxLong long letters whole | cap | 24 | 48 | 60 | 252.0 |
| `World/Ledger/History.cs:events` | HistoryMath.MaxEvents; small ones past 60 days fold into one per actor, kind and season, milestones stay; over it older seasons go first | cap | 720 | 800 | 800 | 250.0 |
| `Social/Gossip/Deed.cs:heard` | who knows each deed (30 at most a deed), with its deed | cap | 2,000 | 2,000 | 2,000 | 214.8 |
| `Persona/PersonaTracker.cs:memories` | PersonaTracker.MaxMemories, 8 per pair and 3 of a kind; past it by 60 the faintest go at once; faded ones and those of people the game no longer knows dropped at saving | cap | 300 | 600 | 600 | 187.5 |
| `Persona/PersonaTracker.cs:peopleSaved` | the colony's own people the game still knows; empty records dropped at saving | known | 33 | 36 | 45 | 159.1 |
| `World/Notables.cs:notables` | the towns' head, heir, envoy and trader (NotableMath.MaxNear + MaxDealt towns) and NotableMath.MaxAcquaintances faces met; with their notes and regards (a save that handed colonies over has four more for each town the colony was, HandOverMath.MaxTowns at most) | fixed | 42 | 42 | 42 | 106.6 |
| `Gods/GodBook.cs:acts` | the gods' acts the colony remembers, GodBook.MaxActs (MaxActsPerGod a god) | cap | 180 | 300 | 300 | 87.9 |
| `UI/WeekMemory.cs:dilemmas` | DilemmaLog.MaxRecords; each with what the answer was to do in numbers (about 500 characters, at most 1600), in words (about 400, at most 1200) and the flavour line under it (about 70, at most 200) | cap | 30 | 60 | 60 | 82.0 |
| `World/Ledger/WorldLedger.cs:ties` | ties between two parties: 3 of a kind per pair, 60 per kind (regard 300), TieRules.MaxTies in all; closed ones dropped after 60 days | cap | 60 | 120 | 300 | 76.2 |
| `Persona/TraitChanges.cs:watches` | one per colonist with its deeds and ten snapshots; known pawns only | colonists | 20 | 20 | 20 | 66.4 |
| `Letters/LetterBook.cs:correspondences` | each correspondence as it builds up: LetterBook.MaxCorrespondences at most, the longest quiet let go | cap | 9 | 18 | 45 | 65.9 |
| `Gods/GodLore.cs:entries` | the colony's book: an entry in the keeper's voice, with its date and witnesses, GodLoreMath.MaxEntries kept | cap | 30 | 60 | 150 | 61.5 |
| `Social/Gossip/ReputationTracker.cs:deeds` | 10 per colonist, 220 in all, faded ones dropped | cap | 200 | 200 | 200 | 58.6 |
| `Camps/CampGround.cs:plans` | every old site's layout as it was left, its constructions and its beds' places (about forty a site, six sites) | fixed | 240 | 240 | 240 | 46.9 |
| `Journal.cs:everydaySeen` | 24 days | window | 480 | 480 | 480 | 42.2 |
| `Defense/DefenseLines.cs:lines` | lines the player made (only their clicks), per map; gone with the map | fixed | 4 | 4 | 4 | 35.7 |
| `Lives/LifeNew.cs:read` | books read, once each per reader, the newest 600 (Lives/LifeNewMath.MaxRead) | fixed | 600 | 600 | 600 | 35.2 |
| `Persona/TraitChanges.cs:changes` | known pawns only, 8 per person, 200 at most; with their story | cap | 6 | 12 | 30 | 35.2 |
| `Dealings/DealingBook.cs:deals` | dealings asked, under way and closed: closed ones kept 60 days (a work built together kept while it stands), DealingBook.MaxDeals at most | cap | 24 | 48 | 80 | 32.8 |
| `Journeys/Journeys.cs:slots` | timetable hours changed for the last day (24 at most each, the night and the free hours about half), put back when they set off | fixed | 240 | 240 | 240 | 32.8 |
| `Gods/GodBook.cs:standings` | the colony's standing with a god at a home, once it has moved; let go once at rest at unknown and left a year | cap | 120 | 200 | 200 | 29.3 |
| `World/FactionMemory.cs:deeds` | 12 deeds per people (12 peoples), fading out | fixed | 144 | 144 | 144 | 28.1 |
| `Heart/HeartTracker.cs:hearts` | one per colonist, the gone dropped hourly | colonists | 20 | 20 | 20 | 27.3 |
| `Animals/AnimalBook.cs:ties` | pairs asleep side by side, friends and rivals, AnimalNatureMath.MaxTies at most, pairs first to go, ties let go when apart 40 days | cap | 60 | 80 | 80 | 25.8 |
| `Gods/GodBook.cs:deeds` | the deeds in a domain at a home a newborn god is born of: a year's, GodBook.MaxDeeds at most | cap | 120 | 240 | 240 | 25.8 |
| `Aspirations/Aspirations.cs:all` | goals of the dead and departed end; finished ones of the dead kept 3 years | known | 55 | 60 | 75 | 25.6 |
| `World/Ledger/LedgerPersons.cs:persons` | every notable, anyone a tie names and anyone kept in the world; the rest go at the day's tidy (LedgerMath.KeepPerson) | fixed | 145 | 145 | 145 | 25.5 |
| `Trade/TradeRecord.cs:goods` | goods the colony sold or bought, TradeMarketMath.MaxGoods at most, the one traded longest ago going first | cap | 48 | 50 | 50 | 24.7 |
| `Social/SocialWeb.cs:ties` | ties of the dead dropped daily, of the gone faded out | pairs | 57 | 57 | 57 | 22.3 |
| `Dilemmas/Dilemmas.cs:lastTick` | one per dilemma def | defs | 243 | 243 | 243 | 21.4 |
| `Ailments/AilmentTracker.cs:last` | last ailments 120 days, known pawns only | window | 120 | 240 | 240 | 21.1 |
| `Calendar/ColonyCalendar.cs:days` | 12 the colony's, 8 the player's, traditions and the founding, the customs kept | cap | 12 | 24 | 36 | 21.1 |
| `World/Ledger/LedgerPersons.cs:townRecords` | the towns in detail within the region map's reach (RegionMath.MaxTownsPerHome a home, one home here), the towns the notables live in, and a gone town for two years (RegionMath.KeepTown); each with its fortune and founding | fixed | 54 | 54 | 54 | 20.0 |
| `Aspirations/Aspirations.cs:counters` | counters of known pawns | known | 165 | 180 | 225 | 19.8 |
| `Strangers/Encounters.cs:lastTick` | one per encounter def, per colony | defs | 225 | 225 | 225 | 19.8 |
| `Dreams/Dreams.cs:recent` | 80 recent dreams | cap | 80 | 80 | 80 | 19.5 |
| `Outsiders/OutsiderBook.cs:pairs` | OutsiderBook.MaxPairs pairs, the longest quiet go first; the gone dropped after 30 days | fixed | 60 | 60 | 60 | 19.3 |
| `Standing.cs:log` | 8 lines per colonist | colonists | 160 | 160 | 160 | 18.8 |
| `Colonies/TownLayout.cs:table` | per town the colony was: its kept layout, the grids deflated and the def names (64 KB at most a town); none for a town handed over before release 2b | cap | 0 | 1 | 2 | 18.5 |
| `Arcs/ArcTracker.cs:past` | ArcTracker.MaxPast | cap | 18 | 36 | 90 | 17.6 |
| `Camps/Camp.cs:plans` | what the camp builds (a bed each, the hut or tents, a windbreak, tables and seats, lights, its spots, the picket, the stash: CampBuildMath), while the camp stays | while | 90 | 90 | 90 | 17.6 |
| `Pressures/Pressures.cs:people` | one per colonist, the gone dropped daily | colonists | 20 | 20 | 20 | 17.6 |
| `Setbacks/SetbackTracker.cs:last` | 20 days | window | 200 | 200 | 200 | 17.6 |
| `Offices/Election.cs:canvassed` | three visits a day for three days a candidate, while it lasts | while | 180 | 180 | 180 | 15.8 |
| `Offices/Election.cs:heard` | who heard each candidate at the hustings, while it lasts | while | 400 | 400 | 400 | 15.6 |
| `Hud/HudComponent.cs:stock` | the inventory panel's stock: one packed line (about 120 items, and the weapon and apparel kinds in storage) every six hours per home map, the last five; only while that panel is on | cap | 5 | 5 | 5 | 14.6 |
| `Social/Groups/GroupTracker.cs:groups` | groups of 3 to 9, the dead and departed dropped daily, ended below two | cap | 6 | 6 | 6 | 14.6 |
| `Social/Bonds.cs:pairs` | one short line per pair of colonists who met, pairs not both among the colony's people for 60 days dropped daily | pairs | 209 | 209 | 209 | 14.3 |
| `CouncilGameComponent.cs:history` | a sample a day, 120 days | window | 60 | 120 | 120 | 14.1 |
| `Animals/AnimalBook.cs:marks` | a temperament for each named or bonded animal, AnimalNatureMath.MaxMarks at most, let go 120 days after last seen | cap | 30 | 60 | 150 | 13.2 |
| `Gods/GodBook.cs:gods` | the pantheon: one record a god, built once per world, a few more as peoples arise and newborns come | cap | 3 | 6 | 15 | 13.2 |
| `World/Ledger/LedgerRegion.cs:houses` | two founding families per new town in detail (RegionMath.HousesPerTown), going with the town's record (the colony's own houses of release 2d.1 went on 5 November 2026: an older save's are dropped as it loads) | cap | 12 | 24 | 60 | 12.9 |
| `Building/ColonyUse.cs:keys` | with the above: what was counted, where and what | window | 280 | 280 | 280 | 12.3 |
| `Annals.cs:written` | Caps.AnnalsYears written years | cap | 1 | 2 | 5 | 12.2 |
| `Building/Suggest/SameRules.cs:marked` | per step: the things it gave an order on (taken down, trees marked, a firebreak's brush) | fixed | 300 | 300 | 300 | 11.7 |
| `CouncilGameComponent.cs:wishes` | open wishes only (one per colonist at most) | cap | 20 | 20 | 20 | 11.7 |
| `Letters/LetterBook.cs:used` | the long pieces written into letters, by their first words: LetterKindMath.MaxUsed at most, the oldest let go | cap | 30 | 60 | 150 | 11.7 |
| `Peoples/PeopleAges.cs:ages` | one record a people of Rimfolk's (the fifteen at once, and each that arises), PeopleAgesMath.MaxRecords kept, gone peoples dropped | cap | 40 | 40 | 40 | 11.7 |
| `Setbacks/SetbackTracker.cs:built` | what colonists built, while it stands and is recent | window | 60 | 60 | 60 | 11.7 |
| `Social/Moments.cs:pairs` | pair tones forgotten after 4 quiet days or when one is gone | pairs | 95 | 95 | 95 | 11.1 |
| `Ailments/AilmentTracker.cs:had` | known pawns only | known | 165 | 180 | 225 | 11.0 |
| `Offices/Election.cs:ballots` | one ranked ballot a voter a seat, while it lasts | while | 280 | 280 | 280 | 10.9 |
| `Post/PostBook.cs:incoming` | answers and the world's letters waiting for a carrier: PostMath.MaxIncoming at most (the oldest goes by messenger), each gone once it opens | cap | 16 | 16 | 16 | 10.9 |
| `Offices/Election.cs:entries` | two or three candidates a seat, with their platform, while it lasts | while | 42 | 42 | 42 | 10.7 |
| `Couriers/CourierTracker.cs:trips` | trips waiting or under way: CourierMath.MaxTrips; ended ones dropped within two days | cap | 12 | 12 | 12 | 10.5 |
| `Happenings/Happenings.cs:memos` | memos 3 years (children's 10), used ones a day | window | 18 | 36 | 54 | 10.5 |
| `Standing.cs:asked` | per colonist, one per wish def asked for | colonists | 300 | 300 | 300 | 10.3 |
| `Camps/CampTracker.cs:camps` | per colony: the camps visiting, one at the usual level, four at most on a huge map at "How many camps": Crowded (CampCrowdMath.MostAtOnce); each one's rows (members, plans...) are counted once above, a second to fourth adds as much again | while | 4 | 4 | 4 | 10.2 |
| `CouncilGameComponent.cs:nextSteps` | the colony's next steps: the plan, at most 8 proposals, done ones for 5 days, let-go ones for 15 (Building/NextSteps.cs) | fixed | 40 | 40 | 40 | 10.2 |
| `Epithets/Epithets.cs:holdsSaved` | how each held name holds (NameMath.Pack: since, a challenger and their season, the last holder), one per name held | defs | 74 | 74 | 74 | 10.1 |
| `UI/EventCards/EventCards.cs:plain` | Rimfolk's plain letters by their ID, so they open as event cards after a load: those still on the stack or in the History tab, the newest 400 (EventCardMath.MaxPlain) | cap | 400 | 400 | 400 | 9.8 |
| `Colonies/LandingRecord.cs:kit` | the starting kit's stacks (a reference, the def and stuff, how many are the kit's, where it came from), a few dozen in a crashlanding; split loads merge back on the stockpile and used-up stacks are let go; only for the first 3 days | while | 60 | 60 | 60 | 9.4 |
| `World/Notables.cs:warned` | threats each people was warned of (World/Warnings.cs): WarnMath.MaxKept, 30 days or while the threat stands | fixed | 60 | 60 | 60 | 9.4 |
| `Building/Suggest/SameRules.cs:markedDefs` | with the above: which order | fixed | 300 | 300 | 300 | 8.8 |
| `World/Notables.cs:letters` | letters and calls under way, PostMath.MaxOpen at once with the post (NotableMath.MaxOpen without); ended ones kept two days; the bringing flag while the writer carries one to the mailbag, cleared once in; the head a town's matter is passed on to | cap | 15 | 15 | 15 | 8.6 |
| `Outsiders/OutsiderBook.cs:heard` | OutsiderTalkMath.HeardMax news heard, all towns, 60 days at most | fixed | 40 | 40 | 40 | 8.6 |
| `Quirks/Quirks.cs:quirks` | 2 per colonist, the dead and discarded dropped | colonists | 40 | 40 | 40 | 7.8 |
| `Secrets/Secrets.cs:secrets` | one per colonist, ended with their death | colonists | 20 | 20 | 20 | 7.8 |
| `Setbacks/SetbackTracker.cs:sown` | fields sown, until harvested or lost | window | 40 | 40 | 40 | 7.8 |
| `Social/Groups/GroupTracker.cs:work` | one work log per colonist, 6 kinds each | colonists | 20 | 20 | 20 | 7.8 |
| `Social/Talk/TalkTracker.cs:notes` | the talk's news: TalkTracker.MaxNotes, a month at most | cap | 40 | 40 | 40 | 7.8 |
| `UI/News.cs:items` | per news letter; the game's archive keeps 200 letters | cap | 40 | 40 | 40 | 7.8 |
| `UI/News.cs:waiting` | the evening's news, 40 at most, sent each evening | cap | 40 | 40 | 40 | 7.8 |
| `Gods/GodBook.cs:vows` | colonists' vows to a god, the open ones and the newest done, GodBook.MaxVows | cap | 6 | 12 | 30 | 7.6 |
| `Journal.cs:griefSeen` | cleared at 500 | cap | 30 | 60 | 150 | 7.3 |
| `World/WorldSituations.cs:history` | 40 past | cap | 6 | 12 | 30 | 7.3 |
| `Building/Suggest/Suggestions.cs:declinedTitles` | with the above: the words they had | fixed | 80 | 80 | 80 | 7.0 |
| `World/WorldSituations.cs:lastEnded` | one per situation def | defs | 78 | 78 | 78 | 6.9 |
| `Building/ColonyUse.cs:amounts` | with the above: how much | window | 280 | 280 | 280 | 6.8 |
| `Building/ColonyUse.cs:days` | the record of use: one entry a day per thing used, bench product and bench hand, today and UseLog.WindowDays before it | window | 280 | 280 | 280 | 6.8 |
| `Dreams/Dreams.cs:seen` | dream counts of known pawns | known | 55 | 60 | 75 | 6.6 |
| `Beasts/BeastBook.cs:alphas` | packs led by an alpha: one a home a month at most (the setting at 200%: two), BeastsMath.MaxAlphaRecords kept | cap | 4 | 7 | 16 | 6.6 |
| `Epithets/Epithets.cs:holders` | one per title | defs | 74 | 74 | 74 | 6.5 |
| `Dilemmas/Dilemmas.cs:traditionCount` | per tradition, an older save's until its customs go on the calendar (the first hour) | defs | 73 | 73 | 73 | 6.4 |
| `Dilemmas/AnswerShifts.cs:shifts` | a colonist's hours or work changed by an answer, a night to a heat wave long, at most AnswerShifts.MaxShifts at once | cap | 18 | 36 | 40 | 6.2 |
| `World/Notables.cs:places` | places heard of in talk or a letter's news, marked on the maps: HeardPlaceMath.Max, 60 days (HeardPlaceMath.KeepDays) | fixed | 24 | 24 | 24 | 6.1 |
| `Health/HealthTracker.cs:healers` | known pawns only | known | 22 | 24 | 30 | 5.9 |
| `Missions/StoriesComponent.cs:chains` | chains 120 days after they end | window | 6 | 12 | 12 | 5.9 |
| `Outsiders/OutsiderBook.cs:visits` | OutsiderBook.MaxVisits outsiders met lately, let go 3 days after they leave | fixed | 30 | 30 | 30 | 5.9 |
| `Secrets/Vendettas.cs:vendettas` | 40 settled, avengers dead or discarded dropped | cap | 3 | 6 | 15 | 5.9 |
| `Standing.cs:standings` | one per colonist, the gone dropped daily | colonists | 20 | 20 | 20 | 5.9 |
| `World/FactionMemory.cs:notes` | who tended or freed whom: 30 days, 60 at most, dropped when the stranger dies | window | 30 | 30 | 30 | 5.9 |
| `Ambitions/Ambitions.cs:lastOffered` | one per ambition def | defs | 66 | 66 | 66 | 5.8 |
| `Camps/CampGround.cs:held` | who held each old site, the newest CampGroundMath.MostHolders of each of six sites | fixed | 36 | 36 | 36 | 5.6 |
| `Visitors/VisitorTracker.cs:trips` | visits waiting or under way: VisitorMath.MaxTrips; ended ones dropped within a day | cap | 8 | 8 | 8 | 5.5 |
| `CouncilGameComponent.cs:cooldowns` | dropped once over | window | 60 | 60 | 60 | 5.3 |
| `Apparel/ApparelPickBook.cs:asks` | each colonist's last ask for better clothes (one each), those of the gone kept fifteen days | colonists | 20 | 20 | 20 | 5.1 |
| `Building/Suggest/Suggestions.cs:done` | the steps done lately, SuggestMath.DoneKept at most, each with its undo and check | fixed | 20 | 20 | 20 | 5.1 |
| `Hauling/HaulBook.cs:loads` | one per pawn carrying something for Rimfolk, dropped once it carries nothing | colonists | 20 | 20 | 20 | 5.1 |
| `Building/BuildProjects.cs:projects` | rooms placed and followed in stages, BuildAheadTracker.MaxProjects at most; a finished one kept while its walls stand | fixed | 16 | 16 | 16 | 5.0 |
| `Offices/Election.cs:pollDays` | the poll a day for six days a seat, while it lasts | while | 84 | 84 | 84 | 4.9 |
| `Arcs/ArcTracker.cs:active` | stories under way | colonists | 10 | 10 | 10 | 4.9 |
| `Weapons/WeaponPickBook.cs:asks` | each colonist's last ask for a better weapon or trade proposed (one each), those of the gone kept fifteen days | colonists | 20 | 20 | 20 | 4.9 |
| `Building/BuildAhead.cs:pieceDefs` | with the above: what each blueprint was | fixed | 120 | 120 | 120 | 4.7 |
| `Outsiders/OutsiderBook.cs:wars` | OutsiderBook.MaxWars wars between other peoples, the ended let go after 60 days | fixed | 30 | 30 | 30 | 4.7 |
| `Peoples/ArisingBook.cs:arisen` | peoples that arose during play: one record each, one every 20 days at the most (the setting at 200%), ArisingMath.MaxRecords kept | cap | 3 | 6 | 15 | 4.7 |
| `PlayOut/PlayOutTracker.cs:plays` | waiting ones capped, done ones dropped | cap | 8 | 8 | 8 | 4.7 |
| `Social/Groups/GroupTracker.cs:past` | 12 past groups, members the game still knows | cap | 3 | 6 | 12 | 4.7 |
| `CouncilGameComponent.cs:everWished` | one per wish def | defs | 129 | 129 | 129 | 4.4 |
| `Building/CouncilPlanQueue.cs:queue` | blueprints waiting to be placed | while | 30 | 30 | 30 | 4.4 |
| `Character/Character.cs:recent` | 30 recent shifts | cap | 30 | 30 | 30 | 4.4 |
| `Social/Banishment.cs:records` | the people the council was asked about for trouble, and those it sent away, the oldest settled first past BanishTracker.MaxRecords | cap | 3 | 6 | 15 | 4.4 |
| `Strangers/Nemeses.cs:all` | 30 that have ended | cap | 3 | 6 | 15 | 4.4 |
| `Persona/Marks.cs:fights` | the last 8 fights won per person, a year at most | colonists | 160 | 160 | 160 | 3.9 |
| `Animals/AnimalBook.cs:gone` | animals that left alive with their handler, AnimalNatureMath.MaxGone at most, oldest first, forgotten after 120 days | cap | 3 | 6 | 15 | 3.8 |
| `Gods/OnMap/GodsOnMapBook.cs:marks` | the marks the gods left, GodsOnMapMath.MaxMarks, let go after five years | cap | 3 | 6 | 15 | 3.8 |
| `Dealings/DealingBook.cs:friends` | one per notable the colony exchanged letters with, a friend or not yet; the gone let go daily (DealingMath.MaxFriends friends) | cap | 6 | 12 | 24 | 3.8 |
| `Building/BuildPriority.cs:levels` | blueprints and frames with a build priority set, per map, until built or cancelled | while | 40 | 40 | 40 | 3.5 |
| `Building/RoomRearranger.cs:orders` | per map: a room's furniture moves still to be carried out, gone once done, called off or after 15 days (Building/RoomRearranger.cs) | fixed | 30 | 30 | 30 | 3.5 |
| `Building/Suggest/Suggestions.cs:declinedKeys` | suggestions set aside with Not this, SuggestMath.DeclinedKept at most; those for some days let go when over | fixed | 80 | 80 | 80 | 3.5 |
| `Diplomacy/DiplomacyBook.cs:wards` | per home: wards and hostages either way, WardMath.MaxPerHome at most, each dropped once they are home again | cap | 4 | 4 | 4 | 3.5 |
| `Integrations/SocialInteractions.cs:bestFriends` | Caps.BestFriendPairs, known pawns | cap | 12 | 24 | 60 | 3.5 |
| `Peoples/Events/PeopleEventsBook.cs:trusts` | goods held in trust: one open with the league at a time, the done ones kept to PeopleEventsMath.MaxTrusts | cap | 1 | 2 | 4 | 3.5 |
| `PlayOut/OnMapTimed.cs:kept` | kept a night to a few days, at most OnMapTimed.MaxKept at once | cap | 12 | 12 | 12 | 3.5 |
| `PlayOut/PlayOutTracker.cs:results` | how the last ones went: PlayOutTracker.MaxResults | cap | 30 | 30 | 30 | 3.5 |
| `CouncilGameComponent.cs:movingOnHeld` | those kept from moving on, one per person and reason, until it resolves or after a season (Social/MovingOnHeld.cs) | fixed | 8 | 8 | 8 | 3.3 |
| `Hud/HudComponent.cs:samples` | the mood trend: one packed number an hour, the last week (HudComponent.MaxSamples) | cap | 168 | 168 | 168 | 3.3 |
| `Decrees/Decrees.cs:readyAt` | one per decree def | defs | 36 | 36 | 36 | 3.2 |
| `Faith/FaithCouncilTracker.cs:heard` | who heard which faith spoken for lately: one per colonist, 30 days, FaithCouncilTracker.MaxHeard | cap | 36 | 40 | 40 | 3.1 |
| `Faith/FaithPeople.cs:people` | one per colonist whose faith moved or is watched; known pawns only, empty records dropped daily and at saving | colonists | 20 | 20 | 20 | 3.1 |
| `Integrations/GuestHelp.cs:guests` | one record per Hospitality guest staying (GuestHelpMath.MaxGuests at most), let go when they leave | while | 10 | 10 | 10 | 3.1 |
| `Journeys/Journeys.cs:roadFood` | travellers eating by the road's food policy, with their own to go back to at home (a pawn and two policy references) | fixed | 20 | 20 | 20 | 3.1 |
| `Building/BuildProjects.cs:levels` | with the above: the priorities they had | fixed | 120 | 120 | 120 | 2.9 |
| `Decrees/Decrees.cs:nightHours` | a curfew's night hours, per colonist while it lasts; after it only those away (the dead, the gone and the unknown let go) | colonists | 20 | 20 | 20 | 2.9 |
| `Gods/GodLore.cs:names` | the names the colony has heard the gods by, each a page of the book | cap | 3 | 6 | 15 | 2.9 |
| `Journeys/Journeys.cs:done` | the last hours' steps each traveller has done (six steps) | fixed | 120 | 120 | 120 | 2.9 |
| `Post/PostBook.cs:rides` | the colony's own riders out with a letter, until they're home: one a letter, a few at most | cap | 5 | 5 | 5 | 2.9 |
| `Social/SocialWeb.cs:lost` | 40 lost bonds | cap | 3 | 6 | 15 | 2.9 |
| `Wishes/Kept.cs:watches` | granted wishes still watched (grace days) | window | 8 | 8 | 8 | 2.9 |
| `World/WorldSituations.cs:active` | 2 running | fixed | 2 | 2 | 2 | 2.9 |
| `Animals/AnimalPowers.cs:familiars` | a mage's familiar: one a mage, AnimalPowersMath.MaxFamiliars kept | cap | 6 | 12 | 12 | 2.8 |
| `Faith/FaithPeople.cs:waiting` | crises waiting to be felt: one per kind per colonist, let go after 6 days | cap | 9 | 18 | 20 | 2.7 |
| `Lean/Homes.cs:ties` | one per colonist who has been on a home: the last home they lived on; the dead, discarded and those no longer the colony's dropped hourly | colonists | 20 | 20 | 20 | 2.7 |
| `Offices/Election.cs:races` | an election's seats, while it lasts | while | 14 | 14 | 14 | 2.7 |
| `Quirks/Quirks.cs:practice` | one per colonist who works or keeps late or early hours; dead and departed dropped daily and at saving, faded-out ones removed | colonists | 20 | 20 | 20 | 2.7 |
| `World/Notables.cs:known` | threats heard of in talk with outsiders: WarnMath.MaxKnown, 30 days | fixed | 40 | 40 | 40 | 2.7 |
| `Ailments/AilmentTracker.cs:healing` | injuries an ailment may follow; let go when healed, known pawns only | known | 16 | 18 | 22 | 2.6 |
| `Aspirations/Aspirations.cs:events` | sparks, 30 days | window | 30 | 30 | 30 | 2.6 |
| `Health/HealthTracker.cs:lastDoctor` | tends 3 days | window | 30 | 30 | 30 | 2.6 |
| `Health/HealthTracker.cs:lastTend` | tends 3 days | window | 30 | 30 | 30 | 2.6 |
| `Health/HealthTracker.cs:visits` | 30 days | window | 30 | 30 | 30 | 2.6 |
| `Health/MedicineChoice.cs:filled` | with Smart Medicine: the minor and harmless conditions still to be tended, pruned to the living at each save | fixed | 30 | 30 | 30 | 2.6 |
| `Pressures/Pressures.cs:firstSeen` | plans while they stand | while | 30 | 30 | 30 | 2.6 |
| `Pressures/Pressures.cs:lastProgress` | frames while they stand | while | 30 | 30 | 30 | 2.6 |
| `Pressures/Pressures.cs:lastWork` | frames while they stand | while | 30 | 30 | 30 | 2.6 |
| `Secrets/Secrets.cs:rolled` | known pawns only | known | 55 | 60 | 75 | 2.6 |
| `Journeys/Journeys.cs:packing` | the packing list: food, medicine, beds, tents, feed, what the player added from the stores and the suggestions they took off | fixed | 20 | 20 | 20 | 2.5 |
| `Building/Automations.cs:log` | the automations' log, AutomationMath.LogLines lines | fixed | 20 | 20 | 20 | 2.3 |
| `Calendar/ColonyCalendar.cs:restHours` | days taken off by hand: only the player's clicks | colonists | 20 | 20 | 20 | 2.3 |
| `Defense/DefenseLinesWatch.cs:running` | one per line while a raid lasts | while | 4 | 4 | 4 | 2.3 |
| `Gods/GodBook.cs:ties` | the ties between gods: GodTiesMath.MaxTies a god at most, two gods to a tie | cap | 3 | 6 | 15 | 2.3 |
| `Gods/GodLore.cs:signs` | the signs the colony has learned as a god's own, a few a god | fixed | 60 | 60 | 60 | 2.3 |
| `Gods/GodLore.cs:told` | how much of each old story the colony has heard, one a story | fixed | 60 | 60 | 60 | 2.3 |
| `Happenings/Happenings.cs:departed` | the departed 2 years | window | 3 | 6 | 6 | 2.3 |
| `Happenings/VisitorServices.cs:tasks` | a night to a day each, at most VisitorServices.MaxTasks at once | cap | 8 | 8 | 8 | 2.3 |
| `Offices/Election.cs:campaigners` | one per candidate, while it lasts | while | 20 | 20 | 20 | 2.3 |
| `Persona/Marks.cs:watches` | one per person watched; known pawns only, empty ones dropped | colonists | 20 | 20 | 20 | 2.3 |
| `Visitors/Callers.cs:tasks` | half a day to a day each, at most Callers.MaxTasks at once | cap | 6 | 8 | 8 | 2.3 |
| `Diplomacy/DiplomacyBook.cs:homes` | one per colony (one home in the model): their envoy's stay and the colony's envoy away (one each at most), when an envoy was last offered, the feast with guests under way and the last one; a lost home's kept 30 days | fixed | 1 | 1 | 1 | 2.3 |
| `Quirks/Quirks.cs:luckyPast` | LuckyItems.MaxPast | cap | 3 | 6 | 15 | 2.2 |
| `Dilemmas/Dilemmas.cs:traditionBreaks` | per tradition | defs | 24 | 24 | 24 | 2.1 |
| `Weather/WeatherBook.cs:days` | WeatherMath.MaxDays: the last month, a short line a day, per home map | fixed | 30 | 30 | 30 | 2.1 |
| `Dealings/DealingBook.cs:asks` | the world's letters waiting for an answer (DealingMath.MaxWaiting at once) and answered ones kept a month | cap | 8 | 8 | 8 | 2.0 |
| `Weapons/WeaponPickBook.cs:raids` | raids the colony met: how many came, mechanoids, armoured, a siege, a tribe; WeaponPickBook.MaxRaids newest | cap | 12 | 12 | 12 | 2.0 |
| `Aspirations/Aspirations.cs:inspiredBy` | known pawns only | known | 16 | 18 | 22 | 2.0 |
| `Building/PersonalTouches.cs:pending` | until built or dropped | cap | 10 | 10 | 10 | 2.0 |
| `Building/Suggest/Suggestions.cs:declinedUntil` | with the above: until when | fixed | 80 | 80 | 80 | 2.0 |
| `Dreams/Dreams.cs:omens` | until they come | cap | 10 | 10 | 10 | 2.0 |
| `Happenings/Happenings.cs:raids` | until they come | cap | 3 | 5 | 5 | 2.0 |
| `Building/Automations.cs:actions` | the last actions to undo, AutomationMath.ActionsKept | fixed | 12 | 12 | 12 | 1.9 |
| `Diplomacy/DiplomacyBook.cs:offers` | per home: offers for captives either way and exchanges, RansomMath.MaxOffers at most, each dropped once done or its captive gone | cap | 6 | 6 | 6 | 1.9 |
| `Gods/OnMap/GodsOnMapBook.cs:omens` | omens waiting before a god's coming, a few days' worth, GodsOnMapBook.MaxOmens at most | fixed | 12 | 12 | 12 | 1.9 |
| `Setbacks/SetbackTracker.cs:friends` | pairs of friends today | pairs | 38 | 38 | 38 | 1.9 |
| `Ailments/AilmentTracker.cs:freshDay` | known pawns only | colonists | 20 | 20 | 20 | 1.8 |
| `Ailments/AilmentTracker.cs:seenDay` | known pawns only | colonists | 20 | 20 | 20 | 1.8 |
| `Building/ShelfReserves.cs:reserves` | shelves with reserved slots, per map, while the shelf stands | while | 30 | 30 | 30 | 1.8 |
| `Calendar/CalendarWatch.cs:huntKills` | one per colonist, those no longer ours dropped daily | colonists | 20 | 20 | 20 | 1.8 |
| `Calendar/ColonyCalendar.cs:harvested` | the year's harvest, per colonist; cleared yearly | colonists | 20 | 20 | 20 | 1.8 |
| `Camps/Camp.cs:felled` | where their work was these two days (trees felled, rock mined, plants gathered), during one stay | while | 60 | 60 | 60 | 1.8 |
| `Camps/CampTracker.cs:past` | camps the colony remembers, six at most (CampStoryMath.RememberMax), faded after some seasons | fixed | 6 | 6 | 6 | 1.8 |
| `CouncilGameComponent.cs:lastAtHome` | colonists still ours | colonists | 20 | 20 | 20 | 1.8 |
| `Gods/GodLore.cs:retold` | the stories retold whole in a neighbour's letter, one a story | fixed | 60 | 60 | 60 | 1.8 |
| `Happenings/Happenings.cs:visits` | visits a month | window | 6 | 6 | 6 | 1.8 |
| `Harm/Harm.cs:waiting` | hurts still to come (harm, part 4): a sickness caught showing in a day or two, a coughing guest while they stay, a sore loser's walk, a rematch owed within the year, a games day's contests | while | 6 | 6 | 6 | 1.8 |
| `Integrations/ModLinks.cs:lastMend` | today and yesterday only | colonists | 20 | 20 | 20 | 1.8 |
| `Integrations/ModLinks.cs:lastWater` | today and yesterday only | colonists | 20 | 20 | 20 | 1.8 |
| `Lives/Legacy.cs:heirlooms` | until the heir dies or is discarded, or the thing is lost | cap | 1 | 2 | 6 | 1.8 |
| `Quirks/Quirks.cs:battleTick` | within the longest quirk window | colonists | 20 | 20 | 20 | 1.8 |
| `Setbacks/SetbackTracker.cs:contentDays` | one per colonist, rebuilt each day from those here | colonists | 20 | 20 | 20 | 1.8 |
| `Strangers/Nuisances.cs:last` | 10 days | window | 20 | 20 | 20 | 1.8 |
| `UI/WeekMemory.cs:trustValues` | 8 days of trust each | colonists | 20 | 20 | 20 | 1.7 |
| `World/Region/RiseFallLedger.cs:ruins` | ruins of towns given up or destroyed in a fight within reach: twelve a home's reach, the oldest going first, each gone after 300 days; fights' ruins waiting, six at most | cap | 2 | 4 | 9 | 1.7 |
| `Camps/CampTracker.cs:diaryCounts` | how often each outcome came, one count per kind of outcome | fixed | 18 | 18 | 18 | 1.6 |
| `Ambitions/CouncilMeeting.cs:invites` | while a meeting is called | while | 20 | 20 | 20 | 1.6 |
| `Building/Automations.cs:billKeys` | bills the council set (bench id and recipe), while they stand on the bench | fixed | 40 | 40 | 40 | 1.6 |
| `Building/Automations.cs:droppedKeys` | bills the player removed, forgotten after a fortnight (AutomationMath.DroppedDays) | fixed | 40 | 40 | 40 | 1.6 |
| `Building/BuildAhead.cs:queueDefs` | with the above: what each was | fixed | 40 | 40 | 40 | 1.6 |
| `Calendar/CalendarWatch.cs:knownCrops` | one per crop the colony has grown (crop defs) | fixed | 40 | 40 | 40 | 1.6 |
| `Harm/Harm.cs:sick` | travellers' stays at a sick town and a sickness carried home: a caravan's people while the road home lasts (thirty days at most), then one until it shows | while | 4 | 4 | 4 | 1.6 |
| `Camps/CampTracker.cs:grounds` | per colony: old camps' sites, the newest CampGroundMath.MostSites, let go once claimed, taken down or built over | fixed | 6 | 6 | 6 | 1.5 |
| `Diplomacy/DiplomacyBook.cs:matches` | per home: matches under way, MatchMath.MaxPlans at most, each let go at the wedding or when called off | cap | 3 | 3 | 3 | 1.5 |
| `World/Ledger/LedgerRegion.cs:foundings` | towns being built within reach: RegionMath.MaxFoundings, the oldest first; dropped when built or gone | cap | 6 | 6 | 6 | 1.5 |
| `Advisor/AdvisorCore.cs:wealthHistory` | 60 days of wealth | window | 60 | 60 | 60 | 1.5 |
| `Animals/HerdStory.cs:lineGens` | their generations, beside the ids | fixed | 60 | 60 | 60 | 1.5 |
| `Animals/HerdStory.cs:lineIds` | animals of the herd leader's line (ids), the newest 60 (HerdStory.LineMax) | fixed | 60 | 60 | 60 | 1.5 |
| `Building/BuildAhead.cs:cutCells` | per room step: the plants it marked to cut, inside the footprint | fixed | 60 | 60 | 60 | 1.5 |
| `Building/BuildAhead.cs:mineCells` | per room step: the rock it marked to mine, inside the footprint | fixed | 60 | 60 | 60 | 1.5 |
| `Camps/Camp.cs:felledTicks` | with the above | while | 60 | 60 | 60 | 1.5 |
| `CouncilGameComponent.cs:expectingTold` | pregnancies told to the family, the newest 60 (Social/FamilyLife.cs) | fixed | 60 | 60 | 60 | 1.5 |
| `CouncilGameComponent.cs:movingOnAsked` | who the council was asked about moving on, and why, the newest 60 (Social/MovingOn.cs) | fixed | 60 | 60 | 60 | 1.5 |
| `Happenings/Happenings.cs:regulars` | visitors the colony keeps up with, five at most (RegularMath.KeepMax), forgotten after half a year away | fixed | 5 | 5 | 5 | 1.5 |
| `Happenings/Happenings.cs:seenQuests` | quests the game still keeps | fixed | 60 | 60 | 60 | 1.5 |
| `Health/HealthTracker.cs:cases` | cases while the patient lives | cap | 2 | 4 | 5 | 1.5 |
| `Animals/AnimalPowers.cs:omens` | the omen animals alive: one in 20 days at most, AnimalPowersMath.MaxOmenRecords kept | cap | 3 | 6 | 6 | 1.4 |
| `Beasts/BeastBook.cs:flights` | beast nemeses that fled hurt: one while it is away or back, dropped when its nemesis ends, BeastsMath.MaxFlights at most | cap | 2 | 4 | 9 | 1.4 |
| `World/Notables.cs:losses` | deaths among the neighbours the colony knows of, 30 days, 12 at most | fixed | 12 | 12 | 12 | 1.4 |
| `Beasts/BeastBook.cs:oaths` | oaths sworn against a beast that killed or maimed: a few a year, BeastsMath.MaxOaths kept, the ended going first | cap | 1 | 2 | 6 | 1.3 |
| `Diplomacy/DiplomacyBook.cs:vassals` | per home: the hamlet sworn and the towns under its protection, FealtyMath.MaxVassals at most, each dropped once released, broken or gone | cap | 1 | 2 | 3 | 1.2 |
| `World/Region/FollowUps.cs:waiting` | follow-ups waiting at a home: FollowUpsMath.MaxWaiting at most, each gone when it starts or 240 days after what it follows | cap | 1 | 1 | 3 | 1.2 |
| `Ambitions/CouncilMeeting.cs:remarks` | while a meeting is called | while | 10 | 10 | 10 | 1.2 |
| `Building/BuildAhead.cs:defs` | per step: the blueprints taken out of a room's kept side, one strip at most | fixed | 30 | 30 | 30 | 1.2 |
| `Building/BuildAhead.cs:stuffs` | with the above: their materials | fixed | 30 | 30 | 30 | 1.2 |
| `Camps/CampTracker.cs:diary` | the camps' diary, one short line a day, thirty at most (CampTracker.DiaryMax), one per colony | fixed | 30 | 30 | 30 | 1.2 |
| `Social/Gossip/ReputationTracker.cs:told` | one packed number per colonist, the gone dropped daily | colonists | 20 | 20 | 20 | 1.2 |
| `Strangers/Pets.cs:known` | animals known: the dead get their farewell, the discarded are dropped | fixed | 30 | 30 | 30 | 1.2 |
| `World/Region/RegionBook.cs:homes` | one per colony (one home in the model): when a founder last wrote to it, its raiders' camp (one at most) and the last word given to keep away, when a town in its reach was last given up, settled again or founded by Rimfolk; a lost home's kept 30 days, its camp gone with it | fixed | 1 | 1 | 1 | 1.2 |
| `Character/ColonyTraits.cs:last` | one per colony trait def | defs | 13 | 13 | 13 | 1.1 |
| `Character/ColonyTraits.cs:rates` | one per colony trait def | defs | 13 | 13 | 13 | 1.1 |
| `Gods/GodLore.cs:hands` | the gods' hands seen lately, for a neighbour's letter, eight at most | fixed | 8 | 8 | 8 | 1.1 |
| `Harm/Harm.cs:kindLast` | the shared budget's memory: each kind's last hurt, kept thirty days (a handful of the thirty-odd kinds at once; ninety-five at most, one each) | fixed | 12 | 12 | 12 | 1.1 |
| `Offices/Offices.cs:holders` | one per office | defs | 12 | 12 | 12 | 1.1 |
| `Setbacks/SetbackTracker.cs:bestAt` | one per skill | fixed | 12 | 12 | 12 | 1.1 |
| `Weather/WeatherBook.cs:quarters` | WeatherMath.MaxQuarters: three years of seasons, a short line each, per home map | fixed | 12 | 12 | 12 | 1.1 |
| `Arcs/ArcTracker.cs:eulogies` | until given | cap | 3 | 5 | 5 | 1.0 |
| `Building/BuildAhead.cs:queueCells` | per room step: the pieces it queued until the ground is clear | fixed | 40 | 40 | 40 | 1.0 |
| `Camps/Camp.cs:owned` | the camp's goods, pruned of the gone at saving | while | 40 | 40 | 40 | 1.0 |
| `CouncilGameComponent.cs:guardianAsked` | children the council was asked about, who raises them, the newest 40 (Social/FamilyGuardians.cs) | fixed | 40 | 40 | 40 | 1.0 |
| `CouncilGameComponent.cs:recentWishDefs` | recent wishes, 20 days | window | 40 | 40 | 40 | 1.0 |
| `CouncilGameComponent.cs:recentWishTicks` | with the above | window | 40 | 40 | 40 | 1.0 |
| `CouncilGameComponent.cs:sharedNameAsked` | couples the council was asked about, one name or two, the newest 40 (Social/FamilyNames.cs) | fixed | 40 | 40 | 40 | 1.0 |
| `CouncilGameComponent.cs:weddingAsked` | couples asked about their wedding, the newest 40 (Social/WeddingPlans.cs) | fixed | 40 | 40 | 40 | 1.0 |
| `Secrets/Vendettas.cs:eulogies` | until given | cap | 3 | 5 | 5 | 1.0 |
| `Dilemmas/Dilemmas.cs:traditions` | per tradition | defs | 24 | 24 | 24 | 0.9 |
| `Animals/AnimalPowers.cs:bindings` | bindings waiting for a quiet hour, AnimalPowersMath.MaxBindings at most, each dropped after 10 days | cap | 6 | 6 | 6 | 0.9 |
| `Camps/CampMemory.cs:people` | the people kept of each remembered camp, four at most (CampStoryMath.KeepPeople) per camp | fixed | 24 | 24 | 24 | 0.9 |
| `Heart/HeartData.cs:offHours` | per heart: the hours a day off changed, only while it runs | fixed | 24 | 24 | 24 | 0.9 |
| `Ailments/AilmentTracker.cs:doctors` | only hediffs colonists still have | colonists | 10 | 10 | 10 | 0.9 |
| `Ailments/AilmentTracker.cs:sweets` | faded below 0.05 dropped | colonists | 10 | 10 | 10 | 0.9 |
| `Building/Suggest/SameRules.cs:markedCellDefs` | with the above: which order | fixed | 30 | 30 | 30 | 0.9 |
| `Calendar/CalendarWatch.cs:awaySince` | the colony's people away now; the dead and those no longer ours dropped | colonists | 10 | 10 | 10 | 0.9 |
| `Camps/CampCarry.cs:carries` | what they are bringing over by hand (herbs, milk, a gift, goods bought), one per bearer, gone once set down | while | 3 | 3 | 3 | 0.9 |
| `Harm/Harm.cs:lastHurt` | the shared cap of hurts from everyday life: the last one's tick per colonist hurt in the last three days, pruned past them daily and at saving | colonists | 10 | 10 | 10 | 0.9 |
| `Persona/Marks.cs:once` | skills mastered; known pawns only, 800 at most | known | 16 | 18 | 22 | 0.9 |
| `Quirks/Quirks.cs:downedTick` | within the longest quirk window | colonists | 10 | 10 | 10 | 0.9 |
| `Pressures/Pressures.cs:apartSince` | pairs apart now | pairs | 10 | 10 | 10 | 0.8 |
| `Ambitions/Ambitions.cs:voterPawns` | while the letter waits | while | 20 | 20 | 20 | 0.8 |
| `Ambitions/CouncilMeeting.cs:attendees` | while a meeting is called | while | 20 | 20 | 20 | 0.8 |
| `Ambitions/CouncilMeeting.cs:voters` | while a meeting is called | while | 20 | 20 | 20 | 0.8 |
| `Annals.cs:present` | the year in progress | colonists | 20 | 20 | 20 | 0.8 |
| `Camps/Camp.cs:kills` | while the camp stays (a stay is days, not years) | while | 20 | 20 | 20 | 0.8 |
| `Gods/OnMap/GodStranger.cs:kind` | each colonist's kindness to the stranger, while they stay (a day or two, a few times in a long game) | while | 20 | 20 | 20 | 0.8 |
| `Gods/OnMap/GodStranger.cs:talkers` | who talked with the stranger, while they stay | while | 20 | 20 | 20 | 0.8 |
| `Health/HealthTracker.cs:known` | known pawns only | colonists | 20 | 20 | 20 | 0.8 |
| `Journeys/Journeys.cs:travellers` | the one plan's travellers, until they set off | fixed | 20 | 20 | 20 | 0.8 |
| `Offices/Election.cs:voted` | who has voted, while it lasts | while | 20 | 20 | 20 | 0.8 |
| `PlayOut/PlayOutLord.cs:fed` | while the gathering lasts | while | 20 | 20 | 20 | 0.8 |
| `PlayOut/PlayOutLord.cs:served` | while the gathering lasts | while | 20 | 20 | 20 | 0.8 |
| `Post/PostBook.cs:packs` | the supplies packed for each letter going with the colony's own rider, from the send until they're home or it goes back in the bag: one a ride | cap | 5 | 5 | 5 | 0.8 |
| `Social/Autonomy.cs:pawns` | who acted when: the last 2 days | window | 20 | 20 | 20 | 0.8 |
| `Social/Battle.cs:fighters` | while a fight lasts | while | 20 | 20 | 20 | 0.8 |
| `UI/WeekMemory.cs:trustKeys` | a trail per colonist, forgotten after 10 days away | colonists | 20 | 20 | 20 | 0.8 |
| `World/WorldSituation.cs:beats` | per situation | fixed | 4 | 4 | 4 | 0.8 |
| `Building/BuildAhead.cs:cells` | with the above: where they stood | fixed | 30 | 30 | 30 | 0.7 |
| `Building/BuildAhead.cs:rots` | with the above: which way they faced | fixed | 30 | 30 | 30 | 0.7 |
| `Building/Suggest/SameRules.cs:markedCells` | per step: the orders it gave on cells rather than things (rare) | fixed | 30 | 30 | 30 | 0.7 |
| `Decrees/Decrees.cs:lampsOff` | saving the power: the lamps switched off for the night (ThingIDs), a colony's lamps at most, cleared at five in the morning and when the decree ends | fixed | 30 | 30 | 30 | 0.7 |
| `Social/Groups/GroupTracker.cs:newcomers` | newcomers, 40 days at most | cap | 5 | 5 | 5 | 0.7 |
| `Social/Banishment.cs:fights` | social fights noted as they start (ids and the tick), kept 35 days, at most BanishTracker.MaxFights | window | 10 | 10 | 10 | 0.7 |
| `Annals.cs:tiesFormed` | the year in progress | yearly | 12 | 12 | 12 | 0.7 |
| `Camps/Camp.cs:warmth` | while the camp stays | while | 8 | 8 | 8 | 0.7 |
| `Colonies/HomesFounding.cs:asks` | those sent or kept against their wish who may ask once to move, for the season after a founding (FoundingMath.AskMoveToDays, 15 days) | while | 6 | 6 | 6 | 0.7 |
| `PlayOut/PlayOutLord.cs:lines` | while the gathering lasts | while | 6 | 6 | 6 | 0.7 |
| `World/Notables.cs:seen` | visitors and traders seen at the colony until they're home, 6 at most, three days | fixed | 6 | 6 | 6 | 0.7 |
| `Camps/CampTracker.cs:raids` | per colony: a band against a camp, while it is on the map (one per camp at a time, seldom more than one) | while | 1 | 1 | 1 | 0.7 |
| `Health/HealthTracker.cs:worst` | hard times not over yet (hunger, cold, untended illness); known pawns only | known | 6 | 6 | 8 | 0.7 |
| `PlayOut/Stage.cs:cups` | life goals' toasts waiting for the next gathering, five days at most each (StageMath.CupLetGo) | fixed | 3 | 3 | 3 | 0.6 |
| `Building/Suggest/SameRules.cs:pieceDefs` | with the above: what each blueprint was | fixed | 16 | 16 | 16 | 0.6 |
| `Colonies/HomesHandOver.cs:handOvers` | a hand-over under way, one at a time, let go once the town is made or it is called off (with the same act's founding inside until it sets out) | while | 1 | 1 | 1 | 0.6 |
| `Diplomacy/DiplomacyBook.cs:tales` | per home: who of the colony's won something at a neighbour's feast, FeastMath.MaxTales at most, told once home or let go after 30 days | cap | 3 | 4 | 4 | 0.6 |
| `UI/WeekMemory.cs:answers` | answers of every kind whose play-outs are still watched, each only for the days they take (Answers.MaxWatched 40 at most) | fixed | 4 | 4 | 4 | 0.6 |
| `World/Ledger/LedgerFormers.cs:formerRecords` | one per colony handed over, HandOverMath.MaxTowns at most (the oldest whose town is gone goes); none in a save that never handed one over | cap | 0 | 1 | 2 | 0.6 |
| `Ambitions/CouncilMeeting.cs:pitches` | while a meeting is called | while | 3 | 3 | 3 | 0.6 |
| `Building/Automations.cs:doneKeys` | one per kind of automated action (a dozen kinds) | fixed | 20 | 20 | 20 | 0.6 |
| `Camps/Camp.cs:asksMade` | asks made of the camp (once each, or once a day), while it stays | while | 15 | 15 | 15 | 0.6 |
| `Secrets/Vendettas.cs:known` | known pawns only | known | 11 | 12 | 15 | 0.6 |
| `Visitors/Callers.cs:commissions` | a piece the collector is owed, until collected or for 60 days at most; at most Callers.MaxCommissions | cap | 1 | 2 | 3 | 0.6 |
| `Gods/GodLore.cs:lastHand` | each god's last hand remarked on | fixed | 14 | 14 | 14 | 0.5 |
| `Apparel/ApparelPickBook.cs:notNow` | colonists whose clothes suggestions are set aside, ten days each, the ended pruned daily | colonists | 6 | 6 | 6 | 0.5 |
| `Building/ShelfReserves.cs:entries` | per shelf: the kinds it keeps slots for, with how they're kept | while | 6 | 6 | 6 | 0.5 |
| `Faith/FaithCouncilTracker.cs:faithEvenings` | each faith's last evening of faith: one per faith, gone after 60 days or with the faith | fixed | 6 | 6 | 6 | 0.5 |
| `Post/RiderPacks.cs:lines` | per pack of the rider's supplies: the lines taken (food, medicine, drugs, feed, a few added), counted under packs | fixed | 6 | 6 | 6 | 0.5 |
| `Weapons/WeaponPickBook.cs:fetched` | colonists who filled an empty weapon slot themselves within the day, pruned daily | colonists | 6 | 6 | 6 | 0.5 |
| `Weapons/WeaponPickBook.cs:notNow` | colonists whose suggestions are set aside, ten days each, the ended pruned daily | colonists | 6 | 6 | 6 | 0.5 |
| `World/Ledger/LedgerColonies.cs:colonyRecords` | one per colony ever founded once there are two, FoundingMath.MaxRecords at most (the oldest ended ones fold into the history); none in a save of one colony | cap | 1 | 1 | 3 | 0.5 |
| `Colonies/HomesFounding.cs:foundings` | a fresh start under way, one at a time, let go once its colony stands and what comes after is done (the newcomers chosen or their call let go after 10 days, the first night, the old colony abandoned) | while | 1 | 1 | 1 | 0.5 |
| `Visitors/VisitorsMore2.cs:deals` | a book swapper's bargain, while their visit lasts, two days at most (Visitors/VisitorsMore2.cs) | while | 2 | 2 | 2 | 0.5 |
| `World/Region/TradingPost.cs:posts` | per home: the trading posts standing near it, TradingPostMath.MaxPerHome at most, each dropped when it closes | cap | 1 | 2 | 2 | 0.5 |
| `Ailments/AilmentTracker.cs:announced` | only hediffs colonists still have | colonists | 20 | 20 | 20 | 0.5 |
| `Ailments/AilmentTracker.cs:woundsRolled` | only hediffs colonists still have | colonists | 20 | 20 | 20 | 0.5 |
| `Ambitions/Ambitions.cs:voterChoice` | while the letter waits | while | 20 | 20 | 20 | 0.5 |
| `Animals/ResidentBeast.cs:knownIds` | wild beasts whose story has closed (ids), the newest 20 (ResidentBeast.KnownMax) | fixed | 20 | 20 | 20 | 0.5 |
| `Building/BuildAhead.cs:removeCells` | per room step: what it marked to take down, inside the footprint | fixed | 20 | 20 | 20 | 0.5 |
| `Defense/DefenseLinesWatch.cs:downedSeen` | colonists downed in the raid under way, cleared when it ends | while | 20 | 20 | 20 | 0.5 |
| `Gods/OnMap/GodStranger.cs:points` | beside it, the points, while the stranger stays | while | 20 | 20 | 20 | 0.5 |
| `Heart/HeartData.cs:goodSpent` | per heart: one per strain source | fixed | 20 | 20 | 20 | 0.5 |
| `PlayOut/Stage.cs:held` | when each gathering began, the last ten days (StageMath.KeepDays): two a day at the very most | fixed | 20 | 20 | 20 | 0.5 |
| `Social/Autonomy.cs:lastActed` | with the above | window | 20 | 20 | 20 | 0.5 |
| `Annals.cs:decrees` | the year in progress | yearly | 12 | 12 | 12 | 0.5 |
| `Building/Automations.cs:droppedTicks` | with the above | fixed | 40 | 40 | 40 | 0.5 |
| `Building/Suggest/SameRules.cs:defs` | with the above: the kind each was set not to take | fixed | 12 | 12 | 12 | 0.5 |
| `Calendar/CalendarLived.cs:captives` | the colony's people carried off, until home or lost to us | colonists | 4 | 4 | 4 | 0.5 |
| `Camps/Camp.cs:members` | the camp's people (up to twelve), while it stays | while | 12 | 12 | 12 | 0.5 |
| `Camps/CampRequests.cs:errandLines` | what they say on the errand, while it lasts | while | 4 | 4 | 4 | 0.5 |
| `CouncilGameComponent.cs:weddingPlans` | weddings the council planned, one per engaged couple, until held or a few days past their time (Social/WeddingPlans.cs) | fixed | 3 | 3 | 3 | 0.5 |
| `Offices/Offices.cs:elected` | one per office | defs | 12 | 12 | 12 | 0.5 |
| `PlayOut/PlayOuts.cs:lines` | per play-out | fixed | 4 | 4 | 4 | 0.5 |
| `PlayOut/PlayOuts.cs:moods` | per play-out | fixed | 4 | 4 | 4 | 0.5 |
| `Ailments/AilmentTracker.cs:carrying` | illnesses had now that an ailment may follow; let go when over, known pawns only | known | 6 | 6 | 8 | 0.4 |
| `Building/LineRoutes.cs:list` | lines being laid again under the walls, one per net, kept only until their new lines stand | fixed | 3 | 3 | 3 | 0.4 |
| `Calendar/CalendarWatch.cs:pendingCrops` | new crops until their first harvest (crop defs) | fixed | 5 | 5 | 5 | 0.4 |
| `Decrees/Decrees.cs:active` | decrees in force (3 at most) | fixed | 3 | 3 | 3 | 0.4 |
| `Decrees/Decrees.cs:requests` | one per decree at most | fixed | 3 | 3 | 3 | 0.4 |
| `Scenarios/ScenarioParts.cs:days` | a scenario part's days, fixed by the scenario | fixed | 3 | 3 | 3 | 0.4 |
| `Ailments/AilmentTracker.cs:budget` | the shared illness budget: one ledger a home (its debts, the game's last disease and skip, the counts of those held back, came all the same and put off, the last record line); gone with the home or once all of it is older than a debt is kept; an older save has none and owes nothing | fixed | 1 | 1 | 1 | 0.4 |
| `CouncilGameComponent.cs:herds` | one per colony: its herd's leader and line (the line's ids have rows of their own) | fixed | 1 | 1 | 1 | 0.4 |
| `Building/Suggest/SameRules.cs:pieceCells` | per step: the blueprints an order-giving step placed (lamps, chairs, an airlock) | fixed | 16 | 16 | 16 | 0.4 |
| `Calendar/ColonyCalendar.cs:forgotten` | days put away by hand: only the player's clicks | fixed | 10 | 10 | 10 | 0.4 |
| `Camps/Camp.cs:trees` | while the camp stays | while | 10 | 10 | 10 | 0.4 |
| `Happenings/VisitorServices.cs:things` | per task: what the tinker still has to mend (30 at most, fewer as the work goes on), mending only | fixed | 10 | 10 | 10 | 0.4 |
| `Journeys/Journeys.cs:animals` | the animals they bring | fixed | 10 | 10 | 10 | 0.4 |
| `Setbacks/SetbackTracker.cs:grantWho` | 20 days | window | 10 | 10 | 10 | 0.4 |
| `Social/Autonomy.cs:plans` | plans until built | cap | 10 | 10 | 10 | 0.4 |
| `CouncilGameComponent.cs:beasts` | one per colony: the beast that stays on its land (its known ids have rows of their own) | fixed | 1 | 1 | 1 | 0.4 |
| `Animals/AnimalBook.cs:keptHome` | ids of the animals kept from hunting, AnimalNatureMath.MaxKeptHome at most, let go when one dies or leaves | cap | 3 | 6 | 15 | 0.4 |
| `Beasts/BeastBook.cs:noted` | beast nemeses already given their scars and oath (ids), BeastsMath.MaxNoted newest | cap | 3 | 6 | 15 | 0.4 |
| `Building/BuildProjects.cs:patients` | the most laid up at once, one a day, BuildAheadTracker.PatientDays at most | fixed | 15 | 15 | 15 | 0.4 |
| `Fields/FieldWeather.cs:byWeather` | per home map: the growing zones set to sow By the weather, while the zone stands | while | 15 | 15 | 15 | 0.4 |
| `Fields/FieldWeather.cs:strays` | FieldWeatherMath.StrayDays: how much colder than the land's reckoning each of the last days got, per home map | fixed | 15 | 15 | 15 | 0.4 |
| `Peoples/Events/PeopleEventsBook.cs:gifts` | a colonist's gift of a kind once, twenty days apart at the least, PeopleEventsMath.MaxGifts kept | cap | 3 | 6 | 15 | 0.4 |
| `Standing.cs:recentGrants` | per colonist, 30 days | window | 15 | 15 | 15 | 0.4 |
| `Annals.cs:goals` | the year in progress | yearly | 6 | 6 | 6 | 0.4 |
| `Annals.cs:omens` | the year in progress | yearly | 6 | 6 | 6 | 0.4 |
| `Building/Suggest/SameRules.cs:owners` | per store step: the stores whose priority or filter it changed | fixed | 12 | 12 | 12 | 0.4 |
| `Colonies/HomesMerge.cs:merges` | a colony whose home was lost while its people come to live at another, then the merge's first season (at most 24, one per home lost; let go after the old council's 30 days if nobody of theirs settles) | while | 1 | 1 | 1 | 0.4 |
| `Health/HealthTracker.cs:bedHours` | known pawns only, while laid up | colonists | 4 | 4 | 4 | 0.4 |
| `Health/HealthTracker.cs:lastTrustLine` | 20 days | window | 4 | 4 | 4 | 0.4 |
| `UI/WeekMemory.cs:following` | FollowMath.Max: what the player follows, only their clicks, as text forms ("p123", "t55") | fixed | 12 | 12 | 12 | 0.4 |
| `World/WorldSituation.cs:numbers` | per situation | fixed | 4 | 4 | 4 | 0.4 |
| `Camps/Camp.cs:willing` | while the camp stays | while | 8 | 8 | 8 | 0.3 |
| `Camps/CampRaid.cs:pawns` | per band: its fighters, eight at most (CampRaids.MakeBand), while it is on the map | while | 8 | 8 | 8 | 0.3 |
| `Camps/CampRequests.cs:errandPeople` | those of the camp on an errand into the colony, while it lasts | while | 8 | 8 | 8 | 0.3 |
| `Couriers/CourierTrip.cs:things` | particular things a trip came for (a neighbour's cargo), while it lasts | while | 8 | 8 | 8 | 0.3 |
| `PlayOut/PlayOutLord.cs:guests` | while the gathering lasts: guests from outside the colony | while | 8 | 8 | 8 | 0.3 |
| `Visitors/VisitorTrip.cs:animals` | a visit's animals (a pack beast, a family's dog, a herd), while it lasts | while | 8 | 8 | 8 | 0.3 |
| `Building/Suggest/SameRules.cs:priorities` | with the above: the priority each had | fixed | 12 | 12 | 12 | 0.3 |
| `Faith/FaithCouncilTracker.cs:plans` | the evening of faith waiting to be held: one at a time, let go when its gathering is over | while | 1 | 1 | 1 | 0.3 |
| `Happenings/Happenings.cs:group` | per raid or visit (counted with them) | fixed | 1 | 1 | 1 | 0.3 |
| `Lives/Legacy.cs:children` | known pawns only | known | 6 | 6 | 8 | 0.3 |
| `Pressures/Pressures.cs:mealTicks` | per colonist | fixed | 12 | 12 | 12 | 0.3 |
| `Camps/WarCamp.cs:pawns` | a war party's fighters, while it's on the map | while | 7 | 7 | 7 | 0.3 |
| `Character/ColonyTraits.cs:today` | today's tallies, emptied each day | fixed | 3 | 3 | 3 | 0.3 |
| `Camps/CampTracker.cs:homes` | one per colony (CampHome: its camp, holding, war party and clock; one home in the model, a second doubles the rows below), a lost home's kept 30 days, then let go | fixed | 1 | 1 | 1 | 0.3 |
| `Character/ColonyTraits.cs:known` | traits the colony grew into | defs | 6 | 6 | 6 | 0.3 |
| `Health/HealthTracker.cs:partsSeen` | missing parts colonists still have | colonists | 10 | 10 | 10 | 0.2 |
| `Setbacks/SetbackTracker.cs:grantTicks` | 20 days | window | 10 | 10 | 10 | 0.2 |
| `Setbacks/SetbackTracker.cs:grantVoiced` | 20 days | window | 10 | 10 | 10 | 0.2 |
| `Annals.cs:joined` | the year in progress | yearly | 6 | 6 | 6 | 0.2 |
| `Annals.cs:traditionsKept` | the year in progress | yearly | 6 | 6 | 6 | 0.2 |
| `Building/Automations.cs:doneTicks` | with the above | fixed | 20 | 20 | 20 | 0.2 |
| `Camps/Camp.cs:prey` | while the camp stays | while | 6 | 6 | 6 | 0.2 |
| `Camps/CampLook.cs:props` | goods the camp put out (a few stacks), pruned of the gone, while it stays | while | 6 | 6 | 6 | 0.2 |
| `Camps/CampRaid.cs:helpers` | per band: the colonists who fought beside the camp, while the band is on the map | while | 6 | 6 | 6 | 0.2 |
| `Epithets/Epithets.cs:founders` | those here in the colony's first two days (by ThingID) | fixed | 6 | 6 | 6 | 0.2 |
| `Persona/Marks.cs:addictions` | the addictions they have now | colonists | 6 | 6 | 6 | 0.2 |
| `Pressures/Pressures.cs:workKinds` | per colonist | fixed | 6 | 6 | 6 | 0.2 |
| `Visitors/VisitorTrip.cs:people` | a visit's people, while it lasts | while | 6 | 6 | 6 | 0.2 |
| `Weather/WeatherDynamics.cs:pulls` | WeatherDynamicsMath.MaxPulls: events' pulls on the weather, a few days each, per home map | fixed | 6 | 6 | 6 | 0.2 |
| `World/Region/Hamlet.cs:escorts` | per home: the colonists who walked its hamlet's settlers out, HamletMath.MaxSettlers at most | cap | 3 | 6 | 8 | 0.2 |
| `World/Region/Hamlet.cs:settlers` | per home: the settlers of its hamlet on their way, HamletMath.MaxSettlers at most, let go once it stands | cap | 3 | 6 | 8 | 0.2 |
| `Camps/CampLook.cs:herd` | the camp's animals (CampLookMath.MaxAnimals), while it stays | while | 5 | 5 | 5 | 0.2 |
| `Decrees/Decrees.cs:threatTicks` | DecreeTracker.MaxThreats: the big threats of the last half month, for how needed a decree is | fixed | 8 | 8 | 8 | 0.2 |
| `Heart/HeartData.cs:goodRecent` | per heart: one per source of the good side | fixed | 8 | 8 | 8 | 0.2 |
| `Heart/HeartData.cs:goodToday` | per heart: one per source of the good side | fixed | 8 | 8 | 8 | 0.2 |
| `Heart/HeartData.cs:recentRelief` | per heart | fixed | 8 | 8 | 8 | 0.2 |
| `Heart/HeartData.cs:today` | per heart | fixed | 8 | 8 | 8 | 0.2 |
| `Journeys/Journeys.cs:plans` | one plan per home at most, until it sets off (one home in the model) | fixed | 1 | 1 | 1 | 0.2 |
| `Missions/StoriesComponent.cs:missions` | missions 2 days after they end | window | 0 | 0 | 0 | 0.2 |
| `Social/Battle.cs:fallen` | while a fight lasts | while | 5 | 5 | 5 | 0.2 |
| `Strangers/Pets.cs:fought` | animals in the last fight | fixed | 5 | 5 | 5 | 0.2 |
| `Peoples/Events/PeopleEventsBook.cs:read` | the old places the scholars read, PeopleEventsMath.MaxRead kept | cap | 2 | 3 | 8 | 0.2 |
| `Animals/AnimalPowers.cs:calmTicks` | a Psychic type's calming: the colonist and the Pokémon, each let go two days after | window | 2 | 2 | 2 | 0.2 |
| `Annals.cs:secretsOut` | the year in progress | yearly | 3 | 3 | 3 | 0.2 |
| `Decrees/Decrees.cs:useDefs` | uses within the fatigue window | window | 4 | 4 | 4 | 0.2 |
| `Health/HealthTracker.cs:lastSetback` | 20 days | window | 2 | 2 | 2 | 0.2 |
| `Health/HealthTracker.cs:setbacks` | convalescences colonists still have | colonists | 2 | 2 | 2 | 0.2 |
| `Annals.cs:died` | the year in progress | yearly | 4 | 4 | 4 | 0.2 |
| `Beasts/BeastBook.cs:homes` | one per colony (one home in the model): its last alphas, the threat looked at, its great herd's season | fixed | 1 | 1 | 1 | 0.2 |
| `Calendar/CalendarDay.cs:knew` | per day on the calendar | fixed | 4 | 4 | 4 | 0.2 |
| `Camps/CampLook.cs:walkSeen` | colonists the farewell round looked in on (the walk's count), while the camp stays | while | 4 | 4 | 4 | 0.2 |
| `ColonyState.cs:colonies` | one council per colony (one home in the model; a second doubles the council's rows), a lost home's kept 30 days (ColonyStateMath.KeepLostDays), then let go | fixed | 1 | 1 | 1 | 0.2 |
| `Couriers/CourierTrip.cs:toUnload` | who still carries the colony's goods, while the trip lasts | while | 4 | 4 | 4 | 0.2 |
| `Gods/Presence/GodAvatar.cs:stored` | what the robe and crown covered, kept in its pack, while it walks as one | while | 4 | 4 | 4 | 0.2 |
| `Happenings/VisitorServices.cs:pawns` | per task: who is still to have a dose (one per colonist at most), doses only | fixed | 4 | 4 | 4 | 0.2 |
| `Health/HealthTracker.cs:convalescing` | known pawns only | colonists | 4 | 4 | 4 | 0.2 |
| `Heart/HeartData.cs:feelingSinceKeys` | per heart: feelings felt now | fixed | 4 | 4 | 4 | 0.2 |
| `Missions/Missions.cs:group` | per mission | fixed | 4 | 4 | 4 | 0.2 |
| `Peoples/Events/PeopleCamps.cs:travellers` | the hostel's travellers resting there now, while the camp stands | fixed | 4 | 4 | 4 | 0.2 |
| `PlayOut/PlayOutLord.cs:abstained` | while the gathering lasts | while | 4 | 4 | 4 | 0.2 |
| `PlayOut/PlayOutTracker.cs:stage` | one count per home (one in the model); homes no longer ours dropped | fixed | 1 | 1 | 1 | 0.2 |
| `Scenarios/ScenarioParts.cs:offices` | a scenario part's seats, fixed by the scenario | fixed | 4 | 4 | 4 | 0.2 |
| `Visitors/Callers.cs:pawns` | per task: who is still to have a cut (one per colonist), or the animals still to tend (CallerMath.MaxPatients at most) | fixed | 4 | 4 | 4 | 0.2 |
| `Visitors/Callers.cs:things` | per task: the art still to see (seven at most) or the dead still to lay to rest (eight graves at most) | fixed | 4 | 4 | 4 | 0.2 |
| `World/WorldSituation.cs:deeds` | per situation | fixed | 4 | 4 | 4 | 0.2 |
| `World/WorldSituation.cs:held` | per situation | fixed | 4 | 4 | 4 | 0.2 |
| `World/WorldSituation.cs:sufferers` | per situation | fixed | 4 | 4 | 4 | 0.2 |
| `Pressures/Pressures.cs:workCounts` | per colonist | fixed | 6 | 6 | 6 | 0.1 |
| `Gods/GodLore.cs:seenActing` | the gods the colony has seen act plainly, one a god at most | fixed | 14 | 14 | 14 | 0.1 |
| `Character/Character.cs:tiers` | five ways | fixed | 5 | 5 | 5 | 0.1 |
| `Character/Character.cs:values` | five ways | fixed | 5 | 5 | 5 | 0.1 |
| `Health/MedicineChoice.cs:handsOff` | with Smart Medicine: Rimfolk's choices taken back to the default by hand, only the player's clicks | fixed | 5 | 5 | 5 | 0.1 |
| `Ambitions/Ambitions.cs:options` | the meeting letter's options, while it waits | while | 3 | 3 | 3 | 0.1 |
| `Ambitions/Ambitions.cs:proposers` | while the letter waits | while | 3 | 3 | 3 | 0.1 |
| `Annals.cs:gone` | the year in progress | yearly | 3 | 3 | 3 | 0.1 |
| `Annals.cs:quit` | the year in progress | yearly | 3 | 3 | 3 | 0.1 |
| `Annals.cs:traditionsBroken` | the year in progress | yearly | 3 | 3 | 3 | 0.1 |
| `Calendar/CalendarDay.cs:things` | a camp gift's things, per day on the calendar (CalendarMath.MaxDayThings, at most 2 such days) | fixed | 3 | 3 | 3 | 0.1 |
| `Calendar/CalendarKeeping.cs:withIds` | the days a calendar day's letter keeps together with it, while the letter waits | while | 3 | 3 | 3 | 0.1 |
| `Calendar/CalendarTracker.cs:colonies` | one calendar per colony (one home in the model; a second doubles the calendar's rows), a lost home's kept 30 days, then let go | fixed | 1 | 1 | 1 | 0.1 |
| `Camps/Camp.cs:joined` | while the camp stays | while | 3 | 3 | 3 | 0.1 |
| `Camps/CampRequests.cs:stallWares` | a few stacks their trader set out by the gate for a day's stall, while it lasts | while | 3 | 3 | 3 | 0.1 |
| `Couriers/CourierTrip.cs:mounts` | the mounts a trip's people ride in on (Giddy-Up), while it lasts | while | 3 | 3 | 3 | 0.1 |
| `Couriers/CourierTrip.cs:people` | a trip's people, while it lasts | while | 3 | 3 | 3 | 0.1 |
| `Gods/GodLore.cs:lastHandAt` | each home's last god's hand remarked on | fixed | 4 | 4 | 4 | 0.1 |
| `Gods/GodLore.cs:lastTalk` | each home's last talk of the gods | fixed | 4 | 4 | 4 | 0.1 |
| `Gods/GodLore.cs:seen` | how many of the gods' doings each colonist has seen, by name | cap | 1 | 1 | 3 | 0.1 |
| `Gods/OnMap/SacredAnimal.cs:kin` | the beast's kin following at a distance, while it stays (a few times in a long game) | while | 3 | 3 | 3 | 0.1 |
| `Gods/OnMap/SacredAnimal.cs:sat` | those who sat near the great beast this visit, while it stays | while | 3 | 3 | 3 | 0.1 |
| `Happenings/Happenings.cs:things` | per departed record | fixed | 3 | 3 | 3 | 0.1 |
| `Happenings/Happenings.cs:touched` | per visit | fixed | 3 | 3 | 3 | 0.1 |
| `PlayOut/OnMapTimed.cs:things` | per thing kept: its lanterns (OnMapMath.MaxLanterns at most), the fire, the door | fixed | 3 | 3 | 3 | 0.1 |
| `PlayOut/PlayOuts.cs:risks` | per play-out: an answer's fight or walk-out held for the scene, one or two at most | fixed | 1 | 1 | 1 | 0.1 |
| `PlayOut/PlayOuts.cs:spots` | per play-out | fixed | 2 | 2 | 2 | 0.1 |
| `Post/PostBook.cs:rounds` | one regular post a home (one home in the model): when it next comes, the rider's group while here | fixed | 1 | 1 | 1 | 0.1 |
| `Scenarios/Scenarios.cs:events` | a scenario part's events | fixed | 3 | 3 | 3 | 0.1 |
| `Social/SocialWeb.cs:battles` | one fight per home while it lasts (one home in the model); dropped when over | while | 1 | 1 | 1 | 0.1 |
| `Wishes/Kept.cs:cells` | per watch | fixed | 4 | 4 | 4 | 0.1 |
| `Decrees/Decrees.cs:useTicks` | with the above | window | 4 | 4 | 4 | 0.1 |
| `Ailments/AilmentTracker.cs:bitesRolled` | only hediffs colonists still have | colonists | 4 | 4 | 4 | 0.1 |
| `Ambitions/Ambitions.cs:baselines` | per ambition's goals | fixed | 4 | 4 | 4 | 0.1 |
| `Arcs/Arc.cs:heldOr` | per story | fixed | 4 | 4 | 4 | 0.1 |
| `Heart/HeartData.cs:feelingSinceTicks` | per heart | fixed | 4 | 4 | 4 | 0.1 |
| `Heart/HeartData.cs:needLack` | per heart: the four needs | fixed | 4 | 4 | 4 | 0.1 |
| `Building/Automations.cs:cells` | per action: cells added to a zone that was there | fixed | 8 | 8 | 8 | 0.1 |
| `Annals.cs:threatSeen` | each home's last big threat counted, one per home (one in the model); homes no longer ours dropped | fixed | 1 | 1 | 1 | 0.1 |
| `Happenings/Happenings.cs:timers` | Rimfolk's own event timer, one per home (one home here); a home lost takes its timer with it | fixed | 1 | 1 | 1 | 0.1 |
| `Harm/Harm.cs:drillDay` | each home's last evening drill, one per home (one in the model); maps gone dropped | fixed | 1 | 1 | 1 | 0.1 |
| `Harm/Harm.cs:turning` | the hunts' game that can turn, one head a hunt at most, remembered six days after its marking and pruned daily | while | 1 | 1 | 1 | 0.1 |
| `Setbacks/SetbackTracker.cs:lastGathering` | one per home map | fixed | 1 | 1 | 1 | 0.1 |
| `Ambitions/Ambitions.cs:absentProposers` | while the letter waits | while | 2 | 2 | 2 | 0.1 |
| `Calendar/CalendarDay.cs:kin` | per day on the calendar | fixed | 2 | 2 | 2 | 0.1 |
| `Camps/Camp.cs:arrested` | while the camp stays | while | 2 | 2 | 2 | 0.1 |
| `Camps/CampKindsLand3.cs:caught` | the beasts beast-catchers roped this stay, two at most (CampLandMath.MaxCaught), while the camp stays | while | 2 | 2 | 2 | 0.1 |
| `Gods/Presence/GodAvatar.cs:given` | the god's robe and crown, while it walks as one | while | 2 | 2 | 2 | 0.1 |
| `Peoples/Events/PeopleCamps.cs:trail` | the lantern post's hunters out on the trail, while the camp stands | fixed | 2 | 2 | 2 | 0.1 |
| `Persona/TraitChanges.cs:once` | the changes that come once in a colony's life | fixed | 2 | 2 | 2 | 0.1 |
| `PlayOut/OnMapTimed.cs:cells` | per thing kept: where each planter sets their sapling (OnMapMath.MaxSaplings at most), plantings only | fixed | 2 | 2 | 2 | 0.1 |
| `PlayOut/PlayOuts.cs:plots` | per play-out: where a burial's graves lie (OnMapMath.MaxDead at most), burials only | fixed | 2 | 2 | 2 | 0.1 |
| `PlayOut/PlayOuts.cs:round` | per play-out: those a round goes to (OnMapMath.MaxRounds at most), rounds only | fixed | 2 | 2 | 2 | 0.1 |
| `PlayOut/PlayOuts.cs:things` | per play-out: the thing handed over, or the dead still to carry out (OnMapMath.MaxDead at most) | fixed | 2 | 2 | 2 | 0.1 |
| `Strangers/Encounters.cs:homes` | one per colony (EncounterHome: the day's count, the last, each kind's cooldown; one home in the model) | fixed | 1 | 1 | 1 | 0.1 |
| `Visitors/Callers.cs:people` | per task: the undertakers (four at most) or the wedding party (five at most) | fixed | 2 | 2 | 2 | 0.1 |
| `Visitors/Callers.cs:plots` | per task: the undertakers' row of graves (CallerMath.MaxGraves at most), graves only | fixed | 2 | 2 | 2 | 0.1 |
| `Calendar/CalendarKeeping.cs:withYears` | the year each of them comes round in, while the letter waits | while | 3 | 3 | 3 | 0.1 |
| `Camps/CampStory.cs:beats` | the beats of the stay played (ask, trouble, parting), while the camp stays | while | 3 | 3 | 3 | 0.1 |
| `Health/MedicineChoice.cs:bestByAnswer` | ids of patients the council gave the best medicine, let go once their care changes | cap | 1 | 1 | 3 | 0.1 |
| `Setbacks/SetbackTracker.cs:ticks` | per sown field | fixed | 3 | 3 | 3 | 0.1 |
| `Annals.cs:traditionsBorn` | the year in progress | yearly | 2 | 2 | 2 | 0.1 |
| `Colonies/LandingRecord.cs:conditions` | the map conditions the scenario set at the start (a cold snap, a fallout), one per def, while the offer is open | while | 2 | 2 | 2 | 0.0 |
| `UI/DilemmaLog.cs:started` | per dilemma record | fixed | 2 | 2 | 2 | 0.0 |
| `UI/WeekMemory.cs:pinnedWishes` | pins of open wishes | fixed | 2 | 2 | 2 | 0.0 |
| `World/Region/Hamlet.cs:refused` | per home: when its hamlet's asks were refused, the newest HamletMath.MaxRefused, none older than a year | cap | 3 | 4 | 4 | 0.0 |
| `PlayOut/OnMapTimed.cs:pawns` | per thing kept: those felling the trees (three at most), fellings only | fixed | 1 | 1 | 1 | 0.0 |
| `PlayOut/PlayOuts.cs:led` | per play-out: animals led out to be let go (a pet, or a few of the herd), walks and visits only | fixed | 1 | 1 | 1 | 0.0 |
| `Dilemmas/Answers.cs:started` | per answer watched | fixed | 1 | 1 | 1 | 0.0 |
| `Gods/OnMap/GodsOnMapBook.cs:weatherHomes` | one per home: when a god last answered in the weather there | fixed | 1 | 1 | 1 | 0.0 |
| `Gods/OnMap/GodsOnMapBook.cs:weatherTicks` | beside it, the tick | fixed | 1 | 1 | 1 | 0.0 |
| `Harm/Harm.cs:calm` | of the hunts' game that can turn, the ones whose turn within the shared budget didn't come up, pruned with them | while | 1 | 1 | 1 | 0.0 |
| `Lean/Homes.cs:known` | the homes at the last hourly look (their settlements' ids), one per home | fixed | 1 | 1 | 1 | 0.0 |
| `Lives/Legacy.cs:cameOfAgeTick` | 6 days | window | 0 | 0 | 0 | 0.0 |
| `Lives/Legacy.cs:cameOfAge` | 6 days | window | 0 | 0 | 0 | 0.0 |
| `Building/ShelfReserves.cs:kinds` | read from saves of 0.23.0 and 0.23.1 only, then kept as entries | while | 0 | 0 | 0 | 0.0 |
| `Character/ColonyCharacters.cs:others` | one per home beside the seat, a lost home's kept 30 days (ColonyStateMath.KeepLostDays); none with one colony, as modelled here | fixed | 0 | 0 | 0 | 0.0 |
| `CouncilGameComponent.cs:chronicle` | an older save's chronicle, read on load and taken into the history (the chronicle is its view); never saved again | fixed | 0 | 0 | 0 | 0.0 |
| `CouncilGameComponent.cs:oldGuestKin` | an older save's guest kin, read on load and moved into the world ledger (Claim ties); never saved again | fixed | 0 | 0 | 0 | 0.0 |
| `Epithets/Epithets.cs:laterFirstborn` | a colony settled later: its first child (none with one colony); a lost home's dropped | fixed | 0 | 0 | 0 | 0.0 |
| `Epithets/Epithets.cs:laterFounders` | a colony settled later: those who were its people in its first two days (none with one colony); a lost home's dropped | fixed | 0 | 0 | 0 | 0.0 |
| `Happenings/Happenings.cs:oldGrudges` | an older save's grudges, read on load and moved into the world ledger (one Grudge tie per people); never saved again | fixed | 0 | 0 | 0 | 0.0 |
| `Outsiders/OutsiderBook.cs:oldFavours` | an older save's favours, read on load and moved into the world ledger (Favour ties); never saved again | fixed | 0 | 0 | 0 | 0.0 |
| `Outsiders/OutsiderBook.cs:oldWords` | an older save's good words, read on load and moved into the world ledger (Promise ties); never saved again | fixed | 0 | 0 | 0 | 0.0 |
| `UI/WeekMemory.cs:pinnedPawns` | colonists pinned before following: read from an older save only, then followed; never written | fixed | 0 | 0 | 0 | 0.0 |
| `Ailments/IllnessBudget.cs:owed` | a home's debts: one at a time (two with a forced case on top), each paid by the game's next disease (held back three times in four, else let go as it comes) or let go after two to five years (counted under budget) | fixed | 1 | 1 | 1 | (in its record) |
| `Ailments/IllnessBudget.cs:owedFor` | beside owed: the illness and who had it (counted under budget) | fixed | 1 | 1 | 1 | (in its record) |
| `Arcs/Arc.cs:told` | per story (counted under active and past) | fixed | 6 | 6 | 6 | (in its record) |
| `Beasts/PackAlphas.cs:pack` | per alpha: its pack as it was named, those gone from the world let go when saving (counted under alphas) | fixed | 6 | 6 | 6 | (in its record) |
| `Building/Ahead.cs:cells` | per step (counted under done): the cells a field step's check looks at, AheadMath.MaxCellsAStep at most | fixed | 80 | 80 | 80 | (in its record) |
| `Building/Automations.cs:things` | per action (counted under actions): the bench, the things ordered down, the blueprints, the trees | fixed | 8 | 8 | 8 | (in its record) |
| `Building/BuildAhead.cs:pieceCells` | per room step (counted under done): where its blueprints went, one room's footprint at most | fixed | 120 | 120 | 120 | (in its record) |
| `Building/BuildProjects.cs:cells` | per step (counted under done): the roof cells a step marked, a room's inside at most | fixed | 60 | 60 | 60 | (in its record) |
| `Building/BuildProjects.cs:things` | per step (counted under done): the blueprints and frames a step allowed or set a priority on, one room's at most | fixed | 120 | 120 | 120 | (in its record) |
| `Building/CustomRooms.cs:pieces` | per saved room, in the mod's settings file | settings | 30 | 30 | 30 | (settings file) |
| `Building/LineRoutes.cs:lay` | per relay (counted under list): the new lines' cells, a net's worth at most | fixed | 120 | 120 | 120 | (in its record) |
| `Building/LineRoutes.cs:takeUp` | per relay (counted under list): the old lines' cells to take up | fixed | 80 | 80 | 80 | (in its record) |
| `Building/Suggest/SuggestUndos.cs:bills` | per step (counted under done): a bill and the ones Better Workbench Management links with it | fixed | 3 | 3 | 3 | (in its record) |
| `Building/Suggest/SuggestUndos.cs:cells` | per step (counted under done): the cells a field step added or moved, AheadMath.MaxCellsAStep at most | fixed | 80 | 80 | 80 | (in its record) |
| `Building/Suggest/SuggestUndos.cs:targets` | per step (counted under done): their old counts | fixed | 3 | 3 | 3 | (in its record) |
| `Calendar/CalendarDay.cs:fallen` | per day on the calendar (counted under days) | fixed | 2 | 2 | 2 | (in its record) |
| `Character/ColonyCharacters.cs:rest` | the same characters as they are read on load (counted under others) | fixed | 0 | 0 | 0 | (in its record) |
| `Colonies/HomesFounding.cs:keptAgainst` | per founding: those kept against their wish (counted under foundings) | while | 4 | 4 | 4 | (in its record) |
| `Colonies/HomesFounding.cs:party` | per founding: who goes from the colony (counted under foundings) | while | 8 | 8 | 8 | (in its record) |
| `Colonies/HomesFounding.cs:scenarioPeople` | per founding with a scenario's start: its people at the new colony (the newcomers and the children in their care, ScenarioStartMath.MaxPeople at most and the scenario's wards), written only from their arrival until Rimfolk's own parts are set up, an hour once they are there or a day at most (counted under foundings) | while | 6 | 6 | 6 | (in its record) |
| `Colonies/HomesFounding.cs:sentAgainst` | per founding: those sent against their wish (counted under foundings) | while | 4 | 4 | 4 | (in its record) |
| `Colonies/HomesHandOver.cs:going` | per hand-over: who goes (counted under handOvers) | while | 8 | 8 | 8 | (in its record) |
| `Colonies/HomesHandOver.cs:keptAgainst` | per hand-over: those kept against their wish (counted under handOvers) | while | 4 | 4 | 4 | (in its record) |
| `Colonies/HomesHandOver.cs:sentAgainst` | per hand-over: those sent against their wish (counted under handOvers) | while | 4 | 4 | 4 | (in its record) |
| `Colonies/HomesHandOver.cs:staying` | per hand-over: who stays (counted under handOvers) | while | 12 | 12 | 12 | (in its record) |
| `Colonies/HomesMerge.cs:newcomers` | per merge: those of them who came (counted under merges) | while | 8 | 8 | 8 | (in its record) |
| `Colonies/HomesMerge.cs:people` | per merge: the old colony's people as its home was lost (counted under merges) | while | 8 | 8 | 8 | (in its record) |
| `Colonies/TownLayout.cs:beds` | per kept layout: the beds of those who stayed (counted under formerRecords) | while | 12 | 12 | 12 | (in its record) |
| `Colonies/TownLayout.cs:sleepers` | per kept layout: who sleeps in each (counted under formerRecords) | while | 12 | 12 | 12 | (in its record) |
| `CouncilSettings.cs:barPutAway` | the mod's settings file, not the save: the bottom bar's buttons put away, one name each | settings | 40 | 40 | 40 | (settings file) |
| `CouncilSettings.cs:councilPageState` | the mod's settings file, not the save | settings | 30 | 30 | 30 | (settings file) |
| `CouncilSettings.cs:hudPanels` | the mod's settings file, not the save: one per panel | settings | 8 | 8 | 8 | (settings file) |
| `CouncilSettings.cs:planMaterials` | the mod's settings file, not the save | settings | 30 | 30 | 30 | (settings file) |
| `CouncilSettings.cs:savedRooms` | the mod's settings file, not the save: rooms the player saved | settings | 10 | 10 | 10 | (settings file) |
| `CouncilSettings.cs:windowPlaces` | the mod's settings file, not the save: one line per window of Rimfolk's that was moved or sized (UI/WindowFrame.cs) | settings | 4 | 4 | 4 | (settings file) |
| `Dealings/DealingBook.cs:letters` | per notable exchanging letters: when each passed, DealingMath.MaxExchanges at most (counted under friends) | fixed | 8 | 8 | 8 | (in its record) |
| `Defense/DefenseLine.cs:done` | per line: one per step (counted under lines) | defs | 11 | 11 | 11 | (in its record) |
| `Defense/DefenseLine.cs:downed` | per raid: 4 at most (counted under lines) | fixed | 4 | 4 | 4 | (in its record) |
| `Defense/DefenseLine.cs:lessons` | per raid: 3 at most (counted under lines) | fixed | 3 | 3 | 3 | (in its record) |
| `Defense/DefenseLine.cs:plans` | per line: DefenseLine.MaxPlans (counted under lines) | fixed | 24 | 24 | 24 | (in its record) |
| `Defense/DefenseLine.cs:raids` | per line: the last DefenseLine.MaxRaids raids (counted under lines) | fixed | 5 | 5 | 5 | (in its record) |
| `Defense/DefenseLine.cs:skipped` | per line: steps set aside by hand, one per step (counted under lines) | defs | 6 | 6 | 6 | (in its record) |
| `Defense/DefenseLine.cs:span` | per line: the cells across its gap, 29 at most (counted under lines) | fixed | 14 | 14 | 14 | (in its record) |
| `Dilemmas/Answers.cs:plays` | per answer watched (counted under answers) | fixed | 2 | 2 | 2 | (in its record) |
| `Diplomacy/FeastBook.cs:contests` | per feast: FeastMath.MaxContests at most (counted under homes) | fixed | 4 | 4 | 4 | (in its record) |
| `Diplomacy/FeastBook.cs:guests` | per home: the feast under way and the last one, FeastMath.MaxGuests guests each (counted under homes) | fixed | 6 | 6 | 6 | (in its record) |
| `Diplomacy/FeastBook.cs:results` | per feast: how each contest went, one each (counted under homes) | fixed | 4 | 4 | 4 | (in its record) |
| `Diplomacy/FeastBook.cs:retinue` | per guest: one or two who walk in with them (counted under homes) | fixed | 2 | 2 | 2 | (in its record) |
| `Faith/FaithPeople.cs:leanings` | per colonist: the ideoligions they're drawn to, the strongest 6 at most, faded ones forgotten daily (counted under people) | fixed | 6 | 6 | 6 | (in its record) |
| `Faith/FaithPeople.cs:notes` | per colonist: the newest faith moments, 120 days at most (counted under people) | fixed | 5 | 5 | 5 | (in its record) |
| `Gods/God.cs:factions` | per god: the peoples that hold it (counted under gods) | fixed | 2 | 2 | 2 | (in its record) |
| `Gods/God.cs:ideos` | per god: the faiths that hold it (counted under gods) | fixed | 1 | 1 | 1 | (in its record) |
| `Gods/God.cs:witnesses` | per act: the colonists who saw it, by name (counted under acts) | fixed | 3 | 3 | 3 | (in its record) |
| `Gods/GodLore.cs:who` | per entry: the colonists who saw it or believe it, by name (counted under entries) | fixed | 3 | 3 | 3 | (in its record) |
| `Happenings/Workers_Chains2.cs:flags` | per story chain (counted under chains) | fixed | 4 | 4 | 4 | (in its record) |
| `Hauling/HaulBook.cs:givers` | per load: the work givers of the work that made it, one or two (counted under loads) | fixed | 2 | 2 | 2 | (in its record) |
| `Hauling/HaulBook.cs:into` | per load: a kind a bill sent to a picked stockpile and a cell of it, kept only while that kind is owed (counted under loads) | fixed | 1 | 1 | 1 | (in its record) |
| `Hauling/HaulBook.cs:owed` | per load: the kinds owed to storage, a few (counted under loads) | fixed | 4 | 4 | 4 | (in its record) |
| `Hauling/HaulBook.cs:run` | per load: a material run's one or two materials (counted under loads) | fixed | 2 | 2 | 2 | (in its record) |
| `Hauling/HaulBook.cs:skills` | per load: the skills of the work that made it (counted under loads) | fixed | 2 | 2 | 2 | (in its record) |
| `Health/HealthTracker.cs:losses` | per healer: 30 days (counted under healers) | fixed | 3 | 3 | 3 | (in its record) |
| `Health/HealthTracker.cs:tends` | per case: who tended it (counted under cases) | fixed | 4 | 4 | 4 | (in its record) |
| `Heart/HeartData.cs:recentBuild` | per heart: one per strain source (counted under hearts) | fixed | 8 | 8 | 8 | (in its record) |
| `Hud/HudPanelState.cs:alertsHidden` | alerts hidden in the alerts panel, by class (settings file) | settings | 3 | 3 | 3 | (settings file) |
| `Hud/HudPanelState.cs:alertsShown` | alerts hidden by default that the player shows again (settings file) | settings | 1 | 1 | 1 | (settings file) |
| `Hud/HudPanelState.cs:folds` | the inventory's folded categories (settings file) | settings | 4 | 4 | 4 | (settings file) |
| `Hud/HudPanelState.cs:hidden` | per panel: the rows or columns switched off (settings file) | settings | 3 | 3 | 3 | (settings file) |
| `Hud/HudPanelState.cs:pins` | the inventory's pinned items (settings file) | settings | 8 | 8 | 8 | (settings file) |
| `Integrations/GuestHelp.cs:enabled` | per guest: work types turned on for a trouble or a thanks day while it lasts, GuestHelpMath.MaxEnabled at most (counted under guests) | fixed | 5 | 5 | 5 | (in its record) |
| `Integrations/GuestHelp.cs:told` | per guest: the kinds of work they said why they took up, GuestHelpMath.MaxTold at most (counted under guests) | fixed | 12 | 12 | 12 | (in its record) |
| `Journal.cs:entries` | per journal: 60 lines (counted under journals) | fixed | 60 | 60 | 60 | (in its record) |
| `Letters/LetterBook.cs:pages` | per exchange: the colony's letter and theirs, or theirs first, the reply and their thanks; each capped (counted under exchanges) | fixed | 3 | 3 | 3 | (in its record) |
| `Letters/LetterBook.cs:threads` | per correspondence: its threads, LetterThreadMath.MaxThreads at most, stale ones let go (counted under correspondences) | fixed | 8 | 8 | 8 | (in its record) |
| `Missions/Missions.cs:goals` | per mission (counted under missions) | fixed | 3 | 3 | 3 | (in its record) |
| `Outsiders/OutsiderBook.cs:asks` | per visit: the last 8 asks made of them (counted under visits) | fixed | 8 | 8 | 8 | (in its record) |
| `Outsiders/OutsiderBook.cs:last` | per pair: when they last did each of the 16 things (counted under pairs) | fixed | 16 | 16 | 16 | (in its record) |
| `Outsiders/OutsiderBook.cs:marks` | per pair: OutsiderTalkMath.MaxMarks, one per key, the oldest go (counted under pairs) | fixed | 10 | 10 | 10 | (in its record) |
| `Outsiders/OutsiderBook.cs:said` | per pair: OutsiderTalkMath.MaxSaid pieces said, newest last (counted under pairs) | fixed | 16 | 16 | 16 | (in its record) |
| `Outsiders/OutsiderBook.cs:topics` | per pair: the subjects of the last 3 talks (counted under pairs) | fixed | 3 | 3 | 3 | (in its record) |
| `Peoples/Events/PeopleEventsBook.cs:crates` | per trust: its six to ten crates while they stand (counted under trusts) | fixed | 10 | 10 | 10 | (in its record) |
| `Peoples/Events/PeopleEventsBook.cs:fates` | per trust: how each crate went that isn't there (counted under trusts) | fixed | 10 | 10 | 10 | (in its record) |
| `Peoples/Events/PeopleEventsBook.cs:ids` | per trust: its crates' ids (counted under trusts) | fixed | 10 | 10 | 10 | (in its record) |
| `Persona/PawnPersona.cs:d` | per person: one per side, fading (counted under people) | fixed | 8 | 8 | 8 | (in its record) |
| `Persona/PawnPersona.cs:l` | per person: one per side (counted under people) | fixed | 8 | 8 | 8 | (in its record) |
| `Persona/PawnPersona.cs:marks` | per person: PawnPersona.MaxMarks, each let go after its keeping (counted under people) | fixed | 12 | 12 | 12 | (in its record) |
| `Persona/PawnPersona.cs:stances` | per person: one per topic (23), and per newcomer for 60 days (counted under people) | fixed | 24 | 24 | 24 | (in its record) |
| `Persona/TraitChanges.cs:absorbed` | per change: how the trait moved each side (counted under changes) | fixed | 8 | 8 | 8 | (in its record) |
| `Persona/TraitChanges.cs:snapshots` | per colonist: 10 snapshots, every ten days (counted under watches) | fixed | 10 | 10 | 10 | (in its record) |
| `Persona/TraitChanges.cs:tallies` | per colonist: deeds counted, 240 days, 12 per kind (counted under watches) | fixed | 3 | 3 | 3 | (in its record) |
| `Persona/TraitChanges.cs:values` | per snapshot: the records a pace reads (counted under watches) | fixed | 3 | 3 | 3 | (in its record) |
| `PlayOut/PlayOuts.cs:game` | per hunt: 3 animals at most (counted under plays) | fixed | 3 | 3 | 3 | (in its record) |
| `PlayOut/PlayOuts.cs:hands` | per play-out: who went (counted under plays) | fixed | 6 | 6 | 6 | (in its record) |
| `PlayOut/PlayOuts.cs:invited` | per play-out (counted under plays) | fixed | 8 | 8 | 8 | (in its record) |
| `PlayOut/PlayOuts.cs:named` | per play-out: those the answer names (counted under plays) | fixed | 2 | 2 | 2 | (in its record) |
| `PlayOut/PlayOuts.cs:route` | per searcher: the places they look over (counted under plays) | fixed | 6 | 6 | 6 | (in its record) |
| `PlayOut/PlayOuts.cs:tasks` | per work party: its kinds of work (counted under plays) | fixed | 3 | 3 | 3 | (in its record) |
| `Pressures/Pressures.cs:meals` | per colonist: 12 meals (counted under people) | fixed | 12 | 12 | 12 | (in its record) |
| `Quirks/Practice.cs:hours` | per colonist: hours at each kind of work, in step with kinds (counted under practice) | fixed | 8 | 8 | 8 | (in its record) |
| `Quirks/Practice.cs:kinds` | per colonist: the kinds of work they've done lately, 8 at most (counted under practice) | fixed | 8 | 8 | 8 | (in its record) |
| `Secrets/Secrets.cs:knowers` | per secret (counted under secrets) | fixed | 3 | 3 | 3 | (in its record) |
| `Setbacks/SetbackTracker.cs:sowers` | per sown field (counted under sown) | fixed | 3 | 3 | 3 | (in its record) |
| `Social/Groups/SocialGroup.cs:counts` | per work log (counted under work) | fixed | 6 | 6 | 6 | (in its record) |
| `Social/Groups/SocialGroup.cs:formerNames` | per group: 10 names (counted under groups) | fixed | 10 | 10 | 10 | (in its record) |
| `Social/Groups/SocialGroup.cs:history` | per group: 14 lines (counted under groups) | fixed | 14 | 14 | 14 | (in its record) |
| `Social/Groups/SocialGroup.cs:kinds` | per work log: 6 kinds (counted under work) | fixed | 6 | 6 | 6 | (in its record) |
| `Social/Groups/SocialGroup.cs:members` | per group, and per past group by ID (counted under groups) | fixed | 6 | 6 | 6 | (in its record) |
| `Social/Groups/SocialGroup.cs:norms` | per group: 3 ways (counted under groups) | fixed | 3 | 3 | 3 | (in its record) |
| `Social/Groups/SocialGroup.cs:rivals` | per group: rivalries with groups still there (counted under groups) | fixed | 3 | 3 | 3 | (in its record) |
| `Social/Groups/SocialGroup.cs:views` | per group: 3 views (counted under groups) | fixed | 3 | 3 | 3 | (in its record) |
| `Social/SocialWeb.cs:history` | per tie (counted under ties) | fixed | 10 | 10 | 10 | (in its record) |
| `Trade/TradeRecord.cs:lines` | per good: its newest trades, TradeMarketMath.MaxLinesPerGood at most (counted under goods) | fixed | 5 | 5 | 5 | (in its record) |
| `UI/DilemmaLog.cs:plays` | per dilemma record (counted under dilemmas) | fixed | 4 | 4 | 4 | (in its record) |
| `Wishes/Kept.cs:things` | per watch (counted under watches) | fixed | 2 | 2 | 2 | (in its record) |
| `Wishes/Wish.cs:coSigners` | per open wish (counted under wishes) | fixed | 2 | 2 | 2 | (in its record) |
| `Wishes/Wish.cs:petitionMet` | per open want in a petition: those of it met before (counted under wishes) | fixed | 2 | 2 | 2 | (in its record) |
| `World/Ledger/History.cs:values` | per event: a deed's name, or an answer's six values, when it has them (counted under events) | fixed | 1 | 1 | 1 | (in its record) |
| `World/Ledger/LedgerFormers.cs:formers` | per town the colony was: the colony's former people who stayed (counted under formerRecords) | while | 12 | 12 | 12 | (in its record) |
| `World/Ledger/LedgerPersons.cs:kept` | per person: the reasons they're kept in the world, one per part of Rimfolk (counted under persons) | fixed | 2 | 2 | 2 | (in its record) |
| `World/Ledger/LedgerRegion.cs:members` | per house: its members by first name, RegionMath.MembersPerHouse at most (counted under houses) | fixed | 4 | 4 | 4 | (in its record) |
| `World/Notables.cs:done` | per notable: losses mourned, the newest 12 (counted under notables; warnings are their people's since 10 November 2026) | fixed | 12 | 12 | 12 | (in its record) |
| `World/Notables.cs:lastKeys` | per notable: one per purpose of a letter, and the last gift with an answer (counted under notables) | fixed | 7 | 7 | 7 | (in its record) |
| `World/Notables.cs:lastTicks` | per notable: one per purpose of a letter, and the last gift with an answer (counted under notables) | fixed | 7 | 7 | 7 | (in its record) |
| `World/Notables.cs:notes` | per notable: NotableMath.MaxMemory lines, the oldest go (counted under notables) | fixed | 8 | 8 | 8 | (in its record) |
| `World/Notables.cs:regards` | per notable: NotableMath.MaxRegards colonists, the faded dropped daily (counted under notables) | fixed | 8 | 8 | 8 | (in its record) |
| `World/Region/FollowUps.cs:flags` | per follow-up: the flags what it follows ended with (counted under waiting) | fixed | 8 | 8 | 8 | (in its record) |
| `World/Region/RaidersCamp.cs:fought` | the colony's people who fought on a camp's map, while it stands (counted under the home's part of the region) | while | 12 | 12 | 12 | (in its record) |
| `World/WorldSituation.cs:flags` | per situation (counted under active and history) | fixed | 4 | 4 | 4 | (in its record) |
| `World/WorldSituation.cs:followUps` | per past situation: what followed from it, once per def (counted under history) | fixed | 2 | 2 | 2 | (in its record) |

