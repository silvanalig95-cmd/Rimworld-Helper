# Stage 3: many colonies, one world (design, 4 November 2026)

> **To revise before building (the player's word, 5 November 2026):** the colony is the family, never a dynasty. Before
> stage 3 continues, go through this design and the 17 decisions again and recast what leans on families and lines:
> a town's Heir (the next head by office or standing, not blood), the kin town and "a colonist with family there",
> notables' houses, anything inherited down a family. A colonist's own ties (spouse, children, friends) in the moving-on
> wishes stay: they are personal. Then put the revised decisions to the player again.

The plan is ROADMAP.md, "The world and its people": stage 3, approved by the player, who said they really like starting
a new colony or base on the same world map so the world keeps developing, and playing different colonies within it:
expanding the colony, leaving it behind, or starting a whole new scenario or colony in the same world. Approved: many
colonies in play (every system right with several), handing a colony over (a town of a new people, the former colonists
its notables, the council's speaker its head; it keeps its history, writes, trades, asks for help, and can be taken
back), and a fresh start inside the same save (a new place, a group from the old colony or newcomers; the old colony
handed over, kept or abandoned). It builds on the foundation (DESIGN.md, "Homes" to "Ages"; steps 7a and 7b, with
`docs/design/multicolony-audit.md` for what is done and what was left) and on stage 1's region
(`docs/design/stage1-region.md`). The game's side was read in the decompiled 1.6 code (32bitx64bit/RW-Decompiled-1.6).
A season is 15 days, a year 60.

## Summary

- **Many colonies in play.** The player can move the seat; two councils become one when people move together; a
  Colonies page shows every home and every town the colony once was; the pages follow the colony in view; and the
  places 7b left anchored at the seat (the notables, the world events, the Neighbours page) work for every home, so a
  second colony far away has neighbors with faces too.
- **Handing a colony over.** From the settlement's world button (beside the game's Abandon) or the Colonies page: who
  stays and who goes, each with their wish shown; the colony's meeting gives its view; a ceremony on the map in the
  age's form; when the last who go have left, the settlement becomes the town of a new people made in play (the
  game's own kind of peaceful people of the colony's age), led by the speaker, its people the former colonists, its
  faith the colony's. The game closes the map as it does for any town; Rimfolk keeps the colony's layout so its map,
  when it is made again, is the colony as it was left. Afterwards it writes, trades, invites, sends people home and
  asks for help, at the game's and the region's rates; it can be raided, fall on hard times or turn against the colony;
  it can come back peacefully or be taken by force.
- **A fresh start in the same save.** A place picked on the world map with what Rimfolk knows of it; a founding party
  from the old colony (the journey planner, by caravan, vehicles, pods, shuttle or gravship), newcomers made like a new
  game's group, or both; the old colony kept, handed over or abandoned. The new game's character page can't be shown
  honestly mid-game; the game's own mid-game window (the one it offers after a game over) is the honest alternative,
  and any scenario's start parts (who, their limits, their kit, their arrival, Rimfolk's story parts) can shape the new
  colony alone. The history, the calendar and a record of every colony keep it all.
- Only the game's own objects (settlements, factions, world pawns, maps), so a world without Rimfolk is an ordinary
  world. Nothing per tick; no new schedule slot. With one colony and the settings off, it plays as before. Five
  releases; seventeen decisions at the end.

## What the game lets a mod do (verified in the 1.6 code)

- **Settle.** `SettleUtility.AddNewHome(tile, faction)` makes the settlement (named by the game) and
  `GetOrGenerateMapUtility.GetOrGenerateMap(tile, Find.World.info.initialMapSize, null)` its map, which is what a
  caravan's Settle does (`SettleInEmptyTileUtility.Settle`). The game's limit is a player preference, not the save's:
  `Prefs.MaxNumberOfPlayerSettlements` (1 by default, 1 to 5 in the options);
  `SettleUtility.PlayerSettlementsCountLimitReached` counts maps that are a player home under a `Settlement` and
  maps made by a gravship landing. A home with no map doesn't count.
- **Abandon.** `SettlementAbandonUtility` asks, then `Settlement.Abandon`: the settlement is destroyed and a new
  `AbandonedSettlement` (a new world object id) marks the tile; the map goes; colonists still on it pass to the world
  as lost, with the game's letter. If no other home is left, everyone gets the game's new-colony hope. `GameEnder` ends
  the game when no free colonist is left on a map, in a caravan, in pods or on a gravship.
