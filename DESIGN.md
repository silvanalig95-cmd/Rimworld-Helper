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
another line fits, so a line doesn't come round again soon. `Tools/variety_report.py` ranks pools by how often their
lines come round.

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
- **Nobody gets hurt for flavour.** Contests are matches at targets, wrestling without weapons, crafts and
  debates. Hail damages crops, never people.

### Scenarios with stakes
Rimfolk's scenarios use the same machinery to start a game with stakes in play: a nemesis from day one, a debt
that comes due as a Rimfolk event, a "hunt" (a promised raid that comes back every few weeks while the one leading it
lives), deserters whose old unit finds them, two families seeded with kin and feud opinions, a starting reputation,
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
  Sources build it with their own daily caps and a cap of about 25 a day for everything; new memories count once, when
  they're new (a renewed memory, like a second cold night, counts again). Drains are slow and steady (sleep, quiet hours,
  recreation) with chunks for gatherings, good talks and catharsis. Small decaying totals per source and per kind of
  relief say what built it and what eased it lately. The pure math (bands, caps, trait factors, episode odds) is in
  `HeartMath`, covered by the tests.
- **Bands** give the only direct effects: a mood of -3/-5/-8, work speed -5%/-10%, darker dreams.
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
- **Nature picks, then scales.** The first mark of a family whose condition fits is given; the same raid hardens one
  colonist and shakes another. Nothing is random but a few small kindnesses (2 in 5 leave a mark).
- **Rare enough to read.** `MarksMath.Blocked`: one small mark per three days, a turning point never twice within its
  keeping, one happening one mark (a story and who it was about), two turning points a day at most. A person keeps 12
  marks; small ones go first.
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
- **Identity.** The kind is what binds most of it (`GroupKindDef`: 25, lowest order wins, friendship last); it gives a
  name picked once, in the colony's era, and kept. Roles come from character and behaviour (`GroupMath.AssignRoles`:
  one anchor, joker and peacemaker each; a change waits three days; the incumbent keeps a unique role against a near
  rival). Ways (`GroupNormDef`, at most three) come from the members' average character or tags, with hysteresis; views
  from members' stances (`Persona.Stance`, any topic key). Members drift towards both (`Persona.Nudge`, `SetStance`).
- **Life.** Cohesion (0-100) settles towards the members' mean affinity and moves with habits kept, shared memories,
  quarrels and mends (read from `MomentTones`), losses and separation. Habits (`GroupHabitDef`) are kept when members are
  really together doing them (hourly, `Together.Can`), or called on the map as a play-out by the anchor. Newcomers
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

- **Deeds** (`DeedDef`, 31): facets per side of a name (courage, kindness, diligence, skill, discretion, temper), a
  theme for lines, a fade half-life, merging of repeats. Recorded where the event is already noticed (one-line calls
  in WorldReactions.Victory, Moments' contests, Secrets.Reveal, Offices, Patch_ArcMade, Ambitions, Encounters,
  MeetingAttendance), by four light postfixes (a memory gained, a mental break, a history event, a social fight; one
  dictionary look each) and by the reputation's own hour (rescues under fire, the brave's company, idling, tending).
  Each keeps doer, victim, when, a name ("the raid on the 5th of Aprimay"), size, and its knowers: witnesses, and those who
  heard, from whom, in what version, believed how far.
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

- **Topics** (`TopicDef`, 19, a worker each) say when they come up, what's news (`TalkNote`s from the game's events and
  the talk's hour, and the game's state: the food, a disaster, traders), who or what a talk is about, and what a
  colonist's life adds to a view. A view (-1..1) is kept with `Persona.SetStance` under the topic's defName
  (`defName:id` for a newcomer, let go once they're settled). It's seeded from the topic's weights per side of a person,
  its leans (traits, passions, memes, precepts) and that life, drifts 4% a day back toward it, is pushed by events (a
  fight, a death) and moved by talk (`TalkMath.Pull`, at most a quarter of the way in one talk).
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
- **The character page**: views (section 30, with what each rests on and the last talk that moved it) and shared
  memories (40).

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
  cut), only for real work in the home area or near the answer's spot, nearest first; each job is followed by a small
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
- **The moods.** While an answer's effects run, a good memory (`DilemmaEffect_Thought`) given to someone taking part
  in what the answer set going is held with the plan (`PlayOuts.Hold`) and given when it's over to those who were
  there long enough (a quarter of a gathering, at most half an hour; half a visit) or ate at the feast. Bad memories,
  trust, standards and the rest come at once, as before. Calendar feasts, games and remembrances hold their own moods
  the same way.
