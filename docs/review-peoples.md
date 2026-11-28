# Review of the peoples' events (28 November 2026)

The player, 28 November 2026: "regularly reviewing what you have already added to the games, all the different events,
visitor world maps, etc. Just testing whether everything works properly as intended, whether it actually has also impacts
on the game and it's not just flavor, whether it take the existing content like events and dilemmas and whatnot can be just
further enhanced. And then after, or also on the side, just add even more variety." After the events and the council, the
camps and the world, and the gods, the player chose the peoples' events (this review) and the visitors and strangers (another
review, at the same time). The standing rule of 27 November: more content for the existing mechanics, no new mechanics.

Read: every one of the peoples' events (170 now: an IncidentDef with a HappeningExtension in
`Defs/IncidentDefs/Rimfolk_Events_People*.xml`, the releases C to G, the lore, and rounds 1 to 10 of
`docs/peoples-events-proposals.md`), each worker's casting (`CanHappen`, `Cast`, `Begin`, `Resolved`), the watches that fire
the 31 events that come when their moment comes, the doors every round sends people through to work at a place, the
peoples' own effects, their visits, camps, contracts, terms and records, and the peoples' casters (`Peoples/Magic`:
`PeoplesMagic.cs`, `PeoplesMagicWatch.cs`, `PeoplesMagicPatches.cs`, `PeoplesMagicMath.cs`). The events and council review
had already measured every event's answers, these among them, and fixed the Quiet Bell's {PLACE}; this review went into the
peoples' own code: who comes, from which people, at which moment, in which age, and what their jobs do to the colony's people.
`RomSpells` (shared since 0.321.0 with the colonists' own casting, `Integrations/MagicAutoCast.cs`) was read and left as it is.

The fixed rules are kept by `Tools/Tests/ReviewPeoplesTests.cs`.

## How it was measured

`Tools/peoples_impact_audit.py` reads each of the peoples' events: its people (the def's callings, else the `PeoplesMath`
calling its worker names, else its file's only one; the eight cast by their setting are named by hand), whether the
storyteller draws it or its own code fires it, the forms of the age its texts are written in (the people's own, [age0] to
[age3], or only the colony's, [lowtech] and the like), and its answers as `Tools/impact_audit.py` reads every event's answers
(what the game keeps, a play-out, Rimfolk's own numbers, or only a mood and words). It sets each event's placeholders against
what its worker's own class fills, finds every job the peoples' code starts and whether the door it goes through refuses one
of the colony who is drafted, in a break, down or on the player's own order, and lists the defs looked up by name without
`SilentFail`, the hediffs the peoples give and how each ends, and the spells the peoples' casters cast for the colony.

    python3 Tools/peoples_impact_audit.py                     # the counts
    python3 Tools/peoples_impact_audit.py --print events      # or peoples, thin, flavour, promises, words, doors, lookups, hediffs, magic
    python3 Tools/peoples_impact_audit.py --write             # the measured part of this file, between its markers
    python3 Tools/peoples_impact_audit.py --root DIR          # the counts of another copy (an older commit's Source, Defs, Languages)

The measure is a hint to read by hand. It doesn't follow a job's end (the lessons most rounds give when the work at a place is
done are in `WorkDone`), so every answer whose words promise something was read in its code; and it reads the forms of the
age from the tags, not the words, so the words of the events told only in the colony's age were searched for things of one
age (radio, wire, truck, pod, lander, shuttle, console).

## Counts, before and after

| Part | Before | After |
|---|---|---|
| The peoples' events (drawn, fired) | 169 (138, 31) | 170 (139, 31) |
| Their answers: with a game effect or a play-out, thin, flavour only, empty | 507: 505, 1, 0, 1 | 510: 509, 0, 0, 1 |
| Events whose every answer is thin | 0 | 0 |
| Events with a placeholder no code of theirs fills | 0 | 0 |
| Events told in their people's own age; in the colony's age only | 72; 97 | 74; 96 |
| The doors that may send the colony's own people, refusing one held | 4 of 11 | 11 of 11 |
| Defs looked up by name without `SilentFail` | 0 | 0 |
| Hediffs the peoples give that nothing ends | 0 of 8 | 0 of 11 (the palmer's three ailments are the road's, and heal) |
| The pilgrim fellowship's events (the fewest of any people) | 6 | 7 |

What the counts say, read by hand:

- **The peoples' events change the game.** Every answer but a refusal does something the game keeps or plays out on the map:
  the rounds were built that way (each item of the proposals names its gift, its camp or its work), and the events and
  council review found no flavour-only answer among them. The one thin answer was the league's red ink argued (below).
- **The bugs were in the shared code, not the words.** The doors every round sends people through checked that they were up
  and not in a break, but not that the player had drafted them or given them an order (fixed 1), as the gods' happenings did
  before their review; and a few records were read again at their moment without asking whether their people were still
  there and in the age the words need (fixed 2 to 4).
- **Each people has seven to ten events of its own** (the art handed on counts for all), the pilgrim fellowship the fewest,
  with six before this review: it has one more now (added below).

## Fixed

Most serious first.

1. **The peoples' jobs took the colony's people out of the player's hands** (`Peoples/PeoplesReview.cs`, `PeoplesReview.Held`;
   rounds 4 to 9's `Send`, round 10 through round 9's; `PeoplesE.Start`; `PeoplesD2.StartBout`; `DilemmaEffect_BrokerPod`;
   `Health/OutsiderTend.Send`). Every round's door sends "one of theirs (or one of the colony's)" to work at a place with a
   forced job, and checked only that they were up and not in a break. Most scenes asked themselves whether their colonist
   was drafted (the moot's talker, the copyist's crafter, the master's apprentice, the console's talker), but none whether the
   player had set them to something of their own, so a colonist the player had just ordered to haul, build or rescue was
   walked off to the table, the bench or the gate; and some asked nothing, so a drafted colonist was taken out of the fight:
   the one who sits with the shouting mind, the Circuit's trainer to the bout, the vault child under the sky, the doctor sent
   to a visitor (the Works' batch, the broker in the pod, the camps' sick and wounded, the hospital's patient). Now one drafted, in a
   break, down or on the player's own order is refused at every door, and the scene waits for them (each round's hour sends
   them again) or lets go at its time, as when someone is away. A visitor is never drafted and has no orders from the player,
   so the same doors still send theirs. The broker rescued from the pod is now carried in by the nearest other colonist who
   is free when the one named is held; a doctor held is not sent to a visitor, and the answer's medicine goes as it did
   before the tending was played out.
2. **The enclave's instalment letter came while the enclave was at war with the colony** (`Peoples/Events2/EnclaveBody.cs`,
   `EnclaveTerms.Due`, `Hour`). The terms' note said "a people gone or at war ends them", but only a people gone did: at war
   the letter still came with its courier "waiting at the gate", and its default answer wrote a default against the colony.
   Now no letter comes while they are at war; the terms are kept, and the instalment is asked for again once there is peace.
