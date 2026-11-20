# The brief every worktree agent gets

The lead hands each build to an agent in its own git worktree (three at most at once) with a brief: the task's own part
(what to build, the player's words, the files to start from, the decisions already made), then this part, unchanged.
For quests and storylines, `docs/quests-proposals.md` and the plain-words rules there go into the task's part too.

## For every Rimfolk build (read this part first)

You work in your own git worktree of Rimfolk (ColonyCouncil), a RimWorld mod. The player plays RimWorld 1.7; the mod
compiles against the 1.6 reference assemblies.

**Read first:** `CLAUDE.md` (the player's binding rules: soft integrations only, harm only at the game's own rate and
shown, every age its own form, the colony is the family (no dynasties), the full mod pool from `docs/player-mods`
active rows (check every def name you use; `python3 Tools/poolcheck.py`), the gods are unseen, words match the map,
speech through `Speech.Say`/`Together` and one sentence per bubble, plain neutral text with the medieval voice only in
the letters' `olde` lines and the gods' and faiths' texts, bug fixes before features, no filler); `docs/HANDOFF.md`
(your task's entry is quoted below with the player's words); `docs/text-style-samples.md` for the plain-text style.
Code conventions: Harmony patches as `[HarmonyPatch]` classes applied by `RimfolkPatches.ApplyAll`; `Sched` offsets
unique; comments and player text in plain, concrete prose like the surrounding code. No em dashes and no exclamation
marks in text you write. No model names anywhere.

**Docs:** update DESIGN.md (the section for your feature, its heading ending "; not released yet)" or "(not released
yet)"), the Almanac page if one covers it (Almanac keywords must not change: `python3 Tools/almanaccheck.py --base
HEAD`), and README where it describes the feature. In `docs/HANDOFF.md`: turn your task's "To do when the player
asks" entry into done (or remove it), and add a "- Not released: no version, no What's new round, About.xml and
CHANGELOG untouched. **Title** (the date; the player's words) ..." entry directly above the newest "- 0." release
line, saying what was built, the code map, what can't be checked without the game, "Try in the game" steps, and mods
that would help. Don't touch About.xml, CHANGELOG.md or Defs/AlmanacArticleDefs/Almanac_WhatsNew.xml. A new saved
collection needs a model in `Tools/save_estimate.py`. Add tests in `Tools/Tests` for pure logic (pure code shared with
the mod is linked in `Tools/Tests/Tests.csproj`).

**Checks (all must pass on your final state):**
- `cd Source/ColonyCouncil && dotnet build -c Release` (only the CS0108 warning in Decrees.cs is expected)
- DefValidator: build it in your worktree if needed (`cd Tools/DefValidator && dotnet build -c Release`), then
  `dotnet Tools/DefValidator/bin/Release/net8.0/DefValidator.dll . ~/.nuget/packages/krafs.rimworld.ref/1.6.4871/ref/net472 1.6/Assemblies/ColonyCouncil.dll` (0 errors)
- `cd Tools/Tests && timeout 900 dotnet run -c Release` ("All tests passed")
- `python3 Tools/labelcheck.py`, `python3 Tools/almanaccheck.py --base HEAD`, `python3 Tools/save_estimate.py --strict`,
  `python3 Tools/interaction_audit.py --open`, `python3 Tools/playout_audit.py` (0 candidates), `python3 Tools/poolcheck.py` ("OK")
- `timeout 600 dotnet run -c Release --project Tools/ApiDiff -- 1.6/Assemblies/ColonyCouncil.dll 1.6.4871 1.7.4977-alpha.2`
  ("Nothing the DLL uses has changed")

**Git:** before the final checks, merge the lead branch (named in the brief) into yours (resolve
`docs/HANDOFF.md` by keeping both sides' entries; for `1.6/Assemblies/ColonyCouncil.dll` rebuild). Commit on your worktree
branch with plain messages ending with the attribution lines the lead gives you (the session's Co-Authored-By and
Claude-Session lines).
Commit your work as you go, a work-in-progress commit after each item or every hour of work: the container can restart
and stop you, and only what is committed on your branch survives it (20 November 2026, two builds' work was saved only
because it happened to be on disk).
Don't push. Leave a clean tree.

**Report:** what you built and where (a short code map); anything you weren't sure of or decided yourself; what can't
be checked without the game; the check results; the branch and last commit; 2 to 6 proposed What's new points (no em
dashes, no exclamation marks, plain words); mods that would help, with Workshop links and whether they're on 1.6/1.7.
