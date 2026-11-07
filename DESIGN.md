# Rimfolk (formerly Colony Council): design

> Status: version 0.1 is built (all three layers plus room planning). This document explains the
> thinking behind it. See [README.md](README.md) for installing and playing.

## The problem

Mid-game, it's easy to stall: the colony survives, but you don't know what to build
next, where to put it, or what actually matters right now. RimWorld already tracks
a lot of this (mood thoughts, traits, passions, wealth, threat points, food,
temperature), but it's spread over a dozen tabs and alerts, and nothing ties it
into a direction.

## The idea in one sentence

**The colony tells you what it needs. Your colonists tell you what they want. You decide.**

This splits into three layers. Each one works on its own, so we can build them in order:

| Layer | Voice | Purpose | Feels like |
|---|---|---|---|
| **1. Advisor** | Neutral, out of character | "What does my colony objectively need next, and why?" | A smart checklist |
| **2. Pawn wishes** | Each colonist, in character | Personal requests driven by traits, passions, relationships and mood | The Sims 2 wants and fears, Dwarf Fortress noble demands |
| **3. Colony meetings** | The colony as a group | Every so often, pick one bigger ambition to work toward | Frostpunk promises, a light quest system |

All of it is **advice, never obligation**. By default, ignoring a wish or a priority
costs nothing; following one gives a small reward. That keeps it a helper and a source
of fresh situations rather than a chore list.

---

## Layer 1: The Advisor

A panel (the "Council" main tab) that shows the **most important priorities** right
now. Each one has a reason and an optional "show me" button that jumps the camera there.

### What it reads and what it says

| Area | What it checks | Example suggestion |
|---|---|---|
| Shelter | Pawns without beds, barracks vs. own bedrooms, unroofed beds | "2 colonists sleep in barracks (−4 mood each). Next: 2 small bedrooms." |
| Food | Days of food left vs. days until the growing season ends; freezer present? | "Winter starts in 9 days. You have 14 days of food for 5 people; you need ~40." |
| Temperature | Forecast temperature for the next season vs. heaters, coolers and warm clothes | "Next quadrum averages −18 °C. Only 1 heater, 3 pawns lack parkas." |
| Defense | Raid threat points (which grow with wealth) vs. defense: turrets, cover, armed pawns, shooting skill | "Wealth rose 40% this season and defenses didn't. Expect bigger raids." |
| Health | Is a doctor assigned? Medicine stock, hospital beds, clean floors | "No hospital beds. One bad raid and patients will lie on the floor." |
| Power | Generation vs. use, battery storage, solar-only grids | "Solar-only grid with no batteries: an eclipse takes everything down." |
| Comfort | Average mood, recreation variety, dining room, beauty, light | "3 pawns have only one kind of recreation. A chess table would help." |
| Progress | Research gaps against what's blocking you (no electricity yet, no components) | "Research Electricity next: it unlocks most of what's above." |

### How it ranks things

Each check produces a **signal**: `severity × urgency × confidence`.

- *Severity*: how bad is it if ignored (starvation beats ugly floors).
- *Urgency*: how soon (winter in 3 days beats winter in 30).
- *Confidence*: how sure we are (the heuristics are rough; say so).

Show the top 3, with the rest collapsible. Each signal also carries a one-line
**"why this matters"** tip, e.g. *"Raids scale with colony wealth, so getting richer
without getting stronger is the classic mid-game trap."* This is the part that
teaches *you*, the player, not just the colony.

---

## Layer 2: Pawn wishes

This is the fun part. Every so often, a colonist voices a **wish**: a concrete,
checkable request that comes from *who they are* and *what they're experiencing*.

### Where wishes come from

A wish is only generated if there's a real reason in the game state, and the
reason is always shown. Sources:

1. **Current bad thoughts.** "Slept in barracks", "Ate without table", cramped room,
   ugly or dark environment, tattered clothes, low recreation, slept in the cold.
2. **Traits.**
   - *Greedy*: wants a more impressive bedroom.
   - *Jealous*: wants a bedroom as nice as the best one.
   - *Ascetic*: wishes are humble (and might *refuse* a fancy room!).
   - *Gourmand*: wants fine meals.
   - *Chemical interest*: wants a brewery.
   - *Night owl*: wants lights in their workplace.
   - *Brawler*: wants a good melee weapon.
   - *Kind*: makes wishes **for other people**.
   - *Abrasive*: makes wishes as demands.
3. **Passions** (the flames on skills).
   - Crafting: a workshop.
   - Art: a sculpting table and somewhere to show the work.
   - Cooking: a proper kitchen next to the freezer.
   - Medicine: a hospital.
   - Plants: a greenhouse.
   - Animals: a barn.
   - Intellectual: a better research bench.
   - Also: "let me actually *do* crafting", when a passion's work type is turned off for them.
4. **Relationships.** Lovers or spouses in separate beds want a shared double bed.
   A pawn whose friend died wants a grave or memorial.
5. **Expectations.** RimWorld already raises colonists' expectations as the colony
   gets richer. Use that to scale wishes: at low expectations, "a bed of my own"; at
   high ones, "a bedroom worth bragging about".
6. **DLC hooks (later).** Ideology precepts and roles, Royalty titles (which already
   list explicit throne room and bedroom requirements), Biotech children and genes.

### Example wish catalogue (first version)

| Wish | Who tends to want it | Fulfilled when | Reward |
|---|---|---|---|
| A room of my own | Anyone in barracks or sharing a bed with a non-partner | Owns a bed in a room they alone use | +5 mood, 5 days |
| A nicer bedroom | Greedy, Jealous, high expectations | Bedroom impressiveness ≥ target | +6 mood, 6 days |
| Share a bed with my partner | Lovers or spouses in separate beds | Both assigned to the same double bed | +6 mood each |
| A place to do what I love | Major passion with no dedicated room or bench | A bench for that skill stands in a room with the right role | +5 mood, chance of an inspiration |
| Let me do my thing | Passion work type is off for them | Work type enabled (priority ≤ 2) | +3 mood |
| Eat at a table | Has "Ate without table" | Dining table and chairs reachable from the food | +3 mood |
| Something new to do | Low recreation variety | One new recreation type available | +4 mood |
| New clothes | Wearing tattered or dead man's apparel | Replaced | +3 mood |
| Make it warmer or cooler | Slept in the cold or heat | Bedroom temperature in comfort range | +3 mood |

The numbers are placeholders. Balance comes later.

### Lifecycle

```
 game state ──► candidate wishes ──► weighted pick ──► letter + listed in "Council" tab
                                                          │
                       checked every in-game hour ◄───────┘
                          │                │
                     fulfilled          expired (default ~10 days)
                          │                │
               mood memory + thanks    quietly fades
               (+ maybe inspiration)   (optional small penalty in "Demanding" mode)
```

### Rules that keep it from being annoying

- At most **one new wish every 2–3 days** colony-wide, and **one open wish per pawn**.
- Never wish for something the colony **can't build yet** (not researched or no materials anywhere).
- Never wish for something **already satisfied**.
- "Not now" button: dismisses it, and the pawn won't ask again for a while.
- Wishes **expire**. A missed wish is just a missed opportunity.
- Pawns in a mental break, downed, or in a crisis (raid underway) don't wish.

### Personality in the delivery

Same wish, different voice:

- **Kind** pawn: *"Ann has been sleeping in the barracks for weeks. Could she get her own room?"*
- **Abrasive** pawn: *"I'm done sleeping next to Rocco's snoring. I want my own room."*
- **Ascetic** pawn, offered an impressive room: *"This is too much. Give it to someone who needs it."*

**Petitions:** when several pawns want the same thing (e.g. 3 want a rec room), the wishes
merge into one bigger request with a bigger, shared reward.

### Shared wants as one petition (the player's decision 11, 6 November 2026)

In the player's save "proper armor" was wished six times by six fighters in 45 days and given up five times, three of
them asked within a day of each other: six letters, five lapses, each a separate disappointment. A wish many back for
one thing (a rec room, a sick bay: `WishDef.allowPetition`) was already one wish with co-signers; a want each has for
themselves was not. Now (`Wishes/Petitions.cs`, a partial of the component; pure rules in `Wishes/PetitionMath.cs`,
tested in `LettersPetitionsTests`):

- **Joining.** A new want (`CreateWish`, every path: the roll, a trigger, a dream, envy) that someone else in the same
  colony already waits for joins theirs instead of being a wish of its own (`ShareWith`, `PetitionMath.Join`): the same
  def, subject key and thing, both a want of the asker's own (not `allowPetition`, not spoken for someone else); a
  petition under way first (the most names), then the oldest. Each want stays its own `Wish` underneath (each fighter's
  armor is checked on them), linked by `Wish.petition` (the first want's id, saved, -1 alone) and kept out of the
  letters: a message names everyone ("Harper wants proper armor too: Stace, Dukialgac and Harper now ask the council
  for it together"), the line is said over their head, the joiners' bond warms a little, and the chronicle has one line.
  With the "Petitions" setting off, each asks on their own, as for the wishes many back.
- **One deadline, one count, one voice.** Joining moves the whole petition's deadline two days on (`PetitionMath.Deadline`,
  never past a fresh wish's full time, never sooner than it stood) and shares its promise and stage. It counts once
  toward the open wishes' cap (`OpenWishCount`, `LeadsPetition`), shows once on the Wishes page (its header names them
  all, a pill "A petition of 3"), in the inbox and on the on-screen panel, and only its oldest open want reminds and
  makes the last call.
- **Answered together.** Promise, Refuse and Not now answer every open want in it (`ForPetition`: the others quietly
  first, then the first, told once with every name); a promise moves the one deadline on as for one wish.
- **Met one by one, the letter at the last.** Each want is granted as it is met on the map, with its own mood, trust,
  motivation and watch (`Grant`); while others still wait its thanks come as a news line naming who still waits
  (`MetInPetition`), and the names met go on the others (`Wish.petitionMet`, saved); the last one met brings the letter,
  naming everyone, and one chronicle line.
- **Lapsing together.** At the shared deadline the first to lapse tells it once with every name (`LapseTogether`,
  `CC_Petition_LapsedTitle`); the others' own lapses keep their trust, mood and journal, quietly.
- **Older saves.** Wishes already open stay on their own (`petition` -1); a new want of the same joins the oldest. The
  save's armor (days 82 to 128) would have come to the council as three: Toni and Alo's, the three fighters' of day 103,
  Shigeko's; five given up as two petitions let lapse (the tests replay it).

### Stakes setting

| Mode | Fulfilled | Ignored, refused, broken promises |
|---|---|---|
| **Gentle** | Mood bonus, motivation, trust | Nothing (trust is tracked but only its good side has effects) |
| **Balanced** (default) | Mood bonus, motivation, trust | Disappointment scaled by how spoiled someone is, disgruntlement (slower work), jealousy and feeling passed over, lower trust |
| **Demanding** | As Balanced | As Balanced, 50% stronger; bitter, unhappy colonists may sulk or go on an insulting spree |
| **Advisor only** | No mood effects at all, pure suggestions | Nothing |

### Trust, promises and consequences (0.2)

Every colonist has a standing with the council, from bitter to devoted. It changes with how their wishes are
handled and fades slowly back to neutral. It feeds a situational mood thought, how patient they are (wish
duration), how often they ask (bitter colonists mostly stop asking and grumble instead) and flavour tags.

- **Promise / Refuse / Not now** give the player three honest answers besides granting.
- **Reminders and last calls** at 50% and 82% of a wish's time. At the last call, industrious and handy
  colonists may help themselves through the wish worker's `TrySelfHelp` (plan furniture in their room, switch
  their work on, mark a grave, designate an animal to tame, move into their partner's double bed).
- **Ripples when granting**: friends are happy for them; the jealous, greedy, rivals and anyone with an older
  open wish may feel passed over (social memory, trust loss); greedy or jealous onlookers may ask for the same.
- **Effects** are data-driven per wish (`grantEffects`, `ignoreEffects`): hediffs (motivated, disgruntled),
  thoughts, skill XP, needs, trust, grief relief, petition bonds, mental states.
- **Event triggers** (`triggers` on a wish): fresh memories, hediffs, game conditions or a recent big threat
  make the matching wish come up within hours.

### Context-aware writing (0.2)

Lines may begin with tags, e.g. `[winter,night]` or `[trigger:raid,lowtech]`. A tagged line is only used when
all its tags match the moment; matching lines win over generic ones, in the speaker's own tone first. Tags cover
season, time, weather, temperature, mood, trust, tech level, wealth, colony size, age, injuries, traits,
passions, grief, recent raids, repeats, promises, petitions and triggers. Wish letters open with a scene drawn
from tagged scene pools. Tags that hold for most of a game (the era, the colony's wealth and size) only decide whether a
line fits; they don't make it win, or a single `[lowtech]` line would take most of a tribal colony's picks for years.
Each pool also remembers the lines it gave out lately (a third of its lines, up to twelve) and passes over them while
another line fits, so a line doesn't come round again soon. Each speaker has a memory of their own too (`LineMemory`,
the player's save of 6 November 2026: 24 of 630 journal lines were a writer's own earlier line, half within ten days):
their last twenty lines from any pool, and every line of the pool still in their journal, read back from the journal
itself (`LineMemory.Wrote`: the line's own words in order, anything where it names someone, a name's colour passed
over), so a load forgets nothing and nothing new is saved. A pick passes over the pool's recent lines and the speaker's
own, then the speaker's own alone (better a line someone else said lately), then takes any (`ToneLines.Pick` with its
speaker, `Flavor.FromPool`, the conversations' `PickLine`). A journal never takes the very same words twice while it
still holds the first (`JournalTracker.WriteText`, for lines with one wording and a thought's own words). A speaker's
tone draws on its own lines alone only with three or more; a tone of one or two is drawn together with the plain lines
(`LineMemory.ToneDrawsAlone`), so a one-line cheerful voice isn't every cheerful colonist's words every time.
`Tools/variety_report.py` ranks pools by how often their lines come round, and with "own", "again" and "left" how often
one person, the unlucky one of ten, would say their own line again within a year, with no memory and with the speaker's
(`--by-speaker` ranks by it; some journal pools' draws are read from the save).

### Example letter

> **Engie wants a proper workshop**
>
> *"I've been sewing on a bench in the corner of the dining room. A real workshop,
> just a room for the benches, would make my day."*
>
> **Why:** Crafting is Engie's major passion. Their tailoring bench is in a dining room.
> **Fulfilled when:** A crafting bench stands in a room that counts as a workshop.
> **Reward:** +5 mood for 5 days; may become inspired.
> **Expires:** in 10 days.  [Jump to Engie] [Not now]

---

## Layer 3: Colony meetings (direction)

This answers "where am I heading?". About once a season, a **colony meeting** letter
proposes **3 ambitions**, drawn from the Advisor's top signals and the most common
open wishes. Each one is "pitched" by the pawn who cares most about it:

> 1. **"Survive the winter in comfort"**, proposed by Mira (Cooking):
>    60 days of food stored, freezer, heaters in every bedroom.
> 2. **"A real hospital"**, proposed by Doc (Medicine):
>    a room with 3 hospital beds, sterile tiles and a vitals monitor.
> 3. **"Never again"**, proposed by Rex (Shooting):
>    a defensive line with sandbags and 2 turrets covering the main approach.

You pick one (or none). It shows as a checklist with progress. Completing it gives a
colony-wide mood boost ("We built this together") and the proposer gets a bigger one.
The pitch is the hook: it gives the plan a *face* and a story beat.

### Elections: every seat accounted for

An election's candidates are chosen seat by seat (the seats few can fill first, a likely winner out of the running for
the next, nobody on more than two lists), so an open seat can end with nobody standing for it while its holder stands
for another. Its holder keeps it unless voted into the other seat, and then it stands empty for a by-election. At the
count, someone holding a seat that isn't voted on is out of the running only if they don't stand in this election
(`ElectionMath.OutOfRunning`): counting them out left the seat they stood for, unopposed, with nobody to win it (the
player's save: Barr, keeper of days, unopposed for quartermaster, was never seated, and the seat went to a by-election
four days later). The results name every open seat (`ElectionMath.UnvotedSeat`, `Offices.Finish`): a holder who keeps a
seat nobody stood for, a seat left empty because its holder won another, and the seats nobody stood for in one line; at a
by-election only the empty seats were up. A by-election for more than one seat says seats. Each new holder's line in the
results (`OfficeDef.elected`) says what they mean to do, not something done nobody saw ("says the first thing she will do
is walk out to the gate"), since the results come as the votes are counted.

### Elections on a record (the player's decision 9, 6 November 2026)

In the player's save ten of the twelve seats had one promise that always fit, so in a quiet season every candidate ran
on it, and three physicians promised word for word that nobody hurt would wait for a doctor; a promise was words only
once the vote was in. Now:

- **More promises.** Each seat has three or more that always fit, beside the ones for the moment (23 more,
  `Rimfolk_OfficeWords.xml`, merged into each office at startup by `OfficeNames.Apply`; their canvass and hustings lines
  in `Rimfolk_Election_PlankLines.xml`), so three candidates for one seat never make the same promise
  (`ElectionPlanks.Deal` deals distinct ones while there are enough). Each promise has its words ("a proper watch at
  night") and how it is judged (`OfficePlank.promise`, `test`; the offices' own planks get theirs from the office words).
- **A record.** Each candidate runs on their own record, said after their promise in the announcement and on the
  election panel (`OfficeTracker.RecordOf`, `RecordMath.Record`): their work in the seat since their promise, when they
  hold it (the seat's work as the game counts it, `OfficeWordsDef.measure`: wounds and illness tended, plants sown and
  brought in, enemies brought down, trades made, quarrels mended, days kept, animals tamed, rites led, camps greeted;
  "In the seat, Alo has tended to the sick and hurt 23 times."); else that work for the colony in all, from ten; else a
  name the colony gave them; else a life goal reached. A holder who did some of the seat's work leans voters a little
  their way (`RecordMath.WorkLean`, 0.75).
- **A promise kept or broken.** At the count, each winner's promise goes into the history (`office:promise`, a
  milestone: the promise in words on their card, with what the map shows of it then and the seat's work count). The
  next yearly election reads it back when it is called (`JudgeRace`, within a year and a season, not judged before) and
  judges it from what the map shows now (`RecordMath.Judge`, at least ten days in the seat): trust in the council, known
  rivalries, the colony's dead (the game's count), days of food, medicine for each, beds for the sick, the colony's
  mood, the herd, days kept, the faithful's share, or the holder's own work (which only ever keeps a promise: no trader
  may have come). Each test breaks a promise only where the map clearly shows it, and says nothing where it barely
  moved. The verdict is said in the announcement ("At the last election Toni promised a proper watch at night, and the
  colony has lost nobody since: a promise kept."), at the hustings before the votes are read (a holder runs on a kept one,
  `Rimfolk_Pool_HustingsKept`; someone who doesn't back them throws a broken one back, `Rimfolk_Pool_HustingsBroken`),
  on the panel and in the results, and goes into the holder's history (`office:kept`, `office:broken`). At the vote a
  kept promise helps the holder (+1.5) and a broken one costs (−1.75), within the luck of the day (`RecordLean`, one term
  of `Leaning`). Promises and their verdicts are the holders' records, never the chronicle's (`ChronicleMath.Shows`): the
  election's one line tells the seats. By-elections fill empty seats and judge nothing; the instant vote (elections off
  the map) has no promises.

### Seats named by the colony's age (the player's decision 10, 6 November 2026)

The seats had one set of names for every age (speaker, reeve, steward, warden of guests), so a spacer colony elected a
reeve. Each seat now has a name in each age, by the colony's own tech level (its means, `Ages.Ours`), in its office words
(`OfficeWordsDef`: tribal, medieval, industrial, spacer; the medieval one is the office's own label, so a medieval
colony reads what it always did):

| Seat | Tribal | Medieval | Industrial | Spacer |
|---|---|---|---|---|
| Speaker | first voice | speaker | chair | coordinator |
| Marshal | war leader | marshal | captain of the guard | security chief |
| Quartermaster | keeper of stores | quartermaster | quartermaster | logistics officer |
| Physician | healer | physician | doctor | medical officer |
| Steward | feast-giver | steward | welfare officer | morale officer |
| Reeve | seed-keeper | reeve | farm foreman | agronomist |
| Peacemaker | peace-weaver | peacemaker | arbiter | mediator |
| Keeper of days | season-caller | keeper of days | master of ceremonies | ceremonies officer |
| Herdmaster | herd-watcher | herdmaster | head stockman | husbandry chief |
| Chaplain | spirit-speaker | chaplain | chaplain | chaplain |
| Warden of guests | guest-friend | warden of guests | gatekeeper | liaison officer |
| Elder | elder | elder | elder | senior councillor |

Every place the code names a seat says `OfficeDef.Name` (the letters, the results, the chronicle, journals, the
Decrees and election pages, the story tab, the People table, causes, deeds, the advisor, RimHUD); its duties and
description, the offices' own lines (`Interplay.Line`, `Interplay.Journal`), the life goals of a seat (their goal,
deed and lines) and the seats' marks in the health tab (`Patch_OfficeHediffLabel`) say a seat's old name with the age's
(`OfficeNames.InAge`, the pure `OfficeNamesMath.Rename`: whole words, the plural and a possessive too, the article put
right, never part of a word or a placeholder); so do the keyed lines about the colony's own seats (the peacemaker's and
the keeper's causes, the defense pane's marshal, the herdmaster naming a newborn, the advisor's empty seats, the colony's
speaker at a ransom), renamed before anything is filled in (`OfficeNames.Key`), so a dog called Marshal keeps its name.
Lines elsewhere that mention a seat in passing (a dilemma's "the
quartermaster's friends", a camp's elders, other peoples' speakers) are left as written: renaming them would rename
other peoples' elders too.

---

## Layer 4: Society (the social web)

Vanilla RimWorld tracks opinion between every pair of colonists, but opinion has no memory of *why*. The
social web adds a small number of named, lasting bonds on top of it, each with a strength and a history:

- **Friends / rivals** form when mutual opinion stays high or low, then drift towards where opinion is now.
  Their own opinion bonus is small enough that a bond can't hold itself up: without good (or bad) things
  happening, it settles or fades.
- **Comrades** come from dealing damage in the same battle, and fade between battles.
- **Mentorships** pair a skilled, passionate colonist with a keen learner and end in graduation.
- **Circles** are connected groups of strong bonds, recomputed daily; with social life on, they are the persistent
  groups ("Layer 8: Social life").

Design rules:
- Everything the council does should be able to touch a bond (elections, overrules, passed-over wishes,
  dilemmas, decree arguments, petitions), and bonds should touch the council back (votes, objections,
  petitions, trust after a lapse). That loop is what makes the colony feel like a society rather than a set
  of mood bars.
- Bonds are announced once, when they become real; budding ones stay quiet. Endings are announced only for
  bonds that were.
- Social events are small and frequent (about one per five colonists per day), mostly worth a few points of
  opinion; the dramatic ones are rare and go in the chronicle.

### Banishment by the council (the player's request, 5 November 2026)

The player's words: "everyone in my colony seems to hate him, and they are regularly starting social fights. i don't
want this necessarily to happen immediately, but i think the council should also be able to decide to banish a
colonist, no? if they are not liked by anyone and only causing trouble." Never a first answer: the council has a word
first, and only if the trouble goes on does a later question offer to send them away.

- **Who.** A grown colonist of the colony (never a child, a prisoner, a slave, a quest lodger, a guest, or anyone held
  under a tie with another people: a ward, a hostage, an envoy, a spouse of a marriage alliance), in a colony big enough
  to spare someone, who over the last month kept causing trouble and whom most of the colony dislikes with almost
  nobody standing by them. The trouble is read from the game's own records and Rimfolk's, never made up: social fights
  they were in (from the game's fight memories, and from Rimfolk's own note of each fight as it starts, which knows who
  threw the first punch) and the insults they threw (the insulted person's memory of it). The thresholds are named
  constants in `Social/BanishMath.cs` (pure, tested).
- **First a word.** The first question about them (`Rimfolk_Dilemma_Trouble`) offers a word face to face from the
  council's speaker (or the faith's leader), remembered as a warning; their hours turned to the night for a few days
  (the game's own timetable) to keep them apart from the ones they fight with; or letting it be (no question about them
  for a while after).
- **Then, if it goes on.** After a word or the nights apart, more trouble brings a second question
  (`Rimfolk_Dilemma_Banish`) with banishment beside the milder answers. The letter carries the council's voices, each by
  their own opinion of the person, the objection of a spouse, kin or friend by name, and the person's own say. The
  answer's hints name who would grieve, who would be glad, the game's heavier mood when they'd be sent out to die (hurt,
  ill, no safe weather: the game's own check), the skills the colony loses, and the risks of their coming back.
- **On the map.** The speaker (or whoever the answer named) walks over and tells them face to face, one sentence each;
  they go to their room to gather their things and walk out by the map's edge, through the game's own banishment and
  its own moods. The chronicle, the history, their card and the world's memory of them keep it by name.
- **Every age.** The words follow the colony's tech level (cast out from the fire, sent into exile, expelled, struck off
  the colony's roster) and the faith's own name for its leader.
- **The bad side, at the game's rate.** Those who loved them stay hurt a while; one sent away may come back one day:
  rarely among raiders with a grudge, or at the gate asking to be let in (the existing event, a later question).
- **Off switch:** Settings, Council, "Banishment by the council".

### A vendetta passed on (the player's decision, 6 November 2026)

When raiders kill or carry off someone, those who loved them swear a vendetta on that people (`Secrets/Vendettas.cs`).
An avenger who dies with it still open, while the war with the killers goes on, leaves it to whoever was closest to them,
a partner, a friend or a relative, by how close they really were: never to blood kin first, as it was until this day
(the colony is the family, never a dynasty).

- **How close** (`Secrets/VendettaMath.cs`, pure, `VendettaTests`): the game's opinion of the dead (which carries the
  game's own weight for a relation and what passed between them), the social web's warmth between the two (friends'
  strength, comrades' and a mentorship's, less a rivalry's), and a partner (40) or a relative by blood (20) as one part
  among them, added. Close enough at 60, as an opinion of 60 alone; the kind need twice that, the bloodthirsty two
  thirds. The closest of those who would takes it up; nobody close enough, it ends with them.
- **Who may, as before**: a grown colonist with an inner life, never a child or a lodger, never one already sworn on the
  same people, and only while the colony is at war with the killers.
- **Noted at the death**: the day's look lets the social web's bonds with the dead go before the vendettas pass on, so
  the closest is noted in the tracker's hour as the death is seen (`VendettaEnds.NoteHeir`, after those who swear for the
  dead; `Vendetta.heirId`), and passed on at the day's look if they still can; otherwise, and for an older save, worked
  out from what is known then. Until this, the web's strong tie never counted.
- **The words** follow who takes it: the told line's tag is what they were to the dead (`partner`, `kin`, `friend`); the
  two lines that made it the family's oath or a family debt are retired (`ShippedLines.Retired`), and a relative who was
  close has words of their own (`Rimfolk_Lines_VendettaCloseness.xml`). A dead colonist's life goal taken up by someone
  close (`Aspirations.cs`) is as it was.

## Layer 5: Rimfolk events

Rimfolk's own incidents: things that happen *to* the colony from outside, as opposed to the wishes and dilemmas
that come from inside it. They can stand on their own, but most reach into the rest of Rimfolk.

- **Plumbing.** Each event is an ordinary `IncidentDef` in its own category (`Council_WorldEvent`) with a
  `HappeningExtension` (texts, options, effects, knobs). No storyteller lists that category, so the storyteller's
  own pacing is untouched; Rimfolk's `HappeningTracker` fires one about every *n* days (a setting), picking by
  weight among those whose `CanFireNow` passes, with vanilla's own refire, earliest-day and biome rules. Dev mode
  can fire any of them. Options reuse the dilemma machinery (`DilemmaOption`, `DilemmaEffect`), plus new effects:
  join, raid later, era loot, research progress, a bond, a mentorship, a secret, leaving the colony.
- **Who comes.** Strangers are generated when the event fires and wait as world pawns until the colony answers;
  then they walk in from the edge, or stay out in the world (family stays family). Visitors come from a people that
  isn't hostile, of the colony's own era by preference, and spawn with a visiting lord.
- **Pasts.** A colonist's past is read from their backstory titles and categories by keyword (soldier, outlaw,
  noble, healer, scholar, farmer, trader, artisan), so modded backstories count. Lines tagged with a past only show
  for someone with that past; events that need a past (an old comrade needs a soldier or an outlaw; bounty hunters
  need a violent past) only fire when someone has one.
- **Fights.** Only with the battles setting on, threat scale above zero, no hostile threat on the map and no big
  threat in the last two days. Strength is a share of `DefaultThreatPointsNow`, sent through the vanilla raid
  incident with the event's story above vanilla's raid text, and recorded with the storyteller as a big threat so
  it doesn't stack its own on top. A promised fight (refused tribute, creditor's thugs) is queued and re-checks
  all of this when it's due.
- **Era.** Every text has lines for pre-industrial and industrial-or-later colonies (`[lowtech]` / `[!lowtech]`, and
  `[industrial]` / `[spacer]` where it matters). Loot is drawn from things at or below the colony's tech level.
- **Choices cost something both ways.** Turning family away hurts; taking in a deserter may bring pursuers; paying
  tribute shames, refusing it brings a war party. Unanswered letters fall to a sensible default.
- **Harm at the game's own rate.** Until November 2026 nobody was hurt for flavour (contests are matches at targets,
  wrestling without weapons, crafts and debates; hail damages crops, never people). Since then the player's rule is
  that harm is part of the game: injuries, illness, attacks and fights are welcome where they're flavourful or make
  the game more interesting, at a rate comparable to the game's own chance of something bad happening, never
  gratuitous, always from something that happens on the map.

### Scenarios with stakes
Rimfolk's scenarios use the same machinery to start a game with stakes in play: a nemesis from day one, a debt
that comes due as a Rimfolk event, a "hunt" (a promised raid that comes back every few weeks while the one leading it
lives), deserters whose old unit finds them, two feuding watches of a colony ship seeded with shipmate and feud opinions, a starting reputation,
a scenario-only secret. Everything is set up once, a few seconds into the game, and kept in Rimfolk's own save data
(the scenario parts hold no state). The one exception to the battle switch: fights a scenario promises ignore the
Rimfolk-event battles setting, because the player picked the scenario for them, but they still need a storyteller
that sends threats.

## Layer 6: World events

Rimfolk events happen to the colony; world events happen to the region, and last. A plague among the neighbors, a
warlord's rise, a chief's death: rare (about one every 30 to 60 days, at most two at once, never two of one kind),
from a week to a month long, and on Rimfolk's own rhythm rather than the storyteller's.

- **Plumbing.** Each is a `WorldSituationDef` (Defs/WorldSituationDefs/): who it's about (a people, a second people, a
  colonist), its letters, its *modifiers* (factors on the storyteller weight of Rimfolk events, through
  `HappeningTracker.StorytellerFactor`, and of the game's own incidents, through a Harmony postfix on
  `IncidentWorker.BaseChanceThisGame` behind the deeper-hooks switch), its *beats* (letters, questions, arrivals,
  fights on set days or brought on by an answer) and its *endings*. A `WorldSituationWorker` adds what data can't say.
  The tracker is a game component of its own (`WorldSituations`).
- **What the colony did.** Answers set flags and count as the colony's part; endings, effects and every line of text
  can read both, so an ending pays off the choices. The region remembers: a people the colony helped is helped-before
  in every later world event (the [remembered] lines, a healer's half price).
- **Real changes, carefully.** A new town is the game's own settlement, made the way world generation makes them; an
  abandoned town is removed only when nothing of the player's depends on it (no map, no caravan on the way, no quest,
  not the people's last town), else a few families stay. A leader is replaced through the game's own
  `TryGenerateNewLeader`, and the old one dies only after, so the game sends no letter of its own. Peace between two
  peoples is the game's own relation. Goodwill losses stop short of hostility.
- **The warlord.** Resolvable without storming his town: the neighbors at war with his people hunt him off-screen
  (a daily chance growing with time and their strength, raised by the colony's silver or a bounty), an ally can march,
  his band breaks up without plunder, or the colony ends it at home. His people's raids are stronger and likelier while
  he rises (Harmony, behind the deeper hooks), and he walks in with some of them.

## Layer 7: The heart (feelings and strain)

Comfort is easy to buy in RimWorld, so mood alone stops meaning much once a colony is rich. The heart adds a slower,
richer layer under it without touching the number the game runs on.

**The safety rule.** Vanilla mood stays the single number the game and every other mod use for mental breaks. Nothing
here patches the mood need, the mood calculation, the mental breaker or which breaks the game picks. The layer lives in
Rimfolk's own save data (`HeartTracker`, a game component of its own) and reaches the game only through things it
already supports: one situational thought (it reads the strain band each time and stores nothing), one stat part on
global work speed (an XML patch, conditional so it never fails), one mental state started on purpose, and Rimfolk's own
systems (wishes, life goals, dreams, decrees, dilemmas, moments, the UI). With the feature off, the thought and the
stat part contribute nothing and the hooks return 1.

- **Strain** (0 to 100) is a slow reservoir. Each colonist is looked at once an hour, a tenth of the colony at a time.
  Sources build it (18, small letdowns among them: see "One heart" below) with their own daily caps and a cap of about 25 a day for everything; new memories count once, when
  they're new (a renewed memory, like a second cold night, counts again). Drains are slow and steady (sleep, quiet hours,
  recreation) with chunks for gatherings, good talks and catharsis. Small decaying totals per source and per kind of
  relief say what built it and what eased it lately. The pure math (bands, caps, trait factors, episode odds) is in
  `HeartMath`, covered by the tests.
- **Bands** give the only direct effects: a mood of -3/-5/-8, work speed -5%/-10%, darker dreams.
- **The good side** is strain's other side, never both at once (see "The heart's good side" below): it fills slowly
  while strain is at nothing, hardship spends it first, and its bands (steady, in good heart, in high spirits) give a
  second situational thought of the same kind (+2/+3/+5), the upper two brighter dreams and a quicker recovery, never
  work speed.
- **Episodes** are the pressure valve, not a punishment: a custom mental state on vanilla's hide-in-room class, started
  silently by the heart (its own letter) and never by the game's break picker, since no MentalBreakDef names it. A think
  tree inserted after the mental states gives it its behavior. Strict conditions (no danger, enough hands, cooldowns,
  nobody left uncared for), and it ends early when a threat appears, a doctor is needed or the room turns too cold or hot.
- **Feelings** are data (`FeelingDef`): thoughts that feed them (by name, pattern or a `HeartTags` extension on the
  thought), a worker for what thoughts can't say, a threshold, and nudges scaled by intensity. They never add mood,
  which would count their causes twice. Nudges stay small: wishes at most half again as likely, moments within half to
  double, strain a few points a day.
- **The heart's needs** (belonging, purpose, safety, freedom) are computed each hour, never saved, and give no mood:
  they show what a life is missing and point wishes and life goals toward it.
- **Removal.** No hediffs and no stored thoughts. The only trace is a colonist in the middle of an episode.

### Life goals that last (the player's decision 8, 6 November 2026)

A life goal is chosen from what fits a colonist (`AspirationDef.who`), made likelier by a spark lately (a memory, a
fight, a brush with death, a loss, a child born, a dream come true, someone close reaching the same goal). In the
player's save, of 38 goals chosen in 88 days 25 came after a battle or a brush with death ("bring down ten mechanoids"
five times), and Stace changed hers four times in 25 days: every raid was a spark for everyone who fought, and a goal
gave way to any spark after ten days. 0.112.0 let a fight spark only the one who brought down the most, at 0.4, and no
goal came from fights at all; the player's word the same day: "some lifegoals should be sparked by fights, no? not 0."
Now (`GoalMath`, pure):

- **A fight sparks those who fought hardest.** The three who brought down the most in it (`FightTop`, more first, the
  first noted of equals), anyone of its fighters downed in it (the goals' own "downed" event since the fight began,
  `AspirationTracker.HappenedSince`) and anyone of ours who carried someone out of it (`FightSides.Carried`) take the
  fight as a spark (`GoalMath.Hardest`, read in `SocialWeb.HardestIn` before the battle's count is reset and before the
  fight's sides are cleared). A brush with death and a loss are still each person's own.
- **The spark's weight spread over a life.** A fight's spark pulls a little less than the rest of life (`SparkPull`:
  0.7, it was 0.4; a nemesis 0.6, everything else 1), both in the chance a spark makes them choose at once (0.6 times
  the strongest pull of their sparks) and in which goal they take. A goal others of their colony hold now weighs less
  (`Shared`: half for one, a third for two), and a fight never sparks a goal someone else at home took up after a fight
  and is still at (`FightMaySpark`, from `Aspiration.spark`), so one fighting goal is never chosen by several at once
  and the colony doesn't fill with mechanoid hunters. Where a goal has several sparks that happened, a brush with death
  or a loss counts first.
- **A goal holds.** A change of heart only after twenty days with the goal (`HoldsDays`, it was ten), only while it's
  under a third done, only for a brush with death or a loss (`Big`), one chance in five a day, and once per goal: the
  goal it brings holds for good (`Aspiration.turned`, saved). A fight or a fine meal never changes a goal held.
- **Measured.** `FairCouncilTests` runs twelve colonists over the save's 88 days, fighting as often as the save (21
  fights in its 128 days, one every six days: nine fight, one is downed and another carries them out), goals reached in
  15 to 90 days and 20 days before the next, fifty runs of a colony under way: goals from fights were 53% under the
  rules before 0.112.0 (the save's own share was 23 of 47), 3% on 0.112.0's and 19% now (a fresh colony 11%: its first
  goals come before its first fight); never two holding one goal from fights at once (six before); changes of heart 16.5
  a run before, 0.3 now, never more than one for a colonist within 25 days. Goals held in an older save stay as they
  are.

### One heart: small letdowns, small joys and good things growing ordinary

Phase 3 of the review (`docs/review-2026-11/`), the player's decisions of 6 November 2026: small letdowns wear on the
heart ("the letdowns also make sense as mood impact, no? But I like strain too though!"), the good side the same way
("the positive mood buffs should also be adjusted accordingly and balanced"), every small mood by the same rule whoever
made it ("then we might also have to look at non-rimfolk moods, right"), and one rule for good things growing ordinary.
The game's mood stays the one number for breaks (the safety rule above): nothing here changes a mood the game or another
mod made.

- **Small letdowns** (`Moods/SmallMoments.cs`, `SmallMomentMath`). Rimfolk's five kinds (everyday pressures, setbacks,
  quiet troubles, nuisances, frictions: a `SetbackExtension`, a `Thought_Pressure`, `Thought_Setback` or
  `Thought_Trouble`, or a `Council_Friction_` or `Council_Nuisance_` name) are each felt as a short, small mood at the
  moment they happen: three points at most, half a day at most. What they would have weighed beyond that moment (their
  size times their days, less the moment's) goes to the heart as strain (`StrainSource.Letdowns`, at the end of the
  list, saved by position), 0.6 a mood-day, within the heart's caps (1.5 a day for the source, at 100% strength, their
  traits counted), so a run of them wears while one passes. The memory that brought it goes on without a mood (its
  `moodPowerFactor` 0) for what else it does: a friction's opinion of the other, what dilemmas, arcs, speech and
  journals look for. The hook is the one every memory passes (`Patch_GoodTimesFade`'s prefix); the heart leaves such a
  memory to its moment (`SmallMoments.Converted`).
- **A lasting one** (the same meal again, days of the same work, someone away, a crowded room, half-built work, a wish
  still waiting, a decree dragging on, a sick friend...) gives no mood of its own any more: a moment when it begins,
  gets worse or holds another day, and strain each hour while it holds at the same rate (its size a day). What the hour
  last saw is kept on the heart unsaved; after a load, what holds already brings no moment until it changes.
- **Small joys**, the mirror: Rimfolk's small good moments (its good memories that no council answer gave, a moment,
  a talk, a kindness, scaled as they're felt, of five points and two days at most as written; a varied table) are each a
  short, small mood at the moment, and what they'd weigh beyond eases strain (`StrainRelief.Joys`, at the end of its
  list), 0.6 a mood-day, within a day's cap of 3. Council rewards, life goals reached and dreams stay moods of their own.
- **A row each.** Each moment is a `Thought_SmallMoment` of one of two defs (`Rimfolk_SmallLetdowns`,
  `Rimfolk_SmallJoys`) carrying its own size, time, words and cause. It has a row of its own under its own name
  (`LabelCap`; the player's request, 6 November 2026: "i'd rather be able to see the small letdowns listed"): the game
  groups thoughts by `GroupsWith`, which here holds only for the same label, so the Mood tab and the game's Needs tab show
  "Not asked along" and "Argued about the temperature x2", added up as they are, and the row's tooltip says what it was
  ("My idea turned down: -2. The council chose the well.") and that the rest goes to the heart. What helps is the fix
  of the letdown behind the row (`SmallMoments.Advice`), else its own hint. Each one still feeds the
  feelings its own memory fed, at the moment's size; a lasting one at its full size while it holds.
- **Every small mood, anyone's.** Each hour the heart adds up every small mood on a colonist's mind, the game's and
  other mods' too, by size and length, not by name (`SmallMomentMath.IsSmall`: six points or less, three days or less
  or lasting): past 8 points of small bad moods each point builds 0.02 strain an hour, past 12 of small good ones each
  eases 0.02, through the same source and relief and their caps. The heavy kinds that have strain sources of their own
  are left to those, so nothing counts twice: the memories the heart counts by its own rules (losses, bad nights, the
  council's hard choices, gatherings, talks), hunger, rest and recreation, pain, the dark, the heart's own strain mood,
  and the moods that feed a feeling that wears (fear, grief, dread, despair). The moods themselves stay as the game and
  the mods made them; only Rimfolk's own became smaller, and only Rimfolk's share a row.
- **The heart's page** names what wore on them and what eased them: "small letdowns (the same meal and my idea turned
  down)", "small joys (a laugh with Ana)" (`PawnHeart.letdownsWhy`, `joysWhy`, saved words like `natureWhy`), and
  "fewer of the small things that keep going wrong" as what would help.
- **Good things grow ordinary** (`Moods/GoodLife.cs`, `GoodLifeMath`), one rule in place of four (standards shrinking the
  council's rewards by up to half and the game's comforts to 90, 80 and 70% on their first 10 points; good memories
  fading up to half sooner from 75% mood; good moods adding up slowly, the third to fifth at 60, 45 and 35% and a quarter
  after; used to the good life). The sum of every good mood on their mind counts in full up to a line their standards
  set, 45 for the modest, then 35, 25 and 15 for the spoiled (a colony used to the good life asks more); of what's above
  it, half of the first 20 points and three quarters of the rest no longer lift them, taken off the good moods by how
  ordinary each is: a fresh joy counts nearly in full and drifts down with its age, an everyday comfort (a lasting mood,
  a kind had often lately) shrinks most, a life goal reached never. One line in a good mood's tooltip says it ("Grown
  ordinary: counts as +3 of +5, part of everyday life now."), one line under the Mood tab gives the sum. One setting,
  "Good things grow ordinary" (`goodGrowsOrdinary`); an older settings file sets it on if any of the four was
  (`GoodLifeMath.MergedSetting`, `ordinaryMerged`). Standards stay (they set the line, sting letdowns up to 40% more and
  make wishes come sooner); rewards are given at the reward strength alone.
- **Off.** With the heart off, the deeper game hooks off or "Advisor only", small letdowns and joys are moods of their
  own as before. Older saves: memories given before keep their moods and run out as they were; a lasting one turns to
  moments at the next hour; the heart's lists grow by a source and a relief (padded with nothing); shortened memories
  from the old fading rule keep their time.

### The heart's good side: in good heart, in high spirits

The player's request of 6 November 2026, approved as proposed ("there should perhaps also be a counterweight/side to
strain, right? If they have positive impacts over an extended period"): one heart with two sides, not a new system.
`Heart/GoodHeart.cs` (the rules in `HeartMath`, "The good side"), `GoodRelease.cs`, `HeartCard.cs`.

- **One axis in effect.** The good side (0 to 100, `PawnHeart.good`) and strain are never both above nothing.
  Hardship spends the good side before strain builds (`HeartMath.Spend`): each point of strain a source would build,
  after its own caps, spends half a point of the good side (`SpendShare`, someone in good heart takes a knock in their
  stride), and strain builds only from what is left once it's gone; the hour's sources, a moment's letdown from outside
  the hour (`HeartLogic.Build`) and an answer's strain (`HeartLogic.Change`) alike, on its record by strain's own
  source (`goodSpent`). Good things ease strain before they fill it: the hour applies rest first (sleep, quiet,
  recreation, the decree), then each good thing in turn (gatherings, good talks, small joys, the pile of small good
  moods), each easing what strain is left, and what is beyond fills the good side at its share (`GoodShare`: 45%, 60%,
  60% and 15%). Rest never fills it.
- **What fills it, slowly, mostly what can't be bought.** Only while strain is at nothing (`GoodHeart.Fill`), within a
  cap a day for each source and 4 a day in all (`GoodCap`, `GoodDailyCap`; `goodToday`, saved): friends close by and a
  purpose (the heart's needs of belonging and purpose well met, 60 or more: 0.035 to 0.07 an hour each, 1.2 a day),
  a life goal moving on since yesterday (0.6 at the day's turn: `goalTick`, `goalProgress`), a deed of theirs the colony
  speaks well of (its sides add up to good, `RepMath.Valence`, and someone else knows of it: 1, up to 2, 2 a day; read
  from the reputations once a day, `deedTick`), gatherings (2.5 a day), good talks (1.5), a run of small joys
  (`StrainRelief.Joys` beyond the strain they have: 1.2), and the comforts of home (the pile of every small good mood,
  the game's fine rooms and meals among them: 0.4 a day, the smallest cap and share, so a rich colony doesn't simply
  buy it). The day's joys cap counts what a joy eased or filled. It fades slowly (about 3% a day, `GoodFadePerHour`).
- **The bands** (the player's names), as the player approved them (6 November 2026, "As I approved": three bands that
  each give a mood): steady from 20 (`SteadyAt`), in good heart from 45, in high spirits from 65; below 20 nothing to
  speak of yet (`GoodBand.None`, no mood, the meter says "nothing yet"). A small good mood each, +2, +3 and +5
  (`Council_Heart_GoodSide`, one stage a band, a situational thought reading the band like strain's, nothing stored in
  memories; it grows ordinary like any good mood, and the pile of small moods leaves it out, `SmallMoments.Heavy`). In
  good heart and in high spirits also brighter dreams (sweet dreams 1.2 and 1.4 times as likely, nightmares 0.85 and
  0.7, `Heart.DreamFactor`) and a quicker recovery from strain (rest eases 1.15 and 1.3 times as fast), read from how
  high the good side stood lately (`goodLately`, halving in about three days), so someone in good heart before a hard
  spell gets over it sooner; steady gives only its mood. No work speed. Shipped first (0.113.0) as two bands, +2 and
  +3, steady giving nothing, to keep a well-run colony on its target; the player chose the stronger reward as first
  approved, knowing Rimfolk's good moods at a high point rise with it (see "Balance targets"). The band is never saved
  (read from the value), so an older save's colonist above 20 is steady at once.
- **The release**, the mirror of strain's day off (`GoodRelease`): in high spirits, about once in four days held there
  (`ReleasePerHour`), at most once in 10 days for each colonist and once a day in the colony (`lastReleaseTick`,
  `HeartTracker.lastColonyReleaseTick`), only when it's safe, at home, awake, an adult free colonist, not drafted,
  downed, in a mental state or at a gathering. Either one of the game's own inspirations, which the game picks by their
  passions and its own rules (`InspirationHandler.GetRandomAvailableInspirationDef`, `TryStartInspiration` with its own
  letter and Rimfolk's reason at its foot; a word said as it begins), 12 off the good side; or a kind word for someone
  close (a partner, kin or a friend by opinion 20 or more, the one under the most strain first, then the lowest in
  spirits): `Together.When` with a walk over in their free time, the line said face to face (`Speech.Say`, one
  sentence, `Rimfolk_Pool_GoodHeart_KindWord`, by who hears it: under strain, grieving, low, hurt, a partner, kin, a
  child, a newcomer), and the other gets a small good memory (`Council_Heart_KindWord`, +3 for 2 days, opinion +6, which
  comes as a small joy and eases their strain or fills their own good side), 6 off the good side. A kind word is set
  going when chosen; if the two never meet within ten hours, it doesn't come. A small gift and a hand with the work
  weren't built: the game has no honest way to show either on the map.
- **Words** in every age: the heart's page (the Mood tab's Heart section) has a second meter, "Good heart", with what
  filled it ("friends close by (+6), a purpose (+4)") and what spent it ("fighting (-6)"), a line while strain holds,
  and the last release; the inspector line and RimHUD read the good band from steady ("Heart: in good heart ·
  proud"); the strain meter's tooltip names the other side; the colonist's card (the Now slot) says the band and what
  filled it; the council's Moods page counts who is steady, in good heart and in high spirits; the band mood's tooltip
  says what filled it. Keys in `Rimfolk_GoodHeart.xml`, the bands' own in `Rimfolk_GoodHeart_Bands.xml` (the first
  file's band words, explanation and count left unused); the Almanac page "the heart's good side"
  (`Almanac_GoodHeart.xml`), the heart's page with its bands in the table.
- **The setting** "The heart's good side" (`heartGoodSide`, on), on the How hard page beside how fast strain builds and the day off (the heart's own switch is on What's on).
  Off (or the heart off): nothing fills it, what there was is gone at the next hour, the bands give nothing, hardship
  builds strain as before. Dev mode: fill it by 20, and overflow now.
- **Saves.** Values on the heart (`good`, `goodTodayTotal`, `goodLately`, `deedTick`, `goalTick`, `goalProgress`,
  `lastReleaseTick`, `releaseWhy`) and three short lists (`goodToday` and `goodRecent` by source, `goodSpent` by strain's
  sources; `Tools/save_estimate.py` models them). An older save loads with no good side and starts at nothing; the first
  look at deeds and the life goal only takes note.

## Layer 8: Social life

One feature in four parts, under one setting (`socialLife`, "Personalities and social life"): **character**
(`Persona/`), **conversations, views and shared memories** (`Social/Talk/`), **groups** (`Social/Groups/`) and
**reputation and gossip** (`Social/Gossip/`). Character is the root everyone reads: its sides give the tone of every
line (`Persona.ToneOf` in `ToneUtility.ToneFor`) and the `persona:*` tags, so a colonist sounds the same in a talk, a
story passed on and at their group's fire. The parts never call each other's internals for what they say: groups and
names reach the lines as tags (`group:`, `grouprole:`, `rep:`), and the shared pieces sit in the foundation
(`SharedMemories`, stances, `CharacterPage`). Rules that hold across all four:

- **One happening, told once.** A fight has one name everywhere (`FightNames`: who came and the day, kept while it
  lasts), so a mark, a shared memory and a deed name the same raid, and two raids by the same people are two
  memories. A happening gets one journal line (`JournalTracker.WroteWithin` for lines written in the same moment,
  `FirstOnLoss` for a death, which several memories and systems notice): a mark's line, the council's, the grief, the
  bond lost and the group's step back for the one written first.
- **One pace for Rimfolk's talk.** Moments (the groups' and names' moments are moments), conversations and gossip keep
  their own waits, and on top `SocialPace`: an hour between any two of them started by the same colonist, and none
  with someone mid-conversation. Their lines go through the talk's queue in turn (`ExchangeQueue`, gossip included).
  Measured against the picker's rhythm (0.6 started a waking hour), a colonist is in about 5 Rimfolk-made exchanges a
  day next to about 14 of the game's own, a quarter of all; `momentFrequency` scales every one of them.
- **Opinion is explainable.** Each part adds its own labelled line in the Social tab, small and short: a talk's 1 to 6
  (2-4 days), a name's -12 to +7, "one of us" +4 and "one of that lot" -5/-10, the group moments' -8 to +6 (1.5-3 days),
  next to the bonds' own (friends up to +18, rivals down to -18). One happening reaches a pair through few of them,
  each saying why: a rescue under fire is the game's own "rescued me", a deed that adds to a name, and the fight's
  comradeship; the memory of it only counts again when they talk about it (a few points, for days).
- **Roles follow character.** A group's role never goes against what shows (no cold anchor, no dour joker), and the
  crowd's in-joke is only for two with none of their own (conversation brings up theirs).
- **Off means off.** With the setting off nothing new is said or kept, the old circles come back at once, talks under
  way stop, and the page shows nature alone; old saves and removed defs load (records of missing defs are dropped).

### Character: nature, marks and the character page
A colonist's character is eight sides from -100 to 100 (`Persona/`). Nature is worked out from the game (traits through
`PersonaNatureDef`s, the Social skill, age, a stable personal lean) and never saved; what happened to them is (lasting
and fading moves, the marks themselves). Marks (`PersonaMarkDef` with a `PersonaMarkExtension`) are data: a family, the
memories that bring it, a condition on nature ("nerve>=10"), a lean that scales it by nature (0.4 to 1.6), journal lines
and news for turning points.

- **One way in per happening.** Memories as they're gained (the one Harmony hook, shared with good times); otherwise
  one-line calls where the colony already notices things: a fight won and a breakdown (WorldReactions), the downed
  during a fight (quirks' hourly look), elections and the speaker's death (Offices), bonds lost (SocialWeb), vengeance
  sworn, the sick pulled through, fine work, and a daily look in the council's day for the slow ones.
- **A fight won** is one with a real threat (`Battle.WasThreat`, `BondsMath.WasThreat`): the storyteller's big threat, or
  a raid Rimfolk sent itself (`Raids.Send` notes it on the home's fight, `Battle.raidSent`, saved), told to the
  storyteller or not. A raiders' camp's raid isn't told to the storyteller, so its fights were never won before: no
  victory, no marks, no deeds, no calendar day (the player's save: the marks' last fight day 114, the camp's raid day 124).
- **Nature picks, then scales.** The first mark of a family whose condition fits is given; the same raid hardens one
  colonist and shakes another. Nothing is random but a few small kindnesses (2 in 5 leave a mark).
- **Rare enough to read.** `MarksMath.Blocked`: one small mark per three days, a turning point never twice within its
  keeping, one happening one mark (a story and who it was about), two turning points a day at most. A person keeps 12
  marks; small ones go first.
- **The one reading of who someone is** (phase 3 of the review, November 2026). The colony's ways read a colonist's
  eight sides, not a table of their own from traits (`PersonaMath.WayLean`, `CharacterTracker.Affinity`): mercy is
  warmth without a temper, generosity warmth without pride, tradition the set in their ways, openness to strangers warmth,
  trust and curiosity, hard work pride in it while the joker takes it easy; a side counts past 15 (the personal lean says
  nothing) and in full at 55. Who minds an answer ("not my choice", "told them so") and the ways' mood follow, and change
  as marks move a person. A stranger's fighting temper reads temper, nerve and warmth (`PersonaMath.FightTemper`). The
  game's traits are the input to character, not a second reading: what the ways' table read and character didn't
  (industriousness, a bloodthirsty temper, a few mods' traits) is in `Rimfolk_PersonaNature_Ways.xml`; a mod's personal
  trait no longer leans the ways by itself (`ModPeople.OnlyTraits`), a kind or a belief still does.
- **Soft feedback only.** Character tips weights already there and never adds mood: moment weights 0.7 to 1.35
  (`Heart.MomentWeightFactor`), soreness half again for hot-heads (`MomentTones`), confiding 0.4 to 1.6 and mentors
  0.5 to 1.6 (with the systems talking to each other). Conversation, groups and gossip read character through their
  own code and the `persona:*` tags.
- **The page.** StoryView draws the sections of `CharacterPage` (character 10, what shaped them 20, then the other parts'),
  with eight bars and per-side tooltips worked out the same way as nature. With social life off only nature shows.
  Each section carries its registry key, so a page picks its parts (`CharacterPage.Make` asks one): the People roster
  shows them all, the Story tab character, shaped and traits, the Culture tab views and ideoligion, and the Social tab's
  column groups, reputation and memories (see "A colonist's tabs").

### Groups

The social web's circles used to be rebuilt every day from strong friendships, with no identity or memory. With
"Personalities and social life" on, they are persistent groups (`Social/Groups/`, saved in `GroupTracker`, a game
component of its own, so an old save just starts without it and finds its groups quietly on the first day).

- **Forming.** Once a day, with the social web's day, every pair gets an affinity from the game (`GroupFacts` →
  `GroupMath.Affinity`): friendship and rivalry most, then shared work (sampled hourly), comrades, the night hours,
  likeness (`Persona.Likeness`), an ideoligion held hard, arriving together or the same kind of life before, age. A cluster of
  three to seven with no rift becomes a group; someone joins the group they hold to (and never one they left) and drifts
  off after days of not holding to it. Two groups each at most.
- **Real divides** (the player's decision 13, 6 November 2026). In the player's save one group held 7 of 12 while Barr,
  whom everyone fights with, belonged to none: after every raid everyone was everyone's comrade, and what everyone shared
  bound one big group. Groups now form, take people in and keep them by what binds some of the colony more than the rest
  (`GroupMath.Distinct`: each pair's pull less three quarters of the colony's mean pull, `GroupFacts.distinct`); how close
  a group already is still reads the plain pull, so friends stay close while those who only fought beside them find
  their own way. A group holds well under half the colony (`GroupMath.MaxFor`: 45%, three to seven; five in a colony of
  twelve): a group grown past it lets its loosest member go, one a day at most, and merges stop there. Someone outside
  every group is shown as outside, with why (`GroupMath.WhyOutside`, `GroupText.OutsideWhy`: at odds with some who are
  in one, new here, or their own way): under their name on the People table, on the Bonds page ("Outside every group:
  Barr (at odds with Toni, Penguin and Macy)"), and on their character page. An older save's groups regroup by these rules
  over ten days (`GroupMath.RegroupDays`, `GroupTracker.regroup`), quietly at the usual pace, told once in the news; a
  new game's groups form as ever. The names stay as they were given (named for what they share, 0.101.2).
- **Identity.** The kind is what binds most of it (`GroupKindDef`: 26, lowest order wins, friendship last); it gives a
  name picked once, in the colony's era, and kept. A group is never named after one of its people (the colony is the
  family, and its groups are of the colony, not of a person: the player's report of "Stace's friends", 5 November
  2026). A friendship group is named for what its people share (`GroupShareDef`, `Rimfolk_GroupShares.xml`, ranked by
  the pure `GroupNaming`): the night hours or a work more than half of them keep or do, a side of character two in
  three have (only sides no kind is named for), else its habits in its own order with their place ("the fireside
  circle", "the long table"), else their friendship ("the old friends"); several names per idea and era, one picked
  by the group's id among those no group now or lately has, and its card's line says what it's named for ("They are
  friends, and most of them keep the night hours."). A kind whose own names are all taken does the same (it was
  "Hector's hunters"). The anchor is a role on the card, never the name. Older saves: a group whose name reads as one
  after someone (the friends kind's old names, kept in its def only to find them, `CC_Group_NameOf`, `CC_Clique_Name`;
  `GroupNaming.AfterSomeone`, never a name a kind or share gives) is named this way once when the game is loaded
  (`GroupTracker.LoadedGame`, and the day's pass if groups were off then), quietly: no news, no history line, nothing
  else changed. The older circles (social life off) take a friendship name the same way, stable while their leader
  leads them. Roles come from character and behaviour (`GroupMath.AssignRoles`:
  one anchor, joker and peacemaker each; a change waits three days; the incumbent keeps a unique role against a near
  rival). Ways (`GroupNormDef`, at most three) come from the members' average character or tags, with hysteresis; views
  from members' stances (`Persona.Stance`, any topic key). Members drift towards both (`Persona.Nudge`, `SetStance`).
- **Life.** Cohesion (0-100) settles towards the members' mean affinity and moves with habits kept, shared memories,
  quarrels and mends (read from `MomentTones`), losses and separation. Habits (`GroupHabitDef`) are kept when members are
  really together doing them (hourly, `Together.Can`), or called on the map as a play-out by the anchor: a close group
  only (cohesion `GroupMath.CloseAt` 50, the card's "close"; until 5 November 2026 a loose one from 35 was called too,
  more often than the words said), `CallAfterDays` 8 days at least since the last and then 1 in 5 a day (about 12 days
  on average, `DaysBetweenCalls`: every week or two), one group at a time, three of them at home at least, its first
  habit with a play kind, for its own people only. The gathering says what it is where the player meets it
  (`GroupHabits.BeginText`, `Does`, `Tip`; `Rimfolk_Groups_Gatherings.xml`): as it begins, who gathers where (their
  anchor by name when it's them leading), the group's line and "those who come feel they belong, and their strain eases
  a little"; in This week's list, its people and how often; at its end (`play.outcome`), how many of them came and that
  the group is closer for it; the founding news says once what keeping habits does and whom the anchor calls to what;
  a habit's pill on the card says what keeping it does and whether the anchor calls them to it. The label on the map
  stays short ("an evening at the fire for the fireside circle"). The words are the same in every age; the age is in
  the names. Newcomers
  integrate by opinion, memories, work, a sponsor and welcomes, scaled by how open the group is; a closed group gives the
  cold shoulder and the old left-out feeling. Rivalry between groups grows from feuds, opposed views and ways, and spats,
  and reaches votes, petitions and objections through `SocialWeb.Warmth`. Splits (loose and riven), merges (overlapping
  or close) and ends are told once.
- **Contracts.** Other parts read groups only through tags: `group:<id>`, `grouprole:<role>`, `ingroup`, plus
  `outsider`, `groupkind:`, `groupnorm:`, `grouprival` (`Groups.AddTags`, one line in `PeopleTags`). `SocialWeb.cliques`
  is built from the groups, so older code that asks about circles still works. The pure arithmetic is `GroupMath`,
  covered by `Tools/Tests/GroupTests.cs`.
- **Balance.** Belonging is company, not mood (`RewardInstead` on the habit, welcome and role moments); what a group does
  against someone is small and short (-1 to -3).

### Reputation and gossip

What people did, who knows it, how the story spreads, and the name that follows (`Social/Gossip/`; pure arithmetic in
`RepMath.cs`, checked by `Tools/Tests/ReputationTests.cs`).

- **Deeds** (`DeedDef`, 44): facets per side of a name (courage, kindness, diligence, skill, discretion, temper), a
  theme for lines, a fade half-life, merging of repeats. Recorded where the event is already noticed (one-line calls
  in WorldReactions.Victory, Moments' contests, Secrets.Reveal, Offices, Patch_ArcMade, Ambitions, Encounters,
  MeetingAttendance), by four light postfixes (a memory gained, a mental break, a history event, a social fight; one
  dictionary look each) and by the reputation's own hour (rescues under fire, the brave's company, idling, tending).
  Each keeps doer, victim, when, a name ("the raid on the 5th of Aprimay"), size, and its knowers: witnesses, and those who
  heard, from whom, in what version, believed how far. The newer systems' deeds (13: a camp friend or quarrel, a lost
  child found, a pilgrim walked home, a harvest worked, neighbours let down, a call answered, a day seen to, a feast
  dish burned, a quarrel at the feast, the night watch, the dead carried out, rounds) come through
  `Social/Talk/SharedTimes.cs` and `Setbacks/SetbackTimes.cs`, from the same one-line hooks as their memories.
- **Gossip** is an InteractionDef in the game's picker (`InteractionWorker_Gossip`, separate from the council's
  `CouncilGossip`). Weight: there's a deed the teller knows and the listener doesn't, by someone else (or the proud
  teller's own good one), its subject absent unless the teller is a hothead or their rival; fresher, juicier, more
  from the talkative, much more to newcomers. Slant from the teller's opinion, ties, group and humour; a version grows
  or shrinks within 40%-160% of the truth and never turns round; belief from the listener's trust and opinions. Lines
  via `Speech.Say` (pools by valence and slant, tagged by deed and persona); the log line from slant and topic packs.
- **A name** is what each person makes of another (deeds known × version × belief × fade × their character's weight),
  capped at ±40 a side, and the colony's view is that summed and shared over everyone. It gives a small social thought
  (-12..+7, the bad sooner), rep: tags, moments that read them, a vote lean (±2), fewer secrets told to a gossip, an
  epithet's bar 7-15% lower, strain eased beside the brave in a fight (through `Interplay.Ease`), and news when a side
  forms or fades.

### Conversations, views and shared memories

What colonists talk about, the views they hold, and what they went through together (`Social/Talk/`; the arithmetic in
`TalkMath.cs` and the lines' queue in `ExchangeQueue.cs`, checked by `Tools/Tests/TalkTests.cs`).

- **Topics** (`TopicDef`, 24, a worker each) say when they come up, what's news (`TalkNote`s from the game's events and
  the talk's hour, and the game's state: the food, a disaster, traders; and in `TalkScene`, found once an hour: a
  calendar day from three days before to two after it was kept, the newest world situation, the camp on the map), who
  or what a talk is about, and what a
  colonist's life adds to a view. A view (-1..1) is kept with `Persona.SetStance` under the topic's defName
  (`defName:id` for a newcomer, let go once they're settled). It's seeded from the topic's weights per side of a person,
  its leans (traits, passions, memes, precepts) and that life, drifts 4% a day back toward it, is pushed by events (a
  fight, a death) and moved by talk (`TalkMath.Pull`, at most a quarter of the way in one talk).
- **Children** talk only of growing up (`TopicDef.forChildren`, `TopicWorker_Growing`): a pair with a child
  (`Talk.EligibleChild`) weighs only the topics for children, at half the pair's budget (`Talk.ChildShare`), and a pair
  of grown-ups never weighs them. Their lines say who speaks ([child], [!child]; a test checks every line does).
- **Not about everything**: a topic's worker can rule out teasing for the moment (`TopicWorker.NoTease`: a day of
  mourning on the calendar, as the dead always).
- **In the game's picker.** One InteractionDef per topic, and one each for remembering, an in-joke and comfort
  (`InteractionWorker_Talk`). They share one budget, 0.08 next to chitchat, times the moments frequency, the pair
  (friends, rivals, dislike) and `Heart.MomentWeightFactor` (which carries the persona's talkativeness, counted once),
  split by salience (`TalkMath.Shares`): more to talk about means livelier talk, not more of it. Worked out once per
  pair and tick. A starter waits 4 hours, a pair 10, a topic between the same two is rarer for two days (not saved).
  `ModOverlapDef` lowers it to 0.4 with RimTalk or Speak Up.
- **How it goes** (`TalkMath.Outcome`): a joker who likes the other teases (never someone low, never about the dead);
  close views agree; otherwise heat (the gap, both tempers against warmth, liking and humour, ties, a sore spat from
  `MomentTones`) decides a debate or an argument, and with frictions off nobody argues. Effects come in `Interacted`:
  small social thoughts (opinion only; the warm ones count for the heart through `RewardInstead`), views moved,
  `MomentTones` warm, friction or mend, tie bumps, now and then a `Persona.Nudge` (arguing hardens temper, a long
  friendship warms, jokes lighten), and a quarrel made up is remembered.
- **Lines** (`TalkLinesDef`: a topic or memory, a turn, outcomes, toned lines with tags): the turn's own pools (twice as
  likely) and the generic ones together, filtered by the moment (both sides, the outcome, their tie, the topic's news,
  the other's group and name read from tags only), in the speaker's voice (`TalkMath.Voice`: their tone, sharpened by
  how it goes), not a line said lately. Two to four lines, said through `Speech.Say` by `TalkTracker` (every 15 ticks,
  only while a talk is under way), dropped as soon as `Together.Can` fails. The game's own log entry for the
  interaction is the one summary (with a sentence on how it went, from a rule pack): a second one through
  `Speech.Logged` would only repeat it, and show twice with Interaction Bubbles.
- **Shared memories** (`SharedMemoryDef`, 17 here, 3 more from groups) come from one-line hooks where the colony
  already notices things (a fight over in SocialWeb, a death, a wedding, a birth, a caravan home, a breakdown), two small
  postfixes of its own (`Frame.CompleteConstruction`, `TendUtility.DoTend`), the talk's hour (night shifts, disasters
  lifting, gatherings ended, fires beaten out, food poisoning, a tame gone wrong from the play log; mentors and secrets
  daily) and a social event's effect (`DilemmaEffect_SharedMemory`). A mishap only becomes a joke an hour later, with
  everyone in it up and nobody of the colony dead meanwhile. A fight is remembered only by those seen side by side in
  it (sampled every 250 ticks while it's on, `TalkMath.BattlePairs`: three partners each, one memory a pair a day), and a
  pair keeps three of a kind at most, so a season of raids never pushes out a funeral, a quarrel made up or a joke. A
  fight is said aloud without its date (`FightNames.Spoken`).
- **What the newer systems leave behind** (`SharedTimes`, 14 more memories): one line where each system already says a
  thing is over, and nothing that ticks of its own. `CampLife.Finish`: evenings at the camp's fire (warmth 2 or more),
  the healer's farewell round (those it looked in on), the smith's mending (those about the camp). `Missions.Finish`:
  the lost child (the finder with three at home), the pilgrim's road and a neighbour's harvest (the caravan, read from
  the ending's look target). `CalendarKeeping.KeepDay`: a day kept without a gathering; with one, `PlayOutTracker.Done`
  knows it by name (`SharedTimes.DayOf`: a calendar day kept this year whose label the gathering carries) and gives
  it to those who came. `PlayOutTracker.Done` also gives the pyre (a haul), the night watch (a patrolling vigil) and
  lantern night (a gathering that ends while `OnMapTimed` keeps lanterns on that map). `WorldSituations.End`: the
  colony's part, when it counted. Labels are made unique enough that `AddOnce` never blocks a later one (a name, a
  date, a year). Two mishaps at a day's gathering (rain on it, a burned dish) wait half a day in an unsaved list,
  looked at in the setbacks' hour, before they become jokes.
- **Jokes passed on** (`JokesRetold`, `InteractionWorker_JokeRetold`): an InteractionDef in the game's picker, weight
  0.04 (x1.6 for a joker, x0.3 for the dour) while the teller has an in-joke with someone else, recalled once and not
  worn, from the last 60 days, that the listener isn't in on (by its label), both at opinion 10 or more; a teller once
  a day, a pair once a week, picks cached per pair for `Sched.RareTicks` (not saved). The teller and the friend share
  a `Rimfolk_Mem_JokeHeard` memory under the joke's own label, which is never passed on again.
- **The character page**: views (section 30, with what each rests on and the last talk that moved it) and shared
  memories (40).

### New moments of life together (the player's request, 6 November 2026)

"Do another big content update to existing mechanics": this part, what happens between colonists, in the existing
frameworks at their existing rates. Every piece starts from something on the map and takes a share of a draw that
doesn't read the defs, so none of them makes the colony busier; none stages a scene, hurts anyone or is saved. The pure
rules are `Social/TogetherFourMath.cs` (checked in `Tools/Tests/TogetherFourTests.cs`).

- **Social events** (`Rimfolk_SocialEvents_Together.xml`, 25; their rewards `Rimfolk_Thoughts_Together.xml`, mostly a
  few points of opinion and belonging instead of mood). New conditions on `SocialEventDef`, read where an event is cast:
  `bMemoryAboutA` (B's memory, `bMemories`, is of A: the game's `HadAngeringFight` after their fight, `Council_SavedMe`
  for the one who came for them), `sameWork` (both at the same work right now, side by side, of the listed work types;
  `{WORK}` its gerund, its first skill the event's for a lesson; `TogetherFourMath.SameWork`), `bMadeFineDays` (B
  finished something excellent or better within the days, `{THING}`: `Patch_ArcMade` tells `SocialEvents.Notify_Made`,
  kept unsaved by pawn) and `bHurtDays` (a hurt of everyday life within the days, `Harm.DaysSince`, with a wound that
  still shows). Role tags of the moment (the weather, the hour, at work or at the table, a newcomer:
  `TogetherFourMath.NeedsMoment`) are now read from the moment (`PeopleTags.MatchesNow`, `Flavor.TagsFor`) in
  `DilemmaRole.Extras`: before, the mud's in-joke (`Rimfolk_Soc_Mud`, roleA "rain|snow") fitted nobody, since who
  someone is has no weather. Texts take the age's form ([tribal], [medieval], [industrial], [spacer], [lowtech]), with
  untagged ones for any age.
- **Shared memories** (`Rimfolk_SharedMemories_Together.xml`, 6; lines `Rimfolk_Talk_Lines_Together.xml`), from one
  line each where the colony already says the thing happened (`Social/Talk/SharedTimesTogether.cs`): an illness nursed
  (`HealthTracker.PulledThrough`: the doctor who tended it twice or more, Rimfolk's serious illnesses and the game's
  own), a close call (`MapDangers.Wound`: the one hurt and whoever is in talking range, `Together.Company`, by the
  danger's name `CC_Times_Close_*`), a colony founded (`Foundings.Founded`: the party, each with the three closest), a
  debt remembered and two in-jokes (the downpour, the snowball fight) from social events.
- **The sick and the hurt** (`Rimfolk_Topic_Health`, `TopicWorker_Health`, its lines `Rimfolk_Talk_Lines_Health.xml`):
  comes up only while someone of the colony on the map is ill or healing from a hurt (looked up hourly a map), about the
  worst off when it isn't one of the two, more while something that can kill goes round (then nobody teases). A topic
  takes a share of the talk's one budget (`TalkMath.Shares`); it steps back with RimTalk or Speak Up.
- **Group habits** (`Rimfolk_GroupHabits_Together.xml`): an hour with a book (`read`: the game's reading), keeping the
  sick company (`sickbed`: one of them abed and healing, the others on the game's visit to the sick), music of an
  evening (`music`: one playing an instrument, Royalty's, the others at leisure within earshot) and the night sky
  (outdoors, 20 to 1). None has a `playKind`, and each comes after a kind's own habits, so what an anchor calls the
  group to and the colony's gatherings are as they were; a group still keeps one habit a day at most. Each is something
  a friendship group may be named for (`GroupShareDef`). A kind of group, the wardens (`Rimfolk_Group_Wardens`, the
  Warden work), and its share.
- **Dreams** (`Rimfolk_Dreams_Together.xml`, 11) after the new moments' memories ({OTHER} the one it was with) and after
  a founding (`Rimfolk_Parted`, `Rimfolk_Uprooted`); three of them nightmares.
- **A personal story**, back to it (`Rimfolk_Arc_BackToIt`, `ArcWorker_BackToIt`): a close call within three days
  (`SharedTimesTogether.RecentCloseCall`, unsaved), the one closest to them; a shaken day, a talk between the two, 800
  experience at the work it happened at (`TogetherFourMath.CloseCallSkill`: mining, plants, crafting) or what they do
  best, then it's just work again. Its gates never pass on time alone, so what its words say happened did.

### Traits that change
The game's traits can grow, appear or fade at the end of a stretch of life the player can read (`Persona/TraitChanges.cs`,
`TraitChangeMath.cs`). Each rule is a `TraitChangeDef`: a trait, gain, loss or a step of one degree (through none, so a
nervous colonist steadies to no nerves trait before steadfast), and what it asks for: marks (defNames, families or
stories), sides moved from nature (lasting plus fading) and where they stand, deeds counted on separate occasions, the
game's records, a share of time from record snapshots every ten days, calm days, skills; guards for DLC, mods and tech.

- **Rare.** Checked in the council's day: when the colony has had no change for 10 days, every rule that holds for a
  colonist rolls its chance (3 to 15% a day) and one of those that come up happens. Nobody twice in 60 days; the same
  trait in a person waits its rule's gap (90 to 150 days). Only marks and deeds since that trait last changed count,
  so a stretch of life earns one change. Psychopath and cannibal need an extreme history and come once per colony.
- **Never touched.** Sexuality, the body and inborn gifts (`TraitChangeMath.Untouchable`), traits from genes, a
  backstory or the scenario, suppressed traits, an ideoligion role whose requirements speak of traits. The game's conflicts,
  exclusion tags, conflicting passions, backstory and gene rules hold; nobody gains past four traits.
- **Nothing doubles.** Nature is read from traits, so the shift a trait brings (worked out from nature before and after)
  is taken out of the person's moves, the fading part first, then the lasting part (`TraitChangeMath.Absorb`): every
  side stays put and the change is now held for good. A loss gives the shift back the same way.
- **Ways in.** The daily pass (one line in `CouncilDynamics.DailySecond`), the memory hook it shares with marks (one
  line), a postfix on `HistoryEventsManager.RecordEvent` for deeds with a doer, and the day's tales for executions.
- **Told.** A letter (good, bad or neither by rule) with the news, what it means and what led to it with dates, a
  journal line by tone, the chronicle, "Traits gained and lost" (page section 25) and a line in each side's tooltip
  for what went into a trait. Setting "Traits can change", under social life.

### The animals' natures (the player's picks, 7 November 2026)

The player chose three from a brainstorm: temperaments (A1), friendships, rivalries and grief among animals (A3, "do it
a way where i don't have any more pop ups for this appearing") and animals that speak (B, rare and behind a setting).
None of it sends a letter or a message: it shows on the map, on the animal's card (`Patch_AnimalNatureInspect`, one
line under its inspect text, rebuilt every 250 ticks at most) and in what the colonists say (`Speech.Say`, the shared
once-a-day rate of everything said on screen) and write in their journals. The rules are pure in
`Animals/AnimalNatureMath.cs` (tests: `AnimalNatureTests`).

- **Who.** Every named or bonded animal of the colony has a temperament; friendships and feuds are the companions'
  (bonded, or not livestock: a herd the herdmaster names would otherwise pair off in every barn); grief is any named
  or bonded animal's.
- **Temperaments** (`Animals/AnimalTempers.cs`): brave, timid, greedy, gentle, proud, mischievous, loyal, lazy, curious.
  Given once from the animal's id and its kind's lean (`AnimalNatureMath.Weights`: its family read from its words, a
  dog loyal, a cat proud, a muffalo gentle, a thrumbo gentle and proud; predators brave and proud; the big gentle, the
  small timid; the most teachable loyal; a Pokémon's types: fire brave and proud, ghost mischievous, psychic proud and
  curious...), then kept. Each moment is something the animal does, by the game's own jobs or two of Rimfolk's
  (`JobDriver_AnimalLinger`: walk to a cell and stay, standing, lying, or holding its ground; `JobDriver_AnimalKnockOver`):
  | Temperament | What it does | Every |
  |---|---|---|
  | brave | stands between its person and the nearest attacker and fights what comes at it there (the fight's look); out of a fight, between its person and a wild predator near them | 2 days, when there is one; once a fight |
  | timid | bolts from the shooting within 20 cells (the fight's look); shies away from a stranger within 8 | 2 days; once a fight |
  | greedy | eats a meal or 3 to 6 of what it eats from the stores or off a table, within 30 cells | 5 days |
  | gentle | the game's nuzzle for someone sick, hurt or low; lies down by a patient asleep in a sickbed | 3 days |
  | proud | follows its person close while they milk, tend or train another animal; stands before a visitor | 3 days |
  | mischievous | a grazer eats a crop in a growing zone; else noses a light stack off a shelf onto the floor beside it | 5 days |
  | loyal | waits at the door to the outside (or by the bed) while its person is away; the game's nuzzle on their homecoming | about twice a day while away |
  | lazy | naps (the game's lying down, asleep) by a fire or heater below 8°C, under a roof above 30°C, else in the sun | 3 days |
  | curious | follows a visitor about; watches someone building, mining, repairing or at a bench | 3 days |

  Days at the setting's 100% (`animalMoments`, How much happens, 0 to 200%, light touch 50%, story-rich 150%; the
  hour's chance `HourChance`, half again as likely in view), a gap of two fifths of that after each, one moment an
  hour on a map, none in danger; the fight's look (three in five each 250 ticks at 100%) once in eight hours each.
- **Friends and rivals** (`Animals/AnimalBonds.cs`): two companions asleep within 1.9 cells on three nights (a night
  counted once, the small hours with the evening before) are friends, two at most each; by day one follows the other
  a while (`FollowClose`) with a heart and a nuzzle in the log (`SocialLog.Add`, only when they are close and in
  sight), about every day and a half; at bedtime a tired one lies down beside its sleeping friend. Two of different
  kinds about each other by day (within 6 cells) roll once a day (`RivalChance`: one day in 250, ten times that for a
  cat and a dog, more for two hunters and for the proud, the greedy and the mischievous, less for the gentle; never
  friends or the same kind), one rival each; rivals in sight hiss, growl, snort or squawk at each other in the log
  (by voice), one flees and the other chases a moment, about every two days. Nobody is hurt (no `Harm` here). Not seen
  together 40 days, they drift apart.
- **Grief**: a bonded animal whose person died and lies in a grave on the map (the body's own time of death) goes to
  lie on the grave (`Rimfolk_AnimalVigil`) about every two and a half days for 60 days, about every eight until 120;
  whoever mourns the dead most (partner, kin, the warmest) may say so and write it, once in twelve days. A friend of
  an animal that died misses it 30 days (its card) and goes to its grave if it has one.
- **Old animals**: one of the colony's named or bonded animals that leaves alive (the pets' watch: sold, given,
  released, gone wild) is kept with the person it was last seen with; meeting them again on any map 2 to 120 days
  later, within 30 cells, it trots over and stays at their side a while with a heart, once in ten days; the handler
  says something, and writes the first meeting down.
- **Speaking animals** (`Animals/AnimalVoices.cs`), a fantasy part, the setting "Animals speak" (`speakingAnimals`:
  Auto, the default, on only while PokéWorld or A RimWorld of Magic is loaded or someone of the colony has a psylink,
  looked at hourly; On; Off). Who, softly by mod and by age: a psychic-type Pokémon (`PokeWorldLink.TypesOf`) to its
  trainer, in the mind; a druid's or a ranger's bonded animal (A RimWorld of Magic's class traits by name); a bonded
  animal of a psycaster of psylink level three or more (Royalty); Vanilla Genetics Expanded's humanoid hybrids, read by
  their comp's name (`CompProperties_HumanoidHybrid`: their own defs give them conversation with the colony; the
  manbear, manwolf, manchicken and the rest), aloud to their person or whoever is with them; in a tribe (the colony's
  age neolithic or below) or for a colonist whose faith has Ideology's animal personhood, a bonded animal is a spirit
  animal that speaks only in their dreams (`DreamAbout.SpiritAnimal`, `Council_Dream_SpiritAnimal`). What it does: a
  line in its own voice (`Council_Pool_AnimalVoice`, tagged by voice, temperament, gift and a Pokémon's types) to its
  person when the two are together and in view (a chance in five each hour, a day apart at least); a warning over its head of
  raiders in a fight or a predator about within 45 cells (an angry beast, one hunting, or one near its own size), with
  the compass word for where, once a day for the same danger and half a day apart for predators; at a council meeting
  its person sits in, it comes to the table at their heels and has one line, its person's side or its own, written
  with the meeting's remarks, and never votes; on half the council's questions about its person or itself, a word in
  the question's letter, said over its head too when its person is beside it.
- **Saved** (`AnimalBook`, a GameComponent: an older save gets an empty one): temperaments by id (300, let go 120
  days unseen; a PokéWorld evolution carries its own over with its ties), pairs and ties by id with names (80, pairs
  first to go), animals gone with their handler by reference (30, forgotten after 120 days or when the handler dies).
  What it did lately, the waits, the homecomings watched and the animals let speak by a dev action are not saved.
- **Ticking**: in the animals' hour and day (`AnimalLife`, the council's people pass) and, for the fight, on the fight
  sides' 250-tick look (`Sched.RareFightSides`): no new slot.

### The colony's animals hunting for themselves (the player's request, 7 November 2026)

The player's husky starved: the game never lets a tame animal hunt unless its kind is flagged a predator, and then only
as `JobGiver_GetFood`'s last resort (`FoodUtility.TryFindBestFoodSourceFor` falls to `BestPawnToHuntForPredator` when no
food at all is found, by the game's prey rules: combat power and size against the hunter's). A husky isn't flagged, can't
graze, and its area ("Animals") left out the stores. The player: "can you make animals hunt if they would do in reality,
and your judgement for fictive species, and that they hunt what they would normally would, or hunt things below them in
the food chain, not above". The pure rules are `Animals/HuntMath.cs` (`HuntTests`, which judge every animal of the base
game, the DLCs and the player's 29 animal mods from their own defs, `Tools/Tests/HuntKinds.txt`); the game's side is
`Animals/PetHunts.cs`, `PetHuntsPatches.cs` and `PetHuntsDebug.cs`.

- **Who hunts** (`HuntMath.Judge`), in this order: a dryad, a kind that eats nothing and one whose diet has no meat (it
  couldn't eat its kill) never; a Pokémon by its species in the table, else by its type, a legendary never; any other kind
  by its defName in the table, else by a word of its name (the last word first: a red fox is a fox, a sabertooth squirrel
  a squirrel), else by the rule. Each entry gives a step and a reason, and whether the kind hunts in packs, takes insects
  or takes venomous prey.
  | Who | Step | Why |
  |---|---|---|
  | dogs (husky, labrador, terrier, retriever, shepherd, corgi, beagle, the hounds, dingo, dhole...) | by size, in packs | they chase down and kill small game; the game doesn't flag them |
  | lapdogs (pug, French bulldog, shih-tzu, chihuahua...) | prey | bred for company, they barely hunt |
  | wolves, hyenas, coyotes, jackals, the African wild dog | by size, in packs | |
  | foxes, the thylacine, weasels, ferrets, martens, badgers, wolverines | by size | |
  | the mongoose | hunter | takes snakes too |
  | house cats (whatever their size: a Maine coon too) | small hunter | they keep the mice and birds down |
  | wild cats (lynx, caracal, serval, ocelot) | by size | |
  | big cats, smilodons; lions (in prides) | apex | |
  | bears (the panda aside) | apex | |
  | birds of prey (eagle, hawk, falcon, owl) | by size | carrion birds (vultures, condors) are prey |
  | snakes; crocodiles; monitors | by size; apex; by size | |
  | raptors (in packs) and the hunting dinosaurs | by size | |
  | fish eaters (penguins, seals, otters, herons), lizards and tortoises, apes and monkeys, rooters (pigs, boars, raccoons, rats), grazers, browsers, fowl, small grazers | prey | the game's flag on a mod's twist (a vampire deer) still stands over a prey word |
  | Megafauna's giant penguin and walrus kin; Alpha Animals' plasmorph (a snail) and frostmite (a scavenger) | prey | the game flags them, but they fished, dug or scavenged |
  | Megafauna's meganeura | small hunter | a giant dragonfly |
  | A RimWorld of Magic's minions, demons, elementals, sentinels and golems | prey | summoned or made: they don't feed themselves (over the game's flag) |

  **The rule** for every kind the table doesn't name (most of Alpha Animals, ReGrowth, Vanilla Genetics Expanded's hybrids,
  Odyssey's and any later mod's): no meat, no hunt; the game's predator flag, or a meat-only diet with teeth or claws, a
  hunter by its size (under 0.7 a small hunter, under 1.6 a hunter, else apex); an omnivore the game doesn't flag doesn't.
  **Pokémon** by species: the canines (Growlithe, Houndour, Poochyena, Rockruff, Lillipup, Electrike, Yamper, Riolu...,
  in packs), foxes (Vulpix, Zorua, Nickit, Fennekin), cats (Meowth, Glameow, Skitty, Purrloin, Litten... small hunters),
  lions (Shinx, Litleo, in prides), Sneasel and Weavile (packs), the mongooses (Zangoose, Yungoos: snakes too), bears,
  birds of prey (Pidgeotto, Fearow, Noctowl, Staraptor, Braviary, Talonflame, Decidueye, Corviknight...), snakes (Ekans,
  Seviper), crocodiles (Sandile, Totodile lines), the hunting dragons (Gible, Bagon, Deino, Axew, Trapinch lines,
  Druddigon), the fossil hunters (Tyrunt, Aerodactyl, Archen, Kabutops, Tyranitar), the hunting bugs (Scyther, Spinarak,
  Galvantula, Skorupi, Sizzlipede, Venipede lines), Pawniard's packs, Heatmor (it eats Durant); a Pokémon the table
  doesn't name hunts only when it is a Dark type of 0.4 or more or a Dragon type of 1 or more (by its size), and the Dark
  and Dragon kinds that don't hunt are named as prey (Dratini's line, Altaria, Goomy's line, the apple dragons, the
  fighters, the sea hunters, the tricksters, Stunky, the vultures). Over the player's game: 16 of the base game's and the
  DLCs' 71 animals hunt, 194 of the other mods' 575, and 119 of PokéWorld's 898 species.
- **What it takes** (`HuntMath.WhyNotPrey`): only a wild animal (no faction, no host), never a person, anyone's tame or
  penned animal, one of Rimfolk's stories (`PackAlphas.Storied`: a named alpha or its pack, a beast nemesis, also one that
  fled, the beast that stays, a legend, anything named), a sacred one, a legendary, one with a group (a herd crossing: a
  stampede waiting), one marked for taming, a manhunter or one in any mental state, one that bursts, splits or vanishes
  when it dies (a boomrat); never an insect or a venomous animal unless its kind's entry says so; only a lower step; no
  bigger than its limit (its size times 0.8, or 1.2 for two of a pack kind together; the game's own maxPreyBodySize where
  its kind has one; a small hunter 0.4 at most, a hunter 1.25, 1.6 in a pack; small game 0.4 for a fed one); and, unless
  the prey is down already, less of a fight than 0.6 of its own (the game's combat power times health times size; 1 in a
  pack) and unlikely to turn on it: the game's revenge chance against it in melee (three times the kind's, with PokéWorld's
  level factor) at most 0.1 for prey a third of its size or more, 0.35 for smaller. Then inside its allowed area, not
  forbidden, reachable, within 120 cells (30 for a fed one), the best by the game's own prey score (`GetPreyScoreFor`).
  For example: a husky takes hares, squirrels and foxes, never a deer alone (two huskies may take an ibex), never a wolf or
  a cobra; a cat takes rats, squirrels and hares, not a wild hen or a fox; a wolf alone takes hares and foxes, two wolves an
  ibex or a gazelle, never an elk or a caribou (its herd would turn); a grizzly takes a deer, never an elk alone or a wolf
  (wolves turn on what hurts them); an Arcanine takes a Rattata or a Pidgey but leaves a Growlithe (it would fight back) and
  a Houndoom (its equal); a Growlithe takes a hare but leaves a Rattata near its own size.
- **When** (`HuntMath.WhyNot`): hungry (past its kind's wish to eat, as the game's food search asks, within that node's
  bounds) with nothing the game's food search found inside its area: `Patch_PetHuntsHungry` (a postfix on
  `JobGiver_GetFood.TryGiveJob`) gives it the game's own `PredatorHunt` (kill, then eat) on the prey above, with one free
  of its pack kind beside it joining; the game's own hunt for a flagged predator is replaced by the same choice (none when
  the rules allow nothing), and food the game found always stands. An animal that looked and found nothing doesn't look
  again for 600 ticks. Fed, a hunter idle in its area sets out about once in four days (1 in 96 an hour, a day and a half
  apart at least, one a map an hour, the people's hour of `CouncilGameComponent`), for small game only; a third of the time,
  with its person up on the map and in reach, it carries the kill to them (`Rimfolk_PetBringsKill`,
  `JobDriver_PetBringsKill`: the game's chase and blows as `JobDriver_PredatorHunt`'s, then the body taken up, carried and
  set down at their feet; their thought "a present from my animal", +3 for 2 days, the pet's present event's). Never with
  the setting off or its "May hunt" off, young, away, downed, drafted, following its master into work or a fight (the game's
  `ShouldFollowMaster`), roped or penned, with a group, in a mental state, ill enough for bed, under 0.6 of its health,
  without an attack, or with danger on the map; a hunt under way is given up when one of those comes up, or when its hunter
  has strayed 6 cells or more outside its area (the wild's short pass, every 250 ticks, `BeastBook.Rare`: no new slot).
- **Harm.** The hunt is the game's own fight, so prey fighting back can hurt a pet now and then, as it does the game's wild
  hunters; the choice of prey (smaller, much weaker, unlikely to turn on it, never venomous) keeps that small. Rimfolk rolls
  nothing of its own here, so the shared budget (`Harm/HarmBudgetMath.cs`) doesn't count it, as it doesn't count the game's
  own predators, raids or the colony's hunters: none of it goes through `Harm`. A hunter hurt to under 0.6 of its health
  breaks off the chase.
- **Seen.** At the kill, someone in view says a word (`Council_Pool_SeeHunt`, through `AnimalTempers.Remark`: the shared
  once-a-day rate of things said on screen; tagged eat or carry, hungry or sport, small or big, pack, body, Pokémon), and the
  animal's card says what it caught; the carried kill gets its person's word to it (`Council_Pool_PetBringsKill`). No letter
  and no message. Fresh carcasses (`Alert_FreshCarcass`) leave a kill alone while a colony animal is eating it.
- **The alert** (`Alert_NoFoodInReach`, the game's alert list): one of the colony's animals hungry (Hungry or worse), not
  fed by someone in bed or downed, not penned or hitched (the game has its own alerts for those), not with a group, with
  nothing the game's food search finds in its area (reservations ignored) and no hunt to go on (its kind doesn't hunt, it
  is kept or young, the setting is off, it can't now, or there is nothing it may take in reach), on two looks in a row.
  It names each (twelve at most, then "and N more"), says why (food it eats lies outside its area; the food inside it is
  forbidden; there is nothing it eats on the map, with what it eats) and how to put it right (the area Unrestricted or
  wider; unforbid; a stockpile of what it eats inside its area). A click selects them. Its bonded person says so aloud
  once (`Council_Pool_SayPetNoFood`), within six hours while they are up on the map being looked at, not again within 12.
- **Control.** The setting "Pets hunt for themselves" (`petsHunt`, What's on, on by default; off, the game's own rules
  stand untouched), "An animal with no food in reach" (`noFoodAlert`, Alerts, on), and on each animal whose kind hunts a
  "May hunt" button (`Patch_PetHuntsGizmo`, a `Command_Toggle` on `Pawn.GetGizmos`, the game's hunt icon). Off, it never
  hunts, the game's own last-resort hunt included.
- **Saved**: only the ids kept home (`AnimalBook.keptHome`, 200 at most, let go when the animal dies or leaves). The hunts
  under way, the waits and the alert's looks are not: a hunt running at a save goes on as the game's own job.
- **Softly.** Every kind by its def and words, nothing required; PokéWorld by its species and types
  (`PokeWorldLink.TypesOf`, `PokeWorldLegends`) and its own revenge patch; Hunt for Me's trained hunts for the colony are
  its own jobs and stay its own (this is the animal feeding itself); a mod that gives a hungry animal another food job
  (foraging, fishing) wins, since food found always stands.

### What the colony's people want, strive for and are known for: the fourth round (6 November 2026)

The player's request ("do another big content update to existing mechanics"), the part about the colony's own people:
new content in the existing frameworks, nothing that asks more often. Pure rules in `Lives/PeopleFourMath.cs` and
`GoalMath` (`PeopleFourTests`); the defs in the People4 files of `Defs/AspirationDefs`, `WishDefs`, `QuirkDefs`,
`EpithetDefs`, `DeedDefs`, with `Defs/Misc/Rimfolk_Records_People4.xml`.

- **Two sparks for life goals** (`Aspirations/AspirationWorkers_People4.cs`, `GoalSparks`): `hurt`, a hurt from everyday
  life lately (a danger, a work hurt, an ailment: the harm service's book, `Harm.DaysSince`), and `war`, a war among the
  neighbours running now (a bad world event of the war kind). A war is a spark of fighting (`GoalMath.FightSpark`): it
  pulls 0.6 like a foe and is held to the one-at-a-time rule, so a war never sets two on one fighting goal. Neither is
  big enough to change a goal held. No new goal is sparked by a fight.
- **Twelve life goals**: a nurse's hands (Medicine to 8 for anyone; an illness pulled through, a hurt, a loss), sure on
  their feet again (thirty days without another hurt or fall for someone hurt in the last ten, the clock starting again
  at each: `AspirationWorker_SureFooted`), keep the colony mended (forty repairs, builders), a trader's name (six deals,
  the sociable), a friend of the road (five evenings at camps' fires), a good neighbor (two calls answered; a war, a good
  turn on the road), keep the colony's days (two kept; a new colony's first night, the founding day, a loss), a
  peacemaker (three quarrels mended; a fight of their own, a loss), lead the rites (three, Ideology), see the new colony
  grow (its own people to eight, two more at least: `AspirationWorker_HomeGrows`), the journey to the other colony (stand
  on another of the colony's homes, someone close living there: `AspirationWorker_Visit`), ready for what comes (Shooting
  to 8 for anyone who can fight, while the neighbours are at war). Goals on Rimfolk's own records come only while their
  feature is on (`AspirationWorker_FeatureRecord`, its `counter` naming it). Each takes the age's form in its lines (the
  old ones' leaves and roots, the village's bone-setter, the medicine stores, the scan; the bow, the crossbow, the rifle).
- **Seven wishes**: cloth put by for mending (80 textiles of any kind, crafters, fall and winter), steel put by (150,
  builders and crafters), spare parts for the machines (6 components, industrial and spacer), milk for the little one
  (20 milk or baby food, parents of a baby), shelves for the stores (two), and two with workers of their own that play
  out when promised (`Wishes/WishWorkers_People4.cs`): the trees by the walls felled (three grown trees or more in the
  home area beside the colony's walls or doors, marked for the game's cutting; never for one whose faith minds it) and
  the snow cleared from the doors (deep snow on twelve cells or more within two steps of the doors, put in the game's
  snow clearing area). No triggers: they take a share of the colony's wish roll, which comes as often as ever.
- **Eight quirks**, two new triggers on `QuirkDef`: `afterHurt` (a hurt from everyday life within its days, once up
  again) and `whileIll` (ill with one of the named hediffs). Watches every step (a hurt), taps the roof before digging
  (a hurt, miners only, +1 at the face), dries their boots by every fire (trench foot, +1 by a fire), boils everything
  they drink (a serious illness pulled through), keeps an ember of the first fire (a new colony's first night, +1 by a
  fire), looks back down the road (a parting or being sent away, -1 late at night), sings a song from the strangers'
  fire (a camp's evening, +1 outdoors), indoors by dusk (sandfly fever, -2 outside after dark). Each shows over a head
  when it fits (`HabitBubbles`, the `Rimfolk_Pool_Bubble` pools). Small chances in rare moments: they press less than a
  tenth of what a fight already brings, under the cap of two.
- **Eight names and six deeds.** Six deeds the colony talks about count a record each (`PeopleFourMath.DeedRecord`, one
  more each time the deed is recorded, merged or not): people carried out of a fight (the Shield, from 3), night watches (Night-Watch, 4), contests won (the
  Champion, 4), masterworks (Master-Hand, 3), rounds of the sick (the Lamp, 4), kindnesses to strangers (Open-Hand, 4).
  Students taught to stand alone count where a mentorship ends (the Teacher, 2), votes lost already did (the Hopeful,
  4, dubious, the Unbeaten's other side). The words are ones every age has. They follow the one-name rules unchanged
  (NameMath: twenty days held, a clear surpasser over a week). New deeds met where the game notices them: letting the
  prisoners out, a binge on the colony's drugs, a body dug up, running off into the wild (the game's own breaks). A wedding and a proposal
  turned down were left out at the merge: a deed moves how the colony sees someone, and family life carries no mechanics.
- **Rates** (`PeopleFourTests`'s model of the real pool of grown-ups' goals, twelve colonists over four years of sixty
  days, the tracker's rules and the same seeds before and after): goals started 0.94 a colonist-year before and 0.96
  after, from fights 8.4% and 6.4%, at most one on a fighting goal from fights or a war at once. The wish roll and its
  pace are untouched (`Tools/Balance` before and after in the handoff).

### Ideoligions in the colony: the people
With Ideology, the game's ideoligions and how firmly colonists hold them (the game's certainty, 0..1) come into their
life (`Faith/`; the arithmetic in `FaithMath.cs` and `FaithPeopleMath.cs`, checked by `Tools/Tests/FaithTests.cs` and
`FaithPeopleTests.cs`). Player-facing text uses the game's own words, ideoligion and certainty, so nobody mistakes this
for a belief system of Rimfolk's own; code, keys, defNames, tags and saved fields keep their older "faith" names.
Everything moves certainty through the core (`Faiths.Sway`, `SwayToward`, `Shake`, `Steady`), so a conversion is
always the game's own `IdeoConversionAttempt` and is told one way. Off without Ideology, in classic mode or with
"Ideoligions in the colony" off; the talks also need the talk's settings, the crises social life.

- **The ideoligions page** (People → Ideoligions, `UI/FaithsPanel.cs`, a page only while `Faiths.On`): a card per
  ideoligion of the census (most believers first) in its colour and icon, with its memes; believers with a certainty
  bar marked devout (0.85) or wavering (under 0.6); leader and moral guide from the ideoligion's roles, or empty here,
  held elsewhere or not in the ideoligion; what it asks that weighs on someone now (`FaithAsks`: a believer's mood
  thought groups whose `sourcePrecept` is of their ideoligion, or whose def a precept's `PreceptComp_Thought` gives,
  half a point off or more); who could sway whom (each devout believer against everyone of another ideoligion, by
  `FaithPeople.Expected`: the core's sway for a friendly debate and the game's certainty-loss factor, from 2.5%); then
  the game's own tools. Built once a second, the game's stats read once per build. `FaithsPanel.Buttons` is the hook
  for the council's own buttons (per ideoligion, or for the colony), empty until something adds to it.
- **Talks that move certainty** (`FaithTalk.cs`, two lines in `Talk.Run` and one in the faith topic's tags): planned
  once the talk's outcome is known and before its lines, applied after its own effects. Across ideoligions: agreeing
  (base 0.035) or a friendly debate (0.045: one of them likes the other at 15 or more, or friends or lovers) lets the
  devout one who comes out of it well sway the other (in a debate by persuasion: conversion power, twice for the
  devout, how liked, with a roll; only a devout winner sways); an argument makes both dig in (+0.01). Two of one
  ideoligion steady each other (+0.01). One move a pair in two days (not saved), a sway once a day per listener and a
  steadying once a day (saved). A listener whose ideoligion frowns on preaching (`Faiths.DislikesPreaching`, the core
  halves the sway) gets `Rimfolk_Faith_PreachedAt` about the preacher (-5 opinion, 4 days, no mood, with a hint). The
  lines' tags say it: persuading, persuaded (expected 0.03 or more) or unmoved, preachedat, digin, steadied,
  otherfaith:devout and :wavering, myfaith or theirfaith for whose ideoligion {SUBJECT} is, nopreaching. The talk
  topic itself stays "faith": it comes up without Ideology too, where the game has no ideoligions.
- **Views beside certainty.** A move of certainty moves the faith view twice over, then `ViewWithCertainty` keeps it
  where it can't read against certainty (wavering: never "holds to the faith"; devout: never "doubts"), daily too.
- **Crises of faith** (`FaithCrisis.cs`, 5 marks in 4 families with a `FaithShake` extension: base shake, a pull toward
  the other's ideoligion, what the pages say caused it). Ways in are one line each in `Persona/Marks.cs`: after any mark
  (`Give`), a loss mark (LostLove, LostChild, LostKin, LostFriend) about someone dead, for an ideoligion with a funeral
  or a prayer, waits 1 to 3 days; a rescue (SavedBy, NursedBack) by someone of another ideoligion waits 0.4 to 1.5; the
  hourly look at the downed notes a brush with death (blood loss 0.45, a lethal hediff three quarters of the way,
  bleeding out within 8 hours) to be felt once they're up; the daily look follows the worst demand of their own
  ideoligion and counts the days it stays (5, at -4 or worse). A crisis then is the family's mark through
  `PersonaMarks.Give` (its chance, repeat window and the marks' own limits), never within 20 days of the last; the
  shake is the base times the mark's lean, 0.1 to 0.3; a rescue also sways toward the rescuer's ideoligion (0.05
  before openness, pride, opinion). Waiting ones are let go after 6 days. A crisis never converts by itself; the game's
  drift by mood rebuilds certainty, nothing more is added.
- **Shown**: the character page's Ideoligion section (35: ideoligion and certainty with the game's daily change, a
  role, what it asks, the last three moments that moved it with how far each moved them, who is likeliest to sway them
  or whom they could bring round), the mark under "what shaped them", news, the journal; the advisor's
  `Council_Adv_FaithSplit` (split 0.5 or more with three ideoligions or more, leading to the page); the Almanac page
  "ideoligions in the colony".
### Ideoligions and the council
With Ideology, the council can work with the colony's ideoligions (`Faith/FaithEvenings.cs`, `Faith/FaithCouncilMath.cs`,
`Dilemmas/DilemmaWorkers_Faith.cs`, `Decrees/DecreeFaith.cs`, `Wishes/WishWorkers_Faith.cs`). Everything moves
certainty through the shared core (`Faiths.Sway`, `SwayToward`, `Steady`), so the listener's openness, pride and opinion
of the speaker always count, no single moment takes more than 15%, and a conversion is the game's own. Off without
Ideology, in classic mode and with "Ideoligions in the colony" off. It costs trust and certainty, never a bad mood: a
colony of many ideoligions is a lever the player can pull, not one more drain.

- **An ideoligion evening** is a gathering (a play-out, `PlayKind.Gathering` with words) for one ideoligion, 17:00 to
  23:00, about two hours. The speaker is the ideoligion's moral guide at home, else its most devout believer with the
  best Social skill; the place its altar or ideogram (styled for it, or for the colony's own ideoligion any unclaimed
  one: a soft check by name, so other mods' altars count), else a fire, the dining table, a gather spot. Who comes is
  decided as it's called (`FaithCouncilMath.ComeChance`): believers always, an ideoligion that frowns on preaching
  never (they say so where they are), the rest by openness, opinion of the speaker, wavering or devout. It is judged
  when the gathering ends (`Interplay.Notify_GatheringOver` hands every gathering to
  `FaithEvenings.Notify_GatheringOver`): Social skill up to 0.55, warmth 0.15 either way, listeners up to 0.2 at six,
  the altar 0.1, halved when broken off. Poor (under 0.3) does nothing but a line; good sways listeners of other
  ideoligions from 6% (to 9%) and steadies believers, with recreation and fellowship instead of mood (RewardInstead);
  fine (0.65 or more) sways up to 12% and gives a small mood (+3, a day and a half) through the shared budget. Its
  outcome becomes the gathering's closing words (`PlayOut.outcome`). One every five days, an ideoligion every ten;
  offered and nobody coming spends the ideoligion's turn, so the dice can't be rolled again at once.
- **Called from:** a calendar day kept as an ideoligion evening (`DayWay.Faith`, the ideoligion by name in the day's
  subject; it ignores the gaps, a yearly day), the devout's council question (`DilemmaWorker_FaithEvening`), and
  `FaithEvenings.CanCall(Map, out why)`, `CanCall(Map, Ideo, out why)` and `Call(Map, Ideo)` for any page.
- **The wish** (`WishWorker_LearnFaith`): wavering (under 60%), a devout believer of another ideoligion thought well of
  (opinion 20+), never for an ideoligion that frowns on preaching. Promised, a play-out visit that evening
  (re-arranged if it falls through while promised; without play-outs, `Together.When`); an evening of that ideoligion
  counts too. The grant's effect (`WishEffect_LearnFaith`) sways 12% base (none more if an evening already did it) and
  adds a shared memory.
- **Ideoligions at odds** (`DilemmaWorker_FaithClash`): two ideoligions of two or more, or a split of a half; half the
  time unless believers of different ideoligions quarrelled in the last five days or an altar stands. Share the altar
  (+2 trust for the open-minded, -1 for the devout), a corner each (+1 trust, 3% certainty, a letter suggesting
  places), or name the largest ideoligion (+3 for its believers, -2 and a 3% sway for the rest, -4 more for the devout
  of other ideoligions who take it hard). Each plays out (`DilemmaEffect_FaithWords`, a work party). Ideoligions are
  kept by name in the letter's context, so an archived letter never references an ideoligion the game has let go.
- **One ideoligion for the colony** (a DecreeDef with `oneFaith`): the player picks the ideoligion from a menu on the
  enact button (the largest first). Each morning (`DecreeWork.Daily`): the wavering of other ideoligions `SwayToward`
  it 1.2% to 2% (opinion of its believers, a little by openness), and those of other ideoligions lose 0.25 to 1 trust
  (logged on the first morning and every fifth). Reactions narrow by ideoligion (`decreeFaith`, `notDecreeFaith`,
  `dislikesPreaching` on a reaction rule), so the devout of other ideoligions hate it and the decree system's own
  petition to lift it comes from them. Meetings open with a word from a believer (`DecreeFaith.MeetingWord`, once a
  meeting). No mood of its own. The talks read `DecreeFaith.FaithOfColony` and `TalkFactor` (1.5). Lifted or run out,
  the named ideoligion is let go.
- **Saved** in `FaithCouncilTracker` (a game component that never ticks): the last evening and each ideoligion's, the
  evening waiting, the decree's ideoligion, and who heard which ideoligion lately (one each, 30 days, 40 at most).

## Real effects: answers that play out on the map

A dilemma's answer, an event's answer or a calendar day used to give moods and a message and nothing else: "a victory
feast" was a mood, and nobody sat down to eat. With **Answers play out on the map** (a setting on the Events & world
page, on by default), an answer that describes something people do sets it going on the map, and the good moods it
gives those taking part come once it's over (PlayOut/).

- **Data, not code per answer.** Real effects are `DilemmaEffect`s like any other, listed with an answer's effects in
  XML; the order doesn't matter, because `PlayOuts.Ordered` applies them first. `DilemmaEffect_Gathering` (a party:
  the rec room, the dining table, a gather spot, the open air, the water's edge for a swim, the fields, a grave, the
  camp's fire, around a stranger), `_Feast` (the dining table, and a cooked meal from the
  stores for everyone who comes, the finest first: `Ingest` jobs on real meals, so the food is eaten), `_Bonfire` (the
  colony's campfire, or one laid for the evening in the open from 20 of its wood, or from wood the answer already took
  with `woodPaid`; it burns down to ash afterwards), `_Toast` and `_Words` (an hour round the table: a toast, an
  apology, a reading, a story told), `_Visit` (one colonist keeps another company, wherever they go and at their
  bedside if they're in bed, or sits by a grave, walks over to the camp's fire, or goes for a long walk; a visit to a
  place on its own, the water or the fields, may name others for when that one can't be had, `orTo`), `_Lesson`
  (a student follows a teacher about their work and learns their skill by watching), `_Rest` (the rest of today's
  work hours become recreation, or sleep, through the calendar's rest-day timetable, restored tomorrow) and `_Hunt`
  (game marked for the hunters). Six more are groups that go out and do something (below): `_WorkParty`,
  `_Procession` (a walk together), `_Vigil`, `_Search`, `_CarryOut` (a gift or a payment carried out) and `_HuntParty`.
  Each takes a label ("the victory feast", with the answer's placeholders), the hours
  it may start in (an evening, past midnight), how long it may wait and how long it lasts.
- **The gathering** is the game's own party (`LordToil_Party` and the `Party` duty) under a lord job of Rimfolk's
  (`LordJob_PlayOut`), started with its organizer and joined by those it was for as they come free, the way party
  guests come; the council meeting's rules for when people can sit down together and what work can't be dropped are
  reused. A feast serves a plate to each guest every few seconds of game time until everyone who came has had one; a
  meal hook on `Thing.Ingested` (the deeper hooks) counts who ate. Notes over heads for a song and a few lines said (flavor pools
  `Council_Pool_Play*`, and the answer's own) are only drawn on the map being looked at.
- **Groups that go out** (`PlayGroups`, `PlayWork`, `PlayCarry`, `PlayOutGroupJobs`, `PlayWords`) never use a lord,
  so the game's parties, rituals, weddings and the council meeting can always take their people: each member gets a
  job of their own, and the tracker's 250-tick step looks after the group. A **work party** runs the game's own work
  givers (found by class: filth, hauling, repair, blueprints and frames, the dead, snow, sowing, harvest, plants to
  cut), only for real work in the home area or near the answer's spot, nearest first: the forty nearest that could hold
  the work at all (a cell without its crop for the sowing, a ripe plant for the harvest, an animal ready for the shears or
  the pail: `PlayWork.MayNeed`), asked the game's own question; only the forty nearest cells were asked before, so a
  field sown near the spot hid those still waiting for seed (the planting day's first row in the player's save). A sowing
  whose fields were sown already says so (`CC_Play_Why_Sown`); each job is followed by a small
  queued marker job (`Council_PlayOutWorkNext`) that hands out the next one, so the vanilla job runs unchanged and
  the party ends when the time is up or the work runs out. A **walk together** gathers (a gather spot, else the open
  middle of the colony), walks out to a grave, the gate, the fields, the water or far out, stands a ring round the
  spot and walks back (`JobDriver_PlayOutGroup`, phases in `PlayOutMath.NextWalkPhase`). A **vigil** stands that ring
  into the night, a glow at the grave or bed after dark. A **search** gives each searcher their own arc of the land
  (`SearchBearing`/`SearchRadius`) and a few stops; the answer's find (`thing`, `amount`, or `value` worth of era
  goods) is set at the feet of one who searched, or where traders drop goods if it never happens. An **errand**
  (`_CarryOut`) takes exactly what the old taking took (same things, same amount, the steward's saving; human and
  insect meat last; with `variant` the event's other number, the envoy's food or the neighbours' medicine, which their
  workers no longer take themselves), but only when it's handed over: the carrier hauls a stack to the visitor's pack or to the gate
  (`JobDriver_PlayOutCarry`), the rest goes with it at the handover, and if it never happens it's taken at once as
  before (never what another pawn has reserved or is picking up). A **hunt party** marks safe game (no predators or insects, nothing likely to turn by its kind whatever the
  difficulty, nothing bigger than a muffalo, nobody's animal, nothing bonded or venerated) and gives the game's own
  hunting job to those able to hunt. Only those capable of the work go; beliefs count (no tree cutting for those
  against it, nothing outdoors for those kept indoors, no hunt for those who hold violence or hunting wrong). Danger,
  drafting, downing, a mental state, a lord, hunger or exhaustion ends a member's part; danger ends the group (an
  errand goes back to waiting). At most eight on one, two groups at once, four hours (eight for a vigil); a plan never
  runs more than three hours past its length. Everything is saved (`PlayHand` per member).
- **Beyond answers, and things made real.** A personal story's stage and an encounter list their effects the same way
  (`ArcTracker.Apply`, `EncounterTracker.Fire`), without holding moods: a grave visit for the one a story grieves
  (`PlaySpots.GraveNamed`, falling back to the visit's `orTo`, which for a grave may be a long walk out), a supper for
  the three people of a story of three (`_Gathering.third`), lessons and songs with a visitor, a bowl carried out only
  when the stores have it (`_CarryOut.ifHave`). Work parties shear and milk (`WorkTask.Tend`: the game's
  `WorkGiver_Shear` and `WorkGiver_Milk`, counted done when the animal is no longer full); a custom of the herd's year
  that shears or milks is asked only with an animal nearly full (`HerdConditions`, `AnimalMath.ReadyToGather`, nine
  tenths: the shearing day came a day after the sheep were shorn, and its party found nothing to shear).
- **Why one didn't happen, said as it was.** "Something else had everyone's attention" stood for every bar to a gathering;
  now it is said for another gathering, and a fight, half the colony under arms and someone bleeding have their own
  words (`MeetingAttendance.Bar`, `PlayOutMath.GatherWhyKey`). A place in the open the weather kept them from is the
  weather, not "nowhere to hold it" (`PlaySpots.KeptByWeather`, `PlayOutMath.PlaceWhyKey`). Of the save's last 30
  answers to play out, 9 didn't: the shearing and the first row (above), two kept from a place, two by something else,
  an organizer never free, the fire still being laid when the time ran out, nobody free; the last three are waits that
  ran out. What an answer says now
  exists is made by `OnMapThings.cs` (pure part `OnMapMath`): `_Made` (the first of a list of things this game has
  and the colony's era allows, its quality from the maker's skill, art signed by them, a book theirs, minified; carried
  over by hand by the giver (below), or set in a corner of the one's bedroom with `inRoom`, else set down beside the
  one it's for), `_Felled` (the nearest ordinary wild trees outside home and zones felled for the wood a guest cut,
  stumps left, the logs shared out beside them; never with a tree-respecting faith at home), `_Plant` (saplings of
  the biome's commonest timber tree by the fields), `_PlanRoom` (the room planner's `FindSpot`, `Plan` and `Execute`
  on a copy of the player's config) and `_BurnFound` (addictive drugs on someone and in a bedroom of their own, with
  ash in the yard). Each is wrapped, and does nothing when it can't.
- **One thing, one round, kept for a while.** Three more group kinds (`OnMapPlays.cs`): **a thing carried by hand**
  (`PlayKind.Give`: `_Made` with a giver of ours walks the thing over and sets it at the other's feet, holding both
  their good moods until then; a thing a visitor made is fetched by the one it's for and taken to the table; `_BurnIt`
  makes the event's stash and carries it out to the yard to burn; the thing waits forbidden, and if it never happens a
  gift is set down beside its receiver after all and a stash burns where it lies; `JobDriver_PlayOutGive`),
  **rounds** (`_Rounds`: one colonist goes to each of up to eight others in turn, nearest first, a few minutes beside
  each, `JobDriver_PlayOutGroup`; `outdoors` only those out in the open) and **the dead carried out** (`_HaulDead`:
  the raiders' bodies, at most twelve, one each with the game's own jobs: `HaulToCell`, `ToCellNonStorage`, to a pyre
  beyond the gate or the tree line, then forbidden there; for `graves` and `trench` (`PlayBurial`, PlayDead.cs) a row
  of the game's Grave frames off the home area (`OnMapMath.GraveRow`, a step apart or side by side) built with
  `FinishFrame` by those who can, the bodies carried in with `HaulToContainer`, the graves set to strangers only; for
  `crematorium` (`PlayCremation`: any colony building with a bill that burns a corpse to nothing, on, powered and
  fuelled; the answer is unavailable without one) `JobDriver_PlayOutCremate` carries a body there, waits its turn and
  burns it with the bill's effect and sound, the gear stripped beside it). A vigil with `patrol` walks the colony's edge from look-out
  to look-out (`PlayVigils.Route`), `untilDawn` sets its length at the start. `OnMapTimed` (a game component, every
  250 ticks at tick 149, only while something is kept, at most twelve) keeps things for a while: `_Lanterns` (torches
  the colony could build, along the outside of its walls or by the gate, lit at dusk with a night's fuel and no
  refueling, taken away at dawn), `_KeepFire` (a campfire in a cold bedroom's free corner, fed the answer's wood,
  cleared when it's burned unless refueled by hand), the pyre (from six, `JobDriver_PlayOutLightPyre` sends the one who
  carried the dead, or the nearest free colonist, out with wood from the stores to light it; lit anyway from ten; the
  bodies stripped and left burning round a campfire until dawn, glow and smoke over each, then ash where they lay and
  the answer's words told), `_BoltDoor` (the single door of a bedroom nobody else sleeps in, forbidden from ten to five
  while its owner is asleep in bed and nothing is wrong, three nights) and `_Follow` (the story's animal on the game's
  `FollowClose` job whenever it's idle, a day and a half, never a penned animal). `OnMapRooms` places things in a
  room's free corner (against a wall, clear of the door and the bed's sides; a sarcophagus is claimed as its owner's
  grave) and trades two colonists' beds (`_SwapRooms`). Personal stories: `Arc.madeThing` keeps what was made for a
  Made gate, `{THING}` names it and a visit `to` Place goes to it; `ArcEffect_Confess` with `gathering` calls a words
  gathering and says the secret at its end (`PlayOut.afterward`, `PlayAfter`).
- **The moods.** While an answer's effects run, a good memory (`DilemmaEffect_Thought`) given to someone taking part
  in what the answer set going is held with the plan (`PlayOuts.Hold`) and given when it's over to those who were
  there long enough (a quarter of a gathering, at most half an hour; half a visit) or ate at the feast. Bad memories,
  trust, standards and the rest come at once, as before. Calendar feasts, games and remembrances hold their own moods
  the same way. A plan that falls through gives its held moods to nobody: it never happened (the player's save showed 9
  of its last 30 plans giving them all the same, with "People took heart from it all the same"). Its line says it never
  came about and why; one of the colony's own is let go without a word (the pace, below).
- **Safety.** Nobody is called from danger, sleep, a sickbed or the need of one, bleeding, starving or exhaustion,
  arms, a mental state, a caravan, the care of someone else (tending, rescuing, feeding, carrying) or an order of the
  player's; guests, prisoners and lodgers are never invited. Nothing starts while enemies are about, while another
  gathering is on, while half the colony is drafted or while someone is bleeding, and any danger calls a gathering off
  (as the council meeting is). A visit ends at any danger. A plan waits for its hours and up to its wait (six hours by
  default, never more than thirty in all), then falls through: nobody has its moods, and a gathering or a plan the colony
  was told of says why. A gathering of the player's kept from its evening by another one (the bar was another gathering,
  `PlayOut.collided`) takes the next evening instead, once (`movedOn`). The player's plans take the evening first; the
  colony's own come after. A bonfire's fire is laid in the hour before its evening opens (`PlayFire.Ahead`), and one
  still not lit two hours before the plan would give up (`PlayFire.GiveUpHours`; "still laying the fire" in the save) is
  let go: whoever was laying it stops, and it is held as a gathering without a fire, in the open or at the rec room or
  the table. Every step is wrapped so a failure is logged once. With the setting off, the effects do nothing and the
  moods come at once, as before; switched off while a plan waits, its moods and its outcome come as answers always gave them.
- **Where it plugs in.** `DilemmaTracker.Resolve` and `HappeningTracker.Resolve` wrap an answer's effects in
  `PlayOuts.Collect`, and so do a world event's beats, answers and endings (`WorldSituations`), an event's letter
  without answers (`IncidentWorker_Happening.Send`) and a story's ending (`Workers_StoryFlow`); the helpers an answer
  picks (`DilemmaEffect_Needs`) come first in `PlayOuts.Ordered`, so a play-out can name them. The tracker keeps the
  last 30 results (who took part, a note), which the dilemmas page shows as how it went (`DilemmaLog`), and This week
  lists every plan waiting or under way (`WeekModel`, `PlayOutText`); `DecreeDef.onEnact` (a feast day at noon, a festival's first evening, a rest day) and
  `AmbitionDef.achievedEffects` (the great feast eaten, the first barrel, a games night) run through `PlayOuts.Run`;
  the calendar's games and remembrance days start theirs directly, and every feast goes through the one feast engine (`PlayFeast`, the festive year below). `PlayOutTracker` (a game component of its
  own) keeps the plans; `PlayOutMath` holds the pure numbers the tests check.

### The audit

Every answer of the dilemmas, Rimfolk events and story chains, world events, the calendar days, the decrees and the
ambitions was read for something people do (`Tools/playout_audit.py` counts what plays out and lists the answers whose
words describe something done without it). Each answer it lists was read by hand; those left as they are (a refusal,
a habit, something far away or told after it happened, someone else's part, or already real) are in its skip list,
`Tools/playout_skips.txt`, with why, so it lists none still to look at and says when an entry goes stale. These now
play out (A and B are the dilemma's or event's people, X its stranger):

**Calendar, decrees, ambitions**

- **Calendar days:** a feast day is a feast from the stores (the day's lift after it); a games day a gathering in the
  rec room; a remembrance of one of the colony's dead an hour by their grave, for those who knew them, if the grave is
  on the map. A rest day was already real (work hours turned to recreation) and is unchanged. A planting day (a day the
  player names and keeps that way) is a sowing party in the fields and a word at the field's edge after, or a walk out
  to the fields when there's nothing to sow.
- **Decrees:** a feast day (a feast at noon), festival days (music on the first evening), days of rest (the first
  day's work hours are free time); drills and the militia's muster, pairs sparring in the open each evening ("Harm from
  everyday life", Drills that bruise).
- **Ambitions reached (the council meeting's outcomes):** the great feast, the great hall, fine dining and a table for
  all (a feast), the brewery (a toast), fun and games (a games night), friends at the camp (an evening at the camp's
  fire, with songs).

**Dilemmas and events**, by file:

**Council_Dilemmas**

- **a forgotten stash** (`Council_Dilemma_Stash`): "Share it out tonight": gathering ("the night the stash was shared").
- **the cook's feast** (`Council_Dilemma_Feast`): "Let A cook the feast": feast from the stores ("A's feast") · "A smaller feast": feast from the stores ("A's supper").
- **a confession** (`Council_Dilemma_Confession`): "Ask B to look after A": B keeps A company.
- **a feud** (`Council_Dilemma_Feud`): "Make them work side by side": a work party of A and B (haul, clean, repair) ("the shared shift").
- **a thief in the stores** (`Council_Dilemma_Thief`): "Punish A publicly": gathering to hear it ("A's punishment at supper").

**Council_Dilemmas2**

- **a bad omen** (`Council_Dilemma_Omen`): "Let A hold a ceremony": bonfire (its own wood) ("A's fire") · "Laugh it off at dinner": gathering to hear it ("the joke at dinner").
- **a broken keepsake** (`Council_Dilemma_Keepsake`): "B apologizes publicly": gathering to hear it, B speaks ("B's apology"), also when it goes wrong.
- **a duel of honor** (`Council_Dilemma_Duel`): "Make them shake hands in public": gathering to hear it, A speaks ("the handshake") · "Settle it with a hauling race": a work party of A and B (haul, build) ("the hauling race").
- **asleep on watch** (`Council_Dilemma_Watch`): "Punish B with extra shifts": a work party of B (haul, clean) ("B's extra shift") · "Put A in charge of the watch": a vigil at the gate ("the first night watch").
- **an unflattering likeness** (`Council_Dilemma_Sculpture`): "Smash it": a work party of B (clean, haul) ("the sweeping up").
- **not pulling weight** (`Council_Dilemma_Slacker`): "Pair them up on every shift": a work party of A and B (haul, clean, repair) ("the first shift together").

**Council_Dilemmas3**

- **the young hunter** (`Council_Dilemma_YoungHunter`): "Teach A to shoot": A learns at B's side (shooting) · "Let B show A the messy part": A learns at B's side (cooking), also when it goes wrong · "A carries the game bags, nothing more": a hunt party ("B's hunt").
- **where the dead go** (`Council_Dilemma_WhereTheDeadGo`): "Hold a funeral for the animal": gathering to hear it, A speaks ("the animal's funeral").
- **an animal in decline** (`Council_Dilemma_PetFading`): "Take the pet on one last walk": A goes for a long walk · "Nurse the pet": B keeps the pet company, also when it goes wrong · "Let B decide": only when it goes wrong: B goes for a long walk.
- **a hungry look** (`Council_Dilemma_PetPot`): "B apologizes to the pet": gathering to hear it, B speaks ("B's apology to the pet").
- **a love letter** (`Council_Dilemma_LoveLetter`): "Make B apologize at dinner": gathering to hear it, B speaks ("B's apology"), also when it goes wrong · "Let A read the rest aloud": gathering to hear it, A speaks ("the reading of the letter"), also when it goes wrong · "Give B the worst chores for a week": a work party of B (clean, haul, corpses) ("B's worst chores").
- **a funeral in advance** (`Council_Dilemma_OwnFuneral`): "Hold a living funeral": gathering to hear it, A speaks ("A's living funeral"), also when it goes wrong · "Let A pick a grave site": A goes for a long walk.
- **the bite** (`Council_Dilemma_PetBite`): "Keep the pet penned for a while": A keeps the pet company.
- **the sleepwalker** (`Council_Dilemma_Sleepwalker`): "B sits up with A at night": B keeps A company.

**Council_Dilemmas_ComingOfAge**

- **a path: the workshop** (`Council_Dilemma_Path_Craft`): "Apprentice A to B in crafting": A learns at B's side (crafting).
- **a path: the healer** (`Council_Dilemma_Path_Medicine`): "Apprentice A to B in medicine": A learns at B's side (medicine).
- **a path: the scholar** (`Council_Dilemma_Path_Scholar`): "Apprentice A to B in study": A learns at B's side (intellectual).
- **a path: the wall** (`Council_Dilemma_Path_Warrior`): "Apprentice A to B at arms": A learns at B's side (shooting).

**Council_Dilemmas_Customs**

- **the planting day** (`Council_Dilemma_PlantingBlessing`, once the planting blessing): "Make a day of the planting": a work party up to 6 (sow) by the fields ("the planting day"), then a gathering at the field's edge to hear A ("a word at the field's edge") · "A sows the first row": a work party of A (sow) ("the first row").
- **the remembrance walk** (`Council_Dilemma_RemembranceWalk`): "Walk to the graves together": a walk together to a grave and back ("the walk to the graves") · "Those who want to can go": A sits by a grave · "The living have work to do": A sits by a grave.
- **the midsummer swim** (`Council_Dilemma_MidsummerSwim`): "Everyone to the water": gathering ("the midsummer swim") · "A swim once the day's work is done": gathering ("the evening swim").
- **the harvest dance** (`Council_Dilemma_HarvestDance`): "Dance until the fire burns down": bonfire with music ("the harvest dance") · "One dance, then bed": bonfire with music ("the harvest dance").
- **first-frost soup** (`Council_Dilemma_FirstFrostSoup`): "One big pot for everyone": feast from the stores ("the first frost soup").
- **lantern night** (`Council_Dilemma_LanternNight`): "A lantern in every window": gathering ("the lantern night") · "One lantern at the gate": a walk together to the gate and back ("the walk out to the lantern").

**Council_Dilemmas_Customs2**

- **the sun's return** (`Council_Dilemma_SunsReturn`): "Everyone up to greet the sun": bonfire ("the sun's return").
- **the rain welcome** (`Council_Dilemma_RainWelcome`): "Everyone out in the rain": gathering ("the rain welcome").
- **the blessing of the waters** (`Council_Dilemma_WaterBlessing`): "Everyone to the water's edge": gathering to hear it, A speaks ("the blessing of the waters") · "A can bless it alone": A goes out to the water ("A's blessing of the waters").
- **the marsh lights** (`Council_Dilemma_MarshLights`): "Everyone out with the torches": gathering ("the marsh lights").
- **the high watch** (`Council_Dilemma_HighWatch`): "Everyone up to the high ground": gathering to hear it, A speaks ("the high watch") · "Those who want to can climb": a walk together far out and back ("the climb at sunset").
- **the night vigil** (`Council_Dilemma_NightVigil`): "Keep the vigil together": gathering to hear it, A speaks ("the night vigil") · "A short vigil, then bed": gathering to hear it, A speaks ("the words at dusk").
- **the tree-singing** (`Council_Dilemma_TreeSinging`): "Everyone out to sing": gathering with music ("the tree-singing") · "A song at dusk, before supper": gathering with music ("the song at dusk").
- **the ancestors' supper** (`Council_Dilemma_AncestorsSupper`): "A place at the table for every one of them": feast from the stores ("the ancestors' supper") · "Say their names at supper": a toast, A speaks ("the names said at supper").

**Council_Dilemmas_Goals**

- **a teacher for a dream** (`Council_Dilemma_Goal_Mentor`): "Ask B to make the time": A learns at B's side, also when it goes wrong · "Free up B's evenings": A learns at B's side.
- **a dream fading** (`Council_Dilemma_Goal_GivingUp`): "Rally the colony behind A": gathering to hear it, A speaks ("the dinner for A"), also when it goes wrong · "A few easy days to think it over": time off for A.
- **if I don't make it** (`Council_Dilemma_Goal_Remember`): "Write it into the colony's records": gathering to hear it, A speaks ("the reading of A's dream").
- **a foolish dream** (`Council_Dilemma_Goal_Foolish`): "Stand behind A's dream": gathering to hear it ("the council's word at dinner").
- **two dreams, one colony** (`Council_Dilemma_Goal_TwoDreams`): "Make them help each other": B keeps A company.
- **the road out** (`Council_Dilemma_Goal_Caravan`): "Give A the council's blessing": A goes out to the gate.

**Council_Dilemmas_Health**

- **a day away** (`Council_DlmH_DayAway`): "Give A the day": time off for A; A goes for a long walk · "B keeps A company for the day": time off for A and B; B keeps A company.
- **a say in the operation** (`Council_DlmH_FamilySay`): "Put it off until they've talked it through": B keeps A company · "B stays at A's side through it": B keeps A company.
- **the midwife's apprentice** (`Council_DlmH_MidwifeApprentice`): "B learns from A": B learns at A's side (medicine).

**Council_Dilemmas_Life**

- **keeping apart** (`Council_DlmL_Quarantine`): "Have someone nurse A apart from the rest": B keeps A company.
- **fever at the camp** (`Council_DlmL_CampSick`): "Send them medicine": A goes to the camp's fire · "Send our doctor to tend them": B goes to the camp's fire · "Food and blankets, not medicine": A goes to the camp's fire.
- **an empty pot at the camp** (`Council_DlmL_CampHungry`): "Have our best tracker show their hunters the game": B goes to the camp's fire · "Fill their pot": food carried out to X ("the food for the camp").
- **the empty chair** (`Council_DlmL_EmptyChair`): "Have a good talker tell A the whole of it": B keeps A company.
- **a day off, now** (`Council_DlmL_DayOffBadTime`): "Give A the days": time off for A · "Everyone takes a half day, in turn": time off for A.
- **it's gone too far** (`Council_DlmL_FrictionGrown`): "Sit them down and make them talk it out": A keeps B company · "One week, one job, together": a work party of A and B (haul, clean, repair) ("the shared job").
- **putting down the tools** (`Council_DlmL_ElderRest`): "Let A rest; A has earned it": time off for A · "Ask A to teach B instead": B learns at A's side.
- **a child's question** (`Council_DlmL_ChildQuestion`): "Ask our most learned to answer it": A learns at B's side (intellectual) · "Tell A a story instead": gathering to hear it ("the story for A").
- **a dead traveler's journal** (`Council_DlmL_FoundJournal`): "Read it aloud over supper": gathering to hear it, A speaks ("the reading of the journal") · "Bury it with a marker": a walk together far out and back ("the burying of the journal").
- **a promise not kept** (`Council_DlmL_BrokenPromise`): "Apologize at the table": gathering to hear it ("the council's apology to A") · "Have our best talker explain what happened": B keeps A company.
- **the same work every day** (`Council_DlmL_SameWork`): "Have someone teach A a new trade": A learns at B's side.
- **small hands at the workbench** (`Council_DlmL_ChildAtTheBench`): "B takes A on as an apprentice": A learns at B's side (crafting) · "Small jobs only, under B's eye": A learns at B's side (crafting) · "Let A make one thing, start to finish": A learns at B's side (crafting) · "Play is a child's work": time off for A.
- **the doctor needs sleep** (`Council_DlmL_TiredDoctor`): "B sleeps; someone else sits up": B sent to bed · "Teach the basics to everyone who's awake": gathering to hear it, B speaks ("B's lesson in nursing") · "B keeps watch; others cover their work": a vigil at A's bedside ("B's watch at A's bedside").
- **the last of the medicine** (`Council_DlmL_LastMedicine`): "Send a forager out for wild herbs": a search of the land ("B's search for wild herbs"), also when it goes wrong · "Treat A with the medicine": B keeps A company, also when it goes wrong.
- **an old remedy** (`Council_DlmL_OldRemedy`): "Let B try the old remedy": B keeps A company, also when it goes wrong.
- **medicine for a stranger** (`Council_DlmL_StrangerMedicine`): "Have A look at X, but keep the medicine": A spends time with X · "Give X a dose": A spends time with X.
- **a strange cure** (`Council_DlmL_OddCure`): "Let X try it, with our doctor watching": B learns at X's side (medicine), also when it goes wrong · "Let X try the cure": A spends time with X, also when it goes wrong.
- **a child missing from the camp** (`Council_DlmL_CampLostChild`): "Send our best tracker to search": a search of the land ("the search for the lost child"), also when it goes wrong · "Keep the gate lit and the bell ringing all night": a vigil at the gate ("the lit gate").
- **the same bowl again** (`Council_DlmL_SameMeal`): "Have our best cook try something new each week": feast from the stores ("B's new supper"), also when it goes wrong · "Send a forager out for something fresh": a search of the land ("B's forage for something fresh"), also when it goes wrong.
- **elbow to elbow** (`Council_DlmL_Crowded`): "Have a builder mark out a new room": a walk together in the open ("the walk round the new room").

**Council_Dilemmas_Mods_Hospitality**

- **a guest who wants to stay** (`Council_DlmH_GuestWantsToStay`): "Ask X to come back another season": A spends time with X · "Take X in, with a gift to their people": silver carried out to the gate ("the gift for their people").
- **a guest's complaint** (`Council_DlmH_GuestComplaint`): "Give back the fee, and a good meal": silver carried out to X ("X's silver").

**Council_Dilemmas_Pets**

- **the eaten dinner** (`Council_Dilemma_PetDinner`): "A cooks B a new meal": feast from the stores ("A's dinner for B").

**Council_Dilemmas_Reputation**

- **a joke at our expense** (`Council_DlmR_StingyMockery`): "Send X off with a gift": A spends time with X.
- **the way it's done here** (`Council_DlmR_SkipCustom`): "Have B tell A why it matters": B keeps A company.
- **how it used to be** (`Council_DlmR_OldCustom`): "Bring it back, the way it was": feast from the stores with music ("the old custom") · "Bring it back, with a few changes": feast from the stores with music ("the old custom").
- **a friend at the gate** (`Council_DlmR_FriendAtTheGate`): "A can go out to X": A spends time with X · "Open the gate for X": A spends time with X.
- **one day off** (`Council_DlmR_DayOfRest`): "Give A the day": time off for A; A goes for a long walk · "A day of rest for everyone": time off for everyone.
- **carrying everyone** (`Council_DlmR_CarryingEveryone`): "Thank A at supper": toast ("the thanks to A") · "Tell A to slow down too": time off for A.
- **one act of mercy** (`Council_DlmR_OneMercy`): "Send food, and ask nothing": food carried out to the gate ("the food for their people").

**Council_Dilemmas_Seasons**

- **midsummer night** (`Council_Dilemma_Midsummer`): "Light the bonfire": bonfire (its own wood) with music · "A small fire, no drinking": bonfire (its own wood) ("the small fire").
- **harvest home** (`Council_Dilemma_HarvestHome`): "A harvest feast for everyone": feast from the stores · "A modest supper": feast from the stores ("the harvest supper").
- **the longest night** (`Council_Dilemma_Midwinter`): "Extra rations for the long night": feast from the stores with music · "Stories by the fire, no feast": bonfire ("the stories by the fire").
- **after the battle** (`Council_Dilemma_VictoryFeast`): "A victory feast": feast from the stores · "A quick hot meal, then the walls": feast from the stores ("the hot meal after the battle") · "Have the best cook stretch half the food": feast from the stores, also when it goes wrong · "Repairs first, celebrations later": a work party up to 6 (repair, build, corpses, clean) ("the repairs after the battle").
- **naming the year** (`Council_Dilemma_NameTheYear`): "Name it for those we lost": a walk together to a grave and back ("the names said at the graves").
- **the spoils** (`Council_Dilemma_Spoils`): "The fighters get the spoils": a work party of the others (clean, corpses, haul) ("the cleanup").
- **the enemy dead** (`Council_Dilemma_EnemyDead`): "Give them graves": a work party up to 4 (corpses, clean) ("the burying of the raiders") · "One grave for all of them": a work party up to 3 (corpses, clean) ("the trench outside the wall").

**Council_Dilemmas_Secrets**

- **blood on old hands** (`Council_Dilemma_Secret_BloodPast`): "Have A tell the whole story": gathering to hear it, A speaks ("A's story") · "Let A work off the past": a work party of A (corpses, clean, haul) ("A's penance").
- **the loose tongue** (`Council_Dilemma_Secret_LooseTongue`): "Make B apologize at supper": gathering to hear it, B speaks ("B's apology") · "Sit them down until they sort it out": A keeps B company.
- **a kindness found out** (`Council_Dilemma_Secret_KindnessFound`): "Raise a cup to A at supper": feast from the stores ("the supper for A").
- **one more bout** (`Council_Dilemma_Secret_OneMoreBout`): "Let A teach fighting instead": B learns at A's side (melee).
- **using again** (`Council_Dilemma_Secret_UsingAgain`): "A week off, with B beside A": time off for A and B; B keeps A company.
- **what came out** (`Council_Dilemma_Secret_WhatCameOut`): "Talk it through one evening": gathering to hear it, A speaks ("the evening of questions").

**Council_Dilemmas_Social**

- **a line down the middle** (`Council_Dilemma_Soc_SwornRivals`): "Make them teach each other their trades": B learns at A's side.
- **the remembrance** (`Council_Dilemma_Soc_Remembrance`): "Hold the remembrance night": gathering ("the remembrance night") · "Allow speeches, but no feast": gathering to hear it, A speaks ("the speeches").
- **a rival's word** (`Council_Dilemma_Soc_Accusation`): "Make B take it back in public": gathering to hear it, B speaks ("B's retraction").
- **a friend left behind** (`Council_Dilemma_Soc_LeftBehind`): "Ask A to make time for B": A keeps B company · "Bring B into the new crowd": B keeps A company, also when it goes wrong.
- **the closed circle** (`Council_Dilemma_Soc_ClosedCircle`): "Have A host a supper for everyone": feast from the stores ("A's supper").
- **inseparable** (`Council_Dilemma_Soc_Inseparable`): "Let them work together": a work party of A and B (haul, clean, repair) ("the first shift together") · "Give them the night watch, together": a vigil at the gate ("the night watch").
- **the cover story** (`Council_Dilemma_Soc_CoverUp`): "Punish them both": a work party of A and B (clean, haul) ("the punishment").
- **a plea for a friend** (`Council_Dilemma_Soc_Plea`): "Halve it, if B takes the other half": a work party of A and B (haul, clean) ("the shared punishment").

**Council_Dilemmas_Strangers**

- **the trader's eye** (`Council_DlmS_HeirloomOffer`): "Sell it": A sits by a grave.
- **a voice through the door** (`Council_DlmS_PrisonerPlea`): "Hear X out properly": A spends time with X.
- **hungry at the gate** (`Council_DlmS_Beggars`): "Send them home with seed, and a grower's advice": B spends time with X · "Fill their sacks": food carried out to X ("the food for their people") · "Give them silver instead": silver carried out to X ("the silver for their people").
- **the traveling healer** (`Council_DlmS_Healer`): "Pay X to teach B": B learns at X's side (medicine) · "Pay the healer": B learns at X's side (medicine), also when it goes wrong · "Let B treat it, with our own medicine": B keeps A company, also when it goes wrong.
- **a guest's tongue** (`Council_DlmS_RudeGuest`): "Ask our best talker to settle it": B spends time with X, also when it goes wrong.
- **a face from before** (`Council_DlmS_OldAcquaintance`): "Pay for their silence": silver carried out to X ("the silver for X's silence").

**Council_Events_Callers**

- **a traveling scribe** (`Council_Evt_Scribe`): "A fair copy of the colony's story (half that)": gathering to hear it, A speaks ("the telling of the colony's story") · "Have X teach reading to anyone who'll sit still (half that)": gathering to hear it ("X's reading lesson").
- **a photographer** (`Council_Evt_Photographer`): "Everyone out front (n silver)": gathering ("the photograph").
- **a spirit-singer** (`Council_Evt_SpiritSinger`): "Let X sing to the land (10 food)": bonfire with music ("X's song") · "Ask X to sing for our dead (10 food)": gathering with music ("X's songs for the dead").
- **a traveling cook** (`Council_Evt_WanderingCook`): "Give X the kitchen (40 food)": feast from the stores ("X's cooking") · "Have X teach our cook (n silver)": B learns at X's side (cooking).
- **a hedge healer** (`Council_Evt_Bonesetter`): "Pay in medicine (2), and let our doctor watch": B learns at X's side (medicine).
- **a song for the weary** (`Council_Evt_Fiddler`): "Let X play for A": A spends time with X · "A song for everyone after supper (n silver)": gathering with music ("X's playing") · "Thank X, and let X go on": gathering with music ("X's tunes at supper").
- **a peacemaker** (`Council_Evt_Peacemaker`): "Let X sit them down": A keeps B company · "Set them a job to do together": a work party of A and B (haul, build) ("the woodpile").
- **a tourist** (`Council_Evt_Tourist`): "Put X to work, as promised": B spends time with X, also when it goes wrong.
- **guests at the council** (`Council_Evt_CouncilGuests`): "Let them watch, and ask what they like": A spends time with X.

**Council_Events_Chains**

- **the lost child returns** (`Council_Evt_ChildGrown`): "Ask X to stay": A spends time with X · "Thank X and wish X well": feast from the stores ("supper with X") · "Send X home with gifts for the family (40 silver)": silver carried out to X ("the gifts for X's family") · "Have a good crafter teach X a trade": B spends time with X.

**Council_Events_Challenges**

- **traveling champion** (`Council_Evt_Champion`): "Accept, and lay on food for the onlookers": feast from the stores ("the food for the onlookers").
- **tribute demanded** (`Council_Evt_Tribute`): "Pay the n silver": silver carried out to the gate ("the tribute") · "Let A haggle": silver carried out to the gate ("half the tribute").

**Council_Events_Land**

- **white nights** (`Council_Evt_WhiteNights`): "Stay up through the bright night together": bonfire ("the bright night").
- **the desert in flower** (`Council_Evt_DesertBloom`): "Walk out to see it": gathering ("the walk among the flowers") · "Have our best grower gather the seed": a search of the land ("B's seed gathering") · "Work goes on": a walk together far out and back ("the walk out to the bloom").
- **the long rains** (`Council_Evt_LongRains`): "Games and stories indoors": gathering ("the rainy evening") · "Patch every roof and dig proper drains": a work party up to 4 (repair, clean, haul) ("the patching and the drains").
- **the river freezes** (`Council_Evt_IceFair`): "Hold our own on our stretch of river": bonfire ("the ice fair").
- **the birds come back** (`Council_Evt_BirdsReturn`): "Take an hour to welcome them": gathering ("the hour for the birds") · "Get the planting started": a work party up to 6 (sow) ("the planting").
- **the rut** (`Council_Evt_Rut`): "Send the hunters out": a hunt party ("B's hunt") · "Go up the ridge at dusk to watch": gathering ("the evening on the ridge").
- **the first snow** (`Council_Evt_FirstSnow`): "Call it a snow day": time off for everyone · "Clear the paths and carry on": a work party up to 4 (snow, haul) ("clearing the paths").
- **the longest night** (`Council_Evt_LongestNight`): "Keep the fire burning till dawn (40 wood)": bonfire (its own wood) ("the longest night") · "A good supper, then early to bed": feast from the stores ("the longest night's supper").
- **the sun goes dark** (`Council_Evt_Eclipse`): "Drum and shout the sun back": gathering with music ("the drumming") · "Watch it through smoked glass": gathering to hear it, B speaks ("the eclipse").
- **mushrooms after rain** (`Council_Evt_MushroomFlush`): "Send our best grower to pick the good ones": a search of the land ("B's mushroom picking"), also when it goes wrong · "Pick enough for supper tonight": gathering ("the mushroom supper").

**Council_Events_Living**

- **the lovers' family** (`Council_Evt_LoversKin`): "Let them talk": A spends time with X; B spends time with X · "Send a gift home (n silver)": silver carried out to X ("the gift for X's family") · "Ask X to leave": A spends time with X.
- **a letter from home** (`Council_Evt_KinLetter`): "Read it out at supper": gathering to hear it, A speaks ("the letter from home").
- **known by name** (`Council_Evt_Renown`): "Let A help": A spends time with X.
- **founding day** (`Council_Evt_Founding`): "Hold a feast": feast from the stores ("the founding feast") · "A quiet supper": toast ("the founding day toast") · "Ask A to tell the story of the first days": gathering to hear it, A speaks ("the story of the first days").
- **a guest on a winter night** (`Council_Evt_MidwinterGuest`): "Bring X in": A spends time with X.

**Council_Events_Living_Mods**

- **a strange creature** (`Council_Evt_RareCreature`): "Let A try": A keeps X company ("A's afternoon with the creature").

**Council_Events_More**

- **traveling doctor** (`Council_Evt_Inoculation`): "Pay half, and have X teach A": A learns at X's side (medicine).
- **journalist** (`Council_Evt_Journalist`): "Give the tour, with A doing the talking": A spends time with X, also when it goes wrong.

**Council_Events_NewsAndPlayers**

- **traveling player** (`Council_Evt_Players`): The letter: gathering around X ("X's play").

**Council_Events_Pasts**

- **old comrade** (`Council_Evt_OldComrade`): "Welcome X": A spends time with X.
- **lost family** (`Council_Evt_LostKin`): "Bring X in": A spends time with X.
- **bounty hunters** (`Council_Evt_BountyHunters`): "Pay them n silver": silver carried out to the gate ("the bounty").

**Council_Events_Remembered**

- **an old debt repaid** (`Council_Evt_OldDebt`): "Accept it, and feast their envoy (20 raw food)": feast from the stores ("the feast for X") · "Ask for something the colony needs instead (a good talker)": B spends time with X.
- **a favor called in** (`Council_Evt_FavorCalled`): "Draw up plans for their rebuilding (a builder)": B spends time with X.
- **an old enemy offers peace** (`Council_Evt_OldEnemyPeace`): "Make peace": A spends time with X · "Make peace over a meal (25 raw food)": A spends time with X.
- **a quarrel between neighbors** (`Council_Evt_Quarrel`): "Hear both sides and judge fairly (a good talker)": B spends time with X.
- **help from old friends** (`Council_Evt_Relief`): "Accept it with thanks": a toast ("a drink with the carters").

**Council_Events_Roots**

- **a taste of home** (`Council_Evt_TasteOfHome`): "Buy it, and let A cook it for everyone (n silver, 10 food)": feast from the stores ("A's supper from home").
- **an old enemy makes amends** (`Council_Evt_Amends`): "Let A hear X out": A spends time with X · "Ask X to stay for supper": A spends time with X.
- **a stonecutter** (`Council_Evt_Headstones`): "A proper marker for every grave (n silver)": A sits by a grave · "Have X teach our best crafter how (half that)": B learns at X's side (artistic).
- **a feast at the camp** (`Council_Evt_CampFeast`): "Go, and bring food (20 food)": gathering ("the evening at the camp") · "Go, and enjoy their hospitality": gathering ("the evening at the camp").
- **a quarrel with the camp** (`Council_Evt_CampQuarrel`): "Have our best talker sort it out between them": B goes to the camp's fire, also when it goes wrong.
- **a neighbor's bridge** (`Council_Evt_BridgeWork`): "Send them timber instead (80 wood)": wood carried out to the gate ("the timber for the town").

**Council_Events_Stories**

- **the wedding** (`Council_Story_FeudWedding`): "Hold the wedding here (40 raw food for the feast)": feast from the stores ("the wedding feast") · "Send them home with a gift (n silver)": silver carried out to X ("the wedding gift").
- **the preacher** (`Council_Story_Prophet`): "Let them stay and preach": gathering with music ("X's preaching") · "A will argue with X": A spends time with X.
- **a divided colony** (`Council_Story_ProphetSchism`): "Call everyone together (A speaks)": gathering to hear it, A speaks ("the meeting in the dining room").
- **the town decides** (`Council_Story_FeudSided`): "Send the other family a peace gift (n silver)": silver carried out to the gate ("the peace gift").
- **the lost expedition** (`Council_Story_Expedition`): "Outfit a rescue (40 raw food, n silver for guides)": food carried out to X ("the food for the rescue").

**Council_Events_Stories_More**

- **the lost letters** (`Council_Story_Letters`): "Read them first": gathering to hear it, A speaks ("the reading of the letters").
- **an answer to the letters** (`Council_Story_LettersReply`): "Welcome X, and listen": A spends time with X.
- **the stranger comes back** (`Council_Story_SeedsReturn`): "Show X the garden, and share the harvest": A spends time with X.
- **a play about the colony** (`Council_Story_Play`): "Tell X everything, good and bad": A spends time with X · "Tell X the best of it": A spends time with X.
- **the premiere** (`Council_Story_PlayPremiere`): "Watch it with everyone": gathering ("the play").
- **an admirer** (`Council_Story_PlayFans`): "Show X around": A spends time with X.
- **the stranger's seeds** (`Council_Story_Seeds`): "Plant them, and let A tend them": A goes out to the fields ("the planting of the seeds").

**Council_Events_Storyteller**

- **an old adventurer** (`Council_Evt_OldAdventurer`): "An evening of stories": gathering ("X's stories") · "Ask X to stay (may say no)": only when it goes wrong: gathering to hear it ("X's stories").
- **a silent guest** (`Council_Evt_SilentMonk`): "Ask X to sit with A": A spends time with X.
- **the limb-maker** (`Council_Evt_LimbMaker`): "Pay a little to have X teach our best crafter": B learns at X's side (crafting).
- **an envoy** (`Council_Evt_Envoy`): "Pay n silver for peace": silver carried out to the gate ("the silver for the peace") · "Pay the food for peace": food carried out to the gate ("the food for the peace").

**Council_Events_Storyteller_Land**

- **a fallen star** (`Council_Evt_StarIron`): "Leave it where it fell": a walk together to the place itself and back ("the evening walk out to the fallen star").
- **a sign at dawn** (`Council_Evt_OmenBeast`): "Hunt it": a hunt party ("the hunt for the beast").
- **wild bees** (`Council_Evt_WildBees`): "Let A smoke them out (they may not like it)": A goes out into the open ("the smoking of the bees"), also when it goes wrong.

**Council_Events_Variety**

- **traveling tinker** (`Council_Evt_Tinker`): "Pay a little to have X teach A": A learns at X's side (crafting).
- **old grave** (`Council_Evt_OldGrave`): "Rebury them properly": A sits by a grave.
- **old letters** (`Council_Evt_OldLetters`): "Read them aloud at supper": gathering to hear it, A speaks ("the old letters").
- **family visit** (`Council_Evt_FamilyVisit`): "Enjoy the visit": A spends time with X.
- **fortune teller** (`Council_Evt_FortuneTeller`): "Pay n silver": gathering ("the fortune-telling") · "One reading, for A only": A spends time with X.
- **neighbors' quarrel** (`Council_Evt_Dispute`): "Let A work it out": A spends time with X, also when it goes wrong.
- **runaway lovers** (`Council_Evt_Elopers`): "Send them home": A spends time with X.
- **neighbors in need** (`Council_Evt_NeighborsSick`): "Send it as a gift": medicine carried out to the gate ("the medicine for their people") · "Sell it to them (n silver)": medicine carried out to the gate ("the medicine for their people") · "Send half, with A's instructions": medicine carried out to the gate ("the medicine for their people").

**Council_Events_Visitors**

- **traveling scholar** (`Council_Evt_Scholar`): "Host X for the night": A spends time with X · "Ask X to teach everyone instead": gathering to hear it ("X's lesson").
- **would-be apprentice** (`Council_Evt_Apprentice`): "Take X on": X learns at A's side.
- **quiet stranger** (`Council_Evt_QuietStranger`): "Let A talk to X first": A spends time with X.
- **traveling storyteller** (`Council_Evt_Storyteller`): The letter: gathering to hear it ("X's tales").
- **mourners** (`Council_Evt_Mourners`): The letter: A sits by a grave.
- **hungry pilgrims** (`Council_Evt_Pilgrims`): "Feed them": food carried out to X ("the food for the pilgrims") · "Bread for the road, no more": food carried out to the gate ("bread for the road").
- **lost child** (`Council_Evt_LostChild`): "Take X home": A spends time with X.
- **stray** (`Council_Evt_Stray`): "Let it go": A spends time with X.

**Rimfolk_Dilemmas_Animals**

- **the herd or the stores** (`Council_DlmA_FamineCull`): "Cull n head": gathering to hear it, A speaks ("a word for the herd") · "Turn the weakest out to forage": a walk together to the gate and back ("turning the herd out").
- **a raider in the crops** (`Council_DlmA_CropRaider`): "Send the hunters": a hunt party · "Watch the fields at night": a vigil at the fields ("the watch over the fields") · "Let it eat": A goes out to the fields ("A's look at the fields").
- **medicine for an animal** (`Council_DlmA_SickAnimal`): "The good medicine for the pet": B keeps the pet company · "A nurses the pet through": a vigil at the pet's side ("a night with the pet").
- **something at the pens** (`Council_DlmA_PenStalker`): "Keep watch at the pens": a vigil at the pet's side ("the watch at the pens") · "Bring the herd in close": a work party of A and B (herd) ("bringing the herd in close").
- **a bond against the ideoligion** (`Council_DlmA_BondFaith`): "Ask B to bless the pet": gathering to hear it, B speaks ("B's blessing over the pet").
- **the lambing watch** (`Council_Dilemma_LambingWatch`): "Keep the lambing watch together": a vigil at the pet's side ("the lambing watch") · "A sits up alone": A keeps the pet company.
- **shearing day** (`Council_Dilemma_ShearingDay`): "Make a day of it": gathering ("shearing day").

**Rimfolk_Dilemmas_Animals2**

- **bringing the herd home** (`Council_Dilemma_HerdHome`): "Bring the herd home together": a work party up to 5 (herd) ("bringing the herd home") · "A brings them in alone": a work party of A (herd) ("bringing the herd home").

**Rimfolk_Dilemmas_Lives**

- **a broken window** (`Council_DlmLv_Mischief`): "A mends it, with B's help": A learns at B's side (construction) · "Say sorry to everyone at supper": gathering to hear it, A speaks ("an apology at supper").
- **afraid after the fighting** (`Council_DlmLv_Frightened`): "B sits up with A tonight": B keeps A company ("sitting up with A") · "Stories round the fire for all the children": bonfire ("stories round the fire") · "Show A the walls and the watch": A keeps B company ("a walk round the walls").
- **a child's grief** (`Council_DlmLv_ChildGrief`): "B takes A under their wing": B keeps A company ("keeping A company") · "A goodbye at the grave, with A": gathering to hear it, A speaks ("goodbye at the grave") · "Keep A busy with lessons": A learns at B's side.
- **lessons for the children** (`Council_DlmLv_Schooling`): "Morning lessons, with B": A learns at B's side (intellectual) · "Let them run free a while yet": time off for A.
- **whose den is it** (`Council_DlmLv_ChildrenQuarrel`): "They share it, and that's that": A keeps B company ("playing in the den together").
- **a youngster who wants to see the world** (`Council_DlmLv_TeenRoad`): "A long walk past the fields with B, first": A goes for a long walk.
- **one last look at the hills** (`Council_DlmLv_ElderWalk`): "B goes with A for the day": A goes for a long walk; B keeps A company ("keeping A company") · "Let A go alone, as A asks": A goes for a long walk.
- **won't see the doctor** (`Council_DlmLv_ElderDoctor`): "Insist: B sees to A today": B keeps A company ("seeing to A") · "Strike a bargain: a check-up for a favor": B keeps A company ("seeing to A").
- **the old one's tale** (`Council_DlmLv_ElderTale`): "Everyone gathers to hear it": gathering to hear it, A speaks ("the colony's story") · "B writes it all down": B keeps A company ("writing down A's story").
- **the old one and the little one** (`Council_DlmLv_ElderAndChild`): "Let A teach B": B learns at A's side.
- **cold in the bones** (`Council_DlmLv_ElderCold`): "B sits with A by the fire in the evenings": B keeps A company ("an evening by the fire").
- **the old way and the new** (`Council_DlmLv_OldWayNewWay`): "Work it out together": B learns at A's side.
- **the worrier's watch** (`Council_DlmLv_WorrierWatch`): "B talks it through with A": B keeps A company ("going through the list with A").
- **the colony's record** (`Council_DlmLv_RecordKeeper`): "Read out the good parts at supper": gathering to hear it, A speaks ("a reading from the colony's record").

**Rimfolk_Dilemmas_Lives_Customs**

- **the snow day** (`Council_Dilemma_SnowDay`): "A snow day for everyone": time off for the colony; gathering ("the snow day") · "The children can play; the rest of us work": time off for A · "Snow means more work, not less": a work party up to 4 (snow, haul, clean) ("the shoveling").
- **the children's race** (`Council_Dilemma_ChildrensRace`): "Everyone comes out to cheer": gathering in the fields ("the children's race") · "The children can race on their own": A goes out to the fields ("the children's race").
- **the children's gleaning** (`Council_Dilemma_Gleaning`): "The gleanings are the children's": gathering to hear it, A speaks ("the gleaning") · "Glean, but it goes to the stores": A goes out to the fields ("the gleaning").
- **a tree for every child** (`Council_Dilemma_ChildTrees`): "Every child plants a tree": gathering to hear it, A speaks ("the planting of the children's trees") · "Just A's tree, this year": A goes for a long walk.

**Rimfolk_Events_Aftermath**

- **survivors give thanks** (`Council_Evt_After_PlagueThanks`): "Say the names of their dead together": gathering to hear it, X speaks ("the names of the town's dead").
- **the first harvest shared** (`Council_Evt_After_FirstHarvest`): "Bake it into a harvest supper, with them": feast from the stores ("the supper of the first harvest").
- **what the river took** (`Council_Evt_After_FloodBell`): "Help them carry it home": A goes for a long walk.
- **guests of the winter return** (`Council_Evt_After_SpringReturn`): "Plant it together, out in the fields": gathering in the fields ("the planting with their people").
- **old friends send seed** (`Council_Evt_After_OldFriends`): "Accept, and feast their messenger": feast from the stores ("the supper for X").
- **a reckoning** (`Council_Evt_After_Reckoning`): "Make amends (n silver for the town)": silver carried out to X ("the amends for the town") · "Hear them out, and say sorry in front of everyone": gathering to hear it, A speaks ("X's reckoning").

**Rimfolk_Events_Hospital**

- **a patient for the hospital** (`Council_Evt_HospitalPatient`): "A sees to X (1 medicine)": A spends time with X.

**Rimfolk_Events_Mods_PokeWorld**

- **a rare Pokémon sighted** (`Council_Evt_PW_RareSighting`): "Everyone out to see it": a walk together to X and back ("a look at the rare one") · "A goes after it": A spends time with X.

**Rimfolk_Events_VendettaEnds**

- **the blood price** (`Council_Evt_VendettaRansom`): "Take the blood price": gathering to hear it, A speaks ("the blood price for the dead").
- **an apology** (`Council_Evt_VendettaApology`): "A hears them, and accepts": A spends time with X · "Let them say it in front of everyone": gathering to hear it, A speaks ("their apology for the dead").

**World events**, by file (a beat's answers and its endings, A the colonist a beat names, X its rider or visitor):

**Council_WS_Hardship**

- **plague in the region** (`Council_WS_Plague`): "Send n medicine": medicine carried out to the gate ("the medicine for the town") · "Sell it to them": medicine carried out to the gate ("the medicine for the town") · "Feed them and send them on": food carried out to the gate ("food for the family from the town") · "Help them (n medicine)": medicine carried out to the gate ("the medicine and the notes for the town").
- **famine** (`Council_WS_Famine`): "Send n food": food carried out to the gate ("the food for the town") · "Sell it at a fair price": food carried out to the gate ("the food for the town") · "Sell it dear": food carried out to the gate ("the food for the town") · "Feed them and send them on": food carried out to the gate ("food for the hungry on the road").
- **hard winter** (`Council_WS_HardWinter`): "Send n wood": wood carried out to the gate ("the wood for the town") · "Sell it to them": wood carried out to the gate ("the wood for the town").
- **locust swarm** (`Council_WS_Locusts`): "Light smudge fires (n wood)": a vigil at the fields ("the watch over the smudge fires") · "Harvest what's ripe": a work party up to 5 (harvest) ("the early harvest") · "Both": a work party up to 5 (harvest) ("the early harvest").
- **flood downriver** (`Council_WS_Flood`): "Feed them and send them on": food carried out to the gate ("food for the family from the town") · "Send n wood": wood carried out to the gate ("the timber for the town") · "Sell it to them": wood carried out to the gate ("the timber for the town").

**Council_WS_Land**

- **a famous healer** (`Council_WS_Healer`): "Invite X (n silver)": silver carried out to the gate ("the healer's fee").

**Council_WS_Mods_Factions**

- **the Empire's levy** (`Council_WS_ImperialLevy`): "Pay in full (n silver)": silver carried out to the gate ("the levy") · "Pay half, with apologies": silver carried out to the gate ("half the levy").
- **the republic votes** (`Council_WS_RepublicVotes`): "Back the reformers (n silver)": silver carried out to the gate ("the silver for the reformers") · "Back the old families (n silver)": silver carried out to the gate ("the silver for the old families").

**Council_WS_Places**

- **a new town** (`Council_WS_NewTown`): "Send a gift (n silver)": silver carried out to the gate ("the gift for the town").
- **a town abandoned** (`Council_WS_Abandoned`): "Feed them and send them on": food carried out to the gate ("food for the family from the town").
- **a trading season** (`Council_WS_TradeBoom`): "Set up a rest stop (60 wood)": wood carried out to the gate ("the wood for the rest stop").
- **a great pilgrimage** (`Council_WS_Pilgrimage`): "Welcome them (n food)": food carried out to the gate ("the food for the pilgrims").

**Council_WS_Powers**

- **a new leader** (`Council_WS_Succession`): "Send a gift (n silver)": silver carried out to the gate ("the gift for the funeral").
- **sellswords** (`Council_WS_Mercenaries`): "Hire them (n silver)": silver carried out to X ("the company's pay") · "Feed the company (30 food)": food carried out to X ("food for the company").
- **bandit road** (`Council_WS_BanditRoad`): "Pay for guards (n silver)": silver carried out to the gate ("the silver for the guards").

**Council_WS_Region**

- **a long drought** (`Council_WS_Drought`): "Pay for a well-digger (n silver)": silver carried out to the gate ("the well-digger's silver") · "Send n food": food carried out to the gate ("the food for the town") · "Sell it at a fair price": food carried out to the gate ("the food for the town").
- **a new road** (`Council_WS_NewRoad`): "Give it, as the colony's share": wood carried out to the gate ("the timber for the road").
- **a holy year** (`Council_WS_HolyYear`): "Feed them (n food)": food carried out to the gate ("the alms for the pilgrims") · "Let A keep it": a vigil at the gate ("A's day of silence").
- **a bandit lord falls** (`Council_WS_BanditLordFalls`): "Send a cart for the tables (20 food)": food carried out to the gate ("the cart for the tables").
- **a sickness in the herds** (`Council_WS_Murrain`): "Send n food": food carried out to the gate ("the food for the town") · "Sell it at a fair price": food carried out to the gate ("the food for the town").

**Council_WS_Warlord**

- **a warlord rises** (`Council_WS_Warlord`): "Pay X off (n silver)": silver carried out to the gate ("the silver for the warlord") · "Send silver to his enemies (n silver)": silver carried out to the gate ("the silver for his enemies").

**Rimfolk_WS_Beyond**

- **great fire** (`Council_WS_GreatFire`): "Send n wood": wood carried out to the gate ("the wood for the town") · "Sell it at a fair price": wood carried out to the gate ("the wood for the town") · "Feed them and send them home": food carried out to the gate ("a sack of food for the road home").
- **blood feud** (`Council_WS_Feud`): "Send him on, with food": food carried out to the gate ("bread for the road") · "Pay the last n silver": silver carried out to the gate ("the last of the blood price").
- **comet** (`Council_WS_Comet`): "A night watch on the hill": bonfire ("the night watch for the comet") · "Send A to look": A goes for a long walk.
- **bumper harvest** (`Council_WS_Bumper`): "Hold a harvest supper": feast from the stores ("the harvest supper").
- **drove season** (`Council_WS_Drovers`): "Graze them, as friends": bonfire ("the drovers' evening").

**Rimfolk_WS_Beyond2**

- **a people in mourning** (`Council_WS_Mourning`): "Send n silver for the stone": silver carried out to the gate ("the silver for the stone") · "Stand by the road with them": a toast ("standing by the road for the dead").
- **season of songs** (`Council_WS_Songs`): "Send the singer a gift, for the next verse": silver carried out to the gate ("the purse for the singer") · "Ask the singer in to sing (feed them well)": bonfire with music ("the singer's evening").
- **toll on the roads** (`Council_WS_Toll`): "Pay n silver": silver carried out to the gate ("the toll").
- **exiles on the roads** (`Council_WS_Exiles`): "Feed them and send them on": food carried out to the gate ("food for the exiles").

**Rimfolk_WS_FollowUps**

- **rebuilding** (`Council_WS_Rebuilding`): "Send n wood": wood carried out to the gate ("the timber for the town") · "Sell it to them": wood carried out to the gate ("the timber for the town") · "Feed them on their way": food carried out to the gate ("the food set out at the gate").

**Rimfolk_WS_Mods_Peoples**

- **a lord's progress** (`Council_WS_LordsProgress`): "Host the retinue for a night (n silver)": a work party up to 4 (clean, haul) ("the sweeping of the yard") · "Send a gift to meet them on the road": silver carried out to the gate ("the gift for the lord").
- **the clans gather** (`Council_WS_ClansGather`): "Pay the token (n silver)": silver carried out to the gate ("the token for the gathering").

### What stays a mood, and why

Outcomes that describe something people do but can't sensibly become one afternoon on the map stay as they were:

- **It's a no.** Most of what the audit flags and leaves are refusals and rulings that mention people at their
  ordinary days: "No dance. A hummed the tune under their breath all through supper." Nothing is set going for a no;
  the small thing someone does alone after it is told, not played.
- **It takes days or weeks.** "After a week of shared shifts", "hauling and digging from dawn to dusk", "one supper a
  week", "supper in two sittings" (the thief, the old face, the pirate's due, the crowded table). One gathering can't
  stand for a habit. Where the answer starts one (the feud's shared shift, the slacker's first shift, the inseparable
  pair's first night watch, the cover-up's and the plea's punishment, the blood past's penance), the first of it plays
  out as a work party or a vigil and the moods stand for the rest; work and priorities stay the player's to set.
- **It happens off the map.** Missions (the neighbor's funeral, wedding, shrine, rebuilding: they take place in the
  neighbor's town), the ice fair down the river, the far market and harvest fair, riding back with the feuding
  families, a rider sent with a letter, two days in the hills with the cartographer (who leaves).
- **It's a stranger's part, or a guest's.** Guests, prisoners and lodgers are never called to anything: the rude guest
  apologizing at supper, the pilgrims and envoys fed, the friend at the gate given the colony's table, the sanctuary
  seeker's bowl, the old acquaintance telling the dining room, the camp's people chopping wood, the healer treating
  someone. Where a colonist goes to the stranger instead (to listen, to learn, to hand over a gift), that plays out.
- **It's tomorrow.** Sleeping late tomorrow after the white night: tomorrow's timetable isn't today's to change. A
  vigil does go into the night, but nobody is kept from sleep: tiredness ends their part.
- **It would hurt someone or take the player's place.** Duels, bouts and fights (where an answer has a fight it was
  already real and stays as it is), executions, burning the enemy dead, searching a room, burning a stash,
  confiscating things, moving a patient to another bed, a quarantine: the colony's bills, beds and care are the
  player's and the game's to order. Burying the enemy dead is a work party carrying the dead, to the graves and
  dumping spots the player set.
- **It isn't a meal from the stores.** Mushrooms fried in the pan, berries eaten from the hand, the forager's tea,
  extra rations for one colonist, the tonic's long sleep (already its well-rested effect): a feast of the colony's
  usual meals would say something else. Where the answer sends someone to pick them, the picking is a search.
- **It's an animal's part.** A last walk with a pet (the walk is real, the pet is let go as before), a pet penned
  with its person at the fence, feeding a wild animal past the wall: animals aren't led about by answers.
- **It was already real.** Contests and rematches (the match happens on the map), taming an animal, a stranger
  joining, recruiting, sending away, mentorships, the meeting itself. Gifts of silver, food or medicine with nobody
  on the map to take them are fetched from the stores by a courier (below); where a visitor waits, they are carried
  out to them.
- **Other systems.** World events play out now (their answers' gifts and payments are carried to the gate, the
  locusts' harvest and watch are real). Personal stories, social events, dreams and encounters are moments that have
  already happened (interactions and moods), not answers to carry out.

## Pace: how often the colony gathers

The staging review (`docs/review-2026-11/staged-scenes.md`) counted about four gatherings a week and a group outing
more, four in ten within a day of the one before, eight or more in the busiest weeks, against the base game's own party
about once in forty days. The player chose a weekly budget (decision 1 of 6 November 2026), built as one count of
gatherings per colony, as letters and messages have theirs (`PlayOut/Stage.cs`; the rules pure in `StageMath.cs`,
tested in `StageTests` and run by `Tools/Balance --scenes`):

- **Counted.** Every gathering on a home, whoever called it: each gathering lord seen as it starts (the game's parties,
  rituals and weddings, Rimfolk's play-outs, the council meeting, the election's gatherings:
  `LordJob_Joinable_Gathering`, `LordJob_Joinable_MarriageCeremony`; a feast with guests once, as they come, not again
  for each people's group at the gate, `LordJob_FeastGuests`, or for its feast), and every outing as it
  starts (work parties, walks, vigils, searches, hunts, the dead carried out; not an errand or a gift carried by one or
  two). Lords already on at a load were counted before it. The count is saved with the play-outs (`PlayOutTracker.stage`,
  one `StageColony` a home: the start of each gathering of the last ten days, the last of any kind, the season's
  custom, the toasts waiting), so a load keeps the week; an older save starts each home's count from the load.
- **What the player chose always happens, and counts**: an answer that gathers people, a calendar day kept, a vote, a
  wedding, a decree, a meeting, an evening of faith, an ambition's feast.
- **The colony's own wait for room** (`PlayOut.routine`): a group's evening called by its anchor, a family supper, a
  child's birthday toast. Each comes only while the last seven days hold fewer gatherings than the week allows (two, the
  setting "Gatherings a week before the colony's own wait" on the People page under How often; the player's choices
  count) and none began in the last day and a half (`StageMath.Room`). It waits evening after evening, up to three days
  (`StageMath.RoutineDeadline`), and is then let go without a word (no line, no moods: it never happened). A camp's show
  or song at its fire is offered only on an evening with room, since its letter says tonight.
- **Calendar days.** Days that come round within three days of each other this year are kept together, as one: the
  first one's letter names them all ("The harvest and the victory over Hive"), its gathering is theirs, each takes its
  step toward a tradition, and they are kept or let pass together (`CalendarKeeping.KeptWith`; only days kept with a
  feast, games or a fire, never one's own day, a remembrance, the first snow or the founding day its event keeps). A day
  that gathers people (a feast, games, a fire, a faith's evening, a quiet hour at a grave) within two days of another
  gathering, or with one the council chose on its way, is kept at supper instead (`StageMath.AtSupper`): its letter says
  so ("Keep it at supper"), and that evening, when the keeper sits down with another of the colony (`Together`, supper
  hours), they say a word over the meal, the day's good memory comes to those at home (to those who knew them, for a
  remembrance), and the evening's news says it was kept at supper. Before that moment nothing is given.
- **One seasonal custom a season** (a dilemma with seasons that can become a tradition, `Dilemmas.IsSeasonalCustom`;
  `StageColony.customSeason`, a season being fifteen days of the year). A season that holds a custom's day on the
  calendar is asked no other (the festive year, below); a tradition still comes round when its time does, and counts as
  the season's. Whether a custom should also stay two days clear of other gatherings was put to the player, who said no
  (6 November 2026): there is no such rule.
- **A life goal's toast is raised at the next gathering**, not on an evening of its own (`Stage.Cup`): the one who thinks
  best of them has a cup to raise there, the two are asked to it, its first message says so, and a while in the cups go
  up with the toast's words; their moods come when it's over. With no gathering within two days, it is raised at supper
  when the two are together in the evening; past the colony's own wait, it is let go. A gathering that falls through
  hands its toasts on to the next.
- **The hustings join the count**: no evening of their own; at the count the candidates speak first, each answered by a
  cheer or a heckle, and then the votes are read (`Election.speakAtCount`). Their words no longer move the vote, cast by
  day; an election under way at a load with its hustings on keeps them.
- **A floor**: ten days never pass without a gathering (where the quiet-days setback starts, `SetbackMath.QuietDays`).
  From the seventh day without one the colony's own come at once whatever the week holds; from the eighth, with nothing
  of its own on the way, the colony keeps an evening together itself ("an evening together", planned in the afternoon,
  at its fire, the rec room or the table, led by its most sociable; the game's own party with answers not playing out).
- **Collisions and fires** (bugs the report found): see the play-outs' safety rules above (the player's plan takes the
  next evening once; a fire laid ahead, or let go near the end).

The dev menu's "Pace: the week's gatherings" logs each home's count, the last gathering and whether there's room.

Measured (`Tools/Balance --scenes`, a colony of ten, 300 colony-years each way of playing; "As built" uses
`StageMath.cs` itself): with average play, gatherings and outings 4.7 a week before and 2.7 after (the review's model of
the proposal said 2.6); any seven days median 3 (from 4 to 5), the busiest 9 (from 14), days with two or more 2.2 a year
(from 8 or 9), under a day apart 24% (from 46%), the longest gap 9 days, floor evenings 0.2 a year. As their own scene:
calendar days 42%, a group's evenings 32%, family suppers 34%, customs 88% (the model asks two to three a year; the
player's game asked about seven in sixty days, of which four a year stay), toasts none (raised at the next gathering),
elections' three gatherings 60%; meetings, decrees, answers, weddings and faith all of them. Well run 2.8 a week,
neglectful 2.7.

## One festive year (the player's decision 7, 6 November 2026)

The reviews found two festival calendars with two rules for a tradition: the calendar's days (kept three years
running, `CalendarMath`) and the council's seasonal customs (the midsummer fire, the swim, the harvest dance, the
longest night's supper: a council question, a tradition once its answer was chosen twice, `DilemmaDef.traditionAfter`),
on two pages, nothing keeping a custom from landing beside a calendar day; and feasts set going five ways and cooked
two. The player chose one rule, three years running. Built as one festive year (phase 5 of the review):

- **Customs on the calendar** (`Calendar/CalendarCustoms.cs`, a part of `ColonyCalendar`). A custom (a dilemma with a
  `traditionOption`) goes on the colony calendar the first time the colony keeps it (`AddCustom`, from
  `DilemmaTracker.Tradition`): a day of `CalendarKind.Custom` and `DayWay.Custom`, its question named by `about`, its
  name the custom's (`traditionLabel`). A seasonal custom keeps the date it was kept on: each year its question comes
  round on that day (`AskCustoms` in the calendar's hour, through `DilemmaTracker.AskCustom`, the council's own letter
  with its own answers), or within five days when its weather or someone to ask for it isn't there
  (`CalendarMath.CustomDue`, `CustomGraceDays`: a swim waits for a warm day); past those its year is spent with no blame
  ("came and went this year", `MarkCustomMissed`). A custom asked in its moment (the planting day as the fields open,
  the herd brought home, the victory feast, telling the bees) is on the calendar "with its occasion" (`CalendarDay.moment`,
  not `Dated`) and comes as it always did. Customs never make way for a day the colony writes down (`Fixed`, not `Auto`)
  and don't count toward its twelve.
- **One season, one custom.** A dated custom on the calendar is never asked as one of the council's ordinary questions
  (`BringsCustom`), and a season that holds a custom's day is asked no other seasonal custom (`CustomSeasonHeld`, the
  fifteen days of `StageMath.SeasonOf`); asked, the custom's day marks the season's custom as any custom does
  (`Stage.CustomAsked`). With the calendar off, customs are asked in their season as before.
- **One tradition rule** (`CalendarMath`, for days and customs alike). Kept three years running (`TraditionAfter`), a
  day or a custom is a tradition. A custom steps once a year (`KeepInYear`: one that comes with its occasion may come
  twice, and the second keeping is no second year); let pass before it is a tradition, its years start again and it
  leaves the calendar, a question again in its season (`Pass`, `OffCalendar`, `LetCustomGo`, a line in the news); a
  tradition let pass costs as it always did ("a tradition broken" for everyone but the ascetic, -2 trust each) and
  lapses the second time in a row, leaving the calendar. The tags a custom's lines read ([again], [years],
  [tradition], [missed], `CustomYearsMath`) come from its day. The answer's sheet says "Toward a tradition: chosen 1 of
  the 3 times it takes", and for a custom on the calendar not yet a tradition, that another answer takes it off.
- **What reads a custom.** `DilemmaTracker.IsTradition`, `TraditionCount`, `Traditions` and `AddCustomTags` read the
  calendar (`CustomDay`); the forced traditions of the council's hour skip those the calendar brings on their day. The
  calendar's traditions (`ColonyCalendar.Traditions`) now hold the customs too, for the founding letter, the keeper of
  days' promise and the colony's ways; a custom kept goes on the record of days kept (`Council_DaysKept`) of the keeper
  of days at home, else of the one the question was about (else a grown colonist of the colony's own), as a calendar
  day's keeper's does, for the seat's work and the promises judged by days kept. The Calendar page shows each custom
  among the days, soonest first: its date (or "with its occasion"), when it next comes, a "custom" pill, its years kept
  or its tradition, and where it came from ("A custom the council first kept in 5502... Kept by answering 'Make a day
  of the planting'."); its way and date can't
  be changed, and taking it off loses its years (kept again, it comes back). The page's separate "Seasonal customs" list
  is gone. The days that come with an occasion are left out of what counts dates (This week's coming days, the advisor's
  day tomorrow, talk of tomorrow, an ambition's days ahead: `CalendarDay.Dated`).
- **Older saves** (`DilemmaTracker.MoveCustoms`, once, the first hour; `customsMoved`, saved): every custom the
  council counted goes on the calendar, dated the last time it was asked (that year spent), with
  `CalendarMath.CarryCustom`: a custom already a tradition stays one (its break, if any, kept); one counted under the
  old two-year rule keeps its count and needs the third year. The tracker's own lists are emptied.
- **One feast engine** (`PlayOut/PlayFeast.cs`). Every feast is set going by `PlayFeast.Stage(FeastOrder)`, the only
  place a play-out of the kind Feast is made: an answer's (`DilemmaEffect_Feast.Create`, with the answer's own way of
  naming who comes), a calendar day kept with a feast (`CalendarKeeping.FeastDay`, the keeper of days as host, the
  grieving sitting it out, "a feast day" held for those who sit down), a feast with guests from the neighbours and the
  steward's suggestion (`Feasts.StartFeast`, guests "feast"), a planned wedding's feast and a family's supper. On the map
  it is one gathering: at the table unless told otherwise, the raw food an answer spent cooked first (`PlayCook`), a meal
  from the stores served to each who sits down (`LordJob_PlayOut.Serve`), the guests at their places (`PlayGuests`), and
  a while in the host raises a cup and the table and its guests raise theirs (`GatheringTouches`, `PlayOut.toast` and
  `toastLine`, saved), with the reason's words ("To the harvest, and to everyone here to keep it.", "To our guests, and
  the road that brought them here.", the wedding's "To Alo and Penguin, and many years.", else a toast's own line); a
  family's supper raises none. The reason's moods come when it's over to those who were there. What differs by the
  reason stays with its caller: the contests, the champion and the guests' welcome (`Feasts`), the day's letter and
  tradition, the answer's outcome, and what happens when no feast can be set going (the game's party and a better
  supper for a day, the guests served by the hosting rules).

Tests: `FestiveYearTests` (a custom's day and grace days, a step once a year, three years running, off the calendar,
an older save's counts, and in the source one rule and one feast engine), `CustomYearsTests` (the custom's lines can
come true under the one rule). Dev menu: "Calendar: a day comes round now..." asks a custom's question.

Measured (`Tools/Balance --scenes`, 800 colony-years each way of playing, a colony of ten; the model now holds the two
seasonal customs a settled colony keeps by its third year, `Rates.SceneKeptCustoms`, which before came as the council's
traditions did, the first hour of their season, and now come on their day): gatherings and outings staged as their own
scene a year, floor evenings included, well run 24.3 before and 24.3 after, average 23.8 and 23.8, neglectful 23.7 and
23.6 (2.8 a week); every gathering before the pace 42.6, 38.7 and 35.9 before, 42.2, 38.1 and 35.7 after. Customs asked
3.9, 3.2 and 2.3 a year before (a tradition and the season's own question could fall in one season), 3.0, 2.4 and 1.7
after, nine in ten of them their own scene. Without the kept customs (the model before this phase) the colony staged
24.2, 23.6 and 22.9.

## The council and the colony's year, fourth round (the player's request, 6 November 2026)

"Do another big content update to existing mechanics": this part the council and the colony's year, in the frameworks
already there (the council's draw, the customs' one-a-season rule, the decrees' hour and day, the advisor's hour). The
pure rules are in `Dilemmas/YearFourMath.cs`, tested in `YearFourTests`.

- **Questions about the colony's own systems** (`Dilemmas/DilemmaWorkers_YearFour.cs`, `Rimfolk_Dilemmas_YearFour.xml`),
  in the usual rhythm (never timely, after a raid or seasonal), each only while the colony is as its letter says, three
  answers, the council's silence setting nothing going. None gathers the colony; three send a few out to work.
  - The empty field: an outdoor field (20 cells or more) under two fifths sown in its growing season, not paused by
    "By the weather". A few hands sow it that afternoon (a work party, Sow, near the fields), or it is given up (the
    game's zone deleted); or left to the growers.
  - What the field grows: medicine under a dose a colonist (and under eight), or cloth under 25 a colonist, with food for
    20 days, and healroot or cotton (devilstrand once researched) sowable there now. The food field set to it, or its far
    half made a field of its own for it (`Sowing.MakeZone`); or kept.
  - Snow on the paths: deep snow (0.5) over three tenths of the home area's open ground, a snow-clearing area of 20 cells
    or fewer, not above 5 °C. The game's snow-clearing area marked over all of it and a party out with shovels, or by the
    doors only (within four cells); or left to melt.
  - The unfinished work: eight blueprints and frames standing, two at least waiting on stuff the stores don't hold, four
    that can be paid for. A building party, or the unpaid blueprints cancelled (the game's own cancel); or left.
  - The trees by the wall: five grown wild trees (never a sacred tree, never in a zone) within seven cells of an outer wall
    of the colony's. All marked for the cutters, or the nearest half; or left. B is someone whose ideoligion holds trees
    sacred when there is one, and the answer says so.
  - Too many in the pens: six grazers, the cold that stops the grass within ten days (the advisor's `SeasonUtil.Winter`),
    hay and kibble lasting under three fifths of it. Enough marked for slaughter that it lasts (one to four, a third at
    most), or one, or none: as the herd's cull spares them (nobody's own, nothing bonded or named, never one carrying
    young), the spare males first, then the oldest, a male and a female of each kind always left.
  - What to teach it: a grown animal that could learn both to guard (Release, with Obedience) and to haul, set to learn
    neither. Its training set to either (`SetWantedRecursive`), the handlers teaching it as the game has them; or left.
  - Home from the road: some of the colony back from five days or more away within a day and a half (the calendar's
    homecoming look keeps the last one, unsaved, `ColonyCalendar.LastHome`). A day off for all of them (the calendar's
    rest timetable), or one tells someone who stayed the road for an hour (a visit); or back to work in the morning.
  - Doubts in the faith (Ideology): someone of the colony's ideoligion under 35% certain. Its moral guide, else its most
    devout speaker, sits with them an hour (8% surer, `Faiths.Steady`), or they sit alone at its altar an hour (5%, only
    while it has one here); or it's left to them.
  - The faith without a guide (Ideology): the moral guide's role empty and two of the faith could take it (grown, free,
    no other role, the role's requirements met). Either named (the game's own `Assign`), or nobody.
- **Customs of the autumn and the winter** (`Rimfolk_Dilemmas_Customs_YearFour.xml`), the seasons that had fewest (seven
  and six before, against ten each in spring and summer). Each is a seasonal custom like the rest: one a season, on the
  calendar once kept, a tradition three years running; the full keeping, a smaller one (silence), a refusal.
  - The woodpile day (fall, the stores short of the winter's wood as wood for the winter reckons it, `woodShort`): a work
    party of six for the woodpile (logs carried in, a tree or two felled where none lie about), cutting and hauling, the
    lazy minding it; or three for a morning. Never asked by someone whose ideoligion holds trees sacred.
  - The night of lights (fall and winter, graves of the colony's own): a light by each such grave at dusk until first
    light (`DilemmaEffect_Lanterns` "graves", `OnMapTimed.GraveCells`) and a short vigil among them; or the lights alone.
    Never asked by someone whose ideoligion despises fire.
  - The mending day (winter): a work party repairing, cleaning and hauling, and a lesson at the bench from the colony's
    best crafter to one of thirty or under, the lazy minding it; or three mending for a morning.
  - The winter tales (winter, someone with a love of talk): the best talker tells the year by the fire (words, with lines
    of its own); or one tale at supper.
  - Each smaller form pleases most of the colony (as lantern night's one lantern does) and each refusal disappoints some
    and satisfies the industrious or the thrifty, so no answer is plainly best (`dilemma_balance.py`: trade-offs).
- **Decrees that each mean one thing on the map** (`Rimfolk_Decrees_YearFour.xml`; the player's decision 13). Three of
  the land's year mark the game's own work each morning and the day they're declared (`DecreeDef.daily`, run by
  `DecreeWork.RunDaily` from the decrees' day): a hunting season (six days, two head of safe game, `DilemmaEffect_Hunt`),
  wood for the winter (eight days, up to six of the nearest wild trees outside the home area until the stores hold 150
  a colonist for a tribe or a village, 80 for any other colony, `DilemmaEffect_MarkTrees` with `forWood`) and foraging
  days (six days, up to twelve wild plants ripe for picking within 45 cells, never a drug, `DilemmaEffect_Forage`).
  Saving the power (`lightsOut`, Electricity researched; asked for when the batteries are under a third full, the new
  `DecreeMoment.PowerShort`): the colony's lamps (a light of 150 W or less with a switch) switched off from eleven to five
  in the decrees' hour, never in a room with a patient abed, and only those switched back on (`DecreeTracker.lampsOff`,
  saved; the switch's wanted state set with it, so nobody is sent to flick it). Every faith its evening (`everyFaith`,
  Ideology and more than one faith at home, as one faith for the colony is listed): declared, the largest faith that isn't
  the colony's holds an evening of faith; each morning those of other faiths trust the council a quarter to a half point
  more by their openness, the colony's own devout a quarter less; never in force with one faith for the colony
  (`DecreeMath.Apart`). Every age's form is in its words: the hunters' moon, a village's open season, a game licence, a
  cull; axes or saws; the berry days or a survey of the land's harvest; a tribe or a village banks its fires instead of
  saving the power. None calls a gathering of its own, and none hurts anyone of Rimfolk's own (a hunted animal may fight
  back as the game has it, and says so).
- **The advisor** (`Advisor/AdvisorSignals_YearFour.cs`, `Rimfolk_Advisor_YearFour.xml`), in its thin parts: hay short of
  the herd's winter (food), animals in a cold they can't bear (temperature), a generator with under half a day's fuel and
  none in store (power), four or more blueprints waiting on stuff the stores don't hold (work), five grown trees close
  outside the walls (defence; a button marks them, never for a colony whose faith holds trees sacred; the look is kept
  six hours a map), deep snow and nowhere set to clear (work; a button marks the paths) and, with Ideology, a faith without
  a moral guide when someone could take the role.
- **Rates**: no new schedule slot, no new setting; one saved list (`lampsOff`, modelled). The questions take a share of the
  council's draw (the balance model knows how often each can be asked, `Rates.WorkerGates`), the customs a share of the
  one-a-season rule's, the decrees only what the player declares. The numbers before and after are in the HANDOFF entry.

## Goods and people that come and go on the map

The player gave 250 corn to a village and later got silver back: the corn vanished from the stockpile and the silver
appeared by the trade spot. With **Goods and people come and go on the map** (Events & world, on by default) whatever
Rimfolk's events move across the colony's edge is carried by people on the map (Couriers/). Off, it's all instant.

- **One shared system.** `Couriers` is the API the effects and workers call: `Deliver`/`PaySilver`/`Hand`/`Loot`
  (things for the colony), `Collect`/`TakeFromStores`/`TakeFor`/`TakeFromStoresFor` (goods it gives, sells or pays),
  `Exchange`/`Sell`/`CollectOrder`/`CollectThings`/`CollectGoals` (both in one visit; a neighbour's cargo; a mission's
  order in the missions' own words for goods), `Fetch` (a prisoner sold, an animal given back), `Await` (an errand's
  courier at the gate), `BringHome`/`TellWhenThere` (a letter held until the goods are set down). Each returns false
  when nobody will come, and the caller does exactly what it did before, so no event changes when the setting is off.
- **A trip** (`CourierTrip`, kept by `CourierTracker`, a game component checked every 250 ticks at tick 59 of the
  cycle, only while there are trips) waits 3 to 9 hours (an errand's or an order's courier under an hour and a half),
  comes only by day (7:00 to 18:00) and only when the map is safe (no danger, no toxic fallout, -25 to 45 degrees),
  and gives up after three days. Its people walk in from an edge cell that can reach the gate (`PlaySpots.GateCell`,
  else `DropCellFinder.TradeDropSpot`) as a `LordJob_Courier`: travel, then work at the gate (`Council_CourierWork`
  duty: `JobGiver_CourierWork` sets down what they carry with `JobDriver_CourierDrop`, fetches from the stores with
  `JobDriver_CourierLoad`, takes someone along with `JobDriver_CourierFetch`, else they wander within three cells),
  then exit. Harmed by the colony or their people turned hostile, they leave defending themselves. A pack animal waits
  by the gate (`WanderClose`).
- **Who and how many** (`CourierMath`, pure and tested): the event's own people if usable, else the nearest of a
  fitting tech (a pre-industrial colony: tribes and medieval folk; otherwise a step either side), always on foot.
  People come from their trader group's lighter guards (combat power 75 or less, never a trader), else the faction's
  ordinary member (`Arrivals.PersonKind`); a pack animal from the trader group's carriers that the biome allows and
  the season suits. One person per 35 "bulk" (kilograms plus 10 per stack), at most three; an animal when their
  caravans use them and the load is more than one person's, which spares one porter. Their own inventory is cleared
  when they're made, so everything they carry is the trip's; the load is shared by carrying capacity.
- **Robustness.** Every end is a `CourierEnd`, settled once (`settled`): goods still owed to the colony are set down
  (at the courier's feet if the colony can reach it, else where traders leave goods) unless the colony hurt them or
  the map is gone; goods owed by it leave the stores the old way unless the colony hurt them, their people turned
  hostile or the map is gone. Can't reach the stores: six hours at the gate, then settled. Goods used up meanwhile:
  they take what there is and the deal stands. Someone they came for who is gone: they don't pay. Exiting couriers'
  packs are cleared (`Notify_PawnLost`, and every pass for anyone already gone). A trip whose record is gone (mod
  removed) sends its group home from the lord's own toil. The setting turned off mid-trip: settled at once, and they go.
- **Merging.** Effects of one answer that go to the same people the same tick are one visit: paying for goods bought
  (`WorldEffect_Pay` + `DilemmaEffect_Spawn`) is an exchange; the silver paid for an errand's goods rides with the
  courier who waits at the gate for them.
- **Visitors in person.** An event's own visitor at the colony hands things over at once (at their feet, with a line);
  one still walking in carries it and sets it down when they arrive (a bearer trip: the goods stay in the trip until
  then, so the visitor's own pack is never touched). Goods for a visitor who is here go by errand (a colonist carries
  them out, PlayOut/), never by a courier.
- **Which effects.** `<by>Outsiders</by>` marks the uses of `DilemmaEffect_Consume` (75), `DilemmaEffect_Spawn` (9),
  `DilemmaEffect_Loot` (9), `WorldEffect_Pay` (4) and `WorldEffect_Medicine` (1) where the goods cross the colony's
  edge; the rest are used up or found at home (a feast, fuel, medicine for a patient, a find). `WorldEffect_Loot`
  defaults to outsiders and its four finds say `<by>Home</by>`. `WorldEffect_Silver`, `_Sell`, the owed payout,
  `DilemmaEffect_Gift`, `_Goods` and `_SellHeirloom` always come from outsiders. Pods stay pods in an industrial colony.
- **Speech** goes through `Speech.Say` (the pool `Rimfolk_CourierLines`, by moment, goods and their people's era);
  messages through `Notify` with LookTargets. **Saved:** at most `CourierMath.MaxTrips` (12) trips at once (the oldest
  waiting is settled when more come), each with up to three people, a pack animal and the things it brings in a
  `ThingOwner`; ended trips are dropped once their people are gone, two days at most.

### Visitors on the map

The people a letter speaks of, there in person (Visitors/), under the same setting as the couriers. Three generic
effects, wired from defs: `VisitEffect_AtGate` (in a beat's or an event's effects, which run just before the letter:
the rider, driver, envoy or family walks in with it and waits at the gate), `VisitEffect_Stay` (in an answer: those
waiting stay N nights, by the gate or moved to the far ground or the road) and `VisitEffect_Come` (a group comes in
its hours: mourners or a band passing to the far edge, a herd grazing the far meadow or drinking at the water for a
night, a lord's retinue staying by the gate).

- **One record, one group.** A `VisitorTrip` (kept by `VisitorTracker`, a game component checked every 250 ticks at
  tick 131 of the cycle, only while there are visits) is made when the effect runs; an AtGate group sets out at once
  if the map is safe and has room, else the letter alone tells it. The people (the people's ordinary member, or a
  caravan guard for an armed group; dressed for the weather; a family related and sharing a name; a robbed merchant
  stripped to the shirt) and animals (a pack beast for peoples of the old ways, the family's dog or goat, a herd of
  farm beasts that suit the weather, likelier those wild on this land) walk in as one `LordJob_RimfolkVisit`: travel
  to the spot, keep close to it (`Rimfolk_VisitWait` for people, `Rimfolk_VisitBeasts` for animals: the game's own
  `JobGiver_GetRest` within the flag radius, then wander near; never its food search, so nobody eats the colony's
  meals or crops: `Visitors.Feed` has the hungry eat from their own packs, or keeps them fed out of sight), then walk
  off, by the nearest edge or on to the far side (`VisitorMath.Opposite`). Hurt or turned hostile: they leave defending themselves. Danger: they leave at a jog.
  Three and a half days at the spot, or the record gone: they leave whatever happens.
- **The letter.** The effect keeps the letter's context (not saved) and finds its letter by it on the next frame, so
  an answer given at once is still theirs; after a reload, by the letter whose X is one of them. The first of them is
  the event's X when it has none, so `Couriers.TakeFor`, `WorldEffect_Silver` and `DilemmaEffect_Spawn` hand things
  over in person and an answer's errand (`DilemmaEffect_CarryOut` without `to` Gate) carries what the colony sends to
  them; where a cartload should bring porters, the rider is not X (`stranger` false) and the gate courier still comes.
  If they go before the answer, the letter's X is cleared and it all goes the couriers' way. `WorldEffect_Join` takes
  in the family waiting for the same letter (`Visitors.TakeIn`), with their animal, instead of making new people.
- **When they go** (`VisitorMath`, pure and tested): someone at the gate half an hour after the answer, or once an
  errand bringing them something has reached them (eight hours at most), or after two and a half days whatever
  happens; with nothing asked, after their hours. Guests at first light after their nights (`HoursToDawn`, at most
  three), by the gate if every one of them is safe in the temperature there with a margin for the night, else in a
  corner of the colony's dining or rec room that is (`Visitors.IndoorsCell`, `Rimfolk_VisitIndoors`: they keep within
  four cells of it), else they go on. Anyone hypothermic,
  heatstruck, starving or malnourished: they all go, with a message. A held beat (`holdBeat`: "the retinue stayed the
  night") waits until they've gone.
- **Limits.** At most three groups on a map at once (`MaxHere`), six people and eight animals in a group, eight
  records kept (`MaxTrips`; the oldest still waiting is dropped). Ended records go once their people are gone, a day at
  most. What they were handed is cleared when they leave the map. Speech through `Speech.Say` (the pool
  `Rimfolk_VisitorLines`, by moment and what they are); messages through `Notify` only for a group coming along the
  road and for one the weather drove off.

### Neighbours' requests and callers at the gate (the player's request, 6 November 2026)

"Do another big content update to existing mechanics": this part the neighbours and the people who come to the colony,
in the frameworks that were there (missions, data events, letter variants), at the rates there were. The pure rules in
`Missions/NeighboursFourMath.cs` (`NeighboursFourTests`), the workers in `Missions/Workers_MissionNeighbours4.cs`, the
texts in `Defs/MissionDefs/Rimfolk_Missions_Neighbours4*.xml`, `Defs/IncidentDefs/Rimfolk_Events_Neighbours4*.xml`.

- **One draw for every mission.** Every mission offer's base chance is scaled so the missions together weigh what the
  thirteen kinds offered weighed before this round (`MissionPoolWeight`, 6.8; `MissionPool.Factor` in the storyteller's
  draw and Rimfolk's own, `Happenings.cs`; the balance model the same). A new kind takes a share; one mission at a time
  as always.
- **Hands lent to a neighbour's town** (`HappeningWorker_MissionHands`, data `MissionHands`): the harvest hands' way for
  any skill. One or two of the colony's people good at it (a level, the work not disabled), the colony's caravan at the
  people's nearest town within three days, staying there `hours`; the whole stay earns the silver into the packs, a gift
  of things of the colony's era (`Loot`) and the skill's experience; half the stay half the silver, less goodwill lost.
  Builders after a storm or a fire (Construction 5, two, 36 hours), a healer for their sickhouse (Medicine 6 and
  doctoring, one, 48), a teacher for their young (Intellectual 7, one, 36), a witness at a truce or a boundary (Social 6,
  one, 12), a smith before the harvest or the sowing, or a mechanic for a hightech town's pumps (Crafting 6, one, 24). Each
  is remembered as `lentHands` (a debt of honour where those are kept). Every one's forms are by the asking people's age
  (`their:tribal` to `their:spacer`, `their:lowtech`, `their:hightech`, added to every mission's tags) and the Empire's
  own lines keep its knights, stewards and heralds.
- **A store kept** (`HappeningWorker_MissionKeep`): their carts bring grain, timber, steel or cloth to the gate
  (`Couriers.Deliver`), and when the season is up come back for as much (`Couriers.CollectGoals`), paying a quarter of
  its worth (`KeepFee`). What the stores hold then counts, the colony's own the same as theirs, as the terms say. Short,
  the colony pays the rest in silver at its plain worth (`Shortfall`, their carts take it); without the silver they take
  what there is, and remember it.
- **A young beast raised** (`HappeningWorker_MissionBeast`): a foal (lowtech peoples), a calf, a kid or a pup of a kind
  that suits the weather and the land. Their drover walks it in with them to the gate (`Arrivals.Visit`), hands it over
  there (`SetFaction`) and goes; at the end of the season their drover comes for it (the runaway's `HandBack`, its
  `Couriers.Fetch`) and pays all of it kept well (no malnutrition of 0.2, nothing sick, no wound untended) or half kept
  poorly. Dead or lost, goodwill and the memory; bonded to one of the colony's, it stays and they are glad.
- **Someone of theirs lost on the land** (`HappeningWorker_MissionLost`, the lost child's worker with its ages, finder,
  deed and memory as virtual hooks): an old one who wandered off (66 to 86) and a hunter overdue (20 to 55), found by
  whoever comes within six steps in sight, then their kin come; remembered as `foundLost`.
- **A traveller seen home** (`HappeningWorker_MissionEscortWho`, the pilgrim's escort with its ages and variant as hooks):
  a trader with the season's silver past a bandit road, a healer bound for a sickhouse, a scholar with a chest of books.
- **Goods** in the delivery framework, data only: the timber and stone of a wall (a palisade, a wall, a watchtower with
  steel and components for an industrial colony), fodder for their herds (hay and kibble in the fall and winter, hay in a
  dry summer), stores for a long road (pemmican or meals and cloth for pilgrims, meals, cloth and timber for a trek,
  survival packs and medicine for an expedition), and seed after a blight or a flood (corn, rice, potatoes).
- **In the town head's name.** The hands, the store, the beast and every delivery name the town's head by their title
  when the region's ledger knows them (`InviteForms.Host`, `{HOST}` and the tag `host`), the Empire's title for the
  Empire. No line counts the days down: the Quests tab shows the time; endings say what is happening as it happens
  (their carts coming, their drover on the road).
- **Callers at the gate** (data events, `HappeningWorker_Setting`, people of about the colony's age): a herb-gatherer (a
  lesson at their side for the colony's grower, or herbal medicine bought), drovers on the road to a fair (a young beast
  bought, handed over beside them, or a meal carried out), a seed-seller in spring and summer (a patch sown by the grower,
  or a young tree planted at the field's edge), a neighbour's patrol after bandits (fed, a drill for the best shot, or
  medicine sold), a traveller on the wrong road (a meal and the roads gone over at the table), a petitioner with a
  grievance against their own head (heard out at the table), a brewer's cart (beer bought, or brewing secrets for the
  cook). Silence sends them on; nothing gathers the colony, nobody is hurt.
- **More ways to tell the callers** that had one (`LetterVariantsDef`): the barber, the animal doctor, the undertakers, a
  captive's kin and the art collector, by the season, the age and what they find, with an ending for each answer.

### The right-click's things, done by themselves (the player's request, 7 November 2026)

"I'm not really using the right click conversation options, so please integrate them to be done automatically by pawns
when there is a good moment, while still having the gameplay impact and limits in mind." The pure rules in
`Outsiders/OutsiderAutoMath.cs` (`OutsiderAutoTests`), the look in `Outsiders/OutsiderAuto.cs`.

- **The same way as the right-click.** A colonist who does one by themselves goes through `Outsiders.WhyNot` (the menu's
  own reasons: willing, the waits, patience, the thing's own conditions), never one `OutsiderTalk.Repeat` would answer
  with "you asked me that already", and the same jobs (`Outsiders.StartTalk` and `StartGift` with `byThemselves`), so the
  scene (`OutsiderTalk.Begin`: patience spent, the ask noted), the outcome (`Outsiders.Talked`) and the record
  (`Remember`: the pair's waits, the bond, a notable's regard) are the menu's. What differs: the job is not the player's
  order (`playerForced` false, `StartJob` rather than `TryTakeOrderedJob`), so the game may call them away, a turning away
  is said in bubbles only, the meal, lesson or bout that follows is theirs too, and the outcome's everyday messages stay
  unsaid (`Talked(..., byThemselves)`, `StillTold`: a new friend, sweethearts, lovers and someone who stays are still
  told; an injury's message is the bout's own). A letter handed over keeps its message: it was ordered.
- **Which things** (`OutsiderAutoMath.Tier`, the setting `outsiderAuto`: off, free ones only, within limits by default).
  Free: news of home, getting to know them, a good word at home, faith with the same faith (always closer). Within
  limits: a meal when the colonist's food is at 45% or less with meals to spare (the guest's portion counted against
  the budget), a drink at leisure as `Beliefs.MindsDrug` and a teetotaler allow, a gift that suits them (the first of
  their visit, from three times as much, never medicine, weapons, clothes, silver, gold or a hard drug, as much as the
  budget left buys and at least 20 silver), a lesson in a skill the colonist has a passion for, a practice bout between
  two keen on it (accepted at 50% or more, one a day, both clear of `Harm.May`'s cap), courting someone already a
  sweetheart at a 50% chance or more, neither with another partner. The player's alone: a chat (`OutsiderTalk`'s talks
  of their own are one), a letter (written in the player's words), something brought (one ask per people stands at a
  time and the thing is the player's pick), an invitation to stay, courting someone new, faith with another faith, hard
  words, a duel, and the diplomacy acts.
- **The moment.** Every fourth of the outsiders' 250-tick looks (`Sched.RareOutsiderTalk`, no slot of its own), on each
  home with `StoryDanger.None` and no `MapFacts.ThreatOn`: outsiders of another faction, grown, able to talk, not busy
  (`Outsiders.Busy`), leaving, travelling in or through (a travel duty or toil), trading, in a scene or already sought by
  one of the colony's jobs; colonists grown, able to talk, of no lord, idle, wandering or at a recreation job (or in
  their recreation time), free by `PlayOuts.CanJoin` and `Interruptible`, not `NeedsPress`, not one who began one in the
  last six hours. A moment is taken at 35% a look.
- **The pace** (`PaceAllows`): 5 a day across the colony, 2 a day with one outsider, two hours between any two, four with
  the same outsider, an hour after a talk of their own with them, and never their last patience (`PatienceKept`); a
  colonist who asked them something in the last six hours leaves the next ask to someone else, so it is never pushy. The
  stores: `Budget`, a four-hundredth of the home's wealth a day, 30 to 250 silver.
- **The choice** (`Worth`, `WhoScore`, `Score`): each allowed thing with each of the six nearest free colonists, by what
  it would bring now (a good word 6 and more, getting to know them 4 or 7 near friendship, courting 5, news 4 or 1 with
  their news heard lately, faith 3, a lesson 3 and the passion, a meal 4, a drink 3, a gift 2 or 4 with a cool people,
  a bout 3) twice over, and who suits it (Social, their regard and knowing, the speaker or steward with a notable or for
  a good word, the warden of guests, the chaplain for faith, the learner's passion, the fighter, the hungrier, less for
  the shy, the walk, whoever did the last one and each they did today). The best six are checked in full; the first
  that passes begins. Three outsiders are weighed a look at most, in a shuffled order.
- **Saved**: no new collection. The book's day, count, spending, last start and bout day (`OutsiderBook.autoDay` ...
  `autoBoutDay`), and on each visit the day's count and the last start (`OutsiderVisit.autos`, `autoDay`, `autoLast`).
  Who began one lately is the session's, as for the talks of their own.
- **No new Harmony patch, no new letter, no new line**: the words are the menu's own, in the forms of their people's age.

## Harm from everyday life

The player's harm rule (3 November 2026) lets Rimfolk hurt its people when it is flavourful or makes the game more
interesting, at the game's own rate, always from something on the map, and shown. The harm review
(`docs/harm-review-proposals.md`) proposed 94 ways for its systems to go badly; the player picked "my top ten, then more"
on 6 November 2026. Every one of them goes through one service, `Harm/Harm.cs` (pure part `Harm/HarmMath.cs`, tested in
`Tools/Tests/HarmTests.cs`), which keeps the review's shared rules. The first part built the service and the three hurts
of work (1.2.1, 1.2.2, 3.2.1, below); the other seven of the top ten are built on it.

### The shared rules

- **One setting, leaning with the storyteller.** "Hurts from everyday life" (How hard, `everydayHurts`, 0 to 200%,
  100 by default; the light-touch preset sets 50%) multiplies every rate. It leans with the storyteller's own numbers
  (`HarmMath.Lean`): the geometric mean of a threat term (0.35 at no threats, 1 at the usual 100%, up to 1.25 at twice
  it) and a disease term (one over the disease interval, between a half and 1.25; diseases off count as a half). A
  game with no threats and diseases half as often comes to about two fifths, the hardest about a quarter more. `Harm.Scale` is the two together.
- **The shared cap** (`HarmMath.Bar`, `Harm.May`): at most one Rimfolk-made hurt per colonist in three days (the last
  one's tick saved in `HarmBook.lastHurt`, by thingIDNumber, pruned past three days), and none while downed, in a sickbed
  or in need of one (`Harm.Sickbed`: in a medical bed or resting, bleeding, or ill with something that can kill; a small
  thing still to be tended, a strained back or last week's cut, is not a sickbed), pregnant, or already fighting (drafted, a social fight or a rage, at an
  attack, hurt or attacking within the hour). Babies never; children only `HurtClass.Mild` (a scrape, a cold), never an
  injury, a bout or a fight.
- **Nothing kills outright.** A wound lands only on a hand, an arm, a foot, a leg or the torso (a crack on one of their
  bones), never a vital part, the head or the neck, and never takes more than the part has left but one point
  (`HarmMath.SafeSeverity`). The game's own fights and illnesses still may, as they always could.
- **The game's own hurts.** A wound is the game's own injury (`Bruise`, `Cut`, `Burn`, `Crush`, `Crack`, `Scratch`), sized
  by the body's own damage factor (the tough take half), shown as the game shows a hit (the red flash, blood for a cut or a
  crush, a puff of smoke for a burn), then bled, tended, infected and scarred by the game as any wound. Ailments are
  Rimfolk's own (`Ailments/`); fights the game's social fight.
- **Named before the choice.** Where a choice brings the risk, its card says so in words, never numbers: a decree's card
  and letter carry `Harm.DecreeLine` (`DecreeDef.hurts`).
- **Each under its own system.** The drills' bouts need "Answers play out on the map"; working through it needs the
  everyday ailments; the setting at nothing stops all of them. No mood is added: being hurt brings the game's pain and the
  ailment's own mood, nothing on top.

### The service, for the next builders

`Harm` (static) in `Harm/Harm.cs`:

| Call | What it does |
|---|---|
| `Harm.On`, `Harm.Setting`, `Harm.Lean`, `Harm.Scale` | the setting above nothing; its value; the storyteller's lean; the two together |
| `Harm.PassOf(kind)`, `Harm.ScaleFor(kind)` | a kind's pass within the shared budget now (below); the scale times it, for a chance the game works out itself |
| `Harm.Chance(kind, baseChance)`, `Harm.Roll(kind, baseChance)` | a kind's base rate after the scale and its pass, and one roll of it (no cap) |
| `Harm.Roll(pawn, kind, baseChance, cls)` | the cap for that pawn and class, then the kind's roll |
| `Harm.May(pawn, cls)`, `Harm.Bar(pawn, cls)` | whether the cap lets them take a hurt of that class now, and if not why (`HurtBar`) |
| `Harm.Injure(pawn, wound, where, low, high, kind, cls)` | one of the game's wounds (`HurtKind`) on a part (`HurtWhere`), sized low to high, under the cap; returns it or null |
| `Harm.Ail(pawn, hediffDef, severity, kind, cls, replaces)` | a hediff (an ailment, the game's flu) under the cap, optionally in place of another; returns it or null |
| `Harm.Fight(a, b, kind)` | the game's social fight between two grown-ups the cap lets fight, recorded for both |
| `Harm.Note(pawn, kind)` | a hurt that came another way (a bout's hard blow, a brawl), counted for the cap (`kind` null: the cap only) |
| `Harm.SystemOn(system)`, `Harm.BudgetReport()` | whether a kind's system is on in this game; the budget for the dev tools |
| `Harm.DaysSince(pawn)`, `Harm.Pregnant(pawn)`, `Harm.Fighting(pawn, now)` | the cap's parts, for a caller's own checks |
| `Harm.DecreeLine(decreeDef)` | the decree's line for its card and letter, or null |

`HurtClass`: `Mild` (children may), `Injury`, `Bout`, `Fight` (grown-ups only). Every roll and every hurt names its
kind (`HarmKinds`, below). The caller rolls its own base rate
through `Harm.Roll`, gives the hurt through `Injure`, `Ail` or `Fight` (each checks the cap again and records it), says
it on the map (a bubble through `Speech.Say` or `CouncilGameComponent.Speak`, one sentence) and tells it through
`Tell.Message` at `Importance.Important` (harm is never held back) in two wordings or more (`TextVariants.Vary`). A hurt the caller can't give through the service (the outsiders' practice
bout, a feast's wrestling and sour night) is counted with `Harm.Note`; the feasts read `Harm.Scale` and `Harm.May` through
`Feasts.HarmScale` and `Feasts.MayBeHurt`.

`HarmBook` (a game component) holds the saved part (`lastHurt`; `drillDay`, a home's last evening drill; `kindLast`, the
budget's memory; `waiting`, parts 4 and 5's hurts still to come; `turning` and `calm`, the hunts' game) and runs the hour (Sched
`RareHarm` 67): one tenth of the colonists each 250 ticks, the first slice also the colony's part.

### The shared budget (7 November 2026)

The player asked whether more kinds of hurt make colonists hurt more often or only more variously, and chose: "yes, a
shared budget please". Rimfolk's everyday hurts, every one that goes through the service (the beasts' stampede, the
feasts', the schemes' and the pilgrims' included), together stay at what the kinds built before the budget came to, and
new kinds take turns within it instead of adding to it. The pure rules are `Harm/HarmBudgetMath.cs` (`HarmBudgetTests`);
the game's side is `Harm.PassOf`.

- **Each kind registered once** (`HarmKinds`): a key (saved), a label, the system it sits under (`Harm.SystemOn`: its
  setting, and for a soft integration its mod) and its base rate (hurts per colonist-year in an average colony of six at
  100% and the usual storyteller, coming on its own, before the cap: the balance model measures it). The 18 kinds built
  before the budget are its founding ones; harm part 4's and part 5's are not (the guest's night is two kinds, Rimfolk's
  visitors and Hospitality's guests, so a game without Hospitality keeps none of the budget for it).
- **The budget** is the founding kinds that are on, their base rates together (about 0.46 a colonist-year).
- **The pass**: a kind's chance is its base rate times the setting and the storyteller's lean (as before) times its
  pass, its variety times the budget over the kinds on weighed by their variety (`HarmBudgetMath.Passes`). So the kinds'
  base rates times their passes always come to the budget: with only the founding kinds on and none come lately, every
  pass is 1 (the hurts as they were); with all of part 4 on, each passes at about half its rate. No kind passes at more
  than twice its own rate.
- **Variety** (`HarmBudgetMath.Variety`): a kind that has just hurt someone weighs a quarter of its rate, back to all of
  it evenly over thirty days (two quadrums), so it passes less often for a while and the others a little more; the mix
  varies instead of the commonest kind coming again and again. Its memory is `HarmBook.kindLast` (a kind's last tick,
  pruned past thirty days: a handful of entries).
- **The same as before in everything else.** The pass knows nothing of the setting or the storyteller, so "Hurts from
  everyday life" and the lean scale every kind exactly as they did; the cap and its exclusions (downed, a sickbed,
  pregnant, fighting, babies never, children only the mildest) are untouched, and nothing here kills outright. A system
  switched off takes its kinds out of the budget, leaving the rest as they would be without it. Prevention still counts:
  a well-run colony meets fewer of the new kinds and its total falls a little below its old one, a neglectful one rises a
  little (the rates below).
- **One roll an occasion.** The pass multiplies the chance of the occasion's hurt, once: a hunt's head that can turn takes
  its turn when the hunt marks it (`HuntsBite.Marked`; not up, it stays calm under its hunters, `HarmBook.calm`), not on
  each shot, so the budget's share is the hunt's; the ailments of the land and the work, which never had the setting's
  scale, take the pass alone (`AilmentTracker.Lean`).
- **A later kind plugs in** with one line in `HarmKinds` (its key, label, system and base rate from the balance model,
  `founding` false, added to `All`), a case in `Harm.SystemOn` if its system is new, its rolls and hurts naming it, and its
  source in the balance model's harm mode (`Tools/Balance/HarmSim.cs`, `PartFourDay` shows the pattern). The budget shares
  itself among however many kinds are on (about 95 once the review is built: each then at about a fifth of its own rate).
  The model prints each kind's measured base rate beside the registry's, flagging any a quarter off.

### Drills that bruise (1.2.1, decrees)

With "Answers play out on the map" on, the drills decree (`hurts` Drills) pairs everyone of the colony who can fight into
practice bouts in the open each evening between five and eight (`DrillBouts.cs`): grown-ups only, free and not pressed
(`PlayOuts.CanJoin`, `Interruptible`, not `NeedsPress`), the yard or the open middle of the colony (`PlaySpots.OpenCell`),
two places side by side for each pair, clear of the others. The militia drills (`hurts` Muster) call one pair, three
evenings in four (`HarmMath.MusterEvening`). Not in bad weather or danger; once a day at a home (`HarmBook.drillDay`).
The first of the pairs calls it (`Rimfolk_Pool_DrillCall`, by age), each pair walks to its places (`JobDriver_DrillBout`,
the job `Rimfolk_DrillBout`), a word to begin (`Rimfolk_Pool_DrillBegin`) and six pulled swings each, the game's lunge and
sound, no harm. One bout in 25 (`HarmMath.DrillBlowChance`, the outsiders' sparring rate; a marshal in office halves it)
has one blow land for real, decided at the start on one of the pair the cap lets take a bout: a bruise or a cut of 6 to 9
by the colony's means (sticks and wrestling: a bruise; blunted blades and shields, the bayonet drill: a bruise or a cut on
an arm; stun batons and training rounds: a burn on the torso; `DrillBouts.BlowOf`). The hurt one says so
(`Rimfolk_Pool_DrillHit`), the other says sorry (`Rimfolk_Pool_DrillSorry`), the news tells it (`CC_Harm_DrillBlow`,
`CC_Harm_MusterBlow`, a NegativeHealthEvent), and the game's doctor tends it. A bout done with nobody hurt ends with a
word (`Rimfolk_Pool_DrillEnd`). Either one leaving ends it for both; danger ends it at once. The decree's hourly practice
goes on as before. About one real injury per five-day drills decree in a colony of ten, the militia about half that
(`HarmMath.DrillBlowsPerDecree`). Since the player's decision 13 (6 November 2026) the drills are one decree in the
colony's age's form: militia drills for a tribal or medieval colony, drills for an industrial or spacer one (see
"Decrees that mean distinct things"); each keeps its own bouts and risk lines.

### Tired hands (1.2.2, decrees)

Under long hours (any work), a harvest push (`hurtsAt` Field) or a building season (`hurtsAt` Heavy), each with `hurts`
TiredHands, the hourly look counts the hours each colonist is found at the work the decree drives (`HarmMath.WorkOf`: the
stove and the forge, the bench, building and mining, hauling, the field). From the sixth hour in a day, a long day, the first
hour they're found right at it (carrying the load, or standing at the bench, the stove, the wall or the plants rather
than walking to them, so the slip shows where the words put it) has one roll: one in 150 (`TiredHandsChance`, the everyday work ailments' rate), twice below 30% rest, once more for
a second work decree driving it, half for the tough, half in a spacer colony. A slip is one of the game's wounds of 3 to 6
(5 to 8 in a spacer colony) by the work and the colony's means (`HarmMath.WorkSlip`): a cut at the bench, a crushed hand
at an industrial colony's machine, a fabricator's burn; a burn at the stove, fire pit or forge; a crushed foot under a
stone or a crate (a hand at a machine); a load dropped on a foot; a sickle's cut. The job stops, a load drops at their
feet, a line is said (`Rimfolk_Pool_WorkSlip`, tagged by work and wound), the news tells it (`CC_Harm_Slip_<work>`), and
the hurt one goes to the doctor as the game sends anyone with a wound to tend. The review's "one or two per decree in a
colony of ten" was a miscount against its own anchor: at one in 150 long days, a five-day decree in a colony of ten brings
about a third of a slip when everyone sleeps, about twice that when nobody does (`TiredHandsPerDecree`). The anchored rate
was kept.

### Decrees that mean distinct things (the player's decision 13, 6 November 2026)

In the player's save drills and militia drills were declared the same day and both ran ("militia drills (2 times) ...
and drills" in the annals); the review named more overlapping pairs. Each was looked at for what it does
(`DecreeMath`, pure):

- **One idea, one decree, in the form of the moment.** Where two decrees are one idea, only the form it takes now is
  listed (`DecreeWork.FormNow`, `HiddenNow`): the drills by the colony's age (militia drills, the muster of a tribe or a
  village, for a tribal or medieval colony; drills, all day, for an industrial or spacer one), rationing by the moment
  (famine bread, smaller portions and a share for the hungry, while a famine or a drought lies over the region and the
  colony's own stores aren't low; plain rationing otherwise). A form in force stays itself until it ends, and the page
  shows it.
- **Overlapping pairs don't run together.** Festival days and a feast day (both celebrations that raise standards),
  quiet hours and days of rest (both rest and slower work), on guard and a curfew (sleep in shifts, or everyone abed by
  ten): with one in force the other can't be declared, and its card says why (`DecreeMath.Excludes`,
  `CC_Decree_Overlaps`, `DecreeTracker.CannotEnact`). An idea's two forms exclude each other the same way. One faith for
  the colony and every faith its evening (the fourth round, below) are the council's two ways with its faiths, and are
  kept apart the same way.
- **Kept apart.** Rest when ill and quarantine may run together: one heals the sick, the other keeps the well from
  catching it. Austerity (no new wishes) and rationing (food) never overlapped, nor shared meals (talk) and festival
  days (rest and music).
- **Older saves.** A decree in force that is an idea's form not listed now carries on as the listed one for what was
  left of it, never longer than that form runs (`DecreeTracker.CarryOn`, `DecreeMath.CarriedEnd`, the first hour after
  a load): militia drills in an industrial colony become its drills; with both forms in force the listed one goes on and
  the other ends quietly. Rationing's forms only lose a duplicate (famine bread declared for a famine since gone stays
  itself). A petition for a form not listed is let go quietly. The drills' bouts and the militia's muster keep their
  risk lines (`hurts` Drills and Muster, "Drills that bruise").

### Working through it (3.2.1, ailments and health stories)

With the everyday ailments on, a strained back or a sprained ankle that is untended and out of bed counts its hours of
heavy work (hauling, building, mining) each day. From the fourth hour in a day, the first hour they're found right at it
has one roll for that ailment (once each, in session memory): one in ten. Not while the rest-when-ill decree holds. A strained back tears: `Council_Ail_TornBack`
(Rimfolk's own ailment, in the strain's place; 3 to 5 weeks, sharper pain, half the carrying, half as long in bed,
tending helps; its own mood in place of the strain's, a hint with the strains). A sprained ankle gives: a fall, a bruise
of 3 to 5 on the leg, or a crack of 2 to 4 on its bone one time in three; the sprain stays. Either way the load drops,
they sit down hard for a few seconds (the game's wait), say so (`Rimfolk_Pool_WorkedThrough`), a doctor of the colony
within talking range has a word (`Rimfolk_Pool_DoctorsWord`), and the news tells it with the ailment's doctor's note
(`CC_Harm_TornBack`, `CC_Harm_AnkleFall`, `CC_Harm_AnkleCrack`). Prevent it with bed rest, the work tab, the rest-when-ill
decree or a tending.

### Ailments of the land, the work and the age (the player's request, 6 November 2026)

Eight everyday ailments and three serious illnesses, each from something the hourly look finds on the map
(`AilmentChecks.Sample`, which holds its counted causes as the bits of a long now; the pure rules in
`Ailments/AilmentCauseMath.cs`, tested in `Tools/Tests/AilmentLandTests.cs`), each the game's own hediff with stages and
tending, the serious ones its immunity race with a hidden incubation (`Rimfolk_Hediffs_Ailments_Land.xml`,
`Rimfolk_Hediffs_Illnesses_Land.xml`; moods, hints and lines in the files of the same name).

| Ailment (names by age) | Cause on the map | Course | Prevention | Rate |
|---|---|---|---|---|
| Swimmer's itch (pond itch) | an hour's swim (the game's `GoSwimming`) in still water: a lake, a pond, a marsh, not a river or the sea | 2 to 4 days, a wash shortens it | swim in the river | 4% a day, children half again |
| Trench foot (marsh foot) | 4 hours outdoors in water, marsh or mud below 10 °C, the line moved by their own cold shift | 1 to 2 weeks, less in bed, tending helps | a bridge or path over the wet, short shifts | 1.5% a day |
| Sandfly fever (three-day fever) | 2 hours outdoors in an arid land from 18:00 to 5:00 at 20 °C or more | about 3 days whatever is done | indoors after dusk | 3% a day, not again within 30 days |
| Jungle rot | a cut, scratch, stab or bite untended for 6 hours in a wet land at 25 °C or more outside | about two weeks, a few days tended, industrial best | wounds tended the same day | 8% a day with such a wound |
| Rabbit fever (hunter's fever) | 2 hours butchering a small wild animal (body 0.5 or less) | about a week, a few days tended, industrial best | a skilled butcher (Cooking) | 1.2% a day |
| Metal fume fever (founder's ague) | 4 hours smithing or smelting in a closed room of 60 cells or fewer | 1 to 2 days, less in bed | a big or open smithy | 2.5% a day |
| Stump sores | 8 hours walking or at heavy or bench work on a limb of the old kinds (weaker than natural, made before the spacer age) | 3 to 6 days, much less in bed | a lighter day after a long one | 3% a day |
| Mechanite fever (machine fever) | a part or implant of the spacer age put in while one of the colony, or a first dose of luciferium | 1 to 3 days, less in bed | none: it's the mechanites | 1 in 4, once a part |
| Anthrax (black bane), serious | an hour butchering an animal that had begun to rot or was sick when it died | 1 to 6 days hidden, kills in about 7 untreated | burn the carcass; rotten bodies off the bill | 4% such a day |
| Valley fever (desert fever), serious | 4 hours building, mining in the open, sowing or clearing in an arid land with no rain falling | 7 to 20 days hidden, kills in about 12 untreated | dig after rain; turns | 0.3% a day |
| Bird fever, serious | an hour cleaning a closed room where birds (a body of the game's Bird) are kept | 5 to 14 days hidden, kills in 7 to 8 untreated | an open pen; turns | 0.25% a day |

- **Through the harm service.** Each is a Rimfolk-made hurt (`HediffCompProperties_Ailment.capped`, with its class in
  `hurt`): `CanGet` asks `Harm.May` and `Give` goes through `Harm.Ail`, which checks the cap again and counts it. So one
  in three days, none while downed, in a sickbed, pregnant or fighting, none for babies, and for children only the three
  of class Mild (the itch, sandfly fever, stump sores); the rest have no child factor. "Hurts from everyday life" at
  nothing stops them with the rest. They are told as harm is: the news at `Importance.Important` and a line of their
  own from `Council_Pool_SayAilment` over their head as it shows (`AilmentTracker.SayOnset`), the serious ones in their
  letter with the doctor's note. The older ailments stay as they were, outside the cap: putting them under it would also
  hold back a drill's blow or a slip at work for three days after every cold.
- **Named by the age.** `names` lists a name per age (`[tribal|medieval] pond itch`); `AilmentTracker.NameOf` gives the
  colony's (cached per def and age), and the health tab (`Hediff_Ailment.LabelBase`), the message (`{ILLNESS}`), the
  letter, the journal, the news of a recovery and the doctor's record use it. The def's label is the industrial name.
- **The storyteller.** The new serious ones' chances lean as the catching clock does (`AilmentCauseMath.DiseaseLean`:
  one over the storyteller's disease interval, between a quarter and double, nothing with the game's diseases off);
  besides that, the serious illnesses' room in the colony (one kind at a time, none within 30 days) holds them as it
  holds the others.
- **Mechanites.** Within three days of the part (`FreshMechanites`: only if they were a colonist for longer than the part
  is old, so a recruit with an old bionic arm has nothing new), the roll waits until the cap lets them be hurt (out of
  the sickbed), then comes once for that part (its load ID in `bitesRolled`).
- **Jungle rot** needs no memory: each colonist is looked at once an hour, so a wound is in its window
  (`RotWindow`, untended and six hours old) at exactly one look.
- **What makes room for them.** The catching clock went from 50 to 60 days (`AilmentTracker.EverydayCatchingDays`), so
  the colony's time with an ailment stays where it was ("Balance targets"). The ailments of deserts, jungles, old limbs
  and mechanites never come in the simulator's temperate colony; they take the place there of the chilblains and snow
  glare a desert never has.

### Dangers on the map (the player's request, 6 November 2026)

Six Rimfolk events of things that go wrong where the colony lives (`Happenings/Dangers.cs`,
`Defs/IncidentDefs/Rimfolk_Events_Dangers.xml`, pure numbers in `Dilemmas/VarietyThreeMath.cs`): the creak at the dig,
the leaning tree, smoke in the dry grass, the old shell, the twitching scrap, the ground giving. They are events like
any other, in the same share of the storyteller's draw (`HappeningTracker.ShareFactor` counts every def, so a new one
thins the rest a little), and come only with `Harm.On` and no threat on the map (`HappeningWorker_Danger`). Each is
seen coming with the game's own signs (grit and rubble, heaved ground, smoke, sparks), asked about in a quick letter
whose words say what can happen, and kept on the map as one of `OnMapTimed`'s kinds (`DigFace`, `Leaning`, `Smoulder`,
`Shell`, `Scrap`, `SoftGround`: the sign until the answer, then the watch the answer sets; `MapDangers.Step`). Every
answer names its hurt under it (a Health line), every hurt is `Harm.Injure` in `MapDangers.Wound` on the scaled chance
(`Harm.Chance`), told at `Importance.Important` in two wordings, with a line said by whoever was there (the event's
`lines`, tagged by the moment). When the cap keeps someone from the hurt, what happens on the map still happens (the rock
falls, the ground gives) and they get clear. The game's own does the rest: its fire (`FireUtility.TryStartFireIn`) for a
smoulder left to catch, its shell and fuse (`CompExplosive.StartWick`) for the shell set off, its mud for ground that
gives, its column, floors and cut designation for the answers that make things safe. A falling tree breaks the wall it
leans on and never brings it down (`VarietyThreeMath.TreeWallDamage`), and reaches only whoever stands on its side.
No mood on top of a hurt; no fight of their own.

### Harm, parts 2 and 3: the rest of the top ten (7 November 2026)

The other seven of the player's top ten, in the player's order, each on the service above (`Harm/`, the pure rules in
`Harm/HarmTwoMath.cs`, tested in `Tools/Tests/HarmTwoTests.cs`). No setting of their own: each sits under its system's
and "Hurts from everyday life"; nothing new on the clock (the sick towns' look and a rival's sabotage ride on
`HarmBook`'s hour, the rest on their system's own). Saved: `HarmBook.turning` (the hunts' game that can turn, six days)
and `HarmBook.sick` (`SickStay`: travellers' stays at sick towns and a sickness carried home), a camp's `quarrelSouredAt`,
`brawled`, `brawlAt`, `brawlDay` and `brawlNames`, an election's `brawlRolled`, a held risk's `frayed`. The service gained
`Harm.MayAtOwnAttack(pawn, cls)`: the cap for someone whose own attack brings the hurt about (a hunter's shot), which
leaves their attack in the last hour out of "already fighting" (the game sets it with every shot, so the plain cap would
never let a hunted beast turn). The pools are in `Rimfolk_HarmTwo_Lines.xml` (in `ImmersionTests.SpokenPools`), the
words in `Rimfolk_HarmTwo.xml`, each told message in two wordings. Dev mode: Debug actions, Rimfolk, "Harm two: ..."
(`Harm/HarmTwoDebug.cs`), each bringing one of the seven about now past its rate, never past the cap.

### A camp's quarrel that becomes a brawl (2.4.1, camps)

`Harm/CampBrawls.cs`, in the camp's hour (`Camp.BrawlHour`). While a camp's tension is 70 or more (the council's second
warning), or for three days after the old quarrel with another people was answered badly (`Rimfolk_DlmC_Feud`: the
shouting at the table's risk and keeping out of it, `DilemmaEffect_CampQuarrel`, named under the answer), the first
evening hour at their fire (six to ten) with someone fit there rolls once a day: one in four (`CampBrawlEvening`), half
with the colony's peacemaker at home, a frayed temper among the colony's people at the fire half again at most, times
`Harm.Scale`; once a stay. Fit: a grown-up up and about at the fire (14 cells for theirs, 16 for the colony's), not
peaceful, able to move and use their hands, the cap letting them fight. With one of the colony there and the deeper
hooks on (so their people don't count a blow as an attack: `Outsiders.QuietBrawl`, a brawl in the outsiders' bout list
whose end the camp tells), the colony's hottest temper and the one of theirs they think least of within talking range;
otherwise two of their own. The first of theirs says it (`Rimfolk_Pool_CampQuarrel`, tagged colony or theirs), the
game's social fight through `Harm.Fight`, then up to two more pairs from those free at the fire (`JoinPairs`), each with a
word (`Rimfolk_Pool_CampBrawlJoin`); tension 15 more; the message names them. Watched each hour until nobody is
fighting (three hours at most): with the colony in it, their people remember a fistfight (`KinMemory.Note` "brawled",
6 goodwill once through `Camp.LoseGoodwill`, which stops short of hostility; the camp's ending is not also remembered as
a stay, `Patch_FactionMemory.CampEnded`), and the camp packs up early (`Leave("brawl")`, `CC_Camp_End_brawl`, a negative
letter). Two of their own: `brawlTheirs`, a neutral letter, nothing held against the colony. Never in danger, never a
child. Prevent it with the tension (trade, gifts, answering their asks), the peacemaker, and by keeping hot tempers from
their fire.

### Sickness brought home (3.1.1, journeys)

`Harm/SickHome.cs`, on `HarmBook`'s hour. A caravan of the colony's found stopped on another people's town counts each
traveller's hours there while the town lies under a sickness: a plague (`kind` sickness, `Council_WS_Plague`) or a famine
(hunger or drought: the famine, the locusts, the long drought, the blight) among the region's running world events, its
people's own or within twenty tiles (the same reading as the town's own hardships, `TownLifeMath.HardshipKind`); never the
herds' murrain. Two hours or more is lodging there. The first hour any of them is home, the journey has one roll:
`HarmTwoMath.SickHome`, one in six for a night's stay (eight hours), less for a shorter one, half with medicine in the
caravan's packs, half with industrial or spacer medicine, half again if the quarantine decree holds for those back, a
famine half a plague; times `Harm.Scale`. If it comes up, one of them the cap lets be hurt falls ill two to four days on:
a plague's the game's flu, or its plague one time in four; a famine's the everyday stomach bug (with the everyday
ailments on). It shows through `Harm.Ail` (an onset held back by the cap waits three days, then passes), with a line over
their head (`Rimfolk_Pool_SickHome`, tagged plague or famine) and a letter naming the town and what lay over it, with the
doctor's note.

### The champion's bout (2.2.1, Rimfolk's events)

`Harm/ChampionBouts.cs`. A travelling champion's challenge at fighting (Melee) is a bout on the map now: the feast's
bout (`Outsiders.ChampionBout`: the sparring rules, eight swings each or until one yields to the pain) at a clear ring
beside the colony's champion, the colony's people nearby called round to watch, the challenger's opening word by their
people's ways (`Rimfolk_Pool_ChampionBegin`, `form:` tagged). One bout in 25 (`ChampionBlowChance`, the sparring rule)
lands a real blow, decided at the start under the scale and the cap for both (the spar counts it with `Harm.Note`). The
bout's winner wins the contest (a draw is settled by skill, as before) and the stake; a real blow gets the challenger's
word (sorry, a sneer from a hot temper, a fair blow granted when it fell on them) and a line in the contest's letter
(`CC_HarmTwo_ChampionBlow`). The answer's sheet names it (`CC_Impact_ChampionBout`). The bout is named as the
challenger's people fight it (`HarmTwoMath.BoutForm`: wrestling and stick-fighting, staves and blunted swords, boxing, a
stun-baton bout). Shooting at targets, crafts, cooking, art and debate are as they were; with the outsiders' practice
bouts off or hurts at nothing, a fighting challenge is decided on the spot, unhurt, as before.

### Hunts that can bite back (2.1.1, answers and the calendar)

`Harm/HuntsBite.cs`, with answers on the map and the deeper hooks. An answer's hunt (`DilemmaEffect_Hunt`), a hunting
party (`DilemmaEffect_HuntParty`) and the calendar's hunting day may take one head (at most) of game that can turn on its
hunters (`HuntsBite.MayTurn`): wild, seen, up and about, a revenge chance above the safe game's (0.1, by its kind and the
storyteller), never a predator, an insect, a dryad or anything bigger than a muffalo or an elk, nobody's, nobody's bond
and not the faith's, and alone (no packmate within the game's own 24 cells, so a turn is that one animal and never a
herd). The safe game comes first as before; the hunting season decree's morning hunt keeps to safe game
(`HuntsBite.SafeOnly`). Marked, it's remembered six days (`HarmBook.turning`). The game's own revenge chance for it
(`Patch_HuntTurnChance`, a postfix on `PawnUtility.GetManhunterOnDamageChance(Pawn, Thing, float)`, with its distance,
stealth and the hunter's role) runs times `Harm.Scale`, and is nothing for a hunter the cap holds back
(`Harm.MayAtOwnAttack`); a colonist the player drafted to shoot it has the game's own chance. When it turns
(`Patch_HuntTurned`, around `Pawn_MindState.Notify_DamageTaken`): the hunter's hurt for the cap, a word from them
(`Rimfolk_Pool_GameTurns`), a message, and the game's own revenge and fight. Named before: the answer's and the hunting
party's sheet (`CC_Impact_HuntTurn`), the marking's message, the hunting day's letter. A hunt under way keeps its head
that can turn as game (`PlayHunt.Huntable`). The ages' difference is the game's own: a bow's or a spear's range brings
the hunter closer, and its distance factor lifts the chance.

### A rival's sabotage, found out (3.5.1, bonds and rivalries)

`Harm/Sabotage.cs`, on `HarmBook`'s hour, with the social web and answers on the map. A bitter rivalry (the web's
rivals at 50 or more) and one of a petty or abrasive nature (abrasive or jealous, never kind): a day's chance of one in
30 while their work is shared (the same best craft), half with it apart, half again with the peacemaker in office, times
`Harm.Scale`; about twice a year. The other's work is a bench: their best of crafting, art and cooking (6 or more), the
bench of that work with the most bills. Within the day, by day, with the bench free and the other 15 cells or more away
and no danger, the rival walks there (the job `Rimfolk_Sabotage`, a few seconds at it): a machine jammed (the game's
breakdown, a component to mend it) in an industrial or spacer colony where the bench has one, else the bench knocked
about (a quarter of its hit points, never below a third; the game's builders repair it). Someone of the colony within
12 cells in sight always tells (`FoundOut`), saying so to the rival there and to the other face to face; with nobody,
word goes round half the time, through whoever is with the other within half a day. Found out, the other goes to have
it out (`Together.When`): the game's social fight under the cap for both, or hard words when the cap holds, the rivalry
8 deeper either way, a message naming what and how it was found out. Not found out, the bench's state is told and
nobody is named. The words are by what was done (`SabotageForm`): a charm taken, a tool broken, a machine jammed, a
program tampered with.

### Hustings that come to blows (1.4.1, elections)

`Harm/HustingsBrawls.cs`, in the hustings (`OfficeTracker.React`). The first heckle at a candidate's speech from someone
backing another candidate, with a rival of the heckler's among the speaker's supporters there in talking range, has
the hustings' one roll (`brawlRolled`): one in eight, half with the peacemaker there, half under the best-behavior
decree for the heckler, half with calm candidates (none abrasive or volatile), a frayed temper half again at most,
times `Harm.Scale`. The supporter answers the heckle (`Rimfolk_Pool_HustingsShove`), the game's social fight under the
cap, a message; the speeches wait round it, and once it's over whoever chairs the hustings (or the eldest there) calls
for order (`Rimfolk_Pool_HustingsOrder`, by age: the fire, the hall, the meeting, the assembly), and the hustings go on.

### Frayed tempers (3.3.1, the heart)

`Harm/FrayedTempers.cs`. Worn down, a sharp word comes to blows a quarter more often; near breaking, half again (a
beer's 1.5, `DrinkBeforeWorkMath.MaxFightFactor`, never more), leaned by `Harm.Scale` up to the usual; the more frayed of
the two sets it. It touches only Rimfolk's own moments with a fight's own chance: a near fight and the other moments
of `DilemmaEffect_Fight` with a chance below certainty (also when held to their play-out, `HeldRisk.frayed`), hard words
with an outsider, a camp's quarrel and the hustings' heckle. Never the game's insults, never the break picker, no mood.
Only the part of a fight the strain made (`HarmTwoMath.FrayedRoll`: a roll between the old chance and the leaned one)
goes through the cap for both and is told with the strain named (`CC_HarmTwo_Frayed`); the rest is the fight it was. The
heart's page says it in the band's own words. An answer whose fight comes with its risk is the risk's roll, not leaned.

### Harm, part 4: the fifteen the player took (7 November 2026)

The player's "go ahead with the suggestions": fifteen more of the review, each under its system's setting and the shared
budget, the pure rules in `Harm/HarmFourMath.cs` (`HarmFourTests`), the hub `Harm/HarmFour.cs` on the harm book's hour
and slices (no new Sched slot), the waiting hurts saved in `HarmBook.waiting` (`HarmWaiting`: a sickness caught, a
coughing guest, a sore loser's walk, a rematch owed, a games day's contests). Messages only where the game gives one for
the same hurt (an illness caught, an animal turning on its handler); the rest are seen on the map, in the hit, a puff of
dust or smoke, the line they say and a few words over them (`HarmFour.Mote`), and on their health tab. The pools are in
`Rimfolk_HarmFour_Lines.xml`, the words in `Rimfolk_HarmFour.xml`; dev mode's "Harm four: ..." (`Harm/HarmFourDebug.cs`)
brings each about past its rate, never past the cap, and logs the budget.

| Kind (review) | Where | Rate (its own) | Prevention | Seen |
|---|---|---|---|---|
| The physician catches what they tend (1.1.1) | `Catching.cs`, the game's tending | 1 tending in 40 of a head cold, chest cold, stomach bug or pinkeye; 1 in 400 of tuberculosis, cholera, measles or the flu | gloves and masks (industrial, spacer) half, a sterile room a quarter, the quarantine or rest-when-ill decree half; a third tending of the sick in a day half again | a day or two later, the ailment's message, the doctor's note, a line |
| The guest's cough (2.5.2) | `Catching.cs`, Rimfolk's visitors | 1 visit in 20; the one who sat longest with them catches it | a guest room of their own (fewer hours near), the quarantine decree | the guest's cough said now and then; a day after they go, the illness |
| Caught in the hail (2.2.3) | `Mishaps.cs`, the hailstorm | 1 in 30 each 250 ticks out under it, 2 a storm at most, a bruise of 1 to 3 (children may) | called in, a roof, a helmet | the stone's ping, a line, a few words |
| A cold night for the frail (1.2.3) | `Catching.cs`, banked fires | 1 such night in 7: a hard frost, 3 hours asleep below comfort, the old, small children, the frail or convalescent | a heater kept up in their room, ending the decree; named on its card | a head cold in the morning, their line, the doctor's word |
| A felling mishap (4.3.1) | `Mishaps.cs`, a tree chopped | 1 tree in 300 under Plants 5 (stone axe x1.5, steel x0.75, spacer x0.5), a bruise or a crack on the leg | skilled cutters | dust where it fell, down on the ground, a line |
| A failed build that hurts (3.4.1) | `Mishaps.cs`, a failed construction | 1 failure in 10 on a wall, furniture of 2 cells or 20 kg, a bridge | skilled builders on heavy work | dust, the builder holding an arm, a line |
| Burned at the bench (4.7.2) | `Mishaps.cs`, Alpha Skills' drink | 1 drink in 100 before cooking or smithing | the switch "A drink before hot work" | the job dropped, a line, a few words |
| The rut in the pens (4.5.3) | `AnimalHurts.cs`, the pens in autumn | 1 charge in 20 hours near a rutting male in his pen, half landing | keep out of the pens in the rut, leave it to the herdmaster (never charged), a handler of Animals 10 half | his charge, the dust, a line, a message |
| The herdmaster's lesson (4.5.1) | `AnimalHurts.cs`, the lesson played out | 1 lesson in 30 with a nervous animal handled beside the learner | a calm animal for the lesson | the kick or bite, the herdmaster's calming word, a message |
| A fall from the saddle (4.9.1) | `AnimalHurts.cs`, Giddy-Up | 1 dismount in 50 under Animals 6 on a mount not trained to obey | riders of Animals 6, obedience training | the mount bucks, the rider on the ground, a line |
| The groups come to blows (3.6.1) | `Quarrels.cs`, a dig at the other lot | 1 dig in 8 at a table between groups at 60 or more, up to 2 more pairs joining | the peacemaker, shared meals | the game's fights, both groups' names over it |
| The sore loser (1.4.2) | `Quarrels.cs`, the count | 1 close loss in 10 among the hot-tempered or bitter, blows half the time | a gracious winner's handshake at the count, a calm loser | the walk over 1 to 3 days on, the words, a fight |
| A guest's night goes sour (2.5.1) | `Quarrels.cs`, a guest with a bed | 1 night in 15 by temper (a drink x1.5) | the warden of guests, the best-behavior decree, no drink; named under the answer | the outsiders' fistfight; a visitor goes on at first light |
| The rematch (2.8.1) | `Quarrels.cs`, a bout's end | 1 beaten in 3 holds it, asked for on a later visit within the year | a good word or a gift to their people; named in the duel's terms | the challenger asks by name, a duel to first blood or a bout |
| Games with real contests (4.1.1) | `GamesDay.cs`, a games day's gathering | 1 bout in 25, 1 runner in 8 on rough ground: about 1 hurt in 2 games days | a paved yard (no sprains), keeping the day otherwise | the contests near the gathering, a line, a few words |

The ages: the physician's gloves and masks from the industrial age; a felling's axe by the means; the groups as clans,
factions, shifts or departments; the games as wrestling and a race, staves, boxing and a three-legged race, or a spacer's
sim tourney with a wrenched wrist and no race; the rest the same in every age. Hooks in other systems (one line each):
`Health/HealthPatches.cs` (the tending), `Happenings/Workers_Nature.cs` (the hail's look, its count saved),
`Calendar/CalendarKeeping.cs` (a games day kept), `Social/Groups/GroupMoments.cs` (the dig),
`Offices/OfficesElection.cs` (the count, the handshake), `Outsiders/OutsiderOutcomes.cs` (a bout's end, a good word, a
gift), `Visitors/VisitEffects.cs` (the bed's line), `Integrations/BeforeWork.cs` (the hot work), `Harm/DrillBouts.cs` (a
games bout is a drill bout with `games`). Two new Harmony patches: `Patch_FellingMishap` (a postfix on
`Plant.PlantCollected`) and `Patch_FailedBuildHurts` (a prefix and postfix on `Frame.FailConstruction`). The hail's letter,
its condition and the Almanac said nobody would be hurt; they say what happens now.

### Harm, part 5: sickness that spreads, the weather and the cold (7 November 2026)

The first of three batches of the review's other 69 proposals, after the player's "all suggestions please": fourteen more
kinds within the shared budget, each under its system's setting (a link between two systems behind "Systems talk to each
other"), the pure rules in `Harm/HarmFiveMath.cs` (`HarmFiveTests`), the hub `Harm/HarmFive.cs` on the harm book's hour
and slices (no new Sched slot); and the forecast that can be wrong (4.4.2), which hurts nobody and stays outside the
budget, in the fields' own code. Every sickness carried in is the guest's cough's rule (part 4, `Catching.CoughHour`, now
shared): someone carries it, and whoever of the colony spent the most hours near them (both awake, within four cells)
catches it a day or a few after, unless a decree keeps the sick apart; the patient's is a night in the same room. The
carriers, the patient's ward and the healer's batch wait in `HarmBook.waiting` (`HarmWaiting` "cough", "ward", "batch",
"dose": no new saved collection). Messages only where the game gives one for the same hurt (an illness caught, naming
whom it came from); a stumble, a sore arm and a slip into the water are seen on the map; a predator comes by the game's own
hunt with the game's own warning. The pools are in `Rimfolk_HarmFive_Lines.xml`, the words in `Rimfolk_HarmFive.xml`; dev
mode's "Harm five: ..." (`Harm/HarmFiveDebug.cs`) brings each about past its rate, never past the cap.

| Kind (review) | Where | Rate (its own) | Prevention | Seen |
|---|---|---|---|---|
| The sick work on (1.2.5) | `Carriers.cs`, a petition's lapse | a lapsed petition to rest when ill or for a quarantine, while one of the colony up and about has something catching: one more case | heed the petition; named under it on the decree page | the sick one's cough at whoever is near, the petitioner's line, the case a day later |
| The sickness at the gate (2.3.1) | `Carriers.cs`, visitors' and traders' groups | 1 group in 4 of a people a plague lies over: the flu, the plague 1 time in 4; the plague's raised flu and plague a little lower for it | the plague's "shut the gate", the quarantine decree, few near the trader; named under keeping the gate open | the carrier's cough; two or three days after they go, the illness and whom it came from |
| The fever at their fire (2.4.2) | `Carriers.cs`, the camp's sick one | 1 camp sickness in 5, its first three days: a chest cold, the flu 1 time in 4 (a child a head cold) | the colony's doctor seeing to them (the camp's ask, named on it), keeping away | the sick one's lines by the fire, the illness a day later |
| A courier who arrives sick (2.7.2) | `Carriers.cs`, a courier's party | 1 courier in 4 of a people under a plague | the quarantine decree, the goods left at the gate | as the gate's |
| The patient who brings something worse (3.2.2) | `Carriers.cs`, `HospitalPatient.cs` | 1 patient in 20 brings tuberculosis or cholera; a night in their room 1 in 4 | a ward or room of their own, the quarantine decree; named in the letter and on every answer | the letter's cough, the case in the shared room a day or two later |
| The hedge healer's bad batch (2.2.4) | `Carriers.cs`, the healer's paid doses | 1 visit in 8; of those dosed, 1 in 3 the next morning | a doctor of Medicine 8 looks it over (a word, nothing more); declining; named under paying | the stomach bug (a tonic) or a sore arm (a shot) |
| Small things that grow (3.2.3) | `Untended.cs`, each colonist's hour | worms untended 3 days, 1 in 20 the game's gut worms; bites untended a day in a malaria country, 1 in 20 its malaria | cooked meat, a tending; sleeves and a bed indoors half each | the illness and its message |
| Dirty water (4.9.3) | `Untended.cs`, Dubs Bad Hygiene by name | a day drinking untreated water: the stomach bug 1 in 600, only while the mod's own illnesses read off | water treatment | the bug, the message naming the water |
| Waiting at the gate in the cold (3.1.2) | `ColdHurts.cs`, a journey's wait | every 2 hours waiting past the hour in a hard frost, colder than comfortable: 1 in 20 frostnip or chilblains | answer the letter, warm clothes; named in the letter in a frost | the line at the gate, the ailment's message |
| The night watch in the cold (4.2.2) | `ColdHurts.cs`, the watch on the walls | the same cold rule, half with two on watch; a lone watcher outside the walls, 1 watch in 6 a hungry predator in reach | warm clothes, a warm look-out, two on watch, walls; named under the watch | the line, the ailment's message; the predator's hunt, the game's warning |
| Weather lovers (3.11.3) | `ColdHurts.cs`, the two quirks | a rain lover's 2 hours in the rain: a chest cold 1 day in 150; a snow lover's 3 in bitter falling snow: frostnip 1 in 75 | a coat, a hood (half) | their line, the ailment's message |
| Sleepwalking after a nightmare (3.12.1) | `NightWalks.cs`, the job `Rimfolk_Sleepwalk` | 1 nightmare in 15 that left them shaken, a later night's small hours; frostnip in the cold, a stumble half the time | the bolted door (the sleepwalker's answer), a roommate | the walk across the yard, the line, someone leading them back |
| A restless walk at dusk (3.3.3) | `NightWalks.cs`, the job `Rimfolk_RestlessWalk` | restlessness at its height: 1 dusk in 15 out to the edge of the ground; alone outside the walls, 1 walk in 4 a hungry predator | a friend along, walls round the ground | the walk out, the line, the game's own hunt and warning |
| A slip into cold water (4.9.2) | `ColdHurts.cs`, the game's or Vanilla Fishing Expanded's fishing | 1 hour in 200 fishing below freezing, twice in a hard frost: the game's hypothermia at shivering | no fishing in a frost | the splash, a few words, the dash for a fire |
| The forecast can be wrong (4.4.2) | `Fields/FieldWeather.cs`, outside the budget | as often as a frost beats the forecast on a field it said would pay | not preventable | the field's note for five days; the game's own frost |

The ages: the bad batch is a bitter tonic's stomach bug before industry and a shot's sore arm after; the rest are the same in
every age (a spacer trader's mask is left out). A predator found for a lone walker or watcher is a wild one, hungry (its
food at 60% or less), within 45 cells, that would take them as prey by the game's own reckoning and can reach them; its hunt
stops once they are down (`killIncappedTarget` false), the cap counts it as their hurt, and the colony can drive it off.
"Outside the walls" is the map's edge reachable from where they stand without passing a closed door. Hooks in other systems
(one line each): `Decrees/Decrees.cs` (the lapse) and `UI/DecreePanel.cs` (the petition's line), `Couriers/CourierTracker.cs`
(a courier's party), `Camps/CampStory.cs` (the fever, the ask's line), `Health/HospitalPatient.cs` (the worse illness and the
letter's tale), `Happenings/Workers_More.cs` and `VisitorServices.cs` (the batch, each dose), `Journeys/JourneyRun.cs` (the
letter in a frost), `PlayOut/PlayOutGroupEffects.cs` (the watch's line); `Harm/Catching.cs`'s cough is shared, and
`DilemmaEffect_HarmRisk` names a risk on an answer's sheet in the defs (the plague's open gate, the healer's pay, the
patient's answers). While the carriers at the gate are on, `Council_WS_Plague` raises the flu 1.35 times and the plague
2.3 (the modifier's `withCarriers`; 1.5 and 2.5 with them off, as before): the carriers bring the rest.

### The rates (the balance model's harm mode)

`dotnet run -c Release --project Tools/Balance -- --harm` plays a colony-year (60 days, 1500 runs by default) of each
style and size with every kind of hurt from everyday life through the shared cap, the heart played hour by hour with each
hurt's pain and each fight's hour of combat. What a kind does once it comes is the mod's arithmetic (`HarmMath`,
`HarmTwoMath`, `HarmFourMath`, `HarmBudgetMath`, `HeartMath`, `BeastsMath`, compiled in); how often a colony meets it is
the mode's own table, each an estimate that says where it comes from. Three runs of the same colonies: "before" (the
kinds built before the budget, each at its own rate), "no budget" (every kind on, parts 4 and 5 too, each at its own
rate), "budget" (every kind on, each at its pass). Alpha Skills, Giddy-Up, Hospitality and Odyssey's fishing loaded;
PokéWorld and Dubs Bad Hygiene's thirst not. Hurts the cap counts, per colonist-year (4000 runs; harm, part 5):

| | 6, well run | 6, average | 6, neglectful | 12, well run | 12, average | 12, neglectful |
|---|---|---|---|---|---|---|
| Before (every kind built up to 0.149.0) | 0.339 | 0.453 | 0.560 | 0.290 | 0.395 | 0.504 |
| Every kind on, no budget | 0.669 | 0.983 | 1.356 | 0.538 | 0.812 | 1.149 |
| Every kind on, the budget | 0.307 | 0.454 | 0.629 | 0.242 | 0.367 | 0.526 |
| With part 4 alone (0.153.0), the budget | 0.311 | 0.452 | 0.618 | 0.245 | 0.368 | 0.519 |
| The game's own beside them: a raid's wound | 1.4 | 1.7 | 2.0 | 1.4 | 1.7 | 2.0 |

The average colony of six, on which the base rates are measured, keeps its total (0.453 before, 0.454 after, with part 5's
fourteen in: part 5 would add 0.12 a colonist-year on its own, and within the budget brings 0.055, the rest giving up as
much; each kind now passes at about 44% of its own rate). Elsewhere
the budget keeps prevention's meaning: a well-run colony prevents more of the new kinds and ends a little below its old
total (9% with six, 17% with twelve), a neglectful one a little above (12% with six, 4% with twelve), and a colony of
twelve meets fewer of the new kinds per head (a storm bruises two at most, a games day has two bouts). Each kind passes at
a little under half its own rate. Part 4's fifteen would add 0.43 a colonist-year on their own; with part 5's beside them, the later kinds would add
0.54 and within the budget bring 0.25, the founding kinds giving up as much. The variety: a kind coming again within thirty
days of its last falls from 0.19 to 0.14 times a colony-year (the average colony of six), the commonest kind (the ailments
of the land and the work) from 17.4% of the hurts to 16.6%, the total the same. The heart's strain, its days off and the strain's mood are the same
before and after to a tenth. The beasts' summary (`BeastSim.cs`) still prints after it; their stampede is a kind in the
budget now, their nemesis's return the game's own attack outside it.

## How the systems talk to each other

Each of Rimfolk's systems was built to stand on its own, and many already read each other (feelings nudge wishes,
dreams and moments; setbacks feed feelings; the colony's ways decide who minds an answer; bonds vote). Interplay
(Source/ColonyCouncil/Interplay/) adds the links that were missing, so that what happens in one part shows up in
another and the colony reads as one life.

- **Rules.** Every link rides on a pass or an event that already happens (the council's day and hour, a gathering
  ending, a spat recorded, a day kept, a deed settled, an election's ballot, the evening news) through a one-line hook;
  nothing ticks of its own. Nothing new is saved: cooldowns and the rolling count of setbacks are session memory, and a
  load only gives them a second chance. Each link answers to the **Systems talk to each other** setting and to the
  settings of the systems it joins. Effects are small and prefer what isn't mood (strain eased, a bond, trust, a story
  moved on, a wish more likely, a recovery a little shorter); no link adds a good mood of its own.
- **Where to look.** `Interplay.cs` (the passes, grief, cooldowns, lines), `Interplay_Offices.cs`,
  `Interplay_Gatherings.cs` (grief, health and the steward at gatherings, pets and grief), `Interplay_Stories.cs`
  (nudging a story: a Time gate's clock moves on, a Hold gate gains held hours, a Talk counts as had),
  `Interplay_Council.cs` (setback runs, votes, ways, the meeting, quirk tags), `Interplay_World.cs` (camps, memory
  tags), `NewsDays.cs`. Lines: Defs/FlavorPoolDefs/Rimfolk_Interplay.xml.

| From | To | Trigger | What happens |
|---|---|---|---|
| Keeper of days | Calendar, PlayOut | a calendar feast or games day not about one person | the keeper organises it, and writes about it |
| Peacemaker | Moments, social events | a friction between two, the peacemaker within a few steps | half the time, they step in: the spat is sore half as long |
| Physician | Convalescence | the council's day | rounds of everyone convalescing on their map: 0.04 off each recovery |
| Physician | Doctors' reputation | a tend | the office counts for two patients pulled through |
| Reeve | Missions | a delivery mission offered, then filled in full | the reeve deals with the neighbours; +3 goodwill, a journal line |
| Herdmaster | Pets, heart, bonds | a named animal dies; its person away two days | sits with the one who lost it (strain -4, friendship); minds the animal and is thanked |
| Marshal | Heart | the end of a fight at home | those who fought beside the marshal: strain -4 |
| Steward | Heart, PlayOut | a gathering over, the steward there | everyone else: strain -2 |
| Heart (grief) | Calendar, PlayOut | a festive calendar day | the grieving sit it out (unless it's their day); a journal line |
| Heart (grief) | Setbacks | a gathering missed | no "missed the gathering" |
| Pets | Heart | the hour, their own animal close by | grieving: strain -3, once a day |
| Heart (strain) | Wishes | a wish roll | worn out: rest and comfort wishes up to 1.3 times as likely |
| Calendar | Personal stories | a day kept | stories of those at home move on a day; a waited-for talk with someone there counts |
| Faction memory | Personal stories | a good turn the colony did is repaid | the stories of those who met the envoy move on two days |
| Camps | Personal stories, heart | a camp leaves, a colonist made a friend there | story on a day, strain -2 |
| Convalescence | Personal stories | back to full strength | story on two days |
| Nemeses | Personal stories | going for their nemesis in a fight | a fear-faced story ends, well |
| Faction memory | Camps | which people camps nearby | up to twice as likely after good turns, less with grudges, hardly after a camp was driven off; half with a vendetta |
| Faction memory | Encounters, letters | any line about a stranger's people | tags their:grateful, their:rescued, their:grudge, their:owed; 5 encounters and 6 sets of lines use them |
| Setbacks | Wishes | two of a kind in ten days | the wishes that answer them up to 1.4 times as likely |
| Setbacks | Personal stories | the same | the stories that answer them twice as likely to begin |
| PlayOut | Convalescence | a gathering over | convalescents who came: 0.03 off their recovery |
| Feasts | Ailments | a feast over, fruit or vegetables in the stores | those who ate count as having had fresh food (scurvy) |
| Traditions | The colony's ways | a council tradition kept, founded, broken, lapsed | tradition +1, +2, -1, -2; a calendar feast or rest-day tradition: a little more generous or easygoing |
| The colony's ways | Setbacks | an answer to a dilemma or event | a colonist here a year or more leans 0.25 per tier toward the colony's name |
| Secrets | Council meeting | the meeting's talk, a confidant who now dislikes the owner | a pointed remark (in the meeting's remarks), a rivalry; a rival may let it out |
| Promises kept | Elections | a ballot | a wish taken back in the last year: -3 for sitting officers |
| Epithets | Elections | a ballot | +1 per name the colony gave, -1 per dubious one (at most 2) |
| The history | Elections | a yearly election called | the holder's last promise judged from the map: +1.5 kept, -1.75 broken; +0.75 for work done in the seat; each candidate's record in the letter |
| Battles | Life goals | a fight over | a fighting spark for those who fought hardest: the three who brought down the most, anyone downed in it, whoever carried someone out |
| Quirks | Who someone is | any tag check | quirk:Hums, quirk:SleepTalker... for roles, lines and dreams; 12 social events use them |
| Calendar, kept, setbacks, health, world events, camps, missions | Dreams | the memory those leave | 15 dreams (XML) |
| Events, world events, camps, missions, encounters, dreams | Quirks | the memory those leave; a sight (the pyre, `QuirkSights`); a dream come true | 8 quirks (XML), their effects in `Quirks/QuirkEvents.cs` |
| News | News | the evening digest | three or more lines about one colonist come together as their day |

## Balance targets

What the player asked for, over and over: it shouldn't be easy to keep everyone happy, good moods shouldn't vanish,
not everything should be a mood, and neglect should have real consequences, never flat penalties. The targets below
put numbers on that. `Tools/Balance` measures them: a colony-year simulator that plays a settled colony through one
60-day year without the game, from the def XML, Rimfolk's rates (read from the source where it can) and the mod's own
mood arithmetic (`GoodLifeMath`, `SmallMomentMath`, `HeartMath`, `PressureMath`, `SetbackMath`, `AilmentMath`, `ArcMath`,
compiled in as they are), with each colonist's heart played hour by hour (`HeartSim.cs`, since phase 3 of the review).
What it can't know (the storyteller, how a player plays) is one documented table, `Tools/Balance/Rates.cs`. It writes
`docs/balance-report.md`; run it after any change to moods, rates or ailments.

**Rimfolk's mood** is everything Rimfolk adds to a colonist's mood, averaged over the year: its good memories (small joys
as their short moments), its bad memories (small letdowns as theirs), and its steady moods (trust, the colony's ways,
ailments, strain, and what good things growing ordinary takes off Rimfolk's own good moods). The game's own moods aren't
in it; the colony's whole mood, theirs included, is a row of its own.

| | Target (default settings) |
|---|---|
| Rimfolk's mood, a well-run colony | about +3 to +6 |
| Rimfolk's mood, an average colony | about 0 to +3 |
| Rimfolk's mood, a neglectful colony | clearly negative, about -4 to -10 |
| Rimfolk's good moods at a colonist's best moment | well short of carrying them to the top (P99 around +15) |
| Questions (choice letters) | not more than about one every 2 to 3 days in a colony of six |
| Two questions at once | rare: a question that can wait doesn't come within 12 hours of another (`LetterGap`) |
| Minor news | in the evening's colony news (`UI/News.cs`), not a letter or message of its own |
| Everyday ailments | a colonist carries one no more than about 10 to 15% of the time in a reasonable colony |
| The colony's whole mood (the game's moods with Rimfolk's) | about as before phase 3 of the review: 69%, 62% and 37% |
| Breaks | no more time under the minor-break line than before; the heart's day off under once a colonist-year, neglected |

**Levers, in order of preference.** Keep flavour; never add a flat penalty; tune values in XML and constants, not
systems.
1. How often things happen: event and dilemma rates and cooldowns, ailment chances, how often small good things come.
2. How long and how big the biggest good moods are (the ones that can carry a colonist for weeks).
3. How hard setbacks and pressures press when a colony is neglected.

**What the first measured pass found and did** (the report has the numbers). The recent passes (the shared budget,
standards over the game's comforts, setbacks, pressures, rewards instead of mood) had already brought Rimfolk's good
side down to about +5 per colonist: rising standards halve the council's rewards in a happy colony, and many good
moods need conditions that rarely hold. So good moods weren't cut across the board; only the very biggest (+10 and
more) came down by about a fifth, with weddings and the council's feast a little. Everyday ailments filled a quarter of
an average colonist's time: the catching clock went from 20 to 30 days and the commonest causes and after-effects
came down. Questions came at a little more than one every two days, a fifth of them within half a day of another: the
gap between questions, dilemmas every 10 days instead of 8, and fewer questions from passing visitors.

**Ailments that follow what happened** (October 2026) added ten everyday ailments that come after something real (the
flu or the plague got over, a break or a burn healed, a fight at home, a horror) or from the place (polluted ground,
snow glare, swamps, orbit). The simulator models their causes as shares of days (`Rates.cs`); to keep the time with an
ailment where it was, the catching clock went from 30 to 35 days, a few of the commonest small ones came down a little
(insomnia, heartburn, the cough after a cold, weakness after a stomach bug), and the new ones are short and mild. Over
480 runs an average colony of six went from 16.8% to about 17% of its time with an ailment, with Rimfolk's mood within
a tenth or two of a point.

**Easing, step one** (October 2026, after a player found mood too hard to keep up, much of it from things they couldn't
prevent and a colony of five ideoligions). Small steps, one at a time, measured each time:
- Normal life's ups and downs presses at 85% (`PressureMath.Factor`), and the colony's small letdowns that the council
  never gave (moments' frictions, nuisances) at 85% on normal and 70% on gentle (`LetdownFactor`, via
  `Pressures.Letdown`). The "never less than a whole point" floor still holds for pressures and setbacks.
- **Hard times** (`PressureMath.HardTimes`): from 15 points above a colonist's minor-break line down to the line,
  small letdowns ease to half and pressures to three quarters, and come back as they recover. It is read when the
  mood is felt, so nothing is stored and nothing lingers. Nightmares at low mood went from twice as likely to a
  quarter more, so they don't feed a bad stretch; sweet dreams stay rarer in low spirits, as the player preferred.
- The good-mood budget counts the third to fifth good moods at 60%, 45% and 35% (was 50%, 35%, 25%), and every one
  after at a quarter (was a fifth).
- The same work starts to wear at 4, 7 and 10 days (was 3, 5 and 8), a step lighter with any passion for it; fine and
  lavish meals never count as the same meal; "the favorite" needs a request of one's own still waiting.
- Measured (6 colonists): well run +3.1 to +3.5, average 0.0 to +0.7, neglected -10.9 to -8.9. The rates for the
  same work and meal in `Rates.cs` were lowered to match the new rules (they are assumptions, not read from code).

**Everyday ailments, a third pass** (November 2026). Averaged over the five cases that leave ailments at 100% (6 and
12 colonists, Story-rich, Demanding, no budget or fading; 800 runs each), a colonist had an everyday ailment 17.2% of
the time in an average colony, 11.7% in a well-run one and 36.5% in a neglected one. The levers, in this order:
- The mildest are shorter: heartburn, a splinter, sunburn, blisters, a smoke cough, a stiff neck, a smoker's cough,
  an itching burn, chapped hands, a cold sore and the cough after a cold; sore hands, a sore elbow, chilblains, plant
  rash and a rimrot cough a day or so.
- The commonest come less often: the catching clock went from 35 to 50 days, a strained back is 1 in 200 after a long day
  of hauling (was 1 in 140), sore hands 1 in 140 at the bench (was 1 in 110), and toothache, lice, a rimrot cough, insomnia from late nights
  and a cold sore from worry a little less.
- Care counts for more, so a well-run colony gains more than a neglected one: bed rest speeds sore hands, a sore
  elbow, chilblains and chapped hands more, and tending does more for insomnia, a rimrot cough and chilblains.

Measured the same way: average 13.9%, well run 9.3%, neglected 30.5%. Rimfolk's mood moved by a few tenths at most.

**One heart** (November 2026, phase 3 of the review; see "One heart" under Layer 7). Small letdowns became short
moments with their weight on the heart, small joys the mirror, every small mood counts toward the heart by one rule,
and one rule for good things growing ordinary took the place of four. The simulator now plays each colonist's heart
hour by hour (`HeartSim.cs`: the life of each play style, set so that life alone gives the shares it always assumed),
adds what the moments and the pile bring (the game's small moods as a share of its moods, `Rates.GameSmallBad` and
`GameSmallGood`), and splits what growing ordinary takes between the game's good moods and Rimfolk's by how ordinary
each is. Measured (6 colonists, 120 runs; before is the code before the phase, with strain a fixed share):
- Rimfolk's mood: well run +2.3 to +5.6, average +0.3 to +2.5, neglected -9.1 to -7.2 (12 colonists +4.6, +1.8,
  -6.5), all on target now (well run was under it before). The colony's whole mood: 69%, 64% and 39% (before 69%,
  62% and 37%).
- The letdowns' side (pressures with the varied table, setbacks, quiet troubles, nuisances, frictions) took -1.8, -3.8
  and -6.4 off Rimfolk's mood (well run, average, neglected) as moods; as moments -0.4, -1.4 and -2.4, and strain's own
  mood went from -0.1, -0.3 and -1.2 to -0.0, -0.4 and -2.4. The joys' side: the good moments' moods +0.6, +1.0, +1.0
  before, +0.7, +0.7, +0.6 as moments; Rimfolk's good memories +4.5, +5.4, +3.7 before (after the budget and fading),
  +8.8, +6.0, +3.5 now as given; growing ordinary takes -4.8, -0.5 and 0 off Rimfolk's good moods (-1.25, 0, 0 before
  for used to the good life alone) and about 13, 2 and 0 points off the game's own good moods.
- The heart: strained, worn and near breaking 1/0/0%, 8/2/0% and 30/17/8% of the time (the same model with letdowns as
  moods: 2/0/0%, 9/2/0% and 23/11/4%); the day off 0, 0.05 and 0.74 a colonist-year (as moods 0, 0.05 and 0.40). Time
  under the minor-break line, neglected: 34% (as moods 42%): fewer breaks, more days off. The game's own small moods
  bring 1%, 6% and 6% of all strain built.
- Tuned to get there: the letdowns' cap 1.5 a day and the joys' 3, the pile from 8 and 12 points at 0.02 an hour a
  point, a lasting letdown felt once a day, 0.6 strain or relief a mood-day, and the line 45, 35, 25 and 15. Left a
  little over: Rimfolk's good moods at a high point, well run, P99 +17.3 (before +15.4) and +12 or more 16% of the time
  (before 10%), with the whole mood no higher (P90 74%, as before).

**The heart's good side** (November 2026, the player's request; see "The heart's good side" under Layer 7). The
simulator plays it in each colonist's heart (`HeartSim.cs`): rest and the good things in their order, the needs of
belonging and purpose drawn a day at a time per play style with each colonist's own lean, deeds and a life goal moving
as daily chances (`HeartLife`), the release with its own draws so the rest of the heart draws the same with it on or
off. Measured (6 colonists, 120 runs, the code just before it and the same seeds with its setting off):
- In good heart and in high spirits: well run 28% and 3%, average and neglected almost never (their good side stays
  under 20: average colonists' hardship spends it about as fast as it fills); the good side still takes average
  colonists' knocks (strain at nothing 46% to 66% of the time, strained, worn and near breaking 8/2/0% to 6/1/0%, the
  day off 0.05 to 0.03 a colonist-year). Inspirations 0.11 and kind words 0.13 a colonist-year, well run (the game's
  own at that mood come about 0.45 a 60-day year).
- Rimfolk's mood: well run +5.6 to +6.0 (the band mood +0.7 before it grows ordinary), average +2.5 to +2.5,
  neglected -7.0 to -7.2 (at 480 runs -7.1 and -7.2, time under the minor-break line 33.6% and 33.5%, the day off 0.78
  both: noise); the colony's whole mood 69%, 64% and 39% as before.
- Tuned to get there: the bands from 45 and 65 (proposed lower), +2 and +3 (proposed about +2, +3 and +5), half a point
  of the good side a point of hardship, 3% a day of fading, 4 a day in all. Left: Rimfolk's good moods at a high point,
  well run, P99 +17.5 to +17.6 and +12 or more 16% to 18%; +1 and +2 lifted the second just as much (any lasting good
  mood held a third of the time does), so the moods stayed where a colonist feels them.
- The bands as the player approved them (6 November 2026, "As I approved"): steady from 20 with +2, in good heart +3,
  in high spirits +5, nothing else changed. Measured against the two bands on the same seeds (6 colonists, 120 runs):
  Rimfolk's mood well run +6.0 to +6.4, now just over its target (12 colonists +5.0 to +5.7), average +2.5 to +2.6,
  neglected -7.2 as before (the good side never reaches 20 there); the band mood well run +0.7 to +2.0 before good
  things growing ordinary take back their part; the whole mood 69%, 64% and 39% as before. Well run, a colonist is
  steady about 48% of the time, in good heart 28% and in high spirits 3%; average, steady 2%. Rimfolk's good moods at a
  high point, well run, P99 +17.6 to +17.9 and +12 or more 18% to 21% (at 480 runs +6.0 to +6.5, P99 +17.7 to +18.1,
  22%): the rise the first pass tuned away, which the player chose knowing it. Strain, the day off and time under the
  minor-break line unchanged.

**The presets** each aim at their own balance: Light touch about +5, +4 and 0 (well run, average, neglected) with
fewer questions (measured +6.2, +4.7 and -1.7); Story-rich the default's moods with a question a little more often than
every two days (+5.9, +3.0, -6.6); Demanding about 0, -4 and -18 (+1.7, -3.0, -14.7, with downsides 85% more and rewards
at 70% since phase 3, was 15% and 80%). Each preset's tooltip says so. Since the settings went onto five pages (phase 6 of
the review, "The settings: five pages"), the presets reach every rate and every knob of difficulty; measured on the
report's seeds before and after: Light touch +7.3, +4.6, -1.7 to +7.2, +4.8, -1.6; Story-rich +6.9, +3.2, -6.7 to +6.9,
+3.1, -6.5; Demanding +2.5, -2.8, -14.8 to +2.3, -2.9, -14.9, its time ill 9%, 15%, 32% to 12%, 17%, 37% (ailments at
125%). Story-rich keeps the default's pace of wishes and meetings: more of them would have moved its moods.

**Ailments of the land, the work and the age** (6 November 2026; see the section of that name under "Harm from everyday
life"). The simulator plays the new causes that a temperate colony has (`Rates.cs`: a swim in still water in summer, two
hours butchering small game for the cook and now and then the hunter, the forge in a small room for a crafter, cold wet
ground from autumn to spring for a hunter or a builder, a bird barn swept out, a bad carcass on the block) and leaves
out those of deserts, jungles, old limbs and mechanites. The new everyday ones fill 0.15, 0.20 and 0.23 days of a
colonist's year (well run, average, neglected, 6 colonists), and take that share from the illnesses that go round: the
catching clock went from 50 to 60 days. Measured on 480 runs against the code before them (the same simulator): time
with an everyday ailment, 6 colonists, 9.5%, 14.0% and 31.5% to 9.1%, 14.2% and 31.7%; 12 colonists 9.2%, 14.1% and
30.9% to 9.0%, 13.8% and 31.3%; Rimfolk's mood within two tenths. Serious illnesses a colony-year, 6 colonists, 0.24,
0.46 and 1.80 to 0.26, 0.53 and 2.00 (anthrax from a bad carcass, now and then bird fever).

## Building help: how far should the mod build things itself?

Think of it as a ladder. Each rung is optional and builds on the one below:

| Rung | What happens | Difficulty | Recommendation |
|---|---|---|---|
| 0. Tell | Text suggestion ("build a 5×6 bedroom") | Easy | Yes, MVP |
| 1. Point | Highlights a candidate spot on the map and jumps the camera there | Medium | Yes, phase 2 |
| 2. Propose | Ready-made room templates (bedroom, workshop, hospital, freezer). Click "Plan it" and a ghost preview follows your mouse; you place, rotate and confirm, and it lays down normal blueprints | Medium to hard | Yes, phase 3. This is the sweet spot |
| 3. Personal touches | A pawn places a small blueprint *inside their own room* (end table, plant pot, sculpture). You can cancel it like any blueprint | Medium | Fun and safe, phase 3 |
| 4. Full auto-planning | The mod picks sites and lays out whole rooms on its own | Very hard | No, or opt-in experiment only |

**Why not full auto-build?** Placing a blueprint is trivial technically. Choosing a
*good* place is the hard part: terrain, mountains, roofs, temperature, traffic, how
it connects to existing walls, fire risk, raid approach, how it looks. An algorithm can
find "a free rectangle next to an existing wall near the colony center" reasonably
well, but not a base you'd be proud of. Rungs 2 and 3 give most of the benefit while
the base layout stays yours.

### The defense helper: reading the ways in

The Defenses page (Colony → Defenses) and its defense lines rest on one reading of the map (`Defense/DefenseSurvey.cs`,
the pure part in `DefenseMath.cs`): from a sample of the map's edge, the cheapest way to the home area, with a door to
break costing about as much as thirty cells of walking, and the narrow places on those ways. A few rules keep it
honest about what the player has built:

- **The door rule.** A defense line's gate, every door in its wall and a door right behind its gate (a double gate)
  are the line's way through, shut or not: raiders who find a gate shut break it rather than walk far round. A door
  held open counts as open. So a shut gate never makes the survey propose a new way in (`DefenseMath.MapGrid.free`,
  `DefenseLineMath.MarkLineDoors`). The line's card says what a shut gate means and, where the door is shut, to hold it
  open in a raid.
- **Gaps a line holds.** The survey measures a pass where it's narrowest outside home, so a line often has the same
  pass found again a few cells off. A gap whose cells overlap the line's (gap, gate, wall), or that lies within 12
  cells of it with open ground between, facing no other way and either in line with its gap (the same pass, measured
  differently) or on the same sampled walks (at least half), is covered by that line (`DefenseLineMath.CoveredBy`). A
  colony door filling a gap further behind the line is a second line, not this one. Covered gaps are never offered as
  new lines; the overlay and the page say "covered by the south-west pass". The sampled walks are kept with the survey
  (`Result.walked`) so lines made later are matched without a new survey.
- **Plans on the way in.** A plan placed in front of a line (the trap corridor) follows the way raiders walk up to the
  gate (`DefenseApproach.PathToGate`: from the furthest cell in front that carries at least half the busiest stream of
  walks, the cheapest way to the gate over the raiders' side, then on out along the stream). Every spot along it is
  tried (its lane on each path cell 1 to 10 out, facing the line's way and the path's, each lane column, mirrored and
  not) and costed: walls to build, rock to dig (dearer), unexplored ground (dearer still), walls on the way raiders
  walk, less for the way running down its lane and for rock or walls standing as its sides. A spot on or behind the
  line's own cells never fits; nor does one beside the way (less than half its length of the way down its lane, or,
  tried with its walls standing, raiders walking round it). The cheapest are then tried on the map (`DefensePlanner.Plan`)
  until the game would take one. The planner says why it stands there, or why nothing fits and what would make room.
- **Walls that turn raiders away.** Before a step that adds walls is proposed (all but a fallback line, a keep or a
  mortar pit), the walks that cross the line now are walked again with the plan built (its walls as walls, its gate on
  the line as the line's way through, the rock it digs as open ground; `DefenseLineMath.CheckWay`). When fewer than half
  still come through, the player is told which way they'd take and offered the corridor with its sides open, closing
  the other gap first, or the plan as it is.
- **Reading again.** The survey is kept for a day, made again at most once a game hour when the colony's buildings
  change a lot, or when a wall, door, turret, trap, cover, fence or rock is finished or goes within six cells of a line
  or a gap (two Harmony hooks, `Frame.CompleteConstruction` and `Building.DeSpawn`, one check each), and at once with
  "Survey again". When the busiest way in moves, the lines say so for a day and a message says it once.

---

## Carrying and hauling (the player's request, 7 November 2026)

The player's words: "when pawns go get materials on the map that are further away, they should always fill up their
inventory with stuff on the way, not that they take just exactly what's required for the build. then they should also
store the hauled items eventually", and "hauling into rimfolk please, i'd anyways want to integrate harvest and haul in
here" (explicit requests: no proposal round). The code is in `Source/ColonyCouncil/Hauling/`, its pure rules in
`HaulMath.cs` (Tools/Tests `HaulTests`), the settings under What's on, Carrying and hauling (`CC_SetSec_Hauling`), the
words in `Languages/English/Keyed/Rimfolk_Hauling.xml`, the Almanac's page `Council_Alm_C_Hauling`.

- **Harvest and haul** (`HarvestHaul.cs`, `JobDriver_CarryToStorage.cs`): the player's own Harvest and Haul 1.2.0
  (`silvanalig95.HarvestAndHaul`, about 1,700 lines) as a Rimfolk system, with its behaviour, its sources (plants,
  also a plant cut to clear a cell; mining; deep drilling; shearing and milking; deconstruction and floors; fishing;
  chunks optional), its delivery rules (the same kind of work by skill, full and worth a trip at 5 kg, 8 hours at most,
  before needs, what comes first), its deconstruction leftovers hauled right after, its delivery job (the game's own
  unloading steps; the stack that didn't fit in the hands) and its settings with their defaults (the `haul*` fields;
  `HaulMath.StandaloneKeys` maps each of its keys). Its hooks are Rimfolk's `[HarmonyPatch]` classes now
  (`HaulPatches.cs`), each standing down on its own; collecting is off whenever the job choice isn't hooked, so pawns
  never hoard (the standalone's own rule).
- **Changed from the standalone, and why.** (1) A caravan's packs: nothing goes into a pawn whose inventory holds them
  (the standalone could merge a harvest into them). (2) What a pawn keeps: a delivery never takes what its drug policy
  carries, its inventory stock or the packed food it wants, as the game's own unloading keeps them
  (`HaulMath.Deliverable`, `Carrying.Kept`); the standalone delivered all it was owed, so a doctor who had used some of
  their stocked herbal medicine before the trip took stocked medicine to storage. (3) A load the job choice can't send
  (the setting off, the deeper hooks off, the standalone loaded) is queued for storage by the book's pass instead of
  waiting for a job choice that never comes. (4) Quest lodgers are left out. (5) With Pick Up And Haul, what is wholly
  Rimfolk's goes through that mod's unloading (below). (6) A hook that stands down on purpose says why in the self-check
  (`RimfolkPatches.StoodDown`, from a class's `StandsDownWhy`), rather than reading as a failed patch.
- **Standing down for the standalone mod** (`HaulLinks.Standalone`: its package id, or its tracker's class
  `HarvestAndHaul.HarvestTracker` in a loaded assembly): the ten hooks that are its own (the job choice's two, the
  drop of unused inventory, the seven sources) aren't applied at all (`Prepare`), so nothing is patched twice; the log
  says once at startup that Rimfolk has it built in and that it can be removed; the settings' heading says so
  (`CC_SetSec_Hauling_Standalone`), and so do the Almanac (its mod page and Rimfolk's) and README. The material run's
  two hooks and things on the way patch nothing the standalone patches, so they work beside it.
- **The hand-over once it is gone.** Its settings file (`Mod_*_HarvestAndHaulMod.xml` in the game's Config folder, kept
  by the game after a mod is removed) is read into Rimfolk's rows the first time Rimfolk starts without it
  (`haulImported`, once; only values that read as what they are, clamped to the sliders). In a save, the book reads the
  standalone tracker's loads from the save itself while it loads (`HaulBook`'s constructor,
  `HaulMath.ReadStandaloneLoads`: the pawns' load ids and the owed counts by def name) and takes them over once the game
  is in (`Carrying.TakeOver`): owed as before, never more than the pawn has, to storage at the next chance. The game
  logs one error about the missing class as it loads such a save; a pawn saved mid-delivery loses that one job.
- **The long material run** (`MaterialRun.cs`). Far is the straight distance from the material to the frame or
  blueprint, from the setting's 30 cells (`HaulMath.Far`): it is the shortest the walk can be, it costs nothing while
  the game looks for work (a path would be worked out for every candidate the work giver tries), and walls only make the
  walk longer, so a run never starts for a trip that is short in truth. The build area is round that frame, half the
  distance, 8 to 15 cells (`HaulMath.RunRadius`: never less than the game's own 8 for nearby frames, never reaching back
  to the stockpile). At the stack (a pre-init action on the game's `Toils_Haul.StartCarryThing`, for the game's own
  delivery job to a frame or blueprint only), the inventory takes what the area still needs past the hands' share and
  the stock already carried (`HaulMath.Extra`; the area's need is the game's own count, `GetSpaceRemainingWithEnroute`,
  leaving out what the pawn itself is bringing), from that stack (never what the hands are to take: hands first) and
  from the stacks of it within the pawn's reach (the cells round it), as far as the fill limit and Combat Extended
  allow. The next deliveries come out of the stock (a prefix on
  `WorkGiver_ConstructDeliverResources.ResourceDeliverJobFor` builds the game's own `HaulToContainer` job with the
  inventory stack as its thing, which the game's driver supports: it walks to the pawn itself and takes it from the
  inventory, as a bill takes carried ingredients). While a runner's stock holds all the area still needs of a material
  and it used the run within the hour, that prefix gives the other builders no delivery of it there
  (`MaterialRun.CoveredForOthers`; never for an order), so nobody walks back to the stockpile for it; the area's need
  for that is worked out on the book's pass. The run ends for a material its area needs none of any more, or after two
  hours unused (`HaulMath.RunOver`), and what is left is owed to storage; it goes there once the pawn stops building
  with the run's other material (`Carrying.StillBuilding`: one trip back for a run of two materials). Enroute claims
  aren't used for the cover: the game saves them with the map and releases them only on a delivery, so a claim for a
  stock in a pocket could outlive the run.
- **Things on the way** (`OnTheWay.cs`): on a walk of twelve cells or more that ends at storage (Rimfolk's delivery, and
  the walk back to a far stockpile for material, where the delivery job takes the place of the fetch and the fetch
  follows from storage), loose things near the straight walk that the game's haulers would take, at most six cells out
  of the way there and back (`HaulMath.OnTheWay`), six at most, only what fits, only for a pawn whose hauling work is
  on. The walk from the stockpile to the build takes none: they would ride away from storage and take the room the
  material needs. The game asks for a delivery job many times in one look for work, so the look is kept for the tick.
- **Pick Up And Haul** (`HaulLinks.Puah`: its `PickUpAndHaul.CompHauledToInventory` and `UnloadYourHauledInventory`, by
  name): a stack that is wholly Rimfolk's (`HaulMath.WhollyOurs`: none kept, none the pawn's own) is registered with its
  comp at the moment it goes to storage, and its own unloading job takes it there with what it hauled itself; Rimfolk's
  job keeps the rest (a stack partly kept, the stack in the hands, things on the way of a kind the pawn already had).
  Registered any earlier, its unloading would take a harvest before the work is done. Without it, Rimfolk's own job,
  built from the game's unloading steps: the game's own switch (`UnloadEverything`) unloads everything a pawn carries,
  its sidearms and what the player had it pick up and keep among them. **Haul to Stack** is left alone: storage is
  always found the game's way. **Combat Extended**: its bulk and weight limits, by reflection (the standalone's own
  way).
- **Saved**: `HaulBook.loads` (a GameComponent: an existing save gets an empty one), one load per pawn carrying
  something for Rimfolk (what is owed by def, the work's skills, a run's materials, its centre and last use), dropped
  once it carries nothing. One 250-tick pass (`Sched.RareHauling`, only while someone carries) and a per-tick look only
  while a spot waits to be looked at again.
- **Not checked here**: none of it could be run in the game. The riskiest parts are the take-over from a save (the
  standalone's XML read without the mod), the pre-init action on every `StartCarryThing` (it returns at once for any
  other job), the run's job from the inventory (the game's own support for it, not seen with frames), the cover for the
  other builders (it may hold them back a while when a runner is stuck: an hour at most), and the hand-over to Pick Up
  And Haul (its comp read by name).

### Everything else work makes (the player's request, 7 November 2026)

"They should always take any 'produced' item or resource into their inventory when they have space, to later store
it, for any possible item creating or harvesting interaction in the game and mods" (explicit: no proposal round). The
code is `Hauling/WorkProducts.cs` and its hooks `Hauling/WorkProductPatches.cs`; the pure rules are in `HaulMath.cs`
("Everything else work makes", `HaulTests`). Delivery is harvest and haul's own: the same kind of work by skill or by
work giver, full and worth a trip, the longest hold, before needs, never instead of rest, Pick Up And Haul's unloading.

- **Bills** (`WorkProducts.PocketBill`). The game's last toil of a bill (`Toils_Recipe.FinishRecipeAndStartStoringProduct`)
  makes the products (`GenRecipe.MakeRecipeProducts`), counts them for the worker's records
  (`RecordsUtility.Notify_BillDone`) and then places them by the bill's store mode: drop on floor near the worker, or
  the first product in the hands to the best stockpile or the picked one (`HaulAIUtility.HaulToCellStorageJob`) and the
  rest near the worker, or on the floor when no cell takes it. Rimfolk wraps that toil's init action to know the
  worker is finishing (`WorkProducts.finishing`), and in a postfix on `Notify_BillDone` (last, so other mods counting
  see every product) takes the products off the list before the game places them: only for a bill taking them to a
  stockpile (`HaulMath.BillPockets`: best or picked; never drop on floor, a mod's own store mode, a bill the player
  ordered, surgery (`Bill_Medical`) or a building's own production (`Bill_Autonomous`, the mech gestators), nor a project
  of 10,000 work or more, whose first product the game names in a tale), only kinds that may go in, only what a store
  cell takes (in the picked stockpile for that mode), and only as many as fit; a partly fitting stack is split. The game
  then places or carries the rest its own way, so "too heavy to carry" is as before. The quests that count what is made
  (`QuestManager.Notify_ThingsProduced`) are told of what went in, before it goes in (merged into a stack already
  carried, the piece is gone). A picked stockpile is remembered by one of its cells per kind (`HaulLoad.into`); the
  delivery finds a cell in that slot group or its storage group (`Carrying.StoreCellFor`), else the best storage (where
  the game's haulers would take it anyway), and such a kind never goes through Pick Up And Haul's unloading, which knows
  only the best. A "do until" bill would make more while the products ride in a pocket (the game counts the hands,
  `RecipeWorkerCounter.GetCarriedCount`, not inventories): postfixes on `RecipeWorkerCounter.CountProducts` and the stone
  block and butchering counters add what pawns owe to storage of it (`WorkProducts.CarriedFor`, by the bill's own
  `CountValidThing`; with "count equipped" the game counts inventories itself; with a stockpile to count in, only what
  goes there).
- **Everything else, one generic catch** (no hook per mod). Three hooks set whose job runs a toil or a tick
  (`JobDriver.TryActuallyStartNextToil`, `DriverTick`, `DriverTickInterval`: prefixes and finalizers that put the one
  before back; `[ThreadStatic]`, so other threads see none). While one is set, an item made (`ThingMaker.MakeThing`) or
  dropped out of a building's container (`GenDrop.TryDropSpawn`, the egg box's eggs) is noted for that job; a part split
  off a stack (`Thing.SplitOff`: a hauler's handful) and anything dropped out of a pawn's hands or inventory, a corpse or
  anything else is never one. Only for a work job (a work giver gave it), not ordered, of a pawn that carries, and not a
  job one of the sources of their own covers (plants and sowing, mining, the deep drill, shearing and milking, taking
  down and floors, Odyssey's fishing, bills, Rimfolk's delivery, Pick Up And Haul's unloading) nor building work. Two
  moments take it: the carry step (the material run's hook on `Toils_Haul.StartCarryThing` adds a pre-init action)
  about to put such a thing in the hands while one of the job's targets is a storage cell takes it into the inventory
  instead, whole or not at all, and ends the job there (`EndJobWith(Succeeded)`: the preInit loop stops when the toils
  are cleared); and the job's end (a prefix on `Pawn_JobTracker.CleanupCurrentJob`, before the game drops what the hands
  hold) takes what still lies loose within 4 cells of the pawn and some storage takes, as far as it fits, the first that
  doesn't fit carried in the hands on the delivery. Not while drafted, downed or in a mental state, nor for a job that
  errored. At most 40 things a job; the note goes with the job.
- **Families and settings** (What's on, Carrying and hauling; each needs "Pawns carry what their work drops"):
  `haulBills` (bills), `haulBuildings` (a job whose first target is a building: the fermenting barrel, the egg box,
  Anomaly's bioferrite harvester and the entity on its holding platform, the mods' barrels, presses, processors,
  beehouses and apiaries), `haulOtherWork` (any other work job: a skull extracted for Ideology, the mods' work; taking
  a mechanoid apart and removing a mechlink are orders, so they go as before), and a mod's fishing job by its name
  (`HaulMath.FishingName`) under the existing `haulFishing`.
- **What stays where it is** (`WorkProducts.PocketableDef`, `Allowed`): medicine (the doctors' to reach), weapons (Simple
  Sidearms, in the player's list, reads a weapon in the inventory as a sidearm), what dissolves (`CompDissolution`:
  wastepacks), what the weather ruins or hatches (`CompTemperatureRuinable`, `CompHatcher`: fertilized eggs, wort),
  Biotech's xenogerms, genepacks, embryos, ova and mechlinks (used where they lie), corpses, minified things and chunks
  (by their setting). Meals (`HaulMath.MealsToPocket`): all while nobody is hungry, else only past what the hungry need
  and the meals in reach don't cover (a working paste dispenser feeds all); a load with meals is held two hours at most
  (`HaulMath.HoldHours`), and the book's pass sends it at once while someone is hungry and the meals in reach don't feed
  them all (`HaulMath.MealsWanted`). The same work for a load is also the same work giver (`HaulLoad.givers`): emptying
  egg boxes and barrels is hauling work, which no skill names.
- **The player's mods** (`docs/player-mods/`, the raw defs, and the sources read where they were to hand): the Vanilla
  Expanded Framework's item processors (`IP_RemoveProduct`, Vanilla Brewing and Cooking Expanded's barrels and presses,
  VFE Settlers and Classical), its pipe processors (`PS_PickUpProcessor`), Medieval 2's apiary
  (`VFEM2_TakeHoneyOutOfApiary`) and the Processor Framework's emptying (`EmptyProcessor`) make the product, place it
  and carry it to a storage cell with the game's carry step, as the fermenting barrel does; the Processor Framework's
  last product of an emptied processor is left on the ground (its job ends first), and the job's end takes it. VEF's
  draining of a marked tank (`PS_DrainFromMarkedStorage`) and Anomaly's bioferrite extraction leave it beside the pawn.
  Alpha Bees' beehouses, Tabula Rasa's producers, Rimefeller's autoclave, Dubs Bad Hygiene's emptied latrines and septic
  tanks and the other mods' producers are caught the same way if their work makes an item as these do; their code
  wasn't to hand. Vanilla Fishing Expanded's and PokéWorld's fishing go by the fishing switch.
- **Saved**: two lists more in each load (`givers`, `into`, modelled in `Tools/save_estimate.py`), emptied with the load.
  The notes of what a job made aren't saved: a job loaded mid-way goes the game's way. No Sched slot, no new pass.
- **Not checked here**: none of it could be run in the game. The riskiest parts are the three hooks on the job driver's
  hot paths (a prefix and a finalizer each, nothing else), the job ended from the carry step's pre-init action, the
  products taken off the game's list in `Notify_BillDone` (a mod reading the list after it sees fewer), and interplay
  with Common Sense and Gastronomy (a restaurant waits up to two hours for meals in a cook's pocket).

## The trade screen (the player's request, 7 November 2026)

"Can you please optimize the trade screen ui? things like sorting by price, recommended trades etc. i also just
installed vanilla trading expanded so integrate that as well." The game's own Dialog_Trade, for every kind of trade,
made quicker to read without taking it over (`Source/ColonyCouncil/Trade/`):

- **Sorts are defs, not patches.** The game lists every `TransferableSorterDef` in its two "Sort by" menus, so the five
  new sorts are defs (`Defs/Misc/Rimfolk_TradeSorts.xml`) with comparers (`TradeSorts.cs`), the highest first: trader
  pays, trader asks, price per kg, best deal, suggested first. The menus are shared with the caravan and transport pod
  windows, which have no prices; a small transpiler on the menu's list (`Patch_TradeSortMenu`) leaves Rimfolk's sorts out
  there, and without the deeper hooks they fall back on market value.
- **The advice is read, not invented** (`TradeAdvice.cs`, once a trade opens): the plan ahead's stock rows, food days
  and shopping list, the blueprints' materials (`Making.BuildingWants`), the record of use, the journey being packed,
  wishes open, promised and kept, the drug policies, the research under way, and Vanilla Trading Expanded. Only what the
  colony has and uses counts as a need, so every age gets its own advice; a bargain fills a stock the colony already
  keeps, never a new one.
- **The reserve, in one rule** (`TradeAdviceMath`, pure and tested): keep the need and as much again, the need being a
  season's use by the record or a floor by kind and headcount, plus what is set aside. Food by days, weapons and
  clothes by spares (one a kind for every three people). Only a surplus worth a word (a tenth of the stock, 40 silver)
  is marked.
- **The price against the usual,** not against market value alone: a trader usually pays 60% of a thing's worth and
  asks 140%, moved by the negotiator and the difficulty, so 1 is "as usual" for any negotiator and a fifth better is a
  good sale or a bargain. With Vanilla Trading Expanded the worth is the good's own (its market's change divided out),
  so its demand and surplus show as good sales and bargains.
- **The basket is suggest-only:** buys first (each need answered once, the cheapest for what it gives), paid by the
  colony's silver and its sales; then sales as far as the trader's silver and the buys go; chosen again on what was sold
  if the trader runs short. It sets counts with the game's own `AdjustTo` and nothing else; the game's Accept trades.
- **Readouts only where the game has none:** silver after the trade in the price columns the silver row leaves empty,
  food days after a trade at home; a caravan's mass and food days after the trade are already the game's.
- **Other mods.** Vanilla Trading Expanded (read by name in `Integrations/VanillaTrading.cs`) patches no part of the
  screen: it moves a good's market value, so every price already carries its market. Its contracts and banks are read
  from its TradingManager; its switch for the vanilla value is set back at once, as its own windows do. Trade Helper's
  source isn't public (its workshop page and a stack trace show a postfix on the dialog's DoWindowContents drawing a
  side panel in 300 pixels it adds); Rimfolk draws inside the game's own layout, beside its buttons and in empty
  columns, never in that panel, and with it loaded leaves out the silver after the trade.
- No letters, messages or saved state: the basket's memory lives with the open trade.

### Price history and the colony's record (the player's request, 7 November 2026)

"Does vanilla trading expanded influence price developments? could you in that case have things like price history,
price compared to market price and other helpful and informative data." It does, three ways: the colony's own trades
(every three days, a chance of the market worth traded of a good over 1,000 silver, down for a sale, up for a
purchase, by 1% to 10% and more past 1,000 silver), its news, and a slow daily drift back toward the vanilla value. It
also keeps one price a day of every good it trades (`PriceHistoryAutoRecorder.records`). Rimfolk reads that, adds no
economic mechanic, and keeps one small record of its own:

- **Read, not modelled** (`Integrations/VanillaTrading.cs`, by name): `priceHistoryRecorders` and each one's `records`,
  `thingsAffectedBySoldPurchasedMarketValue` (the colony's trades VTE hasn't reckoned yet), the private `allNews` and
  each news' `AffectedThingDefs()`, `priceImpact` and `priceImpactStartTick`, and the setting
  `playerTransactionImpactMultiplier`. The vanilla value goes through VTE's own `outputOnlyVanilla` switch, set back in a
  `finally` in one place. "So many days ago" is the price that many records back, as VTE's terminal reads it.
- **The rules are pure** (`Trade/TradeMarketMath.cs`, tested): against the usual and the 30 days' average; the change
  over 7 and 30 days (over what there is when the history is shorter); the 60 days' high and low, "at" one within 2%
  where ten days or more spread a tenth; a notable move a tenth in a week (3 days at least) or a fifth in a month (10
  days at least). VTE's rule for the colony's trades becomes words only: "may" from a chance of a tenth, "likely" from
  two in five, "almost surely" from four in five, "sharply" from three of its steps; a trade against what was traded
  lately "evens it out", and where the earlier trades still outweigh it they are named instead.
- **Where it shows.** A card (an immediate window, Super layer) beside the mouse on either price of a row, on the left
  so the game's own price tooltip keeps the right, after the tooltip's own pause; a 28 by 14 line in the row's icons for
  a notable move only (`Patch_TradeRowMark`); the Market window from a button in the silver row's empty name area (drawn
  with the row's icons so the silver's own tooltip stops short of it, with a fallback after the row); a sixth sort,
  `Rimfolk_PriceChange`, left out of the menu without VTE's history and, like the rest, out of the caravan and transport
  pod windows. The marks' tooltips gain the market's reasons (`MarketHint`) for a sale or buy already marked, never a
  mark of their own. Nothing is drawn in Trade Helper's side panel.
- **The colony's record** (`Trade/TradeRecord.cs`, `TradeBook`, saved): taken in a prefix on the game's
  `TradeDeal.TryExecute` (the hook `Epithets/CouncilRecords.cs` already had for the negotiator's count; the counts are
  gone after it) and written in its postfix only if the trade went through. Nothing in Rimfolk recorded goods or prices
  before: the faction memory notes a trade's total worth and a sold captive, nothing a piece. Silver trades only, never
  gifts, people or animals; rows of one good, way and quality merged at the average price; the last 5 lines of a good
  (a thing and its stuff), 50 goods, the one traded longest ago going first; about 25 KB at the cap
  (`Tools/save_estimate.py`). Works without VTE.

## Algorithms, not AI

**All logic is plain rules and weights.** It's deterministic, needs no API key, works
offline, costs nothing, is fast, and can be balanced. Every wish and signal is a small
rule: "if this condition is true, propose X, fulfilled when Y."

The mod has no AI or LLM component and doesn't depend on any other mod. The flavour
comes from hand-written lines, six "tones" per wish (plain, warm, blunt, cheerful,
gloomy, cold) chosen by the colonist's traits and mood, plus vanilla social-log
interactions where colonists grumble about their wishes to each other.

**"Random but sensible"** comes from *weighted* random picks: every candidate wish gets
a weight (how bad the related thought is, how strong the passion, trait multipliers),
and the generator rolls among them. Same colony, different stories each playthrough.

---

## Fitting the world: tech level, materials, map

A medieval or tribal colony must never be asked for turrets, heaters or hospital beds it
can't build. Every suggestion goes through one gate (`Avail`):

- **Research**: a building is only suggested if its research is finished and it shows up in
  the build menu.
- **Power**: anything that needs electricity is skipped until Electricity is researched.
- **Purpose, not names**: buildings are found by what they do (heats, glows, holds
  patients, is a crafting bench for skill X, gives recreation of kind Y). So a medieval colony
  gets a torch lamp or a campfire, an industrial one a standing lamp or a heater, and modded
  furniture (e.g. Medieval Overhaul) counts automatically.
- **Materials**: room plans pick the wall and furniture material the colony has most of;
  wood counts as available when there are trees, stone blocks when there are chunks.
- **The map itself**: "tame an animal" only when tameable animals are roaming nearby,
  "bury your friend" only when the body is on the map, "a prosthetic" only when one can be
  made or is in stock, no winter advice on space maps.
- **Era of the colony**: the research advisor suggests projects from the colony's own tech
  level first.
- **Ambitions** are offered only if every goal is possible right now. "A ring of iron"
  (turrets) never comes up for a medieval colony, but "teeth in the mud" (traps) does.

---

## Playing nicely with any modlist (rules for every integration)
Rimfolk is played with a 200+ mod list (medieval before, industrial and spacer now), but must work just as well with
any other list, including a plain vanilla run. Every feature that touches another mod or a game system follows these
rules:

1. **Soft integrations only.** A mod-specific feature switches itself on when that mod is active and is simply
   absent otherwise: defs are matched by name at runtime (`GetNamedSilentFail`, type names, package ids), never by
   XML cross-references to another mod's defs, and mod-only content is gated (`MayRequire`, mod checks). Removing a
   mod mid-save must not leave errors or broken entries behind.
2. **Never harder or emptier without a mod.** Integrations add flavor and options; they don't carry the balance.
   With a mod missing, the base experience stays complete and fair.
3. **Only what the colony has unlocked.** Wishes, ambitions, advice, room plans, decrees and dilemmas only ask for or
   suggest what the colony can research, build, make or buy now. Nothing pushes the player into a tech line or a
   mod's late-game system (diplomatic marriages, faction wars) they haven't chosen to engage with.
4. **No per-run adjustment.** Era (medieval or industrial) and mods are detected automatically; medieval content stays
   out of an industrial run and the other way round.
5. **Harm at the game's own rate.** Until November 2026 moments between colonists never injured anyone (contests were
   friendly: arm wrestling, races, riddles). Since then harm may come where it's flavourful or interesting, at a rate
   comparable to the game's own chance of something bad happening, never gratuitous.
6. **Most mods get no integration, and that's fine.** A big modlist is mostly nice-to-haves the player may never use
   in a given run. Rimfolk only has to read them correctly (no false warnings, no impossible wishes, no colonist
   counted as gone). Deeper integration is kept for the few mods the player always runs, and even those stay soft.
7. **Harmony is the only hard dependency.** It's declared in About.xml, so the game's mod menu shows it and offers it
   when it's missing. Nothing else is required: no other mod's assembly is referenced, and with any other mod missing
   Rimfolk loads and plays without errors.

## Limits and risks

- **Heuristics are approximate.** Defense strength in particular is hard to compute
  (a killbox with 2 turrets beats 6 turrets in the open). Present it as "rough
  read" with confidence, not as truth.
- **Room-quality judgments.** We can read RimWorld's own room stats (impressiveness,
  space, beauty, cleanliness, role), which keeps us honest and in line with the game's
  own mood logic.
- **Modded content.** Avoid hard-coding specific buildings. Detect by category
  instead, e.g. "a bench whose recipes use the Crafting skill", or "a building that
  provides recreation". Modded benches and furniture then work automatically.
- **Performance.** Run checks every in-game hour (or less often), never every tick, each on a tick of the hour of its
  own (see "Ticking"). Cache results.
- **Exploits.** Rewards must not be farmable: cooldowns, no repeat wishes, rewards scale with effort.
- **Nagging.** The biggest design risk. Rate limits and "Not now" are essential, and
  the default is Gentle.
- **Overlap with vanilla alerts.** RimWorld already warns about some basics (e.g. no
  beds, low food). The Advisor should go beyond those (forecasts, trade-offs, *why*)
  rather than repeat them.

---

## Technical overview

Standard RimWorld mod: XML defs plus a C# assembly, depending on Harmony. Most of it hooks in
through vanilla extension points (game and map components, a main tab, inspector tabs, alerts,
thought and interaction workers, a designator, a choice letter). Where those can't reach, small
Harmony patches do (Source/ColonyCouncil/Patches): postfixes where possible, each applied on its
own so one failure doesn't stop the others, each logged once if skipped, all switchable at
runtime through the "deeper game hooks" setting. A patch has to earn its place: it changes how
the game behaves in a way the extension points can't.

```
About/About.xml, About/Preview.png
1.6/Assemblies/ColonyCouncil.dll
Defs/
  WishDefs/            # 23 data-driven wishes with flavour lines (add new ones in XML)
  AdvisorSignalDefs/   # 14 advisor checks with "why it matters" tips
  AmbitionDefs/        # 20 ambitions with goals and pitch lines
  RoomTemplateDefs/    # 13 room templates for the planner
  ThoughtDefs/         # wish granted, ambition achieved, spoke up for me, ...
  InteractionDefs/     # colonists talking about their wishes and the ambition
  Misc/                # the Council main tab and the meeting letter
Languages/English/Keyed/
Patches/               # one conditional addition: the strain part on global work speed
Textures/ColonyCouncil/
Source/ColonyCouncil/  # C#; builds against the Krafs.Rimworld.Ref NuGet package
Tools/DefValidator/    # offline XML checker against the real game types
Tools/ApiDiff/         # will the built DLL still run on another game version?
Tools/Tests/           # tests for the room rotation math
Tools/make_icons.py    # draws the interface icons (Textures/ColonyCouncil/Icons) and their contact sheet
Tools/perf_audit.py    # ranks the code's hot spots without the game (docs/perf-audit.md)
Tools/save_estimate.py # how each saved collection grows over five years (docs/save-size.md)
```

Core types:

- `WishDef` / `WishWorker`: a worker lists candidate wishes for a colonist (with a weight
  and a reason) and says when a wish is fulfilled.
- `Wish`: the live, saved instance (who asked, for whom, about whom, expiry, quote, co-signers).
- `CouncilGameComponent`: generates, tracks, grants and expires wishes hourly; owns the
  ambition tracker and personal touches; tells the chronicle (`AddChronicle`, into the one history).
- `AdvisorSignalDef` / `AdvisorSignalWorker` + `CouncilMapComponent`: advice per map,
  refreshed hourly, ranked by severity, urgency and confidence.
- `AmbitionDef` + goal classes + `AmbitionTracker`: council meetings and the active ambition.
- `RoomTemplateDef`, `TemplatePlanner`, `Designator_PlanRoom`: room plans with a ghost preview,
  rotation, material choice and a spot finder.
- `Avail`: the tech, research, material and purpose checks described above.
- `MainTabWindow_Council`, `MainTabWindow_World`, `MainTabWindow_Defenses`, `MainTabWindow_Planning`: the four
  windows (see "Rimfolk's windows").

Things the game exposes that this relies on (all available to mods):

| Need | Game data |
|---|---|
| Pawn's bad thoughts | Mood thought list per pawn |
| Bedroom and room quality | Owned bed and room, room role, room stats (impressiveness, space, beauty) |
| Traits, passions, skills | Trait set, skill records with passion level |
| Work assignments | Work priorities per work type |
| Partners | Direct relations (lover, spouse, fiancé) |
| Raid strength | Storyteller threat points, colony wealth |
| Food | Total human-edible nutrition on the map |
| Seasons | Average temperature per twelfth for the map's tile, growing-season check |
| Power | Power net gain rate and stored energy |
| Building | Placing standard blueprints (so pawns build them normally) |
| Rewards | Mood memories, inspirations |

### Homes: what a colony is

A colony is a player home map, known by its settlement's world object id (`map.Parent.ID`, which outlives the map's
own id). `Lean/Homes.cs` is the one place that says which; its rules are pure (`Lean/HomesMath.cs`) and tested.

- `Homes.All`: the player's homes in the order they were founded, those on the ground before those in orbit.
- `Homes.Seat`: the colony the council sits at. The first home founded, unless a seat is stored (a saved id, set by
  "Make this the seat" on a colony's card: below, "A fresh start") and is still a home. It doesn't move as people do.
- `Homes.Of(pawn)`: the colony someone belongs to. On a home map now (held there too, as in a casket): that one. Else
  the home where they own a bed. Else the last home they lived on (set once they've been on it a day without a break,
  kept per colonist, forgotten once they're dead, discarded or no longer the colony's). Else the seat.
- `Homes.People(map)`: the free colonists who belong to a home; a meeting's members are those of them with a say.
- `Homes.Name(map)`: the settlement's name, for texts that have to tell two colonies apart.
- `Homes.Changed`: a home founded or lost, noticed in the council's hour (the council's book of colonies listens: below).
- `Homes.LastHome(pawn)`: the last home someone lived on for a day, while it's still a home.

With one colony every answer is that colony, so it plays as before. `MapFacts.BusiestHome` and
`HappeningTracker.HomeMap()` (the seat, on the ground) keep their names but return the seat; "the colony" used to be
the home with the most colonists on it, which moved as people did. New code asks `Homes`: the seat for what the colony
as a whole has, a loop over `Homes.All` for what each home has, and `Homes.Busiest` only for what follows people about
(nothing asks it since social events are worked out at each home, step 7b). Rimfolk's own event timer runs per home (the seat carries on an older
save's single timer), a memo keeps its colony so its follow-up (a guest, goods brought back, an order collected) comes
where it was set going, talks with visitors start on every home, and a raid is written up by those at the home raided.

### Many homes

Each colony has a council of its own (stage 0, step 7a): `ColonyState`, one per colony, known by its key
(`Homes.KeyOf`), kept in the council component's `colonies` and saved with it (the council already runs and saves
everything it holds, so the trackers stay what they were and only move one level down). Its rules are pure
(`Lean/ColonyStateMath.cs`) and tested (`Tools/Tests/ColonyStateTests.cs`). `ColonyState.For(map)` gives a home's (the
seat's for a map that isn't a home), made when a home first needs one; `ColonyState.Of(pawn)` someone's
(`Homes.Of`); `ColonyState.Ticking()` the councils that run this hour, each home's in the order they were founded.

**Per colony now.**
- *Offices and elections* (`OfficeTracker`): one set of seats per colony. Only its own people at home vote and stand;
  the election is held at its own home, by its own year; its peacemaker cools rivalries between its own people; a
  seat's edge and the colony-wide edges reach only its people. An officer who has gone to live at another colony (there
  now, and a day there) steps down; away on a caravan or a short visit they keep the seat. `OfficeTracker.HeldBy(pawn)`
  finds anyone's office, their own colony's first. The elders sit with their own colony's young; days as physician
  and marshal count in every colony.
- *Ambition and meeting* (`AmbitionTracker`): one ambition and one meeting per colony, each at its own home
  (`MeetingMap`); the meeting's gathering, its waking and its letter find their colony by the map. One whose home is no
  longer ours ends quietly (clean-up asks "still our home", not "is the map still there").
- *Decrees* (`DecreeTracker`): one per colony. What makes it needed, its petitions, the threats it counts, its tithes
  and alms (from its own stores, stretched by its own quartermaster, the goodwill to the nearest friendly settlement
  measured from its home), its hediffs, curfew hours, practice, banked fires, grief fading, the backlash of a repeat and
  the daily trust are all its own people's and home's. The declaration's letter counts its own for and against. Decree
  moods, the heart's strain and relief, a quarantine's contagion, a clean sweep's work order and the petitions' setback
  read the decrees of the person's own colony. The one faith a decree names is still one for the game.
- *Dilemmas and traditions* (`DilemmaTracker`): each colony keeps its own rhythm, cooldowns (the pool's colony key)
  and traditions, and casts its questions at its own home; a raid's aftermath is asked at the colony raided. A question
  waiting for its answer holds back only its own colony (one about no colony, or a home no longer ours, holds back all).
  The answer goes to the council of the colony it's about. Secrets, a coming of age, kin in the cells and a partner from
  outside ask the colony concerned.
- *Wishes and trust*: the wishes stay one list, each counted under the colony of the one who asked. Each colony rolls
  for its own wishes, from its own people, under its own decrees and its own cap on open wishes (the event wishes, a
  dream's and an envious one's too); a petition is joined only within a colony; the speaker raises trust in their own
  colony; "never the colony's last few people" counts each colony's own; `ColonyTrust(map)` and the steward's hold on
  standards are per colony. Trust itself stays per person.

**Per colony too (stage 0, step 7b).** The other books kept per colony follow the council's rules through one helper,
`ColonyBook` (in `ColonyState.cs`: a home's entry made when first needed, the seat's for a map that isn't a home, an old
save's single entry placed at the seat, a lost home's kept thirty days and carried by its people when no home is left),
over any `IColonyEntry`.
- *Happenings*: a later event picks up only its own colony's memos (`Memos(kind, days, map)`, `HomesMath.MemoAt`; an
  old memo, or one of a home no longer ours, is the seat's): a rematch, a beast's young, pilgrims' thanks, a regular
  buyer, a quarrel remembered, a guest expected, an orphaned young, a commission and its patron. Those that follow a
  person on the map (a deserter, a lost child's finder, lovers, kin, a bad fortune) were per home already. An
  unanswered event letter holds back only its own colony (`WaitingAt(map)`, `HomesMath.HoldsBack`; one about no colony,
  or a home no longer ours, holds back all).
- *Calendar*: one per colony (`Calendar/ColonyCalendar.cs`, kept in the calendars' book, `CalendarTracker`, the same
  saved component). Each settlement has its own founding day (the game's start for a colony settled in the game's
  first two days, else the day its settlement was made: `CalendarMath.FoundedTick`), its own days, date (`seenDay`,
  `seenYear`), rest day, talk and traditions, and keeps them at its own home; a day's letter keeps its colony and holds
  back only that colony's days. A victory is filed at the battle's colony, a death at the dead's, a wedding, a foal, a
  companion and a camp's gift at their own; harvest, snow, homecomings, hunts, trades, rescues, the first child and
  one of the first members (joined in its own first year: `CalendarMath.FirstMember`) are read from each colony's own
  people and land. The seat's calendar keeps what is the colony's as a whole: the nemeses and the world's events. The
  founding event keeps only the founding day of the colony settled with the game; a later colony keeps its own. The
  Calendar page shows the colony in view (with the colony header); the region window's Here and away, This week
  and the HUD list each colony's coming days, named.
- *Officers at work*: Interplay's officers are each colony's (`Interplay.Officer(def, map)`, `OfficerOf`, `Holds`,
  `Officers`): the keeper of days, the peacemaker, the steward and the chaplain of the colony where it happens; each
  colony's physician, herdmaster (`AnimalFacts.HerdmasterAt`, `IsHerdmaster`), chaplain, warden and marshal keep their
  own days. The chaplain's faith factor, a steward's thrift, the speaker who writes the neighbour letters and leads a
  talk, a scenario's first council, an office a question asks for, and the annals' list of seats (each colony's,
  named) read the colony concerned. A quarantine shuts only its own gates (`DecreeWork.VisitorFactor(map)`: with one
  colony it stays in the storyteller's base chance, with more it is in the event's chance at its target).
- *Questions asked of the colony concerned*: the harvest home, supper and name-the-year traditions an event reads, a
  camp's trouble, the resident beast, a guest's invitation and their kin, a shared name and a guardian (each colony's
  council looks among its own), asking again after someone was kept from moving on.
- *Camps*: one `CampHome` per colony (in `CampTracker.homes`): its visiting camp, its neighbours settled for good, its
  war party, and its own clock (the day's try, the gap since its last camp, the diary: `CampStoryMath.RepickTry`,
  `InGap`, `PutOff`). Camps are rolled at each home on the ground; allies answer a threat at any home;
  `CampTracker.VisitingAt(map)`, `HoldingAt`, `WarBandAt` are what everything else asks.
- *Encounters, the herd and the beast*: strangers are met at each home on its own (`EncounterHome`: the day's count,
  the last, each kind's cooldown; their questions asked of that colony); the herd's year and the beast that stays are
  one per colony (`comp.HerdOf(map)`, `BeastOf(map)`).
- *The social web*: a fight per home (`Social/Battle.cs`, `SocialWeb.BattleAt(map)`, `BattleOn`, `AnyBattle`): two
  fights at two homes are two fights, and a victory is felt at the home that fought it. Social events are worked out
  and held at each home. Groups form within one home (`GroupMath.Apart`: people of two colonies hold to each other not
  at all, so they never form, join or merge one group, and someone who moved away drifts out of their old one).
- *The year's letter*: with more than one colony, a part for each colony under its name (its herd, its beast, its
  neighbours), then the world once. The annals count big threats at each home (two raids at once are two) and compare
  the whole colony's wealth; the Overview's wealth tile names each colony's.
- *Names*: a colony key on each name (`EpithetMath.Key`; the name alone with one colony, as always): the Healer is the
  best healer of that colony, and taking a name over makes a rival only within one colony. A name from one colony's
  days belongs to its holder's colony. The game's founders and firstborn are the colony settled with the game, else the
  seat's (`EpithetMath.FirstColony`); a colony settled later has its own (those who were its people in its first two
  days, the first child on its own calendar).
- *The advisor and automations*: with more than one colony "Not this" is set at the home it's said at
  (`SuggestMath.HomeKey`, the list to bring it back names the settlement), a step done waits for its check only at its
  home (`SuggestMath.SameHome`), and an automation's cooldowns, materials budget and daily caps are each home's own
  (research stays one). What was set aside with one colony stays set aside everywhere. The game-wide advice (a mission
  near its deadline, a plague or famine, a colony split between faiths) shows at each home on the ground.
- *Journeys*: one plan per home at most (`JourneyBook.PlanAt(map)`, `PlanOf(pawn)`), each setting off from its own home.
- *News*: what was told with no map but is about a home goes through `Tell` with it, so it names the settlement.

**Still one for the game**: the chronicle and the annals (one book, each colony named in it, the Chronicle page filtered
by the colony in view since release 4), the wishes' list, trust, the heart's episodes, the remembered camps that may come
back, the world's news. The colony's character was one until stage 3's release 4 made it each colony's own (below, "Many
colonies in play"). The one faith
a decree names stays one for the game: the game keeps one faith per people (`Faction.OfPlayer.ideos`), not per map, so
two colonies naming two faiths would pull the same people two ways. `comp.offices`, `decrees`, `ambitions` and
`dilemmas` still read the seat's council where the colony as a whole is meant.

**An old save** puts everything it had under its one colony: its offices, election, decrees, ambition, meeting,
dilemmas and traditions become the seat's council, untouched (`ColonyState.FromOldSave`, placed when the seat first
asks). An election letter from before has no map and reads the seat's council. Its calendar, camps and their clock, a
fight going on, encounters, herd, beast and journey plan are read from where they always were into the seat's entry
(each book reads the old fields into an entry with no home, which the seat takes); its names stay under the name alone
and count for the colony of the one who holds them; its memos, letters and suggestions with no colony are the seat's
(or everywhere, for "Not this").

**A home lost** (abandoned, taken, handed over; `Homes.Changed`): a meeting, an election and an ambition there end
quietly; the council is kept thirty days (`ColonyStateMath.KeepLostDays`) in case the home comes back, then let go.
With no home left, its people carry it, and the next home founded (the seat) takes it; a new seat that is the only home
and has no council yet takes the one of the home just left. So a colony that moves keeps its council, whether it gives
up the old home first, lands a gravship, or settles and gives up the old home within the hour. A colony founded beside
the first has a council of its own, and people who move to a colony that has one join it.

**The pages** show the colony in view (`UI/ColonyView.cs`: the map on screen when it's a home, else the seat): the
Decrees page with the offices and the election, the Ambition and Dilemmas pages, the council HUD panel, the calendar's
election and traditions, the Council tab's counts. With more than one colony those pages name the colony at the top
("The council of Oakhold (the seat)") with a button to go to another colony, which changes the map in view. This
week lists every colony's petitions and meetings, each named; texts that mix colonies name the settlement through
`Tell`.

With one colony there is one council, every colony check is that colony (`ColonyState.Has` and `Of` answer it at once),
the same people are taken in the same order, and the same rolls are made in the same order: it plays as before. Step
7b keeps to the same: lists are filtered by colony, never sorted again; each per-home loop has one pass with one home;
keys (names, suggestions, automations' cooldowns) are what they always were with one colony; and where the place of a
roll moved (a quarantine's factor) it stays where it was with one colony. Tests: `Tools/Tests/ManyHomesTests.cs`.

#### A fresh start (stage 3, release 1)

A new colony founded in the same world, without giving up the one there is (docs/design/stage3-many-colonies.md,
section 1 without a scenario's parts, 3a and 3c; the player's decisions of 6 November 2026). `Source/ColonyCouncil/Colonies/`:
the rules are pure (`FoundingMath.cs`, tested in `Tools/Tests/FoundingTests.cs`); `Foundings.cs` runs it in the
council's hour, right after the homes (`Homes.Hourly`), and only while there is a founding or a question about moving
(nothing per tick, no schedule slot); the place picker and the founding window are `FoundingWindow.cs`, the newcomers'
window `NewcomersWindow.cs`, the questions `FoundingLetters.cs`, the seat `ColonySeat.cs`, the cards and the Ours lens
`ColonyCards.cs`; what it keeps rides the homes' own component (`HomesFounding.cs`).
- **The place.** "Found a new colony" on a colony's card (a home on the ground, one founding at a time) opens the
  game's world targeter (`WorldTargeter.BeginTargeting`), with Rimfolk's facts of the tile under the cursor beside it
  (`FoundingPlace.Facts`): whose land (Faction Territories, `WorldMechanics.TerritoryOwner`), the towns within 12 tiles
  and their stance, how remote (`RegionMath.Remoteness`, the region's own five words), the days by caravan from each
  home (the game's estimate), the land, and the game's checks (`TileFinder.IsValidTileForNewSettlement`, the goodwill
  a nearby town loses each season, `SettlementProximityGoodwillUtility`).
- **Who goes (decision 2A).** The founding window lists the home's people (`Homes.People`), grown-ups first; children
  follow their parents unless moved. Each grown-up's wish comes from their own ties (`FoundingMath.Pull`: their own, a
  partner, child or parent, going or staying ±3 each; at odds with most here by the fights the council counts +2;
  close friends ±1, rivals the other way; a seat on the council, a room of their own, old age -1 each; youth +1; past
  1.5 either way "wants to go" or "wants to stay", else "torn"), with the reasons. Nobody refuses. Sent or kept against
  it (`AgainstWish`), they feel uprooted for a season (`Rimfolk_Uprooted`, -4, 15 days) and, with the old colony kept,
  may ask once between the third day and the season's end to move to the colony they wanted (`FoundingAsk`, a
  `Tell.Ask`: a journey plan of one, or asked to stay at 2 trust). Close bonds split between the two colonies (a
  partner, a parent or child, a sibling, a friend of 60) feel parted for a season (`Rimfolk_Parted`, -3, the three
  closest at most). Who may go: this home's people, at home, not a lodger, not being banished, not on another journey
  (`CantSend`); the party needs a grown-up, and the old colony kept needs one left in it (`PartyCheck`).
- **How they go.** By caravan through the journey planner's own plan (`Foundings.PlanJourney`: one way, a
  `JourneyPlan.founding` id, food for the road and fifteen days more, the founding kit by the colony's age from what the
  stores hold: wood for a tribe or a medieval colony, steel, components and a little wood for industry and beyond,
  medicine for the first season; fitted to 90% of the party's room, fewer days of food first, then less of the
  materials), and the planner opens on it. Its farewell at the gate is a parting's (`Rimfolk_Pool_FoundingSays`). By the
  colony's own pods or shuttle (a launcher there), or the gravship (Odyssey's engine there): the game's own way, told
  in a letter. A caravan of theirs stopped at the place or within two tiles is asked once "Settle here?"
  (`ChoiceLetter_Founding`), which runs the game's own `SettleInEmptyTileUtility.Settle` with its proximity warning;
  the game's Settle button works the same. Any home made since the founding set out at the place (anywhere, for the
  gravship's landing) is the new colony. Nobody from the colony: the newcomers alone, the settlement made with
  `SettleUtility.AddNewHome` and its map with `GetOrGenerateMapUtility.GetOrGenerateMap` in the game's long events.
- **Newcomers (decisions 3A and 4A).** Made with `Dialog_Newcomers`, built as the game's `Dialog_ChooseNewWanderers`:
  a fresh `GameInitData`, one to six people with `StartingPawnUtility` (the portrait area and character card with its
  randomize, Biotech's options, health, relations, possessions, the skills summary), the player's people's kind; the
  start data cleared however the window closes. At once with nobody going, else asked for once the colony stands (a
  letter; unanswered, let go after 10 days). They walk in from the edge with their possessions, as the game's own new
  wanderers do. Free with the old colony abandoned in the same act; with it kept, at most once a year
  (`Homes.newcomersPaidAt`), their passage paid from the old colony's stores, about their kit's worth, at least 150
  each, up to half in food (`FoundingMath.Passage`, `Pay`), named at the window's foot before Confirm. A scenario's
  start parts come in release 3, through the window's request.
- **The colony stands** (`Foundings.Founded`): the record of every colony on the world ledger (`LedgerColony`, below),
  a milestone `colony:founded` (the founders "did", the mother colony "about"), a letter naming the founders, the party's
  age form (a band to make a new hearth, settlers with a charter and carts, a settlement company, a colony mission) and
  what becomes of the old colony, the seat moved if asked, the moods above, and on the old colony's calendar (kept) the
  parting day (`CalendarKind.Parting`, a remembrance that may become a tradition). Its own council, calendar from its
  founding day, founders and firstborn come from 7a and 7b's rules. Its first evening (17 to 21h, two hours after it
  stands at least) is a fire (`PlayKind.Bonfire`, laid from the wood they brought if there's none): the founders
  invited, a seat holder among them or the eldest organising, their words from `Rimfolk_Pool_FirstNight`, and
  `Rimfolk_FirstNight` (+4, 4 days) held until it is over; with answers not played out, the speaker's line and the mood.
- **The old colony.** Kept, a second home under the game's limit. Or abandoned: once the new colony stands and none of
  the party is still on the old map (a straggler holds it back, and the colony is told once), the game's own
  `MapParent.Abandon` at the next frame, then `GameEnder.CheckOrUpdateGameOver`; anyone left there is lost to the
  colony as the game rules, and the window named them. Its council goes with its people to the new home
  (`FoundingMath.CarryCouncil`, 7a's rule as a gravship landing carries it), its record ends "abandoned" with the
  game's ruin, the milestone `colony:abandoned`, and its founding day stays on the new calendar as a remembrance. Any
  home on the record that is lost another way ends there too (`Foundings.HomesLost`: abandoned when the game's ruin
  stands at its place, else lost).
- **The game's colony limit (decision 5B).** `Prefs.MaxNumberOfPlayerSettlements` is the game's preference, shared by
  every save: as the founding starts, Rimfolk raises it by one when the new colony would pass it, never past the game's
  five (`FoundingMath.LimitFor`, against the homes the game counts), and the founding window says so plainly, with
  where to lower it again. With the old colony abandoned, or the founding called off, it is set back by one while it
  is still what Rimfolk set and the homes left fit (`LimitBack`).
- **The record of every colony** (`World/Ledger/LedgerColonies.cs`): `LedgerColony` per colony (its key, name, tile,
  founding day, the colony it came from, how and when it ended, the ruin it left), 24 at most (the oldest ended ones
  fold into the history), written only once a second colony is founded.
- **The seat (decision 1A).** "Make this the seat" on a colony's card, a home on the ground of several, at most once a
  season (`FoundingMath.SeatWhyNot`, `Homes.seatMovedAt`), or ticked in the founding window for the new colony. What
  reads `Homes.Seat` moves with it: the events and letters about no home of their own (`HappeningTracker.HomeMap`), the
  seat's council (`comp.offices` and the others, the speaker who signs to the world), the seat's calendar's world days
  and nemeses from then on, the notables and world events anchored at the seat (release 4 gives every home its own),
  the region map's default. Each colony's own council, calendar and names stay. The old seat's people lose 3 trust
  (not a mood); the milestone `colony:seat`; the seat's word by age (`AgeFeature.Seat`: the elders' fire, the
  council's hall, the town hall, the administration).
- **Seeing every colony (3c).** The region map's Ours lens lists every home however far (beyond the map's reach too),
  each row its grown-ups and children, speaker, mood and strain, its card's lines on hover (founded when and from
  where, the seat or not, wealth, its next day by name and date, a threat now); a founding on its way; and the colonies
  of the past (group 3), founded and ended how. A colony's card: go there, make this the seat, found a new colony, and
  with a founding under way plan the party's journey, choose the newcomers or call it off; a colony of the past has a
  card from its record. With more than one colony every view's header carries a chip naming the colony in view, a
  click from another (`ColonyView.Chip`, drawn by `UiKit.ViewHeader`); the council pages' own line it replaces.
- **Settings.** "Many colonies" on the What's on page: Fresh starts, and Newcomers under it. Off, nothing new is
  offered; a founding under way goes on.
- **Seams.** Release 2's hand-over is the founding window's third choice for the old colony and a card act beside
  these (marked `HandOver`); release 3's scenario parts shape the newcomers' request; release 4 widens the seat-anchored
  notables and world events to every home. With one colony nothing runs: no founding, no question, nothing saved (each
  new list is written only when it holds something), and the Ours lens's home row reads the same people as before in
  more words.

#### A scenario's start (stage 3, release 3)

Any loaded scenario can shape a new colony alone (docs/design/stage3-many-colonies.md, section 1, "A scenario's start";
the player's decision 3A of 6 November 2026). Pure rules in `Colonies/ScenarioStartMath.cs` (`Tools/Tests/ScenarioStartTests.cs`,
prefix "scenario start: "); the game's side in `Colonies/ScenarioStarts.cs` (reading a scenario, making its people and
kit, their arrival, Rimfolk's parts at the new colony), `ScenarioStartWindow.cs` (the list), `ScenarioStartPatches.cs`
(the pawn hooks), with seams in `NewcomersWindow.cs`, `FoundingWindow.cs`, `Foundings.cs` and `HomesFounding.cs`.
- **The choice.** With newcomers called, the founding window asks their start: new people as a new game's group (as
  before), or a scenario's start, chosen in `Dialog_ScenarioStart` from every scenario def loaded (the game's, its DLCs',
  Rimfolk's seventeen, other mods'), this game's own when it is none of those, and the player's own and the workshop's
  the game already holds (`ScenarioStarts.Loaded`; nothing read from the disk mid-game). For each: what comes for the
  new colony alone, what isn't carried, what never comes, and that a whole new scenario on its own page is what a new
  game is for. The founding plan keeps it by a key (`FoundingPlan.scenario`, `ScenarioStartMath.Key`: "def:", "game",
  "file:", "workshop:"); not loaded any more when the newcomers are chosen, they come as a new game's group.
- **What each part does** (`ScenarioStartMath.Carry`, by the part's class as the game names it). Carried: the people
  (the four `ScenPart_ConfigPage_ConfigureStartingPawns*` parts), what shapes starting people (forced trait and hediff,
  naked, a need's level, the age filter, no possessions), the kit (defined things, mechs, what it scatters near the start
  or anywhere), the animals, the arrival, and Rimfolk's own thirteen. Not carried, listed by what they change: the
  scenario's own people when it isn't the colony's kind, starting research, rules for every map, its own kind of map,
  world conditions, incidents switched off, the whole game's events (quests and incidents made at the start, the
  monolith, the pursuing mechanoids), the opening words. Another mod's part, even one built on the game's, is listed as
  not carried; with the deeper game hooks off, so are the parts that shape people through them. Never: Bad Blood's
  watches (`ScenPart_CouncilFamilies`), a feud that began aboard their ship.
- **The people.** `Dialog_Newcomers` reads the people part's fields into its own start data (`ScenarioStarts.InitData`:
  the count, the left-behind kind, the required kinds, xenotypes and mutants, the allowed life stages, the required
  skills; never its `PostIdeoChosen`), makes each place's request as the part's own `GenerateStartingPawns` would (its
  kind, its xenotype with the part's kind overrides, its mutant), tries again up to twenty times until the work the
  colony can't do without is covered, then adds the candidates to choose from (the scenario's count, 1 to 10, chosen
  from its choice). Those coming are listed first, those left behind under them; an arrow swaps one, with the request
  that made it. Confirm waits, in the game's own words, until the chosen meet the scenario's requirements
  (`ScenarioStarts.WhyNot`, `ScenarioStartMath.RequiredMet`: at least as many of a kind, exactly as many of a xenotype
  or mutant). Those left behind are let go (their families only when none of the chosen is kin).
- **The pawn hooks.** While the window is open with a scenario's start (`ScenarioStarts.Shaping`, live only while the
  game is in play and the window's start data is set), prefixes on `Scenario.Notify_NewPawnGenerating`,
  `Notify_PawnGenerated` and `AllowPlayerStartingPawn` (`Patch_ScenarioStartShapeNew`, `Done`, `Allow`) send a starting
  colonist being made to that scenario's shaping parts instead of the game's own; any other person, at any other time,
  goes to the game's own scenario as always. A shaping part that waits for the map (hidden until then) is applied as
  they are confirmed; no possessions clears what the game's roll gave them.
- **The kit and the arrival.** Made as they are confirmed, while the start data holds the chosen alone
  (`ScenarioStarts.MakeKit`): the scenario's own `PlayerStartingThings` (its things, mechs with an overseer and animals
  bonded to the newcomers as at a new game, Rimfolk's children in its care), what it scatters made as the game's scatter
  makes it. They arrive (`ScenarioStartMath.Arrival`): on foot from the edge of the land, the kit set down where they
  come in; in drop pods, a pod each and the kit shared, near the middle of a new colony's land (the game's start spot) or
  at the colony's drop spot when its party is there first, with the game's own "crashed together"; or in the gravship
  (Odyssey), made and set down as the game's own start does (`SketchGen` with the gravship resolver,
  `GenStep_ReserveGravshipArea.SetStartSpot`, `GravshipPlacementUtility.ClearAreaForGravship`, the people on its deck,
  the kit on its shelves, its tanks full), only with the old colony left behind on a map made for them alone, else on
  foot. What it scatters comes near where they arrive (20 cells) or anywhere. A letter tells it as it happened.
- **Rimfolk's own parts** (`ScenarioStarts.SetUpWhenHere`, in the founding's hour): once the scenario's people are on
  the map, or a day after they came, each of the scenario's Rimfolk parts is set up as at a new game but for that colony
  (`ScenarioSetupContext` with that map, the newcomers and the children in their care, the scenario, and no warm start):
  its council's seats, its calendar's days, a goal, the colony's reputation (its one character for now), a sworn enemy,
  a bond, a secret, a world event, its wards, a debt, deserters; the Beholden's grant is a fealty tie from that colony,
  without its goodwill raised. One letter says how things stand there.
- **The passage (4A).** Free with the old colony left behind. Kept, release 1's passage for each newcomer, the least
  passage for each child in the kit's care, and the kit's own worth by the game's market values (a random pet or mech at
  its kinds' average), rounded to ten (`ScenarioStartMath.Passage`), named before the choice.
- **Saves.** The plan keeps the key and the scenario's name; its people at the new colony (`scenarioPeople`) are written
  only from their arrival until Rimfolk's parts are set up. No setting, no schedule slot, nothing per tick; with one
  colony, or the start a new game's group, nothing changes.

#### Another landing site (the player's request, 6 November 2026)

"I don't like the map": in a new game's first days the colony can be set down on a new map without starting over.
`Source/ColonyCouncil/Colonies/`: the rules are pure (`LandingMath.cs`, tested in `Tools/Tests/LandingTests.cs`), the
record of the start is `LandingRecord.cs`, the move `Landing.cs`, the window `LandingWindow.cs`, the game's hooks
`LandingPatches.cs`. Nothing per tick and no schedule slot: the record is let go in the council's hour.
- **The entry.** "Choose another landing site" ("starting site" for a start on foot) in the game's own menu (Esc), put
  in by a transpiler on `MainMenuDrawer.DoMainMenuControls` that sends its first `OptionListingUtility.DrawOptionListing`
  (the main options, not the web links) through `LandingMenu.Draw`, before "Quit to main menu", with the menu window one
  option taller (`MainTabWindow_Menu.RequestedTabSize`); if the call isn't found, nothing changes and the log says so
  once. The same act on the colony's card beside "Found a new colony". Shown for `LandingMath.OfferDays` (3) days
  after a landing, greyed with why (`LandingMath.WhyNot`, in order): the deeper hooks off, a start Rimfolk didn't see,
  the tutorial, a gravship start (Odyssey: the gravship can fly them elsewhere; its own capture and placement isn't
  used), a start above the planet, more than the one home, a journey or founding being planned, the void monolith
  woken, someone of the colony away or inside something (a casket, a pod, a vat), a guest of the colony on the map, a
  quest under way with a target on the map that wouldn't come along, a threat (the game's own
  `GenHostility.AnyHostileActiveThreatToPlayer` when the move is made).
- **The record of the start.** The game drops the scenario's things only on the first map
  (`ScenPart_PlayerPawnsArriveMethod` and `ScenPart_ScatterThings` return early unless `Find.GameInitData` is set), so
  a prefix and postfix on `Scenario.GenerateIntoMap`, only while it is set, take the things on the map before and after:
  the difference is the kit (`LandingPiece`: the stack by reference, its def, how many of it are the kit's, and where it
  came from), told apart by a prefix and postfix on each scatter part (near the start or anywhere); each starting
  colonist's apparel, equipment and inventory is theirs; the start spot, the arrival method, the map's generator and
  the map conditions the scenario set are kept too. On the council's component, saved only while the offer is open.
- **The kit followed.** The colonists haul the kit a load at a time and the loads merge on the stockpile with each
  other and with what was found on the map, so a reference alone loses it at the first haul. A postfix on
  `Thing.SplitOff` and a prefix and postfix on `Thing.TryAbsorbStack` (a dictionary look-up while the offer is open,
  nothing after) carry each stack's kit count over (`LandingKit`): the kit's part goes first when a stack is split,
  used or merged (`LandingMath.SplitKit`, `MergeKit`), so it is never counted larger than it is, nothing found or made
  counts, and what is used is gone. What comes is the kit's part of each stack lying on the map (`Takes`) and whatever
  of it someone who comes holds.
- **The window.** A new map of the same place, or another place with the game's world targeter, Rimfolk's facts of
  the tile (`FoundingPlace.Facts`) and the game's own check (`TileFinder.IsValidTileForNewSettlement`, the colony's own
  settlement not counted as a neighbour since it moves; nothing but a passing caravan may stand there); with a
  scenario's forced map, only the same place. The map size: the game's sizes (`Dialog_AdvancedGameConfig.MapSizes`, its
  test sizes with the option), the current one first. What comes along and what is lost, by name and count, worked out
  by the same `Landing.Plan` the move uses.
- **The move**, in one long event. Everyone of the colony standing on the map (and anyone of it they carry) is taken
  off it as the game moves someone through a portal (their lord told, drafted no more, their beds' claims given up);
  what they carry in their hands comes along, a stranger or a corpse is set down. The kit's part of each loose stack is
  split off and taken. The scenario's map conditions are noted as they stand. The old map goes
  (`Game.DeinitAndRemoveMap`, no letter), and the same settlement is kept: for another place it is moved (the game's
  own `WorldObject.Tile` setter redraws it and its tile lookups) and `Find.GameInfo.startingTile` follows. So every
  record keyed by the settlement (`Homes.KeyOf`: council, calendar, founding day, history, ledger) and every quest that
  names it stays with the colony, and the old place is left empty, not a ruin. The design asked for the new home's map
  to be made before the old one went for another place; with one settlement the game allows only one map at a time
  (`MapGenerator.GenerateMap` refuses a parent that has one), so the old map goes first either way, as in the game's own
  `MoveColonyUtility.MoveColonyAndReset`. The new map is made the game's way (`MapGenerator.GenerateMap`, the
  start's generator, the chosen size, which becomes the world's size for new maps); for the same place the seed its
  content is made from is turned for that one map (`Patch_LandingSeed` on `GenerateContentsIntoMap`), never the world's
  own seed, whose noise the game builds once (a mineral-rich tile's ore), so the place keeps its rock and ore. The
  scenario's after-map step runs inside it (`Scenario.PostMapGenerate`): with Anomaly the monolith comes on the starting
  tile as at a game's start, and it is made the game's own (`Find.Anomaly.monolith`, which the game sets only while it
  has none), waking when the scenario said (`AutoActivate`). The scenario's map conditions, set again by its genstep,
  are put back to the time they had left, or ended. They arrive the scenario's way at the new start spot
  (`DropPodUtility.DropThingGroupsNear`: each person a pod of their own, the mechs, animals and kit shared, as the game
  drops them; or standing), what was scattered near the start within the game's 20 cells of the new one, what was
  scattered anywhere anywhere. `GameEnder.CheckOrUpdateGameOver` ends with `gameEnding` false (a wild animal spawned on
  the new map asks it while nobody stands on a map), so there is no game-over letter. The camera goes to the new start
  and a short message says what happened, in the colony's age's words. If the new map can't be made, the colony gets the
  game's map of where it was; if no map at all, it goes on as a caravan.
- **Nothing else.** It is not a founding, an abandoning or a journey: no history entry, no calendar day, no mood, no
  speech. What was under way on the old map (a play-out, a visit) ends with it, as when any map is closed.

#### A town of our own (stage 3, release 2a)

A colony handed over to a people of its own (docs/design/stage3-many-colonies.md, section 2 without "What it does
afterwards", "Taking it back" and "Keeping the layout"; the player's decisions 2A, 6B, 8A and 9A of 6 November 2026).
`Source/ColonyCouncil/Colonies/`: the rules are pure (`HandOverMath.cs`, tested in `Tools/Tests/HandOverTests.cs`);
`HandOvers.cs` runs it in the region's hour (`Pass.Region`, `RegionBook.Hourly`, only while one is under way) and the
region's day (the bond); the window is `HandOverWindow.cs`, the peoples and the bond `HandOverPeoples.cs`, the notables
`HandOverNotables.cs`, the game's hooks `HandOverPatches.cs`; the plan rides the homes' component (`HomesHandOver.cs`),
the towns the colony was the world ledger (`World/Ledger/LedgerFormers.cs`). Its questions are release 1's letter kind
(`ChoiceLetter_Founding`, asks 3 and 4).
- **The window** (`Dialog_HandOver`), from the colony's card ("Hand it over", beside "Found a new colony") or the
  settlement's world buttons beside the game's Abandon (`Patch_HandOverGizmo`, a postfix on `Settlement.GetGizmos`):
  who stays and who goes (release 1's rows, `Foundings.Rows`, each grown-up's wish with why; children follow their
  parents; anyone away from home or a guest of another people's goes); the head and the head's second
  (`HandOverMath.Head`, `Second`: the player's pick, else the speaker, else the office holder first by `OfficeDef.order`,
  else the one the others think best of, `HandOvers.Standing`: the others' opinions and the colony's view; never by
  blood); the people's name by the age's word (`CC_HandOver_PeopleName_<Age>`, renameable, `NameWhyNot`: not empty,
  not long, not another people's); the terms (free, a defence or trade pact; a pact needs Pacts on and a home of the
  colony's left, or a new colony of the same act); the prisoners and slaves set free or taken along; what stays with the
  place; where those who go go (another ground home, the road, or a founding: "Found a new colony with them" opens the
  place picker with the third choice set); the checks (`HandOverMath.WhyNot`: a settlement on the ground, no fight or
  threat, no banishment under way, three grown-ups staying, someone of the player's elsewhere, no grav anchor); and the
  plain word that a save holding the town then needs Rimfolk (6B). "Put it to the meeting" stores the plan (one at a
  time).
- **The meeting's vote (2A).** A play-out at the meeting spot (`PlayKind.Toast` with words, the dining table first, as
  the council meets; 17 to 22h; its chair the speaker; `Rimfolk_Pool_HandOverMeeting`); when it is over (the play-outs'
  own `PlayOut.afterward`, `PlayAfter.Run` now passing who took part) each member who came votes
  (`HandOverMath.VoteOf`: their wish met +1, against it -1, torn 0, plus trust / 40, at most 1.5 either way; past 0.25
  either way for or against, else torn and not counted), every member when nobody gathered or answers don't play out.
  A letter tells the vote and who voted which way, and asks: go ahead (the hand-over set for the next day, by its date;
  against the vote, `TrustAgainstVote` -6 among those who stay) or call it off; the colony's card has the same while the
  letter waits.
- **The ceremony.** On the day set, between 9 and 19h with no fight: everyone of the home who stays or goes is invited
  to a play-out at the meeting spot, the head its organizer and speaker (`Rimfolk_Pool_HandOver`, lines by age). When it
  is over: the one who hands it over (the speaker if they go, else an office holder who goes, else the eldest who goes)
  and the head, face to face (`Together.When`, four hours), say the give and take lines and the social log records the
  totem, the keys and charter, the town charter or the codes (`CC_HandOver_Token_<Age>`); each who goes says goodbye
  to their two closest who stay (`Foundings.IsClose`, twelve in all, `Together.When` with one going to find the other
  after an hour). Then those who go leave: to another home by the journey planner's plan (one way, the planner opened),
  else a letter to form their caravan the game's way (`Dialog_FormCaravan`), or by pods, shuttle or gravship; a
  founding of the same act sets out now (`Foundings.Begin`). With answers not played out, the vote is counted at once
  and the farewells still wait for the two to be together.
- **The town made**, at the next frame (a long event) once `HandOverMath.WaitsFor` is clear: nobody of those who go
  (or a colonist of another home visiting) on the map, no guest of the colony's there, the same act's new colony standing
  when nobody goes from here, three grown-ups on the map, no gravship engine left. In order: the colony's record
  (`LedgerColony`, written now if it wasn't) ends `HandedOver` with the town; the people is made
  (`HandOverPeoples.Make`: `FactionGenerator.NewGeneratedFaction` with the age's def, hidden at creation so no town of
  its own is placed and, by `Patch_HandOverLeader`, no stranger made its leader; named; its primary faith the colony's
  (`SetPrimary`, a faith the game made for it and nobody holds removed); shown; allied with the player at 80 and with
  every other people half the player's goodwill, at war only while the colony is, a permanent or natural enemy kept so
  (`OtherGoodwill`); `FactionManager.Add`); a gravship-landed home's flag cleared so it is no longer a home; the
  prisoners set free (their own people's again), the slaves, those who stay, the animals, mechs and any other player
  pawn on the map given the people; the head its leader; the settlement's faction set, its cached material cleared and
  its trade stock let go. The game closes the map at the settlement's next tick (`Settlement.ShouldRemoveMapNow`: no
  player pawn on it), passing those who stay to the world as its people's, with no "lost" letter, and
  `GameEnder.CheckOrUpdateGameOver` finds the colony's people elsewhere. A gravship taking off without an anchor from a
  home whose ceremony is over makes the town instead of the game's abandon (`Patch_HandOverGravship`, a prefix on
  `GravshipUtility.AbandonMap` for that map only), anyone of those who go left on the ground staying with it.
- **The records.** A `LedgerFormer` (the settlement id, which is the colony's key, the people, the colony's name and age,
  its founding and the hand-over, the terms, the second, the former colonists, where those who went went, the bond's
  state; six at most); the town's `LedgerTown` of a new kind `TownKind.Former`, its prosperity seeded from the colony's
  wealth per head (everyone who stayed and went: `HandOverMath.Seed`, 30 at 2,000 a head to 70 at 40,000), its founding
  the colony's founding day, its founder one of the colony's founders who stayed (else the head); its notables
  (`NotableBook.FormerTown`: the head, the head's second as the heir, the envoy by social impact, the trader by trade
  price improvement, all of them the colony's own); the rest kept in the world with `KeepFor.Former` while the town
  stands; the pact (`Pacts.Make`, the seat's, or the colony's as a whole while it has no other home); the moods
  (release 1's uprooted for those sent or kept against their wish, parted for the three closest on the other side);
  the milestone `colony:handedOver` (the colony about, the people for, the head did; at the town); a letter pointing at
  the town. The colony's council, calendar and books for that home are let go by the usual rule (thirty days).
- **The heads.** `Patch_HandOverLeader`, a prefix on `Faction.TryGenerateNewLeader` for Rimfolk's own peoples only:
  while one is being made, no leader; when its leader dies or is lost, the second leads (`HandOvers.NextLeader`), else
  the best thought of among its former colonists, and the game's own letter names them; a new second is chosen, former
  colonists first (`NextSecond`), taking the heir's place among the notables if they were one already; with nobody of
  theirs left, the game makes its own. The notables keep a town the colony was wherever it lies (`Towns`, `Look`), fill
  an empty place from its former people first (`HandOvers.NotableFor`), and title the second by the colony's age
  (`CC_HandOver_Second_<Age>`: the headman's second, the reeve's deputy, deputy mayor, deputy administrator).
- **The four peoples (6B)** (`Defs/Misc/Rimfolk_HandOver.xml`): `Rimfolk_People_Tribal` (hearth folk, headman) and
  `_Medieval` (free folk, reeve) on the game's `TribeBase`, `_Industrial` (township, mayor) and `_Spacer` (free colony,
  administrator) on `OutlanderFactionBase`: the game's own groups, caravans, traders and town defenders; peaceful,
  human, not hidden, no fixed name, no royal titles, never made with the world or listed in its choices. With a mod of
  a medieval (or spacer) people loaded, Medieval Overhaul's first, its pawn groups and traders are lent to Rimfolk's
  people of that age at startup (`HandOverPeoples.Lend`), so their gear is the age's.
- **The bond (8A)**: `Rimfolk_OnceOf`, `GoodwillSituationWorker_OnceOf`, shown as "once of [colony]": +30 natural
  goodwill while one of the former colonists heads the town, +15 after, 0 once the bond is broken; the region's day
  breaks it when the people are at war with the colony (`colony:bondEnded` in the history, a line in the news). The
  game's drift and same-faith situation do the rest.
- **The same act as a fresh start.** The founding window's third choice for the old colony, "Handed over" (`HandOver`):
  its own checks join the founding's, the newcomers come free, and its Confirm opens the hand-over window with the
  party as those who go; the founding is kept inside the hand-over (no other founding meanwhile) and sets out when the
  ceremony is over (`FoundingPlan.handOver`: never abandoned; done once its hand-over is, with the old founding day kept
  at the new colony and the game's limit set back by one as after an abandon).
- **Cards.** The colony's card: "Hand it over", "Go ahead with the hand-over" while the vote waits, "Call off the
  hand-over", and a line for where it stands (by names and dates, no countdown). A town the colony was: its card says
  which colony it was, when it was handed over, how many of the colony's people live there and the bond.
- **Setting.** "Handing over" in Many colonies. Off, nothing new is offered; a hand-over under way goes on, and the
  bond and the heads keep to their records.
- **Release 2b** (below, "The town afterwards") filled the seams 2a left: the layout kept in `Make` before the place
  is its people's, the town's letters, trade, days, homecomings and moving there, and taking it back peacefully or by
  force (which ends the bond). With one colony and nothing handed over nothing runs: no plan, no record, nothing saved
  (each new list is written only when it holds something).

#### The town afterwards (stage 3, release 2b)

What a town the colony was does once handed over (docs/design/stage3-many-colonies.md, section 2, "What it does
afterwards", "Taking it back", "Keeping the layout"; the player's decisions 7A and 10A of 6 November 2026; release 5's
raid, hard times and turning against the colony, Faction Territories' pace and 11B are not here). Pure rules in
`Colonies/TownAfterMath.cs` (`LayoutMath`, `FormerMath`; `Tools/Tests/TownAfterTests.cs`, prefix "town after: "); the
game's side in `Colonies/TownLayout.cs` (kept and placed), `TownsAfter.cs` (its life, the hour and the day, the card),
`TownTakeBack.cs` (asking for it back, taking it back, by force), `TownAfterPatches.cs` (the game's hooks),
`TownAfterFacts.cs` (the facts), `Missions/TownDayMission.cs` (its days) and `Social/MovingOnTown.cs` (moving there);
the records on `LedgerFormer` (World/Ledger/LedgerFormers.cs). No schedule slot: the region's hour (`RegionBook.Hourly`,
`TownsAfter.Hourly`) and day (`TownsAfter.Daily`), only while a town the colony was is on record.
- **The layout kept (7A).** In the hand-over's long event, before the place is its people's (`TownLayouts.Capture`):
  over the home area's bounds and 6 cells around (`LayoutMath.Rect`, the colony's buildings' bounds when there is no
  home area), every building of the colony's (`listerBuildings.allBuildingsColonist`, not a frame, not the land's rock)
  at its origin by a number into a table of "def|stuff" names and its rotation (14 and 2 bits of a ushort), edifices
  in one grid and what lies under or on them (conduits, lamps) in another; the floors laid (layerable, or a smoothed
  floor) and the foundations (bridges, substructure) by number; a built roof and where natural rock is gone (mined out,
  or never there) as flags; the beds of those who stay and who sleeps in each. Saved the way the game saves its grids
  (`DataExposeUtility.LookByteArray`: two bytes a cell, deflated), with the table and the beds (`LedgerLayout`). Past
  64 KB (base64 of the deflated grids, measured with the game's own `CompressUtility`), the roofs go, then the floors,
  then the lesser buildings; past that nothing is kept. Items, plants, filth and art's tales are not kept. A made-up
  80 by 80 base of 18 rooms takes about 3 KB, one over most of a 275 map about 14 KB (the tests); the player's own
  colony (the Atia save of 5 November, a 250 map: 166 by 156 cells kept, 935 buildings, 140 defs) about 7.5 KB, most of
  it the table of names.
- **The town's map from it.** When the game makes the map of a town the colony was, still its people's, with a layout
  kept (an attack, a fight there): `Patch_TownLayoutSettlement`, a guarded prefix on `GenStep_Scatterer.Generate` for a
  `GenStep_Settlement` (the game's, and the Vanilla Expanded Framework's KCSG step, which derives from it and which
  Vanilla Base Generation Expanded gives the game's tribal and outlander peoples and so ours), places the layout
  instead of the game's base, for that map only; `Patch_TownLayoutPower` stands the game's power step down for it.
  Placing (`TownLayouts.Place`): the rock gone is cleared (its hewn ground left), the foundations set, the floors, the
  edifices and what lies under them spawned through the game's own `Sketch` (with its placement rules, wiping plants and
  chunks, forcing the ground under a building where it must), the built roofs set; a def or stuff the game no longer has
  is left out; the roofs the cleared rock had the game mark to fall are unmarked where the colony's walls hold them. Then its people (`AtHome`): its notables free in the world at home (their own beds where the layout knows
  them, else a roofed cell), and the game's own settlement group of that people (`PawnGroupKindDefOf.Settlement`, the
  points the game gives a town less what the notables bring), all under `LordJob_DefendBase` at its middle. On a map of
  another size the layout keeps its place, moved in to fit. Without Rimfolk, deep hooks off, or a town handed over
  before 2b, the game builds its own base.
- **Letters.** Its head (the people's leader) writes at most a season apart, the first some days after the hand-over,
  then about one day in ten (`FormerMath.LetterDue`): carried to the gate by their messenger (the couriers' own mail,
  `Couriers.MailAnswer`, read when it is handed over; over comms at once in the spacer age, by the age it had at the
  hand-over; the word the old way when nobody of theirs can come), a line of `Rimfolk_Pool_TownLetter` by the town's
  fortune, the age, whether the head was one of the colony's own, the years since and someone of its former people
  asking after their own in the colony, with the facts service's tags, and the town's fortune in words.
- **Trade.** The game's own caravans; when a trader caravan of theirs comes, its Trader notable (free at home) is the one
  who trades (`Patch_TownTrader`, a prefix on `PawnGroupKindWorker_Trader.GenerateTrader` for Rimfolk's peoples only),
  no longer the caravan's trader once home.
- **Its days.** It keeps the colony's founding day and the day of the hand-over (`FormerMath.DayComing`: whichever comes
  first, the founding day after the first season, the hand-over day after a year, a year apart), and invites the colony
  to one a few days before it, by the invitation missions: `Rimfolk_Mission_TownDay` (`HappeningWorker_MissionTownDay`,
  the invitation's own journey, stay, gift and memory, base chance 0, fired for that town's people alone with
  `MissionPeoples.Only`).
- **Homecomings.** A former colonist, never the head, free at home, whose spouse or child (or a close friend: 40 regard
  both ways) is in the colony, or whose town is struggling, may ask to come home: never in the town's first season,
  once a season from each town at most, a day's chance by the best reason (kin 5%, a friend 3%, hard times 4%), each
  person asked about once, through `Tell.Ask` (`ChoiceLetter_Founding` ask 6). Welcomed, they walk in from the edge on
  the town's side as colonists (`Arrivals.Join`), no longer the town's or its notable, the one they came home to goes to
  meet them (`Together.When`, `Rimfolk_Pool_TownHomecoming`, the social log), the milestone `colony:homecoming`.
- **Moving there.** A moving-on question of its own (`Rimfolk_DlmMove_Town`, `DilemmaWorker_TownKin`): a colonist with a
  spouse, child or close friend among those who stayed, at a town at peace with the colony, never one of the last four
  grown-ups. Going, they are seen to the gate, join the town's people (`DilemmaEffect_LeaveForGood`) and count as one of
  its former colonists (`DilemmaEffect_JoinTown`); kept, they are held back as the other moving-on questions hold them.
- **Asking for it back.** A letter purpose of the neighbour letters' own, `TakeBack` (`TownLetters`, through
  `DiplomacyLetters`), offered only in a letter to the head of a town the colony was, at peace, once a year, while the
  game's colony limit allows one more home (5B raising it by one): "Ask for it back" on the town's card opens it. Their
  answer (`FormerMath.TakeBackChance`): 0.15, plus their regard (0.3 a hundred) and their goodwill (0.25 a hundred), plus
  or minus up to 0.3 by the town's fortune (struggling readier), less 0.1 a year (five at most) and 0.35 for how much it
  has grown (its fortune over the seed, a quarter a town of its own), between 0.02 and 0.9; a no says the heaviest
  reason (`TakeBackWhy`); their own words are the letters' pool's (`takeback`). A yes reaches the colony with the answer:
  a letter (ask 5) and the card's "Take it back", for a year.
- **Taken back** (`TownTakeBack.Make`, a long event): the limit raised if needed; the people's leader stepped aside, the
  people made temporary when it has no other town; the settlement the colony's, its look cleared; its map made the
  game's way for a home at the size the colony was, the layout placed (unfogged); its former colonists free at home
  walk out of their rooms as colonists (their beds, `Arrivals.Join`), and up to three of its other people choose to
  come (`FormerMath.Joiners` by their best regard of the colony's people, kin among them and hard times); a people that
  keeps other towns leads on, else it is defeated and the game lets it go; the town's notables let go, the colony's
  record standing again, the milestone `colony:takenBack`, a letter. Records that would point at a people let go
  (the former's, the ledger town's, the faction memory's) drop it when saved.
- **By force** (10A): attacked, its map is the layout with its people defending it; beaten, the game's destroyed
  settlement keeps the map (`TownTakeBack.Watch` in the region's hour: `FormerMath.Became`); the bond ends, its people
  remember `townTaken` (45; when it keeps another town, else the game counts it defeated) and every people of the same
  faith `townTakenFaith` (20), each with the ledger's grudge (origin `townTaken`), a letter and the history; settled with the game's Settle button, the milestone
  `colony:takenBack`.
- **Facts.** `town:former` (their town was the colony), `them:former` (the one spoken to was one of the colony's own),
  `{OLDCOLONY}`, `{YEARSSINCE}`, through the region's provider; talk that remembers the colony's days
  (`Rimfolk_OutsiderTalks_TownAfter.xml`).
- **Ages.** The head's letters come by the town's means; taking it back is `AgeFeature.Back` (two fires one again, the
  charter returned and the oaths renewed, a merger signed, the charter dissolved).
- **Setting.** "The town's life" in Many colonies (`townLife`): off, no letter, trader, invitation, homecoming or move
  there; the kept layout and taking it back go on.
- **Dev tools.** Debug actions "Town after: ..." (`Colonies/TownAfterDebug.cs`): the head's letter, a homecoming asked,
  its day's invitation, their yes to the colony's letter, the question about moving there, and the kept layout's size.

#### The town's life (stage 3, release 5)

A town the colony was in its own life (docs/design/stage3-many-colonies.md, section 2, "The bad side", "Taking it back"
and "Its growth"; the player's decisions 11B and 15B of 6 November 2026). Pure rules in `Colonies/TownLifeMath.cs`
(`Tools/Tests/TownLifeTests.cs`, prefix "town life: "); the game's side in `Colonies/TownRaid.cs` (the raid, a world
event of its own), `TownLife.cs` (the hour and the day: hard times, the gate, the goodwill, the hardships, growth),
`TownLifeLetters.cs` (its questions, `ChoiceLetter_TownLife`), `TownLifePatches.cs` (the game's hooks) and
`TownLifeDebug.cs`; the records on `LedgerFormer` (values only). No schedule slot: `TownsAfter.Hourly` and `Daily` call
in, the raid rides the world events' own hour. Setting: "The town's life" (with handing over); off, none of it.
- **Raided** (`Rimfolk_WS_TownRaid`, `SituationWorker_TownRaid`, weight 4, kind `townraid`, a fight of its own): one of
  the world events of their usual rhythm, so it adds none to the year; it can be drawn only while a town the colony was
  stands at peace with the colony, its map closed, its people at the heart of no other world event, and a people at war
  with both (and able to field a band) exists, the nearer its towns the likelier. The points: its defenders the game's
  own for a town (1,150 to 1,600) by its fortune (0.7 at nothing, 1.3 at 100, `TownLifeMath.DefendPoints`), the raiders
  0.75 to 1.5 of the game's own before the fortune (so a struggling town meets them weaker). The start letter is the news by the town's means (`CallMeans`: a runner, a rider, the radio,
  comms, by its age at the hand-over); within hours the beat `call` (the runner or rider at the gate,
  `VisitEffect_AtGate` with `requires`) asks: send fighters, or send word that the colony cannot come (the default).
  - *Sent*: the caravan window opens on the home the letter came to. The town holds out for a caravan of two grown
    colonists or more (`TownLifeMath.Answers`) five days, and on while one is on the road to it; within a tile, its map
    is made in a long event as the game makes a map it attacks (`GetOrGenerateMap` at the layout's size, the kept layout
    and its notables at home, the defenders at the raid's own points through `TownLayouts.DefendPoints`), the game's
    detection countdown reset, the caravan in at an edge with its colonists drafted, the raiders made as their people
    fight and in at the farthest of eight entry cells under `LordJob_AssaultColony` (no kidnapping, no stealing, they
    may flee), a threat letter naming those who came and the former colonists on the walls. Watched hourly
    (`TownLifeMath.OnMap`): every raider down, dead or fled, defended (+12 goodwill, the faction memory's
    `townDefended`, 18; the colony's part in the letter); nobody of the town or the colony standing, sacked (`fell`); the
    map closed with both up (the colony left), by those left standing. Raiders still there then go.
    `Patch_TownRaidNotDefeated` (a prefix on `SettlementDefeatUtility.CheckDefeated`) stands the game's defeat down for a
    town of Rimfolk's peoples the colony was, not at war with the colony, whose map is open: the game counts a town
    defeated when nobody of its people is a threat to the colony, which a friendly town never is.
  - *Not sent* (refused, or sent and never came: -4 or -8 goodwill and the memory's `refused` or `letDown`): within half
    a day to a day and a half, the game's measure of the two sides' points (`TownLifeMath.Alone`: held on its share of
    the points, 0.1 to 0.9; else hurt, or sacked with raiders a quarter the stronger).
  - *What it costs* (any ending): fortune 0 / 3 / 12 / 25 (on its own record, the raid named as the cause), its trade
    stock less 0 / 10 / 35 / 70% a stack, up to one or two of its notables who were never the colony's (off the map), and
    by 11B one former colonist at most, never its head, only in a raid it lost alone (`FormerDeathChance`: 0.1 to 0.4
    sacked by the raiders' strength, 0.03 to 0.15 hurt: one unanswered raid in about 13 on a steady town, 6 on a struggling
    one, 40 on a flourishing one) or seen dead in the fight on
    its map; the ending's letter names them (`{DEAD}`, `{ITEM}` for its other dead). History `colony:formerDied`.
  - Without the deeper hooks (`noMap`), no map of it is made and "Send fighters" is not offered.
- **Hard times.** Struggling by the region's own rule (`RiseFallMath.Struggling`, `RiseFall.Blows`), goodwill 60 or more,
  at peace, past its first season and a season since its last ask, not refused twice (`TownLifeMath.AsksBack`): its head
  asks to come back under the colony (`ChoiceLetter_TownLife`, ask Back, by the town's means, its fortune and its former
  colonists named; "take it back" by `AgeFeature.Back`). Their yes stands from then (`agreed`), so "Take it back" is on
  the letter and the town's card (2b's `TownTakeBack.Make`); "Not now", or ten days unanswered, is a refusal (the
  memory's `refused`). Refused twice, two to five days later, by day and with no fight at home: the town is given up
  (`RiseFall.MakeRuin`, the region's own ruin in its people's colour, the town's notables let go, `colony:townGivenUp`, a
  milestone); its former colonists free at home first, then up to three of its other people who would join
  (`TownTakeBack.Joiners`, `TownLifeMath.Gate`, eight at most), walk in from the side of the map facing the town to the
  gate (`LordJob_VisitColony`, two days); ask Gate: take them all in, only the colony's own, or send them on. The region
  never gives a people's seat up (`RiseFallMath.MayGiveUp`); only the town's own people give it up, and with no other
  town the people is no more: its leader steps aside, it is made temporary and defeated (the game lets it go once its
  ruin and its people are gone). `TownTakeBack.Watch` leaves a town given up alone (`TownEnd.GivenUp`).
- **Turning against the colony.** `Rimfolk_TownCooled` (`GoodwillSituationWorker_TownCooled`, "wronged by [colony]" or
  "a head who thinks little of [colony]"): the town's natural goodwill less half a point a point of the colony's
  unsettled wrongs they remember (40 at most: the faction memory, a broken pact or truce among them), 10 a live grudge
  of the ledger from them (30 at most), and 0.4 a point of a head's regard under nothing (30 at most), worked out each
  day; the game's own drift follows (outside 50 of it, 10 points each 50 days) and its changes toward it are larger. A new
  head (the second leading after the head) who thinks little of the colony is told (`CoolHead`, -10). At war: the
  game's storyteller raids with the people's own raiders; `Patch_TownRaidFormers` (a postfix on
  `IncidentWorker_RaidEnemy.GetLetterText`) names a former colonist the game redressed into them; the raiders' camps can
  pick the people by their own rule (nothing kept out).
- **Its growth (15B).** Only with Faction Territories, at that mod's own pace (about one town every 30 days a people):
  the existing hooks on its expansion (`WorldMechanics.Founded`, `Built`) tell the colony of a town of theirs begun and
  finished, wherever it lies, and the history keeps it (`colony:townGrew`). Rimfolk's own new towns and ruins settled
  again (`NewTowns.CanFound`) are never these peoples'.
- **11B, the region's hardships.** Each day, a world event of kind sickness (plague) or hunger or drought (famine), bad,
  ended since the last look and after the hand-over, of the town's people or within 20 tiles of it: each former colonist
  free at home (not its head), the eldest first, at 6% a plague or 4% a famine, half again in a struggling town, twice
  from 65, half where the colony helped that people through it, 25% at most, one death at most; a letter names them and
  what took them, by the town's means.
- **Saves.** Values on `LedgerFormer` only (`askedBackAt`, `askOpen`, `refusals`, `gateAt`, `gateCame`, `cooledWrongs`,
  `cooledHead`, `headSeen`, `hardshipSeen`); the raid's state on its world event's numbers and flags (who of the
  colony's former people was on the map by id). No new collection.
- **Dev tools.** Debug actions "Town life: ..." (`Colonies/TownLifeDebug.cs`): a raid on it now, its hard times now (its
  ask), its people at the gate now, a plague's reckoning now, its people at war with the colony, what cools it.

#### Many colonies in play (stage 3, release 4)

Every system right with several homes (docs/design/stage3-many-colonies.md, section 3's 3b, 3c and 3d; the player's
decisions 12A, 13A and 14A of 6 November 2026; 3a, the seat, came with release 1). Pure rules in
`Colonies/ManyColoniesMath.cs` (`Tools/Tests/ManyColoniesTests.cs`, prefix "many colonies: "). With one colony none of it
runs or writes anything: every rule answers as before, no roll is added, and the save is as it was.
- **Two councils become one** (3b, 12A; `Colonies/Merges.cs`, `HomesMerge.cs`, `Calendar/CalendarMerge.cs`). A home lost
  (`Homes.Changed`: given up, handed over) with another home left notes its people (`ColonyMerge`, on the homes'
  component, written only while there is one): those whose last home it was, and those who moved from it within the
  thirty days its council is kept (`HomeTie.prev`, `movedAt`, kept only with more than one home or a merge waiting). In
  the council's hour (after the homes, no schedule slot) the home most of them have lived a day at is where they came
  (`MergedInto`); a merge only where it has grown-ups of its own, the old council is still kept, and it wasn't founded
  from theirs within the season (a fresh start's party with the old colony handed over in the same act is its founding,
  not a merge; `IsMerge`); a home of theirs alone is their colony moved, 7a's carried council. It waits while some of them
  are still on the road or a fight is on the map (three days at most). Then:
  - *Traditions*: each tradition of their calendar (not its founding day, not someone's own day) by id against the
    colony's (`Tradition`): only they kept it, it joins at half its strength (`Halve`: half its years kept, half its
    weight; still a tradition, lapsing by the usual two years let pass), written down by the calendar's own `TryAdd`
    (inside its cap of twelve); both kept it, the stronger stays (theirs taken when stronger by its standing).
  - *The election*: with them a third or more of the grown-ups (`EarlyElection`), the council's own election
    (`OfficeTracker.Call`) is called three days on, within the season, unless one is under way or was held since.
  - *Names*: in the next naming round (`EpithetTracker.Daily`, `Merged`) each name under their colony's key: one the colony
    had nobody for stays its holder's, held as long as it was; where both had a holder the better by the name's own count
    keeps it (`NewcomerKeeps`, the colony's own on a tie), in a line of news; and for the merge's first season a name taken
    over in the colony makes no rival (`QuietSeason`, `Merges.Quiet` in `Award`).
  - *The calendar*: their old colony's founding day as a remembrance on its own date (`Notify_Merged`, id
    `oldfounding|`, known by the newcomers), let go once none of them is of this colony (`KeepsOldFounding`; the
    abandon's day of release 1 by the same rule).
  - *The welcome*: the feast engine's meal (`PlayFeast.Stage`, the hours 17 to 23, so the first evening) for everyone at
    home, hosted by the speaker (else the eldest of the colony's own), the speaker's greeting from
    `Rimfolk_Pool_MergeGreeting` as the host's cup, the table's lines `Rimfolk_Pool_MergeWelcome`; with answers not
    playing out, the greeting in a bubble. The milestone `colony:merged` and one letter saying what merged (the meal only
    when it was set going). The record is let go once its names were looked at, its election called and its first season
    over. Setting: "Two councils become one" (Many colonies); off, only 7a's rules.
- **Each colony its own character** (3d, 13A; `Character/ColonyCharacters.cs`). `CharacterTracker` is one per colony in a
  `ColonyBook` (`CouncilGameComponent.CharacterOf(map)`, `CharacterTracker.For`, `Of`, `InView`); `comp.character` is the
  seat's, for what the colony as a whole has. Saved as before: the seat's as "character" (its home not written), the
  others (with their home) as "characters" only when there are any; an old save's one character is the seat's. Every
  reading asks the colony concerned: an answer's ways shift its question's colony (dilemmas, events, world events,
  encounters, camps, decrees, the calendar's traditions, a wedding's), its tags and `colonyIs` its map's, a colonist's pride
  or chafing their own colony's (`ThoughtWorker_ColonyWays`, `ColonyTrait`), the tallies where they happened (a visitor's
  remark, a camp hosted, a ritual held), the work names from each colony's own people, guests and chaplain, the
  calendar's long memory its own calendar. Letters and news of a way or a name name the settlement with more than one
  colony; the chronicle's sentence of a name's change is kept under the colony (`History.About`). A colony founded by
  people of another starts with half its mother's (`StartFrom`: half each way, half the work's rates, a name kept only
  where half still holds it, `ColonyTraits.HalfOf`); newcomers alone fresh; a colony that moves (the old abandoned) takes
  all of it (`CharacterCarried`, beside the council). "Walled" needs the calendar's walls-up line of wall sections
  (`MayBeWalled`, 80) at that home, kept from a colony without and let go once they are gone. What stays one: word among
  the peoples (the goodwill a generous or open colony earns) is the seat's ways' only (`CarriesWord`), and what the colony
  is known for beyond its walls (renown) is one, told by the seat's name only (`TellsRenown`).
- **The region near every home** (3d, 14A). `NotableBook.Refresh` staffs each further home's nearest two towns on the
  ground within twice the region's reach, none the seat already has (`FurtherTowns`, `NearestTowns`), beside the seat's
  four near, two dealt with and two new: eight more people a home at most (`NotableCap`), made by the existing rules, two
  an hour. An acquaintance lives in their people's town nearest the colony of whoever met them (`ColonyOfMet`). World
  events are placed near a home picked by its people (`WorldSituations.PlaceNear`, `PickHome`: each home on the ground
  weighted by its people; one home, the seat without a roll); the two running slots stay two.
- **What the player sees** (3c; `UI/ColonyView.cs`). The People view's Table, Moods, Bonds, Faiths and Renown, the
  Chronicle, and the region window's Faces (Peoples) and Here and away follow the colony in view with more than one home
  (`ColonyView.Shows`, `ShowsHome`, `ShowsLine`: a chronicle line placed at it, about one of its people, or of no colony),
  and a chip beside the colony chip in their header, "This colony", turns to "Every colony" with a click for the session
  (`ColonyView.Everyone`, `ScopeChip`; a page builds again at once when either moves, `ColonyView.Version`). The council
  panel on the map names the colony (`HudPanel.HeaderLabel`) and its trust is its people's. The town cards' Who covers
  every staffed town (`NotableBook.AllTowns`), a people's card lists their towns within the colony in view's reach, and
  the faces and here and away keep to its neighbours (`NotableBook.NearColony`, `DipRow.home`).
- **Small ones.** The frost alert looks at every home's fields, the one in view first, naming the other; an outsider's
  town distance and kin among the colony are reckoned from the colony they are at (`Facts`); a favour's bearer is one of
  their people at the colony of the one who asked; a courier's goods for an answer with no map are fetched at its
  colonist's colony; growth needs count a quest's arrivals with no map at the home the quest points at, else the seat.
- **Dev tools.** Debug actions "Many colonies: ..." (`Colonies/ManyColoniesDebug.cs`): two councils become one here now
  (a home's people on this map as the newcomers), each colony's character, the towns near each home, where a world
  event would be placed.

### The world ledger

One way to name anything out in the world, one record for every tie between two parties, and one place to ask about a
town, a people or a person: `World/Ledger/`, one game component, `WorldLedger`. Its pure parts (`LedgerMath.cs`,
`TieMath.cs`, `StoreMath.cs`) are tested (`Tools/Tests/LedgerTests.cs`, and `LedgerMovesTests.cs` for the stores that
moved in, each against the old store's own rules).

- **Refs.** A `Ref` is a kind and a number, a plain value: a person is a pawn's `thingIDNumber`, or a ledger number
  for someone with no pawn, stored below zero so the two never meet; a town is its `Settlement.ID`; a people its
  faction's `loadID`; a colony its home's key (`Homes.KeyOf`); a house a ledger number; a tile of the planet its tile
  id (a layer other than the surface adds its id times ten million). Its text form (`p123`, `p-4`, `t55`, `f3`,
  `c12`, `h2`, `w801`) is what is saved and what history keys and tests use. `Refs` turns pawns, factions, towns,
  homes and tiles into refs and back while the game still has them.
- **Ties.** A `Tie` is one record for any tie between two parties: its kind (favour owed, pact, truce, marriage, ward,
  hostage, fealty, grudge, debt, promise, regard, a family's claim), `from` (the party bound: who owes, swore, promised
  or holds the grudge or the claim) and `to`, its terms (goods, count, days, a free key, a number, and the third party
  it concerns), since, until, its state and where it came from. Open and called in are live; kept, broken and ended are
  closed for good. From open a tie can be kept, called in, broken or ended; from called in kept, broken, ended or put
  off (open again). Only what can be asked for is called in (a favour, a pact, fealty, a debt, a promise, a hostage, a
  claim); a regard only ends. At its until an open debt comes due (called in), any other open tie ends, and one called
  in and not met by then is broken. Three of one kind between the same two at most, sixty of a kind (three hundred
  regards); a new one makes the oldest closed one go and is refused when all are live. Closed ties are dropped after
  sixty days (the history will fold them in).
- **Views.** `WorldLedger.Town(id)`: its name and people, the notables there with their roles and regard, its mood
  toward each colony, the world situations about its people or at the town (running, or ended in the last fifteen
  days), Heard lately, and whether it stands; a town gone from the world is still known by what the notables and the
  news kept of it. `WorldLedger.People(faction)`: its stance toward each colony, what it remembers (the faction
  memory's deeds, strongest first), its wars as the colony saw them, its age (tribal, medieval, industrial or spacer
  by its tech level, as the colony's own era is read) and whether it is the Empire. `WorldLedger.Person(ref)`: name,
  people, town, role and title, and who of the colony met them (the notables' record first, then outsider pairs,
  regulars and departed ex-colonists). A stance is today's: goodwill, the tone of what the people remember
  (`FactionMemory.Tone`) and the regard of `NotableBook.Baseline` with no nature of its own (as `Outsiders.OfColony`
  gives it); a town whose head the colony knows has the head's own regard (`NotableBook.Opinion`). The player faction
  has one stance per people today, so each colony gets the same until the colonies keep their own (many homes, stage
  0 step 7). Views are built when asked and kept for one 250-tick stretch (`Lean/Fresh.cs`), sooner when the faction
  memory, the notables or the ledger change.
- **Persons and towns.** The ledger keeps a record of everyone it knows (`LedgerPerson`: their ref, their pawn by
  reference while the game keeps one, their name for when it doesn't, their people as last known) and of the towns
  they live in (`LedgerTown`: name, people, and when the colony first missed it from the world). Every notable is a
  person: their pawn's ref, or a ledger number (`WorldLedger.NewPerson`) for one known only by name. The ledger keeps
  their pawn and name; the notable keeps its role, town and people. Anyone a tie names is a person too
  (`WorldLedger.PersonOf(pawn)`), so a colonist or outsider the game has let go of is still known by name, and a ref
  turns back into its pawn at once (`WorldLedger.PawnOf`, which `Refs.Pawn` asks first). A person stays while they are
  a notable, a tie names them or a reason keeps them in the world; a town while a notable lives there or while it stands
  within the region map's reach of a home (kept in detail, below: "The region"), and a gone town two years from when it
  was missed (`LedgerMath.KeepPerson`, `RegionMath.KeepTown`, at the day's tidy); beyond the reach nothing is kept. The
  person and town views read these records first.
- **The region's records** (stage 1, `World/Ledger/LedgerRegion.cs`). A town in detail has on its `LedgerTown` what it
  is (`TownKind`: town, and later ruin, post, hamlet), when and by whom it was founded (`founder`, a person of the
  ledger), the town its settlers came from and why (`mother`, `why`), its prosperity, when it last moved and what moved
  it last, and since when it has been low. `LedgerHouse` is a family line known by name (the house ref, its name, its
  town and people, its head as a person, its members by first name only, so a family costs a line in the save): two
  founding families per new town. `LedgerFounding` is a town being built within reach (its site, tile, people, start
  and due, founder, mother town and reason, what was told), six at most, dropped when built or gone. A founder is kept
  in the world with `KeepFor.Founding` while a founding or a town record names them, then let go as a notable is.
- **What the ledger owns** (stage 0 step 3b). Each tie that lived in another store is now the ledger's own `Tie`,
  written and read through it by a small helper beside the system that uses it; the old store keeps only what isn't a
  tie, and an older save's ties move in on load (each store, once its save has loaded):
  - *Grudges* (`Happenings/Grudges.cs`): one Grudge tie per people against the colony, the prisoners of theirs who
    died in its hands in its key and how many are not yet settled in its count (`GrudgeTally`: ten at most over all
    peoples, the oldest first; settled ones and those held two years go at the day's tidy). Kept once its last went
    settled (the revenge came), ended once forgotten. The deed "prisonerDied" stays the people's memory.
  - *Favours* (`Outsiders/OutsiderTies.cs`, `Favours`): from the outsider's people to the colonist who asked, about the
    outsider, goods and count in its terms; brought keeps it, thirty days with nobody coming lets it go with its
    message. The bearer on the way stays unsaved, by the tie's id.
  - *Good words* (`GoodWords`): a Promise from the outsider to the colonist, about the notable's person, with how much
    it moves them and the notable's record in its terms; it lands (kept) on whoever holds that place by then, and ends
    twenty days on exactly when the old list let it go (`StoreMath.After`).
  - *Guest kin* (`Integrations/GuestWill.cs`, `GuestKin`): a new kind, Claim, from the family's people to the one who
    joined, about the colony keeping them, the due day and "forced" in its terms, since the day they joined (an older
    save's have none). A claim can be called in: the family coming to ask presses it; going home with them keeps it;
    one called in and not met in a fortnight is broken.
  - *The scenario's silver debt* (`Scenarios/ScenarioDebts.cs`): a Debt from the colony to the people it owes, the
    silver, the times put off and the story in its terms, coming due (called in) at its day; paid kept, refused
    broken, half paid put off. The memo stays as the scenario's handle: its place in the scenario's hour, so events keep
    their order, its story, the one it's about, its colony, and when the scenario next looks.
  - *The grant of land* (`ScenPart_CouncilBeholden`): a Fealty tie from the colony to the people who granted it, about
    the lord who did; the deed stays what they remember (their tone and their notables' regard read it).
  - *Regard*: a notable's own regard of the colony and a regular's bond are Regard ties (from them to the colony, the
    score the number), fed and faded where they always were, ended when the notable or regular goes.
- **What stays a view.** `TiesOf(ref)` and `TiesBetween(a, b)` also return the ties still kept in other stores, marked
  as views (`Tie.IsView`) and not editable through the ledger: a vendetta (the avenger's grudge against a people: the
  vendetta is the colonist's own story), a notable's regard of each colonist, an outsider pair's warmth and
  familiarity, and a camp's warmth toward each colonist. Those regards stay with their records because moved in, the
  notables' regard of up to eight colonists each (272) with the pairs (60) could pass the ledger's three hundred
  regards, and a regard refused would change play.
- **Keeping someone in the world.** One way: `WorldLedger.Keep(pawn, reason)` and `Release(pawn, reason)`. A pawn stays
  in the world while any reason holds (`KeepFor`: a notable, a world situation by its id, a memo, a departed colonist,
  a regular, a camp that may come back, a story chain); the reasons are kept on the person and saved. The game is told
  as Rimfolk always told it, through its world pawns' forceful keep (`Find.WorldPawns.ForcefullyKeptPawns`, the set
  `PawnDiscardDecideMode.KeepForever` adds to), added when a reason is given; Rimfolk patches nothing of the world's
  clean-up. Each part still lets go its own way (back to the world's own keeping, or gone for good if Rimfolk made
  them), once `Release` says nothing else holds them. `WorldLedger.Held(pawn)` is the one "held elsewhere" check: a
  reason here, or a part that keeps people without a reason of its own (a nemesis, a raid one of them leads, an open
  mission, whose quest reserves them in the game's own way, a world situation's stranger or other). An older save's
  reasons are noted once on load, and a reason whose part let go without saying so is dropped at the day's tidy. So
  someone two parts need is no longer let go by one of them while the other still needs them.
- **The rule.** New features about the world read and write through the ledger: they name parties with refs (a person
  through `PersonOf`, so the ledger knows them), keep a tie between two parties as a `Tie` (not a new list of their
  own), keep someone in the world with `Keep` and a reason of their own (never the game's keep directly), and ask
  about a town, a people or a person through its views, never through the notables', the memory's or the outsiders'
  insides.

It has no pass of its own: ties reaching their end, closed ones dropped, and persons, towns and reasons nothing needs
any more are seen to when the ledger is asked (ties at most once a 250-tick stretch, the rest once a day), and before
saving. The history it keeps folds once a day (below).

### One history

One history of the world, which later features (the region's towns, the diplomacy, letters and talk that remember)
read and write instead of keeping lists of their own: `World/Ledger/History.cs`, kept by the world ledger and saved
with it. Its pure parts (`HistoryMath.cs`: the entry, the kinds, the book with its indexes and folding) are tested
(`Tools/Tests/HistoryTests.cs`).

- **An entry** (`HistoryEntry`; the game's own `HistoryEvent` is Ideology's): an id, when, a kind, who took part (refs
  with a role each: did, for, against, about, with; the one it is mostly of first), where (a colony, a town, a tile, or
  none), how much it matters (everyday, or a milestone by the chronicle's own rule), and its words: the finished line
  as the chronicle wrote it, or a text key with values (a deed's). A summary stands for several: its count, from its
  first to its last.
- **Kinds** are small keys with families before a colon: the chronicle's twenty kinds as they are (`loss`,
  `wishGranted`...), `world:began` and `world:ended` for world situations (in the chronicle's family `world`),
  `deed:` and the deed's kind for what a people remembers, `tie:`, the tie's kind and what happened (made, kept,
  calledIn, broken, ended, putOff) for the ledger's ties, and `world:founded`, `world:built`, `world:razed`,
  `world:taken`, `world:vassal` and `world:battle` for what other mods do to the world (below), and `office:promise`,
  `office:kept` and `office:broken` for a seat's promise and how the next election judged it (milestones with words,
  on the holder's card, never in the chronicle: "Elections on a record"). Asking for a family finds every kind in it.
- **What writes.** Every line told for the chronicle: `AddChronicle` writes it into the history and nowhere else (its
  tick, words and kind, the colonist as an actor, their colony as the place; a milestone of the chronicle is one here;
  `History.About` adds others who took part, as an election's one line does for every seat filled). Every question's
  answer (`Answers`, below). A world situation's lines carry its place, its people, its enemy and its own person
  (`History.Situation`). The deeds the peoples remember (`FactionMemory.Note`): the people and the colony with their
  parts by the deed's side, a run of the same within a day one entry with a count (a raid's fallen), a deed of size 15
  or more a milestone. The ledger's ties when made or moved, and when they reach their end at their until. The region's
  and diplomacy's own events, with their words.
- **The chronicle is its view** (the player's decision of 6 November 2026, phase 1 of the review: one record). Colony →
  Chronicle reads the history (`Chronicle.Lines`, newest first, built again when the history changes): every line told
  for it, every answer, and the colony's own milestones written elsewhere with words (a brigands' hold found, a trading
  post opened, a pact: at one of its homes or with its people in it). Never a deed, a tie or a far town's news: those
  are the region window's History. Which kinds it shows and under which of its kinds, and a season's summary in words,
  are `ChronicleMath` (pure, `Tools/Tests/OneRecordTests.cs`): the chronicle's twenty kinds, a situation's `world:began`
  and `world:ended` (the region's other `world:` kinds are its own), an answer under its question's kind (a small one
  not at all). A summary reads "Harper made 5 wishes. (Day 12 to Day 26)"; an answer adds what came of it ("It took
  place (the feast: 12 came).") and shows its whole record on hover. So it reaches back to the first day: the last
  sixty days line for line, older seasons' small lines as one each, milestones as told (the save at day 128 had lost
  everything before day 41 in the old list). The overview's lately, the Wishes and Ambition pages' earlier lines, the
  year's letter, the calendar's backfill, the storyteller's tale, the travellers' reason to visit and the faith's
  "told already" all read the view or the history. Nothing is kept twice: the old list (300 lines) is gone.
- **An older save.** Its chronicle list is read on load and never written again (`CouncilGameComponent.chronicle`, in
  LoadingVars only); at every load the ledger takes it into the history (`HistoryBook.TakeChronicle`) and lets it go.
  A line comes in unless the history has it already: the same tick and words (as many times as it is there), or a
  season's summary of its kind and of the one it is of whose days hold it (folded into it, it counts there). A save from
  before the history had its whole list come in; a later one had every line in the history already (where its lines
  happened isn't known: the seat). No error is logged.
- **The answers' log** (`Dilemmas/Answers.cs`, pure `AnswerMath.cs`). Every kind of question letter writes one entry of
  the family `answer` when it is answered or its time runs out: the dilemma, Rimfolk's event, the world event, a camp's
  word, the calendar's day, the meeting, the election (backing a candidate or calling a seat off, where it's done;
  letting the vote run a small one), a brigands' hold's terms, a hamlet's ask, a journey at the gate, and the thirteen
  of diplomacy (a call to arms, an envoy, two ransoms, a vassal's ask, a ward, a hostage forfeit, a match, a feast, a
  favour, fealty, a pact, a truce): 23 letters, `AnswerMath.Sources`. Its values: the question, the answer, the
  follow-up's first sentence, how it went, a word on it, and whether nobody answered. Its line is the chronicle's
  (`CC_Answer_Line`, "the council chose", one wording for dilemmas and events alike; a meeting's choice and a kept day
  keep their own lines, which become the answer's). What the answer set going on the map is watched (`AnswerWatch` in
  `WeekMemory`, a light look every 250 ticks and only while one is): waiting, under way, then taken place (with the
  play-out's note) or never come about; a gamble gone wrong stays so. A small answer (an event never chronicled, letting
  the vote run, waiting an hour at the gate, letting a day pass) is kept and listed but not chronicled. The letters'
  own options are wrapped (`Answers.Recorded`, `RecordedAll`: the letter's stock options pass as they are). Council →
  Dilemmas lists every question's answer: the dilemmas from their own log (with what each answer was to do), the rest
  from the history, sorted by day, filtered the same way.
- **The year, told once** (`World/YearLetter.cs`, pure `YearLetterMath.cs`). It was told four times within two days
  (the annals at the calendar's new year, "the year on this land" at the landing's anniversary, the founding day's
  letter, and their chronicle lines). Now one letter on the landing's anniversary, the colony's founding day (the
  first days of a new year since the landing), with the annals' setting, written by the annalist or whoever reads and
  talks best: the people first (the annals' figures, `AnnalsTracker.PeoplePart`, the richest of the three, counted
  from one landing day to the next; wishes and ambitions counted from the chronicle's view, a summary for all it
  stands for), then the land and the world (the year letter's part), then the founding (who has been here since the
  first days, the dead, the colony's name on the roads, its customs: the founding event's own lines, `YearFacts`), and
  the author's last word. A year counted from part of the way through (the mod added midway, or an older save whose
  annals ran by the calendar year) says since when. The founding event keeps only who keeps the day tonight (the last
  of the first, the Founder, the first child grown) and asks how to keep it; the calendar's letter, when Rimfolk's
  events are off, asks the same. The chronicle has one line ("Harper wrote down the colony's second year."), and the
  Chronicle's header has a button for each year ("The second year"; an older save's annals keep "Annals of 5501").
- **Less noise in the record** (the systems review's list). A fight's comradeship is told only when something changed:
  the pairs who count each other comrades for the first time and who carried whom out of the fight, in one message and
  one line; the roster of everyone after every raid is gone (the battle's own line names the best fighter). An
  election is one line ("The colony elected its council: Harper as speaker and Hector as marshal."), everyone seated in
  it. Those who mark the same years in the colony on the same day are one line. A day kept from its letter is one line
  (the answer's), and the founding event's answer is the founding day's line.
- **Folding**, once a day: everyday entries older than sixty days fold into one summary per kind, per the one they are
  of and per season (the planet's quadrum); milestones stay as they are, and a lone one in its season stays as it was.
  The bound is 800 entries. Over it, the oldest everyday ones from before those sixty days go first (older seasons'
  summaries), so the last sixty days stay whole; only when those and the milestones alone are too many do everyday
  ones fold sooner (from thirty days, then fifteen); then the oldest everyday ones go, then the oldest milestones, the
  newest 200 always kept. Far over it between folds (a quarter past), the oldest everyday ones go at once.
- **Asking.** `History.Of(ref, kinds, since, max)`: what someone (a person, a people, a town, a colony) took part in;
  `History.At(place, ...)`: what happened there; `History.Latest(kind, who, place)`; `History.Count(kind, who, place,
  since)` (a summary counts all it stands for); `History.Get(id)`; `History.Text(entry)`, its words (a deed's from the
  memory's keys: "traded fairly with them, 11 times"). The newest first, from indexes by actor, by place, by kind (each
  family too) and by id, kept up as entries come and rebuilt on load. The ledger's views carry the newest twelve: a
  town's what happened there, a people's and a person's what they took part in.
- **Places of world events.** A world situation and its record keep a place ref: the town when it is at a settlement
  (`WorldSituation.At(town, orName)`, which sets the name the texts use with it), else the tile, else none (no
  situation today has a tile without a town). Older saves have none; a town's view still finds those by name.

The history asks nothing heavy of the game: no pawn searches, a few index lookups an entry, and its fold has a pass of
its own (`Pass.DailyHistory`, timed by Rimfolk's clock).

### One name

Phase 4 of the review, part 1 (the player's decision 6 of 6 November 2026: one slow "known as" per colonist and one
"known for" card per colony; reputation's six sides stay as details). The save at day 128 showed a colonist carrying up
to four names that churned ("the Healer" went Harper, Alo, Harper, Alo in 70 days; Harper held six names in 80 days;
Hector was "Hector, the Builder, Beastfriend, Tinker" on his card, with renown's "Known as the one who ..." as a second
line), and the colony told by four systems in four places (our ways and the colony's traits on Council → Ways, renown
under People, what each people remembers in the region window), with their labels and lines in the chronicle each.

- **A name holds** (`Epithets/NameMath.cs`, pure, `OneNameTests`; `EpithetTracker.Names`). A name still goes to whoever
  has the most of its record past its threshold, but once given it is held (`NameHold`: since when, who is clearly ahead
  and since when with both counts then, who had it before): nobody can take it for `HoldDays` (20); after that it
  passes to the best other who is clearly ahead on the count (`ClearLead`, a fifth, or the name's own margin if larger)
  every day of a week (`ChallengeDays`, 7; a day not ahead ends the challenge, a new best begins their own) and over
  that week does more of the work than the holder (`Outdid`). A holder still at the work as much keeps it and the week
  starts again. The one who lost it can't take it back for 30 days (`ReturnDays`), so it never goes back and forth.
  The timings are the player's retune of the same day ("titles should be able to change if someone surpasses the
  current owner"; 0.110.0 held a name 30 days, then asked a quarter ahead for 15 days with half again the work, and a
  year before it came back): calmer than the old 15%, not frozen. On a model of the save's two doctors taking turns at
  the sickbeds (`OneNameTests`), the old 15% passed the name three times in 80 days and the rule never; turns of 5 to
  30 days, either first, over a year, pass it 0.28 times a year and never more than once in any 60 days; one who takes
  over the sickbeds has it a week after the holder's 20 days, one doing twice the holder's share ten days after; a
  holder who stops loses it, once. A name lost by its condition (the Unshaken's break) goes at once, as before.
  The one who lost the name they were known by writes it once in their journal (`Rimfolk_Journal_EpithetLost`). Saved
  packed per name (`EpithetTracker.holds`, through the field `holdsSaved`, `NameMath.Pack`), one per name held.
- **One "known as" per colonist** (`Epithets/KnownAs.cs`, the one place). Its short form is the colony's name for them,
  one of those they hold (`EpithetTracker.Chief`, `NameMath.Chief`: a proud name before a dubious one, then the one held
  longest, so a name earned later never pushes an older one aside; Hector the Builder, Harper the Vault, Alo the Healer).
  Its line (`KnownAs.Line`, `NameMath.LineKey`) says it with what the peoples know them for, read from the history's deeds
  and the peoples' memory (`Renown.Own`, unchanged) and how far it carries: "Known as the Healer, the one who won the
  wrestling at the feast with the Lenlia, talked about in the next town."; "Known in the colony as the Healer." while
  nobody outside speaks of them; a bad name abroad beside a good one at home in two halves; renown's own line for one with
  no name at home. Under it, the details (`KnownAs.Details`): the colony's other names for them ("Also called Beastfriend
  and Tinker.") and reputation's six sides as they show (`Reputations.SidesLine`, the character page's first line).
  Readers: the card's title and its Who lines (`KnownAs` registers them; renown's own colonist line went), the Story tab's
  header, the People table and its tooltip, the wishes' tooltip, the annals and the vendetta's eulogy tags
  (`EpithetTracker.NameOf`), the death letter (`Chief`), renown's "the one the colony calls ..." in talk and letters
  abroad (`Renown.Held` names the chief), People → Renown (each of the colony's people by their one name, with the
  details; "Known by name"). The chronicle writes "X became known as Y." only when it is the name they are known by now
  (`Award`); a second name is the news and their journal. What other systems count stays every name held
  (`EpithetTracker.Of`: a ballot, renown's points, the heart, the events that ask for one known for a skill).
- **One "known for" per colony** (`Character/ColonyName.cs`, pure `ColonyNameMath.cs`). The four keep their data where
  everything else reads it (the ways' tiers and tags, the traits' tags and leanings, renown's band and factors, the
  peoples' deeds and tones); the player reads one card: what people call the colony, its ways and what its work made it
  in one list (`ColonyNameMath.Order`: a way known far and wide first, then the ways, then the traits), and what it is known
  for beyond its walls and how far its name carries, in one sentence (`ColonyName.Main`, `ColonyNameMath.CardKey`:
  "People call the colony traditional, generous and walled; beyond its walls it is known for good doctors, its name talked
  about in the next town."); then what it comes from (renown's deeds, `RenownPanels.From`; the council's latest three
  choices behind the ways it is called, from the ways' recent shifts) and what is held against it; what each people
  remembers, a line each (four at most, the strongest first: how they stand, what they remember best); the notables who
  know its people and what its name does (`RenownPanels.Reach`). Where: the top of Council → Ways ("The colony's name",
  the ways, the traits, who's proud and who chafes, what shaped it below), a home's card where the colony is shown to
  others (`CardSections.HomeLines`, in place of renown's), the Overview's line, the Culture tab, the founding letter's and
  the press article's "what travellers call the colony" (`ColonyName.CalledWords`). What it is known for beyond its walls
  holds while it is still enough by renown's own rule (`ColonyNameMath.HeldKind`), so two kinds close together don't take
  turns.
- **One sentence when it changes** (`ColonyName.Look`, `ColonyNameMath.Key`, `Diff`, `Clauses`). The name is kept as a
  key (`CharacterTracker.nameKey`: what it is called with each one's strength, what it is known for beyond its walls)
  and looked at after the ways move and at the end of the council's day: whatever changed (a way or a trait gained or lost,
  a way known far and wide, what it is known for beyond its walls) is one chronicle sentence ("People have started calling
  the colony walled and they have stopped calling it restless."). A way that falls from far and wide to known is still what
  it is called: nothing in the chronicle, and its news says the neighbours still call it that (`CC_Name_Ways_Narrower`;
  "people have stopped calling the colony merciful" was untrue of it). The ways and the traits write no chronicle line of
  their own; their letters and news stay. A move of renown's band stays renown's own sentence (its five wordings, with
  what it is known for), and the name's key takes it without another word (`ColonyName.Settle`).
- **An older save.** Names already held stay. A name with no hold is held from the day the history wrote it given to them
  (the newest "X became known as Y." line of theirs, `NameMath.SameLine`), else from its first look, so the hold may be
  over and the new rule applies from that look, but nothing moves at once (a challenge needs a season) and nothing is told.
  The colony's name has no key: its first look keeps it as it stands without a word; a part not read (the ways off,
  renown not yet looked at) is never a change. On the player's save: Hector the Builder (also Beastfriend and Tinker), Harper
  the Vault (also the Old Hand and the Bottomless), Alo the Healer (also Goodhands), Penguin the Cook (also the Unshaken),
  Stace the Mender (also the Scholar), Barr the Sower, Macy Greenhand; the colony "traditional, generous, learned, devout,
  artful and walled".

### Facts

One way to know what is true for a text, and one way to fill it in, so a line is written once and reacts to the same
world wherever it is said: the facts service (`Text/Facts/`) and the fill (`Text/Fill.cs`). Their pure parts
(`FactSet.cs`, `FactsMath.cs`, `Fill.cs`) are tested (`Tools/Tests/FactsTests.cs`).

- **A scene** (`FactScene`): who speaks or writes (one of the colony), to whom (someone out in the world), at which
  colony, their town and their people, how it goes (a tone, the tag `tone:X`), and the providers it asks for, in order.
  `FactScene.Talk(colonist, outsider, tone)` is a conversation with an outsider (every provider);
  `FactScene.Letter(notable, writer, home, news)` is a letter's answer (the ties and the history, and the world's news
  when it tells some); `FactScene.Event(home, people, town, stranger)` is one of Rimfolk's events about a people, with
  nobody speaking (what their people remember of the colony, the ties, their town lately, the region's word on it:
  `deed:`, `deedbest:`, `wrongbest:`, `tie:`, `town:lately`, the region's tags, and `{DEED}` and `{WRONG}`, the good turn
  remembered best and the worst wrong in words). `HappeningContext.Facts` reads it once when the event's text is first
  picked: its tags join the event's own (an event about one deed of theirs keeps to that deed) and its values fill the
  lines that name them. The talks also ask the region's provider (`region`, after the history; see "The region").
- **Asking.** `Facts.For(scene)` gives a `FactSet`: tags for the lines' conditions; the speaker's and the listener's own
  tags for their own lines (traits, age); values for the placeholders, each with the fact behind it where a later
  mention can follow it (`ws:12`, `war:5`); how current each tag is (heat: `FactsMath.Fresh`, new news first); earlier
  mentions' marks with what became of them; and the world's news near the place, as finished lines. Providers only
  read: no dice rolled, nothing changed (the ledger's ties are read as they stand, without its upkeep). Each runs on
  its own, so one that fails leaves the others' facts. A fact the game can't give is left out, and a line that needs
  it isn't said. Nothing is saved and there is no pass: facts are read when a text is about to be written.
- **Providers**, by file: the moment, their people, the two of them, what passed between them (`FactsWho.cs`: moment,
  people, persons, pair); their town, the land around it, its notables, places and work near it (`FactsPlace.cs`:
  town, land, notables, places); wars and peace, the world's events, the conditions over the land (`FactsWorld.cs`:
  wars, situations, conditions); the colony's news, its hospital, what their people remember of it
  (`FactsColony.cs`: colony, hospital, deeds); earlier mentions (`FactsMarks.cs`: marks); the ages, the ties between
  the two sides and what the history holds lately (`FactsLedger.cs`: ages, ties, history); the world's news near their
  town (`FactsNews.cs`: news). The first fifteen are the outsider talks' sections as they were; a talk asks for them
  in the order they were always read, then the ages, the ties and the history.
- **Families of tags.** The moment's own (season, hour, weather, the colony's age, wealth, size), `tone:`, the two
  of them (`first`, `met`, `friends`, `did:`, `talks:`, `last:`), `role:`, the listener (`them:`) and the speaker
  (`us:`), their people (`their:`: their age, the Empire, their leader, war and peace, a friend of theirs among the
  colony), their land (`home:`), their town (`town:`), wars (`war:`, `world:`, `battle:`), the world's events
  (`situation:`, `ended:`, `ws:`, `condition:`), the colony (`colony:`), what they remember (`deed:`, `deedbest:`,
  `wrongbest:`, `byyou`), earlier mentions (`talked:`, `then:`), and the ties (`tie:`). The full list, with the
  placeholders, is in the header of `Defs/OutsiderTalkDefs/Rimfolk_OutsiderTalks_Greetings.xml`; the talks' tests know
  each one.
- **From the ledger and the history** (new with the service):
  - *Ties*: a favour owed, a promise, a claim, a debt, fealty or a grudge between the two sides (ours: a colony, the
    colony's people, one of the colony; theirs: the one spoken to, their people, their town, one of their people), the
    ledger's own and those kept in other stores (a vendetta, a vassal's fealty), of each kind the one the speaker is
    party to, else the newest: `tie:KIND` (current, more so made in the last ten days), `tie:KIND:ours` or
    `tie:KIND:theirs` (which side is bound: owes, promised, swore, holds the grudge), `tie:KIND:you` (the speaker is a
    party), `tie:KIND:due` (called in), and the party it is toward by name: `{FAVOURFOR}`, `{PROMISEDTO}`,
    `{CLAIMON}`, `{DEBTTO}`, `{FEALTYTO}`, `{GRUDGEAGAINST}`.
  - *Lately*: what the history holds of their town and of the one spoken to in the last thirty days: `town:lately`,
    `them:lately`, and the same with each event's kind and family (`town:lately:world:taken`, `town:lately:world`,
    `them:lately:tie:favour:made`).
  - *Ages*: the colony's age as lines are tagged everywhere (`Flavor.AddEraTags`), their people's (`their:medieval`,
    `their:lowtech`, `their:empire`, from `Ages`), and how it stands to the colony's: `their:sameage`,
    `their:behind`, `their:ahead`.
- **Callbacks.** A line that leaves a mark keeps the fact behind its value; when the two meet again, the marks provider
  reads what became of it (`then:still`, `then:over`, `then:dead`, `then:changed`) from the game itself: a world event
  still running, a war still on, a notable's health, who leads now, a site or a quest. The history and the ledger
  don't keep these by the ids a mark holds (a situation's id, a quest's), and their views are kept for a 250-tick
  stretch where a callback reads the moment, so these stay with the game.
- **The fill.** `Texts.Fill(text, values)` fills each `{KEY}` that has a value and leaves every other placeholder as
  written, for the game's grammar or a context's `Format` after (`{PAWN_nameDef}`); `Texts.FillLine(text, values,
  first)` is a line said aloud, by the talks' rule (every placeholder, an earlier talk's own values first, and a line
  that starts with a value in lower case capitalized); `Texts.Holes` lists a text's placeholders; `Texts.WithThe`
  gives a value that comes with "the" ("the gray cough"). A placeholder is the innermost pair of braces, as the def
  validator reads it. The talks, the neighbour letters, the outsiders' bubble lines, events' and chains' lines, the
  camps' asks and words, the road post's word and group names fill through it. Three fills hand pawns to the game's
  own grammar (`{A_nameDef}`, `{OTHER_pronoun}`) and stay with it: the calendar's days, the play-outs' words, the
  pressures' labels; the council's own (offices' lines, an answer's impact hints) move in a later pass.
- **A new text system** makes a scene (or a builder beside `Talk` and `Letter`) asking for the providers it needs, calls
  `Facts.For`, picks its lines with the facts' tags (`LineTags`, `Flavor`) and fills them with `Texts.Fill(text,
  facts.values)`. A new fact is a provider in `Text/Facts/` (registered in `Facts`, named in `FactsMath`), with tags
  in an existing family, listed in the talks' header and known to their tests. Facts added to a scene change no choice
  until a line names them: the tests choose 1500 generated talks with and without the new facts, and match every
  letter line as before.
- **Shipped words found untrue.** A shipped line's text and tags stay as they are (`Tools/textcheck.py`); one found
  untrue in a case its tags let through is put right where it is picked: a conversation piece by
  `OutsiderTalkMath.AlsoWhen` (its name and the condition it needs besides its own), a line of a flavour pool by
  `Lean/ShippedLines.cs` (its text as written, and the condition it needs, or retired when it is true in no case its pool
  is drawn for), which `Flavor.Match` asks once per line and keeps with it (`LineTags.Parsed.also`). Better words for the
  other case go in a file of their own. The tests check each is still in the defs, word for word.
- **Words by the animal's kind.** The herd's letters and journal lines take the young's word from the kind's first life
  stage ("the first chick of spring", `HerdStoryMath.YoungWord`; "spring's first born" for a kind with none), hatched or
  born by whether its kind lays eggs, and old age and the leader's ways from its body (`HerdStoryMath.BodyOf`: a beast,
  gray at the muzzle; a bird, ragged in the feather; anything else, slow in everything); the journal's tag is
  `body:beast`, `body:bird` or `body:other`, and `hatched` or `born`.
- **Story text in words.** A count in the chronicle, the annals and the year's letter is said in words for one and for
  many (`Texts.CountKey`: "marked a year", "marked 2 years"), not "(s)"; a life goal's spark is said in words for each
  memory that can spark one (`CC_Asp_Spark_thought_DEFNAME`), never the memory's label quoted; a wish in the middle of a
  line is not capitalized (`Wish.LabelInline`), and every wish is a thing to wish for; a chronicle line begins with a
  capital whatever label leads it (`ChronicleEntry`), and is a sentence, not a letter's label ("Known as Walled"); a
  conversion the faith told isn't told again by the mark it leaves (`Faiths.ToldConversion`); a world event's end in the
  year's letter is its ending's word or "is over", never the ending's key.

### Telling the player

Every Rimfolk system tells the player through one teller, `UI/Tell.cs`, and the teller picks the channel (the
player's decision 2 of 6 November 2026): by how much a happening matters, its importance and its kind of news, and by
how busy the colony's day already is. A system says what happened and how much it matters; it doesn't decide alone
whether that is a letter, a message, a line of the evening's news or only the history. New code tells the player through
the teller; a second way is a bug.

- `Tell.Message(kind, importance, map, text, look, type)`: something that happened, as a message now if the day allows.
  The "How many messages" setting decides by its importance whether it's told at all (colour only with "everything").
  Something hard to miss (harm, a death near, a mission's timing) is always shown, and makes the day busy for six hours.
  Something worth knowing is shown until the colony's messages for the day are reached (12 by default, all kinds
  together, the setting "Most messages a day from Rimfolk"), then waits for the evening's Colony news. On a busy day
  (half the day's messages shown, something hard to miss told in the last six hours, or a raid on the home in them:
  `storyState.LastThreatBigTick`) one person's news, their health and the land's wait for the evening while the
  colony's and the world's still come (`TellMath.Weighty`). Nothing held back is lost.
- `Tell.Minor(...)`, which `News.Post` calls: the smaller news, to the evening's letter, a message or the History tab
  by the "Colony news" setting. As a message it is weighed as messages are. The same words shown as a message within
  four hours aren't put in the evening's news again (`TellBook.Told`): told once, by whichever channel came first.
- `Tell.Letter(kind, importance, map, label, text, def, look, faction)`: a letter that only tells. Past the colony's
  letters for the day (4 by default, all kinds together), or a person's or the land's on a busy day, it becomes a line in
  the evening's letter. Important letters and threats are never held back.
- **The evening.** The Colony news letter has less room the busier the day was (`TellMath.EveningRoom`): eight lines
  after a quiet day, one fewer for each letter, message or question the day already brought, three at the least. It
  carries the lines that weigh most (`EveningScore`: hard to miss first, then what the day held back, then the colony's
  and the world's before one person's, the earlier first among equals) and puts the rest in the History tab's message log.
  Each line keeps its weight through a save (`NewsItem.weight`; an older save's are worth knowing).
- `Tell.Chronicle(entry)`: a line for the colony's story. Every system's `AddChronicle` comes here; the teller writes
  it into the one history, and the chronicle shows what its view selects (`ChronicleMath`, "One history" above).
- `Tell.Ask(letter, map)` and `Tell.CanAsk`: Rimfolk's questions (choice letters). Never capped; those that can wait
  check `CanAsk` first, which is `LetterGap`'s twelve hours between questions. A question counts toward how busy the
  day was (the evening's room).
- With more than one colony, what happened at a home names its settlement (`Homes.Name`): "Oakhold: ..." on a message
  or a line of the news, "At Oakhold." heading a letter. With one colony the text is as it always was.
- `Notify.Message` goes through `Tell.Message`, counted under the kind of news of its caller's folder
  (`TellMath.KindOfFolder`). Overflow past the 40 waiting for the evening goes to the History tab's message log.

The counts (`TellBook`, in `UI/TellMath.cs`, pure and tested) are kept by colony and channel for the day, all kinds
together (the old caps of 20 messages and 6 letters for each kind folded into one count a colony), with when the colony
was last told something hard to miss and the day's tally of each channel; they aren't saved, so a load only lets a
little more through. The settings moved to new keys (`messagesADay`, `lettersADay`), so an older settings file's caps of
each kind don't stand for the colony's whole day. The dev menu's "Teller: what was told today" logs the day's tally,
colony by colony (letters, messages, the evening, the History tab, merged, the chronicle).

Measured (`Tools/Balance --tell`, the simulator's telling stream for a colony of ten, the old way and by the teller with
the colony's pace, 300 colony-years): letters and messages together 1.92 a day before, 1.64 after, nearly all from the
gatherings' own messages (0.67 to 0.39, see the pace below); the busiest day's letters and messages 12 to 8; questions
and letters that only tell as before (an ordinary day is under the counts); lines in the evening's news 2.2 to 2.3 (a
day kept at supper is a line there). The simulator models part of what Rimfolk tells (not the region, diplomacy,
visitors or couriers), so these are the same stream before and after, not the game's whole day. On the player's save
(the archive's last 13 whole days, Rimfolk's letters and questions picked out by their titles, its messages not kept by
the game so not counted), the evening's news would carry 3.3 lines a day instead of 3.9, 14% fewer, more on a day the
messages are counted too.

What stays a direct `Messages.Message`: the answer to the player's own click, for the game's own sounds and timing. A
rejection (`RejectInput`), a button's or designator's confirmation (`SilentInput`, `TaskCompletion`, `CautionInput`),
a window's feedback, a developer's button. One that uses another message type carries a `// direct:` comment saying
why (the player's promise or refusal, an appointment, a pick in a letter); `TellTests` checks every direct message and
letter against this. The other direct letters are the colony news itself, the welcome and update letters, and the
storyteller's own ailment incident (`SendStandardLetter`).

### Other mods' world mechanics

Some mods already run parts of the world well (territories and new towns, battles between peoples, outposts, a
marriage alliance), and the Empire has its titles. Rimfolk reads them through one small interface and keeps none of
their state twice: `Integrations/World/`, an abstract `WorldAdapter` with an adapter per mod, behind the facade
`WorldMechanics`. The facade finds every adapter by its type, so the core never names a mod; each adapter knows its mod
by its package id and its types by name, reaches them by reflection or by Harmony patches on the mod's own methods
(each a `[HarmonyPatch]` class that stands down without the mod and follows the deeper-hooks setting), and does
nothing when its mod isn't there. Adapters read and write the history; they tell the player nothing.

- **Asking.** `WorldMechanics.TerritoryOwner(tile)`: the people who own a tile, or null (nobody, a contested tile, no
  such mod). `SettlementsBeingBuilt()`: each site's tile, people, start and due tick (`Progress(now)` from 0 to 1).
  `OurVassals(colony)` and `OurHoldings(colony)`: the colony's vassal towns, and its vassals and outposts, each under
  the home it names itself (an outpost's delivery colony) while that is a home, else the nearest one. `Title(pawn)`:
  the royal title someone holds. These lists are read again at most once a 250-tick stretch.
- **Ties, as views.** Each vassal's fealty to its colony (from the vassal, as a town, to the colony; since the
  `world:vassal` entry when the history has one) and a marriage alliance (from the allied people, who proposed it, to
  the colony, about the child who married, the spouse in its key) come back from the world ledger's `TiesOf` as views
  (`WorldLedger.Today`). The mods' own world objects and components stay the record: nothing is saved twice, and
  nothing is left behind when a mod is removed.
- **The history.** The hooks write what those mods do: a people began a settlement (`world:founded`, at the tile) and
  finished it (`world:built`, at the new town, about the tile), the colony's people beat a settlement being built
  (`world:razed`), a town taken by another people, the colony among them when a vassal of its is taken
  (`world:taken`, at the town as it is now, about the one it was when the mod made it afresh), a town made the
  colony's vassal (`world:vassal`), and two peoples fighting on one of the colony's homes (`world:battle`: both "with"
  while it lasts, then the winner "did" and the loser was "against" once the mod's own trigger says who won, by
  `HistoryBook.SetRole`). What touches the colony is written wherever it is and is a milestone; the rest only within
  the region map's reach of a home, as everyday events (towns far away stay the mods' business). The small rules are in
  `WorldMath.cs` (Tools/Tests checks them).
- **The adapters.**
  - Faction Territories and Vassalage: territory owner (`TerritoryOwnershipCache.TryGetSingleFactionIfUncontested`),
    settlements being built (its `SettlementConstruction` world objects), the colony's vassals (its
    `FactionTerritories_VassalOutpost` world objects, with their people and name); hooks on
    `Expansion.Utility.TryCreateForFaction`, `TryComplete` and `IsDefeated`, `Invasions.Utility.ResolveWithWinner` and
    `VassaliseUtility.ExecuteVassalisation` / `ExecuteVassalisationAtTile`.
  - [SR] Factional War: a battle begins when `IncidentWorkerFactionWar.TryExecuteWorker` succeeds on a home (its two
    peoples), and is won when `TriggerFactionAssaultVictory.ActivateOn` fires (the lord's people beat the one it
    watches). Its fights on sites away from the homes aren't written.
  - Vanilla Outposts Expanded: the colony's outposts (`Outposts.Outpost` world objects: name, kind, delivery colony,
    how many are posted). A view, nothing more.
  - The Empire (Royalty, through the game's API): a person's most senior title, in the ledger's person view
    (`PersonView.royal`).
  - Diplomatic Marriage+: its permanent alliance (`PermanentAlliance.WithFaction`, `PlayerBetrothed`,
    `NpcMarriageSeeker`, read only; its `IsValid` changes the alliance, so it isn't called).

No pass of their own: the hooks run when the mods act, and the views when asked. Through the facade the hooks also hand
the region a town begun or built within reach (its site, and the founder named in `world:built`), and a town taken
starts its fortune again ("The region").

### Medical triage with Move the Patient

The player's request (6 November 2026, with a save): "i have a hospital bed with high criticality and set as surgery
bed. when a pawn is injured they always take one of the other two beds though ... i want them to always take the best
bed, and once a patient with a more serious injury arrives, the previous pawn should be put into another bed via
'transfer to bed'". `Integrations/MedicalTriage.cs`, its pure rules in `MedicalTriageMath.cs` (Tools/Tests,
`MedicalTriageTests`). Move the Patient (`konstantynopolitaneczka.movethepatient`, assembly `PatientBedTransfer`) is
read by name: its settings (`PatientBedTransferMod.settings`: the priority system and its mode), a bed's priority and a
patient's severity (`PatientTriage.GetBedPriority`, `CalculatePatientSeverity`, Low 1 to Critical 4), its surgery
beds and pending operations, xenotype rule, bed kinds and transfers under way (`BedUtility`), who can carry
(`PatientTransferUtility.CanPawnPerformTransfer`), its rerun without surgery beds (`MedicalBedSelector.Busy`) and its
job (`TransferPatientToBed`, the patient then the bed). Anything missing: one warning, and nothing is patched. The
mod's own scheme stays as it is underneath: each patient gets the lowest-priority bed at least as high as their
severity (the higher beds kept for worse cases), and a surgery bed goes to nobody else while another medical bed is
free.

- **Best bed first.** Postfixes on `RestUtility.FindBedFor` (the five-argument one) and `FindPatientBedFor`, the
  methods the mod's own postfixes adjust, ordered after them (`HarmonyAfter` its Harmony id, low priority), and only
  when the result is a medical bed for one of the colony's people (not an enemy unless a prisoner, not a baby, not a
  faith that wants slab beds). The best free bed they may use: the player's priority with the mod (Normal for all while
  its priority system is off), then the game's medical quality (`MedicalTriageMath.Quality`: the bed's tend quality
  offset, its immunity gain above 1, the room's infection chance below 1, in steps of 0.005 so the order is whole), then
  the nearest. A candidate passes the call's own `IsValidBedFor` (forbidden, owner type, prisoner beds and cells,
  reachable, reservable), the mod's xenotype rule, and is no more dangerous than the bed chosen. A surgery bed counts
  for an ordinary patient only while nobody waiting for an operation could use it; one waiting for theirs keeps the
  surgery bed the mod gave them. Someone already in a medical bed that counts keeps it (no getting up for a better
  one), and a bed being freed for someone is theirs alone. Inside the mod's rerun without surgery beds it stands aside.
- **Making room.** A pass (`GameComponent_MedicalTriage`, at tick 53 of the 250-tick cycle, `Sched.RareTriage`): on the
  hour every home map; every 250 ticks only a map where a patient went to a lesser bed than the best of their kind in
  the last hour (the bed choice notes it) and a move under way. The patients: those in the colony's medical beds and
  those lying downed on the floor, the worst first by the mod's severity, one waiting for an operation first among
  equals. For each, the beds ranked for them (a surgery bed first for an operation): when one better than what they
  have (or the best free bed they would be carried to) holds someone they outrank (`Outranks`: more serious, or an
  operation for the surgery bed over an ordinary patient; nobody is moved out of a surgery bed while waiting for their
  own operation), and that one may be moved (`MayMove`: not being operated on, tended or on the way to, in a mental
  state, in labour or carried; another free medical bed of their kind; not moved in four hours), the nearest awake
  colonist who does doctoring and isn't busy (drafted, in a ritual, tending, rescuing, firefighting, carrying, on the
  player's order) gets the mod's job to the best other free bed, as its "Transfer to bed" gives it (`playerForced`,
  marked active in the mod). One move at a time per map; the bed is kept for the one worse off until they lie in it,
  they no longer need it, or four hours pass. Once the lighter case lies in the new bed, the worse one is brought in:
  by the game's rescue (best bed first sends them there), by another transfer when they lie in a lesser bed or on the
  floor with nobody coming, or on their own feet. Each one carried by triage is held four hours.
- **Shown.** The carrier says one line as they set off (`Rimfolk_Pool_TriageRoom`, `_TriageSurgery`, by the colony's
  age); the colony news (`Tell.Minor`, Health) tells it once the lighter case lies in their new bed, in two wordings
  for each age, with the room they were carried to in the age's word when it is a hospital (the healer's hut, the
  infirmary, the ward, the sickbay); the bed kept says for whom, and the patient moved why, under their inspect text
  for six hours.
- **The player's case.** A hospital bed at High and marked for surgery, two plain beds at Normal. The mod alone sends
  every ordinary patient to the plain beds while one is free. With triage the injured take the hospital bed first; a
  newcomer worse off (open wounds over tended ones, downed and unconscious or bleeding out over the rest) has the
  lighter case carried to a plain bed if one is free; and anyone waiting for an operation has an ordinary patient
  carried out of it. Without the surgery mark the same, by severity only: an operation then claims the bed like anyone
  else, by how badly they are hurt.

The setting `medicalTriage` (Your other mods, Move the Patient, shown while it is loaded; on by default, needs the deeper hooks). The mod's "by distance"
mode stands triage down: the player chose the nearest bed there. Nothing is saved: a load forgets a move under way
(the carry itself is the game's and goes on) and who was moved lately.

### Medicine by what is treated

The player's request (7 November 2026): "can you make it so that medicine is always taken according to the injury? if
it's just scratches or bruises i don't want proper or glitterworld medicine used if a pawn also has a more serious
illness at the same time. sometimes i even want no medicine if the injury is not threatening". `Health/MedicineChoice.cs`
(the game's side, the saved book `GameComponent_MedicineChoice`), its patches in `MedicineChoicePatches.cs`, its pure
rule in `MedicineChoiceMath.cs` (Tools/Tests, `MedicineChoiceTests`), Smart Medicine read by name in
`Integrations/SmartMedicine.cs`, dev actions in `MedicineChoiceDebug.cs`. The patient's medical care setting is the
ceiling: the rule only ever chooses less (`CareFor`), and a patient with care is always tended.

**The rule** (`MedicineChoiceMath.Judge`), from the game's own numbers: a wound's untended bleeding (its size times its
kind's bleed rate, the part's and the race's factors, nothing on bone or a prosthetic, before any tending or a field
medic's stabilizing, so a wound stays what it is), its infection chance as the game rolls it (the kind's chance, by its
size from a tenth at 1 point to the whole at 12, a tenth for an animal, none on bone, a prosthetic, a permanent wound,
for someone immune or regenerating, times the storyteller's factor for the colony's own; never the hidden roll itself),
the part (a vital tag, an organ inside), the stage, the def.

| Kind | What counts | Medicine |
|---|---|---|
| Serious | the council's answer gave them the best; a stage threatening life now; any illness, infection or lasting condition but Rimfolk's everyday ailments; bleeding while the patient would bleed to death within 18 hours (the game's urgent mark); bleeding 40% a day or more (every fresh stump but a small creature's); a vital part (brain, heart, lungs, liver, kidneys, stomach, neck); an organ; deep (half the part's health, or 12 points on a wound that can go bad) | the medical care itself |
| Minor | Rimfolk's everyday ailments (not its serious illnesses, never one that can kill); a wound bleeding 10% a day or more; a wound with a 3% chance or more of turning infected | herbal at most |
| Harmless | a wound that does neither: a bruise, a crack, the smallest scratch or crush | none, only bandaging (the setting: herbal at most) |

Examples (the tests): a bruise, harmless; the smallest scratch (1 point, 6% a day, 1.5%), harmless; a small cut (2 on
an arm, 12% a day), minor; a scratch that bleeds (4 on a hand), minor; a small burn (3, 7.9%), minor; a crush (5, 6.4%),
minor; a gunshot in the torso (10, 60% a day), serious; a small cut on the neck (48% a day), serious; a cut on a lung,
serious; a bruise on the brain, serious; a crack through most of the femur, serious; a large burn (14), serious; a lost
arm's or finger's stump, serious; an infection, the flu, the plague, asthma, a carcinoma, serious; Rimfolk's head cold,
minor, its tuberculosis, serious. Vanilla's scratches all bleed and can get infected, so they are minor (herbal at
most), and its crushes are minor too once past the smallest.

**Without Smart Medicine**, Rimfolk does it itself:

- **The order.** A postfix on `Hediff.TendPriority` shifts the game's priority by kind (serious 0, minor -10, harmless
  -20): the serious are tended first, the game's own order (bleeding first) within a kind. The game tends one condition
  at a time, but with medicine a wound takes other wounds along up to 20 points (`GetOptimalHediffsToTendWithSingleTreatment`):
  a tending's care is the most any of its conditions gets, so a scratch beside a gunshot rides along at no cost.
- **The medicine fetched.** A postfix on `HealthAIUtility.FindBestMedicine`: when its pick is better than the first
  tending gets, the game's own search runs again (the doctor's pockets, the stores, the pack animals) with the patient's
  care lowered to that tending's for the length of the call, and put back; none when it gets no medicine. An illness
  whose last tending runs out within the hour (`SoonTicks`) counts as well, so a job made for a bruise brings the
  plague's medicine should the plague come due on the way (the bruise is then bandaged without it). A postfix on
  `Medicine.GetMedicineCountToFullyHeal`: only the tendings that get the same care as the first (`SameMedicineCount`),
  so no glitterworld medicine is carried for scratches; one at least while the game counts any (a job made before the
  patient changed still reserves what it holds; the search itself finds none for a tending that gets none). The care is
  remembered for the doctor and patient.
- **At the tending.** A prefix on `TendUtility.DoTend`: a tending whose care doesn't allow the medicine in hand is done
  without it (kept for the next). A postfix: the job ends after this tending, by the game's own `endAfterTendedOnce`,
  when the next tending gets other medicine (`EndAfter`: better than it gets, or better than it was fetched for, as
  when the plague comes due while the scratches are seen to; with none in hand, any it wasn't made for), so the next
  job fetches the right medicine. Never a job the player ordered or a drafted doctor's (those only ever use less).
- **Caravans.** A postfix on `CaravanInventoryUtility.TryGetBestMedicine`: the packs' best medicine the first tending
  allows, or none.

**With Smart Medicine** (`memegoddess.smartmedicine`, Uuugggg's mod continued; the original reads the same), which keeps
a choice of medical care for each condition (`PriorityCareSettingsComp.hediffCare`, a dictionary from a hediff to a
`MedicalCareCategory`, saved in the continued version, `PriorityCareComp`'s and unsaved in the original), set with a
right click on the health tab, and does the treating by it: a condition with a lower choice sorts after the rest (its
tend priority five a step from the patient's care), its search for medicine (its own `FindBestMedicine.Find`, which
replaces the game's and is called straight from its `WorkGiver_Tend.JobOnThing` prefix) takes the highest choice of the
first tending, its count (a transpiler on `GetMedicineCountToFullyHeal`) only the tendings at the highest choice, and the
next tending looks again. Rimfolk's own patches above stand down, and Rimfolk fills that choice in where the player set
none: the rule's care where it is less than the patient's (a serious condition keeps the default), right before each
choice of medicine (prefixes on `WorkGiver_Tend.JobOnThing` and `FindBestMedicine` before Smart Medicine's own, by its
Harmony id, and on `DoTend`) and when the health tab is drawn (Smart Medicine's own icon then shows it). What Rimfolk
wrote is kept in the save by the condition's `loadID` with the care: a choice Rimfolk never wrote, or one changed by
hand, is the player's and never touched; one taken back to the default stays the default (`handsOff`). Rimfolk's own
follow the wound and the care setting and are taken out with the setting off. Smart Medicine's short-lived raise of a
patient's care (`DefaultCareFix`, when a choice set by hand is above it) is read through. After a tending the same
job end applies (with Smart Medicine tending with what is in hand, also when the next gets none). Smart Medicine's own
"minimal medicine for non-urgent" settings still apply on top, as the player set them.

**Other medical mods in the player's list.** Field Medic (its stabilizing with the medic bag multiplies a wound's bleed
rate for a while: the rule reads the untended bleeding, so a stabilized wound stays what it is), VFE Medical (beds and
facilities: the tend quality, not the medicine), Nice Health Tab (the health tab redrawn: the line is in the condition's
own tooltip, `Hediff.GetTooltip`, which it shows as the game's does; with Smart Medicine its icons are Smart Medicine's
own work there), Quality Surgeon (surgery, not tending), Medic Warcasket (its mechanite dressing is a job of its own: the
tending prefix leaves any job but the game's own tend job alone): nothing to do but coexist.

**Shown.** A postfix on `Hediff.GetTooltip`: each condition still to be tended says what it gets and why ("Herbal
medicine at most: it can get infected.", "Medicine up to Ada's medical care (best quality medicine): it bleeds
heavily.", "Doctor care, but no medicine, as Ada's medical care is set: ...", "Best quality medicine, as you set it with
Smart Medicine."). No letters or messages.

**The council's answer.** "The best medicine for" the doctor who is the patient (`DilemmaEffect_MedCare`) sets their care
to the best, as before, and the book keeps their id: everything of theirs is serious while their care stays at the
best, so the answer's words hold for a bruise too; their care changed by hand lets it go.

The settings `medicineByNeed` (What's on, people's lives; on by default, needs the deeper hooks) and `harmlessMedicine`
(no medicine or herbal at most, under it). Saved: `GameComponent_MedicineChoice.bestByAnswer` (40 at most),
`filled` and `handsOff` (only with Smart Medicine; pruned to the living at each save, 600 and 200 at most; the original
Smart Medicine's forgetting at a load clears `filled`). No Sched slot: everything rides on the game's own calls.

### Ages

Every feature takes the form of the age (ROADMAP.md, "Every age, not only the medieval one"), and the ages are read in
one place: `Lean/Ages.cs`, with its pure rules in `Lean/AgesMath.cs`.

- **The ages.** Tribal, medieval, industrial or spacer, from a tech level as `LedgerMath.AgeOf` reads it (neolithic and
  below tribal, spacer and above spacer). `Ages.Of(colony)`: the colony's age, its own tech level or what it has
  researched, whichever is higher (`Ages.Ours`, from `Avail.EffectiveTech`); the player's faction has one, so every home
  has it until homes keep their own. `Ages.Of(faction)`: a people's age by its tech level. `Ages.IsEmpire(faction)`:
  a people of royal titles, the Empire, which keeps its own titles and honor. Flavor's era tags (`AddEraTags`) and the
  world ledger's people view ask here, and give exactly what they gave before (`WorldMechanicsTests` checks every tech
  level against the old table).
- **Forms.** `Ages.Form(feature, ours, theirs)` gives a feature's word and rite: the nine of ROADMAP's table (favor
  owed, pact and truce, feast and contest, marriage alliance, ward and hostage, ransom, fealty and vassals, colony
  renown, schemes; council tasks were dropped on 5 November 2026), each with a word and a rite in each of the four
  ages, as keyed strings (`CC_Form_<Feature>_<Age>` and `CC_Form_<Feature>_<Age>_Rite`, `Rimfolk_Ages.xml`), short noun
  phrases to sit in a sentence.
- **Mixed ages.** The colony's own age sets its means: the word it has for the thing and what it does it with (a debt
  of honor, an IOU; ransom by messenger or by radio; a feast in the hall or a gala). The other people's age sets the
  rite: how it is done with them by their ways (a gift owed back, oaths sworn before witnesses, an exchange at a
  crossing). `Form` takes both and says which part came from which (`AgeForm.ours` for the word, `AgeForm.theirs` for
  the rite, `AgeForm.empire` when the other people are the Empire). The region's news of a new town reads the
  eleventh feature, the founding (its word how the colony hears of it, its rite how their people found one); the diplomacy of stage 2
  will.

### The region

Stage 1's first release (docs/design/stage1-region.md, item 1 with item 2's prosperity): the towns within the region
map's reach of a home kept in detail, each with a fortune, and the new towns given faces. `World/Region/`, its pure
rules in `RegionMath.cs` (Tools/Tests: `RegionTests`).

- **The reach in detail** (`RegionTowns`). For each home, the nearest 40 towns of humanlike peoples (not hidden, not
  the player's) within the region map's reach (`regionMapRadius`, 35 tiles by default), by straight-line distance on
  the home's layer; one set for all homes, so a town within reach of two has one fortune. Worked out again at most once
  a 250-tick stretch when the world's towns, the homes or the reach changed, and every day; nothing saved. The ledger
  keeps a standing town's record only while it is in this set or a notable lives there: beyond the reach nothing is
  kept and nothing changed, and the hooks write nothing there (as `WorldMath.Records` rules).
- **Remoteness** per home: from the nearest town of a humanlike people at peace with the colony, 0 within 12 tiles,
  rising evenly to 1 at 40, 0.2 more with fewer than two such towns within 40, at most 1. Worked out with the reach,
  not saved; shown for a home on the region map. The travellers who seek out the remote read it in the next release.
- **Each home's part** (`RegionHome`, kept by `ColonyBook` in `RegionBook.homes`): when a new town's founder last wrote
  to it and when a new town last welcomed it. With one colony there is one.
- **Two passes.** `Pass.Region` once an hour at tick 107 (`Sched.Region`, on `Sched.HourlyOnCycle`: the hour's own
  ticks are all taken at 90 apart, so it has a tick of the hour's first 250-tick stretch that no other pass uses), only
  with work waiting; `Pass.DailyRegion` at tick 1373 of the day. Both timed by Rimfolk's clock; see "Ticking".
- **Fortunes** (`RegionFortunes`, setting "Towns' fortunes"). Prosperity 0 to 100 on the town's record: seeded at 50,
  up to 15 less on poor land (its biome's `settlementSelectionWeight`) and 10 less while its people are in hard times;
  a new town at 30; a town taken in an invasion again at 25. Each day: the drift back toward 50 (45 on poor land) by
  0.3, 0.3 off for a town within 20 tiles of a town of a people its own is at war with (the game's relations between
  peoples that count), and the world events' moves: `WorldSituationDef.prosperity` (32 events), in full at the event's
  town and half at its people's other towns in detail, a fifth on each of its first five days (the event keeps how many
  shares it gave, `regionDays`, so a day is never given twice), and `prosperityOther` for the second people of a war
  of words, a feud, an embargo or a war between neighbours, at half. How a world event ends can move them too, at once
  (`WorldEffect_Fortune` in an ending, `RegionFortunes.Move`: in full at the event's town, half at its people's other
  towns, all at half for the second people, the event named as the cause): a war between neighbours won or lost, a
  great work finished or left half built, a great caravan arrived or robbed, a ban on drink that fell or held,
  households on the move come through or come in worn out, a cold year eased. Bands: struggling under 20, poor, steady, thriving, flourishing from 80, in
  their age's word where it has one. The last cause is named in the town's panel for 60 days when it fits how the town
  stands. In what it sells (decision 3, `RegionStock`, setting "Fortunes in the trade stock", with the deeper hooks): a
  postfix on the game's `Settlement_TraderTracker.RegenerateStock`, last of all patches, brings a struggling town's
  food, medicine and silver to three quarters, a thriving one's silver to 1.1 and a flourishing one's to 1.2, by count
  across each good's stacks; nothing else is touched.
- **New towns' faces** (`RegionFaces`, setting "New towns' faces"). Sources: Faction Territories' settlements begun and
  finished (its hooks through `WorldMechanics.Founded` and `Built`, which now pass the site and name the founder in the
  history's `world:built`), and Rimfolk's own new towns (the new-town, other-settlers and crossroads events' `Begin`).
  A founding within reach gets, in the region's hour, a founder (their leaders' kind, an Empire's titled kind, aged 30
  to 55, two a day at most, a day's wait at most), the mother town (their nearest other town) and a reason read from
  the land (`RegionMath.WhyFounded`: a town of theirs given up in 120 days, a ruin, a flourishing mother town, a road,
  a river, the coast, the hills, else the land). When it stands: its record, two founding families (`LedgerHouse`; the
  envoy the notables make is born to the second), its founder the Head notable, and the town kept among the notables'
  towns for its first year (`NotableMath.MaxNew`, two). Told once begun and once built (`Tell.Minor`, the colony's
  means and their rite by `Ages.Form(AgeFeature.Founding)`): within reach Faction Territories' own message is held
  while its method runs (`TerritoryNews`) and dropped once the region took the town (`RegionFaces.Claims`), else given
  back, so there is one message and never none. A town within 15 tiles of a home gets the full welcome (decision 1,
  `Rimfolk_WS_NewTownMet`, `SituationWorker_NewTownMet`: the new-town event's runner at the gate, gift and timber,
  traders and visitors on the town that stands) at most once a year per home when a world event's slot is free
  (`WorldSituations.SlotFree`), whose letter is then the town's news; else the founder's letter (`RegionWords`,
  one a season per home). Rimfolk's own new towns are told and welcomed by their own event.
- **Words and facts.** The place card, which the region map's panel became (`RegionPanel`, through `CardSections.TownLines`,
  `PlaceLines` and `HomeLines`; "Rimfolk's windows"): a town's fortune and founding, a site's settlers, a home's remoteness. The facts service's
  provider `region` (`Text/Facts/FactsRegion.cs`): `town:struggling` to `town:flourishing` with `{FORTUNE}` (and
  `{FORTUNEWHY}`, the world event its panel names as the cause),
  `town:new`, `town:founder` `{FOUNDER}`, `them:founder`, `town:mother` `{MOTHERTOWN}`, `town:why:KEY` `{WHYFOUNDED}`,
  `region:newtown` (`:theirs`, `:other`) `{NEWTOWN}` `{NEWTOWNPEOPLE}`; 40 talk pieces use them.
- **Settings off**: a part makes nothing new and tells nothing, and keeps its records (the reach is still worked out,
  which changes no play). Neither is per tick; the daily pass loops once over the world's towns per home.
- **Travelers who seek out the remote** (the second release, item 5; `TravelMath.cs` pure, `TravelLand.cs`, `FarNews.cs`,
  `Camps/CampSeekers.cs`; setting "Travelers to the remote", 0 to 200%, 100% by default; the rates also behind
  "Systems talk to each other"). Through the existing clocks, per home, never per tick: the camps' daily try at
  `CampTimer.DailyChance` × (1 + 0.6 × remoteness × the setting), so 0.05 near and 0.08 at the most remote; the rest
  after a camp 15 days × (1 − 0.3 × remoteness), never under 40%; Rimfolk's visitor events weighed 1 + 0.6 ×
  remoteness, and on Rimfolk's own timer the home's next wait cut by 1 / (1 + 0.6 × remoteness × the visitors' share of
  what could come), so the visitors come 60% more often and every other event as often as before (drawn by the
  storyteller, the weight alone: their share of its pool grows). The land's reasons, read once a day a home at most
  when a camp could come (`TravelLand.Read`, not saved): a relic of the colony's faith on the map (Ideology), the tile's
  landmark, a Gauranlen or anima tree, ancient ruins on the land (where salvagers would strip), an abandoned town within
  the reach, six books or more on the shelves, a history (a year and five milestones), ore in sight (six cells of a rich
  ore, or forty of steel), a rush running, grass in its growing season, a road, far from every market (rather remote or
  more). A kind's `seeks` lists the reasons it comes for, strongest first: with one on the land it weighs 1 + 2 ×
  remoteness (else its base weight, or not at all with `seekOnly`), picks one (`TravelMath.Pick`), says it in its letter
  (`seekTexts` tagged `[seek:REASON]`, else `CC_Travel_Came_REASON`) and looks at it, pitches by the ruins or the ore,
  walks out to them on its first working day, or asks leave by letter on its first day for what lies in the colony (an
  ask with `needs` `seek:REASON`: the relic, the books, the tree, the colony's story). Scholars are the one new kind
  (`Rimfolk_Camp_Scholars`, by their people's age, `[their:AGE]` on every camp's lines now); the nomads are the drovers
  (grazing), the salt caravan and the far merchants (a road, far from every market). Prospectors who came for the ore
  come to blows over a claim at most once a stay, 12% a stay (the game's social fight, between two of their own). News of
  far towns (`FarNews`): the nearest twelve towns beyond every home's reach, within three times it; a world event about
  their people, a war begun or ended in the last 60 days (`OutsiderBook.wars`), the town new, a site within 10 tiles, a
  quest of theirs, the weightiest not told in the last ten days; told by one of a seeking camp to a colonist at their fire
  (`Together.Can`), two pieces a stay at most, into the town's Heard lately (its forty), the history (`world:heard`, at
  the colony, the town and its people named) and the colony news. The region map's panel for a home says how much more
  often travelers come and what draws them, and lists the latest word from far towns.
- **Titles and rites in invitations** (item 7; `Missions/InviteForms.cs`, `InviteMath.cs` pure). The invitation to a
  feast, midwinter or a wedding comes from the town's head as the ledger knows them (`WorldLedger.Town(id).head`), an
  Empire head by the royal title of the ledger's person view (`WorldMechanics.Title`), and gives the feast; without a
  known head, someone of theirs made from one of the Empire's titled kinds carries the title the game gives them. A
  colonist holding a title of the inviting people is named first among the guests. The rite: a ritual their ideoligion
  really has (Ideology, read from its rituals' names: a feast, a wedding, a funeral; `{RITE}`, tags `[rite]`), else how
  their age keeps the day (`Ages.Form`, `[agerite]`). The funeral and wedding asks (the feast mission) name their rite the
  same way. Saved on the mission as words (`host`, `rite`, `guest`); `{HOST}`, `{RITE}` and `{GUEST}` fill its lines.
- **Raiders' camps** (the fifth release, item 4, decisions 7 and 12; `World/Region/RaidersCampMath.cs` pure,
  `RaidersCamp.cs`, `RaidersCampTerms.cs`, `RaidersCampPage.cs`, `RaidersCampDebug.cs`; setting "Raiders' camps", 0 to
  200%, 100% by default; `RaidersCampTests`). Each home's camp is a `RaidersCamp` on its `RegionHome` (`raiders`, a
  `RaidersCampHome` with the last end and the word of the last people paid off), run in the region's two passes: no
  pass, slot or tick of its own. **Set up**: a look every 10 days per home from day 60; a home of 1,000 threat points or
  more (`StorytellerUtility.DefaultThreatPointsNow`), no camp and 60 days since the last ended, then 10% × the rate,
  then a people that can raid and hold a camp (`Raids.CanRaid`, a settlement pawn group, `BanditCamp.FactionCanOwn`)
  with a town within 60 tiles, Faction Territories territory within 30 (`WorldMechanics.TerritoryOwner` over a flood
  fill, only when needed) or a grudge of 20 (memory's wrongs + 15 a held dead + 20 a vendetta), never while a world
  event about them runs or their word holds; the biggest grudge first, then the nearest. The tile: a flood fill to 10
  tiles over passable land, more than 5 tiles from every home, a base could stand there with nothing on it, 3 tiles
  from friendly towns, weighed toward hills (×2) and forest (×1.5). Points: `DefaultSiteThreatPointsNow`. **Hidden**
  until found: by day, the first of a hunter, someone past the home area or (from day 3) anyone out of doors sees it,
  30% a day and certain from the third (`FindChance` an hour), says one line, and a threat letter names the danger, who
  leads it (`leader`, `leaderWhy`: the hook for the warlord's son), a grudge's wrong or a broken word. Only then the
  site: `SiteMaker.MakeSite(SitePartDefOf.BanditCamp, tile, people, true, points)`, `customLabel` by their age
  (`CC_RaidCamp_Kind_<Age>`). Faction Territories counts no territory for it. **Raids**: an hourly roll on
  `QuestTuning.PointsToRaidSourceRaidsMTBDaysCurve` at its points, the mean divided by the rate, no sooner than two days
  after set up and a day after found, 1.5 days apart, never on another threat or within a day of a big one, at 0.6 of
  the home's threat points through `Raids.Send` (new optional `from`, an edge cell on the camp's side by
  `WorldGrid.GetRotFromTo` kept as `IncidentParms.spawnCenter`, and `tellStoryteller` false: on top of the
  storyteller's, as the game's own raid-source camps); its raiders watched hourly (not saved: found again on the map
  after a load) until none stands; beaten when half fell or they fled (`LordToil_PanicFlee`). **Attacked**: two
  look-only patches (`Site.PostMapGenerate`, `Site.Notify_MyMapAboutToBeRemoved`) and the hour's look count its
  fighters and any threat left; when the game removes the site (`Site.ShouldRemoveMapNow` removes a bandit camp's
  either way) it is broken unless a quarter or more still stood with a threat left, else put back on its tile with its
  points times the share standing and half the loot. **Ends**: broken; paid (terms a day after found through
  `Tell.Ask` when `Tell.CanAsk`, `ChoiceLetter_RaidersCamp`, price half a raid's points in tens, at least 100, also from
  the page; the silver carried to the gate by `DilemmaEffect_RaidersCampPrice`, a `DilemmaEffect_CarryOut` with no
  courier, raids paused; a ledger Promise tie for a year, kept or broken, one in ten break it); peace (the game removes
  such sites itself with its letter, so Rimfolk's is a news line); burned (an ally at war with them with a town within
  30 tiles, 10% every 5 days); given up (two raids beaten or 60 days). Told (`Tell.Letter`, threat letters for found and
  put back), history `world:camp:found/raid/back/broken/paid/peace/burned/gaveUp` (milestones but the raid and put
  back), memory deeds `camped` (their wrong), `campBroken` and `campFailed` (the colony's wrongs to them), `paidOff`
  (a favor), `campStruck` and `burnedCamp` (good turns the colony owes; the ally's for a burning). A home lost takes its
  camp (`ColonyBook.Lost`). Shown: the region map's Threats lens (its marks, its line to the home it raids, its List with
  Pay; its old page name `RaidersCamps` forwards there), the place card's sections for its site and its home, This week's missions card.
- **Towns that rise and fall** (the third release, item 2 with decisions 2, 5 and 14; `World/Region/RiseFall.cs`,
  `NewTowns.cs`, `RiseFallLedger.cs`, `RiseFallSituation.cs`, `RiseFallWords.cs`, `RiseFallEffects.cs`,
  `RiseFallPatches.cs`; rules in `RiseFallMath.cs`, Tools/Tests `RiseFallTests`; setting "Towns rise and fall"). Run on
  the region's two passes, no new pass. The pace per home's reach (`RegionHome.givenUpAt`, `resettledAt`, `foundedAt`;
  a change needs every home whose reach holds the place past its gap): one town given up in 60 days, one ruin settled
  in 120, one town founded by Rimfolk in 90; Rimfolk's world events that give a town up (`Council_WS_Abandoned`, whose
  candidates now also keep two other towns and wait for the pace) or found one (the new-town event's `Begin`) count on
  the same clocks. **Given up**: a town in detail under 15 for 30 days (`LedgerTown.lowSince`), or under 25 with two
  bad events of its people's in 60 days, or a new town under 10 in its first 15 days; not its people's seat
  (`NotableBook.SeatOf`), not the Empire's, its people keeping two other towns, `SituationWorker_Abandoned.Safe`; 7%
  on its own day every 5 days. With a world event's slot free at the home nearest it, `Rimfolk_WS_GivenUp`
  (`SituationWorker_GivenUp`, the abandoned event's beats on that town, its letter by what struck it, read from its
  people's latest bad event); else quietly (`LedgerTown.closing`): a line in the news, a refugee family's camp of
  theirs a third of the time within 25 tiles, the ruin on the fifth day, or a few families holding on. Its notables the
  colony dealt with become acquaintances in their people's nearest town. **Ruins** (`LedgerRuin` on the ledger: the
  town it was, the game's `AbandonedSettlement` in their colour, since when and why): made by a giving up, adopted
  from the abandoned event's ending (a town in detail gone from the map with an abandoned settlement on its tile and
  the event's record), or left by a fight: the game's defeat of a town (`Patch_RiseFallDefeated`, a note only) or a
  town in detail gone with the game's destroyed settlement on its tile; such a ruin waits (30 days at most, six at
  once) until only a caravan or nothing stands there, so a vassal or the colony's own settlement on the tile wins.
  Gone after 300 days, twelve per home's reach (the oldest first), each only when safe (no map, quest, caravan or
  pods); one another took off the map is let go. Only the vanilla def is adopted, so Real Ruins' sites are left alone.
  **Settled again**: a ruin 120 days old, 10% every 15 days, by the ruin's own people with a town in detail within 25
  tiles at 50 or more, else the people at peace doing best there at 60 or more; its tile still valid, clear of homes
  and of another people's territory; the old name kept (newcomers' own half the time), history `world:resettled`.
  **Founded by Rimfolk** (decision 2, beside Faction Territories' builders): a town flourishing (80) for 60 days
  (`LedgerTown.highSince`), 15% every 15 days, a tile 6 to 15 tiles from it found by `TileFinder`'s flood fill with
  straight-line checks only (valid for a town and empty, within reach, more than 6.5 tiles from every one of the
  colony's settlements by the game's `SettlementProximityGoodwillUtility.MaxDist`, not in another people's territory,
  not beside a town being built), weighed by the biome's settlement weight, only while fewer than 10 towns stand in
  every reach it lies in. Both kinds of new town go through `RegionFaces.Founded` (a founder in the region's hour,
  families, the town their settlers came from, why: `mother` or `ruin`) and its own line in the news. History kinds
  `world:abandoned`, `world:ruined` (a milestone when the colony did it), `world:ruingone`, `world:resettled`; the
  new towns' `world:built` as the region writes it. The words (`RiseFallWords`): the colony's means
  (`Ages.Form(AgeFeature.Founding)`), a ruin by its people's age, the settlers by theirs (a clan, families, a company,
  a charter), the region map's panel for a ruin and for a town being given up, a line in the ruin's own inspect pane
  (`Patch_RiseFallRuinInspect`). **Prosperity's effects** (`RiseFallEffects`, with the fortunes and "Systems talk to
  each other"): provisions and rebuilding asks twice as likely while a town of a people on good terms struggles in the
  home's reach, asked in that town's name (`HappeningWorker_MissionDelivery`); sheltering refugees twice as likely when
  the people they flee have a town struggling there (and a town given up is a hardship they flee); the refugee
  family's camp twice as likely, six times as likely to be of that people (`CampTracker`); trader caravans' people
  weighed 1.25 thriving or flourishing, 0.6 struggling, by their town nearest the home, when the game sends one without
  a people (`Patch_RiseFallCaravans`, with the deeper hooks).
- **Trading posts** (the fourth release, item 3 with decision 6; `World/Region/TradingPostMath.cs` pure, `TradingPost.cs`,
  `TradingPostWords.cs`, `TradingPostPage.cs`, `TradingPostFacts.cs`, `TradingPostNotables.cs`, `TradingPostPatches.cs`,
  `TradingPostDebug.cs`; setting "Trading posts"; `TradingPostTests`). Each home's posts are a `TradingPostHome` on its
  `RegionHome` (`posts`: the posts, two at most, each a `TradingPost` with its town, people, name, tile, opened, sour
  since, warned, a closing waiting and the last sack; when the home was last looked at and last saw a post open or
  close), run in the region's day only: no pass, slot or tick of its own. The town's record on the ledger is of kind
  `Post` (`TownKind.Post`), so the fortunes cap it at `RegionMath.PostCap` (70); a record of that kind no home's post
  holds is a town again. **Who**: a people that can open one (`CanOpen`: humanlike, not hidden or temporary, a naming
  rule for towns and `baseTraderKinds`; the Empire too) at peace, goodwill 40 or allied, three trades or more in 120
  days (`History.Count` of `deed:traded`, the memory's trades), its nearest town 15 tiles or more from the home, no post
  of theirs there yet. **When**: a look every 15 days per home, two posts at most, 60 days since the last opening or
  closing there, then 20%; the people that trades most, then the best goodwill (the next when no place fits). **Where**:
  a flood fill to 12 tiles over passable land with straight-line distances only, 6 tiles or more from the home, more
  than the game's goodwill reach from every one of the colony's settlements (`RiseFallMath.ClearOfHomes`), 3 tiles
  from other towns, valid for a town and empty (`TileFinder.IsValidTileForNewSettlement`), its land's settlement weight
  above 0, not beside a building site nor in another people's territory; the tiles toward their nearest town (within
  60 degrees of its heading, `TradingPostMath.AngleAt` on the tile centres' tangent plane) when any, by the land's
  weight, ×3 on a road, ×2 on a river. **What**: the game's `Settlement` of theirs named by their naming rule and their
  age (`CC_Post_Name_<Age>`: Trading Camp, Market, Depot, Ground Station), prosperity 40 with the cause "newly opened";
  its keeper (a trader kind, an Empire's titled kind) and trader made notables at once (`NotableBook.StaffPost`, a
  partial of the notables' book), the keeper remembering opening it (so it counts as a town the colony dealt with) and
  titled by their age (`TradingPosts.KeeperTitle` in `NotableBook.TitleOf`: trader, factor, manager, quartermaster);
  `Tell.Letter` looking at it, with the colony's means (`Ages.Form(AgeFeature.Post)`: its word by the colony's age, its
  rite the post's kind by theirs), transport pods named only when researched; history `world:post:opened`, a milestone.
  Their couriers are chosen by it and letters to its keeper and trader travel to it, as their people's nearest town,
  with no change of the couriers' or the letters' own. **While it stands** (with "Systems talk to each
  other"): their trader caravans weighed 1.5 in `Patch_RiseFallCaravans` (with the deeper hooks), the bandit road ×2
  where world events are weighed (`WorldSituations.TryStart`). **Souring**: since when goodwill has been under 0;
  ten days: the keeper's warning (`Tell.Letter`, `world:post:warned`, This week's line); back at 0: mended (`Tell.Minor`,
  `world:post:mended`); ten days after the warning: closed. War with the colony: closed within a day. A closing goes
  through `RiseFall.MakeRuin` when `SafeToClose` (`SituationWorker_Abandoned.Safe`, or for the Empire the same checks
  but its royal one), else waits (`closing`). **Sacked**: with the fortunes on, a people at war with theirs (`Facts.Counts`)
  with a town within 20 tiles, 5% every 15 days on the post's own day: −30, or given up when that leaves it under 10
  (a refugee family's camp at the gate). **Razed** by the colony (`Patch_TradingPostRazed` on the game's defeat of a
  town): the memory's `postRazed` (a wrong of 30), `world:post:razed`, a news line. Taken, made a vassal or given up
  another way: its record ends quietly. History kinds of the family `world:post` (opened, warned, mended, closed,
  sacked, razed). Facts (the region's provider): `town:post` ({TOWN}, `{POSTKIND}`, `{KEEPER}`), `town:post:warned`,
  `their:post` and `region:post` (`{POST}`, `{POSTPEOPLE}`); 14 talk pieces. Shown: the region map's Trade lens
  (its marks, its line to its town, its List; the old page name `TradingPosts` forwards there), the place card's sections for a post, This week while warned. Dev actions open, warn, sack and close one.
- **Settlers' hamlets** (the sixth release, item 6 with decisions 9 to 11; `World/Region/HamletMath.cs` pure, `Hamlet.cs`,
  `HamletPlace.cs`, `HamletAsk.cs`, `HamletEscort.cs`, `HamletFound.cs`, `HamletLife.cs`, `HamletWords.cs`, `HamletPage.cs`,
  `HamletFacts.cs`, `HamletNotables.cs`, `HamletPatches.cs`, `HamletDebug.cs`; setting "Hamlets"; `HamletTests`). Each home's
  hamlet is a `HamletHome` on its `RegionHome` (`hamlet`: the stage, the way in, the settlers kept with `KeepFor.Hamlet`
  (8 at most, emptied once they settle), the place, the town, the Fealty tie's id, its days at 40, the refusals, when it
  last asked, harvested and swore), run in the region's day (looks, life) and hour (only while settlers wait or travel):
  no pass, slot or tick of its own. **Who**: from day 90, five grown colonists, no hamlet under way at the home and a
  year since the last ask, a question may come (`Tell.CanAsk`), a look every 10 days, then 15%: the colony's holding when
  it has lived 60 days on its land with unease under 30, else the poorest town in reach under 40 of a people that
  `CanFound` (humanlike, at peace, people to send, a naming rule); the town-abandoned event's refugees by its answer
  "Offer them a place for a hamlet" (`WorldEffect_Hamlet`, greyed with the reason when no ask could come; the family at
  the gate goes back for the others and is among the settlers). **Where**: the posts' flood fill to 10 tiles, 6 or more
  from the home, x2 by a river, x1.5 on a road; "choose another place" opens the world targeter, held to the same rules
  (`WhyNotHere`). **The ask** (`ChoiceLetter_Hamlet`, `Rimfolk_HamletAsk`): escort (two grown colonists at home), alone,
  another place, refuse; unanswered 10 days, they look elsewhere. **The escort**: the settlers' own camp by the colony
  (`Rimfolk_Camp_Settlers`, weight 0, built with `CampTracker.Make` from the settlers themselves; the holding stays as
  their camp; another camp there: they wait on the road off the map), up to five days; a player caravan within three
  tiles of the home, bound for the place, with two grown colonists takes them along: the camp packed (`Camp.Leave`
  with ending `hamlet`, told by the hamlet's own words), the settlers despawned and held in the world beside it,
  `Patch_HamletCaravanLine` names them in its inspect pane. Arrived (the caravan on the tile, or its people on a map
  there): one roll of `CaravanDemand` or `Ambush` (15%, 35% with a raiders' camp at the home or a hostile town within 15
  tiles of the place); `Patch_HamletFight` (on `CaravanIncidentUtility.SetupCaravanAttackMap`) spawns the grown settlers
  near the caravan with `LordJob_AssistColony` when the attackers are hostile to their people too. After eight hours there
  and the morning, or a day, with no map on the tile, the hamlet stands. The caravan home or bound elsewhere first: they
  go alone. **Alone**: a third turned back (told when they would have arrived), the rest found it after a caravan's
  pace and a day. **The hamlet**: the game's `Settlement` of their people named by their naming rule (decision 9); the
  ledger's record of kind `Hamlet`, founded now, its founder, mother town and reason, prosperity 25 (12 alone); the
  founder's line and a second founding family from the settlers' names; the founder its head (`NotableBook.StaffHamlet`
  over `MakeHead`, remembering the colony); a Fealty tie hamlet to colony, key `protection`; history
  `world:hamlet:founded`, a milestone; the memory's `hamlet` deed; `ColonistMemory.WalkedSettlers` for the caravan's
  colonists (release 7). **Its life**: at harvest (autumn at the home, Septober where the seasons never turn), 45 days
  apart, not while struggling, a carter brings 20 plus 0.6 of its fortune in raw food (`Couriers.Deliver`, else left at
  the gate), +3 goodwill, `hamletHarvest`; struggling, an ask every five days at most for food (30 raw food), a doctor
  (5 medicine, carried by the best doctor) or tools by the colony's age (40 wood, 25 steel, 4 components), carried out to
  their carter at the gate (`DilemmaEffect_CarryOut`): +6 to its fortune and `hamletHelped`, or refused
  (`hamletRefused`); two refusals within 15 days break the tie. **Fealty**: 60 days at 40 or more, no refusal in 60
  days, a question (again 30 days after "not yet"): with Faction Territories and its vassalage on
  (`IsVassalageEnabled`), its `VassaliseUtility.ExecuteVassalisation` by name, no goodwill price, the tie ended (the
  adapter's vassal view takes over); else stage Sworn, the tie's key `oath`. **War**: its people hostile before that, a
  question: its families come (two to four of its family names walk in and join; the hamlet given up through
  `RiseFall.MakeRuin` when `SafeToClose`), or stay (the tie broken); unanswered ten days, they stay. **Second year**: 120
  days after its founding, `GrownTowns.Note(home, town, founder, first-year flags fed/gift/refused, its word, "hamlet")`
  (release 7); once grown, the ledger keeps it as a town and the hamlet stays the colony's. Ages: `AgeFeature.Hamlet`
  (the protection by the colony's age, the hamlet's word by theirs) and `AgeFeature.Oath` (where the colony takes the
  oath, the oath by theirs). Facts: `town:hamlet` (`{HAMLETWORD}`), `town:hamlet:sworn`, `their:hamlet` and
  `region:hamlet` (`{HAMLET}`, `{HAMLETPEOPLE}`); 14 talk pieces. Shown: the region map's Ours lens (the old page name `Hamlets` forwards there), the place card's
  sections for a hamlet, This week while settlers wait. Dev actions: settlers ask, found it now, a hard season's ask,
  fealty, the harvest, the war's question.
- **Small towns look small** (decision 4; `World/Region/SmallTownsMath.cs` pure, `SmallTowns.cs`; setting "Small towns'
  maps", with the deeper hooks). `Patch_SmallTownMap` prefixes `SymbolStack.Push(string, ResolveParams, string)` for the
  game's `settlement` and the Vanilla Expanded Framework's `kcsg_settlement` (Vanilla Base Generation Expanded's
  layouts, which build inside the ground they are handed and scale defenders by their own multiplier): on a map whose
  parent is the game's own `Settlement` of another people with a ledger record of a hamlet, a post or a town in its
  first year, the ground is shrunk around its middle (0.6, 0.75, 0.8 a side, never under 18 cells), the defenders'
  points (0.35, 0.5, 0.65 of the game's 1,150 to 1,600) and, for the game's base, the stockpile's worth (0.4, 0.75, 0.6)
  and a hamlet's edge defense. An Empire town or a base that needs a gravcore room keeps its ground. Sites (Real Ruins',
  bandit camps), Faction Territories' and Vanilla Outposts Expanded's world objects and Factional War's battles are
  never a `Settlement` of the vanilla def, so never touched.
- **The follow-ups and the memory of single colonists** (the seventh release, item 7, decisions 12 and 13;
  `World/Region/FollowUps.cs`, `WarlordSon.cs`, `GrownTown.cs`, `RaidersCampWarnings.cs`, `World/ColonistMemory.cs`;
  rules in `World/Region/FollowUpsMath.cs` and `World/ColonistMemoryMath.cs`, Tools/Tests `FollowUpTests`).
  **Follow-ups**: noted when what they follow ends (one-line hooks: `WarlordWorker.Ended` calls
  `WarlordSons.Remember`, and `Watch` notes his weapon's thing id while he is at a home; the other settlers'
  `Ended` calls `GrownTowns.Remember`; a hamlet's second year will call `GrownTowns.Note`), kept on the home's
  `RegionHome.followUps` (a `FollowUpsHome`, six waiting at most) with what they follow: its people and second people,
  how it ended, its own person (the warlord, the steading's leader: the pawn while the game keeps it, kept in the world
  by the ledger's reason `followup`, and the name, last name, sex and age for when it doesn't), the colonist it
  remembers, a thing (the weapon, the town) and its flags. Due 60 to 180 days after (`FollowUpsMath.DueTick`), started
  in the region's day when a world event's slot is free there (`WorldSituations.SlotFree`), one a day; let go 60 days
  past the window. They never start by themselves (a static `pending`, as the welcome and the giving up); dev actions
  in `FollowUpsDebug.cs` force each. **The
  warlord's son** (`Rimfolk_WS_Son*`, kind `son`, `SituationWorker_Son`): his branch by the ending
  (`FollowUpsMath.Branch`), a band only while his people are hostile and can hold a camp near the home, else the gate;
  he rides with his people when they are at peace with the colony, else with the people at peace nearest the home
  (`WarlordSons.Banner`, never the hunters). Made of his father's people (a fighter for a band or riders), 16 to 30 and
  17 years younger than his parent, with the family's last name, and his parent's child by `PawnRelationDefOf.Parent`
  while the game has the parent ({OTHER}), else named by {DEAD}. Band: `RaidersCamps.SetUp(home, people, son, "son")`
  as Cast's last step; ends `slain` (by whose hand), `taken`, or quietly with the camp. The camp's leader is now kept
  each hour (`KeepLeader`, and the ledger's daily check knows `raidcamp`, which it had dropped before), put on the
  camp's map behind its walls when the colony attacks (`LeaderOnMap` in `MapMade`), and leads its raids as before.
  Gate: one beat `ask` by `FollowUpsMath.Ask` (father in the cells: `DilemmaEffect_KinFree`, the callers' own walk of a
  captive out to their kin; father one of the colony: a visit at the gate; grave: a visit to `HappeningContext.place`;
  weapon: `DilemmaEffect_CarryOut` of `ctx.thing`; else a peace, a ledger Promise from the son, `warlordson`, two
  years), switched to a peace when what he came for is gone. Exile: shelter (`WorldEffect_Join`, the hunters' goodwill
  -8, and while they are at war with the colony 30% a raid of theirs two to five days on), rest, or the road. Riders:
  join, a meal, or the road. A story whose question never came ends quietly. **The town that grew**
  (`Rimfolk_WS_GrownTown`, `SituationWorker_GrownTown`): on the steading's own `Settlement` (the other settlers' event
  made it by the new towns' rules), the ledger's record kind Town, prosperity 15 up (90 at most), its leader its head
  (`NotableBook.MakeHead`, never a seat), history `world:grown` (a milestone); beats `name` (the colony's name by their
  age, the remembered colonist's, or its own; `NotableBook.TownRenamed`), `market` (the carter buys a load at 1.3 at
  the gate, `WorldEffect_Sell` toVisitor) and a market day's crowd; lines read their first year as `first:FLAG`.
  **Found another way** (`RaidersCampWarnings.cs`): from day two hidden, a visitor of a people at peace on the map
  (35% a day) or a town at peace within 15 tiles (12%, with neighbours' letters on), by the rate. **Colonist memory**:
  one ledger Regard tie per people and colonist (origin `colonist`, the marks packed in its key, up to four), six
  colonists a people and 48 in all, the faintest ending first; marks fade by `MemoryMath.Strength`; an index by people
  and person rebuilt when the ledger changes. Marks of its own: `campBroken` (everyone noted on the camp's map, for
  each people at peace with a town within 20 tiles, which also remember the colony's `brokeCamp`), `foughtBeside` (an
  enemy killed with their fighters near and fighting), `tradedPost` and `walkedSettlers` (hooks for the posts and
  hamlets), `killedRaid` 2 (10 a raid at most) / `killedField` 6 / `killedPeace` 15 (the death's instigator, `Patch_ColonistMemoryDied`),
  `executed` 25, `harvested` 20 (`Patch_ColonistMemoryHarvest` on `ThoughtUtility.GiveThoughtsForPawnOrganHarvested`);
  every deed `KinMemory.Name` gives a colonist goes in too, so one way names colonists. Read by `KinMemory.Friend` (the
  warmer of a deed's name and this), `KinMemory.AddTags` (`their:foe`, `their:foe:KIND`, `their:foeA`), the facts
  (`FOE`, `you:foe`, `you:remembered`; seven talk pieces), a notable's `OpinionOf` (the good as a deed, the wrongs
  1.5 a point off, 40 at most) and `Known`, and the trade: at -12 or less `FactionUtility.CanTradeWith` says no with the
  reason (`Patch_ColonistMemoryTrade`), so the option is greyed out and a caravan's negotiator is someone else. No raid
  of its own.

### Rimfolk's peoples

New peoples of peaceful callings the world lacked (the player's request, 7 November 2026; `docs/design/peoples.md`
has the whole design, the softness and the templates for release B). Release A: three peoples a new world is made with;
release B, peoples that arise during play, builds on them. `Defs/PeopleDefs/`, `Source/ColonyCouncil/Peoples/`, the
pure rules in `PeoplesMath.cs` (`Tools/Tests/PeoplesTests.cs`, "peoples: ").

- **The peoples.** The road league (`Rimfolk_League`, industrial, outlander parent, calling `traders`), the hospitaller
  order (`Rimfolk_Order`, medieval, tribal parent, `healers`), the herding folk (`Rimfolk_Herders`, neolithic, tribal
  parent, `herders`). Each a FactionDef on the game's abstract parent with its own pawn kinds (gear by the game's tags,
  which the VE sets add to, a few required pieces, all dyed its colour), traders, names (its own rule packs), a fixed
  faith (Ideology: the game's memes, VE Memes' where loaded, a style), a xenotype share (Biotech, Alpha Genes),
  `colorSpectrum`, and an emblem and town icon drawn in code (`Tools/make_emblems.py`, preview
  `Tools/emblems_preview.png`). Listed once among a new world's peoples at the game's usual chances
  (`startingCountAtWorldCreation` 1), never required (`requiredCountAtGameStart` 0); never natural or permanent
  enemies, no sieges, raids a quarter to a half of another people's commonality, and only once the colony has turned
  them hostile. Few towns for the league (`settlementGenerationWeight` 0.4).
- **The template** (`PeopleExtension` on the FactionDef): the calling, the town's word, the notables' titles (head and a
  woman's, heir and a woman's, envoy, trader), the messengers' word, the caravan weight, the camp kinds it favours, the
  town plan, the town's goods and herd. `Peoples` reads it: `Of`, `CallingOf`, `Title`, `Courier`, `CaravanFactor`,
  `CampFactor`, `Preference`, `Fits`, `CanSend`, `TendsLodgers`, `CaravanCanCome`, `Make` (a people made now, the game's
  way, its town near a home and clear of the game's nearness rule: the dev tools). Nothing saved, no clock of its own
  (release B's book aside, below).
- **Words.** Notables titled by their ways (`NotableBook.TitleOf`, `RegionFaces.TitleOf`: waymaster, prior or prioress,
  camp elder; caravan master, cellarer, drover). Their messengers by their own word (`CourierWords.Noun`: road riders,
  postulants, riders). The tag `their:traders`, `their:healers` or `their:herders` (`PeoplesMath.Tags`) in the facts
  (`Facts.People`), the asks' tags, the events', the camps', the bubbles', the visitors', the messengers', the letters'
  and the world events'; their letters (`Rimfolk_Pool_NeighbourWrites`), their messengers' lines and nine pieces of talk
  in their own voices.
- **How often.** The game's trader caravans weigh each people's `caravanWeight` (`Patch_RiseFallCaravans`: the
  league's 3, the herders' 1.5). A camp kind a people favours comes from them four times as often (the league's
  market folk, salt caravans, far merchants and road wardens; the order's healers, herb-gatherers, pilgrims and
  penitents; the herders' drovers, shearers and catchers). A world event's people (`SituationFactionNeed.calling`) and
  an ask (`MissionPeople.prefers`) prefer a calling five times: by patch (`Patches/Rimfolk_Peoples.xml`), the drove
  roads and the great herds the herders, six road and trade events the league, a healer lent the order, fodder the
  herders; a caller's people (`HappeningSetting.prefers`): a thirsty herd, a sick herd and drovers on the road to a fair
  the herders.
- **Theirs only.** `MissionPeople.only` and `whileKind`: the order's medicine for the sickhouse in a plague year (a
  delivery, their carts at the gate) and the league's factor to see home (the traveller's escort); an ask only theirs
  takes no share of the missions' draw while no people of its calling is at peace (`MissionPool`). `HappeningSetting`
  `people` `calling:CALLING`: the order's hospitaller at the gate (free care for an ailment, medicine sent back, the
  doctor learning) and the league's outrider (the need `caravan`: bidden, the league's own trader caravan comes).
  `CampKindDef.calling`, `seasons`, `maxAnimals`: the herders' winter herd (fall and winter, up to twelve head and a dog,
  `CampLookMath.MaxHerd` 14).
- **Their towns.** `TownPlanDef` (`Defs/PeopleDefs/Rimfolk_TownPlans.xml`): rows of keys read into the kept-layout grid
  (`PeoplesMath.Grid`, the game's footprint rule `PeoplesMath.Footprint`) and placed by `TownLayouts.Place` from
  `Patch_TownLayoutSettlement` (`PeopleTowns.PlaceTown`, after a town the colony was), near the map's middle where at
  most 6% of its cells are bad ground; then the game's loot and the people's goods in the storehouse, the herd in the
  pens, the healroot in the garden, the notables and the game's settlement group under its defend-the-town lord
  (`TownLayouts.AtHome`). The power step stands down for it (`TownLayouts.NotePlaced`). Else the game's own base.
- **Elsewhere.** A sickhouse of the order sees to the colony's lodgers as medicine would (`SickHome`); the hand-over's
  peoples never borrow these peoples' kinds (`HandOverPeoples.Lend`). The Almanac's page *the road league, the
  hospitallers and the herders*.
- **Release B: peoples that arise during play** (`Peoples/Arising.cs`, `ArisingMath.cs` pure and tested in
  `Tools/Tests/ArisingTests.cs`, "new peoples: "; `ArisingWords.cs`, `ArisingBook.cs`, `ArisingDebug.cs`). Four causes
  read from what the world keeps: a fight's ruin within reach (`LedgerRuin.Fight`, from three days after the fall to
  sixty, two in five: `ArisingMath.Fated`, the same answer each time it is asked), three or four towns of two peoples or
  more at 62 or more within 18 tiles of the most prosperous and at peace with each other and the colony
  (`ArisingMath.LeagueOf`, a fifth of the looks), one the council sent away (`BanishRecord`, alive in the world and of no
  people, 15 to 300 days after, half of them), and with Ideology the end of a war of words (`Council_WS_HolyWar` in the
  world events' history, within 30 days, half of them). The template by the cause's age (`ArisingMath.Template`: the
  calling a cause may take nearest its age), made the game's way (`FactionGenerator.NewGeneratedFaction`, hidden while
  made, no leader until its faith is set: `Arising.Making` in `Patch_HandOverLeader`), its name from its cause, its
  colour the palette's farthest from the world's (`Faction.color`), its faith (survivors keep their old people's, a band
  its leader's, a league another league's, the splinter a reformed creed: `IdeoUtility.RandomizeNormalMemesForReforming`),
  goodwill by what happened (`ArisingMath.*Goodwill`, never past the game's -75 or 75), its town by `Arising.TryTownTile`
  (a few days off, clear of `RiseFallMath.ClearOfHomes`, on no land but its own peoples', `NewTowns.MakeTown`), the
  region's faces (`RegionFaces.Founded`) and the founding pace (`RiseFall.NoteFounded`), a letter
  (`Importance.Important`, the founding's means and rite), the history's `world:arose`, a line on the people's and the
  towns' cards. The one sent away leads the band (`Faction.leader`), rides with no one else's raid (`JoinArrivingRaid`) and
  doesn't ask at the gate to come home (`HappeningWorker_Prodigal`). Run by the
  region's day (`RegionBook.Daily`), one a day at most; the pace `ArisingMath.PaceAllows` (40 days between any two, 90
  between two of a cause, none before day 15, six standing), scaled by the setting `newPeoples` (0 to 200%). Saved: the
  records in `ArisingBook` (one per people, `ArisingMath.MaxRecords`). The Almanac's page *new peoples that arise*.
- **Release C: four more peoples** (`Tools/Tests/PeoplesCTests.cs`, "peoples, release C: "). The pilgrim fellowship
  (`Rimfolk_Pilgrims`, medieval, tribal parent, calling `pilgrims`), the learned society (`Rimfolk_Scholars`, industrial,
  outlander parent, `scholars`), the spacer enclave (`Rimfolk_Enclave`, spacer, outlander parent, `brokers`), the river
  folk (`Rimfolk_RiverFolk`, medieval, tribal parent, `fishers`), on release A's template with three new fields:
  `ariseAges` (the ages a template arises in, wider than its own: `PeoplesMath.AgeSpan`), `byWater` (its towns by a
  river: `PeopleTowns.ToTheRivers` from `ArisingBook.StartedNewGame` moves a new world's landings to a river tile within
  twelve, a new people's town is looked for on one) and `fish` (Odyssey's fish of the map's own waters in the stores).
  Their plans (`Rimfolk_TownPlans_PeoplesC.xml`) and a new plan mark, `dock`: `PeopleTowns.Spot` looks wider for a place
  where most docks are water (`PeoplesMath.DockSpot`), `LayDocks` lays the game's bridge over water a bridge can stand
  in and planks on dry ground. Callers (`Peoples/PeoplesCallers.cs`): the pilgrims' night (`HappeningWorker_PilgrimShelter`:
  dusk, a band of three to five, each lodged by `VisitorServices.Lodge`, the morning a memo of the living events,
  `LivingEvents.Shelter`, with the blessing said, the colony's feeling, and one night in ten a cough caught through
  `Harm.Roll`/`Harm.Ail`), the society's copyist (`HappeningWorker_ScholarCopyist`: a book of the colony's age by
  `DilemmaEffect_Goods`, or a lesson), the river folk's catch (`HappeningWorker_RiverCatch`, the biome's fresh-water fish),
  the enclave's lander (`HappeningWorker_EnclaveLander`, an industrial or spacer colony: `DilemmaEffect_EnclaveLanding`
  drops the factor with the stock in its pack, the marshals and the beasts by drop pod, under the game's visitors' lord,
  as its visiting traders trade). Asks (`Rimfolk_Missions_PeoplesC.xml`): the hostel's food in a holy year
  (`whileKind` `faith`), the river folk's timber and net cloth. By patch (`Patches/Rimfolk_PeoplesC.xml`): nine world
  events prefer a calling, and five visitor events (`HappeningExtension.prefers`, read by their own workers through
  `Peoples.Weigh`). Release B's causes may take their ways (`ArisingMath.Leans`, `Lean`, `Wanted`; the cause's `lean`):
  survivors near a river (half of them, `SaltRiver`) river folk, a band led by a learned one (Intellectual 10)
  scholars, a league of the spacer age an enclave, faithful who broke away in an industrial or spacer world pilgrims; a
  lean with no river in reach is let go. Release B's pace and caps unchanged. The Almanac's page *pilgrims, scholars,
  the enclave and the river folk*. Fixed on the way: a caller of a calling (`HappeningSetting.people` `calling:`) could
  be a Hospitality guest of any people already on the map, standing in under that people's name.
- **Their gear, their kinds of people and their gifts** (`Tools/Tests/PeoplesGearTests.cs`, "peoples' gear: ";
  docs/design/peoples.md, the section of that name). No kind has `apparelColor`: each people's base kind dyes its
  signature pieces by name (`specificApparelRequirements` with an `apparelDef` and a `color`), a civilian kind picks its
  signature garment by `alternateTagChoices`, the rest comes from wider tags and money by role. Soldiers wear a common
  kit (`apparelRequired`), a religious habit is shared but not its cut or age, civilians vary. A mod's tags and pieces
  only with `MayRequire` on it, every name in `Tools/Tests/PeopleGear.txt`; medieval and tribal peoples off
  `IndustrialBasic` (Vanilla Apparel Expanded's modern clothes). `inventoryOptions` for what they carry, never on a
  trader. `xenotypeSet` a real mix of Biotech's, Vanilla Races Expanded's, Alpha Genes' and the xenotype mods' kinds of
  people, three in four or so baseliners. `PeopleKindExtension` (gifts: a class of A RimWorld of Magic by its trait's name,
  or a psylink, with Vanilla Psycasts Expanded a path by name) given by `Patch_PeopleGifts`, a postfix on the game's
  `GenerateNewPawnInternal` (fresh pawns only, the deeper game hooks on); `PeoplesGearMath` (one roll against the shares,
  never more than 60% of a kind, the psylink's levels, the class's setting). Pawns already in a game keep theirs.
- **Their stories, sayings and touchpoints** (`Tools/Tests/PeoplesLoreTests.cs`, "peoples' lore: "; docs/design/
  peoples.md, the section of that name). A founding story in each faction's description (its middle paragraph), and
  `PeopleExtension.honours`, `fears`, `sayings` shown on the people's card (`PeopleLore`, a lines section in the card's
  story slot; a saying picked by the faction's id, so it reads the same). Their voices in the existing pools by
  `their:CALLING` (letters' condolences and warnings, messengers, `Rimfolk_VisitorLines`, `Rimfolk_Pool_OutsiderSays`),
  three talk pieces each, `Rimfolk_Pool_PlayDisputation`. Touchpoints, each only from a people of its calling:
  `HappeningWorker_OrderNurse` (a fever: the hospitaller goes from bed to bed by `VisitorServices.Dose` with
  `Rimfolk_OrderNursed`), `HappeningWorker_LongWalk` (a file of pilgrims at the graves, a walk together or food carried
  out), `Rimfolk_Evt_Disputation` (data only: an evening gathering with the fellow as guest), `HappeningWorker_EnclaveSalvage`
  and `DilemmaEffect_EnclaveSalvage` (salvage by drop pod at the trade spot, by `PeoplesLoreMath.SalvageGoods` of the
  colony's age; a runner on foot for a colony without radio), `HappeningWorker_RiverNets` (boatmen on the dry bank by the
  water), the league's camp `Rimfolk_Camp_LeagueWinter` (winter, trading, guards if asked, the Waystone Book) and the
  winter herd's young beast. A camp's gift (`CampGift`) may now be `young` (a young beast of the camp's own grazing kinds,
  the colony's, handed over at the gate on the farewell walk) and kept for a good stay (`maxTension`: no walk to the gate
  without it). Faiths gain a soft meme or structure each where thin, checked against the memes' exclusion tags. Arising
  names gain a third form carrying their ways. Nothing saved, no clock, nobody hurt.
- **Release D: the Lantern Wardens, the Quiet Bell, the Circuit of Tamers and the Wheel** (`Tools/Tests/PeoplesDTests.cs`,
  "peoples, release D: "; docs/design/peoples.md, the section of that name; the first four of `docs/design/peoples-next.md`).
  `Rimfolk_Wardens` (medieval, tribal parent, calling `hunters`), `Rimfolk_QuietBell` (medieval, tribal parent,
  `sextons`), `Rimfolk_Tamers` (industrial, outlander parent, `tamers`), `Rimfolk_Wheel` (industrial, outlander parent,
  `salvagers`), each arising in every age (`ariseAges` 0~3), drawn only from the player's active mods
  (`docs/player-mods/`). Every age: `Peoples.AgeOf` (an arisen people keeps the age of what it came from,
  `PeoplesDMath.PeopleAge`), `PeopleKindExtension.ageArms` (a kit's weapon tags for other ages and what it may cost,
  `AgeArms.Arm` as a pawn is made) and `PeopleGift.ages`. Callers (`Peoples/PeoplesD.cs`, `PeoplesD2.cs`): the Wardens'
  hunt on a legend, nemesis or alpha on the land (`HappeningWorker_WardensHunt`, `DilemmaEffect_WardensHunt`: the game's
  assault-things lord, the colony's shot on the game's hunt; the kill seen in `HappeningTracker.Notify_Killed`, the
  carcass skinned by `JobDriver_PeoplesWork` and its butchering split, `PeoplesD.Divide`; given up after two days, they
  walk home), the pelt-buyer, a lantern for one a beast took; the Bell at the enemy's dead (a subclass of the
  undertakers' worker, `DilemmaEffect_BellForTheDead`'s goodwill with the dead's peoples), its funeral at a fresh grave,
  the void's signs (Anomaly); the tamer's bout (`JobDriver_TamerBout` by the Fighting-type spar's rules, `HarmKinds.TamerBout`
  through `Harm.Roll`/`Injure`), eggs swapped or sold, a stray brought home; the Wheel's fixer (a subclass of the
  tinker's worker), its scrap-runners (`PlayCarry.Scrap`), a mechanoid wreck stripped half and half (`StripJob`). The hunt
  and the bout are visits of the happenings' tracker (`Visit` kinds `wardenHunt`, `tamerBout`). The Wheel's towns move
  (`WheelTowns.Daily` from `RegionBook.Daily`: on each town's own day of 30, two to five tiles by `Arising.TryTownTile`,
  never with a map, a caravan or a quest on it). Asks, the patch (four world events, the undertakers, the tinker and the
  vet prefer them: `Undertakers.People`, `Tinker.People`), release B's leans (`ArisingMath.Lean`: survivors to the Bell on
  a fated quarter, `SaltBell`, or in a later age to the Wheel on a fated third, `SaltWheel`; a band led by one good with
  beasts to the Circuit, by a good shot to the Wardens). Nothing saved.

### The colony's animals with powers

The player's pick "magic and powers" (7 November 2026, built without a proposal round): four parts that share one pass
(`Sched.RarePowers`, tick 227 of the 250-tick cycle; its hour on the pass at tick 227 of the hour, its day in the first
hour of the day; `Animals/AnimalPowers.cs`, timed as `Pass.RarePowers`) and one saved book (`AnimalPowersBook`, a
GameComponent). The numbers are pure (`Animals/AnimalPowersMath.cs`; `Tools/Tests/AnimalPowersTests.cs`, "animal
powers: "). No Harmony patch, no letter: news, the chronicle, journals, moods, glows and one-sentence words.

- **Pokémon types at home** (`Integrations/PokeWorldTypes.cs`; PokéWorld, types by `PokeWorldLink.TypesOf`). Only the
  colony's own, spawned on a home map, up and not in a mental state; counted once an hour by species (`MaskOf`), run
  every 250 ticks. "Rests" is not walking (`pather.Moving`). Fire: `GenTemperature.PushHeat` into the room it rests in,
  6 heat a second times its body size (0.5 to 1.5) through the pass, only while the room is under 21 °C and never past
  it (`AnimalPowersMath.HeatPush` stops at the mark: the game adds energy over the room's cells), nothing outdoors, two
  a room. Ice: the same, cooling a room over 23 °C, never below. Grass: crops (sowable, in a growing zone or a plant
  grower) within 2.9 cells grow a quarter faster by their own rate (`GrowthPush`: a quarter of `1/(60000 growDays)` times
  `GrowthRate` for 250 ticks), only while plants are awake (the game's own resting hours), one push a crop a pass.
  Electric: a battery of the colony's own within one cell (`CompPowerBattery.AddEnergy`) gets 100 W. Psychic: a colonist
  within 6 cells in its sight, under their minor break threshold or within 5 points of it, gets `Rimfolk_PW_Calmed` (+5,
  half a day); one in two days a colonist, one a day a Pokémon (`calmTicks`, by ThingID). Fighting: `JoyGiver_PokemonSpar`
  (`Gaming_Dexterity`, base chance 2): a grown colonist who can fight, not hurting, no danger on the map, goes to a free
  Fighting type (their own first) and spars 900 to 1,500 ticks (`JobDriver_PokemonSpar`: the game's lunge each way in
  turn, 15 melee experience a swing of theirs; the Pokémon holds still in `JobDriver_StayWith`); at the end
  `Harm.Roll(pawn, 1/25, HurtClass.Bout)` may land a bruise of 3 to 6 (`Harm.Injure`, under the shared cap), said and told
  at `Importance.Important`. Water: a fire within 2.9 cells takes 40 extinguishing damage a pass; an idle one jogs to a
  fire on the home area within 10 cells. Each shows a glow in view (the game's heat glow, a tinted lightning glow,
  micro sparks, a splash) and, once a day at most for each power on a map and by a chance of one in eight, the nearest
  colonist who sees it says a word (`Rimfolk_Pool_PW_Say*`). Settings `pokemonTypes` and its six parts.
- **Familiars** (`Integrations/MagicFamiliars.cs`; A RimWorld of Magic, read by its defs' names: `TM_Mana`,
  `TM_ArcaneWeakness`, the trait `Faceless`, checked against its 1.6 source). A mage is a grown colonist with an inner
  life and the mana need, not faceless. Bound by the council's answer (`Rimfolk_Wish_Familiar`, `WishWorker_Familiar`:
  promised, a `FamiliarBinding` waits) or the mage's own choice (a mage bonded to one of the colony's animals, one day in
  four). A binding begins when the mage is free for a play-out, from eight in the morning to nine at night, no danger, the
  animal awake and reachable; `JobDriver_BindFamiliar`: up to it, an hour beside it (`BindingTicks` 3,000; it stays
  facing them), a glow between them, then `Bound` (named if it had no name, news, the chronicle, a journal line, +3 for
  5 days). Dropped after 10 days or six tries. Every 250 ticks: the familiar glows faintly in view; within 12 cells of
  its mage, 0.0004 mana (`Need.CurLevel`, never above `MaxLevel`) and the hediff `Rimfolk_FamiliarNear`
  (`IncomingDamageFactor` 0.95, renewed while near, gone 700 ticks after). Its death: `Rimfolk_Fam_Died` (-12, 15 days),
  the mana emptied, `TM_ArcaneWeakness` +4, told at `Importance.Important`, the chronicle and the journal. One a mage;
  never an animal bonded to someone else, not flesh, or of the mod's own (its summoned creatures). Setting `familiars`.
- **Omen animals** (`Animals/OmenAnimals.cs`). Each hour, a colony animal on a home map born within two hours, not seen
  before: its sign now (`SignOf`: the game's Eclipse, then its Aurora, a shiny Pokémon, a calendar day falling today
  that is a tradition, the founding, the player's own or kept before), at most one in 20 days and three living in a
  colony, half the births on a kept day (by the animal's own seeded roll, so a reload doesn't roll it again). Named by
  its keeper (`HerdStory.Keeper`; half the time a name from `Rimfolk_Pool_Omen_Names` by its sign), the news, the
  chronicle, the keeper's journal, the name said over it and a word back from whoever is with them. While one thrives
  (alive, up, not ill or starving: a per-map cache each hour), `Rimfolk_Omen_Thrives` (+2, a situational mood named for
  it, `Thought_OmenThrives`) for its colony; its death `Rimfolk_Omen_Died` (-4, 5 days) for everyone on its home map,
  told and in the chronicle. Sold or released, let go without ill luck. Saved: the living ones (`omens`) and the last
  omen's tick. Setting `omenAnimals`.
- **Sacred animals** (`Animals/SacredAnimals.cs`; Ideology). The kinds are the colony's faith's `VeneratedAnimals`; the
  game's rules (hunting, slaughter, the meat, a tame one's death) stay. Each hour: a fresh corpse (dead within two
  hours) on a home map of an animal that wasn't the colony's, of a kind a colonist's own faith venerates, gives those
  colonists `Rimfolk_Sacred_Fell` (-2, 3 days, three at most), the news, and a word at the body (once an hour a map); a
  hunt designation on one seen by a believer in view gets a word. The council's cull (`DilemmaEffect_Cull`) leaves out
  any animal a faith of the colony's venerates (`SacredAnimals.HeldSacred`), as `PlayCarry` already did for hunts.
  `Rimfolk_Dilemma_SacredDay` (`DilemmaWorker_SacredDay`, a custom: spring to autumn, a sacred animal on the map, asked of
  a believer): food carried out to it (`DilemmaEffect_FeedBeast`) and a gathering round it (`PlaySpot.Stranger`), +3 for
  3 days; or the food only; or not this year. Kept, it joins the calendar as every custom does
  (`Calendar/CalendarCustoms.cs`). Setting `sacredAnimals`.
- **Dev actions** (Debug actions, "Rimfolk", "Animal powers: ..."): what each part sees now (the log), the Pokémon types'
  pass now, a spar, a binding begun now, a familiar's death, an animal born under a sign, the sacred animals' day asked.

### Alphas, nemeses, legends and great herds

The wild's alphas and bosses (the player's pick "D, alphas and bosses", 7 November 2026: built without a proposal
round). `Source/ColonyCouncil/Beasts/`, the pure rules in `BeastsMath.cs` (`Tools/Tests/BeastsTests.cs`, "beasts: "),
the words in `Languages/English/Keyed/Rimfolk_Beasts.xml`, the lines in `Defs/FlavorPoolDefs/Rimfolk_Pools_Beasts.xml`.
Animals are picked by what they are (`RaceProperties`: size, predator, herd animal, migration, insect, dryad, entity),
never by name; the land's own animals are read from each biome's own records (`BiomeDef.wildAnimals`, read once per
look in `GreatHerds.LandAnimals`), never `AllWildAnimals` or `CommonalityOfAnimal`, which a biome listing an animal
twice breaks. Each part has a rate on How much happens (`packAlphas`, `beastNemeses`, `regionLegends`, `greatHerds`,
0 to 200%: `BeastsMath.Rate`, `Gap` and `Chance`); presets at 50% and 125%. No Harmony patch of its own.

- **Pack alphas** (`PackAlphas.cs`). A pack is three or more of one hunting kind (`BeastsMath.PackKind`) within 14
  cells of each other, wild, up, seen and in no mental state (`FindPack`), none of it a story already (named, a
  nemesis, the beast the colony watches, a world event's). The day (`Daily`): with no alpha open at the home and 30 days
  since its last (`AlphaGapDays`), a quarter of the days a pack is there it gets one (`AlphaDayChance`). A big threat
  just come (`storyState.LastThreatBigTick`, looked at once within two hours, `WatchThreats`): a pack of the game's own
  manhunters gets one three times in ten (`ManhunterAlphaChance`, 15 days apart). The alpha is the best by
  `AlphaScore` (size, its prime, a male a little first), named from `Rimfolk_Pool_AlphaNames` in the voice of someone
  of the colony who sees it (a `NameSingle`, so the game draws it on the map), with `Rimfolk_PackAlpha` (incoming damage
  0.85, melee 1.15, meat and leather 1.25). The news for a plain pack (`CC_Alpha_Led`), a letter for manhunters
  (`CC_Alpha_LedManhunters`); a later letter once, when the pack threatens the colony (`Threatens`: manhunting, or a
  `PredatorHunt` job on one of the colony's people or animals). Every 250 ticks (`Rare`): dead, it is told (a letter if
  a threat letter went out, else the news), its pack scatters (`Scatter`: each of it up and wild, with manhunters every
  manhunter of its kind on the map, out of any mental state, each its own `LordJob_ExitMapNear` from where it stands),
  and its slayer (`BeastBook.Slayer`, the wounds' combat log) gets `Rimfolk_Alpha_Slain`, a journal line and the name
  `Rimfolk_Epithet_Packbreaker` (`EpithetTracker.Bestow`); tamed, its pack follows (`Follow`): from half a day after,
  one at a time (`FollowAfterDays`, then 0.6 to 1 day apart), the next of it on the map (or back from the world at the
  edge) walks to it (a Goto job re-given while it isn't there) and is the colony's within 7 cells
  (`InteractionWorker_RecruitAttempt.DoRecruit`, the alpha's master its own), six at most, one not in within two days
  passed over. Gone off the land, the news (flavor). Saved: `BeastBook.alphas` (`PackAlpha`, its pack by reference,
  `MaxAlphaRecords` 16, the ended first).
- **Animal nemeses** (`BeastNemeses.cs`), on the nemeses' own (`Strangers/Nemeses.cs`). Every 250 ticks a beast nemesis
  (announced or still being fought) up on a home map under 45% of its health (`BeastsMath.Flees`; never downed, dead or
  owned) leaves any mental state and runs for the nearest edge (its own `LordJob_ExitMapNear`, jogging); off the map it
  is kept in the world (`ForcefullyKeptPawns`) and comes back 6 to 16 days later at 100% (`ReturnDays`), twice at most
  (`MayReturn`), once healed to 60% and when the map has no threat on and none came in the last half day, at an entry cell; one time in three
  (`ForBlood`) it starts the game's own `Manhunter` state there, with a letter (`CC_BeastNem_Blood`); the nemeses tell
  the rest (`Returned`). Nemeses.cs names one that ran off unannounced as got away (`BeastNemeses.Fled`) instead of
  forgetting it, and never names a beast lying downed (`!x.Downed`). The hour (`Named`): a beast nemesis newly named
  (up, or away) that killed or maimed (`Grave`) gets `Rimfolk_BeastScars` (a mark, no stats), and three times in five
  (`OathChance`) the closest of those close to the one it hurt (`Close`: a partner or kin, a bond of 30 not a rivalry,
  an opinion of 60; grown, able to fight, not sworn already) swears (`Swear`): a line said aloud (`Rimfolk_Pool_BeastOath`),
  a message, the chronicle, a journal line, the spark "nemesis" for "settle the score", whose `CanHave` now counts an
  open oath (`BeastNemeses.Sworn`). While the beast is up on the sworn one's map, `Rimfolk_BeastOathFury` once a day.
  The oath ends (`Oaths`): kept (their own hand: `Rimfolk_Beast_OathKept`, the goal's counter), someone else's
  (`Rimfolk_Beast_OathOthers`, the counter too), tamed, vanished, or lapsed (the sworn one dead or gone); each a journal
  line. Saved: `BeastBook.flights` (`BeastFlight`, `MaxFlights` 12), `oaths` (`BeastOath`, `MaxOaths` 20), `noted`
  (`MaxNoted` 40).
- **Legends of the region** (`RegionLegend.cs`), the world event `Rimfolk_WS_Legend` (kind `legend`) with `LegendWorker`.
  Can start (`CanHappen`) when `BeastsMath.LegendDue` (the world's 40th day, 150 days after the last one ended at 100%,
  `BeastBook.legendEnded`) and a kind fits (`Kinds`: of each race its first animal kind, the dozen biggest beasts of 2.5
  or more whose comfortable warmth is within ten degrees of the home tile's, `LegendFits`; with PokéWorld its
  legendaries, read from its comps' attributes, once its own `LegendaryQuestsTracker` has a condition met:
  `PokeWorldLegends`). `Cast`: the kind by `LegendWeight` (its size squared), the beast made old (`AgeMin` to `AgeMax` of
  its years) with `Council_Legendary`, named from `Rimfolk_Pool_LegendNames` (a legendary Pokémon keeps its species'
  name), held by the event (`WorldSituation.Hold`); its ground's tile (`GroundTile`: land a settlement could stand on,
  nothing on it, 4 to 12 tiles off, within reach of the home, hills and woods first); a friendly people's nearest town
  for {PLACE} (flag `town`). `Begin`: its ground, a `Site` with no faction of the part `Rimfolk_LegendGround`
  (`SitePartWorker_LegendGround`: its name, the arrival letter's part looking at it; no detection raids, not an attack),
  labelled "{NAME}'s ground"; the start letter looks at it. The bounty beat (requires `town`) sets `bounty` and its
  {COUNT}, paid on the "slain" ending by `WorldEffect_Silver` from afar. `Daily`: every five to nine days its ground
  moves 2 to 4 tiles (`MoveMinTiles` to `MoveMaxTiles`), unless the site has a map or a caravan of the colony is on it
  or bound for it; a message with the direction looks at the new site. A caravan's arrival makes the map with the beast
  on it (`GenStep_LegendGround`, linked to the site part: a standable open cell near the middle, unfogged). `Hourly`:
  dead by a colonist's hand, "slain" ({B} the slayer, {ITEM} the trophy's words); dead otherwise, "fell"; the colony's
  (tamed, or caught), "befriended" ({B} its master); lost or someone else's, "gone"; back in the world with no ground and
  up again (the caravan left it there), its ground made again near the last, with a message. Time runs out: "moved".
  `Ended`: `BeastBook.legendEnded`, the site removed unless it has a map, for "slain" the trophy beside the body (its
  butcher products, horns or tusks, else a quarter of its leather, 10 to 60) and the name
  `Rimfolk_Epithet_GiantSlayer`, for "befriended" `Rimfolk_Epithet_BeastSpeaker`; journal lines. The "slain" ending
  sings of it at home (`DilemmaEffect_Bonfire` with music, `Rimfolk_Pool_LegendSong`) and gives `Rimfolk_Legend_Slayer`
  and `Rimfolk_Legend_Fell`; "befriended" `Rimfolk_Legend_Befriended` and `Rimfolk_Legend_Awe`.
- **Great herds** (`GreatHerds.cs`). The day: each new season (year and season, `BeastHome.herdSeason`) decides its
  great herd, spring and fall (`Migrates`) six times in ten at 100% (`SeasonChance`), on day 2 to 12 of it
  (`PlannedDay`); put off a day while a threat is on. `Start`: a herd kind (`HerdKind`: a herd animal that can migrate,
  never a predator, an insect or a dryad, 0.9 body or more, fit for the season's weather; a Pokémon only as
  `PokeWorldLink.Allows`), the land's own three times likelier (`HerdKindWeight`), `GreatCount` head (6 to 24, fewer the
  bigger, 60 bodies at most), spawned near an entry cell and walked to the farthest reachable edge
  (`LordJob_ExitMapNear`, walking), as `IncidentWorker_HerdMigration` does; a letter by the season and the hunting
  season (`Rimfolk_Evt_HerdCrossing` fired when its worker can, the council's own question). Every 250 ticks, each home
  map's groups of `LordJob_ExitMapNear` with four or more wild animals of one kind (the game's migrations too) are
  watched (unsaved `HerdWatch`): spooked (`Spooked`) by one of it hurt in the last 250 ticks (`lastHarmTick`) or found
  dead since the last look, a projectile within 9 cells, or a fire within 7 (once a herd), at `SpookChance` times the
  rate, it stampedes (`Stampede`: those not in a mental state, a new group sprinting for the edge cell nearest the
  point across the map from what spooked it, from the nearest humanlike drafted, hunting or fighting within 40 cells
  of the hurt one, a projectile's launcher, or the fire), told at once (`Tell.Message`, threat). For 1.2 hours
  (`StampedeHours`) each runner of 1.2 body or more knocks down someone smaller than half again its size whose cell lies
  within 1.3 of its way since the last look (`SegmentDistSq`; once a stampede for each, three at most): only with
  everyday hurts on, `Harm.May` and `Harm.Chance(TrampleChance)`, a blunt blow (`TrampleDamage`, 6 to 16 by its size)
  from the runner, `Harm.Note`, and for one of the colony or its guests a message; and plants of growing zones on its
  way, a fifth of them, twelve at most (`Plant.Destroy`), told in the news once it is over. A herd of six or more in
  view and no threat on (once a herd, looked at hourly): up to three of the colony within 32 cells with sight of it and
  at ease (no job, wandering, waiting, at leisure, hauling or cleaning, not ordered, in no group) take
  `Rimfolk_WatchHerd` (`JobDriver_WatchHerd`: face the nearest of it, 1,200 ticks, resuming what they were at), one says
  so (`Rimfolk_Pool_HerdPasses`), each gets `Rimfolk_Herd_Watched`. Saved: each home's season and the alphas' times in
  `BeastBook.homes` (`BeastHome`).
- **The clock** (`BeastBook`, a GameComponent; an older save gets an empty one): every 250 ticks at `Sched.RareBeasts`
  (193: `PackAlphas.Rare`, `WatchThreats`, `BeastNemeses.Rare`, `GreatHerds.Rare`, each cheap with nothing there), its
  hour on the same tick (`Pass.Beasts`: alphas' threats and sightings, nemeses' returns, oaths), its day in the first hour
  of the day (alphas and herds per home, the nemeses' flights pruned, the caps).
- **Harm** (`Tools/Balance --harm`, `BeastSim.cs`): about 0.019 hurts a colonist-year from stampedes and 0.007 from beasts
  come back for blood at 100%, 0.025 in all, beside a raid's 1.4 to 2.0 and Rimfolk's everyday hurts' 0.26 to 0.50. An
  alpha adds none of its own; a legend hurts only those who go to its ground.
- **Dev mode** (`BeastsDebug.cs`, "Beasts: ..."): a wild pack led by an alpha, manhunters led by one, the alpha tamed,
  its pack in now, a beast nemesis made here (maimed, sworn against), it flees now, back now; a legend now, it moves, it
  walks onto this map; a great herd now, the herd stampedes; what stands now in the log.

### Letters that arrive in days and bring something (the player's decision 12, 6 November 2026)

In the player's save Stace wrote three letters to notables in 18 days; each leg took 12 to 14 days (the towns are over
fifty tiles off, and a leg went at the other people's walking pace, twelve days at most), and the one answer was "Your
letter came. We are well enough, thank you." and "It changed little between the colony and Blash", 26 days after it was
written. Now (`World/LetterMath.cs`, pure and tested in `LettersPetitionsTests`; wired in `World/NeighbourLetters.cs`):

- **By the colony's means.** A letter goes by the colony's age (`Ages.Ours`, `LetterMath.MeansOf`): runners (tribal), a
  rider (medieval), a courier (industrial), a courier by shuttle (spacer); a call on the comms console is at once. Each
  leg is half a day plus 0.09, 0.06, 0.04 or 0.015 days a tile, at most six, five, four or two days (`LegDays`); the
  answer comes back the same way (`backDays`), and the courier tracker still brings it to the gate by a messenger of
  theirs (`Couriers.MailAnswer`). Before (by the other people's age) and after (by the colony's), a leg at 10, 25, 50 and 80 tiles: tribal 4.4, 10.4, 12, 12
  days before, 1.4, 2.8, 5, 6 now; medieval 3.7, 8.7, 12, 12 before, 1.1, 2, 3.5, 5 now; industrial 2.6, 5.9, 11.4, 12
  before, 0.9, 1.5, 2.5, 3.7 now; spacer 1.6, 3.4, 6.4, 10 before, 0.65, 0.9, 1.25, 1.7 now. The save's letter: 12 days a
  leg before, under three by courier. A letter carried home by one of their people at the colony goes at their means;
  a call's "how far they are" (when a guest or raiders could come) at theirs too. Letters already on the road keep the
  day they were given (`due`, `backDays` saved). The letters page says by which means it is on the road, never when.
- **What an answer brings** (`LetterMath.Brings`), for the purposes whose answer was words only: greetings answered
  warmly bring a token gift (50 to 100 silver's worth by the colony's wealth, `Loot.Make`) in the messenger's pack,
  kindly or coolly how their town is faring (`RegionFortunes.Line`, else a piece of news near them, else that the roads
  are quiet); condolences taken warmly or kindly are remembered by their people (the "mourned" deed, as before, now
  said); a warning heeded kindly or coolly is remembered ("warned"), heeded warmly it is a debt of honour (the deed at
  weight 10, `LetterMath.HeededWarmly`, made when the answer reaches the colony: `NeighbourLetter.owed`, saved, then
  `Owed`, through `FavoursOwed.Quietly` so the answer's own words tell it once), where debts of honour are kept
  (`CanOwe`; else remembered). News, help, a visit and stage 2's purposes (a favour, a pact, a truce, a ransom, an envoy,
  a ward, a match, an oath, a feast) bring what they always did.
- **Regard in words.** The answer's last line says how far it moved them, from their regard before and after
  (`LetterMath.Move`: into another band, "civil before, friendly now"; a good deal, a shade, nothing, a shade less;
  `CC_LetterRegard_*`), the writer named on a warm answer, and their people's goodwill when it rose. It replaces the
  old tone line (`CC_Letter_Outcome_*`, kept for answers already written).
- **A refusal with a reason.** A curt answer or a refusal says why (`LetterMath.Why`: the heaviest of what counted
  against it in `NotableMath.Score`: a people at war first, then their regard of the colony or the writer, the writer's
  words, their nature, hard times on business, the same asked lately, a call, a great deal asked, else a bad day), and
  so does an invitation not taken up, or one taken up with nobody free to travel. A truce, a ransom, an envoy, a ward, a
  match and an oath give their own reasons (`LetterMath.OwnReasons`) and get no second one.

### Diplomacy

Stage 2's base and its first item, favours owed and called in (docs/design/stage2-diplomacy.md, release 2a.1):
`Diplomacy/`, rules and text on records that exist, its pure parts tested (`DiplomacyMath.cs`, `FavourMath.cs`,
`NatureMath.cs`; `Tools/Tests/DiplomacyTests.cs`).

- **The ledger owns it.** A debt of honour is a Favour tie of origin `debts` (the outsiders' bring-next-time favours
  keep `outsiders`): from the one who owes (their people, or a notable as a person) to the one owed (the colony that did
  it, or the colonist), its terms the deed it came from (`key`), how big (`number`) and who did it (`about`), until two
  years after. Made where the memory notes a deed (`FactionMemory.Note` calls `FavoursOwed.Noted`: a good turn of size
  10 or more of `FavourMath.OurGoodTurns` or `TheirGoodTurns`) and where it names who did it (`KinMemory.Credit` and
  `Note` call `FavoursOwed.Personal`: a notable helped in person owes the colonist, twelve such at most). One open each
  way per people and colony; a new good turn renews it. While called in its terms also hold what was asked
  (`goods`: `FavourMath.AskCode` or `NeedCode`) and, for a call of theirs, the day it is due (`days`). Help toward peace
  is a Promise tie (key `peace`, 20 in its number, a year) that 2a.2's truce answer reads. `TieRules.Kinds` is 14: Secret
  and Scheme are reserved (rules and bounds, nothing makes one), and all ties are bounded at 1,080.
- **Nothing else saved.** Favours need nothing beyond their ties (the last call of a people is the history's
  `tie:favour:calledIn`); the first records that aren't ties (an envoy's stay, an offer for a captive) came with
  release 2a.3, in `DiplomacyHome` kept by the `ColonyBook` rules (below, "Ransom and the envoy"). Their letter (`ChoiceLetter_FavourAsk`) saves which tie and need it is.
- **No pass of its own.** `Diplomacy.Hourly` rides on the neighbours' hour (`NotableBook`, `Sched.Neighbours`): lines
  waiting for their letter, calls of theirs overdue, and once a day the look at each debt the colony owes.
- **The colony calls one in** by a letter or a call (the purpose `LetterPurpose.Favour` in the letters' machinery,
  `DiplomacyLetters`: what about, what is asked, their answer in `NeighbourLetters.Read`), from the region window's Peoples or
  the town's card (which open that letter), or face to face (`OutsiderAct.AskFavour` after Invite, `DiplomacyActs`).
  `FavourCalls.Options` lists what they can do, each greyed with why not: goods by their town's fortune and age, a word
  with a third people, a colonist they hold walked home, help toward peace. `FavourCalls.Hear` rolls their head's
  regard (`FavourMath.HonourChance`), does it (the goods with the answer's messenger or their porters, the third
  people's own letter, the colonist set down at the edge to walk to the gate, the promise) and keeps or breaks the tie;
  a refusal is the deed `brokeWord`.
- **They call one in** (`FavourAsks`): once a day, 1 in 90, only with a need the world shows (a sickness, a famine or a
  hard time among them, a feud or a contested seat, a harvest standing), a season apart. Medicine and food: their
  letter, sent by carrying it out to their messenger (`DilemmaEffect_CarryOut` to the gate); hands at harvest and
  refugees: the existing missions fired for them (`MissionPeoples.Only`), kept when the mission's deed is noted, broken
  when it ends otherwise (`Missions.Finish` calls `FavourAsks.MissionEnded`); the colony's word in a dispute: a letter
  from the speaker to their head. Refused or overdue: broken, the deed `refused`, 5 goodwill.
- **What the player sees.** The region window's Peoples (the owed strip, each people's ties) and Here and away (who is
  here, who is away, the peoples bound to the colony by marriage; UI 2, "The region in four views"; the old page names forward), drawn by
  `DiplomacyPanels` over one model built at most every two seconds (`DiplomacyModel`), its rows small pieces any page
  can draw; a click on a name goes through `DiplomacyLinks.Open`, which opens the thing's card; a town's ties in the Ties
  slot of its card;
  This week's line for a call waiting (no timer); the line in the letter that tells the good turn (else the evening's
  news). Words by both ages (`Ages.Form(AgeFeature.Favor)`; truce, hostage, envoy and levy have their forms too). Facts:
  a debt of honour speaks as `tie:honour` (`:ours`, `:theirs`, `:you`, `:due`, `{HONOURTO}`, `{FAVOURWHY}`), apart from
  `tie:favour`, so the bring-next-time talk never takes one for the other. History: the family `dip:` for what isn't a
  tie (`dip:word`, `dip:peace`, `dip:freed`, `dip:side`; the later releases' kinds named in `DiplomacyMath.Kinds`).
- **The nature service** (`Nature.Felt`, `NatureMath`): every act of item 13 listed with its sides and worked out,
  switched on in release 2d.2 (below, "Against their nature").
- **Settings**: the section Diplomacy, `diplomacyFavours`.

#### Pacts and truces

Release 2a.2 (docs/design/stage2-diplomacy.md, item 2, and the fighters through a favour from item 1): `Diplomacy/`,
its rules pure and tested (`PactMath.cs`; `Tools/Tests/PactTests.cs`).

- **Ties only.** A truce is a Truce tie of origin `truce`, a pact a Pact tie of origin `pact`, between a colony and a
  people, from whoever offered. Their ends are in their terms (`number`, a game day) and reached in diplomacy's hour,
  not by the ledger's until (unset), so a truce's end can be a peace (kept) and a pact's an offer to renew. A truce's
  terms: `key` the term (`season`, `year`; `parley:` before it is sworn), `days` the firebrands' day, `count` and
  `goods` the price; a pact's: `key` its kind (`defence`, `trade`), `days` the last call or season, `count` the calls
  (a trade pact: the cart's count), `goods` a call under way (`band:DAY`, `arms:TOWN:DAY`; a trade pact: the cart's
  thing), `about` the enemy. Their letters (`ChoiceLetter_TruceOffer`, `_PactOffer`, `_CallToArms`), the parley's lord
  and the jobs keep their own; nothing else is saved.
- **Truces** (`Truces`): offered by letter or call (`LetterPurpose.Truce`, `DiplomacyLetters`) when `PactMath.Why`
  holds (a raid beaten in 15 days, one of their notables or kin held, a friend's `peace` promise, read as 20), answered
  by `PactMath.TruceScore`; offered by them after a raid is beaten (`Truces.WatchRaids`, a quarter of the time, a season
  apart). Agreed, the envoy and a guard walk to the gate as guests of a people at peace with both (`TruceParley`,
  `LordJob_Parley`, so neither side's fighters raise a hand), an avenger's objection comes first
  (`Rimfolk_DlmTruce_Avenger`), and the speaker walks out (`JobDriver_Parley`, with the silver when that is the price);
  `Truces.Swear` starts the term, pays the price (silver handed over or set down, their captives set to be released),
  rolls the firebrands once (10%, 15% for natural goodwill below -50), breaks up their raiders' camp
  (`RaidersCamps.TruceSworn`) and writes `dip:parley`. `Patch_TruceRaids` (a postfix on
  `IncidentWorker_RaidEnemy.FactionCanBeGroupSource`) reads `Truces.KeepsAway`, a set built once a 250-tick stretch;
  `Raids.CanRaid(f, home)` asks `Truces.Bars`, so Rimfolk's own raids and camps keep away too, but the firebrands'
  (`Truces.letting`). A raid of theirs that arrives anyway (`Patch_TruceRaided`), their ambush (`Patch_TruceAmbush`) or
  a colonist killed breaks it (`truceBroken`).
- **Pacts** (`Pacts`): proposed by letter or call (`LetterPurpose.Pact`), face to face (`OutsiderAct.SpeakPact`), or
  offered by them (`Pacts.Daily`: regard 50, an enemy in common, a year apart), answered by `PactMath.PactScore`; the
  Empire makes none. A year, renewable by their letter. Called in (`Pacts.Call`; the people card's act and
  `Alert_PactCall`) by the comms console (`JobDriver_PactCall`) or a signal fire by the gate (`JobDriver_SignalFire`,
  the bonfire's own campfire, done in danger), `PactMath.Answer` decides (the attacker their friend: they decline and
  it ends without blame; nine in ten they come; else put off), and their band walks in some hours later as a war band
  (`WarCamps.MakeFighters`, `Start`, the band's `called`: only their fallen leave a debt). A debt of honour called in
  for fighters (`FavourAsk.Fighters`) brings a band the same way. A trade pact: `IncidentDefOf.TraderCaravanArrival` for
  them each season, and the cart carried out to the gate (`DilemmaEffect_CarryOut`, their porters waiting) and paid for
  (`Couriers.PaySilver`, four fifths of its worth).
- **Calls to arms** (`CallsToArms`): a partner's town within reach attacked (a Faction Territories invasion,
  `Territories.InvasionAt`, or a running bad war world event within 10 tiles) brings a defense partner's letter (once a
  day, a third of the time) or a debt's call (`FavourNeed.Fighters`); answered by a caravan of two grown colonists or more
  bound there within five days; at the town the fight is on the game's caravan battle map beside it
  (`CaravanIncidentUtility.SetupCaravanAttackMap`, their attackers at half the home's threat points, a few of theirs
  with `LordJob_AssistColony`), or Faction Territories' own fight joined through its `ArrivalAction_JoinFight`.
- **Breaches** (`PactsHooks.Noted`, from `FactionMemory.Note`): a wrong of the colony's (`PactMath.OurBreaches`, for a
  pact `PactBreaches`) breaks every truce and pact with them (`brokeTruce`, `brokePact`, and `wordBroken` with every
  people at peace with a town in reach); refusing a call breaks a pact. Factional War's battles
  (`PactsHooks.BattleBegan` from `WorldMechanics.Battle`) name a partner among the sides; a wrong done to the other side
  then is `sidedWith` for a pact partner.
- **What the player sees** (`PactsPanels`): rows on each people's card (Peoples) and its town's card, the tie's own card
  (`CardSections`, `CardActs`), the people card's acts (offer a truce, propose a pact, call them), the inbox (Needs you:
  their offers and calls; Under way: a truce to be sworn, a band coming, the colony's fighters away, in steps; Coming: a
  truce broken), the Stance lens' lines and the Threats lens dimming a truce partner. Facts: `tie:pact`, `tie:truce`
  (`:ours`, `:theirs`, `:due`; a pact's `:defence`, `:trade`), `{PACTWITH}`, `{TRUCEWITH}` (`Facts.PactTies`). Words by
  both ages (`AgeFeature.Pact`, `Truce`), the call by the colony's means. Settings `diplomacyPacts`, `truceRaids`.

#### Ransom and the envoy

Release 2a.3 (docs/design/stage2-diplomacy.md, items 3 and 4; decisions 4, 5 and 6): `Diplomacy/`, its rules pure and
tested (`RansomMath.cs`, `EnvoyMath.cs`; `Tools/Tests/RansomTests.cs`).

- **The first records that aren't ties.** `DiplomacyBook` (a game component, nothing per tick) keeps one `DiplomacyHome`
  per colony by the `ColonyBook` rules: its offers for captives (`RansomOffer`, `RansomMath.MaxOffers` = 6, each
  dropped once done or its captive gone), their envoy's stay (`EnvoyStay`, one) and the colony's envoy away
  (`EnvoyAbroad`, one), and when an envoy was last offered to it. A grudge of refusals is a Grudge tie of origin
  `ransom` (key `ransomRefused`, 120 days). Keep reasons `KeepFor.Envoy` (their envoy and attendant while here) and
  `KeepFor.Lent` (the colony's envoy at their town). Both ride on diplomacy's hour and day; the envoys' hour runs only
  while something waits (`DiplomacyBook.AnyWaiting`).
- **Their offer for one of theirs** (`Ransoms`): `Notice` looks at each home's prisoners hourly; whether their people
  want one back is a steady roll (`RansomMath.SteadyRoll` of the captive and the people, so nothing is saved for those
  not wanted): a leader, a notable or a notable's kin always, else 0.6 at peace and 0.3 at war. Due two to six days on,
  their word comes when the land is quiet: by a call (`Ransoms.ByCall`: a working console, a people who call), else a
  messenger of theirs, or a go-between of a people at peace with both for a people at war (`Ransoms.Bearer`,
  `TruceParley.Escort`), walking to the gate (`LordJob_Messenger`), whose arrival brings `ChoiceLetter_RansomOffer`.
  Accept (`Ransoms.Accept`: the game's release, the price set down by the messenger or brought by pod after a call, the
  deed `ransomed`, `dip:ransomed`); ask for more (`RansomMath.MoreChance` by the negotiator's Social: a quarter more, or
  a refusal); free them (`forgave`, which makes their debt of honour); keep them (once more within a season; the third
  refusal a grudge and `slighted`). `Ransoms.PaidFor` keeps a captive paid for or exchanged from becoming a debt of
  honour when the game's `released` goodwill comes (`FavoursOwed.Noted`). The colony writes first with the purpose
  `LetterPurpose.Ransom` (topic `ransom:ID`; `RansomLetters`), and the captive card's "Offer for ransom" opens it.
- **The game's demand without a console** (decision 4; `RansomHooks.cs`): `Patch_RansomByHand_CanFire` lets the game's
  own `IncidentWorker_RansomDemand` fire at a home with no powered console when a colonist is held by a people who can
  send word, and `Patch_RansomByHand_Execute` turns it into Rimfolk's messenger at the gate (`Ransoms.Demand`: the same
  fee range, `ChoiceLetter_RansomByHand` with the game's day). Paid, the speaker carries the silver out
  (`JobDriver_RansomPay`), and three to six hours later the colonist walks in from the edge (or by drop pod from a
  people of the machine ages), as the game's own letter returns them. With a console the game's letter is untouched.
- **An exchange** (`Ransoms.HearExchange`, `RansomMath.ExchangeChance`): offered by letter (topic `exchange:OURS:THEIRS`),
  from the captive's, the held colonist's or the people card, or face to face (`OutsiderAct.OfferExchange`,
  `ExchangeActs`); agreed, their party walks in with the colony's own beside them (spawned free and walked to the gate),
  and at the gate the warden walks theirs out (`dip:exchanged`).
- **The trap** (decision 5): with a hostile people only, on an exchange at the gate (a captive paid for, or exchanged),
  `RansomMath.TrapChance` is the peace talks' disaster weight 0.05 times the game's own curve for the negotiator's
  ability (4 with none, 1 at 100%, 0.4 at 150%); sprung, `Raids.Send` at 0.35 of the threat points from the map edge
  nearest the gate, and their deed `trapped` (its own since 2b.3, apart from a debt refused, `brokeWord`). Every letter that can bring it names the chance in words before the
  choice (`RansomWords.Odds`), and so does the captive's card.
- **Their envoy** (`Envoys`): once a day a people at peace (goodwill 30), whose envoy notable is free, may offer them
  (`EnvoyMath.MayOffer`: a year apart per people, a season apart per home, one stay per home; one day in thirty),
  `ChoiceLetter_EnvoyOffer`; or the colony asks (`LetterPurpose.Envoy`, `EnvoyMath.SendChance` by their head's
  regard). The notable's own pawn and an attendant (a light fighter of theirs) walk in with food enough for the season
  (`Hosting.Provision`) and stay as `LordJob_EnvoyStay` guests (`LordToil_EnvoyStay`: `Rimfolk_EnvoyStay` about the
  common rooms by day, `Rimfolk_EnvoyIndoors` in danger; their doors open). The hour feeds and beds them by the
  missions' hosting rules (`Hosting.Meal`, `Eat`, `Sleep`, `SleepRough`, `GetUp`), in the bed the player set aside
  (`Envoys.ChooseBed`) or a spare one or the floor, tallies each day (`EnvoyMath.Day`, `Night`, `Talks` from the
  outsiders' own pair warmth, gifts from their visit, `SatIn` at a council meeting in session, `Hurt` sheltered or in
  the open) and reads the mood by the score a day (`EnvoyMath.Mood`). Every five days their runner comes to the gate
  (`LordJob_Messenger` with the home's key) and the envoy walks out with their letter (`JobDriver_EnvoyLetter`), which
  moves their head's regard a little. At the end (`Envoys.End`): their head's regard by the mood, `envoyHonored` (12,
  a debt of honour, for a very good stay) or `slighted`, `dip:envoy`; seen off early, a slight; dead at the colony, a
  little or a lot of regard by whether it sheltered them; hurt by the colony, they flee.
- **The colony's envoy** (`EnvoysAbroad`): a colonist with Social 6 or more, picked from the people or town card,
  after the council's question; they take food that keeps from the stores (`JobDefOf.TakeInventory`), walk to the edge
  (`JobDriver_EnvoySetOut`) and leave as a caravan of their own (`CaravanExitMapUtility.ExitMapAndCreateCaravan`, then
  `StartPath` to the town); at the town they leave the caravan and are kept in the world (`KeepFor.Lent`); every five
  days a letter with the town's news (`Facts.For(FactScene.Letter(...)).news`) and its notables' regard up by
  `EnvoyMath.AbroadRegard`; at the season's end their hosts provision them, a head who thinks well of the colony gives
  a parting gift (about 200 silver's worth), and a caravan of theirs takes them home (`CaravanArrivalAction_Enter`),
  where they learn the peace talks' 6,000 Social.
- **What the player sees** (`RansomPanels`): the captive's card and the held colonist's (an offer in a sentence, the
  trap's odds, "Offer for ransom", "Offer an exchange", "Answer their offer", "Call home"); their envoy's Person card
  (the stay, the bed, last night, letters; "Set aside a bed", "See them off"); the people card and a town's card
  ("Ask for their envoy", "Send an envoy", what is under way), the Peoples view's rows and acts (`DiplomacyModel`),
  Here and away (their envoy here in `WhyHere`, the colony's envoy away in `AwayRows`), the grudge's tie card; the
  inbox: `ransom` and `envoy` in Needs you (an offer for a captive, a demand, their envoy's offer, a bed for their
  envoy; counted through `Inbox.Count`), `ransom-under-way` and `envoy-under-way` in Under way, in steps. Facts
  (`FactsRansom.cs`): `ransom:theirs`, `ransom:held`, `them:wascaptive` with `{CAPTOR}`, `envoy:here` with `{ENVOY}`,
  `them:envoy`, `envoy:ours` with `{OURENVOY}`. Words by both ages (`AgeFeature.Ransom`, `Envoy`). Settings
  `diplomacyRansom`, `diplomacyEnvoys`.

#### Feasts with guests

Release 2b.1 (docs/design/stage2-diplomacy.md, item 5; decision 7): `Diplomacy/`, its rules pure and tested
(`FeastMath.cs`; `Tools/Tests/FeastTests.cs`).

- **The record.** `GuestFeast` (`FeastBook.cs`; the name `FeastPlan` is the play-outs' enum) on the home's part,
  `DiplomacyHome.feast`, one at a time, and `lastFeast` kept for its card; `feastHeld` (one a season,
  `FeastMath.MayPlan`), `feastSuggested`, and `tales` (the colony's own at a neighbour's feast, four at most). Each
  `FeastGuest` keeps the notable, their pawn, people, town, letter, answer (`GuestState`), when they can come, their
  retinue (one or two), their hope (`FeastHope`) and their welcome; each `FeastResult` a contest's winner, side and count.
  The stages (`FeastStage`): Inviting, Set, Here, Contests, Feasting, Night, Done. The hour rides on diplomacy's (only
  while one waits), the day's look on the guests' own group every 250 ticks (`LordToil_FeastGuests` calls
  `Feasts.Rare`), so nothing ticks while there is no feast.
- **Planning** (`Dialog_Feast`, `Feasts.Plan`): up to three notables (`Feasts.InviteWhyNot`: at peace, regard 0 or more,
  `NotableMath.TravelDays` 3 or less, free to travel, a letter possible; the letters under way at once counted), up to
  four contests (`FeastContests.WhyNot` by what the colony has), a prize from the stores (art, or a weapon or clothes of
  good quality, set aside forbidden until the feast), the writer (the steward, else `NeighbourLetters.DefaultWriter`).
  Each invitation is the existing `LetterPurpose.Invitation` with the topic `feast:HOME` (`FeastMath.Topic`), by
  console where the colony and their people can call. Opened from the people card ("Hold a feast for them"), a
  notable's ("Invite to a feast"), the Peoples view's act, the calendar's feast row (`FeastPanels.CalendarRow`, beside
  election day) and the steward's question (`ChoiceLetter_FeastSuggest`, `FeastMath.Suggests`: a season apart, fed,
  quiet, a notable at 20 or more within reach, one day in twenty).
- **Their answer** (`FeastLetters.Read`, hooked in `NeighbourLetters.Read` after diplomacy's own): `FeastMath.Accepts` by
  their regard, goodwill, the road and whether their hope is offered; yes, the tone is cordial at worst and the letter's
  visit line says they will come (`Feasts.Answer`, `comeBy` = their answer's way back and half a day); no, cool at best.
  Once no invitation is still open, the day is set (`FeastMath.FeastDay`: the day the last can arrive if by noon, else
  the next), told in a letter and shown on the calendar, This week's Coming and the Event card.
- **The day** (`Feasts.Arrive`, `LordJob_FeastGuests`): from 8 to 15 on the day or the two after, when fine
  (`FeastMath.Fine`) and safe, each guest (the notable's own pawn) and one or two of their lighter fighters
  (`Arrivals.Make`) walk in, a group to each people, to the gate (`PlaySpots.GateCell`), provisioned for two days;
  a colonist near them greets them (`feastwelcome`, `feastarrive`). The group keeps about `focus` (the gate, a contest,
  the feast's spot, a room for the night: `Feasts.Focus`), indoors in danger, and flees if the colony harms them
  (`Trigger_PawnHarmed`; a bout's or a brawl's blow is excused by `Patch_OutsiderBoutLord`). Meals and beds by the
  missions' hosting rules (`Hosting.Meal`, `Eat`, `Pack`, `Sleep`, `SleepRough`), Hospitality's guest beds first.
- **The contests** (`FeastContests`, one at a time from noon; an outdoor one waits for fine weather and is let go from
  18, nothing new from 20): wrestling, up to three bouts in a ring by the gate through the outsiders' own bout
  (`Outsiders.FeastBout` in `FeastBouts.cs`: no asking, the hard blow decided by `FeastMath.HardBlow`, one in 25, never
  when either may not be hurt: a child, with child, an injury in the last three days); a race, runners from a line at
  the gate to a marker out on open ground and back (`JobDriver_FeastRun` at a sprint, one stumble in twelve); a hunt,
  safe game marked (`PlayHunt.ChooseGame`, the calendar hunt's rules) and the game's own `JobDefOf.Hunt` for the
  colony's hunters and the guests with bows or guns, counted by `RecordDefOf.KillsAnimals`; a game at the tables, the
  game's own play job of each table's joy giver (sit adjacent, interaction cell, billiards, watch building) with
  `doUntilGatheringEnded` so a guest with no recreation need plays on, won by `FeastMath.TableWin` on the play job's
  own skill; a shooting match only at a mod's target read by name (`FeastMath.IsTarget`), five shots each
  (`JobDriver_FeastShoot`: the weapon's own sound and flash, hits by `ShotReport`). Children run races only. The guests
  and a few free colonists watch (`Council_PlayOutAttend`). Each result goes into a message and the history
  (`dip:contest`); a reload during one lets it go as broken up (the run isn't saved).
- **The feast** (`Feasts.StartFeast`): a play-out of the kind Feast through the one feast engine (`DilemmaEffect_Feast.Begin` with `FeastWhy.Guests`, a cup raised to the guests; guests
  `PlayGuests.Feast`), so the colony's gathering serves everyone, the guests included, by the feast's serving rules;
  with answers not playing out, each guest is served at the table by the hosting rules. The Empire's guests hope for
  music: where the colony has an instrument, its best player plays (the game's `Concert` gathering, with Royalty). At the
  end the winners speak (`feastwinner`), the champion of the day is named (`FeastMath.Champion`) and the writer carries
  the prize to them (`JobDriver_FeastPrize`; a guest puts it in their pack). After it, one guest night in 15
  (`FeastMath.SourNight`, more with a hot temper) turns into the outsiders' fistfight with a colonist near them
  (`Outsiders.FeastBrawl`), only with the deeper hooks.
- **The end** (`Feasts.End`, at first light or a day and a half on): each guest's welcome (`FeastMath.Welcome`: a meal,
  a fine one, the hope met, the contests, the prize, a bed or the floor, a hard blow, a sour night) into their regard and
  their head's (half), the deed `feasted` (new, Favor 8, 12 for a great welcome; counts toward a hospitable colony) or
  `slighted` for an empty plate, `dip:feast` (a milestone) and a letter. A guest dead at the colony, arrested or hurt by
  it ends it at once. Called off (nobody coming, the weather or danger two days past the day, the colony's own word
  before they come: 5 regard from each who said yes).
- **For release 2b.2's weddings** (`Feasts.Host`): guests who already agreed (a people, their notables, the purpose
  `FeastPurpose.Wedding`, a day, a caller's `tag`) come on the first fine day from it with no letters and no contests
  unless given, wait at the gate, sit at the feast in the evening, stay the night and go home at first light, their
  welcome counted the same; a second `Host` takes pawns of their people who aren't notables (kin made by the caller).
  `Feasts.StageChanged` tells a listener when they are at the gate (`FeastStage.Here`) and when they are gone
  (`FeastStage.Done`).
- **The colony's own away** (`FeastAway`): at the missions' invitation, when the colony's guests sit to the end, each
  may win one of their people's contests (`FeastMath.AwayWin` by the skill for it), told in the news once home.
- **Where it shows** (`FeastPanels`): an Event card for each feast (its `dip:feast` entry: where it stands, the guests and
  answers, the contests, the prize, the results and the welcomes; "Call it off"), a notable's card, the people card's
  lines and acts, the Peoples view's rows and act (`DiplomacyModel`), Here and away (`Feasts.HereAs`), the calendar's
  row, the inbox (`feast-coming` in Coming, `feast-under-way` in Under way, in steps). Facts (`FactsFeast.cs`):
  `feast:invited`, `feast:soon`, `feast:here`, `feast:contests`, `them:feastguest`, `feast:welcomed`, `feast:slighted`,
  `feast:good`, `feast:poor`, `feast:theywon`, `feast:wewon`, `town:lately:dip:feast`, `{FEAST}` and `{CHAMPION}`.
  Words by both ages (`AgeFeature.Feast`; `{FEAST}` the colony's word with "the"), their faith's feast with Ideology
  (`InviteForms.Rite`). Setting `diplomacyFeasts`. Dev actions in `FeastDebug.cs`.

#### Marriage alliances

Release 2b.2 (docs/design/stage2-diplomacy.md, item 6; decisions 8 and 9): `Diplomacy/`, its rules pure and tested
(`MatchMath.cs`; `Tools/Tests/MatchTests.cs`). Reshaped by the player's decision of 5 November 2026 (the colony is the
family, never a dynasty): a marriage alliance is a bond between the colony and their people made through one marriage;
no child keeps it, their people are their people (never in-laws), their gift comes from their town, and a grudge for a
spouse wronged is their people's, handed down no line.

- **The records.** A match under way is a `MatchPlan` (`MatchBook.cs`) on the home's part, `DiplomacyHome.matches`
  (`MatchMath.MaxPlans`, three); the marriage itself is the ledger's `Tie.Kind.Marriage` of origin `match`, from their
  people to the colony, about the colony's own (`terms.about`), its key `spouse:` and their spouse's ref (the key
  Diplomatic Marriage+'s view writes, so both read alike), where they live in `terms.goods` (`colony`, `theirs`), the
  last kin's gift day in `terms.number`, held through a child in `terms.count`. Three marriages with a people at most
  (the ledger's own bound between two). The stages (`MatchStage`): Asking (the council's question), Agreed, Here, Away,
  then Wed (the tie made, the plan let go) or Off. The hour and the day ride on diplomacy's; nothing ticks otherwise.
- **Three ways in.** "Speak for a match" (`OutsiderAct.SpeakMatch`, `MatchActs.cs`, in the menu after "Speak of a
  pact"): whom of the colony's at the home and where they would live, each with how the colonist feels; their answer
  face to face (`matchask`, `matchtalk`, `matchyes`, `matchno`). The letter purpose `LetterPurpose.Match`
  (`MatchLetters.cs`, through `DiplomacyLetters`): topics `match:OURS:THEIRS` (the notable written to, another of their
  town) or `match:OURS:h:NOTABLE:G` (one of the town that head speaks for, made by `Matches.MakeOfTown` only when they
  agree: their people's kind, a name of their own, the sex the colonist is drawn to, an age near theirs, nobody's child
  or kin; a son or daughter of the head's house until 5 November 2026, the h kept in the topic), each with the
  colonist's feelings; what is asked is where they live (`home:Colony`,
  `home:Theirs`). Their offer (`Matches.Offers`, the day's look; `ChoiceLetter_MatchOffer`, `Rimfolk_MatchOffer`):
  `MatchMath.MayOffer` (their head's regard 40, a year apart by the head's `Last(LetterPurpose.Match)`, fewer than three
  marriages), one day in fifteen once it may, the colonist their head thinks best of who fits one of theirs (a titled
  one for the Empire's titled), where they live by `MatchMath.TheyPropose` (an heir stays home).
- **Who may marry** (`Matches.WhyNotPair`, `MatchMath.WhyNot`): both grown (adult, 18), each drawn to the other's sex
  (`RelationsUtility.AttractedToGender` both ways), not kin (the game's own romance factor of a relation), each free to
  take one more spouse (unmarried, or with Ideology `GetHistoryEventForSpouseAndFianceCountPlusOne` willing), not
  engaged elsewhere, never their people's leader, and the Empire's titled only to a titled colonist (decision 9).
- **The colonist asked first** (`Matches.ConsentOf`, `MatchMath.ConsentOf`): against it while they love someone else of
  the colony (a lover or fiancé), would leave children under 18 behind to go to them, or think -10 or less of the one
  chosen; glad with a lover or a regard of 30; else willing. Their yes to a match the colonist is against begins it at
  Asking: the council's question `Rimfolk_DlmMatch_Against` (`MatchObjection.cs`, A the colonist, X theirs, B whom A
  loves, `{LIVES}`), asked when no other question is open at the home, else the hour after; pressed, the match is
  agreed with `Rimfolk_MatchPressed` (-6 for 20 days, a mood hint), trust -3 and `Nature.Felt(MatchAgainstHeart)`;
  withdrawn (the default), their regard -3. A colonist who comes round meanwhile is agreed without it.
- **Their answer** (`Matches.Hear`): `MatchMath.Accepts` by their head's (or the house's) regard, never below 0, much
  likelier for two lovers, less when theirs would move, more for an ally; no costs a little regard.
- **Agreed** (`Matches.Agree`): the game's fiancé relation (a Lover's made Fiance; the name change rolled by the game's
  own fiancé worker), the day their kin come (`MatchMath.LeadDays`: their road and a day), gifts both ways at the gate
  (`Couriers.Exchange`: theirs by their ways, `Matches.GiftDef`, a tribe's jade, gold for the middle ages and the Empire,
  else silver; the colony's silver up to the same worth, `MatchMath.GiftValue` by their town's fortune), and a letter
  that names what the alliance gives and the wrongs it can't bear.
- **Theirs comes to the colony** (`MatchDays.cs`): `Feasts.Host` with up to two of their notables of that town as the
  wedding's guests (`FeastPurpose.Wedding`, tag `match:ID`); at `FeastStage.Here` (`Matches.OnStage`, subscribed in
  `MatchHooks`) the one to be married walks in beside them and joins the colony at once (`Arrivals.Join`), the council's
  own wedding question marked asked for the pair; the game's ceremony (`TryStartMarriageCeremony` when
  `AcceptableGameConditionsToStartCeremony` and both are ready, 5 to 16, with the guests here only before the feast's
  hour: `MatchMath.CeremonyHour`) is tried each hour, the guests standing by the couple while it runs, its party after
  the vows too (`Feasts.HereFocus` → `Matches.WeddingSpot`), and the feast waiting until it is over: the ceremony and
  the feast each let no other gathering begin, so begun under it the feast would wait and fall through. Nobody to
  host, or the feast let go before they came: they walk in alone on a safe hour. Not married within three days, the
  council's own wedding plan (`WeddingPlans.Plan`, quiet); not within eight more, the match lapses.
- **Ours goes to them**: on the day, by daylight and with ours at home, the one they marry and one of their kin walk to
  the gate (`LordJob_MatchEscort`); the colonist walks out (`JobDriver_MatchLeave`), and their group's own tick takes
  them along (`Matches.TakeLeave` → `Departs`: `matchfarewell`, `matchescort`, `SetFaction` to theirs, kept with the new
  `KeepFor.Spouse`); not sent within ten hours, their kin go home and the colony's word is broken. Off the map, after
  the road (`MatchMath.RoadDays`) the game's spouse relation is set and their letter tells of the wedding at their town;
  the colonist becomes a face of their town (`NotableBook.Notify_Met`, "spouse"), both kept while the marriage holds.
- **The wedding** (`Matches.Wed`, from `Patch_Married` or the hour's look): the Marriage tie (its `tie:marriage:made` a
  milestone), the deed `married` (15), `dip:wedding` (a milestone), and a defense pact (`Pacts.Make`) unless one stands
  or the people is the Empire.
- **While it holds** (`MatchAlliance.cs`): their notables' regard +15 (`Matches.RegardBonus` in
  `NotableBook.Baseline`, cached by the ledger's version; Rimfolk's own ties only, never Diplomatic Marriage+'s); their
  town's gift once a season by their courier (`MatchMath.TownGiftValue`, the town's fortune; the words name the town,
  `CC_Match_KinGift` rewritten in place); their letter warning of a raiders' camp hidden two
  days or more (`RaidersCamps.WarnedByKin` in `MatchWarnings.cs`, a partial of the camps: the camp found by a letter);
  the alliance's pact kept running (moved on within five days of its year's end), made again a year after the last one
  closed; their calls on the colony are the pact's.
- **Ends and bad sides**: a spouse's death ends it, a child of the two or not, with no grudge unless the colony was to
  blame (`MatchMath.GrudgeAtDeath`: killed by its hand); an older save's alliance held through a child (`terms.count`,
  set no more) ends at the day's look without a word; one of theirs dying at the colony costs their notables 3, or 15 in the open or untended (`Patch_MatchKilled`,
  with the deeper hooks, reads it before the death); the couple parted (no spouse relation) ends it, -10; theirs gone
  home of their own accord ends it with nobody at fault; a spouse wronged (`MatchMath.WrongOf`: killed by a colonist,
  executed, held as a prisoner or a slave, sold, banished) breaks it, `spouseWronged` (30), -30 with their head and
  notables, their defense pact broken without a deed, and a Grudge (origin `match`, key `spouse`, their people in its
  count) held by their people against the colony, which their head speaks for and which is handed down no line; an older
  save's held by their head (taken up and passed to the next head until 5 November 2026) is made their people's, quietly,
  at the day's look (`Matches.PeoplesGrudges`). A match broken off by the
  colony (or ours never sent) is `slighted`, its word broken in reach, -12.
- **Where it shows** (`MatchPanels`): the Tie card (who, where, since, what it gives, the pact; a grudge's holder), a
  Person card (a match under way, a marriage, who of the colony's would fit; "Propose a match"), the People card (rows,
  "Seek a match", "Write to their people"), a colonist's card, the Peoples view (`DiplomacyModel`), Here and away (the
  colony's own married away, on the road; the peoples the colony is bound to by marriage, `MatchPanels.BondRows`, where the
  in-laws among the houses were; the party at the gate, `Matches.HereAs`), the inbox (`match` in Needs you, `match-under-way` in steps,
  `match-coming` for their kin's day; the wedding's guests have the feast's own row). Facts (`FactsMatch.cs`):
  `tie:marriage`, `:ours`, `:theirs`, `:you`, `match:agreed`, `match:wronged`, `town:lately:dip:wedding`, `{INLAW}`,
  `{SPOUSEOF}` (`match:child`, held through a child, is set no more: its pieces and lines went with it, and the shipped
  pieces and lines that make their people the colony's family or in-laws, or hand a grudge down, are retired in
  `OutsiderTalkMath.Retired`, `RetiredLines` and `ShippedLines.Retired`, their better words in
  `Rimfolk_OutsiderTalks_ColonyBonds.xml` and `Rimfolk_Lines_ColonyBonds.xml`). The match's word by the colony's age is a
  match between clans, a marriage pact between two peoples, a marriage between two settlements, a partnership contract
  between settlements (`CC_Form_Marriage_*`). Words by both ages (`AgeFeature.Marriage`), their faith's wedding with Ideology. Setting
  `diplomacyMarriage`. Dev actions in `MatchDebug.cs`. Diplomatic Marriage+ is read only: its alliance a row as the mod's.

#### Wards and hostages

Release 2b.3 (docs/design/stage2-diplomacy.md, item 7; decisions 9 and 10): `Diplomacy/`, its rules pure and tested
(`WardMath.cs`; `Tools/Tests/WardTests.cs`). Reshaped by the player's decision of 5 November 2026 (the colony is the
family, never a dynasty): their ward is a youth of their town, never of a notable's house; the colony's own youth goes in
return as before; the parents' feelings at home stay, being personal. And by the decision of 6 November 2026: their
hostage is anyone of standing among their people, never by kinship (until then one of their notable's kin, their child
where the ages fit).

- **The records.** Each `DiplomacyHome` keeps its wards and hostages, either way (`WardStay`, `WardMath.MaxPerHome` = 4:
  one ward each way, one hostage each way per truce or pact), and when a ward was last offered (and by whom). The bonds
  are the ledger's own ties: a Ward tie from whoever sent the youth to whoever keeps them (origin `ward`, key the skill,
  about the youth), a Hostage tie from whoever gave them to whoever holds them (origin `hostage`, about the hostage, its
  count the truce or pact it guarantees; called in when that is broken). Their ends are reached in diplomacy's hour, not
  the ledger's until. Keep reasons `KeepFor.Ward` (their ward agreed and not yet come; a ward fostered and gone home,
  while their Regard tie lives) and `KeepFor.Hostage` (their hostage agreed and not yet come); the colony's own away is
  `KeepFor.Lent`, as its envoy is. History `dip:ward` and `dip:hostage` beside the ties' own `tie:ward:*`, `tie:hostage:*`.
- **The game's lodger** (`Wards.StartQuest`): a quest of Rimfolk's own (`Rimfolk_LodgerQuest`, in the Quests tab) with
  the game's `QuestPart_ExtraFaction` (`ExtraFactionType.HomeFaction`, their people), added before `QuestManager.Add`
  (whose list of extra-faction parts is filled there and on load) and accepted from the start (`SetInitiallyAccepted`),
  as the refugee hosting quest does with its own part: `IsQuestLodger` holds, `HomeFaction` is their people, the game's
  arrest, banishment and death rules for lodgers apply. No part of Rimfolk's own is in it; the terms are the record's.
  The youth joins at the gate by `Arrivals.Join` and leaves by the game's own `LeaveQuestPartUtility.MakePawnLeave`, or,
  walking out alone (`JobDriver_WardLeave`), as the colony's lodger to the edge and out (`ExitMap`, their people's again
  as a world pawn), so a hostage of a people at war is never shot on the way.
- **Their ward** (`Wards`): once a day a people at peace whose head thinks 30 or more of the colony, while a colonist has
  10 in some skill, may offer (`WardMath.MayOffer`: a year apart per people, a season per home; one day in forty;
  `ChoiceLetter_WardOffer`); or the colony asks (`LetterPurpose.Ward`, topic `ward:ask`, the skill asked
  `skill:Crafting`; `WardMath.SendChance` by their head's regard). Agreed, a youth of their town is made
  (`Wards.MakeYouth`: 9 to 12 with Biotech's children, else 14 to 19; a name of their own, nobody's child) and kept in
  the world, sent by their head, heir or envoy (`Wards.Sender`, the town nearest the home first; the offer names the
  youth's town, `CC_Ward_OfferTown`); one or two days on, they walk in with one of their own family (`LordJob_WardParty`, guests of a people at peace with
  both for a people at war) and join at the gate. A guardian is the best with the skill (`Wards.Guardians`, 10 or
  more; "Pick a guardian" on the card). Once a day, by day, the lesson waits for both to be free and together
  (`Together.When`, the ward walking over in free time): `JobDriver_WardLesson` and `JobDriver_WardTeach`, an hour at
  `WardMath.LessonXpPerTick`; a child with Biotech learns at the game's lessons instead. Each day is tallied (mood,
  hunger, new injuries); the card shows how they are (`WardMath.Mood`: homesick, settling in, happy). At the year's end
  two of their kin come to the gate and the ward walks out to them (`JobDriver_WardToKin`; at war, or if the kin can't
  come or leave, the ward walks out alone), and the year is reckoned (`WardMath.Year`): their head's and the sending notable's
  regard, goodwill, the deed `fostered` (a fine year, 12, a debt of honour) or `slighted`, the tie kept or ended, and a
  Regard tie of the youth's toward the colony that the grown ward keeps (`Wards.Grown` when they become a notable;
  `Wards.HeirFor` lets one who had a good year stand as their town's heir at 18). Dead in the colony's care: regard by
  whether it sheltered them; at a colonist's hand (`Patch_WardKilled`, with the deeper hooks) a feud, a Grudge tie
  (key `ward`). Carried off, arrested or sent away: a slight.
- **The colony's youth** (`WardsAbroad`): offered by letter (topic `ward:send`, the youth `youth:ID`; 14 to 19, 10 to
  19 with children); taken in, they take food that keeps from the stores, walk to the edge (`JobDriver_WardSetOut`) and
  go by caravan to their nearest town, kept there as lent; a letter every ten days by their people's age (the town's
  notables' regard up); home after a year with `WardMath.AbroadXp` in their people's skill (`WardMath.TheirSkills` by
  age), a friend among the notables, and their people's deed `taught`. The Empire takes them as a page in a titled
  household: regard only. "Call home" brings them back sooner.
- **Hostages** (`Hostages`): a truce's price (`TrucePrice.TheirHostage`, `OurHostage` with the colonist's id in the
  terms' count) or a defence pact's term (the letter's ask `hostage:Theirs`, `hostage:Ours:ID`; the pact's roll moved
  by `WardMath.PactRoll`), adults only (`WardMath.MayBeHostage`, 18). Theirs is one of standing among their people
  (`Hostages.MakeHostage`): one of their traders (the kinds their caravans bring, `TraderKinds`) or of their seasoned
  fighters (the stronger half by the game's combat power of the kinds their war bands bring, `FighterKinds`,
  `WardMath.Seasoned`), never a leader's own kind or one that asks a royal title, whichever their people have and by a
  roll where they have both (`WardMath.Standing`); one of their people where they have neither. Made by the game's own
  generation with that kind, 20 to 50 (`WardMath.StandingAge`), a name of their own and nobody's child; sent and answered
  for by their head, heir or envoy (`Wards.Sender`, the record's `notable`; with nobody the colony knows,
  `CC_Hostage_Why_NoSender`). The record keeps what they are (`WardStay.standing`; `Unknown` for a hostage given before,
  who stays as they were), and the terms' answer (`CC_Hostage_Given_TheirsStanding`), the letter at the gate
  (`CC_Hostage_Arrives_Standing`), their card and the talk (`them:hostage:trader`, `:fighter`) say it. They come some hours
  after the oath or the pact (at war only where `TruceParley.Escort` finds someone to vouch); the colony's sets out as
  its youth does. The hour reads the term from the tie it guarantees (`WardMath.EndOf`): over, both go home (their
  hostage walks out, `hostageReturned`), and a defence pact's hostage goes home at the end of the pact year they were
  given for (`WardStay.until`, noted at the giving), even when the pact is renewed by their letter or by a marriage
  alliance; their renewal letter may ask for a new hostage either way as a term of the next year; their side broke the truce, theirs is forfeit (`ChoiceLetter_HostageForfeit`:
  home anyway, `forgave`; kept, a warden's `JobDefOf.Arrest` and then their people's prisoner, ransom applying; asked to
  stay, if `WardMath.WouldJoin`; unanswered five days, they slip away) and the colony's comes home; the colony broke it,
  theirs goes home at once and the colony's is held (`KidnappedPawnsTracker.Kidnap`: the game's captivity, its ransom
  demand, rescues, and joining them in time).
- **What the player sees** (`WardPanels`): the colonist's card of a ward or hostage of theirs (the year so far, the
  guardian, lessons, the skill's level, the bed, hunger and hurts; "Pick a guardian", "Send home early"; "Decide" on a
  forfeit hostage) and of the colony's own away ("Call home"), a guardian's card, a grown ward's Person card, the people
  card ("Ask for a ward", "Offer a youth" or "Offer a page"), a town's card, the Ward and Hostage tie cards; Peoples'
  rows, Here and away (`Wards.HereAs`, `WardPanels.AwayRows`); the inbox: `ward` in Needs you (their offer, a forfeit
  hostage, a ward without a guardian), `ward-under-way` in steps. Facts (`FactsWards.cs`): `tie:ward` (`:theirs`,
  `:ours`), `tie:hostage` (`:theirs`, `:ours`, `:due`), `them:ward` with `them:ward:homesick`, `:settling`, `:happy`,
  `them:hostage`, `them:fostered`; `{WARD}`, `{HOSTAGE}`, `{GUARDIAN}`. Words by both ages (`AgeFeature.Ward`,
  `Hostage`; the ward's rites say the ward's side only). Setting `diplomacyWards`.

#### The keeper of secrets and council tasks (dropped)

Release 2c.1 (0.96.0, docs/design/stage2-diplomacy.md, item 8) gave eight offices a task with a neighbour and the council
a keeper of secrets. Both were dropped by the player's decision (5 November 2026), with the rule that the colony is the
family, never a dynasty: no office takes a task, there is no keeper's seat, and the colony's own secrets slip at their
base rate again (`SecretTracker.Step`, as before 0.96.0). Fealty and vassals run without the quartermaster's dues (below).
What an older save still holds (`Diplomacy/Retired.cs`):
- **Left unread.** A home's `tasks` (`CouncilTask` records), `drillPeople` and `drillAt` on the diplomacy book, a vassal's
  `duesAt`, the setting `diplomacyTasks`. Scribe passes over a node nothing reads, so no type is needed for them.
- **Let go quietly.** The keeper's seat: `Offices.ExposeData` drops a holder or an election's winner whose office no
  longer exists, and `ElectionRace` reads its office with `CouncilScribe.LookDef` (a missing def loads as none) so the
  election drops the race. The holder's hediff and the marshal's patrol job are read by the game by name, so their defs
  stay as stand-ins (`Defs/Misc/Rimfolk_Retired.xml`): `HediffComp_Retired` removes the hediff at its next health tick,
  `JobDriver_TaskPatrol` ends at its first look. A letter of the council's business (`LetterPurpose.Task`, kept in its
  place in the enum, `NotableMath.Retired`) not yet read is called off without a word (`NeighbourLetters.Step`); an
  answer already on its way back comes as it was written. A sounding out under way (`OutsiderAct.SoundOut`, kept in its
  place: the talk job keeps its act by number) ends at once (`OutsiderMath.Retired`, `JobDriver_OutsiderTalk`). The
  keeper's Secret ties (origin `keeper`) end at the day's look (`Retired.Daily`), a pressed one too.
- **Kept as it is.** An officer's visit planned or on the road goes on as the colony's own journey (the journey planner's
  plan there and back); a favour won by letter keeps its words (`CC_Dip_For_wonOver`); the history's `dip:task` lines and
  the notables' memory lines stay as text, and `dip:task` gives no renown. `dip:task` is still written for a vassal's ask
  answered (`Vassals`).
- `Tie.Kind.Secret` stays reserved for schemes (release 2c.3) with its rules and bounds; nothing makes one now.

#### Fealty and vassals

Release 2c.2 (docs/design/stage2-diplomacy.md, items 9 and 10; decisions 9, 11 and 12): `Diplomacy/`, its rules pure and
tested (`FealtyMath.cs`; `Tools/Tests/FealtyTests.cs`).

- **The liege.** A Fealty tie from the colony (`Diplomacy.Colony(home)`) to the people, origin `fealty`: key `liege`
  (`liege:failed` after a season they failed to protect it, read with its day as `Fealty.FailedSeason`), number the day
  the next tribute is due, count the silver still owed (the tie called in while it is), days the day their band last
  came (or failed), goods a call under way (PactMath's `band:` and `arms:` codes), about the enemy of that call. One
  liege a home. A scenario's grant of land from the same people (`ScenPart_CouncilBeholden`, origin `granted`) ends
  when the colony swears anew. Tie history `tie:fealty:made` (a milestone) and the rest by the ledger.
- **Swearing.** `Fealty.SwearWhyNot`: at peace, not the Empire, goodwill 40, two towns in reach, no liege, no oath
  awaited. "Swear fealty..." (`FealtyPanels.SwearAct`) shows their terms by their ways (`FealtyWords.Terms`), the bad
  sides (`Risks`) and who will object, then opens the letter dialog with the purpose `LetterPurpose.Fealty` (after Task
  in the enum; `FealtyLetters`, topic `fealty:swear:ID`). Their answer (`Fealty.Hear`, `FealtyMath.Accepts`) sets the
  oath awaited on the home's part (`DiplomacyHome.oathPeople`, `oathDue`, `oathAt`, `oathOffered`). Their offer
  (`Fealty.Daily`): a home raided twice in the last season (the memory's `raided` deeds) with five fighters or fewer, a
  quarter of the days, once a year a home (`fealtyOfferAt`): `ChoiceLetter_FealtyOffer`.
- **The oath at the gate** (`FealtyOath.cs`): by day with no threat, their head when free to travel (else one of their
  people) and a guard walk in (`LordJob_FealtyOath`: to the gate, wait 20 hours and 12 more, leave on a memo; harmed,
  they run, the deed `attacked`). Each hour at the gate an objector (`FealtyObjection.Objector`: an avenger
  (`TruceObjection.Avenger`), pride 30 or more, a devout colonist of another faith with Ideology) has the council's
  question first (`Rimfolk_DlmFealty_Swear`, tags avenger/proud/faith/pet); then the speaker
  (`TruceParley.Speaker`) walks out (`JobDriver_FealtyOath`: the bubbles `fealtyoath`, `fealtyterms`, `fealtysworn`)
  and `Fealty.Swear` makes the tie, notes `swore`, calls the nature service (`Nature.Felt`: the speaker's pride, since
  release 2d.2) and tells the terms.
- **The hour** (`Fealty.Hourly`, from `Diplomacy.Hourly`): oaths awaited; for each oath, gone or at war ends it; their
  band on its way walks in through `Pacts.Arrive(..., "fealty", words "fealty")` and notes `protected`; protection
  (`ProtectionHour`): a home raided by a people (not the liege) with no band on the map, once a season
  (`FealtyMath.MayProtect`), one roll a raid and day: their band (`Protects`: nine in ten, never against their friends)
  or a failed season told; the levies (`CallsToArms.PactHour` with the tie: its letter's words `CC_Arms_Fealty`, the
  far fight as a pact's, kept as `Fealty.LeviesCame` (deed `levies`, `dip:levy`), refused or not sent as
  `Fealty.LeviesRefused` (10 goodwill, deed `refused`, `dip:levy`; the oath stands)); the season's tribute
  (`TributeHour`, from 9 to 16 with no threat): still owed from the last breaks the oath (20 goodwill, `tributeShort`);
  else `CollectTribute` carries silver (or the most plentiful goods, two kinds) out
  to their collector at the gate (`DilemmaEffect_CarryOut`, their courier waiting there), the rest owed
  (`tributeShort`, `dip:tribute`) or paid in full (`tribute`, `dip:tribute`).
  `Fealty.PayOwed` sends what is owed the same way ("Send what is owed" on the tie's card and This week).
- **Renouncing** (`FealtyObjection.AskRenounce`, the question `Rimfolk_DlmFealty_Renounce`, tags clean/owed):
  `Fealty.Renounce` ends it with 30 goodwill and `renounced`, nothing within a season of a failure. Attacking the liege
  (PactsHooks: a pact breach) breaks it (`renounced`, the word broken in every ear). Goodwill moves only with the liege
  (decision 11). The Empire takes no oath (decision 9); its titled colonists show on its card as sworn to it
  (`Fealty.Titled`, `WorldMechanics.Title`). [SR] Factional War's battles on a home name the liege among the sides
  (`PactsHooks.BattleBegan`, `CC_War_Liege`), and fighting its enemy there is siding with it (`sidedWith`).
- **Vassals of the colony's own** (`Vassals.cs`): `DiplomacyHome.vassals` (`VassalTerms`, `FealtyMath.MaxVassals` = 3):
  the hamlet once sworn (synced each day from `HamletHome`; its own tie, origin `hamlet`) and up to two protected towns
  (a Fealty tie from the town to the colony, origin `vassal`, key `protected`), each with the council's `TributeLevel`
  and when its harvest, season, levies and last ask were, its head's low regard and release. A struggling town
  (`FealtyMath.AsksProtection`: under 20 for 30 days by its ledger record, its people at peace and at war with another,
  not the Empire's, not a seat, hamlet or post) asks once a year a home (`protectAskAt`). Each day a vassal's season
  (heavy: 3 goodwill and 4 regard; light: 2 regard), a protected town's harvest at harvest time (`ProtectedProduce` by
  level; the hamlet's own harvest through `Vassals.HamletProduce` in `HamletLife`), its head's low regard and one ask a
  season (`FealtyMath.AskOf`: release, a hostile site within 8 tiles, a hard season, a neighbouring town of another
  people at peace) as `ChoiceLetter_VassalAsk` (help carried out to the gate; a judgment moving the neighbour's
  goodwill 5; a camp or an outpost's invasion promised as a Promise tie of origin `vassal`, kept when the site is gone or
  the colony's people are at the outpost, broken at its day in the terms; release: released (the hamlet back to
  standing under protection, `Hamlets.ReleaseOath`) or broken away (15 goodwill; the hamlet's record ended without its
  own letter, `Hamlets.BreakAway`)). Levies: "Call their levies" (the place card, `Alert_VassalLevies`) once a season
  during a threat, their band at a third of the threat (`Pacts.Arrive(..., "vassal", strength 2/3)`), its dead
  weighing on their head (`Vassals.LeviesHome` from `WarCamps.Thank`, 6 a death, 20 at most). Their head's regard moves
  through `Vassals.Regard` (the notable's personal regard and a memory line, shown on the town's card as what moved it).
- **No dues task.** The quartermaster's dues (0.97.0 to 0.101.1) went with the council's tasks (5 November 2026). A
  vassal pays at harvest, its carter bringing produce to the gate by the council's level; the liege's collector takes
  the season's tribute at the gate, and what the stores couldn't cover is paid with "Send what is owed"
  (`Fealty.PayOwed`). Nothing came only through the task but the vassals' season's silver, which the design's vassals
  (item 10) never had: their tribute is the harvest.
- **Faction Territories' vassals** (decision 12): views as before (`WorldMechanics.OurVassals`), with a head made or
  carried over (`NotableBook.StaffOutpost`, the history's `world:vassal` naming the town it was) while their first
  people are at peace with the colony; kept by `Notables.Look` (`Vassals.KeepsHead`), never their people's head
  (`FavourCalls.HeadOf` skips `Vassals.OutpostHead`); their last ask on the head (`Last(LetterPurpose.Fealty)`); an
  invasion read by name (`Territories.InvasionAtOutpost`, `FindForVassalOutpost`). Nothing of the mod's points,
  purchases or tribute is touched.
- **Where it shows** (`FealtyPanels.cs`): the people card's rows and acts (`DiplomacyModel.Card`), the tie's card,
  the place card's vassal lines and acts (tribute level, levies) and an outpost's lines, the inbox (Needs you:
  their offer, levies called, tribute owed, a vassal's asks; Under way: the oath awaited in steps, their band, the
  colony's levies on the road, a vassal's levies; Coming: tribute expected within three days with its date), the Ours
  lens's gold marks and lines to the mod's outposts (`LensModel`). Facts (`FactsFealty.cs`): `fealty:oath`,
  `fealty:levies`, `fealty:protected`, `fealty:short`, `vassal:town`, `vassal:heavy`, `vassal:levy` beside the
  ledger's `tie:fealty`. Setting `diplomacyFealty` ("Fealty and vassals"). Dev actions in `FealtyDebug.cs`.
- **Pacts of their own kind** (the player's decision 14, 6 November 2026: "fealty and vassals: their pages and settings
  fold into pacts; the mechanics stay"). What the player sees is one place for the colony's bonds with other peoples:
  on a people's card the liege's row reads "A pact of fealty with the Ironhold, the colony sworn to them: ..." and a
  vassal's "A pact of protection with Ashford, the colony's vassal: ..." (`CC_Fealty_RowPact`, `CC_Vassal_RowPact`,
  `_RowPactHamlet`, `CC_Fealty_RowAwaitedPact`), among the truces and pacts and right after them; the tie's card is
  named "A pact of fealty" or "A pact of protection" (`CardWords.TieName`: a fealty tie from a town is a vassal's); Needs
  you lists fealty's asks right after the pacts' (order 32.5); the setting is "... and pacts of fealty" under "Pacts and
  truces" in the settings' "Bonds with other peoples" (What's on), and `Fealty.On` reads `Pacts.On` too, so pacts off is
  no new pact of any kind (the oaths and vassals standing run on, as with the switch off); the Almanac's diplomacy page
  tells fealty and vassals as "Pacts of fealty" and "Pacts of protection" and its Pacts section points to them. The
  ties, their origins, terms, the hour, the oath, tribute, levies and the inbox's rows are as they were.

#### Colony renown

Release 2d.1 (docs/design/stage2-diplomacy.md, item 12; decision 17: earned and shown in words, with a "known for",
nothing spent), built as house renown and reshaped by the player's decision of 5 November 2026: the colony is the family,
never a dynasty, so its name among the peoples is the colony's, built from everything its people do, and each colonist
has a name of their own for their deeds. `Diplomacy/`, its rules pure and tested (`RenownMath.cs`; `Tools/Tests/RenownTests.cs`).

- **Read, never stored** (`Renown.Read`, kept for the in-game hour). The colony's renown: every event of the history's
  diplomacy kinds the colony is a party to (`Book.OfKind` of `RenownMath.MemberKinds`), once each, its part by the role of
  the one of the colony's it names (`RenownMath.SourceOf`: the champion of `dip:contest`, only when the colony won it,
  `RenownMath.OursIn`, as a guest may win; the one married in `dip:wedding`; a ward's guardian at a good year's end and the
  colony's youth home in `dip:ward` (`RenownMath.GuardianRole`); its envoy home and its hostage home after the term, named
  as having done it for the colony; the speaker at `dip:parley`; an older save's council tasks (`dip:task`) give none), so
  the living and the gone count alike; what the peoples remember of any of its people by name (`ColonistMemory.AllMarks`:
  a camp broken, fighting beside theirs, trade at a post, settlers walked, a prisoner freed, their wounded tended; blood
  shed at peace, executions, harvests, hard words, brawls); what its living people hold now (`Renown.People`: its free
  colonists and those away on its errand, kept in the world as lent; the colony's names for them by kind, years as
  physician or marshal, terms as speaker, the Empire's title by seniority, notables whose own regard of them is 20 or
  more); and its own deeds at every home (`deed:swore`, `keptWord`, `levies`, `tie:pact:kept`; `brokeTruce`, `brokePact`,
  `renounced`, `schemed`, `refused`, `tributeShort`, `wordBroken`). Each source has its points and its cap, held for the
  colony as a whole; a deed fades (full for a year, a tenth a season to three tenths). Bands: a town at 10, the region at
  30, the peoples in reach at 60, infamous at -15, each held until three under its start. Known for: the kind with six
  points or more and three tenths of the kinds'.
- **Each colonist's own name** (`Renown.Own`, an `OwnName`, kept for the hour): the same sources from what they alone did
  (`History.Of` their ref, the same rule for the colony's own in an event; `ColonistMemory.MarksOf` with the people who
  keep each mark; what they hold), read with the same bands. They are known by name from a town's band (two contests won
  make one) by the part largest among their good deeds, or among their wrongs when infamous (`RenownMath.KnownAs`; a tie
  ordered by the source's name, so the name is the same each time), with the people it was done with or for (the event's
  people, the people who keep the most of that mark) and what it was (a contest's kind, kept since this reshaping as the
  `dip:contest` entry's key; the colony's name for them; the office; the Empire's title; a notable's name):
  `RenownWords.KnownAs`, "the one who won the wrestling at the feast with the Lenlia", "the one the colony calls the
  Healer", "the one who tended the wounded of the Kuvar", one wording a part (a name, the same each time), `_Any` where no
  people is named. Their people know it across the region or wider; at a town's band only the people it was done with or
  for, or anyone for a part that names no people (`RenownMath.TheyKnow`, `Renown.KnownAmong`).
- **Told.** The colony's band, saved on the diplomacy book (`DiplomacyBook.renownBand`; `RenownMath.NotRead` until the
  first day's look, which sets it without a word, so an older save's name, worked out from what it already holds, and a
  new game's are never told on load); a move is `dip:renown` (a milestone, the colony as its actor), `Tell.Minor` with
  five wordings of each kind of move (`Rimfolk_Renown_Colony.xml`), and a bubble from one of its people on the seat's map,
  awake and free, to nobody, only where its words are true (`RenownMath.Bubble`): `renownup` as its name carries further,
  with the colony's tags as it stands now (a town nearby is the next town's talk, never a song or the papers), `renowndown`
  as it fades from the talk, with `colony:wasrenowned` (`RenownMath.BubbleTag`) for a name that had been told across the
  region or wider. A colonist's own name moves quietly: it shows where it is read.
- **What it does.** `Notables.OpinionOf` adds `Renown.RegardBonus` for any of the colony's people (up to 15, down to -10); a
  good word (`OutsiderOutcomes.GoodWord`) counts up to half again; a match is accepted (`Matches.Hear`,
  `RenownMath.MatchChance`) and offered (`Matches.Offers`) up to twice as readily; `Renown.EventFactor` in Rimfolk's own
  event weights and the storyteller's (`Happenings.Weight`, `IncidentWorker_Happening.ChanceFactorNow`) draws
  `RenownMath.Events` by what the colony is known for (a quarter, half, three quarters more by band), or by what one of its
  people at that home is known for by name, a quarter more at most (`RenownMath.OwnFactor`; the best of the two, never
  both); the champion challenges a colonist known by name as a fighter six times in ten (`Renown.KnownFighter`,
  `HappeningWorker_Champion`); a feast's guests answer a colony known for hosting up to three tenths more readily
  (`Feasts.Answer`).
- **Where it shows**, since one name (phase 4 of the review, part 1; "One name" under the technical overview): inside the
  colony's one name and each colonist's. The colony's card (`Character/ColonyName.cs`, on Council → Ways and a home's
  card) says how far its name carries and for what in its first line, with renown's lines below
  (`RenownPanels.From`, `Against`, `Reach`: the deeds it came from and those held against it, who knows its people, what
  it does). A colonist's own name is part of their one "known as" (`Epithets/KnownAs.cs`): "Known as the Healer, the one
  who won the wrestling at the feast with the Lenlia, talked about in the next town." on their card, and People → Renown
  (where People → Houses was, `PagesMath.Moved` sends the old address there; its "?" opens the diplomacy page) lists each
  of the colony's people by it, the band in its header. No acts and nothing in This week (it is earned). Facts (`FactsRenown.cs`, in the ties' provider, when one of the colony speaks or
  writes): `colony:known`, `colony:renowned`, `colony:wide`, `colony:infamous`, `colony:knownfor:KIND`, `{KNOWNFOR}` (by
  the colony's age), `{RENOWN}` (by the listener's ways; the Empire's people by the court's, whatever their tech level, its
  own row of the bands' words: "spoken of with honor in the region's noble houses"); the speaker known by their own name to
  the listener's people, `us:knownas`, `us:knownas:KIND` and `{KNOWNAS}`; bubbles get the colony's tags
  (`Outsiders.Line`). Words: `Rimfolk_OutsiderTalks_ColonyRenown.xml` (83 pieces: the colony's name by band and their
  ways, known for each kind, infamous, not yet known, a colonist's own name, 35 callbacks), `Rimfolk_Lines_ColonyRenown.xml`
  (their letters by band, known-for, infamy and the writer's own name; the bubbles at every band by age; good words),
  `Rimfolk_Renown_Colony.xml`. Setting `diplomacyRenown` ("Colony renown"). Dev actions in `RenownDebug.cs`.
- **The houses, gone** (5 November 2026). The colony's houses (two or more of one family name, `LedgerHouse` records marked
  ours), their House cards, People → Houses, Here and away's houses and the region's houses' renown (a town's founding
  family, its head's place, its fortune and its ties: the sources `founding`, `head`, `leader`, `colonyTie`, `thriving`,
  `struggling`, `skill`) went. An older save's houses of the colony's have no town and are dropped as the ledger loads
  (`WorldLedger.ExposeRegion`); nothing reads their nodes (`ours`, `seat`, `folk`, `folkNames`, `lastSeen`, `band`). The
  towns' founding families stay as their towns' story (the region panel names them, unlinked); a house's name in an older
  history line opens its town's card (`Cards.House`), and one of the colony's shows as gone. The houses' tags
  (`house:...`, `them:house...`) and `{HOUSE}`, `{THEIRHOUSE}` went with them, and so did every talk piece and line said
  only on them (`Rimfolk_OutsiderTalks_Renown.xml`, `_RenownMore.xml`, the letters of `Rimfolk_Lines_Renown.xml` and
  `_RenownMore.xml`, the house news and its fellows); their bubbles that say "our family" are retired
  (`ShippedLines.Retired`), and those that name a band ask the colony's (`ShippedLines.AlsoWhen`). Family names stay as
  everyday life (`Social/FamilyNames.cs`: the Boyds, suppers, couples taking one name), with nothing of renown or
  diplomacy tied to them.

#### Against their nature

Release 2d.2 (docs/design/stage2-diplomacy.md, item 13; decision 19: strain through the heart only, no new mood, easing
when someone acts with their nature, marks for repeated acts): `Diplomacy/Nature.cs`, its rules pure and tested
(`NatureMath.cs`; `Tools/Tests/NatureTests.cs`), tuned with the balance simulator (`Tools/Balance -- --nature`).

- **What an act brings.** `NatureMath.Rules`: each act, the side of a person it goes against (`PersonaAxis`, the end, from
  30 where a side shows). Past it, strain up to 12 by how far (`Floor`: about 5 just past it); an act against two sides
  counts the worse; a match pressed and a truce with one's foe weigh on anyone (8, more for the warm or proud). The other
  end of a side it eases (`easesOther`: the curious by a season away, the cold by a dark scheme) gets relief up to 6.
  Two acts at once (a hostage given is a season away too) count as one, the one that weighs most (`Pick`).
- **Through the heart.** `StrainSource.Nature` and `StrainRelief.Nature`, each at the end of its list (saved by
  position). Strain through `HeartLogic.Build` as the hour builds (the strength setting, traits, the source's cap of 12
  and the day's 25), scaled first as the pressures' bad side is (`NatureMath.Scale`: `PressureMath.Factor` of "Life's ups
  and downs", read whatever the stakes since it is no mood, and `HardTimesPressure` of `Pressures.Hard`); relief through
  `HeartLogic.Relieve` (their way of letting go, never more than they hold; `Interplay.Ease` counts every kind but
  gatherings against the friends' cap, so it is not used). One act a day per colonist: the heart keeps the last
  (`natureTick`, `natureAct`, `natureEased`, `natureWhy`, values, not a list).
- **Where each act happens.** The oath: `Fealty.Swear`, the speaker who walked out (`JobDriver_FealtyOath`). A truce, pact,
  oath or protection broken by the colony: `PactsHooks.Noted`, on whoever the game names for the deed (`MemoryHooks.Doer`:
  the blow's hand kept by `Patch_MemoryDamageBy` while the game weighs it, the captor by `Patch_MemoryCapturedBy`, the
  arrester by `Patch_MemoryArrestedBy` (the game weighs an arrest as a capture as it is tried, in
  `Pawn.CheckAcceptArrest`), the seller at the trade, the first of the caravan that fell on theirs, the killer); a deed
  the game names nobody for (a town attacked or destroyed, a harmful ability or item) touches nobody; a pact's call
  refused by the council's answer (`CallsToArms.Refused`), the speaker who sends it, in the words of one who sent the
  refusal (none with no home left). Those it touches as onlookers (a hostage's kin and a ward's parents, whose words say
  they had to let them go, never that they watched; the champion of a challenge turned down; the captive's kin, who heard
  it; the avenger) feel it only when there, on that map (`Nature.There`). A hostage of the colony's: as they walk off the
  map (`WardsAbroad.AtEdge`, `Nature.HostageGoes`: the hostage, timid or set in their ways, one act; their close kin among
  the colony there, warm, read through every relation the game knows, so a child and a brother or sister count, the game's
  implied relations). A youth sent away as a ward: the same moment, the parents there (`Nature.WardGoes`; a page sent to the
  Empire's titled household said as a page). A match pressed: `Matches.Pressed`, once it is agreed. A challenge: the
  travelling champion's and the rematch's (`HappeningWorker_Champion`, the challenge renown sends to a fighters' house),
  turned down by the council's answer (`DilemmaEffect_SendAway` with the context's `contest`, the challenger still here to
  be sent away) on the champion, proud or hot-tempered; faced at the contest itself (`Contest`) by a timid one. The envoy
  abroad: as they walk off the map (`EnvoysAbroad.AtEdge`). Asking more for a captive (`Ransoms.AskMore`): the speaker who
  haggles, and the captive's own close kin among the colony there. A truce sworn (`Truces.Swear`, `Nature.TruceWithFoe`):
  each avenger on this home's map whose vendetta is on them (`Vendettas`, as the truce's objection asks them), who saw it
  sworn at the gate, or heard of it when it was sworn by word with no envoy there (the role `Word`). The dark
  scheme is listed and unwired: schemes (2c.3) are not built.
- **Shown.** The heart's page says it the way a pressure says its cause (`HeartView.SourceText`, `ReliefText`): "swore
  the colony's oath to the Lenlia, against her pride (+7)", the side by the game's own pronouns; while the strain is too
  low for the general advice, "A quiet evening with friends would help." (`NatureHelp`; `HelpFor` names it in the advice
  above 30). The strain mood ("built up by ...") and an episode's letter name the source, "acts against their nature"
  (`SourcesText`), since the act's words don't fit their sentence. The act, the side and their frame are each worded
  from their fellows (`Languages/English/Keyed/Rimfolk_Nature_Variants.xml`) once, as the act is felt, and kept on the
  heart for its page and the news. The colony's news tells it (`Tell.Minor`, six wordings of a weight, five of an ease).
  The council's questions name it before an
  answer that asks it (`Nature.Impact`: the oath's speaker's pride, the avenger's vengeance, the colonist's heart for a
  match pressed, the champion's nature on the challenge's answers; three wordings, one kept for each colonist and act by
  `TextVariants.VaryBy`, so the letter's answer and the Dilemmas page say it alike), and the oath's objection names a
  proud colonist's nature in two new lines ("too proud to kneel to anyone").
- **Marks** (`Defs/PersonaDefs/Rimfolk_PersonaMarks_Nature.xml`, `NatureMath.MarkFamily`, given through
  `PersonaMarks.Give`): an oath sworn by a proud one may humble them (`NatureOath`, a turning point, half the time); the
  same act against the same side twice within a year (the act before kept on the heart) may change that side: a word
  broken twice makes a trusting one warier (`NatureWordBroken`), a challenge faced twice a timid one bolder
  (`NatureNerve`), a second season away one set in their ways more open (`NatureAbroad`), each six times in ten, within
  the persona's own limits. Each is told in three wordings (the def's `news` and its keyed fellows
  `CC_Mark_News_DEFNAME_2`..., `PersonaMarks.NewsLine`, which any mark may have), as it happens, so none says time has
  passed since. Three dark schemes hardening a kind one waits for the schemes.
- **The balance** (`Tools/Balance/NatureSim.cs`, printed): the heart hour by hour with the mod's caps and odds, the rest of
  life set to the colony-year report's strain shares per style, natures from the vanilla traits. A colony deep in
  diplomacy (about 5.5 acts a colony-year, on the generous side) has about 0.8 felt a year, each about 6 strain (on the
  heart's page about five days), 0.1 to 0.4 per cent of all strain built (15th of 17 sources), the main cause of no
  episode in the runs. Setting `diplomacyNature` ("Against their nature", Diplomacy, needs the heart).

#### Schemes against the colony

Release 2c.3, theirs only (docs/design/stage2-diplomacy.md, item 11; the player's decision 16: "only theirs"; the
colony's own schemes, gentle and dark, are not built, so "Against their nature" still leaves its dark scheme unwired):
`Diplomacy/Schemes.cs`, its rules pure and tested (`SchemeMath.cs`; `Tools/Tests/SchemesTests.cs`), measured with the
balance model (`Tools/Balance -- --schemes`, `SchemeSim.cs`). Of the two tie kinds the base reserved, Scheme is now in use;
Secret stays reserved (the envoy who learns a colonist's secret was left out with the Secret ties' code).

- **Who schemes.** Per home: each people at peace with a town in the home's reach (`RegionTowns`) holding a grudge of 20
  or more (`SchemeMath.Grudge`, as the raiders' camps read one: the wrongs it still holds, 15 a dead held, 20 a
  vendetta), and each notable of the home's neighbours (`NotableBook.NearColony`) who thinks -40 or less of the colony
  (`NotableBook.Opinion`). With none, nothing rolls and nothing changes.
- **How often.** In diplomacy's day (`Schemes.Daily`, no Sched slot): a home with a schemer, no scheme of its own under
  way and a season since its last began rolls 8% (times the setting): about 2.3 a year, one a season at most
  (`SchemeMath.YearRate`). The design's "a roll every 15 days at 8% (about two a year)" can't be both: a roll every 15
  days at 8% is a third of one a year, so the roll is daily with the season's wait, which gives the two a year the design
  asks for and the design's performance notes put on the daily pass. The act is picked from what the home has, whatever
  the hour: a machine that can break down (industrial and after), a small valuable in its stores, a hot temper (a
  fighting temper of 2 or more) with another grown-up, each weighing one (a fight times the harm scale, so none with
  hurts at nothing), a smear always.
- **The tie.** `WorldLedger.AddTie(Scheme, schemer, colony, key: the act, until: a season, origin "theirs")`; given up
  (ended) at its end by the ledger's own rules, kept once it worked, broken once found out; its key becomes the form it
  took ("Console", "Purse", "Papers"), `goods` the thing's def, `about` the one caught. Unknown until found out:
  `History.Tie` writes nothing for it (the History page shows the history), the people's card's tie lines and the facts
  read only broken ones.
- **Acted out** in diplomacy's hour (`Schemes.Hourly`) only while one of theirs is on the home's map (`Schemes.Agent`: a
  grown-up of the game's visitors, `LordJob_VisitColony`, or a guard of their trader's caravan, never the trader nor a
  Hospitality guest, up and about), at the act's hour (the night, eleven to four; a word by day, nine to eight), with no
  danger and nobody of the colony awake within 6 cells of the place. A forced job, the agent back with their group after:
  - *a machine*: the rival's sabotage job (`Rimfolk_Sabotage`, now at any building, from beside one with no place to
    stand), the game's breakdown (`CompBreakdownable.DoBreakdown`, sparks), a spacer colony's comms console first;
  - *a theft* (`Rimfolk_SchemeTake`): from a stockpile or shelf by the colony's age, a tribe's totem (a small minified
    sculpture), a purse (silver, gold, jade), a tool (components), 60 to 180 silver's worth, into their pack (split off as
    the act ends, so a job left over from before a load takes nothing);
  - *a quarrel stirred up* (`Rimfolk_SchemeWord`): to the colony's hottest temper by day, a lie about the one they like
    least (kin and lovers left out) said at their side; unheard, the hot temper goes to the other (`Together.When` with a
    walk) and it is `Harm.Fight`, or hard words when the cap holds, with a message;
  - *a smear* (`Rimfolk_SchemeLook`): a few seconds in an empty room of the colony's in the night; one to three days later
    (the tie's count 1, the day in `days`, its end moved past it) every other people at peace in reach loses 3 goodwill,
    never to hostility, and the news says it by their ways (other fires, other halls, the papers, the feeds, the court).
- **Caught.** As the act ends (`Schemes.Done`, called by both jobs' last toil): at night each colonist awake within 12
  cells in sight (half), each guard on duty within 20, drafted or under the on-guard decree (nine in ten), each dog of the
  colony awake within 10 in sight or 5 through a wall (three in four); a whisper, whoever is in earshot (`Together.Hears`,
  7 cells, two in five). Caught: a shout with nobody named (`Rimfolk_Pool_SchemeCaught`), or the dog's own call
  (`Pawn_CallTracker.DoCall`) and the nearest sleeper woken; the thing dropped, the machine untouched, the lie unheeded;
  their word as they go and a jog off the map (`LordJob_ExitMapBest`, the rest of theirs staying); the tie broken, the
  history's `tie:scheme:theirs:broken` and `dip:foundout` (a milestone), the deed `schemedUs` (with the name of the one
  caught), 5 goodwill never to hostility, a line in the notable's memory if one schemed, a letter. Not caught: nothing
  told but the game's own (a machine broken down, something gone with them, the fight's message).
- **Shown.** Facts `tie:scheme:theirs:broken` and `them:caught` (`FactsSchemes.cs`, in the ties' provider), for the
  sixty days the ledger keeps a closed tie; the rows on Here and away ("Schemes found out", the Household page's place
  for what was found out) and on the people's card on Peoples, each opening the tie's card, whose story reads the history
  line. The words by both ages (`Rimfolk_Schemes.xml`: two wordings of each told message), four pools of one sentence
  (`Rimfolk_Schemes_Lines.xml`), an Almanac page ("schemes against the colony"). Dev mode: Debug actions, Rimfolk,
  "Schemes: ..." (`SchemesDebug.cs`).
- **The game's code.** No Harmony patch: the game's breakdown, social fight, call, wake-up and exit are called as they
  are; the jobs are forced on their pawn as the callers' and the feasts' are.
- **Saved.** Only the ties (the ledger's bound; no new list); an act under way is forgotten at a load and comes again.
  Setting `theirSchemes` ("Schemes against the colony", How much happens, 0 to 200%, 100%; half in Light touch, 125% in
  Story-rich; needs answers on the map). Off, none begin and those under way run out by their terms.
- **The rates** (`--schemes`, 4,000 home-years each): with a people with a grudge in reach all year, about 2.4 schemes
  begin, 1.4 are acted out (0.7 with their people seldom here), 0.65 given up; caught one time in eight with nobody
  keeping watch (a third with a dog and the guard kept); in a colony of six a machine broken 0.35 a year (beside the
  game's own 2.6 for ten machines), something taken 0.3 to 0.5 (40 to 60 silver's worth), a fight stirred up 0.16 to
  0.21 (0.03 to 0.07 hurts a colonist-year, beside 0.26 to 0.50 of harm's parts 1 to 3 and a raid's 1.4 to 2.0), a smear
  0.3 to 0.5, about 5 goodwill lost in all. With no grudge and no notable that cold: nothing (0.132.0 had none of it).

### Rimfolk's windows

The player's word (4 November 2026): few buttons on the bottom bar, each with its own window, and no master window.
Four buttons, placed together after the council's by `CouncilStartup.ApplyButtonPosition` in the order of
`Defs/Misc/Council_Misc.xml`, each a `MainTabWindow` deriving from `RimfolkTabWindow` (the same size, `WindowTabs.Size`:
1010 by 720, or up to 1280 by 860 with the setting "Window size" on Large, below; and the same place, so going from
one to another keeps the frame):

| Button (def) | Label | Window | Pages |
|---|---|---|---|
| `Council_MainButton` | council | `MainTabWindow_Council` | views Week (This week), People (modes Table, Moods, Bonds, Faiths, Renown), Council (Decrees, Ambition, Ways, Wishes, Dilemmas), Colony (Overview, Calendar, Chronicle) |
| `Rimfolk_WorldButton` | region | `MainTabWindow_World` | views Map (lenses Land, Stance, Fortunes, Threats, Trade, Ours; modes Map, List), Peoples (modes Peoples, Faces), HereAndAway, WorldHistory; pages other mods add under History's More |
| `Council_DefenseButton` | defense | `MainTabWindow_Defenses` | Defenses |
| `Council_RoomsButton` | planning | `MainTabWindow_Planning` | views Plan (modes Plan, Priorities), Base, Making, Rooms (modes Build, Rooms: List and At a glance) |

- **One list of pages** (`UI/PagesMath.cs`, pure, tested in `Tools/Tests/PagesTests.cs`): each page's window and
  view (its section), in tab and mode order. A window of more than one section is made of views (`HasViews`): its
  tabs are the views, and a view's pages its modes; a window of one section has its pages as tabs. The windows build
  their tabs and modes from it. A page's name is its address: `Pages.Open(name)` (`UI/Pages.cs`;
  `MainTabWindow_Council.OpenTab`, `OpenOrClose`, `IsPage` and `TabName` forward to it, so every older caller, mood
  hint, advisor step, This week card, panel line and page key routes through it) opens the window that has the page,
  on its view and mode; a view's name ("People") opens the mode last looked at there; "Almanac" opens the Almanac's
  own window. Old addresses resolve (`PagesMath.Moved`, below for UI 4's and UI 2's): `World` is the region map's Stance and
  ties lens as a list (`Map/Stance/List`), `RegionMap` its Map, `History` (the old section) the Chronicle; `Defenses`
  lives in its window. An address may name a region view's lens and mode after slashes (`PagesMath.TryAddress`). A window opens through its button's def, which works with the button hidden or put away.
- **The corner** (`UI/WindowTabs.cs`): every window draws its tabs with `WindowTabs.Draw`, the game's tabs with their
  icons (only the icon, the name on hover, where the name doesn't fit), and in the top right corner back and forward
  (below, "Names that lead somewhere"), the search, This week's inbox with its count and what is followed (below, "One
  inbox and search"), the Almanac's book (its window, lit while open) and the four windows' icons, the one showing lit.
  A click opens that window as its button would. The corner is `WindowLayoutMath.CornerWidth()` wide (316: six tools and
  four windows of 24 with their gaps), which the layout test tries on every screen. The defense window has one tab, so
  its corner is where the others' is.
- **The region window** opens on the region map unless something asked for a view: four views (below, "The region
  in four views"). The map is `RegionMapView` (what was `Window_RegionMap`, the same drawing, now inside the view): its
  picture is made when the view is first drawn and let go when the window closes; where it looked is kept for the
  session. Its panel is the window's drawer: what is picked is the place whose card is open there (kept for the
  session too), and with nothing picked the drawer shows how to use the map and its lens's whole legend. The drawing
  went into the window rather than the region map keeping a window of its own because the window was only a frame
  around it (its own place, size and close button): inside the region window the map sits beside the views the player
  asked to have with it, under one button, as the other windows are.
- **Other mods' region pages**: `MainTabWindow_World.Register(name, labelKey, icon, rect => Draw(rect), () => shown,
  order)` from a `[StaticConstructorOnStartup]` class still adds one. It is listed under History's More (in its order)
  and drawn in the window's body with History's tab lit, until it finds its place in a view; a name that is already an
  address is refused. Rimfolk's own parts add rows, sections, acts and lens marks instead (docs/design/interface.md,
  "Where a new thing goes").
- **The planning button** kept the rooms button's def name (`Council_RoomsButton`), so a bar arranged around the rooms
  button (`barPutAway`) and other mods' settings keyed by the def stay as they were; the small minimized
  `Rimfolk_PlanButton` is gone. Its settings merged once (`planningMerged`, false in a settings file from before):
  `planButton` is on if either `roomsButton` or `planButton` was, and `BarMoreMath.MergePlanning` keeps the button on
  the bar if either button was on it, under More if both were put away.
- **Settings**: `worldButton`, `defenseButton` and `planButton` show or hide the three buttons beside the council's.

#### Names that lead somewhere (UI 1 of docs/design/interface.md)

The first of the interface's five releases: every thing has one card, a name of it in Rimfolk's windows leads there,
a pause on a name shows who or what it is, and every window has a way back. All of it is in `UI/Cards/`; its pure
parts (`ThingRefMath.cs`, `LinkTextMath.cs`, `NavTrailMath.cs`) are tested in `Tools/Tests/CardsTests.cs`.

- **Things.** `ThingRef` is the world ledger's `Ref` (a person, a town, a people, a colony, a house, a tile) and the
  things only the windows name: any other place on the world by its world object (`s40`), a tie (`y7`), an entry of
  the history (`e12`), a letter (`l3`). A plain value with a text form, safe when the thing is gone: `ThingRefs` finds
  what one names while the game has it, whether it is still there (kept three seconds), its name (five seconds, then
  what the ledger, the notables or the history kept), and its card: a person of the colony's own opens a colonist's.
- **Cards.** Eight kinds, each built by one builder in `Cards.cs` at most once a second while it shows (nothing while
  no card does): Colonist (the Roster's page of them, StoryView, under the card's head), Person (a notable, a known
  face, anyone the ledger keeps: their letter on its way in steps, who of the colony knows them, what they remember),
  Place (the region map's panel: a town, a post, a hamlet, a ruin, a raiders' camp, a town being built, a bare tile,
  a colony; with a small world view button on its head where "Show on the world" was), People, House (since 5 November
  2026 none of its own: a house's name opens its town's card), Tie, Event (an entry of the history: its words with links,
  who took part), Letter (its steps, never a time left). Every card has
  the same slots in the same order: head, now, who, ties, words, story, acts. A section folds, with its count in its
  heading, its fold kept per kind of card for the session, and a section with nothing is not drawn. A card's story is
  its newest three lines of the history and a link to the whole of it on the History page. Its acts are a bar at its
  foot, each greyed with why not and the first reason written under them, the rest under More.
- **Adding to a card**: `CardSections.Register(kind, slot, order, draw, heading, id, count)` (a section drawn by its
  own code) or `RegisterLines(kind, slot, order, lines, heading, id)` (lines, links allowed, asked once a second with
  the model), and `CardActs.Register(kind, label, whyNot, act, shows, order)`; for the place card,
  `CardSections.Town`, `TownLines`, `PlaceLines` and `HomeLines` take what the region map panel's sections took. The
  fourteen sections of 0.80.0 moved into its slots (who the colony knows there, Who; heard lately and far news, Words;
  fortune, a hamlet, a post, a ruin, a town being given up, a camp, a town being built, a home's remoteness and its
  camp, Now; a new town's founding, Story; "Plan a journey here", an act), and stage 2's debts of honor are its Ties.
  `NeighbourPanel`'s three lists stay for a release as a shim: what is still added there draws on the place card, a
  town's in Ties, any other place's and a home's in Now.
- **Opening a card.** `Cards.Open(ThingRef)` (and overloads for a pawn, a people, a world object, a notable, a tie, a
  ref) opens it where the player is: in the drawer in sight (the council, region and planning windows', since UI 4;
  `Drawer.Active`), else in a small floating card window (`Window_Card`) with its own trail and "Open in its window"
  (a colonist in the People table, a place on the region map, anything else in the region window's drawer on the view
  that lists it). `Cards.ShowOnMap` opens a place
  on the region map with its card as the panel: the "Show" buttons of the trading posts, hamlets, raiders' camps and
  journeys pages, This week's rows about places and the missions lead there now rather than to the game's world view;
  something on a map is still jumped to. `DiplomacyLinks.Open` opens cards.
- **The drawer** (`Drawer.cs`, `DrawerMath`): 380 wide at the right of the content area, beside the view from a content
  area of 900 (at 1920 by 1080, scale 1 and 1.5, the map is 558 wide beside it) and over it with a shadow below that,
  where the view under it doesn't see the mouse. Its head is the trail of cards opened in it ("Tallbrook › Edda
  Marsh"), each a link back, and a close button; Escape closes it before the window (`RimfolkTabWindow`). In UI 1 it
  was the region window's; since UI 4 the council and planning windows hold one too (below), and since UI 5 the
  defense window.
- **Links in text.** A link is a marker only `LinkText` reads: three characters of Unicode's private use area around
  the thing's text form and the words shown (`LinkTextMath.Wrap`). Text for Rimfolk's windows asks for links: the
  history's lines (`CardWords.HistoryLine`: each name of who took part or where, marked at its first mention), the
  facts' fill (`Texts.Fill(text, values, things, links: true)`, with `FactSet.things` naming a placeholder's thing),
  the panel lines that name a founder, a mother town, a family, a people. Everything that goes to the game is filled
  plain, and `Tell` and `Speech.Say` take out any stray marker at the door. `LinkText` lays a text out for its width
  once (one `Text.CalcSize` per word, kept), draws it piece by piece, and keeps each link's rect for the mouse: link
  ink with a fine dotted line under it, solid and brighter under the mouse; a click opens the card. In UI 1: the
  world history, the place card, the Neighbours page's towns and people, the Peoples rows and their wars, the Letters
  rows, a people's chip (`UiKit.FactionChip(..., link: true)`), a notable's portrait, a town's name.
- **Previews** (`Previews.cs`): a pause on a link (0.35 seconds, the setting `linkPreviewDelay`) shows a small card under
  it, Rimfolk's own immediate window on the layer the game's tooltips use, asked for each frame while it shows: the
  picture, the name, what it is, its state, two or three lines that matter now and how to keep it. It stays while the
  mouse is on the link or in it, and half a second after; a click on it opens the card. None while dragging or
  scrolling, none with `linkPreviews` off (a chip's plain tooltip comes back then). Buttons and icons keep the game's
  tooltips. Since UI 5 a link inside a preview shows a second one beside it, two levels at most (below, "One look").
- **Back and forward** (`Nav.cs`, `NavTrail`): each window tells where it is once a frame (its page, the filters that
  matter (the History page's), the card open in its drawer); a new place is a step, scrolling and folding aren't. At
  most 50 steps, kept for the session; going back past a step whose thing is gone drops it. The arrows in every
  corner, two keys (`Council_Key_Back` and `Council_Key_Forward` in `Defs/Misc/Rimfolk_NavKeys.xml`, unbound), and the
  mouse's side buttons where Unity reports them (buttons 3 and 4).
- **The look** (`UI/UiKitCards.cs`): the design's tokens (link ink and its line, the cool stance, done, danger, the
  drawer's and the preview's surfaces) and the components UI 1 draws: a section heading with its count and fold, a
  thing chip, a state chip, an act button with why not, steps, an icon button.
- **Settings**: `linkPreviews` and `linkPreviewDelay`, under "Names and cards" on the How you're told page (`windowSize`,
  since UI 5, beside the Almanac's text size).

#### The council in four views (UI 4 of docs/design/interface.md)

The fourth release, on the player's decisions 4 and 5: the council window's eighteen pages in two rows of tabs became
four views in one row, the planning window's five pages four views, and the Almanac left for a window of its own.

- **Views and modes.** A window made of views (`PagesMath.HasViews`) has them as its tabs; a view's pages are its
  modes, at most five, a row of small buttons under the view's title (`UiKit.ModeButtons`). The council window: This
  week; People (Table, Moods, Bonds, Faiths (shown with "Faith in the colony" on), Renown); Council (Offices and
  decrees, Ambition, Ways, Wishes, Dilemmas); Colony (Overview, Calendar, Chronicle). The planning window: Plan (the
  plan, Priorities), Base, Making, Rooms (Build as List, Rooms as At a glance). Each window keeps the mode last
  looked at in each view for the session; the council window's page is saved in the settings as before
  (`lastCouncilPage`, an old name read as where it went). The page keys step through the modes view by view. Tab and
  mode labels carry their counts (This week's, the Council view's petitions and wishes, Wishes, Offices and
  decrees, the plan's steps), made twice a second.
- **The view header** (`UI/UiKitViews.cs`, `UI/ViewHeadMath.cs`, pure and tested): icon, title, one line, the stat,
  the "?" to the page's Almanac article (`PagesMath.Articles`) just left of the page's own buttons, and the modes in a
  row of their own under the line (26 more, so a view with modes is 82 tall against the old header and sub-tabs'
  94), always at the same place so a mode doesn't move under the mouse; squeezed evenly when they don't fit. A page
  keeps drawing its own header (`UiKit.PageHeader`, so every page keeps its title, line, stat and buttons): the
  window says what its view's header carries (`ViewHead.Draw(rect, key, title, icon, model, draw)`) and the page's
  `PageHeader` takes it. A page that draws no header (an empty state) gets one from the window from the next frame
  on, so its modes stay in reach. None of it knows of Rimfolk's tab windows: it takes a rect and a model, so one of
  the game's windows could draw a view the same way.
- **The drawer in any window** (`UI/Cards/Drawer.cs`): a `DrawerHost` is one drawer's trail and card; each of the four
  windows has one (`Drawer.Of(window)`), another window can make its own (`Drawer.Make()`) and draw it with
  `Drawer.Around(host, rect, view, empty)`. A host drawn this frame or the last is in sight; `Drawer.Active`, the one
  drawn last, is where `Cards.Open` puts a card, the floating card only when none is. The council and planning
  windows draw every view inside theirs, so a name clicked anywhere in them opens its card beside the view. The
  region window's calls are as before.
- **People.** The Roster and Everyone are one list, the table (`PeopleTable`): a click on a row opens the colonist's
  card in the drawer, the row lit while it shows, and the columns that don't fit beside it give way (the name, mood
  and trust always stay; `PagesTests` checks it at the screens and scales the game offers); a right click shows them
  on the map, their Mood tab, the pin. The colonist's card is the Roster's page of them (StoryView): their whole
  story with their dreams and the newest of their journal, and since UI 4 their years asleep beside their age. The
  old Roster page (`PeoplePanel`) is gone. Renown (`UI/RenownPage.cs`, built once a second; Houses until 5 November
  2026, whose address now opens it): the colony's name among the peoples and its people known by name ("Colony renown"
  under Diplomacy, below).
- **Colony.** The Chronicle has two more filters, what were pages: Dreams (every dream told, by dreamer and kind) and
  Journals (each colonist's journal), `UI/HistoryPanels.cs`. Its filter is part of the way back (`Nav.Filters`), and
  `Nav.SetFilter` puts it when an old address asks. A chronicle line about a colonist names them as a link to their
  card (`LinkTextMath.MarkNames`, made once per line).
- **Links.** A colonist's portrait opens their card in the drawer: This week's rows, the Wishes cards, the dreamers and
  journal writers, the ideoligions' people, the groups, a mood row, `UiKit.PawnChip`; This week's colonist rows open
  it too. `MainTabWindow_Council.OpenPerson` (from the colonist bar, the Story tab, a panel, the card's own act) opens
  People's table with the colonist's card in the drawer.
- **The Almanac's window** (`Almanac/Window_Almanac.cs`): movable, resizable, never pausing, one at a time. It opens
  beside Rimfolk's windows where the screen has room (they open at the bottom left; it at the top right), else in the
  middle, and comes back where it stood and at its size for the session. Every view's "?" opens it on that view's
  article, the book in every corner opens or closes it, and `MainTabWindow_Council.OpenAlmanac`, "Almanac" and the
  Almanac key do as before. A page opened again comes back where it was read to (`AlmanacPanel`, kept for the
  session); the page last read is kept in the settings (`lastAlmanacPage`) for the next session.
- **Old addresses** (`PagesMath.Moved`, `MovedFilters`; `PagesTests` resolves each): Roster and Everyone are People's
  Table, Dreams and Journals the Chronicle with their filter, Offices is Offices and decrees, Priorities is the
  planning window's Plan view, Build and Rooms are the Rooms view's two modes, Almanac is the Almanac's window, a
  view's name ("People", "Council", "Colony") its last mode; every other page kept its name. The mood hints, the
  advisor's steps, This week's cards, the panels, the page keys (Everyone's is People's table now) and the bottom bar
  buttons made in XML (a section "Almanac" opens the window) all go through `Pages.Open`.

#### The region in four views (UI 2 of docs/design/interface.md)

The second of the interface's five releases: the region window's ten pages (Map, Neighbours, Peoples, Letters and calls,
Journeys, World events, Trading posts, Raiders' camps, Hamlets, History, and stage 2a.1's hidden Diplomacy and
Household) are four views, each one page of its own section in `PagesMath.All` (so the region window counts as views
in the five-views check), its own modes and lenses parts of its address. All in `UI/Region/` with the pure part in
`UI/RegionViewsMath.cs` (tested in `Tools/Tests/RegionViewsTests.cs`, the addresses in `PagesTests`).

- **Addresses** (`PagesMath.TryAddress`, `Address`, `Modes`, `Lenses`, `MaxLenses`): a view and, after slashes, a mode or
  a lens of it: `Map/Trade/List`, `Peoples/Faces`. An old name forwards with its parts (`Neighbours` and `World`:
  `Map/Stance/List`; `TradingPosts`: `Map/Trade/List`; `RaidersCamps`: `Map/Threats/List`; `Hamlets`:
  `Map/Ours/List`; `Letters`, `Journeys`, `Household`: `HereAndAway`; `Diplomacy`: `Peoples`; `Events`:
  `WorldHistory`), and parts after an old name override it. A mode an address doesn't name is the view's first; a lens
  it doesn't name stays. `Pages.Open` passes the whole address to the window (`MainTabWindow_World.Select`, which sets
  `RegionView`); the way back keeps the view's lens and mode as its step's mode (`Nav.Observe(w, page, mode, card)`,
  `RegionView.Within`), and `Pages.Name` names it ("Map: Trade, as a list"). The window's button opens it on the map
  itself, the lens as it was; nothing else of the views is saved.
- **Map** (`MapView`, `RegionMapView`, `LensModel`): the lens bar (`UiKit.LensBar`: icon and name for each, the lit one
  named and the rest icons where they don't all fit), Map or List (`UiKit.Modes`) and the "?" along the top; under
  them, in Map, a line of what the lens shows, the way home and the reach, then the map. A lens (`Lens`, at most six)
  recolours the marks drawn each frame (`RegionViewsMath.MarkTone` over each place's `PlaceFacts`: Land as the map
  was; Stance by the people's stance, warm, cool or sour, with the town's head's regard as a ring; Fortunes by fortune
  band, an arrow where it moved this season, a ring where a world event runs, a mark on a world event's tile; Threats
  hostile towns, camps and a home with a threat now; Trade posts and the towns that trade; Ours homes, hamlets,
  caravans), draws lines with `Widgets.DrawLine` (`UiKit.DashedLine`: a pact solid, a truce dashed, a favour owed
  dotted, warm to the colony or soon from it, protection double, a grudge dotted and sour, a camp's raids in short
  dashes, a post to its town, home to its hamlet), and has its legend in a strip at the map's foot (`UiKit.LegendStrip`)
  and whole in the panel with nothing picked. Lines read the ledger: its live pacts, truces, favours, fealty and grudges
  with the colony on one end (a home, its people, one of its own), and other mods' fealty views, each from home to the
  other end's place (a town, a notable's town, a people's town nearest the home); `RegionViewsMath.TieLine` says which
  draw. The land's picture is made once as before; the lens model is built at most once a second while the view
  shows, from the world objects within the map's reach. The List: the lens's rows in groups (`RegionViewsMath.Group`,
  `Key`, `Compare`: Threats camps, fights on their way, hostile towns; Trade posts, towns that trade, caravans; Ours
  homes, hamlets and settlers on their way, caravans; Fortunes from struggling to flourishing, a town that moved before
  its band, then ruins and towns being built; Land and Stance the towns, nearest first, Stance with the people known
  there), each 44 high with its picture, its name as a link, its state as a chip, a second line, its distance by
  caravan (`CardWords.DistanceShort`), its act (a camp's Pay) and "Show" (the map with it picked); a click on the row
  opens its card in the drawer; only rows in sight are drawn; what came before (how the last camps, posts or hamlets
  went, from the history) at the foot. With no card open the List takes the whole width.
- **Peoples** (`PeoplesView`): Peoples mode, the owed strip (`DiplomacyPanels.Owed`), a card for each people the colony
  can know (stance, kind, wars, the world's events about it, and where 2a.1's model has it its head, ties, what is
  under way and its acts, `DiplomacyPanels.PeopleBody`; "Their history"; a click opens the people's card), then what
  they remember (`FactionMemoryPanel`). Faces mode: the people known by name with Write (`LettersPage.KnownSection`),
  then who's out there by `OutGroup` (`WorldPanel.OutThereSection`: old foes, those who left, the rest, each opening
  its card) and the faces the colony keeps up with.
- **Here and away** (`HereAndAwayView`): who is here from outside (2a.1's rows, and the camp on the colony's land,
  `CampPanel`), what's coming (`WorldPanel.ComingSection`: questions waiting, debts, orders and visits due, a story's
  next stage; the fights on their way are the Threats lens's, the calendar's days and the council's meetings This
  week's), letters on the road (`LettersPage.UnderWaySection`), the colony's people away (2a.1's rows, the journey
  planned, the caravans with Show), missions (`MissionPanel`) and the peoples the colony is bound to by marriage.
- **History** (`WorldHistoryPage`): the world's events running as a strip of chips at the top (a click leads to where
  each is, or its people; the tooltip says what it means and the colony's part), the scope's chips (this colony, the
  region, the world: `WorldHistoryMath.InScope` over the parties of each, the region the default), "Letters lately"
  (the colony's letters and calls as rows, `LettersPage.LatelyRows`), More (other mods' pages), then the filters and
  the rows as before, each row opening its entry's card. The scope and the letters chip are part of its filters on the
  way back. `WorldHistoryPage.ShowFor` (a people's or a town's history) opens on the world.
- **Components** (`UI/UiKitLenses.cs`, beside UI 4's `UI/UiKitViews.cs` whose view header with modes and "?" the
  Peoples, Here and away and History views use): `ToneColor` (a `Hue`: warm, cool, sour, gold, soon, dim, world),
  `KeptWidth` (a label measured once), `Modes`, `LensBar`, `LegendItem` with `LegendStrip` and `LegendList`,
  `DashedLine`, `FilterChip`. Each takes a rect and plain data, so any window can draw it.
- **What became of the old pages' code**: `WorldPanel` keeps what's coming and who's out there as sections (the
  fights, calendar and meetings left it); `NeighboursPanel` is the town card's Who only; `LettersPage` is sections; the
  posts', camps' and hamlets' pages keep their This week rows, card sections and words, and `RaidersCampPage.AskToPay`
  is the Threats list's Pay; `DiplomacyPanels` registers no page (its old names forward). Mood hints, This week's rows,
  the advisor, the overview and the cards' "open in its window" (`Cards.FullPage`: a person on Peoples' faces, a
  people and a tie on Peoples, an event in History, a letter and a house on Here and away) lead to the new views.
- **Icons**: `Away`, the six lenses (`LensLand` to `LensOurs`), `ModeMap`, `ModeList` (`Tools/make_icons.py`).

#### One look, the first part (UI 5a of docs/design/interface.md)

The fifth release's first part, on the player's decisions 3 and 6: the defense window in the views' grammar, names as
links where Rimfolk draws them in the game's own panes, a second level of previews, the window size setting and a test
of the windows' layout across screens. Built beside UI 3 (the inbox, search and following in the corner); the rest of
the grammar across every window (UI 5b) waits for it.

- **The window size** (`UI/WindowLayoutMath.cs`, pure, tested in `Tools/Tests/LayoutTests.cs`): `windowSize`
  (`WindowSizeChoice`, Standard at first, on the How you're told page beside the Almanac's text size). Standard is 1010 by
  720, Large up to 1280 by 860; both within the screen less 20 at the side and 55 at the foot, the large never smaller
  than the standard. `WindowTabs.Size` asks `WindowLayoutMath.Size`, so all four windows, the drawer, the region map
  and the views lay out at either (they work from their rects); a window takes the new size the next time it opens.
  `DrawerMath.WindowSize` and `ContentWidth` are `WindowLayoutMath`'s.
- **The layout test** (`LayoutTests`, like `TabFitMath`'s): fourteen screens from 1024 by 768 to 3840 by 2160 (4:3,
  16:9, 16:10, 21:9) at scales 1 to 2, standard and large: the window on the screen; the top row with five views and
  the whole corner as it will be once UI 3 is in (back, forward, search, the inbox, following, the Almanac's book and the
  four windows, `CornerWidth()` 316) never meeting, each tab at least its icon, icon and name from 1010 wide at five
  views and from 900 at four; the drawer inside the window, beside from a content area of 900 with the view at least
  500, over it below with its edge in sight; the region map (`RegionMap`: the view beside the panel or the whole
  content under it, less the lens bar and the map's line) at least 400 wide and 150 tall everywhere, 300 tall from a
  screen of 540; the People table's name, mood and trust beside a card. At 1080p the map is 558 wide standard and 828
  large.
- **Two levels of previews** (`UI/Cards/PreviewMath.cs`, pure, tested in `Tools/Tests/OneLookTests.cs`):
  `PreviewStack` holds a preview per level; a link drawn in a window or pane asks for the first, a link drawn inside the
  first (`Previews` knows which it is drawing) for the second, a link inside the second for none (a click still opens
  its card). The second sits beside the first, level with its link, right where there is room, else left, under the
  link only when neither side has room (`PreviewMath.Beside`), so the two never cover each other. The mouse in either
  keeps both; the second goes half a second after the mouse leaves it and its link, the first when the mouse has left
  both; a new first takes the second away; none while dragging or scrolling; `linkPreviews` off covers both. Each is
  its own immediate window on the tooltips' layer (`WindowId + level`), asked for in order so the second stays on top;
  the mouse inside one is its own (`Mouse.IsOver` is false in the windows under it), a click on its empty part stays in
  it (`GenUI.AbsorbClicksInRect`), and when it goes the window under it has the mouse back.
- **The defense window** (`MainTabWindow_Defenses`, `DefensePanel`): its one view drawn as the others are, through
  `ViewHead.Draw` (the page's own header takes the "?" to `Rimfolk_AlmDef_Overview`, `PagesMath.Articles["Defenses"]`;
  no modes, it has one way of looking) and `Drawer.Around` with the window's own drawer (`Drawer.Of(Defense)`), so a
  name on the page opens its card beside it; the way back keeps its card (`Nav.Observe`). The page keeps everything it
  did; how it reads changed: the map card first, then each part a section whose heading folds (`UiKit.SectionHeading`,
  folds kept for the session) and carries its count where it counts things (defense lines, what to do, the plans);
  facts as rows with a dot; advice and plans as rows with their acts at the right through `UiKit.ActButton` ("Plan it
  here" and a plan the colony can't build yet greyed with the reason as their tooltip, rather than refused after the
  click; "At the suggested spot" greyed where the survey found nowhere to put it); an empty state where there is no
  advice. Names as links (`PaneLinks`): the best shot and the best in close fighting, the marshal (their portrait too),
  the hostile peoples in what the colony faces. The Almanac button at the foot of the facts went: the "?" opens the same
  page.
- **Names as links in the game's panes** (`UI/Cards/PaneLinks.cs`: a line's names marked once when its model is built,
  `LinkTextMath.MarkNames`, never in a tooltip): the Story tab (`StoryView`: the family's portraits in the overview with
  their names as links, `UiKit.PawnChip(..., link: true)`; a vendetta's people and a nemesis on their tags,
  `UiKit.PillSpec.thing`; the family tree's names; the family's, the years' and the bonds' lines; the secrets' names),
  the Culture tab (a prisoner's people, who won them over), the Social column (each bond's colonist, the secrets'
  names, the colony's people named in their groups, what people say and their shared memories,
  `PaneLinks.MarkColony`), the Mood tab (the one a mood is about in its why line, from the game's social thoughts,
  `MoodView.Texts.whyShown`; the plain `why` stays for the tooltips). `PawnPane` rows and `CharacterView.DrawSection`
  draw through `LinkText` when a line has links. From the panes a link opens the floating card (`Window_Card`, no
  drawer in sight there); in a card or a window, the drawer. The game's own text stays as it is: the Social column's
  talks (the play log), the moods' names (the game's thoughts). The Needs tab's line names no one (the groups' totals).
  A row's or a chip's plain tooltip gives way while a preview shows, so the two don't cover each other.
- **Left for UI 5b**, once UI 3 is in, and built there (below, "One look across every window"): the tokens and
  components across every window's pages, the page accents giving way to the tokens, the Planning window's pages in the
  section grammar, links in the remaining panels. (`WindowTabs.LinksWidth` reads `WindowLayoutMath`'s corner since UI 3.)

#### One inbox and search (UI 3 of docs/design/interface.md)

The third release: This week becomes the one place where everything waits on the player, with one count shown
everywhere, any thing can be followed, and a search in every corner finds anyone and anything by name. Built beside UI
5a and merged with it. Pure and tested (`Tools/Tests/InboxTests.cs`): `UI/InboxMath.cs`, `UI/FollowMath.cs`,
`UI/SearchMath.cs`.

- **The inbox** (`UI/Inbox.cs`): `Inbox.Register(provider)` (an `InboxProvider`: its id, its section, its order, its
  rows into a list, how many of them wait on the player, and whether they stand at the section's foot) is how a
  feature adds to This week: rows, not pages (docs/design/interface.md, "Where a new thing goes", rule 1). The sections,
  in order (`InboxSection`, `InboxMath.Sections`): Following, Needs you, Under way, Coming, How people are, The plan.
  `InboxRegistry` (pure, like `CardRegistry`) keeps the providers by section, order and the order added; one added again
  under its id replaces the first.
- **The providers** (`UI/InboxProviders.cs`, `UI/InboxUnderWay.cs`), what were This week's twelve cards and the region's
  and diplomacy's lines. Following: the things followed (`FollowWords.Rows`), then the wishes pinned. Needs you (each
  row with its act on the right): wishes and petitions (Look, Decide), Rimfolk's letters waiting for an answer and the
  evening news (Answer, Put away), the region's and the council's questions still open (a raiders' camp's terms, a
  hamlet's ask, the election while the council may step in, a journey waiting at the gate: each letter's `Waiting`;
  Answer), a people's call on a debt (`DiplomacyPanels.WeekRows`, Answer), settlers at the gate (`HamletPage.WeekRows`,
  Look), promises at risk (Look, with the day by which to put it right). Under way, each as steps (`UiKit.StepsLine`:
  a dot and a word for each, the one it is at lit) and never a time left: letters on the road (written, on the road,
  arrived, answered; `CardWords.LetterSteps`), the journey planned (planned, getting ready, at the gate, set out), the
  colony's caravans (set out, on the road, there, on the way home; `InboxMath.CaravanStep`), missions taken on (taken
  on, under way, done), settlers on the road (at the gate, on the road, settled), a raiders' camp's price going out
  (agreed, going out, handed over), what an answer set going on the map (set going, happening, done) and what it keeps
  there, the council meeting in session, the camp on the colony's land, Hospitality's guests. Coming, by the day
  (`InboxWords.When`: today, tomorrow, or the date, never "in 3 days"): the calendar's days, the council's meetings,
  fights promised, stories, missions offered (until their date), a post that may close, a raiders' camp standing (Pay
  while its price is offered, greyed with why not). How people are as before (strain, illness, trust, the colony's
  trust trend), the mood balance at its foot (`footer`). The plan: the advisor's steps, two showing, the proposals
  waiting at its foot.
- **This week** (`UI/WeekModel.cs`, `UI/WeekPanel.cs`): the model asks each provider once a second, section by
  section, each provider's rows sorted as its card always sorted them (by their order, equals as found), provider by
  provider (`InboxMath.Order`). A row is one sentence with its names as links (`LinkText.Line`), a second line in small
  print (what it is, its newest change, or its steps; 40 high, else 26), a note on the right, its act (a small button,
  greyed with why not), and the pin where it names a thing. A section shows six rows (the plan two) and "and 3 more",
  which opens the rest in place ("Show fewer" folds them, kept for the session); its heading opens its view (Under way:
  Here and away; Coming: the calendar; How people are: Moods; The plan: Planning's Plan). Following and the plan show
  only with something in them.
- **The one count** (`Inbox.Count`, at most twice a second): what Needs you's providers count. Rimfolk's own count what
  0.83's `NeedsCount` counted (every colony's petitions, the open wishes, Rimfolk's letters waiting, promises at risk), so
  with no new asks the number is the same (`InboxTests`), and the region's and diplomacy's asks add to it. It shows on
  This week's tab (`WeekModel.NeedsCount`), as the stat in its header, on the inbox in every window's corner
  (`UiKit.CountBadge`), and on the council button on the bottom bar (`MainButtonWorker_Council`, the button's own
  worker in `Council_MainButton`; the setting `inboxCountOnButton`, on at first, on the How you're told page).
- **What waits, one list** (phase 6 of the review, the player's decision 16 of 6 November 2026: "the on-screen council
  panel reads the inbox"). What waits on the player was worked out four times: This week, the on-screen council panel
  (its own lists of wishes, petitions, promises and letters), the colonist bar's badges (their own loops over the wishes
  and the promises) and the game's alerts. Now Needs you is the one list. `Inbox.Waiting` (`InboxWaiting`, at most once
  a second) asks Needs you's providers for their rows and counts, as This week does, and indexes the rows by colonist.
  A row says what it is about (`WeekRow.about`: the `Wish`, the petition, the `Letter`, the `GrantWatch`), how pressing
  it is on the panels' scale (`rank`: `HudRank.Wish` by the patience left, `HudRank.Question` by the hours before a
  letter goes, 270 or 230 for a promise at risk, else worth knowing, `InboxMath.WaitingRank`), the panel's kind it shows
  under (`part`, else its provider's `InboxProvider.part`, else the other asks: `InboxMath.PanelPart`) and whether it
  waits at all (`waits`: the evening news' row asks nothing). The council panel (`Hud/HudCouncil.cs`) draws one line
  per kind (wishes, petitions, promises at risk, letters waiting, the other asks of the region and the peoples, a new
  part `asks`), its count, ranked by its most pressing row (`InboxMath.MostPressing`, `PanelScore`), the rows in its
  tooltip and that row's own, and a click that goes where that row goes; its own walks over the wishes, the petitions
  of the colony in view, the promises and the letter stack are gone, and a promised wish counts with the wishes (it was
  counted twice, as a wish and a promise). The bar's wish and promise badges read the colonist's own rows
  (`Inbox.Waiting.Of(p)`), with the badge's own patience threshold; the ring, strain, faith and leaving read the person
  as before (how someone is, not what waits). Fixed with it: a promise taken back past its days for amends no longer
  waits (`AtRisk`; it showed in This week until its watch closed, while the badge had already dropped it), and wishes
  and petitions are counted as their rows list them (a wish with no asker, a petition with no decree). The game's alerts
  stay the game's, Rimfolk's wishes alert hidden while the panel shows the wishes as before. Tested in `InboxTests`.
- **Following** (`UI/FollowMath.cs`, `WeekMemory.following`, `UI/FollowWords.cs`): a pin in every card's head (and on a
  row that names a thing, the People table's rows, a wish card for the wish) follows any thing a card shows: a
  colonist, anyone outside, a place, a people, a house, a tie, a letter, an entry of the history. At most twelve
  (`FollowMath.Max`; a thirteenth is refused with a word why), kept with the game as their text forms ("p123", "t55";
  a model in `Tools/save_estimate.py`); an older save's pinned colonists (by ThingID) are followed on loading
  (`FollowMath.TakeOldPins`). Each shows at the top of This week with its newest change in one line, built once a
  second while something shows it: a colonist's strain, feeling and wish with their mood; anyone or anything else the
  history's newest line about it in the last fifteen days, else where it stands (a person's regard, a town's fortune or
  stance, a people's stance, a tie's state, a letter's step); a thing gone says so until it is let go. The pin in the
  corner lists them (each opening its card) and switches the Following panel (`Hud/HudFollowing.cs`), a ninth on-screen
  panel beside the other eight (off at first, under health in the second column; five panels to a row in the settings
  now).
- **Search** (`UI/Search.cs`, `UI/SearchMath.cs`): the magnifying glass in every corner and the key `Council_Key_Search`
  (`Defs/Misc/Rimfolk_SearchKeys.xml`, unbound) open `Window_Search` under it (never pausing, shut by Escape or a
  click outside). Its index is made when it opens, never before: the colonists, the people the notables and the ledger
  know, the homes, every named town and the raiders' camps found, the peoples in sight, every view, mode
  and lens of the four windows by its name ("Bonds", "Map: Trade"), the Almanac's articles (by title and keywords); and
  whatever `Search.Register` adds. As the player types it is filtered (names folded: any case, no accents or apostrophes,
  no link markers; scored by the whole name, its start, a word's start, every word of the query, inside a word, then
  other words), grouped by kind with the best group first and six of each; Enter takes the one lit (the first), the
  arrows move it. A result opens the thing's card (the drawer in sight, else the floating card), the view, or the
  Almanac's window.
- **Icons**: `Inbox` (a tray) and `Search` (a magnifying glass) (`Tools/make_icons.py`).

#### One look across every window (UI 5b of docs/design/interface.md)

The fifth release's second part: the design's visual grammar ("The look") across every page of Rimfolk's windows, so
they read alike. It is how things read, not what they do: every page keeps its acts, and every old address forwards.

- **The tokens** (`UI/UiKit.cs`, one block between "Tokens" and "End of the tokens"): Ink (white), Faint, Dim, Heading,
  Gold, Link (with LinkLine and LinkHot), Warm, Cool, Sour, Soon, Danger, Done and World; the surfaces CardFill,
  CardHover, CardSelected, CardBorder, ZebraFill, DividerColor, MeterBack, MeterEdge, Shade, Ground, PortraitBack,
  DrawerFill, PreviewFill, PreviewEdge and BadgeFill. The names the pages used before are tokens now (Good is Warm, Bad
  Sour, Warn Soon, Info Cool, Neutral Faint, Quote Ink, HeaderColor Heading, DreamColor and LossColor Faint,
  StrangerColor Cool, WorldColor World), so code that reads them (stage 2's and other mods' included) draws the same
  set; new code uses the design's names. Around them: `WithAlpha` (a token fainter; it changes only the alpha),
  `Quieter` (a step toward Faint: "likes" beside "loves"), `Wash` (a token as a faint wash behind a card) and
  `LevelColor(AdviceLevel)` (harm now Danger, high Sour, medium Soon, low Faint, an idea Cool), which the plan,
  priorities, base and defense pages read instead of their own switches. The colour helpers map to tokens: ties
  (friends warm, rivals sour, comrades cool, a mentor gold), a decree's reactions (loves warm, likes a quieter warm,
  dislikes a quieter sour, hates sour), the chronicle's kinds (good news warm, what came to nothing faint, the council's
  choices gold, strangers cool, the world's news World), urgency. No page makes a colour (`LookTests` reads every file
  under UI/ and the pages elsewhere for `new Color(` and the named colours). A picture's own colours stay (the region
  map's land, the base's and the defense window's little maps as pixels, their legends reading those same colours), and
  so do the game's (a people's or an ideoligion's colour on its icon). The page accents gave way: the Peoples cards'
  violet names are link ink (they are links), the world's panels' tan and violet the world's colour, the journeys'
  sage the heading's, the calendar's ten ways of keeping a day gold (remembrance faint), dreams by their kind (sweet
  warm, strange cool, a nightmare sour, an omen gold), the mood and strain bands (fine warm, near the minor break Soon,
  major Sour, extreme Danger), the defense lines' steps in the steps' colours (done Done, the one it is at Gold, what
  waits Cool, later Dim). The on-screen panels' mood bar and their meaning colours read the tokens too; their
  backgrounds (the settings' looks) stay theirs.
- **The grid** (`UiKit`): Padding 12, SectionGap 16, CardGap 8, CardInset 10, RowDense 22, RowStandard 28 (what
  `RowHeight` is now), RowPortrait 40, CardHeadHeight 56; icons IconDense 14, IconRow 18, IconHead 24. This week's cards
  8 apart with a heading of 32 and rows of 28, 40 with a second line (`CouncilLayoutMath`); the pages' gaps between
  sections and cards and their rows on the same steps.
- **The components, drawn one way** (`UI/UiKitLook.cs` beside UiKit and UiKitCards): every section heading through
  one line (`HeadingLine`: the fold's caret, an icon in its meaning colour, the name in heading ink, the count on a
  small plate, a note on the right, the line under it), which `SectionTitle` (with a count now), `FoldTitle` and
  `SectionHeading` (with an act on its right now, greyed with why not) all draw; a tag (`Pill`, `Pills`) as a state chip
  (a quiet frame, a dot or its icon in its colour, the word in ink, in link ink for a tag that names a thing; the dot
  only where the colour says something), `StateChip` the same; steps with the one it is at in gold; the empty state
  with its icon dim and its sentence in a dashed frame (`DashedBox`), and `EmptyLine`, the same in one row for a section;
  the row (`Row`: zebra, a portrait or an icon, the line with its links, a second line in small print at 40, a note on
  the right, room for an act); `RowAct`, a row's small act (This week's), greyed with why not; `WhyNotLine`, the line
  under a card's acts ("Plan it: ...", made once per act and reason with the cards' `CC_Card_WhyLine`); the line of days
  (`DaysLine`: 7 or 30 days, the area under it filled and its last point marked; `DaysHeading`, `DaysWord` rising,
  falling or steady, `DaysColor`, `DaysLineWithWord`); filter chips with a count (`FilterChip(rect, label, count, on)`);
  `NameLink`, a name standing alone as a link. Words measured once (`KeptWidth`), counts' words made once: nothing new
  is made per frame.
- **The pages through them.** This week: the sections' headings with their icon and count, an empty section as an
  empty line, "and 3 more" with its caret, the rows' acts through `RowAct`, the mood balance warm and sour. People: the
  table's filters as chips with their counts and its numbers right-aligned; Moods with those closest to breaking as rows
  of 40 (the portrait and name to the card, the rest to the Mood tab), the colony's mood this week as a line of days with
  its word, Compact or Details as the modes switch, and the strain list the same way; Bonds with List or Web as modes,
  the sections' counts, each bond's two names, a vendetta's avenger and people, a nemesis's victim and a secret's people
  as links made with the model; Faiths with a card's name in ink and the faith's own colour on its icon, Asks and Sways
  as sections with counts and names as links, a believer's name in link ink, other mods' buttons greyed with why not and
  a line under them; Renown with counts and empty lines. Council: the decrees' filters and order as filter chips, the
  glance and each kind as folding headings with counts, Enact greyed with the reason as its tooltip and in a line under
  it (Force below where it can be forced), who asked as a link, the offices' heading with "Call an election" on its
  right (greyed while one runs), the seats as rows with small acts and their holders as links, the election's seats the
  same; Ambition with "Plan it" and the meeting's acts greyed with why and its history's names as links; Ways with its
  bars' ends warm and sour; Wishes with Listen greyed with why (once a day), the asker as links, an envious or objecting
  colonist's tag a link, the acts at the design's height; Dilemmas with filter chips and counts, the waiting letters as
  rows with the day they go (never a time left), who answered and the question's names as links. Colony: the overview's
  tiles with no page colour and a line of their last week each, in the colour of where it went, its graphs in the colour
  of their week, its cards of lines under headings with names as links; the calendar's "Name a day" greyed with why, a
  day's menu behind the More icon, a day's name and origin naming the colony's people as links; the chronicle's filters
  as chips, every colonist an entry names as a link (`ChroniclePanel.TextOf`, which the council's pages share), the
  words in ink beside the kind's colour on its icon. Planning: the levels' tokens, "Take them all" on the proposals'
  heading, Add greyed until there is a step; the priorities' and the base's level on its tag and edge with the title in
  ink and their acts through the kit; the stock, Colony Manager, shopping and building tables with their numbers
  right-aligned; Making's sections with counts and empty lines; a room plan the colony can't build with its acts greyed
  with the reason, the rooms at a glance under headings with counts. Defense: the levels' tokens, the lines' acts greyed
  with why and a line of the reason under them, never refused after the click. The region's views: Faces' Write and
  History's Clear greyed with why. The cards' why line through the kit; the Almanac's colours as tokens (its own
  buttons keep its text size; planning a room says why not under it).
- **Names as links where Rimfolk draws them**: the on-screen panels (`HudPanel.NameLink` and `LinkWords`): the mood
  board's, health's and work's rows draw the colonist's name as a link (a left click on it opens their card, the
  floating one, there being no drawer over the map; a preview after a pause; the row's own click and its right-click
  menu kept for the rest of it; its tooltip giving way while the preview shows), and the Following panel's lines, which
  already opened their cards, read as links (`HudLine.thing`); the hover card, whose name is a link where the mouse can
  reach it, in the corner (`PawnUnderMouse` keeps the card while a button is held over it; beside the mouse the card
  moves with it and its name stays plain); This week (the journey's travellers and where they go, who a play on the map
  is about); the council's pages (above). The game's own text stays plain.
- **Keys** in `Languages/English/Keyed/Rimfolk_Look.xml` (rising, falling, steady, the colony's mood this week, Enact,
  a step to write first). **Tests**: `Tools/Tests/LookTests.cs` (the token block and its names, the old names as tokens,
  no colour made outside the block in UiKit and none in any file under UI/ or the pages elsewhere, the accents gone, the
  grid's measures, the components and their one heading line, the pages without the game's bare buttons and tabs, the
  filter chips and modes, lines of days, numbers right-aligned, the levels by token, names as links in the panels, the
  hover card, This week and the council's pages, no bold in the keyed texts); `LayoutTests` at both window sizes as
  before.

#### The journey planner: who to bring and what to pack

The player's request (5 November 2026): a better overview of the colonists in "Plan a journey", so they can judge
who to bring, in what state they are, what to take, with the recommended picked for them. The planner is a dialog
(`UI/JourneyDialog.cs`, 1120 by 820 at most) drawn in the kit's grammar:

- **What it is for** (`Journeys/JourneyAdvice.Purpose`): read from the plan's destination, the colony's own errands
  first: an active mission at that town (`Missions.All`: a pilgrim's escort, a feast, word carried, a harvest, an
  apprentice, with its named colonist and number), a people's call to arms there (a defence pact's `PactMath.ParseArms`,
  a debt's Fighters need; `PactMath.MinFighters`), settlers waiting for the colony's caravan to their hamlet's place
  (`HamletMath.MinEscort`); then the place: a site (a raiders' camp's own points, else the site's threat points
  against the home's `StorytellerUtility.DefaultThreatPointsNow`; a distress signal or hidden parts are unknown and
  count as some danger; a precious lump wants Mining; a downed refugee a doctor), peace talks, a town (hostile: a
  high-danger fight; one that trades, a trading post, a trade request: trade; else a visit), anything else a place to
  look over. Its kind and danger show as state chips under the title with one sentence, the recommendation's size and
  what it leaves the home without, and **Pick the recommended** on the right.
- **Who to bring** (`Journeys/JourneyAdviceMath.cs`, pure, `Tools/Tests/JourneyAdviceTests.cs`): each colonist at
  home read into a `Person` (health, pain, walking, illness, bed rest, wounds, pregnancy; mood, break line, the heart's
  band; Shooting, Melee, Medicine, Social, Animals, Plants, Mining; weapon, quality, violence, best sharp armor; the
  road's weather against their comfortable range once dressed; addictions; carrying capacity; doctor, cook, grower,
  warden by their work settings, office, a baby's only parent at home, a lover who stays). `BetterNot` gives the
  reasons nobody else changes; `PartySize` the least and ideal number by the aim, the danger, the errand's own number
  and `KeepHome` (two in five grown stay); `Pick` takes the errand's named one, the lead (negotiator, speaker or talker,
  the best hands), a doctor where `WantsDoctor` (never the home's last), then the best `Score`s, passing over anyone
  whose going would leave the home without a doctor, a cook or enough defenders while another would do (placed better
  not for it) until the party's least number; `HomeLeft` names what a choice leaves the home without, for the
  recommendation and for the player's own picks. `PickBeasts`: a mount per traveller (the game's
  `CaravanRidingSpeedFactor`), pack animals for the load past 90% of the people's room.
- **The overview** (`DrawWho`): rows in the kit's grammar under section headings that fold with their counts
  (Recommended, Can go, Better not; the animals the same): a checkbox, the portrait and the name as links to the card
  with their previews, the reason in a line in its group's ink, and two lines of pills (health, mind, gear, the
  weather, needs, home; the journey's skills with the colony's best in gold). A row's click picks or leaves out; the
  player's picks are never changed but by their own press.
- **What to bring**: above the list, "Recommended for this journey" (`JourneyAdvice.Items`): medicine by the party and
  the danger (`MedicineFor`), silver (`SilverFor`), a town's trade request, goods (`PickGoods`: the stalls' first,
  then art, within 60% of the room left), weapons for the unarmed (a new line kind `arm`, group "arms": the unarmed take
  one up at the end of their Dress step, `JourneyRun.ArmJob`, marked in `plan.done`, counted as packed by
  `JourneyRun.Armed`), bedrolls; notes for what the stores lack. "Add" and "Add the recommended" put them on the list as
  the player's own lines (`JourneyAdvice.Apply`).
- **The load at a glance**: three meters at the foot (the load against the room, the caravan's
  `TilesPerDayCalculator.ApproxTilesPerDay` with this load, the food's days against the road's), the text lines below.
- **RimMarket's stalls** (`Integrations/RimMarketJourney.cs`, by name): `JourneyStock` lists the colony stalls'
  counters, counted with the stores (`ToHand`), in the picker and the recommendations, the stores first
  (`JourneyAdviceMath.TakeFrom`); in the Pack step `JourneyRun.StallJob` sends a traveller to the stall
  (`JobDriver_JourneyStall`, `Rimfolk_JourneyStall`) to take what the stores and packs don't cover out of its
  `StockOwner`, the sale line lowered as `Notify_Sold` would (refill lines kept) through RimMarket's own `SetOrder`, no
  takings, demand or sale recorded. `plan.stallTaking` (not saved) keeps two from fetching the same count.
- **Performance**: the model (`JourneyAdviceModel`) is built when the planner opens and at most once a second while it
  is open (the press of Pick the recommended builds it at once); rows measure their pills once per width; nothing runs
  while the planner is shut.

### The settings: five pages

The player's decision 15 of 6 November 2026 ("settings on five pages: how much happens, how hard, what's on, how
you're told, your other mods; presets covering every part"). The settings were seven pages by system (Council, People,
Heart and health, Events, Building, Interface, Advanced): "how much happens" was about twenty sliders over six of them,
"how hard" over three, ten switches for other mods sat under Interface beside the bar buttons, and the presets were made
before the region and diplomacy. Now five pages by what a player thinks about (`SettingsPage`, `UI/SettingsCatalog.cs`),
264 rows, every one keeping its field and so its saved key and value (`Tools/Tests/SettingsKeys.txt` holds the fields as
they stood at 0.113.0; `SettingsTests` checks each is still saved under its key with its type and default):

| Page | Rows | Sections |
|---|---|---|
| How much happens | 17 | the colony's pace (gatherings a week); the council's asks (wishes, how many open, dilemmas, meetings); people's lives (moments, social events, dreams, personal stories, personal touches, the outsiders' chatter); visitors, events and the region (encounters, Rimfolk's events by the storyteller or its own timer, world events, remote travellers, raiders' camps) |
| How hard | 26 | stakes (with downsides and rewards, time for wishes and ambitions, jealousy, promises taken back and their grace, quitting); moods and pressures; feelings and strain (how fast, the day off, the heart's good side); ailments and illnesses; realistic ways to die; hurts from everyday life (everyday hurts, a scenario's fights, world events that end in a fight) |
| What's on | 153 | the quick setup; the council; people's lives; bonds; the colony's story; strangers; Rimfolk's events; world events; the region; bonds with other peoples; neighbours and letters; then the tools: journeys, many colonies (fresh starts, newcomers), the weather, personal touches, the build menu, the stores, carrying and hauling, defense, the room planner, the automations, planning ahead; the deeper game hooks, this colony's era, checks and resets |
| How you're told | 73 | messages (how many, the colony news, the day's messages and letters, which letters, the notes in the game's letters, what each answer does); on the map and in the game's tabs (speech, the game's own touches, mood bubbles, mood insight, the inspector line, the Social tab's column); alerts; buttons and windows; names and cards; on-screen panels; hover card; colonist bar |
| Your other mods | 15 | Hospitality, RimMarket, Alpha Skills, Alpha Genes, RimWorld of Magic, Faction Territories and Vassalage, Move the Patient, faiths from other mods |

- **Rows keep what they need.** A row's `needs` (a switch on, an option chosen) still greys it with why; a row hangs
  indented under its parent only where the parent is on the same page (`SettingEntry.Indented`, worked out once), so a
  rate on How much happens stands alone while its switch is on What's on, and a row whose label continues its parent's
  ("... including ones that can end in a fight") reads "Rimfolk's events › including ones that can end in a fight"
  there. The wishes' own switches (from a raid, reminders, self-help, for others, petitions) hang under Wishes now.
  The deeper game hooks keep every row that needs them greyed while off. The search finds every row on every page.
- **Your other mods shows only the mods loaded** (`SettingEntry.mods`, package ids as the integrations check them;
  `SettingsAccess.ModsLoaded` with `People.ModActive`): a row for a mod not loaded isn't drawn on the page, and the
  page's foot names those mods ("Rimfolk also has settings here for ..., shown when they're loaded"); the search still
  finds such a row, greyed "Only with RimMarket loaded." Guests shopping at the market needs both RimMarket and
  Hospitality. A faith's precept against turrets is any mod's, so it always shows.
- **Presets reach every part.** Light touch, story-rich and demanding between them set every rate on How much happens and
  every knob on How hard (`SettingsTests`), and say how you're told: light touch slows every rate, half the travellers,
  raiders' camps and outsiders' chatter, one gathering a week, more time for wishes and ambitions, no strain from
  diplomacy against someone's nature, a scenario's fights a quarter softer, 8 messages and 3 letters a day; story-rich
  switches on every part of the colony's life and its world (the region and the bonds with other peoples among them),
  half again as many personal touches, travellers and chatter, a quarter more raiders' camps, three gatherings a week,
  16 messages and 6 letters a day, and only ever switches on what is on by default; demanding adds a shorter grace for
  what was given and is gone, and a quarter more illness, everyday hurts and a scenario's fights. Default puts back
  exactly what the three change. The quick setup at the top of What's on (Everything on, Council only, Advisor only)
  reaches every switch of the parts of the colony's life and its world (`SettingsCatalog.SystemSections`, 89 switches
  with the region's and diplomacy's): Everything on turns on all of them, the switches under them too; Council only
  keeps the council and the colony's story; Advisor only the advisor and its plan; a part's own switch goes off and the
  switches under it keep their values for when it comes back. The tools (journeys, the weather, building, the
  planner, the automations, planning ahead) are the player's own and no preset touches them. Each preset's tooltip says
  what it covers (`_TipWhole` keys).
- **Fealty's switch hangs under the pacts'** ("... and pacts of fealty", "Pacts of their own kind" under Diplomacy).

### A colonist's tabs

A colonist's inspect pane has four Rimfolk places, each a page of folding sections built a couple of times a second
and drawn from that model (`UI/PawnPane.cs` lays out the Culture tab and the column; StoryView the Story tab;
MoodCompact the Mood tab), and a line in the game's Needs tab:

- **Story** (`ITab_Pawn_Story`): who they are and what they've lived (standing, character, what shaped them, traits,
  life goal, personal story, quirks, heirlooms, vendettas, nemeses, dreams, journal). `StoryView` takes a `StoryParts`
  filter: the roster passes `All`, the tab `StoryTab`, which leaves out the Culture tab's parts always and the
  column's parts only while the column can show (`SocialColumn.Available`), so nothing is lost with it off.
- **Culture** (`ITab_Pawn_Culture`, added in code to every humanlike race right after `ITab_Pawn_Social` in
  `inspectorTabs`, so it's drawn just left of Social): ideoligion (the vanilla icon, certainty and role, then
  `FaithCharacter.Details`), views, how they sit with the colony's ways (`CharacterTracker.Approval`, its reasons,
  `ColonyTraits.PrideOf`), their reaction to each decree in force and their own petitions, and where they come from
  (backstories, xenotype, a prisoner's faction or the kind of people the game made them among, time with the colony,
  founder, born here, or the game's "recruited" tale). Its button asks only a cheap check (a backstory, an ideoligion or a
  xenotype).
- **Social**: the game's own tab with Rimfolk's column (`SocialColumn`) to the right of its card. Three patch classes,
  each applied on its own:
  - `Patch_SocialTabColumn` on `ITab_Pawn_Social.FillTab`. Its prefix sets the tab's protected `size` (a FieldRef on
    `InspectTabBase.size`) to the tab's own width plus a gap and the column (`TabFitMath.ColumnWidth`: 360, less on a
    narrow screen, none under 300), or back to its own width when the column doesn't apply. The tab's own width is
    what it had before Rimfolk touched it (`TabFitMath.BaseWidth`), so a width another mod gives it becomes the base.
    It notes whom the tab draws for; the postfix draws the column only when the window really is wide enough and the
    game's card really was kept to its width this frame; a finalizer forgets it all, even when the card throws.
  - `Patch_SocialCardWidth` on `SocialCardUtility.DrawSocialCard(ref Rect rect, Pawn pawn)` (priority first): while the
    tab draws for that colonist, the card keeps the tab's own width, however FillTab sizes it.
  - `Patch_SocialRowTip` on `SocialCardUtility.GetPawnRowTooltip` (postfix): the game's own tooltip on a relation row
    gets one Rimfolk line about the pair (bond, shared group, memories, views agreed or at odds), kept a second per pair.

  The column needs both of the first two (`SocialColumn.Patched`), the setting, the deeper game hooks and a wide enough
  screen. If the tab stays narrow for a second and a half while the column wants room (another mod keeping its own
  width), it is switched off for the session with one log line, the tab keeps its width, and the Story tab shows the
  social parts again. Guests (Hospitality) and other people's pawns never get the column. "Recent talks" reads the
  game's play log (Rimfolk's interactions and social events concerning them), so nothing is saved for it.

- **Mood** (`ITab_Pawn_Mood`, with the council's Moods page through the same `MoodCompact`): opens compact. The
  player said a colonist with 20 or 30 moods was a long scroll of cards they had to hover one by one, so the tab sorts
  moods by what the player can do and gives each one line. `MoodSortMath` (pure, tested) holds the rules: a good mood
  is *Lifting*; a mood that never fades goes with *Ideoligion and who they are*; a mood whose hint is `actionable` (doing
  something now ends it, or keeps it from coming straight back: a table, the next night in a real bedroom) is *You
  can fix*, even when a belief or trait asks for it (an altar, a greedy colonist's bedroom); other moods from a
  precept (`sourcePrecept`, or a thought some `PreceptComp_Thought` gives) or with `requiredTraits`/`requiredGenes`
  are *Ideoligion and who they are*; memories are *Will pass*; a mood that lasts while its cause does is *You can fix*,
  unless its hint says it only passes (a world event). Each `MoodHintDef` with a `fix` has an `act` (2 to 6 words, at
  most 40 characters, no full stop; a test checks every one) and `actionable` (default true; false on 75 of 184).
  Moods without a hint get a short line by kind (`MoodSortMath.FallbackOf`). The model is built every half second
  with the cards' texts; rows are drawn only in sight, each laid out once per width (`MoodSortMath.RowLayout`), and a
  click opens the row's card (`MoodView.Draw`) in place, one at a time. The heart section moves under the moods,
  folded. "Details" switches back to the cards (`moodDetails`, kept in the settings file, not on the settings page).
  On a short tab (`MoodSortMath.StateFixed`) the state sentence scrolls with the moods, so at least six rows show at
  768 pixels with RimHUD at 400.
- **Needs** (the game's tab): `Patch_NeedsTabMoodHints` (a transpiler on `DrawThoughtGroup`) adds Rimfolk's lines to
  each mood's tooltip; `Patch_NeedsTabMoodSummary` (a prefix on `NeedsCardUtility.DrawThoughtListing(Rect
  listingRect, Pawn pawn, ref Vector2 thoughtScrollPosition)`, present in 1.6 and 1.7) draws one line of group totals
  at the top of `listingRect` and moves its top down by the line, in the layout pass too, so the game's list and
  scroll view start below it; a click opens the Mood tab. Its `Prepare` checks the method's first two parameters (a
  `Rect` named `listingRect`, a `Pawn` named `pawn`); if they differ, nothing is patched and the bootstrap lists it
  as skipped. It never skips the game's method and draws nothing for pawns without a Mood tab.
### The colonist bar's badges

`ColonistBar/BarBadges.cs` draws small badges on the game's colonist bar so the colony's state shows without clicking
into each colonist: close to a mental break (a soft ring, `BarRing`, tinted amber to deep red with its strength
following how close, or a dot among the badges), about to leave (`PawnStanding.leavingTick`), an own open wish past
the set share of its patience (`Wish.Elapsed`, or on its last call), strain worn down or near breaking
(`Heart.ForView`), wavering certainty (`Faiths.Wavering`, so only with Ideology and the setting) and a promise at risk (a
`GrantWatch` missing or taken back and still within its amends window). A calm colonist shows nothing. The wish and
the promise are what waits on the player, so since phase 6 of the review they are read from This week's Needs you, the
colonist's own rows (`Inbox.Waiting.Of`, "What waits, one list" above), as the council panel and the council button read it.
`BarBadgeMath` holds the parts without game types, tested in `Tools/Tests/BarBadgeTests.cs`: the break level and
closeness against the colonist's own `MentalBreaker` lines, the ring's colour, each kind's step, one priority table
(`Priority`: under the major line and leaving first, wavering certainty last) with an allocation-free sort, and the layout
(from the chosen corner down the side as far as fits above the game's own icon row, then the next column inward,
never more than fit on the portrait).

Two patch classes, each applied on its own:
- `Patch_ColonistBarBadges`, a postfix on `ColonistBarColonistDrawer.DrawColonist(Rect, Pawn, Map, bool, bool)`, which
  the bar calls only to paint. It reads a cache keyed by `thingIDNumber`, worked out at most once a second per
  colonist and started over when `Current.Game` changes; the draw allocates nothing, fades with the portrait (messages,
  another map) and writes a tooltip only while one is hovered.
- `Patch_ColonistBarBadgeClicks`, a prefix on `ColonistBarColonistDrawer.HandleClicks`, which the bar calls for every
  event before it paints. A left mouse-down on a badge is used there (`Event.Use`), so the bar's selection (the
  selector's drag box, later in the frame) and its reordering never see it; every other event passes untouched. Without
  this patch the badges show and explain without clicks, and their tooltips leave the click out.

Both signatures are the same in 1.6 and 1.7 (ApiDiff). The settings (How you're told, "Colonist bar") switch each kind,
the break mark and its margin above the minor line, the wish threshold, size, corner and the most shown.

### On-screen panels and the hover card

The player asked for "more things visible directly on the screen without always having to go to a menu", the mood of
the pawns first, in places other than the alerts column, movable and switchable; then (0.9.0's feedback) the game's
alerts in a movable panel "instead" of the game's list, health, work and idle, and the panels always behind windows; then
(0.9.3's) "I don't want the HUD elements to disappear when there's game windows in front of it, but they should just go to
the background so that I can still see them", the wishes alert hidden where the council panel repeats it, more health
details ("whether it's tended, how far the healing process is"), and a movable inventory "with different styles". The
rule for all of it, from the player: the most impactful and relevant data comes first. Everything lives in
`Source/ColonyCouncil/Hud/`.

- **One ranking** (`HudRank`, pure, tested): urgency times impact in three tiers that never overlap. Life and limb
  (300 to 399, in the player's order: bleeding by hours to death, a life-threatening stage not yet immune, starving,
  a mental break, under or within 2% of the minor break line), soon (200 to 299: falling towards a break, within the
  at-risk margin of it, downed, hungry, exhausted, strain worn or breaking, a wish at its last call or under a quarter
  of its time, food or medicine low, a question unanswered within a day), worth knowing (100 to 199: the moods
  dragging someone down, `3 x size`, times 0.4 when the mood passes on its own; a wish waiting; certainty wavering;
  strain). The mood board sorts by `MoodRisk`, the hover card shows the top lines of `HudConcerns`, the vitals and
  the council's lines take their colour from the tier (orange soon, red life). `HudMath.Keep` cuts rows for space:
  everything at life and limb stays, then the highest of the rest, in their order, and the panel says "and N more".
- **A background layer** (`HudPaint`, `Patch_HudPaint`). The panels are painted beneath all of the game's own interface,
  windows and non-window readouts alike, and above the map. The order relied on, within one repaint of
  `UIRoot_Play.UIRootOnGUI`: `MapInterface.MapInterfaceOnGUI_BeforeMainTabs` draws the map's overlays first
  (`ThingOverlays.ThingOverlaysOnGUI`: pawn labels, health bars), then every map component's `MapComponentOnGUI`
  (`MapComponentUtility.MapComponentOnGUI`: other mods' map labels and overlays, Rimfolk's defence labels), then the
  colonist bar, the drag box, designators, targeter, the things' tooltips, the bottom-right controls with the letters,
  and the resource list; after it the main buttons and the game's alert list, the tutor, and last the window stack
  (`WindowStackOnGUI`: every window's `ExtraOnGUI` first, where the inspect pane draws its tab buttons and gizmos, then
  every window). The panels paint in a postfix on `MapComponentUtility.MapComponentOnGUI`, on repaint only, in the
  window stack's order, each in a `GUI.BeginGroup` at its place. The evidence: Interaction Bubbles and RimTalk draw
  their bubbles from a postfix on `MapInterfaceOnGUI_BeforeMainTabs` and RimTalk's chat overlay from a
  `MapComponentOnGUI` ("below the UI", with an option to draw it from a `UIRoot_Play` postfix "above"), RimHUD's
  floating panel from a prefix on `MapInterfaceOnGUI_AfterMainTabs`, and the inspect tabs are drawn in the inspect
  window's `ExtraOnGUI` (RimHUD replaces `InspectPaneUtility.DoTabs`, called from there). The order is also checked as
  the game runs: the resource list's patch (`Patch_HudGameResources`) notes when it draws, and if it ever drew before
  the panels in a frame, the early pass stands down for the session with one warning. **Fail-safe**: a panel's window
  paints the panel itself, exactly as before, in any frame the early pass didn't paint it (the hook couldn't be applied,
  the deeper game hooks are off, the world map is up, or the order check stood it down). Screenshot mode paints nothing.
  The game's messages (top left) are drawn as immediate windows (`Message.Draw`), so they stay above the panels too, as
  do tooltips, float menus and the hover card.
- **Windows for input** (`HudPanel : Window`): each panel keeps its window, for input only. `DoWindowContents` runs the
  same drawing code for every event: on repaint, when the early pass painted, it only hands the tooltips over (kept
  during the paint where the mouse was, then `TooltipHandler.TipRegion` from inside the window, so they show only where
  the panel is the window under the mouse, never through another window); on mouse and key events it takes the clicks,
  the drag and the menu. The panels draw through `Over`, `Clicked`, `Tip` and `ImageButton`, which paint in the early pass
  (no `GUI.Button`, so no control ids outside windows on repaint) and take input in the window. **Stepping aside**
  (`HudGameUi`): where the game's own interface is drawn over a panel, that interface must get the mouse, but a window
  under the mouse would take it (and the game's `Mouse.IsOver` is false wherever any window is under the mouse). So
  once a frame, while the mouse is over a panel, the places the game draws outside windows are worked out: the main
  buttons along the bottom (35 px), the colonist bar (the union of `ColonistBar.DrawLocs` and `Size`, with the names
  under them; not while the play settings hide the bar), the letters and the date, time and play settings under them
  (`GlobalControls.Width` against the right edge, from `LetterStack.LastTopY` down), the game's alert list while it
  shows (`Alert.Width` by `AlertsReadout.AlertsHeight` above the letters), the resource list while it shows (150 px by its
  `lastDrawnHeight`, read by name), and with the inspect pane open its tab row (`HudMath.TabRow`: `TabWidth` per visible
  tab from the pane's right edge, the whole row with a tab open) and the gizmos beside it (`GizmoGridDrawer
  .HeightDrawnRecently`). If the mouse is in one of them, the panel's window shrinks to no size for that frame, so the
  game's interface there gets the highlight, the tooltip and the click; the panel stays painted. Anything that can't
  be read is left out (the panel then takes the mouse there). The inventory's search box is the one exception: while
  it's typed in, its panel keeps the keyboard (`KeepsKeyboard`: the window order leaves it the focus, it doesn't step
  aside, and it holds the camera still), and it is drawn by the window, since a text field needs the window's keyboard.
- **The windows' own settings**: layer `GameUI`, no close button, no game background or shadow,
  `focusWhenOpened`, `closeOnAccept`/`closeOnCancel`/`closeOnClickedOutside`, `absorbInputAroundWindow`,
  `preventCameraMotion` and `forcePause` all off, not drawn in screenshot mode. Being windows, clicks on them don't
  reach the map. They are not `draggable`: the game's drag-anywhere can't snap, clamp or honour the lock, so a panel
  starts a drag itself (on its bar or an empty spot, on mouse down) and follows the mouse in `WindowUpdate` until
  `Input.GetMouseButton(0)` lets go, snapping each frame (`HudMath.Snap`: screen edges and the other panels' edges
  within 8 px) and saving on release. The bar (drag, compact, background, options, hide) is added above the body only
  while the mouse is on the panel (below it at the top of the screen), so the body never moves and no invisible
  strip blocks the map. The bar has no tooltip of its own (the player found it in the way); its buttons keep theirs.
- **Behind every other window** (`HudWindowOrder`, from `HudManager.Update` once a frame). The panels share the game's
  interface layer with the main tabs, the inspect pane, Rimfolk's planners and most mods' windows, and in a layer the
  window opened or clicked last goes on top: a panel reopened after the room planner covered its buttons. The game
  has no way to send a window back, so when any panel sits above another window in `WindowStack.windows` (the list
  `GetWindowAt`, `GetsInput` and the draw order follow), the panels move to the bottom of the list in their own order,
  a panel that took the focus by a click hands it back to the topmost ordinary window, and the stack's
  `updateInternalWindowsOrderLater` flag makes the game re-apply the order (`GUI.BringWindowToFront` in list order) at
  the end of its next pass. A clicked panel is on top for that one frame; the click itself works. All three fields are
  found by name (ApiDiff checks them on 1.6 and 1.7); if one is missing, it logs once and the game's order stays.
  Behind a window a panel never takes input meant for it: on any mouse or wheel event where `GetWindowAt` under the
  mouse isn't the panel, `DoWindowContents` returns before drawing anything clickable, the bar shows only when the
  panel is the window under the mouse, and the tooltips go through `Mouse.IsOver`, which is false where a window
  covers it. "Hide them under big windows" can still close them for the Work tab and the like; it is off by default
  since 0.9.4, as the panels now sit beneath every window (a one-time migration, below, switched it off for players
  who had it on by default).
- **Places** are saved as the nearest corner's point as fractions of the screen (`HudMath.ToFraction`), so a panel
  against the bottom or right edge stays there at any resolution and grows away from it; `FromFraction` keeps it on
  the screen. Each panel's state (`HudPanelState`: on, place, compact, scale 0.75 to 1.5, opacity, rows, whom, order,
  range, the parts switched off) is a list in the mod settings (`CouncilSettings.hudPanels`, listed as not a setting
  in `SettingsCatalog.NotDrawn`, with the settings page's buttons opening the same options window as the panel's gear).
  The global options are ordinary settings rows under "On-screen panels" and "Hover card". Scale is applied by sizes
  and font choice (Tiny or Small by scale and the text-size setting), never by a GUI matrix. The state also keeps a
  panel's style (`style`: the inventory's look, the health panel's rows), the inventory's grid `columns`, `pins` and
  `folds`, and the alerts hidden in the alerts panel (`alertsHidden`, and `alertsShown` for those hidden by default).
- **Settings version** (`CouncilSettings.hudVersion`, `HudLayout.SettingsVersion`, applied in `HudLayout.Migrate` at the
  first game of a session): a settings file without it reads as 0. Version 1 (0.9.4): "Hide them under big windows" goes
  off once (the player asked to keep the panels in view behind windows; it can be switched on again), and the mood board
  and the trend where they used to start (against the left edge) move right of the inventory panel. A new player's
  settings start at the current version. The vitals and the council where 0.9.0 put them still move every session.
- **Lifecycle** (`HudComponent : GameComponent`): `FinalizeInit` (new game or load) forgets the last game's windows,
  which went with the old interface; `GameComponentUpdate` asks `HudManager.Update` once a frame to open or close each
  panel (shown, on, not failed, not hidden by the world map, or by a main tab covering 15% of the screen or more while
  "hide them under big windows" is on), takes the trend's sample when the in-game hour changes and, while the inventory
  panel is on, the stock's snapshot every six hours; `GameComponentOnGUI` hears the two keys and draws the hover card.
  Nothing runs per tick. Going to the main menu takes the windows with the game's interface.
- **Data**: each panel gathers at most twice a second while the game runs (every three seconds while paused, or at
  once after an option changes), and measures itself without the GUI (fixed widths, so `Measure` can run from
  `WindowUpdate`); drawing reads only what was gathered. The mood board and the hover card share `HudMood`'s cache: the
  levels, break, strain, wish and faith every 0.45 s per colonist, the mood lines (`MoodInsight.MoodOf`) every 2 s,
  staggered by ID. `HudMood.TopDrag` is the one place that picks "what drags them down most": by `HudRank.Drag` over
  the lines, with the hint's short action where the Mood tab would show one (`MoodSortMath.ShowsAct`). Any panel that
  throws is hidden for the session with one `ErrorOnce` (`HudManager.Fail`).
- **The trend** samples the colony's free colonists (as This week counts them) once an in-game hour: one packed int
  per hour (`HudMath.Pack`: hour x 10000, average and lowest in whole percent), the last 168 kept in the save
  (`moodSamples`, about 3 KB; `Tools/save_estimate.py` models it). The daily `history` only had the average.
- **The hover card** (`HudHoverCard`): on repaint only, finds the pawn from `UI.MouseCell()` and the cell's thing list
  (looked at again when the cell changes or every 0.1 s), never while a mouse button is held, a designator or targeter
  is active, the drag box is open, a float menu or input-absorbing window is up, a window is under the mouse, or the
  world is selected. After the delay (and with the modifier held, if one is set) it's drawn with
  `WindowStack.ImmediateWindow` in the `Super` layer, beside the mouse and never under it, so it never takes a click;
  in the corner it sits above the inspect pane. The card's content is kept half a second per pawn.
- **Where they start** (`HudMath.DefaultPlace`, tested at 1080p and 1440p with full-size panels: nothing overlaps,
  nothing touches the colonist bar at its widest, the mood board and the trend start clear of the resource list's
  column and above the inspect pane's tab row). Checked against the game's own interface: the resource list down the
  top left (it can be long), the colonist bar top centre, the alerts on the right above the letters, the date, time and
  play settings bottom right, the inspect pane bottom left with its tab row and the gizmos beside it. The inventory in
  the resource list's place (top left, where it stands in for that list); the mood board (27% down) and the trend
  (bottom at 78%) 160 px in from the left (`HudMath.LeftColumn`), right of it and of the game's list when that shows, and
  below the colonist bar's height; on the right edge the vitals (20% down) and the council (33%), above the alerts,
  which take the game's alert place; a second column 262 px in from the right, clear of the colonist bar, for work and
  idle (14%), health (41%) and, since UI 3, what the player follows (60%, `HudFollowing`, off at first: each thing
  followed with its newest change, `FollowWords`, then the wishes pinned; a click opens its card). Places are worked out for the screen at the time a panel's state is made. Old places move
  in `HudLayout.Migrate` (above); a panel the player moved stays. With the panels beneath the game's interface an
  overlap no longer hides anything of the game's, but the places still avoid it.
- **The alerts panel** (`HudAlerts`) mirrors the game's alert list, every mod's alerts included (Rimfolk's
  `Alert_CouncilAdvice` and `Alert_CouncilWishes` too), from the game's own `AlertsReadout.activeAlerts` (a
  `FieldRefAccess`, read twice a second): nothing is worked out twice. Rows: the alert's cached `Label`, in red, orange
  or plain by `Priority`, with a stripe in that colour and the game's critical pulse; in the game's order (critical,
  high, medium, and the game's own order within each) or the newest first; cut by `HudMath.Keep` with critical alerts
  never cut (`HudRank.Alert`). A count ("×3") from the alert's report (its culprits, read at most every two seconds)
  where the label doesn't already carry a number (`HudMath.ShowCount`). The tooltip is `GetExplanation()`, worked
  out only on hover and kept half a second. A left click calls the alert's own protected `OnClick` (by reflection, so
  it does exactly what clicking the game's alert does: jump to its culprits and cycle through them, or open a tab);
  a right click on a row offers "Hide this alert in the panel", then the panel's menu. New high and critical alerts
  (those the game bounces in with a bell) flash for three seconds; what is up when the panel opens doesn't. Until the player moves it, it has no saved place:
  `AutoPlace` puts it against the right edge with its bottom 4 px above `LetterStack.LastTopY`, so it follows the
  letters like the game's list (above the game's list, by `AlertsHeight`, when that still shows). The tooltip is the
  alert's label and explanation only (the "Click: ... Right-click: ..." help went, as the drag help did before).
- **Alerts hidden in the panel** (`HudAlerts.Hidden`): kept by the alert's class (its full type name) in the alerts
  panel's state; the options list them, each with "Unhide", and "Show all" (the names are the labels seen this session,
  else the class's name in words). Rimfolk's own `Alert_CouncilWishes` is hidden by default while the council panel is on
  and shows the wishes (it would repeat them); unhiding it is remembered (`alertsShown`), and with the council panel off
  it shows again by itself. Only the panel changes: the game's own list, when it shows, is never filtered.
- **The game's own alert list** is hidden while the alerts panel shows (`Patch_HudGameAlerts`, a prefix on
  `AlertsReadout.AlertsReadoutOnGUI` returning false; the setting "Hide the game's own alert list while the Alerts
  panel shows", on by default, as the player asked for "instead"). Only the drawing goes: `AlertsReadoutUpdate`, which
  recalculates the alerts and keeps `activeAlerts` (the game's logic and the panel depend on it), is untouched. It
  hides nothing while the panels are hidden, on the world map, with the game hooks off, or when the list can't be
  read; if the patch can't be applied or throws, the game's list stays.
- **The council panel** (`HudCouncil`): the plan's next step; what waits on the player read from This week's Needs you
  (`Inbox.Waiting`, "What waits, one list" under Rimfolk's windows): a line each for wishes (with whose has waited
  longest), petitions, promises at risk, letters waiting for an answer and the other asks of the region and the peoples
  (the part `asks`), each with its count, ranked by its most pressing row and going where that row goes; then the
  decrees in force, the next calendar day or meeting, and trust. Its parts (`HudCouncil.Parts`) switch each line, as
  before; a part switched off in an older settings file stays off.
- **The health panel** (`HudHealth`, rules in `HudHealthMath`, tested one exclusion at a time): a row for each colony
  pawn (free colonists and slaves; caravans and prisoners as options) with something that will pass or needs a
  reaction. `HudHealthMath.Shows` takes the facts of a visible hediff: never a lost part, an added part or implant
  (`countsAsAddedPartOrImplant`), a permanent wound, a chronic condition, pregnancy or labour (`def.pregnant`,
  `HediffWithParents`), or anything the game doesn't call bad (psylinks, auras, buffs, drug highs); then yes for
  bleeding, wounds, anything wanting tending, Rimfolk's ailments, illnesses (an immunity race, or `makesSickThought`)
  and anything with a lethal severity (hypothermia, toxic buildup, blood loss, whose severity is moved from outside);
  and no for the rest, so another mod's curse or magic effect is left out by its properties, not its name. The race
  uses the game's numbers: immunity from the comp, its gain a day from `ImmunityRecord.ImmunityChangePerTick`, the
  severity's change a day from every `HediffComp_SeverityModifierBase` (tending included); "will recover" when
  immunity comes first, "losing" when death would. Bleeding uses `HealthUtility.TicksUntilDeathDueToBloodLoss`. Order
  by the shared ranking: bleeding, or soon lethal (at a life-threatening stage, or three quarters of the way and not
  recovering) is life and limb; untended (255 to 279), losing a race (215 to 249) and something deadly getting worse
  (210 to 249) are soon; healing is worth knowing. A click selects them and opens `ITab_Pawn_Health`. A calm colony:
  "Everyone is well."
- **How far along** (the health panel's "Show details", on by default; maths in `HudHealthMath`, tested). For each
  condition: tended and how well (`HediffComp_TendDuration.tendQuality`) and when it wears off (`tendTicksLeft`, unless
  `TendIsPermanent`; orange inside three hours), or "untended" in orange (red while it bleeds). Wounds: how much has
  healed, from the severity at its worst while the panel has watched it (a cache keyed by the hediff, not saved, pruned
  each refresh; only for wounds first seen under an hour old, as otherwise the start isn't known and the HP still to heal
  shows instead), and roughly when it's healed, from the game's own healing (`Pawn_HealthTracker`: 8 a day, 4 more lying
  down plus the bed's `bed_healPerDay`, times the conditions' `naturalHealingFactor`, `InjuryHealingFactor` and the body
  size, shared at random among the wounds that heal on their own, and for tended wounds 8 more a day times 0.5 to 1.5
  by the tending's quality, shared among them; nothing while starving). Illnesses with an immunity race: "immunity 62%
  vs 40%" (two small bars on a line of their own) and "over in ~1.5 d" (until immune at today's pace, then until the
  severity is gone at the comp's `severityPerDayImmune` plus the other comps), or "losing". Rimfolk's ailments and other
  conditions that ease: the days left (`HediffComp_Disappears.ticksToDisappear` when it has one, else the severity over
  its fall a day). Bleeding: the rate a day and the time to death as the game reckons it. The rows: a summary line under
  each name in the conditions' colours ("Cut 60% healed · Flu immunity 62% vs 40% · Lice untended"; the same said twice
  counts once with ×2), or one line per condition (an option, up to five lines, the rest in the tooltip). The tooltip
  says all of it in full and no longer ends with the click help.
- **The work and idle panel** (`HudWork`, `HudWorkMath`): a row per colonist with the job report
  (`JobDriver.GetReport`) cut short (no full stop, cut at "to", "at", "from"... when two words come before, so
  "Tending to Bob" stays whole; the words are a translation key), an idle mark from the game's own check
  (`Pawn_MindState.IsIdle`, as its idle alert), how long they've been idle (kept by the panel from when it first saw
  them idle, not saved), food, rest and recreation as three thin bars red at the game's hungry, tired and low
  categories, and a mark for drafted, down or a break. Order: `HudRank.Work`, starving, a break, urgently hungry or
  exhausted, idle (longest first), hungry or tired, busy, drafted. Above the rows, only when there's something: how
  many are idle and who needs food or sleep (a click goes to each in turn), and work nobody does: at most every five
  seconds, for each of ten work types nobody able has switched on (and only then), one cheap look at the lists and
  designations the game keeps (a fire in the home area, someone to tend, blueprints, marked plants, rock, hunts,
  tames, prisoners, things to haul, a research project). Drafted colonists are an option.
- **The inventory panel** (`HudInventory`, rules in `HudInventoryMath`, tested): the game's own counts, as its resource
  list shows them (`map.resourceCounter.AllCountedAmounts`, read twice a second; the categories' totals from
  `GetCountIn`), for the map on screen or the colony's seat, with the game's icons (`Widgets.ThingIcon`, which carries the
  def's colour). Four looks, kept in the panel's `style`: classic (icon and count a line, the name on hover or as an
  option, on the far left in the game's list's place), grouped (the resource list's root categories,
  `ThingCategoryDef.resourceReadoutRoot`, in the game's order, each a folding header with its total; the folds kept),
  an icon grid (tiles with the count under them, `columns` to a row, for a strip along an edge), and essentials (meals
  with the days of food the vitals use, medicine, silver, steel, components, wood, then the pinned, larger and calm).
  In each: pinned items first (right-click an item; kept by def name), an optional search box (the game's
  `QuickSearchWidget`), items at zero left out unless asked (then with what ran out since yesterday), the order by
  category, name, count or the biggest change, rows before "and N more" (pinned ones always show). The change since
  about the same hour yesterday: `HudComponent` takes a snapshot of each home map's counted stock every six in-game
  hours while the panel is on (one packed line, `map|hour|def:count,...`, the last five a map, 15 in all, in the save as
  `stockSnapshots`; modelled in `Tools/save_estimate.py`), and "yesterday" is the newest snapshot 24 to 30 hours old.
  The tooltip (worked out on hover, kept half a second): the name and description, the stacks and where they're stored
  (`SlotGroup.parent.SlotYielderLabel`), the change. A click goes to the stacks in storage one after another, as the
  game's alerts cycle. **The game's own resource list** steps aside while the panel shows (`Patch_HudGameResources`, a
  prefix on `ResourceReadout.ResourceReadoutOnGUI` returning false; the setting "Hide the game's own resource list
  while the Inventory panel shows", on by default): only the drawing, never the counting; nothing hidden with the
  panels hidden, on the world map, with the game hooks off, or if the patch can't be applied or throws.
- **The game's tooltip under the card** (`Patch_HudGameTooltip`, a postfix on `Pawn.GetTooltip`, which the game calls
  from `TooltipGiverList.DispenseAllThingTooltips` only for the pawn under the mouse, on repaint): while the card was
  drawn for that same pawn this frame or the last (`HudHoverCard.ShowingFor`) and "Hide the game's tooltip for that
  pawn" is on, the tooltip comes back empty with a wait it never reaches; for anyone else, or with no card, it's the
  game's. If it can't be applied, both show as before.
- **The card never covers a window**: it isn't drawn while the mouse is over any window, and it tries the four sides
  of the mouse, skipping any place that would overlap a window other than the panels; with none free it waits. What
  would help ("→ Heat or cool the bedroom") goes beside the mood when both fit the card (now 292 px) and on its own
  line under it when they don't, so it's never cut short.
- **The trend's labels** never overlap or leave the panel: `HudMath.FitThree` places "3 days ago", the key and "now"
  (the key steps aside when there's no room) and the range and the latest values above; the y-axis' 100%, 50% and 0%
  sit in a gutter inside the panel. Until there are two samples it says "Collecting: the first points come after an
  hour" instead of an empty graph.
- **The switch** (`Patch_HudButton`, a postfix on `PlaySettings.DoPlaySettingsGlobalControls(WidgetRow row, bool
  worldView)`, through the per-patch bootstrap): on the map only, an icon (`WidgetRow.Icon`) with the on/off check like
  the game's toggles; a left click shows or hides, a right click opens the menu. It draws nothing with the deeper game
  hooks off, and if it fails or can't be applied, the key (`Council_Key_Panels`, unbound like the council's page
  keys) and the settings still work.

### Checking a new game version

One DLL serves every supported version: it is built against the 1.6 references into `1.6/Assemblies`, and on 1.7
the game falls back to that folder. A new game version can break that DLL in ways the compiler never sees, because a
built DLL finds every game method, field and override by exact name and signature when it loads, Harmony finds its
targets by name, and constants and enum values are baked in as numbers. To check a new version (1.7 as the example):

1. **Build against it.** Add the version to the `RimRef` switch at the top of `Source/ColonyCouncil/ColonyCouncil.csproj`
   (its `Krafs.Rimworld.Ref` version and target framework), then `dotnet build -c Release -p:RimRef=1.7` in
   `Source/ColonyCouncil`. It goes to `bin/1.7/` and ships nowhere. Errors are source changes the new version needs;
   obsolete warnings are members to move off.
2. **Check the shipped DLL against it**, from the repository root:
   `dotnet run -c Release --project Tools/ApiDiff -- 1.6/Assemblies/ColonyCouncil.dll 1.6.4871 1.7.4977-alpha.2`.
   It lists every type, method, field, constant, override, Harmony patch target (with the patch's parameter names)
   and by-name lookup the DLL uses that the new version lacks or changed, and where each is used. `--all` also lists
   the ones that are fine. Exit code 0 means nothing changed. It works for any two versions, or any two folders of
   reference assemblies.
3. **Check the XML against the new def classes:** the DefValidator with the new references, the new build and, for a
   game on .NET, the runtime's reference pack as the fourth argument:
   `dotnet Tools/DefValidator/bin/Release/net8.0/DefValidator.dll . ~/.nuget/packages/krafs.rimworld.ref/1.7.4977-alpha.2/ref/net10.0 Source/ColonyCouncil/bin/1.7/ColonyCouncil.dll <dotnet>/packs/Microsoft.NETCore.App.Ref/10.0.x/ref/net10.0`.
4. **If something changed,** keep one DLL where at all possible: avoid the changed member, or reach it through
   reflection with a fallback, inside a try/catch. Only if that can't work: a `LoadFolders.xml` with a version
   folder holding a DLL built with `-p:RimRef=<version>` (`Install Rimfolk.bat` already copies a `1.7` folder and
   `LoadFolders.xml` when they exist).
5. **Then** add the version to `supportedVersions` in `About/About.xml`.

What the tools can't see: what a transpiler expects inside a game method (reference assemblies have no method bodies;
Rimfolk's one transpiler, on the Needs tab tooltip, falls back to a postfix), other mods' types, and vanilla def
names (game data, not code). Those need a look in game.

**1.7 (October 2026, Krafs.Rimworld.Ref 1.7.4977-alpha.2).** 1.7 runs on .NET 10 with a new engine layer (the
UnityEngine types come from `UnityStubs`), but the game's own API is nearly the same as 1.6. The 1.7 build compiles
with no errors and no new warnings, and the DefValidator's report is identical. ApiDiff finds nothing the 1.6 DLL
uses that 1.7 lacks or changed: 883 types, 5,183 methods and fields (game, Unity and runtime), 1,115 constants, 1,333
overrides, 28 Harmony patches, 9 by-name lookups. On the types Rimfolk names, the only difference is 14 old
`TextureFormat` members it doesn't use. The other direction doesn't hold: a DLL built against 1.7 calls things 1.6
lacks (UnityStubs' own `Texture ==`, .NET's `MaxBy` and string-interpolation handler), so the 1.7 build must never
go into the 1.6 folder.

---

## Ticking

Every system keeps its own clock; nothing is merged into one scheduler. What they share is the list of ticks in
`Source/ColonyCouncil/Lean/Sched.cs`: each hourly pass has a tick of the hour to itself, at least 90 ticks from the
next, and none lands on a tick of the short 250-tick passes (Tools/Tests checks both). The council's hour, which used to
run twenty-odd systems on one tick, runs in four parts in its old order, and its day in two halves, so the first hour of
a day (where most daily work happens) is spread over the hour too. The per-colonist passes (the heart, ailments, health
stories) take a tenth of the colony every 250 ticks (`Sched.Slices`, by ID), so each colonist still comes round once
an hour and no tick carries the whole colony. "Tick" below is the tick of the hour (0 to 2499), of the 250-tick cycle
or of the day.

What many passes ask about a map is worked out once and shared (`Lean/MapFacts.cs`): whether a threat is on the map (as
of this 250-tick stretch), the homes and their seat (the same, `Lean/Homes.cs`), and for the hour the colonists at
home, the days of food, the medicine and the colony's beds. The first pass to ask in its stretch works it out; the
rest read it. The heart's map context, decrees, the life checks behind dilemmas, the advisor, ambition goals,
autonomy, encounters, social events, nuisances, the calendar, Rimfolk's events, world situations, scenarios, story
chains, nemeses, camps, play-outs, the fights pass and the worn-out episode read from it. (A downed victim after a fight is still judged at that moment.)
`Lean/Fresh.cs` holds the rules for how long such an answer holds; Tools/Tests checks them.

**Every hour** (2,500 ticks), in the order they come in the hour:

| Tick | Pass | Work |
|---|---|---|
| 0-30 | Visual touches, the hour's look (the first pulse of the hour; only with visual touches on) | the map in view: maybe one comet gazer, one ambient line or a hum |
| 64 | The neighbours by name (World/Notables.cs; with "Neighbours by name" on): traders and visitors seen at the colony, those kept held in the world; every six hours the towns within reach looked over (the gone followed, the missing made, two at most an hour); old age in the first hour of the day; then the letters' and calls' hour (World/NeighbourLetters.cs) | per notable (42 at most), per town in reach (eight), per letter or call under way (three) |
| 107 | The region's hour (World/Region/RegionBook.cs; with "New towns' faces" on, only while a town is being founded within reach; on a tick of the hour's first 250-tick stretch that no other pass uses, `Sched.HourlyOnCycle`, since the hour's own ticks are all taken 90 apart): each founding gets its founder (two made a day at most), its line in the news when begun, and its faces when built (its record, two founding families, the news line, the full welcome or the founder's letter) | per founding (six at most): a founder made now and then, a few ledger writes |
| 157 | Council: fights and losses, decrees, wish upkeep, promises kept (every other hour), event wishes, new wishes | per home map (a fight going on?), per open wish, per granted wish still watched (its own check, and a look at plans being built only when something is missing); a new wish rolls once, and only when it comes does it ask every wish of every colonist |
| 327 | A camp's hour (only while a camp is there) | the camp's members, trade, fees, its day's event |
| 502 | Council's day, first half (first hour of the day only): trust, standards, decree trust and petitions, anniversaries, names, journals, offices | per colonist (their memories, for the journal) |
| 677 | Health stories, the colony's part (with convalescence or reputation on): the cases of dangerous illness; pruning in the first hour of the day | per case (each colonist's part runs in its slice, below) |
| 852 | Council's day, second half (first hour of the day only): the social web, social events, life goals, secrets, vendettas, quirks, legacy, the colony's ways, marks on people's characters (illness, hunger, addiction, mastery, faith, peace), traits that change (the watches, then at most one change), the links between systems (the physician's rounds, the herdmaster, setbacks counted), the day's history sample, trust effects, bitter colonists | per pair of colonists (opinions, ties, circles), per colonist and quirk def |
| 917 | Links with other mods (only with one there): water and mending for strain and recovery | per colonist |
| 1027 | Council: ambitions, personal touches, dilemmas, annals, dreams, quirks | the ambition's goals on its map; per colonist (asleep or waking; a dream weighs every dream def for the one waking) |
| 1203 | World situations | per running situation (at most two); a new one rolls now and then |
| 1311 | Colony calendar | the home map: snow, the day's first look (harvest, deaths, rest day), talk, a day coming round |
| 1427 | Setbacks: the lasting ones (a wish still waiting, a decree dragging on, a sick friend, quiet days...), crops lost, rain on gatherings, noisy nights, long hauls, waiting for a doctor, guests eating from low stores, a day's mishaps turned to jokes; outshone, cooled friendships and hard weeks in the first hour of the day | per colonist (their wish, decrees, those close to them who are ill or away), per home map (gatherings, sleepers and who is at loud work nearby, hauls, beds), per skill once a day |
| 1544 | Council: nemeses, pets, the links between systems (pets and grief, the marshal after a fight, a fear faced), moments waiting for two to be together, PokeWorld, the animals' natures (a temperament's moment, friends and rivals, a grave, an old animal back, a speaking animal's line; Animals/), autonomy, who is home, which home is whose (a day on a home makes it someone's last home; a home founded or lost is told; Lean/Homes.cs), everyday pressures, what was done with outsiders (good words carried home, favours brought, bonds fading; Outsiders/) | per colony animal on home maps, per spawned colonist (a plan or a job now and then), per colonist (meals, work, their home: one lookup), per home map (crowded rooms, half-built frames) |
| 1660 | Reputation and gossip (only with "Personalities and social life"): in a fight, rescues under way and the brave's company; otherwise who idles while the rest work and who tends whom; in the first hour of the day, the day's idling and tending made deeds, the names that formed or faded told, the dead and departed let go | per spawned colonist on home maps (a job look; a sight check only for the idle, the brave and their side) |
| 1777 | Personal stories | per story under way; new stories in the first hour of the day |
| 1903 | Groups (with "Personalities and social life" and the social web on): work sampled, habits kept together | per colonist on a home map (their job); per group (its members up and about, together, doing its habit) |
| 1996 | Fields by the weather (Fields/FieldWeather.cs, a map component; with "Fields by the weather" or the frost harvest on): how much colder than the land's own reckoning the hour is, kept as the day's coldest; every other hour (every hour while a frost harvest stands, at once when a field's crop or setting changes) the forecast for each hour ahead from the game's own numbers, each field set to sow by the weather grown hour by hour, and the frost harvest picked plant by plant; the frost checks' coldest hour | per home map: one forecast (31 hours, or 120 days with a field set to sow by the weather), per field set to sow by the weather a few crop runs, per outdoor plant in a growing zone a limit read |
| 2089 | Story chains and missions; the peoples' memory fades (first hour of the day only) | per running chain, per mission; per remembered deed |
| 2185 | The weather record (Weather/WeatherBook.cs, a map component): the hour's rain, snow, storm, fog and temperature noted; a finished day, and a finished season set against the year before and named; then the weather dynamics' hour (Weather/WeatherDynamics.cs): the hour's temperature kept for tomorrow's front, events' pulls that ended dropped, and a forecast when the weather is a few hours from changing | per home map: a few numbers; a forecast rolls the game's next weather once per weather |
| 2280 | Council: encounters and nuisances, Rimfolk's own events (a timer per home) | one roll each, Rimfolk's events one per home whose timer is due; event memory pruned in the first hour of the day |
| 2375 | Conversations: the hour's look for what people go through together (with social life on); views drift in the first hour of the day | per home map: colonists up between one and four at night (pairs together), active conditions, lords, fires, fresh food poisoning, newcomers, prisoners, traders; new play-log lines (tames); finished studies; daily per colonist and view, mentors and secrets |
| 2471 | Ailments, the colony's part (on the slice that lands here): the catching ailments' clock; pruning on the first look of the day | per home map |

**Every 250 ticks**, by tick of the cycle:

| Tick | Pass | Work |
|---|---|---|
| 37 | World situations keep their people from the world's clean-up (only while one runs) | per person a situation holds |
| 41 | Room planner queue (only while blueprints are queued) | the next blueprints |
| 53 | Medical triage with Move the Patient (Integrations/MedicalTriage.cs; only with the mod): once an hour, at tick 53 of the hour, every home map's medical beds; every 250 ticks only a map where a patient lately went to a lesser bed, and a move under way followed (the lighter case carried out, then the worse one brought in) | per home map: its medical beds and the patients in them or downed on the floor, the mod's severity each; a carrier sought only for a move |
| 67 | Hurts from everyday life (Harm/HarmBook; nothing while the setting is at nothing): one tenth of the colonists (by ID), each once an hour; the first slice also calls the evening drill (with a drills or militia decree, five to eight, once a day) and prunes the cap's record in the first hour of the day | per colonist in the slice: the job's work under a work decree (a long day's one roll), a strain or a sprain worked through (heavy hours, one roll each); per colony, its drilling decree and home map's hour |
| 71 | The heart: one tenth of the colonists (by ID), so each colonist once an hour | per colonist in the slice: moods, feelings, needs, strain; each map's context (food, medicine, filth, defences, dread) at most once an hour, its threat flag every 250 ticks; the dead pruned on slice 0 |
| 77 | A camp's phase, hunts and trees (only while a camp is there) | per camp member, prey and tree |
| 89 | Missions: an accepted quest starts, a goal met is noticed (with a map) | per open mission |
| 97 | The weather's push on the outdoor temperature, eased a tenth of the way toward what the weather makes it (Weather/WeatherDynamics.cs; with "The weather moves the temperature" on, or while a push is still easing off) | per map: the sun, the snow cover's total, the weather |
| 113 | The welcome letter (only until it's sent); answers playing out on the map (only while one waits or runs) | one check; per play-out |
| 131 | Visitors on the map: each visit waiting to come or under way (only while there are any) | per visit: its hour, danger, the weather, its letter, its people |
| 149 | Things an answer keeps on the map for a while: lanterns for a night, a fire in a cold room, the pyre, a bolted door, an animal at someone's heels (only while there are any) | per thing kept: its hour, danger, its fuel, its people |
| 139 | Health stories: one tenth of the colonists (by ID), each once an hour (with convalescence or reputation on) | per colonist in the slice: limbs lost, bed rest, setbacks |
| 167 | The colony news: what waits goes out (as the evening's digest, around six) | the waiting news |
| 173 | The food policy on the road (Journeys/RoadFood.cs; once an hour, at tick 173 of the hour, and on the first pass after a load; only while one is chosen or someone is on it): the colony's people in caravans onto it, those home again back to their own | per player caravan, per colonist in it; per traveller on it |
| 173 | A journey planned ahead (Journeys/JourneyRun.cs; only while there is one): the stage its hour has reached (a day before, the travellers' timetables shifted; the last hours begun), each traveller's next step, the hour itself and the question when someone isn't ready | per traveller: free for a step, the step's job; at the hour, each one ready or not |
| 199 | The council window's memory: the day's trust sample, the dilemmas and answers it watches | per colonist once a day |
| 187 | Who stands beside whom in a fight at home (only while one is on; Social/FightSides.cs), so only they remember it together and become comrades; who carries, tends or stands over someone down; and the colony's animals in it (Animals/AnimalTempers.cs, AnimalVoices.cs): a brave one goes to stand by its person, a timid one bolts from the shooting, a speaking one warns of the raiders | per fighting colonist pair on that map (close, then the same room or in sight); per named or bonded animal there that is brave, timid or speaks |
| 193 | The wild's alphas and bosses (Beasts/BeastBook.cs; each part by its rate): an alpha fallen (its pack scattered) or tamed (its pack walking in), a big threat looked at for a manhunters' alpha, a beast nemesis badly hurt turning to run, one fled off the map kept, a sworn one's fury, each home map's herds crossing for an edge spooked into a stampede, its runners' way, those who stop to watch; its hour on the same tick of the hour (alphas' threats and sightings, beast nemeses' returns and oaths), its day in the first hour of the day (packs led by an alpha, the season's great herd) | per open alpha and its pack, per beast nemesis on a home map, per home map's exit groups of four or more wild animals; a stampede's runners against the map's pawns for an hour |
| 221 | Ailments: one tenth of the colonists (by ID), each once an hour; the slice at tick 2471 also carries the colony's part | the colony's serious illnesses counted (every colonist's hediffs, as before every hour's rolls), then per colonist in the slice: circumstances sampled, each ailment's counted causes rolled; diet and sweets on the first look of the day |
| 227 | The colony's animals with powers (Animals/AnimalPowers.cs; nothing without PokéWorld, A RimWorld of Magic, Ideology or the omens' setting): the colony's typed Pokémon at home (warmth, crops, a battery, a calming, a fire), each familiar near its mage, a binding waiting; on the hour's first pass the typed Pokémon counted, newborns under a sign, omens' fates, sacred animals fallen and hunts marked; in the first hour of the day a mage's own choice | per typed Pokémon of the colony (a room, 25 cells, 9 cells, the colonists within 6, the fires); per familiar and binding; hourly per colony animal (a birth check) and per fresh corpse |
| 211 | A neighbour's letter being written, a call under way or the comms console ringing (World/NeighbourLetters.cs; only while there is one) | per letter or call under way: the writer still at it, someone free to answer |
| 83 | Colonists starting a talk with an outsider near them (Outsiders/OutsiderTalk.cs; on each home map with outsiders on it, whichever map is in view, with the setting on); every fourth look, the right-click's things done by themselves (Outsiders/OutsiderAuto.cs, with its setting on) | per home map, per free outsider: a free colonist near, the pair's and the visit's waits, then a small chance by both natures; one talk at most a home. By themselves: the day's pace first, then per home with no danger, per free outsider (three weighed at most) the pace with them, the six nearest free colonists times the things allowed (cheap facts), the best six checked by the menu's own reasons and the stores; one at most a look |
| 241 | A meal or a drink shared with an outsider, a bout or a brawl with one (Outsiders/; only while there is one) | per meal: both still eating; per bout: still at it, its end by its terms; per brawl: still fighting |
| 233 | Defense lines watch a raid (only while hostile people or mechanoids are on a home map that has lines) | per raider: in front of or behind each line, how it got behind; colonists newly downed near a line; the raid's lessons once it's over |
| 7 | Carrying and hauling (Hauling/HaulBook.cs; only while a pawn carries something for Rimfolk): never more owed than it has, the run's build area looked at (what it still needs of each material, for the other builders' cover) and a run over sent to storage, a load the job choice can't send queued for storage | per pawn carrying something: a radial look round its run's frame per material carried (15 cells at most) |

**Other rhythms**

| How often | Pass | Work |
|---|---|---|
| every tick | Ailments and health: queued work (an ailment that ended, a tend) | only when something is queued |
| every tick | Carrying and hauling: the look again round a spot where a pawn's work just dropped something (Hauling/HaulBook.cs) | only while a spot waits: its few cells |
| every 30 ticks, tick 0 | Council: wishes after building (only after something was built) | per open wish |
| every 30 ticks, tick 11 | Visual touches pulse (at most every 0.4 s of real time; only with them on) | the map in view: flecks due, a warlord's band, fireflies, hail, camp smoke, a camp walking in, the first snow's flakes, a dream told at breakfast (the untold dreams only) |
| every 15 ticks, tick 7 | Conversations: the next line of a talk under way (only while one is) | the two speaking: still together? |
| every 60 ticks, tick 23 | Council meeting upkeep (only while one is called) | the attendees |
| every 60 ticks | Rimfolk scenario parts set their stakes (only until done) | one check |
| every 300 ticks | A council meeting in session talks (its lord job) | the attendees |
| every 250 ticks of a gathering | An answer playing out: a feast's plates, a toast's cups, its touches | those not yet served: the meals, found and sorted once for all of them, each colonist asked in that order only until one fits |
| every 1000 ticks, tick 433 | Other mods' people: new play-log lines and tales; who changed (Big & Small, an editor, a xenogerm) | the new entries only; a few fields per free colonist |
| every 30 ticks of a camp's toil | A camp's lord: hunters' jobs; every 2,500 a camp without a record leaves | per camp member |
| every 200 ticks per hediff | Ailment comps: incubation and treatment course (immunizable); severity per day (medicine, course, recovery, diet), read by the game | the colonist with the ailment |
| every 250 ticks per pawn | The heart's worn-out episode (a mental state): ends on danger, a wound, cold or heat | the colonist in it |
| every 250 ticks | Hail (a game condition, while it lasts) | the open fields' crops, colonists outside |
| every 600 ticks | Fireflies (a game condition, while it lasts) | colonists outside at night |
| once a day, tick 433 | Advisor: the wealth sample (per home map) | one number |
| once a day, tick 1789 | Other mods' people: best friends | per pair of colonists |
| once a day, tick 1489 | The world's history (World/Ledger/History.cs): everyday entries past sixty days folded into one per actor, kind and season, and the history kept within its bound | per entry |
| once a day, tick 1373 | The region's day (World/Region/RegionBook.cs): the towns in detail worked out again (each home's nearest 40 within the region map's reach, straight-line distances) with each home's remoteness; foundings whose site is gone let go; with "Towns' fortunes" on, each town in detail seeded or moved (the running world events' shares, a war within 20 tiles, the drift back) | per home, per town in the world (one distance); per town in detail (40 a home) its enemies' towns (one distance each); per running world event |
| once a day, tick 1117 (and the first tick after a load) | RimWorld of Magic's travel need, which the mod switches off, taken from anyone who has it (Integrations/MagicNeeds.cs; only with the mod) | every living pawn: one need looked up |
| every frame | Slow time: the speed keys and the tick budget; its two buttons (when shown) | a few checks |
| a building finished or gone (Harmony on `Frame.CompleteConstruction`, `Building.DeSpawn`) | The defense survey is marked to be read again (at most once a game hour, when next asked) | one check: the kind of building, then its distance to each line and the survey's gaps |

**Worked out when asked, not on a clock.** The advisor's advice (24 checks on a map) is refreshed at most once an hour,
when the alert or the council tab reads it; the inspector's strain line is built when the heart changes; Rimfolk's tabs
and pages rebuild their models every half second to second while open (UiCache, the Mood, Story and Culture tabs' and
the Social tab column's own caches).

**Called by the game often (no clock of their own).** These must stay cheap:
- *Situational moods* (asked for every colonist every few seconds): heart strain, the colony's ways, decrees, trust,
  quirks, vendettas, heirlooms, a goal within reach, the comet, everyday pressures, ailments and
  convalescence. Each answers from data worked out on the hour (a dictionary or a short loop, no LINQ), and the
  convalescent moods look at the hediff before anything else. The colony's ways keep each colonist's approval until
  the ways move (or 250 ticks, `Lean/Fresh.cs`).
- *The social tie mood* (asked for every pair of colonists): one dictionary lookup in an index rebuilt only when ties
  change.
- *Mood totals*: Rimfolk's memories scale themselves (a small moment carries its own size), and the postfix on
  `ThoughtHandler.MoodOffsetOfGroup` applies good things growing ordinary, a share per kind worked out at most every
  250 ticks per colonist (`GoodLife.Refresh`). A
  decree's mood finds its decree in a map built once.
- *Global work speed* (StatPart_HeartStrain): one dictionary lookup.
- *Conversation weights*: each Rimfolk interaction is weighed every time two colonists talk; def-name endings, a
  moment's tie kind and need are read once. Gossip picks its story once per pair every 250 ticks (an opinion and a
  sight check once per subject, only for a story that could win). Conversations work out all their topics' shares
  once per pair and tick (the topic's scene once an hour per map, the news' heat every 250 ticks).
- *The reputation opinion* (asked for every pair of colonists): one dictionary lookup in what each makes of the other,
  worked out again only when a deed or a knower changes (at most every 30 ticks) and once an hour for fading.
- *Per frame*: the inspector line (a string join), the Needs tab's extra tooltip lines and its summary line (each
  cached half a second), the
- *Per frame*: the colonist bar's badges (a dictionary lookup per portrait, the facts worked out once a second), the
  inspector line (a string join), the Needs tab's extra tooltip lines (cached half a second), the
  Social tab's width (a cached model and a field write), its rows' extra tooltip line (cached a second per pair), alert
  labels (rebuilt only when they change), the slow-time buttons (labels translated once, tooltips only on hover), a
  lucky thing's inspect line and description (a lookup by thing id, nothing while no thing is lucky). Windows and tabs
  keep a model and draw from it: the floor picker keeps each floor's research, cost, material, stats and tooltip for a
  second (at once again after a pick), the decree page lays out its cards with what stands in a decree's way once a
  second, the Mood tab builds its groups, rows, headings and hint buttons with its half-second model and draws only
  the rows in sight, the Almanac's cards their
  "Column: value" lines with their layout.
- *Picking a line*: every line Rimfolk says goes through Flavor, which takes each tagged line apart once
  (`Lean/LineTags.cs`, with what a shipped line needs besides its tags, `Lean/ShippedLines.cs`, looked up the first time)
  and only compares after that; the moment's tags (trait:, skill:, weather:) are built once per def.
- *Defs by name*: looked up once and kept (`Lean/DefRef.cs`), misses too; caches keyed by defs start over each game.
- *Harmony patches on the game's own events* (a memory gained, a meal eaten, a death, a letter, a raid, a goodwill
  change, a trade, a quest's end): run when the event happens. Only the wind guard patches a tick method
  (`WindManager.WindManagerTick`, a few checks).

Ailments and health stories, once on one tick of the hour each, are spread like the heart. Ailments count the colony's
serious illnesses before every slice (as they did before every hour's rolls) and give at most one new one in the
colony's room for it, so a colonist's rolls see the colony as it is; the catching clock still runs once an hour on its
old tick.

**Where to look.** `Tools/perf_audit.py` reads the code and ranks every entry point by estimated cost (how often it
runs times what it does, loops weighed by what they go over): `docs/perf-audit.md`, with the ranking before this pass
beside it. Estimates, not measurements; a profiler pass in a long save is still to come.

**Rimfolk's clock (measured).** Every pass with a slot in `Sched` (hourly, every 250 ticks, waiting, daily, the
people log) is timed where it runs: `Lean/PassClock.cs` reads `Stopwatch.GetTimestamp` before and after it (a struct
scope, nothing allocated) and keeps per pass its runs and time in this in-game hour and day and the ones before
(rolled on by game ticks), and the worst run since loading; nothing is saved. The table of passes is
`Lean/PassClockMath.cs` (Tools/Tests: every slot has a pass, every pass is timed); systems sharing a slot (four at 149)
have a pass each. Timing changes nothing a pass does: same tick, same order, no random numbers, exceptions as before.
The dev window "Rimfolk's clock" (Debug actions, Rimfolk) lists the passes by cost, with runs and milliseconds in the
last whole hour and day (the one so far until the clock has seen a whole one), the average and worst run, the totals
and Rimfolk's average share of a tick (60 ticks are a second at normal speed), then the sizes of the main saved stores
and the world's objects and pawns; hover a line for when it runs. Its model is built at most every half second.

There is no dispatcher: each system still checks its own tick. One component running the passes would move them to
its own place in the game's loop over components, past work that can share their tick in components whose place
differs from save to save (a talk's next line every 15 ticks, the visual touches every 30, the council meeting every
60, the ailments' and health stories' queued work on any tick), so it would not be provably the same; and the game
calls every component's tick anyway, so it would save one comparison per component per tick.

## Save size

Everything Rimfolk saves has a fixed size, a cap, or is pruned of the dead, the departed and the old. The caps are in
`Source/ColonyCouncil/Lean/Caps.cs` and the trackers themselves. "Known" means a pawn the game still has (alive or dead,
on a map, in the world or in a caravan), so such a record is let go when the game itself lets go of the pawn.

| Saved | Cap or pruning |
|---|---|
| Chronicle | none of its own: the colony's view of the world history below (`Chronicle.cs`); an older save's 300-line list is read on load and taken into the history |
| Journals | 60 lines each; journals of those no longer with the colony: the 30 written in most lately; grief already written about: cleared at 500; everyday moments: forgotten after 24 days |
| Annals | the year in progress (from one landing day to the next, reset by the year's letter); 40 written years |
| Day samples (history) | 120 days |
| Wishes | open ones only; recent wishes 20 days or 60; cooldowns dropped once over |
| Trust | one record per colonist, the gone dropped daily; 8 log lines; grants of the last 30 days |
| Social web | ties of the dead dropped daily, of those gone faded out; 40 lost bonds; pair tones (moment memory) forgotten after 4 quiet days or when one of the two is gone; one short line per pair of colonists who met (Social/Bonds.cs: how well they know each other, fights side by side, seats lost), dropped once the two have not both been among the colony's people for 60 days |
| Groups | groups of 3 to 9, the dead and departed dropped daily (and anyone the game has let go of, at saving, so no reference is saved to nobody), a group ended when fewer than two are left; 14 history lines, 10 former names and the rivalries with groups still there each; newcomers 40 days at most; 12 past groups (those the game still knows); work samples of colonists only, 6 kinds each |
| Dreams | 80 recent dreams (a discarded dreamer kept by name only); omens until they come; dream counts of known pawns. The session-only maps (last dream, untold dream) drop the dead and whatever is too old to matter |
| Life goals | goals of the dead and departed end daily; finished ones of the dead kept 3 years, of discarded pawns dropped; counters of known pawns; sparks 30 days; a goal chosen by a change of heart is marked (one value) |
| Secrets | one per colonist, ended with their death; who was rolled for one: known pawns only |
| Vendettas | 40 settled; avengers dead or discarded dropped; eulogies until given |
| Quirks | 2 per colonist, the dead and discarded dropped; fights and falls only within the longest quirk window. Sights (the pyre), the lamp's visits and a juggler's last turn are held for the session only, a few days at most |
| Legacy | heirlooms until the heir dies or is discarded, or the thing is lost; children waiting to come of age: known pawns only; came of age: 6 days |
| Nemeses | 30 that have ended |
| Pets | animals known: the dead get their farewell, the discarded are dropped |
| The animals' natures | by id, never by reference: temperaments 300 (unseen 120 days let go), pairs, friends and rivals 80 (pairs first; apart 40 days, or a friend missed 30 days, let go), animals gone alive 30 with their handler (120 days, or the handler dead) |
| Autonomy | who acted when: the last 2 days, not the dead; plans until built |
| Rimfolk's events | raids until they come; the departed 2 years (unless asked about); memos 3 years (children's 10), used ones a day; quests already told of: those the game still keeps; the event timer, one per home (a home lost takes its own) |
| Homes | one entry per colonist who has been on a home (the last home they lived on), forgotten once they're dead, discarded or no longer the colony's; the stored seat; the homes at the last look |
| World ledger | its own ties (grudges, favours, good words, claims, the scenario's debt, fealty, a notable's and a regular's regard): three of a kind between the same two, sixty of a kind (three hundred regards), closed ones dropped after sixty days; a grudge's dead ten at most over all peoples, two years; persons while a notable, named by a tie or kept in the world, with their reasons; towns while a notable lives there or while they stand within the region map's reach of a home (the nearest 40 a home), a gone one two years; the founding families of new towns, two per town, six members each by first name, going with the town's record; the towns being built within reach, six at most, dropped when built or gone; the counter for ledger numbers |
| The region | one part per colony (when a new town last wrote to it and last welcomed it), a lost home's kept 30 days; the towns in detail and remoteness are worked out, not saved |
| World history | 800 entries (every chronicle line, every answer with its six values, the peoples' deeds, the ledger's ties); everyday ones past sixty days folded into one per actor, kind and season; milestones (the chronicle's) kept longest; over the bound older seasons' summaries go first, the newest 200 always stay |
| Everyday pressures | per colonist, the gone dropped daily; 12 meals; frames and plans while they stand |
| Character | 5 ways; 30 recent shifts; the colony's traits one per trait, today's tallies emptied each day; the colony's one name as last looked at, one line |
| Decrees, dilemmas, offices, names, encounters, ambitions | one entry per def, office or title, and how each name held holds (one line per name) (decrees, dilemmas, offices and ambitions for each colony's council; a lost home's kept 30 days); a curfew's night hours per colonist while it lasts, then only for those away (the dead, those no longer the colony's and those the game no longer knows let go); the colony's founders: those here in its first two days |
| Heart | one per colonist, the gone dropped hourly |
| Character marks | a person's record (sides moved, marks, views): the colony's own people the game still knows, empty records dropped at saving; 12 marks per person (small ones go first, each let go after its keeping, turning points three times as long); what the marks watch: per person, the last 8 fights won (a year), their addictions and their last breakdown (5 days), known pawns only; skills mastered: known pawns, 800 at most |
| Traits that change | changes made: known pawns only, 8 per person, 200 in all; what each colonist is watched for: deeds counted 240 days (12 per kind), 10 snapshots of a few records, when they were last unsettled; the once-in-a-colony changes (2 keys) |
| Ailments | by pawn: last ailments 120 days, known pawns only; by hediff: only hediffs colonists still have; illnesses and injuries an ailment may follow: until over or healed, known pawns only |
| Hurts from everyday life | each colonist's last Rimfolk-made hurt, pruned past three days (daily and at saving); each home's last evening drill, a map gone dropped; what the day's work counted and the evening's bouts not saved |
| Health stories | healers, beds, convalescence, hard times not over yet: known pawns only; losses 30 days; tends 3 days; setbacks and trust lines 20 days; visits 30 days; cases while the patient lives |
| Calendar | at most 12 days written by the colony and 8 named by the player (traditions and the founding day stay); days about people who are gone let go; days taken off by hand grow only with the player's clicks; who is away and each colonist's hunt: the colony's own only; crops grown: one per crop |
| Personal stories | stories under way; 200 past; eulogies until given |
| Camps | one camp; its lists (and the asks made of it) go with it, pruned of the gone at saving |
| Journeys | one plan at a time: its travellers, animals, packing list, the last hours' steps done and the timetable hours it changed, all gone when they set off or it's called off |
| World situations | 2 running; 40 past, each with what followed from it (once per def) |
| Story chains, missions | chains 120 days after they end; missions 2 days |
| Answers playing out | waiting ones 10 at most, done ones dropped (their people, places, hunt and search routes with them); how the last 30 went, for the dilemmas page; the colony's pace, one count a home (gatherings of the last ten days, the season's custom, life goals' toasts waiting five days at most), homes no longer ours dropped |
| Defense lines | per home map, gone with it; only the lines the player makes: one mark per step done or set aside, 24 plans, the last 5 raids (4 downed and 3 lessons each); the raid under way only while it lasts |
| Scenario parts | fixed by the scenario (its days, its seats) |
| Faction memory | 12 deeds per people (the settled and the faintest make way); a deed is dropped once it fades below 1, and with its people when they're gone; who tended or freed whom: 60 notes, 30 days, dropped when the stranger dies |
| Faith (the people) | one record per colonist whose faith moved or is watched (when talk last swayed or steadied them, their last crisis, the demand of their faith weighing on them), known pawns only, empty ones dropped; 5 faith moments each, 120 days; crises waiting to be felt, one per kind per colonist, 6 days; which pairs talked about faith lately not saved |
| Views and shared memories | views with the person's record (`PawnPersona.stances`), a view about a newcomer let go once they've been here 60 days or are gone; shared memories 8 per pair, 3 of one kind, and 600 in all (the faintest go at once past 660, and at saving the faded ones and those of people the game no longer knows); a fight is remembered only by those seen side by side in it, 3 partners each; the talk's news 40 notes, a month at most; who talked to whom lately and talks under way not saved |
| Reputation | 10 deeds per colonist and 220 in all (the faintest make way), 30 knowers a deed; a deed faded to nearly nothing is dropped, and daily those of the dead and departed and anything the dead knew; one packed number per colonist for the names told |
| Faith and the council | each faith's last evening of faith (one per faith, let go after 60 days or with the faith); the evening waiting to be held (one); the faith a decree names; who heard which faith spoken for lately (one per colonist, 30 days, 40 at most) |
| Other mods' people | best-friend pairs of known pawns, 200 at most |
| Advisor (per map) | 60 days of wealth samples |

How big it gets: `Tools/save_estimate.py` models every saved collection (293) with its rule and plays five in-game
years in a colony of 20 (`docs/save-size.md`). Nothing grows without end; Rimfolk's part of a five-year save comes to
about 2.2 MB: a fifth of it the journals, and about as much again people's records and their shared memories.
`--strict` fails on a saved collection with no model, so a new one needs its rule written down.

Pawn references: a saved reference to a pawn the game has discarded is written as nothing, so every such record was
already dropped on the next load. The trackers above now drop or forget them while playing too, so a long session
doesn't hold on to pawns the game has let go of. No storage format changed.

---

## Roadmap

**Done in 0.1**
- Advisor with 14 checks and rotating tips, plus an alert for urgent advice.
- 23 wish types, six tones each, petitions, Kind colonists asking for others, ascetics
  skipping luxuries, Gentle / Demanding / Advisor-only modes.
- Council meetings with three pitched ambitions, heckling, rewards for success.
- Room templates with ghost placement, rotation, material choice and "find a good spot".
- Personal touches in colonists' own bedrooms.
- Royalty (noble room requirements), Biotech (children) and Odyssey (space maps) handled.

**Done in 0.2**
- Room planner panel (works from the council tab and the "rooms" button in the bottom bar): rotate, mirror,
  move the door, per-element materials, furniture variants, cost against stock, site clearing (cut, mine,
  replace own buildings, smooth stone) with queued blueprints. 32 templates with good-practice tips.
- Trust, promises, refusals, reminders, self-help, jealousy, event-triggered wishes, data-driven effects,
  tagged context-aware lines and scenes.
- Physical council meetings: a gathering lord job at a fitting venue, pitches and banter above heads, votes,
  and a decision letter at the end.
- Ten more advisor checks from good-practice research; 31 ambitions.
- The Almanac tab (framework; content being written from wiki-checked research).

**Done in 0.3**
- Counterweights to permanent good mood: rising standards, decrees that trade mood for output, dilemmas with
  real costs, bitter colonists who may walk out.
- Decree petitions, names (epithets), anniversaries, yearly annals, personal journals.

**Done in 0.4 (Rimfolk)**
- Traditions: seasonal choices repeated become expected.
- Council offices with yearly elections.
- The social web: friends, rivals, comrades, mentors and circles that form and fade from opinion, battles and
  council decisions, daily social events, social dilemmas, and bonds feeding back into votes, objections,
  petitions and trust.
- Dreams drawn from each colonist's recent memories, injuries, bonds and losses; most are flavour (mood,
  journal, a line told over breakfast), a few change something, and rare omens schedule the incident they
  foretell.

**Feelings and strain**
- Strain under mood (bands, a mood of its own, a work speed part, darker dreams, a day off at the end of the rope),
  twenty data-driven feelings that nudge wishes, moments, dreams and strain, four needs of the heart, the days of rest
  decree, and the Heart section of the Mood tab, the inspector line and RimHUD lines.

**Ideas for later**
- Ambition chains (multi-step goals across seasons).
- More Ideology hooks (precept-driven wishes, ritual spaces).
- Translations.

---

## References and prior art

No existing mod was found that does pawn-driven building requests or a colony advisor
in this form. Closest:

- ["Dream and Desire" idea thread](https://steamcommunity.com/workshop/filedetails/discussion/2697015438/3199243752762917307):
  pawns wanting specific apparel and equipment based on traits and backstory (concept only, as far as we could tell).
- [RimWorld Wiki: Expectations](https://rimworldwiki.com/wiki/Expectations) and [Thoughts list](https://rimworldwiki.com/wiki/Thoughts_list): the vanilla systems the wishes build on.
- [RimWorld Wiki: Colony Building Guide](https://rimworldwiki.com/wiki/Colony_Building_Guide): its "current plan and next plan" advice is what the Advisor tries to automate.
- Design inspirations: The Sims 2 wants and fears, Dwarf Fortress noble mandates, Frostpunk promises.
