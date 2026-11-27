# Content round 4: the colonists' own lives

The player's go of 27 November 2026 ("more content to the already existing mechanics ... adding more mechanics for the
time being doesn't necessarily make it better"), and after two rounds: "continue". Twenty-four pieces of the colonists'
own lives, all inside the frames there are: eight personal stories (`ArcDef`), eight wishes (`WishDef`), four life goals
(`AspirationDef`) and four ambitions (`AmbitionDef`). Built in `Defs/ArcDefs/Rimfolk_Round4_Arcs.xml`,
`Defs/WishDefs/Rimfolk_Round4_Wishes.xml`, `Defs/AspirationDefs/Rimfolk_Round4_Aspirations.xml`,
`Defs/AmbitionDefs/Rimfolk_Round4_Ambitions.xml`, their moods in `Defs/ThoughtDefs/Rimfolk_Round4_Lives_Thoughts.xml`,
words in `Languages/English/Keyed/Rimfolk_Round4_Lives.xml` (`CC_R4L_*`), code in
`Source/ColonyCouncil/Arcs/Round4Lives.cs` (classes `R4L*`), pure rules in `Source/ColonyCouncil/Arcs/Round4LivesMath.cs`
and the test `Tools/Tests/Round4LivesTests.cs`.

What every piece keeps to:
- **The frames there are.** Stories move on by the gates the tracker has (a talk, time together, work done, a skill, a
  record, a custom gate the worker answers); wishes are asked, promised, granted or let lapse as every wish is; life goals
  count a record or a skill; ambitions are pitched at the meeting and reached by their goals. No new system, window,
  setting, saved collection or `Sched` offset: what a story or a wish keeps, it keeps in the fields they already save
  (a story's `key` and `tag`, a wish's `subjectKey`, `baseline` and `targetValue`).
- **Triggers read what the game and Rimfolk already know:** work settings and skills, the game's records, seasons and the
  map's own climate, snow, crops, graves, books, eyesight, visitors on the map and their people, memories of talks,
  Rimfolk's ties, moments, contests and rescues, the colony's gods as each colonist holds them, its shrines, its world
  events.
- **On the map.** A promise sets something going that the player sees: a bill at a bench, crops marked for harvest, a
  visit, a gathering out in the snow with a snowman, a walk to a grave or a shrine. Stories end with a toast, a visit, an
  evening at the fire or thanks at a shrine where the words say so. What the words say is what happens; no countdowns.
- **Text:** the flavour style of these four parts (CLAUDE.md, the player's 20 November 2026 rule), still true to the map;
  messages and the reasons on a wish in plain words; spoken bubble lines one sentence.
- **Every age:** a story's lines are written so they fit a tribe and a spacer colony alike; wishes, goals and ambitions
  carry `[tribal]`, `[lowtech]` and `[spacer]` lines where an age sees it differently. What is made, grown, read or
  played with is the colony's own (its benches, crops, books, instruments), so it takes the form of the age by itself.
- **The full mod pool:** nothing is picked by a fixed name. A recipe at any of the colony's benches, any crop in its
  fields, any book, any people that visits, any god the colonist holds: the player's active mods count with the game's.
- **The gods are unseen:** two pieces touch the gods (7 and 16), each only as the colonist believes: they name the god as
  they call it, and no line says the god did anything.
- **The colony is the family:** nothing reads kin. The old one and the child, the friend at the bench, the friend not
  seen lately: chosen by what they are to each other (opinion, ties, work), never by blood.
- **Harm:** none of the twenty-four hurts anyone.

---

## Personal stories

**1. A trade of their own** (`Rimfolk_R4L_Arc_TradeOfTheirOwn`; work and crafts)
- **When:** someone grown does a kind of work for the colony that has fallen to them: the work is on for them, they are
  the best at it among those who do it (or the only one), it is no passion of theirs and they are still poor at it
  (level 6 or below).
- **Asks of them:** to keep at it. Days of the work, a talk about it with someone who thinks well of them, two levels
  gained.
- **On the map:** the work itself; the talk is a social one, in the log.
- **Ends:** "it's mine now": a good memory, and sometimes (a quarter of the time) the game's own interest in it, told as
  a message. Fizzles if they stop doing it for a long time ([timeout]).
- **Not a duplicate of:** the apprentice's masterwork (a keen crafter with a master), passing it on (an elder's craft),
  a calling (a child), not a child (a young one proving themselves at work they love), back to it (after a hurt).

