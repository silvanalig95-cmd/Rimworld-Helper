# Review of the gods (28 November 2026)

The player, 28 November 2026: "regularly reviewing what you have already added to the games, all the different events,
visitor world maps, etc. Just testing whether everything works properly as intended, whether it actually has also impacts
on the game and it's not just flavor, whether it take the existing content like events and dilemmas and whatnot can be just
further enhanced. And then after, or also on the side, just add even more variety." After the events and the council, and
the camps and the world, the player chose the gods. The standing rule of 27 November: more content for the existing
mechanics, no new mechanics.

Read: every one of the gods' own happenings (`OwnHappening`, 110: 65 on the land, 45 between the colony's people) and the
code that plays each out, the gods' signs (`OmenKind`, 43), the touch and the occurrences' own effects that follow what
was seen (`Gods/Touch/`), the offerings and their ways of taking, the rites, prayers, vows and festivals, the gods' events
with answers (the warm voice's bargain, the wounded thrumbo, the great cat's riddle, the stranger at the gate), the council's
questions of the gods and the faiths (25), the gods in the world (letters, the slight, the myths at supper, the Hunger's
bargain, the grudges), the colony's book and its keeper's reading of each god's temper, and the words of all of it in
`Languages/English/Keyed`. `OldPowers` where it is the gods' (its walks and steps), and `Faith` where the gods' code calls
it. The world events and camps of the gods were in the camps and world review; the council's questions of the gods were
counted in the events and council review, and were read again here for the unseen rule and for the gods' names.

The fixed rules are kept by `Tools/Tests/ReviewGodsTests.cs`.

## How it was measured

`Tools/gods_impact_audit.py` sorts what each of the gods' happenings, signs and answers carries, with
`Tools/impact_audit.py`'s reading of the game's calls (three calls deep) and the gods' own: a god's hidden favour, a
colonist's belief and the book are Rimfolk's own state, a memory of each other is the game's opinions (relations), a walk or
a wait is a play-out. A happening's code is its `Between` kind's class, or the methods of its god's file that note it as
come and those named for it (a short table names the rest). What its witness saw (its `CC_God*_Saw_` key) adds what follows
it: its own effect in `GodImpactMath.Table`, an effect of its own code (`GodTouchMath.OwnEffectKeys`), or else one of its
god's touch effects (real, but random). The council's questions of the gods and the gods' events are read answer by answer
as `impact_audit.py` reads every question. The words are searched for a card's or message's text that names a god, and for
words of a god's mind (favour, pleased, wants) outside the book's and the faiths' own texts.

    python3 Tools/gods_impact_audit.py                       # the counts
    python3 Tools/gods_impact_audit.py --print happenings    # or thin, flavour, signs, questions, touch, unseen
    python3 Tools/gods_impact_audit.py --write               # the measured part of this file, between its markers
    python3 Tools/gods_impact_audit.py --root DIR            # the counts of another copy (an older commit's Source, Defs, Languages)

The measure is a hint to read by hand: it tells that a happening does something the game keeps, not that it does what its
words say. Every happening, sign and answer it lists as thin was read, and the rest were read where the words promised
something particular (a cold snap broken, the rain called, a floor scrubbed, a wound tended).

## Counts, before and after

| Part | Before | After |
|---|---|---|
| The gods' happenings on the land: with a game effect, thin, flavour only | 65: 65, 0, 0 | 65: 65, 0, 0 |
| The gods between the colony's people: with a game effect, thin, flavour only | 45: 45, 0, 0 | 45: 45, 0, 0 |
| The gods' signs, each with an effect of its own | 43 | 46 |
| The council's questions of the gods, answers: with a game effect, thin, flavour only | 77: 60, 13, 4 | 77: 60, 13, 4 |
| The gods' events with answers, answers: with a game effect, thin | 11: 8, 2 | 11: 8, 2 (the tending's lesson is in the play-out's code, which the measure doesn't follow) |
| Card or message texts naming a god; words of a god's mind (read by hand) | 4; 7 | 4; 7 |

