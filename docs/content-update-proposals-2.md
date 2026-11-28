# The content update, round 2: more of each kind (proposals, 20 November 2026)

The player, 20 November 2026: "In general, I just want additional God events, more immersion, more just further also
building up on the gods in general, how they're integrated, the effects they have, the relations between pawns, the kind
of events that generated from it ... And then also, just variety, further kind of events, camps, world events, normal
storyteller events, dilemmas, etc. Just more of them." And: "So for the content update, yes, I want more events, camps,
world events, and dilemmas, but first do all the integrations". The integrations of `docs/content-update-proposals.md`
are built and released (0.278.0 to 0.285.0). This is the second round: more of each kind.

**Built on the player's go of 27 November 2026** ("you can go ahead with the content round ... more content to the
already existing mechanics"), as content inside the existing frames: D33 to D44 in 0.308.0, D1 to D15 in 0.309.0 (D6
not built: a new mechanic of the book), D16 to D32 in 0.310.0. What each left out is in docs/HANDOFF.md.

## How many there are now

Counted from the defs and the code on 20 November 2026:

- **The gods' own happenings:** 90 (`OwnHappening`): 58 on the land and 32 between the colony's people, Helios's walk into
  the sun among them. Beside them: the gods' comings (the stranger at the gate, the white beast, the great cat, the
  risings and Smoulder's waking), 14 festivals, 8 kinds of vow, the gods' hands in 119 of the game's and Rimfolk's
  events, the gods' doings in the world (kindness and anger, gifts, the Hunger, the quarrels), 2 gods' camps, 10 council
  questions of faith, and the old powers' 14 strange happenings.
- **Rimfolk events:** 425 (each an `IncidentDef` with a `HappeningExtension`): 278 callers, peoples' events and stories
  (Visitors), 54 of the land, the weather and the colony's everyday life (Nature), 50 challenges, 31 from colonists'
  pasts, 12 fights.
- **Council questions:** 296 (about 40 of them seasonal customs and days).
- **Camps:** 83 kinds (2 of them the gods').
- **World events:** 89 (about 80 kinds, the rest follow-ups such as the warlord's son's four turns, rebuilding and the
  road reopening).

## The rules on every item

- Checked against the defs, DESIGN.md and the first list; where an item is close to something, it says "not a duplicate
  of".
- **The gods are unseen.** No favour, wants, rules or truths are shown. A card's words never name a god; its title names
  one only where `Gods.CardName` allows, by the colony's name for it, and the Hunger never. What people say is their own
  guess, and they may be wrong. The colony's book keeps what someone saw, was told or believes.
- **The colony is the family.** Nothing reads kin, houses or lines.
- **Answers play out on the map**, with no countdown in the words. Speech goes through `Speech.Say` / `Together`; bubbles
  are one sentence.
- **Harm** only where an item says so: at the game's own rate, from something on the map, through the shared harm and
  illness budgets, and shown.
- **Every age:** each item says its tribal, medieval, industrial and spacer form.
- **Mods** only from the `active` rows of `docs/player-mods/`, softly (`MayRequire` or by name in code), with a vanilla
  fallback; every def name below was checked there.
- **Text styles** (CLAUDE.md): the earlier flavour style for letters, wishes, ambitions, life goals, personal stories,
  council questions, camps and world events; the old voice for the gods' and faiths' own texts (the book, sayings,
  prayers, the words at a god's place); plain words for event cards, messages, encounters and the gods' cards.

**Sizes:** S (defs and a small worker), M (a play-out or a few hooks), L (several hooks and saved state of its own).

---

## 1. The gods' events (D1 to D15)

Most are new happenings between the colony's people, in the frame 0.281.0 built (`Gods/OnMap/Between/Between.cs`, rules in
`OwnBetweenMath.cs`): each a new `OwnHappening` at the end of the list (so a save keeps its order), on the gods' pace
(`BetweenPaceOf`, `FollowChance`, `GodTouch.Turn`), a card in three wordings, the god's look where it acted
(`GodTouchFx`), the book (`GodsOnMapWords.Saw`, `GodLore.Disputed`), and an outcome between the two
(`BetweenOutcome`: closer, a quarrel, made it up, ashamed, resentful, wary, laughed at, a kindness remembered).

**D1. The dare** (M)
- **On the map:** a scoffer and a believer are together after the believer was seen at their small rite. The scoffer
  dares the god to show itself by something the next day settles: rain by morning in a dry spell, the sick one up by
  morning, meat from tomorrow's hunt, the light at the god's place still burning at dawn with nobody seeing to it. Each
  says one sentence. In the morning the two walk out together to look. As it falls out: the believer proud and the
  scoffer shaken a little; the believer quiet and the scoffer pleased; or they argue what it means. Nothing says a god
  answered or didn't. A god drawn near may answer in its own way (hidden: `GodWeather.Answer`, the touch's effects).
- **Ties:** `Between.cs`, `FaithHeart` scoffers (`Worship.Scoffer`), `GodNear`, `GodWeather.Answer`, `PawnGods`
  (`BeliefEvent.Bickered`, or a new "tested"), `GodLore.Disputed`.
- **Every age:** rain, the sick and the hunt in every age; the light is a torch, a candle, an oil lamp or an electric
  light that a believer of the Hum says will hold through the storm.
- **Harm:** none. **Mods:** none needed.
- **Text:** card plain; bubbles one sentence; the book's verse in the old voice.
- Not a duplicate of the scoffer's mock (`FaithHeart.Mock`, a word to a believer's face) or Pip's talking to the machine
  (`PipTalked`): this one waits a night and is settled by what anyone can see.

**D2. Came through: the doctor or the god** (M)
- **On the map:** someone who came through a wound or sickness that nearly killed them (bled near to death and tended, a
  deadly illness past its worst, the cold or heat `PawnGods.LivedThrough` already reads) walks, once on their feet, to
  the place of the god they hold and leaves something there: the weapon broken in the fight, a raider's blade, a dented
  helmet, the cup they drank from in bed. The doctor who tended them most hears of it: one who holds the same god walks
  over and stands with them (closer); a doubter or one of another god says it was the stitches and the medicine (a sore
  one resents it, a good sport laughs). Someone who prayed by the bed to their own god may say so. What was left is
  taken later in the god's own way, as any gift is, or stays.
- **Ties:** `PawnGods.LivedThrough` (wounds added), `Gods/Worship/Offerings.cs`, `OfferingWays.cs`, `GodTouch.Taken`, the
  doctor's tending record, Between.
- **Every age:** a spear point, a sword, a rifle stock, a cracked visor.
- **Harm:** none. **Mods:** none needed.
- **Text:** card plain; bubbles one sentence; the book old voice.
- Not a duplicate of Helios's walk into the sun (C9) or `LivedThrough`, which moves belief with nothing seen.

**D3. The believers take sides** (M)
- **On the map:** while two gods' quarrel shows in the weather or on the ground (`GodDrama`: snow then a warm day, rain
  against snow, wild growth against a ring of mushrooms), the colony's believers on each side keep their small rites more
  often (seen at the tree, the rock, the water), and at supper two of opposite sides argue whose turn it was (a quarrel,
  each held harder to their own). On the quarrel's last turn the side whose weather came last says so. The peacemaker
  (else the chaplain, else the best talker) may sit the two at one table: made it up, or a lasting coolness.
