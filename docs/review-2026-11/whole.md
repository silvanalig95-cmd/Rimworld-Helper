# Rimfolk as one whole: a holistic review (6 November 2026, at 0.104.0)

A read-only review of everything built so far, looked at together. No mod code was changed. Evidence: the docs
(CLAUDE.md, README, DESIGN.md, ROADMAP.md, docs/HANDOFF.md, docs/design/interface.md), the code (1,086 files), the
defs, the settings catalog, `Tools/perf_audit.py` and `Tools/save_estimate.py`, and the player's save Atia (day 128,
12 colonists), read with scripts kept in the scratchpad (`holistic/`). The two other reviews of the same day (how often
things are staged and told; each system on its own) are tied in at the end of each part where they meet this one.

---

## Summary for the player

**What Rimfolk is now.** A mod that turns the colony into a society with a memory. It has seven parts a player meets:
the **council** (wishes, promises, decrees, offices and elections, meetings and ambitions, hard questions); the
**people's inner lives** (moods and what wears on them, the heart and its strain, character, quirks, dreams, life
goals, personal stories, health); **social life** (friends and rivals, groups, gossip, family); the **colony's story**
(the chronicle, journals, the calendar, and answers that play out on the map); the **world and region** (Rimfolk's
events, world events, towns that rise and fall, camps, visitors, couriers, letters); **diplomacy** (favours, pacts,
feasts, marriages, wards, fealty, renown); and **tools** (the advisor, planning ahead, rooms, the defense planner,
fields by the weather, journeys, on-screen panels, the Almanac). Under it all sits a sound foundation: one ledger for
the world's ties, one history, one facts service for text, one voice that caps messages, one clock for every pass.