- **Safety.** Nobody is called from danger, sleep, a sickbed or the need of one, bleeding, starving or exhaustion,
  arms, a mental state, a caravan, the care of someone else (tending, rescuing, feeding, carrying) or an order of the
  player's; guests, prisoners and lodgers are never invited. Nothing starts while enemies are about, while another
  gathering is on, while half the colony is drafted or while someone is bleeding, and any danger calls a gathering off
  (as the council meeting is). A visit ends at any danger. A plan waits for its hours and up to its wait (six hours by
  default, never more than thirty in all), then falls back to what answers always did: its moods for everyone it was
  meant for, and for a gathering a line saying why. Every step is wrapped so a failure is logged once and the moods
  still come. With the setting off, the effects do nothing and the moods come at once.
- **Where it plugs in.** `DilemmaTracker.Resolve` and `HappeningTracker.Resolve` wrap an answer's effects in
  `PlayOuts.Collect`, and so do a world event's beats, answers and endings (`WorldSituations`), an event's letter
  without answers (`IncidentWorker_Happening.Send`) and a story's ending (`Workers_StoryFlow`); the helpers an answer
  picks (`DilemmaEffect_Needs`) come first in `PlayOuts.Ordered`, so a play-out can name them. The tracker keeps the
  last 30 results (who took part, a note), which the dilemmas page shows as how it went (`DilemmaLog`), and This week
  lists every plan waiting or under way (`WeekModel`, `PlayOutText`); `DecreeDef.onEnact` (a feast day at noon, a festival's first evening, a rest day) and
  `AmbitionDef.achievedEffects` (the great feast eaten, the first barrel, a games night) run through `PlayOuts.Run`;
  the calendar's feast, games and remembrance days start theirs directly. `PlayOutTracker` (a game component of its
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
  day's work hours are free time).
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
  on the map to take them are still taken from the stores at once; where a visitor or a rider waits, they are carried
  out.
- **Other systems.** World events play out now (their answers' gifts and payments are carried to the gate, the
  locusts' harvest and watch are real). Personal stories, social events, dreams and encounters are moments that have
  already happened (interactions and moods), not answers to carry out.

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
| Quirks | Who someone is | any tag check | quirk:Hums, quirk:SleepTalker... for roles, lines and dreams; 12 social events use them |
| Calendar, kept, setbacks, health | Dreams | the memory those leave | 5 dreams (XML) |
| News | News | the evening digest | three or more lines about one colonist come together as their day |

## Balance targets

What the player asked for, over and over: it shouldn't be easy to keep everyone happy, good moods shouldn't vanish,
not everything should be a mood, and neglect should have real consequences, never flat penalties. The targets below
put numbers on that. `Tools/Balance` measures them: a colony-year simulator that plays a settled colony through one
60-day year without the game, from the def XML, Rimfolk's rates (read from the source where it can) and the mod's own
mood arithmetic (`GoodTimesMath`, `PressureMath`, `SetbackMath`, `AilmentMath`, `ArcMath`, compiled in as they are).
What it can't know (the storyteller, how a player plays) is one documented table, `Tools/Balance/Rates.cs`. It writes
`docs/balance-report.md`; run it after any change to moods, rates or ailments.

**Rimfolk's mood** is everything Rimfolk adds to a colonist's mood, averaged over the year: its good memories (after
the shared budget and fading sooner), its bad memories, and its steady moods (trust, the colony's ways, pressures,
ailments, waiting, strain). The game's own moods aren't in it.

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

**The presets** each aim at their own balance: Light touch about +5, +4 and 0 (well run, average, neglected) with
fewer questions; Story-rich the default's moods with a question a little more often than every two days; Demanding
about 0, -4 and -18. Each preset's tooltip says so.

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
5. **Nobody gets hurt for flavor.** Moments between colonists never injure anyone: contests are friendly (arm
   wrestling, races, riddles), never fights with weapons.
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
  chronicle, the ambition tracker and personal touches.
- `AdvisorSignalDef` / `AdvisorSignalWorker` + `CouncilMapComponent`: advice per map,
  refreshed hourly, ranked by severity, urgency and confidence.
- `AmbitionDef` + goal classes + `AmbitionTracker`: council meetings and the active ambition.
- `RoomTemplateDef`, `TemplatePlanner`, `Designator_PlanRoom`: room plans with a ghost preview,
  rotation, material choice and a spot finder.
- `Avail`: the tech, research, material and purpose checks described above.
- `MainTabWindow_Council`: the Priorities, Wishes, Ambition, Room plans and Chronicle tabs.

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
`GrantWatch` missing or taken back and still within its amends window). A calm colonist shows nothing.
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

Both signatures are the same in 1.6 and 1.7 (ApiDiff). The settings (Interface page, "Colonist bar") switch each kind,
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
  idle (14%) and health (41%). Places are worked out for the screen at the time a panel's state is made. Old places move
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
  `GetCountIn`), for the map on screen or the busiest home, with the game's icons (`Widgets.ThingIcon`, which carries the
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
of this 250-tick stretch), the busiest home map (the same), and for the hour the colonists at home, the days of food,
the medicine and the colony's beds. The first pass to ask in its stretch works it out; the rest read it. The heart's map
context, decrees, the life checks behind dilemmas, the advisor, ambition goals, autonomy, encounters, social events,
nuisances, the calendar, Rimfolk's events, world situations, scenarios, story chains, nemeses, camps, play-outs, the
fights pass and the worn-out episode read from it. (A downed victim after a fight is still judged at that moment.)
`Lean/Fresh.cs` holds the rules for how long such an answer holds; Tools/Tests checks them.

