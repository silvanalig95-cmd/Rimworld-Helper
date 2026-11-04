# Stage 1: the region comes alive (design, 4 November 2026)

The plan is ROADMAP.md, "The world and its people": stage 1 and the content pass that joins it, every item approved by
the player. It builds on the foundation (`docs/design/foundation.md`; DESIGN.md, "Homes" to "Ages"). The game's side
was read in the decompiled 1.6 code (32bitx64bit/RW-Decompiled-1.6) and the game's 1.6 data, Faction Territories' side
in its decompiled 1.6 assembly; every game name used below is in the 1.7 reference too. Times are the game's: a season
is 15 days, a year 60.

## Summary

- Towns within the region map's reach are kept in detail in the world ledger: founder, families, why they were
  founded, and a prosperity that Rimfolk's world events move and that shows in their words, caravans and asks.
  Struggling towns can be given up and leave ruins; ruins can be resettled; without another mod building towns,
  flourishing ones found new ones. Every new town near a home has faces, news and talk.
- Friends who trade with a far colony open a trading post a few days off, a real town of theirs named by their age,
  and close it if relations sour. A rich colony on bad terms with a people may find a raiders' camp nearby (the game's
  own bandit camp site) that raids at the game's raid-source rate until it is dealt with in one of five ways.
- A remote colony gets more travellers who seek it out, each with a reason the map shows, bringing news of the wider
  world. Settlers can ask to found a hamlet under the colony's protection; escorted there, it becomes a town of their
  people and in time swears fealty (a Faction Territories vassal, or a fealty tie).
- The content pass: memory in more events' texts, a people's memory of single colonists, Empire titles and Ideology
  rites in invitations, the warlord's son, and the town that grew from the settlers' camp.
- Only the game's own world objects, so a world without Rimfolk is an ordinary world; nothing per tick; nothing
  changed beyond the reach. For the player's remote colony the first things felt are more travellers, a friend's post
  and faces for the towns that come near, at a pace the land allows. Seven releases; fourteen decisions at the end.

## What the game lets a mod do (verified in the 1.6 code)

- **Add a town.** A `Settlement` of `WorldObjectDefOf.Settlement` with a faction, a tile and a name from
  `SettlementNameGenerator`, added to `Find.WorldObjects`: what the game does at world creation (`FactionGenerator`),
  what Faction Territories does when a town is finished (`Expansion.Utility.TryComplete`) and what Rimfolk's new-town,
  other-settlers and crossroads events already do. It trades at once (stock from its people's `baseTraderKinds`), can
  get caravan requests (`TradeRequestComp`), is picked by quests that want a nearby town
  (`QuestNode_GetNearbySettlement`), and can be attacked: its map is the game's faction base (34 to 38 cells,
  defenders worth 1,150 to 1,600 points, `GenStep_Settlement`). Two cautions. The game takes 10 to 30 goodwill a
  season from a people with a town within 4 tiles of a player home (`SettlementProximityGoodwillUtility`, `MaxDist`
  5), so whatever Rimfolk founds stands 6 tiles or more from every home, measured in a straight line inside tile
  searches (a walking distance there starts a flood fill inside a flood fill and hangs the game, as the new-town event
  learned). And a people is defeated only when its last town falls (`SettlementDefeatUtility`), so a post or a hamlet
  is one more town to it.
- **Take a town away.** `Destroy()` on a town with no map, no quest look-target or tag, no open caravan request, no
  caravan or pods on the way, never the Empire's, never one of a people's last two (`SituationWorker_Abandoned.Safe`,
  which every removal here reuses, raised from one to two). Faction Territories redraws its territories by itself when
  a world object comes or goes (its `WorldObjectsHolder` patches).
