# Rimfolk 0.104.0: how often the colony stages things, and a budget for it

An audit of every source that sets colonists doing something together on the map, pulls them from work, or tells the
player about it; how often each comes for a colony of about ten adults; what the player's own game recorded; where the
sources overlap; and a proposal. No mod code was changed. The Balance tool has a new counting mode for it
(`Tools/Balance/Scenes.cs`, `--scenes`; commits 403a57c, 45e0b3a and 5bcd2ab in the audit's worktree, not pushed; an ordinary Balance run is byte-identical).

## Summary

**The headline.** Rimfolk stages a gathering (people sitting or standing together: a feast, a toast, a fire, words, a
meeting) about **four times a week**, and a group outing (a work party, a walk, a vigil, the dead carried out) about once
more. The player's game measured **3.6 gatherings a week over its last 60 days, 4.2 over its last 30** (5.1 with outings),
with a calendar still only half grown. The model of a settled colony of ten, in its third year, gives **4.5 a week with
average play and 4.9 well run**. Either way, **four gatherings in ten come within a day of the one before**, there are six
to eight days a year with two or more on the same day, and the busiest weeks hold eight or more. The base game's own
spontaneous party comes about once in forty days (its mean time as recalled, not checked against the game here; its
rituals and other mods' gatherings come on top), so Rimfolk's gatherings outnumber it by roughly twenty to one. Of the plans Rimfolk made, three in ten
never happened (nobody free, nowhere to hold it, or another gathering already on), each still sending its line and its moods.

**The five biggest contributors** (model, average play, a year of 60 days; the player's game over 60 days in brackets):

1. **The colony calendar's days**: about 9 gatherings a year once the calendar fills, most of them feasts (4 in 60 days, with
   the calendar half full). The player's calendar for next year already holds five days between the 43rd and the 47th day
   of the year: a remembrance, two victory feasts over the same enemy, a wedding day's feast and a faith's evening.
2. **Groups called to their habit**: a close group's evening at the fire every twelve days or so, about 8 a year with two
   close groups (4 in 60 days with the player's one group of seven).
3. **Seasonal customs asked as council questions**: the midsummer fire, the swim, the harvest dance, the longest night's
   supper, the planting day; almost every one offers a gathering and becomes a tradition after being kept twice (5
   gatherings and 2 outings in 60 days; 7 of the player's 14 council questions were customs). The model gives only 2 to 3
   a year here, so its total is an underestimate by about this much.
4. **Toasts when a life goal is reached**: 3 to 5 a year (5 in 60 days, two of them on the same evening for a couple who
   had just married).
5. **The council's own business**: meetings, the election's hustings, count and ceremony, and decrees that gather people
   (about 9 a year; 8 in 60 days, three of them the year's election inside five days).

**The proposed budget.** Count every gathering and outing in one place per colony, as letters and messages already are.
What the player chose always happens (an answer, a day kept, a vote, a wedding, a decree) but counts; the colony's own
doings (a group's evening, a family supper, a toast, a camp's show, a child's birthday) wait for room: **no more than two
of those in any seven days, never within a day and a half of another gathering, and let go after three days of
waiting**. Days of the calendar within three days of each other are kept together; a calendar day that falls within two
days of another gathering is kept at supper instead of with a feast; **one seasonal custom a season at most**; a life goal's
toast is raised at the next gathering rather than on an evening of its own; the hustings join the count. A floor keeps the
quiet-days setback honest: never ten days without one. Modelled, this brings the colony to **2.6 staged scenes a week
(from 4.7), its busiest week to 8 (from 14), days with two to 2 a year (from 9) and gaps under a day to 22% (from 46%)**,
while everything still happens in some form. Each scene kept should give the player something: a choice, a lasting mark
(a tradition, a bond, a group's closeness, a story that comes back), or at least words that remember the last time.
Camps, which never came in the player's game (fixed in 0.104.1), add little: about one camp every five weeks, a show or a
song at their fire in about one camp out of four.

What needs the player's word: the weekly allowance and its numbers, merging calendar days, one custom a season, and
folding toasts and hustings into other gatherings (all are changes to how often the game shows something). Nothing
proposed breaks a binding rule: answers still play out on the map, nothing is switched off for an age, and family
suppers stay flavour.

## 1. Inventory

Each source that stages something with colonists together or in front of others, by what starts it. "Waits for" names the
cross-system gates: `PlayOuts.WhyNotNow` (danger, a threat on the map; for a gathering also another gathering already on,
half the colony drafted, someone bleeding: `MeetingAttendance.WhyNotNow`), the play-out's hours and wait (6 hours by
default, 30 at most, then its moods and a line instead), and the one-at-a-time rules. There is **no gap between two
gatherings and no weekly count anywhere**: the only cross-system rule is that a second gathering cannot start while
another is on the map, so it waits or falls through.

### 1a. Gatherings (people together, a lord of the game's party)

| Source | Trigger and file | Rate, mean time between | Cooldowns and caps | Takes | Waits for, shares | Setting |
|---|---|---|---|---|---|---|
| Answers that gather (feast, gathering, bonfire, toast, words) | a dilemma's, event's, story chain's, world event's answer: `PlayOutEffects.cs` effects, started by `PlayOutTracker.TryStartGathering` | dilemmas about every 10 days scaled by mood (0.6 to 1.3), plus questions after a raid and from strangers (13 a year modelled); Rimfolk's events up to half the storyteller's everyday pool (6 a year); world events every 45 days. A random pick gathers people 12.7% of the time for dilemmas, 12.3% for events, 1.4% for world events | a question that can wait never within 12 hours of another (`LetterGap`); a dilemma def's cooldown | the colony or those the answer names; 1 to 3 hours, between 6 and 22 by default | `WhyNotNow`; the answer's hours and wait; then the moods and a "never came" line | Answers play out on the map (`answersPlayOut`); Dilemmas; World events |
| Seasonal customs | 31 dilemmas with seasons, ordered three times as likely as others (`Dilemmas.TryFire`, `Dilemmas.cs:1216`; `DilemmaWorker_Custom`), in `Council_Dilemmas_Customs*.xml`, `Rimfolk_Dilemmas_Customs_New.xml`, `Rimfolk_Dilemmas_Lives_Customs.xml`, `Council_Dilemmas_Seasons.xml` | 74% of them offer a gathering, usually first; the player's game asked one about every 8 to 9 days | each def 50 days apart (once a year); a tradition once kept twice (`traditionAfter`), broken at a cost (`Dilemmas.Tradition`) | the colony, 2 to 3 hours, mostly evenings | as answers | as dilemmas |
| Calendar days | `ColonyCalendar.ComeRound`, `CalendarKeeping.KeepDay`/`DoWay` (a choice letter: keep, pass, a ritual) | each day once a year; up to 12 of the colony's own and 8 named by the player (`CalendarMath.MaxAutoDays`, `MaxNamedDays`); founding, harvest, weddings, battles won, the walls, a new crop, a homecoming keep a feast by default (`ColonyCalendar.DefaultWay`) | none between days; the grieving sit out a festive day (`Interplay_Gatherings`) | feast, games, fire: everyone at home, 3 hours (a fire 17 to 23); remembrance: the mourners at the grave, 1 hour; faith: an evening of faith | `WhyNotNow`; a feast falls back to the game's own party, then to a better supper's mood | Colony calendar (`colonyCalendar`) |
| A group called to its habit | `GroupHabits.Daily`, once a day | a close group (cohesion 50 or more) after 8 days, then 1 in 5 a day: one every 12 days (`GroupMath.MayCall`, `CallChance`) | one group's call at a time, none while three plans wait; at least three invited | the group (3 to 7), 2 hours; an evening at the fire (a fire laid from the stores, music), or the rec room | `WhyNotNow`; habit hours (fire 18 to 23) | Social life (`socialLife`) and social web (`socialWeb`); answers play out |
| A group keeping its habit as it comes | `GroupHabits.Hourly` | once a day per group, when two or more are already together at it | once a day | nobody moved; a line said, a small memory | none | as above |
| Council meeting | `Ambitions.TryStartMeeting`, `MeetingAttendance` | every 15 days (`daysBetweenMeetings`), but only when no ambition runs; the player's game held 4 in 128 days | one at a time | everyone with a say, **called from ordinary work and woken** (`meetingsCallFromWork`), 1 to 2 hours | `MeetingAttendance.WhyNotNow` | Ambitions (`enableAmbitions`) |
| Election | `OfficesElection`, `ElectionGathering` | yearly: the hustings the evening before polling, the count on polling evening, and a short ceremony for a seat nobody stood for | at most two put-offs (`ElectionMath.MaxPutOffs`) | people drift over when free, 1 to 2 hours each, at 18 | `WhyNotNow` (danger), its own lord | Offices, Election event |
| Life goal reached: a toast | `GoalRewards.Toast` | on each goal reached (3 to 5 a year modelled; 5 in the player's 60 days) | none | those who think well of them (opinion 20), kin and partner; 17 to 21, about an hour | play-out wait 4 hours | Aspirations; answers play out |
| Children's growth birthdays: a toast | `FamilyLife.Notify_Birthday` | 7, 10, 13 and 18 with a parent at home | none | the family, 17 to 22, under an hour | 4 hours | answers play out |
| Family supper | `FamilyBonds.Supper` | a family of three or more at home with a meal each: 6 days apart, then 1 in 5 an evening | one family a day | the family, 17 to 20, 1.5 to 2 hours, at the table | 3 hours | answers play out (no setting of its own) |
| Wedding | the game's own ceremony; `WeddingPlans` when the council planned it (a question in `Rimfolk_Dilemmas_Family.xml`, or a match, `MatchDays`) | as couples marry (3 in the player's first 128 days) | none | the colony, the game's ceremony on a fine evening, a feast or toast after a planned one | the game's | the game's; a planned one: Dilemmas, answers play out |
| Evening of faith | `FaithEvenings` (from a calendar day, a devout believer's question, a wish) | at most one in 5 days, the same faith one in 10 (`FaithCouncilMath`) | as said | believers and those who choose to come, 17 to 23, 2 hours | `WhyNotNow` | Faith in the colony (Ideology), answers play out |
| Decrees that gather | `Decrees.cs:605` (`onEnact`): a feast day, festival days, holy days, militia drills' address, one faith | when the player declares them (3 in 60 days) | the decree's own | the colony, at noon or the first evening | as answers | Decrees |
| Ambition reached | `Ambitions.cs:1029` (`achievedEffects`) | 9 of the 66 ambitions: a great feast, a toast with the first barrels, a fire | none | the colony | as answers | Ambitions |
| Camps | `CampTimer.HourAt`, `CampKinds` (show, song), `CampAsks` | 15 days' rest after a camp, then 1 in 20 an open morning: about one camp every 35 days; it stays 4 to 10 days; a day's help 15% (a show or a song at their fire, an evening's gathering); an ask a day and a half in | one camp at a time | the colony walks over, 1.5 to 2.5 hours | `WhyNotNow`, the question gap | Camps |
| Diplomacy: feasts with guests, contests | `Feasts.cs`, `FeastContests.cs` | planned by the player, or the steward's suggestion a season apart | as said | the colony and up to three notables' retinues, a day | its own | Feasts with guests |
| Personal stories' stages | `ArcTracker.Apply` | 4.7% of stages gather (a supper for three, a secret told at a gathering), 9.4% a visit or lesson | the story's own | 3 or so | as answers | Personal stories |
| Encounters | `EncounterTracker.Fire` | 0.9% gather | up to 3 encounters a day | | | Strangers |

### 1b. Outings and small errands (no lord: jobs of their own, `PlayGroups`)

| Source | Trigger and file | Rate | Caps | Takes | Waits for | Setting |
|---|---|---|---|---|---|---|
| Work parties, walks together, vigils, searches, hunt parties, the dead carried out to a pyre or graves | answers (10.4% of dilemma picks, 4.8% of events'), calendar ways (Planting, Walk, Hunt), the earthquake's repairs (`WeatherDisasters`) | 2 a year modelled; 5 in the player's 60 days (two pyres, the planting, a vigil at the pens, the shearing) | at most 8 on one, two groups at once, 4 hours (a vigil 8) (`PlayOutMath.MaxGroup`, `MaxGroupsRunning`) | those able to; **jobs given over ordinary work** (`PlayOuts.Interruptible`) | `WhyNotNow`; danger ends it | answers play out |
| Visits, lessons, gifts carried out, things handed over, rounds | answers (18% of dilemma picks), herd stories (`HerdStory`), the dying's last season (`LastSeason`), a relative who came (`MovingOnHeld`), the calendar's gift day (pairs, `Together`) | 8 a year modelled | one visit per colonist at a time | one or two, an hour | `WhyNotNow` (danger only) | answers play out |
| Small habits | joy givers (`SmallHabits`, `MoreHabits`, `QuirkHabits`): the grave, the fire after a hard fight with those who fought, old stories by the evening fire, stars, plants, animals | only in recreation time, each habit half a day to a day apart per colonist | the game's joy rules | one, sometimes a few at the fire | recreation only | Small habits |
| Visitors at the gate and their camps | `Visitors/` (from event letters), `VisitorCamp` (their fire; colonists may sit with them, a joy giver), `VisitorMusic` | with the events that bring them | one at a time per kind | one or two walk out | danger sends them off | Goods and people come and go |

### 1c. What pulls colonists from work

- **The council meeting** calls everyone from ordinary work and has sleepers woken (on by default).
- **Every gathering** is the game's own party under Rimfolk's lord (`LordJob_PlayOut`): those invited leave ordinary work
  to join, as they do for the game's parties, except those in danger, in a sickbed, bleeding, starving, exhausted, caring
  for someone or under the player's order (`PlayOuts.CanJoin`). An answer's gathering may start any hour from 6 to 22.
- **Outings** give each member a job over whatever ordinary work they were at (`PlayOuts.Interruptible`), up to 4 hours.
- Modelled, gatherings take about **480 colonist-hours a year** in a colony of ten (8 a colonist a 60-day year), a third of
  it the calendar's feasts.

### 1d. Letters, messages and bubbles

- Every staged scene sends a message when it begins and its closing words when it ends (`PlayOutTracker`, `Tell.Message`
  at Notable), and a "never came" line when it falls through; a calendar day sends its choice letter first and rings a
  bell (`Chimes`); a custom or an event is a choice letter.
- All of Rimfolk's telling goes through `Tell` (`UI/Tell.cs`): 20 messages and 6 letters a day per kind per colony, the same
  text within 4 hours told once, past the cap into the evening's colony news; questions are never capped but never come
  within 12 hours of another (`LetterGap.Hours`). **Staged scenes have no such ledger.**
- Bubbles (`Speech.Say`, about 60 callers) are rate-limited by their callers, mostly a line per colonist a day or half a
  day (`VisualTouches`), and drawn only on the map being looked at. At a gathering: a song's notes, lines from its pool,
  cups raised at a toast (`GatheringTouches`), a group's habit line, the election's toast to the new council.
- Drink and smoke before work (`Integrations/BeforeWork.cs`, Alpha Skills only) is one colonist and a bubble now and then:
  no scene.

## 2. Estimates: a colony of 10 adults over a 60-day year

From `dotnet Tools/Balance/bin/Release/net8.0/Balance.dll --scenes . --runs 300` (300 colony-years for each way of playing,
default settings, a third-year calendar from the player's own). The answers, the calendar, meetings, evenings of faith and
life goals are the simulator's own; groups, elections, weddings, family suppers, camps, decrees, children's birthdays and
stories' stages are added at the code's rates (read from `GroupMath`, `FamilyLifeMath`, `CampTimer`, `CampTracker`,
`CampKindDef`, `FaithCouncilMath`) and at the documented `Rates.Scene*` assumptions where the code can't say.

| Source (average play) | Gatherings a year | Outings | Small | Colonist-hours |
|---|---|---|---|---|
| Calendar days | 9.3 | | 0.9 | 167 |
| Groups calling their people | 7.8 | | | 60 |
| Council meetings | 3.8 | | | 56 |
| Life goals' toasts | 2.9 (5.1 well run) | | | 10 |
| Decrees declared | 2.6 | | | 21 |
| Elections (hustings, count, ceremony) | 2.5 | | | 25 |
| Family suppers | 2.2 | | | 12 |
| Evenings of faith (in a colony of several faiths) | 1.6 | | | 20 |
| Seasonal customs | 1.2 (the player's game: about 5) | 0.6 | 0.4 | 23 |
| Personal stories' stages | 1.1 | 0.4 | 2.0 | 11 |
| Weddings | 1.0 | | | 23 |
| Rimfolk's events | 0.7 | 0.1 | 0.8 | 10 |
| Camps | 0.4 | | 0.7 | 8 |
| Ambitions reached | 0.3 | | | 7 |
| Dilemmas after a raid | 0.3 | 0.9 | 0.3 | 14 |
| Encounters, children's birthdays | 0.6 | | 0.1 | 5 |
| Other dilemmas, strangers' questions | 0 | | 2.5 | 6 |
| **All** | **38.3 (4.5 a week)** | **2.0** | **7.7** | **477** |

- Well run: 41.9 gatherings a year (4.9 a week); neglectful: about 36, mostly from fewer life goals and customs kept.
- **Bunching** (average play): any seven days hold 4 gatherings in the median week, 7 or more one week in ten, 14 at the
  busiest; 8 days a year with two or more, 1.3 with three; the median gap to the next is a day, 44% come within a day and
  71% within two.
- **Camps** at their real rate (the save never had one; fixed in 0.104.1): about 1.7 camps a year, each staying 4 to 10
  days; 0.4 shows or songs at their fire a year and 0.7 small asks played out, so about one scene more every two months,
  inside the week a camp is there. Small next to the rest.
- **The model's known gap**: it asks seasonal customs two to three times a year, the player's game about seven times in 60
  days, because the real game's other dilemmas fail their own checks more often than the model's flat 80%, so the custom
  first in line is asked. With the player's rate, the total is about 42 gatherings a year, **5 a week**.
- **Per age.** No rate depends on the colony's tech level: the same scenes come in every age, in its form (a clan's moot
  or a council meeting, a fire or a hall's table, beer or the age's drink). With Ideology and several faiths, evenings of
  faith add about 1.6 a year. With the base game alone (no Ideology), the faith's day and evenings drop out.
- **A season.** The 60-day year has four 15-day seasons. A season brings about 2.3 calendar gatherings, 2 group evenings,
  a meeting, the season's custom or two, a toast or two, and every other season the election, a wedding or a camp: about
  ten gatherings in fifteen days.

## 3. The player's game (Atia.rws, day 127.9)

Read with scripts kept outside the save's folder (`scratchpad/audit-pl/`, run with `python3 -I`): `records.py` (journals,
chronicle, the week's memory of answers, the world's history), `calendar.py`, `answers.py` (each "the council chose"
line matched to its def and answer by their labels, with what that answer stages), `archive.py` (the game's archive),
`journalpools.py` (each journal line matched to its flavour pool), `timeline.py` (the bunching). The colony had nine to
eleven adults over the window. Caveats: the mod was updated many times during the window (0.69 to 0.101 within the last
twelve days alone), so some sources arrived mid-window; camps never came (every try "noroom"); the group habit count is for
the one group the colony had.

**Staged gatherings, days 68 to 128 (60 days) and 98 to 128 (last 30):**

| Source | 60 days | Last 30 | When |
|---|---|---|---|
| Life goals' toasts | 5 | 4 | 79, 105 (two: a couple married the day before, both goals reached that morning), 116, 126 |
| Seasonal customs | 5 (+2 outings) | 2 (+2 outings) | midsummer fire 81, the swim 88, harvest dance fire 95, the high watch 103, the longest night's supper 113; outings: planting 115, shearing 120 (never came) |
| Groups calling their people | 4 | 1 | 68, 84, 94, 116 (one group: "Stace's friends", seven people, evening at the fire, about every 12 to 16 days); its table habit kept 51 times as it came |
| Calendar days kept | 4 (+1 gift day) | 3 | harvest feast 77, the first trade's gifts 87 (pairs), the wedding day's feast 101, a pet's remembrance at the grave 103, a faith's evening 106; the founding day is kept by its own event (below) |
| Elections | 4 | 4 | an election 103, the year's hustings 122 (assumed) and count 123, a ceremony for an empty seat 127 |
| Decrees that gather | 3 | 1 | one faith 77, militia drills 86 and 119 (the marshal's address) |
| Events that gather | 3 | 2 | a wake with words 87, a fire for kin of the blood 118, the founding day's feast 121 |
| Weddings | 2 | 1 | 95, 104 |
| Council meeting | 1 | 0 | 81 (an ambition of 75 days has held the next one back since) |
| **All gatherings** | **31 (3.6 a week)** | **18 (4.2 a week)** | |
| Outings | 5 | 4 | the pyre 77, the crematorium 107, planting 115, a vigil at the pens 117, shearing 120 |
| Small (visits, food carried out, a lesson) | 3 or more | 2 | a guest's meal 72, pilgrims fed 104, an apprentice 126; herd and last-season visits |

- **Bunching** (days counted from the game's start, as the records keep them): two or more gatherings on days 77 (a harvest
  feast and a decree's words), 81 (the midsummer fire and the council meeting), 95 (a wedding and the harvest dance's fire),
  103 (an election, a remembrance at a grave and the high watch), 105 (two toasts) and 116 (a toast and the group's fire); 12 of 30 gaps under a day, 21 under two days; the median gap 1.3 days; any seven days held 4 in the median
  week and 8 at the most.
- **Rimfolk's own record of its plans** (`PlayOutTracker`: 62 made in the whole game, the last 30 kept, which matches the
  30 or so plans the records show in the window): **21 happened, 9 fell through**: "nobody was free", "there was nowhere to hold it" twice, "something else had
  everyone's attention" twice (a gathering already on), "still laying the fire", "was never free", "there was nothing that
  needed doing" twice (work parties). Each still sent its line and gave its moods.
- **Council questions** (the week's memory of answers): 14 in 60 days, 7 of them seasonal customs, every one of those
  staging something (the shearing fell through); the others were the enemy dead twice, the spoils, three strangers' and guests' complaints, a secret.
- **The calendar** (13 days written, 6 kept in the 60 days): next year it holds feasts for founding, harvest, two weddings and two
  victories over the same hive, four remembrances, the first trade's gifts, a faith's day and a wedding day kept alone; five of
  them fall on days 43 to 47.
- **Traditions**: the harvest dance (95) and the midwinter supper (113) became council traditions this year; the colony
  now risks a broken tradition each year it doesn't keep them.
- **Journals and chronicle** (60 days): 600 journal lines, 257 chronicle entries, 330 history events. Matched to their
  pools, journals mostly hold elections (22), life goals changed (17), illness, comrades, dreams and grief; play-outs
  write little (two "carried", the gift day, the faith's day, harvest), so journals undercount scenes.
- **The game's archive** (its last 14 days only, 201 items): Rimfolk's play-out messages aren't archived; the game's and
  other mods' own gatherings in those 14 days were a binge party, a beer party, a eulogy and a funeral's call, about one and
  a half a week on top of Rimfolk's.

## 4. Overlaps

**Several systems light a fire and gather people round it.** A group's evening at the fire (a fire laid, music); a
calendar day kept as a bonfire (the walls); customs: midsummer night, the harvest dance, the star night, lantern night;
events: kin of the blood's fire, a wake; ambitions: raise the walls, the hunters' year; small habits: those who fought sit
round the fire after a hard fight, the nostalgic tell old stories by the evening fire; visitors' camps (sit with them at
their fire) and camps' shows and songs at theirs; an evening of faith with no altar meets round a fire. 21 answers stage a
bonfire. In the player's game: fires on days 68, 81, 84, 94, 95, 116 and 118, seven in 60 days, four of them the same
group's.

**Several kinds of toast and raised cup.** A life goal's toast; a child's growth birthday; a wedding's toast; the
election count's toast to the new council; the calendar's remembrance at a grave (a toast, solemn) and a faith's day
without its evening (a word at supper); answers: 7 toasts and 58 "words" (an hour round the table: an apology, a
reading, a story told, a wake); the founding day's cup; an ambition's first barrels; drinks poured from the stores at each
(`ToastDrinks`). The player saw five life-goal toasts in 60 days, two the same evening.

**Several feasts and suppers.** Six of the thirteen calendar days; customs (the longest night's supper, harvest home, the
ancestors' supper, first-frost soup); the founding day's event; the victory feast question; ambitions (the great feast, a
table for everyone, the great hall, fine dining); the feast-day decree; a wedding's feast; family suppers; feasts with
guests; the game's own Ideology feasts. 42 answers stage a feast.

**The calendar and the customs are two festival calendars.** The calendar keeps the colony's own days (a choice letter,
a tradition after three years); the customs are the season's festivals asked as council questions (a tradition after two).
Both come round once a year, both mostly gather everyone, both build traditions that are costly to break, and nothing
keeps one from landing beside the other (day 103: a remembrance and the high watch).

**The council's own business comes in clusters.** The election is three evenings in five days (hustings, count, a
ceremony), and the year's first days bring the election, the founding day, the annals and the second year's letter
together (days 120 to 128 in the player's game).

**Same day, nothing in between.** The only rule between systems is that a gathering can't start while another is on; a
second one waits behind it the same evening or falls through. A wedding is followed by two toasts for the couple's goals,
a group's fire by a calendar feast the next day. The heart's relief from gatherings is capped per day (`HeartLogic`,
`GatheringCap`), so the second gathering of a day eases nobody's strain.

**What adds little.**
- A life goal's toast: no choice, a small mood, and the deed is already told round the colony and in its letter.
- A group's evening: no choice; it does give belonging and eases strain, and the group's closeness, but the same words
  begin it after its first two times ("An evening at the fire for Stace's friends...").
- The second victory feast over the same enemy, and battles' remembrances without a grave.
- The hustings and the ceremony beside the count: three gatherings for one vote.
- A family supper: flavour by the player's rule, and it still takes a family from work at 17.
- Calendar feasts kept every year in the same words, with nothing that remembers last year beyond the tradition's count.

**What adds the most.** The council meeting (an ambition chosen, a lasting goal), the election (offices, rivalries), the
customs (a real choice, traditions), an evening of faith (faiths swayed), answers' gatherings with a risk (a fight at the
table), the first group evenings (a group's first gathering says what the group is), weddings.

## 5. Proposals

All of these keep the player's rules: answers still play out on the map and what the words say happens is what is seen
(a scene let go is the colony's own doing, never an answer the player picked); nothing is switched off for an age; harm
stays at its rate; family suppers stay flavour with no mechanics; nothing becomes a countdown in text. Numbers are the
model's (average play, ten colonists).

### 5a. One ledger for staged scenes (needs the player's word on the numbers)

A per-colony count of gatherings and outings, beside `Tell`'s counts (a small pure `StageMath` with a `StageBook` like
`TellBook`, nothing saved: a load gives a fresh week), read by everything that stages a scene:

- **Priority by value.** (1) What the player chose: an answer, a calendar day kept, a vote, a wedding, a decree, a
  meeting. Always happens, counts toward the week. (2) Once a year or once in a life: a tradition's day, the election's
  count, a wedding, a funeral. (3) The colony's own routine: a group's evening, a family supper, a toast, a child's birthday,
  a camp's show. Waits for room.
- **Room**: no more than two routine scenes in any seven days, and none within a day and a half of any other gathering;
  a routine scene waits up to three days for room, then is let go without a line (its group still keeps its habit as it
  comes, which moves nobody).
- **Quiet days**: after a colonist's death or a raid that cost the colony, two days with no festive routine scene (the
  grieving already sit out festive calendar days; this extends it to the colony's own doings).
- **A floor**: if ten days pass with no gathering (the quiet-days setback's threshold, `SetbackMath.QuietDays`), the next
  routine scene may come at once.
- Measured with the allowance alone (routine scenes only, customs not asked while the week is full): 3.3 a week from 4.7,
  because most scenes are of the first two kinds. The rest needs 5b and 5c.

### 5b. Merge (needs the player's word)

- **Calendar days close together are kept together**: days within three days of each other come round as one, with one
  letter naming both and one gathering ("The victory over Hive and Alo and Penguin's wedding day"); two victories over the
  same enemy are one day on the calendar (`ColonyCalendar.TryAdd` only limits a kind, three battles, three weddings, four
  deaths, and merges nothing).
- **The calendar and the customs as one festival year**: a season's custom isn't asked within two days of a calendar day
  or any gathering, and at most one custom a season (four a year, from about seven in the player's 60 days); a custom
  kept as a tradition can be written on the calendar as the colony's day, so its date is known and it no longer comes
  as a question each year.
- **Toasts fold into the next gathering**: a life goal's toast is raised at the next gathering within two days ("and a
  cup for Barr, who found something worth doing"), else at supper among those already at the table (no one called from
  work); the same for a child's growth birthday; two toasts the same evening are one.
- **The hustings join the count** in a colony of fewer than about twelve voters (the candidates speak, then the votes are
  read); the ceremony for an empty seat joins the count when it falls within three days.

Modelled with 5a and 5b together: **2.6 staged scenes a week (from 4.7)**, any seven days median 2 and 4 or more one week in
ten (from 5 and 7), the busiest 8 (from 14), days with two or more 2 a year (from 9), gaps under a day 22% (from 46%).
Calendar days keep 40% of theirs as a gathering (the rest at supper or together with another day), groups 35%, customs 39%,
family suppers 39%, toasts none on their own; meetings, elections' counts, answers, weddings, decrees and faith all stay.

### 5c. Thin and make rarer (needs the player's word)

- **Groups' evenings**: about every 16 days for a close group instead of 12 (`GroupMath.CallAfterDays` from 8 to 12, with
  `CallChance` 0.2 as it is), and only the first habit's call; the group's closeness to come more from keeping its habits as they come.
- **The calendar's default ways**: a battle won is kept with a toast at supper rather than a feast (a feast for the first
  one, its anniversaries quieter); remembrances of a battle with nothing to stand at kept with a word at supper; feasts
  for founding, harvest and weddings stay.
- **Council meetings** stay as they are (the ambition already spaces them).

### 5d. Make richer (the player's call which; each is its own small feature)

What each kept scene should give, so it earns its place:

- **A story that builds**: words that remember the last time ("Last midsummer the fire burned till dawn; this year it
  rained and they kept it in the hall"), who was there and who wasn't (the empty chair already exists for the calendar,
  `SharedTimes`), a group's evenings counted in its words ("the twelfth evening at the fire, and the first without Dukialgac").
- **A lasting mark**: a tradition already does this for days and customs; a group's evening could settle a rivalry
  between two of its people or bring a newcomer in (`GroupNewcomers`), so it moves the social web, not only a mood.
- **A small choice**: a calendar day could offer the way to keep it this year (the feast, games, a fire, a quiet supper)
  in its existing letter, so a cluster of days can be thinned by the player; a custom kept twice could ask once whether to
  make it the colony's day.
- **Something seen**: the scene's one moment a viewer would remember (cups raised, a song, the fire's sparks) already
  exists; what is said at it could come more from who came (their quirks, their ties, the last time) and less from the
  general pool.

### 5e. Smaller fixes found along the way (bugs before features)

- **Second-gathering collisions fall through** with "something else had everyone's attention" (two of the player's 30): with
  the ledger, a routine scene should not be planned for an evening that already has one; an answer's scene could take the
  next evening instead of the line.
- **Fires that aren't laid in time** ("still laying the fire"): a bonfire planned for tonight with no fire and nobody free
  to lay it could start at the colony's own campfire, or the hearth indoors.
- **The model's custom rate** (Tools only): `Sim.FireDilemma`'s flat cast chance makes customs about a third as common as
  the game; giving the non-seasonal pool the real share of failed casts would make the scenes count honest. Not changed here.

### What the player decides

1. Whether an allowance for the colony's own scenes is wanted, and its numbers (two in seven days and a day and a half
   apart are this audit's suggestion).
2. Whether calendar days close together are kept together, and whether customs become one a season and join the calendar.
3. Whether toasts and the hustings fold into other gatherings.
4. Which of the richer ideas (5d) to build first.
