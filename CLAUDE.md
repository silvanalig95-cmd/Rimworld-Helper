# Rimfolk (ColonyCouncil) — working notes for Claude

A RimWorld mod. The player plays 1.7; the mod compiles against the 1.6 reference assemblies.
Work in progress and the next steps are in `docs/HANDOFF.md`: read it first.

## Git
- Develop and push on branch `claude/charming-pasteur-9qbjle` (`git push -u origin claude/charming-pasteur-9qbjle`). No pull requests unless asked.
- End every commit message with the attribution lines the session gives you (Co-Authored-By and Claude-Session).
- No model names in commits, code or docs. Leave a clean, pushed tree.

## Player rules (binding)
- Soft integrations only, and only content the colony has unlocked.
- Nobody is hurt for flavour. Bug fixes come before features. No filler.
- New wishes and features are proposed as a list first; the player's own explicit requests can go straight ahead.
- Answers play out on the map; no countdowns in text. Speech goes through `Speech.Say` / `Together`.
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

## Code conventions
- Harmony patches are classes with `[HarmonyPatch]`, applied per class by `RimfolkPatches.ApplyAll`; `RimfolkPatches.On` is the deep-hooks setting.
- `Sched` rare offsets must be unique and must not collide with hourly offsets mod 250 (LeanTests).
- Comments and player text: plain, concrete prose, matching the surrounding code.