3. **A Fleet of an older age landed a shuttle** (`Peoples/PeoplesF2.cs`, `HappeningWorker_FleetShuttle`). The Hollow Fleet
   arises in every age and falls back like any people, but its shuttle asked only that the colony knew what comes down out of
   the sky. Now it lands only from a Fleet of the industrial or spacer age, by the same rule the colony's age is held to.
4. **A Signal Line of the tribal or medieval age spoke of radio masts** (`Rimfolk_Evt_NewsForSale`, `Rimfolk_Evt_LineCrew`).
   The Line arises in every age; its news for sale and its line crew said "radio masts", "a headset", "a beacon's call",
   "string wire" in texts any age could draw. Those are now told only of a Line of the industrial or spacer age, and the news
   of the old ages' Line has its own: news passed along the signal towers by flag and fire, a bandits' camp seen from a tower,
   something seen to fall out of the sky.

## Answers given real effects

- **The league's red ink argued** (`Rimfolk_Evt_RedInk`, "Send {A} to argue the entry": "{A} disputed the entry at the
  gate"). Only the book's page changed. Now the talker learns Social by it, 200 when the entry is struck out, 100 when it is
  written in a second time, as the creditor talked down does since the events and council review.

That is the only one. Read by hand, the peoples' words keep their promises: the lessons the rounds give are in their jobs'
done code (the copyist's crafter learns Crafting and Social, the art handed on teaches its skill, the sick room's rounds
Medicine, the bond given Animals, the night with the rainmakers Plants and the Glass's lesson, the drill's walk Mining, the
ship's lesson Intellectual, the shouting mind's helper Social); the old soldier joins the colony (`Arrivals.Join`); the vial of
the colony's own storm is the price instead of silver, nothing to carry; the pilgrims "taken in" stay the night in a warm
corner each (`VisitorServices.Lodge`).

## Added in existing frames

