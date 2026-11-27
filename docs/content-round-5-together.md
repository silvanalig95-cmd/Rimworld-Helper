# Content round 5: the colony's people together

The player's go of 27 November 2026 ("more content to the already existing mechanics ... adding more mechanics for the
time being doesn't necessarily make it better"), and after three rounds: "continue". Twenty pieces of life between the
colony's people, all inside the frames there are: eight social events (`SocialEventDef`), six shared memories
(`SharedMemoryDef`, with their talk) and six quirks (`QuirkDef`, with their journal lines, their dreams and a word over
the head when they fit what someone is doing). Built in `Defs/SocialEventDefs/Rimfolk_Round5_SocialEvents.xml`,
`Defs/SharedMemoryDefs/Rimfolk_Round5_Memories.xml`, `Defs/QuirkDefs/Rimfolk_Round5_Quirks.xml`, the moods in
`Defs/ThoughtDefs/Rimfolk_Round5_Thoughts.xml`, the dreams in `Defs/DreamDefs/Rimfolk_Round5_Dreams.xml`, the memories'
talk in `Defs/FlavorPoolDefs/Rimfolk_Round5_Talk_Memories.xml`, the quirks' words over the head in
`Defs/FlavorPoolDefs/Rimfolk_Round5_Bubbles.xml`, two small knacks in `Defs/HediffDefs/Rimfolk_Round5_Knacks.xml`, code in
`Source/ColonyCouncil/Social/Round5Together.cs` (classes `R5T*` and `Round5Together`), pure rules in
`Source/ColonyCouncil/Social/Round5TogetherMath.cs` and the test `Tools/Tests/Round5TogetherTests.cs`.

What every piece keeps to:
- **The frames there are.** A social event is cast as every one is: a share of the day's draw (the day's count comes
  from the colony's size, never from the defs), at a waking hour, between two who are near each other, with its tone
  (warm, friction, mend) kept by the moments' own record. A shared memory is given by the event that makes it and is
  talked over by the talk that exists (open, reply, close, in every voice). A quirk is picked up after a memory, counts
  toward the two a colonist holds at most, fades in its time, and writes its journal lines as every quirk does. No new
  system, window, setting, saved collection or `Sched` offset.
- **One small hook for three of the events.** Like the council's seasons (`council`, `CouncilSeasons.Fits`/`Fill`), a
  social event may name a scene of this round (`scene`): `oldhands` (both have lived at the colony a year or more, by the
  game's own record of time as a colonist), `game` (both are playing at the same board, table, pin or ring: a building of
  the game's or a mod's whose pastime is a game; `{THING}` names it) and `childgrief` (the child lost someone of the colony
  in the last five days, by the game's own memory of the death; `{X_nameDef}` names them). Nothing is kept: the scene is
  read from the map when the event is cast.
- **Read from the map:** the season by the map's calendar, the hour, the weather and the cold (the moment's tags), what
  each of the two is doing (at a meal, at their free time, at the same work side by side), indoors or out, newcomers by
  the game's own record (their first ten days), elders and children by age, ties and how the two feel about each other,
  the game's memories of a death.
- **On the map.** The event happens where the two are, doing what the words say: at the same work, at the same game, at
  the table, out in the summer evening. It is told as a message and in the social log (where Interaction Bubbles picks it
  up). A quirk shows now and then as a word over the head when it fits what they are doing (at a game, at a meal with
  someone, near a newcomer, near a child, near one of the old).