- **Turn a player settlement into another people's.** `WorldObject.SetFaction` only sets the faction (the settlement's
  material is cached with the old colors and must be cleared). `Map.IsPlayerHome` reads the parent's faction, except
  for a map made by a gravship landing, which stays a home whatever its parent. Once it is no longer a home,
  `Settlement.ShouldRemoveMapNow` closes the map as soon as no player pawn is on it and nothing blocks removal; the
  world object stays, with its id. `MapDeiniter` passes every pawn to the world: the player's as lost (the game's
  letter), others as plain world pawns of their faction, and colonists on a hostile parent's map as kidnapped. Things
  on the map go with it. When that town's map is made again (an attack), the game makes its people's ordinary base
  (`Base_Faction`, or a base-generation mod's layouts), not the colony.
- **The gravship (Odyssey).** Taking off from a home without a grav anchor calls `GravshipUtility.AbandonMap`: pawns
  left on the ground are lost, then the settlement is abandoned. With an anchor the home stays.
- **Take a town.** `SettlementDefeatUtility.CheckDefeated` replaces a beaten town with a `DestroyedSettlement` that
  keeps its map, and the last town of a people makes it defeated. A destroyed settlement's map has the game's Settle
  button (`SettleInExistingMapUtility.Settle`), which makes it a home under the max-settlements limit.
- **Make a people.** `FactionGenerator.NewGeneratedFaction` gives it an ideoligion (chosen or generated), a name from
  its def, relations with every people by the def's rules (`TryMakeInitialRelationsWith`), a town placed at random
  unless hidden, and a leader generated from its def; `FactionManager.Add` adds it. `faction.leader` can be set,
  `FactionIdeosTracker.SetPrimary` sets its faith, `Faction.SetRelation` its relations. Goodwill with the player drifts
  toward a natural goodwill (`CheckReachNaturalGoodwill`: outside 50 either side of it, 10 points each 50 days),
  which `GoodwillSituationDef`s (same ideoligion, meme compatibility) move and a mod can add to. Only a temporary
  faction is ever removed (once nothing is left of it); a faction whose def is missing is dropped on load with an error.
- **World pawns.** The game never lets go of a pawn who was ever a colonist (`WorldPawnGC`, "Colonist"). It may
  redress a free world pawn of a people into that people's groups: a town's `previouslyGeneratedInhabitants` first when
  its map is made, else a small chance for any group (`PawnGenerator`).
- **Starting pawns mid-game.** `Page_ConfigureStartingPawns` is a page of the new-game flow: a scenario sets its rules
  in `PostIdeoChosen` (which also clears the game's custom xenotypes and resets the default drug policy), and its Back
  and Next lead into the world and site pages that start a game. The game's own mid-game window is
  `Dialog_ChooseNewWanderers`, offered after a game over: it sets a fresh `GameInitData`, makes one to six people with
  `StartingPawnUtility` (the same portrait area, character card with randomize, Biotech's xenotype and life stage
  options, health, relations and possessions, and the skills summary as the new-game page), then fires
  `GameEndedWanderersJoin`, which walks them in at a home's edge with their possessions.
- **Arriving.** A scenario's `PlayerPawnsArriveMethod` (standing, drop pods, gravship); transport pods
  (`TransportersArrivalAction_FormCaravan`, `_LandInSpecificCell`, `_VisitSettlement`, `_GiveGift`, `_Trade`); Odyssey's
  passenger shuttle; Vehicle Framework's vehicles (and Medieval Overhaul's balloon and airship) travel as caravans.

## Shared rules

- **Words match the map.** What a text says happened is what the map shows: the ceremony, the farewell and the
  welcome play out; a town the colony was is the colony as it was left when its map is made.
- **The game's own objects.** The town is a `Settlement` of a faction of a game def; its people are world pawns; a
  fresh start is a settlement and map the game's way. Without Rimfolk the town is an ordinary town of an ordinary
  people (only its kept layout is Rimfolk's).
- **The ledger and the history own it.** A new record, `LedgerColony`, for every colony ever founded: its key (the
  settlement id), name, founding tick, its mother colony, how it ended (abandoned, handed over, lost, taken back) and
  when, the town or ruin it left. A town the colony was is a `LedgerTown` of a new kind, `Kin`, whose id is the
  colony's own key (the same settlement), so `History.At` finds the colony's past under the colony ref and the town's
  under the town ref. New history kinds in a `colony:` family: `founded`, `seat`, `merged`, `handedOver`,
  `abandoned`, `takenBack`, `homecoming`, all milestones. Former colonists are ledger persons kept with a new reason,
  `KeepFor.Kin`.
- **Per colony through `ColonyBook`.** Everything new kept per home is an `IColonyEntry` in a book, so a lost home's
  entry is kept thirty days and one colony plays as before.
- **One voice.** `Tell.Letter` for a town's news, `Tell.Minor` for the small news, `Tell.Ask` after `Tell.CanAsk` for
  questions that can wait. The hand-over and founding windows answer the player's own clicks. This week lists a
  hand-over or a founding planned, never with a timer.
- **No new schedule slot.** The pending hand-over and the town's hour ride `Pass.Region` (hourly), the town's day
  `Pass.DailyRegion`, the seat and the merge the council's hour; each with its pass entry and timed.
- **Settings.** A section "Many colonies": handing over, fresh starts, newcomers, the kin bond in goodwill, the town's
  life (letters, asks, homecomings). Off, a part offers nothing new and keeps its records.
- **Every age.** New `Ages.Form` features (word by our age, rite by theirs):

| Feature | Tribal | Medieval | Industrial | Spacer and beyond |
|---|---|---|---|---|
| The seat | the elders' fire | the council's hall | the town hall | the administration |
| A founding party | a band to make a new hearth | settlers with a charter and carts | a settlement company | a colony mission |
| A hand-over | the fire passed on, the totem left | a charter and the keys | a town charter, the flag lowered | a charter registered, the codes handed over |
| A call for newcomers | word sent along the trade paths | a call for settlers at the markets | an advertisement by radio | a colonization call by comms |
| Coming back | the clans one fire again | the charter returned, oaths renewed | a merger signed | the charter dissolved |

## 1. Many colonies in play: what is left

### 1a. Moving the seat
`Homes.SetSeat` exists with no button. **What the player does:** "Make this the seat" on the Colonies page (1c), for a
home on the ground (a home in orbit can't be one: nobody walks in, as `HappeningTracker.HomeMap` already rules), at most
once a season. **What moves:** what the colony as a whole has, which reads the seat: the events and letters about no
home of their own (about fifty callers of `HomeMap`), the seat's council (`comp.offices` and the others: the speaker who
signs letters to the world), the seat's calendar's world days and nemeses from then on (the old seat keeps those it
had), the region map's default, the year letter's first part. What doesn't move: the game's founders and firstborn
(the colony settled with the game), names, each colony's own council and calendar. **Told:** a milestone
`colony:seat`; the old seat's people trust the council a little less (trust, not a mood); a lost seat goes to the first
home founded on the ground, as now.

### 1b. Two councils become one
When a colony's people come to live at another (their home given up or handed over, or a founding party that joins
an existing home), 7a's rules already hold: a moving officer steps down, the old council is kept thirty days, then let
go. What merges, once the newcomers have lived a day at the new home:
- *Traditions* (the dilemmas' and the calendar's): one only the newcomers kept becomes the colony's at half its
  strength, and lapses by the usual rule unless kept; one both kept keeps the stronger.
- *Elections*: when the newcomers are a third or more of the grown-ups, the next election comes within the season, so
  they have a say. Their decrees ended with their council.
- *Names*: names under the old colony's key are looked at again in the next naming round; where both colonies have a
  holder, the better keeps it, and a merge makes no rival in its first season.
- *The calendar*: the old colony's founding day becomes a remembrance day of the new one while some of its people live
  there (inside the cap of twelve written days).
- *The welcome*: a meal together on the first evening (the play-out's shared meal), the speaker's greeting in a
  bubble; a milestone `colony:merged`. Old hands and newcomers keep apart at first through the groups' own rule
  (people of two colonies form no group), which is the friction the game already has.

### 1c. What the player sees switching between colonies
- **A Colonies page** in the council window's Colony section, shown with more than one home or a town the colony
  was: each home (name, seat, grown-ups and children, its speaker, mood and strain, wealth, its next day, a threat
  now), each kin town (its head, fortune, stance), and the colonies of the past (founded, ended how). Buttons: go
  there (the map in view changes), make it the seat, hand it over, found a new colony. No new bar button: the
  windows' rule stands.
- **The pages follow the colony in view.** The People section's pages show the colony in view with more than one
  home (everyone a click away); the Neighbours page and the region window's panels show the colony in view's
  neighbors (the seat's today); the colony header (`ColonyView.Header`) on the Calendar, Chronicle (filtered by
  colony, all a click away) and the Colony section's pages; the HUD's council panel names the colony.
- Already so: messages and letters name the settlement, This week lists every colony named, the year letter has a
  part per colony, the Overview's wealth tile names each, the region map draws the colony in view's region.

### 1d. What 7b left that matters in real play
- **The notables near every home.** `NotableBook.Home` is the seat, so a second colony's nearby towns have no faces,
  letters or calls. Each further home gets its nearest two towns staffed (beside the seat's four near, two dealt with
  and two new), and acquaintances live in their people's town nearest their colony.
- **World events across the homes.** `WorldSituations` places events from the seat; it picks the home first (weighted
  by its people), so a far colony hears of the world near it. The two running slots stay two.
- **The colony's character per colony** (decision 3): the names for a place and its work (famed hall, walled,
  artful, hallowed, green-fingered, builders...) are about one place and its people, so each colony earns its own;
  "walled" can't be said of a colony with no walls. The five ways follow each council's choices too; a colony founded
  from another starts with half its mother's, newcomers start fresh.
- **Small ones:** the frost alert for every home, not only the one in view (the harvest itself is per map already);
  an outsider's distance and kin from their own colony; the favor bearer and the courier's fallback by the colony
  the answer is about; growth needs' arrivals at their own home.
- **Stays shared on purpose:** the chronicle and annals (each colony named), the wishes' list (counted per colony),
  trust (per person), the heart's episode cooldown, the remembered camps, the faith a decree names (one per people in
  the game), the colony's age (research is the faction's; the means are per map already: a console at that home).