**The footsore palmer** (`Rimfolk_Evt_PplR7_Footsore`, `Defs/IncidentDefs/Rimfolk_Events_PeoplesReview.xml`;
`HappeningWorker_PplR7Footsore`). The pilgrim fellowship had the fewest events of any people. A palmer on the long walk to
one of the fellowship's hostels comes to the gate hurt by the road (blisters, a sprained ankle or sore knees, Rimfolk's own
small ailments, given to the palmer as the event makes them) and asks for the colony's doctor, whom the card names. Built from
the shapes there are, the fellowship's ways ("the open door, the footsore and the old shrines"; "Every door is a shrine to
someone"):

| Answer | On the map | What it does |
|---|---|---|
| {A} sees to {X}'s {TALE} (1 medicine) | The doctor fetches a medicine from the stores, goes out to the palmer at the gate and tends them (`DilemmaEffect_TendStranger`). | The game's own tend; the doctor learns Medicine (200); the fellowship +5 and remembers who tended them. |
| A medicine for the road (1) | A medicine is carried out to the palmer at the gate (`DilemmaEffect_CarryOut`). | The fellowship +3. |
| Keep the gate shut (no answer) | The palmer blesses the shut gate all the same, said over their head, and walks on. | Nothing. |

Only from a pilgrim people at peace with the colony, among the visitors (0.4, not again for 45 days, not before day 15),
told in the fellowship's own age (on foot in every age), a doctor asked only if one is free to doctor. The palmer's word at the
gate is one sentence for each answer.

## Checked and found sound

- **Who comes, and from which people.** Every drawn event finds its people through `PeoplesD.Friendly` (a people of its
  calling, not the colony, not hidden, not defeated, not at war with the colony) or the pair of round 10 (`PeopleEvents10.Pair`:
  both at peace with the colony and not at war with each other). The 31 fired events are fired by their watches in the same
  hour the watch checks the record's people: the enclave's contract lapses at war, the Banner's muster waits a season, the
  knot night and the lanterns skip a people at war, the mourners need the Bell's escort at peace with them, the Wheel's yard
  needs the Wheel at peace, the guild's journeyman waits a season while the guild is at war, a claim on the fallen comes only
  from a people at peace.
- **The words.** Every placeholder of every one of the peoples' events is filled by its worker's own class (the measured part
  below); none of the round tests' rules was loosened. The proposals' rule "each comes only from a people of that calling at
  peace with the colony" holds for all 170.
- **Every age.** Rounds 4 to 10 tell each event in the people's own age; the earlier events in the colony's age and the
  people's founding ways. Their words were searched for things of one age: the Line's radio and the Fleet's shuttle were the
  only ones a people of another age could meet (fixed above); the enclave's lander, pod and salvage by pod come only to a colony
  of the industrial or spacer age, and the enclave never falls below the industrial age.
- **Defs looked up by name** are all looked up softly (`GetNamedSilentFail`); `ThingDefOf.GravlitePanel` is Odyssey's and falls
  back to a part when Odyssey is off.
- **Hediffs.** Those the peoples give end on their own (the Glass's lesson and the order's nursing go after a while, the Works'
  gene fault and the road's ailments heal), or are honours meant for life (the Wardens' red cord, the Circuit's token, the
  world's word on a legend), or are ended by their own class (the Lattice's link).
- **Harm.** Every harm the peoples' events do to the colony's people goes through the shared budget (`Harm.Roll`, `Harm.Injure`,
  `Harm.Ail`, `Harm.Fight`, `IllnessBudget`): the drovers' bull, the pit's cave-in, the linesman's fall, the tamers' bout, the
  camp fevers, the doctor at the field sickhouse, the storm tower's strike, the fire that rekindles, the company's bet, the
  hybrid that gets loose. The sick and wounded the sickhouse brings and the footsore palmer are the event's own visitors.
- **Goodwill** changes for a reason named (`WorldSituationWorker.Goodwill` with a `GoodwillWhy`, `Missions.Goodwill`, the
  answers' own `EventGoodwill` under the event's reason); `GoodwillReasonTests` holds for the whole mod.
- **The council not answering.** The default is the answer that sends them on, but for the few whose people are at work
  already (the tide reader, the canopy fire) or whose default costs nothing (the pilgrims sleep outside the gate, the order's
  nurse goes from bed to bed); every worker's `Resolved` that pays or carries waits for an answer given (`!byDefault`).
- **Visitors leave.** Every visit, errand and gathering of theirs is a lord with its hours (`LordJob_VisitOnErrand`), and each
  round's hour lets its visit go at its time.
