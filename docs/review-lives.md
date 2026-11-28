# Review of the colonists' own lives (28 November 2026)

The player, 28 November 2026: "regularly reviewing what you have already added to the games, all the different events,
visitor world maps, etc. Just testing whether everything works properly as intended, whether it actually has also impacts
on the game and it's not just flavor, whether it take the existing content like events and dilemmas and whatnot can be just
further enhanced. And then after, or also on the side, just add even more variety." After the events and the council, the
camps and the world, the gods and the visitors, the player chose the colonists' own lives (and, in parallel, a round giving
the peoples' older events their own ages). The standing rule of 27 November: more content for the existing mechanics, no new
mechanics.

Read: the wishes (`Wishes/`, the grant and the lapse in `CouncilDynamics.cs`, the hourly check in `CouncilGameComponent.cs`,
`Defs/WishDefs`, 141 wishes): asked, promised, granted, let lapse, beliefs, kept promises; the ambitions (`Ambitions/`,
`Defs/AmbitionDefs`, 73): pitched at the meeting, reached by goals; the life goals (`Aspirations/`, `Defs/AspirationDefs`,
120): a record, a skill, a counter, a seat, the heart's needs, and what a reached goal leaves behind (`GoalRewards`); the
personal stories (`Arcs/`, `Defs/ArcDefs`, 56): their gates (a talk, time together, work, a skill, a record, a custom gate)
and their stages' effects; and of `Lives/` and `LifeVariety/` what belongs to one colonist's life (the stories and wishes of
the colony's own life, the toy, the seats' goals, the everyday journal, the legacy). Content round 4's twenty-four pieces
(`docs/content-round-4-lives.md`) were read for bugs like the rest. The gods' vows (`Defs/GodDefs/Rimfolk_Worship_Goals.xml`)
were the gods' review's and are not counted here.

The fixed rules are kept by `Tools/Tests/ReviewLivesTests.cs`.

## How it was measured

`Tools/lives_impact_audit.py` uses `Tools/impact_audit.py`'s reading of the game's calls (three calls deep) and adds what is
the lives' own:

- **Can it finish?** Every wish's worker is in the code, says when it comes true and offers it to someone. Every record a life
  goal or a story counts is one of the game's or the player's mods' (`docs/player-mods`, active rows), and one of Rimfolk's own
  is counted somewhere in its code; every counter a life goal waits on is told to the life goals; every custom gate is answered
  by its story's worker, every talk is a story's talk, every Skill, Practice or Made gate has a skill to be about, and every
  Goal gate's worker names the goal it waits for. Every ambition's goals are in the code, and their records counted.
- **Impact.** A wish: the memory a granted wish gives (its mood, how long), or what a memory gives instead of mood (recreation,
  practice, trust: `RewardInstead`), its grant effects, a likelier inspiration, a lasting thing the council keeps watch over.
  A life goal: the skill its reaching marks (`GoalRewardMath.SkillFor`). A story: what its stages and its ending do. An
  ambition: what reaching it sets going on the map.
- **Shown and true to the words.** The placeholders each frame's words use against the ones its code fills.
- **Code hints** in the six folders: a def looked up by name without SilentFail, a hediff or a wound given outside `Harm`, a
  job started with no guard against the player's own order in the method (a check of the drafted and the broken alone no
  longer passes); and every hediff the frames give, whether it ends.

Rows read by hand and left are in `Tools/lives_impact_skips.txt`, each with why; a stale row is named.

    python3 Tools/lives_impact_audit.py                  # the counts
    python3 Tools/lives_impact_audit.py --print wishes   # or goals, stories, gates, ambitions, code, hediffs, placeholders
    python3 Tools/lives_impact_audit.py --write          # the measured part of this file, between its markers
    python3 Tools/lives_impact_audit.py --root DIR       # the counts of another copy (an older commit's Source and Defs)