- **Leave a ruin.** `AbandonedSettlement`: a marker in the people's colour, no map, no timeout, blocking the tile for
  new towns (Faction Territories' builders skip it too). When a town is destroyed in a fight, the game's
  `DestroyedSettlement` goes with its map and leaves nothing.
- **Set an enemy camp.** A `Site` from `SiteMaker.MakeSite` with Core's `BanditCamp` part (built like an outpost: the
  game's fortified camp, `GenStep_Outpost`, with the people's fighters and loot), threat points from
  `StorytellerUtility.DefaultSiteThreatPointsNow`, our own name in `Site.customLabel`. Faction Territories takes a
  site with the `Outpost` part as a source of territory and one with `BanditCamp` not, so a camp claims no land. When
  the player leaves its map, the game removes such a site, cleared or not (`Site.ShouldRemoveMapNow`). Royalty's
  `RaidSource` part raids by itself at `QuestTuning.PointsToRaidSourceRaidsMTBDaysCurve` (a raid in 25 days on average
  at 400 points, 10 at 1,500, 5 at 5,000, each at 0.6 of the map's threat points), but against every player home and
  only with Royalty. Its curve is the game's own measure of how often a raiders' camp raids, and stage 1 uses it.
- **Bring a raid from a side.** A raid's `IncidentParms.spawnCenter` set beforehand is kept by the edge walk-in
  (`PawnsArrivalModeWorker_EdgeWalkIn.TryResolveRaidSpawnCenter`), so raiders come from where their camp lies
  (`WorldGrid.GetHeadingFromTo` gives the side).
- **Make a people.** `FactionGenerator.NewGeneratedFactionWithRelations` and `FactionManager.Add`, as the game does
  for Ideology's work sites (hidden and temporary there). It chooses or generates an ideoligion, makes a leader, sets
  relations with every people and places a town at random unless hidden. Only a temporary faction is ever removed
  (once no world object, no pawn on a map or on the road and no quest is left of it), and the game sends no visitors
  or caravans from a temporary one; a faction whose def is missing is dropped on load.
- **Know where people settle.** `TileFinder.IsValidTileForNewSettlement` and each biome's `settlementSelectionWeight`
  (none on ice sheets, little in deserts, most in temperate forest): the game's own sense of where towns stand, used
  to weigh every founding here.

## Shared rules

- **The game's own objects only.** Towns, posts and hamlets are `Settlement`s of the vanilla def, ruins
  `AbandonedSettlement`s, raiders' camps `Site`s with Core's `BanditCamp` part. Without Rimfolk they are an ordinary
  town, ruin and bandit camp; Faction Territories, which counts towns of the vanilla def only, treats them as any
  other. Rimfolk already makes and removes towns this way.
- **Towns in detail.** The towns within the region map's reach of a home (`regionMapRadius`, 35 tiles by default), the
  nearest 40 per home, of humanlike peoples (an insect hive is not a neighbour). Beyond, the game and other mods keep
  the world: Rimfolk keeps nothing there and changes nothing there. A town within reach of two homes has one fortune
  and a mood toward each.
- **The ledger owns it.** Town state goes on the ledger's `LedgerTown`, which today keeps a standing town only while a
  notable lives there and a gone one 120 days; it now keeps every town in detail, its ruins and posts and hamlets.
  Families go on a new `LedgerHouse` (the house ref exists, the record doesn't yet). Bonds are `Tie`s (a hamlet's
  fealty, raiders' word to stay away). What happens goes into the history in the `world:` family, within reach, a
  milestone when it touches a colony. People are kept with `WorldLedger.Keep` and reasons of their own (`founding`,
  `hamlet`). Each home's part (its remoteness, its posts, camp and hamlet, its clocks) is a `RegionHome` kept by the
  `ColonyBook` rules, so a second colony has its own and one colony plays as before.
- **Two passes.** `Pass.Region` hourly (a raiders' camp, a hidden camp, an escort under way) and `Pass.DailyRegion`
  (the reach, prosperity, the rolls that come every few days), each with its `Sched` slot and timed by Rimfolk's
  clock. Hooks (Faction Territories', the world events') act when they fire.
- **One voice.** `Tell.Minor` for the region's news, `Tell.Letter` for an opening or a closing, `Tell.Ask` after
  `Tell.CanAsk` for a question. This week lists what waits on the colony (settlers at the gate, a camp standing),
  never with a timer. Answers that carry something play out (`DilemmaEffect_CarryOut`, a runner or messenger at the
  gate); spoken lines go through `Speech.Say`.
- **Words by age.** New `Ages.Form` features: the trading post, the raiders' camp, the hamlet's protection, a ruin, a
  founder's title. A thing of theirs (a post, a camp) is named by their age, the colony's means by its own.
- **Settings.** A section "The region": towns' fortunes, new towns' faces, Rimfolk's own new towns (automatic: on only
  while no other mod builds towns), trading posts, raiders' camps (with a rate, 0 to 200%), travellers to the remote
  (a rate), hamlets. Off, a part makes nothing new and keeps its records. Links into other systems (the camps' clock,
  events' weights, caravans' rates) also sit behind "Systems talk to each other".

## 1. The new towns' faces and stories

**What the player sees.** When settlers begin a town within reach, the colony news says who leads them and where they
come from: "Settlers of the Lenlia under Tamsin Coor, from Ashford, have begun a town two days north." When it stands,
Tamsin is its head, her family and one other founding family are named in the town's panel on the region map, the
town's people speak of it in their talks ("We came out from Ashford with Tamsin last spring"), other peoples' visitors
mention it, and it shows in Heard lately. A town close to a home sends its first letter; now and then, the full
welcome comes: a runner at the gate, a gift hoped for and remembered.

**How it works.**
- Sources: Faction Territories' hooks on `TryCreateForFaction` and `TryComplete` (already writing `world:founded` and
  `world:built`), and Rimfolk's own towns: the new-town, other-settlers and crossroads events, a resettled ruin, a
  daughter town, a trading post, a hamlet (items 2, 3, 6).
- The founder: made when the founding begins within reach (Faction Territories gives ten days of building): a person
  of their people (their leaders' kind, never a second ruler for the Empire; aged 30 to 55), kept with `Keep(founder,
  KeepFor.Founding)`, the history's `did`. When the town stands they are its Head notable, and the town counts as one
  the colony has dealt with for its first year, so the notables keep it beside the nearest four. An Empire founder is
  made from one of the Empire's titled kinds, so the title the news gives is the title the game shows.
- Families: a `LedgerHouse` for the founder's line and one more founding family (the surname of the heir, envoy or
  trader), with a few named members; only the notables' four are pawns, the rest ledger persons known by name.
- From where and why: the mother town (their people's nearest older town) and a reason read from the world: a road or
  river through the tile, the coast, hills, the mother town flourishing, a town of theirs given up in the last 120
  days (its people are the settlers), the old walls of a ruin. `{WHYFOUNDED}`, `{MOTHERTOWN}`.
- Telling: one `Tell.Minor` line when begun and one when built, in place of Faction Territories' own message within
  reach (its far messages already go to the news; the near ones become Rimfolk's line, so the player gets one message,
  not two). Within about 15 tiles of a home, the founder's first letter (`Tell.Letter`), at most one a season per
  home; the full welcome at most once a year per home, when a world-event slot is free: `SituationWorker_NewTownMet`
  runs the new-town event's beats (the runner, the gift, the timber, the traders) on the town that stands, instead of
  making one.
- Facts and talk: `town:new` (its first year), `town:founder`, `town:mother`, `{FOUNDER}`; about 40 talk pieces (their
  own people, other peoples, the colony's people) and the letters' lines.

**Saved and bounds.** On `LedgerTown`: kind, founded, founder, mother, why. A founding record per site being built (6
at most, dropped when built or razed). Houses: two per town in detail, six members each. Founders made two a day at
most.

**Ages.** The founder's title by their age (a headman or chief, a reeve, a mayor, an administrator; the Empire's own
titles); the news by the colony's means (word from a traveller, a rider, the radio, a comms relay).

**Bad side.** A founding can fail. A site within reach that the colony razes (Faction Territories lets only the player
attack one) is a heavy wrong their people remember, and its founder flees or dies. A town of Rimfolk's that meets a
hard first season (prosperity under 10) is given up (item 2). Nothing here hurts a colonist.

**With and without other mods.** With Faction Territories, its builders give most new towns (each people one every 30
days somewhere in its territory, ten days to build) and Rimfolk gives those within reach their faces. Without it, or
with its building switched off (Rimfolk reads `enableSettlementConstruction`), Rimfolk's own events and foundings
(item 2) bring the new towns. Factional War founds none.

## 2. Towns that rise and fall

**What the player sees.** Each town on the region map has a fortune in words with its reason: "Struggling, since the
plague", "Thriving: the new road". A town in trouble writes for help, sends fewer caravans and more beggars and
refugees; a thriving one trades more and talks of it. Now and then a struggling town is given up: families on the road
for a few days, then a ruin on the map in its people's colour. Later, families may come back to the old walls, or
newcomers raise a town there. Without Faction Territories' builders, a flourishing town may send settlers to found a
new one nearby.

**Prosperity.**
- 0 to 100 on each town in detail. Bands: struggling under 20, poor to 40, steady to 60, thriving to 80, flourishing
  above. A town first seen starts at 50, less on poor land (its biome's settlement weight) and while its people are in
  hard times; a new town at 30.
- World events move it by a new field on `WorldSituationDef`, `prosperity`: a plague −20, a famine −25, a hard winter
  −10, locusts −15, a flood −15, a great fire −30, a war of creeds −15; a bumper year +10, a trading season +10, a new
  road +10, a fair +8, a silver rush +12. In full at the event's town, half at its people's other towns in detail,
  spread over the event's first days so the change is seen while it runs; a second field for the other people of a
  war. About 30 events get a value.
- The colony moves it: its help to the people (gifts, relief, rebuilding, the favour deeds: +3 to +10 to the town
  concerned), trade with their caravans (+1 each, +4 a season at most), a raid on the town (−30).
- Wars: from the game's relations (the wars Rimfolk reads daily), −0.3 a day for a town within 20 tiles of an enemy's
  town; a Factional War battle on a home, −6 for the loser's town nearest it and +2 for the winner's; a town taken in
  a Faction Territories invasion starts again at 25 under its new people.
- Drift back toward 50 (45 on poor land) by 0.3 a day.
- What it changes: the words (facts `town:struggling` to `town:flourishing`, `{FORTUNE}`, the region map panel with
  the last cause from the history); the asks (a struggling town's notables write for help, and the provisions,
  refugees and rebuilding missions are twice as likely for its people); caravans (their trader caravans ×1.25 from a
  thriving town, ×0.6 from a struggling one, when it is their town nearest the home, through the events' modifiers on
  the game's incidents); beggars and refugees (the existing events twice as likely from a struggling town); and what
  it sells (decision 3).

**Abandonment.** A town in detail under 15 for 30 days (or under 25 when a second bad event hits it) that passes
`Safe`, is not its people's seat nor the Empire's, and whose people keep two other towns or more: a 7% roll every 5
days. At most one per home's reach in 60 days. It runs as the town-abandoned event on that town when a world-event
slot is free (refugees at the gate, salvage, holdouts while the colony has business there), else as a quiet version:
news of families on the road, maybe a family at the gate, the ruin after five days. Its notables move to their
people's nearest town or become acquaintances.

**Ruins.** The game's `AbandonedSettlement` in the people's colour, and a town of kind Ruin on the ledger with when
and why. Where the game leaves nothing (a town within reach destroyed by the colony or in an invasion) Rimfolk puts a
ruin on the tile once the game's destroyed settlement has gone (decision 5). A ruin Rimfolk made that nobody resettles
is removed after 300 days ("in ten years a story"); twelve per home's reach at most, the oldest going first.

**Resettlement.** A ruin at least 120 days old, a people at peace with the colony with a town within 25 tiles of it at
60 or more (or the ruin's own people again at 50 or more): a 10% roll every 15 days, at most one per home's reach in
120 days. The ruin goes and a town stands on its tile under its old name (newcomers may give their own), with a
founder and families (item 1): "Families of the Lenlia have come back to the old walls of Ashford."

**Rimfolk's own new towns.** Only while no other mod builds towns (decision 2): a flourishing town (80 or more for 60
days) may send settlers to a tile 6 to 15 tiles from it, within reach of a home, valid for a town and weighed by its
biome's settlement weight; a 15% roll every 15 days, at most one per home's reach in 90 days, and only while fewer
than 10 towns stand in reach. A harsh, empty land stays empty; a mild one fills slowly.

**Saved and bounds.** On `LedgerTown`: prosperity, when it last moved, since when it has been low, its state
(founding, standing, closing, gone), its ruin's world object. 40 towns in detail per home, 12 ruins, gone towns 120
days as now. Per home: when it last saw a founding, an abandonment and a resettlement.

**Ages.** Words only: a struggling town is "hungry" or "short of everything" by their age; a ruin is burned-out
longhouses, an empty town, a ghost town, a derelict colony; the newcomers to a ruin are a clan, families, a company, a
charter.

**Bad side.** The region's hardship comes to the gate through the existing events (beggars, refugees, thieves) at
their own rates; nothing new hurts a colonist. A town the colony relied on can go.

**With and without other mods.** Faction Territories: its invasions reset a town; its new towns start at 30; its
builders switched on turn Rimfolk's own new towns off (it already adds about a town a day across the world). Factional
War: its battles on a home move the two peoples' nearest towns. Without either: wars come from the game's relations,
new towns from Rimfolk.

## 3. Trading posts

**What the player sees.** A people the colony trades with, whose towns are far, opens a trading post a few days off:
"The Lenlia have opened Ashford Depot, two days east, and their factor sends greetings." It is on the world map like
any of their towns; caravans trade there in the game's own trade window; its keeper and factor are notables who write;
their trader caravans come more often while it stands. When relations sour, the factor writes that the post will close
unless things mend; if they don't, it is packed up and leaves a ruin.

**How it works.**
- Who: a humanlike people at peace with goodwill 40 or more (or allied), whose memory holds trade with the colony
  three times or more in the last 120 days, whose nearest town is 15 tiles or more from the home, and who keep town
  traders (`baseTraderKinds`). The Empire may open one.
- When: looked at every 15 days per home, 20% when a people qualifies; at most two posts per home and one per people;
  none within 60 days of another opening or closing at that home.
- Where: 6 to 12 tiles from the home, toward their nearest town (within 60 degrees) where it can, on a road or river
  by preference, on a tile valid for a town with no world object, 3 tiles or more from other towns and 6 or more from
  every home. None found, no post.
- What: a `Settlement` of theirs named by their age, their naming rule's name with "Trading Camp", "Market", "Depot"
  or "Ground Station" ("Ashford Depot"). On the ledger as kind Post with its colony; prosperity from 40, at most 70.
  Its keeper (Head) and factor (Trader) are notables at once (a post counts as dealt with). An opening letter
  (`Tell.Letter`) looking at it; history `world:post`, a milestone.
- Visiting: the game's own trade at a town (its stock from their town traders), caravan requests and quests that pick
  a nearby town. Rimfolk adds their trader caravans ×1.5 while it stands within reach, couriers' trips measured to the
  post, the factor's letters (greetings, news, help) and talk pieces about it.
- Souring: goodwill under 0 for ten days brings the factor's warning letter; ten more days under 0 and it closes
  through item 2's abandonment (a ruin; holdouts while the colony has business there). War with the colony closes it
  within a day when `Safe`, else when it next is. Attacked by the colony, it falls as any town does, and their people
  remember a post razed (a heavy wrong).

**Saved and bounds.** The town record (kind, colony, opened, warning sent). Two per home.

**Ages.** Theirs names it: a trading camp (tribal), a market (medieval), a depot (industrial), a ground station
(spacer); its keeper a trader, a factor, a manager, a quartermaster. The colony reaches it by its own means: a
caravan, or pods and a shuttle, which trade at a town too.

**Bad side.** Goods on the road draw robbers: the bandit-road world event twice as likely while a post stands in
reach; the colony's caravans to it meet the game's own caravan incidents; if the post's people are at war with a
people whose town is within 20 tiles, a 5% roll every 15 days that it is sacked (prosperity −30 or given up, refugees
at the colony's gate). A post closing costs no goodwill of its own; what soured it already did.

**With and without other mods.** Faction Territories counts the post as their town: their territory reaches toward the
colony, their builders may expand from it, and their territory's encounters meet the colony's caravans there. Its "buy
as vassal" works on a post too (it is then the colony's vassal, and the post record ends). Without it, the post is
simply their town. Factional War: nothing special.

## 4. Raiders' camps

**What the player sees.** A rich colony on bad terms with a people: raiders make camp a day or two off. A colonist out
hunting sees smoke and comes back to say so, or a friendly visitor warns, or the first raid's tracks lead back to it;
then it is on the world map ("the brigands' hold"). Raids come from that side until the colony attacks it (the game's
own camp map, their fighters behind their walls), pays them to go, makes peace with their people, sees an ally burn
it, or until their raids fail and they move on.

**How it works.**
- Who: a home whose threat points (`StorytellerUtility.DefaultThreatPointsNow`, the game's measure of a colony's
  wealth and strength) are 1,000 or more, about a mid-game colony, and a people that can raid it (`Raids.CanRaid`) and
  is on bad terms with it: hostile with a town within 60 tiles or Faction Territories' territory within 30, or holding
  a grudge (the ledger's grudge, a vendetta, their memory's wrongs). The biggest grudge first, then the nearest.
- When: looked at every 10 days per home, 10% when a people qualifies (a camp in about 100 days of qualifying), by the
  rate setting; never before day 60; one per home; none within 60 days after the last ended or while a world event
  about that people runs.
- The camp: a `Site` with Core's `BanditCamp` part, their faction, the game's own site threat points, `customLabel` by
  their age, 4 to 9 tiles from the home on a passable tile, preferring hills and forest, 3 tiles or more from any
  friendly town. It stays off the world map until found; Rimfolk keeps its tile and points.
- Found: on each day colonists work or hunt beyond the walls, 30% that one of them spots it (a play-out: they come
  back to the gate and tell); a friendly visitor or a neighbour's letter may warn; at the latest its first raid's
  retreat leads back to it. Then the site is added, a threat letter looks at it, and the history has `world:raidcamp`.
- Raids: in `Pass.Region`, a roll on the game's raid-source curve at the camp's points; the first no sooner than two
  days after it is set; through `HappeningTracker.AddRaid` at 0.6 of the home's threat points (the game's own factor),
  from the map edge on the camp's side, at the home it threatens only, never while another threat is on that map.
- Its ends:
  - Attacked: Rimfolk watches the site's map; when the game removes it with no active threat left, the camp is broken:
    a milestone, the peoples it troubled remember it, and so do the colonists who fought there (item 7). Left with
    fighters alive, the game removes the site anyway, and Rimfolk puts it back on the same tile with points less those
    who fell.
  - Paid: once it is found their terms come (`Tell.Ask`), silver about half their raid points, carried by a colonist
    to their messenger at the map's edge. They leave, with a Promise tie to stay away a year; one in ten break it (a
    new camp later: "they broke their word").
  - Peace: their people no longer hostile, they break camp.
  - An ally: an ally of the colony with a town within 30 tiles may burn it, 10% every 5 days ("Riders of the Lenlia
    burned the brigands' hold"); the site goes.
  - Giving up: two raids beaten (half their fighters down or fled) or 60 days, and they move on.

**Saved and bounds.** Per home one camp (people, tile, points, the site's id, found or not, raids sent and beaten,
terms offered, since) and its Promise tie. History entries.

**Ages.** Theirs names it: a war camp, a brigands' hold, an outlaw camp, a raider base; their tech builds its map. The
terms go by the colony's means (a gift by messenger, silver by runner, a transfer by radio) and their rite.

**Bad side.** It is one: raids at the game's raid-source rate and points while it stands, on top of the storyteller's
own. Hence a rich colony only, found within days, five ways to end, and a give-up rule (decision 7). The storyteller's
threat scale sets its points, as for every raid.

**With and without other mods.** Faction Territories: a hostile owner of territory near the home is the likeliest
people; its encounters and invasions stay its own. Factional War: while the camp's people fight a battle on that home,
the camp's next raid waits. Without either: hostile towns and grudges choose the people.

## 5. Travellers who seek out the remote

**What the player sees.** The farther the colony is from everyone, the more often travellers come on purpose: pilgrims
to a relic or a holy place, scholars to the ruins or the library, nomads with their herds, prospectors for the ore in
the hills, a hermit, penitents. Each says why they came, and it is true on the map. They bring news of towns far away,
so a lonely colony still hears what the world is doing.

**How it works.**
- Remoteness per home, from the nearest town of a humanlike people at peace: 0 within 12 tiles, rising to 1 at 40,
  plus 0.2 with fewer than two such towns within 40 tiles, at most 1. Read daily; it falls by itself as the region
  fills (Faction Territories' towns, posts, hamlets).
- The camps' clock: `CampTimer.DailyChance` (0.05, a camp in about 20 days) times 1 + 0.6 × remoteness, so up to 0.08
  (one in about 12 days at the most remote); the gap after a camp, 15 days, times 1 − 0.3 × remoteness. The seekers'
  kinds weigh 1 + 2 × remoteness (a field `seeksRemote` on `CampKindDef`): pilgrims, scholars, prospectors, surveyors,
  drovers (the nomads), the salt caravan, far merchants, the hermit, penitents, tree singers, and spacers and
  salvagers by age. The game's own visitors and traders are left alone (it sends them at the same rate whatever the
  distance).
- Why they came, from the map: scholars to ancient structures or ruins on the map, the colony's books, a ruin in the
  region; pilgrims to the colony's relic or shrine (Ideology), else a wonder of the land; prospectors to the map's ore
  or a silver rush; nomads to grazing in season. A kind with no reason on this map keeps its base weight.
- Scholars, a new camp kind by age (a tale-seeker, a scholar with a mule of books, a professor and two students, an
  archaeotech surveyor): they study the ancient things on the map or read in the colony's library, ask for lodging or
  a guide, and may leave a book. Nomads are the drovers, the salt caravan and the far merchants; no new kind.
- News: a seekers' camp tells Heard lately of towns beyond the reach (the news provider widened for them), so the
  World page and the region map show what the far world does.

**Saved and bounds.** Nothing beyond the camps' records; remoteness is not saved.

**Ages.** The kinds already go by age; scholars as above; a spacer colony gets survey teams and salvagers more than
pilgrims on foot.

**Bad side.** The camps' own, at their rates (the harm review's picks for camps when chosen: a brawl when tension runs
high, a fever at their fire, a startled beast), and prospectors quarrelling over a claim.

**With and without other mods.** None needed. With Faction Territories the region fills over the years and the
travellers thin as neighbours come. Alpha Animals' and PokéWorld's camps keep their own weights.

## 6. Settlers who found a hamlet under the colony's protection

**What the player sees.** Families ask to settle a few days off under the colony's protection: a holding that has
lived a year on the colony's land and wants a place of its own, refugees from a town given up, or settlers from a
struggling one. The letter shows the place they have in mind. Accepted, they camp at the gate with their carts until
the colony's caravan sets out; they go with it, and where it stops a hamlet is founded: on the world map, a town of
their people with its founder and families. It brings produce at harvest, asks for help in hard times, and in time
asks to swear fealty to the colony.

**How it works.**
- Who and when: at most one ask a year per home, from day 90, with five grown colonists or more at the home and the
  settlers' people at peace with it. Three ways in: a holding settled a year with low tension; the town-abandoned
  event's refugees ("found a hamlet near you" beside "take them in"); settlers from a poor or struggling town of a
  people at peace.
- The ask (`Tell.Ask`): take them under protection and escort them; let them go alone (they may be turned back or
  fail); refuse. The place: a tile 6 to 10 tiles off by the post's rules, looked at in the letter; "choose another
  place" opens the world map's targeter, checked by the same rules.
- The escort: the settlers camp at the gate (the camps' machinery, a settlers' kind with families and carts) for up to
  five days. A caravan of the colony's with two grown colonists or more that leaves the home for the tile takes them
  along, as the pilgrim's escort does (they are kept with it), and the caravan's panel says so (decision 10). At the
  tile, after a night with the caravan there ("the first fires lit"), the hamlet stands: a `Settlement` of their
  people, kind Hamlet with its colony on the ledger, prosperity 25, a founder and families (item 1), a Fealty tie from
  the hamlet to the colony (key "protection", open), history `world:hamlet`, a milestone.
- Its life: prosperity like any town's; at harvest a carter brings produce to the gate (goodwill, a deed); in a hard
  season it asks for food, a doctor or tools (the help letters and missions). Refused twice in a season, its tie is
  broken and it is just their town.
- Fealty: after 60 days at 40 or more, with no ask refused in the last 60 days, it asks to swear fealty (`Tell.Ask`).
  With Faction Territories and its vassalage on, Rimfolk calls the mod's own `VassaliseUtility.ExecuteVassalisation`
  on the hamlet, by name and guarded, without the goodwill price (they asked): it becomes the mod's vassal outpost,
  and the adapter's view of vassals takes over. Without, the Fealty tie stays, and stage 2's fealty and vassals read
  it.
- Their people turned hostile before fealty: the hamlet asks (`Tell.Ask`) whether its families may come to the colony
  (they join, the hamlet is given up) or stay with their people (the tie broken).

**Saved and bounds.** Per home one plan: its stage, the settlers (kept with `KeepFor.Hamlet`, 8 at most), the tile,
the town's id, since when, asks refused. One hamlet per home.

**Ages.** The protection by the colony's age (the clan's spear, a lord's protection, a protectorate, a charter), the
oath by theirs (gifts at the river, an oath before witnesses, a signed agreement, a registered charter), the hamlet's
word by theirs (a new hearth, a hamlet, a homestead, an outpost colony).

**Bad side.** On the road, the game's own caravan incidents; at the founding, one roll of the game's `CaravanDemand`
or `Ambush`, 15% (35% with a raiders' camp or a hostile town within 15 tiles): a real fight on a map, the settlers
among the defenders. Alone, a third of them are turned back. A hamlet can fail (item 2), and its hard times come to
the colony's gate. Fights at the hamlet itself: decision 11.

**With and without other mods.** Faction Territories: the vassal step; its invasions may strike the hamlet, and its
join-the-fight arrival lets the colony help. Without: the fealty tie. Factional War: nothing special. Whose town it
is: decision 9.

## 7. The content that joins stage 1

- **Memory in more events' texts.** Of Rimfolk's 254 events, 169 name a people or a place and 23 read what that people
  remembers. Events with a people get a facts scene (`FactScene.Event`: their people, deeds, ties, the history) with
  `deed:`, `deedbest:`, `wrongbest:`, `tie:`, `town:lately` and `{DEED}`; then lines for the 60 or so where memory
  fits (visitors, traders, envoys, refugees, callers, missions, camps) and for the world events' openings.
- **A people's memory of single colonists.** `KinMemory` names the colonist of more deeds: who walked the settlers out
  (item 6), who broke a raiders' camp, who traded at their post, who fought beside them; and the bad ones: who killed
  one of theirs in a fight (the death's instigator), who executed or harvested one of their prisoners, who insulted
  one of their notables. Read by the notables' regard of each colonist, by talk and letters (`{FRIEND}`, a new
  `{FOE}`), and by their visitors, who ask for someone or refuse to deal with them (decision 13).
- **Empire titles and Ideology rites in invitations.** The invitation mission, the letters' invitations and the feast
  and funeral missions: an Empire host by the title the game shows (`WorldMechanics.Title`) and the colony's titled
  guest by theirs; a people with Ideology names a ritual its faith really has (its rituals: a feast, a dance, a
  funeral; `their:rite:` and `{RITE}`); others the feast's rite from `Ages.Form`.
- **World events' follow-ups.**
  - *The warlord's son*: a world event 60 to 180 days after the warlord's ends, by how it ended. Slain by the colony:
    the son raises a band, a raiders' camp of item 4 that he leads (he is on its map and at the head of its raids,
    through `AddRaid`'s leader). Captured, handed over or ransomed: he comes to the gate for his father's grave, his
    sword or a peace. Hunted down by others: an exile asking shelter. Broken up or gone: a young man with a few riders
    looking for work, or settlers (item 6). He is the warlord's child by the game's relations while the ledger keeps
    the father, else known as his son by name.
  - *The town that grew from the settlers' camp*: 60 to 180 days after other settlers' year ends in friendship or as
    neighbours (and after a hamlet's second year): the steading is a town now, its leader its head, its prosperity up,
    a proper name (the colony may give one, as at the crossroads), its founding families remembering what the colony
    did in their first year (the earlier event's flags, found through the history's record of how it ended), and an
    invitation to its first market.

## The order to build it in

Each release plays as before with its settings off and every check green; stage 1 is waves 9 and 10 of the plan
(version numbers as they fall).

1. **0.69.0, the region's towns** (wave 9): `LedgerTown`'s new fields and `LedgerHouse`, `RegionHome`, the two passes,
   the reach in detail, remoteness, prosperity moved by world events (the def field) and shown (region map, facts),
   the new towns' faces (item 1), Faction Territories' near messages through Rimfolk's line. Needs stage 0 only.
   Beside it: memory in events' texts (the event scene and the first 30 lines), and the Almanac page "The region".
2. **0.70.0, travellers who seek the remote** (item 5). Needs remoteness (1). Beside it: Empire titles and Ideology
   rites in invitations.
3. **0.71.0, rise and fall** (item 2): abandonment by prosperity, ruins, resettlement, Rimfolk's own new towns,
   prosperity's effects on asks and caravans (and the stock, decision 3). Needs 1.
4. **0.72.0, trading posts** (item 3; wave 10). Needs 1 and 3 (a closing is an abandonment).
5. **0.73.0, raiders' camps** (item 4). Needs 1 (grudges, the history); can swap with 4.
6. **0.74.0, settlers' hamlet** (item 6), with small towns' maps if chosen (decision 4). Needs 1, 3 and 4's placement,
   the holdings and the escort.
7. **0.75.0, the follow-ups and the memory of single colonists** (item 7). The warlord's son needs 5, the grown town 1
   and 3; the colonists' new deeds come from 4 to 6.

The harm review's picks for camps, when chosen, come with 2.

## Performance

- Nothing per tick at world level. `Pass.Region` (hourly) rolls a few dice per home; `Pass.DailyRegion` loops once a
  day over the world's towns per home with straight-line distances (294 today; Faction Territories adds about one a
  day across the world, so some 470 after three years and 900 after ten: a fraction of a millisecond), keeps the
  in-reach set until the towns' count or newest id changes, moves prosperity for 40 towns per home at most, and makes
  the rolls due that day. Both are in Sched's table with their `Pass` entries and timed by Rimfolk's clock.
- The reach in detail: the region map's radius (35 tiles by default, 20 to 60) and the nearest 40 towns per home. A
  crowded region (a mild world after years of Faction Territories) holds the cap; a remote one has a handful. Beyond,
  nothing is kept or changed, and the hooks write nothing (as `WorldMath.Records` already rules).
- Tile searches (`TileFinder`) only when something is placed (a few times a year), with straight-line checks inside
  their flood fill.
- Adding or removing a town or a site makes Faction Territories redraw its territories, as it does for each of its own
  towns (about one a day).
- Pawns: founders two a day at most; the notables' cap grows by two new towns and a home's posts and hamlet (from 34
  to 54 people, the ledger keeping them by its rules).
- Save: towns in detail (40 × about 150 bytes), houses (80 × 120), ruins (12), and per home a camp, two posts and a
  hamlet: under 20 KB a home at five years. The new history kinds fold inside the existing bound of 800 entries. Each
  new list gets its model in `Tools/save_estimate.py` before it ships.
- The region map reads towns through the ledger's views, kept a 250-tick stretch; nothing when the window is closed.

## Tests and checks

The rules are pure (`World/Region/*Math.cs`) and tested in `Tools/Tests` (`RegionTests`), like the ledger's:
- The reach and remoteness: the nearest 40, layers, humanlike peoples only, the edges at 12 and 40 tiles, the
  lone-town step.
- Prosperity: bands, an event's move (full at its town, half at its people's others), drift, the post's cap, war and
  battle moves, the seed by biome.
- Abandonment, resettlement and founding: eligibility (Safe's rules mirrored: the last two towns, the seat, the
  Empire), the caps per reach over five simulated years, "fewer than 10 in reach".
- Posts: who qualifies, placement (the distance band, the heading, the 6-tile rule), the warning and closing clock.
- Raiders' camps: who qualifies, the curve's values, points after losses, the five ends, the Promise tie's states.
- Travellers: the daily chance and gap against remoteness, the kinds' weights, a kind with no reason on the map at its
  base weight.
- The hamlet: the ask's conditions, its stages, fealty's timing, the broken tie.
- The follow-ups: which branch each of the warlord's endings takes.

Existing checks that must stay green: `LeanTests` (the two new slots), `TellTests` (no direct message), `LedgerTests`
and `HistoryTests` (new fields, kinds and folding), `FactsTests` (1500 talks identical with the region's provider),
`OutsiderTalkTests` (new tags and placeholders), `ImmersionTests` (spoken lines one sentence), `WorldMechanicsTests`
(the vassal call and the builders' setting stand down without the mod), `ManyHomesTests` (each home's region its own;
one home plays as before), the save model (`--strict`), the def validator (the scholars' camp kind, the follow-ups,
the new def field, the keyed forms), `labelcheck`, `almanaccheck` (keywords unchanged), `playout_audit` (gifts,
payments and produce carried), `interaction_audit`, and the 1.7 API diff for the new game names (`SiteMaker.MakeSite`,
`Site.customLabel`, `QuestTuning.PointsToRaidSourceRaidsMTBDaysCurve`, `BiomeDef.settlementSelectionWeight`,
`WorldGrid.GetHeadingFromTo`, `IncidentParms.spawnCenter`).

In the game, with dev tools to force each part: a town begun and finished by Faction Territories within reach
(founder, one message, the panel); a plague moving a town's fortune; an abandonment and a resettlement end to end; a
post opening, trading, souring and closing; a camp found, raiding from its side, cleared, and left half-cleared; a
remote start's camps over a year; an escort with the settlers, the hamlet, and its fealty with Faction Territories.

## Decisions for the player

The player's answers (4 November 2026): 1 and 3 yes, built in the first release; 2 changed: Rimfolk founds its own new
towns even while Faction Territories builds towns, at Rimfolk's own pace; 4 yes, and small towns' maps must cooperate
with the player's base-generation mods; 5 to 14 as recommended.

1. New towns' welcome: the full welcome (a runner at the gate, a gift hoped for) for towns within about 15 tiles, at
   most once a year per home, and news, faces and talk for the rest of the reach? Recommended: yes.
2. Rimfolk's own new towns only while no other mod builds towns (on without Faction Territories or with its builders
   switched off, off while they build, since they add about a town a day across the world)? Recommended: yes, worked
   out by itself.
3. A town's fortune in what it sells: when the game restocks a town in detail, a struggling one has a quarter less
   food, medicine and silver and a thriving one a little more silver (a small patch on the game's restock), so the
   trade window matches the words; or words, caravans and asks only? Recommended: the small patch.
4. A small town looks small: a post, a hamlet or a town in its first year, when attacked or entered, is a smaller base
   with fewer defenders on its map (a patch on the game's settlement generation), so what the words call a hamlet is a
   hamlet? Recommended: yes, with the hamlet release.
5. Ruins where the game leaves none: after a town within reach is destroyed (by the colony or in an invasion), a ruin
   stays on its tile, to be resettled in time? Recommended: yes.
6. The trading post as a real town of theirs on the world map (visitable, attackable, part of their territory with
   Faction Territories, whose builders may then expand toward the colony from it), or only a market camp on the
   colony's own land? Recommended: a real town.
7. Raiders' camps: raids at the game's own raid-source rate on top of the storyteller's while the camp stands (found
   within days, five ways to end), or the camp only as the source of that people's ordinary raids, adding none?
   Recommended: on top, with the rate setting at 100% and the give-up rule.
8. Travellers: up to 60% more camps for the most remote colony, and scholars as the one new camp kind (nomads from the
   drovers, the salt caravan and the far merchants)? Recommended: yes.
9. The hamlet's people: a town of the settlers' own people (it follows them in war and peace until it swears fealty),
   or a small new people of its own made in play (loyal to the colony alone, but a permanent faction in the save, with
   relations to every people, an ideoligion to choose, Faction Territories expanding it every 30 days unless patched,
   Factional War and quests using it, one more in every mod's faction loops; made temporary it would leave the save
   with the hamlet, but the game sends no visitors or caravans from a temporary people)? Recommended: their own
   people.
10. The escort: the settlers go with the caravan as the escort mission's pilgrim does (kept beside it, not in its pawn
    list), with a line on the caravan's panel naming them (a small patch); or as temporary members of the caravan,
    like the game's lodgers (more to go wrong in a save)? Recommended: beside it, with the panel line.
11. Fights at the hamlet itself only through Faction Territories' invasions (which the colony can join); without that
    mod, the hamlet's troubles come as asks and news, and a threat to it as a raiders' camp near both? Recommended:
    yes for now; a staged defense later if wanted.
12. The warlord's son: when the colony killed his father, he comes back at the head of a raiders' camp (item 4's rate)
    and leads its raids? Recommended: yes.
13. The bad memories of single colonists: their people name who killed their kin or executed one of theirs, think less
    of them, and their visitors may refuse to deal with that colonist; no raid of its own (a vendetta stays the
    colony's own story)? Recommended: yes.
14. The pace of change in a home's reach: at most one town given up in 60 days, one resettled in 120, one founded by
    Rimfolk in 90; ruins Rimfolk made gone after 300 days? Recommended: these numbers, with the fortunes setting to
    switch it off.

## The player's decisions (4 November 2026)
1. The full welcome for new towns within about 15 tiles (a runner at the gate, a gift hoped for, once a year per
   home at most): yes. Built in the first release.
2. Rimfolk's own new towns: yes, and also while Faction Territories builds towns, at Rimfolk's own pace (at most one
   founded by Rimfolk per 90 days in a home's reach, decision 14).
3. Prosperity in the trade stock (the small patch on the game's restock): yes. Built in the first release.
4. Small towns look small on their own map: yes, with the hamlet release, working with the player's generation mods.
   In the modlist: Vanilla Base Generation Expanded (each people's settlements from its own layouts: the smaller base
   goes through its layouts, not around them), Real Ruins (ruins and points of interest on the world: leave its
   sites alone, and let a Rimfolk ruin be an ordinary `AbandonedSettlement` it may dress), Geological Landforms, Biome
   Transitions and Grand Rivers Reborn (the land of a tile, not its base), Faction Territories, Vanilla Outposts
   Expanded and Factional War (world objects and sites of their own). Read each mod's code before patching.
5. Ruins where a fight destroys a town within reach: yes.
6. The trading post: a real town on the world map.
7. A raiders' camp's raids on top of the storyteller's, at the game's rate, with the rate setting and the give-up
   rule: yes.
8. Up to 60% more camps for the most remote colony, scholars the one new camp kind: yes.
9. to 14. As recommended: the hamlet is the settlers' own people's town; settlers travel beside the caravan with a
   line on its panel; fights at the hamlet only through Faction Territories' invasions for now; the warlord's son
   returns at the head of a raiders' camp; bad memories of single colonists (no raid of their own); the pace of
   change as given in decision 14, with a setting to switch it off.
