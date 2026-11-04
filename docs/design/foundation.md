# Stage 0: the foundation (design, 4 November 2026)

The plan is in ROADMAP.md ("The world and its people"); what exists today is in `docs/design/world-stores-inventory.md`
and `docs/design/multicolony-audit.md`. This is how the foundation is built: what each part is, what it takes in,
and the order, so that each step ships on its own with the game playing as before.

## Principles
- **The game plays as before at every step.** Each step is a release of its own with every check green. A store is
  moved only when the new home reads the same as the old one (old saves load into the new place on load).
- **A facade first, then the move.** Each new service starts by reading the stores that exist today, so the features
  of stages 1 and 2 build on it at once; the old stores move in behind it one at a time.
- **One way per need.** One idea of "a colony", one ledger of the world, one history, one facts service, one way to
  tell the player, one way to keep a pawn in the world. A second way is a bug.
- **Bounded and spread out.** Every saved list has a cap in `Tools/save_estimate.py`; every periodic pass a slot in
  `Lean/Sched.cs`; nothing at world level per tick; towns within the region map's reach in detail, the rest coarse.
- **Every age.** Each part knows the colony's age and the other people's (tribal, medieval, industrial, spacer; the
  Empire apart).

## 1. Homes: what "a colony" is (fixes the root cause of the audit)
`Lean/Homes.cs` (new, a GameComponent for the little it saves):
- **A colony** is a player home map; its key is its settlement's world object id (`map.Parent.ID`), which outlives
  the map's own id and is what the ledger uses for it.
- `Homes.All`: the player homes, in the order they were founded (on the ground before orbit, as `BusiestHome` does).
- `Homes.Seat`: the colony the council sits at: the first founded until the player moves it (a button later). It
  replaces `BusiestHome` wherever one colony is meant for the whole game; it doesn't switch as people move.
- `Homes.Of(pawn)`: the colony someone belongs to. On a home map now: that one. Else the home where they own a bed.
  Else the last home they lived on (kept per pawn, set after a day on a home map). Else the seat. One answer for
  everyone away (a caravan, a quest site, an outpost, orbit); today's `HomeIs` counts them for every home.
- `Homes.People(map)`: the free colonists who belong there (the start is `MeetingAttendance.Members`).
- `Homes.Name(map)`: the settlement's name, for texts once there is more than one colony (`map.Parent.LabelCap`, as
  `Workers_World.cs:177` and `Family.cs:243` do).
- `Homes.Changed`: an event when a home is founded, abandoned or handed over, for the systems that keep state per
  colony (none hooks a faction change today).
With one colony every answer is what it is today, so this step changes nothing in play.

