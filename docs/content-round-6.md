# Content round 6: dreams, decrees, callers and the weather (28 November 2026)

Built on the player's go of 27 November 2026: "go ahead with the content round, maybe also other content, camps, dilemmas,
world events, just further expanding on the mechanics you already have or adding more, so to say, content to the
mechanics ... adding more mechanics for the time being doesn't necessarily make it better since it's already just a lot."
And of 28 November: the content should have "impacts on the game and it's not just flavor".

So everything here is content inside frames that exist, and every item does something the game keeps: practice, an
inspiration, goods, goodwill, research, game marked for the hunters, work done on the map, a warning of an event that then
really happens, or a wish the council can grant. Thirty items in four frames: fifteen dreams
(`Defs/DreamDefs/Rimfolk_Round6_Dreams.xml`), seven decrees (`Defs/DecreeDefs/Rimfolk_Round6_Decrees.xml`, their moods in
`Defs/ThoughtDefs/Rimfolk_Round6_Thoughts.xml`, their effects on the body in `Defs/HediffDefs/Rimfolk_Round6_Hediffs.xml`),
five callers at the gate (`Defs/IncidentDefs/Rimfolk_Round6_Callers.xml`) and three happenings of the weather on the land
(`Defs/IncidentDefs/Rimfolk_Round6_Weather.xml`). The test is `Tools/Tests/Round6Tests.cs`. No new system, window, setting,
saved collection, `Sched` offset or effect class: the only code is two pairs added to the decrees kept apart
(`DecreeMath.Apart`: evenings at the bench never with a building season or a harvest push).

## Which frames, and why

Measured on 28 November 2026 from the defs and the code (events counted before this round):