- **Ties:** `Gods/World/GodDrama.cs` (today one word from whoever saw a turn), Between, `BeliefEvent.Bickered`, the
  peacemaker's seat (`Defs/OfficeDefs/`), `PawnGodsMath.RiteOf`.
- **Every age:** the turns are weather and ground in any age; the seat named by `OfficeNames.InAge`.
- **Harm:** none (a quarrel, no blows). **Mods:** none needed.
- **Text:** card plain; bubbles one sentence; the book old voice.
- Not a duplicate of named seasons taken for a god's doing (C18, at a season's end) or the cloud shapes
  (`CirrusCloudShape`).

**D4. The guest at the burying** (Ennu; M)
- **On the map:** at a colonist's funeral, while the eulogy is said, a stranger walks in from the edge and stands at the
  back. When the speaker finishes, the stranger says one true thing about the dead that nobody at the colony knew (from
  the dead's own past: where they grew up, a name they lost, something they did on a day the chronicle has), and leaves
  before dark, never coming to the gate. In the morning someone finds something on the grave: bread and salt, a coin, a
  dried flower. Disguised as the Guest is, with small tells for the observant; named on the card only where the colony
  knows the Guest.
- **Ties:** `Gods/OnMap/GodStranger.cs`, `Presence/GodAvatar.cs` (`GodAvatar.Make`), `GodDeeds.Funeral`,
  `Gods/Among/GraveGods.cs`, `OwnHappenings.Found`, the strangers' gap (120 days, `GodsOnMapMath`).
- **Every age:** a person of a people at peace in their own dress; the thing on the grave by the colony's age.
- **Harm:** none. **Mods:** none needed.
- **Text:** card plain, three wordings; the stranger's line one sentence; the book old voice.
- Not a duplicate of Morel's mourner (a figure of threads at the graves' edge at dusk, silent, never at the funeral) or
  the stranger at the gate (`EnnuTest`).

**D5. The children's little shrine** (M; with Biotech)
- **On the map:** after a child heard a piece of a god's story at supper, two or more children carry stones and sticks
  to a corner by the edge, set them up as a little place for that god (or something they made up), and leave part of a
  meal there. Then: a grown-up who holds that god helps them set a flat stone on top (closer); the keeper tells them the
  god's name as the book has it, and the book notes what the children call it; or a scoffer kicks it over (the children
  cry, and those fond of them think worse of the scoffer). By morning the food is gone, taken by a fox, in a god's own
  way, or by nobody seen; people say what they think.