**Every hour** (2,500 ticks), in the order they come in the hour:

| Tick | Pass | Work |
|---|---|---|
| 0-30 | Visual touches, the hour's look (the first pulse of the hour; only with visual touches on) | the map in view: maybe one comet gazer, one ambient line or a hum |
| 157 | Council: fights and losses, decrees, wish upkeep, promises kept (every other hour), event wishes, new wishes | per home map (a fight going on?), per open wish, per granted wish still watched (its own check, and a look at plans being built only when something is missing); a new wish rolls once, and only when it comes does it ask every wish of every colonist |
| 327 | A camp's hour (only while a camp is there) | the camp's members, trade, fees, its day's event |
| 502 | Council's day, first half (first hour of the day only): trust, standards, decree trust and petitions, anniversaries, names, journals, offices | per colonist (their memories, for the journal) |
| 677 | Health stories, the colony's part (with convalescence or reputation on): the cases of dangerous illness; pruning in the first hour of the day | per case (each colonist's part runs in its slice, below) |
| 852 | Council's day, second half (first hour of the day only): the social web, social events, life goals, secrets, vendettas, quirks, legacy, the colony's ways, marks on people's characters (illness, hunger, addiction, mastery, faith, peace), traits that change (the watches, then at most one change), the links between systems (the physician's rounds, the herdmaster, setbacks counted), the day's history sample, trust effects, bitter colonists | per pair of colonists (opinions, ties, circles), per colonist and quirk def |
| 917 | Links with other mods (only with one there): water and mending for strain and recovery | per colonist |
| 1027 | Council: ambitions, personal touches, dilemmas, annals, dreams, quirks | the ambition's goals on its map; per colonist (asleep or waking; a dream weighs every dream def for the one waking) |
| 1203 | World situations | per running situation (at most two); a new one rolls now and then |
| 1311 | Colony calendar | the home map: snow, the day's first look (harvest, deaths, rest day), talk, a day coming round |
| 1427 | Setbacks: the lasting ones (a wish still waiting, a decree dragging on, a sick friend, quiet days...), crops lost, rain on gatherings, noisy nights, long hauls, waiting for a doctor; outshone, cooled friendships and hard weeks in the first hour of the day | per colonist (their wish, decrees, those close to them who are ill or away), per home map (gatherings, sleepers and who is at loud work nearby, hauls, beds), per skill once a day |
| 1544 | Council: nemeses, pets, the links between systems (pets and grief, the marshal after a fight, a fear faced), moments waiting for two to be together, PokeWorld, autonomy, who is home, everyday pressures | per colony animal on home maps, per spawned colonist (a plan or a job now and then), per colonist (meals, work), per home map (crowded rooms, half-built frames) |
| 1660 | Reputation and gossip (only with "Personalities and social life"): in a fight, rescues under way and the brave's company; otherwise who idles while the rest work and who tends whom; in the first hour of the day, the day's idling and tending made deeds, the names that formed or faded told, the dead and departed let go | per spawned colonist on home maps (a job look; a sight check only for the idle, the brave and their side) |
| 1777 | Personal stories | per story under way; new stories in the first hour of the day |
| 1903 | Groups (with "Personalities and social life" and the social web on): work sampled, habits kept together | per colonist on a home map (their job); per group (its members up and about, together, doing its habit) |
| 2089 | Story chains and missions; the peoples' memory fades (first hour of the day only) | per running chain, per mission; per remembered deed |
| 2280 | Council: encounters and nuisances, Rimfolk's own events | one roll each; event memory pruned in the first hour of the day |
| 2375 | Conversations: the hour's look for what people go through together (with social life on); views drift in the first hour of the day | per home map: colonists up between one and four at night (pairs together), active conditions, lords, fires, fresh food poisoning, newcomers, prisoners, traders; new play-log lines (tames); finished studies; daily per colonist and view, mentors and secrets |
| 2471 | Ailments, the colony's part (on the slice that lands here): the catching ailments' clock; pruning on the first look of the day | per home map |