**Ages.** The seat by its age's word (the table); everything else is the systems' own forms.
**Bad side.** None new: a seat moved costs a little trust; a merge's friction is the groups' own.
**Mods.** Faction Territories' territory and Vanilla Outposts' delivery colony are read per home already
(`WorldMechanics.OurHoldings(colony)`); nothing else changes.
**Save and performance.** The notables' cap grows by eight people per further home, all made by the existing rules
(two a day at most); two more towns per home in the region pass. The character per colony: five ways and the names per
home in a `ColonyBook` (under 2 KB a home). Nothing per tick.
**Tests.** Pure rules beside `ColonyStateMath`: the seat (ground only, once a season, what reads it), the merge
(traditions' union and strengths, the early election, names' keys), the character's split (an old save's single
character to the seat; a daughter's half). `ManyHomesTests`: with one colony every answer as before.

## 2. Handing a colony over

### What the player does
"Hand over" on the settlement's world buttons (a postfix on the player settlement's gizmos, beside the game's
Abandon) or on the Colonies page opens one window:
- **Who stays and who goes.** Every colonist, child, animal, prisoner and slave of that home. Each grown-up shows a
  wish worked out from their ties (spouse, lover, children and parents go where they go; close friends and rivals;
  their own bed and room; their office; their age; their faith's relic or shrine there) as "wants to stay", "wants to
  go" or "torn", with the reasons. Children follow their parents unless moved; prisoners and slaves are set free or
  go with those who go. Those who go say where: another home, a founding party (section 3), or a caravan the player
  forms.
- **The head:** the speaker if they stay, else the highest office holder who stays, else the one of most standing; the
  player can pick another.
- **The people's name** ("the folk of Oakhold", by the age's word; the player may rename) and **terms**: a pact of
  kin (a `Pact` tie: they help each other, as stage 2's pacts when built) or a free town owing nothing.
- **What stays with the place**, listed as the game's own abandon would: the stores and goods (the town keeps them; its
  trade stock is its people's), buildings, fields, animals that stay, a relic (theirs from then on), held entities and
  anything the town can't keep (gone with the map, as when abandoning). Mechs go with their mechanitor.
- **The checks:** at least three grown-ups stay (fewer is a homestead, not a town); someone stays the player's in
  another home, a caravan, pods or a gravship (else the game would end); no hand-over during a fight at that home.

The colony's meeting gives its view first (decision 5): at its next meeting, or at once when the player asks, the
council votes by each member's wish and their regard for the player's council; going against its vote costs trust
among those who stay. The window then sets the day.

### What the game does, in order
1. **The ceremony** (a play-out, at the meeting spot): those who stay and those who go gather; the head speaks (a
   bubble), the colony's banner, totem, keys or codes change hands by the age's form; those who go say farewell to
   those they're close to (`Together.Can`, the social log). Those who go then leave: the caravan forms, the pods or
   the shuttle launch, the gravship takes off.
2. **When the last who go have left the map** (`Pass.Region` notices, or at once if none are there):
   - Rimfolk keeps the colony's layout (decision 7): its buildings, floors, roofs and mined-out rock, as a compact
     record (below).
   - It makes the people: `NewGeneratedFaction` with the def chosen for the colony's age (decision 6), hidden at
     creation so the game places no random town, then shown; its primary faith the colony's (`SetPrimary`); its leader
     the head; its relations: allied with the player (goodwill 80), with every other people half the player's goodwill
     with them within the def's own rules (permanent enemies stay so); `FactionManager.Add`.
   - Those who stay and the animals that stay get the new faction (`Pawn.SetFaction`); prisoners and slaves were set
     free or went with those who go (the window said which: the town keeps no prisoners).
   - The settlement's faction is set and its cached material cleared; a gravship-landed home is closed by Rimfolk,
     since the game would still count it a home.
   - The game closes the map as it does any town's: those who stay become world pawns of their people, no "lost" letter.
3. **The records:** the `LedgerColony` ends "handed over"; a `Kin` town record (prosperity seeded from the colony's
   wealth per head, 30 to 70, its founding the colony's founding day, its founder the colony's first speaker); its
   notables: Head, Heir (the next by office or standing), Envoy (the most social), Trader (the best trader), the rest
   ledger persons kept with `KeepFor.Kin` (the game keeps former colonists anyway); the colony's council, calendar and
   books lost by the usual rule (kept thirty days, then let go); the milestone `colony:handedOver`; a letter
   ("Oakhold is the Oakfolk's now") pointing at the town.

### The new people
- **Its def** (decision 6): a peaceful people of the colony's age that the game already knows: human, not hidden, not
  a permanent or natural enemy, no fixed name, no royal titles; the vanilla def first (tribal: `TribeCivil`;
  industrial: `OutlanderCivil`), else a loaded mod's (a medieval colony with Medieval Overhaul gets its civil noble
  house's def; a spacer one a spacer people's if one is loaded), else the nearest age. Its pawns, caravans and gear
  follow that def; its words follow the colony's age at the hand-over, kept on the people's record.