- **The peoples' casters.** A visitor of the peoples with a class names it once in their first hours, a raider's is named by a
  colonist who sees them; a spell for the colony in peace (a healing, a cure, a scar eased, rain in a dry spell) only from one
  content or allied, with no danger on the map, once a visit, cast by the mod's own job, mana and cooldown, and told only when
  the mod shows it cast; the mod's most broken spells are kept from the peoples' hands in both of the mod's checks. Nothing of
  it is found without A RimWorld of Magic. `RomSpells` was not changed.

## Looked at and left

- **The events told in the colony's age only** (96, rounds 1 to 3 and releases C to G): written in the people's founding ways,
  their words are about what the people do, not what they carry, and the search above found nothing more. Left; a content round
  could give them the people's own forms as rounds 4 to 10 have.
- **The enclave lander's "Not today"** (`Rimfolk_Evt_EnclaveLander`): a refusal that does nothing, as it should.
- **The Banner's muster and the sergeant under a white flag** take the record's people at their casting without asking again,
  but are fired only by their watches, which ask it in the same hour. Left.
- **"How the masts heard it"** in the news's answers and title: a mast of flags is a mast too. Left.
- **The pilgrims "taken in"** read as someone joining by the word list: they stay the night. Left.
- **The peoples' casters' memo of what each people is known for** (`PeoplesMagic.among`) is kept by people and cleared at every
  load. Left.

<!-- peoples_impact_audit: begin -->

