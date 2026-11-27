# Content round 4: ten council questions and six world events (27 November 2026)

The player's go of 27 November 2026: "you can go ahead with the content round, maybe also other content, camps, dilemmas,
world events, just further expanding on the mechanics you already have or adding more, so to say, content to the
mechanics ... adding more mechanics for the time being doesn't necessarily make it better since it's already just a lot."
After two rounds were built: "continue". So this is content inside the frames that exist: council questions (`DilemmaDef`
with a `DilemmaWorker` where the roles can't say it) and world events (`WorldSituationDef` with a `SituationWorker_Variety`
where data can't say it). No new system, window, setting, saved collection or `Sched` offset.

Built in `Defs/DilemmaDefs/Rimfolk_Round4_Council.xml`, `Defs/WorldSituationDefs/Rimfolk_WS_Round4.xml`,
`Languages/English/Keyed/Rimfolk_Round4_CouncilWorld.xml` (`CC_R4C_*`, `CC_R4W_*`),
`Source/ColonyCouncil/Dilemmas/DilemmaWorkers_Round4.cs`, `Source/ColonyCouncil/World/Variety/WorldRound4.cs`, the pure rules
in `Source/ColonyCouncil/World/Variety/Round4CouncilWorldMath.cs`, and `Tools/Tests/Round4CouncilWorldTests.cs`; a section
on the world events' Almanac page. Every new class is prefixed `R4C`, `R4W` or `Round4CouncilWorld`.

## The rules on every item

- **Council questions and world events keep the earlier flavour style** (CLAUDE.md, 20 November 2026); the gods' words in
  the old voice; bubble lines one sentence.
- **Every answer plays out on the map** with the play-outs that exist (`Tools/playout_audit.py`), and what the words say is
  what happens. No countdowns in the words.
- **Triggers read only what the game and Rimfolk already know**: the game's moods, hediffs, needs, traits, rooms, beds,
  stockpiles and plants, the seats and decrees in force, the gods' records, a world event running.
- **The gods are unseen.** No god acts in any of these. Where the colony's believers speak, they name the god by the
  colony's own name for it (`{GOD}`), and only in the question of faith.
- **The colony is the family.** Nothing reads kin, houses or lines.
- **Harm:** none of the sixteen hurts anyone by itself. The one fire laid (the beacon) goes through the bonfire's own harm
  rule (a burnt hand, a spark in dry grass) on the shared harm budget, named before the answer.
- **Every age:** each takes the colony's own means (a fire pit or a stove, a long hut or a barracks) and, for the world
  events, the other people's ways (`[theirTribal]`, `[theirMedieval]`, `[theirIndustrial]`, `[theirSpacer]`).
- **The full mod pool:** what is picked is read from the defs' own properties at run time (any stone chunk, any weapon of
  the people's age from every active mod, any animal bed the colony can build), with the game's own by name after.
- **Saved state:** a question's context holds one more word at most (the decree's name); a world event's state is its own
  flags. No new saved collection.

---

## Council questions (R4C-1 to R4C-10)

### The colony's work and stores

**R4C-1. The stone by the walls** (`Rimfolk_DlmR4_Stone`, `DilemmaWorker_R4CStone`)
- **Asked when:** thirty stone chunks or more (any of the `StoneChunks` category, the game's or a mod's) lie in the home
  area out of storage and not forbidden.