**2. The wager** (`Rimfolk_R4L_Arc_Wager`; friendly rivalry)
- **When:** two grown colonists who get on (opinion 10 or more both ways, not rivals) both do the same kind of work and
  both are good at it (5 or more): the fields, the dig, building, the kitchen, the bench or the hunt.
- **Asks of them:** a wager made in a talk, then six days of work; the one who brings in more (harvests, rock dug,
  things built, meals cooked, things made, game brought down: the game's own records) wins.
- **On the map:** the work; at the end the loser stands the winner a drink: a toast in the evening, the loser raising it.
- **Ends:** [won], [lost] or [even], each with its own lines; the two a little closer either way. A fight between them
  ends it ([fight]).
- **Not a duplicate of:** grudging respect (rivals at odds who come to respect each other), two students, the suitors,
  the last two of us.

**3. The old one's tales** (`Rimfolk_R4L_Arc_OldTales`; children and elders)
- **When:** one of the colony's elders (55 or more) and a child at home (4 or older) who like each other (10 or more both
  ways).
- **Asks of them:** a tale told (a talk), time together (eight hours near each other), and the child asking for the tale
  to be told to everyone.
- **On the map:** the talks; the hours side by side; at the end an evening by the fire with everyone, the elder telling
  it.
- **Ends:** an evening of tales; both glad of it, and closer. The elder's death ends it, told by the child.
- **Not a duplicate of:** passing it on (a craft taught), a calling (a child taken with a grown-up's skill), bad nights,
  the last two of us, one more spring.

**4. The first winter** (`Rimfolk_R4L_Arc_FirstWinter`; the land and the seasons)
- **When:** autumn on land with a real winter (its coldest month below freezing), and someone who came to the colony since
  the last one, so has never wintered here.
- **Asks of them:** to see it through: the first frosts, a talk with someone who has wintered here, the deep of it, the
  thaw.
- **On the map:** the season itself (the stages wait for winter and for spring by the map's own calendar); the talk.
- **Ends:** "a winter through": a good memory and a little closer to the one who told them how. Fizzles if winter never
  comes or spring is very late ([timeout]).
- **Every age:** the same in all; the lines speak of cold, snow and the fire, not of heaters or furs.
- **Not a duplicate of:** finding home (a newcomer's belonging), the long way home (a journey), a companion.

**5. Work friends** (`Rimfolk_R4L_Arc_WorkFriends`; friendship)
- **When:** two grown colonists who love the same kind of work (a passion each), both doing it, who hardly know each other
  yet (opinion 0 to 25 both ways, no friendship tie).
- **Asks of them:** work at it, a talk about the work, time together (six hours near each other).
- **On the map:** the work; the talk; the hours side by side; at the end the two sit down together of an evening (a
  visit, the two at a table).
- **Ends:** friends: the tie grows, and both are glad of it. A fight ends it ([fight]).
- **Not a duplicate of:** finding home, grudging respect, between, old wounds, two students.

**6. Friends over the hills** (`Rimfolk_R4L_Arc_OverTheHills`; the colony's neighbours)
- **When:** someone grown got on well with a visitor of another people (the game's own memory of a talk with them in the
  last few days, opinion 20 or more), the people at peace with the colony, and the visitors have gone home.
- **Asks of them:** to wait for that people to come again, and then an hour near them.
- **On the map:** the waiting is the colony's own life; then the people's visitors, traders or a caravan on the map, and
  the colonist among them.
- **Ends:** "they came back": if it was the same person, [friend] lines; a good memory. Fizzles if the people never come
  ([timeout]) or turn hostile ([fight]).
- **Not a duplicate of:** the road calls (a camp on the colony's land), word from far away (kin out in hard times), the
  long way home.

**7. A promise in the dark** (`Rimfolk_R4L_Arc_Promise`; faith, the gods unseen)
- **When:** someone who holds to a god (as they believe it), with a shrine of it at home, was down and helpless in the
  last ten days and is up again.
- **Asks of them:** to tell someone of the promise they made while they lay there, to keep it with their hands (work done
  at what they do best), and to give thanks at the shrine.
- **On the map:** the talk; the work; at the end they walk to the shrine and stand there a while.
- **Ends:** "the promise kept". The god is named as they call it ({NAME}); no line says the god answered or saved them.
  Fizzles if they never get to it ([timeout]).
- **Not a duplicate of:** a prayer for a friend (praying for someone else who is ill), a fear faced, back to it.

**8. At the healer's side** (`Rimfolk_R4L_Arc_HealersSide`; work and crafts, healing; the apprentice's own worker)
- **When:** someone keen on doctoring (a passion), still learning (3 to 10), with a doctor at home much better at it (10
  or more).
- **Asks of them:** to learn at the doctor's side (a mentorship) until they have come on two levels, to tend eight
  patients themselves, and to go over a patient with the doctor (a talk).
- **On the map:** the game's own tending; the mentorship; the talk.
- **Ends:** the doctor's word that they can be trusted with the sick; good memories both ways; sometimes a deeper passion.
- **Not a duplicate of:** the apprentice's masterwork (crafting, art and building, ending in a piece made), passing it on
  (from the elder's side), a calling (a child), two students.

## Wishes

**9. Something new at the bench** (`Rimfolk_R4L_Wish_NewMake`; work and crafts)
- **When:** a crafter or a cook (a passion for it) could make something at one of the colony's benches that the colony
  has none of: the recipe known, their skill enough for it, the stuff for it in store.
- **Asks:** to try making it. **Promised:** a bill for one at that bench, for them if the bench allows it.
- **On the map:** the bench's bill, and the thing made. **Granted:** when one exists and they have made something since.
- **Every age and mods:** whatever the colony's benches can make, the game's or a mod's.
- **Not a duplicate of:** better food (fine meals stocked), a better weapon, new clothes, art where people gather,
  a place for their passion, lessons, a trader's goods.

**10. An evening with an old friend** (`Rimfolk_R4L_Wish_OldFriend`; friendship)
- **When:** two friends (a friendship tie) who haven't had a warm moment together in four days, both at home.
- **Asks:** an evening together. **Promised:** the one who asked walks over in the evening and the two sit down
  together for a couple of hours (a visit; with answers not played out, the next time they meet).
- **Granted:** when the evening happened.
- **Not a duplicate of:** a day off for a friend (a worn-out friend rested), a visit at the sickbed, a party.

**11. Someone to read to me** (`Rimfolk_R4L_Wish_ReadToMe`; elders)
- **When:** one of the old (60 or more) whose eyes have gone poor (the game's sight 75% or less), with books in the
  colony and someone who thinks well of them who can read.
- **Asks:** to be read to. **Promised:** the reader comes in the evening and sits with them, reading aloud (a visit, a
  line or two said over it).
- **Granted:** when the evening happened.
- **Not a duplicate of:** a visit at the sickbed (the ill), books to read, a bookcase, a soft chair.

**12. The harvest in before the frost** (`Rimfolk_R4L_Wish_FrostHarvest`; the land and the seasons)
- **When:** autumn on land with a real winter, twelve or more ripe crops standing in the colony's fields, not yet marked.
- **Asks:** to bring them in before the cold. **Promised:** each ripe plant is marked for the game's own harvest, and the
  colony's people bring it in.
- **Granted:** when nine in ten of them are in.
- **Not a duplicate of:** something fresh to eat, a field of a crop of their own, something green in winter, fodder and
  firewood, the trees by the walls.

**13. A snow day** (`Rimfolk_R4L_Wish_SnowDay`; children and the seasons)
- **When:** a child at home, deep snow on the open ground, and not bitter cold.
- **Asks:** a day out in the snow. **Promised:** a gathering out in the open by day, with whoever is free, and the child
  builds a snowman there (the game's own snowman). With answers not played out, the child goes out to build one.
- **Granted:** when the day happened.
- **Not a duplicate of:** something to play with, a toy, a pet, lessons, time outside (cabin fever).

**14. A word at the grave** (`Rimfolk_R4L_Wish_GraveWord`; faith and the dead, no god named)
- **When:** someone of the colony died in the last fifteen days and lies in a grave here, and a grown colonist thought
  well of them (opinion 20 or more).
- **Asks:** to go and say a few words at the grave. **Promised:** they walk to the grave and stand there a while, a word
  said over it.
- **Granted:** when it happened.
- **Not a duplicate of:** a proper burial (someone not yet buried), a grave for an animal, a sarcophagus.

**15. A word with the visitors** (`Rimfolk_R4L_Wish_Visitors`; the colony's neighbours)
- **When:** visitors of another people at peace with the colony are on the map (a visit, traders, a caravan), and a
  sociable or curious colonist hasn't spoken with them.
- **Asks:** to go and talk with them. **Promised:** they walk over to one of the visitors and spend an hour with them.
- **Granted:** when the hour happened, or they talked with them anyway.
- **Not a duplicate of:** an evening at the camp, a trader's goods, someone joining the colony, a trip out.

**16. An hour at the shrine** (`Rimfolk_R4L_Wish_ShrineHour`; faith, the gods unseen)
- **When:** someone who holds to a god, with a shrine of it at home, is having a hard time (low mood, strain, or a
  recent loss).
- **Asks:** an hour to themselves at the shrine. **Promised:** they walk there and stay a while.
- **Granted:** when it happened. The god is named as they call it; nothing is said of what the god thinks.
- **Not a duplicate of:** a place to pray (a shrine built), an evening learning a faith, more time to rest.

## Life goals

**17. Pictures on the floor** (`Rimfolk_R4L_Asp_ChildDrawing`; children)
- **When:** a child. **Asks:** to get better at drawing (Artistic 5, two levels more), the game's own floor drawing and
  lessons doing it. **Grows into** making art worth remembering.
- **Not a duplicate of:** letters and numbers, something growing, a den, a pet, making the grown-ups proud.

**18. Five friendly contests won** (`Rimfolk_R4L_Asp_Contests`; friendly rivalry)
- **When:** anyone grown; sparked by a contest lost. **Asks:** five friendly contests won (arm-wrestling, riddles,
  storytelling and the rest of Rimfolk's moments between colonists; Rimfolk's own count).
- **Not a duplicate of:** making peace with a rival, the warrior's count, becoming a blademaster.

**19. Two masterworks** (`Rimfolk_R4L_Asp_Masterworks`; work and crafts)
- **When:** someone with a passion for crafting. **Asks:** two pieces of masterwork quality or better made by their
  hands (Rimfolk's own count).
- **Not a duplicate of:** mastery (a skill level), the maker (a hundred and twenty things), art worth remembering (a
  skill), one last thing worth keeping (one fine piece in old age).

**20. The one who carries them out** (`Rimfolk_R4L_Asp_Rescuer`; friendship and courage)
- **When:** anyone grown; sparked by a fight, by being down themselves, or by having been carried out. **Asks:** three of
  the colony's people carried out of a fight while it was still going on (Rimfolk's own count).
- **Not a duplicate of:** the healer's wounds tended, ten lives pulled through, the warrior's count, the firefighter.

## Ambitions

**21. New fields** (`Rimfolk_R4L_Amb_NewFields`; the land and the seasons)
- **Goal:** a hundred and fifty plants sown by the colony's people while it runs (the game's record), only with someone
  growing. **Reached:** a supper after the sowing. **Ages:** tribal and spacer pitch lines.
- **Not a duplicate of:** the winter cache (a harvest and pemmican), a season of plenty (food in store), a greenhouse,
  a full larder.

**22. The makers' month** (`Rimfolk_R4L_Amb_MakersMonth`; work and crafts)
- **Goal:** forty things made by hand while it runs (the game's record), only with someone crafting. **Reached:** an
  evening in the dining room with what was made.
- **Not a duplicate of:** the workshop (a room), a masterwork (one piece), a master's hand (a skill), the finer things.

**23. A good old age** (`Rimfolk_R4L_Amb_GoodOldAge`; elders)
- **Goal:** every one of the colony's elders at home sleeps in a bedroom of their own (or shared with their partner) that
  is at least decent (impressiveness 30). Pitched only while there is an elder and one of them lacks it.
  **Reached:** a supper with a toast to the old.
- **Not a duplicate of:** rooms of our own (a room for everyone, any room), a warm winter, a home and not a camp.

**24. The neighbours' call** (`Rimfolk_R4L_Amb_NeighboursCall`; the colony's neighbours)
- **Goal:** one of the neighbours' calls for help answered by one of the colony's people (Rimfolk's own count of calls
  answered). Pitched only with world events on and a call that asks for help running. **Reached:** a toast to the ones
  who went.
- **Not a duplicate of:** friends abroad (goodwill), word from outside, a market town, friends at the camp.

## New code, kept small

Seven story workers (the eighth uses the apprentice's), eight wish workers, two ambition goals. Each reads what the
game and Rimfolk already keep, and plays out with what exists: the play-out tracker's visits and gatherings (created
through their effects' `Begin`, the wish keeping the plan's number), the game's bills, designations and snowman job,
`Together.When` where answers are not played out.