- **Kinship in goodwill** (decision 8): a `GoodwillSituationDef`, "kin of [colony]", +30 natural goodwill while one of
  the former colonists heads it, +15 after, gone after a war between them or a taking by force. The game's own drift
  and same-faith situation do the rest, so the bond holds unless the colony wrongs them.
- **Its growth** (decision 9): with Faction Territories its territory is drawn around it as for any town; new towns
  of its own only at Rimfolk's pace (a flourishing kin town, at most one founded each 90 days), through the existing
  hook on Faction Territories' expansion.

### What it does afterwards
- **Letters.** Its head writes as a notable does (the neighbor letters, at most a season apart for news of its own),
  in the voice of someone who was the colony's speaker: new facts `town:kin`, `them:former` (they were one of the
  colony), `{OLDCOLONY}`, the years since; their talk remembers the colony's days.
- **Trade.** The game's own: a caravan or pods trade at the town; its trader caravans come as any people's, and when
  one is theirs its Trader comes with it (a face the colony knows).
- **Its days.** It keeps the colony's founding day and the day of the hand-over, and invites the colony to one a
  year at most (the invitation missions).
- **Asks for help** in hard times (stage 1's asks of a struggling town), in a kin's words.
- **Homecomings and moving there.** A former colonist (not the head) whose spouse, child or close friend is in the
  colony, or whose town struggles, may ask to come home: once a season per town at most, `Tell.Ask`; yes, they walk
  in from the town's side and rejoin. The moving-on questions can name the kin town: a colonist with family there
  may go to live there and stays known as one of its people.
- **The town's map** is made only when the game makes it (an attack, a fight the colony joins, taking it back):
  from the kept layout, with its people at home in it (below).

### Taking it back
- **Peacefully, the colony asks** (a letter to the head): the answer weighs the head's regard, goodwill, the town's
  fortune (a struggling town is readier), the years since and how much it has grown. Yes: the settlement becomes the
  player's again, its map is made from the kept layout (the game's player-base map, then the layout), its former
  colonists walk out of their houses as colonists, and up to three of its other people choose to join; the people,
  with no town left, is made temporary so the game lets it go (it stays a people if it has other towns). Told as a
  milestone `colony:takenBack`; the game's max-settlements limit applies.
