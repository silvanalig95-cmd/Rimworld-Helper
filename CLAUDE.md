# Rimfolk (ColonyCouncil) — working notes for Claude

A RimWorld mod. The player plays 1.7; the mod compiles against the 1.6 reference assemblies.
Work in progress and the next steps are in `docs/HANDOFF.md`: read it first.

## Git
- Develop and push on branch `claude/charming-pasteur-9qbjle` (`git push -u origin claude/charming-pasteur-9qbjle`). No pull requests unless asked.
- End every commit message with the attribution lines the session gives you (Co-Authored-By and Claude-Session).
- No model names in commits, code or docs. Leave a clean, pushed tree.

## Player rules (binding)
- Soft integrations only, and only content the colony has unlocked.
- Harm is part of the game (the player's rule since 3 November 2026): features may hurt pawns (injuries, illness, attacks, fights) when it's flavourful or makes the game more interesting, at a rate comparable to the game's own chance of something bad happening; never gratuitous, always from something that happens on the map, and shown.
- Every age, not only the medieval one (the player's rule since 4 November 2026): nothing is switched off for not being medieval; each feature takes the form of the age, by the colony's own tech level (its means) and the other people's ways (the rites). The Empire keeps its own titles and honour.
- The colony is the family (the player's rule since 5 November 2026): this is never a dynastic game. Mechanics revolve around the colony and its people, not houses, dynasties or family lines; everyday family life (names, suppers, kin's moments) stays as flavour with no mechanics tied to it. Council tasks and the keeper of secrets were dropped the same day.
- Draw from the full mod pool (the player's rule, repeated 7 November 2026: "always draw from the full mod pool ... for
  weapons, apparell, items, gear, xenotypes etc."): content that picks things (weapons, apparel, items, gear, xenotypes
  and races, animals, magic and abilities) uses everything in the player's mod list that fits, softly (`MayRequire` or
  by name in code), with a vanilla fallback; never only the base game when the mods offer more. The catalogue of every
  def the player's mods add is `docs/player-mods/` (built from their own files); build from its `active` rows only, the
  mods actually enabled in their list and save (the player: "always consider the actual mods i have enabled in my save
  and mod list"), and check every def name and tag against it.
- Bug fixes come before features. No filler.
- The gods are unseen (the player's rule since 11 November 2026): the gods act like a storyteller, an ominous presence,
  above the player as above the pawns. The player learns only what the pawns know: what they saw, were told, believe or
  felt, attributed and possibly wrong (the colony's own book of the gods, kept by its keeper). Never a meta view: no
  favour, wants, rules or truths shown, and no Almanac page of the gods (docs/design/gods.md, "The gods are unseen").
  Events and cards may name a god when the colony knows it was that god (it was seen, revealed itself, or the gods walk
  openly), by the name the colony knows it by; else unnamed or "some say".
- New ideas and requests go on the to-do list first (the player's rule since 11 November 2026): every new wish or
  report is written into `docs/HANDOFF.md` as a "To do when the player asks" entry with their words, and nothing is
  built until they say "do this now", "start now" or the like; gathered entries are then planned and built together.
- Name mods that would help (the player's rule since 11 November 2026): for every request, past or future, check
  whether existing mods would give Rimfolk what it can't make itself, graphics above all (swords, animals, buildings,
  creatures) and sound or music, and tell the player which to add to their list (with the Workshop link, and whether
  they're on the current game version and still kept up). Use those already in `docs/player-mods` as the reference first.
- New wishes and features are proposed as a list first; the player's own explicit requests can go straight ahead.
- Answers play out on the map; no countdowns in text. Speech goes through `Speech.Say` / `Together`.
- Events and answers show on the map as accurately as possible: what the words say happens is what the player sees.
- Spoken bubble lines are one sentence (ImmersionTests).

## Releases
- `Defs/AlmanacArticleDefs/Almanac_WhatsNew.xml` drives `CHANGELOG.md` (`python3 Tools/changelog.py`, then `--check`).
  Each round has 2–8 points, no em dashes or exclamation marks. `About/About.xml` modVersion equals the newest round.

## Checks (run all before pushing)
- Build: `cd Source/ColonyCouncil && dotnet build -c Release` (only the CS0108 warning in Decrees.cs is expected; the build rewrites the tracked `1.6/Assemblies/ColonyCouncil.dll`).
- Defs: `dotnet Tools/DefValidator/bin/Release/net8.0/DefValidator.dll . ~/.nuget/packages/krafs.rimworld.ref/1.6.4871/ref/net472 1.6/Assemblies/ColonyCouncil.dll` (0 errors; rebuild it with `cd Tools/DefValidator && dotnet build -c Release` if its source changed).
- Tests: `cd Tools/Tests && timeout 900 dotnet run -c Release` ("All tests passed"). Pure code shared with the mod is linked in `Tools/Tests/Tests.csproj`.
- Text: `python3 Tools/labelcheck.py`, `python3 Tools/almanaccheck.py --base HEAD` (Almanac keywords must not change), `python3 Tools/save_estimate.py --strict` (a new saved collection needs a model in `Tools/save_estimate.py`), `python3 Tools/interaction_audit.py --open`, `python3 Tools/playout_audit.py`.
- 1.7: `timeout 600 dotnet run -c Release --project Tools/ApiDiff -- 1.6/Assemblies/ColonyCouncil.dll 1.6.4871 1.7.4977-alpha.2` must print "Nothing the DLL uses has changed".
- Mod pool: `python3 Tools/poolcheck.py` must print "OK" (every MayRequire'd def or tag exists in the player's mods under the package named; `docs/player-mods/`).

## Code conventions
- Harmony patches are classes with `[HarmonyPatch]`, applied per class by `RimfolkPatches.ApplyAll`; `RimfolkPatches.On` is the deep-hooks setting.
- `Sched` rare offsets must be unique and must not collide with hourly offsets mod 250 (LeanTests).
- Comments and player text: plain, concrete prose, matching the surrounding code.
