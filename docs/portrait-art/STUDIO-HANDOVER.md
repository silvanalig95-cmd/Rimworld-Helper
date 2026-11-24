# Rimfolk Portrait Studio: the handover

Written 23 November 2026 for a new project: one program that runs and watches the whole portrait workflow. Everything a
new session needs to start is here or linked from here. The workflow as it is today, step by step, is in `GUIDE.md`
(same folder); the ComfyUI setup in `SETUP-ComfyUI.md`. When a file format or a step below changes in this repository,
this document changes with it.

## 0. Keeping the two in step (read this first on every sync)

The player, 24 November 2026: "how do I keep everything synced up and not update one aspect and then the other one
doesn't work anymore". The two projects and who owns what:

- **This repository** (silvanalig95-cmd/rimworld-helper, default branch `claude/charming-pasteur-9qbjle`) owns the game
  mod and everything the game reads or writes: the file formats (section 4), the kit's scripts (`Tools/PortraitArt`),
  the lists and catalogues (`docs/portrait-art`), and the portrait code the Studio copies. A format changes here first.
- **Rimfolk Studio** (silvanalig95-cmd/rimfolk-studio, private) owns the program. It follows this repository: it copies
  files with its `tools/sync_mod_code.py`, runs the kit's scripts, and never changes a format or pushes here. It asks
  for a change here through the player.
- **The routine.** Every change here that touches the portraits (a format, a script, a list or style, a copied file,
  `CreaturePortraitMath.cs` and the other `*Math.cs` under `UI/Faces`) adds a line to the list below with the version,
  and the reply to the player ends with one line for the Studio. The player pastes it into the Studio's chat, which
  syncs (`python3 tools/sync_mod_code.py --from <checkout of this repository>`), reads the list below from its last
  synced commit (`src/Mod/MOD-SOURCE.json`), and adapts. A change the Studio needs from here goes the other way: its chat
  writes the wish, the player pastes it here, it goes on the to-do list in `docs/HANDOFF.md`.
- Nothing here depends on the Studio: the scripts keep working on their own, so a change here never waits for it.
- **Where a request belongs** (the player, 24 November 2026: "whenever I post something here or ask you for something
  that should actually be posted in the chat with the studio, you should always recognize that and then instead give
  me a prompt to provide the studio chat, and also the other way around"). Each chat builds only its own part; for the
  other part it tells the player so and gives a ready prompt to paste into the other chat:

  | Belongs in the Rimfolk chat (this repository) | Belongs in the Studio chat |
  |---|---|
  | the game: which picture a pawn, creature or place gets, how pictures are shown, corrections and deletions in the game, what the game writes (missing.csv, variants-wanted.csv, deleted.csv) | the program: its pages, tasks, scans, sync, storage, clean-up |
  | the file formats (section 4), and a new kind of picture or record field | painting, copying, checking, reviewing, sorting and organising pictures |
  | what to paint: the catalogues (kinds, creatures, looks), the planners and the batch lists, the styles' wording | ComfyUI and its workflows as the program runs them |
  | the content the pictures are for (gods, peoples, events) | the kit's scripts (developed in the Studio from 24 November 2026, once it has taken them over) |

  A request with parts on both sides: each chat does its own part and hands the rest over as a prompt.

### Changes since the handover (newest first)

- **Rimfolk 0.303.1 (24 November 2026): pointed ears are fine for the fantasy races.** The player: "for all the fantasy
  races, it's totally fine. They anyways look unusual ... But for humans, of course, or anything that's really close to
  humans, they should not have pointy ears ... unless the pawn for some reason has its xenotype on pointy ears."
  - `catalogue/kinds.csv` has a new column `humanLooking` (yes or no; after `farFromHuman`): 16 kinds look human, the
    other 66 are fantasy races.
  - The game: a picture with the feature `pointedEars` never stands for someone who looks human and hasn't got pointed
    ears from their genes (a new hard rule, `PortraitLibraryMath.EarsAgree`, also in `Shows`); for a fantasy race it
    costs nothing (`Parts`). Copied file changed: `PortraitLibraryMath.cs` (`HumanLookingKinds`, `HumanLooking`,
    `EarsAgree`, `HardRules.ears`, `PortraitLook.ownXenotype`).
  - For the Studio's check and review: pointed ears on a picture of a fantasy race are no reason to leave it out (keep
    it, with `features add:pointedEars`); on a human-looking kind they still are, unless the row's kind has pointed ears.
