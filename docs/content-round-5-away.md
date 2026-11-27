# Content round 5: the colony out in the world (27 November 2026)

Built on the player's go of 27 November 2026: "you can go ahead with the content round, maybe also other content, camps,
dilemmas, world events, just further expanding on the mechanics you already have or adding more, so to say, content to the
mechanics ... adding more mechanics for the time being doesn't necessarily make it better since it's already just a lot."
After three rounds were built the player said: "continue".

So everything here is content inside the frames that exist: six missions (`Defs/MissionDefs/Rimfolk_Round5_Missions.xml`)
and eight encounters (`Defs/EncounterDefs/Rimfolk_Round5_Encounters.xml`), their moods in
`Defs/ThoughtDefs/Rimfolk_Round5_Away_Thoughts.xml`, words in `Languages/English/Keyed/Rimfolk_Round5_Away.xml`
(`CC_R5A_*`), one small worker in `Source/ColonyCouncil/Missions/Round5Away.cs` (an order whose goods are picked from what
the colony keeps in its stores), its pure rules in `Source/ColonyCouncil/Missions/Round5AwayMath.cs` and the test
`Tools/Tests/Round5AwayTests.cs`. No new system, window, setting, saved collection or `Sched` offset: what a mission keeps,
it keeps in the `Mission` fields there are (its goals, its variant, its pay).

## The rules on every item

- **The frames there are.** Four missions are told from data on the frames of the fourth round of neighbours
  (`MissionHands`: hands lent to a town for a few days; `MissionEscortWho`: a traveller walked home by the colony's
  caravan) and of the deliveries (`MissionDelivery`: goods collected at the gate by their carts). One is a delivery whose
  goods are picked by a small worker of its own (`HappeningWorker_R5AOrder`, over the delivery's own start, checks and
  carts). The encounters are `EncounterDef`s with the dilemma effects and scenes there are (a visit, a lesson in the
  skill, a look at a building of the colony's, the wild one too close).
- **Where it plays out.** A hands mission plays out on the world map: the colony's caravan with the people asked for
  reaches the town and waits there while they work, and comes home with the pay and a gift in its packs (the Quests tab
  shows the hours). A delivery plays out at the gate: their carts come in and take the goods from the stockpiles, and pay
  there. The walk home: the traveller comes to the colony and waits by its fire until a caravan sets out for their town.
  The encounters happen where the colonist and the stranger are together (the encounters' own rule), and a visit or a look
  at the loom is walked on the map.
- **Every age.** Each mission's opening and its endings come in the asking people's age (`their:tribal`,
  `their:medieval`, `their:industrial`, `their:spacer`), the Empire in its own titles and honour (`their:empire`), and in
  the name of the town's head when the region knows them (`[host]`; all but the walk home, whose frame names none). The bell's goods follow the colony's own means (a
  drum, a bell, a siren, a beacon). Each encounter has lines for a visitor of a low-tech people and of a high-tech one
  (`theirLowtech`, `theirHightech`) or for the colony's own age. Nothing is switched off for an age.
- **The gods are unseen.** None of the fourteen names a god or says a god did anything. Two touch faith, both the other
  people's own and plainly told: the carver's work for their house of prayer, and a visitor's grace before eating.
- **The colony is the family.** Nothing reads kin, houses or lines; who goes is chosen by skill.
- **Harm** only in one item, the wild one too close (E3), through `Harm` and `HarmKinds.TooClose` at the game's own
  taming-revenge chance for the kind, shown with a mote and a line. The missions hurt nobody: a hurt at a far town could not
  be seen where it happens, and the frames' own hurts are written for their own work (the harvest's sore hands). Their
  risk is the colony's standing with the people who asked (goodwill and what they remember), and for the walk home the
  game's own dangers of the road, which no line overstates.