What the counts say, read by hand:

- **The gods change the game.** Two earlier rounds were about exactly this (the player, 15 November 2026: "for many of the
  gods occurences i don't see any real game impact", and 17 November 2026's 55 proposals): every occurrence someone sees is
  felt, by belief, and followed by its own effect or its god's touch; every sign has an effect of its own. Nothing on the
  land is only a mood and words.
- **The gods between the colony's people are about the people, by design** (round 6, the player, 20 November 2026: "the
  relations between pawns"): each ends in the game's own opinions of each other (closer, a quarrel, made up, jealous, wary),
  and many do something besides (a coat worn, a floor scrubbed, a machine running again, a lamp lit, the frost off the
  ground). They take no touch on top, as `GodTouchMath.OwnEffectKeys` says.
- **The thin council answers are refusals** ("Not now", "Let it be", "Let {DEAD} rest", "Each keeps their own thanks") or
  answers that play out later in their own frame (the two rival feasts kept on their days, a place given once the player
  picks the spot). Left by design, as the events and council review left them.
- **The words of a god's mind** the search found are words nobody sees (two favour words and two natures' lines no code
  calls, a dev action's label) or what a colonist dreamt ("a warm voice ... that sounded displeased"), which is theirs to
  feel. **The texts naming a god** are the neighbours' letters, where their people speak of their own god, and the
  message that asks the player to choose a place for the god the council agreed to.

## Fixed

Most serious first.

1. **A god's happening could take someone out of the player's hands** (`Gods/OnMap/OwnHappenings.cs`, `OwnHappenings.Job`;
   `OwnFeasts.TakeThen`). Every walk, wait and carry of the gods' own happenings goes through `OwnHappenings.Job`, which
   started the job forced whatever the pawn was doing. A scene chooses its people while they are free, but its later steps
   send them on again: the wounded thrumbo's tender walked back out to it, the feast's table sat its eaters down, a
   happening's witness walked over to look, though the player had drafted them since, or they were in a mental break, or on
   an order the player gave. Now one drafted, in a break, down or on the player's own order is held from every happening
   (`OwnHappenings.Held`; the scene waits, and lets go at its time, as when someone is away), and the feasts' carrying from the
   stores is refused the same way. The 222 calls in the gods, the old powers, the giving day, the telling of the bees and the
   round 3 kitchen all go through the one door.