**What works.** The foundation is strong and the newer windows are clear: four windows, thirteen views, names that
lead to cards, one inbox (This week), search, back and forward. The writing is often lovely ("From up there it
looked like a place someone had meant to make."). The tools stand well on their own, the defense planner most of all.
Rimfolk costs little: about 1 MB of a 31 MB save at day 128, bounded for five years, and no slowdown reported. Every
age and every DLC is thought of, and every setting has its line of what you'll notice.

**The five biggest problems in the whole.**
1. **Many systems do one job, each in its own way.** A colonist's feelings are shaped by about a dozen separate
   models (four that soften good moods, five kinds of small letdown that are each a mood, the heart's strain, two
   different readings of someone's nature). A colonist carries up to four names and the colony four more. A good
   share of the code exists to keep these from piling up (budgets, caps, "hard times", "systems talk to each
   other").
2. **Nothing decides what the player hears, only how much at most.** About 25 sources (events, world events,
   questions, camps, visitors, couriers, the region, 13 kinds of diplomatic offer, the calendar, elections, stories...)
   each run their own clock. The only shared limits are 12 hours between questions and a flood cap of 20 messages
   and 6 letters a day per kind. Every system also picks its own channels: one happening can become a bubble, a
   message, a news line, a chronicle line, journal lines and a mark. In the save the last month brought about six
   chronicle lines a day, elections on days 103, 124 and 128, and one colonist's life goal changed four times in 25
   days.
3. **The story is kept twice and half-remembered.** The chronicle and the history hold the same lines with different
   reach (at day 128 the chronicle shown in the council window has already dropped everything before day 41; the
   history in the region window still has day 1). The log of "how each answer went" covers only the council's
   dilemmas, one of the 21 kinds of question. The year is told four times (the annals, the year's letter, the
   founding-day event, the calendar's founding day).
4. **What the player sees is weighted the other way from what was built.** World, region and diplomacy are a third of
   the code (about 117,000 lines), yet at day 128 the save holds two world events, one raiders' camp, one favour owed,
   and nothing from diplomacy (built in the save's last days, so not yet a fair test), missions, callers or services;
   camps were tried 23 times since day 108 and found "no room" every time, so the player has never seen one. Meanwhile the inner-colony systems speak every hour. Three in
   ten answers that should play out on the map did not (nobody free, nowhere to hold it).
5. **Settings and pages are grouped by system, not by what a player thinks about.** 263 settings on seven pages.
   "How much happens" is about twenty sliders over six pages; "how hard" is spread over three; ten switches for
   other mods sit under "Interface". The presets were made before the region and diplomacy and don't reach their
   switches, so "Light touch" doesn't quiet the newest third of the mod. What waits on the player is worked out four
   times (This week, the council panel on screen, the colonist bar's badges, the game's alerts).

**The target shape, in a few lines.** One backbone per pillar, the rest feeding it, one voice to the player:
the **heart** carries slow wear and relief (moods stay for sharp moments), **character** is the one reading of who
someone is, the **history** is the one record (names, renown, the chronicle and the answers' log read from it), a
**teller** picks the channel by how much a happening matters, a **director** paces the big beats of each week and
leaves quiet days, the **play-outs** are the one scene engine on one festive calendar, the **ledger** keeps every tie
and regard, and the **tools stay standalone**. Settings ask five questions: how much happens, how hard, what's on,
how I'm told, my other mods.

**The phases** (each small enough to build, test and play; details at the end):
0. Fix what the save shows (character lost at every load, election results missing seats, camps that never find
   room, play-outs that don't happen, writing slips, name churn).
1. One record: the history is the timeline, the chronicle a view of it, every question in the answers' log, one year.
2. One teller and one pace: channels by weight; a weekly budget of big beats with quiet days; settings for pace in
   one place; presets reaching every part.
3. One heart: one rule for good moods growing ordinary; character as the one nature; small letdowns wear on the heart.
4. One name: one "known as" per colonist and one "known for" per colony, read from the history.
5. One festive year: customs and calendar days on one calendar with one rule for traditions; one feast engine.
6. One way in: one question letter, one visitors engine, the on-screen council panel reading the inbox, settings
   regrouped.

**Decisions only the player can make** are marked **[Player]** in the parts below and gathered at the end.

---

## 1. What Rimfolk is now: the pillars

Code share by pillar (lines of C#, `holistic/pillars.py`): world and region 86,000 (25%), tools 60,000 (17%), inner
lives 37,000 (11%), interface shell 34,500 (10%), diplomacy 30,600 (9%), council 28,500 (8%), the colony's story 24,600
(7%), social life 22,100 (6%), integrations 11,900 (3%), foundation 11,000 (3%). Defs: 9,917 in 891 files (1,979
ThoughtDefs, 695 of them good moods and 787 bad; 1,625 outsider talk pieces; 829 flavour pools and 713 extensions;
463 Almanac pages; 252 incidents; 214 dilemmas; 231 dreams; 225 encounters; 121 wishes; 68 world events).

| Pillar | What it is for in play | Systems in it (folders) |
|---|---|---|
| **The council** | The player's choices given faces: they ask, you decide, they remember | Wishes, petitions, promises kept and taken back, trust in the council (`Wishes/`, `CouncilDynamics.cs`, `Standing.cs`); decrees (`Decrees/`); offices and elections (`Offices/`); meetings and ambitions (`Ambitions/`); dilemmas, seasonal customs, answer shifts (`Dilemmas/`); banishment and moving on (`Social/Banish*`, `MovingOn*`); the colony's ways and colony traits (`Character/`); the links between systems (`Interplay/`); a council per colony (`ColonyState.cs`) |
| **Inner lives** | Why a colonist is how they are, and how a life changes over years | The game's mood with Rimfolk's moods (1,445 mood ThoughtDefs); standards, good times fading, good moods adding up slowly, used to the good life, rewards instead of moods, milestones (`Standards.cs`, `GoodTimes.cs`, `SmallJoys.cs`, `Moods/`); pressures (`Pressures/`); setbacks, quiet troubles, nuisances, frictions (`Setbacks/`, `Strangers/Nuisances*`); the heart: strain, 26 feelings, 4 needs, the day off (`Heart/`); character: 8 sides, marks, traits that change (`Persona/`); against their nature (`Diplomacy/Nature*`); quirks, practice, habits passed on (`Quirks/`); dreams; life goals (`Aspirations/`); personal stories (`Arcs/`); secrets and vendettas (`Secrets/`); faith in the colony (`Faith/`); ailments, serious illness, life risks, convalescence, healers' names (`Ailments/`, `Health/`); heirlooms, coming of age, everyday journal (`Lives/`) |
| **Social life** | The colony as a web of people, not a set of mood bars | Bonds (friends, rivals, comrades, mentors), chemistry, social events, moments, the pace of talk (`Social/`); conversations, views, shared memories, in-jokes (`Social/Talk/`); groups with roles, habits and newcomers (`Social/Groups/`); deeds, reputation, gossip (`Social/Gossip/`); epithets (`Epithets/`); family: names, suppers, inheritance, guardians, years asleep (`Social/Family*`); weddings, love and jealousy; fights' names and sides |
| **The colony's story** | Remembering, and showing what happened | Chronicle, journals, annals, the year's letter (`Chronicle.cs`, `Journal.cs`, `Annals.cs`, `World/YearLetter.cs`); the calendar and its traditions (`Calendar/`); answers that play out on the map, gatherings, work parties, vigils (`PlayOut/`); speech, habits over heads, weather talk, visitors who notice, glows and smoke (`Immersion/`, `Touches/`); the evening's colony news (`UI/News.cs`, `Interplay/NewsDays.cs`); notes in the game's letters (`Social/LetterNotes.cs`) |
| **World and region** | The colony isn't alone; the outside comes to the gate | Rimfolk's events, story chains, regulars, strays, visitor services (`Happenings/`, 252 incidents); world events (`World/WorldSituation*`, 68); what the peoples remember of the colony and of single colonists, notables, allies (`World/`); the region's towns, fortunes, rise and fall, new towns' faces, trading posts, raiders' camps, hamlets, small towns' maps, travellers to the remote, follow-ups, far news (`World/Region/`); camps (`Camps/`, 31 kinds); visitors and callers (`Visitors/`); couriers (`Couriers/`); missions and hosting (`Missions/`); outsiders, talk and right-click acts (`Outsiders/`, 1,625 pieces); strangers, encounters, nemeses, pets (`Strangers/`); the colony's animals and herds (`Animals/`); the weather as the colony knows it (`Weather/`); scenarios with stakes (`Scenarios/`) |
| **Diplomacy** | The colony's standing among the peoples as something it builds and can break | Favours owed, pacts and truces, ransom, envoys, feasts and contests with guests, marriage alliances, wards and hostages, fealty and vassals, calls to arms, colony renown, letters to neighbours (`Diplomacy/`, 95 files, the largest folder) |
| **Tools** | Help with what the player does by hand; never acts unless asked | The advisor's priorities and next steps (`Advisor/`); planning ahead, build ahead, making and chains, automations, base and storage review, room plans, templates, themes, rearrange a room, shelf reserves, build priorities (`Building/`); the defense planner, lines, approaches, cover drag, the gate reminder (`Defense/`); fields by the weather (`Fields/`); journeys planned ahead (`Journeys/`); on-screen panels and the hover card (`Hud/`); colonist bar badges; the Mood tab and Moods page; slow speeds; the Almanac (463 pages, 400 of them game knowledge) |
| **Integrations** | Other mods given faces, softly | 61 files: Hospitality, RimMarket, Alpha Skills, Genes, Animals, Memes, the Vanilla Expanded family, RimWorld of Magic, PokéWorld, RimHUD, Colony Manager, Faction Territories, Factional War, Outposts, the Empire, Diplomatic Marriage+, social interactions, hygiene, cuisine |
| **Foundation** (unseen) | Keeps the rest honest | Homes and a council per colony, Rimfolk's clock (52 passes), the world ledger and its ties, one history, facts and fill for text, one voice (`Tell`), flavour tags, ages, what the colony can do (`Avail`), speech and togetherness, causes shown under a mood (`Lean/`, `World/Ledger/`, `Text/`, `UI/Tell.cs`) |

---

## 2. The player's experience over time

**A note on time.** A RimWorld day is 60,000 ticks: at the game's second speed about 5.5 real minutes, at the third
under 3. With pauses, a real hour of play is about one in-game season (15 days), and a year (60 days) four to six
hours. So the colony news arrives every few real minutes, a Rimfolk event about every twenty minutes (at the
storyteller's pace), a wish every ten, a dilemma about once an hour, a council meeting about once an hour, a world event
every three to four hours, an election every four to six hours, and a tradition (two or three years running) after ten
to eighteen hours of play. Rimfolk's slow payoffs are far away in real time; its fast ones arrive many times a sitting.

| Scale (in-game) | What a player notices | What they decide | What they remember | Rich, flat, or piling up |
|---|---|---|---|---|
| An hour | Bubbles over heads (habits, weather, how someone really is, moments), mood bubbles with their names, the hover card, the on-screen panels; Rimfolk's exchanges are about a quarter of all social log lines (DESIGN.md, "One pace") | Little: a click on a name, a wish's Answer | Hardly anything | Rich, and lively on screen; this is where Rimfolk feels alive |
| A day | The evening's colony news letter; a wish every two days (five open at most); journal lines; dreams; small moods; in the save's last month about six chronicle lines a day, and epithets moving between two people | Wishes; a question now and then (12 hours apart at least) | A fight, a death, a wedding, a name earned | Piling up: many small lines with the same weight, told in several places (systems review: names passing back and forth, life goals switched after each fight, "fighting side by side brought them closer" after every raid) |
| A week | About two Rimfolk events, three or four wishes, most of a dilemma, a calendar day, personal touches, groups at the fire, a story stage | Event answers, a dilemma, a decree, a promise | An event that played out on the map | Rich when an answer plays out; flat when it doesn't (three in ten didn't in the save) |
| A season | A council meeting and its ambition; a seasonal custom (midwinter, harvest dance); perhaps an election or by-election; perhaps a world event; offers from the peoples (each kind on its own 15 to 60 day gap) | The ambition; the custom; votes; an offer | The custom's scene, the meeting | Uneven: the save's seasons repeat the same customs (Spoils three times, the burial of the enemy dead three times, Midwinter twice) while the world's offers barely came |
| A year | Elections, annals, the year's letter, the founding day, calendar anniversaries, traditions forming, colony traits and renown bands moving, traits that change, life goals reached | Who stands, what to keep as tradition | The year looked back on | Told four times (the annals, the year's letter, the founding-day event, the calendar's founding day), and the chronicle that holds the year has already lost its first two months |

**Early, mid and late game.**
- **Early (the first season, 3 to 5 colonists).** The advisor and the room wishes matter most and work well: the
  player learns what to build. Five offices open, but with three people every seat is the same few. World systems are
  mostly asleep. Flat in a good way, apart from the welcome and the first update letters.
- **Mid (seasons 2 to 8, 6 to 13 colonists).** Everything switches on at once: eleven or more seats with elections and
  by-elections, groups and roles, gossip and names, life goals sparked by every fight, the calendar filling, the
  region waking. This is where it piles up. In the save (day 128): elections on days 103, 124 and 128 (eleven seats
  on the first, seven on the second), Harper holding six epithets in 80 days, Stace changing her life goal four times in 25 days (systems
  review), 9 of the 13 journals (the dead keep theirs) full.
- **Late (year 3 on, 15 to 20 colonists).** The slow systems should pay off: traditions, colony traits, renown,
  traits that change, marriages, wards. But the records that would carry the long story are bounded tightly (the
  chronicle 300 lines, journals 60 entries, the history folding everyday entries after 60 days). Worse, the systems
  review found that every colonist's character (marks, lasting nature, views) is lost at each load
  (`Persona/PersonaTracker.cs`, `ExposeData`, since 27 October), so the one system meant to carry a life's change has
  never built up in a played game. The late game risks being the same mid-game noise with older names.

**In each age.** Tribal and medieval colonies are the richest: 675 lines are tagged for a low-tech colony against
440 for industrial and later, 34 for industrial and 55 for spacer alone (most lines fit every age). Diplomacy has its
forms by age (ROADMAP's table) and the Empire keeps its own titles. Two places still speak one age: the council's
offices have one set of names for every age (speaker, reeve, steward, marshal, keeper of days, warden of guests;
`Defs/OfficeDefs/`), so a spacer colony elects a reeve; and several decrees are a village's in every age (tithes, alms, a curfew, holy days).
Under the every-age rule each office should take its age's name (a fire-keeper, a reeve, a foreman, a coordinator).

---

## 3. The seams, family by family

### 3.1 How colonists' feelings are modelled

| Member | What it does | What the player sees |
|---|---|---|
| The game's mood | The one number for breaks; untouched (DESIGN.md, "The safety rule") | The game's needs tab, Rimfolk's Mood tab |
| Rimfolk's moods | 1,445 ThoughtDefs that carry mood, from 40-odd systems (dilemmas about 340, ailments 74, reputation 64, Rimfolk's events about 140, setbacks 51, decrees 38, quirks 31, family 29...) | Each as a line in the Mood tab with its cause |
| Four rules that soften good moods | Standards (rewards shrink as standards rise), good times fade sooner, good moods add up slowly (a shared budget), used to the good life (a soft line over all good moods) (`Standards.cs`, `GoodTimes.cs`, `SmallJoys.cs`, `Moods/GoodLife.cs`) | A +8 that counts as +3, explained four ways |
| Five kinds of small letdown | Pressures (same meal, same work, someone away, crowded rooms, half-built work), setbacks (an idea turned down, a vote lost, a building lost...), quiet troubles, nuisances (24), frictions (34) | Each a mood with its own setting |
| Trust in the council | A colonist's standing with the council, a situational mood, patience with wishes (`Standing.cs`) | People table column; This week |
| The heart | Strain (0 to 100) built by 17 sources (one of them low mood) and eased by 9; its bands give a mood (-3, -5, -8) and slower work; 26 feelings and 4 needs that give no mood and only nudge odds; the day off (`Heart/`) | The heart section of the Mood tab, the inspector line, This week |
| Character | Eight sides (warmth, nerve, temper, humour, trust, pride, openness, outlook) from traits, moved by marks (`Persona/`) | The Story tab's bars and "what shaped them" |
| The colony's ways read against nature | Its own small table from traits to the five ways (`Character/Character.cs`, `Affinity`): who minds an answer ("not agreed", a setback mood) | A setback mood after an answer |
| Against their nature | An act read against the eight sides of character, felt as strain, no mood (`Diplomacy/Nature.cs`) | The heart's page, the news |
| Faith in the colony | Certainty, leanings, conversion | Culture tab |
| Quirks and habits | Small knacks, moods and habits passed on | Story tab tags, bubbles |
| Ailments | 74 mood-bearing ailments | Health, Mood tab |

**Where they overlap or contradict.**
- One job, four rules: softening good moods is done by standards, fading, a budget and a soft line, each with a switch
  and a page of explanation. They were added one after another as the player found moods too easy, and they work
  (DESIGN.md, "Balance targets"), but a player can't follow why a reward shrank.
- Small letdowns are moods five ways over, against the player's own "not everything should be a mood". They also feed
  the heart twice: as low mood (a strain source) and through feelings that read mood thoughts; then the strain band
  adds a mood of its own. Caps and "hard times" keep the loop small, but it is a loop.
- Two readings of who someone is: character's eight sides (talk, marks, against their nature) and the ways' own trait
  table (who minds an answer). The same answer can strain someone by one and leave them unmoved by the other.
- Feelings and needs explain but give the player no lever: they nudge the odds of wishes, goals, dreams and moments.
- Words that mean two things: "trust" (in the council) and character's "trust" (a trusting nature); "openness" is a
  side of character and one of the colony's ways; "temper" a side of character and of a reputation.

**One coherent shape.** The game's mood for sharp moments; **the heart as the backbone for slow wear and relief**
(pressures, setbacks, troubles, nuisances, frictions and acts against nature become strain sources, with a short mood
only for the sharp ones: a vote lost, a building lost); **character as the one nature** (the ways' affinity reads the
eight sides); **one rule for good things growing ordinary** with one line in the Mood tab ("used to good things: this
counts for less") and one setting; the Mood tab's "what weighs on them" as one list whatever system it comes from.
[Player] Moving small letdowns from moods to strain changes the balance; it needs a measured pass with
`Tools/Balance` and the player's yes. [Player] One softening rule instead of four.

### 3.2 How memory and reputation are kept

| Member | Keeps | Names it gives |
|---|---|---|
| Faction memory (`World/FactionMemory.cs`) | Each people's deeds toward the colony, a dozen each, fading over years | A people's tone; `their:grateful`, `their:grudge` |
| Colonist memory and kin memory (`World/ColonistMemory.cs`, `World/KinMemory.cs`) | A people's memory of single colonists: two stores for one idea (kin memory notes on the faction's deeds; colonist memory as ledger regard ties, taking kin memory in) | "the one who set our son free" |
| Colony renown (`Diplomacy/Renown*.cs`) | Read each hour from the history and colonist memory | The colony's band and known-for; each colonist's own "known as" abroad |
| Reputation, deeds and gossip (`Social/Gossip/`) | 91 deeds in the save with who knows each (103 KB, the largest record per item) | Six sides of a name in the colony (courage, kindness, diligence, skill, discretion, temper) |
| Epithets (`Epithets/`) | Records counted by the game and Rimfolk | "the Healer": to whoever counts most, moving when overtaken |
| The colony's ways (`Character/`) | Council answers, decrees, how strangers are treated | merciful, thrifty, open... |
| Colony traits (`Character/ColonyTraits.cs`) | Rolling averages of what the people do | walled, learned, green-fingered |
| Notables' regard, outsider pairs, camps' warmth, regulars | Regard kept beside the ledger's own regard ties, some moved in, some views | |
| Marks (`Persona/`) | What shaped someone | "since going down in the raid on the 5th" |

**Where they overlap or contradict.**
- A colonist carries up to four names (an epithet, a reputation at home, renown abroad, marks) and they change at
  different speeds; epithets churn (Harper six in 80 days, the "Healer" passing four times; systems review).
- The colony carries four (its ways, its traits, its renown and each people's tone); both the ways and the traits
  write bare labels into the chronicle ("Known as Walled", "Known as Generous"; systems review), and they sit on two
  pages (Council, Our ways; People, Renown).
- What someone did is kept three times: as a reputation deed, as a people's deed, and in the history renown reads.
- Regard lives in five places.

**One coherent shape.** **The history as the one record of what was done**, by whom, to whom and where (it already
has the actors, places and kinds); reputation, renown and the colony's name read from it. One **"known as"** per
colonist (what the colony says, what the peoples say, by who knows), with the epithet as its short form and a margin
before it moves (so it changes once a season, not every week). One **"known for"** per colony in three lines: what its
council chose (ways), what its people do (traits), what the peoples say (renown). The ledger keeps every regard;
colonist and kin memory become one. [Player] Whether the six sides of a reputation stay visible or become words only.

### 3.3 The story logs

| Member | Holds | Shown |
|---|---|---|
| The chronicle (`Chronicle.cs`, `comp.chronicle`) | 300 lines; in the save back to day 41 only | Colony, Chronicle; annals; the year's letter; the calendar |
| The history (`World/Ledger/History.cs`) | 800 entries with folding; in the save 405, back to day 1; 396 of them are the chronicle's own lines | Region, History; every card's story |
| `comp.history` | Daily numbers (mood, trust, wealth): a second meaning of "history" | The overview's graphs |
| Journals (`Journal.cs`) | 60 entries a colonist; 9 of the 13 kept full at day 128 (134 KB) | Story tab, the card |
| Annals, the year's letter, the founding-day event, the calendar's founding day | The year, four times | A letter or a question each |
| Personal stories, shared memories (333, 85 KB), marks, talk notes, dreams (80), group histories, camp memory, heirlooms, eulogies | Each its own record | Story tab, cards, news |
| The answers' log (`UI/DilemmaLog.cs`) | Only the council's dilemmas (25 in the save) | Council, Dilemmas ("how it went") |
| The colony news | The evening's digest | A letter a day |

**Where they overlap or contradict.**
- Two timelines with the same lines and a different reach, in two windows. The council window's is the poorer one.
- 21 kinds of question letter (`ChoiceLetter_*`: dilemmas, events, world events, camps, 13 of diplomacy, calendar
  days, meetings, elections, raiders' camps, hamlets, journeys); only dilemmas record how they went.
- Each system picks its channels: 72 files write the chronicle, 85 the news, 65 letters, 118 messages, 72 speech, 49
  journals. Nothing weighs a happening once and tells it once in the right places. The systems review found the same
  conversion told twice a day apart by two systems.

**One coherent shape.** **The history as the one timeline**: the chronicle becomes its colony view (one list fewer, no
lost first months); daily numbers renamed. **A teller**: a system records a happening once with its weight
(everyday, notable, milestone) and who was there; the teller sends it on (a milestone: a letter and the chronicle; a
notable one: the news and the journals of those involved; everyday: the history, and a journal line by chance). Every
question writes the answers' log. One year: the annals, the year's letter and the founding-day event as one, on the
calendar's founding day.
[Player] The chronicle as a view of the history (older saves keep theirs, read once).

### 3.4 Where events come from

About 25 sources, each with its own clock: Rimfolk's events (drawn by the game's storyteller by default, the one
source that already rides on a director; else one every 4 days), world events (one every 45), dilemmas
(one every 10), encounters (225), nuisances, social events (104), moments, camps (a daily try), visitors, callers,
visitor services, regulars, visitors for what the colony keeps, couriers, missions and hosting, story chains, the
region (raiders' camps, trading posts, hamlets, travellers, rise and fall, follow-ups, far news), 13 kinds of
diplomatic offer (each with its own gap of 15 to 60 days and daily odds, `Diplomacy/*Math.cs`), letters from
neighbours, outsiders talking by themselves, calendar days, elections, meetings, banishment, moving on, personal
stories, life goals, secrets, vendettas, the weather's disasters, the herds and beasts, ailments, scenarios.

**Where they overlap or contradict.**
- Rimfolk's events and dilemmas share their machinery (options, effects, letter variants) but not their settings,
  pages or log; a player can't tell an event's question from a dilemma's, and only the dilemma keeps "how it went"
  (an event's choice is one line in the chronicle, "The colony chose ...").
- People who come to the map are run by about ten trackers (visitors, callers, services, visitors for what the colony
  keeps, couriers, camps, hosting, envoys, feast guests, the parties of wards, matches, ransom, fealty and truces),
  each its own lord job, tick, and rules for danger, beds and leaving.
- Pacing is per system. The 12-hour gap between questions (`Lean/LetterGap.cs`) and the per-kind flood caps of `Tell`
  (5 kinds, 20 messages and 6 letters a day each) are the only shared limits. There is no budget for "big things this
  week", and no quiet days by design.
- Some sources hardly fire in a real base: camps found no room 23 times in 20 days; diplomacy, missions, callers,
  services and the visitors for what the colony keeps hold nothing at day 128 (diplomacy was built in the save's last
  days, so it needs a fair season before judging it).

**One coherent shape.** **A director per colony**: each source offers candidates (with a weight and a kind: a question,
a visit, a scene, news), and the director keeps a weekly budget of big beats by colony size, spaces them, mixes inside
and outside, and keeps quiet days; the sources keep their own rules for what can happen. Rimfolk's events already
show the way (they ride on the game's storyteller), and the director should read the storyteller's own threats too,
so a raid week is a quiet week for Rimfolk's asks. **One question letter** with
one log. **One visitors engine** (arrive, stay, do, leave; services as data), later. [Player] How many big beats a week
for a colony of ten, and whether quiet days are wanted.

### 3.5 Gatherings and scenes

| Member | Engine |
|---|---|
| Answers that play out: feasts, bonfires, toasts, vigils, walks, work parties, visits, lessons (`PlayOut/`) | `LordJob_PlayOut` on the game's gathering, `PlayCook`, `PlayYard` |
| Council meetings, election hustings and counts | Their own lord jobs on the same gathering base |
| Calendar days kept (feast, games, bonfire, gifts, hunt, planting) | Through PlayOut |
| Seasonal customs (34) becoming traditions after two years running | Dilemmas, `ColonyState` |
| Calendar days becoming traditions after three years running | `Calendar/CalendarMath.cs` |
| Feasts: a custom's, the calendar's, a decree's feast day, a victory feast, feasts with guests | PlayOut and PlayCook; feasts with guests have their own lord job, book and contests (`Diplomacy/Feast*`) |
| Family suppers, faith evenings, groups at the fire, toasts for a goal reached, weddings, burials, small habits, the first pot | PlayOut, moments, habits |

**Where they overlap or contradict.**
- Two festive years with two rules for a tradition (two years or three) on two pages (Our ways, Calendar). A
  midwinter custom and a calendar feast can come in the same season.
- Feasts by five routes, cooked by two engines.
- Scenes are many and close together, and mostly routine (the staging review's working numbers, 3.6: about five
  gatherings a week in a colony of ten, calendar days alone ten a year), so the ones that carry a story (an event's
  answer, a camp, a wedding) are lost among them.
- In the save 9 of the last 30 answers that should have played out did not (nobody free, nowhere to hold it,
  something else had everyone's attention, nothing needed doing), against "what the words say happens is what the
  player sees".

**One coherent shape.** **PlayOut as the one scene engine** (meetings and elections stay on the shared gathering base;
feasts with guests move onto PlayOut's feast); **one festive calendar** where customs are days on it, with one
tradition rule; a scene budget (the staging review's). A play-out that can't happen changes the answer's words (the
letter says what will happen only once it can), or waits and says so. [Player] Two or three years for a tradition.

### 3.6 Tied to the other reviews

- **The staging review** counts how often scenes and messages come; its budget is the director's scene share (3.4) and
  the scene engine's (3.5). The holistic point is that a budget only holds if one place decides. Its working numbers
  (`audit-pl/scenes10.txt`, its report not yet written when this one closed: 300 simulated years, 10 colonists,
  default settings, a well-run colony) bear out problem 4 from the scene side: about 42 gatherings a year, about five a
  week, half of them less than a day after the one before; the biggest sources are the colony's own routine (calendar
  days 10 a year, groups calling their people 7, toasts for life goals 5, meetings 4, family suppers, decrees and
  elections 2.5 each), while everything from outside (events 0.8, camps 0.5, encounters 0.3) is under one a year
  each. Its draft allowance (two routine scenes a week, a day and a half apart) keeps about 23 of 43 as scenes of
  their own and folds the rest into smaller ones: the director's scene share in this plan.
- **The systems review** (`audit-systems.md`) judged each system alone and reached several of the same seams from the
  other side: one name for the colony (its item 3 is 3.2 here), a colonist's name that sticks and one "known as"
  (item 4, 3.2), the chronicle read as a history and the year ended once (item 7, 3.3), one rule for traditions
  (its verdicts, 3.5), renown placed under People while the peoples live in the region window (4 here), and settings to
  thin and group (4 here). Its repetitions (names passing, goals churning, templates reused, labels as chronicle lines,
  one conversion told twice, two drills on one day, nine vendettas for one death) are mostly what these seams
  produce: each fix is local, but the cause is that each system tells and names on its own, so the same kind of slip
  will come back with the next system unless the backbones exist.
- Its first bug changes this review's reading: character (`Persona/`) is lost at every load, so it has never had the
  chance to show what it does over a long game. Character as the one nature (3.1) is still the right backbone, but it
  has to be fixed and then watched over a real season before anything else leans on it.
- Where the two differ in emphasis: the systems review keeps fealty and vassals but would thin them into pacts; this
  review leaves that to the player once diplomacy has had a real season in a save. It finds the play-out audit's
  candidates all covered (every answer's words have a play-out); this review finds three in ten of those play-outs
  didn't take place in the save. Both are true: the words are covered, the map isn't always ready.

---

## 4. The interface and settings as a whole

**Entry points.** Four bar buttons and "more", the slow speed buttons; four windows with thirteen views; the
Almanac's own window; search, This week's inbox (48 providers), following; cards in a drawer with previews; three
inspect tabs (Story, Culture, Mood), the Social tab's column and a line in Needs; six colonist bar badges; eight
on-screen panels and the hover card; about twenty dialogs; 21 kinds of question letter; right-click "Talk with...";
263 settings rows on seven pages; 17 key bindings.

**What works.** The interface releases of 4 and 5 November turned 34 pages into 13 views with a rule for every new
thing (`docs/design/interface.md`, "Where a new thing goes"). Names lead to cards; the inbox gathers what waits; one
visual grammar. This is the strongest part of the whole and the model for the other seams.

**What doesn't.**
- What waits on the player is built four times: This week's inbox, the on-screen council panel (`Hud/HudCouncil.cs`,
  its own list), the bar's badges (a wish waiting, a promise at risk) and the game's alerts (open wishes, urgent
  advice). They can disagree.
- Two doors to the story with different reach (Chronicle, History); the colony's name on two views (Our ways,
  Renown); the answers' log for dilemmas only.
- The Almanac: 463 pages, about 400 of them the game's own knowledge (food, rooms, health, defense, mods, mistakes).
  Rimfolk's hundred-odd systems share about 60 pages on its "Council" shelf, so a system often has a paragraph in a
  shared page and no card of its own to point the "?" at. Good as a book; thin as a map of Rimfolk.
- After each update one letter; six came in the save's last fourteen days.

**Settings.** 263 rows (Council 32, People 42, Heart and health 14, Events 58, Building 31, Interface 83, Advanced 3).
Grouped by system, not by what a player thinks about:
- "How much happens": about twenty sliders on six pages (wishes, dilemmas and meetings on Council; moments, social
  events, dreams and personal stories on People; ailments and strain on Heart; encounters, events, world events,
  travellers, raiders' camps and outsiders' chatter on Events; personal touches on Building; messages and letters a day
  on Interface).
- "How hard": stakes and the strength of downsides and rewards on Council, life's ups and downs and the four softening
  rules on People, strain on Heart, illness on Heart.
- Ten switches for other mods (RimMarket, Alpha Skills, Alpha Genes, RimWorld of Magic, Faction Territories) under
  Interface, beside the bar buttons; Hospitality's checklist under Events; secrets,
  vendettas and faith under Bonds; "Moving on, by the council's word" under "Realistic ways to die".
- The presets were made before the region and diplomacy: Light touch and Demanding don't touch them, Story-rich only
  switches camps on, and the "what's switched on" setups (Everything, Council only, Advisor only) leave the region's
  own switches (raiders' camps, trading posts, hamlets, travellers), diplomacy's eleven and the couriers as they are
  (`UI/SettingsCatalog.cs`, `BuildPresets`, `PeopleSystems`). So "Light touch" doesn't quiet the newest third of the
  mod.

**Proposal.** Five pages by the player's questions, keys unchanged (the catalog already tests each row is on one page):
**How much happens** (one pace dial that scales every source, and each source's rate under it), **How hard** (stakes,
pressures, standards, strain, illness, harm), **What's on** (the systems by pillar), **How I'm told** (messages, letters,
news, speech, panels, badges, hover card, windows), **My other mods** (only the mods present). Presets that reach every
pillar. [Player] Regrouping the settings.

---

## 5. Standalone tools

**Stay standalone** (they do one job well and the player uses them by hand): the defense planner with its lines, cover
drag and gate reminder (the player's word); the room planner, templates and rearrange; planning ahead, making and
chains, automations (the player's rule: suggest only, one click each); fields by the weather; journeys planned ahead;
shelf reserves and build priorities; slow speeds; the on-screen panels; the Almanac.

**Would gain from a light tie** (each a read, not a merge):
- The on-screen council panel should read the inbox rather than build its own list (or go, if the inbox's panel is
  enough). [Player]
- Defense: the marshal's few words on the plan's next step, and the fight's account (who held which line) feeding the
  deeds and marks that already exist; the plan's step as a goal for the "never again" ambition.
- Fields by the weather: the frost harvest and the planting day as one work party on the calendar.
- Journeys: the journey planner for diplomacy's errands abroad (envoys, feasts at their town, wards taken home).
- Advisor, wishes and ambitions already share their rules; keep.

---

## 6. Cost (where it matters to the whole)

- **Save.** About 1 MB of 31 MB at day 128 (3%); other mods' components are bigger. `save_estimate.py --strict`: 506
  collections, all bounded, about 2.4 MB after a year and 3.1 MB after five. The chronicle (64 KB) and the history
  (111 KB) hold the same lines; reputation's deeds are the costliest per item (1.1 KB each, the lists of who knows).
- **Ticks.** 52 passes on Rimfolk's clock, 49 game components and 12 map components each with a tick check; the static
  audit puts all clocked work far below the per-frame work of an open planner window. No slowdown reported.
- **Frames.** Heaviest while open: rearranging a room and the defense planner (static estimate only; worth a profiler
  look if the player ever feels them).
- **Load.** 9,917 defs in 891 files (17 MB of XML), an 8 MB DLL, 208 Harmony patch attributes. Worth one measured
  load on the player's modlist; thinning the 1,979 thoughts and 829 pools would help most.
- **The real cost** is attention: each new system has brought its own tracker, clock, settings, letters and names. The
  backbones below are what keep the next system small.

---

## 7. The target shape and the phases

**What Rimfolk should feel like.** A colony that remembers and asks. Every hour a small thing seen on the map. Every
few days one thing to decide, and its answer played out where the player can see it. Every season one gathering that
matters and one story from outside. Every year a look back the player wants to read. Quiet days between. Every system
speaks through the map first, then This week, then the evening news, then the history, and never twice.

**How to balance it: fewer, bigger, remembered.** The player asked whether too much is going on. The whole says yes in
the middle scales (the day and the week) and no at the ends (the hour on the map is lively; the season and the year
are thin and repetitive). So the balance is not "less of everything" but a move of weight:
- **Less:** small moods (fold into the heart), chronicle and news lines for everyday things (the teller), names that
  move every week, the same custom or visitor again without change, many small asks in one week.
- **More:** callbacks (what was answered last season shaping this season's question and talk: the facts service
  already reads "then: still / over / dead / changed"), outcomes seen on the map every time, ties that last and come
  due (the ledger), seasons that differ (a custom once a year, told as "again" or "for the third year"), lives that
  visibly change (character, once it survives a load), and the outside world arriving (camps that find room, offers
  that come).
- **Impact** comes from consequence and memory rather than from mood size: the director lets one big thing breathe
  instead of five small ones crowding it.

**Backbones, by pillar.**

| Pillar | Backbone | Feeds it | Thins, merges or goes |
|---|---|---|---|
| Council | The question (one letter, one log) and trust | Dilemmas, events, offers, customs, elections | 21 question letters to one base; the answers' log for all |
| Inner lives | The heart (strain and relief) and character (one nature) | Pressures, setbacks, troubles, nuisances, frictions, acts against nature | Four softening rules to one; the ways' trait table reads character |
| Social life | Bonds and groups | Moments, talk, gossip | Reputation read from the history |
| The colony's story | The history, a teller, PlayOut, one calendar | Every system's happenings | The chronicle becomes a view; annals and the year's letter one; customs on the calendar |
| World and region | The ledger, the region map, a director per colony | Events, world events, camps, visitors, couriers, region features | Colonist and kin memory one; visitors on one engine (later) |
| Diplomacy | The ledger's ties | Every offer, through the director | Renown and the colony's name one "known for" |
| Tools | Stay standalone | Read the same facts | The council panel reads the inbox |

**Cut, only with the player's word.** The "systems talk to each other" switch once the links are native; the
separate chronicle list; deeds kept twice; the anniversary marks if the calendar keeps them. Nothing a player has
grown fond of goes without a list first (CLAUDE.md).

**The phases.** Each is a release or two, plays as before where it doesn't change things, keeps older saves, and keeps
every rule in CLAUDE.md (soft integrations, harm at the game's rate and shown, every age, the colony as the family,
answers on the map, one-sentence bubbles, Speech and Together).

0. **What the save shows (bugs first).** Character lost at every load (`Persona/PersonaTracker.cs`, found by the
   systems review; the first fix of all, since phases 3 and 4 build on character); the election results that leave
   seats out; camps that never find room in a grown base (`Camps/CampSite.cs`, a circle ruled out by any home-area or
   zone cell; the "noroom" diary); answers that don't play out (say what happened instead, or wait and say so);
   writing slips in shipped lines and their fills (a wish's label in mid sentence, "year(s)", lowercase chronicle
   lines, bare labels as lines, "ended in over", "0 of them born"); one line per conversion; epithets with a margin
   before they move; life goals not switched after every fight. Mostly the systems review's list. Test on the save.
1. **One record.** The history is the timeline; Colony, Chronicle reads it; daily numbers renamed; every question
   writes the answers' log; one year's letter. Test: no line lost, the chronicle reaching day 1 again. [Player]
2. **One teller and one pace.** A weight on each happening and the teller's channels by weight; a director per colony
   with a weekly budget and quiet days over the existing clocks (the staging review's numbers); "How much happens" in
   one place; presets reaching every pillar. Test: the save's lines a day and letters a day before and after.
   [Player]
3. **One heart.** One softening rule; character as the one nature; small letdowns as strain with short moods for the
   sharp ones; one "what weighs on them". `Tools/Balance` before and after, to the targets in DESIGN.md. [Player]
4. **One name.** One "known as" per colonist and one "known for" per colony, from the history; colonist and kin
   memory one; deeds once; office names by age. [Player]
5. **One festive year.** Customs on the calendar with one tradition rule; feasts on one engine; the scene budget.
   [Player]
6. **One way in.** One question letter base; the on-screen council panel reading the inbox; settings in five pages;
   the visitors' engine last, as it touches the most. [Player]

Then the work waiting (harm parts 2 and 3, schemes, stage 3) is built on the backbones, each a source for the
director and the teller rather than a new channel. Stage 3 most of all: many colonies multiply every source, so the
director and the teller (already per colony in this plan, as `ColonyState` is) should exist before it. The work under
way in parallel (the harm service, vendettas and hostages, medical triage) touches phases 0 and 2: harm is a source
the director should weigh, and the vendetta rework already answers the systems review's "nine vendettas for one
death". [Player] The order of these phases against that work.

---

## Decisions for the player

1. Small everyday letdowns (pressures, setbacks, quiet troubles, nuisances, frictions) wear on the heart's strain,
   with a short mood only for the sharp ones, instead of each being a mood. (Phase 3; balance changes, measured.)
2. One rule for good things growing ordinary, instead of four. (Phase 3.)
3. A weekly budget of big beats, and quiet days: how many for a colony of ten? (Phase 2; the staging review's draft
   for scenes is two routine ones a week, a day and a half apart, against about five now.)
4. The teller: Rimfolk decides the channel by how much a happening matters, so fewer lines reach the chronicle and the
   news. (Phase 2.)
5. The chronicle becomes the colony's view of the one history. (Phase 1.)
6. One "known as" per colonist (the epithet its short form, moving more slowly) and one "known for" per colony; do
   the six sides of a reputation stay visible? (Phase 4.)
7. One festive calendar: seasonal customs become calendar days; two or three years for a tradition? (Phase 5.)
8. Office names by the colony's age. (Phase 4.)
9. Settings in five pages by the player's questions; presets for every pillar. (Phase 6.)
10. The on-screen council panel: read the inbox, or go. (Phase 6.)
11. Camps that find no room in a dense base: widen where they may camp, or let them come less and say why. (Phase 0.)
12. The order: these phases before or between harm parts 2 and 3, schemes and stage 3.