## 2. The world ledger
`World/Ledger/` (new), one GameComponent `WorldLedger`. Ids are plain values: `Ref` = kind + int (Person: a pawn's
`thingIDNumber`, or a ledger number for someone with no pawn; Town: `Settlement.ID`; People: faction `loadID`;
Colony: the home's settlement id; House: a ledger number).
- **Person** (who is out there): name, pawn reference while one is kept, people, town, house, roles (head, heir,
  envoy, trader, acquaintance; later ward, hostage, spouse), met by whom and when, alive or dead. The notables are the
  first people in it.
- **House** (a family line, ours or theirs): name, people, seat (a town or one of our colonies), members, renown, a
  focus. Rimfolk's family lines become houses too (renown, stage 2).
- **Town** (a settlement's state): id, people, name, founded (Faction Territories' construction when there is one),
  prosperity, its mood toward each colony, needs (food, medicine, protection), its head, recent events (history ids),
  abandoned or destroyed. In detail within the region map's reach of a home, coarse and seasonal beyond.
- **People** (a faction's stance): toward each colony, from what it remembers (`FactionMemory` stays the memory; the
  stance is stored here instead of worked out in three places), its wars (`WarSeen` moves here), its age.
- **Tie** (every tie between two parties, one record): kind (favour owed, pact, truce, marriage, ward, hostage,
  fealty, grudge, debt, promise, regard), from and to (refs), terms (goods, count, days, a free text key), since,
  until, state (open, kept, called in, broken, ended), and the history event it came from. What exists today folds in:
  `OutsiderFavour`, `OutsiderWord`, the debt memo, `Grudge` (which repeats the deed "prisonerDied"), `GuestKin`, the
  scenario's granted debt, vendettas toward a people. Regard (the six scores toward outsiders today: a notable's
  personal and per-colonist regard, a pair's warmth and familiarity, a camp's warmth, a regular's bond) becomes ties of
  kind regard between two refs, each with its own feeding rules kept.
- **Keeping pawns in the world**: `Ledger.Keep(pawn, reason)` and `Release(pawn, reason)`; a pawn stays while any
  reason holds. It takes over the eight ways that exist (notables, world situations, memos, departed, missions, past
  camps, story chains, regulars) and their `HeldElsewhere` checks.
- Bounds: persons by the notables' rule (6 towns in reach x 4 roles + 10 acquaintances) plus those in a live tie;
  towns kept while the settlement stands (a summary of a dead town for 2 years); ties capped per pair and per kind,
  ended ones folded into history after 60 days. Modelled in `Tools/save_estimate.py` before the first save.

## 3. One history
`World/Ledger/History.cs`: `HistoryEvent` = id, tick, kind (a small def), actors (refs with a role each: did, for,
against, about), place (a colony, a town or a tile), weight (how much it matters), text key and its values, and an
optional finished line. Indexes by actor, place and kind, rebuilt on load.
- `AddChronicle` writes through it (the chronicle panel becomes a view of it), and so do the deeds of `FactionMemory`,
  Heard lately, talk news, situation records and calendar days, each keeping its own view.
- Folding: past 60 days, small events fold into a summary per actor and kind ("traded with Lenlia's caravans eleven
  times this year"); the weighty ones stay (the chronicle's milestones rule, `Lean/Caps.cs`).
- Read by conversations, letters, news, the year letter and the Almanac through the facts service.

## 4. One facts service
`Text/Facts/` (from `Outsiders/OutsiderFacts.cs`): a scene (speaker, listener, colony, place, people, tone) gets tags
and values from providers (today's sections: moment, people, persons, pair, town, land, notables, places, wars,
situations, conditions, colony, deeds, marks; new ones from the ledger and the history). The tag syntax stays
`LineTags`. One `Fill(text, values)` replaces the seven private fill helpers and the raw `.Replace("{X}")`.
Neighbour letters, petitions, dilemmas' letters, news and the year letter move to it one at a time; the outsider talks'
callbacks (`Since`) become history queries. Ages: providers add the colony's and the other people's age tags.

## 5. One voice to the player
`UI/Tell.cs` over `News` and `Notify`: `Tell.Message(kind, importance, colony, text, look)` and `Tell.Letter(...)`.
- A cap per day by kind and by colony, the same thing within hours merged, minor ones to the evening letter (as News
  does), Rimfolk's questions spaced as `LetterGap` does, the settlement named once there is more than one colony.
- The 237 direct `Messages.Message` calls are sorted once: answers to a click stay direct (the game's own reject and
  confirm sounds), everything else goes through `Tell`. Other mods' world noise goes the same way (Faction
  Territories' distant settlements, 0.64.4).

## 6. Budgets in sight
`Lean/PassClock.cs`: every pass in `Sched`'s table timed with `Stopwatch.GetTimestamp` (two reads a pass), kept as
the hour's and the day's total and the worst single run; a dev window "Rimfolk's clock" lists the passes, the ledger's
and history's sizes and the save model's figures. Costs nothing that shows; read at the player's request with a Dubs
Performance Analyzer capture of their own save now and then.

## 7. Many homes (the audit's 38 breaks)
After `Homes`: state that belongs to one colony is kept per colony in a `ColonyState` keyed by the colony's id, inside
the council component (offices and elections, decrees, ambition and meeting, calendar and traditions, the wish pool's
cap, camps, the event timer and its memos, social events, encounters, herd leader and resident beast, the year
letter). Shared things get a colony key (suggestion keys and automation budgets, dilemmas' pool, names, groups within
one home, battle state per map). An old save puts everything it has under its one colony. Texts that mix two colonies
name the settlement. Clean-up checks "still our home", not "the map still exists" (for a hand-over). Order: the event
timer and memos, then the council's seats, decrees and meetings, then the calendar, then camps and the rest.

## 8. Other mods behind adapters
`Integrations/World/`: one small interface for world mechanics (who owns a tile, settlements being built, a
settlement founded or taken, our vassals), with adapters read by name: Faction Territories and Vassalage
(`FactionTerritories.Expansion`, its vassal components), [SR] Factional War (its battles on our map), Vanilla
Outposts Expanded (outposts as our holdings), the Empire (titles), Diplomatic Marriage+. Each optional; nothing in the
core names a mod.

## 9. Ages
`Lean/Ages.cs`: `Ages.Of(colony)` and `Ages.Of(faction)` give tribal, medieval, industrial or spacer (from the tech
level, as `Flavor.AddEraTags` does today), with the Empire apart; `Ages.Form(feature, ours, theirs)` gives the word and
rite for each feature from keyed strings per age (ROADMAP.md's table), so text and features ask one place.

## 10. The coupling rule
Written into DESIGN.md: systems talk through `Homes`, the ledger, the history, the facts and `Tell`, never through
each other's insides. Each system's ticks, saved data, messages and moods are listed in DESIGN.md's tables (ticks and
save size are already; messages and moods are added). New features of stages 1 to 3 are rules and text on these.

## The order, each a release
1. `Homes` and the root cause: `BusiestHome` users moved to `Homes.Seat` or a loop over homes; `Memo` gets its colony;
   the event timer per home. One colony plays as before.
2. `PassClock` and the dev window.
3. The ledger as a facade over today's stores (notables, faction memory, outsiders, situations, happenings), then
   the moves: persons and towns from the notables, ties from favours, words, grudges, debts and guest kin, the one
   keep-in-world.
4. The history, with `AddChronicle` writing through it, then the folding.
5. The facts service out of `OutsiderFacts`; letters first.
6. `Tell`, the caps, the settlement named; the direct messages sorted.
7. Many homes, by the audit's order.
8. The adapters.
9. Ages.
The harm review runs alongside 7: each system is visited anyway, and its bad side is proposed to the player then.

## Tests and checks
Pure parts are tested like the rest (`Tools/Tests`): home membership rules, the ledger's ids, ties and their states,
keep and release, the history's folding and indexes, the facts' fill, the voice's caps and merging, the clock's
arithmetic, the ages. `LeanTests` keep the schedule; the save model gets every new list before it ships. Each release
lists what to look at in the game.
