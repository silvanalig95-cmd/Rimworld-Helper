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
- **Circles** are connected groups of strong bonds, recomputed daily.

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
a day (where most daily work happens) is spread over the hour too. "Tick" below is the tick of the hour (0 to 2499), of
the 250-tick cycle or of the day.

**Every hour** (2,500 ticks), in the order they come in the hour:

| Tick | Pass | Work |
|---|---|---|
| 0-30 | Visual touches, the hour's look (the first pulse of the hour; only with visual touches on) | the map in view: maybe one comet gazer, one ambient line or a hum |
| 157 | Council: fights and losses, decrees, wish upkeep, rooms taken, event wishes, new wishes | per home map (a fight going on?), per open wish, per colonist given a room; a new wish rolls once, and only when it comes does it ask every wish of every colonist |
| 327 | A camp's hour (only while a camp is there) | the camp's members, trade, fees, its day's event |
| 502 | Council's day, first half (first hour of the day only): trust, standards, decree trust and petitions, rooms taken, anniversaries, names, journals, offices | per colonist (their memories, for the journal) |
| 677 | Health stories (with convalescence or reputation on) | per colonist: limbs lost, bed rest, setbacks; the cases of dangerous illness; pruning in the first hour of the day |
| 852 | Council's day, second half (first hour of the day only): the social web, social events, life goals, secrets, vendettas, quirks, legacy, the colony's ways, the day's history sample, trust effects, bitter colonists | per pair of colonists (opinions, ties, circles), per colonist and quirk def |
| 1027 | Council: ambitions, personal touches, dilemmas, annals, dreams, quirks | the ambition's goals on its map; per colonist (asleep or waking; a dream weighs every dream def for the one waking) |
| 1203 | World situations | per running situation (at most two); a new one rolls now and then |
| 1311 | Colony calendar | the home map: snow, the day's first look (harvest, deaths, rest day), talk, a day coming round |
| 1544 | Council: nemeses, pets, PokeWorld, autonomy, who is home, everyday pressures | per colony animal on home maps, per spawned colonist (a plan or a job now and then), per colonist (meals, work), per home map (crowded rooms, half-built frames) |
| 1777 | Personal stories | per story under way; new stories in the first hour of the day |
| 2089 | Story chains and missions; the peoples' memory fades (first hour of the day only) | per running chain, per mission; per remembered deed |
| 2280 | Council: encounters and nuisances, Rimfolk's own events | one roll each; event memory pruned in the first hour of the day |
| 2471 | Ailments | the colony's serious illnesses counted (every colonist's hediffs), then per colonist: circumstances sampled, each ailment's counted causes rolled; diet and sweets on the first look of the day |

**Every 250 ticks**, by tick of the cycle:

| Tick | Pass | Work |
|---|---|---|
| 37 | World situations keep their people from the world's clean-up (only while one runs) | per person a situation holds |
| 41 | Room planner queue (only while blueprints are queued) | the next blueprints |
| 71 | The heart: one tenth of the colonists (by ID), so each colonist once an hour | per colonist in the slice: moods, feelings, needs, strain; each map's context (food, medicine, filth, defences, dread) at most once an hour, its threat flag every 250 ticks; the dead pruned on slice 0 |
| 77 | A camp's phase, hunts and trees (only while a camp is there) | per camp member, prey and tree |
| 89 | Missions: an accepted quest starts, a goal met is noticed (with a map) | per open mission |
| 113 | The welcome letter (only until it's sent) | one check |

**Other rhythms**

| How often | Pass | Work |
|---|---|---|
| every tick | Ailments and health: queued work (an ailment that ended, a tend) | only when something is queued |
| every 30 ticks, tick 0 | Council: wishes after building (only after something was built) | per open wish |
| every 30 ticks, tick 11 | Visual touches pulse (at most every 0.4 s of real time; only with them on) | the map in view: flecks due, a warlord's band, fireflies, hail, camp smoke |
| every 60 ticks, tick 23 | Council meeting upkeep (only while one is called) | the attendees |
| every 60 ticks | Rimfolk scenario parts set their stakes (only until done) | one check |
| every 300 ticks | A council meeting in session talks (its lord job) | the attendees |
| every 1000 ticks, tick 433 | Other mods' people: new play-log lines and tales | the new entries only |
| every 30 ticks of a camp's toil | A camp's lord: hunters' jobs; every 2,500 a camp without a record leaves | per camp member |
| every 200 ticks per hediff | Ailment comps: incubation and treatment course (immunizable); severity per day (medicine, course, recovery, diet), read by the game | the colonist with the ailment |
| every 250 ticks per pawn | The heart's worn-out episode (a mental state): ends on danger, a wound, cold or heat | the colonist in it |
| every 250 ticks | Hail (a game condition, while it lasts) | the open fields' crops, colonists outside |
| every 600 ticks | Fireflies (a game condition, while it lasts) | colonists outside at night |
| once a day, tick 433 | Advisor: the wealth sample (per home map) | one number |
| once a day, tick 1789 | Other mods' people: best friends | per pair of colonists |
| every frame | Slow time: the speed keys and the tick budget; its two buttons (when shown) | a few checks |

**Worked out when asked, not on a clock.** The advisor's advice (24 checks on a map) is refreshed at most once an hour,
when the alert or the council tab reads it; the inspector's strain line is built when the heart changes; Rimfolk's tabs
and pages rebuild their models every half second to second while open (UiCache, the Mood and Story tabs' own caches).

**Called by the game often (no clock of their own).** These must stay cheap:
- *Situational moods* (asked for every colonist every few seconds): heart strain, the colony's ways, decrees, trust, a
  room taken, quirks, vendettas, heirlooms, a goal within reach, the comet, everyday pressures, ailments and
  convalescence. Each answers from data worked out on the hour (a dictionary or a short loop, no LINQ), and the
  convalescent moods look at the hediff before anything else.
- *The social tie mood* (asked for every pair of colonists): one dictionary lookup in an index rebuilt only when ties
  change.
- *Mood totals*: Rimfolk's memories scale themselves (SmallJoys ranks one colonist's good memories once per tick), and
  the postfix on `ThoughtHandler.MoodOffsetOfGroup` applies the standards factor, cached per colonist and tick. A
  decree's mood finds its decree in a map built once.
- *Global work speed* (StatPart_HeartStrain): one dictionary lookup.
- *Conversation weights*: each Rimfolk interaction is weighed every time two colonists talk; def-name endings, a
  moment's tie kind and need are read once.
- *Per frame*: the inspector line (a string join), the Needs tab's extra tooltip lines (cached half a second), alert
  labels (rebuilt only when they change), the slow-time buttons (labels translated once, tooltips only on hover).
- *Harmony patches on the game's own events* (a memory gained, a meal eaten, a death, a letter, a raid, a goodwill
  change, a trade, a quest's end): run when the event happens. None patches a tick method.

What was left alone: ailments and health stories still look at every colonist on one tick of the hour. Splitting them
across the hour like the heart would change when a colony-wide count is taken (ailments count the colony's serious
illnesses at the start of the pass, then give at most one), and that can't be checked without the game.

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
| Character | 5 ways; 30 recent shifts |
| Decrees, dilemmas, offices, names, encounters, ambitions | one entry per def, office or title |
| Heart | one per colonist, the gone dropped hourly |
| Ailments | by pawn: last ailments 120 days, known pawns only; by hediff: only hediffs colonists still have |
| Health stories | healers, beds, convalescence: known pawns only; losses 30 days; tends 3 days; setbacks and trust lines 20 days; visits 30 days; cases while the patient lives |
| Calendar | at most 12 days written by the colony and 8 named by the player (traditions and the founding day stay); days about people who are gone let go; days taken off by hand grow only with the player's clicks |
| Personal stories | stories under way; 200 past; eulogies until given |
| Camps | one camp; its lists pruned of the gone at saving |
| World situations | 2 running; 40 past |
| Story chains, missions | chains 120 days after they end; missions 2 days |
| Faction memory | 12 deeds per people (the settled and the faintest make way); a deed is dropped once it fades below 1, and with its people when they're gone |
| Other mods' people | best-friend pairs of known pawns, 200 at most |
| Advisor (per map) | 60 days of wealth samples |

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