| Frame | What it holds | How often it comes | Last round | Taken? |
|---|---|---|---|---|
| Dreams (`Dreams/`) | 281 dreams; only 62 change anything beyond a mood (27 effects, 19 bonds, 15 omens, 3 wishes) | 30% a waking after four hours' sleep, at most once in 18 hours: about one a colonist every three days, three a day in a colony of ten | Round 5 added four quirk dreams, all moods; no content round added a dream that does something | Yes: the dreams that matter are a fifth of a frame that fires every day |
| Decrees (`Decrees/`) | 38 decrees | Declared at the council's word, two at a time, each resting 70% of its cooldown; petitioned for when their moment comes | Two on 20 November | Yes: every decree is data with real effects, and none were added by the content rounds |
| Callers and visitors at the gate (`Visitors/`, `Strangers/`) | 31 callers who come to do something (16 callers, 15 of the neighbours' rounds), among 278 visitor events most of which are the peoples' own; 24 nuisances, moods only; guest stories, a hook in code | The visitors' share of the storyteller's draw | The fourth round of neighbours and callers, 6 November | Yes, the callers: told from data, each answer can play out on the map. Nuisances are moods by frame |
| Storyteller events | 449 Rimfolk events; the storyteller batch 26 | The storyteller's draw | Map events in rounds 2, 3 and 4 (23, the last on 27 November) | No: three recent rounds |
| Weather happenings (`Weather/`) | 16 events read the weather record; 14 events of the land are told from data (the turning year, the hardships) | When the record and the season fit | Rounds 2 to 4 touched the weather in custom-coded events (meltwater, ice on the paths, the storm's shore, snow on the roof); none told from data since the first round | Yes, three: the land told from data is thin, and the record is asked by few |
| Seasons and calendar days (`Calendar/`) | Days written from the colony's history in code; 43 customs, which are council questions | Once a year each | Customs in round 3 | No: the days are code, the customs the council's |
| The neighbours' letters (`Letters/`) | 15 shapes, thousands of lines | Each correspondent every few weeks | The plain-words pass | No: a text frame with no slot for an effect |
| Setbacks, pressures | Moods hooked into the game's code | Often | Their own releases | No: moods only, and the player asked for effects beyond flavour |
| Touches | Flecks, sky colours and sounds | Every few seconds | | No: by design they change nothing |

Weather is the thinnest of the four taken: rounds 2 to 4 already used most of what the weather does on the map, so it has
three items, each checked against those rounds.

## The rules on every item

- **The frames there are.** Dreams are `DreamDef`s with the dream's own effects, omens and wishes. Decrees are `DecreeDef`s
  read field by field by `DecreeWork` (a hediff, practice, a share of the stores and goodwill, work set going each morning).
  Callers and the weather are Rimfolk events told from data (`HappeningWorker_Setting` with a `HappeningSetting`), answered
  with the effects there are.
- **A real effect, every item.** Every dream does more than give a mood (`DreamDef.Matters`); every decree has a hediff,
  practice, a take from the stores or daily work; every answer but silence of every caller and weather happening does
  something the game keeps (silver and goods, a lesson, a thing made, game marked, research, goodwill, recreation, a work
  party). The test checks each.
- **Where it plays out.** A caller's lesson is a lesson at the visitor's side; what is bought is set down at the gate by the
  visitor; the joiner works by the woodpile and carries the chair to the room; the guide's game is marked for the colony's
  hunters. The weather's answers are a night outside, an hour under the stars, the hunters out, a track lesson, a work party
  at the fallen logs, wood carried to the gate. A decree's morning work is the game's own work set going (repairs, fishing).
  A dream's omen is the game's own event, a day or a few later.
- **Every age.** Each caller and weather card opens in the tribal, medieval, industrial and spacer forms (or the low-tech and
  high-tech ones) and in a form for any colony; the callers come from a people of about the colony's own age. Each decree is
  declared in every age's form (its card names them: a tribe's healer and a spacer colony's medical drill, a village's cart
  and an industrial colony's truck) and asked for in every age's words. Dreams have era-tagged lines. Nothing is switched off
  for an age; the Empire keeps its own titles where the frames already give them.
- **The gods are unseen.** No item names or means a god; no line says anything was sent. An omen is a dream that comes true,
  as the older omens are.
- **The colony is the family.** Nothing reads kin, houses or lines; who is asked is chosen by skill, office, age or passion.
- **Harm.** None of the items hurts anyone by its own doing. The dry storm's omen brings the game's own flashstorm, with its
  fires (one bad omen of fifteen, rarer than the older ones). Game marked for the hunters is the game's own hunting, and a
  hunted animal may turn as it can in any hunt (named under the answer, through the shared budget, `Harm/HuntsBite.cs`).
- **No countdown** in any card, answer or line; no em dashes or exclamation marks.
- **Text.** Plain, neutral words (CLAUDE.md: dreams, decrees, event cards and messages keep to the plain rule; none of the
  eight flavour-style parts is touched). Lines said aloud (a decree's for and against) are one sentence each.
- **Mods.** Only `active` rows of `docs/player-mods/`: the arms-master's blade is any active mod's forged blade the colony's
  age has (`@Blade`, the things' role), the joiner's seat Vanilla Furniture Expanded's school chair or bench where the game's
  dining chair doesn't fit the age, the fishing season works with Odyssey's fishing or Vanilla Fishing Expanded's, the game
  marked is whatever animals the player's mods put on the land. `python3 Tools/poolcheck.py` is OK.

---

## Dreams that change something (R6-D1 to R6-D15)

Fields as in `Council_Dreams.xml`. Lines are first person, plain, a sentence or two, in all six voices (the omens in four).
An omen comes no oftener than any omen: one at a time, eight days apart, with the setting "dream omens" on.

**R6-D1. At the stove** (`Rimfolk_R6_Dream_Stove`; Strange; a passion for cooking)
- **Does:** Cooking practice (400).
- **Not a duplicate of:** the craft dream (the dreamer's own skill, any), the teacher's dream (a bond and a lesson), the
  feast dream (a wish for a party).

**R6-D2. Stitches** (`Rimfolk_R6_Dream_Stitches`; Strange; a passion for medicine; notable)
- **Does:** the game's surgery inspiration (`Inspired_Surgery`): the next operation goes better.
- **Not a duplicate of:** the sickbed dreams (moods), the spark and the shape (creativity), the fever dreams.

**R6-D3. Followed home** (`Rimfolk_R6_Dream_FollowedHome`; Sweet; a passion for animals; notable)
- **Does:** the taming inspiration (`Inspired_Taming`).
- **Not a duplicate of:** the life goal's animal dream (practice), the spirit animal's and the pet's dreams (moods).

**R6-D4. A good bargain** (`Rimfolk_R6_Dream_Bargain`; Sweet; a passion for social; notable)
- **Does:** the trade inspiration (`Inspired_Trade`): better prices with the next trader.
- **Not a duplicate of:** the omen of the trader (an event), the council's dreams (trust).

**R6-D5. The bout** (`Rimfolk_R6_Dream_Bout`; Strange; a passion for melee, able to fight)
- **Does:** Melee practice (400).
- **Not a duplicate of:** target practice (shooting), the battle won (a hediff of the fight), back to back (a bond).

**R6-D6. Talked round** (`Rimfolk_R6_Dream_TalkedRound`; Strange; the warden's seat; notable)
- **Does:** the recruitment inspiration (`Inspired_Recruitment`).
- **Not a duplicate of:** any dream of a seat (the lost seat, the council fire: trust).

**R6-D7. The work done** (`Rimfolk_R6_Dream_WorkDone`; Sweet; the industrious and hard workers in fair spirits)
- **Does:** the game's work frenzy (`Frenzy_Work`).
- **Not a duplicate of:** the hard days (well rested, lower standards), the deep sleep (rest).

**R6-D8. The deep rock** (`Rimfolk_R6_Dream_DeepRock`; Strange; a passion for mining)
- **Does:** Mining practice (400).
- **Not a duplicate of:** the mountain (steadied), the cave and seam events.

**R6-D9. A fire in my room** (`Rimfolk_R6_Dream_RoomFire`; Strange; winter)
- **Does:** the wish for a fire in their own room (`Council_Wish_Hearth`): built, they sleep warm and the council keeps its
  word.
- **Not a duplicate of:** the garden (greenery), the unburied (a memorial), the feast (a party).

**R6-D10. The gap in the wall** (`Rimfolk_R6_Dream_GapInWall`; Nightmare; within two days of an attack)
- **Does:** the wish for stronger defences (`Council_Wish_Defenses`, what the colony can build).
- **Not a duplicate of:** the battle again (shaken), the dread (a hediff), the omen of the torches (a raid).

**R6-D11. Omen: the open gate** (`Rimfolk_R6_Dream_Omen_StrayHerd`)
- **Does:** tame animals wander in a day to three later (`FarmAnimalsWanderIn`).
- **Not a duplicate of:** the omens of visitors, a stranger, the white giant (a thrumbo).

**R6-D12. Omen: the passing herd** (`Rimfolk_R6_Dream_Omen_Migration`)
- **Does:** a herd migrates across the land (`HerdMigration`): game for the hunters.
- **Not a duplicate of:** the omen of the manhunters (a threat), the white giant.

**R6-D13. Omen: the dry storm** (`Rimfolk_R6_Dream_Omen_DryStorm`; spring to autumn)
- **Does:** a flashstorm comes (`Flashstorm`): the warning lets the colony clear brush and stand by. The one bad omen of the
  round, weighted below the older omens.
- **Not a duplicate of:** the omens of the cold, the heat, the solar flare.

**R6-D14. Omen: the lights in the sky** (`Rimfolk_R6_Dream_Omen_Lights`; children too)
- **Does:** an aurora (`Aurora`), the game's lift in mood for those who see it.
- **Not a duplicate of:** the eclipse omen, the comet event.

**R6-D15. Omen: the one from the woods** (`Rimfolk_R6_Dream_Omen_WildOne`)
- **Does:** a wild person wanders in (`WildManWandersIn`), who can be tamed and taught.
- **Not a duplicate of:** the omen of a stranger who joins (a wanderer), the lost child (an event).

## Decrees (R6-K1 to R6-K7)

Each in a place of its own on the Decrees page (orders 50 to 56), with four moods (hate, dislike, like, love; -3 to +3) and
its row in the Almanac's table of decrees.

**R6-K1. First-aid lessons** (`Rimfolk_DecreeR6_FirstAid`; order and health; five days)
- **Does:** everyone who can tend practises medicine every waking hour (15 an hour); tending 5% better and 10% quicker;
  other work 4% slower.
- **Asked for:** when someone is ill or many are hurt; needed after an attack.
- **Ages:** a tribe's healer, a village's barber-surgeon, an industrial first-aid course, a spacer medical drill.
- **Not a duplicate of:** rest when ill (healing and immunity), apprenticeships (four other trades), the physician's seat.

**R6-K2. Days with the animals** (`Rimfolk_DecreeR6_Beasts`; food and stores; six days)
- **Does:** everyone who can handle animals practises every hour; taming 10% and training 15% likelier to work; milking,
  shearing and eggs 5% more; other work 5% slower.
- **Asked for:** in good times.
- **Ages:** beast-lore, breaking the young stock, a dog school, behaviour training.
- **Not a duplicate of:** the herdmaster's seat, the question of what to teach an animal, a hunting season.

**R6-K3. Evenings at the bench** (`Rimfolk_DecreeR6_Bench`; work; eight days)
- **Does:** work at the benches (smithing, tailoring, stonecutting) 20% quicker; building, mining and the fields 10% slower.
  Never with a building season or a harvest push (`DecreeMath.Apart`).
- **Asked for:** in winter.
- **Ages:** hides and baskets in a winter camp, the spindle by the fire, workshop shifts, a fabrication run.
- **Not a duplicate of:** long hours (all work), a building season (outdoors), a harvest push (the fields).

**R6-K4. Medicine for the sick** (`Rimfolk_DecreeR6_MedicineSent`; the wider world; six days)
- **Does:** each morning 1 to 3 of the colony's medicine (the plainest first) goes to the people a sickness hits hardest, 3
  goodwill a day. Only while a plague or a sickness in the herds goes round the region (`situationKinds` sickness).
- **Ages:** a runner with herb bundles, a physician's chest, a crate of supplies, medical kits.
- **Not a duplicate of:** a quarantine (keeps the colony's own well), famine bread (food in a famine), the world events' own
  answers (once).

**R6-K5. Wood for the neighbours** (`Rimfolk_DecreeR6_WoodSent`; the wider world; seven days)
- **Does:** each morning 10 to 40 wood goes to the people who need it, 2 goodwill a day. Only while a town rebuilds after a
  fire, a flood or a raid, or a hard winter lies over the region (rebuild, townraid, weather).
- **Ages:** sleds, a cart, a truck, a hauler.
- **Not a duplicate of:** alms (food), tithes (silver), wood for the winter (the colony's own woodpile).

**R6-K6. Mending days** (`Rimfolk_DecreeR6_Mending`; order and health; five days)
- **Does:** each morning two of the colony's builders mend the damaged walls, doors and furniture before other work (a work
  party of the game's own repairs).
- **Asked for:** after an attack.
- **Not a duplicate of:** a clean sweep (cleaning), the fence down (an event), the night's mending (a people's event).

**R6-K7. A fishing season** (`Rimfolk_DecreeR6_Fishing`; food and stores; six days; with Odyssey or Vanilla Fishing
Expanded)
- **Does:** each morning two of the colony's people fish at its fishing spots before other work.
- **Asked for:** when food is low, or in good times.
- **Ages:** spears and weirs, nets, rods and lines.
- **Not a duplicate of:** the fish are running (an event of the season), a hunting season (game), foraging days.

## Callers at the gate (R6-C1 to R6-C5)

Each is told from data, comes from a people of about the colony's age, has an ordinary share of the visitors' draw (0.6 to
0.7) and is not seen again for 40 to 45 days. Silence sends them on and sets nothing going; two answers or more play out
on the map; nothing gathers the colony and nobody is hurt (the rules of the fourth round of callers).

**R6-C1. An arms-master** (`Rimfolk_Evt_R6Caller_ArmsMaster`; Melee 11 to 16)
- **Answers:** a day of hand-to-hand with the colony's best fighter (a lesson at X's side, Melee 1500, silver); a day at the
  targets with its best shot (Shooting 1500, silver); a blade from the pack for the fighter (any active mod's forged blade
  the colony's age has, normal, silver), handed over by X; not today.
- **Ages:** a spear-fighter, a master-at-arms, a teacher of boxing and knife-work, a combat instructor.
- **Not a duplicate of:** the visiting master (a council question about a visitor already here), the travelling champion (a
  contest), drills and militia drills (decrees).

**R6-C2. A joiner** (`Rimfolk_Evt_R6Caller_Joiner`; Crafting 10 to 15)
- **Answers:** a seat for someone's room (silver and 25 wood; X makes it by the woodpile and carries it in: the game's
  dining chair, Vanilla Furniture Expanded's school chair or bench, or a stool, whichever the colony's age has); a day at the
  bench with the colony's crafter (Crafting 1200, half the silver); nothing this time.
- **Ages:** bent wood lashed with hide, a joiner's chisels, a cabinet-maker's plane, furniture made by hand on the rim.
- **Not a duplicate of:** the woodcarvers' camp (carvings), the instrument-makers' camp, the apprentice's first piece (a
  story's).

**R6-C3. A carter in the cold months** (`Rimfolk_Evt_R6Caller_Carter`; autumn and winter, lands with a cold season)
- **Answers:** a load of firewood (75 wood, silver); a load of hay (100, silver; greyed out with no animals); a hot meal for
  the carter (carried out, 2 goodwill); nothing today.
- **Ages:** a sled of dead wood and dried grass, a woodman's cart, a truck of logs and bales, a hauler.
- **Not a duplicate of:** fodder for a neighbour's herds (the colony gives), wood for the winter (the colony cuts), the
  brewer's cart, the drovers (animals).

**R6-C4. A master builder** (`Rimfolk_Evt_R6Caller_Builder`; Construction 11 to 16)
- **Answers:** a day on the walls with the colony's builder (Construction 1500, silver); the cut stone on the cart (60 blocks,
  silver); send them on.
- **Ages:** a builder of longhouses, a master mason, a building engineer, a construction technician.
- **Not a duplicate of:** the stonecutter (stones for the graves), hands for hire, a neighbour's bridge or wall (the colony
  builds for them).

**R6-C5. A hunter's guide** (`Rimfolk_Evt_R6Caller_Guide`; Shooting 10 to 15; leans with the beasts)
- **Answers:** a day out with the colony's best shot (Shooting 800 and Animals 500, a lesson out on the land, and two head of
  game marked for the hunters, silver); dried meat (30 pemmican, silver); send them on.
- **Ages:** a hunter following the game, a huntsman, a guide with a rifle, a tracker mapping the herds.
- **Harm:** the game marked is the game's own hunting; a hunted animal can turn, named under the answer.
- **Not a duplicate of:** a hunting season (a decree), the rut (an autumn hunt party), the wildfowlers' camp.

## The weather on the land (R6-W1 to R6-W3)

Each is told from data in the group of the land, asks the colony's weather record for the days it speaks of, has an
ordinary share (0.6 to 0.7) and is not seen again for 45 to 50 days. The default leaves things as they are.

**R6-W1. Clear nights** (`Rimfolk_Evt_R6Weather_ClearNights`; no rain or snow for four days, `dry:4`; a research project in
hand; A the best at intellectual work)
- **Answers:** A sits up with the sky and the notes (a night outside, Intellectual 600, the research in hand moved on by a
  share of what's left, 600 at most); everyone out for an hour under the stars (recreation, a little Intellectual, an hour
  together outside); early to bed.
- **Ages:** the stars that tell the seasons, a candle and a book on the wall, a telescope on the roof, satellites and wrecks
  crossing the sky.
- **Not a duplicate of:** the comet, the eclipse, white nights, the longest night (sky and season events with no research).

**R6-W2. Fresh snow** (`Rimfolk_Evt_R6Weather_FreshSnow`; snow in the last two days, `snow:1/2`; autumn and winter, cold
lands; A the best shot, B a younger grown-up)
- **Answers:** the hunters out while the tracks are fresh (two head of game marked, Shooting 300 for A); A takes B out to read
  tracks instead (a lesson on the trails, Shooting 600 and Animals 300 for B, no game today); stay in by the fire.
- **Ages:** a bow restrung, a rifle at first light, caribou tracks on the tundra.
- **Not a duplicate of:** the first snow (a snow day or the paths cleared), tracks in the snow (an encounter's mood), the
  hunter's guide (a visitor), the rut.

**R6-W3. Windfall after the storm** (`Rimfolk_Evt_R6Weather_Windfall`; a storm in the last two days, `storm:2`; lands with
rain enough for trees; a neighbouring people)
- **On the map:** when the card comes, trees by the road outside the gate are really down (`Felled`, 40 wood in logs by the
  stumps, outside the home area and the fields; with someone whose faith forbids felling, the logs lie at the gate).
- **Answers:** bring the wood in (a work party carrying the logs to the stores); send 20 to the neighbours whose roofs came
  off in the same storm (carried to the gate for their people, 3 goodwill); leave it where it fell.
- **Ages:** trunks across the track by the camp, timber by the road.
- **Not a duplicate of:** the lightning-struck tree (one tree, carving or timber), the leaning tree (a danger by the walls),
  what the storm threw up (the shore).

## New code, kept small

- `DecreeMath.Apart` holds two more pairs (evenings at the bench with a building season, and with a harvest push), and
  `DecreeMath.Bench` names the decree. Nothing else: no worker, no effect class, no saved field, no `Sched` offset.

## Mods that would help (from `docs/player-mods`, subscribed and not enabled)

None of the thirty needs a mod the player hasn't enabled: they draw on what is active (every active mod's forged blades for
the arms-master, Vanilla Furniture Expanded's school chair and bench for the joiner, Odyssey's and Vanilla Fishing Expanded's
fishing for the fishing season, every active mod's animals for the
hunts and the animals' days). Of the subscribed mods that are off:

- **Outland - Furniture & Decor** (https://steamcommunity.com/sharedfiles/filedetails/?id=2897435739; catalogued for 1.4 to 1.7, so updated for the version the player plays):
  wooden chairs, a dining chair, benches and tables in its own look; the joiner's list could name them, so a colony with it
  gets a seat that matches the rest of its furniture.
- **Medieval Total Rim: Animals** (https://steamcommunity.com/sharedfiles/filedetails/?id=3806262056; catalogued for 1.6 and 1.7): horses,
  hounds and sheep breeds (destriers, greyhounds, merinos), more animals for the days with the animals and more game for the
  guide and the fresh snow.
- **The Wild Fields - apparel and weapons** (2941661586) would add blades for the arms-master, but it is catalogued only up
  to 1.6, not the 1.7 the player plays; and Medieval Overhaul (3219596926) likewise stops at 1.6.
