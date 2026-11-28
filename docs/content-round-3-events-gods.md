# The content round 3: eight map events and six happenings of the gods on the land (27 November 2026)

The player's go, 27 November 2026: "you can go ahead with the content round, maybe also other content, camps, dilemmas,
world events, just further expanding on the mechanics you already have or adding more, so to say, content to the
mechanics ... adding more mechanics for the time being doesn't necessarily make it better since it's already just a lot."

So everything here is content inside frames that exist: the Rimfolk events (an `IncidentDef` with a `HappeningExtension`,
its answers through the dilemma effects, what it keeps on the map one of `OnMapTimed`'s kinds, as round 2's), and each
god's own happenings on the land (new `OwnHappening` values at the end of the list, in the frame of
`Gods/OnMap/OwnHappenings.cs` and `OwnSeven.cs`, on the gods' pace). No new system, window, setting or saved subsystem;
the gods' six keep what they have under way in one small record of the gods' book, as each god of the second seven does.

## The rules on every item

- **Answers play out on the map**, with no countdown in the words; what the words say happens is what the player sees.
  Speech goes through `Speech.Say`; bubbles are one sentence.
- **Harm** only where an item says so, through `Harm` and `HarmKinds` (no new kind), at the game's own rate, from
  something on the map, named under the answer before it is chosen, and shown.
- **Every age:** each item says its tribal, medieval, industrial and spacer form.
- **The gods are unseen.** An event card never names a god. A god's card names it in its title only where
  `Gods.CardName` allows (the colony knows it was that god), by the colony's name for it; its text never does. What people
  say is their own guess and may be wrong; the colony's book keeps what someone saw, in their words.
- **The colony is the family:** nothing reads kin or houses.
- **Mods** only from the `active` rows of `docs/player-mods/`, by name in code with a vanilla fallback.
- **Text:** event cards, messages and the gods' cards in plain, neutral words; the book's lines plain too (what was seen).

Spread: the eight cover the forest, the desert, the snow, the marsh, the mountains and the coast, with two that come on
any farmed land; spring, summer, autumn and winter; and herding, building, hauling, cooking, farming, fishing and
crafting. The six gods' happenings cover the snow, the desert sun, the marsh and the wood, the workshop, the coast and the
mountains.

---

## 1. Eight events of everyday life on the map (R1 to R8)

Defs: `Defs/IncidentDefs/Rimfolk_Round3_Events.xml`; code: `Source/ColonyCouncil/Happenings/Workers_Round3.cs` (workers
`HappeningWorker_R3*`, effects `DilemmaEffect_R3*`, steps in `Round3Map`); rules: `Happenings/Round3EventsMath.cs`; words:
`Languages/English/Keyed/Rimfolk_Round3_Events.xml`. What each keeps on the map is a new `OnMapTimed` kind after round
2's last: `R3Boar`, `R3Snake`, `R3Roof`, `R3Seals`, `R3Dinner`.

**R1. The mast year** (the forest, autumn; hunting, herding)
- **When:** in autumn, with many trees near the colony (25 or more within reach of its middle) and no fight on the map,
  where the game's wild boar can live in the season.
- **On the map:** a group of three to five wild boar comes in from the edge to the trees nearest the colony and roots
  there for the fallen nuts, staying among the trees (dust where they root). The one who saw them tells; the best hunter
  could go.