- **Peacefully, the town asks** (with the town's life, release 5): after a long struggle or under a war it can't win,
  with goodwill 60 or more, its head asks to come back under the colony.
- **By force**, as the game allows: attack the town (its map from the kept layout, its people's defenders by the
  game's own group and lord, its notables at home among them), beat it (the game's destroyed settlement keeps the
  map), and settle it with the game's Settle button. The kin bond ends; its people, and every people of the same
  faith, remember a heavy wrong (decision 16).

### Keeping the layout (decision 7)
At the hand-over, over the colony's built area (the home area's bounds and a margin): per cell, the building there
(def, stuff, rotation; multi-cell ones at their origin), the floor, a built roof and whether natural rock was mined
out, as small numbered grids against a table of def names, compressed the way the game saves its own grids (bytes,
deflated): about 10 to 40 KB for a mid-game base, 64 KB at most (past that, roofs then floors are dropped first).
Items, plants, filth and art's tales are not kept. When the town's map is made, the game's terrain and rock come from
the same tile, so the layout fits; Rimfolk stands the game's base generation down for that map only (a guarded prefix
on `GenStep_Settlement`, and on a base-generation mod's own step if one runs, read before patching), places the
layout (`Sketch`, as the game places its own structures), then the people: the game's settlement group under its
defend-the-town lord, and the notables at home. A def the game no longer has is left out. Without Rimfolk, or with
the record lost, the game makes its ordinary base.

### The bad side (the harm rule)
- **Raided** (release 5): a world event "a raid on [town]", at the world events' own rate, only for a kin town in
  detail. Its head's runner, rider, radio or comms (by its means) asks for help: send fighters by the colony's means,
  who arrive on the town's map with its defenders and the raiders (a real fight, the game's own harm), or not.
  Unanswered, the outcome follows the game's measure of the two sides' points: the town loses goods and prosperity
  and some of its other people; a former colonist dies only in a fight the player sees, or of the notables' old-age
  rule (decision 10).
- **Hard times:** stage 1's prosperity: famine, plague and war bring asks; a town long under 15 may be given up (the
  region's rule), and its people come to the colony's gate, the former colonists first, asking to come home.
- **Turning against the colony:** the colony's wrongs (the faction memory, the ledger's grudges, a broken pact) and the
  game's drift lower its goodwill; when the head dies the heir leads, and an heir with a low regard of the colony
  cools the town. Hostile, it raids at the storyteller's rate with its def's raiders (the game may redress a former
  colonist into them, and the letter names them), and stage 1's raiders' camp rule can pick it.
- **Parted:** colonists whose spouse, lover, child or parent stayed (or went) have a season's mood "parted from
  [name]" with a hint (letters, an invitation, a visit); those sent against their wish a season's "uprooted"
  (decision 17).

**Ages.** The ceremony, the letters' channels, the call for help and coming back by the forms table; the head's title
by their age (headman, reeve, mayor, administrator); the Empire keeps its titles: a titled colonist who stays keeps
their title and honor, and the Empire is never the new people.

**Mods.**
- *Faction Territories:* territory drawn around the town; its invasions can take it (the history's `world:taken`; the
  former colonists who live come as refugees); its vassalage is not used at the hand-over (decision 11), and stage 2's
  fealty uses it later as the hamlet does.
- *Vanilla Outposts:* outposts sending to that colony are named in the window; the player points them at another home
  with the mod's own button. Their pawns stay the player's.