2. **The colony's own words called a god by a name the colony may never have heard** (`Worship.ArgsCalled`). A god's page in
   the colony's book is under the name it was first heard by: a tribal colony may know Hollin only as Grandmother Root, a
   medieval one as the Green Lady, and two names for one god stay apart until someone works it out (docs/design/gods.md, "The
   colony's book"). But the council's questions of the gods filled {GOD} with the god's own name while {GOD2} and {GOD3}
   were the colony's ("Honour Hollin this year" beside "Honour the Green Lady"), and so did the festival's name on the
   colony's calendar, a colonist's word about a god in the world, the myth told at supper and the colony's own slighting
   letter and its answer: each showed the player a name the pawns didn't know, and joined two names the book keeps apart. Now
   each is the colony's name for the god, or the speaker's (`Worship.Called`), and a god the colony hasn't heard of is "a god".
   The worship questions and the second, third and fourth rounds' questions of the faith all take their gods from the one
   context, `GodDilemmaContext`, so the one change mends them all.

## Answers and happenings given real effects

- **The wounded thrumbo tended** (`Rimfolk_Evt_GodHurtBeast`, "Send {A} to tend it": "{A} walks out to the {BEAST} to tend
  its wounds"). The tending is the game's own doctoring, wound by wound by the tender's skill, and now teaches as doctoring
  does: the tender learns Medicine (200) once its wounds are tended (`OwnBellwether.TendLesson`).

That is the only one. Read by hand, the gods' words keep their promises: the vigil kept through a cold snap ends the snap
at dawn, the song round the well brings the rain, the wager's loser scrubs the floor with the game's cleaning, the coat is
worn, the drink found is real, the coin is gold, the ring of mushrooms rots the body away, the bargain's silver goes down the
shaft and its gold comes back.

## Added in existing frames

**Three more signs of the gods' own** (`OmenKind`, `GodShapeDef.omens`; `Gods/OnMap/GodOmensReview.cs`). Selene, Murmur and
Helios each had one or two signs of their own and the rest borrowed (the strange calm any god may send, Morel's smell of
earth). Each new one shows on the map as its words say, is said aloud by who saw it (three lines each, joined to the signs'
own pool), is kept in the colony's book in their words, names no god, and leaves its own effect through the occurrences'
table (`GodImpactMath.Table`, 56 to 58):

| Sign | Whose | On the map | What follows |
|---|---|---|---|
| `Moths` | Selene | At night, pale moths at one of the colony's lights, far more than anyone has seen, and someone near sees them. | The one who watched them is inspired. |
| `WaterRisen` | Murmur | At dawn, on a night with no rain, the water stands higher in the wet ground or the well: splashes, a cold glimmer. | Flowers come up in a ring where it stood. |
| `SunShaft` | Helios | On a gray day (rain, fog or cloud), a single shaft of sun comes down onto one place in the open near someone, and stays. | The crops in the open grow a day ahead, and solar panels give their most until sundown. |

They come as the others do: one of a god's signs a day in the days before it comes or acts (`GodOmens.Before`), each when it
can show (the dark, a dawn without rain, a gray sky), at its own hour. No god is named and nothing says why.

## Checked and found sound

- **The unseen rule.** No number, favour, wants, rules or truths in any card, message, bubble, inspect line or tooltip;
  favour moves only hidden state, and the council's answers never say how a god takes them (`DilemmaEffect_GodFavour` has
  no line on the sheet). A card's title names a god only through `Gods.CardName` and `GodsOnMapWords.Title`, its text
  never; the Hunger nowhere (the whisper's and the lamp's cards have no named title). The keeper's reading of each
  god's temper is words only, a step at a time toward the truth or beside it, late, "some say" when nothing was seen, and
  only in the god's chapter of the book (`GodTemper`).
- **Harm.** Every harm the gods do to the colony's people goes through the shared budget (`Harm.Roll`, `Harm.May`): the
  burn of the warm dream, the lightning on a busy place, the grudge's beasts and lightning, the riddle's cuff, the wrapped
  dead, the cup's addiction, the old powers' hurts. The wounded thrumbo's wounds are the wild's; the Hunger's price (years,
  a mark, a mind) is the bargain the colony made with it.
- **Every card's placeholders** in the two frames every happening uses (`OwnHappenings.Card`, `OwnBetween.Card`, `Book`,
  `End`) and the book's lines (`GodsOnMapWords.Saw`), against what each call passes: all filled.
- **Defs looked up** by name are looked up softly (`GetNamedSilentFail`) but one, a magic class the code has already found
  with it (`GodWorldGifts.Pick`).
- **Hediffs that never end** are the ones meant to stay: a god's hand and the god's mark (gifts for life), the priest's
  office (taken off by the office), the avatar and the wrapped dead (their comps end them).
- **The council not answering.** The gods' events' default answers are the quiet ones (keep the silver, leave the beast,
  let the cat lie, let the stranger sleep by the gate), and their workers act only on the answer given.
- **Jobs at the start of a happening**: chosen among those awake, free and not drafted (`GodOmens.Awake`, `OwnBetween.Able`,
  `WorshipJobs.Free`, `HeartActs.Safe`); the later steps are fixed above.
- **The gods' own effects that need someone**: an inspiration, the sweet smoke's cheer and a bad night go only to one who
  saw it; the rest are on the place, there to be found.

## Looked at and left

- **The council's refusals** ("Not now", "Let it be", "Each keeps their own thanks"): the colony keeps its time and goods,
  and pays in moods and trust. By design.
