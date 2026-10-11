# Rimfolk roadmap

A living plan. Each phase ends with a build, the XML validator, a review pass and a push. Phases can overlap;
content work runs alongside code work.

## Phase A: Faster and sturdier (light pass done)
Performance is good in play (big medieval modlist, no complaints), so this stays a light pass: apply the cheap,
safe wins from an audit of the per-frame interface and the hourly simulation, and keep every system skipped
entirely when switched off.

## Phase B: The colony's character (done)
The council's choices add up to a reputation. The colony becomes known as merciful or harsh, generous or
thrifty, open or wary, keeper of traditions or restless for change, easygoing or hard-working.
- Council answers, decrees, granted wishes and how strangers are treated shift the colony's ways; old deeds
  slowly fade, so the character can change.
- Colonists approve or disapprove by nature: the kind like mercy, the greedy like thrift, the lazy like ease.
- Visitors talk about the colony's reputation; some dilemmas and encounters only come up for a colony with a
  certain name. Crossing a line is news ("The colony is becoming known as merciful").
- A page in the council window shows the colony's ways, who approves, and what changed them.

## Phase C: Medieval life
More of what makes a long medieval run feel lived-in.
- More decrees (feast days, tithes, curfews, militia drills, alms for the poor), more quirks, more social
  moments, seasonal festivals that can become traditions, more dreams and secrets.
- Era-aware writing throughout.

## Phase D: Reacting to the world (mostly done)
The colony notices what the game does to it. Done so far (Harmony hooks): weddings, births, homecomings, breakdowns,
exact nemesis detection, victories (who fell, who stood out), quests won and lost.
- Weddings, births, breakdowns, victories, caravans coming home with stories, quests won and lost.
- Journals, the chronicle, friends' reactions and life-goal sparks for each.

## Phase H: World events (first batch done)
Things that happen to the colony from outside, on Rimfolk's own rhythm, written for every era.
- Done: 31 events in five groups (pasts catching up, challenges, old enemies, the land and sky, visitors with a
  purpose), fights behind their own switch, era-appropriate loot and visitors, ties into nemeses, secrets,
  mentorships, journals, the chronicle and the colony's character.
- Done: events that build on each other (a deserter's old unit, the lost child grown, a rematch, the great beast's
  young, fed pilgrims' thanks) and the World page (what's coming, who's out there, what happened lately).
- Next: more events per group, mod-aware events when the user's new modlist arrives.

## Phase E: The council at a glance
- An overview page: the colony's mood and trust over time, open wishes, the colony's character, nemeses still
  out there, and the latest chronicle.
- Finer frequency settings (how often encounters, dilemmas and social moments come up).

## Phase E2: The mod almanac (done for the main modlist)
The Almanac learns about the mods in the user's modlist, and Rimfolk uses that knowledge for new ideas.
- Almanac articles on the mods in play: what each adds, how to get the most out of it, and how it touches
  Rimfolk (medieval factions, hospitality, outposts, quality of life, and so on).
- A reference in the repository (`Modlists/`): each modlist the user shares, plus a list of mods with the
  hooks Rimfolk can use (their pawns, buildings, items, events and settings). New features check it first.
- New or alternative modlists from the user go into the same folder and update the articles.
- Done: 104 Almanac articles covering all 216 mods of the main medieval modlist; notes and `Modlists/HOOKS.md`.
  Next: build the best ideas from HOOKS.md into Rimfolk, a few at a time.

## Phase G: Integrations, the careful way
Built from `Modlists/HOOKS.md`, following the rules in DESIGN.md ("Playing nicely with any modlist"): soft, only
when a mod is active, only what the colony has unlocked, never harder without a mod, no per-run tweaks.
- First: more moments between colonists (conversations that show in the social log and as Interaction Bubbles):
  shared grief, talking shop, praising the cook, old stories, friendly contests that hurt nobody, faith, magic and
  hygiene moments when those mods are present.
- Then: people, magic and beliefs (RimWorld of Magic classes, traits and skills mods, memes); hygiene and cooking
  (bathroom plans, better bath wishes, processors and aging); medieval life (Medieval Overhaul production, wine and
  cheese) when those mods are active.
- Not planned: diplomacy and faction-war features (Diplomatic Marriage, Factional War); weapon duels.
- An audit of existing content against these rules (unlocked-only, mod removal, era fit) comes first.

## Phase F: Release (when the user asks)
- Steam Workshop upload (private first), then an automatic upload of each new version.

## Also done
- Decrees can be repeated or forced through their cooldown, at a price in trust and resentment, with a chance
  someone snaps.

## Done so far
See README.md for everything Rimfolk does today, and DESIGN.md for the reasoning behind it.