- **No countdown** in any line: the Quests tab shows the time. No em dashes or exclamation marks.
- **Text:** plain, neutral words (CLAUDE.md: missions and encounters keep to the plain rule); spoken lines none.
- **Mods:** only `active` rows of `docs/player-mods/`. The order's goods are whatever the colony has made and stored, of
  any mod; the deliveries' groups (cloth and leather, drink, meals) take every mod's; the loom encounter names the active
  mods' looms and tailoring tables by name (softly: a name that isn't loaded never matches), the game's tailor benches
  first; strangers and wild animals are whoever the player's mods bring.

Spread: help (the round-up, the carver, the cook, the bell, the captive walked home), trade (the order, the trader's
scales), faith (the carver's house of prayer, the visitor's grace), the neighbours (all six missions, the boundary), the
land (the forest, the snowfields, the mountains, the open grass), spring, summer, autumn and winter, and one with a risk.

---

## Missions (R5A-M1 to R5A-M6)

**R5A-M1. Hands for the round-up** (`Rimfolk_R5A_Mission_RoundUp`, `MissionHands`, kind `herding`)
- **When:** spring to autumn, a friendly people with a town within three days' caravan, and two of the colony's people
  good with animals (Animals 5 or more, able to handle them) it can spare with others left at home.
- **Who goes:** two of the colony's handlers, by caravan, about a day and a half at their town.
- **What happens:** their herds are brought in: the lambing in spring (`lambing`), the shearing in summer (`shearing`),
  the round-up before winter in autumn (`roundup`). The caravan reaches the town and waits while the two work.
- **Outcomes:** `worked` (pay and a gift of the town's in the packs, Animals learned, their people remember who came),
  `partial` (half the stay, half the pay), `missed` (nobody came; goodwill lost), cancelled if the town or the friendship
  is gone.
- **Ages:** a tribal people's herd brought down from the high grass to the winter camp; a medieval town's folds and
  shearing floor; an industrial ranch's corrals, the dogs and the stock trucks; a spacer settlement's gene-bred herd,
  where the machines can't do the handling; the Empire's estate herds, asked in the steward's name.
- **Harm:** none. **Mods:** none needed.
- **Not a duplicate of:** hands for a harvest (the fields), fodder for a neighbour's herds (goods), a young beast raised
  or the tamers' foal (an animal kept at the colony), the stud visit, the strayed herd (a storyline on the colony's
  land), an animal tamed for them, a sick animal tended; no mission sends the colony's handlers to a neighbour's herd.

**R5A-M2. Carvings for their hall** (`Rimfolk_R5A_Mission_Carver`, `MissionHands`, kind `carver`)
- **When:** any season, a friendly town within three days, and one of the colony's artists (Artistic 6 or more, able to
  do art) it can spare.
