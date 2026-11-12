# Rimfolk's peoples

New peoples (factions) for Rimfolk, built mostly from what the game, the DLCs and the player's mods already draw. The
player's words, 7 November 2026: "we can start with a few factions and see how it goes, and then further expand on
those. reuse and create at your own discretion. they should also be able to arise during play so that i will see new
factions in an existing run", and "use all dlc and mods to your availability".

Four releases. **Release A** (7 November 2026): three peoples of peaceful callings the world lacked, made when a new
world is made. **Release B** (7 November 2026, below): peoples that arise during play, so an existing save sees new
ones, built on release A's templates and code. **Release C** (7 November 2026, below): four more peaceful peoples, the
pilgrims, the scholars, the spacer enclave and the river folk, which release B's peoples that arise may also take the
ways of. Their gear, kinds of people and gifts were reworked after (7 November 2026, "Their gear, their kinds of people
and their gifts", below): the tables' looks of each release are as first built. Then all seven were brought to the level
of the eight new peoples of `docs/design/peoples-next.md`: a founding story, sayings, what they honour and fear, a fuller
voice and a touchpoint each ("Their stories, sayings and touchpoints", below). **Release D** (7 November 2026, below):
the first four of those eight, the Lantern Wardens, the Quiet Bell, the Circuit of Tamers and the Wheel.

## What was built (release A)

Three FactionDefs in `Defs/PeopleDefs/`, each on the game's own abstract parent, with its pawn kinds, traders, names,
fixed faith, xenotype share, colours and a `PeopleExtension` (the template Rimfolk reads); emblems and town icons
drawn in code; their towns' plans; and their places in Rimfolk's camps, asks, callers, letters, talk and world events.
Code in `Source/ColonyCouncil/Peoples/`: `PeoplesMath.cs` (pure, tested: `Tools/Tests/PeoplesTests.cs`),
`PeopleDefs.cs` (the template and the plan defs), `Peoples.cs` (the registry and every question Rimfolk asks),
`PeopleTowns.cs` (a town's map from its plan), `PeoplesAsks.cs` (an ask from a people of a calling),
`PeoplesDebug.cs` (dev actions).

| | The road league | The hospitaller order | The herding folk |
|---|---|---|---|
| defName | `Rimfolk_League` | `Rimfolk_Order` | `Rimfolk_Herders` |
| Calling | traders | healers | herders |
| Age | Industrial (outlander parent) | Medieval (tribal parent) | Neolithic (tribal parent) |
| Look | dusters dyed teal, cowboy hats; outriders in flak and helmets; the factor's bowler, the league master's top hat (Royalty) | ivory robes and hoods; sworn guards in plate and helmets; lay archers | russet hides; fur tail caps (Ideology), broadwraps (Ideology), war veils |
| Weapons | the game's gun tags | knives, maces, swords; recurve bows | clubs, spears, ikwas; recurve and great bows |
| Leader | warden of the roads | prior or prioress | first herder |
| Town head, heir, envoy, trader | waymaster, second waymaster, league factor, caravan master | prior or prioress, subprior or subprioress, almoner, cellarer | camp elder, second elder, rider, drover |
| Messengers | road riders | postulants | riders |
| Towns | few (weight 0.4): walled waystations | sickhouses (0.8) | camps (0.6) |
| Caravans | three times as likely as another's; rich and varied stock | an almoner's caravan of medicine and plain goods | half again as likely; a herd of 8 to 14 for sale walks with it |
| Raids | 0.35 of another people's commonality | 0.25 | 0.5 |
| Faith (Ideology) | The Long Road: collectivist (and VE Memes' trader); ideological or abstract; rustic style | The Open Hand: guilty, flesh purity (and VE Memes' healthcare); abstract or Christian origin; Christian style | The Long Grass: rancher (and VE Memes' nomad); animist or embodied; animalist style |
| Kinds (Biotech) | hussar, genie, highmate, starjack (Odyssey) | genie | yttakin, pigskin, neanderthal, Alpha Genes' animusen |
| Colours | teal | ivory | russet |

**Gear** was at first the game's own apparel and weapon tags plus a few required pieces, all dyed the people's colour
(`apparelColor`), so each people looked like one uniform; since the gear rework every kind varies around one signature
colour on a piece or two (below, "Their gear, their kinds of people and their gifts"). An ordinary member forces no skill
(a joiner made from one is no healer or trader by force), except the herders' herding.

**At a world's making**: each is listed once among the world's peoples (`startingCountAtWorldCreation` 1,
`maxConfigurableAtWorldCreation` 9999), the game's usual way; never required (`requiredCountAtGameStart` 0). The player
can remove them or add more on the world's page.

**Raids**: never natural or permanent enemies, no sieges, a low raid commonality. They raid only when the colony has
turned them hostile, by the game's own rules.

**Emblems** (`Tools/make_emblems.py`, the shape helpers of `Tools/make_icons.py`, drawn at 512 and scaled to 128, white
on transparent, tinted by the game): the league's scales hung from a waystone, the order's open hand with a lamp's
flame in its palm, the herders' crook across a horn. Town icons for the world map: a walled waystation with its arched
gate and pennant, a gabled sickhouse with its lantern tower, a round felt house between two tents. Preview:
`Tools/emblems_preview.png`. Files: `Textures/ColonyCouncil/Peoples/League.png`, `LeagueTown.png`, `Order.png`,
`OrderTown.png`, `Herders.png`, `HerdersTown.png`.

**Their towns' maps** (`Defs/PeopleDefs/Rimfolk_TownPlans.xml`, `TownPlanDef`; `Peoples/PeopleTowns.cs`): rows of
characters from the north, each a key (a building, its material or the town's wall material, its turn, the floor, a
roof, what the cell is for). Read into the kept-layout grid (`PeoplesMath.Grid`, the game's footprint rule
`PeoplesMath.Footprint`) and placed by `TownLayouts.Place` from the same prefix on the game's settlement step that
places a town the colony was (with the deeper game hooks on), near the map's middle where at most 6% of the cells it
uses are bad ground (water, marsh, impassable). Then the game's own loot in the storehouse, the people's own goods, the
herd in the pens, the garden's healroot, and its people at home: its notables, then the game's settlement group under
its defend-the-town lord. The league's waystation (33 by 17): a walled yard with market stalls and a fire, two rooms of
beds, a storehouse, the waymaster's hall, a gate and two pens. The order's sickhouse (31 by 18): a ward of sixteen beds,
a cloister yard with herb beds round a fire, a storehouse of medicine, a refectory, the hospitallers' own cells. The
herders' camp (37 by 23): five round houses, a fire, a roofed shelter on four posts for hay and hides, two great pens.
Only the base game's buildings. Vanilla Base Generation Expanded's layouts stand down for these towns (its settlement
step is the game's kind); where the land won't take the plan, its layout or the game's own base is made.

**In Rimfolk's systems**:
- Notables (`NotableBook.TitleOf`, `RegionFaces.TitleOf`): titled by their people's ways.
- Letters by their means: their answers in their own words (`Rimfolk_Pool_NeighbourWrites`, tagged `their:CALLING`),
  their messengers by their own word (`CourierWords.Noun`) and lines (`Rimfolk_CourierLines`).
- Talk: the facts service gives `their:traders`, `their:healers`, `their:herders`; nine pieces of talk in their voices.
  The same tag in the asks', the camps', the events', the bubbles', the visitors' and the world events' lines.
- Camps: each favours some of Rimfolk's kinds (four times as likely from them): the league's market folk, salt
  caravans, far merchants and road wardens; the order's healers, lone healer, herb-gatherers, pilgrims and penitents;
  the herders' drovers, shearers and beast-catchers. One new kind only theirs: the herders' **winter herd**
  (`Rimfolk_Camp_WinterHerd`, fall and winter, a herd of up to twelve head and a dog, eight to thirteen days, hay of
  their own; `CampKindDef.calling`, `seasons`, `maxAnimals`).
- Asks (`MissionPeople` on a mission: `only`, `prefers`, `whileKind`): the order's **medicine for the sickhouse** in a
  plague year (a delivery: their carts come for it); the league's **factor to see home** (the traveller's escort); the
  healer lent to a sickhouse likelier from the order and the fodder for a herd from the herders (by patch). An ask only
  theirs takes no share of the missions' draw in a world without them (`MissionPool`).
- Callers (`HappeningSetting.people` `calling:CALLING`): the order's **hospitaller at the gate** (free care for a
  colonist with an ailment, medicine sent back, the colony's doctor learning); the league's **outrider** (the league's
  own caravan turns aside to the colony when bidden; only when it can come now, the need `caravan`).
- World events (`SituationFactionNeed.calling`, by patch): the drove roads and the great herds prefer the herders; a
  great caravan, a trading season, market days, the road in use, a wardens' post and a new road the league.
- The animal callers (`HappeningSetting.prefers`, by patch): a herd that needs water, a sick herd and drovers on the
  road to a fair come from the herders likeliest.
- The game's caravans (`Patch_RiseFallCaravans`): each people's `caravanWeight` in the game's choice of whose comes.
- Sickness brought home: a sickhouse of the order sees to the colony's lodgers as medicine in their packs would.
- The hand-over's peoples never borrow these peoples' kinds (`HandOverPeoples.Lend`).
- An Almanac page: *the road league, the hospitallers and the herders*.

Nothing new is saved and nothing runs on a clock of its own.

## Softness

| Missing | What happens |
|---|---|
| Ideology | No fixed faith, no style; the game's own faith for a people without it. Tail caps and broadwraps aren't worn; the work-camp groups aren't there. |
| Royalty | The league master wears no top hat. |
| Biotech | Humans only; no children in their peaceful groups. |
| Odyssey | No starjacks with the league. |
| VE Memes | Their faiths have only the game's memes (one or two each). |
| Alpha Genes | No animusen with the herders. |
| VE apparel, armour, weapons | The base game's gear under the same tags. |
| Vanilla Base Generation Expanded | Nothing changes: their towns are made from their plans either way. |
| RimMarket | Their camps don't buy at the colony's stalls; nothing else changes. |
| The deeper game hooks (setting) | The game's own bases for their towns, and their caravans at the game's even chance. |
| Rimfolk itself | A save holding one needs Rimfolk, as for any mod's people (the game drops a people whose def is gone). |

Not used: Vanilla Races Expanded's xenotypes (their defNames couldn't be checked here), the Sophian style (not
checked), Vanilla Base Generation Expanded's layouts for these towns (Rimfolk's own plans instead).

## For release B (peoples that arise during play)

What release B can reuse:

- **The templates**: every FactionDef with a `PeopleExtension` (`Peoples.Templates`): `Rimfolk_League`,
  `Rimfolk_Order`, `Rimfolk_Herders`. A people that arises is one of these (a splinter of a league, a new order, a herd
  folk come down from the mountains), or a new def written the same way: a FactionDef on the game's parent with the
  template; Rimfolk's systems then know it by its calling with no code.
- **Making one**: `Peoples.Make(def, nearMap)` makes a people of a template the game's way
  (`FactionGenerator.CreateFactionAndAddToManager`: its fixed faith, leader, relations, one town) and moves its town a
  few days from the home. Release B adds when and why (a town's split, a ruin resettled, a caravan that stays), the
  news, and the relations a new people starts with.
- **The callings**: `PeoplesMath.Callings` and their tags. A new calling is a new string there, its lines in the pools
  (`their:CALLING`), and its own words in its template.
- **The emblem pipeline**: a function in `Tools/make_emblems.py` and a line in `GLYPHS`; the faction def's
  `factionIconPath` and `settlementTexturePath` name the files.
- **The plan pipeline**: a `TownPlanDef` (checked by `PeoplesTests` for every plan in `Defs/PeopleDefs/`) named in the
  template.
- **The asks and callers**: `MissionPeople` (`only`, `prefers`, `whileKind`) on any mission def, `calling:CALLING` or
  `prefers` on any caller, `calling` on any camp kind or world event's people.
- **The dev actions**: "Peoples: a people of a template now, near this home (testing)", "Peoples: Rimfolk's peoples in
  this world", "Peoples: one of their camps here now", "Peoples: lay a town's plan at the mouse (testing)".

Templates for release B to draw from (ideas, not built): a salt or river league (traders, the league's parent, a
river-town plan), a mountain hospice (healers, a stone hospice plan), a reindeer folk of the cold (herders, yaks and
the cold animals), a pilgrim order (healers' parent, pilgrim camps), a caravan town of former raiders turned traders.

## What was built (release B: peoples that arise during play)

New peoples come into being during a game from what happened in it, so the player sees new peoples appear in an
existing run (the player's words: "they should also be able to arise during play so that i will see new factions in an
existing run"; later "i'd be interested in some non-hostile faction variety"). No new FactionDef: each arising people is
one of release A's three templates, by its cause and its age, with a name, an emblem colour and a town of its own. Code
in `Source/ColonyCouncil/Peoples/`: `ArisingMath.cs` (pure, tested: `Tools/Tests/ArisingTests.cs`), `Arising.cs` (the
causes, the pace, the making), `ArisingWords.cs` (the letter, the history, the cards), `ArisingBook.cs` (the records,
saved), `ArisingDebug.cs` (dev actions). The Almanac has a page of its own, *new peoples that arise*.

| | Survivors | A league | A band of the banished | Faithful who broke away |
|---|---|---|---|---|
| Kind | `refugees` | `league` | `exiles` | `splinter` (Ideology) |
| Cause | a town within reach fell in a fight (RiseFall's fight ruin, `LedgerRuin.Fight`) | three or four towns of two peoples or more at 62 or more, within 18 tiles of the most prosperous, none at war with another or with the colony | one the council sent away (`BanishRecord`), alive in the world and of no people, not come home, never ridden with raiders | a war of words over a faith ended (`Council_WS_HolyWar` in the world events' history): the people whose preachers began it |
| When | 3 to 60 days after the fall, two fallen towns in five | looked for every five days, on a fifth of the looks | 15 to 300 days after, half of those sent away, on a fifth of the looks | within 30 days of its end, half of them |
| Age from | the fallen town's people | most of its towns' peoples | the colony | the people they left |
| Template (tribal, medieval, industrial, spacer) | herders, order, league, league | herders, league, league, league | herders, herders, league, league | herders, order, order, order |
| How they live (the letter) | kept to the open grass with what beasts they saved; a sickhouse begun for their own wounded; carrying goods between the towns from a waystation | the merchants keep the roads between their towns open; among tribes, herders join their herds and grazing | a few beasts on the grazing, taking in whoever else is turned out; hiring out as carriers and guards | vows and a sickhouse, as they say the faith first asked; their herds on the open grass apart from the old camps |
| Goodwill with the colony | -40 if the colony's own people took the town; else half the colony's goodwill with their old people, -20 to 40, never warm toward a colony their old people were at war with | 10 and half its towns' peoples' average, -10 to 50 | -30 with a grudge; else 10, 8 more for each of the colony who stood by them, 8 less for each who was glad to see them go, -20 to 40 | 25 if the colony kept a fair table in the debate, else 10 |
| With other peoples | 40 with their old people | 50 with each of its towns' peoples | none set | -30 with the people they left |
| Faith (Ideology) | their old people's | another league's of the same ways, else its own | their leader's | their old faith reformed the game's way (`IdeoUtility.RandomizeNormalMemesForReforming`: one meme dropped or one added), of the same kind of foundation, a name of its own |
| Name | "Ashford Folk", "Ashford Hospitallers", "Ashford Carriers" (or their second forms) | "Brightwater League", "League of Brightwater"; among tribes "Brightwater Drove" | "Barr's Folk", "Barr's Company" | their template's own rule |
| Town | 2 to 6 tiles from the ruin, "New Ashford" when the name is free | 3 to 8 tiles from the most prosperous, likelier between them all | 7 to 14 tiles from the seat | 3 to 8 tiles from their old people's town |
| Leader | made once their faith is set | made once their faith is set | the one sent away | made once their faith is set |

**The pace** (`ArisingMath.PaceAllows`): none before the world's 15th day; 40 days at least between two new peoples and
90 between two of the same cause, at the setting's 100% (the setting *New peoples arise*, `newPeoples`, 0 to 200%: the
waits divided by it, 0 none); six standing at most. A year is 60 days, so one or two a year at most, and fewer in
practice since most causes give none. Run by the region's day (`RegionBook.Daily`), one a day at most, the causes in
turn so none waits behind another. No Sched slot.

**Making one** (`Arising.Arise`): the town's tile first (`Arising.TryTownTile`: a few tiles' walk from the cause, valid
for a town, nothing on it or next to a town being built, clear of the game's nearness rule round every home
(`RiseFallMath.ClearOfHomes`), on no people's land but the cause's own peoples' (Faction Territories), by the biome's
settlement weight; a second, wider look; none: nothing happens and the cause may give one later). Then the people
(`FactionGenerator.NewGeneratedFaction` with the template's fixed faith parms, hidden while made so the game places no
town, no leader until the faith is set: `Arising.Making` in `Patch_HandOverLeader`), its name (`CC_Arise_Name_*`, kept
when 24 characters or fewer and no other people's), its colour (`ArisingMath.Palette`, the one farthest from every
people's in the world, as `Faction.color`, which the game saves), its faith, its leader, its goodwill (both ways, by the
game's thresholds, never for a pair that are enemies for good), then the game's peoples. Then its town
(`NewTowns.MakeTown`), its record (`ArisingBook`), the region's faces (`RegionFaces.Founded` with its leader as founder:
founding families, a notable at its head; the founding counts on the region's pace, `RiseFall.NoteFounded`), its
letter and the history's `world:arose`.

**The letter** (`ArisingWords.Tell`, `Importance.Important`, a positive letter unless they start cool): opens with the
colony's means (`Ages.Form(AgeFeature.Founding)`: word from a traveler, a rider's news, the radio, a comms relay's
report), says what they came from and, by their calling, how they live (which is what their town's plan and their
pawns show); then where their town stands with the founding's rite by their age, and how they feel (cool, plain or warm
by their goodwill, the grievance said). It looks at their town on the world map, with the ruin, the league's towns or
the old people's town. Their card (`CardKind.People`) and their towns' cards: "Arose 40 days ago: the survivors of
Ashford, a town of the Lenlia that fell."

**Saved**: `ArisingBook.arisen`, one `ArisenPeople` a people (the people, the kind, the cause's key, when, what it came
from, the parent people and their name), `ArisingMath.MaxRecords` (24) kept, the oldest of a people no longer standing
first. A cause's key (the ruin's town, the banished one's id, the war of words, the league's towns) gives one people at
most, and a town is in one league at most. Modelled in `Tools/save_estimate.py`. The rolls are `ArisingMath.Fated` (a
hash of the cause's id), so nothing else needs keeping.

**Softness**

| Missing | What happens |
|---|---|
| Ideology | No faithful break away. The others keep the game's faith for a people without it. |
| Faction Territories | Any land the game takes a town on. With it, no other people's land. |
| Towns rise and fall (setting) | No new fight ruins are kept, so no new survivors (ruins kept already still count). |
| Towns' fortunes (setting) | No league. |
| Banishment by the council (setting) | No one newly sent away, so no new band (those sent away already still count). |
| World events (setting) | No war of words, so no faithful break away. |
| Rimfolk's peoples' defs | None arises (no template). |
| A save made before release A | Arising is how it first meets Rimfolk's peoples. |

**Dev actions** (Debug actions, Rimfolk): "Peoples: a new people arises now (testing)..." (each cause, whatever the
pace and the rolls, from the cause the world has or a stand-in: a town in reach as if it fell, a stranger as if sent
away, a people with a faith as if it split), "Peoples: what could arise now" (the pace, each cause found or not, the
template it would take, the peoples that arose, in the log).

The one sent away, once at the head of their band, rides with no one else's raid (`BanishTracker.JoinArrivingRaid`)
and doesn't come to the gate asking to be let in (`HappeningWorker_Prodigal`): the band would lose its head.

Fixed on the way: the dev action "a people of a template now, near this home" could put its town three or four tiles
from the home, where the game takes goodwill every quadrum (`Peoples.Make` now uses `Arising.TryTownTile`).

## What was built (release C: four more peoples)

Four more peaceful peoples, each a new FactionDef on release A's template, distinct from release A's road league
(traders), hospitaller order (healers) and herding folk, from each other, and from the peoples the player's mods bring
(no mod among the 303 has a pilgrim, scholarly or river people; Odyssey's Traders Guild trades from orbit, the enclave
lives on the ground and comes down to the colony's gate). The player's words: "yes, i'd be interested in some
non-hostile faction variety", and "go ahead on all the new factions also". Code in `Source/ColonyCouncil/Peoples/`:
`PeoplesCallers.cs` (their callers and the lander), new pure rules in `PeoplesMath.cs` and `ArisingMath.cs`
(`Tools/Tests/PeoplesCTests.cs`, "peoples, release C: "), the plans' docks and the rivers in `PeopleTowns.cs`. Defs in
`Defs/PeopleDefs/Rimfolk_People_Pilgrims.xml`, `_Scholars.xml`, `_Enclave.xml`, `_RiverFolk.xml`,
`Rimfolk_TownPlans_PeoplesC.xml`; their callers in `Defs/IncidentDefs/Rimfolk_Events_PeoplesC.xml`, their asks in
`Defs/MissionDefs/Rimfolk_Missions_PeoplesC.xml`, their words in `Defs/FlavorPoolDefs/Rimfolk_Lines_PeoplesC.xml` and
`Defs/OutsiderTalkDefs/Rimfolk_OutsiderTalks_PeoplesC.xml`, the patch in `Patches/Rimfolk_PeoplesC.xml`. The Almanac has
a page of their own, *pilgrims, scholars, the enclave and the river folk*.

| | The pilgrim fellowship | The learned society | The spacer enclave | The river folk |
|---|---|---|---|---|
| defName | `Rimfolk_Pilgrims` | `Rimfolk_Scholars` | `Rimfolk_Enclave` | `Rimfolk_RiverFolk` |
| Calling | pilgrims | scholars | brokers | fishers |
| Age | Medieval (tribal parent) | Industrial (outlander parent) | Spacer (outlander parent) | Medieval (tribal parent) |
| Arises in (release B) | medieval to spacer | medieval to spacer | spacer | tribal to industrial |
| Look | capes dyed sage over plain clothes, wide cone hats (Ideology) | collar shirts and jackets dyed claret; the provost's cape and beret (Royalty) | collar shirts and jackets in pale cyan, a few bionics; the factor's ruffled shirt (Royalty) | kingfisher blue, knit caps, floppy hats (Ideology); the river master's cape |
| Weapons | clubs and knives; wardens' spears, short and recurve bows | revolvers; proctors' guns in flak vests | marshals in recon armour with charge rifles and the game's advanced guns | knives; river wardens' spears, recurve bows |
| Leader | keeper of the shrines | provost | chair of the exchange | river master |
| Town head, heir, envoy, trader | hosteller, under-hosteller, palmer, chandler | archivist, under-archivist, society fellow, bookseller | pad master, second pad master, enclave factor, factor | landing master, second at the landing, boatman, boat-trader |
| Messengers | palmers | copyists | runners | boatmen |
| Towns | few (0.5): a shrine and its hostel | few (0.45): archives | fewest (0.3): markets round a landing pad | landings (0.7) by a river |
| Caravans | a chandler's string, 0.6 as likely as another's | a bookseller's caravan of books, schematics and techprints (Royalty) | 0.7 (its trade comes down by lander); components, plasteel, glitterworld medicine, implants, genepacks (Biotech) | 1.2; fish (Odyssey), timber, cloth, food |
| Raids | 0.2 | 0.2 | 0.15 | 0.3 |
| Faith (Ideology) | The Pilgrim's Way: proselytizer (and VE Memes' nomad); embodied, abstract or Buddhist origin; Buddhist style | The Open Book: individualist (and Alpha Memes' monastic); ideological or abstract; Islamic style | The Exchange: transhumanist (and VE Memes' trader); ideological or archist; techist style | The River's Keeping: nature primacy (and Alpha Memes' water primacy); animist or embodied; totemic style |
| Kinds (Biotech) | hussar, genie, neanderthal, impid, pigskin (a few each) | genie, highmate | hussar, genie, highmate, starjack (Odyssey) | neanderthal, pigskin, yttakin |
| Colours | sage | claret | pale cyan | kingfisher blue |