- **Ties:** Biotech children (`DevelopmentalStage.Child`), `GodTales.Tell`, `GodLore` (the children's name for it),
  `PawnGods` (a child's first belief), `OfferingWays`, Between.
- **Every age:** stone chunks and sticks (tribal, medieval); slag and a painted can (industrial, `ChunkSlagSteel`); a toy
  and a small light (spacer).
- **Harm:** none. **Mods:** none needed.
- **Text:** card plain; the children's lines one sentence; the book old voice.
- Not a duplicate of the children's council questions (the child's question, where the dead go): this one is a scene,
  not a question.

**D6. A name of the colony's own** (L)
- **On the map:** when something nobody could name happens at one place three times or more (a mark on the land, a
  ring, a light, a sound: the book's entries with no god), the colony's people start calling what is there by a name of
  their own: someone says it first at supper ("the Old One at the spring"), others take it up, children leave things
  there, and the book opens a chapter under that name. Later a teller (a visitor, a letter, the novices of D37) tells
  something that matches a known god, and the keeper works out that the two names are one and joins the chapters, or
  works it out wrong and joins it to another god. What is really there stays hidden.
- **Ties:** `Gods/GodLore.cs` (pages, two names worked out to be one), `GodLoreVerses.cs`, `GodMarks`,
  `OwnHappenings` (by place), `GodLoreMath.Attribute`, `GodSigns` (the colony's own drawing once the chapter says what it
  is). Saved: the names coined, kept small, with a model in `Tools/save_estimate.py`.
- **Every age:** the name by place and age: "the Old One of the Spring", "the Lady of the Well", "the Ghost of Shaft
  Two", "the Echo in the Hull".
- **Harm:** none. **Mods:** none needed.
- **Text:** the book's verses old voice; the name said aloud plain, one sentence.
- Not a duplicate: today the book names a god only by a name heard from a people (or the speaker's own people's), and
  keeps the rest as "something nobody could name".

**D7. The one who turned** (S to M)
- **On the map:** after a crisis of faith turns someone from one god to another (`FaithHeart.TurnAway`, today one
  message), the next day they carry the small thing of their old rite (a stone from the rock, a sprig from the tree) to
  the new god's place and set it down. One who holds the old god meets them on the way: a friend says they understand
  (closer), others call it a betrayal (a quarrel). One who holds the new god walks the last steps with them and says the
  god's words (closer). A priest of the old god asks them, in one sentence, to think again.
- **Ties:** `Gods/Worship/FaithHeart.cs` (`TurnAway`, `CC_Wsh_Turned`), `BeliefRite`, `Shrines.cs`, Between, the
  chaplain's seat.
- **Every age:** the same in every age; the rite's things by the god.
- **Harm:** none. **Mods:** none needed.
- **Text:** card plain; bubbles one sentence; the new god's words old voice.
- Not a duplicate of doubts in the faith (`Rimfolk_DlmY_Doubter`, one who stops believing).

**D8. A machine's wake** (Pip; M)
- **On the map:** when a machine that served the colony long is destroyed (a mechanitor's mech of a year or more, a
  turret that held through five raids, a vehicle, a generator that ran three years), the one who repaired it most and the
  Hum's believers stand round the wreck at dusk; its keeper says one sentence; someone carries a piece of the slag to
  their room or to the Hum's place. A scoffer laughs at mourning a machine (a sore one is resented) or comes and stands
  too (closer).
- **Ties:** Biotech's mechs, turrets (Rimfolk's raid count), `ChunkSlagSteel`, `PawnGodsMath` (who holds Pip), Between,
  a line in the news.
- **Every age:** industrial and spacer for machines; a tribal or medieval colony gets it for its oldest workbench or loom
  lost to fire or a raid (Pip's second domain is craft), and not at all where it has neither.
- **Harm:** none.
- **Mods:** Alpha Mechs' mechs count as any mech; Vanilla Vehicles Expanded's vehicles (both active).
- **Text:** card plain; bubbles one sentence; the Hum's words old voice.
- Not a duplicate of Pip's own (a machine that won't stop, the console at night, the turret's held fire, two hands on a
  broken machine).

**D9. The offering nobody's god took** (M)
- **On the map:** in the night someone hungry, a child, or one with a greedy heart walks to a god's place and eats or
  pockets what was left there. Someone awake sees it from a window or the wall. In the morning the believers find it gone
  and say the god took it, and the book writes it so. The one who saw: tells (the taker ashamed, the believers angry, the
  book's entry disputed: "others hold, with Mara, that Tobin ate it"); or keeps it (a secret between the two, which may
  come out later as secrets do).
- **Ties:** `Gods/Worship/Offerings.cs`, `OfferingWays.cs`, `GodTouch.Taken` (the believers' reaction is the same seam),
  a new secret in `Defs/SecretDefs/` with `Secrets/Secrets.cs`, Between, `GodLore.Disputed`.
- **Every age:** the same.
- **Harm:** none. **Mods:** none needed.
- **Text:** card plain; the secret's words plain; the book old voice.
- Not a duplicate of the food that went missing (`Council_Secret_StoresThief`) or the thief in the stores (a council
  question).

**D10. The book read at the fire** (M)
- **On the map:** once a season, on a quiet evening, the keeper takes the colony's book of the gods to the fire and reads
  the newest chapter aloud, a verse at a time over their head, to whoever sits. Those listening hold the god a little
  more. One whose own words are read may say it wasn't like that and tell it their way: the keeper writes "Others hold,
  with ...", or crosses the verse through and writes it again in their own hand. The keeper may say how a god seems to
  them toward the colony, in words only and maybe wrong (the book's temper). A scoffer gets up and walks off.
- **Ties:** `Gods/Among/KeeperAtWork.cs` (C12), `GodLoreVerses.cs`, `GodLorePage.cs`, `GodLore.Disputed`,
  `Gods/GodTemper.cs`, `PawnGods` (`BeliefEvent.Told`), `PlayOut/PlayListen.cs`.
- **Every age:** by firelight, candle or lamp; a spacer keeper reads from a slate, and the words say so.
- **Harm:** none.
- **Mods:** none needed; Epochs - Incense (subscribed, not enabled) would put a burner beside the reader.
- **Text:** the verses old voice, one sentence each aloud; the card plain.
- Not a duplicate of the year read aloud (A29, the chronicle) or the keeper's sermon at supper (a piece of a tale).

**D11. A sign at the wedding** (S to M)
- **On the map:** during a wedding (the game's `MarriageCeremony`), something happens that the guests take for a sign:
  rain begins on the couple, a white bird or a thrumbo crosses the land's edge, a torch goes out, a ring of mushrooms is
  found at the spot next morning, or, when a god is near, one of its own small signs. Two guests say what they take it
  for, one a blessing and one a warning, each holding their own god harder; the couple remember it. The book keeps it as
  the guests said it.
- **Ties:** the game's `MarriageCeremony`, `GodOmens`, `GodOmensOwn`, `GodWeather`, `GodNear`, Between, `GodLore.Saw`.
- **Every age:** the same in every age.
- **Harm:** none.
- **Mods:** none needed; P-Music (subscribed, not enabled) for the music.
- **Text:** card plain; bubbles one sentence; the book old voice.
- Not a duplicate of a wedding at the colony (`Rimfolk_Evt_Wedding`, a couple from outside) or the wedding dreams.

**D12. A word over the raider** (S)
- **On the map:** after a raid whose raiders cried a god (C14), one of the colony who holds that same god walks to a
  fallen raider when the fight is over, kneels, says one sentence over them, and asks that they be buried rather than
  burned or dumped. The marshal, or one who lost a friend in the fight, says what they think: a quarrel, or a nod and
  they stand there together (closer).
- **Ties:** `Gods/Among/RaidersGods.cs`, `GraveGods.cs`'s words, Between; the council's question of the enemy dead
  (`Council_Dilemma_EnemyDead`) stays its own.
- **Every age:** the same in every age.
- **Harm:** none. **Mods:** none needed.
- **Text:** card plain; the words over the body old voice.
- Not a duplicate of the enemy dead (the council's question): this is two people at the body.

**D13. A gift at the other's place** (S)
- **On the map:** some days after two of the colony quarrelled over their gods (D3, `CirrusCloudShape`, `MorelSealed`,
  any `Bickered`), one of them (kind, or the one who likes the other more) carries something to the other's god's place
  and leaves it there. The other sees it or is told, and they make it up. A zealous believer of the giver's own god calls
  it disloyal (wary).
- **Ties:** Between outcomes, `Offerings.cs`, `Shrines.cs`, `PawnGods`.
- **Every age:** the same in every age.
- **Harm:** none. **Mods:** none needed.
- **Text:** card plain; bubbles one sentence.
- Not a duplicate of sorry in the morning (`SeleneSorry`, after an insult) or one cup between them (`RevelRivalsCup`).

**D14. Suspected of a bargain** (M)
- **On the map:** when one of the colony has had a run of luck others notice (the beasts shy from them, silver they seem
  to have, a wound healed fast, a rare gift found by them, the great cat's favourite's lucky season), someone who saw it
  says to their face that they made a bargain with the dark. The accused denies it, or, where it is true (the Hunger's
  bargain, `GodVoid`), may own it. A friend stands up for them (a quarrel with the accuser); believers of the gods that
  turn from such bargains keep away for a while; owned, the chaplain may sit a night's vigil with them at a god's place.
  The accusation can fall on someone who made no bargain at all, and nothing says which.
- **Ties:** `Gods/World/GodVoid.cs`, `UndertowShun`, `SacredAnimal.Lucky`, `Gods/Touch/GodRareGifts.cs`, Between,
  `Secrets/Secrets.cs`.
- **Every age:** the same; the dark named by the speaker's people's name, never on the card; the Hunger never named.
- **Harm:** none. **Mods:** none needed.
- **Text:** card plain; bubbles one sentence; the book old voice.
- Not a duplicate of the whispering at the edge (`UndertowWhisper`), which is only seen.

**D15. Lightning on the shrine** (M)
- **On the map:** in a thunderstorm, at most once a year at a home, when the colony has kept one god's place busy
  (offerings, a festival, a vow) and someone at home or a people near holds that god's rival, lightning strikes the place
  or the tree beside it: the game's own strike, scorch and fire. The rival's believers say whose they take it for; the
  place's believers say it was the storm, a punishment or a test. In the morning they rebuild or replant it together (a
  work party), or argue whether to. The card says what was struck.
- **Ties:** `GodTiesMath` (rivals), `GodDrama`, `GodWeather`, `Shrines.cs`, the game's lightning strike, Between.
- **Every age:** a wooden place burns, a stone one is scorched, a later colony's light at the place blows.
- **Harm:** the game's own lightning and fire, never aimed at a person, through the shared budget
  (`HarmKinds.GodLightning`), shown.
- **Mods:** none needed.
- **Text:** card plain; bubbles one sentence; the book old voice.
- Not a duplicate of a god's grudge (`GodHealth`: lightning on one colonist) or the quarrels' turns (D3).

---

## 2. Storyteller events (D16 to D23)

Rimfolk events (`IncidentDef` with a `HappeningExtension`, fired by `Happenings/Happenings.cs`, answers through the
dilemma effects and play-outs), ordinary RimWorld happenings on the map with a choice and a play-out.

**D16. Not in by dark** (M)
- **On the map:** at dusk in fog, a snowstorm or a dark with no moon, someone working far out (hauling, hunting, cutting)
  has not come in, or a child out playing has not. The card: two or three go out with lights toward where they were last
  seen, calling (the lost one wanders a little until they meet, and they walk home together; the lost one thinks better of
  those who came); someone stands at the edge with a light and a horn or bell so the lost one walks toward it (they come in
  later, cold); or wait for morning (they find their own way at first light, colder).
- **Ties:** `Happenings/Happenings.cs`, a search play-out (`PlayOut/PlayRoutes.cs`, `Together`), a wander job of its own,
  the weather, `Harm/ColdHurts.cs`.
- **Every age:** a horn and a fire on the hill (tribal); lanterns and a bell (medieval); flares and whistles
  (industrial); suit lights and a beacon (spacer, and seldom).
- **Harm:** the night's cold for whoever is out, at the cold's own rate (`ColdHurts`), only when they wait for morning or
  the search is long; shown.
- **Mods:** the fogs: the game's `Fog` and `FoggyRain`, Odyssey's `BlindFog`, Seasonal Weather (Continued)'s `FogSnow`,
  ReGrowth 2's `RG_FoggySnow`; Giddy-Up 2's riders search faster.
- **Text:** card plain, three wordings, no countdown.
- Not a duplicate of waiting up for the late one (`EnnuWaitedUp`, a quiet scene with no danger or choice), the lost
  traveller or the lost child (outsiders).

**D17. The fish are running** (S to M)
- **On the map:** in spring or autumn, where the colony has a river or lake it fishes, the water fills with fish for three
  days. The card: everyone who can be spared to the water (a work party fishing, the catch better while it lasts); a few
  fish now and the rest put by for winter (the catch made into pemmican or frozen); or let them pass. A bear may come down
  to the bank too and keep to itself unless hunted. The Murmur's believers give back the first fish as they do (C7).
- **Ties:** Odyssey's fishing (`FishingYield`), a three-day condition of Rimfolk's, `Gods/Among/MurmurFishers.cs`, a work
  party play-out.
- **Every age:** spears and weirs, nets, rods; the game's fishing in every age, the words by age.
- **Harm:** none of its own; a bear hunted at the bank answers at the game's revenge chance (`Harm/HuntsBite.cs`).
- **Mods:** Odyssey's `Fish_Salmon`, `Fish_Cod`; Vanilla Fishing Expanded's `VCEF_FishingLuckOffset`, `VCEF_RawTrout`,
  `VCEF_RawHerring`; the game's `Pemmican`; the game's `Bear_Grizzly`, Vanilla Animals Expanded's `AEXP_BlackBear`.
- **Text:** card plain.
- Not a duplicate of the river folk's catch, the fishers' and ice fishers' camps, or C7.

**D18. A bear asleep in the rock** (M)
- **On the map:** in winter, a miner breaks into a hollow in the mountain and finds a bear asleep in it. The card: wall it
  up again (a wall built across the gap; in the first warm days the wall is taken down and the bear walks out and off the
  land); the best handler tries to tame it while it sleeps (an hour by it each day, the game's own taming chance and its
  own risk if it wakes angry); or hunt it where it lies (meat and a pelt).
- **Ties:** mining (`Happenings/Dangers.cs`, as the creak at the dig), the game's taming and hunting,
  `Harm/HuntsBite.cs`. Saved: the walled den until spring.
- **Every age:** the same; torches and spears or rifles, by the colony's own weapons.
- **Harm:** the bear's own, only if a tame or a hunt wakes it, at the game's revenge chance; shown.
- **Mods:** the game's `Bear_Grizzly` and `Bear_Polar` (in the cold), Vanilla Animals Expanded's `AEXP_BlackBear`.
- **Text:** card plain.
- Not a duplicate of the den in the brush (`Council_DlmS_PredatorDen`, a predator near the fields) or the creak at the
  dig.

**D19. Water for the fields** (M)
- **On the map:** in a drought with crops in the ground, the reeve (else the best grower) asks. The card: a water chain at
  dawn, everyone free carrying water from the river, lake or well to the fields for two hours (the watered field grows
  that day as if it had rained); let the farthest field go, cut for hay, so the rest gets what water there is; or wait it
  out. The Murmur's song may still come in the dry spell, as now.
- **Ties:** the droughts, `PlayOut/PlayCarry.cs`, the reeve's acts (A1), `OwnMurmur` (the song), plant growth.
- **Every age:** pails and clay jars (tribal, medieval), buckets and a hand cart (industrial), water cans (spacer); the
  carrying the same, the words by age.
- **Harm:** none.
- **Mods:** Odyssey's `Drought` and Vanilla Events Expanded's `VEE_Drought` (game conditions).
- **Text:** card plain.
- Not a duplicate of the thirsty herd (drovers at the water) or breaking the drought (a people's event).

**D20. The animal that's wrong** (S to M)
- **On the map:** at dawn someone sees a wild animal at the edge acting wrong: walking in circles, foaming, snapping at
  nothing. The card: the best shot goes out to kill it from range (a hunt); bring the animals into the pens and keep the
  children in until it's gone (a play-out); or let it be. Let be, later that day it goes mad as the game's own mad animal
  does and comes for whoever is nearest.
- **Ties:** the game's `AnimalInsanitySingle` (fired on that animal when let be or missed), the herd brought in
  (`Animals/`), `HuntsBite`.
- **Every age:** bow, crossbow, rifle or charge lance.
- **Harm:** the game's own mad animal's bites, only when it is let be or the shot fails; counted in the shared budget,
  about the game's own rate for a mad animal; shown.
- **Mods:** now and then, with Sanguinary Animals Reborn, the animal is a `pphhyy_SanguineDeer`.
- **Text:** card plain.
- Not a duplicate of something at the pens (a predator) or the wild one; it is the game's mad animal with a warning and a
  choice first.

**D21. The one who hid** (M)
- **On the map:** after a raid, when the raiders flee, one of them stays hidden at the far side of the land. A day later a
  hunter or woodcutter finds them, hungry, in cover; if their people cry a god (C14), they heard them praying. The card:
  take them prisoner (two go to bring them in, and they may fight); give them food and let them walk home (their people
  remember it); or drive them off with shots over their head (they run).
- **Ties:** the game's raid leaving the map, `World/FactionMemory.cs`, `Gods/Among/RaidersGods.cs`, the game's arrest.
- **Every age:** the same in every age.
- **Harm:** the raider's own fight if cornered, one against two, by the game's own combat; shown.
- **Mods:** none needed.
- **Text:** card plain.
- Not a duplicate of the one still breathing (`Council_DlmS_FallenPlea`, a downed raider) or the raiders' camp's runaway
  (B13, a captive).

**D22. Stuck in the bog** (S to M)
- **On the map:** after rain, one of the colony's animals (or a wild deer or horse) sinks in the marsh or mud at the
  water's edge and can't get out; it shows on its health tab and it doesn't move. The card: two of the colony pull it out
  with a rope (an hour, muddy, and wanting a wash after); lay boards out to it (wood used, quicker and cleaner); or, for a
  wild one only, leave it.
- **Ties:** the game's `Marsh` and `Mud`, a Rimfolk condition "stuck", a play-out (`PlayOut/PlayCarry.cs`), the
  animal's bond.
- **Every age:** rope, a winch, a mount or a vehicle pulling.
- **Harm:** none to the colony; a wild one left may die of the cold, the weather's own.
- **Mods:** Giddy-Up 2's mounts and Vanilla Vehicles Expanded's vehicles pull where the colony has them; Dubs Bad
  Hygiene's hygiene for the wash (all active).
- **Text:** card plain.
- Not a duplicate of the ground giving (`Rimfolk_Evt_SoftGround`, under a building).

**D23. Bad air in the deep** (M)
- **On the map:** when the colony mines deep under thick rock (the deep that draws Smoulder's rumbles), a newly opened
  face lets out a pocket of bad air: the game's tox gas over a few cells. A miner coughs and comes out, and the light
  they carry shrinks. The card: open a shaft to the air (the nearest rock to the outside marked for mining); work in
  short turns until it clears; or wall it off. The Smoulder's believers say the deep breathed.
- **Ties:** `GodsOnMapMath.DeepNow`, `Happenings/Dangers.cs`, the game's gas, a believer's line as the gods' hands give
  them, the harm budget.
- **Every age:** a candle that gutters (tribal, medieval), a lamp (industrial), a suit's alarm (spacer, whose miners mind
  it least).
- **Harm:** the gas's own on whoever stays in it, through the shared budget (a new `HarmKinds` entry), shown.
- **Mods:** none needed.
- **Text:** card plain.
- Not a duplicate of the creak at the dig or the hot floor before stone falls (`SmoulderHeat`).

---

## 3. Council questions (D24 to D32)

All in the flavour style the player restored (CLAUDE.md), each answer played out (`Tools/playout_audit.py`), each
balanced with `Tools/dilemma_balance.py`. Five of the nine are the gods' and the faith's, four are everyday life.

**D24. The room nobody will sleep in** (M)
- **Asked when:** a colonist who died had a room of their own, it passed to the one closest to them (A30), and that one
  has not slept in it for three nights. Asked by a friend of the dead.
- **Answers:** the keeper (or chaplain) says words in the room at dusk with whoever comes, and the heir moves in; the room
  is cleared for the colony (the bed and the dead's things carried to the stores, the room left for whatever the colony
  makes of it); left as it was for a season (those who loved them may sit in it on a quiet day).
- **Ties:** `Lives/Legacy.cs`, `Social/FamilyLifeMath.cs` (closeness), `Gods/Among/KeeperAtWork.cs`,
  `PlayOut/OnMapRooms.cs`, the calendar's quiet days.
- **Every age:** the same. **Harm:** none. **Mods:** none needed.
- **Sample opening:** "Nobody has slept in {DEAD}'s room since the burying. {A_nameDef} says the door still sticks the
  way it did when {DEAD} slammed it, and every time it sticks, somebody in the yard looks up."

**D25. How to lay them down** (M)
- **Asked when:** a colonist dies who held one god, and the one closest to them holds another whose ways differ:
  Tamiu's keep the dead whole (a sarcophagus), Morel's give them to the soil (a plain grave by growing things), the
  Smoulder's and Helios's give them to the fire (a crematorium where the colony has one).
- **Answers:** as the dead held; as the one closest asks; as the colony always does. Each plays out by the game's own
  burying or cremating, and the eulogy gives them to the god of the way chosen (C15).
- **Ties:** `Gods/Among/GraveGods.cs`, `GodDeeds.Funeral`, `PlayOut/PlayDead.cs`.
- **Every age:** by what the colony can build; a wood-fired crematorium from the neolithic on.
- **Harm:** none.
- **Mods:** the game's `Sarcophagus` and `ElectricCrematorium`; Vanilla Factions Expanded - Classical's
  `VFEC_WoodFiredCrematorium`; Vanilla Furniture Expanded - Factory's `VFEFactory_AutomatedCrematorium`; Alpha Memes'
  `AM_Urn` for the ashes. Dark Ages: Crypts and Tombs (subscribed, not enabled) would add `DA_Coffin` and `DA_BurialUrn`.
- **Text:** flavour style; the words over the dead old voice.
- Not a duplicate of a funeral in advance (`Council_Dilemma_OwnFuneral`) or where the dead go (a child's question).

**D26. The ordeal** (M)
- **Asked when:** two of the colony accuse each other (a rival's word, a thief in the stores, the miser's silver), and
  someone devout to the Smoulder asks that the hearth decide.
- **Answers:** let the ordeal decide (at the hearth before witnesses each carries a hot stone across it; the believers
  take the one less burned for the truthful, and the book keeps it as their belief); the council judges by what it knows;
  let it lie.
- **Every age:** in a tribal or medieval colony, the hot stone or the iron; in an industrial or spacer colony, an oath
  sworn with a hand on the furnace's housing before witnesses, with no burn, the believers then watching the one who swore
  and taking any later bad luck for the deep's answer (their word, nothing more).
- **Ties:** `Council_Dilemma_Soc_Accusation`, `Council_Dilemma_Thief`, `DucatMiser`, `PlayOut/PlayFire.cs`,
  `GodLore.Believes`.
- **Harm:** in the older ages only, a burn across the hands for one or both, through the shared budget
  (`HarmKinds.GodBurn`), said in the outcome.
- **Mods:** none needed.
- **Text:** flavour style.
- Not a duplicate of a duel of honour (`Council_Dilemma_Duel`).

**D27. A prisoner asks to pray** (S to M)
- **Asked when:** a prisoner of a people that holds gods has been held three days or more.
- **Answers:** walked to a god's place under guard (the warden and one more; they kneel an hour; their resistance eases a
  little; their people hear of it); a small figure of their god set in their cell (from the stores; they pray there);
  no (their mood worse; one of the colony who holds the same god may object at supper).
- **Ties:** `Gods/Among/RaidersGods.cs`, the game's prisoners, `Shrines.cs`, `Worship.Booked`,
  `World/FactionMemory.cs`.
- **Every age:** the same. **Harm:** none (a guard goes with them).
- **Mods:** Vanilla Ideology Expanded - Relics and Artifacts' `VIE_RelicInertIdol` as the figure where the stores have
  one, else the game's small sculpture.
- **Text:** flavour style; the prisoner's prayer old voice.

**D28. The feast after a death** (S to M)
- **Asked when:** a god's festival or a seasonal custom falls within three days of a colonist's death.
- **Answers:** keep it with a place set for them (the feast as usual, an empty seat with their cup, a toast; the
  grieving eased a little); the rite only (the priest at the god's place, no feast); keep it later, once the mourning
  days are past (the calendar moves the day).
- **Ties:** `Gods/Worship/Festivals.cs`, `Calendar/CalendarKeeping.cs`, `PlayOut/PlayFeast.cs`, `GraveGods.cs`.
- **Every age:** the same. **Harm:** none. **Mods:** none needed.
- **Text:** flavour style.
- Not a duplicate of the feast in a hard season (C13: hunger or a raid).

**D29. A path: the shrine** (S to M)
- **Asked when:** a youngster coming of age holds a god of their own and asks to follow the keeper of the colony's book.
- **Answers:** they follow the keeper for a season (on the keeper's walks to look at what was seen and at the evening
  writing, C12; the tales told to them piece by piece; social and intellectual practice; their belief grows); a path of
  their own choosing (the other eight paths); not yet.
- **Ties:** `Council_Dilemmas_ComingOfAge.xml`, A8's paths (`Rimfolk_Dilemmas_ComingOfAge_More.xml`),
  `Gods/Among/KeeperAtWork.cs`, `GodTales.Tell`, `PawnGods`.
- **Every age:** a shaman's helper, a novice, a deacon's helper, a chaplain's assistant.
- **Harm:** none. **Mods:** none needed.
- **Text:** flavour style.

**D30. The work my god won't have** (M)
- **Asked when:** a devoted believer is set to work their god refuses, by the god's own ways (Hollin's won't fell a grown
  tree, Bellwether's won't hunt the wild grazers, Tamiu's won't butcher a cat, Morel's won't burn the dead), and stops at
  it with a word.
- **Answers:** excused from that work for a season (their work tab, put back after); they do it after a minute at the
  god's place each time, asking pardon (their small rite); they do it like everyone else (strain, and their belief
  shaken).
- **Ties:** `GodShapeDef` / `GodWaysDef` (what each god refuses), `PawnGods`, `BeliefRite`, the heart's strain.
- **Every age:** the same. **Harm:** none. **Mods:** none needed.
- **Text:** flavour style.
- Not a duplicate of a bond against the ideoligion (`Council_DlmA_BondFaith`, Ideology's precepts).

**D31. One of the camp died in the night** (S to M)
- **Asked when:** someone of a camp on the colony's land died there (of sickness, age or a fight). Their people ask.
- **Answers:** a grave in the colony's ground (their people carry them; the words by their people's name for their god;
  when that camp comes back it visits the grave, B31); carried home (the camp leaves early with them; their people
  remember it); the colony's ground is for its own.
- **Ties:** `Camps/Camp.cs`, `CampLife`, `GraveGods.cs`, camps that come back (B31), `World/FactionMemory.cs`.
- **Every age:** the same. **Harm:** none. **Mods:** none needed.
- **Text:** flavour style.
- Not a duplicate of fever at the camp (`Council_DlmL_CampSick`) or the mourners passing through.

**D32. The new age** (M)
- **Asked when:** the colony finishes its first research of a later age, or builds its first thing of it (a forge, a
  generator, a comms console). Asked by an elder.
- **Answers:** a day to mark it (kept each year in the calendar; this year a gathering at the new thing at dusk); the old
  thing kept in use beside it (the old hearth or campfire kept lit, fuel spent, the elders glad); let it pass quietly
  (the young don't mind, the elders are sad).
- **Ties:** the colony's age (`OfficeNames.InAge`, `CampBuildMath.AgeOf`), `Calendar/CalendarCustoms.cs`,
  `PlayOut/PlayFire.cs`.
- **Every age:** the item is the change of age itself, once for each.
- **Harm:** none. **Mods:** none needed.
- **Text:** flavour style.
- Not a duplicate of the old way and the new (`Council_DlmLv_OldWayNewWay`, two workers' methods).

---

## 4. Camps (D33 to D38)

`CampKindDef`s with their own asks, works and structures (`Camps/`), in the flavour style.

**D33. Diggers at the old place** (M)
- **On the map:** three to five diggers with picks and sieves ask leave to dig at old ruins on the colony's land, or at an
  old grave that isn't the colony's. Asks: dig for a share (they dig by day where they said; on their last day one brings
  the colony's share: an old worn thing, an inert relic, now and then something sealed as the old powers give); the
  ruins but not the graves (they keep to the ruins, though one night one of them may be seen at the graves with a spade:
  a quarrel, or a brawl if caught); send them away. Tamiu's believers are uneasy while they dig near the dead.
- **Ties:** `Camps/CampKindDef.cs`, `Camps/CampBuildMath.cs`, `OwnMurmur.OldThing`, `OldPowers/OldPowersFinds.cs`,
  `Harm/CampBrawls.cs`, belief.
- **Every age:** grave robbers with picks (tribal, medieval); archaeologists with a permit and a sieve table
  (industrial); relic hunters with scanners (spacer).
- **Harm:** a brawl if one is caught at the graves, at the camps' brawl rate; shown.
- **Mods:** Real Ruins' sites; Vanilla Ideology Expanded - Relics and Artifacts' `VIE_RelicInertBone`,
  `VIE_RelicInertScroll`, `VIE_RelicInertMedallion` (all active).
- **Text:** camp flavour style.
- Not a duplicate of the salvagers (scrap), the prospectors (ore) or the scholars at the old place (measuring it, not
  digging for gain).

**D34. A herd kept apart** (M)
- **On the map:** while a sickness in the herds runs in the region (`Council_WS_Murrain`), herders whose beasts are
  coughing ask to camp at the farthest corner of the land. Asks: stay, kept apart (their herd penned at their camp, never
  on the colony's grazing); stay, and the colony's best with animals tends their beasts (animals practice, their
  goodwill, a closer brush with the sickness); turn them away. If their beasts and the colony's meet, the game's own animal
  flu comes to the colony's herd.
- **Ties:** `Council_WS_Murrain`, the camps' herds (B19), the game's `Disease_AnimalFlu`.
- **Every age:** goats and muffalo, horses, a cattle truck's load, a spacer colony's own herd.
- **Harm:** to the colony's animals only, at the game's incident's own rate, and only if the herds met; no person.
- **Mods:** the herds from Alpha Animals and Vanilla Animals Expanded, as B19 reads them (active).
- **Text:** camp flavour style.
- Not a duplicate of the herders by the water (a drought) or the calving camp.

**D35. The stone-raisers** (M)
- **On the map:** a people at peace that holds a god asks leave to raise that god's stone at a place on the colony's land
  that fits it (a hill, the oldest tree, the water's edge). Asks: raise it there (over two to four days they haul stone
  and build it, a real frame rising; they keep a short rite when it stands, and leave it); at the edge of the land only;
  no. The stone stays: a god's place once someone of the colony dedicates it, and the book has that people's name for the
  god. If it is the rival of a god the colony holds most, those believers grumble.
- **Ties:** `Camps/CampBuildMath.cs` (camps that build), `Gods/Worship/Shrines.cs`, `Gods/Among/GodRooms.cs`,
  `GodLore.Heard`, `Gods.Of`.
- **Every age:** Ideology's `TotemicSlab_Medium` (tribal); Alpha Memes' `AM_Megalith` or Vanilla Furniture Expanded -
  Props and Decor's `VFEPD_Megalith` (medieval); Vanilla Furniture Expanded - Art's `VFE_Obelisk` (industrial);
  Alpha Memes' `AM_FuturisticObelisk` or `VFEPD_FuturisticObelisk` (spacer); the game's sculpture otherwise.
- **Harm:** none.
- **Mods:** as above (active).
- **Text:** camp flavour style; their rite's words old voice.
- Not a duplicate of the stonecutters (stone for sale) or the pilgrims at a holy place.

**D36. Smugglers by night** (M)
- **On the map:** while a ban or a shortage runs in the region (a ban on drink, markets closed, iron, medicine, cloth or
  timber gone scarce), a small camp sits at the far edge, sleeps by day and sells by one lamp at night. Asks: buy openly
  (a colonist walks out after dark with silver and comes back with goods); say nothing (the Open Cup's believers and anyone
  with a craving may walk out at night on their own to buy, a little silver gone); tell the people whose law it is (their
  goodwill up; the smugglers leave in the night and don't stop here again).
- **Ties:** `Rimfolk_WS_DryLaw`, `Rimfolk_WS_Embargo` and the scarcities (the camp's `situationKinds`), `RevelGoods`
  (what they sell), the drug policies.
- **Every age:** night traders (tribal, medieval), bootleggers (industrial), contraband runners (spacer).
- **Harm:** none.
- **Mods:** their goods read from every drink and drug loaded, as Revel's are (Drugs+, Vanilla Brewing Expanded; active).
- **Text:** camp flavour style.
- Not a duplicate of the traders or the merchants from afar.

**D37. Novices on the road** (M)
- **On the map:** three or four young people of a faith, walking between holy places, stop two nights. Asks: a tale of
  the colony's gods (someone who holds a god walks over and tells a piece; the novices tell one back by their people's
  names, and the book hears them; when theirs matches a chapter the colony keeps under another name, the keeper works out
  that the two names are one); to see the colony's god's place (they kneel there); food for the road. The colony's young
  may sit at their fire.
- **Ties:** `GodTales.Tell`, `GodLore.Heard` and the names worked out, `Shrines.cs`, D6.
- **Every age:** seekers with painted faces (tribal), novices (medieval), seminarians with satchels (industrial),
  acolytes of a ship's chapel (spacer).
- **Harm:** none. **Mods:** none needed.
- **Text:** camp flavour style; the tales old voice.
- Not a duplicate of the procession of the faithful, the penitents or the pilgrims at a holy place.

**D38. A lord's hunt** (M)
- **On the map:** a noble of a people near (the Empire, or a medieval people) comes with two or three hunters and their
  dogs and asks leave to hunt the land for some days; they hunt by day and roast at night. They name one beast: the land's
  rarest (a thrumbo, a royal tiger), and, if one is on the land, a beast of a god's sacred kind or the white beast. Asks:
  hunt anything (a gift of silver); hunt, but not that one (they keep to it, though at the camp's tension one may not);
  no. If they kill a god's beast, it is the worst thing, as it is now.
- **Ties:** `CampKindDef` (`huntsPerDay`, `armedHunters`), `Gods/SacredKinds.cs`, `GodBeasts`,
  `Rimfolk_HarmedSacredBeast`. The Empire keeps its own titles and honour; Rimfolk adds none.
- **Every age:** a chief's hunt (tribal), a lord's hunt (medieval), a trophy party (industrial), offworld trophy hunters
  (spacer).
- **Harm:** the hunt's own on their hunters (`HuntsBite`); a colonist only if they ride with them.
- **Mods:** Vanilla Animals Expanded - Royal Animals' `VAERoy_Pheasant` and `VAERoy_RoyalTiger` (active; Rimfolk uses only its
  crane so far, as a sacred kind); the game's `Thrumbo`, `Husky` and `LabradorRetriever`. Dark Ages: Beasts and Monsters (subscribed, not
  enabled) would give `DA_ImperialRedhound` for their dogs.
- **Text:** camp flavour style.
- Not a duplicate of the hunting party (hunters after a herd, no noble, no beast named) or a lord's progress (a world
  event).

---

## 5. World events (D39 to D44)

`WorldSituationDef`s with a `WorldSituationWorker` (`World/WorldSituations.cs`), each people's age as a tag (as 0.282.0's
do), a start and ending in the peoples' own ways where one is preferred (B1), in the flavour style.

**D39. A god put away** (M)
- **In the region:** a people that holds several gods puts one away after a hard year (a famine, a plague, a lost war):
  its towns take down that god's stones and its priests walk the roads. The letter tells it in that people's voice. At
  the gate one of its priests asks shelter. Answers: take them in for a season (a guest who keeps that god's rite at the
  colony's place of it and tells its tales by their people's name); a meal and send them on; turn them away. The colony's
  believers of that god are uneasy, and the book keeps "so their letter said". The ending says whether the people took
  the god back, by what befell them next. Their standing with the god moves out of sight (`GodPeoples`).
- **Ties:** `Gods/World/GodPeoples.cs`, `GodLetters.cs`, `Gods.Of`, B32 (the region's events in the book), `GodTales`,
  the procession and penitents' camps (`situationKinds`).
- **Every age:** a tribe puts a spirit out of the fire circle; a medieval people pulls down a chapel; an industrial people
  shuts a church; a spacer people locks an archive.
- **Harm:** none. **Mods:** none needed.
- **Text:** flavour style; the priest's words old voice.
- Not a duplicate of a war of words (`Council_WS_HolyWar`) or a holy year.

**D40. The dancing sickness** (M)
- **In the region:** in one town of a people at peace, people dance in the streets and can't stop; the region says it is
  a curse, or the Open Cup's doing, or bad bread. Two or three of that town arrive at the gate dancing, dance an hour in
  the yard (to the colony's instrument where it has one), then ask water and rest. Answers: rest indoors (they sleep and
  wake well; one of the colony who danced with them may catch it for a day); fed at the gate and sent on; sent on. Hush's
  believers ask for quiet, Revel's join in.
- **Ties:** a visitors' play-out (`PlayOut/PlayGuests.cs`), the illness budget (a day's "dancing fever": restless, no
  sleep), `HushNoise`'s lines, Revel's believers.
- **Every age:** a dancing plague (tribal, medieval), a mania in the mill town (industrial), a psychic echo that sets a
  station dancing (spacer).
- **Harm:** the day's fever on a colonist who joined them, through the shared illness budget, shown.
- **Mods:** Royalty's `Harp`, Ideology's `Drum`; P-Music (subscribed, not enabled) for the music.
- **Text:** flavour style.

**D41. The restless dead** (M)
- **In the region:** graves found open in a people's towns (with Anomaly, the dead walk there); the peoples burn their
  dead, and sextons and priests argue. The world event's own question asks what to do with the colony's dead: leave them
  as they lie; burn them (the graves taken up and the dead cremated by the game's own jobs); or seal them (sarcophagi where
  the colony can build them). Tamiu's believers want them kept whole, Morel's want the soil, doubters say burn.
- **Ties:** `GraveGods.cs`, Tamiu's wrapped dead, the old powers' dead on the road, Anomaly's shamblers
  (`ShamblerSwarm`), the game's burying and cremating.
- **Every age:** the same; the words by age.
- **Harm:** left as they lie, with Anomaly and harm on, a shambler may rise from the colony's oldest grave at the shared
  budget's roll and a small share of the threat; shown.
- **Mods:** none needed; Dark Ages: Crypts and Tombs (subscribed, not enabled) for coffins and candles.
- **Text:** flavour style.
- Not a duplicate of the great burying (a flood opens a battlefield) or the dead on the road (the old powers).

**D42. Bad coin** (S to M)
- **In the region:** one people's silver is found mixed with tin; traders across the region weigh every coin. That
  people's traders carry no silver while it lasts (they pay in goods) and take the colony's silver a little lower, and
  their towns' prices shift (B5). At the gate a money-changer offers to weigh the colony's silver for a fee. Answers: weigh
  it (a little found bad and carried out); refuse; trade only goods with that people. The Ducat's believers say the gold
  goes to the honest.
- **Ties:** the region's prices (B5, `RegionPricesMath`), the traders' stock and prices, `DucatDebts`.
- **Every age:** clipped coin (tribal, medieval), a falling currency (industrial), a credit crash (spacer).
- **Harm:** none. **Mods:** none needed.
- **Text:** flavour style.
- Not a duplicate of a merchant house failing or markets closed.

**D43. A rising in the town** (M)
- **In the region:** in a town of a people that keeps slaves or serfs, the bound rise. Freed ones come down the road and
  one reaches the gate. Answers: take them in (they join); a meal and send them on; hand them back to their masters (that
  people's goodwill up; the colony's merciful think less of the council). If the colony keeps slaves of its own, visitors
  bring the news, and the game's own slave rebellion is a little likelier while it lasts (the letter says they talk of
  it). The ending: the town freed (its leader replaced by the game's own) or the rising put down.
- **Ties:** the game's slavery, `PeopleTies`, `World/FactionMemory.cs`, the colony's ways (mercy).
- **Every age:** a tribe's captives, serfs, strikers in the mill town, indentured crew.
- **Harm:** the game's own slave rebellion at its own rate, a little likelier; shown.
- **Mods:** none needed.
- **Text:** flavour style.
- Not a duplicate of the union's strike (a people's event) or a pardon proclaimed.

**D44. Two claims on the valley** (M)
- **In the region:** two peoples at peace with the colony each claim the land the colony stands on, and each sends someone
  to the gate with its claim. Answers: side with one (its goodwill up, the other's down; later the chosen may offer
  fealty); say the land is the colony's own (both a little cooler; the stronger walks a party along the land's edge one
  day, seen on the map, not fighting); pay each a small toll (silver carried out). The ending: settled at a moot
  (`Rimfolk_WS_Moot`) or by the stronger.
- **Ties:** fealty (`Rimfolk_DlmFealty_Swear`), the moot, goodwill, a walk along the edge (`PlayOut/PlayRoutes.cs`).
- **Every age:** a chief's word (tribal), a charter (medieval), a survey and a deed (industrial), an orbital registry
  (spacer).
- **Harm:** none.
- **Mods:** none; Faction Territories and Vassalage (active) is left alone.
- **Text:** flavour style.
- Not a duplicate of a contested seat (two claimants to one people's head).

---

## The lead's recommended first round (twelve)

Chosen for the player's words (the gods first, the relations between people, then the other kinds):

- **D1** the dare, **D9** the offering nobody's god took, **D14** suspected of a bargain: the people may be wrong, and the
  book shows it.
- **D3** the believers take sides, **D4** the guest at the burying, **D10** the book read at the fire.
- **D16** not in by dark, **D17** the fish are running: ordinary map events in every age.
- **D25** how to lay them down, **D30** the work my god won't have: council questions of faith.
- **D35** the stone-raisers (a camp) and **D39** a god put away (a world event).

The rest by kind, in the rounds HANDOFF.md describes (three builds at a time): gods 1 (D1 to D8), gods 2 (D9 to D15),
storyteller events (D16 to D23), council questions (D24 to D32), camps and world events (D33 to D44).

Saved state: D6 (the names coined) and D18 (the walled den until spring) add saved collections, each needing a model in
`Tools/save_estimate.py`; the Between items keep their state in `BetweenState`, the camps in their camp, the world events
in their flags.

## Mods that would help

From `docs/player-mods/mods.tsv`, subscribed but not enabled. The Workshop pages were read on 20 November 2026; every page
read from here shows the line "removed from the community ... incompatible with RimWorld", which is how the pages read
from this machine (the first list found the same for P-Music, which is kept up), so look at each in Steam.

| Mod | Workshop | Versions | Kept up | For |
|---|---|---|---|---|
| P-Music | [725130005](https://steamcommunity.com/sharedfiles/filedetails/?id=725130005) | 1.0 to 1.7 | updated 25 October (first list) | sound: music for the wedding (D11) and the dancing (D40) |
| Epochs - Incense | [3072579620](https://steamcommunity.com/sharedfiles/filedetails/?id=3072579620) | 1.4 to 1.7 | updated 15 November ("Mod updated to 1.7") | an incense burner and tower, no research, for the reading (D10), the burying (D25) and the stone's rite (D35); needs Vanilla Expanded Framework (active) |
| Dark Ages: Crypts and Tombs | [2963826335](https://steamcommunity.com/sharedfiles/filedetails/?id=2963826335) | 1.4 to 1.6, no 1.7 tag | updated 16 November; the author's answer on 1.7 is unclear | graphics: `DA_Coffin`, `DA_BurialUrn`, `DA_Candles`, a wall tomb, for D25, D33 and D41 |
| Dark Ages: Beasts and Monsters | [3472275628](https://steamcommunity.com/sharedfiles/filedetails/?id=3472275628) | 1.5 to 1.7 | updated 10 November | graphics: `DA_ImperialRedhound` for the lord's hounds (D38); it also brings trolls and other creatures with dangers of their own |

Looked at and not recommended:
- **Atmosphere Overhaul** (dropped by the player, 28 November 2026) (3830470974): a simulation of air pressure, wind, humidity and fronts, not a look for fog or
  sky; it would change the weather these events read.
- **Simple FX: Splashes** (3261109179): rain splashes on hard ground only, no 1.7 tag, last updated 19 June 2025.
- **Gerrymon's Greekcore Style** (3666883698): classical clothes, a couch, a fence and a floor; it has no columns or
  statues, so the first list's note that it would serve the shrine rooms was mistaken.

Already enabled and used by these items: Odyssey's fishing and fogs, Vanilla Fishing Expanded, Seasonal Weather
(Continued), ReGrowth 2, Vanilla Events Expanded, Vanilla Animals Expanded and its Royal Animals, Sanguinary Animals
Reborn, Alpha Memes, Vanilla Furniture Expanded (Props and Decor, Art, Factory), Vanilla Factions Expanded - Classical,
Vanilla Ideology Expanded - Relics and Artifacts, Real Ruins, Alpha Mechs, Vanilla Vehicles Expanded, Giddy-Up 2, Drugs+,
Vanilla Brewing Expanded.

No mod in the list gives a bell or horn sound for the search at dusk (D16) or the sound of a crowd dancing (D40).
