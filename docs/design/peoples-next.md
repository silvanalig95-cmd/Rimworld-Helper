# Peoples, the next round: eight new peoples and the seven brought to their level

The player's words (7 November 2026), in order:
- "could it be that the new factions pawns all wear just one single outfit per faction? ... the factions should try to
  draw from all kinds of mod and vanilla references for items, weapons, gear, clothing & armor, magic and abilities etc."
- "also for example if there is an army faction or so, it makes sense if people have more or less the same uniform. it
  should make sense for each faction, but have variety whenever possible"
- "you can already think of new factions with the new design philosophy. add as much flavour, lore and character as
  possible"
- On the eight below: "love all these factions, go ahead! also update existing factions to this level"

## The design philosophy (binding for every people, old and new)

- **A signature, then variety.** Each people is known at a glance by one or two things (a colour on a cape, sash, hood
  or tabard; a token; a hallmark piece for a role). Everything else varies by pawn, drawn from vanilla, the DLCs and the
  player's mods (`docs/player-modlist.md`, the full list in the session scratchpad's `player_activemods.txt`), softly:
  `MayRequire` or by name in code, with a vanilla fallback that dresses and arms every kind well with no mods at all.
- **Uniform where it makes sense.** Soldiers, guards, sworn fighters and militias share one kit, varying only in wear,
  quality and small personal touches; an order's habit is shared but not its cut or its age; civilians, traders,
  pilgrims and craftsfolk vary freely around their signature. Each people's defs say why, role by role.
- **Magic and abilities for a share, never all**, where the calling, the age and the faith fit: A RimWorld of Magic's
  classes, Royalty psylinks and Vanilla Psycasts Expanded's paths, Biotech xenotypes, genes and mechanitors, the
  player's race and gene mods, Anomaly where the lore calls for it.
- **Xenotypes and races from the whole pool**, not nearly all baseliners (the player: "these include different
  xenotypes not just baseliners, right?" and "again, always draw from the full mod pool ... for weapons, apparell,
  items, gear, xenotypes etc."): each people has a real mix that fits its calling, age and lore, from Biotech's own,
  Vanilla Races Expanded's (Archon, Fungoid, Genie, Highmate, Hussar, Lycanthrope, Phytokin, Pigskin, Sanguophage,
  Saurid, Starjack, Waster, Insector, Android), Alpha Genes', Vanilla Genetics Expanded's where it has people, and the
  Humanoid Alien Races peoples where one reads as natural (elves, merfolk, Avaloi, Boglegs, Stoneborn, Venators), with
  baseliners common enough that the people still reads as one.
- **Every age.** Each people takes the form of its age (tribal to spacer), as release A's template does.
- **Flavour, lore and character as much as possible:** a founding story, sayings, what they honour and fear, how
  they speak (their lines, letters, messengers and talk pieces in their own voice), what they do at the colony's gate
  (touchpoints that play out on the map), a mission, a faith with Ideology, a town plan that tells their story, titles,
  an emblem.
- The colony is the family: no houses, dynasties or family lines as mechanics. Harm only at the game's rate, shown.

## The eight new peoples (approved as written; details are the builder's within this)

### 1. The Lantern Wardens: beast-hunters of the crossroads
*"Where a lantern burns at a crossroads, someone was taken there. We have come to settle it."*
The first Warden was a shepherd whose village was emptied in one night by something from the treeline. He hung his dead
brother's lantern at the crossroads and swore to keep it lit until the beast was dead; it took eleven years. The
Wardens follow the region's great beasts from lantern to lantern and keep a book of every creature run down, with empty
pages for the ones still loose.
- Look: no uniform; furs, oiled leather, whatever the last hunt paid for. Signature: a soot-blackened lantern at the belt
  and a red cord on the sleeve, a knot for every beast felled.
- Arms and magic: boar spears, crossbows and nets (VE Bows, VFE Medieval 2), hunting rifles in the industrial age;
  some are RoM rangers; an old tracker may be a VPE Wildspeaker.
