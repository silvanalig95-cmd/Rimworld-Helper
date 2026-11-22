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

The direction (the player, 22 November 2026, after the first test: style C liked best but "way too realistic ... it
should fit the RimWorld style much more ... look at the actual RimWorld loading screens ... the DLC images ... the
storytellers", with the game's own key art, the Biotech and the Ideology covers as references): the game's own comic
key-art look, described in words (no picture of the game's is copied or fed in):
- **Linework**: clean bold black ink outlines of varied weight; inked detail on faces (eye bags, cheek and nose lines,
  stubble, scars, wrinkles); hatching only in deep shadow.
- **Shading**: cel shading in two or three hard-edged tones, a subtle gradient inside the shadow shapes.
- **Colour and light**: rich but grounded colours, earthy for the poor and the tribal, cleaner for spacers; a cool cyan
  or blue rim light from behind, a warm key light from the front left.
- **Faces**: angular and characterful, slightly exaggerated (a strong nose, defined cheekbones, real asymmetry), never the
  generic pretty face: every person gets features of their own (pillar 2, face).
- **Frame**: a medium close-up cut by the picture's bottom edge at the belt (1024 x 1280), the head near the top and about
  a third of the picture's height, the body and arms running past the edges (so no picture ends in a cut-out bust; "from
  the waist up" alone gave nearly the whole figure, legs too), one person, bare-headed, on one flat, even dark blue-grey
  colour that is cut away afterwards (see "Backgrounds"). Said only positively: FLUX reads "no border, no hat" as a call
  for a border and a hat. Poses keep to what fits that frame (a staff gripped at chest height, a bow over the shoulder).
- **Holding the style**: every prompt starts with the style and ends with it repeated (`style_end`), so a reptile, an
  elf or a child is drawn the same way as a baseliner. FLUX ignores the negative prompt at its usual settings, so what
  matters is said positively. Once 20 to 40 pictures are chosen, a small style LoRA trained on them on the player's card
  holds the look for every later picture.
- **Chosen** (the player, 22 November 2026, after the LoRA tests: "this Henry Daubrez one, the style L9S looks really,
  really cool ... these paintings ... already have a lot of character just from the style. So that is the one I want to
  go with"): the Henry Daubrez painterly LoRA (Civitai 696050, `FLUX-daubrez-DB4RZ.safetensors`, 0.9) with the SameFace
  Fix at -0.7, loose painted brushwork in muted colours; `batches/style.json`. Earlier candidates (`style-A` to `L9s`)
  stay for reference.
- **Children and babies**: the same style, softer and rounder features as fits their age, never made to look older.
- **What varies, inside the style**: wealth and hardship (pillar 4), the age of their dress, the kind of person, the angle,
  the pose, the expression and the face. Never the brushwork, light or frame.

## Pillar 2: the person (what every picture's record says)

| Axis | Words |
|---|---|
| kind | a xenotype or race, by its defName (`Baseliner`, `DevDesigner_XenotypeDarkElves`, `VRESaurids_Saurid` ...) |
| sex | male, female |
| age | baby (0 to 1), toddler (1 to 3), child (4 to 12), teen (13 to 17), young (18 to 29), adult (30 to 44), middle (45 to 59), old (60 to 74), elder (75+) |
| build | slim, average, heavy, muscular |
| skin | a colour (and for scales, fur or bark the same) |
| hair colour | a colour |
| hair | bald, buzzed, short, medium, long, braids, tied, bun, topknot, mohawk, curly, dreadlocks |
| face hair | none, stubble, short beard, full beard, moustache, goatee |
| features | from the kind: pointed ears, long ears, fins, frills, horns, tusks, snout, fur, scales, gills, glowing eyes, markings, extra eyes ... |
| marks | lost eye, eye patch, bionic eye, scar, burn, missing ear, tattoo |
| expression | calm, warm, stern, weary, wary, proud, cheerful, grim |
| face shape | oval, round, square, long, narrow, heart, diamond, gaunt, broad |
| nose | straight, hooked, crooked, broad, flat, snub, upturned, bulbous, long, broken |
| eyes | deep-set, hooded, heavy-lidded, wide-set, close-set, narrow, round, almond, with dark circles, crinkled |
| brows | thick, sparse, arched, straight, heavy, scarred, joined |
| mouth and jaw | thin lips, full lips, wide mouth, small mouth, lopsided grin, overbite, gap teeth, weak chin, strong jaw, cleft chin |
| skin detail | freckles, moles, acne scars, pockmarks, wrinkles, crow's feet, sunburn, stubble shadow, birthmark |
| facing | front, left, right, three-quarter left, three-quarter right |
| pose | arms crossed, arms folded, hands clasped, a hand on the hip, a hand on a weapon, a weapon on the shoulder, the tool of their trade in hand, holding something of their own up to the chest, leaning forward, hunched, standing square; the head tilted or the chin raised |

Every new person in a batch gets two or three face words drawn at random (and kept for that character), so no two
read alike and none falls back to the generic face. The pose (the player, 22 November 2026: "the poses ... should maybe
also allow some variety ... give character ... more fitting to the pawn and the traits and how they look like") comes
from the person: their traits, work and bearing (a guarded scout with arms crossed and a sidelong look, a calm surgeon
with hands clasped, a warrior with a sword on the shoulder); it goes in the batch list's `pose` column. Hands show now
and FLUX gets some wrong: those pictures are thrown out.

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

## Pillar 6, later: animals and other creatures

The player: "babies/kids should also have images, later also animals & other creatures". Babies and children are part of
pillar 2 from the start (a baby or toddler as a head-and-shoulders picture in a blanket, sling or small clothes). Animals,
mechanoids, insectoids and the other creatures get a library of their own after the people: one look sheet per species
(the game's and every mod's animals, read from their defs the same way as the kinds), pictures by species, young or grown,
sex where it shows, colour or coat variant, and marks (a lost eye, a scar, a bionic part), in the same style bible with a
"creature" framing (head and chest, the animal's own pose). A bonded or named animal keeps its picture like a colonist.

## Backgrounds

The player, 22 November 2026: "Is there like a plan to have transparent backgrounds and kind of fill this with something
else? Or ... background images that can be applied to each of the kind of character images ... It shouldn't ... distract
too much, but just having a blank color can also be a bit ... boring", and then: "I will want to have hundreds, if not
thousands of different images ... So I don't know if removing a background for each of them anyways is a sustainable
solution. I mean, I cannot upload that many images to you to edit."

- **Cut out on the player's computer, in the same run**: a background remover in the ComfyUI workflow (BiRefNet, from
  the ComfyUI-RMBG node pack; `SETUP-ComfyUI.md` step 6) saves a transparent copy of every picture next to it
  (`..._cut.png`), with no work per picture. Tested here on the first five Daubrez pictures: clean edges even on the
  loose, brushy hair, and the painted seal the style sometimes adds in a corner goes with the background.
- **Backdrops, in the same style**: `batches/backdrops.csv` with `style-backdrop` (no cut-out): twelve kinds of room in
  the four ages of the colony (tribal, medieval, industrial, spacer: bedroom, barracks, hospital, prison cell, throne
  room, dining room, common room, kitchen, workshop, study, shrine, storeroom), sixteen outdoor looks that the player's
  56 biomes fall into (temperate and boreal forest, tundra, ice, grassland, shrubland, desert, jungle, swamp, coast,
  wasteland, volcanic, glowforest, cave, space, mountain), and three plain painted grounds. 67 places, two of each.
- **In the game** (to build once the backdrops are made): behind the cut-out person, the backdrop of where they are: the
  room's kind and the colony's age indoors, the biome outdoors; day or night and the weather as a tint; drawn softer,
  darker and out of focus, with a light shadow behind the figure, so the face stays first. A plain painted ground is the
  fallback, and a setting chooses backdrops, plain, or the picture's own background.
- **How they go together** (tried on the player's first pictures, 22 November 2026, the player: "whether the
  backgrounds are then too noisy or ... distracting from the pawn ... think about how to best kind of put it
  together"): the backdrop as painted is too busy (a stone wall fights a saurid's quills); far back or only its colours
  loses the place. The player then: "I don't mind the as is painted version because the background images look really,
  really cool and I don't mind seeing more of it ... maybe ... a very, very small, minor, softened effect ... Perhaps
  there could be a little bit of a border around the colonists, kind of a line around it that fits, that highlights them
  a bit without losing details on the background." So: the backdrop's middle band behind the head, kept almost as
  painted (a blur of 0.3% of the panel, 86% brightness, 92% colour, a light darkening at the corners), and the person
  set off by its own edge: a soft dark halo just round them, a faint pale edge (which keeps dark hair apart from dark
  beams) and a thin dark ink line, all following the cut-out's shape. A pale line on its own looks like a sticker and is
  left out. Optional: the person tinted lightly (about a fifth) towards the room's light. The crop
  follows the panel's size: at 96 pixels the head fills 45% of it, at 360 pixels 30%, so a small panel shows the face
  and a large one the pose; the head is found from the cut-out's own shape (its top and the neck).
- **Nothing of this is baked into the pictures** (the player: "if we do have to regenerate every single image again ...
  Could this be adjusted also later on, even with a large library"): the library keeps each person as a plain cut-out
  and each backdrop as painted; the game picks the backdrop, softens it as the setting says, draws the halo, pale edge
  and ink line from the cut-out's own shape, crops by the panel's size, and keeps the result until the pawn's place, the
  panel's size or a setting changes. So the backdrop's softening (as painted, light, softened), the line round the
  person (off, subtle, stronger) and the light tint are settings, changed in play for every picture at once. The pack
  stores pictures large enough for the 360-pixel panel; the full-size pictures from ComfyUI stay with the player as the
  archive, so even the pack's size can change by packing again, never by making the pictures again.

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

Each round: the player runs a batch (`Tools/PortraitArt/Run-PortraitBatch.ps1`); ComfyUI makes each picture and its
cut-out. For the first rounds the player zips the output folder with "Zip for upload" and sends it, and the lead checks
the look. For the hundreds and thousands after, every step per picture runs on the player's computer by itself (the player: "I
cannot upload that many images to you"): the chosen pictures go into the library folder, `Update-PortraitLibrary.ps1`
writes a record for each from its batch row, and Rimfolk reads the folder directly (`SETUP-ComfyUI.md` step 7).

**An evolving library** (the player: "a constantly evolving database of images for backgrounds, pawns, and also maybe
animals and creatures at a later stage ... include what is missing and not ... go through the entire list another
time"): the batch lists only ever grow; Rimfolk writes `library/missing.csv` (the kinds, ages, rooms and biomes it met
without a picture); the lead turns it into new rows; the batch tool makes only the rows the library has no picture of.
A new kind of thing (animals, creatures) gets a batch list and record columns of its own the same way.
