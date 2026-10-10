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
from tagged scene pools.

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
- **Performance.** Run checks every in-game hour (or less often), never every tick. Cache results.
- **Exploits.** Rewards must not be farmable: cooldowns, no repeat wishes, rewards scale with effort.
- **Nagging.** The biggest design risk. Rate limits and "Not now" are essential, and
  the default is Gentle.
- **Overlap with vanilla alerts.** RimWorld already warns about some basics (e.g. no
  beds, low food). The Advisor should go beyond those (forecasts, trade-offs, *why*)
  rather than repeat them.

---

## Technical overview

Standard RimWorld mod: XML defs plus a C# assembly. No Harmony patches and no other mods
are needed; everything hooks in through vanilla extension points (game and map components,
a main tab, alerts, interaction workers, a designator, a choice letter).

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
Textures/ColonyCouncil/
Source/ColonyCouncil/  # C#; builds against the Krafs.Rimworld.Ref NuGet package
Tools/DefValidator/    # offline XML checker against the real game types
Tools/Tests/           # tests for the room rotation math
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
