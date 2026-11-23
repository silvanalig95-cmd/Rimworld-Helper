# The portrait catalogue

The player, 22 November 2026: "create a catalog or a library of how all the possible combinations ... starting out with
the likeliest ones, and then over time growing, and including even more, also, I guess, rarer combinations ... keep
track of what we added ... all the races and how old they are, how old they look, gender". And: "I'm going to be
running the same scripts over and over again ... script everything in a way that doesn't even create name duplicates".

This folder is the list of everything a person in your game can look like, and the record of which pictures exist. It
is built from your own mod list (`docs/player-mods`, the mods you have enabled) and your save, League of Toba (day 138,
405 people: 346 alive, 59 dead). A script reads it and writes the next batch lists for ComfyUI, so you never write a
prompt by hand and no two pictures ever get the same name.

## What is in it

| File | What it holds |
|---|---|
| `kinds.csv` | Every kind of person your game can have: the 79 xenotypes of your enabled mods and the 3 humanlike races besides the human (Anomaly's creepjoiners, Conjoined Twins' conjoined pair, A RimWorld of Magic's spirit). For each: how common it is (how many your save holds, and a weight from 0 to 1 that also counts the factions that bring it), its wave (1 to 5), the sexes it comes in, its ages, how it ages (ageless or not), the skins and hair colours it can have, a look in plain words for the prompt, and for kinds whose genes change the head or body (a heavy brow, a gorilla's size, a gaunt skull, an animal head) that shape in plain words (`shape`, used by the characters lists). Kinds your save holds have their look from their real genes; the others are marked `guessed`. |
| `looks.csv` | Everything else a waist-up picture can show, 146 looks: faces and bearing from traits (a drinker, a pyromaniac, a psychopath, a beauty, a brawler ...), A RimWorld of Magic's classes (a fire mage with a flame in hand, an ice mage, a necromancer ...), psycasters, implants and conditions (a bionic arm, a clouded eye, a blindfold ...), genes that show (antennae, a third eye, mandibles ...), and 23 special looks that only their own pictures fit (conjoined twins, wings, extra limbs, ghouls, the undead, warcaskets). Each says what the game finds it by (traits, genes, conditions or a body) and how common it is in your save. |
| `marks.csv` | The marks of the body the game reads by itself: a lost eye, an eye patch, a bionic eye, a missing ear, a scar or a burn on the head. Two more are proposed (a missing arm, a face tattoo) and wait for the game to read them. |
| `roles.csv` | The 18 roles a person can have (scholar, doctor, soldier, warrior, hunter, scout, builder, miner, crafter, smith, farmer, herder, cook, artist, trader, leader, priest, labourer), how common each is in your save, and what shows it in each of the four ages of dress (a medieval scholar's book, an industrial one's lab coat, a spacer one's data slate). The game reads a person's role from their adult backstory, else their best skill with a passion; children have none. |
| `COVERAGE.md` | Written by the planner: what the library and the lists hold against the catalogue, by wave and kind, the gaps first, and the characters lists by wave. |

## How a picture finds its person

- **Kind, sex and age first.** A picture only ever goes to its own kind (a dark elf picture never to a human), its own
  sex, and someone who looks that age. How old someone looks follows their kind: an elf of 300 can still look 25.
- **Plain pictures are the defaults.** A picture with no role and no look fits anyone of its kind, sex and age.
- **A role or a look goes only to people who have it.** A picture of a fire mage holding a flame goes only to fire
  mages; a soldier's picture only to soldiers. So nobody casts a spell they can't, or holds a rifle they never use.
- **Special looks go both ways.** A conjoined pair, someone with wings or with a crab claw for an arm only gets a picture
  made for that look, and those pictures go to no one else. With none yet, the game shows its own drawing and notes it
  in `missing.csv`.

## Character, and only what they have

Every picture gets a face of its own (two or three face words), an expression and a bearing, and these vary widely:
eccentric, funny, intense, shy, proud, weathered, wild-haired. The rarer kinds and the later rounds get the widest range.
But a picture never shows a prop, a power or gear that its role and looks don't stand for: a plain picture holds nothing
a skill would need, a fire is only in a fire mage's hand, a rifle only in a soldier's. Anything a prompt adds that the
game should check is written into the row's `looks` or `role`.

## The loop

1. **Make the next list.** Run `Run-PortraitBatch.ps1`, choose the list (start with `people-01`) and the style
   `style`. Each row makes two pictures.
2. **Keep the good ones.** In ComfyUI's `output\Rimfolk\<list>` folder, throw out the misses (wrong age, wrong kind,
   broken hands, something held or shown that the row doesn't stand for), then double-click `Copy-ToLibrary.cmd`: it
   copies the rest into `library\<list>\` (for example `library\people-01\`) with the names ComfyUI gave them.
3. **Update the library.** `Copy-ToLibrary` writes the records itself (by hand: `Update-PortraitLibrary.ps1`), then
   "Reload the library" in Rimfolk's settings.
4. **Play.** Rimfolk writes `library\missing.csv` with what it needed and had no picture for.
5. **Send** your save, `library\missing.csv` and `library\records.xml` when you send a save anyway.
6. **The lead plans the next lists**: the census reads the save into the catalogue, the planner writes the next lists
   (what `missing.csv` asked for first), and `COVERAGE.md` shows what is still missing.

To try again for a row whose pictures you all threw out, run the same list again and answer yes to "make only the rows
that have none yet". Lists are never rewritten: new rows always go into new lists, so the pictures already in
`library\<list>\` keep their rows.

## The order of the lists

- `needs-people-NN`: what the game asked for in `missing.csv`. Run these first.
- `characters-NN`: the characters lists (below), every kind with a face and a mood of its own. Run them beside the
  `people` lists, in any order.
- `people-NN`: everything else, mixed (the player, 22 November 2026: not "first white boy, white man, white old man
  ... Not that for the first few hours I only have like white humans"). The kinds take turns, each as often as the
  square root of its count in your world, so baseliners come up most (about a third of the first hundred pictures),
  dark elves next, and every kind the waves plan shows up within the first dozen. Inside a kind its sexes and age bands
  take turns, plain pictures before roles and looks. The waves still decide which kinds get planned: wave 1 is
  baseliners and dark elves; wave 2 hussars, neanderthals, impids, pigskins, wasters and saurids; wave 3 genies,
  starjacks, yttakin, sanguophages and the high and wood elves; wave 4 the kinds your save holds a few times (lesser
  fairies, avaloi, stoneborn, the apes, drakonori and others); wave 5 the rest, seen once or not yet.

The first lists (22 November 2026, mixed the same day): `people-01` to `04`, 728 rows (382 plain, 66 roles, 280 looks),
about 1,450 pictures with two each.

## The characters lists

The player, 22 November 2026: "i have to say many of the characters looks rather generic, please create me a new list of
characters to import with all kinds of variety that could appear in the game", and on the faces: "I just don't want this
traditional AI face ... everyone should have something individual about them. It doesn't have to be many things ...
Especially on women, this is much more noticeable", "Someone could have one, someone could have three ... no basic faces
... never a caricature ... especially for other races ... they don't have to be essentially a human in shape".

`characters-01` to `03` (599 rows, about 1,200 pictures) are a second set beside the `people` lists, made with
`plan_batches.py --phases characters`. They hold every kind `kinds.csv` plans, all five waves: 156 baseliners and 73
dark elves, 10 of each wave 2 kind, 6 of each wave 3 kind, 4 of each wave 4 kind, 3 of each wave 5 kind (2 for the
rarest), in every sex the kind comes in and its common ages; babies, toddlers and children for baseliners and dark
elves, children and teens for wave 2, teens for wave 3. Each row is a distinct person:
- **The face**: its shape and an ordinary, real skin, and one to three features of its own (a hawk nose, a lantern jaw,
  a gap between the teeth, heavy brows, laugh lines, freckles across the nose ...), now and then a little stronger than
  life (an especially big chin, huge ears), now and then a little uneven; the same pools for women and men, so a woman
  gets a big nose or a heavy jaw as often as a man. No two rows have the same face, and no row has a beauty word
  (beautiful, pretty, flawless, smooth, delicate ...): kinds whose look sheet has them are said without them here.
- **Hair, beard and build with shape**: a mohawk, an undercut, cornrows, a wild mane, a single long braid, a topknot;
  a braided beard, mutton chops, a handlebar moustache; wiry, barrel-chested, big-bellied, lanky. Each phrase says what
  the record's word says (a mohawk is short hair, dreadlocks long, a topknot a bun).
- **Clothes with character**, in the row's age of dress and wealth: a patched coat with mismatched buttons, a scarf
  wound high, a fur-lined cloak, a quilted flight jacket covered in patches, and now and then a trinket (a ring in one
  ear, a string of beads).
- **A mood and a pose to match**: sly, gleeful, sullen, haughty, shy, intense, weary, odd, gruff, friendly, proud,
  anxious, stoic, droll or wary, the rarer kinds more often odd or intense; and a short line that sums them up ("In
  short, a sly, wiry, hawk-nosed old man").
- **Other kinds in their own shape**: the kind's look sheet and its `shape` (a neanderthal's big head and low
  forehead, a gorilla's massive body and long arms, an animal head on a slim body), and features that fit it (a muzzle,
  a coat of fur, scales or bark instead of a human nose and skin).

Most rows are plain in the game's sense (named `..._char_...`): no role, no look and no mark but what the row records,
so anyone of the kind, sex and age can get them, and they hold nothing a skill or a power would need. Baseliners, dark
elves and the wave 2 kinds also have rows with a role or a look (76), written the same way and recorded in their
`role` or `looks` column so the game gives them only to those who have it. A few plain rows carry a mark (an eye patch,
a scar), recorded in `marks`. The rows' `kind`, `race` and `features` columns are their kind's, so the picture check
expects the horns, tails and wings their kind has. The lists are mixed the same way as the `people` lists (`mix`): the
rarer kinds come up early, so the third list is mostly baseliners and dark elves. A row may move to another list when lists are mixed again: the library finds a
picture's row in any list, so pictures already made keep their records.

`characters-04` to `07` are the second round (the player, 23 November 2026: "there should again be a lot of variety,
but it should also cover more of the general combinations"): a few pictures for each common kind, sex, age band and
skin tone, counted with the rows already there, most of them broad (fewer, milder features).

`characters-08` to `16` are the third round (1,632 rows, one picture each; the player, 23 November 2026: "Rather have
overall more variety with different kinds of characters", and "Please also really review my game and also mod list for
all the xenotypes that are available, not just from the saves I gave you ... I want variety from all of them available
if I now start a new save"). Every kind of the player's enabled mods (the 78 xenotypes that show, all but alpha genes'
random one, and the creepjoiner, conjoined-twin and spirit races; `docs/player-mods`, active rows) is topped up by the
same rule, whatever its count in the save: to one picture for each sex, grown band from young to old and up to three of
its skins (at least 12, at most 20, `round3_floor`), and at least 4 new ones however many it has (`ROUND3_*` in
`plan_batches.py`). Baseliners as many as all the other kinds together, so each list is about half baseliners with 66 to
76 kinds. Each row goes to the sex and age band where its kind has the fewest pictures against how common the band is,
then to the skin with the fewest there; clothes lean to tribal and medieval dress without leaving the others out, about
half are broad, and a few of the common kinds' rows have a role. All nine lists mix every kind evenly, so painting only
some of them still gives every kind its share. Not in them: the Forgotten Realms and Profaned xenotypes, whose mods are
subscribed but not enabled.

## The creature catalogue and lists

`creatures.csv` (the player, 23 November 2026: "also please create lists for animals and creatures and implement image
display for them too", and "also include insects of course"): every animal, insect, mechanoid, entity, dryad, drone,
hybrid and Pokemon the player's enabled mods add (`docs/player-mods/races.tsv`, the active rows that aren't humanlike,
1,736), one row each: `creature` (the race's def name), `label`, `mod`, `group`, `size`, `saveCount` (how many the save
holds), `wave`, and by hand `stages` (`baby;adult`, or `adult` for those never seen young), `sexes` (`any`, or
`male;female` with `maleLook` and `femaleLook` where the sexes look different), `look` (what the picture shows, the
prompt's words) and `notes`. `creature_census.py [--save <save.rws>]` keeps it: new creatures added, gone ones left
out, the counts and waves written again, the hand columns kept.

Waves: 1 the creatures in the save (62), 2 the base game's and the DLCs' and the insects of Vanilla Factions Expanded -
Insectoids 2 (156), 3 the other animal and creature mods (360), 4 genetic hybrids, magic summons and other mods'
mechanoids and drones (270), 5 PokeWorld's Pokemon (888). Waves 1 and 2 have their looks (218 creatures); a creature
without a look is not planned.

`plan_creatures.py` writes `creatures-NN` (one picture a row, the whole creature, square, cut out; painted in
`style-rimworld-creatures`): three adults of each creature of wave 1, two of wave 2, one later; that many of each sex
where they look different; a baby for each animal and hybrid of waves 1 and 2; each a little different (its condition
and stance, by its name). Rows already listed are counted, so running again adds only what is missing; the save's
creatures come first, and every list holds many kinds. `creatures-01` to `04`: 624 rows for 218 kinds.

## Names

A row's name says what it shows: `p_<kind>_<m or f>_<age>_<role, look, plain or char>_<skin>_<hair>_<number>`, for
example `p_baseliner_f_adult_plain_fair_brown_01`, `p_darkelf_m_young_hunter_white_black_01` or a characters row
`p_korg_m_adult_char_green_bald_01` (`char` a plain row of the characters lists). A name is written once and
never changes or comes back for anything else, so the scripts can run as often as you like without a clash; every
picture's own file name adds the style and the run's date and time.

## For the lead: the tools

All in `Tools/PortraitArt`, Python 3, nothing to install:
- `save_census.py <save.rws> --update` (run with `python3 -I`; a save is someone else's data): counts the save's people
  in the catalogue's words and writes the counts into the tables (save count, weight, wave, ages, roles and dress ages
  for kinds seen 8 times or more, how common each look, mark and role is), and lists the kinds the save holds that
  `kinds.csv` has no row for.
- `plan_batches.py [--waves 1,2] [--phases defaults,roles,looks,variety] [--records records.xml] [--missing
  missing.csv] [--dry-run]`: the next lists and `COVERAGE.md`. Running it again with the same inputs adds nothing.
  `--phases characters` writes the characters lists instead (all five waves unless `--waves` says otherwise); their
  words (faces, hair, beards, builds, clothes, moods, poses) are in `character_words.py`.
- `looks_to_defs.py`: writes `Defs/PortraitLookDefs/PortraitLooks.xml` from `looks.csv` (one look def per row).
- `catalogue_check.py`: every name in the catalogue against the active rows of `docs/player-mods` ("OK").
- `test_portrait_catalogue.py`: the tools' tests ("OK").
- `creature_census.py`, `plan_creatures.py`: the creature catalogue and lists (above); `test_creatures.py` their tests.

## Adding a new mod's kinds

1. Rebuild `docs/player-mods` from the player's new collection (`Tools/mod_pool.py`, see its README).
2. Run the census on a save with the mod (`save_census.py ... --update`): it names the kinds it found without a row.
3. Add a row to `kinds.csv` for each: a short `slug` (small letters, never changed), the kind and race, the look in
   plain words starting with the kind ("a lapis: rocky ridges along the brow ..."), the skins and hair colours (a
   human skin word, or `slug=#colour=words`), how it ages, its features, and `guessed` yes until its genes are seen in
   a save. New looks the mod brings go into `looks.csv` the same way (a new word, never a changed one).
4. `catalogue_check.py` must say "OK"; run the census again to fill the counts, then the planner.
