# The content round, part 4: six camps and eight map events (27 November 2026)

Built on the player's go of 27 November 2026: "you can go ahead with the content round, maybe also other content, camps,
dilemmas, world events, just further expanding on the mechanics you already have or adding more, so to say, content to the
mechanics ... adding more mechanics for the time being doesn't necessarily make it better since it's already just a lot."
After two rounds were built the player said: "continue".

So everything here is content inside the frames that exist: camp kinds (`CampKindDef`, a `CampWorker` where a kind needs
ways of its own, its state a few values in a `CampWorkState` saved with the camp) and Rimfolk events of everyday life on
the map (an `IncidentDef` with a `HappeningExtension`, its answers through the dilemma effects, what it keeps on the map one
of `OnMapTimed`'s kinds, as rounds 2 and 3 did). No new system, window, setting or saved collection. The camps fit 0.307.0's
packs and shared store: what they make, buy, are given or take goes into their store and is shared out among the packs when
they leave (`CampDayMath.Repacks`, `TakeFirst`, `Camp.TakeGoods`); what they sell is carried to the colony's gate by one of
them (`Camp.StartBring`) or, a piece of their own making, set out at the edge of their camp nearest the colony for the
colony's haulers to fetch (`CampRound4.SetOut`, the camp's share spot); nothing goes any other way.

## The rules on every item

- **Every age.** Each item takes the form of the age: a camp's titles and letters by its people's age (`[their:tribal]`...),
  an event's words and outcomes by the colony's (`[tribal]`, `[lowtech]`...). Nothing is switched off for an age.
- **The gods are unseen.** No god is named in any of the fourteen.
- **The colony is the family.** Nothing reads kin, houses or lines.
- **Harm** only where an item says so, through `Harm` and `HarmKinds` (no new kind), at the game's own rate, from something
  on the map, named under the answer before it is chosen, and shown when it happens.