- *The Empire:* titles stay with their holders; a titled head's throne-room needs end with the faction change, as
  the game rules.
- *Odyssey's gravship:* leaving without a grav anchor with people staying: the hand-over runs instead of the game's
  abandon (a prefix on `GravshipUtility.AbandonMap` for that map only), after the takeoff, so the layout is the
  ground the ship left.
- *Factional War, Hospitality, quests:* the kin people is a people like any other; they may fight on a home, send
  guests, give quests. *Base generation mods, Real Ruins:* left alone except for a kin town's own map.

**Save and performance.** Per kin town: the town record and its persons (12 at most), the kept layout (64 KB at most),
the people (the game's: relations with every people, a few KB; its faith shared), a pact tie. Kin towns 6 at most
(the game allows five homes). The former colonists are kept by the game anyway. The layout is read once (a scan of the
map's things inside the game's long event) and placed once per map made. The town's day rides `Pass.DailyRegion`;
one more people in every faction loop. Nothing per tick. Each new list gets its model in `Tools/save_estimate.py`.

**Tests.** Pure (`HandOverMath`, `KinMath`, `LayoutMath`): the checks (three stay, someone stays the player's, max
settlements), each grown-up's wish from their ties, the head's choice, relations carried, the prosperity seed, the
kin offset by state, the def's choice by age from def descriptions, the layout's encoding round trip, size cap and
dropping order, the take-back answer, the homecoming's chance and caps. `ManyHomesTests`: a hand-over that leaves one
colony plays as one colony; `LedgerTests` and `HistoryTests` for the kin town and the new kinds.

## 3. A fresh start in the same save

### Choosing a place
"Found a new colony" on the Colonies page starts the game's world targeter. For the tile under the cursor Rimfolk's
panel adds to the game's own tile view: whose land it is (Faction Territories), the towns within reach and their
peoples' stance, how remote it would be (stage 1's rule for a tile), the days of travel from each home by the colony's
means, and the game's own checks (a valid tile, the goodwill warning near a town, the max-settlements limit, named
with where to change it: Options, Gameplay; Rimfolk never changes it, decision 15).

### Choosing who
- **From the colony:** the same who-stays-who-goes list as the hand-over, from any home, with each one's wish.
- **Newcomers:** a window built as the game's own after a game over (decision 12): one to six people (or the
  scenario's count and choice), each randomized, with Biotech's xenotype and life stage options, health, relations,
  possessions and the skills summary, made from the player faction's kind as at a new game; their faith the colony's
  or one the world knows. The game's `GameInitData` exists only while the window is open and is cleared when it
  closes, by the X too. **Why not the new game's page:** it is a step of the new-game flow; its scenario rules come
  from `PostIdeoChosen`, which mid-game would also clear the game's custom xenotypes and reset the default drug
  policy, its Back and Next lead into pages that start a game, and character-editor mods hook it expecting one. The
  game's own mid-game window is the honest alternative; what it lacks is the reordering of chosen and left-behind
  people and other mods' editors.
