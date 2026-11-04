# What Rimfolk already keeps about the world (inventory, 4 November 2026)

Read-only inventory for stage 0 (ROADMAP.md, "The world and its people"), so the world ledger takes these in rather
than adding a copy. Paths under `Source/ColonyCouncil/`. Factions are saved by reference (load ID); towns by
`Settlement.ID`. Every saved list's cap is modelled in `Tools/save_estimate.py`.

## 1. Stores about anything outside the colony
- **What each people remembers: `FactionMemory`** (World/FactionMemory.cs:126, a GameComponent). `deeds`
  (`FactionDeed` :25: faction, kind of about 60 (:179-257), side Favor/Wrong/TheirFavor/TheirWrong, weight, tick,
  count, name, settled, the colonist `by`/`byName`): merged within `mergeDays` (:293), 12 a people (`Trim` :314,
  World/MemoryMath.cs:19), fading by `MemoryMath.Strength`, dropped daily when faded (:342). `notes` (`KinNote` :81:
  who tended or freed whom, 30 days). `ReadSituations` (:354) turns ended world events into hardship deeds. Written by
  World/Patch_FactionMemory.cs (goodwill, exits, deaths, raids, quests, trade), missions, camps, mods' memory, outsider
  outcomes, scenarios (a "granted" debt like fealty, Scenarios/ScenarioParts.cs:189), vendettas, neighbour letters,
  `DilemmaEffect_Remember` (77 uses in XML), and `KinMemory.Note` (World/KinMemory.cs:103). Read by its `Tone` and
  `RaidNote`, Notables (`Baseline` :781), OutsiderFacts.Deeds (:774), Interplay_World (`AddMemoryTags` :55), KinMemory,
  Workers_Remembered (debts repaid or called in, peace), setbacks, stories and the World page panels.
- **Towns and named people: `NotableBook`** (World/Notables.cs:218). `Notable` (:90): own id, pawn kept in the world
  and name, faction, town (Settlement.ID) and name, role Head/Heir/Envoy/Trader/Acquaintance (NotableMath.cs:8), met
  and by whom, personal regard and `regards` per colonist (:29), `notes` (8), waits per letter purpose, topics done
  (12). 6 towns in reach x 4 roles plus 10 acquaintances (NotableMath.cs:110-128), looked over every 6 hours, old age
  daily. `losses` (12, 30 days), `seen` visitors (6, 3 days: freed prisoners, tended guests, envoys and camp leaders
  become notables). `letters` (`NeighbourLetter`, World/NeighbourLetters.cs:43; 3 open at most). Answering a letter
  writes regard, goodwill, memory, threats (`happenings.AddRaid`), a courier trip and a chronicle line (:800-903).
  The region map is not saved (rebuilt every 2 s).