- **The two rival feasts and a place given** (`Rimfolk_Dlm_RivalFeasts`, `Rimfolk_Dlm_PlaceAsked`): thin by the measure,
  they play out later in their own frames (the feasts on their days, kept in full or quietly; the place once the player
  picks it with the place's button).
- **"Asked that it be buried"** (the word over a fallen raider, D12): a request to the colony, which buries its dead and the
  raiders' as the player sets it; the scene is the two people at the body.
- **The neighbours' letters naming their own gods** by the god's own name (a holy war, a myth, an invitation, an envoy):
  their people's words, as a people names its gods; the colony's own words are fixed above.
- **The thrumbo's lesson on the answer's card**: the lesson comes with the tending, which may not happen (the tender kept
  away, the beast gone), so it is not promised on the card before the choice.
- **More happenings** in the gods' own frame (`OwnHappening`): left for a content round; this review added signs.

<!-- gods_impact_audit: begin -->

Measured by `python3 Tools/gods_impact_audit.py --write` (rewritten each run; the review's own notes are outside the markers).

| Part | Count | With a game effect | Thin (moods, belief, words) | Flavour only |
|---|---|---|---|---|
| The gods' happenings on the land | 65 | 65 | 0 | 0 |
| The gods between the colony's people | 45 | 45 | 0 | 0 |
| The gods' signs (46 with an effect of their own, 0 followed by the touch) | 46 | 46 | 0 | 0 |
| The council's questions of the gods (answers) | 77 | 60 | 13 | 4 |
| The gods' events with answers (answers) | 11 | 8 | 2 | 0 |

Questions and events: 29; whose every answer is thin: 3. Answers with no effect at all: 1. Words that promise more: 3.
The unseen rule in the words: 4 card or message texts that name a god; 7 lines with words of a god's mind outside the book and the faiths' texts.

What the happenings carry (happenings with at least one of the kind): items 62, goodwill 1, skill 1, health 13, needs 27, work 1, relations 45, people 26, animals 1, buildings 48, threat 1, plants 7, weather 15, touch 12, map 91, state 104, mood 64, words 104.

### Happenings that are thin (moods, belief and words only)

None.

### Each happening

| Happening | Carries | What follows its sight |
|---|---|---|
| `HollinRoots` | buildings, items, touch, words | a touch of Hollin |
| `HollinDryads` | buildings, map, touch | a touch of Hollin |
| `HollinWalk` | buildings, items, map, people, state, weather, words | its code's own |
| `HollinOldTree` | buildings, items, map, people, plants, state, words | its code's own |
| `SmoulderBargain` | buildings, items, map, mood, people, relations, state, words | its code's own |
| `SmoulderName` | items, map, people, state, words | its own: Admired |
| `SmoulderHeat` | people, state, touch, words | a touch of Smoulder |
| `SmoulderStretch` | mood, state, words, work | its own: DigFaster |
| `SmoulderBurn` | health, items, map, mood, needs, people, state, touch, words | a touch of Smoulder; its code's own |
| `BellwetherRoad` | buildings, map, mood, needs, people, state, touch, words | a touch of Bellwether; its code's own |
| `BellwetherHorn` | items, map, state, touch, weather, words | a touch of Bellwether |
| `BellwetherHurt` | buildings, health, items, map, words | its code's own |
| `BellwetherLast` | animals, mood, state, words | its own: WithYoung |
| `MorelMourner` | needs, state, touch | a touch of Morel; its own: GriefEased |
| `MorelRing` | buildings, items, map, people, plants, state, words | nothing |
| `MorelThreads` | buildings, map, people, state, touch, words | a touch of Morel |
| `MorelBreath` | items, map, mood, needs, state, touch, words | a touch of Morel |
| `EnnuTest` | buildings, items, map, mood, needs, state, weather, words | nothing |
| `EnnuBowl` | buildings, map, state, touch, words | a touch of Ennu; its code's own |
| `EnnuTales` | buildings, map, skill, state, words | nothing |
| `EnnuKindest` | map | its code's own |
| `PipWontStop` | buildings, people, state, words | its own: FreeHour |
| `PipStars` | buildings, state, touch | a touch of Pip |
| `PipTurret` | mood, needs, state, words | its code's own |
| `PipSulk` | map, people, state, words | its code's own |
| `CirrusStar` | buildings, state, words | its code's own |
| `CirrusFall` | buildings | its code's own |
| `CirrusAurora` | people, state, weather, words | its code's own |
| `CirrusThunder` | people, state, weather, words | its own: Rain |
| `HushWord` | buildings, items, map, needs, state, words | its own: Find |
| `HushVigil` | items, map, mood, needs, state, words | its code's own |
| `HushEdge` | buildings, plants | its own: FrostLine |
| `HushGrave` | items, needs, people, state, words | its own: RestKin |
| `UndertowShun` | map, state, words | its code's own |
| `UndertowDrift` | health, map, mood, needs, state, words | its code's own |
| `UndertowEar` | health, items, map, people, state, words | its code's own |
| `HeliosLookBack` | buildings, map, needs, people, plants, state, words | its own: LongNoon |
| `HeliosFruit` | buildings, items, map, mood, needs, people, state, words | nothing |
| `HeliosDawn` | buildings, items, map, mood, needs, state, words | its own: Rested |
| `SeleneMorning` | map, people, state, words | nothing |
| `SeleneWater` | buildings, items, map, mood, needs, state, words | its own: Inspire |
| `SeleneWatch` | health, items, map, mood, needs, state, words | its code's own |
| `SeleneDarkFace` | buildings, map, mood, needs, state, threat, words | its own: NightWard |
| `MurmurSong` | buildings, map, mood, needs, people, state, words | its code's own |
| `MurmurGiveBack` | buildings, items, map, mood, needs, state, words | its code's own |
| `MurmurFrogs` | buildings, items, state, words | its code's own |
| `DucatTrail` | items, map, needs, state, words | its code's own |
| `DucatPurse` | goodwill, items, map, state, words | nothing |
| `DucatBeetle` | buildings, items, map, needs, state, words | its code's own |
| `MogThreshold` | health, items, map, mood, needs, state, words | its own: PainEased |
| `MogLostDead` | buildings, items, map, needs, state, touch, words | a touch of Mog |
| `RevelLongNight` | items, map, mood, needs, people, state, words | its code's own |
| `RevelVision` | buildings, items, map, needs, state, words | its code's own |
| `RevelCraving` | map, state, words | its code's own |
| `RevelStash` | buildings, items, map, state, words | its code's own |
| `RevelBinge` | items, needs, state, words | its own: BingeEnds |
| `RevelLetGo` | health, items, map, mood, state, words | its code's own |
| `RevelSatyr` | health, items, map, people, state, words | its own: Drink |
| `RevelCup` | buildings, health, items, map, needs, state, words | nothing |
| `HollinBound` | items, map, mood, plants, relations, state, words | its code's own |
| `HollinStumps` | items, map, mood, plants, relations, state, words | its code's own |
| `SmoulderPraise` | map, mood, relations, state, words | its code's own |
| `SmoulderNearMiss` | map, mood, relations, state, words | its code's own |
| `BellwetherTold` | map, mood, relations, state, words | its code's own |
| `BellwetherSpared` | buildings, map, mood, relations, state, words | its code's own |
| `MorelWasted` | buildings, map, mood, relations, state, words | its code's own |
| `MorelSealed` | map, mood, relations, state, words | its code's own |
| `EnnuWelcomed` | map, mood, relations, state, words | its code's own |
| `EnnuWaitedUp` | buildings, items, map, mood, relations, state, words | its code's own |
| `PipTwoHands` | buildings, map, mood, relations, state, words | its code's own |
| `PipTalked` | items, map, mood, relations, state, words | its code's own |
| `CirrusTwoWatchers` | items, map, mood, relations, state, words | its code's own |
| `CirrusCloudShape` | map, mood, relations, state, words | its code's own |
| `HushCoat` | items, map, mood, relations, state, weather, words | its code's own |
| `HushNoise` | map, mood, relations, state, weather, words | its code's own |
| `UndertowWhisper` | buildings, map, mood, relations, state, words | its code's own |
| `UndertowLampOut` | buildings, map, mood, relations, state, words | its code's own |
| `HeliosSun` | health, items, map, mood, relations, state, weather, words | its code's own |
| `HeliosAlike` | items, map, mood, relations, state, words | its code's own |
| `HeliosShade` | map, mood, relations, state, weather, words | its code's own |
| `SeleneSorry` | items, map, mood, relations, state, words | its code's own |
| `SeleneSameDream` | map, mood, relations, state, words | its code's own |
| `MogGraveCat` | items, map, mood, relations, state, words | its code's own |
| `MogRiddle` | items, map, mood, relations, state, words | its code's own |
| `MurmurSplash` | buildings, items, map, mood, relations, state, weather, words | its code's own |
| `MurmurFrogIn` | items, map, mood, relations, state, words | its code's own |
| `DucatPassed` | items, map, mood, relations, state, words | its code's own |
| `DucatMiser` | items, map, mood, relations, state, words | its code's own |
| `RevelRivalsCup` | map, mood, relations, state, words | its code's own |
| `RevelWager` | map, mood, relations, state, words | its code's own |
| `FaithDare` | buildings, items, map, mood, needs, relations, state, words | its code's own |
| `FaithCameThrough` | items, map, mood, relations, state, words | its code's own |
| `FaithSides` | map, mood, relations, state, words | its code's own |
| `EnnuBurying` | health, items, map, mood, relations, state, words | its code's own |
| `FaithLittleShrine` | buildings, items, map, mood, relations, state, words | its code's own |
| `FaithTurned` | items, map, mood, relations, state, words | its code's own |
| `PipWake` | buildings, map, mood, relations, state, words | its code's own |
| `FaithTakenOffering` | items, map, mood, relations, state, words | its code's own |
| `FaithBookRead` | buildings, map, mood, relations, state, words | its code's own |
| `FaithWeddingSign` | buildings, state, weather, words | its code's own |
| `FaithRaiderWord` | map, mood, relations, state, words | its code's own |
| `FaithGift` | items, map, mood, relations, state, words | its code's own |
| `FaithBargain` | items, map, mood, relations, state, words | its code's own |
| `FaithLightning` | buildings, health, map, mood, relations, state, weather, words | its code's own |
| `BellwetherNewborn` | health, items, map, people, state, weather, words | its code's own |
| `HeliosRipen` | buildings, items, map, people, plants, state, weather, words | its code's own |
| `MorelFoxfire` | buildings, items, state, weather, words | its code's own |
| `PipFinished` | items, map, people, state, words | its code's own |
| `SeleneTide` | buildings, items, people, state, words | its code's own |
| `CirrusGlory` | buildings, items, map, people, state, words | its code's own |

### The council's questions and the gods' events: thin answers

| Def | # | Answer | Carries |
|---|---|---|---|
| `Council_DlmA_BondFaith` | 1 | {A_nameDef} keeps {PET_nameDef}, and that's the end of it | mood, state |
| `Council_DlmA_BondFaith` | 2 | {PET_nameDef} keeps out of {B_nameDef}'s way | mood |
| `Council_DlmFaith_Evening` | 3 | Not now | state |
| `Council_DlmV_PreceptBroken` | 3 | The council stands by {A_nameDef} | mood, state |
| `Rimfolk_DlmR3_ScofferGrave` | 3 | Nothing said, as {DEAD} would have wanted | mood |
| `Rimfolk_DlmR4_Grace` | 3 | Each keeps their own thanks | mood |
| `Rimfolk_DlmY_Doubter` | 3 | It's {A_possessive} own affair | mood, state |
| `Rimfolk_Dlm_Path_Shrine` | 2 | A path of {A_possessive} own choosing | mood, state |
| `Rimfolk_Dlm_Path_Shrine` | 3 | Not yet | mood |
| `Rimfolk_Dlm_PlaceAsked` | 1 | Give {GOD} a place here | state, words |
| `Rimfolk_Dlm_PlaceAsked` | 2 | Not now | mood, state |
| `Rimfolk_Dlm_RivalFeasts` | 1 | {GOD}'s feast in full; {GOD2}'s kept quietly | state |
| `Rimfolk_Dlm_RivalFeasts` | 2 | {GOD2}'s feast in full; {GOD}'s kept quietly | state |
| `Rimfolk_Dlm_RivalFeasts` | 3 | Both in full | state |
| `Rimfolk_Dlm_Saint` | 3 | Let {DEAD} rest | state |
| `Rimfolk_Dlm_ScofferAtRite` | 3 | Let it be | state |
| `Rimfolk_Dlm_Temple` | 3 | Not now | state |
| `Rimfolk_Evt_GodHurtBeast` | 1 | Send {A_nameDef} to tend it | state |
| `Rimfolk_Evt_GodHurtBeast` | 2 | Leave it be | state |

### The signs: whose, and what follows each

| Sign | Whose (the gods' shapes) | What follows its sight |
|---|---|---|
| `Birds` | Cirrus | its own: AnimalsCalm |
| `Calm` | Helios, Selene, Murmur | its own: StillAir |
| `Dream` | Selene | its own: SameDream |
| `Candle` | Undertow | its own: FiresOut |
| `Restless` | any god's | its own: AnimalsCalm |
| `Drift` | Hollin | its own: Bloom |
| `AnimaHum` | Hollin | its own: AnimaHum |
| `DryadsStill` | Hollin | its own: AnimalsCalm |
| `Shadow` | Smoulder | its own: GameLeaves |
| `Steam` | Smoulder | its own: WarmGround |
| `Rumble` | Smoulder | its own: StoneChunks |
| `FairyRing` | Morel | its own: RichSoil |
| `Earth` | Morel, Murmur | its own: Rain |
| `Threads` | Morel | its own: KeepsLess |
| `Flicker` | Pip, Machine | its own: Charge |
| `Static` | Pip, Machine | its own: Trader |
| `Unpowered` | Pip, Machine | its own: StaysLit |
| `VastShadow` | Cirrus | its own: GameLeaves |
| `Star` | Cirrus | its own: Inspire |
| `FrostWriting` | Hush | its own: ColdRoom |
| `Silence` | Hush | its own: StillHour |
| `WhiteFigure` | Hush | its own: Sleepless |
| `Dark` | Undertow | its own: StaysDark |
| `Whisper` | Undertow | its own: Inspire |
| `Croak` | Murmur | its own: Fish |
| `StillSun` | Helios | its own: LongNoon |
| `HeatHaze` | Helios | its own: Warmth |
| `BigMoon` | Selene | its own: MoonSleep |
| `Glint` | Ducat | its own: Trader |
| `Beetle` | Ducat | its own: Silver |
| `Coin` | Ducat | its own: Coin |
| `CatsFacing` | Mog | its own: RatsGone |
| `GoldEyes` | Mog | its own: CatComes |
| `Sand` | Mog | its own: KindDream |
| `ThrumbosFace` | Bellwether | its own: AnimalsCalm |
| `GrassBowed` | Bellwether | its own: Game |
| `Hoofprint` | Bellwether | its own: Game |
| `Knock` | Ennu | its own: Sleepless |
| `Footprints` | Ennu | its own: Sleepless |
| `DogsSilent` | Ennu | its own: AnimalsCalm |
| `Bleat` | Revel | its own: WithYoung |
| `SweetSmoke` | Revel | its own: Cheered |
| `Spilled` | Revel | its own: Spill |
| `Moths` | Selene | its own: Inspire |
| `WaterRisen` | Murmur | its own: Bloom |
| `SunShaft` | Helios | its own: LongNoon |

### The touch: each god's effects by kind

- Hollin: Bless (health), Bloom (plants), Learn (skill), Ripen (plants), Healroot (plants), Inspire (needs), Linger (weather), Brambles dark (plants)
- Smoulder: Bless (health), Refuel (buildings), Learn (skill), Find (items), Inspire (needs), Vein (items), Linger (weather), FiresOut dark (buildings)
- Bellwether: Bless (health), Learn (skill), Game (animals), Inspire (needs), Herd (animals), Linger (weather), GameLeaves dark (animals)
- Morel: Bless (health), Learn (skill), Find (items), RichSoil (plants), Fertile (plants), Spoil dark (items)
- Ennu: Bless (health), Learn (skill), Visitors (people), Goodwill (goodwill), Trader (people), Inspire (needs), Wanderer (people), Linger (weather)
- Pip: Bless (health), Mend (buildings), Charge (buildings), Find (items), Inspire (needs), Linger (weather), Breakdown dark (buildings)
- Cirrus: Bless (health), ClearSky (weather), Aurora (weather), SkyGift (items), Inspire (needs), Linger (weather), Fog dark (weather)
- Hush: Bless (health), Snow (weather), Preserve (items), ColdBreak (weather), Hardy (health), FiresOut dark (buildings)
- Undertow: Bless (health), Inspire (needs), FiresOut dark (buildings), Vanish dark (items), Dim dark (weather)
- Helios: Bless (health), ClearSky (weather), Ripen (plants), Inspire (needs), SunYear (plants)
- Selene: Bless (health), Heal (health), Inspire (needs), ScarGone (health), Linger (weather), Sleepless dark (needs)
- Mog: Bless (health), RatsGone (animals), CatComes (animals), Inspire (needs), Ward (health)
- Murmur: Bless (health), Rain (weather), Ripen (plants), Fish (items), Spring (plants), Linger (weather), Fog dark (weather)
- Ducat: Bless (health), Find (items), Trader (people), Inspire (needs), Hoard (items), Linger (weather), Vanish dark (items)
- Revel: Bless (health), Merry (needs), Find (items), Inspire (needs), Linger (weather), Sour dark (items)

### The unseen rule in the words (to read by hand)

- names a god in a card's text: `CC_GW_Invite_Text` (Rimfolk_GodsWorld.xml): {PEOPLE} write from {TOWN} that they keep {FEAST} in a few days' time, and that the col...
- names a god in a card's text: `CC_GW_HolyWar_Text` (Rimfolk_GodsWorld.xml): {PEOPLE} write that their war with {ENEMY} is no war over land. It is {GOD}'s quarrel w...
- names a god in a card's text: `CC_GW_Envoy_Text` (Rimfolk_GodsWorld.xml): {ENVOY_nameDef}, an envoy of the temple of {GOD} among {PEOPLE}, has come to the colony...
- names a god in a card's text: `CC_Wsh_PlaceAgreed_Message` (Rimfolk_Worship.xml): Choose a place for {GOD}: an altar, a good sculpture or another place of worship, with ...
- a god's mind: `CC_GodO_BurnDream_Text_3` (Rimfolk_GodCards_Wordings.xml): {PAWN} dreamt of a warm voice deep under the mountain that sounded displeased, and woke...
- a god's mind: `CC_GodO_Saw_BurnDream` (Rimfolk_GodOwnHappenings.xml): {WHO} dreamt at {HOME} of a warm voice under the mountain that was not pleased with the...
- a god's mind: `CC_God_Word_Displeased` (Rimfolk_Gods.xml): displeased
- a god's mind: `CC_God_Word_Favoured` (Rimfolk_Gods.xml): favored
- a god's mind: `CC_God_Dev` (Rimfolk_Gods.xml): a test of favor (dev)
- a god's mind: `CC_God_NatureLine_Capricious` (Rimfolk_Gods.xml): Its favor swings both ways, fast, and fades fast; it hates to be bored and to be forgot...
- a god's mind: `CC_God_NatureLine_Grieving` (Rimfolk_Gods.xml): It mourns something, and favor comes slowly to it; the dead left lying and its days let...

<!-- gods_impact_audit: end -->