- **Both:** the colony's party settles first, the newcomers walk in within the day and meet them.
- **Newcomers' price** (decision 13): free only when the old colony is handed over or abandoned in the same act (a
  true fresh start, as the game's new wanderers are free); with the old colony kept they come at most once a year and
  the colony pays their passage from its stores (silver and food worth about their kit, by the call's age form).

### A scenario's start (decision 14)
The player may shape the new colony by any scenario (vanilla's, Rimfolk's seventeen, a mod's). Carried, for the new
colony alone: the people (its count and choice, required kinds, xenotypes, ages and skills, read from its
`ScenPart_ConfigPage_ConfigureStartingPawns*` part's fields, never by calling `PostIdeoChosen`), the parts that shape
starting people (forced traits and hediffs, through their pawn hooks), the kit (`PlayerStartingThings`), the
arrival, the starting animals, and Rimfolk's own parts (`ScenPart_CouncilDays`, `Seats`, `Goal`, `Ways`, `Foe`, `Bond`,
`Secret`, `Event`, `Wards`, `Situation`) on that colony's council and calendar. Not carried, and listed in the window:
what changes the whole game (the player faction, starting research, rules for every map, world conditions, incidents
switched off). The gravship start is offered only when the old colony is left behind. A scenario's kit comes as the
scenario defines it (the player's chosen start); Rimfolk's own kits by age hold only what the colony has unlocked.

### What they bring
From the colony: the journey planner's packing list (`JourneyPack`), with a founding kit suggested by age: food for
the road and fifteen days, medicine, tools and weapons, and what raises the first shelter by its means (wood for a
tribe or a medieval colony, steel and components for industry and beyond), from what the colony has and has unlocked;
pack animals, carts and vehicles; what pods carry; the gravship carries itself. Newcomers: their possessions, and the
scenario's kit or a kit by age from the old colony's stores.

### How they arrive
- **From the colony,** by the game's own travel: a caravan on foot or with pack animals and carts (every age),
  vehicles (Vehicle Framework; Medieval Overhaul's balloon and airship), transport pods (with their research), the
  passenger shuttle (Odyssey), the Empire's shuttle permit for one who holds it, the gravship. Rimfolk adds the
  journey plan's departure (the evening meal together, the farewell at the gate, the hour), a question when the
  caravan reaches the tile ("Settle here?", which runs the game's own Settle; pods first form their caravan, as the
  game does), and the first night: the founders gather at a fire, the eldest or the speaker-to-be speaks.
- **Newcomers,** at the new place as their start says: the settlement and map made the game's way, then they walk in
  from the edge (every age, with a cart and a pack animal for a medieval group), come down in pods (the scenario's
  crash, or the colony's pods if it has them), or land by shuttle (Odyssey, if unlocked). Never a means the colony or
  the scenario doesn't have.

### The old colony
Kept (a second home, under the game's limit), handed over (section 2, in the same act: the hand-over waits for those
who go), or abandoned (the game's own abandon once those who go have left; the `LedgerColony` ends "abandoned", the
game's ruin is known to the ledger as the old home's, its council carried by its people by 7a's rule, and its
founding day becomes a remembrance day of the colony they settle).

### The history and the calendar
The new colony's `LedgerColony` (its mother colony named), a milestone `colony:founded` (the founders "did", the
mother "about"), its calendar from its founding day (`CalendarMath.FoundedTick`), its own founders and firstborn
names (7b's rule), the scenario's days and seats if one shaped it; on the mother's calendar, if kept, "the parting
day", which it may keep as a tradition; the year letter's new part; a new council of its own (7a), its first meeting
called by the founders.

**Ages.** The founding party, the call for newcomers and the first night by the forms table; arrivals by the colony's
means and the scenario's.
**Bad side.** The game's own: caravan incidents on the road, the storyteller's threats at the new colony's small
points, a first winter without walls; the old colony weaker by those who left; "parted" for close bonds split between
the two colonies, for a season. Nothing new hurts anyone.
**Mods.** Faction Territories: the tile's owner and its goodwill; its encounters on the road. Vanilla Outposts: a new
home can be chosen as a delivery colony with the mod's own button; an outpost's people can join the party by leaving
it the mod's way. The Empire: a titled colonist's throne-room needs go with them; the shuttle permit as a means.
Odyssey: the gravship (with or without an anchor left behind) and the passenger shuttle. Vehicle Framework: vehicle
caravans.
**Save and performance.** A new colony is a new map: the game's own cost, which the window names ("two colonies cost
about twice the map time"); Rimfolk's books gain one entry per home. `LedgerColony` records: one per colony ever, 24 at
most (the oldest ended ones fold into the history). The newcomers' window holds its people only while open; those not
chosen are discarded as the game's window discards them. The founding plan is a journey plan (one per home).
**Tests.** Pure (`FoundingMath`, `ScenarioStartMath`): the founding kit by age and unlocked content, the arrival ways
by age, means and scenario, the newcomers' price and yearly cap, which scenario parts carry, the tile panel's facts;
the creation window's `GameInitData` cleared on every close (a check in the self-check); `ManyHomesTests` for a
founding's council, calendar and names.

## The order to build it in

Stage 3 is waves 15 and 16 of the plan, after stage 2; each release plays as before with its settings off and one
colony, with every check green (version numbers as they fall).

1. **Many colonies in play** (section 1): the seat's button and what moves, merging councils, the Colonies page, the
   pages following the colony in view, notables near every home, world events across homes, the character per colony
   (if chosen), the small ones. Needs only 7b.
2. **A founding party** (section 3 with people from the colony): the place picker and its panel, the who-goes list
   with wishes, the journey plan to a tile with the founding kit, the settling question and the first night,
   `LedgerColony` and the `colony:` kinds, the old colony kept or abandoned. Needs 1 (merging, the Colonies page).
3. **Handing a colony over** (section 2): the window, the meeting's vote, the ceremony, the new people (def choice,
   kinship), the kin town and its notables, the kept layout and its placing, letters, trade and its days, taking it
   back peacefully and by force; the gravship case. Needs 2 (the who-goes list, the colony record) and stage 1's towns.
4. **A fresh start with newcomers** (section 3's newcomers): the creation window, scenario starts, newcomers' arrival
   and price, the old colony handed over in the same act. Needs 2 and 3.
5. **The kin town's life**: the raid and the call for help on its map, hard times and giving up, turning against the
   colony, homecomings and moving there, the town asking to come back, Faction Territories' pace for its new towns.
   Needs 3 and stage 1's rise and fall and raiders' camps.

Beside them, the content pass (wave 16): letters, talk, news and journals for the kin towns, the founders and the
newcomers in every age's form; the Almanac pages "Many colonies", "A town of our own", "A new start".

Existing checks that must stay green: `ManyHomesTests`, `ColonyStateTests`, `LeanTests` (no new slot), `TellTests`,
`LedgerTests`, `HistoryTests`, `FactsTests` (1500 talks unchanged with the new facts), `OutsiderTalkTests` (new tags
and placeholders), `ImmersionTests` (one-sentence bubbles), `PagesTests` (the Colonies page), the save model
(`--strict`), the def validator (the goodwill situation, the forms, the world event, the letters), `labelcheck`,
`almanaccheck`, `playout_audit` (the ceremony, the farewell, the welcome), `interaction_audit`, and the 1.7 API diff
for the new game names (`SettleUtility.AddNewHome`, `GetOrGenerateMapUtility.GetOrGenerateMap`,
`WorldObject.SetFaction`, `Settlement.ShouldRemoveMapNow`, `FactionGenerator.NewGeneratedFaction`,
`FactionManager.Add`, `FactionIdeosTracker.SetPrimary`, `Faction.SetRelation`, `GoodwillSituationWorker`,
`GravshipUtility.AbandonMap`, `SettleInExistingMapUtility`, `GenStep_Settlement`, `Sketch.Spawn`,
`StartingPawnUtility`, `CharacterCardUtility.DrawCharacterCard`, `ScenPart.PlayerStartingThings`).

In the game, with dev tools to force each part: a seat moved and an event that follows it; two colonies merged;
a hand-over with a caravan leaving, with everyone going to another home, and with a gravship leaving without an
anchor; the town on the world map (colors, notables, letters); attacking it (the layout, the notables at home);
taking it back both ways; Faction Territories' territory around it; a fresh start by each arrival; the newcomers'
window with Biotech's options, closed with the X; the game's limit at one colony.

## Decisions for the player

1. **The seat.** Moved only by the player (the Colonies page), a home on the ground, at most once a season; what the
   colony as a whole has moves with it, the old seat's people trust the council a little less, no mood? Recommended:
   yes.
2. **Merging councils.** The newcomers' traditions join at half strength, an election within the season when they
   are a third of the grown-ups, names looked at again with no rival from a merge, the old founding day a remembrance
   day, a welcome meal? Recommended: yes.
3. **The colony's character per colony.** Each colony earns its own place and work names and its own five ways (a
   daughter colony starts with half its mother's, newcomers fresh), or one character for the whole game as now?
   Recommended: per colony.
4. **The region for every home.** The nearest two towns of each further home get notables (eight more people a home)
   and world events are placed near any home by its people? Recommended: yes.
5. **The hand-over's say.** The colony's meeting votes first and going against it costs trust among those who stay,
   or the player alone decides? Recommended: the vote, as advice with a cost.
6. **The new people's kind.** A peaceful people of the colony's age the game already knows (vanilla first, else a
   loaded mod's, else the nearest age), so the town outlives Rimfolk's removal; or Rimfolk's own four people defs
   (exact by age, but a save with a kin town then needs Rimfolk, and the town is dropped with an error without it)?
   Recommended: the game's own.
7. **The town's map.** The colony's layout kept (10 to 40 KB a town, 64 KB at most) and placed whenever the town's map
   is made, or the game's ordinary base for that people? Recommended: kept.
8. **Kinship in goodwill.** A goodwill situation "kin of [colony]" (+30 while a former colonist heads the town, +15
   after, gone with a war or a taking by force), the game's drift doing the rest? Recommended: yes.
9. **The kin people's growth with Faction Territories.** New towns only at Rimfolk's pace (a flourishing kin town, one
   at most each 90 days), not the mod's (about one each 30 days for every people)? Recommended: Rimfolk's pace.
10. **Former colonists off the map.** They die only of the notables' old-age rule or in a fight the player sees; a
    raid the colony doesn't answer costs the town goods, prosperity and its other people? Recommended: yes.
11. **Faction Territories' vassalage at the hand-over.** A free town of its own people, fealty later through stage 2
    (with the mod's vassalage, as the hamlet), rather than the mod's vassal outpost at once? Recommended: a free town.
12. **The newcomers' window.** The game's own mid-game window (as after a game over) with the scenario's limits read
    by Rimfolk, or the new game's page itself shown mid-game (closer to a new game, but the scenario's setup would
    clear the game's custom xenotypes and reset the default drug policy, and character-editor mods expect a new
    game)? Recommended: the mid-game window.
13. **The newcomers' price.** Free only when the old colony is handed over or abandoned in the same act; with it kept,
    once a year at most and their passage paid from its stores? Recommended: yes.
14. **Scenario starts.** Any scenario's start parts for the new colony alone (its people and their limits, kit,
    arrival, animals, Rimfolk's story parts), the whole-game parts not carried and listed, the gravship start only
    when the old colony is left behind? Recommended: yes.
15. **The game's colony limit.** Respected and named in the windows (Options, Gameplay), never changed by Rimfolk?
    Recommended: yes.
16. **Taking back by force.** Allowed as the game allows (attack, beat, settle); the kin bond ends and their people
    and every people of their faith remember a heavy wrong? Recommended: yes.
17. **Against their wish.** Those who go or stay against their wish have a season's "uprooted" mood with a hint and
    may ask later to move (to the kin town or the other colony); none refuses the player's choice? And "parted" for a
    season for close bonds split apart? Recommended: yes.

Beyond the plan, proposed for the content pass and built only if picked: a wedding in the kin town between two former
colonists, with the colony invited; old hands and newcomers as a question for the council in a merged colony's first
season; a founders' monument on the new colony's first anniversary.