**Every 250 ticks**, by tick of the cycle:

| Tick | Pass | Work |
|---|---|---|
| 37 | World situations keep their people from the world's clean-up (only while one runs) | per person a situation holds |
| 41 | Room planner queue (only while blueprints are queued) | the next blueprints |
| 71 | The heart: one tenth of the colonists (by ID), so each colonist once an hour | per colonist in the slice: moods, feelings, needs, strain; each map's context (food, medicine, filth, defences, dread) at most once an hour, its threat flag every 250 ticks; the dead pruned on slice 0 |
| 77 | A camp's phase, hunts and trees (only while a camp is there) | per camp member, prey and tree |
| 89 | Missions: an accepted quest starts, a goal met is noticed (with a map) | per open mission |
| 113 | The welcome letter (only until it's sent); answers playing out on the map (only while one waits or runs) | one check; per play-out |
| 139 | Health stories: one tenth of the colonists (by ID), each once an hour (with convalescence or reputation on) | per colonist in the slice: limbs lost, bed rest, setbacks |
| 167 | The colony news: what waits goes out (as the evening's digest, around six) | the waiting news |
| 199 | The council window's memory: the day's trust sample, the dilemmas it watches | per colonist once a day |
| 187 | Conversations: who stands beside whom in a fight at home (only while one is on), so only they remember it together | per fighting colonist pair on that map (close, then the same room or in sight) |
| 221 | Ailments: one tenth of the colonists (by ID), each once an hour; the slice at tick 2471 also carries the colony's part | the colony's serious illnesses counted (every colonist's hediffs, as before every hour's rolls), then per colonist in the slice: circumstances sampled, each ailment's counted causes rolled; diet and sweets on the first look of the day |
| 233 | Defense lines watch a raid (only while hostile people or mechanoids are on a home map that has lines) | per raider: in front of or behind each line, how it got behind; colonists newly downed near a line; the raid's lessons once it's over |

**Other rhythms**

| How often | Pass | Work |
|---|---|---|
| every tick | Ailments and health: queued work (an ailment that ended, a tend) | only when something is queued |
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
- *Mood totals*: Rimfolk's memories scale themselves (SmallJoys ranks one colonist's good memories once per tick), and
  the postfix on `ThoughtHandler.MoodOffsetOfGroup` applies the standards factor, cached per colonist and tick. A
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
  (`Lean/LineTags.cs`) and only compares after that; the moment's tags (trait:, skill:, weather:) are built once per def.
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

## Save size

Everything Rimfolk saves has a fixed size, a cap, or is pruned of the dead, the departed and the old. The caps are in
`Source/ColonyCouncil/Lean/Caps.cs` and the trackers themselves. "Known" means a pawn the game still has (alive or dead,
on a map, in the world or in a caravan), so such a record is let go when the game itself lets go of the pawn.

| Saved | Cap or pruning |
|---|---|
| Chronicle | 300 entries; the newest 200 always stay, and of the older ones everyday lines go before milestones (losses, ambitions won or lost, life events, world events, secrets out) |
| Journals | 60 lines each; journals of those no longer with the colony: the 30 written in most lately; grief already written about: cleared at 500; everyday moments: forgotten after 24 days |
| Annals | the year in progress (reset each year); 40 written years |
| Day samples (history) | 120 days |
| Wishes | open ones only; recent wishes 20 days or 60; cooldowns dropped once over |
| Trust | one record per colonist, the gone dropped daily; 8 log lines; grants of the last 30 days |
| Social web | ties of the dead dropped daily, of those gone faded out; 40 lost bonds; pair tones (moment memory) forgotten after 4 quiet days or when one of the two is gone |
| Groups | groups of 3 to 9, the dead and departed dropped daily (and anyone the game has let go of, at saving, so no reference is saved to nobody), a group ended when fewer than two are left; 14 history lines, 10 former names and the rivalries with groups still there each; newcomers 40 days at most; 12 past groups (those the game still knows); work samples of colonists only, 6 kinds each |
| Dreams | 80 recent dreams (a discarded dreamer kept by name only); omens until they come; dream counts of known pawns. The session-only maps (last dream, untold dream) drop the dead and whatever is too old to matter |
| Life goals | goals of the dead and departed end daily; finished ones of the dead kept 3 years, of discarded pawns dropped; counters of known pawns; sparks 30 days |
| Secrets | one per colonist, ended with their death; who was rolled for one: known pawns only |
| Vendettas | 40 settled; avengers dead or discarded dropped; eulogies until given |
| Quirks | 2 per colonist, the dead and discarded dropped; fights and falls only within the longest quirk window |
| Legacy | heirlooms until the heir dies or is discarded, or the thing is lost; children waiting to come of age: known pawns only; came of age: 6 days |
| Nemeses | 30 that have ended |
| Pets | animals known: the dead get their farewell, the discarded are dropped |
| Autonomy | who acted when: the last 2 days, not the dead; plans until built |
| Rimfolk's events | raids until they come; the departed 2 years (unless asked about); grudges 2 years; memos 3 years (children's 10), used ones a day; quests already told of: those the game still keeps |
| Everyday pressures | per colonist, the gone dropped daily; 12 meals; frames and plans while they stand |
| Character | 5 ways; 30 recent shifts; the colony's traits one per trait, today's tallies emptied each day |
| Decrees, dilemmas, offices, names, encounters, ambitions | one entry per def, office or title; a curfew's night hours per colonist while it lasts, then only for those away (the dead, those no longer the colony's and those the game no longer knows let go); the colony's founders: those here in its first two days |
| Heart | one per colonist, the gone dropped hourly |
| Character marks | a person's record (sides moved, marks, views): the colony's own people the game still knows, empty records dropped at saving; 12 marks per person (small ones go first, each let go after its keeping, turning points three times as long); what the marks watch: per person, the last 8 fights won (a year), their addictions and their last breakdown (5 days), known pawns only; skills mastered: known pawns, 800 at most |
| Traits that change | changes made: known pawns only, 8 per person, 200 in all; what each colonist is watched for: deeds counted 240 days (12 per kind), 10 snapshots of a few records, when they were last unsettled; the once-in-a-colony changes (2 keys) |
| Ailments | by pawn: last ailments 120 days, known pawns only; by hediff: only hediffs colonists still have; illnesses and injuries an ailment may follow: until over or healed, known pawns only |
| Health stories | healers, beds, convalescence, hard times not over yet: known pawns only; losses 30 days; tends 3 days; setbacks and trust lines 20 days; visits 30 days; cases while the patient lives |
| Calendar | at most 12 days written by the colony and 8 named by the player (traditions and the founding day stay); days about people who are gone let go; days taken off by hand grow only with the player's clicks; who is away and each colonist's hunt: the colony's own only; crops grown: one per crop |
| Personal stories | stories under way; 200 past; eulogies until given |
| Camps | one camp; its lists (and the asks made of it) go with it, pruned of the gone at saving |
| World situations | 2 running; 40 past, each with what followed from it (once per def) |
| Story chains, missions | chains 120 days after they end; missions 2 days |
| Answers playing out | waiting ones 10 at most, done ones dropped (their people, places, hunt and search routes with them); how the last 30 went, for the dilemmas page |
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
