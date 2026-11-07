# Rimfolk's peoples

New peoples (factions) for Rimfolk, built mostly from what the game, the DLCs and the player's mods already draw. The
player's words, 7 November 2026: "we can start with a few factions and see how it goes, and then further expand on
those. reuse and create at your own discretion. they should also be able to arise during play so that i will see new
factions in an existing run", and "use all dlc and mods to your availability".

Three releases. **Release A** (7 November 2026): three peoples of peaceful callings the world lacked, made when a new
world is made. **Release B** (7 November 2026, below): peoples that arise during play, so an existing save sees new
ones, built on release A's templates and code. **Release C** (7 November 2026, below): four more peaceful peoples, the
pilgrims, the scholars, the spacer enclave and the river folk, which release B's peoples that arise may also take the
ways of. Their gear, kinds of people and gifts were reworked after (7 November 2026, "Their gear, their kinds of people
and their gifts", below): the tables' looks of each release are as first built. Then all seven were brought to the level
of the eight new peoples of `docs/design/peoples-next.md`: a founding story, sayings, what they honour and fear, a fuller
voice and a touchpoint each ("Their stories, sayings and touchpoints", below).

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
  wayfarers wayfarer 3% and minstrel 2%, pilgrims monk 3%.
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

## What could come next

- Their own world events: a league's toll war, an order's plague year with the colony's help asked by name, a great
  drove to the far market.
- A league trading post in the colony's region (Rimfolk's trading posts) and the league's book of fair dealers shown on
  the Peoples page.
- A visitor of the order who stays a season at the colony's hospital.
- Their notables' own small stories (a waymaster's caravan lost, a prior's illness).