- **Text:** plain, neutral words for the messages, moods, memories, dreams, journal lines and talk (CLAUDE.md, "Plain,
  neutral text"; none of these is one of the eight parts that keep the flavour style). Spoken lines one sentence.
- **Every age:** each event has lines for any age and lines tagged `[tribal]`, `[medieval]`, `[industrial]`, `[spacer]`,
  `[lowtech]` or `[!lowtech]`, told only in that age by the colony's own tech; what is worked, played or eaten is the
  colony's own (its fields, its benches, its games), so it takes the form of the age by itself. Quirks have a line of
  their own for a tribe or a spacer colony where the habit looks different there.
- **The full mod pool:** nothing is picked by a fixed name. A game is any building whose pastime the game calls a game
  (the chess table, horseshoes, the billiards table, a mod's darts board or wargaming table); the work is whatever the two
  are at; the dead are the colony's own.
- **The gods are unseen:** no god is named and no line says a god did anything. A child's question about the dead is
  answered with what the grown-up believes, as their people tell it, as a belief.
- **The colony is the family:** nothing reads kin. The old one and the child, the two old hands, the newcomer and the one
  beside them are chosen by age, time at the colony, what they are doing and how they feel about each other, never by
  blood.
- **Harm:** none of the twenty hurts anyone. One event is a friction (a game that got heated); it leaves a point of
  mood and a few of opinion, as the other sore moments do, and a joke between the two.

---

## Social events

**1. The spring sowing** (`Rimfolk_R5T_Soc_SpringSowing`; the seasons, work)
- **When:** spring by the map's calendar, two colonists who don't dislike each other (opinion 0 or more both ways) at the
  field work side by side (both at Growing, near each other).
- **On the map:** the two in the field together; the message names what they talked about while they worked.
- **Effects:** a small good memory each (a few points of opinion, belonging instead of mood); the shared memory of the
  spring sowing (9); friendship a little stronger.
- **Ages:** a tribe works the ground with sticks and the seed from the gathering baskets; a medieval colony sows by hand
  along the rows; an industrial or spacer colony talks over what the seed stock will give and where the new rows go.
- **Not a duplicate of:** the last of the harvest (autumn, bringing it in), good company at the work (friends at any
  work), a race at the work (rivals), a neighbour's harvest (a camp's field, a memory).

**2. A summer evening** (`Rimfolk_R5T_Soc_SummerEvening`; the seasons, friendship, leisure)
- **When:** summer, evening, no rain; two friends (a friendship tie) both at their free time under the open sky, near
  each other.
- **On the map:** the two outside in their free time as the day ends; the message.
- **Effects:** a good memory each (opinion, a little recreation instead of mood); the shared memory of the summer
  evening (10); the friendship stronger.
- **Ages:** the insects and the last light in any age; a tribe stays out late in the warm, a medieval colony sits on the
  bench by the wall, an industrial colony talks until the lamps come on, a spacer colony counts the lights of the ships
  crossing the sky.
- **Not a duplicate of:** a long talk (friends, any time or place), the stars named (a stargazer's quirk), a drink in the
  heat (a hot day at work), caught in the rain, stamping in the cold.

**3. Two old hands in winter** (`Rimfolk_R5T_Soc_WinterOldHands`; the seasons, old hands, leisure)
- **When:** winter, evening or night, both indoors, one of them at their free time, and both have lived at the colony a
  year or more (`scene` `oldhands`).
- **On the map:** the two indoors on a winter evening; the message tells what they remembered of the colony's first
  days.
- **Effects:** a good memory each (opinion, belonging); the shared memory of the winter evenings (11); friendship a
  little stronger. The memory can give the quirk "talks about the early days" (15).
- **Ages:** a tribe remembers the first shelters, a medieval colony the hall before it had a roof, an industrial colony
  the first generator, a spacer colony the drop pods and the first night after the landing.
- **Not a duplicate of:** two old-timers (two people of 55 or more, about aches and the old days before the colony),
  stories of the old days (an elder to a young one), the founding (a memory given when a new colony stands), the year
  read aloud (a gathering).

**4. The newcomer's way** (`Rimfolk_R5T_Soc_NewcomersWay`; newcomers and old hands, work)
- **When:** a newcomer (in their first ten days at the colony) and someone who has been here longer at the same
  hands-on work side by side (the field, the trees, the dig, the build, the stove, the bench, the animals).
- **On the map:** the two at the work; the message tells how the newcomer showed the other a way of doing it from where
  they came from.
- **Effects:** the one who has been here longer learns a little of the work's skill; the newcomer feels listened to (a
  good memory: opinion, belonging instead of mood), which can give the quirk "looks out for newcomers" (16); friendship
  a little stronger.
- **Ages:** the newcomer's way comes from their old band, their home town, the works they came from or the ship they
  served on.
- **Not a duplicate of:** shown the ropes (the old hand tells the newcomer how things are here), a lesson and the teacher
  learns (a mentorship), good company at the work (friends).

**5. A game that got heated** (`Rimfolk_R5T_Soc_HeatedGame`; rivalry, leisure)
- **When:** the two are playing at the same game (`scene` `game`: the same board, table, pin or ring), and they are not
  fond of each other (opinion from -40 to 30 both ways).
- **On the map:** both at the game; the message names the game ({THING}) and what the quarrel was over (a throw, a piece
  moved, the score).
- **Effects:** friction: a point of mood and a few of opinion each, a rivalry a little stronger; and a joke between them
  they will bring up later (the shared memory of the game, 12). It can give the quirk "plays to win" (17).
- **Ages:** the game itself is the colony's own; lines for a tribe (a throw called foul), a medieval colony (a piece
  said to have moved) and later ages (the score kept on paper and argued over).
