# Review of Rimfolk's map events and the council's questions (28 November 2026)

The player, 28 November 2026: "regularly reviewing what you have already added to the games, all the different events,
visitor world maps, etc. Just testing whether everything works properly as intended, whether it actually has also impacts
on the game and it's not just flavor, whether it take the existing content like events and dilemmas and whatnot can be just
further enhanced. And then after, or also on the side, just add even more variety." The player chose to start with the
events and the council's questions; the standing rule of 27 November is more content for the existing mechanics, no new
mechanics.

Read: every Rimfolk event on the map (an IncidentDef with a HappeningExtension, 378 with answers; the personal stories',
missions' and storylines' own cards are their frames' and left out) and every council question (331), their answers, the
workers' `Resolved` code and the answers' effect classes; for the events and questions the audit flagged most, their
workers and effects in full. Camps and world events were reviewed at the same time by another reviewer and are left to
that review here.

The fixed rules are kept by `Tools/Tests/ReviewEventsCouncilTests.cs`.

## How it was measured

`Tools/impact_audit.py` reads every answer and sorts what it carries into what the game itself keeps (items and silver,
goodwill, skills, health, needs, work, research, relations, people, animals, buildings, threats), a follow-up booked by the
answer's code (an order collected later, a visit), a play-out on the map, Rimfolk's own numbers (the council's trust,
standards, a faction's memory, a camp's tension, a faith's certainty, a storyline's turn) and flavour (a mood, words, a
journal or chronicle line). An effect class without a fixed reading is read from its code: its `Apply` and what that calls,
three calls deep, matched against the game calls that do each thing; an event worker's `Resolved` adds what its code does to
the answers it names. It lists flavour-only and thin answers, events and questions whose every answer is so, answers whose
words promise something (silver, a meal, a lesson, goodwill, someone joining, a mending, a hunt, tending, a rest) that no
effect does, answers with no effect at all, and the questions where one answer is plainly best or worse by
`Tools/dilemma_balance.py`'s measure (dominated, or easy).

Run it again for a later review:

```
python3 Tools/impact_audit.py                    # the counts
python3 Tools/impact_audit.py --print flavour    # or thin, empty, allflavour, allthin, promises, balance, kinds
python3 Tools/impact_audit.py --filter Council_DlmL_ --print kinds
python3 Tools/impact_audit.py --write            # rewrites the measured part of this file, between its markers
```

The word list for promises is a hint to read by hand, not a verdict: most of its rows are words about something that
already happened or that someone else does ("supper as usual").

## Counts, before and after

| | Answers | Flavour-only | Thin | Empty | All flavour-only | All thin | Promises to read | Easy or dominated |
|---|---|---|---|---|---|---|---|---|
| Events, before | 1136 | 15 | 17 | 48 | 0 | 2 | 34 | 96 |
| Events, after | 1136 | 14 | 17 | 48 | 0 | 2 | 34 | 96 |
| Council's questions, before | 1067 | 99 | 191 | 1 | 1 | 6 | 76 | 69 |
| Council's questions, after | 1069 | 92 | 181 | 1 | 1 | 5 | 75 | 70 |

Read by hand, the counts say less than they seem to:

- **The events are in good shape.** Of the 14 flavour-only event answers, 11 are the "leave it" or "send them on" answer
  of an event whose other answers do something; the 48 empty answers are refusals ("Not this time", "Decline") whose
  event does its work elsewhere, or answers whose worker acts in code the scan can't name (a memo, a visit booked). No
  event has only flavour in its answers. The events' answers that looked thin (a visit sent away, a memo kept) were read in
  their workers and do what their words say: the gate's errands, the couriers' payments, the bills, the felling, the
  orders collected later. The two "all thin" events act in code the scan reads only as Rimfolk's own state: the wounded
  beast of the gods (`Rimfolk_Evt_GodHurtBeast`) is tended on the map by the one sent (OwnBellwether's tending step), and
  the wish waiting (`Rimfolk_Evt_WishWaiting`) promises the wish in the wishes' own frame.
- **Most thin council answers are the trade-off itself.** A question's "no" (not this year, leave it, the work comes first)
  costs moods and trust and keeps the colony's time and goods; a judgement between two people (who is right, who goes
  first) is about their moods, trust and ties by design. These were left: about two thirds of the 92 flavour-only
  answers are such refusals. The five "all thin" questions are of that kind too: the two wishes asked for the one
  thing in the stores (`Rimfolk_Dilemma_OneSteel`, which wish is promised first), a god's place and two gods' feasts (the
  gods' own book), and two of a world event's questions (keeping apart from travellers, a vow of vigil days kept each
  week); the only all-flavour question is the shared family name (`Council_DlmFam_SharedName`), names being flavour by the
  player's rule that the colony is the family.
- **What the measure can't see.** The balance measure doesn't count the time and goods a refusal saves beyond its
  play-outs, so a do-nothing default answer is often "dominated" by an active one. Changes that would have made an
  answer plainly worse by the measure were not kept (below, "Looked at and left").

## Fixed

Most serious first.

1. **A bell for the dead named no town** (`Peoples/PeoplesD.cs`, `HappeningWorker_BellForTheDead`). The Quiet Bell's
   sextons offer to carry the raiders' dead "to the grave-garden at {PLACE}", and its card and outcome name {PLACE} in
   every wording, but its worker took the plain undertakers' casting, which never sets a place: the card read "the
   grave-garden at ." and "led it away down the road toward ." Now the Bell's own town is named, as its other events name
   it (`LivingEvents.TownOf`, else the people's name).
2. **The slave freed could be walked to by someone drafted or in a break** (`Dilemmas/DilemmaWorkers_Variety.cs`,
   `DilemmaEffect_Emancipate`). With no warden at work, the colonist who asked was given the game's emancipation job
   whatever they were doing: drafted (taken out of the player's hands), in a mental break, a slave themselves, or unable to
   reach the slave. Now only one up to it is sent; otherwise the slave's emancipation stays set for the game's own wardens,
   as from the slave's tab.
3. **"Not for sale" promised a fine mood that never came** (`Council_Evt_TalentScout`). "{A} heard every word of it, and is
   in a fine mood all day", and the answer only moved trust. Now A feels it for the day (`Council_Evt_NotForSale`, +5, one
   day), only when the council chose the answer, as its trust already was.
4. **The creditor talked down left nothing behind** (`Council_Evt_Creditor`, "Let {A} talk them down"). When it went well
   the debt was forgiven with no effect at all. Now A feels the old debt settled (the paid answer's own memory) and the
   talking is practice at Social, either way it goes.

## Answers given real effects

Each keeps its words; the effect is what the words already say, kept small, and the answer stays a trade-off. The answer's
card shows it before the choice (the effects describe themselves).

Skill from the work the words give (a helper chosen for their skill, `Needs ... asB`, was never trained by it; now every one
is, and a test keeps it so):

| Question or event | Answer | Now |
|---|---|---|
| `Council_Dilemma_Goal_Purse` | Give the silver, and send our best haggler along | the haggler B: Social 200, either way |
| `Council_DlmL_EmptyChair` | Have a good talker tell {A} the whole of it | B: Social 200 (100 if it goes badly) |
| `Council_DlmL_BrokenPromise` | Have our best talker explain what happened | B: Social 200 (100) |
| `Council_Dilemma_VictoryFeast` | Have the best cook stretch half the food | the cook B: Cooking 300 (150) |
| `Council_DlmS_WantsToStay` | Ask our best talker to square it with {FACTION} | B: Social 250 |
| `Council_DlmS_RudeGuest` | Ask our best talker to settle it | B: Social 200 (100) |
| `Council_DlmS_Beggars` | Send them home with seed, and a grower's advice | the grower B: Plants 100 |
| `Council_DlmS_Tonic` | Have our doctor mix a sleeping draught | the doctor B: Medicine 200 |
| `Council_DlmL_CampLostChild` | Send our best tracker to search | the tracker B: Animals 200 (100) |
| `Council_Evt_TreasureMap` | Have it checked first, then buy | B: Intellectual 200, either way |
| `Council_Evt_LostCargo` | Send it back, and ask for a finder's fee | B: Social 150 |
| `Council_Evt_Journalist` | Give the tour, with {A} doing the talking | A: Social 250 (150) |
| `Council_Evt_OldEnemyPeace` | Make peace, and ask weregild (a good talker) | B: Social 250 (150) |
| `Council_Evt_QuietStranger` | Let {A} talk to {X} first | A: Social 200 |
| `Council_Evt_PawnedKeepsake` | Have {B} haggle for it | B: Social 200 (100) |

What the words say happens to someone:

| Question or event | Answer | The words | Now |
|---|---|---|---|
| `Council_DlmLv_WorrierWatch` | The walls are fine. Get some sleep. | "lay awake listening anyway" | A short on sleep |
| `Council_DlmLv_ChildGrief` | Time will do it | "goes to bed early these days, and lies awake" | A short on sleep |
| `Council_DlmLv_Frightened` | Children get over these things | "is still awake at night" | A short on sleep |
| `Council_Dilemma_Omen` | Tell {A} to keep it quiet | "They still come, and {A} still wakes from them" | A short on sleep |
| `Rimfolk_DlmR3_CryingNight` | Babies cry | up every night, "went back to it" | A short on sleep |
| `Rimfolk_DlmR3_LightSleeper` | A shared room is a shared room | woken every night, "went back to it" | A short on sleep |
| `Rimfolk_DlmR4_NightOwl` | The colony keeps one day | "works half asleep all day" | A short on sleep |
| `Council_Dilemma_Music` | Ban the night music | "{B} slept fourteen hours straight" | B slept like a stone (+4% work, a day) |
| `Council_DlmLv_OldWayNewWay` | The new way: let {B} try it | B's quicker way, "delighted" | B motivated (+4% work, two or three days) |
| `Council_DlmLv_ElderDoctor` | Respect {A}'s wishes | "{B} has taken to leaving remedies where {A} will find them" | A's illness eased a little (0.04; seeing the doctor eases 0.1 to 0.12) |
| `Council_DlmLv_Schooling` | They learn by working beside us | "{A} spent the day following the builders about" | A: Construction 250 |
| `Council_DlmLv_Mischief` | Children break things. Let it go. | "{B} did the mending, muttering" | B: Construction 150 |
| `Rimfolk_Evt_FirstPiece` | {A} tells {B} so, between the two of them | "told {B} the {THING} was good work, and why" | the maker B: 200 xp at its craft |

What the words say is done, now done on the map:

| Question | Answer | The words | Now |
|---|---|---|---|
| `Council_DlmR_FriendAtTheGate` | The rule is the rule | "{A} has passed a bowl of stew and a blanket over the wall" | A carries a meal out to the friend at the gate |
| `Council_DlmR_Sanctuary` | A night's rest, then the road | "a loaf of the colony's bread into {X}'s pack" | a meal carried out to them for the road |
| `Council_DlmS_WantsToStay` | {X} leaves with {X}'s people | "found a fence to fix on the far side of the colony" | A goes and does the colony's mending (a work party of one) |
| `Council_DlmLv_ElderWalk` | It's too far; stay where it's safe | "went to sit by the wall, and looked at the hills all afternoon" | A goes and sits at the gate |
| `Council_Dilemma_TreeSinging` | Trees don't listen | "went out alone after dark and sat a long while with {A}'s back to the old tree" | A goes out alone after dark |

## Answers added

Where a question had only weak answers, an answer in the frames there are, in the council questions' flavour style:

- **An animal in the kitchen** (`Council_Dilemma_PetKitchen`), whose three answers were moods and trust: "{A} teaches
  {PET} to wait at the door". A spends the evening at the kitchen door with the animal (played out), learns Animals
  (300), and both meet halfway; it may not take (the risk lowered by Animals skill), and then the cook is overruled.
- **The good meals** (`Rimfolk_DlmE_GoodMeals`), with two answers: "Keep them, and {A} shows {B} how they're made". The
  meals stay in the stores, and the cook gives B a cooking lesson at the stove that evening (played out).

## Checked and found sound

- Every {PLACE}, {X_}, {FACTION}, {THING}, {SKILL}, {BEAST}, {TALE}, {DEAD}, {ENEMY}, {RELATION}, {PET_}, {ILLNESS}, {GOAL},
  {SECRET} and {QUIRK} an event or question uses against what its worker fills in (the one gap: the Bell, fixed above).
- Answers that reach B, X, a faction or a skill the worker never sets; mod defs looked up without `SilentFail` (none in
  this area); every hediff an answer gives ends on its own; every memory an answer gives has a length and a mood; the
  roles' thoughts exist; default answers that spend goods spend only a little (a meal, wood for a lantern).
- Workers' `Resolved` code that acts when the council didn't answer (none with a cost); jobs given in events and questions
  (all guarded but the emancipation, fixed above); harm in events and questions (through `Harm`, or to a stranger who is
  the event, such as the sick and wounded the sickhouse brings).
- Every event's chance and refire (every one has a refire, none fires far too often).

## Looked at and left

- **Refusals that are only a mood.** "Not this year", "Leave it", "The work comes first": the colony keeps its time and
  goods, and the answer's cost is the mood and trust it says. Left by design.
- **Changes tried and taken back**, because they made an answer plainly worse by the balance measure: a short-on-sleep
  for "The work won't wait" (`Council_DlmR_DayOfRest`; it cost as much work as the day off), lower resistance for "Show
  {X} the colony's other side" (`Council_DlmR_CountingOnMercy`; harsher treatment isn't how resistance falls in the game,
  and it made the plain answer dominated), a work party for the grower going out alone (`Rimfolk_DlmE_HarvestHands`; they
  harvest anyway), and a walk to the wall for "Whoever's awake can watch it" (`Council_Dilemma_SunsReturn`).
- **Words with no existing effect to match**: a cot in the storeroom (`Council_DlmL_Crowded`), the colony eating less
  (`Council_DlmA_FamineCull`), the spoils shared out by need (`Council_Dilemma_Spoils`), a bed apart for the sick
  (`Council_DlmL_Quarantine`), separate shifts (`Council_Dilemma_Soc_SwornRivals`): each would need a new mechanic.
- **Camps and world events** (the other review's area): "Welcome them: fight together" (`Council_Evt_WarCamp`) and "Let
  them stay" (`Council_Evt_Camp`) have no effect of their own here (their camp's code may carry them); the camp's song
  taught at a feast (`Council_Evt_CampFeast`). Left to that review.
- **A radio answer that sends nobody away** (`Rimfolk_Evt_FleetShuttle`, "Not today"): its `SendAway` has nobody to send
  and does nothing, which is right. Left.
- **A regular buyer's order taken while the buyer turned hostile** (`Council_Evt_RegularBuyer`): if the faction stops
  being friendly in the day the council takes to answer, "Take the order" books nothing. Very rare; left.
- **The new answer for the animal in the kitchen dominates the default** ("Let them sort it out") by the measure: the
  do-nothing answer, with its risk of a fight, was meant to be the worst. Left.
- **Personal stories', missions' and storylines' cards**: their frames act on the choice (the storyline's next part, a
  mission's delivery); not read here.

<!-- impact_audit: begin -->

Measured by `python3 Tools/impact_audit.py --write` (this part between the markers is rewritten each run; the review's
own notes are outside them).

| | Defs | Answers | Flavour-only | Thin (moods and trust) | Empty | All flavour-only | All thin | Promises to read | Easy or dominated |
|---|---|---|---|---|---|---|---|---|---|
| events | 378 | 1136 | 14 | 17 | 48 | 0 | 2 | 34 | 96 |
| council's questions | 331 | 1069 | 92 | 181 | 1 | 1 | 5 | 75 | 70 |

Flavour-only: only a mood, words, a journal or chronicle line. Thin: only those and Rimfolk's own numbers (trust,
standards, the colony's ways, a faction's memory, a camp's tension). Empty: no effect and no worker code at all (a refusal
that does nothing, or an answer whose work the event's code does elsewhere). Promises: a word list's hint, read by hand.

What the answers carry (answers with at least one effect of the kind):

| Kind | Events | Questions |
|---|---|---|
| items | 355 | 154 |
| goodwill | 298 | 110 |
| skill | 119 | 107 |
| health | 19 | 105 |
| needs | 37 | 75 |
| work | 29 | 94 |
| research | 3 | 1 |
| relations | 17 | 105 |
| people | 82 | 81 |
| animals | 13 | 26 |
| buildings | 75 | 38 |
| threat | 8 | 0 |
| later | 89 | 15 |
| map | 949 | 570 |
| state | 413 | 667 |
| mood | 223 | 956 |
| words | 67 | 124 |
| journal | 0 | 11 |
| chronicle | 1 | 12 |
| unknown | 0 | 0 |

### Events and questions whose every answer is flavour-only

- question `Council_DlmFam_SharedName` (Rimfolk_Dilemmas_Family.xml)

### Events and questions whose every answer is thin

- event `Rimfolk_Evt_GodHurtBeast` (Rimfolk_GodOwnHappenings.xml)
- event `Rimfolk_Evt_WishWaiting` (Rimfolk_Events_ColonyLife.xml)
- question `Rimfolk_Dilemma_OneSteel` (Rimfolk_Dilemmas_CouncilLife.xml)
- question `Rimfolk_Dlm_PlaceAsked` (Rimfolk_Worship_Places.xml)
- question `Rimfolk_Dlm_RivalFeasts` (Rimfolk_Worship_Places.xml)
- question `Rimfolk_Dlm_WS_Apart` (Rimfolk_Dilemmas_WorldHome.xml)
- question `Rimfolk_Dlm_WS_Vigil` (Rimfolk_Dilemmas_WorldHome.xml)

### Flavour-only answers

| Def | # | Answer | Carries | File |
|---|---|---|---|---|
| `Council_Evt_VendettaBand` | 2 | Let them pass | mood | Council_Events_Living.xml |
| `Council_Evt_Creditor` | 3 | Refuse | nothing | Council_Events_Pasts.xml |
| `Council_Evt_OldComrade` | 2 | Turn {X_objective} away | mood | Council_Events_Pasts.xml |
| `Council_Evt_WoundedStranger` | 3 | Leave {X_objective} there | mood | Council_Events_Storyteller.xml |
| `Council_Evt_ChildFind` | 2 | Let {A_objective} keep it | mood | Council_Events_Storyteller_Home.xml |
| `Council_Evt_OldMap` | 2 | Keep it as a keepsake | mood | Council_Events_Storyteller_Home.xml |
| `Council_Evt_StruckTree` | 3 | Leave it standing | mood | Council_Events_Storyteller_Land.xml |
| `Council_Evt_Stray` | 1 | It can stay | mood | Council_Events_Visitors.xml |
| `Rimfolk_Evt_GroupEveningOff` | 2 | The work comes first this week | mood | Rimfolk_Events_ColonyLife.xml |
| `Rimfolk_Evt_QuarrelAtSupper` | 2 | Keep them apart for a few days | mood | Rimfolk_Events_ColonyLife.xml |
| `Rimfolk_Evt_StormVow` | 3 | The work comes first | mood | Rimfolk_Events_ColonyLife.xml |
| `Council_Evt_PW_RareSighting` | 3 | Let it pass | mood | Rimfolk_Events_Mods_PokeWorld.xml |
| `Council_Evt_OrphanedYoung` | 3 | Leave it be | mood | Rimfolk_Events_New.xml |
| `Council_Evt_VendettaRansom` | 3 | Some things aren't for sale | mood | Rimfolk_Events_VendettaEnds.xml |
| `Council_Dilemma_YoungHunter` | 3 | Not until {A_nameDef} is older | mood | Council_Dilemmas3.xml |
| `Council_Dilemma_SunsReturn` | 2 | Whoever's awake can watch it | mood | Council_Dilemmas_Customs2.xml |
| `Council_DlmH_MidwifeApprentice` | 2 | Not now | mood | Council_Dilemmas_Health.xml |
| `Council_DlmL_ChildQuestion` | 4 | That's a question for when you're older | mood | Council_Dilemmas_Life.xml |
| `Council_DlmL_Crowded` | 2 | Supper in two sittings | mood | Council_Dilemmas_Life.xml |
| `Council_DlmL_Crowded` | 4 | Everyone is crowded | mood | Council_Dilemmas_Life.xml |
| `Council_DlmL_FrictionGrown` | 2 | Separate chores, separate tables | mood | Council_Dilemmas_Life.xml |
| `Council_DlmL_FrictionGrown` | 4 | Leave them to it | mood | Council_Dilemmas_Life.xml |
| `Council_DlmL_SameMeal` | 3 | Food is food | mood | Council_Dilemmas_Life.xml |
| `Council_DlmL_SameWork` | 3 | Rotate everyone's chores once a week | mood | Council_Dilemmas_Life.xml |
| `Council_DlmL_SickCook` | 2 | Let {A_nameDef} cook, with clean hands and a cloth over t... | mood | Council_Dilemmas_Life.xml |
| `Council_Dilemma_PetKitchen` | 3 | Let them sort it out | mood | Council_Dilemmas_Pets.xml |
| `Council_Dilemma_Midwinter` | 3 | Just another night | mood | Council_Dilemmas_Seasons.xml |
| `Council_Dilemma_Spoils` | 2 | Share everything equally | mood | Council_Dilemmas_Seasons.xml |
| `Council_DlmS_BeastReturns` | 2 | Keep everyone inside | mood | Council_Dilemmas_Strangers.xml |
| `Council_DlmS_NemesisInChains` | 2 | Let {X_objective} rot in there | mood | Council_Dilemmas_Strangers.xml |
| `Council_DlmS_PredatorDen` | 3 | Leave it alone | mood | Council_Dilemmas_Strangers.xml |
| `Council_DlmS_PrisonerPlea` | 3 | Keep the door locked | mood | Council_Dilemmas_Strangers.xml |
| `Council_DlmS_Tonic` | 3 | Keep the silver | mood | Council_Dilemmas_Strangers.xml |
| `Council_Dilemma_LambingWatch` | 3 | The herd can manage | mood | Rimfolk_Dilemmas_Animals.xml |
| `Council_Dilemma_ShearingDay` | 3 | No time this year | mood | Rimfolk_Dilemmas_Animals.xml |
| `Council_DlmA_BondFaith` | 2 | {PET_nameDef} keeps out of {B_nameDef}'s way | mood | Rimfolk_Dilemmas_Animals.xml |
| `Council_DlmA_PenStalker` | 4 | Leave it be | mood | Rimfolk_Dilemmas_Animals.xml |
| `Council_Dilemma_HerdHome` | 3 | They'll find their own way in | mood | Rimfolk_Dilemmas_Animals2.xml |
| `Council_Dilemma_FirstSupper` | 3 | Not this year | mood | Rimfolk_Dilemmas_Customs_ColonyLife.xml |
| `Rimfolk_Dilemma_EldersDay` | 3 | Not this year | mood | Rimfolk_Dilemmas_Customs_LifeVariety.xml |
| `Rimfolk_Dilemma_GivingDay` | 3 | Not this year | mood | Rimfolk_Dilemmas_Customs_LifeVariety.xml |
| `Council_Dilemma_AutumnHunt` | 3 | No hunt this year | mood | Rimfolk_Dilemmas_Customs_New.xml |
| `Council_Dilemma_BoundsWalk` | 3 | Not this year | mood | Rimfolk_Dilemmas_Customs_New.xml |
| `Council_Dilemma_FishingMatch` | 3 | Not now | mood | Rimfolk_Dilemmas_Customs_New.xml |
| `Council_Dilemma_SpringCleaning` | 3 | Not this year | mood | Rimfolk_Dilemmas_Customs_New.xml |
| `Council_Dilemma_StarNight` | 3 | Bed as usual | mood | Rimfolk_Dilemmas_Customs_New.xml |
| `Council_Dilemma_TellingTheBees` | 3 | It isn't done here | mood | Rimfolk_Dilemmas_Customs_New.xml |
| `Rimfolk_DlmE_ChildAndBeast` | 3 | Not yet | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_DoctorsRounds` | 3 | The doctoring stays with {A_nameDef} | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_GoodMeals` | 2 | Keep them for the hard days | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_HarvestHands` | 3 | The growers in their own time | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_HerdHands` | 3 | The herd stays with {A_nameDef} | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_KitchenHelp` | 3 | The kitchen stays {A_nameDef}'s | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_NameNewborn` | 3 | It doesn't need a name yet | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_StormNight` | 3 | Storms pass | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_TravellersNews` | 3 | The news will keep | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_VisitorSupper` | 3 | They stay where they are | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_WarmDay` | 3 | The sowing comes first | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_WhoseBeast` | 3 | {PET_nameDef} stays with the herd | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_YoungVoice` | 3 | {A_nameDef} can wait {A_possessive} turn | mood | Rimfolk_Dilemmas_Everyday.xml |
| `Council_DlmFam_SharedName` | 1 | {A_nameDef} takes the {LASTB} name | chronicle, journal, words | Rimfolk_Dilemmas_Family.xml |
| `Council_DlmFam_SharedName` | 2 | {B_nameDef} takes the {LASTA} name | chronicle, journal, words | Rimfolk_Dilemmas_Family.xml |
| `Council_DlmFam_SharedName` | 3 | Each keeps their own name | chronicle, journal, words | Rimfolk_Dilemmas_Family.xml |
| `Council_DlmGuest_Invite` | 2 | {X_nameDef} stays a guest for now | mood | Rimfolk_Dilemmas_Guests.xml |
| `Council_DlmLv_ElderAndChild` | 3 | Children belong with children | mood | Rimfolk_Dilemmas_Lives.xml |
| `Council_DlmLv_ElderTale` | 3 | Another time | mood | Rimfolk_Dilemmas_Lives.xml |
| `Council_DlmLv_OldWayNewWay` | 1 | The old way: {A_nameDef} has done it longer | mood | Rimfolk_Dilemmas_Lives.xml |
| `Council_DlmLv_RecordKeeper` | 2 | Keep it, but keep it private | mood | Rimfolk_Dilemmas_Lives.xml |
| `Council_DlmLv_TeenRoad` | 1 | Let {A_nameDef} go with the next journey out | mood | Rimfolk_Dilemmas_Lives.xml |
| `Council_Dilemma_ChildTrees` | 3 | Trees take room from the crops | mood | Rimfolk_Dilemmas_Lives_Customs.xml |
| `Council_Dilemma_ChildrensRace` | 3 | No time for races this summer | mood | Rimfolk_Dilemmas_Lives_Customs.xml |
| `Council_Dilemma_Gleaning` | 3 | The animals can have the stubble | mood | Rimfolk_Dilemmas_Lives_Customs.xml |
| `Council_DlmV_CellLessons` | 3 | No contact with the prisoner | mood | Rimfolk_Dilemmas_New.xml |
| `Council_DlmV_NewLimb` | 3 | Keep it until there's one for each | mood | Rimfolk_Dilemmas_New.xml |
| `Rimfolk_Dilemma_SacredDay` | 3 | Not this year | mood | Rimfolk_Dilemmas_SacredDay.xml |
| `Rimfolk_DlmE_GoodWeapon` | 3 | It stays in the stores | mood | Rimfolk_Dilemmas_Variety2.xml |
| `Rimfolk_DlmE_MasterGone` | 3 | {PET_nameDef} is the colony's animal now | mood | Rimfolk_Dilemmas_Variety2.xml |
| `Rimfolk_DlmE_HurtBeast` | 3 | Leave it to the wild | mood | Rimfolk_Dilemmas_Variety3.xml |
| `Rimfolk_DlmE_NewRoom` | 3 | Whoever sleeps in it first | mood | Rimfolk_Dilemmas_Variety3.xml |
| `Rimfolk_DlmE_NextStudy` | 3 | Leave it for now | mood | Rimfolk_Dilemmas_Variety3.xml |
| `Rimfolk_Dlm_WS_Apart` | 2 | Greet them as always | mood | Rimfolk_Dilemmas_WorldHome.xml |
| `Rimfolk_Dlm_WS_FairDay` | 2 | Not this year | mood | Rimfolk_Dilemmas_WorldHome.xml |
| `Rimfolk_Dlm_WS_Mourning` | 2 | Leave the colony's clothes as they are | mood | Rimfolk_Dilemmas_WorldHome.xml |
| `Rimfolk_Dlm_WS_Sides` | 3 | Keep out of it | mood | Rimfolk_Dilemmas_WorldHome.xml |
| `Rimfolk_Dlm_WS_Vigil` | 2 | The work comes first | mood | Rimfolk_Dilemmas_WorldHome.xml |
| `Rimfolk_Dlm_WS_FallingStars` | 2 | The work starts early tomorrow | mood | Rimfolk_Dilemmas_WorldVariety.xml |
| `Rimfolk_Dlm_NewAge_Industrial` | 3 | Let it pass quietly | mood | Rimfolk_Round2_Council.xml |
| `Rimfolk_Dlm_NewAge_Medieval` | 3 | Let it pass quietly | mood | Rimfolk_Round2_Council.xml |
| `Rimfolk_Dlm_NewAge_Spacer` | 3 | Let it pass quietly | mood | Rimfolk_Round2_Council.xml |
| `Rimfolk_Dlm_Ordeal` | 2 | The council judges by what it knows | mood | Rimfolk_Round2_Council.xml |
| `Rimfolk_Dlm_Path_Shrine` | 3 | Not yet | mood | Rimfolk_Round2_Council.xml |
| `Rimfolk_DlmR3_AfterTantrum` | 3 | A bad day, and no more | mood | Rimfolk_Round3_Council.xml |
| `Rimfolk_DlmR3_AnimalGrave` | 3 | An animal is an animal | mood | Rimfolk_Round3_Council.xml |
| `Rimfolk_DlmR3_HiveMeat` | 3 | Keep it for a hungry day | mood | Rimfolk_Round3_Council.xml |
| `Rimfolk_DlmR3_HungryGap` | 3 | No one goes before anyone else | mood | Rimfolk_Round3_Council.xml |
| `Rimfolk_DlmR3_OurVerse` | 3 | Leave the verses to {NAME} | mood | Rimfolk_Round3_Council.xml |
| `Rimfolk_DlmR3_ScofferGrave` | 3 | Nothing said, as {DEAD} would have wanted | mood | Rimfolk_Round3_Council.xml |
| `Rimfolk_DlmR3_VisitorFriend` | 3 | Guests come and go | mood | Rimfolk_Round3_Council.xml |
| `Rimfolk_DlmR4_ColdCell` | 3 | The cell stays as it is | mood | Rimfolk_Round4_Council.xml |
| `Rimfolk_DlmR4_DryGrass` | 3 | The grass can stay | mood | Rimfolk_Round4_Council.xml |
| `Rimfolk_DlmR4_Grace` | 3 | Each keeps their own thanks | mood | Rimfolk_Round4_Council.xml |
| `Rimfolk_DlmR4_PetBed` | 3 | Animals sleep where animals sleep | mood | Rimfolk_Round4_Council.xml |
| `Rimfolk_DlmR4_SeatAway` | 3 | The seat waits for {A_nameDef} | mood | Rimfolk_Round4_Council.xml |
| `Rimfolk_DlmR4_SlaveRest` | 3 | The work is theirs | mood | Rimfolk_Round4_Council.xml |
| `Rimfolk_DlmR4_Stone` | 3 | The fields first | mood | Rimfolk_Round4_Council.xml |
| `Rimfolk_DlmR4_VisitingMaster` | 3 | Guests are left to their business | mood | Rimfolk_Round4_Council.xml |

### Thin answers (moods and Rimfolk's numbers only)

| Def | # | Answer | Carries | File |
|---|---|---|---|---|
| `Rimfolk_Evt_GodHurtBeast` | 1 | Send {A_nameDef} to tend it | state | Rimfolk_GodOwnHappenings.xml |
| `Rimfolk_Evt_GodHurtBeast` | 2 | Leave it be | state | Rimfolk_GodOwnHappenings.xml |
| `Council_Evt_TalentScout` | 2 | Let {A_nameDef} decide | mood, state | Council_Events_More.xml |
| `Council_Evt_TalentScout` | 3 | Not for sale | mood, state | Council_Events_More.xml |
| `Council_Evt_LostKin` | 2 | Send {X_objective} on | mood, state | Council_Events_Pasts.xml |
| `Council_Evt_OldFlame` | 2 | Not here | state | Council_Events_Pasts.xml |
| `Council_Evt_Prodigal` | 2 | Refuse | state | Council_Events_Pasts.xml |
| `Council_Evt_After_Reckoning` | 3 | The colony owes {PLACE} nothing | mood, state | Rimfolk_Events_Aftermath.xml |
| `Rimfolk_Evt_PromiseSore` | 3 | Leave it | state | Rimfolk_Events_ColonyLife.xml |
| `Rimfolk_Evt_StoresShort` | 3 | Wait and see | state | Rimfolk_Events_ColonyLife.xml |
| `Rimfolk_Evt_WishWaiting` | 1 | Promise it | state | Rimfolk_Events_ColonyLife.xml |
| `Rimfolk_Evt_WishWaiting` | 2 | Tell {A_nameDef} it will have to wait | state | Rimfolk_Events_ColonyLife.xml |
| `Rimfolk_Evt_WishWaiting` | 3 | Say nothing | state | Rimfolk_Events_ColonyLife.xml |
| `Council_Evt_PW_ShinyRumour` | 1 | Keep an eye out for it | state | Rimfolk_Events_Mods_PokeWorld.xml |
| `Council_Evt_WildAtPens` | 2 | Let it be | state | Rimfolk_Events_New.xml |
| `Rimfolk_Evt_RedInk` | 2 | Send {A_nameDef} to argue the entry | state | Rimfolk_Events_PeopleEvents3.xml |
| `Council_Evt_VendettaRansom` | 2 | Let {A_nameDef} decide | mood, state, words | Rimfolk_Events_VendettaEnds.xml |
| `Council_Dilemma_Feast` | 2 | Save the food | mood, state | Council_Dilemmas.xml |
| `Council_Dilemma_Feud` | 4 | Stay out of it | mood, state | Council_Dilemmas.xml |
| `Council_Dilemma_Thief` | 1 | Let {A_nameDef} off with a warning | mood, state | Council_Dilemmas.xml |
| `Council_Dilemma_Duel` | 2 | Forbid it | mood, state | Council_Dilemmas2.xml |
| `Council_Dilemma_Hoarder` | 2 | Let {A_nameDef} keep it | mood, state | Council_Dilemmas2.xml |
| `Council_Dilemma_Music` | 2 | Set quiet hours after midnight | mood, state | Council_Dilemmas2.xml |
| `Council_Dilemma_OldFriends` | 4 | We need it more than they do | mood, state | Council_Dilemmas2.xml |
| `Council_Dilemma_Seat` | 4 | Let {A_nameDef} sit in on one meeting | mood, state | Council_Dilemmas2.xml |
| `Council_Dilemma_Slacker` | 4 | Stay out of it | mood, state | Council_Dilemmas2.xml |
| `Council_Dilemma_Wager` | 1 | Make {B_nameDef} pay up | mood, state | Council_Dilemmas2.xml |
| `Council_Dilemma_Wager` | 3 | Ban gambling in the colony | mood, state | Council_Dilemmas2.xml |
| `Council_Dilemma_Watch` | 2 | Warn {B_nameDef} privately | mood, state | Council_Dilemmas2.xml |
| `Council_Dilemma_Credit` | 4 | Credit everyone who fought | mood, state | Council_Dilemmas3.xml |
| `Council_Dilemma_LetterHome` | 3 | No: a message can be followed back | mood, state | Council_Dilemmas3.xml |
| `Council_Dilemma_OwnFuneral` | 4 | Say nothing and hope it passes | mood, state | Council_Dilemmas3.xml |
| `Council_Dilemma_PetFading` | 4 | Let {PET_nameDef} be | mood, state | Council_Dilemmas3.xml |
| `Council_Dilemma_PetPot` | 1 | {PET_nameDef} is family | mood, state | Council_Dilemmas3.xml |
| `Council_Dilemma_SideDeal` | 4 | Let the deal stand | mood, state | Council_Dilemmas3.xml |
| `Council_Dilemma_Sleepwalker` | 3 | Tie a bell to {A_nameDef}'s ankle | mood, state | Council_Dilemmas3.xml |
| `Council_Dilemma_WhereTheDeadGo` | 4 | Change the subject | mood, state | Council_Dilemmas3.xml |
| `Council_Dilemma_WildFriend` | 4 | Say nothing | mood, state | Council_Dilemmas3.xml |
| `Council_Dilemma_Path_Medicine` | 3 | Let {A_nameDef} choose | mood, state | Council_Dilemmas_ComingOfAge.xml |
| `Council_Dilemma_Path_Scholar` | 3 | Let {A_nameDef} choose | mood, state | Council_Dilemmas_ComingOfAge.xml |
| `Council_Dilemma_Path_Warrior` | 2 | Not yet | mood, state | Council_Dilemmas_ComingOfAge.xml |
| `Council_Dilemma_Path_Warrior` | 3 | Let {A_nameDef} choose | mood, state | Council_Dilemmas_ComingOfAge.xml |
| `Council_Dilemma_FirstFrostSoup` | 3 | Everything goes to the winter stores | mood, state | Council_Dilemmas_Customs.xml |
| `Council_Dilemma_HarvestDance` | 3 | There's work in the morning | mood, state | Council_Dilemmas_Customs.xml |
| `Council_Dilemma_LanternNight` | 3 | Save the fuel for the cold | mood, state | Council_Dilemmas_Customs.xml |
| `Council_Dilemma_MidsummerSwim` | 3 | Keep everyone working | mood, state | Council_Dilemmas_Customs.xml |
| `Council_Dilemma_AncestorsSupper` | 3 | The dead don't need feeding | mood, state | Council_Dilemmas_Customs2.xml |
| `Council_Dilemma_HighWatch` | 3 | We can see enough from here | mood, state | Council_Dilemmas_Customs2.xml |
| `Council_Dilemma_MarshLights` | 3 | Keep the wood for the fires | mood, state | Council_Dilemmas_Customs2.xml |
| `Council_Dilemma_NightVigil` | 3 | Everyone needs their sleep | mood, state | Council_Dilemmas_Customs2.xml |
| `Council_Dilemma_RainWelcome` | 3 | It's only rain | mood, state | Council_Dilemmas_Customs2.xml |
| `Council_Dilemma_SunsReturn` | 3 | The sun comes back without our help | mood, state | Council_Dilemmas_Customs2.xml |
| `Council_Dilemma_WaterBlessing` | 3 | Bread is for eating | mood, state | Council_Dilemmas_Customs2.xml |
| `Council_Dilemma_Goal_Caravan` | 1 | Ask {A_nameDef} to stay | mood, state | Council_Dilemmas_Goals.xml |
| `Council_Dilemma_Goal_Caravan` | 4 | Let {A_nameDef} decide alone | mood, state | Council_Dilemmas_Goals.xml |
| `Council_Dilemma_Goal_Excused` | 3 | The chores come first | mood, state | Council_Dilemmas_Goals.xml |
| `Council_Dilemma_Goal_Foolish` | 2 | Hear {B_nameDef} out | mood, state | Council_Dilemmas_Goals.xml |
| `Council_Dilemma_Goal_Foolish` | 4 | Stay out of it | mood, state | Council_Dilemmas_Goals.xml |
| `Council_Dilemma_Goal_GivingUp` | 3 | There's no shame in letting it go | mood, state | Council_Dilemmas_Goals.xml |
| `Council_Dilemma_Goal_GivingUp` | 4 | Tell {A_nameDef} to toughen up | mood, state | Council_Dilemmas_Goals.xml |
| `Council_Dilemma_Goal_Mentor` | 3 | That's between the two of them | mood, state | Council_Dilemmas_Goals.xml |
| `Council_Dilemma_Goal_Purse` | 3 | Not from the colony's purse | mood, state | Council_Dilemmas_Goals.xml |
| `Council_Dilemma_Goal_Remember` | 3 | A private promise from the council | mood, state | Council_Dilemmas_Goals.xml |
| `Council_Dilemma_Goal_Remember` | 4 | Don't talk like that | mood, state | Council_Dilemmas_Goals.xml |
| `Council_Dilemma_Goal_TwoDreams` | 3 | Split everything down the middle | mood, state | Council_Dilemmas_Goals.xml |
| `Council_DlmH_FamilySay` | 1 | Go ahead as planned | mood, state | Council_Dilemmas_Health.xml |
| `Council_DlmL_BrokenPromise` | 4 | The council owes nothing for it | mood, state | Council_Dilemmas_Life.xml |
| `Council_DlmL_Crowded` | 1 | Give {A_nameDef} a corner of the storeroom | mood, state | Council_Dilemmas_Life.xml |
| `Council_DlmL_ElderRest` | 4 | Not yet; we need every hand | mood, state | Council_Dilemmas_Life.xml |
| `Council_DlmL_EmptyChair` | 1 | Hear {A_nameDef} out at the next meeting | mood, state | Council_Dilemmas_Life.xml |
| `Council_DlmL_EmptyChair` | 2 | What was decided stands | mood, state | Council_Dilemmas_Life.xml |
| `Council_DlmL_EmptyChair` | 3 | From now on, meetings wait for those at work | mood, state | Council_Dilemmas_Life.xml |
| `Council_DlmL_Quarantine` | 1 | Move {A_nameDef} to a bed apart | mood, state | Council_Dilemmas_Life.xml |
| `Council_DlmL_Quarantine` | 2 | Keep {A_nameDef} where {A_pronoun} is | mood, state | Council_Dilemmas_Life.xml |
| `Council_DlmL_SameWork` | 1 | Give {A_nameDef} a week of something different | mood, state | Council_Dilemmas_Life.xml |
| `Council_DlmL_SameWork` | 4 | Someone has to do it | mood, state | Council_Dilemmas_Life.xml |
| `Council_DlmH_GuestWantsToStay` | 4 | No | mood, state | Council_Dilemmas_Mods_Hospitality.xml |
| `Council_Dilemma_PetKitchen` | 1 | Keep {PET_nameDef} out of the kitchen | mood, state | Council_Dilemmas_Pets.xml |
| `Council_Dilemma_PetKitchen` | 2 | The kitchen is {PET_nameDef}'s too | mood, state | Council_Dilemmas_Pets.xml |
| `Council_DlmR_CarryingEveryone` | 1 | Everyone pulls their weight | mood, state | Council_Dilemmas_Reputation.xml |
| `Council_DlmR_CountingOnMercy` | 1 | Show {X_objective} the colony's other side | mood, state | Council_Dilemmas_Reputation.xml |
| `Council_DlmR_DayOfRest` | 3 | The work won't wait | mood, state | Council_Dilemmas_Reputation.xml |
| `Council_DlmR_OldCustom` | 3 | Let the old ways rest | mood, state | Council_Dilemmas_Reputation.xml |
| `Council_DlmR_OneMercy` | 3 | Let them talk | mood, state | Council_Dilemmas_Reputation.xml |
| `Council_DlmR_SkipCustom` | 1 | The custom stands, for everyone | mood, state | Council_Dilemmas_Reputation.xml |
| `Council_DlmR_SkipCustom` | 2 | Let {A_nameDef} choose | mood, state | Council_Dilemmas_Reputation.xml |
| `Council_DlmR_StingyMockery` | 2 | Let {X_objective} laugh | mood, state | Council_Dilemmas_Reputation.xml |
| `Council_Dilemma_HarvestHome` | 2 | Store every scrap for winter | mood, state | Council_Dilemmas_Seasons.xml |
| `Council_Dilemma_Midsummer` | 2 | Save the wood this year | mood, state | Council_Dilemmas_Seasons.xml |
| `Council_Dilemma_NameTheYear` | 1 | The year of {A_nameDef} | mood, state | Council_Dilemmas_Seasons.xml |
| `Council_Dilemma_NameTheYear` | 3 | The year of the muffalo | mood, state | Council_Dilemmas_Seasons.xml |
| `Council_Dilemma_Spoils` | 1 | {A_nameDef} earned first pick | mood, state | Council_Dilemmas_Seasons.xml |
| `Council_Dilemma_Spoils` | 4 | Let the quartermaster share it out | mood, state | Council_Dilemmas_Seasons.xml |
| `Council_Dilemma_Secret_BloodPast` | 1 | Keep {A_nameDef} under watch | mood, state | Council_Dilemmas_Secrets.xml |
| `Council_Dilemma_Secret_BloodPast` | 3 | What's past is past | mood, state | Council_Dilemmas_Secrets.xml |
| `Council_Dilemma_Secret_DoctorsHands` | 1 | {A_nameDef} keeps doctoring, as before | mood, state | Council_Dilemmas_Secrets.xml |
| `Council_Dilemma_Secret_KindnessFound` | 2 | Thank {A_objective} in private | mood, state | Council_Dilemmas_Secrets.xml |
| `Council_Dilemma_Secret_LooseTongue` | 1 | Order {B_nameDef} to drop the subject | mood, state | Council_Dilemmas_Secrets.xml |
| `Council_Dilemma_Secret_LooseTongue` | 4 | It's out. People will talk | mood, state | Council_Dilemmas_Secrets.xml |
| `Council_Dilemma_Secret_OneMoreBout` | 3 | No fighting. The pits are behind {A_objective} | mood, state | Council_Dilemmas_Secrets.xml |
| `Council_Dilemma_Secret_Sickness` | 1 | A room apart, until it's certain | mood, state | Council_Dilemmas_Secrets.xml |
| `Council_Dilemma_Secret_Sickness` | 3 | No quarantine, no fuss | mood, state | Council_Dilemmas_Secrets.xml |
| `Council_Dilemma_Secret_UsingAgain` | 3 | Let {A_nameDef} handle it alone | mood, state | Council_Dilemmas_Secrets.xml |
| `Council_Dilemma_Secret_WhatCameOut` | 2 | The subject is closed | mood, state | Council_Dilemmas_Secrets.xml |
| `Council_Dilemma_Secret_WhatCameOut` | 3 | Let {A_nameDef} decide | mood, state | Council_Dilemmas_Secrets.xml |
| `Council_Dilemma_Soc_ClosedCircle` | 4 | Let people choose their own friends | mood, state | Council_Dilemmas_Social.xml |
| `Council_Dilemma_Soc_CoverUp` | 4 | Let it go | mood, state | Council_Dilemmas_Social.xml |
| `Council_Dilemma_Soc_Favoritism` | 3 | Make {B_nameDef} keep the ledger | mood, state | Council_Dilemmas_Social.xml |
| `Council_Dilemma_Soc_Favoritism` | 4 | Hand out the good things by lot | mood, state | Council_Dilemmas_Social.xml |
| `Council_Dilemma_Soc_Inseparable` | 3 | Keep the roster as it is | mood, state | Council_Dilemmas_Social.xml |
| `Council_Dilemma_Soc_LeftBehind` | 4 | Leave them to it | mood, state | Council_Dilemmas_Social.xml |
| `Council_Dilemma_Soc_Remembrance` | 4 | No. The stores are for the living | mood, state | Council_Dilemmas_Social.xml |
| `Council_Dilemma_Soc_SwornRivals` | 2 | Keep them apart: separate shifts, separate meals | mood, state | Council_Dilemmas_Social.xml |
| `Council_Dilemma_Soc_SwornRivals` | 4 | Tell them to grow up | mood, state | Council_Dilemmas_Social.xml |
| `Council_Dilemma_Soc_WhoseWork` | 4 | Credit the colony as a whole | mood, state | Council_Dilemmas_Social.xml |
| `Council_DlmS_ChildPet` | 3 | No, and that's final | mood, state | Council_Dilemmas_Strangers.xml |
| `Council_DlmS_FallenPlea` | 3 | Leave {X_objective} where {X_pronoun} lies | mood, state | Council_Dilemmas_Strangers.xml |
| `Council_DlmS_Healer` | 3 | Keep the silver | mood, state | Council_Dilemmas_Strangers.xml |
| `Council_DlmS_HeirloomOffer` | 3 | It stays with {A_nameDef} | mood, state | Council_Dilemmas_Strangers.xml |
| `Council_DlmS_KinInChains` | 3 | Treat {X_objective} like any other prisoner | mood, state | Council_Dilemmas_Strangers.xml |
| `Council_DlmS_OldAcquaintance` | 2 | Let {X_objective} talk | mood, state | Council_Dilemmas_Strangers.xml |
| `Council_DlmS_RudeGuest` | 1 | Demand an apology | mood, state | Council_Dilemmas_Strangers.xml |
| `Council_DlmS_RudeGuest` | 2 | Let it go, they're guests | mood, state | Council_Dilemmas_Strangers.xml |
| `Council_DlmA_BondFaith` | 1 | {A_nameDef} keeps {PET_nameDef}, and that's the end of it | mood, state | Rimfolk_Dilemmas_Animals.xml |
| `Council_DlmA_FamineCull` | 2 | Tighten everyone's belts | mood, state | Rimfolk_Dilemmas_Animals.xml |
| `Council_DlmA_ResidentBeast` | 5 | Let it be | state | Rimfolk_Dilemmas_Beast.xml |
| `Rimfolk_DlmC_FeeShort` | 4 | The terms are the terms | state | Rimfolk_Dilemmas_Camps.xml |
| `Rimfolk_DlmC_Festival` | 4 | Not this year | state | Rimfolk_Dilemmas_Camps.xml |
| `Rimfolk_DlmC_Playmates` | 3 | Keep the children apart | mood, state | Rimfolk_Dilemmas_Camps.xml |
| `Rimfolk_Dilemma_Path_Animals` | 3 | Let {A_nameDef} choose | mood, state | Rimfolk_Dilemmas_ComingOfAge_More.xml |
| `Rimfolk_Dilemma_Path_Building` | 3 | Let {A_nameDef} choose | mood, state | Rimfolk_Dilemmas_ComingOfAge_More.xml |
| `Rimfolk_Dilemma_Path_Fields` | 3 | Let {A_nameDef} choose | mood, state | Rimfolk_Dilemmas_ComingOfAge_More.xml |
| `Rimfolk_Dilemma_Path_Kitchen` | 3 | Let {A_nameDef} choose | mood, state | Rimfolk_Dilemmas_ComingOfAge_More.xml |
| `Rimfolk_Dilemma_OneSteel` | 1 | First come, first served: {A_nameDef}'s | mood, state, words | Rimfolk_Dilemmas_CouncilLife.xml |
| `Rimfolk_Dilemma_OneSteel` | 2 | {B_nameDef}'s first | mood, state, words | Rimfolk_Dilemmas_CouncilLife.xml |
| `Rimfolk_Dilemma_OneSteel` | 3 | Neither until there's more {THING} | mood, state, words | Rimfolk_Dilemmas_CouncilLife.xml |
| `Rimfolk_Dilemma_StepDown` | 3 | Ask {A_nameDef} to stay on | mood, state | Rimfolk_Dilemmas_CouncilLife.xml |
| `Rimfolk_Dlm_GraveLights` | 3 | The dead are at peace | mood, state | Rimfolk_Dilemmas_Customs_YearFour.xml |
| `Rimfolk_Dlm_MendingDay` | 3 | No day of its own for it | mood, state | Rimfolk_Dilemmas_Customs_YearFour.xml |
| `Rimfolk_Dlm_WinterTales` | 3 | The year is best forgotten | mood, state | Rimfolk_Dilemmas_Customs_YearFour.xml |
| `Rimfolk_Dlm_Woodpile` | 3 | The stores have wood enough | mood, state | Rimfolk_Dilemmas_Customs_YearFour.xml |
| `Rimfolk_DlmE_BoredAbed` | 3 | Rest is rest | mood, state | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_Broom` | 3 | The floors are for whoever minds them | mood, state | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_ColdBedroom` | 3 | {A_nameDef} will manage | mood, state | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_DawnToDark` | 3 | {A_nameDef} knows {A_possessive} own limits | mood, state | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_ElderChair` | 3 | The stool has done this long | mood, state | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_FriendsFellOut` | 3 | Let them cool off | mood, state | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_OldFriendLeftOut` | 3 | {A_nameDef} will get over it | mood, state | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_OnlyOneKnows` | 2 | {A_nameDef} keeps the craft to {A_objective}self | mood, state | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_OnlyOneKnows` | 3 | Leave it to the two of them | mood, state | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_OwnPatch` | 3 | Every row belongs to the colony | mood, state | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_Seconds` | 3 | One more helping won't break the colony | mood, state | Rimfolk_Dilemmas_Everyday.xml |
| `Rimfolk_DlmE_Sickroom` | 3 | It will get its turn | mood, state | Rimfolk_Dilemmas_Everyday.xml |
| `Council_DlmFaith_Evening` | 3 | Not now | state | Rimfolk_Dilemmas_Faith.xml |
| `Rimfolk_Dlm_HerdCount` | 1 | Keep them all | mood, state | Rimfolk_Dilemmas_HerdCount.xml |
| `Rimfolk_Dilemma_KinSendHome` | 2 | The colony can't spare it now | state | Rimfolk_Dilemmas_KinSendHome.xml |
| `Council_DlmLv_ElderCold` | 4 | Everyone's cold. {A_nameDef} will manage. | mood, state | Rimfolk_Dilemmas_Lives.xml |
| `Council_DlmLv_Mischief` | 4 | A stern word from the council | mood, state | Rimfolk_Dilemmas_Lives.xml |
| `Council_DlmLv_TeenRoad` | 2 | Not this year | mood, state | Rimfolk_Dilemmas_Lives.xml |
| `Council_DlmV_BadBatch` | 3 | Blame the meat | mood, state | Rimfolk_Dilemmas_New.xml |
| `Council_DlmV_DrugBench` | 3 | Keep making them | mood, state | Rimfolk_Dilemmas_New.xml |
| `Council_DlmV_HeatWave` | 3 | Carry on | mood, state | Rimfolk_Dilemmas_New.xml |
| `Council_DlmV_PreceptBroken` | 3 | The council stands by {A_nameDef} | mood, state | Rimfolk_Dilemmas_New.xml |
| `Council_DlmV_SlaveFreedom` | 3 | Not now | mood, state | Rimfolk_Dilemmas_New.xml |
| `Council_DlmA_ResidentPokemon` | 4 | Let it be | state | Rimfolk_Dilemmas_Pokemon.xml |
| `Rimfolk_Dilemma_SeatHalfway` | 3 | Leave it to the vote | state | Rimfolk_Dilemmas_SeatHalfway.xml |
| `Rimfolk_DlmE_Blight` | 3 | Let it run its course | mood, state | Rimfolk_Dilemmas_Variety2.xml |
| `Rimfolk_DlmE_ColdSnap` | 3 | Carry on | mood, state | Rimfolk_Dilemmas_Variety2.xml |
| `Rimfolk_DlmE_Inspired` | 3 | The work comes first | mood, state | Rimfolk_Dilemmas_Variety3.xml |
| `Rimfolk_DlmE_OldWalls` | 3 | Leave them as they stand | mood, state | Rimfolk_Dilemmas_Variety3.xml |
| `Rimfolk_DlmE_Spoiling` | 3 | Let it go | mood, state | Rimfolk_Dilemmas_Variety3.xml |
| `Rimfolk_DlmE_StoresInRain` | 3 | Leave it to the weather | mood, state | Rimfolk_Dilemmas_Variety3.xml |
| `Rimfolk_DlmE_Withdrawal` | 3 | It's {A_possessive} own business | mood, state | Rimfolk_Dilemmas_Variety3.xml |
| `Rimfolk_Dlm_WS_Apart` | 1 | Keep apart from travellers until it passes | mood, state | Rimfolk_Dilemmas_WorldHome.xml |
| `Rimfolk_Dlm_WS_Vigil` | 1 | Grant the vigil days | mood, state | Rimfolk_Dilemmas_WorldHome.xml |
| `Rimfolk_DlmY_Doubter` | 3 | It's {A_possessive} own affair | mood, state | Rimfolk_Dilemmas_YearFour.xml |
| `Rimfolk_DlmY_EmptyField` | 3 | The growers will get to it | mood, state | Rimfolk_Dilemmas_YearFour.xml |
| `Rimfolk_DlmY_HerdTooBig` | 3 | Keep them all | mood, state | Rimfolk_Dilemmas_YearFour.xml |
| `Rimfolk_DlmY_HomeFromRoad` | 3 | Back to work in the morning | mood, state | Rimfolk_Dilemmas_YearFour.xml |
| `Rimfolk_DlmY_NoGuide` | 3 | The faithful can manage | mood, state | Rimfolk_Dilemmas_YearFour.xml |
| `Rimfolk_DlmY_SnowPaths` | 3 | It will melt | mood, state | Rimfolk_Dilemmas_YearFour.xml |
| `Rimfolk_DlmY_TrainBeast` | 3 | Let {PET_objective} be | mood, state | Rimfolk_Dilemmas_YearFour.xml |
| `Rimfolk_DlmY_TreeLine` | 3 | The trees stay | mood, state | Rimfolk_Dilemmas_YearFour.xml |
| `Rimfolk_DlmY_Unfinished` | 3 | The builders will get there | mood, state | Rimfolk_Dilemmas_YearFour.xml |
| `Rimfolk_DlmY_WhichCrop` | 3 | The field stays as it is | mood, state | Rimfolk_Dilemmas_YearFour.xml |
| `Rimfolk_Dlm_Path_Shrine` | 2 | A path of {A_possessive} own choosing | mood, state | Rimfolk_Round2_Council.xml |
| `Rimfolk_DlmR3_Forgetting` | 3 | {A_nameDef} keeps on as before | mood, state | Rimfolk_Round3_Council.xml |
| `Rimfolk_DlmR3_NoMeatTable` | 3 | The colony's pot is the colony's pot | mood, state | Rimfolk_Round3_Council.xml |
| `Rimfolk_DlmR3_PickingDay` | 3 | The work comes first | mood, state | Rimfolk_Round3_Council.xml |
| `Rimfolk_DlmR4_DecreeToll` | 3 | The decree is for the whole colony | mood, state | Rimfolk_Round4_Council.xml |
| `Rimfolk_Dlm_Saint` | 3 | Let {DEAD} rest | state | Rimfolk_Saints.xml |
| `Rimfolk_Dlm_Temple` | 3 | Not now | state | Rimfolk_Worship_Dilemmas.xml |
| `Rimfolk_Dlm_ScofferAtRite` | 3 | Let it be | state | Rimfolk_Worship_Dilemmas_More.xml |
| `Rimfolk_Dlm_PlaceAsked` | 1 | Give {GOD} a place here | state, words | Rimfolk_Worship_Places.xml |
| `Rimfolk_Dlm_PlaceAsked` | 2 | Not now | mood, state | Rimfolk_Worship_Places.xml |
| `Rimfolk_Dlm_RivalFeasts` | 1 | {GOD}'s feast in full; {GOD2}'s kept quietly | state | Rimfolk_Worship_Places.xml |
| `Rimfolk_Dlm_RivalFeasts` | 2 | {GOD2}'s feast in full; {GOD}'s kept quietly | state | Rimfolk_Worship_Places.xml |
| `Rimfolk_Dlm_RivalFeasts` | 3 | Both in full | state | Rimfolk_Worship_Places.xml |

### Answers with no effect at all

| Def | # | Answer | Carries | File |
|---|---|---|---|---|
| `Rimfolk_Evt_GodRiddle` | 3 | Let her lie there | nothing | Rimfolk_GodsOnMap_Mog.xml |
| `Council_Evt_Deserter` | 2 | Send {X_objective} on | nothing | Council_Events_Battles.xml |
| `Council_Evt_Camp` | 1 | Let them stay | nothing | Council_Events_Camps.xml |
| `Council_Evt_WarCamp` | 1 | Welcome them: fight together | nothing | Council_Events_Camps.xml |
| `Council_Evt_TreasureMap` | 2 | No, thank you | nothing | Council_Events_Challenges.xml |
| `Council_Evt_FarMarket` | 2 | Not this time | nothing | Council_Events_Living.xml |
| `Council_Evt_HarvestFair` | 2 | Not this year | nothing | Council_Events_Living.xml |
| `Council_Evt_RegularBuyer` | 2 | Not this time | nothing | Council_Events_Living.xml |
| `Council_Evt_NotableGuest` | 2 | Decline politely | nothing | Council_Events_Living_Mods.xml |
| `Council_Evt_RareCreature` | 2 | Leave it be | nothing | Council_Events_Living_Mods.xml |
| `Council_Evt_OldEnemyPeace` | 4 | Send the envoy away | nothing | Council_Events_Remembered.xml |
| `Council_Evt_Envoy` | 3 | Send the envoy away | nothing | Council_Events_Storyteller.xml |
| `Council_Evt_Signal` | 3 | Leave it | nothing | Council_Events_Storyteller.xml |
| `Council_Evt_OldCommander` | 2 | Send good wishes only | nothing | Council_Events_Storyteller_Home.xml |
| `Council_Evt_BulkOrder` | 2 | Turn it down | nothing | Council_Events_Variety.xml |
| `Council_Evt_Elopers` | 3 | Turn them away | nothing | Council_Events_Variety.xml |
| `Council_Evt_NeighborsSick` | 3 | We can't spare it | nothing | Council_Events_Variety.xml |
| `Council_Evt_OldGrave` | 3 | Leave it be | nothing | Council_Events_Variety.xml |
| `Council_Evt_OldLetters` | 3 | Put them away unread | nothing | Council_Events_Variety.xml |
| `Council_Evt_Apprentice` | 2 | Send {X_objective} home | nothing | Council_Events_Visitors.xml |
| `Council_Evt_QuietStranger` | 2 | Not without answers | nothing | Council_Events_Visitors.xml |
| `Rimfolk_Evt_ChildsQuestion` | 3 | Later, when there's time | nothing | Rimfolk_Events_ColonyLife.xml |
| `Rimfolk_Evt_FirstPiece` | 3 | No fuss | nothing | Rimfolk_Events_ColonyLife.xml |
| `Rimfolk_Evt_HerdNight` | 2 | The animal will manage | nothing | Rimfolk_Events_ColonyLife.xml |
| `Rimfolk_Evt_LastField` | 3 | The growers can manage | nothing | Rimfolk_Events_ColonyLife.xml |
| `Rimfolk_Evt_NewcomerAtEdge` | 3 | Let {A_nameDef} find {A_possessive} own way | nothing | Rimfolk_Events_ColonyLife.xml |
| `Rimfolk_Evt_OldSong` | 3 | Not tonight | nothing | Rimfolk_Events_ColonyLife.xml |
| `Council_Evt_HospitalPatient` | 3 | A word of advice, nothing more | nothing | Rimfolk_Events_Hospital.xml |
| `Council_Evt_PW_ShinyRumour` | 3 | Just a story | nothing | Rimfolk_Events_Mods_PokeWorld.xml |
| `Council_Evt_Commission` | 3 | Turn it down | nothing | Rimfolk_Events_New.xml |
| `Council_Evt_RichSeam` | 3 | Leave it in the rock for now | nothing | Rimfolk_Events_New.xml |
| `Council_Evt_TableGame` | 3 | Not today | nothing | Rimfolk_Events_New.xml |
| `Rimfolk_Evt_EnclaveLander` | 2 | Not today | nothing | Rimfolk_Events_PeoplesC.xml |
| `Rimfolk_Evt_HerdCrossing` | 3 | Let them cross | nothing | Rimfolk_Events_Variety2.xml |
| `Rimfolk_Evt_Ambrosia` | 3 | Leave it on the bush | nothing | Rimfolk_Events_Variety3.xml |
| `Rimfolk_Evt_Clutch` | 3 | Leave them where they lie | nothing | Rimfolk_Events_Variety3.xml |
| `Rimfolk_Evt_FenceDown` | 2 | Let them graze outside a while | nothing | Rimfolk_Events_Variety3.xml |
| `Rimfolk_Evt_WildOne` | 2 | Let {X_objective} be | nothing | Rimfolk_Events_Variety3.xml |
| `Rimfolk_Evt_RegularDied` | 3 | Thank {X_nameDef} for the word | nothing | Rimfolk_Events_VisitorsMore2.xml |
| `Rimfolk_Evt_FishRunning` | 3 | Let them pass | nothing | Rimfolk_Round2_Events.xml |
| `Rimfolk_Evt_WaterForFields` | 3 | Wait it out | nothing | Rimfolk_Round2_Events.xml |
| `Rimfolk_Evt_R3ColdRoom` | 3 | Leave things as they are | nothing | Rimfolk_Round3_Events.xml |
| `Rimfolk_Evt_R3FieldDinner` | 3 | Leave it as it is | nothing | Rimfolk_Round3_Events.xml |
| `Rimfolk_Evt_R3MarshPath` | 3 | Keep wading | nothing | Rimfolk_Round3_Events.xml |
| `Rimfolk_Evt_R3WornClothes` | 3 | Wear them through the winter | nothing | Rimfolk_Round3_Events.xml |
| `Rimfolk_Evt_R4EWashingDay` | 3 | Not today | nothing | Rimfolk_Round4_Events.xml |
| `Council_Mission_RunawayTamed` | 3 | Keep {BEAST} without paying | nothing | Council_Missions.xml |
| `Rimfolk_Mission_DisputeJudgement` | 4 | Send them off without a judgement | nothing | Rimfolk_Missions_Home.xml |
| `Rimfolk_DlmFealty_Renounce` | 2 | Keep the oath | nothing | Rimfolk_Dilemmas_Fealty.xml |

### Words that promise what the effects don't do (to read by hand)

| Def | # | Promise | Words | Carries |
|---|---|---|---|---|
| `Council_Evt_After_SpringReturn` | 3 | silver | Tell them the debt is long paid {A_nameDef} sends the seed back with them for their own fields. | goodwill, state |
| `Council_Evt_BountyHunters` | 1 | a hunt | The hunters will be here within hours. | mood, state, threat |
| `Council_Evt_BountyHunters` | 3 | a hunt | The hunters bind {A_possessive} hands, and within the hour they're gone. | mood, people |
| `Council_Evt_CampFeast` | 2 | a lesson | Before the evening was out the camp had taught the colony one of its songs. | map, mood, needs, state |
| `Council_Evt_Champion` | 1 | silver | The match starts in a couple of hours, and word of the silver is already going around. | later, map |
| `Council_Evt_DeserterFound` | 3 | a meal | Supper tonight will be a quiet one. | goodwill, mood, people |
| `Council_Evt_FamilyVisit` | 1 | a meal | Ask {X_objective} to stay {X_nameDef} thinks it over through supper and says yes. | later, people |
| `Council_Evt_HospitalPatient` | 3 | tending | The medicine stays on the shelf. | nothing |
| `Council_Evt_NeighborsSick` | 3 | tending | We can't spare it The colony keeps its medicine. | nothing |
| `Council_Evt_OldDebt` | 3 | silver | Tell them they owe nothing {A_nameDef} tells {X_nameDef} the colony did what anyone should have, and won't ... | goodwill, state |
| `Council_Evt_Rematch` | 1 | silver | The rematch is on, and the silver is counted out on a barrel head where everyone can see it. | later, map |
| `Council_Evt_VendettaRansom` | 3 | silver | Some things aren't for sale {X_nameDef} is told the colony doesn't sell its dead, and takes the silver home... | mood |
| `Council_Evt_WantedPrisoner` | 3 | silver | Keep {X_objective} here The colony keeps its prisoner, and {FACTION} keep their silver, and a grudge. | goodwill |
| `Rimfolk_Evt_ArtHandedOn` | 1 | a lesson | Let {A_nameDef} learn it {A_nameDef} will spend the afternoon with {X_nameDef} at the gate. | map, state |
| `Rimfolk_Evt_ArtHandedOn` | 2 | a lesson | Let {A_nameDef} learn it, and give {X_nameDef} a gift in return {A_nameDef} will spend the afternoon with {... | items, map, state |
| `Rimfolk_Evt_BondGiven` | 1 | a lesson | They will work through every task it can learn this afternoon. | map, state |
| `Rimfolk_Evt_BreakDrought` | 2 | a lesson | Have them bring the rain, and send {A_nameDef} to keep the night with them ({COUNT} silver) The storm-walke... | items, map, state |
| `Rimfolk_Evt_DeepSeam` | 2 | a lesson | Have {X_objective} mark it, and send {A_nameDef} to walk the ground with {X_objective} ({COUNT} silver) {X_... | items, map, state |
| `Rimfolk_Evt_GravshipLog` | 2 | a lesson | Let them come down, and teach {A_nameDef} the ship The Fleet's officers will come down near the {TALE}, wal... | map |
| `Rimfolk_Evt_KinForCaptive` | 2 | silver | Let {TALE} go with them, and keep their silver The colony would not take their silver. | goodwill, map, state |
| `Rimfolk_Evt_LearningTraded` | 1 | a lesson | Host them, and let {A_nameDef} teach them each afternoon The copyist and the technician will stay {COUNT} d... | map |
| `Rimfolk_Evt_MindShouts` | 2 | a lesson | Let {A_nameDef} sit with them and learn how {X_nameDef} and {A_nameDef} go to {B_nameDef} and sit beside {B... | map, state |
| `Rimfolk_Evt_OldSoldier` | 1 | someone joins | Take {X_nameDef} in {X_nameDef} joins the colony. | map, state |
| `Rimfolk_Evt_OldSoldier` | 2 | someone joins | Take {X_nameDef} in, and buy out the musters ({DEAD} silver) {X_nameDef} joins the colony, and the Banner w... | items, map, state |
| `Rimfolk_Evt_PilgrimShelter` | 1 | someone joins | Take them in for the night The pilgrims were given a warm corner each indoors for the night, and {A_nameDef... | goodwill, later, map |
| `Rimfolk_Evt_QuarrelAtSupper` | 2 | a meal | Keep them apart for a few days {A_nameDef} and {B_nameDef} are kept apart at meals and at work for a few days. | mood |
| `Rimfolk_Evt_RedInk` | 3 | silver | The league will give the colony worse terms until it pays. | map, state |
| `Rimfolk_Evt_SickRoom` | 2 | a lesson | Give {X_objective} {PLACE}, {B_nameDef} on the rounds too The sick are going to {PLACE}, and {B_nameDef} wi... | items, map, state |
| `Rimfolk_Evt_StormSense` | 2 | silver | Pay with a vial of our own storm, caught at our rod {B_nameDef} and {X_nameDef} are walking out to the colo... | goodwill, map, state |
| `Rimfolk_Evt_SummerSchool` | 1 | a lesson | The school will camp on the colony's land, and the lessons can be asked for from its page in the afternoons. | map, state |
| `Rimfolk_Evt_SummerSchool` | 3 | a lesson | Not this summer {X_nameDef} thanks the colony and goes on to look for another place to teach. | map |
| `Rimfolk_Evt_Vet` | 1 | a lesson | {X_nameDef} went from animal to animal, tending the hurt and the sick where they lay, and then trained {PET... | items, map, state |
| `Rimfolk_Evt_Vet` | 2 | a lesson | {X_nameDef} went from animal to animal, tending the hurt and the sick where they lay, and then trained {PET... | items, map, state |
| `Rimfolk_Evt_WorkThatStays` | 2 | a lesson | Let them build it, and send {A_nameDef} to work beside them The three journeymen will camp by the colony, a... | map, state |
| `Council_Dilemma_AncestorsSupper` | 3 | a meal | The dead don't need feeding Supper as usual. | mood, state |
| `Council_Dilemma_BoundsWalk` | 3 | a meal | {A_nameDef} didn't say much at supper. | mood |
| `Council_Dilemma_Feast` | 2 | a meal | Save the food {A_nameDef} has gone back to plain meals, and sets each bowl down harder than it needs to. | mood, state |
| `Council_Dilemma_FirstFrostSoup` | 3 | a meal | {A_nameDef} made the usual meals and said nothing about it. | mood, state |
| `Council_Dilemma_Goal_Remember` | 4 | a meal | {A_nameDef} tried twice more at dinner, was told not to be morbid both times, and gave up. | mood, state |
| `Council_Dilemma_HarvestDance` | 3 | a meal | There's work in the morning {A_nameDef} hummed the dance tune under {A_possessive} breath all through suppe... | mood, state |
| `Council_Dilemma_PetBite` | 4 | an animal | Let it go {PET_nameDef} goes unpunished, to {A_nameDef}'s relief. | mood, relations, state |
| `Council_Dilemma_Secret_Bounty` | 3 | a hunt | Refuse, and tell them the gate stays shut {A_nameDef} belongs to this colony, the council told the hunters,... | health, mood, state |
| `Council_Dilemma_Secret_DoctorsHands` | 1 | tending | {A_nameDef} keeps doctoring, as before {A_nameDef} keeps the medicine shelf, with the council's blessing. | mood, state |
| `Council_Dilemma_Secret_LooseTongue` | 1 | a meal | The looks across the dinner table haven't stopped. | mood, state |
| `Council_Dilemma_Secret_Sickness` | 1 | a meal | A room apart, until it's certain {A_nameDef} has a room apart now, with meals left at the door and visitors... | mood, state |
| `Council_Dilemma_Secret_WhatCameOut` | 2 | a meal | People still talk when {A_nameDef} isn't in the room, but {A_nameDef} can eat dinner in peace. | mood, state |
| `Council_Dilemma_Soc_Accusation` | 1 | someone leaves | Search {A_nameDef}'s things The council turned out {A_nameDef}'s belongings with a crowd watching and found... | mood, relations, state |
| `Council_Dilemma_Soc_Accusation` | 3 | silver | {B_nameDef} has started keeping a ledger of the silver, and stares at {A_nameDef} while writing in it. | mood, relations, state |
| `Council_Dilemma_Soc_ClosedCircle` | 4 | a meal | {B_nameDef} has been saying so warmly, and {A_nameDef} ate dinner alone again. | mood, state |
| `Council_Dilemma_Soc_CoverUp` | 4 | an animal | Let it go No one was punished. | mood, state |
| `Council_Dilemma_Soc_Inseparable` | 3 | a meal | {A_nameDef} and {B_nameDef} still find each other at meals, and glare at the roster on the way past. | mood, state |
| `Council_Dilemma_Soc_LeftBehind` | 4 | a meal | Leave them to it {A_nameDef} has new friends now, and {B_nameDef} has an empty seat beside {B_objective} at... | mood, state |
| `Council_Dilemma_Soc_SwornRivals` | 2 | a meal | Keep them apart: separate shifts, separate meals {A_nameDef} works the mornings now and {B_nameDef} the aft... | mood, state |
| `Council_Dilemma_Soc_WhoseWork` | 4 | a meal | {A_nameDef} and {B_nameDef} both sulked through supper, at opposite ends of the table. | mood, state |
| `Council_Dilemma_Spoils` | 4 | an animal | {A_nameDef} got the bow {A_pronoun} wanted and let the rest go without a fuss. | mood, state |
| `Council_Dilemma_Thief` | 2 | silver | Make {A_nameDef} work it off {A_nameDef} is pulling extra shifts until the stores are paid back, sack for s... | health, mood, state |
| `Council_Dilemma_TreeSinging` | 1 | a lesson | Everyone out to sing The colony sat in a ring round the old tree all afternoon, singing the songs {A_nameDe... | map, mood, state |
| `Council_Dilemma_Wager` | 1 | silver | Make {B_nameDef} pay up The debt stands. | mood, state |
| `Council_Dilemma_Watch` | 4 | an animal | Let it go this time The matter was dropped. | health, mood, state |
| `Council_Dilemma_WaterBlessing` | 3 | a meal | Bread is for eating The loaf went on the supper table. | mood, state |
| `Council_DlmA_CropRaider` | 2 | a lesson | Watch the fields at night {A_nameDef} and {B_nameDef} take turns sitting out by the crops with a lantern an... | health, map, mood |
| `Council_DlmA_PenStalker` | 1 | a hunt | Hunt it down The hunters have marked {X_definite}, and {B_nameDef} has asked to be first out after it. | buildings, mood |
| `Council_DlmC_TeachOrBuild` | 3 | a lesson | After the rush The lessons wait until the work slows. | map, mood, work |
| `Council_DlmH_BackTooSoon` | 2 | a meal | {A_nameDef} is in bed with orders to stay there, and somebody brings {A_possessive} meals so there's no exc... | health, mood |
| `Council_DlmH_GuestWantsToStay` | 4 | a meal | No {X_nameDef} nodded at the answer, went back to the guest room, and didn't come out for supper. | mood, state |
| `Council_DlmH_MidwifeApprentice` | 2 | a lesson | Not now There's too much work to spare two pairs of hands for lessons. | mood |
| `Council_DlmL_CampLostChild` | 2 | a gift or goods | The colony handed over a bundle of pitch torches. | goodwill, map, state |
| `Council_DlmL_CampSick` | 1 | a lesson | {A_nameDef} took two bundles of healroot over to the camp and showed them how to steep it: a fistful to the... | goodwill, items, map, mood, state |
| `Council_DlmL_Crowded` | 2 | a meal | Supper in two sittings Supper now comes in two sittings, early and late, and there's room on the bench for ... | mood |
| `Council_DlmL_EmptyChair` | 3 | a meal | Supper on meeting nights comes later, and the cook complains about it. | mood, state |
| `Council_DlmL_FrictionGrown` | 4 | a meal | Leave them to it {A_nameDef} and {B_nameDef} are still at it, and the people who used to sit between them a... | mood |
| `Council_DlmL_OldRemedy` | 1 | a lesson | Humming the whole time, {B_nameDef} made it up exactly as {B_pronoun} was taught as a child. | health, map, mood, relations |
| `Council_DlmL_OldRemedy` | 3 | a lesson | {A_nameDef} is getting better, and has learned to thank them both, separately. | health, items, mood |
| `Council_DlmL_Quarantine` | 1 | a meal | Meals come on a board pushed under the hide, and {A_nameDef} counts the days. | mood, state |
| `Council_DlmL_SickCook` | 1 | a meal | The meals are plainer. | health, mood, skill |
| `Council_DlmL_SickCook` | 3 | a meal | Cold suppers for a few days The stove is cold, {A_nameDef} is in bed, and supper comes out of jars and croc... | health, mood |
| `Council_DlmLv_ElderAndChild` | 3 | a lesson | Children belong with children {B_nameDef} belongs with the other children, at lessons and games, the counci... | mood |
| `Council_DlmLv_Mischief` | 3 | an animal | Let it go. | skill, state |
| `Council_DlmLv_Mischief` | 3 | a mending or building | Children break things and grown-ups mend them, the council said, and that was the end of it. | skill, state |
| `Council_DlmLv_RecordKeeper` | 1 | a meal | {B_nameDef} has been very careful what {B_pronoun} says at supper. | mood, skill, state |
| `Council_DlmLv_Schooling` | 3 | a lesson | Let them run free a while yet Lessons can wait, the council said; the children should have the run of the p... | map, mood, needs, work |
| `Council_DlmMove_AgainFamily` | 3 | goodwill | {A_nameDef} will stay, and won't forget that the council kept {A_objective} from {RELATIVE}. | mood, people |
| `Council_DlmMove_AgainLove` | 3 | goodwill | {X_nameDef} will go home alone when the visit ends, and {A_nameDef} won't forget who kept them apart. | mood, people |
| `Council_DlmMove_AgainRoom` | 3 | goodwill | {A_nameDef} will stay where {A_pronoun} sleeps now, and won't forget it. | mood, people |
| `Council_DlmR_CountingOnMercy` | 3 | a meal | {X_nameDef} is fed, watched and spoken to like any other prisoner, and the release {X_pronoun} keeps asking... | people |
| `Council_DlmS_BeastReturns` | 1 | a hunt | Hunt it down now A hunt, then. | buildings, mood, state |
| `Council_DlmS_Healer` | 3 | silver | Keep the silver {X_nameDef} went back to the {FACTION} camp with the bag still tied shut, and {A_nameDef} w... | mood, state |
| `Council_DlmS_NemesisInChains` | 2 | a meal | Let {X_objective} rot in there {X_nameDef} stays in the cell, fed and ignored. | mood |
| `Council_DlmS_PetBit` | 2 | a meal | {FACTION} took that badly, and {A_nameDef} gave {PET_definite} an extra helping at dinner. | goodwill, mood, state |
| `Council_DlmS_PredatorDen` | 1 | a hunt | Hunt it The council marked {X_definite} for the hunters. | buildings, mood, state |
| `Council_DlmS_PrisonerPlea` | 3 | a meal | {A_nameDef} still carries {X_nameDef}'s meals down, and doesn't stay to talk anymore. | mood |
| `Council_DlmS_RudeGuest` | 2 | an animal | Let it go, they're guests The council said nothing to {X_nameDef}, who took that as encouragement. | mood, state |
| `Council_DlmS_RudeGuest` | 2 | a meal | {A_nameDef} ate the rest of {A_possessive} meals somewhere else until the visitors left. | mood, state |
| `Council_DlmS_Tonic` | 3 | silver | Keep the silver {X_nameDef} put the bottle away with a sigh. | mood |
| `Council_DlmV_CellLessons` | 2 | a lesson | Once {X_pronoun} is one of the colony, {A_nameDef} can learn all {A_pronoun} likes. | mood, people |
| `Council_DlmV_DrugBench` | 1 | silver | Those who counted on the silver they fetch are not. | map, mood, state, words, work |
| `Council_DlmV_SoClose` | 2 | research | The research will be there in the morning. | map, mood, needs, work |
| `Rimfolk_DlmE_KitchenHelp` | 3 | someone leaves | The kitchen stays {A_nameDef}'s The council left the kitchen to {A_nameDef}, and {B_nameDef} was turned out... | mood |
| `Rimfolk_DlmE_MasterGone` | 2 | a lesson | {B_nameDef}, who knows animals, takes {PET_nameDef} on {B_nameDef} is {PET_nameDef}'s master now, and {PET_... | animals, map, mood, state, words |
| `Rimfolk_DlmE_Seconds` | 3 | an animal | One more helping won't break the colony The council let it go. | mood, state |
| `Rimfolk_DlmE_Spoiling` | 3 | an animal | Let it go The {THING} is left where it lies, and the work goes on as it was. | mood, state |
| `Rimfolk_DlmR3_AfterTantrum` | 3 | an animal | A bad day, and no more The council said it was a bad day and let it go. | mood |
| `Rimfolk_DlmY_TrainBeast` | 1 | a lesson | Teach {PET_objective} to guard {PET_nameDef} is set to learn to guard and to go for an enemy, and {A_nameDe... | animals, map, mood, state, words |
| `Rimfolk_DlmY_TrainBeast` | 2 | a lesson | Teach {PET_objective} to haul {PET_nameDef} is set to learn to haul, and {A_nameDef} will train {PET_object... | animals, map, mood, state, words |
| `Rimfolk_Dlm_GodAsks` | 3 | a gift or goods | Offer {GOD} something lesser at {PLACE} The council sent a lesser gift to {PLACE} instead. | later, map, people, state, words |
| `Rimfolk_Dlm_MendingDay` | 2 | a lesson | {B_nameDef} will learn the bench another time. | map, mood, state, work |
| `Rimfolk_Dlm_RivalFeasts` | 1 | a meal | {GOD}'s feast in full; {GOD2}'s kept quietly The council gave the season to {GOD}. | state |
| `Rimfolk_Dlm_RivalFeasts` | 2 | a meal | {GOD2}'s feast in full; {GOD}'s kept quietly The council gave the season to {GOD2}. | state |
| `Rimfolk_Dlm_WS_Mourning` | 2 | someone leaves | Leave the colony's clothes as they are The council thinks {FACTION}'s grief is their own, and leaves it there. | mood |

### One answer plainly best or worse (dilemma_balance's measure)

- event `Council_Evt_Amends`: 3 {A_nameDef} wants nothing from {X_objective}: dominated by answer 2
- event `Council_Evt_Apprentice`: 1 Take {X_objective} on: easy: best by 104; 2 Send {X_objective} home: dominated by answer 1
- event `Council_Evt_Bathhouse`: 1 Let them, for {COUNT} silver: dominated by answer 2; 3 Not today: dominated by answer 1, dominated by answer 2
- event `Council_Evt_BirdsReturn`: 1 Get the planting started: easy: best by 13; 3 They're only birds: dominated by answer 1
- event `Council_Evt_BountyHunters`: 3 Hand {A_nameDef} over: dominated by answer 2
- event `Council_Evt_CalledHome`: 1 Write back: {A_nameDef} is needed here: dominated by answer 2; 2 Send {COUNT} silver home instead, with a long l...: easy: best by 10; 3 Let {A_nameDef} go home, with the colony's bles...: dominated by answer 1, dominated by answer 2
- event `Council_Evt_Cartographer`: 1 Pay for a survey ({COUNT} silver): dominated by answer 2; 3 Not today: dominated by answer 2
- event `Council_Evt_ChildFind`: 1 Let {B_nameDef} study it with {A_objective}: easy: best by 25; 2 Let {A_objective} keep it: dominated by answer 1; 3 Trade it ({COUNT} silver): dominated by answer 1, dominated by answer 2
- event `Council_Evt_ChildGrown`: 1 Ask {X_objective} to stay: easy: best by 43; 2 Thank {X_objective} and wish {X_objective} well: dominated by answer 3, dominated by answer 4
- event `Council_Evt_CouncilGuests`: 1 Let them watch, and ask what they like: easy: best by 14; 3 The council's business is its own: dominated by answer 1
- event `Council_Evt_CrashedDrone`: 1 Strip it for parts: dominated by answer 3
- event `Council_Evt_Creditor`: 1 Pay the {COUNT} silver: easy: best by 64; 3 Refuse: dominated by answer 1
- event `Council_Evt_DeathNews`: 1 A wake tonight, and {A_nameDef} tells the colon...: dominated by answer 3; 4 Keep {A_objective} busy; work helps: dominated by answer 3
- event `Council_Evt_DebtDue`: 3 Refuse to pay: dominated by answer 1, dominated by answer 2
- event `Council_Evt_DesertBloom`: 1 Walk out to see it: easy: best by 84; 3 Work goes on: dominated by answer 2
- event `Council_Evt_Deserter`: 1 Take {X_objective} in: dominated by answer 4; 2 Send {X_objective} on: dominated by answer 3
- event `Council_Evt_Dispute`: 4 Stay out of it: dominated by answer 1, dominated by answer 2
- event `Council_Evt_Eclipse`: 1 Drum and shout the sun back: dominated by answer 2, dominated by answer 3; 2 Watch it through smoked glass: easy: best by 19
- event `Council_Evt_Elopers`: 1 Take them in: easy: best by 46; 3 Turn them away: dominated by answer 2
- event `Council_Evt_Envoy`: 3 Send the envoy away: dominated by answer 1, dominated by answer 2
- event `Council_Evt_FamilyVisit`: 1 Ask {X_objective} to stay: easy: best by 61; 2 Enjoy the visit: dominated by answer 1
- event `Council_Evt_Fiddler`: 3 Thank {X_objective}, and let {X_objective} go on: dominated by answer 1
- event `Council_Evt_FirstSnow`: 1 Call it a snow day: easy: best by 41
- event `Council_Evt_FortuneTeller`: 1 Pay {COUNT} silver: dominated by answer 2
- event `Council_Evt_Founding`: 1 Hold a feast: easy: best by 95
- event `Council_Evt_Headstones`: 1 A proper marker for every grave ({COUNT} silver): easy: best by 121; 3 Our dead are well enough as they are: dominated by answer 1, dominated by answer 2
- event `Council_Evt_HiredHands`: 1 A day's wood for a meal each: dominated by answer 2; 2 A winter's wood for {COUNT} silver: easy: best by 12; 4 No work today: dominated by answer 2
- event `Council_Evt_KillingFrost`: 2 {A_nameDef} and two others save the best of it: dominated by answer 1; 3 Let the frost take what it takes: dominated by answer 1, dominated by answer 2
- event `Council_Evt_KinLetter`: 2 Invite {X_objective} to come and live here: dominated by answer 1; 3 Send a parcel home (30 silver): easy: best by 11
- event `Council_Evt_LimbMaker`: 1 Pay {COUNT} silver: dominated by answer 4; 2 Pay in materials ({PLACE}): dominated by answer 4; 3 No, thank you: dominated by answer 4; 4 Pay a little to have {X_objective} teach our be...: easy: best by 22
- event `Council_Evt_LongRains`: 1 Patch every roof and dig proper drains: easy: best by 14; 2 Games and stories indoors: dominated by answer 1; 3 Wait it out: dominated by answer 1
- event `Council_Evt_LongestNight`: 1 Keep the fire burning till dawn (40 wood): easy: best by 96
- event `Council_Evt_LostCargo`: 2 Keep it: dominated by answer 1; 3 Send it back, and ask for a finder's fee: dominated by answer 1
- event `Council_Evt_LostChild`: 1 Take {X_objective} home: easy: best by 105
- event `Council_Evt_LostKin`: 1 Bring {X_objective} in: easy: best by 248; 2 Send {X_objective} on: dominated by answer 1
- event `Council_Evt_LoversKin`: 3 Ask {X_objective} to leave: dominated by answer 2
- event `Council_Evt_NeighborsSick`: 2 Sell it to them ({COUNT} silver): dominated by answer 1, dominated by answer 4; 3 We can't spare it: dominated by answer 1, dominated by answer 2, dominated by answer 4
- event `Council_Evt_OldAdventurer`: 3 An evening of stories: easy: best by 38
- event `Council_Evt_OldComrade`: 1 Welcome {X_nameDef}: easy: best by 128; 2 Turn {X_objective} away: dominated by answer 1
- event `Council_Evt_OldFlame`: 1 Let {X_objective} stay: easy: best by 84; 2 Not here: dominated by answer 1
- event `Council_Evt_OldGrave`: 1 Rebury them properly: dominated by answer 3
- event `Council_Evt_OldLetters`: 3 Put them away unread: dominated by answer 2
- event `Council_Evt_OldMap`: 1 Follow it up: dominated by answer 2; 2 Keep it as a keepsake: easy: best by 12
- event `Council_Evt_OmenBeast`: 2 Leave it an offering: easy: best by 97; 3 Hunt it: dominated by answer 2, dominated by answer 4; 4 Let it go its way: dominated by answer 2
- event `Council_Evt_PW_RareSighting`: 3 Let it pass: dominated by answer 2
- event `Council_Evt_PawnedKeepsake`: 1 Buy it back ({COUNT} silver): easy: best by 13; 3 Say it was stolen, and ask for it: dominated by answer 1, dominated by answer 2; 4 Let it go: dominated by answer 1
- event `Council_Evt_PenRaider`: 1 Sit up by the pens with a lamp until it's gone: dominated by answer 2; 3 Leave it; a fence is a fence: dominated by answer 2
- event `Council_Evt_Pilgrims`: 2 Turn them away: dominated by answer 1, dominated by answer 3; 3 Bread for the road, no more: dominated by answer 1
- event `Council_Evt_Prodigal`: 1 Let {X_objective} come home: easy: best by 60; 2 Refuse: dominated by answer 1
- event `Council_Evt_Renown`: 1 Let {A_nameDef} help: easy: best by 52; 2 {A_nameDef} can't spare the day: dominated by answer 1
- event `Council_Evt_Rut`: 2 Go up the ridge at dusk to watch: easy: best by 35; 3 Keep everyone close to home: dominated by answer 1
- event `Council_Evt_Scholar`: 2 No room tonight: dominated by answer 1; 3 Ask {X_objective} to teach everyone instead: easy: best by 61
- event `Council_Evt_Schoolteacher`: 3 Thank {X_objective}, but no: dominated by answer 1
- event `Council_Evt_Scribe`: 1 Letters for anyone who wants one ({COUNT} silver): easy: best by 124; 4 No letters today: dominated by answer 1
- event `Council_Evt_SealedCrate`: 2 Break it open (it may break what's inside): dominated by answer 1; 3 Sell it sealed ({COUNT} silver): dominated by answer 1
- event `Council_Evt_Signal`: 1 Answer it (it may be a lure): dominated by answer 2, dominated by answer 3; 3 Leave it: dominated by answer 2
- event `Council_Evt_SilentMonk`: 2 Ask {X_objective} to sit with {A_nameDef}: easy: best by 47
- event `Council_Evt_SpiritSinger`: 1 Let {X_objective} sing to the land (10 food): dominated by answer 2
- event `Council_Evt_Stranded`: 3 Wish them luck: dominated by answer 2
- event `Council_Evt_Stray`: 1 It can stay: easy: best by 46; 2 Let it go: dominated by answer 1
- event `Council_Evt_StruckTree`: 1 Let {A_nameDef} carve from the heartwood: easy: best by 11; 2 Cut it for timber: dominated by answer 1; 3 Leave it standing: dominated by answer 1
- event `Council_Evt_TableGame`: 2 Play for the fun of it: dominated by answer 1, dominated by answer 3
- event `Council_Evt_TalentScout`: 1 Accept ({COUNT} silver): dominated by answer 3; 3 Not for sale: easy: best by 27
- event `Council_Evt_TasteOfHome`: 2 Buy it, and let {A_nameDef} cook it for everyon...: easy: best by 28; 3 {A_nameDef} can live without it: dominated by answer 1
- event `Council_Evt_Tinker`: 1 Pay {COUNT} silver: dominated by answer 3; 2 No, thank you: dominated by answer 3; 3 Pay a little to have {X_objective} teach {A_nam...: easy: best by 18
- event `Council_Evt_Tithe`: 4 Refuse: easy: best by 48
- event `Council_Evt_TreasureMap`: 1 Buy it ({COUNT} silver): dominated by answer 2
- event `Council_Evt_Tribute`: 3 Refuse: easy: best by 49
- event `Council_Evt_VendettaBand`: 2 Let them pass: easy: best by 23
- event `Council_Evt_WanderingCook`: 1 Give {X_objective} the kitchen (40 food): easy: best by 41; 3 Not today: dominated by answer 2
- event `Council_Evt_WantedPrisoner`: 2 Release {X_objective} instead: dominated by answer 3
- event `Council_Evt_WarBandNear`: 1 Watch the walls every night until it's gone: dominated by answer 2; 2 Send {COUNT} silver to {FACTION}'s militia: easy: best by 16; 3 Change nothing: dominated by answer 2
- event `Council_Evt_WhiteNights`: 1 Stay up through the bright night together: easy: best by 45; 2 Blankets over the windows, beds at the usual hour: dominated by answer 3
- event `Council_Evt_WoundedStranger`: 1 Bring {X_objective} in (someone may come looking): dominated by answer 2; 3 Leave {X_objective} there: dominated by answer 2
- event `Rimfolk_Evt_BannerRoll`: 3 We keep our own names: dominated by answer 2
- event `Rimfolk_Evt_BellFuneral`: 3 We will mourn our own way: dominated by answer 2
- event `Rimfolk_Evt_BellVoidSigns`: 1 Keep a watch on the walls tonight, lamps at the...: dominated by answer 2; 3 Thank them and send them on: dominated by answer 2
- event `Rimfolk_Evt_Brewer`: 3 Not today: dominated by answer 1, dominated by answer 2
- event `Rimfolk_Evt_ChildsQuestion`: 1 {A_nameDef} tells {B_nameDef} the story this ev...: easy: best by 13; 3 Later, when there's time: dominated by answer 1, dominated by answer 2
- event `Rimfolk_Evt_Disputation`: 3 Not tonight: dominated by answer 2
- event `Rimfolk_Evt_FirstPiece`: 1 A toast to the piece at supper: easy: best by 16; 3 No fuss: dominated by answer 2
- event `Rimfolk_Evt_HerbGatherer`: 3 Not on the colony's land: dominated by answer 1, dominated by answer 2
- event `Rimfolk_Evt_HerdNight`: 2 The animal will manage: dominated by answer 1
- event `Rimfolk_Evt_LastField`: 3 The growers can manage: dominated by answer 1, dominated by answer 2
- event `Rimfolk_Evt_LostTraveller`: 2 Something for the road, and the way: dominated by answer 1, dominated by answer 3
- event `Rimfolk_Evt_OldSong`: 1 An evening of it at the fire: dominated by answer 2, dominated by answer 3; 3 Not tonight: dominated by answer 2
- event `Rimfolk_Evt_OrderNurse`: 3 Thank {X_objective}, but the colony will manage: dominated by answer 1
- event `Rimfolk_Evt_PeltBuyer`: 1 Sell them the pelts ({COUNT} silver): dominated by answer 2; 3 The pelts are not for sale: dominated by answer 1, dominated by answer 2
- event `Rimfolk_Evt_Petitioner`: 2 A meal, and good luck to {X_objective}: dominated by answer 1, dominated by answer 3; 3 Stay out of their people's business: dominated by answer 1
- event `Rimfolk_Evt_PilgrimShelter`: 2 Let them stay the night outside the gate: dominated by answer 1; 3 Keep the gate shut: dominated by answer 1, dominated by answer 2
- event `Rimfolk_Evt_PromiseSore`: 1 {HOLDER_nameDef} says sorry at supper: easy: best by 26; 2 {B_nameDef} takes {A_nameDef} out for a walk: dominated by answer 1; 3 Leave it: dominated by answer 1, dominated by answer 2
- event `Rimfolk_Evt_R4EWashingDay`: 1 A washing day at the water for everyone: dominated by answer 2, dominated by answer 3
- event `Rimfolk_Evt_RationJoin`: 1 Take {X_objective} in: easy: best by 60; 2 Give {X_objective} two meals for the road: dominated by answer 1, dominated by answer 3; 3 Not now: dominated by answer 1
- event `Rimfolk_Evt_RegularDied`: 1 Keep a vigil for {DEAD}: dominated by answer 3
- event `Rimfolk_Evt_WardensLantern`: 2 Let them light it at the gate: dominated by answer 1; 3 Thank them, but no lantern: dominated by answer 1, dominated by answer 2
- event `Rimfolk_Evt_WheelMending`: 2 No, thank you: dominated by answer 1, dominated by answer 3; 3 Pay a little to have {X_objective} teach {A_nam...: easy: best by 16
- question `Council_Dilemma_ChildTrees`: 3 Trees take room from the crops: dominated by answer 2
- question `Council_Dilemma_ChildrensRace`: 3 No time for races this summer: dominated by answer 2
- question `Council_Dilemma_Gleaning`: 1 The gleanings are the children's: dominated by answer 2; 3 The animals can have the stubble: dominated by answer 2
- question `Council_Dilemma_Goal_Caravan`: 3 The colony needs every hand: dominated by answer 1, dominated by answer 2, dominated by answer 4; 4 Let {A_nameDef} decide alone: dominated by answer 1, dominated by answer 2
- question `Council_Dilemma_Goal_Foolish`: 4 Stay out of it: dominated by answer 2
- question `Council_Dilemma_Goal_Mentor`: 3 That's between the two of them: dominated by answer 2
- question `Council_Dilemma_Goal_Remember`: 4 Don't talk like that: dominated by answer 3
- question `Council_Dilemma_HerdHome`: 1 Bring the herd home together: easy: best by 12; 2 {A_nameDef} brings them in alone: dominated by answer 1; 3 They'll find their own way in: dominated by answer 1, dominated by answer 2
- question `Council_Dilemma_LambingWatch`: 1 Keep the lambing watch together: easy: best by 13; 2 {A_nameDef} sits up alone: dominated by answer 1
- question `Council_Dilemma_Path_Warrior`: 2 Not yet: dominated by answer 1, dominated by answer 3
- question `Council_Dilemma_PetDinner`: 3 Turn {PET_nameDef} loose in the wild: dominated by answer 1, dominated by answer 2
- question `Council_Dilemma_PetKitchen`: 3 Let them sort it out: dominated by answer 4
- question `Council_Dilemma_Secret_BloodPast`: 1 Keep {A_nameDef} under watch: dominated by answer 2, dominated by answer 3
- question `Council_Dilemma_Secret_DoctorsHands`: 3 Take {A_possessive} instruments away: dominated by answer 2
- question `Council_Dilemma_Secret_PirateDue`: 1 Pay it, all of it: easy: best by 47
- question `Council_Dilemma_Secret_UsingAgain`: 1 Time off, with {B_nameDef} beside {A_objective}: easy: best by 99
- question `Council_Dilemma_ShearingDay`: 1 Make a day of it: easy: best by 68; 3 No time this year: dominated by answer 2
- question `Council_Dilemma_Soc_Accusation`: 2 Take {B_nameDef}'s word for it: dominated by answer 1
- question `Council_Dilemma_Soc_ClosedCircle`: 2 Mix up the work crews: dominated by answer 1
- question `Council_Dilemma_Soc_CoverUp`: 3 Punish {B_nameDef} for the lie: dominated by answer 1
- question `Council_Dilemma_Soc_Inseparable`: 3 Keep the roster as it is: dominated by answer 1
- question `Council_Dilemma_Soc_LeftBehind`: 4 Leave them to it: dominated by answer 3
- question `Council_Dilemma_Soc_Plea`: 2 Punish {A_nameDef} as the rules say: dominated by answer 1, dominated by answer 3; 4 Double it, for trying to sway the council: dominated by answer 1, dominated by answer 2, dominated by answer 3
- question `Council_Dilemma_Soc_Remembrance`: 4 No. The stores are for the living: dominated by answer 3
- question `Council_Dilemma_Soc_SwornRivals`: 4 Tell them to grow up: dominated by answer 2
- question `Council_DlmA_FamineCull`: 2 Tighten everyone's belts: dominated by answer 4; 3 Turn the weakest out to forage: dominated by answer 1, dominated by answer 4; 4 Let {A_nameDef} decide: easy: best by 36
- question `Council_DlmA_PenStalker`: 1 Hunt it down: easy: best by 28; 2 Keep watch at the pens: dominated by answer 1; 4 Leave it be: dominated by answer 1, dominated by answer 3
- question `Council_DlmGuest_Invite`: 1 Take {X_nameDef} in all the same: easy: best by 16
- question `Council_DlmH_GuestWantsToStay`: 4 No: dominated by answer 3
- question `Council_DlmL_CampHungry`: 4 We have nothing to spare: dominated by answer 3
- question `Council_DlmL_CampLostChild`: 4 It's their child and their search: dominated by answer 2
- question `Council_DlmL_ChildQuestion`: 2 Tell {A_nameDef} a story instead: dominated by answer 1; 4 That's a question for when you're older: dominated by answer 1
- question `Council_DlmL_Crowded`: 4 Everyone is crowded: dominated by answer 1, dominated by answer 2, dominated by answer 3
- question `Council_DlmL_DayOffBadTime`: 3 Ask {A_nameDef} to hold on until the rush is over: dominated by answer 2
- question `Council_DlmL_ElderRest`: 3 Lighter work: mending and minding the stores: dominated by answer 2; 4 Not yet; we need every hand: dominated by answer 3
- question `Council_DlmL_EmptyChair`: 2 What was decided stands: dominated by answer 1, dominated by answer 3; 3 From now on, meetings wait for those at work: dominated by answer 1
- question `Council_DlmL_FoundJournal`: 4 Bury it with a marker: dominated by answer 1, dominated by answer 3
- question `Council_DlmL_FrictionGrown`: 4 Leave them to it: dominated by answer 2, dominated by answer 3
- question `Council_DlmL_SameWork`: 4 Someone has to do it: dominated by answer 1, dominated by answer 2, dominated by answer 3
- question `Council_DlmL_StrangerMedicine`: 3 We can't spare anything: dominated by answer 2
- question `Council_DlmLv_ChildGrief`: 1 {B_nameDef} takes {A_nameDef} under {B_possessi...: easy: best by 15; 2 A goodbye at the grave, with {A_nameDef}: dominated by answer 1; 4 Time will do it: dominated by answer 1, dominated by answer 2, dominated by answer 3
- question `Council_DlmLv_ElderAndChild`: 1 Let {A_nameDef} teach {B_nameDef}: easy: best by 14; 2 Ask {B_nameDef}'s family first: dominated by answer 1; 3 Children belong with children: dominated by answer 1, dominated by answer 2
- question `Council_DlmLv_ElderCold`: 3 {B_nameDef} sits with {A_nameDef} by the fire i...: easy: best by 10; 4 Everyone's cold. {A_nameDef} will manage.: dominated by answer 2, dominated by answer 3
- question `Council_DlmLv_ElderDoctor`: 1 Insist: {B_nameDef} sees to {A_nameDef} today: dominated by answer 3; 2 Respect {A_nameDef}'s wishes: dominated by answer 3
- question `Council_DlmLv_Mischief`: 2 Say sorry to everyone at supper: dominated by answer 1, dominated by answer 3; 3 Children break things. Let it go.: dominated by answer 1; 4 A stern word from the council: dominated by answer 1, dominated by answer 3
- question `Council_DlmLv_Schooling`: 1 Morning lessons, with {B_nameDef}: easy: best by 41; 2 They learn by working beside us: dominated by answer 1
- question `Council_DlmLv_TeenRoad`: 2 Not this year: dominated by answer 1
- question `Council_DlmLv_WorrierWatch`: 1 Set a night watch for the night: dominated by answer 2; 3 The walls are fine. Get some sleep.: dominated by answer 1, dominated by answer 2
- question `Council_DlmMove_AgainFamily`: 2 Send for {RELATIVE} to come here, with {COUNT} ...: easy: best by 111
- question `Council_DlmMove_AgainLove`: 2 Ask {X_nameDef} to join the colony: easy: best by 199
- question `Council_DlmMove_Family`: 3 Send for {RELATIVE} to come here, with 300 silv...: easy: best by 62; 4 Ask {A_nameDef} to stay: dominated by answer 2
- question `Council_DlmMove_Love`: 2 Ask {X_nameDef} to join the colony: easy: best by 159
- question `Council_DlmR_FriendAtTheGate`: 2 The rule is the rule: dominated by answer 1
- question `Council_DlmR_Handouts`: 2 Food for work: easy: best by 16; 3 Close the gate to beggars: dominated by answer 2
- question `Council_DlmR_OldCustom`: 1 Bring it back, the way it was: easy: best by 56
- question `Council_DlmR_OneMercy`: 1 Send food, and ask nothing: easy: best by 33
- question `Council_DlmR_Sanctuary`: 1 Take {X_objective} in: easy: best by 96
- question `Council_DlmR_StingyMockery`: 1 Send {X_nameDef} off with a gift: easy: best by 25
- question `Rimfolk_DlmE_ColdSnap`: 3 Carry on: dominated by answer 1
- question `Rimfolk_DlmR3_AfterTantrum`: 1 {A_nameDef} mends the damage: easy: best by 11; 2 {A_nameDef} says sorry at supper: dominated by answer 1; 3 A bad day, and no more: dominated by answer 1, dominated by answer 2
- question `Rimfolk_DlmR3_CryingNight`: 1 {B_nameDef} sees to {THIRD_nameDef} for a few days: easy: best by 15; 3 Babies cry: dominated by answer 1
- question `Rimfolk_DlmR3_OurVerse`: 1 An evening at the fire to make the verse: easy: best by 45; 3 Leave the verses to {NAME}: dominated by answer 2
- question `Rimfolk_DlmR3_VisitorFriend`: 3 Guests come and go: dominated by answer 2
- question `Rimfolk_DlmR4_ColdCell`: 2 {A_nameDef} takes {X_nameDef} a hot meal: dominated by answer 1; 3 The cell stays as it is: dominated by answer 1
- question `Rimfolk_DlmR4_NightOwl`: 1 {A_nameDef} keeps the night hours for a while: easy: best by 16; 3 The colony keeps one day: dominated by answer 1
- question `Rimfolk_Dlm_MendingDay`: 3 No day of its own for it: dominated by answer 1, dominated by answer 2
- question `Rimfolk_Dlm_PrisonerPray`: 3 No: dominated by answer 1, dominated by answer 2
- question `Rimfolk_Dlm_WS_FairDay`: 1 Hold a fair day: easy: best by 15
- question `Rimfolk_Dlm_WS_FallingStars`: 1 A night under the stars for all who want it: easy: best by 45
- question `Rimfolk_Dlm_Woodpile`: 3 The stores have wood enough: dominated by answer 1, dominated by answer 2

<!-- impact_audit: end -->
