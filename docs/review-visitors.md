# Review of the visitors and strangers (28 November 2026)

The player, 28 November 2026: "regularly reviewing what you have already added to the games, all the different events,
visitor world maps, etc. Just testing whether everything works properly as intended, whether it actually has also impacts
on the game and it's not just flavor, whether it take the existing content like events and dilemmas and whatnot can be just
further enhanced. And then after, or also on the side, just add even more variety." After the events and the council, the
camps and the world, and the gods, the player chose the visitors and strangers (and, in parallel, the peoples' events). The
standing rule of 27 November: more content for the existing mechanics, no new mechanics.

Read: the visitors on the map (`Visitors/`: the visits and their lords, the callers and their work, the services, the idle
hours, keeping out of rooms, the beds, the camps and the hunt, the borne dead, the gravship landing, the market) and the
visitors' workers (`Happenings/Workers_Visitors*.cs`, `Workers_Callers.cs`, `VisitorServices.cs`, `Regulars.cs`); the
strangers (`Strangers/`: the nuisances, the nemeses, the pets, the guest stories, the lands, the effects of the questions
about a stranger, the encounters' map side and their gifts); `Outsiders/` where it is the visitors' (the talk, the gifts,
the bouts); the callers at the gate and the guests' events (88), the questions about a stranger (33) and the nuisances'
defs. Content round 6's five callers were read for bugs like the rest and not counted as new. The encounters' counts were
the camps and world review's and are not repeated here.

The fixed rules are kept by `Tools/Tests/ReviewVisitorsTests.cs`.

## How it was measured

`Tools/visitors_impact_audit.py` uses `Tools/impact_audit.py`'s reading of the game's calls (three calls deep, a worker's
`Resolved` by the answers it names) and adds what is the visitors' own:

- **The callers at the gate and the guests**: every event of the Visitors group but the peoples' own (their workers in
  `Peoples/`), the missions, the storylines and the long stories; read answer by answer: flavour-only, thin (moods and
  Rimfolk's own numbers), empty, and words that promise more than the effects do.
- **The questions about a stranger**: every council question with a stranger role, answer by answer.
- **The nuisances**: the mood each gives and an effect of its own (the new `rest` and `wear`), and the lines that tell of
  more than a mood (a night awake, a hurt, a garment torn, food a pest spoiled, a mess cleaned).
- **The callers' and the visitors' work on the map** (`CallerKind`, `ServiceKind`): the code that finishes each.
- **Code hints** in the visitors' and strangers' code: a def looked up by name without SilentFail, a hediff or wound given
  outside `Harm`, a job started with no guard against the drafted, the broken and the player's orders in the method.
- **Balance**: each answer of a caller told from data that trades silver for goods, its price against the game's own worth
  of the goods (`docs/player-mods/items.tsv`).
- **Idle hours**: listed, not counted: by the player's rule of 14 November 2026 visitors never work for the colony.

Rows read by hand and left are in `Tools/visitors_impact_skips.txt`, each with why; a stale row is named.

    python3 Tools/visitors_impact_audit.py                  # the counts
    python3 Tools/visitors_impact_audit.py --print events   # or questions, nuisances, callers, code, balance, promises, idle
    python3 Tools/visitors_impact_audit.py --write          # the measured part of this file, between its markers
    python3 Tools/visitors_impact_audit.py --root DIR       # the counts of another copy (an older commit's Source and Defs)

Besides the measure, two scans were run by hand and found nothing: every Keyed text the visitors' and strangers' code
translates has every placeholder it uses filled by the call, and the pets' and nemeses' pools use only the placeholders their
code gives. Every hediff a visitor or a question about a stranger gives ends by itself.

## Counts, before and after

| Part | Before | After |
|---|---|---|
| Callers at the gate and guests: events, answers | 88, 250 | 88, 253 (three offers added) |
| ... flavour-only, thin, empty, words that promise more | 2, 2, 12, 6 | 2, 2, 11, 6 |
| Questions about a stranger: questions, answers | 33, 107 | 33, 107 |
| ... flavour-only, thin, empty, words that promise more | 10, 11, 0, 14 | 10, 11, 0, 14 |
| Nuisances: all, with an effect of their own, a mood only | 24, 0, 24 | 26, 5, 21 |
| Nuisances' lines that tell of more than a mood | 13 | 10 |
| The callers' and visitors' work: kinds, finishing with something the game keeps | 13, 12 | 13, 12 |
| Code hints: jobs started with no guard in the method; hurts outside Harm; lookups without SilentFail | 14; 1; 0 | 12; 1; 0 |
| Balance: priced answers of the callers told from data, outside the fair range | 7, 3 | 7, 0 |

What the counts say, read by hand:

- **The callers and the guests change the game.** Of 253 answers, 4 are flavour-only or thin: two refusals, the stray that
  is the colony's from the moment it follows someone home, and a Pokémon rumour kept in mind. The 11 empty answers are
  refusals whose visitor waits in the world and never comes, or goes once the letter is answered; the camp and the war camp
  are their frames' own. The callers' work on the map ends in something real in all but one kind: the art collector's walk
  round the colony's art, which is what leads to the sale.
- **The questions about a stranger are the trade-off itself.** The 21 flavour-only and thin answers are refusals ("Keep the
  silver", "No", "Leave it be") and the status quo ("Let them rot in there"); the 14 promises are words of what happens
  anyway (a prisoner's meals) or what the game's own hunting does with the mark the answer puts on a beast.
- **The nuisances were moods.** By design each points at a fault the player can see and fix (the leak, the filth, the rot
  are the game's own), so most stay a mood. Three whose every line told of more now do it: a night by a droning generator or
  a draught at the bed costs rest, a shoddy garment wears.
- **The code hints left** are visitors' own jobs (carrying their dead, eating from their pack, shopping), the player's own
  orders, and callers that pick only the free; the one hediff outside `Harm` is a dose's protection, its bad batch being
  `Harm`'s.

## Fixed

Most serious first.

1. **Food carried out to a visitor could take a colonist from tending, a rescue or the player's order**
   (`Visitors/KeepOut.cs`, `KeepOut.Fetch`). When a visitor's food lies where visitors may not go (a cold store, someone's
   room), or a starving visitor has nothing of their own, the nearest colonist free to take part is sent with it. "Free"
   checked the drafted, the broken, the bleeding and the player's own order, but not what they were doing: a doctor tending a
   wound, someone carrying the downed to bed, a colonist at their meal or fighting a fire could be pulled away to carry a
   meal to the gate. Now only one who may break off (`PlayOuts.Interruptible`, as every errand of an answer asks).
2. **The fallen raider's plea was asked of anyone, anywhere, and "end it" was done from across the map**
   (`Council_DlmS_FallenPlea`; `Strangers/StrangerEffects.cs`, `DilemmaEffect_EndIt`). Its words say the fallen raider is
   "a few steps from" the colonist and caught their ankle, and "End it quickly" says they knelt beside them; but the
   question picked any fighter on the map, and the answer killed the raider wherever the colonist stood, even in bed or
   away. Now it is asked only of one within a few steps (`maxDistance`, `VisitorReviewMath.FewSteps`); beside them it is done
   there and then, further off the colonist walks over and does it on the map (the game's own attack on one lying down, the
   council's answer being the order); and the answer is greyed out while they are down, in a break or away.
3. **A pet's farewell was told as the day after a battle, not as it died** (`Strangers/Pets.cs`, `PetTracker.Farewell`).
   "Went down in the fighting" was any death within a day of a battle beginning (or in the game's first day, with no battle
   yet), so a pet that starved, fell ill or was slaughtered that day died a hero, and its person might swear a vow on the
   raiders; and an old pet that a wild beast or the colony's own knife killed "died of old age, warm and well fed". Now the
   lines follow the death (`VisitorReviewMath.Manner`): the fighting's lines only when an enemy's blow killed it (the game's
   combat log, as the vendettas read a death), the quiet and old-age lines only when it died with no fresh wound, neither for
   any other death.
4. **An animal given to a visitor could be given from another map, or left wandering as theirs**
   (`DilemmaEffect_GivePet`). One away with a caravan was handed to the visitor's people where it stood; one their group
   couldn't take stayed on the colony's land, theirs, with no group to leave with. Now the answer waits for it to be here,
   and one their group can't take sets off the map their way.
5. **Nobody in a mental break is ordered to take a prisoner** (`DilemmaEffect_Capture`). The answer gave the capture order
   to a colonist in a break; it is greyed out now while they are in one, down or away, as "end it" is.
6. **A visitor's game of fetch with the colony's dog** (`Visitors/IdleHoursJobs.cs`). The dog was sent after the stick
   while led on a rope, in a caravan forming or on an order of the player's; now the game ends the moment it is wanted.
7. **The trainer told "just a story" stayed for hours** (`Council_Evt_PW_ShinyRumour`; `HappeningWorker_ShinyRumour`). The
   words say they go on down the road; they now do, at once (a Hospitality guest keeps to their own stay).

Balance (`--print balance`): three callers told from data sold at well off the game's own worth. The brewer's 12 beers
(144 silver's worth) for 60 to 110 is now 8 (96); the master builder's 60 granite blocks (54) for 70 to 130 is now 90 (81);
the hunter's guide's 30 pemmican (42) for 50 to 90 is now 45 (63). The carter's hay, 100 (60) for 70 to 110, is now 150 (90),
the same as the carter's firewood.

## Given real effects

The nuisances whose every line tells of more than a mood (two new fields of `NuisanceDef`, kept small by its own check):

| Nuisance | Its lines | Now |
|---|---|---|
| A generator by the bed (`Council_Nuisance_NoisyGenerator`) | droned all night, lay awake, got up tired | a tenth of the rest bar lost |
| A draught at the bed (`Council_Nuisance_DraughtyBed`) | the cold across the bed all night, slept badly | 8% of the rest bar lost |
| Shoddy clothes (`Council_Nuisance_ShoddyClothes`) | the seams split again, a whole seam came away, patched again | the piece named loses 4% of its hit points |

The hard bed stays a mood: the game's own rest on a poor bed already costs the night.

## Added in existing frames

- **Two more nuisances** (`Defs/EncounterDefs/Rimfolk_VisR7_Nuisances.xml`, checks in `Strangers/NuisancesNew.cs`), read from
  the map like the others, each with a small effect of what its lines say and a hint at the fix: the fire in their bedroom
  gone out on a cold night (some rest lost), and boots worn below half (they wear a little more).
- **More words for the pets** (`Defs/FlavorPoolDefs/Rimfolk_VisR7_PetLines.xml`): nine farewells (for an enemy's blow, a
  quiet death, old age, and any) and six for a pet that fought at its person's side.
- **Three more offers at the gate**, in the frame of the callers told from data (`Rimfolk_Events_Neighbours4.xml`): the
  herb-gatherer leaves two bundles of herbs for leave to gather; the drovers sell a pair of young beasts; the lost traveller
  is given a meal and a night by the wall, and goes on at first light.

## Checked and found sound

- **Every visit has an end.** Each visit leaves when its time is up, when it's answered, when danger comes, when the cold,
  the heat or hunger gets to them, when their people turn hostile or one of them is harmed; a day after setting off and still
  on the map they are sent again, and those walled in leave at once (`VisitorTracker.SendOff`). What they were given goes
  with them. The callers' work each has a limit and says how far it got (`Callers.Finish`), the commissions are let go when
  their collector can't come, and goods paid for are paid on hand-over.
- **The colony's people are never taken out of the player's hands** by the callers (`Callers.CanStand`, `HoldStill`), the
  services, the outsiders' talk and gifts (the player's order, or the colonist's own idea only when free) and the duels'
  witnesses; the visitors' own jobs are theirs.
- **Harm** goes through `Harm`: the spars and brawls, the barber's nick, the bad batch of a dose.
- **Defs by name** are all looked up softly; the placeholders all filled; the hediffs all end.
- **Hostility** is the game's own: Rimfolk never turns a guest on the colony; an answer's goodwill is shown, and visitors
  whose people turn hostile leave.
- **Content round 6's callers**: texts in every age's form, placeholders the frame fills, saying nothing sends them on, two
  answers that play out, real goods by name; their prices brought to the game's worth above.
- **The nemeses** come back with raids, are captured or brought down, and are let go by the world when it ends; **the lands**
  are tags only; **the encounters' map side** meets only a stranger the colonist is really with, plays an animal's scene first,
  and the carving a guest gives is of wood, of their people's age, at the colonist's feet; **the gravship landing** queues the
  gawkers, who still check everything when they come.
- **Idle hours** give the colony nothing by design (the player, 14 November 2026: "i don't want them to generate value for me").

## Looked at and left

- **The refusals and the status quo** of the questions and the callers (the skip list): saying no is meant to cost and give
  little.
- **"Keep everyone inside"** when the named beast is back: a trade of moods; the game has no way to keep people indoors short
  of an allowed area, and none is added (no new mechanics).
- **The nuisances that stay a mood**: the fault behind each is the game's own; a single line of a skinned knuckle or a night
  awake among many is not the nuisance's.
- **The watch patrol pays 40 to 80 silver for two doses of medicine**, up to four times their worth when the colony gives its
  herbal: they need it on the road, and the colony's medicine is scarce; a sale by the colony, kept.
- **A storyteller who is a Hospitality guest** is made good at talking (the story is theirs); small and in the story's sense.
- **A nemesis who rides back with a raid** stands with it when the raid's group takes them, as the game's own leaders do.
- **Guest stories**: Hospitality's guests carry half the stories of a single visitor already; no new kind added.

## For the review of the peoples' events

Nothing of the visitors' code was changed in `Peoples/`. The visitors call into it only for who a people is (`Peoples.TechOf`,
`Peoples.CallingOf`, `Couriers.PeopleOf`), and those read sound. The peoples' callers (`Peoples/PeoplesCallers.cs` and the
events' workers built on `HappeningWorker_Visitors`) were left out of this measure as theirs.

<!-- visitors_impact_audit: begin -->

Measured by `python3 Tools/visitors_impact_audit.py --write` (this part between the markers is rewritten each run; the
review's own notes are outside them).

| Part | Defs | Answers | Flavour-only | Thin (moods and Rimfolk's numbers) | Empty | Words that promise more |
|---|---|---|---|---|---|---|
| Callers at the gate and guests | 88 | 253 | 2 | 2 | 11 | 6 |
| Questions about a stranger | 33 | 107 | 10 | 11 | 0 | 14 |

Nuisances: 26; with an effect of their own besides the mood: 5; a mood only: 21; lines that tell of more than a mood: 10.
The callers' and the visitors' work on the map: 13 kinds; finishing with something the game keeps: 12.
Code hints: 0 defs looked up by name without SilentFail, 1 hurts given outside Harm, 12 jobs started with no guard in the method.
Balance: 7 priced answers of the callers told from data; outside the fair range: 0.

### Callers at the gate and guests: flavour-only answers (0; 2 more read and left (the skip list))

None.

### Callers at the gate and guests: thin answers (moods and Rimfolk's numbers only) (0; 2 more read and left (the skip list))

None.

### Callers at the gate and guests: answers with no effect at all (refusals, or their worker's code does it) (0; 11 more read and left (the skip list))

None.

### Callers at the gate and guests: words that promise more than the effects do (0; 6 more read and left (the skip list))

None.

### Questions about a stranger: flavour-only answers (0; 10 more read and left (the skip list))

None.

### Questions about a stranger: thin answers (moods and Rimfolk's numbers only) (0; 11 more read and left (the skip list))

None.

### Questions about a stranger: answers with no effect at all (refusals, or their worker's code does it) (0)

None.

### Questions about a stranger: words that promise more than the effects do (0; 14 more read and left (the skip list))

None.

### Nuisances: a mood only (0; 21 more read and left (the skip list))

None.

### Nuisances with an effect of their own (5)

- `Council_Nuisance_NoisyGenerator`: rest
- `Council_Nuisance_ShoddyClothes`: wear
- `Council_Nuisance_DraughtyBed`: rest
- `Rimfolk_VisR7_Nuisance_HearthOut`: rest
- `Rimfolk_VisR7_Nuisance_WornBoots`: wear

### Nuisances: lines that tell of more than a mood (0; 10 more read and left (the skip list))

None.

### The callers' and the visitors' work on the map

- CallerKind `View`: later, words
- CallerKind `Hand`: items, later, words
- CallerKind `Cut`: health, later, map, needs, words
- CallerKind `Vet`: animals, health, later, words
- CallerKind `Graves`: buildings, items, later, map, people, words
- CallerKind `Load`: items, later, words
- CallerKind `Talk`: later, map, people, words
- CallerKind `Free`: later, map, people, words
- CallerKind `Night`: buildings, items, later, map, people, words
- ServiceKind `Mend`: items
- ServiceKind `Dose`: health, later
- ServiceKind `Lodge`: map, needs, words
- ServiceKind `Ease`: health

### Code: hints to read (0; 13 more read and left (the skip list))

None.

### Balance: the callers told from data, their prices against the game's own worth of what changes hands

Worth from docs/player-mods/items.tsv; a sale to the colony is the price over the worth of the goods, a sale by the colony
(their silver for the colony's goods) the silver over the worth. Listed when outside 0.5 to 2.0 (a sale by the colony: above 4.0).

- `Rimfolk_Evt_HerbGatherer/2` Buy a bundle of dried herbs ({COUNT} silver): 30 to 60 silver for 30 worth (1.0 to 2.0)
- `Rimfolk_Evt_WatchPatrol/3` Sell them two doses of medicine ({COUNT} silver): 40 to 80 silver for 20 worth (2.0 to 4.0)
- `Rimfolk_Evt_Brewer/1` Buy a load of beer ({COUNT} silver): 60 to 110 silver for 96 worth (0.6 to 1.1)
- `Rimfolk_Evt_R6Caller_Carter/1` A load of firewood ({COUNT} silver): 70 to 110 silver for 90 worth (0.8 to 1.2)
- `Rimfolk_Evt_R6Caller_Carter/2` A load of hay for the animals ({COUNT} silver): 70 to 110 silver for 90 worth (0.8 to 1.2)
- `Rimfolk_Evt_R6Caller_Builder/2` The cut stone ({COUNT} silver): 70 to 130 silver for 81 worth (0.9 to 1.6)
- `Rimfolk_Evt_R6Caller_Guide/2` Buy {X_possessive} dried meat ({COUNT} silver): 50 to 90 silver for 62 worth (0.8 to 1.4)

### What visitors do with an idle hour (their own, never the colony's work: the player's rule of 14 November 2026)

Chat, Company, Fire, Warm, Seat, Shelter, Snack, Nap, Game, Music, Stars, Art, Pray, Animal, Play, OwnDrug, Pipe, Carve, Read, Walk, Pet, Write, Train, Archery, Fish, Mend, Sharpen, Cards, Sing, Sleep.

<!-- visitors_impact_audit: end -->