- **Not a duplicate of:** a contest between rivals (a race at the hauling or arm-wrestling, a rivals tie, no game on the
  map), a race at the work, one-upmanship, the games group habit (a group's warm evening at the games).

**6. Made up over supper** (`Rimfolk_R5T_Soc_SupperMend`; making up)
- **When:** two who are still sore at each other after a spat (the moments' own record: tone `mend`) are both at a meal
  near each other.
- **On the map:** the two eating near each other; the message tells how it ended before the meal did.
- **Effects:** a good memory each (opinion, belonging); the existing shared memory of making up ("made up after the
  quarrel"); any rivalry eases. It can give the quirk "can't leave a quarrel standing" (18).
- **Ages:** a tribe shares the bowl, a medieval colony passes the bread, a spacer colony says sorry over the ration
  trays.
- **Not a duplicate of:** making it up after the fight (a fight's memory), a peace offering (one brings something to the
  other), a truce (rivals at work), company at a bad meal (one at a meal in a low mood).

**7. A child's question about the dead** (`Rimfolk_R5T_Soc_ChildAsksDead`; children, a loss)
- **When:** a child who lost someone of the colony in the last five days (the game's own memory of the death, naming
  them: `scene` `childgrief`) and a grown-up the child doesn't dislike, near each other.
- **On the map:** the two together; the message names the dead and says what the grown-up told the child.
- **Effects:** the child is comforted (a good memory, belonging instead of mood); the grown-up a small good memory too,
  which can give the quirk "always has time for the children" (19); the shared memory of the questions answered (14);
  friendship stronger.
- **Ages:** each age's people tell children what they believe of the dead: the ancestors, the churchyard and the stone,
  a photograph kept, the records of the ship. Said as the grown-up's belief, never as a god's doing, and no god named.
- **Not a duplicate of:** a quiet word after a loss (to a grown-up), grief carried together (a grown-up's grief), a
  child's questions (a child's curiosity about the sky and the council).

**8. Old and small hands** (`Rimfolk_R5T_Soc_OldAndSmallHands`; children and elders, work)
- **When:** one of the colony's elders (55 or more) and a child at the same work side by side (hauling, cleaning, the
  fields, cutting plants).
- **On the map:** the two at the work; the message.
- **Effects:** the child learns a little of the work's skill and is glad of the time (a good memory); the elder too,
  which can give the elder the quirk "always has time for the children" (19) and the child the quirk "follows the old
  ones about" (20); the shared memory of working beside them as a child (13).
- **Ages:** a tribe's elder shows how their own grandmother's people did it, a medieval elder hums an old work song, a
  spacer elder tells the child how it was done before machines did it.
- **Not a duplicate of:** an old hand's correction (an elder and a young adult), an old game (play), story hour (a
  storyteller's quirk), shown the animals (a keeper and a child), a child's questions (curiosity, any grown-up).

## Shared memories

Each is given by its event to the two who were there, talked over by the talk that exists (an opening, a reply and a
close in every voice, thirty lines or more of its own; `[byme]` and `[byother]` where it happened more to one of them),
and fades in its time. `{LABEL}` says when it was: the year or the season by the map's calendar, the game by its name, the
dead by theirs.

**9. The spring sowing** (`Rimfolk_R5T_Mem_SpringSowing`): "did the spring sowing of {YEAR} together". Good to remember,
fades over a few seasons. *Not a duplicate of:* a neighbour's harvest (a camp's field), built something together.

**10. A summer evening** (`Rimfolk_R5T_Mem_SummerEvening`): "sat out together on a summer evening in {YEAR}". Good,
fades in a year or two. *Not a duplicate of:* a gathering together (a party), lantern night, evenings at a camp's fire.

**11. The winter evenings** (`Rimfolk_R5T_Mem_WinterEvenings`): "talked over the colony's early days in the winter of
{YEAR}". Good, slow to fade. *Not a duplicate of:* founded a colony together, the year read aloud, a long night together
(two at work in the small hours).

**12. The game** (`Rimfolk_R5T_Mem_TheGame`, a joke): "still argue about the game at the {THING}". It gets funnier each
time it comes up, then wears out, as the other jokes do. *Not a duplicate of:* the bad stew, the explosion, the tame that
went wrong, the snowball fight (other jokes).

**13. Small hands** (`Rimfolk_R5T_Mem_SmallHands`): "{B} worked beside {A} as a child, in the {SEASON} of {YEAR}". Good,
very slow to fade; the child brings it up when grown. *Not a duplicate of:* taught and learned (a mentorship's lesson),
the lost child.

**14. The questions answered** (`Rimfolk_R5T_Mem_Answered`): "{A} answered {B}'s questions about the death of {X}".
Both good and sad to remember (mixed), slow to fade. *Not a duplicate of:* buried someone together (a funeral), a secret
confided.

## Quirks

Each is picked up after a memory the moments above leave (or one the older moments leave that fits), at a small chance,
counts toward the two a colonist holds at most, fades in its time (or takes root when lived), has its journal lines in six
voices (gained, and faded), a dream or two of its own (`[quirk:...]` lines in `Rimfolk_Round5_Dreams.xml`, three
dreams for the six, one of them a bad one), and a word over the head now and then when it fits what they are doing
(`Immersion/HabitBubbles.cs`, one line hooking the round's list in). Two have a small knack, as many habits do.

**15. Talks about the early days** (`Rimfolk_R5T_Quirk_EarlyDays`; old hands)
- **From:** two old hands' winter evening (3). **Shows:** at a meal or their free time with someone near: a word about
  how the colony was when they came. **Lasts:** a long while.
- **Not a duplicate of:** tells the old stories (tales of the old days before the colony, to children), speaks in
  proverbs, writes everything down.

**16. Looks out for newcomers** (`Rimfolk_R5T_Quirk_NewcomersFriend`; newcomers)
- **From:** having been new and made welcome: shown the ropes, the newcomer's way taken up (4), a group or a table that
  made room for them. **Shows:** a word to themselves when a newcomer is near. **Passes** to those close to them.
- **Not a duplicate of:** keeps a lamp lit for travellers (visitors and caravans), first to volunteer, remembers every
  birthday.

**17. Plays to win** (`Rimfolk_R5T_Quirk_PlaysToWin`; rivalry, leisure)
- **From:** a game that got heated (5), a race at the work, a contest lost. **Shows:** at a game.
- **Not a duplicate of:** counts everything, counts every coin twice, a battle ritual.

**18. Can't leave a quarrel standing** (`Rimfolk_R5T_Quirk_Peacemaker`; making up)
- **From:** making up: over supper (6), after a fight, a peace offering, a truce. **Shows:** at a meal with someone near.
  **Knack:** a little more pleasant to deal with (the game's social impact, a few points). **Passes.**
- **Not a duplicate of:** a worrier, laughs at the wrong moments, first to volunteer.

**19. Always has time for the children** (`Rimfolk_R5T_Quirk_TimeForChildren`; grown-ups and children)
- **From:** a child's question answered (7), working beside a child (8), an old game taught. **Shows:** a word when a
  child is near. **Passes.**
- **Not a duplicate of:** tells the old stories, a soft spot for strays, remembers every birthday.

**20. Follows the old ones about** (`Rimfolk_R5T_Quirk_FollowsTheOld`; children and elders)
- **From:** a child's time beside one of the old at work (8) or an old game taught them. **Who:** a child. **Shows:** a
  question when one of the old is near. **Knack:** learns a little quicker. **Lasts:** a season or two, a phase.
- **Not a duplicate of:** sleeps with a light burning, the children's other quirks (none follows anyone).

## New code, kept small

`Round5Together.Fits`/`Fill` for the three scenes, called where a social event is cast (two lines in
`Social/SocialEvents.cs`, beside the council's seasons), and its name checked with the def (`scene`). One effect,
`DilemmaEffect_R5TMemory`, gives a shared memory whose label says when it was ({YEAR}, {SEASON}) and what it was about
({THING}, {X_nameDef}), as the plain shared-memory effect gives one with a fixed label. `Round5Together.Bubbles` adds the
six quirks' words over the head to the habits' list. Nothing ticks, nothing is saved, nobody is hurt.
