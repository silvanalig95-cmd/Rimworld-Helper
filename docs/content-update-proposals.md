# The large content update: proposals (20 November 2026)

The player, 20 November 2026: "since i still have quite some tokens available, i want to make another large content
update, meaning adding further variety to existing mechanics, further enhancing what's already created, further
integrating different features with each other, adding more text variety etc." Nothing here is built. Pick by id (or
say "all", or strike and change any of it), and the picked ones are built together, a round at a time.

Three parts, each written from the mod as it is now (code, defs and the counted text pools): **A, colony life** (37
items), **B, the world around the colony** (32), **C, the gods and faith, the land and its life, and the other mods**
(36). Each item is marked **V** (more variety in an existing mechanic), **E** (enhancing what is already made), **I**
(tying two or more features together) or **T** (more text where the pools are thin), says what is seen on the map and in
the letters, what it ties together, what it draws from the player's mods (active rows of `docs/player-mods/` only), its
size (S, M, L), and any harm. Text follows CLAUDE.md as of 20 November 2026: the earlier flavour style for letters,
wishes, ambitions, life goals, personal stories, council questions, camps and world events; the old medieval voice for
the gods' and faiths' own texts; plain words for the rest.

## First, a fix (the colony is the family)

**A30** is a rule breach found while counting: what a dead colonist leaves (their heirloom, their room) goes to blood
kin first (`FamilyLifeMath.HeirBonus` ranks a parent, child or sibling above a partner), against the player's rule of 5
November 2026 that the colony is the family. The vendetta was already changed to go by closeness; A30 does the same for
what the dead leave. Proposed to build first, as a fix.

## The lead's recommended first round (twelve)

Chosen across the three parts for what ties the most together and is seen most often:

- **A1** (E) Each council seat seen at its work on the map (the speaker, the quartermaster's count, the reeve's dawn
  walk, the marshal on the walls at dusk).
- **A2** (I) The seat whose work it is speaks in the council's question.
- **A14** (I) An ambition reached is seen on the map and kept as a day in the calendar (today 9 of about 67 show
  anything).
- **B2** (I) World events steer which quests and storylines are offered.
- **B1** (I) World events told in the peoples' own ways (none has a people's voice today).
- **B14** (I) Ties (pacts, matches, wards, fealty) bring a people's own events and camps.
- **B23** (I) What the colony is known for draws the quests and camps that fit it.
- **B19** (E) Camps' herds, dogs and goods from the full mod pool (the salt caravan carries salt).
- **C1** (E) Each god's festival kept as its words say (8 of 14 give only the shared feast today).
- **C3** (I) A god's beast at home: the colony's own animal of a god's sacred kind (with C2, sacred animals for every
  god from the player's mods).
- **C14** (I) The raiders' gods heard at the wall, and told by a prisoner.
- **C17** (I) The gods, the beasts and the land in the colony's art (Rimfolk reads none of the game's art stories today).

Then a text round from the three parts' T items (A31, B9, B10, B16, B18, B30, C31 to C36), and the rest by the
player's pick. Each part's own recommended round is at its end.

## Proposals A: colony life

The player, 20 November 2026: "since i still have quite some tokens available, i want to make another large content
update, meaning adding further variety to existing mechanics, further enhancing what's already created, further
integrating different features with each other, adding more text variety etc."

This is a list to pick from. Nothing here is built. It covers the council (questions, decrees, seats, elections,
petitions, the meeting), wishes, ambitions, life goals, the heart and time off, personal stories, character marks,
deeds and names, groups, gossip and talk, journals, the calendar and its customs, children and elders, everyday family
flavour, and the chronicle and annals. Trait changes are left out (`docs/traits-proposals.md` has them).

**Kinds.** **V** more variety in an existing mechanic, **E** something already made made deeper or better shown,
**I** two or more features joined, **T** more lines in thin text pools. **Size**: S (defs and a small worker), M (a
worker, a hook or a small play-out), L (several hooks and a new play-out).

**Text style.** The player's message of 20 November 2026 (relayed by the lead): "please restore letters, wishes,
ambitions, life goals, personal stories, council questions, camps, and world events to just the previous text, to kind
of the flavor style text that you created before ... and keep that also in mind for additional content to be added".
So every new line proposed here for those eight parts (in this list: letters, including the year letter; wishes;
ambitions; life goals; personal stories; council questions, including the seasonal customs, which are council
questions) is written in that earlier flavour style, as the text read before the November plain-words pass, and still
says only what happens on the map. Everything else (journals, talk, bubbles, social events, gossip, seats, decrees,
messages) follows CLAUDE.md's plain rule. Each item that adds text says which. Spoken bubble lines stay one sentence.
Seats are always named in the colony's age (`OfficeNames.InAge`, an `{OFFICE}` placeholder), never by the medieval word.

**What is there now** (counted from the defs): about 240 council questions (39 of them seasonal customs), 36 decrees,
12 seats, about 120 wishes, about 67 ambitions (9 with something on the map when reached), about 100 life goals,
39 personal stories, 57 character marks, 49 deeds, 71 names, 13 group habits and 26 kinds of group, 24 talk topics,
about 130 social events, 40 shared memories, 71 quirks. The text variety report (`Tools/variety_report.py`, run
read-only for this list) finds the colony-life pools mostly well stocked; the thin ones are named in the T items.

**What this list leans on, found while reading.** Four seats (speaker, quartermaster, reeve, marshal) have no act
anyone sees; seat holders speak in 5 of 240 questions; groups touch no wish, seat or day; decrees touch no wish, story
or group; only 9 ambitions show anything when reached and none reaches the calendar; the meeting decides the ambition
and nothing else; nothing reads Vanilla Aspirations Expanded or Vanilla Christmas Expanded, both active in the player's
list; and what the dead leave still goes to blood kin first (A30).

---

### The council: seats, questions, the meeting and decrees

