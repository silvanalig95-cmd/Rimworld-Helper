# The content round, part 3: six camps and six world events (27 November 2026)

Built on the player's go of 27 November 2026: "you can go ahead with the content round, maybe also other content, camps,
dilemmas, world events, just further expanding on the mechanics you already have or adding more, so to say, content to the
mechanics ... adding more mechanics for the time being doesn't necessarily make it better since it's already just a lot."

So everything here is content inside the frames that exist: camp kinds (`CampKindDef`, a `CampWorker` where a kind needs
ways of its own, its state in a `CampWorkState` saved with the camp) and world events (`WorldSituationDef` with a
`SituationWorker_Variety` where data can't say it). No new system, window, setting or saved collection. The camps fit
0.307.0's packs and shared store: what they buy, are given or take goes into their store and is shared out among the packs
when they leave (`CampDayMath.Repacks`, `TakeFirst`, `Camp.TakeGoods`); nothing here carries goods any other way.

## The rules on every item

- **Every age.** Each item takes the form of the age of the people it is about: their camp's titles and letters
  (`[their:tribal]`...), a world event's names and letters (`[theirTribal]`...). Nothing is switched off for an age.
- **The gods are unseen.** No god is named in a camp's or a world event's words. Where the colony's believers of a god
  speak (the sky's at the rain-callers' rite, the ledger's at the gamblers' fire), they speak as people, by
  `CampRound2.Believers`, and name no god.
- **The colony is the family.** Nothing reads kin, houses or lines.
- **Harm:** none of the twelve hurts anyone by itself; a camp's brawl comes only by the camps' own rule (`CampBrawls`, the
  shared harm budget) when a camp's unease runs high (the witch-finders refused, the gamblers' dice caught).
- **Answers play out on the map**, with no countdown in the words. Speech through `Speech.Say` and the camps' `SayMoment`;
  bubbles one sentence.
- **What the words say happens is what the player sees**: the rite at their fire, the dyed coat, the dog at someone's heels,
  the water-carriers at the colony's water, the rats on the land and the dog after them, the miner walking off to the dig.
- **Text:** camps and world events keep the earlier flavour style (CLAUDE.md, 20 November 2026).
- **Mods:** only `active` rows of `docs/player-mods/`, softly (`MayRequire` or by name in code), the game's own after.
- **Saved state:** each camp's state is a few values and single references in its `CampWorkState`; each world event's in
  its own flags and numbers. No new saved collection.

---

## Camps (R3-C1 to R3-C6)

`Defs/CampDefs/Rimfolk_CampKinds_Round3.xml`, `Source/ColonyCouncil/Camps/CampRound3.cs`.

**R3-C1. Rain-callers** (`Rimfolk_Camp_RainCallers`, `CampWorker_R3RainCallers`)
- **When:** while a drought or a hungry year runs in the region (`situationKinds`: drought, hunger; five times as often),
  else now and then in summer.
- **On the map:** three to five camp by the colony and keep their rite each evening: tribal and medieval callers dance round
  their fire (the dancing job of the round before), industrial and spacer ones work at their launcher or mast by the fire and
  send something up into the sky (smoke and a flash over their camp). On their first morning they ask the colony to promise
  a fee if the rain comes. When it rains on the colony's land after their first rite (the game's own weather, read, never
  made), they cheer at their fire; if the fee was promised, someone of the colony carries it out to their camp, and they pack
  up and go; if not, they go the next morning saying it was their doing. No rain before their days run out: they go saying
  the sky wasn't ready.
- **Answers:** promise the fee ({COUNT} silver, carried out only if it rains); promise nothing (they stay, a little sour).
  The colony's believers of the sky's god may say at the first rite that the sky isn't theirs to call (no god named).
- **Every age:** rain-dancers with drums (tribal), a rain-priest's band with bells and a water jar (medieval), cloud-seeders
  with rockets (industrial), a weather crew with an ionizer mast (spacer).