- **Outsiders: `OutsiderBook`** (Outsiders/OutsiderBook.cs:249). `pairs` (colonist and outsider: warmth, familiarity,
  visits, friends, courting, talks, marks for callbacks `"key|tick|subject|fact"`, said, topics; 60, pruned 30 days
  after one side is gone; warmth carried into the notable's regard once). `visits` (patience, gifts, asks; 30).
  `heard` (Heard lately: town, people, text, teller and told, tick; 40, 60 days). `wars` (`WarSeen`: pairs of peoples
  at war from the game's relations, looked at daily; 30). `favours` (6, 30 days) and `words` (a good word promised to a
  notable; 8, 20 days).
- **World situations: `WorldSituations`** (World/WorldSituations.cs:17). `active` (2 at most; context with faction,
  enemy, stranger, other, place, name; flags, the colony's help and deeds, beats, sufferers, `held` pawns, `numbers`
  such as silver owed; the rival settlers are one). `history` (`SituationRecord`: 40). `lastEnded`.
- **Rimfolk's events: `HappeningTracker`** (Happenings/Happenings.cs:429, inside the council component). `raids`
  promised, `departed` ex-colonists (6), `grudges` (prisoners who died in our hands, 10, two years; duplicates the
  deed "prisonerDied"), `visits`, `memos` (what an event left behind: debt, order, hunt, deserter, child, pilgrims,
  contest, challenger, beast; 24; no map), `regulars` (returning visitors, 5), `seenQuests`.
- **Stories and missions: `StoriesComponent`** (Missions/StoriesComponent.cs:13): `chains` (120 days after ending),
  `missions` (2 days after ending).
- **Camps: `CampTracker`** (Camps/CampTracker.cs:37): the live `camp` and `holding` (warmth per colonist, terms,
  tension), `past` camps (6), the diary (30), the `warBand`.
- **Couriers, visitors, strangers.** Courier trips (12 open), visitor trips, callers' tasks and commissions, book
  deals, nemeses, encounter cooldowns.
- **Other ties.** `guestKin` (someone who joined from a visit), `movingOnHeld` (a relative elsewhere), vendettas and
  eulogies, a story's faction, the scenario's debt memo.
- **Not stored anywhere: worked out each time.** A town's state; a people's stance (FactionMemory.Tone,
  NotableBook.Baseline/Opinion, the memory-tag thresholds); an outsider's town (`Outsiders.TownOf`, Outsiders.cs:179).
- **Overlaps to fold together.** Six regard scores toward outsiders (`Notable.personal` and regards, `OutsiderPair`
  warmth and familiarity, `Camp.warmth`, `Regular.bond`, `friendOf` in past camps and regulars). Eight ways of keeping
  a pawn in the world (Notables `Keep`, `WorldSituation.held`, `Memo.pinned`, `Departed.pinned`, `Mission.group`,
  `PastCamp.people`, story chains, regulars), released by checks such as `HeldElsewhere` (Notables.cs:569). The name
  `Deed` (Social/Gossip/Deed.cs, a colonist's reputation) is unrelated to `FactionDeed`.

## 2. Histories
- **The chronicle** (`CouncilGameComponent.chronicle` :65, `ChronicleEntry` Chronicle.cs:37: tick, kind of 21, text,
  pawn id; no people, place or others). 300 kept, the newest 200 always, milestones outlast everyday lines
  (Lean/Caps.cs). 155 call sites; read by the chronicle panel, World page, year letter, couriers, calendar, stories,
  annals.
- **Annals** (Annals.cs:28: the year's lists; `written` 40 years). **The year letter** (World/YearLetter.cs, reads the
  others). **Journals** (Journal.cs:42: 60 lines each, 30 departed journals). **WeekMemory** (dilemma records, 60).
  **Talk news** (`TalkTracker.notes`, 40, 30 days: raid, death, wedding, birth, caravan; read by OutsiderFacts.Colony).
  **Per domain:** situation records (40), past camps (6) and the diary, notable notes and losses, `ArcRecord` (200),
  nemesis endings, `CalendarDay` (with battles, homecomings, camp gifts), the day samples (120 days), the evening news.

## 3. Facts and text
- **Outsider talks.** `OutsiderTalkDef` (about 592 pieces, 18 files, format at the top of
  `Rimfolk_OutsiderTalks_Greetings.xml`). `OutsiderTalk.Choose` (:389) calls `OutsiderFacts.For(c, x, tone)` (:33),
  then `OutsiderTalkMath.Choose` (`Fits` :420 by `LineTags.Match` and filled placeholders, `Score` :461, `Build` :545,
  `Fill` :593). `TalkFacts` (:79: tags, us and them tags, values, refs behind values, heat). Callbacks by marks,
  resolved in `OutsiderFacts.Since` (:843). About 62 placeholders and 151 tags plus families (deed, ws, situation,
  ended, condition, town, war, colony, home, their, them, us, tone, trait, skill). Sections: Moment, People, Persons,
  Pair, Town, Land, Notables, Places, Wars, Situations, Conditions, Colony, Deeds, Marks.
- **The shared tag syntax** `[a,b|c,!d] text`: Lean/LineTags.cs (`Parse` :81, `Match` :125); picking in Flavor.cs
  (`TagsFor`, `Pick`, `PickFresh`, `PickTagged`, `FromPool`), repeats avoided by `LineMemory`.
- **Separate condition and placeholder systems.** Dilemmas, events and encounters (`LetterTags.For`,
  Dilemmas/DilemmaChoices.cs:114; memory tags from `Interplay.AddMemoryTags` and `KinMemory.AddTags`; named arguments
  in `DilemmaContext.Format` Dilemmas.cs:284, extended in Happenings.cs:160 and WorldSituation.cs:26). World situations
  (`WorldSituationWorker.Tags` :372, flags in XML). Neighbour letters (`.Replace` of {WRITER} {NAME} {TOWN} {FACTION}
  {TOPIC} {HELP}, NeighbourLetters.cs:974-990; its own world-news reader `NewsItems` :1005, parallel to OutsiderFacts).
  Outsider bubble lines (`Outsiders.Line` :515). Petitions and journals (`FromPool` and `.Formatted`). Talk topics
  (`TopicWorker.AddTags`). Seven private `Fill` helpers (CalendarKeeping, OfficesElection, CampKinds, PlayWords,
  Pressures, ImpactHints, ImpactMath) and raw `.Replace("{X}")` in eight places. `News.Post` takes finished text.

## 4. How the player is told
Call sites (not messages shown): 237 `Messages.Message`, 76 `LetterStack.ReceiveLetter` (and 13 standard or choice
letters), 150 `News.Post`, 73 `Notify.Message`. The most: Building 39, PlayOut 35, Defense 19, UI 18, Missions 15,
Offices 13, Happenings 7 + 12 letters + 11 news, World 15 Notify + 7 letters. Today: `Notify` filters by the message
level setting only; `News` holds minor news for an evening letter (same text twice a day dropped, 40 waiting at most,
`NewsDays.Group` merges a busy colonist's lines); `LetterGap` keeps 12 hours between Rimfolk's questions that can
wait; pacing settings for world events, situations and dilemmas; letters to neighbours 3 open at most; outsiders'
patience. Missing: any cap per day or per kind on messages or letters; the 237 direct messages bypass both Notify and
News (all 15 in Missions/, World/WorldSituations.cs:548).