**A1 (E) Each seat seen at its work: the speaker, the quartermaster, the reeve and the marshal.**
Eight seats already do something you can watch (the chaplain sits with the grieving, the warden goes to the camp's
fire, the elders teach, the physician does rounds). These four work only through numbers. Each gets one act of its
own, face to face as the others' are (`Together.When`): the speaker sits an hour with the colonist whose trust in the
council is lowest and hears them out (their trust +1 to +2, a line each, a news line "Harper heard Barr out"); the
quartermaster walks the storerooms once a week at dawn and the count goes in the news and This week (days of meals,
medicine, what is shortest), as the seat's own promise ("a count of the stores every week") says; the reeve walks the
fields at first light in the growing season and marks blighted plants for cutting (the game's own order), saying so;
the marshal walks the walls or the gate at dusk for an hour in the two days after raiders were seen (the patrolling
vigil the night watch already uses, `DilemmaEffect_Vigil`) with a word for whoever is on watch.
*Ties:* `Interplay/Interplay_Offices.cs` and `_Offices2.cs`, the seats' duties lines (`Defs/OfficeDefs/`), the seat's
work count for elections (`OfficeWordsDef.measure`, `RecordMath`), news, This week. *Mod pool:* RimFridge and the deep
storage mods' shelves count as stores, as the stores code already reads them. *Size:* M. *Harm:* none. *Text:* plain
(seats). A bubble: "The medicine shelf is lower than I'd like." (said only when it is).
**Built** (20 November 2026, the council's integrations, part 1, not released yet; DESIGN.md "The council at its work,
in its questions and at its meeting"). Each act is a play-out of the colony's own that never takes a gathering's place:
the speaker sits an hour at a table (trust under 10, every three days); the quartermaster's round of the stores at
dawn, its count in the news and at the head of This week's plan; the reeve's walk at first light every other day in the
growing season and the next morning for blight; the marshal's walk on the walls at dusk the two days after an attack.
The marshal's dusk walk is kept only with "Answers play out on the map" on.

**A2 (I) The seat whose work it is speaks in the question.**
The questions about a seat's work name its holder as the one asked (role B, which already takes an `office:` tag, as
`Council_Dilemmas_Seasons.xml` does for the quartermaster) and give them a voice in the opening and the outcome: the
physician in the sickness questions (`Council_Dilemmas_Life.xml`: the last of the medicine, keeping the sick apart, a
sick cook, the tired doctor), the reeve in the field ones (the empty field, what the field grows, blight), the
herdmaster in the animal ones (the herd or the stores, too many in the pens, training an animal), the peacemaker in
quarrels (a quarrel that has grown, trouble at every turn, the sworn rivals), the keeper of days in the customs, the
chaplain in the faith questions, the warden of guests in strangers' and camps' questions, the quartermaster where the
stores are spent. Their answer counts toward the seat's record that the next election reads.
*Ties:* `Defs/DilemmaDefs/` and their Variants files (tags `[b:office:Reeve]`...), `OfficeNames`, `RecordMath`, A21.
*Size:* M (about 60 new opening and outcome variants, little code). *Harm:* none. *Text:* council questions, so the
earlier flavour style. A sample opening: "[b:office:Reeve] {B_nameDef} has walked the bare field twice since sunrise,
kicking at the clods. As {OFFICE_B} {B_pronoun} would like it sown by tomorrow, and would like it known that nobody
asked {B_objective} before it went bare."
**Built** (20 November 2026, part 1, not released yet). The holder is a role of its own (`{HOLDER_...}`, `{OFFICE}`, tags
`[seat]` and `[seat:Reeve]`) rather than B, since many of these questions already have a B; 34 questions name a seat, every
seasonal custom is the keeper of days', 26 have an opening and an outcome of their own for the seat and the rest the
holder's word after the text (69 lines in all). Each one answered counts toward the record the next election reads
("spoken for the seat in 3 of the council's questions").

**A3 (V) A seat holder asks to step down.**
A new question when a holder has been worn down or near breaking for five days (the heart), or laid up for ten days,
never during an election. Answers: let them step down (the seat empties and the by-election comes on the map as it
does now; their strain eases; the deed "stepped down from a seat", A23); give them a deputy for ten days (the next best
does the seat's daily act, from A1 or the existing acts, and the holder gets the heart's day off at once); or ask them
to stay (the devoted trust the council more, their strain stays, a line). The letter shows the holder's heart band as
every card does.
*Ties:* Offices (`OfficeTracker`, by-elections), Heart (bands, `HeartTimeOff`), A1, A23. *Size:* M. *Harm:* none.
*Text:* council question, flavour style: "{A_nameDef} came to the council with the {OFFICE}'s keys wrapped in a cloth
and set them on the table. 'Someone else should carry these for a while,' {A_pronoun} said, and didn't sit down."
**Built** (20 November 2026, colony life's variety, part 1, not released yet; DESIGN.md "Colony life's variety, part 1: the council's situations and its everyday questions"). "Five days worn" is read from the
heart's new since-when (strain 55 or more), "laid up ten days" from the game's own age of the illness or wound keeping
them abed; B, the one who would stand in, is named in the letter (the most able at the seat's work holding no seat). The
deputy does every act and word of the seat for ten days (`OfficeTracker.Acting`); the holder has the heart's day off at
once, or the rest of the day when one can't be given yet. The deed "stepped down from a seat" is the first of A23's deeds.

**A4 (V) Two wishes, one pile of steel.**
When two colonists' open wishes each need the same stuff to build and the stores hold enough for only one (steel,
wood, components, cloth: the wish's thing and its cost), the council is asked which comes first. Answers: A's first
(A's wish promised and its own promise play-out runs; B's deadline moves on three days and B is told why, a smaller
sting than being passed over), B's first, or the one who asked first (the patient approve). The quartermaster is the
one who notices, when there is one.
*Ties:* Wishes (the worker's thing, promises, `Kept`, trust), council questions, A2. *Size:* M. *Harm:* none.
*Text:* council question, flavour style: "There are ninety bars of steel in the store and two people with plans for
every one of them. {A_nameDef} has chalked an outline on the bedroom floor. {B_nameDef} has chalked one over it."
**Built** (20 November 2026, part 1, not released yet). A is always the one who asked first, so the answers are first
come first served (A's), B's first, and neither until there's more; whoever waits is told why face to face by the
quartermaster or the other asker. The stuff is the wish's thing's fixed costs and its stuff (the kind the stores hold
most of), ten at least each.

**A5 (I) A promise in the seat, halfway through.**
The election's promises are judged only at the next yearly vote. Halfway (25 days or more in the seat), when the map
clearly shows a promise going badly by the very test the vote uses (`RecordMath.Judge`: the meals put by running out,
someone lost after "a proper watch at night"), someone who didn't vote for the holder raises it. Answers: give them
hands (a work party of two on the seat's own work for an afternoon: cooking, sowing, a night's vigil, a round of the
sick), have them answer for it at supper (a word at the table; their "weight of office" story moves to its hard call,
A21), or leave it to the vote (nothing now; judged as before).
*Ties:* `Offices/RecordMath.cs`, `ElectionPlanks`, the history's `office:promise`, PlayOut work parties, Arcs. *Size:* M.
*Harm:* none. *Text:* council question, flavour style.
**Built** (20 November 2026, part 1, not released yet). The ballots aren't kept by voter, so the one who raises it is
someone who stood against the holder for the seat (now kept with the promise), else whoever thinks least of them. Only
promises hands can help are raised (a watch, the food, the sick, the herd, the spirits); the food is brought in
(harvest, fishing, hauling) rather than cooked, the game having no cooking work party. Each promise is raised once.

**A6 (I) Any other business: petitions read out at the meeting.**
After the ambition vote at the seasonal meeting, each open decree petition (for a decree or against one, the existing
`DecreeRequest`) and each open petition of wishes is read out by its asker, one line each; those present say aye or no
by how they feel about it (the decree's reaction to them, or wanting the same thing), and anyone there who wants the
same thing joins the petition (the existing co-signing). The tally stays on the petition ("backed at the meeting by 5
of 7") on the Decrees page, in This week and in the meeting's letter. The council still decides from the page, as now.
*Ties:* `Ambitions/CouncilMeeting.cs`, `MeetingAttendance`, `Decrees/Decrees.cs`, `Wishes/Petitions.cs`,
`UI/DecreePanel.cs`, the inbox. One small saved tally a petition (a model in `Tools/save_estimate.py`). *Size:* M.
*Harm:* none. *Text:* plain (meeting lines and the page), the letter's paragraph in the flavour style of letters.
**Built** (20 November 2026, part 1, not released yet). Read out during the meeting once the ambitions are put (the
vote's count is at its very end), four at most; the tally is three numbers on the petition (values, so no new saved
collection needed a model).

**A7 (V) Two decrees that show on the map: the night watch, and a welcome for newcomers.**
The night watch (six days): each night two of the colony's fighters take turns walking the walls and the gate from nine
to one (the patrolling vigil, run each day by `DecreeWork.RunDaily` like the hunting season), the thefts and night
nuisances that a night watch already halves are halved, and the watchers sleep late; night owls and the nervous like
it, the lazy hate their turn; it counts toward the deed "kept the night watch" and the Night-Watch name.
A welcome for newcomers (ten days; asked for when someone here less than twenty days stands outside every group): each
evening a settled colonist who likes them spends an hour with each newcomer, groups take newcomers in faster (the
sponsor and welcome the groups already have), and the newcomer's "somewhere to belong" story moves on. The aloof and
the wary dislike it. Each age has its words (a tribe's welcome at the fire, a spacer colony's orientation week).
*Ties:* `DecreeDef.daily`, a new `DecreeMoment`, Groups (`GroupMath`), Arcs (`ArcWorker_Newcomer`), deeds and names,
setbacks. *Size:* M. *Harm:* none added; a watcher out at night meets whatever the game sends. *Text:* plain (decrees).
**Built** (20 November 2026, part 1, not released yet). The rota goes round the colony's fighters in a fixed order, two a
night (one when there is only one); the watch is asked for after an attack. The welcome's hour is a sitting at a table
where there is one; it counts as welcomes for the group the newcomer is finding their way into (two from its own people),
the host speaks for them there when fond enough, and it is the welcome the newcomer's story waits for when the host is
its friendly face. "Sponsor" read as the group's own speaking for a newcomer; groups' "welcome" as their daily welcomes.

**A8 (V) Coming of age: four more paths.**
A child who comes of age is offered one of four apprenticeships today (crafting, medicine, scholarship, fighting:
`Council_Dilemmas_ComingOfAge.xml`). Four more with the colony's best: the fields (B the reeve when there is one), the
kitchen, the animals (B the herdmaster), and building. Each is a real mentorship and a first lesson on the map, as the
four are.
*Ties:* `Lives/Legacy.cs` (`DilemmaWorker_ComingOfAge`), the social web's mentorships, A2. *Size:* S. *Harm:* none.
*Text:* council question, flavour style: "{B_nameDef} has taken to leaving the gate to the pens open whenever
{A_nameDef} is about, and pretending not to notice who closes it."

### Wishes and petitions

**A9 (V) A day off for a friend.**
A kind colonist, or a close friend of someone worn down or near breaking for two days, asks the council for a day off
for them. It is met when that colonist is given the heart's day off (the existing button) or a rest day comes while the
wish waits. Then the asker's thanks, the two grow a little closer, and the asker often keeps them company for an hour
of it (the day off's own "a friend sent for"). Let lapse, the asker's trust drops; the one worn down isn't told.
*Ties:* Heart (bands, `HeartTimeOff`, the heart's advice), Wishes (a wish for another), the social web. *Size:* S.
*Harm:* none. *Text:* wishes, flavour style: "{OTHER_nameDef} hasn't laughed in a week, and still gets up before anyone.
Give {OTHER_objective} a day. I'll do {OTHER_possessive} share."

**A10 (I) A place for the group.**
A close group (the card's "close", cohesion 50) whose first habit has nowhere to happen asks the council for its place:
the fireside circle a fire pit with seats round it, the readers a bookcase and chairs, the game night a game table, the
musicians an instrument, the faithful a shrine. The anchor asks and every member at home signs (one petition, the
existing machinery). Met when it is built: a first evening there (the habit called as now, counted in the week's
gatherings), and the group's habit can be called to it from then on.
*Ties:* Groups (`GroupHabitDef`, `GroupMath.CloseAt`), Wishes (`Petitions`, the "have it" worker), the week's count
(`Stage`). *Mod pool* (active rows): [sbz] Bookcase (`sbz_Bookshelf`, `sbz_TwoTierBookcase`), Vanilla Furniture Expanded
and its Art and Props and Decor modules, Alpha Props - Parks and Gardens (benches), Royalty's instruments, Ideology's
altars and Vanilla Ideology Expanded - Memes and Structures; the game's campfire, chess table and stools when none fit.
*Size:* M. *Harm:* none. *Text:* wishes, flavour style: "Every evening we drag the same four stools out to the same cold
ashes. A proper fire pit would do. We'd keep it lit."
**Built** (20 November 2026, not released yet; DESIGN.md "Colony life's integrations, part 2"). "First habit" read as the
first of the group's habits that wants a place of its own (no group kind lists reading or music first); a close group with
its anchor and two more at home.

**A11 (V) A toy for the little one.**
A parent, an elder or a child's close grown-up friend (any of them: nothing here follows family lines) asks for a toy
for a child of three to twelve, made at the bench. With Vanilla Christmas Expanded it is a wooden, plushie or metal toy
(`VCE_Toy_Wooden`, `VCE_Toy_Cloth`, `VCE_Toy_Metal`, from `VCE_MakeToy_*`); without it, a small sculpture for the
child's room. Met when it is made: the asker carries it to the child (`PlayGive`), and the child's mood is the mod's own
toy thought where it has one.
*Ties:* Wishes (for another), children (Biotech), `PlayOut/OnMapPlays.cs`. *Mod pool:* Vanilla Christmas Expanded
(active). *Size:* S. *Harm:* none. *Text:* wishes, flavour style: "{OTHER_nameDef} has been talking to a stick with a
face scratched on it for a week. {OTHER_pronoun} deserves a better friend than that stick."

**A12 (E) The wished-for thing, tried out.**
When a wish for a thing or a room is met, the thanks are said wherever the asker happens to be. Instead, in their free
time within the day, they go to it: sit in the new chair, lie on the new bed a moment, stand before the art, warm their
hands at the new heater, and say the thanks there; a friend nearby now and then admires it. If it can't be reached, the
thanks stay as now.
*Ties:* `Wishes/Kept.cs` (`Notify_Granted`), a small job like the heart's acts, `Speech.Say`, `Together`. *Size:* S/M.
*Harm:* none.
**Built** (20 November 2026, not released yet). With "Answers play out on the map" on; the lasting wishes' own kinds (a
thing, a bed, a room); a friend admires it a third of the time.

**A13 (I) A wish talked up finds backers.**
Colonists already bring up their open wish in conversation (`InteractionWorker_TalkAboutWish`, the social log). For a
wish many may back (a rec room, a sick bay, a council meeting: `allowPetition`), a listener who would want the same can
join it there and then (the co-signing the wish roll already uses, `CouncilGameComponent.CoSign`), with a line over both
heads. A petition grows this way once a day at most; the roll's own co-signing is unchanged.
*Ties:* `Interactions.cs`, `Wishes/Petitions.cs`, `Speech`. *Size:* S. *Harm:* none.
**Built** (20 November 2026, not released yet). Half the time for someone who would want the same, more for a friend; who
would want the same is the meeting's own rule (A6's `WantsSame`).

### Ambitions and life goals

**A14 (I) An ambition reached, seen on the map and kept on the calendar.**
Nine ambitions do something when reached (a feast, a toast, a games night). The others get a short scene of what was
built, led by the one who proposed it: a real hospital or a proper infirmary, the physician shows the colony round the
ward; the workshop, the first piece made there; rooms of our own, an evening of visits to the new rooms; turrets or the
walls, the marshal walks the line at dusk; the greenhouse, its first harvest at supper; keep the lights on, the lamps
lit for an evening together; rest for the dead, a quiet hour at the graves; school days, the first lesson at the new
desks. The day it was reached goes on the colony calendar as a day of its own kind ("the day the hall was finished"),
kept or let pass, toward a tradition, as the other days are.
*Ties:* `AmbitionDef.achievedEffects` through `PlayOuts.Run`, Calendar (a new `CalendarKind`, within the calendar's
twelve), the week's count (chosen things always count), the chronicle. *Size:* M/L. *Harm:* none. *Text:* ambitions,
flavour style: "The hall is finished, and {PAWN_nameDef}, who first said it could be done, is walking everyone round it
as if none of them had ever seen a wall before."
**Built** (20 November 2026, not released yet). All 57 others have a scene; the seat whose work it is leads where someone
holds it; up to three such days on the calendar. The ambitions' own letters are unchanged: the letter adds when the
scene will be, so no line claims a walk round before it happens.

**A15 (V) Three ambitions of the colony's people.**
Nobody left out: every grown colonist belongs to a group or has a friend (`GroupMath.WhyOutside`, the social web's
ties), in stages as each outsider finds a place; proposed when two or more stand outside every group. A season of kept
promises: fifteen days running with two or more wishes promised and none let lapse after a promise; proposed after a
promise broken. A lighter season: fifteen days with nobody worn down or near breaking (feelings on); proposed when
someone is worn down. (A calm season counts mental breaks; this one counts the heart.)
*Ties:* Ambitions (`AmbitionGoals`), Groups, `Wishes/Kept.cs`, Heart. *Size:* M. *Harm:* none. *Text:* ambitions,
flavour style: "Barr eats alone, works alone and goes to bed early. That isn't a choice, {PAWN_nameDef} says. That's a
colony that hasn't made room."

**A16 (V) Life goals for the other seats.**
A life goal may be a seat today: any seat, or the marshal's, physician's, steward's or quartermaster's
(`AspirationWorker_Office`, XML only). Seven more: the reeve's, the peacemaker's, the keeper of days', the herdmaster's,
the chaplain's (Ideology), the warden of guests' and an elder's seat. Each is sparked by the seat's own work (a harvest
in, a quarrel mended, a day kept, an animal tamed, a rite led, a camp greeted) and by losing an election, and a goal of
a seat already counts in the vote (`BondsMath.SeatWant`).
*Ties:* `Defs/AspirationDefs/`, Offices, elections. *Size:* S. *Harm:* none. *Text:* life goals, flavour style:
"Somebody has to know which field wants resting and which wants turnips. I've been that somebody for a year, without
the chair."

**A17 (I) Vanilla Aspirations Expanded's aims, marked by the colony.**
Vanilla Aspirations Expanded (active) gives colonists aims of its own, a fulfilment need (`VAspirE_Fulfillment`) and a
letter when one is reached (`VAspirE_Fulfilled`). Rimfolk reads that letter as it comes: the colonist's closest friend
raises a cup to it at the next gathering (as for a life goal, `Stage.Cup`), a journal line, a chronicle line and the
deed "reached a life goal" for gossip; the heart's need for purpose counts the fulfilment need where it is. Aims the
colony wouldn't toast (to kill someone, to overdose, to eat human meat and the like) get the journal line only.
Rimfolk's own life goals come a third less often to someone who has the mod's aims, so nobody chases six at once.
*Ties:* `Aspirations/GoalMath` (the rate), `Heart/HeartNeeds.cs`, `PlayOut/Stage.cs`, `Social/Gossip`, the journal.
*Mod pool:* Vanilla Aspirations Expanded, read by name. *Size:* S/M. *Harm:* none.
**Built** (20 November 2026, not released yet). The aim is the one whose label the letter names; 22 of the mod's 76 aims
are not toasted; an aim the letter doesn't name gets the journal line only.

### Personal stories

**A18 (V) After the vote.**
For someone voted out of a seat they held a year or more. A sour few days; then a word with the winner, who shows them
the seat's work or asks their advice (or with a friend); then either they help the winner for a few days (practice in
the seat's work, and the grudge that "a seat lost is felt" leaves eases) or they keep it, and the story ends with a run
at the next election noted. Each gate passes only on what happened.
*Ties:* Offices (voted out, the grudge), a new arc worker, the social web, A16. *Size:* M. *Harm:* none. *Text:*
personal stories, flavour style: "{PAWN_nameDef} handed over the {OFFICE}'s ledger with every page in order, and one
page more that said only: good luck."
**Built** (20 November 2026, part 1, not released yet). The word comes from the winner, so it begins only with the winner
at home; helping them is an afternoon's lesson at their side and the rivalry eased; "a run at the next election noted"
is in the journal and the chronicle. The council now keeps when each seat's holder took it.

**A19 (V) The one sent away.**
After a banishment, for the colonist closest to the one sent out (the one who objected in the letter, else the closest
by opinion): a hard day; a walk to the edge of the map where they left (the story's own short walk); a talk with a
friend or the chaplain; then better days, or, if the one sent away ever comes back to the gate, a wish to let them in
(the "joining the colony" wish already there).
*Ties:* `Social/BanishDilemmas.cs`, Arcs, Wishes, the chaplain's day. *Size:* M. *Harm:* none. *Text:* personal stories,
flavour style.
**Built** (20 November 2026, part 1, not released yet). The closest is the one who would stand by them (the banishment
letter's own rule), else the one who thinks best of them. If they come back to the gate, the colonist walks out to meet
them; the game's own joiner letter is the choice to let them in, so no wish is made (the "joining the colony" wish holds
only for prisoners).

**A20 (V) Worn thin.**
After the heart's own episode ends (`Council_Heart_WornOut`), or a week near breaking. A quiet day; a word from the
closest friend, who comes over; a day off taken (the heart's day off or a rest day, so the player's own button moves
the story); then back to themselves (strain at 30 or under for two days, a gate the stories already have). Its end
lifts some of the weight and gives the friend a small mark of kindness.
*Ties:* `Heart/Episodes.cs`, `HeartTimeOff`, `HeartLogic`, Arcs (`ArcHold.Strain`), A9. *Size:* S/M. *Harm:* none.
*Text:* personal stories, flavour style.
**Built** (20 November 2026, part 1, not released yet). The friend sits with them at a table; a day off is the heart's
(running or given since the stage began) or the calendar's rest timetable today; with none in twelve days it comes to
nothing. The friend's mark is a new small one, "stood by a friend" (warmth, outlook).

**A21 (E) The weight of office: the hard call made real.**
The third stage of "the weight of office" (`Rimfolk_Arcs_World.xml`) says "Had to make a call today that somebody won't
like" after a few days in the seat, with nothing on the map behind it. It waits instead for a real call: the holder
named in a council question about their seat's work (A2), a decree they backed that someone hates, or the supper of A5;
its journal and chronicle name it. With none in twenty days the story ends quietly instead of claiming a call.
*Ties:* Arcs (a custom gate, `ArcWorker_Office`), the dilemmas' resolve, decrees. *Size:* S. *Harm:* none. *Text:*
personal stories, flavour style.
**Built** (20 November 2026, part 1, not released yet). "A decree they backed" is one they asked for or like, declared
while someone at home hates it. The quiet end claims no call and brings no mood; the mood of a seat lost too soon now
comes only when the seat was lost.

### Character, deeds and names

**A22 (V) Marks from the colony's own life.**
Character marks come from fights, losses, faith and the body; from the council and the colony's own life only a seat
given or lost. Five more, each read by nature as the others are: reached a life goal (pride, outlook); saw their idea
built (an ambition's proposer: openness, pride); a custom they first asked for became a tradition (warmth, openness
down a little); a year in a seat with the promise kept (pride, trust); a personal story that ended well (warmth,
outlook).
*Ties:* `Persona/` marks (`Rimfolk_PersonaMarks_Life.xml`), one-line hooks in Aspirations, Ambitions,
`CalendarCustoms`, Offices and Arcs; the character page; journals. *Size:* M. *Harm:* none. *Text:* plain (journals).

**A23 (V) Deeds and names of the council's life.**
Deeds, for gossip and a name: kept a promise in the seat, broke a promise in the seat (from the history's
`office:kept` and `office:broken`), spoke up for someone before the council (the friend named objecting to a
banishment or making a plea), stepped down from a seat (A3). Names for long service in the seats that have none, as Old
Doc and Old Iron have: a days-in-the-seat record for the quartermaster, reeve, herdmaster, steward and elders, and a
name at 120 days (to choose; for instance Old Keys, Old Furrow, the Drover, the Host, the Old Voice), and Word-Kept for
two promises judged kept.
*Ties:* `Defs/DeedDefs/`, `Epithets/` (`NameRecords`), Offices. *Size:* S/M. *Harm:* none. *Text:* plain.

### Groups, gossip and talk

**A24 (V) Group habits from the colony's pastimes.**
Four habits, kept when two or more of a group do them together, as reading and music are: fishing together (Odyssey's
`Fish`), the hot water (Dubs Bad Hygiene's pool and hot tub, `UseDBHPoolTiles`; ReGrowth's hot spring, `RG_Bathe`), a
pot of tea or coffee at one table (Vanilla Brewing Expanded - Coffees and Teas; the morning cup is already seen), and
cards, chess or darts (`Play_Poker`, `Play_Chess`, Vanilla Furniture Expanded's `Play_DartsBoard`). Each is also a name
a friendship group may take ("the anglers", "the tea table").
*Ties:* `Social/Groups/GroupHabits.cs` (a new `where` each), `GroupShareDef`, `Lives/LifeNew.cs`, the heart (a habit
kept eases strain). *Mod pool:* as named, all active; the game's chess and poker and Odyssey's fishing without them.
*Size:* M. *Harm:* none. *Text:* plain (habit lines).

**A25 (V) Social events and talk of the council's seasons.**
Six social events: help with the speech (a candidate and a friend the evening before the count); the old holder shows
the new one the seat's things (the physician's shelf, the reeve's seed store); a promise remembered (someone reminds a
holder of their election promise, warmly or not, by the record); two who back rival candidates for one seat at odds;
a petition talked over by two of its signers; an elder's word to a young holder. And a talk topic, "the vote", only
while an election is under way, with lines on whom they lean to, read from the vote's own lean so the words match; it
never moves the vote.
*Ties:* `Defs/SocialEventDefs/`, `Defs/TopicDefs/` and `Social/Talk/TopicWorkers.cs`, `Offices/Election.cs`, Petitions.
*Size:* M. *Harm:* none. *Text:* plain.

### The calendar, customs and festivals

**A26 (V) The giving day (a winter custom).**
In winter someone asks that everyone give one other person something by hand. Answers: the giving day (names drawn so
everyone gives and gets; each takes something worth giving from the stores, by what the other wants or lacks: warm
clothes, a book, a drink, a carving, a toy for a child; each carries theirs over at supper, `PlayGive`, and they eat
together); the children only; not this year. Kept three years running it is a tradition like the other customs.
*Mod pool:* Vanilla Christmas Expanded (active): the toys, gingerbread (`VCE_Gingerbread`), eggnog (`VCE_Eggnog`), hot
cocoa (`VCE_HotCocoa`), milk and cookies (`VCE_MilkAndCookies`), and its fairy lamps (`VCE_FairyLamps`) lit over the
table if built; the game's novels and Vanilla Apparel Expanded's hats and scarves as gifts; otherwise whatever the
stores hold. Every age has its own gifts (carved bone and dried fruit for a tribe, store goods for a spacer colony). If
the colony's faith keeps the mod's festive feast (`VCE_FestiveFeast`), the giving comes with that feast.
*Ties:* `Calendar/CalendarCustoms.cs`, `PlayOut/OnMapPlays.cs` (`PlayGive`), `PlayFeast`, the wishes (what each wants),
the social web (giver and receiver), children. *Size:* M. *Harm:* none. *Text:* a council question, flavour style:
"{A_nameDef} has been whittling something behind {A_possessive} back for a week and says it isn't for anyone in
particular. It plainly is. {A_nameDef} thinks the whole colony could do the same, one evening this winter, and then
nobody would have to pretend."

**A27 (I) The year's first supper: aims said aloud.**
A custom for the first days of the year (spring): at supper each says what they mean to do this year. Answers: everyone
says it (a supper gathering; each with a life goal says it in one line; someone without one may choose one there and
then, from what fits them, `GoalMath`, and say it; a goal said aloud brings the speaker and those who back them a
little closer); only those who want to; not this year. The year letter later reads it back ("At the first supper Toni
said she would train the dogs. She did.").
*Ties:* Calendar customs, Aspirations (`GoalMath`), `World/YearLetter.cs`, `Speech`. *Size:* M. *Harm:* none. *Text:* the
question in the flavour style; the lines said at table plain, one sentence each.
**Built** (20 November 2026, not released yet). The year's first week (days 0 to 7), any season; asked by someone with a
life goal.

**A28 (V) The elders' day (an autumn custom).**
With two colonists of sixty or more, or an elder's seat held, someone young asks to honour them: the elders sit at the
head of the table and are served first (`PlayFeast` with them as its guests of honour), each tells one thing from their
life, and the children bring them something they made. Answers: the whole day, supper only, not this year.
*Ties:* Calendar customs, `PlayFeast`, the elders' seat (`Interplay_Offices2.cs`), children, the elders' questions in
`Rimfolk_Dilemmas_Lives.xml`. *Size:* S/M. *Harm:* none. *Text:* the question in the flavour style.

### The chronicle and the annals

**A29 (I) The year read aloud.**
The year letter is written on the landing's anniversary, but nobody reads it. That evening, at the founding day's
gathering or at supper, its writer stands and reads a few lines of it aloud (three to five, one sentence each: who came
and who was lost, the work built, the names earned), and those there share a memory of the year read together. The
colony's record keeper (the answer "Make A keeper of the colony's record", `Council_DlmLv_RecordKeeper`) writes it from
then on, counting toward the Annalist's name. With Vanilla Books Expanded the writer writes it up at the writer's table
(`VBE_WritersTable`) as a book called "The second year" that stays on the colony's shelf; with its printing
(`VBE_Printing`) as a news sheet (`VBE_Newspaper`); without the mod, the reading alone.
*Ties:* `Annals.cs`, `World/YearLetter.cs`, `Epithets/` (the Annalist), the record keeper question, the founding day,
`SharedMemoryDef`. *Mod pool:* Vanilla Books Expanded, [sbz] Bookcase (active). *Size:* M. *Harm:* none. *Text:* the
letter in the flavour style of letters; the lines read aloud plain.
**Built** (20 November 2026, not released yet). The book is the game's novel (the newspaper with printing), on any
bookcase with room ([sbz] Bookcase's included), else by the table.

### Everyday family flavour

**A30 (E) What the dead leave goes to whoever was closest.**
A dead colonist's heirloom (`Lives/Legacy.cs`) and their room (`Social/FamilyInheritance.cs`) go to family first:
`FamilyLifeMath.HeirBonus` puts a parent, child or sibling (80) above a partner (70), and the room goes to "the closest
of their family here". That is a mechanic on family lines, which the player's rule of 5 November 2026 rules out; the
vendetta was changed the next day to pass by closeness (`Secrets/VendettaMath.cs`). The same measure for both: the
game's opinion of the dead, the social web's warmth, a partner as one part among them, blood no longer first; the room
to the closest who has no room of their own. The words still say what they were to the dead.
*Ties:* `Legacy`, `FamilyInheritance`, `FamilyLifeMath`, `VendettaMath` shared, the tests. *Size:* S. *Harm:* none.
This one is a rule fix and comes before the rest (CLAUDE.md: bug fixes before features).

### Text variety (counted with `Tools/variety_report.py`)

**A31 (T) Journals of the council's life.** `Council_Journal_Elected` has 8 lines (3 plain, one in each tone) for about
11 entries a year, the thinnest for its use: +16. `Council_Journal_GoalChanged` 8: +16, tagged by what changed it (a
loss, a brush with death: the only two that change a goal now). `Council_Journal_MovedDown` 3: +9.
`Council_Journal_Epithet` 22 (only 2.7 effective for a speaker): +14. `Council_Journal_TieFormed_Comrades` 22 and
`_Friends` 25: +12 each. `Council_Journal_Anniversary` 23: +10. `Council_Journal_Homecoming` 9: +10. About 100 lines,
plain. Sample: "They made me {OFFICE}. I went back to work afterwards, because nobody said what else to do." *Size:* S.
**Built** (20 November 2026, not released yet; DESIGN.md "Colony life's text variety"). Recounted first (unchanged): 99 lines in `Rimfolk_Lines_ColonyLife_Journals.xml`, the counts as proposed. The changed goal's entry is now written with its spark (`WriteTagged`), its new lines tagged `[loss]` or `[downed]`. A voice of one line got one more, so it is still drawn with the plain lines.

**A32 (T) Teasing, jokes and shared memories in talk.** Eighteen teasing pools (`Rimfolk_TL_*_Tease`: food, work,
growing, the council, the future, the colony's project, outside, art, prisoners, the calendar, drugs, wealth, tech,
raids, children, love, the camp, health) have 8 to 14 lines each, only in the plain and cheerful voices: +6 each (about
110). Five closing pools of shared memories have 3 lines each (`Rimfolk_TL_Mem_Nursed_Close`, `_CloseCall_Close`,
`_Repaid_Close`, `Rimfolk_TL_Joke_Downpour_Close`, `_Snowball_Close`): +7 each. `Rimfolk_TL_Joke_JokeHeard_Close` 10:
+6. `Rimfolk_TL_Mem_ChildFound_Open`, `_Reply`, `_Close` 11, 11 and 7: +6 each. About 170 lines, plain, one sentence each
where said aloud; old talk left as it is. *Size:* S.
**Built** (20 November 2026, not released yet; DESIGN.md "Colony life's text variety"). 167 lines in `Rimfolk_Talk_Lines_Teasing.xml`: six for each teasing topic, two plain and one each in the cheerful, warm and blunt voices and the gloomy or the cold, since those who tease in their own voice draw them (`TalkMath.Voice`); the closing words as proposed, one in each voice and the rest plain.

**A33 (T) Decree voices for the thinnest decrees.** Lines for and against, extension files counted: every faith its
evening 4 and 3, foraging days 5 and 3, saving the power 5 and 3, wood for the winter 6 and 3, one ideoligion 4 and 7,
a hunting season 8 and 4, days of rest 8 and 7. +6 on each side under 8 (about 70), tagged by the traits that care, as
`Extra_DecreeLines.xml` does, and by age where the decree's form changes (banked fires for a tribe). Plain. *Size:* S.
**Built** (20 November 2026, not released yet; DESIGN.md "Colony life's text variety"). 72 lines on the twelve sides under eight (a hunting season's and days of rest's support already had eight), in `Rimfolk_DecreeLines_ColonyLife.xml`; four plain lines a side, most tagged by a trait or faith of the decree's reactions or by the age of its form (the berry days, the gleaning, the hunters' moon, a cull), and two in voices that had none or one. Saving the power is for a colony on the grid, so its lines are for the industrial and spacer ages.

**A34 (T) The council's answers to wishes.** The wish scene pools of `Council_Scenes.xml`: `Council_Pool_MeToo` 32
(about 10 draws a year), `_Bitter` 24, `_BrokenPromise` 21, `_PassedOver` 22, `_SelfHelp` 25, `_Promised` 26: +10 each
(60). Wishes, so the flavour style. Samples: "Ask the council? I did. I've still got the answer somewhere, folded very
small." / "If {OTHER_nameDef} is getting one, I'd like it noted that I asked first, quietly." *Size:* S.
**Built** (20 November 2026, not released yet; DESIGN.md "Colony life's text variety"). 60 lines in `Rimfolk_Lines_ColonyLife_Wishes.xml`, one in each voice and five plain for each pool, each one sentence since it is said aloud; the pools' own people kept (`{ENVIED_...}` for asking because someone got one, `{OTHER_...}`, `{WISH}` and `{MINE}` for passed over).

**A35 (T) Gossip and council talk in the social log.** `Council_Gossip_Devoted` 12 lines, `Council_Gossip_Bitter` 23,
`Council_Decree_Gripe` 17, `Council_Decree_Argue` 15 (about 10 a year each): +10 to 12 each (about 45). The four newest
deeds' gossip has 3 lines each (`Rimfolk_GossipTopic_Jailbreak`, `_Binge`, `_DugUpDead`, `_RanWild`): +5 each; six each
for A23's deeds. Plain. *Size:* S.
**Built** (20 November 2026, not released yet; DESIGN.md "Colony life's text variety"). 45 rules for the council's talk and 23 things the gossip was about, in `Defs/InteractionDefs/Rimfolk_LogLines_ColonyLife.xml`. Of A23's deeds only the first is built (a seat put down, in colony life's variety, part 1): its three things to six; the others get theirs when they are built.

**A36 (T) Social events with two texts.** About 25 social events of the elders' and the quirks' (in
`Rimfolk_SocialEvents_Lives.xml`, `Rimfolk_SocialEvents_Quirks.xml`, `Council_SocialEvents_Lives.xml`) have 2 or 3
texts each: +2 each (about 50), by age where it matters. Plain. *Size:* S.
**Built** (20 November 2026, not released yet; DESIGN.md "Colony life's text variety"). All 35 social events of the three files, the six of the old, the 23 of the quirks and the six of secrets, vendettas and life goals beside them: two texts more each (70), written into the defs, since no file joins texts to a social event. None turned on the age.

**A37 (T) The seats' first words and the year letter's last.** Each seat has three lines for what a new holder means to
do (`elected`, 12 seats): +3 each (36), plain, each something the seat really does on the map (A1). The year letter's
closing word (`Council_Pool_AnnalsClose`, 36 lines) and the anniversary's (`Council_Pool_Anniversary`, 17 lines,
8/2/2/2/2/1): +10 each, as letters, in the flavour style. *Size:* S.
**Built** (20 November 2026, not released yet; DESIGN.md "Colony life's text variety"). The seats' 36 first words written into `Defs/OfficeDefs/` (no file joins words to a seat), about the seats' work as A1 and the duties show it; the 20 last words in `Rimfolk_Lines_ColonyLife_Letters.xml`. The anniversary's lines are said aloud, so one sentence each, nine plain and one cold (its voices keep under three).

---

### Recommended first round

1. **A30 What the dead leave goes to whoever was closest**: a rule fix, small, and first by the colony's own rules.
2. **A1 Each seat seen at its work**: four seats that only change numbers become people you see working.
3. **A2 The seat whose work it is speaks in the question**: the seats and the council's questions read as one council.
4. **A14 An ambition reached, seen on the map and kept on the calendar**: about 58 ambitions end with nothing to see.
5. **A6 Any other business at the meeting**: the meeting, petitions and decrees meet in one place.
6. **A13 A wish talked up finds backers**: petitions grow from talk you can see; small.
7. **A10 A place for the group**: the first link from groups to the council and the stores.
8. **A9 A day off for a friend**: the heart, wishes and friendships in one small wish.
9. **A26 The giving day**: a winter custom that uses Vanilla Christmas Expanded, already in the player's list.
10. **A31 Journals of the council's life**: the two thinnest pools for their use (8 lines each) and their neighbours.

Why these: most are I and E items that make what is already built visible and joined (seats, questions, the meeting,
petitions, groups, the heart, ambitions, the calendar) rather than new systems; one is a rule fix; one brings a festival
from a mod the player already runs; one is cheap text where the report found it thinnest. A2 and A21, and A3 and A23,
share parts, so A21 and A23 fit a second round well.

### Mods that would help

From `docs/player-mods/mods.tsv`. The pages were looked at on 20 November 2026; every Workshop page read from here
shows the line "removed from the community ... incompatible with RimWorld", even P-Music, posted in 2016 and updated in
October, so that line is how the page reads from this machine, not the mod's status: worth a look in Steam.

**Already active, and used by these items** (nothing to add): Vanilla Christmas Expanded (A11, A26), Vanilla Aspirations
Expanded (A17), Vanilla Books Expanded and [sbz] Bookcase (A29, A10), Vanilla Brewing Expanded - Coffees and Teas,
Dubs Bad Hygiene and Vanilla Furniture Expanded (A24, A10).

**Subscribed but not enabled:**
- **Tavern**, https://steamcommunity.com/sharedfiles/filedetails/?id=3775694305 (versions 1.6, 1.7; updated 4 November):
  medieval tavern furniture and a home kitchen set, mostly for looks; needs Vanilla Expanded Framework (active). A
  room for a group's evenings (A10), the giving day's supper (A26) and the elders' day (A28).
- **Epochs - Pottery**, https://steamcommunity.com/sharedfiles/filedetails/?id=3092351095 (mods.tsv: 1.4, 1.5, 1.6; the
  page now tags 1.7 too; updated 15 November): clay, two pottery workbenches, clay buildings and walls. Gifts for the
  giving day and things for a tribe's or a village's group places.
- **Epochs - Incense**, https://steamcommunity.com/sharedfiles/filedetails/?id=3072579620 (1.4, 1.5, 1.6; page not
  checked): incense for the chaplain's and the remembrance days; only if it now lists 1.7.
- **P-Music**, https://steamcommunity.com/sharedfiles/filedetails/?id=725130005 (1.0 to 1.7; updated 25 October): 56
  tracks added to the game's music, for the feel of festival evenings; it doesn't replace the game's music.

**Not recommended:** Milky Way (3773448562) is a framework for its author's mods (Nice Research Tab), not a night sky,
so it adds nothing to the night-sky habit or the star night. Simple Leadership (3668307448, 1.6, 1.7) would run a second
leadership system beside the council. RimTalk (3551203752) is already met by Rimfolk's talk stepping back.

**Gaps:** a bell that rings for the meeting, a calendar day or the giving day would help the most; a quick search found
no 1.6 or 1.7 mod for it, so none is named.

## Content update proposals, part B: the world around the colony

For the player's wish of 20 November 2026: "another large content update, meaning adding further variety to existing
mechanics, further enhancing what's already created, further integrating different features with each other, adding
more text variety etc." Nothing here is built. The player picks.

**What this part covers.** The region map and the world events, the 23 peoples, camps, encounters and nuisances, missions
and storylines, visitors and travellers, trade, pacts, fealty, feasts, matches, ransom, wards, couriers, letters and the
neighbours, and outsider talk (new lines only).

**Rules every item keeps.** These are the binding rules in CLAUDE.md:
- Soft integrations only, and only content the colony has unlocked.
- Harm happens on the map, is shown, and comes at the game's own rate.
- Every age gets its own form of an item.
- The colony is the family, so nothing is dynastic.
- Content that picks things draws on the `active` rows of `docs/player-mods/`, with a vanilla fallback.
- Answers play out on the map, and the words match what the player sees.
- The gods stay unseen.
- Away content follows `docs/quests-proposals.md`: within 3 tiles and rare. Where something would go far, the neighbours
  go and the colony helps from home.

**Text style (the player's rule change, 20 November 2026).** The player asked to restore letters, wishes, ambitions, life
goals, personal stories, council questions, camps and world events to their earlier flavour text, and to "keep that also
in mind for additional content to be added". So:
- **Flavour style.** New text for those eight parts is written in the earlier style, the one these files had before the
  November plain-words pass. An example is the great fire's "the wind did the rest ... ground it had already eaten".
  That text still says only what happens on the map. The items that touch these parts say so ("**text: flavour
  style**").
- **Plain style.** Everything else stays in plain, neutral words: encounters, missions and storylines, outsider talk,
  courier lines and the peoples' own texts.
- **Speech bubbles.** These stay one sentence each and sound like a person talking.

**What I counted (20 November 2026).**
- **World events.** There are 78. Some have very few start letters:
  - GoldFever and RepublicVotes have 1 each.
  - OrbitalLanes, MarketDays, Assize, MedicineScarce, IronScarce, ImperialLevy, ImperialDeserters, Crossroads and
    Waystation have 2 each.
  - Many beats have 1 or 2 lines, and many endings have 1.
- **World events and the peoples.** About 31 world events are patched to prefer one of Rimfolk's peoples
  (`Patches/Rimfolk_Peoples*.xml`, `<calling>`). Yet `Defs/WorldSituationDefs/` has no line tagged `their:<calling>`
  (the patches add 18), so a plague at the hospitallers' sickhouse reads like a plague anywhere.
- **World events and talk.** 15 world events have no outsider talk piece of their own.
- **Who reads world events.** The world events' modifiers name 5 old missions, but no home quest or storyline opener.
  The hamlet, the wards, the matches, the envoys, the feasts, the encounters, the council questions and the couriers
  never read a running world event.
- **The peoples' own talk.** 16 peoples have 4 talk pieces of their own and 7 have 6. One of each is the founding
  story, which waits for the player's answer on the peoples' texts.
- **The peoples' own camps.** Each of the 26 has on average 2.2 arrival texts, 5.1 bubble lines, 3.9 moment lines, 1 join
  and 1 help text. The older camps have up to 10 to 13 of each.
- **Camp beasts and goods.** Camps' herds and dogs are almost all vanilla (two mod animals in 73 kinds). The salt caravan
  sells cloth and hay but no salt.
- **What renown draws.** Colony renown draws 9 old events and nothing newer.
- **The 100 peoples' events.** They never read pacts, marriages, wards, envoys or fealty.

Sizes are S (defs and lines on existing workers), M (a small worker or condition plus defs) and L (a new play-out). Kinds
are marked **V** (variety), **E** (enhancing), **I** (integrating) and **T** (text).

---

### World events

**B1. World events in the peoples' own ways** (I, with T; M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world brought home"; `Source/ColonyCouncil/World/Home/`). Decided: a paragraph under the letter's own text (`WorldSituationDef.theirs`, `theirEnds`), told only when a line of that people's calling fits; one start and one ending paragraph for each of the 33 world events that prefer a people, and one start paragraph for each of the 23 peoples in each of the six hard ones: 205 lines.
- **On the map.** When a world event falls on one of Rimfolk's 23 peoples, its letters sound like that people:
  - A plague at the hospitallers' town fills their sickhouse, and their priors ask for linen as well as medicine.
  - A famine among the herding folk means herds sold at a loss and elders who eat last.
  - A great fire at the Kiln Guild's town takes their kilns.
  - A drought at the Deepwell Union's town sends its drillers deeper.
- **What changes.** Each people gets its own start-letter line and ending line in every world event that already prefers
  it (about 31). Each also gets a line in the six hard ones (plague, famine, hard winter, flood, great fire, mourning)
  when they strike its town. That comes to about 110 lines, tagged `their:<calling>`. The worker already adds the tag
  (`Peoples.AddTags`).
- **Ties.**
  - World events: `Defs/WorldSituationDefs/` and the `Patches/Rimfolk_Peoples*.xml` patches that prefer a people.
  - The peoples' lore (`honours` and `fears` in `Defs/PeopleDefs/`).
- **Text:** flavour style (world events).
- **Harm:** none.

**B2. World events steer the quests and storylines offered** (I; M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world brought home"; `WorldSteer`). Decided: the table as written (21 world events, 17 first parts, factors 1.5 to 3); a steered first part's people are the world event's own four times as often; its lines are picked with `ws:KIND`, `wsdef:DEFNAME` and `ws:theirs`, and the 41 lines written for a season (`{WSNAME}`, `{WSPLACE}`) are preferred when one fits.
- **On the map.** While a world event runs, the home quests and storylines that fit it come more often, and the first
  part's letter names it ("With the fever in Ashford, the town's healer asks..."). Each world event below makes these
  likelier:
  - **Plague or medicine gone scarce:** the sickness storyline (S2) and `Rimfolk_Mission_Healer` /
    `Rimfolk_Mission_OrderMedicine`.
  - **Drought:** the well storyline (S10).
  - **Bandit road, a bandit lord's fall or a warlord:** raiders settling nearby (S9) and the outlaw quest (Q7).
  - **Markets closed, a blood feud or a war between neighbours:** the two towns' quarrel (S7).
  - **The great fire or a war between neighbours:** the children from the burned village (S8).
  - **Drovers, the great herds or sickness in the herds:** the strayed herd (S3) and the sick animal quest (Q6).
  - **A holy year or a great pilgrimage:** the pilgrims' relic (S5).
  - **A people in mourning:** the lost founder's grave (S12).
  - **The assize:** their prisoner kept (Q1) and a dispute judged (Q2).
  - **A great fair or a season of songs:** musicians for a night (Q13) and a piece for a wedding (Q4).
  - **A year without a summer or a hard winter:** seed grain kept cold (Q11).
- **What changes.** These are `<rimfolk>` modifiers on the openers' IncidentDefs, the same field that already raises
  `Council_Mission_Provisions`. The openers also get a `ws:` tag for their first letter. The pacing rule of one storyline
  at a time still holds.
- **Ties.**
  - World events: `Defs/WorldSituationDefs/`.
  - Missions and storylines: `Defs/MissionDefs/`, `Storylines/Storylines.cs`.
- **Text:** plain (missions).
- **Harm:** only what each quest already has.

**B3. Messengers tell the road's news** (I, with T; S to M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world brought home"; `RoadNews`). Decided: told three times in four when a world event lies near the colony, near their town or not far out of the way between; their own town's first; at a courier's delivery, an answer handed over, and every carrier of the post's bag; 44 lines.
- **On the map.** The colony's messengers say a sentence about the road as they set down the bag at the mailbag. That
  covers the post rider, the mail run, a people's runner and a carrier. Some examples:
  - "The ford below Ashford is under water, I came round by the hill."
  - "They've shut the gate at Kettleford for the fever."
  - "Half the road is wagons going to the fair."
- **What changes.**
  - The courier worker adds `ws:<kind>` tags for world events near the courier's road or town, and `ws:theirs` when the
    event is at their own town.
  - About 40 new lines go into `Rimfolk_CourierLines`, which today has 83 lines and none about the world.
- **Ties.**
  - Couriers: `Couriers/CourierWords.cs`, `Defs/FlavorPoolDefs/Rimfolk_CourierLines.xml`.
  - World events: the running world events.
- **Text:** plain speech, one sentence each.
- **Harm:** none.

**B4. The new head comes to the gate** (E; M)
- **On the map.** After "a new leader" or "a contested seat" within reach, the new head rides out to see the region and
  stops at the colony's gate with two to four of their people. The colony can:
  - sit them at its table, a meal from the stores, with the best talker's Social counting;
  - give a gift from the stores;
  - or say a word at the gate.

  Their temper (the flags kind, greedy, harsh and steady, which the succession already sets) shows in what they do:
  - a kind head leaves a gift;
  - a greedy one asks the colony's trade terms;
  - a harsh one wants a token of respect;
  - a steady one opens talk of a trade pact where goodwill allows (20 or more).
- **Ties.**
  - World events: `World/Workers_Situations.cs` (`SituationWorker_Succession`, `_Claimants`).
  - Notables: the new head's regard (`World/Notables.cs`).
  - Pacts: `Diplomacy/Pacts.cs`.
  - Feasts: an invitation to the next feast with guests.
- **Text:** flavour style (world events).
- **Harm:** none.
- **Built** (20 November 2026, the world's integrations, part 2: the peoples and their ties, not released yet; DESIGN.md "The peoples and their ties"; `World/Region/NewHeads.cs`, `Rimfolk_WS_NewHead.xml`). Decided: a follow-up of the world events (kind `head`), three to eight days after the seat is settled, let go after thirty days or when they no longer lead; the best talker hosts at the table (a feast with them as guests and beds for the night) or says the word at the gate (the default); the kind head's gift is silver, handed over whatever the answer; the greedy head's caravan follows two to three days on; the steady head's pact offer comes as its own letter; the head sat at the table or given a gift is on the next feast's guest list (a promise on the ledger); the words fit every age (a messenger comes ahead; a high-tech people's head makes a round of the settlements).

**B5. Traders' prices follow the region's hard and good years** (E; S to M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The region's years and the camps"; `World/Region/RegionPrices.cs`, `RegionPricesMath`). Decided: a quarter dearer, a fifth cheaper in a bumper year; every trader of the world event's people, buying and selling alike; a war between neighbours and closed markets touch both peoples; one factor with the moot's season price on the same hook, kept within half and one and a half; the hover reads "Food dear at Ashford (famine): 25% more with the Ashford Union, buying or selling."
- **On the map.** While a world event runs at a people's town, their caravans, visitors' traders and towns pay more for
  what that town lacks:
  - food in a famine, blight, locust year, year without a summer or drought;
  - medicine in a plague or when medicine is scarce;
  - steel when iron is scarce or in a war between neighbours;
  - wood after a fire, a flood or rebuilding, or during a great work;
  - cloth and leather in sickness of the herds or when markets are closed.

  In a bumper year they sell food cheaper. The trade screen's price hover names why ("dear at Ashford: the famine").
- **What changes.** This reuses the season-price patch the moot over the old wood added
  (`Patch_SeasonPrices`, `Peoples/Events10/PeopleEvents10.cs`), by people instead of for all traders. It is the
  everyday counterpart of the one-off `WorldEffect_Sell` beats.
- **Ties.**
  - Trade: `Trade/TradeScreen.cs`.
  - World events and town fortunes.
- **Harm:** none.

**B6. Two more scarcities: cloth, and timber** (V; S)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world's variety: new world events, camps and encounters"; `Rimfolk_WS_Variety4.xml`, `World/Variety/`). Decided: cloth gone scarce comes on its own, three times as readily within 40 days after a murrain or closed markets; its tailor buys the colony's cloth dear or teaches a day's Crafting (coats from worn ones); timber gone scarce comes only after a great fire, a flood, rebuilding or a great work, and its carpenter's apprentice asks the colony's best builder for a day's teaching; both make their goods a quarter dearer with that people's traders (the region's prices), and the shearers and the woodcutters come with them.
- **Cloth gone scarce.** This comes alone, or after sickness in the herds or markets closed:
  - Weavers and tailors buy cloth, wool and leather dear.
  - A tailor of theirs at the gate wants the colony's cloth or teaches Crafting.
  - Shearers' camps come more often.
- **Timber gone scarce.** This comes after a great fire, a flood, rebuilding or a great work:
  - Timber is bought dear.
  - Woodcutters ask to cut on the colony's land more often.
  - A carpenter's apprentice asks to learn.
- **What changes.** Data only, built like "iron gone scarce" and "medicine gone scarce" (`WorldEffect_Sell`,
  `WorldEffect_Learn`, the camps' `situationKinds`, `after`).
- **Text:** flavour style (world events).
- **Harm:** none.

**B7. The games: a season of contests in the region** (V, with I; M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world's variety: new world events, camps and encounters"; `Rimfolk_WS_Games`, `World/Variety/GamesDay.cs`, `FeastPurpose.Games`). Decided: the four names by age as proposed; champions on the road three times as often, travelling groups and camps a third more; the games day is a letter's answer with two days' notice, then the feast's own day without notables or a meal hope: young people of 16 to 25, two of the games' people and one of a second friendly people (three of the one when there is none), the bouts, the race and the shooting match where the colony has targets, by the feast's rules; the end letter names a champion of the region, and a colonist who won at the games day is named in it and gets the feast's contest deed; a games team camps on the road.
- **On the map.** A town within reach holds its games. Each age has its own form:
  - **Tribal:** the gathering games.
  - **Medieval:** a tourney.
  - **Industrial:** an athletics meet.
  - **Spacer:** a league season.
- **While it runs.**
  - Travelling champions on the road stop at the colony more often (`Council_Evt_Champion`).
  - The colony may hold a games day at home. Young people of two friendly peoples come for the feast's wrestling, race
    and shooting contests, without the feast's notables.
  - Its end letter names the region's best. A colonist who won at the colony's games day counts it for their own name
    (renown's contest deed).
- **Ties.**
  - World events: a new definition.
  - Feasts: `Diplomacy/FeastContests.cs`.
  - Champions and renown: the champion event and `Diplomacy/Renown.cs`.
- **Text:** flavour style (world events).
- **Harm:** bouts as at feasts, a hard blow now and then at the game's rate, shown.

**B8. A pardon proclaimed** (V; M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world's variety: new world events, camps and encounters"; `Rimfolk_WS_Pardon`, `World/Variety/Pardon.cs`). Decided: it follows the succession ended with a kind head (its `kind` flag), or comes from a people in a holy year; the outlaw's meal is the road beat at the gate; the prisoners' beat comes only while the colony holds prisoners of that people, and its answer sets them all free by the game's own release (the wardens walk them out), counted as help to that people (the small favour) with goodwill at its end; bounty hunters don't come while it runs (weight 0), the outlaw quest at 0.3; outlaws walking home camp nearby; merciful in the colony's ways.
- **On the map.** After a kind new head takes their seat, or in a holy year, that people proclaims a pardon:
  - Outlaws of theirs walk home along the roads, and one asks a meal at the gate.
  - The colony may send home its prisoners of that people, pardoned. They walk out as a ransomed captive does, their
    people remember the colony warmly, and they owe it a small favour.
  - Bounty hunters for that people's quarrels stop coming while the pardon lasts.
- **Ties.**
  - World events: the succession's `kind` flag, the holy year.
  - Prisoners: `Diplomacy/Ransoms.cs` (its walk out).
  - Rimfolk events: `Council_Evt_BountyHunters`.
  - Missions: the outlaw quest (Q7), rarer while the pardon lasts.
  - The colony's character: merciful.
- **Text:** flavour style (world events).
- **Harm:** none.

**B9. More ways to begin and end for the thinnest world events** (T; S to M)
- **The thin pools as counted** (start letters / beat lines / ending lines):

  | World event | Start letters | Beat lines | Ending lines | Note |
  |---|---|---|---|---|
  | GoldFever | 1 | 2 | 1 | |
  | RepublicVotes | 1 | | | |
  | OrbitalLanes | 2 | 2 | 1 | |
  | MarketDays | 2 | | 1 | |
  | Assize | 2 | | | 1 title |
  | MedicineScarce | 2 | 1 (gatherer) | 1 + 1 | |
  | IronScarce | 2 | | 1 + 1 | |
  | ImperialLevy | 2 | | | |
  | ImperialDeserters | 2 | | | |
  | Crossroads | 2 | | | |
  | Waystation | 2 | | | |
  | NewTown | | | 1 | |
  | NewTownMet | | | 1 | |
  | Migration | | 2 across 3 beats | | |
  | StrangeHerds | | 2 across 4 beats | 1 to 2 | |
  | WildMagic | | 2 across 3 beats | 1 to 2 | |
  | Locusts | | 1 beat | | |

- **What to add.**
  - Start letters to at least 4 each, so every age and season has one.
  - Ending lines to at least 3.
  - A second line for each one-line beat.
- **Size.** About 90 lines.
- **Text:** flavour style (world events).

**B10. Talk of the world events nobody mentions** (T; S to M)
- **The thin pools.**
  - Twelve world events have no outsider talk piece at all: Blight, ColdYear, DryLaw, GreatCaravan, GreatWork, Legend,
    MedicineScarce, NeighborsWar, NewTownMet, SonBand, TownRaid and Trek.
  - Famine, Plague and HardWinter have only the general hunger, sickness and cold pieces.
- **What to add.** Two pieces each (one for a visitor whose own town it is, `situation:theirs`, and one for another
  people's visitor), plus one "ended" piece each. That is about 45 talk pieces.
- **Text:** in the voice of the existing outsider talk (people talking). No old talk is rewritten.

### The region

**B11. The hamlet's hard seasons follow the region** (I; S)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The region's years and the camps"; `HamletLife.RegionYear`, `HamletMath`). Decided: the nearest running world event within 15 tiles of the hamlet's town; "a little sooner" is an ask already below a fortune of 30 (20 otherwise), the gap between asks kept; the carter's word is said by their courier who comes to the gate for the help, once; a bumper year near makes the harvest's load half again as big.
- **What happens now.** The colony's own hamlet asks for food, a doctor or tools at random
  (`HamletMath.NeedFor`, a roll).
- **On the map.** When a world event runs within reach of the hamlet, the need follows it:
  - plague: a doctor;
  - famine, blight, locusts, drought or a year without a summer: food;
  - fire, flood or rebuilding: tools and timber.

  Help comes due a little sooner. Their carter's words at the gate name what is happening ("the fever has reached the
  lower farms"). A bumper year makes their harvest load bigger.
- **Ties.**
  - The hamlet: `World/Region/HamletLife.cs`, `HamletMath.cs`.
  - World events.
- **Harm:** none.

**B12. A town's fortune shows on its people** (I; M)
- **On the map.** Visitors, traders' guards and camps from a struggling town wear their clothes worn (the apparel's own
  hit points lower) and bring fewer pack beasts, and one of them asks for a meal. Those from a flourishing town come
  better dressed (a quality step up) and more often bring a small gift. The town's card already says "Struggling, since
  the plague", and now the gate shows it too.
- **Ties.**
  - Town fortunes: `World/Region/RegionFortunes.cs`.
  - Visitors and camps: the peoples' gear as new pawns come (`Peoples/PeoplesGearMath.cs`), camps
    (`Camps/CampKinds.cs`).
- **Harm:** none.
- **Built** (20 November 2026, not released yet; DESIGN.md "The peoples and their ties"; `World/Region/FortuneShows.cs`). Decided: the town is the people's nearest to the colony, only the two ends show (struggling, flourishing); clothes worn to 40 to 65%; one pack beast fewer for caravans and camps; the meal is asked of the nearest colonist and seen to first by the colonists themselves; a flourishing town's gift is 20 to 45 silver about one time in three, from visitors only; camps are dressed but ask for no meal.

**B13. Signs of the raiders' camp on the map** (E; M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The region's years and the camps"; `World/Region/RaidersCampSigns.cs`, `TakenIn.cs`). Decided: the scouts before every raid of a found camp, an hour or so watching, the raid an hour or two after they were seen from where they came in; the runaway from the camp's third day, a 5% chance a day, once; taken in, +6 goodwill, the deed `rescued` and the next raid's mean days halved once; the smoke at the 18th hour, every three days at most, half the time; the defense planner is left as it is (the message and the raid's letter name the side).
- **On the map.** While a raiders' camp stands:
  - **Scouts.** Before each of its raids, two of its scouts are seen at the map edge on the side the raid will come
    from, an hour or two ahead. They watch, then go. The message names the side.
  - **A runaway.** Once in a camp's life, a captive who ran from it (one of a friendly people, already hurt) reaches the
    gate. The colony can take them in and tend them, and then:
    - their people are grateful (the memory `rescued`);
    - the camp's next raid may come a little sooner.
  - **Smoke.** Colonists on the walls at dusk mention its smoke.
- **Ties.**
  - The raiders' camp: `World/Region/RaidersCamp.cs`, `RaidersCampWarnings.cs`.
  - What the peoples remember: `World/FactionMemory.cs`.
  - The defense planner's sides.
- **Harm:**
  - The scouts do not fight unless attacked.
  - The runaway's wounds are from before.
  - The raid is the camp's own, at its own rate.

### The peoples

**B14. Ties make a people's own events and camps come** (I; M)
- **What happens now.** The 100 peoples' events and their own camps weigh goodwill only.
- **On the map.** A people bound to the colony sends its events, gifts and camps more often (about 1.5 times). "Bound"
  means a defense or trade pact, a marriage between the two, a ward of theirs at the colony, an envoy, fealty either way,
  or a debt of honour. Their letters name the tie ("as partners in the pact..."). Their gifts go first to the colonist
  who carries the tie: the ward's guardian, the one married in, or the speaker who swore. A people the colony broke its
  word with sends them less often.
- **Ties.**
  - The peoples' events: `Happenings/Happenings.cs` (where renown already weighs events), `Peoples/Events*`.
  - Camps: the people's camps (`PeopleExtension.camps`).
  - The colony's ties: `Diplomacy/Pacts.cs`, `Matches.cs`, `Wards.cs`, `Envoys.cs`, `Fealty.cs`,
    `FavoursOwed.cs`.
- **Text:** plain for the events' lines; letters in flavour style.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; DESIGN.md "The peoples and their ties"; `Peoples/PeopleTies.cs`). Decided: 77 of the peoples' events name their callings (every one that comes by itself; follow-ups and the art handed on are as ever); bound 1.5, a broken word 0.67 while still felt; the speaker carries a pact, fealty or an envoy; the gift goes to the carrier first, else as ever.

**B15. The peoples' own encounters** (V, with I; M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world's variety: new world events, camps and encounters"; `Rimfolk_Encounters_Peoples.xml`, `StrangerRole.calling`, `WorldScenes`). Decided: two for each of the 23 callings, one showing their ways and one telling how their people began; five new scenes that must be true on the map and play out (a grave, a broken machine, a colony animal, a mech with Biotech, a building: books or the comms console), the old injury by the colonist's own hediffs; the linesman looks over the comms console rather than climbing a roof, and the Kiln Guild's moment is a mason judging a piece the colony made; small thoughts (+1 to +3).
- **What happens now.** An encounter with a visitor of one of Rimfolk's peoples is a general one: road news, a recipe
  swap.
- **On the map.** Two moments per people, by their ways, 46 in all. Some examples:
  - A hospitaller looks at a colonist's limp and tells them how to rest it.
  - A river-folk visitor shows a knot for the colony's nets.
  - A Wheel salvager crouches by a broken machine and names the part it needs.
  - A Signal Line linesman climbs a roof to look at the masts on the horizon (industrial).
  - A Kiln Guild potter turns a colony bowl over to read its mark.
  - A Quiet Bell sexton stops at the colony's graves.
- **What changes.** `StrangerRole` gets a `calling` filter. The moments use existing play-outs.
- **Ties.**
  - Encounters: `Strangers/Encounters.cs`, `Strangers.cs`, `Defs/EncounterDefs/`.
  - The peoples' lore.
- **Text:** plain (encounters).
- **Harm:** none.

**B16. Each people's own talk** (T; M)
- **The thin pools as counted.** 16 peoples have 4 talk pieces of their own (faith, honour, hardship, work) and 7 have 6.
- **What to add.** Six more each, about 138 pieces, on:
  - their working day;
  - food and drink;
  - their young;
  - their towns and land;
  - the other peoples, by the real relations in the save;
  - the colony as they see it.
- **Not included.** No founding stories: those wait for the player's answer on the peoples' own texts.
- **Ties.** Outsider talk: `Defs/OutsiderTalkDefs/Rimfolk_OutsiderTalks_Peoples*.xml`.
- **Text:** in the existing talk's voice (people talking). New pieces only.

**B17. Kin hear of their town's hard year** (I; M to L)
- **What happens now.** The colony's worry feeling covers parents, healers and growers (`Heart/FeelingFeeds_World.cs`).
- **On the map.** When a world event strikes a town, the people at the colony with a tie to it hear by the next
  courier and are worried. That means a ward from it, a spouse who married in from it, a hostage, their envoy, or a
  colonist who came from that people:
  - Their card says so.
  - A letter from home comes.
  - They may ask to send food or medicine home with the next courier, from the colony's stores, carried to the gate.
  - The colony's own ward or envoy at a struck town writes of it, and may come home early from a plague town.
- **Ties.**
  - The colony's ties: `Diplomacy/Wards.cs`, `WardsAbroad.cs`, `Matches.cs`, `Hostages.cs`, `Envoys.cs`,
    `EnvoysAbroad.cs`.
  - Couriers: `Couriers/`.
  - The heart's feelings.
  - World events.
- **Text:** flavour style for the letters; plain for messages.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; DESIGN.md "The peoples and their ties"; `World/KinNews.cs`, `Rimfolk_Dilemmas_KinSendHome.xml`). Decided: a colonist "who came from that people" is one of a Rimfolk people's own kinds, theirs alone; the word comes with the next courier, or by the road after two and a half days; the ask comes six times in ten as a council question (a fifth of the stores' raw food or medicine, carried to the gate for their runner) and counts as the colony's help in the world event; the colony's own at a plague town come home early half the time.

### Camps

**B18. The peoples' own camps, fuller words** (T; M)
- **The thin pools as counted.** These are the 26 camps of the peoples' events (`Rimfolk_CampKinds_PeopleEvents*.xml`):

  | Pool | Lines in all 26 camps | Average per camp |
  |---|---|---|
  | Arrival texts | 56 | 2.2 |
  | Bubble lines | 132 | 5.1 |
  | Moment lines | 102 | 3.9 |
  | Join texts | 26 | 1 |
  | Help texts | 27 | 1 |

  ScholarsMeasure is thinnest: 1 title, 1 text and no asks.
- **What to add.**
  - Arrival texts: to 4 each (+50).
  - Joins: to 2 each (+26).
  - Help texts: to 2 each (+25).
  - Bubble lines: to 9 each (+100).
  - Moment lines: to 6 each (+55).
- **Text:** flavour style for the camps' letters (arrival, join, help). The bubbles are speech, one sentence each.

**B19. Camps' herds, dogs and goods from the full mod pool** (E; S)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The region's years and the camps"; `Defs/CampDefs/`, `CampKindDef.traderGoods`). Decided: the beasts as listed, the megareindeer also with the snowbound caravan; a camp's dog is now any that fits rather than the first; the salt (else pemmican) and the market's honey also always in the camp's trader's pack; honey is VFE Medieval 2's or Alpha Bees'.
- **On the map.**
  - **Herds.** Drovers', herders' and calving camps bring beasts from the player's animal mods where the land and
    weather allow, besides muffalo, yak and sheep:
    - Megafauna's aurochs (`Aurochs`, spino.megafauna);
    - Alpha Animals' chameleon yak (`AA_ChameleonYak`) in the cold and thunderox (`AA_Thunderox`, already in one camp);
    - Vanilla Christmas Expanded's megareindeer (`VCE_Megareindeer`) in winter camps;
    - Vanilla Animals Expanded's camel (`AEXP_Camel`) and Indian elephant (`AEXP_IndianElephant`) as pack beasts.
  - **Dogs.** Vanilla Animals Expanded's dogs (`AEXP_GermanShepherd`, `AEXP_Beagle`, `AEXP_FrenchBulldog`).
  - **A hermit's cat.** One of Vanilla Animals Expanded's cats.
  - **Goods.**
    - The salt caravan finally carries salt (`VCE_Salt`, Vanilla Cooking Expanded).
    - Herb-gatherers carry tea leaves (`VBE_RawTea`, Vanilla Brewing Expanded).
    - A market camp carries raw honey (`VFEM2_Honey`, VFE Medieval 2).
- **What changes.** `MayRequire` entries in `<kinds>` and `<campGoods>`, with vanilla kept first as the fallback, checked
  by `poolcheck.py`.
- **Text:** none needed.
- **Harm:** none.

**B20. Beekeepers on the move** (V; M; only with Alpha Bees or VFE Medieval 2)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world's variety: new world events, camps and encounters"; `Rimfolk_Camp_Beekeepers`, `Camps/CampVariety.cs`). Decided: only with Alpha Bees, whose beehouses are the camp's hives at its edge (straw skeps without them were left out); spring and summer, five to eight days; honey, wax and mead in their pack and on their page (Alpha Bees' or VFE Medieval 2's); helping at their hives is two hours at a beehouse, practice with animals and Alpha Bees' own stings; the queen once the colony knows Alpha Bees' basic beekeeping, woodland or temperate by the land; they don't ask the colony to keep fires and axes away (no such rule plays out on the map).
- **On the map.** In summer, a beekeeping family camps by the colony's flowering ground for five to eight days:
  - They set their hives at the edge of the camp (Alpha Bees' `RB_Beehouse` as a camp piece, else straw skeps by the
    fire).
  - They sell honey and beeswax (`RB_BaseHoney`, `RB_Beeswax`, or `VFEM2_Honey`) and mead (`RB_Mead`).
  - They ask the colony not to burn or cut near the hives.
  - Once the colony has Alpha Bees' own research, they offer a temperate or woodland queen
    (`RB_Bee_Temperate_Queen`, `RB_Bee_Woodland_Queen`).
- **Ties.**
  - Camps: `Defs/CampDefs/`, a new kind.
  - Encounters: the existing "bees at work" moments.
  - Ailments: the bee stings ailment, when a colonist helps at their hives.
- **Mod pool.** sarg.rimbees, oskarpotocki.vfe.medieval2.
- **Text:** flavour style (camps).
- **Harm:** a sting now and then on a colonist who helps at their hives, by the existing bee-sting ailment.

**B21. Mushroom and truffle pickers** (V; M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world's variety: new world events, camps and encounters"; `Rimfolk_Camp_MushroomPickers`, `CampKindDef.gathers`). Decided: in the fall, or within six days after the mushrooms after rain; only on wooded land; they gather the listed caps first (VPE's, else the caves' agarilux and bryolux), then the camps' usual gathering; truffles with Alpha Animals, else raw fungus, and a pig or dog with them; in one stay in four, one of them falls ill with the game's food poisoning in the evening and they ask for the colony's doctor; the ask to keep the herd out of the wood is left out (nothing would keep it).
- **On the map.** In autumn, or after the "mushrooms after rain" event, a few pickers camp on wooded or cave land. They
  gather the wild mushrooms that grow there (the camps' existing gather work):
  - with Vanilla Plants Expanded - Mushrooms, from its plants (`VPE_Plant_Healtruffle`, `VPE_Plant_Glowcap`,
    `VPE_Plant_Timbercap`);
  - else raw fungus.

  They sell truffles (`AA_BlackTruffles`, Alpha Animals) and lead their truffle dog or pig. They ask the colony to keep
  its herd out of the wood.
- **Ties.** Camps, the mushroom flush event, the "Mushrooms" encounter.
- **Mod pool.** vanillaexpanded.vplantsemushrooms, sarg.alphaanimals; vanilla fallback.
- **Text:** flavour style (camps).
- **Harm:** now and then a picker who ate the wrong one asks for the colony's doctor (the game's food poisoning, on them,
  shown).

**B22. The neighbours for good keep the colony's days** (I; M)
- **On the map.** The family settled for good at the edge of the land (`Camps/Holding.cs`) walks over for the colony's
  calendar days:
  - a feast day, the bonfire, harvest home: they sit at the gathering and bring a dish from their stores;
  - a funeral: they come to the grave.

  On their own good days (a birth, a wedding in the family) they invite the colony's nearest friend to their fire.
- **Ties.** Camps and the holding, the calendar (`Calendar/ColonyCalendar.cs`, `CalendarDay.cs`), gatherings.
- **Text:** flavour style for the camp's letters; bubbles as speech.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; DESIGN.md "The peoples and their ties"; `Camps/HoldingDays.cs`). Decided: the days are calendar days kept with a feast, a bonfire or games, the harvest's day, and customs kept with a gathering; up to three walk over as the gathering's guests, and the first of them to get there sets down simple meals from their provisions; a grave within two days of the death, two of them; their good days are the day they settled, a birthday and a rare wedding in the family; a birth is left out (a camp can't care for a newborn on the map).

### Visitors, quests and the colony's name

**B23. What the colony is known for draws the quests and camps that fit it** (I; S)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world brought home"; `RenownMath.Events`, `RenownMath.Camps`). Decided: the table as written, by the same band factor as the old events (a quarter more to three quarters).
- **What happens now.** Renown draws 9 old events (`Diplomacy/RenownMath.cs`, `Events`).
- **What changes.** Widen the table to the newer quests and camps that match each kind of renown:

  | Known for | Quests and storylines drawn | Camps drawn |
  |---|---|---|
  | Healers | Healer mission, OrderMedicine, the sickness storyline | field sickhouse, lone healer, healers |
  | Fighters | trainee (Q3), outlaw (Q7), camp guard (Q8), BannerMuster | war party, hired guard |
  | Hosts | musicians (Q13), dignitary, festival | players, minstrels |
  | Traders | the factor's goods (S1), LeagueEscort | far merchants, salt caravan |
  | Makers | wedding piece (Q4), Smith, GlassApprentice, the journeyman's year | smith |
  | Faithful | the relic (S5), Shrine, HostelBread | pilgrims, penitents |

- **Ties.** Renown, missions, storylines, camps.
- **Harm:** none.

**B24. A stud visit** (V; M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world's variety: new world events, camps and encounters"; `Rimfolk_Mission_StudVisit`, `Missions/Workers_MissionStud.cs`). Decided: any people at peace that keeps a kind the colony keeps and that breeds by mating (egg layers excluded), herding peoples first; their male to the colony's females or their female to its male; it stands five to seven days as the colony's own, so the game's mating does the rest; the fee is a seventh of the beast's worth, 30 to 200 silver, taken from the stores when the drover comes for him; their goodwill +8 for a stand kept to its end, -10 if their animal is lost; their courier brings the young once born; the silver for his keep is half the fee (at least 20), asked by letter only, and then no fee is paid and no young is owed.
- **On the map.** Herders, the Circuit of Tamers or any herding people ask the colony to let their best male of a kind
  the colony keeps stand in its pen for five to seven days. Then:
  - the game's own mating does the rest;
  - a young one born to the colony's beast is the colony's, for a small fee;
  - or the colony's male sires on their beast, and next season their courier brings one of the young.

  The colony can also refuse, or ask for silver.
- **Ties.**
  - Missions: `Defs/MissionDefs/`.
  - The colony's animals and the herders' and tamers' lore.
  - Couriers, which bring the young one.
- **Mod pool.** Whatever kind both sides keep, mods' kinds included.
- **Text:** plain (missions).
- **Harm:** none beyond the game's own animal births.

**B25. Encounters of the region's hard times** (I; M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world brought home"; `WorldScenes`). Decided: fourteen, the nine above and five more of the same seasons; `EncounterDef.situations` takes a kind or a world event's name, and `scene` what must be true on the map and plays out (the food into their pack, the kneeling, the warming, the two sides at one fire, the comet under the open sky, the dog and the hens, the humming).
- **On the map.** About 14 moments that come only while a world event runs:
  - **Plague:** a visitor keeps their distance and won't share a cup.
  - **Famine:** a hungry visitor pockets bread from the table, and the colonist lets it go or speaks.
  - **Holy year:** a pilgrim kneels at the colony's shrine or altar.
  - **War between neighbours:** two visitors of the two sides share a fire, warily.
  - **Comet:** a visitor and a colonist watch the comet at night.
  - **Great fair:** a visitor shows a ribbon won.
  - **Drovers:** a drover's dog rounds up the colony's hens.
  - **Ice road:** a carter with frostnipped hands warms at the hearth.
  - **Season of songs:** a visitor hums the song about the colony.
- **What changes.** `EncounterDef` gets a `situations` condition (world event kinds), the way the camps' `situationKinds`
  work.
- **Ties.** Encounters, world events, the colony's character.
- **Text:** plain (encounters).
- **Harm:** none.

**B26. Council questions the world brings home** (I; M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world brought home"; `WorldQuestions`). Decided: kept apart halves the sickness at the gate and the couriers', stops the small moments with travellers and costs a point of goodwill a group; the vigil day is most of a working day each week at the god's place, an altar or the gathering spot; backing a side lets only its asks come; "keep the gate quiet" stays in the colonist's words, and the answer dyes the colony's outer clothes dark grey until the mourning ends; the fair day is a gathering and the day off.
- **On the map.** Five questions about the colony's own people that come only in a world event's season:
  - **Plague:** keep apart from travellers while the fever lasts. Fewer sicknesses come in with visitors and couriers
    (`Harm/Carriers.cs`), and visitors are cooler.
  - **Holy year:** a devout colonist's vow of a vigil day each week. Work is lost, and their belief and mood rise.
  - **War between two neighbours:** a colonist who came from one side wants the colony to back it, and a friend of the
    other side objects. This decides which side's beats come.
  - **Mourning:** wear mourning colours and keep the gate quiet for the neighbours' dead.
  - **Great fair:** take a fair day at home. It is a gathering, and the day's work waits.
- **What changes.** `DilemmaDef` gets a running-world-event condition.
- **Ties.** Council questions, world events, carriers, faith, the colony's character.
- **Text:** flavour style (council questions).
- **Harm:** none.

### Diplomacy at home

**B27. Answer a call to arms from home** (E; M)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The region's years and the camps"; `Diplomacy/CallsFromHome.cs`, `World/Region/TakenIn.cs`). Decided: supplies are 60 raw food (else 20 meals), 6 medicine and up to two spare weapons; silver in place of service is a season's tribute, from the medieval age on; two to four wounded a day to three later; either answer counts as half the deed of fighters sent; the doctor who tended them most is named in the deed `tended` when they leave.
- **What happens now.**
  - A defense partner's call and a liege's call for levies can only be answered by sending colonists away by caravan.
  - Otherwise the pact breaks, or levies count as refused (`Diplomacy/CallsToArms.cs`, `Fealty.LeviesRefused`).
  - The player hardly ever leaves the base.
- **On the map.** Two answers from home:
  - **Supplies or silver in place of fighters.** Food, medicine and weapons from the stores are carried to the gate for
    their runners. Under fealty, the medieval form is silver in place of service. This keeps the pact as a smaller
    kept word.
  - **Their wounded taken in after the fight.** Two to four of their wounded are walked or carried in and tended in the
    colony's beds. Their people remember who tended them.
- **Ties.** Pacts, fealty, the colony's beds and doctor, `FactionMemory` (`tended`), renown.
- **Text:** flavour style for the letters.
- **Harm:** the wounded arrive hurt from their own fight; any who die do so by the game's own rules, shown.

**B28. A feast's guests hope for their own people's contest, and a singing contest** (V, with I; S for the hopes, M for
the contest)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world's variety: new world events, camps and encounters"; `FeastMath`, `Diplomacy/FeastSinging.cs`, `FeastContests`). Decided: the hopes as listed, gene-editors and mechanitors at the tables, river folk at the race, the vault's folk and the Choir a fine meal and music; up to six singers, the guests and the colony's people in turn, at any instrument the colony has built (the game's and any mod's), else by a lit fire; the score is artistic and social skill with a little for an instrument and some luck; it answers a hope for music as a concert does, and its champion is named as any contest's.
- **What happens now.** Guests hope for a contest by their world age only (`Diplomacy/FeastMath.cs`, `FeastHope`).
- **The hopes, by people.**
  - **The hunt:** hunters, herders, grovekeepers.
  - **Bouts:** mercenaries, drillers, citizens, crew.
  - **The tables:** traders, brokers, scholars, artisans, seers.
  - **The race:** salvagers, linesmen, tamers.
  - **A fine meal and music only:** pilgrims, sextons, healers, listeners.
- **The new singing contest.** Singers take turns at the colony's instruments the colony has built: the game's Harp,
  Harpsichord, Piano and Drum, and VFE's `Joy_Piano`. Without one, they sing by the fire, judged on Artistic and Social.
  Each says one sentence.
- **Ties.** Feasts, the peoples' lore.
- **Text:** flavour style for the feast letters; plain lines spoken.
- **Harm:** none.

**B29. A feast's guests bring their town's year** (I; S)
- **On the map.**
  - Guests from a town in a hard world event ask leave to carry the leftovers home, and are grateful. Their regard rises
    more for the food than for the contests.
  - Guests from a town at its fair or in a bumper year bring a bigger gift.
  - The host's toast names it, in one sentence.
- **Ties.** Feasts (`Diplomacy/Feasts.cs`, `FeastLetters.cs`), world events.
- **Text:** flavour style for the letters; the toast is speech.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; DESIGN.md "The peoples and their ties"; `Diplomacy/FeastYear.cs`). Decided: a good year is the town's fair or a bumper harvest, a hard one any bad world event there; guests brought no gift before, so the good year's gift (60 to 120 silver) is the gift; the host grants the leftovers (two meals a party, never past half of what is spare) without a question, and a colonist brings them out of the stores to each guest as the night begins, into their pack.

### More text

**B30. The thinnest encounters** (T; S)
- **The thin pools as counted.** 25 encounters have only 1 or 2 texts: MarshLights, CaveMouth, FogShape, Gallatross,
  Graveyard, Meadow, Mushrooms, OcularForest, RefugeeThanks, RefugeeCarving, Shiny, Rare, ChildAndWild, RowTalk,
  QuietHour, Lifter, SongFromHome, HardWord, Trick, Fascination, Unease, ChildQuestion, DebtMentioned, HeatLightning and
  Blizzard.
- **What to add.** Two texts each, about 50, by age and season where they cross them.
- **Text:** plain (encounters).

**B31. Camps that come back bring the world's news** (I; S)
- **On the map.** A remembered camp coming back (`Camps/CampMemory.cs`) says what it lived through since:
  - the drought at their town and whether the rains came;
  - the fire and the new mill;
  - the fever and who they lost, from the world events that ended at their people while they were away.

  This line goes into the letter that already says "what became of them since".
- **Ties.** Camps, world event history.
- **Text:** flavour style (camps).
- **Harm:** none.
- **Built** (20 November 2026, not released yet; DESIGN.md "The peoples and their ties"; `Camps/CampMemory.cs`). Decided: the newest world event that ended at their people since they left, of 17 kinds of news by its kind and ending, and whether the colony helped.

**B32. The region's great events in the colony's book of the gods, as the colony believes** (I; S to M; coordinate
with the gods' list)
- **Built** (20 November 2026, not released yet; DESIGN.md, "The world brought home"; `GreatTaken`). Decided: ten great world events, each tied to one or two gods; once while it is new, nearly two times in three; the believer, a rival believer and a doubter as for a named season (C18); the book's words in its old voice.
- **On the map.** When a great world event comes (a plague, a comet, a bumper year, a great fire, a year without a
  summer), a devout colonist may set it in the colony's book as a god's doing. It is attributed, "some say", and possibly
  wrong. The book says who believes it.
- **The gods stay unseen.** A god is named only where the colony knows it. No favour or truth is shown.
- **Ties.** World events and the Gods' book (`Gods/GodBook.cs`, `GodTales.cs`).
- **Text:** the medieval voice of the gods' book, saying plainly what it means.
- **Harm:** none.

---

### Recommended first round (ten)

These make what already exists answer to each other, cost little, play out at home, and fill the thinnest text.

1. **B2. World events steer the quests and storylines offered.** The newest system, the quests, starts answering the
   oldest, the world events. Mostly data.
2. **B1. World events in the peoples' own ways.** The 23 peoples become present in the region's news, where they are now
   absent.
3. **B14. Ties make a people's own events and camps come.** Pacts, matches, wards and fealty start to show in the 100
   peoples' events.
4. **B23. What the colony is known for draws the quests and camps that fit it.** Renown starts to matter. One table.
5. **B11. The hamlet's hard seasons follow the region.** A small change, and the hamlet stops being random.
6. **B27. Answer a call to arms from home.** Pacts and fealty become usable for a player who never leaves the base.
7. **B5. Traders' prices follow the region's hard and good years.** The world events show in every trade, on an existing
   patch.
8. **B19. Camps' herds, dogs and goods from the full mod pool.** It follows the player's mod-pool rule, it is cheap, and
   it fixes the salt caravan with no salt.
9. **B15. The peoples' own encounters.** The 23 peoples' visitors get moments of their own.
10. **B9. More ways to begin and end for the thinnest world events.** These are the most-read letters with the fewest
    lines, written in the restored flavour style.

**Next, as a text round:** B10, B16, B18 and B30.

### Mods that would help

From `docs/player-mods/mods.tsv`, subscribed but not enabled. The versions column is copied from the catalogue (the player
plays 1.7). Whether each is still kept up was not checked in this run; that needs a look at each page.

| Mod | Workshop link | Versions | For which item | Note |
|---|---|---|---|---|
| Tavern | https://steamcommunity.com/sharedfiles/filedetails/?id=3775694305 | 1.6, 1.7 | B7 (a games day), fairs | Tavern furniture, flags and dishes |
| Gerrymon's Misc Props: Fences | https://steamcommunity.com/sharedfiles/filedetails/?id=3797400517 | 1.6, 1.7 | B24 (the stud pen), herders' camps | 25 fence pieces |
| Dark Ages : Beasts and Monsters | https://steamcommunity.com/sharedfiles/filedetails/?id=3472275628 | 1.5, 1.6, 1.7 | B19 (herds and dogs), the old beast and strange herds | `DA_DwarvenMuffton`, `DA_ImperialRedhound`, `DA_Taraal`. It also brings trolls and its own incidents, which add danger of their own |
| P-Music | https://steamcommunity.com/sharedfiles/filedetails/?id=725130005 | 1.0 to 1.7 | B28 (the singing contest), feasts | Music only |
| Medieval Total Rim: Quests and Incidents | https://steamcommunity.com/sharedfiles/filedetails/?id=3806263602 | 1.6, 1.7 | B28 (the singing contest) | Its flute and lute (`BA_Flute`, `BA_Lute`) would be instruments. It also adds its own pilgrims, minstrel and royal marriage quests that overlap Rimfolk's, and likely needs its Core (3806242927). Probably not worth it |
| Simple FX: Smoke | https://steamcommunity.com/sharedfiles/filedetails/?id=3261314247 | 1.3 to 1.6 | B13 (the camp's smoke at dusk) | Not marked for 1.7, so likely unusable now |
| Medieval Overhaul | https://steamcommunity.com/sharedfiles/filedetails/?id=3219596926 | 1.4, 1.5, 1.6 | B21 (mushroom pickers) | Has its own mushrooms (`DankPyon_Plant_Mushrooms`). Not marked for 1.7, and a very large mod |

Already enabled and used above:
- Alpha Bees
- VFE Medieval 2
- Vanilla Plants Expanded - Mushrooms
- Alpha Animals
- Megafauna
- Vanilla Animals Expanded
- Vanilla Christmas Expanded
- Vanilla Cooking Expanded
- Vanilla Brewing Expanded

No mod in the list gives:
- sounds for a crowd or a tourney (B7);
- a moving camp's skeps (B20).

Finding one would need a search outside the catalogue.

## Content update, part C: the gods and faith, the land and its life, and the other mods

A list to pick from (the player's request of 20 November 2026, "another large content update"). Nothing here is built
until the player picks. C1, C2, C3, C4, C16 and C17 are built (not released yet); so are C19, C20, C21, C22, C24, C28 and C30 (the land and its life, not released yet), and C5, C9, C10, C11, C13, C23 and C31 (the gods' variety, with thirty-one new happenings between the colony's people, not released yet); each says so. Every item was checked against the code, the defs, DESIGN.md and HANDOFF.md, and against the
active rows of `docs/player-mods/`. Nothing here was dropped by the player before, and each item builds on something
that already exists.

**Kinds.** **V**: more variety in an existing mechanic. **E**: something already made goes deeper, is shown better or
reacts more. **I**: two or more features worked together. **T**: more text variety. **Size**: S (defs and a small
hook), M (a new play-out or a few hooks), L (a new piece with its own saved state).

**The rules on every item.** Soft integrations only, and only what the colony has unlocked. No harm unless the item
says so, and then only at the shared budget's rate, from something on the map, and shown. Every age has its own form.
No family mechanics. Mods only from the `active` rows. Words match what is seen.

**The gods are unseen.** No favour, wants or rules are shown anywhere. A god is named only where `Gods.CardName` allows,
by the colony's name for it. What people believe goes into the colony's book as their own belief, which may be wrong.

**Text styles** (the player's rule of 20 November 2026, passed on by the lead):
- Letters, wishes, ambitions, life goals, personal stories, council questions, camps and world events are written in
  the earlier flavour style, the way their text read before the November plain-words pass, and still true to the map.
- The gods' own texts keep the old medieval voice: the book, sayings, prayers, sermons and the festival's words.
- Everything else is plain, neutral words.

Each item that adds text says which of these it follows.

### How it is now (what was counted)

- **Each god's own happenings** (`OwnHappening`, `Gods/OnMap/Own*.cs`):

  | God | Happenings |
  |---|---|
  | Hollin | 4 |
  | Smoulder | 5 |
  | Bellwether | 4 |
  | Morel | 4 |
  | Ennu | 4 |
  | Pip | 4 |
  | Cirrus | 4 |
  | Hush | 4 |
  | Undertow | 3 |
  | Helios | 3 |
  | Selene | 4 |
  | Murmur | 3 |
  | Ducat | 3 |
  | Tamiu | 2, besides her riddle, the wrapped dead and her stare |
  | Revel | 8 |

- **Festivals.** 14 gods keep a festival; Tamiu keeps none (`noFestival`). Only 6 festivals play out what their words
  say: Ennu, Cirrus, Helios, Murmur, Ducat and Revel (`OwnHappenings.FestivalKept`).
  - The other 8 keep the shared feast and the priest alone at the shrine: Hollin, Smoulder, Bellwether, Morel, Pip,
    Hush, Undertow and Selene.
  - Their words still say "a sapling set in the ground for every tree felled", "silver thrown down the deepest
    shaft", "a vigil kept on the walls till dawn" (`festivalDishes`, said in `CC_Wsh_RiteDone` and the book).
- **Sacred animals.** Only 5 of 15 gods have sacred kinds: Hollin's dryads, Bellwether's thrumbos, Tamiu's cats, Murmur's
  colossus toad and Revel's goat. The other 10 fall back to their domain's kinds for vows, offences and dreams.
- **Vows and questions.** Vows come in 4 kinds (`VowKind`). Faith has 4 council questions and the saint's
  (`Rimfolk_Worship_Dilemmas.xml`, `Rimfolk_Saints.xml`).
- **Few links outside the gods.** Outside `Gods/`, only about ten files read the gods: the calendar, the harm budget,
  the visitors' idle hours, the story levers, the priest's seat, the letters, the dreams, a slain animal, and two
  peoples' events. Wishes, the herd, the beast that stays, the weather book, the defense, the rooms and the art never
  meet them.
- **Mods used thinly or not at all** (their defs found in Rimfolk's code and defs by name):

  | Mod | Defs used |
  |---|---|
  | Vanilla Fishing Expanded | 0 of 27 |
  | Vanilla Books Expanded | 1 of 28 |
  | Possessed Weapons | 0 of 58 |
  | Fairycore - Carillon and Magic Wand | 0 |
  | Vanilla Ideology Expanded - Dryads | 0 of 40 |
  | Vanilla Christmas Expanded | 2 of 98, winter camps only |
  | Alpha Bees | 4 of 767 |
  | Dragons Descent | 9 of 210, Smoulder's true form only |
  | Sanguinary Animals | 0 |
  | VAE Royal Animals | 0 |
  | The Vanilla Quests Expanded family | 0 |
  | Vanilla Brewing Expanded - Coffees and Teas | used through the first-pot habit only |

  About 30 active xenotypes draw nobody toward any god (`PawnGodsMath.XenotypeDraws`).
- **Thin text pools.** Each kind of line, how many it has now:

  | Lines | Now |
  |---|---|
  | Each god's scoffs | 3 |
  | Each god's `whatLines` | 3 |
  | Each god's `seenLines` | 3 |
  | Each god's neighbours' `letterLines` | 2 |
  | Each god's own happening card (about 78 cards) | 1 wording |
  | Each god's way of taking an offering | 2 remarks, and 1 for a scoffer |
  | Many worship, belief and saint pools | 2 to 6 |
  | The gods' camps' texts | 2 to 6 |
  | Several weather and herd journal pools | 2 or 3 |

  The prayers, rites and saints' prayers are in plain modern speech. CLAUDE.md keeps the old voice for the gods' and
  faiths' texts.

---

### The gods and faith

**C1 (E) Each god's festival kept as its words say** (M to L) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The gods' integrations, part 1: festivals, sacred beasts and art").
- **On the map:** the 8 festivals that today keep only the shared feast get their own play-out, each matching its
  words:
  - Hollin's Greening: saplings set by hand, one for each tree felled this year (the count is kept), and bread eaten
    under the oldest tree.
  - Smoulder's Deep Silver: silver carried from the stores and dropped down the deepest shaft or a crack in the rock.
  - Bellwether's Letting Pass: a strip of grass at the edge left uncut. The herd is walked out to it, and the children
    sit for the old story.
  - Morel's Turning Under: the year's scraps and husks carried out and dug into a field, which turns rich soil, and a
    mushroom stew cooked for the feast.
  - Pip's First Spark: a broken thing mended, the machines walked round, and every light kept on through the night.
  - Hush's Stillness: a silent supper of simple meals, with the old and the sick seated nearest the fire.
  - Undertow's Shuttered Night: doors shut and every lamp lit.
  - Selene's Longest Night: a watch on the walls till dawn, then dreams told over breakfast.
- **Also:** the believers stand behind the priest at the rite, as a gathering, instead of the priest alone.
- **Ties:** `Festivals.Kept`, `OwnHappenings.FestivalKept` (the Ennu and Cirrus frame), the feast engine, `PlayCook`,
  `PlayWork`, the dreams told, the calendar, belief.
- **Every age:** torches instead of lamps for a tribal colony, a crack in the rock where there is no mine.
- **Mod pool:** Vanilla Plants Expanded - Mushrooms' mushrooms for Morel's stew, else the game's glowstool and
  agarilux.
- **Harm:** Selene's watch on a cold night already falls under the night watch's budget (`nightWatch`); nothing new.

**C2 (V) Sacred kinds for every god, from the player's animals** (S) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The gods' integrations, part 1: festivals, sacred beasts and art").
- **What changes:** the 10 gods with no sacred kinds get some from the active rows, and Hollin gets Vanilla Ideology
  Expanded - Dryads' castes.
  - Kept to rare kinds, never the land's common game or the colony's livestock, so hunting does not offend all the
    time.
- **Ties:** what the kinds already drive, by the god's own kinds instead of its domain's:
  - a sacred beast killed is an offence (`GodDeeds`, `GodHealth`);
  - the vow to spare a beast (`Vows`);
  - the god's dream warnings (`GodDreams`);
  - the god's mark never being one of them.
- **The kinds:**

  | God | Kinds | From |
  |---|---|---|
  | Smoulder | `DD_Drake`, `AA_IronhuskBeetle`, `MonitorLizard`, `AA_MammothWorm` | Dragons Descent, Alpha Animals, Odyssey |
  | Morel | `AA_FungalHusk`, `AA_RedSpore`, `AA_DecayDrake` | Alpha Animals |
  | Ennu | `Donkey`, `Crow` | the game, Odyssey |
  | Cirrus | `AA_Thunderox`, `AA_Thunderbeast`, `AA_ShockGoat`, `VAERoy_Crane` | Alpha Animals, VAE Royal Animals |
  | Hush | `AA_FrostboundBehemoth`, `AA_Frostling`, `AA_FrostLynx`, `AA_ArcticLion`, `AA_FrostAve`, `RG_ArcticDrak` | Alpha Animals, ReGrowth 2 |
  | Undertow | `AA_DuskProwler`, `AA_DuskRat`, `AA_NightRam`, `AA_NightMule` | Alpha Animals |
  | Helios | `AA_FlamingoPhoenix`, `AA_PhoenixOwlcat`, `Peacock` | Alpha Animals, Odyssey |
  | Selene | `AA_AngelMoth`, `AA_CrepuscularBeetle`, `RG_Owlbeast` | Alpha Animals, ReGrowth: Boiling |
  | Murmur, added to the toad | `Bullfrog`, `Heron`, `Otter`, `SeaOtter`, `AEXP_Beaver` | Odyssey, Animals of Monterey Bay, VAE |
  | Ducat | `AA_BlackScarab`, `AEXP_Pangolin` (the scales like coins) | Alpha Animals, VAE Endangered |
  | Hollin, added to her dryads | `VDE_Dryad_*` and `VDE_AwakenedDryad_*`, `Stagz_Dryad_Squid`, `DevDesigner_AncientDeer` | VIE Dryads, Merren, Elves |

  - Pip keeps none, being a god of machines.
- **Mod pool:** soft, by name, with each god's domain kinds as the fallback.
- **Harm:** none.

**C3 (I) A god's beast at home** (S to M) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The gods' integrations, part 1: festivals, sacred beasts and art").
- **When:** one of the colony's own animals is of a god's sacred kind. In their save these would be the thunderox, the
  dusk prowler and the two dryads.
- **What happens:**
  - Those who hold that god greet it the first time (one sentence) and now and then bring it a bite by hand, on the
    animals' rounds.
  - At the god's festival it is fed first and walked to the shrine.
  - One born on that god's festival day is an omen animal named in the god's own words.
  - When it dies, those who hold the god grieve a little, and the book keeps a believer's verse, perhaps under the wrong
    god.
- **Ties:** C2's kinds, `AnimalDays` (the rounds), `OmenAnimals`, `Festivals`, `PawnGods`, `GodLore.Believes`.
- **Harm:** none.

**C4 (I) The beast that stays, taken for a god's** (S) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The gods' integrations, part 1: festivals, sacred beasts and art").
- **When:** the named wild beast that stays (`ResidentBeast`) is of a god's sacred kind or shape (a thrumbo, a frost
  lynx, a dusk prowler).
- **What happens:**
  - A believer says whose they take it for when it is first named.
  - The council's question about it gains a believer's answer ("leave it, it is not ours to hunt"). Council questions
    are in the flavour style.
  - Hunting it counts as harming a sacred beast.
  - If it stays a season, the book keeps "it stayed among us" under the god the believer named.
- **Ties:** `ResidentBeast`, `Council_DlmA_ResidentBeast`, `GodDeeds`, `GodLoreMath.Attribute`, the book.
- **Harm:** none new (the hunt is the game's).

**C5 (V) Four more vows** (M)
- **What changes:** besides no meat, the pilgrimage, the temple and the beast spared, a colonist may make one of these:
  - **A lamp kept lit at the god's place for a season.** The vower carries fuel or checks the power each day: Pip,
    Selene, Helios, or the Hunger's shuttered lamps.
  - **A tree planted each week for a season.** Hollin; planted by hand, as play-outs already plant saplings.
  - **Plain bread for a season.** Hush and Morel; simple meals only, and broken by a fine one.
  - **Every visitor fed for a season.** Ennu and Ducat; a meal carried to each caller at the gate.
- **On the map:** each is kept on the map by the vower's own walk, and broken by what anyone can see.
- **Ties:** `Vows`, `VowKind`, life goals (`Rimfolk_Worship_Goals.xml`), `WorshipJobs`, the visitors at the gate.
  Life-goal text is in the flavour style.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/Worship/VowsSeason.cs`). Decided: each a life goal (weight 0.35) set by the devout or the priest, only to its gods (`WorshipMath.VowFits`), fifteen days; the lamp is the light within eight cells of the god's place, filled from the stores below six tenths, broken the night it is out or gone; a tree a week near its place, broken by a week with none or by felling a tree; plain bread broken by a fine or lavish meal; callers are a party of visitors or traders, broken when one leaves unfed.

**C6 (I) A vowed pilgrimage that really walks out** (M)
- **When:** today the pilgrimage vow walks to the far edge of the colony's own land. When a holy place of that god lies
  within reach in the region, the vower goes there instead:
  - a town of a people that holds the god (`Gods.Of`);
  - an old shrine by a landmark;
  - the fellowship's shrine.
- **The journey:** the vower is away for the days the words say, as the fellowship's pilgrimage already sends someone
  (`SeasonAway`, `Peoples/Events2/Pilgrimage.cs`).
- **Home again:** they bring something of the place and tell one piece of that people's tale of the god at supper
  (`GodTales.Tell`), and the book hears that people's name for it.
- **Ties:** `Vows`, life goals (flavour style), the region map, journeys, peoples, the book.
- **Harm:** the road's own, already in the budget (`pilgrimCough`).
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/Among/VowPilgrimage.cs`). Decided: a holy place within one to six days' road each way; a town of a people that holds the god is chosen before the fellowship's shrine or an old shrine unless those are much nearer; one to three days there; the keepsake is Vanilla Ideology Expanded - Relics and Artifacts' small inert relic (a statuette, idol, medallion or amulet from a town, polished rock or a fossil from an old shrine), the fellowship's shell from theirs, else a new pilgrim's token; the tale is told at the first supper home within six days; the cough only from a town.

**C7 (I) Murmur and the fishers** (S to M)
- **On the map:** a fisher who holds Murmur gives the first fish of the morning back to the water, and someone sees it
  splash. That day the fishing goes better: Vanilla Fishing Expanded's own `VCEF_FishingLuckOffset`, or a little more
  from Odyssey's fishing.
- **Also:**
  - The mod's `VCEF_Fisherman` trait draws its holder toward Murmur in belief.
  - Murmur's give-back (`MurmurGiveBack`) may leave a fish of the mod's on the mud.
- **Ties:** `OwnMurmur`, `PawnGodsMath` draws, the touch's "fish by the water".
- **Mod pool:** Vanilla Fishing Expanded (and its Fishing Treasures add-on), Odyssey.
- **Harm:** none new; the cold-water slip already counts fishing.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/Among/MurmurFishers.cs`). Decided: those who hold to Murmur (holds, not leans), the first catch between 4 and 12 o'clock, one fish of it; the day's better fishing is a half-day hediff (Odyssey's `FishingYield` +0.1, the mod's `VCEF_FishingLuckOffset` +0.02, each soft); the book keeps it once in ten days; two to four fish on the mud half the time the water gives back.

**C8 (I) Ducat and what the colony owes** (M)
- **What already exists:** Ducat's lore says he watches debts, and the region already keeps "what is owed each way"
  (`FavoursOwed`, the factor's debts, the scenario debts).
- **A debt paid when due:** the next morning one gold coin lies on the doorstep of the one who carried the payment, and
  the next trader at the gate stays half a day longer.
- **A debt let lapse:** the next trader drives a hard bargain for the day, shown as a plain line in the trade window
  ("drives a hard bargain today"), the god never named.
- **Ties:** `OwnDucat`, the diplomacy ledger, the trade window, `GodTouch`.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/Among/DucatDebts.cs`). Decided: the debts are the ledger's own (a colony's `Debt` ties: scenario silver, an enclave's dues; debts of honour the colony owes), paid when kept, let lapse when broken, three times in four; "the one who carried the payment" is the debt's own colonist at home, else the quartermaster, else the best talker; the coin lies outside their bedroom door (the gate with no room); the trader's half day is added to their lord's own timer once settled; the hard bargain is 5% worse prices for a day, also said in a plain message.

**C9 (V) Helios: out into the sun** (S)
- **On the map:** on a clear, warm noon, someone who holds Helios (else the doctor) walks a colonist who is getting over
  an ailment or an illness out of doors. The two sit an hour in the sun, and the illness moves on a little faster for
  the day.
  - Only for someone up and about, never someone on bed rest (`BedRest`). Never in a heat wave.
- **Ties:** ailments, `Health/BedRest`, `OwnHelios` (a fourth happening for him), belief.
- **Harm:** the sunburn ailment keeps its own rate; an hour adds nothing to it.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/OnMap/Between/BetweenHelios.cs`, `HeliosSun`). Decided: 11 to 14 o'clock, clear, 15 to 34 degrees, no heat wave; an illness or ailment the body is fighting off, up and about and not to be kept abed (`HealthAIUtility.ShouldSeekMedicalRest`); the severity eased by 0.06 once after the hour; the one walked out remembers the kindness.

**C10 (V) The gods' rarest gifts, from the mods** (M)
- **What changes:** the great tier of the gods' touch (one in 40 days at most for any god, `GodTouchMath`) may, once in
  two colony-years at most, leave something the mods make.
- **Possessed Weapons**, a named weapon in a ring of scorched ground, matched to the god:

  | Weapon | God |
  |---|---|
  | glacial cleaver | Hush |
  | lightning javelin | Cirrus |
  | volcanic maul, craghammer | Smoulder |
  | verdant bow | Hollin |
  | gilded arquebus | Ducat |
  | shrouded dagger | Tamiu |
  | sanctified sword | Helios |
  | automaton crossbow | Pip |

- **Dragons Descent:** a warm drake's egg found in Smoulder's deep after his waking (`DD_EggDrakeFertilized`), which
  hatches as the colony's.
- **Words:** say what lay there, as the marks already do (`GodMarkMath.WithLeft`).
- **Mod pool:** Possessed Weapons (`BotchJob_Possessed*`), Dragons Descent; none without them.
- **Harm:** the weapons are persona weapons (`CompProperties_BladelinkWeapon`), with the game's own bonds and traits;
  what the mod's weapons do (`GhostlySickness` among its hediffs) is the mod's own, to be read before building.
  Rimfolk adds no harm.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/Touch/GodRareGifts.cs`). Decided: after a god's great effect (not an anger), half the time once 120 days have passed since the last rare gift for the whole colony; a masterwork in a ring of ash; the egg after Smoulder's waking on the same wait, its hatchling the colony's. Read before building: the mod's JavelinShock and RockSmash are its weapons' hit effects, GhostlySickness the ghostly flintlock's (not given).

**C11 (V) Who is drawn to which god: the rest of the player's xenotypes** (S)
- **What changes:** about 30 active xenotypes draw nobody today. Each would get a pull by what it is
  (`PawnGodsMath.XenotypeDraws`; as belief, never favour):

  | God | Xenotypes |
  |---|---|
  | Hush | `AG_Animusen` |
  | Undertow | `AG_Malachai`, `AG_Wretch` |
  | Smoulder | `AG_Lapis` |
  | Cirrus | `Uredd`, `Askbarn` (its name means thunder) |
  | Pip | `AG_Taukai`, `AG_Helixien`, `VRE_Archon` |
  | Morel | `AG_Hiveling`, `VRE_Insector` |
  | Hollin | `VRE_Ocularkin` |
  | Helios | `VRESaurids_Saurid` |
  | Revel | `Highmate`, `Waster` |
  | Ennu | `AG_Fleetkind` |
  | Selene | `DV_Avaloi` |

  - Paws of Interest's types follow their kind: Umbreon to Selene, Espeon to Helios, Glaceon to Hush, Flareon to
    Smoulder, Leafeon to Hollin, Vaporeon to Murmur.
  - The apes go to Bellwether.
- **Ties:** belief, the Culture tab, the book.
- **Mod pool:** every row checked against `xenotypes.tsv`.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `PawnGodsMath.XenotypeDraws`). Decided: weights 12 to 20 by how plainly the xenotype fits; beside the table, Paws of Interest's Skitty (a kitten) to Tamiu and Goodra (a rain dragon) to Murmur.

**C12 (E) The keeper at work** (M)
- **On the map:** the book's keeper is chosen today but never seen at the work.
  - When a colonist tells of a god's sign that the keeper didn't see, the keeper walks out to the place, looks, and says
    one line. The verse then counts the keeper among those who saw what is left there.
  - In the evening, when there are new verses, the keeper sits at a writing table and writes them: Vanilla Books
    Expanded's `VBE_WritersTable` or `VBE_TypewritersTable` if built, else a table or a lectern.
- **Ties:** `GodLore` (the keeper, `Saw`), a play-out at a table, the Gods' book. The keeper's spoken line is in the old
  voice.
- **Mod pool:** Vanilla Books Expanded.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/Among/KeeperAtWork.cs`). Decided: the keeper goes to a sign seen at a place within 90 cells, within eight hours, one at a time; the entry gains their name and a line in their own voice; the evening's writing from 18 to 22 o'clock with any new entry, once in two days at most, a little longer for each.

**C13 (V) More council questions of faith** (M; flavour style)
- **The shrine in the way.** A room plan or a defense line is laid over a dedicated shrine: move it with a rite, build
  round it, or take it down.
- **The feast in a hard season.** A raid is feared or food is short as a god's festival comes: keep it with less, keep
  only the rite, or let it pass.
- **The scoffer at the rite.** Someone laughed at the rite, and the priest wants them kept from the next one: keep them
  away, have them carry the offering, or let it be.
- **A shrine nobody keeps.** Its last believer has died or left: keep it, give it to another god, or let it fall.
- **Ties:** `WorshipCouncil`, the room planner and the defense lines, `Festivals`, `PawnGods`, the book's verse on what
  was chosen.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/Worship/WorshipCouncilMore.cs`, `Defs/GodDefs/Rimfolk_Worship_Dilemmas_More.xml`). Decided: "in the way" is a blueprint or frame of the colony's within a cell and a half of the place; the hard season is under five days of food or a raid in the last three days, asked on the festival's eve, its answer kept on the day (with less: no offering and no guests; the rite only: held that evening, the day not kept; let pass); "laughed at the rite" is the scoffer's mockery before the faithful (`FaithHeart`) within five days, kept from the rites for fifteen days; a shrine nobody keeps is a dedicated place whose god nobody at home holds, given to the asker's own god.

**C14 (I) The raiders' gods, heard at the wall** (S to M)
- **On the map:** today the colony learns a raiding people's gods silently (`Pantheon.LearnFromVisitors`). Instead:
  - a raider of a people that holds a god cries its name as they come on (one sentence, by their people's own name for
    it, through `Speech.Say`);
  - the book sets it down ("so a raider of the Ashen Banner cried at our wall");
  - a prisoner of that people, visited by the warden, tells a piece of their god's tale (`GodTales.Tell`).
- **Ties:** raids, prisoners, peoples, `GodLore.Heard`, the stories in pieces.
- **Harm:** none (the raid is the game's).
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/Among/RaidersGods.cs`). Decided: seven raids in ten of a people with gods have a cry, when a raider is within 38 cells and in sight; their war god first, else the god they swear by; a prisoner's tale one talk in five with the warden, once in two days at most.

**C15 (I) The dead's own god at the grave** (S)
- **On the map:** at a eulogy (`GodDeeds.Funeral`), the speaker gives the dead to the god they held (`PawnGods.Held`),
  in one sentence in the old voice.
- **What follows:**
  - Mourners who hold the same god find their grief a little lighter.
  - The book keeps "laid to rest in Hush's keeping" by the colony's name for that god, or unnamed.
- **Ties:** funerals (`Vendettas` eulogy), belief, grief, the book.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/Among/GraveGods.cs`). Decided: the god the dead held most when they died (kept five days for the eulogy); the line is said over the speaker's head and added to the eulogy's letter; mourners are those of the colony grieving the dead who hold the same god (+3 for six days); the book keeps it as the speaker's belief.

**C16 (I) Legendary Pokémon taken for a god's** (S) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The gods' integrations, part 1: festivals, sacred beasts and art").
- **When:** a legendary of PokéWorld comes onto the land.
- **What happens:** a believer says whose they take it for, perhaps wrongly, and the book keeps it as a hand
  (`GodHands`).
- **Who is taken for whose:**

  | God | Pokémon |
  |---|---|
  | Helios | `PW_HoOh`, `PW_Solgaleo` |
  | Selene | `PW_Lunala`, `PW_Cresselia` |
  | Undertow | `PW_Darkrai`, `PW_Giratina`, `PW_Yveltal` |
  | Hush | `PW_Articuno`, `PW_Regice` |
  | Cirrus | `PW_Zapdos`, `PW_Raikou`, `PW_Rayquaza` |
  | Smoulder | `PW_Moltres`, `PW_Heatran`, `PW_Groudon` |
  | Murmur | `PW_Lugia`, `PW_Kyogre`, `PW_Suicune` |
  | Hollin | `PW_Celebi`, `PW_Shaymin`, `PW_Xerneas` |
  | Ducat | `PW_Persian`, `PW_Meowth` (it pays in coins) |
  | Tamiu | `PW_Spiritomb` |

  - Hunting one counts as harming a sacred beast for those who believe it.
- **Ties:** PokéWorld's spawns and Rimfolk's rare sighting, the gods' hands, belief.
- **Harm:** none.

**C17 (I) The gods, the beasts and the land in the colony's art** (M) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The gods' integrations, part 1: festivals, sacred beasts and art").
- **What changes:** Rimfolk records none of the game's tales today, so the colony's sculptures never show any of its
  stories. Each of these would become one:
  - a god seen;
  - a sacred beast's visit;
  - an offering taken before everyone's eyes;
  - a festival kept;
  - the beast that stays given its name;
  - the herd's old leader dying;
  - a named season (C18);
  - a raid held at a line.
- **On the map:** the game's own art then describes them, for example "This sculpture depicts Avila kneeling before a
  great white beast at dusk". A god is named only by the colony's name where the colony knew it was that god at the
  time (`GodDef.unheard` otherwise).
- **Ties:** the gods' moments, the animals, the weather book, the defense lines, the game's art.
- **Mod pool:** Vanilla Furniture Expanded - Art's statues show the same tales (they use the game's art system).
- **The named season (added with the land and its life, not released yet):** the ninth tale, a named season a believer
  took for a god's doing, recorded from C18's `SeasonGods.Named`.
- **Harm:** none.

### The land and its life

**C18 (I) Named seasons taken for a god's doing** (S to M)
- **When:** the weather book names a season far from the land's usual ("the winter of the long cold",
  `WeatherBook.NameText`).
- **What happens:**
  - Believers say whose doing they take it for (Hush for a long cold, Helios for a hot summer, Murmur for a wet one).
  - The book keeps it under that name.
  - A neighbour's letter may retell it (letters are in the flavour style).
  - When the same season comes round, the council may be asked to keep a day against it, as the customs do (council
    questions in the flavour style).
- **Ties:** `WeatherBook`, `GodLore.Believes`, the neighbours' letters, the calendar's customs.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/Among/SeasonGods.cs`). Decided: cold Hush; heat Helios or Smoulder (a warm winter Helios); storms Cirrus; wet Murmur; dry Helios or Murmur; the most devout believer speaks, another who holds another of them argues when beside them, a doubter beside them answers; the letter through the hands kept for neighbours; the council asked in the first six days of the same quarter the year after (keep a day at the god's place, or let it pass and the one who asked goes alone).

**C19 (E) After the storm: a work party for every disaster** (S to M) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The land and its life").
- **What exists:** after Seasonal Weather's earthquake, a party already goes round mending (`WeatherDisasters`).
- **What changes:** the same after the others:
  - a tornado: debris cleared, roofs and walls mended;
  - a flashstorm's fires: once out, the burnt ground cleared and the crops sown again;
  - hail: the broken crops cut and sown again;
  - a flood (Odyssey's): the stores moved up and the mud cleaned;
  - Vanilla Events Expanded's earthquake.
- **Words:** the chronicle and the annals keep each.
- **Ties:** `WeatherDisasters`, `WeatherBook` (it already notes the tornado), `PlayWork`, the chronicle.
- **Harm:** none (fires are the game's).
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Weather/AfterStorm.cs`, `AfterStormMath.cs`). Decided: each disaster as the game or the mod shows it (Seasonal Weather's quake condition, Vanilla Events Expanded's quake event, the game's flashstorm condition and tornado thing, Rimfolk's hail, Odyssey's seasonal flood thing, Vanilla Landmarks Expanded's tidal flooding condition); the party an hour or so after it ends, on a quiet map, the fires out first, led by the best builder or grower and three others, only where there is work, let go after two days; hail's broken crops are sown again (Rimfolk's hail destroys those it flattens, so nothing is left to cut); a flood's "stores moved up" is the water's leavings carried back to the stores; the chronicle keeps it once some work is done, the annals count each kind.

**C20 (E) The advisor remembers last winter** (S to M) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The land and its life").
- **What changes:** the advisor's firewood, winter-stores, herd-feed and herd-cold notes read the colony's own record of
  the same season a year before: its longest cold, its coldest night, how long the snow lay (`WeatherBook`).
- **In words:** said in the colony's terms: "last winter the cold held 19 days; at today's fires the wood lasts 11 such
  days".
- **Ties:** `AdvisorSignals5` (Firewood, WinterStores), HerdFeed, HerdCold, `WeatherBook`.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Advisor/AdvisorLastWinter.cs`, `WeatherMath.WinterBefore`). Decided: last winter is the four quadrums before the one now; "its longest cold" is how many days the cold lasted (a mean below 6 °C, the advisor's own measure), since the record keeps quadrums; the snow lay when half or more of the open land had a layer of it most of the day; spoken of after 20 days of a winter the record counted (seasons kept before the update are left out); one sentence added to each of the four notes.

**C21 (I) The autumn count of the herd** (M; flavour style for the question) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The land and its life").
- **When:** each autumn, when the herd is four or more.
- **What happens:**
  - The herdmaster, else the best with animals, walks the pens counting; a play-out.
  - The council is asked what to keep through winter, with the hay in store against last winter's length (C20's
    record): keep them all, sell the yearlings to the next trader, or slaughter the oldest for the winter's meat.
  - The herd story notes the year's count and its leader.
- **Ties:** `HerdStory`, the HerdFeed advisor, `WeatherBook`, trade, the council.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Animals/HerdCount.cs`, `Defs/DilemmaDefs/Rimfolk_Dilemmas_HerdCount.xml`). Decided: between 8 and 15 o'clock on a quiet day of autumn, once a year; the count is a round of up to eight of the herd; the question asked after it, within two days; yearlings are past their first stage and under a year old (eight at most, nobody's own, bonded, named or carrying young); they are marked to sell in the trade window for 30 days and the counter goes down to the pens when a caravan stops (Rimfolk never sells by itself); the oldest never the herd's leader, a male and a female of each kind kept, one when the hay lasts and the share it falls short by when it doesn't, four at most.

**C22 (I) The colony's animals at the line** (S to M) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The land and its life").
- **What changes:** the defense lines' lessons note colonists downed at a line (`DefenseLinesWatch`). They would also
  count the colony's animals that fought there: how many enemies they brought down, and who was hurt.
  - A lesson would say "the dogs held the gate" or "keep the animals back from the gate: three were downed there".
- **What follows:** the animal's own temperament line and its person's journal keep it, beside the pets' heroics that
  already exist (`Strangers/Pets.cs`).
- **Ties:** defense lines, the animals, the journal.
- **Harm:** none (the fight is the game's).
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Defense/DefenseLinesWatch.cs`, `DefenseLineMath`). Decided: an animal fights at a line when it went for a raider within 30 cells of the gate; what it brought down is the greater of its own downings and kills in the raid (the game's records); "keep the animals back" when two or more fell there (or one that brought nobody down), "they held" as a note beside the lessons when they brought enemies down and none fell; the card's "lately" and its person's journal for one that brought an enemy down, unless the fight came to the person (the pets' heroics say that).

**C23 (V) Telling the bees** (S)
- **When:** one of the colony dies, or someone new joins.
- **On the map:** the beekeeper (the one who tends the hives most) walks to each of Alpha Bees' beehouses
  (`RB_Beehouse`, `RB_AdvancedBeehouse`), stands a moment and tells them (one sentence).
  - After a death, the teller's grief eases a little, and the journal keeps a line.
  - No penalty if nobody goes.
- **Ties:** deaths and grief, newcomers, the journal.
- **Mod pool:** Alpha Bees; never comes without it.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Immersion/TellingBees.cs`). Decided: the beekeeper by Alpha Bees' own beekeeping priority, then skill with animals (else the best with animals); up to four hives in a round, in daylight on a quiet day; after a death +2 for two days; the news let go after three days; under the small habits setting.

**C24 (I) A cup for the sick** (S to M) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The land and its life").
- **On the map:** the first-pot habit already carries coffee or tea to a friend in the morning (`Immersion/FirstPot.cs`).
  This item would bring something hot to someone with a cold, a cough or a sore throat:
  - Vanilla Brewing Expanded - Coffees and Teas' `VBE_MintTea`, `VBE_LemonTea`, `VBE_BlackTea`;
  - honey from Alpha Bees (`RB_BaseHoney`) or Vanilla Cooking Expanded (`VCE_Honey`);
  - Vanilla Christmas Expanded's `VCE_HotCocoa`.

  The cup is carried from the stores, handed over and drunk.
- **What follows:** the ailment ends a little sooner, and the two think a little better of each other.
- **Ties:** ailments, `FirstPot`, the bonds.
- **Mod pool:** the three mods, by name; never offered against a drug policy, a faith or teetotalism.
- **Harm:** none; it slightly lowers the time spent ill, which sits near the top of its target (14% average,
  `docs/balance-report.md`).
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Immersion/SickCup.cs`, `SickCupMath.cs`). Decided: the ailments are Rimfolk's colds and coughs (head and chest cold, the smoke, rimrot, post-flu, pollution, lingering, smoker's and spore coughs); the bringer is the colonist at their leisure, not ill, who thinks best of the sick one; Vanilla Cooking Expanded has no honey in the player's mods, so the honey is Alpha Bees' or Vanilla Factions Expanded - Medieval 2's raw honey, and the mods' other teas come after mint and lemon; no vanilla fallback (the game has no hot drink); one cup a day each, waking hours, one every two hours a map, severity eased by 0.04 (some five hours off a head cold).

**C25 (V) The colony's own names for its places** (L)
- **What changes:** places on the home map get names from what first happened there, given by the colony's people:
  - the rise where the white beast stood;
  - the field of the graves;
  - the old oak;
  - the gate that held;
  - the drowned path.
- **Where the names are used:** in the words that already point at places (a god's card, supper talk, the chronicle,
  letters home), and as a quiet label the player can switch on.
- **Ties:** the gods' moments, the beast that stays, the defense lines, deaths, `WeatherBook`, the chronicle.
- **Harm:** none.

### Rooms and buildings

**C26 (I) A shrine room for each god** (M)
- **What changes:** the room planner offers a shrine room in the way the colony's people picture each god it knows. It
  is shown only once the god has a chapter in the book, by the colony's name for it, and says nothing of what the god
  wants.
- **The rooms:**

  | God | The room |
  |---|---|
  | Hollin | a garden shrine of planters round a tree |
  | Smoulder | a stone room cut into the rock, with a brazier and Dragons Descent's `DD_DragonStatueRitual_Large`, else `VFE_StatueLarge` |
  | Bellwether | a megalith in the open (`AM_Megalith`, `VFEPD_Megalith`) |
  | Morel | a dark cellar with mushrooms |
  | Cirrus | an obelisk under the open sky (`VFE_Obelisk`, `AM_Obelisk`) |
  | Hush | white stone and urns (`AM_Urn`) |
  | Helios | an open court with `VFE_StatueGrand` |
  | Selene | a moon garden by `AP_ObeliskFountain` |
  | Tamiu | `AM_Sphynx` or `VFEPD_Sphinx` with `AM_KemeticSlab_Medium` and a sarcophagus |
  | Murmur | Dubs Bad Hygiene's `PrimitiveWell` |
  | Pip | `AncientTerminal_Worshipful` |

- **Without Ideology:** works too, through statues and art, which can already be dedicated (`Shrines.Is`).
- **Ties:** the room planner (`RoomTemplateDef`), `Shrines`, the book.
- **Mod pool:** Alpha Memes, VIE Memes and Structures, VFE Art, VFE Props and Decor, Alpha Props - Parks and Gardens,
  Dubs Bad Hygiene, Dragons Descent; each soft.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/Among/GodRooms.cs`, `Defs/RoomTemplateDefs/Rimfolk_RoomTemplates_GodRooms.xml`). Decided: "a chapter in the book" is a page for the god; each room has a piece of the game's own it can't do without (pots, a torch or brazier, columns, a sarcophagus); the open-sky rooms leave their roof off; `AM_Sphynx` (12 by 18) is too big for a room and was left out; Pip's terminal and the art statues are placed only where the colony can.

**C27 (V) Room plans from the player's mods** (M)
- **What changes:** of 109 room plans, few use the player's mods (VFE's plinth and lamps, a sauna). New plans:
  - a writer's study (`VBE_WritersTable`, `VBE_TypewritersTable`);
  - a coffee corner (`VBE_EspressoMachineBuilding`);
  - a dryad grove (`VDE_DryadNest`);
  - a festive hall (`VCE_FestiveFeastTable`, `VCE_GrandFestiveTree`);
  - a beekeeper's yard (`RB_Beehouse`);
  - a fish house for Vanilla Fishing Expanded's catch.
- **Ties:** the room planner, the rooms read by what is in them.
- **Mod pool:** each plan only with its mod.
- **Harm:** none.

### Defense and the advisor

**C28 (E) The lines in today's weather** (S to M) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The land and its life").
- **What changes:** the game lowers every shot taken outdoors in fog, rain or snow (the weather factor in the aim,
  `docs/defense-research/mechanics.md`). The defense survey follows the game's own sight and cover but not the weather.
  With this item:
  - a line's card and the raid's first look say what today's weather does to the shooters on both sides;
  - the lessons note when a raid came in fog.
- **Ties:** `DefenseLineReview`, `DefenseLinesWatch`, the weather. To check against the 1.6 code before building, as
  the survey's other rules were.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/UI/DefenseLinesPanel.cs`, `DefenseLinesWatch.cs`). Checked in the 1.6 code (`Verse/ShotReport.HitReportFor`): the weather's accuracy factor multiplies every shot's aim when the shooter or the target is unroofed, any shooter, turrets too; melee is untouched. Decided: said when it takes a tenth or more, in percent; the raid's first look is one message; the lessons keep it as a note beside the others.

(C20 and C22 also touch the advisor and the defense.)

### The other mods

**C29 (I) Midwinter with Vanilla Christmas Expanded** (M)
- **When:** the colony's midwinter day, which is the winter god's festival (Hush's Stillness or Selene's Longest Night)
  or the calendar's midwinter custom.
- **On the map, using the mod's things where they are built or in store:**
  - the festive tree lit (`VCE_GrandFestiveTree`, `VCE_StandingFestiveTree`), and fairy lamps hung;
  - the feast laid on `VCE_FestiveFeastTable` with gingerbread, eggnog and hot cocoa;
  - `VCE_FestivePresent` handed round, one each, carried from the stores;
  - a snow sculpture made where the snowman play-out already makes one (`VCE_SnowSculpture`).
- **Ties:** C1, the calendar, the feast engine, `PlaySnow`.
- **Mod pool:** Vanilla Christmas Expanded; none without it.
- **Harm:** none.
- **Built** (20 November 2026, not released yet; `Source/ColonyCouncil/Gods/Among/Midwinter.cs`). Decided: kept from the calendar (the winter god's festival or the midwinter custom's day) and the midwinter question's first two answers; the tree switched on at dusk (said plainly without power), the food set out beside the table at supper, the presents after supper by the eldest at home, the sculpture by day; the winter god's own festival play-out (C1) is left to it.

(Other items that use the mods: C2, C7, C10, C11, C12, C16, C23, C24, C26, C27.)

### The Almanac

**C30 (E) The Almanac's pages for the mods this round uses** (S) Built, not released yet (the player's go of 20 November 2026; DESIGN.md, "The land and its life").
- **What changes:** these active mods have no page today, or are named nowhere in the Almanac:
  - Possessed Weapons
  - Vanilla Ideology Expanded - Dryads
  - Sanguinary Animals
  - Demigryphs
  - Vanilla Christmas Expanded (beyond the winter camps)
  - Dragons Descent
  - Vanilla Fishing Expanded's part with Murmur

  Each mod a picked item uses gets a short page saying what Rimfolk does with it, plus a row in the compatibility table
  (`Council_Mods_Support.xml`).
- **No gods:** like the gods' own page, no page says what a god does with them; the pages say what the colony's people
  do.
- **Keywords:** of existing pages unchanged (`almanaccheck`).
- **Built** (20 November 2026, not released yet; `Defs/AlmanacArticleDefs/Almanac_Mods_I_Integrations.xml`, `Council_Mods_Support.xml`). Decided: pages for the mods the built integrations use that had none (Vanilla Ideology Expanded - Dryads, Dragons Descent, Vanilla Landmarks Expanded, Vanilla Christmas Expanded, Vanilla Factions Expanded - Medieval 2); Vanilla Fishing Expanded's part with the fishers on the Vanilla Cooking Expanded page that covers it; the brewing, bees and events pages brought up to date; a table row for each. Possessed Weapons, Sanguinary Animals and Demigryphs have no page, since no built integration uses them.

### Text variety

**T1 / C31 (T) The gods' happening cards, three wordings each** (M)
- **Now:** about 78 card texts (`Rimfolk_GodOwnHappenings.xml`, `Rimfolk_GodHappenings_<God>.xml`,
  `Rimfolk_GodsOnMap*.xml`) have one wording each. The player has seen the same card three times.
- **Add:** two more wordings each, about 156 lines, saying the same thing seen, with the same placeholders.
- **Style:** plain words; the cards are the world's voice, not a god's.
- **Built** (20 November 2026, not released yet; `Languages/English/Keyed/Rimfolk_GodCards_Wordings.xml`). The 99 card texts of the gods' own happenings, the gods on the map and the festivals, each with two more wordings, told through `TextVariants.Vary`; the 31 new happenings between the colony's people (DESIGN.md, "The gods between the colony's people") come in three wordings from the start.

**T2 / C32 (T) The gods' own lines** (M)
- **Now and to add**, each god's lines in `Rimfolk_Gods.xml`:

  | Lines | Now | Add | Voice |
  |---|---|---|---|
  | Scoffs | 3 | 3 more each, 45 | plain: people talking |
  | `whatLines`, `seenLines` | 3 each | 2 more each, 60 | the old voice: the book's verses |
  | `letterLines` | 2 (Tamiu's 3) | 3 more each, about 44 | the flavour style: letters |

**T3 / C33 (T) Worship talk** (M)
- **What to add:**
  - The remarks when an offering is taken: 2, and 1 for a scoffer, per way (`Rimfolk_Pool_Worship_Way`, 42 lines over
    15 ways). Add 3 and 1 per way, 60.
  - The presences' remarks: 4 to 6 per presence in `Rimfolk_Pool_GodPO_Say`. Add 4 per presence, 60.
  - The true dreams: 2 per kind in `Rimfolk_Pool_GodPO_Dream`. Add 2 per kind, 14.
- **The thin pools** (each to 8 lines, about 130 more):

  | File | Pool | Lines |
  |---|---|---|
  | `Rimfolk_Worship_Pools.xml` | `GiveBeast` | 2 |
  | | `GiftYes`, `GiftNo`, `PilgrimOut` | 3 |
  | | `DedicateJournal`, `CrisisJournal`, `ScoffReply`, `Bicker`, `BickerReply`, `VowKeptJournal`, `VowBrokenJournal` | 4 |
  | | `AskGift`, `ReturnFound`, `Vow`, `PilgrimThere` | 5 |
  | | `Crisis`, `RiteWords` | 6 |
  | `Rimfolk_Belief_Pools.xml` | `DreadJournal`, `ShrineLost` | 3 |
  | | `KeepAway`, `RivalReply` | 4 |
  | | `Flinch`, `Rival` | 5 |
  | | `WonOverJournal` | 6 |
  | Saints | `Raise`, `NamedJournal` | 4 |
  | | `Remembered`, `Words` | 6 |
  | The gods on the map | `TL_Mem_GodSeen_Close` | 4 |
  | | `SacredAnimalBeckon` | 5 |
  | | `GodStrangerThanks`, `MogStares` | 6 |
  | | `GodMarkVisitor` | 7 |
  | The calendar's festival | `Council_Cal_Pass_GodFestival` | 2 plain |
  | | `Council_Cal_Talk_GodFestival` | 3 |
  | | `Council_Journal_Cal_GodFestival` | 4 |

- **Style:**
  - Plain, but the vow and pilgrimage lines belong to life goals and take the flavour style.
  - Prayer and rite words take the old voice (T4).

**T4 / C34 (T) Scripture in the old voice** (M; needs the player's word)
- **Now:** CLAUDE.md keeps the old medieval voice for the gods' and faiths' texts, and the player enjoys it "especially
  the religions". The prayers, thanks, rites and saints' prayers read in plain modern speech today:

  | Pool | Lines |
  |---|---|
  | `Rimfolk_Pool_Worship_Pray` | 31 |
  | `Thanks` | 12 |
  | `Rite` | 16 |
  | `RiteWords` | 6 |
  | `Rimfolk_Pool_Saint_Prayer` | 13 |
  | `Rimfolk_Pool_Saint_Feast` | 9 |
  | each festival's `festivalDishes` | 14 |

- **Add:** an old-voice set beside them, about 100 lines ("Hear me, Rootmother, as thou hearest the roots"). It would be
  spoken by:
  - the priest;
  - the book's keeper;
  - a devout elder;
  - a believer of a faith of the old ages.

  Everyone else keeps the plain lines, and each line still says plainly what it means.

**T5 / C35 (T) The gods' camps** (S; flavour style, as camps)
- **Add:** the pilgrims' camp and the seer's camp (`Rimfolk_GodsWorld_Camps.xml`) to 8 lines each, about 30 lines.

  | Camp | `texts` | `momentLines` | `lines` |
  |---|---|---|---|
  | Pilgrims' | 4 | 4 | 6 |
  | Seer's | 3 | 2 | 5 |

**T6 / C36 (T) Weather talk and the land's journal lines** (S to M; plain)
- **Weather:** the weather pools under 7 lines, counting the everyday add-ons. Bring them to 8, about 26 more:

  | Pool | Lines |
  |---|---|
  | `SayLightningRod` | 3 |
  | `Council_Journal_Tornado` | 3 |
  | `SayLightningFlinch` | 5 |
  | `SayRecordDry` | 5 |
  | `SayRecordWet` | 6 |
  | `SayWarmSpell` | 6 |
  | `SayForecastCloud` | 6 |
  | `SayForecastDust` | 6 |
  | `SayForecastWrong` | 6 |

- **The land's journals:** bring them to 6, about 57 more:

  | Pool | Lines |
  |---|---|
  | `Council_Journal_Herd_Gone` | 2 |
  | `Herd_LastSeason` | 2 |
  | `Herd_SpringCalf` | 2 |
  | `Herd_SpringKinds` | 2 |
  | `Rimfolk_Journal_AlphaSlain` | 2 |
  | `LegendBefriended` | 2 |
  | `LegendSlain` | 2 |
  | `Council_Lines_Journal_PetDefended` | 2 |
  | `Council_Journal_NemesisReleased` | 2 |
  | `Herd_Leader` | 3 |
  | `Herd_LastSeasonKinds` | 3 |
  | `Rimfolk_Journal_AnimalDays` | 3 |
  | `BeastOath` | 3 |
  | `PetFarewell` | 3 |
  | `Council_Cal_Talk_Companion` | 3 |
  | `Council_Cal_Talk_Foal` | 3 |

---

### Count

- **V (9):** C2, C5, C9, C10, C11, C13, C23, C25, C27
- **E (6):** C1, C12, C19, C20, C28, C30
- **I (15):** C3, C4, C6, C7, C8, C14, C15, C16, C17, C18, C21, C22, C24, C26, C29
- **T (6):** C31 to C36

36 items.

### The recommended first round (10)

Chosen because they make what exists feel alive and connected, are seen on the map, and are mostly small. They also
build on each other: C2 feeds C3, C4 and C16, and C17 shows the others in the colony's art.

1. **C1** Each god's festival kept as its words say: the biggest gap between words and map in the gods' part, and seen
   every year.
2. **C2** Sacred kinds for every god: small, and it feeds the vows, offences, dreams, C3 and C4.
3. **C3** A god's beast at home: their thunderox, dusk prowler and dryads would start to matter.
4. **C4** The beast that stays, taken for a god's: two systems the player already sees, joined.
5. **C17** The gods, the beasts and the land in the colony's art: the game's own art tells Rimfolk's stories.
6. **C14** The raiders' gods, heard at the wall: the gods reach the book through enemies, too.
7. **C18** Named seasons taken for a god's doing: the weather book meets the gods' book.
8. **C11** Who is drawn to which god: about 30 of the player's xenotypes, all small defs.
9. **C24** A cup for the sick: everyday care, three of the player's mods, and a little less time ill.
10. **T1 / C31** The gods' happening cards, three wordings each: the player's own complaint was seeing the same thing
    over and over.

Next, as a second round: C7, C12, C5, C19, C26 and T2 to T4. T4 waits for the player's word on the old voice.

### Mods that would help

All of these are in the player's subscriptions but switched off (`active` no in `docs/player-mods/mods.tsv`). Versions
are as the catalogue has them. "Kept up" was checked only where a doc already says so; for the rest, look at the
Workshop page before adding.

| Mod | Workshop | Versions | Kept up | For |
|---|---|---|---|---|
| P-Music | [725130005](https://steamcommunity.com/sharedfiles/filedetails/?id=725130005) | 1.0 to 1.7 | yes (HANDOFF 0.255.0) | sound: more tracks for the gods' big moments and festivals (C1, C29) |
| Milky Way | [3773448562](https://steamcommunity.com/sharedfiles/filedetails/?id=3773448562) | 1.6, 1.7 | to check | graphics: the night sky, for Selene's nights, Cirrus's stars and the watch on the walls (C1) |
| Atmosphere Overhaul | [3830470974](https://steamcommunity.com/sharedfiles/filedetails/?id=3830470974) | 1.6, 1.7 | to check | graphics: sky and weather, for the presences, named seasons and storms (C18, C19, C28) |
| Gerrymon's Greekcore Style | [3666883698](https://steamcommunity.com/sharedfiles/filedetails/?id=3666883698) | 1.6, 1.7 | to check | graphics: columns and temple pieces, for the shrine rooms (C26); the player named the Greek pantheon as their reference |
| Epochs - Incense | [3072579620](https://steamcommunity.com/sharedfiles/filedetails/?id=3072579620) | 1.4 to 1.6 | to check; no 1.7 tag | incense for rites and offerings (C1, C5) |
| Epochs - Pottery | [3092351095](https://steamcommunity.com/sharedfiles/filedetails/?id=3092351095) | 1.4 to 1.6 | to check; no 1.7 tag | urns and pots for Tamiu's and Hush's rooms (C26) |
| Dark Ages: Crypts and Tombs | [2963826335](https://steamcommunity.com/sharedfiles/filedetails/?id=2963826335) | 1.4 to 1.6 | to check; no 1.7 tag | graphics: crypts and tombs, for Tamiu's room and the graves (C15, C26); the Almanac already has a page for it |
| Dark Ages: Beasts and Monsters | [3472275628](https://steamcommunity.com/sharedfiles/filedetails/?id=3472275628) | 1.5 to 1.7 | to check | graphics: creatures for the beasts, legends and the gods' sacred kinds (C2, C4) |
| Gauranlen Tree Expanded (Continued) | [3025108023](https://steamcommunity.com/sharedfiles/filedetails/?id=3025108023) | 1.3 to 1.6 | to check; no 1.7 tag | more dryad castes for Hollin (C2) |
| Simple FX: Smoke and Simple FX: Splashes | [3261314247](https://steamcommunity.com/sharedfiles/filedetails/?id=3261314247), [3261109179](https://steamcommunity.com/sharedfiles/filedetails/?id=3261109179) | to 1.6 | to check; no 1.7 tag | graphics: smoke and water effects for Murmur, the festivals' fires and the storms (C1, C7, C19) |
| Stoneborn - Dwarven Style Pack | [3172496453](https://steamcommunity.com/sharedfiles/filedetails/?id=3172496453) | 1.4 to 1.6 | to check; no 1.7 tag | graphics: a stone hall look for Smoulder's room (C26) |
| Tavern | [3775694305](https://steamcommunity.com/sharedfiles/filedetails/?id=3775694305) | 1.6, 1.7 | to check | Revel's long night and the feasts |

One that is not in the list at all:
- **Pawns on Display** ([3446605621](https://steamcommunity.com/sharedfiles/filedetails/?id=3446605621); tags 1.5, 1.6,
  1.7; updated 12 October as `docs/gods-next-proposals.md` found it): statues that copy a colonist's or a colony
  animal's look. It would serve C3 (a statue of the god's beast at home), C17 and the saints. The seam
  `SaintsPlaces.LikenessComps` is already there.

Not recommended, though subscribed: **Divine Order**
([3017163907](https://steamcommunity.com/sharedfiles/filedetails/?id=3017163907), 1.4 to 1.6) and **Call of Cthulhu -
Cults (Continued)** ([3588548484](https://steamcommunity.com/sharedfiles/filedetails/?id=3588548484), 1.5, 1.6). Each
is a second religion system, with its gods explained, which would blur Rimfolk's unseen gods.