- **Who goes:** the colony's best artist, by caravan, about thirty hours at the town.
- **What happens:** their new hall (`hall`) or their house of prayer (`holyhouse`, their faith, nothing of the colony's)
  is built and they want the carving done by someone better at it than they are: posts, a screen, doors, panels.
- **Outcomes:** `worked`, `partial`, `missed`, cancelled, as above (Artistic learned).
- **Ages:** carved posts for a tribal longhouse or spirit house; a carved screen and beams for a medieval chapel or
  guildhall; the doors and the panelled front of an industrial town's meeting house; hand-carved panels for a spacer
  settlement's common room, where handwork is rare; the knight's hall for the Empire.
- **Faith:** the house of prayer is the other people's own, named only as theirs; no god named.
- **Harm:** none. **Mods:** none needed.
- **Not a duplicate of:** a piece for a wedding (made at home and sent), a shrine for the pilgrims (built at the colony),
  builders for a neighbour (construction), the woodcarvers' camp (carvers come to the colony), a commission or the art
  collector (the colony's art bought).

**R5A-M3. An order from their merchants** (`Rimfolk_R5A_Mission_Order`, `HappeningWorker_R5AOrder` over
`HappeningWorker_MissionDelivery`, kind `order`)
- **When:** the colony keeps a good amount of something it makes (at least ten, worth at least 120 silver in all): meals,
  cloth and leather, medicine, drink, stone blocks, or any one thing a recipe of the game or a mod makes (components, a
  mod's cheese or tea, chemfuel), never weapons, clothes, drugs or silver; a friendly neighbour.
- **Who:** their merchants write to the colony's reeve (or whoever deals with neighbours), in the town head's name when
  the region knows them.
- **What happens:** they ask for about as much of it as the colony keeps, give or take a third (`Round5AwayMath.OrderCount`,
  never more than about 1200 silver's worth at the colony's wealth), collected at the gate by their carts once it is ready
  or when the time is up, paid at half again its plain worth (`OrderPay`). The order names the goods (`food`, `cloth`,
  `medicine`, `drink`, `stone`, `goods`).
- **Outcomes:** the delivery's own: `delivered` (all of it, well paid), `partial` (half at least, at its plain worth),
  `missed` (goodwill lost).
- **Ages:** a tribal people's traders who carry goods between the camps; a medieval town's merchants' guild; an
  industrial town's trading house or store; a spacer settlement's exchange; the Empire's steward ordering for the
  knight's household.
- **Mods:** whatever the colony has made and stored, from any active mod (the full pool by itself), the game's own
  groups first.
- **Harm:** none.
- **Not a duplicate of:** the deliveries (a feast, rebuilding, provisions, a wall, fodder, stores for the road, seed
  grain: each a need, with fixed goods), a store kept, the arms sold off (the colony buys), the game's traders and the
  trade caravans (bought and sold on the spot).

**R5A-M4. A cook for their feast day** (`Rimfolk_R5A_Mission_Cook`, `MissionHands`, kind `cook`)
- **When:** a friendly town within three days and one of the colony's cooks (Cooking 6 or more, able to cook) it can spare.
- **Who goes:** the colony's best cook, by caravan, about a day at the town.
- **What happens:** their feast is coming and their kitchen needs a good cook: the spring feast (`springfeast`), the
  summer fair (`fair`), the harvest home (`harvesthome`), the midwinter feast (`midwinter`), by the season.
- **Outcomes:** `worked`, `partial`, `missed`, cancelled (Cooking learned).
- **Ages:** a tribal people's feast in the longhouse after the hunt or the harvest; a medieval town's harvest home in the
  tithe barn; an industrial town's supper in the grange hall; a spacer settlement whose kitchens print their food and
  want real cooking for once; the knight's table for the Empire.
- **Harm:** none. **Mods:** none needed.
- **Not a duplicate of:** goods for a funeral or a wedding (meals and drink sent), the town's festival (the colony's
  entertainers or guests), the town's day (guests), food for the hostel (a people's delivery), musicians for a night (at
  home).

**R5A-M5. A captive walked home** (`Rimfolk_R5A_Mission_Captive`, `MissionEscortWho`, kind `captive`)
- **When:** a friendly people with a town within four days, and two grown people at the colony.
- **Who:** one of theirs who was taken by raiders and got away (`escaped`), or was let go when their people paid
  (`released`), is making for the colony as the nearest friendly place. They come to the colony and wait by its fire;
  any caravan of the colony's that sets out for their town takes them along.
- **Outcomes:** the escort's own: `arrived` (pay at their town, the people remember who walked with them), `alone`
  (nobody went), `turned` (the caravan turned back), `harmed` (they died or were taken prisoner on the colony's watch),
  `late`, cancelled.
- **Ages:** by the asking people's (a tribal people's runner, a medieval town's rider, an industrial town's radio message,
  a spacer settlement's signal), the Empire's steward for a servant of the Empire.
- **Harm:** none of the item's own; the road is the game's, and the words say only that the one walked home is frightened
  and doesn't want to walk alone.
- **Not a duplicate of:** a pilgrim to escort, a traveller seen home (a trader, a healer, a scholar), the League's factor,
  refugees (a group hosted), a wounded messenger, a hostage returned (the wards).

**R5A-M6. A bell for their watch** (`Rimfolk_R5A_Mission_Bell`, `MissionDelivery`, kind `alarm`)
- **When:** a friendly neighbour; any season.
- **What happens:** their town wants a way to call everyone in when raiders or fire come, and asks the colony for what it
  takes, by the colony's own means: a great drum (`drum`, a tribal colony: cloth or hide and wood), a bell (`bell`,
  medieval: steel and wood for the frame), a siren and a clock (`siren`, industrial: steel and components), an alarm
  beacon (`beacon`, spacer: components and plasteel, steel where there is none). Collected at the gate by their carts.
- **Outcomes:** `delivered`, `partial`, `missed`.
- **Ages:** the goods by the colony's age; the words by the asking people's (a tribal people's drum for the camps, a
  medieval town's watch bell, an industrial town's siren on the water tower, a spacer settlement's beacon), the Empire's
  steward for the knight's tower.
- **Harm:** none. **Mods:** the groups take every mod's cloth and leather.
- **Not a duplicate of:** a neighbour's wall (a palisade, a wall or a tower: defences), rebuilding after a fire, a storm
  or a flood, parts for the Wheel or the lift (a people's own), the beacon fires (a world event: a chain of fires lit).

## Encounters (R5A-E1 to R5A-E8)

**R5A-E1. The trader's scales** (`Rimfolk_R5A_Enc_Scales`; Trader; trade)
- **When:** a trader on the map, with one of the colony's grown people near them; any land.
- **What happens:** the trader weighs out a deal on their own scales, then shows the colonist how to tell an honest scale
  or measure from a crooked one: the beam and the stone weights of a low-tech people, a sprung balance or a counter of a
  high-tech one.
- **Effects:** Social learned (250), a mood (an honest measure, +2).
- **Not a duplicate of:** a haggling triumph or humiliation, cheated by a trader, a free taste, known for hard bargains
  (one of its lines has a trader bring their own scales, for a thrifty colony), the enclave's exact figure (a price worked
  out).

**R5A-E2. A visitor's grace** (`Rimfolk_R5A_Enc_Grace`; Visitor; faith, theirs)
- **When:** a grown visitor near one of the colony's grown people; any land.
- **What happens:** they sit down to eat together, and the visitor says their people's words of thanks over the food
  first: for the food and the hands that made it, for the road, for the people who aren't at the table. The colonist
  waits, and asks what the words mean. No god is named.
- **On the map:** the colonist goes over to the visitor and the two sit down at a table together (a visit, an hour or so).
- **Effects:** a mood (+2), the visitor's good opinion, the colony a little more open.
- **Not a duplicate of:** a pilgrim's prayer (Devout colonies, a pilgrim at the colony's ritual place), a pilgrim's
  prayer for a colonist (holy years), an enemy's prayer (the fallen), swapped recipes.

**R5A-E3. Shelter from the weather** (`Rimfolk_R5A_Enc_Shelter`; Wild; risky)
- **When:** rain, snow or a storm, outdoors, a wild grazer (no predator, body up to a muffalo's) within sight of one of
  the colony's grown people; any land.
- **What happens:** the animal is standing still with its head down in the weather, and the colonist goes up closer than
  is wise to look at it.
- **Harm:** the wild one too close (`DilemmaEffect_TooClose`, `HarmKinds.TooClose`): by the game's own taming-revenge
  chance for its kind, about one in twenty, a kick from a big one or a bite from a small one, shown with a mote and a line
  over their head, the animal running off. Nobody nervous or volatile is the one who goes closer (the colonist's tags), as
  the harm's own rule has the nervous never step close.
- **Effects:** a mood (+1).
- **Not a duplicate of:** an animal sheltering from the blizzard (Odyssey's blizzard, a mood only), a shape in the fog,
  waiting out the rain and thunder (the colony's own animals), feeding a wild animal.

**R5A-E4. A forester's eye** (`Rimfolk_R5A_Enc_Forester`; Visitor; the forest)
- **When:** the forest or the boreal forest, a grown visitor near one of the colony's grown people.
- **What happens:** the visitor asks where the colony cuts its wood and tells the colonist how their people choose which
  trees to fell and which to leave: the old ones and the seed trees left standing, the young ones thinned.
- **Effects:** Plants learned (250), a mood (+2).
- **Not a duplicate of:** the guest who pitched in (fells trees), the woodcarvers' camp, the old tree by the house.

**R5A-E5. At the colony's loom** (`Rimfolk_R5A_Enc_Loom`; Visitor; the scene `building`)
- **When:** a grown visitor near one of the colony's tailoring benches or looms (the game's hand and electric tailor
  benches, Vanilla Furniture Expanded - Production's large tailoring table, Vanilla Gravship Expanded's compact tailor
  bench, MoreFloors' wool loom).
- **On the map:** the visitor goes over to the bench and looks it over (the scene's own walk).
- **What happens:** the visitor shows the colonist a weave or a stitch their people use (a twill, a seam that keeps the
  rain out).
- **Effects:** Crafting learned (250), a mood (+2).
- **Not a duplicate of:** a cape of leaves (a rain cape woven of fronds, jungle and swamp), a scholar at the colony's
  books, a linesman at the console (the other building scenes, each a people's own).

**R5A-E6. Snow glare** (`Rimfolk_R5A_Enc_SnowGlare`; Visitor; the snowfields)
- **When:** winter on the tundra, the ice or the boreal forest, a grown visitor near one of the colony's grown people.
- **What happens:** the visitor sees the colonist squinting and shows them how their people keep the glare of the snow
  out of their eyes: goggles with a narrow slit cut in bone or wood, soot under the eyes, or tinted visors.
- **Effects:** Crafting learned (150), a mood (+2).
- **Not a duplicate of:** a trick against the cold (feet and cheeks), tracks in the snow, a frozen morning.

**R5A-E7. Sound rock** (`Rimfolk_R5A_Enc_SoundRock`; Visitor; the mountains)
- **When:** the mountains, the hills or a land of caves, a grown visitor near one of the colony's grown people.
- **What happens:** the visitor, of a people who dig, asks whether the colony has dug into the rock, and tells the
  colonist how they tell sound rock from rock that will fall: by the sound when it is struck, by the cracks, by the damp,
  or by a reading on a meter.
- **Effects:** Mining learned (250), a mood (+2).
- **Not a duplicate of:** news of the mountain roads, a pet found a cave, the cave at dusk, a mine fallen in (a world
  event).

**R5A-E8. Where the land ends** (`Rimfolk_R5A_Enc_Boundary`; Visitor of a friendly people; the neighbours)
- **When:** the open grass or the hills, a grown visitor of a people at peace with the colony near one of its grown
  people.
- **What happens:** with no river or ridge to mark it, the visitor asks where the colony counts its land as ending. The
  two talk it over and agree that the old track or a line of stones is a fair line, and that each will tell their own.
- **Effects:** a mood (+2), their people's goodwill (+2, a meeting with one of theirs).
- **Not a duplicate of:** two claims on the valley (a world event), news from the road, the ways in and out of the valley.

## Mods that would help (from `docs/player-mods`, subscribed and not enabled)

None of the fourteen needs a mod the player hasn't enabled: they draw on what is active (Vanilla Furniture Expanded -
Production's large tailoring table, Vanilla Gravship Expanded's compact tailor bench, MoreFloors' wool loom for the loom;
every active mod's cloth, leather, drink and made goods for the deliveries and the order; every active mod's peoples and
animals for who comes). Of the subscribed mods that are off, Medieval Overhaul (Workshop 3219596926) would add a spinning
wheel and more of a medieval colony's own goods to sell, but it is catalogued only up to 1.6, not the 1.7 the player plays.