- **Harm:** none. **Mods:** none needed.
- Not a duplicate of the tree-singers (they sing to the trees), the herders by the water (a drought's herds drinking) or the
  rain dance custom (the colony's own, `Council_Dilemmas_Customs2`).

**R3-C2. Dyers** (`Rimfolk_Camp_Dyers`, `CampWorker_R3Dyers`)
- **When:** spring to fall, from any people; a little more often while cloth is scarce.
- **On the map:** a family of dyers camps by the colony with their vats by the fire (their task: stirring the vat) and dyed
  cloth set out; their trader sells dye and cloth and buys the colony's.
- **Answers (their page):** send someone over to have their clothes dyed ({COUNT} silver): the colonist walks to their camp,
  and once they have sat with the dyers an hour their outer coat or shirt takes the camp's colour (its own `CompColorable`,
  the same way the mourning colours are put on). Learn their craft (an afternoon's lesson, crafting). Buy dye, carried to
  the gate. If the one sent never gets there, the fee comes back to the gate.
- **Every age:** woad and madder in pits (tribal), a dyer's family with vats on a cart (medieval), an aniline dye salesman
  with bright powders (industrial), a colour crew with nanodye sprayers (spacer). The colour by their age: woad blue, madder
  red, weld yellow, aniline purple or green, a bright spacer colour.
- **Harm:** none. **Mods:** the game's dye (`Dye`) and cloth.
- Not a duplicate of the shearers (wool from the colony's flock), the tailor of cloth gone scarce (coats cut) or the
  mourning colours (a world event's grief, put back after).

**R3-C3. Witch-finders** (`Rimfolk_Camp_WitchFinders`, `CampWorker_R3WitchFinders`)
- **When:** rarely, from a people of Neolithic age or later; more often while a war of words or a prophet runs (`creed`,
  `faith`).
- **On the map:** two or three camp at the edge of the land, and by day one of them stands at the edge of the colony looking
  it over. On their first morning, if someone of the colony fits what they look for (one with a psychic link; else one of a
  people not baseline; else one psychically sensitive; else the colony's best healer, in an age of herbs), they ask to
  question them at their fire. Nobody fits: they look the colony over for a day or two and leave saying there is no witch
  here.
- **Answers:** let them question {SUSPECT}: the suspect walks to their fire (a visit), and after an hour there the finders
  say what they make of it, by the suspect's way with words (Social): cleared (they go next morning, their people think a
  little better of the colony) or named a witch (they go at once telling it; the suspect is upset for a few days, their
  people think a little worse of the colony). Refuse: the camp's unease rises, and the camps' own rule may bring an evening
  of blows at their fire (`CampBrawls`).
- **Every age:** spirit-hunters with bone rattles (tribal), witch-finders with a book of marks (medieval), a purity league's
  inquirers (industrial), agents of a psy-registry (spacer).
- **Harm:** only a brawl by the camps' own rule. **Mods:** Royalty's psylink, Biotech's xenotypes, read by the game's own
  values.
- Not a duplicate of the penitents, the procession of the faithful or a war of words (a creed's quarrel in the region).

**R3-C4. Rag-and-bone men** (`Rimfolk_Camp_RagPickers`, `CampWorker_R3RagPickers`)
- **When:** any season; more often after a war or in rebuilding (`war`, `rebuild`).
- **On the map:** two or three with a handcart and a beast; by day they sort rags and scrap at their fire; now and then they
  mend a colonist's worn gear (the camps' own [mend]).
- **Answers:** carry out the colony's worn-out things (clothes and weapons in the stores too far gone, and slag: the scrap
  the errands already know): someone of the colony carries them out to the camp, and one of theirs brings the price to the
  gate. Buy a bundle of mended clothes of their age (three pieces, carried to the gate).
- **Every age:** hide-traders who take worn skins (tribal), rag-and-bone men with a handcart (medieval), a rag merchant's
  wagon for the paper mill (industrial), a recycler crew with a hover-cart (spacer).
- **Harm:** none. **Mods:** clothes of their age from the player's active mods: Vanilla Factions Expanded - Tribals'
  `VFET_Apparel_TribalLight`, Vanilla Apparel Expanded's `VAE_Apparel_TribalPoncho`, `VAE_Apparel_Tunic`,
  `VAE_Apparel_FleeceShirt` and `VAE_Apparel_CasualTShirt`, Rimsenal - Feral's `Apparel_PatchedShirt`; the game's tribalwear
  and T-shirt after.
- Not a duplicate of the salvagers (ruins and scrap on the land), the Wheel's runners (a people's one-off buy of scrap at
  the gate) or the traders.

**R3-C5. Gamblers** (`Rimfolk_Camp_Gamblers`, `CampWorker_R3Gamblers`)
- **When:** any; more often with a fair or a season of games in the region.
- **On the map:** two to four camp by the colony; each evening they play dice or cards at their fire, all of them sitting
  round it.
- **Answers:** send {A} to play an evening (a stake of {COUNT} silver, carried out): {A} walks over at dusk and sits at
  their fire; after an hour or two of play: won (one of theirs brings twice the stake to the gate), lost (the stake stays
  with them), or {A} catches their loaded dice (by a sharp eye, Intellectual): the stake is handed back and their camp's
  unease jumps. If {A} never sits down with them, the stake is sent back to the gate. The colony's believers of the
  ledger's god may say a word against the dice.
- **Every age:** knucklebone players (tribal), dicers (medieval), card sharps with a folding table (industrial), holo-dice
  hustlers (spacer).
- **Harm:** only a brawl by the camps' own rule, when their unease runs high. **Mods:** none needed.
- Not a duplicate of the games team (sport), the Circuit's ring (bouts) or the traders.

**R3-C6. Dog-breeders** (`Rimfolk_Camp_Kennel`, `CampWorker_R3Kennel`)
- **When:** any season but the deep cold.
- **On the map:** two or three breeders with four or five dogs, picketed by their tents and one at a breeder's heels; by
  day they work one dog or another, the dog standing for it.
- **Answers:** buy one of their dogs ({COUNT} silver): the youngest of theirs becomes the colony's where it stands, and
  goes to the colony's best with animals and keeps at their heels for a day (the play-out that keeps an animal at someone's
  heels). Learn to train dogs (a lesson, animals). After a good stay they may hand a pup over at the gate (the camps' own
  young beast at the gate).
- **Every age:** sled-dog keepers (tribal), a kennel-man with hounds (medieval), a breeder with papers for every dog
  (industrial), a breeder whose dogs are bred to a registry (spacer).
- **Harm:** none. **Mods:** Vanilla Animals Expanded's `AEXP_GermanShepherd`, `AEXP_Beagle`, `AEXP_Corgi`,
  `AEXP_WelshTerrier`, `AEXP_Poodle`, `AEXP_FrenchBulldog` (active); the game's `Husky`, `LabradorRetriever`,
  `YorkshireTerrier`.
- Not a duplicate of the beast show (a show of beasts), the beast-catchers (wild herds tamed), the travelling trainers or
  the hunting party's dogs.

---

## World events (R3-W1 to R3-W6)

`Defs/WorldSituationDefs/Rimfolk_WS_Round3.xml`, `Source/ColonyCouncil/World/Variety/WorldRound3.cs`, pure rules in
`World/Variety/Round3WorldMath.cs`.

**R3-W1. A mine falls in** (`Rimfolk_WS_MineFall`, `SituationWorker_R3MineFall`; kind `minefall`)
- **In the region:** the miners of a people at peace are trapped behind a fall at one of their towns.
- **Answers:** a runner at the gate asks for help: timber for the props (sixty logs carried out to them), medicine for those
  brought out (carried out), or the colony's best miner for two days of digging (the far errand: a caravan of one there and
  home, with mining learned and a letter of how it went); or nothing to spare.