- **A** the best builder, who wants the stone in; **B** the best grower, who says the fields can't spare the hands.
- **Answers:** everyone carries the stone in (a work party of five on the stone and the hauling: the game's own hauling,
  the chunks to storage or a heap by the gate); the stone cut into blocks (a bill at the colony's stonecutter's bench for
  blocks from any stone, the game's `Make_StoneBlocksAny`, first on its stack; greyed out with no bench for it); the
  fields first (moods).
- **Ages:** "the stones from the digging lie about the huts" in the older ages, "the rubble" after.
- Not a duplicate of the mess after the fight (a raid's damage cleared), the broom and the bucket (filth), the unfinished
  work (blueprints), the old walls (a ruin) or the stores in the rain (stockpiles under the sky).

**R4C-2. The night owl** (`Rimfolk_DlmR4_NightOwl`, `DilemmaWorker_R4CNightOwl`)
- **Asked when:** a grown-up of the colony with the game's night owl trait (`NightOwl`) has the game's own mood for being
  awake by day (`NightOwlDuringTheDay`) and their mood is under half.
- **A** the night owl; **B** the one who shares A's room, else the colony's most industrious other grown-up.
- **Answers:** A keeps the night hours for a while (the timetable's nights worked and days slept, the plan that exists,
  `apart`, put back after); one night at the bench to see how it goes (`night`: tonight's sleep turned to work, the
  morning slept in); the colony keeps one day (moods).
- **Ages:** the same in every age: the fire, the lamp.
- Not a duplicate of music at midnight (a noisy hobby), the sleepwalker, asleep on watch, the worrier's watch or dawn to
  dark (long days). The light-in-the-bedroom wish for night owls is a wish, not a question of hours.

### The council's own seats and decrees

**R4C-3. The seat while its holder is away** (`Rimfolk_DlmR4_SeatAway`, `DilemmaWorker_R4CSeatAway`)
- **Asked when:** a seat's holder is away from the home map (in a caravan, at a far errand or on another map), the seat has
  no deputy and no election is under way.
- **A** the holder away (named, not present); **B** the one at home most able at the seat's work who holds no seat.
- **Answers:** B stands in until A is home (the deputy that exists, `DilemmaEffect_Deputy`: the seat's daily work done in
  A's place for ten days); B reads out the seat's business at supper (words at the table, B speaking); the seat waits for
  A (moods).
- **Ages:** the seat by the colony's age (`{OFFICE}`, `OfficeNames.InAge`).
- Not a duplicate of a seat put down (a worn holder at home steps down or is given a deputy), a promise halfway (a holder's
  promise not kept), a seat at the table (someone wants a seat) or the empty chair (a meeting held without someone).

**R4C-4. Too hard on one** (`Rimfolk_DlmR4_DecreeToll`, `DilemmaWorker_R4CDecreeToll`)
- **Asked when:** a decree of hard work is in force (one that strains, two a day or more: long hours, rationing, drills,
  the harvest push, the building season, on guard) with a day or more left, and someone (A) it falls hard on: sixty or
  older, ill, troubled by an old injury, or worn (the heart's strain), who dislikes or hates it.
- **A** the one it is too hard on; **B** the one who would take on A's main work, the best at it.
- **Answers:** A has the heart's day off today (`DilemmaEffect_HeartDayOff`, the button's own); A's main work goes to B
  while the decree lasts (the hand-over that exists, for the decree's days left, put back after); the decree is for
  everyone (moods, trust).
- **Ages:** the decree as the colony's age names it; the words have a `[lowtech]` form.
- Not a duplicate of putting down the tools (an elder asks to stop for good), a day off, now (one worn asks for days off
  at a busy time) or carrying everyone (one doing the work of many). Lifting the decree itself is the decree panel's
  repeal and colonists' own requests, so it is not an answer here.

### Guests

**R4C-5. The visiting master** (`Rimfolk_DlmR4_VisitingMaster`, `DilemmaWorker_R4CVisitingMaster`, the warden's seat)
- **Asked when:** a visitor or trader of a people at peace is on the map with a skill of ten or more, five or more above
  the colony's best at it, and a grown-up of the colony with a passion for that skill is at home.
- **A** the one who wants to learn; **B** another grown-up who says the visitors are here to trade, not to teach; **X**
  the visitor.
- **Answers:** an afternoon at X's side (a lesson, X teaching A the skill; X stays on for it); A sits with X at supper and
  asks about it (A and X at the table, facing each other); the visitors are left to their business (moods).
- **Ages:** the evening fire in the older peoples' words (`[theirLowtech]`).
- Not a duplicate of the travelling healer (a healer's paid treatment and lesson in medicine), a traveller's news (an
  evening of news), the only one who knows how (teaching inside the colony) or lessons through the bars (a prisoner).

### Prisoners and slaves

**R4C-6. The cold cell** (`Rimfolk_DlmR4_ColdCell`, `DilemmaWorker_R4CColdCell`, the warden's seat)
- **Asked when:** a prisoner of the colony has the game's own hypothermia, in a cell (their prison bed's room, a proper
  room) that is cold.
- **A** the warden (whoever does the warden's work best); **B** the one of the colony who thinks least of the prisoner.
- **Answers:** a fire in the cell (a campfire the colony can build set in a corner of the cell, fed with forty wood and
  cleared when burned out, the play-out that exists, `DilemmaEffect_KeepFire`); A takes the prisoner a hot meal (a meal
  carried in from the stores and eaten there); the cell stays as it is (moods).
- **Ages:** a fire pit in a hut in the older ages, the same campfire after; the words say "a fire".
- Not a duplicate of a cold bedroom (a colonist's cold room), a voice through the door, the one in the cell, family in the
  cells, lessons through the bars or a prisoner asks to pray.

**R4C-7. A rest for a slave** (`Rimfolk_DlmR4_SlaveRest`, `DilemmaWorker_R4CSlaveRest`, the warden's seat; Ideology)
- **Asked when:** a slave of the colony whose recreation is nearly gone (the game's own need under a fifth) and whose mood
  is low, held down well enough (suppression at least two fifths), and a grown-up of the colony who thinks well of them.
- **A** the one who asks for them; **B** the one who keeps the slaves to their work (the best warden, else the one who
  thinks least of them); **X** the slave.
- **Answers:** X rests the rest of today (the calendar's rest, the slave's own timetable, put back tomorrow; their
  suppression eases a little); A speaks for X at supper (words at the table, A speaking); the work is theirs (moods).
- **Ages:** a thrall, a serf, a bonded worker: the game's own slavery, the same answers.
- Not a duplicate of a slave's freedom (a friend asks for a slave to be freed), the kind word at work or the hard word
  from the overseer (encounters).

### Animals

**R4C-8. A bed for the animal** (`Rimfolk_DlmR4_PetBed`, `DilemmaWorker_R4CPetBed`, the herdmaster's seat)
- **Asked when:** a grown-up of the colony with a bedroom of their own has a bonded animal (the game's bond), small enough
  for a bedroom and not a pen animal, and the room has no animal bed or sleeping spot.
- **A** the one bonded; **B** the colony's best with animals other than A, who says animals sleep with the animals.
- **Answers:** a bed for the animal set in a corner of A's room (the best the colony can build of an animal bed, a
  sleeping box or a sleeping spot, from the game's or a mod's own; `DilemmaEffect_Made` set in the room); A sits with the
  animal of an evening instead (a visit, A keeping it company where it is); animals sleep where animals sleep (moods).
- **Ages:** straw in a box or a fine bed, whatever the colony can build; the words name it (`{THING}`).
- Not a duplicate of a child and an animal, can I keep it, two hands for one animal, an animal in the kitchen or a grave
  for an animal.

### The seasons

**R4C-9. The dry grass by the walls** (`Rimfolk_DlmR4_DryGrass`, `DilemmaWorker_R4CDryGrass`, the marshal's seat)
- **Asked when:** summer, hot (25 degrees or more outside), no rain falling, and thirty or more dry wild plants (grass and
  brush, never trees or sown crops) stand at the edge of the colony's home ground.
- **A** the best grower, who knows how dry grass burns; **B** the best builder, who says the walls are stone.
- **Answers:** a firebreak cut round the walls (that dry growth marked for cutting, the game's own designation, and a work
  party of four cutting it); A walks the walls at dusk watching for smoke (a watch on the move along the colony's edge, the
  vigil that exists); the grass can stay (moods).
- **Ages:** "the grass grows right up to the huts" in the older ages.
- Not a duplicate of smoke in the dry grass (a smoulder already found, an event), the trees by the wall (trees as cover for
  raiders) or snow on the paths.

### Faith (one)

**R4C-10. Words before supper** (`Rimfolk_DlmR4_Grace`, `DilemmaWorker_R4CGrace`)
- **Asked when:** the gods' worship is on, a devout believer of a god (A) is at home, the god has a place at this home,
  one who holds to no god (a scoffer, B) is at home too, and the colony eats at a table.
- **A** the believer, {GOD} the god A holds and {PLACE} its place; **B** the scoffer.
- **Answers:** {GOD}'s words before supper tonight (a short gathering at the dining table, A speaking, the words in the
  old voice); A says the words at {PLACE} and the table eats as it likes (words at the god's place, A alone there); everyone
  eats as they will (moods). No god acts: only what the living believe and feel.
- **Ages:** a `[lowtech]` form (the fire, the pot).
- Not a duplicate of the scoffer at the rite (a scoffer at a rite), the table without meat (a faith's precept at meals),
  ideoligions at odds or an ideoligion evening.

---

The ten, by where they belong: work and stores 1 and 2; seats and decrees 3 and 4; guests 5; prisoners and slaves 6 and 7;
animals 8; the seasons 9; faith 10.

Considered and left: who sleeps by the fire in winter (the banked fires decree covers saving fuel), the herd's first day out
in spring (bringing the herd home covers the other end of it and no play-out lets a herd out), the first basket of the
harvest for a god (the offerings carry what the god likes from the stores, not the harvest itself, so the words could not
be true), the newcomer who is slow (not pulling weight covers it), the decree lifted by the council (the decree panel's
repeal and colonists' requests cover it).

---

## World events (R4W-1 to R4W-6)

Three peaceful, three troubled, each in its people's age's form (`SituationWorker_Variety`).

**R4W-1. The warm springs** (`Rimfolk_WS_WarmSprings`, `SituationWorker_R4WSprings`; kind `springs`; peaceful)
- **In the region:** warm water has broken out of the ground at one of a friendly people's towns ({PLACE}), and the sick and
  the aching of the region go there to bathe.
- **On the map:** bathers of that people pass the colony on the road (a few, older ones among them).
- **Answers (at the gate, their keeper's runner):** send {A} to bathe there for three days (the far errand: a caravan of one
  there and home; A is the one with an aching ailment of Rimfolk's, a strained back, a stiff neck, sore hands,
  chilblains, a limp or a sprained ankle, else the most worn, else the eldest from fifty; home again the ache is eased and
  the strain with it); a gift of silver for the keepers (carried out to the runner); not now.
- **Endings:** the springs keep flowing and the town grows; the water goes cold again.
- **Every age:** a hot spring the old ones keep (tribal), the baths at {PLACE} (medieval), a spa with a pump room
  (industrial), a mineral clinic (spacer).
- **Harm:** none. **Mods:** none needed.
- Not a duplicate of a famous healer (a healer on tour, invited for a fee), the fouled wells (bad water) or the healers'
  sickhouse (a peoples' event).

**R4W-2. The gift road** (`Rimfolk_WS_GiftRoad`, `SituationWorker_R4WGiftRoad`; kind `giftroad`; peaceful)
- **In the region:** after a good year a friendly people sends a gift along the road from settlement to settlement, each
  one keeping it and passing a gift of its own on to the next; this year it comes the colony's way.
- **On the map:** their bearer at the gate with a pack beast; whatever the colony answers, the bearer sets their gift
  down at the gate (goods of their age, worth about a hundred and fifty silver).
- **Answers:** pass on food (meals carried out to the bearer, who takes them on); pass on silver (carried out); keep the
  gift and send nothing on.
- **Endings:** the gift road reaches its last town (the colony's gift named in it if it gave); the road stopped short.
- **Every age:** a carved staff and bundle passed hand to hand (tribal), the gift cart (medieval), the friendship parcels
  (industrial), the goodwill crates (spacer).
- **Harm:** none. **Mods:** what they bring is the frame's own goods of their age.
- Not a duplicate of a new town's welcome, the new head at the gate or a town's founding day (gifts the colony sends), or
  the giving day (the colony's own custom).

**R4W-3. The timber rafts** (`Rimfolk_WS_Rafts`, `SituationWorker_R4WRafts`; kind `rafts`; peaceful; only with a river on
the colony's map)
- **In the region:** a friendly people cuts timber upriver and floats it down to {PLACE} in rafts.
- **On the map:** the raftsmen tie up below the colony and walk up to camp a night at the colony's water.
- **Answers (their foreman at the gate):** buy timber cheap (logs set down at the gate, the price handed over); a hot meal
  carried down to their camp; nothing.
- **Endings:** the rafts reach {PLACE}; a raft broke up on the rocks, and some of its logs wash up on the colony's bank (the
  logs lie there, on the colony's own stretch of river).
- **Every age:** log rafts poled by hand (tribal), timber rafts with a hut on the biggest (medieval), a log drive (industrial),
  towed timber barges (spacer).
- **Harm:** none. **Mods:** none needed.
- Not a duplicate of timber gone scarce (no wood), flood downriver or the river folk's events.

**R4W-4. The beacon fires** (`Rimfolk_WS_Beacons`, `SituationWorker_R4WBeacons`; kind `beacons`; troubled)
- **In the region:** raiders are out (a people hostile to the colony where there is one), and a friendly people lights a
  chain of beacons on the hills so that the warning runs ahead of them; they ask the colony to keep a fire of its own.
- **Answers (their runner at the gate):** keep the beacon (each evening for three evenings someone of the colony lays a fire
  in the open and sits by it into the night, the bonfire that exists; the bonfire's own small risk of a burnt hand or a
  spark in dry grass is named); wood for their beacons (forty logs carried out to the rider); the colony keeps its own
  watch (nothing).
- **Endings:** the warning ran ahead of the raiders and the towns were ready (likelier with the colony's fire); the raiders
  slipped through a gap in the chain.
- **Every age:** smoke by day and fire by night on the hills (tribal), the beacon towers (medieval), a heliograph and
  signal rockets (industrial), relay flares (spacer). The colony's part is a fire in any age.
- **Harm:** only the bonfire's own, on the shared budget. **Mods:** none needed.
- Not a duplicate of a warlord rises, the bandit road, a war between neighbours, the night watch (the colony's own decree)
  or the high watch (a custom of looking out over the land).

**R4W-5. The arms sold off** (`Rimfolk_WS_ArmsSold`, `SituationWorker_R4WArms`; kind `arms`; troubled)
- **In the region:** a people at peace with the colony made a hard peace and must give up its armoury; rather than hand it
  to the victors, it sells the weapons off to its neighbours.
- **On the map:** their seller at the gate with a pack beast and the weapons.
- **Answers:** buy three of them (the weapons set down at the gate, the price handed over: about half their worth); a meal
  for the seller's road; send them on.
- **The weapons:** of the people's age, read at run time from every weapon of the player's active mods and the game's
  (tradeable, of that tech level, of a fair price, never unique or biocoded), the game's own by name after (a spear, a
  longsword, a bolt-action rifle, a charge rifle).
- **Endings:** the terms kept and the town quiet; the rest of the armoury went to the road, and its people say so.
- **Every age:** spears and bows (tribal), swords and crossbows (medieval), rifles (industrial), charge weapons (spacer).
- **Harm:** none. **Mods:** the full mod pool for the weapons (Vanilla Weapons Expanded and its parts, the Factions
  Expanded peoples' weapons, Rimsenal, all active), the game's own after.
- Not a duplicate of sellswords, deserters on the roads, peace talks or the spoils (a raid's gear).

**R4W-6. The missing caravan** (`Rimfolk_WS_MissingCaravan`, `SituationWorker_R4WMissing`; kind `missing`; troubled)
- **In the region:** a friendly people's caravan bound for {PLACE} never arrived. Whether it is found is settled at the
  start, out of sight, and shown by what follows.
- **On the map:** a group of theirs camps a night on the colony's far ground; if it is found, the caravan itself passes
  the colony on the road home with its beasts.
- **Answers (their search leader at the gate):** two of the colony walk the colony's land looking for any sign (a search, the
  play-out that exists); A goes over the hill paths with the leader at the gate; nothing seen here.
- **Endings:** found (lost in the hills and late), the colony's search named if it searched; lost.
- **Every age:** traders with dog sledges or pack beasts (tribal), a merchant's wagon train (medieval), a mule train or a
  lorry convoy (industrial), a cargo crawler (spacer).
- **Harm:** none. **Mods:** none needed.
- Not a duplicate of an expedition (sent into the far country), the bandit road (fewer caravans), a great caravan or a dead
  traveller's journal.

---

## Mods that would help

From `docs/player-mods/mods.tsv`, subscribed but not enabled: see the round's final report. Already enabled and used here:
Ideology (the slaves), the weapon mods (the arms sold off), More Animal Beds (any animal bed the colony can build).
