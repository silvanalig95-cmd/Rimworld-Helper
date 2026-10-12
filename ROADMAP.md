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
- Done: Rimfolk's context in the game's own letters (the nemesis riding with a raid, what weighed on a colonist who
  breaks, who someone was when they die); nemeses now arrive with the raid itself.
- Done: everyday journal entries drawn from what the game says is on a colonist's mind that day.

## Phase H: Rimfolk events (first batch done)
Things that happen to the colony from outside, drawn by the storyteller (or on Rimfolk's own timer), written for
every era. Called world events until the world situations below took that name.
- Done: 31 events in five groups (pasts catching up, challenges, old enemies, the land and sky, visitors with a
  purpose), fights behind their own switch, era-appropriate loot and visitors, ties into nemeses, secrets,
  mentorships, journals, the chronicle and the colony's character.
- Done: events that build on each other (a deserter's old unit, the lost child grown, a rematch, the great beast's
  young, fed pilgrims' thanks) and the World page (what's coming, who's out there, what happened lately).
- Done: a second batch for industrial and spacer colonies (each with a tribal and medieval version too): lost
  cargo, a talent scout, a journalist, a travelling doctor, music in the air, a famous visitor.
- Done: a third batch of twelve everyday events with no fights: stranded merchants, a neighbor's order (collected
  five days later, shown on the World page), a traveling tinker, neighbors in need of medicine, a quarrel between
  two neighbors, growing weather, a warm spell, an old grave, a box of old letters, runaway lovers, a family visit
  and a fortune teller.
- Done: a fourth batch of seventeen that remember and unfold: follow-ups to the third batch (a regular buyer, the
  runaway lovers' family, a letter from a relative who visited, the fortune teller's bad fortune spent, a quarrel
  remembered), events drawing on epithets, life goals, vendettas (the one fight, behind the battles switch) and the
  colony's founding day, seasonal ones (a harvest fair, a winter-night traveler, the thaw), a caravan to a far
  market, and four that need a mod (A RimWorld of Magic, Alpha Animals, Dubs Bad Hygiene, Hospitality). Things on
  their way show on the World page.
- Next: more events per group.

## Phase H2: Scenarios with stakes (done)
- Done: six scenarios (Hot Money, Marooned, Deserters, Bad Blood, Borrowed Time, The Fallen Tyrant) built from six
  scenario parts anyone can add in the scenario editor: a sworn enemy, a debt, deserters, feuding families, a
  reputation, a secret.
- Done: gentler stakes (later, smaller and fewer attacks; smaller debts; a softer feud; a strength setting) and
  four small, gentle starts with one or two colonists (The Inheritance, Master and Apprentice, Runaway Hearts,
  The Lone Hunter), built from two new parts: a bond from the start, and an event around a set day.
- Next: a tribal and a medieval scenario or two, once the parts have been played with.

## Phase H3: World events (first batch done)
Rare happenings across the whole region that last a while and change what the colony meets, on Rimfolk's own slow
rhythm (about one every 30 to 60 days), at most two at a time.
- Done: the framework (a def format most can be written in without code; modifiers on Rimfolk events and, through the
  deeper game hooks, the game's own; beats, questions and endings that read what the colony did; the region
  remembering who helped), the "In the world" block on the World page, the chronicle, settings, dev tools.
- Done: 22 of them. A warlord rises (hunted by the neighbors, a bounty, an ally's march, a truce, waited out, or
  brought down at home), a plague, a famine, a hard winter, locusts, a flood downriver, a neighbor's leader dying, two
  claimants for a seat, a war of creeds (Ideology), peace talks, a mercenary company, a bandit road, a new town, a town
  abandoned, a trading season, a great fair, a pilgrimage, a silver rush, a famous healer, the great herds, strange
  herds (Alpha Animals) and a season of wild magic (A RimWorld of Magic).
- Next: more of them, and Rimfolk events written to follow up on how one ended (the warlord's son, the town that
  grew from the settlers' camp).

## Phase E: The council at a glance (done)
- Done: an Overview page (the colony's numbers and how they moved this week, graphs of mood, trust, wealth and
  people over the last weeks, and one line from the advisor, the World page, the colony's character and the
  chronicle), and "how often" sliders for encounters, colonist moments, social events and dreams.

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
