# Rimfolk: shared brief for content agents

Rimfolk is a RimWorld mod (C#, Harmony, XML defs) in this repository. It is played on RimWorld 1.7 and compiled against the 1.6 reference assemblies. You work in your own git worktree, on a branch of `claude/charming-pasteur-9qbjle`.

- Commit often, with clear messages. Do NOT push.
- Rebuild the DLL in your LAST commit.
- Every commit message ends with these two lines:

```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01BPumbqQfw465PjV9qCCwHZ
```

- Never put model names in code, docs or commits.
- Other agents work in parallel on other areas. Stay inside your area's files, and add NEW files rather than editing shared ones where you can.
- If you must touch a shared file (README.md, ROADMAP.md, CouncilSettings.cs, Tools/Tests/Program.cs, Tools/Tests/Tests.csproj), keep the edits small and local.

## Design rules (from the player, binding)

- **Soft integrations only.** Harmony is the only hard dependency. Rimfolk must never break if another mod or a DLC is missing:
  - check `ModsConfig.IsActive`, or look defs up with `GetNamedSilentFail`;
  - use `MayRequire` / `MayRequireAnyOf` on def references to DLC or mod content.
- **Only content the colony has unlocked.** Era, research and tech level must fit. Look at how existing workers check this.
- **Nobody is hurt for flavour.** The exception is the deliberately rare serious-illness tier.
- Not wanted:
  - weapon duels
  - Diplomatic Marriage
  - Factional War
- **What the player values most right now:** more content and deeper improvement of what is there: variety, options, flavour, immersion, and systems that visibly play out and touch each other. Quality matters: each new thing should feel like a small story, fit RimWorld's tone, and have proper text, consequences and Almanac coverage where its system has Almanac pages.
- **No filler (the player's rule, refined).** "I'd rather not have further wishes etc be added just for the sake of variety, if they don't fit naturally into the game's goals." Every wish, goal, project, dilemma, ailment, day or trait you add must point at something a player would genuinely want to pursue or deal with in a run. The player later added: a wish they can ignore at a mood cost is welcome when it makes the game more dynamic. New wishes are proposed to the player as a list first; only add the ones your task names (they were picked by the player). Deepening how existing things play out beats adding new things to want.
- **Leave "What's new" to the lead.** Don't edit `Defs/AlmanacArticleDefs/Almanac_WhatsNew.xml` or `CHANGELOG.md`; list in your final report the 1-3 points you'd put there.
- **Balance matters too.** The player found moods too easy. Not every good thing should be a mood:
  - a small good moment should usually give something else (`ColonyCouncil.RewardInstead` on the ThoughtDef: recreation, rest, practice, trust, an inspiration chance, a feeling, a journal line);
  - new bad outcomes and setbacks are welcome;
  - keep big good moods rare.
- **Prose style:** plain, warm, concrete, in the style of the existing text. No exclamation-mark spam, no stock phrases, British/neutral spelling as in the existing files.

## Learn the system before adding to it

Before writing defs, read:

- 2–3 existing def files of the kind you add
- their Def class (search `class XxxDef` in `Source/ColonyCouncil`)
- the worker classes

Reuse existing generic workers wherever possible: data-driven defs beat new code. Only write new C# when a new thing really needs new behaviour, and keep it defensive:

- null checks
- try/catch with `CouncilLog.ErrorOnce` around anything that touches other mods
- no per-tick heavy work

## Validation (all must pass before your final commit)

1. `cd Source/ColonyCouncil && dotnet build -c Release`
   - 0 errors.
   - Only the existing CS0108 warning in Decrees.cs is allowed.
   - This rebuilds `1.6/Assemblies/ColonyCouncil.dll`.
2. Validate the defs:
   - `cd Tools/DefValidator && dotnet build -c Release`
   - then, from the repo root: `dotnet Tools/DefValidator/bin/Release/net8.0/DefValidator.dll . ~/.nuget/packages/krafs.rimworld.ref/1.6.4871/ref/net472 1.6/Assemblies/ColonyCouncil.dll`
   - 0 errors.
   - Warnings about vanilla defs not in a DefOf are normal; check that each name you reference really exists in vanilla or is guarded.
3. `cd Tools/Tests && dotnet run -c Release`: all tests pass.
4. **1.7 label rule:** any def whose `<label>` contains `[`, `]`, `{` or `}` needs `<ignoreIllegalLabelCharacterConfigError>true</ignoreIllegalLabelCharacterConfigError>`. Check with `python3 Tools/labelcheck.py`; it must print 0.
5. Every new translation key you use must exist in `Languages/English/Keyed/`. The DefValidator checks keys used from C#.

## Conventions from recent rounds (follow them)

- **Balance simulator.** If you add or change moods, ailments, questions or their rates, run `dotnet run -c Release --project Tools/Balance -- . --before Tools/Balance/before.json` from the repo root before and after. Rimfolk's net mood must stay inside the targets in DESIGN.md ("Balance targets"). Do not commit a changed `before.json`.
- **Questions keep apart.** A new letter that asks the player something and can wait must check `LetterGap.Open` (see `Lean/LetterGap.cs` and how `Dilemmas.cs` uses it).
- **Answers play out on the map.** When an answer says people do something (eat together, visit a grave, hold a vigil, teach), use the play-out system in `Source/ColonyCouncil/PlayOut/`. Look at how dilemmas declare play-out effects; the Almanac page "answers on the map" explains it.
- **Colonists talk only when near.** A conversation or social log entry between two pawns needs `SocialLog.Near(a, b)`.
- **Copy shared pawn lists.** Loop over `PawnsFinder.*` lists with `.ToList()` if the body might query them again.
- **This week.** If your work adds something the player must answer, or something coming up, check that `UI/WeekModel.cs` lists it, or add it there.
- **Systems talk to each other.** `Source/ColonyCouncil/Interplay/` shows how links between systems are made. New links go behind the "Systems talk to each other" setting, and strain relief goes through `Interplay.Ease` so it counts in the heart's daily caps.
- **Mood hints.** New negative thoughts get a mood hint (MoodHintDefs) saying what helps.

- **Speech goes through one helper.** Every line a pawn says goes through `Speech.Say(pawn, line, other)` (`Immersion/Speech.cs`); never throw text motes or write to the play log yourself (a test checks this). Two-person moments in the social log use `Speech.Logged`.
- **Interactions need people together.** Anything where two colonists interact either waits until they are really together (`Social/Together.cs`: `Together.Can`, `Together.When`) or is made visible with a walk-over talk. Things that rightly happen apart (council decisions, letters) carry an `// apart:` comment saying why; `python3 Tools/interaction_audit.py --open` must list nothing open.
- **Beliefs and diets.** New wishes, life goals, council projects and anything that hands out food, drink, drugs, beds or lovin must respect the pawn's faith, traits and policies: use `Beliefs.Forbids`, `Beliefs.MindsEating`, `Beliefs.MindsDrug` and the `forbiddenBy` lists (`Wishes/Beliefs.cs`, `Wishes/BeliefMath.cs`). Anything that serves food uses the feast's serving rules.
- **Buildings still going up don't count.** Count buildings with `Avail.ColonyCount` / `ColonyHas` / `ColonyBuildings` (frames are skipped there), never by scanning `allBuildingsColonist` by name yourself.
- **Offices need the skill.** `OfficeDef.CanHold(pawn)`: a new office must list the skills its work needs, and only those who can hold it stand.
- **Lights.** Lights in rooms go through `WallLights` / `LightMath` (`Building/`): wall lamps on walls in small and medium rooms.
- **No countdowns.** Events and questions come unannounced; don't show "next X in" timers.

## Useful tools

(The inspector, the decompiled stubs and the other mods' sources below lived in an earlier session's scratchpad; a new session has to fetch or rebuild them.)


- **Vanilla 1.6 API member inspector:** `cd /tmp/claude-0/-home-user-Rimworld-Helper/fcfbed79-3623-5e83-ac50-ef9782a7e41c/scratchpad/ws_inspect && dotnet run -c Release -- Full.TypeName filter`
- **Decompiled reference stubs (signatures only):** `/tmp/claude-0/-home-user-Rimworld-Helper/fcfbed79-3623-5e83-ac50-ef9782a7e41c/scratchpad/decomp/ac/`
- **Many other mods' source, for vanilla def names and patterns:** `/tmp/claude-0/-home-user-Rimworld-Helper/fcfbed79-3623-5e83-ac50-ef9782a7e41c/scratchpad/repos*` and `.../D_repos`
- **Line-variety audit:** `python3 Tools/variety_report.py` (`--top`, `--filter`).
- `python3 Tools/almanaccheck.py` checks Almanac pages. Known old errors: the WorldEvents label/seeAlso and the GrowingSeason numbers.

## Docs

- Update the README section of your area: counts and a sentence or two on what is new.
- Update the Almanac pages that list your kind of content.
- Update the content counts table in ROADMAP.md ("Always: breadth grows with every round"): change only your rows.

## Final report

In your final message, report:

- what you added, with counts before → after
- any new code and the hooks it uses
- anything that needs checking in game
- the commit hashes
