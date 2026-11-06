# Rimfolk systems review (0.104.0): value, variety, writing, integration

A review, not a change: no mod code was touched. Evidence comes from the defs and keyed text at 0.104.0, the repo's
tools (variety report, interaction audit, play-out audit, label check), and the player's save
(`save3/Atia.rws`, day 128, colony of 12, industrial, desert), parsed read-only with scripts kept in
`scratchpad/sysaudit/`. How often things are staged and how many messages come is another agent's audit and is left
alone here; so are the harm service, vendettas, hostages and triage that others are changing.

Marks used below: **[fix]** a plain fix (words untrue to the map, a broken line, a repeat, a seam that tells one
thing twice), safe to do without asking. **[word]** needs the player's word (cutting, merging, changing what they see
or how a system behaves).

---

## Summary

### First, bugs (bug fixes come before features)

- **Every colonist's character is lost at each load (found, with the cause).** `PersonaTracker.ExposeData`
  (`Source/ColonyCouncil/Persona/PersonaTracker.cs:112`) saves `people` through a local list:
  `var list = people.Values.ToList(); Scribe_Collections.Look(ref list, "people", LookMode.Deep);` and fills `people`
  from it only in PostLoadInit. The list read in LoadingVars is the local of that pass and is thrown away; in
  PostLoadInit the local is made again from the still-empty `people`. So after every load each colonist's marks, their
  lasting and drifting nature and their stances on topics start from nothing (the shared memories, a plain field,
  survive). The save shows exactly that: the seven elected on day 123.7 carry no "elected" mark, the three elected on
  day 127.6 (after the player's last load) do, and only those three have any nature; the conversion marks told on days
  78 to 104 (a major mark is meant to last 720 days) are gone. What rides on it: the Character page ("what shaped
  them"), views that last in conversation, the voice a marked character speaks in (`Persona.ToneOf`), trait changes
  that need marks, and how far a deed goes against someone's nature. In the code since 27 October (dd498d6).
  Fix: fill `people` from the list in LoadingVars (or keep the list as a field and rebuild in PostLoadInit), and a
  save-and-load test for the tracker. **[fix]**, the most valuable fix in this review.
- **Marks also stop being noted?** The marks tracker's last fight is day 114 though the colony fought on day 124;
  worth a look once the load is fixed (`Persona/Marks.cs`). **[fix]** if it is a bug.
- **The election results leave seats out.** The 5502 letter names 7 of the 11 seats; Barr, "standing unopposed for
  quartermaster" on election day, isn't in the results, nor are steward and keeper; four days later a by-election made
  Stakius quartermaster a day and a half after joining. **[fix]**
- **Camps never find room.** The camp tracker tried 23 times since day 108 and found "no room" every time: a spot is
  ruled out if any cell of its circle is in the home area or a zone (`Camps/CampSite.cs` TryFind), and a grown colony's
  home area covers the ground a camp may use. Only the core should need to be clear, and the region page should say
  when there's no room. **[fix]**

### The ten most valuable improvements, ranked

1. **People stop repeating themselves.** In the save, 24 of the 630 journal entries are a colonist writing a line
   they already wrote, half of them within ten days ("It's going to be fine! Grumpy, but fine." twice in 2.4 days;
   "Dreamt of the best party there ever was..." twice in 3.8 days). A cheerful colonist has exactly one line for
   catching a cold, so every cold reads the same. Give each person a memory of their own last lines (rebuilt from
   their journal when a game loads, so nothing new is saved), and let a speaker's tone win only when it has three or
   more lines to choose from. **[fix]**
2. **Every "it happened" sentence is true.** The election results say the new warden "went straight out to the gate",
   the marshal "walked the wall and moved two of the sandbags", the reeve "walked the fields the same afternoon"
   (the count is in the evening): none of it happens on the map. The herd's letters call a chick "the first calf of
   spring" and give an old chicken a "gray muzzle". Rewrite these as sayings and intentions, or stage the one walk;
   take the young's and the body's words from the animal's kind. **[fix]**
3. **One name for the colony.** Four systems now say what the colony is known as: our ways ("Known as Generous"),
   the colony's traits ("Known as Walled", "Known as Learned"), colony renown among the peoples, and what each people
   remembers. Two of them sit on the Ways page, one under People, one in the region window, and the chronicle gets
   bare labels ("Known as Artful") as lines. Bring them into one "The colony's name" card and one chronicle sentence
   each time the name moves. **[word]**
4. **A colonist's name sticks.** "The Healer" went Harper, Alo, Harper, Alo in 70 days; Harper held six names in 80
   days. A name earned should stay while the holder still does the work, and the second person earns a name of their
   own; and the colonist's epithet and their renown "known as the one who..." are two "known as" lines for one person
   that should read as one. **[word]**
5. **Life goals that last and come from the person.** After each raid colonists swap their goal for a fighting one:
   "bring down ten mechanoids" was chosen five times, "someone nobody wants to face up close" six; Stace changed goals
   four times in 25 days. Make a change of heart rare (once per goal, only for a brush with death or a loss), spread
   the battle goals, and say the spark in a sentence, not "(after "ate fine meal")". **[word]** for the rule,
   **[fix]** for the words.
6. **Elections with promises that differ and count.** Three physician candidates made the same promise word for word
   (two for reeve, two for chaplain); ten of the twelve seats have only one plank that always fits. Give each seat
   several, have candidates run on their own record (the wounds they dressed, the name the colony gave them, the fight
   they held), and let the next election remember whether the holder kept it, read from the map. **[fix]** for
   distinct planks, **[word]** for running on a record and promises judged.
7. **The chronicle reads as a history, and the year ends once.** Conversions are chronicled twice a day apart (14
   lines for 7 people); "Fighting side by side has brought [all nine] closer together" after nearly every raid (6
   times in 26 days);
   lines start in lower case ("open gates came to an end."); "(s)" plurals ("marked 1 year(s)", "attacked 10 time(s)");
   and the year ends with three retrospectives within two days (the annals, "the year on this land", the founding day
   letter). One line per happening, sentences not labels, and one year letter in two parts. **[fix]** for the lines,
   **[word]** for one year letter.
8. **Shared wants become one petition.** "Proper armor" was wished six times by four fighters and given up five times;
   three asked within a day of each other. When several people want the same kind of thing, one petition ("the
   fighters want armor"), and a want given up comes back as "still waiting, the second time" rather than as new.
   **[word]**
9. **Letters to the region's people that arrive and bring something.** Stace wrote three letters to notables in 18
   days; each leg takes 12 to 14 days, and the one answer was "Your letter came. We are well enough, thank you." (it
   "changed little"). Legs by distance (a near town in days, not a quarter of a year), and an answer that carries a
   thing: news of the town's fortune, an invitation, a request, a name. **[word]**
10. **Decrees and groups that mean distinct things.** "Drills" and "militia drills" were declared on the same day and
   both ran (the annals list "militia drills (2 times) ... and drills"); several more pairs overlap (rationing,
   austerity, famine bread; festival days, a feast day, shared meals; quiet hours, days of rest). And the colony's
   one group, "Stace's friends", holds 7 of 12 while Barr, the one everyone fights with, belongs nowhere visible.
   Fold the overlapping decrees into one with an age's form, and let groups form around the colony's real divides.
   **[word]**

Close behind: dreams and personal stories with a cause first (the generic "best party there ever was" dream five
times; "the long discipline" story for three people in 110 days with the same lines) **[fix]**; and the colony's
one vendetta instead of nine for one death (passed to the vendetta work).

---

## Verdicts, system by system

| System | Verdict | Reason |
|---|---|---|
| The council (trust, standing) | keep | The spine: trust moves with every answer and feeds elections, wishes, petitions. |
| Offices | improve | Seats do things on the map (rounds, steps into spats), but the "elected" lines claim acts that never happen, and most office bonuses are invisible percentages. |
| Elections | improve | Real votes and canvassing; planks repeat word for word, promises are never held to, the results letter drops seats. |
| Decrees and petitions | improve, merge pairs | 31 decrees, several pairs overlapping (drills and militia drills both active at once). |
| Wishes and promises | improve | The best-written part; the same want comes again and again as separate wishes. |
| Meetings and projects (ambitions) | keep | Four meetings in 128 days, three ambitions achieved: rare, weighty, readable. |
| Dilemmas and answers | keep | 214 questions, answers play out (play-out audit: 0 candidates left). The strongest system. |
| Rimfolk's events | keep | 238 events, concrete prose, consequences both ways. |
| World events | keep | Two in 128 days, each a real arc (a town abandoned, a silver rush); the year letter garbles the ending ("ended in over"). |
| Camps | improve (check) | Rich (31 kinds), but none since day 108: "no room" on every try. |
| Visitors and callers | keep, check | Nothing of theirs in the save in 128 days (callers, visitors' services, trips empty); check they can fire for a colony like this. |
| Missions | keep, check | None in the save; quests from Rimfolk are a good bridge to the game's Quests tab. |
| Couriers | keep | Plumbing that makes words true (silver carried to the gate). Invisible when it works, as it should be. |
| Outsiders and their talk | keep, thin settings | 1,626 talk pieces, well tagged by age; six settings for one system. |
| Notables and letters | improve | Letters take 12 to 14 days a leg (three letters in 18 days, one answer, "answered coolly"); little comes of an answer. |
| The region (towns, posts, raiders' camps, hamlets) | keep | The brigands' hold in the save was the best story of those weeks: seen, priced, raided, fought with an ally. |
| Debts of honour | keep | Small, concrete, remembered. |
| Truces and pacts | keep | Clear terms, broken oaths weigh on people (against their nature). |
| Ransom and envoys | keep | Answers with something on the map (a rider, an envoy at the table). |
| Feasts and contests with guests | keep | Feeds renown and a colonist's own name; good integration. |
| Marriage alliances | keep | Just reshaped to the colony; check the age forms ("a partnership contract between settlements" rings cold for a wedding). |
| Wards and hostages | keep | Reshaped; hostages are being reworked elsewhere. |
| Fealty and vassals | thin | A large machine (tribute, calls to arms, protectorates) for a rare situation; fold its pages and settings into pacts. |
| Colony renown | merge | With our ways, the colony's traits and what the world remembers: one name for the colony. |
| Against their nature | keep | Strain through the heart only, no new mood: a light touch that gives diplomacy a cost in people. |
| Banishment | keep | The player's request, built carefully (a word first, then the question, then the walk out). |
| Journeys | keep | The planner (who to bring, what to pack) is practical; nothing in the save yet. |
| Ailments | keep | Small illnesses with names; one cheerful line per kind is the repeat ("Laid low by a bug" by four people). |
| The heart and feelings | keep | Strain and feelings drive breaks, dreams, goals; invisible unless read, which suits it. |
| Setbacks and nuisances | keep | Everyday friction with a reason; keep the gentle setting. |
| Bonds and rivalries | improve | Good bonds with a history; the comrades message lists the whole colony after every raid. |
| Groups | improve or thin | One group of 7 in a colony of 12, named after a person; its history repeats "gathered for an evening at the fire for Stace's friends." |
| Reputation and gossip | keep | Deeds with heard-by chains are a lovely idea; "the vote on the ..." is a quarter of all deeds. |
| Vendettas | improve (elsewhere) | One death made nine vendettas, one per colonist; reads better as the colony's one grudge carried by the closest. Being reworked by another agent. |
| Secrets | keep | Two in 128 days, one told and judged: rare and weighty. |
| Personal stories | keep | Good arcs; "the long discipline" ran three times in 110 days with the same lines. |
| Quirks and habits | keep | Each shows on the map; good integration with dreams and talk. |
| Dreams | improve | 231 dreams, but the same handful of generic sweet dreams come back ("the best party there ever was" five times). |
| Life goals | improve | Battle goals swamp the rest; changes of heart too easy. |
| Names (epithets) | improve, merge | Names flip between two people; a second "known as" from renown. |
| Character (persona nature, marks, views) | fix first | Lost at every load (`PersonaTracker.ExposeData`); the system has never had the chance to build in a played game. |
| Faith | keep, fix seam | Conversions told twice. Otherwise thoughtful (certainty, evenings of faith). |
| The calendar and traditions | keep, merge rules | Two ways a custom becomes a tradition (a seasonal answer twice running, a calendar day three years running); one rule would read clearer. |
| The record (journals, chronicle, annals, news) | improve | Where the player reads the story; most repetition lives here. |
| The defense planner | keep | Tool, not story; looked after lately (two rounds of "turrets where they stand"). |
| The advisor and planning | keep | Already unified ("the same rules for all the council's advice"). |
| Fields by the weather | keep | Small, true to the map (hail seen coming, frost harvest). |
| Animals (herd, beasts, pets) | improve words | Lovely herd story; mammal words for every kind. |
| Drink and smoke before work, toasts | keep | Tiny, mod-bound, real items from the stores. |
| The interface windows | keep, merge one page | Four windows, already consolidated; renown sits under People while the peoples live in the region window. |
| Settings (266) | thin | Region, diplomacy and outsiders alone have 26 switches; group them under three, details folded. |

---

## Findings, system by system

### The record: journals, chronicle, annals, colony news

Where the player reads the colony's story, and where nearly all the save's repetition is.

- **Same person, same line.** 24 of 630 journal entries repeat the writer's own earlier line (list in
  `sysaudit/jgap.py` output): MJ "It's going to be fine! Grumpy, but fine." at 2.4 and 10.4 days apart; Dukialgac
  "With Alo on it, I'll be up in no time!" three times; Hector "Vera took a blow meant for one of us..." twice in six
  days; Stace "I'm the speaker! Practiced my serious face in the washbasin tonight." both years. 13 lines were written
  by three or more people ("Laid low by a bug. Back on my feet in a day or two." by four).
  - Causes: `LineMemory` is memory only ("after a load every line is fresh again", `Source/ColonyCouncil/LineMemory.cs`);
    the window is a third of a pool, shared by all speakers; and `Tone.PickOnce` (`Source/ColonyCouncil/Tone.cs:100`)
    picks the speaker's own tone 80% of the time even when that tone has one line (e.g.
    `Council_Journal_Ailments` cheerful: one line; `Council_Pool_SayRecordDry` 5/1/1/1/1/1). Journal lines taken from a
    thought's description have one wording each ("news from the road", `Council_Thoughts_Encounters_People.xml:15`,
    five times in the journals).
  - Proposal **[fix]**: a per-speaker recent set (their last 20 journal lines, rebuilt from the saved journal on load, no
    new save data); a tone list under three lines draws together with plain; a thought-described journal line gets two
    more wordings or is written only when the person hasn't written it this season.
  - The variety report can't see this: its numbers (every system under 2 sightings a line a year) are right for pools
    but miss single keyed strings and per-speaker repeats. Add a "per speaker" column (eff for one fixed tone and
    tags) and count keyed strings that go to the chronicle or news. **[fix]** (tool only)
- **Told twice.** A conversion is chronicled by the faith (`CC_Faith_Converted`, `Rimfolk_Faith.xml:10`) and a day
  later by the persona mark (`Rimfolk_Mark_Converted`, `Defs/PersonaDefs/Rimfolk_PersonaMarks_Life.xml:362`,
  `<chronicle>true</chronicle>`): 14 lines for 7 people in the save. The mark should keep its journal line and drop the
  chronicle and news when the faith already told it. **[fix]**
- **Labels as lines.** `ColonyTraits.cs:254` and `Character.cs:215` put the letter's label into the chronicle: "Known
  as Walled", "Known as Learned", "Known as Artful", "Known as Generous" stand as whole lines. Use the sentence
  ("The colony has become known as walled."). **[fix]**
- **Lower-case starts.** "the harvest dance became a tradition.", "open gates came to an end.", "a market day came to an
  end.", "the high watch: The council chose ..." (decree and tradition labels are lower case and lead the line); world
  events' lines start with a capital ("A silver rush: The colony chose ..."). Capitalise the first letter where a
  label leads. **[fix]**
- **"(s)" in story text.** About 180 "(s)" in keyed text; in the planning pages they're tolerable, in story text they
  aren't: `CC_Chronicle_Anniversary` "marked 1 year(s) in the colony" (11 lines in the save), `CC_Annals_Raids`
  "attacked 10 time(s)", the annals' "8 wish(es)", "15 hard question(s)", `CC_Year_Chronicle` "29 chronicle line(s)".
  Plural forms (the game's own `Find.ActiveLanguageWorker.Pluralize` or two keys). **[fix]**
- **Machine words in letters.** The year letter: "Of the year's 29 chronicle line(s) about strangers and the world:"
  (`Rimfolk_YearLetter.xml:29`), "Out in the world: A silver rush ended in over." (the ending's key "over" dropped into
  a sentence), "The herd numbers 4, 0 of them born this year." Life goals: "(after "ate fine meal")", "(after "left
  out")", "(after "had my say at the council")" (`CC_Asp_Spark_thoughtNamed` quotes the game's thought label). Write
  each as words: "Some of what the year's chronicle says about the world:", "The silver rush is over.", "none born
  this year", "after a fine meal". **[fix]**
- **The year ends three times.** Day 119.8 the annals, day 120.0 "The second year on this land", day 121.4 the founding
  day letter ("The colony is 2 years old today... Here since the first days..."). Each is good; together they are
  one occasion told thrice. One year letter in two parts (the people, the land and the world), on founding day,
  with the founding day's question at its end. **[word]**
- **Templates by office.** "I'm the speaker! / I'm the chaplain! Practiced my serious face in the washbasin tonight.",
  "They made me X. I didn't even put my name forward." (Harper, Hector), "I've already talked three people round to
  X!" (Stace, three decrees), "X and Y married today. I hadn't seen X smile like that before." (Harper, both
  weddings). These are good once; a line per office (the physician's infirmary, the reeve's fields) and per decree kind
  would make the second time different. **[fix]**
- **Numbers in a journal.** "Home after 4.1 days. Nothing burned down..." and "4.1 days of bad roads": round to words
  ("four days"). **[fix]**
- **Comrades roster.** "Fighting side by side has brought Harper, Hector, Stace, Macy, Alo, Penguin, Dukialgac, MJ and
  Toni closer together." after nearly every raid (6 times in 26 days; `SocialWeb.cs:706`, `CC_Tie_Battle`). Chronicle only the new
  comradeships and the rescues ("Penguin dragged Hector out of the fight"), and leave the roster to the battle's own
  line. **[fix]**
- Good as it is: the colony news letter's sections (People, The colony, The land, Health), the annals' content (who
  joined, died, bonds made, names, "Nobody trusts the council more than Harper"), the battle lines with the best
  fighter named.

### Names (epithets) and a colonist's renown

- `Epithets.cs` gives each name to one holder and moves it when someone passes the holder by 15% on a running count
  (`margin = 1.15f`, line 18). Two doctors leapfrog: "the Healer" Harper (day 46), Alo (55), Harper (91), Alo (113).
  Harper also went the Vault, the Mender, the Bottomless, the Old Hand. A name means less each time it moves.
- Two "known as" for one person: the epithet ("Alo the Healer") and renown's own name ("Known as the one who won the
  wrestling at the feast with the Lenlia", `Renown.Own`). On the card they're two lines.
- Proposals **[word]**: a name stays with its holder while they still do the work (their count rises in the last
  season), so the other earns a different one (the Mender, Goodhands); a name lost says so once in the holder's own
  journal; the epithet and the renown name are one "Known as" line on the card ("Alo the Healer, who won the
  wrestling at the Lenlia's feast").
- Variety: 60 epithets; the earned line is one per name (`<earned>` in `Council_Epithets.xml`). Three wordings each
  would cover a name that changes hands. **[fix]**

### Life goals (aspirations)

- Impact: good when they finish (Penguin cooked 500 meals; Barr found something worth doing, which gave a work frenzy).
  But battles spark goals for everyone who fought: of 38 goals chosen in the chronicle's 88 days, 21 say "(after a
  battle)" and 4 more "(after a brush with death)". "Bring down ten mechanoids" five times, "someone nobody wants to
  face up close" six. `TryChangeOfHeart` (`Aspirations.cs` ~690): after 10 days and under 35% done, 25% a sparked
  option, and a raid every ten days is a spark each time; Stace went from "a bedroom fit for a noble" to "feel safe
  again" to "ten mechanoids" to "a friend for life" in 25 days, each change a journal line ("I've got a new dream!").
- Proposals **[word]**: one change of heart per goal, only for a brush with death or a loss; a battle sparks one
  goal per fight at most, for the one who did most; a goal already held by someone else at home weighs half (no five
  mechanoid hunters). **[fix]**: the spark said in words (`CC_Asp_Spark_thoughtNamed` quotes a thought label).
- Integration: a goal reached should touch the colonist's name and renown (it touches the chronicle and deeds now:
  `Rimfolk_Deed_GoalReached` 4 in the save). Good.

### Offices and elections

- **Elected lines claim acts** (`Defs/OfficeDefs/*.xml`, `<elected>`): "walked the wall before dark and moved two of the
  sandbags", "counted the stores that same evening, twice", "walked the fields the same afternoon" (before the count,
  which is in the evening), "went straight to the pens to tell the animals", "went straight out to the gate to see
  who was there", "spent the evening at the altar", "took the seat by the fire". None is staged. Rewrite as sayings
  and intentions ("Penguin says the first thing she'll do is walk out to the gate"), or stage the four walks that are
  natural (gate, pens, wall, fields) as a short errand the next morning. **[fix]**
- **Planks**: 30 planks for 12 seats, but ten seats have only one that always fits, so in a quiet season every
  candidate runs on it (`ElectionPlanks.Deal` deals distinct planks only while there are enough). The save's letter:
  three physicians "promise that nobody who is hurt will wait for a doctor". Give each seat three or four always-fit
  planks, and let candidates run on their own record: the wounds they dressed (`TimesTendedOther`), the name the
  colony gave them, a deed the colony heard of (the reputation tracker has 91), a fight they held. **[fix]** for
  more planks, **[word]** for running on their record.
- **Promises nobody checks**: a plank is words only after the vote (`Election.cs` saves its key for the canvass
  lines and nothing else). The next election could say "Toni promised a proper watch at night, and there was one" from
  what the map saw (night watch hours, medicine counts, fields sown). **[word]**
- **Results letter**: in 5502 it names 7 seats; Barr, "standing unopposed for quartermaster" on election day, is not in
  the results; steward and keeper aren't mentioned; four days later a by-election made Stakius, who had joined 1.5 days
  before, quartermaster, and Shigeko herdmaster on arrival. Check that every seat is accounted for in the letter, and
  consider a few days at home before someone can stand (a newcomer elected the day after arriving reads oddly).
  **[fix]** for the letter, **[word]** for the wait.
- Office bonuses ("everyone heals a little faster", "standards rise a quarter more slowly") are invisible; the
  Interplay links (rounds, stepping into spats, the herdmaster sitting with the bereaved) are what the player sees.
  More of the latter, fewer percentages, over time. **[word]**

### Decrees and petitions

- Overlaps: `Council_Decree_Drills` and `Council_Decree_Militia` both "practice shooting and melee every waking hour",
  declared the same day in the save and both active (the annals: "militia drills (2 times) ... and drills"). Also
  rationing, austerity and famine bread; festival days, a feast day and shared meals; quiet hours and days of rest;
  sick bay and quarantine; on guard and curfew. Proposal **[word]**: one decree per idea, with its age's form
  (militia's muster for low tech, drills for the rest), and pairs that exclude each other while one is active.
  At least **[fix]**: drills and militia drills can't run together.
- Drills add skill every hour unseen ("practices shooting and melee every waking hour"); only militia's first muster is
  staged. An evening drill at the gathering spot each day the decree runs would make the words true (DESIGN.md "What
  stays a mood" accepts days-long habits, so this is **[word]**).
- Petitions: co-signing works ("Stace backed Harper's wish for A place to pray", "MJ objected to Stakius's wish").

### Wishes and promises

- Writing: the best in the mod. Concrete, voiced, specific ("Somebody propped up a table with a book. I know who.").
- Repeats in kind: "Proper armor" wished by Stace, Dukialgac, Harper (within a day), later Shigeko; given up by
  Stace, Dukialgac, Harper, and earlier others (WorldLedger: 6 wished, 5 given up). Roomier bedroom given up by MJ,
  Penguin and Barr. Each is a separate letter and a separate disappointment. Proposal **[word]**: when two or more want
  the same kind of thing, one petition for all of them (one letter, one deadline, one reward); a want given up and
  asked again reads as the second time (Harper's own line exists: "Asked a second time and got ignored a second time.
  I'm keeping count in here."), and the Wishes page shows "asked 3 times this year".
- Integration: granted wishes taken back feed elections (good); the chronicle keeps every wish made (29 "wished for"
  lines in 88 days). The chronicle could keep only granted, given up and the petition's, and let the journal keep the
  asking. **[word]**

### Dilemmas and answers, Rimfolk's events, world events

- Strong. Answers play out (`Tools/playout_audit.py`: 0 candidates, 341 looked at and left), letters are concrete
  ("The snow by the pens is trampled in a ring, round and round"), and each event has age forms.
- Seams: the chronicle uses "The council chose" for dilemmas and "The colony chose" for events; one wording.
  **[fix]**
- World events: two in 128 days, both told well. The year letter garbles one ending ("A silver rush ended in
  over.", see the record). **[fix]**
- The brigands' hold (raiders' camp) in the save is exemplary: seen by Macy from the fields, a letter with the terms
  and how it ends, a raid from the east, an ally's fighters coming over the hill. This is the model for others.

### Camps

- The camp tracker's diary: 23 tries since day 108, all "noroom" (`CampTracker.cs:439`, `CampSite.AnyRoom`). `TryFind`
  (`Camps/CampSite.cs:115`) rules out a spot if any cell of its circle is in the home area or a zone, and requires it
  within `MaxFromColony` of the colony. In a grown colony the home area covers the reachable ground. Proposal
  **[fix]**: let the circle's rim touch the home area (only the core must be outside it), or allow a little farther
  out; and say on the region page when no camp has room ("No camp can find room near the colony: the home area and
  fields reach to ..."), so a quiet system isn't a silent one.
- Variety and writing: 31 kinds, good talk; nothing to fault in what was sampled.

### Visitors, callers, missions, journeys, couriers

- None in the save in 128 days: `Callers` (tasks, commissions), `VisitorServices`, `VisitorTracker` trips,
  `StoriesComponent` missions and `JourneyBook` plans are all empty. Some are recent; callers and missions are older.
  Worth a check (the self-check report, "Rimfolk: check myself") that each can fire on this map, and a line on the
  Overview when one was ruled out ("no callers: no trade spot"), so the player knows the system exists. **[fix]**
  (check)
- Couriers did their work: a letter's answer came by a messenger ("A letter from Blash"). Keep.

### Notables and letters

- Three letters in 18 days, one answer, each leg 12 to 14 days (`NotableBook` letters: created 109.8, answering 124;
  created 115.9, due 129; created 123.8, due 137.6). The one answer: "Your letter came. We are well enough, thank you."
  and "It changed little between the colony and Blash." That is a long wait for little. Proposal **[word]**: legs by
  distance on the world map (a near town in 3 to 5 days), and an answer that carries something: a name, news of the
  town's fortune, an invitation, a request (the region's fortunes and notables' roles are there to draw on).
- Notables' notes are thin ("Spent time getting to know Stace, pleasantly." twice of four). Two or three wordings
  per note kind. **[fix]**

### The region and diplomacy

- Diplomacy book empty in the save; all of it is newer than most of the player's days. From the code and words:
  - **Colony renown**: merge with our ways and the colony's traits (see the colony's name below). **[word]**
  - **Fealty and vassals**: a large machine (tribute, calls to arms, protectorates, an oath walked out) for a rare
    situation; its settings (`diplomacyFealty`) and page sections could fold into pacts as "a pact of fealty". **[word]**
  - **Marriage alliances**: the colony-age forms are good for low tech ("a match between clans") and stiff for the
    top ("a partnership contract between settlements"). A warmer spacer form ("a joining between two settlements").
    **[fix]**
  - **Settings**: `diplomacyFavours ... diplomacyNature` (11), region (9), outsiders (6): 26 switches. Three master
    switches with details folded under them, as the Events page does for the weather. **[word]**
- Region: towns with fortunes, posts, hamlets, raiders' camps; the raiders' camp was the best story of the save's last
  weeks. Keep.
- Vendettas (being reworked): Dukialgac's death made nine vendettas, one per colonist ("Over Dukialgac's body, MJ,
  Stace, Penguin, Harper, Alo, Toni, Hector and Macy swore that House Uthorin would pay"). The colony-is-the-family
  rule points to one vendetta of the colony, carried by those closest. Passed to that work.

### The colony's name: our ways, the colony's traits, renown, what the world remembers

- Four systems: `Character.cs` (mercy, generosity, tradition, openness, toil: "Known as Generous"), `ColonyTraits.cs`
  ("Walled", "Learned", "Artful", from deeds), colony renown (bands among the peoples, `Renown.Read`), and faction
  memory (what each people remembers, `FactionMemory`). The founding day letter says "Travelers on the roads call the
  colony traditional and generous", renown says the colony is "spoken of in the next town's market", and the Ways page
  lists two kinds of "Known as". The player can't tell which is which or why there are four.
- Proposal **[word]**: one "The colony's name" card (the Ways page renamed): what the colony is known for (ways and
  traits together, one list), how far the name has gone (renown's band), and what each people remembers (a line each,
  linking to the region). One chronicle sentence when the name changes. Renown's page under People moves there.

### Bonds, rivalries, groups, reputation and gossip

- Bonds: good histories ("Day 121: Hector covered a shift"). The comrades roster is the repeat (see the record).
- Groups: one group, "Stace's friends", 7 of 12; its history: "5 of them gathered for an evening at the fire for
  Stace's friends." (the group's name dropped into the sentence; `GroupTracker` history). The colony's real divide in
  this save is Barr (four rivalries, two fights, a banishment question coming) and the newcomers; no group shows it.
  Proposals: **[fix]** the history sentence ("Five of Stace's friends spent the evening at the fire."); **[word]**
  groups that form around a divide (the night shift, the faithful and the rest, the old hands and the newcomers), and
  one who stands outside every group named as such on the People table.
- Reputation: 91 deeds; 25 are "the vote on the ..." (elections), 26 "held the line". Votes as deeds crowd out the
  rest; count one per election. **[fix]**

### Personal stories, secrets, quirks, dreams

- Stories: "the long discipline" ran for Stace, Harper and Barr within 110 days with the same lines ("has stopped
  getting better at her art, and knows it"; "Early and late, X goes through the same forms of her art, over and
  over."). `ArcTracker.TryStart` lowers a story seen in the last 20 days; a season (or a year) would keep a story
  someone's own. **[fix]**
- "Dukialgac died before Barr could get past the wall in her art." reads as cause and effect; it means the story's
  other (who was watching her practice) died. "Barr's story ends unfinished: Dukialgac, who watched her practice, is
  gone." **[fix]**
- Secrets: two in 128 days, one told and judged. Keep.
- Quirks: each shows on the map, and they reach dreams and talk. Keep.
- Dreams: 231 dreams, yet the generic sweet ones come back: "Dreamt of the best party there ever was" 5 times in the
  journals, "Dreamt we all ate outside on a long summer night" 4. Weigh dreams with a cause (a raid, a loss, a skill,
  a person) above the ones with none, so the general sweet dreams are the filler they are. **[fix]**

### Faith

- Thoughtful (certainty, leanings, evenings of faith, the chaplain). The one seam is the conversion told twice (see the
  record). Seven conversions to the council's named ideoligion in 26 days, each with its own two lines, is a lot of
  one kind of news at once; the second through seventh could gather into one evening line ("Toni, Penguin and Alo
  have taken up Astro-Genetics too."). **[fix]**

### The calendar and traditions

- Good: days the colony lived through, kept or let pass, with their own letters. "The colony kept Texas's day" and
  "the victory over Hive" are the kind of memory the player wants.
- Two rules make a tradition: a seasonal answer chosen two years running (council traditions) or a calendar day kept
  three years running. One rule, said once in the Almanac. **[word]**

### Animals (the herd, beasts, pets)

- Words assume a large mammal: "The first calf of spring" for Chick 1 (`CC_Herd_SpringCalfTitle`,
  `Rimfolk_HerdStory.xml:12`, also the thought `Rimfolk_Thoughts_Animals.xml:250`), "gray at the muzzle, slow to rise"
  for an old chicken, "led them to water and back". Proposal **[fix]**: the young's word from the animal (the game's
  life stage label: chick, calf, foal, lamb), and three body sets (beasts, birds, others) for the old leader's line.
- The resident beast ("A name for the lion", then "Huck is dead, out on the land where it lived") is a good small arc.

### The heart, ailments, setbacks and nuisances

- Strain and feelings sit behind other things (breaks, dreams, goals), as they should. No change.
- Ailments: one cheerful line per kind makes the four "Laid low by a bug" in the save. More lines in the cheerful
  and blunt voices of `Council_Journal_Ailments` and `Council_Journal_Health` ([patient] "With X on it, I'll be up in no
  time!" three times by Dukialgac). **[fix]**
- Setbacks: everyday frictions with reasons; keep. The setback tracker's "built" list (furniture labels) is kept long;
  no player-facing issue.

### The defense planner, the advisor and planning, fields by the weather

- Tools rather than story; consolidated already ("the same rules for all the council's advice"). Keep.
- Fields by the weather (hail seen coming, frost harvest) is small and true to the map. Keep.

### Drink and smoke before work, toasts

- Mod-bound, from real stores, within drug policy. Keep.

### The interface windows

- Four windows (council: This week, People, Council, Colony; region; defense; planning), 25 pages, search and an
  inbox. Already consolidated twice. One seam: colony renown is under People (council window) while the peoples and
  their memory are in the region window; the colony's name belongs in one place (above).
- Where one thing shows in many places: a dream is in the journal, the Social tab, the colony news and the chronicle
  ("Dream" kind, three in the save). The chronicle could leave dreams to the journal. **[word]**

### Settings

- 266 settings, 197 switches, with search and presets. The weight is fine for a player who searches; the region,
  diplomacy and outsider switches (26) are the place to fold. **[word]**

---

## Save sample (Atia.rws, day 128): what repeats

Read from `history/archive` (the game keeps the last 201: days 114 to 128), the chronicle (300 lines, days 41 to
128), journals (60 a person), dreams (80), the persona memories (298), deeds (91), groups, faith notes, the world
ledger (395 events). Scripts: `scratchpad/sysaudit/` (`repeats.py`, `jgap.py`, `chron.py`).

Worst, in order of how often a player meets them:
1. **Journal lines repeated by the same writer**: 24 of 630, half within ten days (MJ, Dukialgac, Hector most).
2. **Life goals after battle**: "made it [his/her] life goal to bring down ten mechanoids (after a battle)" x5,
   "become someone nobody wants to face up close" x6, each a news line, a chronicle line and a journal line.
3. **Names changing hands**: "became known as the Healer" x4 (two people), "Greenhand" x3, "the Sower" x3.
4. **Conversions told twice**: 7 people, 14 lines ("has taken up X, leaving Y behind" then "has taken up the ways of X").
5. **"Fighting side by side has brought [everyone] closer together."** x6 in 26 days.
6. **Election lines**: "was elected X" one line per seat (21 lines in 25 days); three identical physician promises.
7. **Wishes for the same thing**: Proper armor x6 wished, x5 given up.
8. **Generic sweet dreams**: "the best party there ever was" x5 across four people.
9. **Chronicle anniversaries**: "marked 1 year(s) in the colony." x12.
10. **Group history**: "N of them gathered for an evening at the fire for Stace's friends." x5; "A shared table: N
    times now." x4.

Also seen: memory labels "the quarrel" for 24 of 298 shared memories (every mended spat the same label; a date or
the cause would tell them apart, **[fix]**); deeds "the vote on the ..." 25 of 91; the archive's messages are mostly
the game's own (alerts, bills, abilities), so Rimfolk isn't crowding them.

Systems with no trace in 128 days: diplomacy, journeys, callers, visitors' services, missions, camps since day 108;
persona nature and marks for 9 of 12 (the load bug above).
