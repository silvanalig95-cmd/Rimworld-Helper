# Plan ahead: the planning helper (spec)

The player's request (1 November 2026): they feel overwhelmed balancing many parts of the game at once and want help
with what needs planning ahead (construction, production chains, the base's growth), not with reacting to events.
They are early or mid game (their save: day ~96, 9 colonists, Hospitality guests). Binding:

- **Suggest only.** Nothing acts by itself. Each suggestion has a button the player presses to carry it out, "Not this"
  (it stops being suggested), and Undo after it was done. "Act by itself" may come later, once the player has tested
  that the suggestions are consistent and helpful, so build every suggestion as a discrete step that can be applied
  and undone and that keeps its reason.
- **From the live game, never fixed advice.** Every suggestion is computed from what is on the map now and what the
  colony really used lately; it shows its reason in numbers ("2 meals in store; 17 eat here, about 27 meals' worth a
  day: 0.1 days"), is ordered by urgency (days until something runs short), disappears by itself when its reason is
  gone, and after the player applied it, checks whether it worked (the stock rising, the zone sown) and if not says
  what is in the way rather than suggesting more of the same.
- **Measure food in nutrition, and storage in cells per nutrition, never in item counts.** Some crops (Vanilla Cooking
  Expanded's chickpeas) give little nutrition per item and fill storage; a big pile is not a big food stock.
- **Soft integrations; don't duplicate the player's mods.** Colony Manager Fork (hunting, forestry, livestock,
  foraging, mining; its production manager is switched off), Better Workbench Management (linked bills, counted extra
  products), Everybody Gets One (per-colonist bill targets: leave those bills alone), Smart Farming, Food Alert, Better
  Pawn Control (writes work priorities when its presets switch), Deep Storage and RimFridge (denser storage).
- Player rules from CLAUDE.md apply (only unlocked content, words match the map, no countdowns in text).

## The model (shared by every part)

A reader of the live colony (`ColonyNeeds.For(map)`, cached hourly, built on what exists: `Making` stock reading,
`ColonyUse`/`UseLog` measured use, `Chains`), with its arithmetic in a pure, tested class:

- **Who eats here**: colonists and slaves, prisoners, guests who eat the colony's food (Hospitality), and the colony's
  animals that eat stored feed. Each one's daily nutrition from its own hunger (`Need_Food` fall per tick x ticks a
  day), so genes, pregnancy, life stage and the like count.
- **What there is**: nutrition in store by kind (meals ready, raw food humans eat, animal feed), with the storage cells
  each takes (stacks by the def's stack limit).
- **What is used**: per day, from the record of use when it covers a day (`ColonyUse`), else estimated from the eaters.
- **What the land gives**: per growing zone, its crop's nutrition per cell per day (harvest yield x the product's
  nutrition / grow days, adjusted by the zone's soil fertility and the plant's fertility sensitivity), and the growing
  days left this year from the map's temperatures by twelfth.
- **What the benches and people can do**: per bill, the work per batch and who can do it (work type enabled, skill
  needed), so time and hands are known.

## Phase 1: production (first)

1. **Stock targets**: one row per good the colony relies on (meals; raw food for the season ahead; medicine; spare
   clothes; feed for the animals; the materials queued blueprints need), each with have, use a day, days left and a
   target = use a day x the days chosen (settings), limited by storage and by spoiling (meals without a freezer keep a
   day or two). Status: short, low, enough, more than needed (and the storage it takes).
2. **Bill changes as one-click suggestions** on the player's own bills: raise or lower a "Do until X", add a missing
   bill with what feeds it (reuse `Making.SetUpChain`), move work to a second bench. Skip Everybody Gets One's bills.
3. **Why it's stuck**: for a bill that isn't moving, name the reason (ingredients missing or out of its radius; nobody
   free with the work enabled and the skill; the bench unpowered, unfuelled, broken or forbidden; nowhere to store the
   product) with a fix where one exists. Also used to check an applied suggestion that didn't work.
4. **Field plan**: crop cells from the nutrition the colony needs a day (measured) against what its zones give a day,
   plus a reserve for the days without growing; suggest growing a zone onto neighbouring fertile cells, a new zone, or
   switching cells from a crop the colony has far more of than it uses (by nutrition and storage), shown on the map.

Where: the Making page gets a section at the top ("Ahead"): the stock rows and the suggestions, each with its numbers,
Do it / Show / Not this, and a short log of what was done with Undo and whether it worked.

## Phase 2: construction and the base's growth

5. **What the colony will be short of**: per room kind, need against what is built (beds for the people and those
   likely to join, freezer cells for the food stock and the winter reserve, dining seats, hospital beds, workshop room
   when benches are busy), proposed before it is short, at a spot from the ground plan or "find a good spot", shown on
   the map.
6. **Build order**: the next one or two rooms for the colony's size and research go onto the Plan page.
7. **Projects in stages**: clear, then walls/doors/roof, then climate and power, then floors, then furniture; build
   priorities by stage, a stockpile for the materials by the site, what is holding the project up, and never more queued
   than the builders can do in a couple of days.
8. **Room to grow**: keep the side a room will grow into free (the freezer's free wall, the end of the bedroom row) and
   warn before something is built there.

## Phase 3

9. **Colony Manager**: show its hunting, wood and mining targets beside the stock targets; suggest raising one when a
   chain needs more (by reflection, failing quietly; namespace `FluffyManager`, `Manager.For(map)`,
   `JobStack.FullStack<T>()`, `Trigger_Threshold.TargetCount`).
10. **Shopping list**: what the colony needs and can't make (neutroamine, components before Fabrication, steel when
    deposits run low), for the next trader; where to mine.

## Rules of thumb (starting values only, replaced by the colony's own numbers as they're recorded)

About 3 to 4 meals per colonist a day and 3 days of food in stock; about 25 crop cells per colonist on normal soil
(16 to 20 rice in the best case); about 10 freezer cells per colonist, one wall kept free to grow; bedrooms about 25
cells; one vitals monitor covers 8 hospital beds; corridors at least 2 wide on main paths; the kitchen next to the
freezer, the workshop by the store. Sources: the RimWorld wiki's Colony Building Guide, Food, Farming and Intermediate
Midgame Guide pages, and Steam discussions.

## The reference colony (the player's save, day ~96)

9 colonists and about 8 Hospitality guests; 2 cooked meals in store; the stove's meal bills "until 2" (fine) and
"until 5" (simple); raw food: chickpeas 2390 (35 cells), corn 440, potatoes 268, rice 252; steel 31 and components 25
with machining bills that need them; 15 growing zones (wheat x4, healroot 79, drago tree 92, hops 63, tobacco 52 and
others); one of each bench; nothing under construction.

## The same rules for what already exists (the player's direction, 1 November 2026)

Everything the mod already suggests or automates follows these rules too, through the shared suggestion core phase 1
builds (one step type, one "Not this" store, one log with undo and the check whether it worked). What the inventory
found missing, by system:

- **Advisor cards** (Priorities page): no "Not this"; few act on a click; room-growth signals (dining, kitchen,
  freezer, hospital, workshop outgrown) react once a room is already short: give them the live numbers (eaters with
  guests, seats, freezer cells against the food stock target) and let them warn before it is short.
- **Base review findings**: no "Not this"; distances are straight lines between room centres (use walking distance);
  one finding per rule; several kinds (rooms far apart, the long meal walk, the freezer airlock, catch-all and spread
  stores, empty filters) have no fix button.
- **Plan page (next steps)**: a step that was let go comes back; steps carry no numbers or urgency of their own.
- **Making page**: staple bills get fixed targets when set (meals at most 60, medicine at most 30) and are never
  resized; only the first bench of a kind; idle benches only named. Phase 1's stock targets replace the fixed numbers.
- **The 8 automations** (off by default, they act by themselves when switched on): each one's candidate should also
  show as a suggestion the player can carry out by hand; an undone action must not come back after its cooldown (undo
  counts as "Not this" for that key); their reasons carry numbers. Their switches stay as they are (off unless the
  player turns them on); "act by itself" for the new helper is for later, after the player has tested the suggestions.
- **Defense advice and line reviews**: "Not this" on notes; numbers in reasons where they're missing.