**Their towns' maps** (`Rimfolk_TownPlans_PeoplesC.xml`): the pilgrims' **shrine and hostel** (31 by 21): a flagstone
court ringed by columns, flower beds and the shrine's flame between two lamps, then the hostel: a dormitory of twelve
bedrolls, a refectory of four tables, the hosteller's room, a storehouse. The scholars' **archive** (33 by 21): a reading
hall with nine bookcases and three pairs of reading tables, the stacks (the store) between rows of small bookcases, a
yard with a fire, the fellows' house, and the scriptorium with its research bench. The enclave's **landing-pad market**
(35 by 23): a concrete pad in a ring of paved tiles with its centre marked, a paved square of ten stalls round the
orbital trade beacon, the factors' house with a comms console, a storehouse of shelves. The river folk's **landing** (33
by 22): three houses and the landing master's hall, a smokehouse, an open shed of fish racks, a boat shed on posts, a
yard fire and willows, and docks four cells deep along its south and east sides. A new plan mark, `dock`: a dock cell is
neither a building nor a floor in the plan; the landing's spot is looked for wider (nine rings of five cells round the
middle), the rest of the plan held to the usual 6% of bad ground, and taken where most docks are water
(`PeoplesMath.DockSpot`, the nearest the middle on a tie); `PeopleTowns.LayDocks` lays the game's bridge over water a
bridge can stand in and planks on dry ground, and leaves deep water as it is. With no water in reach of the map's
middle, the landing stands on dry ground with a landing of planks.

**A river where the world has one** (`PeopleExtension.byWater`): as a new game starts (`ArisingBook.StartedNewGame`,
`PeopleTowns.ToTheRivers`), each landing the game put where no river runs moves to a tile a river runs through within
twelve tiles, by `Arising.TryTownTile` (valid for a town, clear of the colony's homes, on no other people's land); when
none will take it (a world with no rivers near, or rivers switched off), it stays where it is, a landing on a lake or a
stream too small for the world map, and its map is laid as above. A river folk people that arises (or the dev tools'
people of a template) is made on a river tile; with none in reach the cause takes its own ways instead.

**Theirs at the gate** (`Peoples/PeoplesCallers.cs`; `Rimfolk_Events_PeoplesC.xml`):

- *A night's shelter* (`HappeningWorker_PilgrimShelter`, from four in the afternoon to ten at night): a band of three to
  five pilgrims asks for a night's shelter. Taken in, each sleeps the night in a warm corner indoors
  (`VisitorServices.Lodge`) while the colonist who answered sits up with their eldest; at first light (a memo of the
  living events, `LivingEvents.Shelter`) the band goes on, the eldest says a blessing over the gate, the colony is glad
  of it (`Rimfolk_Evt_ShelteredPilgrims`, a feeling of fellowship), and one night in ten (`PeoplesMath.CoughChance`,
  through the harm setting's cap and scale) the one who sat up with them has caught a pilgrim's cough (the everyday head
  cold), which they say and the morning's word tells. Let stay outside the gate, they go on in the morning with thanks.
  Or the gate stays shut.
- *The society's copyist* (`HappeningWorker_ScholarCopyist`, a data event of the setting's kind, only where the colony
  gathers somewhere): a copyist asks to copy the colony's records (a tribal colony's stories, written down) with the
  colony's best reader to explain them; they leave a book of the archive's (a textbook for a tribal or medieval colony,
  a schematic after) by `DilemmaEffect_Goods`, or teach the reader instead (`DilemmaEffect_Lesson`); the colony is proud
  its story is kept (`Rimfolk_Evt_RecordsKept`).
- *A boat-trader's catch* (`HappeningWorker_RiverCatch`, with Odyssey's fish: the map's own fresh-water kind first): a
  basket of fish at the gate (more for a richer colony), for a silver apiece or food from the stores, carried out and
  handed over.
- *The enclave's lander* (`HappeningWorker_EnclaveLander`, an industrial or spacer colony only, by radio or the comms, or
  the colony's trade beacon): bidden, `DilemmaEffect_EnclaveLanding` drops the factor with one of the enclave's caravan
  stocks in its own pack, two to four marshals by the colony's wealth and any beasts for sale, by drop pod at the colony's
  trade drop spot; they trade from the factor's pack under the game's visitors' lord (`LordJob_VisitColony`, as the
  game's visiting traders do: a caravan's lord sells only from pack beasts) for ten to fourteen hours, then walk off. A
  tribal or medieval colony meets the enclave's caravans on foot instead, as it meets the Empire's.

**Their asks** (`Rimfolk_Missions_PeoplesC.xml`, deliveries, `MissionPeople.only`): the fellowship's hostel asks for
food in a holy year (`whileKind` `faith`: a great pilgrimage, a holy year or a prophet on the roads running), the river
folk for timber for the boats in the spring and cloth for the nets in the summer.

**By patch** (`Patches/Rimfolk_PeoplesC.xml`, conditional): world events that prefer a calling, the great pilgrimage,
the holy year and the prophet on the roads the pilgrims; the old vault opened and the comet the scholars; busy orbital
lanes and a ship come down the enclave; the flood downriver and the ice road the river folk. Visitors whose own worker
picks their people now read `HappeningExtension.prefers` (`Peoples.Weigh`): hungry pilgrims, pilgrims to the relic and
pilgrims to the anima tree the fellowship; a travelling scholar and a book swapper the society. Camps they favour: the
pilgrims, the procession, the penitents and the mourners (the fellowship); the scholars, the surveyors, the teacher and
the monolith's watchers (the society); far merchants, stranded spacers, salvagers and market folk (the enclave);
fishers, woodcutters and harvest hands (the river folk).

**Release B's peoples that arise may take their ways.** Each template's span of ages (`PeopleExtension.ariseAges`,
`PeoplesMath.AgeSpan`) and a cause's lean (`ArisingMath.Leans`, `Lean`, `Wanted`): the cause's own callings as before
(a league of the spacer age now an enclave of brokers, faithful who broke away in an industrial or spacer world
pilgrims), and two leans: survivors near a river, on their fated half (`SaltRiver`), river folk in any age; a band
whose leader is learned (Intellectual 10 or more) scholars, but among the tribes, and else near a river river folk. A
river folk lean is only kept when a river tile in reach will take their town (`Arising.TileFor`); otherwise the cause
takes its own ways. Their words: how they live (`CC_Arise_refugees_fishers`, `_exiles_fishers`, `_exiles_scholars`,
`_splinter_pilgrims`), the league's opening as an enclave (`CC_Arise_league_brokers`), their names. Release B's pace,
causes and caps are unchanged.

**Saved**: nothing new. The pilgrims' morning is a memo of the living events, an existing collection; the lander's
party and the lodgers are the game's own lords and Rimfolk's existing visitor services.

**Softness**

| Missing | What happens |
|---|---|
| Ideology | No fixed faiths or styles; no cone or floppy hats. |
| Royalty | No techprints in the society's and the enclave's stock; no beret for the provost, no ruffled shirt for the factor. |
| Biotech | Humans only; no genepacks with the enclave. |
| Odyssey | No fish: none in the river folk's stock or racks, and the boat-trader's catch never comes; no starjacks with the enclave. |
| Anomaly | No tomes in the society's stock. |
| VE Memes | The fellowship's and the enclave's faiths have only the game's meme. |
| Alpha Memes | The society's and the river folk's faiths have only the game's meme. |
| Hospitality | Nothing changes: a caller of a calling is never one of its guests standing in. |
| A world with no river near a landing | The landing stays where the game put it, on dry ground with its planks. |
| A colony with no place it gathers | No copyist (they work where the colony gathers). |
| A tribal or medieval colony | No lander: the enclave's caravans come on foot. |
| The deeper game hooks (setting) | The game's own bases for their towns, and their caravans at the game's even chance. |
| Ailments (setting) or the harm setting at 0 | No pilgrim's cough. |