- **On the map:** the runner at the gate, the logs or the medicine carried out, the miner walking off the map and home again.
- **Endings:** brought out (likelier with the colony's help); brought out by their own diggers; not reached.
- **Every age:** the flint pits fallen in (tribal), the mine's roof down (medieval), a colliery disaster (industrial), a
  mining shaft breached (spacer).
- **Harm:** none. **Mods:** none needed.
- Not a duplicate of the Deepwell Union's drill (a colonist working beside its crew), a great work or rebuilding.
- *Changed while building:* the first list had "the beast of a town" here; it was too near the storyline of the beast coming
  down the valley (S4, `Storylines/StorylineBeast.cs`), so it was replaced.

**R3-W2. The fouled wells** (`Rimfolk_WS_FouledWells`, `SituationWorker_R3Wells`; kind `wells`)
- **In the region:** a people's wells at a town go bad; some blame a dead beast in the spring, some the tanners upstream.
  Only when the colony's land has fresh water.
- **Answers:** at the gate their headman asks whether their water-carriers may draw from the colony's water: let them; let
  them for a fee (their silver handed over); not at the colony's water.
- **On the map:** let, their water-carriers come with their beasts every few days and stand at the colony's water an hour or
  two before going home.
- **Endings:** the spring cleaned (the cause found); water brought down from the hills.
- **Every age:** the spring gone foul (tribal), the town well fouled (medieval), the waterworks gone bad and a fever with it
  (industrial), the station's water recycler failed (spacer).
- **Harm:** none. **Mods:** none needed.
- Not a duplicate of fouled water (the river folk ask the colony to clean what it fouled), a long drought, the herders by
  the water, or the town's well ran dry (S10, a storyline: water and meals collected by the town's carts and a builder lent
  to dig a new well); the endings here are the cause found or water brought down from the hills.