Measured by `python3 Tools/peoples_impact_audit.py --write` (rewritten each run; the review's own notes are outside the markers).

| Part | Count |
|---|---|
| The peoples' events (drawn by the storyteller, fired when their moment comes) | 170 (139, 31) |
| Their answers: with a game effect or a play-out, thin (moods and Rimfolk's numbers), flavour only, empty | 510: 509, 0, 0, 1 |
| Events whose every answer is thin | 0 |
| Words that promise more than the answer's effects (to read by hand) | 17 |
| Events with a placeholder no code of theirs fills | 0 |
| Events whose people the code doesn't name | 0 |
| Events told in their people's own age ([age0] to [age3], or their goods by it); checked and left, the same in every age; in the colony's age only | 157; 13; 0 |
| The doors that send someone to work and may send the colony's own, refusing one held | 11 of 11 |
| Defs looked up by name without SilentFail | 0 |
| Hediffs of Rimfolk's own the peoples give, nothing ending them | 0 of 11 |

### Each people's events

| People | Calling | Events | Drawn | Fired | Shared with another people | Answers | With a game effect or a play-out | Thin or flavour | Empty |
|---|---|---|---|---|---|---|---|---|---|
| the road league | traders | 7 | 7 | 0 | 1 | 21 | 21 | 0 | 0 |
| the hospitaller order | healers | 8 | 7 | 1 | 1 | 25 | 25 | 0 | 0 |
| the herding folk | herders | 7 | 5 | 2 | 0 | 21 | 21 | 0 | 0 |
| the pilgrim fellowship | pilgrims | 7 | 7 | 0 | 0 | 22 | 22 | 0 | 0 |
| the learned society | scholars | 8 | 8 | 0 | 1 | 24 | 24 | 0 | 0 |
| the spacer enclave | brokers | 9 | 7 | 2 | 0 | 25 | 24 | 0 | 1 |
| the river folk | fishers | 9 | 7 | 2 | 0 | 27 | 27 | 0 | 0 |
| the Lantern Wardens | hunters | 9 | 7 | 2 | 1 | 24 | 24 | 0 | 0 |
| the Quiet Bell | sextons | 10 | 7 | 3 | 1 | 31 | 31 | 0 | 0 |
| the Circuit of Tamers | tamers | 10 | 9 | 1 | 1 | 30 | 30 | 0 | 0 |
| the Wheel | salvagers | 9 | 7 | 2 | 1 | 28 | 28 | 0 | 0 |
| the Stormglass Conclave | seers | 8 | 6 | 2 | 0 | 24 | 24 | 0 | 0 |
| the Mossborn | grovekeepers | 9 | 7 | 2 | 1 | 28 | 28 | 0 | 0 |
| the Ashen Banner | mercenaries | 9 | 7 | 2 | 0 | 27 | 27 | 0 | 0 |
| the Kiln Guild | artisans | 8 | 6 | 2 | 0 | 24 | 24 | 0 | 0 |
| the Signal Line | linesmen | 8 | 8 | 0 | 0 | 26 | 26 | 0 | 0 |
| the Deepwell Union | drillers | 10 | 8 | 2 | 1 | 31 | 31 | 0 | 0 |
| the Last Republic | citizens | 9 | 5 | 4 | 0 | 27 | 27 | 0 | 0 |
| the Hollow Fleet | crew | 8 | 8 | 0 | 0 | 24 | 24 | 0 | 0 |
| the Helix Works | editors | 9 | 9 | 0 | 0 | 27 | 27 | 0 | 0 |
| the Lattice | mechanitors | 9 | 9 | 0 | 0 | 27 | 27 | 0 | 0 |
| the Choir of the Spire | listeners | 7 | 6 | 1 | 0 | 21 | 21 | 0 | 0 |
| the Sunken Vault | vaultfolk | 10 | 9 | 1 | 1 | 30 | 30 | 0 | 0 |

An event shared by two peoples (round 10's four, the hunt or the tame) is counted for each, and the art handed on, which any of the peoples may hand on, for every one; the forms of the age are in the table of each event at the end.

### Thin, flavour-only and empty answers

| Event | # | Answer | Carries |
|---|---|---|---|
| `Rimfolk_Evt_EnclaveLander` | 2 | Not today | nothing |

### Words that promise more (to read by hand)

| Event | # | Promise | Words | Carries |
|---|---|---|---|---|
| `Rimfolk_Evt_ArtHandedOn` | 1 | a lesson | Let {A_nameDef} learn it {A_nameDef} will spend the afternoon with {X_nameDef} at the gate. | map, state |
| `Rimfolk_Evt_ArtHandedOn` | 2 | a lesson | Let {A_nameDef} learn it, and give {X_nameDef} a gift in return {A_nameDef} will spend the aftern... | items, map, state |
| `Rimfolk_Evt_BondGiven` | 1 | a lesson | They will work through every task it can learn this afternoon. | map, state |
| `Rimfolk_Evt_BreakDrought` | 2 | a lesson | Have them bring the rain, and send {A_nameDef} to keep the night with them ({COUNT} silver) The s... | items, map, state |
| `Rimfolk_Evt_DeepSeam` | 2 | a lesson | Have {X_objective} mark it, and send {A_nameDef} to walk the ground with {X_objective} ({COUNT} s... | items, map, state |
| `Rimfolk_Evt_GravshipLog` | 2 | a lesson | Let them come down, and teach {A_nameDef} the ship The Fleet's officers will come down near the {... | map |
| `Rimfolk_Evt_LearningTraded` | 1 | a lesson | Host them, and let {A_nameDef} teach them each afternoon The copyist and the technician will stay... | map |
| `Rimfolk_Evt_MindShouts` | 2 | a lesson | Let {A_nameDef} sit with them and learn how {X_nameDef} and {A_nameDef} go to {B_nameDef} and sit... | map, state |
| `Rimfolk_Evt_OldSoldier` | 1 | someone joins | Take {X_nameDef} in {X_nameDef} joins the colony. | map, state |
| `Rimfolk_Evt_OldSoldier` | 2 | someone joins | Take {X_nameDef} in, and buy out the musters ({DEAD} silver) {X_nameDef} joins the colony, and th... | items, map, state |
| `Rimfolk_Evt_PilgrimShelter` | 1 | someone joins | Take them in for the night The pilgrims were given a warm corner each indoors for the night, and ... | goodwill, later, map |
| `Rimfolk_Evt_RedInk` | 3 | silver | The league will give the colony worse terms until it pays. | map, state |
| `Rimfolk_Evt_SickRoom` | 2 | a lesson | Give {X_objective} {PLACE}, {B_nameDef} on the rounds too The sick are going to {PLACE}, and {B_n... | items, map, state |
| `Rimfolk_Evt_StormSense` | 2 | silver | Pay with a vial of our own storm, caught at our rod {B_nameDef} and {X_nameDef} are walking out t... | goodwill, map, state |
| `Rimfolk_Evt_SummerSchool` | 1 | a lesson | The school will camp on the colony's land, and the lessons can be asked for from its page in the ... | map, state |
| `Rimfolk_Evt_SummerSchool` | 3 | a lesson | Not this summer {X_nameDef} thanks the colony and goes on to look for another place to teach. | map |
| `Rimfolk_Evt_WorkThatStays` | 2 | a lesson | Let them build it, and send {A_nameDef} to work beside them The three journeymen will camp by the... | map, state |

### Placeholders no code of the event's fills

None.

### The doors that send someone to work, and their guard

| Door | File | Sends | Refuses one drafted, in a break, down or on the player's order |
|---|---|---|---|
| `Bout` | HuntOrTame.cs | theirs only | no |
| `Send` | PeopleWorkOn.cs | theirs only | no |
| `Send` | SeasonAway.cs | the colony's too | yes |
| `Send` | SeasonAway.cs | the colony's too | yes |
| `Send` | PeopleEvents4.cs | the colony's too | yes |
| `Send` | PeopleEvents5.cs | the colony's too | yes |
| `Send` | PeopleEvents6.cs | the colony's too | yes |
| `Send` | PeopleEvents7.cs | the colony's too | yes |
| `Start` | PeopleEvents8.cs | theirs only | no |
| `Send` | PeopleEvents9.cs | the colony's too | yes |
| `StartBout` | PeoplesD2.cs | the colony's too | yes |
| `Start` | PeoplesE.cs | the colony's too | yes |
| `Send` | PeoplesF2.cs | the colony's too | yes |
| `Hold` | PeoplesG.cs | the colony's too | yes |

### Defs looked up by name without SilentFail

None.

### Hediffs the peoples give, and how each ends

| Hediff | File | How it ends |
|---|---|---|
| `Council_Ail_HeadCold` | Council_Hediffs_Ailments.xml | an illness that is fought off |
| `Council_Ail_Blisters` | Council_Hediffs_Ailments.xml | its severity falls, an illness that is fought off |
| `Council_Ail_SprainedAnkle` | Council_Hediffs_Ailments.xml | its severity falls, an illness that is fought off |
| `Council_Ail_SoreKnees` | Rimfolk_Hediffs_Ailments_New.xml | its severity falls, an illness that is fought off |
| `Council_Legendary` | Council_WorldEvents_Misc.xml | the world's word on a colonist, for life |
| `Rimfolk_RedCord` | Rimfolk_PeopleEvents4_Misc.xml | an honour, for life |
| `Rimfolk_CircuitToken` | Rimfolk_PeopleEvents5_Misc.xml | an honour, for life |
| `Rimfolk_LatticeLink` | Rimfolk_PeopleEvents9_Misc.xml | its own class ends it when the link is broken |
| `Rimfolk_GlassTaught` | Rimfolk_PeoplesE_Misc.xml | goes after a while |
| `Rimfolk_GeneFault` | Rimfolk_PeoplesG_Misc.xml | its severity falls |
| `Rimfolk_OrderNursed` | Rimfolk_PeoplesLore_Misc.xml | goes after a while |

### The peoples' casters

Spells cast for the colony in peace: Priest TM_AdvancedHeal (heal); Priest TM_Purify (scar); Paladin TM_Heal (heal); Druid TM_CureDisease (cure); Druid TM_Regenerate (heal); HeartOfFrost TM_Rainmaker (rain).

Kept out of the peoples' hands: `TM_Firestorm`, `TM_Blizzard`, `TM_EyeOfTheStorm`, `TM_LightningStorm`, `TM_Meteor`, `TM_HolyWrath`, `TM_OrbitalStrike`, `TM_FoldReality`, `TM_ChronostaticField`, `TM_Resurrection`, `TM_Polymorph`, `TM_SummonPoppi`, `TM_SummonElemental`, `TM_Sabotage`, `TM_TechnoTurret`, `TM_RaiseUndead`, `TM_LichForm`, `TM_BloodMoon`, `TM_Scorn`, `TM_PsychicShock`, `TM_Headshot`, `TM_60mmMortar`, `TM_PsionicStorm`, `TM_BreachingCharge`, `TM_GraveBlade`.

### Each event

| Event | People | Drawn or fired | Ages in its texts | Answers: game, thin, empty |
|---|---|---|---|---|
| `Rimfolk_Evt_HerdersBeast` | herders | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_HuntOrTame` | hunters, tamers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_LanternPost` | hunters | drawn | 0 1 2 3 any | 0, 0, 0 |
| `Rimfolk_Evt_LatticeMechlink` | mechanitors | drawn | 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_LeagueTrust` | traders | drawn | 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_MossSeed` | grovekeepers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_OrderLimb` | healers | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_ScholarsReading` | scholars | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_StormSense` | seers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_ArtHandedOn` | any | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_LearningTraded` | scholars, vaultfolk | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_PlagueYear` | healers, sextons | drawn | 0 1 2 3 | 4, 0, 0 |
| `Rimfolk_Evt_RoadToll` | traders, salvagers | drawn | 0 1 2 3 | 4, 0, 0 |
| `Rimfolk_Evt_WoodMoot` | drillers, grovekeepers | drawn | 0 1 2 3 | 4, 0, 0 |
| `Rimfolk_Evt_ArchiveTrust` | scholars | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_EnclaveBody` | brokers | drawn | 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_EnclaveInstalment` | brokers | fired | 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_EnclavePad` | brokers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_GuildMark` | artisans | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_HerdersDrought` | herders | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_KnotNight` | fishers | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_LameBeast` | tamers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_LeagueGuard` | traders | drawn | 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_NewsForSale` | linesmen | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_Pilgrimage` | pilgrims | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_RiverCrossing` | fishers | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_DroveCrossing` | herders | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_FieldSickhouse` | healers | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_HardBirth` | herders | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_OldShrine` | pilgrims | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_RedInk` | traders | drawn | 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_RoadGang` | traders | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_SickRoom` | healers | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_SickbedLitters` | healers | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_SpringCalving` | herders | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_ThiefTrail` | herders | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_TwoFaiths` | pilgrims | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_BoatBuilders` | fishers | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_BrokerPod` | brokers | drawn | 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_ContractDue` | brokers | fired | 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_EnclaveContract` | brokers | drawn | 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_FellowExamined` | scholars | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_FloodedOut` | fishers | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_FouledWater` | fishers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_HiveBurned` | hunters | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_KnotLanterns` | fishers | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_SummerSchool` | scholars | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_WardensManhunt` | hunters | fired | 0 2 any | 3, 0, 0 |
| `Rimfolk_Evt_WardensPage` | hunters | fired | 0 2 any | 3, 0, 0 |
| `Rimfolk_Evt_BondGiven` | tamers | drawn | 0 2 any | 3, 0, 0 |
| `Rimfolk_Evt_CircuitRing` | tamers | drawn | 0 2 any | 3, 0, 0 |
| `Rimfolk_Evt_CraterEgg` | tamers | drawn | 0 2 any | 3, 0, 0 |
| `Rimfolk_Evt_CraterEggGrown` | tamers | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_FallenClaimed` | sextons | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_FieldOfFallen` | sextons | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_FieldStirs` | sextons | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_GraveGarden` | sextons | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_MadeBetter` | salvagers | drawn | 0 2 any | 3, 0, 0 |
| `Rimfolk_Evt_MendTonight` | salvagers | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_OtherSideMourners` | sextons | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_WagonOfOurOwn` | salvagers | drawn | 0 2 any | 3, 0, 0 |
| `Rimfolk_Evt_WheelStopsBy` | salvagers | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_AfterFire` | grovekeepers | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_BannerContract` | mercenaries | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_BannerMuster` | mercenaries | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_BreakDrought` | seers | fired | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_CanopyFire` | grovekeepers | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_CompanyMarch` | mercenaries | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_OldSoldier` | mercenaries | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_OldWoodSeeds` | grovekeepers | drawn | 0 2 any | 3, 0, 0 |
| `Rimfolk_Evt_OtherSideContract` | mercenaries | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_StormTower` | seers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_TideReader` | seers | fired | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_ComeBackBetter` | artisans | fired | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_DeepSeam` | drillers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_LineCrew` | linesmen | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_MastersMark` | artisans | fired | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_OilInGround` | drillers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_OrganizersEvening` | drillers | fired | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_Picket` | drillers | fired | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_PitCaveIn` | drillers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_TownMast` | linesmen | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_VoiceForColony` | linesmen | drawn | 0 1 2 3 any | 5, 0, 0 |
| `Rimfolk_Evt_WorkThatStays` | artisans | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_FieldLab` | editors | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_FleetSleeperCasket` | crew | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_GeneTakenOut` | editors | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_GravshipLog` | crew | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_HybridBeast` | editors | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_LandingParty` | crew | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_OrderToDisembark` | crew | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_RationJoin` | citizens | fired | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_RationPost` | citizens | fired | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_RepublicFugitive` | citizens | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_RepublicMedal` | citizens | fired | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_VoteObserver` | citizens | fired | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_WholeXenogerm` | editors | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_ConsoleAsk` | vaultfolk | fired | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_ConsoleTalk` | vaultfolk | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_FeralMech` | mechanitors | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_HumDeepened` | listeners | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_LatticeLinked` | mechanitors | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_ListeningRing` | listeners | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_MechYard` | mechanitors | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_MindShouts` | listeners | fired | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_SealBroken` | vaultfolk | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_SurfaceScouts` | vaultfolk | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_VaultLastPart` | vaultfolk | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_VoidListens` | listeners | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_LeagueOutrider` | traders | drawn | 1 2 3 any | 2, 0, 0 |
| `Rimfolk_Evt_OrderHealer` | healers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_EnclaveLander` | brokers | drawn | 2 3 any | 1, 0, 1 |
| `Rimfolk_Evt_PilgrimShelter` | pilgrims | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_RiverCatch` | fishers | drawn | 0 2 any | 3, 0, 0 |
| `Rimfolk_Evt_ScholarCopyist` | scholars | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_BellForTheDead` | sextons | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_BellFuneral` | sextons | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_BellVoidSigns` | sextons | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_EggTrade` | tamers | drawn | 0 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_PeltBuyer` | hunters | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_TamerBout` | tamers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_TamerStray` | tamers | drawn | any | 3, 0, 0 |
| `Rimfolk_Evt_WardensHunt` | hunters | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_WardensLantern` | hunters | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_WheelMending` | salvagers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_WheelScrap` | salvagers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_WheelWreck` | salvagers | drawn | 0 1 any | 3, 0, 0 |
| `Rimfolk_Evt_BannerDeserter` | mercenaries | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_BannerHire` | mercenaries | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_BannerRoll` | mercenaries | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_GlassLesson` | seers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_GlassReading` | seers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_GlassRod` | seers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_GuildBid` | artisans | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_GuildCollects` | artisans | drawn | any | 3, 0, 0 |
| `Rimfolk_Evt_GuildCommission` | artisans | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_MossGraveTree` | grovekeepers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_MossSaplings` | grovekeepers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_MossTreeTend` | grovekeepers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_FleetSalvage` | crew | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_FleetShuttle` | crew | drawn | 2 any | 3, 0, 0 |
| `Rimfolk_Evt_FleetSleeper` | crew | drawn | 0 1 any | 3, 0, 0 |
| `Rimfolk_Evt_LineMast` | linesmen | drawn | 0 1 2 3 | 3, 0, 0 |
| `Rimfolk_Evt_LineStorm` | linesmen | drawn | 0 1 any | 3, 0, 0 |
| `Rimfolk_Evt_LineWire` | linesmen | drawn | 0 1 2 3 | 3, 0, 0 |
| `Rimfolk_Evt_RepublicCensus` | citizens | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_RepublicConscript` | citizens | drawn | 0 1 any | 3, 0, 0 |
| `Rimfolk_Evt_RepublicPapers` | citizens | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_UnionDrill` | drillers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_UnionGoods` | drillers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_UnionStrike` | drillers | drawn | 0 1 any | 3, 0, 0 |
| `Rimfolk_Evt_ChoirArt` | listeners | drawn | 0 1 any | 3, 0, 0 |
| `Rimfolk_Evt_ChoirListening` | listeners | drawn | 0 1 any | 3, 0, 0 |
| `Rimfolk_Evt_HelixBatch` | editors | drawn | any | 3, 0, 0 |
| `Rimfolk_Evt_HelixCase` | editors | drawn | 0 1 2 3 | 3, 0, 0 |
| `Rimfolk_Evt_HelixGeneWork` | editors | drawn | 1 any | 3, 0, 0 |
| `Rimfolk_Evt_HelixSample` | editors | drawn | 0 1 any | 3, 0, 0 |
| `Rimfolk_Evt_LatticeLostMech` | mechanitors | drawn | any | 3, 0, 0 |
| `Rimfolk_Evt_LatticeParts` | mechanitors | drawn | any | 3, 0, 0 |
| `Rimfolk_Evt_LatticeRepair` | mechanitors | drawn | any | 3, 0, 0 |
| `Rimfolk_Evt_LatticeTurret` | mechanitors | drawn | any | 3, 0, 0 |
| `Rimfolk_Evt_VaultBarter` | vaultfolk | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_VaultChild` | vaultfolk | drawn | any | 3, 0, 0 |
| `Rimfolk_Evt_VaultCraft` | vaultfolk | drawn | 0 1 any | 3, 0, 0 |
| `Rimfolk_Evt_Disputation` | scholars | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_EnclaveSalvage` | brokers | drawn | 2 3 any | 2, 0, 0 |
| `Rimfolk_Evt_LongWalk` | pilgrims | drawn | 0 1 2 3 any | 4, 0, 0 |
| `Rimfolk_Evt_OrderNurse` | healers | drawn | 0 1 2 3 any | 3, 0, 0 |
| `Rimfolk_Evt_RiverNets` | fishers | drawn | 0 1 2 any | 3, 0, 0 |
| `Rimfolk_Evt_PplR7_Footsore` | pilgrims | drawn | 0 1 2 3 any | 3, 0, 0 |

<!-- peoples_impact_audit: end -->
