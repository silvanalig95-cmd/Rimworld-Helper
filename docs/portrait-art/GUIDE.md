# Rimfolk portraits: the guide

Last changed: 27 November 2026 (0.306.1: no Pokémon in missing.csv; 0.306.0: a new picture that fits one of your people nearly exactly becomes theirs by itself; 0.305.0: dismiss a variant from the large portrait's menu, and a picture made for a name is free in a save where no one has that name; 0.304.0: painted conditions such as asleep or wounded, and the game's people list `people.csv`; before it the lists, styles and scripts come with Rimfolk Studio; before it the characters' fourth round, the creatures' variety round, pointed ears for the fantasy races, the Pokémon out of the creature lists, and 0.303.0: the gods' pictures). This guide is
kept up to date with every change to the portrait kit (the player, 23 November 2026: "keeps the whole step-by-step
procedure on track and documented with what it does ... when it changes, please update it"). It comes with every kit
zip. The setup of each ComfyUI workflow, click by click, is in `SETUP-ComfyUI.md`; training the style is in
`STYLE-RIMWORLD.md`.

## In short

You paint pictures in ComfyUI from batch lists. Each row of a list is one person: what to paint (the prompt) and what
the picture shows (its description: kind, sex, age, skin and so on). The pictures go into your library folder, where a
script writes `records.xml`, one description per picture. The picture check asks a vision model what it sees and
corrects a description where the picture plainly shows something else. The game reads `records.xml` and gives each
pawn the picture closest to them. Your corrections in the game always win.

## What you set up once

| What | Where | Guide |
|---|---|---|
| ComfyUI Desktop with FLUX.1 Dev | your PC | SETUP-ComfyUI.md, 1 and 2 |
| Your style LoRA `rmwstyle.safetensors` and `SameFace_Fix.safetensors` | ComfyUI `models\loras` | STYLE-RIMWORLD.md |
| `rimfolk-workflow-api.json`: people, with the cut-out (two LoRA boxes) | `Rimfolk-portraits` | SETUP 3 and 6 |
| `rimfolk-backgrounds-workflow-api.json`: backdrops, no cut-out | `Rimfolk-portraits` | SETUP 3 |
| `rimfolk-check-workflow-api.json`: the picture check, Qwen3.5 | `Rimfolk-portraits` | SETUP 8 |
| `rimfolk-depth-workflow-api.json`: depth maps, Depth Anything 3 (optional) | `Rimfolk-portraits` | SETUP 9 |
| `rimfolk-variant-workflow-api.json`: variants, FLUX.1 Kontext Dev (optional) | `Rimfolk-portraits` | SETUP 10 |
| `Copy-ToLibrary.ps1` and `.cmd` | ComfyUI `output\Rimfolk` | below, step 3 |
| In RimWorld: Rimfolk's settings, "Portrait library folder" | the path of `Rimfolk-portraits\library` | SETUP 7 |

The models, the LoRA files and the exported workflows are yours: the kit never has them and never overwrites them.

## The folders and files

`Rimfolk-portraits\` (the kit folder, kept on Proton Drive so the laptop and the PC share it):
- the scripts (`.ps1`, each with a `.cmd` to double-click; Run-PortraitBatch is started with right-click, Run with
  PowerShell), this guide, SETUP-ComfyUI.md, STYLE-RIMWORLD.md. Since 24 November 2026 the scripts come with Rimfolk
  Studio (its folder `kit`);
- `batches\`: the lists (`characters-01.csv` to `20`, `creatures-01.csv` to `08`, `gods-01.csv`, `backdrops-gods.csv`,
  `priests-01.csv`, `colonists.csv`, `backdrops.csv`, `style-test.csv`) and the styles (`style-rimworld-people.json`,
  `style-rimworld-creatures.json`, `style-rimworld-backdrops.json`). They come with Rimfolk Studio (0.8.0 on): its Lists
  page puts them here, and new lists and more rows are planned there. Keep every list you have made pictures from: a
  picture finds its description by its row's name, and without its row it is left out of the records;
- `library\`: one folder per list with the pictures, and the files below;
- `logs\` (every prompt and seed), `training\` (pictures kept for training).

In `library\`:

| File | Written by | What it holds |
|---|---|---|
| `<list>\<row name>_<style>_<date-time>_01_cut_00001_.png` | Copy-ToLibrary | a picture; the name ties it to its row |
| `<picture>_v-blink.png`, `_v-lostEyeLeft.png`, `_v-asleep.png` ... | Make-Variants | its variants (expressions, marks and conditions) |
| `records.xml` | Update-PortraitLibrary (and the game's corrections) | what the game reads: one description per picture (people, animals and creatures, backdrops) |
| `corrections.csv` | Check-Pictures, Review-Pictures, the game | changes to single pictures' descriptions |
| `checked.csv`, `check-report.csv` | Check-Pictures | the model's answers; what it found, worst first |
| `deleted.csv` | the game | pictures deleted for good: never copied or recorded again |
| `variants-wanted.csv`, `missing.csv` | the game, when you save | the variants it wants; what it needed and the library lacks |
| `people.csv` | the game, when you save | each of your people and each notable: the picture they hold, the game's words for them, what the picture doesn't show, what changed and since when (Rimfolk Studio reads it) |

## Each round, step by step

1. **Paint: Run-PortraitBatch.ps1** (right-click, Run with PowerShell; ComfyUI open). Pick lists (`1,2,3` for several)
   and a style (`style-rimworld-people` for people, `style-rimworld-backdrops` for backdrops; a `creatures` list is
   painted in `style-rimworld-creatures` by itself, whichever you pick). It hands every row to
   ComfyUI and offers to paint only the rows that have no picture yet, so a list half done is finished without doubles.
   Pictures land in ComfyUI's `output\Rimfolk\<list>\`, the people as cut-outs.
2. **Sort:** in `output\Rimfolk`, delete the pictures you don't want.
3. **Copy in: Copy-ToLibrary.cmd** (in `output\Rimfolk`). Copies the new cut-outs (and every backdrop) into
   `library\<list>\`, skipping what is there already and what you deleted for good in the game, then runs step 5 by
   itself.
4. **Check: Check-Pictures.cmd** (ComfyUI open, Qwen workflow exported). Animals and creatures are left out (the
   form is about a person): look through those yourself in step 2. Asks the model a fixed form about each new
   picture (and only a new or changed question about older ones), and looks at each cut-out for a figure cut off at the
   side. Compares the answers with each picture's row and writes `corrections.csv` and `check-report.csv` (what it
   checks and what it can change: below). Offers a zip of examples for the lead, then offers to run step 5. All its
   questions go into ComfyUI's queue at once; a batch queued meanwhile waits until the check is done.
5. **Write the records: Update-PortraitLibrary.ps1** (Copy-ToLibrary, Check-Pictures and Review-Pictures run it for you).
   For each picture in the library: finds its row by its name, takes the row's description, applies its corrections
   (yours from the game last, so they win), leaves out the pictures marked out or held back, adds its variants and
   depth map, and writes `records.xml`. It says how many corrections it used, how many pictures are left out or held
   back, and any picture without a row.
6. **Review: Review-Pictures.cmd.** Opens a page in your browser (on your own PC only) with the pictures held back or
   left out, one at a time, large, with why and what the model saw:
   - **Keep** (K): back in the game, as its row describes it.
   - **Leave out** (O): stays out.
   - **Keep as the model saw it** (S), for something about the body (pointed or long ears, horns, a tail, glowing eyes,
     scales, fur): back in the game with what the model saw added to its description. It still goes to people of its
     kind, those who have that feature first. (If the library is ever cleaned up, these go first.)
   - For many at once: **Keep all cut at the side** and **Keep all as the model saw it**. Each clears only its own kind
     of reason; a picture also out for something else (two people, limbs that don't make sense) stays out for that.
   - **Done**: offers to run step 5.
7. **Depth maps (optional): Make-DepthMaps.cmd.** A depth map for each new backdrop, so the large portrait's backdrop
   moves a little with the mouse. Writes the records.
8. **Variants (optional): Make-Variants.cmd.** Once set up (SETUP 10: FLUX.1 Kontext Dev and its exported workflow), it
   runs by itself: it reads `variants-wanted.csv` (the game writes it when you save: the expressions blink, smile,
   frown and pain for the pictures your people hold, a mark variant when someone gets a mark, such as a lost left eye,
   and for your own people the conditions: asleep, tired, hurt, wounded, ill, freezing, then overheated, starving,
   drunk, breaking and unconscious), your colonists first, makes each variant once into the library and writes
   the records. A variant is never
   made twice unless you delete it in the game.
9. **In the game:** Rimfolk's settings, **Reload the library**. The game also reads it at start-up and whenever a game
   starts or a save loads; nothing is looked at while you play.

Then play. Now and then send the lead `library\missing.csv` and the check's zip, so the next lists cover what is
missing.

## The lists

The lists come with Rimfolk Studio (0.8.0 on): its Lists page puts them into `batches\`, and a new list, more rows or a
change to a planner's rule is asked of the Studio. What each list holds:

Each row is one person and makes one picture (the style `style-rimworld-people` says one a row). Every list mixes its
kinds, baseliners about half:
- `characters-01` to `03`: distinct people of every kind, the common kinds most.
- `characters-04` to `07`: the common combinations of kind, sex, age and skin, mostly milder faces.
- `characters-08` to `16`: more people of every kind in your enabled mods (all 78 xenotypes that show and the
  creepjoiner, conjoined-twin and spirit races), each kind topped up by the same rule whatever your save holds, to the
  ages and skins it has fewest of. Every kind runs through all nine lists, so painting only some of them still gives
  each kind its share.
- `characters-17` to `20`: the likeliest colonists, 800 people: 60% baseliners, the rest the commonest kinds, nearly
  all young, adult or middle-aged. Most are broad (no role, look or mark, so any pawn of their kind, sex, age and skin
  can have them) but every face has one to three details of its own written in its row (a crooked nose, freckles, jug
  ears), which the game never holds a pawn to; about one in seven is a special case (a role, a look or a scar) that
  goes only to pawns who have it.
- `colonists.csv`: your own colonists' faces.
- `creatures-01` to `04`: animals, insects, mechanoids, Anomaly's entities, dryads and drones, 594 rows for 208 kinds:
  first every creature your save holds (three adults each), then the base game's and the DLCs' and the insects of
  Vanilla Factions Expanded - Insectoids 2 (two each); a male and a female where they look different (deer, elk,
  moose, peafowl, chickens), and a baby for each animal. The whole creature, square, cut out. The other kinds of your
  mods (about 1,500, most of them PokeWorld's) wait for their looks to be written (`catalogue/creatures.csv`). No
  Pokémon: you asked for them to be left out (24 November 2026), so the planner leaves the
  catalogue's group pokemon out by default and takes any of their rows out of the lists, every other row as it was;
  the Studio's planner can plan them again. Pictures of Pokémon already painted have no row any more.
- `creatures-05` to `08`: more variety for the same creatures, 542 rows: two more adults of each of the 208 (one of each
  sex where they look different) and one more baby for each animal that has one, worded as in the first four lists but
  each in a pose and a condition that creature's pictures don't have yet (lying at rest, looking back over its shoulder,
  feeding, curled up, mid-stride, seen from the front; a baby taking wobbly steps). Paint them like the first four.
  Another such round is asked of the Studio.
- `backdrops-gods.csv`: the gods' places, 56 backdrops: one for each kind of god (the harvest, the storm, the hunt,
  healing, war, the forge, roads, the dead, the night, the hearth, the green, the waters, the machine, the void) in each
  age (a ring of stones, a chapel, a church, a terminal). Paint it in `style-rimworld-backdrops`, like `backdrops.csv`.
- `gods-01.csv`: the gods as they are seen when they walk the map, 92 rows: the gods who show a body of their own (the
  Old Horn, the great cat, the toad, the Long White, the Guest, the horned man with the cup, the Rootmother risen, the
  dragon) in each age's dress, a figure for every other god by its kind in each age, and the thrumbos and lions that
  walk with the Old Horn and the great cat. The run paints it in the creatures' style by itself (whole figure, cut out).
- `priests-01.csv`: 64 priests of the common kinds, a man and a woman of each in every age's dress (painted markings, a
  plain robe, a cassock, ceremonial robes). Paint it like the people's lists.
- Rows for these three after a change to the gods are asked of the Studio.

## What the check checks, and what it can change

The check never changes a picture's **kind** (race or xenotype): the form does not even ask for it, so a hussar stays a
hussar however human the model thinks they look. It never changes the skin or hair colour, the hairstyle, the beard, the
build or the clothes either: those come from the row. What it can change, by what the model sees:

| It sees | It writes | Marked |
|---|---|---|
| no one, or a person in a backdrop | the picture out | ask |
| two or more people (not twins) | the picture out | yes |
| a figure cut off at the side (the picture itself, no model) | the picture out | ask |
| limbs that don't make sense (an extra arm or hand, six fingers, a limb belonging to no one) | held back until you keep it | hold |
| the other sex | the sex (a picture made for one colonist: out) | ask |
| an age two bands off / three or more | the age band | ask / yes |
| headwear the row lacks | headwear | yes (ask for a colonist's own) |
| ears, horns, a tail, glowing eyes, scales or fur the row lacks, or not those it has | the picture out | ask |
| an eye patch, a lost or bionic eye, an artificial arm the row lacks | the mark | yes |
| none of the row's eye or arm marks | the mark taken away | ask |
| a weapon, armour, a crown, a book or tools with no matching work | a role (warrior, soldier, hunter, leader, scholar ...) | yes, ask if the row has another |
| wings / fire or magic the row lacks | the wings' look / a magic look | yes or ask / ask |
| none of the row's looks or role | the look or role taken away | ask |

Lines marked **yes** and **ask** are applied (ask only says the check was less sure); **hold** waits for you on the
review page; a line you mark **no** is never applied. Your corrections in the game ("About this picture") are never
changed by the check and always win.

## How the game picks a picture

For each pawn the game reads their race, xenotype, sex, age, skin, hair, traits, genes, injuries and work, then:
- **Must match:** the kind (race and xenotype), the sex, the age band, the skin tone (the same or the next of eight; an
  unusual skin by its colour). A picture with a look, a role or a mark goes only to pawns who have it; a pawn with one
  may still take a plain picture.
- **Pointed ears** (the image model often paints them): for a fantasy race (every kind but the 6 the catalogue calls
  human-looking, `kinds.csv` column `humanLooking`: baseliners, dirtmoles, highmates, hussars, random custom xenotypes
  and the conjoined pair) they never count against a picture, so keep such pictures. For someone who looks human, a
  picture with pointed ears goes only to a person whose genes give them pointed ears.
- **The closest of the rest:** the hair colour first, then hairstyle, beard, build, the clothes' age and kind, features.
- The pick is kept for that pawn (saved with the game); your colonists' faces are kept for them alone.
- **A new picture made for one of your people** (for example "a picture of this person as they are" in Rimfolk Studio)
  reaches them by itself: when the library has changed and a picture no one else keeps fits them nearly exactly, and
  clearly better than the one they have, it becomes theirs and a message says so. Not if you chose their picture by
  hand. Such a picture is otherwise like any other: in another game it goes to whoever fits it best.
- No picture passes the rules: the game's own drawing, and a line in `missing.csv`.
- **What they go through:** where their picture has a painted condition (asleep, tired, hurt, wounded, ill, freezing,
  overheated, starving, drunk, breaking, unconscious), it shows in place of the picture while that holds, on the
  portraits the setting "What they go through, on their portraits" covers: asleep while they sleep (instead of the eyes
  held shut), tired when their rest is nearly gone, hurt and wounded by their wounds, bleeding and pain, ill by a
  sickness or an infection, freezing and overheated by hypothermia and heatstroke, starving by malnutrition, drunk by
  alcohol or a drug's high, breaking near or in a mental break, unconscious when downed. When several hold, the most
  serious: unconscious, breaking, wounded, ill, freezing, overheated, starving, asleep, hurt, drunk, tired. Meanwhile they
  don't blink or change expression, and the drawn effect for it stays faint. A condition stays at least three seconds,
  unless a more serious one comes.

**Animals and creatures** (with the setting "Painted pictures of animals and creatures" on): select one and the large
portrait at the inspect pane shows its painted picture in front of the backdrop of where it is, if the library has
one of its kind. It must match the kind and the age (a baby a baby's; a juvenile its own, else an adult's) and the sex
where the picture shows one; the same picture each time for the same animal, the pictures of a kind shared evenly.
Click it for the game's information on the animal; right-click: **Choose another picture** (kept for it) or **Delete
this picture for good**. A creature with no picture of its kind shows no portrait and is a "creature" line in
`missing.csv`, except a Pokémon: those aren't painted, so they are never listed.

**The gods** (from `gods-01`): a god walking the map as itself (not in disguise) shows its own picture when selected,
in the colony's age where there is one; a god without pictures of its own shows the figure of its kind; with none of
either it keeps the ordinary picture of whatever body it walks in. The beasts that walk with a god show its beasts'
pictures, else their kind's. **The gods' places** (from `backdrops-gods`): someone praying, keeping a rite or swearing
at a god's place, or standing in the room of one (its temple), has the backdrop of that kind of god's place behind them,
the colony's age first; those backdrops are never used anywhere else. **Priests**: a picture with the role priest goes
to people whose backstory, faith role or the colony's priest seat makes them one.

In the game: right-click the large portrait for **Choose another picture**, **About this picture** (its description,
why it was chosen, its variants, corrections: race, xenotype, sex, age, skin, hair, beard, build, clothes, features,
marks, looks, work, headwear), **Keep for training**, **Delete for good**, and **Dismiss this variant** for the
variant shown now (a condition, the expression on the face, a mark variant): it is deleted the way a picture is, the
picture and its other variants stay, and the next save asks for it again in `variants-wanted.csv`, so the Studio makes a
new one. A picture made for one of your people (their name in the `pawn` column) is theirs alone in a game where someone
of that name is in your colony; in another game, where no one has that name, it is an ordinary picture for whoever fits
it best. On the starting pawns' page each pawn's
painted portrait shows beside the page (right-click: choose another); the pawns you start with keep theirs.

## Training the style again

Right-click a picture that is just right, **Keep for training** (20 to 40, varied), then **Make-TrainingSet.cmd**:
writes `training\set\`, each picture on dark grey with its caption, for FluxGym. Details: STYLE-RIMWORLD.md, "Training
again".

## Updating the kit

The scripts and the lists come with Rimfolk Studio (since 24 November 2026); a kit zip, where one is still made, takes
its scripts from the Studio. Unzip the newest kit over your `Rimfolk-portraits` folder and put its `Copy-ToLibrary`
files into ComfyUI's `output\Rimfolk`. It replaces the scripts, the guides and the lists it has; it never touches your library (pictures,
records, corrections, checked answers), your exported workflows, your settings files (`check-settings.json`,
`copy-settings.json`) or anything in ComfyUI. Keep the lists you have made pictures from even if a kit no longer has
them.