**R3-W3. An expedition** (`Rimfolk_WS_Expedition`, `SituationWorker_R3Expedition`; kind `expedition`)
- **In the region:** a people sends an expedition from its town into the far country. Whether it comes back is settled at the
  start, out of sight, and shown by what follows.
- **On the map:** the expedition passes the colony on the road with its beasts. Their quartermaster comes to the gate:
  sell them food for the road (pemmican carried out to them, the price handed over), give them a guide to the edge of the
  land (someone of the colony walks out with them), or wish them well. If it comes to grief, one of them comes back past the
  colony and asks at the gate: take them in, a meal, or send them on.
- **Endings:** back (if the colony helped, their courier brings a thing from the far country); lost.
- **Every age:** young hunters going over the mountains (tribal), a company of explorers (medieval), a scientific expedition
  with mules and instruments (industrial), a deep survey crew (spacer).
- **Harm:** none. **Mods:** none needed.
- Not a duplicate of the surface scouts or the landing party (spacer camps), the surveyors or an old vault opened.

**R3-W4. A ransom** (`Rimfolk_WS_Ransom`, `SituationWorker_R3Ransom`; kind `ransom`)
- **In the region:** raiders (a hostile people where there is one) took people of a friendly people's town on the road and
  ask a ransom; their people collect it across the region.
- **Answers:** a collector at the gate: give silver (handed over), give food for the captives' road home (carried out),
  nothing to spare.
- **On the map:** if they are ransomed, the freed walk home past the colony on the road with their people's riders.
- **Endings:** ransomed (likelier if the colony gave); taken back by their own warriors; still held.
- **Every age:** captives of a raid, the ransom in furs and shell (tribal), a ransom asked under seal (medieval), a gang's
  ransom note (industrial), a crew held by pirates on a hulk (spacer).
- **Harm:** none. **Mods:** none needed.
- Not a duplicate of a rising in the town, a pardon proclaimed or the raid on a town the colony was.

**R3-W5. The year of the rats** (`Rimfolk_WS_Rats`, `SituationWorker_R3Rats`; kind `rats`)
- **In the region:** rats in a people's granaries and stores; their towns pay rat-catchers.
- **On the map:** rats come onto the colony's land from the fields (the game's own wild rats, a handful, never manhunting).
  A rat-catcher comes to the gate with a dog: hire them ({COUNT} silver, handed over), and the dog goes after the rats on
  the colony's land while the catcher stays the day; send them on.
- **Endings:** the rats gone with the cold, or with the catchers' work.
- **Every age:** mice in the grain pits (tribal), the rat year (medieval), vermin in the warehouses (industrial), pests in
  the hydroponics bays (spacer).
- **Harm:** none. **Mods:** the game's `Rat`.
- Not a duplicate of locusts, a blight or a famine.

**R3-W6. A new way spreads** (`Rimfolk_WS_NewWay`, `SituationWorker_R3NewWay`; kind `newway`)
- **In the region:** a people takes up a new way of working, and the one who brought it walks the region showing it.
- **Answers:** at the gate: have the colony's best at it learn it there (a lesson at the gate, growing or crafting by the
  way); buy their drawings ({COUNT} silver, handed over; the game's schematic handed over by them); not now.
- **Endings:** it spreads across the region; it doesn't catch on.
- **Every age:** a new way of hafting and binding (tribal), the heavy plough (medieval), a new threshing machine
  (industrial), a new fabrication pattern (spacer).
- **Harm:** none. **Mods:** none needed (the game's `Schematic`).
- Not a duplicate of a famous healer (medicine), a hiring fair or the travelling teacher (a camp).

---

## Mods that would help

From `docs/player-mods/mods.tsv`, subscribed but not enabled (to be read on the Workshop before recommending): see the
round's final report. Already enabled and used here: Vanilla Animals Expanded (its dogs), Royalty and Biotech (what the witch-finders look for),
Ideology (the dye).