Besides the measure, every frame's death and departure was read by hand (below, "Checked and found sound"), and the newest
code (content round 4, the colony's own life, the life goals of the fourth round of people) line by line.

## Counts, before and after

| Part | Before | After |
|---|---|---|
| Wishes: all, with a problem found, thin | 141, 0, 3 | 144 (three added), 0, 3 |
| ... with a memory of a real number when granted; lasting (watched) | 135; 68 | 138; 68 |
| Life goals: all, with a problem found | 120, 0 | 120, 0 |
| ... marking a skill when reached, of the heart | 43, 77 | 54, 66 |
| Personal stories: all, with a problem found | 56, 1 | 56, 0 |
| ... ending in something the game keeps, in moods and words (in every stage) | 47, 9 (7) | 48, 8 (5) |
| Ambitions: all, with a problem found, with a scene of their own when reached | 73, 0, 73 | 73, 0, 73 |
| Code hints: lookups without SilentFail; hurts outside Harm; jobs with no guard | 0; 2; 3 | 0; 2; 0 |
| Hediffs given, that never end; placeholders never filled | 3, 0; 0 | 3, 0; 0 |

What the counts say, read by hand:

- **The frames already change the game.** A granted wish gives a memory (135 of 141 with a real number, three more something
  instead of mood), trust, a few days of working faster (`Council_Motivated`) and sometimes an inspiration, and the thing
  itself is usually what the player built, made or did; a lapse costs trust and a mood, sometimes a sour spell, a broken promise
  more. A reached life goal always inspires, raises a toast at the next gathering, names the deed on the Story tab and, where it
  was about a skill, marks it (it never slips, comes a little faster, and someone younger with the passion spends some hours
  learning at their side). A reached ambition gives the whole colony a mood scaled by the work it took, trust, sometimes an
  inspiration, its scene on the map and its day on the calendar.
- **The thin ones are the thing itself.** The three wishes with no mood of a real number give what was asked for (a familiar
  bound, a toy handed to the child, the ball belts on the rack); the stories that end in a mood end in what the story was about
  (trust in the council, a goal reached, a custom become a tradition, a seat held, a stray come home), or did their work on the
  way.
- **The life goals of the heart** (a marriage, friends, years, a feeling let go, a faith shared) mark no skill by design. Eleven
  goals about work marked none either; they do now (below).

## Fixed

Most serious first.

1. **The year's aim could never end well** (`Rimfolk_Arc_YearsAim`; `LifeVariety/ColonyStories.cs`, `ArcWorker_YearsAim`).
   Its last stage waits for the life goal reached, read by the tracker from the story's goal; the worker kept the goal in its
   key only, so the gate never opened, and sixty days after a colonist reached the goal they had said aloud at the year's first
   supper the story was told as missed ("The year's nearly done, and I'm not there"). Now the story keeps the goal (one begun
   before takes it from its key), and a goal reached in one go no longer waits forty days at halfway. The audit now checks every
   Goal gate's worker names its goal (only this one didn't).
2. **Friends over the hills seldom came to anything** (`Rimfolk_R4L_Arc_OverTheHills`). When the people the colonist got on
   with came back, the words say "I'm going over to see them as soon as I can", and the story waited three days for the two to
   be a few steps apart; nothing sent the colonist, so unless their work took them past the visitors it ended "they came and went
   while I wasn't looking". Now they walk over to one of that people, the one they knew first if he or she came, and spend an hour
   with them (a visit of the play-out engine, `LivesReviewEffect_SeeTheirPeople`, `Arcs/LivesReview.cs`); the story's own gate is
   unchanged.
3. **The harvest in before the frost was met by the frost** (`Rimfolk_R4L_Wish_FrostHarvest`). It came true once the crops it
   named no longer stood ripe, so a cold night, a blight or a fire that killed them was thanked for as the harvest brought in.
   Now it needs the colony's people to have brought in half as many harvests since it was asked as the plants it named (the
   game's own record); crops lost with nobody bringing them in let it go without blame.
4. **Something new at the bench was met by a trader** (`Rimfolk_R4L_Wish_NewMake`). One of the thing anywhere in the colony
   made it come true, a trader's or a raider's left behind, and they said "Made it"; content round 4 meant it as one in the
   colony and something made by them since. Now so, and its condition says it.