- **The Pokémon out of the creature lists (24 November 2026):** `creatures-01` lost 24 rows and `creatures-02` 6, all
  of the catalogue's group `pokemon` (PokeWorld); every other row is unchanged, names included. `plan_creatures.py` has
  `--without` (default `pokemon`): those groups get no rows and are taken out of the lists. The catalogue keeps them.
  Nothing for the Studio to change (the player): it reads the lists as they are.
- **Rimfolk 0.303.0 (24 November 2026): the gods' pictures.**
  - records.xml: `ColonyCouncil.BackdropDef` has a new, optional field `god`: the domain of the god whose place it shows
    (`harvest storm hunt healing war craft way dead night hearth wild waters machine void`). The game shows such a
    backdrop only behind someone praying at a god's place of that domain or standing in its temple, and never elsewhere
    (`BackdropMath.GodDomains`, `BackdropMath.Choose`; `BackdropRecord.god`, `BackdropWhere.god`). An unknown word is a
    problem line of the record.
  - A creature record's `race` may now name a god's own pictures: `god:<Key>:<age>`, `god:<Key>`, `god:<domain>:<age>`,
    `god:<domain>` (a god walking the map, shown as itself) and `godbeast:<Key>` (the beasts that walk with it); the
    game tries them in that order (`CreaturePortraitMath.GodKeys`, `BeastKeys`, `FirstCandidates`). Ages are
    `tribal medieval industrial spacer`. The Studio should not check these against the creature catalogue.
  - New lists from the new `Tools/PortraitArt/plan_gods.py` (with `test_gods.py`): `backdrops-gods` (56 rows, the
    backdrop columns plus `god`; paint in `style-rimworld-backdrops`), `gods-01` (92 rows, the creature columns,
    `creature` holding the keys above; painted in the creatures' style by itself), `priests-01` (64 rows, the people
    columns, role `priest`, every age's dress; a people's style).
  - Update-PortraitLibrary writes a backdrop's `god` from its row. Copied files changed: `BackdropMath.cs`,
    `CreaturePortraitMath.cs`.
  - The game reads the colony's own priest (the council's seat) as role `priest` for the matcher.