- **Answers:**
  1. *Take the two fattest for the stores:* the two biggest are marked for the hunters (the game's own hunt).
     **Harm:** a wounded boar may turn on its hunter, at the game's own revenge chance (named under the answer).
  2. *Let them feed, and bring the herd in:* a work party walks the colony's animals home to the pens and barns
     (the game's roping, `WorkTask.Herd`); the boar root under the trees a while (a day and a half) and then leave the land.
  3. *Drive them off:* the hunter and one more walk out at them shouting, and the boar run off the land.
- **Every age:** shouting and sticks beaten together (tribal); a horn and staves (medieval); shots fired into the air
  (industrial, spacer); spears or bows, crossbows, rifles for the hunt by the colony's own weapons.
- **Mods:** the game's `WildBoar`; on a polluted map Vanilla Animals Expanded - Wastes' `VAEWaste_Wasteboar` (active).
- Not a duplicate of the herd crossing (`Rimfolk_Evt_HerdCrossing`, grazers passing through, hunted or let cross), the
  rut (stags on a ridge), the wild harvest (plants marked for picking) or something at the pens (a predator).

**R2. A snake in the woodpile** (the desert and every hot land, summer; hauling)
- **When:** in summer heat (28 degrees or more), when the colony has logs lying in a pile on its home ground and a snake
  can live here.
- **On the map:** a snake lies coiled among the logs (kept there, still); the one who nearly put a hand on it backs off
  and says so. The best handler could deal with it.
- **Answers:**
  1. *The handler lifts it away on a long stick:* the handler walks over, and the snake is walked off ahead of them,
     past the edge of the colony, and let go (it goes off the land). **Harm:** a small chance it bites the handler's
     hand or arm, through the shared budget (`HarmKinds.Dangers`, a bite), named under the answer.
  2. *Kill it:* marked for the hunters. **Harm:** the game's own revenge chance for a hunted snake (a bite), named.
  3. *Leave the woodpile alone until dusk:* the logs there are forbidden; at dusk the snake moves off by itself and
     the logs are let go again.
- **Every age:** a forked stick (tribal), a forked staff (medieval), a snake hook or a shovel (industrial), a long
  grabber (spacer).
- **Mods:** Vanilla Animals Expanded's `AEXP_Rattlesnake` (active) on dry land, else the game's `Cobra`.
- Not a duplicate of the animal that's wrong (a sick wild animal at the edge), the wild one or a wild one at the pens.

**R3. Snow on the roof** (the snow lands, winter; building)
- **When:** in winter, after snow on three of the last five days, with deep snow lying round one of the colony's big
  rooms under a built roof (not rock), and someone inside it.
- **On the map:** dust falls from the roof inside and it creaks; the one inside looks up and says so. The best builder
  could see to it.
- **Answers:**
  1. *Shovel it off:* the builder and two more work along the room's outside walls for about an hour, throwing the
     snow down (puffs of snow, the snow piled against the walls), and the creaking stops.
  2. *Prop it from inside:* a column marked in the middle of the room for the builders (the game's own column, of wood
     or stone); the creaking stops once it stands.
  3. *Leave it:* if the snow still lies deep later that day, the roof comes down over a few cells where nobody is
     standing (the game's own roof collapse), and the hole is there to patch. **Harm:** none to people (only cells with
     nobody and nothing under them); named under the answer.
- **Every age:** wooden shovels and baskets (tribal), shovels (medieval, industrial), the same with a heated suit
  (spacer, words only); the column of whatever the colony builds with.
- Not a duplicate of snow on the paths (a council question about clearing paths), the first snow, the ground giving
  (soft ground by a door) or the creak at the dig (a rock face in a mine).

**R4. The way across the marsh** (the marsh, spring to autumn; building)
- **When:** the straight way from the colony to its biggest field (else its fishing ground) crosses wet ground (marsh
  and mud) for four cells or more, and it isn't frozen.
- **On the map:** the one who came back from the field is muddy to the knees and says so (mud where they came in).
  The best builder could lay a way across.
- **Answers:**
  1. *Planks the whole way across:* bridge blueprints (the game's own `Bridge`) over every wet cell on the way, and a
     building party at them. The wood is named.
  2. *Only over the worst of it:* the same over the deep marsh only, fewer planks, a smaller party.
  3. *Keep wading:* nothing is built.
- **Every age:** a road of logs laid side by side (tribal), planks (medieval), a boardwalk (industrial), a walkway
  (spacer); the game's bridge in each.
- Not a duplicate of a crossing on the river (the river folk's event, a crossing for them over a river), stuck in the
  bog (an animal in the mud) or the ground giving.

**R5. The cold room under the rock** (the mountains, summer; cooking, hauling)
- **When:** in a warm summer, the colony's food is stored in a room warmer than ten degrees, and it has a room dug under
  the mountain (thick rock roof) that stays colder, with floor free for a store and no food store in it yet.
- **On the map:** the cook went down into the deep room, felt how cold it is, and says so.
- **Answers:**
  1. *All the food down there for the summer:* a stockpile is marked over the deep room's free floor for food, preferred
     to the others, and the haulers carry the food down (a work party, the game's own hauling).
  2. *Only the meat:* the same for raw meat and fish only.
  3. *Leave things as they are.*
- **Every age:** a cave larder (tribal), a cellar (medieval), a cold store (industrial), a store that saves the coolers'
  power (spacer).
- Not a duplicate of short in the stores (food running low), the food that won't keep (a cooking bill), vermin in the
  stores, or a room's plan (the room templates).

**R6. Seals at the fishing ground** (the coast, winter and spring; fishing)
- **When:** the colony has a fishing ground at the sea (else at a lake or river), in winter or spring, with no fight on.
- **On the map:** three or four seals (on the coast), else otters (fresh water), come up on the shore by the fishing
  ground, lie there and splash in the water after the fish. The fisher who saw them tells.
- **Answers:**
  1. *Drive them off:* two walk down to the water shouting and the seals go back into the water and off the land.
  2. *Take two for meat and fat:* the two biggest marked for the hunters. **Harm:** a sea lion may turn on its hunter,
     at the game's own revenge chance (named where the kind has it).
  3. *Let them be:* they stay by the fishing ground a while (a day and a half) and then go.
- **Every age:** stones and shouting (tribal), staves (medieval), shots into the air (industrial, spacer).
- **Mods:** Odyssey's `Seal` and `SeaLion` on the coast, `Otter` on fresh water (all active).
- Not a duplicate of the fish are running (a run of fish), the herd crossing, or the river folk's catch.

**R7. Dinner in the field** (any farmed land, summer and autumn; cooking, farming, hauling)
- **When:** in the late morning of summer or autumn, two or more of the colony are at work in a field that lies far from
  the dining table, there are meals in the stores, and the cook is free.
- **On the map:** the card is the cook's offer.
- **Answers:**
  1. *The cook carries dinner out to them:* at midday the cook takes a meal for each from the stores out to the edge of
     the field (the game's own carrying), and the field workers come over and eat there together, a meal each from what was carried,
     and go back to work. A memory of a meal in the field.
  2. *Everyone in to the table at midday:* at noon they come in and eat at the table (the game's own eating), and
     remember it.
  3. *Each eats when they're hungry,* as usual.
- **Every age:** a basket of bread and stew (tribal, medieval), a pail and a pot (industrial), packed meals (spacer).
- Not a duplicate of hands for the last field (all hands to pick before a frost), the first harvest shared (food sent to
  neighbours) or the harvest fair.

**R8. Clothes worn through** (any land, autumn; crafting)
- **When:** in autumn, three or more of the colony wear something worn past half its life (the game's "tattered"), and
  the colony has a bench that makes clothes.
- **On the map:** the card is the tailor's; one in rags is named.
- **Answers:**
  1. *A bill for warm clothes at the bench:* a bill at the colony's tailoring bench for the warmest garment it can make
     (any mod's: whatever that bench lists), one for each in rags, four at most. The game's own tailoring.
  2. *Hand out what's in the stores:* each one in rags walks to the stores and puts on a better piece of the same kind
     that lies there unused and their outfit allows (the game's own "wear").
  3. *Wear them through the winter.*
- **Every age:** the garment is whatever the bench makes (tribalwear and parkas, a medieval mod's tunics and cloaks,
  dusters and jackets, a spacer's); the words say hides, wool or cloth by age.
- **Mods:** every apparel recipe the player's mods add to that bench (Vanilla Apparel Expanded, Vanilla Factions
  Expanded - Medieval 2 and the rest), read from the bench itself.
- Not a duplicate of the mending day (a custom: repairs and a lesson) or the advisor's note on worn clothes.

---

## 2. Six happenings of the gods on the land (G1 to G6)

New `OwnHappening` values after round 7's `FaithLightning` (so a save's list keeps its order), each its god's own (the
name begins with the god's key). Code: `Source/ColonyCouncil/Gods/OnMap/OwnRound3.cs` (hooked into the second seven's
hour and steps, `OwnSeven.cs`), what is under way in `Round3LandState` on the gods' book; pace and rules in
`Happenings/Round3EventsMath.cs` (`OwnHappeningsMath.Land3PaceOf`); words in
`Languages/English/Keyed/Rimfolk_Round3_GodsLand.xml`, spoken lines in a pool of their own
(`Defs/GodDefs/Rimfolk_Round3_GodsLand.xml`). Each keeps the gods' pace (a gap, a chance that grows, a little more often
the nearer its god is), comes only where its god acts at the home (held, heard of, or drawn near), has a card in three
wordings named only where `Gods.CardName` allows, a line in the colony's book in its witness's words, and a dev action.
None of them hurts anyone.

**G1. BellwetherNewborn: the ring in the snow** (the snow lands; herding)
- **When:** at dawn after a frosty night, when one of the colony's animals was born in the night out of doors in the
  snow; at a chance, once in a long while.
- **On the map:** the newborn is alive beside its mother, warm (any hypothermia gone), and the snow in a wide ring round
  them is trodden flat (the snow cleared in a ring), as if something large had lain round them through the night. The
  nearest of the colony walks over, sees it and says so.
- **What people say:** one who holds the white beast says it kept them warm; a doubter says the mother trod it down.
- Not a duplicate of the wounded thrumbo (tended at the edge), the vigil in the cold (someone sits with the frail) or a
  night with the herd (sitting up with a birth).

**G2. HeliosRipen: ripe at noon** (the desert and the sun; farming)
- **When:** at noon on a hot, clear summer day (30 degrees or more), when one of the colony's fields is nearly ripe
  (four fifths grown on average) and none of it is ripe yet.
- **On the map:** heat shimmers over the field, and the whole field is ripe at once; the grower who sees it says so and
  the field is the growers' to bring in.
- **What people say:** one who holds the sun says he gave it; a doubter says it was the heat.
- Not a duplicate of the long noon (a god's touch that grows every crop in the open a day ahead) or his first fruit (eaten
  in the sun at his feast): one field, nearly ripe, ripe all at once.

**G3. MorelFoxfire: the light on the wet ground** (the marsh and the wood; autumn and spring nights)
- **When:** on a night after two or more wet days, when it isn't freezing, near a patch of wet ground or ground under
  trees not far from the colony.
- **On the map:** a soft green light lies over the patch through the night (motes of her colour), seen by whoever is
  awake and near; by morning the soil there is dark and rich (the game's rich soil, a few cells), and the first to see it
  says so.
- **What people say:** one who holds her says she is at her work in the rot; a doubter says rotten wood glows.
- Not a duplicate of her ring (mushrooms round something real), her breath in a ring, mushrooms after rain, or a gift
  taken leaving black soil (an offering).

**G4. PipFinished: done in the night** (the workshop; crafting)
- **When:** in the small hours, when an unfinished piece of the colony's lies at a bench with nobody at it.
- **On the map:** small sparks and a light at the bench for an hour, seen by whoever is awake near it; the work left on
  the piece is cut to a tenth; in the morning the first of the colony to see it (the nearest, sent to look) says how far it has come.
- **What people say:** one who holds the Hum says it worked the night through; a doubter says they did more yesterday than
  they thought.
- Not a duplicate of the machine mended overnight (a broken machine, its light winking back) or two hands on a broken
  machine: a piece of craft work, nearly done.

**G5. SeleneTide: the full-moon tide** (the coast)
- **When:** on a full-moon night at a home on the coast, at a chance.
- **On the map:** the sea comes up the sand further than anyone has seen it, over empty ground by the shore (shallow
  water laid over it for the night), with silver light on it, seen by whoever is awake; at first light it goes back and
  the sand is as it was.
- **What people say:** one who holds the moon says she drew the sea after her; a doubter says it is a high tide.
- Not a duplicate of the moon in the still water, the water giving back (things left on the mud after a wet run) or high
  water (a river in spate).

**G6. CirrusGlory: the giant in the mist** (the mountains and hills)
- **When:** on a foggy morning at a home in the mountains or hills, with someone of the colony out in the open.
- **On the map:** they stop and stand looking out: their own shadow lies huge on the mist with a ring of colours round its
  head, and moves when they move (a ring of light round them); they say what they saw.
- **What people say:** one who holds the sky says it looked down on them; a doubter says it was their own shadow.
- Not a duplicate of the aurora (the coldest night), the shapes in the clouds (two see gods' shapes, between them) or the
  sky's signs.

---

## Mods that would help (not built on)

Subscribed and not enabled, from `docs/player-mods/` (nothing here depends on them; each is read by its own defs once on):
- **Medieval Tailor Continued** (1.5, 1.6, 1.7; https://steamcommunity.com/sharedfiles/filedetails/?id=3512201406): more
  medieval clothes at the tailor's bench, which R8's bill would then pick from by itself.
- **Atmosphere Overhaul** (dropped by the player, 28 November 2026) (1.6, 1.7; https://steamcommunity.com/sharedfiles/filedetails/?id=3830470974): fog and light
  that would make the foggy mornings of G6 and the dusk scenes look as the words say.
- **Medieval Total Rim: Animals** (1.6, 1.7; https://steamcommunity.com/sharedfiles/filedetails/?id=3806262056): herd
  animals of the older ages (merino sheep, the Swabian hall pig, Spanish cattle), counted as the colony's herd in R1 and
  G1 once on.