- **Answers play out on the map**, with no countdown in the words; what the words say happens is what the player sees.
  Speech through `Speech.Say` (the events' moments) and the camps' `SayMoment`; bubbles one sentence.
- **Text:** camps keep the earlier flavour style (CLAUDE.md, 20 November 2026); event cards and messages plain, neutral words.
- **Mods:** only `active` rows of `docs/player-mods/`, softly (`MayRequire` or by name in code), the game's own after.
- **Saved state:** each camp's state is a few values and single references in its `CampWorkState`; each event's in the
  `TimedThing` OnMapTimed already keeps. No new saved collection, no new `Sched` offset.

Spread: the forest (the woodcarvers, the old tree), the desert and the dry grass (the fire-watchers), the ice and the snow
(the snow-carvers, ice on the paths), the coast (the wildfowlers, what the storm threw up), the marsh (the wildfowlers,
midges at dusk), the mountains (the cheesemakers, meltwater in the deep rooms), the plains (the fire-watchers, crows on the
new field, stones in the field) and any land (the instrument-makers, washing day). Spring, summer, autumn and winter. The
colony's work: farming, cleaning, hauling, gathering food, woodcutting, building, art, music, firefighting, hunting and
cooking. Two with a risk (ice on the paths, midges at dusk), the rest peaceful.

---

## Camps (R4-C1 to R4-C6)

`Defs/CampDefs/Rimfolk_CampKinds_Round4.xml`, `Source/ColonyCouncil/Camps/CampRound4.cs` (classes `CampWorker_R4K*`,
`DilemmaEffect_R4K*`, states `R4K*State`), pure rules in `Source/ColonyCouncil/Happenings/Round4CampsEventsMath.cs`.

**R4-C1. Woodcarvers** (`Rimfolk_Camp_Woodcarvers`, `CampWorker_R4KWoodcarvers`)
- **When:** spring to fall, on land with trees (twenty wild trees or more clear of the colony).
- **On the map:** a family of two to four with a pack beast; by day one or two of them sit at a block by their fire and
  carve (the round 2 carving job, chips flying); they fell a tree now and then for their wood (the camps' own felling).
- **Answers (their page):** have {A} sit for a carving (silver): {A} walks over to their fire, and once {A} has sat with the
  carver an hour, a small wooden sculpture with {A}'s name in its title is made and set out at the edge of their camp for
  the colony to fetch (the game's own small sculpture, packed, the carver's art). If {A} never sits down with them, the
  silver is brought back to the gate. Learn their craft (a lesson, art). Buy one of their carvings (silver; a small wooden
  sculpture set out the same way).
- **Every age:** carvers of wood and bone with figures of the land's animals (tribal), a woodcarver's family with saints,
  toys and bowls (medieval), a family of whittlers selling carved figures from a cart (industrial), carvers who work real
  wood by hand, a rarity among their people (spacer).
- **Harm:** none. **Mods:** none needed (the game's sculpture, of the wood).
- Not a duplicate of the players (a show), the stone-raisers (a god's stone), the woodcutters (logs), a commission or the
  guild's commission (the colony makes art for a buyer) or the art collector (buys the colony's art).

**R4-C2. Fire-watchers** (`Rimfolk_Camp_FireWatchers`, `CampWorker_R4KFireWatchers`)
- **When:** in a hot, dry summer (24 °C or more outdoors and no rain falling) on dry land (not the tundra, the marsh or the
  jungle); four times as often while a drought runs in the region.
- **On the map:** three or four camp on the colony's land; by day one stands at the edge of their camp looking out over the
  land. When a fire burns in the open anywhere within reach of their camp (the game's own fire, from lightning, a raid or the
  colony's own doing), two of them run to it and beat it out (the game's own beating of a fire), told once a day.
- **Answers (their page):** have them cut a firebreak round the colony's biggest field (silver): over the working hours two
  of them cut the dry wild grass and bushes in a strip just outside the field (the game's own cutting, a few at a time;
  never a tree, a crop, a plant that gives a harvest or anything in a zone), forty plants at most. Learn from them (a
  lesson, plants).
- **Every age:** watchers with green-branch beaters and a horn (tribal), a town's beacon watch with wet sacks and buckets
  (medieval), fire lookouts with a spyglass and shovels (industrial), a fire crew with a drone and foam packs (spacer).
- **Harm:** none to the colony (a fire's own burns to them are the game's). **Mods:** none needed.
- Not a duplicate of smoke in the dry grass (a danger the colony stamps out), fire on the far ridge (a council question),
  fire in the canopy (a people's event), the grove-keepers in the burn (after a fire) or the storm tower.

**R4-C3. Wildfowlers** (`Rimfolk_Camp_Wildfowlers`, `CampWorker_R4KWildfowlers`)
- **When:** autumn and winter, on land with open water (marsh, coast, river or lake) clear of the colony, where a goose or a
  duck can live in the season.
- **On the map:** two to four fowlers with bows, slings or guns; a flock of four to six wild geese or ducks comes in with them
  and stays about the water by their camp (wild animals of the land, no one's). At dawn one of them goes out after a bird
  (the game's own hunt, their kill, dressed at their camp like any camp's kill).
- **Answers (their page):** buy a brace of their birds (silver; a share of their meat set out at their camp's edge for the
  colony to fetch, as the hunters do). Learn to call the birds in (a lesson, animals).
- **Every age:** fowlers with nets and slings (tribal), fowlers with nets, wooden decoys and bows (medieval), fowlers with
  shotguns and painted decoys (industrial), a shooting party with decoy drones (spacer, words only).
- **Harm:** none to the colony. **Mods:** Vanilla Animals Expanded's `AEXP_WildGoose` (active); the game's `Goose` and `Duck`.
- Not a duplicate of the hunting party (any game), the lord's hunt (one named beast), the beast-catchers (wild herds tamed),
  the fishers or seals at the fishing ground (an event).

**R4-C4. Snow-carvers** (`Rimfolk_Camp_SnowCarvers`, `CampWorker_R4KSnowCarvers`)
- **When:** in winter, with deep snow lying on the land and the air below freezing; winter folk, in the winter folk's
  clothes (`Rimfolk_CampWear_Winter`).
- **On the map:** three to five camp on the snow; by day two of them work a heap of snow at the edge of their camp (snow
  thrown up as they work), and when one is done it stands there: the game's own snow figure (`Snowman`), up to five over a
  stay. The figures stay when they go, until the thaw takes them (the game's own melting).
- **Answers (their page):** an evening among the snow figures (a gathering at their camp, their people as guests); {A} makes
  a figure with them ({A} walks over, and once {A} has worked with them an hour a figure stands by their camp, {A}'s);
  learn how they shape the snow (a lesson, art).
- **Every age:** the carvers of a winter gathering with figures of the land's animals (tribal), a winter fair's
  snow-carvers with ice saws and lanterns (medieval), an ice-sculpture club on a winter tour (industrial), artists with
  heated cutters (spacer, words only).
- **Harm:** none. **Mods:** none needed (the game's snow figure).
- Not a duplicate of the river freezes (an ice fair, an event), the first snow (a snow day), the pilgrims of the long night
  (a vigil) or the ice fishers.

**R4-C5. Cheesemakers** (`Rimfolk_Camp_Cheesemakers`, `CampWorker_R4KCheesemakers`)
- **When:** spring and summer, on hilly or mountain land, from any people.
- **On the map:** a family with three or four goats grazing by their camp; they milk the goats (the camps' own milking) and
  by day one of them stirs the curd in a pot by the fire; once a day a little of the milk in their store becomes cheese
  (Vanilla Cooking Expanded's cheese where it is on; without it the milk stays milk). Their trader sells cheese and milk.
- **Answers (their page):** buy the goats' milk (the camps' own milk sale: milked now and carried to the gate); buy a wheel
  of their cheese (carried to the gate; only with Vanilla Cooking Expanded); learn cheesemaking (a lesson, cooking).
- **Every age:** herders who set curds in skin bags (tribal), cheesemakers down from the high pastures (medieval), a dairy
  cooperative's cart with a press (industrial), a family that keeps a hand dairy in an age of vats (spacer).
- **Harm:** none. **Mods:** Vanilla Cooking Expanded's `VCE_Cheese` (active); the game's `Goat` and `Milk`.
- Not a duplicate of the drovers (a herd driven to market, milk and wool), the shearers, the herding folk's winter herd or a
  herd kept apart.

**R4-C6. Instrument-makers** (`Rimfolk_Camp_InstrumentMakers`, `CampWorker_R4KInstruments`)
- **When:** spring to fall, from any people, where an instrument of the game or the player's mods exists.
- **On the map:** two or three makers with a cart; their own instrument of their age stands by their fire (theirs, packed
  again when they go); by day one of them works at a new one (fitting and tuning), and in the evening one plays theirs, notes
  over their head.
- **Answers (their page):** buy an instrument of their age (silver; a new one, packed and set out at the edge of their camp
  for the colony to fetch); an
  evening of their music (a gathering at their camp with music, their people as guests); learn to play (a lesson, art).
- **Every age:** drum-makers (tribal: Ideology's drum), harp-makers (medieval: Royalty's harp), a piano maker's cart
  (industrial: Vanilla Furniture Expanded's upright piano, else Royalty's piano), harpsichord-makers (spacer: Royalty's
  harpsichord); where one of their age isn't there, the nearest age's that is.
- **Harm:** none. **Mods:** Ideology's `Drum`, Royalty's `Harp`, `Harpsichord` and `Piano`, Vanilla Furniture Expanded's
  `Joy_Piano` (all active).
- Not a duplicate of the minstrels (a band that plays), music in the air (an event), the players (a show) or a song for the
  weary.

---

## Events (R4-E1 to R4-E8)

`Defs/IncidentDefs/Rimfolk_Round4_Events.xml`, `Source/ColonyCouncil/Happenings/Workers_Round4.cs` (workers
`HappeningWorker_R4E*`, effects `DilemmaEffect_R4E*`, steps in `R4EMap`), rules in `Round4CampsEventsMath.cs`, words in
`Languages/English/Keyed/Rimfolk_Round4_CampsEvents.xml`. What they keep on the map is a new `OnMapTimed` kind after round
3's last: `R4ECrows`, `R4EMelt`, `R4EMidges`, `R4EShore`, `R4EIce`. The spoken lines are tagged with their moment.

**R4-E1. Crows on the new field** (the plains and the forest's clearings, spring; farming)
- **When:** in spring, the colony's biggest field freshly sown (a dozen young plants or more), no fight on the map, and a
  crow (Odyssey's `Crow`) or else a goose (the game's) can live here.
- **On the map:** five to eight crows come in from the edge and walk about the field among the seedlings; every couple of
  hours they pull up one or two (the young plant gone, a puff of dust), until the answer.
- **Answers:**
  1. *{B} keeps watch in the field:* {B} walks out and goes about the field until evening; the crows {B} comes near run off
     the land; nothing more is pulled up while {B} is there.
  2. *Shoot two to scare off the rest:* the two nearest are marked for the hunters (the game's own hunt); once one falls the
     rest leave the land.
  3. *Let them have their share:* they peck about until evening (a few more seedlings lost, ten at most) and then go.
- **Every age:** shouting and a rattle, slings (tribal); a wooden clapper, bows (medieval); a gun (industrial, spacer).
- **Harm:** none. **Mods:** Odyssey's `Crow` (active); the game's `Goose` after.
- Not a duplicate of the herd crossing, the mast year (boar under the trees), vermin in the stores, the killing frost or the
  blighted plants (a council question).

**R4-E2. Meltwater in the deep rooms** (the mountains, spring; cleaning)
- **When:** in spring, above freezing, after a winter with snow, when the colony has a room dug under the mountain (thick
  rock overhead) with a door to the outside.
- **On the map:** meltwater runs in under that door: puddles (the game's own `Filth_Water`) spread over the floor inside it
  every few hours while it lasts (a day and a half); {A} saw it.
- **Answers:**
  1. *Mop it up as it comes:* a cleaning party (the game's own cleaning) for the day, while the water keeps coming.
  2. *Keep that door shut until it stops:* the door is forbidden until the meltwater stops, and no more water comes in; the
     puddles already there are cleaned in the colony's own time. Only when the room has another way in.
  3. *Leave it until it stops:* the water keeps coming in until it is done; the puddles stay for the cleaners.
- **Every age:** a hide flap or a plank door (words only).
- **Harm:** none. **Mods:** none needed.
- Not a duplicate of the thaw (a letter of a find in the melting snow), bad air in the deep, the creak at the dig, or the
  cold room under the rock.

**R4-E3. Midges at dusk** (the marsh, summer; field work by the water)
- **When:** in a warm summer afternoon, the colony's biggest field lies within reach of wet ground (marsh, mud, shallow water).
- **On the map:** {A}, working that field, says the midges come up off the wet ground at dusk; for the next two evenings
  those out by the wet ground at dusk slap at them (bubbles).
- **Answers:**
  1. *Smudge fires along the field:* the game's campfires lit along the field's edge, kept smoking for two days (the existing
     smudge fires); no bites while they burn.
  2. *Everyone in from the wet fields before dusk:* for two evenings, those working within reach of the wet ground walk in
     before dusk.
  3. *Put up with them:* for two evenings those out by the wet ground at dusk are bitten: a bad mood, and a small chance of
     insect bites (`Council_Ail_InsectBites`, the land's ailments: `HarmKinds.LandAilments`), named under the answer.
- **Every age:** green wood on the fires (tribal, medieval), a smoke pot (industrial), the same fires (spacer).
- **Harm:** answer 3 only, through the shared budget, named.
- Not a duplicate of the smudge fires against a frost (the killing frost), the hail, the wild bees or the swamp fever (an
  ailment of its own).

**R4-E4. What the storm threw up** (the coast, autumn, winter and spring; gathering food)
- **When:** after rain, on land by the sea, with the colony's shore reachable and no fight on.
- **On the map:** along the shore near the colony lie crabs and lobsters (Vanilla Fishing Expanded's `VCEF_RawLobster`) and
  seaweed (Vanilla Cooking Expanded - Sushi's `VCE_RawNoriSeaweed`) thrown up by the waves, five to eight heaps, held back
  from the haulers until the answer; Odyssey's fish where those mods are off; without any of them it doesn't come.
- **Answers:**
  1. *Everyone down to the shore to gather it:* all of it let go and a work party carries it to the stores (the game's own
     hauling).
  2. *Only what lies nearest:* the half nearest the colony let go and one carries it in; the rest goes back to the sea at
     night.
  3. *Leave it to the sea:* by night the waves take it back (gone with a splash), told.
- **Every age:** baskets (tribal, medieval), buckets and crates (industrial), a hover-sled (spacer).
- **Harm:** none. **Mods:** as above, by name, the game's own fishing DLC after.
- Not a duplicate of high water (driftwood left along a river), the fish are running, seals at the fishing ground or lost
  cargo.

**R4-E5. Ice on the paths** (the ice and every cold land, winter; all outdoor work)
- **When:** in winter, below freezing after rain or snow in the last day, with two doors or more to the outside.
- **On the map:** {A} nearly fell on the ice outside a door and says so.
- **Answers:**
  1. *Ash and grit on the paths between the doors:* {B} walks from door to door scattering ash (a puff at each); while the ice
     lasts nobody slips.
  2. *Only by the busiest doors:* {B} does the two doors used most; away from them the risk stays.
  3. *Leave it until it thaws:* until it thaws (or a day and a half), anyone walking out on the colony's ground may slip:
     a small chance of a sprained ankle (`Council_Ail_SprainedAnkle`, `HarmKinds.Dangers`; one chance in two hundred an
     hour for one walking out, before the budget's scale, twice at most in a spell), named and told.
- **Every age:** ash from the hearth (tribal, medieval), grit and salt (industrial), a spreader (spacer).
- **Harm:** answers 2 and 3, through the shared budget, named under the answer.
- Not a duplicate of snow on the roof, the first snow, the night's cold, not in by dark or the thaw.

**R4-E6. The old tree by the house** (the forest and every land with trees, spring to fall; woodcutting, building)
- **When:** the colony's wood runs short (under forty logs a person), and a big grown tree stands close to the colony's
  buildings, outside them; {A} likes to sit under it (and walks out to it as the card comes).
- **On the map:** {A} stands under the tree a while.
- **Answers:**
  1. *Fell it for the wood:* the tree is marked for cutting and a work party cuts it (the game's own felling, the logs the
     colony's); {A} is sad about it.
  2. *Keep it, and put a seat under it:* a stool of wood marked beside the trunk and built by a work party; {A} is glad.
  3. *Leave it standing:* nothing more is done; {A} is a little glad.
- **Every age:** the colony's own stool.
- **Harm:** none beyond the game's own felling.
- Not a duplicate of the leaning tree (a danger), a lightning-struck tree, the moot over the old wood (a people's quarrel
  over a forest), singing to the old tree (a custom) or a tree for the dead.

**R4-E7. Stones in the field** (the plains and the hills, spring; farming, hauling)
- **When:** in spring, on land where the winter freezes, the colony's biggest field (forty cells or more) with open ground.
- **On the map:** the frost has pushed stones up through the field: three to six chunks of the land's own rock lie in it;
  {A}, sowing, found them.
- **Answers:**
  1. *Carry them off the field:* a work party carries the chunks to the stores or a heap by the gate (the game's own).
  2. *Cut them into blocks:* a bill at the colony's stonecutter's table for blocks of that stone (the game's own recipe),
     as many as the stones; only with a stonecutter's table.
  3. *Sow round them:* they stay where they lie.
- **Every age:** baskets and a sledge (tribal), a stone-boat behind an ox (medieval), a cart (industrial), a hover-sled
  (spacer); the colony's own stonecutter's table.
- **Harm:** none. **Mods:** none needed.
- Not a duplicate of the rockslide, a rich seam or the fence down.

**R4-E8. Washing day** (any land with water, spring to fall; cleaning)
- **When:** a warm, dry morning (16 °C or more, no rain), with shallow water near the colony.
- **On the map:** the card is {A}'s offer.
- **Answers:**
  1. *A washing day at the water for everyone:* the colony gathers at the water for a couple of hours (a gathering at the
     water), with its own lines.
  2. *{A} and one more wash the bedding:* {A} walks down to the water and back (a visit to the water).
  3. *Not today:* nothing.
- **Every age:** beating the clothes on the stones (tribal), lye and paddles (medieval), washboards and tubs (industrial),
  the same by hand at the water (spacer).
- **Harm:** none. **Mods:** none needed.
- Not a duplicate of a bath for the road (travellers asking for the colony's bath), the bathers' or a swim (wishes only).

---

## What stays as it was

The camps' whole frame, the events' whole frame, OnMapTimed and its saved `TimedThing`, the shared harm budget and its
kinds. New are only the defs, the keyed words, the workers and effects above, five `TimedKind` values after `R3Dinner`,
their entries in the play-out audit (one block, `# round 4, camps and events`), the camps' facts tags at the end of the camp
tags, the moments the camp workers say (`CampWordsSaidTests`), and one section on the camps' Almanac page.