Not used: Royalty's shuttle for the lander (its leaving by shuttle couldn't be tried here; drop pods are the base
game's), VE Fishing's and other mods' fish (their defNames couldn't be checked; Odyssey's are read from the biome),
Vanilla Races Expanded's xenotypes, and a ferry on the colony's journeys (the world map's crossings would need a hook
into the game's caravan paths, too much to finish well).

## Their gear, their kinds of people and their gifts

The player's report, 7 November 2026: "could it be that the new factions pawns all wear just one single outfit per
faction? ... the factions should try to draw from all kinds of mod and vanilla references for items, weapons, gear,
clothing & armor, magic and abilities etc."; then "if there is an army faction or so, it makes sense if people have more
or less the same uniform. it should make sense for each faction, but have variety whenever possible"; and "these include
different xenotypes not just baseliners, right?", "again, always draw from the full mod pool". Every kind of the seven
peoples was reworked (and so release B's peoples that arise, which use the same kinds). Pawns already in a game keep
what they have; only pawns made from now on are dressed this way.

**How it is done.** No kind has `apparelColor` any more. Each people's base kind dyes its signature pieces by name
(`specificApparelRequirements` with an `apparelDef` and a `color`, which colour that piece whenever it is worn and make
nothing); a civilian kind picks its signature garment by `alternateTagChoices` (a robe or a mantle, a coat of any
`IndustrialAdvanced` cut on the shell layer over the torso, where the colour goes with it). Everything else comes from
wider `apparelTags`, weapon tags and money by role, the game's own way. The game's tags are the base: the mods add their
gear to them (Vanilla Weapons Expanded's guns to `Gun`, its bows to `NeolithicRangedDecent`, Vanilla Apparel Expanded's
clothes to `IndustrialBasic`), and a mod's own tags and pieces are named only with `MayRequire` on that mod. Medieval and
tribal peoples are kept off `IndustrialBasic`, which Vanilla Apparel Expanded fills with t-shirts, hoodies and baseball
caps; their plain shirts and trousers are required instead, under the habit or the cloak. What they carry is
`inventoryOptions` (a chance of one thing of their calling); traders carry nothing of their own, since a lander's or a
visitor's trader sells from its own pack. Every name was looked up in its mod's own 1.6 defs and is listed in
`Tools/Tests/PeopleGear.txt` with where it comes from.

**How alike each role is** (the player's rule: soldiers in a common kit, a religious habit shared but not its cut or age,
everyone else varying freely around the signature):

| People | Signature | Soldiers' kit | Religious and civilians |
|---|---|---|---|
| Hospitaller order | ivory on the habit (robe, mantle with Medieval 2, the prior's royal robe with Royalty), the guards' plate and tabard | sworn guards: ivory plate, helmet, with Medieval 2 the ivory tabard and a heater shield; their own only what is under the plate, gloves, boots, a banner. Lay archers (its militia): helmet, ivory cape, quiver | the habit shared, its cut, age and cloth free; hood, cap, plague doctor's mask or none |
| Herding folk | russet on caps and wraps (tail cap, broadwrap, war veil, headdress) and the coat over the hides | herd wardens: war veil, Tribals' war wraps | hides, kilts, ponchos and pelt coats of every make |
| Road league | teal on the coat (any cut) and the duster | caravan guards: teal duster, cowboy hat, flak vest; outriders: the same with flak trousers, grenade belts | coat of any cut, the rest travelling clothes and hats |
| Pilgrim fellowship | sage on the travelling cloak (cape, robe, mantle) | wardens of the way: helmet, sage cape, padding; archers: sage cape, quiver | cloak, cone hat, packs and banners, plain clothes of any colour |
| Learned society | claret on the coat or gown and the proctors' jacket | proctors: flak vest under the claret jacket, a military cap (Hats and Rags) | gown or coat of any cut over a collar shirt |
| Spacer enclave | pale cyan on the coat and the marshals' armour | marshals: one issue of recon armour and helmet in pale cyan over Rimsenal's spacer bodysuit, a charge weapon | coat of any cut, industrial clothes, tool belts and packs, the chair's psychic finery |
| River folk | kingfisher blue on hats (floppy hat, tuque) and the coat | river wardens: blue tuque, plain clothes, padding | hides and plain clothes, slings and nets |

**What they draw on**, each only when loaded: Vanilla Apparel Expanded (ponchos, kilts, pelt coats, suit jackets and lab
coats, boots and scarves), its Accessories (quivers, packs, tool belts, medic bags, banners, the ranged shield belt),
Vanilla Armour Expanded (gambesons, quilted vests, mail, light plate), Vanilla Factions Expanded - Medieval 2 (mantles,
tabards, chaperons, caps, the plague mask, padded armour, heater shields; war bows, pikes, flails), Tribals (war wraps),
Settlers (bandanas, sombreros, vests); Vanilla Weapons Expanded and its Frontier (the league's revolvers and rifles),
Non-Lethal (the proctors' tasers and stun batons), Grenades (the outriders' belts), Tribal (slings, clubs), Coilguns and
Laser through the game's own tags; since the check against the player's own mods (below) also Vanilla Ideology Expanded -
Hats and Rags, Melee Animation's lassos, Rimsenal's unisuits, data glasses, bodysuits and smart rifles, Classical's
javelins, and Rimsenal's and Advanced Armaments' other guns under the game's tags. The base game alone dresses and arms every kind: robes, capes,
hoods, plate and helmets, tribalwear and parkas, dusters, jackets and flak, recon armour; knives, clubs, maces, swords,
spears and bows, revolvers, the game's guns and charge rifles.

**What they carry**: the order's herbs and clean cloth (now and then a kit of industrial medicine), the herders' hay or
hides, the league's beer, cloth or a little silver, the pilgrims' book of the road or herbs, the scholars' textbooks,
novels and schematics, the enclave's components, chemfuel or medicine, the river folk's net cloth, herbs or timber; with
the player's mods also salves, candles, butter, fish sauce, a medicinal quill, wine, maps, wool and river fish.

**Their kinds of people** (`xenotypeSet`, its parent's left out; each entry `MayRequire` Biotech and its mod; about three
in four baseliners with every mod, more without):

| People | The mix (in hundredths) | Why |
|---|---|---|
| Hospitaller order (24) | genie 6, highmate 4, pigskin 2, neanderthal 2; lowmate 3 (VRE Highmate), drakonori 2 (Alpha Genes), high elf 3 (Elves), orangutan 2 (Awakened Apes) | thinkers and gentle carers, and those the order nursed through |
| Herding folk (31) | yttakin 6, neanderthal 4, pigskin 3; animusen 4 (Alpha Genes), boarskin 3 (VRE Pigskin), venator 3, wood elf 4, saurid 2 (VRE Saurid), gorilla 2 (Awakened Apes) | hardy, furred, at home with beasts and on the open grass |
| Road league (31) | hussar 4, genie 4, highmate 3, dirtmole 2, neanderthal 2, starjack 2 (Odyssey); bogleg 5, stoneborn 3, uhlan 2 (VRE Hussar), android 2 (VRE Android), bonobo 2 (Awakened Apes) | the road brings every kind: boglegs' silver tongues, stoneborn craftsfolk, guards, bonobos at the bargaining table |
| Pilgrim fellowship (27) | hussar, genie, neanderthal, impid, pigskin, yttakin, highmate 2 each; animakin 3, gauranlenkin 2 (VRE Phytokin), avaloi 2, forsaken 2 (Alpha Genes), wood elf 2, gorilla 2 (Awakened Apes) | pilgrims from everywhere, the tree folk drawn to holy places |
| Learned society (26) | genie 10, highmate 3; high elf 4, avaloi 3, forsaken 2, archon 2 (VRE Archon), orangutan 2 (Awakened Apes) | the learned, the psychically gifted, the last of an old empire's lines |
| Spacer enclave (31) | hussar 3, genie 5, highmate 5, starjack 4; archon 3, avaloi 3, android 3, lapis 2 (Alpha Genes), Åskbarn 2 and Uredd 1 (Rimsenal Askbarn) | transhumanist spacers: the made and the improved |
| River folk (22) | neanderthal 3, pigskin 2, yttakin 2; coastal merren 5 and abyssal merren 1 (Stagz Merfolk), nereid 4 (Alpha Genes), poluxkin 3, gauranlenkin 2 (VRE Phytokin) | people of the water, and plant folk who keep it clean |

The player's race mods are all xenotypes (Elves Continued, Stagz Merfolk, the Stoneborn, Avaloi, Boglegs, Venators,
Awakened Apes, Rimsenal Askbarn, Fairycore, Korg, Paws of Interest; the catalogue's only humanlike race besides the
human is A RimWorld of Magic's spirit), so no extra pawn kinds were needed; Vanilla Genetics Expanded has no people.
Fairycore's fairies were left out (exclusively female, and a pawn kind can't keep its men from the share).

**Their gifts** (`PeopleKindExtension` on a kind: a list of gifts, each a share of the kind; at most one a pawn, the
shares together never more than 60%; `Peoples/PeopleGifts.cs`, a postfix on the game's `GenerateNewPawnInternal`, so
only freshly made pawns of these kinds, with the deeper game hooks on; `PeoplesGearMath.cs` holds the pure rules):

- A RimWorld of Magic's classes, by the trait's name, only the classes its components know as a mage's or a fighter's
  (`PeoplesGearMath.MagicClasses`), only when switched on in its settings and when its own share for that people
  (`FactionMageSettings`, `FactionFighterSettings`) isn't 0, never for a pawn that rolled a class of its own (its classes
  conflict); the mod's components keep whether a pawn is a caster once they look, so their flags are set back by name.
  Hospitallers priest 15%, their prior 35%, sworn guards paladin 8%; herders druid 3%, archers ranger 8%, the elder druid
  30%; league guards gladiator 4% and sharpshooter 3%, outriders sharpshooter 8%, the master commander 20%; pilgrims
  wanderer 4% and minstrel 3%, wardens paladin 10%, archers ranger 6%, the keeper priest 30%; fellows arcanist 3% and
  enchanter 2%, scholars arcanist 5% and chronomancer 3%, the provost arcanist 12% and chronomancer 8%; enclave hands
  technomancer 4%, marshals psionic 5%; river ferry hands druid 3%, wardens ranger 5%, archers ranger 8%, the river master
  druid 30%. Since the check against the player's own mods: lay archers ranger 4%, herd wardens gladiator 4%, league
  wayfarers wayfarer 3% and minstrel 2%, pilgrims monk 3%. Reviewed against each people's lore on 12 November 2026
  (DESIGN.md "Magic among the peoples, seen"; `PeoplesMagicMath.ShareCap`): sparing everywhere, a kind's classes at most
  6% and a people's head 15%, but among the four peoples built on magic (the Conclave, the Mossborn, the Choir and the
  Bell) 15% and 40%; so the hospitallers' priests are 6% and the prior 15%, the elder and master druids 15%, the guards'
  and archers' classes 6% together. The defs hold the shares; a test checks each kind against its cap.
- A psylink (Royalty), the game's own way with no letter (each level a psycast of its level); with Vanilla Psycasts
  Expanded, whose psylinks give points instead, one of the kind's paths it can follow and a few of its powers in order,
  as that mod's own pawn generation does (read by name). Scholars 5% (1 to 2 levels, chronopath or empath), the provost
  20% (1 to 3); enclave hands 3% (1, skipmaster or empath), marshals 6% (1 to 2, protector or warlord), factors 20% (1 to
  2, skipmaster, empath or harmonist), the chair 45% (2 to 4, harmonist, empath or skipmaster). None for the medieval and
  tribal peoples, whose magic is A RimWorld of Magic's.
- Genes put in (Biotech), for the peoples whose age has the means (`PeoplesGearMath.GenesFit`: industrial and later): a
  baseliner of no custom xenotype given them as xenogenes by name, none that conflicts with a gene it has (the other kinds
  of people keep their genome whole). Scholars quick study 4%; enclave hands strong immunity 4%, marshals fast wound
  healing 4%, factors attractive 4%, the chair Alpha Genes' extraordinary lifespan 10%.

No letter, nothing saved, nothing on a clock. Dev actions (Debug actions, Rimfolk): "Peoples: their gear, kinds of
people and gifts (log)..." (four new pawns of each kind of a people, described in the log and not kept) and "Peoples: one
of each of their kinds here now, as visitors (testing)..." (spawned at the mouse under a visitors' lord for half a day).
Tests: `Tools/Tests/PeoplesGearTests.cs`.

**Softness**

| Missing | What happens |
|---|---|
| Every mod | The base game's gear under the same tags and required pieces; the signature colours on the base game's robes, capes, plate, dusters, jackets, parkas, tuques and recon armour. |
| Ideology | No tail caps, broadwraps, cone or floppy hats. |
| Royalty | No royal robes, top hat, beret, ruffled shirt, psychic finery; no psylinks. |
| Biotech | Humans only. |
| A race or xenotype mod | Its share goes to baseliners. |
| A RimWorld of Magic | No classes (that share has no gift). A class it has switched off, or a people it keeps from classes, gets none. |
| Vanilla Psycasts Expanded | Psylinks with the game's own random psycasts. |
| Vanilla Factions Expanded - Medieval 2 | No mantles, tabards, heater shields, caps, chaperons, plague masks, war bows or pikes. |
| The VE apparel, armour, accessories and weapons sets | Their pieces and weapons are missing from the choices; nobody goes without. |
| Vanilla Ideology Expanded - Hats and Rags | No beads, mitres, skullcaps, wide hats, fezzes or military caps: no prior wears a mitre and the keeper wears the cone hat, the proctors their own hats. |
| Rimsenal, Melee Animation, Alpha Crafts, Alpha Animals, Awakened Apes | No unisuits, data glasses, spacer bodysuits (the marshals' collar shirt instead) or smart rifles; no lassos; no salves, candles, butter or fish sauce; no apes. |
| The deeper game hooks (setting) | No gifts; the gear is unchanged (it is the defs'). |

Not used: Vanilla Apparel
Expanded's spectacles (never generated: their commonality is 0), Vanilla Expanded Framework's shield generation by tag
(the order's guards are given their shield as part of the kit instead), Vanilla Races Expanded's sanguophages, lycans and
insectors (not for peaceful folk), Alpha Genes' wretches, helixans, hivelings and mind devourers.

**Checked against the player's own mods (7 November 2026).** The player: "full mod list to be considered everywhere, and
now first for the faction gear, items, clothing, abilities etc." The first pass was read from the mods' public 1.6
sources; this one from the player's own files, the catalogue `docs/player-mods/` (its active rows, the mods in their list
and save). The catalogue itself was wrong first: `Tools/mod_pool.py` read every version folder of a mod, so 1.4 and 1.5
defs stood for 1.6 ones (Vanilla Weapons Expanded's bows and guns with their old tags, Rimsenal's retired guns); it now
reads only what 1.7 loads (a mod's `LoadFolders.xml` for 1.7 with its `IfModActive`, else root, `Common` and the newest
version folder), in load order, an inactive mod's def never standing for an active one's, and it records each piece's
`generateCommonality`. Every name the peoples used was in the player's active mods; what was wrong was what the game
picks with them:
- Vanilla Psycasts Expanded's eltex cape carries the game's `Cape` tag and is as likely as the plain cape in a tag
  choice (which ignores a kind's money), so a quarter of the pilgrims, their chandlers and keeper and the scholars'
  provost would have worn an 800-silver psychic cape without the people's colour. The plain cape has Rimfolk's own tag,
  `Rimfolk_Cape`, and they choose that.
- Royalty's `RoyalTier2` (the league master's and the enclave factor's finery) is also Rimsenal's close combat armour,
  Vanilla Armour Expanded's marine boots and gloves, and A RimWorld of Magic's and the elves' robes and cloaks (rare by the
  game's weights, about one piece in a hundred): they take `Rimfolk_Finery` instead, on the beret, the ladies' and top
  hats, the formal shirt, the royal vest and the corset.
- Vanilla Apparel Expanded - Accessories' `VAEA_Utility_Industrial` gave enclave hands explosive belts, mini turret
  packs and battle banners, and its `VAEA_Utility_Neolithic` gave pilgrims quivers: a hand (and now a league wayfarer)
  keeps out the soldiers' belts, packs and banners, a pilgrim the quiver (`apparelDisallowTags`).
- `Patches/Rimfolk_PeoplesGear.xml` puts Rimfolk's tags on those pieces and on Vanilla Ideology Expanded - Hats and Rags'
  hats and beads, which carry no tag at all and so were never worn by anyone the game makes; a patch whose piece is
  missing does nothing.
What was added, from the player's active mods (every age and calling, soldiers' kits still one kit): Hats and Rags'
prayer beads (the order, the pilgrims), a bishop's mitre for the keeper of the shrines and now and then a prior, skullcaps for hospitallers and
scholars, headscarves (and Ideology's headwrap) for lay servants and way archers, the wide hat of the road for way
archers and chandlers, the fez for league wayfarers and factors, a military cap in the proctors' kit; Melee Animation's
lassos for herders, drovers, the herd elder and the league's outriders; Accessories' ammo packs for the league's guards and
outriders, packs, tool belts and medic bags for wayfarers; Rimsenal's unisuits and data glasses for enclave hands and data
glasses for factors, its spacer bodysuit under the marshals' armour and its smart rifles in their hands (Rimsenal's and
Advanced Armaments' other guns already come in under the game's `Gun`, `SpacerGun` and `IndustrialGunAdvanced`);
Vanilla Factions Expanded - Classical's javelins for herd archers and river fishers, Medieval 2's arbalests for the lay
archers; to carry, Alpha Crafts' aloe salve, candles, butter and fish sauce, Alpha Animals' medicinal cactipine quill,
Medieval 2's wine, Odyssey's maps, muskox wool and river fish. "Variety Coats" in the player's list is Animal Variety
Coats, animal fur textures: nothing to wear. Tests: `PeoplesGearTests` loads the catalogue's tables and fails on any row
of `Tools/Tests/PeopleGear.txt` (now built from the catalogue) or any name of the peoples the active rows don't have, on
a signature choice that lands on another mod's piece (97% at least on the people's own, weighted as the game picks), and
on another calling's or age's armour among what a kind's tags give (half a hundredth at most).

## Their stories, sayings and touchpoints

The player's words on the eight new peoples of `docs/design/peoples-next.md` (7 November 2026): "love all these
factions, go ahead! also update existing factions to this level" (an explicit request: no proposal round). The seven
keep their callings, titles, towns, gear and events, and gain what the eight have: a founding story, sayings, what they
honour and fear, a fuller voice, a touchpoint of their own at the gate, a faith checked against the lore, and names for
peoples that arise in their ways that carry their signature. Code in `Source/ColonyCouncil/Peoples/`: `PeoplesLore.cs`
(their card, the new workers and the salvage's effect), `PeoplesLoreMath.cs` (pure, tested: `Tools/Tests/
PeoplesLoreTests.cs`, "peoples' lore: "); a camp's gift may now be a young beast and kept for a good stay
(`CampGift.young`, `maxTension` in `Camps/CampLookDefs.cs`, `CampLook.cs`). Defs: the founding in each faction's
description, `honours`, `fears` and `sayings` in its `PeopleExtension`, `Defs/IncidentDefs/Rimfolk_Events_PeoplesLore.xml`,
`Defs/CampDefs/Rimfolk_CampKinds_PeoplesLore.xml` (and the winter herd's gift in `Rimfolk_CampKinds_Peoples.xml`),
`Defs/FlavorPoolDefs/Rimfolk_Lines_PeoplesLore.xml`, `Defs/OutsiderTalkDefs/Rimfolk_OutsiderTalks_PeoplesLore.xml`,
`Defs/ThoughtDefs/Rimfolk_Thoughts_PeoplesLore.xml`, `Defs/Misc/Rimfolk_PeoplesLore_Misc.xml`, `Patches/
Rimfolk_PeoplesLore.xml`, Keyed `Rimfolk_PeoplesLore.xml`; the Almanac's two pages a section each, *Where their ways
began* (keywords unchanged).

| | Founding (the middle of their description) | Sayings | They honour | They fear |
|---|---|---|---|---|
| Road league | At Harrow Ford, where the road belonged to whoever had the most rifles, the carter Edda Vane asked every waymaster for one guard and one wagon and took them through under nine flags; nobody fired. The Waystone Book: fair dealers in good ink, robbers in red. | Every crate home. A word given at the waystone stands. Good ink or red ink. The road won't wait. | the word given at a waystone, a fair price and a caravan home whole | the robber, the broken word and a road nobody keeps |
| Hospitaller order | Aldith, a carter's daughter, nursed a fever town alone through a winter and in the spring hung its last lamp over the sickhouse door for anyone, raiders included; those she nursed took her vows. Every sickhouse keeps a lamp lit and the Book of the Spared. | We were spared, and so we owe. Nobody on a sickbed is anyone's enemy. Keep the lamp lit. A hand that heals is not raised first. | the spared, clean hands and the night watch | the fever's return and a door shut on the sick |
| Herding folk | In the year the wells failed, the herd girl Tamsa walked the last forty head through two hundred miles of dust to grass nobody believed in and brought every one home; the camps that followed her never stopped moving. Herders count every beast at dusk because she did. | Count them at dusk. Never leave a beast on the road. A guest eats from the same pot. The grass will tell. | the count at dusk, a guest at the fire and good grass | the dry year, the thief and a fence across the old drove roads |
| Pilgrim fellowship | Old Brannoc, a nearly blind preacher turned out for praying at the wrong shrine, walked with a boy to every holy place he had heard of and blessed every door at dusk, open or shut. A shell for every shrine reached; the names of the doors that opened. | Peace on this house. Every door is a shrine to someone. Walk on, and bless what you pass. There is no last shrine. | the open door, the footsore and the old shrines | the shut door, a shrine forgotten and sitting still too long |
| Learned society | The night the archive at Hesk burned, the clerk Ilse Varro carried out eleven books and a ledger in a handcart and copied them for the rest of her life. The night of the fire, argued until dawn every year; a scholar's first oath is to copy one thing that would be lost. | Nothing known should be lost. Copy it twice. Ask the question nobody will. Strictly speaking, that is two questions. | the copied page, a good question and a patient reader | fire, the lost book and the closed mind |
| Spacer enclave | An ore hauler came down with a dead drive; its purser Ines Okoro sold its cargo, passage and debts one bargain at a time until the crew owned a pad and the ships above called them. The hauler's manifest holds every bargain since. | A bargain struck is kept to the last digit. Everything has a price, and a fair one exists. Nobody is stranded who has something to sell. Terms first, then talk. | the kept bargain, the exact figure and the body made better | the default, the debt nobody pays and being stranded |
| River folk | The night the dam broke, the ferrywoman Wenna Reed's boats pulled people off the rooftops for three days; those they carried stayed. A knot in the net for everyone pulled from the water; on its night any stranger is carried across for nothing. | Take what the water gives and no more. Everything comes down to the river in the end. Carry the stranger across. Never foul the water another drinks. | clean water, the stranger carried across and the catch shared | fouled water, a cheat at the ferry and the river rising |

**Their card** (`PeopleLore`, a section of the people's card in its story slot): "They honour ..., and they fear ...", and
one of their sayings, the same one each time for a people (`PeoplesLoreMath.Saying` by its id). A people that arose in
their ways (release B) shows its template's, as it keeps their ways. A founding is told everywhere as the story of their
ways (the first prior, the first herd girl), so a people that took those ways can tell it too.

**Their voices** (`Rimfolk_Lines_PeoplesLore.xml`): each people's letters now also grieve with the colony and thank it for
a warning (`condolences`, `warning` in `Rimfolk_Pool_NeighbourWrites`), two or three more messengers' lines, their visitors'
words at every moment of a visit (`Rimfolk_VisitorLines`: arrive, wait, stay, pass, leave), their words face to face
(`Rimfolk_Pool_OutsiderSays`: a chat, a warm parting, their faith), three new pieces of talk each (how their ways began,
what they honour, what they fear: `Rimfolk_OutsiderTalks_PeoplesLore.xml`), and the society's night of the fire's words
(`Rimfolk_Pool_PlayDisputation`, checked in ImmersionTests). Their faction descriptions and creeds tell their founding; the
speech is plain and in each one's own manner (the order patient, the herders few-worded, the league in prices and ink,
the pilgrims in blessings, the society precise, the enclave dry and exact, the river folk easy).

**Their touchpoints** (each only from a people of its calling at peace with the colony; every age by the colony's means):

| People | Touchpoint | What is seen on the map |
|---|---|---|
| Hospitaller order | *The order's night nurse* (`Rimfolk_Evt_OrderNurse`, `HappeningWorker_OrderNurse`): a colonist is down with a fever (an illness the body fights off, not an everyday ailment) | A hospitaller (their healing kind, Medicine 10 to 16) walks to the gate; let in, they go from bed to bed (`VisitorServices.Dose`) to the fevered, four at most, the sickest first, giving the order's care (`Rimfolk_OrderNursed`: immunity gained a fifth faster for a day and a half) and stay the night; the colony's doctor may follow at their side learning (a lesson), and two doses of medicine go back with them for the sickhouse. Or sent back down the road. |
| Herding folk | *A young beast at the gate* (the winter herd camp): after a winter's grazing that went well (the camp's unease under 40) | The camp elder walks to the colony's gate as they go and hands over a young beast of the herd's own kind, the colony's (a farewell walk; `CampGift.young`), told in the farewell. After a bad stay, no walk. |
| Road league | *Winter quarters* (`Rimfolk_Camp_LeagueWinter`, winter only, `calling` traders) | A caravan out of the snow camps for six to ten days with its pack beasts, crates, beer and a trading table, trading all the while (the game's own trading); asked, its wardens stand with the colony if raiders come (`DilemmaEffect_CampGuards`) and the colony is written in the Waystone Book (a visit at their table); an evening at their fire; after a good stay the caravan master leaves a carved waystone in its crate at the gate. |
| Pilgrim fellowship | *The Long Walk* (`Rimfolk_Evt_LongWalk`, spring and summer) | A file of six to nine pilgrims comes to pray at the colony's graves (or its gate without any); the colony may walk out to the graves with them or see them off to the road (a walk together), give them food for the road (carried out to them), or watch from the wall; the eldest says a blessing as the colony answers. |
| Learned society | *The night of the fire* (`Rimfolk_Evt_Disputation`, once a year at most) | A fellow comes where the colony gathers; that evening the colony gathers there with the fellow as its guest and argues over what was lost at Hesk (its own words over heads), the colony's best mind learning from it; or an hour at a table together. |
| Spacer enclave | *Salvage from above* (`Rimfolk_Evt_EnclaveSalvage`, any age) | The factor's offer by radio or comms, or by a runner on foot to a tribal or medieval colony: salvage worth 600 silver at the colony's usual wealth (0.6 to 2 times) for two in five of it; bought, drop pods come down by the trade spot with steel and what the age can use (cloth, components, medicine; plasteel and chemfuel; a spacer component), and the silver goes to the runner or a courier of theirs. |
| River folk | *Nets on the bank* (`Rimfolk_Evt_RiverNets`, not in winter, only with water near the colony) | Two or three boatmen settle on the dry bank by the water nearest the colony for the day; they leave fish of the colony's own waters (Odyssey) or net cloth, and the colony's best crafter may sit with them learning their knots (a lesson). |

Callers that prefer them, by patch (`Patches/Rimfolk_PeoplesLore.xml`, conditional; `HappeningSetting.prefers`), each with
a text in that people's voice: a herb-gatherer the order's, a neighbour's patrol after robbers the league's, a traveller on
the wrong road the pilgrims', a travelling teacher the society's, a tourist the enclave's. Nothing of these touchpoints
hurts anyone, so none draws on the harm budget; the pilgrims' cough is release C's, through it.

**Their faiths** (Ideology; every meme `MayRequire` its mod, no two sharing an exclusion tag by the memes' own 1.6 defs,
four at most): the order adds Vanilla Ideology Expanded's *light* (the lamp over every door); the herders its *elders*
(the elders heard before anyone); the league its *republic* (the waymasters together, as at Harrow Ford); the enclave its
*progressive* (no hands to spare since Okoro's crew); the river folk its *egalitarian* (whoever is in the water pulled out
first). The pilgrims may take its *omnism* as their structure (every people's shrines), the society its *agnosticism*
(what cannot be known said plainly). Each creed's description adds a line of its founding.

**Peoples that arise in their ways** (release B): a third form of every name, carrying the ways they took (*Lamp of
Ashford*, *Ashford Waystone*, *Last Herd of Ashford*, *Ashford High Bank*, *Flags of Brightwater*, *Brightwater Long
Grass*, *Brightwater Manifest*, *Barr's Herd*, *Barr's Waystone*, *Barr's Landing*, *Barr's Handcart*), a second form of
how they live for the survivors of every calling, the learned band, the pilgrims who broke away and the enclave's league
(the herders' count at dusk, the sickhouse's lamps at its gate, good ink, pulling whoever is in the water out first, a
shell for every shrine, copying what would be lost, keeping a bargain to the last digit), each what their town and people
show. The templates' own name rules gain their founding's words (the Order of the Spared, the Forty Head, the League of
the Nine Flags, the Shell, the Handcart, the Manifest, the Knotted Net); the order's *Bell* became *Ward*, so no people of
it is ever named the Quiet Bell.

**Saved**: nothing new. The hospitaller's rounds are the visitors' service's existing tasks; the young beast and the
waystone are the camps' existing farewell walk and gifts.

**Softness**

| Missing | What happens |
|---|---|
| Ideology | No faiths; the rest as before. |
| Vanilla Ideology Expanded - Memes | Their faiths have only their earlier memes; no omnism or agnosticism. |
| Odyssey | Net cloth instead of fish on the bank. |
| A colony with no fever | No night nurse. |
| A colony with no graves | The Long Walk stops at the gate, and the graves' answer is greyed out. |
| No water near the colony | No nets on the bank. |
| Answers play out on the map (setting) off | The order's care is given at once; the walks, the gathering and the lessons are the play-outs' own fallbacks. |
| Camps (setting) off | No winter quarters and no young beast. |

## What was built (release D: the Lantern Wardens, the Quiet Bell, the Circuit of Tamers and the Wheel)

The first four of the eight peoples of `docs/design/peoples-next.md` (the player's words, 7 November 2026: "love all
these factions, go ahead!"; an explicit request, no proposal round), each at the level the seven were brought to: a
founding story, sayings, what they honour and fear, their own titles, messengers, gear by role, faith, town plan, voice,
touchpoints at the gate that play out on the map, an ask, a patch on the callers that should come from them, names for
peoples that arise in their ways and an Almanac page. Built only from the player's active mods (`docs/player-mods/`, the
rows with `active` yes; every def name and tag checked there), always softly, with the base game's fallback. Code in
`Source/ColonyCouncil/Peoples/`: `PeoplesD.cs` (helpers, the Wardens, the Bell), `PeoplesD2.cs` (the Circuit, the
Wheel), `WheelTowns.cs` (the Wheel's towns that move), `PeoplesDMath.cs` (pure, linked into the tests:
`Tools/Tests/PeoplesDTests.cs`, "peoples, release D: "); `PeopleKindExtension.ageArms` and `PeopleGift.ages`
(`PeopleDefs.cs`, `PeopleGifts.cs`: `AgeArms`), `Peoples.AgeOf`. Defs: `Defs/PeopleDefs/Rimfolk_People_Wardens.xml`,
`_QuietBell.xml`, `_Tamers.xml`, `_Wheel.xml`, `Rimfolk_TownPlans_PeoplesD.xml`; `Defs/IncidentDefs/Rimfolk_Events_PeoplesD.xml`,
`Defs/MissionDefs/Rimfolk_Missions_PeoplesD.xml`, `Defs/FlavorPoolDefs/Rimfolk_Lines_PeoplesD.xml`,
`Defs/OutsiderTalkDefs/Rimfolk_OutsiderTalks_PeoplesD.xml`, `Defs/ThoughtDefs/Rimfolk_Thoughts_PeoplesD.xml`,
`Defs/Misc/Rimfolk_PeoplesD_Misc.xml` (the jobs), `Patches/Rimfolk_PeoplesD.xml`, Keyed `Rimfolk_PeoplesD.xml`; the
Almanac's page *wardens, the Bell, the Circuit and the Wheel*.

| | The Lantern Wardens | The Quiet Bell | The Circuit of Tamers | The Wheel |
|---|---|---|---|---|
| defName | `Rimfolk_Wardens` | `Rimfolk_QuietBell` | `Rimfolk_Tamers` | `Rimfolk_Wheel` |
| Calling | hunters | sextons | tamers | salvagers |
| Age | Medieval (tribal parent) | Medieval (tribal parent) | Industrial (outlander parent) | Industrial (outlander parent) |
| Arises in (release B) | every age | every age | every age | every age |
| Founding | Tobin Garrow, a shepherd whose village was emptied in one night, hung his dead brother's lantern at the crossroads and kept it lit eleven years, until the beast was dead | after the long war nobody would bury the raiders; the widow Ysolde Marr buried them one by one and rang a cracked chapel bell over every grave | the herder girl Nessa Pell found an egg in a falling star's crater and walked the region with what hatched for forty years, challenging every keeper of beasts | the night the valley burned, the wheelwright Mab Wainwright put her house on her cart and drove out ahead of the fire; nobody has voted to stop |
| Sayings | Where a lantern burns, someone was taken. Keep it lit until it is done. Every page of the book gets its ending. | Everyone is owed a bell. Ring once for friend and foe alike. Quiet now, you are home. | A bond is not a leash. To be beaten fairly is an honour. Every egg is a road not walked yet. | Nothing is broken, only waiting. Mend it tonight, we leave tomorrow. Buy it cheap, sell it better. |
| They honour / fear | the lantern kept lit, the book's last line and a steady hand / the beast that gets away, a dark crossroads, the empty page | a grave for everyone, the bell rung in time, quiet ground / the unburied dead, a field that stirs, the bell left silent | the bond freely given, a fair bout, the stranger who beats them / the leash, a beast ill-used, a bout won by a trick | a thing well mended, the road ahead, a fair swap / standing still, the fire behind them, the vote to stop |
| Signature | a hood dyed lantern red (the game's hood) | the bell-keepers' grey habit and veil | a sash in the Circuit's violet (Biotech's sash; its bandolier where worn) | a coat of any cut in old brass |
| Leader, head, heir, envoy, trader | lantern-master, keeper of the book, under-keeper, tracker, pelt-buyer | first bell, sexton, under-sexton, gravewarden, tollkeeper | circuit-holder, ring-keeper, second of the ring, roaming tamer, egg-trader | wheelwright, yard boss, second boss, fixer, scrap-runner |
| Messengers | lantern-runners | bell-carriers | beast-riders | scrap-runners |
| Towns | 0.5: palisaded lodges | 0.4: grave-gardens | 0.5: rings | 0.35: yards, which move |
| Caravans | 0.8 | 0.5 | 1 | 2, the region's most |
| Raids | 0.3 | 0.15 | 0.25 | 0.3 |
| Faith (Ideology) | The Lit Crossroads: human primacy (and VE Memes' light); abstract, animist or ideological; rustic | The Quiet Ground: guilty (and VE Memes' exalted priesthood); abstract, Christian origin or ideological; morbid | The Freely Given Bond: animal personhood, individualist (and VE Memes' nomad); animist, embodied or ideological; animalist | The Turning Wheel: collectivist (and VE Memes' scrapper and nomad); ideological, abstract or archist; techist |
| Kinds (Biotech, a quarter or so) | Venators, yttakin, neanderthals, hussars, saurids, wood elves, animusen | dirtmoles, fungoids, stoneborn, forsaken, dark elves, neanderthals, genies | animusen, yttakin, saurids, animakin, Venators, wood elves, hussars, highmates, pigskins | wasters, dirtmoles, genies, hussars, starjacks (Odyssey), androids, stoneborn, boglegs, lapis |
| Colour | lantern red | undyed grey | violet | old brass |

**How alike they look, role by role** (the design's rule: uniform only where it makes sense). The Wardens wear no
uniform: wardens, trackers and the lantern-master in furs, tribal wear and Vanilla Apparel Expanded's utility pieces;
spear-wardens in a shirt and trousers under medieval armour of any make; the pelt-buyer in a collar shirt with a trader's
coat (Medieval 2); all under the red hood. Trackers carry bows, great bows and crossbows (Vanilla Weapons Expanded,
Medieval 2's arbalest), spear-wardens spears and pikes (Medieval 2), wardens a spear or a bow; they carry pemmican,
herbal medicine or a plain hide. The Bell's bell-keepers share one grey habit (hood, shirt and trousers dyed grey, the
cut free) and carry linen and herbs; sextons dress plainly in grey hoods with spades (Vanilla Weapons Expanded's
shovel) or clubs and carry linen and wood; the gravewardens are the Bell's one kit (a grey cape and simple helmet over
shirt and trousers, medieval armour, maces and hammers); the tollkeeper and the first bell wear a cape (with Medieval 2
its mantle). The Circuit's roaming tamers dress freely (western, outlander and industrial clothes) under the sash and
carry kibble, hay and herbs; rangers in the sash and trousers with guns; ring-wardens their one kit (jacket, simple
helmet, collar shirt and trousers); the egg-trader and the circuit-holder in collar shirts, the holder in a duster. The
Wheel's hands wear whatever the scrap gave around the brass coat, with tool belts and packs (Vanilla Apparel Expanded -
Accessories), wrenches and revolvers (Vanilla Weapons Expanded's tools, Makeshift's scrap guns) and carry components,
steel or chemfuel; yard guards their one kit (jacket, flak vest, trousers, the game's guns or scrap-built ones); the
mechanitor in Biotech's airwire headset; the fixer and the wheelwright in shirts and a duster.

**Gifts** (`PeopleKindExtension.gifts`, A RimWorld of Magic by its trait's name, psylinks with Vanilla Psycasts
Expanded's paths): the Wardens' rangers and snipers (the lantern-master a ranger one in four, and the only Warden with a
psylink: a Wildspeaker's path, 15%); the Bell's priests (the first bell one in three), a sexton geomancer now and then,
gravewarden paladins; the Circuit's druids and summoners, and in the spacer age only (`PeopleGift.ages` 3~3) a few
Wildspeakers (the holder 30%); the Wheel's technomancers and an enchanter, the wheelwright a technomancer one in five
and a commander. No gift over 35% of a kind.

**Every age** (the player's rule). A people of one of Rimfolk's templates lives in its template's age unless it arose
(release B) from something of another: survivors and faithful who broke away keep the age of the people they came
from, a band of the banished the colony's, within the span its template arises in (`Peoples.AgeOf`,
`PeoplesDMath.PeopleAge`). New: `PeopleKindExtension.ageArms`, a kit's weapon for other ages (`ages`, `weaponTags`,
`money`: the most it may cost, else the kind's budget and a quarter), given as a pawn is made (`AgeArms.Arm`, the
weapon's own stuff and quality): the Wardens' trackers take guns in the industrial and spacer ages, the Bell's
gravewardens molotovs and (Anomaly) an incinerator, the Circuit's rangers and ring-wardens bows and spears in the tribal
and medieval ages, the Wheel's yard guards spears and maces in the tribal and medieval ages and (Vanilla Weapons
Expanded - Laser) salvaged lasers in the spacer age. A set whose tags are all a missing mod's arms nobody. And
`PeopleGift.ages`: a gift only in those ages. Their touchpoints speak by the colony's means (`lowtech`,
`industrial|spacer` texts).

**Their towns' maps** (`Rimfolk_TownPlans_PeoplesD.xml`, laid like the others'). The Wardens' **lodge** (35 by 22): a
palisade, the trophy hall with its long tables, five beds, the store, a butcher's spot and a hide-scraper's spot
(crafting) by the fire, kennels with sleeping spots in a pen, torches at every way out. The Bell's **grave-garden** (35
by 24): eighteen graves on flagstones among rose beds inside the wall, the ossuary with four sarcophagi under the bell
tower's column, a quiet hostel of five beds, the store. The Circuit's **ring** (37 by 24): a ring of packed earth inside
a fence with six gates, torches round it, the hatchery's six animal boxes, four beds, the store, and stables (a pen of
146 cells) for every kind. The Wheel's **yard** (37 by 22): a ring of fences, the workshop (a fueled smithy, a machining
table, a tailor's bench), a bunkhouse of eight bedrolls, an open yard of scrap (the store), a small pen for the draught
beasts: laid out as if leaving tomorrow.

**The Wheel's towns move** (`WheelTowns.Daily`, from the region's day, the deeper game hooks on): each of the Wheel's
towns on its own day of every 30 (`PeoplesDMath.MoveDay`, by its id) moves two to five tiles by `Arising.TryTownTile`
(land a town can stand on, clear of the colony's homes and of other peoples' land), never while the colony has a map
of it, a caravan on or bound for its tile, or an ongoing quest that looks at it; the news (`CC_PeoplesD_WheelMoved`,
Flavor) with the town on the world map, and the region's towns refreshed. A town that can't move stays.

**Theirs at the gate** (`Rimfolk_Events_PeoplesD.xml`; each only from a people of that calling at peace with the
colony, each with three answers, the last always to send them on):

- *The lantern hunt* (`HappeningWorker_WardensHunt`): when a legend of the region, a beast nemesis or a pack's alpha is
  on the colony's land, wild and on its feet (the legend first: `PeoplesDMath.QuarryRank`), two or three Wardens come.
  Beside the colony's best shot (`DilemmaEffect_WardensHunt` `together`): the shot is given the game's hunt on the beast
  when they carry a hunting weapon, the Wardens go for it under the game's assault-things lord, the shot learns from
  them; or the Wardens alone. When it falls (`Happenings.Notify_Killed`), the Wardens gather at the carcass, one skins it
  where it lies for two hours (`JobDriver_PeoplesWork`, a progress bar), and the game's butchering of it is split: the
  leather into the Warden's pack for their book, the meat and the rest left there for the colony (`PeoplesD.Divide`;
  `CC_PeoplesD_Skinned`); then they go. If it leaves the land, or two days pass, they go home.
- *The pelt-buyer* (`HappeningWorker_PeltBuyer`): with twenty pelts or more in the colony's stores, a pelt-buyer and a
  tracker buy up to sixty at 1.2 times their worth (`PeltPrice`), carried out to them and paid in silver, the tracker
  teaching the colony's best hand with beasts to read a trail if asked (a lesson).
- *A lantern at the crossroads* (`HappeningWorker_WardensLantern`): within thirty days of a beast nemesis killing one of
  the colony, a Warden comes towards evening; lit with them, the lantern stands at the gate from dusk through the night,
  and those who were close to the dead stand its first hours there (a vigil); or the Warden alone stands by it until
  first light. If the beast is dead already, the Warden says the page has its ending.
- *A bell for the dead* (`HappeningWorker_BellForTheDead`, the undertakers' worker): after a fight with the enemy's dead
  still lying out, three or four bell-keepers bury them in graves beyond the wall or carry them off on their beast, the
  Bell's words said; either way `DilemmaEffect_BellForTheDead` tells up to three of the dead's peoples (not a permanent
  enemy), each gaining goodwill by how many of theirs were seen to (`GoodwillFor`: 6 for one, 10 at most), each with a
  message.
- *The Bell's funeral* (`HappeningWorker_BellFuneral`): for one of the colony who died within six days and lies in a
  grave, a bell-keeper waits by the grave; the colony gathers there in the evening (a vigil at the grave with the funeral's own words,
  `Rimfolk_Pool_PlayBellFuneral`), or one colonist stands a while with the bell-keeper.
- *The ground is listening* (`HappeningWorker_BellVoidSigns`, Anomaly only, the void awake and no big threat for eight
  days): towards evening an old bell-keeper warns at the gate; lamps at the colony's doors and a watch on the walls
  until first light (a patrol), or the bell-keeper alone at the gate with a lamp until first light.
- *A friendly bout* (`HappeningWorker_TamerBout`): a roaming tamer with their companion (with PokéWorld a Fighting type
  that is no legend, else a trainable beast of the land of half to two and a half a man's size, else a husky, a timber
  wolf or a retriever) challenges the
  colony's best hand with beasts. Taken (`DilemmaEffect_TamerBout`), the two meet at the gate and spar by the
  Fighting-type spar's rules (`JobDriver_TamerBout`: practice swings each way for 900 to 1500 ticks, melee practice,
  the companion held to the bout by PokéWorld's own stay job where it has one); the winner by the colonist's way with
  beasts and hands (`WinChance`, a fifth for someone green, four in five at most); beaten fairly, the tamer ties a token
  on the sash and hands over an egg of the Circuit's hatchery, and the colony is proud of it; one bout in 25 the
  companion's blow lands (a bruise of 3 to 6 through the harm setting, below). Or an hour with the tamer and the
  companion, a lesson in beasts.
- *The egg-trader* (`HappeningWorker_EggTrade`): an egg of a kind the colony doesn't keep, swapped for one of the
  colony's fertile eggs (carried out) or bought for silver at half again its worth.
- *A stray home* (`HappeningWorker_TamerStray`): one of the colony's animals that went wild or was lost within four
  months, found in the world, is walked to the gate on a lead; handed back (its master again if it has one), with a
  tenth of its worth for the ranger's trouble if the colony likes; or the ranger keeps it.
- *The fixer* (`HappeningWorker_WheelMending`, the travelling tinker's worker under the Wheel's name): mends worn
  clothes and weapons for silver, one piece at a time, or teaches.
- *The scrap-runners* (`HappeningWorker_WheelScrap`): with six pieces or more of scrap (slag in the stores or the home
  area, worn-out or dead men's clothes and weapons in the stores), they buy up to thirty, carried out to their cart, for
  silver (`ScrapPrice`) or for a piece of the colony's age they mended back to new (good quality).
- *The wreck* (`HappeningWorker_WheelWreck`): a mechanoid's wreck near the colony brings them at a run; stripped where it
  lies for three hours (`DilemmaEffect_WheelStrip`), half of each part left for the colony and half into their packs;
  or sold whole for three fifths of its parts' worth.

**Their asks** (`Rimfolk_Missions_PeoplesD.xml`, each only theirs): the Wardens' drive (two good shots for a few days,
`MissionHands`), the Bell's linen for shrouds in a plague year (`whileKind` sickness), the Circuit's young beast raised a
season (`MissionBeast`), the Wheel's steel or components for a broken axle.

**By patch** (`Patches/Rimfolk_PeoplesD.xml`, conditional): world events that prefer a calling (the old beast the
Wardens, the mourning the Bell, the fair the Circuit, the iron scarce the Wheel); the undertakers prefer the Bell, the
travelling tinker the Wheel, the animal doctor the Circuit, each with a text in their voice when they are the ones who
came (`Undertakers.People`, `Tinker.People` and the vet's people read `HappeningExtension.prefers` now).

**Release B's peoples that arise may take their ways** (`ArisingMath.Leans`, `Lean`): survivors of a fallen town, after
the river (half of those by one), on their fated quarter (`SaltBell`) stay to bury its dead and keep the Bell's ways; in
the industrial and spacer ages, on their fated third (`SaltWheel`), they put what is left on wheels; a band of the
banished whose leader is learned keeps a school as before, else one good with beasts (Animals 10) walks as tamers, else a
good shot (Shooting 10) hunts as wardens. Their words: `CC_Arise_refugees_sextons`, `_refugees_salvagers`,
`_exiles_tamers`, `_exiles_hunters` (each two ways), their three names (*Ashford Bell-keepers*, *The Ashford Wheel*,
*Barr's Circuit*, *Barr's Lantern*).

**Harm.** One touchpoint can hurt: the bout's bruise, `HarmKinds.TamerBout` (0.009 a colony-year, the harm model's
0.0097: `Tools/Balance/HarmSim.cs`), through `Harm.Roll` and `Harm.Injure` (`HurtClass.Bout`), on the shared budget and
the harm setting, shown on the answer (`Harm.On`) and told when it lands. Nothing else of theirs hurts anyone: the hunt
is the game's own fight with the beast.

**Saved**: nothing new. The hunt and the bout are visits of the happenings' tracker (an existing collection,
dispatched hourly by kind); the stray's faction is set back if the visit is cut short; the Wheel's town moves are the
world's own tiles.

**Softness**

| Missing | What happens |
|---|---|
| Ideology | No fixed faiths or styles. |
| Biotech | Humans only; no sash (a tamer's signature is then the violet of the rest), no mechanitor's headset. |
| Royalty | No psylinks; no animakin. |
| Anomaly | No void's signs; the gravewardens of a later age carry molotovs only. |
| Odyssey | No starjacks with the Wheel. |
| A RimWorld of Magic | No classes. |
| Vanilla Psycasts Expanded | Psylinks on the game's own way. |
| Vanilla Weapons Expanded (and Makeshift, Laser), Medieval 2, Vanilla Apparel Expanded - Accessories | The game's own bows, spears, clubs, guns, hoods and capes; no salvaged lasers for a spacer-age Wheel (its guards keep their guns). |
| PokéWorld | The tamer's companion is a beast of the land; no Pokémon egg. |
| The beasts' alphas, nemeses and legends off (Beasts settings) | No lantern hunt; no lantern without a beast nemesis. |
| The harm setting at 0 | The bout never bruises, and the answer doesn't say it may. |
| Answers play out on the map (setting) off | The skinning and stripping are done at once; the vigils and lanterns are the play-outs' own fallbacks. |
| The deeper game hooks (setting) | The game's own bases for their towns; the Wheel's towns stay put; no age arms or gifts. |

Not used: Vehicle Framework (a convoy needs its own vehicles' defs and pathing on the world map; not done), Giddy-Up
(its own rules mount riders where it is loaded; nothing named), the design's small mechs for the Wheel's mechanitors
(a mechanitor's mechs need their own group maker; they wear the headset and carry components only), Rimsenal (not in
the player's active mods), nets for the Wardens (no net weapon among the active mods).

## What was built (release E: the Stormglass Conclave, the Mossborn, the Ashen Banner and the Kiln Guild)

The second four of the eight peoples of `docs/design/peoples-next.md` (the same request as release D, the player's
words, 7 November 2026: "love all these factions, go ahead!", and "always consider the actual mods i have enabled in my
save and mod list"; no proposal round), at release D's level: a founding story, sayings, what they honour and fear,
titles, messengers, gear by role, faith, town plan, voice, touchpoints at the gate that play out on the map, an ask, a
patch on the callers and world events that should come from them, names for peoples that arise in their ways and an
Almanac page. Built only from the player's active mods (`docs/player-mods/`, the rows with `active` yes), always softly,
with the base game's fallback. Code in `Source/ColonyCouncil/Peoples/`: `PeoplesE.cs` (helpers, the job driver, the
Conclave, the Mossborn), `PeoplesE2.cs` (the Banner, the guild, the two hosting asks), `PeoplesEPatches.cs` (four game
hooks), `PeoplesEMath.cs` (pure, linked into the tests: `Tools/Tests/PeoplesETests.cs`, "peoples, release E: ");
`Camps/CampContract.cs` (a camp hired by contract); `PeopleAgeArms.apparel` (`PeopleDefs.cs`, `AgeArms.Kit` in
`PeopleGifts.cs`: a kit for another age, worn with the arms). Defs: `Defs/PeopleDefs/Rimfolk_People_Stormglass.xml`,
`_Mossborn.xml`, `_AshenBanner.xml`, `_KilnGuild.xml`, `Rimfolk_TownPlans_PeoplesE.xml`;
`Defs/IncidentDefs/Rimfolk_Events_PeoplesE.xml`, `Defs/MissionDefs/Rimfolk_Missions_PeoplesE.xml`,
`Defs/CampDefs/Rimfolk_CampKinds_PeoplesE.xml` (the squad's camp and its sentry's job),
`Defs/FlavorPoolDefs/Rimfolk_Lines_PeoplesE.xml`, `Defs/OutsiderTalkDefs/Rimfolk_OutsiderTalks_PeoplesE.xml`,
`Defs/ThoughtDefs/Rimfolk_Thoughts_PeoplesE.xml`, `Defs/Misc/Rimfolk_PeoplesE_Misc.xml` (the jobs, the taught reader's
season, the rod), `Patches/Rimfolk_PeoplesE.xml`, Keyed `Rimfolk_PeoplesE.xml`; the Almanac's page *the Conclave, the
Mossborn, the Banner and the guild*; the rod's texture `Textures/ColonyCouncil/Things/StormglassRod.png`.

| | The Stormglass Conclave | The Mossborn | The Ashen Banner | The Kiln Guild |
|---|---|---|---|---|
| defName | `Rimfolk_Stormglass` | `Rimfolk_Mossborn` | `Rimfolk_AshenBanner` | `Rimfolk_KilnGuild` |
| Calling | seers | grovekeepers | mercenaries | artisans |
| Age | Industrial (outlander parent) | Tribal (tribal parent) | Medieval (tribal parent) | Medieval (tribal parent) |
| Arises in (release B) | every age | every age | every age | every age |
| Founding | the glassblower Hesper Lyle, whose village lost its harvest to a hail nobody saw coming, sealed a vial of rain from every storm and wrote on its cap what came after; in thirty years she read a frost in the glass a week ahead | the charcoal-burner's widow Elin Sedge walked the stumps of a valley a war had cut down and planted an acorn beside each; she lies under the last sapling she set | the quartermaster Corra Mallin would not let five hundred soldiers of a lost war scatter unpaid: she sold every banner but the one burned grey and hired the company out; the Roll is everyone lost under it | the mason Oswin Kell rebuilt the Hardwater bridge three times after the flood and cut his mark into every stone; the fourth still stands |
| Sayings | The sky is the world thinking aloud. Every storm is written down. Never wrong, never sure. | Every tree you fell owes the world a seedling. The forest remembers every axe. In the end we are all the grove. | Paid to the day, held to the day. Every name on the Roll. No pay, no wall. | Anyone can stack stone. Mark it, and answer for it. Bad work is a lie told in stone. |
| They honour / fear | the vial kept, the storm written down and a forecast that comes true / the hail nobody saw coming, a broken glass and a reader who is sure | the seedling set beside the stump, an old tree left standing and a grave that grows / fire in the canopy, the clear-cut hillside and the axe that keeps no count | a contract kept to its last day, a wall held and every name on the Roll / a contract broken, a name left off the Roll and a field nobody came back from | the mark cut true, a masterwork and a journeyman who comes back better / work that lies, a cold kiln and a mark on a thing nobody wanted |
| Signature | a long coat in storm slate (the game's duster and jacket dyed) | one piece of tribal wear in moss green, and Vanilla Expanded - Classical's wreath where it is loaded | the one issued kit, ash-grey (plate, cape, flak, recon armour, Medieval 2's tabard, tribal war wear dyed), an officer's black sash | kiln ochre on a shirt, a cape or Vanilla Apparel Expanded's apron |
| Leader, head, heir, envoy, trader | first reader, keeper of the glass, second keeper, storm-walker, vial-seller | eldest root, grove-warden, second warden, seed-carrier, herb-trader | captain of the Banner, keeper of the Roll, under-keeper, sergeant, paymaster | guildmaster, kiln-warden, second warden, journeyman, master-trader |
| Messengers | vial-runners | leaf-runners | banner-runners | guild runners |
| Towns | 0.35: towers | 0.5: groves | 0.4: forts | 0.45: guildhalls |
| Caravans | 0.7 | 0.8 | 0.6 | 1.2 |
| Raids | 0.2 | 0.2 | 0.4 | 0.2 |
| Faith (Ideology) | The Read Sky: nature primacy (and VE Memes' astrology and elders); animist, abstract or esoteric; Islamic | The Owed Seedling: tree connection, nature primacy (and Alpha Memes' Gauranlen supremacy); animist, embodied or pantheist; totemic | The Roll: loyalist, collectivist (and Alpha Memes' bulwark); ideological, abstract or authoritarian; spikecore | The Mark: proselytizer (and VE Memes' craft culture, Alpha Memes' artist); ideological, abstract or corporate; Hindu |
| Kinds (Biotech, a fifth or so) | genies, yttakin, highmates, Rimsenal's askbarn and uredd, Avaloi, archons, high elves | Neanderthals, pigskins, the phytokin (animakin, gauranlenkin, poluxkin, ocularkin), wood elves, fungoids, awakened orangutans and gorillas | hussars, Neanderthals, uhlans, boarskins, Venators, uredd, gorillas, korgs | dirtmoles, genies, stoneborn, orangutans, lapis, boglegs |
| Colour | storm slate | moss green | ash grey (the sash black) | kiln ochre |

**How alike they look, role by role** (uniform only where it makes sense). The Conclave wears no uniform: readers in a
dyed long coat of the industrial age over plain clothes, wide hats and packs (Vanilla Apparel Expanded - Accessories),
carrying herbal medicine, books and cloth; storm-walkers in a duster, flak vest, collar shirt and trousers with a gun;
apprentices and the vial-seller freer; the first reader in a duster, with Royalty its psychic gear. The Mossborn dress
freely in tribal wear with one piece moss green and the wreath; thorn-guards in Vanilla Armour Expanded's wooden armour
with bows, spears and clubs; seed-carriers with a headscarf and a satchel of berries, herbs and (Ideology) a Gauranlen
seed; the herb-trader with beads; the eldest root in a parka. The Banner is the one people in one kit: every soldier and
shot in the same ash-grey plate, simple helmet, shirt, trousers and (Medieval 2) tabard, shots with a quiver; sergeants
and the captain in a cape and the black sash (Biotech) over the same; the paymaster in a cape and collar shirt. The
guild's journeymen in shirt, trousers and apron with a cap or headscarf, mallets, tools and blades (Vanilla Weapons
Expanded's tools); guards in a cape and helmet with longswords; the guildmaster in an apron and cape with beads.

**Every age** (the player's rule). `PeopleAgeArms.apparel`: a kit worn with an age's arms, made in its stuff, dyed the
people's colour where its kind dyes that piece, worn over what clashes with it (`AgeArms.Kit`). The Banner issues every
age's kit: tribal war wear and great clubs and spears in the tribal age, the medieval plate, flak vest, trousers and
advanced helmet with an assault rifle in the industrial age, recon armour and helmet with a spacer gun in the spacer age
(its shots the same with a bow, a rifle or a charge rifle). The Conclave's storm-walkers take great bows and Medieval 2's
arbalest in the tribal and medieval ages and spacer guns in the spacer age; the Mossborn's thorn-guards guns in the
industrial and spacer ages; the guild's guards an ikwa in the tribal age, a longsword or Bamba's forged one-handed
blades in the industrial age and monoswords and the like (Royalty) in the spacer age.

**Gifts** (`PeopleKindExtension.gifts`): the Conclave's psylinks (Royalty) on Vanilla Psycasts Expanded's Staticlord or
Frostshaper paths, a reader 35%, the first reader 45% at two to four levels, and A RimWorld of Magic's StormBorn and
Heart of Frost now and then (the Conclave reads the weather as the planet's psychic tide: their lore, beside the learned
and the spacers as the peoples who have psylinks); the Mossborn's druids and rangers, the eldest root a druid one in
three and its only psylink (a Wildspeaker's, 20%); the Banner's gladiators, paladins, snipers, super soldiers in the
industrial and spacer ages, and commanders (the captain 30%); the guild's enchanters and geomancers (the guildmaster one
in five), technomancers in the industrial and spacer ages. No gift over 45% of a kind.

**Their towns' maps** (`Rimfolk_TownPlans_PeoplesE.xml`). The Conclave's **tower** (33 by 23): a marble-tiled library
of five bookcases and two research benches (the record of the storms), a reading table, six beds, the store, rose and
daylily beds, and a ring of ten steel columns (the rods). The Mossborn's **grove** (35 by 24): a hall of bedrolls and the
store, a crafting spot, twelve graves on packed earth among oaks and birches planted for them, and thirty-two cells of
healroot. The Banner's **fort** (37 by 25): a walled square with barracks of ten beds, the hall of the Roll (bookcases, a
long table), a drill yard behind ten sandbags, the store, and stables for its horses. The guild's **guildhall** (37 by
24): the workshop (a fueled smithy, a stonecutter's and a sculptor's table, a fueled stove), the hall under eight
columns, market stalls of tables on the flagstones, four beds, the store.

**Theirs at the gate** (`Rimfolk_Events_PeoplesE.xml`; each only from a people of its calling at peace with the colony,
the deserter from any Banner still standing, each with three answers, the last always to send them on):

- *A season's watch* (`HappeningWorker_GlassReading`, `DilemmaEffect_GlassWatch`): a reader offers to watch the
  colony's sky for fifteen days (a fee of 80 to 400 silver by its wealth, `WatchFee`, carried out); the reader then walks
  out to read the colony's biggest field before the road. While the watch runs (a visit of the happenings' tracker,
  kind `glassWatch`), every condition of the sky the storyteller sends to that home (`PeoplesEMath.SkyIncidents`: cold
  snap, heat wave, toxic fallout, volcanic winter, flashstorm, eclipse, Biotech's noxious haze, Odyssey's drought and
  Vanilla Events Expanded's drought, whiteout, scorch and long night), never a quest's, is held: `Patch_GlassHold`
  (a prefix on `Storyteller.TryFire`, never on one coming back out of the queue) puts it into the game's own incident
  queue 18 to 30 hours on (`QueuedIncident`, with 12 hours to retry), one held at a time; a vial-runner of theirs sets
  off for the gate at once (a visit, kind `glassRunner`, who says it over their head there) and the news letter names
  the condition; and the colony's fields by the weather reckon the held cold or heat from that hour
  (`GlassWatch.Ahead`, read by `FieldWeather.Forecast` through `FieldWeatherMath.Forecast`'s new hours-from list: a cold
  snap −20 °C, a heat wave +17 °C, a volcanic winter −7 °C, for its usual length). Or a free reading of tonight's sky
  at the gate with the colony's best hand at the fields.
- *A rod on the roof* (`HappeningWorker_GlassRod`, `DilemmaEffect_GlassRod`): with a roofed room of nine cells or more
  and no rod yet, a storm-walker carries a rod in to the colony's biggest room and sets it up there (`JobDriver_PeoplesE`,
  a progress bar and sparks; `Rimfolk_StormglassRod`, the colony's, can be moved). From then a lightning strike on the
  open ground within 18 cells of a rod strikes the rod instead (`Patch_GlassRodStrike` on
  `WeatherEvent_LightningStrike.FireEvent`, the cell chosen as the game chooses it when it had none), and the strike's
  blast there is skipped (`Patch_GlassRodExplosion` on `GenExplosion.DoExplosion` at that cell and map only): the bolt,
  its sound and flash, no fire; told once a storm. The rod counts as a weather station
  (`WeatherDynamics`' stations). The colony's builder may stand by to learn how it is grounded.
- *Reading the glass* (`HappeningWorker_GlassLesson`, `DilemmaEffect_GlassTaught`): towards evening a reader teaches the
  colony's best hand at the fields who isn't taught yet (a lesson from 17h); for sixty days (`Rimfolk_GlassTaught`) their
  forecasts are 15 points nearer the truth, never past a station's (`PeoplesEMath.Taught`), and one who didn't read
  the weather before now does. Or an hour at the gate watching the sky.
- *Saplings for the felled* (`HappeningWorker_MossSaplings`, `DilemmaEffect_MossPlant`): once the colony has felled a
  dozen trees in the ledger's season, a seed-carrier comes with six to fourteen saplings (`Saplings`: one for every three
  felled) and plants them one by one on open soil round the colony, out of its home area, fields and buildings, two cells
  apart (`MossPlanting.Cells`; the land's own wild trees that give wood, else oak, birch or poplar; sown, so they count as
  the colony's planting), the colony's planter beside them if it likes.
- *A tree for the dead* (`HappeningWorker_MossGraveTree`): for one of the colony in a grave who died within ten days, a
  grove-warden plants a sapling on soil beside the grave, the colony gathered there (a vigil, its own words, and
  `Rimfolk_Evt_GraveTree`) or one colonist with them.
- *The tree-connector's pruning* (`HappeningWorker_MossTreeTend`, Ideology): the colony's Gauranlen tree connected to one
  of its people, under full strength; a tree-connector prunes it a while (`Rimfolk_PeoplesE_TendTree`) and its
  connection strength rises 15 points, never past full (`CompTreeConnection.ConnectionStrength`).
- *The squad for the season* (`HappeningWorker_BannerHire`, `DilemmaEffect_BannerHire`, `BannerSquad`, `Camp` work
  `contract`): with a camp able to stand (camps on, no other camp, no danger, a site, bearable weather), a sergeant and
  two to four of the company. Hired, the first wage is carried out to the sergeant and the squad makes camp by the colony
  for fifteen days (`Rimfolk_Camp_BannerSquad`: `fights`, so its people take up arms against any threat on the map as a
  war party does; one of them stands watch at the gate by day, `Rimfolk_CampStandWatch`). On the fifth and the tenth day
  the camp asks its wage by letter (the ask `wage`, `price` wage: 60 silver a soldier for five days by the Banner's age,
  0.6 tribal to 2.4 spacer, `Wage`; paid as the ask is answered, carried out to their fire, blocked without the silver);
  turned down or let go, they strike camp and walk (`Leave("unpaid")`, the ending `CC_Camp_End_unpaid`) and the Banner
  thinks 8 less of the colony; kept to its end, 6 more. Or the sergeant drills the colony's best fighter for an afternoon.
- *A deserter at dusk* (`HappeningWorker_BannerDeserter`): a soldier who ran from the Banner (the tabard left behind)
  asks shelter at dusk: taken in (they join; the Banner, which struck the name from the Roll, thinks 3 less of the
  colony), a warm corner for the night and the road at first light (`VisitorServices.Lodge`), or the gate stays shut.
- *The Roll read at the grave* (`HappeningWorker_BannerRoll`): for one of the colony who died of wounds within thirty days
  and lies in a grave, the keeper of the Roll comes; the colony gathers at the grave at dusk to hear the name read into
  it (a vigil with its own words, `Rimfolk_Pool_PlayBannerRoll`, `Rimfolk_Evt_NameOnRoll`), or one colonist stands with
  the keeper.
- *A guild commission* (`HappeningWorker_GuildCommission`, `DilemmaEffect_GuildCommission`): a journeyman commissions a
  piece from the colony's best maker (the better of its crafter and artist): a weapon or a garment of the colony's own
  benches, or a sculpture for an artist, of quality, made from the plainest stuff in its stores whose recipe it has and
  whose bench it has. The stuff is set apart beside the stack and the maker makes it at the bench, as the collector's
  commission is made (`PlayMake.KeepMade`, `Callers.Commission`). Or the maker talks shop with the journeyman at the gate.
- *The guild comes for the commission* (`HappeningWorker_GuildCollects`): the journeyman back for a finished guild piece
  (`Callers.Due` with the guild's commissions only; the collector's own `CollectorBack` takes the rest), paying by its
  quality (`GuildPay`: half its worth for awful work up to 3.2 times for a legend, to the nearest five): handed over, with
  the maker hearing what the guild saw in it if the colony likes, or kept.
- *A bid on a masterwork* (`HappeningWorker_GuildBid`): the colony's finest masterwork or legend (art or furniture
  standing in its rooms, anything in its stores) brings the master-trader to look at it and bid 1.8 times its worth, a
  legend 2.4 (`Bid`): sold and carried out, or kept and talked of.

**Their asks** (`Rimfolk_Missions_PeoplesE.xml`, each only theirs): the Conclave's apprentice of the glass
(`HappeningWorker_MissionGlassApprentice`, hosted as the dignitary is: a bed nobody else uses, the colony's meals; on
each clear night from 21h they lie out under the open sky near the gate for two hours, `Rimfolk_PeoplesE_Skywatch`,
face up, ended under a roof, and the night is counted when it's done; none at all and the stay is wasted; the colony's
best at the fields learns for each night); the Mossborn's food and medicine for a burned grove (a delivery); the
Banner's muster (two fighters, Melee 6, on its fort's wall for a day and a half, `MissionHands`); the guild's
wander-year (`HappeningWorker_MissionWanderYear`: hosted; two hours a working day at the bench nearest the colony's best
maker, `Rimfolk_PeoplesE_Bench`, the journeyman learning and the maker a little; at the end a small work of art of the
guild's age left by the gate, good, `ThingRoles.From(ThingRole.SmallArt, ...)` of stone or wood).

**The Mossborn's ledger** (`MossLedger`, from the region's day, the deeper game hooks on): the trees the colony's
people cut on a home map (`Patch_MossFelled`, a prefix on `Plant.PlantCollected`) and the trees sown by hand that came
up since the last look (`Plant.sown`, counted by id) are kept in the peoples' book (`ArisingBook.groveFelled`,
`grovePlanted`, `groveSownId`, `groveSince`); every fifteen days each Mossborn people reckons them (`Shift`: thirty
planted more than felled warms them by 5, ten by 3; twenty felled more than planted cools them by 3, sixty by 6;
nothing for a season under ten trees) and the news says so, never cooling them below none (`Kept`).

**By patch** (`Patches/Rimfolk_PeoplesE.xml`, conditional): world events that prefer a calling (a year without a summer
the Conclave, a blight the Mossborn, a mercenary company looking for work the Banner, a great work the guild); the
fortune teller prefers the Conclave and the art collector the guild, each with a text in their voice when they are the
ones who came (`HappeningWorker_FortuneTeller` and `HappeningWorker_Collector` read `HappeningExtension.prefers` now).

**Release B's peoples that arise may take their ways** (`ArisingMath.Lean`, its 13-argument form): a band of the
banished, after the learned, the beast-handler and the good shot, led by one with a psylink reads the sky as seers, by a
fighter (Melee 10) hires out as mercenaries, by a crafter (Crafting 10) keeps a guild, by one good at the fields (Plants
10) a grove. Their words: `CC_Arise_exiles_seers`, `_mercenaries`, `_artisans`, `_grovekeepers` (each two ways), three
names each (*Barr's Glass*, *Barr's Company*, *Barr's Mark*, *Barr's Seedling*).

**Harm.** Nothing of theirs hurts anyone: the squad's fights are the game's own, against whatever attacks the colony.

**Saved**: four counts in the peoples' book (`ArisingBook`, modelled in `Tools/save_estimate.py`). The watch and the
runners are visits of the happenings' tracker; the held condition is in the game's own incident queue; the squad is a
camp (its wages marked in the camp's own asks); the hosting asks are missions.

**Softness**

| Missing | What happens |
|---|---|
| Ideology | No fixed faiths or styles; no Gauranlen tree to tend, no Gauranlen seed carried. |
| Biotech | Humans only; no black sash for the Banner's officers. |
| Royalty | No psylinks; no monoswords for a spacer-age guild (their Bamba blades or longswords). |
| Odyssey, Vanilla Events Expanded | Their conditions aren't there to foresee. |
| A RimWorld of Magic | No classes. |
| Vanilla Psycasts Expanded | Psylinks on the game's own way. |
| Medieval 2, Vanilla Factions Expanded - Tribals, Vanilla Expanded - Classical, Vanilla Apparel and Armour Expanded, Vanilla Weapons Expanded, Bamba's melee | The game's own pieces: no tabard, wreath, apron, wooden armour, arbalest, tribal war wear, tools or forged blades. |
| Camps off | No squad to hire (the drill alone). |
| Answers play out on the map (setting) off | The rod, the saplings, the grave's tree and the pruning are done at once; the commission made at once. |
| The deeper game hooks (setting) off | No condition held back (the watch's runner never comes), no rod drawing the lightning, no ledger; the game's own bases for their towns; no age arms, kits or gifts. |

Not used: Vanilla Trade Expanded's contracts (its code isn't among the player's files, only its defs: its contract
board needs its own UI and world objects; the squad is Rimfolk's own camp instead), Vehicle Framework, a convoy for the
Banner (as release D), Vanilla Psycasts Expanded's Wildspeaker for the Conclave (the weather's paths fit it better).

## What was built (release F: the Signal Line, the Deepwell Union, the Last Republic and the Hollow Fleet)

The first four of the eight peoples of `docs/design/peoples-third.md` (step 2 of its order of building; approved by the
player as written, 8 November 2026), at releases D's and E's level: a founding story, sayings, what they honour and
fear, titles, messengers, gear by role, faith, town plan, voice, touchpoints at the gate that play out on the map, an
ask, a patch on the world events that should come from them, names for peoples that arise in their ways and an Almanac
page; and the age they live in moves over the years by the rule of "Ages that change over time". Built only from the
player's active mods (`docs/player-mods/`, the rows with `active` yes), always softly, with the base game's fallback.
Code in `Source/ColonyCouncil/Peoples/`: `PeoplesF.cs` (helpers, the job driver, the relay mast, the Line, the union),
`PeoplesF2.cs` (the Republic, the Fleet, the dev actions), `PeoplesFPatches.cs` (six game hooks), `PeoplesFMath.cs`
(pure, linked into the tests: `Tools/Tests/PeoplesFTests.cs`, "peoples, release F: "); `Source/ColonyCouncil/PaceVisit.cs`
(`LordJob_VisitOnErrand`: the game's visit at an errand's pace, `Pace.Errand`). Defs:
`Defs/PeopleDefs/Rimfolk_People_SignalLine.xml`, `_Deepwell.xml`, `_Republic.xml`, `_HollowFleet.xml`,
`Rimfolk_TownPlans_PeoplesF.xml`; `Defs/IncidentDefs/Rimfolk_Events_PeoplesF.xml`,
`Defs/MissionDefs/Rimfolk_Missions_PeoplesF.xml`, `Defs/FlavorPoolDefs/Rimfolk_Lines_PeoplesF.xml`,
`Defs/OutsiderTalkDefs/Rimfolk_OutsiderTalks_PeoplesF.xml`, `Defs/ThoughtDefs/Rimfolk_Thoughts_PeoplesF.xml`,
`Defs/Misc/Rimfolk_PeoplesF_Misc.xml` (the jobs, the relay mast), `Patches/Rimfolk_PeoplesF.xml`, Keyed
`Rimfolk_PeoplesF.xml`; the Almanac's page *the Line, the union, the Republic and the Fleet*; the mast's texture
`Textures/ColonyCouncil/Things/RelayMast.png`.

| | The Signal Line | The Deepwell Union | The Last Republic | The Hollow Fleet |
|---|---|---|---|---|
| defName | `Rimfolk_SignalLine` | `Rimfolk_Deepwell` | `Rimfolk_Republic` | `Rimfolk_HollowFleet` |
| Calling | linesmen | drillers | citizens | crew |
| Age | Industrial (outlander parent) | Industrial | Industrial | Spacer |
| Its age over the years | 8 years an age, up to the spacer age | 11, up to the spacer age | 9, up to the spacer age | 10; at the height already, falls back when cut off |
| Arises in (release B) | every age | every age | every age | every age |
| Founding | after the long war cut every road, the lineman Ennis Calloway walked the hills with a coil of copper wire and raised a mast on every height he could climb, until one valley could hear the next; the first mast still stands and has never gone quiet | the night the seam under Deepwell ran dry and the owners fled, the shift-captain Marta Grieve put it to the vote in the hall, scatter or stay together; they wrote every name in a book and walked out with their picks | the winter the capital went dark, the postmaster Clement Ashdown was the most senior official still answering the radio; he read the old laws aloud every evening, issued ration cards and called an election in the spring | Captain Imre Sallow's last order, when the hull came down short of its world: keep your posts until the order comes to disembark; he died before giving it, and nobody since has had the rank |
| Sayings | Every mast is a voice. A voice is a road nobody has to walk. Keep it singing. Say where you heard it. Silence is the only storm. | The ground keeps what it's given. We go down and ask for it back. Everyone up at the end of the shift. Put it to the vote. One book, one union. | The Republic never fell. It simply has fewer citizens than it used to. Everyone counted, everyone owed. Keep the radio on. There is a form for that. | We were promised a world. We are still waiting for orders to disembark. Holding position. Every watch is logged. The ship provides, and we provide for the ship. |
| They honour / fear | a mast kept singing, a message passed on true and a lineman back down safe / silence on the line, a storm that takes a mast and news passed on wrong | a seam worked well, a shaft shored right and everyone up at the end of the shift / a cave-in, a boss who decides alone and a pit gone silent | the census kept, an election held on the day and a radio that never goes silent / disorder in the streets, an uncounted people and the radio gone quiet | a watch kept to its last hour, the ship in good repair and a sleeper woken among friends / the last part failing, a sleeper who wakes to strangers and the order that never comes |
| Signature | the linemen's kit: an oilskin (duster) in signal orange over a collar shirt and trousers, Vanilla Apparel Expanded's hard hat; operators and families an orange cap or hat | the union's red on a work shirt and an armband (Biotech's sash), hard hats on the miners and shot-firers | the Civic Guard's one uniform in civic navy (shirt, trousers, flak vest, helmet); clerks and citizens a navy cap or bowler | the ship's uniform in deck grey (collar shirt and trousers) on every rank; recon armour for the ship's guard; officers a jacket and Royalty's beret |
| Leader, head, heir, envoy, trader | chief lineman, relay-keeper, second keeper, lineman, wire-trader | union boss, shift-captain, second captain, driller, assayer | president, minister, deputy minister, census-taker, quartermaster | captain, first officer, second officer, ensign, quartermaster |
| Messengers | line-runners | pit-runners | dispatch riders | deck-runners |
| Towns | 0.4: relays | 0.45: pits | 0.5: towns | 0.3: hulls |
| Caravans | 0.8 | 1 | 0.9 | 0.6 |
| Raids | 0.2 | 0.3 | 0.3 | 0.15 |
| Faith (Ideology) | The Open Line: collectivist (and VE Memes' progressive and trader); ideological or abstract; techist | The Union Book: collectivist, tunneler (and VE Memes' industrialism); ideological or abstract; rustic | The Republic Never Fell: loyalist (and VE Memes' republic and city builders); ideological or archist; Christian | The Ship's Watch: loyalist, transhumanist, Odyssey's shipborn; ideological or archist; techist |
| Kinds (Biotech) | genies, hussars, highmates, starjacks, awakened androids, lapis, Avaloi (18%) | dirtmoles, Neanderthals, wasters, stoneborn, boglegs, korgs, gorillas | hussars, genies, highmates, Neanderthals, pigskins, uredd, askbarn, Venators | starjacks, genies, highmates, hussars, archons, awakened androids, askbarn, nereids (29%) |
| Colour | signal orange | union red | civic navy | deck grey |

**How alike they look, role by role** (uniform only where it makes sense). The Line's linemen and the chief lineman in
the one kit above, with Vanilla Apparel Expanded - Accessories' utility belts and packs; relay-keepers in the oilskin
without the hat; operators and the wire-trader in their own clothes with an orange cap. The union dresses by trade in
work clothes with the red: miners and shot-firers in trousers, the sash and a hard hat; the shift-captain in a jacket;
the assayer and the boss freer. The Republic's Civic Guard in its one uniform, every piece navy (and every other age's
issue in the same navy); clerks in shirt, trousers and bowler; citizens and the quartermaster in their own clothes; the
president in a duster. The Fleet's whole crew in the ship's uniform: deckhands as it is, the ship's guard in recon
armour and helmet over it, engineers with Biotech's airwire headset, officers in a jacket and beret, the quartermaster
and the captain in a duster.

**Every age** (the player's rule; the age they live in now, `Peoples.AgeOf`). The Line's linemen take Vanilla Factions
Expanded - Tribals' oiled poncho and a bow in the tribal age, a cape and a great bow in the medieval age, a charge rifle
in the spacer age. The union's miners clubs and spears, maces, charge rifles; its shot-firers bows, great bows, charge
rifles; its captains spears, swords and maces, charge rifles. The Civic Guard's issue: tribal war wear and great clubs,
plate armour with spears and swords, recon armour and charge rifles. The ship's guard falls back through every earlier
age (tribal war wear, plate with spears and swords, flak and an advanced helmet with an assault rifle), all in grey.

**Gifts** (`PeopleKindExtension.gifts`): the Line's linemen A RimWorld of Magic's snipers and rangers; its operators
technomancers and, with Royalty, a psylink of one or two levels on Vanilla Psycasts Expanded's Staticlord path (4%: the
psychic hum in the static). The union's miners a few with mining genes put in (Biotech's dark vision and robust, 4%;
Alpha Genes' high mining yield, 3%) and geomancers; shot-firers geomancers and inner fire. The Republic's clerks bards,
and its propaganda officers a psylink of one or two levels as Empaths or Puppeteers (4%). The Fleet's deckhands strong
immunity from the ship's old gene work (5%); engineers Biotech's mechlink and technomancers; officers a psylink of two to
four levels as Skipmasters, Protectors or Technomancers (25%), the captain three to five (30%) and commanders; the
ship's guard super soldiers and snipers.

**Their towns' maps** (`Rimfolk_TownPlans_PeoplesF.xml`). The Line's **relay** (35 by 22): the mast (`Rimfolk_RelayMast`)
on the plan's height inside a fenced yard, the hut with two comms consoles, a bunkhouse of ten beds, a mess, and the
cable yard of shelves and the store. The union's **pit** (37 by 24): the headframe of columns over the shaft, the slag
heap, the union hall with its bookcases (the book), a boarding house of twelve beds, the smithy and the store. The
Republic's **town hall** (37 by 22): the hall with its radio (comms consoles), the archive of eight bookcases, the
ration office, the guardhouse behind sandbags, four beds. The Fleet's **hull** (37 by 21): the bridge (comms consoles),
the crew deck of nine beds, the sleepers' hold of five cryptosleep caskets, hydroponics and the landing pad.

**The relay mast** (`Rimfolk_RelayMast`, the colony's own, `CompRelayMast`): it sings while it is above half its hit
points (`PeoplesFMath.Quiet`) and a people of the linesmen is at peace with the colony (`RelayMast.Sings`). A singing
mast at a home: the colony's letters by a messenger and by a bearer, and their answers, are on the road half the days
(`PeoplesFMath.Relayed`, never under half a day; `NeighbourLetters.HandedToBearer` and `Handed`); a raid the storyteller
sends to that home (big or small, never a quest's, never one coming back out of the queue) is held by
`Patch_LineMastWarns` (a prefix on `Storyteller.TryFire`) and put into the game's own incident queue 2 to 4 hours on
(`WarnHours`, 6 to retry), with the line's warning letter at once; the post (`Post/PostReach.Mast`) counts it as a mast
near for its range. A thunderstorm wears it a hit point a quarter hour (`StormWear`, the comp's rare tick); under half
its strength it is quiet until mended (told once), and its inspect line says which.

**Theirs at the gate** (`Rimfolk_Events_PeoplesF.xml`; each only from a people of its calling at peace with the
colony, each with three answers, the last always to send them on):

- *A relay mast* (`HappeningWorker_LineMast`, `DilemmaEffect_LineMast`): with no mast yet, a lineman offers to raise one
  on the colony's highest ground (`RelayMast.Site`: open, unroofed ground that bears a heavy building, 8 to 40 cells from
  the colony's middle, the hillside first, rough ground next, the nearer the better). The lineman walks there and raises
  it (`JobDriver_PeoplesF`, two hours, a bar and sparks), the colony's builder beside them if it likes.
- *The wire-trader* (`HappeningWorker_LineWire`): the colony has components in its stores; the wire-trader buys four to
  twenty at a fifth over their worth (`WirePrice`), carried out to them and paid in silver. Or the colony pays fifty
  silver for the line's news of a caravan on the road, and the game's own trader caravan comes.
- *A climb in the storm* (`HappeningWorker_LineStorm`, `DilemmaEffect_LineClimb`): a thunderstorm on a home whose mast
  is worn; a lineman comes up the road through it, climbs the mast (a bar and sparks at its foot) and mends it to full
  strength (`RelayMast.Climbed`), the colony's builder holding the line at its foot if it likes.
- *A drill for a share* (`HappeningWorker_UnionDrill`, `DilemmaEffect_UnionDrill`, `UnionDrill`): the colony's ground
  holds a deep seam (the game's own deep resources, `deepResourceGrid`); a drill crew comes, the driller works it six
  hours where it lies (a bar and dust) while the crew stands by, then what came up (as much as lies there, up to about
  700 silver's worth, to 1,400 for a richer colony, `Yield`) is split: the union's two in five into the driller's pack,
  the colony's three in five left at the drill (`ColonyShare`), and the seam is the less by it. The colony's best miner
  may work the rig beside them and learn; one shift in twenty the rig hurts them (below).
- *The assayer's crate* (`HappeningWorker_UnionGoods`, `DilemmaEffect_UnionGoods`): a crate of steel (`CrateSteel`, 40 to
  120 by the colony's wealth), components (2 to 8) and blasting goods (high-explosive shells, Settlers' dynamite where it
  is loaded) for a tenth over their worth (`GoodsPrice`), the silver carried out and the crate set down at the gate by
  the assayer (`Couriers.Loot`). Or the assayer walks the colony's ground with its best miner.
- *The strike* (`UnionStrike`, `HappeningWorker_UnionStrike`, `DilemmaEffect_UnionSettle`): each union is looked at
  once a season on its own day (`StrikeDay`), and one look in seven calls a strike (a visit, kind `unionStrike`). While
  it runs, no caravan of theirs comes (`Patch_UnionStrikeCaravans` on
  `IncidentWorker_TraderCaravanArrival.FactionCanBeGroupSource`), their visitors bring nothing to sell
  (`Patch_UnionStrikeVisitors` on `IncidentWorker_VisitorGroup.TryConvertOnePawnToSmallTrader`), and their pits don't
  trade (`Patch_UnionStrikeTown` on `Settlement.CanTradeNow`). It ends by their own vote after a month (`StrikeDays`),
  or sooner: from the second day a shift-captain may come to the gate, and food for the picket line (60 to 160,
  `PicketFood`) carried out, or the colony's best talker sitting with them, settles it (+8, `Rimfolk_Evt_StrikeSettled`).
- *The census* (`HappeningWorker_RepublicCensus`, `DilemmaEffect_RepublicCensus`, `Census`): a census-taker and a guard
  walk the colony from bed to bed (to whoever has none, where they stand), a while at each with the roll (`CensusTicks`,
  a line over their head), and the count is written and told: the Republic thinks 3 better of the colony. The colony's
  best talker may walk them round. Or the colony keeps its own count (-2).
- *Citizenship papers* (`Papers`, `HappeningWorker_RepublicPapers`): taken, a visit (kind `republicPapers`) for a year
  (sixty days): the Republic's traders, caravans and towns give the colony a citizen's terms, six in a hundred better
  (`Patch_PapersPawnTerms`, `Patch_PapersTownTerms` on the game's own `TradePriceImprovementOffsetForPlayer`), and the
  colony owes a duty (+5; struck, -6).
- *A conscript for a season* (`HappeningWorker_RepublicConscript`, `Conscripts`, `JobDriver_ConscriptOut`): a sergeant
  asks one good fighter for fifteen days' service at the Republic's town. Sent (a visit, kind `conscript`, its stake the
  stage), the colonist takes the Guard's rations (`Hosting.Provision`), walks to the map's edge and sets out as a caravan
  of their own (`CaravanExitMapUtility`, kept in the world as lent, `WorldLedger`), the envoys' way; at the town they
  serve the season; then they come home by the same roads (`CaravanMaker`, `CaravanArrivalAction_Enter`) with the pay
  (150 silver and 90 an age, `ServicePay`), the drill (shooting, and melee half) and +8. The roads' ambushes and weather
  are the game's own; nothing of Rimfolk's hurts them. The town fallen or the Republic turned on the colony, they come
  home sooner. Or the colony pays to be excused (120 to 480 silver by its wealth, `Exemption`), or refuses (-5, and a
  citizen colony's papers are struck).
- *The radio* (`RepublicRadio`, from the region's day): once a season on each Republic's own day (`RadioDay`), at a home
  with a radio (a powered comms console, or the Line's mast singing), its evening broadcast praises a colony of citizens
  or good friends (goodwill 40) or denounces one under none or at war (`Broadcast`), told in its voice and felt by
  everyone at home (`Rimfolk_Thought_RadioPraised`, `_RadioDenounced`).
- *The shuttle* (`HappeningWorker_FleetShuttle`, `DilemmaEffect_FleetShuttle`, `FleetLanding`): for an industrial or
  spacer colony the quartermaster calls the radio; bidden, Royalty's shuttle lands by the gate (by drop pod without
  Royalty), the quartermaster and one to three of the ship's guard (`ShuttleGuards`) step down and trade as a visiting
  trader does. The colony may also buy a cryptosleep casket from the hull (a tenth over its worth, `CasketPrice`), which
  comes down crated in the hold.
- *Salvage* (`HappeningWorker_FleetSalvage`, `DilemmaEffect_FleetSalvage`, `FleetSalvage`): the colony has what the ship
  needs (ship chunks on its ground, worth 220 each to them; persona cores and advanced components in its stores,
  `PlayCarry.Salvage`), 150 silver's worth at least; the quartermaster pays three tenths over its worth (`SalvagePrice`):
  the cores and parts carried out, and the deckhands cut up to three chunks apart where they lie (two hours each, a bar
  and sparks) and carry them off. The colony's crafter may watch them.
- *The sleeper woken too late* (`HappeningWorker_FleetSleeper`): towards evening (15h to 21h) a sleeper from the Fleet's
  caskets, 190 to 230 years asleep (their age the older by it, still with cryptosleep sickness), asks shelter: taken in
  (they join), a warm corner for the night, or the gate stays shut.

**Their asks** (`Rimfolk_Missions_PeoplesF.xml`, each only theirs): the Line's fallen span (parts or steel, a delivery),
the union's short shift (two good miners, Mining 6, for a day and a half), the Republic's witness at the count of its
election (a good talker, Social 6, for a day) and the Fleet's lift (plasteel or parts).

**By patch** (`Patches/Rimfolk_PeoplesF.xml`, conditional): world events that prefer a calling, five times as likely as
another people: two peoples who want to talk (the Line's masts carry the talks), a silver rush (the union), a republic's
vote (the Republic), exiles from a town (the Fleet: sleepers who left the hull).

**Release B's peoples that arise may take their ways** (`ArisingMath.Lean`, its 17-argument form): a band of the
banished, after all the other leans, led by a good miner (Mining 10) digs as a union, by a good talker (Social 10) keeps
a census and votes; survivors of a fallen town, after the Bell's and the Wheel's ways, keep its radio going and raise
masts on their fated quarter in the industrial or spacer age (`LineChance`, `SaltLine`), and keep its watches as a crew
on their fated third in the spacer age (`CrewChance`, `SaltCrew`). Their words: `CC_Arise_refugees_linesmen`,
`_crew`, `CC_Arise_exiles_drillers`, `_citizens` (each two ways), three names each (*the Barr Line*, *the Crew of Barr*,
*Barr's Union*, *the Republic of Barr*, each with two other forms). Each people's rise and fall told in its own voice
(`CC_PeopleAge_Voice_*`).

**Harm.** One thing of theirs hurts anyone: the colony's hand at the union's rig, one shift in twenty
(`AccidentChance`), a crush or a bruise to a foot of 3 to 8 (`Harm.Injure`), through the harm setting and the shared
budget (`HarmKinds.UnionDrill`, base 0.005 a colonist-year, in the peoples' share; modelled in `Tools/Balance/HarmSim.cs`),
told when it lands. The conscript's dangers are the world's own roads.

**Their pace on the map.** Their callers come, go about their work and leave at an errand's pace (`Pace.Errand`): the
gate's visit lord is `LordJob_VisitOnErrand` (the game's visit with its travel on an errand and its way off the map at
the errand's pace; `HappeningHelpers.Visit` uses it for every caller), and so is the lord that keeps a caller at its
work's spot.

**Saved**: nothing new. The strike, the papers and the conscript are visits of the happenings' tracker; the mast is a
building; the held raid is in the game's own incident queue. A drill or a climb loaded halfway starts again (a drill
loaded halfway brings nothing up; the crew goes home on its hours).

**Softness**

| Missing | What happens |
|---|---|
| Ideology | No fixed faiths or styles. |
| Odyssey | The Fleet's faith without shipborn. |
| Biotech | Humans only; no sash for the union's red, no mechlink or headset, no genes. |
| Royalty | No psylinks; the Fleet comes down by drop pod; no beret. |
| A RimWorld of Magic, Vanilla Psycasts Expanded | No classes; psylinks on the game's own way. |
| Vanilla Apparel Expanded (and Accessories), Vanilla Factions Expanded - Tribals, Settlers, Frontier, Non-Lethal, Alpha Genes | The game's own pieces: no hard hat, belts, poncho or tribal war wear, dynamite, lever guns, batons, mining genes. |
| Answers play out on the map (setting) off | The mast raised, the climb, the drill, the census and the salvage done at once. |
| The deeper game hooks (setting) off | No raid warning, no strike stalling trade, no citizen's terms, no strike called or radio heard; the game's own bases for their towns; no age arms, kits or gifts. |

Not used: a radio talk window of the Line's own (the game's comms console and Rimfolk's letters carry it), real world
ships for the Fleet (its shuttle is Royalty's, as the enclave's lander is), a vote on the colony's own matters.

## What was built (release G: the Helix Works, the Lattice, the Choir of the Spire and the Sunken Vault)

The second four of the eight peoples of `docs/design/peoples-third.md` (step 3 of its order of building; approved by the
player as written, 8 November 2026: "i love all your faction proposals!"), at release F's level: a founding story,
sayings, what they honour and fear, titles, messengers, gear by role, faith, town plan, voice, touchpoints at the gate
that play out on the map, an ask, a patch on the world events that should come from them, names for peoples that arise
in their ways and an Almanac page; and the age they live in moves over the years by the rule of "Ages that change over
time", the Sunken Vault by its own way down. Built only from the player's active mods (`docs/player-mods/`, the rows
with `active` yes), always softly, with the base game's fallback. Code in `Source/ColonyCouncil/Peoples/`: `PeoplesG.cs`
(helpers, the job driver, the Works, the Lattice), `PeoplesG2.cs` (the Choir, the Vault, the dev actions), `PeoplesGMath.cs`
(pure, linked into the tests: `Tools/Tests/PeoplesGTests.cs`, "peoples, release G: "). No game hook of their own. Defs:
`Defs/PeopleDefs/Rimfolk_People_HelixWorks.xml`, `_Lattice.xml`, `_Choir.xml`, `_SunkenVault.xml`,
`Rimfolk_TownPlans_PeoplesG.xml`; `Defs/IncidentDefs/Rimfolk_Events_PeoplesG.xml`, `Defs/MissionDefs/Rimfolk_Missions_PeoplesG.xml`,
`Defs/FlavorPoolDefs/Rimfolk_Lines_PeoplesG.xml`, `Defs/OutsiderTalkDefs/Rimfolk_OutsiderTalks_PeoplesG.xml`,
`Defs/ThoughtDefs/Rimfolk_Thoughts_PeoplesG.xml`, `Defs/Misc/Rimfolk_PeoplesG_Misc.xml` (the jobs, the gene fault),
`Patches/Rimfolk_PeoplesG.xml`, Keyed `Rimfolk_PeoplesG.xml`; the Almanac's page *the Works, the Lattice, the Choir and
the Vault*.

| | The Helix Works | The Lattice | The Choir of the Spire | The Sunken Vault |
|---|---|---|---|---|
| defName | `Rimfolk_HelixWorks` | `Rimfolk_Lattice` | `Rimfolk_Choir` | `Rimfolk_SunkenVault` |
| Calling | editors | mechanitors | listeners | vaultfolk |
| Age | Spacer (outlander parent) | Spacer | Spacer, with the archotech's flavour (beyond spacer by the design) | Spacer, falling |
| Its age over the years | 9; at the height already, falls back when cut off | 10; the same | 16; the same | declines: an age about every 4 years unsupplied (`ageDeclines`, `ageDeclineYears` 4), never below the medieval age (`ageFloor` 1), held by components and parts |
| Arises in (release B) | industrial and spacer | industrial and spacer | every age | industrial and spacer |
| Founding | the lab's founders fled a plague in the vats and left them running; the eldest of the lab's own grown, Orla Venn, kept them warm, read the founders' notes aloud to the others they had made and taught them to edit what they were given; her genome is the first entry in the ledger | on a field the war had left a hundred mechs stood still round their fallen commander; Tamsin Arro, a deserter with a mechlink, linked the first ten to her own mind and taught three others to take ten each, and their minds began to share a quiet | a herder called Ilsabet Ruen lay down one night at the foot of the Spire and woke able to hear it think; others came to sit with her, their children were born hearing it, and they built a ring of halls so that someone is always listening | the vault's water ran short generations after the doors were sealed; the council chose Overseer Hollis Crane to open them, and she broke the seal and walked out first; half of them went back below for a week |
| Sayings | Every body is a draft. We are only editors. Ask before you change it. Mark the change. Read twice, cut once. | A mind alone is a candle. A lattice is a lamp. Every link holds two. Nothing linked is lost. Quiet hands, exact work. | The Spire is thinking. We are what it thinks about. Listen first. The hum is not silence. Be ordinary, if you can. | The doors opened. The world we were waiting for didn't come with them. Every cell counts. Number off. Mind the seal. |
| They honour / fear | a clean edit, a child who chose their own genes and a sample freely given / a batch gone wrong, a change made without asking and the vats going cold | a link held true, a mech mended to its last part and work done exactly / a broken link, a mech gone feral and a mind left on its own | a quiet mind, a song held with others and a stranger who listened / the hum going silent, a mind that shouts and the void that listens back | a part kept working, a number answered at the roll call and a craft learned again / the last cell going dark, a door that will not close and the sky with nothing over it |
| Signature | the lab coat in clinic mint (Anomaly's, Vanilla Apparel Expanded's) over a mint collar shirt with the helix pin; the lab guard in mint recon armour | the robe in lattice indigo, the lattice worked on its hood; bandwidth gear (Biotech's headsets) on the mech-tenders and the node | the pale robe in spire lilac (and Royalty's eltex robe in the same lilac) on everyone, nothing else alike, no armour at all | the vault suit in vault olive (collar shirt and trousers of the one cloth, the vault's number on the back), worn thin; the overseer's jacket of the same olive |
| Leader, head, heir, envoy, trader | chief editor, sequencer, second sequencer, vat-keeper, sample-buyer | node, link-warden, second warden, mech-tender, part-trader | first voice, cantor, second cantor, listener, keeper of the hum | overseer, warden, deputy warden, surface-scout, technician |
| Messengers | lab-runners | link-runners | hum-walkers | surface-runners |
| Towns | 0.3: labs | 0.3: yards | 0.25: rings | 0.25: vaults |
| Caravans | 0.6 | 0.7 | 0.4 | 0.6 |
| Raids | 0.12 | 0.15 | 0.06 (the fewest) | 0.15 |
| Faith (Ideology) | The Living Draft: transhumanist (and VE Memes' progressive and healthcare); ideological or archist; techist | The Lattice: transhumanist (and VE Memes' gestalt and mechanoid supremacy); ideological or abstract; techist | The Hum: collectivist (and VE Memes' psychic focus, Alpha Memes' monastic); embodied or abstract; Buddhist | The Seal: tunneler, loyalist (and VE Memes' scrapper); archist or ideological; techist |
| Kinds (Biotech) | 34 kinds from every source of the player's mods, about six in ten not baseliners (genies, highmates, hussars, Alpha Genes' helixan and seven more, Odyssey's starjacks, Vanilla Races Expanded's archons, androids and five more, askbarn, uredd, Avaloi, Venators, stoneborn, boglegs, korgs, chimps, pixies, Eevees) | genies, hussars, highmates, androids, archons, askbarn, ocularkin (17%) | highmates, genies, archons, animusen, ocularkin, high elves, court fairies, Avaloi (21%) | dirtmoles, genies, pigskins, wasters, stoneborn, insectors, korgs (18%) |
| Colour | clinic mint | lattice indigo | spire lilac | vault olive |

**How alike they look, role by role** (uniform only where it makes sense). The Works' every kind wears the mint shirt;
the sequencers, vat-keepers, sample-buyers and the chief editor the lab coat over it (Anomaly's; Vanilla Apparel
Expanded's lab coat in the same mint when the game picks it), the vat-keepers Vanilla Apparel Expanded's surgical mask;
lab hands and the vat-grown only the shirt and whatever the lab's lockers held; the lab guard recon armour and helmet in
mint with charge rifles (and Rimsenal's spacer pistols under the same tag); the lab's mechanitor Biotech's airwire headset
over a mechlink. The Lattice's every kind the indigo robe over trousers: link-hands in their own clothes under it,
mech-tenders the airwire headset over a mechlink, engineers tool belts and a shirt, link-wardens a flak vest and advanced
helmet under it with charge rifles or a machine pistol, the part-trader a plain shirt, the node Biotech's array headset
over a mechlink. The Choir's every kind the lilac robe and nothing of armour: listeners in whatever they wear under it,
cantors and wardens of the quiet Royalty's eltex, the keeper of the hum and the first voice a shirt beneath. The Vault's
every kind the vault suit: dwellers as it is, worn thin, with tool belts; security recon armour over it with charge
rifles; surface-scouts a duster over it with rifles; medics the surgical mask; technicians tool packs; the overseer the
olive jacket and the vault's last charge rifle.

**Every age** (the player's rule; the age they live in now, `Peoples.AgeOf`). The Works' lab guard and the Lattice's
link-wardens take the age's issue when their people fall back (plate and spears in the medieval age, flak and assault
rifles in the industrial age). The Vault, which falls by its ways, patches its suit with whatever is at hand: its
dwellers a parka over it in the industrial age, a cape in the medieval age; its security flak, a duster and assault
rifles, then plate, a cape and spears. Everyone else takes the every-age pass (`AgeArms.ToAge`).

**Gifts** (`PeopleKindExtension.gifts`): the Works' lab hands strong immunity or robust (genes put in by the hands before
them), sequencers quick study and now and then a psylink (Harmonist, Empath), vat-keepers fast wound healing and a priest
of A RimWorld of Magic, the vat-grown superclotting, the lab guard fast wound healing or a super soldier, the lab's
mechanitor a technomancer, the chief editor super-strong immunity and superfast wound healing, a psylink of two to four
levels (20%) or a chronomancer. The Lattice's link-hands, engineers and mech-tenders technomancers, link-wardens a
psylink of one to three levels on the Technomancer's or Protector's path (12%), the node a Technomancer's psylink of two
to four (25%). The Choir's listeners a psylink of one or two as Empaths or Harmonists (35%) or psy-sensitive by birth,
cantors two to four (Harmonist, Empath, Archotechist, 45%), the archite gene of perfect immunity or a bard's class,
wardens of the quiet three to five (Protector, Puppeteer, Staticlord, half of them) or a psionic's class, the first voice
four to six (Archotechist, Harmonist) or the archite gene of agelessness. The Vault's dwellers technomancers, security
super soldiers, scouts rangers, medics strong immunity, the overseer a Protector's psylink of one to three (15%).

**Their towns' maps** (`Rimfolk_TownPlans_PeoplesG.xml`, only the base game's buildings). The Works' **lab** (36 by 15):
the sterile hall with two hi-tech research benches, the vat hall's four cradles (hospital beds), the stores, quarters of
eight beds with a table, and a quiet garden of roses round two willows. The Lattice's **yards** (38 by 17): the control
tower with its console, the gestation hall with two machining benches and the stores of parts, a bunkhouse of eight
beds, and a yard of eight batteries on concrete under four mini-turrets. The Choir's **ring** (37 by 15): a pale monolith
(a grand marble sculpture) on marble flagstones among four columns, and four quiet halls round it, two of sleeping mats,
one of books, one of stores. The Vault's **door** (36 by 18): the vault behind plasteel walls and autodoors, twelve
bunks, the console that talks to nobody, a battery, its stores; and outside it the surface camp, six bedrolls round two
fires and two potato fields inside a fence.

**Theirs at the gate** (`Rimfolk_Events_PeoplesG.xml`; each only from a people of its calling at peace with the colony,
each with three answers, the last always to send them on). The Works' sample, gene work and batch, and the Lattice's
repairs, lost mech and part-trader want Biotech (`HappeningExtension.mods`); the vat-keeper's case and the engineers'
turret come with or without it, as the design asks ("without Biotech they trade medicine and drugs" and "come as
engineers with turrets").

- *A sample for the Works* (`HappeningWorker_HelixSample`, `DilemmaEffect_HelixSample`, `HelixSample`): the colonist
  whose genes are rarest to the Works (`PeoplesGMath.Rarity`: an archite gene three, a xenotype's own genes one each to
  twelve, a plain baseliner nothing; three or more is worth a sample) is asked for a sample at 80 silver and 25 a point
  (at most 680). Taken, the colonist keeps still where they are (a forced wait), the sample-buyer walks over and draws it
  (a bar and the healing cross), a little blood from the arm (`HediffDefOf.BloodLoss` 0.08, below the game's first stage
  at 0.15, gone in a few hours: the colony's answer, not a mishap, so not a harm roll), and pays at the gate. The colony's
  doctor may watch (+Medicine).
- *Gene work at the gate* (`HappeningWorker_HelixGeneWork`, `DilemmaEffect_HelixGene`, `HelixGenes`): a sequencer offers
  one of the Works' gentle Biotech genes (strong immunity, fast wound healing, superclotting, strong stomach, tox
  resistance, robust, dark vision, cold or heat tolerance, reduced pain) the colonist lacks and that conflicts with nothing
  they carry, for 180 silver, 90 for each point of metabolism and 40 of complexity, or a genepack; the colonist keeps still
  while the sequencer works on them two hours, then carries it as a xenogene with the game's own xenogerm mark (no other
  until it settles).
- *The vat-keeper's case* (`HappeningWorker_HelixCase`, `DilemmaEffect_PeoplesGCrate`): medicine (4 to 12), glitterworld
  medicine (1 or 2) and penoxycyline (3 to 8) by the colony's wealth, a tenth over their worth, the case set down at the
  gate; the colony's doctor may go through it with them (+Medicine).
- *A batch gone wrong* (`HappeningWorker_HelixBatch`, `DilemmaEffect_HelixBatch`): one to three of the Works' vat-grown
  come with a vat-keeper, sick with a gene fault (`Rimfolk_GeneFault`, tendable, never lethal, put only on them). The
  colony's doctor sees to each where they stand, a medicine from the stores for each (`OutsiderTend`, the colony's way of
  tending a stranger at the gate), or two medicine each go out with them; the Works think 8 better of a colony that tended
  them, 5 for the medicine.
- *A mech-tender at the gate* (`HappeningWorker_LatticeRepair`, `DilemmaEffect_LatticeRepair`, `LatticeRepair`): the
  colony's hurt mechs (the game's own `MechRepairUtility.CanRepair`, six at most) mended one by one where they stand, each
  kept still at its turn (sparks and a bar), the game's own repair run until it wants none; 40 silver a mech and 15 a
  wound (at most 600), or a basic subcore.
- *A lost mech* (`HappeningWorker_LatticeLostMech`, `LatticeLost`): a lifter, constructoid, cleansweeper or paramedic of
  theirs wanders onto the map and drifts near the colony's edge. Send word: a link-warden walks in from the edge to it,
  relinks it (sparks and a bar) and the two walk home together, the Lattice +6 (`DilemmaEffect_LatticeReturn`). Keep it:
  only with a mechanitor of the colony's who can take it on (`MechanitorUtility.CanControlMech`, the game's own test of
  bandwidth), its overseer from then (`PawnRelationDefOf.Overseer`), the Lattice -15 (`DilemmaEffect_LatticeKeep`). Or
  let it find its own way home.
- *The part-trader* (`HappeningWorker_LatticeParts`, `DilemmaEffect_LatticeParts`, `PlayCarry.MechParts`): the colony's
  subcores and mech chips (150 silver's worth at least) carried out and paid a fifth over their worth; the colony's
  crafter may look at their tools (+Crafting).
- *A turret for the gate* (`HappeningWorker_LatticeTurret`, `DilemmaEffect_LatticeTurret`, `LatticeTurret`): for an
  industrial or spacer colony, an engineer raises a mini-turret on clear ground a few cells in from the gate (two hours,
  sparks and a bar), the colony's from then and on the colony's power, at its worth and a tenth; the colony's builder may
  help (+Construction).
- *The Choir listens* (`HappeningWorker_ChoirListening`, `DilemmaEffect_ChoirListen`, `ChoirListening`): a cantor and one
  or two listeners sit in a ring round the colony's gathering spot (or its middle) for three hours (the meditation's mark
  over them), and then everyone at home feels it (`PeoplesGMath.Felt`): the psychically deaf nothing, the rest an unease
  one time in five, more for the psychically sensitive (a third more for each point over the ordinary), more still when
  the void stirs (Anomaly: the monolith awake, `Find.Anomaly.Level`), never past three in five; a calm otherwise.
  Shown as moods (`Rimfolk_Thought_ChoirCalm` +4 for a day and a half, `_ChoirUnease` -3 for a day) and told by name. The
  colony's most sensitive may sit with them: always calm, and a psycaster's focus the fuller.
- *The keeper of the hum* (`HappeningWorker_ChoirArt`, `DilemmaEffect_ChoirArt`, `ChoirGoods`, `PlayCarry.Art`): the
  colony's art packed up (two pieces at most, the dearest, 300 silver's worth at least) for one of the Spire's goods (a
  psychic soothe or animal pulser, an archite capsule, a psychic harmonizer or sensitizer, a healer mech serum, an eltex
  skullcap, whatever the game and the DLCs have), the dearest no more than a quarter over the art's worth, set down at the
  gate; the colony's artist may talk the work over with them (+Artistic).
- *The vault's last goods* (`HappeningWorker_VaultBarter`, `DilemmaEffect_VaultSupply`): a technician's crate of the
  vault's last spacer goods, fewer the further it has fallen (glitterworld medicine, an advanced component, a charge rifle
  at the spacer age), for 4 to 12 components and 40 to 120 raw food by the colony's wealth, carried out; or the colony
  gives them for nothing (+8). Either way the vault remembers it as supply (`VaultSupply.Noted`, half again as much for
  the gift).
- *A craft the vault lost* (`HappeningWorker_VaultCraft`, `DilemmaEffect_VaultTeach`, `VaultCraft`): the colony's best
  crafter teaches their technician at the bench where they work (`WanderYear.Bench`) for three hours, another colonist
  learning it again beside them if the colony likes; the vault remembers it as supply worth 300 silver and as one of
  theirs who learned at the colony (`learnedHere`), and the technician takes it home.
- *A child who never saw a sky* (`HappeningWorker_VaultChild`, `DilemmaEffect_VaultSky`, `VaultChild`): a surface-scout
  comes with a vault-born child (nine to twelve with Biotech, else a youth of fifteen to seventeen) who keeps looking up
  and asks to stay. Taken in, the child joins the colony as anyone does (`DilemmaEffect_Join`; the colony is the family,
  nothing of houses or lines), stands under the open sky by the gate a while and says so, and the scout goes home alone;
  or the child stands under the sky a while before going home with the scout; or they are sent home.

**Their asks** (`Rimfolk_Missions_PeoplesG.xml`, each only theirs): the Works' genepacks (medicine where the colony has
none or there is no Biotech, `MissionGoalSpec.instead`), the Lattice's two good crafters (Crafting 6) for a day at the
yards (`Rimfolk_Mission_LatticeHands`, a day they remember, `Rimfolk_Mission_HandsWithLattice`), the Choir's listener
who stays four to six days to learn what it is to be ordinary (`HappeningWorker_MissionChoirStay`, hosted at the
colony's table and in a bed of its own; each afternoon an hour beside someone of the colony at their work, the one they
watched a little prouder, `Rimfolk_Thought_ChoirFollowed`), the Vault's power cells (components, or steel and components
for the seal), which keep its age as supply does (`Patch_FactionMemory.MissionEnded`, kind `vaultcells`).

**By patch** (`Patches/Rimfolk_PeoplesG.xml`, conditional): world events that prefer a calling, five times as likely as
another people: a famous healer (the Works), a town rebuilding (the Lattice), a season of wild magic (the Choir), a new
town (the Vault).

**Release B's peoples that arise may take their ways** (`ArisingMath.Lean`, its 20-argument form): a band of the
banished led in the spacer age by one with a psylink of four or more listens as a choir (before the seers, who take any
psylink), after all the others by a surgeon (Medicine 10) in the spacer age edits genes as a works, by a mechanitor (a
mechlink) in the industrial age or later links its mechs as a lattice; survivors of an industrial or spacer town, after
the Line's and the crew's ways, on their fated quarter (`VaultChance`, `SaltVault`) sat out its fall in its shelter and
come up again as vaultfolk. Their words: `CC_Arise_refugees_vaultfolk`, `CC_Arise_exiles_listeners`, `_editors`,
`_mechanitors` (each two ways), three names each (*the Barr Shelter*, *the Choir of Barr*, *Barr's Works*, *Barr's
Lattice*, each with two other forms). Each people's rise and fall told in its own voice (`CC_PeopleAge_Voice_*`).

**Harm.** Nothing of theirs rolls a harm. The sample's draw is the colony's answer and is shown, a little blood below
anything the game counts as weakening; the gene fault is the Works' own batch's, never the colony's.

**Their pace on the map.** Their callers come and go at an errand's pace (`LordJob_VisitOnErrand`, `Pace.Errand`): the
gate's visit lord, the lord that keeps one of them at their work's spot (`PeoplesG.Errand`), and their way home
(`LordJob_ExitMapBest` at an errand's pace, a mech with its link-warden).

**Saved**: nothing new. What is under way waits on the job that does it (a sample, a gene, a mending, a listening or a
lesson loaded halfway starts again or is let go; their visitors go home on their hours); the vault's supply is a memory
of the peoples' memory.

**Softness**

| Missing | What happens |
|---|---|
| Biotech | Humans only; no sample, gene work, batch, mech repairs, lost mech or part-trader; the Works trade medicine and drugs (the case, their stock), the Lattice come as engineers with turrets; no mechs in their groups, no mechlinks, headsets or genes; the vault's child is a youth. |
| Ideology | No fixed faiths or styles. |
| Royalty | No psylinks; no eltex robes, the Choir's goods without the harmonizer, sensitizer or skullcap. |
| Anomaly | The Works without Anomaly's lab coat (Vanilla Apparel Expanded's, or the mint shirt); the Choir's listening never with the void's stirring. |
| Vanilla Psycasts Expanded, A RimWorld of Magic | Psylinks on the game's own way; no classes. |
| Vanilla Apparel Expanded (and Accessories), Rimsenal, Alpha Memes, VE Memes, the xenotypes' mods | The game's own pieces and kinds of people; fewer memes. |
| Answers play out on the map (setting) off | The draw, the gene, the mending, the relinking, the turret, the listening, the lesson and the sky done at once. |
| The deeper game hooks (setting) off | The game's own bases for their towns; no age arms, kits or gifts. |

Not used: the game's xenogerm items in their stock (an empty xenogerm would be worthless; the gene work at the gate is
the Works' trade in them), a growth vat or a mech gestator in their towns (not the base game's), real archotech
structures for the Choir (the Spire is their story; Rimfolk's ages end at spacer).

## What was built (the peoples' own events, round 1)

The player (10 November 2026): "I'd first like more faction specific events, interactions etc. like this event i had
with these gene guys who gave my pawn a new gene ... these should be applied to the other factions according to their
lore, design and theme", then "also some faction-specific camps can be included in the events". The proposal
(`docs/peoples-events-proposals.md`, 100 items) was approved in full and split into ten rounds; this is the first:
items 10, 5, 47, 83, 51, 1, 19, 17, 34 and 95. DESIGN.md "Rimfolk's peoples" has the code; here, what each people does.

| People | Item | What happens on the map | Its age | The player's mods |
|---|---|---|---|---|
| The herding folk | A beast for a colonist (10) | A rider leads in a young beast the elder chose for one colonist; bought or traded for a young beast of the colony's; bonded, trained to follow, led home together; the rider stays to supper | The same beast in every age | The land's beasts (Alpha Animals, Vanilla Animals Expanded, Megafauna where they live), a riding beast likelier with Giddy-Up 2 |
| The hospitaller order | A limb fitted (5) | The colonist lies at the colony's bed, the surgeon (Medicine 10 to 16) fits the limb, the colony's doctor at the bedside; the game's own surgery and chance; a favour owed, or medicine sent back | A peg leg or wooden hand from the colony's wood before the industrial age; simple prostheses after; never bionics | The base game's limbs (the player's mods add none of the order's kind) |
| The Stormglass Conclave | Storm-sense (47) | In a thunderstorm the most sensitive colonist stands an hour out under the sky with a reader, at the colony's rod if it has one; a flash, and a first psylink | The reader's in every age | Royalty's psylink, Vanilla Psycasts Expanded's Staticlord or Frostshaper path; without Royalty A RimWorld of Magic's Lightning tome |
| The Lattice | A mechlink (83) | Two hours where the colonist stands, sparks and a bar; the mech that came with the tender is theirs | Spacer; a tribal colony finds it strange | Biotech; Alpha Mechs' culinarius and turbocleaner beside the lifter and constructoid |
| The Mossborn | A Gauranlen seed (51) | Once the ledger is warm: planted where the colony chooses (the game's own rule for the seed) or where she does, the colonist beside her; the tree bound to them; the promise reckoned each season | The same seed in every age | Ideology (Vanilla Ideology Expanded's dryads with it); without Ideology a healroot grove |
| The road league | Crates held in trust (1) | Six to ten sealed crates carried into the stores; the fetch twenty to thirty days on; good ink or red | A lame pack beast, a broken truck, a lander short of lift (its crates by pod) | None needed |
| The learned society | What lies in the old place (19) | Two fellows camp four days beside an old place, measuring; then exactly what is inside, and research; or they open it | Instruments of their age in the words | The game's ancient dangers; Odyssey's and Vanilla Landmarks Expanded's landmarks; Geological Landforms' landforms |
| The pilgrim fellowship | A hostel on the road (17, camp) | In a holy year or a great pilgrimage: a long hut, the shrine's flame, a bread table; the region's travellers a night or two with their news; bread each morning; the evening blessing and its cough; a quarrel at the fire | Poles and bedrolls, or folding cots and a lamp | A brazier with Royalty |
| The Lantern Wardens | The lantern post (34, camp) | Three Wardens and two hounds at the land's edge while a great beast roams it; the trail by day, the lantern by night; a warning; meat for the hounds; the colony's best shot on the trail; the beast at the fire, a hound lost | Spears or rifles by their age's arms | A German shepherd with Vanilla Animals Expanded |
| The Wardens and the Circuit | Kill it or win it over (95) | Both at the gate the same day over a legend or an alpha: the hunt, the colony's own taming with the tamer beside, or a bout at the gate | Each people's own | The legends of the region's beasts (Megafauna, Dinosauria, ReGrowth, Alpha Animals, PokéWorld's legendaries where PokéWorld opens them) |

Gifts (the beast, the limb, storm-sense, the mechlink, the seed) come once to a colonist and never within twenty days of
each other, so a long game sees a handful, each one a story. What the colony gives for them: silver at a fair price, a
young beast, wood, medicine, subcores, a vial of its storm, a promise to the ledger, or a favour owed.

What can hurt, at the game's own rate and shown: a fitting that fails (the game's surgery), a beast's revenge on a
failed try at taming it (its own chance, shown on the card), the beast at the Wardens' fire (the game's fight), an old
place opened (its sleepers awake), the cough round the hostel's hut and a brawl between travellers (Rimfolk's harm
budget, the pilgrims' cough and a camp brawl's rate).

| Without | What happens |
|---|---|
| Royalty and A RimWorld of Magic | No storm-sense. |
| Biotech | No mechlink. |
| Ideology | The Mossborn sow a healroot grove. |
| Giddy-Up 2 | Any beast that fits the land, a riding one no likelier. |
| The camps (setting) off | No hostel, no lantern post, no scholars at an old place. |
| Answers play out on the map (setting) off | The work on a colonist done at once. |
| The deeper game hooks (setting) off | Good ink's prices and red ink's stopped caravans don't apply; the rest does. |

## What could come next

- Their own world events: a league's toll war, an order's plague year with the colony's help asked by name, a great
  drove to the far market.
- A league trading post in the colony's region (Rimfolk's trading posts) and the league's book of fair dealers shown on
  the Peoples page.
- A visitor of the order who stays a season at the colony's hospital.
- Their notables' own small stories (a waymaster's caravan lost, a prior's illness).
