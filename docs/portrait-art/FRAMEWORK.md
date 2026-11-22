# Rimfolk portraits: the framework

The player, 22 November 2026: "a plan, process, and a framework on how to create these pawn images ... represent as many
possible combinations as possible. All the different races, ages, genders, perhaps, even injuries ... haircuts ... expand
that continuously ... applicable to all characters ... that could even appear in the game ... The test examples should be
my actual pawns ... It should not be too realistic ... not like photographic style ... still fit a bit to the RimWorld
theme ... rugged survivalist, post apocalyptic ... If a pawn ... looks super poor and survived a lot of stuff, they
should ... look more rugged than if it's like a royal member who's super rich ... but it should always kind of follow
more or less the same style."

The framework has five pillars. Each is a fixed list of words that both the prompts and the library's records use, so a
picture made today and one made in a year describe themselves the same way, and new entries only ever add to a list.

## Pillar 1: the style bible (one look for every picture)

Fixed once the style test is done (below); every prompt starts with it, word for word.
- **Medium**: an illustrated, painted character portrait; not a photograph, not a 3D render, not anime.
- **World**: a rugged frontier of a far-future rim world: people who make do, mend, scavenge and survive; low-tech next to
  salvaged high-tech.
- **Palette**: earthy and muted (dust, rust, leather, wool, bone), one or two warm accents; saturated colour only where
  the person owns something bright.
- **Light**: soft key light from the upper left, a faint warm rim light, no harsh studio look.
- **Rendering**: visible brush texture, clean readable shapes, faces with real anatomy and character, slightly stylised.
- **Frame**: head and shoulders, the eyes about 40% from the top, the shoulders reaching the bottom edge, one person,
  a plain dark neutral background, no text, no border.
- **What varies, inside the style**: wealth and hardship (pillar 4), the age of their dress, the kind of person, the angle
  and the expression. Never the medium, palette treatment, light or frame.

## Pillar 2: the person (what every picture's record says)

| Axis | Words |
|---|---|
| kind | a xenotype or race, by its defName (`Baseliner`, `DevDesigner_XenotypeDarkElves`, `VRESaurids_Saurid` ...) |
| sex | male, female |
| age | child (5 to 12), teen (13 to 17), young (18 to 29), adult (30 to 44), middle (45 to 59), old (60 to 74), elder (75+) |
| build | slim, average, heavy, muscular |
| skin | a colour (and for scales, fur or bark the same) |
| hair colour | a colour |
| hair | bald, buzzed, short, medium, long, braids, tied, bun, topknot, mohawk, curly, dreadlocks |
| face hair | none, stubble, short beard, full beard, moustache, goatee |
| features | from the kind: pointed ears, long ears, fins, frills, horns, tusks, snout, fur, scales, gills, glowing eyes, markings, extra eyes ... |
| marks | lost eye, eye patch, bionic eye, scar, burn, missing ear, tattoo |
| expression | calm, warm, stern, weary, wary, proud, cheerful, grim |
| facing | front, left, right, three-quarter left, three-quarter right |

## Pillar 3: the kinds (any race or xenotype, including ones not met yet)

Every kind has a look sheet: what it adds to a human picture (or replaces), in plain words for prompts, and which words of
pillar 2 it uses. The lead writes it from the kind's own files (its genes and description; a collector script gathers them
for mods added later), so a kind new to the game gets a sheet the same way. Until a kind has pictures of its own, a near-
human kind borrows the baseliners' pictures and a far-from-human kind keeps the game's own face (the matcher already does
this). The order of work follows the player's world (League of Toba, day 138): baseliners, dark elves, then hussars,
neanderthals, impids, genies, wasters, saurids, sanguophages, pigskins, starjacks, the elves, avaloi, yttakin, fairies,
merren and the rest of the 79 active xenotypes.

## Pillar 4: life on them (wealth and hardship, in the same style)

Two short phrases a prompt adds, read in the game from the pawn (its clothes' worth and quality, a royal title, a slave's
or prisoner's state, its mood and health, its backstory):

| Wealth | phrase |
|---|---|
| destitute | ragged, patched clothing, a gaunt face, grime |
| poor | worn, mended practical clothing |
| common | practical clothing, clean but well used |
| prosperous | good tailored clothing, well groomed |
| noble | rich fabrics with fine trim and embroidery, jewellery, an air of command |

| Hardship | phrase |
|---|---|
| sheltered | smooth skin, rested eyes |
| ordinary | lightly weathered |
| hard | wind-burned, weathered skin, tired eyes |
| brutal | deep lines, old scars, a hard stare |

And the age of their dress: tribal (hide, fur, woven fibre, bone, beads), medieval (wool, linen, leather, mail),
industrial (canvas, denim, wool coats, flak vests), spacer (synthetic flight suits, sleek armour, soft-glowing seams).

## Pillar 5: characters and variants

A picture is a character. The same character can have variants made from it (with the same seed or from the picture
itself): a lost eye or eye patch, a fresh scar, a burn, a bionic eye; older (one age step); richer or poorer. A pawn keeps
its character for life and moves between its variants as its body and life change.

## The process

1. **Set up** the image tool on the player's computer (`SETUP-ComfyUI.md`).
2. **Find the style**: the player's ten colonists (`batches/colonists.csv`) in five candidate styles
   (`batches/style-A.json` to `style-E.json`), two pictures each. The player picks; the lead writes the final style bible
   into `batches/style.json`, which every later batch uses.
3. **The colonists' own portraits**: the ten again in the chosen style, four each; the player keeps the best; the lead puts
   them in the game tied to those pawns.
4. **The starter set**: the commonest looks (baseliners of every age, sex, skin and hair in the four ages of dress, then
   the dark elves), a few hundred, made overnight from batch files the lead writes.
5. **Waves**, by the kinds' order above, then variants (pillar 5) for the characters most used.
6. **Growing for good**: the lead's coverage report compares the library with the pawns in a save and lists the gaps (a
   kind, an age, a look with too few pictures); each new batch fills the biggest ones. A new mod's kind gets a look sheet
   and its first batch the same way.

Each round: the player runs a batch (`Tools/PortraitArt/Run-PortraitBatch.ps1`), zips the output folder with "Zip for
upload" and sends it; the lead cuts out the background, lines them up, describes each picture from what it shows, writes
their records and releases them.