- **Rimfolk 0.302.1 (23 November 2026):** nothing for the kit or the Studio (the peoples' leaders and log errors).
- **Rimfolk 0.302.0 (23 November 2026): animals and creatures.**
  - records.xml has a third element, `ColonyCouncil.CreaturePortraitDef` (`defName`, `texPath`, `race`, `sex` any, male
    or female, `age` baby, juvenile or adult, `character`). `PortraitFolderMath.Parse` takes a fifth, optional list for
    them; without it they are passed over quietly (no problem line). `PortraitDeleteMath.WithoutRecords` takes them out
    too. New pure file `Source/ColonyCouncil/UI/Faces/CreaturePortraitMath.cs` (the record, the matching): add it to
    the Studio's `FILES`.
  - New lists `creatures-01` to `04` (columns `name,subject,pose,count,seed,creature,sex,age,mod`), from
    `catalogue/creatures.csv` by `Tools/PortraitArt/plan_creatures.py` (`creature_census.py` keeps the catalogue).
  - New style `style-rimworld-creatures.json` (1024 x 1024, no face fix) with a new field `"for": "creatures"`.
    Run-PortraitBatch swaps a people's style for it on a list with a `creature` column, and notes a creatures style
    chosen for people.
  - Update-PortraitLibrary writes a creature record for a row with `creature` (`Creature_<list>_<row>_<run>_<NN>`;
    corrections apply, only `out`, `sex` and `age` mean anything; no variants or depth maps).
  - Check-Pictures leaves creatures out (a creature record, or a folder `creatures-NN`) and says how many.
  - missing.csv: `what` can be `creature` (kind = the race's def name, with its age and sex).
  - The game's save keeps the pictures chosen by hand for creatures (`PortraitKeeper.creaturePicks`).
- **Characters lists, third round (23 November 2026):** `characters-08` to `16`, 1,632 rows, the same columns as
  before; one picture a row.

## 1. What the player asked for

The player, 23 November 2026:
- "at this point, I would really love to have like one UI for the whole software. So I don't know if it's best if we
  transport this into a separate GitHub, but then I have a UI to manage and coordinate all of these scripts and
  decisions. So like one unified UI with like especially visual buttons and representations of kind of what we're
  doing, all the steps required, because then I can much more effectively handle it. And you know, if I have like all
  these tables, table views or something similar for all the files and whether they're in sync or not and whether what
  attributes they have, like a database. So really combine all this workflow and that I don't have to run and update
  individual scripts at the same time."
- "a kind of this unified program that really monitors and does like quality check and lets me know if things are
  missing or if I deleted stuff or what do I need to copy where and where is it missing still"
- "I can say where all my folders are. It scans for folders all the time. It allows me to sync between the different
  locations, give tasks, monitor the progress of tasks, perhaps clean up, see my storage, all these things."
- On the guide: "keeps the whole step-by-step procedure on track and documented with what it does ... when it changes,
  please update it."
- Earlier the same day, things that went wrong and that the program should prevent or show: the batch lists the
  pictures were made from were deleted while tidying up ("I've deleted all the older batches lists inbetween to celan up
  the folders... didn't know that I would still need them"), so the library lost most of its records; "i don't fully
  remember which of the batches i've already imported with the new style ... some were partial, some perhaps complete,
  some twice etc. it's a bit of a mess"; a save loaded during that time gave every colonist a worse picture; the
  picture check's misreadings left about a quarter of the library out of the game, found only by reading the update's
  console output.

## 2. The player's set-up

- Windows, two computers: a PC with the graphics card (ComfyUI runs there) and a laptop; RimWorld 1.7 on both.
- The kit folder `Rimfolk-portraits` lives on the PC (`H:\Comfy AI Images\Rimfolk-portraits`) and is synced by Proton
  Drive, which shows it on the laptop under "Other computers"; Proton Drive fetches files on demand, so a scan must not
  read every file's content.
- ComfyUI Desktop on the PC; its output folder `ComfyUI-Shared\output\Rimfolk` (the exact path is in
  `copy-settings.json` next to `Copy-ToLibrary.ps1`). ComfyUI's API at `http://127.0.0.1:8000` (Desktop) or `:8188`.
- Models the player installed: FLUX.1 Dev with their own style LoRA `rmwstyle.safetensors` and `SameFace_Fix.safetensors`;
  a background remover; Qwen3.5 4B for the check; Depth Anything 3 (optional); FLUX.1 Kontext Dev (optional).
- In RimWorld, Rimfolk's setting "Portrait library folder" points at `Rimfolk-portraits\library`.

## 3. The steps today

See `GUIDE.md`, "Each round, step by step". In one line each:

| Step | Script (in `Tools/PortraitArt` here) | Reads | Writes |
|---|---|---|---|
| Paint | `Run-PortraitBatch.ps1` | `batches\<list>.csv`, `batches\style-*.json`, `rimfolk-workflow-api.json` | ComfyUI queue; `output\Rimfolk\<list>\*.png`; `logs\` |
| Copy in | `Copy-ToLibrary.ps1` (in ComfyUI's `output\Rimfolk`) | the output folder, `library\deleted.csv` | `library\<list>\*.png`; runs Update |
| Records | `Update-PortraitLibrary.ps1` | `batches\*.csv`, the library's pictures, `corrections.csv`, `deleted.csv` | `library\records.xml` |
| Check | `Check-Pictures.ps1` | the library, `checked.csv`, `rimfolk-check-workflow-api.json` | `checked.csv`, `check-report.csv`, `corrections.csv`, an example zip; offers Update |
| Review | `Review-Pictures.ps1` (a page on localhost) | `corrections.csv`, `check-report.csv` | `corrections.csv`; offers Update |
| Depth maps | `Make-DepthMaps.ps1` | backdrops, `rimfolk-depth-workflow-api.json` | `<backdrop>_depth.png`; runs Update |
| Variants | `Make-Variants.ps1` | `variants-wanted.csv`, `rimfolk-variant-workflow-api.json` | `<picture>_v-<word>.png`; runs Update |
| Training set | `Make-TrainingSet.ps1` | `training\`, `logs\` | `training\set\` |

Animals and creatures (the lists `creatures-NN`, since 23 November 2026) go the same way: painted in
`style-rimworld-creatures` (picked by itself for such a list), copied in as cut-outs, recorded as
`ColonyCouncil.CreaturePortraitDef`; the check and the variants leave them out. The gods' pictures (24 November 2026)
too: `gods-01` as creatures (the gods walking the map and their beasts), `backdrops-gods` as backdrops (the gods'
places), `priests-01` as people.

Each `.ps1` has a `.cmd` to double-click (Run-PortraitBatch is run with right-click, Run with PowerShell). They are
Windows PowerShell 5.1 scripts; they also run under PowerShell 7 (tested here on Linux, except what needs Windows: the
side check's pixel reading).

## 4. The files and their formats (the contracts)

All CSV files are UTF-8 with a byte-order mark, CRLF, comma-separated, quoted where needed; a file saved again by Excel
may use semicolons (every reader here accepts both). Paths inside files use backslashes.

**Batch lists** `batches\<list>.csv` (people): `name,subject,pose,count,seed,kind,race,sex,age,skin,hairColour,hair,beard,
build,era,dress,features,marks,looks,role,facing,pawn`. `name` is the row's key (`p_baseliner_f_adult_char_veryfair_auburn_01`);
`subject` (+ `pose`) is the prompt; the rest is the picture's description (kind = xenotype def name, `Baseliner` for
none; race = race def names, empty for human; sex `male`/`female`; age `baby toddler child teen young adult middle old
elder`; skin and hairColour `#rrggbb`; hair `bald short medium long braids bun tied`; beard `none stubble short full
moustache`; build `average slim heavy broad`; era `tribal medieval industrial spacer`; dress `rags work fine robes
armour`; features, marks and looks `;`-separated words; role one word; pawn = a colonist's name for a picture made for
them). Backdrops: `name,subject,count,seed,place,rooms,outdoors,era,contents`, and for the gods' places (`backdrops-gods`)
`god` (the god's domain). Animals and creatures (`creatures-NN`, and the gods' `gods-NN` whose `creature` holds a god's
key, `god:<Key>:<age>` ... `godbeast:<Key>`):
`name,subject,pose,count,seed,creature,sex,age,mod` (creature = the race's def name; sex `male`/`female`/`any`; age
`baby`/`adult`, a juvenile taking an adult's picture; the row's name `c_<creature>_<age>_<m, f or a>_<NN>`). Written by
the lead from `docs/portrait-art/catalogue/` and `Tools/PortraitArt/plan_batches.py` (people), `plan_creatures.py`
(creatures, from `catalogue/creatures.csv`, kept by `creature_census.py`); a list must be kept as long as pictures made
from it exist.

**Styles** `batches\style-*.json`: `label, style, frame, style_end, negative, width, height, count, guidance, steps,
sampler_name, scheduler, loras[{name, strength}]`, optional `cut` (false for backdrops) and `for` (`creatures` for the
creatures' style; Run-PortraitBatch swaps a people's style for `style-rimworld-creatures` on a list with a `creature`
column). The prompt is style + subject +
pose + frame + style_end; the LoRA boxes of the workflow are filled in their node-id order, spare boxes get strength 0.

**Picture files**: `<row name>_<style name>_<yyMMdd-HHmmss>_<NN>[_cut]_<counter>_.png` (ComfyUI's naming:
`^(?<name>.+?)_(?<style>style[^_]*)_(?:(?<run>\d{6}-\d{4,6})_)?(?<n>\d+)(?<cut>_cut)?(_\d+_?)?$`). A Windows copy name
(` (2)`, ` - Copy`) still counts as its picture. Variants: `<picture stem>_v-<word>[-N].png` (words: `blink smile frown
pain lostEyeLeft lostEyeRight eyePatchLeft eyePatchRight bionicEyeLeft bionicEyeRight scar burn missingEarLeft
missingEarRight artificialArmLeft artificialArmRight`, `-2`, `-3` for one made again after a deleted one), nested for a
mark variant's expressions (`X_v-lostEyeLeft_v-blink.png`); depth maps `<backdrop stem>_depth.png`.

**records.xml** (`library\`, written by Update-PortraitLibrary, read by the game): `<Defs>` of
`<ColonyCouncil.PortraitDef>` with `defName` (`Portrait_<list>_<row>_<run digits>_<NN>`), `texPath` (`<list>/<stem>`),
`variant`, `variantOf`, `pawn`, `xenotypes`, `races`, `sex`, `age`, `skinColor`, `hairColor`, `hair`, `beard`, `build`,
`era`, `dress`, `features`, `marks`, `side`, `looks`, `role`, `headwear`, `facing`, `character`, and
`<ColonyCouncil.BackdropDef>` with `defName`, `texPath`, `depthPath`, `place`, `rooms`, `outdoors`, `era`, `contents`, `god`
(a god's place: its domain; optional), and
`<ColonyCouncil.CreaturePortraitDef>` (an animal or creature) with `defName` (`Creature_<list>_<row>_<run digits>_<NN>`),
`texPath`, `race`, `sex` (`male`/`female`/`any`), `age` (`baby`/`juvenile`/`adult`), `character`. The game's parser:
`PortraitFolderMath.Parse`, `PortraitLibraryMath.Record`, `CreaturePortraitMath.Record`. The picture check leaves
creatures out (its form is about a person).

**corrections.csv**: `picture,field,value,apply,why`. `picture` = `<list>\<file>.png` (or the file alone). Fields: `out`
(the picture left out), `race`, `xenotype` (alias `kind`), `sex`, `age`, `skin`, `hairColour`, `hair`, `beard`, `build`,
`era`, `dress`, `headwear` (`true`/`false`), `role` (`clear`), `side` (`left`/`right`/`clear`), and `looks`, `marks`,
`features` with `add:word`/`remove:word`. `apply`: `yes` and `ask` are applied (ask = the check was less sure), `hold` is
applied as out until the player keeps it, `no` is never applied. `why` = the reason; `set in the game` marks the
player's own corrections from the game, which win over every other line for the same picture and field (for list fields
the same word); `kept as the model saw it (Review-Pictures)` marks features the player accepted on the review page
(these pictures go first if the library is ever cleaned up). Logic in C#: `PortraitCorrectMath` (Apply, Fixes, Append,
WithRecords, ApplyGameLines); in PowerShell: Update-PortraitLibrary's Apply-Corrections and Game-Last.

**checked.csv**: `picture,size,versions,checked,model,<form keys>,answer`; form keys `people sex age skin hair beard
ears horns tail wings eyes body eyemarks arms limbs hands weapons armour magic headwear tools unusual`; `versions` like
`people=3 sex=3 ... limbs=1` (a line is asked again when its version rises); `answer` the model's raw text.
**check-report.csv**: `picture,row,check,what is wrong,what the model saw,its row says,suggested fix,in corrections.csv`
(`check` = `1 wrong`, `2 shows more`, `3 shows less`, `4 not compared`, `5 set in the game`). What the check can change:
`GUIDE.md`, "What the check checks".

**Written by the game** (Rimfolk, into `library\`): `deleted.csv` `file,batch,row,record,date,how,sha256` (deleted for
good: never copied or recorded again); `variants-wanted.csv` `picture,word,instruction,who,priority,first` (on save);
`missing.csv` `what,kind,look,role,mod,sex,age,skin,hair,era,why,used instead,how many,last seen` (on save; `what` is
`person`, `look`, `creature` (kind = its race), `room` or `outdoors`); corrections
lines `set in the game` and records.xml fields (the About window); the kept-for-training copies in `training\` with
`training.csv` `file,batch,record,date`. The game's picks per pawn live in the save (`ColonyCouncil.PortraitKeeper`:
`id, picture, character, kind, words, stage, pinned, byHand, reserved, since, distance, library, seen`; for animals and
creatures only the pictures chosen by hand, `creaturePicks` id to picture: the others get theirs by their id each time).

**Settings** next to the scripts: `check-settings.json` `{ "server": "<ComfyUI url>" }`; `copy-settings.json` (the kit
folder). Workflows exported in ComfyUI's API format: `rimfolk-workflow-api.json` (prompt box `%PROMPT%`, negative
`%NEGATIVE%`), `rimfolk-backgrounds-workflow-api.json`, `rimfolk-check-workflow-api.json` (`%QUESTION%`),
`rimfolk-depth-workflow-api.json`, `rimfolk-variant-workflow-api.json` (`%INSTRUCTION%`).

## 5. ComfyUI, as the scripts use it

`GET /system_stats` (alive), `POST /upload/image` (multipart; the check and the variants upload into
`input\rimfolk-check` and `input\rimfolk-variant` under names of their own), `POST /prompt` (`{prompt, client_id}`),
`GET /queue` (running and pending), `GET /history/<id>` (outputs: images, or the text of a Preview as Text box),
`POST /queue {delete: [ids]}` (only their own jobs, on Ctrl+C or an error). Every job of a run is queued at once, so a
batch queued meanwhile waits behind them; jobs ahead are waited for however long; 15 minutes running on one job is the
limit. The cut-out: a Save Image box fed by the background remover (or titled "cut"). A program can follow progress
over ComfyUI's websocket (`/ws?clientId=`).

## 6. What the program should do (the player's wishes, made concrete)

- **Folders:** the player names their locations once (kit folder on the PC, the same through Proton Drive on the
  laptop, ComfyUI's output, RimWorld's saves and Player.log); it finds the rest and watches them (a scan on start and on
  change, by names, sizes and dates, never reading on-demand files whole).
- **Overview:** the steps as a visual pipeline with counts and a state per step (to do, running, done, needs you): new
  pictures in the output not yet copied, pictures not checked, held back, left out, records out of date, variants
  wanted, depth maps missing.
- **Tables like a database:** lists and rows (with how many pictures each row has, by style and run), pictures (file,
  row, description, corrections, check state, in the records or not and why), records, corrections, checked answers,
  deleted, variants wanted, missing; filters, search, the picture beside its row.
- **Problems, said plainly:** a picture whose row is missing (and the list it came from), a list deleted that pictures
  still need, rows never painted, pictures twice, a picture in the records but not on disk, records older than the
  corrections, a correction that leaves out many pictures, a location out of sync, ComfyUI not reachable, a workflow not
  exported.
- **Tasks:** each step from a button, queued and shown with live progress (ComfyUI's queue and the step's own log), one
  after another without the player starting scripts; a history of what ran.
- **Sync and copy:** what is where, what to copy where, done on a click and shown first.
- **Review and corrections:** the review page's choices (Keep, Leave out, Keep as the model saw it, and for many at
  once), and editing a picture's description with the same rules as the game's About window.
- **Clean-up and storage:** sizes per folder and kind; suggestions to free space (ComfyUI's output already copied,
  `Rimfolk-extras`, old example zips; the pictures kept as the model saw them first when pictures must go), to the bin.
- **Later:** "which picture would this pawn get" for a save's pawns, with the game's own matcher.

## 7. What can be reused

- **The game's own rules, in C#, without the game:** `Source/ColonyCouncil/UI/Faces/` `PortraitLibraryMath.cs` (records,
  the matcher, hard rules, distances), `PortraitFolderMath.cs` (records.xml, missing.csv, file keys), `PortraitCorrectMath.cs`
  (corrections), `PortraitDeleteMath.cs` (deleted.csv, names), `PortraitVariantsMath.cs` (variants-wanted.csv),
  `PortraitTrainingMath.cs`, `PortraitPictureMath.cs` (scaling, the head measured), `PortraitLookMath.cs` (looks and roles),
  `PortraitLifeMath.cs`, `PortraitViewMath.cs`, `PortraitComposeMath.cs`, `BackdropMath.cs`, `FaceMath.cs`, `FacePoseMath.cs`,
  `PortraitEffectsMath.cs`. They build without RimWorld (see how `Tools/Tests/Tests.csproj` links them); `Tools/Tests`
  exercises them. Using them, the program judges pictures exactly as the game does.
- **The scripts** (`Tools/PortraitArt/*.ps1`): run as tasks at first, ported step by step. The check's readers of the
  model's answers live in Check-Pictures.ps1 ("The comparison" and the readers above it); its tests in this session's
  harnesses were stand-in ComfyUI servers (a small Python HTTP server returning canned answers).
- **The catalogue** `docs/portrait-art/catalogue/` (kinds, looks, marks, roles of the player's mods) and the planner
  `Tools/PortraitArt/plan_batches.py`.
- **The analysis of the player's check answers**: `docs/portrait-art/CHECK-ANSWERS-2026-11-23.md` (the check's known
  misreadings, waiting for the player's go to fix in the kit).

## 8. A suggested start (the new project decides with the player)

- A Windows program the player starts with a double-click: .NET 8, a local web interface (ASP.NET Core on localhost,
  opened in the browser or a WebView2 window), published as one self-contained `.exe`; SQLite as its index of files and
  rows; the pure C# above linked from this repository (a git submodule or a copy kept in step by a script); a task runner
  for the steps; a ComfyUI client. Testable without Windows for everything but the Windows-only parts.
- Phases: (1) set up the folders, scan, the overview, the tables and the problems, read only; (2) the steps as tasks with
  progress, the scripts wrapped; (3) sync, copy and clean-up; (4) the steps and the review page native, corrections
  editing, the pawn matcher.
- The kit's scripts keep working throughout; the program never needs to be running for the game.

## 9. How to work with the player

- Plain, neutral text in the interface and messages: say what happens, who does it and why, in ordinary words; no
  poetic turns (see this repository's CLAUDE.md, "Plain, neutral text").
- New wishes go on a to-do list first with the player's own words, and are built when the player says so; the player's
  own explicit requests can go ahead.
- The player's corrections always win over anything automatic.
- Name tools or mods that would help, with links, when they would.
- Keep a step-by-step guide for the program up to date with every change, as `GUIDE.md` is for the kit.
- No model names in commits or code; keep the user's personal details out of anything sent to outside services.

## 10. The prompt for the new project

```
I want to build "Rimfolk Portrait Studio": one Windows program with a visual interface that runs, watches and checks
my whole portrait workflow for my RimWorld mod Rimfolk, so I no longer run and update separate scripts by hand.

Please add the repository silvanalig95-cmd/rimworld-helper to this session (read only, for reference) and read
docs/portrait-art/STUDIO-HANDOVER.md first: it describes what I want in my own words, my set-up (two Windows computers,
the kit folder synced by Proton Drive, ComfyUI on the PC), every step and script, every file format, ComfyUI's API as
the scripts use it, and the C# code from the mod that can be reused so the program judges pictures exactly as the game
does. docs/portrait-art/GUIDE.md is the workflow step by step as it is today; the scripts are in Tools/PortraitArt.

What I want, in short: I name my folders once and the program scans them all the time; a visual overview of all the
steps with buttons; table views like a database of every file (lists and rows, pictures, records, corrections, checked
answers, deleted pictures, wanted variants, missing pictures) and whether they are in sync; plain warnings when things
are missing, deleted or out of date, and what to copy where; tasks I start with a button and whose progress I can
follow (ComfyUI's queue too); syncing between my locations; clean-up and a view of my storage; the review of held-back
and left-out pictures built in.

Please: first propose the architecture and a phased plan with the decisions I need to make (the handover suggests
.NET 8 with a local web interface, one .exe, SQLite and the mod's pure C# linked in) and wait for my go. Then build
phase by phase with tests, keeping my existing scripts working the whole time, and keep a step-by-step guide for the
program up to date with every change. Write all text in the program plainly and neutrally. When you need my folder
paths or a decision, ask.
```