- Towns: a palisaded lodge, a trophy hall, kennels, a lantern post on every road out.
- At the gate: they come when a pack alpha, a beast nemesis or a legend is near (0.149.0's `Beasts/`) and offer to hunt
  it beside the colony for a share of the trophy; they buy pelts and trophies and teach tracking; a Warden lights a
  lantern at the colony's crossroads when a beast has taken one of its people.
- Titles: lantern-master, keeper of the book, tracker, pelt-buyer.

### 2. The Quiet Bell: keepers of the dead
*"Everyone is owed a bell. Even the ones who came over your wall."*
After the long war nobody buried the raiders, and what came up out of those fields (with Anomaly) taught the region why
that matters. The Quiet Bell keeps the dead of every people, friend or foe, and listens to the ground for what should
not stir.
- Look: bell-keepers share an undyed grey habit and veil (the habit rule); diggers and wardens dress plainly and
  differently; all carry a small bronze bell on a cord.
- Arms and magic: spades and mattocks, flamers in the industrial age for what will not stay down; priests may be RoM
  priests; with Anomaly they know the void's signs.
- Towns: a walled grave-garden, an ossuary, a bell tower, a quiet hostel.
- At the gate: after a raid they take the enemy's dead off the colony's hands and ring for them (goodwill with the
  raiders' people); a bell-keeper can lead the colony's funerals; with Anomaly they warn of void signs before the void
  acts.
- Titles: first bell, sexton, gravewarden, tollkeeper.

### 3. The Circuit of Tamers (with PokéWorld; beast-tamers without it)
*"A bond is not a leash. Win their trust and they will win your battles."*
A herder girl found an egg in a meteor crater, raised what hatched, and walked the region with it for forty years,
challenging every keeper of beasts she met. The Circuit grew from those she beat and those who beat her. Their towns keep
bout-rings; their highest honour is to be beaten fairly by a stranger.
- Look: travelling clothes, freely varied; signature: a braided sash with a token for every bout won.
- Arms and abilities: each tamer travels with one or two companions (Pokémon with PokéWorld, else the region's beasts
  and VAE's); light weapons; Wildspeakers in the spacer age.
- Towns: bout-rings, a hatchery, stables for every kind.
- At the gate: a tamer challenges the colony's best trainer to a friendly bout (0.147.0's Fighting-type spar rules,
  bruises included); egg trades and swaps; a ranger brings back one of the colony's animals that strayed.
- Titles: circuit-holder, ring-keeper, roaming tamer, egg-trader. (Original names only: nothing borrowed from any game's
  own trainer ranks.)

### 4. The Wheel: the rolling salvage town
*"The ground burned under our grandmothers. We have not stopped moving since."*
When their valley burned they put everything on wheels and runners, and nobody ever voted to stop. A town in motion,
circling the region once every few years, buying what others throw away and selling it back better.
- Look: patched coats, welding goggles, tool-belts, every piece mended differently; signature: a brass cog stitched to
  the shoulder.
- Arms and abilities: scrap-built guns and heavy wrenches (VE Weapons, Rimsenal); a mechanitor or two with small mechs
  (Biotech) in the industrial and spacer ages; a convoy (Vehicle Framework), mounts (Giddy-Up), carts in the medieval
  age; a RoM technomancer now and then.
- Towns: workshop yards and scrap heaps laid out as if leaving tomorrow; a Wheel town can move.
- At the gate: they mend worn clothes and weapons for silver; buy scrap, slag and broken things and sell refurbished
  gear; a wrecked mechanoid near the colony brings them at a run.
- Titles: wheelwright, yard boss, scrap-runner, fixer.

### 5. The Stormglass Conclave: weather-seers
*"The sky is the world thinking aloud. We have learned to read its hand."*
In a tower of glass vessels on a stormy plateau, the Conclave has written down every storm for three hundred years. They
hold that weather is the planet's psychic tide and that a trained mind feels a cold snap days before the frost. Some can.
- Look: storm-grey coats hung with brass-capped glass vials (the coat is the order's; everything under it varies).
- Arms and magic: most are psycasters (VPE Staticlord and Frostshaper, Royalty psylinks otherwise); a few RoM
  storm-born; charge weapons in the spacer age, crossbows in the medieval one.
- Towns: a glass tower, lightning rods, a weather garden.
- At the gate: for a fee a seer warns of the next cold snap, heat wave or toxic fallout (with Rimfolk's fields by the
  weather); they ask to set a lightning rod on the colony's highest roof; an apprentice may stay a season to study its
  skies.
- Titles: first reader, keeper of the glass, storm-walker, vial-seller.

### 6. The Mossborn: grove-keepers
*"Every tree you fell owes the world a seedling. We are here to collect."*
The forest remembers every axe. Their towns grow inside living trees; their dead are buried under saplings so the person
becomes the grove. Gentle and patient, and fiercer than anyone when a forest burns.
- Look: bark-cloth, leaf-green and moss, hand-varied; signature: a living sprig in the hair; some carry plant genes
  (VRE Phytokin, Alpha Genes).
- Arms and magic: bows and thorned clubs; RoM druids; VPE Wildspeakers; Ideology tree-connectors with their dryads.
- Towns: tree-dwellings, a seed vault, a burial grove.
- At the gate: saplings as gifts and a request to replant what the colony cleared; herbal medicine and dyes in trade;
  cooler towards a colony that strips the land, warmer towards one that plants.
- Titles: eldest root, grove-warden, seed-carrier, herb-trader.

### 7. The Ashen Banner: a free company
*"We fought for a king nobody remembers. Now we fight for whoever pays, and we keep the roll of our dead."*
The company outlived the war, the king and the kingdom. What it kept is the Roll: every name lost under the banner, read
aloud once a year. Neither friend nor enemy, for hire, and they keep contracts better than most keep their word.
- Look: the clearest uniform: the company's ash-grey tabard over matching issued armour; only scars, wear and a few
  personal trophies tell them apart; officers add a black sash.
- Arms and abilities: one issued set per age (pikes and crossbows with VFE Medieval 2, rifles and bayonets, charge
  rifles with VE and Rimsenal kits); mounted lances with Giddy-Up; a RoM commander or two.
- Towns: a barracks fort, a drill yard, the hall of the Roll.
- At the gate: hire a squad for a season to hold the walls (temporary guests who fight, then leave); contracts (with
  Vanilla Trading Expanded); left unpaid, they walk; their deserters sometimes ask for shelter.
- Titles: captain of the Banner, keeper of the Roll, sergeant, paymaster.

### 8. The Kiln Guild: artisans of the region
*"Anyone can stack stone. We build things people come to look at."*
Masons, glassblowers, smiths and carvers, bound by guild marks cut into everything they make. Their towns are the
region's finest work; their apprentices spend a wander-year living among other peoples to learn and to leave a little
guild work behind.
- Look: leather aprons, burned sleeves, varied work clothes; signature: the guild mark branded on a bracer.
- Arms and abilities: mostly hammers and tools; guild guards carry fine blades as a display; RoM enchanters and
  geomancers.
- Towns: kilns, forges, a market of masterworks, a carved guildhall.
- At the gate: they commission work from the colony's crafters (more for higher quality); a wander-year apprentice comes
  to learn from the colony's best maker; they bid on the colony's masterworks.
- Titles: guildmaster, kiln-warden, journeyman, master-trader.

Each comes with an emblem and town plan, a faith with Ideology, messengers, letters and talk lines in its own voice,
touchpoints at the gate and a mission, and its names for peoples that arise during play.

## The seven existing peoples, brought to this level

The order (hospitallers), the herders, the merchant league, the pilgrim fellowship, the learned society, the spacer
enclave and the river folk keep their callings, titles, towns and events, and gain what the eight above have: a founding
story and sayings, what they honour and fear, a fuller voice (lines, letters, talk pieces), one or two new touchpoints
each that play out on the map, a signature in the gear rework's sense, and magic or abilities for a share where they fit
(the gear rework, merged first, sets the clothes and arms).

## Order of building (one after another: same area)

1. The gear rework of the seven (running when this was written).
2. The seven brought to this level.
3. New peoples, first four: the Lantern Wardens, the Quiet Bell, the Circuit of Tamers, the Wheel.
4. New peoples, second four: the Stormglass Conclave, the Mossborn, the Ashen Banner, the Kiln Guild.