5. **A wish's errand could take someone from tending, a rescue or the player's order** (`Arcs/Round4Lives.cs`, `WalkTo` and the
   snow day's child; `Wishes/WishWorkers_Seasons.cs`, the promise of warmer or cooler clothes). With answers not played out on
   the map, a promise walked the colonist to the grave, the shrine or the visitors, sent the child out to build a snowman, or had
   someone go and put on clothes, checking only the drafted, the downed and the broken. Now only one free to break off
   (`Round4Lives.Free`: `OwnHappenings.Held`, reused, and `PlayOuts.Interruptible`); the promise waits otherwise.
6. **Ready for the seat practised a skill they couldn't use** (`Rimfolk_Arc_ReadyForTheSeat`). Its first stage counts practice
   in the seat's first skill, taken whether or not the colonist was capable of it; the practice never came and the stage sat out
   its forty days. Now the seat's first skill they can use, else talk, else it doesn't begin.

## Given real effects

| Where | The words | Now |
|---|---|---|
| Eleven life goals about work (`GoalRewardMath.SkillFor`) | the mender's forty things mended, the haggler's six deals, two masterworks, a seat held: the reeve's, the peacemaker's, the keeper's, the herdmaster's, the chaplain's, the warden's, the elder's, the quartermaster's | reaching one marks the skill it was about, as every goal of work did: building, talk, crafting or art (the better of the two), and each seat its own first skill (the reeve's growing, the herdmaster's animals, the others' talk). A marked skill never slips and comes a little faster, someone younger with the passion learns at their side, and the inspiration is the one that fits. Goals reached before are marked on load. |
| Somewhere to belong (`Council_Arc_FindingHome`), its second stage | "has a friend here now. It was {OTHER} who made the first move" | the two friends in the social web, a small start (8) |
| Friends over the hills, its ending | "We talked until they had to go", "The friendship with {FACTION} holds" | the hour of talk as recreation (15%), and that people's goodwill, +3 |

## Added in existing frames

Wishes are the frame offered most often: by default a wish every two days, about thirty a year from 141, against four
personal stories a year from 56, about one life goal a colonist a year from 120 and an ambition every fifteen days from 73. So
the additions are three wishes, all of the XML-driven kinds (`Defs/WishDefs/Rimfolk_LivR7_Wishes.xml`, their moods in
`Defs/ThoughtDefs/Rimfolk_LivR7_Thoughts.xml`), in the wishes' flavour style, one sentence a line:

- **Boots for them** (`Rimfolk_LivR7_Wish_Boots`, Gear): someone going barefoot, when the colony can make boots or has a pair
  in store. Found by name ("boot", "shoe", "sandal", "footwear"): the game has no footwear, so it comes with the player's mods
  that add it (Vanilla Apparel Expanded's boots and shoes, Vanilla Armour Expanded's plate and marine boots, the elves' boots)
  and in the age of what the colony can make.
- **Gloves for the cold** (`Rimfolk_LivR7_Wish_Gloves`, Gear): one who loves outdoor work (growing, mining, building, animals)
  with the cold coming; gloves or mittens, found by name the same way.
- **Cloth and hides in store** (`Rimfolk_LivR7_Wish_Cloth`, Work): a tailor (a passion for crafting) with the cold coming
  wants two hundred of any textile put by; any source, a petition others may join.

Each gives a mood the size of the hat's or the firewood's (3), never asked by one whose faith keeps them unclothed (the two to
wear), and like every wish is promised, granted or let lapse in the frame there is.

## Checked and found sound

- **The dead and the departed.** A wish of someone dead, departed, kidnapped or made a slave is let go quietly, never against
  the council, and those who backed it are dropped one by one; a wish waits while they are away. A life goal ends with its
  holder's death (mourned, and an old one's last wish may be taken up) or departure; one of someone away waits. A story ends
  with its colonist's death (a eulogy a day later), departure or enslavement; one whose other or third dies or leaves fizzles
  or, for an old companion, ends as a farewell; a story that only leans on a friend goes on without them. An ambition whose
  proposer died is reached with the scene led by the seat or by nobody, and no deed for the dead.
- **Who is asked.** Slaves, guests, babies and anyone in a break, down, drafted or in danger never voice a wish; children only
  the wishes and goals made for them (`who.child`, `child` in a wish's wants); work wishes check the work is theirs to do.
- **Can finish.** Every record counted, every counter told, every custom gate answered, every talk a story's talk, every
  practice gate with a skill, every placeholder filled (the audit's measure, now also a test for every frame).
- **Harm** goes through `Harm`; the one hediff a wish gives outside it is `Council_Motivated` (working faster); every hediff the
  frames give ends.
- **Jobs**: all else plays out through the play-out engine (which takes only the free), `Together.When` or the meeting's own
  `CanWake`.
- **Defs by name** are all looked up softly.
- **Every age and the full mod pool**: the data-driven wishes ask by what a thing is (its name, its category, its layer), never
  by a fixed mod's def; ambitions carry their eras; content round 4's stories and wishes read the colony's own benches, crops,
  books and visitors.
- **The gods are unseen**: the wishes and stories of faith name a god only as the colonist calls it, and say nothing of what
  the god did.
- **Balance**: wish moods run from 2 to 10, the largest for the largest asks (a rescue, leaving the colony, a wedding); every
  life goal shares one reward frame; an ambition already mostly done when chosen is worth less (`PressureMath.EffortFactor`).

## Looked at and left

- **The life goals tied to family** (`Council_Asp_Marry`, `Council_Asp_Child`, `Council_Asp3_Grandchild`,
  `Council_Asp4_ElderFamily`): goals of one person's heart, rewarded with what every goal gives (an inspiration, a toast, the
  deed told round the colony), nothing for a family line. A deed for a wedding was dropped at the merge of the fourth round of
  people under "the colony is the family"; reaching one of these still tells the deed "goal reached". A question for the player,
  not changed here.
- **The thin wishes and stories** (the skip list): the thing granted or the ending reached is their effect.
- **Lead the rites** (`Rimfolk_Asp_Celebrant`) needs Ideology, though Rimfolk's own gods' rites count too: a goal for a faith's
  rites, kept to the faith's DLC.
- **No life goal on night watches or rounds of the sick**: those records come only from events' play-outs, too seldom for a goal
  of years.
- **No stories, life goals or ambitions added**: each is offered far less often than a wish.

<!-- lives_impact_audit: begin -->

Measured by `python3 Tools/lives_impact_audit.py --write` (this part between the markers is rewritten each run; the
review's own notes are outside them).

| Frame | Defs | With a problem found | Changing the game | Moods and words only |
|---|---|---|---|---|
| Wishes | 144 | 0 | 17 with more of their own besides the frame's | 3 thin |
| Life goals | 120 | 0 | 54 marking a skill (all inspire and raise a toast) | 66 of the heart |
| Personal stories | 56 | 0 | 48 ending in something the game keeps | 8 ending in moods and words (5 in every stage) |
| Ambitions | 73 | 0 | 73 with a scene of their own when reached | - |

Wishes: 138 give a memory of a real number when granted; 68 are lasting ones the council keeps watch over. Every grant also
gives trust and a few days of working faster, and every lapse costs trust and a mood (the frame's own).
Code hints: 0 defs looked up by name without SilentFail, 2 hurts given outside Harm, 0 jobs started with no guard in the method.
Hediffs given: 3; that never end: 0.
Placeholders the frames' code never fills: 0.

### Problems found: can't finish, never counted, missing (0)

None.

### Wishes: thin (no memory of a real number when granted, nothing of their own) (0; 3 more read and left (the skip list))

None.

### Wishes with more of their own besides the frame's

- `Council_Wish_OwnAnimal`: watched
- `Council_Wish_Barn`: watched
- `Council_Wish_FavouriteAnimal`: needs; an inspiration 15% more likely; watched
- `Council_Wish_PackAnimal`: skill; watched
- `Council_Wish_Mount`: watched
- `Rimfolk_Wish_Shelves`: watched
- `Council_Wish_PetBed`: watched
- `Council_Wish_Sickbed`: watched
- `Council_Wish_Healroot`: watched
- `Council_Wish_SeatsForAll`: watched
- `Council_Wish_Apprentice`: an inspiration 10% more likely
- `Council_Wish_Fishing`: watched
- `Council_Wish_Telescope`: an inspiration 10% more likely; watched
- `Council_Wish_ToolCabinet`: watched
- `Council_Wish_CommsConsole`: watched
- `Council_Wish_Biosculpter`: watched
- `Council_Wish_Armchair`: watched
- `Council_Wish_Sarcophagus`: watched
- `Council_Wish_ChildPet`: watched
- `Council_Wish_Storybook`: watched
- `Council_Wish_CoolRoom`: watched
- `Council_Wish_PenMarker`: watched
- `Council_Wish_Floor`: watched
- `Council_Wish_Birthday`: an inspiration 10% more likely
- `Council_Wish_ArtInRoom`: watched
- `Council_Wish_DayOff`: watched
- `Council_Wish_Music`: an inspiration 15% more likely; watched
- `Council_Wish_OwnRoom`: watched
- `Council_Wish_NicerBedroom`: watched
- `Council_Wish_Spacious`: watched
- `Council_Wish_ShareBed`: health; watched
- `Council_Wish_BedroomFurniture`: watched
- `Council_Wish_Light`: watched
- `Council_Wish_ComfortTemperature`: watched
- `Council_Wish_DiningTable`: watched
- `Council_Wish_RecreationVariety`: watched
- `Council_Wish_Greenery`: watched
- `Council_Wish_Toilet`: watched
- `Council_Wish_WashPlace`: watched
- `Council_Wish_HotWater`: watched
- `Council_Wish_Companion`: an inspiration 20% more likely; watched
- `Council_Wish_ChildPlay`: watched
- `Council_Wish_Grill`: watched
- `Council_Wish_GuestBed`: watched
- `Council_Wish_Scarecrow`: watched
- `Council_Wish_GuestRoom`: watched
- `Council_Wish_PM_MagicCircle`: watched
- `Council_Wish_PW_OwnPokemon`: watched
- `Council_Wish_Quit`: an inspiration 20% more likely
- `Council_Wish_Separate`: watched
- `Council_Wish_Infirmary`: an inspiration 15% more likely; watched
- `Council_Wish_CoolClothes`: needs
- `Council_Wish_Sauna`: watched
- `Council_Wish_Hearth`: watched
- `Council_Wish_Bookcase`: an inspiration 10% more likely; watched
- `Council_Wish_Books`: watched
- `Council_Wish_TrainingDummy`: watched
- `Council_Wish_Crib`: watched
- `Council_Wish_NestEgg`: watched
- `Council_Wish_Shrine`: watched
- `Council_Wish_Bath`: watched
- `Council_Wish_Smokehouse`: watched
- `Council_Wish_Beehive`: watched
- `Council_Wish_Fountain`: an inspiration 10% more likely; watched
- `Council_Wish_Banner`: watched
- `Council_Wish_WeaponRack`: watched
- `Council_Wish_MoreArt`: an inspiration 10% more likely
- `Council_Wish_GardenSeat`: watched
- `Council_Wish_PassionSpace`: an inspiration 35% more likely; watched
- `Council_Wish_LetMeWork`: health; watched
- `Council_Wish_Artwork`: an inspiration 25% more likely; watched
- `Council_Wish_Brewery`: watched
- `Council_Wish_Freezer`: watched

### Personal stories ending in moods and words only (0; 8 more read and left (the skip list))

None.

### Life goals of the heart (no skill to mark when reached)

`Council_Asp_Marry`, `Council_Asp_Child`, `Council_Asp_Friends`, `Council_Asp_Teach`, `Council_Asp_Peace`, `Council_Asp_Seat`, `Council_Asp_Room`, `Council_Asp_Name`, `Council_Asp_Endure`, `Council_Asp_Grow`, `Council_Asp_Seer`, `Council_Asp_Avenge`, `Council_Asp2_Gravedigger`, `Council_Asp2_Spotless`, `Council_Asp2_Firefighter`, `Council_Asp2_Beloved`, `Council_Asp2_LongHaul`, `Council_Asp2_Township`, `Council_Asp3_BestFriend`, `Council_Asp3_GrowUp`, `Council_Asp3_Grandchild`, `Council_Asp3_OneMoreYear`, `Council_Asp3_Legacy`, `Council_Asp3_FaithRole`, `Council_Asp3_SharedFaith`, `Council_Asp3_Belonging`, `Council_Asp3_Freedom`, `Council_Asp3_Heard`, `Council_Asp_PM_MagicCraft`, `Council_Asp_PM_MightCraft`, `Council_Asp_PM_MagicLevel`, `Council_Asp_PM_MightLevel`, `Council_Asp_PM_Expertise`, `Council_Asp_Nemesis`, `Rimfolk_Asp_ReadersYear`, `Council_Asp4_ChildProud`, `Council_Asp4_TeenSeriously`, `Council_Asp4_TeenRoad`, `Council_Asp4_TeenLove`, `Council_Asp4_ElderSeeGrown`, `Council_Asp4_ElderPeace`, `Council_Asp4_ElderFamily`, `Council_Asp4_FaithAgain`, `Council_Asp4_FaithShared`, `Council_Asp4_HeartLove`, `Council_Asp4_HeartSafe`, `Council_Asp4_HeartPurpose`, `Council_Asp4_HeartGrief`, `Council_Asp4_HeartShame`, `Council_Asp4_HeartFriend`, `Rimfolk_Asp_SureFooted`, `Rimfolk_Asp_FriendOfTheRoad`, `Rimfolk_Asp_GoodNeighbour`, `Rimfolk_Asp_Daykeeper`, `Rimfolk_Asp_Peacemaker`, `Rimfolk_Asp_Celebrant`, `Rimfolk_Asp_NewHome`, `Rimfolk_Asp_Visit`, `Rimfolk_R4L_Asp_Contests`, `Rimfolk_R4L_Asp_Rescuer`, `Rimfolk_Asp_Pilgrimage`, `Rimfolk_Asp_Temple`, `Rimfolk_Asp_VowLamp`, `Rimfolk_Asp_VowTrees`, `Rimfolk_Asp_VowPlainBread`, `Rimfolk_Asp_VowVisitors`.

### Code: hints to read (0; 2 more read and left (the skip list))

None.

### Placeholders the frames' code never fills (0)

None.

### Hediffs that never end (0)

None.

<!-- lives_impact_audit: end -->
